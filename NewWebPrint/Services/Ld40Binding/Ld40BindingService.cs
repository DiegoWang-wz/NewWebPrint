using Microsoft.Data.SqlClient;

namespace NewWebPrint.Services.Ld40Binding;

public sealed class Ld40BindingService(IConfiguration config, ILogger<Ld40BindingService> logger) : ILd40BindingService
{
    string AoboorConn => config.GetConnectionString("aoboor")
        ?? throw new InvalidOperationException("未配置 ConnectionStrings:aoboor");

    string JcLoginConn => config.GetConnectionString("JClogin_connStr")
        ?? throw new InvalidOperationException("未配置 ConnectionStrings:JClogin_connStr");

    SqlConnection OpenAoboorConnection()
    {
        var builder = new SqlConnectionStringBuilder(AoboorConn)
        {
            TrustServerCertificate = true
        };
        return new SqlConnection(builder.ConnectionString);
    }

    SqlConnection OpenJcLoginConnection()
    {
        var builder = new SqlConnectionStringBuilder(JcLoginConn)
        {
            TrustServerCertificate = true
        };
        return new SqlConnection(builder.ConnectionString);
    }

    static string ReadCellAsString(object? value)
        => value is null or DBNull ? string.Empty : value.ToString()?.Trim() ?? string.Empty;

    static bool IsOkTestResult(string? testResult)
        => string.Equals(testResult?.Trim(), "OK", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 与 WinForms barcode_NG_OK 一致：取该条码最新检测记录，OK 且外箱为空则通过。
    /// </summary>
    public async Task<(int Result, string? BoundOuterBox)> CheckBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP 1 TestResult, Outer_box_barcode
            FROM LD40 a
            WHERE RTRIM(LTRIM(CAST(a.Barcode AS NVARCHAR(64)))) = @Barcode
            ORDER BY a.TestTime DESC
            """;

        try
        {
            await using var conn = OpenAoboorConnection();
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Barcode", barcode.Trim());

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return (1, null);

            var testResult = ReadCellAsString(reader["TestResult"]);
            if (!IsOkTestResult(testResult))
                return (1, null);

            var boundOuter = ReadCellAsString(reader["Outer_box_barcode"]);
            if (!string.IsNullOrEmpty(boundOuter))
                return (2, boundOuter);

            return (0, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CheckBarcodeAsync failed, barcode={Barcode}", barcode);
            throw;
        }
    }

    public Task<bool> UpdateLd40OuterBoxBindingAsync(string outerBoxBarcode, string assemblyBarcode, CancellationToken ct = default)
        => UpdateLd40OuterBoxBindingBatchAsync(outerBoxBarcode, assemblyBarcode + "/", ct);

    public async Task<bool> UpdateLd40OuterBoxBindingBatchAsync(
        string outerBoxBarcode,
        string assemblyBarcodeListSlash,
        CancellationToken ct = default)
    {
        var barcodes = assemblyBarcodeListSlash
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(b => b.Trim())
            .Where(b => !string.IsNullOrEmpty(b))
            .ToArray();

        if (barcodes.Length == 0) return false;

        var paramNames = barcodes.Select((_, i) => "@p" + i).ToArray();
        var inClause = string.Join(",", paramNames);
        var sql = $"""
            UPDATE LD40 SET Outer_box_barcode = @outer
            WHERE RTRIM(LTRIM(CAST(Barcode AS NVARCHAR(64)))) IN ({inClause})
              AND RTRIM(LTRIM(CAST(TestResult AS NVARCHAR(20)))) = 'OK'
            """;

        try
        {
            await using var conn = OpenAoboorConnection();
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@outer", outerBoxBarcode.Trim());
            for (var i = 0; i < barcodes.Length; i++)
                cmd.Parameters.AddWithValue(paramNames[i], barcodes[i]);

            var affected = await cmd.ExecuteNonQueryAsync(ct);
            if (affected > 0)
                return true;

            // 已绑定同一外箱时 UPDATE 影响行数为 0，逐条复核
            foreach (var bc in barcodes)
            {
                var (check, bound) = await CheckBarcodeAsync(bc, ct);
                if (check == 2
                    && string.Equals(bound, outerBoxBarcode.Trim(), StringComparison.OrdinalIgnoreCase))
                    continue;

                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "UpdateLd40OuterBoxBindingBatchAsync failed");
            return false;
        }
    }

    public async Task<string?> SaveAssemblyOuterCasingBindingAsync(
        string outerBoxBarcode,
        string assemblyBarcodeSlash,
        string userId,
        string materialNo,
        string saveState,
        string caseNumber,
        string packingNumber,
        string orderNo,
        CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO dbo.Assembly_outer_casing_binding
                (Outer_box_barcode, Assembly_barcode, user_id, materialno, LASTTIME, save_state, Case_number, Packing__number, OrderNO)
            VALUES
                (@Outer, @Assembly, @UserId, @MaterialNo, GETDATE(), @SaveState, @CaseNumber, @PackingNumber, @OrderNo)
            """;

        try
        {
            await using var conn = OpenJcLoginConnection();
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Outer", outerBoxBarcode);
            cmd.Parameters.AddWithValue("@Assembly", assemblyBarcodeSlash);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@MaterialNo", materialNo);
            cmd.Parameters.AddWithValue("@SaveState", saveState);
            cmd.Parameters.AddWithValue("@CaseNumber", caseNumber);
            cmd.Parameters.AddWithValue("@PackingNumber", packingNumber);
            cmd.Parameters.AddWithValue("@OrderNo", orderNo);

            return await cmd.ExecuteNonQueryAsync(ct) == 1 ? "suc" : string.Empty;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveAssemblyOuterCasingBindingAsync failed");
            return ex.Message;
        }
    }

