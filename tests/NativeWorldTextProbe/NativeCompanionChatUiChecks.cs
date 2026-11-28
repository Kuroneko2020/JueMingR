using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Threading;
using System.Threading.Tasks;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Companions;
using JueMingR.Platform.Llm;
using JueMingR.Features.Llm;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Graphics;
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
                CheckDrawContinuity(context, window, graphics, output);
                Resize(960, 640, 1f);
                Call(window, "Toggle", Get(layout, "Viewport")); PrepareTaskPicture();
                Console.WriteLine("PASS original XNA model and chat at 700x500, 1280x800 and 1920x1080 (115%); dropdown, long fields, advanced errors and conversation cards.");

                void PrepareTaskPicture()
                {
                    Resize(960, 640, 1f);
                    NativeCompanionCoreLifecycleChecks.PrepareHuman();
                    Require((bool)Call(host, "Join", id), "the showcased task uses a real joined companion");
                    var body = Call(host, "GetBody", id);
                    var actor = Call(host, "Owned", body);
                    var profile = (CompanionProfile)Get(actor, "Profile");
                    CheckBudgetPicture(profile);
                    var identity = new CompanionTaskIdentity(profile.OwnerId, id, (long)Get(host, "session"), (long)Get(actor, "Generation"), profile.DataEpoch);
                    var anchor = host.GetType().GetMethod("CaptureFishingAnchor", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { Main.LocalPlayer });
                    var need = new CompanionTaskCondition("has_item", new Dictionary<string, object>
                    { ["item_type"] = (int)Terraria.ID.ItemID.TerraBlade, ["quantity"] = 1 }, wait: true, timeoutTicks: 600);
                    var waiting = (CompanionTaskSnapshot)Call(host, "SubmitTask", identity, "有武器后待命",
                        new[] { new CompanionTaskStep("stand_by", null, condition: need) }, anchor, Guid.NewGuid());
                    Require(waiting.State == "accepted", "condition task enters the real runtime: " + waiting.Detail);
                    CompanionTaskSnapshot Current() => (CompanionTaskSnapshot)Call(host, "GetTask", profile.OwnerId, id, null);
                    NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); return Current().State == "waiting"; });
                    Resize(960, 640, 1f);
                    Open("profile:" + id); Open("chat");
                    Require((string)Get(window, "chatTaskReason") == Current().Detail && Current().Detail.Contains("主背包数量 0"),
                        "default task view renders the authoritative waiting reason without opening details");
                    Image("chat-task-waiting");
                    Open("chat-stop-task:" + waiting.TaskId);
                    var task = (CompanionTaskSnapshot)Call(host, "SubmitTask", identity, "在这里等我", new[] { new CompanionTaskStep("stand_by", null) }, anchor, Guid.NewGuid());
                    Require(task.State == "accepted", "task visible in native draw begins in the authoritative accepted state");
                    NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); return ((CompanionTaskSnapshot)Call(host, "GetTask", profile.OwnerId, id, null)).State == "succeeded"; });
                    Resize(960, 640, 1f);
                    Require(Control("chat-stop-task:" + task.TaskId) != null, "an already entered standby remains stoppable in the visible task bar");
                    Require(((string)Get(window, "chatTaskSummary")).StartsWith("待命中", StringComparison.Ordinal), "default view presents the real ongoing stance");
                    Image("chat-task");
                    Open("chat-task-details"); Image("chat-task-detail");
                    bool follows = profile.Behavior.Follow;
                    Open("chat-stop-task:" + task.TaskId);
                    Require(((CompanionTaskSnapshot)Call(host, "GetTask", profile.OwnerId, id, null)).State == "cancelled" &&
                        ((CompanionProfile)Get(actor, "Profile")).Behavior.Follow == follows && Control("chat-stop-task:" + task.TaskId) == null,
                        "the real viewed task stop retires the completed standby and preserves Follow");
                    Console.WriteLine("PASS native rendered task bar, expandable receipt and physical stop after entering standby.");
                }

                void CheckBudgetPicture(CompanionProfile profile)
                {
                    var format = window.GetType().GetMethod("FormatChatTokens", BindingFlags.Static | BindingFlags.NonPublic);
                    string Tokens(long? total, long missing = 0, long count = 1) => (string)format.Invoke(null, new object[] { new LlmUsageTotals(count, missing, total: total) });
                    Require(Tokens(null, 0, 0) == "0 tokens" && Tokens(null, 1) == "—" && Tokens(999) == "999 tokens" &&
                        Tokens(1000) == "1k tokens" && Tokens(1249) == "1.2k tokens" && Tokens(3400000) == "3.4m tokens" &&
                        Tokens(999950) == "1m tokens" && Tokens(999950, 1, 2) == "999.9k+ tokens" && Tokens(1260, 1, 2) == "1.2k+ tokens",
                        "only the footer abbreviates large totals and never rounds a partial lower bound upward");
                    var fixture = NativeCompanionChatChecks.Attach(host);
                    try
                    {
                        object View() => Call(host, "GetChat", profile.OwnerId, profile.Id);
                        void SendKnown(string text, LlmUsageSample sample)
                        {
                            fixture.Remote.Reply = request => Task.FromResult(new LlmTurn("答复完成。", Array.Empty<LlmToolCall>(),
                                "{\"role\":\"assistant\",\"content\":\"答复完成。\"}", usage: sample));
                            object[] arguments = { profile.OwnerId, profile.Id, text, null };
                            Require((bool)Call(host, "SendChat", arguments), "real Host sends the controlled usage request: " + arguments[3]);
                            NativeQuickItemChecks.Until(() => { Call(host, "PumpChat"); return !(bool)Get(View(), "IsRunning"); });
                        }
                        NativeQuickItemChecks.Until(() => { Call(host, "PumpChat"); return (bool)Get(View(), "CanSend"); });
                        SendKnown("说说库存。", new LlmUsageSample(300, 40, cacheRead: 22));
                        var connections = (LlmConnections)Get(host, "ChatConnections");
                        var alternate = connections.Edit();
                        connections.ApplyPreset(alternate, "deepseek_official_chat");
                        alternate.DisplayName = "换用连接"; alternate.Model = "deepseek-next"; alternate.ApiKey = "local-footer-model-test";
                        connections.SaveAndUseAsync(alternate, profile.OwnerId, profile.Id).GetAwaiter().GetResult();
                        SendKnown("还有用量记录吗？", new LlmUsageSample(190, null, 215, reasoning: 9));
                        Resize(700, 500, 1f); Open("profile:" + profile.Id); Open("chat");
                        var progress = (LlmChatProgress)Get(View(), "Progress");
                        var usage = (LlmChatUsage)Get(View(), "Usage");
                        string Footer() => (string)Get(window, "chatUsageLine");
                        string percent = (100d * progress.EstimatedInput / progress.InputLimit).ToString("0", CultureInfo.InvariantCulture);
                        Require(usage.Connections.Count == 2 && Footer() == "当前上下文 " + percent + "% · 总用量 555+ tokens",
                            "actual footer includes one estimate and the current chat's known cumulative lower bound across both connections: " + Footer());
                        void AssertFooterFits()
                        {
                            var rect = Get(window, "chatUsageFooter");
                            var copy = Get(Control("chat-copy-status"), "Rect");
                            var send = Get(Control("chat-send"), "Rect");
                            var measured = Call(window, "MeasureOutlined", Footer(), .65f);
                            Require((float)Get(copy, "Right") < (float)Get(rect, "X") && (float)Get(rect, "Right") < (float)Get(send, "X") &&
                                (float)Get(rect, "Y") == (float)Get(send, "Y") && (float)Get(rect, "Y") > (float)Get(Get(window, "chatDraftRect"), "Bottom") &&
                                (float)Get(rect, "Width") >= (float)Get(measured, "Width") && Control("chat-usage") == null,
                                "the only usage line fits the original footer between copy status and send without a detail control");
                        }
                        AssertFooterFits(); Image("chat-usage-footer-100");
                        Resize(700, 500, 1.15f);
                        AssertFooterFits(); Image("chat-usage-footer-115");
                        Call(host, "RestartChatContext", profile.OwnerId, profile.Id);
                        Resize(700, 500, 1f);
                        Require(Footer().EndsWith("总用量 555+ tokens", StringComparison.Ordinal), "context reset preserves the same chat's cumulative usage");
                        Require((bool)Call(host, "Create", "空用量队友"), "actual roster supplies another independent conversation");
                        var other = (Guid)Get(host, "LastCreatedId");
                        NativeQuickItemChecks.Until(() => { Call(host, "Update", 0UL); return (bool)Call(archive, "IsSaved", other); });
                        Resize(700, 500, 1f); Open("profile:" + other);
                        Require(Footer().EndsWith("总用量 0 tokens", StringComparison.Ordinal), "selecting another actor immediately shows its actual zero total");
                        Open("profile:" + profile.Id);
                        Call(window, "Close"); Call(window, "Toggle", Get(layout, "Viewport")); Resize(700, 500, 1f);
                        Open("profile:" + profile.Id); Open("chat");
                        Require(Footer().EndsWith("总用量 555+ tokens", StringComparison.Ordinal), "reopening returns to the correct actor's existing total");
                        Resize(960, 640, 1f);
                        Console.WriteLine("PASS actual chat footer: one line at two scales, two connections with a partial cumulative total, actor switch, reopen and context reset.");
                    }
                    finally { fixture.Restore(); }
                }
            }
            finally { Call(window, "Close"); NativeToolsUiChecks.Mouse(Vector2.Zero, false); }
        }

        internal static void CheckTaskControls(object context, object host, CompanionProfile first, CompanionProfile second)
        {
            var shell = Get(context, "Shell"); var window = Get(shell, "CompanionUi");
            var layout = Get(Get(shell, "State"), "Layout");
            var renderer = Get(shell, "renderer");
            var originalFont = Terraria.GameContent.FontAssets.MouseText;
            var screen = new Vector2(700, 500);
            var ai = Get(window, "aiInput"); var text = Get(ai, "input");
            var imeField = text.GetType().GetField("ime", BindingFlags.Instance | BindingFlags.NonPublic);
            object originalIme = imeField.GetValue(text);
            CompanionTaskSnapshot View(CompanionProfile profile) => (CompanionTaskSnapshot)Call(host, "GetTask", profile.OwnerId, profile.Id, null);
            object Control(string key) => ((IEnumerable)Get(window, "controls")).Cast<object>().SingleOrDefault(c =>
                ((string)Get(c, "Key")).StartsWith(key + "/", StringComparison.Ordinal));
            void Sample(Vector2 at, bool left = false, string queue = "", int wheel = 0)
            {
                NativeToolsUiChecks.Mouse(at, left);
                Call(window, "BeforeInput", true);
                Main.keyCount = queue.Length;
                for (int i = 0; i < queue.Length; i++) { Main.keyInt[i] = queue[i]; Main.keyString[i] = queue[i].ToString(); }
                Call(window, "ProcessLayoutInput", true, new KeyboardState(), at, Matrix.Identity, screen, true, wheel);
            }
            Vector2 At(string key) => NativeToolsUiChecks.Point(Get(Control(key) ?? throw new InvalidOperationException("Task fixture control absent: " + key), "Rect"));
            void Click(string key) { var at = At(key); Sample(at); Sample(at, true); Sample(at); }
            try
            {
                if (originalFont?.Value == null) FiniteCostChecks.SetCpuFont(8);
                Call(renderer, "RefreshResources");
                Call(renderer, "Prepare", Get(shell, "State"), screen.X, screen.Y, 1f);
                Call(window, "Close"); imeField.SetValue(text, new TextCandidate(imeField.FieldType).GetTransparentProxy());
                NativeToolsUiChecks.Mouse(Vector2.Zero, false);
                Call(window, "Toggle", Get(layout, "Viewport"));
                Call(window, "PrepareLayout", Matrix.Identity, screen);
                var a = View(first); var b = View(second);
                Require(a.State == "paused" && b.State == "succeeded", "task UI uses independently observed running and completed task states");
                Click("profile:" + first.Id); Click("chat"); Click("chat-draft");
                Sample(At("chat-draft"), queue: "任务期间的草稿");
                var editor = (JueMingR.Features.Text.TextEditBuffer)Get(ai, "Editor");
                var editorRect = Get(window, "chatDraftRect");
                var history = Get(window, "chatBlocks");
                int layouts = (int)Get(window, "LayoutBuilds");
                for (int i = 0; i < 60; i++) Call(window, "PrepareLayout", Matrix.Identity, screen);
                Require((int)Get(window, "LayoutBuilds") == layouts, "stable task progress does not rebuild the chat layout");
                var historyOffset = (float)Get(window, "chatOffset");
                Click("chat-task-details");
                var details = ((IEnumerable)Get(window, "chatTaskDetailLines")).Cast<object>().Select(line => (string)Get(line, "Text")).ToArray();
                Require(details.Any(line => line.Contains("第 1 步")) && ReferenceEquals(editor, Get(ai, "Editor")) && editor.Text == "任务期间的草稿" &&
                    editorRect.Equals(Get(window, "chatDraftRect")), "showing real receipts keeps the current editor and its layout");
                Sample(NativeToolsUiChecks.Point(Get(window, "chatHistoryView")), wheel: -120);
                Click("chat-task-details");
                Require((float)Get(window, "chatOffset") == historyOffset && ReferenceEquals(history, Get(window, "chatBlocks")), "task receipt browsing preserves the conversation scroll and cache");
                Click("profile:" + second.Id);
                bool follow = (bool)Get(Get(Call(window, "CurrentProfile"), "Behavior"), "Follow");
                Click("chat-stop-task:" + b.TaskId);
                Require(View(second).State == "cancelled" && View(first).TaskId == a.TaskId && View(first).State == "paused" &&
                    (bool)Get(Get(Call(window, "CurrentProfile"), "Behavior"), "Follow") == follow,
                    "stopping an ongoing stance after completion targets only the selected companion and leaves Follow unchanged");
                Require(Control("chat-stop-task:" + b.TaskId) == null, "retired task no longer offers a stop action");
                Click("profile:" + first.Id);
                Require(ReferenceEquals(editor, Get(Get(window, "currentChatDraft"), "Text")), "reselecting the first companion restores its own unsent draft");
                Console.WriteLine("PASS real task toolbar: progress, details, draft and scroll, no steady rebuild, stable owner-selected stop after standby completion, preserved Follow.");
            }
            finally
            {
                Call(window, "Close"); imeField.SetValue(text, originalIme); Main.clrInput(); NativeToolsUiChecks.Mouse(Vector2.Zero, false);
                Terraria.GameContent.FontAssets.MouseText = originalFont; Call(renderer, "RefreshResources");
            }
        }

        private sealed class TextCandidate : RealProxy
        {
            internal TextCandidate(Type type) : base(type) { }
            internal string Composition = "";
            internal bool Candidate;
            internal int Toggles;
            public override IMessage Invoke(IMessage message)
            {
                var call = (IMethodCallMessage)message;
                if (call.MethodName == "Toggle") Toggles++;
                object result = call.MethodName == "get_Composition" ? (object)Composition : call.MethodName == "get_Candidates" ? Candidate : (object)null;
                return new ReturnMessage(result, null, 0, call.LogicalCallContext, call);
            }
        }

        private static void CheckDrawContinuity(object context, object window, ProbeGraphics graphics, string output)
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
            var physicalInput = Get(context, "Input");
            var ai = Get(window, "aiInput"); var text = Get(ai, "input");
            var imeField = text.GetType().GetField("ime", fields); var originalIme = imeField.GetValue(text);
            var ime = new TextCandidate(imeField.FieldType);
            var nativeAnchor = typeof(Main).GetField("imePanelAnchor", fields);
            var foreground = Get(physicalInput, "foregroundWindow");
            float scale = Main.UIScale; int oldWidth = Main.screenWidth, oldHeight = Main.screenHeight;
            var screen = new Vector2(960, 640);
            void Prepare() => Call(window, "Prepare", Main.UIScaleMatrix, screen);
            object Rect(string key) => Get(((IEnumerable)Get(window, "controls")).Cast<object>().SingleOrDefault(c =>
                ((string)Get(c, "Key")).StartsWith(key + "/", StringComparison.Ordinal)) ?? throw new InvalidOperationException("IME fixture field absent: " + key), "Rect");
            void Sample(Vector2 at, bool left = false, Keys[] keys = null, string press = null)
            {
                keys = keys ?? Array.Empty<Keys>();
                NativeQuickItemChecks.Sample(physicalInput, keys);
                NativeToolsUiChecks.Mouse(at, left);
                Call(window, "BeforeInput", true);
                if (press != null)
                {
                    Main.keyCount = press.Length;
                    for (int i = 0; i < press.Length; i++) { Main.keyInt[i] = press[i]; Main.keyString[i] = press[i].ToString(); }
                }
                Call(window, "Process", true, new KeyboardState(keys), at, Main.UIScaleMatrix, screen, true, 0);
            }
            void Click(Vector2 at) { Sample(at); Sample(at, true); Sample(at); }
            Color[] Draw(Rectangle? restrict = null)
            {
                nativeAnchor.SetValue(null, null);
                return graphics.Pixels(() =>
                {
                    var device = graphics.GraphicsDevice;
                    var prior = device.ScissorRectangle;
                    if (restrict.HasValue) device.ScissorRectangle = restrict.Value;
                    try { Call(window, "Draw"); }
                    finally { if (restrict.HasValue) device.ScissorRectangle = prior; }
                }, Main.UIScaleMatrix, (int)screen.X, (int)screen.Y);
            }
            Vector2 Anchor() => (Vector2)Get(nativeAnchor.GetValue(null) ?? throw new InvalidOperationException("Native IME anchor missing"), "Position");
            Rectangle Crop(object rect)
            {
                var a = Vector2.Transform(new Vector2((float)Get(rect, "X"), (float)Get(rect, "Y")), Main.UIScaleMatrix);
                var b = Vector2.Transform(new Vector2((float)Get(rect, "Right"), (float)Get(rect, "Bottom")), Main.UIScaleMatrix);
                return new Rectangle((int)Math.Ceiling(a.X), (int)Math.Ceiling(a.Y), (int)Math.Floor(b.X) - (int)Math.Ceiling(a.X),
                    (int)Math.Floor(b.Y) - (int)Math.Ceiling(a.Y));
            }
            bool SameCrop(Color[] a, Color[] b, Rectangle area)
            {
                for (int y = area.Top; y < area.Bottom; y++)
                    for (int x = area.Left; x < area.Right; x++)
                        if (a[y * (int)screen.X + x] != b[y * (int)screen.X + x]) return false;
                return true;
            }
            void CheckDraftGeometry(float uiScale)
            {
                Main.UIScale = uiScale; Prepare();
                var field = Rect("chat-draft");
                float originX = (float)Get(field, "X") + 7, originY = (float)Get(field, "Y") + 3;
                var at = new Vector2(originX, originY + 3);
                Click(at);
                var buffer = (JueMingR.Features.Text.TextEditBuffer)Get(ai, "Editor");
                void Replace(string value)
                {
                    buffer.SelectAll();
                    Sample(at, press: value);
                    if (value.Length == 0) { buffer.DeleteSelection(); Prepare(); }
                }
                var font = graphics.Font;
                const float glyphScale = .76f;
                var sample = Enumerable.Range(33, 94).Select(c => (char)c).Concat("你好，世界ＭｅＢ正方Ｔ·；？！关大小开语");
                var widths = sample.Select(c => new { Glyph = c, Gap = Math.Abs(font.CharacterSpacing + font.GetCharacterMetrics(c).KernedWidth - font.MeasureString(c.ToString()).X) });
                var widest = widths.OrderByDescending(g => g.Gap).First();
                string mixed = "你好，世界 MiW " + (widest.Gap > .05f ? new string(widest.Glyph, 8) : "") + " ";
                if (uiScale == 1f)
                    Console.WriteLine("CHAT FONT original MouseText continuation max tested metric gap=" + widest.Gap + " glyph=" + widest.Glyph + " spacing=" + font.CharacterSpacing);
                Replace(mixed);
                Require(buffer.Text == mixed && buffer.Caret == mixed.Length, "original input commits mixed text and both trailing spaces at the true logical end");
                Draw();
                float firstCaret = Anchor().X;
                var chatLayout = Get(window, "chatDraftLayout");
                var lines = (IList)Get(chatLayout, "Lines");
                var size = Get(Get(lines[0], "Label"), "TextSize");
                float nativeOrigin = originX - (float)Get(size, "OffsetX");

                // A second, independent native draw projects the next glyph
                // after the committed line. Read ink rather than using CaretX.
                // Subtract the chosen glyph's known line-start adjustment.
                var batch = (SpriteBatch)Get(graphics, "batch");
                const char marker = 'H';
                Color[] Reference(string textValue) => graphics.Pixels(() =>
                    batch.DrawString(font, textValue, new Vector2(100, 100), Color.White, 0f, Vector2.Zero, glyphScale, SpriteEffects.None, 0f),
                    Main.UIScaleMatrix, (int)screen.X, (int)screen.Y);
                int InkLeft(Color[] a, Color[] b)
                {
                    for (int x = 85; x < (int)screen.X - 12; x++)
                        for (int y = 89; y < 170; y++)
                            if (a[y * (int)screen.X + x] != b[y * (int)screen.X + x]) return x;
                    throw new InvalidOperationException("Native glyph did not draw a discriminator pixel");
                }
                var metric = font.GetCharacterMetrics(marker);
                float markerAdjustment = (font.CharacterSpacing + metric.KernedWidth - metric.KernedWidthOnNewLine) * glyphScale;
                float actualScale = Main.UIScaleMatrix.M11;
                float Pen(string value) => (InkLeft(Reference(value + marker), Reference(value)) - InkLeft(Reference(marker.ToString()), Reference(""))) / actualScale - markerAdjustment;
                float end = Pen(mixed);
                float isolated = buffer.Boundaries.Zip(buffer.Boundaries.Skip(1), (a, b) => font.MeasureString(mixed.Substring(a, b - a)).X * glyphScale).Sum();
                Require(Math.Abs(firstCaret - nativeOrigin - end) <= 1.5f / actualScale,
                    "chat caret agrees with independent native next-glyph ink, including trailing spaces: " + firstCaret + " vs " + (nativeOrigin + end));
                if (widest.Gap >= 1f)
                    Require(Math.Abs(isolated - end) > 3f, "the independent native ruler discriminates the prior isolated-character geometry");
                Console.WriteLine("CHAT DRAFT native measured ui=" + uiScale + " old-isolated=" + isolated + " next-glyph=" + end + " caret=" + (firstCaret - nativeOrigin));

                const int between = 5;
                float hit = Pen(mixed.Substring(0, between));
                var middle = new Vector2(nativeOrigin + hit, at.Y);
                Click(middle);
                Sample(middle, press: "插");
                Require(buffer.Text == mixed.Insert(between, "插"), "actual pointer places a queued character at the gap following the Chinese phrase");
                Replace("前e\u0301后X");
                Draw();
                chatLayout = Get(window, "chatDraftLayout");
                lines = (IList)Get(chatLayout, "Lines");
                size = Get(Get(lines[0], "Label"), "TextSize");
                nativeOrigin = originX - (float)Get(size, "OffsetX");
                var selected = new Vector2(nativeOrigin + Pen("前"), at.Y);
                Click(selected);
                Sample(selected, keys: new[] { Keys.LeftShift, Keys.Right });
                Require(buffer.SelectedText == "e\u0301", "physical Shift+Right selects the full combining text element");
                var before = Draw();
                Require(Enumerable.Range((int)(selected.X * actualScale), 15).Any(x => before[(int)((originY + 1) * actualScale) * (int)screen.X + x] == new Color(62, 101, 170)),
                    "the selected text element has a visible native selection rectangle");
                Sample(selected); Sample(selected, press: "中");
                Require(buffer.Text == "前中后X", "queued native input replaces the physically selected element exactly once");

                string wide = String.Concat(Enumerable.Repeat("W你", 48));
                Replace(wide);
                chatLayout = Get(window, "chatDraftLayout");
                lines = (IList)Get(chatLayout, "Lines");
                Require(lines.Count > 1, "large committed phrase wraps in the real chat composer");
                var last = lines[lines.Count - 1];
                string visual = (string)Get(Get(last, "Label"), "Text");
                size = Get(Get(last, "Label"), "TextSize");
                Draw();
                Require(buffer.Caret == wide.Length && Math.Abs(Anchor().X - (originX - (float)Get(size, "OffsetX") + Pen(visual))) <= 1.5f / actualScale,
                    "final wrapped row restarts the native first-bearing rule at the visible caret");
                var stable = Get(window, "chatDraftLayout");
                Sample(at);
                Require(ReferenceEquals(stable, Get(window, "chatDraftLayout")), "an empty sample retains the actual measured draft lines");
                string fourLines = String.Join("\n", Enumerable.Repeat("系同一行", 4));
                buffer.SelectAll();
                Require(buffer.Insert(fourLines), "fixture places a known four-line draft");
                Prepare();
                buffer.MoveTo(0); buffer.Insert("首");
                buffer.MoveTo(buffer.Text.Length); buffer.Insert("末");
                Prepare();
                var changed = (IList)Get(Get(window, "chatDraftLayout"), "Lines");
                Require((string)Get(Get(changed[0], "Label"), "Text") == "首系同一行" &&
                    (string)Get(Get(changed[changed.Count - 1], "Label"), "Text") == "系同一行末",
                    "two edits before presentation refresh cannot share a stale prefix before the last edit");
                Replace("");
            }
            void Check(string label, object field)
            {
                var at = NativeToolsUiChecks.Point(field);
                Click(at);
                var buffer = (JueMingR.Features.Text.TextEditBuffer)Get(ai, "Editor");
                Sample(at, press: "测");
                string committed = buffer.Text;
                var baseline = Draw();
                ime.Composition = "pin"; ime.Candidate = true;
                Sample(at);
                var composing = Draw(); var anchor = Anchor(); var crop = Crop(field);
                Require(!SameCrop(baseline, composing, crop), label + " shows inline composition in the actual surface");
                var point = Vector2.Transform(anchor - new Vector2(0, 32), Main.UIScaleMatrix);
                bool caretInk = Enumerable.Range(Math.Max(crop.Left, (int)point.X - 1), 3).Where(x => x < crop.Right).Any(x =>
                    composing[(int)(point.Y - 2) * (int)screen.X + x] == Color.White);
                int toggles = ime.Toggles, builds = (int)Get(window, "LayoutBuilds");
                Main.keyCount = 1; Main.keyInt[0] = '中'; Main.keyString[0] = "中";
                for (int frame = 0; frame < 3; frame++)
                {
                    Main.CurrentInputTextTakerOverride = null;
                    Call(physicalInput, "BeginUpdate");
                    var pixels = Draw();
                    Require(Anchor() == anchor && SameCrop(composing, pixels, crop) && Main.CurrentInputTextTakerOverride == null &&
                        Main.keyCount == 1 && Main.keyInt[0] == '中' && ReferenceEquals(buffer, Get(ai, "Editor")) && buffer.Text == committed,
                        label + " keeps anchor and field pixels through an empty native update and leaves the pending text untouched");
                }
                Require(crop.Contains((int)point.X, (int)point.Y - 1) && caretInk,
                    label + " publishes a visible caret baseline at the actual scaled field");
                var foreign = new object(); Main.CurrentInputTextTakerOverride = foreign; Draw();
                Require(nativeAnchor.GetValue(null) == null && ReferenceEquals(Main.CurrentInputTextTakerOverride, foreign), label + " cannot draw the candidate for a foreign owner");
                Main.CurrentInputTextTakerOverride = null;
                Draw(new Rectangle(0, 0, (int)screen.X, Math.Max(1, crop.Top - 1)));
                Require(nativeAnchor.GetValue(null) == null && ReferenceEquals(buffer, Get(ai, "Editor")), label + " suppresses a clipped anchor while retaining the edit");
                Require(ime.Toggles == toggles && (int)Get(window, "LayoutBuilds") == builds, label + " rendering never toggles IME or rebuilds layout");
                Draw();
                graphics.Image(Path.Combine(output, "companion-ime-" + label + ".png"), () => Call(window, "Draw"), Main.UIScaleMatrix, (int)screen.X, (int)screen.Y);
                ime.Composition = ""; ime.Candidate = false;
                Sample(at, keys: new[] { Keys.Enter }); Sample(at, keys: new[] { Keys.Enter }, press: "\r"); Sample(at);
                Require(ReferenceEquals(buffer, Get(ai, "Editor")) && buffer.Text == committed.Insert(buffer.Caret - 1, "中"),
                    label + " candidate confirmation commits once and the held Enter does not submit the editor");
                Console.WriteLine("PASS original XNA IME presentation: " + label + ", same 3 empty-frame crops and anchor, typed input resumed once.");
            }
            try
            {
                imeField.SetValue(text, ime.GetTransparentProxy());
                Main.screenWidth = (int)screen.X; Main.screenHeight = (int)screen.Y; Main.UIScale = 1.15f;
                PlayerInput.CacheOriginalScreenDimensions(); Prepare();
                CheckDraftGeometry(1f);
                CheckDraftGeometry(1.15f);
                Check("chat", Rect("chat-draft"));
                var chat = (JueMingR.Features.Text.TextEditBuffer)Get(ai, "Editor");
                Sample(NativeToolsUiChecks.Point(Rect("chat-draft")), keys: new[] { Keys.LeftShift, Keys.Enter }, press: "\r");
                Require(chat.Text.Contains("\n"), "chat Shift+Enter still inserts a newline after the native IME candidate tail");
                Click(NativeToolsUiChecks.Point(Rect("model")));
                Check("model-field", Rect("model-field-base-setup"));
                Click(NativeToolsUiChecks.Point(Rect("model-provider")));
                Check("provider-search", Get(Get(window, "modelDropdownSearch"), "Rect"));
                Click(Vector2.Zero);
                Call(window, "LoadModelDraft", Call(window, "NewModelDraft")); Prepare();
                Click(NativeToolsUiChecks.Point(Rect("model-provider")));
                var popup = Get(window, "modelDropdownPanel");
                Click(new Vector2((float)Get(popup, "X") + 20, (float)Get(window, "ModelDropdownListTop") + 1.5f * (float)Get(window, "modelDropdownRow")));
                var draft = (LlmConnectionDraft)Get(window, "modelDraft");
                Require(draft.PresetId == "deepseek_official_chat", "the known real provider route opens a supported model discovery");
                draft.ApiKey = "isolated-test-key";
                var connections = Get(window, "modelConnections"); var oldTransport = Get(connections, "transport");
                var discovery = new DiscoveryTransport { Next = Task.FromResult<IReadOnlyList<LlmModelInfo>>(new[]
                { new LlmModelInfo("chat-one", "支持文字"), new LlmModelInfo("chat-two", "支持文字") }) };
                Set(connections, "transport", discovery);
                try
                {
                    Click(NativeToolsUiChecks.Point(Rect("model-discover")));
                    NativeQuickItemChecks.Until(() => { Prepare(); return Get(window, "modelPicker").ToString() == "Models"; });
                    Require(discovery.Reads == 1 && discovery.Inference == 0, "the actual discovered model page is available with a controlled connection reply");
                }
                finally { Set(connections, "transport", oldTransport); }
                Check("model-search", Rect("model-field-search"));
                Main.UIScale = 1f; Draw();
                Require(nativeAnchor.GetValue(null) == null, "a stale companion matrix does not publish an anchor");
                Main.UIScale = 1.15f; Prepare();
                Set(physicalInput, "foregroundWindow", (Func<IntPtr>)(() => IntPtr.Zero));
                Call(physicalInput, "BeginUpdate"); Draw();
                Require(nativeAnchor.GetValue(null) == null && !(bool)Get(physicalInput, "CanPrepareText"), "real focus check closes the draw-only eligibility");
                Call(window, "Process", true, new KeyboardState(), Vector2.Zero, Main.UIScaleMatrix, screen, false, 0);
                Require(GetOptional(ai, "Editor") == null && !(bool)Get(text, "Owned"), "focus loss retires the actual editor lease");
                Console.WriteLine("PASS original XNA four AI edit consumers: 12 skipped-sample Draws, native anchors and identical editor pixels, foreign/clipped/stale/focus gates, no token or queued-text changes, Chinese Enter and multiline tail.");
            }
            finally
            {
                Call(window, "Close"); imeField.SetValue(text, originalIme); Set(physicalInput, "foregroundWindow", foreground);
                Main.screenWidth = oldWidth; Main.screenHeight = oldHeight; Main.UIScale = scale; PlayerInput.CacheOriginalScreenDimensions();
                nativeAnchor.SetValue(null, null); Main.clrInput();
                NativeQuickItemChecks.Sample(physicalInput, Array.Empty<Keys>());
                NativeQuickItemChecks.Sample(physicalInput, Array.Empty<Keys>());
            }
        }

        private sealed class DiscoveryTransport : ILlmTransport
        {
            internal Task<IReadOnlyList<LlmModelInfo>> Next;
            internal int Reads, Inference;
            public Task<IReadOnlyList<LlmModelInfo>> DiscoverAsync(LlmConnectionSnapshot connection, CancellationToken cancellation)
            {
                Reads++;
                return Next;
            }
            public Task<LlmTurn> CompleteAsync(LlmConnectionSnapshot connection, LlmRequest request, Action<string> stream, CancellationToken cancellation)
            {
                Inference++;
                throw new InvalidOperationException("Model discovery did not request inference.");
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
                    new[] { "model-create", "model-copy", "model-delete", "model-field-name", "model-enabled", "model-default", "model-plan", "model-region", "model-test-tools" }
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
                ScrollTo("model-manage"); Click("model-manage"); Click("model-save-name");
                NativeQuickItemChecks.Until(() => { Prepare(); return GetOptional(window, "modelWrite") == null; });
                string[] Feedback() => ((IEnumerable)Get(window, "labels")).Cast<object>().Select(label => (string)Get(label, "Text")).ToArray();
                Require(Feedback().Contains("保存未完成") && Feedback().Contains("请填写或选择模型 ID。") && Control("model-test-details") != null,
                    "a real failed configuration write exposes its actionable cause on the management page");
                Click("model-test-details");
                Require(!Feedback().Contains("请填写或选择模型 ID。"), "management can collapse its visible failure details");
                Click("model-back-to-setup");
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
                Require(Control("model-test-chat") != null && Control("model-save-use") != null && (bool)Get(window, "modelAdvancedExpanded"),
                    "advanced fields expand in place while the fixed basic actions remain available");
                ScrollTo("model-test-tools");
                Require((bool)Get(Control("model-test-tools"), "Enabled"), "advanced conversation settings expose the real safe tool test");
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
                var liveTransport = Get(connections, "transport");
                var discovery = new DiscoveryTransport();
                Set(connections, "transport", discovery);
                try
                {
                    void PollDiscovery() => NativeQuickItemChecks.Until(() => { Prepare(); return GetOptional(window, "modelRead") == null; });
                    string ModelId() => ((LlmConnectionDraft)Get(window, "modelDraft")).Model;
                    void Read(params LlmModelInfo[] models)
                    {
                        discovery.Next = Task.FromResult<IReadOnlyList<LlmModelInfo>>(models);
                        Click("model-discover"); PollDiscovery();
                    }
                    Read(new LlmModelInfo("text-one", "支持文字"), new LlmModelInfo("text-two", "支持文字"));
                    Require(Control("model-found-text-one") != null && Control("model-found-text-two") != null && ModelId() == "",
                        "getting multiple live models fills and opens the picker without choosing one");
                    Click("model-found-text-two");
                    Require(ModelId() == "text-two" && Control("model-view-models") != null, "selecting a model returns to the configuration and keeps the fetched list");
                    Click("model-view-models");
                    Require(Control("model-found-text-one") != null && Control("model-found-text-two") != null && discovery.Reads == 1,
                        "reopening a fetched list does not make another discovery request");
                    Click("model-return");
                    Read(new LlmModelInfo("only-text", "支持文字"));
                    Require(ModelId() == "text-two" && Control("model-found-only-text") != null, "a populated draft keeps its model when discovery returns one other model");
                    Click("model-return");
                    Read();
                    Require(ModelId() == "text-two" && Control("model-found-only-text") == null && Control("model-field-model-id") != null,
                        "an empty list keeps the entered model and stays on the form");
                    discovery.Next = Task.FromException<IReadOnlyList<LlmModelInfo>>(new LlmException(LlmFailure.DiscoveryUnavailable, "fixture failure"));
                    Click("model-discover"); PollDiscovery();
                    Require(ModelId() == "text-two" && Control("model-field-model-id") != null, "a failed lookup leaves the typed model and form available");
                    var late = new TaskCompletionSource<IReadOnlyList<LlmModelInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);
                    discovery.Next = late.Task;
                    Click("model-discover");
                    Pick("model-provider", "provider-custom");
                    late.SetResult(new[] { new LlmModelInfo("wrong-provider", "支持文字") });
                    PollDiscovery();
                    Require(Control("model-field-base-setup") != null && GetOptional(window, "discoveredModels") == null,
                        "changing connection identity prevents a late model list from reopening the picker");
                    Pick("model-provider", "provider-ollama");
                    Read(new LlmModelInfo("fixture-local-model", "支持文字"));
                    Require(ModelId() == "fixture-local-model" && Control("model-found-fixture-local-model") != null && discovery.Inference == 0,
                        "a single known text model fills an empty draft and opens its list without an inference request");
                    Click("model-return");
                }
                finally { Set(connections, "transport", liveTransport); }
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
