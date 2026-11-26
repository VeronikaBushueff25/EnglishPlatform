using System.ComponentModel.DataAnnotations;
using EnglishPlatform.BuildingBlocks.Results;
using EnglishPlatform.Exercises.Contracts;

namespace EnglishPlatform.LearningSessions.Application;

/// <summary>Что ждёт пользователя сегодня. <c>ToStudyNow</c> — сколько карточек войдёт в занятие прямо сейчас.</summary>
public sealed record TodayDto(
    int ReviewsDue,
    int NewAvailable,
    int NewLimit,
    int ReviewLimit,
    int ReviewsDoneToday,
    int NewDoneToday,
    int ToStudyNow,
    Guid? ActiveSessionId,
    int ProblemWords = 0,
    int AutoAddWords = 0);

/// <summary>Тип занятия: Daily (по умолчанию) или Mistakes — работа над ошибками.</summary>
public sealed record StartSessionRequest(
    [property: RegularExpression("^(Daily|Mistakes)$", ErrorMessage = "Тип занятия: Daily или Mistakes")] string? Kind);

/// <summary>Правило к ошибке: тип ошибки и тема грамматики с коротким правилом.</summary>
public sealed record RuleDto(string TagTitle, string? TopicSlug, string? TopicTitle, string? Text);

public sealed record SessionStats(int Answered, int Correct, int Almost, int Wrong, int NewWords, int Reviews, int DurationSeconds);

public sealed record SessionProgress(int Done, int Total);

public sealed record SessionDto(Guid Id, string Kind, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, SessionProgress Progress, SessionStats Stats);

/// <summary>Слово для показа: в задании-знакомстве и в разборе после ответа.</summary>
public sealed record WordView(
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

/// <summary>Задание для клиента: без правильных ответов (кроме знакомства, где показывается само слово).</summary>
public sealed record TaskView(
    Guid TaskId,
    ExerciseKind Kind,
    string Mode,
    string Prompt,
    string? PromptNote,
    IReadOnlyList<string> Options,
    string? Sentence,
    string? SentenceTranslation,
    string? Hint,
    string? AudioText,
    string? AudioUrl,
    WordView? Word,
    bool IsWarmUp = false,
    bool IsProblem = false);

public sealed record NextStepDto(bool Finished, TaskView? Task, SessionProgress Progress, SessionStats Stats);

public sealed record AnswerRequest(
    [property: Required] Guid AttemptId,
    [property: Required] Guid TaskId,
    [property: MaxLength(512)] string? Answer,
    bool UsedHint,
    [property: Range(0, 3_600_000)] int ElapsedMs);

/// <summary>Результат проверки: вердикт, разбор и когда слово вернётся. Одного запроса хватает на шаг занятия.</summary>
public sealed record AnswerResultDto(
    AnswerVerdict? Verdict,
    ErrorTag ErrorTag,
    string CorrectAnswer,
    string Explanation,
    int? Rating,
    DateTimeOffset? NextReviewAt,
    bool RecallUnlocked,
    WordView Word,
    SessionProgress Progress,
    RuleDto? Rule = null,
    bool IsWarmUp = false);

public static class SessionErrors
{
    public static readonly Error NotFound = Error.NotFound("sessions.not_found", "Занятие не найдено");

    public static readonly Error Finished = Error.Conflict("sessions.finished", "Занятие уже завершено");

    public static readonly Error StaleTask = Error.Conflict("sessions.stale_task", "Это задание уже неактуально, загрузите следующее");
}
