using System;
using System.IO;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeCombatAttackMechanismChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatRollingBoundaryChecks
    {
        internal static void Run(object host,NpcPredictionCache cache,Action step,string output)
        {
            var source=Get(host,"Prediction");
            Require(Get(source,"Native")==null,"Default production composition has no native owner.");
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1600,2400,NPCID.Derpling,Start:16,Target:Main.myPlayer);
            var n=Main.npc[slot];step();cache.Demand(0,1,120);cache.Demand(1,120);
            TerrainBounds(source,n);
            var identity=new NpcIdentity((long)Get(host,"Session"),n,n.whoAmI,n.generation,n.type,n.netID);
            foreach(int effect in new[]{120,151,169,183,186,189,337,344,362,30,375,395,397})
            {
                n.AddBuff(effect,300);Require(Array.IndexOf(n.buffType,effect)>=0,"Original AddBuff reaches the attachment/non-motion effect "+effect);
                Call(source,"Prepare",identity,(long)Main.GameUpdateCount);
                Require(cache.Read(0)!=null,"Existing non-motion/attached effect must not withdraw the whole rolling path: "+effect);
                Require(effect==120 || (cache.Read(0).Assumptions&PredictionAssumption.UnmodeledDamageEffects)!=0,"Unmodeled damage effect remains explicit: "+effect);
                for(int i=0;i<n.buffType.Length;i++)if(n.buffType[i]==effect)n.DelBuff(i);
            }
            ShortPrefix(host,source,cache,n,identity,output);
            Call(source,"Prepare",identity,(long)Main.GameUpdateCount);var before=cache.Read(0);
            Require(before!=null && before.Strategy==PredictionStrategy.RollingConditional,"Boundary fixture consumes default output.");
            Vector2 position=n.position;n.SetDefaults(n.type);n.position=position;n.target=Main.myPlayer;
            Require(cache.Read(0)==null,"Same-type SetDefaults immediately retires the published result.");
            identity=new NpcIdentity((long)Get(host,"Session"),n,n.whoAmI,n.generation,n.type,n.netID);
            Call(source,"Prepare",identity,(long)Main.GameUpdateCount+1);var reset=cache.Read(0);
            Require(reset!=null && reset.RelationVersion>before.RelationVersion,"A reconstruction epoch survives identical type/slot.");
            n.life=0;Call(source,"Prepare",identity,(long)Main.GameUpdateCount+2);Require(cache.Read(0)==null,"Actual death clears rather than relabels a living forecast.");
            cache.Release(0);Require(cache.Required==120,"Path OFF only releases its own consumer.");cache.Release(1);Call(source,"Clear");NPC.ClearAll();Projectile.ClearAll();
            NativeCombatObservationChecks.Save(host,new ObservationOptions(collision:true,path:false));
            step();clears=0;
            var hooks=new HarmonyLib.Harmony("JueMingR.Tests.RollingOffBoundary");
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            try
            {
                foreach(var pair in new[]{new object[]{Get(source,"Terrain"),"Reset"},new object[]{Get(source,"rolling"),"Clear"}})
                    hooks.Patch(pair[0].GetType().GetMethod((string)pair[1],flags),prefix:new HarmonyLib.HarmonyMethod(typeof(NativeCombatRollingBoundaryChecks),nameof(Clearing)));
                for(int i=0;i<30;i++)step();
                Require(clears==0 && cache.Required==0,"Collision-only updates do no repeated prediction buffer/terrain/history cleanup.");
            }
            finally{hooks.UnpatchAll(hooks.Id);}
            File.WriteAllText(Path.Combine(output,"rolling-boundaries.txt"),"PASS default owner, original AddBuff effects, same-type reset epoch, death, independent consumers");
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        private static int clears;
        private static void ShortPrefix(object host,object source,NpcPredictionCache cache,NPC n,NpcIdentity identity,string output)
        {
            int life=n.life,regen=n.lifeRegenCount;Vector2 position=n.position,velocity=n.velocity,camera=Main.screenPosition;
            var rows=new System.Collections.Generic.List<string>{"condition,liveLife,active,rawCount,stop,displayMinimum,displayShown,strict120Shown"};
            try
            {
                n.life=1;n.lifeRegenCount=0;n.AddBuff(BuffID.OnFire,300);
                Require(Array.IndexOf(n.buffType,BuffID.OnFire)>=0,"Original OnFire must reach the terminal prefix fixture.");
                Call(source,"Prepare",identity,(long)Main.GameUpdateCount);
                var path=(NpcTrajectory)Get(cache,"result");
                Require(n.active && path!=null && path.Count>1 && path.Count<=30 && path.Stop==PredictionStop.Despawn,"A still-living target can have a known short terminal future.");
                rows.Add("known-DOT-death,"+n.life+","+n.active+","+path.Count+","+path.Stop+","+cache.MinimumRequired+","+(cache.Read(0)!=null)+","+(cache.Read(1)!=null));
                Require(cache.Read(0)==path && cache.Read(1)==null,"Display consumes the true short endpoint; strict120 still refuses it.");
                Main.screenPosition=n.Center-new Vector2(Main.screenWidth/2,Main.screenHeight/2);Call(Get(host,"World"),"Prepare");NativeCombatPresentationChecks.Project(Get(host,"World"));
                Require((int)Get(Get(host,"World"),"StrokeCount")>0 && ((string)Get(Get(host,"World"),"pathText")).Contains("0.2 秒"),"WorldLayer actually projects the short endpoint and its real duration.");
                n.life=life;n.lifeRegenCount=regen;for(int i=0;i<n.buffType.Length;i++)if(n.buffType[i]==BuffID.OnFire)n.DelBuff(i);
                n.position.X=15;n.velocity.X=-4;
                Call(source,"Prepare",identity,(long)Main.GameUpdateCount+1);path=(NpcTrajectory)Get(cache,"result");
                Require(path!=null && path.Count<=30 && path.Stop==PredictionStop.TerrainUnavailable,"A real border prefix remains short and cannot be padded.");
                rows.Add("world-border,"+n.life+","+n.active+","+path.Count+","+path.Stop+","+cache.MinimumRequired+","+(cache.Read(0)!=null)+","+(cache.Read(1)!=null));
                Require(path.Count==1 && cache.Read(0)==null,"A current point without a computed future remains invisible.");
                n.life=0;Call(source,"Prepare",identity,(long)Main.GameUpdateCount+2);Call(Get(host,"World"),"Prepare");
                Require(cache.Read(0)==null && Get(Get(host,"World"),"pathText")==null,"Actual observed death immediately retires display output.");
            }
            finally
            {
                n.life=life;n.lifeRegenCount=regen;n.position=position;n.velocity=velocity;Main.screenPosition=camera;
                for(int i=0;i<n.buffType.Length;i++)if(n.buffType[i]==BuffID.OnFire)n.DelBuff(i);
                File.WriteAllLines(Path.Combine(output,"rolling-short-prefix.csv"),rows);
            }
        }
        private static void Clearing(){clears++;}
        private static void TerrainBounds(object source,NPC n)
        {
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public;
            var seed=(NpcMotionState)source.GetType().GetMethod("Read",flags).Invoke(null,new object[]{n,1L});
            var terrain=(IPredictionTerrain)Activator.CreateInstance(Get(source,"Terrain").GetType(),true);
            var env=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY};
            var cases=new[]{new Vector2(-1,128),new Vector2(128,-1),new Vector2(Main.maxTilesX*16-10,128),new Vector2(128,Main.maxTilesY*16-10)};
            foreach(bool free in new[]{false,true})foreach(var position in cases)
            {
                var state=seed;state.X=position.X;state.Y=position.Y;state.Vx=state.Vy=0;state.NoTileCollide=free;terrain.Reset();PredictionStop stop;
                Require(!terrain.Move(ref state,env,out stop) && stop==PredictionStop.TerrainUnavailable,"Real terrain refuses world-edge movement before clamping: free="+free+" pos="+position);
            }
            foreach(bool free in new[]{false,true})
            {
                var state=seed;state.X=20;state.Y=128;state.Vx=-40;state.Vy=0;state.NoTileCollide=free;terrain.Reset();PredictionStop stop;
                Require(!terrain.Move(ref state,env,out stop) && stop==PredictionStop.TerrainUnavailable,"Real terrain refuses an outgoing border crossing: free="+free);
            }
            terrain.Reset();PredictionTile tile;PredictionStop reason;int x=80,y=80;var original=Main.tile[x,y];bool active=original.active();ushort type=original.type;
            try
            {
                Require(terrain.Tile(x,y,out tile,out reason),"Known real tile copied.");original.active(true);original.type=1;terrain.Reset();
                Require(terrain.Tile(x,y,out tile,out reason) && tile.Active && tile.Solid,"New sample invalidates hot geometry cells after a real tile edit.");
                terrain.Reset();int read=0;for(int j=10;j<90;j++)for(int i=10;i<90;i++)
                {
                    if(terrain.Tile(i,j,out tile,out reason)){read++;continue;}
                    Require(reason==PredictionStop.TerrainLimit && read==4096,"Hot cache cannot bypass the 4096 distinct-cell limit.");return;
                }
                throw new InvalidOperationException("Geometry limit fixture did not reach its real bound.");
            }
            finally{original.active(active);original.type=type;terrain.Reset();}
        }
        private static void Call(object value,string name,params object[] args)
        {value.GetType().GetMethod(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic).Invoke(value,args);}
    }
}
