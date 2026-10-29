using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ObjectData;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        internal static void ConstructionWiring(object context, object host, object tools, object input)
        {
            ConstructionConsumerCase("color wrenches",()=>ConstructionWireColors(context, host, tools, input, false));
            ConstructionConsumerCase("multi wrench",()=>ConstructionWireColors(context, host, tools, input, true));
            ConstructionConsumerCase("temporary actuator",()=>ConstructionActuator(context, host, tools, input, false));
            ConstructionConsumerCase("existing actuator",()=>ConstructionActuator(context, host, tools, input, true));
            ConstructionConsumerCase("external actuator replacement",()=>ConstructionActuatorProvenance(context, host, tools, input));
            ConstructionConsumerCase("Grand Design commit exact debit",()=>ConstructionGrandDesign(context, host, tools, input, false));
            ConstructionConsumerCase("Grand Design native right cancel",()=>ConstructionGrandDesign(context, host, tools, input, true));
            ConstructionConsumerCase("Grand Design unauthorized SP fallback",()=>ConstructionGrandDesign(context, host, tools, input, true, true));
            ConstructionConsumerCase("Grand Design unauthorized109 fallback",()=>ConstructionGrandDesign(context, host, tools, input, true, true, true));
            ConstructionConsumerCase("actual wire range fallback and continuous tool",()=>ConstructionWiringToolSelection(context, host, tools, input, false));
            ConstructionConsumerCase("actual actuator range fallback and primary debit",()=>ConstructionWiringToolSelection(context, host, tools, input, true));
            ConstructionConsumerCase("owned actuator cleanup range fallback",()=>ConstructionActuatorCleanupTool(context, host, tools, input));
            ConstructionConsumerCase("native109/110 and pending slot0",()=>ConstructionWiringNetwork(context, host, tools, input));
            ConstructionConsumerCase("native110 correlation loss",()=>ConstructionWiringNetwork(context, host, tools, input, true));
            Console.WriteLine("PASS BlueprintConstructionWiring: native colors, multi-wrench, actuator provenance, owned651 single-point/cancel, original109/server MassWire/110; sockets/friend-host-client/GPU/FPS NOT_RUN");
        }
        internal static void ConstructionWiringActuatorNetwork(object context, object host, object tools, object input)
        {
            ConstructionConsumerCase("single-player temporary actuator regression", () => ConstructionActuator(context, host, tools, input, false));
            ConstructionConsumerCase("client GD temporary actuator ordered native confirmation and cleanup", () => ConstructionTemporaryActuatorNetwork(context, host, tools, input, 0));
            ConstructionConsumerCase("client GD count0 with actual existing actuator", () => ConstructionTemporaryActuatorNetwork(context, host, tools, input, 1));
            ConstructionConsumerCase("client GD observed actuator ABA", () => ConstructionTemporaryActuatorNetwork(context, host, tools, input, 2));
            ConstructionConsumerCase("client GD retired projection late native replies", () => ConstructionTemporaryActuatorNetwork(context, host, tools, input, 3));
            Console.WriteLine("PASS BlueprintTemporaryActuatorNetwork: original651/109, server MassWire/17/110, client actual17 installation and110 debit, rod/cutter cleanup, count0/ABA/retired guards; sockets/friend-host-client/GPU/FPS/owner acceptance NOT_RUN");
        }
        internal static void ConstructionSpecial(object context, object host, object tools, object input)
        {
            ConstructionConsumerCase("finite cycle and recipe contract",ConstructionCycleContract);
            ConstructionConsumerCase("tile wand last material",()=>ConstructionTileWand(context, host, tools, input));
            ConstructionConsumerCase("flexible bucket and variant",()=>ConstructionFlexible(context, host, tools, input));
            ConstructionConsumerCase("flexible last held item",()=>ConstructionWithoutBucketConsumption(context, host, tools, input));
            ConstructionConsumerCase("substrate native stages",()=>ConstructionSubstrates(context, host, tools, input));
            Console.WriteLine("PASS BlueprintConstructionSpecial: native wand depletion, exact flexible bucket/variant, held-only consumption and substrate stages; real multiplayer/GPU/FPS NOT_RUN");
        }
        private static void ConstructionConsumerCase(string name,Action body)
        { body();Console.WriteLine("PASS BlueprintConstruction consumer="+name); }
        private static void ConstructionCycleContract()
        {
            int cycle;
            var reachable = new BlueprintSpecialProducer(BlueprintSpecialProducerKind.Flexible, 2, true, false, 2, 6, 4, 0, 0);
            Require(reachable.TryCycle(7, out cycle) && cycle == 8 && reachable.TryCycle(-3, out cycle) && cycle == -4,
                "joint native option/random period handles nontrivial and negative cycles");
            var incompatible = new BlueprintSpecialProducer(BlueprintSpecialProducerKind.Flexible, 2, true, false, 2, 6, 4, 1, 1);
            Require(!incompatible.TryCycle(0, out cycle), "incompatible option/random residues have no exact cycle, never a speculative placement");
            BlueprintSubstrateRecipe recipe;
            Require(BlueprintSubstrateRecipe.TryGet(661, out recipe) && recipe.BaseTile == 59 && recipe.BaseItem == 176 && recipe.ConversionItem == 59 &&
                !BlueprintSubstrateRecipe.TryGet(999, out recipe), "finite mud-to-corrupt-grass recipe retains original seed identity and does not invent generic conversion");
        }
        private static void ConstructionWireColors(object context, object host, object tools, object input, bool multi)
        {
            var p = Reset(context, host, tools, input);
            p.inventory[12].SetDefaults(multi ? ItemID.MulticolorWrench : ItemID.BlueWrench);
            if (!multi) p.inventory[13].SetDefaults(ItemID.GreenWrench);
            p.inventory[20].SetDefaults(ItemID.Wire); p.inventory[20].stack = 3;
            WorldGen.PlaceWire(44, 42); WorldGen.PlaceWire4(44, 42);
            var oldMode = WiresUI.Settings.ToolMode;
            WiresUI.Settings.ToolMode = WiresUI.Settings.MultiToolMode.Yellow | WiresUI.Settings.MultiToolMode.Cutter | WiresUI.Settings.MultiToolMode.Actuator;
            try
            {
                var borrowed = WiresUI.Settings.ToolMode;
                var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Wire, Wires = 6 });
                Place(context, host, input, entry, 44, 42); SetAutomatic(host, input, true);
                Until(context, input, () => Scene(host).Length == 0);
                Require(Main.tile[44, 42].wire() && Main.tile[44, 42].wire2() && Main.tile[44, 42].wire3() && Main.tile[44, 42].wire4() &&
                    p.inventory[20].stack == 1 && p.inventory[12].stack == 1 && WiresUI.Settings.ToolMode == borrowed,
                    "blue/green map to their real native colors, spend exactly two wires, preserve unrequested colors and restore multi-tool mode; multi=" + multi);
            }
            finally { WiresUI.Settings.ToolMode = oldMode; }
        }
        private static void ConstructionActuator(object context, object host, object tools, object input, bool existing)
        {
            var p = Reset(context, host, tools, input);
            p.inventory[12].SetDefaults(ItemID.ActuationRod); p.inventory[13].SetDefaults(ItemID.WireCutter);
            p.inventory[20].SetDefaults(ItemID.Actuator); p.inventory[20].stack = 2;
            NativeToolsChecks.Tile(44, 42, TileID.Stone); WorldGen.PlaceWire2(44, 42);
            if (existing) WorldGen.PlaceActuator(44, 42);
            var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone, Inactive = true });
            Place(context, host, input, entry, 44, 42); SetAutomatic(host, input, true);
            if (!existing)
            {
                Until(context, input, () => Main.tile[44, 42].actuator());
                Require(p.inventory[20].type == ItemID.Actuator && p.inventory[20].stack == 1 &&
                    !Main.tile[44, 42].inActive() && ((IDictionary)Get(host, "ownedBuildActuators")).Count == 1,
                    "the first real temporary actuator spends exactly one item before rod, cleanup or any native pickup can occur");
            }
            Until(context, input, () => Scene(host).Length == 0);
            Require(Main.tile[44, 42].inActive() && Main.tile[44, 42].actuator() == existing && Main.tile[44, 42].wire2(),
                "real rod reaches Inactive and cutter removes only this action's proven precondition; existing=" + existing);
            Require(((IDictionary)Get(host, "ownedBuildActuators")).Count == 0,
                "settled actuator chain leaves no stale cleanup owner or retirement blocker");
            Require(existing ? p.inventory[20].stack == 2 : p.inventory[20].stack >= 1,
                "existing actuator costs no new item; own precondition uses original consumption and any pickup remains native");
        }
        private static void ConstructionActuatorProvenance(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input);
            p.inventory[12].SetDefaults(ItemID.ActuationRod); p.inventory[13].SetDefaults(ItemID.WireCutter);
            p.inventory[20].SetDefaults(ItemID.Actuator); p.inventory[20].stack = 2;
            NativeToolsChecks.Tile(44, 42, TileID.Stone); WorldGen.PlaceWire3(44, 42);
            var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone, Inactive = true });
            Place(context, host, input, entry, 44, 42); SetAutomatic(host, input, true);
            Until(context, input, () => Main.tile[44, 42].actuator());
            // A separate original world writer replaces the part between the
            // action's stages. It must revoke provenance, not become "ours".
            WorldGen.KillActuator(44, 42); WorldGen.PlaceActuator(44, 42);
            Until(context, input, () => Scene(host).Length == 0);
            Require(Main.tile[44, 42].actuator() && Main.tile[44, 42].wire3() && Main.tile[44, 42].inActive(),
                "external remove/re-add revokes the old cleanup right; neither the new actuator nor existing wire is cut");
        }
        private static void ConstructionStepWithProjectiles(object context, object input)
        {
            NativeToolExecutionChecks.Sample(context, input, new Vector2(480, 540), false);
            NativeQuickItemChecks.BeginWorldStep(); Main.LocalPlayer.Update(0);
            for (int i = 0; i < Main.projectile.Length; i++)
                if (Main.projectile[i].active && Main.projectile[i].type == 651 && Main.projectile[i].owner == Main.myPlayer) Main.projectile[i].Update(i);
            Call(context, "UpdateRuntime");
        }
        private static void ConstructionGrandDesign(object context, object host, object tools, object input, bool cancel, bool missingRightEdge = false, bool clientSubmission = false)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(3611);
            p.inventory[20].SetDefaults(ItemID.Wire); p.inventory[20].stack = 2;
            var oldMode = WiresUI.Settings.ToolMode; var oldScreen = Main.screenPosition; var mode = WiresUI.Settings.MultiToolMode.Green | WiresUI.Settings.MultiToolMode.Cutter;
            bool oldRight = Main.mouseRight, oldRightRelease = Main.mouseRightRelease;
            int oldNetMode = Main.netMode;
            var oldConnection=Netplay.Connection;var oldBuffer=NetMessage.buffer[256];
            WiresUI.Settings.ToolMode = mode;
            try
            {
                var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Wire, Wires = 2 });
                Place(context, host, input, entry, 44, 42); SetAutomatic(host, input, true);
                NativeQuickItemChecks.Until(() => { ConstructionStepWithProjectiles(context, input); return GetOptional(host, "grandBuild") != null && (bool)Get(Get(host, "grandBuild"), "OriginReady"); });
                var receipt = Get(host, "grandBuild"); var projectile = (Projectile)Get(receipt, "Projectile");
                Require(projectile.ai[0] == 44 && projectile.ai[1] == 42 && projectile.Center.ToTileCoordinates() == new Point(44, 42),
                    "this native651 establishes exactly the intended start and end before any commit");
                Main.screenPosition += new Vector2(11, 5);
                if (cancel)
                {
                    SetAutomatic(host, input, false);
                    // These are isolated fixture input preconditions. Production
                    // must never manufacture either release value. Execute the
                    // owned projectile's next native AI before another player's
                    // input opportunity can change the condition under test.
                    Main.mouseRight = false; Main.mouseRightRelease = !missingRightEdge; p.channel = false;
                    if (clientSubmission)
                    {
                        // Kill still emits its ordinary projectile29 after the
                        // denied109. Give that real serializer its native buffer
                        // and disconnected transport, not a null lock target.
                        NetMessage.buffer[256]=new MessageBuffer();
                        Netplay.Connection=new RemoteServer{PendingTermination=true,State=10};Main.netMode = 1;
                    }
                    projectile.Update(projectile.whoAmI);
                    Main.netMode = oldNetMode;
                    Require(!projectile.active && GetOptional(host, "grandBuild") == null &&
                        (bool)Get(receipt, "CancelRequested") && !(bool)Get(receipt, "Submitted"),
                        "lost authorization ends the exact owned651 without submitting its action; missingEdge=" + missingRightEdge + "; client=" + clientSubmission);
                    Require(projectile.localAI[0] == (missingRightEdge ? 1f : 0f) && !Main.mouseRight && Main.mouseRightRelease == !missingRightEdge,
                        missingRightEdge ? "unavailable right edge reaches vanilla's no-channel submit branch, whose unauthorized SP/109 consumer is refused; release remains untouched" :
                        "available right edge takes vanilla's earlier right-button cancel branch with localAI0 still zero and returns the borrowed press");
                    Require(GetOptional(host, "wiringReply") == null,
                        "a canceled owned651 never creates a late material-reply responsibility");
                }
                else NativeQuickItemChecks.Until(() => { ConstructionStepWithProjectiles(context, input); return GetOptional(host, "grandBuild") == null; });
                Require(WiresUI.Settings.ToolMode == mode && Main.projectile.Count(q => q.active && q.type == 651 && q.owner == Main.myPlayer) == 0,
                    "native651 lifetime ends and returns its short mode scope; cancel=" + cancel);
                Require(cancel ? !Main.tile[44, 42].wire2() && p.inventory[20].stack == 2 : Main.tile[44, 42].wire2() && p.inventory[20].stack == 1,
                    "normal Grand Design commits one blue point; stop cancels without consumption or a cursor path; cancel=" + cancel+
                    "; blue="+Main.tile[44,42].wire2()+"; wire="+p.inventory[20].type+"/"+p.inventory[20].stack);
                if (!cancel) Until(context, input, () => Scene(host).Length == 0);
            }
            finally
            {
                WiresUI.Settings.ToolMode = oldMode; Main.screenPosition = oldScreen; Main.netMode = oldNetMode;
                if(clientSubmission){NetMessage.buffer[256]=oldBuffer;Netplay.Connection=oldConnection;}
                Main.mouseRight = oldRight; Main.mouseRightRelease = oldRightRelease;
            }
        }
        private static void ConstructionWiringToolSelection(object context, object host, object tools, object input, bool actuator)
        {
            var p = Reset(context, host, tools, input);
            p.inventory[13].SetDefaults(3611); var grand = p.inventory[13];
            if (!actuator) p.inventory[12].SetDefaults(ItemID.BlueWrench);
            p.inventory[20].SetDefaults(actuator ? ItemID.Actuator : ItemID.Wire);
            p.inventory[20].stack = actuator ? 2 : 3; var supply = p.inventory[20];
            var shortTool = actuator ? supply : p.inventory[12];
            // Native color wrenches have tileBoost = 20. Point 70 is beyond
            // their reach at player tile 40, yet inside this fixture's screen.
            Require(!p.IsInTileInteractionRange(70, 42, TileReachCheckSettings.Simple, shortTool.tileBoost + p.blockRange) &&
                (bool)Call(host, "GrandBuildPointVisible", p, 70, 42),
                "the short real tool is present but out of native reach; the Grand Design point is actually visible");
            if (actuator) NativeToolsChecks.Tile(70, 42, TileID.Stone);
            var cells = actuator ? new[] { new CaptureCell { Layers = CaptureLayer.Tile | CaptureLayer.Actuator, TileType = TileID.Stone, Actuator = true } } :
                new[] { new CaptureCell { Layers = CaptureLayer.Wire, Wires = 2 }, new CaptureCell { X = 1, Layers = CaptureLayer.Wire, Wires = 2 } };
            var oldMode = WiresUI.Settings.ToolMode; var gestures = new List<object>(); bool selectedPlanChecked = false;
            long unknown = (long)Get(host, "BuildUnknownResults");
            Action observe = () => {
                var use = GetOptional(host, "grandBuild"); if (use == null || gestures.Contains(use)) return;
                gestures.Add(use); var flight = Get(use, "Flight"); var operation = Get(flight, "Operation");
                var primary = ((IEnumerable)Get(flight, "Dependencies")).Cast<object>().Single(value => (bool)Get(value, "Primary"));
                Require(ReferenceEquals(Get(Get(flight, "Source"), "Item"), grand) && Get(operation, "Consumer").ToString() == "GrandDesign" &&
                    (int)Get(operation, "HeldMaximum") == 0 && (int)Get(operation, "PrimaryMaximum") == 1 &&
                    ReferenceEquals(Get(primary, "Item"), supply) && (int)Get(primary, "Maximum") == 1,
                    "selected native651 uses the exact Grand Design and one primary-material budget, never a leaked short-tool held budget");
            };
            try
            {
                Place(context, host, input, Asset(host, cells), 70, 42); SetAutomatic(host, input, true);
                NativeQuickItemChecks.Until(() => {
                    ConstructionStepWithProjectiles(context, input); observe();
                    return GetOptional(host, "grandBuild") != null && (bool)Get(Get(host, "grandBuild"), "OriginReady");
                });
                if (!actuator)
                {
                    // Move only the isolated player after the first genuine
                    // gesture. Both choices are now legal, so the next same
                    // color must retain the currently feasible exact tool.
                    p.position = new Vector2(52 * 16, p.position.Y);
                    Require(p.IsInTileInteractionRange(70, 42, TileReachCheckSettings.Simple, shortTool.tileBoost + p.blockRange) &&
                        p.IsInTileInteractionRange(71, 42, TileReachCheckSettings.Simple, shortTool.tileBoost + p.blockRange),
                        "the short wrench becomes a real reachable alternative before the next continuous target");
                }
                NativeQuickItemChecks.Until(() => {
                    ConstructionStepWithProjectiles(context, input); observe();
                    if (!actuator && !selectedPlanChecked) selectedPlanChecked = CheckSelectedWiringPlan(host, p, shortTool, grand, supply);
                    return Scene(host).Length == 0;
                });
                Require((actuator || selectedPlanChecked) && gestures.Count == (actuator ? 1 : 2) && supply.stack == 1 && grand.stack == 1 &&
                    (actuator ? Main.tile[70, 42].actuator() && ((IDictionary)Get(host, "ownedBuildActuators")).Count == 0 :
                        Main.tile[70, 42].wire2() && Main.tile[71, 42].wire2() && shortTool.stack == 1) &&
                    WiresUI.Settings.ToolMode == oldMode && (long)Get(host, "BuildUnknownResults") == unknown,
                    "real651 completes the far target with exact debit; the feasible current tool continues and permanent actuators earn no cleanup right; actuator=" + actuator);
            }
            finally { WiresUI.Settings.ToolMode = oldMode; }
        }
        private static bool CheckSelectedWiringPlan(object host, Player p, Item shortTool, Item grand, Item supply)
        {
            if (!Main.tile[70, 42].wire2() || Main.tile[71, 42].wire2() || GetOptional(host, "grandBuild") != null) return false;
            var prepared = GetOptional(host, "preparedBuild"); if (prepared == null) return false;
            var original = Get(prepared, "Target");
            var hint = Call(host, "PreferredBuildSourceHint", p);
            if ((int)Get(original, "WorldX") != 71 || hint == null || !ReferenceEquals(Get(hint, "Item"), grand) ||
                !(bool)Call(host, "IsBuildTargetCurrent", original)) return false;
            // Copy a currently proven description, without changing the real
            // candidate, owner or world. This witness checks preparation only;
            // both real651 consumptions remain the surrounding case's duty.
            var target = original.GetType().GetMethod("MemberwiseClone", F).Invoke(original, null);
            var preferred = Call(host, "SnapshotSource", 12, shortTool);
            object[] planning = { target, p, preferred, null, null, 0UL };
            Require((bool)Call(host, "TryPlanBuild", planning) && ReferenceEquals(Get(planning[3], "Tool"), shortTool),
                "two native tools are feasible and the explicit pure winner is the short exact Item while the current hint is Grand Design");
            int stack = supply.stack; var oldMode = WiresUI.Settings.ToolMode;
            object[] preparing = { target, p, null, planning[3] };
            Require((bool)Call(host, "TryPrepareSelectedBuild", preparing), "the explicit feasible tool plan passes actual final preparation");
            var flight = preparing[2]; var operation = Get(flight, "Operation");
            Require(ReferenceEquals(Get(Get(flight, "Source"), "Item"), shortTool) && (int)Get(Get(flight, "Source"), "Slot") == 12 &&
                Get(operation, "Consumer").ToString() == "Wiring" && (int)Get(operation, "HeldMaximum") == 0 && (int)Get(operation, "PrimaryMaximum") == 1 &&
                Get(Get(prepared, "Operation"), "Consumer").ToString() == "GrandDesign" && supply.stack == stack && WiresUI.Settings.ToolMode == oldMode &&
                GetOptional(host, "grandBuild") == null,
                "final preparation keeps the selected exact Item/consumer/budgets, does not reselect the active hint or mutate/consume the real next651 candidate");
            Console.WriteLine("PASS BlueprintConstruction selected wiring plan actualHint=GrandDesign selectedExactShort=12 consumer=Wiring primary=1 held=0 witness=prepare-only real651SequenceUnchanged=true");
            return true;
        }
        private static void ConstructionActuatorCleanupTool(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input);
            p.inventory[12].SetDefaults(ItemID.ActuationRod); p.inventory[13].SetDefaults(ItemID.WireCutter);
            p.inventory[14].SetDefaults(3611); var grand = p.inventory[14];
            p.inventory[20].SetDefaults(ItemID.Actuator); p.inventory[20].stack = 2;
            NativeToolsChecks.Tile(44, 42, TileID.Stone); WorldGen.PlaceWire2(44, 42);
            p.position = new Vector2(72 * 16, p.position.Y);
            Require(!p.IsInTileInteractionRange(44, 42, TileReachCheckSettings.Simple, p.inventory[12].tileBoost + p.blockRange),
                "the real rod is initially too far away, even though a visible Grand Design could install a precondition");
            var instance = Place(context, host, input, Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone, Inactive = true }), 44, 42);
            long sourceSteps = (long)Get(host, "ProjectionMatchSourceSteps");
            SetAutomatic(host, input, true);
            NativeQuickItemChecks.Until(() => { ConstructionStepWithProjectiles(context, input); return ProgressDormant(instance) && !(bool)Get(Get(tools, "Use"), "Active"); });
            Require(!Main.tile[44, 42].actuator() && ((IDictionary)Get(host, "ownedBuildActuators")).Count == 0 && p.inventory[20].stack == 2,
                "a real candidate search creates no temporary actuator debt until the current rod route is reachable");
            Require((long)Get(host, "ProjectionMatchSourceSteps") > sourceSteps, "the negative route completed actual source discovery before sleeping, without entering a final preparation");
            p.position = new Vector2(40 * 16, p.position.Y);
            Until(context, input, () => Main.tile[44, 42].inActive());
            Require(Main.tile[44, 42].actuator() && ((IDictionary)Get(host, "ownedBuildActuators")).Count == 1,
                "a real nearby installation and rod leave exactly the proven temporary actuator to clean up");
            p.position = new Vector2(72 * 16, p.position.Y);
            Require(!p.IsInTileInteractionRange(44, 42, TileReachCheckSettings.Simple, p.inventory[13].tileBoost + p.blockRange) &&
                (bool)Call(host, "GrandBuildPointVisible", p, 44, 42), "the existing cutter is now out of reach while Grand Design cleanup is legal");
            var oldMode = WiresUI.Settings.ToolMode;
            try
            {
                NativeQuickItemChecks.Until(() => {
                    ConstructionStepWithProjectiles(context, input); return GetOptional(host, "grandBuild") != null;
                });
                var flight = Get(Get(host, "grandBuild"), "Flight"); var operation = Get(flight, "Operation");
                Require(Get(operation, "Kind").ToString() == "RemoveOwnedActuator" && ReferenceEquals(Get(Get(flight, "Source"), "Item"), grand) &&
                    (int)Get(operation, "HeldMaximum") == 0 && (int)Get(operation, "PrimaryMaximum") == 0,
                    "the reachable cleanup alternative keeps this exact installation receipt and has no consumption budget");
                NativeQuickItemChecks.Until(() => { ConstructionStepWithProjectiles(context, input); return Scene(host).Length == 0; });
                Require(!Main.tile[44, 42].actuator() && Main.tile[44, 42].inActive() && Main.tile[44, 42].wire2() &&
                    ((IDictionary)Get(host, "ownedBuildActuators")).Count == 0 && grand.stack == 1 && WiresUI.Settings.ToolMode == oldMode,
                    "the actual single-point651 removes only the owned actuator, preserves existing blue wire and settles its cleanup receipt");
            }
            finally { WiresUI.Settings.ToolMode = oldMode; }
        }
        private static void ConstructionTileWand(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.LivingWoodWand);
            p.inventory[20].SetDefaults(ItemID.Wood); p.inventory[20].stack = 1; var oldMaterial = p.inventory[20];
            var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = (ushort)p.inventory[12].createTile });
            Place(context, host, input, entry, 44, 42); long unknown = (long)Get(host, "BuildUnknownResults"); SetAutomatic(host, input, true);
            Until(context, input, () => Scene(host).Length == 0);
            Require(p.inventory[12].type == ItemID.LivingWoodWand && p.inventory[12].stack == 1 && p.inventory[20].IsAir && !ReferenceEquals(oldMaterial, p.inventory[20]) &&
                (long)Get(host, "BuildUnknownResults") == unknown,
                "ordinary wand consumes its last real wood through the ItemCheck tail's new-Air replacement, preserves the tool and does not self-lock or become unknown");
        }
        private static void ConstructionFlexible(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.RubblemakerSmall);
            p.inventory[0].SetDefaults(ItemID.StoneBlock); p.inventory[0].stack = 5; var firstBucket = p.inventory[0];
            p.inventory[20].SetDefaults(ItemID.DirtBlock); p.inventory[20].stack = 3; var selectedBucket = p.inventory[20];
            int oldCycle = Player.FlexibleWandCycleOffset;
            Require(WorldGen.PlaceObject(60, 42, 650, true, 8), "original source object is a specific second-bucket rubble variant");
            var data = TileObjectData.GetTileData(650, 8);
            var entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, (LibraryState)Get(host, "Library"), () => Step(context, input),
                60 - data.Origin.X, 42 - data.Origin.Y, data.Width, data.Height, "碎块机精确变体");
            Place(context, host, input, entry, 44, 43 - data.Height); SetAutomatic(host, input, true);
            Until(context, input, () => Scene(host).Length == 0 && ReferenceEquals(p.inventory[0], firstBucket) && ReferenceEquals(p.inventory[20], selectedBucket) &&
                Player.FlexibleWandCycleOffset == oldCycle && Main.mouseItem.IsAir);
            Require(firstBucket.stack == 5 && selectedBucket.stack == 2 && p.inventory[12].type == ItemID.RubblemakerSmall,
                "real ItemSlot prepares the later bucket, exact native cycle/frame is placed, only its material is spent, and bucket/selection return finishes before retirement");
        }
        private static void ConstructionWithoutBucketConsumption(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.GardenGnome); p.inventory[12].stack = 1;
            int cycle = Player.FlexibleWandCycleOffset; var wand = p.inventory[12].GetFlexibleTileWand();
            Terraria.GameContent.FlexibleTileWand.PlacementOption option; Item material;
            Require(wand.TryGetPlacementOption(p, Player.FlexibleWandRandomSeed, cycle + 1, out option, out material) && !wand.ConsumesAmmoItem,
                "real held consumable has a non-consuming bucket requirement");
            var data = TileObjectData.GetTileData(option.TileIdToPlace, option.TileStyleToPlace);
            Require(WorldGen.PlaceObject(60, 42, option.TileIdToPlace, true, option.TileStyleToPlace), "held-only source requires a different native cycle");
            var entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, (LibraryState)Get(host, "Library"), () => Step(context, input),
                60 - data.Origin.X, 42 - data.Origin.Y, data.Width, data.Height, "仅耗持用物");
            Place(context, host, input, entry, 44, 43 - data.Height); SetAutomatic(host, input, true);
            Until(context, input, () => Scene(host).Length == 0 && p.inventory.All(i => i.type != ItemID.GardenGnome || i.stack <= 0) &&
                Player.FlexibleWandCycleOffset == cycle && Main.mouseItem.IsAir);
            Require(p.inventory.Where(i => i.type == ItemID.GardenGnome).Sum(i => Math.Max(0, i.stack)) == 0,
                "last held-only flexible item spends itself once after real nonzero variant preparation, then returns the user's cycle without requiring another tool");
        }
        private static void ConstructionSubstrates(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input);
            p.inventory[12].SetDefaults(ItemID.DirtBlock); p.inventory[12].stack = 3;
            p.inventory[13].SetDefaults(ItemID.GrassSeeds); p.inventory[13].stack = 3;
            p.inventory[14].SetDefaults(ItemID.MudBlock); p.inventory[14].stack = 2;
            p.inventory[15].SetDefaults(ItemID.CorruptSeeds); p.inventory[15].stack = 2;
            var catalog=(BlueprintPlacementCatalog)Get(host,"MaterialCatalog");var grassTarget=new CaptureCell{Layers=CaptureLayer.Tile,TileType=TileID.Grass};
            var grassProducers=catalog.Producers(grassTarget);
            Console.WriteLine("SUBSTRATE actual grass qualification="+catalog.QualifyTile(grassTarget).Resolution+" producers="+
                string.Join(",",grassProducers.Select(value=>value.UseItemType+":"+value.ConsumeItemType+"x"+value.Units)));
            Require(catalog.QualifyTile(grassTarget).Resolution==BlueprintPlacementResolutionKind.Ambiguous &&
                new[]{62,213,5295}.All(type=>grassProducers.Any(value=>value.UseItemType==type)) && catalog.Substrate(grassTarget)?.UseItemType==ItemID.GrassSeeds,
                "actual native grass producers stay ambiguous while the specified finite seed recipe is executable and billable");
            Console.WriteLine("SUBSTRATE original fixture seed reach48="+p.IsInTileInteractionRange(48,42,TileReachCheckSettings.Simple,p.inventory[13].tileBoost+p.blockRange));
            p.position=new Vector2(42*16,p.position.Y);
            Require(p.IsInTileInteractionRange(44,42,TileReachCheckSettings.Simple,p.inventory[13].tileBoost+p.blockRange) &&
                p.IsInTileInteractionRange(46,42,TileReachCheckSettings.Simple,p.inventory[15].tileBoost+p.blockRange) &&
                p.IsInTileInteractionRange(48,42,TileReachCheckSettings.Simple,p.inventory[13].tileBoost+p.blockRange) &&
                p.IsInTileInteractionRange(47,42,TileReachCheckSettings.Simple,p.inventory[13].tileBoost+p.blockRange),
                "all native seed and conflict targets in this static fixture are actually within the player's current interaction range");
            NativeToolsChecks.Tile(48, 42, TileID.Dirt); NativeToolsChecks.Tile(47, 42, TileID.Stone);
            var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Grass },
                new CaptureCell { X = 2, Layers = CaptureLayer.Tile, TileType = 661 },
                new CaptureCell { X = 4, Layers = CaptureLayer.Tile, TileType = TileID.Grass },
                new CaptureCell { X = 3, Layers = CaptureLayer.Tile, TileType = TileID.Grass });
            Place(context, host, input, entry, 44, 42); SetAutomatic(host, input, true);
            try{Until(context, input, () => Main.tile[44, 42].type == TileID.Grass && Main.tile[46, 42].type == 661 && Main.tile[48, 42].type == TileID.Grass);}
            finally{Console.WriteLine("SUBSTRATE actual44/46/48/47="+Main.tile[44,42].active()+":"+Main.tile[44,42].type+"/"+Main.tile[46,42].active()+":"+Main.tile[46,42].type+"/"+Main.tile[48,42].active()+":"+Main.tile[48,42].type+"/"+Main.tile[47,42].active()+":"+Main.tile[47,42].type+
                "; dirt/seeds/mud/corrupt="+p.inventory[12].stack+"/"+p.inventory[13].stack+"/"+p.inventory[14].stack+"/"+p.inventory[15].stack);}
            Require(Main.tile[47, 42].type == TileID.Stone && p.inventory[12].stack == 2 && p.inventory[13].stack == 1 &&
                p.inventory[14].stack == 1 && p.inventory[15].stack == 1 && Scene(host).Length == 1,
                "empty targets use base then original seeds, existing dirt only uses seeds, mud seed rewrite reaches661, foreign stone is not replaced");
        }

        private static readonly List<byte[]> constructionWiringPackets = new List<byte[]>();
        private static void RecordConstructionWiringPacket(int __0, int __1)
        {
            if (__0 != 109 && __0 != 110 && __0 != 17) return;
            var bytes = NetMessage.buffer[Main.netMode == 2 && __1 >= 0 ? __1 : 256].writeBuffer;
            int length = BitConverter.ToUInt16(bytes, 0);
            Require(length >= 3 && length <= bytes.Length && bytes[2] == __0, "actual wiring serializer produced the observed message");
            if (__0 == 17 && (length < 4 || bytes[3] != 8 && bytes[3] != 9)) return;
            constructionWiringPackets.Add(bytes.Take(length).ToArray());
        }
        private static void ReplayConstructionPacket(byte[] packet, int sender)
        {
            var receiver = new MessageBuffer { whoAmI = sender };
            // The decoder starts with the message id, after the two-byte wire length.
            Array.Copy(packet, 2, receiver.readBuffer, 0, packet.Length - 2);
            receiver.ResetReader(); int kind; receiver.GetData(0, packet.Length - 2, out kind);
            Require(kind == packet[2], "original wiring decoder dispatches the serialized message type");
        }
        private static void ConstructionTemporaryActuatorNetwork(object context, object host, object tools, object input, int scenario)
        {
            const int x = 50, y = 42;
            int mode = Main.netMode; var connection = Netplay.Connection; var sections = Main.sectionManager; var client = Main.clientPlayer;
            var tiles = Main.tile; var buffers = (MessageBuffer[])NetMessage.buffer.Clone(); var clients = (RemoteClient[])Netplay.Clients.Clone();
            var oldWireMode = WiresUI.Settings.ToolMode;
            var patch = new Harmony("JueMingR.Tests.TemporaryActuatorPackets"); var send = typeof(NetMessage).GetMethod("SendData", F);
            patch.Patch(send, postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(RecordConstructionWiringPacket)));
            try
            {
                Netplay.Connection = new RemoteServer { PendingTermination = true, State = 10 }; Main.clientPlayer = new Player();
                for (int i = 0; i < NetMessage.buffer.Length; i++) if (NetMessage.buffer[i] == null) NetMessage.buffer[i] = new MessageBuffer();
                NetMessage.buffer[256] = new MessageBuffer(); NetMessage.buffer[0] = new MessageBuffer();
                Main.netMode = 1; Main.sectionManager = new WorldSections(1, 2); Main.sectionManager.SetAllSectionsLoaded(); Call(context, "UpdateRuntime");
                var p = Reset(context, host, tools, input);
                p.inventory[12].SetDefaults(3611); p.inventory[13].SetDefaults(ItemID.ActuationRod); p.inventory[14].SetDefaults(ItemID.WireCutter);
                p.inventory[20].SetDefaults(ItemID.Actuator); p.inventory[20].stack = 2; var supply = p.inventory[20];
                Require(!p.IsInTileInteractionRange(x, y, TileReachCheckSettings.Simple, supply.tileBoost + p.blockRange) &&
                    p.IsInTileInteractionRange(x, y, TileReachCheckSettings.Simple, p.inventory[13].tileBoost + p.blockRange) &&
                    p.IsInTileInteractionRange(x, y, TileReachCheckSettings.Simple, p.inventory[14].tileBoost + p.blockRange),
                    "real actuator item is too short while the real rod and cutter are legal; GD is selected by capability fallback");
                NativeToolsChecks.Tile(x, y, TileID.Stone); WorldGen.PlaceWire2(x, y);
                Place(context, host, input, Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone, Inactive = true }), x, y);
                long unknown = (long)Get(host, "BuildUnknownResults");
                constructionWiringPackets.Clear(); SetAutomatic(host, input, true);
                NativeQuickItemChecks.Until(() => { ConstructionStepWithProjectiles(context, input); return GetOptional(host, "wiringReply") != null; });
                var request = constructionWiringPackets.Single(packet => packet[2] == 109); var reply = Get(host, "wiringReply");
                var candidate = Get(reply, "TemporaryActuator");
                Require(request.Length == 12 && BitConverter.ToInt16(request, 3) == x && BitConverter.ToInt16(request, 5) == y &&
                    BitConverter.ToInt16(request, 7) == x && BitConverter.ToInt16(request, 9) == y &&
                    request[11] == (byte)WiresUI.Settings.MultiToolMode.Actuator && !Main.tile[x, y].actuator() &&
                    ((IDictionary)Get(host, "ownedBuildActuators")).Count == 0 && supply.stack == 2,
                    "native651 sends only the exact single-point Actuator109; submission creates a candidate, never premature ownership or debit");
                SetAutomatic(host, input, false);
                Until(context, input, () => !(bool)Get(Get(tools, "Use"), "Active") && p.itemAnimation == 0 && p.itemTime == 0);
                Require(ReferenceEquals(Get(host, "wiringReply"), reply) && !(bool)Get(candidate, "Retired"),
                    "pausing releases ToolUse without revoking this already-sent request's observation and finite debit duty");

                for (int i = 0; i < Netplay.Clients.Length; i++) if (Netplay.Clients[i] == null) Netplay.Clients[i] = new RemoteClient { Id = i, State = 10 };
                Netplay.Clients[0] = new RemoteClient { Id = 0, State = 10 };
                var clientTiles = Main.tile;
                // The existing isolated decoder fixture executes both native
                // roles. Give the authority its own target Tile so its write
                // cannot masquerade as the later client's actual17 transition.
                var serverTiles = (Tile[,])clientTiles.Clone(); serverTiles[x, y] = (Tile)clientTiles[x, y].Clone();
                constructionWiringPackets.Clear();
                try
                {
                    Main.netMode = 2; Main.tile = serverTiles;
                    if (scenario == 1)
                    {
                        Require(WorldGen.PlaceActuator(x, y), "another original authority action installs before this request");
                        NetMessage.SendData(17, -1, -1, null, 8, x, y);
                    }
                    ReplayConstructionPacket(request, 0);
                    Require(Main.tile[x, y].actuator() && supply.stack == 2 && !clientTiles[x, y].actuator(),
                        "original server109 writes only the authority Tile and leaves the client's actual material untouched");
                }
                finally { Main.tile = clientTiles; Main.netMode = 1; }
                var ordered = constructionWiringPackets.Where(packet => packet[2] == 17 || packet[2] == 110).ToArray();
                Require(ordered.Length == 3 && ordered[0][2] == 17 && ordered[0][3] == 8 &&
                    ordered[1][2] == 110 && BitConverter.ToInt16(ordered[1], 3) == 530 && BitConverter.ToInt16(ordered[1], 5) == 0 &&
                    ordered[2][2] == 110 && BitConverter.ToInt16(ordered[2], 3) == 849 && BitConverter.ToInt16(ordered[2], 5) == (scenario == 1 ? 0 : 1),
                    "actual authority serializers establish17/8 before110 wire0 before110 actuator count; scenario=" + scenario);
                if (scenario == 3)
                {
                    Call(host, "ClearProjection");
                    Require((bool)Get(candidate, "Retired") && Scene(host).Length == 0 && Get(Get(reply, "Flight"), "Target") != null,
                        "retirement revokes new cleanup ownership while preserving the already-sent finite decoder");
                }
                ReplayConstructionPacket(ordered[0], 256);
                Require(Main.tile[x, y].actuator() && supply.stack == 2 && ((IDictionary)Get(host, "ownedBuildActuators")).Count == 0 &&
                    !(bool)Get(Get(tools, "Use"), "Active"),
                    "actual client17 changes false to true but grants neither cleanup nor consumer/input authority");
                if (scenario != 3) Require((bool)Get(candidate, "SawInstall"), "the exact actual client PlaceActuator transition was observed");
                if (scenario == 2)
                {
                    Require(WorldGen.KillActuator(x, y) && WorldGen.PlaceActuator(x, y), "a separate original writer removes and reinstalls the actuator");
                    Require((bool)Get(candidate, "Invalid") && Main.tile[x, y].actuator(),
                        "observed actuator ABA permanently revokes candidate provenance despite the identical final value");
                }
                ReplayConstructionPacket(ordered[1], 256);
                Require(GetOptional(host, "wiringReply") != null && supply.stack == 2 && ((IDictionary)Get(host, "ownedBuildActuators")).Count == 0,
                    "wire0 alone cannot claim or end this responsibility");
                ReplayConstructionPacket(ordered[2], 256);
                Require(GetOptional(host, "wiringReply") == null && supply.stack == (scenario == 1 ? 2 : 1) &&
                    (long)Get(host, "BuildUnknownResults") == unknown && !(bool)Get(Get(tools, "Use"), "Active") && !(bool)Get(host, "AutoBuildEnabled"),
                    "full native debit settles the exact protected source while passive confirmation preserves pause and known consumption");
                var owners = (IDictionary)Get(host, "ownedBuildActuators");
                Require(owners.Count == (scenario == 0 ? 1 : 0) && (scenario != 0 || (bool)Get(owners.Values.Cast<object>().Single(), "Confirmed")),
                    "only the live, continuous, actually installed count1 sequence creates a confirmed cleanup owner; scenario=" + scenario);
                SetAutomatic(host, input, true);
                if (scenario == 3)
                {
                    for (int i = 0; i < 8; i++) ConstructionStepWithProjectiles(context, input);
                    Require(Scene(host).Length == 0 && Main.tile[x, y].actuator() && !Main.tile[x, y].inActive() &&
                        GetOptional(host, "grandBuild") == null && !(bool)Get(Get(tools, "Use"), "Active"),
                        "late retired replies debit once but cannot revive projection, rod, cutter or old651 input");
                }
                else
                {
                    Until(context, input, () => Scene(host).Length == 0);
                    Require(Main.tile[x, y].inActive() && Main.tile[x, y].actuator() == (scenario != 0) && Main.tile[x, y].wire2() &&
                        owners.Count == 0 && (long)Get(host, "BuildUnknownResults") == unknown,
                        "native rod/cutter remove only the proven installation; count0 and ABA actuators remain as existing pieces; scenario=" + scenario);
                }
                Console.WriteLine("PASS TemporaryActuatorNative scenario=" + scenario + " debit=" + (scenario == 1 ? 0 : 1) +
                    " finalActuator=" + Main.tile[x, y].actuator() + " inactive=" + Main.tile[x, y].inActive());
            }
            finally
            {
                SetAutomatic(host, input, false); Call(host, "ClearProjection"); Main.tile = tiles; Main.netMode = mode;
                Netplay.Connection = connection; Main.sectionManager = sections; Main.clientPlayer = client; WiresUI.Settings.ToolMode = oldWireMode;
                for (int i = 0; i < buffers.Length; i++) NetMessage.buffer[i] = buffers[i];
                for (int i = 0; i < clients.Length; i++) Netplay.Clients[i] = clients[i];
                patch.Unpatch(send, HarmonyPatchType.All, patch.Id); Call(context, "UpdateRuntime");
            }
        }
        private static void ConstructionWiringNetwork(object context, object host, object tools, object input,bool loseCorrelation=false)
        {
            int mode = Main.netMode; var connection = Netplay.Connection; var sections = Main.sectionManager; var client = Main.clientPlayer;
            var buffers = (MessageBuffer[])NetMessage.buffer.Clone(); var clients = (RemoteClient[])Netplay.Clients.Clone();
            var patch = new Harmony("JueMingR.Tests.ConstructionWiringPackets"); var send = typeof(NetMessage).GetMethod("SendData", F);
            patch.Patch(send, postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(RecordConstructionWiringPacket)));
            try
            {
                Netplay.Connection = new RemoteServer { PendingTermination = true, State = 10 }; Main.clientPlayer = new Player();
                // Main normally initializes all 257 buffers. The original server109 writes the tile,
                // then SendData17 broadcasts through every client buffer before producing its two110s.
                // Preserve that serializer/decoder path instead of stopping after the world mutation.
                for(int i=0;i<NetMessage.buffer.Length;i++) if(NetMessage.buffer[i]==null) NetMessage.buffer[i]=new MessageBuffer();
                NetMessage.buffer[256] = new MessageBuffer(); NetMessage.buffer[0] = new MessageBuffer(); Main.netMode = 1;
                Main.sectionManager = new WorldSections(1, 1); Main.sectionManager.SetAllSectionsLoaded(); Call(context, "UpdateRuntime");
                var p = Reset(context, host, tools, input); p.position=new Vector2(42*16,p.position.Y);p.inventory[12].SetDefaults(ItemID.MulticolorWrench);
                int wireSlot=loseCorrelation?20:0;
                p.inventory[wireSlot].SetDefaults(ItemID.Wire); p.inventory[wireSlot].stack = 2; p.inventory[14].SetDefaults(ItemID.StoneBlock); p.inventory[14].stack = 2;
                var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Wire, Wires = 2 }, new CaptureCell { X = 2, Layers = CaptureLayer.Tile, TileType = TileID.Stone });
                Place(context, host, input, entry, 44, 42); constructionWiringPackets.Clear(); SetAutomatic(host, input, true);
                Until(context, input, () => GetOptional(host, "wiringReply") != null);
                var request = constructionWiringPackets.Single(p0 => p0[2] == 109);
                var receipt = Get(host, "wiringReply");
                Until(context, input, () => Main.tile[46, 42].active());
                Require(ReferenceEquals(GetOptional(host, "wiringReply"), receipt) && p.inventory[wireSlot].stack == 2,
                    "unconfirmed109 preserves its source and retirement responsibility while independent body construction progresses");
                if(!loseCorrelation)
                {
                    Until(context,input,()=>!(bool)Get(Get(tools,"Use"),"Active") && p.itemAnimation==0 && p.itemTime==0);
                    var preparation=Get(host,"buildMaterialPreparation");var pendingWire=p.inventory[0];
                    p.inventory[18].SetDefaults(ItemID.RubblemakerSmall);p.inventory[22].SetDefaults(ItemID.DirtBlock);p.inventory[22].stack=2;
                    Require((bool)Call(preparation,"StartAllowed",p) && (bool)Call(tools,"BuildInventoryEligible",22,p.inventory[22]) &&
                        ((ulong)Get(Get(Get(tools,"Items"),"Ownership"),"ProtectedSlots")&1UL)==0 && (bool)Call(host,"WiringPendingMaterial",pendingWire),
                        "a detached real109 source at slot0 is pending despite the ended ToolUse, while an independent flexible bucket otherwise has legal preparation gates");
                    long clicks=(long)Get(preparation,"NativeClicks");
                    var nextIdentity=new BlueprintProjectionIdentity(Guid.NewGuid(),Guid.NewGuid(),"pending109-flexible-bucket",(long)Get(Get(tools,"Runtime"),"Generation"),1);
                    object[] prepare={p,22,0,nextIdentity,-1};
                    Require(!(bool)Call(preparation,"TryPrepare",prepare) && !(bool)Get(preparation,"HasWork") &&
                        (long)Get(preparation,"NativeClicks")==clicks && ReferenceEquals(p.inventory[0],pendingWire) && pendingWire.stack==2 &&
                        ReferenceEquals(GetOptional(host,"wiringReply"),receipt),
                        "real pending110 material cannot become an automatic ItemSlot destination; no click, movement or pending-receipt loss occurs");
                    Console.WriteLine("PASS BlueprintConstruction consumer=real109 pending slot0 cannot become a preparation endpoint");
                }
                for (int i = 0; i < Netplay.Clients.Length; i++) if (Netplay.Clients[i] == null) Netplay.Clients[i] = new RemoteClient { Id = i, State = 10 };
                Netplay.Clients[0] = new RemoteClient { Id = 0, State = 10 };
                Main.netMode = 2; ReplayConstructionPacket(request, 0);
                var replies = constructionWiringPackets.Where(p0 => p0[2] == 110).ToArray();
                Require(replies.Length == 2 && BitConverter.ToInt16(replies[0], 3) == 530 && BitConverter.ToInt16(replies[0], 5) == 1 &&
                    BitConverter.ToInt16(replies[1], 3) == 849 && BitConverter.ToInt16(replies[1], 5) == 0 && p.inventory[wireSlot].stack == 2 && Main.tile[44, 42].wire2(),
                    "original server109/MassWire writes blue then serializes wire1 and actuator0; server does not debit the client inventory");
                if(loseCorrelation)
                {
                    Main.netMode=1;long unknown=(long)Get(host,"BuildUnknownResults");
                    ReplayConstructionPacket(replies[1],256);
                    Require((bool)Get(host,"wiringCorrelationLost") && GetOptional(host,"wiringReply")==null && (long)Get(host,"BuildUnknownResults")>unknown,
                        "a real out-of-order zero second110 makes this responsibility unknown and loses automatic correlation");
                    ReplayConstructionPacket(replies[0],256);ReplayConstructionPacket(replies[1],256);
                    Require(GetOptional(host,"wiringReply")==null && p.inventory[20].stack==2,
                        "the old native110 tail is not claimed by another action or allowed through the lost source's protection");
                    p.inventory[0].SetDefaults(ItemID.Wire);p.inventory[0].stack=3;
                    p.inventory[13].SetDefaults(ItemID.MulticolorWrench);p.inventory[16].SetDefaults(ItemID.Wrench);
                    var fallback=Asset(host,new CaptureCell{Layers=CaptureLayer.Wire,Wires=1},
                        new CaptureCell{X=2,Layers=CaptureLayer.Wire,Wires=2},new CaptureCell{X=4,Layers=CaptureLayer.Tile,TileType=TileID.Stone});
                    Place(context,host,input,fallback,43,42);
                    Until(context,input,()=>Main.tile[43,42].wire() && Main.tile[47,42].active());
                    for(int i=0;i<40;i++)Step(context,input);
                    Require(constructionWiringPackets.Count(p0=>p0[2]==109)==1 && GetOptional(host,"wiringReply")==null &&
                        (bool)Get(host,"wiringCorrelationLost") && !Main.tile[45,42].wire2() && p.inventory[0].stack==2,
                        "lost correlation blocks a fresh eligible multi-wrench109 until the session ends, while a direct color and independent body still consume their actual sources");
                    return;
                }
                Main.netMode = 1; ReplayConstructionPacket(replies[0], 256);
                Require(p.inventory[wireSlot].stack == 1 && GetOptional(host, "wiringReply") != null,
                    "first original110 consumes the exact pending source but does not prematurely end the pair");
                ReplayConstructionPacket(replies[1], 256);
                Until(context, input, () => Scene(host).Length == 0);
                Require(p.inventory[wireSlot].stack == 1 && GetOptional(host, "wiringReply") == null,
                    "zero-count second110 closes exactly this finite pair and full current world Match retires naturally");
            }
            finally
            {
                SetAutomatic(host, input, false); Call(host, "ClearProjection"); Main.netMode = mode; Netplay.Connection = connection;
                Main.sectionManager = sections; Main.clientPlayer = client;
                for(int i=0;i<buffers.Length;i++) NetMessage.buffer[i]=buffers[i];
                for (int i = 0; i < clients.Length; i++) Netplay.Clients[i] = clients[i];
                patch.Unpatch(send, HarmonyPatchType.All, patch.Id); Call(context, "UpdateRuntime");
            }
        }
    }
}
