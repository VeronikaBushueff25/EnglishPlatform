using EnglishPlatform.BuildingBlocks.Events;
using EnglishPlatform.BuildingBlocks.Results;
using EnglishPlatform.BuildingBlocks.Security;
using EnglishPlatform.Identity.Contracts;
using EnglishPlatform.Users.Contracts;
using EnglishPlatform.Users.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnglishPlatform.Users.Application;

public interface IUsersDb
{
    DbSet<UserProfile> Profiles { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Профиль текущего пользователя. Если профиль не создался при регистрации, он создаётся при первом обращении.</summary>
public sealed class ProfileService(IUsersDb db, ICurrentUser currentUser, TimeProvider timeProvider)
{
    public async Task<MeResponse> GetMeAsync(CancellationToken cancellationToken)
    {
        UserProfile profile = await GetOrCreateAsync(currentUser.UserId, cancellationToken);
        return ToResponse(profile);
    }

    public async Task<Result<MeResponse>> UpdateMeAsync(UpdateMeRequest request, CancellationToken cancellationToken)
    {
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(request.TimeZone, out _))
        {
            return ProfileErrors.UnknownTimeZone;
        }

        UserProfile profile = await GetOrCreateAsync(currentUser.UserId, cancellationToken);
        profile.UpdateProfile(request.DisplayName, request.TimeZone, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(profile);
    }

    public async Task<MeResponse> UpdateSettingsAsync(UpdateSettingsRequest request, CancellationToken cancellationToken)
    {
        UserProfile profile = await GetOrCreateAsync(currentUser.UserId, cancellationToken);
        profile.UpdateSettings(request.InterfaceLanguage, request.Goals, request.DailyNewWords, request.DailyReviews, timeProvider.GetUtcNow(), request.AutoAddWords);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(profile);
    }

    private async Task<UserProfile> GetOrCreateAsync(Guid userId, CancellationToken cancellationToken)
    {
        UserProfile? profile = await db.Profiles.SingleOrDefaultAsync(p => p.Id == userId, cancellationToken);
        if (profile is not null)
        {
            return profile;
        }

        profile = UserProfile.CreateDefault(userId, timeProvider.GetUtcNow());
        db.Profiles.Add(profile);
        await db.SaveChangesAsync(cancellationToken);
        return profile;
    }

    private MeResponse ToResponse(UserProfile profile) => new(
        profile.Id,
        currentUser.Email ?? string.Empty,
        currentUser.Roles,
        profile.DisplayName,
        profile.TimeZone,
        profile.Plan,
        new LearningSettingsDto(profile.InterfaceLanguage, profile.Goals, profile.DailyNewWords, profile.DailyReviews, profile.AutoAddWords));
}

/// <summary>Создаёт профиль с настройками по умолчанию сразу после регистрации.</summary>
public sealed class CreateProfileOnUserRegistered(IUsersDb db, TimeProvider timeProvider) : IIntegrationEventHandler<UserRegistered>
{
    public async Task HandleAsync(UserRegistered integrationEvent, CancellationToken cancellationToken)
    {
        if (await db.Profiles.AnyAsync(p => p.Id == integrationEvent.UserId, cancellationToken))
        {
            return;
        }

        db.Profiles.Add(UserProfile.CreateDefault(integrationEvent.UserId, timeProvider.GetUtcNow()));
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class UserLearningSettingsQuery(IUsersDb db) : IUserLearningSettings
{
    public async Task<UserLearningSettings> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        UserProfile? profile = await db.Profiles.AsNoTracking().SingleOrDefaultAsync(p => p.Id == userId, cancellationToken);
        UserLearningSettings? settings = profile is null
            ? null
            : new UserLearningSettings(profile.Id, profile.DailyNewWords, profile.DailyReviews, profile.TimeZone)
            {
                Goals = profile.Goals.Select(g => g.ToString()).ToList(),
                AutoAddWords = profile.AutoAddWords,
            };

        UserProfile defaults = UserProfile.CreateDefault(userId, DateTimeOffset.UnixEpoch);
        return settings ?? new UserLearningSettings(userId, defaults.DailyNewWords, defaults.DailyReviews, defaults.TimeZone);
    }
}
