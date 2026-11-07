using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.Fishing;
using JueMingR.Features.Movement;
using JueMingR.Features.QuickItems;
using JueMingR.Platform.Items;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;
using Fixture = NativeWorldTextProbe.NativeJourneyCloseAdmissionChecks;

namespace NativeWorldTextProbe
{
    // Real native containers/SelectedItemState and production return owners.
    // Component admission supplies a finite loan, not a claim about selecting
    // a rescue target or a complete gameplay effect. No loan state is forged.
    internal static class NativeRuntimeFailureReturnChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static bool probeEntered;
        private static int displayFaults;
        private static int unrelatedDeathSaves;
        private static object trackedToolsUse;
        internal static void ToolsSelection(object context)
        {
            LocalDraw(context);
            object tools = Get(context, "Tools"), input = Get(context, "Input"), use = Get(tools, "Use");
            NativeToolExecutionChecks.Initialize(context, null);
            Player p = NativeToolExecutionChecks.Reset(context, tools, input, ItemID.CopperPickaxe, 12, 0);
            Main.playerInventory = false; NativeQuickItemChecks.Sample(input, new Keys[0]);
            NativeQuickItemChecks.BeginWorldStep();
            Type type = tools.GetType().Assembly.GetType("JueMingR.TerrariaHost.Tools.ToolIntent", true);
            object intent = Activator.CreateInstance(type, true);
            Fixture.Set(intent, "Kind", Enum.Parse(type.GetField("Kind", F).FieldType, "Build"));
            Fixture.Set(intent, "Slot", 12); Fixture.Set(intent, "Valid", (Func<bool>)(() => true));
            Fixture.Set(intent, "Refresh", (Func<bool>)(() => true)); Fixture.Set(intent, "SelectionOnly", true);
            Fixture.Require((bool)Call(use, "Acquire", p, intent), "fixture acquires a real tools source lease");
            p.selectedItemState.Update();
            Fixture.Require(p.selectedItem == 12 && p.selectedItemState.HasActiveOverride && !(bool)Get(use, "InNativeUse"), "fixture native selection precedes first ItemCheck");
            var witness = new Harmony("JueMingR.Tests.RuntimeToolsRetireWitness");
            MethodInfo retire = use.GetType().GetMethod("Retire", F); trackedToolsUse = use;
            try
            {
                witness.Patch(retire, prefix: new HarmonyMethod(typeof(NativeRuntimeFailureReturnChecks), nameof(ObserveToolsRetire)));
                ToolsState("before-common", tools, use, p);
                CommonFault(context); ToolsState("after-common", tools, use, p);
                for (int i = 0; i < 4; i++) { Call(context, "UpdateRuntime"); ToolsState("cleanup-" + i, tools, use, p); }
                Fixture.Require((bool)Get(use, "Active") && p.selectedItemState.HasBufferedChange, "common failure retains the exact tool return until native selection consumes it");
                p.selectedItemState.Update(); Call(context, "UpdateRuntime"); ToolsState("after-native-return", tools, use, p);
                var ownership = (ItemOperationOwnership)Get(Get(tools, "Items"), "Ownership");
                Fixture.Require(p.selectedItem == 0 && !(bool)Get(use, "Active") && !ownership.IsUseSlot(12), "cancelled tool before first ItemCheck retires after the real buffered return");
                Fixture.Require(!p.controlUseItem && !Main.mouseLeft, "terminal tools cleanup cannot manufacture a new use press");
            }
            finally { witness.Unpatch(retire, HarmonyPatchType.All, witness.Id); trackedToolsUse = null; }
        }

