using System.Text.RegularExpressions;
using EnglishPlatform.Exercises.Contracts;

namespace EnglishPlatform.Exercises.Application;

/// <summary>
/// Строит задание по слову. Узнавание: выбор перевода (иногда на слух). Вспоминание: пропуск в предложении из примера,
/// если слово в примере найдено, иначе ввод слова по переводу.
/// </summary>
public sealed partial class ExerciseFactory(Random random)
{
    public const int Options = 4;
    public const string Gap = "_____";
    private const int MaxListenLength = 40;

    private static readonly HashSet<string> Placeholders =
        ["sb", "sth", "someone", "somebody", "something", "smb", "smth", "one's", "oneself", "doing", "do"];

    private static readonly Dictionary<string, string> TypeLabels = new()
    {
        ["Word"] = "слово",
        ["Phrase"] = "фраза",
        ["Idiom"] = "идиома",
        ["PhrasalVerb"] = "фразовый глагол",
    };

    public ExerciseTask Introduction(ExerciseWord word) =>
        new(Guid.CreateVersion7(), ExerciseKind.Introduce, word, word.Text, Note(word), [], null, [word.Text], null, null, null);

    public ExerciseTask Build(ExerciseWord word, ExerciseMode mode, IReadOnlyList<string> distractors) =>
        Build(word, mode, distractors, ExercisePreferences.Default);

    public ExerciseTask Build(ExerciseWord word, ExerciseMode mode, IReadOnlyList<string> distractors, ExercisePreferences preferences)
    {
        if (mode == ExerciseMode.Recognition)
        {
            bool listen = word.Text.Length <= MaxListenLength && random.NextDouble() < preferences.ListenShare;
            return Choice(word, distractors, listen ? ExerciseKind.ListenChoose : ExerciseKind.ChooseTranslation);
        }

        return random.NextDouble() < preferences.GapShare && FillGap(word) is { } gap ? gap : TypeWord(word);
    }

    /// <summary>Перевод → выбрать английское слово. Легче ввода: слово нужно узнать среди похожих, а не вспомнить целиком.</summary>
    public ExerciseTask ChooseWord(ExerciseWord word, IReadOnlyList<string> englishDistractors)
    {
        var options = englishDistractors.Where(d => !string.IsNullOrWhiteSpace(d) && !d.Equals(word.Text, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Options - 1)
            .Append(word.Text)
            .OrderBy(_ => random.Next())
            .ToList();

        return new ExerciseTask(
            Guid.CreateVersion7(),
            ExerciseKind.ChooseWord,
            word,
            string.Join(", ", word.Translations.Take(3)),
            Note(word),
            options,
            options.IndexOf(word.Text),
            [word.Text],
            null,
            null,
            null);
    }

    private ExerciseTask Choice(ExerciseWord word, IReadOnlyList<string> distractors, ExerciseKind kind)
    {
        string correct = Meaning(word);
        var options = distractors.Where(d => !string.IsNullOrWhiteSpace(d)).Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Options - 1)
            .Append(correct)
            .OrderBy(_ => random.Next())
            .ToList();

        string prompt = kind == ExerciseKind.ListenChoose ? "Что значит это слово?" : word.Text;
        return new ExerciseTask(Guid.CreateVersion7(), kind, word, prompt, Note(word), options, options.IndexOf(correct), [correct], null, null, null);
    }

    private static ExerciseTask TypeWord(ExerciseWord word) =>
        new(
            Guid.CreateVersion7(),
            ExerciseKind.TypeWord,
            word,
            string.Join(", ", word.Translations.Take(3)),
            Note(word),
            [],
            null,
            [word.Text],
            null,
            null,
            Hint(word.Text));

    /// <summary>Предложение из примера с пропуском. null — слово в примере не нашлось (например, there's sth between).</summary>
    public static ExerciseTask? FillGap(ExerciseWord word)
    {
        if (string.IsNullOrWhiteSpace(word.Example) || FindInSentence(word.Text, word.Example) is not { } match)
        {
            return null;
        }

        string sentence = word.Example[..match.Index] + Gap + word.Example[(match.Index + match.Length)..];
        string answer = word.Example.Substring(match.Index, match.Length);
        return new ExerciseTask(
            Guid.CreateVersion7(),
            ExerciseKind.FillGap,
            word,
            string.Join(", ", word.Translations.Take(3)),
            Note(word),
            [],
            null,
            [answer],
            sentence,
            word.ExampleTranslation,
            Hint(answer));
    }

    /// <summary>
    /// Ищет слово или фразу в предложении: первое слово может стоять в другой форме (bump into → bumped into),
    /// служебные «something/someone/doing» и начальное «to» пропускаются.
    /// </summary>
    public static (int Index, int Length)? FindInSentence(string text, string sentence)
    {
        List<string> target = text.ToLowerInvariant().Replace('’', '\'')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim('.', ',', '!', '?'))
            .Where(t => t.Length > 0)
            .SkipWhile(t => t == "to")
            .ToList();

        if (target.Count == 0 || target.Any(Placeholders.Contains) || target.Any(t => !t.All(c => char.IsLetter(c) || c is '\'' or '-')))
        {
            // Конструкции с местом под объект (take sth into account) и шаблоны (would + V1) в пропуск не превращаем.
            return null;
        }

        List<Match> tokens = WordRegex().Matches(sentence).ToList();
        for (int start = 0; start + target.Count <= tokens.Count; start++)
        {
            bool matches = true;
            for (int k = 0; k < target.Count && matches; k++)
            {
                string token = tokens[start + k].Value.ToLowerInvariant().Replace('’', '\'');
                matches = k == 0 ? token == target[0] || AnswerChecker.IsOtherForm(token, target[0]) : token == target[k];
            }

            if (matches)
            {
                Match first = tokens[start];
                Match last = tokens[start + target.Count - 1];
                return (first.Index, last.Index + last.Length - first.Index);
            }
        }

        return null;
    }

    /// <summary>Перевод для варианта ответа: первые два значения через запятую.</summary>
    public static string Meaning(ExerciseWord word) => string.Join(", ", word.Translations.Take(2));

    private static string? Note(ExerciseWord word)
    {
        string? type = TypeLabels.GetValueOrDefault(word.Type);
        return word.Type == "Word" ? null : type;
    }

    /// <summary>Подсказка: первая буква и длина, пробелы видны — «s________ (11)», «b___ i___».</summary>
    private static string Hint(string answer) =>
        string.Join(' ', answer.Split(' ').Select(w => w.Length <= 1 ? w : w[0] + new string('_', w.Length - 1)));

    [GeneratedRegex(@"[\p{L}']+(?:-[\p{L}']+)*")]
    private static partial Regex WordRegex();
}
