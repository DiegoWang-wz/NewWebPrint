using NewWebPrint.DataDtos;

namespace NewWebPrint.Services.Ld40Binding;

/// <summary>
/// 将 WinForms <c>printDocument2_PrintPage</c> 外箱标签（约 404×315 百分之一英寸）映射为 LocalAgent 打印布局。
/// </summary>
public static class Ld40PrintLayoutBuilder
{
    const float PaperWidthMm = 102.6f;
    const float PaperHeightMm = 80f;
    const float ContentLeftMm = 2.8f;
    const float ContentWidthMm = 96f;
    const float LineStepMm = 7.4f;

    public static PrintLayoutModel Build(string printerName, string outerBoxBarcode, string sideMarkText, string workerId, int fontSizePt = 13)
    {
        var blocks = new List<PrintBlock>
        {
            BorderBlock("borderTop", 0, 0, PaperWidthMm, 0.5f, bottom: true),
            BorderBlock("borderBottom", 0, PaperHeightMm - 0.5f, PaperWidthMm, 0.5f, top: true),
            BorderBlock("borderLeft", 0, 0, 0.5f, PaperHeightMm, right: true),
            BorderBlock("borderRight", PaperWidthMm - 0.5f, 0, 0.5f, PaperHeightMm, left: true),
            new PrintBlock
            {
                Name = "qr",
                X = 72f,
                Y = 4f,
                Width = 28f,
                Height = 22f,
                Qr = new QrElement { Value = outerBoxBarcode, ErrorCorrection = "M" }
            }
        };

        var lines = SplitDisplayLines(sideMarkText);
        var j = CalcLineOffset(lines.Count);
        var extraLines = 0;

        for (var i = 0; i < lines.Count; i++)
        {
            foreach (var segment in WrapLine(lines[i], 48))
            {
                var y = 4f + fontSizePt * 0.35f + (i + j + extraLines) * LineStepMm;
                blocks.Add(new PrintBlock
                {
                    Name = $"line_{i}_{extraLines}",
                    X = ContentLeftMm,
                    Y = y,
                    Width = ContentWidthMm,
                    Height = LineStepMm,
                    Text = new TextElement
                    {
                        Content = segment,
                        FontName = "SimSun",
                        FontSize = fontSizePt,
                        Bold = true,
                        Align = "center",
                        VerticalAlign = "middle"
                    }
                });
                if (segment != lines[i]) extraLines++;
            }
        }

        blocks.Add(new PrintBlock
        {
            Name = "worker",
            X = 62f,
            Y = PaperHeightMm - 8f,
            Width = 38f,
            Height = 7f,
            Text = new TextElement
            {
                Content = "工号:" + workerId,
                FontName = "SimSun",
                FontSize = fontSizePt,
                Bold = true,
                Align = "left",
                VerticalAlign = "middle"
            }
        });

        return new PrintLayoutModel
        {
            PrinterName = printerName,
            PaperWidthMm = PaperWidthMm,
            PaperHeightMm = PaperHeightMm,
            Copies = 1,
            Blocks = blocks
        };
    }

    static PrintBlock BorderBlock(string name, float x, float y, float w, float h, bool top = false, bool bottom = false, bool left = false, bool right = false)
        => new()
        {
            Name = name,
            X = x,
            Y = y,
            Width = w,
            Height = h,
            ShowBorder = true,
            BorderTopMm = top ? 0.4f : 0f,
            BorderBottomMm = bottom ? 0.4f : 0f,
            BorderLeftMm = left ? 0.4f : 0f,
            BorderRightMm = right ? 0.4f : 0f,
            BorderStyle = "Solid"
        };

    static List<string> SplitDisplayLines(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<string>();
        return text.Replace("\r\n", "\n").Split('\n').ToList();
    }

    static int CalcLineOffset(int lineCount) => lineCount switch
    {
        1 => 4,
        2 or 3 => 3,
        4 or 5 => 2,
        6 or 7 => 1,
        _ => 0
    };

    static IEnumerable<string> WrapLine(string line, int maxChars)
    {
        if (string.IsNullOrEmpty(line)) yield break;
        if (line.Length <= maxChars)
        {
            yield return line;
            yield break;
        }

        for (var i = 0; i < line.Length; i += maxChars)
            yield return line.Substring(i, Math.Min(maxChars, line.Length - i));
    }
}