        internal static void QuickSelection(object context)
        {
            object quick = Get(context, "QuickItems"), use = Get(quick, "Use"), input = Get(context, "Input");
            var settings = (QuickItemSettings)Get(quick, "Settings");
            var entry = new QuickItemEntry("8e9b43973d964144930d1027c4f5e8bd", ItemID.MagicMirror, QuickItemMode.Use, true, true);
            string reason;
            Fixture.Require(settings.TryChange(new QuickItemDocument(false, true, new[] { entry }), entry.Id, out reason), "fixture commits the real quick entry: " + reason);
            NativeQuickItemChecks.Until(() => { Call(quick, "Poll"); return !settings.Busy; });
            Player p = Main.LocalPlayer; p.inventory[17].SetDefaults(ItemID.MagicMirror);
            p.selectedItemState.Select(2); p.selectedItemState.Update(); Main.playerInventory = false;
            NativeQuickItemChecks.Sample(input, new Keys[0]);
            Call(use, "TryStart", entry); p.selectedItemState.Update();
            typeof(Player).GetMethod("TrySyncingInput", F).Invoke(p, null);
            Fixture.Require(p.selectedItem == 17 && p.selectedItemState.HasActiveOverride && p.controlUseItem && !(bool)Get(use, "InNativeUse"), "fixture native quick selection/pulse precedes first ItemCheck");
            Item provider = p.inventory[17]; CommonFault(context);
            Fixture.Require(!p.controlUseItem && !Main.mouseLeft, "common failure withdraws its same-frame quick pulse before any ItemCheck");
            Call(context, "UpdateRuntime");
            Fixture.Require((bool)Get(use, "Active"), "quick selection keeps its buffered native return responsibility");
            p.selectedItemState.Update(); Call(context, "UpdateRuntime");
            Fixture.Require(p.selectedItem == 2 && !(bool)Get(use, "Active") && ReferenceEquals(p.inventory[17], provider), "quick cancellation before first ItemCheck returns and retires the original native lease");
        }

