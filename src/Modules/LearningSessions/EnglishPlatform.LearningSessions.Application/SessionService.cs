using System.Text.Json;
using System.Text.Json.Serialization;
using EnglishPlatform.Analytics.Contracts;
using EnglishPlatform.BuildingBlocks.Events;
using EnglishPlatform.BuildingBlocks.Paging;
using EnglishPlatform.BuildingBlocks.Results;
using EnglishPlatform.BuildingBlocks.Security;
using EnglishPlatform.Exercises.Contracts;
using EnglishPlatform.Grammar.Contracts;
using EnglishPlatform.LearningSessions.Contracts;
using EnglishPlatform.LearningSessions.Domain;
using EnglishPlatform.SpacedRepetition.Contracts;
using EnglishPlatform.Users.Contracts;
using EnglishPlatform.Vocabulary.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EnglishPlatform.LearningSessions.Application;

public interface ILearningSessionsDb
{
    DbSet<LearningSession> Sessions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Пороги «быстрого» ответа: быстрый верный ответ — оценка Easy.</summary>
public sealed class SessionOptions
{
    public const string Section = "LearningSessions";

    public int FastChoiceMs { get; set; } = 2500;

    public int FastTypingMs { get; set; } = 6000;

    /// <summary>Карточка, которую FSRS просит показать раньше этого срока, возвращается в то же занятие.</summary>
    public int RepeatWithinMinutes { get; set; } = 20;

    /// <summary>Сколько слов в одной «работе над ошибками».</summary>
    public int MistakesSessionSize { get; set; } = 20;
}

/// <summary>
/// Занятие: собирает очередь (проблемные слова, повторения, новые слова), выдаёт задания с учётом слабых мест,
/// проверяет ответы, ставит оценку FSRS, пишет журнал и сообщает Analytics о каждом ответе. Ответ идемпотентен по attemptId.
/// </summary>
public sealed class SessionService(
    ILearningSessionsDb db,
    IReviewScheduler scheduler,
    IExerciseEngine exercises,
    IUserWordsReader words,
    IUserLearningSettings settings,
    IWeaknessReader weaknesses,
    IGrammarHints hints,
    IEventBus eventBus,
    ICurrentUser currentUser,
    SessionOptions options,
    TimeProvider time)
{
    private const int Distractors = 3;

    /// <summary>Типы ошибок, к которым есть смысл показывать правило.</summary>
    private static readonly HashSet<ErrorTag> RuleTags = [ErrorTag.Typo, ErrorTag.Article, ErrorTag.WordForm, ErrorTag.Preposition];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<TodayDto> GetTodayAsync(CancellationToken cancellationToken)
    {
        Guid userId = currentUser.UserId;
        DateTimeOffset now = time.GetUtcNow();
        UserLearningSettings s = await settings.GetAsync(userId, cancellationToken);
        WeaknessProfile weakness = await weaknesses.GetAsync(userId, cancellationToken);
        DailyQueue queue = await scheduler.BuildQueueAsync(
            userId, s.DailyNewWords, s.DailyReviews, DayStart(now, s.TimeZone), now, weakness.ProblemWordIds, cancellationToken);
        IReadOnlyList<QueuedCard> mistakes = await scheduler.BuildMistakesQueueAsync(userId, weakness.ProblemWordIds, options.MistakesSessionSize, cancellationToken);
        LearningSession? active = await FindActiveAsync(userId, DayStart(now, s.TimeZone), SessionKinds.Daily, cancellationToken);

        return new TodayDto(
            queue.ReviewsDue,
            queue.NewAvailable,
            s.DailyNewWords,
            s.DailyReviews,
            queue.ReviewsDoneToday,
            queue.NewDoneToday,
            queue.Reviews.Count + queue.NewCards.Count,
            active?.Id,
            mistakes.Count);
    }

    /// <summary>Начинает занятие. Если сегодня уже есть незавершённое того же типа — продолжаем его.</summary>
    public async Task<SessionDto> StartAsync(string? kind, CancellationToken cancellationToken)
    {
        kind = kind == SessionKinds.Mistakes ? SessionKinds.Mistakes : SessionKinds.Daily;
        Guid userId = currentUser.UserId;
        DateTimeOffset now = time.GetUtcNow();
        UserLearningSettings s = await settings.GetAsync(userId, cancellationToken);
        DateTimeOffset dayStart = DayStart(now, s.TimeZone);

        if (await FindActiveAsync(userId, dayStart, kind, cancellationToken) is { } active)
        {
            if (active.Remaining > 0)
            {
                return ToDto(active);
            }

            active.Finish(now);
        }

        WeaknessProfile weakness = await weaknesses.GetAsync(userId, cancellationToken);
        IEnumerable<QueuedCard> cards;
        if (kind == SessionKinds.Mistakes)
        {
            cards = await scheduler.BuildMistakesQueueAsync(userId, weakness.ProblemWordIds, options.MistakesSessionSize, cancellationToken);
        }
        else
        {
            DailyQueue queue = await scheduler.BuildQueueAsync(
                userId, s.DailyNewWords, s.DailyReviews, dayStart, now, weakness.ProblemWordIds, cancellationToken);
            cards = queue.Reviews.Concat(queue.NewCards);
        }

        IEnumerable<SessionItem> items = cards.Select(c =>
            new SessionItem(c.CardId, c.UserWordId, c.Mode.ToString(), c.IsNew, Introduced: !c.IsNew, Repeats: 0, IsProblem: c.IsProblem));

        var session = LearningSession.Start(userId, kind, items, now);
        if (session.Remaining == 0)
        {
            session.Finish(now);
        }

        db.Sessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(session);
    }

    /// <summary>Текущее задание; если его нет — строит следующее. Пустая очередь завершает занятие.</summary>
    public async Task<Result<NextStepDto>> NextAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        LearningSession? session = await FindAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return SessionErrors.NotFound;
        }

