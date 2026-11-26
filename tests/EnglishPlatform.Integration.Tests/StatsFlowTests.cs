using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace EnglishPlatform.Integration.Tests;

/// <summary>Этап 7: сводка статистики после занятия.</summary>
public sealed class StatsFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Summary_reflects_a_finished_session()
    {
        factory.SkipIfUnavailable();
        HttpClient client = await UserClientAsync();

        JsonElement empty = await GetJsonAsync(client, "/api/v1/stats/summary");
        empty.GetProperty("totalWords").GetInt32().ShouldBe(0);
        empty.GetProperty("currentStreak").GetInt32().ShouldBe(0);
        empty.GetProperty("days").GetArrayLength().ShouldBe(30);

        var translations = new Dictionary<string, string>();
        foreach (string text in new[] { "passport", "luggage", "receipt" })
        {
            JsonElement lexeme = (await GetJsonAsync(client, "/api/v1/lexemes?search=" + text)).GetProperty("items")
                .EnumerateArray().First(i => i.GetProperty("text").GetString() == text);
            JsonElement added = await PostJsonAsync(client, "/api/v1/my-words", new { lexemeId = lexeme.GetProperty("id").GetGuid() });
            translations[text] = string.Join(", ", added.GetProperty("translations").EnumerateArray().Select(t => t.GetString()).Take(2));
        }

        Guid sessionId = (await PostJsonAsync(client, "/api/v1/sessions", new { })).GetProperty("id").GetGuid();
        for (int i = 0; i < 30; i++)
        {
            JsonElement step = await GetJsonAsync(client, $"/api/v1/sessions/{sessionId}/next");
            if (step.GetProperty("finished").GetBoolean())
            {
                break;
            }

            JsonElement task = step.GetProperty("task");
            string kind = task.GetProperty("kind").GetString()!;
            string word = kind == "ListenChoose" ? task.GetProperty("audioText").GetString()! : task.GetProperty("prompt").GetString()!;
            string answer = kind == "Introduce" ? "" : translations[word];
            await PostJsonAsync(client, $"/api/v1/sessions/{sessionId}/answers", new
            {
                attemptId = Guid.NewGuid(),
                taskId = task.GetProperty("taskId").GetGuid(),
                answer,
                usedHint = false,
                elapsedMs = 3000,
            });
        }

        JsonElement summary = await GetJsonAsync(client, "/api/v1/stats/summary");
        summary.GetProperty("totalWords").GetInt32().ShouldBe(3);
        summary.GetProperty("notStarted").GetInt32().ShouldBe(0);
        (summary.GetProperty("learning").GetInt32() + summary.GetProperty("passive").GetInt32() + summary.GetProperty("active").GetInt32()).ShouldBe(3);
        summary.GetProperty("passive").GetInt32().ShouldBe(3);
        summary.GetProperty("studiedToday").GetBoolean().ShouldBeTrue();
        summary.GetProperty("currentStreak").GetInt32().ShouldBe(1);
        summary.GetProperty("reviewsToday").GetInt32().ShouldBe(6);
        summary.GetProperty("newWordsWeek").GetInt32().ShouldBe(3);
        summary.GetProperty("accuracyWeek").GetDouble().ShouldBe(1);
        JsonElement lastDay = summary.GetProperty("days")[29];
        lastDay.GetProperty("reviews").GetInt32().ShouldBe(6);
        lastDay.GetProperty("successful").GetInt32().ShouldBe(6);
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
        string email = $"stats-{Guid.NewGuid():N}@example.com";
        JsonElement auth = await (await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "secret123" }, Ct))
            .Content.ReadFromJsonAsync<JsonElement>(Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return client;
    }
}
