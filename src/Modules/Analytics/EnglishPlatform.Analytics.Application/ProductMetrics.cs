using System.Diagnostics;
using System.Diagnostics.Metrics;
using EnglishPlatform.BuildingBlocks.Events;
using EnglishPlatform.Identity.Contracts;
using EnglishPlatform.LearningSessions.Contracts;
using EnglishPlatform.Vocabulary.Contracts;

namespace EnglishPlatform.Analytics.Application;

/// <summary>
/// Продуктовые метрики OpenTelemetry (метр <see cref="MeterName"/>): регистрации, добавленные слова, ответы по результату
/// и формату, время ответа. Технические метрики (HTTP, рантайм) собирает хост. Без персональных данных в тегах.
/// </summary>
public sealed class ProductMetrics
{
    public const string MeterName = "EnglishPlatform";

    private readonly Counter<long> _registrations;
    private readonly Counter<long> _wordsAdded;
    private readonly Counter<long> _answers;
    private readonly Histogram<double> _answerSeconds;

    public ProductMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);
        _registrations = meter.CreateCounter<long>("ep.users.registered", unit: "{user}", description: "Новые регистрации");
        _wordsAdded = meter.CreateCounter<long>("ep.words.added", unit: "{word}", description: "Слова, добавленные в личные словари");
        _answers = meter.CreateCounter<long>("ep.answers", unit: "{answer}", description: "Проверенные ответы в занятиях");
        _answerSeconds = meter.CreateHistogram<double>(
            "ep.answer.duration",
            unit: "s",
            description: "Сколько пользователь думает над ответом",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [1, 2, 3, 5, 8, 13, 20, 30, 60] });
    }

    public void UserRegistered() => _registrations.Add(1);

    public void WordAdded() => _wordsAdded.Add(1);

    public void AnswerChecked(string verdict, string exerciseKind, int elapsedMs)
    {
        var tags = new TagList { { "verdict", verdict }, { "exercise", exerciseKind } };
        _answers.Add(1, tags);
        if (elapsedMs > 0)
        {
            _answerSeconds.Record(elapsedMs / 1000.0, tags);
        }
    }
}

public sealed class MeterUserRegistered(ProductMetrics metrics) : IIntegrationEventHandler<UserRegistered>
{
    public Task HandleAsync(UserRegistered integrationEvent, CancellationToken cancellationToken)
    {
        metrics.UserRegistered();
        return Task.CompletedTask;
    }
}

public sealed class MeterUserWordAdded(ProductMetrics metrics) : IIntegrationEventHandler<UserWordAdded>
{
    public Task HandleAsync(UserWordAdded integrationEvent, CancellationToken cancellationToken)
    {
        metrics.WordAdded();
        return Task.CompletedTask;
    }
}

public sealed class MeterAnswerChecked(ProductMetrics metrics) : IIntegrationEventHandler<AnswerChecked>
{
    public Task HandleAsync(AnswerChecked integrationEvent, CancellationToken cancellationToken)
    {
        if (!integrationEvent.IsWarmUp)
        {
            metrics.AnswerChecked(integrationEvent.Verdict, integrationEvent.ExerciseKind, integrationEvent.ElapsedMs);
        }

        return Task.CompletedTask;
    }
}
