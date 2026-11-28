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
        private enum ModelPageView { Setup, Connections, ProviderDetails }
        private sealed class ModelFieldView
        {
            internal string Id, Value;
            internal F5Rect Rect;
            internal bool Secret, Dropdown;
        }
        private sealed class ModelReadResult
        {
            internal string Message;
            internal string Detail;
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
        private ModelPageView modelPageView;
        private string modelSearch = "", modelStatus, modelReadKind, modelStorage;
        private string modelRevealedId, modelRevealedValue;
        private bool modelConfirmDelete, modelAdvancedExpanded, modelTestDetailsExpanded;
        private string modelTestDetails;
        private int modelCapabilityFilter, modelOffset, modelMaximum, modelMainOffset;
        private float modelY, modelRow, modelX, modelWidth, modelTextWidth;
        private int modelTextSkin = -1;
        private F5Rect modelScrollView, modelFooterView;
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
                    LoadModelDraft(id.HasValue ? connections.Edit(id) : NewModelDraft());
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
            modelStatus = String.IsNullOrEmpty(probes) ? null : probes.Split(new[] { " · " }, StringSplitOptions.None)[0];
            modelTestDetails = String.IsNullOrEmpty(probes) ? null : ModelTestSubject(value, modelPreset) + "\n" + probes;
            modelTestDetailsExpanded = false;
            modelConfirmDelete = false;
            pendingModelPreset = null;
            Invalidate();
        }

        private LlmConnectionDraft NewModelDraft()
        {
            var draft = modelConnections.Edit();
            modelConnections.ApplyPreset(draft, "custom_openai_chat");
            return draft;
        }

        private void BuildModel(float row)
        {
            modelRow = row;
            modelWidth = Math.Min(720, bodyView.Width - 28);
            modelX = bodyView.X + (bodyView.Width - modelWidth) / 2;
            float footerHeight = modelConnections != null && modelDraft != null && modelPicker == ModelPicker.None && modelPageView == ModelPageView.Setup ? ModelFooterHeight(row) : 0;
            modelScrollView = new F5Rect(modelX - 5, bodyView.Y, modelWidth + 10, Math.Max(1, bodyView.Height - footerHeight));
            modelFooterView = footerHeight > 0 ? new F5Rect(modelX - 5, modelScrollView.Bottom + 3, modelWidth + 10, footerHeight - 3) : default(F5Rect);
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
            if (modelOffset > modelMaximum)
            {
                modelOffset = modelMaximum;
                controls.RemoveRange(firstControl, controls.Count - firstControl);
                labels.RemoveRange(firstLabel, labels.Count - firstLabel);
                modelFields.Clear();
                modelRevealLabels.Clear();
                BuildModelRows();
            }
            if (footerHeight > 0) BuildModelFooter();
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
            if (modelPageView != ModelPageView.Setup)
            {
                ModelButtonRow(("back-to-setup", "返回模型", () => OpenModelPage(ModelPageView.Setup), true));
                if (modelPageView == ModelPageView.Connections) BuildModelManagement();
                else BuildPresetNotes();
                return;
            }
            BuildModelSetup();
        }

        private void OpenModelPage(ModelPageView view)
        {
            CloseModelDropdown();
            modelDiscoveryPickerPending = false;
            modelPageView = view;
            modelPicker = ModelPicker.None;
            modelSearch = "";
            modelMainOffset = 0;
            modelCapabilityFilter = 0;
            modelOffset = 0;
            modelConfirmDelete = false;
            pendingModelPreset = null;
            HideModelSecrets();
            Invalidate();
        }

        private void BuildModelManagement()
        {
            bool saving = modelWrite != null;
            var stored = StoredModel();
            var actions = new List<(string, string, Action, bool)>();
            if (modelList.Count > 0) actions.Add(("connections", "切换连接", () => OpenModelPicker(ModelPicker.Connections), !saving));
            actions.Add(("create", "新增连接", () => { LoadModelDraft(NewModelDraft()); OpenModelPage(ModelPageView.Setup); }, !saving));
            if (stored != null)
            {
                actions.Add(("copy", "复制配置", () => { LoadModelDraft(modelConnections.Copy(stored.Id)); OpenModelPage(ModelPageView.Setup); }, !saving));
                actions.Add(("delete", "删除", () => { modelConfirmDelete = true; Invalidate(); }, !saving));
            }
            ModelButtonRow(actions.ToArray());
            if (modelConfirmDelete && stored != null)
            {
                int count = modelConnections.GetAffectedBindingCount(stored.Id);
                ModelParagraph("删除「" + stored.DisplayName + "」？明确绑定此连接的 " + count + " 名队友会变为未配置。" +
                    (modelConnections.DefaultId == stored.Id ? "继承此默认连接的队友也会变为未配置。" : ""));
                ModelButtonRow(("confirm-delete", "确认删除", () => StartModelWrite(() => modelConnections.DeleteAsync(stored.Id), "已删除连接。", () => LoadModelDraft(NewModelDraft())), !saving),
                    ("cancel-delete", "取消", () => { modelConfirmDelete = false; Invalidate(); }, true));
            }
            ModelField("name", "连接名称", modelDraft.DisplayName, value => modelDraft.DisplayName = value, 80);
            ModelButtonRow(("save-name", "保存连接", SaveModel, !saving && modelRead == null));
            ModelParagraph(modelStatus);
            if (!String.IsNullOrEmpty(modelTestDetails))
            {
                if (modelTestDetailsExpanded) ModelParagraph(modelTestDetails);
                ModelButtonRow(ModelResultDetails());
            }
            bool enabled = modelConnections.Enabled;
            if (stored == null)
                ModelButtonRow(("enabled", enabled ? "关闭直连" : "启用直连",
                    () => StartModelWrite(() => modelConnections.SetEnabledAsync(!enabled), enabled ? "已关闭直连。" : "已启用直连。"), !saving && enabled));
            else
            {
                bool isDefault = modelConnections.DefaultId == stored.Id;
                ModelButtonRow(("enabled", enabled ? "关闭直连" : "启用直连",
                        () => StartModelWrite(() => modelConnections.SetEnabledAsync(!enabled), enabled ? "已关闭直连。" : "已启用直连。"), !saving),
                    ("default", isDefault ? "取消默认" : "设为默认", () => StartModelWrite(() => modelConnections.SetDefaultAsync(isDefault ? (Guid?)null : stored.Id),
                        isDefault ? "已取消默认连接。" : "已设置默认连接。"), !saving));
            }
            BuildModelBinding(stored, saving);
            if (!String.IsNullOrEmpty(modelStorage)) ModelParagraph(modelStorage);
        }

        private void BuildModelSetup()
        {
            ModelHeading("模型连接");
            var stored = StoredModel();
            ModelParagraph((stored == null ? "新连接 · 尚未保存" : stored.DisplayName) +
                (stored != null && modelConnections.DefaultId == stored.Id ? " · 默认" : ""));
            ModelDropdownField("provider", "服务商", ProviderName(modelPreset), ModelPicker.Provider);
            if (modelPreset != null)
            {
                bool planChoice = modelConnections.Presets.Any(item => item.Provider == modelPreset.Provider && item.PlanId != modelPreset.PlanId);
                bool regionChoice = modelConnections.Presets.Any(item => SamePlan(item, modelPreset) && item.Region != modelPreset.Region);
                if (modelPreset.Provider != "custom")
                {
                    ModelDropdownField("plan", "计费方式", ModelPlanName(modelPreset), ModelPicker.Plan, planChoice);
                    ModelDropdownField("region", "地区", modelPreset.RegionLabel, ModelPicker.Region, regionChoice);
                }
                else ModelDropdownField("protocol", "接口协议", ProtocolName(modelDraft.Protocol), ModelPicker.Protocol);
            }
            if (modelPreset?.Policy == "reference_only")
                ModelDropdownField("reference-base", "Base URL", modelDraft.BaseUrl, ModelPicker.None, false);
            else ModelField("base-setup", "Base URL", modelDraft.BaseUrl, value => modelDraft.BaseUrl = value.Trim(), 1500);
            if (pendingModelPreset != null)
            {
                ModelParagraph("你修改过服务地址。应用其他预设会替换地址、模型和凭据。是否继续？");
                ModelButtonRow(("apply-preset", "应用预设", () => ApplyModelPreset(pendingModelPreset, true), true),
                    ("keep-preset", "保留当前配置", () => { pendingModelPreset = null; Invalidate(); }, true));
            }

            if (modelPreset?.Policy == "reference_only")
            {
                ModelParagraph("这项套餐仅供参考。可切换到按量 API 继续连接。");
                if (modelPreset.CredentialKinds?.Count > 0)
                    ModelParagraph("适用凭据 · " + String.Join("、", modelPreset.CredentialKinds.Select(ModelCredentialKindName)));
                BuildModelSecondary();
                if (modelAdvancedExpanded) BuildModelAdvanced();
                return;
            }
            if (modelPreset?.CredentialKinds?.Count > 1)
                ModelDropdownField("credential", "凭据类型", ModelCredentialKindName(modelDraft.CredentialKind), ModelPicker.Credential);
            BuildApiKey();
            BuildModelInput();
            var profile = CurrentProfile();
            if (profile != null) ModelParagraph(profile.Name + " · " + ModelBindingLabel(profile.OwnerId, profile.Id));
            else if (modelConnections.DefaultId.HasValue && modelConnections.DefaultId != Guid.Empty)
                ModelParagraph("默认连接 · " + (modelList.FirstOrDefault(item => item.Id == modelConnections.DefaultId)?.DisplayName ?? "未配置"));
            if (modelPreset?.Policy == "interactive_tools_conditional" || modelPreset?.Policy == "personal_interactive_conditional")
                ModelParagraph("套餐有使用限制，请核对服务商的适用范围和额度。");
            if (modelTestDetailsExpanded) ModelParagraph(modelTestDetails);
            BuildModelSecondary();
            if (modelAdvancedExpanded) BuildModelAdvanced();
        }

        private void BuildModelSecondary()
        {
            var options = new List<(string, string, Action, bool)>();
            options.Add(("manage", "管理连接", () => OpenModelPage(ModelPageView.Connections), modelWrite == null));
            if (modelPreset != null) options.Add(("details", "使用说明", () => OpenModelPage(ModelPageView.ProviderDetails), true));
            if (!String.IsNullOrEmpty(modelTestDetails)) options.Add(ModelResultDetails());
            ModelButtonRow(options.ToArray());
            ModelDisclosure("advanced", "高级设置", modelAdvancedExpanded, () => { modelAdvancedExpanded = !modelAdvancedExpanded; HideModelSecrets(); Invalidate(); });
        }

        private (string Key, string Label, Action Run, bool Enabled) ModelResultDetails() =>
            ("test-details", modelTestDetailsExpanded ? "收起详情" : "详细结果", () => { modelTestDetailsExpanded = !modelTestDetailsExpanded; Invalidate(); }, true);

        private void BuildModelFooter()
        {
            bool callable = modelPreset?.Policy != "reference_only";
            bool idle = modelWrite == null && modelRead == null;
            string status = modelStatus ?? (callable ? "尚未测试当前配置 · 测试可能产生费用" : "该方案仅供参考");
            Text(status, new F5Rect(modelX, modelFooterView.Y + 3, modelWidth, modelRow - 8), .7f);
            if (!callable) return;
            modelY = modelFooterView.Y + modelRow;
            ModelActions(new (string, string, Action, bool)[]
            {
                modelRead == null ? ("test-chat", "测试连接", (Action)(() => StartModelTest(false)), idle) :
                    ("cancel-read", "取消请求", () => { CancelModelRequest(); ModelMessage("已取消请求。"); }, true),
                ("save-use", "保存为默认", SaveModelAndUse, idle)
            }, modelFooterView, alignLastRight: true);
        }

        private float ModelFooterHeight(float row)
        {
            float buttons = ConfigurationWidth("测试连接") + ConfigurationWidth("保存为默认") + 8;
            return row * (buttons > modelWidth ? 3 : 2) + 10;
        }

        private string ModelBindingLabel(Guid owner, Guid profile)
        {
            Guid? binding = modelConnections.GetBinding(owner, profile);
            if (!binding.HasValue)
            {
                string defaultName = modelList.FirstOrDefault(row => row.Id == modelConnections.DefaultId)?.DisplayName;
                return defaultName == null ? "尚未设置默认连接" : "默认连接「" + defaultName + "」";
            }
            if (binding.Value == Guid.Empty) return "已停用模型连接";
            string label = modelList.FirstOrDefault(row => row.Id == binding.Value)?.DisplayName;
            return label == null ? "原连接已删除" : "使用「" + label + "」";
        }

        private void BuildModelBinding(LlmConnectionInfo stored, bool saving)
        {
            var profile = CurrentProfile();
            if (profile == null) return;
            Guid? binding = modelConnections.GetBinding(profile.OwnerId, profile.Id);
            ModelParagraph(profile.Name + " · " + ModelBindingLabel(profile.OwnerId, profile.Id));
            ModelButtonRow(("bind", "绑定当前连接", () => StartModelWrite(() => modelConnections.SetBindingAsync(profile.OwnerId, profile.Id, stored.Id), "已为该队友设置连接。"), stored != null && !saving && binding != stored.Id),
                ("inherit", "继承默认", () => StartModelWrite(() => modelConnections.SetBindingAsync(profile.OwnerId, profile.Id, null), "该队友现在继承默认连接。"), binding.HasValue && !saving),
                ("unbind", "停止使用连接", () => StartModelWrite(() => modelConnections.SetBindingAsync(profile.OwnerId, profile.Id, Guid.Empty), "该队友暂时不使用模型连接。"), binding != Guid.Empty && !saving));
        }

        private void SaveModel() => SaveModel(false);

        private void SaveModelAndUse() => SaveModel(true);

        private void SaveModel(bool use)
        {
            var draft = modelDraft;
            if (use && StoredModel() == null && draft.DisplayName == "新连接" && !String.IsNullOrWhiteSpace(draft.Model))
            {
                string name = ProviderName(modelPreset) + " · " + draft.Model;
                var boundaries = TextElements.Boundaries(name);
                int length = boundaries.Last(offset => offset <= 80);
                draft.DisplayName = name.Substring(0, length);
            }
            string message = use ? "队友默认使用「" + draft.DisplayName + "」。" : "连接已保存。";
            StartModelWrite(() => use ? modelConnections.SaveAndUseAsync(draft) : modelConnections.SaveAsync(draft), message, () =>
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
            bool optional = modelDraft.AuthMode == "none" || modelDraft.AuthMode == "optional_bearer";
            string status = modelDraft.ApiKey == null ? (modelDraft.HasStoredKey ? "已保存 · 点击替换" : optional ? "可留空" : "点击粘贴 API Key") :
                (modelDraft.ApiKey.Length == 0 ? "保存后清除" : "已填写");
            ModelSecretField("api-key", "API Key", status, modelDraft.ApiKey, value => modelDraft.ApiKey = value, 4096,
                () => modelConnections.RevealApiKey(modelDraft), compact: true);
        }

        private void BuildModelInput()
        {
            bool canRead = modelPreset?.DiscoveryStatus == "provider_specific" || modelPreset?.DiscoveryStatus == "documented" ||
                modelPreset?.Provider == "custom" && !String.IsNullOrWhiteSpace(modelDraft.DiscoveryPath);
            bool hasList = discoveredModels?.Count > 0;
            bool hasCandidates = discoveredModels == null && modelPreset?.Candidates?.Count > 0;
            float y = modelY, right = modelX + modelWidth;
            string pickLabel = hasList ? "选择" : "候选";
            float pickWidth = ConfigurationWidth(pickLabel), readWidth = ConfigurationWidth("获取");
            float reserve = (canRead ? readWidth + 6 : 0) + (hasList || hasCandidates ? pickWidth + 6 : 0);
            ModelField("model-id", "模型", modelDraft.Model, value => SetModelId(value.Trim()), 200,
                "填写模型 ID", invalidatesProbe: false, trailing: reserve);
            if (hasList || hasCandidates)
            {
                ModelRowAction("view-models", pickLabel, right - pickWidth, y, pickWidth, () => OpenModelPicker(ModelPicker.Models), true);
                right -= pickWidth + 6;
            }
            if (canRead) ModelRowAction("discover", "获取", right - readWidth, y, readWidth, StartModelDiscovery, modelRead == null && modelWrite == null);
        }

        private static string ModelTestSubject(LlmConnectionDraft draft, LlmPreset preset) =>
            String.Join(" · ", new[] { draft.DisplayName, preset?.PlanLabel, preset?.RegionLabel, ModelAddressSummary(draft) + " / " + draft.Model }
                .Where(text => !String.IsNullOrWhiteSpace(text)));

        private static string ModelAddressSummary(LlmConnectionDraft draft)
        {
            string raw = draft.BaseUrl ?? "";
            if (raw.Contains("{workspaceId}") && !String.IsNullOrEmpty(draft.Workspace)) raw = raw.Replace("{workspaceId}", draft.Workspace);
            return Uri.TryCreate(raw, UriKind.Absolute, out Uri address) && address.UserInfo.Length == 0 ? address.Authority : "请在高级设置填写服务地址";
        }

        private void BuildPresetNotes()
        {
            if (modelPreset == null) return;
            ModelHeading(ProviderName(modelPreset) + " · " + ModelPlanName(modelPreset));
            ModelParagraph(PlanScope(modelPreset.Policy));
            ModelParagraph(modelPreset.Note);
            ModelParagraph("公开接入说明核对于 " + modelPreset.VerifiedAt + "。尚无本连接的实测结果时，以测试和控制台为准。");
            if (modelPreset.Sources?.Count > 0)
                ModelButtonRow(("official", "复制官方链接", () => ModelMessage(aiInput.TryCopy(String.Join(Environment.NewLine, modelPreset.Sources)) ? "已复制官方链接。" : "无法复制官方链接。"), true));
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
        private static string ModelPlanName(LlmPreset preset)
        {
            switch (preset.PlanId)
            {
                case "standard_api": return "按量 API";
                case "subscription": return "M Plan";
                case "coding_plan": return "Coding Plan";
                case "token_plan": return "Token Plan";
                case "agent_plan": return "Agent Plan";
                case "kimi_code": return "Kimi Code";
                default: return preset.PlanLabel;
            }
        }
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
