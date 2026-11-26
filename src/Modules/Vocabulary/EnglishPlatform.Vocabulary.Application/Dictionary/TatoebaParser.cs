using System.Text.Json;

namespace EnglishPlatform.Vocabulary.Application.Dictionary;

/// <summary>Пример употребления с переводом: «The decision was put off.» — «Решение было отложено.»</summary>
public sealed record TranslatedExample(string Text, string Translation);

/// <summary>Примеры с переводом (Tatoeba, CC BY 2.0 FR). Null — подходящего примера нет или источник недоступен.</summary>
public interface IExampleSource
{
    Task<TranslatedExample?> FindAsync(string text, CancellationToken cancellationToken);
}

/// <summary>
/// Выбор примера из ответа Tatoeba (api_v0/search): с русским переводом, разумной длины, со словом в тексте.
/// Tatoeba полна предложений про Тома и Мэри — такие берём, только если других нет.
/// </summary>
public static class TatoebaParser
{
    private static readonly string[] StockNames = ["Tom", "Mary", "John", "Ken"];

    public static TranslatedExample? Pick(string json, string text)
    {
        JsonElement root;
        try
        {
            root = JsonDocument.Parse(json).RootElement;
        }
        catch (JsonException)
        {
            return null;
        }

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("results", out JsonElement results) || results.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        string needle = text.Trim().ToLowerInvariant();
        var candidates = new List<(TranslatedExample Example, int Score)>();
        foreach (JsonElement result in results.EnumerateArray())
        {
            if (Str(result, "text") is not { } english || Russian(result) is not { } russian)
            {
                continue;
            }

            english = english.Trim();
            if (english.Length is < 12 or > 110 || !english.Contains(needle.Split(' ')[0], StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Чем ближе к 30–70 символам и без «Тома», тем лучше.
            int score = english.Length is >= 25 and <= 70 ? 0 : 2;
            if (StockNames.Any(n => english.Contains(n + " ", StringComparison.Ordinal) || english.Contains(n + "'", StringComparison.Ordinal)))
            {
                score += 3;
            }

            candidates.Add((new TranslatedExample(english, russian.Trim()), score));
        }

        // Порядок Tatoeba (по релевантности) сохраняется среди равных: OrderBy стабилен.
        return candidates.OrderBy(c => c.Score).Select(c => c.Example).FirstOrDefault();
    }

    private static string? Russian(JsonElement result)
    {
        if (!result.TryGetProperty("translations", out JsonElement groups) || groups.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement group in groups.EnumerateArray())
        {
            if (group.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement translation in group.EnumerateArray())
            {
                if (Str(translation, "lang") == "rus" && Str(translation, "text") is { Length: > 0 } ru)
                {
                    return ru;
                }
            }
        }

        return null;
    }

    private static string? Str(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
