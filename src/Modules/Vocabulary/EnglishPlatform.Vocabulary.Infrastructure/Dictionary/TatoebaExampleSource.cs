using EnglishPlatform.Vocabulary.Application.Dictionary;
using EnglishPlatform.Vocabulary.Domain;
using EnglishPlatform.Vocabulary.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnglishPlatform.Vocabulary.Infrastructure.Dictionary;

/// <summary>
/// Примеры с русским переводом из Tatoeba. Ответы кешируются в той же таблице, что и словарь (ключ «tatoeba:слово»),
/// поэтому за примером к каждому слову ходим один раз.
/// </summary>
public sealed class TatoebaExampleSource(
    IHttpClientFactory httpClientFactory,
    VocabularyDb db,
    DictionaryOptions options,
    TimeProvider time,
    ILogger<TatoebaExampleSource> logger) : IExampleSource
{
    private const string KeyPrefix = "tatoeba:";

    public async Task<TranslatedExample?> FindAsync(string text, CancellationToken cancellationToken)
    {
        string word = TextNormalizer.Normalize(text);
        string key = KeyPrefix + word;
        if (!options.Enabled || word.Length == 0 || key.Length > DictionaryCacheEntry.MaxKeyLength)
        {
            return null;
        }

        DateTimeOffset now = time.GetUtcNow();
        DictionaryCacheEntry? cached = await db.DictionaryCache.SingleOrDefaultAsync(c => c.Id == key, cancellationToken);
        if (cached is not null && (cached.Found || cached.FetchedAt > now.AddDays(-options.NotFoundCacheDays)))
        {
            return cached.Found ? TatoebaParser.Pick(cached.Payload, word) : null;
        }

        string? payload = await FetchAsync(word, cancellationToken);
        if (payload is null)
        {
            return null;
        }

        TranslatedExample? example = TatoebaParser.Pick(payload, word);
        if (cached is null)
        {
            db.DictionaryCache.Add(DictionaryCacheEntry.Create(key, payload, example is not null, now));
        }
        else
        {
            cached.Refresh(payload, example is not null, now);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Параллельный запрос уже сохранил ответ.
        }

        return example;
    }

    private async Task<string?> FetchAsync(string word, CancellationToken cancellationToken)
    {
        // «=слово» — точное совпадение слова (не «buried» для «bury»); фразу ищем как есть.
        string query = Uri.EscapeDataString(word.Contains(' ') ? $"\"{word}\"" : "=" + word);
        var url = new Uri($"{options.TatoebaUrl}?from=eng&to=rus&query={query}&orphans=no&unapproved=no&sort=relevance");
        try
        {
            using HttpClient client = httpClientFactory.CreateClient(WiktionaryDictionarySource.HttpClientName);
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
            using HttpResponseMessage response = await client.GetAsync(url, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Tatoeba ответила {Status} на «{Word}»", (int)response.StatusCode, word);
                return null;
            }

            return await response.Content.ReadAsStringAsync(timeout.Token);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(e, "Tatoeba недоступна для «{Word}»", word);
            return null;
        }
    }
}
