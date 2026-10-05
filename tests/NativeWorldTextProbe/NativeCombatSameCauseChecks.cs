using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeCombatAttackMechanismChecks;

namespace NativeWorldTextProbe
{
    // Small frozen decision checks reuse the real original-update/Host seam.
    // Only legal scene initial conditions are written. Later state is original
    // input, collision, damage and transformation, never an expected answer.
    internal static class NativeCombatSameCauseChecks
    {
        internal static void Run(object context,object host,NpcPredictionCache cache,Action step,string output,string phases)
        {
            var rows=new List<string>{phases=="same-slime"?"scene,future,tick,type,x,y,vx,vy,ai0,dir,expectedJump,expectedVx,actualJump,actualVx,frozenX,frozenY,error,life,playerLife,premiseSame":"scene,frame,tick,type,px,py,pwet,m_px,m_py,m_pwet,x,y,vx,vy,wet,cx,cy,ai0,dir,m_x,m_y,m_vx,m_vy,m_wet,m_cx,m_cy,m_ai0,m_dir,selected,published,count,stop,frozenX,frozenY,frozenError"};
            try
            {
                if(phases=="same-slime")Slimes(context,host,cache,step,rows);
                if(phases=="same-shore")Shore(context,host,cache,step,rows);
                if(phases=="same-water")WaterSurface(host);
            }
            finally{File.WriteAllLines(Path.Combine(output,phases+".csv"),rows);}
        }
        private static Player Scene(bool day)
        {
            NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.dayTime=day;Main.GameMode=0;
            Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};
            var p=Main.LocalPlayer;p.position=new Vector2(2200,2400-p.height);p.fallStart=p.fallStart2=(int)(p.position.Y/16);p.statLife=p.statLifeMax=p.statLifeMax2=500;return p;
        }
        private static void Slimes(object context,object host,NpcPredictionCache cache,Action step,List<string> rows)
        {
            foreach(int caseKey in new[]{183,81,304,667,244,1,59,138,71,659,658,141,377,446,685,-1,-183,-1001,-1183,-1304,-2183})
            {
                int type=Math.Abs(caseKey)%1000;bool injured=caseKey<-1000 && caseKey>-2000,night=caseKey<0 && caseKey>-1000,blocked=caseKey<-2000;
                Scene(!night);Main.worldSurface=160;int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),2800,2400,type,Start:16,Target:Main.myPlayer,ai0:injured && type==304?-50:-20,ai2:blocked?200:1);
                var n=Main.npc[slot];n.direction=n.spriteDirection=1;
                if(injured)n.life=n.lifeMax-1;
                if(type==377 || type==446)Main.LocalPlayer.position.X=2670;
                NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:25));
                // The first Host update activates demand; capture only after
                // the second ordinary update has sampled and published it.
                for(int warm=0;warm<2;warm++){NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();}
                var frozen=cache.Read(0);bool friendly=n.friendly;
                if(friendly)
                {
                    // A legal non-target stays outside production selection.
                    // Its common branch is compared as a scalar motion trace.
                    Require(frozen==null,"Friendly slime variant is not selected for combat prediction.");
                    var source=Get(host,"Prediction");var state=(NpcMotionState)source.GetType().GetMethod("Read",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public).Invoke(null,new object[]{n,1L});
                    var terrain=(IPredictionTerrain)Activator.CreateInstance(source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);terrain.Reset();var p=Main.LocalPlayer;
                    var env=new PredictionEnvironment{PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerWet=p.wet,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,Day=Main.dayTime,Remix=Main.remixWorld};
                    var points=new NpcTrajectoryPoint[25];points[0]=new NpcTrajectoryPoint(0,state);int count=1;PredictionStop stop=PredictionStop.None;
                    for(int i=1;i<points.Length;i++){if(!NpcMotion.Step(ref state,new[]{state},1,env,terrain,i,true,out stop))break;points[count++]=new NpcTrajectoryPoint(i,state);}
                    frozen=new NpcTrajectory(state.Identity,(long)Main.GameUpdateCount,1,NpcMotion.Assumptions(state),stop,points,count);
                }
                Require(frozen!=null && frozen.Identity.Type==type,"Pre-jump route is available without widening production target legality: "+type);
                int expected=-1,actual=-1,comparable=0,captureLife=n.life,capturePlayerLife=Main.LocalPlayer.statLife;double maxError=0,error15=double.NaN,error24=double.NaN;float expectedVx=0,actualVx=0,previous=n.velocity.Y;
                for(int future=1;future<frozen.Count;future++)if(frozen[future].Vy<0 && frozen[future-1].Vy>=0){expected=future;expectedVx=frozen[future].Vx;break;}
                for(int future=1;future<=24;future++)
                {
                    NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();
                    if(actual<0 && n.velocity.Y<0 && previous>=0){actual=future;actualVx=n.velocity.X;}previous=n.velocity.Y;
                    Require(future<frozen.Count,"The captured slime route covers its near trajectory.");var point=frozen[future].Bounds;double error=Math.Sqrt((point.X-n.position.X)*(point.X-n.position.X)+(point.Y-n.position.Y)*(point.Y-n.position.Y));
                    bool premise=n.life==captureLife && Main.LocalPlayer.statLife==capturePlayerLife && n.active && !Main.LocalPlayer.dead; if(premise)comparable++;maxError=Math.Max(maxError,error);if(future==15)error15=error;if(future==24)error24=error;
                    rows.Add(Csv("slime-"+caseKey,future,Main.GameUpdateCount,type,n.position.X,n.position.Y,n.velocity.X,n.velocity.Y,n.ai[0],n.direction,expected,expectedVx,actual,actualVx,point.X,point.Y,error,n.life,Main.LocalPlayer.statLife,premise));
                }
                Console.WriteLine("SAME-SLIME case="+caseKey+" type="+type+" consumer="+(friendly?"direct-friendly":"Source/Cache")+" frozenTick="+frozen.CaptureTick+" expectedJump="+expected+" actualJump="+actual+" predictedVx="+expectedVx+" actualVx="+actualVx+" comparable="+comparable+"/24 maxError="+maxError+" error15="+error15+" error24="+error24);
                Require(actual>0 && actual==expected && Math.Sign(actualVx)==Math.Sign(expectedVx),"Frozen pre-jump direction and timing match original, without refreshing the captured route: "+type);
                bool toward=!blocked && (night || injured || type==183 || type==81 || type==304 || type==667 || type==244 || type==658 || type==659);
                Require(toward?actualVx<0:actualVx>0,"Passive/forced-active/turn-away/blocked counterexample has its actual direction: "+caseKey);
                if(type==183 || type==1 || type==81)Require(comparable==24 && maxError<.1,"The frozen near route agrees through 24 comparable original updates: "+caseKey);
            }
        }
        private static void Shore(object context,object host,NpcPredictionCache cache,Action step,List<string> rows)
        {
            var source=Get(host,"Prediction");const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
            var read=source.GetType().GetMethod("Read",flags);var readPlayer=source.GetType().GetMethod("ReadPlayer",flags);
            var terrainType=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);
            var advance=typeof(RollingNpcPrediction).GetMethod("AdvancePlayer",BindingFlags.Instance|BindingFlags.NonPublic);
            var body=typeof(RollingNpcPrediction).GetField("playerBody",BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(bool walking in new[]{false,true})
            {
                var p=Scene(true);
                for(int x=100;x<160;x++)for(int y=140;y<150;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(0);}
                for(int x=160;x<180;x++)for(int y=140;y<150;y++){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}
                p.position=new Vector2(walking?2510:2570,2240-p.height);p.fallStart=p.fallStart2=(int)(p.position.Y/16);
                if(walking)p.armor[3].SetDefaults(ItemID.WaterWalkingBoots);
                int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),2480,2290,157,Start:16,Target:Main.myPlayer);
                var n=Main.npc[slot];n.wet=true;n.velocity=new Vector2(2,0);n.direction=1;n.ai[0]=1;
                NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:25));
                for(int warm=0;warm<2;warm++){NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();}
                var frozen=cache.Read(0);Require(frozen!=null,"Shore initial default consumer has a route.");
                var state=(NpcMotionState)read.Invoke(null,new object[]{n,frozen.Identity.Session});var pm=(PredictionPlayerMotion)readPlayer.Invoke(null,new object[]{p});
                var env=new PredictionEnvironment{PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerWet=p.wet,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,RockLayer=(float)Main.rockLayer,Day=Main.dayTime,Remix=Main.remixWorld,PlayerIndex=Main.myPlayer};
                var terrain=(IPredictionTerrain)Activator.CreateInstance(terrainType,true);terrain.Reset();var model=new RollingNpcPrediction();
                int firstPlayer=-1,firstFish=-1,turn=-1;bool dry=false;double maxFrozenError=0;string scene=walking?"shore-waterwalk":"shore-solid";
                for(int future=1;future<=120;future++)
                {
                    object[] args={pm,env,terrain,PredictionStop.None};Require((bool)advance.Invoke(model,args),"Bounded shared player movement succeeds.");pm=(PredictionPlayerMotion)args[0];var pb=(NpcMotionState)body.GetValue(model);
                    var next=env;next.PlayerX=pm.X+pm.Width/2;next.PlayerY=pm.Y+pm.Height/2;next.PlayerWet=pb.Wet;
                    PredictionStop stop;Require(NpcMotion.Step(ref state,new[]{state},1,next,terrain,future,true,out stop),"Shore bounded fish step succeeds: "+stop);
                    NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();
                    if(firstPlayer<0 && (Math.Abs(pm.Y-p.position.Y)>.01 || pb.Wet!=p.wet))firstPlayer=future;
                    if(firstFish<0 && (Math.Abs(state.Vx-n.velocity.X)>.01 || state.Wet!=n.wet || state.A0!=n.ai[0] || state.Direction!=n.direction))firstFish=future;
                    if(turn<0 && n.direction<0)turn=future;dry|=!n.wet;
                    var selection=Get(host,"Selection");bool chosen=(bool)Get(selection,"HasTarget") && ReferenceEquals(((NpcIdentity)Get(selection,"Target")).Token,n);var path=cache.Read(0);
                    Require(future<frozen.Count,"The captured shore route has a future at this actual update.");var f=frozen[future].Bounds;
                    double error=Math.Sqrt((f.X-n.position.X)*(f.X-n.position.X)+(f.Y-n.position.Y)*(f.Y-n.position.Y));maxFrozenError=Math.Max(maxFrozenError,error);
                    rows.Add(Csv(scene,future,Main.GameUpdateCount,n.type,p.position.X,p.position.Y,p.wet,pm.X,pm.Y,pb.Wet,n.position.X,n.position.Y,n.velocity.X,n.velocity.Y,n.wet,n.collideX,n.collideY,n.ai[0],n.direction,state.X,state.Y,state.Vx,state.Vy,state.Wet,state.CollideX,state.CollideY,state.A0,state.Direction,chosen,path!=null,path?.Count??0,path?.Stop.ToString(),f.X,f.Y,error));
                }
                Console.WriteLine("SAME-SHORE scene="+scene+" firstPlayer="+firstPlayer+" firstFish="+firstFish+" originalTurn="+turn+" originalDry="+dry+" frozenTick="+frozen.CaptureTick+" frozenCount="+frozen.Count+" frozenMaxError="+maxFrozenError);
                Require(turn>0 && !dry,"Original solid shore turns fish while it remains wet.");
                Require(firstPlayer<0,"Stationary supported player remains dry at the observed position.");
                Require(firstFish<0 && maxFrozenError<.1,"The real pre-turn Cache route agrees with original at all 120 frozen offsets.");
            }
        }
        private static void WaterSurface(object host)
        {
            var source=Get(host,"Prediction");var type=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);
            var water=type.GetMethod("WaterSurface",BindingFlags.Instance|BindingFlags.NonPublic);int checkedCases=0;
            foreach(byte kind in new byte[]{0,1,2,3})foreach(byte liquid in new byte[]{32,128,255})foreach(int state in new[]{0,1,2,3,4})
            {
                Scene(true);for(int x=100;x<104;x++){Main.tile[x,140].liquid=liquid;Main.tile[x,140].liquidType(kind);}
                float surface=140*16+16-(liquid/32*2+2),vy=state==1?-1:state==4?3:.4f;
                var box=new MotionRect(1610,surface-42+(state==2?2:state==4?-2:0),20,42);bool down=state==3,lavaWalk=state!=4;
                var native=Collision.WaterCollision(new Vector2(box.X,box.Y),new Vector2(.5f,vy),20,42,down,false,lavaWalk);
                var terrain=(IPredictionTerrain)Activator.CreateInstance(type,true);terrain.Reset();object[] args={box,.5f,vy,down,lavaWalk,PredictionStop.None};
                Require((bool)water.Invoke(terrain,args) && Math.Abs((float)args[2]-native.Y)<.0001,"Water support matches original liquid/upward/submerged/down/capability boundary.");checkedCases++;
            }
            Scene(true);Main.tile[100,140]=null;var unknown=(IPredictionTerrain)Activator.CreateInstance(type,true);unknown.Reset();
            object[] missing={new MotionRect(1610,2198,20,42),0f,.4f,false,true,PredictionStop.None};
            Require(!(bool)water.Invoke(unknown,missing) && (PredictionStop)missing[5]==PredictionStop.TerrainUnavailable,"Unknown water-surface input is protected.");
            Console.WriteLine("SAME-WATER originalComparisons="+checkedCases+" unknownProtected=True; gravity-independent original bottom predicate, down/up/submerged/lava distinctions retained");
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        private static string Csv(params object[] values){var fields=new string[values.Length];for(int i=0;i<fields.Length;i++)fields[i]="\""+Convert.ToString(values[i],CultureInfo.InvariantCulture).Replace("\"","\"\"")+"\"";return string.Join(",",fields);}
    }
}
