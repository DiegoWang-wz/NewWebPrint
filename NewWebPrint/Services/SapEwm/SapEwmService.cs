using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using NewWebPrint.Models.Api;

namespace NewWebPrint.Services.SapEwm;

public interface ISapEwmService
{
    Task<ApiResponse<SapEwmInvokeResult>> GetSaleInfoCMAsync(string so, CancellationToken cancellationToken = default);
    Task<ApiResponse<SapEwmInvokeResult>> GetProductInfoAsync(string po, string lang, CancellationToken cancellationToken = default);
    Task<ApiResponse<SapEwmInvokeResult>> GetProductInfo2000Async(string po, CancellationToken cancellationToken = default);
    Task<ApiResponse<SapEwmInvokeResult>> GetpurchaseInfoAsync(string po, string lineNo, string supplierCode, string lang, CancellationToken cancellationToken = default);
    Task<ApiResponse<SapEwmInvokeResult>> GetMaterialModelLangAsync(string oldCode, string newCode, string lang, CancellationToken cancellationToken = default);
    Task<ApiResponse<SapEwmInvokeResult>> GetHUAsync(string factory, string guid, CancellationToken cancellationToken = default);
    /// <summary>GetBatch：无参数，返回 SOAP 中的原始字符串见 <see cref="SapEwmInvokeResult.ResultText"/>。</summary>
    Task<ApiResponse<SapEwmInvokeResult>> GetBatchAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<SapEwmDynamicPayload>> GetBatchDynamicAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<SapEwmInvokeResult>> InvokeAsync(string methodName, IReadOnlyDictionary<string, string?> parameters, CancellationToken cancellationToken = default, bool logToBrowserConsole = false);
    /// <summary>通用动态 JSON：任意 SOAP 方法成功后，将 JsonResult 解析为 <see cref="SapEwmDynamicPayload"/>（根节点须为 JSON 对象）。</summary>
    Task<ApiResponse<SapEwmDynamicPayload>> InvokeDynamicAsync(string methodName, IReadOnlyDictionary<string, string?> parameters, CancellationToken cancellationToken = default, bool logToBrowserConsole = false);

    /// <summary>将一次 <see cref="InvokeDynamicAsync"/> 的结果按统一格式写入浏览器控制台（页面调试）。</summary>
    ValueTask LogDynamicInvokeToBrowserConsoleAsync(string methodName, IReadOnlyDictionary<string, string?> parameters, ApiResponse<SapEwmDynamicPayload> response, CancellationToken cancellationToken = default);

    /// <summary>将一次 <see cref="InvokeAsync"/> 的结果按统一格式写入浏览器控制台。</summary>
    ValueTask LogInvokeToBrowserConsoleAsync(string methodName, IReadOnlyDictionary<string, string?> parameters, ApiResponse<SapEwmInvokeResult> response, CancellationToken cancellationToken = default);
    Task<ApiResponse<SapEwmDynamicPayload>> GetProductInfoDynamicAsync(string po, string lang, CancellationToken cancellationToken = default);
}

public sealed class SapEwmService(HttpClient http, ILogger<SapEwmService> logger, IBrowserDevConsoleService browserConsole) : ISapEwmService
{
    private const string InternalEndpoint = "http://192.168.3.240:7639/SAP_RFC_EWM/WebService_ewm.asmx";
    private const string ExternalEndpoint = "http://61.175.247.114:7639/SAP_RFC_EWM/WebService_ewm.asmx";
    private const string ActionNamespace = "http://tempuri.org/";
    private const string BodyNamespace = "http://tempuri.org/";

    public Task<ApiResponse<SapEwmInvokeResult>> GetSaleInfoCMAsync(string so, CancellationToken cancellationToken = default) =>
        InvokeAsync("GetSaleInfoCM", new Dictionary<string, string?> { ["so"] = so }, cancellationToken);

    public Task<ApiResponse<SapEwmInvokeResult>> GetProductInfoAsync(string po, string lang, CancellationToken cancellationToken = default) =>
        InvokeAsync("GetProductInfo", new Dictionary<string, string?>
        {
            ["po"] = po,
            ["lang"] = lang
        }, cancellationToken);

