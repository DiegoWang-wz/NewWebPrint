using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Text.RegularExpressions;
using AutoMapper;
using Blazored.LocalStorage;
using Blazored.SessionStorage;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using NewWebPrint.Components;
using NewWebPrint.DbContexts;
using NewWebPrint.Services;
using NewWebPrint.Services.Hu;
using NewWebPrint.Services.Ld40Binding;
using NewWebPrint.Services.SapEwm;
using NewWebPrint.Utilities;
using Oracle.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options =>
    {
        // 条码/二维码预览等 JS 互操作可能较大；主路径已避免草稿传 Base64
        options.MaximumReceiveMessageSize = 10 * 1024 * 1024;
    });
builder.Services.AddMudServices();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddControllers(options => { options.Filters.Add(new IgnoreAntiforgeryTokenAttribute()); });

builder.Services.AddDbContext<JCloginDbContext>(m =>
    m.UseSqlServer(builder.Configuration.GetConnectionString("JClogin_connStr")));

builder.Services.AddDbContext<OracleDbContext>(opt =>
{
    var conn = builder.Configuration.GetConnectionString("OracleConn");
    opt.UseOracle(conn, b => b.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion21));
});

builder.Services.AddAutoMapper(cfg => cfg.AddMaps(typeof(AutoMapperSetting).Assembly));

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddBlazoredSessionStorage();
builder.Services.AddScoped<LocalStorageHelper>();
builder.Services.AddScoped<SessionStorageHelper>();
builder.Services.AddScoped<KeypadService>();
builder.Services.AddScoped<SnackbarHelper>();
builder.Services.AddScoped<LocalAgentPrinterBootstrap>();
builder.Services.AddScoped<ICordListService, CordListService>();
builder.Services.AddScoped<ILd40BindingService, Ld40BindingService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IBrowserDevConsoleService, BrowserDevConsoleService>();
builder.Services.AddHttpClient<SapEwmService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddScoped<ISapEwmService>(sp => sp.GetRequiredService<SapEwmService>());
builder.Services.AddHttpClient(nameof(HuLabelService), client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // 内网 HU API 直连；避免被本机代理（如 127.0.0.1:7897）劫持导致“连接被拒绝”
    UseProxy = false,
    Proxy = null
});
builder.Services.AddScoped<IHuLabelService, HuLabelService>();

var app = builder.Build();
var serialPortLocks = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

var fwd = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor
                       | ForwardedHeaders.XForwardedProto
                       | ForwardedHeaders.XForwardedHost
};
fwd.KnownNetworks.Clear();
fwd.KnownProxies.Clear();
app.UseForwardedHeaders(fwd);

static string? ExtractWeightValue(string? input)
{
    if (string.IsNullOrWhiteSpace(input)) return null;
    var normalized = Regex.Replace(input, @"([\d.](?:\s*[\d.])*)", m => m.Value.Replace(" ", ""));
    var match = Regex.Match(normalized, @"[\d.]+");
    return match.Success ? match.Value.Trim() : null;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseInstallerDownloads();
app.UseStaticFiles();
app.UseAntiforgery();

var supportedCultures = new[] { "zh-CN", "en-US" };
var locOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("zh-CN")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);
app.UseRequestLocalization(locOptions);

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapControllers();

app.MapGet("/api/printagent/printers", async (string? baseUrl, IHttpClientFactory factory) =>
{
    if (string.IsNullOrWhiteSpace(baseUrl))
        return Results.BadRequest("缺少 baseUrl");
    var u = baseUrl.Trim();
    if (!u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest("baseUrl 须为 http 或 https");
    u = u.TrimEnd('/');
    try
    {
        var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);
        var resp = await client.GetAsync(u + "/printers");
        var body = await resp.Content.ReadAsStringAsync();
        return Results.Content(body, "application/json", statusCode: (int)resp.StatusCode);
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = "转发请求失败", message = ex.Message }, statusCode: 502);
    }
});

app.MapPost("/api/printagent/print/layout", async (HttpContext ctx, string? baseUrl, IHttpClientFactory factory) =>
{
    if (string.IsNullOrWhiteSpace(baseUrl))
        return Results.BadRequest("缺少 baseUrl");
    var u = baseUrl.Trim();
    if (!u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest("baseUrl 须为 http 或 https");
    u = u.TrimEnd('/');
    try
    {
        using var ms = new MemoryStream();
        await ctx.Request.Body.CopyToAsync(ms);
        ms.Position = 0;
        var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        var content = new StreamContent(ms);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        var resp = await client.PostAsync(u + "/print/layout", content);
        var body = await resp.Content.ReadAsStringAsync();
        return Results.Content(body, "application/json", statusCode: (int)resp.StatusCode);
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = "转发打印请求失败", message = ex.Message }, statusCode: 502);
    }
});

app.MapGet("/api/serial/local/ports", () =>
{
    try
    {
        var ports = SerialPort.GetPortNames().OrderBy(p => p).ToArray();
        return Results.Json(new { ok = true, ports });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message, ports = Array.Empty<string>() }, statusCode: 500);
    }
});

app.MapPost("/api/serial/local/read-once", async (HttpContext ctx) =>
{
    SerialReadRequest? req;
    try
    {
        req = await ctx.Request.ReadFromJsonAsync<SerialReadRequest>();
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = $"请求体解析失败: {ex.Message}" }, statusCode: 400);
    }

    if (req is null || string.IsNullOrWhiteSpace(req.PortName))
        return Results.Json(new { ok = false, message = "缺少 PortName" }, statusCode: 400);

    SemaphoreSlim? locker = null;
    var lockKey = req.PortName.Trim();
    try
    {
        locker = serialPortLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
        await locker.WaitAsync();

        var parity = Enum.TryParse<Parity>((req.Parity ?? "None"), true, out var p) ? p : Parity.None;
        var stopBits = Enum.TryParse<StopBits>((req.StopBits ?? "One"), true, out var sb) ? sb : StopBits.One;
        var timeoutMs = req.TimeoutMs is > 0 and <= 15000 ? req.TimeoutMs.Value : 1500;

        using var serial = new SerialPort(req.PortName.Trim(), req.BaudRate ?? 19200, parity, req.DataBits ?? 8, stopBits)
        {
            ReadTimeout = timeoutMs,
            WriteTimeout = timeoutMs,
            DtrEnable = req.DtrEnable ?? true,
            RtsEnable = req.RtsEnable ?? true,
            NewLine = string.IsNullOrWhiteSpace(req.NewLine) ? "\r\n" : req.NewLine
        };

        serial.Open();
        serial.DiscardInBuffer();
        await Task.Delay(180);

        string raw;
        try
        {
            raw = serial.ReadLine();
        }
        catch (TimeoutException)
        {
            raw = serial.ReadExisting();
        }

        var weight = ExtractWeightValue(raw);
        return Results.Json(new
        {
            ok = true,
            port = req.PortName.Trim(),
            raw = raw ?? "",
            weight = weight ?? "",
            message = string.IsNullOrWhiteSpace(raw) ? "已打开串口，但在超时时间内没有读到数据" : "读取成功"
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: 500);
    }
    finally
    {
        if (locker is not null) locker.Release();
    }
});

app.Run();

internal sealed class SerialReadRequest
{
    public string? PortName { get; set; }
    public int? BaudRate { get; set; }
    public string? Parity { get; set; }
    public int? DataBits { get; set; }
    public string? StopBits { get; set; }
    public int? TimeoutMs { get; set; }
    public bool? DtrEnable { get; set; }
    public bool? RtsEnable { get; set; }
    public string? NewLine { get; set; }
}
