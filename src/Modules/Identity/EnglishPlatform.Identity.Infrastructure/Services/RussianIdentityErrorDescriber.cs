using Microsoft.AspNetCore.Identity;

namespace EnglishPlatform.Identity.Infrastructure.Services;

/// <summary>Тексты ошибок Identity на русском: они показываются пользователю под полем пароля.</summary>
internal sealed class RussianIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"Пароль не короче {length} символов" };

    public override IdentityError PasswordRequiresDigit() =>
        new() { Code = nameof(PasswordRequiresDigit), Description = "В пароле нужна хотя бы одна цифра" };

    public override IdentityError PasswordRequiresLower() =>
        new() { Code = nameof(PasswordRequiresLower), Description = "В пароле нужна хотя бы одна строчная буква" };

    public override IdentityError PasswordRequiresUpper() =>
        new() { Code = nameof(PasswordRequiresUpper), Description = "В пароле нужна хотя бы одна заглавная буква" };

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "В пароле нужен хотя бы один символ, кроме букв и цифр" };

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        new() { Code = nameof(PasswordRequiresUniqueChars), Description = $"В пароле нужно не меньше {uniqueChars} разных символов" };

    public override IdentityError DuplicateEmail(string email) =>
        new() { Code = nameof(DuplicateEmail), Description = "Пользователь с таким email уже зарегистрирован" };

    public override IdentityError InvalidEmail(string? email) =>
        new() { Code = nameof(InvalidEmail), Description = "Некорректный email" };

    public override IdentityError InvalidToken() =>
        new() { Code = nameof(InvalidToken), Description = "Ссылка недействительна или устарела" };
}
