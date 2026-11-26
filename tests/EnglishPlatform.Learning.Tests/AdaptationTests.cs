using EnglishPlatform.Analytics.Contracts;
using EnglishPlatform.Exercises.Application;
using EnglishPlatform.Exercises.Contracts;
using EnglishPlatform.LearningSessions.Application;
using EnglishPlatform.LearningSessions.Domain;
using Shouldly;

namespace EnglishPlatform.Learning.Tests;

/// <summary>Этап 5: новые типы ошибок, разминка для проблемных слов, форматы по слабым местам.</summary>
public sealed class AdaptationTests
{
    private static ExerciseWord Word(string text, params string[] translations) => new(
        Guid.NewGuid(), text, "Phrase", null, "B1", null, translations.Length > 0 ? translations : ["перевод"], null, null, null);

    private static ExerciseTask Typed(string text) =>
        new(Guid.NewGuid(), ExerciseKind.TypeWord, Word(text), "перевод", null, [], null, [text], null, null, null);

    [Theory]
    [InlineData("depend on", "depend of", true)]
    [InlineData("interested in", "interested at", true)]
    [InlineData("look forward to", "look forward for", true)]
    [InlineData("depend on", "rely on", false)]
    [InlineData("deal with", "deal", false)]
    [InlineData("put up with", "put out of", false)]
    public void Detects_wrong_preposition(string expected, string answer, bool isPreposition) =>
        AnswerChecker.DiffersOnlyInPreposition(AnswerChecker.Normalize(answer), AnswerChecker.Normalize(expected)).ShouldBe(isPreposition);

    [Fact]
    public void Wrong_preposition_is_almost_with_its_own_tag_not_a_typo()
    {
        CheckResult result = AnswerChecker.Check(Typed("depend on"), "depend of");

        result.Verdict.ShouldBe(AnswerVerdict.Almost);
        result.ErrorTag.ShouldBe(ErrorTag.Preposition);
        result.Explanation.ShouldContain("предлог");
    }

    [Fact]
    public void Choose_word_offers_english_options_and_checks_the_choice()
    {
        var factory = new ExerciseFactory(new Random(3));
        ExerciseWord word = Word("bump into", "случайно встретить");

        ExerciseTask task = factory.ChooseWord(word, ["run into", "Bump into", "come across", "get on", "take off"]);

        task.Kind.ShouldBe(ExerciseKind.ChooseWord);
        task.Prompt.ShouldBe("случайно встретить");
        task.Options.Count.ShouldBe(4);
        task.Options.Count(o => o.Equals("bump into", StringComparison.OrdinalIgnoreCase)).ShouldBe(1);
        task.Options[task.CorrectOption!.Value].ShouldBe("bump into");

        AnswerChecker.Check(task, task.CorrectOption.Value.ToString()).Verdict.ShouldBe(AnswerVerdict.Correct);
        int wrongIndex = task.CorrectOption.Value == 0 ? 1 : 0;
        CheckResult wrong = AnswerChecker.Check(task, wrongIndex.ToString());
        wrong.Verdict.ShouldBe(AnswerVerdict.Wrong);
        wrong.Explanation.ShouldContain("случайно встретить");
    }

    [Fact]
    public void Preferences_follow_weak_spots_only_after_enough_answers()
    {
        static WeaknessProfile Profile(int total, Dictionary<string, double> tags, params FormatStat[] formats) => new(total, tags, formats, []);

        SessionService.Preferences(Profile(10, new() { ["WordForm"] = 0.5 })).ShouldBe(ExercisePreferences.Default);
        SessionService.Preferences(Profile(50, new() { ["WordForm"] = 0.06 })).GapShare.ShouldBe(0.8);
        SessionService.Preferences(Profile(50, new() { ["Preposition"] = 0.1 })).GapShare.ShouldBe(0.8);
        SessionService.Preferences(Profile(50, new() { ["Typo"] = 0.2 })).GapShare.ShouldBe(0.35);
        SessionService.Preferences(Profile(50, new(), new FormatStat("ListenChoose", 10, 0.5), new FormatStat("ChooseTranslation", 30, 0.2)))
            .ListenShare.ShouldBe(0.5);
        SessionService.Preferences(Profile(50, new(), new FormatStat("ListenChoose", 3, 0.9), new FormatStat("ChooseTranslation", 30, 0.2)))
            .ListenShare.ShouldBe(ExercisePreferences.Default.ListenShare);
    }

    [Fact]
    public void Problem_item_needs_one_warm_up_before_the_real_task()
    {
        Guid card = Guid.NewGuid();
        var session = LearningSession.Start(Guid.NewGuid(), SessionKinds.Mistakes, [new SessionItem(card, Guid.NewGuid(), "Recall", false, true, 0, IsProblem: true)], DateTimeOffset.UtcNow);

        session.Current!.NeedsWarmUp.ShouldBeTrue();
        Guid attempt = Guid.NewGuid();
        session.CompleteWarmUp(attempt, DateTimeOffset.UtcNow);

        session.Current!.NeedsWarmUp.ShouldBeFalse();
        session.Position.ShouldBe(0);
        session.Answered.ShouldBe(0);
        session.WasIntroduced(attempt).ShouldBeTrue();

        session.CompleteAnswer("Wrong", repeatSoon: true, DateTimeOffset.UtcNow);
        session.Queue.Count.ShouldBe(2);
        session.Queue[1].NeedsWarmUp.ShouldBeFalse();
    }
}
