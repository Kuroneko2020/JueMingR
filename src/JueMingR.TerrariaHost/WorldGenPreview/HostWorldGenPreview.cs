using System;
using System.Threading;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent.UI.States;
using Terraria.WorldBuilding;
using JueMingR.TerrariaHost.Input;

namespace JueMingR.TerrariaHost.WorldGenPreview
{
    // The generator owns every world operation. This owner only publishes the
    // exact in-flight GenerateWorld invocation and admits its menu UI entry.
    internal sealed class HostWorldGenPreview
    {
        internal sealed class Generation
        {
            internal readonly WorldGenerator.Controller Controller;
            internal Generation(WorldGenerator.Controller controller) { Controller = controller; }
            internal GenerationProgress Progress;
            internal volatile bool Entered;
            internal volatile bool Entering;
        }
        private readonly HostInputState input;
        private Generation generation;
        internal bool Ready { get; set; }
        internal HostWorldGenPreview(HostInputState input) { this.input = input; }
        internal Generation Begin(WorldGenerator.Controller controller)
        {
            var next = new Generation(controller);
            Volatile.Write(ref generation, next);
            return next;
        }
        internal void Published(Generation current)
        {
            if (current != null && ReferenceEquals(Volatile.Read(ref generation), current) && ReferenceEquals(current.Controller, WorldGenerator.CurrentController))
                Volatile.Write(ref current.Progress, WorldGenerator.CurrentGenerationProgress);
        }
        internal void Retire(Generation value)
        { Interlocked.CompareExchange(ref generation, null, value); }
        internal bool IsCurrent(Generation current)
        {
            if (!Ready || current == null || current.Controller == null) return false;
            var progress = Volatile.Read(ref current.Progress);
            // Native generation retires these statics on its background task.
            // Keep the token's objects for every dereference; a successful
            // identity check never leases the lifetime of a static field.
            return progress != null && WorldGen.generatingWorld && WorldGen.isGeneratingOrLoadingWorld &&
                ReferenceEquals(current.Controller, WorldGenerator.CurrentController) &&
                ReferenceEquals(progress, WorldGenerator.CurrentGenerationProgress) &&
                !current.Controller.QueuedAbort && ReferenceEquals(Volatile.Read(ref generation), current);
        }
        private Generation Capture()
        { var current = Volatile.Read(ref generation); return IsCurrent(current) ? current : null; }
        internal bool IsGenerationAvailable
        {
            get
            {
                return Capture() != null;
            }
        }
        internal bool CanShow(UIWorldLoad page)
        { return Capture() != null && Main.gameMenu && Main.netMode != 2 &&
            Main.MenuUI != null && ReferenceEquals(Main.MenuUI.CurrentState, page); }
        // An owned generation's early native F5 branch must yield even when
        // global debug is enabled. Only the post-input callback dispatches it.
        internal bool OwnsGeneration { get { return Ready && Volatile.Read(ref generation) != null; } }
        internal bool CanRestore(Generation entry)
        { var current = Volatile.Read(ref generation); return current == null || ReferenceEquals(current, entry); }
        internal Generation AdmitEntry()
        {
            var current = Capture();
            var page = Main.MenuUI == null ? null : Main.MenuUI.CurrentState as UIWorldLoad;
            if (current == null || page == null || !Main.gameMenu || Main.netMode == 2 || !input.CanStartActions ||
                Main.drawingPlayerChat || Main.blockInput || Terraria.GameInput.PlayerInput.WritingText ||
                !input.Hotkeys.IsNew((int)Keys.F5) || !IsCurrent(current)) return null;
            // MenuUI.Update precedes native input in the locked client. This
            // consumer runs only after the shared production input postfix.
            input.Hotkeys.ConsumePress((int)Keys.F5);
            return current;
        }
        internal Generation BeginNativeEntry()
        {
            var current = Capture();
            if (current == null || !Main.gameMenu || Main.netMode == 2 || Main.MenuUI == null) return null;
            // Both the load-page F5 and native quickload regeneration call Open.
            // Construction needs display scope; operations wait for activation.
            current.Entered = false;
            current.Entering = true;
            return current;
        }
        internal void CompleteNativeEntry(Generation entry, bool success)
        {
            if (entry == null) return;
            entry.Entering = false;
            entry.Entered = success && ReferenceEquals(Volatile.Read(ref generation), entry) &&
                IsCurrent(entry) && Main.gameMenu && Main.netMode != 2;
        }
        internal bool HasPreviewContext
        {
            get
            {
                var current = Capture();
                return current != null && (current.Entered || current.Entering);
            }
        }
        internal bool OwnsController(WorldGenerator.Controller controller)
        {
            var current = Capture();
            return current != null && current.Entered && ReferenceEquals(current.Controller, controller);
        }
        internal bool CanSaveQuickload
        { get { return Main.gameMenu && Main.netMode == 0 && Main.MenuUI != null && Main.MenuUI.CurrentState is UIWorldGenDebug && OwnsController(WorldGenerator.CurrentController); } }
        internal void Disable() { Ready = false; Volatile.Write(ref generation, null); }
    }
}
