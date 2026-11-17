using System;
using System.Collections.Generic;
using JueMingR.Features.Text;
using JueMingR.Platform.Companions;
using JueMingR.Platform.Hotkeys;
using JueMingR.TerrariaHost.Companions;
using JueMingR.TerrariaHost.Input;
using JueMingR.TerrariaHost.Items;
using JueMingR.TerrariaHost.Notes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.F5
{
    // The window owns selection, creation drafts and physical gestures. Item
    // previews and atomic commits belong to the Host, which revalidates current
    // native inventories; this surface never owns a detached consumable Item.
    internal sealed partial class CompanionWindow : ITextEditSession
    {
        internal const string ActionId = "companions.manage";
        private sealed class Control
        {
            internal string Key, Text;
            internal F5Rect Rect;
            internal bool Enabled = true, Selected, Off;
            internal Action Execute;
            internal int ItemType, Stack, Prefix=-1;
            internal Player Portrait;
            internal F5Element Element;
            internal string Hint, Placeholder, StackText, Availability, StateText;
            internal bool Locked;
            internal bool IsSlot, Owner;
            internal bool ActorOverlay,ActorState;
            internal int TeamIcon=-1;
            internal CompanionInventoryArea Area;
            internal int Slot;
            internal int EmptyIcon=-1;
            internal Guid ProfileId;
            internal bool Joined;
        }
        private readonly HostCompanions host;
        private readonly HostInputState input;
        private readonly Func<string, float, F5Size> measure;
        private readonly Func<string, float, F5Size> measureSource;
        private readonly Dictionary<string, F5Size> sizes70 = new Dictionary<string, F5Size>();
        private readonly Dictionary<string, F5Size> sizes65 = new Dictionary<string, F5Size>();
        private readonly ItemsRenderer renderer = new ItemsRenderer();
        private readonly PreparedItemIcons icons = new PreparedItemIcons();
        private readonly Terraria.Graphics.Renderers.LegacyPlayerRenderer portraits = new Terraria.Graphics.Renderers.LegacyPlayerRenderer();
        private readonly List<Control> controls = new List<Control>();
        private readonly List<F5Element> labels = new List<F5Element>();
        private readonly TextEditInput textInput;
        private readonly SingleLineEditView editView = new SingleLineEditView();
        private readonly F5HintLayout hint = new F5HintLayout();
        private readonly List<string> visibleStatuses = new List<string>();
        private readonly List<Guid> visibleIds = new List<Guid>();
        private readonly List<string> visibleNames = new List<string>();
        private readonly List<Player> visibleBodies = new List<Player>();
        private int preparedGeneration = -1, previousProfileCount = -1, maximumLife, maximumMana, weapon;
        private long profileRevision;
        private bool editing,memberJoined,selectedTransfer,saveFailed;
        private string selectedUnavailable;
        private string displayedNotice;
        private int noticeLabel=-1;
        private Player presentedOwner;
        private string presentedOwnerName;
        private Player selectedBody;
        private float rowHeight;
        private long viewDataEpoch;
        private long editRevision = -1, caretRevision = -1;
        private string editComposition;
        private float editWidth = -1;
        private F5Element editElement;
        private TextEditBuffer editor;
        private string editKind, armedKey, notice;
        private F5Rect panel, listView, bodyView, field;
        private F5Rect titleBar, ownerView, companionView;
        private int ownerOffset, ownerMaximum, ownerGearPage, companionGearPage,manualWeaponSlot=-1;
        private readonly Dictionary<Guid,CompanionPreview> railPreviews=new Dictionary<Guid,CompanionPreview>();
        private bool previousRight;
        private int rightRepeat;
        private string rightSlot;
        private int ownerLifeMax, ownerManaMax;
        private string resourceStyle;
        private readonly CompanionPreview ownerPreview = new CompanionPreview(), companionPreview = new CompanionPreview();
        private readonly CompanionResourceDisplay resourceDisplay = new CompanionResourceDisplay();
        private F5Rect ownerHero, companionHero,ownerShowcase,companionShowcase,ownerShowcasePanel,companionShowcasePanel;
        private float sharedResourcesHeight,sharedEquipmentHeight,sharedLifeHeight;
        private readonly List<F5Rect> actorHeaders=new List<F5Rect>();
        private readonly List<F5Rect> groups = new List<F5Rect>();
        private int selectedStatusLabel=-1,respawnLabel=-1;
        private bool contentInvalidated;
        private const float AvatarSize=54, AvatarStep=62;
        private Vector2 requestedPosition, grabOffset, dragStartPosition;
        private bool dragging, userPositioned, dragStartPositioned;
        private Matrix matrix;
        private Vector2 screen, pointer;
        private Guid selected,pendingCreatedId;
        private string pendingCreatedName;
        private long pendingCreationToken;
        private int page, listOffset, bodyOffset, listMaximum, bodyMaximum, generation, armedGeneration, skin;
        private bool previousLeft, leftTail, rightTail, ready, confirmingReset;
        public TextEditBuffer Editor { get { return editor; } }
        internal bool Visible { get; private set; }
        internal bool Captured { get { return dragging || armedKey != null || leftTail || rightTail; } }
        internal bool Independent { get; private set; }
        internal bool Dragging { get { return dragging; } }
        internal bool OwnsPointer { get; private set; }
        internal bool ConsumeLeft { get; private set; }
        internal bool ConsumeRight { get; private set; }
        internal bool ConsumeWheel { get; private set; }
        internal bool OwnsTextToken { get { return textInput.OwnsTextToken; } }
        internal int LayoutBuilds { get; private set; }
        internal int ItemPresentationUpdates { get; private set; }
        internal CompanionWindow(HostCompanions host, HostInputState input, Func<string, float, F5Size> measure)
        {
            this.host = host; this.input = input; measureSource = measure; this.measure = MeasureCached;
            textInput = new TextEditInput(this, new NotesClipboard(() => Main.instance.Window.Handle));
        }
        internal void Toggle(F5Rect anchor)
        { ToggleCore(false); }
        internal void ToggleIndependent(HotkeyChord chord)
        {
            // Only an admitted mouse-primary opener owns that physical tail.
            // Keyboard open never steals an already-held world mouse gesture.
            if (chord?.MainKey == 256) leftTail = PlayerInput.MouseInfo.LeftButton == ButtonState.Pressed;
            if (chord?.MainKey == 257) rightTail = PlayerInput.MouseInfo.RightButton == ButtonState.Pressed;
            ToggleCore(true);
        }
        private void ToggleCore(bool independent)
        {
            if (Visible) { Close(); return; }
            Visible = true; Independent = independent; ready = false; notice = null;
            // The entry's release belongs to the entry. A held button cannot
            // become a fresh slot press when this upper surface appears.
            previousLeft = PlayerInput.MouseInfo.LeftButton == ButtonState.Pressed;
            previousRight = PlayerInput.MouseInfo.RightButton == ButtonState.Pressed; rightRepeat=0; rightSlot=null;
            armedKey = null; generation++;
        }
        internal bool Contains(float x, float y) { return Visible && ready && panel.Contains(x, y); }
        internal void BeforeInput(bool active) { textInput.BeforeSample(active && Visible); }
        internal void CloseWithParent() { if (!Independent) Close(); }
        internal void CancelForFocusLoss()
        {
            if (dragging) { requestedPosition = dragStartPosition; userPositioned = dragStartPositioned; }
            Close();
        }
        internal void Close()
        {
            if (!Visible && !ready && editor == null) return;
            Visible = false; ready = false; armedKey = null; confirmingReset = dragging = false;
            host.CancelItemGesture(); host.EndItemRepeat(); rightSlot=null; rightRepeat=0; ownerPreview.Clear(); companionPreview.Clear(); railPreviews.Clear(); resourceDisplay.ClearNumbers(); CancelEdit(); icons.Clear(); renderer.Dispose(); controls.Clear(); labels.Clear(); generation++;
            // Keep already owned physical tails until the shared trusted sample
            // observes release, including close, lost focus and world departure.
        }
        internal void PrepareLayout(Matrix transform, Vector2 physical) { PrepareCore(transform, physical, false); }
        internal void Prepare(Matrix transform, Vector2 physical) { PrepareCore(transform, physical, true); }
        private void PrepareCore(Matrix transform, Vector2 physical, bool resources)
        {
            if (!Visible) return;
            if (resources && !renderer.Refresh() || transform.M11 <= 0 || transform.M22 <= 0) { Close(); return; }
            Vector2 logical = physical / transform.M11;
            if (skin != renderer.Generation) { sizes70.Clear(); sizes65.Clear(); hint.Clear(); editRevision = -1; }
            if (skin != renderer.Generation || rowHeight == 0) rowHeight = Math.Max(34, measure("主人附近优先", .7f).Height + 14);
            // A readable native ten-slot row fixes the desired column width;
            // actual native segment rows and slot groups determine the height.
            // Current life, countdowns, item counts and saves never resize it.
            float width=Math.Min(104+2*(10*44+24),logical.X-24);
            float height=Math.Min(PreferredHeight(width,rowHeight),logical.Y-24);
            if (width < 520 || height < 270) { Close(); return; }
            if (dragging && (matrix != transform || screen != physical || skin != renderer.Generation)) dragging = false;
            Vector2 origin = userPositioned ? requestedPosition : new Vector2((logical.X - width) / 2, (logical.Y - height) / 2);
            var next = new F5Rect(Math.Max(12, Math.Min(logical.X - width - 12, origin.X)),
                Math.Max(12, Math.Min(logical.Y - height - 12, origin.Y)), width, height);
            if (ready && panel.Width == width && panel.Height == height && matrix == transform && screen == physical && skin == renderer.Generation && !ViewChanged() &&
                (panel.X != next.X || panel.Y != next.Y))
            {
                // A title drag translates the prepared cells without remeasuring
                // text, rebuilding inventory controls or changing either scroll.
                Translate(next.X - panel.X, next.Y - panel.Y);
                generation++; preparedGeneration = generation;
                RefreshItemPresentation(resources); RefreshDynamicPresentation(); RefreshNotice(); return;
            }
            if (!panel.Equals(next) || matrix != transform || screen != physical || skin != renderer.Generation)
            { host.CancelItemGesture(); armedKey = null; rightSlot = null; generation++; }
            panel = next; matrix = transform; screen = physical; skin = renderer.Generation;
            float row = rowHeight;
            titleBar = new F5Rect(panel.X + 8, panel.Y + 4, width - 56, row);
            listView = new F5Rect(panel.X + 10, panel.Y + row + 8, AvatarSize, height - row - AvatarSize - 26);
            bodyView = new F5Rect(listView.Right + 12, listView.Y, panel.Right - listView.Right - 28, panel.Bottom - listView.Y - 45);
            float half = (bodyView.Width - 12) / 2;
            ownerView = new F5Rect(bodyView.X, bodyView.Y, half, bodyView.Height);
            companionView = new F5Rect(bodyView.X + half + 12, bodyView.Y, half, bodyView.Height);
            if (ready && !ViewChanged()) { RefreshItemPresentation(resources); RefreshDynamicPresentation(); RefreshNotice(); PrepareEditPresentation(); return; }
            // Only a structural projection/identity change retires an armed button.
            // Visible item values refresh in place on the fast path above.
            var pressed=armedKey==null?null:controls.Find(c=>c.Key==armedKey);
            controls.Clear(); labels.Clear(); groups.Clear(); actorHeaders.Clear(); selectedStatusLabel=respawnLabel=-1; icons.BeginVisibleSet();
            Text("AI 队友", new F5Rect(panel.X + 12, panel.Y + 4, width - 64, row));
            Button("close", "×", new F5Rect(panel.Right - 40, panel.Y + 4, 30, row), Close);
            BuildList(row);
            viewDataEpoch = CurrentProfile()?.DataEpoch ?? 0;
            Button("create", "＋", new F5Rect(listView.X, panel.Bottom - AvatarSize - 12, AvatarSize, AvatarSize), StartCreation);
            Button("inventory", "背包", new F5Rect(panel.Right-198,panel.Y+4,72,row), () => SelectPage(0), true, page == 0);
            Button("configuration", "配置", new F5Rect(panel.Right-120,panel.Y+4,72,row), () => SelectPage(1), true, page == 1);
            var retiredHeads=new List<Guid>();foreach(var id in railPreviews.Keys)if(!controls.Exists(c=>c.ProfileId==id))retiredHeads.Add(id);foreach(var id in retiredHeads)railPreviews.Remove(id);
            int bodyControlStart = controls.Count, bodyLabelStart = labels.Count;
            bodyOffsetClamped = false;
            BuildSelected(row);
            if (bodyOffsetClamped)
            {
                // Content height is independent of the scroll offset. Shrinking
                // content can clamp that offset only after the first projection;
                // replace those cells now, before hit testing or caching its epoch.
                controls.RemoveRange(bodyControlStart, controls.Count - bodyControlStart);
                labels.RemoveRange(bodyLabelStart, labels.Count - bodyLabelStart);
                icons.BeginVisibleSet(); groups.Clear(); actorHeaders.Clear(); selectedStatusLabel=respawnLabel=-1; bodyOffsetClamped = false;
                BuildSelected(row);
            }
            displayedNotice=NoticeText(); noticeLabel=labels.Count;
            labels.Add(new F5Element(F5ElementKind.Text,new F5Rect(bodyView.X,panel.Bottom-38,bodyView.Width,30),displayedNotice??"",default(F5Size),.65f,F5Command.None));
            icons.EndVisibleSet(); if (resources) icons.Prepare(); if(pressed!=null) { var replacement=controls.Find(c=>c.Key==pressed.Key); if(replacement==null||!replacement.Enabled||!replacement.Rect.Equals(pressed.Rect)){armedKey=null;generation++;} }
            ready = true; LayoutBuilds++; contentInvalidated=false; CaptureView(); RefreshDynamicPresentation(); PrepareEditPresentation();
        }
        private void BuildList(float row)
        {
            var profiles = host.Profiles;
            if(pendingCreatedId!=Guid.Empty) {
                if(pendingCreationToken!=host.CreationSessionToken){pendingCreatedId=Guid.Empty;pendingCreatedName=null;}
                else {selected=pendingCreatedId;for(int i=0;i<profiles.Count;i++)if(profiles[i].Id==pendingCreatedId){pendingCreatedId=Guid.Empty;pendingCreatedName=null;break;}}
            }
            int visible = Math.Max(1, (int)(listView.Height / AvatarStep));
            listMaximum = Math.Max(0, profiles.Count - visible); listOffset = Math.Max(0, Math.Min(listOffset, listMaximum));
            if (profiles.Count == 0) return;
            bool found = false; for (int i = 0; i < profiles.Count; i++) if (profiles[i].Id == selected) found = true;
            if (!found && pendingCreatedId==Guid.Empty) { selected = profiles[0].Id; confirmingReset = false; }
            for (int i = listOffset; i < Math.Min(profiles.Count, listOffset + visible); i++)
            {
                var profile = profiles[i]; Guid id = profile.Id;
                var rect = new F5Rect(listView.X, listView.Y + (i - listOffset) * AvatarStep, AvatarSize, AvatarSize);
                var c = Button("profile:" + id, profile.Name, rect, () => SelectProfile(id), true, selected == id);
                c.Portrait = host.GetBody(id); c.ProfileId=id; c.Joined=host.IsJoined(id);
                c.Hint = profile.Name + "\n" + host.Status(id);
                c.Element = null;

            }
        }
        private void SelectProfile(Guid id)
        { host.CancelItemGesture(); CancelEdit(); pendingCreatedId=Guid.Empty;pendingCreatedName=null; selected = id; confirmingReset = false; bodyOffset = 0; notice = null; Invalidate(); }
        private void SelectPage(int value)
        { host.CancelItemGesture(); CancelEdit(); confirmingReset = false; page = value; bodyOffset = 0; Invalidate(); }
        private void Invalidate() { armedKey = null; generation++; }
        private void Translate(float x, float y)
        {
            panel = panel.Offset(x, y); listView = listView.Offset(x, y); bodyView = bodyView.Offset(x, y);
            titleBar = titleBar.Offset(x, y); field = field.Offset(x, y); contentY += y;
            ownerView = ownerView.Offset(x,y); companionView = companionView.Offset(x,y); ownerHero = ownerHero.Offset(x,y); companionHero = companionHero.Offset(x,y);
            ownerShowcase=ownerShowcase.Offset(x,y);companionShowcase=companionShowcase.Offset(x,y);
            ownerShowcasePanel=ownerShowcasePanel.Offset(x,y);companionShowcasePanel=companionShowcasePanel.Offset(x,y);
            for(int i=0;i<actorHeaders.Count;i++)actorHeaders[i]=actorHeaders[i].Offset(x,y);
            for(int i=0;i<groups.Count;i++) groups[i]=groups[i].Offset(x,y);
            foreach (var c in controls) { c.Rect = c.Rect.Offset(x, y); if (c.Element != null) c.Element = Move(c.Element, x, y); }
            for (int i = 0; i < labels.Count; i++) labels[i] = Move(labels[i], x, y);
            if (editElement != null) editElement = Move(editElement, x, y);
            hint.Hide();
        }
        private static F5Element Move(F5Element element, float x, float y)
        { return new F5Element(element.Kind, element.Rect.Offset(x, y), element.Text, element.TextSize, element.TextScale,
            element.Command, element.HotkeyTarget, element.Description, element.HintRect.Offset(x, y)); }
        private F5Size MeasureCached(string text, float scale)
        {
            if (scale != .7f && scale != .65f) return measureSource(text, scale);
            var cache = scale == .7f ? sizes70 : sizes65; F5Size size;
            if (!cache.TryGetValue(text, out size))
            { if (cache.Count >= 256) cache.Clear(); cache[text] = size = measureSource(text, scale); }
            return size;
        }
        // Structural facts alone invalidate geometry. Slot values and current
        // resource values have independent presentation updates; neither an
        // unrelated count change nor a save message retires an armed button.
        private bool ViewChanged()
        {
            if (contentInvalidated || preparedGeneration != generation || editing != (editor != null) || !ReferenceEquals(presentedOwner,Main.LocalPlayer) || presentedOwnerName != Main.LocalPlayer?.name) return true;
            var profiles = host.Profiles;
            if (profiles.Count != previousProfileCount) return true;
            for (int i = 0; i < visibleIds.Count; i++)
            {
                int index = listOffset + i;
                if (index >= profiles.Count || profiles[index].Id != visibleIds[i] || profiles[index].Name != visibleNames[i] ||
                    !ReferenceEquals(host.GetBody(profiles[index].Id), visibleBodies[i])) return true;
            }
            var profile = CurrentProfile(); var body = host.GetBody(selected);
            if ((profile?.DataEpoch??0)!=viewDataEpoch || page!=0 && ((profile?.Revision ?? 0) != profileRevision || host.SaveFailed(selected)!=saveFailed) || !ReferenceEquals(body, selectedBody) || host.IsJoined(selected)!=memberJoined) return true;
            if (resourceStyle != (Main.ResourceSetsManager?.ActiveSetKeyName??"Default") || ownerLifeMax != (Main.LocalPlayer?.statLifeMax2??0) || ownerManaMax != (Main.LocalPlayer?.statManaMax2??0) || body != null && (page!=0 && weapon != body.selectedItem || maximumLife != body.statLifeMax2 || maximumMana != body.statManaMax2)) return true;
            return false;
        }
        private string NoticeText()
        { return host.HasItemGesture?"未放完时，离开窗口会取消本次操作。":notice??textInput.Error??editor?.Error??host.Feedback; }
        private void RefreshNotice()
        {
            string next=NoticeText(); if(next==displayedNotice||noticeLabel<0||noticeLabel>=labels.Count) return;
            var old=labels[noticeLabel];displayedNotice=next;
            labels[noticeLabel]=new F5Element(old.Kind,old.Rect,next??"",old.TextSize,old.TextScale,F5Command.None);
        }
        private void CaptureView()
        {
            resourceStyle=Main.ResourceSetsManager?.ActiveSetKeyName??"Default"; ownerLifeMax=Main.LocalPlayer?.statLifeMax2??0; ownerManaMax=Main.LocalPlayer?.statManaMax2??0; presentedOwner=Main.LocalPlayer;presentedOwnerName=Main.LocalPlayer?.name; preparedGeneration = generation; editing = editor != null;
            var profiles = host.Profiles; previousProfileCount = profiles.Count;
            visibleIds.Clear(); visibleNames.Clear(); visibleStatuses.Clear(); visibleBodies.Clear();
            int count = Math.Max(1, (int)(listView.Height / AvatarStep));
            for (int i = listOffset; i < Math.Min(profiles.Count, listOffset + count); i++)
            { var p = profiles[i]; visibleIds.Add(p.Id); visibleNames.Add(p.Name); visibleStatuses.Add(host.Status(p.Id)); visibleBodies.Add(host.GetBody(p.Id)); }
            profileRevision = CurrentProfile()?.Revision ?? 0; selectedBody = host.GetBody(selected); memberJoined=host.IsJoined(selected);saveFailed=host.SaveFailed(selected);
            if (selectedBody != null) { maximumLife = selectedBody.statLifeMax2; maximumMana = selectedBody.statManaMax2; weapon = selectedBody.selectedItem; }
        }
        private Control Button(string key, string text, F5Rect rect, Action action, bool enabled = true, bool selectedValue = false, bool off = false)
        {
            var c = new Control { Key = key + "/" + selected + "/" + viewDataEpoch, Text = text, Rect = rect, Execute = action, Enabled = enabled, Selected = selectedValue, Off = off,
                Element = new F5Element(F5ElementKind.Button, rect, text, measure(text, .7f), .7f, F5Command.None) };
            controls.Add(c); return c;
        }
        private void Text(string text, F5Rect rect, float scale = .7f)
        {
            if (string.IsNullOrEmpty(text)) return;
            labels.Add(new F5Element(F5ElementKind.Text, rect, text, default(F5Size), scale, F5Command.None));
        }
        internal void Process(bool active, KeyboardState keys, Vector2 point, Matrix transform, Vector2 physical, bool focused, int wheel)
        { ProcessCore(active, keys, point, transform, physical, focused, wheel, true); }
        internal void ProcessLayoutInput(bool active, KeyboardState keys, Vector2 point, Matrix transform, Vector2 physical, bool focused, int wheel)
        { ProcessCore(active, keys, point, transform, physical, focused, wheel, false); }
        private void ProcessCore(bool active, KeyboardState keys, Vector2 point, Matrix transform, Vector2 physical, bool focused, int wheel, bool resources)
        {
            pointer = point; OwnsPointer = ConsumeWheel = false;
            bool left = PlayerInput.MouseInfo.LeftButton == ButtonState.Pressed, right = PlayerInput.MouseInfo.RightButton == ButtonState.Pressed;
            ConsumeLeft = leftTail; ConsumeRight = rightTail;
            bool wasEditing = editor != null;
            textInput.AfterSample(active && Visible && focused, null, keys, focused);
            if (!active || !focused)
            {
                if (!focused) CancelForFocusLoss(); else Close();
                previousLeft = focused ? left : true; ReleaseTails(left, right, focused); return;
            }
            if (!Visible) { previousLeft = left; ReleaseTails(left, right, true); return; }
            if (!wasEditing && editor == null && input.Hotkeys.IsNew((int)Keys.Escape)) { input.ConsumeCancellation((int)Keys.Escape); Close(); ReleaseTails(left, right, true); return; }
            host.ValidateItemGesture();
            if (!panel.Contains(point.X, point.Y)) host.CancelItemGesture();
            PrepareCore(transform, physical, resources);
            bool wasDragging = dragging;
            if (dragging)
            {
                leftTail = true;
                if (left) { requestedPosition = point - grabOffset; PrepareCore(transform, physical, resources); }
                else dragging = false;
            }
            OwnsPointer = Captured || Contains(point.X, point.Y);
            if (OwnsPointer) { if (left) leftTail = true; if (right) rightTail = true; }
            if (wheel != 0 && Contains(point.X, point.Y))
            {
                if (!dragging && listView.Contains(point.X, point.Y)) listOffset = Math.Max(0, Math.Min(listMaximum, listOffset - Math.Sign(wheel)));
                else if (!dragging && page == 0 && ownerView.Contains(point.X, point.Y)) ownerOffset = Math.Max(0, Math.Min(ownerMaximum, ownerOffset - Math.Sign(wheel) * 48));
                else if (!dragging && bodyView.Contains(point.X, point.Y)) bodyOffset = Math.Max(0, Math.Min(bodyMaximum, bodyOffset - Math.Sign(wheel) * 48));
                ConsumeWheel = true; Invalidate(); PrepareCore(transform, physical, resources);
            }
            Control hit = null;
            if (ready) foreach (var c in controls) if (c.Enabled && c.Rect.Contains(point.X, point.Y)) { hit = c; break; }
            ProcessItemInput(hit, keys, left, right, wasDragging,resources);
            if (left && !previousLeft && !wasDragging && !(hit?.IsSlot ?? false))
            {
                if (hit == null && ready && titleBar.Contains(point.X, point.Y))
                {
                    dragStartPosition = requestedPosition; dragStartPositioned = userPositioned;
                    requestedPosition = new Vector2(panel.X, panel.Y); grabOffset = point - requestedPosition;
                    dragging = userPositioned = true; armedKey = null;
                }
                else { armedKey = hit?.Key; armedGeneration = generation; }
            }
            if (!wasDragging && !left && previousLeft && armedKey != null && armedGeneration == generation && hit?.Key == armedKey)
            {
                var execute = hit.Execute; armedKey = null; execute?.Invoke();
                if (Visible) PrepareCore(transform, physical, resources);
            }
            ReleaseTails(left, right, true); previousLeft = left; previousRight = right; PrepareHint();
        }
        private void ReleaseTails(bool left, bool right, bool focused)
        {
            ConsumeLeft = leftTail; ConsumeRight = rightTail;
            if (!focused) return;
            if (!left) { leftTail = false; armedKey = null; } if (!right) rightTail = false;
        }
        public bool RequestFinish()
        {
            if (editor == null) return true;
            textInput.FinishComposition(false); if (textInput.HasComposition) return false;
            host.ConnectionPassword = editor.Text;
            CancelEdit(); notice = null; Invalidate(); return true;
        }
        private void BeginEdit(string kind, string value)
        {
            if (editor != null && editKind == kind) return;
            CancelEdit(); notice = null; editKind = kind; editor = new TextEditBuffer(value, true, kind == "name" ? Player.nameLen : 256, 1024, "输入内容过长。");
            editor.SelectAll(); textInput.PrepareEditor();
        }
        public void CancelEdit() { textInput.Release(true); editor = null; editKind = null; editRevision = -1; editElement = null; }
        public void PreserveUncommittedInput(TextEditBuffer value) { }
        private void PrepareEditPresentation()
        {
            if (editor == null || field.Width <= 0) { editElement = null; return; }
            if (editRevision == editor.Revision && caretRevision == editor.CaretRevision && editComposition == textInput.Composition && editWidth == field.Width &&
                editElement != null && editElement.Rect.X == field.X + 6 && editElement.Rect.Y == field.Y + (field.Height - editView.Size.Height) / 2) return;
            TextEditBuffer display = editor; string composition = textInput.Composition;
            if (editKind == "password")
            {
                // The prepared glyph view and its bounded metrics cache contain
                // only mask glyphs, including the native IME composition preview.
                display = new TextEditBuffer(new string('•', editor.Text.Length), true, 1024, 1024, "");
                display.MoveTo(editor.Anchor); display.MoveTo(editor.Caret, true);
                composition = new string('•', composition.Length);
            }
            editView.Prepare(display, composition, field.Width - 12, measure);
            editElement = new F5Element(F5ElementKind.Text, new F5Rect(field.X + 6, field.Y + (field.Height - editView.Size.Height) / 2,
                editView.Size.Width, editView.Size.Height), editView.Text, editView.Size, .7f, F5Command.None);
            editRevision = editor.Revision; caretRevision = editor.CaretRevision; editComposition = textInput.Composition; editWidth = field.Width;
        }
        private void PrepareHint()
        {
            string hintText = null; F5Rect target = default(F5Rect);
            if (Visible && ready && editor == null && !Captured) foreach (var c in controls)
                if (c.Hint != null && c.Rect.Contains(pointer.X, pointer.Y)) { hintText = c.Hint; target = c.Rect; break; }
            hint.Prepare(hintText, target, new F5Rect(8, 8, screen.X / matrix.M11 - 16, screen.Y / matrix.M11 - 16), renderer, measure);
        }
        internal void Draw()
        {
            if (!Visible || !ready || !renderer.ResourcesCurrent) return;
            if (page == 0)
            { ownerPreview.Prepare(Main.LocalPlayer); companionPreview.Prepare(host.GetBody(selected)); resourceDisplay.Prepare(); }

            renderer.Pass(matrix, panel, () =>
            {
                UiSurface.Panel(Main.spriteBatch,Terraria.GameContent.TextureAssets.MagicPixel.Value,panel,Terraria.GameContent.TextureAssets.InventoryBack.Value,new Color(165,180,205),roundedOuter:true);
                Fill(new F5Rect(listView.X,listView.Y,listView.Width,listView.Height),Color.Black*.12f);
                foreach(var header in actorHeaders)UiSurface.Panel(Main.spriteBatch,Terraria.GameContent.TextureAssets.MagicPixel.Value,header,Terraria.GameContent.TextureAssets.InventoryBack.Value,new Color(220,225,245),roundedOuter:true);
                foreach(var group in groups) {Fill(group,Color.Black*.12f);DrawFrame(group,Color.White*.15f);}
                foreach (var c in controls)
                {
                    if(c.ActorOverlay)continue;
                    bool hover = c.Rect.Contains(pointer.X, pointer.Y);
                    if (c.Element != null) renderer.Button(c.Element, c.Selected, c.Enabled, c.Off, hover);
                    else renderer.ItemButton(c.Rect, c.Enabled, hover);
                    if (c.Portrait != null) {CompanionPreview head;if(!railPreviews.TryGetValue(c.ProfileId,out head))railPreviews[c.ProfileId]=head=new CompanionPreview();head.Prepare(c.Portrait);head.DrawHead(portraits,c.Rect,matrix,renderer.Generation);}
                    if(c.ProfileId!=Guid.Empty) { if(c.Selected) DrawAvatarSelection(c.Rect); if(c.Joined) DrawJoinedUnderline(c.Rect); }
                    else if (c.Selected && c.Element == null) renderer.Selection(c.Rect);
                    if(c.Placeholder!=null) DrawCentered(c.Placeholder,c.Rect,new Color(160,180,210),.7f);
                }
                PointDraw(()=> {
                    foreach(var c in controls) {
                        if(c.EmptyIcon>=0) DrawEmptyIcon(c.EmptyIcon,c.Rect);
                        if(c.ItemType>0) {renderer.PreparedItem(c.ItemType,icons.Get(c.ItemType),c.Rect);if(c.Stack>1)DrawStack(c.StackText,c.Rect);}
                    }
                });
                foreach (var e in labels) renderer.Text(e.Text, e.Rect, Color.White, e.TextScale);
                if (editor != null && field.Width > 0) DrawEditor();
                ScrollIndicator(listView, listOffset, listMaximum);
                if (page == 0) { ScrollIndicator(ownerView, ownerOffset, ownerMaximum); ScrollIndicator(companionView, bodyOffset, bodyMaximum); }
                else ScrollIndicator(bodyView, bodyOffset, bodyMaximum);
            });
            if (page == 0)
            {
                DrawHero(ownerView, ownerHero,ownerShowcase,ownerShowcasePanel, ownerPreview, Main.LocalPlayer);
                DrawHero(companionView, companionHero,companionShowcase,companionShowcasePanel, companionPreview, host.GetBody(selected));
                renderer.Pass(matrix,bodyView,()=>{foreach(var c in controls)if(c.ActorOverlay)DrawActorControl(c);});
            }

            if (host.HasItemGesture && host.CursorItem != null && !host.CursorItem.IsAir)
                renderer.Pass(matrix,panel,()=>PointDraw(()=> {var r=new F5Rect(pointer.X+10,pointer.Y+10,40,40);renderer.Item(host.CursorItem.type,r);if(host.CursorItem.stack>1)DrawStack(host.CursorItem.stack.ToString(),r);}));
            if (hint.Visible && editor == null && !Captured) renderer.Pass(matrix, hint.Panel, () => { renderer.Panel(hint.Panel); foreach (var line in hint.Lines) renderer.Label(new F5Element(F5ElementKind.Text,line.Rect.Offset(hint.Panel.X+8,hint.Panel.Y+8),line.Text,line.TextSize,line.TextScale,F5Command.None)); });
        }
        private void DrawEditor()
        {
            if (editElement == null) return;
            var pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;
            if (editView.SelectionRight > editView.SelectionLeft)
                Main.spriteBatch.Draw(pixel, new Vector2(field.X + 6 + editView.SelectionLeft, field.Y + 4), new Rectangle(0, 0, 1, 1), Color.CornflowerBlue, 0,
                    Vector2.Zero, new Vector2(editView.SelectionRight - editView.SelectionLeft, field.Height - 8), Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            renderer.Label(editElement);
            Main.spriteBatch.Draw(pixel, new Vector2(field.X + 6 + editView.Caret, field.Y + 5), new Rectangle(0, 0, 1, 1), Color.White, 0,
                Vector2.Zero, new Vector2(1, field.Height - 10), Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            Main.instance.SetIMEPanelAnchor(new Vector2(field.X + 6 + editView.Caret, field.Bottom + 32), 0);
        }
        private void ScrollIndicator(F5Rect view, int offset, int maximum)
        {
            if (maximum <= 0) return;
            renderer.Divider(new F5Rect(view.Right + 2, view.Y, 2, view.Height));
            renderer.Divider(new F5Rect(view.Right + 1, view.Y + (view.Height - 18) * offset / maximum, 4, 18));
        }
    }
}
