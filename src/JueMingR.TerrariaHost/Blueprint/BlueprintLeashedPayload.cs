using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.LeashedEntities;
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal static partial class BlueprintNativePayload
    {
        private static readonly FieldInfo leashedSections = typeof(LeashedEntity).GetField("BySection", F);
        private static readonly Type leashedSectionType = typeof(LeashedEntity).GetNestedType("SectionEntityList", F);
        private static readonly FieldInfo sectionEntities = leashedSectionType?.GetField("list", F), sectionEntityCount = leashedSectionType?.GetField("count", F);
        private static readonly FieldInfo leashedNpcType = typeof(LeashedCritter).GetField("npcType", F);
        private static readonly FieldInfo butterflyVariant = typeof(NormalButterflyLeashedCritter).GetField("variant", F);
        private static readonly Dictionary<long, int> kiteProducers = new Dictionary<long, int>(), critterProducers = new Dictionary<long, int>();
        private static bool leashedProducersReady;
        // This is a read-only anchor index for one native section, not a second
        // moving-entity model. Changing sections rebuilds this one index;
        // repeated reads within it use native slots/references. Real sync and
        // removal update the current section without reading motion state.
        private static object indexedLeashedSection;
        private static int indexedSectionX = -1, indexedSectionY = -1;
        private static readonly Dictionary<long, LeashedEntity> indexedLeashedAnchors = new Dictionary<long, LeashedEntity>();
        private static readonly Dictionary<long, TileEntity> awaitingLeashedSync = new Dictionary<long, TileEntity>();
        private static void ClearLeashedObservation()
        { indexedLeashedSection = null; indexedSectionX = indexedSectionY = -1; indexedLeashedAnchors.Clear(); }
        private static void ResetLeashedObservation() { ClearLeashedObservation(); awaitingLeashedSync.Clear(); }
        private static long KiteProducerKey(int type, int projectile) { return (long)type << 32 | (uint)projectile; }
        private static long CritterProducerKey(int type, int npc, int style, int variant)
        { return (long)type << 48 | (long)npc << 32 | (long)(ushort)style << 16 | (ushort)variant; }
        private static void AddLeashedProducer(Dictionary<long, int> map, long key, int item)
        { int prior; if (!map.TryGetValue(key, out prior)) map.Add(key, item); else if (prior != item) map[key] = 0; }
        private static bool EnsureLeashedProducers()
        {
            if (leashedProducersReady) return true;
            if (LeashedKite.Prototype == null || LeashedKite.Prototype.Type == 0 || ContentSamples.ItemsByType.Count == 0) return false;
            kiteProducers.Clear(); critterProducers.Clear();
            for (int id = 1; id < ItemID.Count; id++)
            {
                Item item;
                if (!ContentSamples.ItemsByType.TryGetValue(id, out item)) continue;
                if (item.createTile == 723 && ItemID.Sets.IsAKite[id]) AddLeashedProducer(kiteProducers, KiteProducerKey(LeashedKite.Prototype.Type, item.shoot), id);
                if (item.createTile == 724 && item.makeNPC > 0)
                {
                    var prototype = TECritterAnchor.GetLeashedCritterPrototype(id);
                    if (prototype == null || prototype.Type == 0) return false;
                    AddLeashedProducer(critterProducers, CritterProducerKey(prototype.Type, item.makeNPC, prototype.anchorStyle, prototype is NormalButterflyLeashedCritter ? item.placeStyle : 0), id);
                }
            }
            leashedProducersReady = true; return true;
        }
        private static void IndexLeashedAnchor(LeashedEntity entity)
        {
            if (entity == null || !entity.active || !(entity is LeashedKite) && !(entity is LeashedCritter)) return;
            long key = Key(entity.AnchorPosition.X, entity.AnchorPosition.Y); LeashedEntity prior;
            if (indexedLeashedAnchors.TryGetValue(key, out prior) && (prior == null || prior.active && !ReferenceEquals(prior, entity))) indexedLeashedAnchors[key] = null;
            else indexedLeashedAnchors[key] = entity;
        }
        private static int ReadLeashedItem(int x, int y, int tileType, int frameY)
        {
            if (!EnsureLeashedProducers()) return 0;
            if (awaitingLeashedSync.ContainsKey(Key(x, y))) return 0;
            int sx = Netplay.GetSectionX(x), sy = Netplay.GetSectionY(y);
            var sections = leashedSections.GetValue(null) as Array;
            if (sections == null || sx < 0 || sy < 0 || sx >= sections.GetLength(0) || sy >= sections.GetLength(1)) return 0;
            object section = sections.GetValue(sx, sy);
            if (!ReferenceEquals(section, indexedLeashedSection) || sx != indexedSectionX || sy != indexedSectionY)
            {
                indexedLeashedAnchors.Clear(); indexedLeashedSection = section; indexedSectionX = sx; indexedSectionY = sy;
                if (section != null)
                {
                    var entities = (LeashedEntity[])sectionEntities.GetValue(section); int count = (int)sectionEntityCount.GetValue(section);
                    for (int i = 0; i < count; i++) IndexLeashedAnchor(entities[i]);
                }
            }
            LeashedEntity entity, current;
            if (!indexedLeashedAnchors.TryGetValue(Key(x, y), out entity) || entity == null || !entity.active || entity.AnchorPosition != new Point16(x, y) ||
                !LeashedEntity.TryGet(entity.whoAmI, out current) || !ReferenceEquals(entity, current)) return 0;
            int item = 0;
            var kite = entity as LeashedKite;
            var critter = entity as LeashedCritter;
            if (tileType == 723 && kite != null) kiteProducers.TryGetValue(KiteProducerKey(kite.Type, kite.projType), out item);
            if (tileType == 724 && critter != null && critter.anchorStyle == frameY / 18)
                critterProducers.TryGetValue(CritterProducerKey(critter.Type, (int)leashedNpcType.GetValue(critter), critter.anchorStyle,
                    critter is NormalButterflyLeashedCritter ? (byte)butterflyVariant.GetValue(critter) : 0), out item);
            return item > 0 && ValidEntityItem(tileType, item, frameY) ? item : 0;
        }
        private static void InstallLeashedObservation(Harmony harmony)
        {
            if (leashedSections == null || sectionEntities == null || sectionEntityCount == null || leashedNpcType?.FieldType != typeof(int) || butterflyVariant?.FieldType != typeof(byte)) throw new MissingMemberException("blueprint-leashed-identity-abi");
            harmony.Patch(typeof(TileEntity).GetMethod("Add", F), postfix: new HarmonyMethod(typeof(BlueprintNativePayload).GetMethod(nameof(LeashedAnchorAdded), F)));
            Type module = typeof(LeashedEntity).GetNestedType("NetModule", F);
            harmony.Patch(module.GetMethod("HandleFullSync", F), postfix: new HarmonyMethod(typeof(BlueprintNativePayload).GetMethod(nameof(LeashedSynchronized), F)));
            harmony.Patch(typeof(LeashedEntity).GetMethod("Remove", F, null, new[] { typeof(LeashedEntity) }, null), postfix: new HarmonyMethod(typeof(BlueprintNativePayload).GetMethod(nameof(LeashedRemoved), F)));
        }
        private static void LeashedSynchronized(int __0, bool __runOriginal)
        {
            LeashedEntity entity;
            if (!__runOriginal || !LeashedEntity.TryGet(__0, out entity)) return;
            if (Main.netMode == 1)
            {
                lock (nameGate) ResetIdentity();
                TileEntity anchor, awaited;
                long key = Key(entity.AnchorPosition.X, entity.AnchorPosition.Y);
                if (TileEntity.ByPosition.TryGetValue(entity.AnchorPosition, out anchor) && awaitingLeashedSync.TryGetValue(key, out awaited) && ReferenceEquals(anchor, awaited) &&
                    (anchor is TEKiteAnchor && entity is LeashedKite || anchor is TECritterAnchor && entity is LeashedCritter)) awaitingLeashedSync.Remove(key);
            }
            var section = entity.SectionCoordinates;
            if (section.X == indexedSectionX && section.Y == indexedSectionY) IndexLeashedAnchor(entity);
            Changed(entity.AnchorPosition.X, entity.AnchorPosition.Y);
        }
        private static void LeashedAnchorAdded(TileEntity __0)
        {
            if (Main.netMode != 1 || !(__0 is TELeashedEntityAnchorWithItem)) return;
            lock (nameGate) ResetIdentity();
            // A new86 cannot borrow an older live entity at the same anchor.
            // The subsequent native FullSync must bind this exact TE instance.
            awaitingLeashedSync[Key(__0.Position.X, __0.Position.Y)] = __0;
            Changed(__0.Position.X, __0.Position.Y);
        }
        private static void LeashedAnchorRemoved(TileEntity entity)
        {
            TileEntity awaited; long key = Key(entity.Position.X, entity.Position.Y);
            if (awaitingLeashedSync.TryGetValue(key, out awaited) && ReferenceEquals(awaited, entity)) awaitingLeashedSync.Remove(key);
        }
        private static void LeashedRemoved(LeashedEntity __0)
        {
            var section = __0.SectionCoordinates;
            if (section.X == indexedSectionX && section.Y == indexedSectionY) ClearLeashedObservation();
            Changed(__0.AnchorPosition.X, __0.AnchorPosition.Y);
        }
    }
}
