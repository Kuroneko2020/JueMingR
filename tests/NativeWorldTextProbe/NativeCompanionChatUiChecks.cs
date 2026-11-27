using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
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
            var window = Get(Get(context, "Shell"), "CompanionUi");
            var layout = Get(Get(Get(context, "Shell"), "State"), "Layout");
            var screen = new Vector2(700, 500);
            void Sample(Vector2 point, bool left)
            {
                NativeToolsUiChecks.Mouse(point, left);
                Call(window, "Process", true, new KeyboardState(), point, Matrix.Identity, screen, true, 0);
            }
            void Open(string key)
            {
                var control = ((IEnumerable)Get(window, "controls")).Cast<object>().Single(c =>
                    ((string)Get(c, "Key")).StartsWith(key + "/", StringComparison.Ordinal));
                var point = NativeToolsUiChecks.Point(Get(control, "Rect"));
                Sample(point, false); Sample(point, true); Sample(point, false);
            }
            try
            {
                NativeToolsUiChecks.Mouse(Vector2.Zero, false);
                Call(window, "Toggle", Get(layout, "Viewport"));
                Call(window, "Prepare", Matrix.Identity, screen);
                Open("model");
                graphics.Image(Path.Combine(output, "companion-model-narrow.png"), () => Call(window, "Draw"), Matrix.Identity, (int)screen.X, (int)screen.Y);
                Open("chat");
                graphics.Image(Path.Combine(output, "companion-chat-narrow.png"), () => Call(window, "Draw"), Matrix.Identity, (int)screen.X, (int)screen.Y);
                Console.WriteLine("PASS companion model and chat pages draw using original font and panel assets at narrow viewport.");
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
            var input = Get(Get(window, "aiInput"), "input");
            var imeField = input.GetType().GetField("ime", BindingFlags.Instance | BindingFlags.NonPublic);
            object originalIme = imeField.GetValue(input);
            var ime = new TextCandidate(imeField.FieldType);
            var screen = new Vector2(1280, 900);
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
                Require(GetOptional(window, "modelDraft") != null && Control("model-create") != null && Control("model-provider") != null,
                    "model configuration is projected and actionable without a selected profile");
                CheckModelResultSubject(window, Get(host, "ChatConnections"));
                Click("model-provider");
                Click("model-field-search");
                Sample(At("model-field-search"), queue: "Claude");
                Sample(At("model-field-search"), keys: new[] { Keys.Enter }, queue: "\r");
                Sample(Vector2.Zero);
                Require(Control("model-provider-anthropic") != null && Control("model-provider-deepseek") == null,
                    "provider filtering accepts an official alias and excludes unrelated profiles");
                int builds = (int)Get(window, "LayoutBuilds");
                for (int frame = 0; frame < 120; frame++) Prepare();
                Require((int)Get(window, "LayoutBuilds") == builds, "stable model path reuses filtered layout for 120 prepares");
                Click("model-return");
                var modelPoint = NativeToolsUiChecks.Point(Get(window, "modelScrollView"));
                Sample(modelPoint, wheel: -120);
                int scroll = (int)Get(window, "modelOffset");
                Require(scroll > 0, "model rows have a real independent scroll range");
                Click("chat"); Click("model");
                Require((int)Get(window, "modelOffset") == scroll, "view changes preserve model scroll");

                void ScrollTo(string key)
                {
                    for (int i = 0; i < 24 && Control(key) == null; i++) Sample(modelPoint, wheel: -120);
                    Require(Control(key) != null, "scroll reveals " + key);
                }
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
                Set(window, "modelOffset", 0);
                Call(window, "Invalidate"); Prepare();
                Click("model-provider");
                Click("model-provider-glm");
                Click("model-plan-standard_api");
                Click("model-region-china");
                ScrollTo("model-credential");
                Click("model-credential");
                Require(Control("model-credential-standard_api_key") != null && Control("model-credential-legacy_compatible_key") != null,
                    "the actual GLM standard preset presents both supported credential categories");
                Click("model-credential-legacy_compatible_key");
                var glm = Get(window, "modelDraft");
                Require((string)Get(glm, "CredentialKind") == "legacy_compatible_key" && GetOptional(glm, "ApiKey") == null,
                    "selecting the explicit credential category keeps the old provider's key out of this draft");

                Guid first = Create("聊天队友甲"), second = Create("聊天队友乙");
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
