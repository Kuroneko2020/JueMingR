using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JueMingR.Features.Llm;
using JueMingR.Features.Text;
using JueMingR.Platform.Llm;
using JueMingR.TerrariaHost.Input;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class CompanionWindow
    {
        private enum ModelPicker { None, Connections, Provider, Plan, Region, Credential, Protocol, Templates, Models }
        private sealed class ModelFieldView
        {
            internal string Id, Value;
            internal F5Rect Rect;
            internal bool Secret;
        }
        private sealed class ModelReadResult
        {
            internal string Message;
            internal IReadOnlyList<LlmModelInfo> Models;
            internal bool Success;
        }

        private LlmConnections modelConnections;
        private IReadOnlyList<LlmConnectionInfo> modelList = Array.Empty<LlmConnectionInfo>();
        private LlmConnectionDraft modelDraft;
        private LlmPreset modelPreset, pendingModelPreset;
        private IReadOnlyList<LlmModelInfo> discoveredModels;
        private Task<ModelReadResult> modelRead;
        private Task<string> modelWrite;
        private CancellationTokenSource modelCancellation;
        private Action modelWriteCompleted;
        private ModelPicker modelPicker;
        private string modelSearch = "", modelStatus, modelChatTest, modelToolTest, modelReadKind, modelStorage;
        private string modelRevealedId, modelRevealedValue;
        private bool modelAdvanced, modelExplain, modelConfirmDelete;
        private int modelCapabilityFilter, modelOffset, modelMaximum, modelMainOffset;
        private float modelY, modelRow, modelX, modelWidth, modelTextWidth;
        private int modelTextSkin = -1;
        private F5Rect modelScrollView;
        private readonly List<ModelFieldView> modelFields = new List<ModelFieldView>();
        private readonly Dictionary<string, F5Element[]> modelParagraphs = new Dictionary<string, F5Element[]>();
        private readonly List<F5Element> modelRevealLabels = new List<F5Element>();
        private SingleLineEditView modelEditView = new SingleLineEditView();
        private TextEditBuffer modelPresentedEditor;
        private long modelEditRevision, modelCaretRevision;
        private float modelEditWidth;
        private string modelComposition;
        private F5Element[] modelRevealLines;
        private float modelRevealWidth;

        private void ObserveModel()
        {
            var connections = host.ChatConnections;
            if (!ReferenceEquals(connections, modelConnections))
            {
                RetireModelRead();
                HideModelSecrets();
                aiInput.End(true);
                modelConnections = connections;
                modelDraft = null;
                modelStorage = null;
                modelList = Array.Empty<LlmConnectionInfo>();
                contentInvalidated = true;
            }
            if (connections == null) return;
            string status = connections.StorageStatus;
            if (modelStorage != status || modelDraft == null)
            {
                modelStorage = status;
                RefreshModelList();
                if (modelDraft == null)
                {
                    Guid? id = modelList.FirstOrDefault(row => row.Id == connections.DefaultId)?.Id ?? modelList.FirstOrDefault()?.Id;
                    LoadModelDraft(connections.Edit(id));
                }
                contentInvalidated = true;
            }
            PollModelOperations();
        }

        private void RefreshModelList() => modelList = modelConnections.List();
        private LlmConnectionInfo StoredModel() => modelList.FirstOrDefault(row => row.Id == modelDraft?.Id);

        private void LoadModelDraft(LlmConnectionDraft value)
        {
            RetireModelRead();
            HideModelSecrets();
            modelDraft = value;
            modelPreset = modelConnections.Presets.FirstOrDefault(preset => preset.Id == value.PresetId);
            string probes = modelConnections.GetProbeSummary(value);
            modelStatus = String.IsNullOrEmpty(probes) ? null : ModelAddressSummary() + " / " + value.Model + " · " + probes;
            modelConfirmDelete = false;
            pendingModelPreset = null;
            Invalidate();
        }

        private void BuildModel(float row)
        {
            modelScrollView = new F5Rect(bodyView.X + 4, bodyView.Y, bodyView.Width - 12, bodyView.Height);
            modelX = modelScrollView.X + 4;
            modelWidth = modelScrollView.Width - 10;
            modelRow = row;
            if (modelTextWidth != modelWidth || modelTextSkin != skin)
            {
                modelTextWidth = modelWidth; modelTextSkin = skin;
                modelParagraphs.Clear(); DropModelEditView();
                modelRevealLines = null;
            }
            int firstControl = controls.Count, firstLabel = labels.Count;
            modelFields.Clear();
            modelRevealLabels.Clear();
            BuildModelRows();
            modelMaximum = Math.Max(0, (int)Math.Ceiling(modelY + modelOffset - modelScrollView.Bottom));
            if (modelOffset <= modelMaximum) return;
            modelOffset = modelMaximum;
            controls.RemoveRange(firstControl, controls.Count - firstControl);
            labels.RemoveRange(firstLabel, labels.Count - firstLabel);
            modelFields.Clear();
            modelRevealLabels.Clear();
            BuildModelRows();
        }

        private void BuildModelRows()
        {
            modelY = modelScrollView.Y + 2 - modelOffset;
            if (modelConnections == null || modelDraft == null)
            {
                ModelParagraph("模型配置当前不可用。");
                return;
            }
            if (modelPicker != ModelPicker.None) { BuildModelPicker(); return; }

            bool saving = modelWrite != null;
            var stored = StoredModel();
            ModelHeading("连接档案");
            ModelButtonRow(
                ("connections", stored == null ? "连接列表" : "切换连接", () => OpenModelPicker(ModelPicker.Connections), !saving),
                ("create", "新增", () => LoadModelDraft(modelConnections.Edit()), !saving),
                ("copy", "复制配置", () => LoadModelDraft(modelConnections.Copy(stored.Id)), stored != null && !saving),
                ("delete", "删除", () => { modelConfirmDelete = true; Invalidate(); }, stored != null && !saving));
            if (modelConfirmDelete && stored != null)
            {
                int count = modelConnections.GetAffectedBindingCount(stored.Id);
                ModelParagraph("删除「" + stored.DisplayName + "」？明确绑定此连接的 " + count + " 名队友会变为未配置。" +
                    (modelConnections.DefaultId == stored.Id ? "继承此默认连接的队友也会变为未配置。" : ""));
                ModelButtonRow(("confirm-delete", "确认删除", () => StartModelWrite(() => modelConnections.DeleteAsync(stored.Id), "已删除连接。", () => LoadModelDraft(modelConnections.Edit())), !saving),
                    ("cancel-delete", "取消", () => { modelConfirmDelete = false; Invalidate(); }, true));
            }
            ModelField("name", "连接名称", modelDraft.DisplayName, value => modelDraft.DisplayName = value, 80);
            bool enabled = modelConnections.Enabled;
            string defaultText = modelConnections.DefaultId == stored?.Id && stored != null ? "取消默认连接" : "设为默认连接";
            ModelButtonRow(("enabled", enabled ? "关闭直连" : "启用直连",
                    () => StartModelWrite(() => modelConnections.SetEnabledAsync(!enabled), enabled ? "已关闭直连。" : "已启用直连。"), !saving),
                ("default", defaultText, () => StartModelWrite(() => modelConnections.SetDefaultAsync(modelConnections.DefaultId == stored.Id ? (Guid?)null : stored.Id),
                    defaultText == "取消默认连接" ? "已取消默认连接。" : "已设置默认连接。"), stored != null && !saving));
            BuildModelBinding(stored, saving);
            if (!String.IsNullOrEmpty(modelStorage)) ModelParagraph(modelStorage);

            ModelHeading("厂商与计费方案");
            ModelChoose("provider", "厂商", ProviderName(modelPreset), () => OpenModelPicker(ModelPicker.Provider));
            ModelChoose("plan", "计费方案", modelPreset?.PlanLabel, () => OpenModelPicker(ModelPicker.Plan), modelPreset != null);
            if (modelPreset != null)
            {
                bool regionChoice = modelConnections.Presets.Any(item => SamePlan(item, modelPreset) && item.Region != modelPreset.Region && !item.Advanced);
                if (regionChoice) ModelChoose("region", "地区", modelPreset.RegionLabel, () => OpenModelPicker(ModelPicker.Region));
                ModelParagraph(PlanScope(modelPreset.Policy));
                BuildPresetNotes();
            }
            if (pendingModelPreset != null)
            {
                ModelParagraph("你修改过服务地址。应用其他预设会替换地址、模型和凭据。是否继续？");
                ModelButtonRow(("apply-preset", "应用预设", () => ApplyModelPreset(pendingModelPreset, true), true),
                    ("keep-preset", "保留当前配置", () => { pendingModelPreset = null; Invalidate(); }, true));
            }

            if (modelPreset?.Policy == "reference_only")
            {
                if (modelPreset.CredentialKinds?.Count > 0)
                    ModelParagraph("适用凭据 · " + String.Join("、", modelPreset.CredentialKinds.Select(ModelCredentialKindName)));
                ModelParagraph("请选择能在游戏内使用的计费方案，以继续设置密钥和模型。");
                return;
            }
            ModelHeading("连接与模型");
            if (modelPreset?.CredentialKinds?.Count > 1)
                ModelChoose("credential", "凭据类型", ModelCredentialKindName(modelDraft.CredentialKind), () => OpenModelPicker(ModelPicker.Credential));
            BuildApiKey();
            ModelField("model-id", "模型 ID", modelDraft.Model, value => modelDraft.Model = value.Trim(), 200);
            BuildModelDiscovery();
            ModelParagraph(ProtocolName(modelDraft.Protocol) + " · " + ModelAddressSummary());
            ModelParagraph("测试会实际请求模型，费用按服务商的用量规则计算。工具测试只在本地回填测试数据。");
            ModelButtonRow(("test-chat", "测试连接", () => StartModelTest(false), !saving && modelRead == null),
                ("test-tools", "测试工具调用", () => StartModelTest(true), !saving && modelRead == null),
                ("save", "保存连接", SaveModel, !saving && modelRead == null));
            if (!String.IsNullOrEmpty(modelChatTest)) ModelParagraph(modelChatTest);
            if (!String.IsNullOrEmpty(modelToolTest)) ModelParagraph(modelToolTest);
            if (!String.IsNullOrEmpty(modelStatus)) ModelParagraph(modelStatus);
            if (modelRead != null)
                ModelButtonRow(("cancel-read", "取消本次请求", () => { CancelModelRequest(); ModelMessage("已取消请求。"); }, true));
            ModelButtonRow(("advanced", modelAdvanced ? "收起高级设置" : "展开高级设置", () => { modelAdvanced = !modelAdvanced; HideModelSecrets(); Invalidate(); }, true));
            if (modelAdvanced) BuildModelAdvanced();
        }

        private void BuildModelBinding(LlmConnectionInfo stored, bool saving)
        {
            var profile = CurrentProfile();
            if (profile == null) return;
            Guid? binding = modelConnections.GetBinding(profile.OwnerId, profile.Id);
            string label = binding.HasValue ? modelList.FirstOrDefault(row => row.Id == binding.Value)?.DisplayName ?? "未配置" : "继承默认连接";
            ModelParagraph(profile.Name + " · " + label);
            ModelButtonRow(("bind", "绑定当前连接", () => StartModelWrite(() => modelConnections.SetBindingAsync(profile.OwnerId, profile.Id, stored.Id), "已为该队友设置连接。"), stored != null && !saving && binding != stored.Id),
                ("inherit", "继承默认", () => StartModelWrite(() => modelConnections.SetBindingAsync(profile.OwnerId, profile.Id, null), "该队友现在继承默认连接。"), binding.HasValue && !saving),
                ("unbind", "停止使用连接", () => StartModelWrite(() => modelConnections.SetBindingAsync(profile.OwnerId, profile.Id, Guid.Empty), "该队友暂时不使用模型连接。"), binding != Guid.Empty && !saving));
        }

        private void SaveModel()
        {
            var draft = modelDraft;
            StartModelWrite(() => modelConnections.SaveAsync(draft), "连接已保存。", () =>
            {
                // Keep successful probes on the very draft they tested until
                // the player changes a request field. Reload only secret state.
                var saved = modelConnections.Edit(draft.Id);
                if (!ReferenceEquals(modelDraft, draft)) return;
                draft.HasStoredKey = saved.HasStoredKey;
                draft.SecretScope = saved.SecretScope;
                draft.SecretVersion = saved.SecretVersion;
                draft.ApiKey = draft.SecretHeaders = draft.ProxyCredentials = null;
                HideModelSecrets();
            });
        }

        private void BuildApiKey()
        {
            string status = modelDraft.ApiKey == null ? (modelDraft.HasStoredKey ? "已保存 · 点击替换" : "粘贴当前方案的 API Key") :
                (modelDraft.ApiKey.Length == 0 ? "待清除 · 保存后生效" : "已替换 · 保存后生效");
            ModelSecretField("api-key", "API Key", status, modelDraft.ApiKey, value => modelDraft.ApiKey = value, 4096,
                () => modelConnections.RevealApiKey(modelDraft));
        }

        private void BuildModelDiscovery()
        {
            bool canRead = modelPreset?.DiscoveryStatus == "provider_specific" || modelPreset?.DiscoveryStatus == "documented" ||
                modelPreset?.Provider == "custom" && !String.IsNullOrWhiteSpace(modelDraft.DiscoveryPath);
            if (!canRead) ModelParagraph("此入口可手动填写模型 ID。模型权限以该账号的控制台为准。");
            var candidates = modelPreset?.Candidates;
            if (candidates?.Count > 0 && discoveredModels == null)
            {
                ModelParagraph("官方候选 · 核对日期 " + modelPreset.VerifiedAt + "。账号是否可用以实际测试为准。");
                ModelButtonRow(("candidates", "查看官方候选", () => OpenModelPicker(ModelPicker.Models), true));
            }
            if (canRead) ModelButtonRow(("discover", "获取模型", StartModelDiscovery, modelRead == null && modelWrite == null),
                ("view-models", "选择模型", () => OpenModelPicker(ModelPicker.Models), discoveredModels?.Count > 0));
        }

        private string ModelAddressSummary()
        {
            string raw = modelDraft.BaseUrl ?? "";
            if (raw.Contains("{workspaceId}") && !String.IsNullOrEmpty(modelDraft.Workspace)) raw = raw.Replace("{workspaceId}", modelDraft.Workspace);
            return Uri.TryCreate(raw, UriKind.Absolute, out Uri address) && address.UserInfo.Length == 0 ? address.Authority : "请在高级设置填写服务地址";
        }

        private void BuildPresetNotes()
        {
            if (!modelExplain)
            {
                string text = modelPreset.Policy == "reference_only" ? "查看使用范围与官方说明" : "查看凭据与模型权益";
                ModelButtonRow(("details", text, () => { modelExplain = true; Invalidate(); }, true));
                return;
            }
            ModelParagraph(modelPreset.Note);
            ModelParagraph("公开接入说明核对于 " + modelPreset.VerifiedAt + "。尚无本连接的实测结果时，以测试和控制台为准。");
            if (modelPreset.Sources?.Count > 0)
                ModelButtonRow(("official", "复制官方链接", () => ModelMessage(aiInput.TryCopy(String.Join(Environment.NewLine, modelPreset.Sources)) ? "已复制官方链接。" : "无法复制官方链接。"), true),
                    ("hide-details", "收起说明", () => { modelExplain = false; Invalidate(); }, true));
        }

        private static string PlanScope(string policy)
        {
            switch (policy)
            {
                case "reference_only": return "此方案仅作参考，游戏内直连不可用。";
                case "interactive_tools_conditional": return "仅限符合条件的交互工具，决明R的适用性待服务商确认。请求需手动发起。";
                case "personal_interactive_conditional": return "仅限套餐允许的个人交互，额度和模型权限取决于该账号。请求需手动发起。";
                case "general_api": return "使用该站点的常规 API 凭据。额度以该服务商的控制台为准。";
                default: return null;
            }
        }

        private static bool SamePlan(LlmPreset a, LlmPreset b) => a.Provider == b.Provider && a.PlanId == b.PlanId;
        private static bool SameRegion(LlmPreset a, LlmPreset b) => SamePlan(a, b) && a.Region == b.Region;
        private static string ProtocolName(string id)
        {
            switch (id)
            {
                case "openai_chat": return "Chat Completions";
                case "openai_responses": return "Responses";
                case "anthropic_messages": return "Messages";
                default: return "请选择协议";
            }
        }

        private static string ModelCredentialKindName(string id)
        {
            switch (id)
            {
                case "standard_api_key": return "标准 API Key";
                case "legacy_compatible_key": return "旧版 Compatible Key";
                case "personal_plan_key": return "个人套餐 Key";
                case "team_plan_key": return "团队套餐 Key";
                case "personal_token_plan_key": return "个人 Token Plan Key";
                case "team_seat_token_plan_key": return "团队席位 Token Plan Key";
                case "kimi_code_key": return "Kimi Code Key";
                default: return "请选择当前凭据类型";
            }
        }

        private static string ProviderName(LlmPreset preset)
        {
            switch (preset?.Provider)
            {
                case "deepseek": return "DeepSeek";
                case "openai": return "OpenAI / GPT";
                case "anthropic": return "Anthropic / Claude";
                case "glm": return "智谱 / Z.AI / GLM";
                case "moonshot": case "kimi": return "Kimi / 月之暗面";
                case "minimax": return "MiniMax";
                case "xiaomi": case "mimo": return "MiMo / 小米";
                case "alibaba_bailian": return "阿里百炼 / Qwen";
                case "volcengine_ark": return "火山方舟 / 豆包";
                case "siliconflow": return "硅基流动";
                case "google_gemini": return "Google Gemini";
                case "xai": return "xAI / Grok";
                case "openrouter": return "OpenRouter";
                case "ollama": return "Ollama";
                case "lm_studio": case "lmstudio": return "LM Studio";
                case "custom": return "自定义";
                default: return preset?.Label ?? "选择厂商";
            }
        }
    }
}
