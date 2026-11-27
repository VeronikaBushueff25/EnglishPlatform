using EnglishPlatform.Pets.Domain;
using Shouldly;

namespace EnglishPlatform.Pets.Tests;

public sealed class PetTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 27, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 27);

    private static Pet NewPet() => Pet.Adopt(Guid.NewGuid(), PetSpecies.Cat, "ginger", " Пушок ", Start);

    [Fact]
    public void New_pet_is_happy_and_has_a_welcome_gift()
    {
        Pet pet = NewPet();
        pet.Name.ShouldBe("Пушок");
        pet.Treats.ShouldBe(3);
        pet.Level.ShouldBe(1);
        pet.Stage.ShouldBe(PetStage.Baby);
        pet.MoodAt(Start).ShouldBe(PetMood.Happy);
    }

    [Fact]
    public void Satiety_and_affection_fade_with_time()
    {
        Pet pet = NewPet();
        pet.SatietyAt(Start.AddHours(18)).ShouldBe(70 - 50, 0.01);
        pet.SatietyAt(Start.AddDays(3)).ShouldBe(0);
        pet.AffectionAt(Start.AddHours(36)).ShouldBe(60 - 50, 0.01);
    }

    [Fact]
    public void Mood_follows_hunger_and_missed_lessons()
    {
        Pet pet = NewPet();
        pet.MoodAt(Start.AddHours(20)).ShouldBe(PetMood.Hungry);
        pet.MoodAt(Start.AddDays(2)).ShouldBe(PetMood.Sad);
        pet.MoodAt(Start.AddDays(5)).ShouldBe(PetMood.Away);
    }

    [Fact]
    public void Lesson_brings_treats_completes_the_lesson_request_and_returns_the_pet_from_a_walk()
    {
        Pet pet = NewPet();
        DateTimeOffset later = Start.AddDays(6);
        pet.MoodAt(later).ShouldBe(PetMood.Away);

        (int treats, IReadOnlyList<PetQuest> completed) = pet.OnLessonFinished(answered: 20, correct: 17, mistakes: false, Today.AddDays(6), later);

        treats.ShouldBe(5);
        completed.Single().Kind.ShouldBe(PetActivity.Lesson);
        pet.Treats.ShouldBe(3 + 5 + 1);
        pet.Xp.ShouldBe(37 + 10);
        pet.MoodAt(later).ShouldNotBe(PetMood.Away);
        pet.MoodAt(later).ShouldNotBe(PetMood.Sad);
    }

    [Fact]
    public void Daily_requests_are_a_lesson_a_game_and_one_more_learning_task()
    {
        Pet pet = NewPet();
        pet.EnsureQuests(Today);
        pet.Quests.Count.ShouldBe(3);
        pet.Quests[0].Kind.ShouldBe(PetActivity.Lesson);
        pet.Quests[1].Kind.ShouldBeOneOf(PetActivity.Match, PetActivity.Blitz, PetActivity.Scramble);
        pet.Quests[2].Kind.ShouldBeOneOf(PetActivity.Reading, PetActivity.AddWords, PetActivity.Quiz, PetActivity.MistakesLesson);

        PetActivity game = pet.Quests[1].Kind;
        pet.Record(game, 1, Today, Start).Single().Kind.ShouldBe(game);
        pet.Record(game, 1, Today, Start).ShouldBeEmpty(); // второй раз награды нет
        pet.Treats.ShouldBe(5);
        pet.GamesPlayed.ShouldBe(2);

        pet.EnsureQuests(Today.AddDays(1));
        pet.Quests.ShouldAllBe(q => !q.Done);
        pet.Quests[1].Kind.ShouldNotBe(game);
    }

    [Fact]
    public void Quiz_answers_count_only_when_correct()
    {
        Pet pet = NewPet();
        pet.AnswerQuiz(false, Today, Start).ShouldBeEmpty();
        pet.QuizCorrect.ShouldBe(0);
        pet.AnswerQuiz(true, Today, Start);
        pet.QuizCorrect.ShouldBe(1);
        pet.AffectionAt(Start).ShouldBe(60 + Pet.QuizAffection);
    }

    [Fact]
    public void Streak_counts_consecutive_days_and_breaks_after_a_gap()
    {
        Pet pet = NewPet();
        pet.OnLessonFinished(5, 5, false, Today, Start);
        pet.OnLessonFinished(5, 5, false, Today, Start.AddHours(2)); // тот же день — серия не растёт
        pet.OnLessonFinished(5, 5, false, Today.AddDays(1), Start.AddDays(1));
        pet.StreakOn(Today.AddDays(1)).ShouldBe(2);
        pet.StreakOn(Today.AddDays(2)).ShouldBe(2); // сегодня ещё можно продолжить
        pet.StreakOn(Today.AddDays(3)).ShouldBe(0);

        pet.OnLessonFinished(5, 5, false, Today.AddDays(3), Start.AddDays(3));
        pet.StreakOn(Today.AddDays(3)).ShouldBe(1);
        pet.BestStreak.ShouldBe(2);
    }

    [Fact]
    public void Room_decor_opens_with_learning_milestones()
    {
        Pet pet = NewPet();
        Pet.Decor.Where(d => pet.CounterValue(d.Counter) >= d.Target).Select(d => d.Key).ShouldBe(["rug"]);
        pet.OnLessonFinished(5, 5, false, Today, Start);
        Pet.Decor.Where(d => pet.CounterValue(d.Counter) >= d.Target).Select(d => d.Key).ShouldBe(["rug", "plant"]);
    }

    [Fact]
    public void Feeding_uses_a_treat_and_refuses_when_full_away_or_out_of_treats()
    {
        Pet pet = NewPet();
        pet.Feed(Start).ShouldBe(PetActionResult.Done);
        pet.Treats.ShouldBe(2);
        pet.SatietyAt(Start).ShouldBe(100);
        pet.Feed(Start).ShouldBe(PetActionResult.Full);

        DateTimeOffset hungry = Start.AddHours(30);
        pet.Feed(hungry).ShouldBe(PetActionResult.Done);
        pet.Feed(hungry).ShouldBe(PetActionResult.Done);
        pet.Feed(hungry).ShouldBe(PetActionResult.NoTreats);

        pet.Feed(Start.AddDays(6)).ShouldBe(PetActionResult.Away);
    }

    [Fact]
    public void Pats_count_only_a_few_times_a_day()
    {
        Pet pet = NewPet();
        for (int i = 0; i < Pet.MaxPatsPerDay; i++)
        {
            pet.Pat(Today, Start).ShouldBe(PetActionResult.Done);
        }

        pet.Pat(Today, Start).ShouldBe(PetActionResult.Tired);
        pet.PatsLeft(Today).ShouldBe(0);
        pet.PatsLeft(Today.AddDays(1)).ShouldBe(Pet.MaxPatsPerDay);
    }

    [Fact]
    public void Levels_unlock_accessories_and_stages()
    {
        Pet.XpForLevel(1).ShouldBe(0);
        Pet.XpForLevel(2).ShouldBe(50);
        Pet.XpForLevel(3).ShouldBe(150);

        Pet pet = NewPet();
        pet.Wear("bow").ShouldBeFalse();
        pet.OnLessonFinished(20, 20, false, Today, Start); // 40 + 10 за просьбу
        pet.Level.ShouldBe(2);
        pet.Wear("bow").ShouldBeTrue();
        pet.Wear("crown").ShouldBeFalse();

        pet.OnLessonFinished(60, 60, false, Today, Start); // 170
        pet.Level.ShouldBe(3);
        pet.Stage.ShouldBe(PetStage.Teen);
        pet.UnlockedAccessories.ShouldBe(["bow", "scarf"]);
    }

    [Fact]
    public void Palette_must_belong_to_the_species()
    {
        Pet pet = NewPet();
        pet.Restyle("red").ShouldBeFalse();
        pet.Restyle("black").ShouldBeTrue();
        pet.Palette.ShouldBe("black");
    }
}
