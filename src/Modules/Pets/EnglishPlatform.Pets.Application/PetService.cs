using System.ComponentModel.DataAnnotations;
using EnglishPlatform.BuildingBlocks.Events;
using EnglishPlatform.BuildingBlocks.Results;
using EnglishPlatform.BuildingBlocks.Security;
using EnglishPlatform.LearningSessions.Contracts;
using EnglishPlatform.Pets.Contracts;
using EnglishPlatform.Pets.Domain;
using EnglishPlatform.Users.Contracts;
using EnglishPlatform.Vocabulary.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EnglishPlatform.Pets.Application;

public interface IPetsDb
{
    DbSet<Pet> Pets { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record PetQuestDto(string Kind, int Target, int Progress, int Treats, int Xp, bool Done);

public sealed record PetDecorDto(string Key, string Counter, int Target, int Progress, bool Unlocked);

public sealed record PetDto(
    string Species,
    string Palette,
    string Name,
    string? Accessory,
    string Mood,
    string Stage,
    int Satiety,
    int Affection,
    int Treats,
    int Level,
    int Xp,
    int LevelXp,
    int NextLevelXp,
    double DaysSinceStudy,
    bool StudiedToday,
    int Streak,
    int BestStreak,
    int PatsLeft,
    int LessonsCompleted,
    int TreatsEaten,
    int WordsAdded,
    int TextsRead,
    int QuizCorrect,
    int GamesPlayed,
    int QuestsCompleted,
    IReadOnlyList<PetQuestDto> Quests,
    IReadOnlyList<PetDecorDto> Decor,
    IReadOnlyList<string> Accessories,
    DateTimeOffset CreatedAt);

/// <summary>Ответ на учебное дело: питомец и просьбы, которые только что выполнены (для поздравления).</summary>
public sealed record PetProgressDto(PetDto Pet, IReadOnlyList<PetQuestDto> Completed);

public sealed record AdoptPetRequest(
    PetSpecies Species,
    [property: Required(ErrorMessage = "Выберите окрас")]
    [property: MaxLength(Pet.MaxPaletteLength)]
    string Palette,
    [property: Required(ErrorMessage = "Придумайте имя")]
    [property: MaxLength(Pet.MaxNameLength, ErrorMessage = "Имя не длиннее 24 символов")]
    string Name);

public sealed record UpdatePetRequest(
    [property: Required(ErrorMessage = "Выберите окрас")]
    [property: MaxLength(Pet.MaxPaletteLength)]
    string Palette,
    [property: Required(ErrorMessage = "Придумайте имя")]
    [property: MaxLength(Pet.MaxNameLength, ErrorMessage = "Имя не длиннее 24 символов")]
    string Name,
    [property: MaxLength(16)]
    string? Accessory);

/// <summary>Сделанное в приложении дело, о котором сообщает клиент: игра, прочитанный текст.</summary>
public sealed record PetActivityRequest(PetActivity Activity);

public sealed record PetQuizAnswerRequest(bool Correct);

public static class PetErrors
{
    public static readonly Error NotFound = Error.NotFound("pet.not_found", "Питомца ещё нет");
    public static readonly Error AlreadyAdopted = Error.Conflict("pet.already_adopted", "Питомец уже есть");
    public static readonly Error UnknownPalette = Error.Validation("pet.unknown_palette", "Такого окраса нет");
    public static readonly Error EmptyName = Error.Validation("pet.empty_name", "Придумайте имя");
    public static readonly Error LockedAccessory = Error.Validation("pet.locked_accessory", "Этот аксессуар откроется на следующих уровнях");
    public static readonly Error NoTreats = Error.Conflict("pet.no_treats", "Угощения закончились — пройдите занятие, чтобы заработать ещё");
    public static readonly Error Full = Error.Conflict("pet.full", "Питомец сыт — покормите попозже");
    public static readonly Error Away = Error.Conflict("pet.away", "Питомец ушёл гулять — вернётся, когда вы позанимаетесь");
    public static readonly Error ServerActivity = Error.Validation("pet.server_activity", "Это дело засчитывается само");
}

/// <summary>День пользователя по его часовому поясу: от него зависят просьбы и серия занятий.</summary>
public sealed class PetDays(IUserLearningSettings settings, TimeProvider time)
{
    public async Task<DateOnly> TodayAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        UserLearningSettings s = await settings.GetAsync(userId, cancellationToken);
        TimeZoneInfo zone = TimeZoneInfo.TryFindSystemTimeZoneById(s.TimeZone, out TimeZoneInfo? found) ? found : TimeZoneInfo.Utc;
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
    }

    public DateTimeOffset Now => time.GetUtcNow();
}

/// <summary>Питомец текущего пользователя: завести, покормить, погладить, ответить на его вопросы, отчитаться о деле.</summary>
public sealed class PetService(IPetsDb db, ICurrentUser currentUser, IEventBus eventBus, PetDays days)
{
    public async Task<PetDto?> GetAsync(CancellationToken cancellationToken)
    {
        Pet? pet = await FindAsync(cancellationToken);
        if (pet is null)
        {
            return null;
        }

        DateTimeOffset now = days.Now;
        DateOnly today = await days.TodayAsync(pet.Id, now, cancellationToken);
        DateOnly before = pet.QuestDay;
        pet.EnsureQuests(today);
        if (pet.QuestDay != before)
        {
            await db.SaveChangesAsync(cancellationToken); // новые просьбы на новый день
        }

        return ToDto(pet, now, today);
    }

