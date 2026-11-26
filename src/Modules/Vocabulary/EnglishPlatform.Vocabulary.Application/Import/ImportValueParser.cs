using EnglishPlatform.Vocabulary.Domain;

namespace EnglishPlatform.Vocabulary.Application.Import;

/// <summary>Значения колонок импорта: принимает английские и русские названия, регистр не важен.</summary>
public static class ImportValueParser
{
    private static readonly Dictionary<string, LexemeType> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        ["word"] = LexemeType.Word, ["слово"] = LexemeType.Word,
        ["phrase"] = LexemeType.Phrase, ["фраза"] = LexemeType.Phrase, ["выражение"] = LexemeType.Phrase,
        ["idiom"] = LexemeType.Idiom, ["идиома"] = LexemeType.Idiom,
        ["phrasal verb"] = LexemeType.PhrasalVerb, ["phrasal_verb"] = LexemeType.PhrasalVerb, ["phrasalverb"] = LexemeType.PhrasalVerb,
        ["фразовый глагол"] = LexemeType.PhrasalVerb,
        ["устойчивое выражение"] = LexemeType.Phrase, ["устойчивое словосочетание"] = LexemeType.Phrase, ["устойчивое сочетание"] = LexemeType.Phrase,
        ["словосочетание"] = LexemeType.Phrase, ["коллокация"] = LexemeType.Phrase, ["collocation"] = LexemeType.Phrase,
        ["конструкция"] = LexemeType.Phrase, ["фиксированная конструкция"] = LexemeType.Phrase, ["грамматическая конструкция"] = LexemeType.Phrase, ["pattern"] = LexemeType.Phrase,
        ["expression"] = LexemeType.Phrase, ["set phrase"] = LexemeType.Phrase,
    };

    private static readonly Dictionary<string, PartOfSpeech> PartsOfSpeech = new(StringComparer.OrdinalIgnoreCase)
    {
        ["noun"] = PartOfSpeech.Noun, ["n"] = PartOfSpeech.Noun, ["существительное"] = PartOfSpeech.Noun, ["сущ"] = PartOfSpeech.Noun,
        ["verb"] = PartOfSpeech.Verb, ["v"] = PartOfSpeech.Verb, ["глагол"] = PartOfSpeech.Verb, ["гл"] = PartOfSpeech.Verb,
        ["adjective"] = PartOfSpeech.Adjective, ["adj"] = PartOfSpeech.Adjective, ["прилагательное"] = PartOfSpeech.Adjective, ["прил"] = PartOfSpeech.Adjective,
        ["adverb"] = PartOfSpeech.Adverb, ["adv"] = PartOfSpeech.Adverb, ["наречие"] = PartOfSpeech.Adverb, ["нар"] = PartOfSpeech.Adverb,
        ["pronoun"] = PartOfSpeech.Pronoun, ["pron"] = PartOfSpeech.Pronoun, ["местоимение"] = PartOfSpeech.Pronoun,
        ["preposition"] = PartOfSpeech.Preposition, ["prep"] = PartOfSpeech.Preposition, ["предлог"] = PartOfSpeech.Preposition,
        ["conjunction"] = PartOfSpeech.Conjunction, ["conj"] = PartOfSpeech.Conjunction, ["союз"] = PartOfSpeech.Conjunction,
        ["determiner"] = PartOfSpeech.Determiner, ["det"] = PartOfSpeech.Determiner, ["определитель"] = PartOfSpeech.Determiner,
        ["interjection"] = PartOfSpeech.Interjection, ["interj"] = PartOfSpeech.Interjection, ["междометие"] = PartOfSpeech.Interjection,
        ["numeral"] = PartOfSpeech.Numeral, ["num"] = PartOfSpeech.Numeral, ["числительное"] = PartOfSpeech.Numeral,
        ["причастие"] = PartOfSpeech.Adjective, ["participle"] = PartOfSpeech.Adjective,
    };

    /// <summary>
    /// Метка вроде «Фразовый глагол», «Устойчивое выражение», «Существительное/глагол», «Прилагательное + предлог»:
    /// из неё берутся тип и/или часть речи. Для составных меток берётся первая узнаваемая часть.
    /// </summary>
    public static bool TryParseLabel(string? value, out LexemeType? type, out PartOfSpeech? partOfSpeech)
    {
        type = null;
        partOfSpeech = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string label = TextNormalizer.Clean(value).TrimEnd('.');
        if (Types.TryGetValue(label, out LexemeType wholeType))
        {
            type = wholeType;
            partOfSpeech = wholeType == LexemeType.PhrasalVerb ? PartOfSpeech.Verb : null;
            return true;
        }

        foreach (string part in label.Split(['/', '+', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string key = part.TrimEnd('.');
            if (Types.TryGetValue(key, out LexemeType partType))
            {
                type = partType;
                partOfSpeech = partType == LexemeType.PhrasalVerb ? PartOfSpeech.Verb : null;
                return true;
            }

            if (PartsOfSpeech.TryGetValue(key, out PartOfSpeech pos))
            {
                partOfSpeech = pos;
                return true;
            }
        }

        return false;
    }

    /// <summary>Тип по колонке; если колонка пустая — слово, а для нескольких слов — фраза.</summary>
    public static bool TryParseType(string? value, string text, out LexemeType type)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            type = TextNormalizer.Clean(text).Contains(' ') ? LexemeType.Phrase : LexemeType.Word;
            return true;
        }

        return Types.TryGetValue(TextNormalizer.Clean(value), out type)
               || (Enum.TryParse(value.Trim(), ignoreCase: true, out type) && Enum.IsDefined(type));
    }

    public static bool TryParseLevel(string? value, out CefrLevel? level)
    {
        level = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (Enum.TryParse(value.Trim(), ignoreCase: true, out CefrLevel parsed) && Enum.IsDefined(parsed))
        {
            level = parsed;
            return true;
        }

        return false;
    }

    public static bool TryParsePartOfSpeech(string? value, out PartOfSpeech? partOfSpeech)
    {
        partOfSpeech = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        string key = TextNormalizer.Clean(value).TrimEnd('.');
        if (PartsOfSpeech.TryGetValue(key, out PartOfSpeech parsed)
            || (Enum.TryParse(key, ignoreCase: true, out parsed) && Enum.IsDefined(parsed)))
        {
            partOfSpeech = parsed;
            return true;
        }

        return false;
    }
}