        DateTimeOffset now = time.GetUtcNow();
        if (session.CurrentTaskJson is { } json && !session.IsFinished)
        {
            return Step(session, ToView(Deserialize(json), session.Current!));
        }

        WeaknessProfile? weakness = null;
        while (!session.IsFinished && session.Current is { } item)
        {
            LearningWord? word = (await words.GetAsync(session.UserId, [item.UserWordId], cancellationToken)).FirstOrDefault();
            if (word is null || await scheduler.GetCardAsync(session.UserId, item.CardId, cancellationToken) is null)
            {
                session.Skip(now);
                continue;
            }

            ExerciseWord exerciseWord = ToExerciseWord(word);
            ExerciseMode mode = item.Mode == nameof(ReviewMode.Recall) ? ExerciseMode.Recall : ExerciseMode.Recognition;
            ExerciseTask task;
            if (item is { IsNew: true, Introduced: false })
            {
                task = exercises.BuildIntroduction(exerciseWord);
            }
            else if (item.NeedsWarmUp)
            {
                // Проблемное слово: сначала легче. Узнавание — напомнить слово; вспоминание — выбрать слово из вариантов.
                task = mode == ExerciseMode.Recall
                    ? exercises.BuildChooseWord(exerciseWord, await words.GetEnglishDistractorsAsync(session.UserId, word.Id, Distractors, cancellationToken))
                    : exercises.BuildIntroduction(exerciseWord);
            }
            else
            {
                weakness ??= await weaknesses.GetAsync(session.UserId, cancellationToken);
                IReadOnlyList<string> distractors = mode == ExerciseMode.Recognition
                    ? await words.GetDistractorsAsync(session.UserId, word.Id, Distractors, cancellationToken)
                    : [];
                task = exercises.Build(exerciseWord, mode, distractors, Preferences(weakness));
            }

            session.SetTask(task.TaskId, JsonSerializer.Serialize(task, Json), now);
            await db.SaveChangesAsync(cancellationToken);
            return Step(session, ToView(task, item));
        }

