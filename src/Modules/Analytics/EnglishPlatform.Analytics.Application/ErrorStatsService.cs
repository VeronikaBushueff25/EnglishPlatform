using EnglishPlatform.Analytics.Contracts;
using EnglishPlatform.Analytics.Domain;
using EnglishPlatform.BuildingBlocks.Events;
using EnglishPlatform.BuildingBlocks.Security;
using EnglishPlatform.Grammar.Contracts;
using EnglishPlatform.LearningSessions.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EnglishPlatform.Analytics.Application;

public interface IAnalyticsDb
{
    DbSet<ErrorStat> ErrorStats { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Атомарно прибавляет ответ к счётчикам (создаёт недостающие). Один запрос INSERT … ON CONFLICT DO UPDATE:
    /// одновременные ответы одного пользователя не теряют приращения и не падают на уникальном индексе.
    /// </summary>
    Task IncrementErrorStatsAsync(Guid userId, IReadOnlyList<ErrorStatIncrement> increments, DateTimeOffset at, CancellationToken cancellationToken);
}

/// <summary>Приращение счётчика: +1 ответ, +Errors ошибок, +Almost «почти».</summary>
public sealed record ErrorStatIncrement(StatDimension Dimension, string Key, int Errors, int Almost);

/// <summary>Пороги проблемного слова: хотя бы две неудачи, не меньше трети ответов неудачные, ошибка была недавно.</summary>
public sealed class WeaknessOptions
{
    public const string Section = "Analytics";

    public double ProblemMinFailures { get; set; } = 2;

    public double ProblemMinErrorRate { get; set; } = 0.34;

    public int ProblemRecentDays { get; set; } = 30;

    public int ProblemWordsLimit { get; set; } = 50;
}

/// <summary>Счётчики по каждому проверенному ответу: всего, по типу ошибки, по формату, по слову.</summary>
public sealed class CountAnswerChecked(IAnalyticsDb db) : IIntegrationEventHandler<AnswerChecked>
{
    public async Task HandleAsync(AnswerChecked e, CancellationToken cancellationToken)
    {
        bool wrong = e.Verdict == "Wrong";
        bool almost = e.Verdict == "Almost";

        var keys = new List<(StatDimension Dimension, string Key)>
        {
            (StatDimension.Total, "all"),
            (StatDimension.Format, e.ExerciseKind),
            (StatDimension.Word, e.UserWordId.ToString()),
        };
        if (e.ErrorTag != "None")
        {
            keys.Add((StatDimension.Tag, e.ErrorTag));
        }

        // Для тега считаем не ответы, а случаи этой ошибки: его доля — случаи / все ответы.
        List<ErrorStatIncrement> increments = keys
            .Select(k => k.Dimension == StatDimension.Tag
                ? new ErrorStatIncrement(k.Dimension, k.Key, 1, 0)
                : new ErrorStatIncrement(k.Dimension, k.Key, wrong ? 1 : 0, almost ? 1 : 0))
            .ToList();

        await db.IncrementErrorStatsAsync(e.UserId, increments, e.OccurredAt, cancellationToken);
    }
}

public sealed class WeaknessReader(IAnalyticsDb db, WeaknessOptions options, TimeProvider time) : IWeaknessReader
{
    public async Task<WeaknessProfile> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        List<ErrorStat> stats = await db.ErrorStats.AsNoTracking()
            .Where(s => s.UserId == userId && s.Dimension != StatDimension.Word)
            .ToListAsync(cancellationToken);

        int total = stats.FirstOrDefault(s => s.Dimension == StatDimension.Total)?.Attempts ?? 0;
        if (total == 0)
        {
            return WeaknessProfile.Empty;
        }

        Dictionary<string, double> tags = stats.Where(s => s.Dimension == StatDimension.Tag)
            .ToDictionary(s => s.Key, s => (double)s.Attempts / total);
        List<FormatStat> formats = stats.Where(s => s.Dimension == StatDimension.Format)
            .Select(s => new FormatStat(s.Key, s.Attempts, s.ErrorRate))
            .ToList();

        DateTimeOffset recent = time.GetUtcNow().AddDays(-options.ProblemRecentDays);
        List<ErrorStat> words = await db.ErrorStats.AsNoTracking()
            .Where(s => s.UserId == userId && s.Dimension == StatDimension.Word && s.LastErrorAt >= recent
                        && s.Errors + s.Almost * 0.5 >= options.ProblemMinFailures)
            .ToListAsync(cancellationToken);
        List<Guid> problemWords = words
            .Where(s => s.ErrorRate >= options.ProblemMinErrorRate)
            .OrderByDescending(s => s.ErrorRate)
            .ThenByDescending(s => s.LastErrorAt)
            .Take(options.ProblemWordsLimit)
            .Select(s => Guid.Parse(s.Key))
            .ToList();

        return new WeaknessProfile(total, tags, formats, problemWords);
    }
}

public sealed record WeakTagDto(string Code, string Title, int Count, double Share, string? TopicSlug, string? TopicTitle, string? Rule);

public sealed record WeakFormatDto(string Kind, int Attempts, double ErrorRate);

/// <summary>Слабые места для экрана занятия: какие ошибки чаще, в каких заданиях, сколько проблемных слов.</summary>
public sealed record WeakSpotsDto(int TotalAnswers, IReadOnlyList<WeakTagDto> Tags, IReadOnlyList<WeakFormatDto> Formats, int ProblemWords);

public sealed class WeakSpotsService(IWeaknessReader reader, IGrammarHints hints, ICurrentUser currentUser)
{
    public async Task<WeakSpotsDto> GetAsync(CancellationToken cancellationToken)
    {
        WeaknessProfile profile = await reader.GetAsync(currentUser.UserId, cancellationToken);
        Dictionary<string, GrammarHint> byTag = (await hints.ListAsync(cancellationToken)).ToDictionary(h => h.TagCode);

        List<WeakTagDto> tags = profile.TagShares
            .OrderByDescending(t => t.Value)
            .Select(t =>
            {
                GrammarHint? hint = byTag.GetValueOrDefault(t.Key);
                return new WeakTagDto(
                    t.Key, hint?.TagTitle ?? t.Key, (int)Math.Round(t.Value * profile.TotalAnswers), Math.Round(t.Value, 3), hint?.TopicSlug, hint?.TopicTitle, hint?.Rule);
            })
            .ToList();

        return new WeakSpotsDto(
            profile.TotalAnswers,
            tags,
            profile.Formats.OrderByDescending(f => f.ErrorRate).Select(f => new WeakFormatDto(f.Kind, f.Attempts, Math.Round(f.ErrorRate, 3))).ToList(),
            profile.ProblemWordIds.Count);
    }
}
