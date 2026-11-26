using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace EnglishPlatform.BuildingBlocks.Infrastructure.Http;

/// <summary>
/// Серверная валидация тела запроса по атрибутам DataAnnotations.
/// Вешается на эндпоинт: <c>.WithValidation&lt;RegisterRequest&gt;()</c>.
/// </summary>
public sealed class ValidationFilter<T> : IEndpointFilter
    where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        T? argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null)
        {
            return TypedResults.Problem(detail: "Пустое тело запроса", statusCode: StatusCodes.Status400BadRequest);
        }

        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(argument, new ValidationContext(argument), results, validateAllProperties: true))
        {
            Dictionary<string, string[]> errors = results
                .SelectMany(r => (r.MemberNames.Any() ? r.MemberNames : [string.Empty])
                    .Select(member => (Member: member, Message: r.ErrorMessage ?? "Некорректное значение")))
                .GroupBy(x => ToCamelCase(x.Member))
                .ToDictionary(g => g.Key, g => g.Select(x => x.Message).ToArray());

            return TypedResults.ValidationProblem(errors);
        }

        return await next(context);
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}

public static class ValidationFilterExtensions
{
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder)
        where T : class =>
        builder.AddEndpointFilter<ValidationFilter<T>>().ProducesValidationProblem();
}
