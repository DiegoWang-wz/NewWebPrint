using System.Drawing;
using System.Drawing.Imaging;
using ZXing;
using ZXing.Common;
using ZXing.Windows.Compatibility;

namespace NewWebPrint.Utilities;

public static class BarcodeHelper
{
    public static int EstimatePreviewHeightPx(float blockHeightMm, float safeMarginMm, bool showTextBelow, bool isEan13, float digitOffsetYMm)
    {
        var safeH = Math.Max(1f, blockHeightMm - 2 * safeMarginMm);
        var textH = showTextBelow || isEan13
            ? Math.Min(3.5f, Math.Max(2f, safeH * 0.2f))
            : 0f;
        var textBand = textH + Math.Max(0f, digitOffsetYMm);
        var barcodeHMm = showTextBelow || isEan13
            ? Math.Max(6f, safeH - textBand)
            : safeH;
        return Math.Max(32, (int)Math.Round(barcodeHMm / 25.4f * 150f));
    }

    public static string? TryGenerateBase64(string value, string? format, int height = 120)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = value.Trim();
        var barcodeFormat = ResolveFormat(format);
        var writer = new BarcodeWriter
        {
            Format = barcodeFormat,
            Options = new EncodingOptions
            {
                Width = EstimateBarcodeWidth(text, barcodeFormat),
                Height = height,
                Margin = 0,
                PureBarcode = true
            }
        };

        try
        {
            using Bitmap bitmap = writer.Write(text);
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

    private static int EstimateBarcodeWidth(string value, BarcodeFormat format)
    {
        if (format == BarcodeFormat.EAN_13)
            return 220;

        // CODE_128：按内容估算模块数，避免固定宽图两侧留白
        var modules = 35 + value.Length * 11;
        return Math.Clamp(modules * 2, 80, 2400);
    }

    private static BarcodeFormat ResolveFormat(string? format) =>
        format?.Trim().ToUpperInvariant() switch
        {
            "EAN_13" or "EAN13" => BarcodeFormat.EAN_13,
            _ => BarcodeFormat.CODE_128
        };
}
