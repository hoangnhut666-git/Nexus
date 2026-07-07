namespace Nexus.Services.Categories.Models;

public sealed record ServiceResult<T>(bool Success, T? Data, string? Error)
{
    public static ServiceResult<T> Ok(T data) => new(true, data, null);

    public static ServiceResult<T> Fail(string error) => new(false, default, error);
}
