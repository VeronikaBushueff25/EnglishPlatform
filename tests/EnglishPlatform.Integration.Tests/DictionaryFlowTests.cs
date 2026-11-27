using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace EnglishPlatform.Integration.Tests;

/// <summary>Внешний словарь: слово попадает в общую базу при первом поиске; «Улучшить мои слова» предлагает правки.</summary>
public sealed class DictionaryFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Unknown_word_is_imported_once_and_then_found_in_base()
    {
        factory.SkipIfUnavailable();
        HttpClient client = await UserClientAsync();
        FakeDictionarySource fake = factory.Services.GetRequiredService<FakeDictionarySource>();
        int before = fake.Calls.GetValueOrDefault("serendipity");

        JsonElement first = await GetJsonAsync(client, "/api/v1/dictionary/lookup?text=Serendipity!");
        JsonElement item = first.GetProperty("items")[0];
        item.GetProperty("text").GetString().ShouldBe("serendipity");
        item.GetProperty("transcription").GetString().ShouldBe("ˌserənˈdɪpəti");
        item.GetProperty("translations")[0].GetProperty("text").GetString().ShouldBe("счастливая случайность");
        item.GetProperty("level").GetString().ShouldBe("C1"); // нет в списках CEFR — редкая лексика
        item.GetProperty("examples")[0].GetProperty("translation").GetString().ShouldBe("Встреча с ней была счастливой случайностью.");

        JsonElement second = await GetJsonAsync(client, "/api/v1/dictionary/lookup?text=serendipity");
        second.GetProperty("fromDictionary").GetBoolean().ShouldBeFalse();
        second.GetProperty("items")[0].GetProperty("id").GetGuid().ShouldBe(item.GetProperty("id").GetGuid());
        fake.Calls.GetValueOrDefault("serendipity").ShouldBeLessThanOrEqualTo(before + 1);

        // Слово теперь в общей базе — его можно найти обычным поиском и добавить себе.
        JsonElement search = await GetJsonAsync(client, "/api/v1/lexemes?search=serendipity");
        search.GetProperty("totalCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Word_form_leads_to_lemma_and_parts_of_speech_merge_into_one_entry()
    {
        factory.SkipIfUnavailable();
        HttpClient client = await UserClientAsync();

        JsonElement overslept = await GetJsonAsync(client, "/api/v1/dictionary/lookup?text=overslept");
        overslept.GetProperty("formOf").GetString().ShouldBe("overslept");
        overslept.GetProperty("items")[0].GetProperty("text").GetString().ShouldBe("oversleep");

        // Две части речи — одна запись: переводы обеих вместе; слова нет в списках CEFR — C1.
        JsonElement quixotic = await GetJsonAsync(client, "/api/v1/dictionary/lookup?text=quixotic");
        quixotic.GetProperty("items").GetArrayLength().ShouldBe(1);
        JsonElement entry = quixotic.GetProperty("items")[0];
        entry.GetProperty("level").GetString().ShouldBe("C1");
        entry.GetProperty("partOfSpeech").GetString().ShouldBe("Adjective");
        entry.GetProperty("translations").EnumerateArray().Select(t => t.GetProperty("text").GetString())
            .ShouldBe(["донкихотский", "мечтательный", "мечтатель"]);
    }

    [Fact]
    public async Task Suggestions_fix_level_type_form_and_fill_gaps()
    {
        factory.SkipIfUnavailable();
        HttpClient client = await UserClientAsync();
        await PostJsonAsync(client, "/api/v1/my-words", new { text = "overslept", translations = new[] { "проспал" }, level = "B1", type = "Word" });
        await PostJsonAsync(client, "/api/v1/my-words", new { text = "get over", translations = new[] { "пережить" }, level = "B1", type = "Phrase" });
        await PostJsonAsync(client, "/api/v1/my-words", new { text = "mostly", translations = new[] { "в основном" }, level = "B1", type = "Word" });

        JsonElement page = await GetJsonAsync(client, "/api/v1/my-words/suggestions?offset=0&limit=25");
        page.GetProperty("total").GetInt32().ShouldBe(3);
        page.GetProperty("checked").GetInt32().ShouldBe(3);
        Dictionary<string, JsonElement> byText = page.GetProperty("items").EnumerateArray().ToDictionary(i => i.GetProperty("text").GetString()!);

        byText["overslept"].GetProperty("suggestedText").GetString().ShouldBe("oversleep");
        byText["overslept"].GetProperty("suggestedTranslations")[0].GetString().ShouldBe("проспать");
        byText["overslept"].GetProperty("suggestedTranscription").GetString().ShouldBe("ˌəʊvəˈsliːp");
        byText["get over"].GetProperty("suggestedType").GetString().ShouldBe("PhrasalVerb");
        byText["mostly"].GetProperty("suggestedLevel").GetString().ShouldBe("A2");

        var items = page.GetProperty("items").EnumerateArray().Select(i => new
        {
            userWordId = i.GetProperty("userWordId").GetGuid(),
            text = i.GetProperty("suggestedText").GetString(),
            type = i.GetProperty("suggestedType").GetString(),
            level = i.GetProperty("suggestedLevel").GetString(),
            transcription = i.GetProperty("suggestedTranscription").GetString(),
            partOfSpeech = i.GetProperty("suggestedPartOfSpeech").GetString(),
            example = i.GetProperty("suggestedExample").GetString(),
            translations = i.TryGetProperty("suggestedTranslations", out JsonElement t) && t.ValueKind == JsonValueKind.Array
                ? t.EnumerateArray().Select(x => x.GetString()).ToArray()
                : null,
        }).ToArray();
        JsonElement applied = await PostJsonAsync(client, "/api/v1/my-words/suggestions/apply", new { items });
        applied.GetProperty("updated").GetInt32().ShouldBe(3);

        JsonElement mine = await GetJsonAsync(client, "/api/v1/my-words?pageSize=50");
        Dictionary<string, JsonElement> words = mine.GetProperty("items").EnumerateArray().ToDictionary(i => i.GetProperty("text").GetString()!);
        words.ShouldContainKey("oversleep");
        words["oversleep"].GetProperty("translations")[0].GetString().ShouldBe("проспать");
        words["get over"].GetProperty("type").GetString().ShouldBe("PhrasalVerb");
        words["mostly"].GetProperty("level").GetString().ShouldBe("A2");

        // Повторная проверка — исправлять больше нечего, кроме того, чего нет в словаре.
        JsonElement again = await GetJsonAsync(client, "/api/v1/my-words/suggestions");
        again.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("text").GetString()).ShouldNotContain("mostly");
    }

    [Fact]
    public async Task Word_added_from_text_is_complete_and_has_progress()
    {
        factory.SkipIfUnavailable();
        HttpClient client = await UserClientAsync();

        Guid lexemeId = (await GetJsonAsync(client, "/api/v1/dictionary/lookup?text=serendipity")).GetProperty("items")[0].GetProperty("id").GetGuid();
        JsonElement word = await PostJsonAsync(client, "/api/v1/my-words", new { lexemeId });
        word.GetProperty("level").GetString().ShouldBe("C1");
        word.GetProperty("transcription").GetString().ShouldNotBeNullOrEmpty();
        word.GetProperty("partOfSpeech").GetString().ShouldBe("Noun");
        word.GetProperty("example").GetString().ShouldBe("Meeting her was pure serendipity.");
        word.GetProperty("exampleTranslation").GetString().ShouldBe("Встреча с ней была счастливой случайностью.");

        JsonElement progress = await GetJsonAsync(client, "/api/v1/my-words/progress");
        JsonElement mine = progress.EnumerateArray().Single(p => p.GetProperty("userWordId").GetGuid() == word.GetProperty("id").GetGuid());
        mine.GetProperty("stage").GetString().ShouldBe("New");
        mine.GetProperty("reviews").GetInt32().ShouldBe(0);
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
        response.IsSuccessStatusCode.ShouldBeTrue($"POST {url}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(Ct)}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private async Task<HttpClient> UserClientAsync()
    {
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        string email = $"dict-{Guid.NewGuid():N}@example.com";
        JsonElement auth = await (await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "secret123" }, Ct))
            .Content.ReadFromJsonAsync<JsonElement>(Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return client;
    }
}
