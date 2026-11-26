using EnglishPlatform.Vocabulary.Domain;
using Shouldly;

namespace EnglishPlatform.Vocabulary.Tests;

public sealed class VocabularyDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("  Don’t   Worry ", "don't worry")]
    [InlineData("Ёлка", "елка")]
    [InlineData(null, "")]
    public void Normalizes_text_for_search_and_duplicates(string? input, string expected) =>
        TextNormalizer.Normalize(input).ShouldBe(expected);

    [Theory]
    [InlineData("/ˈæpl/", "ˈæpl")]
    [InlineData("[ bred ]", "bred")]
    [InlineData("  ", null)]
    public void Strips_transcription_brackets(string input, string? expected) =>
        TextNormalizer.CleanTranscription(input).ShouldBe(expected);

    [Fact]
    public void Lexeme_cleans_translations_and_builds_search_text()
    {
        Lexeme lexeme = Lexeme.Create(Data("Apple", "яблоко", "Яблоко", " ", "яблоня"), Now);

        lexeme.NormalizedText.ShouldBe("apple");
        lexeme.Translations.Select(t => t.Text).ShouldBe(["яблоко", "яблоня"]);
        lexeme.SearchText.ShouldContain("яблоня");
    }

    [Fact]
    public void User_copy_follows_lexeme_until_user_edits_it_but_not_after_note_only_change()
    {
        Lexeme lexeme = Lexeme.Create(Data("apple", "яблоко"), Now);
        UserWord word = UserWord.FromLexeme(Guid.NewGuid(), lexeme, "ru", Now);

        word.Edit(word.ToData() with { Note = "мой пример" }, Now);
        word.IsEdited.ShouldBeFalse();

        lexeme.Update(Data("apple", "яблоко", "яблоня"), Now);
        word.SyncWith(lexeme, "ru", Now);
        word.Translations.ShouldBe(["яблоко", "яблоня"]);
        word.Note.ShouldBe("мой пример");

        word.Edit(word.ToData() with { Translations = ["моё яблоко"] }, Now);
        word.IsEdited.ShouldBeTrue();

        lexeme.Update(Data("apple", "другое"), Now);
        word.SyncWith(lexeme, "ru", Now);
        word.Translations.ShouldBe(["моё яблоко"]);
    }

    [Fact]
    public void Detached_word_keeps_its_content()
    {
        Lexeme lexeme = Lexeme.Create(Data("apple", "яблоко"), Now);
        UserWord word = UserWord.FromLexeme(Guid.NewGuid(), lexeme, "ru", Now);

        word.DetachFromLexeme(Now);

        word.LexemeId.ShouldBeNull();
        word.Text.ShouldBe("apple");
        word.Translations.ShouldBe(["яблоко"]);
    }

    private static LexemeData Data(string text, params string[] translations) => new(
        text,
        LexemeType.Word,
        PartOfSpeech.Noun,
        null,
        CefrLevel.A1,
        null,
        translations.Select(t => new LexemeTranslation("ru", t)).ToList(),
        [new LexemeExample("I like apples.", "Я люблю яблоки.")],
        []);
}
