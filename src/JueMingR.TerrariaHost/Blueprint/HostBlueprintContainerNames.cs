using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.UI;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        private sealed class ContainerNameTarget
        {
            internal BlueprintProjectionPlacement Owner;
            internal BlueprintProjectionMask Mask;
            internal CaptureCell Cell;
            internal int X, Y, OriginX, OriginY, Index = -1;
            internal Chest Chest;
            internal object Connection, Tiles;
            internal bool Requested, Opened, Submitted, Suspended, InventoryWasOpen;
            internal ContainerOpenReceipt PendingOpen;
            internal ulong Started;
        }
        private readonly LinkedList<ContainerNameTarget> containerNames = new LinkedList<ContainerNameTarget>();
        private readonly Dictionary<string, LinkedListNode<ContainerNameTarget>> containerNameKeys = new Dictionary<string, LinkedListNode<ContainerNameTarget>>();
        private ContainerNameTarget namingContainer;
        // Native31 has no request id and a rejected request has no reply. Keep
        // the coordinate obligation after cancellation; never lend its33 to a
        // new transaction. It retains no projection/window/asset, and another
        // coordinate remains serviceable when a server silently rejects31.
        private sealed class ContainerOpenReceipt
        {
            internal int X, Y, Index;
            internal Chest Chest;
            internal bool Cancelled;
            internal object Connection, Tiles;
            internal long ManualGeneration;
        }
        private readonly Dictionary<long, ContainerOpenReceipt> containerOpenDebts = new Dictionary<long, ContainerOpenReceipt>();
        private long containerManualGeneration;
        private object containerDebtConnection, containerDebtTiles;
        private bool sendingContainerOpen;
        private static readonly MethodInfo nativeOpenChest = typeof(Player).GetMethod("OpenChest", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(int), typeof(int), typeof(int) }, null);
        private static string ContainerDefaultName(Chest chest)
        {
            Tile tile = Main.tile[chest.x, chest.y];
            if (tile.type == 21) return Lang.chestType[tile.frameX / 36].Value;
            if (tile.type == 467) return Lang.chestType2[tile.frameX / 36].Value;
            return Lang.dresserType[tile.frameX / 54].Value;
        }
        private static string NameKey(BlueprintProjectionPlacement owner, CaptureCell cell) { return owner.Identity.AssetId.ToString("N") + ":" + owner.Identity.SessionGeneration + ":" + owner.Identity.ProjectionGeneration + ":" + cell.X + ":" + cell.Y; }
        internal bool AutomaticContainerOperation
        {
            get
            {
                var target = namingContainer;
                return target?.Requested == true && target.Opened && player != null && player.chest == target.Index &&
                    ReferenceEquals(target.Connection, Netplay.Connection) && ReferenceEquals(target.Tiles, Main.tile) &&
                    target.Index >= 0 && target.Index < Main.chest.Length && ReferenceEquals(Main.chest[target.Index], target.Chest);
            }
        }
        private void ContainerOpenRequested(int x, int y)
        {
            if (sendingContainerOpen) return;
            containerManualGeneration++;
            // Same-box manual31 takes ownership of its eventual open. A
            // different-box request must not let the old reply overwrite it.
            containerOpenDebts.Remove((long)y << 32 | (uint)x);
            if (namingContainer != null) ReleaseContainerName(player, true);
        }
        private bool ContainerOpenAllowed(int index, int x, int y)
        {
            long key = (long)y << 32 | (uint)x; ContainerOpenReceipt debt;
            if (!containerOpenDebts.TryGetValue(key, out debt) || debt.Index != index ||
                !ReferenceEquals(debt.Connection, Netplay.Connection) || !ReferenceEquals(debt.Tiles, Main.tile)) return true;
            containerOpenDebts.Remove(key);
            var target = namingContainer;
            if (!debt.Cancelled && target != null && ContainerTargetCurrent(target) && ReferenceEquals(target.PendingOpen, debt) &&
                index >= 0 && index < Main.chest.Length && ReferenceEquals(Main.chest[index], debt.Chest))
            { target.PendingOpen = null; target.Opened = true; return true; }
            // Cancel only our exact late permission before vanilla assigns the
            // UI. No newer manual request is followed by a synthetic close.
            if (debt.ManualGeneration == containerManualGeneration && player?.chest == -1) NetMessage.SendData(33, -1, -1, null, -1);
            return false;
        }

        private void ObserveContainerNameTarget(BlueprintProjectionPlacement owner, CaptureCell target, BlueprintWorldMatchSample actual)
        {
            if (!BlueprintNativePayload.Ready || target.Payload.NameState != BlueprintPayloadState.Known || !actual.Available || actual.ObjectUnknown ||
                target.TileType != actual.Cell.TileType || target.FrameX != actual.Cell.FrameX || target.FrameY != actual.Cell.FrameY ||
                target.Payload.Compare(actual.Cell.Payload) == BlueprintWorldMatchState.Match) return;
            string key = NameKey(owner, target);
            if (containerNameKeys.ContainsKey(key)) return;
            var value = new ContainerNameTarget { Owner = owner, Mask = owner.Mask, Cell = target, OriginX = owner.OriginX, OriginY = owner.OriginY,
                X = owner.OriginX + target.X, Y = owner.OriginY + target.Y };
            containerNameKeys.Add(key, containerNames.AddLast(value));
        }
        private bool ContainerTargetCurrent(ContainerNameTarget target)
        { return target != null && AutoBuildEnabled && ControlsEnabled && HasSession && projectionScene.Contains(target.Owner) &&
            ReferenceEquals(target.Mask, target.Owner.Mask) && target.OriginX == target.Owner.OriginX && target.OriginY == target.Owner.OriginY; }
        private void RemoveContainerTarget(ContainerNameTarget target)
        {
            LinkedListNode<ContainerNameTarget> node;
            if (containerNameKeys.TryGetValue(NameKey(target.Owner, target.Cell), out node)) { containerNames.Remove(node); containerNameKeys.Remove(NameKey(target.Owner, target.Cell)); }
        }
        private void ReleaseContainerName(Player p, bool userTookOver = false)
        {
            var target = namingContainer; namingContainer = null;
            if (target == null) return;
            if (target.PendingOpen != null) { target.PendingOpen.Cancelled = true; target.PendingOpen = null; }
            if(userTookOver)target.Suspended=true;
            // Only our still-open exact native chest may be closed. Editing,
            // another chest or a changed connection belongs to the player.
            if (!userTookOver) ReturnContainerWindow(p, target);
            target.Requested = target.Opened = target.Submitted = false;
        }
        private void ReturnContainerWindow(Player p, ContainerNameTarget target)
        {
            if (target.Opened && p != null && p.chest == target.Index && !Main.editChest &&
                ReferenceEquals(target.Connection, Netplay.Connection) && ReferenceEquals(target.Tiles, Main.tile) &&
                target.Index >= 0 && target.Index < Main.chest.Length && ReferenceEquals(Main.chest[target.Index], target.Chest))
            {
                if (Main.netMode == 1) NetMessage.SendData(33, -1, -1, null, -1);
                nativeOpenChest?.Invoke(p, new object[] { target.X, target.Y, -1 });
                Main.playerInventory=target.InventoryWasOpen;
            }
            target.Requested = target.Opened = false;
        }
        private void AdvanceContainerNames(Player p)
        {
            if (BlueprintNativePayload.ContainerOpenAllowed == null)
            {
                BlueprintNativePayload.ContainerOpenRequested = ContainerOpenRequested;
                BlueprintNativePayload.ContainerOpenAllowed = ContainerOpenAllowed;
            }
            if (!ReferenceEquals(containerDebtConnection, Netplay.Connection) || !ReferenceEquals(containerDebtTiles, Main.tile))
            {
                containerOpenDebts.Clear(); containerManualGeneration = 0;
                containerDebtConnection = Netplay.Connection; containerDebtTiles = Main.tile;
            }
            var target = namingContainer;
            if (target != null)
            {
                bool takeover = p == null || p.dead || p.ghost || p.noBuilding || !buildTools.Available ||
                    State.Active || HasActiveCaptureWork || ProjectionEditing || pendingProjection != null || deletePending ||
                    Main.editChest || buildTools.ManualLeft || buildTools.ManualRight ||
                    target.Opened && p.chest!=target.Index || !target.Opened && p.chest>=0 && p.chest!=target.Index ||
                    !Input.CanRetainIntent || !Input.SampleFocused || Main.gameMenu || Main.inFancyUI || Main.onlyDrawFancyUI || Main.ingameOptionsWindow ||
                    Main.drawingPlayerChat || Terraria.GameInput.PlayerInput.WritingText;
                if (!ContainerTargetCurrent(target) || takeover || !ReferenceEquals(target.Connection, Netplay.Connection) || !ReferenceEquals(target.Tiles, Main.tile) ||
                    !p.IsInTileInteractionRange(target.X, target.Y, TileReachCheckSettings.Simple) || Main.GameUpdateCount - target.Started >= 600)
                { ReleaseContainerName(p, takeover); return; }
                if (target.Index < 0 || !ReferenceEquals(Main.chest[target.Index], target.Chest)) { ReleaseContainerName(p); return; }
                if (!target.Opened)
                {
                    if (p.chest != target.Index || p.chestX != target.X || p.chestY != target.Y) return;
                    target.Opened = true;
                }
                if (!target.Submitted)
                {
                    if (Main.netMode == 1)
                    {
                        // Message33's edit flag commits to the server's actual
                        // opened chest. Local string mutation is not authority.
                        NetMessage.SendData(33, -1, -1, NetworkText.FromLiteral(target.Cell.Payload.Name), target.Index, 1f);
                        BlueprintNativePayload.RefreshName(target.X, target.Y, target.Index);
                    }
                    else
                    {
                        string previous = Main.npcChatText, previousDefault = Main.defaultChestName;
                        // This is the same default-name context established by
                        // IngameFancyUI before its native RenameChestSubmit.
                        try { Main.defaultChestName = ContainerDefaultName(target.Chest); Main.npcChatText = target.Cell.Payload.Name; ChestUI.RenameChestSubmit(p); }
                        finally { Main.npcChatText = previous; Main.defaultChestName = previousDefault; }
                    }
                    target.Submitted = true;
                }
                // Submission/query are already ordered on this connection.
                // Waiting for the authoritative name owns no use/UI: return
                // only our window now so ordinary and other-name work can run.
                ReturnContainerWindow(p, target); namingContainer = null;
                var actual = BlueprintNativePayload.Read(new CaptureCell { X = target.X, Y = target.Y, TileType = target.Cell.TileType, Layers = CaptureLayer.Tile }, null);
                if (target.Cell.Payload.Compare(actual) != BlueprintWorldMatchState.Match) return;
                RemoveContainerTarget(target); return;
            }
            if (containerNames.Count == 0 || !AutoBuildEnabled || !ControlsEnabled) return;
            var node = containerNames.First; target = node.Value; containerNames.Remove(node); containerNames.AddLast(node);
            if (!ContainerTargetCurrent(target)) { RemoveContainerTarget(target); return; }
            if(target.Suspended)return;
            if (target.Submitted)
            {
                if (!ReferenceEquals(target.Connection, Netplay.Connection) || !ReferenceEquals(target.Tiles, Main.tile) ||
                    target.Index < 0 || target.Index >= Main.chest.Length || !ReferenceEquals(Main.chest[target.Index], target.Chest))
                { RemoveContainerTarget(target); return; }
                var actual = BlueprintNativePayload.Read(new CaptureCell { X = target.X, Y = target.Y, TileType = target.Cell.TileType, Layers = CaptureLayer.Tile }, null);
                if (target.Cell.Payload.Compare(actual) == BlueprintWorldMatchState.Match) RemoveContainerTarget(target);
                else if (Main.GameUpdateCount - target.Started >= 600)
                { target.Suspended = true; projectionFeedback = "此箱名尚未取得原版确认，当前保留名称待办。"; }
                return;
            }
            if (p == null || !CanBuildAutomatically(p) || p.chest != -1 || Main.editChest ||
                buildTools.Use.Active || buildTools.Items.Ownership.HasUse || buildMaterialPreparation?.HasWork == true ||
                p.itemAnimation != 0 || p.itemTime != 0 || p.channel || nativeOpenChest == null) return;
            if (buildContainer != null && !buildContainer.Resolved && buildContainer.Bounds.Contains(target.X, target.Y)) return;
            if (containerOpenDebts.ContainsKey((long)target.Y << 32 | (uint)target.X)) return;
            if (!p.IsInTileInteractionRange(target.X, target.Y, TileReachCheckSettings.Simple)) return;
            BlueprintWorldMatchSample sample;
            if (!ReadBuildCell(target.X, target.Y, out sample) || sample.ObjectUnknown || sample.Cell.TileType != target.Cell.TileType ||
                sample.Cell.FrameX != target.Cell.FrameX || sample.Cell.FrameY != target.Cell.FrameY) return;
            if (target.Cell.Payload.Compare(sample.Cell.Payload) == BlueprintWorldMatchState.Match) { RemoveContainerTarget(target); return; }
            int index = Chest.FindChest(target.X, target.Y);
            if (index < 0 || Chest.UsingChest(index) != -1) return;
            if (target.Cell.Payload.Name.Length > 0 && String.Equals(target.Cell.Payload.Name, ContainerDefaultName(Main.chest[index]), StringComparison.Ordinal))
            {
                // Native renaming normalizes the displayed default to empty;
                // retain an unreachable foreign literal rather than fake Match.
                target.Suspended = true;
                projectionFeedback = "原版会将此箱名恢复为默认名称，当前保留名称待办。";
                return;
            }
            target.Index = index; target.Chest = Main.chest[index]; target.Connection = Netplay.Connection; target.Tiles = Main.tile; target.Started = Main.GameUpdateCount;
            target.InventoryWasOpen=Main.playerInventory;
            namingContainer = target; target.Requested = true;
            if (Main.netMode == 1)
            {
                var debt = new ContainerOpenReceipt { X = target.X, Y = target.Y, Index = target.Index, Chest = target.Chest,
                    Connection = target.Connection, Tiles = target.Tiles, ManualGeneration = containerManualGeneration };
                target.PendingOpen = debt; containerOpenDebts.Add((long)target.Y << 32 | (uint)target.X, debt);
                sendingContainerOpen = true;
                try { NetMessage.SendData(31, -1, -1, null, target.X, target.Y); }
                finally { sendingContainerOpen = false; }
            }
            else { nativeOpenChest.Invoke(p, new object[] { target.X, target.Y, index }); target.Opened = true; AdvanceContainerNames(p); }
        }
        private void ResumeContainerNames()
        { if(AutoBuildEnabled)foreach(var target in containerNames){if(target.Suspended)target.Submitted=false;target.Suspended=false;} }
        private void ClearContainerNames(BlueprintProjectionIdentity identity=null)
        {
            if(identity==null) { ReleaseContainerName(player);containerNames.Clear();containerNameKeys.Clear();return; }
            if(namingContainer?.Owner.Identity.Equals(identity)==true)ReleaseContainerName(player);
            for(var node=containerNames.First;node!=null;)
            {
                var next=node.Next;
                if(node.Value.Owner.Identity.Equals(identity))RemoveContainerTarget(node.Value);
                node=next;
            }
        }
    }
}