    public Task<ApiResponse<SapEwmInvokeResult>> GetProductInfo2000Async(string po, CancellationToken cancellationToken = default) =>
        InvokeAsync("GetProductInfo2000", new Dictionary<string, string?> { ["po"] = po }, cancellationToken);

    public Task<ApiResponse<SapEwmInvokeResult>> GetpurchaseInfoAsync(string po, string lineNo, string supplierCode, string lang, CancellationToken cancellationToken = default) =>
        InvokeAsync("GetpurchaseInfo", new Dictionary<string, string?>
        {
            ["Po"] = po,
            ["lineNo"] = lineNo,
            ["supplierCode"] = supplierCode,
            ["lang"] = lang
        }, cancellationToken);

    public Task<ApiResponse<SapEwmInvokeResult>> GetMaterialModelLangAsync(string oldCode, string newCode, string lang, CancellationToken cancellationToken = default) =>
        InvokeAsync("GetMaterialModel_lang", new Dictionary<string, string?>
        {
            ["oldCode"] = oldCode,
            ["newCode"] = newCode,
            ["lang"] = lang
        }, cancellationToken);

    public Task<ApiResponse<SapEwmInvokeResult>> GetHUAsync(string factory, string guid, CancellationToken cancellationToken = default) =>
        InvokeAsync("GetHU", new Dictionary<string, string?>
        {
            ["factory"] = factory,
            ["guid"] = guid
        }, cancellationToken);

    public Task<ApiResponse<SapEwmInvokeResult>> GetBatchAsync(CancellationToken cancellationToken = default) =>
        InvokeAsync("GetBatch", new Dictionary<string, string?>(), cancellationToken);

    public Task<ApiResponse<SapEwmDynamicPayload>> GetBatchDynamicAsync(CancellationToken cancellationToken = default) =>
        InvokeDynamicAsync("GetBatch", new Dictionary<string, string?>(), cancellationToken);

    public Task<ApiResponse<SapEwmDynamicPayload>> GetProductInfoDynamicAsync(string po, string lang, CancellationToken cancellationToken = default) =>
        InvokeDynamicAsync("GetProductInfo", new Dictionary<string, string?>
        {
            ["po"] = po,
            ["lang"] = lang
        }, cancellationToken);

    public async Task<ApiResponse<SapEwmDynamicPayload>> InvokeDynamicAsync(
        string methodName,
        IReadOnlyDictionary<string, string?> parameters,
        CancellationToken cancellationToken = default,
        bool logToBrowserConsole = false)
    {
        var response = await InvokeAsync(methodName, parameters, cancellationToken).ConfigureAwait(false);
        ApiResponse<SapEwmDynamicPayload> final;
        if (response.Code != ApiResultCode.Success || response.Data is null)
            final = ApiResponse<SapEwmDynamicPayload>.Fail(response.Message, response.Code);
        else
        {
            var rawForParse = response.Data.ResultText ?? response.Data.JsonResult ?? string.Empty;
            var ep = response.Data.Endpoint;
            var nw = response.Data.Network;
            var sv = response.Data.SoapVersion;
            var dynamicPayload = SapEwmDynamicPayload.FromJson(rawForParse, response.Data.RawSoapResponse, ep, nw, sv);
            if (!dynamicPayload.Success)
                final = ApiResponse<SapEwmDynamicPayload>.Fail(dynamicPayload.Error ?? "JSON 解析失败", data: dynamicPayload);
            else
                final = ApiResponse<SapEwmDynamicPayload>.Ok(dynamicPayload);
        }

        if (logToBrowserConsole)
            await LogDynamicInvokeToBrowserConsoleAsync(methodName, parameters, final, cancellationToken).ConfigureAwait(false);
        return final;
    }

    public ValueTask LogDynamicInvokeToBrowserConsoleAsync(
        string methodName,
        IReadOnlyDictionary<string, string?> parameters,
        ApiResponse<SapEwmDynamicPayload> response,
        CancellationToken cancellationToken = default) =>
        browserConsole.LogSapEwmDynamicAsync(methodName, parameters, response, cancellationToken);

    public ValueTask LogInvokeToBrowserConsoleAsync(
        string methodName,
        IReadOnlyDictionary<string, string?> parameters,
        ApiResponse<SapEwmInvokeResult> response,
        CancellationToken cancellationToken = default) =>
        browserConsole.LogSapEwmInvokeAsync(methodName, parameters, response, cancellationToken);

