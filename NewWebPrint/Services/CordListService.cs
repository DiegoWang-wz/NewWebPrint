using AutoMapper;
using AutoMapper.QueryableExtensions;
using NewWebPrint.DataDtos;
using NewWebPrint.DbContexts;
using NewWebPrint.Models;
using NewWebPrint.Models.Api;
using NewWebPrint.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace NewWebPrint.Services
{
    public interface ICordListService
    {
        Task<ApiResponse<List<CordListDto>>> GetAllAsync(CancellationToken ct = default);
        Task<ApiResponse<CordListDto>> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ApiResponse<bool>> AddAsync(CordListDto dto, CancellationToken ct = default);
        Task<ApiResponse<bool>> UpdateAsync(int id, CordListDto dto, CancellationToken ct = default);
        Task<ApiResponse<bool>> DeleteAsync(int id, CancellationToken ct = default);
        Task<int> GetLd40OkCountByBarcodeAsync(string barcode, CancellationToken ct = default);
    }

    public class CordListService : ICordListService
    {
        private readonly JCloginDbContext _db;
        private readonly IMapper _mapper;
        private readonly ILogger<CordListService> _logger;

        public CordListService(JCloginDbContext db, IMapper mapper, ILogger<CordListService> logger)
        {
            _db = db;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<ApiResponse<List<CordListDto>>> GetAllAsync(CancellationToken ct = default)
        {
            try
            {
                var list = await _db.CordLists
                    .AsNoTracking()
                    .OrderBy(c => c.Id)
                    .ProjectTo<CordListDto>(_mapper.ConfigurationProvider)
                    .ToListAsync(ct);
                return ApiResponse<List<CordListDto>>.Ok(list, "OK");
            }
            catch (Exception ex)
            {
                var root = ex.Root();
                _logger.LogError(ex, "CordList GetAllAsync failed, root={Root}", root.Message);
                return ApiResponse<List<CordListDto>>.Fail($"系统异常：{root.Message}", data: new List<CordListDto>());
            }
        }

        public async Task<ApiResponse<CordListDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            try
            {
                var dto = await _db.CordLists
                    .AsNoTracking()
                    .Where(x => x.Id == id)
                    .ProjectTo<CordListDto>(_mapper.ConfigurationProvider)
                    .SingleOrDefaultAsync(ct);
                if (dto == null)
                    return ApiResponse<CordListDto>.BadRequest("未找到该 CORD 记录");
                return ApiResponse<CordListDto>.Ok(dto, "OK");
            }
            catch (Exception ex)
            {
                var root = ex.Root();
                _logger.LogError(ex, "CordList GetByIdAsync failed, id={Id}, root={Root}", id, root.Message);
                return ApiResponse<CordListDto>.Fail($"系统异常：{root.Message}", data: default);
            }
        }

        public async Task<ApiResponse<bool>> AddAsync(CordListDto dto, CancellationToken ct = default)
        {
            try
            {
                if (dto == null)
                    return ApiResponse<bool>.BadRequest("参数不能为空");
                if (string.IsNullOrWhiteSpace(dto.JCPartNumber))
                    return ApiResponse<bool>.BadRequest("JC Part Number 不能为空");

                var entity = _mapper.Map<CordListEntity>(dto);
                entity.Id = 0;
                _db.CordLists.Add(entity);
                await _db.SaveChangesAsync(ct);
                return ApiResponse<bool>.Ok(true, "新增成功");
            }
            catch (Exception ex)
            {
                var root = ex.Root();
                _logger.LogError(ex, "CordList AddAsync failed, root={Root}", root.Message);
                return ApiResponse<bool>.Fail($"系统异常：{root.Message}", 0);
            }
        }

        public async Task<ApiResponse<bool>> UpdateAsync(int id, CordListDto dto, CancellationToken ct = default)
        {
            try
            {
                if (dto == null)
                    return ApiResponse<bool>.BadRequest("参数不能为空");
                if (string.IsNullOrWhiteSpace(dto.JCPartNumber))
                    return ApiResponse<bool>.BadRequest("JC Part Number 不能为空");

                var entity = await _db.CordLists.SingleOrDefaultAsync(x => x.Id == id, ct);
                if (entity == null)
                    return ApiResponse<bool>.BadRequest("CORD 记录不存在");

                _mapper.Map(dto, entity);
                entity.Id = id;
                await _db.SaveChangesAsync(ct);
                return ApiResponse<bool>.Ok(true, "更新成功");
            }
            catch (Exception ex)
            {
                var root = ex.Root();
                _logger.LogError(ex, "CordList UpdateAsync failed, id={Id}, root={Root}", id, root.Message);
                return ApiResponse<bool>.Fail($"系统异常：{root.Message}", 0);
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id, CancellationToken ct = default)
        {
            try
            {
                var entity = await _db.CordLists.SingleOrDefaultAsync(x => x.Id == id, ct);
                if (entity == null)
                    return ApiResponse<bool>.BadRequest("CORD 记录不存在");

                _db.CordLists.Remove(entity);
                await _db.SaveChangesAsync(ct);
                return ApiResponse<bool>.Ok(true, "删除成功");
            }
            catch (Exception ex)
            {
                var root = ex.Root();
                _logger.LogError(ex, "CordList DeleteAsync failed, id={Id}, root={Root}", id, root.Message);
                return ApiResponse<bool>.Fail($"系统异常：{root.Message}", 0);
            }
        }

        /// <summary>
        /// 使用独立连接字符串查询 aoboor.LD40 中指定条码且测试结果为 OK 的数量
        /// </summary>
        /// <param name="barcode">条码</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>符合条件的记录数</returns>
        public async Task<int> GetLd40OkCountByBarcodeAsync(string barcode, CancellationToken ct = default)
        {
            const string connectionString = "server=192.168.3.248;user id=sa;password=JCDB@2025(..)sa;database=aoboor;persistsecurityinfo=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
            const string sql = """
SELECT COUNT(*) AS Result
FROM LD40
WHERE Barcode = @Barcode AND TestResult = 'OK'
""";

            try
            {
                await using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync(ct);

                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Barcode", barcode ?? string.Empty);

                var result = await cmd.ExecuteScalarAsync(ct);
                if (result == null || result == DBNull.Value)
                    return 0;

                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                var root = ex.Root();
                _logger.LogError(ex, "GetLd40OkCountByBarcodeAsync failed, barcode={Barcode}, root={Root}", barcode, root.Message);
                throw;
            }
        }
    }
}
