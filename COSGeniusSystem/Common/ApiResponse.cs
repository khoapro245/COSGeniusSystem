namespace COSGeniusSystem.Common;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public ApiResponse()
    {
    }

    public ApiResponse(bool success, string message, T? data = default, List<string>? errors = null)
    {
        Success = success;
        Message = message;
        Data = data;
        Errors = errors;
        Timestamp = DateTime.UtcNow;
    }

    public static ApiResponse<T> Ok(T data, string message = "Success")
    {
        return new ApiResponse<T>(true, message, data);
    }

    public static ApiResponse<T> Fail(string message, List<string>? errors = null)
    {
        return new ApiResponse<T>(false, message, default, errors);
    }

    public static ApiResponse<T> Fail(string message, string error)
    {
        return new ApiResponse<T>(false, message, default, new List<string> { error });
    }
}
