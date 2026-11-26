using EnglishPlatform.LearningProfiles.Application;
using EnglishPlatform.LearningProfiles.Domain;
using Shouldly;

namespace EnglishPlatform.Learning.Tests;

public sealed class PlacementEngineTests(ITestOutputHelper output)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);

    private static PlacementAnswer A(Skill skill, Cefr level, bool correct, bool dontKnow = false) =>
        new(Guid.NewGuid(), skill, level, dontKnow ? -1 : correct ? 0 : 1, correct, Now, 4);

    /// <summary>
    /// Ученик с уровнем <paramref name="trueLevel"/> (θ — середина уровня) проходит тест: знает ответ с вероятностью
    /// по модели, не знает — угадывает один из 4 вариантов (или жмёт «Не знаю» с вероятностью <paramref name="honesty"/>).
    /// </summary>
    private static List<PlacementAnswer> Simulate(Cefr trueLevel, PlacementPlan plan, Random random, double honesty = 0)
    {
        double theta = (int)trueLevel + 0.5;
        var answers = new List<PlacementAnswer>();
        while (PlacementEngine.NextStep(answers, plan) is var (skill, level))
        {
            double know = 1 / (1 + Math.Exp(-PlacementEngine.Discrimination * (theta - (int)level)));
            if (random.NextDouble() < know)
            {
                answers.Add(A(skill, level, true));
            }
            else if (random.NextDouble() < honesty)
            {
                answers.Add(A(skill, level, false, dontKnow: true));
            }
            else
            {
                answers.Add(A(skill, level, random.Next(4) == 0));
            }
        }

        return answers;
    }

    private static Cefr OverallOf(List<PlacementAnswer> answers) =>
        PlacementEngine.Overall(
            answers.GroupBy(a => a.Skill).ToDictionary(g => g.Key, g => PlacementEngine.Ability(g)),
            answers.GroupBy(a => a.Skill).ToDictionary(g => g.Key, g => g.Count()));

    /// <summary>
    /// Главное свойство: тест находит уровень и не завышает его на две ступени (B1 → C1), даже когда ученик
    /// угадывает. Тысяча прогонов на каждый уровень и режим; доли пишутся в вывод теста.
    /// </summary>
    [Theory]
    [InlineData(PlacementLength.Short, 0.55, 0.97, 0.03)]
    [InlineData(PlacementLength.Long, 0.88, 0.99, 0.01)]
    public void Overall_level_is_found_despite_guessing(PlacementLength length, double minExact, double minWithinOne, double maxOverTwo)
    {
        var random = new Random(42);
        PlacementPlan plan = PlacementPlan.For(length);
        const int runs = 400;
        foreach (Cefr trueLevel in Enum.GetValues<Cefr>())
        {
            int exact = 0, withinOne = 0, overTwo = 0;
            for (int i = 0; i < runs; i++)
            {
                List<PlacementAnswer> answers = Simulate(trueLevel, plan, random);
                answers.Count.ShouldBe(plan.TotalQuestions);
                int diff = (int)OverallOf(answers) - (int)trueLevel;
                exact += diff == 0 ? 1 : 0;
                withinOne += Math.Abs(diff) <= 1 ? 1 : 0;
                overTwo += diff >= 2 ? 1 : 0;
            }

            output.WriteLine($"{length} {trueLevel}: точно {exact * 100.0 / runs:F1}%, ±1 {withinOne * 100.0 / runs:F1}%, завышение на 2+ {overTwo * 100.0 / runs:F1}%");
            (exact / (double)runs).ShouldBeGreaterThanOrEqualTo(minExact, $"{trueLevel}");
            (withinOne / (double)runs).ShouldBeGreaterThanOrEqualTo(minWithinOne, $"{trueLevel}");
            (overTwo / (double)runs).ShouldBeLessThanOrEqualTo(maxOverTwo, $"{trueLevel}");
        }
    }

    [Fact]
    public void One_lucky_guess_high_up_does_not_raise_the_level()
    {
        // Так было в жалобе: B1 уверенно, на B2 через раз, на C1 один удачный ответ — раньше это давало C1.
        List<PlacementAnswer> answers =
        [
            A(Skill.Grammar, Cefr.A2, true), A(Skill.Grammar, Cefr.B1, true), A(Skill.Grammar, Cefr.B2, false),
            A(Skill.Grammar, Cefr.B1, true), A(Skill.Grammar, Cefr.B2, true), A(Skill.Grammar, Cefr.C1, true),
            A(Skill.Grammar, Cefr.C1, false),
        ];

        PlacementEngine.Estimate(answers).ShouldBeOneOf(Cefr.B1, Cefr.B2);
        PlacementEngine.Estimate(answers).ShouldNotBe(Cefr.C1);
    }

    [Fact]
    public void Dont_know_lowers_the_estimate_at_least_as_much_as_a_wrong_answer()
    {
        double withDontKnow = PlacementEngine.Ability([A(Skill.Vocabulary, Cefr.B2, false, dontKnow: true)]);
        double withWrong = PlacementEngine.Ability([A(Skill.Vocabulary, Cefr.B2, false)]);

        withDontKnow.ShouldBeLessThanOrEqualTo(withWrong + 0.01);
    }

    [Fact]
    public void Questions_follow_the_estimate()
    {
        PlacementPlan plan = PlacementPlan.Short;
        PlacementEngine.NextStep([], plan).ShouldBe((Skill.Vocabulary, Cefr.A2));

        (Skill _, Cefr up) = PlacementEngine.NextStep([A(Skill.Vocabulary, Cefr.A2, true), A(Skill.Vocabulary, Cefr.B1, true)], plan)!.Value;
        (Skill _, Cefr down) = PlacementEngine.NextStep([A(Skill.Vocabulary, Cefr.A2, false), A(Skill.Vocabulary, Cefr.A1, false)], plan)!.Value;
        ((int)up).ShouldBeGreaterThan((int)Cefr.A2);
        down.ShouldBe(Cefr.A1);
    }

    [Fact]
    public void Next_skill_starts_from_what_previous_skills_showed()
    {
        PlacementPlan plan = PlacementPlan.Short;
        List<PlacementAnswer> vocabulary = Enumerable.Range(0, 7).Select(_ => A(Skill.Vocabulary, Cefr.B2, true)).ToList();

        (Skill skill, Cefr level) = PlacementEngine.NextStep(vocabulary, plan)!.Value;
        skill.ShouldBe(Skill.Grammar);
        ((int)level).ShouldBeGreaterThanOrEqualTo((int)Cefr.B2);
    }

    [Fact]
    public void Long_test_asks_twice_as_many_questions()
    {
        PlacementPlan.Long.TotalQuestions.ShouldBe(2 * PlacementPlan.Short.TotalQuestions);
        Simulate(Cefr.B1, PlacementPlan.Long, new Random(1)).Count.ShouldBe(PlacementPlan.Long.TotalQuestions);
    }

    [Fact]
    public void Overall_of_saved_levels_is_average_rounded_down()
    {
        PlacementEngine.Overall([Cefr.B1, Cefr.B2, Cefr.B2]).ShouldBe(Cefr.B1);
        PlacementEngine.Overall([Cefr.C1, Cefr.C1, Cefr.C2]).ShouldBe(Cefr.C1);
    }

    [Fact]
    public void Starter_words_are_own_and_next_level()
    {
        PlacementService.WordLevels(Cefr.B1).ShouldBe(["B1", "B2"]);
        PlacementService.WordLevels(Cefr.C2).ShouldBe(["C1", "C2"]);
    }
}
