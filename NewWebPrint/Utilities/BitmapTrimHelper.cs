using System.Drawing;
using System.Drawing.Imaging;

namespace NewWebPrint.Utilities;

internal static class BitmapTrimHelper
{
    /// <summary>裁掉 PNG 四周多余白边，避免预览/打印出现非预期的 quiet zone。</summary>
    public static Bitmap TrimWhiteMargins(Bitmap source)
    {
        var minX = source.Width;
        var minY = source.Height;
        var maxX = -1;
        var maxY = -1;

        var rect = new Rectangle(0, 0, source.Width, source.Height);
        var data = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var stride = data.Stride;
            var bytes = stride * source.Height;
            var pixels = new byte[bytes];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, bytes);

            for (var y = 0; y < source.Height; y++)
            {
                var row = y * stride;
                for (var x = 0; x < source.Width; x++)
                {
                    var i = row + x * 4;
                    var b = pixels[i];
                    var g = pixels[i + 1];
                    var r = pixels[i + 2];
                    var a = pixels[i + 3];
                    if (a < 8 || (r > 245 && g > 245 && b > 245))
                        continue;

                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }
        }
        finally
        {
            source.UnlockBits(data);
        }

        if (maxX < minX || maxY < minY)
            return (Bitmap)source.Clone();

        var crop = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
        return source.Clone(crop, source.PixelFormat);
    }
}
