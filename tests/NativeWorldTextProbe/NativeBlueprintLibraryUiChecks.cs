using System;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Blueprint;
using JueMingR.Features.Text;
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
            float[] headerX = { 272, 342, 400, 458 };
            for (int i = 0; i < 4; i++) Require((float)Get(invoke("HeaderButton", new object[] { 522f, i }), "X") == headerX[i], "import/previous/next/back stay in their original order and positions");

            // Construct only this presentation over the isolated fixture Host.
            // BeginEdit does not acquire the native text lease; BeforeSample is
            // deliberately absent, so this does not claim Windows IME coverage.
            var state = Get(Get(context, "Shell"), "State");
            var uiType = assembly.GetType("JueMingR.TerrariaHost.F5.BlueprintLibraryPresentation", true);
            var ui = Activator.CreateInstance(uiType, F, null, new object[] { host, state, null }, null);
            var library = (LibraryState)Get(host, "Library"); var savedSnapshot = Get(library, "snapshot");
            try
            {
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
                Call(ui, "CancelEdit"); Require(GetOptional(ui, "Editor") == null, "explicit cancel releases this editor only");
            }
            finally { Set(library, "snapshot", savedSnapshot); ((IDisposable)ui).Dispose(); }
        }
        private static void ReceiptFailure(object ui, LibraryState library, TextEditBuffer editor, Guid id, LibraryStatus status, long command)
        {
            Set(ui, "pendingMutation", (LibraryCommandKind?)LibraryCommandKind.Rename); Set(ui, "pendingAsset", id);
            Set(ui, "pendingEditor", editor); Set(ui, "pendingEditRevision", editor.Revision); Set(ui, "mutationAfter", command - 1);
            var receipt = (LibraryReceipt)Activator.CreateInstance(typeof(LibraryReceipt), F, null,
                new object[] { command, LibraryCommandKind.Rename, status, Guid.Empty, id, default(CaptureToken), 0L, "测试写入失败", status == LibraryStatus.CommitUnconfirmed, false, null, null, null, null, null }, null);
            var snapshot = Activator.CreateInstance(typeof(LibrarySnapshot), F, null, new object[] { command, false, null, receipt, 0L }, null);
            Set(library, "snapshot", snapshot); Call(ui, "PollReceipts");
        }
    }
}
