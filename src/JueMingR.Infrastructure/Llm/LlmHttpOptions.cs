using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using JueMingR.Platform.Llm;
using static JueMingR.Infrastructure.Llm.LlmJson;

namespace JueMingR.Infrastructure.Llm
{
    internal sealed class LlmHttpOptions
    {
        private static readonly HashSet<string> ReservedParameters = new HashSet<string>(new[]
        {
            "model", "messages", "input", "instructions", "system", "stream", "stream_options", "tools", "tool_choice",
            "parallel_tool_calls", "functions", "function_call", "n", "max_tokens", "max_completion_tokens",
            "max_output_tokens", "temperature", "top_p", "stop", "stop_sequences", "thinking", "reasoning", "reasoning_effort",
            "previous_response_id", "conversation", "store", "include", "background", "request_id", "api_key", "password",
            "access_token", "token", "credentials", "authorization", "__type"
        }, StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> ReservedHeaders = new HashSet<string>(new[]
        {
            "Authorization", "X-Api-Key", "Api-Key", "Proxy-Authorization", "Proxy-Authenticate", "Host", "Connection",
            "Transfer-Encoding", "Content-Length", "Content-Type", "Cookie", "Set-Cookie", "Accept", "Accept-Encoding",
            "TE", "Trailer", "Upgrade", "Expect", "Keep-Alive", "anthropic-version", "anthropic-workspace-id"
        }, StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, string> publicHeaders;
        private readonly Dictionary<string, string> sensitiveHeaders;
        public Dictionary<string, object> Parameters { get; }
        public string[] SensitiveValues { get; }

        public LlmHttpOptions(LlmConnectionSnapshot connection, LlmConnectionSettings settings)
        {
            Parameters = ParseOptions(settings.ExtraParameters, "额外请求参数");
            if (Parameters.Keys.Any(ReservedParameters.Contains) || ContainsSecretFields(Parameters))
                throw Configuration("额外请求参数含受保护字段，请使用对应设置。");
            publicHeaders = ParseHeaders(settings.ExtraHeaders, false);
            sensitiveHeaders = ParseHeaders(connection.Secrets.Headers, true);
            if (publicHeaders.Keys.Any(sensitiveHeaders.ContainsKey))
                throw Configuration("额外请求头名称重复。");
            if (settings.ProxyMode == "custom" && connection.Secrets.ProxyCredentials.Length != 0 &&
                connection.Secrets.ProxyCredentials.IndexOf(':') < 1)
                throw Configuration("代理凭据须按“用户名:密码”填写。");
            ValidateReasoning(connection, settings);
            SensitiveValues = sensitiveHeaders.Values.Concat(new[] { connection.Secrets.ApiKey, connection.Secrets.ProxyCredentials })
                .Concat(ProxyParts(connection.Secrets.ProxyCredentials)).Where(value => !string.IsNullOrEmpty(value))
                .Distinct(StringComparer.Ordinal).OrderByDescending(value => value.Length).ToArray();
        }

        public void Apply(HttpRequestMessage request, LlmConnectionSnapshot connection, LlmConnectionSettings settings, bool discovery)
        {
            string mode = discovery && connection.Preset.DiscoveryAuthMode == "api_key_header"
                ? "api_key_header" : settings.AuthMode;
            if (connection.Secrets.ApiKey.Length != 0 && mode != "none")
            {
                string header = mode == "api_key_header"
                    ? (discovery && connection.Preset.DiscoveryAuthHeader != null ? connection.Preset.DiscoveryAuthHeader :
                       connection.Preset.AuthMode == "api_key_header" ? (connection.Preset.AuthHeader ?? "x-api-key") : "x-api-key") : "Authorization";
                string value = mode == "api_key_header" ? connection.Secrets.ApiKey : "Bearer " + connection.Secrets.ApiKey;
                Add(request, header, value);
            }

            if (settings.Protocol == "anthropic_messages" && !string.IsNullOrWhiteSpace(settings.AnthropicVersion))
                Add(request, "anthropic-version", settings.AnthropicVersion);
            if ((connection.Preset.Provider == "anthropic" || connection.Preset.Provider == "custom") &&
                settings.Protocol == "anthropic_messages" && !string.IsNullOrWhiteSpace(settings.Workspace))
                Add(request, "anthropic-workspace-id", settings.Workspace);
            foreach (var header in publicHeaders) Add(request, header.Key, header.Value);
            foreach (var header in sensitiveHeaders) Add(request, header.Key, header.Value);
        }

        private static void Add(HttpRequestMessage request, string key, string value)
        {
            if (!request.Headers.TryAddWithoutValidation(key, value))
                throw Configuration("请求头名称在该连接中不可用。");
        }

        private static bool ContainsSecretFields(object value)
        {
            if (Map(value) is Dictionary<string, object> fields)
                return fields.Any(pair => IsSensitiveName(pair.Key) || ContainsSecretFields(pair.Value));
            return Array(value).Any(ContainsSecretFields);
        }

        private static bool IsSensitiveName(string name)
        {
            string compact = Regex.Replace(name, "[^a-zA-Z0-9]", "").ToLowerInvariant();
            return compact.Contains("apikey") || compact.Contains("password") || compact.Contains("authorization") ||
                compact.Contains("accesstoken") || compact.Contains("secret");
        }

        private static Dictionary<string, object> ParseOptions(string input, string title)
        {
            if (string.IsNullOrWhiteSpace(input)) return new Dictionary<string, object>();
            try
            {
                var result = Map(Parse(input));
                if (result != null && result.Count <= 24) return result;
            }
            catch (Exception exception) when (IsJsonError(exception)) { }
            throw Configuration(title + "须为有效的 JSON 对象，最多 24 项。");
        }

        private static Dictionary<string, string> ParseHeaders(string input, bool secret)
        {
            var fields = ParseOptions(input, secret ? "敏感请求头" : "额外请求头");
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in fields)
            {
                if (!Regex.IsMatch(entry.Key, "^[a-zA-Z0-9!#$%&'*+.^_`|~-]{1,100}$") ||
                    ReservedHeaders.Contains(entry.Key) || entry.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase) ||
                    (!secret && IsSensitiveName(entry.Key)) || !(entry.Value is string value) || value.Length > 4096 ||
                    value.Any(character => character < 32 || character > 126) || headers.ContainsKey(entry.Key))
                    throw Configuration("请求头含保留名称、重复项、敏感明文或无效值。");
                headers.Add(entry.Key, value);
            }
            return headers;
        }

