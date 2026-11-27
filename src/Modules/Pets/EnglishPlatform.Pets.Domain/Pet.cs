using EnglishPlatform.BuildingBlocks.Domain;

namespace EnglishPlatform.Pets.Domain;

public enum PetSpecies
{
    Cat,
    Corgi,
    Hamster,
}

/// <summary>Настроение — из сытости, ласки и того, давно ли были занятия.</summary>
public enum PetMood
{
    Happy,
    Content,
    Hungry,
    Lonely,
    Sad,
    Away,
}

public enum PetStage
{
    Baby,
    Teen,
    Adult,
}

/// <summary>Что получилось сделать с питомцем (для ответа пользователю).</summary>
public enum PetActionResult
{
    Done,
    NoTreats,
    Full,
    Away,
    Tired,
}

/// <summary>Учебные дела, о которых питомец просит и за которые благодарит.</summary>
public enum PetActivity
{
    Lesson,
    MistakesLesson,
    Match,
    Blitz,
    Scramble,
    Reading,
    AddWords,
    Quiz,
}

/// <summary>Просьба питомца на сегодня: сделать учебное дело несколько раз и получить угощения.</summary>
public sealed class PetQuest
{
    private PetQuest()
    {
    }

    public PetQuest(PetActivity kind, int target, int treats, int xp)
    {
        Kind = kind;
        Target = target;
        Treats = treats;
        Xp = xp;
    }

    public PetActivity Kind { get; private set; }

    public int Target { get; private set; }

    public int Progress { get; private set; }

    public int Treats { get; private set; }

    public int Xp { get; private set; }

    public bool Done { get; private set; }

    /// <summary>Засчитывает сделанное; true — просьба только что выполнена.</summary>
    internal bool Advance(int amount)
    {
        if (Done)
        {
            return false;
        }

        Progress = Math.Min(Target, Progress + amount);
        Done = Progress >= Target;
        return Done;
    }
}

/// <summary>Счётчик, по которому открывается вещь в комнате.</summary>
public enum DecorCounter
{
    Lessons,
    WordsAdded,
    BestStreak,
    QuestsCompleted,
    TextsRead,
    QuizCorrect,
    GamesPlayed,
}

/// <summary>Вещь в комнате питомца — достижение за учёбу.</summary>
public sealed record PetDecor(string Key, DecorCounter Counter, int Target);

/// <summary>
/// Питомец пользователя (один на пользователя, ключ — id пользователя). Питается угощениями за учёбу:
/// занятия и просьбы питомца (сыграть в игру, почитать, добавить слова, ответить ему на вопросы).
/// Растёт от опыта, грустит без занятий и «уходит гулять», но никогда не умирает: возвращается с первым занятием.
/// Вещи в комнате — достижения: открываются за занятия, слова, серию дней, тексты.
/// Сытость и ласка убывают со временем — хранится значение на момент <see cref="UpdatedAt"/>, текущее считается.
/// </summary>
public sealed class Pet : Entity<Guid>
{
    public const int MaxNameLength = 24;
    public const int MaxPaletteLength = 16;
    public const int MaxTreats = 30;

    /// <summary>Полная миска пустеет за полтора дня, ласки хватает на три.</summary>
    public const double SatietyLossPerHour = 100.0 / 36;
    public const double AffectionLossPerHour = 100.0 / 72;

    public const int FeedSatiety = 30;
    public const int PatAffection = 4;
    public const int MaxPatsPerDay = 8;
    public const int QuizAffection = 4;
    public const int MaxQuizPerDay = 15;
    public const int LessonAffection = 15;

    public const int SadAfterDays = 2;
    public const int AwayAfterDays = 5;

    /// <summary>Аксессуары и уровень, с которого они открываются.</summary>
    public static readonly IReadOnlyDictionary<string, int> Accessories = new Dictionary<string, int>
    {
        ["bow"] = 2,
        ["scarf"] = 3,
        ["glasses"] = 4,
        ["hat"] = 5,
        ["crown"] = 7,
    };

