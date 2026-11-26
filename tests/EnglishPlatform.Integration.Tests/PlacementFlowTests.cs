using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace EnglishPlatform.Integration.Tests;

/// <summary>Этап 6: адаптивный тест уровня, уровни по навыкам, стартовый набор слов и учебный план.</summary>
public sealed class PlacementFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly string[] Levels = ["A1", "A2", "B1", "B2", "C1", "C2"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Placement_finds_b1_adds_starter_words_and_builds_a_plan()
    {
        factory.SkipIfUnavailable();
        HttpClient admin = await AdminClientAsync();
        HttpClient user = await UserClientAsync();

        // Ответы знает только админка: правильный вариант и уровень каждого вопроса.
        JsonElement bank = await GetJsonAsync(admin, "/api/v1/admin/placement-questions?pageSize=100");
        Dictionary<Guid, (string Correct, int Level)> answers = bank.GetProperty("items").EnumerateArray().ToDictionary(
            q => q.GetProperty("id").GetGuid(),
            q => (q.GetProperty("options")[q.GetProperty("correctOption").GetInt32()].GetString()!, Array.IndexOf(Levels, q.GetProperty("level").GetString())));

        (await user.PutAsJsonAsync("/api/v1/me/settings", new { interfaceLanguage = "Ru", goals = new[] { "Travel" }, dailyNewWords = 10, dailyReviews = 100 }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetJsonAsync(user, "/api/v1/me/plan")).GetProperty("hasPlacement").GetBoolean().ShouldBeFalse();

        JsonElement step = await PostJsonAsync(user, "/api/v1/placement/start", new { });
        Guid attemptId = step.GetProperty("attemptId").GetGuid();
        int total = step.GetProperty("progress").GetProperty("total").GetInt32();
        total.ShouldBe(18);

        // Повторный старт продолжает тот же тест.
        (await PostJsonAsync(user, "/api/v1/placement/start", new { })).GetProperty("attemptId").GetGuid().ShouldBe(attemptId);

        bool repeated = false;
        while (!step.GetProperty("finished").GetBoolean())
        {
            JsonElement question = step.GetProperty("question");
            Guid questionId = question.GetProperty("questionId").GetGuid();
            (string correct, int level) = answers[questionId];
            List<string> options = question.GetProperty("options").EnumerateArray().Select(o => o.GetString()!).ToList();
            int option = level <= 2 ? options.IndexOf(correct) : options.FindIndex(o => o != correct);

            step = await PostJsonAsync(user, $"/api/v1/placement/{attemptId}/answers", new { questionId, option });
            if (!repeated)
            {
                // Обрыв связи: тот же ответ ещё раз не засчитывается второй раз.
                JsonElement again = await PostJsonAsync(user, $"/api/v1/placement/{attemptId}/answers", new { questionId, option });
                again.GetProperty("progress").GetProperty("answered").GetInt32().ShouldBe(1);
                repeated = true;
            }
        }

        step.GetProperty("progress").GetProperty("answered").GetInt32().ShouldBe(total);

        int wordsBefore = (await GetJsonAsync(user, "/api/v1/my-words")).GetProperty("totalCount").GetInt32();
        JsonElement result = await PostJsonAsync(user, $"/api/v1/placement/{attemptId}/finish", new { });
        result.GetProperty("overall").GetString().ShouldBe("B1");
        result.GetProperty("levels").EnumerateArray().Select(l => l.GetProperty("level").GetString()).ShouldAllBe(l => l == "B1");
        result.GetProperty("levels").GetArrayLength().ShouldBe(3);
        int added = result.GetProperty("starterWordsAdded").GetInt32();
        added.ShouldBeInRange(1, 20);

        JsonElement mine = await GetJsonAsync(user, "/api/v1/my-words?pageSize=100");
        mine.GetProperty("totalCount").GetInt32().ShouldBe(wordsBefore + added);
        mine.GetProperty("items").EnumerateArray().Select(w => w.GetProperty("level").GetString()).ShouldAllBe(l => l == "B1" || l == "B2");

        JsonElement plan = await GetJsonAsync(user, "/api/v1/me/plan");
        plan.GetProperty("hasPlacement").GetBoolean().ShouldBeTrue();
        plan.GetProperty("overall").GetString().ShouldBe("B1");
        plan.GetProperty("wordLevels").EnumerateArray().Select(l => l.GetString()).ShouldBe(["B1", "B2"]);
        plan.GetProperty("grammar").GetArrayLength().ShouldBeInRange(1, 4);
        plan.GetProperty("goals").EnumerateArray().Select(g => g.GetString()).ShouldBe(["Travel"]);

        // Повторный тест уровни обновляет, но слов больше не добавляет.
        JsonElement retake = await PostJsonAsync(user, "/api/v1/placement/start", new { });
        retake.GetProperty("attemptId").GetGuid().ShouldNotBe(attemptId);
        JsonElement retakeResult = await PostJsonAsync(user, $"/api/v1/placement/{retake.GetProperty("attemptId").GetGuid()}/finish", new { });
        retakeResult.GetProperty("starterWordsAdded").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Admin_manages_question_bank_and_users_cannot()
    {
        factory.SkipIfUnavailable();
        HttpClient user = await UserClientAsync();
        (await user.GetAsync("/api/v1/admin/placement-questions", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        HttpClient admin = await AdminClientAsync();
        HttpResponseMessage invalid = await admin.PostAsJsonAsync(
            "/api/v1/admin/placement-questions",
            new { skill = "Reading", level = "B1", prompt = "Q?", passage = (string?)null, options = new[] { "a", "a" }, correctOption = 5 },
            Ct);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        JsonElement errors = (await invalid.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        errors.TryGetProperty("passage", out _).ShouldBeTrue();
        errors.TryGetProperty("correctOption", out _).ShouldBeTrue();

        JsonElement created = await PostJsonAsync(admin, "/api/v1/admin/placement-questions", new
        {
            skill = "Grammar",
            level = "A1",
            prompt = "They ___ students.",
            passage = (string?)null,
            options = new[] { "are", "is", "am" },
            correctOption = 0,
            isActive = false,
        });
        Guid id = created.GetProperty("id").GetGuid();
        (await GetJsonAsync(admin, $"/api/v1/admin/placement-questions/{id}")).GetProperty("isActive").GetBoolean().ShouldBeFalse();
        (await admin.DeleteAsync($"/api/v1/admin/placement-questions/{id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
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

    private HttpClient NewClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });

    private async Task<HttpClient> UserClientAsync()
    {
        HttpClient client = NewClient();
        string email = $"placement-{Guid.NewGuid():N}@example.com";
        JsonElement auth = await (await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "secret123" }, Ct))
            .Content.ReadFromJsonAsync<JsonElement>(Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return client;
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        HttpClient client = NewClient();
        JsonElement auth = await (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword }, Ct))
            .Content.ReadFromJsonAsync<JsonElement>(Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return client;
    }
}
