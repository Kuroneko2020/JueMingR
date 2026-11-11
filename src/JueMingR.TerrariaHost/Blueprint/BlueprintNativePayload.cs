using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Blueprint
{
    // Only explicitly requested chest anchors are retained. A network Tile or
    // slot receipt never establishes the custom name, including an empty one.
    internal static partial class BlueprintNativePayload
    {
        private static readonly FieldInfo entityItem = typeof(TELeashedEntityAnchorWithItem).GetField("itemType", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo dollItems = typeof(TEDisplayDoll).GetField("_misc", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo dollPose = typeof(TEDisplayDoll).GetField("_pose", BindingFlags.Instance | BindingFlags.NonPublic);
        private sealed class NameQuery { internal Chest Chest; internal object Connection, Tiles; internal bool Known; internal ulong Started; }
        private static readonly Dictionary<long, NameQuery> names = new Dictionary<long, NameQuery>();
        private static readonly object nameGate = new object();
        private static object connection, tiles, world;
        private static BlueprintWorldChangeObserver observer;
        private static readonly Harmony payloadHarmony = new Harmony("JueMingR.Blueprint.PayloadChanges");
        internal static bool Ready { get; private set; }
        internal static Action<int, int> ContainerOpenRequested;
        internal static Func<int, int, int, bool> ContainerOpenAllowed;
        private const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static long Key(int x, int y) { return (long)y << 32 | (uint)x; }
        private static void ResetIdentity()
        {
            if (ReferenceEquals(connection, Netplay.Connection) && ReferenceEquals(tiles, Main.tile) && ReferenceEquals(world, Main.ActiveWorldFileData)) return;
            names.Clear(); ResetLeashedObservation(); connection = Netplay.Connection; tiles = Main.tile; world = Main.ActiveWorldFileData;
        }
        internal static BlueprintObjectPayload Read(CaptureCell cell, BlueprintObjectShape shape)
        {
            var value = default(BlueprintObjectPayload);
            if ((cell.Layers & CaptureLayer.Tile) == 0 || shape != null && (cell.X != shape.Object.X || cell.Y != shape.Object.Y)) return value;
            if (Main.netMode==1) lock(nameGate) ResetIdentity();
            if (!Ready)
            {
                if (BlueprintObjectPayload.EntityType(cell.TileType)) value.EntityState = BlueprintPayloadState.Unknown;
                if (cell.TileType == TileID.DisplayDoll) value.PoseState = BlueprintPayloadState.Unknown;
                if (BlueprintObjectPayload.ContainerType(cell.TileType)) value.NameState = BlueprintPayloadState.Unknown;
                return value;
            }
            if (BlueprintObjectPayload.EntityType(cell.TileType) || cell.TileType == TileID.DisplayDoll)
            {
                if (BlueprintObjectPayload.EntityType(cell.TileType)) value.EntityState = BlueprintPayloadState.Unknown;
                TileEntity entity;
                if (TileEntity.ByPosition.TryGetValue(new Point16(cell.X, cell.Y), out entity))
                {
                    if ((cell.TileType == 723 && entity is TEKiteAnchor || cell.TileType == 724 && entity is TECritterAnchor) && entityItem?.FieldType == typeof(int))
                    {
                        // Client87/156 creates an initially empty86 TE. Later
                        // FullSync is the actual identity authority, including
                        // replacements; an old nonzero TE field is not fallback.
                        value.EntityItemType = Main.netMode == 1 ? ReadLeashedItem(cell.X, cell.Y, cell.TileType, cell.FrameY) : (int)entityItem.GetValue(entity);
                        if (value.EntityItemType > 0 && ValidEntityItem(cell.TileType,value.EntityItemType,cell.FrameY)) value.EntityState = BlueprintPayloadState.Known; else value.EntityItemType = 0;
                    }
                    if (entity is TEDisplayDoll && dollPose?.FieldType == typeof(byte)) { value.PoseState = BlueprintPayloadState.Known; value.Pose = (byte)dollPose.GetValue(entity); }
                }
                if (cell.TileType == TileID.DisplayDoll && value.PoseState == BlueprintPayloadState.Absent) value.PoseState = BlueprintPayloadState.Unknown;
            }
            if (BlueprintObjectPayload.ContainerType(cell.TileType))
            {
                int index = Chest.FindChest(cell.X, cell.Y);
                Chest chest = index>=0?Main.chest[index]:null;
                if (Main.netMode != 1)
                {
                    value.NameState=chest==null?BlueprintPayloadState.Unknown:BlueprintPayloadState.Known;
                    if(chest!=null)value.Name=chest.name??"";
                }
                else lock(nameGate)
                {
                    ResetIdentity(); long key=Key(cell.X,cell.Y); NameQuery query;
                    if(!names.TryGetValue(key,out query) || !ReferenceEquals(query.Chest,chest))
                    {
                        query=new NameQuery { Chest=chest, Connection=Netplay.Connection,Tiles=Main.tile,Started=Main.GameUpdateCount };
                        names[key]=query;
                        // The server can resolve a negative index, but the
                        // native sender always serializes Main.chest[index].
                        // Wait for this anchor's real local slot before asking;
                        // its later Chest reference replaces this pending query.
                        if (index >= 0) NetMessage.SendData(69,-1,-1,null,index,cell.X,cell.Y);
                    }
                    value.NameState=query.Known && chest!=null?BlueprintPayloadState.Known:BlueprintPayloadState.Unknown;
                    if(value.NameState==BlueprintPayloadState.Known)value.Name=chest.name??"";
                }
            }
            return value;
        }
        internal static void RefreshName(int x, int y, int index)
        {
            lock (nameGate) { ResetIdentity(); NameQuery query;
            if (names.TryGetValue(Key(x, y), out query)) { query.Known = false; query.Started = Main.GameUpdateCount; } }
            NetMessage.SendData(69, -1, -1, null, index, x, y);
        }
        internal static bool NamePending(CaptureCell cell)
        {
            if (Main.netMode != 1 || cell.Payload.NameState != BlueprintPayloadState.Unknown) return false;
            lock (nameGate) { NameQuery query;
            return names.TryGetValue(Key(cell.X, cell.Y), out query) && Main.GameUpdateCount - query.Started < 600; }
        }
        internal static long StructureDependency(int x, int y)
        {
            TileEntity entity;
            if (!TileEntity.ByPosition.TryGetValue(new Point16(x,y),out entity) || !(entity is TEDisplayDoll)) return 0;
            var items = dollItems?.GetValue(entity) as Item[];
            Item held = items != null && items.Length > 0 ? items[0] : null;
            return (long)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(entity) << 32 ^ (held?.type ?? 0) ^ (long)(held?.useStyle ?? 0) << 16;
        }
        internal static bool ValidEntityItem(int type, int itemType, int frameY)
        {
            Item item;
            if (itemType <= 0 || itemType >= ItemID.Count || !ContentSamples.ItemsByType.TryGetValue(itemType, out item)) return false;
            return type == 723 ? ItemID.Sets.IsAKite[itemType] && item.createTile == type :
                type == 724 && item.makeNPC > 0 && item.createTile == type && TECritterAnchor.GetLeashedCritterPrototype(itemType).anchorStyle == frameY / 18;
        }
        internal static void Install(Harmony harmony, BlueprintWorldChangeObserver owner)
        {
            observer = owner;
            // A missing new payload ABI disables these new observations, not
            // the established ordinary-Tile world change hooks or Blueprint.
            harmony = payloadHarmony;
            try
            {
            if (entityItem?.FieldType != typeof(int) || dollPose?.FieldType != typeof(byte)) throw new MissingMemberException("blueprint-payload-abi");
            InstallLeashedObservation(harmony);
            Patch(harmony, typeof(TELeashedEntityAnchorWithItem), "InsertItem", nameof(EntityChanged));
            Patch(harmony, typeof(TELeashedEntityAnchorWithItem), "ReadExtraData", nameof(EntityChanged));
            Patch(harmony, typeof(TEDisplayDoll), "ChangePose", nameof(EntityChanged));
            Patch(harmony, typeof(TEDisplayDoll), "ReadPose", nameof(EntityChanged));
            Patch(harmony, typeof(TEDisplayDoll), "ReadExtraData", nameof(EntityChanged));
            Patch(harmony, typeof(TEDisplayDoll), "ReadData", nameof(EntityChanged));
            harmony.Patch(typeof(Terraria.UI.ChestUI).GetMethod("RenameChestSubmit", F), postfix: new HarmonyMethod(typeof(BlueprintNativePayload).GetMethod(nameof(NameSubmitted), F)));
            harmony.Patch(typeof(TileEntity).GetMethod("Remove", F), prefix: new HarmonyMethod(typeof(BlueprintNativePayload).GetMethod(nameof(EntityRemoving), F)), postfix: new HarmonyMethod(typeof(BlueprintNativePayload).GetMethod(nameof(EntityRemoved), F)));
            var get = typeof(MessageBuffer).GetMethod("GetData", F);
            harmony.Patch(get, new HarmonyMethod(typeof(BlueprintNativePayload).GetMethod(nameof(BeforeReceive), F)), new HarmonyMethod(typeof(BlueprintNativePayload).GetMethod(nameof(AfterReceive), F)));
            harmony.Patch(typeof(NetMessage).GetMethod("ReceiveBytes",F),prefix:new HarmonyMethod(typeof(BlueprintNativePayload).GetMethod(nameof(CurrentReadSource),F)));
            harmony.Patch(typeof(NetMessage).GetMethod("SendData",F),prefix:new HarmonyMethod(typeof(BlueprintNativePayload).GetMethod(nameof(ContainerRequest),F)));
            Ready = true;
            }
            catch { harmony.UnpatchAll(harmony.Id); Ready = false; }
        }
        private static void Patch(Harmony harmony, Type type, string name, string after)
        { harmony.Patch(type.GetMethod(name, F), postfix: new HarmonyMethod(typeof(BlueprintNativePayload).GetMethod(after, F))); }
        private static void EntityRemoving(TileEntity __0, out Point16 __state) { __state = __0.Position; }
        private static void EntityRemoved(Point16 __state, TileEntity __0) { LeashedAnchorRemoved(__0); Changed(__state.X, __state.Y); }
        private static void EntityChanged(TileEntity __instance) { Changed(__instance.Position.X, __instance.Position.Y); }
        private static void NameSubmitted(Player __0)
        {
            // Single-player renaming does not pass through Message69 or touch
            // a Tile. Invalidate its exact anchor so a cached name conflict can
            // finish, and later player renames invalidate still-live targets.
            if (Main.netMode != 0 || __0.chest < 0 || __0.chest >= Main.chest.Length) return;
            var chest = Main.chest[__0.chest];
            if (chest != null) Changed(chest.x, chest.y);
        }
        private static void Changed(int x, int y)
        { if (observer?.HasInterest(x, y, 1) == true) using (observer.BeginMutation(new BlueprintProjectionBounds(x, y, 1, 1))) { } }
        // RemoteServer callbacks may outlive their connection. Reject actual
        // old bytes before native appending to shared buffer256; a no-data or
        // disconnected old callback must not poison current payload knowledge.
        private static bool CurrentReadSource(byte[] __0, int __2)
        { return Main.netMode != 1 || __2 != 256 || ReferenceEquals(__0, Netplay.Connection.ReadBuffer); }
        private static void ContainerRequest(int __0, int __4, float __5)
        { if (Main.netMode == 1 && __0 == 31) ContainerOpenRequested?.Invoke(__4, (int)__5); }
        private struct Receipt { internal int Kind, X, Y, Index; internal object Connection, Tiles; }
        private static bool BeforeReceive(MessageBuffer __instance, int __0, int __1, out Receipt __state)
        {
            __state = default(Receipt);
            if (Main.netMode != 1 || __instance.whoAmI != 256 || __0 < 0 || __1 < 7 || __0 > __instance.readBuffer.Length - __1) return true;
            byte[] data = __instance.readBuffer; int kind = data[__0];
            int a = (short)(data[__0 + 1] | data[__0 + 2] << 8), b = (short)(data[__0 + 3] | data[__0 + 4] << 8), c = (short)(data[__0 + 5] | data[__0 + 6] << 8);
            if (kind == 33) return ContainerOpenAllowed?.Invoke(a, b, c) != false;
            if (kind != 69 && kind != 156) return true;
            __state = new Receipt { Kind = kind, Index = a, X = kind == 69 ? b : a, Y = kind == 69 ? c : b, Connection = Netplay.Connection, Tiles = Main.tile };
            return true;
        }
        private static void AfterReceive(Receipt __state, bool __runOriginal)
        {
            if (!__runOriginal || __state.Kind == 0 || !ReferenceEquals(__state.Connection, Netplay.Connection) || !ReferenceEquals(__state.Tiles, Main.tile)) return;
            if (__state.Kind == 69)
            {
                lock (nameGate) { ResetIdentity(); NameQuery query;
                if (names.TryGetValue(Key(__state.X, __state.Y), out query) && __state.Index >= 0 && __state.Index < Main.chest.Length &&
                    Main.chest[__state.Index]!=null && Main.chest[__state.Index].x==__state.X && Main.chest[__state.Index].y==__state.Y &&
                    (query.Chest==null || ReferenceEquals(query.Chest,Main.chest[__state.Index])))
                {query.Chest=Main.chest[__state.Index];query.Known=true;} }
            }
            Changed(__state.X, __state.Y);
        }
        internal static void Clear() { payloadHarmony.UnpatchAll(payloadHarmony.Id); Ready = false; ResetLeashedObservation(); ContainerOpenRequested = null; ContainerOpenAllowed = null; lock (nameGate) { names.Clear(); observer = null; connection = tiles = world = null; } }
    }
}
