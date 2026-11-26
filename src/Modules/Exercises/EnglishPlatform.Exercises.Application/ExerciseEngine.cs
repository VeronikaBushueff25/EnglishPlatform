using System.Text.Json;
using System.Text.Json.Serialization;
using EnglishPlatform.Exercises.Contracts;
using EnglishPlatform.Exercises.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnglishPlatform.Exercises.Application;

public interface IExercisesDb
{
    DbSet<ExerciseAttempt> Attempts { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class ExerciseEngine(IExercisesDb db) : IExerciseEngine
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ExerciseFactory _factory = new(Random.Shared);

    public ExerciseTask Build(ExerciseWord word, ExerciseMode mode, IReadOnlyList<string> distractors, ExercisePreferences preferences) =>
        _factory.Build(word, mode, distractors, preferences);

    public ExerciseTask BuildChooseWord(ExerciseWord word, IReadOnlyList<string> englishDistractors) =>
        _factory.ChooseWord(word, englishDistractors);

    public ExerciseTask BuildIntroduction(ExerciseWord word) => _factory.Introduction(word);

    public CheckResult Check(ExerciseTask task, string? answer) => AnswerChecker.Check(task, answer);

    public async Task<AttemptRecord?> FindAttemptAsync(Guid userId, Guid attemptId, CancellationToken cancellationToken)
    {
        ExerciseAttempt? attempt = await db.Attempts.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, cancellationToken);
        if (attempt is null)
        {
            return null;
        }

        ExerciseTask task = JsonSerializer.Deserialize<ExerciseTask>(attempt.TaskSnapshot, Json)!;
        return new AttemptRecord(
            attempt.Id,
            attempt.UserId,
            attempt.SessionId,
            attempt.CardId,
            task,
            attempt.Answer,
            attempt.UsedHint,
            attempt.ElapsedMs,
            new CheckResult(Enum.Parse<AnswerVerdict>(attempt.Verdict), Enum.Parse<ErrorTag>(attempt.ErrorTag), attempt.CorrectAnswer, attempt.Explanation),
            attempt.Rating,
            attempt.AnsweredAt);
    }

    public async Task RecordAsync(AttemptRecord attempt, CancellationToken cancellationToken)
    {
        db.Attempts.Add(ExerciseAttempt.Create(
            attempt.AttemptId,
            attempt.UserId,
            attempt.SessionId,
            attempt.CardId,
            attempt.Task.Word.UserWordId,
            attempt.Task.Kind.ToString(),
            JsonSerializer.Serialize(attempt.Task, Json),
            attempt.Answer,
            attempt.UsedHint,
            attempt.ElapsedMs,
            attempt.Result.Verdict.ToString(),
            attempt.Result.ErrorTag.ToString(),
            attempt.Result.CorrectAnswer,
            attempt.Result.Explanation,
            attempt.Rating,
            attempt.AnsweredAt));
        await db.SaveChangesAsync(cancellationToken);
    }
}
