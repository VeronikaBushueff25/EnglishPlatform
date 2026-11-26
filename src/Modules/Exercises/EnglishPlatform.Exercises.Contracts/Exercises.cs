namespace EnglishPlatform.Exercises.Contracts;

public enum ExerciseMode
{
    Recognition,
    Recall,
}

/// <summary>Формат задания.</summary>
public enum ExerciseKind
{
    /// <summary>Знакомство с новым словом: без проверки.</summary>
    Introduce,

    /// <summary>Английское слово → выбрать перевод из вариантов.</summary>
    ChooseTranslation,

    /// <summary>Слово звучит → выбрать перевод.</summary>
    ListenChoose,

    /// <summary>Перевод → написать английское слово.</summary>
    TypeWord,

    /// <summary>Предложение из примера с пропуском → вписать слово.</summary>
    FillGap,

    /// <summary>Перевод → выбрать английское слово из вариантов. Лёгкая «разминка» перед вводом для проблемных слов.</summary>
    ChooseWord,
}

public enum AnswerVerdict
{
    Correct,

    /// <summary>Засчитано, но с ошибкой: опечатка, артикль, форма слова.</summary>
    Almost,

    Wrong,
}

/// <summary>Тип ошибки. На этапе 5 по этим тегам считаются слабые места.</summary>
public enum ErrorTag
{
    None,
    Typo,
    Article,
    WordForm,
    WrongWord,
    Empty,

    /// <summary>Не тот предлог во фразе: depend of вместо depend on.</summary>
    Preposition,
}

/// <summary>
/// Как часто давать тот или иной формат. Собирается из слабых мест пользователя: частые ошибки в формах слова —
/// больше пропусков в предложениях, частые опечатки — больше чистого ввода, плохо на слух — больше заданий на слух.
/// </summary>
public sealed record ExercisePreferences(double ListenShare, double GapShare)
{
    public static readonly ExercisePreferences Default = new(0.3, 0.6);
}

/// <summary>Слово глазами задания.</summary>
public sealed record ExerciseWord(
    Guid UserWordId,
    string Text,
    string Type,
    string? PartOfSpeech,
    string? Level,
    string? Transcription,
    IReadOnlyList<string> Translations,
    string? Example,
    string? ExampleTranslation,
    string? AudioUrl);

/// <summary>
/// Задание целиком, вместе с правильными ответами. Хранится на сервере (снимок попадает в журнал попыток),
/// клиенту уходит без ответов.
/// </summary>
public sealed record ExerciseTask(
    Guid TaskId,
    ExerciseKind Kind,
    ExerciseWord Word,
    string Prompt,
    string? PromptNote,
    IReadOnlyList<string> Options,
    int? CorrectOption,
    IReadOnlyList<string> AcceptedAnswers,
    string? Sentence,
    string? SentenceTranslation,
    string? Hint);

public sealed record CheckResult(AnswerVerdict Verdict, ErrorTag ErrorTag, string CorrectAnswer, string Explanation);

/// <summary>Попытка ответа. <c>AttemptId</c> присылает клиент: повторная отправка того же ответа не учитывается дважды.</summary>
public sealed record AttemptRecord(
    Guid AttemptId,
    Guid UserId,
    Guid SessionId,
    Guid CardId,
    ExerciseTask Task,
    string Answer,
    bool UsedHint,
    int ElapsedMs,
    CheckResult Result,
    int Rating,
    DateTimeOffset AnsweredAt);

public interface IExerciseEngine
{
    /// <summary>Задание для слова. Для узнавания — выбор (иногда на слух), для вспоминания — ввод или пропуск в предложении.</summary>
    ExerciseTask Build(ExerciseWord word, ExerciseMode mode, IReadOnlyList<string> distractors, ExercisePreferences preferences);

    /// <summary>Разминка для проблемного слова: выбрать английское слово по переводу из вариантов.</summary>
    ExerciseTask BuildChooseWord(ExerciseWord word, IReadOnlyList<string> englishDistractors);

    ExerciseTask BuildIntroduction(ExerciseWord word);

    CheckResult Check(ExerciseTask task, string? answer);

    Task<AttemptRecord?> FindAttemptAsync(Guid userId, Guid attemptId, CancellationToken cancellationToken);

    Task RecordAsync(AttemptRecord attempt, CancellationToken cancellationToken);
}
