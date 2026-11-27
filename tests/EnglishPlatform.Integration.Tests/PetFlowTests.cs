using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace EnglishPlatform.Integration.Tests;

/// <summary>Питомец: завести, покормить, погладить; занятие приносит угощения и опыт.</summary>
public sealed class PetFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Adopt_feed_pat_and_lesson_brings_treats()
    {
        factory.SkipIfUnavailable();
        HttpClient client = await UserClientAsync();

        (await GetJsonAsync(client, "/api/v1/pet")).GetProperty("pet").ValueKind.ShouldBe(JsonValueKind.Null);

        (await client.PostAsJsonAsync("/api/v1/pet", new { species = "Cat", palette = "purple", name = "Пушок" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        JsonElement pet = await PostJsonAsync(client, "/api/v1/pet", new { species = "Cat", palette = "ginger", name = "  Пушок " });
        pet.GetProperty("name").GetString().ShouldBe("Пушок");
        pet.GetProperty("treats").GetInt32().ShouldBe(3);
        pet.GetProperty("level").GetInt32().ShouldBe(1);
        pet.GetProperty("mood").GetString().ShouldBe("Happy");

        (await client.PostAsJsonAsync("/api/v1/pet", new { species = "Corgi", palette = "red", name = "Бублик" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        JsonElement fed = await PostJsonAsync(client, "/api/v1/pet/feed", new { });
        fed.GetProperty("treats").GetInt32().ShouldBe(2);
        fed.GetProperty("satiety").GetInt32().ShouldBeGreaterThan(pet.GetProperty("satiety").GetInt32());

        JsonElement patted = await PostJsonAsync(client, "/api/v1/pet/pat", new { });
        patted.GetProperty("patsLeft").GetInt32().ShouldBe(7);

        // Аксессуар не открыт на первом уровне.
        (await client.PutAsJsonAsync("/api/v1/pet", new { palette = "tabby", name = "Пушок", accessory = "crown" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        JsonElement restyled = await PutJsonAsync(client, "/api/v1/pet", new { palette = "tabby", name = "Барсик", accessory = (string?)null });
        restyled.GetProperty("palette").GetString().ShouldBe("tabby");
        restyled.GetProperty("name").GetString().ShouldBe("Барсик");
        restyled.GetProperty("quests").GetArrayLength().ShouldBe(3);

        // Вопрос питомца и игра из просьб дня.
        JsonElement quiz = await PostJsonAsync(client, "/api/v1/pet/quiz", new { correct = true });
        quiz.GetProperty("pet").GetProperty("quizCorrect").GetInt32().ShouldBe(1);
        string game = restyled.GetProperty("quests")[1].GetProperty("kind").GetString()!;
        JsonElement played = await PostJsonAsync(client, "/api/v1/pet/activity", new { activity = game });
        played.GetProperty("completed").GetArrayLength().ShouldBe(1);
        played.GetProperty("pet").GetProperty("treats").GetInt32().ShouldBe(4);
        (await client.PostAsJsonAsync("/api/v1/pet/activity", new { activity = "Lesson" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Занятие: угощения, опыт, пройденное занятие.
        foreach (string text in new[] { "passport", "luggage", "receipt" })
        {
            JsonElement lexeme = (await GetJsonAsync(client, "/api/v1/lexemes?search=" + text)).GetProperty("items")
                .EnumerateArray().First(i => i.GetProperty("text").GetString() == text);
            await PostJsonAsync(client, "/api/v1/my-words", new { lexemeId = lexeme.GetProperty("id").GetGuid() });
        }

        Guid sessionId = (await PostJsonAsync(client, "/api/v1/sessions", new { })).GetProperty("id").GetGuid();
        JsonElement step = await GetJsonAsync(client, $"/api/v1/sessions/{sessionId}/next");
        for (int i = 0; i < 4 && !step.GetProperty("finished").GetBoolean(); i++)
        {
            // Меньше 5 верных — ровно 2 угощения.
            await PostJsonAsync(client, $"/api/v1/sessions/{sessionId}/answers", new
            {
                attemptId = Guid.NewGuid(),
                taskId = step.GetProperty("task").GetProperty("taskId").GetGuid(),
                answer = "x",
                usedHint = false,
                elapsedMs = 3000,
            });
            step = await GetJsonAsync(client, $"/api/v1/sessions/{sessionId}/next");
        }
        await PostJsonAsync(client, $"/api/v1/sessions/{sessionId}/finish", new { });
        await PostJsonAsync(client, $"/api/v1/sessions/{sessionId}/finish", new { }); // повторное завершение не кормит второй раз

        JsonElement after = (await GetJsonAsync(client, "/api/v1/pet")).GetProperty("pet");
        after.GetProperty("treats").GetInt32().ShouldBe(7); // 4 + 2 за занятие + 1 за просьбу «пройди занятие»
        after.GetProperty("streak").GetInt32().ShouldBe(1);
        after.GetProperty("wordsAdded").GetInt32().ShouldBe(3); // три слова добавлены вручную
        after.GetProperty("studiedToday").GetBoolean().ShouldBeTrue();
        after.GetProperty("lessonsCompleted").GetInt32().ShouldBe(1);
        after.GetProperty("xp").GetInt32().ShouldBeGreaterThan(restyled.GetProperty("xp").GetInt32());
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

    private static async Task<JsonElement> PutJsonAsync(HttpClient client, string url, object body)
    {
        HttpResponseMessage response = await client.PutAsJsonAsync(url, body, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"PUT {url}: {await response.Content.ReadAsStringAsync(Ct)}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private async Task<HttpClient> UserClientAsync()
    {
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        string email = $"pet-{Guid.NewGuid():N}@example.com";
        JsonElement auth = await (await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "secret123" }, Ct))
            .Content.ReadFromJsonAsync<JsonElement>(Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return client;
    }
}
