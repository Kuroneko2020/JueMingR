using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBlueprintLibraryWait
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

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
