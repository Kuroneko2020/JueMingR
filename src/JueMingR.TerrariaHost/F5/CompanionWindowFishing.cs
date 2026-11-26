using System;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.Fishing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class CompanionWindow
    {
        private FishingPresentation fishingPane;
        private bool fishingPage, shownFishing, shownFishingReady;
        private float fishingFilterTop;
        private bool FishingPageVisible => Visible && ready && page == 1 && fishingPage && !PasswordPromptVisible;

        private void SelectFishingPage(bool value)
        {
            if (fishingPage == value) return;
            RetireConfigurationInput();
            fishingPage = value; bodyOffset = 0; notice = null; Invalidate();
        }

        private void BuildFishingRows(CompanionProfile profile, float row)
        {
            var behavior = profile.Behavior;
            ConfigurationPair(profile, behavior, CompanionSetting.FishingMode, CompanionSetting.FishingEquipment);
            ConfigurationPair(profile, behavior, CompanionSetting.FishingStorage, CompanionSetting.FishingCut);
            ConfigurationPair(profile, behavior, CompanionSetting.FishingTeleport, CompanionSetting.FishingLoadout);
            if (host.SaveFailed(profile.Id))
                ConfigurationAction("retry-fishing-save", "保存失败 · 重试保存", () => Command(() => host.RetrySave(profile.Id)), row);
            fishingFilterTop = contentY + bodyOffset - configurationViewport.Y;
        }

        private void PrepareFishingPane(Matrix transform, Vector2 physical, bool resources)
        {
            var profile = FishingPageVisible ? CurrentProfile() : null;
            if (profile == null)
            { RetireFishingPane(); return; }
            if (fishingPane == null) fishingPane = new FishingPresentation(host, measure);
            host.SetFishingPage(profile.OwnerId, profile.Id);
            fishingPane.PointAt(profile, host.CreationSessionToken, generation, panel, configurationViewport, bodyOffset - fishingFilterTop);
            if (resources) fishingPane.Prepare(true, transform, physical);
            else fishingPane.PrepareLayout(transform, physical);
            int maximum = Math.Max(0, (int)Math.Ceiling(fishingFilterTop + fishingPane.FilterHeight - configurationViewport.Height));
            bodyMaximum = maximum;
            if (bodyOffset <= maximum) return;
            bodyOffset = maximum; Invalidate();
            PrepareWindow(transform, physical, resources);
            fishingPane.PointAt(profile, host.CreationSessionToken, generation, panel, configurationViewport, bodyOffset - fishingFilterTop);
            if (resources) fishingPane.Prepare(true, transform, physical);
            else fishingPane.PrepareLayout(transform, physical);
        }

        private bool ProcessFishingPane(KeyboardState keys, Vector2 point, bool focused, int wheel, bool left, bool right)
        {
            if (fishingPane == null) return false;
            bool popup = fishingPane.Visible, editingFishing = fishingPane.Editor != null;
            fishingPane.ProcessPopup(true, keys, point, ready, focused, wheel);
            fishingPane.ProcessInput(true, keys, point, ready, focused, dragging, wheel);
            bool escape = (popup || editingFishing) && input.Hotkeys.IsNew((int)Keys.Escape);
            if (escape) input.ConsumeCancellation((int)Keys.Escape);
            if (!escape && !fishingPane.OwnsPopupGesture) return false;

            // Closing an editor cannot replay the press/release against the
            // configuration buttons or the Terraria world underneath it.
            OwnsPointer = fishingPane.OwnsPointer || fishingPane.BlockPointer;
            ConsumeWheel = fishingPane.ConsumeWheel || popup && fishingPane.BlocksPageWheel;
            leftTail |= fishingPane.ConsumeLeft;
            rightTail |= fishingPane.ConsumeRight;
            armedKey = null; previousLeft = left; previousRight = right;
            ReleaseTails(left, right, focused);
            PrepareHint();
            return true;
        }

        private void RetireFishingPane()
        {
            if (fishingPane == null) return;
            leftTail |= fishingPane.ConsumeLeft; rightTail |= fishingPane.ConsumeRight;
            fishingPane.Dispose(); fishingPane = null;
            host.SetFishingPage(Guid.Empty, Guid.Empty);
        }
    }
}
