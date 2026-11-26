using EnglishPlatform.Vocabulary.Application.Dictionary;

namespace EnglishPlatform.Vocabulary.Infrastructure.Dictionary;

/// <summary>Встроенный список уровней (Seed/cefr-levels.csv: CEFR-J 1.5 и Octanove C1/C2, см. заголовок файла).</summary>
internal static class CefrLevelResource
{
    public static CefrLevelList Load()
    {
        using Stream stream = typeof(CefrLevelResource).Assembly.GetManifestResourceStream("Seed.cefr-levels.csv")
            ?? throw new InvalidOperationException("Не найден встроенный ресурс Seed.cefr-levels.csv");
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return new CefrLevelList(lines);
    }
}
