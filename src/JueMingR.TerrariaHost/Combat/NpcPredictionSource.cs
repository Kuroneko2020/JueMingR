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
        private readonly NpcMotionState[] states=new NpcMotionState[NpcPredictionCache.Capacity];
        private readonly bool[] visited=new bool[NpcPredictionCache.Capacity];
        internal void Clear(){Cache.Clear();Terrain.Reset();Array.Clear(states,0,states.Length);}
        internal void Prepare(NpcIdentity identity,long tick)
        {
            if(!CombatSelection.Valid(identity,identity.Session)){Clear();return;}
            Array.Clear(visited,0,visited.Length);int slot=identity.Slot,count=0;
            while(slot>=0 && slot<Main.maxNPCs && !visited[slot])
            {
                var n=Main.npc[slot];if(n==null || !n.active)break;
                visited[slot]=true;states[count++]=Read(n,identity.Session);
                slot=states[count-1].ParentSlot;
            }
            // Sort only the required dependency chain by native slot order.
            for(int i=1;i<count;i++){var value=states[i];int j=i-1;while(j>=0 && states[j].Identity.Slot>value.Identity.Slot){states[j+1]=states[j];j--;}states[j+1]=value;}
            int selected=0;for(int i=0;i<count;i++)if(states[i].Identity.Equals(identity))selected=i;
            var current=Main.npc[identity.Slot];int target=current.target;
            if(target<0 || target>=Main.maxPlayers || Main.player[target]==null || !Main.player[target].active || Main.player[target].dead)target=Main.myPlayer;
            var player=Main.player[target];
            var env=new PredictionEnvironment{PlayerX=player.Center.X,PlayerY=player.Center.Y,PlayerWidth=player.width,PlayerHeight=player.height,PlayerWet=player.wet,Wind=Main.windSpeedCurrent,Expert=Main.expertMode,Day=Main.dayTime,WorldWidth=Main.maxTilesX,WorldSurface=(float)Main.worldSurface,Multiplayer=Main.netMode==1,Remix=Main.remixWorld,SlimeRain=Main.slimeRain,
                Enraged=player.position.Y<800 || player.position.Y>Main.worldSurface*16 || player.position.X>6400 && player.position.X<Main.maxTilesX*16-6400,
                ClearLine=Collision.CanHitLine(current.position,current.width,current.height,player.position,player.width,player.height)};
            Cache.Prepare(states,count,selected,tick,env,Terrain);
        }
        internal static NpcMotionState Read(NPC n,long session)
        {
            int confused=0,buffHash=0,expires=0;for(int i=0;i<n.buffType.Length;i++)if(n.buffType[i]>0 && n.buffTime[i]>0){if(n.buffType[i]==BuffID.Confused)confused=Math.Max(confused,n.buffTime[i]);buffHash=unchecked((buffHash*397^n.buffType[i])*397^n.buffTime[i]);expires=expires==0?n.buffTime[i]:Math.Min(expires,n.buffTime[i]);}
            return new NpcMotionState{Identity=CombatSelection.Identity(n,session),X=n.position.X,Y=n.position.Y,Vx=n.velocity.X,Vy=n.velocity.Y,OldVx=n.oldVelocity.X,OldVy=n.oldVelocity.Y,Width=n.width,Height=n.height,Scale=n.scale,Style=n.aiStyle,Direction=n.direction,DirectionY=n.directionY,Target=n.target,
                ParentSlot=(n.aiStyle==6 || n.aiStyle==37) && n.ai[1]>0?(int)n.ai[1]:-1,TimeLeft=n.timeLeft,ConfusedTicks=confused,Life=n.life,LifeMax=n.lifeMax,BuffFingerprint=buffHash,BuffExpires=expires,WaterSpeed=n.waterMovementSpeed,HoneySpeed=n.honeyMovementSpeed,
                A0=n.ai[0],A1=n.ai[1],A2=n.ai[2],A3=n.ai[3],L0=n.localAI[0],L1=n.localAI[1],L2=n.localAI[2],L3=n.localAI[3],Active=n.active,NoGravity=n.noGravity,NoTileCollide=n.noTileCollide,Wet=n.wet,Honey=n.honeyWet,CollideX=n.collideX,CollideY=n.collideY,CanReceive=CombatSelection.Receives(n,true),CanHarm=!n.friendly && n.damage>0,JustHit=n.justHit};
        }
    }
}
