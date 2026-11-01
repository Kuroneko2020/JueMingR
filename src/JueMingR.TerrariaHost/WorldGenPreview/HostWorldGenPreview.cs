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
            internal bool Entering;
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
        internal void Published()
        {
            var current = Volatile.Read(ref generation);
            if (current != null && ReferenceEquals(current.Controller, WorldGenerator.CurrentController))
                Volatile.Write(ref current.Progress, WorldGenerator.CurrentGenerationProgress);
        }
        internal void Retire(Generation value)
        { if (ReferenceEquals(Volatile.Read(ref generation), value)) Volatile.Write(ref generation, null); }
        internal bool IsGenerationAvailable
        {
            get
            {
                var current = Volatile.Read(ref generation);
                return Ready && current != null && Volatile.Read(ref current.Progress) != null &&
                    WorldGen.generatingWorld && WorldGen.isGeneratingOrLoadingWorld &&
                    ReferenceEquals(current.Controller, WorldGenerator.CurrentController) &&
                    ReferenceEquals(current.Progress, WorldGenerator.CurrentGenerationProgress);
            }
        }
        internal bool CanShow(UIWorldLoad page)
        { return IsGenerationAvailable && Main.gameMenu && Main.netMode != 2 &&
            Main.MenuUI != null && ReferenceEquals(Main.MenuUI.CurrentState, page) && !WorldGenerator.CurrentController.QueuedAbort; }
        internal bool AdmitEntry()
        {
            var page = Main.MenuUI == null ? null : Main.MenuUI.CurrentState as UIWorldLoad;
            if (page == null || !CanShow(page) || !input.CanStartActions ||
                Main.drawingPlayerChat || Main.blockInput || Terraria.GameInput.PlayerInput.WritingText ||
                !input.Hotkeys.IsNew((int)Keys.F5)) return false;
            // Reuse the native open call below this gate. The shared sample has
            // already advanced before MenuUI.Update; no second keyboard reader
            // or synthetic release may create a second entry across pages.
            input.Hotkeys.ConsumePress((int)Keys.F5);
            var current = Volatile.Read(ref generation);
            if (current == null) return false;
            current.Entering = true;
            return true;
        }
        internal void CompleteEntry(bool success)
        {
            var current = Volatile.Read(ref generation);
            if (current == null) return;
            current.Entering = false;
            if (success) current.Entered = true;
        }
        internal bool HasPreviewContext
        {
            get
            {
                var current = Volatile.Read(ref generation);
                return IsGenerationAvailable && current != null && (current.Entered || current.Entering);
            }
        }
        internal bool OwnsController(WorldGenerator.Controller controller)
        {
            var current = Volatile.Read(ref generation);
            return IsGenerationAvailable && current != null && current.Entered && ReferenceEquals(current.Controller, controller);
        }
        internal bool CanSaveQuickload
        { get { return Main.gameMenu && Main.netMode == 0 && Main.MenuUI != null && Main.MenuUI.CurrentState is UIWorldGenDebug && OwnsController(WorldGenerator.CurrentController); } }
        internal void Disable() { Ready = false; Volatile.Write(ref generation, null); }
    }
}
