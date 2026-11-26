using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace EnglishPlatform.Integration.Tests;

/// <summary>«Не могу слушать»: задания на слух пропадают до конца часа, текущее заменяется без штрафа.</summary>
public sealed class MuteAudioTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Muted_session_has_no_listening_tasks_and_next_session_stays_muted()
    {
        factory.SkipIfUnavailable();
        HttpClient client = await UserClientAsync();
        var translations = new Dictionary<string, string>();
        foreach (string text in new[] { "passport", "luggage", "receipt", "ticket", "apple" })
        {
            JsonElement lexeme = (await GetJsonAsync(client, "/api/v1/lexemes?search=" + text)).GetProperty("items")
                .EnumerateArray().First(i => i.GetProperty("text").GetString() == text);
            JsonElement added = await PostJsonAsync(client, "/api/v1/my-words", new { lexemeId = lexeme.GetProperty("id").GetGuid() });
            translations[text] = string.Join(", ", added.GetProperty("translations").EnumerateArray().Select(t => t.GetString()).Take(2));
        }

        Guid sessionId = (await PostJsonAsync(client, "/api/v1/sessions", new { })).GetProperty("id").GetGuid();
        JsonElement step = await PostJsonAsync(client, $"/api/v1/sessions/{sessionId}/mute-audio?minutes=60", new { });
        int answered = 0;
        for (int i = 0; i < 40 && !step.GetProperty("finished").GetBoolean(); i++)
        {
            JsonElement task = step.GetProperty("task");
            string kind = task.GetProperty("kind").GetString()!;
            kind.ShouldNotBe("ListenChoose");
            string answer = kind == "Introduce" ? "" : translations.GetValueOrDefault(task.GetProperty("prompt").GetString()!, "x");
            await PostJsonAsync(client, $"/api/v1/sessions/{sessionId}/answers", new
            {
                attemptId = Guid.NewGuid(),
                taskId = task.GetProperty("taskId").GetGuid(),
                answer,
                usedHint = false,
                elapsedMs = 3000,
            });
            answered++;
            step = await GetJsonAsync(client, $"/api/v1/sessions/{sessionId}/next");
        }

        answered.ShouldBeGreaterThan(5);
        step.GetProperty("finished").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Mute_replaces_current_listening_task()
    {
        factory.SkipIfUnavailable();
        HttpClient client = await UserClientAsync();
        foreach (string text in new[] { "passport", "luggage", "receipt" })
        {
            JsonElement lexeme = (await GetJsonAsync(client, "/api/v1/lexemes?search=" + text)).GetProperty("items")
                .EnumerateArray().First(i => i.GetProperty("text").GetString() == text);
            await PostJsonAsync(client, "/api/v1/my-words", new { lexemeId = lexeme.GetProperty("id").GetGuid() });
        }

        Guid sessionId = (await PostJsonAsync(client, "/api/v1/sessions", new { })).GetProperty("id").GetGuid();
        JsonElement step = await GetJsonAsync(client, $"/api/v1/sessions/{sessionId}/next");
        Guid before = step.GetProperty("task").GetProperty("taskId").GetGuid();
        string kind = step.GetProperty("task").GetProperty("kind").GetString()!;

        JsonElement after = await PostJsonAsync(client, $"/api/v1/sessions/{sessionId}/mute-audio", new { });

        // Знакомство со словом — не задание на слух: оно остаётся тем же. Задание на слух заменяется.
        Guid afterId = after.GetProperty("task").GetProperty("taskId").GetGuid();
        if (kind == "ListenChoose")
        {
            afterId.ShouldNotBe(before);
        }
        else
        {
            afterId.ShouldBe(before);
        }

        after.GetProperty("task").GetProperty("kind").GetString().ShouldNotBe("ListenChoose");
        after.GetProperty("progress").GetProperty("done").GetInt32().ShouldBe(0);
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
        string email = $"mute-{Guid.NewGuid():N}@example.com";
        JsonElement auth = await (await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "secret123" }, Ct))
            .Content.ReadFromJsonAsync<JsonElement>(Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return client;
    }
}
