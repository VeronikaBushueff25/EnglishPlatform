using System.ComponentModel.DataAnnotations;
using EnglishPlatform.BuildingBlocks.Results;
using EnglishPlatform.Grammar.Contracts;
using EnglishPlatform.Grammar.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnglishPlatform.Grammar.Application;

public interface IGrammarDb
{
    DbSet<GrammarTopic> Topics { get; }

    DbSet<ErrorTagInfo> ErrorTags { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record GrammarExampleDto(
    [property: Required, MaxLength(300)] string Text,
    [property: MaxLength(300)] string? Translation,
    bool IsWrong);

public sealed record GrammarTopicSummaryDto(Guid Id, string Slug, string Title, string? Level, string Summary);

public sealed record GrammarTopicDto(
    Guid Id,
    string Slug,
    string Title,
    string? Level,
    string Summary,
    string Body,
    IReadOnlyList<GrammarExampleDto> Examples,
    int SortOrder,
    IReadOnlyList<string> ErrorTags);

public sealed record ErrorTagDto(string Code, string Title, string Description, Guid? TopicId, string? TopicSlug, string? TopicTitle);

public sealed record SaveGrammarTopicRequest(
    [property: Required(ErrorMessage = "Введите идентификатор"), MaxLength(GrammarTopic.MaxSlugLength),
               RegularExpression("^[a-z0-9]+(-[a-z0-9]+)*$", ErrorMessage = "Латинские буквы, цифры и дефис, например articles")] string Slug,
    [property: Required(ErrorMessage = "Введите название"), MaxLength(GrammarTopic.MaxTitleLength)] string Title,
    [property: RegularExpression("^(A1|A2|B1|B2|C1|C2)$", ErrorMessage = "Уровень A1–C2")] string? Level,
    [property: Required(ErrorMessage = "Коротко сформулируйте правило"), MaxLength(GrammarTopic.MaxSummaryLength)] string Summary,
    [property: MaxLength(GrammarTopic.MaxBodyLength)] string? Body,
    [property: Required, MaxLength(GrammarTopic.MaxExamples)] IReadOnlyList<GrammarExampleDto> Examples,
    [property: Range(0, 1000)] int SortOrder) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (GrammarExampleDto example in Examples)
        {
            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(example, new ValidationContext(example), results, validateAllProperties: true))
            {
                foreach (ValidationResult result in results)
                {
                    yield return new ValidationResult(result.ErrorMessage, [nameof(Examples)]);
                }
            }
        }
    }

    public GrammarTopicData ToData() => new(
        Slug, Title, Level, Summary, Body ?? string.Empty, Examples.Select(e => new GrammarExample(e.Text.Trim(), e.Translation?.Trim(), e.IsWrong)).ToList(), SortOrder);
}

public sealed record UpdateErrorTagRequest(
    [property: Required(ErrorMessage = "Введите название"), MaxLength(ErrorTagInfo.MaxTitleLength)] string Title,
    [property: MaxLength(ErrorTagInfo.MaxDescriptionLength)] string? Description,
    Guid? TopicId);

public static class GrammarErrors
{
    public static readonly Error TopicNotFound = Error.NotFound("grammar.topic_not_found", "Тема не найдена");

    public static readonly Error TagNotFound = Error.NotFound("grammar.tag_not_found", "Тип ошибки не найден");

    public static readonly Error SlugTaken = Error.Conflict("grammar.slug_taken", "Тема с таким идентификатором уже есть");
}

