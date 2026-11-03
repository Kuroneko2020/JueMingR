using System;
using System.Collections.Generic;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Actual public input stages and Shell dispatch; no Draw proof is supplied.
    // A cancellation must leave no new pending request before any rendering.
    internal static class NativeTargetCancellationChecks
    {
        internal static void Run(object context)
        {
            var browser = Get(context, "Browser"); var targets = Get(browser, "Targets");
            var input = Get(context, "Input"); var shell = Get(context, "Shell");
            var state = Get(shell, "State");
            var bindings = (HotkeyBindings)Get(Get(shell, "hotkeys"), "Bindings");
            var oldSend = bindings.Get("announcement.send"); var oldQuery = bindings.Get("item-browser.query");
            var announcements = Get(browser, "Announcements"); bool wasEnabled = (bool)Get(announcements, "Enabled");
            HotkeyChord mouse; string reason;
            Require(HotkeyChord.TryParse("Mouse2", out mouse, out reason), "native Mouse2 binding parses");
            Action<string, HotkeyChord> bind = (id, chord) =>
            {
                long command; string error;
                Require(bindings.TrySet(id, chord, null, out command, out error), "legal isolated binding transaction " + id + ": " + error);
                NativeQuickItemChecks.Until(() => { bindings.Poll(); return !bindings.Busy; });
            };
            Action<bool, bool> sample = (right, escape) =>
            {
                Main.keyState = escape ? new KeyboardState(Keys.W, Keys.Escape) : new KeyboardState(Keys.W);
                PlayerInput.MouseInfo = new MouseState(940, 620, 0, ButtonState.Released, ButtonState.Released,
                    right ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released);
                Main.mouseLeft = false; Main.mouseRight = right;
                Main.LocalPlayer.mouseInterface = Main.mouseText = false;
                PlayerInput.Triggers.Reset(); PlayerInput.Triggers.Current.Up = true; PlayerInput.Triggers.Current.MouseRight = right; PlayerInput.Triggers.Update();
                Call(input, "BeginUpdate");
                Call(input, "AfterNativeMouse", new List<string>(right ? new[] { "Mouse2" } : Array.Empty<string>()));
                Call(input, "AfterMapping"); Call(input, "AfterKeyboardRefresh");
            };
            try
            {
                Call(announcements, "SetEnabled", true);
                bind("announcement.send", null); bind("item-browser.query", null);
                foreach (string action in new[] { "announcement.send", "item-browser.query" })
                {
                    bind(action, mouse);
                    foreach (bool escape in new[] { false, true })
                    {
                        Call(targets, "Cancel", (object)null); Call(shell, "CloseAndSubmitPosition");
                        sample(false, false); sample(false, false);
                        Call(targets, "BeginPick"); Require((bool)Get(targets, "Picking"), "actual pick begins before cancellation");
                        sample(false, false); Call(targets, "ProcessInput");
                        sample(!escape, escape); Call(targets, "ProcessInput");
                        Require(!(bool)Get(targets, "Busy") && (bool)Get(state, "Visible") && (int)Get(state, "Page") == 3,
                            "explicit cancellation restores the query page once");
                        Call(shell, "ProcessInput");
                        Require(!(bool)Get(targets, "Busy"), action + " cannot reuse the same " + (escape ? "Escape" : "Mouse2") + " cancellation in Shell dispatch");
                        sample(false, false); Call(targets, "ProcessInput"); Call(shell, "ProcessInput");
                        sample(false, false);
                        Require((bool)Get(input, "CanStartActions") && PlayerInput.Triggers.Current.Up,
                            "holding W across cancellation preserves movement after the actual cancellation-key release");
                        Require((int)Get(input, "ReadOnlyMouseMask") == 0, "real Mouse2 release retires only its owned tail");
                        sample(true, false); Call(targets, "ProcessInput"); Call(shell, "ProcessInput");
                        Require((bool)Get(targets, "Busy"), "a subsequent real Mouse2 press still starts " + action + " with F5 visible");
                        Call(targets, "Cancel", (object)null); sample(false, false); sample(false, false);
                    }
                    bind(action, null);
                }
                Console.WriteLine("PASS Targets cancellation: separate legal Mouse2 announcement/query, actual Targets then Shell dispatch, Escape and held W/release; no synthetic Draw proof.");
            }
            finally
            {
                Call(targets, "Cancel", (object)null); Call(shell, "CloseAndSubmitPosition");
                bind("announcement.send", null); bind("item-browser.query", null);
                bind("announcement.send", oldSend); bind("item-browser.query", oldQuery);
                Call(announcements, "SetEnabled", wasEnabled);
                Main.keyState = new KeyboardState(); Main.mouseRight = false;
            }
        }
    }
}
