using EnglishPlatform.Vocabulary.Application.Dictionary;
using Shouldly;

namespace EnglishPlatform.Learning.Tests;

/// <summary>Выбор примера из настоящих ответов Tatoeba (Fixtures/tatoeba).</summary>
public sealed class TatoebaParserTests
{
    private static string Load(string word) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tatoeba", word + ".json"));

    [Theory]
    [InlineData("gullible")]
    [InlineData("headline")]
    public void Picks_a_translated_sentence_with_the_word(string word)
    {
        TranslatedExample? example = TatoebaParser.Pick(Load(word), word);

        example.ShouldNotBeNull();
        example.Text.ShouldContain(word, Case.Insensitive);
        example.Translation.ShouldNotBeNullOrWhiteSpace();
        example.Text.Length.ShouldBeInRange(12, 110);
    }

    [Fact]
    public void Prefers_sentences_without_stock_names()
    {
        const string json = """
            {"results":[
              {"text":"Tom was gullible and believed everything.","translations":[[{"lang":"rus","text":"Том был доверчив."}]]},
              {"text":"My neighbour is far too gullible.","translations":[[{"lang":"rus","text":"Мой сосед слишком доверчив."}]]}
            ]}
            """;
        TatoebaParser.Pick(json, "gullible")!.Text.ShouldBe("My neighbour is far too gullible.");
    }

    [Fact]
    public void Nothing_without_russian_or_on_garbage()
    {
        TatoebaParser.Pick("""{"results":[{"text":"A gullible man.","translations":[[{"lang":"deu","text":"x"}]]}]}""", "gullible").ShouldBeNull();
        TatoebaParser.Pick("not json", "x").ShouldBeNull();
    }
}
