using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBlueprintLibraryWait
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static HostObservation hostObservation;

        // Host-owned compound conditions must keep their normal Update/Poll/
        // Prepare path, including its receipt consumer. Observe actual accepted
        // List submissions and actual receipt takes; never pump/take for Host.
        internal static IDisposable ObserveHost(LibraryState library) { return new HostObservation(library); }
        internal static void HostCondition(LibraryState library, string phase, Func<bool> done)
        {
            Require(hostObservation != null && ReferenceEquals(hostObservation.Library, library), "host list condition has its explicit library observation scope");
            hostObservation.Wait(phase, done);
        }

        private sealed class HostObservation : IDisposable
        {
            internal readonly LibraryState Library;
            private readonly HostObservation previous;
            private readonly Harmony patch;
            private readonly int thread = Thread.CurrentThread.ManagedThreadId;
            private Flight flight;
            private bool waiting;
            private sealed class Flight
            {
                internal long Command, Bound, Assets, Processed;
                internal LibraryReceipt Receipt;
            }
            internal HostObservation(LibraryState library)
            {
                Library = library; previous = hostObservation;
                if (previous == null)
                {
                    patch = new Harmony("JueMingR.Tests.BlueprintHostListWait");
                    try
                    {
                        patch.Patch(typeof(LibraryState).GetMethod("TryList"), postfix: new HarmonyMethod(typeof(NativeBlueprintLibraryWait), nameof(HostListAccepted)));
                        patch.Patch(typeof(LibraryState).GetMethod("TryTakeReceipt"), postfix: new HarmonyMethod(typeof(NativeBlueprintLibraryWait), nameof(HostListTaken)));
                    }
                    catch { patch.UnpatchAll(patch.Id); throw; }
                }
                hostObservation = this;
            }
            internal void Accepted()
            {
                Require(Thread.CurrentThread.ManagedThreadId == thread, "host library commands remain on their actual game-thread owner");
                long assets = AssetDirectories(Library);
                flight = new Flight { Command = Library.AcceptedCommandId, Assets = assets, Bound = checked(2 * assets) };
            }
            internal void Taken(LibraryReceipt receipt)
            {
                if (flight == null || receipt.CommandId != flight.Command) return;
                Require(Thread.CurrentThread.ManagedThreadId == thread, "observed Host receipt stays on its game-thread owner");
                flight.Receipt = receipt; flight.Processed = Library.Snapshot.ProcessedItems;
                if (waiting) Completed(flight);
            }
            private static void Completed(Flight value)
            {
                Require(value.Processed >= 0 && value.Processed <= value.Bound, "completed host List stays within its independent two-pass input bound");
                Require(value.Receipt.Kind == LibraryCommandKind.List && value.Receipt.Status == LibraryStatus.Completed,
                    "host selected list retains its actual Completed receipt; failure/cancellation cannot extend a condition: " + value.Receipt.Status + " " + value.Receipt.Error);
            }
            internal void Wait(string phase, Func<bool> done)
            {
                Require(!waiting, "host list conditions do not recursively advance the Host");
                var clock = Stopwatch.StartNew(); long ordinaryDeadline = 8000, command = 0, progressAt = 0, processed = 0;
                bool completionObserved = false;
                waiting = true;
                try
                {
                    while (true)
                    {
                        bool ready = done(); long now = clock.ElapsedMilliseconds;
                        Flight current = flight;
                        if (current != null && current.Receipt == null)
                        {
                            Require(Library.AcceptedCommandId == current.Command, "one observed host List retains its accepted command");
                            long actual = Library.Snapshot.ProcessedItems;
                            if (command != current.Command) { command = current.Command; processed = 0; progressAt = now; completionObserved = false; }
                            Require(actual >= processed && actual <= current.Bound, "host List progresses monotonically within two passes of independent fixed directories");
                            if (actual != processed) { processed = actual; progressAt = now; }
                            if (now - progressAt > 8000 || now > 60000)
                                throw new Exception("G05 host List stalled or exceeded facility guard: phase=" + phase + " command=" + command + " processed=" + processed + "/" + current.Bound + " elapsedMs=" + now);
                        }
                        else
                        {
                            if (current != null && command == current.Command && !completionObserved)
                            {
                                Completed(current); completionObserved = true; ordinaryDeadline = now + 8000;
                                Console.WriteLine("PASS bounded host list phase=" + phase + " command=" + command + " assetDirectories=" + current.Assets + " processed=" + current.Processed + " bound=" + current.Bound + " conditionElapsedMs=" + now + " receiptOwner=Host");
                            }
                            // Only actual List work pauses the original
                            // ordinary condition protection. Materials, preview,
                            // capture and every other command still get eight
                            // seconds; several Lists cannot exceed one minute.
                            if (!ready && (now > ordinaryDeadline || now > 60000))
                                throw new Exception("G05 host library condition timeout: phase=" + phase + " elapsedMs=" + now);
                        }
                        if (ready) return;
                        Thread.Sleep(2);
                    }
                }
                finally { waiting = false; }
            }
            public void Dispose()
            {
                Require(ReferenceEquals(hostObservation, this), "host library observation scopes retire in owner order");
                hostObservation = previous;
                if (patch != null) patch.UnpatchAll(patch.Id);
            }
        }
        private static void HostListAccepted(LibraryState __instance, bool __result)
        { if (__result && hostObservation != null && ReferenceEquals(__instance, hostObservation.Library)) hostObservation.Accepted(); }
        private static void HostListTaken(LibraryState __instance, bool __result, LibraryReceipt __0)
        { if (__result && hostObservation != null && ReferenceEquals(__instance, hostObservation.Library)) hostObservation.Taken(__0); }

        internal static LibraryReceipt AcceptedList(LibraryState library, string phase)
        {
            long command = library.AcceptedCommandId;
            Require(command > 0 && library.Snapshot.Busy, "bounded list wait requires an already accepted command");
            long assets = AssetDirectories(library), bound = checked(2 * assets);
            var clock = Stopwatch.StartNew(); long lastProgressAt = 0, previous = 0; bool observed = false;
            LibraryReceipt result;
            while (true)
            {
                library.Pump();
                long processed = library.Snapshot.ProcessedItems;
                Require(library.AcceptedCommandId == command && processed >= previous && processed <= bound,
                    "one accepted list stays on its command and within two passes of the independent fixed input directories");
                if (library.TryTakeReceipt(out result))
                {
                    Require(result.CommandId == command && result.Kind == LibraryCommandKind.List && result.Status == LibraryStatus.Completed,
                        "selected list must return its real Completed receipt; failure/cancellation is not progress: " + result.Status + " " + result.Error);
                    Console.WriteLine("PASS bounded selected list phase=" + phase + " command=" + command + " assetDirectories=" + assets +
                        " processed=" + processed + " bound=" + bound + " elapsedMs=" + clock.ElapsedMilliseconds);
                    return result;
                }
                if (processed != previous) { previous = processed; lastProgressAt = clock.ElapsedMilliseconds; }
                if (!observed && clock.ElapsedMilliseconds >= 4000)
                {
                    observed = true;
                    Console.WriteLine("Selected list still processing phase=" + phase + " command=" + command + " assetDirectories=" + assets +
                        " processed=" + processed + " bound=" + bound + " elapsedMs=" + clock.ElapsedMilliseconds);
                }
                // List synchronously validates every asset's layout/index and
                // may repeat once to select another six-row page. Suite assets
                // remain intact: this is a finite completion proof, not a new
                // product response-time promise. Only actual processed entries
                // renew the original eight-second stagnation protection; no
                // Host Update/Poll, native use or unrelated activity does so.
                if (clock.ElapsedMilliseconds - lastProgressAt > 8000 || clock.ElapsedMilliseconds > 60000)
                    throw new Exception("G05 selected list stalled or exceeded facility guard: phase=" + phase + " command=" + command +
                        " processed=" + processed + "/" + bound + " elapsedMs=" + clock.ElapsedMilliseconds);
                Thread.Sleep(2);
            }
        }

        private static long AssetDirectories(LibraryState library)
        {
            object worker = typeof(LibraryState).GetField("worker", Flags).GetValue(library);
            object files = worker.GetType().GetField("files", Flags).GetValue(worker);
            // The two native fault adapters only delegate this actual file
            // root. Do not infer a work bound from a product page/summary or
            // accept an unknown adapter as an empty fixture.
            for (int depth = 0; !(files is FileBlueprintLibrary) && depth < 4; depth++)
            {
                Require(files != null && files.GetType().Namespace == "NativeWorldTextProbe", "list fixture owns a known native file adapter");
                var inner = files.GetType().GetField("inner", Flags);
                Require(inner != null, "native list fault adapter retains its actual inner files");
                files = inner.GetValue(files);
            }
            Require(files is FileBlueprintLibrary, "selected list bound comes from actual isolated file inputs");
            string root = Path.GetFullPath((string)typeof(FileBlueprintLibrary).GetField("directory", Flags).GetValue(files));
            string isolated = Path.GetFullPath(Terraria.Program.SavePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Require(root.StartsWith(isolated, StringComparison.OrdinalIgnoreCase), "list input directory remains within this native process's isolated SavePath");
            long count = 0;
            // The accepted slot excludes concurrent asset mutations. Enumerate
            // names once, without reading metadata or traversing child links;
            // this independent input bound matches EnumerateAssets' ID domain.
            foreach (string path in Directory.EnumerateDirectories(root))
            {
                string name = Path.GetFileName(path); Guid id;
                if (Guid.TryParseExact(name, "N", out id) && id != Guid.Empty && name == id.ToString("N")) count++;
            }
            return count;
        }
    }
}
