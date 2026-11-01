using System;
using System.IO;
using System.Linq;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework.Input;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Only the three owner feedback items. The existing million-cell exchange
    // and construction fixtures remain independent, already recorded evidence.
    internal static class NativeBlueprintExchangeFollowupChecks
    {
        internal static void Run(object context)
        {
            object shell = Get(context, "Shell"), input = Get(context, "Input"), host = Get(context, "Blueprint"), keys = Get(shell, "hotkeys");
            var registry = (HotkeyRegistry)Get(keys, "Registry"); var bindings = (HotkeyBindings)Get(keys, "Bindings");
            NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); bindings.Poll(); return bindings.Loaded && (bool)Get(host, "CanOpenPlacedList"); });
            const string placed = "blueprint.placed-list", library = "blueprint.library";
            Require(registry.Find(placed) != null && registry.Find(placed).CanConfigure && bindings.Get(placed) == null, "placed list registers with common owner and defaults unbound");
            HotkeyChord chord; string reason; long command;
            Require(HotkeyChord.TryParse("L", out chord, out reason), "isolated binding parses");
            HotkeyChord originalLibrary = bindings.Get(library);
            Action<string, HotkeyChord> save = (id, value) =>
            {
                NativeQuickItemChecks.Until(() => { bindings.Poll(); return !bindings.Busy; });
                Require(bindings.TrySet(id, value, null, out command, out reason), "common reliable binding accepts " + id + ": " + reason);
                NativeQuickItemChecks.Until(() => { bindings.Poll(); return !bindings.Busy; });
                Require(Equals(bindings.Get(id), value), "binding activates after successful reliable save");
            };
            try
            {
                save(library, chord);
                Require(!bindings.TrySet(placed, chord, null, out command, out reason) && bindings.Get(placed) == null, "existing common conflict leaves placed list unbound");
                save(library, null); save(placed, chord);
                string path = (string)Get(Get(Get(bindings, "worker"), "storage"), "path");
                Require(HotkeyDocument.Decode(File.ReadAllBytes(path)).Entries.Any(row => row.Key == placed && row.Value == "L"), "actual isolated common hotkeys.json persists placed-list action");
                Call(shell, "CloseAndSubmitPosition");
                NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
                NativeQuickItemChecks.Sample(input, new[] { Keys.L });
                bindings.Dispatch((HotkeyInput)Get(input, "Hotkeys"), HotkeyContext.SinglePlayer, true);
                Require((bool)Get(Get(shell, "BlueprintPlacedUi"), "Visible"), "shared binding dispatch reaches real Host/Shell placed-list opener");
                Call(shell, "CloseAndSubmitPosition"); NativeQuickItemChecks.Sample(input, new[] { Keys.L });
                bindings.Dispatch((HotkeyInput)Get(input, "Hotkeys"), HotkeyContext.SinglePlayer, true);
                Require(!(bool)Get(Get(shell, "State"), "Visible"), "held common action cannot reopen after explicit close");
                Console.WriteLine("PASS placed-list hotkey: default unbound, common conflict, real document persistence, common dispatch -> real Host/Shell and held suppression.");
            }
            finally
            {
                Call(shell, "CloseAndSubmitPosition"); save(placed, null); save(library, originalLibrary);
                NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
            }
            NativeBlueprintExchangeLifecycleChecks.Run(context);
            NativeBlueprintPlacedListChecks.Run(context);
        }
    }
}
