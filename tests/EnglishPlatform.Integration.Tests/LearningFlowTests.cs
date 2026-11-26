using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace EnglishPlatform.Integration.Tests;

/// <summary>Этап 4: занятие целиком — знакомство, задания, проверка, повторная отправка, расписание, итог.</summary>
public sealed class LearningFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Session_introduces_new_words_checks_answers_and_schedules_reviews()
    {
        factory.SkipIfUnavailable();
        HttpClient client = await UserClientAsync();

        // Три слова из общей базы.
        var words = new Dictionary<string, string[]>();
        foreach (string text in new[] { "bread", "ticket", "happy" })
        {
            JsonElement lexeme = (await GetJsonAsync(client, "/api/v1/lexemes?search=" + text)).GetProperty("items")
                .EnumerateArray().First(i => i.GetProperty("text").GetString() == text);
            JsonElement added = await PostJsonAsync(client, "/api/v1/my-words", new { lexemeId = lexeme.GetProperty("id").GetGuid() });
            words[text] = added.GetProperty("translations").EnumerateArray().Select(t => t.GetString()!).ToArray();
        }

        JsonElement today = await GetJsonAsync(client, "/api/v1/learning/today");
        today.GetProperty("newAvailable").GetInt32().ShouldBe(3);
        today.GetProperty("toStudyNow").GetInt32().ShouldBe(3);

        JsonElement session = await PostJsonAsync(client, "/api/v1/sessions", new { });
        Guid sessionId = session.GetProperty("id").GetGuid();
        session.GetProperty("progress").GetProperty("total").GetInt32().ShouldBe(3);

        // Повторный старт в тот же день продолжает то же занятие.
        (await PostJsonAsync(client, "/api/v1/sessions", new { })).GetProperty("id").GetGuid().ShouldBe(sessionId);

        // Первое — знакомство с новым словом.
        JsonElement step = await GetJsonAsync(client, $"/api/v1/sessions/{sessionId}/next");
        JsonElement task = step.GetProperty("task");
        task.GetProperty("kind").GetString().ShouldBe("Introduce");
        string introduced = task.GetProperty("word").GetProperty("text").GetString()!;
        JsonElement seen = await AnswerAsync(client, sessionId, task, Guid.NewGuid(), "");
        seen.GetProperty("verdict").ValueKind.ShouldBe(JsonValueKind.Null);

        // Затем задание по этому же слову; устаревший taskId отклоняется.
        step = await GetJsonAsync(client, $"/api/v1/sessions/{sessionId}/next");
        task = step.GetProperty("task");
        task.GetProperty("kind").GetString().ShouldBeOneOf("ChooseTranslation", "ListenChoose");
        task.TryGetProperty("word", out JsonElement hiddenWord).ShouldBeTrue();
        hiddenWord.ValueKind.ShouldBe(JsonValueKind.Null);

        HttpResponseMessage stale = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/answers",
            new { attemptId = Guid.NewGuid(), taskId = Guid.NewGuid(), answer = "0", usedHint = false, elapsedMs = 3000 },
            Ct);
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        Guid attemptId = Guid.NewGuid();
        string correct = string.Join(", ", words[introduced].Take(2));
        JsonElement result = await AnswerAsync(client, sessionId, task, attemptId, correct);
        result.GetProperty("verdict").GetString().ShouldBe("Correct");
        result.GetProperty("rating").GetInt32().ShouldBe(3);
        DateTimeOffset next = result.GetProperty("nextReviewAt").GetDateTimeOffset();
        (next - DateTimeOffset.UtcNow).TotalMinutes.ShouldBeInRange(5, 11);

        // Шаг заучивания 10 минут — слово вернётся в это же занятие.
        JsonElement progress = result.GetProperty("progress");
        progress.GetProperty("done").GetInt32().ShouldBe(1);
        progress.GetProperty("total").GetInt32().ShouldBe(4);

        // Обрыв связи: тот же attemptId — тот же результат, второй раз не засчитан.
        JsonElement again = await AnswerAsync(client, sessionId, task, attemptId, "совсем другое");
        again.GetProperty("verdict").GetString().ShouldBe("Correct");
        again.GetProperty("progress").GetProperty("done").GetInt32().ShouldBe(1);

        // Проходим занятие до конца, отвечая верно.
        for (int i = 0; i < 40; i++)
        {
            step = await GetJsonAsync(client, $"/api/v1/sessions/{sessionId}/next");
            if (step.GetProperty("finished").GetBoolean())
            {
                break;
            }

            task = step.GetProperty("task");
            string kind = task.GetProperty("kind").GetString()!;
            string answer = kind switch
            {
                "Introduce" => "",
                "ChooseTranslation" => string.Join(", ", words[task.GetProperty("prompt").GetString()!].Take(2)),
                "ListenChoose" => string.Join(", ", words[task.GetProperty("audioText").GetString()!].Take(2)),
                _ => throw new InvalidOperationException($"Неожиданное задание {kind} для новых слов"),
            };
            await AnswerAsync(client, sessionId, task, Guid.NewGuid(), answer);
        }

        step.GetProperty("finished").GetBoolean().ShouldBeTrue();
        JsonElement stats = step.GetProperty("stats");
        stats.GetProperty("newWords").GetInt32().ShouldBe(3);
        stats.GetProperty("wrong").GetInt32().ShouldBe(0);
        stats.GetProperty("answered").GetInt32().ShouldBe(6);

        // Слова выучены до повторения через дни; открылись карточки вспоминания — они уже ждут.
        JsonElement after = await GetJsonAsync(client, "/api/v1/learning/today");
        after.GetProperty("newAvailable").GetInt32().ShouldBe(0);
        after.GetProperty("newDoneToday").GetInt32().ShouldBe(3);
        after.GetProperty("reviewsDue").GetInt32().ShouldBe(3);

        JsonElement recallSession = await PostJsonAsync(client, "/api/v1/sessions", new { });
        recallSession.GetProperty("id").GetGuid().ShouldNotBe(sessionId);
        JsonElement recallTask = (await GetJsonAsync(client, $"/api/v1/sessions/{recallSession.GetProperty("id").GetGuid()}/next")).GetProperty("task");
        recallTask.GetProperty("mode").GetString().ShouldBe("Recall");
        recallTask.GetProperty("kind").GetString().ShouldBeOneOf("TypeWord", "FillGap");

        // Ошибка: слово вернётся в этом же занятии, разбор объясняет правильный ответ.
        JsonElement wrong = await AnswerAsync(client, recallSession.GetProperty("id").GetGuid(), recallTask, Guid.NewGuid(), "qwerty");
        wrong.GetProperty("verdict").GetString().ShouldBe("Wrong");
        wrong.GetProperty("rating").GetInt32().ShouldBe(1);
        wrong.GetProperty("explanation").GetString()!.ShouldContain(wrong.GetProperty("correctAnswer").GetString()!);

        JsonElement history = await GetJsonAsync(client, "/api/v1/sessions");
        history.GetProperty("totalCount").GetInt64().ShouldBe(2);
    }

    [Fact]
    public async Task Empty_dictionary_gives_an_empty_finished_session_and_sessions_are_private()
    {
        factory.SkipIfUnavailable();
        HttpClient client = await UserClientAsync();
        // Автопополнение выключено: проверяем именно пустой словарь.
        (await client.PutAsJsonAsync("/api/v1/me/settings", new { interfaceLanguage = "Ru", goals = Array.Empty<string>(), dailyNewWords = 10, dailyReviews = 100, autoAddWords = false }, Ct))
            .EnsureSuccessStatusCode();

        JsonElement session = await PostJsonAsync(client, "/api/v1/sessions", new { });
        session.GetProperty("finishedAt").ValueKind.ShouldNotBe(JsonValueKind.Null);
        (await GetJsonAsync(client, $"/api/v1/sessions/{session.GetProperty("id").GetGuid()}/next")).GetProperty("finished").GetBoolean().ShouldBeTrue();

        HttpClient stranger = await UserClientAsync();
        (await stranger.GetAsync($"/api/v1/sessions/{session.GetProperty("id").GetGuid()}/next", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<JsonElement> AnswerAsync(HttpClient client, Guid sessionId, JsonElement task, Guid attemptId, string answer) =>
        await PostJsonAsync(client, $"/api/v1/sessions/{sessionId}/answers", new
        {
            attemptId,
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

    private async Task<HttpClient> UserClientAsync()
    {
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        string email = $"learn-{Guid.NewGuid():N}@example.com";
        JsonElement auth = await (await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "secret123" }, Ct))
            .Content.ReadFromJsonAsync<JsonElement>(Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return client;
    }
}
