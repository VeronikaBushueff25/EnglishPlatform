using EnglishPlatform.BuildingBlocks.Results;

namespace EnglishPlatform.Vocabulary.Application.Import;

/// <summary>Замечания по строкам импорта. Хранит первые <see cref="ImportLimits.MaxIssues"/>, остальные только считает.</summary>
internal sealed class ImportIssues
{
    private readonly List<ImportIssue> _issues = [];
    private int _hidden;

    public void Add(int line, string message)
    {
        if (_issues.Count < ImportLimits.MaxIssues)
        {
            _issues.Add(new ImportIssue(line, message));
        }
        else
        {
            _hidden++;
        }
    }

    public IReadOnlyList<ImportIssue> ToList() =>
        _hidden == 0 ? _issues : [.. _issues, new ImportIssue(0, $"И ещё замечаний: {_hidden}")];
}

public static class ImportErrors
{
    public static Error InvalidFile(string message) =>
        Error.Validation("vocabulary.import_invalid_file", message, new Dictionary<string, string[]>
        {
            ["content"] = [message],
        });
}
