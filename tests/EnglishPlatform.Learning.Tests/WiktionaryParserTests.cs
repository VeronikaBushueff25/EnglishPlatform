using EnglishPlatform.Vocabulary.Application.Dictionary;
using EnglishPlatform.Vocabulary.Domain;
using Shouldly;

namespace EnglishPlatform.Learning.Tests;

/// <summary>Разбор настоящих статей Викисловаря (kaikki.org), сохранённых в Fixtures/wiktionary.</summary>
public sealed class WiktionaryParserTests
{
    private static IReadOnlyList<DictionaryEntry> Load(string word) =>
        WiktionaryParser.Parse(word, File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "wiktionary", word + ".jsonl")));

    [Fact]
    public void Phrasal_verb_gets_type_translations_without_stress_marks()
    {
        DictionaryEntry entry = Load("put off")[0];

        entry.Type.ShouldBe(LexemeType.PhrasalVerb);
        entry.PartOfSpeech.ShouldBe(PartOfSpeech.Verb);
        entry.Translations.ShouldContain("откладывать");
        entry.Translations.ShouldNotContain("отложить"); // видовая пара схлопнута
        entry.Translations.ShouldAllBe(t => !t.Contains('́'));
    }

    [Fact]
    public void One_translation_per_sense()
    {
        Load("work out")[0].Translations.ShouldBe(["рассчитывать", "понимать", "разрабатывать", "получаться"]);
    }

    [Fact]
    public void Prefers_british_transcription()
    {
        DictionaryEntry delay = Load("delay").First(e => e.PartOfSpeech == PartOfSpeech.Noun);

        delay.Transcription.ShouldBe("dɪˈleɪ");
        delay.Translations.ShouldContain("задержка");
    }

    [Fact]
    public void Same_word_has_entries_per_part_of_speech()
    {
        IReadOnlyList<DictionaryEntry> delay = Load("delay");

        delay.Select(e => e.PartOfSpeech).ShouldContain(PartOfSpeech.Noun);
        delay.Select(e => e.PartOfSpeech).ShouldContain(PartOfSpeech.Verb);
    }

    [Theory]
    [InlineData("buried", "bury")]
    [InlineData("ovaries", "ovary")]
    public void Inflected_form_points_to_lemma(string form, string lemma)
    {
        Load(form).ShouldContain(e => e.LemmaOf == lemma);
    }

    [Fact]
    public void Idiom_and_phrasal_verb_with_preposition_are_told_apart()
    {
        IReadOnlyList<DictionaryEntry> terms = Load("come to terms with");
        IReadOnlyList<DictionaryEntry> look = Load("look forward to");
        terms.Count.ShouldBeGreaterThan(0, "come to terms with");
        look.Count.ShouldBeGreaterThan(0, "look forward to");
        terms[0].Type.ShouldBe(LexemeType.Idiom);
        look[0].Type.ShouldBe(LexemeType.PhrasalVerb);
    }

    [Theory]
    [InlineData("give up", "verb", LexemeType.PhrasalVerb)]
    [InlineData("put up with", "verb", LexemeType.PhrasalVerb)]
    [InlineData("deal with", "verb", LexemeType.PhrasalVerb)]
    [InlineData("take into account", "verb", LexemeType.Phrase)]
    [InlineData("get used to", "verb", LexemeType.Phrase)]
    [InlineData("a piece of cake", "noun", LexemeType.Phrase)]
    [InlineData("subtle", "adj", LexemeType.Word)]
    public void Classifies_by_tokens(string text, string pos, LexemeType expected)
    {
        WiktionaryParser.Classify(text, pos, []).ShouldBe(expected);
    }

    [Fact]
    public void Idiom_category_wins_for_non_verbs()
    {
        WiktionaryParser.Classify("a piece of cake", "noun", ["English idioms"]).ShouldBe(LexemeType.Idiom);
    }

    [Fact]
    public void Russian_cleanup_keeps_yo_and_short_i()
    {
        WiktionaryParser.CleanRussian("неизбе́жный").ShouldBe("неизбежный");
        WiktionaryParser.CleanRussian("ёлка  й").ShouldBe("ёлка й");
    }

    [Theory]
    [InlineData("/ˈtɛnʃn̩/", "ˈtenʃn")]
    [InlineData("/ˈbɛə.li/", "ˈbeəli")]
    [InlineData("/ˈɹʌnɪŋ/", "ˈrʌnɪŋ")]
    public void Transcription_is_brought_to_learner_style(string raw, string expected)
    {
        WiktionaryParser.LearnerIpa(raw).ShouldBe(expected);
    }

    [Fact]
    public void Garbage_is_ignored()
    {
        WiktionaryParser.Parse("x", "not json\n{}\n[1,2]").ShouldBeEmpty();
    }
}
