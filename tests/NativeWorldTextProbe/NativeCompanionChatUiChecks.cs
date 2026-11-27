using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Llm;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionChatUiChecks
    {
        internal static void Draw(object context, ProbeGraphics graphics, string output)
        {
            var host = Get(context, "Companions");
            var window = Get(Get(context, "Shell"), "CompanionUi");
            var layout = Get(Get(Get(context, "Shell"), "State"), "Layout");
            var screen = new Vector2(700, 500);
            var transform = Matrix.Identity;
            void Sample(Vector2 point, bool left, int wheel = 0)
            {
                NativeToolsUiChecks.Mouse(point, left);
                Call(window, "Process", true, new KeyboardState(), point, transform, screen, true, wheel);
            }
            object Control(string key) => ((IEnumerable)Get(window, "controls")).Cast<object>().SingleOrDefault(c =>
                ((string)Get(c, "Key")).StartsWith(key + "/", StringComparison.Ordinal));
            void Open(string key)
            {
                var point = NativeToolsUiChecks.Point(Get(Control(key) ?? throw new InvalidOperationException("Draw control absent: " + key), "Rect"));
                Sample(point, false); Sample(point, true); Sample(point, false);
            }
            void Image(string name) => graphics.Image(Path.Combine(output, "companion-" + name + ".png"), () => Call(window, "Draw"),
                transform, (int)screen.X, (int)screen.Y);
            void Resize(int width, int height, float scale)
            {
                screen = new Vector2(width, height);
                transform = Matrix.CreateScale(scale);
                Call(window, "Prepare", transform, screen);
            }
            void ScrollTo(string key)
            {
                var point = NativeToolsUiChecks.Point(Get(window, "modelScrollView"));
                for (int i = 0; i < 32 && Control(key) == null; i++) Sample(point, false, -120);
            }
            void AssertCompact(string key, string parent, float max)
            {
                var rect = Get(Control(key), "Rect");
                Require((float)Get(rect, "Width") < max && (float)Get(rect, "Width") < (float)Get(Get(window, parent), "Width") / 2,
                    key + " follows its actual text width even on wide screens");
            }
            void SeedChat()
            {
                var view = Get(window, "chatView");
                var now = new DateTimeOffset(2026, 11, 27, 11, 12, 13, TimeSpan.Zero);
                var history = new[]
                {
                    new LlmHistoryEntry(Guid.NewGuid(), "user", "告诉我现在适合在这里钓鱼吗？", now),
                    new LlmHistoryEntry(Guid.NewGuid(), "query", "当前时间：下午 2:13。你在森林，附近有一处水池。", now),
                    new LlmHistoryEntry(Guid.NewGuid(), "assistant", "可以在这处水池钓鱼。**先确认浮标能落到水里。**\n\n- 在岸边站稳，再选用鱼竿。\n- 我可以等你挑好位置再开始。", now),
                    new LlmHistoryEntry(Guid.NewGuid(), "action", "已停止钓鱼，队友会留在原地。", now)
                };
                Set(view, "Entries", history);
                Set(window, "chatOffset", 0f);
                Set(window, "followChat", false);
                Call(window, "PrepareChatPresentation");
            }
            try
            {
                NativeToolsUiChecks.Mouse(Vector2.Zero, false);
                Call(window, "Toggle", Get(layout, "Viewport"));
                Call(window, "Prepare", Matrix.Identity, screen);
                Open("model");
                var firstScreen = ((IEnumerable)Get(window, "controls")).Cast<object>().Select(c => (string)Get(c, "Key")).ToArray();
                Require(new[] { "model-provider/", "model-field-base-setup/", "model-field-api-key/", "model-field-model-id/", "model-test-chat/", "model-save-use/" }
                    .All(key => firstScreen.Any(candidate => candidate.StartsWith(key, StringComparison.Ordinal))),
                    "the real 700 by 500 model page draws all first-use controls without scrolling");
                Image("model-narrow");
                Open("model-provider"); Image("model-dropdown-narrow");
                Sample(Vector2.Zero, true); Sample(Vector2.Zero, false);
                Resize(1280, 800, 1f);
                var draft = (LlmConnectionDraft)Get(window, "modelDraft");
                draft.BaseUrl = "https://example.invalid/very/long/custom/provider/service/endpoint/for/the/native/scale/and/text/probe/v1";
                draft.Model = "provider-name/extended-model-identifier-2026-with-a-long-release-suffix";
                Call(window, "Invalidate"); Resize(1280, 800, 1f);
                AssertCompact("model-test-chat", "modelFooterView", 135);
                AssertCompact("model-save-use", "modelFooterView", 145);
                Image("model-medium");
                ScrollTo("model-advanced"); Open("model-advanced");
                Call(window, "ModelFailure", "测试连接未完成", "服务商暂时无法接受请求。请检查地址和模型名称，确认当前账号可以使用该模型，再次尝试。原有连接配置已保留。");
                Resize(1920, 1080, 1.15f);
                Image("model-wide-advanced-error");

                var archive = Get(host, "profiles");
                NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); return (bool)Get(archive, "Ready"); });
                Require((bool)Call(host, "Create", "聊天绘制队友"), "real companion creates the visual chat target");
                var id = (Guid)Get(host, "LastCreatedId");
                NativeQuickItemChecks.Until(() => { Call(host, "Update", 0UL); return (bool)Call(archive, "IsSaved", id); });
                Resize(1920, 1080, 1.15f);
                Open("profile:" + id); Open("chat");
                NativeQuickItemChecks.Until(() =>
                {
                    Call(host, "Update", 0UL); Resize(1920, 1080, 1.15f);
                    return !((string)Get(Get(window, "chatView"), "Status") ?? "").Contains("正在读取");
                });
                SeedChat();
                AssertCompact("chat-copy", "chatHistoryView", 100);
                Image("chat-wide");
                Resize(1280, 800, 1f); SeedChat(); Image("chat-medium");
                Resize(700, 500, 1f); SeedChat(); Image("chat-narrow");
                Console.WriteLine("PASS original XNA model and chat at 700x500, 1280x800 and 1920x1080 (115%); dropdown, long fields, advanced errors and conversation cards.");
            }
            finally { Call(window, "Close"); NativeToolsUiChecks.Mouse(Vector2.Zero, false); }
        }

        private sealed class TextCandidate : RealProxy
        {
            internal TextCandidate(Type type) : base(type) { }
            internal string Composition = "";
            internal bool Candidate;
            public override IMessage Invoke(IMessage message)
            {
                var call = (IMethodCallMessage)message;
                object result = call.MethodName == "get_Composition" ? (object)Composition : call.MethodName == "get_Candidates" ? Candidate : (object)null;
                return new ReturnMessage(result, null, 0, call.LogicalCallContext, call);
            }
        }

        internal static void Run(object context)
        {
            Terraria.GameContent.Creative.CreativePowerManager.Initialize();
            var host = Get(context, "Companions");
            var archive = Get(host, "profiles");
            var shell = Get(context, "Shell");
            var window = Get(shell, "CompanionUi");
            var state = Get(shell, "State");
            var layout = Get(state, "Layout");
            var renderer = Get(shell, "renderer");
            var physicalKeys = (HotkeyInput)Get(Get(window, "input"), "Hotkeys");
            var physical = new bool[HotkeyChord.KeyCount];
            var input = Get(Get(window, "aiInput"), "input");
            var imeField = input.GetType().GetField("ime", BindingFlags.Instance | BindingFlags.NonPublic);
            object originalIme = imeField.GetValue(input);
            var ime = new TextCandidate(imeField.FieldType);
            var screen = new Vector2(700, 500);
            Call(renderer, "RefreshResources");
            Call(renderer, "Prepare", state, screen.X, screen.Y, 1f);
            NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); return (bool)Get(archive, "Ready"); });

            object Control(string key) => ((IEnumerable)Get(window, "controls")).Cast<object>().SingleOrDefault(c =>
                ((string)Get(c, "Key")).StartsWith(key + "/", StringComparison.Ordinal));
            Vector2 At(string key) => NativeToolsUiChecks.Point(Get(Control(key) ?? throw new InvalidOperationException("Control absent: " + key), "Rect"));
            void Prepare() => Call(window, "PrepareLayout", Matrix.Identity, screen);
            void Sample(Vector2 point, bool left = false, Keys[] keys = null, string queue = "", int wheel = 0)
            {
                NativeToolsUiChecks.Mouse(point, left);
                Array.Clear(physical, 0, physical.Length);
                physical[256] = left;
                if (keys != null) foreach (Keys key in keys) physical[(int)key] = true;
                physicalKeys.Update(physical, true);
                Call(window, "BeforeInput", true);
                Main.keyCount = queue.Length;
                for (int i = 0; i < queue.Length; i++) { Main.keyInt[i] = queue[i]; Main.keyString[i] = queue[i].ToString(); }
                Call(window, "ProcessLayoutInput", true, new KeyboardState(keys ?? Array.Empty<Keys>()), point, Matrix.Identity, screen, true, wheel);
            }
            void Click(string key)
            {
                var point = At(key);
                Sample(point); Sample(point, true); Sample(point);
            }
            object[] Choices() => ((IEnumerable)Get(window, "modelDropdownChoices")).Cast<object>().ToArray();
            string[] ChoiceIds() => Choices().Select(choice => (string)Get(choice, "Id")).ToArray();
            Vector2 SearchPoint() => NativeToolsUiChecks.Point(Get(Get(window, "modelDropdownSearch"), "Rect"));
            Vector2 RowPoint(int index)
            {
                var popup = Get(window, "modelDropdownPanel");
                return new Vector2((float)Get(popup, "X") + 24, (float)Get(window, "ModelDropdownListTop") +
                    (index - (int)Get(window, "modelDropdownOffset") + .5f) * (float)Get(window, "modelDropdownRow"));
            }
            void PickOpen(string id)
            {
                int index = Array.IndexOf(ChoiceIds(), id);
                Require(index >= 0, "anchored dropdown choice is reachable: " + id);
                for (int i = 0; i < 25 && index >= (int)Get(window, "modelDropdownOffset") + (int)Get(window, "modelDropdownVisibleRows"); i++)
                    Sample(RowPoint((int)Get(window, "modelDropdownOffset")), wheel: -120);
                var point = RowPoint(index);
                Sample(point); Sample(point, true); Sample(point);
                Require(!(bool)Get(window, "ModelDropdownOpen"), "choosing one row closes its anchored dropdown");
            }
            void Pick(string key, string id) { Click(key); PickOpen(id); }
            Guid Create(string name)
            {
                Require((bool)Call(host, "Create", name), "real companion profile creation for chat UI");
                var id = (Guid)Get(host, "LastCreatedId");
                NativeQuickItemChecks.Until(() => { Call(host, "Update", 0UL); return (bool)Call(archive, "IsSaved", id); });
                return id;
            }
            string Draft() => (string)Get(Get(Get(window, "currentChatDraft"), "Text"), "Text");

            try
            {
                imeField.SetValue(input, ime.GetTransparentProxy());
                NativeToolsUiChecks.Mouse(Vector2.Zero, false);
                Call(window, "Toggle", Get(layout, "Viewport"));
                Prepare();
                Require(((IEnumerable)Get(host, "Profiles")).Cast<object>().Count() == 0, "the model page starts without any owned companion");
                Require(new[] { "inventory", "configuration", "chat", "model" }.All(key => Control(key) != null), "all four top-level controls are real hit targets");
                Click("model");
                Require(GetOptional(window, "modelDraft") != null &&
                    new[] { "model-provider", "model-field-base-setup", "model-field-api-key", "model-field-model-id", "model-test-chat", "model-save-use" }
                        .All(key => Control(key) != null && (bool)Get(Control(key), "Enabled")) &&
                    new[] { "model-create", "model-copy", "model-delete", "model-field-name", "model-enabled", "model-default", "model-plan", "model-region" }
                        .All(key => Control(key) == null) && !(bool)Get(Control("model-save-use"), "Selected"),
                    "the zero-profile narrow page immediately exposes a complete custom connection form");
                var connections = Get(host, "ChatConnections");
                CheckModelResultSubject(window, connections);
                Click("model-provider");
                Require(ChoiceIds()[0] == "provider-custom", "the first real provider row is Custom");
                int oldScroll = Convert.ToInt32(Get(window, "modelOffset"));
                var firstPopupRow = RowPoint(0);
                Sample(firstPopupRow, true);
                Sample(firstPopupRow, true, wheel: -120);
                Sample(firstPopupRow);
                Require((bool)Get(window, "ModelDropdownOpen") && (int)Get(window, "modelDropdownOffset") > 0 &&
                    Convert.ToInt32(Get(window, "modelOffset")) == oldScroll, "scrolling changes popup choices, cancels old presses, and leaves the form fixed");
                Click("chat");
                Require((int)Get(window, "page") == 3 && !(bool)Get(window, "ModelDropdownOpen"),
                    "clicking outside consumes the opening press and release before the tab below receives input");
                Click("model-provider");
                Sample(Vector2.Zero, keys: new[] { Keys.Escape });
                Sample(Vector2.Zero, keys: new[] { Keys.Escape });
                Require((bool)Get(window, "Visible") && !(bool)Get(window, "ModelDropdownOpen") && physicalKeys.IsSuppressed((int)Keys.Escape),
                    "Escape closes only the dropdown and owns the held native cancellation tail");
                Sample(Vector2.Zero);
                Click("model-provider");
                var armed = RowPoint(1);
                Sample(armed, true);
                Call(window, "Invalidate"); Prepare();
                Sample(armed);
                Require((bool)Get(window, "ModelDropdownOpen") && ((LlmConnectionDraft)Get(window, "modelDraft")).PresetId == "custom_openai_chat",
                    "a layout rebuild between press and release cannot pick the old row");
                Sample(Vector2.Zero, true); Sample(Vector2.Zero);
                Click("chat");
                Click("chat-open-model");
                Require(Control("model-field-model-id") != null && !(bool)Get(window, "ModelDropdownOpen"),
                    "go to model settings from chat resets an earlier selection subpage to the first-use form");
                Click("model-provider");
                var search = SearchPoint();
                Sample(search, true); Sample(search);
                Sample(search, queue: "Claude");
                Sample(search, keys: new[] { Keys.Enter }, queue: "\r");
                Sample(search);
                Require(ChoiceIds().Contains("provider-anthropic") && !ChoiceIds().Contains("provider-deepseek"),
                    "provider filtering accepts an official alias and excludes unrelated profiles");
                int builds = (int)Get(window, "LayoutBuilds");
                var choices = Get(window, "modelDropdownChoices");
                for (int frame = 0; frame < 120; frame++) Prepare();
                Require((int)Get(window, "LayoutBuilds") == builds && ReferenceEquals(choices, Get(window, "modelDropdownChoices")),
                    "stable model path reuses filtered options and layout for 120 prepares");
                Sample(Vector2.Zero, true); Sample(Vector2.Zero);
                var modelPoint = NativeToolsUiChecks.Point(Get(window, "modelScrollView"));
                void ScrollTo(string key)
                {
                    for (int i = 0; i < 32 && Control(key) == null; i++) Sample(modelPoint, wheel: -120);
                    Require(Control(key) != null, "scroll reveals " + key);
                }
                void ToTop() { Set(window, "modelOffset", 0); Call(window, "Invalidate"); Prepare(); }
                void SetPreset(LlmPreset preset)
                {
                    ToTop();
                    Pick("model-provider", "provider-" + preset.Provider);
                    var active = (LlmPreset)Get(window, "modelPreset");
                    if (active.PlanId != preset.PlanId) Pick("model-plan", "plan-" + preset.PlanId);
                    active = (LlmPreset)Get(window, "modelPreset");
                    if (active.Region != preset.Region) Pick("model-region", "region-" + preset.Region);
                    active = (LlmPreset)Get(window, "modelPreset");
                    if (active.Id != preset.Id && preset.Provider != "custom" && !(bool)Get(window, "modelAdvancedExpanded"))
                    { ScrollTo("model-advanced"); Click("model-advanced"); }
                    if (active.Protocol != preset.Protocol)
                    { ScrollTo("model-protocol"); Pick("model-protocol", "protocol-" + preset.Protocol); }
                    active = (LlmPreset)Get(window, "modelPreset");
                    if (active.Id != preset.Id)
                    { ScrollTo("model-template"); Pick("model-template", "template-" + preset.Id); }
                    var draft = (LlmConnectionDraft)Get(window, "modelDraft");
                    Require(draft.PresetId == preset.Id && draft.Protocol == preset.Protocol && draft.BaseUrl == (preset.BaseUrl ?? "") &&
                        draft.InferencePath == (preset.InferencePath ?? ""), "UI applies the full request route: " + preset.Id + " (actual " + draft.PresetId + ")");
                }
                var presets = ((IEnumerable)Get(connections, "Presets")).Cast<LlmPreset>().ToDictionary(preset => preset.Id);
                string[] previouslyHidden =
                {
                    "custom_openai_chat", "custom_openai_responses", "custom_anthropic_messages",
                    "minimax_china_chat", "minimax_international_chat", "minimax_china_subscription_chat", "minimax_international_subscription_chat",
                    "openai_official_chat", "xai_official_chat", "openrouter_official_responses", "mimo_account_region_chat",
                    "bailian_cn_beijing_token_responses", "bailian_ap_southeast_1_token_responses",
                    "ark_beijing_coding_plan_responses", "ark_beijing_agent_plan_responses", "bailian_workspace_beijing_chat"
                };
                foreach (string id in previouslyHidden) SetPreset(presets[id]);
                SetPreset(presets.Values.First(item => item.Policy == "reference_only"));
                ToTop();
                Require(((IEnumerable)Get(window, "labels")).Cast<object>().Any(label => (string)Get(label, "Text") == "Base URL") &&
                    new[] { "model-test-chat", "model-test-tools", "model-save-use", "model-discover" }.All(key => Control(key) == null),
                    "the reference plan shows its address without any network or save action");
                SetPreset(presets["custom_openai_chat"]);
                ToTop();
                Click("model-field-base-setup");
                const string customBase = "https://example.invalid/custom/v1";
                var baseField = At("model-field-base-setup");
                Sample(baseField, queue: customBase);
                Sample(baseField, keys: new[] { Keys.Enter }, queue: "\r");
                Sample(baseField);
                ScrollTo("model-advanced");
                if ((bool)Get(window, "modelAdvancedExpanded")) Click("model-advanced");
                ToTop(); ScrollTo("model-advanced");
                Click("model-advanced");
                Require(Control("model-test-tools") != null && (bool)Get(window, "modelAdvancedExpanded"), "advanced fields expand in place while the fixed actions remain available");
                Require(((LlmConnectionDraft)Get(window, "modelDraft")).BaseUrl == customBase, "opening or closing disclosure preserves the entered custom address");
                Sample(modelPoint, wheel: -120);
                int scroll = Convert.ToInt32(Get(window, "modelOffset"));
                Require(scroll > 0, "model rows have a real independent scroll range");
                Click("chat"); Click("model");
                Require(Convert.ToInt32(Get(window, "modelOffset")) == scroll, "view changes preserve model scroll");
                ToTop();
                ScrollTo("model-field-api-key");
                Click("model-field-api-key");
                var secretPoint = At("model-field-api-key");
                const string testSecret = "fixture-only-test-key";
                Sample(secretPoint, queue: testSecret);
                var aiInput = Get(window, "aiInput");
                Require((bool)Get(aiInput, "Secret") && !((string)Get(Get(window, "modelEditView"), "Text")).Contains(testSecret),
                    "the key field has a separate masked presentation");
                Sample(secretPoint, keys: new[] { Keys.Enter }, queue: "\r");
                ScrollTo("model-api-key-reveal");
                Click("model-api-key-reveal");
                Require((string)Get(window, "modelRevealedValue") == testSecret, "explicit reveal reads the current draft key");
                Click("chat");
                Require(GetOptional(window, "modelRevealedValue") == null, "page switch retires the revealed secret projection");
                Click("model");
                ToTop();
                Pick("model-provider", "provider-glm");
                ScrollTo("model-apply-preset"); Click("model-apply-preset");
                ScrollTo("model-credential");
                Click("model-credential");
                Require(ChoiceIds().Contains("credential-standard_api_key") && ChoiceIds().Contains("credential-legacy_compatible_key"),
                    "the actual GLM standard preset presents both supported credential categories");
                PickOpen("credential-legacy_compatible_key");
                var glm = Get(window, "modelDraft");
                Require((string)Get(glm, "CredentialKind") == "legacy_compatible_key" && GetOptional(glm, "ApiKey") == null,
                    "selecting the explicit credential category keeps the old provider's key out of this draft");

                ToTop();
                Pick("model-provider", "provider-ollama");
                Click("model-field-model-id");
                Sample(At("model-field-model-id"), queue: "fixture-local-model");
                Click("model-save-use");
                NativeQuickItemChecks.Until(() => { Prepare(); return GetOptional(window, "modelWrite") == null; });
                var used = Get(window, "modelDraft");
                var oldDefault = (Guid)Get(used, "Id");
                Require((bool)Get(connections, "Enabled") && (Guid)Get(connections, "DefaultId") == oldDefault &&
                    ((string)Get(used, "DisplayName")).Contains("fixture-local-model"),
                    "the normal first-use button saves, names and activates a local connection as default without a companion");

                Guid first = Create("聊天队友甲"), second = Create("聊天队友乙");
                Prepare(); Click("profile:" + first);
                ScrollTo("model-manage");
                Click("model-manage");
                ScrollTo("model-bind"); Click("model-bind");
                NativeQuickItemChecks.Until(() => { Prepare(); return GetOptional(window, "modelWrite") == null; });
                ToTop(); Click("model-create");
                Click("model-provider");
                Require(ChoiceIds()[0] == "provider-custom" && ((LlmConnectionDraft)Get(window, "modelDraft")).PresetId == "custom_openai_chat",
                    "the actual management New connection action starts with a blank custom configuration and Custom as the first option");
                PickOpen("provider-custom");
                Pick("model-protocol", "protocol-openai_responses");
                Click("model-field-base-setup");
                Sample(At("model-field-base-setup"), queue: "http://127.0.0.1:11434/v1");
                Click("model-field-model-id");
                Sample(At("model-field-model-id"), queue: "next-default-model");
                Click("model-save-use");
                NativeQuickItemChecks.Until(() => { Prepare(); return GetOptional(window, "modelWrite") == null; });
                var newDefault = (LlmConnectionDraft)Get(window, "modelDraft");
                var owner = (Guid)Get(Call(window, "CurrentProfile"), "OwnerId");
                Require((Guid)Get(connections, "DefaultId") == newDefault.Id && newDefault.Id != oldDefault &&
                    (Guid)Call(connections, "GetBinding", owner, first) == oldDefault && Call(connections, "GetBinding", owner, second) == null,
                    "saving a custom Responses draft changes the default independently of explicit teammate overrides");
                Click("chat");
                NativeQuickItemChecks.Until(() => { Call(host, "Update", 0UL); Prepare();
                    return ((string)Get(Get(window, "chatView"), "Connection")).Contains("fixture-local-model"); });
                Click("profile:" + second);
                NativeQuickItemChecks.Until(() => { Call(host, "Update", 0UL); Prepare();
                    return ((string)Get(Get(window, "chatView"), "Connection")).Contains("next-default-model"); });
                Click("model"); ScrollTo("model-manage"); Click("model-manage");
                Click("model-enabled");
                NativeQuickItemChecks.Until(() => { Prepare(); return GetOptional(window, "modelWrite") == null; });
                Require(!(bool)Get(connections, "Enabled"), "explicit connection management can turn the feature off before the offline chat checks");
                Click("model-back-to-setup");

                Prepare(); Click("profile:" + first); Click("chat"); Click("chat-draft");
                var editAt = At("chat-draft");
                Sample(editAt, queue: "你好");
                Require((bool)Get(window, "OwnsTextToken") && Main.blockInput && Draft() == "你好", "chat editor acquires the real native lease and commits UTF-16 characters");
                ime.Composition = "shi"; ime.Candidate = true;
                Sample(editAt, keys: new[] { Keys.LeftShift, Keys.Enter }, queue: "\r");
                ime.Composition = ""; ime.Candidate = false;
                Sample(editAt, keys: new[] { Keys.LeftShift, Keys.Enter }, queue: "世\r");
                Sample(editAt, keys: new[] { Keys.LeftShift, Keys.Enter }, queue: "\r");
                Require(Draft() == "你好世", "IME confirmation and its held Enter tail leave the committed candidate intact without inserting a line");
                Sample(editAt);
                Sample(editAt, keys: new[] { Keys.LeftShift, Keys.Enter }, queue: "\r");
                Sample(editAt, keys: new[] { Keys.LeftShift, Keys.Enter }, queue: "\r");
                Require(Draft() == "你好世\n", "Shift+Enter inserts exactly one newline per physical press");
                Sample(editAt);
                Sample(editAt, keys: new[] { Keys.Enter }, queue: "\r");
                Require(Draft() == "你好世\n", "a disabled connection never clears the draft on Enter");
                Click("profile:" + second);
                Require(Draft() == "" && GetOptional(window, "Editor") == null && !Main.blockInput,
                    "another target opens its own draft and the previous edit releases the native lease");
                Click("profile:" + first);
                Require(Draft() == "你好世\n", "the draft returns to its original target on selection");
                CheckHistoryMeasurement(window, layout);
                Click("chat-draft"); Sample(editAt);
                Click("model"); Sample(Vector2.Zero);
                Require(GetOptional(window, "Editor") == null && !Main.blockInput && Main.CurrentInputTextTakerOverride == null && !PlayerInput.WritingText,
                    "page switch releases the trusted native text consumer");
                Console.WriteLine("PASS companion four-page input, zero-profile model settings, provider aliases, per-page scroll, IME Enter priority, held Shift+Enter and target drafts.");
            }
            finally
            {
                Call(window, "Close");
                imeField.SetValue(input, originalIme);
                NativeToolsUiChecks.Mouse(Vector2.Zero, false);
                Main.clrInput();
            }
        }

        private static void CheckModelResultSubject(object window, object connections)
        {
            var presets = ((IEnumerable)Get(connections, "Presets")).Cast<LlmPreset>().ToArray();
            string model = "fixture-model";
            var meter = presets.Single(preset => preset.Id == "minimax_china_anthropic");
            var subscription = presets.Single(preset => preset.Id == "minimax_china_subscription_anthropic");
            var draft = new LlmConnectionDraft(Guid.NewGuid()) { DisplayName = "测试连接", BaseUrl = meter.BaseUrl, Model = model };
            string regular = (string)Call(window, "ModelTestSubject", draft, meter);
            string plan = (string)Call(window, "ModelTestSubject", draft, subscription);
            Require(meter.BaseUrl == subscription.BaseUrl && regular.Contains(meter.PlanLabel) && plan.Contains(subscription.PlanLabel) &&
                regular.Contains(meter.RegionLabel) && plan.Contains(subscription.RegionLabel) && regular != plan &&
                regular.Contains("api.minimax.cn / " + model) && plan.Contains("api.minimax.cn / " + model),
                "connection tests identify plan, region, final host and selected model even where different plans share one host");
        }

        private static void CheckHistoryMeasurement(object window, object layout)
        {
            var now = DateTimeOffset.UtcNow;
            var entries = Enumerable.Range(0, 100).Select(i => new LlmHistoryEntry(Guid.NewGuid(),
                i == 2 ? "action" : "assistant", i == 2 ? "正在钓鱼。" : "队友回复 " + i, now)).ToArray();
            object[] Blocks() => ((IEnumerable)Get(window, "chatBlocks")).Cast<object>().ToArray();
            int Measurements() => (int)Get(layout, "MeasurementCount");
            void Update(LlmHistoryEntry[] records) => Call(window, "UpdateChatHistory", (object)records);
            Update(entries);
            var old = Blocks();
            Set(window, "followChat", false);
            Set(window, "chatOffset", (float)Get(old[12], "Top") + 8f);
            int before = Measurements();
            entries = entries.Skip(1).Concat(new[] { new LlmHistoryEntry(Guid.NewGuid(), "user", "该结束了。", now) }).ToArray();
            Update(entries);
            var afterTrim = Blocks();
            Require(Measurements() - before < 20 && ReferenceEquals(Get(old[12], "Lines"), Get(afterTrim[11], "Lines")),
                "a full history evicts its first entry and only measures the new row");
            Require((float)Get(window, "chatOffset") == (float)Get(afterTrim[11], "Top") + 8f,
                "trimming the oldest row keeps the same visible conversation anchor");
            var changed = entries[1];
            var revised = (LlmHistoryEntry[])entries.Clone();
            revised[1] = new LlmHistoryEntry(changed.Id, changed.Kind, "已停止钓鱼。\n队友开始跟随。", now.AddSeconds(1));
            var originalLines = Get(afterTrim[1], "Lines");
            before = Measurements();
            Update(revised);
            var afterAction = Blocks();
            Require(Measurements() - before < 28 && !ReferenceEquals(Get(afterAction[1], "Lines"), originalLines) &&
                ReferenceEquals(Get(afterTrim[11], "Lines"), Get(afterAction[11], "Lines")),
                "an early activity receipt updates its row without measuring the trailing history again");
            Require((float)Get(window, "chatOffset") == (float)Get(afterAction[11], "Top") + 8f,
                "an action card changing height keeps older reading in the same place");
        }
    }
}
