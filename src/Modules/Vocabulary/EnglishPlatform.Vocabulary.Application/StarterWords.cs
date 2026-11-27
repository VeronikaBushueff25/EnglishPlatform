using EnglishPlatform.BuildingBlocks.Events;
using EnglishPlatform.Vocabulary.Contracts;
using EnglishPlatform.Vocabulary.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnglishPlatform.Vocabulary.Application;

public sealed class StarterWords(IVocabularyDb db, IEventBus eventBus, TimeProvider time) : IStarterWords
{
    public async Task<StarterWordsResult> AddAsync(
        Guid userId,
        IReadOnlyCollection<string> levels,
        IReadOnlyCollection<string> preferredCategorySlugs,
        int limit,
        CancellationToken cancellationToken)
    {
        List<CefrLevel> wanted = levels
            .Select(l => Enum.TryParse(l, ignoreCase: true, out CefrLevel level) ? level : (CefrLevel?)null)
            .Where(l => l is not null)
            .Select(l => l!.Value)
            .Distinct()
            .ToList();
        if (wanted.Count == 0 || limit <= 0)
        {
            return new StarterWordsResult(0, 0);
        }

        List<string> slugs = preferredCategorySlugs.Select(TextNormalizer.Normalize).ToList();
        HashSet<Guid> preferred = (await db.Categories.AsNoTracking()
                .Where(c => slugs.Contains(c.Slug))
                .Select(c => c.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var mine = await db.UserWords.AsNoTracking()
            .Where(w => w.UserId == userId)
            .Select(w => new { w.LexemeId, w.NormalizedText })
            .ToListAsync(cancellationToken);
        HashSet<Guid> ownLexemes = mine.Where(w => w.LexemeId != null).Select(w => w.LexemeId!.Value).ToHashSet();
        HashSet<string> ownTexts = mine.Select(w => w.NormalizedText).ToHashSet();

        List<Lexeme> candidates = (await db.Lexemes.AsNoTracking()
                .Where(l => l.Level != null && wanted.Contains(l.Level.Value))
                .ToListAsync(cancellationToken))
            .Where(l => !ownLexemes.Contains(l.Id) && !ownTexts.Contains(l.NormalizedText))
            .ToList();

        // Сначала свой уровень (младший из запрошенных), внутри — слова из категорий под цели, затем по алфавиту.
        List<Lexeme> chosen = candidates
            .OrderBy(l => l.Level)
            .ThenByDescending(l => l.CategoryIds.Any(preferred.Contains))
            .ThenBy(l => l.NormalizedText)
            .DistinctBy(l => l.NormalizedText)
            .Take(limit)
            .ToList();

        DateTimeOffset now = time.GetUtcNow();
        List<UserWord> words = chosen.Select(l => UserWord.FromLexeme(userId, l, Lexeme.DefaultLanguage, now)).ToList();
        db.UserWords.AddRange(words);
        await db.SaveChangesAsync(cancellationToken);
        foreach (UserWord word in words)
        {
            await eventBus.PublishAsync(new UserWordAdded(userId, word.Id, now, ByUser: false), cancellationToken);
        }

        return new StarterWordsResult(words.Count, candidates.Count);
    }
}
