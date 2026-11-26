namespace EnglishPlatform.LearningProfiles.Contracts;

/// <summary>Уровень по навыку строками: Skill — Vocabulary/Grammar/Reading, Level — A1…C2.</summary>
public sealed record SkillLevelInfo(string Skill, string Level, DateTimeOffset AssessedAt);

public interface ISkillLevelsReader
{
    /// <summary>Уровни пользователя по навыкам в порядке «лексика, грамматика, чтение»; пусто, если тест не пройден.</summary>
    Task<IReadOnlyList<SkillLevelInfo>> GetAsync(Guid userId, CancellationToken cancellationToken);
}
