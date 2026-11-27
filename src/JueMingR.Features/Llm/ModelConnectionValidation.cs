using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JueMingR.Platform.Llm;

namespace JueMingR.Features.Llm
{
    internal static class ModelConnectionValidation
    {
        private static readonly string[] CommonPaths = { "/chat/completions", "/responses", "/v1/messages" };

        public static LlmConnectionSnapshot Capture(LlmConnectionDraft draft, LlmPreset preset, LlmConnectionSecrets secrets,
            string wireIdentity, bool requireModel, bool request)
        {
            if (preset == null || preset.Protocol != draft.Protocol)
                throw Error("请选择该方案支持的协议。");
            if (preset.Policy == "reference_only")
                throw Error("此方案仅供参考，游戏内直连不可用。");
            if (string.IsNullOrWhiteSpace(draft.DisplayName) || draft.DisplayName.Length > 80)
                throw Error("请为连接填写 1–80 字的名称。");
            if (requireModel && string.IsNullOrWhiteSpace(draft.Model))
                throw Error("请填写或选择模型 ID。");
            if (draft.Model?.Length > 200 || draft.Model?.Any(char.IsControl) == true)
                throw Error("模型 ID 的格式不合适。");
            if (draft.AuthMode != "bearer" && draft.AuthMode != "optional_bearer" && draft.AuthMode != "api_key_header" && draft.AuthMode != "none")
                throw Error("请选择有效的鉴权方式。");
            if (!preset.CredentialKinds.Contains(draft.CredentialKind, StringComparer.Ordinal))
                throw Error("请选择此地区和计费方案适用的凭据类型。");
            if (preset.AuthRequired && draft.AuthMode == "none")
                throw Error("该厂商需要 API Key。");
            if (request && (preset.AuthRequired || draft.AuthMode == "bearer" || draft.AuthMode == "api_key_header") &&
                string.IsNullOrWhiteSpace(secrets.ApiKey))
                throw Error("请填写当前站点与计费方案的 API Key。");
            if (secrets.ApiKey.Length > 4096 || secrets.ApiKey.Any(char.IsControl) || secrets.ProxyCredentials.Length > 4096 ||
                secrets.ProxyCredentials.Any(char.IsControl) || secrets.SecretHeadersTooLong())
                throw Error("密钥、代理凭据或敏感请求头的格式不合适。");

            ValidateOptions(draft, preset);
            string location = Expand(draft.BaseUrl, draft.Workspace);
            Uri baseUrl = Base(location);
            string basePath = baseUrl.AbsolutePath.TrimEnd('/');
            if (CommonPaths.Any(path => basePath.EndsWith(path, StringComparison.OrdinalIgnoreCase)))
                throw Error("Base URL 应填接口公共前缀。请将末尾的请求路径移到操作路径栏。");
            Uri endpoint = Append(baseUrl, draft.InferencePath, false);

            Uri discovery = draft.DiscoveryEndpoint();
            if (preset.Provider == "custom" && !string.IsNullOrWhiteSpace(draft.DiscoveryPath))
            {
                try { discovery = Append(baseUrl, draft.DiscoveryPath, true); }
                catch (LlmException) { discovery = null; }
            }
            if (discovery != null && !SameOrigin(baseUrl, discovery)) discovery = null;
            return new LlmConnectionSnapshot(draft.Id, wireIdentity, draft, preset, secrets, endpoint, discovery);
        }

        private static bool SecretHeadersTooLong(this LlmConnectionSecrets secrets) => secrets.Headers.Length > 8192;

        private static string Expand(string raw, string workspace)
        {
            raw = raw ?? "";
            if (!raw.Contains("{workspaceId}")) return raw;
            if (string.IsNullOrEmpty(workspace) || !Regex.IsMatch(workspace, @"^[a-zA-Z0-9-]{1,63}$"))
                throw Error("请填写控制台提供的工作空间 ID。");
            return raw.Replace("{workspaceId}", workspace);
        }

        private static Uri Base(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.Length > 1500 || raw.Any(char.IsControl) ||
                raw.IndexOf((char)92) >= 0 || raw.Contains("{") || raw.Contains("}") || HasDotSegment(raw) ||
                !Uri.TryCreate(raw.Trim(), UriKind.Absolute, out Uri result) ||
                (result.Scheme != "https" && !(result.Scheme == "http" && result.IsLoopback)) ||
                string.IsNullOrEmpty(result.Host) || result.UserInfo.Length != 0 || result.Query.Length != 0 || result.Fragment.Length != 0)
                throw Error("请填写不含凭据、查询参数和片段的 HTTPS 地址。本机服务可用 HTTP。");
            return result;
        }

