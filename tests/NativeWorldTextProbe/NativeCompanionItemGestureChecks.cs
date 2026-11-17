using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Net;
using System.Net.Sockets;
using HarmonyLib;
using JueMingR.Platform.Companions;
using JueMingR.Platform.Items;
using Terraria;
using Terraria.ID;
using Terraria.Net.Sockets;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Isolated native components, actual host commit/archive and Item rules.
    // No game is launched; this cannot establish OS input, Steam or FPS results.
    internal static class NativeCompanionItemGestureChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static bool failCapture;
        private static bool failSend;
        private static int itemClones, nativeCaptures;
        private static ItemOperationOwnership trashGuard;
        private static bool blockTrashDuringPrepare;
        private static readonly List<Tuple<int,Player>> trashResearch = new List<Tuple<int,Player>>();
        private static void ResearchObserved(ref Item item) { trashResearch.Add(Tuple.Create(item?.type ?? 0,Main.LocalPlayer)); }
        private static void BlockTrashAfterPrepare()
        { if(blockTrashDuringPrepare) { blockTrashDuringPrepare=false; Require(trashGuard.TryBeginDiscard(trashGuard.Session,49),"late native discard guard acquired during detached preparation"); } }
        private static bool Trash(object host,Guid id,bool owner,int slot)
        { return (bool)Call(host,"QuickTrashGesture",id,owner,CompanionInventoryArea.Inventory,slot); }
        private static readonly List<byte[]> primaryPackets = new List<byte[]>();
        private static void CaptureFailure() { nativeCaptures++; if (failCapture) throw new IOException("isolated item capture failure"); }
        private static void CountClone() { itemClones++; }
        [HarmonyPriority(Priority.Last)]
        private static bool CapturePrimary(byte[] __0)
        { if (failSend) throw new IOException("isolated item send failure"); primaryPackets.Add(__0.Take(BitConverter.ToUInt16(__0, 0)).ToArray()); return false; }
        private static object EnumGesture(object host, string gesture)
        { return Enum.Parse(host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionItemGesture"), gesture); }
        private static bool Gesture(object host, Guid id, bool owner, CompanionInventoryArea area, int slot, string gesture)
        { return (bool)Call(host, "ItemGesture", id, owner, area, slot, EnumGesture(host, gesture)); }
        private static Item Preview(object host, Guid id, bool owner, CompanionInventoryArea area, int slot)
        { return (Item)host.GetType().GetMethod("ItemPreview", Flags, null, new[] { typeof(Guid), typeof(bool), typeof(CompanionInventoryArea), typeof(int) }, null).Invoke(host, new object[] { id, owner, area, slot }); }
        private static void Put(Item[] items, int slot, int type, int stack = 1)
        { var item = new Item(); item.SetDefaults(type); item.stack = stack; items[slot] = item; }
        private static void Clear(Player player)
        {
            foreach (var array in new[] { player.inventory, player.armor, player.dye, player.miscEquips, player.miscDyes })
                for (int slot = 0; slot < array.Length; slot++) array[slot] = new Item();
            player.itemAnimation = player.itemTime = player.reuseDelay = 0; player.channel = false; player.dead = player.ghost = false;
            player.selectedItemState.Select(0);
        }
        private static void Saved(object host, Guid id)
        { var profiles = Get(host, "profiles"); NativeQuickItemChecks.Until(() => { Call(profiles, "Pump"); return (bool)Call(profiles, "IsSaved", id); }); }

        internal static void Run(object context)
        {
            NativeCombatAimChecks.Initialize();
            // SetDefaults derives Item.dye from the native shader registry,
            // not the type's case body. This CPU fixture bypasses Main.Load's
            // registration. Run the real basic-color registration, which only
            // records shader metadata; do not manufacture a dye field or Draw.
            if (Terraria.Graphics.Shaders.GameShaders.Armor.GetShaderIdFromItemId(ItemID.RedDye) == 0)
                AccessTools.Method(typeof(Terraria.Initializers.DyeInitializer), "LoadBasicColorDyes").Invoke(null, null);
            var nativeDye = new Item(); nativeDye.SetDefaults(ItemID.RedDye);
            Require(nativeDye.dye > 0, "native basic-color shader registration must classify a real RedDye before dye-slot tests");
            // Main's later initialization derives projHook from each original
            // Projectile's aiStyle. Initialize only the projectile used here.
            // The earlier real TileAndNPCData1 call supplies pet/light tables;
            // check the complete set of tested misc prerequisites together so
            // a missing native category fails at setup, not after a transfer.
            var nativeHook = new Item(); nativeHook.SetDefaults(ItemID.GrapplingHook);
            Require(nativeHook.shoot > 0, "original grappling-hook Item names a native projectile");
            var nativeHookProjectile = new Projectile(); nativeHookProjectile.SetDefaults(nativeHook.shoot);
            Main.projHook[nativeHook.shoot] = nativeHookProjectile.aiStyle == 7;
            var nativeLight = new Item(); nativeLight.SetDefaults(ItemID.MagicLantern);
            var nativePet = new Item(); nativePet.SetDefaults(ItemID.Carrot);
            var nativeCart = new Item(); nativeCart.SetDefaults(ItemID.Minecart);
            var nativeMount = new Item(); nativeMount.SetDefaults(ItemID.SlimySaddle);
            Require(Main.projHook[nativeHook.shoot] && nativeLight.buffType > 0 && Main.lightPet[nativeLight.buffType] &&
                nativePet.buffType > 0 && Main.vanityPet[nativePet.buffType] && !Main.lightPet[nativePet.buffType] &&
                nativeCart.mountType >= 0 && MountID.Sets.Cart[nativeCart.mountType] &&
                nativeMount.mountType >= 0 && !MountID.Sets.Cart[nativeMount.mountType],
                "actual native metadata must classify hook, light pet, vanity pet, minecart and mount before exact misc-slot tests");
            Main.spawnTileX=60;Main.spawnTileY=58;Main.leftWorld=Main.topWorld=0;Main.rightWorld=Main.maxTilesX*16;Main.bottomWorld=Main.maxTilesY*16;
            for(int x=2;x<118;x++){Main.tile[x,60].active(true);Main.tile[x,60].type=TileID.Stone;}
            new Harmony("JueMingR.Tests.QuickItemOutlets").Unpatch(AccessTools.Method(typeof(Player), "Spawn"), HarmonyPatchType.Prefix, "JueMingR.Tests.QuickItemOutlets");
            NativeCompanionCoreLifecycleChecks.PrepareHuman();
            var host = Get(context, "Companions"); var profiles = Get(host, "profiles");
            NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); Call(host, "Update", 0UL); return (bool)Get(profiles, "Ready"); });
            Require((bool)Call(host, "Create", "物品事务队友"), "create actual native item-test profile");
            NativeQuickItemChecks.Until(() => { Call(host, "Update", 0UL); return ((IEnumerable)Get(host, "Profiles")).Cast<CompanionProfile>().Any(p => p.Name == "物品事务队友"); });
            var id = ((IEnumerable)Get(host, "Profiles")).Cast<CompanionProfile>().Last().Id;
            Saved(host, id);
            Require((bool)Call(host, "Join", id), "join the actual native body: " + Get(host, "Feedback"));
            var human = Main.LocalPlayer; var companion = (Player)Call(host, "GetBody", id); var actor = Call(host, "Owned", companion);
            Clear(human); Clear(companion); Main.mouseItem = new Item();
            var nativeCursor = Main.mouseItem; Put(human.inventory, 58, ItemID.DirtBlock, 3); var native58 = human.inventory[58];
            var ownership = (ItemOperationOwnership)Get(host, "OwnerOperations");
            try
            {
                Put(human.inventory, 10, ItemID.Wood, 20); Put(companion.inventory, 12, ItemID.Wood, 3);
                Require(Gesture(host, id, true, CompanionInventoryArea.Inventory, 10, "Left"), "left takes full source into a projection");
                Require(human.inventory[10].stack == 20 && ((Item)Get(host, "CursorItem")).stack == 20 && Preview(host, id, true, CompanionInventoryArea.Inventory, 10).IsAir,
                    "real source remains untouched while preview is carried");
                Require(Gesture(host, id, false, CompanionInventoryArea.Inventory, 12, "Right"), "right projects one piece onto other body");
                Require(human.inventory[10].stack == 20 && companion.inventory[12].stack == 3, "partial chain has not committed either endpoint");
                Call(host, "CancelItemGesture");
                Require(human.inventory[10].stack == 20 && companion.inventory[12].stack == 3 && !(bool)Get(host, "HasItemGesture"), "cancel has no inverse/old-snapshot writes");
                Gesture(host, id, true, CompanionInventoryArea.Inventory, 10, "Left"); Gesture(host, id, false, CompanionInventoryArea.Inventory, 12, "Left");
                Require(human.inventory[10].IsAir && companion.inventory[12].stack == 23, "balanced stack commits exact 20+3 conservation");
                Saved(host, id);
                var current = ((IEnumerable)Get(host, "Profiles")).Cast<CompanionProfile>().Single(p => p.Id == id);
                var reloaded = (Player)Call(profiles, "LoadBody", current);
                Require(reloaded.inventory[12].stack == 23, "actual native archive stores the committed resource state");

                Put(human.inventory, 11, ItemID.Wood, 10);
                for (int i = 0; i < 3; i++) Require(Gesture(host, id, true, CompanionInventoryArea.Inventory, 11, "Right"), "same held right chain keeps taking one");
                Require(((Item)Get(host, "CursorItem")).stack == 3 && Preview(host, id, true, CompanionInventoryArea.Inventory, 11).stack == 7,
                    "right repeat direction does not alternate take and put");
                Call(host, "EndItemRepeat");
                Gesture(host, id, true, CompanionInventoryArea.Inventory, 11, "Right");
                Require(((Item)Get(host, "CursorItem")).stack == 2, "a new physical right press can put one back at the source");
                Gesture(host, id, true, CompanionInventoryArea.Inventory, 12, "Left");
                Require(human.inventory[11].stack == 8 && human.inventory[12].stack == 2, "right split balances once without duplicating a stack");
                Put(human.inventory, 13, ItemID.WoodenBow); human.inventory[13].Prefix(PrefixID.Demonic); Put(companion.inventory, 13, ItemID.WoodenBow);
                Gesture(host, id, true, CompanionInventoryArea.Inventory, 13, "Left"); Gesture(host, id, false, CompanionInventoryArea.Inventory, 13, "Left");
                Require((bool)Get(host, "HasItemGesture") && ((Item)Get(host, "CursorItem")).prefix == 0 && Preview(host, id, false, CompanionInventoryArea.Inventory, 13).prefix != 0,
                    "native different prefixes exchange rather than merge");
                Gesture(host, id, true, CompanionInventoryArea.Inventory, 14, "Left");
                Require(human.inventory[14].prefix == 0 && companion.inventory[13].prefix != 0, "prefix exchange closes with two distinct real items");

                Put(companion.armor, 0, ItemID.IronHelmet); Put(companion.inventory, 20, ItemID.WoodenBow);
                Gesture(host, id, false, CompanionInventoryArea.Armor, 0, "Left");
                Gesture(host, id, false, CompanionInventoryArea.Inventory, 20, "Left");
                Require(companion.armor[0].type == ItemID.IronHelmet && companion.inventory[20].type == ItemID.WoodenBow, "equipment-to-bag swap remains uncommitted while a bow is carried");
                Gesture(host, id, false, CompanionInventoryArea.Inventory, 21, "Left");
                Require(companion.armor[0].IsAir && companion.inventory[20].type == ItemID.IronHelmet && companion.inventory[21].type == ItemID.WoodenBow,
                    "helmet to bow swap then empty bag landing never puts bow into old helmet slot");

                Put(human.inventory, 20, ItemID.Wood, 4);
                Gesture(host, id, true, CompanionInventoryArea.Inventory, 20, "Left");
                Require(!Gesture(host, id, false, CompanionInventoryArea.Armor, 0, "Left") && companion.armor[0].IsAir, "wood cannot enter exact armor slot");
                Call(host, "CancelItemGesture");
                EquipExact(host, id, human, companion, ItemID.Worm, CompanionInventoryArea.Inventory, 54);
                Require(companion.inventory[54].bait > 0 && companion.inventory[54].ammo == 0, "native FitsAmmoSlot permits bait without simplified ammo-only logic");
                EquipExact(host, id, human, companion, ItemID.CopperCoin, CompanionInventoryArea.Inventory, 50);
                EquipExact(host, id, human, companion, ItemID.RedDye, CompanionInventoryArea.Dye, 0);
                EquipExact(host, id, human, companion, ItemID.Minecart, CompanionInventoryArea.Misc, 2);
                EquipExact(host, id, human, companion, ItemID.SlimySaddle, CompanionInventoryArea.Misc, 3);
                EquipExact(host, id, human, companion, ItemID.GrapplingHook, CompanionInventoryArea.Misc, 4);
                EquipExact(host, id, human, companion, ItemID.MagicLantern, CompanionInventoryArea.Misc, 1);
                EquipExact(host, id, human, companion, ItemID.Carrot, CompanionInventoryArea.Misc, 0);
                EquipExact(host, id, human, companion, ItemID.RedDye, CompanionInventoryArea.MiscDye, 4);
                bool extra = human.extraAccessory; human.extraAccessory = true; companion.extraAccessory = false;
                Put(human.inventory, 22, ItemID.Aglet); Gesture(host, id, true, CompanionInventoryArea.Inventory, 22, "Left");
                Require(!Gesture(host, id, false, CompanionInventoryArea.Armor, 8, "Left"), "target AI unlocks, not human unlocks, control its locked accessory slot");
                Call(host, "CancelItemGesture"); human.extraAccessory = extra;
                Put(companion.armor, 3, ItemID.CloudinaBottle); Put(human.inventory, 22, ItemID.CloudinaBottle);
                Gesture(host, id, true, CompanionInventoryArea.Inventory, 22, "Left");
                Require(!Gesture(host, id, false, CompanionInventoryArea.Armor, 4, "Left"), "second exact accessory slot rejects a duplicate"); Call(host, "CancelItemGesture");

                Put(human.inventory, 30, ItemID.Wood, 10); Gesture(host, id, true, CompanionInventoryArea.Inventory, 30, "Left");
                var work = new Harmony("JueMingR.Tests.CompanionItemWork");
                work.Patch(AccessTools.Method(typeof(Item), "Clone"), prefix: new HarmonyMethod(typeof(NativeCompanionItemGestureChecks), nameof(CountClone)));
                work.Patch(AccessTools.Method(profiles.GetType(), "Capture"), prefix: new HarmonyMethod(typeof(NativeCompanionItemGestureChecks), nameof(CaptureFailure)));
                try
                {
                    itemClones = nativeCaptures = 0;
                    for (int i = 0; i < 120; i++) Require((bool)Call(host, "ValidateItemGesture"), "stable finite draft stays valid");
                    Require(itemClones == 0 && nativeCaptures == 0 && (int)Get(Get(host, "itemDraft"), "DependencyCount") == 1,
                        "120 stable draft checks clone/serialize no item/player and inspect only its one dependency");
                }
                finally { work.UnpatchAll(work.Id); }
                Put(human.inventory, 31, ItemID.StoneBlock, 2);
                Require((bool)Call(host, "ValidateItemGesture"), "unrelated inventory changes do not cancel draft");
                human.inventory[30].stack--;
                Require(!(bool)Call(host, "ValidateItemGesture") && human.inventory[30].stack == 9 && !(bool)Get(host, "HasItemGesture"), "native consumption cancels projection without restoring old count");
                Gesture(host, id, true, CompanionInventoryArea.Inventory, 30, "Left"); human.inventory[30] = human.inventory[30].Clone();
                Require(!(bool)Call(host, "ValidateItemGesture"), "same-type same-stack Clone replacement invalidates stale object identity");
                Gesture(host, id, false, CompanionInventoryArea.Inventory, 12, "Left");
                Set(actor, "Generation", (long)Get(actor, "Generation") + 1);
                Require(!(bool)Call(host, "ValidateItemGesture"), "activation successor cannot finish older generation draft");
                Set(actor, "Generation", (long)Get(actor, "Generation") - 1);
                Gesture(host, id, false, CompanionInventoryArea.Inventory, 12, "Left"); companion.dead = true;
                Require(!(bool)Call(host, "ValidateItemGesture"), "death cancels held projection"); companion.dead = false;
                Gesture(host, id, true, CompanionInventoryArea.Inventory, 30, "Left");
                Main.mouseItem = new Item(); Main.mouseItem.SetDefaults(ItemID.Wood);
                Require(!Gesture(host, id, true, CompanionInventoryArea.Inventory, 31, "Left") && human.inventory[30].stack == 9,
                    "nonempty native cursor is read-only and never adopted by the panel"); Main.mouseItem = nativeCursor;

                Put(human.inventory, 32, ItemID.Wood, 15);
                Require(ownership.TryBeginStore(ownership.Session, 1UL << 32), "exact live Store owns source even after native lock clears");
                Require(!Gesture(host, id, true, CompanionInventoryArea.Inventory, 32, "Left"), "Store lease cannot be bypassed by custom UI");
                Require(Gesture(host, id, true, CompanionInventoryArea.Inventory, 31, "Left"), "unrelated slot stays manually usable under Store lease"); Call(host, "CancelItemGesture");
                ownership.FinishStore(ownership.Session, new ItemOperationResult(ItemOperationState.Completed));
                human.inventoryChestStack[32] = true; human.inventory[48] = new Item(); human.inventoryChestStack[48] = true;
                Require(!Gesture(host, id, true, CompanionInventoryArea.Inventory, 32, "Left"), "exact native stacking source remains protected");
                Require(Gesture(host, id, true, CompanionInventoryArea.Inventory, 31, "Left") && (bool)Call(host, "ValidateItemGesture") && human.inventoryChestStack[48],
                    "unrelated source remains usable under another native slot lock and pure validation does not clear an Air lock");
                Call(host, "CancelItemGesture"); human.inventoryChestStack[32] = human.inventoryChestStack[48] = false;
                long use = ownership.NewUseToken(); Require(ownership.TryBeginUse(ownership.Session, 32, use), "precise Use lease begins");
                Require(!Gesture(host, id, true, CompanionInventoryArea.Inventory, 32, "Left"), "Use protection remains effective"); ownership.EndUse(ownership.Session, use);
                var recovery = new ulong[5]; recovery[0] = 1UL << 32;
                Require(ownership.TryBeginRecovery(ownership.Session, recovery, 321), "precise Recovery lease begins");
                Require(!Gesture(host, id, true, CompanionInventoryArea.Inventory, 32, "Left"), "Recovery protection remains effective"); ownership.EndRecovery(ownership.Session, recovery, 321, false);

                // One target stack has two places; every other main-bag slot is
                // full. Dedicated coin/ammo slots are not Shift's destination.
                for (int i = 0; i < 50; i++) Put(companion.inventory, i, ItemID.StoneBlock, 9999);
                Put(companion.inventory, 40, ItemID.Wood, 9997); Put(human.inventory, 32, ItemID.Wood, 15);
                Require(Gesture(host, id, true, CompanionInventoryArea.Inventory, 32, "Shift"), "Shift accepts available partial capacity");
                Require(human.inventory[32].stack == 13 && companion.inventory[40].stack == 9999, "Shift commits two and preserves thirteen rather than claiming full transfer");
                Set(actor, "Ready", false); Put(human.inventory, 33, ItemID.DirtBlock, 4); human.inventory[34] = new Item();
                Gesture(host, id, true, CompanionInventoryArea.Inventory, 33, "Left"); Gesture(host, id, true, CompanionInventoryArea.Inventory, 34, "Left");
                Require(human.inventory[33].IsAir && human.inventory[34].stack == 4, "owner organization remains usable when AI is ineligible"); Set(actor, "Ready", true);
                ManualNotification(context, host, id, human, ownership);
                QuickTrash(host,profiles,id,actor,human,companion,ownership);

                var fault = new Harmony("JueMingR.Tests.CompanionItemCapture");
                fault.Patch(AccessTools.Method(profiles.GetType(), "Capture"), prefix: new HarmonyMethod(typeof(NativeCompanionItemGestureChecks), nameof(CaptureFailure)));
                try
                {
                    companion.inventory[41] = new Item(); Put(human.inventory, 35, ItemID.Wood, 7); failCapture = true;
                    Gesture(host, id, true, CompanionInventoryArea.Inventory, 35, "Left");
                    Require(Gesture(host, id, false, CompanionInventoryArea.Inventory, 41, "Left"), "post-commit save fault does not report the transfer as unexecuted");
                    Require(human.inventory[35].IsAir && companion.inventory[41].stack == 7 && (bool)Call(host, "HasPendingItemCapture", id), "capture failure retains unique latest body and explicit retry duty");
                    failCapture = false; Require((bool)Call(host, "RetrySave", id), "retry saves latest body only"); Saved(host, id);
                    Require(human.inventory[35].IsAir && companion.inventory[41].stack == 7, "save retry does not transfer a second time");
                }
                finally { failCapture = false; fault.UnpatchAll(fault.Id); }
                NetworkPublication(host, id, actor, human, companion);
                LifecycleIsolation(host, profiles, id, human, companion);
                Require(ReferenceEquals(Main.mouseItem, nativeCursor) && ReferenceEquals(human.inventory[58], native58) && native58.stack == 3,
                    "all panel operations preserve native mouse and legitimate slot58 identity/content");
                Console.WriteLine("PASS companion items: real host bounded drafts, exact body slots/rules, right-repeat direction, conservation/cancel/conflicts, Store/Use/Recovery, partial Shift, latest archive retry. No game/Steam/FPS claim.");
            }
            finally { Call(host, "CancelItemGesture"); Main.mouseItem = nativeCursor; companion.dead = false; Call(host, "Recall", id); }
        }

        private static void QuickTrash(object host,object profiles,Guid id,object actor,Player human,Player companion,ItemOperationOwnership ownership)
        {
            var patch=new Harmony("JueMingR.Tests.CompanionTrash");
            patch.Patch(typeof(Terraria.UI.ItemSlot).GetMethod("TryResearchingItem",Flags,null,new[]{typeof(Item).MakeByRefType(),typeof(bool)},null),
                prefix:new HarmonyMethod(typeof(NativeCompanionItemGestureChecks),nameof(ResearchObserved)));
            patch.Patch(AccessTools.Method(typeof(Item),"DeepClone"),prefix:new HarmonyMethod(typeof(NativeCompanionItemGestureChecks),nameof(BlockTrashAfterPrepare)));
            bool disabled=Terraria.UI.ItemSlot.Options.DisableQuickTrash,ctrl=Terraria.UI.ItemSlot.Options.DisableLeftShiftTrashCan;
            try
            {
                var map=host.GetType().GetMethod("QuickTrashRequested",Flags);
                Terraria.UI.ItemSlot.Options.DisableQuickTrash=false;Terraria.UI.ItemSlot.Options.DisableLeftShiftTrashCan=true;
                Require((bool)map.Invoke(null,new object[]{true,false}) && !(bool)map.Invoke(null,new object[]{false,true}),"current native Ctrl mapping takes one trash intent");
                Terraria.UI.ItemSlot.Options.DisableLeftShiftTrashCan=false;
                Require((bool)map.Invoke(null,new object[]{false,true}) && !(bool)map.Invoke(null,new object[]{true,false}),"current native Shift mapping replaces quick-transfer intent");
                Terraria.UI.ItemSlot.Options.DisableQuickTrash=true;
                Require(!(bool)map.Invoke(null,new object[]{true,true}),"disabled native quick trash does not enable physical Ctrl");
                Terraria.UI.ItemSlot.Options.DisableQuickTrash=false;
                trashResearch.Clear();trashGuard=ownership;
                Put(human.inventory,42,ItemID.Wood,8);human.trashItem=new Item();human.trashItem.SetDefaults(ItemID.StoneBlock);human.trashItem.stack=3;
                companion.trashItem=new Item();companion.trashItem.SetDefaults(ItemID.DirtBlock);var aiTrash=companion.trashItem;
                Set(actor,"Ready",false);
                Require(Trash(host,id,true,42) && human.inventory[42].IsAir && human.trashItem.type==ItemID.Wood && human.trashItem.stack==8,
                    "offline selected AI cannot block actual owner source-to-owner scalar trash");Set(actor,"Ready",true);
                Require(trashResearch.Count==2 && trashResearch[0].Item1==ItemID.Wood && trashResearch[1].Item1==ItemID.StoneBlock && trashResearch.All(x=>ReferenceEquals(x.Item2,human)),
                    "accepted native research adjacency sees detached source and replaced old trash once under human identity");
                Put(companion.inventory,42,ItemID.DirtBlock,6);var original=companion.inventory[42];var oldTrash=human.trashItem;
                original.favorited=true;Require(!Trash(host,id,false,42),"favorite AI source cannot be quick-trashed");original.favorited=false;
                Require(ownership.TryBeginDiscard(ownership.Session,49),"native owner discard guard acquired");
                Require(!Trash(host,id,false,42) && ReferenceEquals(human.trashItem,oldTrash) && ReferenceEquals(companion.inventory[42],original),"AI source respects occupied real owner trash");
                ownership.FinishDiscard(ownership.Session,new ItemOperationResult(ItemOperationState.Completed));
                blockTrashDuringPrepare=true;
                Require(!Trash(host,id,false,42) && ownership.DiscardBlocked && ReferenceEquals(human.trashItem,oldTrash) && companion.inventory[42].stack==6,
                    "final guard rejects occupancy acquired after preparation with both actual endpoints unchanged");
                ownership.FinishDiscard(ownership.Session,new ItemOperationResult(ItemOperationState.Completed));
                Require(trashResearch.Count==2,"failed preparations never research either endpoint");
                var fault=new Harmony("JueMingR.Tests.CompanionTrashCapture");
                fault.Patch(AccessTools.Method(profiles.GetType(),"Capture"),prefix:new HarmonyMethod(typeof(NativeCompanionItemGestureChecks),nameof(CaptureFailure)));
                try
                {
                    failCapture=true;Require(Trash(host,id,false,42),"post-commit capture fault retains accepted trash operation");
                    Require(companion.inventory[42].IsAir && human.trashItem.type==ItemID.DirtBlock && human.trashItem.stack==6 && ReferenceEquals(companion.trashItem,aiTrash),
                        "actual AI source is cleared into human scalar trash; AI trash remains its exact old object");
                    int researchCount=trashResearch.Count;failCapture=false;Call(host,"RetrySave",id);Saved(host,id);
                    var saved=((IEnumerable)Get(host,"Profiles")).Cast<CompanionProfile>().Single(p=>p.Id==id);
                    var reloaded=(Player)Call(profiles,"LoadBody",saved);
                    Require(reloaded.inventory[42].IsAir && trashResearch.Count==researchCount && human.trashItem.stack==6,
                        "archive reload and capture retry preserve debit without replaying old-trash destruction or research");
                }
                finally { failCapture=false;fault.UnpatchAll(fault.Id); }
            }
            finally { blockTrashDuringPrepare=false;trashGuard=null;patch.UnpatchAll(patch.Id);Terraria.UI.ItemSlot.Options.DisableQuickTrash=disabled;Terraria.UI.ItemSlot.Options.DisableLeftShiftTrashCan=ctrl; }
        }

        private static void LifecycleIsolation(object host, object profiles, Guid id, Player human, Player companion)
        {
            Require((bool)Call(host, "Create", "无关生命周期队友"), "create independent lifecycle profile");
            NativeQuickItemChecks.Until(() => { Call(profiles, "Pump"); return ((IEnumerable)Get(host, "Profiles")).Cast<CompanionProfile>().Any(p => p.Name == "无关生命周期队友"); });
            Guid other = ((IEnumerable)Get(host, "Profiles")).Cast<CompanionProfile>().Single(p => p.Name == "无关生命周期队友").Id;
            Require((bool)Call(host, "Join", other), "join distinct lifecycle body");
            var otherBody = (Player)Call(host, "GetBody", other); var otherActor = Call(host, "Owned", otherBody);
            Put(human.inventory, 47, ItemID.Wood, 3); Gesture(host, id, true, CompanionInventoryArea.Inventory, 47, "Left");
            otherBody.dead = true; Call(host, "ObserveDeath", otherActor);
            Require((bool)Get(host, "HasItemGesture") && (bool)Call(host, "ValidateItemGesture"), "unrelated AI death cannot cancel owner-only organization");
            Call(host, "CancelItemGesture");
            Gesture(host, id, false, CompanionInventoryArea.Inventory, 41, "Left");
            Require((bool)Call(host, "Recall", other) && (bool)Get(host, "HasItemGesture") && (bool)Call(host, "ValidateItemGesture"),
                "recalling unrelated B preserves the actual selected A resource draft");
            Call(host, "CancelItemGesture");
            Gesture(host, id, false, CompanionInventoryArea.Inventory, 41, "Left");
            Require((bool)Call(host, "Recall", id) && !(bool)Get(host, "HasItemGesture") && companion.inventory[41].stack == 7,
                "actual related actor retirement cancels its pending projection without writing old resources");
        }

        private static void ManualNotification(object context, object host, Guid id, Player human, ItemOperationOwnership ownership)
        {
            var items = Get(host, "OwnerItems"); var storage = Get(items, "Storage"); var world = Get(items, "World");
            var preparationType = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Items.ItemMaterialPreparation");
            var preparation = preparationType.GetProperty("Current", Flags).GetValue(null);
            var abandoned = (IList)Get(preparation, "abandonedDisplaced");
            var oldPending = GetOptional(storage, "pending");
            var oldPlayer = GetOptional(preparation, "guardPlayer"); var oldInventory = GetOptional(preparation, "guardInventory");
            long oldSession = (long)Get(preparation, "guardSession");
            Put(human.inventory, 36, ItemID.Wood, 5); Put(human.inventory, 37, ItemID.StoneBlock, 6); human.inventory[38] = new Item();
            var moved = human.inventory[36]; var untouched = human.inventory[37];
            var request = new StoreItemsRequest(ownership.Session, new[] { new ItemSlotObservation(36, new ItemIdentity(ItemID.Wood, 0), 5, moved.maxStack, 1, false) });
            var pendingType = storage.GetType().GetNestedType("Pending", Flags); var fragmentType = storage.GetType().GetNestedType("Fragment", Flags);
            var pending = Activator.CreateInstance(pendingType, Flags, null, new object[] { request, human, 5, 0UL }, null);
            var fragments = Array.CreateInstance(fragmentType, 58);
            fragments.SetValue(Activator.CreateInstance(fragmentType, true), 36); fragments.SetValue(Activator.CreateInstance(fragmentType, true), 37);
            Set(pending, "Fragments", fragments); Set(storage, "pending", pending);
            Set(preparation, "guardPlayer", human); Set(preparation, "guardInventory", human.inventory); Set(preparation, "guardSession", ownership.Session);
            abandoned.Add(moved); abandoned.Add(untouched);
            long revision = (long)Get(world, "Revision");
            try
            {
                Gesture(host, id, true, CompanionInventoryArea.Inventory, 36, "Left"); Gesture(host, id, true, CompanionInventoryArea.Inventory, 38, "Left");
                Require(human.inventory[36].IsAir && human.inventory[38].stack == 5 && fragments.GetValue(36) == null && fragments.GetValue(37) != null,
                    "actual panel commit retires only changed slot's storage-reception history");
                Require(!abandoned.Contains(moved) && abandoned.Contains(untouched) && (long)Get(world, "Revision") > revision,
                    "manual Clone replacement retires actual material return duty and invalidates observation, preserving unrelated duty");
            }
            finally
            {
                abandoned.Remove(moved); abandoned.Remove(untouched); Set(storage, "pending", oldPending);
                Set(preparation, "guardPlayer", oldPlayer); Set(preparation, "guardInventory", oldInventory); Set(preparation, "guardSession", oldSession);
            }
        }

        private static void NetworkPublication(object host, Guid id, object actor, Player human, Player companion)
        {
            int mode = Main.netMode; var socket = Netplay.Connection.Socket; var buffer = NetMessage.buffer[256];
            var previousConnection = GetOptional(actor, "Connection"); var frames = new List<byte[]>();
            var connectionType = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionConnection");
            var connection = Activator.CreateInstance(connectionType, Flags, null, new object[] { new Action<byte[]>(p => frames.Add(p)), new Action(() => { }) }, null);
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var client = new TcpClient();
            client.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port); var peer = listener.AcceptTcpClient();
            Netplay.Connection.Socket = new TcpSocket(client); NetMessage.buffer[256] = new MessageBuffer { whoAmI = 256 }; Set(actor, "Connection", connection);
            var patch = new Harmony("JueMingR.Tests.CompanionItemPublish");
            patch.Patch(AccessTools.Method(typeof(NetMessage), "SendPacketToServer"), prefix: new HarmonyMethod(typeof(NativeCompanionItemGestureChecks), nameof(CapturePrimary)));
            try
            {
                Main.netMode = 1; primaryPackets.Clear(); frames.Clear();
                human.armor[0] = new Item(); human.dye[0] = new Item(); human.miscEquips[4] = new Item(); human.miscDyes[4] = new Item();
                EquipExact(host, id, human, companion, ItemID.IronHelmet, CompanionInventoryArea.Armor, 0);
                // The helper places into AI. Move that exact equipment back to
                // the human counterpart and inspect both native framed streams.
                Gesture(host, id, false, CompanionInventoryArea.Armor, 0, "Left"); Gesture(host, id, true, CompanionInventoryArea.Armor, 0, "Left");
                Put(human.inventory, 23, ItemID.RedDye); Gesture(host, id, true, CompanionInventoryArea.Inventory, 23, "Left"); Gesture(host, id, true, CompanionInventoryArea.Dye, 0, "Left");
                Put(human.inventory, 23, ItemID.GrapplingHook); Gesture(host, id, true, CompanionInventoryArea.Inventory, 23, "Left"); Gesture(host, id, true, CompanionInventoryArea.Misc, 4, "Left");
                Put(human.inventory, 23, ItemID.RedDye); Gesture(host, id, true, CompanionInventoryArea.Inventory, 23, "Left"); Gesture(host, id, true, CompanionInventoryArea.MiscDye, 4, "Left");
                foreach (int slot in new[] { PlayerItemSlotID.Armor0, PlayerItemSlotID.Dye0, PlayerItemSlotID.Misc0 + 4, PlayerItemSlotID.MiscDye0 + 4 })
                    Require(primaryPackets.Any(p => p[2] == 5 && p[3] == human.whoAmI && BitConverter.ToInt16(p, 4) == slot), "human native region uses correct inventory packet slot " + slot);
                Require(frames.Any(p => p[2] == 5 && p[3] == companion.whoAmI && BitConverter.ToInt16(p, 4) == PlayerItemSlotID.Armor0) &&
                    primaryPackets.All(p => p[2] != 5 || p[3] == human.whoAmI), "AI changes use independent connection identity without impersonating human");
                Put(companion.inventory,42,ItemID.DirtBlock,5);primaryPackets.Clear();frames.Clear();failSend=true;
                Require(Trash(host,id,false,42) && companion.inventory[42].IsAir && human.trashItem.stack==5,"AI trash debit commits despite owner publication failure");
                Require(frames.Any(p=>p[2]==5 && p[3]==companion.whoAmI && BitConverter.ToInt16(p,4)==PlayerItemSlotID.Inventory0+42),"source inventory publication uses actual AI connection");
                failSend=false;Call(host,"FlushItemSync",true);
                Require(primaryPackets.Any(p=>p[2]==5 && p[3]==human.whoAmI && BitConverter.ToInt16(p,4)==PlayerItemSlotID.TrashItem) &&
                    companion.inventory[42].IsAir && human.trashItem.stack==5 && ((IList)Get(host,"pendingItemSync")).Count==0,
                    "retry publishes native human trash slot on main connection without replaying source debit");
                Put(human.inventory, 45, ItemID.Wood, 4); human.inventory[46] = new Item(); failSend = true;
                Gesture(host, id, true, CompanionInventoryArea.Inventory, 45, "Left");
                Require(Gesture(host, id, true, CompanionInventoryArea.Inventory, 46, "Left") && human.inventory[45].IsAir && human.inventory[46].stack == 4,
                    "send failure retains one committed local transfer");
                Require(((IList)Get(host, "pendingItemSync")).Count == 2, "failed sends retain exact two-slot publication duty");
                failSend = false; Call(host, "FlushItemSync", true);
                Require(((IList)Get(host, "pendingItemSync")).Count == 0 && human.inventory[46].stack == 4, "retry sends current slots without replaying transfer or waiting for nonexistent ACK");
            }
            finally
            {
                failSend = false; patch.UnpatchAll(patch.Id); Main.netMode = mode; Set(actor, "Connection", previousConnection);
                ((IDisposable)connection).Dispose(); Netplay.Connection.Socket = socket; NetMessage.buffer[256] = buffer;
                client.Dispose(); peer.Dispose(); listener.Stop();
            }
        }

        private static void EquipExact(object host, Guid id, Player owner, Player companion, int type, CompanionInventoryArea area, int slot)
        {
            Put(owner.inventory, 22, type);
            Require(Gesture(host, id, true, CompanionInventoryArea.Inventory, 22, "Left"), "take exact-category source " + type);
            Require(Gesture(host, id, false, area, slot, "Left") && !(bool)Get(host, "HasItemGesture"), "native-category exact target accepts " + type + " at " + area + ":" + slot + "; feedback=" + Get(host, "Feedback"));
        }
    }
}
