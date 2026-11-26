using EnglishPlatform.Identity.Application;
using Microsoft.Extensions.Logging;

namespace EnglishPlatform.Identity.Infrastructure.Services;

/// <summary>Заглушка почты: письмо со ссылкой сброса пароля пишется в лог. Для продакшена нужен SMTP.</summary>
internal sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IAuthEmailSender
{
    public Task SendPasswordResetAsync(string email, string resetLink, CancellationToken cancellationToken)
    {
        logger.LogInformation("Письмо сброса пароля для {Email}: {ResetLink}", email, resetLink);
        return Task.CompletedTask;
    }
}
