using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingR.Features.Blueprint;
using JueMingR.Features.Text;
using JueMingR.TerrariaHost.Blueprint;
using JueMingR.TerrariaHost.Input;
using JueMingR.TerrariaHost.Notes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;

namespace JueMingR.TerrariaHost.F5
{
    // Only navigation, edit buffers and pointer targets live here. HostBlueprint
    // owns capture/draft/save semantics; LibraryState owns durable assets and
    // receipts. Closing this view cannot turn an accepted write into a cancel.
    internal sealed class BlueprintLibraryPresentation : ITextEditSession, IDisposable
    {
        private enum View { None, Library, Capture }
        private readonly HostBlueprint host;
        private readonly F5Interaction shell;
        private readonly BlueprintLibraryRenderer renderer = new BlueprintLibraryRenderer();
        private readonly BlueprintLibraryPreviewCache previews = new BlueprintLibraryPreviewCache();
        private readonly SingleLineEditView editView = new SingleLineEditView();
        private readonly List<BlueprintLibraryPart> parts = new List<BlueprintLibraryPart>();
        private readonly HashSet<string> failedPreviews = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, CaptureSummary> failedSummaries = new Dictionary<string, CaptureSummary>(StringComparer.Ordinal);
        private readonly Dictionary<Guid, string> pageKeys = new Dictionary<Guid, string>();
        internal readonly TextEditInput TextInput;
        private View mode;
        private TextEditBuffer editor;
        private Guid? editId, selected, deleteConfirm;
        private BlueprintLibraryPart armed, hovered;
        private F5Rect view, hitClip, editRect, pressView;
        private Matrix matrix, pressMatrix;
        private Vector2 screen;
        private int layoutGeneration = -1, resourceGeneration = -1, previewCacheGeneration = -1, projection, pressProjection;
        private Point previewSize;
        private float finalLibraryHeight;
        private bool ready, dirty = true, previousLeft, leftTail, rightTail, wantList, listFailed, objectFailed, editProtected, focusSuspended;
        private int requestedPage, objectIndex;
        private long readGeneration, activeReadGeneration, listGeneration, objectGeneration, previewGeneration, seenReceipt, seenFlow = -1, seenLibrary = -1;
        private float scroll;
        private Guid? selectAfterList;
        private LibraryObjectPage objects;
        private Guid objectsHandle;
        private string pendingPreviewKey, validation, draftPreviewKey;
        private int pendingPreviewDevice;
        private long pendingPreviewGeneration;
        private BlueprintPreviewImage pendingImage;
        private CaptureSummary pendingSummary;
        private LibraryCommandKind? pendingMutation;
        private Guid pendingAsset;
        private long mutationAfter, pendingEditRevision;
        private TextEditBuffer pendingEditor;
        private string shownMessage;
        private Guid lastSavedOperation;
        private long editRevision = -1, caretRevision = -1;
        private string composition;
        private Guid lastClickId;
        private int lastClickTime;
        private bool lastClickName;
        public TextEditBuffer Editor { get { return editor; } }
        internal bool Submenu { get { return mode != View.None; } }
        internal bool Visible { get { return Submenu && shell.Visible && shell.Page == 6; } }
        internal bool WorldInteractionBlocked { get { return Visible; } }
        internal bool OwnsTextToken { get { return TextInput.OwnsTextToken; } }
        internal bool OwnsPointer { get; private set; }
        internal bool ConsumeLeft { get; private set; }
        internal bool ConsumeRight { get; private set; }
        internal bool ConsumeWheel { get; private set; }
        internal long LayoutBuilds { get; private set; }
        internal int PreviewCount { get { return previews.Count; } }
        internal long PreviewBytes { get { return previews.PixelBytes; } }
        internal BlueprintLibraryPresentation(HostBlueprint host, F5Interaction shell, INotesIme ime = null)
        {
            this.host = host; this.shell = shell;
            TextInput = new TextEditInput(this, new NotesClipboard(() => Main.instance.Window.Handle), ime);
        }
        internal void OpenLibrary()
        {
            if (mode == View.Capture) host.CancelCaptureOrDraft();
            EndEdit(); mode = View.Library; deleteConfirm = null; validation = null; objects = null;
            failedPreviews.Clear(); failedSummaries.Clear(); RequestList(0, host.Flow.SelectedAssetId); Reveal();
        }
        internal void ShowCapture()
        {
            CancelRead();
            EndEdit(); mode = View.Capture; selected = Guid.Empty; deleteConfirm = null; validation = null; objectIndex = 0; objects = null;
            objectsHandle = Guid.Empty; objectFailed = false; failedPreviews.Clear(); failedSummaries.Clear(); Reveal();
        }
        private void Reveal() { focusSuspended = false; shell.RestoreVisible(); shell.Navigate(6); dirty = true; armed = null; }
        private void RequestList(int page, Guid? asset = null)
        { CancelRead(); requestedPage = Math.Max(0, page); selectAfterList = asset; listGeneration = ++readGeneration; wantList = true; listFailed = false; dirty = true; }
        internal void ShowSaved(Guid asset)
        {
            if (!Visible) { selectAfterList = asset; return; }
            EndEdit(); mode = View.Library; selected = asset; deleteConfirm = null;
            RequestList(requestedPage, asset); shell.ScrollTo(0);
        }
        internal void BeforeInput(bool active)
        {
            // A retired capture retains its name buffer until explicit edit
            // cancellation, but its hidden field cannot keep a native IME lease.
            TextInput.BeforeSample(active && Visible && (mode != View.Capture || !host.Flow.ContentHandle.IsEmpty));
        }
        public void PreserveUncommittedInput(TextEditBuffer value) { }
        public void CancelEdit() { EndEdit(); deleteConfirm = null; dirty = true; }
        public bool RequestFinish()
        {
            if (editor == null) return true;
            if (editProtected) return false;
            TextInput.FinishComposition(false); if (TextInput.HasComposition) return false;
            string error;
            if (!LibraryName.TryValidate(editor.Text, out error)) { validation = error; dirty = true; return false; }
            if (mode == View.Capture)
            {
                host.SetDraftName(editor.Text);
                if (host.Flow.DraftName != editor.Text) { validation = host.Flow.Message; dirty = true; return false; }
                // Keep the exact editor until reliable Saved. A write failure
                // returns to this draft/focus rather than rebuilding its text.
                validation = null; host.ConfirmSave(); dirty = true; return false;
            }
            if (!editId.HasValue || host.Library.Snapshot.Busy || pendingMutation.HasValue) return false;
            var entry = Find(editId.Value); if (entry == null || entry.IsProtected) { validation = "这个蓝图暂不可修改，原文件已保护"; dirty = true; return false; }
            if (host.Library.TryRename(entry.AssetId, entry.MetadataIdentity, editor.Text))
            {
                pendingMutation = LibraryCommandKind.Rename; pendingAsset = entry.AssetId;
                mutationAfter = host.Library.Snapshot.LastReceipt?.CommandId ?? 0;
                pendingEditor = editor; pendingEditRevision = editor.Revision; validation = null; dirty = true;
            }
            return false;
        }
        private void BeginEdit(Guid id, string name)
        {
            EndEdit(); editor = new TextEditBuffer(name, true, LibraryName.MaximumElements, LibraryName.MaximumElements * TextElements.MaximumElementUnits,
                "名称最多48个文字元素"); editId = id; deleteConfirm = null; validation = null;
            editProtected = false; TextInput.PrepareEditor(); dirty = true;
        }
        private void EndEdit()
        { TextInput.Release(true); editor = null; editId = null; pendingEditor = null; editProtected = false; validation = null; editRevision = caretRevision = -1; composition = null; dirty = true; }
        // Explicit navigation drops unsubmitted name edits. Already accepted
        // writes retain their receipt association and complete in the owner.
        internal void Suspend()
        {
            if (mode == View.Capture) host.CancelCaptureOrDraft();
            ReleasePresentation(); mode = View.None;
        }
        // Focus is an input/resource boundary, not a capture cancellation.
        // Preserve the submenu so a valid accepted job can finish and reappear.
        internal void LoseFocus() { ReleasePresentation(); focusSuspended = true; if (mode == View.Library) RequestList(requestedPage); }
        private void ReleasePresentation()
        {
            CancelRead();
            EndEdit(); deleteConfirm = null; armed = hovered = null; ready = false;
            OwnsPointer = false; parts.Clear(); previews.Dispose(); renderer.Dispose(); objects = null; objectsHandle = Guid.Empty;
            pendingImage = null; pendingPreviewKey = null; failedPreviews.Clear(); failedSummaries.Clear();
        }
        private void CancelRead()
        {
            // Every accepted read uses this one generation namespace. Writes
            // never acquire activeReadGeneration and cannot be cancelled here.
            if (activeReadGeneration != 0) host.Library.CancelRead(activeReadGeneration);
            activeReadGeneration = 0; pendingPreviewKey = null; pendingImage = null; pendingSummary = null;
        }
        internal bool RequestLeave() { Suspend(); return true; }
        internal void ProcessInput(bool active, KeyboardState keys, Vector2 point, bool geometryCurrent, bool focused, bool blocked)
        {
            bool left = host.Input.RawMouseLeft, right = host.Input.RawMouseRight;
            ConsumeLeft = ConsumeRight = ConsumeWheel = OwnsPointer = false;
            PollReceipts();
            bool ownGeometry = Same(view, shell.Layout.Viewport.Offset(shell.X, shell.Y)) && matrix == Main.UIScaleMatrix &&
                scroll == shell.Scroll && layoutGeneration == shell.Layout.Generation;
            bool on = active && Visible && ready && geometryCurrent && ownGeometry && !dirty && !blocked;
            if (!Visible) { if (Submenu && !shell.Visible && !focusSuspended) Suspend(); previousLeft = left; return; }
            focusSuspended = false;
            bool wasEditing = editor != null;
            TextInput.AfterSample(on, null, keys, focused);
            bool inside = hitClip.Contains(point.X, point.Y);
            OwnsPointer = on && (inside || leftTail || rightTail);
            hovered = on && inside ? Hit(point.X - view.X, point.Y - view.Y + shell.Scroll) : null;
            if (!on || !focused) { armed = null; if (deleteConfirm.HasValue) { deleteConfirm = null; dirty = true; } lastClickName = false; }
            else
            {
                if (left && !previousLeft && (!inside || hovered == null || hovered.Command != BlueprintLibraryCommand.Name)) lastClickName = false;
                if (left && !previousLeft && inside)
                {
                    leftTail = true; armed = hovered != null && hovered.Enabled ? hovered : null;
                    pressProjection = projection; pressView = view; pressMatrix = matrix;
                }
                if (right && inside) rightTail = true;
                if (!left && previousLeft && armed != null)
                {
                    var target = armed; armed = null;
                    if (ReferenceEquals(target, hovered) && pressProjection == projection && pressMatrix == matrix && Same(pressView, view)) Execute(target);
                }
                if (keys.IsKeyDown(Keys.Escape) && host.Input.Hotkeys.IsNew((int)Keys.Escape))
                {
                    if (editor != null && editProtected && mode == View.Capture && host.Flow.ContentHandle.IsEmpty &&
                        !TextInput.Owned && !TextInput.OtherTextOwner)
                    {
                        // Retirement removes the field and its text lease, so
                        // TextEditInput cannot consume this Escape. Cancel only
                        // our retained buffer here; preserve the failure facts
                        // and own its tail before a later independent Back.
                        string message = validation;
                        EndEdit(); validation = message;
                        host.Input.Hotkeys.SuppressKey((int)Keys.Escape);
                    }
                    else if (!wasEditing && editor == null)
                    { host.Input.Hotkeys.SuppressKey((int)Keys.Escape); Back(); }
                }
            }
            ConsumeLeft = leftTail; ConsumeRight = rightTail;
            if (!left && focused) leftTail = false;
            if (!right && focused) rightTail = false;
            previousLeft = left;
        }
        private BlueprintLibraryPart Hit(float x, float y)
        {
            for (int i = parts.Count - 1; i >= 0; i--)
                if (parts[i].Command != BlueprintLibraryCommand.None && parts[i].Element.Rect.Contains(x, y)) return parts[i];
            return null;
        }
        private void Execute(BlueprintLibraryPart part)
        {
            if (part.Command != BlueprintLibraryCommand.Name) lastClickName = false;
            switch (part.Command)
            {
                case BlueprintLibraryCommand.Back: Back(); return;
                case BlueprintLibraryCommand.Cancel: Back(); return;
                case BlueprintLibraryCommand.Previous: NavigatePage(-1); return;
                case BlueprintLibraryCommand.Next: NavigatePage(1); return;
                case BlueprintLibraryCommand.PreviousObjects: CancelRead(); objectIndex = Math.Max(0, objectIndex - 1); objects = null; objectFailed = false; objectGeneration = ++readGeneration; deleteConfirm = null; dirty = true; return;
                case BlueprintLibraryCommand.NextObjects: CancelRead(); objectIndex++; objects = null; objectFailed = false; objectGeneration = ++readGeneration; deleteConfirm = null; dirty = true; return;
                case BlueprintLibraryCommand.Exclude: CancelRead(); host.ExcludeObject(part.ObjectId, !part.Selected); dirty = true; return;
                case BlueprintLibraryCommand.ExcludeAll: CancelRead(); host.SetExcludeUnreliable(!host.Flow.ExcludeUnreliable); dirty = true; return;
                case BlueprintLibraryCommand.ConfirmDraft:
                    if (editor != null) { RequestFinish(); return; }
                    validation = null; host.ConfirmSave(); dirty = true; return;
                case BlueprintLibraryCommand.Rename: RequestFinish(); return;
                case BlueprintLibraryCommand.Delete:
                    EndEdit();
                    if (deleteConfirm == part.AssetId && selected == part.AssetId)
                    {
                        var entry = Find(part.AssetId);
                        if (entry != null && !entry.IsProtected && host.Library.TryDelete(entry.AssetId, entry.MetadataIdentity))
                        { pendingMutation = LibraryCommandKind.Delete; pendingAsset = entry.AssetId; mutationAfter = host.Library.Snapshot.LastReceipt?.CommandId ?? 0; }
                    }
                    else { selected = part.AssetId; deleteConfirm = part.AssetId; }
                    dirty = true; return;
                case BlueprintLibraryCommand.Card:
                    if (selected != part.AssetId) { EndEdit(); selected = part.AssetId; }
                    deleteConfirm = null; dirty = true; return;
                case BlueprintLibraryCommand.Name:
                    if (part.Editing) return;
                    int now = Environment.TickCount;
                    bool twice = lastClickName && lastClickId == part.AssetId && unchecked(now - lastClickTime) >= 0 && unchecked(now - lastClickTime) <= 450;
                    lastClickTime = now; lastClickId = part.AssetId; lastClickName = true;
                    if (selected != part.AssetId) EndEdit();
                    selected = part.AssetId; deleteConfirm = null;
                    if (twice)
                    { string name = mode == View.Capture ? host.Flow.DraftName : Find(part.AssetId)?.Name; if (name != null) BeginEdit(part.AssetId, name); lastClickName = false; }
                    dirty = true; return;
            }
        }
        private void NavigatePage(int delta)
        {
            var page = host.Library.Snapshot.Page; EndEdit(); deleteConfirm = null; lastClickName = false;
            int next = Math.Max(0, (page?.PageIndex ?? requestedPage) + delta);
            RequestList(next); shell.ScrollTo(0); failedPreviews.Clear(); failedSummaries.Clear();
        }
        private void Back()
        {
            Suspend(); shell.ScrollTo(0);
        }
        private LibraryEntry Find(Guid id)
        {
            var page = host.Library.Snapshot.Page;
            if (page != null) foreach (var entry in page.Entries) if (entry.AssetId == id) return entry;
            return null;
        }
        private void PollReceipts()
        {
            var receipt = host.Library.Snapshot.LastReceipt;
            if (receipt != null && receipt.CommandId != seenReceipt)
            {
                seenReceipt = receipt.CommandId;
                if (receipt.Generation == activeReadGeneration) activeReadGeneration = 0;
                if (receipt.Kind == LibraryCommandKind.Preview && receipt.Generation == pendingPreviewGeneration && pendingPreviewKey != null)
                {
                    if (receipt.Preview != null) { pendingImage = receipt.Preview; pendingSummary = receipt.Summary; }
                    else { RememberPreviewFailure(pendingPreviewKey, receipt.Summary); pendingPreviewKey = null; }
                    dirty = true;
                }
                if (receipt.Kind == LibraryCommandKind.Objects && receipt.Generation == objectGeneration)
                { objects = receipt.ObjectPage; objectFailed = objects == null; if (objectFailed) validation = receipt.Error ?? "对象说明暂不可用"; dirty = true; }
                if (receipt.Kind == LibraryCommandKind.List && receipt.Generation == listGeneration)
                {
                    if (selectAfterList.HasValue) selected = selectAfterList;
                    listFailed = receipt.Page == null;
                    if (listFailed) validation = receipt.Error ?? "蓝图库读取失败";
                    selectAfterList = null; requestedPage = receipt.Page?.PageIndex ?? requestedPage; dirty = true;
                }
                if (mode == View.Capture && receipt.Kind == LibraryCommandKind.Create && receipt.OperationId == host.Flow.Token.OperationId &&
                    receipt.Status != LibraryStatus.Saved)
                {
                    // File protection and commit certainty are separate facts.
                    // A proven precommit failure must not be called unknown, and
                    // a retired handle must never regain a retry gate. Its name
                    // buffer awaits explicit cancellation even without a field.
                    validation = receipt.Status == LibraryStatus.CommitUnconfirmed ? "无法确认是否保存成功，相关文件已保留" :
                        receipt.OperationRetired ? "此次蓝图未保存，相关文件已保留。可重新采集。" : receipt.Error ?? "保存失败，草稿已保留";
                    if (receipt.OperationRetired) { TextInput.Release(false); editProtected = true; }
                    dirty = true;
                }
                if (pendingMutation == receipt.Kind && pendingAsset == receipt.AssetId && receipt.CommandId > mutationAfter)
                {
                    bool success = receipt.Status == LibraryStatus.Saved || receipt.Status == LibraryStatus.Deleted;
                    if (success)
                    {
                        if (ReferenceEquals(editor, pendingEditor) && editor != null && editor.Revision == pendingEditRevision && !TextInput.HasComposition) EndEdit();
                        if (receipt.Kind == LibraryCommandKind.Delete) { if (selected == receipt.AssetId) selected = null; deleteConfirm = null; }
                        if (mode == View.Library) RequestList(requestedPage);
                    }
                    else
                    {
                        validation = receipt.Status == LibraryStatus.CommitUnconfirmed ? "无法确认修改是否保存成功，原文件已保护" : receipt.Error ?? "修改未能保存";
                        if ((receipt.Status == LibraryStatus.CommitUnconfirmed || receipt.IsProtected) && ReferenceEquals(editor, pendingEditor)) editProtected = true;
                    }
                    if (receipt.CleanupPending) validation = "蓝图已删除，文件清理尚未完成";
                    pendingMutation = null; pendingEditor = null; dirty = true;
                }
            }
            var flow = host.Flow;
            if (flow.Status == BlueprintCaptureStatus.Saved && flow.SelectedAssetId.HasValue && lastSavedOperation != flow.Token.OperationId)
            { lastSavedOperation = flow.Token.OperationId; if (mode == View.Capture) ShowSaved(flow.SelectedAssetId.Value); }
        }
        internal void Prepare(bool active, Matrix transform, Vector2 dimensions)
        {
            PollReceipts();
            if (!active || !Visible) { ready = false; return; }
            if (!renderer.Refresh()) { ready = false; return; }
            previews.Observe(renderer.Device);
            if (pendingImage != null)
            {
                try { previews.Upload(pendingPreviewKey, pendingImage, pendingSummary, pendingPreviewDevice); }
                catch { RememberPreviewFailure(pendingPreviewKey, pendingSummary); }
                pendingImage = null; pendingSummary = null; pendingPreviewKey = null; dirty = true;
            }
            var next = shell.Layout.Viewport.Offset(shell.X, shell.Y);
            bool geometry = matrix != transform || screen != dimensions || !Same(view, next) || layoutGeneration != shell.Layout.Generation || resourceGeneration != renderer.Generation || previewCacheGeneration != previews.Generation;
            if (geometry || scroll != shell.Scroll) { projection++; armed = null; }
            matrix = transform; screen = dimensions; view = next; scroll = shell.Scroll; ready = true;
            previewSize = BlueprintLibraryLayout.PreviewSize(view.Width, matrix);
            if (geometry) hitClip = F5ControlRenderer.LogicalClip(F5ControlRenderer.ContentClip(view, matrix,
                new Rectangle(0, 0, (int)dimensions.X, (int)dimensions.Y)), matrix);
            bool shape = dirty || geometry || seenFlow != host.Flow.Revision || seenLibrary != host.Library.Snapshot.Revision || shownMessage != Message;
            if (shape)
            {
                Build(); LayoutBuilds++; dirty = false; projection++; armed = null;
                // Dynamic page heights are complete only here. A loading list
                // retains its last effective height, so Ensure's temporary empty
                // page cannot consume the user's scroll before the reply arrives.
                shell.ClampScroll();
                if (scroll != shell.Scroll) { scroll = shell.Scroll; projection++; armed = null; }
                seenFlow = host.Flow.Revision; seenLibrary = host.Library.Snapshot.Revision; shownMessage = Message;
                layoutGeneration = shell.Layout.Generation; resourceGeneration = renderer.Generation; previewCacheGeneration = previews.Generation;
            }
            if (editor != null && editRect.Width > 0 && (shape || editRevision != editor.Revision || caretRevision != editor.CaretRevision || composition != TextInput.Composition))
            {
                // Reserve the caret's own pixel, including at the end of a full
                // field. Composition/caret changes update this view without a
                // layout rebuild or revoking the text lease.
                editView.Prepare(editor, TextInput.Composition, editRect.Width - 11, Measure);
                editRevision = editor.Revision; caretRevision = editor.CaretRevision; composition = TextInput.Composition;
            }
            RequestWork();
        }
        private string Message
        {
            get { return TextInput.Error ?? editor?.Error ?? validation ?? (mode == View.Capture ?
                host.Flow.Status == BlueprintCaptureStatus.Failed || host.Flow.Status == BlueprintCaptureStatus.CommitUnconfirmed ? host.Flow.Message : null : host.Library.Snapshot.Page?.Error); }
        }
        private void RequestWork()
        {
            if (host.Library.Snapshot.Busy || pendingMutation.HasValue || pendingPreviewKey != null || pendingImage != null) return;
            if (mode == View.Library)
            {
                if (wantList) { if (host.Library.TryList(listGeneration, requestedPage, selectAfterList)) { wantList = false; activeReadGeneration = listGeneration; } return; }
                var page = host.Library.Snapshot.Page; if (page == null || page.Generation != listGeneration) return;
                for (int i = 0; i < page.Entries.Count; i++)
                {
                    var card = BlueprintLibraryLayout.Card(view.Width, i);
                    if (card.Bottom < scroll || card.Y > scroll + view.Height) continue;
                    var entry = page.Entries[i]; string key;
                    if (!pageKeys.TryGetValue(entry.AssetId, out key)) return;
                    if (previews.Touch(key) || failedPreviews.Contains(key)) continue;
                    long generation = previewGeneration = ++readGeneration;
                    if (host.Library.TryPreview(generation, entry.AssetId, entry.ContentHash, previewSize.X, previewSize.Y)) PreviewPending(key, generation);
                    return;
                }
            }
            else if (mode == View.Capture && !host.Flow.ContentHandle.IsEmpty)
            {
                var summary = host.Flow.Summary;
                if (summary != null && summary.UnreliableObjectCount > 0 && !objectFailed && (objects == null || objectsHandle != host.Flow.ContentHandle.Id))
                {
                    if (objectsHandle != host.Flow.ContentHandle.Id) { objectsHandle = host.Flow.ContentHandle.Id; objectGeneration = ++readGeneration; }
                    if (host.Library.TryObjects(objectGeneration, host.Flow.ContentHandle, objectIndex)) activeReadGeneration = objectGeneration; return;
                }
                string key = draftPreviewKey;
                if (key == null) return;
                if (!previews.Touch(key) && !failedPreviews.Contains(key))
                {
                    long generation = previewGeneration = ++readGeneration;
                    if (host.Library.TryPreview(generation, host.Flow.ContentHandle, previewSize.X, previewSize.Y, host.Flow.ExcludeUnreliable, host.Flow.ExcludedObjectIds)) PreviewPending(key, generation);
                }
            }
        }
        private void PreviewPending(string key, long generation)
        { pendingPreviewKey = key; pendingPreviewGeneration = generation; activeReadGeneration = generation; pendingPreviewDevice = previews.Generation; }
        private void RememberPreviewFailure(string key, CaptureSummary summary = null)
        {
            // Failed derived keys also need a bound when an owner repeatedly
            // changes exclusions. This never changes asset or commit state.
            if (failedPreviews.Count >= 12) { failedPreviews.Clear(); failedSummaries.Clear(); }
            failedPreviews.Add(key);
            if (summary != null) failedSummaries[key] = summary;
        }
        private string PreviewIdentity { get { return ":" + BlueprintPreviewBuilder.RendererVersion + ":" + previewSize.X + ":" + previewSize.Y + ":g" + previews.Generation; } }
        private string AssetKey(LibraryEntry entry) { return "a:" + entry.ContentHash + PreviewIdentity; }
        private string DraftKey() { return "d:" + host.Flow.ContentHandle.Id.ToString("N") + ":" + host.Flow.ExcludeUnreliable + ":" + string.Join(",", host.Flow.ExcludedObjectIds) + PreviewIdentity; }
        private void Build()
        {
            parts.Clear(); pageKeys.Clear(); draftPreviewKey = null; editRect = default(F5Rect);
            if (mode == View.Library) BuildLibrary(); else BuildCapture();
        }
        private void BuildLibrary()
        {
            var snapshot = host.Library.Snapshot; var page = snapshot.Page;
            bool current = page != null && page.Generation == listGeneration && !wantList;
            Panel(new F5Rect(0, 0, view.Width, 42));
            Label("蓝图库", new F5Rect(10, 5, 220, 16), .76f);
            Label("共保存" + (current ? page.TotalCount : 0).ToString(CultureInfo.InvariantCulture) + "个蓝图", new F5Rect(10, 24, 235, 16), .62f);
            Button("导入", BlueprintLibraryLayout.HeaderButton(view.Width, 0), BlueprintLibraryCommand.None, false);
            Button("上一页", BlueprintLibraryLayout.HeaderButton(view.Width, 1), BlueprintLibraryCommand.Previous, current && page.PageIndex > 0 && !snapshot.Busy);
            Button("下一页", BlueprintLibraryLayout.HeaderButton(view.Width, 2), BlueprintLibraryCommand.Next, current && page.PageIndex + 1 < page.PageCount && !snapshot.Busy);
            Button("返回", BlueprintLibraryLayout.HeaderButton(view.Width, 3), BlueprintLibraryCommand.Back, true);
            int count = current ? page.Entries.Count : 0;
            float height = BlueprintLibraryLayout.Height(count);
            if (count == 0)
            {
                Panel(new F5Rect(0, 48, view.Width, 58));
                Label(!current ? listFailed ? "蓝图库读取失败" : "正在读取蓝图库" : page.Error != null ? "蓝图库读取失败" : "暂无蓝图", new F5Rect(12, 58, view.Width - 24, 18));
                height = Math.Max(height, 82 + AddWrapped(current && page.Error == null ? "采集并保存后，会显示在这里。" : Message ?? "", 12, 82, view.Width - 24, Color.White) + 8);
            }
            else for (int i = 0; i < count; i++)
            {
                var entry = page.Entries[i]; var card = BlueprintLibraryLayout.Card(view.Width, i);
                string key = AssetKey(entry); pageKeys[entry.AssetId] = key;
                BuildCard(card, entry.AssetId, entry.Name, entry.Width, entry.Height, entry.ContentCount, entry.IsProtected ? "这个蓝图暂不可修改，原文件已保护" : entry.MissingSummary, key,
                    !snapshot.Busy && !page.IsProtected && !entry.IsProtected, false);
            }
            if (count > 0 && !string.IsNullOrEmpty(Message)) { height += AddWrapped(Message, 8, height, view.Width - 16, Color.LightSalmon) + 6; }
            var chosen = selected.HasValue ? Find(selected.Value) : null;
            if (chosen != null && !string.IsNullOrEmpty(chosen.MissingSummary)) height += AddWrapped(chosen.MissingSummary, 8, height, view.Width - 16, Color.LightGoldenrodYellow) + 6;
            if (current && page.IsolatedCount > 0) height += AddWrapped("有" + page.IsolatedCount + "项内容暂不可用，原文件已保留。", 8, height, view.Width - 16, Color.LightGoldenrodYellow) + 6;
            if (current || listFailed) finalLibraryHeight = height;
            shell.Layout.SetBlueprintContentHeight(current || listFailed ? height : Math.Max(height, finalLibraryHeight));
        }
        private void BuildCard(F5Rect card, Guid id, string name, int width, int height, long count, string missing, string key, bool enabled, bool draft)
        {
            var panel = Panel(card); panel.Card = true; panel.Selected = draft || selected == id; panel.Command = BlueprintLibraryCommand.Card; panel.AssetId = id; panel.Enabled = true;
            bool editing = editor != null && editId == id;
            var nameRect = BlueprintLibraryLayout.Name(card);
            var namePart = Button(editing ? "" : name, nameRect, BlueprintLibraryCommand.Name, draft ? host.Flow.Status == BlueprintCaptureStatus.DraftReady && !host.Library.Snapshot.Busy : enabled || editing, id);
            namePart.Editing = editing;
            if (editing) editRect = nameRect;
            Button(draft ? "保存" : editing ? "保存" : "放置", BlueprintLibraryLayout.Action(card, 0), draft ? BlueprintLibraryCommand.ConfirmDraft : editing ? BlueprintLibraryCommand.Rename : BlueprintLibraryCommand.None,
                draft ? enabled : editing && enabled && !editProtected, id);
            Button("导出", BlueprintLibraryLayout.Action(card, 1), BlueprintLibraryCommand.None, false, id);
            Button(draft ? "取消" : deleteConfirm == id ? "确认" : "删除", BlueprintLibraryLayout.Action(card, 2), draft ? BlueprintLibraryCommand.Cancel : BlueprintLibraryCommand.Delete, draft || enabled, id);
            var preview = BlueprintLibraryLayout.Preview(card);
            var image = TextPart(count == 0 ? "没有可预览的内容" : failedPreviews.Contains(key) ? "预览暂不可用" : "正在生成预览", preview, .62f, F5ElementKind.Image); image.PreviewKey = key;
            var materials = BlueprintLibraryLayout.Materials(card);
            Label(width + "x" + height + " / 内容 " + count.ToString(CultureInfo.InvariantCulture), new F5Rect(preview.X, preview.Bottom + 5, Math.Max(1, materials.X - 4 - preview.X), 13), .62f);
            Button("材料", materials, BlueprintLibraryCommand.None, false);
            string inlineError = (editing || selected == id) && !string.IsNullOrEmpty(Message) ? Message : null;
            // A compact card shows two readable lines; selected diagnostics are
            // also rendered in full below the cards and count toward scrolling.
            if (inlineError != null) CardMessage(inlineError, preview, Color.LightSalmon);
            else if (!string.IsNullOrEmpty(missing)) CardMessage(missing, preview, Color.LightGoldenrodYellow);
        }
        private void BuildCapture()
        {
            var flow = host.Flow; var summary = flow.Summary;
            Panel(new F5Rect(0, 0, view.Width, 42));
            Label("保存蓝图", new F5Rect(10, 5, view.Width - 90, 20), .76f);
            Button("返回", BlueprintLibraryLayout.HeaderButton(view.Width, 3), BlueprintLibraryCommand.Back, true);
            float y = 48;
            if (summary != null && !flow.ContentHandle.IsEmpty)
            {
                draftPreviewKey = DraftKey();
                var projected = previews.Summary(draftPreviewKey);
                if (projected == null) failedSummaries.TryGetValue(draftPreviewKey, out projected);
                bool resolved = summary.UnreliableObjectCount == 0 || flow.ExcludeUnreliable || host.Flow.ExcludedObjectIds.LongLength >= summary.UnreliableObjectCount;
                bool canSave = previews.Get(draftPreviewKey) != null && projected != null && projected.ContentCount > 0 && resolved && flow.Status == BlueprintCaptureStatus.DraftReady && !host.Library.Snapshot.Busy;
                var display = projected ?? summary;
                BuildCard(BlueprintLibraryLayout.Card(view.Width, 0), Guid.Empty, flow.DraftName, display.Width, display.Height, display.ContentCount,
                    string.Join("；", display.MissingPayloads), draftPreviewKey, canSave, true);
                y = 48 + BlueprintLibraryLayout.CardHeight + 8;
                if (projected == null) y += AddWrapped(failedPreviews.Contains(draftPreviewKey) ? "预览暂不可用" : "正在准备预览", 8, y, view.Width - 16, Color.LightGoldenrodYellow) + 4;
                else if (projected.ContentCount == 0) y += AddWrapped("没有可保存的内容", 8, y, view.Width - 16, Color.LightGoldenrodYellow) + 4;
                if (display.MissingPayloads.Length > 0) foreach (string item in display.MissingPayloads) y += AddWrapped(item, 8, y, view.Width - 16, Color.LightGoldenrodYellow) + 3;
                if (summary.UnreliableObjectCount > 0)
                {
                    y += AddWrapped("有" + summary.UnreliableObjectCount + "件对象尚未确认，可排除后保存其余内容。", 8, y, view.Width - 16, Color.LightGoldenrodYellow) + 4;
                    Button(flow.ExcludeUnreliable ? "恢复全部" : "排除全部", new F5Rect(8, y, 98, 24), BlueprintLibraryCommand.ExcludeAll, flow.Status == BlueprintCaptureStatus.DraftReady && !host.Library.Snapshot.Busy);
                    Button("上一组", new F5Rect(view.Width - 172, y, 76, 24), BlueprintLibraryCommand.PreviousObjects, objects != null && objects.PageIndex > 0 && !host.Library.Snapshot.Busy);
                    Button("下一组", new F5Rect(view.Width - 88, y, 76, 24), BlueprintLibraryCommand.NextObjects, objects != null && objects.PageIndex + 1 < objects.PageCount && !host.Library.Snapshot.Busy); y += 30;
                    if (objects == null) { Label(objectFailed ? "对象说明暂不可用" : "正在读取对象说明", new F5Rect(8, y, view.Width - 16, 20), .62f); y += 26; }
                    else foreach (var item in objects.Items)
                    {
                        bool excluded = flow.ExcludeUnreliable || Array.IndexOf(flow.ExcludedObjectIds, item.Id) >= 0;
                        float height = AddWrapped("对象" + item.Id + "（" + item.X + "," + item.Y + "）：" + item.Reason, 8, y, view.Width - 110, Color.LightGoldenrodYellow);
                        var toggle = Button(excluded ? flow.ExcludeUnreliable ? "已排除" : "恢复" : "排除", new F5Rect(view.Width - 90, y, 82, 24), BlueprintLibraryCommand.Exclude, flow.Status == BlueprintCaptureStatus.DraftReady && !flow.ExcludeUnreliable && !host.Library.Snapshot.Busy);
                        toggle.ObjectId = item.Id; toggle.Selected = excluded;
                        y += Math.Max(28, height + 4);
                    }
                }
            }
            else
            {
                string state = flow.Status == BlueprintCaptureStatus.NoContent ? "选区内没有可保存的内容" : flow.Status == BlueprintCaptureStatus.Starting || flow.Status == BlueprintCaptureStatus.Capturing || flow.Status == BlueprintCaptureStatus.Sealing ? "正在采集 " + flow.Progress + " / " + flow.Total : flow.Status == BlueprintCaptureStatus.Cancelled ? "已取消采集" : "采集未完成";
                y += AddWrapped(state, 8, y, view.Width - 16, Color.White) + 8;
                Button("取消", new F5Rect(8, y, 76, 24), BlueprintLibraryCommand.Cancel, true); y += 32;
            }
            if (flow.Status == BlueprintCaptureStatus.Saving) { y += AddWrapped("正在保存", 8, y, view.Width - 16, Color.White) + 6; }
            if (flow.Status == BlueprintCaptureStatus.CommitUnconfirmed) { y += AddWrapped("无法确认是否保存成功，原文件已保护", 8, y, view.Width - 16, Color.LightSalmon) + 6; }
            if (!string.IsNullOrEmpty(Message)) y += AddWrapped(Message, 8, y, view.Width - 16, Color.LightSalmon) + 6;
            shell.Layout.SetBlueprintContentHeight(y + 6);
        }
        private BlueprintLibraryPart Panel(F5Rect rect) { return TextPart("", rect, 1, F5ElementKind.Panel); }
        private BlueprintLibraryPart Label(string text, F5Rect rect, float scale = .70f, Color? ink = null)
        { var part = TextPart(text, rect, scale, F5ElementKind.Text); part.Ink = ink ?? Color.White; return part; }
        private BlueprintLibraryPart Button(string text, F5Rect rect, BlueprintLibraryCommand command, bool enabled, Guid id = default(Guid))
        {
            if (command == BlueprintLibraryCommand.Name) text = Fit(text ?? "", Math.Max(1, rect.Width - 14), .62f);
            var part = TextPart(text, rect, .62f, F5ElementKind.Button); part.Command = command; part.Enabled = enabled; part.AssetId = id; return part;
        }
        private BlueprintLibraryPart TextPart(string text, F5Rect rect, float scale, F5ElementKind kind)
        {
            text = text ?? "";
            string fitted = Fit(text, Math.Max(1, rect.Width - (kind == F5ElementKind.Button ? 8 : 0)), scale);
            var size = Measure(fitted, scale);
            var part = new BlueprintLibraryPart { Element = new F5Element(kind, rect, fitted, size, scale, F5Command.None) }; parts.Add(part); return part;
        }
        private F5Size Measure(string text, float scale)
        {
            // Reuse the shared glyph bounds, changing only this renderer's known
            // outline expansion. Draw, fit, caret and hit rectangles share it.
            var size = shell.Layout.DynamicTextSize(text, scale);
            float border = BlueprintLibraryRenderer.Outline(scale);
            return new F5Size(size.Width - 4 + 2 * border, size.Height - 4 + 2 * border,
                size.OffsetX + 2 - border, size.OffsetY + 2 - border);
        }
        private void CardMessage(string text, F5Rect preview, Color ink)
        {
            var boundaries = TextElements.Boundaries(text); int start = 0;
            float y = preview.Bottom + 25;
            for (int row = 0; row < 2 && start < boundaries.Length - 1; row++)
            {
                int lo = start + 1, hi = boundaries.Length - 1;
                while (lo < hi) { int mid = (lo + hi + 1) / 2; if (Measure(text.Substring(boundaries[start], boundaries[mid] - boundaries[start]), .62f).Width <= preview.Width) lo = mid; else hi = mid - 1; }
                string line = text.Substring(boundaries[start], boundaries[lo] - boundaries[start]);
                if (row == 1 && lo < boundaries.Length - 1) line = Fit(line + "…", preview.Width, .62f);
                var size = Measure(line, .62f); float height = Math.Max(16, size.Height + 1);
                Label(line, new F5Rect(preview.X, y, preview.Width, height), .62f, ink); y += height; start = lo;
            }
        }
        private string Fit(string text, float width, float scale)
        {
            if (Measure(text, scale).Width <= width) return text;
            var boundaries = TextElements.Boundaries(text); int lo = 0, hi = boundaries.Length - 1;
            while (lo < hi) { int mid = (lo + hi + 1) / 2; if (Measure(text.Substring(0, boundaries[mid]) + "…", scale).Width <= width) lo = mid; else hi = mid - 1; }
            return text.Substring(0, boundaries[lo]) + "…";
        }
        private float AddWrapped(string text, float x, float y, float width, Color color)
        {
            var boundaries = TextElements.Boundaries(text ?? ""); int start = 0; float top = y;
            while (start < boundaries.Length - 1)
            {
                int lo = start + 1, hi = boundaries.Length - 1;
                while (lo < hi) { int mid = (lo + hi + 1) / 2; if (Measure(text.Substring(boundaries[start], boundaries[mid] - boundaries[start]), .62f).Width <= width) lo = mid; else hi = mid - 1; }
                string line = text.Substring(boundaries[start], boundaries[lo] - boundaries[start]);
                var size = Measure(line, .62f); float height = Math.Max(18, size.Height + 2);
                Label(line, new F5Rect(x, top, width, height), .62f, color); top += height; start = lo;
            }
            return top - y;
        }
        internal void Draw()
        {
            if (!Visible || !ready) return;
            renderer.Draw(parts, view, matrix, shell.Scroll, hovered, armed, previews, editView,
                !dirty && TextInput.Owned && TextInput.OwnsTextToken && !TextInput.OtherTextOwner);
            // Retain the final caller scissor, including its fractional-scale
            // rounding. A changed visible region retires any earlier press.
            if (!Same(hitClip, renderer.LastClip)) { hitClip = renderer.LastClip; projection++; armed = null; }
        }
        private static bool Same(F5Rect a, F5Rect b) { return a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height; }
        public void Dispose() { Suspend(); }
    }
}
