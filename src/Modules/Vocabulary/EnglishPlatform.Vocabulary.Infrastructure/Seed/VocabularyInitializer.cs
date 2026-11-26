using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
using EnglishPlatform.BuildingBlocks.Results;
using EnglishPlatform.Vocabulary.Application;
using EnglishPlatform.Vocabulary.Domain;
using EnglishPlatform.Vocabulary.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnglishPlatform.Vocabulary.Infrastructure.Seed;

/// <summary>
/// Стартовое наполнение общей базы: категории и около 150 слов уровней A1–B2 (Seed/starter-lexemes.csv).
/// Набор составлен вручную для проекта, поэтому вопросов лицензии нет. Загружается только в пустую базу,
/// дальше контентом управляет администратор.
/// </summary>
internal sealed class VocabularyInitializer(
    VocabularyDb db,
    VocabularyAdminService admin,
    ILogger<VocabularyInitializer> logger) : IModuleInitializer
{
    private static readonly (string Slug, string Name, string Description)[] StarterCategories =
    [
        ("everyday", "Повседневное", "Самые частые слова на каждый день"),
        ("food", "Еда", "Продукты, блюда, кафе и рестораны"),
        ("travel", "Путешествия", "Дорога, транспорт, отели"),
        ("work", "Работа", "Офис, карьера, деловое общение"),
        ("home", "Дом", "Жильё, быт, мебель"),
        ("people", "Люди и отношения", "Семья, друзья, характер"),
        ("emotions", "Чувства", "Эмоции и состояния"),
        ("time", "Время", "Дни, расписание, частота"),
        ("nature", "Природа и погода", "Погода, природа, экология"),
        ("health", "Здоровье", "Самочувствие, врачи, спорт"),
        ("shopping", "Покупки", "Магазины, цены, деньги"),
        ("communication", "Общение", "Разговорные фразы и связки"),
    ];

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (await db.Lexemes.AnyAsync(cancellationToken))
        {
            return;
        }

        HashSet<string> existing = (await db.Categories.Select(c => c.Slug).ToListAsync(cancellationToken)).ToHashSet();
        foreach ((string slug, string name, string description) in StarterCategories.Where(c => !existing.Contains(c.Slug)))
        {
            db.Categories.Add(Category.Create(name, slug, description));
        }

        await db.SaveChangesAsync(cancellationToken);

        await using Stream stream = typeof(VocabularyInitializer).Assembly.GetManifestResourceStream("Seed.starter-lexemes.csv")
            ?? throw new InvalidOperationException("Не найден встроенный ресурс Seed.starter-lexemes.csv");
        using var reader = new StreamReader(stream);
        string content = await reader.ReadToEndAsync(cancellationToken);

        Result<ImportResult> result = await admin.ImportLexemesAsync(new ImportCsvRequest(content), cancellationToken);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Стартовый набор слов не загрузился: {result.Error.Message}");
        }

        foreach (ImportIssue issue in result.Value.Issues)
        {
            logger.LogWarning("Стартовый набор слов, строка {Line}: {Message}", issue.Line, issue.Message);
        }

        logger.LogInformation("Загружен стартовый набор слов: {Count}", result.Value.Added);
    }
}