        session.Finish(now);
        await db.SaveChangesAsync(cancellationToken);
        return new NextStepDto(true, null, Progress(session), Stats(session));
    }

    public async Task<Result<AnswerResultDto>> AnswerAsync(Guid sessionId, AnswerRequest request, CancellationToken cancellationToken)
    {
        LearningSession? session = await FindAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return SessionErrors.NotFound;
        }

        DateTimeOffset now = time.GetUtcNow();
        Guid userId = session.UserId;

        // Повторная отправка того же ответа (обрыв связи): отдаём записанный результат, второй раз не считаем.
        if (await exercises.FindAttemptAsync(userId, request.AttemptId, cancellationToken) is { } recorded)
        {
            bool warmUp = recorded.Rating == 0;
            if (session.CurrentTaskId == recorded.Task.TaskId)
            {
                // Ответ записался, а занятие не сдвинулось — досдвигаем.
                if (warmUp)
                {
                    session.CompleteWarmUp(request.AttemptId, now);
                }
                else
                {
                    session.CompleteAnswer(recorded.Result.Verdict.ToString(), repeatSoon: false, now);
                }

                await db.SaveChangesAsync(cancellationToken);
            }

            RuleDto? recordedRule = await RuleAsync(recorded.Result.ErrorTag, cancellationToken);
            return ToResult(recorded.Task, recorded.Result, warmUp ? null : recorded.Rating, null, false, session, recordedRule, warmUp);
        }

        if (session.IsFinished)
        {
            return SessionErrors.Finished;
        }

        if (session.CurrentTaskJson is not { } json || session.CurrentTaskId != request.TaskId || session.Current is not { } item)
        {
            return SessionErrors.StaleTask;
        }

        ExerciseTask task = Deserialize(json);

        if (task.Kind == ExerciseKind.Introduce)
        {
            if (!session.WasIntroduced(request.AttemptId))
            {
                if (item.NeedsWarmUp)
                {
                    session.CompleteWarmUp(request.AttemptId, now);
                }
                else
                {
                    session.CompleteIntroduction(request.AttemptId, now);
                }

                await db.SaveChangesAsync(cancellationToken);
            }

            return new AnswerResultDto(
                null, ErrorTag.None, task.Word.Text, "Сейчас проверим, запомнилось ли.", null, null, false, ToWordView(task.Word), Progress(session), null, item.NeedsWarmUp);
        }

        CheckResult check = exercises.Check(task, request.Answer);
        RuleDto? rule = await RuleAsync(check.ErrorTag, cancellationToken);

        if (item.NeedsWarmUp)
        {
            // Разминка не влияет на расписание FSRS (оценка 0), но попадает в журнал и в счётчики ошибок.
            await exercises.RecordAsync(
                new AttemptRecord(request.AttemptId, userId, session.Id, item.CardId, task, request.Answer ?? "", request.UsedHint, request.ElapsedMs, check, 0, now),
                cancellationToken);
            await PublishAsync(userId, item, task, check, isWarmUp: true, request.ElapsedMs, now, cancellationToken);
            session.CompleteWarmUp(request.AttemptId, now);
            await db.SaveChangesAsync(cancellationToken);
            return ToResult(task, check, null, null, false, session, rule, isWarmUp: true);
        }

        ReviewRating rating = Rate(options, task.Kind, check.Verdict, request.UsedHint, request.ElapsedMs, item.IsNew && item.Repeats == 0);
        ReviewOutcome outcome = await scheduler.ReviewAsync(userId, item.CardId, rating, now, request.ElapsedMs, cancellationToken);

        await exercises.RecordAsync(
            new AttemptRecord(request.AttemptId, userId, session.Id, item.CardId, task, request.Answer ?? "", request.UsedHint, request.ElapsedMs, check, (int)rating, now),
            cancellationToken);
        await PublishAsync(userId, item, task, check, isWarmUp: false, request.ElapsedMs, now, cancellationToken);

        bool repeatSoon = outcome.NextDue <= now.AddMinutes(options.RepeatWithinMinutes);
        session.CompleteAnswer(check.Verdict.ToString(), repeatSoon, now);
        await db.SaveChangesAsync(cancellationToken);

        return ToResult(task, check, (int)rating, outcome.NextDue, outcome.RecallUnlocked, session, rule, isWarmUp: false);
    }

    public async Task<Result<SessionDto>> FinishAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        LearningSession? session = await FindAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return SessionErrors.NotFound;
        }

        session.Finish(time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(session);
    }

    public async Task<PagedResult<SessionDto>> ListAsync(PageRequest page, CancellationToken cancellationToken)
    {
        Guid userId = currentUser.UserId;
        IQueryable<LearningSession> query = db.Sessions.AsNoTracking().Where(s => s.UserId == userId && s.Answered > 0);
        long total = await query.LongCountAsync(cancellationToken);
        List<LearningSession> sessions = await query.OrderByDescending(s => s.StartedAt).Skip(page.Skip).Take(page.SafePageSize).ToListAsync(cancellationToken);
        return new PagedResult<SessionDto>(sessions.Select(ToDto).ToList(), page.SafePage, page.SafePageSize, total);
    }

    /// <summary>
    /// Оценка FSRS из ответа: неверно — Again; почти или с подсказкой — Hard; верно — Good; верно и быстро — Easy.
    /// Новое слово при первом показе не получает Easy: одного быстрого ответа мало, чтобы отпустить его на дни.
    /// </summary>
    public static ReviewRating Rate(SessionOptions options, ExerciseKind kind, AnswerVerdict verdict, bool usedHint, int elapsedMs, bool firstSightOfNewWord)
    {
        if (verdict == AnswerVerdict.Wrong)
        {
            return ReviewRating.Again;
        }

        if (verdict == AnswerVerdict.Almost || usedHint)
        {
            return ReviewRating.Hard;
        }

        int fast = kind is ExerciseKind.TypeWord or ExerciseKind.FillGap ? options.FastTypingMs : options.FastChoiceMs;
        return elapsedMs > 0 && elapsedMs <= fast && !firstSightOfNewWord ? ReviewRating.Easy : ReviewRating.Good;
    }

    /// <summary>
    /// Доли форматов по слабым местам. Часто путает формы слова — больше пропусков в предложениях (форма там видна в контексте);
    /// много опечаток — больше чистого ввода слова; на слух ошибается заметно чаще, чем при чтении, — больше заданий на слух.
    /// Пока ответов мало (меньше 20), берутся доли по умолчанию.
    /// </summary>
    public static ExercisePreferences Preferences(WeaknessProfile weakness)
    {
        ExercisePreferences preferences = ExercisePreferences.Default;
        if (weakness.TotalAnswers < 20)
        {
            return preferences;
        }

        double gap = preferences.GapShare;
        if (weakness.TagShare(nameof(ErrorTag.WordForm)) >= 0.05 || weakness.TagShare(nameof(ErrorTag.Preposition)) >= 0.05)
        {
            gap = 0.8;
        }
        else if (weakness.TagShare(nameof(ErrorTag.Typo)) >= 0.08)
        {
            gap = 0.35;
        }

        double listen = preferences.ListenShare;
        FormatStat? heard = weakness.Format(nameof(ExerciseKind.ListenChoose));
        FormatStat? read = weakness.Format(nameof(ExerciseKind.ChooseTranslation));
        if (heard is { Attempts: >= 5 } && read is not null && heard.ErrorRate > read.ErrorRate + 0.1)
        {
            listen = 0.5;
        }

        return new ExercisePreferences(listen, gap);
    }

    /// <summary>Начало сегодняшнего дня по часовому поясу пользователя, в UTC.</summary>
    public static DateTimeOffset DayStart(DateTimeOffset now, string timeZone)
    {
        TimeZoneInfo zone = TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out TimeZoneInfo? found) ? found : TimeZoneInfo.Utc;
        DateTimeOffset local = TimeZoneInfo.ConvertTime(now, zone);
        DateTime midnight = local.Date;
        return new DateTimeOffset(midnight, zone.GetUtcOffset(midnight)).ToUniversalTime();
    }

    private async Task<RuleDto?> RuleAsync(ErrorTag tag, CancellationToken cancellationToken)
    {
        if (!RuleTags.Contains(tag) || await hints.GetForTagAsync(tag.ToString(), cancellationToken) is not { } hint)
        {
            return null;
        }

        return new RuleDto(hint.TagTitle, hint.TopicSlug, hint.TopicTitle, hint.Rule);
    }

    private Task PublishAsync(
        Guid userId, SessionItem item, ExerciseTask task, CheckResult check, bool isWarmUp, int elapsedMs, DateTimeOffset now, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(
            new AnswerChecked(userId, item.UserWordId, item.CardId, item.Mode, task.Kind.ToString(), check.Verdict.ToString(), check.ErrorTag.ToString(), isWarmUp, elapsedMs, now),
            cancellationToken);

    private Task<LearningSession?> FindAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        Guid userId = currentUser.UserId;
        return db.Sessions.SingleOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, cancellationToken);
    }

    private Task<LearningSession?> FindActiveAsync(Guid userId, DateTimeOffset dayStart, string kind, CancellationToken cancellationToken) =>
        db.Sessions
            .Where(s => s.UserId == userId && s.Kind == kind && s.FinishedAt == null && s.StartedAt >= dayStart)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

    private static ExerciseTask Deserialize(string json) => JsonSerializer.Deserialize<ExerciseTask>(json, Json)!;

    private static NextStepDto Step(LearningSession session, TaskView task) => new(false, task, Progress(session), Stats(session));

    private static SessionProgress Progress(LearningSession session) =>
        new(Math.Min(session.Position, session.Queue.Count), session.Queue.Count);

    private static SessionStats Stats(LearningSession session) =>
        new(
            session.Answered,
            session.Correct,
            session.Almost,
            session.Wrong,
            session.NewWords,
            session.Reviews,
            (int)((session.FinishedAt ?? session.LastActivityAt) - session.StartedAt).TotalSeconds);

    private static SessionDto ToDto(LearningSession session) =>
        new(session.Id, session.Kind, session.StartedAt, session.FinishedAt, Progress(session), Stats(session));

    private static AnswerResultDto ToResult(
        ExerciseTask task, CheckResult check, int? rating, DateTimeOffset? nextReview, bool recallUnlocked, LearningSession session, RuleDto? rule, bool isWarmUp) =>
        new(check.Verdict, check.ErrorTag, check.CorrectAnswer, check.Explanation, rating, nextReview, recallUnlocked, ToWordView(task.Word), Progress(session), rule, isWarmUp);

    private static TaskView ToView(ExerciseTask task, SessionItem item) => new(
        task.TaskId,
        task.Kind,
        item.Mode,
        task.Prompt,
        task.PromptNote,
        task.Options,
        task.Sentence,
        task.SentenceTranslation,
        task.Hint,
        task.Kind is ExerciseKind.ListenChoose or ExerciseKind.Introduce ? task.Word.Text : null,
        task.Kind is ExerciseKind.ListenChoose or ExerciseKind.Introduce ? task.Word.AudioUrl : null,
        task.Kind == ExerciseKind.Introduce ? ToWordView(task.Word) : null,
        IsWarmUp: item.NeedsWarmUp,
        IsProblem: item.IsProblem);

    private static WordView ToWordView(ExerciseWord word) => new(
        word.UserWordId, word.Text, word.Type, word.PartOfSpeech, word.Level, word.Transcription, word.Translations, word.Example, word.ExampleTranslation, word.AudioUrl);

    private static ExerciseWord ToExerciseWord(LearningWord word) => new(
        word.Id, word.Text, word.Type, word.PartOfSpeech, word.Level, word.Transcription, word.Translations, word.Example, word.ExampleTranslation, word.AudioUrl);
}
