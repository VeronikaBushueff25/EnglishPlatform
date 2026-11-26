using EnglishPlatform.Vocabulary.Application.Dictionary;
using EnglishPlatform.Vocabulary.Domain;
using Shouldly;

namespace EnglishPlatform.Learning.Tests;

public sealed class CefrLevelListTests
{
    private static readonly CefrLevelList Levels = new(
    [
        "# комментарий",
        "A1;light;Adjective",
        "B1;light;Verb",
        "A1;get;Verb",
        "A2;keep;Verb",
        "A2;touch;Noun",
        "B2;subtle;Adjective",
        "C1;cake;Noun",
        "plain garbage",
    ]);

    [Fact]
    public void Level_depends_on_part_of_speech()
    {
        Levels.Find("light", PartOfSpeech.Adjective).ShouldBe(CefrLevel.A1);
        Levels.Find("light", PartOfSpeech.Verb).ShouldBe(CefrLevel.B1);
        Levels.Find("Light", null).ShouldBe(CefrLevel.A1); // без части речи — самый ранний уровень
        Levels.Find("unknownword", null).ShouldBeNull();
    }

    [Fact]
    public void Multiword_levels_are_estimated()
    {
        Levels.Find("get over", PartOfSpeech.Verb, LexemeType.PhrasalVerb).ShouldBe(CefrLevel.B1);
        Levels.Find("keep in touch", null, LexemeType.Phrase).ShouldBe(CefrLevel.A2);
        Levels.Find("a piece of cake", null, LexemeType.Idiom).ShouldBe(CefrLevel.C1);
        Levels.Find("keep in touch", null, LexemeType.Idiom).ShouldBe(CefrLevel.B2);
    }

    [Fact]
    public void Words_are_listed_for_import()
    {
        Levels.Words.Count.ShouldBe(7);
        Levels.Words.ShouldContain((CefrLevel.B2, "subtle", PartOfSpeech.Adjective));
    }

    [Theory]
    [InlineData("cats", "cat")]
    [InlineData("boxes", "box")]
    [InlineData("studies", "study")]
    [InlineData("studied", "study")]
    [InlineData("stopped", "stop")]
    [InlineData("running", "run")]
    [InlineData("liked", "like")]
    [InlineData("making", "make")]
    [InlineData("quickly", "quick")]
    public void Lemma_candidates_include_the_base_form(string form, string lemma)
    {
        DictionaryLookup.Lemmas(form).ShouldContain(lemma);
    }
}
