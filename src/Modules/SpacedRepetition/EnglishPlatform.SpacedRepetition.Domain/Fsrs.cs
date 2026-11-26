namespace EnglishPlatform.SpacedRepetition.Domain;

public enum CardState
{
    New,
    Learning,
    Review,
    Relearning,
}

/// <summary>Оценка ответа по шкале FSRS. Пользователь её не ставит — она выводится из ответа.</summary>
public enum Rating
{
    Again = 1,
    Hard = 2,
    Good = 3,
    Easy = 4,
}

/// <summary>Состояние памяти карточки в терминах FSRS.</summary>
public sealed record MemoryState(
    CardState State,
    int Step,
    double Stability,
    double Difficulty,
    DateTimeOffset Due,
    DateTimeOffset? LastReview);

/// <summary>Параметры планировщика. Веса — значения FSRS-5 по умолчанию из open-spaced-repetition.</summary>
public sealed record FsrsParameters
{
    public static readonly double[] DefaultWeights =
    [
        0.40255, 1.18385, 3.173, 15.69105, 7.1949, 0.5345, 1.4604, 0.0046, 1.54575, 0.1192,
        1.01925, 1.9395, 0.11, 0.29605, 2.2698, 0.2315, 2.9898, 0.51655, 0.6621,
    ];

    public double[] Weights { get; init; } = DefaultWeights;

    /// <summary>Желаемая вероятность вспомнить слово к моменту повторения.</summary>
    public double DesiredRetention { get; init; } = 0.9;

    public TimeSpan[] LearningSteps { get; init; } = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10)];

    public TimeSpan[] RelearningSteps { get; init; } = [TimeSpan.FromMinutes(10)];

    public int MaximumIntervalDays { get; init; } = 36500;
}

/// <summary>
/// Планировщик FSRS-5. Реализация по описанию алгоритма и референсному py-fsrs: начальные стабильность и сложность,
/// краткосрочная стабильность для повторов в тот же день, стабильность после успеха и после забывания,
/// шаги заучивания и переучивания. Без «размытия» интервалов (fuzz), чтобы расписание было предсказуемым.
/// </summary>
public sealed class FsrsScheduler(FsrsParameters parameters)
{
    public const double Decay = -0.5;
    public static readonly double Factor = Math.Pow(0.9, 1 / Decay) - 1; // 19/81

    private readonly double[] _w = parameters.Weights;

    public FsrsParameters Parameters { get; } = parameters;

    public static MemoryState NewCard(DateTimeOffset now) => new(CardState.New, 0, 0, 0, now, null);

    /// <summary>Вероятность вспомнить карточку в момент <paramref name="now"/>.</summary>
    public static double Retrievability(MemoryState card, DateTimeOffset now)
    {
        if (card.State == CardState.New || card.LastReview is null || card.Stability <= 0)
        {
            return 0;
        }

        double elapsedDays = Math.Max(0, (now - card.LastReview.Value).TotalDays);
        return Math.Pow(1 + Factor * elapsedDays / card.Stability, Decay);
    }

    public MemoryState Review(MemoryState card, Rating rating, DateTimeOffset now)
    {
        double retrievability = Retrievability(card, now);
        bool sameDay = card.LastReview is { } last && (now - last).TotalDays < 1;

        return card.State switch
        {
            CardState.New => Learn(
                CardState.Learning,
                step: 0,
                stability: InitialStability(rating),
                difficulty: InitialDifficulty(rating),
                rating,
                now,
                Parameters.LearningSteps),
            CardState.Learning or CardState.Relearning => Learn(
                card.State,
                card.Step,
                sameDay ? ShortTermStability(card.Stability, rating)
                    : rating == Rating.Again ? ForgetStability(card.Difficulty, card.Stability, retrievability)
                    : RecallStability(card.Difficulty, card.Stability, retrievability, rating),
                NextDifficulty(card.Difficulty, rating),
                rating,
                now,
                card.State == CardState.Learning ? Parameters.LearningSteps : Parameters.RelearningSteps),
            _ => ReviewState(card, rating, retrievability, sameDay, now),
        };
    }

