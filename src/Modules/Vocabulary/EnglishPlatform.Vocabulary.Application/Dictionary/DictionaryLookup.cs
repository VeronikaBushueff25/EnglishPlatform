using EnglishPlatform.BuildingBlocks.Security;
using EnglishPlatform.Vocabulary.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnglishPlatform.Vocabulary.Application.Dictionary;

/// <summary>
/// Результат поиска слова. <c>FormOf</c> — что искали, если нашли начальную форму (buried → bury).
/// <c>FromDictionary</c> — слова не было в базе, его только что взяли из внешнего словаря.
/// <c>MyCustomWordId</c> — такое слово уже есть среди своих слов пользователя (не из базы).
/// </summary>
public sealed record LookupResultDto(
    string Query,
    string? FormOf,
    IReadOnlyList<LexemeDto> Items,
    bool FromDictionary,
    Guid? MyCustomWordId,
    bool DictionaryUnavailable = false);

/// <summary>
/// Поиск слова для пользователя: сначала общая база (и простые формы: cats → cat, stopped → stop),
/// потом внешний словарь. Найденное во внешнем словаре сохраняется в общую базу с пометкой «проверить» —
/// так база растёт от реального использования, а не загрузкой всего словаря.
/// </summary>
public sealed class DictionaryLookup(
    IVocabularyDb db,
    IDictionarySource source,
    IExampleSource examples,
    ICefrLevels levels,
    ICurrentUser currentUser,
    TimeProvider time)
{
    /// <summary>Сколько частей речи одного слова берём из словаря: delay — существительное и глагол.</summary>
    public const int MaxEntriesPerWord = 2;

    public async Task<LookupResultDto?> LookupAsync(string query, CancellationToken cancellationToken)
    {
        string text = CleanQuery(query);
        if (text.Length == 0 || text.Length > Lexeme.MaxTextLength)
        {
            return null;
        }

        string normalized = TextNormalizer.Normalize(text);
        List<Lexeme> found = await FindAsync([normalized], cancellationToken);
        string? formOf = null;
        bool fromDictionary = false;

        if (found.Count > 0 && !normalized.Contains(' '))
        {
            // В базе есть и сама форма (running — «бег»), и начальная (run): глагол или существительное в начальной
            // форме показываем первыми — в тексте это почти всегда оно.
            // Кандидаты по порядку правил: does → do (а не doe), studies → study.
            foreach (string candidate in Lemmas(normalized))
            {
                Lexeme? lemma = (await FindAsync([candidate], cancellationToken))
                    .FirstOrDefault(l => l.PartOfSpeech is PartOfSpeech.Verb or PartOfSpeech.Noun);
                if (lemma is not null)
                {
                    found = [lemma, .. found.Take(MaxEntriesPerWord)];
                    formOf = text;
                    break;
                }
            }
        }

        if (found.Count == 0 && !normalized.Contains(' '))
        {
            // Простые формы — без запроса во внешний словарь.
            foreach (string candidate in Lemmas(normalized))
            {
                found = await FindAsync([candidate], cancellationToken);
                if (found.Count > 0)
                {
                    formOf = text;
                    break;
                }
            }
        }

        if (found.Count == 0)
        {
            (List<Lexeme> imported, string? lemma) result;
            try
            {
                result = await ImportAsync(text, depth: 0, cancellationToken);
            }
            catch (DictionaryUnavailableException)
            {
                return (await ToResultAsync(text, null, [], false, cancellationToken)) with { DictionaryUnavailable = true };
            }

            (found, string? lemma) = result;
            fromDictionary = found.Count > 0;
            if (lemma is not null && !string.Equals(lemma, normalized, StringComparison.OrdinalIgnoreCase))
            {
                formOf = text;
            }
        }

        if (!fromDictionary)
        {
            foreach (Lexeme lexeme in found)
            {
                await CompleteAsync(lexeme, cancellationToken);
            }
        }

        return await ToResultAsync(text, formOf, found, fromDictionary, cancellationToken);
    }

    /// <summary>
    /// Находит слово в базе или берёт из словаря и сохраняет. Возвращает единицы базы (не больше <see cref="MaxEntriesPerWord"/>)
    /// и начальную форму, если искали форму слова.
    /// </summary>
    public async Task<(List<Lexeme> Lexemes, string? Lemma)> FindOrImportAsync(string text, CancellationToken cancellationToken)
    {
        string normalized = TextNormalizer.Normalize(CleanQuery(text));
        List<Lexeme> found = await FindAsync([normalized], cancellationToken);
        if (found.Count > 0)
        {
            return (found, null);
        }

        try
        {
            return await ImportAsync(normalized, 0, cancellationToken);
        }
        catch (DictionaryUnavailableException)
        {
            return ([], null); // проверим в следующий раз — ответ «недоступен» не кешируется
        }
    }

    /// <summary>
    /// Слово уже в базе, но без транскрипции, части речи или примера (например, пришло из импорта файла) —
    /// дозаполняем пустые поля из внешнего словаря. Ответы словаря кешируются, поэтому повторно это дёшево.
    /// </summary>
    public async Task<bool> CompleteAsync(Lexeme lexeme, CancellationToken cancellationToken)
    {
        bool needsDictionary = lexeme.Transcription is null || lexeme.PartOfSpeech is null || lexeme.Examples.Count == 0;
        bool needsExample = !lexeme.Examples.Any(e => e.Translation is not null);
        if (!needsDictionary && !needsExample && lexeme.Level is not null)
        {
            return false;
        }

        DictionaryEntry? entry = null;
        if (needsDictionary)
        {
            IReadOnlyList<DictionaryEntry> entries;
            try
            {
                entries = await source.LookupAsync(lexeme.Text, cancellationToken);
            }
            catch (DictionaryUnavailableException)
            {
                entries = [];
            }

            entry = entries.FirstOrDefault(e => lexeme.PartOfSpeech is not null && e.PartOfSpeech == lexeme.PartOfSpeech && e.Translations.Count > 0)
                ?? entries.FirstOrDefault(e => e.Translations.Count > 0);
        }

        // Часть речи ставим, только если такой пары «текст + часть речи» в базе ещё нет (уникальный индекс).
        PartOfSpeech? pos = lexeme.PartOfSpeech is null && entry?.PartOfSpeech is { } candidate
            && !await db.Lexemes.AnyAsync(l => l.Id != lexeme.Id && l.NormalizedText == lexeme.NormalizedText && l.PartOfSpeech == candidate, cancellationToken)
                ? candidate
                : null;
        TranslatedExample? example = needsExample ? await examples.FindAsync(lexeme.Text, cancellationToken) : null;
        DateTimeOffset now = time.GetUtcNow();
        bool changed = lexeme.FillGaps(
            entry?.Transcription,
            pos,
            entry?.Examples ?? [],
            now,
            LevelFor(lexeme.Text, lexeme.PartOfSpeech ?? pos, lexeme.Type),
            example is null ? null : new LexemeExample(example.Text, example.Translation));
        if (!changed)
        {
            return false;
        }

        // Слова пользователей, взятые из базы и не правленые вручную, получают то же самое.
        foreach (UserWord word in await db.UserWords.Where(w => w.LexemeId == lexeme.Id && !w.IsEdited).ToListAsync(cancellationToken))
        {
            word.SyncWith(lexeme, Lexeme.DefaultLanguage, now);
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Уровень по спискам CEFR. Одиночного слова нет в ~10 тысячах самых нужных — это редкая лексика, ставим C1
    /// (точнее не определить без частотного словаря); у фраз — оценка по составу (см. <see cref="ICefrLevels"/>).
    /// </summary>
    public CefrLevel? LevelFor(string text, PartOfSpeech? partOfSpeech, LexemeType type) =>
        levels.Find(text, partOfSpeech, type) ?? (type == LexemeType.Word && !text.Trim().Contains(' ') ? CefrLevel.C1 : null);

    /// <summary>Кандидаты начальной формы для английского слова: cats → cat, boxes → box, studies → study, stopped → stop, making → make.</summary>
    public static IReadOnlyList<string> Lemmas(string word)
    {
        var result = new List<string>();
        void Add(string candidate)
        {
            if (candidate.Length >= 2 && candidate != word && !result.Contains(candidate))
            {
                result.Add(candidate);
            }
        }

        if (word.EndsWith("ies", StringComparison.Ordinal))
        {
            Add(word[..^3] + "y");
        }

        if (word.EndsWith("es", StringComparison.Ordinal))
        {
            Add(word[..^2]);
        }

        if (word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal))
        {
            Add(word[..^1]);
        }

        foreach (string suffix in new[] { "ed", "ing" })
        {
            if (!word.EndsWith(suffix, StringComparison.Ordinal) || word.Length <= suffix.Length + 2)
            {
                continue;
            }

            string stem = word[..^suffix.Length];
            if (suffix == "ed" && stem.EndsWith('i'))
            {
                Add(stem[..^1] + "y"); // studied → study
            }

            if (stem.Length >= 3 && stem[^1] == stem[^2] && !"aeiou".Contains(stem[^1]))
            {
                Add(stem[..^1]); // stopped → stop, running → run
            }

            Add(stem);
            Add(stem + "e"); // liked → like, making → make
        }

        if (word.EndsWith("ly", StringComparison.Ordinal) && word.Length > 4)
        {
            Add(word[..^2]); // quickly → quick
        }

        return result;
    }

    private async Task<(List<Lexeme>, string?)> ImportAsync(string text, int depth, CancellationToken cancellationToken)
    {
        IReadOnlyList<DictionaryEntry> entries = await source.LookupAsync(text, cancellationToken);
        List<DictionaryEntry> translated = entries.Where(e => e.Translations.Count > 0).ToList();

        if (translated.Count == 0)
        {
            // Форма слова без своего перевода (buried, ovaries) — берём начальную форму. Один шаг, без циклов.
            string? lemma = entries.Select(e => e.LemmaOf).FirstOrDefault(l => l is not null);
            if (lemma is null || depth > 0)
            {
                return ([], null);
            }

            string normalizedLemma = TextNormalizer.Normalize(lemma);
            List<Lexeme> existing = await FindAsync([normalizedLemma], cancellationToken);
            if (existing.Count > 0)
            {
                return (existing, lemma);
            }

            (List<Lexeme> imported, string? _) = await ImportAsync(lemma, depth + 1, cancellationToken);
            return (imported, lemma);
        }

        // Слово — ещё и форма другого (running → run): начальную форму показываем первой, она важнее для ученика.
        List<Lexeme> lemmaLexemes = [];
        string? formLemma = depth == 0 ? entries.Select(e => e.LemmaOf).FirstOrDefault(l => l is not null) : null;
        if (formLemma is not null)
        {
            lemmaLexemes = await FindAsync([TextNormalizer.Normalize(formLemma)], cancellationToken);
            if (lemmaLexemes.Count == 0)
            {
                (lemmaLexemes, string? _) = await ImportAsync(formLemma, depth + 1, cancellationToken);
            }
        }

        DateTimeOffset now = time.GetUtcNow();
        var created = new List<Lexeme>();
        // Пример с переводом из Tatoeba — один на слово, первой записи (обычно главная часть речи).
        TranslatedExample? withTranslation = await examples.FindAsync(translated[0].Text, cancellationToken);
        foreach (DictionaryEntry entry in translated.DistinctBy(e => e.PartOfSpeech).Take(MaxEntriesPerWord))
        {
            List<LexemeExample> lexemeExamples = entry.Examples.Select(e => new LexemeExample(e, null)).ToList();
            if (created.Count == 0 && withTranslation is not null)
            {
                lexemeExamples.Insert(0, new LexemeExample(withTranslation.Text, withTranslation.Translation));
            }

            var data = new LexemeData(
                entry.Text,
                entry.Type,
                entry.PartOfSpeech,
                entry.Transcription,
                LevelFor(entry.Text, entry.PartOfSpeech, entry.Type),
                null,
                entry.Translations.Select(t => new LexemeTranslation(Lexeme.DefaultLanguage, t)).ToList(),
                lexemeExamples,
                []);
            created.Add(Lexeme.FromDictionary(data, now));
        }

        try
        {
            foreach (Lexeme lexeme in created)
            {
                db.Lexemes.Add(lexeme);
            }

            await db.SaveChangesAsync(cancellationToken);
            return lemmaLexemes.Count > 0 ? ([.. lemmaLexemes.Take(1), .. created], formLemma) : (created, null);
        }
        catch (DbUpdateException)
        {
            // Это же слово только что добавил другой запрос (уникальный индекс текст + часть речи) — берём из базы.
            foreach (Lexeme lexeme in created)
            {
                db.Lexemes.Entry(lexeme).State = EntityState.Detached;
            }

            return (await FindAsync([TextNormalizer.Normalize(translated[0].Text)], cancellationToken), null);
        }
    }

    private Task<List<Lexeme>> FindAsync(IReadOnlyCollection<string> normalized, CancellationToken cancellationToken) =>
        db.Lexemes
            .Where(l => normalized.Contains(l.NormalizedText))
            .OrderBy(l => l.NeedsReview)
            .ThenBy(l => l.CreatedAt)
            .Take(MaxEntriesPerWord + 1)
            .ToListAsync(cancellationToken);

    private async Task<LookupResultDto> ToResultAsync(string query, string? formOf, List<Lexeme> found, bool fromDictionary, CancellationToken cancellationToken)
    {
        Guid userId = currentUser.UserId;
        List<Guid> ids = found.Select(l => l.Id).ToList();
        Dictionary<Guid, Guid> mine = await db.UserWords.AsNoTracking()
            .Where(w => w.UserId == userId && w.LexemeId != null && ids.Contains(w.LexemeId.Value))
            .ToDictionaryAsync(w => w.LexemeId!.Value, w => w.Id, cancellationToken);

        List<string> texts = found.Select(l => l.NormalizedText).Append(TextNormalizer.Normalize(query)).Distinct().ToList();
        Guid? custom = await db.UserWords.AsNoTracking()
            .Where(w => w.UserId == userId && w.LexemeId == null && texts.Contains(w.NormalizedText))
            .Select(w => (Guid?)w.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return new LookupResultDto(
            query,
            formOf,
            found.Select(l => LexemeCatalog.ToDto(l, [], mine.TryGetValue(l.Id, out Guid id) ? id : null)).ToList(),
            fromDictionary,
            custom);
    }

    /// <summary>Слово из текста: без кавычек, точек и скобок по краям; апостроф внутри (don't) остаётся.</summary>
    private static string CleanQuery(string query) =>
        TextNormalizer.Clean(query).Trim('.', ',', '!', '?', ';', ':', '"', '(', ')', '[', ']', '«', '»', '“', '”', '‘', '’', '\'', '-', '—');
}
