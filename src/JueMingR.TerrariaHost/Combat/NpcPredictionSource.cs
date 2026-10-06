using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed class NpcPredictionSource
    {
        internal readonly NpcPredictionCache Cache=new NpcPredictionCache();
        internal readonly PredictionTerrain Terrain=new PredictionTerrain();
        internal readonly Prediction.NativePredictionSession Native;
        private readonly Prediction.SegmentedNpcPrediction segmented=new Prediction.SegmentedNpcPrediction();
        private bool usingSegmented;
        private readonly RollingNpcPrediction rolling=new RollingNpcPrediction();
        private int observedCount,targetPlayer=-1;
        private long epoch;
        private bool ownsState;
        // Only the current synchronous sample's scalar outcome is retained.
        // This is not a second trajectory/cache and cannot turn point0 into a future.
        private NpcIdentity outcomeIdentity;
        private long outcomeTick=-1;
        private PredictionStop outcomeStop;
        private PredictionFailureLayer outcomeLayer;
        private int outcomeFuture;
        internal int OutcomeStyle {get;private set;}
        internal float OutcomePhase {get;private set;}
        internal PredictionStrategy OutcomeStrategy {get;private set;}
        internal bool EmptyFutureReason(NpcIdentity identity,long tick,out PredictionStop stop,out PredictionFailureLayer layer)
        {stop=outcomeStop;layer=outcomeLayer;return outcomeTick==tick && outcomeIdentity.Equals(identity) && outcomeFuture==0 && stop!=PredictionStop.None && CombatSelection.Valid(identity,identity.Session);}
        private void Outcome(NpcIdentity identity,long tick,NpcTrajectory path,PredictionFailureLayer layer)
        {outcomeIdentity=identity;outcomeTick=tick;outcomeStop=path?.Stop??PredictionStop.InvalidState;outcomeFuture=Math.Max(0,(path?.Count??0)-1);outcomeLayer=layer;OutcomeStrategy=path?.Strategy??PredictionStrategy.RollingConditional;var n=Main.npc[identity.Slot];OutcomeStyle=n.aiStyle;OutcomePhase=n.ai[0];}
        // Authentication alone never enables the expensive comparison route.
        // Only isolated comparison fixtures explicitly opt into native proof.
        internal NpcPredictionSource(Prediction.PredictionLaunchIdentity launch=null,bool exactComparison=false)
        {
            if(exactComparison){if(launch==null)throw new ArgumentNullException(nameof(launch));Native=new Prediction.NativePredictionSession(launch,Cache);}
            else PrepareRollingCode();
        }
        private static void PrepareRollingCode()
        {
            // Code preparation belongs to feature composition, once per owner.
            // Compile the bounded local kernels; do NOT sample Main, construct
            // forecasts or run a hidden warmup world. Startup is measured
            // separately; OFF world updates still do no prediction work.
            var types=new[]{typeof(NpcPredictionSource),typeof(NpcTrackingObservation),typeof(NpcPositionObservation),typeof(NpcCollisionRules),typeof(RollingNpcPrediction),typeof(NpcMotion),typeof(NpcHealth),typeof(PredictionTerrain),typeof(NpcTrajectory),
                typeof(MotionRect),typeof(NpcMotionState),typeof(NpcTrajectoryPoint),typeof(NpcBuffLayout),typeof(PredictionPlayers),typeof(NpcTrajectory).GetNestedType("PackedPoint",System.Reflection.BindingFlags.NonPublic),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcRollingMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcTargeting",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcPositionMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcGroundMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.PlayerHorizontalMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcGravityMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.FighterHorizontalMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcAquaticMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcAnchoredMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcWallMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcWormMotion",true)};
            foreach(var type in types)
            {
                foreach(var method in type.GetMethods(System.Reflection.BindingFlags.DeclaredOnly|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static))
                    if(!method.IsAbstract && !method.ContainsGenericParameters)System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(method.MethodHandle);
                foreach(var constructor in type.GetConstructors(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance))
                    System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(constructor.MethodHandle);
            }
            // ReadPlayer's mode query is a leaf on the default route, even
            // though the remaining NativePlayerMotion code is comparison-only.
            foreach(string name in new[]{"Conditional","Mechanism","SupportedHover"})
                System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(typeof(Prediction.NativePlayerMotion).GetMethod(name,System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).MethodHandle);
        }
        private readonly NpcMotionState[] states=new NpcMotionState[NpcPredictionCache.Capacity];
        private readonly bool[] visited=new bool[NpcPredictionCache.Capacity];
        private readonly bool[] motionSlots=new bool[NpcPredictionCache.Capacity],motionRoles=new bool[NpcPredictionCache.Capacity];
        private readonly int[] pending=new int[NpcPredictionCache.Capacity];
        private readonly MotionRect[] playerAreas=new MotionRect[255];
        private PredictionPlayers players;
        internal void Clear()
        {
            outcomeTick=-1;OutcomeStyle=0;OutcomePhase=0;OutcomeStrategy=PredictionStrategy.Model;
            Native?.ClearTarget();Cache.Clear();
            if(!ownsState)return;
            ownsState=false;segmented.Clear();rolling.Clear();usingSegmented=false;observedCount=0;targetPlayer=-1;epoch++;
            Prediction.AimLightTrace.Cache(null,Native?.Worker,"source-clear");Terrain.Reset();Array.Clear(states,0,states.Length);
        }
        internal void ObservePlayerRelocation(Player player)
        {
            Native?.ObservePlayerRelocation();
            if(player!=null && player.whoAmI==targetPlayer){outcomeTick=-1;rolling.Clear();Cache.Clear();epoch++;}
        }
        internal void ObserveNpcReset(NPC npc)
        {
            Native?.ObserveNpcReset(npc);
            if(npc!=null)RetireSlot(npc.whoAmI,null);
        }
        internal void ObserveNpcQueryUpdate(int slot,bool discontinuity)
        {
            Native?.ObserveNpcQueryUpdate(slot);
            if(discontinuity){RetireSlot(slot,null);return;}
            bool affected=usingSegmented && segmented.DependsOn(slot);
            for(int i=0;i<observedCount;i++)if(states[i].Identity.Slot==slot)affected=true;
            // A normal same-instance sync can change velocity/AI immediately;
            // revoke publication until the next completed sample, preserving
            // rolling/segment history and its relation identity. It is not a
            // birth, transform, teleport or permission to resurrect old data.
            if(affected){outcomeTick=-1;Cache.Clear();}
        }
        private void RetireSlot(int slot,NPC token)
        {
            bool affected=segmented.Reset(slot,token);
            for(int i=0;i<observedCount;i++)if(states[i].Identity.Slot==slot && (token==null || ReferenceEquals(states[i].Identity.Token,token)))affected=true;
            if(affected){outcomeTick=-1;rolling.Clear();Cache.Clear();epoch++;}
        }
        internal void Stop(){Native?.Stop();Clear();}
        internal void EndWorld(){Native?.DetachWorld();Clear();}
        internal void Prepare(NpcIdentity identity,long tick)
        {
            outcomeTick=-1;
            if(Cache.Required==0){Clear();return;}
            if(!CombatSelection.Valid(identity,identity.Session)){Clear();return;}
            ownsState=true;
            if(Prediction.SegmentedNpcPrediction.Family(identity.Type)!=0)
            {
                if(!usingSegmented){Native?.ClearTarget();rolling.Clear();observedCount=0;targetPlayer=-1;usingSegmented=true;}
                Native?.DiscardRetiredResult();
                var path=segmented.Prepare(identity,tick,Cache.Required);Outcome(identity,tick,path,PredictionFailureLayer.NpcMotion);Cache.Publish(path);Prediction.AimLightTrace.Cache(path,null,"segmented");return;
            }
            if(usingSegmented){Cache.Clear();rolling.Clear();usingSegmented=false;}
            if(Native!=null){Native.Prepare(identity,tick);return;}
            Terrain.Reset();Array.Clear(visited,0,visited.Length);Array.Clear(motionSlots,0,motionSlots.Length);int count=0,queued=1;pending[0]=identity.Slot;visited[identity.Slot]=motionSlots[identity.Slot]=true;
            for(int next=0;next<queued;next++)
            {
                int slot=pending[next];
                var n=Main.npc[slot];if(n==null || !n.active)continue;
                states[count++]=Read(n,identity.Session);
                NpcPositionObservation.Capture(n,ref states[count-1],identity.Session);
                int parent=states[count-1].ParentSlot,child=states[count-1].ChildSlot;
                if(motionSlots[slot])
                {
                    var owner=states[count-1].PositionOwner;if(owner.Token!=null){motionSlots[owner.Slot]=true;if(!visited[owner.Slot]){visited[owner.Slot]=true;pending[queued++]=owner.Slot;}}
                    if(parent>=0 && parent<Main.maxNPCs){motionSlots[parent]=true;if(!visited[parent]){visited[parent]=true;pending[queued++]=parent;}}
                    if(child>=0 && child<Main.maxNPCs){motionSlots[child]=true;if(!visited[child]){visited[child]=true;pending[queued++]=child;}}
                }
                int lifeOwner=states[count-1].Health.RealLife;
                if(lifeOwner>=0 && lifeOwner<Main.maxNPCs && !visited[lifeOwner]){visited[lifeOwner]=true;pending[queued++]=lifeOwner;}
            }
            // Sort only the required dependency chain by native slot order.
            for(int i=1;i<count;i++){var value=states[i];int j=i-1;while(j>=0 && states[j].Identity.Slot>value.Identity.Slot){states[j+1]=states[j];j--;}states[j+1]=value;}
            int selected=0;for(int i=0;i<count;i++)if(states[i].Identity.Equals(identity))selected=i;
            for(int i=0;i<count;i++){motionRoles[i]=motionSlots[states[i].Identity.Slot];if(motionRoles[i])NpcTrackingObservation.Capture(Main.npc[states[i].Identity.Slot],ref states[i],Terrain);}
            var current=Main.npc[identity.Slot];
            if(current.aiStyle==6 || current.aiStyle==37)for(int i=0;i<count;i++)if(states[i].ParentSlot<0){current=Main.npc[states[i].Identity.Slot];break;}
            var premiseEnv=new PredictionEnvironment{PlayerIndex=-1,Day=Main.dayTime,Remix=Main.remixWorld,WorldSurface=(float)Main.worldSurface};
            int oldPlayer=states[selected].PlayerIndex;
            premiseEnv.Graveyard=oldPlayer>=0 && oldPlayer<Main.maxPlayers && Main.player[oldPlayer]!=null && Main.player[oldPlayer].ZoneGraveyard;
            int target=NpcMotion.PlayerPremiseTarget(states[selected],premiseEnv);bool needsPlayer=false;
            // Only real movement consumers acquire a future-player prerequisite.
            // A required numbered target is never replaced by the local player.
            for(int i=0;i<count;i++)if(motionRoles[i] && NpcMotion.NeedsPlayerMotion(states[i],premiseEnv,Cache.Required,states,count))
            {if(!needsPlayer)target=NpcMotion.PlayerPremiseTarget(states[i],premiseEnv);needsPlayer=true;}
            var player=target>=0 && target<Main.maxPlayers?Main.player[target]:null;
            bool playerAlive=player!=null && player.active && !player.dead && !player.ghost;
            if(needsPlayer && !playerAlive){rolling.Clear();Outcome(identity,tick,null,PredictionFailureLayer.PlayerPremise);Cache.Publish(null);return;}
            targetPlayer=target;observedCount=count;
            int playerCount=0;bool samePlayers=players!=null,anyCorrupt=false;
            for(int i=0;i<Main.maxPlayers;i++)
            {
                var p=Main.player[i];if(p==null || !p.active)continue;var area=new MotionRect((int)p.position.X,(int)p.position.Y,p.width,p.height);
                samePlayers&=players!=null && playerCount<players.Count && PredictionPlayers.Same(area,players[playerCount]);playerAreas[playerCount++]=area;anyCorrupt|=!p.dead && p.ZoneCorrupt;
            }
            if(!samePlayers || players.Count!=playerCount)players=new PredictionPlayers(playerAreas,playerCount);
            var env=new PredictionEnvironment{BloodMoon=Main.bloodMoon,PlayerProtected=playerAlive && player.insideUnbreakableWalls,PlayerIndex=target,PlayerX=player==null?0:player.Center.X,PlayerY=player==null?0:player.Center.Y,PlayerWidth=player==null?0:player.width,PlayerHeight=player==null?0:player.height,PlayerWet=player!=null && player.wet,Wind=Main.windSpeedCurrent,Expert=Main.expertMode,Day=Main.dayTime,WorldWidth=Main.maxTilesX,GravityWorldSurface=Main.worldSurface,WorldSurface=(float)Main.worldSurface,Multiplayer=Main.netMode==1,Remix=Main.remixWorld,SlimeRain=Main.slimeRain,
                Enraged=player!=null && (player.position.Y<800 || player.position.Y>Main.worldSurface*16 || player.position.X>6400 && player.position.X<Main.maxTilesX*16-6400),
                MechQueenUp=NPC.mechQueen>=0 && NPC.mechQueen<Main.maxNPCs && Main.npc[NPC.mechQueen]!=null && Main.npc[NPC.mechQueen].active && Main.npc[NPC.mechQueen].type==127,Players=players,WorldHeight=Main.maxTilesY,RockLayer=(float)Main.rockLayer,PlayerDead=!playerAlive,PlayerIdleWithNegativeAggro=player!=null && player.itemAnimation==0 && player.aggro<0,Corrupt=player!=null && player.ZoneCorrupt,Crimson=player!=null && player.ZoneCrimson,AnyLivingCorrupt=anyCorrupt,SkyblockLowTiles=WorldGen.Skyblock.lowTiles,ClearLine=false,Eclipse=Main.eclipse,Graveyard=player!=null && player.ZoneGraveyard,GoodWorld=Main.getGoodWorld,InvasionType=Main.invasionType,SnowMoon=Main.snowMoon,DontStarve=Main.dontStarveWorld};
            var result=rolling.Prepare(states,count,selected,tick,Cache.Required,epoch,env,player==null?default(PredictionPlayerMotion):ReadPlayer(player),Terrain,motionRoles);
            Outcome(identity,tick,result,rolling.FailureLayer);Cache.Publish(result);
        }
        private static PredictionPlayerMotion ReadPlayer(Player p)
        {
            bool badHook=false;
            if(p.grappling!=null && p.grappling.Length>0 && p.grappling[0]>=0)
            {
                // Original forces consume grapCount, not unused array capacity
                // (whose trailing zero does not attach projectile slot zero).
                badHook=p.grapCount<1 || p.grapCount>p.grappling.Length;
                for(int i=0;i<Math.Min(p.grapCount,p.grappling.Length);i++)
                {int slot=p.grappling[i];var hook=slot>=0 && slot<Main.maxProjectiles?Main.projectile[slot]:null;if(hook==null || !hook.active || hook.owner!=p.whoAmI || hook.aiStyle!=7 || hook.ai==null || hook.ai.Length<1 || hook.ai[0]!=2)badHook=true;}
            }
            bool hover=p.mount.Active && (p.mount.Type==MountID.WitchBroom || p.mount.Type==5) && !p.CCed && !p.pulley && !p.shimmering && !p.tongued && (p.grappling==null || p.grappling.Length==0 || p.grappling[0]<0);
            // jumpSpeed/Height are shared native scratch, not this player's
            // completed observation. Derive ordinary values from owned effects.
            int jumpHeight=p.shimmerWet?23:p.wet?30:15;float jumpSpeed=p.shimmerWet?5.51f:p.wet?6.01f:5.01f;
            if(p.jumpBoost){jumpSpeed=Math.Max(jumpSpeed,6.51f);jumpHeight=Math.Max(jumpHeight,20);}
            if(p.wereWolf){jumpSpeed+=.2f;jumpHeight+=2;}if(p.moonLordLegs)jumpHeight++;
            jumpSpeed+=p.jumpSpeedBoost;if(p.sticky){jumpSpeed/=5;jumpHeight/=10;}if(p.dazed){jumpSpeed/=2;jumpHeight/=5;}
            return new PredictionPlayerMotion{X=p.position.X,Y=p.position.Y,Vx=p.velocity.X,Vy=p.velocity.Y,Width=p.width,Height=p.height,
                PlayerToken=p,PlayerIndex=p.whoAmI,ObservationMechanism=Prediction.NativePlayerMotion.Mechanism(p)|(p.ShouldFloatInWater?256:0)|(p.isLockedToATile?512:0),Rope=p.pulley,InvalidMechanism=badHook,
                Gravity=p.gravity,GravityDirection=p.gravDir,MaxFall=p.maxFallSpeed,Acceleration=hover?p.mount.Acceleration:p.runAcceleration,
                Slowdown=hover?.2f:p.runSlowdown,MaxSpeed=hover?p.mount.RunSpeed:p.maxRunSpeed,FastMaxSpeed=hover?p.mount.DashSpeed:p.chilled && p.oldStyleParkour?p.maxRunSpeed:p.accRunSpeed,
                WindSpeed=Main.windSpeedCurrent,WindPushed=p.windPushed && p.CanBePushedByWind(),TrackBoost=p.trackBoost,
                DashDelay=p.dashDelay,Wings=p.wingsLogic>0,CanFly=p.mount.CanFly(p),OnWrongGround=p.onWrongGround,PortalPhysics=p.PortalPhysicsEnabled,Jump=p.jump,JumpHeight=jumpHeight,JumpSpeed=jumpSpeed,
                IgnorePlatforms=p.gravDir<0 || p.mount.Active && (p.mount.Cart || p.mount.Type==12 || p.mount.Type==7 || p.mount.Type==8 || p.mount.Type==23 || p.mount.Type==44 || p.mount.Type==48 || p.mount.Type==55 && p.slideDir!=0) || p.GoingDownWithGrapple || p.pulley,
                IgnoreWater=p.ignoreWater,Merman=p.merman,Trident=p.trident,OnTrack=p.onTrack,Cart=p.mount.Active && p.mount.Cart,SkipSlope=p.mount.Active && p.mount.Type==48,SkipConveyor=p.grapCount>0 || p.pulley || p.shimmering || p.tongued || p.isLockedToATile,
                RidingTracks=p.IsRidingTracks,StepMount=p.mount.Active && (p.mount.Type==7 || p.mount.Type==8 || p.mount.Type==12 || p.mount.Type==44 || p.mount.Type==49),Carpet=p.carpetFrame!=-1,Grappled=p.grappling!=null && p.grappling.Length>0 && p.grappling[0]>=0,UnsupportedGeometry=p.shimmering || p.tongued || p.pulley || p.grappling!=null && p.grappling.Length>0 && p.grappling[0]>=0,FloatInWater=p.ShouldFloatInWater,StairFall=p.stairFall,GfxOffset=p.gfxOffY,StepSpeed=p.stepSpeed,
                Left=p.controlLeft,Right=p.controlRight,Up=p.controlUp,Down=p.controlDown,HoldJump=p.controlJump,ReleaseJump=p.releaseJump,AutoJump=p.autoJump,Hover=hover,Complex=Prediction.NativePlayerMotion.Conditional(p),WaterWalk=p.waterWalk || p.waterWalk2,LavaWalk=p.waterWalk};
        }
        internal static NpcMotionState Read(NPC n,long session)
        {
            if(n.ai==null || n.ai.Length<4 || n.localAI==null || n.localAI.Length<4 || n.immune==null || n.immune.Length<256 || n.buffType==null || n.buffType.Length!=20 || n.buffTime==null || n.buffTime.Length!=20 || n.buffImmune==null || n.buffImmune.Length<BuffID.Count)
                throw new NpcObservationFailure(CombatSelection.Identity(n,session));
            var health=new NpcHealthState{Regen=n.lifeRegen,RegenCount=n.lifeRegenCount,RealLife=n.realLife,DontTakeDamage=n.dontTakeDamage,Immortal=n.immortal,Immune255=n.immune[255],Defense=n.defense,DamageMultiplier=n.takenDamageMultiplier,LavaImmune=n.lavaImmune,FireImmune=n.buffImmune[24],ShimmerImmune=n.buffImmune[353],ShimmerTransparency=n.shimmerTransparency,ShimmerAction=n.SpawnedFromStatue || NPCID.Sets.ShimmerTransformToNPC[n.type]>=0 || NPCID.Sets.ShimmerTransformToItem[n.type]>=0 || NPCID.Sets.ShimmerTownTransform[n.type]};
            int confused=0,buffHash=0,expires=0,attached=0;
            for(int i=0;i<n.buffType.Length;i++)if(n.buffType[i]>0 && n.buffTime[i]>0)
            {
                int time=n.buffTime[i],type=n.buffType[i];
                if(type==BuffID.Confused){confused=Math.Max(confused,time);continue;}
                if(ReadTimer(ref health,type,time))continue;
                // Locked .8 UpdateNPC_BuffApplyDOTs: attached stacks require
                // projectile ownership; other listed DOTs still have a real
                // damage timer. Their unmodeled future damage is a condition,
                // while current life/known DOT settlement remains real. Stinky
                // and tipsy are non-motion only outside AI_007 town movement.
                if((type==120 || type==25) && n.aiStyle!=7)continue;
                if(type==151 || type==169 || type==183 || type==186 || type==189 || type==337 || type==344 || type==362 || type==30 || type==375 || type==395 || type==397){attached=Math.Max(attached,time);continue;}
                // Visual and defence-only effects do not change motion under
                // the explicit NoNewHits premise; their clocks need no model.
                if(type==119 || type==320 || type==36 || type==69 || type==72 || type==203 || type==310 || type==399 || type==400)continue;
                // Locked .8: Slow has no NPC flag/AI action; Dryad's Ward
                // changes defence only under the NoNewHits motion premise.
                if(type==BuffID.Slow || type==165)continue;
                buffHash=unchecked((buffHash*397^type)*397^time);expires=expires==0?time:Math.Min(expires,time);
            }
            int child=(n.aiStyle==6 || n.aiStyle==37) && n.ai[0]>0 && n.ai[0]<Main.maxNPCs?(int)n.ai[0]:-1;var linked=child>=0?Main.npc[child]:null;
            if(health.Fire>0 || health.Fire3>0 || n.buffType[19]!=0){health.Buffs.Captured=true;for(int i=0;i<20;i++)health.Buffs.Set(i,n.buffType[i],n.buffTime[i],Main.debuff[n.buffType[i]]);}
            return new NpcMotionState{UnmodeledDamageTicks=attached,NetOffsetX=n.netOffset.X,NetOffsetY=n.netOffset.Y,SmoothingRange=Main.multiplayerNPCSmoothingRange,ResetNetOffset=Main.netMode==2 || NPC.offSetDelayTime>0 || NPCID.Sets.NoMultiplayerSmoothingByType[n.type] || NPCID.Sets.NoMultiplayerSmoothingByAI[n.aiStyle] || n.townNPC && n.ai[0]==25,Friendly=n.friendly,ChildSlot=child,ChildIdentity=linked!=null && linked.active && linked.aiStyle==n.aiStyle?CombatSelection.Identity(linked,session):default(NpcIdentity),LavaSpeed=n.lavaMovementSpeed,ShimmerSpeed=n.shimmerMovementSpeed,Lava=n.lavaWet,Shimmer=n.shimmerWet,Health=health,Identity=CombatSelection.Identity(n,session),X=n.position.X,Y=n.position.Y,OldX=n.oldPosition.X,OldY=n.oldPosition.Y,StairFall=n.stairFall,Vx=n.velocity.X,Vy=n.velocity.Y,OldVx=n.oldVelocity.X,OldVy=n.oldVelocity.Y,Width=n.width,Height=n.height,Scale=n.scale,Style=n.aiStyle,Direction=n.direction,DirectionY=n.directionY,Target=n.target,
                CollisionPart=HasCollisionPart(n),Town=n.townNPC,HomeTileY=n.homeTileY,CritterTurns=NPCID.Sets.CritterThatCanTurnOnPlayers[n.type],Boss=n.boss,InactivityImmune=n.DoesntDespawnToInactivity() || n.townNPC,SpriteDirection=n.spriteDirection,SpawnedFromStatue=n.SpawnedFromStatue,ParentSlot=(n.aiStyle==6 || n.aiStyle==37) && n.ai[1]>0?(int)n.ai[1]:-1,TimeLeft=n.timeLeft,ConfusedTicks=confused,Life=n.life,LifeMax=n.lifeMax,BuffFingerprint=buffHash,BuffExpires=expires,WaterSpeed=n.waterMovementSpeed,HoneySpeed=n.honeyMovementSpeed,
                A0=n.ai[0],A1=n.ai[1],A2=n.ai[2],A3=n.ai[3],L0=n.localAI[0],L1=n.localAI[1],L2=n.localAI[2],L3=n.localAI[3],Active=n.active,NoGravity=n.noGravity,NoTileCollide=n.noTileCollide,Wet=n.wet,Honey=n.honeyWet,CollideX=n.collideX,CollideY=n.collideY,CanReceive=CombatSelection.Receives(n,true),CanHarm=!n.friendly && n.damage>0,NoContactDamage=n.damage==0,JustHit=n.justHit};
        }
        private static bool HasCollisionPart(NPC n)
        {
            int part=n.type==391?390:n.type==415?416:-1;if(part<0)return false;
            // Only these two native movement boxes depend on an attached live
            // part. Sample once on the game thread, never rescan for 120 steps.
            for(int i=0;i<Main.maxNPCs;i++)
            {var other=Main.npc[i];if(other!=null && other.active && other.type==part && other.ai[0]==n.whoAmI)return true;}
            return false;
        }
        private static bool ReadTimer(ref NpcHealthState h,int type,int time)
        {
            switch(type)
            {case 153:h.Shadow=time;break;case 20:h.Poison=time;break;case 24:h.Fire=time;break;case 39:h.Cursed=time;break;case 70:h.Venom=time;break;case 44:h.Frost=time;break;case 323:h.Fire3=time;break;case 324:h.Frost2=time;break;case 137:h.Slimed=time;break;case 204:h.Oil=time;break;case 398:h.Accelerated=time;break;case 103:h.WetBuff=time;break;case 353:h.ShimmerTicks=time;break;default:return false;}return true;
        }
    }
}
