using System.Text;
using NewWebPrint.Models.Hu;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewWebPrint.Services.Hu;

public interface IHuLabelService
{
    Task<(bool ok, string message, List<HuDataItem> items)> QueryAsync(
        string inboundDelivery,
        string purchaseOrder,
        CancellationToken cancellationToken = default);
}

/// <summary>对齐 WinForms LabelPrintForHu：POST JSON + Newtonsoft 解析 T_DATA（数字字段可落到 string）。</summary>
public sealed class HuLabelService(IHttpClientFactory httpClientFactory, IConfiguration config) : IHuLabelService
{
    public async Task<(bool ok, string message, List<HuDataItem> items)> QueryAsync(
        string inboundDelivery,
        string purchaseOrder,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = config["HuApi:BaseUrl"]?.Trim().TrimEnd('/');
        var path = config["HuApi:Path"]?.Trim();
        var auth = config["HuApi:Authorization"]?.Trim();

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(path))
            return (false, "未配置 HuApi:BaseUrl / HuApi:Path", []);

        // 与 WinForms 一致：对象序列化请求体
        var jsonBody = JsonConvert.SerializeObject(new
        {
            VBELN = inboundDelivery?.Trim() ?? "",
            EBELN = purchaseOrder?.Trim() ?? ""
        });

        try
        {
            var client = httpClientFactory.CreateClient(nameof(HuLabelService));
            using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl + path);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            if (!string.IsNullOrWhiteSpace(auth))
                request.Headers.TryAddWithoutValidation("Authorization", auth);

            using var resp = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var respText = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
                return (false, $"请求失败：HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}", []);

            if (string.IsNullOrWhiteSpace(respText))
                return (false, "返回内容为空", []);

            // 与 WinForms 一致：
            // var root = JsonConvert.DeserializeObject<Dictionary<string, object>>(respText);
            // var tData = JsonConvert.DeserializeObject<List<DataItem>>(root["T_DATA"].ToString());
            var root = JsonConvert.DeserializeObject<Dictionary<string, object>>(respText);
            if (root is null || !root.TryGetValue("T_DATA", out var tDataToken) || tDataToken is null)
                return (false, "返回格式不正确：缺少 T_DATA", []);

            var tDataJson = tDataToken switch
            {
                string s => s,
                JToken jt => jt.ToString(Formatting.None),
                _ => tDataToken.ToString() ?? "[]"
            };

            var items = JsonConvert.DeserializeObject<List<HuDataItem>>(tDataJson) ?? [];
            if (items.Count == 0)
                return (false, "无法查询订单信息", []);

            // 对齐 WinForms 填表：描述去换行；空值兜底
            foreach (var item in items)
            {
                item.VBELN ??= "";
                item.POSNR ??= "";
                item.LIFNR ??= "";
                item.EBELN ??= "";
                item.EBELP ??= "";
                item.WERKS ??= "";
                item.MATNR ??= "";
                item.MAKTX = (item.MAKTX ?? "").Replace("\n", " ");
                item.ZCTEXT ??= "";
                item.LFIMG ??= "";
                item.MEINS ??= "";
                item.CHARG ??= "";
                item.VBELV ??= "";
                item.POSNV ??= "";
                item.LGORT ??= "";
                item.BSTRF ??= "";
                item.MQP ??= "";
                item.IDNLF ??= "";
            }

            return (true, $"查询成功，共 {items.Count} 行", items);
        }
        catch (Exception ex)
        {
            return (false, $"查询异常：{ex.Message}", []);
        }
    }
}
