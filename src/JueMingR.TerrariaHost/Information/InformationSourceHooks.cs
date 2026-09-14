using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Terraria;

namespace JueMingR.TerrariaHost.Information
{
    // A separate Harmony owner: failure cannot disable item receipts or the
    // primary load chain. The success callbacks only publish readiness flags.
    internal sealed class InformationSourceHooks : IDisposable
    {
        private const string PatchOwner = "JueMingR.Information.Readiness";
        private static InformationReadiness current;
        private readonly InformationReadiness readiness;
        private readonly Harmony harmony = new Harmony(PatchOwner);
        private readonly List<MethodInfo> targets = new List<MethodInfo>();
        internal InformationSourceHooks(InformationReadiness readiness) { this.readiness = readiness; }
        internal void Install()
        {
            try
            {
                if (current != null) throw new InvalidOperationException("Information native observer already installed.");
                if (typeof(Main).Module.ModuleVersionId != new Guid("2c29f6c3-4bd9-4add-9c58-da159804e083")) throw new InvalidOperationException("Information requires the verified Terraria 1.4.5.8 module.");
                current = readiness;
                Patch(typeof(MessageBuffer), "GetData", new[] { typeof(int), typeof(int), typeof(int).MakeByRefType() }, null, null, nameof(MessageTranspiler));
                Patch(typeof(WorldGen), "clearWorld", Type.EmptyTypes, nameof(ClearBefore), nameof(ClearAfter));
                Patch(typeof(WorldGen), "CountTiles", new[] { typeof(int) }, nameof(WorldBefore), nameof(CountAfter));
                Patch(typeof(Terraria.IO.WorldFile), "LoadWorldFlags", new[] { typeof(BinaryReader), typeof(int) }, nameof(WorldBefore), nameof(FlagsAfter));
                Patch(typeof(Main), "AnglerQuestSwap", Type.EmptyTypes, nameof(WorldBefore), nameof(QuestAfter));
                readiness.Installed = true; AppDomain.CurrentDomain.ProcessExit += OnExit;
            }
            catch (Exception e) { readiness.Failure = e.GetType().Name + ": " + e.Message; Dispose(); }
        }
        private void Patch(Type type, string name, Type[] signature, string before, string after, string transpiler = null)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance, null, signature, null);
            if (method == null || method.ReturnType != typeof(void)) throw new MissingMethodException(type.FullName, name);
            targets.Add(method); harmony.Patch(method, Hook(before), Hook(after), Hook(transpiler));
            var info = Harmony.GetPatchInfo(method);
            if (info == null || !info.Owners.Contains(PatchOwner)) throw new InvalidOperationException("Information hook was not installed.");
        }
        private static HarmonyMethod Hook(string name) { return name == null ? null : new HarmonyMethod(typeof(InformationSourceHooks).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)); }
        private static InformationReadiness.Receipt BeginMessage(MessageBuffer source, int start, int length)
        { try { return current == null ? default(InformationReadiness.Receipt) : current.BeginMessage(source, start, length); } catch { return default(InformationReadiness.Receipt); } }
        private static void Applied57(InformationReadiness.Receipt receipt) { try { receipt.Owner?.AppliedMessage(receipt, 57); } catch { } }
        private static void Applied74(InformationReadiness.Receipt receipt) { try { receipt.Owner?.AppliedMessage(receipt, 74); } catch { } }
        private static void ClearBefore() { try { current?.BeginClear(); } catch { } }
        private static void ClearAfter(bool __runOriginal) { if (__runOriginal) { try { current?.EndClear(); } catch { } } }
        private static void WorldBefore(out InformationReadiness.Receipt __state)
        { try { __state = current == null ? default(InformationReadiness.Receipt) : current.BeginWorld(); } catch { __state = default(InformationReadiness.Receipt); } }
        private static void CountAfter(int __0, InformationReadiness.Receipt __state, bool __runOriginal)
        { if (__runOriginal) { try { __state.Owner?.PublishedCounts(__state, __0); } catch { } } }
        private static void FlagsAfter(int __1, InformationReadiness.Receipt __state, bool __runOriginal)
        { if (__runOriginal) { try { __state.Owner?.LoadedFlags(__state, __1); } catch { } } }
        private static void QuestAfter(InformationReadiness.Receipt __state, bool __runOriginal)
        { if (__runOriginal) { try { __state.Owner?.SwappedQuest(__state); } catch { } } }
        private static IEnumerable<CodeInstruction> MessageTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var code = instructions.ToList();
            var blood = typeof(WorldGen).GetField("tBlood"); var today = typeof(Main).GetField("anglerQuestFinished");
            int counts = FindSuccess(code, blood, "ReadByte", new[] { "tGood", "tEvil", "tBlood" });
            int quest = FindSuccess(code, today, "ReadBoolean", new[] { "anglerQuest", "anglerQuestFinished" });
            var receipt = generator.DeclareLocal(typeof(InformationReadiness.Receipt));
            // Local capture belongs to this invocation, including recursive or
            // interleaved callers. No global in-flight message slot is needed.
            var output = new List<CodeInstruction>(code.Count + 10) {
                new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldarg_1), new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Call, typeof(InformationSourceHooks).GetMethod(nameof(BeginMessage), BindingFlags.Static | BindingFlags.NonPublic)),
                new CodeInstruction(OpCodes.Stloc, receipt) };
            for (int i = 0; i < code.Count; i++)
            {
                output.Add(code[i]);
                if (i != counts && i != quest) continue;
                output.Add(new CodeInstruction(OpCodes.Ldloc, receipt));
                output.Add(new CodeInstruction(OpCodes.Call, typeof(InformationSourceHooks).GetMethod(i == counts ? nameof(Applied57) : nameof(Applied74), BindingFlags.Static | BindingFlags.NonPublic)));
            }
            return output;
        }
        private static int FindSuccess(List<CodeInstruction> code, FieldInfo last, string read, string[] stores)
        {
            var matches = Enumerable.Range(0, code.Count).Where(i => code[i].opcode == OpCodes.Stsfld && Equals(code[i].operand, last)).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("Information message success field is not unique.");
            int index = matches[0];
            if (index < 1 || index + 1 >= code.Count || code[index + 1].opcode != OpCodes.Ret || !(code[index - 1].operand is MethodInfo method) || method.DeclaringType != typeof(BinaryReader) || method.Name != read)
                throw new InvalidOperationException("Information message read/write/return changed.");
            int start = index;
            while (start > 0 && index - start < 40 && code[start - 1].opcode != OpCodes.Ret) start--;
            var actual = code.Skip(start).Take(index - start + 1).Where(c => c.opcode == OpCodes.Stsfld).Select(c => ((FieldInfo)c.operand).Name).ToArray();
            if (!actual.SequenceEqual(stores)) throw new InvalidOperationException("Information message complete field sequence changed.");
            // Fixed client branch directly precedes the early ret separating
            // this success sequence. A server early return never runs callback.
            bool clientGate = false;
            for (int i = Math.Max(0, start - 7); i + 2 < start; i++)
                if (code[i].opcode == OpCodes.Ldsfld && Equals(code[i].operand, typeof(Main).GetField("netMode")) && code[i + 1].opcode == OpCodes.Ldc_I4_1 &&
                    (code[i + 2].opcode == OpCodes.Beq || code[i + 2].opcode == OpCodes.Beq_S)) clientGate = true;
            if (!clientGate) throw new InvalidOperationException("Information message client gate changed.");
            if (code.Skip(start).Take(index - start + 2).Any(c => c.blocks.Count != 0)) throw new InvalidOperationException("Information message exception shape changed.");
            return index;
        }
        private void OnExit(object sender, EventArgs args) { Dispose(); }
        public void Dispose()
        {
            AppDomain.CurrentDomain.ProcessExit -= OnExit; readiness.Installed = false;
            foreach (var target in targets) { try { harmony.Unpatch(target, HarmonyPatchType.All, PatchOwner); } catch (Exception e) { readiness.Failure = "Unpatch: " + e.GetType().Name; } }
            targets.Clear(); if (ReferenceEquals(current, readiness)) current = null;
        }
    }
}
