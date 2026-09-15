using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Terraria;

namespace JueMingR.TerrariaHost.WorldTime
{
    internal sealed class WorldTimeSourceHooks : IDisposable
    {
        private const string Owner = "JueMingR.WorldTime.Source";
        private const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        private static WorldTimeSourceHooks current;
        private readonly Action<double> contribution;
        private readonly Harmony harmony = new Harmony(Owner);
        private MethodInfo target;
        internal WorldTimeSourceHooks(Action<double> contribution) { this.contribution = contribution ?? throw new ArgumentNullException(nameof(contribution)); }
        internal bool Ready { get; private set; }
        internal string Failure { get; private set; }
        internal void Install()
        {
            try
            {
                if (current != null) throw new InvalidOperationException("world-time-source-already-installed");
                if (typeof(Main).Module.ModuleVersionId != new Guid("2c29f6c3-4bd9-4add-9c58-da159804e083")) throw new InvalidOperationException("world-time-source-version");
                target = typeof(Main).GetMethod("UpdateTime", Flags, null, Type.EmptyTypes, null);
                if (target == null || target.IsPublic || !target.IsStatic || target.ReturnType != typeof(void) || target.ContainsGenericParameters || target.GetMethodBody() == null) throw new MissingMethodException("Main.UpdateTime");
                current = this; harmony.Patch(target, transpiler: new HarmonyMethod(typeof(WorldTimeSourceHooks).GetMethod(nameof(Rewrite), Flags)));
                if (!Harmony.GetPatchInfo(target).Owners.Contains(Owner)) throw new InvalidOperationException("world-time-source-not-installed"); Ready = true;
            }
            catch (Exception e) { Failure = e.GetType().Name + ": " + e.Message; Dispose(); }
        }
        private static void Applied(double value)
        { var owner = current; if (owner == null || !owner.Ready || value <= 0) return; try { owner.contribution(value); } catch (Exception e) { owner.Failure = "world-time-contribution: " + e.GetType().Name; } }
        private static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var code = instructions.ToList(); var time = typeof(Main).GetField("time"); var rate = typeof(Main).GetField("dayRate"); int found = -1;
            for (int i = 0; i + 4 < code.Count; i++)
                if (code[i].opcode == OpCodes.Ldsfld && Equals(code[i].operand, time) && code[i + 1].opcode == OpCodes.Ldsfld && Equals(code[i + 1].operand, rate) &&
                    code[i + 2].opcode == OpCodes.Conv_R8 && code[i + 3].opcode == OpCodes.Add && code[i + 4].opcode == OpCodes.Stsfld && Equals(code[i + 4].operand, time))
                { if (found >= 0) throw new InvalidOperationException("world-time-contribution-not-unique"); found = i; }
            if (found < 0 || code.Skip(found + 1).Take(4).Any(c => c.labels.Count != 0) || code.Skip(found).Take(5).Any(c => c.blocks.Count != 0)) throw new InvalidOperationException("world-time-contribution-shape");
            var actual = generator.DeclareLocal(typeof(double)); var output = new List<CodeInstruction>(code.Count + 4);
            for (int i = 0; i < code.Count; i++)
            {
                output.Add(code[i]);
                // Capture the operand actually on the evaluation stack, after
                // UpdateTimeRate and before later resets or rate mutations.
                if (i == found + 2) { output.Add(new CodeInstruction(OpCodes.Dup)); output.Add(new CodeInstruction(OpCodes.Stloc, actual)); }
                if (i == found + 4) { output.Add(new CodeInstruction(OpCodes.Ldloc, actual)); output.Add(new CodeInstruction(OpCodes.Call, typeof(WorldTimeSourceHooks).GetMethod(nameof(Applied), Flags))); }
            }
            return output;
        }
        public void Dispose()
        {
            Ready = false; if (ReferenceEquals(current, this)) current = null;
            if (target != null) { try { harmony.Unpatch(target, HarmonyPatchType.All, Owner); } catch (Exception e) { Failure = "world-time-unpatch: " + e.GetType().Name; } target = null; }
        }
    }
}
