using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JueMingR.Features.Movement;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Movement
{
    internal static class TeleportHooks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static Harmony harmony;
        private static Func<Player, bool> canCorrect;
        private static Action<string> reportFailure;
        internal static Func<Player, Item, Vector2, Vector2?> RescueTarget;
        internal static bool Available { get; private set; }

        internal static bool TryInstall(Func<Player, bool> permission, Action<string> report)
        {
            if (harmony != null) throw new InvalidOperationException("teleport-hooks-already-installed");
            canCorrect = permission ?? throw new ArgumentNullException(nameof(permission));
            reportFailure = report;
            try
            {
                var target = typeof(Player).GetMethod("ItemCheck_UseTeleportRod", Flags, null, new[] { typeof(Item) }, null);
                if (target == null || target.IsStatic || target.ReturnType != typeof(void) || target.GetMethodBody() == null)
                    throw new MissingMethodException("Player.ItemCheck_UseTeleportRod(Item)");
                // Pay the finite geometry initialization once at installation,
                // never on the first invalid destination in a gameplay frame.
                if (TeleportTargetSearch.CandidateCount == 0) throw new InvalidOperationException("teleport-offsets-empty");
                harmony = new Harmony("JueMingR.Movement.Teleport");
                harmony.Patch(target, transpiler: new HarmonyMethod(typeof(TeleportHooks), nameof(Target)));
                Available = true; return true;
            }
            catch (Exception error)
            {
                Action<string> failure = reportFailure;
                Uninstall();
                Report(failure, "teleport-hook-unavailable:" + error.GetType().Name + ":" + error.Message);
                return false;
            }
        }

        internal static void Uninstall()
        {
            Available = false;
            if (harmony != null)
                foreach (MethodBase method in harmony.GetPatchedMethods().ToArray())
                    harmony.Unpatch(method, HarmonyPatchType.All, harmony.Id);
            harmony = null; canCorrect = null; reportFailure = null; RescueTarget = null;
        }

        private static IEnumerable<CodeInstruction> Target(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var code = instructions.ToList();
            var limit = typeof(Player).GetMethod(nameof(Player.LimitPointToPlayerReachableArea), new[] { typeof(Vector2).MakeByRefType() });
            int found = -1, count = 0;
            for (int i = 0; i < code.Count; i++) if (code[i].Calls(limit)) { found = i; count++; }
            if (count != 1 || found < 2 || code[found - 2].opcode != OpCodes.Ldarg_0 ||
                (code[found - 1].opcode != OpCodes.Ldloca && code[found - 1].opcode != OpCodes.Ldloca_S))
                throw new InvalidOperationException("teleport-limit-unique-local-abi");
            var address = code[found - 1];
            int index = address.operand is LocalBuilder ? ((LocalBuilder)address.operand).LocalIndex : Convert.ToInt32(address.operand);
            var locals = __originalMethod.GetMethodBody().LocalVariables;
            if (index < 0 || index >= locals.Count || locals[index].LocalType != typeof(Vector2))
                throw new InvalidOperationException("teleport-limit-vector-local-abi");
            if (found + 3 >= code.Count || LoadLocalIndex(code[found + 1]) != index ||
                !code[found + 2].LoadsField(typeof(Vector2).GetField(nameof(Vector2.X))) || !code[found + 3].LoadsConstant(50f))
                throw new InvalidOperationException("teleport-limit-native-consumer-abi");
            // Copy only the operand/opcode. Original labels and EH markers stay
            // on the native instructions; no branch or exception region moves.
            code.InsertRange(found + 1, new[] {
                new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(address.opcode, address.operand),
                new CodeInstruction(OpCodes.Call, typeof(TeleportHooks).GetMethod(nameof(Correct), Flags)) });
            return code;
        }

        private static int LoadLocalIndex(CodeInstruction instruction)
        {
            if (instruction.opcode == OpCodes.Ldloc_0) return 0;
            if (instruction.opcode == OpCodes.Ldloc_1) return 1;
            if (instruction.opcode == OpCodes.Ldloc_2) return 2;
            if (instruction.opcode == OpCodes.Ldloc_3) return 3;
            if (instruction.opcode != OpCodes.Ldloc && instruction.opcode != OpCodes.Ldloc_S) return -1;
            return instruction.operand is LocalBuilder ? ((LocalBuilder)instruction.operand).LocalIndex : Convert.ToInt32(instruction.operand);
        }

        private static void Correct(Player player, Item item, ref Vector2 point)
        {
            if (!Available) return;
            Vector2 original = point;
            try
            {
                if (player == null || item == null || (item.type != 1326 && item.type != 5335) ||
                    Main.myPlayer < 0 || Main.myPlayer >= Main.player.Length ||
                    !ReferenceEquals(Main.player[Main.myPlayer], player)) return;
                // A rescue owns only this one native Vector2 local and its
                // exact selected item. Its target is independent of the user's
                // ordinary teleport-correction switch; no global mouse lease.
                var rescue = RescueTarget?.Invoke(player, item, point);
                if (rescue.HasValue) { point = rescue.Value; return; }
                if (!canCorrect(player)) return;
                int enumerated;
                TeleportTargetCorrection.TryCorrect(player, ref point, out enumerated);
            }
            catch (Exception error)
            {
                // The helper owns only this local. A partial query cannot leak
                // a candidate or swallow any exception from the native method.
                point = original; Available = false;
                Report(reportFailure, "teleport-correction-unavailable:" + error.GetType().Name + ":" + error.Message);
            }
        }

        private static void Report(Action<string> report, string reason)
        { try { report?.Invoke(reason); } catch { /* Failure reporting cannot replace native item use. */ } }
    }
}