    /// <summary>Обстановка комнаты в порядке открытия.</summary>
    public static readonly IReadOnlyList<PetDecor> Decor =
    [
        new("rug", DecorCounter.Lessons, 0),
        new("plant", DecorCounter.Lessons, 1),
        new("lamp", DecorCounter.Lessons, 5),
        new("books", DecorCounter.WordsAdded, 20),
        new("globe", DecorCounter.BestStreak, 3),
        new("armchair", DecorCounter.TextsRead, 3),
        new("poster", DecorCounter.QuestsCompleted, 10),
        new("fishbowl", DecorCounter.QuizCorrect, 30),
        new("bed", DecorCounter.BestStreak, 7),
        new("lights", DecorCounter.GamesPlayed, 15),
        new("trophy", DecorCounter.Lessons, 30),
    ];

    /// <summary>Окрасы каждого вида (рисунки — на клиенте).</summary>
    public static readonly IReadOnlyDictionary<PetSpecies, string[]> Palettes = new Dictionary<PetSpecies, string[]>
    {
        [PetSpecies.Cat] = ["ginger", "gray", "black", "white", "tabby"],
        [PetSpecies.Corgi] = ["red", "fawn", "tricolor"],
        [PetSpecies.Hamster] = ["golden", "white", "gray", "chocolate"],
    };

    private static readonly PetActivity[] Games = [PetActivity.Match, PetActivity.Blitz, PetActivity.Scramble];

    private readonly List<PetQuest> _quests = [];

    private Pet()
    {
        Name = string.Empty;
        Palette = string.Empty;
    }

    private Pet(Guid userId, PetSpecies species, string palette, string name, DateTimeOffset now)
        : base(userId)
    {
        Species = species;
        Palette = palette;
        Name = name;
        Satiety = 70;
        Affection = 60;
        Treats = 3; // подарок к знакомству: сразу можно покормить
        UpdatedAt = now;
        LastStudiedAt = now;
        CreatedAt = now;
    }

    public PetSpecies Species { get; private set; }

    public string Palette { get; private set; }

    public string Name { get; private set; }

    public string? Accessory { get; private set; }

    public double Satiety { get; private set; }

