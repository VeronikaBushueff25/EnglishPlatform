using System.Text.Json;
using EnglishPlatform.BuildingBlocks.Domain;

namespace EnglishPlatform.LearningProfiles.Domain;

/// <summary>Короткий тест — быстро прикинуть уровень; длинный — вдвое больше вопросов и заметно точнее.</summary>
public enum PlacementLength
{
    Short,
    Long,
}

/// <summary>Сколько вопросов задавать по каждому навыку (в порядке прохождения) и с какого уровня начинать.</summary>
public sealed record PlacementPlan(IReadOnlyList<(Skill Skill, int Questions)> Skills, Cefr StartLevel)
{
    public static readonly PlacementPlan Short = new([(Skill.Vocabulary, 7), (Skill.Grammar, 7), (Skill.Reading, 4)], Cefr.A2);

    public static readonly PlacementPlan Long = new([(Skill.Vocabulary, 14), (Skill.Grammar, 14), (Skill.Reading, 8)], Cefr.A2);

    public static readonly PlacementPlan Default = Short;

    public static PlacementPlan For(PlacementLength length) => length == PlacementLength.Long ? Long : Short;

    public int TotalQuestions => Skills.Sum(s => s.Questions);
}

/// <summary>
/// Адаптивный тест по модели ответа (как в компьютерных тестах на основе IRT, упрощённо).
/// Уровень навыка — число θ на шкале A1 = 0 … C2 = 5: на вопросах своего уровня L человек с θ = L отвечает верно
/// в половине случаев (без учёта угадывания), выше — реже, ниже — чаще. Каждый ответ сдвигает оценку θ:
/// верный — вверх, но с поправкой на угадывание (1 из числа вариантов), неверный и «не знаю» — вниз.
/// Следующий вопрос — на уровне текущей оценки, где он даёт больше всего информации.
/// Так одно удачное угадывание на C1 больше не делает уровень C1: его перевешивают остальные ответы.
/// </summary>
public static class PlacementEngine
{
    /// <summary>Крутизна: насколько резко падает доля верных ответов на уровень выше своего.</summary>
    public const double Discrimination = 2.5;

    /// <summary>Априорное распределение: большинство пришедших — около A2–B1, но разброс широкий.</summary>
    public const double PriorMean = 2.0;

    public const double PriorSd = 2.5;

    private const int DefaultOptions = 4;

    /// <summary>Вероятность верного ответа на вопрос уровня <paramref name="level"/> при способности θ.</summary>
    public static double PCorrect(double theta, Cefr level, int options)
    {
        double guess = 1.0 / Math.Max(2, options);
        double know = 1 / (1 + Math.Exp(-Discrimination * (theta - (int)level)));
        return guess + ((1 - guess) * know);
    }

    /// <summary>Оценка θ — среднее апостериорного распределения (EAP) по сетке; устойчивее точечного максимума при малом числе ответов.</summary>
    public static double Ability(IEnumerable<PlacementAnswer> answers)
    {
        List<PlacementAnswer> list = answers.ToList();
        double sum = 0, weight = 0;
        for (double theta = -1; theta <= 6.5; theta += 0.02)
        {
            double log = -((theta - PriorMean) * (theta - PriorMean)) / (2 * PriorSd * PriorSd);
            foreach (PlacementAnswer a in list)
            {
                int options = a.Options > 1 ? a.Options : DefaultOptions;
                double p = a.Correct
                    ? PCorrect(theta, a.Level, options)
                    // «Не знаю» — честный ноль без шанса угадать; неверный вариант — угадывание не сработало.
                    : a.Chosen < 0
                        ? 1 - (1 / (1 + Math.Exp(-Discrimination * (theta - (int)a.Level))))
                        : 1 - PCorrect(theta, a.Level, options);
                log += Math.Log(Math.Max(p, 1e-12));
            }

            double w = Math.Exp(log);
            sum += w * theta;
            weight += w;
        }

        return weight > 0 ? sum / weight : PriorMean;
    }

    /// <summary>Уровень по θ: целая часть (θ = 2,7 — B1), в пределах A1…C2.</summary>
    public static Cefr ToLevel(double theta) => (Cefr)Math.Clamp((int)Math.Floor(theta), (int)Cefr.A1, (int)Cefr.C2);

    public static Cefr Estimate(IEnumerable<PlacementAnswer> answers) => ToLevel(Ability(answers));

    /// <summary>Следующий вопрос: навык по плану, уровень — ближайший к текущей оценке θ.</summary>
    public static (Skill Skill, Cefr Level)? NextStep(IReadOnlyList<PlacementAnswer> answers, PlacementPlan plan)
    {
        foreach ((Skill skill, int questions) in plan.Skills)
        {
            List<PlacementAnswer> own = answers.Where(a => a.Skill == skill).ToList();
            if (own.Count >= questions)
            {
                continue;
            }

            // Первый вопрос навыка — от того, что показали предыдущие навыки (навыки связаны), иначе с начального уровня.
            double theta = own.Count > 0 ? Ability(own) : answers.Count > 0 ? Ability(answers) : (int)plan.StartLevel;
            return (skill, Target(theta));
        }

        return null;
    }

