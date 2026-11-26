using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace EnglishPlatform.Integration.Tests;

/// <summary>Сценарии этапа 3: общая база, личный словарь, импорт CSV, админка лексики.</summary>
public sealed class VocabularyFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Starter_base_is_searchable_by_word_translation_and_filters()
    {
        factory.SkipIfUnavailable();
        HttpClient client = await UserClientAsync();

        JsonElement byWord = await GetJsonAsync(client, "/api/v1/lexemes?search=Apple");
        JsonElement first = byWord.GetProperty("items")[0];
        first.GetProperty("text").GetString().ShouldBe("apple");
        first.GetProperty("translations")[0].GetString().ShouldBe("яблоко");
        first.GetProperty("level").GetString().ShouldBe("A1");

        JsonElement byTranslation = await GetJsonAsync(client, "/api/v1/lexemes?search=" + Uri.EscapeDataString("хлеб"));
        Texts(byTranslation).ShouldContain("bread");

        JsonElement idioms = await GetJsonAsync(client, "/api/v1/lexemes?type=Idiom&pageSize=100");
        idioms.GetProperty("totalCount").GetInt64().ShouldBeGreaterThan(5);
        idioms.GetProperty("items").EnumerateArray().ShouldAllBe(i => i.GetProperty("type").GetString() == "Idiom");

        JsonElement categories = await GetJsonAsync(client, "/api/v1/categories");
        JsonElement food = categories.EnumerateArray().Single(c => c.GetProperty("slug").GetString() == "food");
        food.GetProperty("lexemeCount").GetInt32().ShouldBeGreaterThan(10);

        JsonElement foodWords = await GetJsonAsync(client, $"/api/v1/lexemes?categoryId={food.GetProperty("id").GetGuid()}&level=A1&pageSize=100");
        Texts(foodWords).ShouldContain("apple");
        Texts(foodWords).ShouldNotContain("recipe");

        // Одно слово — две части речи.
        JsonElement book = await GetJsonAsync(client, "/api/v1/lexemes?search=book");
        book.GetProperty("items").EnumerateArray().Count(i => i.GetProperty("text").GetString() == "book").ShouldBe(2);
    }

    [Fact]
    public async Task My_words_add_edit_delete_and_isolation_between_users()
    {
        factory.SkipIfUnavailable();
        HttpClient client = await UserClientAsync();
        Guid appleId = await FindLexemeIdAsync(client, "apple");

        HttpResponseMessage added = await client.PostAsJsonAsync("/api/v1/my-words", new { lexemeId = appleId, note = "из сада" }, Ct);
        added.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonElement word = await added.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Guid wordId = word.GetProperty("id").GetGuid();
        word.GetProperty("translations")[0].GetString().ShouldBe("яблоко");
        word.GetProperty("note").GetString().ShouldBe("из сада");
        word.GetProperty("isEdited").GetBoolean().ShouldBeFalse();

        (await client.PostAsJsonAsync("/api/v1/my-words", new { lexemeId = appleId }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        JsonElement inBase = await GetJsonAsync(client, "/api/v1/lexemes?search=apple");
        inBase.GetProperty("items")[0].GetProperty("myWordId").GetGuid().ShouldBe(wordId);

        HttpResponseMessage custom = await client.PostAsJsonAsync(
            "/api/v1/my-words",
            new { text = "  Serendipity ", translations = new[] { "счастливая случайность" }, example = "Pure serendipity." },
            Ct);
        custom.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonElement customWord = await custom.Content.ReadFromJsonAsync<JsonElement>(Ct);
        customWord.GetProperty("text").GetString().ShouldBe("Serendipity");
        customWord.GetProperty("lexemeId").ValueKind.ShouldBe(JsonValueKind.Null);
        Guid customId = customWord.GetProperty("id").GetGuid();

        (await client.PostAsJsonAsync("/api/v1/my-words", new { text = "serendipity", translations = new[] { "x" } }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        HttpResponseMessage invalid = await client.PostAsJsonAsync("/api/v1/my-words", new { text = "nothing", translations = Array.Empty<string>() }, Ct);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await invalid.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors").TryGetProperty("translations", out _).ShouldBeTrue();

        HttpResponseMessage updated = await client.PutAsJsonAsync(
            $"/api/v1/my-words/{wordId}",
            new { text = "apple", type = "Word", translations = new[] { "яблоко", "яблоня" } },
            Ct);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await updated.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("isEdited").GetBoolean().ShouldBeTrue();

        JsonElement mine = await GetJsonAsync(client, "/api/v1/my-words?search=" + Uri.EscapeDataString("яблон"));
        mine.GetProperty("items").GetArrayLength().ShouldBe(1);

        HttpClient stranger = await UserClientAsync();
        (await stranger.GetAsync($"/api/v1/my-words/{wordId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await stranger.DeleteAsync($"/api/v1/my-words/{wordId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await GetJsonAsync(stranger, "/api/v1/my-words")).GetProperty("totalCount").GetInt64().ShouldBe(0);

        (await client.DeleteAsync($"/api/v1/my-words/{customId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/v1/my-words/{customId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Import_my_words_from_csv_with_russian_headers()
    {
        factory.SkipIfUnavailable();
        HttpClient client = await UserClientAsync();

        const string csv = """
            слово;перевод;пример
            "to be on cloud nine";"быть на седьмом небе";"She was on cloud nine; really."
            bread;;
            qwertyuiop;;
            bread;хлеб;
            ;пусто;
            """;

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/my-words/import", new { content = csv }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonElement result = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        result.GetProperty("added").GetInt32().ShouldBe(2);
        result.GetProperty("skipped").GetInt32().ShouldBe(1);
        result.GetProperty("issues").GetArrayLength().ShouldBe(2);

        JsonElement mine = await GetJsonAsync(client, "/api/v1/my-words");
        JsonElement idiom = mine.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("text").GetString() == "to be on cloud nine");
        idiom.GetProperty("type").GetString().ShouldBe("Phrase");
        idiom.GetProperty("example").GetString().ShouldBe("She was on cloud nine; really.");
        JsonElement bread = mine.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("text").GetString() == "bread");
        bread.GetProperty("lexemeId").ValueKind.ShouldBe(JsonValueKind.String);

        HttpResponseMessage noHeader = await client.PostAsJsonAsync("/api/v1/my-words/import", new { content = "apple;яблоко" }, Ct);
        noHeader.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admin_manages_lexemes_and_categories_and_user_copies_follow()
    {
        factory.SkipIfUnavailable();
        HttpClient user = await UserClientAsync();
        (await user.PostAsJsonAsync("/api/v1/admin/lexemes", new { }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        HttpClient admin = await AdminClientAsync();
        string slug = $"test-{Guid.NewGuid():N}"[..20];
        HttpResponseMessage categoryResponse = await admin.PostAsJsonAsync("/api/v1/admin/categories", new { name = "Тест", slug, description = (string?)null }, Ct);
        categoryResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        Guid categoryId = (await categoryResponse.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
        (await admin.PostAsJsonAsync("/api/v1/admin/categories", new { name = "Ещё", slug }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await admin.PostAsJsonAsync("/api/v1/admin/categories", new { name = "Плохой", slug = "Плохой slug" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        string text = $"zebra{Guid.NewGuid():N}"[..14];
        object lexeme = LexemeBody(text, "зебра", categoryId);
        HttpResponseMessage created = await admin.PostAsJsonAsync("/api/v1/admin/lexemes", lexeme, Ct);
        created.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonElement createdBody = await created.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Guid lexemeId = createdBody.GetProperty("id").GetGuid();
        createdBody.GetProperty("categories")[0].GetProperty("slug").GetString().ShouldBe(slug);
        (await admin.PostAsJsonAsync("/api/v1/admin/lexemes", lexeme, Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        JsonElement userWord = await (await user.PostAsJsonAsync("/api/v1/my-words", new { lexemeId }, Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct);
        Guid userWordId = userWord.GetProperty("id").GetGuid();

        // Правка в общей базе доходит до непоправленной копии в личном словаре.
        (await admin.PutAsJsonAsync($"/api/v1/admin/lexemes/{lexemeId}", LexemeBody(text, "полосатая лошадь", categoryId), Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetJsonAsync(user, $"/api/v1/my-words/{userWordId}")).GetProperty("translations")[0].GetString().ShouldBe("полосатая лошадь");

        // Удаление категории снимает её со слова.
        (await admin.DeleteAsync($"/api/v1/admin/categories/{categoryId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetJsonAsync(admin, $"/api/v1/lexemes/{lexemeId}")).GetProperty("categories").GetArrayLength().ShouldBe(0);

        // Удаление слова из базы оставляет его в личном словаре как своё.
        (await admin.DeleteAsync($"/api/v1/admin/lexemes/{lexemeId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        JsonElement orphan = await GetJsonAsync(user, $"/api/v1/my-words/{userWordId}");
        orphan.GetProperty("lexemeId").ValueKind.ShouldBe(JsonValueKind.Null);
        orphan.GetProperty("text").GetString().ShouldBe(text);

        string csv = $"text,translation,level,part_of_speech,categories\n{text}x,новое,B2,noun,food\napple,яблоко,A1,noun,food\nbad,плохо,Z9,,\n";
        JsonElement import = await (await admin.PostAsJsonAsync("/api/v1/admin/lexemes/import", new { content = csv }, Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct);
        import.GetProperty("added").GetInt32().ShouldBe(1);
        import.GetProperty("skipped").GetInt32().ShouldBe(1);
        import.GetProperty("issues")[0].GetProperty("line").GetInt32().ShouldBe(4);
    }

    [Fact]
    public async Task Imported_labels_become_type_and_level_and_admin_moves_own_words_to_the_base()
    {
        factory.SkipIfUnavailable();
        HttpClient admin = await AdminClientAsync();
        string suffix = Guid.NewGuid().ToString("N")[..6];

        // Свой импорт: метка в заметке, уровень в колонке, фразовый глагол по виду.
        string myCsv = $"слово;перевод;уровень;заметка\nbump{suffix} into;случайно встретить;B1;Фразовый глагол\n" +
                       $"deny{suffix};отрицать;b2;\"Глагол; deny doing\"\nkeep{suffix} going;продолжать;;\n";
        JsonElement mine = await PostJsonAsync(admin, "/api/v1/my-words/import", new { content = myCsv });
        mine.GetProperty("added").GetInt32().ShouldBe(3);

        JsonElement deny = await FindMyWordAsync(admin, $"deny{suffix}");
        deny.GetProperty("level").GetString().ShouldBe("B2");
        deny.GetProperty("partOfSpeech").GetString().ShouldBe("Verb");
        deny.GetProperty("note").GetString().ShouldBe("deny doing");
        (await FindMyWordAsync(admin, $"bump{suffix} into")).GetProperty("type").GetString().ShouldBe("PhrasalVerb");

        // Слова без уровня — и проставление уровня пачкой.
        JsonElement noLevel = await GetJsonAsync(admin, $"/api/v1/my-words?noLevel=true&search=keep{suffix}");
        Guid keepId = noLevel.GetProperty("items")[0].GetProperty("id").GetGuid();
        (await PutJsonAsync(admin, "/api/v1/my-words/bulk", new { ids = new[] { keepId }, level = "A2" })).GetProperty("updated").GetInt32().ShouldBe(1);
        (await GetJsonAsync(admin, $"/api/v1/my-words/{keepId}")).GetProperty("level").GetString().ShouldBe("A2");

        // Перенос своих слов в общую базу: слова остаются в словаре, теперь привязаны к базе.
        JsonElement published = await PostJsonAsync(admin, "/api/v1/admin/lexemes/from-my-words", new { });
        published.GetProperty("added").GetInt32().ShouldBeGreaterThanOrEqualTo(3);
        JsonElement denyAfter = await FindMyWordAsync(admin, $"deny{suffix}");
        Guid denyLexemeId = denyAfter.GetProperty("lexemeId").GetGuid();
        denyAfter.GetProperty("note").GetString().ShouldBe("deny doing");
        JsonElement denyLexeme = await GetJsonAsync(admin, $"/api/v1/lexemes/{denyLexemeId}");
        denyLexeme.GetProperty("level").GetString().ShouldBe("B2");
        denyLexeme.GetProperty("partOfSpeech").GetString().ShouldBe("Verb");

        // Массовый уровень в базе доходит до непоправленной копии.
        (await PutJsonAsync(admin, "/api/v1/admin/lexemes/bulk", new { ids = new[] { denyLexemeId }, level = "C1", addCategoryId = (Guid?)null }))
            .GetProperty("updated").GetInt32().ShouldBe(1);
        (await FindMyWordAsync(admin, $"deny{suffix}")).GetProperty("level").GetString().ShouldBe("C1");

        // Импорт в базу с галочкой: своё слово с тем же текстом не дублируется, а привязывается.
        HttpClient user = await UserClientAsync();
        string adminOnly = $"stand{suffix} out";
        await PostJsonAsync(admin, "/api/v1/my-words", new { text = adminOnly, translations = new[] { "выделяться" } });
        string baseCsv = $"text;translation;level;note\n{adminOnly};выделяться;B2;Фразовый глагол\nfresh{suffix};свежий;A2;Прилагательное\n";
        JsonElement imported = await PostJsonAsync(admin, "/api/v1/admin/lexemes/import", new { content = baseCsv, addToMyWords = true });
        imported.GetProperty("added").GetInt32().ShouldBe(2);
        imported.GetProperty("addedToMyWords").GetInt32().ShouldBe(2);

        JsonElement standOut = await GetJsonAsync(admin, "/api/v1/my-words?search=" + Uri.EscapeDataString(adminOnly));
        standOut.GetProperty("totalCount").GetInt64().ShouldBe(1);
        standOut.GetProperty("items")[0].GetProperty("lexemeId").ValueKind.ShouldBe(JsonValueKind.String);
        standOut.GetProperty("items")[0].GetProperty("type").GetString().ShouldBe("PhrasalVerb");
        (await FindMyWordAsync(admin, $"fresh{suffix}")).GetProperty("partOfSpeech").GetString().ShouldBe("Adjective");

        (await user.PostAsJsonAsync("/api/v1/admin/lexemes/from-my-words", new { }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static async Task<JsonElement> FindMyWordAsync(HttpClient client, string text)
    {
        JsonElement page = await GetJsonAsync(client, "/api/v1/my-words?pageSize=100&search=" + Uri.EscapeDataString(text));
        return page.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("text").GetString() == text);
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

    private static object LexemeBody(string text, string translation, Guid categoryId) => new
    {
        text,
        type = "Word",
        partOfSpeech = "Noun",
        transcription = "/ˈzebrə/",
        level = "B1",
        audioUrl = (string?)null,
        translations = new[] { new { language = "ru", text = translation } },
        examples = new[] { new { text = "A zebra has stripes.", translation = "У зебры полоски." } },
        categoryIds = new[] { categoryId },
    };

    private static IEnumerable<string?> Texts(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("text").GetString());

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string url)
    {
        HttpResponseMessage response = await client.GetAsync(url, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"GET {url}: {await response.Content.ReadAsStringAsync(Ct)}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<Guid> FindLexemeIdAsync(HttpClient client, string text)
    {
        JsonElement page = await GetJsonAsync(client, "/api/v1/lexemes?search=" + Uri.EscapeDataString(text));
        return page.GetProperty("items").EnumerateArray().First(i => i.GetProperty("text").GetString() == text).GetProperty("id").GetGuid();
    }

    private async Task<HttpClient> UserClientAsync()
    {
        HttpClient client = CreateClient();
        string email = $"vocab-{Guid.NewGuid():N}@example.com";
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "secret123" }, Ct);
        return Authorize(client, await response.Content.ReadFromJsonAsync<JsonElement>(Ct));
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        HttpClient client = CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword },
            Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return Authorize(client, await response.Content.ReadFromJsonAsync<JsonElement>(Ct));
    }

    private static HttpClient Authorize(HttpClient client, JsonElement auth)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return client;
    }

    private HttpClient CreateClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
}
