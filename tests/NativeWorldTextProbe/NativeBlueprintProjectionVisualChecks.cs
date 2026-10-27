using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBlueprintProjectionVisualChecks
    {
        // This exercises actual Host.Place -> existing files owner -> completed
        // window receipt -> BlueprintWorldLayer.Draw. It is not a direct builder
        // image, OS physical-input test, full Main run or owner acceptance.
        internal static void Run(object context, ProbeGraphics graphics, string output)
        {
            var host = Get(context, "Blueprint"); var library = (LibraryState)Get(host, "Library");
            var editor = (BlueprintCaptureState)Get(host, "State");
            var shell = Get(context, "Shell"); Call(shell, "CloseAndSubmitPosition");
            Call(host, "CancelCaptureOrDraft");
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy && !(bool)Get(host, "discardPending"); });
            editor.Exit();
            var sample = NativeBlueprintProjectionChecks.MixedWindow();
            var entry = Save(library, sample);
            NativeQuickItemChecks.Sample(Get(context, "Input"), Array.Empty<Microsoft.Xna.Framework.Input.Keys>());
            foreach (int id in sample.Cells.Where(c => (c.Layers & CaptureLayer.Tile) != 0).Select(c => (int)c.TileType).Distinct()) graphics.LoadTexture("Tile", "Images/Tiles_" + id, id);
            graphics.LoadTexture("Wall", "Images/Wall_7", 7); graphics.LoadTexture("Extra", "Images/Extra_181", 181);
            var crystal = TextureAssets.Extra[181].Value;
            Console.WriteLine("BLUEPRINT native Extra181 texture=" + crystal.Width + "x" + crystal.Height + "; 14x8 frames=" + crystal.Width / 14 + "x" + crystal.Height / 8);
            Vector2 camera = Main.screenPosition; int width = Main.screenWidth, height = Main.screenHeight;
            float gravity = Main.LocalPlayer.gravDir; Vector2 zoom = Main.GameViewMatrix.Zoom; float ui = Main.UIScale;
            string before = Snapshot();
            try
            {
                Require((bool)Call(host, "Place", entry), "actual Host place command accepts the reliably saved mixed asset");
                NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return GetOptional(host, "CurrentProjection") != null; });
                // This graphic specialist supplies a fixed business-level point
                // only; native pointer admission/physical edges are tested by
                // BlueprintOriginal and must not be inferred from this call.
                Call(host, "AcceptWorldPointer", true, false, false, 30, 30, Main.maxTilesX, Main.maxTilesY);
                var current = Get(host, "CurrentProjection");
                int ox = (int)Get(current, "OriginX"), oy = (int)Get(current, "OriginY");
                string drawBefore = Snapshot(ox, oy, sample.Summary.Width, sample.Summary.Height);
                Main.screenWidth = 960; Main.screenHeight = 640; Main.screenPosition = new Vector2(ox * 16 + 96 - 480, oy * 16 + 80 - 320);
                Main.GameViewMatrix.SetViewportOverride(new Microsoft.Xna.Framework.Graphics.Viewport(0, 0, 960, 640));
                Main.GameViewMatrix.Zoom = Vector2.One;
                Call(Get(context, "worldTiles"), "BeginTick");
                Action draw = () => Call(Get(host, "World"), "Draw");
                graphics.Pixels(draw, Main.GameViewMatrix.ZoomMatrix);
                var renderer = Get(host, "ProjectionRenderer");
                NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return (int)Get(renderer, "CommandCount") > 0; });
                long builds = (long)Get(renderer, "BuildCount");
                foreach (float scale in new[] { .3f, 1f, 10f })
                foreach (float grav in new[] { 1f, -1f })
                {
                    Main.GameViewMatrix.Zoom = new Vector2(scale); Main.LocalPlayer.gravDir = grav;
                    Main.UIScale = scale == 1 ? 1.5f : 1f;
                    Call(Get(context, "worldTiles"), "BeginTick");
                    var pixels = graphics.Pixels(draw, Main.GameViewMatrix.ZoomMatrix);
                    Require(pixels.Any(c => c.A > 0), "actual world-layer mixed projection reaches pixels at zoom/gravity " + scale + "/" + grav);
                    graphics.Image(Path.Combine(output, "blueprint-projection-world-" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + grav + ".png"), draw, Main.GameViewMatrix.ZoomMatrix);
                }
                // Origin/camera updates may request another bounded window, but
                // the retained immutable window is not reparsed merely to draw.
                Require((long)Get(renderer, "BuildCount") == builds, "world transform changes alone reuse completed local commands before a different window receipt");
                Require(Snapshot() == before && Snapshot(ox, oy, sample.Summary.Width, sample.Summary.Height) == drawBefore,
                    "actual Host/worker/world draw leaves sampled local tiles, materials and entity owners unchanged");
                Main.UIScale = 1; Main.LocalPlayer.gravDir = 1; Main.GameViewMatrix.Zoom = Vector2.One;
                Call(host,"AcceptWorldPointer",true,false,false,30,30,Main.maxTilesX,Main.maxTilesY);
                Call(host,"AcceptWorldPointer",true,true,false,30,30,Main.maxTilesX,Main.maxTilesY);
                NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return Get(host,"ProjectionMode").ToString()=="Fixed";});
                var original=Get(host,"CurrentProjection");
                var beforeMask=graphics.Pixels(draw,Main.GameViewMatrix.ZoomMatrix);
                var cover=Save(library,new BlueprintProjectionWindow(sample.Identity,1,new CaptureSummary{Width=1,Height=1},new BlueprintProjectionBounds(0,0,1,1),new[]{new CaptureCell{X=0,Y=0,Layers=CaptureLayer.Tile,TileType=1}},Array.Empty<CaptureObject>()));
                NativeQuickItemChecks.Sample(Get(context,"Input"),Array.Empty<Microsoft.Xna.Framework.Input.Keys>());
                Require((bool)Call(host,"Place",cover),"real masked furniture cover is accepted");
                NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return Get(host,"ProjectionMode").ToString()=="Following";});
                Call(host,"AcceptWorldPointer",true,false,false,ox+6,oy+2,Main.maxTilesX,Main.maxTilesY);Call(host,"AcceptWorldPointer",true,true,false,ox+6,oy+2,Main.maxTilesX,Main.maxTilesY);
                NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return Get(host,"ProjectionMode").ToString()=="Fixed";});
                graphics.Pixels(draw,Main.GameViewMatrix.ZoomMatrix);
                NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return (int)Get(Get(host,"ProjectionRenderer"),"CommandCount")>0;});
                var mask=(BlueprintProjectionMask)Get(original,"Mask");
                Require(sample.Cells.Where(c=>c.ObjectId==2).All(c=>(mask.Apply(c).Layers&CaptureLayer.Tile)==0),"actual Host commit erases whole door Tile across all members");
                var rendered=((System.Collections.IEnumerable)Get(Get(original,"Renderer"),"commands")).Cast<object>().ToArray();
                Require(!rendered.Any(c=>Get(c,"Texture").ToString()=="Tile" && (int)Get(c,"Type")==10),"actual masked renderer has no stale door member command");
                var afterMask=graphics.Pixels(draw,Main.GameViewMatrix.ZoomMatrix);
                Require(!beforeMask.SequenceEqual(afterMask) && afterMask.Any(c=>c.A>0),"masked multi-instance world consumer reaches changed GPU pixels");
                graphics.Image(Path.Combine(output,"blueprint-multi-masked-door.png"),draw,Main.GameViewMatrix.ZoomMatrix);
                Require(Snapshot()==before && Snapshot(ox,oy,sample.Summary.Width,sample.Summary.Height)==drawBefore,"masked commit/draw remains read-only for finite world/material/entity sample");
                Console.WriteLine("PASS Blueprint projection actual Host.Place/files/window/WorldLayer pixels and local read-only snapshot. Native reference responsibility moved to initialized BlueprintOriginal after visual-1 DrawSingleTile NullReference; this scope does not claim native reference or ready paint shaders, OS actions or multiplayer.");
            }
            finally
            {
                Call(host, "ClearProjection"); Main.screenPosition = camera; Main.screenWidth = width; Main.screenHeight = height;
                Main.LocalPlayer.gravDir = gravity; Main.GameViewMatrix.Zoom = zoom; Main.UIScale = ui;
            }
        }

        internal static LibraryEntry Save(LibraryState library, BlueprintProjectionWindow sample)
        {
            var token = new CaptureToken(Guid.NewGuid(), 1, 1, 1); var handle = new ContentHandle(token.OperationId);
            Require(library.TryBeginStage(token), "projection mixed private stage accepted"); Complete(library);
            var cells = sample.Cells.Select(c => { c.X += sample.Summary.Left; c.Y += sample.Summary.Top; return c; }).OrderBy(c => c.Y).ThenBy(c => c.X).ToArray();
            var objects = sample.Objects.Select(o => { o.X += sample.Summary.Left; o.Y += sample.Summary.Top; return o; }).ToArray();
            Require(library.TryAppendChunk(handle, new CaptureChunk(cells, cells.Length, objects, objects.Length)), "projection mixed raw content accepted by real worker"); Complete(library);
            var summary = sample.Summary; summary.ContentCount = cells.LongLength;
            Require(library.TrySealDraft(handle, summary), "projection mixed raw draft sealed"); Complete(library);
            Require(library.TryCreate(handle, "投影机制混合样本", token), "projection mixed asset accepted"); var saved = Complete(library);
            Require(saved.Status == LibraryStatus.Saved, "projection mixed asset reliably saved");
            Require(library.TryList(91001, 0, saved.AssetId), "projection mixed asset lists through real metadata");
            return NativeBlueprintLibraryWait.AcceptedList(library, "projection selected=" + saved.AssetId).Page.Entries.Single(e => e.AssetId == saved.AssetId);
        }
        private static LibraryReceipt Complete(LibraryState state)
        {
            LibraryReceipt result = null;
            NativeQuickItemChecks.Until(() => { state.Pump(); return state.TryTakeReceipt(out result); });
            Require(result.Status != LibraryStatus.Failed && result.Status != LibraryStatus.CommitUnconfirmed, "mixed fixture repository operation: " + result.Error);
            return result;
        }
        internal static string Snapshot(int left = 0, int top = 0, int width = 16, int height = 16)
        {
            var parts = new List<string>();
            // The isolated fixture is 120x120. Snapshot the finite scene area,
            // not the whole world, and include all materials/entity ownership.
            for (int y = Math.Max(0, top); y < Math.Min(top + height, Main.maxTilesY); y++)
                for (int x = Math.Max(0, left); x < Math.Min(left + width, Main.maxTilesX); x++)
            {
                var t = Main.tile[x, y];
                parts.Add(t == null ? "null" : t.type + ":" + t.wall + ":" + t.frameX + ":" + t.frameY + ":" + t.active() + ":" + t.inActive() + ":" + t.slope() + ":" + t.halfBrick() + ":" + t.liquid + ":" + t.wire() + ":" + t.wire2() + ":" + t.wire3() + ":" + t.wire4());
            }
            foreach (var item in Main.LocalPlayer.inventory) parts.Add(item == null ? "item-null" : item.type + ":" + item.stack + ":" + item.prefix);
            parts.Add("entities=" + TileEntity.ByID.Count + ":" + (Main.npc ?? Array.Empty<NPC>()).Count(n => n != null && n.active) + ":" + (Main.projectile ?? Array.Empty<Projectile>()).Count(p => p != null && p.active));
            return String.Join(";", parts);
        }

    }

    // Uses the already initialized original Main/asset/paint/tile renderers.
    // Test scene setup is finite and owned; production drawing never receives a
    // substituted world or a synthetic native renderer as its reference.
    internal static class NativeBlueprintProjectionOriginalChecks
    {
        internal static void Run(Main game, object context, Action draw, Func<RenderTarget2D> canvas, string output)
        {
            Require(ReferenceEquals(game, Main.instance) && game.TilesRenderer != null && game.TilePaintSystem != null,
                "native mixed reference requires original initialized Main/tile/paint owners");
            var host = Get(context, "Blueprint"); var library = (LibraryState)Get(host, "Library");
            ((BlueprintCaptureState)Get(host, "State")).Exit(); Call(Get(context, "Shell"), "CloseAndSubmitPosition");
            var raw = NativeBlueprintProjectionChecks.MixedWindow();
            var cells = raw.Cells.Select(c => { c.FullbrightTile = (c.Layers & CaptureLayer.Tile) != 0; c.FullbrightWall = (c.Layers & CaptureLayer.Wall) != 0; if (c.X == 11 && c.Y == 9) c.HalfBrick = true; return c; }).Concat(new[]
            {
                // Normal violet paint on colored wood, and an independent lime
                // wood wall, make shader consumption visible. The original
                // gray-stone paint and wall/liquid/circuit mixed cell remain.
                new CaptureCell { X = 0, Y = 7, Layers = CaptureLayer.Tile, TileType = 30, FrameX = 18, FrameY = 18, TilePaint = 11, FullbrightTile = true },
                new CaptureCell { X = 0, Y = 2, Layers = CaptureLayer.Wall, WallType = 4, WallPaint = 4, FullbrightWall = true }
            }).ToArray();
            var sample = new BlueprintProjectionWindow(raw.Identity, raw.WindowGeneration, raw.Summary, raw.Bounds, cells, raw.Objects.ToArray());
            const int sx = 380, sy = 208, dx = 405, dy = 208;
            var saved = new Tile[sample.Summary.Width + 4, sample.Summary.Height + 4];
            Vector2 playerPosition = Main.LocalPlayer.position; float gravity = Main.LocalPlayer.gravDir, zoom = Main.GameZoomTarget, ui = Main.UIScale;
            int pylonCounter = Main.tileFrameCounter[597];
            bool renderNow = Main.renderNow; int renderCount = Main.renderCount;
            try
            {
                Main.LocalPlayer.gravDir = 1; Main.GameZoomTarget = 1; Main.UIScale = 1;
                Main.LocalPlayer.position = new Vector2(400 * 16, 220 * 16);
                Refresh(draw); var empty = Pixels(canvas());
                for (int y = 0; y < saved.GetLength(1); y++) for (int x = 0; x < saved.GetLength(0); x++)
                { saved[x, y] = Main.tile[sx + x - 2, sy + y - 2]; Main.tile[sx + x - 2, sy + y - 2] = new Tile(); }
                foreach (var c in cells)
                {
                    var t = Main.tile[sx + c.X, sy + c.Y];
                    t.active((c.Layers & CaptureLayer.Tile) != 0); t.type = c.TileType; t.frameX = c.FrameX; t.frameY = c.FrameY;
                    t.halfBrick(c.HalfBrick); t.slope(c.Slope); t.inActive(c.Inactive); t.color(c.TilePaint); t.fullbrightBlock(c.FullbrightTile);
                    t.wall = c.WallType; t.wallColor(c.WallPaint); t.fullbrightWall(c.FullbrightWall);
                    t.liquid = c.LiquidAmount; t.liquidType(c.LiquidKind);
                    t.wire((c.Wires & 1) != 0); t.wire2((c.Wires & 2) != 0); t.wire3((c.Wires & 4) != 0); t.wire4((c.Wires & 8) != 0); t.actuator(c.Actuator);
                }
                foreach (var c in cells) if ((c.Layers & CaptureLayer.Wall) != 0) Framing.WallFrame(sx + c.X, sy + c.Y);
                // Native animation frame zero gives a stable comparison source;
                // its small native floating motion remains an allowed effect.
                Main.tileFrameCounter[597] = (64 - ((sx + 8 + sy + 2) % 64)) % 64;
                Refresh(draw); Refresh(draw);
                var tilePaint = game.TilePaintSystem.TryGetTileAndRequestIfNotReady(1, 0, 11);
                var wallPaint = game.TilePaintSystem.TryGetWallAndRequestIfNotReady(7, 4);
                var woodPaint = game.TilePaintSystem.TryGetTileAndRequestIfNotReady(30, 0, 11);
                var woodWallPaint = game.TilePaintSystem.TryGetWallAndRequestIfNotReady(4, 4);
                Require(tilePaint != null && wallPaint != null && woodPaint != null && woodWallPaint != null,
                    "original Main actually prepared native stone/wood violet and mixed/independent wall lime paint textures before comparison");
                var reference = Pixels(canvas()); Save(canvas(), output, "blueprint-projection-initialized-native-reference");
                string beforeSource = NativeBlueprintProjectionVisualChecks.Snapshot(sx - 2, sy - 2, sample.Summary.Width + 4, sample.Summary.Height + 4);
                string beforeTarget = NativeBlueprintProjectionVisualChecks.Snapshot(dx - 2, dy - 2, sample.Summary.Width + 4, sample.Summary.Height + 4);
                var entry = NativeBlueprintProjectionVisualChecks.Save(library, sample);
                Require((bool)Call(host, "Place", entry), "initialized mixed asset enters actual Host.Place");
                NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return GetOptional(host, "CurrentProjection") != null; });
                int mx = dx + (sample.Summary.Width - 1) / 2, my = dy + (sample.Summary.Height - 1) / 2;
                // Position-only business samples; the preceding initialized
                // placement specialist proves actual native pointer admission.
                Call(host, "AcceptWorldPointer", true, false, false, mx, my, Main.maxTilesX, Main.maxTilesY);
                Call(host, "AcceptWorldPointer", true, true, false, mx, my, Main.maxTilesX, Main.maxTilesY);
                NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return Get(host, "ProjectionMode").ToString() == "Fixed"; });
                Require(Get(host, "ProjectionMode").ToString() == "Fixed", "comparison projection is fixed without anchor/outline contaminating its pixels");
                Main.renderNow = false; Main.renderCount = 3; draw(); var renderer = Get(host, "ProjectionRenderer");
                NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return (int)Get(renderer, "CommandCount") > 0; });
                Refresh(draw); var actual = Pixels(canvas()); Save(canvas(), output, "blueprint-projection-initialized-native-and-r");
                Require(beforeSource == NativeBlueprintProjectionVisualChecks.Snapshot(sx - 2, sy - 2, sample.Summary.Width + 4, sample.Summary.Height + 4) &&
                    beforeTarget == NativeBlueprintProjectionVisualChecks.Snapshot(dx - 2, dy - 2, sample.Summary.Width + 4, sample.Summary.Height + 4),
                    "initialized actual Main plus R overlay preserves finite source/target tiles, materials and related entity owners");
                CompareShapes(empty, reference, actual, sx, sy, dx, dy, canvas().Width);
                int nativePlant = Changed(empty, reference, Screen(sx * 16 + 48, sy * 16 + 52, 16, 12), canvas().Width, 12);
                int rPlant = Changed(reference, actual, Screen(dx * 16 + 48, dy * 16 + 52, 16, 12), canvas().Width, 5);
                Require(nativePlant > 0 && rPlant > 0, "native and R 32px plant both produce visible pixels above their member cell");
                int nativeCrystal = Changed(empty, reference, Screen(sx * 16 + 137, sy * 16 + 34, 28, 26), canvas().Width, 12);
                int rCrystal = Changed(reference, actual, Screen(dx * 16 + 137, dy * 16 + 34, 28, 26), canvas().Width, 5);
                Require(nativeCrystal > 20 && rCrystal > 10, "native and R pylon both draw crystal body above pedestal fragments without creating a tile entity");
                PaintBlend("violet wood tile", reference, actual, Screen(sx * 16 + 4, (sy + 7) * 16 + 4, 8, 8),
                    Screen(dx * 16 + 4, (dy + 7) * 16 + 4, 8, 8), canvas().Width, true);
                PaintBlend("lime independent wood wall", reference, actual, Screen(sx * 16 + 4, (sy + 2) * 16 + 4, 8, 8),
                    Screen(dx * 16 + 4, (dy + 2) * 16 + 4, 8, 8), canvas().Width, false);
                Console.WriteLine("PASS Blueprint initialized native/R mixed reference: full Main assets/renderers, native/R final colored wood tile11 and independent wood wall4 blend pixels, actual Host/files/window/WorldLayer; native/R half/slopes masks, plant overhang and pylon body pixels; finite source/target/material/entity snapshots. Two PNGs retain walls/liquid/circuits/furniture for bounded visual comparison; static liquids/wall variants/pylon animation are representative, other paint IDs/OS/multiplayer unclaimed.");
            }
            finally
            {
                Call(host, "ClearProjection");
                for (int y = 0; y < saved.GetLength(1); y++) for (int x = 0; x < saved.GetLength(0); x++)
                    if (saved[x, y] != null) Main.tile[sx + x - 2, sy + y - 2] = saved[x, y];
                Main.LocalPlayer.position = playerPosition; Main.LocalPlayer.gravDir = gravity; Main.GameZoomTarget = zoom; Main.UIScale = ui;
                Main.tileFrameCounter[597] = pylonCounter; Main.renderNow = renderNow; Main.renderCount = renderCount;
            }
        }
        private static void CompareShapes(Color[] empty, Color[] reference, Color[] actual, int sx, int sy, int dx, int dy, int width)
        {
            int nativeTop = Changed(empty, reference, Screen((sx + 11) * 16, (sy + 9) * 16, 16, 8), width, 12);
            int nativeBottom = Changed(empty, reference, Screen((sx + 11) * 16, (sy + 9) * 16 + 8, 16, 8), width, 12);
            int rTop = Changed(reference, actual, Screen((dx + 11) * 16, (dy + 9) * 16, 16, 8), width, 5);
            int rBottom = Changed(reference, actual, Screen((dx + 11) * 16, (dy + 9) * 16 + 8, 16, 8), width, 5);
            Console.WriteLine("BLUEPRINT native/R half mask top/bottom=" + nativeTop + "/" + nativeBottom + " vs " + rTop + "/" + rBottom);
            Require(nativeTop <= 8 && rTop <= 8 && nativeBottom >= 32 && rBottom >= 32, "native and R half-brick occupy lower eight pixels with transparent upper half");
            for (int column = 2; column <= 5; column++)
            {
                // Saved 20/32px plants above this row overlap its first four
                // pixels. Compare the independent terrain mask below them;
                // their visible overhang has a separate image assertion.
                var n = Screen((sx + column) * 16, (sy + 5) * 16 + 4, 16, 12);
                var r = Screen((dx + column) * 16, (dy + 5) * 16 + 4, 16, 12);
                int different = 0, occupied = 0;
                for (int y = 0; y < 12; y++) for (int x = 0; x < 16; x++)
                {
                    bool expected = Difference(empty[(n.Y + y) * width + n.X + x], reference[(n.Y + y) * width + n.X + x]) > 12;
                    bool projected = Difference(reference[(r.Y + y) * width + r.X + x], actual[(r.Y + y) * width + r.X + x]) > 5;
                    if (expected) occupied++; if (expected != projected) different++;
                }
                Console.WriteLine("BLUEPRINT native/R slope=" + (column - 1) + " occupied=" + occupied + " mismatches=" + different + "/192");
                Require(occupied > 10 && different <= 12, "native original and R slope masks agree away from neighboring plant overlap for slope " + (column - 1));
            }
        }
        private static Rectangle Screen(int x, int y, int width, int height)
        {
            Vector2 point = Vector2.Transform(new Vector2(x, y) - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix);
            return new Rectangle((int)Math.Round(point.X), (int)Math.Round(point.Y), width, height);
        }
        private static void PaintBlend(string label, Color[] reference, Color[] actual, Rectangle source, Rectangle target, int width, bool violet)
        {
            double numerator = 0, denominator = 0; int colored = 0;
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
            {
                Color native = reference[(source.Y + y) * width + source.X + x], background = reference[(target.Y + y) * width + target.X + x];
                Color projected = actual[(target.Y + y) * width + target.X + x];
                if (violet ? native.R > native.G + 8 && native.B > native.G + 8 : native.G >= native.R && native.G > native.B + 8) colored++;
                double r = native.R - background.R, g = native.G - background.G, b = native.B - background.B;
                numerator += r * (projected.R - background.R) + g * (projected.G - background.G) + b * (projected.B - background.B);
                denominator += r * r + g * g + b * b;
            }
            Require(colored >= 16 && denominator > 10000, "original final " + label + " has a visible native paint color and differs from target background");
            // Independent oracle is the original final colored source pixels.
            // Fit one common transparency, rather than reading production
            // command colors or duplicating its paint/shader implementation.
            double alpha = numerator / denominator, maxResidual = 0;
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
            {
                Color native = reference[(source.Y + y) * width + source.X + x], background = reference[(target.Y + y) * width + target.X + x];
                Color projected = actual[(target.Y + y) * width + target.X + x];
                maxResidual = Math.Max(maxResidual, Math.Abs(projected.R - (native.R * alpha + background.R * (1 - alpha))));
                maxResidual = Math.Max(maxResidual, Math.Abs(projected.G - (native.G * alpha + background.G * (1 - alpha))));
                maxResidual = Math.Max(maxResidual, Math.Abs(projected.B - (native.B * alpha + background.B * (1 - alpha))));
            }
            Console.WriteLine("BLUEPRINT native/R final paint " + label + " colored=" + colored + "/64 fitted-alpha=" + alpha.ToString("F5", System.Globalization.CultureInfo.InvariantCulture) +
                " max-RGB-residual=" + maxResidual.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
            Require(alpha > .15 && alpha < .9 && maxResidual <= 3, "R final " + label + " preserves original native paint color/texture under one semitransparent blend");
        }
        private static void Refresh(Action draw)
        {
            // The specialist omits native simulation/update's render phase
            // progression. Exercise the original water/background/tile phases
            // in order. renderNow=true always selects the first water branch
            // in locked .8 RenderToTargets, so it cannot refresh changed tiles.
            for (int phase = 1; phase <= 3; phase++) { Main.renderNow = false; Main.renderCount = phase; draw(); }
        }
        private static int Changed(Color[] before, Color[] after, Rectangle area, int width, int threshold)
        {
            int count = 0; for (int y = area.Top; y < area.Bottom; y++) for (int x = area.Left; x < area.Right; x++)
                if (Difference(before[y * width + x], after[y * width + x]) > threshold) count++; return count;
        }
        private static int Difference(Color a, Color b) { return Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B); }
        private static Color[] Pixels(RenderTarget2D target) { var pixels = new Color[target.Width * target.Height]; target.GetData(pixels); return pixels; }
        private static void Save(RenderTarget2D target, string output, string name) { using (var stream = File.Create(Path.Combine(output, name + ".png"))) target.SaveAsPng(stream, target.Width, target.Height); }
    }
}
