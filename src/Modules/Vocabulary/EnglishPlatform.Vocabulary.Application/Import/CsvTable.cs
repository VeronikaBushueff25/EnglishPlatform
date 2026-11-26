using System.Text;

namespace EnglishPlatform.Vocabulary.Application.Import;

/// <summary>Строка CSV с номером строки файла (для сообщений об ошибках) и значениями по каноническим именам колонок.</summary>
public sealed record CsvRow(int Line, IReadOnlyDictionary<string, string> Values)
{
    public string? Get(string column) =>
        Values.TryGetValue(column, out string? value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}

/// <summary>
/// Разбор CSV с заголовком. Разделитель (<c>;</c>, <c>,</c> или табуляция) определяется по первой строке:
/// Excel в русской локали сохраняет через точку с запятой, Google Таблицы — через запятую.
/// Поддерживаются значения в кавычках с переводами строк и удвоенными кавычками внутри.
/// </summary>
public static class CsvTable
{
    /// <summary>Канонические имена колонок и их синонимы в заголовке файла.</summary>
    private static readonly Dictionary<string, string> HeaderAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["text"] = Columns.Text, ["word"] = Columns.Text, ["english"] = Columns.Text, ["слово"] = Columns.Text, ["английский"] = Columns.Text,
        ["translation"] = Columns.Translation, ["translations"] = Columns.Translation, ["russian"] = Columns.Translation,
        ["перевод"] = Columns.Translation, ["переводы"] = Columns.Translation,
        ["type"] = Columns.Type, ["тип"] = Columns.Type,
        ["transcription"] = Columns.Transcription, ["ipa"] = Columns.Transcription, ["транскрипция"] = Columns.Transcription,
        ["example"] = Columns.Example, ["пример"] = Columns.Example,
        ["example_translation"] = Columns.ExampleTranslation, ["example translation"] = Columns.ExampleTranslation,
        ["перевод примера"] = Columns.ExampleTranslation,
        ["level"] = Columns.Level, ["cefr"] = Columns.Level, ["уровень"] = Columns.Level,
        ["part_of_speech"] = Columns.PartOfSpeech, ["part of speech"] = Columns.PartOfSpeech, ["pos"] = Columns.PartOfSpeech,
        ["часть речи"] = Columns.PartOfSpeech,
        ["categories"] = Columns.Categories, ["category"] = Columns.Categories, ["категории"] = Columns.Categories, ["категория"] = Columns.Categories,
        ["note"] = Columns.Note, ["заметка"] = Columns.Note,
        ["audio"] = Columns.AudioUrl, ["audio_url"] = Columns.AudioUrl, ["аудио"] = Columns.AudioUrl,
    };

    public static class Columns
    {
        public const string Text = "text";
        public const string Translation = "translation";
        public const string Type = "type";
        public const string Transcription = "transcription";
        public const string Example = "example";
        public const string ExampleTranslation = "example_translation";
        public const string Level = "level";
        public const string PartOfSpeech = "part_of_speech";
        public const string Categories = "categories";
        public const string Note = "note";
        public const string AudioUrl = "audio_url";
    }

    /// <summary>Разделитель нескольких значений в одной ячейке: «дом | жилище».</summary>
    public const char MultiValueSeparator = '|';

    public static IReadOnlyList<string> SplitValues(string? cell) =>
        string.IsNullOrWhiteSpace(cell)
            ? []
            : cell.Split(MultiValueSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Читает таблицу. Неизвестные колонки игнорируются. Если нет колонки со словом, возвращает ошибку вместо строк.
    /// </summary>
    public static (IReadOnlyList<CsvRow> Rows, string? Error) Parse(string content, int maxRows)
    {
        content = content.TrimStart('﻿');
        char delimiter = DetectDelimiter(content);
        List<(int Line, List<string> Cells)> records = ReadRecords(content, delimiter);

        if (records.Count == 0)
        {
            return ([], "Файл пуст");
        }

        string?[] columns = records[0].Cells
            .Select(h => HeaderAliases.TryGetValue(h.Trim(), out string? canonical) ? canonical : null)
            .ToArray();

        if (!columns.Contains(Columns.Text))
        {
            return ([], "В первой строке нужен заголовок с колонкой text (или «слово»), например: text;translation;example");
        }

        if (records.Count - 1 > maxRows)
        {
            return ([], $"Слишком много строк: {records.Count - 1}. За один раз можно загрузить до {maxRows}");
        }

        var rows = new List<CsvRow>(records.Count - 1);
        foreach ((int line, List<string> cells) in records.Skip(1))
        {
            if (cells.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var values = new Dictionary<string, string>();
            for (int i = 0; i < columns.Length && i < cells.Count; i++)
            {
                if (columns[i] is { } column && !values.ContainsKey(column))
                {
                    values[column] = cells[i];
                }
            }

            rows.Add(new CsvRow(line, values));
        }

        return (rows, null);
    }

    private static char DetectDelimiter(string content)
    {
        int end = content.IndexOfAny(['\r', '\n']);
        string header = end < 0 ? content : content[..end];
        char[] candidates = [';', '\t', ','];
        return candidates.MaxBy(c => header.Count(h => h == c));
    }

    private static List<(int Line, List<string> Cells)> ReadRecords(string content, char delimiter)
    {
        var records = new List<(int, List<string>)>();
        var cells = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false;
        int line = 1;
        int recordLine = 1;

        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];

            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    if (c == '\n')
                    {
                        line++;
                    }

                    cell.Append(c);
                }

                continue;
            }

            if (c == '"' && cell.Length == 0)
            {
                quoted = true;
            }
            else if (c == delimiter)
            {
                cells.Add(cell.ToString());
                cell.Clear();
            }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                {
                    i++;
                }

                cells.Add(cell.ToString());
                cell.Clear();
                records.Add((recordLine, cells));
                cells = [];
                line++;
                recordLine = line;
            }
            else
            {
                cell.Append(c);
            }
        }

        if (cell.Length > 0 || cells.Count > 0)
        {
            cells.Add(cell.ToString());
            records.Add((recordLine, cells));
        }

        return records;
    }
}
