using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Terraria;

namespace JueMingR.TerrariaHost.Movement
{
    internal static class MovementHooks
    {
        private const string Id = "JueMingR.Movement.Input1458";
        private static MovementSession owner;
        private static readonly List<MethodBase> targets = new List<MethodBase>();
        [ThreadStatic] private static int worldDepth;
        private struct UpdateScope { internal bool Active; }
        private struct ItemScope { internal long Operation; internal bool Entered; }
        internal static void Install(MovementSession value)
        {
            if (owner != null) throw new InvalidOperationException("movement-already-installed");
            var harmony = new Harmony(Id);
            try
            {
                Patch(harmony, AccessTools.DeclaredMethod(typeof(Main), "DoUpdateInWorld_Inner"), "BeginWorld", null, null, "EndWorld");
                Patch(harmony, AccessTools.DeclaredMethod(typeof(Player), "Update", new[] { typeof(int) }), "BeginPlayer", null, "InputBoundary", "EndPlayer");
                Patch(harmony, AccessTools.DeclaredMethod(typeof(Player), "JumpMovement"), "BeforeJump", "AfterJump", "JumpReads", null);
                Patch(harmony, AccessTools.DeclaredMethod(typeof(Player), "CarpetMovement"), null, null, "CarpetReads", null);
                Patch(harmony, AccessTools.DeclaredMethod(typeof(Player), "WingMovement"), null, "AfterFlight", null, null);
                Patch(harmony, AccessTools.DeclaredMethod(typeof(Player), "UpdateEquips", new[] { typeof(int) }), null, "AfterEquips", null, null);
                Patch(harmony, AccessTools.DeclaredMethod(typeof(Player), "RefreshDoubleJumps", Type.EmptyTypes), null, "ResourcesRefreshed", null, null);
                Patch(harmony, AccessTools.DeclaredMethod(typeof(Player), "GrappleMovement"), null, "AfterGrapple", null, null);
                Patch(harmony, AccessTools.DeclaredMethod(typeof(Player), "ItemCheck", Type.EmptyTypes), "BeforeItemCheck", null, null, "EndItemCheck");
                Patch(harmony, AccessTools.DeclaredMethod(typeof(Player), "PickItemSelectionOverride", new[] { typeof(int).MakeByRefType() }), null, "PickSelection", null, null);
                Patch(harmony, AccessTools.DeclaredMethod(typeof(Player), "TrySyncingInput", Type.EmptyTypes), "BeforeSync", null, null, null);
                Patch(harmony, AccessTools.DeclaredMethod(typeof(Player), "TrySwitchingLoadout", new[] { typeof(int) }), "BeforeLoadout", null, null, "AfterLoadout");
                var selection = typeof(Player).GetNestedType("SelectedItemState", BindingFlags.Public | BindingFlags.NonPublic);
                if (selection == null) throw new MissingMemberException("movement-selected-item-state");
                Patch(harmony, AccessTools.DeclaredMethod(selection, "Select", new[] { typeof(int) }), "ManualSelection", null, null, null);
                owner = value;
            }
            catch { foreach (var target in targets) harmony.Unpatch(target, HarmonyPatchType.All, Id); targets.Clear(); throw; }
        }
        private static void Patch(Harmony harmony, MethodInfo target, string prefix, string postfix, string transpiler, string finalizer)
        {
            if (target == null || target.GetMethodBody() == null) throw new MissingMethodException("movement-native-boundary");
            targets.Add(target);
            harmony.Patch(target, prefix: prefix == null ? null : new HarmonyMethod(typeof(MovementHooks), prefix), postfix: postfix == null ? null : new HarmonyMethod(typeof(MovementHooks), postfix), transpiler: transpiler == null ? null : new HarmonyMethod(typeof(MovementHooks), transpiler), finalizer: finalizer == null ? null : new HarmonyMethod(typeof(MovementHooks), finalizer));
        }
        internal static void Uninstall(MovementSession value)
        { if (!ReferenceEquals(owner, value)) return; owner = null; var harmony = new Harmony(Id); foreach (var target in targets) harmony.Unpatch(target, HarmonyPatchType.All, Id); targets.Clear(); }
        private static void BeginWorld() { worldDepth++; }
        private static Exception EndWorld(Exception __exception) { worldDepth--; return __exception; }
        private static void BeginPlayer(Player __instance, out UpdateScope __state)
        { __state = new UpdateScope { Active = owner != null && worldDepth == 1 && owner.BeginUpdate(__instance) }; }
        private static Exception EndPlayer(Player __instance, UpdateScope __state, Exception __exception)
        {
            try { if(worldDepth==1)owner?.ObserveResourcesAfter(__instance);if (__state.Active) owner?.EndUpdate(__instance); }
            catch when (__exception != null) { /* Preserve the native failure after attempting owned cleanup. */ }
            return __exception;
        }
        private static void BeforeJump(Player __instance) { if(worldDepth==1)owner?.ObserveResourcesBefore(__instance);owner?.BeforeJump(__instance); }
        private static void AfterJump(Player __instance) { if(worldDepth==1)owner?.ObserveResourcesAfter(__instance);owner?.AfterJump(__instance); }
        private static void ResourcesRefreshed(Player __instance) { if(worldDepth==1)owner?.ObserveResourcesRefresh(__instance); }
        private static void AfterFlight(Player __instance) { owner?.AfterFlight(__instance); }
        private static void Capture(Player player) { owner?.CaptureInput(player); }
        private static void AfterEquips(Player __instance) { owner?.FallHost.AfterEquips(__instance); }
        private static void BeforeGrapple(Player player) { owner?.FallHost.BeforeGrapple(player); }
        private static void AfterGrapple(Player __instance) { owner?.FallHost.AfterGrapple(__instance); }
        private static void BeforeItemCheck(Player __instance, out ItemScope __state)
        { long operation = 0; bool entered = owner != null && owner.FallHost.BeginItemCheck(__instance, out operation); __state = new ItemScope { Operation = operation, Entered = entered }; }
        private static Exception EndItemCheck(Player __instance, ItemScope __state, Exception __exception)
        {
            try { owner?.FallHost.EndItemCheck(__instance, __state.Operation, __state.Entered, __exception); }
            catch when (__exception != null) { /* The original ItemCheck exception remains authoritative. */ }
            return __exception;
        }
        private static void PickSelection(Player __instance, ref int __0, ref bool __result) { owner?.FallHost.Pick(__instance, ref __0, ref __result); }
        private static void BeforeSync(Player __instance) { owner?.FallHost.BeforeSync(__instance); }
        private static void ManualSelection(Player ___player) { owner?.FallHost.ManualSelection(___player); }
        private static void BeforeLoadout(Player __instance, out int __state) { __state = __instance.CurrentLoadoutIndex; }
        private static Exception AfterLoadout(Player __instance, int __state, Exception __exception)
        {
            try { if (__instance.CurrentLoadoutIndex != __state) owner?.FallHost.ManualLoadout(__instance); }
            catch when (__exception != null) { /* Keep native partial-loadout failure primary. */ }
            return __exception;
        }
        private static IEnumerable<CodeInstruction> InputBoundary(IEnumerable<CodeInstruction> source)
        {
            var code = new List<CodeInstruction>(source);
            var left = AccessTools.Field(typeof(Player), "controlLeft"); var right = AccessTools.Field(typeof(Player), "controlRight");
            int count = 0;
            for (int i = 0; i + 11 < code.Count; i++)
            {
                if (code[i].opcode != OpCodes.Ldarg_0 || !code[i + 1].LoadsField(left) || !BranchFalse(code[i + 2]) ||
                    code[i + 3].opcode != OpCodes.Ldarg_0 || !code[i + 4].LoadsField(right) || !BranchFalse(code[i + 5]) ||
                    code[i + 6].opcode != OpCodes.Ldarg_0 || code[i + 7].opcode != OpCodes.Ldc_I4_0 || !code[i + 8].StoresField(left) ||
                    code[i + 9].opcode != OpCodes.Ldarg_0 || code[i + 10].opcode != OpCodes.Ldc_I4_0 || !code[i + 11].StoresField(right)) continue;
                // Native text/map/spectator/remote-entity controls have already
                // been consumed; capture precedes ONLY vanilla's dual-key zero.
                // Preserve labels so every incoming path obtains this sample.
                var load = new CodeInstruction(OpCodes.Ldarg_0); load.labels.AddRange(code[i].labels); code[i].labels.Clear();
                code.InsertRange(i, new[] { load, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MovementHooks), "Capture")) }); i += 13; count++;
            }
            if (count != 1) throw new InvalidOperationException("movement-input-dual-key-boundary:" + count);
            // The unique native WOFTongue call immediately precedes the local
            // controlHook/releaseHook branch. Insert before its whole branch,
            // not inside the player's already-pressed QuickGrapple path.
            var tongue = AccessTools.DeclaredMethod(typeof(Player), "WOFTongue");
            int grappleBoundaries = 0;
            for (int i = 0; i < code.Count; i++)
                if (code[i].Calls(tongue))
                { code.InsertRange(i + 1, new[] { new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MovementHooks), "BeforeGrapple")) }); i += 2; grappleBoundaries++; }
            if (grappleBoundaries != 1) throw new InvalidOperationException("movement-grapple-boundary:" + grappleBoundaries);
            var jump=AccessTools.DeclaredMethod(typeof(Player),"JumpMovement");var grab=AccessTools.DeclaredMethod(typeof(Player),"GrabItems");
            int start=-1,end=-1;
            for(int i=0;i<code.Count;i++){if(code[i].Calls(jump)){if(start>=0)throw new InvalidOperationException("movement-jump-boundary-duplicate");start=i;}if(code[i].Calls(grab)){if(end>=0)throw new InvalidOperationException("movement-grab-boundary-duplicate");end=i;}}
            if(start<0 || end<=start)throw new InvalidOperationException("movement-flight-boundary-order");
            return MaskReads(code,start+1,end,8,"update-flight");
        }
        private static IEnumerable<CodeInstruction> JumpReads(IEnumerable<CodeInstruction> source)
        {var code=new List<CodeInstruction>(source);return MaskReads(code,0,code.Count,1,"jump");}
        private static IEnumerable<CodeInstruction> CarpetReads(IEnumerable<CodeInstruction> source)
        {var code=new List<CodeInstruction>(source);return MaskReads(code,0,code.Count,2,"carpet");}
        private static List<CodeInstruction> MaskReads(List<CodeInstruction> code,int start,int end,int expected,string name)
        {
            var jump=AccessTools.Field(typeof(Player),"controlJump");int count=0;
            // Keep each original read (and its labels/exception blocks) in
            // place. Only its stack value is filtered; no Player field writes,
            // saved-input restoration, branch rewrites or per-tick discovery.
            for(int i=end-1;i>=start;i--)if(code[i].LoadsField(jump))
            {code.InsertRange(i+1,new[]{new CodeInstruction(OpCodes.Ldarg_0),new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(MovementHooks),"EffectiveJump"))});count++;}
            if(count!=expected)throw new InvalidOperationException("movement-effective-jump-"+name+":"+count);
            return code;
        }
        private static bool EffectiveJump(bool nativeJump,Player player)
        {return owner==null?nativeJump:owner.EffectiveJump(player,nativeJump);}
        private static bool BranchFalse(CodeInstruction code) { return code.opcode == OpCodes.Brfalse || code.opcode == OpCodes.Brfalse_S; }
    }
}
