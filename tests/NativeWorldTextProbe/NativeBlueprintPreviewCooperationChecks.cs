using System;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Text;
using System.Security.Cryptography;
using System.Linq;
using System.Threading;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static LibraryReceipt PreviewSetup(LibraryState library, bool admitted)
        {
            Require(admitted, "large preview setup accepted through the existing library owner");
            // A million-cell setup does real reliable block writes. This is a
            // facility bound for creating the fixture, not the fairness gate.
            var timer = Stopwatch.StartNew(); LibraryReceipt receipt;
            while (timer.ElapsedMilliseconds < 60000)
            {
                library.Pump();
                if (library.TryTakeReceipt(out receipt)) { Require(receipt.Status == LibraryStatus.Completed || receipt.Status == LibraryStatus.Saved, "preview setup: " + receipt.Error); return receipt; }
                Thread.Sleep(1);
            }
            throw new TimeoutException("large preview fixture setup");
        }
        private static LibraryEntry PreloadPreviewTemplate(LibraryState library, int width, int height)
        {
            // Setup only: an independently encoded immutable template avoids
            // invoking the unrelated capture Normalize pipeline for a million
            // cells. The real library List/Preview performs all normal checks.
            var id = Guid.NewGuid(); var revision = Guid.NewGuid();
            string rootDirectory = (string)Get(Get(Get(library, "worker"), "files"), "directory");
            string asset = Path.Combine(rootDirectory, id.ToString("N")), content = Path.Combine(asset, "content");
            Directory.CreateDirectory(Path.Combine(content, "chunks"));
            byte[] index = new byte[height * 32];
            using (var hash = SHA256.Create())
            {
                for (int y = 0; y < height; y++)
                {
                    byte[] block;
                    using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
                    {
                        writer.Write(0x43424D4A); writer.Write(1); writer.Write(width); writer.Write(0);
                        for (int x = 0; x < width; x++)
                        {
                            writer.Write(x); writer.Write(y); writer.Write(0); writer.Write((byte)1); writer.Write((ushort)1); writer.Write((ushort)0);
                            writer.Write((short)0); writer.Write((short)0); for (int i = 0; i < 7; i++) writer.Write((byte)0);
                        }
                        block = stream.ToArray();
                    }
                    File.WriteAllBytes(Path.Combine(content, "chunks", y.ToString("D20", CultureInfo.InvariantCulture) + ".bin"), block);
                    Array.Copy(hash.ComputeHash(block), 0, index, y * 32, 32);
                }
                byte[] root;
                using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(0x42524D4A); writer.Write(1); writer.Write(revision.ToByteArray()); writer.Write((long)height); writer.Write(hash.ComputeHash(index));
                    writer.Write(0); writer.Write(0); writer.Write(width); writer.Write(height); writer.Write((long)width * height); writer.Write(0L);
                    writer.Write(0UL); writer.Write(0UL); byte[] version = Encoding.UTF8.GetBytes(typeof(Main).Assembly.GetName().Version.ToString()); writer.Write(version.Length); writer.Write(version); writer.Write(0); root = stream.ToArray();
                }
                File.WriteAllBytes(Path.Combine(content, "index.bin"), index); File.WriteAllBytes(Path.Combine(content, "root.bin"), root);
                string digest = Convert.ToBase64String(hash.ComputeHash(root));
                string metadata = "{\"format\":\"JueMingR.BlueprintAsset\",\"version\":1,\"assetId\":\"" + id.ToString("N") + "\",\"name\":\"大模板预览准备并行样本\",\"contentRevision\":\"" + revision.ToString("N") + "\",\"contentHash\":\"" + digest + "\",\"width\":" + width + ",\"height\":" + height + ",\"contentCount\":" + ((long)width * height) + ",\"blockCount\":" + height + ",\"missingSummary\":\"\",\"createdTicks\":0,\"updatedTicks\":0,\"deleted\":false}";
                File.WriteAllText(Path.Combine(asset, "metadata.json"), metadata, new UTF8Encoding(false));
            }
            return PreviewSetup(library, library.TryList(87001, 0, id)).Page.Entries.Single(e => e.AssetId == id);
        }
        private static void ConstructionPreviewCooperation(object context, object host, object tools, object input)
        {
            var player = Reset(context, host, tools, input); var library = (LibraryState)Get(host, "Library");
            const int width = 2048, height = 512;
            var entry = PreloadPreviewTemplate(library, width, height);
            object preparation = Get(host, "buildMaterialPreparation");
            player.inventory[12].SetDefaults(ItemID.RedPaint); player.inventory[12].stack = 2;
            player.inventory[54].SetDefaults(ItemID.BluePaint); player.inventory[54].stack = 7;
            Item material = player.inventory[12], priority = player.inventory[54];
            var identity = new BlueprintProjectionIdentity(entry.AssetId, Guid.Parse(entry.ContentRevision), entry.ContentHash, (long)Get(Get(tools, "Runtime"), "Generation"), 71);
            Step(context, input); long before = (long)Get(preparation, "NativeClicks");
            Require(library.TryPreview(-87001, entry.AssetId, entry.ContentHash, 160, 100), "million-cell original preview accepted");
            LibraryReceipt receipt;
            try
            {
                Call(preparation, "TryPrepare", new object[] { player, 12, 54, identity, -1 }); constructionInputTail();
                for (int i = 0; i < 16 && !(bool)Get(preparation, "Ready"); i++) Step(context, input);
                Require((bool)Get(preparation, "Ready") && (long)Get(preparation, "NativeClicks") == before + 3 && ReferenceEquals(player.inventory[54], material) && Main.mouseItem.IsAir,
                    "actual native inventory preparation progresses while the same library serves a large original preview");
                // Poll normally consumes completed receipts into Host. Its
                // Preview receipt must still be absent here: Busy by itself
                // could merely mean an already completed unconsumed result.
                var worker = Get(library, "worker");
                Require(GetOptional(worker, "previewReading") != null && GetOptional(worker, "result") == null,
                    "three real inventory swaps finish before the preview body iterator or receipt finishes");
                Call(preparation, "RequestReturn");
                for (int i = 0; i < 20 && (bool)Get(preparation, "HasWork"); i++) Step(context, input);
                Require(!(bool)Get(preparation, "HasWork") && (long)Get(preparation, "NativeClicks") == before + 6 && ReferenceEquals(player.inventory[12], material) && ReferenceEquals(player.inventory[54], priority) && Main.mouseItem.IsAir,
                    "original inverse inventory cleanup retains exact material endpoint identities during preview work");
            }
            finally
            {
                library.CancelRead(-87001);
                NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy; });
            }
            Require(!library.TryTakeReceipt(out receipt), "Host consumed the exact cancelled ordinary result through its existing exit");
            Console.WriteLine("PASS large-template preview/native inventory cooperation: 1048576 real template cells/512 blocks, original three prepare swaps before preview completion, six total prepare/return swaps, exact cancellation; no OS/FPS claim.");
        }
    }
}
