using EnglishPlatform.Vocabulary.Domain;

namespace EnglishPlatform.Vocabulary.Application.Dictionary;

/// <summary>
/// Уровни CEFR по открытым спискам: строка «уровень;слово;часть речи», строки с # — комментарии.
/// Слово с несколькими частями речи может иметь разные уровни (light — прилагательное A1, глагол B1).
/// </summary>
public sealed class CefrLevelList : ICefrLevels
{
    /// <summary>Служебные слова не делают фразу трудной: «in», «the», «to» есть на A1.</summary>
    private static readonly HashSet<string> FunctionWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "to", "of", "in", "on", "at", "for", "with", "by", "from", "up", "down", "out", "off", "over",
        "into", "my", "your", "his", "her", "its", "our", "their", "someone", "something", "somebody", "one's", "oneself",
        "it", "i", "you", "he", "she", "we", "they", "be", "is", "are", "and", "or", "not", "no", "so", "as",
    };

    private readonly Dictionary<(string Word, PartOfSpeech? Pos), CefrLevel> _exact = [];
    private readonly Dictionary<string, CefrLevel> _lowest = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(CefrLevel Level, string Text, PartOfSpeech? PartOfSpeech)> _words = [];

    public CefrLevelList(IEnumerable<string> lines)
    {
        foreach (string line in lines)
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            string[] parts = line.Split(';');
            if (parts.Length < 3 || !Enum.TryParse(parts[0], out CefrLevel level))
            {
                continue;
            }

            string word = parts[1].Trim();
            PartOfSpeech? pos = Enum.TryParse(parts[2], out PartOfSpeech parsed) ? parsed : null;
            string key = word.ToLowerInvariant();
            _exact.TryAdd((key, pos), level);
            if (!_lowest.TryGetValue(key, out CefrLevel current) || level < current)
            {
                _lowest[key] = level;
            }

            _words.Add((level, word, pos));
        }
    }

    public IReadOnlyList<(CefrLevel Level, string Text, PartOfSpeech? PartOfSpeech)> Words => _words;

    public CefrLevel? Find(string text, PartOfSpeech? partOfSpeech, LexemeType type = LexemeType.Word)
    {
        string key = TextNormalizer.Normalize(text);
        if (_exact.TryGetValue((key, partOfSpeech), out CefrLevel exact))
        {
            return exact;
        }

        if (_lowest.TryGetValue(key, out CefrLevel any))
        {
            return any;
        }

        string[] tokens = key.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2)
        {
            return null;
        }

        if (type == LexemeType.PhrasalVerb)
        {
            CefrLevel verb = Level(tokens[0]) ?? CefrLevel.B1;
            return Max(verb, CefrLevel.B1);
        }

        List<CefrLevel> content = tokens.Where(t => !FunctionWords.Contains(t)).Select(Level).OfType<CefrLevel>().ToList();
        CefrLevel hardest = content.Count > 0 ? content.Max() : CefrLevel.B1;
        return type == LexemeType.Idiom ? Max(hardest, CefrLevel.B2) : hardest;
    }

    private CefrLevel? Level(string word) => _lowest.TryGetValue(word, out CefrLevel level) ? level : null;

    private static CefrLevel Max(CefrLevel a, CefrLevel b) => a > b ? a : b;
}
