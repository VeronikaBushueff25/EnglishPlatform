using EnglishPlatform.Vocabulary.Domain;

namespace EnglishPlatform.Vocabulary.Application.Import;

/// <summary>Тип, часть речи и заметка после разбора. <c>Error</c> — значение колонки не распознано.</summary>
public sealed record Classification(LexemeType Type, PartOfSpeech? PartOfSpeech, string? Note, string? Error = null);

/// <summary>
/// Определяет тип и часть речи слова при импорте и переносе в общую базу.
/// Порядок: колонки «тип» и «часть речи» → метка в начале заметки («Глагол; deny doing») →
/// признаки фразового глагола (<c>bump into</c>, <c>get on</c>) → слово или фраза по количеству слов.
/// Уровень CEFR так не определить, его берут из колонки или из общей базы.
/// </summary>
public static class LexemeClassifier
{
    /// <summary>Частицы и предлоги, которыми заканчиваются фразовые глаголы.</summary>
    private static readonly HashSet<string> Particles = new(StringComparer.OrdinalIgnoreCase)
    {
        "up", "down", "out", "off", "on", "in", "into", "over", "away", "back", "through", "across", "along",
        "around", "round", "about", "by", "forward", "after", "together", "apart", "onto", "behind", "ahead",
    };

    /// <summary>Первые слова, с которых фразовый глагол не начинается: be afraid of, the end, to be honest.</summary>
    private static readonly HashSet<string> NotVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "be", "a", "an", "the", "to", "it", "it's", "this", "that", "my", "your", "his", "her", "our", "their",
        "of", "in", "on", "at", "for", "with", "from", "by", "not", "no", "very", "so", "too", "all", "just", "once",
    };

    public static Classification Classify(string text, string? typeCell, string? partOfSpeechCell, string? note)
    {
        LexemeType? type = null;
        PartOfSpeech? partOfSpeech = null;

        if (!string.IsNullOrWhiteSpace(typeCell))
        {
            if (!ImportValueParser.TryParseLabel(typeCell, out type, out partOfSpeech))
            {
                return new Classification(DefaultType(text), null, note, $"неизвестный тип «{typeCell.Trim()}»");
            }
        }

        if (!string.IsNullOrWhiteSpace(partOfSpeechCell))
        {
            if (!ImportValueParser.TryParsePartOfSpeech(partOfSpeechCell, out PartOfSpeech? explicitPos))
            {
                return new Classification(type ?? DefaultType(text), partOfSpeech, note, $"неизвестная часть речи «{partOfSpeechCell.Trim()}»");
            }

            partOfSpeech = explicitPos;
        }

        // Метка в начале заметки: «Глагол; deny something» → глагол, в заметке остаётся «deny something».
        string? cleanNote = TextNormalizer.NullIfEmpty(note);
        if ((type is null || partOfSpeech is null) && cleanNote is not null)
        {
            int separator = cleanNote.IndexOf(';');
            string head = separator < 0 ? cleanNote : cleanNote[..separator];
            if (head.Length <= 40 && ImportValueParser.TryParseLabel(head, out LexemeType? noteType, out PartOfSpeech? notePos))
            {
                type ??= noteType;
                partOfSpeech ??= notePos;
                cleanNote = separator < 0 ? null : TextNormalizer.NullIfEmpty(cleanNote[(separator + 1)..]);
            }
            else if (partOfSpeech is null && PartOfSpeechInPhrase(head) is { } described)
            {
                // «Наречие от inevitable», «Неисчисляемое существительное»: часть речи есть, но заметка содержательная — оставляем её.
                partOfSpeech = described;
            }
        }

        // «Предлог in» у interested in, «Предлог on» у depend on: это слово с предлогом, а не фразовый глагол.
        bool notePointsToPreposition = note?.TrimStart().StartsWith("предлог", StringComparison.OrdinalIgnoreCase) == true;
        if (type is null && !notePointsToPreposition && LooksLikePhrasalVerb(text))
        {
            type = LexemeType.PhrasalVerb;
        }

        LexemeType resolved = type ?? DefaultType(text);

        // Метка «Глагол» у фразы не делает её одним словом, а у фразового глагола часть речи — всегда глагол.
        if (resolved == LexemeType.PhrasalVerb)
        {
            partOfSpeech ??= PartOfSpeech.Verb;
        }
        else if (resolved == LexemeType.Word && TextNormalizer.Clean(text).Contains(' '))
        {
            resolved = LexemeType.Phrase;
        }

        return new Classification(resolved, partOfSpeech, cleanNote);
    }

    /// <summary>
    /// Часть речи внутри короткой фразы-пояснения: «Наречие от inevitable» (наречие в начале) или
    /// «Неисчисляемое существительное» (часть речи вторым словом). «Предлог on» сюда не подходит: это предлог
    /// при слове, а не часть речи самого слова.
    /// </summary>
    private static PartOfSpeech? PartOfSpeechInPhrase(string head)
    {
        string[] words = TextNormalizer.Clean(head).Split(' ');
        if (words.Length >= 2 && words[0].Equals("наречие", StringComparison.OrdinalIgnoreCase))
        {
            return PartOfSpeech.Adverb;
        }

        return words.Length == 2 && ImportValueParser.TryParsePartOfSpeech(words[1], out PartOfSpeech? second) ? second : null;
    }

    public static LexemeType DefaultType(string text) =>
        TextNormalizer.Clean(text).Contains(' ') ? LexemeType.Phrase : LexemeType.Word;

    /// <summary>Два-три слова латиницей, первое похоже на глагол, остальные — частицы: get on, run out of, put up with.</summary>
    public static bool LooksLikePhrasalVerb(string text)
    {
        string[] words = TextNormalizer.Clean(text).Split(' ');
        if (words.Length is < 2 or > 4 || NotVerbs.Contains(words[0]))
        {
            return false;
        }

        if (!words.All(w => w.Length > 0 && w.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z')))
        {
            return false;
        }

        // Хвост: частица, затем по желанию ещё частица или of/with/to (run out of, put up with, look forward to).
        if (!Particles.Contains(words[1]))
        {
            return false;
        }

        return words.Skip(2).All(w => Particles.Contains(w) || w is "of" or "with" or "to" or "for");
    }
}
