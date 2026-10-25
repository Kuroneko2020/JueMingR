using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using Terraria.ObjectData;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBlueprintCaptureContentChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static void Run(object context)
        {
            var host = Get(context, "Blueprint"); var input = Get(context, "Input");
            var editor = (BlueprintCaptureState)Get(host, "State"); var flow = (BlueprintCaptureFlow)Get(host, "Flow");
            var library = (LibraryState)Get(host, "Library"); var shell = Get(context, "Shell");
            // The shared headless fixture omits normal Main initialization.
            // Execute the original tile-property initializer for these object
            // consumers; do not hand-author flags to force a resolver result.
            NativeBlueprintWorldMatchChecks.InitializeMetadata();
            var sourceType = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintCaptureSource", true);
            var source = (IBlueprintCaptureSource)Activator.CreateInstance(sourceType, F, null, new object[] { (Func<bool>)(() => true) }, null);
            Tile value = Main.tile[10, 10];
            value.active(true); value.type = 1; value.inActive(true); value.halfBrick(true); value.color(11); value.invisibleBlock(true); value.fullbrightBlock(true);
            value.wall = 7; value.wallColor(4); value.invisibleWall(true); value.fullbrightWall(true); value.wire(true); value.wire2(true); value.wire3(true); value.wire4(true); value.actuator(true); value.liquid = 201; value.liquidType(3);
            CaptureCell cell; Require(source.Read(10, 10, out cell) && cell.Layers == (CaptureLayer)31 && cell.TilePaint == 11 && cell.WallPaint == 4 && cell.Wires == 15 && cell.Inactive && cell.HalfBrick && cell.LiquidKind == 3 && cell.LiquidAmount == 201 && cell.InvisibleTile && cell.FullbrightTile && cell.InvisibleWall && cell.FullbrightWall, "real Tile semantic reader preserves every meaningful layer including inactive tile");
            value.active(false); Require(source.Read(10, 10, out cell) && (cell.Layers & CaptureLayer.Tile) == 0 && cell.TileType == 0 && (cell.Layers & CaptureLayer.Wall) != 0, "inactive=false tile presence differs from inActive; residual type/paint/frame never creates foreground"); value.active(true);
            int oldMode = Main.netMode; var oldSections = Main.sectionManager;
            try { Main.netMode = 1; Main.sectionManager = null; Require(!source.Read(10, 10, out cell), "allocated Tile with unreceived client section is unknown"); }
            finally { Main.netMode = oldMode; Main.sectionManager = oldSections; }

            Furniture(source, 30, 30, TileID.Tables, 3, 2, "ordinary 3x2 table");
            Furniture(source, 40, 30, TileID.ClosedDoor, 1, 3, "closed door row offsets");
            Furniture(source, 50, 30, TileID.Containers, 2, 2, "chest exterior and missing contents");
            Furniture(source, 60, 30, TileID.Signs, 2, 2, "sign exterior without text payload");
            Furniture(source, 70, 30, TileID.ItemFrame, 2, 2, "display exterior without held item payload");

            // Earlier consumers advance the true entry counter. Starting a
            // capture at the old literal 100 would make EndTick < StartTick
            // and correctly fail immutable-root validation.
            ulong tick = (ulong)Get(host,"latestTick") + 1;
            Console.WriteLine("CAPTURE monotonic fixture firstTick=" + tick + " hostPriorTick=" + Get(host,"latestTick") + " nativeEntry=" + Main.GameUpdateCount);
            Action pump = () => { Call(host, "Update", tick++); Call(host, "Poll"); };
            Action begin = () => { Call(shell, "CloseAndSubmitPosition"); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); editor.Enter(); editor.Selection.ToggleRectangle(8, 8, 12, 12, 120, 120); Require((bool)Call(host, "Save"), "shared save request accepts a valid committed selection"); };
            begin();
            Require(!(bool)Get(host, "CanEdit"), "capture confirmation UI revokes world editor input");
            NativeQuickItemChecks.Until(() => { pump(); return flow.Status == BlueprintCaptureStatus.DraftReady || flow.Status == BlueprintCaptureStatus.Failed; });
            Require(flow.Status == BlueprintCaptureStatus.DraftReady && flow.Summary.ContentCount == 1 && flow.Summary.Width == 1 && flow.Summary.Height == 1, "accepted capture progresses through CanEdit=false and trims real empty border; status=" + flow.Status + " message=" + flow.Message + " content=" + flow.Summary?.ContentCount + " bounds=" + flow.Summary?.Width + "x" + flow.Summary?.Height + " interval=" + flow.Summary?.StartTick + ".." + flow.Summary?.EndTick);
            value.type = 2; Call(host, "SetDraftName", "真实采集一"); Call(host, "ConfirmSave");
            Call(shell, "CancelForFocusLoss"); Call(shell, "ProcessInput");
            Require(flow.Status == BlueprintCaptureStatus.Saving, "real Shell focus-loss follow-up cannot revoke an accepted save");
            NativeQuickItemChecks.Until(() => { pump(); return flow.Status == BlueprintCaptureStatus.Saved || flow.Status == BlueprintCaptureStatus.CommitUnconfirmed; });
            Require(flow.Status == BlueprintCaptureStatus.Saved && editor.Active && !editor.Selection.HasSelection && flow.SelectedAssetId.HasValue, "only reliable save consumes its original selection and retains capture mode");

            begin(); NativeQuickItemChecks.Until(() => { pump(); return flow.Status == BlueprintCaptureStatus.DraftReady || flow.Status == BlueprintCaptureStatus.Failed; });
            Require(flow.Status == BlueprintCaptureStatus.DraftReady, "second fixed capture completes");
            Call(host, "ConfirmSave"); editor.Selection.ToggleRectangle(15, 15, 15, 15, 120, 120);
            long newerRevision = editor.Selection.Revision;
            NativeQuickItemChecks.Until(() => { pump(); return flow.Status == BlueprintCaptureStatus.Saved || flow.Status == BlueprintCaptureStatus.CommitUnconfirmed; });
            Require(flow.Status == BlueprintCaptureStatus.Saved && editor.Selection.HasSelection && editor.Selection.Revision == newerRevision, "late saved receipt never clears a changed selection revision");
            begin(); NativeQuickItemChecks.Until(() => { pump(); return flow.Status == BlueprintCaptureStatus.DraftReady || flow.Status == BlueprintCaptureStatus.Failed; });
            Require(flow.Status == BlueprintCaptureStatus.DraftReady, "third fixed capture completes");
            Call(host, "ConfirmSave"); editor.Exit(); editor.Enter(); editor.Selection.ToggleRectangle(22, 22, 22, 22, 120, 120);
            long freshMode = editor.ModeGeneration, freshRevision = editor.Selection.Revision;
            NativeQuickItemChecks.Until(() => { pump(); return flow.Status == BlueprintCaptureStatus.Saved || flow.Status == BlueprintCaptureStatus.CommitUnconfirmed; });
            Require(flow.Status == BlueprintCaptureStatus.Saved && editor.Active && editor.Selection.HasSelection && editor.ModeGeneration == freshMode && editor.Selection.Revision == freshRevision, "late reliable save after exit/reenter preserves the new mode and selection");
            Call(shell, "CloseAndSubmitPosition"); editor.Exit();

            var ui = Get(shell, "BlueprintUi"); Call(ui, "OpenLibrary");
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
            PlayerInput.Triggers.Current.MouseLeft = PlayerInput.Triggers.Current.MouseRight = true;
            PlayerInput.Triggers.Current.CopyInto(Main.LocalPlayer);
            Require(!editor.Active && (bool)Get(ui, "WorldInteractionBlocked") && !Main.LocalPlayer.controlUseItem && !Main.LocalPlayer.controlUseTile, "library opened outside capture blocks actual world input consumers");
            Call(shell, "CloseAndSubmitPosition");
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
            PlayerInput.Triggers.Current.MouseLeft = true; PlayerInput.Triggers.Current.CopyInto(Main.LocalPlayer);
            Require(Main.LocalPlayer.controlUseItem, "closed library restores ordinary world action after trusted neutral");
            Require(!library.Snapshot.Busy, "capture leaves no accidental duplicate save queue");
            NativeBlueprintRepairContentChecks.Run(sourceType);
            NativeBlueprintCaptureBoundaryChecks.Run(context, source);
            NativeBlueprintRepairRecoveryChecks.Run(context);
            Console.WriteLine("PASS Blueprint real Tile fields, received-section rejection, metadata furniture families, capture UI progress, reliable save and stale revision/mode receipt, library world-input gate, native directional stage/Saved and real 25600-cell Host scale. Isolated fixture only; OS/IME/gameplay/multiplayer NOT_EXECUTED.");
        }
        private static void Furniture(IBlueprintCaptureSource source, int x, int y, ushort type, int width, int height, string name)
        {
            TileObjectData data = TileObjectData.GetTileData(type, 0);
            Require(data != null && data.Width == width && data.Height == height, "original metadata matches independent representative: " + name);
            int offset = 0;
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                { Tile member = Main.tile[x + col, y + row]; member.active(true); member.type = type; member.frameX = (short)(col * (data.CoordinateWidth + data.CoordinatePadding)); member.frameY = (short)offset; }
                offset += data.CoordinateHeights[row] + data.CoordinatePadding;
            }
            var selection = new BlueprintSelection(); selection.ToggleRectangle(x + width - 1, y + height - 1, x + width - 1, y + height - 1, 120, 120);
            CaptureCell literal; source.Read(x + width - 1, y + height - 1, out literal); var described = source.Describe(literal);
            Console.WriteLine("CAPTURE OBJECT " + name + " important=" + Main.tileFrameImportant[type] + " described=" + (described == null ? "null" : described.Object.Reliable + "/" + described.Object.Reason + "/" + described.Object.X + "," + described.Object.Y) + " metadata=" + data.Width + "x" + data.Height + " full=" + data.CoordinateFullWidth + "x" + data.CoordinateFullHeight);
            var job = new CaptureJob(source, selection.Freeze(), 0); var cells = new List<CaptureCell>(); var objects = new List<CaptureObject>();
            for (int i = 0; !job.Complete && job.Error == null && i < 1000; i++)
            { job.Advance(2, 20); if (job.PendingChunk != null) { cells.AddRange(job.PendingChunk.Cells.Take(job.PendingChunk.Count)); objects.AddRange(job.PendingChunk.Objects.Take(job.PendingChunk.ObjectCount)); job.AcceptChunk(); } }
            Require(job.Complete && cells.Count == width * height && objects.Count == 1 && objects[0].Reliable, "real metadata partial capture completes one reliable object: " + name);
            Require(string.IsNullOrEmpty(objects[0].MissingPayload) && job.Summary(5).MissingPayloads.Length == 0, "normal building exterior generates no out-of-scope payload warning: " + name);
        }
    }
}
