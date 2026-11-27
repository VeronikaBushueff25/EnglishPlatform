using EnglishPlatform.BuildingBlocks.Events;
using EnglishPlatform.BuildingBlocks.Results;
using EnglishPlatform.BuildingBlocks.Security;
using EnglishPlatform.Vocabulary.Application.Import;
using EnglishPlatform.Vocabulary.Contracts;
using EnglishPlatform.Vocabulary.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnglishPlatform.Vocabulary.Application;

/// <summary>Управление общей базой лексики и категориями (роль admin).</summary>
public sealed class VocabularyAdminService(IVocabularyDb db, ICurrentUser currentUser, IEventBus eventBus, TimeProvider timeProvider)
{
    private const string Language = Lexeme.DefaultLanguage;

    public async Task<Result<LexemeDto>> CreateLexemeAsync(SaveLexemeRequest request, CancellationToken cancellationToken)
    {
        LexemeData data = request.ToData();
        if (await ValidateLexemeAsync(data, excludeId: null, cancellationToken) is { } error)
        {
            return error;
        }

        var lexeme = Lexeme.Create(data, timeProvider.GetUtcNow());
        db.Lexemes.Add(lexeme);
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(lexeme, cancellationToken);
    }

    public async Task<Result<LexemeDto>> UpdateLexemeAsync(Guid id, SaveLexemeRequest request, CancellationToken cancellationToken)
    {
        Lexeme? lexeme = await db.Lexemes.SingleOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (lexeme is null)
        {
            return VocabularyErrors.LexemeNotFound;
        }

        LexemeData data = request.ToData();
        if (await ValidateLexemeAsync(data, excludeId: id, cancellationToken) is { } error)
        {
            return error;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        lexeme.Update(data, now);
        await SyncUserWordsAsync([lexeme], now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(lexeme, cancellationToken);
    }

    /// <summary>Удаляет слово из общей базы. В личных словарях оно остаётся своим словом пользователя.</summary>
    public async Task<Result> DeleteLexemeAsync(Guid id, CancellationToken cancellationToken)
    {
        Lexeme? lexeme = await db.Lexemes.SingleOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (lexeme is null)
        {
            return VocabularyErrors.LexemeNotFound;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        List<UserWord> words = await db.UserWords.Where(w => w.LexemeId == id).ToListAsync(cancellationToken);
        words.ForEach(w => w.DetachFromLexeme(now));
        db.Lexemes.Remove(lexeme);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Импорт общей базы из CSV. Слово с тем же текстом и частью речи считается тем же: пропускается
    /// или обновляется (<see cref="ImportCsvRequest.UpdateExisting"/>). Категории указываются по идентификатору или названию.
    /// </summary>
    public async Task<Result<ImportResult>> ImportLexemesAsync(ImportCsvRequest request, CancellationToken cancellationToken)
    {
        (IReadOnlyList<CsvRow> rows, string? error) = CsvTable.Parse(request.Content, ImportLimits.MaxRows);
        if (error is not null)
        {
            return ImportErrors.InvalidFile(error);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        var issues = new ImportIssues();

        List<Category> categories = await db.Categories.AsNoTracking().ToListAsync(cancellationToken);
        var categoryByKey = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (Category category in categories)
        {
            categoryByKey.TryAdd(category.Slug, category.Id);
            categoryByKey.TryAdd(TextNormalizer.Normalize(category.Name), category.Id);
        }

        List<string> texts = rows.Select(r => TextNormalizer.Normalize(r.Get(CsvTable.Columns.Text))).Where(t => t.Length > 0).Distinct().ToList();
        // В общей базе одно слово — одна запись: совпадение ищем по тексту, часть речи не важна.
        Dictionary<string, Lexeme> existing = (await db.Lexemes
                .Where(l => texts.Contains(l.NormalizedText))
                .ToListAsync(cancellationToken))
            .ToDictionary(l => l.NormalizedText);

        // Слово встретилось в файле ещё раз (book — книга; book — бронировать) — это не повтор, а ещё значения:
        // склеиваем в одну запись.
        var inFile = new Dictionary<string, (LexemeData Data, Lexeme? Lexeme)>();
        var updated = new List<Lexeme>();
        var forMyWords = new List<(Lexeme Lexeme, string? Note)>();
        int added = 0;
        int skipped = 0;

        foreach (CsvRow row in rows)
        {
            if (ParseLexemeRow(row, categoryByKey, issues) is not ({ } data, var note))
            {
                continue;
            }

            string key = TextNormalizer.Normalize(data.Text);
            if (inFile.TryGetValue(key, out (LexemeData Data, Lexeme? Lexeme) earlier))
            {
                LexemeData merged = MergeRows(earlier.Data, data);
                earlier.Lexeme?.Update(merged, now);
                inFile[key] = (merged, earlier.Lexeme);
                continue;
            }

            if (existing.TryGetValue(key, out Lexeme? lexeme))
            {
                if (request.UpdateExisting)
                {
                    lexeme.Update(data, now);
                    updated.Add(lexeme);
                }
                else
                {
                    skipped++;
                    inFile[key] = (data, null);
                    forMyWords.Add((lexeme, note));
                    continue;
                }
            }
            else
            {
                lexeme = Lexeme.Create(data, now);
                db.Lexemes.Add(lexeme);
                added++;
            }

            inFile[key] = (data, lexeme);
            forMyWords.Add((lexeme, note));
        }

        await SyncUserWordsAsync(updated, now, cancellationToken);
        (List<UserWord> created, int inMyWords) = request.AddToMyWords
            ? await AddToMyWordsAsync(forMyWords, now, cancellationToken)
            : ([], 0);
        await db.SaveChangesAsync(cancellationToken);
        await PublishAddedAsync(created, now, cancellationToken);

        return new ImportResult(added, updated.Count, skipped, issues.ToList(), inMyWords);
    }

    /// <summary>
    /// Переносит свои слова текущего администратора в общую базу и оставляет их в его словаре уже как слова базы.
    /// Если такое слово в базе уже есть, своё слово привязывается к нему, а перевод остаётся своим.
    /// Тип и часть речи уточняются по заметке («Глагол; …», «Фразовый глагол») и по виду фразы.
    /// </summary>
    public async Task<ImportResult> PublishMyWordsAsync(CancellationToken cancellationToken)
    {
        Guid userId = currentUser.UserId;
        DateTimeOffset now = timeProvider.GetUtcNow();
        List<UserWord> words = await db.UserWords
            .Where(w => w.UserId == userId && w.LexemeId == null)
            .OrderBy(w => w.AddedAt)
            .ToListAsync(cancellationToken);

        List<string> texts = words.Select(w => w.NormalizedText).Distinct().ToList();
        Dictionary<string, List<Lexeme>> byText = (await db.Lexemes.Where(l => texts.Contains(l.NormalizedText)).ToListAsync(cancellationToken))
            .GroupBy(l => l.NormalizedText)
            .ToDictionary(g => g.Key, g => g.ToList());

        int created = 0;
        int linked = 0;
        foreach (UserWord word in words)
        {
            // Тип «слово/фраза» мог быть выбран по умолчанию — уточняем; идиому и фразовый глагол пользователь указал сам.
            Classification kind = word.Type is LexemeType.Word or LexemeType.Phrase
                ? LexemeClassifier.Classify(word.Text, null, null, word.Note)
                : new Classification(word.Type, word.PartOfSpeech, word.Note);
            PartOfSpeech? partOfSpeech = word.PartOfSpeech ?? kind.PartOfSpeech;
            word.Edit(word.ToData() with { Type = kind.Type, PartOfSpeech = partOfSpeech, Note = kind.Note }, now);

            List<Lexeme> candidates = byText.TryGetValue(word.NormalizedText, out List<Lexeme>? found) ? found : [];
            Lexeme? match = candidates.FirstOrDefault();
            if (match is not null)
            {
                word.LinkToLexeme(match, Language, keepOwnContent: true, now);
                linked++;
                continue;
            }

            Lexeme lexeme = Lexeme.Create(
                new LexemeData(
                    word.Text,
                    kind.Type,
                    partOfSpeech,
                    word.Transcription,
                    word.Level,
                    null,
                    word.Translations.Select(t => new LexemeTranslation(Language, t)).ToList(),
                    word.Example is null ? [] : [new LexemeExample(word.Example, word.ExampleTranslation)],
                    []),
                now);
            db.Lexemes.Add(lexeme);
            byText[word.NormalizedText] = [.. candidates, lexeme];
            word.LinkToLexeme(lexeme, Language, keepOwnContent: false, now);
            created++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ImportResult(created, linked, 0, []);
    }

    /// <summary>Уровень и/или категория сразу для нескольких слов базы. Непоправленные копии в личных словарях обновляются.</summary>
    public async Task<Result<BulkResult>> BulkUpdateLexemesAsync(BulkLexemesRequest request, CancellationToken cancellationToken)
    {
        if (request.AddCategoryId is { } categoryId && !await db.Categories.AnyAsync(c => c.Id == categoryId, cancellationToken))
        {
            return VocabularyErrors.CategoryNotFound;
        }

        List<Guid> ids = request.Ids.Distinct().ToList();
        List<Lexeme> lexemes = await db.Lexemes.Where(l => ids.Contains(l.Id)).ToListAsync(cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        foreach (Lexeme lexeme in lexemes)
        {
            if (request.Level is { } level)
            {
                lexeme.SetLevel(level, now);
            }

            if (request.AddCategoryId is { } category)
            {
                lexeme.AddCategory(category, now);
            }
        }

        await SyncUserWordsAsync(lexemes, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new BulkResult(lexemes.Count);
    }

    /// <summary>
    /// Кладёт слова базы в словарь администратора. Своё слово с тем же текстом не дублируется, а привязывается к слову базы;
    /// если его перевод отличается, перевод остаётся своим. Возвращает новые слова словаря (для событий) и сколько слов
    /// оказалось в словаре благодаря импорту (новые плюс привязанные).
    /// </summary>
    private async Task<(List<UserWord> Created, int InMyWords)> AddToMyWordsAsync(
        IReadOnlyList<(Lexeme Lexeme, string? Note)> items, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Guid userId = currentUser.UserId;
        List<Guid> ids = items.Select(i => i.Lexeme.Id).Distinct().ToList();
        List<string> texts = items.Select(i => i.Lexeme.NormalizedText).Distinct().ToList();
        List<UserWord> mine = await db.UserWords
            .Where(w => w.UserId == userId
                        && ((w.LexemeId != null && ids.Contains(w.LexemeId.Value)) || (w.LexemeId == null && texts.Contains(w.NormalizedText))))
            .ToListAsync(cancellationToken);

        HashSet<Guid> alreadyLinked = mine.Where(w => w.LexemeId != null).Select(w => w.LexemeId!.Value).ToHashSet();
        Dictionary<string, UserWord> customByText = mine.Where(w => w.LexemeId == null).ToDictionary(w => w.NormalizedText);
        var created = new List<UserWord>();
        int inMyWords = 0;

        foreach ((Lexeme lexeme, string? note) in items)
        {
            if (!alreadyLinked.Add(lexeme.Id))
            {
                continue;
            }

            if (customByText.Remove(lexeme.NormalizedText, out UserWord? custom))
            {
                bool sameTranslations = custom.Translations.Order(StringComparer.OrdinalIgnoreCase)
                    .SequenceEqual(lexeme.TranslationsFor(Language).Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
                custom.LinkToLexeme(lexeme, Language, keepOwnContent: !sameTranslations, now);
                inMyWords++;
                continue;
            }

            UserWord word = UserWord.FromLexeme(userId, lexeme, Language, now);
            if (note is not null)
            {
                word.Edit(word.ToData() with { Note = note }, now);
            }

            created.Add(word);
            inMyWords++;
        }

        db.UserWords.AddRange(created);
        return (created, inMyWords);
    }

    private async Task PublishAddedAsync(IReadOnlyList<UserWord> words, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (UserWord word in words)
        {
            await eventBus.PublishAsync(new UserWordAdded(word.UserId, word.Id, now, ByUser: false), cancellationToken);
        }
    }

    public async Task<Result<CategoryDto>> CreateCategoryAsync(SaveCategoryRequest request, CancellationToken cancellationToken)
    {
        string slug = TextNormalizer.Normalize(request.Slug);
        if (await db.Categories.AnyAsync(c => c.Slug == slug, cancellationToken))
        {
            return VocabularyErrors.SlugTaken;
        }

        var category = Category.Create(request.Name, request.Slug, request.Description);
        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return new CategoryDto(category.Id, category.Name, category.Slug, category.Description, 0);
    }

    public async Task<Result<CategoryDto>> UpdateCategoryAsync(Guid id, SaveCategoryRequest request, CancellationToken cancellationToken)
    {
        Category? category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return VocabularyErrors.CategoryNotFound;
        }

        string slug = TextNormalizer.Normalize(request.Slug);
        if (await db.Categories.AnyAsync(c => c.Slug == slug && c.Id != id, cancellationToken))
        {
            return VocabularyErrors.SlugTaken;
        }

        category.Update(request.Name, request.Slug, request.Description);
        await db.SaveChangesAsync(cancellationToken);
        int count = await db.Lexemes.CountAsync(l => l.CategoryIds.Contains(id), cancellationToken);
        return new CategoryDto(category.Id, category.Name, category.Slug, category.Description, count);
    }

    /// <summary>Удаляет категорию и снимает её со слов; сами слова остаются.</summary>
    public async Task<Result> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken)
    {
        Category? category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return VocabularyErrors.CategoryNotFound;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        List<Lexeme> lexemes = await db.Lexemes.Where(l => l.CategoryIds.Contains(id)).ToListAsync(cancellationToken);
        lexemes.ForEach(l => l.RemoveCategory(id, now));
        db.Categories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static (LexemeData Data, string? Note)? ParseLexemeRow(CsvRow row, Dictionary<string, Guid> categoryByKey, ImportIssues issues)
    {
        string? text = row.Get(CsvTable.Columns.Text);
        if (text is null)
        {
            issues.Add(row.Line, "Пустое слово");
            return null;
        }

        if (text.Length > Lexeme.MaxTextLength)
        {
            issues.Add(row.Line, $"Слово длиннее {Lexeme.MaxTextLength} символов");
            return null;
        }

        IReadOnlyList<string> translations = CsvTable.SplitValues(row.Get(CsvTable.Columns.Translation));
        if (translations.Count == 0)
        {
            issues.Add(row.Line, $"«{text}»: нет перевода");
            return null;
        }

        Classification kind = LexemeClassifier.Classify(
            text, row.Get(CsvTable.Columns.Type), row.Get(CsvTable.Columns.PartOfSpeech), row.Get(CsvTable.Columns.Note));
        if (kind.Error is not null)
        {
            issues.Add(row.Line, $"«{text}»: {kind.Error}");
            return null;
        }

        LexemeType type = kind.Type;
        PartOfSpeech? partOfSpeech = kind.PartOfSpeech;

        if (!ImportValueParser.TryParseLevel(row.Get(CsvTable.Columns.Level), out CefrLevel? level))
        {
            issues.Add(row.Line, $"«{text}»: уровень должен быть A1–C2, а не «{row.Get(CsvTable.Columns.Level)}»");
            return null;
        }

        string? audioUrl = row.Get(CsvTable.Columns.AudioUrl);
        if (audioUrl is not null && !(Uri.TryCreate(audioUrl, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https"))
        {
            issues.Add(row.Line, $"«{text}»: аудио должно быть ссылкой http(s), строка загружена без него");
            audioUrl = null;
        }

        var categoryIds = new List<Guid>();
        foreach (string name in CsvTable.SplitValues(row.Get(CsvTable.Columns.Categories)))
        {
            if (categoryByKey.TryGetValue(TextNormalizer.Normalize(name), out Guid categoryId))
            {
                categoryIds.Add(categoryId);
            }
            else
            {
                issues.Add(row.Line, $"«{text}»: категории «{name}» нет, слово загружено без неё");
            }
        }

        string? example = row.Get(CsvTable.Columns.Example);
        LexemeExample[] examples = example is null ? [] : [new LexemeExample(Truncate(example, Lexeme.MaxExampleLength), TruncateOrNull(row.Get(CsvTable.Columns.ExampleTranslation), Lexeme.MaxExampleLength))];

        var data = new LexemeData(
            text,
            type,
            partOfSpeech,
            TruncateOrNull(row.Get(CsvTable.Columns.Transcription), Lexeme.MaxTranscriptionLength),
            level,
            audioUrl,
            translations.Select(t => new LexemeTranslation(Lexeme.DefaultLanguage, Truncate(t, Lexeme.MaxTranslationLength))).ToList(),
            examples,
            categoryIds);
        return (data, TruncateOrNull(kind.Note, UserWord.MaxNoteLength));
    }

    /// <summary>
    /// Две строки одного слова в файле: главная — с более базовым уровнем (при равенстве — первая),
    /// переводы, примеры и категории второй добавляются следом.
    /// </summary>
    public static LexemeData MergeRows(LexemeData first, LexemeData second)
    {
        int Rank(LexemeData d) => d.Level is { } level ? (int)level : int.MaxValue;
        (LexemeData main, LexemeData other) = Rank(second) < Rank(first) ? (second, first) : (first, second);
        return main with
        {
            Transcription = main.Transcription ?? other.Transcription,
            Translations = main.Translations.Concat(other.Translations).ToList(),
            Examples = main.Examples.Concat(other.Examples).DistinctBy(e => e.Text.ToLowerInvariant()).ToList(),
            CategoryIds = main.CategoryIds.Union(other.CategoryIds).ToList(),
        };
    }

    private async Task<Error?> ValidateLexemeAsync(LexemeData data, Guid? excludeId, CancellationToken cancellationToken)
    {
        List<Guid> categoryIds = data.CategoryIds.Distinct().ToList();
        List<Guid> known = await db.Categories.Where(c => categoryIds.Contains(c.Id)).Select(c => c.Id).ToListAsync(cancellationToken);
        if (known.Count != categoryIds.Count)
        {
            return VocabularyErrors.UnknownCategories(categoryIds.Except(known));
        }

        string normalized = TextNormalizer.Normalize(data.Text);
        bool duplicate = await db.Lexemes.AnyAsync(l => l.NormalizedText == normalized && l.Id != excludeId, cancellationToken);
        return duplicate ? VocabularyErrors.DuplicateLexeme : null;
    }

    /// <summary>Обновляет копии слова в личных словарях, которые пользователи не правили.</summary>
    private async Task SyncUserWordsAsync(IReadOnlyCollection<Lexeme> lexemes, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (lexemes.Count == 0)
        {
            return;
        }

        Dictionary<Guid, Lexeme> byId = lexemes.ToDictionary(l => l.Id);
        List<Guid> ids = [.. byId.Keys];
        List<UserWord> words = await db.UserWords
            .Where(w => w.LexemeId != null && ids.Contains(w.LexemeId.Value) && !w.IsEdited)
            .ToListAsync(cancellationToken);

        foreach (UserWord word in words)
        {
            word.SyncWith(byId[word.LexemeId!.Value], Lexeme.DefaultLanguage, now);
        }
    }

    private async Task<LexemeDto> ToDtoAsync(Lexeme lexeme, CancellationToken cancellationToken)
    {
        List<CategoryRefDto> categories = await db.Categories.AsNoTracking()
            .Where(c => lexeme.CategoryIds.Contains(c.Id))
            .OrderBy(c => c.Name)
            .Select(c => new CategoryRefDto(c.Id, c.Name, c.Slug))
            .ToListAsync(cancellationToken);
        return LexemeCatalog.ToDto(lexeme, categories, myWordId: null);
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private static string? TruncateOrNull(string? value, int max) => value is null ? null : Truncate(value, max);
}
