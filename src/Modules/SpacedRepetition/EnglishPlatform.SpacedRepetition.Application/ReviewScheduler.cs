using EnglishPlatform.BuildingBlocks.Events;
using EnglishPlatform.SpacedRepetition.Contracts;
using EnglishPlatform.SpacedRepetition.Domain;
using EnglishPlatform.Vocabulary.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EnglishPlatform.SpacedRepetition.Application;

public interface ISpacedRepetitionDb
{
    DbSet<ReviewCard> Cards { get; }

    DbSet<ReviewLog> Logs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Слово считается проблемным, если его забывали после выучивания хотя бы столько раз.</summary>
public sealed class ProblemOptions
{
    public int ProblemLapses { get; set; } = 2;
}

public sealed class ReviewScheduler(ISpacedRepetitionDb db, IUserWordsReader words, FsrsScheduler fsrs, ProblemOptions problems) : IReviewScheduler
{
    public async Task<DailyQueue> BuildQueueAsync(
        Guid userId,
        int newLimit,
        int reviewLimit,
        DateTimeOffset dayStart,
        DateTimeOffset now,
        IReadOnlyCollection<Guid> problemWordIds,
        CancellationToken cancellationToken)
    {
        var problemWords = problemWordIds.ToHashSet();
        await SyncCardsAsync(userId, now, cancellationToken);

        List<ReviewLog> today = await db.Logs.AsNoTracking()
            .Where(l => l.UserId == userId && l.ReviewedAt >= dayStart)
            .ToListAsync(cancellationToken);
        int newDone = today.Count(l => l.StateBefore == CardState.New && l.Mode == CardMode.Recognition);
        int reviewsDone = today.Count - newDone;

        List<ReviewCard> due = await db.Cards.AsNoTracking()
            .Where(c => c.UserId == userId && c.Due <= now && !(c.State == CardState.New && c.Mode == CardMode.Recognition))
            .ToListAsync(cancellationToken);

        // Сначала проблемные слова, затем то, что вот-вот забудется (минимальная вероятность вспомнить);
        // карточки вспоминания, которые только что открылись, — после просроченных.
        List<QueuedCard> reviews = due
            .OrderByDescending(c => IsProblem(c, problemWords))
            .ThenBy(c => c.State == CardState.New ? 1 : 0)
            .ThenBy(c => FsrsScheduler.Retrievability(c.Memory, now))
            .ThenBy(c => c.Due)
            .Take(Math.Max(0, reviewLimit - reviewsDone))
            .Select(c => ToQueued(c) with { IsProblem = IsProblem(c, problemWords) })
            .ToList();

        int newAvailable = await db.Cards.CountAsync(c => c.UserId == userId && c.State == CardState.New && c.Mode == CardMode.Recognition, cancellationToken);
        List<QueuedCard> newCards = (await db.Cards.AsNoTracking()
                .Where(c => c.UserId == userId && c.State == CardState.New && c.Mode == CardMode.Recognition)
                .OrderBy(c => c.CreatedAt)
                .ThenBy(c => c.Id)
                .Take(Math.Max(0, newLimit - newDone))
                .ToListAsync(cancellationToken))
            .Select(ToQueued)
            .ToList();

        return new DailyQueue(reviews, newCards, reviewsDone, newDone, due.Count, newAvailable);
    }

    public async Task<IReadOnlyList<QueuedCard>> BuildMistakesQueueAsync(
        Guid userId, IReadOnlyCollection<Guid> problemWordIds, int limit, CancellationToken cancellationToken)
    {
        List<Guid> ids = problemWordIds.Distinct().ToList();
        int lapses = problems.ProblemLapses;
        List<ReviewCard> cards = await db.Cards.AsNoTracking()
            .Where(c => c.UserId == userId && (ids.Contains(c.UserWordId) || c.Lapses >= lapses))
            .ToListAsync(cancellationToken);

        var order = ids.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
        return cards
            .Where(c => !(c.State == CardState.New && c.Mode == CardMode.Recognition))
            .GroupBy(c => c.UserWordId)
            .Select(g => g.OrderByDescending(c => c.Mode == CardMode.Recall).First())
            .OrderBy(c => order.GetValueOrDefault(c.UserWordId, int.MaxValue))
            .ThenByDescending(c => c.Lapses)
            .Take(limit)
            .Select(c => ToQueued(c) with { IsProblem = true })
            .ToList();
    }

