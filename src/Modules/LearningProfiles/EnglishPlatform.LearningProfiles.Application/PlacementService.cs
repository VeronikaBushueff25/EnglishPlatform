using EnglishPlatform.BuildingBlocks.Paging;
using EnglishPlatform.BuildingBlocks.Results;
using EnglishPlatform.BuildingBlocks.Security;
using EnglishPlatform.Grammar.Contracts;
using EnglishPlatform.LearningProfiles.Domain;
using EnglishPlatform.Users.Contracts;
using EnglishPlatform.Vocabulary.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EnglishPlatform.LearningProfiles.Application;

public interface ILearningProfilesDb
{
    DbSet<PlacementQuestion> Questions { get; }

    DbSet<PlacementAttempt> Attempts { get; }

    DbSet<SkillLevel> SkillLevels { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class PlacementOptions
{
    public const string Section = "Placement";

    /// <summary>Сколько слов добавить в словарь после первого теста.</summary>
    public int StarterWords { get; set; } = 20;

    /// <summary>Незавершённый тест старше этого срока начинается заново.</summary>
    public int ResumeHours { get; set; } = 24;
}

/// <summary>Тест уровня: адаптивные вопросы по навыкам, итог по CEFR, стартовый набор слов по уровню и целям.</summary>
public sealed class PlacementService(
    ILearningProfilesDb db,
    ICurrentUser currentUser,
    IUserLearningSettings settings,
    IStarterWords starterWords,
    IGrammarHints grammar,
    PlacementOptions options,
    TimeProvider time)
{
    private const string PlacementSource = "Placement";

    /// <summary>Какие категории словаря ближе к цели обучения из профиля.</summary>
    private static readonly Dictionary<string, string[]> GoalCategories = new()
    {
        ["Conversation"] = ["communication", "people", "everyday"],
        ["Travel"] = ["travel", "food", "shopping"],
        ["Work"] = ["work", "communication"],
        ["Exams"] = ["everyday", "communication", "time"],
        ["Reading"] = ["everyday", "emotions"],
        ["MoviesAndSeries"] = ["emotions", "people", "communication"],
        ["Writing"] = ["communication", "work"],
    };

    public async Task<Result<PlacementStepDto>> StartAsync(CancellationToken cancellationToken)
    {
        Guid userId = currentUser.UserId;
        DateTimeOffset now = time.GetUtcNow();

        DateTimeOffset resumeFrom = now.AddHours(-options.ResumeHours);
        PlacementAttempt? attempt = await db.Attempts
            .Where(a => a.UserId == userId && a.FinishedAt == null && a.StartedAt >= resumeFrom)
            .OrderByDescending(a => a.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        PlacementPlan plan = await PlanAsync(cancellationToken);
        if (plan.TotalQuestions == 0)
        {
            return PlacementErrors.EmptyBank;
        }

        if (attempt is null)
        {
            attempt = PlacementAttempt.Start(userId, plan.TotalQuestions, now);
            db.Attempts.Add(attempt);
        }

        PlacementStepDto step = await StepAsync(attempt, plan, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return step;
    }

    public async Task<Result<PlacementStepDto>> AnswerAsync(Guid attemptId, PlacementAnswerRequest request, CancellationToken cancellationToken)
    {
        PlacementAttempt? attempt = await FindAsync(attemptId, cancellationToken);
        if (attempt is null)
        {
            return PlacementErrors.AttemptNotFound;
        }

        if (attempt.IsFinished)
        {
            return PlacementErrors.Finished;
        }

        PlacementPlan plan = await PlanAsync(cancellationToken);

        // Повторная отправка ответа на уже отвеченный вопрос (обрыв связи) — просто отдаём текущий шаг.
        if (attempt.Answers.Any(a => a.QuestionId == request.QuestionId))
        {
            PlacementStepDto again = await StepAsync(attempt, plan, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return again;
        }

        if (attempt.Current is not { } current || current.QuestionId != request.QuestionId)
        {
            return PlacementErrors.StaleQuestion;
        }

        PlacementQuestion? question = await db.Questions.AsNoTracking().SingleOrDefaultAsync(q => q.Id == current.QuestionId, cancellationToken);
        if (question is null)
        {
            return PlacementErrors.QuestionNotFound;
        }

        int chosen = request.Option >= 0 && request.Option < current.Order.Count ? current.Order[request.Option] : -1;
        attempt.Answer(new PlacementAnswer(question.Id, question.Skill, question.Level, chosen, chosen == question.CorrectOption, time.GetUtcNow()));

        PlacementStepDto step = await StepAsync(attempt, plan, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return step;
    }

    /// <summary>
    /// Итог: уровни по навыкам сохраняются в профиль. После первого теста в словарь добавляется стартовый набор —
    /// слова своего и следующего уровня, сначала из категорий под цели обучения.
    /// </summary>
    public async Task<Result<PlacementResultDto>> FinishAsync(Guid attemptId, CancellationToken cancellationToken)
    {
        PlacementAttempt? attempt = await FindAsync(attemptId, cancellationToken);
        if (attempt is null)
        {
            return PlacementErrors.AttemptNotFound;
        }

        Guid userId = currentUser.UserId;
        DateTimeOffset now = time.GetUtcNow();
        List<SkillLevel> levels = await db.SkillLevels.Where(l => l.UserId == userId).ToListAsync(cancellationToken);
        bool firstPlacement = levels.Count == 0;

        if (!attempt.IsFinished)
        {
            IReadOnlyDictionary<Skill, Cefr> result = attempt.Finish(Enum.GetValues<Skill>(), now);
            foreach ((Skill skill, Cefr level) in result)
            {
                SkillLevel? row = levels.FirstOrDefault(l => l.Skill == skill);
                if (row is null)
                {
                    row = SkillLevel.Create(userId, skill);
                    db.SkillLevels.Add(row);
                    levels.Add(row);
                }

                row.Set(level, PlacementSource, now);
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        int added = 0;
        if (firstPlacement && levels.Count > 0)
        {
            Cefr vocabulary = levels.FirstOrDefault(l => l.Skill == Skill.Vocabulary)?.Level ?? PlacementEngine.Overall(levels.Select(l => l.Level));
            UserLearningSettings s = await settings.GetAsync(userId, cancellationToken);
            IReadOnlyList<string> categories = s.Goals.SelectMany(g => GoalCategories.GetValueOrDefault(g, [])).Distinct().ToList();
            StarterWordsResult starter = await starterWords.AddAsync(userId, WordLevels(vocabulary), categories, options.StarterWords, cancellationToken);
            added = starter.Added;
        }

        return new PlacementResultDto(
            attempt.Id,
            PlacementEngine.Overall(levels.Select(l => l.Level)),
            levels.OrderBy(l => (int)l.Skill).Select(ToDto).ToList(),
            attempt.Answers.Count(a => a.Correct),
            attempt.Answers.Count,
            added);
    }

    public async Task<LearningPlanDto> GetPlanAsync(CancellationToken cancellationToken)
    {
        Guid userId = currentUser.UserId;
        // Навык хранится строкой, поэтому порядок «лексика, грамматика, чтение» задаём уже в памяти.
        List<SkillLevel> levels = (await db.SkillLevels.AsNoTracking().Where(l => l.UserId == userId).ToListAsync(cancellationToken))
            .OrderBy(l => (int)l.Skill)
            .ToList();
        UserLearningSettings s = await settings.GetAsync(userId, cancellationToken);

        if (levels.Count == 0)
        {
            return new LearningPlanDto(false, null, [], [], [], s.DailyNewWords, s.DailyReviews, s.Goals, null);
        }

        Cefr overall = PlacementEngine.Overall(levels.Select(l => l.Level));
        Cefr grammarLevel = levels.FirstOrDefault(l => l.Skill == Skill.Grammar)?.Level ?? overall;
        Cefr vocabulary = levels.FirstOrDefault(l => l.Skill == Skill.Vocabulary)?.Level ?? overall;

        // Грамматика: темы своего уровня и чуть выше — то, что пора закрепить; самые близкие к уровню первыми.
        IReadOnlyList<GrammarTopicRef> topics = await grammar.ListTopicsAsync(cancellationToken);
        List<GrammarRecommendationDto> recommended = topics
            .Select(t => (Topic: t, Level: Enum.TryParse(t.Level, out Cefr level) ? level : (Cefr?)null))
            .Where(t => t.Level is null || (int)t.Level <= (int)grammarLevel + 1)
            .OrderBy(t => t.Level is null ? 99 : Math.Abs((int)t.Level.Value - (int)grammarLevel - 0.4))
            .Take(4)
            .Select(t => new GrammarRecommendationDto(t.Topic.Slug, t.Topic.Title, t.Topic.Level, t.Topic.Summary))
            .ToList();

        return new LearningPlanDto(
            true,
            overall,
            levels.Select(ToDto).ToList(),
            WordLevels(vocabulary),
            recommended,
            s.DailyNewWords,
            s.DailyReviews,
            s.Goals,
            levels.Max(l => l.AssessedAt));
    }

    /// <summary>Слова своего уровня и следующего: свои — закрепить, следующие — расти.</summary>
    public static IReadOnlyList<string> WordLevels(Cefr level) =>
        level == Cefr.C2 ? [Cefr.C1.ToString(), Cefr.C2.ToString()] : [level.ToString(), ((Cefr)((int)level + 1)).ToString()];

    private async Task<PlacementPlan> PlanAsync(CancellationToken cancellationToken)
    {
        Dictionary<Skill, int> available = await db.Questions.AsNoTracking()
            .Where(q => q.IsActive)
            .GroupBy(q => q.Skill)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);

        // Навык без вопросов пропускается; вопросов меньше, чем задумано, — задаём сколько есть.
        PlacementPlan plan = PlacementPlan.Default;
        return plan with
        {
            Skills = plan.Skills
                .Select(s => (s.Skill, Questions: Math.Min(s.Questions, available.GetValueOrDefault(s.Skill))))
                .Where(s => s.Questions > 0)
                .ToList(),
        };
    }

    private async Task<PlacementStepDto> StepAsync(PlacementAttempt attempt, PlacementPlan plan, CancellationToken cancellationToken)
    {
        attempt.ShortenTo(plan.TotalQuestions);
        var progress = new PlacementProgress(attempt.Answers.Count, attempt.TotalQuestions);

        if (attempt.Current is { } current
            && await db.Questions.AsNoTracking().SingleOrDefaultAsync(q => q.Id == current.QuestionId, cancellationToken) is { } asked)
        {
            return new PlacementStepDto(attempt.Id, false, View(asked, current.Order), progress);
        }

        if (PlacementEngine.NextStep(attempt.Answers, plan) is not (var skill, var level))
        {
            return new PlacementStepDto(attempt.Id, true, null, progress);
        }

        HashSet<Guid> used = attempt.Answers.Select(a => a.QuestionId).ToHashSet();
        List<PlacementQuestion> pool = (await db.Questions.AsNoTracking()
                .Where(q => q.IsActive && q.Skill == skill)
                .ToListAsync(cancellationToken))
            .Where(q => !used.Contains(q.Id))
            .ToList();

        if (pool.Count == 0)
        {
            // Вопросы навыка закончились раньше плана — считаем навык пройденным.
            PlacementPlan shorter = plan with
            {
                Skills = plan.Skills.Select(s => s.Skill == skill ? (s.Skill, attempt.Answers.Count(a => a.Skill == skill)) : s).ToList(),
            };
            return await StepAsync(attempt, shorter, cancellationToken);
        }

        // Ближайший к нужному уровень; при равенстве — уровень ниже (лучше недооценить, чем перегрузить).
        PlacementQuestion question = pool
            .OrderBy(q => Math.Abs((int)q.Level - (int)level))
            .ThenBy(q => q.Level)
            .ThenBy(_ => Random.Shared.Next())
            .First();

        List<int> order = Enumerable.Range(0, question.Options.Count).OrderBy(_ => Random.Shared.Next()).ToList();
        attempt.Ask(new PlacementCurrent(question.Id, order));
        return new PlacementStepDto(attempt.Id, false, View(question, order), progress);
    }

    private static PlacementQuestionView View(PlacementQuestion question, IReadOnlyList<int> order) =>
        new(question.Id, question.Skill, question.Prompt, question.Passage, order.Select(i => question.Options[i]).ToList());

    private static SkillLevelDto ToDto(SkillLevel level) => new(level.Skill, level.Level, level.AssessedAt, level.Source);

    private Task<PlacementAttempt?> FindAsync(Guid attemptId, CancellationToken cancellationToken)
    {
        Guid userId = currentUser.UserId;
        return db.Attempts.SingleOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, cancellationToken);
    }
}

/// <summary>Банк вопросов теста в админке.</summary>
public sealed class PlacementQuestionsAdmin(ILearningProfilesDb db, TimeProvider time)
{
    public async Task<PagedResult<PlacementQuestionDto>> ListAsync(Skill? skill, Cefr? level, PageRequest page, CancellationToken cancellationToken)
    {
        IQueryable<PlacementQuestion> query = db.Questions.AsNoTracking();
        if (skill is { } s)
        {
            query = query.Where(q => q.Skill == s);
        }

        if (level is { } l)
        {
            query = query.Where(q => q.Level == l);
        }

        long total = await query.LongCountAsync(cancellationToken);
        List<PlacementQuestion> items = await query.OrderBy(q => q.Skill).ThenBy(q => q.Level).ThenBy(q => q.Id)
            .Skip(page.Skip).Take(page.SafePageSize).ToListAsync(cancellationToken);
        return new PagedResult<PlacementQuestionDto>(items.Select(ToDto).ToList(), page.SafePage, page.SafePageSize, total);
    }

    public async Task<Result<PlacementQuestionDto>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Questions.AsNoTracking().SingleOrDefaultAsync(q => q.Id == id, cancellationToken) is { } question
            ? ToDto(question)
            : PlacementErrors.QuestionNotFound;

    public async Task<PlacementQuestionDto> CreateAsync(SavePlacementQuestionRequest request, CancellationToken cancellationToken)
    {
        var question = PlacementQuestion.Create(request.ToData(), time.GetUtcNow());
        db.Questions.Add(question);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(question);
    }

    public async Task<Result<PlacementQuestionDto>> UpdateAsync(Guid id, SavePlacementQuestionRequest request, CancellationToken cancellationToken)
    {
        PlacementQuestion? question = await db.Questions.SingleOrDefaultAsync(q => q.Id == id, cancellationToken);
        if (question is null)
        {
            return PlacementErrors.QuestionNotFound;
        }

        question.Update(request.ToData(), time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(question);
    }

    /// <summary>Удаляет вопрос. Ответы в прошлых тестах остаются (в них сохранены навык и уровень).</summary>
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        PlacementQuestion? question = await db.Questions.SingleOrDefaultAsync(q => q.Id == id, cancellationToken);
        if (question is null)
        {
            return PlacementErrors.QuestionNotFound;
        }

        db.Questions.Remove(question);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static PlacementQuestionDto ToDto(PlacementQuestion q) =>
        new(q.Id, q.Skill, q.Level, q.Prompt, q.Passage, q.Options, q.CorrectOption, q.IsActive);
}

public sealed class SkillLevelsReader(ILearningProfilesDb db) : EnglishPlatform.LearningProfiles.Contracts.ISkillLevelsReader
{
    public async Task<IReadOnlyList<EnglishPlatform.LearningProfiles.Contracts.SkillLevelInfo>> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        (await db.SkillLevels.AsNoTracking().Where(l => l.UserId == userId).ToListAsync(cancellationToken))
            .OrderBy(l => (int)l.Skill)
            .Select(l => new EnglishPlatform.LearningProfiles.Contracts.SkillLevelInfo(l.Skill.ToString(), l.Level.ToString(), l.AssessedAt))
            .ToList();
}
