namespace OMS.Application.Common.Models;

public class Result
{
    protected Result(bool success, string? error)
    {
        Success = success;
        Error = error;
    }

    public bool Success { get; }
    public string? Error { get; }

    public static Result Ok() => new(true, null);
    public static Result Fail(string error) => new(false, error);
    public static Result<T> Ok<T>(T value) => Result<T>.Ok(value);
    public static Result<T> Fail<T>(string error) => Result<T>.Fail(error);
}

public class Result<T> : Result
{
    private Result(bool success, T? value, string? error) : base(success, error)
    {
        Value = value;
    }

    public T? Value { get; }

    public static Result<T> Ok(T value) => new(true, value, null);
    public new static Result<T> Fail(string error) => new(false, default, error);
}
