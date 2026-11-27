using EnglishPlatform.Ai.Infrastructure;
using EnglishPlatform.Analytics.Infrastructure;
using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
using EnglishPlatform.Exercises.Infrastructure;
using EnglishPlatform.Grammar.Infrastructure;
using EnglishPlatform.Identity.Infrastructure;
using EnglishPlatform.LearningProfiles.Infrastructure;
using EnglishPlatform.LearningSessions.Infrastructure;
using EnglishPlatform.Pets.Infrastructure;
using EnglishPlatform.SpacedRepetition.Infrastructure;
using EnglishPlatform.Users.Infrastructure;
using EnglishPlatform.Vocabulary.Infrastructure;

namespace EnglishPlatform.Host;

/// <summary>Все модули монолита. Новый модуль подключается одной строкой здесь.</summary>
internal static class ModuleCatalog
{
    public static IReadOnlyCollection<IModule> All { get; } =
    [
        new IdentityModule(),
        new UsersModule(),
        new LearningProfilesModule(),
        new VocabularyModule(),
        new LearningSessionsModule(),
        new ExercisesModule(),
        new SpacedRepetitionModule(),
        new GrammarModule(),
        new AiModule(),
        new AnalyticsModule(),
        new PetsModule(),
    ];
}
