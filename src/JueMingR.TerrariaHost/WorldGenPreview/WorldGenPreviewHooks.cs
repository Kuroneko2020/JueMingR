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
        [ThreadStatic] private static HostWorldGenPreview.Generation requestedEntry;
        [ThreadStatic] private static NativeEntry activeEntry;
        [ThreadStatic] private static GenerationCall generatingCall;
        private sealed class GenerationCall
        {
            internal HostWorldGenPreview.Generation Token;
            internal GenerationCall Parent;
        }
        private sealed class NativeEntry
        {
            internal WorldGenPreviewHooks Hooks;
            internal HostWorldGenPreview.Generation Generation;
            internal Terraria.UI.UIState Previous;
            internal UIWorldGenDebug Page;
            internal WorldGenerator.SnapshotFrequency Frequency;
            internal NativeEntry Parent;
        }
        private sealed class GenerationRetiredException : Exception
        {
            internal readonly HostWorldGenPreview.Generation Generation;
            internal GenerationRetiredException(HostWorldGenPreview.Generation generation) { Generation = generation; }
        }
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
                if (current != null || target != typeof(Main).Assembly)
                    throw new InvalidOperationException("World generation preview is already installed or its live Terraria object differs.");
                current = this;
                ownsPatchScope = true;
                progressTextures = new[] { "_texOuterCorrupt", "_texOuterCrimson", "_texOuterRandom", "_texOuterLower" }
                    .Select(n => typeof(UIGenProgressBar).GetField(n, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)).ToArray();
                if (progressTextures.Any(f => f == null || f.IsStatic || f.FieldType != typeof(Asset<Texture2D>)))
                    throw new MissingFieldException("World generation native progress texture footprint changed.");
                // Install the shared getter before patching/compiling callers.
                // Synchronous GenPassElement construction also reads Controller;
                // counting only top-level constructor reads misses that chain.
                var controllerGetter = Exact(typeof(UIWorldGenDebug), "get_Controller", true, typeof(WorldGenerator.Controller));
                var getterCode = PatchProcessor.GetOriginalInstructions(controllerGetter);
                if (getterCode.Count != 2 || !getterCode[0].LoadsField(typeof(WorldGenerator).GetField("CurrentController")) || getterCode[1].opcode != OpCodes.Ret)
                    throw new InvalidOperationException("World generation native Controller getter changed.");
                Patch(controllerGetter, before: nameof(NativeController));
                Patch(Exact(typeof(UIWorldLoad), "Update", false, typeof(void), typeof(GameTime)), after: nameof(LoadUpdated), transpiler: nameof(LoadEntry));
                Patch(Exact(typeof(UIWorldGenDebug), "Open", true, typeof(void)), before: nameof(NativeEntryBegin), finalizer: nameof(NativeEntryEnd));
                Patch(Exact(typeof(UIWorldGenDebug), "OnActivate", false, typeof(void)), before: nameof(NativeActivating));
                Patch(Exact(typeof(WorldGenerator), "GenerateWorld", false, typeof(bool)), before: nameof(GenerationBegin), transpiler: nameof(GenerationPublication), finalizer: nameof(GenerationEnd));
                Patch(Exact(typeof(WorldGenerator), "SetDebugWorldGenUIVisibility", true, typeof(void), typeof(bool)), before: nameof(GenerationClosing));
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
        private static bool EntryAllowed()
        {
            // Native Update may already have passed its outer non-null check.
            // Keep owned closing calls suppressed; if their finalizer completed,
            // the old controller must not be borrowed across native cleanup.
            var controller = WorldGenerator.CurrentController;
            return DebugOptions.enableDebugCommands && controller != null &&
                !(current != null && current.Owner.OwnsGeneration) &&
                ReferenceEquals(controller, WorldGenerator.CurrentController);
        }
        internal void ProcessInput()
        {
            try
            {
                var entry = Owner.AdmitEntry();
                if (entry != null) OpenPreview(entry);
            }
            catch (Exception e) { Fail(e); }
        }
        private void OpenPreview(HostWorldGenPreview.Generation entry)
        {
            if (!Owner.IsCurrent(entry)) return;
            var previous = requestedEntry; requestedEntry = entry;
            try { UIWorldGenDebug.Open(); }
            finally { requestedEntry = previous; }
        }
        private static bool NativeEntryBegin(out NativeEntry __state)
        {
            __state = null;
            var hooks = current;
            if (requestedEntry != null && (hooks == null || !hooks.Owner.IsCurrent(requestedEntry))) return false;
            var entry = hooks?.Owner.BeginNativeEntry();
            if (requestedEntry != null && !ReferenceEquals(entry, requestedEntry))
            { if (entry != null) hooks.Owner.CompleteNativeEntry(entry, false); return false; }
            if (entry == null) return true;
            __state = new NativeEntry { Hooks = hooks, Generation = entry, Previous = Main.MenuUI?.CurrentState,
                Frequency = entry.Controller.SnapshotFrequency, Parent = activeEntry };
            activeEntry = __state;
            return true;
        }
        private static void NativeActivating(UIWorldGenDebug __instance)
        { if (activeEntry != null) activeEntry.Page = __instance; }
        private static bool NativeController(ref WorldGenerator.Controller __result)
        {
            var entry = activeEntry;
            if (entry == null) return true;
            // One thread-local Open scope covers the whole synchronous chain,
            // including child Index/color helpers. Outside it, run the original
            // getter body; never recurse into this patched getter for fallback.
            if (!entry.Hooks.Owner.IsCurrent(entry.Generation)) throw new GenerationRetiredException(entry.Generation);
            __result = entry.Generation.Controller;
            return false;
        }
        private static Exception NativeEntryEnd(Exception __exception, NativeEntry __state)
        {
            if (__state != null)
            {
                var owner = __state.Hooks.Owner;
                var retired = __exception as GenerationRetiredException;
                bool normalRetirement = retired != null && ReferenceEquals(retired.Generation, __state.Generation) && !owner.IsCurrent(__state.Generation);
                bool completed = __exception == null && owner.IsCurrent(__state.Generation) && Main.MenuUI?.CurrentState is UIWorldGenDebug;
                owner.CompleteNativeEntry(__state.Generation, completed);
                activeEntry = __state.Parent;
                if (!completed)
                {
                    if (ReferenceEquals(__state.Generation.Controller, WorldGenerator.CurrentController))
                        __state.Generation.Controller.SnapshotFrequency = __state.Frequency;
                    // Do not reclaim another page or a later generation's UI.
                    if (__state.Previous is UIWorldLoad && ReferenceEquals(Main.MenuUI?.CurrentState, __state.Page) && owner.CanRestore(__state.Generation))
                        Main.MenuUI.SetState(__state.Previous);
                }
                if (normalRetirement) return null;
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
        private static void GenerationBegin(WorldGenerator.Controller ____controller, out GenerationCall __state)
        {
            __state = current == null ? null : new GenerationCall { Token = current.Owner.Begin(____controller), Parent = generatingCall };
            if (__state != null) generatingCall = __state;
        }
        private static void Published() { if (generatingCall != null) current?.Owner.Published(generatingCall.Token); }
        // Retire BEFORE the native visibility check, not after its static-null
        // tail. An entry already activated is then closed by native main-thread
        // handoff; one still constructing must fail its scoped read/final check.
        private static void GenerationClosing(bool __0) { if (!__0 && generatingCall != null) current?.Owner.Retire(generatingCall.Token); }
        private static Exception GenerationEnd(Exception __exception, GenerationCall __state)
        {
            if (__state != null)
            { current?.Owner.Finish(__state.Token); if (ReferenceEquals(generatingCall, __state)) generatingCall = __state.Parent; }
            return __exception;
        }
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
