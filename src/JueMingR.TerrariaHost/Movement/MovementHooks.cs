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
        internal static void Install(MovementSession value)
        {
            if (owner != null) throw new InvalidOperationException("movement-already-installed");
            var harmony = new Harmony(Id);
            try
            {
                Patch(harmony, AccessTools.DeclaredMethod(typeof(Main), "DoUpdateInWorld_Inner"), "BeginWorld", null, null, "EndWorld");
                Patch(harmony, AccessTools.DeclaredMethod(typeof(Player), "Update", new[] { typeof(int) }), "BeginPlayer", null, "InputBoundary", "EndPlayer");
                Patch(harmony, AccessTools.DeclaredMethod(typeof(Player), "JumpMovement"), "BeforeJump", "AfterJump", null, null);
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
        { if (__state.Active) owner?.EndUpdate(__instance); return __exception; }
        private static void BeforeJump(Player __instance) { owner?.BeforeJump(__instance); }
        private static void AfterJump(Player __instance) { owner?.AfterJump(__instance); }
        private static void Capture(Player player) { owner?.CaptureInput(player); }
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
            return code;
        }
        private static bool BranchFalse(CodeInstruction code) { return code.opcode == OpCodes.Brfalse || code.opcode == OpCodes.Brfalse_S; }
    }
}
