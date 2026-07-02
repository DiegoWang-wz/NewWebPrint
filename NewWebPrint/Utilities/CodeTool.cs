using System.Data;
using System.Reflection;

namespace NewWebPrint.Utilities;

public class CodeTool
{
    public static string ClearFrontZreo(string str)
    {
        if (str == "")
        {
            return "0";
        }

        char[] array = str.ToCharArray();
        string empty = string.Empty;
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i].ToString() != "0")
            {
                return str.Substring(i, str.Length - i);
            }
        }

        return "0";
    }

    public static string ClearNumberZreo(string str)
    {
        if (str == "")
        {
            return "0";
        }

        try
        {
            return Convert.ToDouble(str).ToString();
        }
        catch (Exception)
        {
            return "0";
        }
    }

    public static T ConvertDataRow2model<T>(DataRow row, T model)
    {
        Type typeFromHandle = typeof(T);
        PropertyInfo[] properties = typeFromHandle.GetProperties();
        PropertyInfo[] array = properties;
        foreach (PropertyInfo propertyInfo in array)
        {
            string name = propertyInfo.Name;
            string name2 = propertyInfo.PropertyType.Name;
            try
            {
                string text = row[name].ToString();
                if (text != null)
                {
                    typeFromHandle.GetProperty(name).SetValue(model, text, null);
                }
            }
            catch (Exception)
            {
            }
        }

        return model;
    }

    public string ConvertUSDate(string date)
    {
        try
        {
            return Convert.ToDateTime(date).ToString("MM/dd yyyy");
        }
        catch
        {
            return "00/00 0000";
        }
    }
}