        internal static void SameIdentityLoans(object context)
        {
            var audit = new Harmony("JueMingR.Tests.CommonFailureFishing");
            foreach (string name in new[] { "HandleSpecialEvent", "HandleMining", "HandleRunning" })
                audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name, F), prefix: new HarmonyMethod(typeof(NativeRuntimeFailureReturnChecks), nameof(SkipAchievement)));
            // Death-save file setup is outside this return-only CPU scene. Do
            // not launch an unrelated async serializer on an incomplete native
            // file fixture; a death/save request still fails this scene below.
            // The real Player.IOLock and both return owners remain untouched.
            unrelatedDeathSaves = 0;
            audit.Patch(typeof(WorldGen).GetMethod("saveToonWhilePlaying", F), prefix: new HarmonyMethod(typeof(NativeRuntimeFailureReturnChecks), nameof(RefuseDeathSave)));
            try
            {
                InitializeFishing(context);
                object fishing = Get(context, "Fishing"), equipment = Get(fishing, "Equipment"), movement = Get(context, "Movement");
                object fall = Get(movement, "FallHost"), borrow = Get(fall, "borrow"), runtime = Get(Get(context, "Runtime"), "SharedRuntime");
                Type fixture = typeof(NativeFishingEquipmentChecks);
                // Its Reset first executes 25 genuine Player.Update steps on
                // the existing initial player. Establish a legal fall history
                // BEFORE those steps, not only after Reset returns.
                Main.LocalPlayer.ResetFallDamage();
                Player p = (Player)fixture.GetMethod("Reset", F).Invoke(null, null);
                // Reset's real floor is tile43. Start grounded with its normal
                // fall baseline, rather than a fall from the default y=0.
                p.fallStart = (int)(p.Bottom.Y / 16);
                p.armor[0].SetDefaults(ItemID.CopperHelmet); Item originalHat = p.armor[0];
                p.inventory[12].SetDefaults(5591); Item fishingHat = p.inventory[12];
                fixture.GetMethod("Cast", F).Invoke(null, new object[] { new FishingOptions(equipment: true) });
                Fixture.Require(!p.dead && unrelatedDeathSaves == 0, "fixture live native fishing session reaches the loan without an unrelated death/save request");
                Fixture.Require(ReferenceEquals(p.armor[0], fishingHat) && (bool)Get(equipment, "Active"), "fixture real wet native fishing session owns an actual head exchange");
                Item shoe, originalArmor; BorrowFall(context, out shoe, out originalArmor);
                long generation = (long)Get(runtime, "Generation"), scans = (long)Get(Get(fishing, "Observation"), "Scans");
                long plans = (long)Get(fall, "Plans"), sourceScans = (long)Get(fall, "SourceScans");
                Main.mouseItem.SetDefaults(ItemID.StoneBlock);
                using (var save = new SaveGate())
                {
                    ProbeFault(context);
                    Fixture.Require((long)Get(runtime, "Generation") == generation && (bool)Get(equipment, "Active") && (bool)Get(borrow, "HasPending"), "an unobservable session preserves both actual loan journals instead of declaring session end");
                    for (int i = 0; i < 4; i++) Call(context, "UpdateRuntime");
                    object input = Get(context, "Input"); NativeQuickItemChecks.Sample(input, new Keys[0]);
                    Fixture.Require((bool)Call(movement, "BeginUpdate", p), "same-identity failure permits only its pending native cleanup scope");
                    Call(movement, "CaptureInput", p); Call(movement, "EndUpdate", p);
                    Fixture.Require(ReferenceEquals(p.armor[0], fishingHat) && ReferenceEquals(p.armor[3], shoe) && (bool)Get(borrow, "HasPending"), "actual mouse/save gates keep both blocked loans intact");
                }
                Main.mouseItem.TurnToAir();
                for (int i = 0; i < 45 && ((bool)Get(equipment, "Active") || (bool)Get(borrow, "HasPending")); i++)
                    NativeWorldStep(context, () => NativeFishingChecks.Step(context, Get(context, "Input"), new Microsoft.Xna.Framework.Vector2(850, 718), false, 0));
                Fixture.Require(!(bool)Get(equipment, "Active") && !(bool)Get(borrow, "HasPending") &&
                    ReferenceEquals(p.armor[0], originalHat) && ReferenceEquals(p.inventory[12], fishingHat) &&
                    ReferenceEquals(p.armor[3], originalArmor) && ReferenceEquals(p.inventory[14], shoe), "same-identity native cleanup returns every exact original and borrowed object after the gates release");
                Fixture.Require((long)Get(Get(fishing, "Observation"), "Scans") == scans && (long)Get(fall, "Plans") == plans && (long)Get(fall, "SourceScans") == sourceScans,
                    "terminal cleanup performs no fishing observation, new rescue plan or rescue source scan");
                Fixture.Require(((FishingSettings)Get(fishing, "Settings")).Value.Equipment && (bool)Call(movement, "IsEnabled", 3), "common failure preserves configured fishing/fall choices");
                Fixture.Require(!p.dead && unrelatedDeathSaves == 0, "fixture remains alive throughout native returns; no death-save result is masked");
            }
            finally { foreach (var method in audit.GetPatchedMethods().ToArray()) audit.Unpatch(method, HarmonyPatchType.All, audit.Id); }
        }

        internal static void ChangedWorldLoan(object context)
        {
            object movement = Get(context, "Movement"), borrow = Get(Get(movement, "FallHost"), "borrow");
            object runtime = Get(Get(context, "Runtime"), "SharedRuntime");
            Player p = Main.LocalPlayer; Item shoe, originalArmor; BorrowFall(context, out shoe, out originalArmor);
            object world = Main.ActiveWorldFileData; long generation = (long)Get(runtime, "Generation");
            try
            {
                using (var save = new SaveGate())
                {
                    ProbeFault(context);
                    Fixture.Require((bool)Get(borrow, "HasPending"), "fixture observation failure retains a blocked actual fall loan");
                    Main.ActiveWorldFileData = new Terraria.IO.WorldFileData(Path.Combine(Terraria.Program.SavePath, "successor.wld"), false) { UniqueId = Guid.NewGuid() };
                    Fixture.Require(!(bool)Call(movement, "BeginUpdate", p), "world replacement rejects old cleanup even before the outer identity observation");
                    Call(context, "UpdateRuntime");
                }
                Fixture.Require(!(bool)Get(borrow, "HasPending") && (long)Get(runtime, "Generation") == generation && !(bool)Get(runtime, "IsSessionActive"), "real identity replacement retires the old journal without starting a successor session");
                Fixture.Require(ReferenceEquals(p.armor[3], shoe) && ReferenceEquals(p.inventory[14], originalArmor) && !(bool)Call(movement, "BeginUpdate", p), "successor world cannot replay the old exchange or inherit any new rescue admission");
            }
            finally { Main.ActiveWorldFileData = (Terraria.IO.WorldFileData)world; }
        }

        private static void BorrowFall(object context, out Item shoe, out Item originalArmor)
        {
            object movement = Get(context, "Movement"), input = Get(context, "Input"), fall = Get(movement, "FallHost");
            NativeQuickItemChecks.Until(() => (bool)Get(movement, "ControlsEnabled"));
            Fixture.Require((bool)Call(movement, "SetEnabled", 3, true) || (bool)Call(movement, "IsEnabled", 3), "fixture enables real fall protection settings");
            Player p = Main.LocalPlayer; p.inventory[14].SetDefaults(ItemID.LuckyHorseshoe); shoe = p.inventory[14]; originalArmor = p.armor[3];
            p.controlUseItem = p.controlUseTile = p.controlTorch = false; Main.mouseLeft = Main.mouseRight = false;
            p.itemAnimation = p.itemTime = 0; Main.playerInventory = false;
            NativeQuickItemChecks.Sample(input, new Keys[0]);
            Fixture.Require((bool)Call(movement, "BeginUpdate", p), "fixture enters the real local movement owner");
            try
            {
                object borrow = Get(fall, "borrow"); long generation = (long)Get(Get(Get(context, "Runtime"), "SharedRuntime"), "Generation");
                Fixture.Require((bool)Call(borrow, "TryBorrow", p, generation, shoe, SmartFallFamily.Horseshoe), "fixture actual fall owner exchanges the carried original shoe");
                Fixture.Require(ReferenceEquals(p.armor[3], shoe) && ReferenceEquals(p.inventory[14], originalArmor), "fixture fall loan uses exact real native containers");
            }
            finally { Call(movement, "EndUpdate", p); }
        }

        private static void InitializeFishing(object context)
        {
            NativeToolExecutionChecks.Initialize(context, null);
            // Full Player.Update consumes native world edges. Bind this scene's
            // actual 120-tile grid before that call; default zero edges do not
            // describe the fixture's legal world or its grounded starting player.
            Main.rightWorld = Main.maxTilesX * 16; Main.bottomWorld = Main.maxTilesY * 16;
            Main.FishDropsDB = new Terraria.GameContent.FishDropRules.FishDropRuleList();
            new Terraria.GameContent.FishDropRules.GameContentFishDropPopulator(Main.FishDropsDB).Populate();
            if (Main.instance == null) { Main.instance = (Main)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Main)); GC.SuppressFinalize(Main.instance); }
            var chum = typeof(Main).GetField("ChumBucketProjectileHelper", F); chum.SetValue(Main.instance, Activator.CreateInstance(chum.FieldType));
            // Reuse the existing real cast/return fixture; only its own context
            // references are bound here, never production loan/session fields.
            foreach (string name in new[] { "context", "host", "owner", "tools", "input" })
            {
                object value = name == "context" ? context : name == "host" ? Get(context, "Fishing") :
                    name == "owner" ? Get(Get(context, "Fishing"), "Equipment") : Get(context, name == "tools" ? "Tools" : "Input");
                typeof(NativeFishingEquipmentChecks).GetField(name, F).SetValue(null, value);
            }
        }
        private static bool SkipAchievement() { return false; }
        private static void ToolsState(string phase, object tools, object use, Player p)
        {
            Console.WriteLine("TOOLS STATE " + phase + " Active=" + Get(use, "Active") + " buffer=" + p.selectedItemState.HasBufferedChange +
                " override=" + p.selectedItemState.HasActiveOverride + " selected=" + p.selectedItem + " original=" + GetOptional(use, "original") +
                " SelectionIntent=" + Get(tools, "SelectionIntent") + " leaseIntent=" + GetOptional(use, "selection") +
                " returnRequested=" + GetOptional(use, "returnRequested") + " InNativeUse=" + Get(use, "InNativeUse") +
                " checkedItem=" + GetOptional(use, "checkedItem") + " cancelled=" + GetOptional(use, "cancelled") + " token=" + Get(use, "Operation"));
        }
        private static void ObserveToolsRetire(object __instance)
        {
            if (ReferenceEquals(__instance, trackedToolsUse))
                Console.WriteLine("TOOLS RETIRE CALL " + new System.Diagnostics.StackTrace(1, false));
        }
        private static bool RefuseDeathSave() { unrelatedDeathSaves++; return false; }
        private static void LocalDraw(object context)
        {
            object runtime = Get(Get(context, "Runtime"), "SharedRuntime"), information = Get(context, "Information");
            Type worker = context.GetType().DeclaringType; FieldInfo current = worker.GetField("postfixContext", F);
            object previous = current.GetValue(null); bool preference = (bool)Get(Get(context, "preferences"), "BiomeEnabled");
            MethodInfo draw = Get(information, "Hud").GetType().GetMethod("Draw", F);
            var patch = new Harmony("JueMingR.Tests.CommonFailureLocalDraw"); displayFaults = 0;
            try
            {
                patch.Patch(draw, prefix: new HarmonyMethod(typeof(NativeRuntimeFailureReturnChecks), nameof(ThrowDisplay)));
                current.SetValue(null, context);
                Fixture.Require((bool)worker.GetMethod("DrawBiomeDisplayLayer", F).Invoke(null, null), "local draw failure preserves later native layers");
            }
            finally { current.SetValue(null, previous); patch.Unpatch(draw, HarmonyPatchType.All, patch.Id); }
            Call(context, "UpdateRuntime");
            Fixture.Require(displayFaults == 1 && (bool)Get(runtime, "IsSessionActive") && !(bool)Get(Get(context, "Shell"), "Failed") &&
                (bool)Get(Get(context, "Tools"), "Available") && (bool)Get(Get(context, "QuickItems"), "Available") &&
                (bool)Get(Get(context, "preferences"), "BiomeEnabled") == preference, "the actual optional biome/HUD Draw fault stays local and preserves adjacent owners and desired setting");
        }
        private static void ThrowDisplay() { displayFaults++; throw new InvalidOperationException("controlled optional HUD draw failure"); }
        private static void NativeWorldStep(object context, Action step)
        {
            // The CPU fixture does not run all of Main.DoUpdateInWorld_Inner.
            // Supply its exact production nesting boundary so the real patched
            // Player.Update owns BeginPlayer/Capture/EndPlayer, including its
            // original finalizer. No movement callback or return is substituted.
            Type hooks = Get(context, "Movement").GetType().Assembly.GetType("JueMingR.TerrariaHost.Movement.MovementHooks", true);
            hooks.GetMethod("BeginWorld", F).Invoke(null, null);
            try { step(); }
            finally { hooks.GetMethod("EndWorld", F).Invoke(null, new object[] { null }); }
        }
        private static void CommonFault(object context)
        { context.GetType().DeclaringType.GetMethod("HandlePostfixFailure", F).Invoke(null, new object[] { context, "RUNTIME", new IOException("controlled common callback failure") }); }
        private static void ProbeFault(object context)
        {
            object runtime = Get(Get(context, "Runtime"), "SharedRuntime"), probe = Get(runtime, "sessionProbe");
            MethodInfo getter = probe.GetType().GetProperty("IsSessionActive", F).GetGetMethod(true);
            var patch = new Harmony("JueMingR.Tests.CommonObservationFault"); probeEntered = false;
            try
            {
                patch.Patch(getter, prefix: new HarmonyMethod(typeof(NativeRuntimeFailureReturnChecks), nameof(ThrowObservation)));
                Call(context, "UpdateRuntime");
                Fixture.Require(probeEntered && !(bool)Get(runtime, "IsSessionActive"), "actual session probe getter throws and the runtime withdraws admission");
            }
            finally { patch.Unpatch(getter, HarmonyPatchType.All, patch.Id); }
        }
        private static void ThrowObservation() { probeEntered = true; throw new IOException("controlled actual session observation failure"); }
        private sealed class SaveGate : IDisposable
        {
            private readonly ManualResetEventSlim entered = new ManualResetEventSlim(), release = new ManualResetEventSlim();
            private readonly Thread holder;
            internal SaveGate()
            {
                object gate = typeof(Player).GetField("IOLock", F).GetValue(null);
                holder = new Thread(() => { lock (gate) { entered.Set(); release.Wait(); } }) { IsBackground = true };
                holder.Start();
                if (!entered.Wait(5000)) { release.Set(); throw new TimeoutException("fixture player-save lock was not entered"); }
            }
            public void Dispose()
            { release.Set(); if (!holder.Join(5000)) throw new TimeoutException("fixture save owner did not retire"); entered.Dispose(); release.Dispose(); }
        }
    }
}
