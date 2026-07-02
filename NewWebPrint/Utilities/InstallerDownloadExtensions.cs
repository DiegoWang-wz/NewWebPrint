using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace NewWebPrint.Utilities;

public static class InstallerDownloadExtensions
{
    private static readonly HashSet<string> AllowedFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        LocalAgentInstallerInfo.SetupFileName,
        LocalAgentInstallerInfo.VcRedistFileName,
        LocalAgentInstallerInfo.ApkFileName
    };

    /// <summary>
    /// 安装包专用下载（优先于 StaticFiles），HTTPS 下返回正确的 octet-stream 与 Content-Disposition。
    /// </summary>
    public static WebApplication UseInstallerDownloads(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (!HttpMethods.IsGet(context.Request.Method)
                && !HttpMethods.IsHead(context.Request.Method))
            {
                await next();
                return;
            }

            var path = context.Request.Path.Value ?? string.Empty;
            if (!path.StartsWith("/downloads/", StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            var fileName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(fileName) || !AllowedFiles.Contains(fileName))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var filePath = Path.Combine(app.Environment.WebRootPath, "downloads", fileName);
            if (!File.Exists(filePath))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(fileName, out var contentType))
                contentType = "application/octet-stream";

            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";
            context.Response.ContentType = contentType;

            await context.Response.SendFileAsync(filePath);
        });

        return app;
    }
}
