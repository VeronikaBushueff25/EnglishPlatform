using EnglishPlatform.LearningProfiles.Application;
using EnglishPlatform.LearningProfiles.Domain;
using Shouldly;

namespace EnglishPlatform.Learning.Tests;

public sealed class PlacementEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);

    private static PlacementAnswer A(Skill skill, Cefr level, bool correct) => new(Guid.NewGuid(), skill, level, correct ? 0 : 1, correct, Now);

    /// <summary>Прогоняет тест: пользователь отвечает верно на вопросы не выше своего уровня.</summary>
    private static List<PlacementAnswer> Simulate(Cefr trueLevel, PlacementPlan plan)
    {
        var answers = new List<PlacementAnswer>();
        while (PlacementEngine.NextStep(answers, plan) is var (skill, level))
        {
            answers.Add(A(skill, level, level <= trueLevel));
        }

        return answers;
    }

    [Theory]
    [InlineData(Cefr.A1)]
    [InlineData(Cefr.A2)]
    [InlineData(Cefr.B1)]
    [InlineData(Cefr.B2)]
    [InlineData(Cefr.C1)]
    [InlineData(Cefr.C2)]
    public void Staircase_finds_the_true_level_for_every_skill(Cefr trueLevel)
    {
        List<PlacementAnswer> answers = Simulate(trueLevel, PlacementPlan.Default);

        answers.Count.ShouldBe(PlacementPlan.Default.TotalQuestions);
        foreach (Skill skill in Enum.GetValues<Skill>())
        {
            PlacementEngine.Estimate(answers.Where(a => a.Skill == skill)).ShouldBe(trueLevel, $"{skill}");
        }
    }

    [Fact]
    public void Starts_from_a2_and_moves_up_after_correct_and_down_after_wrong()
    {
        PlacementPlan plan = PlacementPlan.Default;
        PlacementEngine.NextStep([], plan).ShouldBe((Skill.Vocabulary, Cefr.A2));
        PlacementEngine.NextStep([A(Skill.Vocabulary, Cefr.A2, true)], plan).ShouldBe((Skill.Vocabulary, Cefr.B1));
        PlacementEngine.NextStep([A(Skill.Vocabulary, Cefr.A2, false)], plan).ShouldBe((Skill.Vocabulary, Cefr.A1));
        PlacementEngine.NextStep([A(Skill.Vocabulary, Cefr.A1, false)], plan).ShouldBe((Skill.Vocabulary, Cefr.A1));
        PlacementEngine.NextStep([A(Skill.Vocabulary, Cefr.C2, true)], plan).ShouldBe((Skill.Vocabulary, Cefr.C2));
    }

    [Fact]
    public void Next_skill_starts_one_level_below_the_previous_result()
    {
        var plan = new PlacementPlan([(Skill.Vocabulary, 2), (Skill.Grammar, 2)], Cefr.A2);
        List<PlacementAnswer> vocabulary = [A(Skill.Vocabulary, Cefr.B2, true), A(Skill.Vocabulary, Cefr.C1, true)];

        PlacementEngine.NextStep(vocabulary, plan).ShouldBe((Skill.Grammar, Cefr.B2));
    }

    [Fact]
    public void Estimate_needs_at_least_half_correct_at_a_level()
    {
        PlacementEngine.Estimate([]).ShouldBe(Cefr.A1);
        PlacementEngine.Estimate([A(Skill.Grammar, Cefr.B2, true), A(Skill.Grammar, Cefr.B2, false), A(Skill.Grammar, Cefr.C1, false)]).ShouldBe(Cefr.B2);
        PlacementEngine.Estimate([A(Skill.Grammar, Cefr.B2, true), A(Skill.Grammar, Cefr.B2, false), A(Skill.Grammar, Cefr.B2, false), A(Skill.Grammar, Cefr.B1, true)])
            .ShouldBe(Cefr.B1);
    }

    [Fact]
    public void Overall_is_rounded_down_average_and_word_levels_include_the_next_one()
    {
        PlacementEngine.Overall([Cefr.B1, Cefr.B2, Cefr.B2]).ShouldBe(Cefr.B1);
        PlacementEngine.Overall([Cefr.C1, Cefr.C1, Cefr.C2]).ShouldBe(Cefr.C1);
        PlacementService.WordLevels(Cefr.B1).ShouldBe(["B1", "B2"]);
        PlacementService.WordLevels(Cefr.C2).ShouldBe(["C1", "C2"]);
    }

    [Fact]
    public void Attempt_stores_answers_and_result()
    {
        var attempt = PlacementAttempt.Start(Guid.NewGuid(), 3, Now);
        attempt.Ask(new PlacementCurrent(Guid.NewGuid(), [2, 0, 1]));
        attempt.Current.ShouldNotBeNull();

        attempt.Answer(A(Skill.Vocabulary, Cefr.A2, true));
        attempt.Current.ShouldBeNull();
        attempt.Answer(A(Skill.Vocabulary, Cefr.B1, true));

        IReadOnlyDictionary<Skill, Cefr> result = attempt.Finish(Enum.GetValues<Skill>(), Now);
        result.ShouldContainKeyAndValue(Skill.Vocabulary, Cefr.B1);
        result.ContainsKey(Skill.Reading).ShouldBeFalse();
        attempt.IsFinished.ShouldBeTrue();
    }
}
