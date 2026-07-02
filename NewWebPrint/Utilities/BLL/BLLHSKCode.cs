using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace NewWebPrint.Utilities;

/// <summary>
/// HSK 标签打印与 HskCode / MYCode / HSK MES Oracle 交互（对齐 WinForms LabelPrintHSK）。
/// </summary>
public class BLLHSKCode : BLLBase
{
    Storage? _myCodeStorage;
    string? _hskMesOracleConn;

    public BLLHSKCode(IConfiguration? config = null)
    {
        var raw = config?.GetConnectionString("hskCodeSQL");
        if (!string.IsNullOrWhiteSpace(raw))
        {
            var builder = new SqlConnectionStringBuilder(raw) { TrustServerCertificate = true };
            hskCodeStorage = new Storage(builder.ConnectionString);
        }

        var myRaw = config?.GetConnectionString("MYCode");
        if (!string.IsNullOrWhiteSpace(myRaw))
        {
            var myBuilder = new SqlConnectionStringBuilder(myRaw) { TrustServerCertificate = true };
            _myCodeStorage = new Storage(myBuilder.ConnectionString);
        }

        _hskMesOracleConn = config?.GetConnectionString("hskMesOracle");
        if (string.IsNullOrWhiteSpace(_hskMesOracleConn))
            _hskMesOracleConn = config?.GetConnectionString("OracleConn");
    }

    public static bool IsFactory4000(string? factory) =>
        factory?.Contains("4000", StringComparison.Ordinal) ?? false;

    public bool ValidateReprintUser(string userid, string password)
    {
        if (string.IsNullOrWhiteSpace(userid) || string.IsNullOrWhiteSpace(password))
            return false;

        var safeU = EscapeSqlLiteral(userid.Trim());
        var safeP = EscapeSqlLiteral(password);
        var sql =
            $"SELECT userid FROM dbo.main_RePrint_User WHERE userid='{safeU}' AND password='{safeP}'";
        return hskCodeStorage.ExecuteSQLJudgeRePrint(sql);
    }

    public void ClearMainTable() => hskCodeStorage.ExecuteSQL("TRUNCATE TABLE main");

    public DataTable? GetPrintNumHistory(string productionNo)
    {
        var safePo = EscapeSqlLiteral(productionNo);
        var where = $"HskCode_print_history_ProductionNo='{safePo}'";
        var ds = hskCodeStorage.GetDataSet(
            "hskcode_print_history_count",
            where,
            "HskCode_print_history_Printed",
            "HskCode_print_history_addTime",
            "Desc");

        if (ds != null && ds.Tables.Count > 0)
            return ds.Tables[0];

        return null;
    }

    public int GetPrintHistoryEndNum(string productionNo)
    {
        var dtb = GetPrintNumHistory(productionNo);
        if (dtb == null || dtb.Rows.Count <= 0)
            return 1;

        var str = dtb.Rows[0]["HskCode_print_history_Printed"]?.ToString();
        if (!int.TryParse(str, out var count))
            return 1;

        return count + 1;
    }

    public int AddPrintHistoryEnd(string productionNo, int printedEnd)
    {
        string[] column = ["HskCode_print_history_ProductionNo", "HskCode_print_history_Printed"];
        string[] value = [productionNo, printedEnd.ToString()];
        return hskCodeStorage.AddRecord("hskcode_print_history_count", column, value);
    }

