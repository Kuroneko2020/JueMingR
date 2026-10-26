using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBlueprintLibraryVisualChecks
    {
        internal static void DrawInput(object ui, ProbeGraphics graphics)
        { graphics.Render(() => Call(ui, "Draw"), Main.UIScaleMatrix); }
        internal static void Run(object context, ProbeGraphics graphics, string output)
        {
            var host = Get(context, "Blueprint"); var library = (LibraryState)Get(host, "Library");
            var shell = Get(context, "Shell"); var state = Get(shell, "State"); var layout = Get(state, "Layout");
            var renderer = Get(shell, "renderer"); var ui = Get(shell, "BlueprintUi");
            // Build representative entries through the real isolated repository,
            // rather than installing a synthetic page or writing the owner's data.
            for (int i = 0; i < 2; i++)
            {
                var token = new CaptureToken(Guid.NewGuid(), 7, 8, i + 1); var handle = new ContentHandle(token.OperationId);
                Require(library.TryBeginStage(token), "visual private stage accepted"); Complete(library);
                Require(library.TryAppendChunk(handle, new CaptureChunk(new[] { new CaptureCell { X = 60 + i, Y = 80, Layers = CaptureLayer.Wall | CaptureLayer.Wire, WallType = 2, Wires = 15 } }, 1)), "visual actual closed content accepted"); Complete(library);
                Require(library.TrySealDraft(handle, new CaptureSummary { Left = 60 + i, Top = 80, Width = 1, Height = 1, ContentCount = 1 }), "visual fixed draft sealed"); Complete(library);
                Require(library.TryCreate(handle, "蓝图库样本 " + (i + 1), token), "visual actual asset created"); Require(Complete(library).Status == LibraryStatus.Saved, "visual asset reliably published");
            }
            Capture(context, 51, 31);
            CaptureWorkspace(context, graphics, output);
            var flow = (BlueprintCaptureFlow)Get(host, "Flow");
            NativeQuickItemChecks.Until(() => { Step(context); Call(host, "Poll"); return flow.Status == BlueprintCaptureStatus.Saved; });
            Terraria.Localization.LanguageManager.Instance.SetLanguage("zh-Hans");
            Main.screenWidth = 1280; Main.screenHeight = 900;
            foreach (float scale in new[] { 1f, 1.5f })
            {
                Main.UIScale = scale; PlayerInput.CacheOriginalScreenDimensions();
                Call(ui, "OpenLibrary"); Call(layout, "SetBlueprintLibraryVisible", true);
                Call(renderer, "RefreshResources"); Call(renderer, "Prepare", state, 1280f, 900f, scale);
                NativeQuickItemChecks.Until(() =>
                {
                    Call(host, "Poll"); Call(ui, "Prepare", true, Main.UIScaleMatrix, new Vector2(1280, 900));
                    return library.Snapshot.Page != null && library.Snapshot.Page.TotalCount == 6 && !library.Snapshot.Busy && (int)Get(ui, "PreviewCount") >= Math.Min(4, library.Snapshot.Page.Entries.Count);
                });
                string suffix = scale.ToString(System.Globalization.CultureInfo.InvariantCulture);
                Action draw = () => { Call(renderer, "Draw", state, Main.UIScaleMatrix, false, false); Call(ui, "Draw"); };
                var pixels = graphics.Pixels(draw, Main.UIScaleMatrix, 1280, 900);
                Require(pixels.Any(c => c.A > 0 && c.R > 100 && c.G > 100), "library actual native resource draw produces visible pixels at UI " + suffix);
                graphics.Image(Path.Combine(output, "blueprint-library-" + suffix + ".png"), draw, Main.UIScaleMatrix, 1280, 900);
                var page = library.Snapshot.Page;
                Call(ui, "BeginEdit", page.Entries[0].AssetId, "中文名称编辑");
                Call(ui, "Prepare", true, Main.UIScaleMatrix, new Vector2(1280, 900));
                var textInput = Get(ui, "TextInput"); var editor = (JueMingR.Features.Text.TextEditBuffer)Get(ui, "Editor");
                editor.MoveTo(editor.Text.Length); Call(ui, "Prepare", true, Main.UIScaleMatrix, new Vector2(1280, 900));
                long revision = editor.Revision, builds = (long)Get(ui, "LayoutBuilds");
                Set(textInput, "Composition", "pin"); Call(ui, "Prepare", true, Main.UIScaleMatrix, new Vector2(1280, 900));
                string firstComposition = (string)Get(Get(ui, "editView"), "Text");
                Set(textInput, "Composition", "pinyin"); Call(ui, "Prepare", true, Main.UIScaleMatrix, new Vector2(1280, 900));
                Require(editor.Revision == revision && (long)Get(ui, "LayoutBuilds") == builds && firstComposition != (string)Get(Get(ui, "editView"), "Text") && ((string)Get(Get(ui, "editView"), "Text")).EndsWith("pinyin"),
                    "actual production Prepare refreshes changed composition without committed revision/layout changes");
                // The actual consumer requires the current editor's real lease;
                // only the OS IME service is substituted in this geometry scope.
                object priorTextOwner = Main.CurrentInputTextTakerOverride;
                using (NativeBlueprintInputContinuityChecks.Lease(ui))
                try
                {
                    graphics.Image(Path.Combine(output, "blueprint-library-name-" + suffix + ".png"), draw, Main.UIScaleMatrix, 1280, 900);
                    Require(GetOptional(Get(ui, "renderer"), "LastImeAnchor") != null, "actual visible editing renderer submits this frame's candidate anchor at " + suffix);
                }
                finally { Call(textInput, "Release", true); Main.CurrentInputTextTakerOverride = priorTextOwner; }
                Call(ui, "CancelEdit"); Call(shell, "CloseAndSubmitPosition");
            }
            Main.UIScale = 1f; PlayerInput.CacheOriginalScreenDimensions();
            ScrollRecovery(host, shell, state, renderer, ui, library);
            Main.tile[30, 30].active(false); Main.tile[32, 31].wall = 2;
            Capture(context, 32, 31, 31);
            Require(flow.Summary.UnreliableObjectCount == 1, "draft visual contains a real incomplete object with confirmed wall");
            Call(layout, "SetBlueprintLibraryVisible", true); Call(renderer, "RefreshResources"); Call(renderer, "Prepare", state, 1280f, 900f, 1f);
            PreviewPairing(host, ui, library, flow);
            graphics.Image(Path.Combine(output, "blueprint-draft-unconfirmed.png"), () => { Call(renderer, "Draw", state, Main.UIScaleMatrix, false, false); Call(ui, "Draw"); }, Main.UIScaleMatrix, 1280, 900);
            Call(host, "CancelCaptureOrDraft");
            NativeQuickItemChecks.Until(() => { Step(context); Call(host, "Poll"); return !library.Snapshot.Busy && !(bool)Get(host, "discardPending"); });
            Main.tile[32, 31].wall = 0; Capture(context, 32, 31, 31); Call(host, "SetExcludeUnreliable", true);
            Call(layout, "SetBlueprintLibraryVisible", true); Call(renderer, "RefreshResources"); Call(renderer, "Prepare", state, 1280f, 900f, 1f);
            var failures = (IDictionary<string, CaptureSummary>)Get(ui, "failedSummaries"); CaptureSummary empty = null;
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); Call(ui, "Prepare", true, Main.UIScaleMatrix, new Vector2(1280, 900)); return !library.Snapshot.Busy && failures.TryGetValue((string)Call(ui, "DraftKey"), out empty); });
            Require(empty.ContentCount == 0 && flow.Summary.ContentCount == 2, "all-excluded empty projection retains its keyed zero summary without mutating the original draft");
            foreach (object part in (IEnumerable)Get(ui, "parts")) if (Get(part, "Command").ToString() == "ConfirmDraft") Require(!(bool)Get(part, "Enabled"), "empty filtered draft cannot be confirmed");
            graphics.Image(Path.Combine(output, "blueprint-draft-empty-excluded.png"), () => { Call(renderer, "Draw", state, Main.UIScaleMatrix, false, false); Call(ui, "Draw"); }, Main.UIScaleMatrix, 1280, 900);
            Call(shell, "CloseAndSubmitPosition"); NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy; });
            Require((int)Get(ui, "PreviewCount") == 0 && (long)Get(ui, "PreviewBytes") == 0, "UI exit disposes its finite preview textures");
            FinalRendererPixels(context, graphics, output);
            Console.WriteLine("PASS Blueprint actual library cards/content previews/disabled operations/Chinese name buffer at 100/150% and resource exit. Offscreen native assets; OS IME/owner/multiplayer NOT_EXECUTED.");
        }
        private static void CaptureWorkspace(object context, ProbeGraphics graphics, string output)
        {
            var host = Get(context, "Blueprint"); var shell = Get(context, "Shell"); var state = Get(shell, "State");
            var ui = Get(shell, "BlueprintUi"); var renderer = Get(shell, "renderer"); var layout = Get(state, "Layout");
            var library = (LibraryState)Get(host, "Library"); var flow = (BlueprintCaptureFlow)Get(host, "Flow");
            var cache = Get(ui, "previews"); string defaultName = flow.DraftName;
            foreach (var dimensions in new[] { new[] { 900f, 1f }, new[] { 720f, 1.5f } })
            {
                Main.screenWidth = 1280; Main.screenHeight = (int)dimensions[0]; Main.UIScale = dimensions[1]; PlayerInput.CacheOriginalScreenDimensions();
                Call(layout, "SetBlueprintLibraryVisible", true); Call(renderer, "RefreshResources"); Call(renderer, "Prepare", state, 1280f, dimensions[0], dimensions[1]);
                Action prepare = () => { Call(host, "Poll"); Call(ui, "Prepare", true, Main.UIScaleMatrix, new Vector2(1280, dimensions[0])); };
                NativeQuickItemChecks.Until(() => { prepare(); return !library.Snapshot.Busy && Call(cache, "Summary", Call(ui, "DraftKey")) != null; });
                var size = (Point)Get(ui, "previewSize");
                Require(size.Y > 128 && size.X <= 1024 && size.Y <= 768, "real stage request exceeds the old 128 cap while staying inside the actual purpose budget");
                var texture = Call(cache, "Get", Call(ui, "DraftKey"));
                Require((int)Get(texture, "Height") > 128 && (long)Get(ui, "PreviewBytes") <= 8L * 1024 * 1024,
                    "actual worker reply and uploaded draft use the extended resolution under retained byte budget");
                var parts = ((IEnumerable)Get(ui, "parts")).Cast<object>().ToArray();
                Console.WriteLine("CAPTURE WORKSPACE requestedLayoutScale=" + dimensions[1] + " actualMatrix=" + Main.UIScaleMatrix.M11 + "/" + Main.UIScaleMatrix.M22 +
                    " header=" + Get(ui, "captureHeaderHeight") + " viewport=" + Get(Get(ui, "view"), "Width") + "x" + Get(Get(ui, "view"), "Height"));
                ConfirmEligibility(ui, host, library, prepare);
                var name = parts.Single(p => Get(p, "Command").ToString() == "Name");
                Call(ui, "Execute", name); prepare();
                var editor = (JueMingR.Features.Text.TextEditBuffer)Get(ui, "Editor");
                Require(editor.Text == defaultName && (float)Get(Get(ui, "editRect"), "X") == 104 && (float)Get(Get(ui, "editRect"), "Width") == 410, "one production name command routes only to the right shared editor rect");
                editor.MoveTo(editor.Text.Length);
                var input = Get(ui, "TextInput"); Set(input, "Composition", "pinyin"); prepare();
                Require(((string)Get(Get(ui, "editView"), "Text")).EndsWith("pinyin"), "real capture input preview consumes IME composition");
                long uploads = (long)Get(cache, "Uploads"), command = library.AcceptedCommandId;
                for (int i = 0; i < 8; i++) { editor.MoveTo(i % 2 == 0 ? 0 : editor.Text.Length); Call(state, "ScrollTo", i % 2); prepare(); }
                Require((long)Get(cache, "Uploads") == uploads && library.AcceptedCommandId == command,
                    "name/caret/scroll changes do not queue preview reads or repeat uploads");
                editor.MoveTo(editor.Text.Length); Call(state, "ScrollTo", 0); prepare();
                object oldOwner = Main.CurrentInputTextTakerOverride;
                using (NativeBlueprintInputContinuityChecks.Lease(ui))
                try
                {
                    Action draw = () => { Call(renderer, "Draw", state, Main.UIScaleMatrix, false, false); Call(ui, "Draw"); };
                    graphics.Image(Path.Combine(output, "blueprint-capture-workspace-" + dimensions[1].ToString(System.Globalization.CultureInfo.InvariantCulture) + ".png"), draw, Main.UIScaleMatrix, 1280, (int)dimensions[0]);
                    Require(GetOptional(Get(ui, "renderer"), "LastImeAnchor") != null, "actual capture Draw publishes the candidate anchor from its full field");
                }
                finally { Call(input, "Release", true); Main.CurrentInputTextTakerOverride = oldOwner; Call(ui, "CancelEdit"); }
            }
            // No edit is needed: execute the real enabled action with the
            // generated default name and let Host own the accepted Create.
            Call(ui, "Prepare", true, Main.UIScaleMatrix, new Vector2(1280, 720));
            var save = ((IEnumerable)Get(ui, "parts")).Cast<object>().Single(p => Get(p, "Command").ToString() == "ConfirmDraft");
            Require((bool)Get(save, "Enabled") && GetOptional(ui, "Editor") == null && flow.DraftName == defaultName, "ready default-name save needs no synthetic editor");
            Call(ui, "BeginEdit", Guid.Empty, defaultName);
            long beforeEnter = library.AcceptedCommandId;
            Require(!(bool)Call(ui, "RequestFinish") && flow.Status == BlueprintCaptureStatus.Saving && library.AcceptedCommandId > beforeEnter,
                "valid current draft Enter still commits its stable name and reaches the actual Create boundary");
            long acceptedEnter = library.AcceptedCommandId; Call(ui, "RequestFinish");
            Require(library.AcceptedCommandId == acceptedEnter, "same draft Enter during Saving cannot create a second asset");
            Call(ui, "Suspend");
            Require(flow.Status == BlueprintCaptureStatus.Saving, "closing capture presentation cannot revoke the accepted write");
            Main.UIScale = 1f; PlayerInput.CacheOriginalScreenDimensions();
        }
        private static void ConfirmEligibility(object ui, object host, LibraryState library, Action prepare)
        {
            var flow = (BlueprintCaptureFlow)Get(host, "Flow");
            var cache = Get(ui, "previews"); long command = library.AcceptedCommandId;
            Action<string> reject = expected =>
            {
                // Both real submit callers must recheck now, even if the old
                // button object was enabled before this state transition.
                string reason = (string)Call(ui, "GetDraftSaveEligibility");
                Require(reason != null && reason.Contains(expected), "current eligibility gives its actual reason: " + expected + " / " + reason);
                var save = ((IEnumerable)Get(ui, "parts")).Cast<object>().Single(p => Get(p, "Command").ToString() == "ConfirmDraft");
                Call(ui, "Execute", save);
                bool protectedEdit = (bool)Get(ui, "editProtected");
                Call(ui, "BeginEdit", Guid.Empty, flow.DraftName); Set(ui, "editProtected", protectedEdit); Call(ui, "RequestFinish");
                Require(library.AcceptedCommandId == command && flow.Status != BlueprintCaptureStatus.Saving,
                    "stale button and name Enter cannot Create without " + expected);
                Call(ui, "CancelEdit");
            };
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            object snapshot = Get(library, "snapshot");
            try
            {
                Set(library, "snapshot", Activator.CreateInstance(typeof(LibrarySnapshot), flags, null, new object[] { 0L, true, null, null, 0L }, null));
                reject("处理");
                Call(ui, "BeginEdit", Guid.Empty, " ");
                string nameReason = (string)Call(ui, "GetDraftSaveEligibility");
                Call(ui, "RequestFinish");
                Require((string)Get(ui, "validation") == nameReason && library.AcceptedCommandId == command,
                    "invalid name during busy has the identical current button/Enter reason and cannot Create");
                Call(ui, "CancelEdit");
            }
            finally { Set(library, "snapshot", snapshot); }
            Set(ui, "editProtected", true); reject("保护"); Set(ui, "editProtected", false);
            flow.Status = BlueprintCaptureStatus.Sealing; reject("尚未完成"); flow.Status = BlueprintCaptureStatus.DraftReady;
            var original = flow.Summary;
            flow.Summary = new CaptureSummary { Width = original.Width, Height = original.Height, ContentCount = original.ContentCount, UnreliableObjectCount = 1 };
            flow.ExcludedObjectIds = new[] { 123456 }; reject("预览");
            // A same-key filtered summary still records any remaining unreliable
            // objects. The original worker remains final storage authority.
            var builder = new BlueprintPreviewBuilder(1, 1, 1, 1); builder.Add(0, 0, BlueprintPreviewLayer.Tile);
            Call(cache, "Upload", Call(ui, "DraftKey"), builder.Complete(), new CaptureSummary { ContentCount = 1, UnreliableObjectCount = 1 }, Get(cache, "Generation"));
            reject("待排除");
            flow.ExcludedObjectIds = Array.Empty<int>(); flow.Summary = original;
            float priorScale = Main.UIScale; Main.UIScale = 1.25f; reject("预览"); Main.UIScale = priorScale;
            int priorHeight = Main.screenHeight; Main.screenHeight += 30; PlayerInput.CacheOriginalScreenDimensions(); reject("预览");
            Main.screenHeight = priorHeight; PlayerInput.CacheOriginalScreenDimensions();
            Call(cache, "Reset", null, EventArgs.Empty); reject("预览");
            NativeQuickItemChecks.Until(() => { prepare(); return !library.Snapshot.Busy && Call(ui, "GetDraftSaveEligibility") == null; });
            Require(library.AcceptedCommandId >= command, "after reset the actual worker is permitted to regenerate the current preview");
        }
        private static void FinalRendererPixels(object context, ProbeGraphics graphics, string output)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var host = Get(context, "Blueprint"); var state = Get(Get(context, "Shell"), "State"); var assembly = host.GetType().Assembly;
            var type = assembly.GetType("JueMingR.TerrariaHost.F5.BlueprintLibraryPresentation", true);
            var ui = Activator.CreateInstance(type, flags, null, new[] { host, state, null }, null);
            var cache = Get(ui, "previews"); var renderer = Get(ui, "renderer"); var parts = (IList)Get(ui, "parts");
            var rectType = assembly.GetType("JueMingR.TerrariaHost.F5.F5Rect", true);
            Func<float, float, float, float, object> rect = (x, y, w, h) => Activator.CreateInstance(rectType, flags, null, new object[] { x, y, w, h }, null);
            var kind = Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.F5.F5ElementKind", true), "Image");
            var viewport = rect(0, 0, 960, 640); var imageRect = rect(100, 100, 241, 68);
            Call(renderer, "Refresh"); Call(cache, "Observe", graphics.GraphicsDevice);
            try
            {
                // Pick a source column the old 256→231 nearest pass omitted.
                var oldColumns = new HashSet<int>(Enumerable.Range(0, 231).Select(x => (int)((x + .5) * 256 / 231)));
                int skipped = Enumerable.Range(1, 254).First(x => !oldColumns.Contains(x));
                foreach (float scale in new[] { 1f, 1.5f })
                {
                    Matrix transform = Matrix.CreateScale(scale);
                    var budget = (Point)assembly.GetType("JueMingR.TerrariaHost.F5.BlueprintLibraryLayout", true).GetMethod("PreviewSize", flags).Invoke(null, new object[] { 522f, transform });
                    var builder = new BlueprintPreviewBuilder(256, 58, budget.X, budget.Y);
                    builder.Add(skipped, 2, BlueprintPreviewLayer.Tile); builder.Add(255, 57, BlueprintPreviewLayer.Object);
                    for (int y = 4; y < 54; y++) builder.Add(170, y, BlueprintPreviewLayer.Wire);
                    for (int y = 12; y <= 40; y++) for (int x = 50; x <= 90; x++)
                        if (x == 50 || x == 90 || y == 12 || y == 40) builder.Add(x, y, BlueprintPreviewLayer.Tile);
                    var image = builder.Complete(); string key = "final-pixel-" + scale;
                    Require((bool)Call(cache, "Upload", key, image, new CaptureSummary { Width = 256, Height = 58, ContentCount = image.ProcessedCells }, Get(cache, "Generation")), "finite renderer sample uploaded");
                    parts.Clear(); var part = Call(ui, "TextPart", "", imageRect, .62f, kind);
                    Action draw = () => Call(renderer, "Draw", Get(ui, "parts"), viewport, transform, 0f, null, null, cache, Get(ui, "editView"), false, 0f);
                    var baseline = graphics.Pixels(draw, transform);
                    Set(part, "PreviewKey", key); var actual = graphics.Pixels(draw, transform);
                    var target = (Rectangle)renderer.GetType().GetMethod("PreviewTarget", flags).Invoke(null, new object[] { imageRect, transform, image.Width, image.Height });
                    Require(target.Width >= image.Width && target.Height >= image.Height, "final renderer never reduces the already aggregated image");
                    int occupied = 0, transparent = 0;
                    for (int y = 0; y < image.Height; y++) for (int x = 0; x < image.Width; x++)
                    {
                        int px = target.X + x, py = target.Y + y, offset = py * 960 + px;
                        bool changed = actual[offset] != baseline[offset];
                        if (image.GetPixel(x, y) != 0) { occupied++; Require(changed, "actual final pixels retain every finite occupied column, thin line and remote component at " + scale + ": " + x + "," + y); }
                        else { transparent++; Require(!changed, "actual final pixels retain holes and separation at " + scale + ": " + x + "," + y); }
                    }
                    Require(occupied > 50 && transparent > occupied, "sparse final-renderer counterexample contains both meaningful occupied pixels and internal/outer empty pixels");
                    graphics.Image(Path.Combine(output, "blueprint-final-sparse-" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".png"), draw, transform);
                }
                // The same key owns both facts and pixels; reset invalidates an
                // accepted old-generation upload before it can become current.
                int generation = (int)Get(cache, "Generation");
                var small = new BlueprintPreviewBuilder(1, 1, 1, 1); small.Add(0, 0, BlueprintPreviewLayer.Tile); var one = small.Complete();
                Call(cache, "Reset", null, EventArgs.Empty);
                Require((int)Get(cache, "Count") == 0 && (long)Get(cache, "PixelBytes") == 0 && !(bool)Call(cache, "Upload", "late-device", one, new CaptureSummary { ContentCount = 1 }, generation),
                    "device generation retires all derived resources and rejects late old-generation upload");
                for (int i = 0; i < 18; i++) Call(cache, "Upload", "bounded-" + i, one, new CaptureSummary { ContentCount = i }, Get(cache, "Generation"));
                Require((int)Get(cache, "Count") == 12 && Call(cache, "Summary", "bounded-0") == null,
                    "texture and summary eviction share the twelve-entry bound");
                var largeBuilder = new BlueprintPreviewBuilder(1024, 768, 1024, 768, true);
                largeBuilder.Add(1023, 767, BlueprintPreviewLayer.Tile); var large = largeBuilder.Complete();
                for (int i = 0; i < 3; i++) Call(cache, "Upload", "bytes-" + i, large, new CaptureSummary { ContentCount = i }, Get(cache, "Generation"));
                Require((long)Get(cache, "PixelBytes") <= 8L * 1024 * 1024 && Call(cache, "Summary", "bytes-0") == null && Call(cache, "Get", "bytes-0") == null,
                    "large retained textures enforce the independent eight MiB byte bound and evict paired facts together");
                string owner = "d:" + Guid.NewGuid().ToString("N");
                string draftA = owner + ":False::1:1024:768:g" + Get(cache, "Generation");
                string draftB = owner + ":True::1:1024:768:g" + Get(cache, "Generation");
                string resized = owner + ":False::1:64:64:g" + Get(cache, "Generation");
                Call(cache, "Upload", draftA, large, new CaptureSummary { ContentCount = 7 }, Get(cache, "Generation"));
                Call(cache, "Upload", draftB, large, new CaptureSummary { ContentCount = 8 }, Get(cache, "Generation"));
                Require(Call(cache, "Summary", draftA) != null && Call(cache, "Summary", draftB) != null, "current-size draft exclusion variants retain correctly paired A-B-A facts");
                Call(cache, "Upload", resized, one, new CaptureSummary { ContentCount = 9 }, Get(cache, "Generation"));
                Require(Call(cache, "Summary", draftA) == null && Call(cache, "Summary", draftB) == null && Call(cache, "Summary", resized) != null,
                    "completed resize retires historical large draft image and summary lifetimes together");
                SpriteBatchRecovery(renderer, ui, cache, viewport, graphics);
            }
            finally { ((IDisposable)ui).Dispose(); }
            Require((int)Get(cache, "Count") == 0 && (long)Get(cache, "PixelBytes") == 0, "renderer counterexample exit releases every owned texture");
            Console.WriteLine("PASS Blueprint final production renderer sparse pixels/old skipped column/hole at 100/150%; paired cache cap/reset and actual batch state recovery.");
        }
        private static void ScrollRecovery(object host, object shell, object state, object renderer, object ui, LibraryState library)
        {
            var layout = Get(state, "Layout"); Main.screenWidth = 1280; Main.screenHeight = 520; PlayerInput.CacheOriginalScreenDimensions();
            Call(ui, "OpenLibrary"); Call(layout, "SetBlueprintLibraryVisible", true); Call(renderer, "Prepare", state, 1280f, 520f, 1f);
            Action prepare = () => { Call(host, "Poll"); Call(ui, "Prepare", true, Main.UIScaleMatrix, new Vector2(1280, Main.screenHeight)); };
            NativeQuickItemChecks.Until(() => { prepare(); return !library.Snapshot.Busy && library.Snapshot.Page != null && !(bool)Get(ui, "wantList"); });
            Call(state, "ScrollTo", float.MaxValue); prepare(); float before = (float)Get(state, "Scroll");
            Require(before > 0, "short viewport has a real nonzero library scroll range");
            Call(ui, "RequestList", 0, null); prepare();
            Require((float)Get(state, "Scroll") == before, "temporary loading height retains the last effective content height and exact scroll");
            NativeQuickItemChecks.Until(() => { prepare(); return !library.Snapshot.Busy && !(bool)Get(ui, "wantList"); });
            var first = ((IEnumerable)Get(ui, "parts")).Cast<object>().First(); Set(ui, "armed", first);
            Main.screenHeight = 900; PlayerInput.CacheOriginalScreenDimensions(); Call(renderer, "Prepare", state, 1280f, 900f, 1f); prepare();
            Require((float)Get(state, "Scroll") <= (float)Get(layout, "MaxScroll") && (float)Get(ui, "scroll") == (float)Get(state, "Scroll") && GetOptional(ui, "armed") == null,
                "larger final viewport clamps once, reprojects the same scroll and retires old press geometry");
            Call(ui, "CancelRead"); NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy; });
            var entries = library.Snapshot.Page.Entries.Take(Math.Max(0, library.Snapshot.Page.Entries.Count - 2)).ToArray();
            foreach (var entry in entries) { Require(library.TryDelete(entry.AssetId, entry.MetadataIdentity), "isolated actual row-reduction delete accepted"); Complete(library); }
            Call(ui, "RequestList", 0, null);
            NativeQuickItemChecks.Until(() => { prepare(); return !library.Snapshot.Busy && !(bool)Get(ui, "wantList"); });
            Require(library.Snapshot.Page.Entries.Count == 2 && (float)Get(state, "Scroll") <= (float)Get(layout, "MaxScroll") && (float)Get(layout, "ContentHeight") < 300,
                "actual deleted rows reduce final height and clamp the old offset to the new valid range");
            Call(shell, "CloseAndSubmitPosition"); NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy; });
        }
        private static void SpriteBatchRecovery(object renderer, object ui, object cache, object viewport, ProbeGraphics graphics)
        {
            var parts = (IList)Get(ui, "parts"); parts.Clear();
            var bad = Activator.CreateInstance(renderer.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.BlueprintLibraryPart", true), true); parts.Add(bad);
            bool failed = false;
            graphics.Pixels(() =>
            {
                var device = graphics.GraphicsDevice;
                device.ScissorRectangle = new Rectangle(9, 11, 700, 500);
                device.RasterizerState = Microsoft.Xna.Framework.Graphics.RasterizerState.CullClockwise;
                device.BlendState = Microsoft.Xna.Framework.Graphics.BlendState.Additive;
                device.DepthStencilState = Microsoft.Xna.Framework.Graphics.DepthStencilState.DepthRead;
                device.SamplerStates[0] = Microsoft.Xna.Framework.Graphics.SamplerState.LinearWrap;
                var scissor = device.ScissorRectangle; var rasterizer = device.RasterizerState;
                var blend = device.BlendState; var depth = device.DepthStencilState; var sampler = device.SamplerStates[0];
                try { Call(renderer, "Draw", Get(ui, "parts"), viewport, Matrix.Identity, 0f, null, null, cache, Get(ui, "editView"), false, 0f); }
                catch (TargetInvocationException error) when (error.InnerException is NullReferenceException) { failed = true; }
                Require(device.ScissorRectangle == scissor && ReferenceEquals(device.RasterizerState, rasterizer) && ReferenceEquals(device.BlendState, blend) && ReferenceEquals(device.DepthStencilState, depth) && ReferenceEquals(device.SamplerStates[0], sampler),
                    "exceptional library pass returns actual caller scissor/rasterizer/blend/depth/sampler");
            }, Matrix.Identity);
            Require(failed, "exceptional path was actually executed"); parts.Clear();
            graphics.Pixels(() =>
            {
                var device = graphics.GraphicsDevice; var scissor = device.ScissorRectangle; var rasterizer = device.RasterizerState;
                var blend = device.BlendState; var depth = device.DepthStencilState; var sampler = device.SamplerStates[0];
                Call(renderer, "Draw", Get(ui, "parts"), viewport, Matrix.Identity, 0f, null, null, cache, Get(ui, "editView"), false, 0f);
                Require(device.ScissorRectangle == scissor && ReferenceEquals(device.RasterizerState, rasterizer) && ReferenceEquals(device.BlendState, blend) && ReferenceEquals(device.DepthStencilState, depth) && ReferenceEquals(device.SamplerStates[0], sampler),
                    "normal library passes also return the actual caller states and a usable begun UI batch");
            }, Matrix.Identity);
        }
        private static LibraryReceipt Complete(LibraryState library)
        {
            LibraryReceipt result = null;
            NativeQuickItemChecks.Until(() => { library.Pump(); return library.TryTakeReceipt(out result); });
            Require(result.Status == LibraryStatus.Completed || result.Status == LibraryStatus.Saved || result.Status == LibraryStatus.Deleted, "visual private repository operation: " + result.Error); return result;
        }
        private static void PreviewPairing(object host, object ui, LibraryState library, BlueprintCaptureFlow flow)
        {
            var cache = Get(ui, "previews"); var original = flow.Summary;
            Func<CaptureSummary> summary = () => (CaptureSummary)Call(cache, "Summary", Call(ui, "DraftKey"));
            Action prepare = () => { Call(host, "Poll"); Call(ui, "Prepare", true, Main.UIScaleMatrix, new Vector2(1280, 900)); };
            NativeQuickItemChecks.Until(() => { prepare(); return !library.Snapshot.Busy && summary() != null; });
            var a = summary(); Require(a.ContentCount == 2 && a.Width == 2, "A preview has both real selected object tiles");
            Call(host, "SetExcludeUnreliable", true);
            NativeQuickItemChecks.Until(() => { prepare(); return !library.Snapshot.Busy && summary() != null; });
            Require(summary().ContentCount == 1 && summary().Width == 1 && original.ContentCount == 2, "B preview excludes only uncertain foreground and keeps confirmed wall with recomputed bounds");
            long uploads = (long)Get(cache, "Uploads"); long receipt = library.Snapshot.LastReceipt.CommandId;
            Call(host, "SetExcludeUnreliable", false); prepare(); prepare();
            Require(ReferenceEquals(summary(), a) && (long)Get(cache, "Uploads") == uploads && library.Snapshot.LastReceipt.CommandId == receipt && ReferenceEquals(flow.Summary, original), "A-B-A cache revisit restores its paired summary without world or file reread or stale B facts");

            // Discard derived GPU state, then accept read A and change to B
            // before pumping its receipt. That old receipt may be cached under
            // A, but must not become B's summary or mutate the sealed facts.
            Call(ui, "LoseFocus"); Call(ui, "ShowCapture");
            NativeQuickItemChecks.Until(() => { prepare(); return GetOptional(ui, "pendingPreviewKey") != null; });
            Call(host, "SetExcludeUnreliable", true);
            NativeQuickItemChecks.Until(() => { prepare(); return !library.Snapshot.Busy && summary() != null; });
            Require(summary().Width == 1 && summary().ContentCount == 1 && ReferenceEquals(flow.Summary, original) && original.Width == 2, "late A result cannot replace current B summary or original fixed capture");
            Call(host, "SetExcludeUnreliable", false); prepare();
            Require(summary().Width == 2 && summary().ContentCount == 2, "the late A cache remains correctly paired when explicitly revisited");
            Console.WriteLine("PASS Blueprint actual GPU preview A-B-A cache summary pairing and old-combination late receipt; fixed source untouched.");
        }
        private static void Step(object context)
        {
            // Runtime owns the capture clock. A private Host tick can put this
            // fixture ahead of later real Runtime callbacks in the same process.
            NativeQuickItemChecks.BeginWorldStep(); Call(context, "UpdateRuntime");
        }
        internal static void Capture(object context, int x, int y, int left = -1)
        {
            var host = Get(context, "Blueprint"); var input = Get(context, "Input"); var shell = Get(context, "Shell");
            Call(shell, "CloseAndSubmitPosition"); NativeQuickItemChecks.Sample(input, Array.Empty<Microsoft.Xna.Framework.Input.Keys>());
            var editor = (BlueprintCaptureState)Get(host, "State"); var flow = (BlueprintCaptureFlow)Get(host, "Flow");
            editor.Enter(); editor.Selection.ToggleRectangle(left < 0 ? x : left, y, x, y, 120, 120);
            Require((bool)Call(host, "Save"), "visual real capture accepted");
            NativeQuickItemChecks.Until(() => { Step(context); Call(host, "Poll"); return flow.Status == BlueprintCaptureStatus.DraftReady || flow.Status == BlueprintCaptureStatus.Failed; });
            var receipt = ((LibraryState)Get(host, "Library")).Snapshot.LastReceipt;
            Require(flow.Status == BlueprintCaptureStatus.DraftReady, "visual real capture completed: " + flow.Message + " / " + receipt?.Kind + " / " + receipt?.FailureCode);
        }
    }
}
