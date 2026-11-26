namespace EnglishPlatform.Analytics.Contracts;

/// <summary>Сколько ответов дано в формате и какая доля из них неудачная.</summary>
public sealed record FormatStat(string Kind, int Attempts, double ErrorRate);

/// <summary>
/// Слабые места пользователя для сборки занятия: доли типов ошибок среди всех ответов, ошибки по форматам
/// и проблемные слова (часто ошибается в последнее время), самые проблемные первыми.
/// </summary>
public sealed record WeaknessProfile(
    int TotalAnswers,
    IReadOnlyDictionary<string, double> TagShares,
    IReadOnlyList<FormatStat> Formats,
    IReadOnlyList<Guid> ProblemWordIds)
{
    public static readonly WeaknessProfile Empty = new(0, new Dictionary<string, double>(), [], []);

    public double TagShare(string tag) => TagShares.GetValueOrDefault(tag);

    public FormatStat? Format(string kind) => Formats.FirstOrDefault(f => f.Kind == kind);
}

public interface IWeaknessReader
{
    Task<WeaknessProfile> GetAsync(Guid userId, CancellationToken cancellationToken);
}
