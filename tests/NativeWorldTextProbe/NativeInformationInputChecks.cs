using System;
using System.IO;
using System.Reflection;
using System.Threading;
using JueMingR.Features.Notes;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Actual separately built shell/input/document consumers. Only foreground
    // window and physical samples are controlled; no device or game loop runs.
    internal static class NativeInformationInputChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal static void Run(object context, object information)
        {
            object shell = Get(context, "Shell"), input = Get(context, "Input"), state = Get(shell, "State");
            object adjustment = Get(information, "Adjustment"), hud = Get(information, "Hud");
            bool focused = true;
            Set(input, "gameWindow", (Func<IntPtr>)(() => new IntPtr(1)));
            Set(input, "foregroundWindow", (Func<IntPtr>)(() => focused ? new IntPtr(1) : IntPtr.Zero));
            SetStatic(typeof(Main), "_uiScaleMatrix", Matrix.Identity); Main.blockInput = false; Main.LocalPlayer.mouseInterface = Main.mouseText = false;
            SetStatic(typeof(PlayerInput), "_originalScreenWidth", 960); SetStatic(typeof(PlayerInput), "_originalScreenHeight", 640);
            SetStatic(typeof(PlayerInput), "RawMouseScale", Vector2.One);
            PlayerInput.Triggers.Initialize();
            Set(state, "Ready", true);
            Action<int, int, bool, Keys[]> frame = (x, y, left, keys) =>
            {
                Main.keyState = new KeyboardState(keys);
                PlayerInput.MouseInfo = new MouseState(x, y, 0, left ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                PlayerInput.Triggers.Reset(); PlayerInput.Triggers.Current.MouseLeft = left; PlayerInput.Triggers.Update(); Main.mouseLeft = left;
                FocusHelper.IsSelectedApplication = focused;
                Exception caught = null;
                EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> capture = (sender, args) => caught = args.Exception;
                AppDomain.CurrentDomain.FirstChanceException += capture;
                try { Call(input, "BeginUpdate"); Call(input, "AfterMapping"); Call(input, "AfterKeyboardRefresh"); Call(shell, "ProcessInput"); }
                finally { AppDomain.CurrentDomain.FirstChanceException -= capture; }
                Require(!(bool)Get(shell, "Failed"), "actual shell failed while handling information gesture: " + caught);
            };
            var keysNone = new Keys[0];
            frame(30, 40, false, keysNone); frame(30, 40, false, keysNone);
            var registry = (HotkeyRegistry)Get(Get(shell, "hotkeys"), "Registry");
            Action begin = () => { Require(registry.Find("information-window.adjust").Invoke(HotkeyContext.Multiplayer), "registered position action is available"); Require((bool)Get(adjustment, "Active") && (bool)Get(hud, "Visible"), "all-off entry prepares an actual placeholder"); };
            Type worker = context.GetType().DeclaringType;
            object priorContext = worker.GetField("postfixContext", Flags).GetValue(null);
            worker.GetField("postfixContext", Flags).SetValue(null, context);
            Action drawPass = () =>
            {
                Main.LocalPlayer.mouseInterface = Main.mouseText = false;
                worker.GetMethod("DrawBiomeDisplayLayer", Flags).Invoke(null, null);
                Call(shell, "EndPointerLayer");
            };
            Func<int> xOf = () => (int)(float)Get(Get(hud, "Bounds"), "X") + 10;
            Func<int> yOf = () => (int)(float)Get(Get(hud, "Bounds"), "Y") + 10;
            Func<long> revision = () => (long)Get(Get(information, "Position"), "Revision");
            try
            {
                begin(); int x = xOf(), y = yOf(); frame(x, y, false, keysNone);
                frame(x, y, true, keysNone); Require(!(bool)Get(adjustment, "Dragging"), "unknown native hit pass cannot grab");
                frame(x, y, false, keysNone); drawPass(); long before = revision();
                frame(x, y, true, keysNone); Require((bool)Get(adjustment, "Dragging"), "matched completed native pass admits fresh HUD press");
                frame(x + 30, y + 20, true, keysNone); frame(x + 45, y + 30, false, keysNone);
                Require(!(bool)Get(adjustment, "Active") && revision() == before + 1, "real release submits once through position document");
                Require(!Main.mouseLeft && !PlayerInput.Triggers.JustReleased.MouseLeft, "owned release tail is consumed by actual shell");
                Call(information, "PrepareHud"); begin(); x = xOf(); y = yOf(); frame(x, y, false, keysNone); drawPass();
                frame(x, y, true, keysNone); frame(x + 20, y, true, keysNone); before = revision();
                SetStatic(typeof(Main), "_uiScaleMatrix", Matrix.CreateScale(1.25f)); frame(x + 30, y, false, keysNone);
                Require(!(bool)Get(adjustment, "Active") && revision() == before, "first resized release cancels before stale geometry can commit");
                SetStatic(typeof(Main), "_uiScaleMatrix", Matrix.Identity); Call(information, "PrepareHud"); begin(); x = xOf(); y = yOf();
                frame(x, y, false, keysNone); drawPass(); frame(x, y, true, keysNone); frame(x + 20, y, true, keysNone);
                focused = false; frame(x + 30, y, false, keysNone); Call(information, "FinishNormalAdjustment");
                Require(!(bool)Get(adjustment, "Active") && revision() == before, "focus-loss synthetic release cannot commit now or on later normal exit");
                focused = true; frame(x, y, false, keysNone); frame(x, y, false, keysNone); frame(x, y, false, keysNone);
                Call(information, "PrepareHud"); begin(); x = xOf(); y = yOf(); frame(x, y, false, keysNone); drawPass();
                frame(x, y, true, keysNone); frame(x + 30, y, true, keysNone); Main.gameMenu = true; frame(x + 30, y, false, keysNone);
                Require(revision() == before + 1 && !(bool)Get(adjustment, "Active"), "normal menu exit accepts only cached foreground drag");
                Main.gameMenu = false;
                CheckNotesLeave(shell, state);
                Console.WriteLine("PASS: real shell empty entry, native hit receipt, drag release, resized release, focus loss, normal exit and Notes save-dependent navigation.");
            }
            finally
            { worker.GetField("postfixContext", Flags).SetValue(null, priorContext); Main.gameMenu = false; SetStatic(typeof(Main), "_uiScaleMatrix", Matrix.Identity); }
        }
        private static void CheckNotesLeave(object shell, object state)
        {
            object presentation = Get(shell, "notes"); var workspace = (NotesWorkspace)Get(presentation, "workspace");
            Drain(workspace); Require(workspace.Request(new NotesAction(NotesActionKind.Create)), "isolated Notes create accepted"); Drain(workspace);
            string id = workspace.Feature.Saved.Notes[0].Id;
            Call(state, "Navigate", 4); Call(state, "RestoreVisible");
            workspace.Request(new NotesAction(NotesActionKind.BeginEdit, id)); workspace.Editor.Insert("first saved draft");
            bool? completed = null;
            Require((bool)Call(presentation, "RequestSafeLeave", (Action<bool>)(ok => completed = ok)), "safe leave accepts dependent save");
            Call(presentation, "Request", new NotesAction(NotesActionKind.Leave, x: 9)); Drain(workspace); Call(presentation, "ApplyNavigation");
            Require(completed == false && (bool)Get(state, "Visible") && workspace.Editor != null && !workspace.Editor.Dirty,
                "cancelled old leave cannot revive; accepted save still acknowledges baseline");
            completed = null; Require((bool)Call(presentation, "RequestSafeLeave", (Action<bool>)(ok => completed = ok)), "fresh safe leave accepted");
            Call(presentation, "ApplyNavigation"); Require(completed == true && !(bool)Get(state, "Visible"), "only actual navigation completes safe leave");
            Call(state, "RestoreVisible"); workspace.Request(new NotesAction(NotesActionKind.BeginEdit, id)); workspace.Editor.Insert("kept on conflict");
            string isolated = Path.Combine(Terraria.Program.SavePath, "information-composition", "JueMingRData", "notes", "notes.json");
            Require(File.Exists(isolated), "test must identify its exact isolated Notes file"); File.WriteAllText(isolated, "external conflict bytes");
            completed = null; Call(presentation, "RequestSafeLeave", (Action<bool>)(ok => completed = ok)); Drain(workspace); Call(presentation, "ApplyNavigation");
            Require(completed == false && (bool)Get(state, "Visible") && workspace.Editor != null && workspace.Editor.Dirty, "failed safe-leave save retains original UI and draft");
        }
        private static void Drain(NotesWorkspace workspace)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            do { workspace.Poll(); if (DateTime.UtcNow > deadline) throw new TimeoutException("isolated Notes worker"); Thread.Sleep(1); }
            while (!workspace.Feature.Loaded || workspace.Feature.Busy);
        }
        private static void SetStatic(Type type, string name, object value)
        { var field = type.GetField(name, Flags); if (field != null) field.SetValue(null, value); else type.GetProperty(name, Flags).SetValue(null, value); }
    }
}
