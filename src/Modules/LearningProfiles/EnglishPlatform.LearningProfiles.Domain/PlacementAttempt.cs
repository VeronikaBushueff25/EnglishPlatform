using System.Text.Json;
using EnglishPlatform.BuildingBlocks.Domain;

namespace EnglishPlatform.LearningProfiles.Domain;

/// <summary>Сколько вопросов задавать по каждому навыку (в порядке прохождения) и с какого уровня начинать.</summary>
public sealed record PlacementPlan(IReadOnlyList<(Skill Skill, int Questions)> Skills, Cefr StartLevel)
{
    public static readonly PlacementPlan Default = new([(Skill.Vocabulary, 7), (Skill.Grammar, 7), (Skill.Reading, 4)], Cefr.A2);

    public int TotalQuestions => Skills.Sum(s => s.Questions);
}

/// <summary>
/// Адаптивный тест «лесенкой»: верный ответ — следующий вопрос этого навыка на уровень выше, неверный — ниже.
/// Так вопросы быстро сходятся к уровню пользователя. Уровень навыка — самый высокий уровень, на котором
/// пользователь ответил верно хотя бы раз и не меньше чем в половине случаев.
/// </summary>
public static class PlacementEngine
{
    public static (Skill Skill, Cefr Level)? NextStep(IReadOnlyList<PlacementAnswer> answers, PlacementPlan plan)
    {
        foreach ((Skill skill, int questions) in plan.Skills)
        {
            List<PlacementAnswer> own = answers.Where(a => a.Skill == skill).ToList();
            if (own.Count >= questions)
            {
                continue;
            }

            if (own.Count == 0)
            {
                // Следующий навык начинаем с уровня, который уже показал предыдущий, — так меньше лишних вопросов.
                Cefr start = answers.Count == 0 ? plan.StartLevel : Shift(Estimate(answers.Where(a => a.Skill == answers[^1].Skill)), -1, plan.StartLevel);
                return (skill, start);
            }

            PlacementAnswer last = own[^1];
            return (skill, Shift(last.Level, last.Correct ? 1 : -1));
        }

        return null;
    }

    public static Cefr Estimate(IEnumerable<PlacementAnswer> answers)
    {
        Cefr result = Cefr.A1;
        foreach (IGrouping<Cefr, PlacementAnswer> level in answers.GroupBy(a => a.Level).OrderBy(g => g.Key))
        {
            int correct = level.Count(a => a.Correct);
            if (correct >= 1 && correct * 2 >= level.Count())
            {
                result = level.Key;
            }
        }

        return result;
    }

    /// <summary>Общий уровень — среднее по навыкам с округлением вниз.</summary>
    public static Cefr Overall(IEnumerable<Cefr> levels)
    {
        List<int> values = levels.Select(l => (int)l).ToList();
        return values.Count == 0 ? Cefr.A1 : (Cefr)(int)Math.Floor(values.Average());
    }

    private static Cefr Shift(Cefr level, int delta, Cefr? atLeast = null)
    {
        var shifted = (Cefr)Math.Clamp((int)level + delta, (int)Cefr.A1, (int)Cefr.C2);
        return atLeast is { } minimum && shifted < minimum ? minimum : shifted;
    }
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

    public string? CurrentJson { get; private set; }

    /// <summary>Итог по навыкам (Skill → уровень), заполняется при завершении.</summary>
    public string? ResultJson { get; private set; }

    private string AnswersJson { get; set; }

    public IReadOnlyList<PlacementAnswer> Answers => _answers ??= JsonSerializer.Deserialize<List<PlacementAnswer>>(AnswersJson)!;

    public PlacementCurrent? Current => CurrentJson is null ? null : JsonSerializer.Deserialize<PlacementCurrent>(CurrentJson);

    public bool IsFinished => FinishedAt is not null;

    public static PlacementAttempt Start(Guid userId, int totalQuestions, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = userId,
        StartedAt = now,
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

    public IReadOnlyDictionary<Skill, Cefr> Finish(IEnumerable<Skill> skills, DateTimeOffset now)
    {
        Dictionary<Skill, Cefr> result = skills
            .Where(skill => Answers.Any(a => a.Skill == skill))
            .ToDictionary(skill => skill, skill => PlacementEngine.Estimate(Answers.Where(a => a.Skill == skill)));
        ResultJson = JsonSerializer.Serialize(result.ToDictionary(r => r.Key.ToString(), r => r.Value.ToString()));
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
