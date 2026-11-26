using System.Diagnostics.CodeAnalysis;

namespace EnglishPlatform.BuildingBlocks.Results;

/// <summary>Результат операции без значения.</summary>
public class Result
{
    protected Result(Error? error)
    {
        Error = error;
    }

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);

    public static Result Failure(Error error) => new(error);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>Результат операции со значением.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T? value, Error? error)
        : base(error)
    {
        _value = value;
    }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Нельзя читать значение неуспешного результата: {Error.Code}");

    public static Result<T> Success(T value) => new(value, null);

    public static new Result<T> Failure(Error error) => new(default, error);

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