    public async Task<QueuedCard?> GetCardAsync(Guid userId, Guid cardId, CancellationToken cancellationToken)
    {
        ReviewCard? card = await db.Cards.AsNoTracking().SingleOrDefaultAsync(c => c.Id == cardId && c.UserId == userId, cancellationToken);
        return card is null ? null : ToQueued(card);
    }

    public async Task<ReviewOutcome> ReviewAsync(
        Guid userId, Guid cardId, ReviewRating rating, DateTimeOffset at, int elapsedMs, CancellationToken cancellationToken)
    {
        ReviewCard card = await db.Cards.SingleOrDefaultAsync(c => c.Id == cardId && c.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException($"Карточка {cardId} не найдена");

        CardState before = card.State;
        db.Logs.Add(card.Review(fsrs, (Rating)rating, at, elapsedMs));
        bool graduated = before != CardState.Review && card.State == CardState.Review;

        // Слово стало узнаваться уверенно — пора учиться вспоминать его самому.
        bool recallUnlocked = false;
        if (card.Mode == CardMode.Recognition && graduated
            && !await db.Cards.AnyAsync(c => c.UserWordId == card.UserWordId && c.Mode == CardMode.Recall, cancellationToken))
        {
            db.Cards.Add(ReviewCard.Create(userId, card.UserWordId, CardMode.Recall, at));
            recallUnlocked = true;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ReviewOutcome(card.Id, card.Due, card.State.ToString(), card.Stability, graduated, recallUnlocked);
    }

    /// <summary>Карточки узнавания для слов без карточек; карточки удалённых слов — прочь (журнал остаётся).</summary>
    private async Task SyncCardsAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> wordIds = await words.ListIdsAsync(userId, cancellationToken);
        List<(Guid UserWordId, Guid Id)> cards = (await db.Cards
                .Where(c => c.UserId == userId)
                .Select(c => new { c.UserWordId, c.Id })
                .ToListAsync(cancellationToken))
            .Select(c => (c.UserWordId, c.Id))
            .ToList();

        var known = cards.Select(c => c.UserWordId).ToHashSet();
        var alive = wordIds.ToHashSet();

        // Порядок создания = порядок добавления слов: новые слова выдаются в том же порядке.
        TimeSpan tick = TimeSpan.FromTicks(1);
        DateTimeOffset createdAt = now - tick * wordIds.Count;
        bool changed = false;
        foreach (Guid wordId in wordIds)
        {
            createdAt += tick;
            if (known.Add(wordId))
            {
                db.Cards.Add(ReviewCard.Create(userId, wordId, CardMode.Recognition, createdAt));
                changed = true;
            }
        }

        List<Guid> orphans = cards.Where(c => !alive.Contains(c.UserWordId)).Select(c => c.Id).ToList();
        if (orphans.Count > 0)
        {
            db.Cards.RemoveRange(await db.Cards.Where(c => orphans.Contains(c.Id)).ToListAsync(cancellationToken));
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private bool IsProblem(ReviewCard card, HashSet<Guid> problemWords) =>
        card.Lapses >= problems.ProblemLapses || problemWords.Contains(card.UserWordId);

    /// <summary>«Новое» — только слово, которое ещё не видели. Карточка вспоминания знакомого слова новой не считается.</summary>
    private static QueuedCard ToQueued(ReviewCard card) =>
        new(card.Id, card.UserWordId, (ReviewMode)card.Mode, card.State == CardState.New && card.Mode == CardMode.Recognition);
}

/// <summary>Новое слово сразу получает карточку узнавания, чтобы попасть в ближайшее занятие.</summary>
public sealed class CreateCardOnUserWordAdded(ISpacedRepetitionDb db, TimeProvider time) : IIntegrationEventHandler<UserWordAdded>
{
    public async Task HandleAsync(UserWordAdded e, CancellationToken cancellationToken)
    {
        if (await db.Cards.AnyAsync(c => c.UserWordId == e.UserWordId, cancellationToken))
        {
            return;
        }

        db.Cards.Add(ReviewCard.Create(e.UserId, e.UserWordId, CardMode.Recognition, time.GetUtcNow()));
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Слово удалено из словаря — его карточки уходят из расписания. Журнал ответов остаётся.</summary>
public sealed class RemoveCardsOnUserWordDeleted(ISpacedRepetitionDb db) : IIntegrationEventHandler<UserWordDeleted>
{
    public async Task HandleAsync(UserWordDeleted e, CancellationToken cancellationToken)
    {
        db.Cards.RemoveRange(await db.Cards.Where(c => c.UserWordId == e.UserWordId).ToListAsync(cancellationToken));
        await db.SaveChangesAsync(cancellationToken);
    }
}
