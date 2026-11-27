using EnglishPlatform.BuildingBlocks.Events;
using EnglishPlatform.BuildingBlocks.Infrastructure.Http;
using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
using EnglishPlatform.BuildingBlocks.Infrastructure.Persistence;
using EnglishPlatform.LearningSessions.Contracts;
using EnglishPlatform.Pets.Application;
using EnglishPlatform.Pets.Domain;
using EnglishPlatform.Vocabulary.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EnglishPlatform.Pets.Infrastructure;

/// <summary>Модуль Pets. Питомец пользователя: кормится занятиями, растёт, грустит без них.</summary>
public sealed class PetsModule : IModule
{
    public string Name => "Pets";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<PetsDb>(configuration, PetsDb.SchemaName);
        services.AddScoped<IPetsDb>(sp => sp.GetRequiredService<PetsDb>());
        services.AddScoped<PetDays>();
        services.AddScoped<PetService>();
        services.AddScoped<IIntegrationEventHandler<SessionFinished>, FeedPetOnSessionFinished>();
        services.AddScoped<IIntegrationEventHandler<UserWordAdded>, CountWordForPet>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        RouteGroupBuilder pet = api.MapGroup("/pet").WithTags("Pet").RequireAuthorization();

        pet.MapGet("/", async (PetService service, CancellationToken ct) =>
                TypedResults.Ok(new MyPetResponse(await service.GetAsync(ct))))
            .WithName("GetMyPet")
            .WithSummary("Питомец текущего пользователя (pet = null, если ещё не заведён)");

        pet.MapPost("/", async (AdoptPetRequest request, PetService service, CancellationToken ct) =>
                (await service.AdoptAsync(request, ct)).ToHttpResult())
            .WithValidation<AdoptPetRequest>()
            .WithName("AdoptPet")
            .Produces<PetDto>();

        pet.MapPut("/", async (UpdatePetRequest request, PetService service, CancellationToken ct) =>
                (await service.UpdateAsync(request, ct)).ToHttpResult())
            .WithValidation<UpdatePetRequest>()
            .WithName("UpdatePet")
            .Produces<PetDto>();

        pet.MapPost("/feed", async (PetService service, CancellationToken ct) => (await service.FeedAsync(ct)).ToHttpResult())
            .WithName("FeedPet")
            .Produces<PetDto>();

        pet.MapPost("/pat", async (PetService service, CancellationToken ct) => (await service.PatAsync(ct)).ToHttpResult())
            .WithName("PatPet")
            .Produces<PetDto>();

        pet.MapPost("/activity", async (PetActivityRequest request, PetService service, CancellationToken ct) => (await service.RecordAsync(request, ct)).ToHttpResult())
            .WithName("RecordPetActivity")
            .WithSummary("Сыграл в игру или дочитал текст — засчитать в просьбы питомца и достижения")
            .Produces<PetProgressDto>();

        pet.MapPost("/quiz", async (PetQuizAnswerRequest request, PetService service, CancellationToken ct) => (await service.AnswerQuizAsync(request, ct)).ToHttpResult())
            .WithName("AnswerPetQuiz")
            .WithSummary("Ответ на вопрос питомца о слове")
            .Produces<PetProgressDto>();
    }
}

public sealed record MyPetResponse(PetDto? Pet);

public sealed class PetsDb(DbContextOptions<PetsDb> options) : ModuleDbContext(options), IPetsDb
{
    public const string SchemaName = "pets";

    public override string Schema => SchemaName;

    public DbSet<Pet> Pets => Set<Pet>();
}

internal sealed class PetConfiguration : IEntityTypeConfiguration<Pet>
{
    public void Configure(EntityTypeBuilder<Pet> pet)
    {
        pet.ToTable("pets");
        pet.HasKey(p => p.Id);
        pet.Property(p => p.Id).HasColumnName("user_id").ValueGeneratedNever();
        pet.Property(p => p.Species).HasConversion<string>().HasMaxLength(16);
        pet.Property(p => p.Palette).HasMaxLength(Pet.MaxPaletteLength);
        pet.Property(p => p.Name).HasMaxLength(Pet.MaxNameLength);
        pet.Property(p => p.Accessory).HasMaxLength(16);
        pet.OwnsMany(p => p.Quests, q =>
        {
            q.ToJson();
            q.Property(x => x.Kind).HasConversion<string>();
        });
        pet.Navigation(p => p.Quests).HasField("_quests").UsePropertyAccessMode(PropertyAccessMode.Field);
        pet.Ignore(p => p.Level);
        pet.Ignore(p => p.Stage);
        pet.Ignore(p => p.UnlockedAccessories);
    }
}
