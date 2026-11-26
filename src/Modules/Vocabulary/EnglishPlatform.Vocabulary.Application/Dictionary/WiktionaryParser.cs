using System.Globalization;
using System.Text;
using System.Text.Json;
using EnglishPlatform.Vocabulary.Domain;

namespace EnglishPlatform.Vocabulary.Application.Dictionary;

/// <summary>
/// Разбор статей Викисловаря в формате kaikki.org (JSONL: по строке на часть речи) в <see cref="DictionaryEntry"/>.
/// Берём только то, что нужно ученику: русские переводы (без ударений, без повторов), британскую транскрипцию,
/// часть речи, пару примеров, ссылку на начальную форму. Статьи без русского перевода и не формы — пропускаются.
/// Данные Викисловаря — CC BY-SA.
/// </summary>
public static class WiktionaryParser
{
    public const int MaxTranslations = 4;
    public const int MaxExamples = 2;

    /// <summary>Наречные частицы фразовых глаголов: give up, put off, look forward to.</summary>
    private static readonly HashSet<string> Particles = new(StringComparer.OrdinalIgnoreCase)
    {
        "up", "down", "out", "off", "on", "in", "over", "away", "back", "through", "across", "along", "around",
        "round", "about", "by", "forward", "ahead", "apart", "aside", "together", "behind",
    };

    /// <summary>Предлоги предложных глаголов, которые учат как фразовые: deal with, look after, run into.</summary>
    private static readonly HashSet<string> Prepositions = new(StringComparer.OrdinalIgnoreCase)
    {
        "with", "for", "into", "after", "from", "of", "to", "onto", "at",
    };

    public static IReadOnlyList<DictionaryEntry> Parse(string text, string jsonl)
    {
        var entries = new List<DictionaryEntry>();
        foreach (string line in jsonl.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            JsonElement root;
            try
            {
                root = JsonDocument.Parse(line).RootElement;
            }
            catch (JsonException)
            {
                continue;
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string word = Str(root, "word") ?? text;
            string pos = Str(root, "pos") ?? string.Empty;
            PartOfSpeech? partOfSpeech = MapPartOfSpeech(pos);
            if (partOfSpeech is null && pos is not ("phrase" or "prep_phrase" or "proverb" or "verb"))
            {
                continue; // имена собственные, аффиксы, символы
            }

            List<string> translations = Translations(root);
            string? lemma = LemmaOf(root, word);
            if (translations.Count == 0 && lemma is null)
            {
                continue;
            }

            entries.Add(new DictionaryEntry(
                word,
                Classify(word, pos, Categories(root)),
                partOfSpeech,
                Transcription(root),
                translations,
                Examples(root),
                lemma));
        }

        // Сначала статьи с переводом; формы (без перевода) — после.
        return entries.OrderBy(e => e.Translations.Count == 0).ToList();
    }

    /// <summary>
    /// Тип единицы: одно слово — Word; глагол + частица (+ предлог) или глагол + предлог — фразовый глагол;
    /// помеченное идиомой — идиома; остальное из нескольких слов — фраза.
    /// </summary>
    public static LexemeType Classify(string text, string pos, IReadOnlyCollection<string> categories)
    {
        string[] tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 1)
        {
            return LexemeType.Word;
        }

        bool phrasalByTokens = pos == "verb" && tokens.Length is 2 or 3 && (
            Particles.Contains(tokens[1]) && (tokens.Length == 2 || Prepositions.Contains(tokens[2]))
            || (tokens.Length == 2 && Prepositions.Contains(tokens[1]) && tokens[1] != "to"));
        if (phrasalByTokens || categories.Any(c => c.Contains("phrasal verbs", StringComparison.OrdinalIgnoreCase)))
        {
            return LexemeType.PhrasalVerb;
        }

        return categories.Any(c => c.Contains("idioms", StringComparison.OrdinalIgnoreCase)) ? LexemeType.Idiom : LexemeType.Phrase;
    }

    public static PartOfSpeech? MapPartOfSpeech(string pos) => pos switch
    {
        "noun" => Domain.PartOfSpeech.Noun,
        "verb" => Domain.PartOfSpeech.Verb,
        "adj" => Domain.PartOfSpeech.Adjective,
        "adv" => Domain.PartOfSpeech.Adverb,
        "pron" => Domain.PartOfSpeech.Pronoun,
        "prep" => Domain.PartOfSpeech.Preposition,
        "conj" => Domain.PartOfSpeech.Conjunction,
        "det" or "article" => Domain.PartOfSpeech.Determiner,
        "intj" => Domain.PartOfSpeech.Interjection,
        "num" => Domain.PartOfSpeech.Numeral,
        _ => null,
    };

