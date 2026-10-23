using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using JueMingR.Features.Blueprint;
using JueMingR.Features.Text;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // These are production CPU geometry and complete-name edit checks. They
    // perform no GPU draw, OS click, native IME operation or world/file write.
    internal static class NativeBlueprintLibraryUiChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static void Run(object context)
        {
            var host = Get(context, "Blueprint"); var assembly = host.GetType().Assembly;
            var layout = assembly.GetType("JueMingR.TerrariaHost.F5.BlueprintLibraryLayout", true);
            Func<string, object[], object> invoke = (name, args) => layout.GetMethod(name, F).Invoke(null, args);
            var cards = Enumerable.Range(0, 6).Select(i => invoke("Card", new object[] { 522f, i })).ToArray();
            for (int i = 0; i < 6; i++)
            {
                Require((float)Get(cards[i], "X") == (i % 2 == 0 ? 0 : 265) && (float)Get(cards[i], "Y") == 48 + i / 2 * 174,
                    "six cards are left-right then three rows after 42 header + 6 gap");
                Require((float)Get(cards[i], "Width") == 257 && (float)Get(cards[i], "Height") == 166, "card geometry follows the approved submenu, without resizing F5");
                var preview = invoke("Preview", new[] { cards[i] });
                Require((float)Get(preview, "Y") == (float)Get(cards[i], "Y") + 36 && (float)Get(preview, "Height") == 68, "preview uses the live submenu's 68-height container");
            }
            Require((float)invoke("Height", new object[] { 0 }) == 112 && (float)invoke("Height", new object[] { 6 }) == 568,
                "empty and six-card content heights use real rows without virtual cards");
            Require((Point)invoke("PreviewSize", new object[] { 522f, Matrix.Identity }) == new Point(231, 58) &&
                (Point)invoke("PreviewSize", new object[] { 522f, Matrix.CreateScale(1.5f) }) == new Point(346, 87),
                "preview worker budget comes from final card pixels at both real UI scales, not a fixed 256-column intermediate");
            float[] headerX = { 272, 342, 400, 458 };
            for (int i = 0; i < 4; i++) Require((float)Get(invoke("HeaderButton", new object[] { 522f, i }), "X") == headerX[i], "import/previous/next/back stay in their original order and positions");

            // Construct only this presentation over the isolated fixture Host.
            // BeginEdit does not acquire the native text lease; BeforeSample is
            // deliberately absent, so this does not claim Windows IME coverage.
            var state = Get(Get(context, "Shell"), "State");
            var shellRenderer = Get(Get(context, "Shell"), "renderer");
            Call(shellRenderer, "RefreshResources"); Call(shellRenderer, "Prepare", state, 960f, 640f, 1f);
            var uiType = assembly.GetType("JueMingR.TerrariaHost.F5.BlueprintLibraryPresentation", true);
            var ui = Activator.CreateInstance(uiType, F, null, new object[] { host, state, null }, null);
            var library = (LibraryState)Get(host, "Library"); var savedSnapshot = Get(library, "snapshot");
            try
            {
                ReadCancellation(ui, host, library);
                EditGeometry(ui, assembly);
                CapturePage(ui, host, library);
                BlueprintHints(context);
                HiddenKeyboardTail(ui, state);
                LegacyMissingUi(ui, host, library);
                Guid id = Guid.NewGuid(); Call(ui, "BeginEdit", id, "原名");
                var editor = (TextEditBuffer)Get(ui, "Editor"); editor.SelectAll(); editor.Insert(" ");
                Require(!(bool)Call(ui, "RequestFinish") && ReferenceEquals(editor, Get(ui, "Editor")) && editor.Text == " ",
                    "failed complete-name validation keeps the same edit buffer for correction");
                editor.SelectAll(); Require(editor.Insert(new string('中', 48)), "48-element paste remains complete");
                editor.MoveTo(editor.Text.Length); Require(!editor.Insert("外") && editor.Text == new string('中', 48), "over-limit edit preserves all prior text without truncation");
                ReceiptFailure(ui, library, editor, id, LibraryStatus.Failed, 901);
                Require(ReferenceEquals(editor, Get(ui, "Editor")) && (string)Get(ui, "validation") == "测试写入失败",
                    "an accepted rename failure preserves the identical edit draft/focus target");
                ReceiptFailure(ui, library, editor, id, LibraryStatus.CommitUnconfirmed, 902);
                Require(ReferenceEquals(editor, Get(ui, "Editor")) && (bool)Get(ui, "editProtected") && !(bool)Call(ui, "RequestFinish"),
                    "unknown commit preserves the draft and closes this editor's retry gate");
                RetiredCreate(ui, host, library);
                Call(ui, "CancelEdit"); Require(GetOptional(ui, "Editor") == null, "explicit cancel releases this editor only");
            }
            finally { Set(library, "snapshot", savedSnapshot); ((IDisposable)ui).Dispose(); }
            OpenAction(context);
        }
        private static void HiddenKeyboardTail(object ui, object state)
        {
            var text = Get(ui, "TextInput");
            var nativeIme = text.GetType().GetNestedType("NativeIme", BindingFlags.NonPublic);
            var patch = new HarmonyLib.Harmony("JueMingR.Tests.Blueprint.HiddenKeyboardTail");
            patch.Patch(nativeIme.GetMethod("Toggle", F), new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintLibraryUiChecks).GetMethod(nameof(ImeToggle), F)));
            patch.Patch(nativeIme.GetProperty("Composition", F).GetGetMethod(true), new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintLibraryUiChecks).GetMethod(nameof(ImeEmpty), F)));
            patch.Patch(nativeIme.GetProperty("Candidates", F).GetGetMethod(true), new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintLibraryUiChecks).GetMethod(nameof(ImeNoCandidates), F)));
            bool oldWriting = PlayerInput.WritingText, oldBlock = Main.blockInput;
            object oldOwner = Main.CurrentInputTextTakerOverride;
            var keyboard = typeof(PlayerInput).GetMethod("KeyboardInput", F);
            var oldChroma = Main.ChromaPainter;
            Main.ChromaPainter = new Terraria.GameContent.ChromaHotkeyPainter();
            try
            {
                foreach (bool switchPage in new[] { false, true })
                {
                    Main.CurrentInputTextTakerOverride = null; Main.blockInput = false; PlayerInput.WritingText = false;
                    Call(state, "RestoreVisible"); Call(state, "Navigate", 6); Call(ui, "OpenLibrary"); Call(ui, "BeginEdit", Guid.NewGuid(), "名称");
                    Call(ui, "BeforeInput", true);
                    Call(text, "AfterSample", true, null, new KeyboardState(Keys.A, Keys.LeftControl), true);
                    Require((bool)Get(text, "Owned"), "real name editor owns the keyboard lease before hide");
                    if (switchPage) Call(state, "Navigate", 0); else Call(state, "Close");
                    Func<bool, Keys[], bool> frame = (focused, keys) =>
                    {
                        // Exact native order: prefix owns WritingText, original
                        // KeyboardInput maps the physical cached keys, native
                        // UpdateInput resets WritingText, postfix feeds sample.
                        Call(ui, "BeforeInput", false); Main.keyState = new KeyboardState(keys); Main.oldKeyState = default(KeyboardState);
                        PlayerInput.Triggers.Reset(); PlayerInput.MouseKeys.Clear(); keyboard.Invoke(null, null);
                        bool moved = PlayerInput.Triggers.Current.Left || PlayerInput.Triggers.Current.Jump;
                        PlayerInput.WritingText = false;
                        Call(ui, "ProcessInput", false, new KeyboardState(keys), Vector2.Zero, false, focused, false);
                        return moved;
                    };
                    Require(!frame(true, new[] { Keys.A, Keys.LeftControl }), "held editor action cannot leak after hide/page change");
                    frame(false, Array.Empty<Keys>());
                    Require(!frame(true, new[] { Keys.A }), "unfocused neutral cannot retire a physical keyboard tail");
                    frame(true, Array.Empty<Keys>());
                    Require(frame(true, new[] { Keys.A, Keys.Space }), "trusted hidden neutral retires tail and new original movement/jump resumes: page=" + switchPage);
                    var foreign = new object(); Main.CurrentInputTextTakerOverride = foreign; PlayerInput.WritingText = true;
                    Call(ui, "BeforeInput", false); Call(ui, "ProcessInput", false, new KeyboardState(), Vector2.Zero, false, true, false);
                    Require(ReferenceEquals(Main.CurrentInputTextTakerOverride, foreign) && PlayerInput.WritingText, "retirement preserves another text owner's valid lease");
                    Main.CurrentInputTextTakerOverride = null; PlayerInput.WritingText = false; Call(ui, "Suspend");
                }
            }
            finally { ((IDisposable)ui).Dispose(); patch.UnpatchAll(patch.Id); Main.ChromaPainter = oldChroma; Main.CurrentInputTextTakerOverride = oldOwner; Main.blockInput = oldBlock; PlayerInput.WritingText = oldWriting; Call(state, "Navigate", 6); Call(state, "RestoreVisible"); }
        }
        private static bool ImeToggle(bool enabled) { PlayerInput.WritingText = enabled; return false; }
        private static bool ImeEmpty(ref string __result) { __result = ""; return false; }
        private static bool ImeNoCandidates(ref bool __result) { __result = false; return false; }
        private static void CapturePage(object ui, object host, LibraryState library)
        {
            var flow = (BlueprintCaptureFlow)Get(host, "Flow");
            var prior = typeof(BlueprintCaptureFlow).GetProperties().ToDictionary(p => p, p => p.GetValue(flow));
            var snapshot = Get(library, "snapshot"); var assembly = ui.GetType().Assembly;
            Type rect = assembly.GetType("JueMingR.TerrariaHost.F5.F5Rect", true);
            var layout = assembly.GetType("JueMingR.TerrariaHost.F5.BlueprintLibraryLayout", true);
            try
            {
                Set(library, "snapshot", Activator.CreateInstance(typeof(LibrarySnapshot), F, null, new object[] { 0L, false, null, null, 0L }, null));
                flow.Status = BlueprintCaptureStatus.DraftReady; flow.ContentHandle = new ContentHandle(Guid.NewGuid());
                flow.DraftName = "默认名可以直接保存"; flow.Summary = new CaptureSummary { Width = 300, Height = 200, ContentCount = 60000 };
                Call(ui, "ShowCapture");
                foreach (float height in new[] { 581f, 297f, 180f })
                {
                    Set(ui, "view", Activator.CreateInstance(rect, F, null, new object[] { 0f, 0f, 522f, height }, null));
                    Call(ui, "Build");
                    var parts = ((IEnumerable)Get(ui, "parts")).Cast<object>().ToArray();
                    var name = parts.Single(p => Get(p, "Command").ToString() == "Name");
                    var nameElement = Get(name, "Element"); var field = Get(nameElement, "Rect");
                    var image = parts.Single(p => Get(Get(p, "Element"), "Kind").ToString() == "Image");
                    var preview = Get(Get(image, "Element"), "Rect");
                    var save = parts.Single(p => Get(p, "Command").ToString() == "ConfirmDraft");
                    var saveRect = Get(Get(save, "Element"), "Rect");
                    Require((float)Get(field, "Width") == 506 && (float)Get(field, "Height") == 32 && (float)Get(nameElement, "TextScale") == .70f,
                        "production capture name uses the full safe-height normal-size field");
                    Require((float)Get(preview, "Width") == 506 && (float)Get(preview, "Height") == (height == 581 ? 350 : height == 297 ? 123 : 48),
                        "production capture gives preview the real remaining viewport, including the low 297 page");
                    Require((float)Get(saveRect, "Width") == 176 && (float)Get(saveRect, "Height") == 32 && (float)Get(saveRect, "Y") > (float)Get(preview, "Bottom"),
                        "production save is a dedicated large action below the image and summary");
                    Require(!parts.Any(p => (bool)Get(p, "Card")) && GetOptional(ui, "Editor") == null,
                        "capture workspace has no selected library card and default name needs no editing");
                    Point budget = (Point)layout.GetMethod("CapturePreviewSize", F).Invoke(null, new object[] { 522f, height, Matrix.CreateScale(1.5f) });
                    Require(budget.X <= 1024 && budget.Y <= 768 && (height != 581 || budget.Y > 128), "actual capture request uses the new purpose's physical container budget");
                    Call(ui, "Execute", name);
                    Require(((TextEditBuffer)Get(ui, "Editor")).Text == flow.DraftName, "one actual capture name command enters the shared text editor");
                    Call(ui, "Build"); Require((float)Get(Get(ui, "editRect"), "Width") == 506, "the real editRect follows the full capture field used by IME");
                    flow.Status = BlueprintCaptureStatus.Saving; Call(ui, "Build");
                    parts = ((IEnumerable)Get(ui, "parts")).Cast<object>().ToArray();
                    Require(!(bool)Get(ui, "Editable") && !(bool)Call(ui, "RequestFinish") &&
                        !parts.Any(p => Get(p, "Command").ToString() == "Cancel"), "accepted save freezes editing and exposes no cancelling write action");
                    flow.Status = BlueprintCaptureStatus.DraftReady; Call(ui, "CancelEdit");
                }
            }
            finally { Call(ui, "ReleasePresentation"); foreach (var item in prior) item.Key.SetValue(flow, item.Value); Set(library, "snapshot", snapshot); }
        }
        private static void BlueprintHints(object context)
        {
            var shell = Get(context, "Shell"); var state = Get(shell, "State"); var renderer = Get(shell, "renderer");
            var assembly = state.GetType().Assembly;
            Call(state, "Navigate", 6); Set(state, "Ready", true); Call(state, "RestoreVisible");
            Call(Get(state, "Layout"), "SetBlueprintLibraryVisible", false);
            Call(renderer, "Prepare", state, 1280f, 900f, 1f);
            var layout = Get(state, "Layout"); var view = Get(layout, "Viewport");
            var elements = ((IEnumerable)Get(layout, "Elements")).Cast<object>().ToArray();
            Func<object, string> hint = element =>
            {
                var area = GetOptional(element, "Description") != null ? Get(element, "HintRect") : Get(element, "Rect");
                var input = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.F5.F5Input", true));
                Set(input, "Active", true); Set(input, "Focused", true); Set(input, "Width", 1280f); Set(input, "Height", 900f); Set(input, "Scale", 1f);
                // Update first establishes this viewport's actual centered
                // shell position; the prior check used a different viewport.
                Call(state, "Update", input);
                Call(state, "ScrollTo", Math.Max(0f, (float)Get(area, "Y") - 20f));
                Set(input, "X", (float)Get(state, "X") + (float)Get(view, "X") + (float)Get(area, "X") + 3);
                Set(input, "Y", (float)Get(state, "Y") + (float)Get(view, "Y") + (float)Get(area, "Y") - (float)Get(state, "Scroll") + 3);
                Call(state, "Update", input);
                object[] args = { state, null, false, false, null, null };
                return (string)renderer.GetType().GetMethod("ResolveHint", F).Invoke(renderer, args);
            };
            foreach (string label in new[] { elements.Any(e => (string)GetOptional(e, "Text") == "采集蓝图") ? "采集蓝图" : "退出采集", "保存采集", "移动蓝图", "移除蓝图" })
            {
                var name = elements.First(e => (string)GetOptional(e, "Text") == label);
                Require(hint(name) == (string)Get(Get(name, "Description"), "Text"), "actual final F5 consumer resolves static description on " + label);
            }
            var controls = Get(renderer, "BlueprintControls");
            foreach (string command in new[] { "BlueprintCapture", "BlueprintSave", "BlueprintMove", "BlueprintRemove" })
            {
                var button = elements.First(e => Get(e, "Command").ToString() == command);
                bool enabled = (bool)Call(controls, "Available", Get(button, "Command"));
                Require(enabled ? hint(button) == null : !string.IsNullOrEmpty(hint(button)), "actual F5 text action removes duplicate only while enabled: " + command);
            }
            var icon = elements.First(e => (string)GetOptional(e, "HotkeyTarget") == "blueprint.save");
            Require(hint(icon) == "双击设置保存采集的快捷键", "actual icon consumer retains its purpose hint");
            Require(GetOptional(elements.First(e => (string)GetOptional(e, "Text") == "手持凝胶显示快捷栏"), "Description") == null,
                "gel display row retains the owner's no-description choice");
            Call(state, "Close");
        }
        private static void LegacyMissingUi(object ui, object host, LibraryState library)
        {
            const string historical = "箱内物品未采集；牌子或墓碑文字未采集；展示物品未采集";
            var saved = Get(library, "snapshot");
            var flow = (BlueprintCaptureFlow)Get(host, "Flow");
            var oldSummary = flow.Summary; var oldHandle = flow.ContentHandle; var oldStatus = flow.Status;
            try
            {
                var assembly = typeof(LibraryState).Assembly; var type = assembly.GetType("JueMingR.Features.Blueprint.LibraryMetadata", true);
                object metadata = Activator.CreateInstance(type, true); Guid id = Guid.NewGuid();
                Set(metadata, "AssetId", id); Set(metadata, "ContentRevision", Guid.NewGuid()); Set(metadata, "Name", "旧合法图");
                Set(metadata, "ContentHash", new string('A', 64)); Set(metadata, "Width", 2); Set(metadata, "Height", 2); Set(metadata, "ContentCount", 4L); Set(metadata, "MissingSummary", historical);
                var entry = (LibraryEntry)Activator.CreateInstance(typeof(LibraryEntry), F, null, new object[] { id, "metadata", metadata, false }, null);
                var page = (LibraryPage)Activator.CreateInstance(typeof(LibraryPage), F, null, new object[] { 2024L, 0, 1L, new[] { entry }, 0, false, null }, null);
                Set(library, "snapshot", Activator.CreateInstance(typeof(LibrarySnapshot), F, null, new object[] { 2024L, false, page, null, 0L }, null));
                Call(ui, "OpenLibrary"); Set(ui, "listGeneration", 2024L); Set(ui, "wantList", false); Set(ui, "selected", (Guid?)id);
                var rect = ui.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Rect", true);
                Set(ui, "view", Activator.CreateInstance(rect, F, null, new object[] { 0f, 0f, 522f, 580f }, null)); Call(ui, "Build");
                Action quiet = () => Require(!((IEnumerable)Get(ui, "parts")).Cast<object>().Any(p => ((string)Get(Get(p, "Element"), "Text")).Contains("未采集")), "actual cards/list/selected-bottom consume historical Missing quietly");
                quiet();
                var namePart = ((IEnumerable)Get(ui, "parts")).Cast<object>().Single(p => Get(p, "Command").ToString() == "Name");
                Set(ui, "lastClickName", false); Call(ui, "Execute", namePart);
                Require(GetOptional(ui, "Editor") == null, "one actual library name command selects without editing");
                Call(ui, "Execute", namePart);
                Require(((TextEditBuffer)Get(ui, "Editor")).Text == "旧合法图", "second actual library name command retains double-click rename");
                Call(ui, "CancelEdit");
                flow.Summary = new CaptureSummary { Width = 2, Height = 2, ContentCount = 4, MissingPayloads = historical.Split('；'), UnreliableObjectCount = 1 };
                flow.ContentHandle = new ContentHandle(Guid.NewGuid()); flow.Status = BlueprintCaptureStatus.DraftReady;
                Call(ui, "ShowCapture"); Call(ui, "Build"); quiet();
                Require(((IEnumerable)Get(ui, "parts")).Cast<object>().Any(p => ((string)Get(Get(p, "Element"), "Text")).Contains("对象尚未确认")), "real incomplete-group warning remains in the draft consumer");
            }
            finally { Call(ui, "Suspend"); Set(library, "snapshot", saved); flow.Summary = oldSummary; flow.ContentHandle = oldHandle; flow.Status = oldStatus; }
        }
        private static void ReceiptFailure(object ui, LibraryState library, TextEditBuffer editor, Guid id, LibraryStatus status, long command)
        {
            Set(ui, "pendingMutation", (LibraryCommandKind?)LibraryCommandKind.Rename); Set(ui, "pendingAsset", id);
            Set(ui, "pendingEditor", editor); Set(ui, "pendingEditRevision", editor.Revision); Set(ui, "mutationAfter", command - 1);
            var receipt = (LibraryReceipt)Activator.CreateInstance(typeof(LibraryReceipt), F, null,
                new object[] { command, LibraryCommandKind.Rename, status, Guid.Empty, id, default(CaptureToken), 0L, "测试写入失败", status == LibraryStatus.CommitUnconfirmed, false, null, null, null, null, null, null, false }, null);
            var snapshot = Activator.CreateInstance(typeof(LibrarySnapshot), F, null, new object[] { command, false, null, receipt, 0L }, null);
            Set(library, "snapshot", snapshot); Call(ui, "PollReceipts");
        }
        private static void EditGeometry(object ui, Assembly assembly)
        {
            Call(ui, "BeginEdit", Guid.Empty, new string('中', 48));
            var editor = (TextEditBuffer)Get(ui, "Editor"); editor.MoveTo(editor.Text.Length);
            var edit = Get(ui, "editView");
            Type size = assembly.GetType("JueMingR.TerrariaHost.F5.F5Size", true);
            Type measureType = typeof(Func<,,>).MakeGenericType(typeof(string), typeof(float), size);
            var measure = Delegate.CreateDelegate(measureType, ui, ui.GetType().GetMethod("Measure", F));
            long revision = editor.Revision;
            Call(edit, "Prepare", editor, "pin", 61f, measure);
            string first = (string)Get(edit, "Text");
            Call(edit, "Prepare", editor, "pinyin", 61f, measure);
            Require(editor.Revision == revision && first != (string)Get(edit, "Text") && ((string)Get(edit, "Text")).EndsWith("pinyin"),
                "same committed revision still projects changed composition at the visible trailing caret");
            Require((float)Get(edit, "Caret") <= 61 && (float)Get(edit, "Caret") >= 0 && ((string)Get(edit, "Text")).Length < editor.Text.Length,
                "long complete name uses a bounded whole-element viewport with its trailing caret visible");
            Type rect = assembly.GetType("JueMingR.TerrariaHost.F5.F5Rect", true);
            Func<float, float, float, float, object> box = (x, y, w, h) => Activator.CreateInstance(rect, F, null, new object[] { x, y, w, h }, null);
            Type renderer = assembly.GetType("JueMingR.TerrariaHost.F5.BlueprintLibraryRenderer", true);
            var anchor = renderer.GetMethod("TryImeAnchor", F);
            object[] args = { box(10, 20, 80, 22), 60f, 16f, box(0, 0, 200, 100), Matrix.CreateScale(1.5f), Matrix.Identity, Vector2.Zero };
            Require((bool)anchor.Invoke(null, args) && (Vector2)args[6] == new Vector2(112.5f, 90.5f),
                "IME anchor follows the frozen UI transform through screen pixels and verified native minus-32 offset");
            args[3] = box(0, 30, 200, 100);
            Require(!(bool)anchor.Invoke(null, args), "partially clipped caret has no candidate anchor eligibility");
            args[3] = box(0, 0, 200, 100); args[1] = 75f;
            Require(!(bool)anchor.Invoke(null, args), "caret outside the field cannot publish an old candidate anchor");
            Call(ui, "CancelEdit");
        }
        private static void RetiredCreate(object ui, object host, LibraryState library)
        {
            object priorMode = Get(ui, "mode");
            var flow = (BlueprintCaptureFlow)Get(host, "Flow");
            var priorFlow = typeof(BlueprintCaptureFlow).GetProperties().ToDictionary(property => property, property => property.GetValue(flow));
            long priorCaptureCommand = (long)Get(host, "captureCommandId");
            var input = Get(host, "Input"); var shell = Get(host, "Shell"); var state = Get(shell, "State"); var layout = Get(state, "Layout");
            var renderer = Get(shell, "renderer"); var selection = ((BlueprintCaptureState)Get(host, "State")).Selection;
            long selectionRevision = selection.Revision, acceptedCommand = library.AcceptedCommandId;
            try
            {
                foreach (var status in new[] { LibraryStatus.Failed, LibraryStatus.CommitUnconfirmed })
                {
                    // This is the real post-Create receipt boundary. Mechanical
                    // failure/protection is covered by the Host recovery checks;
                    // here actual Host retirement must feed the UI key consumer.
                    var token = new CaptureToken(Guid.NewGuid(), 7, 8, 1);
                    flow.Token = token; flow.ContentHandle = new ContentHandle(token.OperationId);
                    flow.Summary = new CaptureSummary { Width = 1, Height = 1, ContentCount = 1 };
                    flow.Status = BlueprintCaptureStatus.Saving; flow.DraftName = "失败时保留的名称";
                    Set(state, "Ready", true); Call(ui, "ShowCapture"); Call(ui, "BeginEdit", Guid.Empty, flow.DraftName);
                    var editor = Get(ui, "Editor");
                    long command = status == LibraryStatus.Failed ? 903 : 904;
                    // A Create outcome must also match the specific command
                    // accepted by Host, not just this operation's stable ID.
                    Set(host, "captureCommandId", command);
                    var receipt = (LibraryReceipt)Activator.CreateInstance(typeof(LibraryReceipt), F, null,
                        new object[] { command, LibraryCommandKind.Create, status, token.OperationId, token.OperationId, token, 0L, "故障材料保留", true, false, null, null, null, null, null, null, true }, null);
                    var snapshot = Activator.CreateInstance(typeof(LibrarySnapshot), F, null, new object[] { command, false, null, receipt, 0L }, null);
                    Call(host, "ApplyReceipt", receipt); Set(library, "snapshot", snapshot); Call(ui, "PollReceipts");
                    Require(flow.ContentHandle.IsEmpty && flow.Summary == null && flow.Status == (status == LibraryStatus.Failed ? BlueprintCaptureStatus.Failed : BlueprintCaptureStatus.CommitUnconfirmed),
                        "actual Host retires the field's handle and summary while preserving commit certainty");
                    string message = (string)Get(ui, "validation");
                    Require(ReferenceEquals(editor, Get(ui, "Editor")) && (bool)Get(ui, "editProtected") && !(bool)Call(ui, "RequestFinish"),
                        "retired create retains its buffer but has no invalid retry execution gate");
                    Require(status == LibraryStatus.Failed ? message.Contains("未保存") && !message.Contains("无法确认") : message.Contains("无法确认"),
                        "actual UI separates definite failed/protected create from unknown commit certainty");

                    // Prepare the production CPU layout only: no GPU/font asset
                    // validity or OS IME claim is needed for this Escape branch.
                    Call(layout, "SetBlueprintLibraryVisible", true); Call(renderer, "RefreshResources"); Call(renderer, "Prepare", state, 960f, 640f, 1f);
                    Action prepare = () =>
                    {
                        Set(ui, "view", Call(Get(layout, "Viewport"), "Offset", (float)Get(state, "X"), (float)Get(state, "Y")));
                        Set(ui, "matrix", Main.UIScaleMatrix); Set(ui, "layoutGeneration", Get(layout, "Generation"));
                        Call(ui, "Build"); Call(state, "ClampScroll"); Set(ui, "scroll", Get(state, "Scroll"));
                        Set(ui, "ready", true); Set(ui, "dirty", false);
                    };
                    Action<Keys[]> frame = keys =>
                    {
                        prepare(); PlayerInput.MouseInfo = new MouseState(); PlayerInput.Triggers.Reset();
                        Call(input, "BeginUpdate"); Call(ui, "BeforeInput", true); Call(input, "AfterMapping");
                        Main.keyState = new KeyboardState(keys); Call(input, "AfterKeyboardRefresh");
                        Call(ui, "ProcessInput", true, new KeyboardState(keys), Vector2.Zero, true, true, false);
                    };
                    frame(Array.Empty<Keys>());
                    Require(!(bool)Get(Get(ui, "TextInput"), "Owned") && (float)Get(Get(ui, "editRect"), "Width") == 0,
                        "next retired frame has no visible name field and no text lease");
                    frame(new[] { Keys.Escape });
                    Require(GetOptional(ui, "Editor") == null && (bool)Get(ui, "Visible") && (string)Get(ui, "validation") == message,
                        "first Escape must cancel the retired hidden name edit while keeping capture feedback visible: " + status);
                    Require((bool)Call(Get(input, "Hotkeys"), "IsSuppressed", (int)Keys.Escape), "retired edit cancellation owns the full Escape physical tail");
                    frame(new[] { Keys.Escape });
                    Require((bool)Get(ui, "Visible"), "held cancellation Escape cannot also navigate back");
                    frame(Array.Empty<Keys>()); frame(new[] { Keys.Escape });
                    Require(!(bool)Get(ui, "Submenu") && (bool)Get(state, "Visible"), "next independent Escape returns to the F5 blueprint menu");
                    Require(flow.Token.OperationId == token.OperationId && flow.ContentHandle.IsEmpty && flow.Summary == null && flow.DraftName == "失败时保留的名称" && selection.Revision == selectionRevision && library.AcceptedCommandId == acceptedCommand,
                        "Escape navigation cannot retry old ID, enqueue cleanup or consume the retained selection");
                    Require(library.Snapshot.LastReceipt.IsProtected && library.Snapshot.LastReceipt.OperationRetired, "Escape does not rewrite the protected retired receipt");
                }
            }
            finally
            {
                Call(ui, "Suspend"); foreach (var prior in priorFlow) prior.Key.SetValue(flow, prior.Value);
                Set(host, "captureCommandId", priorCaptureCommand);
                Set(ui, "mode", priorMode);
                Call(layout, "SetBlueprintLibraryVisible", (bool)Get(Get(shell, "BlueprintUi"), "Submenu"));
            }
        }
        private static void OpenAction(object context)
        {
            var host = Get(context, "Blueprint"); var shell = Get(context, "Shell"); var state = Get(shell, "State");
            var ui = Get(shell, "BlueprintUi"); var input = Get(context, "Input"); var renderer = Get(shell, "renderer");
            var bindings = (HotkeyBindings)Get(Get(shell, "hotkeys"), "Bindings");
            var registry = (HotkeyRegistry)Get(Get(shell, "hotkeys"), "Registry");
            Require(registry.Find("blueprint.library")?.CanConfigure == true && bindings.Get("blueprint.library") == null,
                "library open is a configurable stable action with no implicit default key");
            Call(state, "Navigate", 6); Set(state, "Ready", true); Call(state, "RestoreVisible");
            Call(renderer, "RefreshResources"); Call(renderer, "Prepare", state, 960f, 640f, 1f);
            Action<int, int, bool, Keys[]> sample = (x, y, left, keys) =>
            {
                PlayerInput.MouseInfo = new MouseState(x, y, 0, left ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                PlayerInput.Triggers.Reset(); PlayerInput.Triggers.Current.MouseLeft = left; PlayerInput.Triggers.Update(); Main.mouseLeft = left;
                Call(input, "BeginUpdate"); Call(input, "AfterMapping");
                // Match Main's actual refresh order: mapping suppression is
                // allowed to clear the old keyState, then native sampling
                // supplies this frame before the public recorder consumes it.
                Main.keyState = new KeyboardState(keys); Call(input, "AfterKeyboardRefresh");
            };
            sample(200, 200, false, Array.Empty<Keys>()); Call(shell, "ProcessInput");
            var layout = Get(state, "Layout");
            var key = ((IEnumerable)Get(layout, "Elements")).Cast<object>().Single(e => (string)GetOptional(e, "HotkeyTarget") == "blueprint.library");
            var rect = Get(key, "Rect"); var view = Get(layout, "Viewport");
            int px = (int)((float)Get(state, "X") + (float)Get(view, "X") + (float)Get(rect, "X") + (float)Get(rect, "Width") / 2);
            int py = (int)((float)Get(state, "Y") + (float)Get(view, "Y") + (float)Get(rect, "Y") + (float)Get(rect, "Height") / 2 - (float)Get(state, "Scroll"));
            for (int i = 0; i < 2; i++)
            { sample(px, py, true, Array.Empty<Keys>()); Call(shell, "ProcessInput"); sample(px, py, false, Array.Empty<Keys>()); Call(shell, "ProcessInput"); }
            var popup = Get(shell, "HotkeyPopup");
            Require((bool)Get(popup, "Visible") && (string)Get(popup, "Target") == "blueprint.library", "actual standard row double click opens the public library binding window");
            // The existing popup owns recording and the reliable hotkeys.json
            // write; exercise its real capture consumer with the shared sample.
            var measureType = typeof(Func<,,>).MakeGenericType(typeof(string), typeof(float), host.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Size", true));
            var measure = Delegate.CreateDelegate(measureType, ui, ui.GetType().GetMethod("Measure", F));
            Call(popup, "Prepare", 960f, 640f, Get(renderer, "FontIdentity"), measure, 0);
            var popupLayout = Get(popup, "Layout"); var commands = ((IEnumerable)Get(popupLayout, "Commands")).Cast<object>().ToArray();
            var button = ((IEnumerable)Get(popupLayout, "Buttons")).Cast<object>().ElementAt(Array.FindIndex(commands, c => c.ToString() == "Record"));
            var buttonRect = Get(button, "Rect"); var panel = Get(popupLayout, "Panel");
            float recordX = (float)Get(panel, "X") + (float)Get(buttonRect, "X") + 3, recordY = (float)Get(panel, "Y") + (float)Get(buttonRect, "Y") + 3;
            sample((int)recordX, (int)recordY, true, Array.Empty<Keys>()); Call(popup, "Process", true, 6, recordX, recordY, true, 0);
            sample((int)recordX, (int)recordY, false, Array.Empty<Keys>()); Call(popup, "Process", true, 6, recordX, recordY, true, 0);
            Require((bool)Get(popup, "Capturing"), "real public record button starts shared capture");
            sample(0, 0, false, new[] { Keys.F8 }); Call(popup, "Process", true, 6, 0f, 0f, true, 0);
            NativeQuickItemChecks.Until(() => { bindings.Poll(); return !bindings.Busy; });
            Require(bindings.CompletionSucceeded && bindings.Get("blueprint.library")?.Text == "F8", "public binder reliably saves and activates the open-library chord; feedback=" + Get(popup, "Status") + "; capturing=" + Get(popup, "Capturing") + "; effective=" + bindings.Get("blueprint.library")?.Text);
            Call(popup, "Close"); Call(shell, "CloseAndSubmitPosition");
            Action release = () => { sample(0, 0, false, Array.Empty<Keys>()); Call(shell, "ProcessInput"); };
            Action press = () => { release(); sample(0, 0, false, new[] { Keys.F8 }); Call(shell, "ProcessInput"); };
            press(); Require((bool)Get(ui, "Visible"), "F5-closed physical new edge executes the actual Host library outlet");
            Call(shell, "CloseAndSubmitPosition"); sample(0, 0, false, new[] { Keys.F8 }); Call(shell, "ProcessInput");
            Require(!(bool)Get(ui, "Submenu"), "held open key cannot reopen after close without a new edge");
            Main.drawingPlayerChat = true; press(); Require(!(bool)Get(ui, "Submenu"), "native text entry blocks library opening"); Main.drawingPlayerChat = false;
            Main.CurrentInputTextTakerOverride = new object(); press(); Require(!(bool)Get(ui, "Submenu"), "foreign IME/text owner blocks library opening"); Main.CurrentInputTextTakerOverride = null;
            // A recorder only exists with its public window and F5 visible.
            // Setting the input bit alone while F5 is closed is an impossible
            // state: the real shell correctly closes that stale lease first.
            Call(state, "RestoreVisible");
            var anchorRect = Call(rect, "Offset", (float)Get(state, "X") + (float)Get(view, "X"), (float)Get(state, "Y") + (float)Get(view, "Y") - (float)Get(state, "Scroll"));
            Call(shell, "OpenHotkey", "blueprint.library", anchorRect); Call(shell, "OpenHotkey", "blueprint.library", anchorRect);
            Call(popup, "Prepare", 960f, 640f, Get(renderer, "FontIdentity"), measure, 0);
            commands = ((IEnumerable)Get(popupLayout, "Commands")).Cast<object>().ToArray();
            button = ((IEnumerable)Get(popupLayout, "Buttons")).Cast<object>().ElementAt(Array.FindIndex(commands, c => c.ToString() == "Record"));
            buttonRect = Get(button, "Rect"); panel = Get(popupLayout, "Panel");
            recordX = (float)Get(panel, "X") + (float)Get(buttonRect, "X") + 3; recordY = (float)Get(panel, "Y") + (float)Get(buttonRect, "Y") + 3;
            sample((int)recordX, (int)recordY, true, Array.Empty<Keys>()); Call(popup, "Process", true, 6, recordX, recordY, true, 0);
            sample((int)recordX, (int)recordY, false, Array.Empty<Keys>()); Call(popup, "Process", true, 6, recordX, recordY, true, 0);
            Require((bool)Get(popup, "Capturing") && (bool)Get(popup, "Visible"), "second public record action establishes an actual capture window");
            press(); Require(!(bool)Get(ui, "Submenu"), "actual public binding capture consumes the new open key without opening library");
            Call(popup, "Close"); Call(shell, "CloseAndSubmitPosition"); NativeQuickItemChecks.Until(() => { bindings.Poll(); return !bindings.Busy; });
            Main.ingameOptionsWindow = true; press(); Require(!(bool)Get(ui, "Submenu"), "native modal blocks library opening"); Main.ingameOptionsWindow = false;
            press(); Require((bool)Get(ui, "Visible"), "new eligible edge recovers opening after text/modal gates");
            Call(shell, "CloseAndSubmitPosition"); release();
        }
        private static void ReadCancellation(object ui, object host, LibraryState library)
        {
            Call(ui, "OpenLibrary"); Call(ui, "RequestWork");
            long first = (long)Get(ui, "activeReadGeneration"); Require(first > 0, "actual presentation accepted its background list read");
            Call(ui, "RequestList", 1, null);
            var worker = Get(library, "worker");
            Require((long)Get(worker, "cancelledRead") == first && (long)Get(ui, "listGeneration") > first, "page change cancels the exact old accepted read and assigns a new shared generation");
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy; });
            Call(ui, "RequestWork"); long second = (long)Get(ui, "activeReadGeneration");
            Require(second > first, "new page read uses a distinct accepted generation");
            Call(ui, "Suspend");
            Require((long)Get(worker, "cancelledRead") == second, "return cancels only its current accepted read");
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy; });
        }
    }
}
