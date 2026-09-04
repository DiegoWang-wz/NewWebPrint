namespace NewWebPrint.DataDtos;

public sealed class PrintersResp
{
    public List<string> installed { get; set; } = [];
    public string? @default { get; set; }
}

/// <summary>整单打印布局（单位：毫米）。与 LocalAgent.PrintLayoutModel 字段对齐；新增属性均为可选或带默认值。</summary>
public class PrintLayoutModel
{
    public string? PrinterName { get; set; }
    public float PaperWidthMm { get; set; }
    public float PaperHeightMm { get; set; }
    public int Copies { get; set; } = 1;

    /// <summary>打印起始值（默认 1）</summary>
    public int StartValue { get; set; } = 1;

    /// <summary>打印数量（默认 1）</summary>
    public int PrintCount { get; set; } = 1;

    /// <summary>整张标签统一的边框线宽（mm）</summary>
    public float BorderThicknessMm { get; set; } = 0.4f;

    /// <summary>一张纸可放多个 Block</summary>
    public List<PrintBlock> Blocks { get; set; } = new();

    // 多列打印可选
    public float? GapMm { get; set; }
    public bool IsRotate { get; set; }

    /// <summary>整张布局在页面上的 X 轴偏移量（mm）。默认 null 表示不偏移。</summary>
    public float? OffsetXMm { get; set; }

    /// <summary>整张布局在页面上的 Y 轴偏移量（mm）。默认 null 表示不偏移。</summary>
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

    /// <summary>四边独立线宽（mm）。null=使用整单 BorderThicknessMm，0=不显示该边，&gt;0=该边线宽。</summary>
    public float? BorderTopMm { get; set; }
    public float? BorderRightMm { get; set; }
    public float? BorderBottomMm { get; set; }
    public float? BorderLeftMm { get; set; }

    /// <summary>边框线型：Solid=实线（默认），Dash=虚线，DashDot=点线点。</summary>
    public string? BorderStyle { get; set; }
    /// <summary>虚线/点线点时的段长（mm），未设时默认 2。</summary>
    public float? DashLengthMm { get; set; }
    /// <summary>虚线/点线点时的间隔（mm），未设时默认 1。</summary>
    public float? DashGapMm { get; set; }

    public TextElement? Text { get; set; }
    public BarcodeElement? Barcode { get; set; }
    public ImageElement? Image { get; set; }
    public QrElement? Qr { get; set; }

    /// <summary>直线线框（端点坐标相对 Block 左上角，单位 mm）。与 Circle 互斥。</summary>
    public LineElement? Line { get; set; }

    /// <summary>圆形线框（圆心/半径相对 Block 左上角，单位 mm）。与 Line 互斥。</summary>
    public CircleElement? Circle { get; set; }
}

/// <summary>直线线框端点（相对 Block 左上角，mm）。</summary>
public class LineElement
{
    public float X1Mm { get; set; }
    public float Y1Mm { get; set; }
    public float X2Mm { get; set; }
    public float Y2Mm { get; set; }
}

/// <summary>圆形线框（相对 Block 左上角，mm）。</summary>
public class CircleElement
{
    public float CenterXMm { get; set; }
    public float CenterYMm { get; set; }
    public float RadiusMm { get; set; }
}

public class TextElement
{
    public string Content { get; set; } = "";
    public string FontName { get; set; } = "Arial";
    public float FontSize { get; set; } = 6f;
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public string Align { get; set; } = "left";
    public string VerticalAlign { get; set; } = "middle";
    public bool AutoFit { get; set; }
}

public class BarcodeElement
{
    public string Value { get; set; } = string.Empty;

    public float SafeMarginMm { get; set; } = 1f;

    /// <summary>条码类型：CODE_128（默认）、EAN_13、CODE_39、QR_CODE、DATA_MATRIX、AZTEC、PDF_417、MAXICODE。</summary>
    public string? Format { get; set; }

    /// <summary>条码整体水平右移量（mm），默认 0。</summary>
    public float OffsetXMm { get; set; }

    /// <summary>普通条码（如 CODE_128）是否在下方显示文字（显示 Value）。EAN_13 始终显示数字。</summary>
    public bool ShowTextBelow { get; set; }

    /// <summary>条码下方文字的字号（磅）。不设则默认约 6pt。</summary>
    public float? DigitFontSizePt { get; set; } = 6f;

    /// <summary>条码下方文字是否加粗。</summary>
    public bool DigitBold { get; set; }

    /// <summary>条码下方文字字体。</summary>
    public string DigitFontName { get; set; } = "Arial";

    /// <summary>条码下方文字的 Y 轴偏移（mm）。正数=往下，负数=往上。默认 0。</summary>
    public float DigitOffsetYMm { get; set; }

    /// <summary>条码本体区域高度（mm，不含下方文字）。开启下方文字后保持不变。</summary>
    public float? CodeBodyHeightMm { get; set; }

    /// <summary>二维码字符集（仅 QR_CODE 有效，默认 UTF-8）。</summary>
    public string Charset { get; set; } = "UTF-8";

    /// <summary>二维码纠错等级 L/M/Q/H（仅 QR_CODE 有效，默认 M）。</summary>
    public string ErrorCorrection { get; set; } = "M";
}

public class ImageElement
{
    public string? Base64 { get; set; }

    /// <summary>浏览器 IndexedDB 图片缓存键（草稿恢复用，打印前由 JS 回填 Base64）。LocalAgent 忽略此字段。</summary>
    public string? StorageKey { get; set; }

    public string? FilePath { get; set; }
    public string Fit { get; set; } = "contain"; // contain / cover / stretch
    public string Align { get; set; } = "center";
    public string VerticalAlign { get; set; } = "middle";
    public float Opacity { get; set; } = 0.2f;
}

public class QrElement
{
    /// <summary>二维码内容</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>纠错等级：L/M/Q/H（默认 M）</summary>
    public string ErrorCorrection { get; set; } = "M";

    /// <summary>字符集（默认 UTF-8）</summary>
    public string Charset { get; set; } = "UTF-8";

    /// <summary>二维码在 Block 内的安全距离（单位：毫米，默认 1）</summary>
    public float SafeMarginMm { get; set; } = 1f;

    /// <summary>是否在二维码下方显示文字（显示 Value）。</summary>
    public bool ShowTextBelow { get; set; }

    /// <summary>下方文字字号（磅），不设则默认约 6pt。</summary>
    public float? DigitFontSizePt { get; set; } = 6f;

    /// <summary>下方文字是否加粗。</summary>
    public bool DigitBold { get; set; }

    /// <summary>下方文字字体。</summary>
    public string DigitFontName { get; set; } = "Arial";

    /// <summary>下方文字 Y 轴偏移（mm）。正数=往下，负数=往上。默认 0。</summary>
    public float DigitOffsetYMm { get; set; }

    /// <summary>二维码本体区域高度（mm，不含下方文字）。开启下方文字后保持不变。</summary>
    public float? CodeBodyHeightMm { get; set; }
}
