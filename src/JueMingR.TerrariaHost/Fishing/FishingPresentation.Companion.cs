using System;
using JueMingR.Features.Fishing;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.Companions;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Notes;

namespace JueMingR.TerrariaHost.Fishing
{
    internal sealed partial class FishingPresentation
    {
        private readonly HostCompanions companion;
        private readonly Func<string, float, F5Size> companionMeasure;
        private Guid companionOwner, companionProfile;
        private long companionEpoch, companionWorld, companionSession, companionRevision;
        private int companionGeneration;
        private bool companionCanConfigure;
        private float companionScroll;
        private F5Rect companionViewport, companionWindow;
        private FishingOptions companionOptions, companionWrite;

        internal FishingPresentation(HostCompanions companion, Func<string, float, F5Size> measure)
            : this((INotesIme)null)
        { this.companion = companion; companionMeasure = measure; }

        internal float FilterHeight { get; private set; }
        internal bool OwnsPopupGesture => framePopup || ConsumeLeft || ConsumeRight || ConsumeWheel;

        // The popup and input lease belong to the exact visible profile and
        // data epoch. Saving creates a new snapshot, while replacing the
        // character or switching view invalidates unconsumed input and receipts.
        internal void PointAt(CompanionProfile profile, long world, int stamp, F5Rect window, F5Rect clip, float offset)
        {
            Guid owner = profile?.OwnerId ?? Guid.Empty, id = profile?.Id ?? Guid.Empty;
            long nextEpoch = profile?.DataEpoch ?? 0;
            if (owner != companionOwner || id != companionProfile || nextEpoch != companionEpoch || world != companionWorld)
            {
                Suspend();
                TextInput.ForgetRetiredDraft();
                pending = 0; companionWrite = null;
                companionOwner = owner; companionProfile = id; companionEpoch = nextEpoch; companionWorld = world;
                companionSession++;
            }
            companionGeneration = stamp;
            companionCanConfigure = id != Guid.Empty && companion.CanUseFishingPreferences(id);
            ReadCompanionOptions(profile?.Fishing ?? CompanionBehavior.DefaultFishing);
            companionWindow = window; companionViewport = clip; companionScroll = offset;
        }

        private void ReadCompanionOptions(FishingOptions next)
        {
            if (ReferenceEquals(companionOptions, next)) return;
            companionOptions = next; companionRevision++;
        }
        private FishingOptions Value => host?.Settings.Value ?? companionOptions;
        private long ValueRevision => host?.Settings.Revision ?? companionRevision;
        private bool Controls => host != null ? host.Controls : PresentationAvailable;
        private long RenameRevision => host?.Rename.Revision ?? -1;
        private bool RenameAllowed => host?.Rename.CanRename ?? false;
        private long PaneSession => host?.Tools.Runtime.Generation ?? companionSession;
        private int PaneGeneration => shell?.Layout.Generation ?? companionGeneration;
        private bool PaneVisible => shell != null ? shell.Visible && shell.Page == 7 : companionProfile != Guid.Empty;
        private float PaneScroll => shell?.Scroll ?? companionScroll;
        private F5Rect PaneViewport => shell != null ? shell.Layout.Viewport.Offset(shell.X, shell.Y) : companionViewport;
        private F5Rect PaneWindow
        {
            get
            {
                if (shell == null) return companionWindow;
                var size = F5Layout.WindowSize(screen.X, screen.Y, matrix.M11);
                return new F5Rect(shell.X, shell.Y, size.Width, size.Height);
            }
        }

        private F5Size Measure(string text, float scale) =>
            shell != null ? shell.Layout.TextSize(text, scale) : companionMeasure(text, scale);
        private F5Size DynamicMeasure(string text, float scale) =>
            shell != null ? shell.Layout.DynamicTextSize(text, scale) : companionMeasure(text, scale);
        private FishKey[] Search(string query) => host != null ? host.Catalog.Search(query) : companion.SearchCompanionFishing(query);

        private void SetFeature(int feature, int state)
        {
            if (host != null) host.Set(feature, state);
            else SetCompanionValue(Value.Change(feature, state));
        }

        private bool SetCompanionValue(FishingOptions options)
        {
            var expected = companionOptions;
            if (expected == null || !companion.SetFishingPreferences(companionOwner, companionProfile, expected, options))
            { message = "钓鱼设置未更改，请重试。"; return false; }
            ReadCompanionOptions(options);
            return true;
        }
    }
}
