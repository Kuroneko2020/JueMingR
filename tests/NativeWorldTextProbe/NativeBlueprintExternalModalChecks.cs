using System;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBlueprintExternalModalChecks
    {
        internal static void Cpu(object context)
        {
            object input = Get(context, "Input");
            Guid request = Guid.NewGuid(); bool focused = true;
            object priorGameWindow = Get(input, "gameWindow"), priorForegroundWindow = Get(input, "foregroundWindow");
            bool priorSelected = FocusHelper.IsSelectedApplication;
            Set(input, "gameWindow", (Func<IntPtr>)(() => new IntPtr(3)));
            Set(input, "foregroundWindow", (Func<IntPtr>)(() => focused ? new IntPtr(3) : new IntPtr(4)));
            Action<Keys[], int> sample = (keys, mask) =>
            {
                FocusHelper.IsSelectedApplication = focused;
                Call(input, "BeginUpdate");
                var mouse = new MouseState(20, 20, 0, (mask & 1) != 0 ? ButtonState.Pressed : ButtonState.Released,
                    (mask & 4) != 0 ? ButtonState.Pressed : ButtonState.Released, (mask & 2) != 0 ? ButtonState.Pressed : ButtonState.Released,
                    (mask & 8) != 0 ? ButtonState.Pressed : ButtonState.Released, (mask & 16) != 0 ? ButtonState.Pressed : ButtonState.Released);
                PlayerInput.MouseInfo = mouse; PlayerInput.Triggers.Reset(); PlayerInput.Triggers.Current.Up = true;
                Call(input, "AfterMapping");
                // Native keyboard refresh follows mapping. Do not feed an
                // already-consumed synthetic empty keyState as release proof.
                Main.keyState = new KeyboardState(keys); Call(input, "AfterKeyboardRefresh"); NativeQuickItemChecks.BeginWorldStep();
            };
            try
            {
            sample(Array.Empty<Keys>(), 0); sample(Array.Empty<Keys>(), 0);
            Require((bool)Get(input, "CanStartActions") && (bool)Get(input, "CanRunAutomaticActions"), "real neutral input starts ordinary actions");
            Require((IntPtr)Call(input, "CaptureGameWindow") == new IntPtr(3), "owner is captured game HWND");
            Require((bool)Call(input, "TryAcquireExternalModal", request), "one native request acquires modal lease");
            Require(!(bool)Call(input, "TryAcquireExternalModal", Guid.NewGuid()), "second request cannot replace modal owner");
            sample(Array.Empty<Keys>(), 0);
            Require(!(bool)Get(input, "CanStartActions") && !(bool)Get(input, "CanRunAutomaticActions") && (bool)Get(input, "CanRetainAutomaticIntent"), "even focused neutral Show blocks new actions while retaining existing responsibility");
            Require((bool)Get(input, "CanRunOwnedContinuation"), "exact existing owners retain finite gameplay-step cleanup outlet");
            focused = false; sample(Array.Empty<Keys>(), 0);
            Require(!(bool)Get(input, "CanRunAutomaticActions") && (bool)Get(input, "CanRetainAutomaticIntent"), "background automation exception cannot bypass native modal");
            Call(input, "ReturnExternalModal", Guid.NewGuid());
            Require(Get(input, "ExternalModalState").ToString() == "ModalOwned", "late other request cannot return this modal");
            Call(input, "ReturnExternalModal", request); sample(Array.Empty<Keys>(), 0);
            Require(Get(input, "ExternalModalState").ToString() == "AwaitFocusedNeutral", "background synthetic neutral is not release proof");
            focused = true;
            sample(new[] { Keys.Enter }, 0);
            Require((bool)Get(input, "ExternalModalBusy"), "native Save Enter remains consumed after refocus");
            for (int button = 0; button < 5; button++)
            { sample(Array.Empty<Keys>(), 1 << button); Require((bool)Get(input, "ExternalModalBusy"), "Mouse" + (button + 1) + " must physically release"); }
            sample(Array.Empty<Keys>(), 0);
            Require(!(bool)Get(input, "ExternalModalBusy") && !(bool)Get(input, "CanStartActions") && !PlayerInput.Triggers.Current.Up, "trusted neutral retires lease but consumes this sample");
            sample(new[] { Keys.W }, 0);
            Require((bool)Get(input, "CanStartActions") && (bool)Get(input, "CanRunAutomaticActions"), "next independent gesture restores actions while exchange IO may remain busy");
            // The no-observed-focus-loss path must use the same real neutral.
            Guid immediate = Guid.NewGuid(); Call(input, "TryAcquireExternalModal", immediate); Call(input, "ReturnExternalModal", immediate);
            sample(new[] { Keys.Escape }, 0); Require((bool)Get(input, "ExternalModalBusy"), "Cancel Esc cannot leak when OS focus never changed");
            sample(Array.Empty<Keys>(), 0); sample(Array.Empty<Keys>(), 0);
            Require((bool)Get(input, "CanStartActions"), "immediate cancel rearms only after real neutral plus next sample");
            Console.WriteLine("PASS native modal shared production input: single lease, focused/background new-action barrier, retained finite cleanup, all keyboard+Mouse1-5 neutral tail, no focus-loss path and stale return rejection; OS interaction not claimed.");
            }
            finally
            {
                focused = true;
                if (Get(input, "ExternalModalState").ToString() == "ModalOwned")
                    Call(input, "ReturnExternalModal", (Guid)Get(input, "externalModalRequest"));
                sample(Array.Empty<Keys>(), 0); sample(Array.Empty<Keys>(), 0);
                Set(input, "gameWindow", priorGameWindow); Set(input, "foregroundWindow", priorForegroundWindow);
                FocusHelper.IsSelectedApplication = priorSelected;
            }
        }
    }
}
