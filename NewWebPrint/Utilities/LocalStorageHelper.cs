using Blazored.LocalStorage;

namespace NewWebPrint.Utilities;

/// <summary>LocalStorage 便捷封装，便于在页面中存取信息。</summary>
public class LocalStorageHelper(ILocalStorageService storage)
{
    public async Task<string?> GetStringAsync(string key, CancellationToken ct = default) =>
        await storage.GetItemAsync<string?>(key, ct).ConfigureAwait(false);

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) =>
        await storage.GetItemAsync<T?>(key, ct).ConfigureAwait(false);

    public async Task<string> GetStringOrDefaultAsync(string key, string defaultValue = "", CancellationToken ct = default)
    {
        var v = await GetStringAsync(key, ct).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(v) ? defaultValue : v.Trim();
    }

    public async Task SetAsync(string key, string? value, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(value))
            await storage.RemoveItemAsync(key, ct).ConfigureAwait(false);
        else
            await storage.SetItemAsync(key, value.Trim(), ct).ConfigureAwait(false);
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken ct = default) =>
        await storage.SetItemAsync(key, value, ct).ConfigureAwait(false);

    public async Task RemoveAsync(string key, CancellationToken ct = default) =>
        await storage.RemoveItemAsync(key, ct).ConfigureAwait(false);

    public async Task<bool> ContainsKeyAsync(string key, CancellationToken ct = default) =>
        await storage.ContainKeyAsync(key, ct).ConfigureAwait(false);

    public async Task ClearAsync(CancellationToken ct = default) =>
        await storage.ClearAsync(ct).ConfigureAwait(false);

    /// <summary>获取打印代理 baseUrl，未设置时返回 null（表示使用 JS 端默认 localhost:5005）。</summary>
    public async Task<string?> GetPrintAgentBaseUrlAsync(CancellationToken ct = default)
    {
        var v = await GetStringAsync(StorageKeys.PrintAgentBaseUrl, ct).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }

    /// <summary>设置打印代理 baseUrl；传 null 或空则清除，使用默认。</summary>
    public async Task SetPrintAgentBaseUrlAsync(string? baseUrl, CancellationToken ct = default) =>
        await SetAsync(StorageKeys.PrintAgentBaseUrl, baseUrl, ct).ConfigureAwait(false);

    /// <summary>读取页面上次使用的打印机名称。</summary>
    public async Task<string?> GetLastUsedPrinterAsync(string storageKey, CancellationToken ct = default) =>
        await GetStringAsync(storageKey, ct).ConfigureAwait(false);

    /// <summary>保存页面上次使用的打印机名称。</summary>
    public Task SaveLastUsedPrinterAsync(string? printerName, string storageKey, CancellationToken ct = default) =>
        SetAsync(storageKey, printerName, ct);
}