    /// <summary>Общий уровень — среднее θ по навыкам, взвешенное числом ответов, с округлением вниз.</summary>
    public static Cefr Overall(IReadOnlyDictionary<Skill, double> abilities, IReadOnlyDictionary<Skill, int> weights)
    {
        double total = abilities.Sum(a => a.Value * weights.GetValueOrDefault(a.Key, 1));
        int count = abilities.Sum(a => weights.GetValueOrDefault(a.Key, 1));
        return count == 0 ? Cefr.A1 : ToLevel(total / count);
    }

    /// <summary>Общий уровень по уже сохранённым уровням навыков — среднее с округлением вниз.</summary>
    public static Cefr Overall(IEnumerable<Cefr> levels)
    {
        List<int> values = levels.Select(l => (int)l).ToList();
        return values.Count == 0 ? Cefr.A1 : (Cefr)(int)Math.Floor(values.Average());
    }

    /// <summary>Вопрос, который больше всего уточняет оценку θ: уровень, где человек отвечает верно примерно в половине случаев.</summary>
    private static Cefr Target(double theta) => (Cefr)Math.Clamp((int)Math.Round(theta - 0.1, MidpointRounding.AwayFromZero), (int)Cefr.A1, (int)Cefr.C2);
}

/// <summary>Попытка теста уровня. Ответы и текущий вопрос хранятся в jsonb строки попытки.</summary>
public sealed class PlacementAttempt : Entity<Guid>
{
    private List<PlacementAnswer>? _answers;

    private PlacementAttempt()
    {
        AnswersJson = "[]";
    }

    public Guid UserId { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public int TotalQuestions { get; private set; }

    public PlacementLength Length { get; private set; }

    public string? CurrentJson { get; private set; }

    /// <summary>Итог по навыкам (Skill → уровень), заполняется при завершении.</summary>
    public string? ResultJson { get; private set; }

    private string AnswersJson { get; set; }

    public IReadOnlyList<PlacementAnswer> Answers => _answers ??= JsonSerializer.Deserialize<List<PlacementAnswer>>(AnswersJson)!;

    public PlacementCurrent? Current => CurrentJson is null ? null : JsonSerializer.Deserialize<PlacementCurrent>(CurrentJson);

    public bool IsFinished => FinishedAt is not null;

    public static PlacementAttempt Start(Guid userId, int totalQuestions, DateTimeOffset now) =>
        Start(userId, PlacementLength.Short, totalQuestions, now);

    public static PlacementAttempt Start(Guid userId, PlacementLength length, int totalQuestions, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = userId,
        StartedAt = now,
        Length = length,
        TotalQuestions = totalQuestions,
    };

    public void Ask(PlacementCurrent current) => CurrentJson = JsonSerializer.Serialize(current);

    public void Answer(PlacementAnswer answer)
    {
        var answers = Answers.ToList();
        answers.Add(answer);
        _answers = answers;
        AnswersJson = JsonSerializer.Serialize(answers);
        CurrentJson = null;
    }

    /// <summary>Вопросов в тесте меньше, чем задумано (в банке не хватило) — подстраиваем счётчик прогресса.</summary>
    public void ShortenTo(int total) => TotalQuestions = Math.Max(Answers.Count, total);

    /// <summary>Итог: уровень и оценка θ по каждому навыку, по которому были ответы.</summary>
    public IReadOnlyDictionary<Skill, double> Finish(IEnumerable<Skill> skills, DateTimeOffset now)
    {
        Dictionary<Skill, double> result = skills
            .Where(skill => Answers.Any(a => a.Skill == skill))
            .ToDictionary(skill => skill, skill => Math.Round(PlacementEngine.Ability(Answers.Where(a => a.Skill == skill)), 2));
        ResultJson = JsonSerializer.Serialize(result.ToDictionary(
            r => r.Key.ToString(),
            r => new { Level = PlacementEngine.ToLevel(r.Value).ToString(), Theta = r.Value }));
        FinishedAt = now;
        CurrentJson = null;
        return result;
    }
}

/// <summary>Уровень пользователя по навыку (skill_levels из плана): из теста или выставлен вручную.</summary>
public sealed class SkillLevel
{
    private SkillLevel()
    {
        Source = string.Empty;
    }

    public Guid UserId { get; private set; }

    public Skill Skill { get; private set; }

    public Cefr Level { get; private set; }

    public DateTimeOffset AssessedAt { get; private set; }

    /// <summary>Откуда уровень: Placement (тест) или Manual.</summary>
    public string Source { get; private set; }

    public static SkillLevel Create(Guid userId, Skill skill) => new() { UserId = userId, Skill = skill };

    public void Set(Cefr level, string source, DateTimeOffset now)
    {
        Level = level;
        Source = source;
        AssessedAt = now;
    }
}
