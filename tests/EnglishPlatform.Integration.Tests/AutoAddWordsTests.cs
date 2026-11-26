using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace EnglishPlatform.Integration.Tests;

/// <summary>Свои новые слова закончились — занятие само добавляет слова своего уровня из общей базы (если не выключено).</summary>
public sealed class AutoAddWordsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Session_adds_words_when_own_are_over()
    {
        factory.SkipIfUnavailable();
        HttpClient client = await UserClientAsync();
        (await client.PutAsJsonAsync("/api/v1/me/settings", new { interfaceLanguage = "Ru", goals = new[] { "Travel" }, dailyNewWords = 5, dailyReviews = 100 }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonElement today = await GetJsonAsync(client, "/api/v1/learning/today");
        today.GetProperty("newAvailable").GetInt32().ShouldBe(0);
        today.GetProperty("autoAddWords").GetInt32().ShouldBe(5);

        JsonElement session = await PostJsonAsync(client, "/api/v1/sessions", new { });
        session.GetProperty("finishedAt").ValueKind.ShouldBe(JsonValueKind.Null);
        session.GetProperty("progress").GetProperty("total").GetInt32().ShouldBeGreaterThan(0);

        JsonElement words = await GetJsonAsync(client, "/api/v1/my-words");
        words.GetProperty("totalCount").GetInt32().ShouldBe(5);
        words.GetProperty("items").EnumerateArray().Select(w => w.GetProperty("level").GetString()).ShouldAllBe(l => l == "A2" || l == "B1");
    }

    [Fact]
    public async Task Nothing_is_added_when_switched_off()
    {
        factory.SkipIfUnavailable();
        HttpClient client = await UserClientAsync();
        JsonElement me = await (await client.PutAsJsonAsync(
                "/api/v1/me/settings", new { interfaceLanguage = "Ru", goals = Array.Empty<string>(), dailyNewWords = 5, dailyReviews = 100, autoAddWords = false }, Ct))
            .Content.ReadFromJsonAsync<JsonElement>(Ct);
        me.GetProperty("settings").GetProperty("autoAddWords").GetBoolean().ShouldBeFalse();

        (await GetJsonAsync(client, "/api/v1/learning/today")).GetProperty("autoAddWords").GetInt32().ShouldBe(0);
        await PostJsonAsync(client, "/api/v1/sessions", new { });
        (await GetJsonAsync(client, "/api/v1/my-words")).GetProperty("totalCount").GetInt32().ShouldBe(0);

        // Если менять другие настройки, не трогая поле, выбор сохраняется.
        JsonElement again = await (await client.PutAsJsonAsync(
                "/api/v1/me/settings", new { interfaceLanguage = "Ru", goals = Array.Empty<string>(), dailyNewWords = 7, dailyReviews = 100 }, Ct))
            .Content.ReadFromJsonAsync<JsonElement>(Ct);
        again.GetProperty("settings").GetProperty("autoAddWords").GetBoolean().ShouldBeFalse();
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string url)
    {
        HttpResponseMessage response = await client.GetAsync(url, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"GET {url}: {await response.Content.ReadAsStringAsync(Ct)}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<JsonElement> PostJsonAsync(HttpClient client, string url, object body)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(url, body, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"POST {url}: {await response.Content.ReadAsStringAsync(Ct)}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private async Task<HttpClient> UserClientAsync()
    {
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        string email = $"auto-{Guid.NewGuid():N}@example.com";
        JsonElement auth = await (await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "secret123" }, Ct))
            .Content.ReadFromJsonAsync<JsonElement>(Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return client;
    }
}
