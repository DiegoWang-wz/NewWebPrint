namespace NewWebPrint.Utilities;

using System;
using System.Data;
using Oracle.ManagedDataAccess.Client;

public class OracleStorage
{
    public OracleStorage()
    {
    }

    public OracleStorage(string _connectStr)
    {
        connectStr = _connectStr;
    }

    public string connectStr { get; set; } = string.Empty;

    #region Oracle 执行语句

    // ✅ 保留：方法名 / 参数 / 返回值 / 失败返回 0 的行为
    public int ExcuteSql(string sql)
    {
        try
        {
            using var conn = OpenConn();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandType = CommandType.Text;

            int rows = cmd.ExecuteNonQuery();

            // 兼容旧代码：以前会手动 CloseConn(conn)，现在 using 会自动释放
            return rows;
        }
        catch
        {
            return 0;
        }
    }

    // ✅ 保留：签名不变
    public OracleConnection OpenConn()
    {
        var conn = new OracleConnection
        {
            ConnectionString = connectStr
        };
        conn.Open();
        return conn;
    }

    // ✅ 保留：签名不变（using 下可能不会再被调用，但保留以兼容旧调用）
    public void CloseConn(OracleConnection conn)
    {
        if (conn == null) return;
        try
        {
            if (conn.State != ConnectionState.Closed)
                conn.Close();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        finally
        {
            conn.Dispose();
        }
    }

    #endregion

    #region ADD

    /// <summary>
    /// 添加一条数据
    /// </summary>
    public int AddRecord(string tableName, string[] column, string[] value)
    {
        try
        {
            if (column.Length != value.Length) return 0;

            string allc = string.Empty;
            string allv = string.Empty;

            for (int i = 0; i < column.Length; i++)
            {
                if (i != 0 && allc != "")
                {
                    allc += ",";
                    allv += ",";
                }

                if (column[i].Trim() != "")
                {
                    allc += column[i];
                    allv += "'" + value[i] + "'";
                }
            }

            string strSql = string.Format("INSERT INTO {0} ( {1} ) VALUES ( {2} ) ", tableName, allc, allv);
            return ExcuteSql(strSql);
        }
        catch
        {
            return 0;
        }
    }

    public int AddRecord(string tableName, string[] column, string[] value, string guidColumn, string guidValue)
    {
        try
        {
            if (column.Length != value.Length) return 0;

            string allc = string.Empty;
            string allv = string.Empty;

            for (int i = 0; i < column.Length; i++)
            {
                if (i != 0 && allc != "")
                {
                    allc += ",";
                    allv += ",";
                }

                if (column[i].Trim() != "")
                {
                    allc += column[i];
                    allv += "'" + value[i] + "'";
                }
            }

            allc += ", " + guidColumn;
            allv += ", " + guidValue;

            string strSql = string.Format("INSERT INTO {0} ( {1} ) VALUES ( {2} ) ", tableName, allc, allv);
            return ExcuteSql(strSql);
        }
        catch (Exception ex)
        {
            return 0;
        }
    }

    #endregion

    #region UPDATE

    /// <summary>
    /// 更新一条数据
    /// </summary>
    public int UpdateRecord(string tableName, string[] column, string[] value, string where)
    {
        try
        {
            if (column.Length != value.Length || where.Trim() == "") return 0;

            string set = string.Empty;
            for (int i = 0; i < column.Length; i++)
            {
                if (i != 0) set += " , ";
                string str = string.Format(" {0}='{1}' ", column[i], value[i]);
                set += str;
            }

            string strSql = string.Format("UPDATE {0} SET {1} where ({2}) ", tableName, set, where);
            return ExcuteSql(strSql);
        }
        catch
        {
            return 0;
        }
    }

    #endregion

    #region 获取DataTable

    public DataTable ExcuteSql_getDataTable(string sql)
    {
        DataTable dt = new DataTable();
        try
        {
            using var conn = OpenConn();
            using var adap = new OracleDataAdapter(sql, conn);
            adap.Fill(dt);
            return dt;
        }
        catch
        {
            return dt;
        }
    }

    #endregion
}
