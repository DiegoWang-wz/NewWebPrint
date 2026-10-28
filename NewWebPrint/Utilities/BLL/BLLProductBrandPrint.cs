using System.Data;
using System.Text;
using NewWebPrint.DataDtos;

namespace NewWebPrint.Utilities;

/// <summary>
/// 铭牌/品牌打印码表 tb_ProductBrandPrint_code（JCControlCode）
/// </summary>
public class BLLProductBrandPrint : BLLBase
{
    public sealed class QueryResult
    {
        public List<ProductBrandPrintCodeRow> Items { get; init; } = [];
        public int TotalCount { get; init; }
    }

    /// <summary>
    /// 分页查询，按 ID 倒序。
    /// </summary>
    public QueryResult QueryPaged(
        string? productionNo,
        string? serialNo,
        string? sapNo,
        string? model,
        string? partNo,
        DateTime? createFrom,
        DateTime? createTo,
        int page,
        int pageSize)
    {
        if (page < 0) page = 0;
        if (pageSize <= 0) pageSize = 20;
        if (pageSize > 200) pageSize = 200;

        string where = BuildWhere(productionNo, serialNo, sapNo, model, partNo, createFrom, createTo);
        int total = controlCodeStorage.GetRecordCount("tb_ProductBrandPrint_code", where);
        if (total < 0) total = 0;

        var list = new List<ProductBrandPrintCodeRow>();
        if (total == 0)
            return new QueryResult { Items = list, TotalCount = 0 };

        int offset = page * pageSize;
        string sql = $@"
SELECT
    ID, Model, Maxload, IPX6, PartNo, SAPNo, ProdDate, SerialNo, RevNo,
    Input, SerialNumber, ProductionNo, SalesOrderNo, Createtime, AllowReprint, ReprintCount
FROM tb_ProductBrandPrint_code
WHERE {where}
ORDER BY ID DESC
OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY";

        DataSet? ds = controlCodeStorage.ExecuteSQLDatable(sql);
        list = MapRows(ds);
        return new QueryResult { Items = list, TotalCount = total };
    }

    /// <summary>
    /// 按当前条件导出全部匹配行（无分页）。
    /// </summary>
    public List<ProductBrandPrintCodeRow> QueryAll(
        string? productionNo,
        string? serialNo,
        string? sapNo,
        string? model,
        string? partNo,
        DateTime? createFrom,
        DateTime? createTo)
    {
        string where = BuildWhere(productionNo, serialNo, sapNo, model, partNo, createFrom, createTo);
        string sql = $@"
SELECT
    ID, Model, Maxload, IPX6, PartNo, SAPNo, ProdDate, SerialNo, RevNo,
    Input, SerialNumber, ProductionNo, SalesOrderNo, Createtime, AllowReprint, ReprintCount
FROM tb_ProductBrandPrint_code
WHERE {where}
ORDER BY ID DESC";

        DataSet? ds = controlCodeStorage.ExecuteSQLDatable(sql);
        return MapRows(ds);
    }

    const string AllowReprintAccount = "100208";
    const string AllowReprintPassword = "Jc100208";

    /// <summary>允许补打前的帐号密码校验。</summary>
    public static bool VerifyAllowReprintAuth(string? account, string? password)
    {
        return string.Equals((account ?? "").Trim(), AllowReprintAccount, StringComparison.Ordinal)
               && string.Equals(password ?? "", AllowReprintPassword, StringComparison.Ordinal);
    }

    /// <summary>将指定记录的 AllowReprint 置为 1。</summary>
    public int AllowReprintByIds(IReadOnlyList<long> ids)
    {
        if (ids is null || ids.Count == 0)
            return 0;

        int updated = 0;
        foreach (var chunk in ids.Distinct().Chunk(400))
        {
            string inList = string.Join(",", chunk);
            string sql = $"UPDATE tb_ProductBrandPrint_code SET AllowReprint = 1 WHERE ID IN ({inList})";
            updated += controlCodeStorage.ExecuteSQL(sql);
        }

        return updated;
    }

