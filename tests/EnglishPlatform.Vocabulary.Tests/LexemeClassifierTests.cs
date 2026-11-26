using EnglishPlatform.Vocabulary.Application.Import;
using EnglishPlatform.Vocabulary.Domain;
using Shouldly;

namespace EnglishPlatform.Vocabulary.Tests;

/// <summary>Примеры взяты из реального импорта: метки лежали в заметке, тип по умолчанию был «Фраза».</summary>
public sealed class LexemeClassifierTests
{
    [Theory]
    [InlineData("deny", "Глагол; deny something / deny doing", LexemeType.Word, PartOfSpeech.Verb, "deny something / deny doing")]
    [InlineData("inconsistency", "Существительное; plural inconsistencies", LexemeType.Word, PartOfSpeech.Noun, "plural inconsistencies")]
    [InlineData("bump into", "Фразовый глагол", LexemeType.PhrasalVerb, PartOfSpeech.Verb, null)]
    [InlineData("lose touch", "Устойчивое выражение", LexemeType.Phrase, null, null)]
    [InlineData("adequate evidence", "Коллокация", LexemeType.Phrase, null, null)]
    [InlineData("be afraid of", "Прилагательное + предлог", LexemeType.Phrase, PartOfSpeech.Adjective, null)]
    [InlineData("buried", "Причастие/прилагательное от bury", LexemeType.Word, PartOfSpeech.Adjective, null)]
    [InlineData("remain", "Глагол; не путать с remind", LexemeType.Word, PartOfSpeech.Verb, "не путать с remind")]
    [InlineData("relevant", "Часто relevant to", LexemeType.Word, null, "Часто relevant to")]
    [InlineData("would have + V3", "Third Conditional result", LexemeType.Phrase, null, "Third Conditional result")]
    [InlineData("inevitably", "Наречие от inevitable", LexemeType.Word, PartOfSpeech.Adverb, "Наречие от inevitable")]
    [InlineData("money", "Неисчисляемое существительное", LexemeType.Word, PartOfSpeech.Noun, "Неисчисляемое существительное")]
    [InlineData("depend", "Предлог on", LexemeType.Word, null, "Предлог on")]
    [InlineData("interested in", "Предлог in", LexemeType.Phrase, null, "Предлог in")]
    [InlineData("manage with", "Устойчивое сочетание", LexemeType.Phrase, null, null)]
    [InlineData("used to", "Фиксированная конструкция", LexemeType.Phrase, null, null)]
    public void Reads_label_from_the_start_of_the_note(string text, string note, LexemeType type, PartOfSpeech? pos, string? restNote)
    {
        Classification result = LexemeClassifier.Classify(text, null, null, note);

        result.Error.ShouldBeNull();
        result.Type.ShouldBe(type);
        result.PartOfSpeech.ShouldBe(pos);
        result.Note.ShouldBe(restNote);
    }

    [Theory]
    [InlineData("come across", true)]
    [InlineData("get on", true)]
    [InlineData("run out of", true)]
    [InlineData("look forward to", true)]
    [InlineData("put up with", true)]
    [InlineData("be afraid of", false)]
    [InlineData("capable of", false)]
    [InlineData("fall in love", false)]
    [InlineData("miss an opportunity", false)]
    [InlineData("the end", false)]
    [InlineData("keep + V-ing", false)]
    [InlineData("apple", false)]
    public void Recognizes_phrasal_verbs_by_shape(string text, bool expected) =>
        LexemeClassifier.LooksLikePhrasalVerb(text).ShouldBe(expected);

    [Fact]
    public void Columns_win_over_the_note_and_unknown_values_are_errors()
    {
        Classification fromColumns = LexemeClassifier.Classify("get on", "идиома", "noun", "Фразовый глагол");
        fromColumns.Type.ShouldBe(LexemeType.Idiom);
        fromColumns.PartOfSpeech.ShouldBe(PartOfSpeech.Noun);
        fromColumns.Note.ShouldBe("Фразовый глагол");

        LexemeClassifier.Classify("x", "что-то", null, null).Error.ShouldNotBeNull();
        LexemeClassifier.Classify("x", null, "что-то", null).Error.ShouldNotBeNull();
    }

    [Fact]
    public void Part_of_speech_label_in_type_column_keeps_word_or_phrase()
    {
        LexemeClassifier.Classify("improve", "Глагол", null, null).ShouldBe(new Classification(LexemeType.Word, PartOfSpeech.Verb, null));
        LexemeClassifier.Classify("lose touch", "Глагол", null, null).Type.ShouldBe(LexemeType.Phrase);
    }
}