/// <summary>Грамматические темы и справочник типов ошибок: чтение для всех, правка — администратору.</summary>
public sealed class GrammarService(IGrammarDb db, TimeProvider time)
{
    public async Task<IReadOnlyList<GrammarTopicSummaryDto>> ListTopicsAsync(CancellationToken cancellationToken) =>
        await db.Topics.AsNoTracking()
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Title)
            .Select(t => new GrammarTopicSummaryDto(t.Id, t.Slug, t.Title, t.Level, t.Summary))
            .ToListAsync(cancellationToken);

    public async Task<Result<GrammarTopicDto>> GetTopicAsync(string slugOrId, CancellationToken cancellationToken)
    {
        string slug = slugOrId.Trim().ToLowerInvariant();
        bool isId = Guid.TryParse(slugOrId, out Guid id);
        GrammarTopic? topic = await db.Topics.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Slug == slug || (isId && t.Id == id), cancellationToken);
        return topic is null ? GrammarErrors.TopicNotFound : await ToDtoAsync(topic, cancellationToken);
    }

    public async Task<IReadOnlyList<ErrorTagDto>> ListTagsAsync(CancellationToken cancellationToken)
    {
        List<ErrorTagInfo> tags = await db.ErrorTags.AsNoTracking().OrderBy(t => t.Code).ToListAsync(cancellationToken);
        Dictionary<Guid, GrammarTopic> topics = await db.Topics.AsNoTracking().ToDictionaryAsync(t => t.Id, cancellationToken);
        return tags.Select(t =>
            {
                GrammarTopic? topic = t.TopicId is { } topicId ? topics.GetValueOrDefault(topicId) : null;
                return new ErrorTagDto(t.Code, t.Title, t.Description, t.TopicId, topic?.Slug, topic?.Title);
            })
            .ToList();
    }

    public async Task<Result<GrammarTopicDto>> CreateTopicAsync(SaveGrammarTopicRequest request, CancellationToken cancellationToken)
    {
        string slug = request.Slug.Trim().ToLowerInvariant();
        if (await db.Topics.AnyAsync(t => t.Slug == slug, cancellationToken))
        {
            return GrammarErrors.SlugTaken;
        }

        var topic = GrammarTopic.Create(request.ToData(), time.GetUtcNow());
        db.Topics.Add(topic);
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(topic, cancellationToken);
    }

    public async Task<Result<GrammarTopicDto>> UpdateTopicAsync(Guid id, SaveGrammarTopicRequest request, CancellationToken cancellationToken)
    {
        GrammarTopic? topic = await db.Topics.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (topic is null)
        {
            return GrammarErrors.TopicNotFound;
        }

        string slug = request.Slug.Trim().ToLowerInvariant();
        if (await db.Topics.AnyAsync(t => t.Slug == slug && t.Id != id, cancellationToken))
        {
            return GrammarErrors.SlugTaken;
        }

        topic.Update(request.ToData(), time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(topic, cancellationToken);
    }

    /// <summary>Удаляет тему; типы ошибок, которые на неё ссылались, остаются без темы.</summary>
    public async Task<Result> DeleteTopicAsync(Guid id, CancellationToken cancellationToken)
    {
        GrammarTopic? topic = await db.Topics.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (topic is null)
        {
            return GrammarErrors.TopicNotFound;
        }

        foreach (ErrorTagInfo tag in await db.ErrorTags.Where(t => t.TopicId == id).ToListAsync(cancellationToken))
        {
            tag.Update(tag.Title, tag.Description, null);
        }

        db.Topics.Remove(topic);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<ErrorTagDto>> UpdateTagAsync(string code, UpdateErrorTagRequest request, CancellationToken cancellationToken)
    {
        ErrorTagInfo? tag = await db.ErrorTags.SingleOrDefaultAsync(t => t.Code == code, cancellationToken);
        if (tag is null)
        {
            return GrammarErrors.TagNotFound;
        }

        GrammarTopic? topic = null;
        if (request.TopicId is { } topicId)
        {
            topic = await db.Topics.AsNoTracking().SingleOrDefaultAsync(t => t.Id == topicId, cancellationToken);
            if (topic is null)
            {
                return GrammarErrors.TopicNotFound;
            }
        }

        tag.Update(request.Title, request.Description ?? string.Empty, request.TopicId);
        await db.SaveChangesAsync(cancellationToken);
        return new ErrorTagDto(tag.Code, tag.Title, tag.Description, tag.TopicId, topic?.Slug, topic?.Title);
    }

    private async Task<GrammarTopicDto> ToDtoAsync(GrammarTopic topic, CancellationToken cancellationToken)
    {
        List<string> tags = await db.ErrorTags.AsNoTracking().Where(t => t.TopicId == topic.Id).Select(t => t.Code).ToListAsync(cancellationToken);
        return new GrammarTopicDto(
            topic.Id,
            topic.Slug,
            topic.Title,
            topic.Level,
            topic.Summary,
            topic.Body,
            topic.Examples.Select(e => new GrammarExampleDto(e.Text, e.Translation, e.IsWrong)).ToList(),
            topic.SortOrder,
            tags);
    }
}

public sealed class GrammarHints(IGrammarDb db) : IGrammarHints
{
    public async Task<IReadOnlyList<GrammarTopicRef>> ListTopicsAsync(CancellationToken cancellationToken) =>
        await db.Topics.AsNoTracking()
            .OrderBy(t => t.SortOrder)
            .Select(t => new GrammarTopicRef(t.Slug, t.Title, t.Level, t.Summary))
            .ToListAsync(cancellationToken);

    public async Task<GrammarHint?> GetForTagAsync(string tagCode, CancellationToken cancellationToken) =>
        (await ListAsync(cancellationToken)).FirstOrDefault(h => h.TagCode == tagCode);

    public async Task<IReadOnlyList<GrammarHint>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await (
                from tag in db.ErrorTags.AsNoTracking()
                join topic in db.Topics.AsNoTracking() on tag.TopicId equals topic.Id into joined
                from topic in joined.DefaultIfEmpty()
                select new { tag.Code, tag.Title, Slug = (string?)topic!.Slug, TopicTitle = (string?)topic!.Title, Rule = (string?)topic!.Summary })
            .ToListAsync(cancellationToken);
        return rows.Select(r => new GrammarHint(r.Code, r.Title, r.Slug, r.TopicTitle, r.Rule)).ToList();
    }
}
