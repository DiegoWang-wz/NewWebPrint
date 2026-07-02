using Blazored.SessionStorage;

namespace NewWebPrint.Utilities;

/// <summary>SessionStorage 便捷封装，便于在页面中存取会话级信息。</summary>
public class SessionStorageHelper(ISessionStorageService storage)
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
}
