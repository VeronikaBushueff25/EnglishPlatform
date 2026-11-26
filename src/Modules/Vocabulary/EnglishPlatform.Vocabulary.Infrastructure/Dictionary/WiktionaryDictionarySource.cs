using System.Net;
using EnglishPlatform.Vocabulary.Application.Dictionary;
using EnglishPlatform.Vocabulary.Domain;
using EnglishPlatform.Vocabulary.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnglishPlatform.Vocabulary.Infrastructure.Dictionary;

public sealed class DictionaryOptions
{
    public const string Section = "Dictionary";

    /// <summary>Выключить внешний словарь (тесты, работа без сети): поиск идёт только по базе.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Статьи английского Викисловаря в формате kaikki.org: {BaseUrl}{p}/{pu}/{put off}.jsonl.</summary>
    public string BaseUrl { get; set; } = "https://kaikki.org/dictionary/English/meaning/";

    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Поиск примеров с переводом (Tatoeba).</summary>
    public string TatoebaUrl { get; set; } = "https://tatoeba.org/en/api_v0/search";

    /// <summary>Сколько хранить ответ «слова нет»: словарь пополняется, через неделю спросим снова.</summary>
    public int NotFoundCacheDays { get; set; } = 7;
}

/// <summary>
/// Викисловарь через kaikki.org. Каждый ответ (и «слова нет») сохраняется в vocabulary.dictionary_cache,
/// поэтому одно слово запрашивается один раз. Сеть недоступна — пустой ответ без кеширования, поиск остаётся по базе.
/// </summary>
public sealed class WiktionaryDictionarySource(
    IHttpClientFactory httpClientFactory,
    VocabularyDb db,
    DictionaryOptions options,
    TimeProvider time,
    ILogger<WiktionaryDictionarySource> logger) : IDictionarySource
{
    public const string HttpClientName = "wiktionary";

    public async Task<IReadOnlyList<DictionaryEntry>> LookupAsync(string text, CancellationToken cancellationToken)
    {
        string word = TextNormalizer.Normalize(text);
        if (!options.Enabled || word.Length is 0 or > DictionaryCacheEntry.MaxKeyLength || !char.IsAsciiLetter(word[0]))
        {
            return [];
        }

        DateTimeOffset now = time.GetUtcNow();
        DictionaryCacheEntry? cached = await db.DictionaryCache.SingleOrDefaultAsync(c => c.Id == word, cancellationToken);
        if (cached is not null && (cached.Found || cached.FetchedAt > now.AddDays(-options.NotFoundCacheDays)))
        {
            return cached.Found ? WiktionaryParser.Parse(word, cached.Payload) : [];
        }

        string? payload = await FetchAsync(word, cancellationToken);
        if (payload is null)
        {
            return [];
        }

        IReadOnlyList<DictionaryEntry> entries = WiktionaryParser.Parse(word, payload);
        bool found = payload.Length > 0;
        if (cached is null)
        {
            db.DictionaryCache.Add(DictionaryCacheEntry.Create(word, payload, found, now));
        }
        else
        {
            cached.Refresh(payload, found, now);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Тот же запрос параллельно уже сохранил ответ — нам достаточно разобранного.
        }

        return entries;
    }

    /// <summary>Тело ответа; пустая строка — слова нет (404); null — словарь недоступен.</summary>
    private async Task<string?> FetchAsync(string word, CancellationToken cancellationToken)
    {
        string first = word[..1];
        string two = word.Length >= 2 ? word[..2] : word;
        var url = new Uri($"{options.BaseUrl}{Uri.EscapeDataString(first)}/{Uri.EscapeDataString(two)}/{Uri.EscapeDataString(word)}.jsonl");
        try
        {
            using HttpClient client = httpClientFactory.CreateClient(HttpClientName);
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
            using HttpResponseMessage response = await client.GetAsync(url, timeout.Token);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return string.Empty;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Словарь ответил {Status} на «{Word}»", (int)response.StatusCode, word);
                return null;
            }

            return await response.Content.ReadAsStringAsync(timeout.Token);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(e, "Словарь недоступен для «{Word}»", word);
            return null;
        }
    }
}
