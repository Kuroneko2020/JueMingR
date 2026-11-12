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
            internal BlueprintWorldObject Body;
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
        // A silent31 refusal is still the same intent, not a cancelled receipt.
        // Only this one automatic permission may be unresolved on a connection;
        // it owns neither the execution/UI slot nor a tool/material operation.
        private ContainerNameTarget waitingContainer;
        // Native31 has no request id and a rejected request has no reply. Keep
        // the coordinate obligation after cancellation; never lend its33 to a
        // new transaction. It retains no projection/window/asset, and another
        // coordinate can be serviced after a real cancellation; a still-live
        // pending intent retries the same box instead of lending its permission.
        private sealed class ContainerOpenReceipt
        {
            internal int X, Y, Index;
            internal Chest Chest;
            internal bool Cancelled;
            internal object Connection, Tiles;
            internal long OpenGeneration;
        }
        private readonly Dictionary<long, ContainerOpenReceipt> containerOpenDebts = new Dictionary<long, ContainerOpenReceipt>();
        private long containerOpenGeneration;
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
            containerOpenGeneration++;
            // Same-box manual31 takes ownership of its eventual open. A
            // different-box request must not let the old reply overwrite it.
            containerOpenDebts.Remove((long)y << 32 | (uint)x);
            if (namingContainer != null || waitingContainer != null) ReleaseContainerName(player, true);
        }
        private bool ContainerOpenAllowed(int index, int x, int y)
        {
            long key = (long)y << 32 | (uint)x; ContainerOpenReceipt debt;
            if (!containerOpenDebts.TryGetValue(key, out debt) || debt.Index != index ||
                !ReferenceEquals(debt.Connection, Netplay.Connection) || !ReferenceEquals(debt.Tiles, Main.tile)) return true;
            containerOpenDebts.Remove(key);
            var target = waitingContainer;
            BlueprintWorldMatchSample anchor;
            if (!debt.Cancelled && debt.OpenGeneration == containerOpenGeneration && target != null && ContainerTargetCurrent(target) &&
                ReferenceEquals(target.PendingOpen, debt) && player?.chest == -1 && !ContainerNameTakeover(player, target) &&
                ContainerNameExecutionReady(player,target,false) &&
                player.IsInTileInteractionRange(target.X,target.Y,TileReachCheckSettings.Simple) &&
                index >= 0 && index < Main.chest.Length && ReferenceEquals(Main.chest[index], debt.Chest) &&
                ContainerBodyCurrent(target,out anchor))
            { target.PendingOpen = null; waitingContainer = null; namingContainer = target; target.Opened = true; return true; }
            if (target != null && ReferenceEquals(target.PendingOpen,debt))
            { target.PendingOpen = null; target.Requested = false; waitingContainer = null; }
            // Cancel only our exact late permission before vanilla assigns the
            // UI. No newer manual request is followed by a synthetic close.
            if (debt.OpenGeneration == containerOpenGeneration && player?.chest == -1) NetMessage.SendData(33, -1, -1, null, -1);
            return false;
        }

        private void ObserveContainerNameTarget(BlueprintProjectionPlacement owner, CaptureCell target, BlueprintWorldMatchSample actual, BlueprintWorldObject body)
        {
            if (!BlueprintNativePayload.Ready || target.Payload.NameState != BlueprintPayloadState.Known || !actual.Available || actual.ObjectUnknown ||
                body == null || !BlueprintObjectPayload.ContainerType(actual.Cell.TileType) ||
                target.Payload.Compare(actual.Cell.Payload) == BlueprintWorldMatchState.Match) return;
            string key = NameKey(owner, target);
            if (containerNameKeys.ContainsKey(key)) return;
            // The caller already proved the complete SameBody under the current
            // substitution policy. Keep its immutable target, not an independent
            // template-frame equality that would reject an allowed chest skin.
            var value = new ContainerNameTarget { Owner = owner, Mask = owner.Mask, Cell = target, Body = body, OriginX = owner.OriginX, OriginY = owner.OriginY,
                X = owner.OriginX + target.X, Y = owner.OriginY + target.Y };
            containerNameKeys.Add(key, containerNames.AddLast(value));
        }
        private bool ContainerTargetCurrent(ContainerNameTarget target)
        { return target != null && AutoBuildEnabled && ControlsEnabled && HasSession && projectionScene.Contains(target.Owner) &&
            ReferenceEquals(target.Mask, target.Owner.Mask) && target.OriginX == target.Owner.OriginX && target.OriginY == target.Owner.OriginY &&
            containerNameKeys.ContainsKey(NameKey(target.Owner,target.Cell)); }
        private bool ContainerBodyCurrent(ContainerNameTarget target,out BlueprintWorldMatchSample anchor)
        {
            anchor = default(BlueprintWorldMatchSample);
            if (!ContainerTargetCurrent(target) || !ReadBuildCell(target.X,target.Y,out anchor) || !anchor.Available || anchor.ObjectUnknown ||
                !BlueprintObjectPayload.ContainerType(anchor.Cell.TileType) || !SameSubstitutionBody(target.Body,anchor.Object)) return false;
            var observed = anchor.Object;
            // The supported chest/dresser bodies contain at most six cells.
            // Reprove only this body at request/permission/commit boundaries;
            // waiting ticks and name receipts never scan all world containers.
            if (observed == null || observed.MemberCount > 6) return false;
            var proof = new BlueprintObjectBodyProof(target.Body,MaterialCatalog,substitutionPolicy);
            for(int row=0;row<observed.Height;row++)for(int column=0;column<observed.Width;column++)
            {
                BlueprintWorldMatchSample member;
                if(!ReadBuildCell(observed.WorldX+column,observed.WorldY+row,out member))return false;
                proof.Observe(member);
            }
            return proof.Complete()==BlueprintBodyState.Same;
        }
        private BlueprintObjectPayload ContainerActualName(ContainerNameTarget target)
        {
            BlueprintWorldMatchSample actual;
            return ReadBuildCell(target.X,target.Y,out actual) && actual.Available && !actual.ObjectUnknown && BlueprintObjectPayload.ContainerType(actual.Cell.TileType)
                ? actual.Cell.Payload : new BlueprintObjectPayload { NameState=BlueprintPayloadState.Unknown };
        }
        private bool ContainerNameTakeover(Player p,ContainerNameTarget target)
        {
            return p == null || p.dead || p.ghost || p.noBuilding || !buildTools.Available ||
                State.Active || HasActiveCaptureWork || ProjectionEditing || pendingProjection != null || deletePending ||
                Main.editChest || buildTools.ManualLeft || buildTools.ManualRight ||
                target.Opened && p.chest!=target.Index || !target.Opened && p.chest>=0 && p.chest!=target.Index ||
                !Input.CanRetainIntent || !Input.SampleFocused || Main.gameMenu || Main.inFancyUI || Main.onlyDrawFancyUI || Main.ingameOptionsWindow ||
                Main.drawingPlayerChat || Terraria.GameInput.PlayerInput.WritingText;
        }
        private bool ContainerNameExecutionReady(Player p,ContainerNameTarget target,bool requireInput)
        {
            // Retaining a name intent is not permission to interact. A silent
            // refusal can outlive a player's inventory/cursor operation; those
            // temporary gates pause this intent rather than cancel its receipt.
            if(p==null || !ReferenceEquals(p,player) || requireInput && !Input.CanRunAutomaticActions ||
                buildTools.Use.Active || buildTools.Items.Ownership.HasUse || buildMaterialPreparation?.HasWork==true ||
                p.itemAnimation!=0 || p.itemTime!=0 || p.channel)return false;
            bool ownWindow=ReferenceEquals(namingContainer,target) && target.Opened && p.chest==target.Index &&
                ReferenceEquals(target.Connection,Netplay.Connection) && ReferenceEquals(target.Tiles,Main.tile) &&
                target.Index>=0 && target.Index<Main.chest.Length && ReferenceEquals(Main.chest[target.Index],target.Chest);
            if(!ownWindow)return buildTools.CanRetainUse(p,false);
            // CanRetainUse correctly rejects any chest/inventory UI. Only our
            // exact automatically opened window needs that exception here;
            // preserve its other manual-item, menu, text and input vetoes.
            return buildTools.Available && buildTools.CanInterface?.Invoke()==true && Input.CanRetainAutomaticIntent && !Main.gamePaused &&
                !p.dead && !p.CCed && !p.cursed && !p.noItems && !p.isOperatingAnotherEntity && !p.HasLockedInventory() &&
                !buildTools.Items.World.Busy && !buildTools.Items.World.HasManualOperation && !Main.mapFullscreen &&
                !Main.inFancyUI && !Main.onlyDrawFancyUI && !Main.ingameOptionsWindow && !Main.blockInput && !Main.drawingPlayerChat &&
                !Main.editSign && !Main.editChest && !Terraria.GameInput.PlayerInput.WritingText && Main.CurrentInputTextTakerOverride==null &&
                !Main.ServerSideCharacter && (Main.ActivePlayerFileData==null || !Main.ActivePlayerFileData.ServerSideCharacter) &&
                !WorldGen.isGeneratingOrLoadingWorld && !Terraria.GameInput.PlayerInput.UsingGamepadUI && p.talkNPC<0 && p.sign<0 && Main.npcShop==0 &&
                (buildTools.CanMouseInterface?.Invoke()??(!Input.IsFocused || !p.mouseInterface)) && Main.mouseItem!=null && Main.mouseItem.IsAir &&
                !buildTools.ManualRight && !Terraria.GameInput.PlayerInput.Triggers.Current.SmartSelect;
        }
        private void RemoveContainerTarget(ContainerNameTarget target)
        {
            LinkedListNode<ContainerNameTarget> node;
            if (containerNameKeys.TryGetValue(NameKey(target.Owner, target.Cell), out node)) { containerNames.Remove(node); containerNameKeys.Remove(NameKey(target.Owner, target.Cell)); }
            if(ReferenceEquals(namingContainer,target) || ReferenceEquals(waitingContainer,target))ReleaseContainerName(player);
        }
        private void ReleaseContainerName(Player p, bool userTookOver = false)
        {
            var target = namingContainer ?? waitingContainer;
            // Cancel/setting changes use this same return path before the
            // next Advance. Sample manual admission while the exact UI owner
            // is still attached; cancellation must not write its stale bag flag.
            bool restoreInventory=target!=null && (!target.Opened || ContainerNameExecutionReady(p,target,false));
            namingContainer = waitingContainer = null;
            if (target == null) return;
            if (target.PendingOpen != null) { target.PendingOpen.Cancelled = true; target.PendingOpen = null; }
            if(userTookOver)target.Suspended=true;
            // Only our still-open exact native chest may be closed. Editing,
            // another chest or a changed connection belongs to the player.
            if (!userTookOver) ReturnContainerWindow(p, target,restoreInventory);
            target.Requested = target.Opened = target.Submitted = false;
        }
        private void ReturnContainerWindow(Player p, ContainerNameTarget target, bool restoreInventory=true)
        {
            if (target.Opened && p != null && p.chest == target.Index && !Main.editChest &&
                ReferenceEquals(target.Connection, Netplay.Connection) && ReferenceEquals(target.Tiles, Main.tile) &&
                target.Index >= 0 && target.Index < Main.chest.Length && ReferenceEquals(Main.chest[target.Index], target.Chest))
            {
                if (Main.netMode == 1) NetMessage.SendData(33, -1, -1, null, -1);
                bool currentInventory=Main.playerInventory;
                nativeOpenChest?.Invoke(p, new object[] { target.X, target.Y, -1 });
                Main.playerInventory=restoreInventory?target.InventoryWasOpen:currentInventory;
            }
            target.Requested = target.Opened = false;
        }
        private void SendContainerOpen(ContainerNameTarget target)
        {
            sendingContainerOpen = true;
            try { NetMessage.SendData(31,-1,-1,null,target.X,target.Y); }
            finally { sendingContainerOpen = false; }
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
                containerOpenDebts.Clear(); containerOpenGeneration = 0;
                containerDebtConnection = Netplay.Connection; containerDebtTiles = Main.tile;
            }
            var waiting = waitingContainer;
            if(waiting != null)
            {
                bool takeover=ContainerNameTakeover(p,waiting);
                if(!ContainerTargetCurrent(waiting) || takeover || !ReferenceEquals(waiting.Connection,Netplay.Connection) ||
                    !ReferenceEquals(waiting.Tiles,Main.tile) || waiting.Index<0 || waiting.Index>=Main.chest.Length ||
                    !ReferenceEquals(Main.chest[waiting.Index],waiting.Chest))
                    ReleaseContainerName(p,takeover);
                else if(waiting.Cell.Payload.Compare(ContainerActualName(waiting))==BlueprintWorldMatchState.Match)
                    RemoveContainerTarget(waiting);
                else if(Main.GameUpdateCount-waiting.Started>=600)
                {
                    // Timeout is only a retry throttle:31 can be silently
                    // refused, including retries while this requester already
                    // owns the server chest. It is not a cancellation or proof
                    // that an old reply was drained. Same-intent retries keep
                    // one receipt; never issue another automatic box meanwhile.
                    waiting.Started=Main.GameUpdateCount;
                    BlueprintWorldMatchSample anchor;
                    if(ContainerNameExecutionReady(p,waiting,true) && p.chest==-1 && p.IsInTileInteractionRange(waiting.X,waiting.Y,TileReachCheckSettings.Simple) &&
                        Chest.UsingChest(waiting.Index)==-1 && ContainerBodyCurrent(waiting,out anchor))SendContainerOpen(waiting);
                }
            }
            var target = namingContainer;
            if (target != null)
            {
                bool takeover = ContainerNameTakeover(p,target);
                if (!ContainerTargetCurrent(target) || takeover || !ReferenceEquals(target.Connection, Netplay.Connection) || !ReferenceEquals(target.Tiles, Main.tile) ||
                    !p.IsInTileInteractionRange(target.X, target.Y, TileReachCheckSettings.Simple))
                { ReleaseContainerName(p, takeover); return; }
                BlueprintWorldMatchSample anchor;
                if (target.Index < 0 || target.Index>=Main.chest.Length || !ReferenceEquals(Main.chest[target.Index], target.Chest) ||
                    !ContainerBodyCurrent(target,out anchor)) { ReleaseContainerName(p); return; }
                if (!target.Opened)
                {
                    if (p.chest != target.Index || p.chestX != target.X || p.chestY != target.Y) return;
                    target.Opened = true;
                }
                if (!target.Submitted)
                {
                    if(!ContainerNameExecutionReady(p,target,false))
                    {
                        // Return only our exact chest permission. The player
                        // now owns the bag/cursor; never restore the inventory
                        // flag captured before that newer manual operation.
                        ReturnContainerWindow(p,target,false);namingContainer=null;
                        return;
                    }
                    if(!Input.CanRunAutomaticActions)return;
                    if(target.Cell.Payload.Compare(anchor.Cell.Payload)==BlueprintWorldMatchState.Match)
                        target.Submitted=true;
                    else if (Main.netMode == 1)
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
                var actual = ContainerActualName(target);
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
                var actual = ContainerActualName(target);
                if (target.Cell.Payload.Compare(actual) == BlueprintWorldMatchState.Match) RemoveContainerTarget(target);
                else if (Main.GameUpdateCount - target.Started >= 600)
                { target.Suspended = true; projectionFeedback = "此箱名尚未取得原版确认，当前保留名称待办。"; }
                return;
            }
            if(waitingContainer != null)return;
            if (p == null || !CanBuildAutomatically(p) || p.chest != -1 || Main.editChest ||
                buildTools.Use.Active || buildTools.Items.Ownership.HasUse || buildMaterialPreparation?.HasWork == true ||
                p.itemAnimation != 0 || p.itemTime != 0 || p.channel || nativeOpenChest == null) return;
            if (buildContainer != null && !buildContainer.Resolved && buildContainer.Bounds.Contains(target.X, target.Y)) return;
            if (containerOpenDebts.ContainsKey((long)target.Y << 32 | (uint)target.X)) return;
            if (!p.IsInTileInteractionRange(target.X, target.Y, TileReachCheckSettings.Simple)) return;
            BlueprintWorldMatchSample sample;
            if (!ContainerBodyCurrent(target,out sample)) return;
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
            target.Requested = true;
            if (Main.netMode == 1)
            {
                var debt = new ContainerOpenReceipt { X = target.X, Y = target.Y, Index = target.Index, Chest = target.Chest,
                    Connection = target.Connection, Tiles = target.Tiles, OpenGeneration = ++containerOpenGeneration };
                target.PendingOpen = debt; containerOpenDebts.Add((long)target.Y << 32 | (uint)target.X, debt);
                waitingContainer=target;
                SendContainerOpen(target);
            }
            else { namingContainer=target;nativeOpenChest.Invoke(p, new object[] { target.X, target.Y, index }); target.Opened = true; AdvanceContainerNames(p); }
        }
        private void ResumeContainerNames()
        { if(AutoBuildEnabled)foreach(var target in containerNames){if(target.Suspended)target.Submitted=false;target.Suspended=false;} }
        private void ClearContainerNames(BlueprintProjectionIdentity identity=null)
        {
            if(identity==null) { ReleaseContainerName(player);containerNames.Clear();containerNameKeys.Clear();return; }
            if(namingContainer?.Owner.Identity.Equals(identity)==true || waitingContainer?.Owner.Identity.Equals(identity)==true)ReleaseContainerName(player);
            for(var node=containerNames.First;node!=null;)
            {
                var next=node.Next;
                if(node.Value.Owner.Identity.Equals(identity))RemoveContainerTarget(node.Value);
                node=next;
            }
        }
    }
}