    public async Task<Result<PetDto>> AdoptAsync(AdoptPetRequest request, CancellationToken cancellationToken)
    {
        Guid userId = currentUser.UserId;
        if (await db.Pets.AnyAsync(p => p.Id == userId, cancellationToken))
        {
            return PetErrors.AlreadyAdopted;
        }

        if (!Pet.IsKnownPalette(request.Species, request.Palette))
        {
            return PetErrors.UnknownPalette;
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return PetErrors.EmptyName;
        }

        DateTimeOffset now = days.Now;
        DateOnly today = await days.TodayAsync(userId, now, cancellationToken);
        Pet pet = Pet.Adopt(userId, request.Species, request.Palette, request.Name, now);
        pet.EnsureQuests(today);
        db.Pets.Add(pet);
        await db.SaveChangesAsync(cancellationToken);
        await eventBus.PublishAsync(new PetAdopted(userId, request.Species.ToString(), now), cancellationToken);
        return ToDto(pet, now, today);
    }

    public async Task<Result<PetDto>> UpdateAsync(UpdatePetRequest request, CancellationToken cancellationToken)
    {
        Pet? pet = await FindAsync(cancellationToken);
        if (pet is null)
        {
            return PetErrors.NotFound;
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return PetErrors.EmptyName;
        }

        if (!pet.Restyle(request.Palette))
        {
            return PetErrors.UnknownPalette;
        }

        if (!pet.Wear(string.IsNullOrWhiteSpace(request.Accessory) ? null : request.Accessory))
        {
            return PetErrors.LockedAccessory;
        }

        pet.Rename(request.Name);
        await db.SaveChangesAsync(cancellationToken);
        DateTimeOffset now = days.Now;
        return ToDto(pet, now, await days.TodayAsync(pet.Id, now, cancellationToken));
    }

    public async Task<Result<PetDto>> FeedAsync(CancellationToken cancellationToken)
    {
        Pet? pet = await FindAsync(cancellationToken);
        if (pet is null)
        {
            return PetErrors.NotFound;
        }

        DateTimeOffset now = days.Now;
        PetActionResult result = pet.Feed(now);
        await db.SaveChangesAsync(cancellationToken);
        return result switch
        {
            PetActionResult.NoTreats => PetErrors.NoTreats,
            PetActionResult.Full => PetErrors.Full,
            PetActionResult.Away => PetErrors.Away,
            _ => ToDto(pet, now, await days.TodayAsync(pet.Id, now, cancellationToken)),
        };
    }

    /// <summary>Погладить: если на сегодня ласка уже набрана, питомец всё равно рад — просто без прибавки.</summary>
    public async Task<Result<PetDto>> PatAsync(CancellationToken cancellationToken)
    {
        Pet? pet = await FindAsync(cancellationToken);
        if (pet is null)
        {
            return PetErrors.NotFound;
        }

        DateTimeOffset now = days.Now;
        DateOnly today = await days.TodayAsync(pet.Id, now, cancellationToken);
        PetActionResult result = pet.Pat(today, now);
        await db.SaveChangesAsync(cancellationToken);
        return result == PetActionResult.Away ? PetErrors.Away : ToDto(pet, now, today);
    }

    /// <summary>Клиент сообщает о сделанном: сыграл в игру, дочитал текст. Занятия и новые слова засчитываются сами.</summary>
    public async Task<Result<PetProgressDto>> RecordAsync(PetActivityRequest request, CancellationToken cancellationToken)
    {
        if (request.Activity is not (PetActivity.Match or PetActivity.Blitz or PetActivity.Scramble or PetActivity.Reading))
        {
            return PetErrors.ServerActivity;
        }

        return await ProgressAsync((pet, today, now) => pet.Record(request.Activity, 1, today, now), cancellationToken);
    }

