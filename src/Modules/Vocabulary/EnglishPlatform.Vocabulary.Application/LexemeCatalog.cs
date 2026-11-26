using EnglishPlatform.BuildingBlocks.Paging;
using EnglishPlatform.BuildingBlocks.Results;
using EnglishPlatform.BuildingBlocks.Security;
using EnglishPlatform.Vocabulary.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnglishPlatform.Vocabulary.Application;

public interface IVocabularyDb
{
    DbSet<Lexeme> Lexemes { get; }

    DbSet<Category> Categories { get; }

    DbSet<UserWord> UserWords { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Чтение общей базы лексики и категорий. Отмечает слова, которые уже есть в словаре текущего пользователя.</summary>
public sealed class LexemeCatalog(IVocabularyDb db, ICurrentUser currentUser)
{
    private const int TranslationsInList = 3;

    public async Task<PagedResult<LexemeSummaryDto>> SearchAsync(LexemeFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        IQueryable<Lexeme> query = db.Lexemes.AsNoTracking();

        string key = TextNormalizer.Normalize(filter.Search);
        if (key.Length > 0)
        {
            query = query.Where(l => l.SearchText.Contains(key));
        }

        if (filter.Type is { } type)
        {
            query = query.Where(l => l.Type == type);
        }

        if (filter.NoLevel)
        {
            query = query.Where(l => l.Level == null);
        }
        else if (filter.Level is { } level)
        {
            query = query.Where(l => l.Level == level);
        }

        if (filter.CategoryId is { } categoryId)
        {
            query = query.Where(l => l.CategoryIds.Contains(categoryId));
        }

        long total = await query.LongCountAsync(cancellationToken);

        // При поиске сначала точное совпадение, затем слова, начинающиеся с запроса, затем остальные.
        IOrderedQueryable<Lexeme> ordered = key.Length > 0
            ? query.OrderBy(l => l.NormalizedText == key ? 0 : l.NormalizedText.StartsWith(key) ? 1 : 2).ThenBy(l => l.NormalizedText)
            : query.OrderBy(l => l.NormalizedText);

        List<Lexeme> items = await ordered.ThenBy(l => l.Id)
            .Skip(page.Skip)
            .Take(page.SafePageSize)
            .ToListAsync(cancellationToken);

        Dictionary<Guid, Guid> mine = await MyWordIdsAsync(items.Select(l => l.Id).ToList(), cancellationToken);

        return new PagedResult<LexemeSummaryDto>(
            items.Select(l => new LexemeSummaryDto(
                    l.Id,
                    l.Text,
                    l.Type,
                    l.PartOfSpeech,
                    l.Transcription,
                    l.Level,
                    l.TranslationsFor(Lexeme.DefaultLanguage).Take(TranslationsInList).ToList(),
                    mine.TryGetValue(l.Id, out Guid wordId) ? wordId : null))
                .ToList(),
            page.SafePage,
            page.SafePageSize,
            total);
    }

    public async Task<Result<LexemeDto>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        Lexeme? lexeme = await db.Lexemes.AsNoTracking().SingleOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (lexeme is null)
        {
            return VocabularyErrors.LexemeNotFound;
        }

        List<CategoryRefDto> categories = await db.Categories.AsNoTracking()
            .Where(c => lexeme.CategoryIds.Contains(c.Id))
            .OrderBy(c => c.Name)
            .Select(c => new CategoryRefDto(c.Id, c.Name, c.Slug))
            .ToListAsync(cancellationToken);

        Dictionary<Guid, Guid> mine = await MyWordIdsAsync([lexeme.Id], cancellationToken);
        return ToDto(lexeme, categories, mine.TryGetValue(lexeme.Id, out Guid wordId) ? wordId : null);
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken) =>
        await db.Categories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.Description, db.Lexemes.Count(l => l.CategoryIds.Contains(c.Id))))
            .ToListAsync(cancellationToken);

    internal static LexemeDto ToDto(Lexeme lexeme, IReadOnlyList<CategoryRefDto> categories, Guid? myWordId) => new(
        lexeme.Id,
        lexeme.Text,
        lexeme.Type,
        lexeme.PartOfSpeech,
        lexeme.Transcription,
        lexeme.Level,
        lexeme.AudioUrl,
        lexeme.Translations.Select(t => new TranslationDto(t.Language, t.Text)).ToList(),
        lexeme.Examples.Select(e => new ExampleDto(e.Text, e.Translation)).ToList(),
        categories,
        myWordId,
        lexeme.UpdatedAt);

    private async Task<Dictionary<Guid, Guid>> MyWordIdsAsync(IReadOnlyCollection<Guid> lexemeIds, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || lexemeIds.Count == 0)
        {
            return [];
        }

        Guid userId = currentUser.UserId;
        return await db.UserWords.AsNoTracking()
            .Where(w => w.UserId == userId && w.LexemeId != null && lexemeIds.Contains(w.LexemeId.Value))
            .ToDictionaryAsync(w => w.LexemeId!.Value, w => w.Id, cancellationToken);
    }
}
