namespace NewWebPrint.Models.Api;

public static class ApiResultCode
{
    public const int Success = 1;
    public const int Failed = 0;
    public const int Canceled = -2;
    public const int NotFound = 404;
}

public class ApiResponse
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
}

public class ApiResponse<T>
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T? data, string message = "OK") =>
        new()
        {
            Code = ApiResultCode.Success,
            Message = message,
            Data = data
        };

    public static ApiResponse<T> Fail(string message, int code = ApiResultCode.Failed, T? data = default) =>
        new()
        {
            Code = code,
            Message = message,
            Data = data
        };

    public static ApiResponse<T> Canceled(string message = "Request canceled") =>
        new()
        {
            Code = ApiResultCode.Canceled,
            Message = message,
            Data = default
        };

    public static ApiResponse<T> NotFound(string message = "Not Found") =>
        new()
        {
            Code = ApiResultCode.NotFound,
            Message = message,
            Data = default
        };

    public static ApiResponse<T> BadRequest(string message = "Bad Request") =>
        new()
        {
            Code = ApiResultCode.Failed,
            Message = message,
            Data = default
        };
}
