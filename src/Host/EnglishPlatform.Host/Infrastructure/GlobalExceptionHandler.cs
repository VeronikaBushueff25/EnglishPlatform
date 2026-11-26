using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EnglishPlatform.Host.Infrastructure;

/// <summary>Необработанное исключение: пишем в лог целиком, клиенту отдаём ProblemDetails без подробностей.</summary>
internal sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        if (exception is BadHttpRequestException badRequest)
        {
            httpContext.Response.StatusCode = badRequest.StatusCode;
            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails { Status = badRequest.StatusCode, Detail = "Некорректный запрос" },
            });
        }

        logger.LogError(exception, "Необработанное исключение на {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Внутренняя ошибка сервера",
            },
        });
    }
}
