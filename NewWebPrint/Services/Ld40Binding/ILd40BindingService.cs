namespace NewWebPrint.Services.Ld40Binding;

/// <summary>LD40 外套管绑定与外箱标签相关数据访问（对应 WinForms LabelPrintForLD40binding）。</summary>
public interface ILd40BindingService
{
    /// <summary>条码校验：0=合格未绑定，1=检验不合格或无记录，2=已绑定外箱。</summary>
    Task<(int Result, string? BoundOuterBox)> CheckBarcodeAsync(string barcode, CancellationToken ct = default);

    Task<bool> UpdateLd40OuterBoxBindingAsync(string outerBoxBarcode, string assemblyBarcode, CancellationToken ct = default);

    Task<bool> UpdateLd40OuterBoxBindingBatchAsync(string outerBoxBarcode, string assemblyBarcodeListSlash, CancellationToken ct = default);

    Task<string?> SaveAssemblyOuterCasingBindingAsync(
        string outerBoxBarcode,
        string assemblyBarcodeSlash,
        string userId,
        string materialNo,
        string saveState,
        string caseNumber,
        string packingNumber,
        string orderNo,
        CancellationToken ct = default);

    Task<int> GetNextCaseNumberAsync(string orderNo, CancellationToken ct = default);

    Task<string?> GetAssemblyBarcodeByOuterBoxAsync(string outerBoxBarcode, CancellationToken ct = default);

    Task<string?> SearchLd40ByBarcodeAsync(string barcode, CancellationToken ct = default);
}
