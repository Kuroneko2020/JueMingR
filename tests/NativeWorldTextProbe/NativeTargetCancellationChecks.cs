using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Actual public input stages and Shell dispatch. Cold transitions receive
    // controlled observer callbacks; no rendered Draw pass or OS input runs.
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
            bool layersReady=(bool)Get(shell,"LayersReady");
            bool stateReady = (bool)Get(state, "Ready");
            HotkeyChord mouse; string reason;
            Require(HotkeyChord.TryParse("Mouse2", out mouse, out reason), "native Mouse2 binding parses");
            Action<string, HotkeyChord> bind = (id, chord) =>
            {
                long command; string error;
                Require(bindings.TrySet(id, chord, null, out command, out error), "legal isolated binding transaction " + id + ": " + error);
                NativeQuickItemChecks.Until(() => { bindings.Poll(); return !bindings.Busy; });
            };
            var replay = new InputReplay(input);
            Action<bool, bool> sample = (right, escape) => replay.Sample(right, escape ? new[] { Keys.W, Keys.Escape } : new[] { Keys.W });
            try
            {
                // This CPU composition never invokes the native layer builder;
                // establish the same isolated readiness as NativeQuickItemChecks.
                Set(shell,"LayersReady",true);
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
                CheckF5Handoff(context, replay, bind);
                Console.WriteLine("PASS Targets cancellation: separate legal Mouse2 announcement/query, actual Targets then Shell, F5/W handoff and real release; cold transitions use controlled CPU observer callbacks, not rendered Draw proof.");
            }
            finally
            {
                Call(targets, "Cancel", (object)null); Call(shell, "CloseAndSubmitPosition");
                bind("announcement.send", null); bind("item-browser.query", null);
                bind("announcement.send", oldSend); bind("item-browser.query", oldQuery);
                Call(announcements, "SetEnabled", wasEnabled);
                Set(shell,"LayersReady",layersReady);
                Set(state, "Ready", stateReady);
                Main.keyState = new KeyboardState(); Main.mouseRight = false;
            }
        }

        // .8 maps the previous Main.keyState cache before its independent final
        // Keyboard.GetState refresh. Never replenish that cache before mapping:
        // BeginUpdate and suppression are allowed to consume it. Only the final
        // refresh receives the immutable physical sample, including held keys.
        private sealed class InputReplay
        {
            private readonly object input, hotkeys;
            internal InputReplay(object input) { this.input = input; hotkeys = Get(input, "Hotkeys"); }
            internal void Sample(bool right, params Keys[] keys)
            {
                var physical = new KeyboardState(keys);
                Call(input, "BeginUpdate");
                PlayerInput.MouseInfo = new MouseState(20, 20, 0, ButtonState.Released, ButtonState.Released,
                    right ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released);
                Main.mouseX = Main.mouseY = 20; Main.mouseLeft = false; Main.mouseRight = right;
                Main.LocalPlayer.mouseInterface = Main.mouseText = false;
                var mouseKeys = new List<string>(right ? new[] { "Mouse2" } : Array.Empty<string>());
                Call(input, "AfterNativeMouse", mouseKeys);
                // Equivalent declared Up/W mapping, with the actual native
                // cache source and owned mouse-token filtering. No device runs.
                bool cachedW = Array.IndexOf(Main.keyState.GetPressedKeys(), Keys.W) >= 0;
                PlayerInput.Triggers.Reset(); PlayerInput.Triggers.Current.Up = cachedW;
                PlayerInput.Triggers.Current.MouseRight = mouseKeys.Contains("Mouse2"); PlayerInput.Triggers.Update();
                Call(input, "AfterMapping");
                Main.keyState = physical;
                Call(input, "AfterKeyboardRefresh");
                var frozen = (KeyboardState)Get(input, "KeyboardSample");
                foreach (Keys key in new[] { Keys.W, Keys.F5, Keys.Escape })
                    Require(frozen.IsKeyDown(key) == physical.IsKeyDown(key) && (bool)Call(hotkeys, "IsDown", (int)key) == physical.IsKeyDown(key),
                        "trusted final sample preserves physical " + key + " independently of consumed mapping cache");
            }
        }

        private static void CheckF5Handoff(object context, InputReplay replay, Action<string, HotkeyChord> bind)
        {
            var browser = Get(context, "Browser"); var targets = Get(browser, "Targets");
            var input = Get(context, "Input"); var shell = Get(context, "Shell"); var state = Get(shell, "State");
            var native = Get(Get(browser, "Knowledge"), "Native"); var failures = new List<string>();
            int picked = 0, announced = 0;
            var originalPicked = (Delegate)Get(targets, "Picked"); var originalAnnounced = (Delegate)Get(targets, "Announced");
            Set(targets, "Picked", Count(originalPicked, () => picked++)); Set(targets, "Announced", Count(originalAnnounced, () => announced++));
            HotkeyChord query, send; string reason;
            Require(HotkeyChord.TryParse("K", out query, out reason), "isolated query chord parses");
            Require(HotkeyChord.TryParse("L", out send, out reason), "isolated announcement chord parses");
            bind("item-browser.query", query); bind("announcement.send", send);
            Action<bool, string> expect = (ok, message) => { if (!ok) { failures.Add(message); Console.WriteLine("FAIL F5 handoff: " + message); } };
            Action<bool, Keys[]> frame = (right, keys) =>
            {
                replay.Sample(right, keys); Call(targets, "ProcessInput"); Call(shell, "ProcessInput");
                Require(!(bool)Get(shell, "Failed"), "actual Shell remains healthy in cancellation handoff");
            };
            var savedTile = Main.tile[1, 1]; var savedPosition = Main.LocalPlayer.position;
            bool savedFrameImportant = Main.tileFrameImportant[21];
            try
            {
                Set(state, "Ready", true);
                foreach (string scenario in new[] { "pick-held-Escape", "query-same-Escape", "pick-same-Mouse2", "cold-query-same-Escape", "cold-announcement-held-Escape" })
                {
                    Call(targets, "Cancel", (object)null); Call(shell, "CloseAndSubmitPosition");
                    frame(false, Array.Empty<Keys>()); frame(false, Array.Empty<Keys>());
                    bool declaration = scenario.StartsWith("cold-announcement", StringComparison.Ordinal);
                    bool cold = scenario.StartsWith("cold-", StringComparison.Ordinal);
                    if (declaration) { Call(state, "Navigate", 2); Call(state, "RestoreVisible"); }
                    if (cold)
                    {
                        Call(native, "Reset");
                        var tile = new Tile { type = 21, frameX = 36 }; tile.active(true); tile.fullbrightBlock(true); Main.tile[1, 1] = tile;
                        Main.tileFrameImportant[21] = true; Main.LocalPlayer.position = new Microsoft.Xna.Framework.Vector2(800, 800);
                    }
                    if (scenario.StartsWith("pick-", StringComparison.Ordinal))
                    {
                        Call(targets, "BeginPick"); Require((bool)Get(targets, "Picking"), "real point selection begins: " + scenario);
                        frame(false, new[] { Keys.W }); // real mouse release arms point selection
                    }
                    else
                    {
                        frame(false, new[] { Keys.W, declaration ? Keys.L : Keys.K });
                        Require((int)Get(targets, "pending") == (declaration ? 2 : 1), "actual Shell binding creates request: " + scenario);
                    }
                    if (cold)
                    {
                        // Controlled CPU Draw-boundary seam, not a rendered pass.
                        // The real request/Update must create preparingPlacement;
                        // the fixture never assigns pending/completed/preparing.
                        Main.LocalPlayer.mouseInterface = Main.mouseText = false;
                        Call(targets, "BeginPass"); Call(targets, "BeforeWorldHover"); Call(targets, "AfterResources", true); Call(targets, "EndPass", true);
                        long before = (long)Get(native, "PlacementReads");
                        Call(targets, "Update", (long)Get(targets, "tick") + 1);
                        Require((bool)Get(targets, "preparingPlacement") && (long)Get(native, "PlacementReads") - before <= 64,
                            "real cold transition uses the shared bounded placement allowance: " + scenario);
                    }
                    bool heldFirst = scenario.Contains("held");
                    if (heldFirst) frame(false, new[] { Keys.W, Keys.F5 });
                    int beforePicked = picked, beforeAnnounced = announced;
                    bool rightCancel = scenario.EndsWith("Mouse2", StringComparison.Ordinal);
                    frame(rightCancel, rightCancel ? new[] { Keys.W, Keys.F5 } : new[] { Keys.W, Keys.F5, Keys.Escape });
                    bool expectedVisible = true; int expectedPage = declaration ? 2 : 3;
                    expect(!(bool)Get(targets, "Busy") && (int)Get(targets, "pending") == 0, scenario + " retires the request after complete Targets/Shell dispatch");
                    expect((bool)Get(state, "Visible") == expectedVisible && (int)Get(state, "Page") == expectedPage, scenario + " preserves the correct page after Shell");
                    frame(rightCancel, rightCancel ? new[] { Keys.W, Keys.F5 } : new[] { Keys.W, Keys.F5, Keys.Escape });
                    expect((bool)Get(state, "Visible") && (int)Get(state, "Page") == expectedPage, scenario + " held cancellation/F5 cannot replay");
                    for (int i = 0; i < 3; i++) frame(false, new[] { Keys.W, Keys.F5 });
                    expect(PlayerInput.Triggers.Current.Up && (bool)Get(input, "CanStartActions"), scenario + " native cached W mapping resumes while F5 is still physically held");
                    expect((bool)Get(state, "Visible") && (int)Get(state, "Page") == expectedPage, scenario + " held F5 after cancel-key release cannot toggle");
                    Require((int)Get(input, "ReadOnlyMouseMask") == 0, "real mouse release retires read-only tail: " + scenario);
                    long reads = (long)Get(native, "PlacementReads");
                    for (int i = 0; i < 32; i++) { frame(false, new[] { Keys.W, Keys.F5 }); Call(targets, "Update", (long)Get(targets, "tick") + 1); }
                    expect(picked == beforePicked && announced == beforeAnnounced && !(bool)Get(targets, "Busy") && (long)Get(native, "PlacementReads") == reads,
                        scenario + " has no delayed query/announcement or continuing placement reads across 32 held mappings");
                    frame(false, new[] { Keys.W });
                    bool beforeToggle = (bool)Get(state, "Visible");
                    frame(false, new[] { Keys.W, Keys.F5 });
                    expect((bool)Get(state, "Visible") != beforeToggle, scenario + " trusted release and fresh F5 toggle once");
                    bool toggled = (bool)Get(state, "Visible"); frame(false, new[] { Keys.W, Keys.F5 });
                    expect((bool)Get(state, "Visible") == toggled, scenario + " ordinary hold toggles only once");
                    Console.WriteLine("CHECK F5 handoff " + scenario + ": complete dispatch, held tail, native Up/W, release/repress, 32 stable mappings; queryOutputs=" + (picked - beforePicked) + "; announcementOutputs=" + (announced - beforeAnnounced));
                }
                Require(failures.Count == 0, "F5 handoff failures: " + string.Join("; ", failures));
            }
            finally
            {
                Call(targets, "Cancel", (object)null); Set(targets, "Picked", originalPicked); Set(targets, "Announced", originalAnnounced);
                Main.tile[1, 1] = savedTile; Main.LocalPlayer.position = savedPosition;
                Main.tileFrameImportant[21] = savedFrameImportant;
                bind("item-browser.query", null); bind("announcement.send", null);
            }
        }

        // The existing PageCompositionCpu profile contains these real adapters;
        // BrowserCpu's historical ItemBrowser profile deliberately does not.
        internal static void RunF5Consumers(object context)
        {
            var shell = Get(context, "Shell"); var state = Get(shell, "State"); var targets = Get(Get(context, "Browser"), "Targets");
            var replay = new InputReplay(Get(context, "Input"));
            Action<Keys[]> frame = keys => { replay.Sample(false, keys); Call(targets, "ProcessInput"); Call(shell, "ProcessInput"); Require(!(bool)Get(shell, "Failed"), "F5 consumer Shell stays healthy"); };
            Action neutral = () => { frame(Array.Empty<Keys>()); frame(Array.Empty<Keys>()); };
            var f5 = new[] { Keys.F5 };

            // Drive the real native menu state and the actual Journey adapter;
            // no power enable, world save, graphics device or OS input is used.
            bool inventory = Main.playerInventory, menu = Main.CreativeMenu.Enabled, menuSearch = Main.CreativeMenu.GamepadMoveToSearchButtonHack;
            try
            {
                Call(shell, "CloseAndSubmitPosition"); neutral(); Main.playerInventory = true;
                Main.CreativeMenu.ResumeMenuFromGamepadSearch();
                Require((bool)Get(Get(shell, "journey"), "IsMenuOpen"), "native Journey window satisfies the actual F5 handoff gate");
                frame(f5);
                Require(!Main.CreativeMenu.Enabled && (bool)Get(state, "Visible"), "fresh F5 closes the native Journey menu and opens Shell once");
                frame(f5); Require((bool)Get(state, "Visible"), "Journey handoff's held F5 does not toggle Shell again");
            }
            finally { Main.CreativeMenu.CloseMenu(); if (menu) Main.CreativeMenu.ResumeMenuFromGamepadSearch(); Main.CreativeMenu.GamepadMoveToSearchButtonHack = menuSearch; Main.playerInventory = inventory; }

            Call(shell, "CloseAndSubmitPosition"); neutral();
            var blueprint = Get(context, "Blueprint"); var materials = Get(shell, "BlueprintMaterialsUi");
            Call(blueprint, "OpenMaterials"); Require((bool)Get(materials, "Visible"), "production world-material entry opens its actual modal");
            neutral(); frame(f5);
            Require(!(bool)Get(materials, "Visible") && !(bool)Get(state, "Visible"), "materials consumes the new F5 action without opening the hidden Shell");
            frame(f5); Require(!(bool)Get(state, "Visible"), "held F5 after materials close cannot open Shell");
            neutral(); frame(f5); Require((bool)Get(state, "Visible"), "new F5 after materials' trusted release opens Shell normally");

            neutral(); Call(state, "Navigate", 9);
            var adjustment = Get(Get(context, "Information"), "Adjustment");
            Call(shell, "RequestInformationAdjustment");
            Require((bool)Get(adjustment, "Active") && !(bool)Get(state, "Visible"), "production adjustment entry owns the hidden Shell");
            neutral(); frame(f5);
            Require(!(bool)Get(adjustment, "Active") && (bool)Get(state, "Visible"), "information adjustment consumes F5 and restores its original page once");
            frame(f5); Require((bool)Get(state, "Visible"), "held F5 after adjustment cancellation cannot close the restored page");

            neutral(); Call(state, "Navigate", 0);
            var items = Get(shell, "items");
            Require((bool)Call(Get(items, "selection"), "Open", JueMingR.Features.Items.ItemListKind.Sell, 0), "actual Items selection owner begins a legal draft");
            frame(new[] { Keys.Escape, Keys.F5 });
            Require(!(bool)Get(items, "Selecting") && (bool)Get(state, "Visible") && (int)Get(state, "Page") == 0,
                "upper selector cancellation retires the entry-cached F5 edge before Interaction");
            frame(f5); Require((bool)Get(state, "Visible"), "selector cancellation cannot replay held F5");
            neutral(); frame(f5); Require(!(bool)Get(state, "Visible"), "selector's later fresh F5 works normally");
            neutral();
            Console.WriteLine("PASS F5 consumers: real Journey/native-menu handoff, world Materials modal, InformationAdjustment and upper Items cancellation; pulse/hold/release through actual Shell/Interaction. No game loop/GPU/OS input.");
        }

        private static Delegate Count(Delegate original, Action count)
        {
            var value = Expression.Parameter(original.GetType().GetMethod("Invoke").GetParameters()[0].ParameterType);
            return Expression.Lambda(original.GetType(), Expression.Block(Expression.Invoke(Expression.Constant(count)), Expression.Invoke(Expression.Constant(original), value)), value).Compile();
        }
    }
}