        private static string[] ProxyParts(string value)
        {
            int index = value.IndexOf(':');
            return index >= 0 ? new[] { value.Substring(index + 1) } : System.Array.Empty<string>();
        }

        private static void ValidateReasoning(LlmConnectionSnapshot connection, LlmConnectionSettings options)
        {
            if (string.IsNullOrWhiteSpace(options.Reasoning)) return;
            string value = options.Reasoning;
            if (options.Protocol == "anthropic_messages")
            {
                bool enabled = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int budget);
                if (enabled && budget >= 1024 && budget < (options.MaxOutput ?? 4096) ||
                    !enabled && (value == "disabled" || value == "adaptive")) return;
                throw Configuration("Messages 的思考设置应为 disabled、adaptive 或小于最大输出的思考预算（至少 1024）。");
            }
            if (new[] { "none", "minimal", "low", "medium", "high", "xhigh", "max" }.Contains(value)) return;
            if (options.Protocol == "openai_chat" &&
                (connection.Preset.Provider == "deepseek" || connection.Preset.Provider == "glm") &&
                (value == "enabled" || value == "disabled")) return;
            throw Configuration("推理强度与所选协议不匹配。");
        }

        public static LlmException Configuration(string message) => new LlmException(LlmFailure.Configuration, message);

        public static bool IsJsonError(Exception exception) =>
            exception is ArgumentException || exception is InvalidOperationException ||
            exception is FormatException || exception is System.Xml.XmlException || exception is OverflowException;
    }
}
