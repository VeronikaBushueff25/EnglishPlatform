using EnglishPlatform.SpacedRepetition.Contracts;
using EnglishPlatform.SpacedRepetition.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnglishPlatform.SpacedRepetition.Application;

/// <summary>Статистика по карточкам и журналу ответов. Считается на лету: журнал индексирован по пользователю и времени.</summary>
public sealed class ReviewStats(ISpacedRepetitionDb db) : IReviewStats
{
    public async Task<VocabularyProgress> GetProgressAsync(Guid userId, double activeStabilityDays, CancellationToken cancellationToken)
    {
        var cards = await db.Cards.AsNoTracking()
            .Where(c => c.UserId == userId && c.State != CardState.New)
            .Select(c => new { c.UserWordId, c.Mode, c.State, c.Stability })
            .ToListAsync(cancellationToken);

        int learning = 0, passive = 0, active = 0;
        foreach (var word in cards.GroupBy(c => c.UserWordId))
        {
            switch (Stage(word.Select(c => (c.Mode, c.State, c.Stability)), activeStabilityDays))
            {
                case "Active":
                    active++;
                    break;
                case "Passive":
                    passive++;
                    break;
                default:
                    learning++;
                    break;
            }
        }

        return new VocabularyProgress(learning, passive, active);
    }

    public async Task<IReadOnlyList<WordProgress>> GetWordProgressAsync(Guid userId, double activeStabilityDays, CancellationToken cancellationToken)
    {
        var cards = await db.Cards.AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => new { c.UserWordId, c.Mode, c.State, c.Stability, c.Reps, c.Lapses, c.Due })
            .ToListAsync(cancellationToken);

        return cards
            .GroupBy(c => c.UserWordId)
            .Select(word =>
            {
                bool started = word.Any(c => c.State != CardState.New);
                return new WordProgress(
                    word.Key,
                    started ? Stage(word.Select(c => (c.Mode, c.State, c.Stability)), activeStabilityDays) : "New",
                    word.Sum(c => c.Reps),
                    word.Sum(c => c.Lapses),
                    started ? word.Where(c => c.State != CardState.New).Min(c => c.Due) : null);
            })
            .ToList();
    }

    /// <summary>
    /// Этап слова по его карточкам: Active — вспоминание выучено со стабильностью не меньше порога;
    /// Passive — узнавание выучено или уже есть карточка вспоминания; иначе Learning.
    /// </summary>
    public static string Stage(IEnumerable<(CardMode Mode, CardState State, double Stability)> cards, double activeStabilityDays)
    {
        var list = cards.ToList();
        var recognition = list.FirstOrDefault(c => c.Mode == CardMode.Recognition);
        bool hasRecall = list.Any(c => c.Mode == CardMode.Recall);
        var recall = list.FirstOrDefault(c => c.Mode == CardMode.Recall);

        if (hasRecall && recall.State == CardState.Review && recall.Stability >= activeStabilityDays)
        {
            return "Active";
        }

        return (list.Any(c => c.Mode == CardMode.Recognition) && recognition.State == CardState.Review) || hasRecall ? "Passive" : "Learning";
    }

    public async Task<IReadOnlyList<DailyReviews>> GetDailyAsync(Guid userId, DateTimeOffset from, string timeZone, CancellationToken cancellationToken)
    {
        TimeZoneInfo zone = TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out TimeZoneInfo? found) ? found : TimeZoneInfo.Utc;
        var logs = await db.Logs.AsNoTracking()
            .Where(l => l.UserId == userId && l.ReviewedAt >= from)
            .Select(l => new { l.ReviewedAt, l.Rating, l.StateBefore, l.Mode, l.ElapsedMs })
            .ToListAsync(cancellationToken);

        return logs
            .GroupBy(l => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(l.ReviewedAt, zone).DateTime))
            .OrderBy(g => g.Key)
            .Select(g => new DailyReviews(
                g.Key,
                g.Count(),
                g.Count(l => l.Rating != Rating.Again),
                g.Count(l => l.StateBefore == CardState.New && l.Mode == CardMode.Recognition),
                g.Sum(l => (long)l.ElapsedMs)))
            .ToList();
    }
}
