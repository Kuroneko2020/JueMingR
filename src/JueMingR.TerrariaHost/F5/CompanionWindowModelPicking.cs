using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Platform.Llm;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class CompanionWindow
    {
        private void OpenModelPicker(ModelPicker picker)
        {
            if (modelPicker == ModelPicker.None) modelMainOffset = modelOffset;
            modelPicker = picker;
            modelSearch = "";
            modelOffset = 0;
            modelCapabilityFilter = 0;
            HideModelSecrets();
            Invalidate();
        }

        private void ReturnToModel()
        {
            modelPicker = ModelPicker.None;
            modelOffset = modelMainOffset;
            modelSearch = "";
            Invalidate();
        }

        private void BuildModelPicker()
        {
            string title;
            switch (modelPicker)
            {
                case ModelPicker.Connections: title = "选择连接档案"; break;
                case ModelPicker.Provider: title = "选择服务商"; break;
                case ModelPicker.Plan: title = "选择计费方式"; break;
                case ModelPicker.Region: title = "选择地区"; break;
                case ModelPicker.Credential: title = "选择凭据类型"; break;
                case ModelPicker.Protocol: title = "选择该方案支持的协议"; break;
                case ModelPicker.Templates: title = "选择服务入口"; break;
                default: title = discoveredModels == null ? "官方模型候选" : "此连接返回的模型"; break;
            }
            ModelButtonRow(("return", "返回连接配置", ReturnToModel, true));
            ModelHeading(title);
            if (modelPicker == ModelPicker.Provider || modelPicker == ModelPicker.Models || modelPicker == ModelPicker.Connections)
            {
                ModelField("search", modelPicker == ModelPicker.Models ? "过滤模型" : "搜索名称", modelSearch, value =>
                {
                    modelSearch = value.Trim();
                    modelOffset = 0;
                }, 120, invalidatesProbe: false);
            }
            if (modelPicker == ModelPicker.Models) { BuildModelChoices(); return; }
            if (modelPicker == ModelPicker.Connections)
            {
                foreach (var entry in modelList)
                {
                    if (!ModelMatch(entry.DisplayName) && !ModelMatch(entry.Model)) continue;
                    string label = entry.DisplayName + (entry.Id == modelConnections.DefaultId ? " · 默认" : "");
                    ModelChoice("saved-" + entry.Id, label, () => { LoadModelDraft(modelConnections.Edit(entry.Id)); ReturnToModel(); }, entry.Id == modelDraft.Id);
                }
                if (modelList.Count == 0) ModelParagraph("还没有保存的连接。返回后可新增连接。");
                return;
            }

            var presets = modelConnections.Presets;
            switch (modelPicker)
            {
                case ModelPicker.Provider:
                    foreach (var group in presets.Where(item => !item.Advanced).GroupBy(item => item.Provider))
                    {
                        var selectedPreset = PreferredPreset(group);
                        string label = ProviderName(selectedPreset);
                        if (!ModelMatch(label) && !group.Any(item => ModelMatch(item.Label) || item.Aliases.Any(ModelMatch))) continue;
                        ModelChoice("provider-" + group.Key, label, () => ChooseProvider(selectedPreset), modelPreset?.Provider == group.Key);
                    }
                    break;
                case ModelPicker.Plan:
                    foreach (var group in presets.Where(item => item.Provider == modelPreset?.Provider && !item.Advanced).GroupBy(item => item.PlanId))
                    {
                        var selectedPreset = PreferredPreset(group);
                        ModelChoice("plan-" + group.Key, ModelPlanName(selectedPreset) + PolicyLabel(selectedPreset.Policy),
                            () => ChoosePlan(selectedPreset), modelPreset?.PlanId == group.Key);
                    }
                    break;
                case ModelPicker.Region:
                    foreach (var group in presets.Where(item => modelPreset != null && SamePlan(item, modelPreset) && !item.Advanced).GroupBy(item => item.Region))
                    {
                        var selectedPreset = PreferredPreset(group);
                        ModelChoice("region-" + group.Key, selectedPreset.RegionLabel, () => ApplyModelPreset(selectedPreset), modelPreset.Region == group.Key);
                    }
                    break;
                case ModelPicker.Credential:
                    foreach (string kind in modelPreset.CredentialKinds)
                        ModelChoice("credential-" + kind, ModelCredentialKindName(kind), () =>
                        {
                            if (kind != modelDraft.CredentialKind)
                            {
                                string scope = modelDraft.CredentialScope();
                                modelDraft.CredentialKind = kind;
                                ModelDraftChanged(scope);
                            }
                            ReturnToModel();
                        }, modelDraft.CredentialKind == kind);
                    break;
                case ModelPicker.Protocol:
                    foreach (var group in presets.Where(item => modelPreset != null && SameRegion(item, modelPreset) && item.Advanced == modelPreset.Advanced).GroupBy(item => item.Protocol))
                    {
                        var selectedPreset = PreferredPreset(group);
                        ModelChoice("protocol-" + group.Key, ProtocolName(group.Key), () => ApplyModelPreset(selectedPreset), modelPreset.Protocol == group.Key);
                    }
                    break;
                case ModelPicker.Templates:
                    foreach (var entry in presets.Where(item => modelPreset != null && SameRegion(item, modelPreset) && item.Protocol == modelPreset.Protocol))
                        ModelChoice("template-" + entry.Id, entry.Label, () => ApplyModelPreset(entry), modelPreset.Id == entry.Id);
                    break;
            }
        }

        private bool ModelMatch(string value) => modelSearch.Length == 0 || value?.IndexOf(modelSearch, StringComparison.OrdinalIgnoreCase) >= 0;

        private static LlmPreset PreferredPreset(IEnumerable<LlmPreset> values) => values
            .OrderBy(item => item.Policy == "reference_only" ? 2 : item.Policy == "general_api" ? 0 : 1)
            .ThenByDescending(item => item.DefaultForProvider).First();

        private static string PolicyLabel(string policy) => policy == "reference_only" ? " · 参考" :
            policy == "interactive_tools_conditional" || policy == "personal_interactive_conditional" ? " · 有条件" : "";

        private void ChooseProvider(LlmPreset preset)
        {
            if (modelPreset?.Provider != preset.Provider && !ApplyModelPreset(preset)) return;
            if (modelConnections.Presets.Any(row => row.Provider == preset.Provider && row.PlanId != preset.PlanId && !row.Advanced))
                OpenModelPicker(ModelPicker.Plan);
            else ChoosePlan(preset);
        }

        private void ChoosePlan(LlmPreset preset)
        {
            if (modelPreset == null || !SamePlan(modelPreset, preset)) if (!ApplyModelPreset(preset)) return;
            if (modelConnections.Presets.Any(row => SamePlan(row, preset) && row.Region != preset.Region && !row.Advanced)) OpenModelPicker(ModelPicker.Region);
            else ReturnToModel();
        }

        private bool ApplyModelPreset(LlmPreset preset, bool confirmAddress = false)
        {
            ReturnToModel();
            if (preset.Id == modelPreset?.Id) return true;
            if (!confirmAddress && modelPreset != null && modelDraft.BaseUrl != (modelPreset.BaseUrl ?? ""))
            {
                pendingModelPreset = preset;
                Invalidate();
                return false;
            }
            pendingModelPreset = null;
            var own = modelDraft;
            // A provider, billing identity or region change starts a separate
            // connection draft, even where the old and new API hosts match.
            if (modelPreset != null && !SameRegion(modelPreset, preset) && StoredModel() != null)
            {
                own = modelConnections.Edit();
            }
            modelConnections.ApplyPreset(own, preset.Id);
            LoadModelDraft(own);
            return true;
        }

        private void BuildModelChoices()
        {
            if (discoveredModels == null)
            {
                ModelParagraph("官方候选 · 核对日期 " + modelPreset?.VerifiedAt + "。请按你的账号权益选择。");
                if (modelPreset?.Candidates != null)
                    foreach (string id in modelPreset.Candidates)
                        if (ModelMatch(id)) ModelChoice("candidate-" + id, id, () => PickModel(id), modelDraft.Model == id);
                return;
            }
            ModelButtonRow(("filter-all", "全部模型", () => SetModelFilter(0), true),
                ("filter-text", "已知支持文本", () => SetModelFilter(1), true),
                ("filter-tools", "已知支持工具", () => SetModelFilter(2), true));
            ModelParagraph(modelCapabilityFilter == 1 ? "筛选中 · 已知支持文本" : modelCapabilityFilter == 2 ? "筛选中 · 已知支持工具" : "筛选中 · 全部模型");
            int matches = 0;
            foreach (var entry in discoveredModels)
            {
                if (!ModelMatch(entry.Id) || !MatchesModelCapability(entry.Capability)) continue;
                ModelChoice("found-" + entry.Id, entry.Id + " · " + ModelCapability(entry.Capability), () => PickModel(entry.Id), modelDraft.Model == entry.Id);
                matches++;
            }
            if (matches == 0) ModelParagraph("此筛选下没有模型。可以更换筛选条件，或返回手动填写模型 ID。");
        }

        private void SetModelFilter(int filter)
        {
            modelCapabilityFilter = filter;
            Invalidate();
        }

        private static bool HasModelCapability(string capability, string value) => capability?.IndexOf(value, StringComparison.Ordinal) >= 0;
        private bool MatchesModelCapability(string capability) => modelCapabilityFilter == 0 ||
            HasModelCapability(capability, modelCapabilityFilter == 1 ? "文字" : "工具");

        private static string ModelCapability(string capability)
        {
            return String.IsNullOrEmpty(capability) ? "能力待确认" : capability;
        }

        private void PickModel(string id)
        {
            if (modelDraft.Model != id) { modelDraft.Model = id; RetireModelRead(); modelStatus = null; }
            ReturnToModel();
        }
    }
}
