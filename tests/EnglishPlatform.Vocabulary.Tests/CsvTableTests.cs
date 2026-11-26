using EnglishPlatform.Vocabulary.Application.Import;
using EnglishPlatform.Vocabulary.Domain;
using Shouldly;

namespace EnglishPlatform.Vocabulary.Tests;

public sealed class CsvTableTests
{
    [Theory]
    [InlineData("text;translation\napple;яблоко")]
    [InlineData("text,translation\napple,яблоко")]
    [InlineData("text\ttranslation\napple\tяблоко")]
    [InlineData("﻿Слово;Перевод\r\napple;яблоко\r\n")]
    public void Detects_delimiter_and_header_aliases(string content)
    {
        (IReadOnlyList<CsvRow> rows, string? error) = CsvTable.Parse(content, 10);

        error.ShouldBeNull();
        rows.Count.ShouldBe(1);
        rows[0].Get(CsvTable.Columns.Text).ShouldBe("apple");
        rows[0].Get(CsvTable.Columns.Translation).ShouldBe("яблоко");
    }

    [Fact]
    public void Reads_quoted_values_with_delimiters_quotes_and_line_breaks()
    {
        const string content = "text;example;note\n\"a \"\"quoted\"\" word\";\"one; two\";\"line 1\nline 2\"\nnext;;\n";

        (IReadOnlyList<CsvRow> rows, _) = CsvTable.Parse(content, 10);

        rows.Count.ShouldBe(2);
        rows[0].Get(CsvTable.Columns.Text).ShouldBe("a \"quoted\" word");
        rows[0].Get(CsvTable.Columns.Example).ShouldBe("one; two");
        rows[0].Get(CsvTable.Columns.Note).ShouldBe("line 1\nline 2");
        rows[1].Line.ShouldBe(4);
        rows[1].Get(CsvTable.Columns.Example).ShouldBeNull();
    }

    [Fact]
    public void Skips_empty_lines_and_ignores_unknown_columns()
    {
        (IReadOnlyList<CsvRow> rows, _) = CsvTable.Parse("word;colour;translation\n\n;;\ncat;black;кошка\n", 10);

        rows.Count.ShouldBe(1);
        rows[0].Line.ShouldBe(4);
        rows[0].Values.Keys.ShouldBe([CsvTable.Columns.Text, CsvTable.Columns.Translation], ignoreOrder: true);
    }

    [Fact]
    public void Requires_text_column_and_limits_rows()
    {
        CsvTable.Parse("apple;яблоко\npear;груша", 10).Error.ShouldNotBeNull();
        CsvTable.Parse("", 10).Error.ShouldNotBeNull();
        CsvTable.Parse("text\na\nb\nc", 2).Error.ShouldNotBeNull();
    }

    [Fact]
    public void Splits_multiple_values()
    {
        CsvTable.SplitValues(" дом | жилище ||").ShouldBe(["дом", "жилище"]);
        CsvTable.SplitValues(null).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("phrasal verb", "give up", LexemeType.PhrasalVerb)]
    [InlineData("Идиома", "x", LexemeType.Idiom)]
    [InlineData("PhrasalVerb", "x", LexemeType.PhrasalVerb)]
    [InlineData("", "apple", LexemeType.Word)]
    [InlineData(null, "by the way", LexemeType.Phrase)]
    public void Parses_lexeme_type(string? value, string text, LexemeType expected)
    {
        ImportValueParser.TryParseType(value, text, out LexemeType type).ShouldBeTrue();
        type.ShouldBe(expected);
    }

    [Theory]
    [InlineData("adj.", PartOfSpeech.Adjective)]
    [InlineData("глагол", PartOfSpeech.Verb)]
    [InlineData("Noun", PartOfSpeech.Noun)]
    public void Parses_part_of_speech(string value, PartOfSpeech expected)
    {
        ImportValueParser.TryParsePartOfSpeech(value, out PartOfSpeech? parsed).ShouldBeTrue();
        parsed.ShouldBe(expected);
    }

    [Fact]
    public void Rejects_unknown_values()
    {
        ImportValueParser.TryParseType("thing", "x", out _).ShouldBeFalse();
        ImportValueParser.TryParseType("7", "x", out _).ShouldBeFalse();
        ImportValueParser.TryParseLevel("Z9", out _).ShouldBeFalse();
        ImportValueParser.TryParseLevel("b2", out CefrLevel? level).ShouldBeTrue();
        level.ShouldBe(CefrLevel.B2);
        ImportValueParser.TryParsePartOfSpeech("smth", out _).ShouldBeFalse();
    }
}
