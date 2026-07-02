using MudBlazor;

namespace NewWebPrint.Utilities;

public class SnackbarHelper(ISnackbar snackbar)
{
    public void Show(string message, Severity severity, int timeout = 2000)
    {
        snackbar.Configuration.PositionClass = Defaults.Classes.Position.TopCenter;
        snackbar.Add(message, severity, config => ApplyConfig(config, timeout));
    }

    /// <summary>显示可手动关闭的提示（用于加载中等需在完成时主动 Dismiss 的场景）。</summary>
    public Snackbar ShowRetained(string message, Severity severity, int timeout = 60000)
    {
        snackbar.Configuration.PositionClass = Defaults.Classes.Position.TopCenter;
        return snackbar.Add(message, severity, config => ApplyConfig(config, timeout));
    }

    public void Dismiss(Snackbar? item)
    {
        if (item != null)
            snackbar.Remove(item);
    }

    private static void ApplyConfig(SnackbarOptions config, int timeout)
    {
        config.ShowTransitionDuration = 300;
        config.HideTransitionDuration = 300;
        config.VisibleStateDuration = timeout;
        config.RequireInteraction = false;
        config.ShowCloseIcon = true;
        config.SnackbarVariant = Variant.Filled;
    }
}