    public int GetHistoryQty(string salesOrderNo, string scLine, string yearMmdd, string productionOrderLast2)
    {
        var safe = new[]
        {
            EscapeSqlLiteral(salesOrderNo),
            EscapeSqlLiteral(scLine),
            EscapeSqlLiteral(yearMmdd),
            EscapeSqlLiteral(productionOrderLast2)
        };
        var sql =
            $"SELECT COUNT(*) AS history_qty FROM dbo.main_history WHERE sales_order_no = '{safe[0]}' AND SCLine = '{safe[1]}' AND LEFT(Codevalue, 5) = '{safe[2]}' AND RIGHT(Codevalue, 2) = '{safe[3]}'";
        var ds = hskCodeStorage.ExecuteSQLDatable(sql);
        if (ds?.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
        {
            var raw = ds.Tables[0].Rows[0]["history_qty"]?.ToString();
            if (int.TryParse(raw, out var qty))
                return qty;
        }

        return 0;
    }

    public void SaveBarcodeRecord(HskBarcodeSaveContext ctx, bool factoryIs4000)
    {
        var scLine = ctx.IsManualReprint || ctx.IsSequenceReprint ? "补打" : ctx.ScLineText;
        var scLineCode = ctx.IsManualReprint || ctx.IsSequenceReprint ? "补打" : ctx.ScLineCode;

        string[] column;
        string[] value;
        if (ctx.IsManualReprint)
        {
            column =
            [
                "Codevalue", "Production_order_no", "sales_order_no", "pruduct_code", "product_name",
                "product_model", "Production_order_sum", "Production_order_6no", "SCLine", "SCLineCode"
            ];
            value = [ctx.CodeValue, "", "", "", "", "", "", "", "补打", "补打"];
        }
        else
        {
            column =
            [
                "Codevalue", "Production_order_no", "sales_order_no", "pruduct_code", "product_name",
                "product_model", "Production_order_sum", "Production_order_6no", "SCLine", "SCLineCode", "syn_flag"
            ];
            value =
            [
                ctx.CodeValue,
                ctx.ProductionOrderNo,
                ctx.SalesOrderNo,
                ctx.ProductCode,
                ctx.ProductName,
                ctx.ProductModel,
                ctx.ProductionOrderSum,
                ctx.ProductionOrderNo,
                scLine,
                scLineCode,
                "0"
            ];
        }

        if (ctx.IsManualReprint || ctx.IsSequenceReprint)
        {
            string[] ledgerColumn =
            [
                "Codevalue", "Production_order_no", "sales_order_no", "pruduct_code", "product_name",
                "product_model", "Production_order_sum", "Production_order_6no", "SCLine", "SCLineCode",
                "userid", "reason", "responsible_person"
            ];
            string[] ledgerValue =
            [
                ctx.CodeValue,
                ctx.IsManualReprint ? "" : ctx.ProductionOrderNo,
                ctx.IsManualReprint ? "" : ctx.SalesOrderNo,
                ctx.IsManualReprint ? "" : ctx.ProductCode,
                ctx.IsManualReprint ? "" : ctx.ProductName,
                ctx.IsManualReprint ? "" : ctx.ProductModel,
                ctx.IsManualReprint ? "" : ctx.ProductionOrderSum,
                ctx.IsManualReprint ? "" : ctx.ProductionOrderNo,
                "补打",
                "补打",
                ctx.ReprintUserId ?? "",
                ctx.ReprintReason ?? "",
                ctx.ResponsiblePerson ?? ""
            ];
            hskCodeStorage.AddRecord("main_RePrint_Ledger", ledgerColumn, ledgerValue);
        }

        if (factoryIs4000)
        {
            _myCodeStorage?.AddRecord("main_history", column, value);
            return;
        }

        if (!ctx.IsManualReprint)
            hskCodeStorage.AddRecord("main", column, value);

        hskCodeStorage.AddRecord("main_history", column, value);
    }

    public List<string> QueryNgBarcodes(string productionOrderNo)
    {
        if (string.IsNullOrWhiteSpace(_hskMesOracleConn))
            throw new InvalidOperationException("未配置 ConnectionStrings:hskMesOracle 或 OracleConn");

        var safePo = EscapeSqlLiteral(productionOrderNo.Trim());
        var sql = BuildNgBarcodeQuerySql(safePo);

        var list = new List<string>();
        using var conn = new OracleConnection(_hskMesOracleConn);
        conn.Open();
        using var cmd = new OracleCommand(sql, conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var barcode = reader["BARCODE"]?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(barcode))
                list.Add(barcode);
        }

        return list;
    }