    /// <summary>Убирает знаки ударения (хорони́ть → хоронить), лишние пробелы.</summary>
    public static string CleanRussian(string value)
    {
        string decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
        {
            // U+0301 (ударение) и U+0300 убираем, а «й» и «ё» (U+0306, U+0308) возвращаем при обратной нормализации.
            if (c is '́' or '̀')
            {
                continue;
            }

            builder.Append(c);
        }

        return string.Join(' ', builder.ToString().Normalize(NormalizationForm.FormC).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// По одному переводу на значение (Викисловарь группирует переводы по значениям): work out →
    /// «рассчитывать, понимать, разрабатывать, получаться», а не четыре синонима первого значения.
    /// Из видовой пары берём несовершенный вид (откладывать, а не отложить). Затем добираем до лимита вторыми вариантами.
    /// </summary>
    private static List<string> Translations(JsonElement root)
    {
        var senses = new List<(string Sense, List<string> Words, List<string> Perfective)>();
        void Collect(JsonElement container)
        {
            if (!container.TryGetProperty("translations", out JsonElement list) || list.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (JsonElement t in list.EnumerateArray())
            {
                if (Str(t, "code") != "ru" || Str(t, "word") is not { Length: > 0 } ru)
                {
                    continue;
                }

                string clean = CleanRussian(ru);
                if (clean.Length is 0 or > Lexeme.MaxTranslationLength)
                {
                    continue;
                }

                string sense = Str(t, "sense") ?? string.Empty;
                int index = senses.FindIndex(s => s.Sense == sense);
                if (index < 0)
                {
                    senses.Add((sense, [], []));
                    index = senses.Count - 1;
                }

                List<string> bucket = HasTag(t, "perfective") ? senses[index].Perfective : senses[index].Words;
                if (!bucket.Contains(clean, StringComparer.OrdinalIgnoreCase))
                {
                    bucket.Add(clean);
                }
            }
        }

        Collect(root);
        if (root.TryGetProperty("senses", out JsonElement senseList) && senseList.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement sense in senseList.EnumerateArray())
            {
                Collect(sense);
            }
        }

        // Совершенный вид — только если в этом значении нет несовершенного (смириться, принять у come to terms with).
        for (int i = 0; i < senses.Count; i++)
        {
            if (senses[i].Words.Count == 0)
            {
                senses[i].Words.AddRange(senses[i].Perfective);
            }
        }

        var result = new List<string>();
        for (int round = 0; result.Count < MaxTranslations && senses.Any(s => s.Words.Count > round); round++)
        {
            foreach ((string _, List<string> words, List<string> _) in senses)
            {
                if (words.Count > round && !result.Contains(words[round], StringComparer.OrdinalIgnoreCase) && result.Count < MaxTranslations)
                {
                    result.Add(words[round]);
                }
            }
        }

        return result;
    }

    private static bool HasTag(JsonElement element, string tag) =>
        element.TryGetProperty("tags", out JsonElement tags) && tags.ValueKind == JsonValueKind.Array
        && tags.EnumerateArray().Any(t => t.ValueKind == JsonValueKind.String && t.GetString() == tag);

    /// <summary>Британская (RP) транскрипция, если есть, иначе первая фонематическая (в /косых/, не [узкая]).</summary>
    private static string? Transcription(JsonElement root)
    {
        if (!root.TryGetProperty("sounds", out JsonElement sounds) || sounds.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        string? first = null;
        foreach (JsonElement sound in sounds.EnumerateArray())
        {
            if (Str(sound, "ipa") is not { } ipa || !ipa.StartsWith('/'))
            {
                continue;
            }

            string clean = LearnerIpa(ipa);
            if (clean.Length == 0 || clean.Length > Lexeme.MaxTranscriptionLength)
            {
                continue;
            }

            first ??= clean;
            if (sound.TryGetProperty("tags", out JsonElement tags) && tags.ValueKind == JsonValueKind.Array
                && tags.EnumerateArray().Any(t => t.GetString() is "Received-Pronunciation" or "UK"))
            {
                return clean;
            }
        }

        return first;
    }

    /// <summary>
    /// К виду учебных словарей (Oxford, Cambridge): ɹ → r, ɛ → e, без точек между слогами и знака слоговости (ˈtɛnʃn̩ → ˈtenʃn).
    /// </summary>
    public static string LearnerIpa(string ipa) =>
        ipa.Trim('/').Trim()
            .Replace('ɹ', 'r')
            .Replace('ɛ', 'e')
            .Replace(".", string.Empty)
            .Replace("̩", string.Empty) // знак слоговости под n, l
            .Replace("̍", string.Empty);

    private static List<string> Examples(JsonElement root)
    {
        var result = new List<string>();
        if (!root.TryGetProperty("senses", out JsonElement senses) || senses.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (JsonElement sense in senses.EnumerateArray())
        {
            if (!sense.TryGetProperty("examples", out JsonElement examples) || examples.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement example in examples.EnumerateArray())
            {
                // Только короткие примеры употребления, без цитат из книг (type = quotation).
                if (Str(example, "type") is "quotation" || Str(example, "text") is not { } text)
                {
                    continue;
                }

                // Настоящее предложение с заглавной буквы и точкой в конце, а не «to borrow the style of…».
                text = text.Trim();
                if (text.Length is >= 10 and <= 160 && char.IsUpper(text[0]) && text[^1] is '.' or '!' or '?' && !result.Contains(text))
                {
                    result.Add(text);
                }

                if (result.Count >= MaxExamples)
                {
                    return result;
                }
            }
        }

        return result;
    }

    private static string? LemmaOf(JsonElement root, string word)
    {
        if (!root.TryGetProperty("senses", out JsonElement senses) || senses.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement sense in senses.EnumerateArray())
        {
            foreach (string key in new[] { "form_of", "alt_of" })
            {
                if (sense.TryGetProperty(key, out JsonElement forms) && forms.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement form in forms.EnumerateArray())
                    {
                        if (Str(form, "word") is { Length: > 0 } lemma && !string.Equals(lemma, word, StringComparison.OrdinalIgnoreCase))
                        {
                            return lemma;
                        }
                    }
                }
            }
        }

        return null;
    }

    private static List<string> Categories(JsonElement root)
    {
        var result = new List<string>();
        void Collect(JsonElement container)
        {
            if (container.TryGetProperty("categories", out JsonElement list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement c in list.EnumerateArray())
                {
                    string? name = c.ValueKind == JsonValueKind.String ? c.GetString() : Str(c, "name");
                    if (name is not null)
                    {
                        result.Add(name);
                    }
                }
            }
        }

        Collect(root);
        if (root.TryGetProperty("senses", out JsonElement senses) && senses.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement sense in senses.EnumerateArray())
            {
                Collect(sense);
            }
        }

        return result;
    }

    private static string? Str(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
