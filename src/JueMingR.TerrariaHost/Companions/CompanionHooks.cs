using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JueMingR.TerrariaHost.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.Companions
{
    internal static partial class CompanionHooks
    {
        internal static readonly Harmony Harmony=new Harmony("JueMingR.Companions");
        private static HostCompanions host;
        private static bool installed;
        [ThreadStatic] internal static Player Spawning;
        [ThreadStatic] private static Scope current;
        [ThreadStatic] private static PlayerStep playerStep;
        internal static HostCompanions.Actor CurrentActor => current?.Actor;
        internal static HostCompanions.Actor SupplyActor(Player player) => host?.Owned(player);
        private static readonly MethodInfo enemyDamage=AccessTools.Method(typeof(Projectile),"Damage_EVP");
        private static readonly Func<Projectile, bool> canDamage=(Func<Projectile, bool>)Delegate.CreateDelegate(typeof(Func<Projectile, bool>),
            AccessTools.Method(typeof(Projectile),"Damage_CanDealDamage"));
        private static readonly MethodInfo hurtTile=AccessTools.Method(typeof(Player),"GetHurtTile"),touchDamage=AccessTools.Method(typeof(Player),"ApplyTouchDamage"),shimmer=AccessTools.Method(typeof(Player),"TryToShimmerUnstuck");
        internal static void Install(HostCompanions value)
        {
            host=value;
            if(installed)return;
            Patch(typeof(Player),"Update",nameof(PlayerBefore),nameof(PlayerAfter),nameof(PlayerFinal),nameof(UpdateGates),new[]{typeof(int)});
            Patch(typeof(Main),"UpdateWorld_NPCs",postfix:nameof(NpcWorldAfter));
            Patch(typeof(Main),"UpdateWorld_Items",postfix:nameof(PersonalItemsAfter));
            Patch(typeof(Main),"DrawPlayerChatBubbles",postfix:nameof(CompanionNamesAfter));
            Patch(typeof(Player),"ItemCheck",nameof(UseBefore),finalizer:nameof(ScopeFinal));
            InstallAchievements();
            Patch(typeof(Player),"PlaceThing",nameof(PlaceBefore),args:new[]{typeof(bool),typeof(Player.ItemCheckContext).MakeByRefType()});
            Patch(typeof(Player),"ItemCheck_UseWiringTools",nameof(PlaceBefore));
            Patch(typeof(Player),"UpdatePlacementPreview",nameof(PreviewBefore));
            Harmony.Patch(AccessTools.PropertyGetter(typeof(PlayerInput),"ShouldFastUseItem"),postfix:Hook(nameof(FastUse)));
            Patch(typeof(PlayerInput),"TryEndingFastUse",nameof(EndFastUse));
            Patch(typeof(Main),"LocalPlayerHasPendingInventoryActions",nameof(PendingInventory));
            Patch(typeof(Player),"Hurt",nameof(UseBefore),finalizer:nameof(ScopeFinal),transpiler:nameof(PaladinDamageReceiver));
            Patch(typeof(Player),"CheckDrowning",nameof(UseBefore),finalizer:nameof(ScopeFinal));
            Patch(typeof(Player),"UpdateLifeRegen",nameof(UseBefore),finalizer:nameof(ScopeFinal));
            Patch(typeof(Player),"UpdateBuffs",nameof(UseBefore),finalizer:nameof(ScopeFinal));
            Patch(typeof(Player),"UpdateLuck",nameof(UseBefore),finalizer:nameof(ScopeFinal));
            Patch(typeof(Terraria.DataStructures.MinionRespawner),"CollectMinionsFor",nameof(CollectMinions));
            Patch(typeof(Player),"KillMe",nameof(DeathBefore),finalizer:nameof(DeathFinal));
            Patch(typeof(Player),"KillMeForGood",nameof(PermanentDeath));
            Patch(typeof(Player),"Spawn",nameof(ItemRecallBefore),postfix:nameof(OwnerSpawnAfter),transpiler:nameof(SpawnLocals));
            Patch(typeof(Player),nameof(Player.Teleport),nameof(ActorTeleportBefore),nameof(ActorTeleportAfter),transpiler:nameof(TeleportPresentation));
            Patch(typeof(Player),nameof(Player.UpdateSceneMetrics),nameof(ActorSceneMetricsBefore));
            Patch(typeof(Player),nameof(Player.UpdateBiomes),nameof(ActorBiomesBefore),finalizer:nameof(ActorBiomesAfter));
            Patch(typeof(Player),"PickupItem",nameof(UseBefore),finalizer:nameof(ScopeFinal));
            Patch(typeof(Player),"GrabItems",nameof(UseBefore),finalizer:nameof(ScopeFinal));
            Patch(typeof(Player),"PurgeDD2EnergyCrystals",transpiler:nameof(TrackEnergyPurge));
            Patch(typeof(WorldItem),"UpdateItem",postfix:nameof(ItemOwner));
            Patch(typeof(Projectile),"AI",nameof(ProjectileBefore),finalizer:nameof(ScopeFinal),transpiler:nameof(DeferSharedProjectileHeal));
            Patch(typeof(Projectile), nameof(Projectile.NewProjectile), postfix: nameof(FishingProjectileSource), args: new[] {
                typeof(IEntitySource), typeof(float), typeof(float), typeof(float), typeof(float), typeof(int), typeof(int),
                typeof(float), typeof(int), typeof(float), typeof(float), typeof(float), typeof(NewProjectileModifier) });
            Patch(typeof(Projectile),"Update",transpiler:nameof(ProjectileSyncTail),args:new[]{typeof(int)});
            Patch(typeof(Projectile),"AI_075",transpiler:nameof(DrillTileGate));
            Patch(typeof(Projectile),"Damage",nameof(ProjectileBefore),nameof(ProjectileDamageAfter),nameof(ScopeFinal));
            Patch(typeof(Projectile),"Kill",nameof(ProjectileBefore),finalizer:nameof(ScopeFinal));
            // 1.4.6 can reclaim a projectile without Kill effects. Its own
            // packet 29 still needs the leased actor's native owner scope.
            var despawn = AccessTools.Method(typeof(Projectile), "Despawn", Type.EmptyTypes);
            if (despawn != null && !despawn.IsStatic && despawn.ReturnType == typeof(void))
                Harmony.Patch(despawn, prefix: Hook(nameof(ProjectileBefore)), finalizer: Hook(nameof(ScopeFinal)));
            Patch(typeof(NetMessage),"SendPacketToServer",nameof(Send));
            Patch(typeof(MessageBuffer),"GetData",nameof(Packet),nameof(PacketAfter));
            Patch(typeof(NPC),"GetPendingDamage",nameof(ObservationPending));
            Patch(typeof(NPC),"ResetNPCSlotData",nameof(ObservationSlotReset),args:new[]{typeof(int)});
            Patch(typeof(NPC),"NewNPCInstanceInSlot",nameof(NpcBirthBefore),finalizer:nameof(NpcBirthFinal));
            Patch(typeof(Projectile),"ResetNPCSlotData",nameof(PrimaryProjectileSlotReset),transpiler:nameof(NpcStaticImmunityWrites),args:new[]{typeof(int)});
            Patch(typeof(Player),"ResetNPCSlotData",nameof(PrimaryPlayerSlotReset),args:new[]{typeof(int)});
            Patch(typeof(Projectile),"ResetStaticImmunityMatching",transpiler:nameof(NpcStaticImmunityWrites));
            Patch(typeof(NPC),"HitEffect",nameof(ObservationVisual));
            Patch(typeof(NPC),"TransformVisuals",nameof(ObservationVisual));
            Patch(typeof(NPC),"OnSpawn",nameof(ObservationVisual));
            Patch(typeof(CombatText),"NewText",nameof(ObservationVisual),args:new[]{typeof(Rectangle),typeof(Color),typeof(int),typeof(bool),typeof(bool)});
            Patch(typeof(WorldGen),"saveToonWhilePlaying",nameof(Save));
            Patch(typeof(WorldGen),"SaveAndQuit",nameof(Leaving),args:new[]{typeof(Action)});

            Patch(typeof(NPC),"PushPendingDamage",postfix:nameof(PredictionPending));
            Patch(typeof(Projectile),"Damage_PVE_Inner",nameof(ProjectilePredictionBefore),finalizer:nameof(PredictionFinal),transpiler:nameof(NpcStaticImmunityWrites));
            Patch(typeof(Player),"ProcessHitAgainstNPC",nameof(MeleePredictionBefore),finalizer:nameof(PredictionFinal),transpiler:nameof(PredictionTarget));
            Patch(typeof(Player),"ApplyDamageToNPC",nameof(DirectPredictionBefore),finalizer:nameof(PredictionFinal),args:new[]{typeof(NPC),typeof(int),typeof(float),typeof(int),typeof(bool),typeof(PlayerNPCHitSource)});
            F5.CompanionCreationHooks.Install(Harmony);
            F5.CompanionPetPreviewHooks.Install(Harmony);
            InstallFurnitureHooks();
            InstallMobilityHooks();
            CompanionSupplyHooks.Install(value);
            InstallFishing();
            installed=true;
        }
        private static void Patch(Type t,string method,string prefix=null,string postfix=null,string finalizer=null,string transpiler=null,Type[] args=null)
        {
            var m=args==null?AccessTools.Method(t,method):AccessTools.Method(t,method,args);if(m==null)throw new MissingMethodException(t.FullName,method);
            Harmony.Patch(m,Hook(prefix),Hook(postfix),Hook(transpiler),Hook(finalizer));
        }
        private static HarmonyMethod Hook(string name){return name==null?null:new HarmonyMethod(typeof(CompanionHooks),name);}
        internal static void Uninstall(){UninstallFishing();CompanionSupplyHooks.Uninstall();Harmony.UnpatchAll(Harmony.Id);installed=false;host=null;}
        internal static void ProjectileBorn(Player player,Projectile projectile){host?.ProjectileBorn(player,projectile);}
        internal static bool ProjectileCanDealDamage(Projectile projectile) => canDamage(projectile);
        private static void FishingProjectileSource(IEntitySource __0, int __result)
        {
            if (host?.WantsFishingBackground != true || (uint)__result >= (uint)Main.maxProjectiles) return;
            CompanionThreatField.RecordShotSource(Main.projectile[__result], __0);
        }
        internal static Scope Enter(HostCompanions.Actor actor,Vector2 point,bool inventory=false){return new Scope(actor,point,inventory,false);}
        // NewNPCInstanceInSlot must still allocate the decoded instance and
        // preserve its generation. Only its global human-world slot cleanup is
        // omitted for this exact observation23 decode; real births stay native.
        private static bool ObservationSlotReset(int npcIndex)
        {return !(host?.DecodingObservationSlot(npcIndex)??false);}
        internal static Scope EnterWithPersonalItems(HostCompanions.Actor actor){return new Scope(actor,actor.Aim,false,true);}
        internal sealed class Scope : IDisposable
        {
            internal readonly HostCompanions.Actor Actor;private readonly Scope previous;
            internal bool PacketAccepted;
            internal readonly bool TileInteractionReady;
            private readonly int oldPlayer,x,y,tileX,tileY;private readonly bool map;private readonly Item mouse;private bool ended;
            private readonly int life,mana;private readonly bool inventory;private readonly ulong resources;
            private readonly NPC[] npcTable;
            private readonly uint[,] staticHits;
            private readonly IDisposable personalView;
            private readonly BiomeScope scene;
            internal int HumanIndex => previous == null ? oldPlayer : previous.HumanIndex;
            internal List<ShieldImpact> HumanShieldHits;
            internal Scope Root => previous == null ? this : previous.Root;
            private static ulong Resources(Player p){unchecked{ulong value=1469598103934665603;for(int i=0;i<p.inventory.Length;i++){var item=p.inventory[i];value=(value^(uint)(item.type*31+item.stack))*1099511628211;}return value;}}
            internal Scope(HostCompanions.Actor actor,Vector2 aim,bool inventory,bool personalItems,bool rawCursor=false)
            {
                this.inventory=inventory;life=actor.Body.statLife;mana=actor.Body.statMana;if(inventory)resources=Resources(actor.Body);
                staticHits=host.EnterObservedNpcHitState(Main.npc);npcTable=host.EnterNpcObservation();
                Actor=actor;previous=current;current=this;oldPlayer=Main.myPlayer;x=Main.mouseX;y=Main.mouseY;tileX=Player.tileTargetX;tileY=Player.tileTargetY;map=Main.mapFullscreen;mouse=Main.mouseItem;
                // Map input belongs to the human. Native actor consumers (for
                // example flails) see their own non-map input, while finally
                // restores the exact entering human or outer-scope value.
                Main.mapFullscreen=false;
                Main.myPlayer=actor.Body.whoAmI;Main.mouseX=(int)(aim.X-Main.screenPosition.X);Main.mouseY=(int)(!rawCursor && actor.Body.gravDir<0?Main.screenHeight-aim.Y+Main.screenPosition.Y:aim.Y-Main.screenPosition.Y);
                Player.tileTargetX=(int)(aim.X/16);Player.tileTargetY=(int)(aim.Y/16);
                // Mouse aim remains the real combat/prism point. An unknown
                // tile is not empty and cannot authorize native tile work;
                // retain its exact coordinate instead of moving the action.
                int tx=Player.tileTargetX,ty=Player.tileTargetY;
                TileInteractionReady=Main.tile!=null && tx>=0 && ty>=0 && tx<Main.tile.GetLength(0) && ty<Main.tile.GetLength(1) && Main.tile[tx,ty]!=null &&
                    (Main.netMode!=1 || Main.sectionManager!=null && Main.sectionManager.TileLoaded(tx,ty));
                if(personalItems)personalView=host.EnterPersonalItems(actor);
                // Scoped native effects (for example campfires, banners and
                // candles) must see the same actor as the borrowed player ID.
                scene=new BiomeScope(actor.Environment.Scene);
            }
            public void Dispose()
            {
                if(ended)return;ended=true;
                try{if(life!=Actor.Body.statLife || mana!=Actor.Body.statMana || inventory && resources!=Resources(Actor.Body))Actor.Dirty=true;}
                finally
                {
                    // Resource observation must not strand the temporary actor
                    // identity on failure. PlayerFinal repairs NPC/tile state,
                    // but cannot restore this scope's mouse or Send routing.
                    // Nested scopes return to their exact prior owner even if
                    // observation/cleanup fails; the exception still propagates.
                    try{try{personalView?.Dispose();}finally{try{host.LeaveNpcObservation(npcTable);}finally{host.LeaveObservedNpcHitState(staticHits);}}}
                    finally
                    {
                        scene.Dispose();
                        Main.myPlayer=oldPlayer;Main.mouseX=x;Main.mouseY=y;Player.tileTargetX=tileX;Player.tileTargetY=tileY;Main.mapFullscreen=map;Main.mouseItem=mouse;current=previous;
                        ApplyHumanShieldHits(HumanShieldHits);
                    }
                }
            }
        }
        private sealed class PlayerStep
        {
            internal HostCompanions.Actor Actor;
            internal int X,Y;
            internal NPC[] Npcs;
            internal uint[,] StaticHits;
            internal PlayerStep Previous;
            internal Scope EntryScope;
            internal List<ShieldImpact> HumanShieldHits;
            internal PlayerStep Root => Previous == null ? this : Previous.Root;
        }
        private static bool PlayerBefore(Player __instance,ref PlayerStep __state)
        {
            var a=host?.Owned(__instance);if(a==null)return true;
            if(!a.Ready || a.Closing || a.Faulted)return false;
            if(a.LastUpdate==Main.GameUpdateCount)return false;
            __state=new PlayerStep{Actor=a,X=Player.tileTargetX,Y=Player.tileTargetY,StaticHits=host.EnterObservedNpcHitState(Main.npc),Previous=playerStep,EntryScope=current};__state.Npcs=host.EnterNpcObservation();
            playerStep=__state;
            a.LastUpdate=Main.GameUpdateCount;
            a.Mobility.WingRunSampled=false;
            host.RefreshActorEnvironment(a);
            PurgeCompanionEnergy(__instance);
            host.Control(a);
            return true;
        }
        private static void PlayerAfter(Player __instance,PlayerStep __state)
        {var a=host?.Owned(__instance);if(__state!=null && a!=null && a.Ready && !a.Closing){
            // The native player pass precedes spawning and NPC AI. Refresh
            // again at the actual destination if native movement crossed out.
            host.RefreshActorEnvironment(a);
            host.AfterBody(a);}}
        private static void NpcWorldAfter(){host?.AdvanceNpcObservation();}
        private static void PersonalItemsAfter(){host?.AdvancePersonalItems();}
        private static void CompanionNamesAfter(){host?.DrawCompanionNames();}
        private static Exception PlayerFinal(Exception __exception,Player __instance,PlayerStep __state)
        {
            if(__state!=null)
            {
                try{try{host.LeaveNpcObservation(__state.Npcs);}finally{host.LeaveObservedNpcHitState(__state.StaticHits);}}
                finally{Player.tileTargetX=__state.X;Player.tileTargetY=__state.Y;playerStep=__state.Previous;}
                ApplyHumanShieldHits(__state.HumanShieldHits);
            }
            if(__exception!=null){var a=host?.Owned(__instance);if(a!=null){
                HostCompanions.RetireMobilityHand(a);host.CloseFaultedBody(a,__exception);return null;}}return __exception;
        }
        private static void UseBefore(Player __instance,MethodBase __originalMethod,ref Scope __state)
        {
            var a=host?.Owned(__instance);
            if(a==null || !a.Ready || a.Closing || __originalMethod.Name=="UpdateBuffs" && ReferenceEquals(a,CurrentActor))return;
            bool use=__originalMethod.Name=="ItemCheck";
            if(use && host.PrepareMobilityItem(a,out Vector2 mobilityAim))
            {
                __state=new Scope(a,mobilityAim,__instance.controlUseItem,false);
                return;
            }
            if(use)
            {
                var hits=host.EnterObservedNpcHitState(Main.npc);var table=host.EnterNpcObservation();
                try{host.PrepareUse(a);}
                finally{try{host.LeaveNpcObservation(table);}finally{host.LeaveObservedNpcHitState(hits);}}
            }
            // These native sky births reread raw mouse Y in Shoot. Keep that
            // point through the actual item consumer even in reverse gravity.
            bool rawCursor=use && HostSkyAttack.RawCursor(a.Supplies.RestoreSelection >= 0 ? __instance.HeldItem.type : a.Combat.Hand.Weapon?.type??0);
            __state=new Scope(a,a.Aim,__originalMethod.Name=="PickupItem" || use &&
                (__instance.controlUseItem || __instance.itemAnimation>0 || __instance.channel),
                __originalMethod.Name=="GrabItems",rawCursor);
        }
        private static void FastUse(ref bool __result){if(current!=null)__result=false;}
        private static bool EndFastUse(){return current==null;}
        private static bool PendingInventory(ref bool __result){if(current==null)return true;__result=false;return false;}
        private static bool PlaceBefore(Player __instance)
        {
            // ItemCheck calls PlaceThing even while idle with a plain weapon;
            // Extractinator reads tile.active before checking the use action.
            // A manual use has no chosen world tile. Even a dual-purpose
            // held item must not activate furniture under its aim or place
            // wire. The independent shoot, self-use and timer paths stay native.
            if(current==null || !ReferenceEquals(current.Actor.Body,__instance))return true;
            var hand=current.Actor.Supplies;
            return current.TileInteractionReady && hand.HeldOnce==null && hand.RestoreSelection<0 && current.Actor.Mobility.PreviousSelection<0;
        }
        private static bool PreviewBefore(Player __instance)
        {
            // Native placement preview is the human cursor's shared UI state,
            // not permission granted by borrowing an actor's item identity.
            // Its Framing path can materialize unknown footprint/anchor cells.
            // Actual known-cell actions retain the original PlaceThing checks.
            return current==null || !ReferenceEquals(current.Actor.Body,__instance);
        }
        private static bool DrillTileWork(Projectile projectile)
        {
            return current==null || projectile.type!=Terraria.ID.ProjectileID.LaserDrill || current.TileInteractionReady ||
                !ReferenceEquals(host?.ProjectileActor(projectile),current.Actor);
        }
        private static IEnumerable<CodeInstruction> DrillTileGate(IEnumerable<CodeInstruction> instructions)
        {
            var code=instructions.ToList();var tile=AccessTools.Field(typeof(Main),"tile");var tx=AccessTools.Field(typeof(Player),"tileTargetX");var ty=AccessTools.Field(typeof(Player),"tileTargetY");
            var reads=Enumerable.Range(0,code.Count-2).Where(i=>code[i].LoadsField(tile) && code[i+1].LoadsField(tx) && code[i+2].LoadsField(ty)).ToArray();
            if(reads.Length!=1)throw new InvalidOperationException("companion-drill ABI: expected one target tile read");
            int read=reads[0],gate=-1;Label end=default(Label);var localAI=AccessTools.Field(typeof(Projectile),"localAI");
            for(int i=Math.Max(0,read-48);i+5<read;i++)
            {
                if(code[i].opcode!=OpCodes.Ldarg_0 || !code[i+1].LoadsField(localAI) || code[i+2].opcode!=OpCodes.Ldc_I4_1 || code[i+3].opcode!=OpCodes.Ldelem_R4 ||
                    code[i+4].opcode!=OpCodes.Ldc_R4 || !Equals(code[i+4].operand,0f) || (code[i+5].opcode!=OpCodes.Bne_Un && code[i+5].opcode!=OpCodes.Bne_Un_S))continue;
                var target=(Label)code[i+5].operand;int after=code.FindIndex(read+1,c=>c.labels.Contains(target));
                if(after<0 || gate>=0)throw new InvalidOperationException("companion-drill ABI: ambiguous tile work boundary");
                gate=i+6;end=target;
            }
            if(gate<0)throw new InvalidOperationException("companion-drill ABI: localAI tile work branch missing");
            // Skip only the known 445 tile/steering block. Native channel-end,
            // cooldown and the common position/timeLeft/heldProj/item-time tail
            // still execute; never synthesize that tail or renew life ourselves.
            var load=new CodeInstruction(OpCodes.Ldarg_0);load.labels.AddRange(code[gate].labels);code[gate].labels.Clear();
            load.blocks.AddRange(code[gate].blocks);code[gate].blocks.Clear();
            code.InsertRange(gate,new[]{load,new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(CompanionHooks),nameof(DrillTileWork))),new CodeInstruction(OpCodes.Brfalse,end)});
            return code;
        }
        private static Exception ScopeFinal(Exception __exception,Scope __state){__state?.Dispose();return __exception;}
        // KillMeForGood otherwise deletes Main.playerPathName and clears the human
        // ActivePlayerFileData even inside an AI identity scope. Match the exact
        // owned body, leave native drops/penalty intact, and retire its own archive.
        private static bool PermanentDeath(Player __instance)
        {var a=host?.Owned(__instance);if(a==null)return true;a.PermanentDeathPending=true;return false;}
        private static void DeathBefore(Player __instance,ref Scope __state)
        {
            var a=host?.Owned(__instance);
            if(a==null || !a.Ready || a.Closing || __instance.creativeGodMode || __instance.dead)return;
            host.BeforeFishingDeath(a);
            __state=Enter(a,a.Aim);
            host.PublishSlotsBeforeDeath(a);
        }
        private static bool CollectMinions(Terraria.DataStructures.MinionRespawner __instance,Player player)
        {
            var a=host?.Owned(player);
            if(a==null || ReferenceEquals(__instance,a.Minions))return true;
            // Vanilla's static respawner remains the human's death record.
            // Dispatch the actual Collect call, including its native Clear,
            // into this activation's temporary record instead of swapping it.
            a.Minions.CollectMinionsFor(player);return false;
        }
        private static Exception DeathFinal(Exception __exception,Player __instance,Scope __state)
        {try{if(__state!=null)host.ObserveDeath(__state.Actor);}finally{__state?.Dispose();}return __exception;}
        private static void ProjectileBefore(Projectile __instance,ref Scope __state)
        {if(host==null || !host.HasActors)return;var a=host.ProjectileActor(__instance);if(a!=null && a.Ready && !a.Closing)__state=Enter(a,host.ProjectileAim(a,__instance));}
        private static void ProjectileDamageAfter(Projectile __instance)
        {
            if(host==null || !host.HasActors || !__instance.active || !__instance.hostile || __instance.damage<=0 || !ProjectileCanDealDamage(__instance))return;
            var hitbox=__instance.Damage_GetHitbox();
            foreach(var a in host.ActiveActors)
            {
                if(!a.Ready || a.Closing || !a.Body.active || a.Body.dead || !__instance.Colliding(hitbox,a.Body.Hitbox))continue;
                using(Enter(a,a.Aim))enemyDamage.Invoke(__instance,new object[]{hitbox});
            }
        }
        private static int ProjectileSyncOwner(Projectile p)
        {
            var a=host?.ProjectileActor(p);
            return a!=null && a.Ready && !a.Closing?p.owner:Main.myPlayer;
        }
        private static void ProjectileSyncSend(int msgType,int remoteClient,int ignoreClient,Terraria.Localization.NetworkText text,int number,float number2,float number3,float number4,int number5,int number6,int number7)
        {
            var p=number>=0 && number<Main.projectile.Length?Main.projectile[number]:null;
            var a=p==null?null:host?.ProjectileActor(p);
            // Only the real native Update tail's original SendData call owns
            // this lease. AI/Damage scopes have already ended; do not replay
            // the native netSpam/extraUpdates/flag transitions ourselves.
            if(msgType==27 && a!=null && a.Ready && !a.Closing)
            {using(Enter(a,host.ProjectileAim(a,p)))NetMessage.SendData(msgType,remoteClient,ignoreClient,text,number,number2,number3,number4,number5,number6,number7);}
            else NetMessage.SendData(msgType,remoteClient,ignoreClient,text,number,number2,number3,number4,number5,number6,number7);
        }
        private static IEnumerable<CodeInstruction> ProjectileSyncTail(IEnumerable<CodeInstruction> input)
        {
            var code=input.ToList();var owner=AccessTools.Field(typeof(Main),"myPlayer");var update=AccessTools.Field(typeof(Projectile),"netUpdate2");
            var send=AccessTools.Method(typeof(NetMessage),"SendData");int gates=0,sends=0;
            for(int i=code.Count-1;i>=0;i--)
            {
                if(code[i].Calls(send)){code[i].operand=AccessTools.Method(typeof(CompanionHooks),nameof(ProjectileSyncSend));sends++;}
                if(!code[i].LoadsField(owner))continue;
                bool tail=false;for(int j=i+1;j<Math.Min(code.Count,i+12);j++)if(code[j].LoadsField(update))tail=true;
                if(!tail)continue;
                var load=new CodeInstruction(OpCodes.Ldarg_0);load.labels.AddRange(code[i].labels);load.blocks.AddRange(code[i].blocks);
                code[i]=load;code.Insert(i+1,new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(CompanionHooks),nameof(ProjectileSyncOwner))));gates++;
            }
            if(gates!=1 || sends!=1)throw new InvalidOperationException("Companion projectile sync tail ABI: "+gates+"/"+sends);
            return code;
        }
        private static bool Send(byte[] data)
        {
            if(Main.netMode==1 && current==null && data!=null && data.Length>=3 && data[2]==38)host?.HumanPassword(data);
            if(Main.netMode==1 && current==null && data!=null && data.Length>=3 && data[2]==154)host?.PrimaryPingSent();

            if(Main.netMode!=1 || current==null || current.Actor.Connection==null)return true;
            int length=data[0]|data[1]<<8;var copy=new byte[length];Buffer.BlockCopy(data,0,copy,0,length);
            var connection = current.Actor.Connection;
            bool teleport = copy[2] == 65;
            if (teleport) host.ObserveWormholeSend(current.Actor, copy, null);
            if (copy.Length == 4 && copy[2] == 73) host.PrepareChatItemTeleportRequest(current.Actor, copy[3]);
            try { current.PacketAccepted = connection.TrySend(copy); }
            catch (CompanionConnection.SendQueueFullException) when (teleport && !connection.IsRelay)
            {
                host.ObserveWormholeSend(current.Actor, copy, false);
                throw;
            }
            if (teleport) host.ObserveWormholeSend(current.Actor, copy, current.PacketAccepted);
            if(current.PacketAccepted)host.RecordActorPacket(current.Actor,copy);
            return false;
        }
        private static bool Packet(MessageBuffer __instance,int start,int length,ref int messageType,out bool __state)
        {
            __state = false;
            if (Main.netMode != 1 || host == null) return true;
            if (host.Decoding == null && length > 0)
            {
                byte kind = __instance.readBuffer[start];
                __state = __instance.whoAmI == 256 &&
                    (kind == 3 && Netplay.Connection?.State == 1 || kind == 7 && Netplay.Connection?.State == 3);
                if (kind == 154 && host.PrimaryPingReceived()) { messageType = 154; return false; }
            }
            if (host.AcceptPrimary(__instance.readBuffer,start,length)) return true;
            messageType = __instance.readBuffer[start];
            if (Netplay.Connection != null) Netplay.Connection.TimeOutTimer = 0;
            return false;
        }
        private static void PacketAfter(MessageBuffer __instance,int start,int length,int messageType,bool __runOriginal,bool __state)
        {
            if (!__runOriginal || Main.netMode != 1 || host == null || host.ReadingObservation || host.Decoding != null) return;
            if (messageType == 3 && __state) host.HumanAccepted();
            if (messageType == 7 && __state) host.HumanWorldArrived();
            if (messageType == 66 && length >= 4 && BitConverter.ToInt16(__instance.readBuffer,start+2)>0)
                host.SharedHealApplied(__instance.readBuffer[start+1]);
            if (messageType == 23 || messageType == 28 || messageType == 54 || messageType == 60) host.PrimaryNpcApplied(__instance.readBuffer,start,length);
        }
        private static bool ObservationPending(ref int damage,ref bool phaseChange)
        {if(host==null || !host.ReadingObservation)return true;damage=0;phaseChange=false;return false;}
        private static bool ObservationVisual(){return host==null || !host.ReadingObservation;}
        private static bool Save()
        {
            if(current==null)return true;
            // KillMe's request saves this role through its own archive. The
            // human save outlet remains fully active outside this exact scope.
            host.ObserveDeath(current.Actor);return false;
        }
        private static void Leaving(){host?.OnSessionEnded();}
        private static void ItemOwner(WorldItem __instance)
        {
            if(host==null || !host.HasActors || Main.netMode!=0 || !__instance.active || __instance.instanced || __instance.timeToKeepReservation>0 || __instance.shimmerTime>0 || __instance.grabDelayTime>0 && __instance.grabDelayPlayer==255)return;
            // Single-player vanilla reserves every drop for its one human.
            // Use the same native nearest/space eligibility for the additional
            // local bodies. No inventory or world stack is copied here.
            int chosen=Main.myPlayer;float best=Single.MaxValue;
            var real=Main.LocalPlayer;
            if(real!=null && real.active && !real.dead && real.CanPullItem(__instance,real.ItemSpace(__instance)))best=Math.Abs(real.Center.X-__instance.Center.X)+Math.Abs(real.Center.Y-__instance.position.Y-__instance.height);
            foreach(var a in host.ActiveActors)
            {
                var p=a.Body;if(!a.Ready || a.Closing || !p.active || p.dead || __instance.grabDelayTime>0 && __instance.grabDelayPlayer==p.whoAmI)continue;
                if(!p.CanPullItem(__instance,p.ItemSpace(__instance)))continue;
                float distance=Math.Abs(p.Center.X-__instance.Center.X)+Math.Abs(p.Center.Y-__instance.position.Y-__instance.height);
                if(distance<best){best=distance;chosen=p.whoAmI;}
            }
            __instance.playerIndexTheItemIsReservedFor=chosen;
        }
        private static int OwnerForUse(Player p){return host?.Owned(p)!=null?p.whoAmI:Main.myPlayer;}
        private static void ReplaceOwner(List<CodeInstruction> code,int at)
        {
            var prior=code[at];var load=new CodeInstruction(OpCodes.Ldarg_0);load.labels.AddRange(prior.labels);load.blocks.AddRange(prior.blocks);code[at]=load;code.Insert(at+1,new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(CompanionHooks),nameof(OwnerForUse))));
        }
        private static IEnumerable<CodeInstruction> UpdateGates(IEnumerable<CodeInstruction> input)
        {
            var c=input.ToList();var field=AccessTools.Field(typeof(Main),"myPlayer");int falls=0,lavas=0,contacts=0,expiredBuffs=0;
            for(int i=c.Count-1;i>=0;i--)if(c[i].LoadsField(field))
            {
                int branch=-1;for(int j=i+1;j<Math.Min(c.Count,i+7);j++)if(c[j].opcode.FlowControl==FlowControl.Cond_Branch && c[j].operand is Label){branch=j;break;}
                if(branch<0)continue;var label=(Label)c[branch].operand;int end=c.FindIndex(branch+1,x=>x.labels.Contains(label));if(end<=branch)continue;
                bool damage=false,fall=false,lava=false,other=false,delBuff=false,buffTime=false;
                for(int j=branch+1;j<end;j++)
                {
                    var f=c[j].operand as FieldInfo;if(f!=null){fall|=f.Name=="fallStart2";lava|=f.Name=="lavaTime";buffTime|=f.Name=="buffTime";}
                    var method=c[j].operand as MethodInfo;if(method==null)continue;string name=method.Name;
                    damage|=name=="Hurt";
                    delBuff|=name=="DelBuff";
                    other|=name=="CopyInto" || name=="ResetControls" || name=="SmartCursorLookup" || name=="ItemCheck" || name=="Update_NPCCollision" || name=="InputTextSign" || name=="InputTextChest";
                }
                // These two original body-only blocks own fall and lava
                // damage. The much larger local UI/NPC block is never widened.
                if(damage && !other && (fall || lava)){ReplaceOwner(c,i);if(fall)falls++;if(lava)lavas++;}
                else if(delBuff && buffTime && !damage && !other){ReplaceOwner(c,i);expiredBuffs++;}
            }
            foreach(var instruction in c)if(instruction.Calls(AccessTools.Method(typeof(Player),"GrappleMovement"))){instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(CompanionHooks),nameof(ContactThenGrapple));contacts++;}
            // UpdateBuffs decrements locally; this separate original Update
            // block owns DelBuff/compaction after expiry. Neither is replayed.
            if(falls!=1 || lavas!=1 || contacts!=1 || expiredBuffs!=1)throw new InvalidOperationException("Companion native survival layout changed: fall="+falls+" lava="+lavas+" contact="+contacts+" buffs="+expiredBuffs);
            ExtendActiveBodyUpdate(c);
            ExtendMobilityUpdate(c);
            return c;
        }
        private static void ContactThenGrapple(Player p)
        {
            var a=host?.Owned(p);
            if(a!=null && a.Ready && !a.Closing && !p.dead)
                using(Enter(a,a.Aim))
                {p.Update_NPCCollision();if(!p.shimmering){var tile=(Collision.HurtTile)hurtTile.Invoke(p,null);if(tile.type>=0)touchDamage.Invoke(p,new object[]{tile.type,tile.x,tile.y});}shimmer.Invoke(p,null);p.GrappleMovement();}
            else p.GrappleMovement();
        }
        private static Player SpawnLocal(){return Spawning??Main.LocalPlayer;}
        private static void ActorTeleportBefore(Player __instance,out Vector2 __state) => __state=__instance.position;
        private static void ActorTeleportAfter(Player __instance,Vector2 __state,int Style,int extraInfo,bool __runOriginal)
        {
            if (!__runOriginal || !(host?.Owned(__instance) is HostCompanions.Actor actor)) return;
            if (__instance.position != __state) host.RefreshTeleportedEnvironment(actor);
            if (ReferenceEquals(CurrentActor,actor)) host.ObserveChatItemTeleport(actor,__state,Style,extraInfo);
        }
        private static void OwnerSpawnAfter(Player __instance, PlayerSpawnContext context, bool __runOriginal)
        {
            // An original recall/revival may place the owner at the very
            // same coordinates. It still starts a new observed trail segment.
            if (__runOriginal && !__instance.dead && (context == PlayerSpawnContext.RecallFromItem ||
                context == PlayerSpawnContext.ReviveFromDeath || context == PlayerSpawnContext.TeamSwap))
                host?.ObserveOwnerWarp(__instance);
        }
        private static bool ItemRecallBefore(Player __instance,PlayerSpawnContext context)
        {
            var actor=current?.Actor;
            if(context!=PlayerSpawnContext.RecallFromItem || actor==null || Spawning!=null ||
                !ReferenceEquals(actor.Body,__instance))return true;
            host.RecallFromItem(actor,current.HumanIndex);
            return false;
        }

        private static int PresentationPlayer(){return current?.HumanIndex??Main.myPlayer;}
        private static IEnumerable<CodeInstruction> TeleportPresentation(IEnumerable<CodeInstruction> instructions)
        {
            var code=instructions.ToList();
            int camera=code.FindIndex(c=>c.Calls(AccessTools.Method(typeof(Main),nameof(Main.StartCameraTransitionForPlayerTeleport))));
            var local=AccessTools.Field(typeof(Main),nameof(Main.myPlayer));
            int check=camera<0?-1:code.FindLastIndex(camera,c=>c.LoadsField(local));
            if(check<0 || code[check+1].opcode!=OpCodes.Bne_Un && code[check+1].opcode!=OpCodes.Bne_Un_S)
                throw new InvalidOperationException("Companion teleport camera receiver changed.");
            // Teleport checks local identity specifically for the camera,
            // black fade and map redraw here. Physics, spectating, pressure
            // plates and the decoder's ACK keep their native owning player.
            code[check].opcode=OpCodes.Call;code[check].operand=AccessTools.Method(typeof(CompanionHooks),nameof(PresentationPlayer));
            return code;
        }
        private static IEnumerable<CodeInstruction> SpawnLocals(IEnumerable<CodeInstruction> input)
        {var getter=AccessTools.PropertyGetter(typeof(Main),"LocalPlayer");foreach(var c in input){if(c.Calls(getter))c.operand=AccessTools.Method(typeof(CompanionHooks),nameof(SpawnLocal));yield return c;}}
        internal static bool AllowItemPacket(HostCompanions.Actor a,byte[] packet){return host!=null && host.AcceptItemPacket(a,packet);}
    }
}
