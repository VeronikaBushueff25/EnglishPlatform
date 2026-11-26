using EnglishPlatform.Exercises.Application;
using EnglishPlatform.Exercises.Contracts;
using EnglishPlatform.LearningSessions.Application;
using EnglishPlatform.SpacedRepetition.Contracts;
using Shouldly;

namespace EnglishPlatform.Learning.Tests;

public sealed class ExerciseTests
{
    private static ExerciseWord Word(string text, string type = "Word", string? example = null, params string[] translations) => new(
        Guid.NewGuid(), text, type, null, "B1", null, translations.Length > 0 ? translations : ["перевод"], example, null, null);

    private static ExerciseTask Typed(string text, ExerciseKind kind = ExerciseKind.TypeWord, string? answer = null) =>
        new(Guid.NewGuid(), kind, Word(text), "перевод", null, [], null, [answer ?? text], null, null, null);

    [Theory]
    [InlineData("deny", "deny", AnswerVerdict.Correct, ErrorTag.None)]
    [InlineData("deny", "  Deny. ", AnswerVerdict.Correct, ErrorTag.None)]
    [InlineData("to deny", "deny", AnswerVerdict.Correct, ErrorTag.None)]
    [InlineData("deny", "to deny", AnswerVerdict.Correct, ErrorTag.None)]
    [InlineData("consider doing something", "consider doing sth", AnswerVerdict.Correct, ErrorTag.None)]
    [InlineData("lose your temper", "lose one's temper", AnswerVerdict.Correct, ErrorTag.None)]
    [InlineData("Don’t worry", "don't worry", AnswerVerdict.Correct, ErrorTag.None)]
    [InlineData("environment", "enviroment", AnswerVerdict.Almost, ErrorTag.Typo)]
    [InlineData("inconsistency", "inconsistancy", AnswerVerdict.Almost, ErrorTag.Typo)]
    [InlineData("a piece of cake", "piece of cake", AnswerVerdict.Almost, ErrorTag.Article)]
    [InlineData("deny", "denied", AnswerVerdict.Almost, ErrorTag.WordForm)]
    [InlineData("apply", "applies", AnswerVerdict.Almost, ErrorTag.WordForm)]
    [InlineData("deny", "refuse", AnswerVerdict.Wrong, ErrorTag.WrongWord)]
    [InlineData("cat", "cut", AnswerVerdict.Wrong, ErrorTag.WrongWord)]
    [InlineData("deny", "   ", AnswerVerdict.Wrong, ErrorTag.Empty)]
    public void Checks_typed_answers(string expected, string answer, AnswerVerdict verdict, ErrorTag tag)
    {
        CheckResult result = AnswerChecker.Check(Typed(expected), answer);

        result.Verdict.ShouldBe(verdict);
        result.ErrorTag.ShouldBe(tag);
        result.CorrectAnswer.ShouldBe(expected);
        result.Explanation.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Fill_gap_wants_the_form_from_the_sentence()
    {
        ExerciseTask task = Typed("deny", ExerciseKind.FillGap, answer: "denied");

        AnswerChecker.Check(task, "denied").Verdict.ShouldBe(AnswerVerdict.Correct);
        CheckResult baseForm = AnswerChecker.Check(task, "deny");
        baseForm.Verdict.ShouldBe(AnswerVerdict.Almost);
        baseForm.ErrorTag.ShouldBe(ErrorTag.WordForm);
        baseForm.Explanation.ShouldContain("denied");
    }

    [Fact]
    public void Checks_choice_by_index_or_text()
    {
        var task = new ExerciseTask(Guid.NewGuid(), ExerciseKind.ChooseTranslation, Word("apple", translations: "яблоко"), "apple", null,
            ["груша", "яблоко", "слива"], 1, ["яблоко"], null, null, null);

        AnswerChecker.Check(task, "1").Verdict.ShouldBe(AnswerVerdict.Correct);
        AnswerChecker.Check(task, "Яблоко").Verdict.ShouldBe(AnswerVerdict.Correct);
        CheckResult wrong = AnswerChecker.Check(task, "0");
        wrong.Verdict.ShouldBe(AnswerVerdict.Wrong);
        wrong.Explanation.ShouldContain("груша");
        AnswerChecker.Check(task, null).ErrorTag.ShouldBe(ErrorTag.Empty);
    }

    [Theory]
    [InlineData("bump into", "I bumped into Tom yesterday.", "bumped into")]
    [InlineData("deny", "He denied taking the money.", "denied")]
    [InlineData("apple", "She eats an Apple every morning.", "Apple")]
    [InlineData("to be honest", "To be honest, I didn't like it.", "be honest")]
    [InlineData("look forward to", "I'm looking forward to the holidays.", "looking forward to")]
    public void Finds_word_in_example(string text, string sentence, string expected)
    {
        (int Index, int Length)? match = ExerciseFactory.FindInSentence(text, sentence);

        match.ShouldNotBeNull();
        sentence.Substring(match.Value.Index, match.Value.Length).ShouldBe(expected);
    }

    [Theory]
    [InlineData("take something into account", "Take the weather into account.")]
    [InlineData("would + V1", "I would go.")]
    [InlineData("deny", "Nobody said anything.")]
    public void Does_not_make_gaps_for_patterns_or_missing_words(string text, string sentence) =>
        ExerciseFactory.FindInSentence(text, sentence).ShouldBeNull();

    [Fact]
    public void Builds_tasks_for_both_modes()
    {
        var factory = new ExerciseFactory(new Random(1));
        ExerciseWord word = Word("deny", example: "He denied taking the money.", translations: ["отрицать", "отказываться признать"]);

        for (int i = 0; i < 20; i++)
        {
            ExerciseTask recognition = factory.Build(word, ExerciseMode.Recognition, ["груша", "стол", "бежать"]);
            recognition.Kind.ShouldBeOneOf(ExerciseKind.ChooseTranslation, ExerciseKind.ListenChoose);
            recognition.Options.Count.ShouldBe(4);
            recognition.Options[recognition.CorrectOption!.Value].ShouldBe("отрицать, отказываться признать");

            ExerciseTask recall = factory.Build(word, ExerciseMode.Recall, []);
            recall.Kind.ShouldBeOneOf(ExerciseKind.TypeWord, ExerciseKind.FillGap);
            if (recall.Kind == ExerciseKind.FillGap)
            {
                recall.Sentence.ShouldBe($"He {ExerciseFactory.Gap} taking the money.");
                recall.AcceptedAnswers.ShouldBe(["denied"]);
            }
            else
            {
                recall.Hint.ShouldBe("d___");
            }
        }
    }

    [Theory]
    [InlineData(ExerciseKind.ChooseTranslation, AnswerVerdict.Wrong, false, 1000, false, ReviewRating.Again)]
    [InlineData(ExerciseKind.TypeWord, AnswerVerdict.Almost, false, 1000, false, ReviewRating.Hard)]
    [InlineData(ExerciseKind.TypeWord, AnswerVerdict.Correct, true, 1000, false, ReviewRating.Hard)]
    [InlineData(ExerciseKind.ChooseTranslation, AnswerVerdict.Correct, false, 1500, false, ReviewRating.Easy)]
    [InlineData(ExerciseKind.ChooseTranslation, AnswerVerdict.Correct, false, 1500, true, ReviewRating.Good)]
    [InlineData(ExerciseKind.ChooseTranslation, AnswerVerdict.Correct, false, 9000, false, ReviewRating.Good)]
    [InlineData(ExerciseKind.TypeWord, AnswerVerdict.Correct, false, 5000, false, ReviewRating.Easy)]
    public void Derives_fsrs_rating_from_answer(ExerciseKind kind, AnswerVerdict verdict, bool hint, int ms, bool firstSight, ReviewRating expected) =>
        SessionService.Rate(new SessionOptions(), kind, verdict, hint, ms, firstSight).ShouldBe(expected);

    [Fact]
    public void Day_starts_at_local_midnight()
    {
        var now = new DateTimeOffset(2026, 11, 26, 22, 30, 0, TimeSpan.Zero); // в Москве уже 27-е, 01:30

        SessionService.DayStart(now, "Europe/Moscow").ShouldBe(new DateTimeOffset(2026, 11, 26, 21, 0, 0, TimeSpan.Zero));
        SessionService.DayStart(now, "Unknown/Zone").ShouldBe(new DateTimeOffset(2026, 11, 26, 0, 0, 0, TimeSpan.Zero));
    }
}
