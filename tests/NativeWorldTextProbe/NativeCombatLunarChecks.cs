using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatLunarChecks
    {
        const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        static Projectile watched;static int flags,expanded;static Vector2 flagCenter;
        static void AiDone(Projectile __instance)
        {
            if(!ReferenceEquals(watched,__instance))return;
            if(watched.ai[1]==-1 && watched.width==10 && flags==0){flags++;flagCenter=watched.Center;Require(watched.numUpdates==0,"near flag occurs on native countdown zero");}
            if(watched.width==140 && expanded==0){expanded++;Require(flags==1 && watched.numUpdates==-1 && watched.velocity==Vector2.Zero,"only next AI expands/freezes the lunar body");Require(Vector2.Distance(watched.Center,flagCenter+new Vector2(10,0))<.001f,"flag AI still translates before next AI freezes");}
        }
        internal static void Run(object context)
        {
            var assembly=Get(context,"Combat").GetType().Assembly;var sky=assembly.GetType("JueMingR.TerrariaHost.Combat.HostSkyAttack",true);var vertical=sky.GetMethod("Vertical",F);
            foreach(int start in new[]{0,1,15})foreach(float gravity in new[]{0f,.1f})foreach(float cap in new[]{0f,16f})
            {
                var motion=new AttackMotion(15,gravity,start,6,10,10,1000,maxFallSpeed:cap);float x=0,y=0,vx=0,vy=15;
                for(int k=1;k<=180;k++){motion.Advance(ref x,ref y,ref vx,ref vy,k);float end=(float)vertical.Invoke(null,new object[]{motion,15f,k});Require(Math.Abs(y-end)<.02f,"closed sky endpoint agrees with actual Advance order/start/cap");}
            }
            // The trigger geometry and the damage receiver are deliberately
            // different. This trajectory never intersects the ten-pixel body.
            var offset2=Main.npc[2].netOffset;var offset3=Main.npc[3].netOffset;
            try{foreach(string scene in new[]{"near","wet-flight","wet-expanded","boundary","outside","offset","los","side","immune","direct","terrain"})Scene(context,assembly,sky,scene);}
            finally{Main.npc[2].netOffset=offset2;Main.npc[3].netOffset=offset3;watched=null;}
        }
        static void Scene(object context,Assembly assembly,Type sky,string scene)
        {
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3570,0,0);p.position=new Vector2(700,646);p.ResetEffects();
            foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;
            for(int x=5;x<115;x++)for(int y=5;y<78;y++)Main.tile[x,y].ClearEverything();Main.tileSolid[1]=true;
            var target=Main.npc[2];target.SetDefaults(NPCID.Zombie);target.netOffset=Vector2.Zero;target.whoAmI=2;target.active=true;target.width=target.height=20;target.Center=new Vector2(scene=="side"?1100:scene=="direct"?945:995,scene=="outside"?680:scene=="direct"?600:scene=="terrain"?670:650);target.life=target.lifeMax=10000;target.knockBackResist=0;target.aiStyle=-1;target.noGravity=target.noTileCollide=true;
            if(scene=="boundary")target.Center=new Vector2(1000,660);
            if(scene=="offset")target.netOffset=new Vector2(80,0);
            if(scene=="terrain"){target.Center=new Vector2(1030,670);for(int x=55;x<70;x++)NativeToolsChecks.Tile(x,40,1);}
            if(scene=="los")for(int x=50;x<100;x++)NativeToolsChecks.Tile(x,39,1);
            // The expanded-only pool is below the entire ten-pixel flight,
            // but inside the next AI's actual 140-pixel body.
            if(scene=="wet-flight")for(int x=55;x<65;x++)for(int y=36;y<39;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(0);}
            if(scene=="wet-expanded")for(int x=57;x<63;x++)for(int y=39;y<41;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(0);}
            if(scene=="side"){var other=Main.npc[3];other.SetDefaults(NPCID.Zombie);other.whoAmI=3;other.active=true;other.width=other.height=20;other.Center=new Vector2(995,650);other.life=10000;other.noGravity=other.noTileCollide=true;}
            var id=new NpcIdentity(1,target,2,0,target.type,target.netID);
            var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,new NpcMotionState{Identity=id,X=target.position.X-(scene=="side"?t*15:0),Y=target.position.Y,Width=20,Height=20,NetOffsetX=target.netOffset.X,NetOffsetY=target.netOffset.Y,Active=true,CanReceive=true,NoTileCollide=true})).ToArray();
            var path=new NpcTrajectory(id,Main.GameUpdateCount,1,PredictionAssumption.None,PredictionStop.None,points,points.Length);
            int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(scene=="terrain"?995:900,600),new Vector2(scene=="terrain"?0:10,scene=="terrain"?10:0),645,50,0,0,0,scene=="terrain"?610:1000);watched=Main.projectile[slot];
            if(scene=="immune")watched.localNPCImmunity[2]=-1;
            var terrain=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");
            if(scene.StartsWith("wet-"))
            {
                var query=new object[]{watched.Center.X,watched.Center.Y,10,10,false,(byte)0,false};
                Require((bool)terrain.GetType().GetMethod("ProjectileWet",F).Invoke(terrain,query) && (bool)query[4]==(scene=="wet-flight"),"wet fixture distinguishes flight sensor from later expanded sensor");
            }
            var obstacles=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),F,null,new object[]{p,watched,path,0,true,false},null);
            var motion=new AttackMotion(10,0,0,6,10,10,watched.timeLeft,AttackConfidence.Representative,componentBirthLimit:true);
            var contact=(AttackContact)sky.GetMethod("LunarReplay",F).Invoke(null,new object[]{new Vector2(1000,scene=="terrain"?610:1000),watched.Center,watched.velocity,motion,path,0,terrain,obstacles,0});
            var small=AttackIntercept.Replay(1000,1000,900,600,10,0,motion,path,0);Require(scene=="direct" || small==null,"small flight rectangle alone cannot explain lunar damage");
            var audit=new Harmony("JueMingR.Tests.LunarState");flags=expanded=0;
            if(scene=="near")audit.Patch(typeof(Projectile).GetMethod("AI",F),postfix:new HarmonyMethod(typeof(NativeCombatLunarChecks),nameof(AiDone)));
            int first=0;bool nativeWet=false;
            try{for(int t=1;t<=16 && watched.active;t++){NativeQuickItemChecks.BeginWorldStep();if(scene=="side")target.position.X=points[t].Bounds.X;watched.Update(slot);nativeWet|=watched.wet;if(target.life<10000 && first==0)first=t;}}
            finally{audit.UnpatchAll(audit.Id);}
            Console.WriteLine("LUNAR scene="+scene+" model="+(contact==null?"none":contact.Tick+"/"+contact.Subupdate)+" nativeDamage="+first+" wet="+nativeWet+" flag="+flags+" expanded="+expanded+" width="+watched.width+" ai="+watched.ai[0]+"/"+watched.ai[1]);
            if(scene.StartsWith("wet-")){Require(nativeWet,"bounded lunar wet fixture reaches actual native liquid detection for "+scene);Require(contact==null,"unsupported lunar liquid must retire Contact for "+scene);}
            else Require((contact!=null)==(first>0) && (contact==null || contact.Tick==first),"lunar native receive clock agrees for "+scene);
            Require(first==0 || watched.localNPCImmunity[2]==-1,"native lunar local immunity prevents a second receive from its stationary explosion");
            if(scene=="near")Require(first>0 && flags==1 && expanded==1,"strict near-distance trigger then next AI damage is observed");
            watched=null;
        }
    }
}
