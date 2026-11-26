namespace EnglishPlatform.BuildingBlocks.Results;

/// <summary>Категория ошибки. По ней Host выбирает HTTP-статус.</summary>
public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
}

/// <summary>Ошибка бизнес-операции. Code стабилен и годится для клиента, Message для человека.</summary>
public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    public IReadOnlyDictionary<string, string[]>? Details { get; init; }

    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? details = null) =>
        new(code, message, ErrorType.Validation) { Details = details };

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
}
