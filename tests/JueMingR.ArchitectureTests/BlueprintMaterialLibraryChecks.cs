using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;

namespace JueMingR.ArchitectureTests
{
    internal static partial class BlueprintLibraryChecks
    {
        internal static void MaterialLane(IList<string> failures)
        {
            string area = Path.Combine(Path.GetTempPath(), "JueMingR-blueprint-materials-" + Guid.NewGuid().ToString("N"));
            int before = failures.Count;
            try
            {
                var catalog = BlueprintMaterialsChecks.Catalog(); Guid id = Guid.NewGuid(); LibraryEntry entry;
                using (var state = new LibraryState(new FileBlueprintLibrary(area)))
                {
                    var cell = Cell(500000, 8000); cell.Layers |= CaptureLayer.Wall; cell.WallType = 7; cell.ObjectId = 1; cell.UnreliableObject = true;
                    var obj = new CaptureObject { Id = 1, X = cell.X, Y = cell.Y, Width = 1, Height = 1, Type = 1, Reliable = false, Reason = "fixture unconfirmed group" };
                    var handle = Draft(state, id, new[] { cell }, new[] { obj });
                    var a = new BlueprintMaterialKey(id, true, Guid.Empty, "", false, null, catalog.Revision);
                    var b = new BlueprintMaterialKey(id, true, Guid.Empty, "", false, new[] { 1 }, catalog.Revision);
                    Require(state.RequestMaterials(a, catalog) && state.RequestMaterials(b, catalog), "independent material requests admitted without using the ordinary command slot");
                    Require(state.TryPreview(8101, handle, 40, 30, false, new[] { 1 }), "preview stays admissible beside queued material work");
                    state.CancelRead(8100);
                    var preview = Await(state);
                    Require(preview.Kind == LibraryCommandKind.Preview && preview.Status == LibraryStatus.Completed && preview.Summary.ContentCount == 1,
                        "material keys do not cancel or replace paired filtered preview summary");
                    var materialA = AwaitMaterials(state, a); var materialB = AwaitMaterials(state, b);
                    Require(materialA.Status == BlueprintMaterialStatus.Unresolved && materialB.Status == BlueprintMaterialStatus.Exact &&
                        materialB.Rows.Single().ItemType == 1003 && materialB.Total == 1, "same fixed stage A/B exclusions preserve wall and distinguish unknown Tile");
                    Require(ReferenceEquals(materialA, state.Materials(a)) && state.RequestMaterials(a, catalog), "A to B to A reuses only its exact fixed-source key");
                    state.CancelMaterials();
                    Require(Command(state, state.TryCreate(handle, "材料旧格式", Token(id), false, new[] { 1 })).Status == LibraryStatus.Saved,
                        "cancelled material viewing cannot cancel an accepted or subsequent reliable Create");
                    entry = List(state).Entries.Single();
                    Require(entry.ContentCount == 1 && entry.Width == 1 && entry.Height == 1, "derived material values do not rewrite content summary");
                }
                string[] original = Directory.GetFiles(area, "*", SearchOption.AllDirectories);
                var hashes = original.ToDictionary(p => p, p => Digest(File.ReadAllBytes(p)), StringComparer.OrdinalIgnoreCase);
                var files = new CountedFiles(area);
                using (var reopened = new LibraryState(files))
                {
                    var key = new BlueprintMaterialKey(entry.AssetId, false, Guid.Parse(entry.ContentRevision), entry.ContentHash, false, null, catalog.Revision);
                    files.BeforeBlockRead = () => { throw new IOException("controlled transient materials read failure"); };
                    Require(reopened.RequestMaterials(key, catalog), "cold old v1 asset is an ordinary material cache miss");
                    var unavailable = AwaitMaterials(reopened, key); long failedReads = files.BlockReads;
                    Require(unavailable.Status == BlueprintMaterialStatus.Unresolved && !String.IsNullOrEmpty(unavailable.Error),
                        "temporary file failure is unresolved rather than exact zero or an original-asset rewrite");
                    for (int i = 0; i < 120; i++) Require(reopened.RequestMaterials(key, catalog) && ReferenceEquals(unavailable, reopened.Materials(key)),
                        "stable failed viewing reuses its result without repeated disk reads");
                    Require(files.BlockReads == failedReads, "failed material viewing does not poll the original files");
                    reopened.CancelMaterials(); files.BeforeBlockRead = null;
                    Require(reopened.RequestMaterials(key, catalog), "explicit reopen retries after the temporary failure is removed");
                    var result = AwaitMaterials(reopened, key);
                    Require(result.Status == BlueprintMaterialStatus.Exact && result.Total == 1 && result.Rows.Single().ItemType == 1003,
                        "old v1 cold read obtains correct derived materials without resampling the world");
                    var renamed = Command(reopened, reopened.TryRename(entry.AssetId, entry.MetadataIdentity, "仅改名"));
                    Require(renamed.Status == LibraryStatus.Saved && renamed.CommittedName == "仅改名",
                        "reliable metadata rename publishes its actual name independently of the current page and material cache");
                    Require(ReferenceEquals(result, reopened.Materials(key)), "renaming does not invalidate immutable material content identity");
                }
                foreach (var pair in hashes)
                {
                    if (Path.GetFileName(pair.Key) == "metadata.json" || Path.GetFileName(pair.Key) == "metadata.json.bak" || Path.GetFileName(pair.Key) == ".library.lock") continue;
                    Require(File.Exists(pair.Key) && Digest(File.ReadAllBytes(pair.Key)).SequenceEqual(pair.Value), "material query leaves original v1 content bytes intact");
                }
                Require(!Directory.GetFiles(area, "*", SearchOption.AllDirectories).Any(p => Path.GetFileName(p).IndexOf("material", StringComparison.OrdinalIgnoreCase) >= 0),
                    "material cache does not add required files or a second persistent data layout");
            }
            catch (Exception error) { failures.Add("Blueprint materials library: " + error + "; evidence=" + area); }
            if (failures.Count == before && Directory.Exists(area))
            {
                string resolved = Path.GetFullPath(area), parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!resolved.StartsWith(parent, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("JueMingR-blueprint-materials-", StringComparison.Ordinal)) throw new InvalidOperationException("Unsafe material test cleanup root.");
                Directory.Delete(resolved, true);
            }
        }
        private static BlueprintMaterialSnapshot AwaitMaterials(LibraryState state, BlueprintMaterialKey key)
        {
            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < 30000)
            {
                var value = state.Materials(key);
                if (value.Status != BlueprintMaterialStatus.Pending) return value;
                Thread.Sleep(1);
            }
            throw new TimeoutException("Material lane did not return the exact requested identity.");
        }
    }
}
