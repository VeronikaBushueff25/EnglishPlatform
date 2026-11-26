using EnglishPlatform.SpacedRepetition.Domain;
using Shouldly;

namespace EnglishPlatform.Learning.Tests;

/// <summary>Ожидаемые числа посчитаны отдельно по формулам FSRS-5 (py-fsrs) с весами по умолчанию.</summary>
public sealed class FsrsSchedulerTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);
    private readonly FsrsScheduler _fsrs = new(new FsrsParameters());

    [Theory]
    [InlineData(Rating.Again, 0.40255)]
    [InlineData(Rating.Hard, 1.18385)]
    [InlineData(Rating.Good, 3.173)]
    [InlineData(Rating.Easy, 15.69105)]
    public void First_answer_sets_initial_stability(Rating rating, double stability) =>
        _fsrs.Review(FsrsScheduler.NewCard(Start), rating, Start).Stability.ShouldBe(stability, 1e-9);

    [Fact]
    public void New_card_goes_through_learning_steps()
    {
        MemoryState good = _fsrs.Review(FsrsScheduler.NewCard(Start), Rating.Good, Start);
        good.State.ShouldBe(CardState.Learning);
        good.Step.ShouldBe(1);
        good.Due.ShouldBe(Start.AddMinutes(10));
        good.Difficulty.ShouldBe(5.282434, 1e-5);

        MemoryState again = _fsrs.Review(FsrsScheduler.NewCard(Start), Rating.Again, Start);
        again.Due.ShouldBe(Start.AddMinutes(1));
        again.Difficulty.ShouldBe(7.1949, 1e-9);

        MemoryState hard = _fsrs.Review(FsrsScheduler.NewCard(Start), Rating.Hard, Start);
        hard.Due.ShouldBe(Start.AddMinutes(5.5));
    }

    [Fact]
    public void Easy_new_card_graduates_immediately()
    {
        MemoryState easy = _fsrs.Review(FsrsScheduler.NewCard(Start), Rating.Easy, Start);

        easy.State.ShouldBe(CardState.Review);
        easy.Due.ShouldBe(Start.AddDays(16));
    }

    [Fact]
    public void Good_twice_on_the_same_day_graduates_with_short_term_stability_then_grows_on_review()
    {
        MemoryState first = _fsrs.Review(FsrsScheduler.NewCard(Start), Rating.Good, Start);
        MemoryState second = _fsrs.Review(first, Rating.Good, Start.AddMinutes(10));

        second.State.ShouldBe(CardState.Review);
        second.Stability.ShouldBe(4.466858, 1e-5);
        second.Difficulty.ShouldBe(5.272968, 1e-5);
        second.Due.ShouldBe(Start.AddMinutes(10).AddDays(4));

        DateTimeOffset review = second.Due;
        FsrsScheduler.Retrievability(second, review).ShouldBe(0.909071, 1e-5);
        MemoryState third = _fsrs.Review(second, Rating.Good, review);
        third.Stability.ShouldBe(14.217284, 1e-4);
        third.Due.ShouldBe(review.AddDays(14));
    }

    [Fact]
    public void Retrievability_equals_desired_retention_after_one_stability_interval()
    {
        var card = new MemoryState(CardState.Review, 0, 10, 5, Start.AddDays(10), Start);

        FsrsScheduler.Retrievability(card, Start.AddDays(10)).ShouldBe(0.9, 1e-9);
        FsrsScheduler.Retrievability(FsrsScheduler.NewCard(Start), Start).ShouldBe(0);
    }

    [Fact]
    public void Lapse_sends_card_to_relearning_with_lower_stability()
    {
        var card = new MemoryState(CardState.Review, 0, 20, 5, Start.AddDays(20), Start);

        MemoryState lapsed = _fsrs.Review(card, Rating.Again, Start.AddDays(25));

        lapsed.State.ShouldBe(CardState.Relearning);
        lapsed.Due.ShouldBe(Start.AddDays(25).AddMinutes(10));
        lapsed.Stability.ShouldBeLessThan(20);
        lapsed.Difficulty.ShouldBeGreaterThan(5);
    }

    [Fact]
    public void Better_answers_give_longer_intervals_and_difficulty_stays_in_range()
    {
        var card = new MemoryState(CardState.Review, 0, 10, 5, Start.AddDays(10), Start);
        DateTimeOffset at = Start.AddDays(10);

        TimeSpan hard = _fsrs.Review(card, Rating.Hard, at).Due - at;
        TimeSpan good = _fsrs.Review(card, Rating.Good, at).Due - at;
        TimeSpan easy = _fsrs.Review(card, Rating.Easy, at).Due - at;

        hard.ShouldBeLessThan(good);
        good.ShouldBeLessThan(easy);

        MemoryState state = FsrsScheduler.NewCard(Start);
        for (int i = 0; i < 30; i++)
        {
            state = _fsrs.Review(state, Rating.Again, Start.AddDays(i));
            state.Difficulty.ShouldBeInRange(1, 10);
        }
    }

    [Fact]
    public void Card_counts_lapses_and_writes_a_log_entry()
    {
        var card = ReviewCard.Create(Guid.NewGuid(), Guid.NewGuid(), CardMode.Recognition, Start);

        ReviewLog first = card.Review(_fsrs, Rating.Easy, Start, 1200);
        first.StateBefore.ShouldBe(CardState.New);
        first.StateAfter.ShouldBe(CardState.Review);
        first.ScheduledDays.ShouldBe(16, 1e-9);

        ReviewLog lapse = card.Review(_fsrs, Rating.Again, Start.AddDays(16), 5000);
        card.Lapses.ShouldBe(1);
        card.Reps.ShouldBe(2);
        lapse.ElapsedDays.ShouldBe(16, 1e-9);
        card.State.ShouldBe(CardState.Relearning);
    }
}
