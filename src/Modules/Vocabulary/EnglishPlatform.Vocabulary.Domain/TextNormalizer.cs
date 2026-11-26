using System.Text;

namespace EnglishPlatform.Vocabulary.Domain;

/// <summary>Приведение пользовательского текста к единому виду.</summary>
public static class TextNormalizer
{
    /// <summary>Обрезает края и схлопывает пробелы внутри.</summary>
    public static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        bool space = false;
        foreach (char c in text.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                space = true;
                continue;
            }

            if (space)
            {
                builder.Append(' ');
                space = false;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>Ключ для поиска и проверки дублей: <see cref="Clean"/>, нижний регистр, ё → е, типографский апостроф заменён обычным.</summary>
    public static string Normalize(string? text) =>
        Clean(text).Replace('’', '\'').Replace('‘', '\'').ToLowerInvariant().Replace('ё', 'е');

    /// <summary>Строка для поиска по подстроке: текст и переводы через перевод строки, в нижнем регистре.</summary>
    public static string SearchKey(string text, IEnumerable<string> translations) =>
        string.Join('\n', translations.Prepend(text).Select(Normalize));

    public static string? NullIfEmpty(string? text)
    {
        string cleaned = Clean(text);
        return cleaned.Length == 0 ? null : cleaned;
    }

    /// <summary>Транскрипция без обрамляющих / / и [ ].</summary>
    public static string? CleanTranscription(string? transcription) =>
        NullIfEmpty(Clean(transcription).Trim('/', '[', ']', ' '));
}
