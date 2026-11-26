namespace EnglishPlatform.Vocabulary.Contracts;

/// <summary>Какие категории общей базы ближе к целям обучения из профиля (цели — имена LearningGoal строками).</summary>
public static class GoalCategories
{
    private static readonly Dictionary<string, string[]> Map = new()
    {
        ["Conversation"] = ["communication", "people", "everyday"],
        ["Travel"] = ["travel", "food", "shopping"],
        ["Work"] = ["work", "communication"],
        ["Exams"] = ["everyday", "communication", "time"],
        ["Reading"] = ["everyday", "emotions"],
        ["MoviesAndSeries"] = ["emotions", "people", "communication"],
        ["Writing"] = ["communication", "work"],
    };

    public static IReadOnlyList<string> For(IEnumerable<string> goals) =>
        goals.SelectMany(g => Map.GetValueOrDefault(g, [])).Distinct().ToList();

    /// <summary>Уровни слов для пополнения: свой и следующий (свои закрепить, следующие — расти).</summary>
    public static IReadOnlyList<string> WordLevels(string level)
    {
        string[] all = ["A1", "A2", "B1", "B2", "C1", "C2"];
        int index = Array.IndexOf(all, level);
        return index < 0 ? ["A2", "B1"] : index == all.Length - 1 ? ["C1", "C2"] : [all[index], all[index + 1]];
    }
}
