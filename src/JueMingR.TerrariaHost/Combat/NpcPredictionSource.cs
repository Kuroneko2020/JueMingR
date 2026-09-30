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
        internal NpcPredictionSource(Prediction.PredictionLaunchIdentity launch=null)
        {if(launch!=null)Native=new Prediction.NativePredictionSession(launch,Cache);}
        private readonly NpcMotionState[] states=new NpcMotionState[NpcPredictionCache.Capacity];
        private readonly bool[] visited=new bool[NpcPredictionCache.Capacity];
        private readonly int[] pending=new int[NpcPredictionCache.Capacity];
        private readonly MotionRect[] playerAreas=new MotionRect[255];
        private PredictionPlayers players;
        internal void Clear(){Native?.ClearTarget();segmented.Clear();usingSegmented=false;Cache.Clear();Terrain.Reset();Array.Clear(states,0,states.Length);}
        internal void Stop(){Native?.Stop();Clear();}
        internal void EndWorld(){Native?.DetachWorld();Clear();}
        internal void Prepare(NpcIdentity identity,long tick)
        {
            if(Cache.Required==0){Clear();return;}
            if(!CombatSelection.Valid(identity,identity.Session)){Clear();return;}
            if(Prediction.SegmentedNpcPrediction.Family(identity.Type)!=0)
            {
                if(!usingSegmented){Native?.ClearTarget();usingSegmented=true;}
                Native?.DiscardRetiredResult();
                Cache.Publish(segmented.Prepare(identity,tick,Cache.Required));return;
            }
            if(usingSegmented){Cache.Clear();usingSegmented=false;}
            if(Native!=null){Native.Prepare(identity,tick);return;}
            Array.Clear(visited,0,visited.Length);int count=0,queued=1;pending[0]=identity.Slot;visited[identity.Slot]=true;
            for(int next=0;next<queued;next++)
            {
                int slot=pending[next];
                var n=Main.npc[slot];if(n==null || !n.active)continue;
                states[count++]=Read(n,identity.Session);
                int parent=states[count-1].ParentSlot,child=states[count-1].ChildSlot;
                if(parent>=0 && parent<Main.maxNPCs && !visited[parent]){visited[parent]=true;pending[queued++]=parent;}
                if(child>=0 && child<Main.maxNPCs && !visited[child]){visited[child]=true;pending[queued++]=child;}
            }
            // Sort only the required dependency chain by native slot order.
            for(int i=1;i<count;i++){var value=states[i];int j=i-1;while(j>=0 && states[j].Identity.Slot>value.Identity.Slot){states[j+1]=states[j];j--;}states[j+1]=value;}
            int selected=0;for(int i=0;i<count;i++)if(states[i].Identity.Equals(identity))selected=i;
            var current=Main.npc[identity.Slot];
            if(current.aiStyle==6 || current.aiStyle==37)for(int i=0;i<count;i++)if(states[i].ParentSlot<0){current=Main.npc[states[i].Identity.Slot];break;}
            int target=current.target;if(target<0 || target>=Main.maxPlayers || Main.player[target]==null)target=Main.myPlayer;
            var player=Main.player[target];
            int playerCount=0;bool samePlayers=players!=null,anyCorrupt=false;
            for(int i=0;i<Main.maxPlayers;i++)
            {
                var p=Main.player[i];if(p==null || !p.active)continue;var area=new MotionRect((int)p.position.X,(int)p.position.Y,p.width,p.height);
                samePlayers&=players!=null && playerCount<players.Count && PredictionPlayers.Same(area,players[playerCount]);playerAreas[playerCount++]=area;anyCorrupt|=!p.dead && p.ZoneCorrupt;
            }
            if(!samePlayers || players.Count!=playerCount)players=new PredictionPlayers(playerAreas,playerCount);
            for(int i=0;i<count;i++)states[i].TargetNoAggro=player.npcTypeNoAggro[states[i].Identity.Type];
            var env=new PredictionEnvironment{BloodMoon=Main.bloodMoon,PlayerProtected=!player.dead && !player.ghost && player.insideUnbreakableWalls,PlayerIndex=target,PlayerX=player.Center.X,PlayerY=player.Center.Y,PlayerWidth=player.width,PlayerHeight=player.height,PlayerWet=player.wet,Wind=Main.windSpeedCurrent,Expert=Main.expertMode,Day=Main.dayTime,WorldWidth=Main.maxTilesX,WorldSurface=(float)Main.worldSurface,Multiplayer=Main.netMode==1,Remix=Main.remixWorld,SlimeRain=Main.slimeRain,
                Enraged=player.position.Y<800 || player.position.Y>Main.worldSurface*16 || player.position.X>6400 && player.position.X<Main.maxTilesX*16-6400,
                MechQueenUp=NPC.mechQueen>=0 && NPC.mechQueen<Main.maxNPCs && Main.npc[NPC.mechQueen]!=null && Main.npc[NPC.mechQueen].active && Main.npc[NPC.mechQueen].type==127,Players=players,WorldHeight=Main.maxTilesY,RockLayer=(float)Main.rockLayer,PlayerDead=player.dead,PlayerIdleWithNegativeAggro=player.itemAnimation==0 && player.aggro<0,Corrupt=player.ZoneCorrupt,Crimson=player.ZoneCrimson,AnyLivingCorrupt=anyCorrupt,SkyblockLowTiles=WorldGen.Skyblock.lowTiles,ClearLine=false,Eclipse=Main.eclipse,Graveyard=player.ZoneGraveyard,GoodWorld=Main.getGoodWorld,InvasionType=Main.invasionType};
            Cache.Prepare(states,count,selected,tick,env,Terrain);
        }
        internal static NpcMotionState Read(NPC n,long session)
        {
            var health=new NpcHealthState{Regen=n.lifeRegen,RegenCount=n.lifeRegenCount,RealLife=n.realLife,DontTakeDamage=n.dontTakeDamage,Immortal=n.immortal,Immune255=n.immune[255],Defense=n.defense,DamageMultiplier=n.takenDamageMultiplier,LavaImmune=n.lavaImmune,FireImmune=n.buffImmune[24],ShimmerImmune=n.buffImmune[353],ShimmerTransparency=n.shimmerTransparency,ShimmerAction=n.SpawnedFromStatue || NPCID.Sets.ShimmerTransformToNPC[n.type]>=0 || NPCID.Sets.ShimmerTransformToItem[n.type]>=0 || NPCID.Sets.ShimmerTownTransform[n.type]};
            int confused=0,buffHash=0,expires=0;
            for(int i=0;i<n.buffType.Length;i++)if(n.buffType[i]>0 && n.buffTime[i]>0)
            {
                int time=n.buffTime[i],type=n.buffType[i];
                if(type==BuffID.Confused){confused=Math.Max(confused,time);continue;}
                if(ReadTimer(ref health,type,time))continue;
                // Visual and defence-only effects do not change motion under
                // the explicit NoNewHits premise; their clocks need no model.
                if(type==119 || type==320 || type==36 || type==69 || type==72 || type==203 || type==310 || type==399 || type==400)continue;
                buffHash=unchecked((buffHash*397^type)*397^time);expires=expires==0?time:Math.Min(expires,time);
            }
            int child=(n.aiStyle==6 || n.aiStyle==37) && n.ai[0]>0 && n.ai[0]<Main.maxNPCs?(int)n.ai[0]:-1;var linked=child>=0?Main.npc[child]:null;
            if(health.Fire>0 || health.Fire3>0 || n.buffType[19]!=0){health.Buffs.Captured=true;for(int i=0;i<20;i++)health.Buffs.Set(i,n.buffType[i],n.buffTime[i],Main.debuff[n.buffType[i]]);}
            return new NpcMotionState{NetOffsetX=n.netOffset.X,NetOffsetY=n.netOffset.Y,SmoothingRange=Main.multiplayerNPCSmoothingRange,ResetNetOffset=Main.netMode==2 || NPC.offSetDelayTime>0 || NPCID.Sets.NoMultiplayerSmoothingByType[n.type] || NPCID.Sets.NoMultiplayerSmoothingByAI[n.aiStyle] || n.townNPC && n.ai[0]==25,Friendly=n.friendly,ChildSlot=child,ChildIdentity=linked!=null && linked.active && linked.aiStyle==n.aiStyle?CombatSelection.Identity(linked,session):default(NpcIdentity),LavaSpeed=n.lavaMovementSpeed,ShimmerSpeed=n.shimmerMovementSpeed,Lava=n.lavaWet,Shimmer=n.shimmerWet,Health=health,Identity=CombatSelection.Identity(n,session),X=n.position.X,Y=n.position.Y,OldX=n.oldPosition.X,OldY=n.oldPosition.Y,StairFall=n.stairFall,Vx=n.velocity.X,Vy=n.velocity.Y,OldVx=n.oldVelocity.X,OldVy=n.oldVelocity.Y,Width=n.width,Height=n.height,Scale=n.scale,Style=n.aiStyle,Direction=n.direction,DirectionY=n.directionY,Target=n.target,
                Boss=n.boss,InactivityImmune=n.DoesntDespawnToInactivity() || n.townNPC,SpriteDirection=n.spriteDirection,SpawnedFromStatue=n.SpawnedFromStatue,ParentSlot=(n.aiStyle==6 || n.aiStyle==37) && n.ai[1]>0?(int)n.ai[1]:-1,TimeLeft=n.timeLeft,ConfusedTicks=confused,Life=n.life,LifeMax=n.lifeMax,BuffFingerprint=buffHash,BuffExpires=expires,WaterSpeed=n.waterMovementSpeed,HoneySpeed=n.honeyMovementSpeed,
                A0=n.ai[0],A1=n.ai[1],A2=n.ai[2],A3=n.ai[3],L0=n.localAI[0],L1=n.localAI[1],L2=n.localAI[2],L3=n.localAI[3],Active=n.active,NoGravity=n.noGravity,NoTileCollide=n.noTileCollide,Wet=n.wet,Honey=n.honeyWet,CollideX=n.collideX,CollideY=n.collideY,CanReceive=CombatSelection.Receives(n,true),CanHarm=!n.friendly && n.damage>0,JustHit=n.justHit};
        }
        private static bool ReadTimer(ref NpcHealthState h,int type,int time)
        {
            switch(type)
            {case 153:h.Shadow=time;break;case 20:h.Poison=time;break;case 24:h.Fire=time;break;case 39:h.Cursed=time;break;case 70:h.Venom=time;break;case 44:h.Frost=time;break;case 323:h.Fire3=time;break;case 324:h.Frost2=time;break;case 137:h.Slimed=time;break;case 204:h.Oil=time;break;case 398:h.Accelerated=time;break;case 103:h.WetBuff=time;break;case 353:h.ShimmerTicks=time;break;default:return false;}return true;
        }
    }
}
