namespace NewWebPrint.DataDtos;

public sealed class PrintersResp
{
    public List<string> installed { get; set; } = [];
    public string? @default { get; set; }
}

public class PrintLayoutModel
{
    public string? PrinterName { get; set; }
    public float PaperWidthMm { get; set; }
    public float PaperHeightMm { get; set; }
    public int Copies { get; set; } = 1;
    public int StartValue { get; set; } = 1;
    public int PrintCount { get; set; } = 1;
    public float BorderThicknessMm { get; set; } = 0.2f;
    public List<PrintBlock> Blocks { get; set; } = new();
    public float? GapMm { get; set; }
    public bool IsRotate { get; set; }
    public float? OffsetXMm { get; set; }
    public float? OffsetYMm { get; set; }
}

public class PrintBlock
{
    public string Name { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public bool ShowBorder { get; set; }
    public float? BorderTopMm { get; set; }
    public float? BorderRightMm { get; set; }
    public float? BorderBottomMm { get; set; }
    public float? BorderLeftMm { get; set; }
    public string? BorderStyle { get; set; }
    public float? DashLengthMm { get; set; }
    public float? DashGapMm { get; set; }
    public TextElement? Text { get; set; }
    public BarcodeElement? Barcode { get; set; }
    public ImageElement? Image { get; set; }
    public QrElement? Qr { get; set; }
}

public class TextElement
{
    public string Content { get; set; } = "";
    public string FontName { get; set; } = "Arial";
    public float FontSize { get; set; } = 6f;
    public bool Bold { get; set; }
    public string Align { get; set; } = "left";
    public string VerticalAlign { get; set; } = "middle";
    public bool AutoFit { get; set; }
}

public class BarcodeElement
{
    public string Value { get; set; } = string.Empty;
    public float SafeMarginMm { get; set; } = 1f;
    public string? Format { get; set; }
    public float OffsetXMm { get; set; }
    public bool ShowTextBelow { get; set; }
    public float? DigitFontSizePt { get; set; }
    public bool DigitBold { get; set; }
    public float DigitOffsetYMm { get; set; }
    public string Charset { get; set; } = "UTF-8";
    public string ErrorCorrection { get; set; } = "M";
}

public class ImageElement
{
    public string? Base64 { get; set; }

    /// <summary>浏览器 IndexedDB 图片缓存键（草稿恢复用，打印前由 JS 回填 Base64）。</summary>
    public string? StorageKey { get; set; }

    public string? FilePath { get; set; }
    public string Fit { get; set; } = "contain";
    public string Align { get; set; } = "center";
    public string VerticalAlign { get; set; } = "middle";
    public float Opacity { get; set; } = 1f;
}

public class QrElement
{
    public string Value { get; set; } = string.Empty;
    public string ErrorCorrection { get; set; } = "M";
    public string Charset { get; set; } = "UTF-8";
    public float SafeMarginMm { get; set; } = 1f;
    public bool ShowTextBelow { get; set; }
    public float? DigitFontSizePt { get; set; }
    public bool DigitBold { get; set; }
    public float DigitOffsetYMm { get; set; }
}
