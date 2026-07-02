using NewWebPrint.Models.Api;

namespace NewWebPrint.Utilities;

/// <summary>
/// 与 BlazorBasic.Utilities.ApiResponse 兼容的 HTTP 响应包装（ResultCode / Msg / ResultData）。
/// CordList 等对外 API 沿用此格式，避免外部调用方解析失败。
/// </summary>
public class LegacyApiResponse<T>
{
    public int ResultCode { get; set; }
    public string Msg { get; set; } = string.Empty;
    public T? ResultData { get; set; }

    public static LegacyApiResponse<T> From(ApiResponse<T> response) =>
        new()
        {
            ResultCode = response.Code,
            Msg = response.Message,
            ResultData = response.Data
        };
}