    public Task<Result<PetProgressDto>> AnswerQuizAsync(PetQuizAnswerRequest request, CancellationToken cancellationToken) =>
        ProgressAsync((pet, today, now) => pet.AnswerQuiz(request.Correct, today, now), cancellationToken);

    private async Task<Result<PetProgressDto>> ProgressAsync(Func<Pet, DateOnly, DateTimeOffset, IReadOnlyList<PetQuest>> action, CancellationToken cancellationToken)
    {
        Pet? pet = await FindAsync(cancellationToken);
        if (pet is null)
        {
            return PetErrors.NotFound;
        }

        DateTimeOffset now = days.Now;
        DateOnly today = await days.TodayAsync(pet.Id, now, cancellationToken);
        IReadOnlyList<PetQuest> completed = action(pet, today, now);
        await db.SaveChangesAsync(cancellationToken);
        return new PetProgressDto(ToDto(pet, now, today), completed.Select(ToDto).ToList());
    }

    private Task<Pet?> FindAsync(CancellationToken cancellationToken) =>
        db.Pets.SingleOrDefaultAsync(p => p.Id == currentUser.UserId, cancellationToken);

    public static PetQuestDto ToDto(PetQuest q) => new(q.Kind.ToString(), q.Target, q.Progress, q.Treats, q.Xp, q.Done);

    public static PetDto ToDto(Pet pet, DateTimeOffset now, DateOnly today)
    {
        int level = pet.Level;
        return new PetDto(
            pet.Species.ToString(),
            pet.Palette,
            pet.Name,
            pet.Accessory,
            pet.MoodAt(now).ToString(),
            pet.Stage.ToString(),
            (int)Math.Round(pet.SatietyAt(now)),
            (int)Math.Round(pet.AffectionAt(now)),
            pet.Treats,
            level,
            pet.Xp,
            Pet.XpForLevel(level),
            Pet.XpForLevel(level + 1),
            Math.Round(pet.DaysSinceStudy(now), 2),
            pet.LastStudyDay == today,
            pet.StreakOn(today),
            pet.BestStreak,
            pet.PatsLeft(today),
            pet.LessonsCompleted,
            pet.TreatsEaten,
            pet.WordsAdded,
            pet.TextsRead,
            pet.QuizCorrect,
            pet.GamesPlayed,
            pet.QuestsCompleted,
            pet.QuestDay == today ? pet.Quests.Select(ToDto).ToList() : [],
            Pet.Decor.Select(d =>
            {
                int progress = pet.CounterValue(d.Counter);
                return new PetDecorDto(d.Key, d.Counter.ToString(), d.Target, Math.Min(progress, d.Target), progress >= d.Target);
            }).ToList(),
            pet.UnlockedAccessories,
            pet.CreatedAt);
    }
}

/// <summary>Закончено занятие — угощения, ласка, опыт, серия дней, просьба «пройди занятие»; питомец возвращается с прогулки.</summary>
public sealed class FeedPetOnSessionFinished(IPetsDb db, PetDays days) : IIntegrationEventHandler<SessionFinished>
{
    public async Task HandleAsync(SessionFinished integrationEvent, CancellationToken cancellationToken)
    {
        Pet? pet = await db.Pets.SingleOrDefaultAsync(p => p.Id == integrationEvent.UserId, cancellationToken);
        if (pet is null)
        {
            return;
        }

        DateOnly today = await days.TodayAsync(pet.Id, integrationEvent.OccurredAt, cancellationToken);
        pet.OnLessonFinished(integrationEvent.Answered, integrationEvent.Correct, integrationEvent.Kind == "Mistakes", today, integrationEvent.OccurredAt);
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Пользователь сам добавил слово — это идёт в просьбу «добавь новые слова» и в достижение «книжная полка».</summary>
public sealed class CountWordForPet(IPetsDb db, PetDays days) : IIntegrationEventHandler<UserWordAdded>
{
    public async Task HandleAsync(UserWordAdded integrationEvent, CancellationToken cancellationToken)
    {
        if (!integrationEvent.ByUser)
        {
            return;
        }

        Pet? pet = await db.Pets.SingleOrDefaultAsync(p => p.Id == integrationEvent.UserId, cancellationToken);
        if (pet is null)
        {
            return;
        }

        DateOnly today = await days.TodayAsync(pet.Id, integrationEvent.OccurredAt, cancellationToken);
        pet.Record(PetActivity.AddWords, 1, today, integrationEvent.OccurredAt);
        await db.SaveChangesAsync(cancellationToken);
    }
}
