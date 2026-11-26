using EnglishPlatform.BuildingBlocks.Results;
using Microsoft.AspNetCore.Http;

namespace EnglishPlatform.BuildingBlocks.Infrastructure.Http;

/// <summary>Переводит Result в HTTP-ответ. Ошибки уходят в формате ProblemDetails с полем code.</summary>
public static class ResultHttpExtensions
{
    public static IResult ToHttpResult(this Result result) =>
        result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();

    public static IResult ToHttpResult<T>(this Result<T> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();

    public static IResult ToProblem(this Error error)
    {
        int status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError,
        };

        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };

        if (error.Details is { Count: > 0 })
        {
            return TypedResults.ValidationProblem(
                error.Details.ToDictionary(),
                detail: error.Message,
                extensions: extensions);
        }

        return TypedResults.Problem(detail: error.Message, statusCode: status, extensions: extensions);
    }
}