        private static bool HasDotSegment(string raw) =>
            Uri.UnescapeDataString(raw).Split('/', '?', '#').Any(segment => segment == "." || segment == "..");

        private static Uri Append(Uri root, string path, bool discovery)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length > 1024 || path.Any(char.IsControl) ||
                !path.StartsWith("/", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal) ||
                path.IndexOfAny(new[] { '\\', '#', '{', '}' }) >= 0 || HasDotSegment(path) ||
                path.IndexOf("%2f", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("%5c", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (path.Contains("?") && (!discovery || path != "/models?sub_type=chat")) ||
                !Uri.TryCreate(root.AbsoluteUri.TrimEnd('/') + path, UriKind.Absolute, out Uri url) || !SameOrigin(root, url))
                throw Error("请填写同一服务地址下的有效操作路径。");
            return url;
        }

        private static bool SameOrigin(Uri a, Uri b) => a.Scheme == b.Scheme && a.IdnHost == b.IdnHost && a.Port == b.Port;

        private static void ValidateOptions(LlmConnectionDraft draft, LlmPreset preset)
        {
            if ((draft.MaxOutput.HasValue && (draft.MaxOutput.Value < 1 || draft.MaxOutput.Value > 262144)) ||
                !Range(draft.Temperature, 0, 2) || !Range(draft.TopP, 0, 1) ||
                draft.InputBudget < 1024 || draft.InputBudget > 524288 || draft.HistoryLimit < 1 || draft.HistoryLimit > 500 ||
                draft.MaxModelTurns < 1 || draft.MaxModelTurns > 16 || draft.MaxToolCalls < 1 || draft.MaxToolCalls > 32 ||
                draft.RunTimeoutSeconds < 1 || draft.RunTimeoutSeconds > 600 || draft.RetryCount < 0 || draft.RetryCount > 2 ||
                draft.FirstByteSeconds < 1 || draft.FirstByteSeconds > 120 || draft.IdleSeconds < 1 || draft.IdleSeconds > 180)
                throw Error("高级选项中有数值超出范围。");
            if ((draft.Workspace?.Length ?? 0) > 120 || draft.Workspace?.Any(char.IsControl) == true ||
                (draft.AnthropicVersion?.Length ?? 0) > 30 || draft.AnthropicVersion?.Any(char.IsControl) == true ||
                (draft.StopSequences?.Length ?? 0) > 500 || (draft.Reasoning?.Length ?? 0) > 32 ||
                (draft.ExtraParameters?.Length ?? 0) > 8192 || (draft.ExtraHeaders?.Length ?? 0) > 8192)
                throw Error("高级选项中有输入过长或格式不合适。");
            if (draft.ProxyMode != "system" && draft.ProxyMode != "direct" && draft.ProxyMode != "custom")
                throw Error("请选择系统、直连或自定义代理。");
            if (!string.IsNullOrWhiteSpace(draft.ProxyUrl) &&
                (!Uri.TryCreate(draft.ProxyUrl, UriKind.Absolute, out Uri proxy) || proxy.Scheme != "http" ||
                 string.IsNullOrEmpty(proxy.Host) || proxy.UserInfo.Length != 0 || proxy.AbsolutePath != "/" ||
                 proxy.Query.Length != 0 || proxy.Fragment.Length != 0))
                throw Error("代理地址应为不含凭据、路径和查询参数的 HTTP 地址。");
            if (draft.ProxyMode == "custom" && string.IsNullOrWhiteSpace(draft.ProxyUrl))
                throw Error("请填写自定义代理地址。");
            if (draft.Protocol == "openai_responses" && !string.IsNullOrWhiteSpace(draft.StopSequences))
                throw Error("Responses 协议不支持停止词。");
            if (preset.Provider == "deepseek")
            {
                bool thinking = draft.Reasoning != "disabled" && draft.Reasoning != "none";
                if (thinking && draft.Temperature.HasValue)
                    throw Error("DeepSeek 思考模式不使用温度。请留空或关闭思考。");
                if (draft.TopP.HasValue && (draft.Model == "deepseek-flash" && (!thinking || draft.TopP < 0.95) ||
                    draft.Model == "deepseek-v4-pro" && thinking))
                    throw Error("该 DeepSeek 模型在当前思考模式下不使用这个 top_p 值。");
            }
        }

        private static bool Range(double? value, double min, double max) =>
            !value.HasValue || (!double.IsNaN(value.Value) && value.Value >= min && value.Value <= max);

        public static LlmException Error(string message) => new LlmException(LlmFailure.Configuration, message);
    }
}
