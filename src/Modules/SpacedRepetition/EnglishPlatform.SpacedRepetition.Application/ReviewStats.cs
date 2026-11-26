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
            var recognition = word.FirstOrDefault(c => c.Mode == CardMode.Recognition);
            var recall = word.FirstOrDefault(c => c.Mode == CardMode.Recall);

            if (recall is { State: CardState.Review } && recall.Stability >= activeStabilityDays)
            {
                active++;
            }
            else if (recognition is { State: CardState.Review } || recall is not null)
            {
                passive++;
            }
            else
            {
                learning++;
            }
        }

        return new VocabularyProgress(learning, passive, active);
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
