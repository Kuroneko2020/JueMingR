using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;

namespace JueMingR.ArchitectureTests
{
    internal static class BlueprintPreviewChecks
    {
        internal static void Check(IList<string> failures)
        {
            try { LiteralLayersAndHoles(); AllContentAndOrder(); ExtremeBounds(); PurposeBudgets(); NameContract(); }
            catch (Exception error) { failures.Add("blueprint preview: " + error.Message); }
        }
        private static void LiteralLayersAndHoles()
        {
            var builder = new BlueprintPreviewBuilder(3, 2, 3, 2);
            builder.Add(0, 0, BlueprintPreviewLayer.Wall);
            builder.Add(1, 0, BlueprintPreviewLayer.Wire);
            builder.Add(2, 0, BlueprintPreviewLayer.Actuator);
            builder.Add(0, 1, BlueprintPreviewLayer.Liquid);
            builder.Add(2, 1, BlueprintPreviewLayer.Tile | BlueprintPreviewLayer.Object);
            var result = builder.Complete();
            // Expected packed colors come directly from the accepted palette;
            // no production palette/encoder is used to build the oracle.
            Require(result.Width == 3 && result.Height == 2 && result.ProcessedCells == 5, "all five meaningful literal cells participate");
            Require(result.GetPixel(0, 0) == 0xDCBC8660u && result.GetPixel(1, 0) == 0xDC5C5EDAu && result.GetPixel(2, 0) == 0xDC54A4C4u, "wall/wire/actuator keep their literal colors");
            Require(result.GetPixel(0, 1) == 0xDCDA9A60u && result.GetPixel(1, 1) == 0 && result.GetPixel(2, 1) == 0xDC7EC488u, "liquid and object remain content while internal air remains transparent");
            var copy = result.CopyPixels(); copy[0] = 0;
            Require(result.GetPixel(0, 0) == 0xDCBC8660u, "consumer cannot mutate the fixed image");
            bool rejected = false; try { builder.Add(0, 0, BlueprintPreviewLayer.Tile); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "sealed builders cannot accept late world data");
        }
        private static void AllContentAndOrder()
        {
            var a = new BlueprintPreviewBuilder(10000, 1, 128, 58);
            var b = new BlueprintPreviewBuilder(10000, 1, 128, 58);
            for (int i = 0; i < 10000; i++) a.Add(i, 0, i == 9999 ? BlueprintPreviewLayer.Object : BlueprintPreviewLayer.Wall);
            for (int i = 9999; i >= 0; i--) b.Add(i, 0, i == 9999 ? BlueprintPreviewLayer.Object : BlueprintPreviewLayer.Wall);
            var first = a.Complete(); var second = b.Complete();
            Require(first.Width == 128 && first.Height == 1 && first.ProcessedCells == 10000, "finite output does not truncate after 768 or a prefix");
            Require(first.GetPixel(127, 0) == 0xDC7EC488u, "remote last content participates in its final pixel");
            for (int x = 0; x < 128; x++) Require(first.GetPixel(x, 0) == second.GetPixel(x, 0), "aggregation is independent of chunk enumeration order");
            var sparse = new BlueprintPreviewBuilder(10000, 1, 100, 1);
            sparse.Add(0, 0, BlueprintPreviewLayer.Tile); sparse.Add(9999, 0, BlueprintPreviewLayer.Wire);
            var image = sparse.Complete();
            Require(image.GetPixel(0, 0) != 0 && image.GetPixel(99, 0) != 0 && image.GetPixel(50, 0) == 0, "sparse remote components retain their relative gap");
        }
        private static void ExtremeBounds()
        {
            var builder = new BlueprintPreviewBuilder(int.MaxValue, int.MaxValue, 512, 128);
            builder.Add(int.MaxValue - 1, int.MaxValue - 1, BlueprintPreviewLayer.Tile);
            var image = builder.Complete();
            Require(image.Width == 128 && image.Height == 128 && image.CopyPixels().Length == 16384, "extreme source bounds allocate only finite pixels");
            Require(image.GetPixel(127, 127) == 0xDC9CBCB2u, "extreme endpoint maps without overflow");
            bool rejected = false; try { new BlueprintPreviewBuilder(1, 1, 513, 128); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Require(rejected, "untrusted output dimensions cannot request an unbounded allocation");
        }
        private static void NameContract()
        {
            string error;
            Require(LibraryName.TryValidate(new string('中', 48), out error), "48 Chinese elements are legal");
            Require(!LibraryName.TryValidate(new string('中', 49), out error), "49 elements are rejected instead of truncated");
            string pair = char.ConvertFromUtf32(0x1F338);
            Require(LibraryName.TryValidate(string.Concat(System.Linq.Enumerable.Repeat(pair, 48)), out error), "supplementary scalars are complete elements");
            Require(!LibraryName.TryValidate("\ud83c", out error) && !LibraryName.TryValidate(" 名称", out error) && !LibraryName.TryValidate("名称\n", out error), "invalid encoding and boundary whitespace/control names fail explicitly");
        }
        private static void PurposeBudgets()
        {
            var builder = new BlueprintPreviewBuilder(2048, 1536, 1024, 768, true);
            builder.Add(2047, 1535, BlueprintPreviewLayer.Object);
            var image = builder.Complete();
            Require(image.Width == 1024 && image.Height == 768 && (long)image.CopyPixels().Length * 4 == 3L * 1024 * 1024,
                "draft purpose admits the independently specified three MiB finite snapshot");
            Require(image.GetPixel(1023, 767) == 0xDC7EC488u, "draft extension retains remote meaningful content");
            foreach (var size in new[] { new[] { 1025, 768 }, new[] { 1024, 769 }, new[] { 0, 768 } })
            {
                bool rejected = false;
                try { new BlueprintPreviewBuilder(1, 1, size[0], size[1], true); } catch (ArgumentOutOfRangeException) { rejected = true; }
                Require(rejected, "draft purpose rejects dimensions outside its finite contract");
            }
            bool libraryRejected = false;
            try { new BlueprintPreviewBuilder(1, 1, 512, 129); } catch (ArgumentOutOfRangeException) { libraryRejected = true; }
            Require(libraryRejected, "ordinary library constructor keeps its original 512 by 128 purpose limit");
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