    private MemoryState ReviewState(MemoryState card, Rating rating, double retrievability, bool sameDay, DateTimeOffset now)
    {
        double difficulty = NextDifficulty(card.Difficulty, rating);

        if (rating == Rating.Again)
        {
            double stability = sameDay ? ShortTermStability(card.Stability, rating) : ForgetStability(card.Difficulty, card.Stability, retrievability);
            if (Parameters.RelearningSteps.Length > 0)
            {
                return new MemoryState(CardState.Relearning, 0, stability, difficulty, now + Parameters.RelearningSteps[0], now);
            }

            return new MemoryState(CardState.Review, 0, stability, difficulty, now + IntervalDays(stability), now);
        }

        double next = sameDay ? ShortTermStability(card.Stability, rating) : RecallStability(card.Difficulty, card.Stability, retrievability, rating);
        return new MemoryState(CardState.Review, 0, next, difficulty, now + IntervalDays(next), now);
    }

    /// <summary>Шаги заучивания: Again — на первый шаг, Hard — повтор шага, Good — следующий шаг или выпуск, Easy — выпуск.</summary>
    private MemoryState Learn(CardState state, int step, double stability, double difficulty, Rating rating, DateTimeOffset now, TimeSpan[] steps)
    {
        if (steps.Length == 0 || rating == Rating.Easy || (rating == Rating.Good && step + 1 >= steps.Length))
        {
            return new MemoryState(CardState.Review, 0, stability, difficulty, now + IntervalDays(stability), now);
        }

        (int nextStep, TimeSpan delay) = rating switch
        {
            Rating.Again => (0, steps[0]),
            Rating.Hard when step == 0 => (0, steps.Length == 1 ? steps[0] * 1.5 : (steps[0] + steps[1]) / 2),
            Rating.Hard => (step, steps[step]),
            _ => (step + 1, steps[step + 1]),
        };

        return new MemoryState(state, nextStep, stability, difficulty, now + delay, now);
    }

    private TimeSpan IntervalDays(double stability)
    {
        double days = stability / Factor * (Math.Pow(Parameters.DesiredRetention, 1 / Decay) - 1);
        return TimeSpan.FromDays(Math.Clamp(Math.Round(days), 1, Parameters.MaximumIntervalDays));
    }

    internal double InitialStability(Rating rating) => Math.Max(_w[(int)rating - 1], 0.01);

    internal double InitialDifficulty(Rating rating) => Math.Clamp(RawInitialDifficulty(rating), 1, 10);

    private double RawInitialDifficulty(Rating rating) => _w[4] - Math.Exp(_w[5] * ((int)rating - 1)) + 1;

    internal double NextDifficulty(double difficulty, Rating rating)
    {
        double delta = -_w[6] * ((int)rating - 3);
        double damped = difficulty + delta * (10 - difficulty) / 9;
        double reverted = _w[7] * RawInitialDifficulty(Rating.Easy) + (1 - _w[7]) * damped;
        return Math.Clamp(reverted, 1, 10);
    }

    internal double ShortTermStability(double stability, Rating rating) =>
        stability * Math.Exp(_w[17] * ((int)rating - 3 + _w[18]));

    internal double RecallStability(double difficulty, double stability, double retrievability, Rating rating)
    {
        double hardPenalty = rating == Rating.Hard ? _w[15] : 1;
        double easyBonus = rating == Rating.Easy ? _w[16] : 1;
        return stability * (1 + Math.Exp(_w[8]) * (11 - difficulty) * Math.Pow(stability, -_w[9])
            * (Math.Exp((1 - retrievability) * _w[10]) - 1) * hardPenalty * easyBonus);
    }

    internal double ForgetStability(double difficulty, double stability, double retrievability)
    {
        double forgotten = _w[11] * Math.Pow(difficulty, -_w[12]) * (Math.Pow(stability + 1, _w[13]) - 1)
            * Math.Exp((1 - retrievability) * _w[14]);
        return Math.Min(forgotten, stability);
    }
}