    public double Affection { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public int Treats { get; private set; }

    public int Xp { get; private set; }

    public DateTimeOffset LastStudiedAt { get; private set; }

    public int LessonsCompleted { get; private set; }

    public int TreatsEaten { get; private set; }

    public int WordsAdded { get; private set; }

    public int QuestsCompleted { get; private set; }

    public int TextsRead { get; private set; }

    public int QuizCorrect { get; private set; }

    public int GamesPlayed { get; private set; }

    /// <summary>Дней подряд с занятиями (по дням пользователя) и рекорд.</summary>
    public int StudyStreak { get; private set; }

    public int BestStreak { get; private set; }

    public DateOnly? LastStudyDay { get; private set; }

    /// <summary>День (пользователя), к которому относятся просьбы и счётчики ласки и вопросов.</summary>
    public DateOnly ActionDay { get; private set; }

    public int PatsToday { get; private set; }

    public int QuizToday { get; private set; }

    public DateOnly QuestDay { get; private set; }

    public IReadOnlyList<PetQuest> Quests => _quests;

    public DateTimeOffset CreatedAt { get; private set; }

    public static bool IsKnownPalette(PetSpecies species, string palette) => Palettes[species].Contains(palette);

    public static Pet Adopt(Guid userId, PetSpecies species, string palette, string name, DateTimeOffset now) =>
        new(userId, species, palette, name.Trim(), now);

    public double SatietyAt(DateTimeOffset now) => Decay(Satiety, SatietyLossPerHour, now);

    public double AffectionAt(DateTimeOffset now) => Decay(Affection, AffectionLossPerHour, now);

    public double DaysSinceStudy(DateTimeOffset now) => Math.Max(0, (now - LastStudiedAt).TotalDays);

    public PetMood MoodAt(DateTimeOffset now)
    {
        double days = DaysSinceStudy(now);
        if (days >= AwayAfterDays)
        {
            return PetMood.Away;
        }

        if (days >= SadAfterDays)
        {
            return PetMood.Sad;
        }

        double satiety = SatietyAt(now);
        double affection = AffectionAt(now);
        if (satiety < 25)
        {
            return PetMood.Hungry;
        }

        if (affection < 25)
        {
            return PetMood.Lonely;
        }

        return satiety >= 60 && affection >= 60 ? PetMood.Happy : PetMood.Content;
    }

    /// <summary>Серия на сегодня: если вчера занятий не было, она уже прервалась.</summary>
    public int StreakOn(DateOnly today) =>
        LastStudyDay is { } last && last.AddDays(1) >= today ? StudyStreak : 0;

    /// <summary>Уровень n начинается с 25·n·(n−1) опыта: 0, 50, 150, 300, 500, 750…</summary>
    public static int XpForLevel(int level) => 25 * level * (level - 1);

    public int Level
    {
        get
        {
            int level = 1;
            while (Xp >= XpForLevel(level + 1))
            {
                level++;
            }

            return level;
        }
    }

    public PetStage Stage => Level switch
    {
        < 3 => PetStage.Baby,
        < 6 => PetStage.Teen,
        _ => PetStage.Adult,
    };

    public IReadOnlyList<string> UnlockedAccessories => Accessories.Where(a => Level >= a.Value).Select(a => a.Key).ToList();

    public int CounterValue(DecorCounter counter) => counter switch
    {
        DecorCounter.Lessons => LessonsCompleted,
        DecorCounter.WordsAdded => WordsAdded,
        DecorCounter.BestStreak => BestStreak,
        DecorCounter.QuestsCompleted => QuestsCompleted,
        DecorCounter.TextsRead => TextsRead,
        DecorCounter.QuizCorrect => QuizCorrect,
        DecorCounter.GamesPlayed => GamesPlayed,
        _ => 0,
    };

    public int PatsLeft(DateOnly today) => ActionDay == today ? Math.Max(0, MaxPatsPerDay - PatsToday) : MaxPatsPerDay;

    /// <summary>
    /// Просьбы на день: занятие; одна из игр (по очереди); ещё одно дело — текст, новые слова, вопросы питомца или трудные слова.
    /// Набор зависит только от даты, поэтому одинаков на всех устройствах.
    /// </summary>
    public void EnsureQuests(DateOnly today)
    {
        if (QuestDay == today && _quests.Count > 0)
        {
            return;
        }

        QuestDay = today;
        _quests.Clear();
        int day = today.DayNumber;
        _quests.Add(new PetQuest(PetActivity.Lesson, 1, treats: 1, xp: 10));
        _quests.Add(new PetQuest(Games[day % Games.Length], 1, treats: 2, xp: 10));
        _quests.Add((day % 4) switch
        {
            0 => new PetQuest(PetActivity.Reading, 1, treats: 2, xp: 15),
            1 => new PetQuest(PetActivity.AddWords, 3, treats: 2, xp: 15),
            2 => new PetQuest(PetActivity.Quiz, 5, treats: 2, xp: 15),
            _ => new PetQuest(PetActivity.MistakesLesson, 1, treats: 2, xp: 15),
        });
    }

    /// <summary>
    /// Учебное дело сделано: счётчики достижений и просьбы на сегодня. Возвращает только что выполненные просьбы
    /// (их награда уже начислена).
    /// </summary>
    public IReadOnlyList<PetQuest> Record(PetActivity activity, int amount, DateOnly today, DateTimeOffset now)
    {
        Settle(now);
        amount = Math.Max(1, amount);
        switch (activity)
        {
            case PetActivity.AddWords:
                WordsAdded += amount;
                break;
            case PetActivity.Reading:
                TextsRead += amount;
                break;
            case PetActivity.Match or PetActivity.Blitz or PetActivity.Scramble:
                GamesPlayed += amount;
                break;
        }

        EnsureQuests(today);
        var completed = new List<PetQuest>();
        foreach (PetQuest quest in _quests.Where(q => q.Kind == activity || (activity == PetActivity.MistakesLesson && q.Kind == PetActivity.Lesson)))
        {
            if (quest.Advance(amount))
            {
                completed.Add(quest);
                Treats = Math.Min(MaxTreats, Treats + quest.Treats);
                Xp += quest.Xp;
                QuestsCompleted++;
            }
        }

        return completed;
    }

    /// <summary>
    /// Занятие закончено: угощения (2 + 1 за каждые 5 верных, до 6), ласка, опыт, серия дней; питомец возвращается с прогулки.
    /// </summary>
    public (int Treats, IReadOnlyList<PetQuest> Completed) OnLessonFinished(int answered, int correct, bool mistakes, DateOnly today, DateTimeOffset now)
    {
        Settle(now);
        int treats = Math.Min(6, 2 + (correct / 5));
        Treats = Math.Min(MaxTreats, Treats + treats);
        Affection = Clamp(Affection + LessonAffection);
        Xp += Math.Max(0, answered) + Math.Max(0, correct);
        LessonsCompleted++;
        LastStudiedAt = now;

        if (LastStudyDay != today)
        {
            StudyStreak = LastStudyDay is { } last && last.AddDays(1) == today ? StudyStreak + 1 : 1;
            BestStreak = Math.Max(BestStreak, StudyStreak);
            LastStudyDay = today;
        }

        return (treats, Record(mistakes ? PetActivity.MistakesLesson : PetActivity.Lesson, 1, today, now));
    }

    /// <summary>Ответ на вопрос питомца о слове: верный радует его (несколько раз в день) и идёт в просьбу «ответь на вопросы».</summary>
    public IReadOnlyList<PetQuest> AnswerQuiz(bool correct, DateOnly today, DateTimeOffset now)
    {
        Settle(now);
        if (!correct)
        {
            return [];
        }

        StartDay(today);
        QuizCorrect++;
        if (QuizToday < MaxQuizPerDay)
        {
            QuizToday++;
            Affection = Clamp(Affection + QuizAffection);
            Xp += 1;
        }

        return Record(PetActivity.Quiz, 1, today, now);
    }

    public PetActionResult Feed(DateTimeOffset now)
    {
        Settle(now);
        if (MoodAt(now) == PetMood.Away)
        {
            return PetActionResult.Away;
        }

        if (Treats <= 0)
        {
            return PetActionResult.NoTreats;
        }

        if (Satiety >= 95)
        {
            return PetActionResult.Full;
        }

        Treats--;
        TreatsEaten++;
        Satiety = Clamp(Satiety + FeedSatiety);
        Affection = Clamp(Affection + 3);
        Xp += 2;
        return PetActionResult.Done;
    }

    /// <summary>Погладить можно сколько угодно, но ласка растёт только первые несколько раз в день.</summary>
    public PetActionResult Pat(DateOnly today, DateTimeOffset now)
    {
        Settle(now);
        if (MoodAt(now) == PetMood.Away)
        {
            return PetActionResult.Away;
        }

        StartDay(today);
        if (PatsToday >= MaxPatsPerDay)
        {
            return PetActionResult.Tired;
        }

        PatsToday++;
        Affection = Clamp(Affection + PatAffection);
        return PetActionResult.Done;
    }

    public void Rename(string name) => Name = name.Trim();

    public bool Restyle(string palette)
    {
        if (!IsKnownPalette(Species, palette))
        {
            return false;
        }

        Palette = palette;
        return true;
    }

    /// <summary>Надеть аксессуар (null — снять). Только открытые по уровню.</summary>
    public bool Wear(string? accessory)
    {
        if (accessory is not null && !UnlockedAccessories.Contains(accessory))
        {
            return false;
        }

        Accessory = accessory;
        return true;
    }

    /// <summary>Фиксирует убывание сытости и ласки на момент now.</summary>
    private void Settle(DateTimeOffset now)
    {
        if (now <= UpdatedAt)
        {
            return;
        }

        Satiety = SatietyAt(now);
        Affection = AffectionAt(now);
        UpdatedAt = now;
    }

    private void StartDay(DateOnly today)
    {
        if (ActionDay != today)
        {
            ActionDay = today;
            PatsToday = 0;
            QuizToday = 0;
        }
    }

    private double Decay(double value, double lossPerHour, DateTimeOffset now) =>
        Clamp(value - (Math.Max(0, (now - UpdatedAt).TotalHours) * lossPerHour));

    private static double Clamp(double value) => Math.Clamp(value, 0, 100);
}
