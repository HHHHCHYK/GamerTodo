namespace HeyeTodo.Server.Application.Common;

public sealed class ServiceResult
{
    public bool IsSuccess { get; init; }
    public string? Error { get; init; }

    public static ServiceResult Ok() => new() { IsSuccess = true };
    public static ServiceResult Fail(string error) => new() { IsSuccess = false, Error = error };
}

public sealed class ServiceResult<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }

    public static ServiceResult<T> Ok(T value) => new() { IsSuccess = true, Value = value };
    public static ServiceResult<T> Fail(string error) => new() { IsSuccess = false, Error = error };
}
