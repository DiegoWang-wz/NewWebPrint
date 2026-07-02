using System.Data;
using NewWebPrint.Models;

namespace NewWebPrint.Utilities;

public class BLLJCZPCode : BLLBase
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

    public int AddCodeEnd(string workno, string end)
    {
        string[] column = new string[2] { "HskCode_print_history_ProductionNo", "HskCode_print_history_Printed" };
        string[] value = new string[2] { workno, end };
        return controlCodeStorage.AddRecord("jczp_print_history_count", column, value);
    }

    public DataTable GetPrintNumHistory(string where)
    {
        DataSet dataSet = controlCodeStorage.GetDataSet("jczp_print_history_count", where, "HskCode_print_history_Printed", "HskCode_print_history_addTime", "Desc");
        if (dataSet != null && dataSet.Tables.Count > 0)
        {
            return dataSet.Tables[0];
        }

        return null;
    }
}