    static string BuildNgBarcodeQuerySql(string safeProductionOrderNo)
    {
        var sb = new StringBuilder();
        sb.Append(" select aa.s.BARCODE as BARCODE from (with CR1 as ");
        sb.Append("(select CODEVALUE from MAIN_HISTORY where PRODUCTION_ORDER_NO = '");
        sb.Append(safeProductionOrderNo);
        sb.Append("'),");
        sb.Append(@" CR2 as
 (select barcode, TIME, BASESTATUS, LOADSTATUS
    from (SELECT a.barcode,
                 A.TIME,
                 A.PRODUCT_STATUS BASESTATUS,
                 A.PRODUCT_STATUS LOADSTATUS,
                 row_number() over(PARTITION BY a.BARCODE order by a.TIME desc) rn
            FROM (SELECT H.*
                    FROM HSK_SBCJ H
                   where H.BARCODE IN (SELECT CODEVALUE FROM CR1)) a)
   where rn = 1),
CR3 as
 (select barcode, TIME, BASESTATUS, LOADSTATUS
    from (SELECT a.barcode,
                 A.TIME,
                 A.STATUS BASESTATUS,
                 A.STATUS LOADSTATUS,
                 row_number() over(PARTITION BY a.BARCODE order by a.TIME desc) rn
            FROM (SELECT H.*
                    FROM HSK_SBCJ_67 H
                   where H.BARCODE IN (SELECT CODEVALUE FROM CR1)) a)
   where rn = 1),
CR4 as
 (select barcode, TIME, BASESTATUS, LOADSTATUS
    from (SELECT a.barcode,
                 A.TIME,
                 A.STATUS BASESTATUS,
                 A.STATUS LOADSTATUS,
                 row_number() over(PARTITION BY a.BARCODE order by a.TIME desc) rn
            FROM (SELECT H.*
                    FROM HSK_SBCJ_74_2 H
                   where H.BARCODE IN (SELECT CODEVALUE FROM CR1)) a)
   where rn = 1),
CR5 as
 (select barcode, TIME, BASESTATUS, LOADSTATUS
    from (SELECT a.barcode,
                 A.TIME,
                 A.STATUS BASESTATUS,
                 A.STATUS LOADSTATUS,
                 row_number() over(PARTITION BY a.BARCODE order by a.TIME desc) rn
            FROM (SELECT H.*
                    FROM HSK_SBCJ_79 H
                   where H.BARCODE IN (SELECT CODEVALUE FROM CR1)) a)
   where rn = 1),
CR21 as
 (select barcode, TIME, BASESTATUS, LOADSTATUS
    from (SELECT a.barcode,
                 A.TIME,
                 A.STATUS BASESTATUS,
                 A.STATUS LOADSTATUS,
                 row_number() over(PARTITION BY a.BARCODE order by a.TIME desc) rn
            FROM (SELECT H.*
                    FROM HSK_SBCJ_048 H
                   where H.BARCODE IN (SELECT CODEVALUE FROM CR1)) a)
   where rn = 1),
CR6 AS
 (
  SELECT H.BARCODE,
          H.LASTDATETIME TIME,
          CASE
            WHEN XRISECURRENTRESULT = 'OK' AND XRISETIMERESULT = 'OK' AND
                 XRISESPEEDRESULT = 'OK' AND XRETRACTIONHEIGHTRESULT = 'OK' AND
                 XSTRETCHHEIGHTRESULT = 'OK' AND XDIFFERENTHEIGHTRESULT = 'OK' AND
                 XDEFORMATIONRESULT = 'OK' AND YDEFORMATIONRESULT = 'OK' THEN
             'OK'
            WHEN XRISECURRENTRESULT IS NULL AND XRISETIMERESULT IS NULL AND
                 XRISESPEEDRESULT IS NULL AND XRETRACTIONHEIGHTRESULT IS NULL AND
                 XSTRETCHHEIGHTRESULT IS NULL AND
                 XDIFFERENTHEIGHTRESULT IS NULL AND XDEFORMATIONRESULT IS NULL AND
                 YDEFORMATIONRESULT IS NULL THEN
             ''
            ELSE
             'NG'
          END BASESTATUS
    FROM TB_PRODUCT_TEST_DATA H
   where H.BARCODE IN (SELECT CODEVALUE FROM CR1)),
CR7 AS
 (
  SELECT H.BARCODE,
          H.LASTDATETIME TIME,
          CASE
            WHEN LOADRISECURRENTRESULT = 'OK' AND LOADRISETIMERESULT = 'OK' AND
                 LOADRISESPEEDRESULT = 'OK' THEN
             'OK'
            WHEN LOADRISECURRENTRESULT IS NULL AND LOADRISETIMERESULT IS NULL AND
                 LOADRISESPEEDRESULT IS NULL THEN
             ''
            ELSE
             'NG'
          END LOADSTATUS
    FROM TB_PRODUCT_TEST_DATA H
   where H.BARCODE IN (SELECT CODEVALUE FROM CR1)),
CR8 AS
 (
  select *
    from (SELECT a.barcode,
                  A.TIME,
                  CASE
                    WHEN A.RISINGRESULT = 'OK' THEN
                     'OK'
                    ELSE
                     'NG'
                  END NOISESTATUS,
                  row_number() over(PARTITION BY a.BARCODE order by a.TIME desc) rn
             FROM (SELECT H.*
                     FROM HSK_SBCJ_30 H
                    where H.BARCODE IN (SELECT CODEVALUE FROM CR1)) a)
   where rn = 1),
CR9 AS
 (
  SELECT H.BARCODE, H.LASTDATETIME TIME, NOICERESULT NOISESTATUS
    FROM TB_PRODUCT_TEST_DATA H
   where H.BARCODE IN (SELECT CODEVALUE FROM CR1)),
cr10 AS
 (
  SELECT *
    FROM (SELECT BARCODE,
                  BASESTATUS,
                  row_number() over(PARTITION BY BARCODE order by TIME desc) rn
             FROM (SELECT BARCODE, TIME, BASESTATUS
                     FROM CR2
                   UNION ALL
                   SELECT BARCODE, TIME, BASESTATUS
                     FROM CR3
                   UNION ALL
                   SELECT BARCODE, TIME, BASESTATUS
                     FROM CR4
                   UNION ALL
                   SELECT BARCODE, TIME, BASESTATUS
                     FROM CR5
                   UNION ALL
                   SELECT BARCODE, TIME, BASESTATUS
                     FROM CR21
                   UNION ALL
                   SELECT BARCODE, TIME, BASESTATUS
                     FROM CR6
                    WHERE BASESTATUS IS NOT NULL
                   ))
   WHERE rn = 1),
cr11 AS
 (
  SELECT *
    FROM (SELECT BARCODE,
                  LOADSTATUS,
                  row_number() over(PARTITION BY BARCODE order by TIME desc) rn
             FROM (SELECT BARCODE, TIME, LOADSTATUS
                     FROM CR2
                   UNION ALL
                   SELECT BARCODE, TIME, LOADSTATUS
                     FROM CR3
                   UNION ALL
                   SELECT BARCODE, TIME, LOADSTATUS
                     FROM CR4
                   UNION ALL
                   SELECT BARCODE, TIME, LOADSTATUS
                     FROM CR5
                   UNION ALL
                   SELECT BARCODE, TIME, LOADSTATUS
                     FROM CR21
                   UNION ALL
                   SELECT BARCODE, TIME, LOADSTATUS
                     FROM CR7
                    WHERE LOADSTATUS IS NOT NULL
                   ))
   WHERE rn = 1),
CR12 AS
 (
  SELECT *
    FROM (SELECT BARCODE,
                  NOISESTATUS,
                  row_number() over(PARTITION BY BARCODE order by TIME desc) rn
             FROM (SELECT BARCODE, TIME, NOISESTATUS
                     FROM CR8
                   UNION ALL
                   SELECT BARCODE, TIME, NOISESTATUS
                     FROM CR9
                    WHERE NOISESTATUS IS NOT NULL
                   ))
   WHERE rn = 1)
SELECT JYF_BARCODE(BARCODE) as s
  FROM (SELECT BARCODE
          FROM CR10
         WHERE BASESTATUS = 'NG'
        UNION
        SELECT BARCODE
          FROM CR11
         WHERE LOADSTATUS = 'NG'
        UNION
        SELECT BARCODE
          FROM CR12
         WHERE NOISESTATUS = 'NG')) aa");
        return sb.ToString();
    }

    static string EscapeSqlLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}

public sealed class HskBarcodeSaveContext
{
    public required string CodeValue { get; init; }
    public string ProductionOrderNo { get; init; } = "";
    public string SalesOrderNo { get; init; } = "";
    public string ProductCode { get; init; } = "";
    public string ProductName { get; init; } = "";
    public string ProductModel { get; init; } = "";
    public string ProductionOrderSum { get; init; } = "";
    public string ScLineText { get; init; } = "";
    public string ScLineCode { get; init; } = "";
    public bool IsManualReprint { get; init; }
    public bool IsSequenceReprint { get; init; }
    public string? ReprintUserId { get; init; }
    public string? ReprintReason { get; init; }
    public string? ResponsiblePerson { get; init; }
}
