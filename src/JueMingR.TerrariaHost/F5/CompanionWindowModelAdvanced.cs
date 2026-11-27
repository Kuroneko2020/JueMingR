using System;
using System.Globalization;
using System.Linq;
using JueMingR.Platform.Llm;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class CompanionWindow
    {
        private void BuildModelAdvanced()
        {
            ModelHeading("服务与网络");
            ModelChoose("protocol", "协议", ProtocolName(modelDraft.Protocol), () => OpenModelPicker(ModelPicker.Protocol));
            if (modelPreset != null && modelConnections.Presets.Any(item => SameRegion(item, modelPreset) && item.Protocol == modelPreset.Protocol && item.Advanced != modelPreset.Advanced))
                ModelChoose("template", "服务入口", modelPreset.Label, () => OpenModelPicker(ModelPicker.Templates));
            ModelField("base", "Base URL", modelDraft.BaseUrl, value => modelDraft.BaseUrl = value.Trim(), 1500);
            ModelField("path", "推理操作路径", modelDraft.InferencePath, value => modelDraft.InferencePath = value.Trim(), 1024);
            BuildEndpointPreview();
            if (modelPreset?.Provider == "custom")
                ModelField("discovery-path", "模型列表路径", modelDraft.DiscoveryPath, value => modelDraft.DiscoveryPath = value.Trim(), 1024);
            BuildAuthentication();
            BuildProxy();
            ModelInteger("first-byte", "首字节超时（秒）", modelDraft.FirstByteSeconds, 1, 120, value => modelDraft.FirstByteSeconds = value);
            if (modelDraft.Stream) ModelInteger("idle", "流空闲超时（秒）", modelDraft.IdleSeconds, 1, 180, value => modelDraft.IdleSeconds = value);
            ModelInteger("timeout", "本轮总时限（秒）", modelDraft.RunTimeoutSeconds, 1, 600, value => modelDraft.RunTimeoutSeconds = value);
            ModelInteger("retry", "模型列表重试次数", modelDraft.RetryCount, 0, 2, value => modelDraft.RetryCount = value);

            ModelHeading("生成");
            ModelToggle("stream", "流式显示", modelDraft.Stream, value => modelDraft.Stream = value);
            ModelOptionalInteger("max-output", "最大输出", modelDraft.MaxOutput, 1, 262144, value => modelDraft.MaxOutput = value);
            ModelDecimal("temperature", "温度（0–2）", modelDraft.Temperature, 2, value => modelDraft.Temperature = value);
            ModelDecimal("top-p", "Top P（0–1）", modelDraft.TopP, 1, value => modelDraft.TopP = value);
            if (modelDraft.Protocol != "openai_responses")
                ModelField("stop", "停止词（用 | 分隔）", modelDraft.StopSequences?.Replace('\n', '|'), value => modelDraft.StopSequences = value.Replace('|', '\n'), 500, "使用模型默认值");
            string thinkingHint = modelDraft.Protocol == "anthropic_messages" ? "填写 adaptive、disabled 或思考预算" :
                modelPreset?.Provider == "deepseek" || modelPreset?.Provider == "glm" ? "填写 low、medium、high、enabled 或 disabled" : "填写 low、medium 或 high 等模型支持的值";
            ModelField("reasoning", "推理强度 / 思考", modelDraft.Reasoning, value => modelDraft.Reasoning = value.Trim(), 32, "使用模型默认值");
            ModelParagraph(thinkingHint + "。模型支持的值以服务商为准。");

            ModelHeading("上下文与活动");
            ModelInteger("input-budget", "上下文输入预算", modelDraft.InputBudget, 1024, 524288, value => modelDraft.InputBudget = value);
            ModelInteger("history-limit", "保留聊天条目数", modelDraft.HistoryLimit, 1, 500, value => modelDraft.HistoryLimit = value);
            ModelParagraph("输入接近预算时会用可见事实摘要续接。聊天页可以新开上下文，聊天记录仍可查看。");
            ModelToggle("game-actions", "允许游戏行动", modelDraft.GameActionsEnabled, value => modelDraft.GameActionsEnabled = value);
            ModelToggle("event-notices", "活动事件通知", modelDraft.LocalNoticesEnabled, value => modelDraft.LocalNoticesEnabled = value);
            if (modelDraft.GameActionsEnabled)
            {
                ModelInteger("rounds", "每轮模型往返上限", modelDraft.MaxModelTurns, 1, 16, value => modelDraft.MaxModelTurns = value);
                ModelInteger("tool-steps", "每轮工具步数上限", modelDraft.MaxToolCalls, 1, 32, value => modelDraft.MaxToolCalls = value);
            }

            ModelHeading("额外请求参数");
            ModelParagraph("使用单行 JSON 对象填写，内置消息、工具和鉴权字段由连接管理。带凭据的请求头请填在敏感请求头。");
            ModelField("extras", "额外参数（JSON）", modelDraft.ExtraParameters, value => modelDraft.ExtraParameters = value.Trim(), 8192, "使用默认参数");
            ModelField("headers", "普通请求头（JSON）", modelDraft.ExtraHeaders, value => modelDraft.ExtraHeaders = value.Trim(), 8192, "未设置");
            ModelSecretField("extra-secret-headers", "敏感请求头", "点击填写单行 JSON", modelDraft.SecretHeaders,
                value => modelDraft.SecretHeaders = value, 8192, () => modelConnections.RevealSecretHeaders(modelDraft));
            ModelButtonRow(("save-advanced", "保存连接", SaveModel, modelWrite == null && modelRead == null));
        }

        private void BuildEndpointPreview()
        {
            string raw = modelDraft.BaseUrl?.TrimEnd('/') ?? "";
            bool absolute = Uri.TryCreate(raw, UriKind.Absolute, out Uri address) && address.UserInfo.Length == 0 && address.Query.Length == 0 && address.Fragment.Length == 0;
            if (absolute)
            {
                string[] knownPaths = { "/chat/completions", "/responses", "/v1/messages" };
                string split = knownPaths.FirstOrDefault(path => address.AbsolutePath.EndsWith(path, StringComparison.OrdinalIgnoreCase));
                if (split != null)
                {
                    ModelParagraph("检测到完整请求地址。可以将末尾路径移到推理操作路径栏。");
                    ModelButtonRow(("split-url", "按建议拆分地址", () =>
                    {
                        string scope = modelDraft.CredentialScope();
                        modelDraft.BaseUrl = raw.Substring(0, raw.Length - split.Length);
                        modelDraft.InferencePath = split;
                        ModelDraftChanged(scope);
                    }, modelWrite == null));
                    return;
                }
                if (modelDraft.InferencePath?.StartsWith("/", StringComparison.Ordinal) == true) ModelParagraph("最终请求地址 · " + raw + modelDraft.InferencePath);
            }
        }

        private void BuildAuthentication()
        {
            ModelChoose("auth", "鉴权方式", AuthLabel(modelDraft.AuthMode), () =>
            {
                string scope = modelDraft.CredentialScope();
                string[] modes = modelPreset?.AuthRequired == true ? new[] { "bearer", "api_key_header" } :
                    new[] { "none", "optional_bearer", "bearer", "api_key_header" };
                modelDraft.AuthMode = NextModelOption(modelDraft.AuthMode, modes);
                ModelDraftChanged(scope);
            });
            if (modelDraft.Protocol != "anthropic_messages" && modelDraft.BaseUrl?.Contains("{workspaceId}") != true) return;
            if (modelDraft.Protocol == "anthropic_messages" && (modelPreset?.Provider == "anthropic" || modelPreset?.Provider == "custom"))
                ModelField("anthropic-version", "Claude API 版本", modelDraft.AnthropicVersion, value => modelDraft.AnthropicVersion = value.Trim(), 30, "由服务端决定");
            if (modelDraft.BaseUrl?.Contains("{workspaceId}") == true || modelPreset?.Provider == "anthropic" && modelDraft.Protocol == "anthropic_messages")
            {
                ModelField("workspace", "工作空间 ID", modelDraft.Workspace, value => modelDraft.Workspace = value.Trim(), 120, "按控制台要求填写");
                ModelParagraph(modelPreset?.Provider == "anthropic" ? "密钥可访问多个工作空间时，请指定本连接使用的空间。" : "业务空间地址与其工作空间 ID、地区和凭据对应。");
            }
        }

        private static string AuthLabel(string mode)
        {
            switch (mode)
            {
                case "bearer": return "Bearer Token";
                case "optional_bearer": return "Bearer（密钥可留空）";
                case "api_key_header": return "API Key 专用请求头";
                default: return "无鉴权";
            }
        }

        private void BuildProxy()
        {
            string label = modelDraft.ProxyMode == "custom" ? "自定义代理" : modelDraft.ProxyMode == "direct" ? "直连" : "使用系统代理";
            ModelChoose("proxy", "代理方式", label, () =>
            {
                string scope = modelDraft.CredentialScope();
                modelDraft.ProxyMode = NextModelOption(modelDraft.ProxyMode, new[] { "system", "direct", "custom" });
                ModelDraftChanged(scope);
            });
            if (modelDraft.ProxyMode != "custom") return;
            ModelField("proxy-url", "HTTP 代理地址", modelDraft.ProxyUrl, value => modelDraft.ProxyUrl = value.Trim(), 1500);
            ModelSecretField("proxy-secret", "代理凭据", "填写 用户名:密码", modelDraft.ProxyCredentials,
                value => modelDraft.ProxyCredentials = value, 4096, () => modelConnections.RevealProxyCredentials(modelDraft));
        }

        private static string NextModelOption(string current, string[] values) => values[(Array.IndexOf(values, current) + 1) % values.Length];

        private void ModelToggle(string id, string name, bool oldValue, Action<bool> write)
        {
            ModelChoose(id, name, oldValue ? "已开启" : "已关闭", () =>
            {
                string scope = modelDraft.CredentialScope();
                write(!oldValue); ModelDraftChanged(scope);
            });
        }

        private void ModelInteger(string id, string name, int value, int minimum, int maximum, Action<int> write) =>
            ModelField(id, name, value.ToString(CultureInfo.InvariantCulture), text => write(ParseModelInteger(name, text, minimum, maximum)), 9);

        private void ModelOptionalInteger(string id, string name, int? value, int minimum, int maximum, Action<int?> write) =>
            ModelField(id, name, value?.ToString(CultureInfo.InvariantCulture), text =>
                write(String.IsNullOrWhiteSpace(text) ? (int?)null : ParseModelInteger(name, text, minimum, maximum)), 9, "使用模型默认值");

        private static int ParseModelInteger(string name, string text, int minimum, int maximum)
        {
            if (Int32.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value >= minimum && value <= maximum) return value;
            throw new LlmException(LlmFailure.Configuration, name + "应为 " + minimum + " 到 " + maximum + " 之间的整数。");
        }

        private void ModelDecimal(string id, string name, double? value, double maximum, Action<double?> write) =>
            ModelField(id, name, value?.ToString(CultureInfo.InvariantCulture), text =>
            {
                if (String.IsNullOrWhiteSpace(text)) { write(null); return; }
                if (!Double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double parsed) || parsed < 0 || parsed > maximum)
                    throw new LlmException(LlmFailure.Configuration, name + "超出可用范围。");
                write(parsed);
            }, 16, "使用模型默认值");
    }
}
