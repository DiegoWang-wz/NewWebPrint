using Microsoft.Data.SqlClient;
using System.Data;
using System.Text;

namespace NewWebPrint.Utilities;

public class Storage
{
    // 连接字符串
    public string ConnectionString { get; set; }

    public Storage(string connectionString)
    {
        ConnectionString = connectionString;
    }

    /// <summary>
    /// 执行存储过程返回 DataSet
    /// </summary>
    public DataSet Ex(string procName, SqlParameter[] param)
    {
        using var conn = new SqlConnection(ConnectionString);
        using var cmd = new SqlCommand(procName, conn);
        cmd.CommandType = CommandType.StoredProcedure;

        // 原代码逻辑：遍历参数（此处根据原代码逻辑保留，虽然原代码注释掉了具体实现）
        if (param != null)
        {
            cmd.Parameters.AddRange(param);
        }

        using var adapter = new SqlDataAdapter(cmd);
        var ds = new DataSet();
        adapter.Fill(ds);
        return ds;
    }

    /// <summary>
    /// 是否存在该记录
    /// </summary>
    public virtual bool Exists(string tableName, string where)
    {
        string strSql = $"select count(1) from {tableName} where {where} ";
        object? obj = ExecuteScalar(strSql);

        if (obj == null || obj == DBNull.Value)
        {
            return false;
        }

        return int.Parse(obj.ToString()!) > 0;
    }

    /// <summary>
    /// 是否存在该记录 (双条件)
    /// </summary>
    public virtual bool Exists(string tableName, string where1, string where2)
    {
        string strSql = $"select count(1) from {tableName} where {where1} and {where2} ";
        object? obj = ExecuteScalar(strSql);

        if (obj == null || obj == DBNull.Value)
        {
            return false;
        }

        return int.Parse(obj.ToString()!) > 0;
    }

