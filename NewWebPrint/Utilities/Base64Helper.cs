using Microsoft.AspNetCore.Components.Forms;

namespace NewWebPrint.Utilities;

public class Base64Helper
{
    // <summary>
    /// 将上传的文件 (IBrowserFile) 转化为 Base64 Data URL
    /// </summary>
    /// <param name="file">上传的文件对象</param>
    /// <param name="maxAllowedSize">允许的最大文件大小，默认 5MB</param>
    /// <returns>可以直接用于 <img> 标签 src 属性的字符串</returns>
    public static async Task<string> ToDataUrlAsync(IBrowserFile file, long maxAllowedSize = 1024 * 1024 * 20)
    {
        // 验证是否是图片格式
        var format = file.ContentType;

        // 使用 MemoryStream 读取文件流
        using var stream = file.OpenReadStream(maxAllowedSize);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);

        byte[] bytes = ms.ToArray();
        string base64 = Convert.ToBase64String(bytes);

        return $"data:{format};base64,{base64}";
    }

    /// <summary>
    /// 将字节数组转化为 Base64 Data URL
    /// </summary>
    public static string ToDataUrl(byte[] bytes, string contentType = "image/png")
    {
        string base64 = Convert.ToBase64String(bytes);
        return $"data:{contentType};base64,{base64}";
    }

    /// <summary>
    /// 将 Stream 转化为 Base64 Data URL
    /// </summary>
    public static async Task<string> ToDataUrlAsync(Stream stream, string contentType = "image/png")
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ToDataUrl(ms.ToArray(), contentType);
    }
    
    public static string GetBase64FromPhysicalPath(string physicalPath)
    {
        try
        {
            if (!File.Exists(physicalPath))
            {
                return string.Empty;
            }

            byte[] imageArray = File.ReadAllBytes(physicalPath);
            string base64String = Convert.ToBase64String(imageArray);
                
            // 识别图片扩展名
            string extension = Path.GetExtension(physicalPath).Replace(".", "").ToLower();
            if (string.IsNullOrEmpty(extension)) extension = "png"; 

            return $"data:image/{extension};base64,{base64String}";
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}