    public async Task<ApiResponse<SapEwmInvokeResult>> InvokeAsync(
        string methodName,
        IReadOnlyDictionary<string, string?> parameters,
        CancellationToken cancellationToken = default,
        bool logToBrowserConsole = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(methodName))
                return ApiResponse<SapEwmInvokeResult>.BadRequest("methodName 不能为空。");

            var result = await InvokeInternalAsync(methodName.Trim(), parameters, cancellationToken).ConfigureAwait(false);
            var apiResponse = result.Success
                ? ApiResponse<SapEwmInvokeResult>.Ok(result)
                : ApiResponse<SapEwmInvokeResult>.Fail(result.Error ?? "调用失败", data: result);
            if (logToBrowserConsole)
                await LogInvokeToBrowserConsoleAsync(methodName, parameters, apiResponse, cancellationToken).ConfigureAwait(false);
            return apiResponse;
        }
        catch (OperationCanceledException)
        {
            return ApiResponse<SapEwmInvokeResult>.Canceled("请求已取消");
        }
        catch (Exception ex)
        {
            return ApiResponse<SapEwmInvokeResult>.Fail(ex.Message);
        }
    }

    private async Task<SapEwmInvokeResult> InvokeInternalAsync(
        string normalizedMethod,
        IReadOnlyDictionary<string, string?> parameters,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        foreach (var endpoint in new[] { InternalEndpoint, ExternalEndpoint })
        {
            foreach (var version in new[] { SoapVersion.Soap12, SoapVersion.Soap11 })
            {
                var network = endpoint == InternalEndpoint ? "内网" : "外网";
                var versionText = version == SoapVersion.Soap12 ? "SOAP 1.2" : "SOAP 1.1";

                try
                {
                    var soapAction = $"{ActionNamespace}{normalizedMethod}";
                    var soapXml = BuildSoapXml(normalizedMethod, parameters, version);

                    using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                    ConfigureRequest(request, soapXml, soapAction, version);

                    using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        errors.Add($"{network} {versionText} HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                        continue;
                    }

                    if (!TryExtractResult(responseText, normalizedMethod, out var middleResult, out var extractError))
                    {
                        errors.Add($"{network} {versionText} 响应解析失败：{extractError}");
                        continue;
                    }

                    var formattedJson = FormatAsPrettyJson(middleResult);
                    logger.LogInformation(
                        "SAP EWM 调用成功：{Method}，网络={Network}，协议={SoapVersion}",
                        normalizedMethod, network, versionText);

                    return new SapEwmInvokeResult(true, endpoint, network, versionText, responseText, null, formattedJson, middleResult);
                }
                catch (Exception ex)
                {
                    errors.Add($"{network} {versionText} 异常：{ex.Message}");
                }
            }
        }

        return new SapEwmInvokeResult(false, null, null, null, null, string.Join(" | ", errors), null, null);
    }

    private static void ConfigureRequest(HttpRequestMessage request, string soapXml, string soapAction, SoapVersion version)
    {
        if (version == SoapVersion.Soap12)
        {
            request.Content = new StringContent(soapXml, Encoding.UTF8);
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(
                $"application/soap+xml; charset=utf-8; action=\"{soapAction}\"");
            return;
        }

        request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{soapAction}\"");
        request.Content = new StringContent(soapXml, Encoding.UTF8, "text/xml");
    }

    private static string BuildSoapXml(string methodName, IReadOnlyDictionary<string, string?> parameters, SoapVersion version)
    {
        var sb = new StringBuilder();

        if (version == SoapVersion.Soap12)
        {
            sb.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
            sb.AppendLine(@"<soap12:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""");
            sb.AppendLine(@"                 xmlns:xsd=""http://www.w3.org/2001/XMLSchema""");
            sb.AppendLine(@"                 xmlns:soap12=""http://www.w3.org/2003/05/soap-envelope"">");
            sb.AppendLine(@"  <soap12:Body>");
            sb.AppendLine($@"    <{methodName} xmlns=""{BodyNamespace}"">");
            AppendParameters(sb, parameters);
            sb.AppendLine($@"    </{methodName}>");
            sb.AppendLine(@"  </soap12:Body>");
            sb.Append(@"</soap12:Envelope>");
            return sb.ToString();
        }

        sb.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
        sb.AppendLine(@"<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""");
        sb.AppendLine(@"               xmlns:xsd=""http://www.w3.org/2001/XMLSchema""");
        sb.AppendLine(@"               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">");
        sb.AppendLine(@"  <soap:Body>");
        sb.AppendLine($@"    <{methodName} xmlns=""{BodyNamespace}"">");
        AppendParameters(sb, parameters);
        sb.AppendLine($@"    </{methodName}>");
        sb.AppendLine(@"  </soap:Body>");
        sb.Append(@"</soap:Envelope>");
        return sb.ToString();
    }

    private static void AppendParameters(StringBuilder sb, IReadOnlyDictionary<string, string?> parameters)
    {
        foreach (var kv in parameters)
        {
            var key = kv.Key?.Trim();
            if (string.IsNullOrWhiteSpace(key))
                continue;
            sb.AppendLine($"      <{key}>{EscapeXml(kv.Value ?? string.Empty)}</{key}>");
        }
    }

    private static bool TryExtractResult(string xml, string methodName, out string middleResult, out string error)
    {
        middleResult = string.Empty;
        error = string.Empty;

        try
        {
            var targetNode = methodName + "Result";
            var doc = XDocument.Parse(xml);
            var resultElement = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == targetNode);
            if (resultElement == null)
            {
                error = $"没有找到 {targetNode} 节点。";
                return false;
            }

            middleResult = resultElement.Value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(middleResult))
            {
                error = $"{targetNode} 为空。";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string FormatAsPrettyJson(string raw)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        try
        {
            var node = JsonNode.Parse(raw);
            if (node is not null)
                return node.ToJsonString(options);
        }
        catch
        {
        }

        return JsonSerializer.Serialize(new { result = raw }, options);
    }

    private static string EscapeXml(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    private enum SoapVersion
    {
        Soap11,
        Soap12
    }
}

/// <param name="ResultText">SOAP 方法返回的原始文本（如 GetBatchResult），未做美化包装。</param>
public sealed record SapEwmInvokeResult(
    bool Success,
    string? Endpoint,
    string? Network,
    string? SoapVersion,
    string? RawSoapResponse,
    string? Error,
    string? JsonResult,
    string? ResultText);

public sealed record SapEwmDynamicPayload(
    bool Success,
    string RawJson,
    IReadOnlyDictionary<string, JsonElement> Fields,
    string? RawSoapResponse,
    string? Error,
    string? InvokeEndpoint = null,
    string? InvokeNetwork = null,
    string? InvokeSoapVersion = null)
{
    public static SapEwmDynamicPayload FromJson(
        string json,
        string? rawSoapResponse = null,
        string? invokeEndpoint = null,
        string? invokeNetwork = null,
        string? invokeSoapVersion = null)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new SapEwmDynamicPayload(false, string.Empty, new Dictionary<string, JsonElement>(), rawSoapResponse, "JsonResult 为空。", invokeEndpoint, invokeNetwork, invokeSoapVersion);

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return new SapEwmDynamicPayload(false, json, new Dictionary<string, JsonElement>(), rawSoapResponse, "根节点不是 JSON 对象。", invokeEndpoint, invokeNetwork, invokeSoapVersion);

            var map = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
                map[prop.Name] = prop.Value.Clone();

            return new SapEwmDynamicPayload(true, json, map, rawSoapResponse, null, invokeEndpoint, invokeNetwork, invokeSoapVersion);
        }
        catch (Exception ex)
        {
            return new SapEwmDynamicPayload(false, json, new Dictionary<string, JsonElement>(), rawSoapResponse, ex.Message, invokeEndpoint, invokeNetwork, invokeSoapVersion);
        }
    }

    public string? GetString(params string[] keys)
    {
        if (!TryGetByKeys(out var el, keys))
            return null;
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    public decimal? GetDecimal(params string[] keys)
    {
        if (!TryGetByKeys(out var el, keys))
            return null;

        if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var n))
            return n;
        if (el.ValueKind == JsonValueKind.String &&
            decimal.TryParse(el.GetString(), out var parsed))
            return parsed;
        return null;
    }

    private bool TryGetByKeys(out JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!string.IsNullOrWhiteSpace(key) && Fields.TryGetValue(key, out element))
                return true;
        }

        element = default;
        return false;
    }
}
