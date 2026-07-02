using Microsoft.AspNetCore.Mvc;

using NewWebPrint.DataDtos;

using NewWebPrint.Services;

using NewWebPrint.Utilities;



namespace NewWebPrint.Controllers;



[ApiController]

[Route("api/[controller]/[action]")]

public class CordListController(ICordListService cordListService) : ControllerBase

{

    [HttpGet]

    public async Task<LegacyApiResponse<List<CordListDto>>> GetAllAsync(CancellationToken ct = default)

    {

        var response = await cordListService.GetAllAsync(ct);

        return LegacyApiResponse<List<CordListDto>>.From(response);

    }



    [HttpGet]

    public async Task<LegacyApiResponse<CordListDto>> GetByIdAsync(int id, CancellationToken ct = default)

    {

        var response = await cordListService.GetByIdAsync(id, ct);

        return LegacyApiResponse<CordListDto>.From(response);

    }



    [HttpPost]

    public async Task<LegacyApiResponse<bool>> AddAsync([FromBody] CordListDto dto, CancellationToken ct = default)

    {

        var response = await cordListService.AddAsync(dto, ct);

        return LegacyApiResponse<bool>.From(response);

    }



    [HttpPost]

    public async Task<LegacyApiResponse<bool>> UpdateAsync(int id, [FromBody] CordListDto dto, CancellationToken ct = default)

    {

        var response = await cordListService.UpdateAsync(id, dto, ct);

        return LegacyApiResponse<bool>.From(response);

    }



    [HttpPost]

    public async Task<LegacyApiResponse<bool>> DeleteAsync(int id, CancellationToken ct = default)

    {

        var response = await cordListService.DeleteAsync(id, ct);

        return LegacyApiResponse<bool>.From(response);

    }



    /// <summary>

    /// 查询 aoboor.LD40 中指定条码且测试结果为 OK 的数量。

    /// GET: api/CordList/GetLd40OkCountByBarcode?barcode=xxxx

    /// （路由自动去掉 Async 后缀，勿再定义同名 action，否则会 AmbiguousMatchException）

    /// </summary>

    [HttpGet]

    public Task<int> GetLd40OkCountByBarcodeAsync(string barcode, CancellationToken ct = default)

        => cordListService.GetLd40OkCountByBarcodeAsync(barcode, ct);

}

