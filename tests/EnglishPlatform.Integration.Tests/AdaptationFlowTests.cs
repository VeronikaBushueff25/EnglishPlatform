using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace EnglishPlatform.Integration.Tests;

/// <summary>Этап 5: ошибки считаются, проблемное слово попадает в «работу над ошибками» и начинается с разминки.</summary>
public sealed class AdaptationFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Repeated_mistakes_make_a_problem_word_with_warm_up_and_weak_spots()
    {
        factory.SkipIfUnavailable();
        HttpClient client = await UserClientAsync();

        var translations = new Dictionary<string, string>();
        foreach (string text in new[] { "journey", "salary" })
        {
            JsonElement lexeme = (await GetJsonAsync(client, "/api/v1/lexemes?search=" + text)).GetProperty("items")
                .EnumerateArray().First(i => i.GetProperty("text").GetString() == text);
            JsonElement added = await PostJsonAsync(client, "/api/v1/my-words", new { lexemeId = lexeme.GetProperty("id").GetGuid() });
            translations[text] = string.Join(", ", added.GetProperty("translations").EnumerateArray().Select(t => t.GetString()).Take(2));
        }

        // Занятие: «journey» всё время отвечаем неверно, «salary» — верно.
        Guid sessionId = (await PostJsonAsync(client, "/api/v1/sessions", new { })).GetProperty("id").GetGuid();
        for (int i = 0; i < 20; i++)
        {
            JsonElement step = await GetJsonAsync(client, $"/api/v1/sessions/{sessionId}/next");
            if (step.GetProperty("finished").GetBoolean())
            {
                break;
            }

            JsonElement task = step.GetProperty("task");
            string kind = task.GetProperty("kind").GetString()!;
            string word = kind == "ListenChoose" ? task.GetProperty("audioText").GetString()! : task.GetProperty("prompt").GetString()!;
            string answer = kind switch
            {
                "Introduce" => "",
                _ when word == "journey" => task.GetProperty("options").EnumerateArray().Select(o => o.GetString()!).First(o => o != translations["journey"]),
                _ => translations[word],
            };
            await AnswerAsync(client, sessionId, task, answer);
        }

        JsonElement weak = await GetJsonAsync(client, "/api/v1/stats/weak-spots");
        weak.GetProperty("totalAnswers").GetInt32().ShouldBeGreaterThanOrEqualTo(4);
        weak.GetProperty("problemWords").GetInt32().ShouldBe(1);
        JsonElement wrongWord = weak.GetProperty("tags").EnumerateArray().Single(t => t.GetProperty("code").GetString() == "WrongWord");
        wrongWord.GetProperty("count").GetInt32().ShouldBeGreaterThanOrEqualTo(2);
        wrongWord.GetProperty("title").GetString().ShouldBe("Не то слово");

        (await GetJsonAsync(client, "/api/v1/learning/today")).GetProperty("problemWords").GetInt32().ShouldBe(1);

        // Работа над ошибками: только проблемное слово, сначала разминка, потом обычное задание.
        JsonElement mistakes = await PostJsonAsync(client, "/api/v1/sessions", new { kind = "Mistakes" });
        mistakes.GetProperty("kind").GetString().ShouldBe("Mistakes");
        mistakes.GetProperty("progress").GetProperty("total").GetInt32().ShouldBe(1);
        Guid mistakesId = mistakes.GetProperty("id").GetGuid();

        JsonElement warmUp = (await GetJsonAsync(client, $"/api/v1/sessions/{mistakesId}/next")).GetProperty("task");
        warmUp.GetProperty("isWarmUp").GetBoolean().ShouldBeTrue();
        warmUp.GetProperty("isProblem").GetBoolean().ShouldBeTrue();
        warmUp.GetProperty("kind").GetString().ShouldBeOneOf("Introduce", "ChooseWord");
        string warmAnswer = warmUp.GetProperty("kind").GetString() == "Introduce" ? "" : "journey";
        JsonElement warmResult = await AnswerAsync(client, mistakesId, warmUp, warmAnswer);
        warmResult.GetProperty("isWarmUp").GetBoolean().ShouldBeTrue();
        warmResult.GetProperty("progress").GetProperty("done").GetInt32().ShouldBe(0);

        JsonElement real = (await GetJsonAsync(client, $"/api/v1/sessions/{mistakesId}/next")).GetProperty("task");
        real.GetProperty("isWarmUp").GetBoolean().ShouldBeFalse();
        real.GetProperty("isProblem").GetBoolean().ShouldBeTrue();
        real.GetProperty("kind").GetString().ShouldNotBe("Introduce");
    }

    [Fact]
    public async Task Grammar_topics_and_error_tags_are_seeded_and_admin_can_edit()
    {
        factory.SkipIfUnavailable();
        HttpClient client = await UserClientAsync();

        JsonElement topics = await GetJsonAsync(client, "/api/v1/grammar/topics");
        topics.GetArrayLength().ShouldBeGreaterThanOrEqualTo(6);
        JsonElement articles = await GetJsonAsync(client, "/api/v1/grammar/topics/articles");
        articles.GetProperty("examples").GetArrayLength().ShouldBeGreaterThan(2);
        articles.GetProperty("errorTags").EnumerateArray().Select(t => t.GetString()).ShouldContain("Article");

        JsonElement tags = await GetJsonAsync(client, "/api/v1/grammar/error-tags");
        tags.EnumerateArray().Single(t => t.GetProperty("code").GetString() == "Preposition").GetProperty("topicSlug").GetString().ShouldBe("prepositions");

        (await client.PostAsJsonAsync("/api/v1/admin/grammar-topics", new { }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        HttpClient admin = await AdminClientAsync();
        string slug = $"t-{Guid.NewGuid():N}"[..12];
        JsonElement created = await PostJsonAsync(admin, "/api/v1/admin/grammar-topics", new
        {
            slug,
            title = "Тест",
            level = "A2",
            summary = "Коротко",
            body = "Подробно",
            examples = new[] { new { text = "Example.", translation = "Пример.", isWrong = false } },
            sortOrder = 99,
        });
        Guid id = created.GetProperty("id").GetGuid();
        (await admin.DeleteAsync($"/api/v1/admin/grammar-topics/{id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static async Task<JsonElement> AnswerAsync(HttpClient client, Guid sessionId, JsonElement task, string answer) =>
        await PostJsonAsync(client, $"/api/v1/sessions/{sessionId}/answers", new
        {
            attemptId = Guid.NewGuid(),
            taskId = task.GetProperty("taskId").GetGuid(),
            answer,
            usedHint = false,
            elapsedMs = 4000,
        });

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
        string email = $"adapt-{Guid.NewGuid():N}@example.com";
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
