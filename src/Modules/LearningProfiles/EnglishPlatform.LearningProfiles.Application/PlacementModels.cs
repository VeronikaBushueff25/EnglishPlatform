using System.ComponentModel.DataAnnotations;
using EnglishPlatform.BuildingBlocks.Results;
using EnglishPlatform.LearningProfiles.Domain;

namespace EnglishPlatform.LearningProfiles.Application;

public sealed record PlacementProgress(int Answered, int Total);

/// <summary>Вопрос для клиента: варианты уже перемешаны, правильный ответ не передаётся.</summary>
public sealed record PlacementQuestionView(Guid QuestionId, Skill Skill, string Prompt, string? Passage, IReadOnlyList<string> Options);

public sealed record PlacementStepDto(Guid AttemptId, bool Finished, PlacementQuestionView? Question, PlacementProgress Progress);

/// <summary><c>Option</c> — номер варианта в том порядке, в каком он показан; −1 — «не знаю».</summary>
public sealed record PlacementAnswerRequest(
    [property: Required] Guid QuestionId,
    [property: Range(-1, PlacementQuestion.MaxOptions - 1)] int Option);

public sealed record SkillLevelDto(Skill Skill, Cefr Level, DateTimeOffset AssessedAt, string Source);

public sealed record PlacementResultDto(
    Guid AttemptId,
    Cefr Overall,
    IReadOnlyList<SkillLevelDto> Levels,
    int Correct,
    int Answered,
    int StarterWordsAdded);

public sealed record GrammarRecommendationDto(string Slug, string Title, string? Level, string Summary);

/// <summary>Персональная программа: уровни, что учить из грамматики, на какие слова опираться, дневная нагрузка.</summary>
public sealed record LearningPlanDto(
    bool HasPlacement,
    Cefr? Overall,
    IReadOnlyList<SkillLevelDto> Levels,
    IReadOnlyList<string> WordLevels,
    IReadOnlyList<GrammarRecommendationDto> Grammar,
    int DailyNewWords,
    int DailyReviews,
    IReadOnlyList<string> Goals,
    DateTimeOffset? LastPlacementAt);

public sealed record PlacementQuestionDto(
    Guid Id, Skill Skill, Cefr Level, string Prompt, string? Passage, IReadOnlyList<string> Options, int CorrectOption, bool IsActive);

public sealed record SavePlacementQuestionRequest(
    Skill Skill,
    Cefr Level,
    [property: Required(ErrorMessage = "Введите вопрос"), MaxLength(PlacementQuestion.MaxPromptLength)] string Prompt,
    [property: MaxLength(PlacementQuestion.MaxPassageLength)] string? Passage,
    [property: Required, MinLength(PlacementQuestion.MinOptions, ErrorMessage = "Нужно хотя бы 2 варианта"),
               MaxLength(PlacementQuestion.MaxOptions, ErrorMessage = "Не больше 6 вариантов")] IReadOnlyList<string> Options,
    int CorrectOption,
    bool IsActive = true) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Options.Any(string.IsNullOrWhiteSpace))
        {
            yield return new ValidationResult("Пустой вариант ответа", [nameof(Options)]);
        }

        if (Options.Any(o => o?.Length > PlacementQuestion.MaxOptionLength))
        {
            yield return new ValidationResult("Вариант не длиннее 200 символов", [nameof(Options)]);
        }

        if (Options.Select(o => o?.Trim().ToLowerInvariant()).Distinct().Count() != Options.Count)
        {
            yield return new ValidationResult("Варианты не должны повторяться", [nameof(Options)]);
        }

        if (CorrectOption < 0 || CorrectOption >= Options.Count)
        {
            yield return new ValidationResult("Отметьте правильный вариант", [nameof(CorrectOption)]);
        }

        if (Skill == Skill.Reading && string.IsNullOrWhiteSpace(Passage))
        {
            yield return new ValidationResult("Для чтения нужен текст", [nameof(Passage)]);
        }
    }

    public PlacementQuestionData ToData() => new(Skill, Level, Prompt, Passage, Options, CorrectOption, IsActive);
}

public static class PlacementErrors
{
    public static readonly Error AttemptNotFound = Error.NotFound("placement.not_found", "Тест не найден");

    public static readonly Error Finished = Error.Conflict("placement.finished", "Тест уже завершён");

    public static readonly Error StaleQuestion = Error.Conflict("placement.stale_question", "Этот вопрос уже неактуален, загрузите следующий");

    public static readonly Error QuestionNotFound = Error.NotFound("placement.question_not_found", "Вопрос не найден");

    public static readonly Error EmptyBank = Error.Conflict("placement.empty_bank", "В банке вопросов пока ничего нет");
}
