using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace EnglishPlatform.Integration.Tests;

/// <summary>Чтение: библиотека текстов, знакомые слова, слова для игр, управление текстами в админке.</summary>
public sealed class ReadingFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Library_has_texts_of_all_levels_and_a_text_opens()
    {
        factory.SkipIfUnavailable();
        HttpClient client = await UserClientAsync();

        JsonElement list = await GetJsonAsync(client, "/api/v1/reading/texts?pageSize=50");
        list.GetProperty("totalCount").GetInt32().ShouldBeGreaterThanOrEqualTo(22);
        list.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("level").GetString()).Distinct().Count().ShouldBe(6);

        JsonElement b1 = await GetJsonAsync(client, "/api/v1/reading/texts?level=B1");
        b1.GetProperty("items").EnumerateArray().ShouldAllBe(t => t.GetProperty("level").GetString() == "B1");

        Guid id = list.GetProperty("items")[0].GetProperty("id").GetGuid();
        JsonElement text = await GetJsonAsync(client, $"/api/v1/reading/texts/{id}");
        text.GetProperty("body").GetString()!.Length.ShouldBeGreaterThan(200);
        text.GetProperty("body").GetString()!.ShouldNotStartWith(" "); // отступы сырой строки сняты
    }

    [Fact]
    public async Task Practice_words_and_known_words_come_from_my_dictionary()
    {
        factory.SkipIfUnavailable();
        HttpClient client = await UserClientAsync();
        foreach (string text in new[] { "harbour", "lantern", "meadow", "pebble" })
        {
            (await client.PostAsJsonAsync("/api/v1/my-words", new { text, translations = new[] { "перевод " + text }, type = "Word" }, Ct)).EnsureSuccessStatusCode();
        }

        JsonElement words = await GetJsonAsync(client, "/api/v1/practice/words?count=10");
        words.GetArrayLength().ShouldBe(4);
        words.EnumerateArray().ShouldAllBe(w => w.GetProperty("translations").GetArrayLength() > 0);

        JsonElement known = await GetJsonAsync(client, "/api/v1/my-words/texts");
        known.EnumerateArray().Select(t => t.GetString()).ShouldBe(["harbour", "lantern", "meadow", "pebble"], ignoreOrder: true);
    }

    [Fact]
    public async Task Admin_hides_text_from_users()
    {
        factory.SkipIfUnavailable();
        HttpClient admin = await AdminClientAsync();
        HttpClient user = await UserClientAsync();

        JsonElement created = await (await admin.PostAsJsonAsync("/api/v1/admin/reading-texts",
                new { title = "Draft story", level = "A2", topic = "Тест", body = "Hello world. This is a draft.", isPublished = false }, Ct))
            .Content.ReadFromJsonAsync<JsonElement>(Ct);
        Guid id = created.GetProperty("id").GetGuid();
        created.GetProperty("wordCount").GetInt32().ShouldBe(6);

        (await user.GetAsync($"/api/v1/reading/texts/{id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await user.PostAsJsonAsync("/api/v1/admin/reading-texts", new { title = "x", level = "A1", body = "y" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await admin.PutAsJsonAsync($"/api/v1/admin/reading-texts/{id}",
            new { title = "Draft story", level = "A2", topic = "Тест", body = "Hello world. This is a draft.", isPublished = true }, Ct)).EnsureSuccessStatusCode();
        (await user.GetAsync($"/api/v1/reading/texts/{id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await admin.DeleteAsync($"/api/v1/admin/reading-texts/{id}", Ct)).IsSuccessStatusCode.ShouldBeTrue();
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string url)
    {
        HttpResponseMessage response = await client.GetAsync(url, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"GET {url}: {await response.Content.ReadAsStringAsync(Ct)}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private async Task<HttpClient> UserClientAsync()
    {
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        string email = $"reading-{Guid.NewGuid():N}@example.com";
        JsonElement auth = await (await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "secret123" }, Ct))
            .Content.ReadFromJsonAsync<JsonElement>(Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return client;
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        JsonElement auth = await (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword }, Ct))
            .Content.ReadFromJsonAsync<JsonElement>(Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return client;
    }
}
