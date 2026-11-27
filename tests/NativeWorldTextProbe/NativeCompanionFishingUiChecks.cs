using System;
using System.Collections;
using System.Linq;
using JueMingR.Platform.Companions;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // The layout-only inlet shares the window's real controls, pointer
    // ownership and file-backed profiles without assuming a graphics adapter.
    internal static class NativeCompanionFishingUiChecks
    {
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
            var screen = new Vector2(1280, 900);
            Call(renderer, "RefreshResources");
            Call(renderer, "Prepare", state, screen.X, screen.Y, 1f);
            NativeQuickItemChecks.Until(() => { Call(context,"UpdateRuntime"); return (bool)Get(archive,"Ready"); });
            CompanionProfile Create(string name)
            {
                Require((bool)Call(host, "Create", name), "fishing preferences use a real owned profile");
                var id = (Guid)Get(host, "LastCreatedId");
                NativeQuickItemChecks.Until(() => { Call(host, "Update", 0UL); return (bool)Call(archive,"IsSaved",id); });
                return NativeCompanionConfigurationChecks.Profile(host, id);
            }

            var first = Create("钓鱼队友甲");
            var second = Create("钓鱼队友乙");
            var owner = first.OwnerId;
            CompanionProfile Current(Guid id) => NativeCompanionConfigurationChecks.Profile(host,id);
            void Sample(Vector2 point, bool left)
            {
                NativeToolsUiChecks.Mouse(point, left);
                Call(window, "ProcessLayoutInput", true, new KeyboardState(), point, Matrix.Identity, screen, true, 0);
            }
            object Control(string key) => ((IEnumerable)Get(window, "controls")).Cast<object>().Single(c =>
                ((string)Get(c, "Key")).StartsWith(key + "/", StringComparison.Ordinal));
            void Click(Vector2 at)
            { Sample(at, false); Sample(at, true); Sample(at, false); }
            void ClickControl(string key) => Click(NativeToolsUiChecks.Point(Get(Control(key), "Rect")));
            Vector2 ControlPosition(string key) => NativeToolsUiChecks.Point(Get(Control(key), "Rect"));
            void Prepare() => Call(window, "PrepareLayout", Matrix.Identity, screen);

            try
            {
                NativeToolsUiChecks.Mouse(Vector2.Zero, false);
                Call(window,"Toggle",Get(layout,"Viewport")); Prepare();
                ClickControl("profile:" + first.Id);
                ClickControl("configuration");
                var bounds = Get(window,"panel");
                ClickControl("configuration-fishing");
                Require(Get(window,"panel").Equals(bounds) && (Guid)Get(host,"fishingPageId") == first.Id,
                    "the fishing subpage preserves the management rectangle and pauses only its selected profile");
                var modeRow = ControlPosition("config-FishingMode/开启");
                var storageRow = ControlPosition("config-FishingStorage/所有");
                var teleportRow = ControlPosition("config-FishingTeleport/开启");
                var equipment = ControlPosition("config-FishingEquipment/开启");
                var loadout = ControlPosition("config-FishingLoadout/开启");
                var cut = ControlPosition("config-FishingCut/开启");
                Require(modeRow.Y == equipment.Y && storageRow.Y == loadout.Y && teleportRow.Y == cut.Y &&
                    equipment.Y < loadout.Y && loadout.Y < cut.Y && equipment.X > modeRow.X &&
                    loadout.X > storageRow.X && cut.X > teleportRow.X,
                    "right column aligns equipment, loadout and cut with mode, storage and teleport, in that order");
                Require(!(bool)Get(Control("config-FishingMode/开启"),"Enabled"), "offline profiles can edit preferences but cannot start the activity");
                var hotkeys = Get(shell, "hotkeys");
                Call(Get(hotkeys,"CompanionSettings"),"Refresh",host,Get(hotkeys,"Bindings"),first.Id);
                var registry = (HotkeyRegistry)Get(hotkeys,"Registry");
                string prefix = "companions.profile." + owner.ToString("N") + "." + first.Id.ToString("N") + ".";
                var fishingActions = registry.Actions.Where(action => action.Id.StartsWith(prefix + "fish", StringComparison.Ordinal)).ToArray();
                Require(fishingActions.Length == 6 && fishingActions.All(action => HotkeyAction.ValidId(action.Id)),
                    "all six actual dynamic registrations honor the complete stored profile identity and ID limit");
                var storageKey = fishingActions.Single(action => action.Id == prefix + "fish-store");
                ClickControl("config-FishingEquipment/开启");
                Require(Current(first.Id).Fishing.Equipment && !Current(second.Id).Fishing.Equipment, "the equipment choice belongs to the selected profile");
                ClickControl("config-FishingLoadout/开启");
                Require(Current(first.Id).Fishing.Loadout && !Current(first.Id).Fishing.Equipment, "the actual second strategy command retires the first");
                ClickControl("config-FishingStorage/任务鱼");
                Require(storageKey.Invoke(HotkeyContext.SinglePlayer), "the real registered storage action dispatches to the selected profile");
                Require(Current(first.Id).Fishing.StoreMode == 0, "the binding command can close task-fish storage");
                Require(storageKey.Invoke(HotkeyContext.SinglePlayer), "the unchanged action survives profile revision and dispatches a second time");
                Prepare();
                Require(Current(first.Id).Fishing.StoreMode == 2 && Current(second.Id).Fishing.StoreMode == 0,
                    "the same binding command restores only that profile's previous storage mode");

                var pane = Get(window,"fishingPane");
                object FilterPart(string command) => ((IEnumerable)Get(pane,"Parts")).Cast<object>().First(p => Get(p,"Command").ToString() == command);
                Vector2 PartPoint(string command) => NativeToolsUiChecks.Point(Get(Get(FilterPart(command),"Element"),"Rect"));
                Click(PartPoint("Filter")); Prepare();
                Click(PartPoint("Match")); Prepare();
                Require(Current(first.Id).Fishing.FilterMode == 1 && Current(first.Id).Fishing.Match == 1, "the shared filter mode and match controls save to the AI instead of the owner");
                Click(PartPoint("Plus")); Prepare();
                Require(Get(pane,"Editor") != null && (bool)Get(pane,"Visible"), "the shared keyword editor owns a real draft");
                // The first press on the rail dismisses the popup and owns its
                // release, the next press selects the other character.
                ClickControl("profile:" + second.Id);
                Require((Guid)Get(window,"selected") == first.Id && GetOptional(pane,"Editor") == null, "dismissing the draft consumes its physical release");
                ClickControl("profile:" + second.Id);
                Require((Guid)Get(host,"fishingPageId") == second.Id && Current(second.Id).Fishing.FilterMode == 0 && Current(second.Id).Fishing.Match == 0,
                    "selection transfers the fishing page to the second profile with its original scope");
                pane = Get(window,"fishingPane");
                long builds = (long)Get(pane,"LayoutBuilds");
                for (int sample = 0; sample < 120; sample++) Prepare();
                Require((long)Get(pane,"LayoutBuilds") == builds, "120 stable pane prepares reuse measured filter geometry");
                ClickControl("configuration-general");
                Require((Guid)Get(host,"fishingPageId") == Guid.Empty && Get(window,"panel").Equals(bounds),
                    "leaving the subpage resumes the old actor and preserves the outside rectangle");
                Console.WriteLine("PASS native companion fishing layout, per-profile options, excluded offline start, filter editor handoff, pause identity and stable work.");
            }
            finally { Call(window,"Close"); NativeToolsUiChecks.Mouse(Vector2.Zero,false); }
        }
    }
}
