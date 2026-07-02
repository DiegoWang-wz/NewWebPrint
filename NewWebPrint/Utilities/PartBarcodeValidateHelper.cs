using System;

namespace NewWebPrint.Utilities
{
    /// <summary>
    /// 部件条码校验：根据部件信息中的“条码前缀”校验扫描/输入的完整条码是否匹配。
    /// 条码格式：前缀 + 日期(8位) + 滚码(4位) + #，例如 GHWL-V7784#T25#KC094#202601010001#
    /// 只校验“扫描条码是否以期望前缀开头”，不再截断末尾的日期和滚码。
    /// </summary>
    public static class PartBarcodeValidateHelper
    {
        /// <summary>
        /// 校验扫描的条码前缀是否与部件期望的前缀一致。
        /// </summary>
        /// <param name="expectedPrefix">部件信息中的条码前缀，如 GHWL-V7784#T25#KC094#</param>
        /// <param name="scannedBarcode">用户填入/扫描的完整条码，如 GHWL-V7784#T25#KC094#202601010001#</param>
        /// <returns>校验结果：是否通过及提示信息</returns>
        public static (bool IsValid, string Message) Validate(string? expectedPrefix, string? scannedBarcode)
        {
            var prefix = (expectedPrefix ?? "").Trim();
            var scanned = (scannedBarcode ?? "").Trim();

            if (string.IsNullOrEmpty(prefix))
                return (false, "部件条码前缀为空，无法校验");

            if (string.IsNullOrEmpty(scanned))
                return (false, "请输入或扫描条码");

            // 只判断“扫描条码是否以期望前缀开头”
            // 兼容 expectedPrefix 末尾是否带 '#'
            var expectedA = prefix;
            var expectedB = prefix.TrimEnd('#').Trim();

            if (scanned.StartsWith(expectedA, StringComparison.Ordinal) ||
                (!string.IsNullOrEmpty(expectedB) && scanned.StartsWith(expectedB, StringComparison.Ordinal)))
                return (true, "校验通过");

            return (false, $"条码前缀不匹配：期望以 [{expectedA}] 开头");
        }

        /// <summary>
        /// 从完整条码末尾去掉“8位日期+4位滚码+#”，返回前缀部分。
        /// 若未匹配到该格式，则原样返回（便于调用方判断）。
        /// </summary>
        public static string StripDateRollSuffix(string? fullBarcode)
        {
            // 保留该方法以兼容可能的旧调用方，但 Validate() 已不再依赖它。
            return (fullBarcode ?? "").Trim();
        }
    }
}
