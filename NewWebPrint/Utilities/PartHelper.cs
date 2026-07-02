using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NewWebPrint.Utilities
{
    public sealed record SimplePartInfo(
        string Raw,
        string KindName, // 识别到的类型（ControllerBracket/RightMount/LeftMount/Fastener/Unknown）
        string Result, // UI 友好名称（中文）
        IReadOnlyDictionary<string, string> Fields
    );

    public static class PartCodeHelper
    {
        // =========================
        // 你当前这批：T25 的部件号映射（含数据库字段位）
        // =========================
        private static readonly Dictionary<string, PartRule> PartNoMap =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // T25控制器
                ["GHWL-V3924"] = new PartRule
                {
                    Kind = "Controller",
                    DisplayCn = "T25控制器",
                    DbField = "part2",
                    DbIndex = 2
                },

                // T25控制器支架
                ["GHWL-V3925"] = new PartRule
                {
                    Kind = "ControllerBracket",
                    DisplayCn = "T25控制器支架",
                    DbField = "part3",
                    DbIndex = 3
                },

                // T25右安装座
                ["GHWL-V3926"] = new PartRule
                {
                    Kind = "RightMount",
                    DisplayCn = "T25右安装座",
                    DbField = "part4",
                    DbIndex = 4
                },

                // T25左安装座
                ["GHWL-V3927"] = new PartRule
                {
                    Kind = "LeftMount",
                    DisplayCn = "T25左安装座",
                    DbField = "part5",
                    DbIndex = 5
                },

                // T25紧固件
                ["T25-JGJ"] = new PartRule
                {
                    Kind = "Fastener",
                    DisplayCn = "T25紧固件",
                    DbField = "part6",
                    DbIndex = 6
                },

                // 总成编码规则（固定部分）
                ["A10C2000X2S"] = new PartRule
                {
                    Kind = "Assembly",
                    DisplayCn = "T25横梁",
                    DbField = "part1",
                    DbIndex = 1,
                    CodeFormat = "Assembly"
                },
            };

        private sealed class PartRule
        {
            public string Kind { get; set; } = "Unknown";
            public string DisplayCn { get; set; } = "Unknown";
            public string DbField { get; set; } = ""; // part3/part4/part5/part6
            public int DbIndex { get; set; } = 0; // 3/4/5/6
            public string CodeFormat { get; set; } = "Default"; // Default, Assembly
        }

        /// <summary>
        /// ✅ 只做“编码识别部件 + 给出落库字段位”
        /// 支持三类格式：
        /// 1) PartNo#Product#KC#yyyyMMddNNNN#
        ///    例：GHWL-V3925#T25#KC094#202601010001#
        ///
        /// 2) PartNo#KC#yyyyMMddNNNN#
        ///    例：T25-JGJ#KC094#202601010001#
        /// 
        /// 3) Assembly code (continuous string, last 6 digits are rolling code)
        ///    例：A10C2000X2S003594
        /// </summary>
        public static SimplePartInfo Parse(string? code)
        {
            var raw = (code ?? "").Trim();
            if (string.IsNullOrWhiteSpace(raw))
                return Unknown(raw, "Empty");

            // 检查是否为总成编码格式（连续字符串，长度至少为6）
            if (!raw.Contains('#') && raw.Length >= 6)
            {
                return ParseAssemblyCode(raw);
            }

            var trimmed = raw.TrimEnd('#').Trim();
            var seg = trimmed.Split('#', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (seg.Length < 3)
                return Unknown(raw, $"SegmentsTooFew={seg.Length}");

            // A: [partNo, productKey, kc, batch]
            // B: [partNo, kc, batch]
            string partNo;
            string? productKey = null;
            string kc;
            string batch;

            if (seg.Length >= 4)
            {
                partNo = seg[0];
                productKey = seg[1];
                kc = seg[2];
                batch = seg[3];
            }
            else
            {
                partNo = seg[0];
                kc = seg[1];
                batch = seg[2];
            }

            var (dateOk, dateStr, seqStr, reason) = TryParseBatch(batch);

            // 识别部件 + 输出 db_field(part3~part6)
            var rule = ResolvePart(partNo, productKey);

            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["part_no"] = partNo,
                ["kc"] = kc,
                ["batch_raw"] = batch,
                ["part_name_cn"] = rule.DisplayCn,
            };

            if (!string.IsNullOrWhiteSpace(productKey))
                fields["product"] = productKey!;

            if (!string.IsNullOrWhiteSpace(rule.DbField))
            {
                fields["db_field"] = rule.DbField; // ✅ part3/part4/part5/part6
                fields["db_index"] = rule.DbIndex.ToString(); // ✅ 3/4/5/6
            }
            else
            {
                fields["db_field"] = "";
                fields["db_index"] = "0";
            }

            if (dateOk)
            {
                fields["date"] = dateStr!;
                fields["seq"] = seqStr ?? "";
            }
            else
            {
                fields["batch_parse_error"] = reason ?? "batch parse failed";
            }

            // Debug：保存分段
            for (int i = 0; i < seg.Length; i++)
                fields[$"seg{i}"] = seg[i];

            return new SimplePartInfo(
                Raw: raw,
                KindName: rule.Kind,
                Result: rule.DisplayCn,
                Fields: fields
            );
        }

        public static string GetDisplayNameByFeatureCode(string? featureCode, int dbIndex)
        {
            if (string.IsNullOrWhiteSpace(featureCode))
                return $"部件 {dbIndex}"; // 兜底

            var code = featureCode.Trim();

            // 1. 尝试从预定义的字典里匹配 (横梁、控制器等)
            if (PartNoMap.TryGetValue(code, out var rule))
            {
                return rule.DisplayCn;
            }

            // 2. 逻辑判断：如果包含 JGJ 关键字，自动识别为紧固件
            if (code.Contains("JGJ", StringComparison.OrdinalIgnoreCase))
            {
                return "紧固件包";
            }

            // 3. 如果都不匹配，显示数据库原始特征码，方便排查配置问题
            return code;
        }

        /// <summary>
        /// 解析总成编码（连续字符串，最后6位为滚码）
        /// 例：A10C2000X2S003594
        /// </summary>
        private static SimplePartInfo ParseAssemblyCode(string code)
        {
            var raw = code.Trim();
            if (string.IsNullOrWhiteSpace(raw))
                return Unknown(raw, "Empty");

            if (raw.Length < 6)
                return Unknown(raw, "AssemblyCodeTooShort");

            // 提取固定部分和滚码部分
            var fixedPart = raw.Substring(0, raw.Length - 6);
            var rollingCode = raw.Substring(raw.Length - 6);

            // 验证滚码是否为数字
            if (!rollingCode.All(char.IsDigit))
                return Unknown(raw, $"InvalidRollingCode={rollingCode}");

            // 识别部件（基于固定部分）
            var rule = ResolveAssemblyPart(fixedPart);

            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["part_no"] = fixedPart,
                ["rolling_code"] = rollingCode,
                ["full_code"] = raw,
                ["part_name_cn"] = rule.DisplayCn,
            };

            if (!string.IsNullOrWhiteSpace(rule.DbField))
            {
                fields["db_field"] = rule.DbField; // ✅ part3/part4/part5/part6
                fields["db_index"] = rule.DbIndex.ToString(); // ✅ 3/4/5/6
            }
            else
            {
                fields["db_field"] = "";
                fields["db_index"] = "0";
            }

            return new SimplePartInfo(
                Raw: raw,
                KindName: rule.Kind,
                Result: rule.DisplayCn,
                Fields: fields
            );
        }

        // =========================
        // helpers
        // =========================
        private static (bool ok, string? dateStr, string? seqStr, string? reason) TryParseBatch(string batch)
        {
            batch = (batch ?? "").Trim();
            if (batch.Length < 8)
                return (false, null, null, "batch length < 8");

            var datePart = batch.Substring(0, 8);
            var seqPart = batch.Length > 8 ? batch.Substring(8) : "";

            if (!DateTime.TryParseExact(datePart, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                return (false, null, null, $"invalid date yyyyMMdd: {datePart}");

            if (!string.IsNullOrEmpty(seqPart) && !seqPart.All(char.IsDigit))
                return (false, null, null, $"invalid seq digits: {seqPart}");

            return (true, datePart, seqPart, null);
        }

        private static PartRule ResolveAssemblyPart(string fixedPart)
        {
            // 直接匹配固定部分
            if (PartNoMap.TryGetValue(fixedPart, out var hit))
                return hit;

            // 兜底：识别为未知总成
            return new PartRule
            {
                Kind = "Assembly", DisplayCn = "UnknownAssembly", DbField = "", DbIndex = 0, CodeFormat = "Assembly"
            };
        }

        private static PartRule ResolvePart(string partNo, string? productKey)
        {
            // 直接匹配
            if (PartNoMap.TryGetValue(partNo, out var hit))
                return hit;

            // 兜底：识别紧固件，但不知道落哪个字段（你以后可以补映射）
            var upper = (partNo ?? "").ToUpperInvariant();
            if (upper.Contains("JGJ", StringComparison.Ordinal))
            {
                var pk = string.IsNullOrWhiteSpace(productKey) ? "" : productKey.Trim().ToUpperInvariant();
                return new PartRule
                {
                    Kind = "Fastener",
                    DisplayCn = $"{pk}紧固件".Trim(),
                    DbField = "", // 未定义就不给字段位，避免写错
                    DbIndex = 0
                };
            }

            return new PartRule { Kind = "Unknown", DisplayCn = "Unknown", DbField = "", DbIndex = 0 };
        }

        private static SimplePartInfo Unknown(string raw, string reason)
            => new(raw, "Unknown", "Unknown",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["reason"] = reason,
                    ["db_field"] = "",
                    ["db_index"] = "0"
                });
    }
}