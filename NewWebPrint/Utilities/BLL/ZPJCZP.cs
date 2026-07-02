using System.Data;
using NewWebPrint.Models;

namespace NewWebPrint.Utilities;

public class ZPJCZP : BLLBase
{
    public void ClearTable()
    {
        controlCodeStorage.ExecuteSQL("TRUNCATE TABLE   tb_jc_ZPbox");
    }

    public int AddCode(ModelMaterialMark model)
    {
        string[] column = new string[11]
        {
            "product_name", "pruduct_code", "Production_order_sum", "OperatePerson", "PrintTime", "Codevalue", "Production_order_no", "sales_order_no", "Createtime", "companyCode",
            "productCode"
        };
        string[] value = new string[11]
        {
            model.JCM_ProductName, model.JCM_ProductCode, model.JCM_ProductNum, model.JCM_OperationPerson, model.JCM_PrintTime, model.JCM_CodeValue, model.JCM_WorkNo, model.JCM_SalesNo, model.JCM_PrintTime, model.companyCode,
            model.productCode
        };
        int num = controlCodeStorage.AddRecord("tb_jc_ZPbox", column, value);
        return controlCodeStorage.AddRecord("tb_jc_ZPbox_history", column, value);
    }

    public int updateEnd(string start, string date)
    {
        string[] column = new string[1] { "HskCode_print_end"};
        string[] value = new string[1] { start };
        return controlCodeStorage.UpdateRecord("jczp_print_history_new", column, value, $"HskCode_print_date = '{date}'");
    }

    public int addEnd(string start, string date)
    {
        string[] column = new string[2] { "HskCode_print_end", "HskCode_print_date" };
        string[] value = new string[2] { start, date};

        return controlCodeStorage.AddRecord("jczp_print_history_new", column, value);
    }

    public int getEnd(string date)
    {
        DataSet dataSet = controlCodeStorage.GetDataSet("jczp_print_history_new", $"HskCode_print_date = '{date}'");
        if (dataSet != null && dataSet.Tables.Count > 0 && dataSet.Tables[0].Rows.Count > 0)
        {
            object value = dataSet.Tables[0].Rows[0]["HskCode_print_end"];
            if (value != null && value != DBNull.Value)
            {
                if (int.TryParse(value.ToString(), out int result))
                {
                    return result;
                }
            }
        }
        else
        {
            addEnd("0", date);
        }
        return 0;
    }
}