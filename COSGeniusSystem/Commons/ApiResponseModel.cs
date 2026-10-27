using System.Text.Json.Serialization;

namespace COSGeniusSystem.Commons;

public sealed class ApiResponseModel<T>
{
    [JsonPropertyName("code")]
    public int Code { get; }
    [JsonPropertyName("message")]
    public string Message { get; }
    [JsonPropertyName("isSuccess")]
    public bool IsSuccess { get; }
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public T? Data { get; }

    public ApiResponseModel(int code, string message, bool isSuccess, T? data)
    {
        if (code < 100 || code > 599 || isSuccess != (code >= 200 && code < 300))
            throw new ArgumentException("HTTP code and success flag must agree.");
        Code = code;
        Message = message;
        IsSuccess = isSuccess;
        Data = data;
    }

    public static ApiResponseModel<T> Success(SystemMessage message, T? data)
    {
        if (!message.IsSuccess || message.Code < 200 || message.Code >= 300)
            throw new ArgumentException("A successful HTTP message is required.", nameof(message));
        return new(message.Code, message.Message, true, data);
    }
}
