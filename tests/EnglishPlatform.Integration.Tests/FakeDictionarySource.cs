using System.Collections.Concurrent;
using EnglishPlatform.Vocabulary.Application.Dictionary;
using EnglishPlatform.Vocabulary.Domain;

namespace EnglishPlatform.Integration.Tests;

/// <summary>Внешний словарь для тестов: несколько статей и счётчик обращений (проверить, что второй раз идём в базу).</summary>
public sealed class FakeDictionarySource : IDictionarySource
{
    private static readonly Dictionary<string, DictionaryEntry[]> Entries = new(StringComparer.OrdinalIgnoreCase)
    {
        ["serendipity"] = [new("serendipity", LexemeType.Word, PartOfSpeech.Noun, "ˌserənˈdɪpəti", ["счастливая случайность"], ["It was pure serendipity."], null)],
        ["oversleep"] = [new("oversleep", LexemeType.Word, PartOfSpeech.Verb, "ˌəʊvəˈsliːp", ["проспать"], ["I overslept and missed the bus."], null)],
        ["overslept"] = [new("overslept", LexemeType.Word, PartOfSpeech.Verb, null, [], [], "oversleep")],
        ["get over"] = [new("get over", LexemeType.PhrasalVerb, PartOfSpeech.Verb, "ˌɡet ˈəʊvə", ["пережить", "оправиться"], [], null)],
        ["quixotic"] = [
            new("quixotic", LexemeType.Word, PartOfSpeech.Adjective, "kwɪkˈsɒtɪk", ["донкихотский", "мечтательный"], [], null),
            new("quixotic", LexemeType.Word, PartOfSpeech.Noun, "kwɪkˈsɒtɪk", ["мечтатель"], [], null),
        ],
    };

    public ConcurrentDictionary<string, int> Calls { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<DictionaryEntry>> LookupAsync(string text, CancellationToken cancellationToken)
    {
        Calls.AddOrUpdate(text, 1, (_, n) => n + 1);
        return Task.FromResult<IReadOnlyList<DictionaryEntry>>(Entries.TryGetValue(text, out DictionaryEntry[]? entries) ? entries : []);
    }
}

/// <summary>Примеры с переводом для тестов (вместо Tatoeba).</summary>
public sealed class FakeExampleSource : IExampleSource
{
    public Task<TranslatedExample?> FindAsync(string text, CancellationToken cancellationToken) =>
        Task.FromResult(text.ToLowerInvariant() switch
        {
            "serendipity" => new TranslatedExample("Meeting her was pure serendipity.", "Встреча с ней была счастливой случайностью."),
            "oversleep" => new TranslatedExample("I overslept again.", "Я опять проспал."),
            _ => (TranslatedExample?)null,
        });
}
