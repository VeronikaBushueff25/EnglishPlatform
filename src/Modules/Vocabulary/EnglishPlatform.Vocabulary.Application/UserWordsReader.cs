using EnglishPlatform.Vocabulary.Contracts;
using EnglishPlatform.Vocabulary.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnglishPlatform.Vocabulary.Application;

public sealed class UserWordsReader(IVocabularyDb db) : IUserWordsReader
{
    private const int CandidatePool = 200;

    public async Task<IReadOnlyList<Guid>> ListIdsAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.UserWords.AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderBy(w => w.AddedAt)
            .ThenBy(w => w.Id)
            .Select(w => w.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LearningWord>> GetAsync(Guid userId, IReadOnlyCollection<Guid> wordIds, CancellationToken cancellationToken)
    {
        List<Guid> ids = wordIds.Distinct().ToList();
        List<UserWord> words = await db.UserWords.AsNoTracking()
            .Where(w => w.UserId == userId && ids.Contains(w.Id))
            .ToListAsync(cancellationToken);

        List<Guid> lexemeIds = words.Where(w => w.LexemeId != null).Select(w => w.LexemeId!.Value).Distinct().ToList();
        Dictionary<Guid, string?> audio = await db.Lexemes.AsNoTracking()
            .Where(l => lexemeIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.AudioUrl, cancellationToken);

        return words.Select(w => new LearningWord(
                w.Id,
                w.LexemeId,
                w.Text,
                w.Type.ToString(),
                w.PartOfSpeech?.ToString(),
                w.Level?.ToString(),
                w.Transcription,
                w.Translations,
                w.Example,
                w.ExampleTranslation,
                w.LexemeId is { } lexemeId ? audio.GetValueOrDefault(lexemeId) : null))
            .ToList();
    }

    public async Task<IReadOnlyList<string>> GetEnglishDistractorsAsync(Guid userId, Guid wordId, int count, CancellationToken cancellationToken)
    {
        UserWord? word = await db.UserWords.AsNoTracking().SingleOrDefaultAsync(w => w.Id == wordId && w.UserId == userId, cancellationToken);
        if (word is null || count <= 0)
        {
            return [];
        }

        // Правдоподобные варианты: тот же тип и часть речи, длина ±40%; сначала свои слова, затем общая база.
        var own = await db.UserWords.AsNoTracking()
            .Where(w => w.UserId == userId && w.Id != wordId && w.NormalizedText != word.NormalizedText)
            .Select(w => new { w.Text, w.Type, w.PartOfSpeech })
            .Take(CandidatePool)
            .ToListAsync(cancellationToken);
        var shared = await db.Lexemes.AsNoTracking()
            .Where(l => l.NormalizedText != word.NormalizedText && l.Type == word.Type)
            .OrderBy(l => l.Id)
            .Select(l => new { l.Text, l.Type, l.PartOfSpeech })
            .Take(CandidatePool)
            .ToListAsync(cancellationToken);

        int length = word.Text.Length;
        return own.Select(c => (c.Text, c.Type, c.PartOfSpeech, Own: true))
            .Concat(shared.Select(c => (c.Text, c.Type, c.PartOfSpeech, Own: false)))
            .Select(c => (c.Text, Score: (c.Type == word.Type ? 2 : 0) + (c.PartOfSpeech == word.PartOfSpeech ? 1 : 0)
                                          + (Math.Abs(c.Text.Length - length) <= Math.Max(2, length * 0.4) ? 1 : 0) + (c.Own ? 1 : 0)))
            .GroupBy(c => c.Text.ToLowerInvariant())
            .Select(g => g.First())
            .OrderByDescending(c => c.Score)
            .ThenBy(_ => Random.Shared.Next())
            .Take(count)
            .Select(c => c.Text)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> GetDistractorsAsync(Guid userId, Guid wordId, int count, CancellationToken cancellationToken)
    {
        UserWord? word = await db.UserWords.AsNoTracking().SingleOrDefaultAsync(w => w.Id == wordId && w.UserId == userId, cancellationToken);
        if (word is null || count <= 0)
        {
            return [];
        }

        var own = new HashSet<string>(word.Translations.Select(TextNormalizer.Normalize));
        var picked = new List<string>();
        var seen = new HashSet<string>(own);

        void Take(IEnumerable<(string Translation, bool SamePart)> candidates)
        {
            // Сначала та же часть речи и тот же тип — так варианты правдоподобнее; внутри группы — случайно.
            foreach ((string translation, _) in candidates.OrderByDescending(c => c.SamePart).ThenBy(_ => Random.Shared.Next()))
            {
                if (picked.Count >= count)
                {
                    return;
                }

                if (seen.Add(TextNormalizer.Normalize(translation)))
                {
                    picked.Add(translation);
                }
            }
        }

        var userCandidates = await db.UserWords.AsNoTracking()
            .Where(w => w.UserId == userId && w.Id != wordId)
            .OrderByDescending(w => w.PartOfSpeech == word.PartOfSpeech && w.Type == word.Type)
            .ThenByDescending(w => w.AddedAt)
            .Take(CandidatePool)
            .Select(w => new { w.Translations, w.PartOfSpeech, w.Type })
            .ToListAsync(cancellationToken);
        Take(userCandidates.Where(c => c.Translations.Count > 0)
            .Select(c => (c.Translations[0], c.PartOfSpeech == word.PartOfSpeech && c.Type == word.Type)));

        if (picked.Count < count)
        {
            List<Lexeme> lexemes = await db.Lexemes.AsNoTracking()
                .Where(l => l.NormalizedText != word.NormalizedText)
                .OrderByDescending(l => l.PartOfSpeech == word.PartOfSpeech && l.Type == word.Type)
                .ThenBy(l => l.Id)
                .Take(CandidatePool)
                .ToListAsync(cancellationToken);
            Take(lexemes
                .Select(l => (Translation: l.TranslationsFor(Lexeme.DefaultLanguage).FirstOrDefault(), SamePart: l.PartOfSpeech == word.PartOfSpeech && l.Type == word.Type))
                .Where(c => c.Translation is not null)
                .Select(c => (c.Translation!, c.SamePart)));
        }

        return picked;
    }
}
