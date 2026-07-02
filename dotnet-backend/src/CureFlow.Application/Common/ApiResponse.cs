namespace CureFlow.Application.Common;

/// <summary>Generic API response envelope.</summary>
public class ApiResponse<T>
{
    public bool Success { get; set; } = true;
    public string? Message { get; set; }
    public T? Data { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }

    public static ApiResponse<T> Ok(T data, string? msg = null) => new() { Data = data, Message = msg };
    public static ApiResponse<T> Fail(string msg, Dictionary<string, string[]>? errors = null) =>
        new() { Success = false, Message = msg, Errors = errors };
}
