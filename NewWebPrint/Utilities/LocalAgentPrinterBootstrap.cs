using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace NewWebPrint.Utilities;

/// <summary>
/// 进入打印页时拉取 LocalAgent 打印机；失败时提示打开或下载代理（与 WebLabelDesign 一致）。
/// </summary>
public sealed class LocalAgentPrinterBootstrap(
    IJSRuntime js,
    LocalStorageHelper storage,
    SnackbarHelper snackbar,
    IDialogService dialogService,
    NavigationManager navigation)
{
    public const int LocalAgentOpenWaitMs = 8000;
    public const int PrintAgentPort = 5005;

    public Task TryLoadOnFirstRenderAsync(
        bool firstRender,
        string lastUsedStorageKey,
        Action<PrinterLoadResult> applyResult,
        Func<Task>? notifyStateChanged = null)
    {
        if (!firstRender)
            return Task.CompletedTask;

        return TryLoadOnEnterAsync(lastUsedStorageKey, applyResult, notifyStateChanged);
    }

    public async Task TryLoadOnEnterAsync(
        string lastUsedStorageKey,
        Action<PrinterLoadResult> applyResult,
        Func<Task>? notifyStateChanged = null)
    {
        var result = await LoadPrintersWithFeedbackAsync(lastUsedStorageKey);
        if (result.Success)
        {
            applyResult(result);
            if (notifyStateChanged is not null)
                await notifyStateChanged();
            return;
        }

        await PromptWhenPrintersUnavailableAsync(lastUsedStorageKey, applyResult, notifyStateChanged);
    }

    private async Task<PrinterLoadResult> LoadPrintersWithFeedbackAsync(
        string lastUsedStorageKey,
        bool showSuccessToast = true)
    {
        const string loadingHint = "正在连接打印代理，获取打印机列表…";
        var loadingToast = snackbar.ShowRetained(loadingHint, Severity.Info);

        try
        {
            return await PrinterHelper.LoadPrintersAsync(
                js, storage, lastUsedStorageKey, snackbar,
                showSuccessToast: showSuccessToast,
                showErrorToast: false);
        }
        finally
        {
            snackbar.Dismiss(loadingToast);
        }
    }

    private async Task PromptWhenPrintersUnavailableAsync(
        string lastUsedStorageKey,
        Action<PrinterLoadResult> applyResult,
        Func<Task>? notifyStateChanged)
    {
        var isLocalTarget = await IsLocalPrintAgentTargetAsync();
        var agentDisplay = isLocalTarget
            ? $"localhost:{PrintAgentPort}"
            : $"{await GetPrintAgentHostAsync()}:{PrintAgentPort}";

        if (isLocalTarget)
        {
            var open = await dialogService.ShowMessageBoxAsync(
                "未获取到打印机",
                $"无法连接打印代理（{agentDisplay}）。\n\n" +
                "请确认本机已安装并运行打印代理程序；或在右上角菜单中检查「打印代理 IP」是否填写正确（默认留空即本机）。\n\n" +
                "是否要打开打印代理程序？",
                yesText: "打开打印代理程序",
                cancelText: "稍后");

            if (open == true)
                await TryOpenLocalAgentAndReloadPrintersAsync(lastUsedStorageKey, applyResult, notifyStateChanged);
            return;
        }

        await dialogService.ShowMessageBoxAsync(
            "未获取到打印机",
            $"无法连接打印代理（{agentDisplay}）。\n\n" +
            "请确认目标电脑上的打印代理程序已启动，并在右上角菜单中检查「打印代理 IP」是否正确。",
            cancelText: "知道了");
    }

    private async Task TryOpenLocalAgentAndReloadPrintersAsync(
        string lastUsedStorageKey,
        Action<PrinterLoadResult> applyResult,
        Func<Task>? notifyStateChanged)
    {
        Snackbar? openingToast = null;
        try
        {
            openingToast = snackbar.ShowRetained("正在尝试打开打印代理…", Severity.Info);
            await Task.Delay(100);

            var ready = await js.InvokeAsync<bool>("localAgentLauncher.openAndWait", LocalAgentOpenWaitMs);

            snackbar.Dismiss(openingToast);
            openingToast = null;

            if (ready)
            {
                var result = await LoadPrintersWithFeedbackAsync(lastUsedStorageKey);
                if (result.Success)
                {
                    applyResult(result);
                    if (notifyStateChanged is not null)
                        await notifyStateChanged();
                    return;
                }
            }

            var download = await dialogService.ShowMessageBoxAsync(
                "仍无法连接打印代理",
                "未能连接到打印代理程序。可能本机尚未安装，或程序未能正常启动。\n\n" +
                "请检查是否已安装并运行打印代理程序；也可通过菜单下载安装包。\n\n" +
                "是否要下载打印代理程序？",
                yesText: "下载打印代理程序",
                cancelText: "取消");

            if (download == true)
                navigation.NavigateTo(LocalAgentInstallerInfo.DownloadPath, forceLoad: true);
            else
                snackbar.Show("请确认本机已安装并启动打印代理程序，或通过菜单下载安装。", Severity.Warning);
        }
        catch (Exception ex)
        {
            snackbar.Show($"打开打印代理程序失败：{ex.Message}", Severity.Error);
        }
        finally
        {
            snackbar.Dismiss(openingToast);
        }
    }

    private async Task<bool> IsLocalPrintAgentTargetAsync()
    {
        var url = await storage.GetPrintAgentBaseUrlAsync();
        return string.IsNullOrWhiteSpace(ParseHostFromUrl(url));
    }

    private async Task<string> GetPrintAgentHostAsync()
    {
        var url = await storage.GetPrintAgentBaseUrlAsync();
        var host = ParseHostFromUrl(url);
        return string.IsNullOrWhiteSpace(host) ? "localhost" : host;
    }

    private static string ParseHostFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";
        try
        {
            var u = url.Trim();
            var uri = new Uri(u.Contains("://") ? u : "http://" + u);
            var host = uri.Host ?? "";
            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
                return "";
            return host;
        }
        catch
        {
            return "";
        }
    }
}