    public async Task<int> GetNextCaseNumberAsync(string orderNo, CancellationToken ct = default)
    {
        const string sql = """
            SELECT t.Case_number
            FROM (
                SELECT a.*, ROW_NUMBER() OVER (PARTITION BY OrderNO ORDER BY a.LASTTIME DESC) rw
                FROM Assembly_outer_casing_binding a
                WHERE a.OrderNO = @OrderNo
            ) t
            WHERE t.rw = 1
            """;

        try
        {
            await using var conn = OpenJcLoginConnection();
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@OrderNo", orderNo.Trim());

            var result = await cmd.ExecuteScalarAsync(ct);
            if (result == null || result == DBNull.Value)
                return 1;

            return int.Parse(result.ToString()!) + 1;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GetNextCaseNumberAsync failed, orderNo={OrderNo}", orderNo);
            return 1;
        }
    }

    public async Task<string?> GetAssemblyBarcodeByOuterBoxAsync(string outerBoxBarcode, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Assembly_barcode
            FROM dbo.Assembly_outer_casing_binding
            WHERE Outer_box_barcode = @Outer
            """;

        try
        {
            await using var conn = OpenJcLoginConnection();
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Outer", outerBoxBarcode.Trim());

            var result = await cmd.ExecuteScalarAsync(ct);
            return result == null || result == DBNull.Value ? null : result.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetAssemblyOuterCasingBindingAsync failed");
            throw;
        }
    }

    public async Task<string?> SearchLd40ByBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        const string sql = """
            SELECT t.ProductName, t.TestTime, t.TestResult, t.Barcode, t.Outer_box_barcode, t.ProdOrder, t.MachineNo, t.OperatorCode, t.SerialNo
            FROM (
                SELECT a.*, ROW_NUMBER() OVER (PARTITION BY Barcode ORDER BY a.TestTime DESC) rw
                FROM LD40 a
                WHERE RTRIM(LTRIM(CAST(a.Barcode AS NVARCHAR(64)))) = @Barcode
            ) t
            WHERE t.rw = 1
            """;

        try
        {
            await using var conn = OpenAoboorConnection();
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Barcode", barcode.Trim());

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return "未在 LD40 中找到该条码的记录。";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("产品型号: " + ReadCellAsString(reader["ProductName"]));
            sb.AppendLine("检测时间: " + ReadCellAsString(reader["TestTime"]));
            sb.AppendLine("检测结果: " + ReadCellAsString(reader["TestResult"]));
            sb.AppendLine("条码: " + ReadCellAsString(reader["Barcode"]));
            var outer = ReadCellAsString(reader["Outer_box_barcode"]);
            if (!string.IsNullOrEmpty(outer))
                sb.AppendLine("外箱条码: " + outer);
            sb.AppendLine("工单号: " + ReadCellAsString(reader["ProdOrder"]));
            sb.AppendLine("机台号: " + ReadCellAsString(reader["MachineNo"]));
            sb.AppendLine("操作工号: " + ReadCellAsString(reader["OperatorCode"]));
            sb.AppendLine("序列号: " + ReadCellAsString(reader["SerialNo"]));
            return sb.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SearchLd40ByBarcodeAsync failed");
            throw;
        }
    }
}
