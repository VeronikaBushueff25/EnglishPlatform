using System.ComponentModel.DataAnnotations;
using EnglishPlatform.Vocabulary.Application.Dictionary;
using EnglishPlatform.Vocabulary.Domain;
using EnglishPlatform.Vocabulary.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EnglishPlatform.Vocabulary.Infrastructure.Dictionary;

public sealed record StartCoreImportRequest(
    [property: Required, MinLength(1)] IReadOnlyList<CefrLevel> Levels,
    [property: Range(1, 20000)] int? Limit);

/// <summary>Состояние пополнения базы: сколько слов списка проверено, сколько добавлено, на каком слове сейчас.</summary>
public sealed record CoreImportStatusDto(
    bool Running,
    IReadOnlyList<CefrLevel> Levels,
    int Total,
    int Processed,
    int Added,
    int Missing,
    string? Current,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    int BaseSize);

/// <summary>
/// Пополнение общей базы ядром лексики: слова списков CEFR нужных уровней, которых ещё нет в базе, берутся из
/// внешнего словаря по одному с паузой (бережно к источнику). Идёт в фоне; повторный запуск пропускает уже добавленное.
/// Один экземпляр на процесс — для MVP с одним API этого достаточно.
/// </summary>
public sealed class CoreVocabularyImport(IServiceScopeFactory scopes, ICefrLevels levels, TimeProvider time, ILogger<CoreVocabularyImport> logger)
    : BackgroundService
{
    /// <summary>Пауза между запросами к внешнему словарю.</summary>
    public static TimeSpan Pause { get; set; } = TimeSpan.FromMilliseconds(400);

    private readonly Lock _lock = new();
    private CancellationTokenSource? _current;
    private TaskCompletionSource<(IReadOnlyList<CefrLevel> Levels, int? Limit)> _request = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CoreImportStatusDto _status = new(false, [], 0, 0, 0, 0, null, null, null, 0);

    public CoreImportStatusDto Status
    {
        get
        {
            lock (_lock)
            {
                return _status;
            }
        }
    }

    /// <summary>false — пополнение уже идёт.</summary>
    public bool Start(IReadOnlyList<CefrLevel> levelsToImport, int? limit)
    {
        lock (_lock)
        {
            if (_status.Running)
            {
                return false;
            }

            _status = _status with { Running = true, Levels = levelsToImport, Processed = 0, Added = 0, Missing = 0, Total = 0, StartedAt = time.GetUtcNow(), FinishedAt = null };
            _request.TrySetResult((levelsToImport, limit));
            return true;
        }
    }

    public void Cancel()
    {
        lock (_lock)
        {
            _current?.Cancel();
        }
    }

    public async Task<int> BaseSizeAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<VocabularyDb>().Lexemes.CountAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            (IReadOnlyList<CefrLevel> wanted, int? limit) = await _request.Task.WaitAsync(stoppingToken);
            using var run = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            lock (_lock)
            {
                _current = run;
                _request = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            try
            {
                await RunAsync(wanted, limit, run.Token);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Пополнение базы остановлено");
            }
            catch (Exception e)
            {
                logger.LogError(e, "Пополнение базы прервано ошибкой");
            }
            finally
            {
                int size = await BaseSizeAsync(CancellationToken.None);
                lock (_lock)
                {
                    _status = _status with { Running = false, Current = null, FinishedAt = time.GetUtcNow(), BaseSize = size };
                    _current = null;
                }
            }
        }
    }

    private async Task RunAsync(IReadOnlyList<CefrLevel> wanted, int? limit, CancellationToken cancellationToken)
    {
        HashSet<string> existing;
        await using (AsyncServiceScope scope = scopes.CreateAsyncScope())
        {
            existing = (await scope.ServiceProvider.GetRequiredService<VocabularyDb>().Lexemes.Select(l => l.NormalizedText).ToListAsync(cancellationToken))
                .ToHashSet();
        }

        List<string> words = levels.Words
            .Where(w => wanted.Contains(w.Level))
            .Select(w => TextNormalizer.Normalize(w.Text))
            .Where(w => w.Length > 1 && !existing.Contains(w))
            .Distinct()
            .Take(limit ?? int.MaxValue)
            .ToList();
        Update(s => s with { Total = words.Count });

        foreach (string word in words)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Update(s => s with { Current = word });
            await using (AsyncServiceScope scope = scopes.CreateAsyncScope())
            {
                DictionaryLookup lookup = scope.ServiceProvider.GetRequiredService<DictionaryLookup>();
                (List<Lexeme> found, string? _) = await lookup.FindOrImportAsync(word, cancellationToken);
                bool added = found.Any(l => l.NeedsReview && l.CreatedAt >= _status.StartedAt);
                Update(s => s with { Processed = s.Processed + 1, Added = s.Added + (added ? 1 : 0), Missing = s.Missing + (found.Count == 0 ? 1 : 0) });
            }

            await Task.Delay(Pause, cancellationToken);
        }
    }

    private void Update(Func<CoreImportStatusDto, CoreImportStatusDto> change)
    {
        lock (_lock)
        {
            _status = change(_status);
        }
    }
}
