namespace NewWebPrint.Services;

public class KeypadService
{
    private Func<string?>? _getter;
    private Action<string?>? _setter;

    public bool HasTarget => _setter != null;

    public void Register(Func<string?> getter, Action<string?> setter)
    {
        _getter = getter;
        _setter = setter;
    }

    public void Unregister()
    {
        _getter = null;
        _setter = null;
    }

    public string? GetValue() => _getter?.Invoke();

    public void SetValue(string? v) => _setter?.Invoke(v);

    public void Backspace()
    {
        var cur = GetValue() ?? "";
        if (cur.Length > 0) SetValue(cur[..^1]);
    }

    public void Clear() => SetValue(string.Empty);

    public void ToggleSign()
    {
        var cur = GetValue() ?? "";
        if (string.IsNullOrEmpty(cur)) SetValue("-");
        else SetValue(cur.StartsWith("-") ? cur.TrimStart('-') : "-" + cur);
    }
}
