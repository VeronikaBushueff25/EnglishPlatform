using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace EnglishPlatform.Integration.Tests;

/// <summary>Сквозной сценарий этапа 2: регистрация, профиль, обновление токена, выход.</summary>
public sealed class AuthFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Register_then_profile_refresh_and_logout()
    {
        factory.SkipIfUnavailable();
        HttpClient client = CreateClient();
        string email = $"user-{Guid.NewGuid():N}@example.com";

        HttpResponseMessage registered = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "secret123" }, Ct);
        registered.StatusCode.ShouldBe(HttpStatusCode.OK);
        AuthResponse auth = (await registered.Content.ReadFromJsonAsync<AuthResponse>(Json, Ct))!;
        auth.User.Roles.ShouldContain("user");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        JsonElement me = await client.GetFromJsonAsync<JsonElement>("/api/v1/me", Ct);
        me.GetProperty("email").GetString().ShouldBe(email);
        me.GetProperty("settings").GetProperty("dailyNewWords").GetInt32().ShouldBe(10);

        HttpResponseMessage settings = await client.PutAsJsonAsync(
            "/api/v1/me/settings",
            new { interfaceLanguage = "En", goals = new[] { "Work" }, dailyNewWords = 15, dailyReviews = 150 },
            Ct);
        settings.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await client.PostAsync("/api/v1/auth/refresh", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsync("/api/v1/auth/logout", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.PostAsync("/api/v1/auth/refresh", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Duplicate_email_is_conflict_and_wrong_password_is_unauthorized()
    {
        factory.SkipIfUnavailable();
        HttpClient client = CreateClient();
        string email = $"dup-{Guid.NewGuid():N}@example.com";

        (await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "secret123" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "secret123" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "wrong-pass1" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Invalid_registration_returns_validation_problem()
    {
        factory.SkipIfUnavailable();
        HttpClient client = CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/register", new { email = "not-an-email", password = "123" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("errors").TryGetProperty("email", out _).ShouldBeTrue();
        problem.GetProperty("errors").TryGetProperty("password", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Profile_requires_authentication_and_admin_area_requires_admin()
    {
        factory.SkipIfUnavailable();
        HttpClient client = CreateClient();

        (await client.GetAsync("/api/v1/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        string email = $"plain-{Guid.NewGuid():N}@example.com";
        AuthResponse auth = (await (await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "secret123" }, Ct))
            .Content.ReadFromJsonAsync<AuthResponse>(Json, Ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        (await client.GetAsync("/api/v1/admin/users", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // Refresh-cookie помечена Secure, поэтому клиент ходит по https.
    private HttpClient CreateClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });

    private sealed record AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, AuthUser User);

    private sealed record AuthUser(Guid Id, string Email, string[] Roles);
}
