using System.Text;
using EnglishPlatform.Exercises.Contracts;

namespace EnglishPlatform.Exercises.Application;

/// <summary>
/// Проверка ответа без ИИ: нормализация строки, набор допустимых вариантов, опечатки по расстоянию Левенштейна,
/// отдельно — пропущенный или лишний артикль и другая форма того же слова. Объяснение — по шаблону.
/// </summary>
public static class AnswerChecker
{
    private static readonly string[] Articles = ["a", "an", "the"];
    private static readonly string[] Suffixes = ["s", "es", "ed", "d", "ing", "er", "est", "ly", "ies", "ied"];

    private static readonly HashSet<string> Prepositions =
    [
        "on", "in", "at", "of", "for", "to", "with", "about", "from", "by", "into", "onto", "over", "under", "up", "out",
        "off", "after", "upon", "against", "towards", "between", "among", "through", "across",
    ];

    /// <summary>Фраза совпадает, кроме одного предлога: «depend of» вместо «depend on», «interested at» вместо «interested in».</summary>
    public static bool DiffersOnlyInPreposition(string given, string accepted)
    {
        string[] a = StripTo(given).Split(' ');
        string[] b = StripTo(accepted).Split(' ');
        if (a.Length != b.Length || a.Length < 2)
        {
            return false;
        }

        int differences = 0;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] == b[i])
            {
                continue;
            }

            if (!Prepositions.Contains(a[i]) || !Prepositions.Contains(b[i]))
            {
                return false;
            }

            differences++;
        }

        return differences == 1;
    }

    public static CheckResult Check(ExerciseTask task, string? answer)
    {
        return task.Kind switch
        {
            ExerciseKind.ChooseTranslation or ExerciseKind.ListenChoose or ExerciseKind.ChooseWord => CheckChoice(task, answer),
            ExerciseKind.TypeWord or ExerciseKind.FillGap => CheckTyped(task, answer),
            _ => new CheckResult(AnswerVerdict.Correct, ErrorTag.None, task.Word.Text, "Слово добавлено в занятие."),
        };
    }

    private static CheckResult CheckChoice(ExerciseTask task, string? answer)
    {
        string correct = task.CorrectOption is { } index && index < task.Options.Count ? task.Options[index] : task.AcceptedAnswers.FirstOrDefault() ?? "";
        int? chosen = int.TryParse(answer, out int number) && number >= 0 && number < task.Options.Count
            ? number
            : FindOption(task.Options, answer);

        string meaning = string.Join(", ", task.Word.Translations.Take(2));
        if (chosen is null)
        {
            return task.Kind == ExerciseKind.ChooseWord
                ? new CheckResult(AnswerVerdict.Wrong, ErrorTag.Empty, correct, $"Нет ответа. «{meaning}» — это «{correct}».")
                : new CheckResult(AnswerVerdict.Wrong, ErrorTag.Empty, correct, $"Нет ответа. «{task.Word.Text}» — это «{correct}».");
        }

        if (chosen == task.CorrectOption)
        {
            return new CheckResult(AnswerVerdict.Correct, ErrorTag.None, correct, "Верно!");
        }

        return task.Kind == ExerciseKind.ChooseWord
            ? new CheckResult(AnswerVerdict.Wrong, ErrorTag.WrongWord, correct, $"Неверно. «{meaning}» — это «{correct}», а не «{task.Options[chosen.Value]}».")
            : new CheckResult(AnswerVerdict.Wrong, ErrorTag.WrongWord, correct, $"Неверно. «{task.Word.Text}» — это «{correct}», а «{task.Options[chosen.Value]}» — другое слово.");
    }

    private static int? FindOption(IReadOnlyList<string> options, string? answer)
    {
        string key = Normalize(answer);
        for (int i = 0; i < options.Count; i++)
        {
            if (key.Length > 0 && Normalize(options[i]) == key)
            {
                return i;
            }
        }

        return null;
    }

    private static CheckResult CheckTyped(ExerciseTask task, string? answer)
    {
        string correct = task.AcceptedAnswers.FirstOrDefault() ?? task.Word.Text;
        string given = Normalize(answer);
        string meaning = task.Kind == ExerciseKind.FillGap ? "" : $" ({string.Join(", ", task.Word.Translations.Take(2))})";

        if (given.Length == 0)
        {
            return new CheckResult(AnswerVerdict.Wrong, ErrorTag.Empty, correct, $"Нет ответа. Правильно — «{correct}»{meaning}.");
        }

        List<string> accepted = task.AcceptedAnswers.Select(Normalize).Where(a => a.Length > 0).Distinct().ToList();
        if (accepted.Any(a => Same(given, a)))
        {
            return new CheckResult(AnswerVerdict.Correct, ErrorTag.None, correct, "Верно!");
        }

        if (accepted.Any(a => Same(StripArticle(given), StripArticle(a))))
        {
            return new CheckResult(AnswerVerdict.Almost, ErrorTag.Article, correct, $"Почти: пропущен или лишний артикль. Правильно — «{correct}».");
        }

        if (accepted.Any(a => DiffersOnlyInPreposition(given, a)))
        {
            return new CheckResult(AnswerVerdict.Almost, ErrorTag.Preposition, correct, $"Почти: не тот предлог. Правильно — «{correct}».");
        }

        if (accepted.Any(a => IsTypo(given, a)))
        {
            return new CheckResult(AnswerVerdict.Almost, ErrorTag.Typo, correct, $"Почти: опечатка. Правильно — «{correct}».");
        }

        // В задании с пропуском нужна именно форма из предложения; начальная форма — «почти».
        string wordText = Normalize(task.Word.Text);
        if (accepted.Any(a => IsOtherForm(given, a)) || (task.Kind == ExerciseKind.FillGap && Same(given, wordText)))
        {
            string where = task.Kind == ExerciseKind.FillGap ? "В этом предложении нужна форма" : "Правильно";
            return new CheckResult(AnswerVerdict.Almost, ErrorTag.WordForm, correct, $"Почти: не та форма слова. {where} «{correct}».");
        }

        return new CheckResult(AnswerVerdict.Wrong, ErrorTag.WrongWord, correct, $"Неверно. Правильно — «{correct}»{meaning}.");
    }

    /// <summary>
    /// Нижний регистр, схлопнутые пробелы, обычный апостроф, без точки в конце; someone/somebody/sb и something/sth
    /// считаются одним и тем же; «to» перед глаголом не обязателен.
    /// </summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (string token in text.Trim().ToLowerInvariant().Replace('’', '\'').Replace('‘', '\'').Replace('ё', 'е')
                     .Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string word = token.Trim('.', '!', '?', ',', ';', ':', '"', '«', '»');
            if (word.Length == 0)
            {
                continue;
            }

            word = word switch
            {
                "someone" or "somebody" or "smb" or "sb" or "smb." or "sb." => "sb",
                "something" or "smth" or "sth" or "smth." or "sth." => "sth",
                "one's" or "your" or "his" or "her" or "my" when builder.Length > 0 => "one's",
                _ => word,
            };

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(word);
        }

        return builder.ToString();
    }

    private static bool Same(string given, string accepted) =>
        given == accepted || StripTo(given) == StripTo(accepted);

    private static string StripTo(string text) => text.StartsWith("to ", StringComparison.Ordinal) ? text[3..] : text;

    private static string StripArticle(string text)
    {
        string stripped = StripTo(text);
        foreach (string article in Articles)
        {
            if (stripped.StartsWith(article + " ", StringComparison.Ordinal))
            {
                return stripped[(article.Length + 1)..];
            }
        }

        return stripped;
    }

    /// <summary>Опечатка: одна ошибка в словах из 4–8 букв, две — в более длинных; первая буква должна совпасть.</summary>
    public static bool IsTypo(string given, string accepted)
    {
        string a = StripTo(given);
        string b = StripTo(accepted);
        // В коротких словах одна буква меняет само слово (cat/cut), поэтому опечатки там не считаем.
        if (b.Length <= 3 || a.Length == 0 || a[0] != b[0])
        {
            return false;
        }

        int allowed = b.Length <= 8 ? 1 : 2;
        return Math.Abs(a.Length - b.Length) <= allowed && Levenshtein(a, b) <= allowed;
    }

    /// <summary>Другая форма того же слова: deny/denied, apply/applies, take off/took off не считается (это не суффикс).</summary>
    public static bool IsOtherForm(string given, string accepted)
    {
        string a = StripTo(given);
        string b = StripTo(accepted);
        if (a == b || a.Split(' ').Length != b.Split(' ').Length)
        {
            return false;
        }

        // Сравниваем по словам: формой может отличаться одно слово фразы (look forward to seeing / to see).
        string[] wordsA = a.Split(' ');
        string[] wordsB = b.Split(' ');
        int differences = 0;
        for (int i = 0; i < wordsA.Length; i++)
        {
            if (wordsA[i] == wordsB[i])
            {
                continue;
            }

            if (!SameStem(wordsA[i], wordsB[i]))
            {
                return false;
            }

            differences++;
        }

        return differences > 0;
    }

    private static bool SameStem(string a, string b)
    {
        string stemA = Stem(a);
        string stemB = Stem(b);

        // Слово само может оканчиваться «суффиксом» (apply, early), поэтому сравниваем и основу с целым словом.
        return stemA.Length >= 3
               && (stemA == stemB || stemA == b || stemB == a
                   || (Math.Min(stemA.Length, stemB.Length) >= 4 && Levenshtein(stemA, stemB) <= 1));
    }

    private static string Stem(string word)
    {
        foreach (string suffix in Suffixes.OrderByDescending(s => s.Length))
        {
            if (word.Length - suffix.Length >= 3 && word.EndsWith(suffix, StringComparison.Ordinal))
            {
                string stem = word[..^suffix.Length];
                return suffix is "ies" or "ied" ? stem + "y" : stem;
            }
        }

        return word.EndsWith('e') && word.Length > 3 ? word[..^1] : word;
    }

    public static int Levenshtein(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