    /// <summary>
    /// 删除一条数据
    /// </summary>
    public virtual int Delete(string tableName, string strWhere)
    {
        try
        {
            string strSql = $"delete from {tableName}";
            if (!string.IsNullOrWhiteSpace(strWhere))
            {
                strSql += " where " + strWhere;
            }

            return ExecuteNonQuery(strSql);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// 获得数据列表
    /// </summary>
    public virtual DataSet? GetDataSet(string tableName, string strWhere)
    {
        try
        {
            string strSql = $"select * from {tableName}";
            if (!string.IsNullOrWhiteSpace(strWhere))
            {
                strSql += " where " + strWhere;
            }

            return ExecuteDataSet(strSql);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 获得数据列表 (指定列 + 默认 ID 排序)
    /// </summary>
    public virtual DataSet? GetDataSet(string tableName, string strWhere, string Columns)
    {
        string strSql = $"select {Columns} from {tableName}";
        if (!string.IsNullOrWhiteSpace(strWhere))
        {
            strSql += " where " + strWhere;
        }

        if (Columns.Contains("ID", StringComparison.OrdinalIgnoreCase))
        {
            strSql += " ORDER BY ID";
        }

        return ExecuteDataSet(strSql);
    }

    /// <summary>
    /// 获得数据列表 (指定列 + 指定排序)
    /// </summary>
    public virtual DataSet? GetDataSet(string tableName, string strWhere, string Columns, string OrderBy)
    {
        string strSql = $"select {Columns} from {tableName}";
        if (!string.IsNullOrWhiteSpace(strWhere))
        {
            strSql += " where " + strWhere;
        }

        if (!string.IsNullOrWhiteSpace(OrderBy))
        {
            strSql += " ORDER BY " + OrderBy;
        }

        return ExecuteDataSet(strSql);
    }

    /// <summary>
    /// 获得数据列表 (指定列 + 指定排序 + 是否倒序)
    /// </summary>
    public virtual DataSet? GetDataSet(string tableName, string strWhere, string Columns, string OrderBy, string desc)
    {
        string strSql = $"select {Columns} from {tableName}";
        if (!string.IsNullOrWhiteSpace(strWhere))
        {
            strSql += " where " + strWhere;
        }

        if (!string.IsNullOrWhiteSpace(OrderBy))
        {
            strSql += " ORDER BY " + OrderBy;
        }

        if (!string.IsNullOrWhiteSpace(desc)) // 注意：原代码逻辑有点反直觉，!desc 却加 Desc? 请根据实际需求确认
        {
            strSql += " " + desc;
        }

        return ExecuteDataSet(strSql);
    }

    /// <summary>
    /// 获取记录数
    /// </summary>
    public virtual int GetRecordCount(string tableName, string strWhere)
    {
        string strSql = $"select Count(*) from {tableName}";
        if (!string.IsNullOrWhiteSpace(strWhere))
        {
            strSql += " where " + strWhere;
        }

        try
        {
            object? obj = ExecuteScalar(strSql);
            if (obj == null || obj == DBNull.Value)
            {
                return 0;
            }

            return Convert.ToInt32(obj);
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// 获取排序的数据列表
    /// </summary>
    public virtual DataSet? GetDataSetSort(string tableName, string strWhere, string sortColumn)
    {
        try
        {
            string strSql = $"select * from {tableName}";
            if (!string.IsNullOrWhiteSpace(strWhere))
            {
                strSql += " where " + strWhere;
            }

            if (!string.IsNullOrWhiteSpace(sortColumn))
            {
                strSql += " ORDER BY " + sortColumn;
            }

            return ExecuteDataSet(strSql);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 添加一条数据
    /// </summary>
    public int AddRecord(string tableName, string[] column, string[] value)
    {
        try
        {
            if (column.Length != value.Length)
            {
                return 0;
            }

            // 使用 String.Join 简化循环
            string allc = string.Join(",", column);
            // 注意：这里手动加单引号非常危险 (SQL注入)，建议后期改为参数化
            string allv = "'" + string.Join("','", value) + "'";

            string strSql = $"INSERT INTO {tableName} ( {allc} ) VALUES ( {allv} ) ";
            return ExecuteNonQuery(strSql);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// 更新一条数据
    /// </summary>
    public int UpdateRecord(string tableName, string[] column, string[] value, string where)
    {
        try
        {
            if (column.Length != value.Length || string.IsNullOrWhiteSpace(where))
            {
                return 0;
            }

            var setParts = new List<string>();
            for (int i = 0; i < column.Length; i++)
            {
                setParts.Add($"{column[i]}='{value[i]}'");
            }

            string setStr = string.Join(" , ", setParts);

            string strSql = $"UPDATE {tableName} SET {setStr} where ({where}) ";
            return ExecuteNonQuery(strSql);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// 左联查询
    /// </summary>
    public DataSet? GetLeftDataSet(string table1, string table2, string leftOn, string where)
    {
        try
        {
            string sql = $"SELECT * from {table1} AS a LEFT JOIN {table2} AS b ON {leftOn} ";
            if (!string.IsNullOrWhiteSpace(where))
            {
                sql += " where " + where;
            }

            return ExecuteDataSet(sql);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 左联查询 (带排序)
    /// </summary>
    public DataSet? GetLeftDataSet(string table1, string table2, string leftOn, string where, string sort)
    {
        try
        {
            string sql = $"SELECT * from {table1} AS a LEFT JOIN {table2} AS b ON {leftOn} ";
            if (!string.IsNullOrWhiteSpace(where))
            {
                sql += " where " + where;
            }

            if (!string.IsNullOrWhiteSpace(sort))
            {
                sql += " ORDER BY " + sort;
            }

            return ExecuteDataSet(sql);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 左联查询 (带排序和倒序)
    /// </summary>
    public DataSet? GetLeftDataSet(string table1, string table2, string leftOn, string where, string sort, bool desc)
    {
        try
        {
            string sql = $"SELECT * from {table1} AS a LEFT JOIN {table2} AS b ON {leftOn} ";
            if (!string.IsNullOrWhiteSpace(where))
            {
                sql += " where " + where;
            }

            if (!string.IsNullOrWhiteSpace(sort))
            {
                sql += " ORDER BY " + sort + " desc"; // 这里的 desc 逻辑与 GetDataSet 里的逻辑似乎相反，按原代码保留
            }

            return ExecuteDataSet(sql);
        }
        catch
        {
            return null;
        }
    }

    #region 多级别左联查询

    /// <summary>
    /// 多级的左联查询 (基础版)
    /// </summary>
    public DataSet? GetConnectedDataSet(string[] tables, string[] leftOn, string where)
    {
        return BuildConnectedQuery(tables, leftOn, where, null, true, null, -1, -1);
    }

    /// <summary>
    /// 多级的左联查询 (带排序)
    /// </summary>
    public DataSet? GetConnectedDataSet(string[] tables, string[] leftOn, string where, string orderName, bool order)
    {
        return BuildConnectedQuery(tables, leftOn, where, orderName, order, null, -1, -1);
    }

    /// <summary>
    /// 多级的左联查询 (带列选择)
    /// </summary>
    public DataSet? GetConnectedDataSet(string[] tables, string[] leftOn, string where, string orderName, bool order,
        string[] columns)
    {
        return BuildConnectedQuery(tables, leftOn, where, orderName, order, columns, -1, -1);
    }

    /// <summary>
    /// 多级别左联查询 (带分页/限制)
    /// </summary>
    public DataSet? GetConnectedDataSet(string[] tables, string[] leftOn, string where, string orderName, bool order,
        string[] columns, int limits, int count)
    {
        return BuildConnectedQuery(tables, leftOn, where, orderName, order, columns, limits, count);
    }

    // 提取公共逻辑以减少重复代码
    private DataSet? BuildConnectedQuery(string[] tables, string[] leftOn, string where, string? orderName, bool order,
        string[]? columns, int offset, int fetch)
    {
        try
        {
            if (tables.Length < 2 || tables.Length - 1 != leftOn.Length || leftOn.Length < 1)
            {
                return null;
            }

            StringBuilder sql = new StringBuilder();
            sql.Append("SELECT ");

            if (columns != null && columns.Length > 0)
            {
                sql.Append(string.Join(" , ", columns));
            }
            else
            {
                sql.Append('*');
            }

            sql.Append(" FROM ");
            for (int n = 0; n < tables.Length; n++)
            {
                if (string.IsNullOrWhiteSpace(tables[n]))
                {
                    return null;
                }

                string tableSerial = "t" + (n + 1).ToString();
                if (n == 0)
                {
                    sql.Append($"{tables[n]} as {tableSerial}");
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(leftOn[n - 1]))
                    {
                        return null;
                    }

                    sql.Append($" LEFT JOIN {tables[n]} AS {tableSerial} ON {leftOn[n - 1]}");
                }
            }

            if (!string.IsNullOrWhiteSpace(where))
            {
                sql.Append(" WHERE " + where);
            }

            bool hasOrder = !string.IsNullOrWhiteSpace(orderName);
            if (hasOrder)
            {
                sql.Append(" ORDER BY " + orderName);
                if (!order)
                {
                    sql.Append(" desc ");
                }
            }

            // 修正：SQL Server 不支持 LIMIT，使用 OFFSET FETCH
            // 注意：OFFSET FETCH 必须配合 ORDER BY 使用
            if (offset >= 0 && fetch > 0)
            {
                if (!hasOrder)
                {
                    // 如果没有指定排序，SQL Server 分页需要一个默认排序，这里默认按第一列或需要在调用时传入
                    sql.Append(" ORDER BY (SELECT NULL) ");
                }

                sql.Append($" OFFSET {offset} ROWS FETCH NEXT {fetch} ROWS ONLY");
            }

            return ExecuteDataSet(sql.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return null;
        }
    }

    #endregion

    /// <summary>
    /// 执行一条 SQL 语句 (NonQuery)
    /// </summary>
    public virtual int ExecuteSQL(string sql)
    {
        return ExecuteNonQuery(sql);
    }

    /// <summary>
    /// 执行一条 SQL 语句 (DataSet)
    /// </summary>
    public virtual DataSet? ExecuteSQLDatable(string sql)
    {
        return ExecuteDataSet(sql);
    }

    /// <summary>
    /// 存储过程查询数据 (参数化)
    /// </summary>
    public DataSet? GetDatasetByProc(string procName, List<DBParam> paramList)
    {
        try
        {
            using var conn = new SqlConnection(ConnectionString);
            using var cmd = new SqlCommand(procName, conn);
            cmd.CommandType = CommandType.StoredProcedure;

            foreach (DBParam param in paramList)
            {
                // 注意：这里假设 DBParam 是你自定义的类，包含 paramName, value 等属性
                // 对于 SqlClient，通常不需要显式指定 Type，除非特殊情况
                var p = new SqlParameter(param.paramName, param.value ?? DBNull.Value);
                if (param.type != default) // 如果 DBParam 里有类型定义
                {
                    p.DbType = param.type;
                }

                cmd.Parameters.Add(p);
            }

            using var adapter = new SqlDataAdapter(cmd);
            var ds = new DataSet();
            adapter.Fill(ds);
            return ds;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 获取数据列表判断是否重复
    /// </summary>
    public virtual bool ExecuteSQLJudgeRePrint(string sql)
    {
        try
        {
            var ds = ExecuteDataSet(sql);
            if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
            {
                return true;
            }

            return false;
        }
        catch
        {
            // 原代码在异常时返回 true，这里保持一致
            return true;
        }
    }

    // ==========================================
    // 私有辅助方法 (替代 Enterprise Library)
    // ==========================================

    private int ExecuteNonQuery(string sql)
    {
        using var conn = new SqlConnection(ConnectionString);
        using var cmd = new SqlCommand(sql, conn);
        conn.Open();
        return cmd.ExecuteNonQuery();
    }

    private DataSet ExecuteDataSet(string sql)
    {
        using var conn = new SqlConnection(ConnectionString);
        using var cmd = new SqlCommand(sql, conn);
        using var adapter = new SqlDataAdapter(cmd);
        var ds = new DataSet();
        adapter.Fill(ds);
        return ds;
    }

    private object? ExecuteScalar(string sql)
    {
        using var conn = new SqlConnection(ConnectionString);
        using var cmd = new SqlCommand(sql, conn);
        conn.Open();
        return cmd.ExecuteScalar();
    }
}

// 补充原代码中缺失的 DBParam 类定义
public class DBParam
{
    public string paramName { get; set; } = string.Empty;
    public object? value { get; set; }
    public DbType type { get; set; } // 或者使用 SqlDbType
}