using EnglishPlatform.BuildingBlocks.Domain;

namespace EnglishPlatform.LearningProfiles.Domain;

/// <summary>Навык, который оценивает тест. Аудирование и говорение без ИИ не оценить — в MVP их нет.</summary>
public enum Skill
{
    Vocabulary,
    Grammar,
    Reading,
}

public enum Cefr
{
    A1,
    A2,
    B1,
    B2,
    C1,
    C2,
}

/// <summary>Вопрос теста уровня: выбор одного варианта. Для чтения есть текст (<c>Passage</c>).</summary>
public sealed class PlacementQuestion : Entity<Guid>
{
    public const int MaxPromptLength = 500;
    public const int MaxPassageLength = 2000;
    public const int MaxOptionLength = 200;
    public const int MinOptions = 2;
    public const int MaxOptions = 6;

    private PlacementQuestion()
    {
        Prompt = string.Empty;
        Options = [];
    }

    public Skill Skill { get; private set; }

    public Cefr Level { get; private set; }

    public string Prompt { get; private set; }

    public string? Passage { get; private set; }

    public List<string> Options { get; private set; }

    /// <summary>Номер правильного варианта в <see cref="Options"/>.</summary>
    public int CorrectOption { get; private set; }

    /// <summary>Неактивный вопрос не попадает в новые тесты (но остаётся в истории старых).</summary>
    public bool IsActive { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static PlacementQuestion Create(PlacementQuestionData data, DateTimeOffset now)
    {
        var question = new PlacementQuestion { Id = Guid.CreateVersion7() };
        question.Update(data, now);
        return question;
    }

    public void Update(PlacementQuestionData data, DateTimeOffset now)
    {
        Skill = data.Skill;
        Level = data.Level;
        Prompt = data.Prompt.Trim();
        Passage = string.IsNullOrWhiteSpace(data.Passage) ? null : data.Passage.Trim();
        Options = data.Options.Select(o => o.Trim()).ToList();
        CorrectOption = data.CorrectOption;
        IsActive = data.IsActive;
        UpdatedAt = now;
    }
}

public sealed record PlacementQuestionData(
    Skill Skill,
    Cefr Level,
    string Prompt,
    string? Passage,
    IReadOnlyList<string> Options,
    int CorrectOption,
    bool IsActive);

/// <summary>Ответ в тесте: вопрос, на каком уровне он был, верно ли. <c>Chosen</c> = −1 — «не знаю».</summary>
public sealed record PlacementAnswer(Guid QuestionId, Skill Skill, Cefr Level, int Chosen, bool Correct, DateTimeOffset AnsweredAt);

/// <summary>Текущий вопрос с порядком вариантов, в котором их увидел пользователь (варианты перемешиваются).</summary>
public sealed record PlacementCurrent(Guid QuestionId, IReadOnlyList<int> Order);
