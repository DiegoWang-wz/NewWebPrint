using System.Drawing;
using System.Drawing.Imaging;
using ZXing;
using ZXing.QrCode;
using ZXing.QrCode.Internal;
using ZXing.Windows.Compatibility;

namespace NewWebPrint.Utilities;

public static class QrCodeHelper
{
    public static string GenerateQrBase64(string content, int width = 300, int height = 300) =>
        TryGenerateBase64(content, "M", width, height)
        ?? throw new ArgumentException("二维码内容不能为空或生成失败", nameof(content));

    public static string? TryGenerateBase64(string? content, string? errorCorrection = "M", int width = 300, int height = 300)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var options = new QrCodeEncodingOptions
        {
            Height = height,
            Width = width,
            Margin = 0,
            ErrorCorrection = ResolveErrorCorrection(errorCorrection)
        };

        var writer = new BarcodeWriter
        {
            Format = BarcodeFormat.QR_CODE,
            Options = options
        };

        try
        {
            using Bitmap bitmap = writer.Write(content.Trim());
            using Bitmap trimmed = BitmapTrimHelper.TrimWhiteMargins(bitmap);
            using MemoryStream ms = new();
            trimmed.Save(ms, ImageFormat.Png);
            return $"data:image/png;base64,{Convert.ToBase64String(ms.ToArray())}";
        }
        catch
        {
            return null;
        }
    }

    private static ErrorCorrectionLevel ResolveErrorCorrection(string? level) =>
        level?.Trim().ToUpperInvariant() switch
        {
            "L" => ErrorCorrectionLevel.L,
            "Q" => ErrorCorrectionLevel.Q,
            "H" => ErrorCorrectionLevel.H,
            _ => ErrorCorrectionLevel.M
        };
}
