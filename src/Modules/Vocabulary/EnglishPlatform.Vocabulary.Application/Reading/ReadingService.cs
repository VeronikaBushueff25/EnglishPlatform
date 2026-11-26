using System.ComponentModel.DataAnnotations;
using EnglishPlatform.BuildingBlocks.Paging;
using EnglishPlatform.BuildingBlocks.Results;
using EnglishPlatform.BuildingBlocks.Security;
using EnglishPlatform.Vocabulary.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnglishPlatform.Vocabulary.Application.Reading;

public sealed record ReadingTextSummaryDto(Guid Id, string Title, CefrLevel Level, string? Topic, int WordCount, string Preview, bool IsPublished);

public sealed record ReadingTextDto(Guid Id, string Title, CefrLevel Level, string? Topic, string Body, int WordCount, bool IsPublished, DateTimeOffset UpdatedAt);

public sealed record SaveReadingTextRequest(
    [property: Required, MaxLength(ReadingText.MaxTitleLength)] string Title,
    CefrLevel Level,
    [property: MaxLength(ReadingText.MaxTopicLength)] string? Topic,
    [property: Required, MaxLength(ReadingText.MaxBodyLength)] string Body,
    bool IsPublished = true);

/// <summary>Слово для игр: текст, переводы, транскрипция. Игры не меняют расписание повторений.</summary>
public sealed record PracticeWordDto(Guid Id, string Text, IReadOnlyList<string> Translations, string? Transcription, LexemeType Type);

public static class ReadingErrors
{
    public static readonly Error TextNotFound = Error.NotFound("reading.text_not_found", "Текст не найден");
}

/// <summary>Тексты для чтения и подборки слов для игр.</summary>
public sealed class ReadingService(IVocabularyDb db, ICurrentUser currentUser, TimeProvider time)
{
    public const int MaxPracticeWords = 60;

    /// <summary>Опубликованные тексты; <paramref name="all"/> — вместе со скрытыми (для админки).</summary>
    public async Task<PagedResult<ReadingTextSummaryDto>> ListAsync(CefrLevel? level, bool all, PageRequest page, CancellationToken cancellationToken)
    {
        IQueryable<ReadingText> query = db.ReadingTexts.AsNoTracking();
        if (!all)
        {
            query = query.Where(t => t.IsPublished);
        }

        if (level is { } l)
        {
            query = query.Where(t => t.Level == l);
        }

        long total = await query.LongCountAsync(cancellationToken);
        List<ReadingText> items = await query.OrderBy(t => t.Level).ThenBy(t => t.Title)
            .Skip(page.Skip).Take(page.SafePageSize).ToListAsync(cancellationToken);
        return new PagedResult<ReadingTextSummaryDto>(items.Select(Summary).ToList(), page.SafePage, page.SafePageSize, total);
    }

    public async Task<Result<ReadingTextDto>> GetAsync(Guid id, bool allowHidden, CancellationToken cancellationToken) =>
        await db.ReadingTexts.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id && (allowHidden || t.IsPublished), cancellationToken) is { } text
            ? ToDto(text)
            : ReadingErrors.TextNotFound;

    public async Task<ReadingTextDto> CreateAsync(SaveReadingTextRequest request, CancellationToken cancellationToken)
    {
        ReadingText text = ReadingText.Create(ToData(request), time.GetUtcNow());
        db.ReadingTexts.Add(text);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(text);
    }

    public async Task<Result<ReadingTextDto>> UpdateAsync(Guid id, SaveReadingTextRequest request, CancellationToken cancellationToken)
    {
        ReadingText? text = await db.ReadingTexts.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (text is null)
        {
            return ReadingErrors.TextNotFound;
        }

        text.Update(ToData(request), time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(text);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        ReadingText? text = await db.ReadingTexts.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (text is null)
        {
            return ReadingErrors.TextNotFound;
        }

        db.ReadingTexts.Remove(text);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Нормализованные тексты всех слов личного словаря — чтобы в тексте подчеркнуть уже знакомые.
    /// </summary>
    public async Task<IReadOnlyList<string>> MyWordTextsAsync(CancellationToken cancellationToken)
    {
        Guid userId = currentUser.UserId;
        return await db.UserWords.AsNoTracking().Where(w => w.UserId == userId).Select(w => w.NormalizedText).Distinct().ToListAsync(cancellationToken);
    }

    /// <summary>Случайные слова личного словаря с переводом — для игр. Пусто, если в словаре меньше <paramref name="minimum"/> слов.</summary>
    public async Task<IReadOnlyList<PracticeWordDto>> PracticeWordsAsync(int count, CancellationToken cancellationToken)
    {
        Guid userId = currentUser.UserId;
        count = Math.Clamp(count, 4, MaxPracticeWords);
        List<UserWord> words = await db.UserWords.AsNoTracking()
            .Where(w => w.UserId == userId && w.Translations.Count > 0)
            .OrderBy(_ => EF.Functions.Random())
            .Take(count)
            .ToListAsync(cancellationToken);
        return words.Select(w => new PracticeWordDto(w.Id, w.Text, w.Translations, w.Transcription, w.Type)).ToList();
    }

    private static ReadingTextData ToData(SaveReadingTextRequest r) => new(r.Title, r.Level, r.Topic, r.Body, r.IsPublished);

    private static ReadingTextSummaryDto Summary(ReadingText t)
    {
        string preview = t.Body.Length > 160 ? t.Body[..t.Body.LastIndexOf(' ', 160)] + "…" : t.Body;
        return new ReadingTextSummaryDto(t.Id, t.Title, t.Level, t.Topic, t.WordCount, preview.Replace('\n', ' '), t.IsPublished);
    }

    private static ReadingTextDto ToDto(ReadingText t) => new(t.Id, t.Title, t.Level, t.Topic, t.Body, t.WordCount, t.IsPublished, t.UpdatedAt);
}
