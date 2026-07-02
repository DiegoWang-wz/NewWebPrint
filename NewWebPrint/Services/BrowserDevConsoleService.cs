using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using NewWebPrint.Models.Api;
using NewWebPrint.Services.SapEwm;
using Microsoft.JSInterop;

namespace NewWebPrint.Services;

/// <summary>在 Blazor Server 交互会话中把信息输出到浏览器 F12 控制台；非组件上下文（如纯 API）调用时静默忽略。</summary>
public interface IBrowserDevConsoleService
{
    /// <summary>快捷输出：<c>console.log("[tag]", …)</c>，复杂对象会序列化为 JSON。</summary>
    ValueTask LogAsync(string tag, params object?[] values);

    /// <summary><c>console.warn</c>，参数规则同 <see cref="LogAsync"/>。</summary>
    ValueTask WarnAsync(string tag, params object?[] values);

    /// <summary>与 <see cref="ISapEwmService.InvokeDynamicAsync"/> 成功/失败时一致的 SAP EWM 动态结果格式。</summary>
    ValueTask LogSapEwmDynamicAsync(
        string methodName,
        IReadOnlyDictionary<string, string?> parameters,
        ApiResponse<SapEwmDynamicPayload> response,
        CancellationToken cancellationToken = default);

    /// <summary><see cref="ISapEwmService.InvokeAsync"/> 的 SOAP 原始结果格式（含内外网、SOAP 版本、业务文本）。</summary>
    ValueTask LogSapEwmInvokeAsync(
        string methodName,
        IReadOnlyDictionary<string, string?> parameters,
        ApiResponse<SapEwmInvokeResult> response,
        CancellationToken cancellationToken = default);
}

public sealed class BrowserDevConsoleService(
    IJSRuntime js,
    ILogger<BrowserDevConsoleService> logger) : IBrowserDevConsoleService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions PrettyConsoleJson = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async ValueTask LogAsync(string tag, params object?[] values)
    {
        try
        {
            var line = values.Length == 0
                ? $"[{tag}]"
                : $"[{tag}] " + string.Join(" ", values.Select(FormatArg));
            await js.InvokeVoidAsync("console.log", line).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "浏览器控制台 Log 不可用（可能不在 Blazor 交互上下文）");
        }
    }

    public async ValueTask WarnAsync(string tag, params object?[] values)
    {
        try
        {
            var line = values.Length == 0
                ? $"[{tag}]"
                : $"[{tag}] " + string.Join(" ", values.Select(FormatArg));
            await js.InvokeVoidAsync("console.warn", line).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "浏览器控制台 Warn 不可用（可能不在 Blazor 交互上下文）");
        }
    }

    public async ValueTask LogSapEwmDynamicAsync(
        string methodName,
        IReadOnlyDictionary<string, string?> parameters,
        ApiResponse<SapEwmDynamicPayload> response,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await js.InvokeVoidAsync("console.log", $"[SAP EWM] {methodName}").ConfigureAwait(false);
            await js.InvokeVoidAsync("console.log", "  参数", FormatParameters(parameters)).ConfigureAwait(false);
            await js.InvokeVoidAsync("console.log", "  Api Code:", response.Code, "Message:", response.Message).ConfigureAwait(false);

            var d = response.Data;
            if (d is null)
            {
                await js.InvokeVoidAsync("console.warn", "  无 Data（调用失败或未返回载荷）").ConfigureAwait(false);
                return;
            }

            await js.InvokeVoidAsync("console.log", "  实际走到的网络:", d.InvokeNetwork ?? "(无)").ConfigureAwait(false);
            await js.InvokeVoidAsync("console.log", "  SOAP 版本:", d.InvokeSoapVersion ?? "(无)").ConfigureAwait(false);
            if (!string.IsNullOrEmpty(d.RawJson))
                await js.InvokeVoidAsync("console.log", "  RawJson:", FormatJsonForConsole(d.RawJson)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "SAP EWM 动态结果写入浏览器控制台失败");
        }
    }

    public async ValueTask LogSapEwmInvokeAsync(
        string methodName,
        IReadOnlyDictionary<string, string?> parameters,
        ApiResponse<SapEwmInvokeResult> response,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await js.InvokeVoidAsync("console.log", $"[SAP EWM] {methodName} (Invoke)").ConfigureAwait(false);
            await js.InvokeVoidAsync("console.log", "  参数", FormatParameters(parameters)).ConfigureAwait(false);
            await js.InvokeVoidAsync("console.log", "  Api Code:", response.Code, "Message:", response.Message).ConfigureAwait(false);

            var d = response.Data;
            if (d is null)
            {
                await js.InvokeVoidAsync("console.warn", "  无 Data（调用失败或未返回载荷）").ConfigureAwait(false);
                return;
            }

            await js.InvokeVoidAsync("console.log", "  实际走到的网络:", d.Network ?? "(无)").ConfigureAwait(false);
            await js.InvokeVoidAsync("console.log", "  SOAP 版本:", d.SoapVersion ?? "(无)").ConfigureAwait(false);
            var body = !string.IsNullOrEmpty(d.JsonResult) ? d.JsonResult : d.ResultText;
            if (!string.IsNullOrEmpty(body))
                await js.InvokeVoidAsync("console.log", "  业务数据:", FormatJsonForConsole(body)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "SAP EWM Invoke 结果写入浏览器控制台失败");
        }
    }

    private static string FormatJsonForConsole(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;
        try
        {
            var node = JsonNode.Parse(raw);
            if (node is not null)
                return node.ToJsonString(PrettyConsoleJson);
        }
        catch (JsonException)
        {
        }

        return raw;
    }

    private static string FormatParameters(IReadOnlyDictionary<string, string?> parameters)
    {
        if (parameters.Count == 0)
            return "(无)";
        return string.Join("  ", parameters.Select(kv => $"{kv.Key}: {kv.Value ?? ""}"));
    }

    private static string FormatArg(object? value)
    {
        if (value is null)
            return "null";
        if (value is string s)
            return s;
        if (value is bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
        return JsonSerializer.Serialize(value, JsonOptions);
    }
}