    static string BuildWhere(
        string? productionNo,
        string? serialNo,
        string? sapNo,
        string? model,
        string? partNo,
        DateTime? createFrom,
        DateTime? createTo)
    {
        var where = new StringBuilder("1=1");

        if (!string.IsNullOrWhiteSpace(productionNo))
            where.Append(" AND ProductionNo = '").Append(Escape(productionNo.Trim())).Append('\'');

        if (!string.IsNullOrWhiteSpace(serialNo))
            where.Append(" AND (SerialNo LIKE '%").Append(Escape(serialNo.Trim()))
                .Append("%' OR SerialNumber LIKE '%").Append(Escape(serialNo.Trim())).Append("%')");

        var sapList = ParseSapNoList(sapNo);
        if (sapList.Count == 1)
        {
            AppendSapNoCondition(where, sapList[0]);
        }
        else if (sapList.Count > 1)
        {
            where.Append(" AND (");
            for (int i = 0; i < sapList.Count; i++)
            {
                if (i > 0) where.Append(" OR ");
                where.Append('(');
                AppendSapNoConditionInner(where, sapList[i]);
                where.Append(')');
            }
            where.Append(')');
        }

        if (!string.IsNullOrWhiteSpace(model))
            where.Append(" AND Model LIKE '%").Append(Escape(model.Trim())).Append("%'");

        if (!string.IsNullOrWhiteSpace(partNo))
            where.Append(" AND PartNo LIKE '%").Append(Escape(partNo.Trim())).Append("%'");

        if (createFrom.HasValue)
            where.Append(" AND Createtime >= '").Append(createFrom.Value.ToString("yyyy-MM-dd 00:00:00")).Append('\'');

        if (createTo.HasValue)
            where.Append(" AND Createtime <= '").Append(createTo.Value.ToString("yyyy-MM-dd 23:59:59")).Append('\'');

        return where.ToString();
    }

    /// <summary>
    /// 单个 SAP 条件：精确匹配，或主号前缀匹配（可省略 -00080 行号）。
    /// </summary>
    static void AppendSapNoCondition(StringBuilder where, string token)
    {
        where.Append(" AND (");
        AppendSapNoConditionInner(where, token);
        where.Append(')');
    }

    static void AppendSapNoConditionInner(StringBuilder where, string token)
    {
        string t = Escape(token);
        // 完整值 / 主号前缀（11326172 可匹配 11326172-00080）/ 模糊兜底
        where.Append("SAPNo = '").Append(t).Append('\'')
            .Append(" OR SAPNo LIKE '").Append(t).Append("%'")
            .Append(" OR SAPNo LIKE '%").Append(t).Append("%'")
            .Append(" OR SalesOrderNo = '").Append(t).Append('\'')
            .Append(" OR SalesOrderNo LIKE '%").Append(t).Append("%'");
    }

    /// <summary>
    /// 解析批量 SAP No：换行、逗号、分号均可；可只填主号不要行号。
    /// </summary>
    public static List<string> ParseSapNoList(string? sapNo)
    {
        if (string.IsNullOrWhiteSpace(sapNo))
            return [];

        // 统一换行，再按常见分隔符拆分
        string normalized = sapNo
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        return normalized
            .Split(['\n', ',', ';', '，', '；', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    static List<ProductBrandPrintCodeRow> MapRows(DataSet? ds)
    {
        var list = new List<ProductBrandPrintCodeRow>();
        if (ds == null || ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
            return list;

        foreach (DataRow row in ds.Tables[0].Rows)
        {
            list.Add(new ProductBrandPrintCodeRow
            {
                ID = ToLong(row, "ID"),
                Model = ToStr(row, "Model"),
                Maxload = ToStr(row, "Maxload"),
                IPX6 = ToStr(row, "IPX6"),
                PartNo = ToStr(row, "PartNo"),
                SAPNo = ToStr(row, "SAPNo"),
                ProdDate = ToStr(row, "ProdDate"),
                SerialNo = ToStr(row, "SerialNo"),
                RevNo = ToStr(row, "RevNo"),
                Input = ToStr(row, "Input"),
                SerialNumber = ToStr(row, "SerialNumber"),
                ProductionNo = ToStr(row, "ProductionNo"),
                SalesOrderNo = ToStr(row, "SalesOrderNo"),
                Createtime = ToDateTime(row, "Createtime"),
                AllowReprint = ToBool(row, "AllowReprint"),
                ReprintCount = (int)ToLong(row, "ReprintCount")
            });
        }

        return list;
    }

    static string Escape(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    static string? ToStr(DataRow row, string col)
    {
        if (!row.Table.Columns.Contains(col) || row[col] == DBNull.Value)
            return null;
        return row[col]?.ToString();
    }

    static long ToLong(DataRow row, string col)
    {
        if (!row.Table.Columns.Contains(col) || row[col] == DBNull.Value)
            return 0;
        return long.TryParse(row[col].ToString(), out var v) ? v : 0;
    }

    static bool ToBool(DataRow row, string col)
    {
        if (!row.Table.Columns.Contains(col) || row[col] == DBNull.Value)
            return false;
        var raw = row[col];
        if (raw is bool b) return b;
        if (raw is byte by) return by != 0;
        if (raw is int i) return i != 0;
        return string.Equals(raw.ToString(), "1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(raw.ToString(), "true", StringComparison.OrdinalIgnoreCase);
    }

    static DateTime? ToDateTime(DataRow row, string col)
    {
        if (!row.Table.Columns.Contains(col) || row[col] == DBNull.Value)
            return null;
        return DateTime.TryParse(row[col].ToString(), out var dt) ? dt : null;
    }
}
