using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.GameContent.UI.States;
using Terraria.Testing;
using Terraria.Testing.ChatCommands;
using Terraria.WorldBuilding;
using JueMingR.TerrariaHost.Input;

namespace JueMingR.TerrariaHost.WorldGenPreview
{
    // This optional owner commits all required seams together and unpatches only
    // itself. A mismatch never takes down the core bootstrap/input patches.
    internal sealed class WorldGenPreviewHooks : IDisposable
    {
        private const string PatchOwner = "JueMingR.WorldGenPreview";
        private static WorldGenPreviewHooks current;
        private readonly Harmony harmony = new Harmony(PatchOwner);
        private readonly Action<Exception> report;
        private bool ownsPatchScope;
        private FieldInfo[] progressTextures;
        private readonly ConditionalWeakTable<UIWorldLoad, WorldGenPreviewBanner> banners = new ConditionalWeakTable<UIWorldLoad, WorldGenPreviewBanner>();
        internal readonly HostWorldGenPreview Owner;
        internal string Failure { get; private set; }
        internal WorldGenPreviewHooks(HostInputState input, Action<Exception> report)
        { Owner = new HostWorldGenPreview(input); this.report = report; }
        internal void Install(Assembly target)
        {
            try
            {
                if (current != null || target != typeof(Main).Assembly || target.ManifestModule.ModuleVersionId != new Guid("2c29f6c3-4bd9-4add-9c58-da159804e083"))
                    throw new InvalidOperationException("World generation preview requires the verified Terraria 1.4.5.8 module.");
                current = this;
                ownsPatchScope = true;
                progressTextures = new[] { "_texOuterCorrupt", "_texOuterCrimson", "_texOuterRandom", "_texOuterLower" }
                    .Select(n => typeof(UIGenProgressBar).GetField(n, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)).ToArray();
                if (progressTextures.Any(f => f == null || f.IsStatic || f.FieldType != typeof(Asset<Texture2D>)))
                    throw new MissingFieldException("World generation native progress texture footprint changed.");
                Patch(Exact(typeof(UIWorldLoad), "Update", false, typeof(void), typeof(GameTime)), after: nameof(LoadUpdated), transpiler: nameof(LoadEntry));
                Patch(Exact(typeof(UIWorldGenDebug), "Open", true, typeof(void)), before: nameof(NativeEntryBegin), finalizer: nameof(NativeEntryEnd));
                Patch(Exact(typeof(WorldGenerator), "GenerateWorld", false, typeof(bool)), before: nameof(GenerationBegin), transpiler: nameof(GenerationPublication), finalizer: nameof(GenerationEnd));
                Patch(Exact(typeof(WorldGenerator.Controller), "ReportException", false, typeof(void), typeof(string), typeof(Exception), typeof(bool)), transpiler: nameof(ExceptionGate));
                // Exactly one compiler-generated native control action owns this
                // call. No global command processor or Toolkit is registered.
                MethodInfo[] save = typeof(UIWorldGenDebug).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                    .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    .Where(m => m.GetMethodBody() != null && PatchProcessor.GetOriginalInstructions(m).Any(i => i.Calls(typeof(DebugUtils).GetMethod("QuickSPMessage")))).ToArray();
                if (save.Length != 1) throw new InvalidOperationException("World generation quickload control call is not unique.");
                Patch(save[0], transpiler: nameof(QuickloadAction));
                WorldGenPreviewLocalization.Install(harmony, target, () => Owner.HasPreviewContext);
                Owner.Ready = true;
            }
            catch (Exception e) { Fail(e); }
        }
        private void Fail(Exception error)
        {
            if (Failure != null) return;
            while (error is TargetInvocationException && error.InnerException != null) error = error.InnerException;
            Failure = error.GetType().Name + ": " + error.Message;
            Dispose(); report?.Invoke(error);
        }
        public void Dispose()
        {
            Owner.Disable();
            if (!ownsPatchScope) return;
            try { harmony.UnpatchAll(PatchOwner); }
            finally { ownsPatchScope = false; if (ReferenceEquals(current, this)) current = null; }
        }
        private static MethodInfo Exact(Type type, string name, bool isStatic, Type result, params Type[] args)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, args, null);
            if (method == null || method.IsStatic != isStatic || method.ReturnType != result || method.IsGenericMethod || method.GetMethodBody() == null)
                throw new MissingMethodException(type.FullName, name);
            return method;
        }
        private void Patch(MethodInfo target, string before = null, string after = null, string transpiler = null, string finalizer = null)
        {
            harmony.Patch(target, Hook(before), Hook(after), Hook(transpiler), Hook(finalizer));
            if (!Harmony.GetPatchInfo(target).Owners.Contains(PatchOwner)) throw new InvalidOperationException("World generation preview patch was not installed.");
        }
        private static HarmonyMethod Hook(string method)
        { return method == null ? null : new HarmonyMethod(typeof(WorldGenPreviewHooks).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)); }
        private static bool EntryAllowed() { try { return current != null && current.Owner.AdmitEntry(); } catch (Exception e) { current?.Fail(e); return false; } }
        private static void OpenPreview()
        {
            var hooks = current; var previous = Main.MenuUI?.CurrentState;
            var controller = WorldGenerator.CurrentController;
            var frequency = controller == null ? WorldGenerator.SnapshotFrequency.None : controller.SnapshotFrequency;
            try
            {
                UIWorldGenDebug.Open();
            }
            catch (Exception e)
            {
                if (controller != null && ReferenceEquals(controller, WorldGenerator.CurrentController)) controller.SnapshotFrequency = frequency;
                hooks?.Fail(e);
                // Native construction precedes SetState, but activation may
                // fail after state assignment. Restore only this entry's page.
                if (previous is UIWorldLoad && Main.MenuUI != null && Main.MenuUI.CurrentState is UIWorldGenDebug)
                    Main.MenuUI.SetState(previous);
            }
        }
        private static void NativeEntryBegin(out HostWorldGenPreview.Generation __state)
        { __state = current?.Owner.BeginNativeEntry(); }
        private static Exception NativeEntryEnd(Exception __exception, HostWorldGenPreview.Generation __state)
        {
            if (__state != null)
            {
                // A finalizer also runs on constructor/activation failure. Never
                // adopt a later generation or leave construction scope behind.
                if (current != null) current.Owner.CompleteNativeEntry(__state,
                    __exception == null && Main.MenuUI?.CurrentState is UIWorldGenDebug);
                else { __state.Entering = false; __state.Entered = false; }
            }
            return __exception;
        }
        private static void LoadUpdated(UIWorldLoad __instance, UIGenProgressBar ____progressBar)
        {
            var hooks = current; if (hooks == null || !hooks.Owner.Ready) return;
            try
            {
                if (!hooks.Owner.CanShow(__instance)) return;
                WorldGenPreviewBanner banner;
                if (!hooks.banners.TryGetValue(__instance, out banner))
                { banner = new WorldGenPreviewBanner(__instance, ____progressBar, hooks.Owner, hooks.Fail,
                    hooks.progressTextures.Select(f => (Asset<Texture2D>)f.GetValue(____progressBar)).ToArray()); __instance.Append(banner); hooks.banners.Add(__instance, banner); }
                banner.Prepare();
            }
            catch (Exception e) { hooks.Fail(e); }
        }
        private static void GenerationBegin(WorldGenerator.Controller ____controller, out HostWorldGenPreview.Generation __state)
        { __state = current == null ? null : current.Owner.Begin(____controller); }
        private static void Published() { current?.Owner.Published(); }
        private static Exception GenerationEnd(Exception __exception, HostWorldGenPreview.Generation __state)
        { if (__state != null) current?.Owner.Retire(__state); return __exception; }
        private static bool AllowException(bool original, WorldGenerator.Controller controller)
        { return original || current != null && current.Owner.OwnsController(controller); }
        private static void SaveQuickload(string command)
        {
            if (command == "/quickload-regen" && current != null && current.Owner.CanSaveQuickload)
                ToolkitDebugCommands.QuickLoadRegenCommand(new DebugMessage((byte)Main.myPlayer, command));
            else DebugUtils.QuickSPMessage(command);
        }
        private static IEnumerable<CodeInstruction> LoadEntry(IEnumerable<CodeInstruction> source)
        {
            var code = source.ToList(); var flag = typeof(DebugOptions).GetField("enableDebugCommands");
            int matches = 0;
            foreach (var instruction in code) if (instruction.LoadsField(flag))
            { instruction.opcode = OpCodes.Call; instruction.operand = typeof(WorldGenPreviewHooks).GetMethod(nameof(EntryAllowed), BindingFlags.NonPublic | BindingFlags.Static); matches++; }
            if (matches != 1 || code.Count(i => i.Calls(typeof(UIWorldGenDebug).GetMethod("Open"))) != 1)
                throw new InvalidOperationException("World generation native F5 entry shape changed.");
            foreach (var instruction in code) if (instruction.Calls(typeof(UIWorldGenDebug).GetMethod("Open")))
                instruction.operand = typeof(WorldGenPreviewHooks).GetMethod(nameof(OpenPreview), BindingFlags.NonPublic | BindingFlags.Static);
            return code;
        }
        private static IEnumerable<CodeInstruction> GenerationPublication(IEnumerable<CodeInstruction> source)
        {
            var output = new List<CodeInstruction>(); int matches = 0, stores = 0;
            var field = typeof(WorldGenerator).GetField("CurrentGenerationProgress");
            var progress = typeof(WorldGenerator).GetField("_progress", BindingFlags.NonPublic | BindingFlags.Instance);
            var code = source.ToList();
            for (int i = 0; i < code.Count; i++)
            {
                var instruction = code[i];
                output.Add(instruction);
                if (!instruction.StoresField(field)) continue;
                stores++;
                // The second store is the native null retirement. Publish only
                // after the exact instance-progress load, never after cleanup.
                if (i > 0 && code[i - 1].LoadsField(progress))
                { output.Add(new CodeInstruction(OpCodes.Call, typeof(WorldGenPreviewHooks).GetMethod(nameof(Published), BindingFlags.NonPublic | BindingFlags.Static))); matches++; }
            }
            if (matches != 1 || stores != 2) throw new InvalidOperationException("World generation publication shape changed: " + matches + "/" + stores);
            return output;
        }
        private static IEnumerable<CodeInstruction> ExceptionGate(IEnumerable<CodeInstruction> source)
        {
            var output = new List<CodeInstruction>(); int matches = 0;
            foreach (var instruction in source)
            {
                output.Add(instruction);
                if (instruction.LoadsField(typeof(DebugOptions).GetField("enableDebugCommands")))
                { output.Add(new CodeInstruction(OpCodes.Ldarg_0)); output.Add(new CodeInstruction(OpCodes.Call, typeof(WorldGenPreviewHooks).GetMethod(nameof(AllowException), BindingFlags.NonPublic | BindingFlags.Static))); matches++; }
            }
            if (matches != 1) throw new InvalidOperationException("World generation exception gate changed.");
            return output;
        }
        private static IEnumerable<CodeInstruction> QuickloadAction(IEnumerable<CodeInstruction> source)
        {
            var code = source.ToList(); int matches = 0;
            foreach (var instruction in code) if (instruction.Calls(typeof(DebugUtils).GetMethod("QuickSPMessage")))
            { instruction.opcode = OpCodes.Call; instruction.operand = typeof(WorldGenPreviewHooks).GetMethod(nameof(SaveQuickload), BindingFlags.NonPublic | BindingFlags.Static); matches++; }
            if (matches != 1) throw new InvalidOperationException("World generation quickload call changed.");
            return code;
        }
    }
}
