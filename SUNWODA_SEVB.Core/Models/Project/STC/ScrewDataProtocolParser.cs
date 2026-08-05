using SUNWODA_SEVB.Core.Models.Project.STC;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SUNWODA_SEVB.Tool.Services
{
    /// <summary>
    /// 上位机螺丝拧紧数据协议解析器
    ///
    /// 处理应用层文本协议（在FrameParser完成传输层拆包之后调用）：
    ///   \x02{站号}\tScrewData\t{JSON正文}\t{结果码}\x03
    ///
    /// FrameParser 已剥离 \x02 / \x03，输入本解析器的格式为：
    ///   {站号}\tScrewData\t{JSON}\t{结果码}
    /// </summary>
    public static class ScrewDataProtocolParser
    {
        /// <summary>
        /// 从FrameParser剥离帧头帧尾后的消息字符串中提取并解析螺丝拧紧数据包
        /// </summary>
        /// <param name="message">Socket收到的完整消息（已去除STX/ETX）</param>
        /// <returns>解析后的数据包，解析失败返回null</returns>
        public static ScrewTighteningPackageResponse? TryParse(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return null;

            // 1. 剥离文本协议头尾（已知FrameParser已剥离STX/ETX，但做二次清理）
            var clean = message.Trim('\x02', '\x03', '\0', '\n', '\r');

            // 2. 按TAB分割文本协议字段
            //    格式: 站号 \t ScrewData \t JSON正文 \t 结果码
            string jsonBody;
            var parts = clean.Split('\t');

            if (parts.Length >= 3 &&
                string.Equals(parts[1], "ScrewData", StringComparison.OrdinalIgnoreCase))
            {
                // 文本协议格式：站号 + ScrewData + JSON + 结果码
                // JSON可能包含TAB，所以拼接索引2到倒数第2
                jsonBody = parts.Length > 3
                    ? string.Join("", parts.Skip(2).Take(parts.Length - 3))
                    : parts[2];
            }
            else
            {
                // 没有文本协议头，直接作为JSON尝试
                jsonBody = clean;
            }

            // 3. 修正常见JSON格式问题
            jsonBody = FixJson(jsonBody);
            if (string.IsNullOrWhiteSpace(jsonBody))
                return null;

            // 4. 反序列化
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<ScrewTighteningPackageResponse>(jsonBody, options);
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScrewDataProtocolParser] JSON解析失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>修正螺丝拧紧数据JSON中的常见格式问题</summary>
        private static string FixJson(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw;

            var json = raw.Trim();

            // 修正：ScrewCount值可能未正确引号包裹
            json = Regex.Replace(json, @"""ScrewCount"":\s*""?(\d+)""?", "\"ScrewCount\":\"$1\"");

            // 修正：数组元素之间缺少逗号
            json = Regex.Replace(json, @"}(\s*){", "},{");

            // 确保数组正确闭合
            if (json.Contains("\"ScrewTighteningInfoLis\"") && !json.EndsWith("}"))
            {
                var lastBrace = json.LastIndexOf('}');
                if (lastBrace > 0)
                {
                    var after = json[(lastBrace + 1)..].Trim();
                    if (!after.StartsWith("]"))
                        json = json[..(lastBrace + 1)] + "]" + after;
                    if (!json.TrimEnd().EndsWith("]"))
                        json = json.TrimEnd() + "]";
                    if (!json.TrimEnd().EndsWith("}"))
                        json = json.TrimEnd() + "}";
                }
            }

            return json;
        }
    }
}
