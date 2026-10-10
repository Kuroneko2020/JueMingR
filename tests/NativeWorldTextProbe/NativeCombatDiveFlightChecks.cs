using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatDiveFlightChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Action<NPC> before,after;
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static Type Model {get{return typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcDiveFlightMotion",true);}}
        internal static void Boundaries()
        {
            var known=Model.GetMethod("Known",Flags);var unit=Model.GetMethod("Unit",Flags);var length=Model.GetMethod("Length",Flags);
            foreach(int type in new[]{388,418})Require((bool)known.Invoke(null,new object[]{new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,type,type),Style=74}}),"Both original AI74 variants use the shared motor.");
            Require(!(bool)known.Invoke(null,new object[]{new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,387,387),Style=74}}),"Adjacent turret does not borrow dive flight.");
            for(int i=0;i<8192;i++)
            {
                float x=(i%257-128)*.17321f,y=(i%113-56)*.337f;if(x==0 && y==0)y=1;
                object[] args={x,y,0f,0f};unit.Invoke(null,args);var original=Vector2.Normalize(new Vector2(x,y));
                Require((float)args[2]==original.X && (float)args[3]==original.Y && (float)length.Invoke(null,new object[]{x,y})==new Vector2(x,y).Length(),"Original AI74 normalized vectors/length preserve field-store math index="+i);
            }
            Console.WriteLine("PASS AI74 exact family and original Vector2 matrix=8192");
        }
        internal static void Lifecycle(object context)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");long session=(long)Get(host,"Session");var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");var action=Model.GetMethod("Step",Flags);int matched=0,random=0,retired=0;
            foreach(int type in new[]{388,418})foreach(int variant in new[]{0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19})
            {
                World();var p=Main.LocalPlayer;var n=Main.npc[16]=new NPC{whoAmI=16};n.SetDefaults(type);n.active=true;n.target=Main.myPlayer;n.position=new Vector2(1800.125f,1850.25f);n.velocity=new Vector2(3.751f,-1.233f);n.direction=-1;
                n.ai[0]=variant<5?0:variant<10?1:variant<15?2:variant<18?4:3;n.ai[1]=variant==9?29:variant==8?19:variant==13?29:variant==17?2:variant==19?2:1;n.ai[2]=3;n.ai[3]=8;
                if(variant==1)n.ai[3]=119;if(variant==2)n.ai[3]=120;if(variant==3)p.position=new Vector2(n.Center.X-p.width*.5f,n.Center.Y+100-p.height*.5f);if(variant==4)p.position.X=n.Center.X+1000;
                if(variant==6 || variant==7)n.velocity=new Vector2(.7f,.1f);if(variant==7)p.dead=true;
                if(variant==11){n.velocity=new Vector2(8,2);p.position.X=n.Center.X+500;p.position.Y=n.Center.Y-500;}
                if(variant==12){int x=(int)n.Center.X/16,y=(int)n.Center.Y/16;Main.tile[x,y].active(true);Main.tile[x,y].type=1;}
                if(variant==14)p.position=new Vector2(n.Center.X-p.width*.5f,n.Center.Y-p.height*.5f);
                terrain.Reset();var state=Read(read,tracking,n,terrain,session);object[] args={state,Environment(p),terrain,0,PredictionStop.None,new[]{state},1,false};bool valid=(bool)action.Invoke(null,args);var stop=(PredictionStop)args[4];n.AI();state=(NpcMotionState)args[0];
                if(!valid){Require(stop==PredictionStop.RandomDecision && type==388 && variant==9 || stop==PredictionStop.Despawn && !n.active,"AI74 stops only at actual scatter or original retirement type="+type+" variant="+variant+" stop="+stop);if(stop==PredictionStop.RandomDecision)random++;else retired++;continue;}
                Require(state.X==n.position.X && state.Y==n.position.Y && state.Vx==n.velocity.X && state.Vy==n.velocity.Y && state.A0==n.ai[0] && state.A1==n.ai[1] && state.A2==n.ai[2] && state.A3==n.ai[3] && state.Width==n.width && state.Height==n.height && state.SpriteDirection==n.spriteDirection && state.Alpha==n.alpha,"Original AI74 scalar type="+type+" variant="+variant+" predicted="+state.X+","+state.Y+" V="+state.Vx.ToString("R")+","+state.Vy.ToString("R")+" actual="+n.position+" V="+n.velocity+" phase="+state.A0+","+state.A1+","+state.A2+","+state.A3+" actual="+string.Join("/",n.ai));matched++;
            }
            Console.WriteLine("PASS original AI74 scalar actions="+matched+" real scatter boundaries="+random+" retirements="+retired);
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");Require(GetOptional(source,"Native")==null,"Dive-flight defaults to pure rolling prediction.");var terrain=(IPredictionTerrain)Get(source,"Terrain");var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);long session=(long)Get(host,"Session");
            var observer=new Harmony("JueMingR.Tests.DiveFlight.NativeAction");observer.Patch(typeof(NPC).GetMethod("UpdateNPC",Flags),prefix:new HarmonyMethod(typeof(NativeCombatDiveFlightChecks),nameof(Before)),postfix:new HarmonyMethod(typeof(NativeCombatDiveFlightChecks),nameof(After)));
            var rows=new List<string>{"type,mode,frame,slot,generation,phase,clock,x,y,vx,vy,selected,published,count,stop,matched,long,actions,random,retired"};var failures=new List<string>();int total=0,totalLong=0;
            try
            {
                foreach(int type in new[]{388,418})foreach(int mode in new[]{0,1})
                {
                    try
                    {
                        World();var p=Main.LocalPlayer;p.isControlledByFilm=true;p.releaseJump=true;p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.immune=true;p.immuneTime=1000000;for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
                        if(mode==1)for(int x=125;x<128;x++)for(int y=112;y<149;y++){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}
                        NPC actor=null;var freezes=new List<NpcTrajectory>();var phases=new HashSet<int>();int selected=0,published=0,matches=0,longMatches=0,actions=0,random=0,retired=0,births=0;double max=0,tangent=0;NpcMotionState forecast=default(NpcMotionState);bool valid=false;
                        before=n=>
                        {
                            valid=false;if(!ReferenceEquals(n,actor) || !n.active)return;terrain.Reset();forecast=Read(read,tracking,n,terrain,session);PredictionStop stop;var e=Environment(p);e.PlayerTimelineActive=true;valid=NpcMotion.Step(ref forecast,new[]{forecast},1,e,terrain,1,true,out stop);Require(valid || stop==PredictionStop.RandomDecision || stop==PredictionStop.Despawn,"Current AI74 original dependencies type="+type+" mode="+mode+" stop="+stop);if(stop==PredictionStop.RandomDecision)random++;
                        };
                        after=n=>
                        {
                            if(!ReferenceEquals(n,actor) || !valid)return;double error=Vector2.Distance(n.position,new Vector2(forecast.X,forecast.Y));Require(error<=.002 && Math.Abs(forecast.Vx-n.velocity.X)<=.0001 && Math.Abs(forecast.Vy-n.velocity.Y)<=.0001 && forecast.A0==n.ai[0] && forecast.A1==n.ai[1] && forecast.Width==n.width && forecast.Height==n.height,"Current original AI74 full action type="+type+" mode="+mode+" error="+error+" predictedV="+forecast.Vx+","+forecast.Vy+" actualV="+n.velocity+" phase="+n.ai[0]+","+n.ai[1]);actions++;
                        };
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                        for(int frame=0;frame<1800;frame++)
                        {
                            if(actor==null || !actor.active){if(actor!=null)retired++;int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),2900,1650,type,Start:16,Target:Main.myPlayer);actor=Main.npc[slot];freezes.Clear();births++;}
                            if(frame%240==0){p.controlRight=frame%480==0;p.controlLeft=!p.controlRight;freezes.Clear();}
                            NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();if(!actor.active)continue;phases.Add((int)actor.ai[0]);bool has=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,actor);if(has)selected++;
                            var path=cache.Read(0);if(path!=null && ReferenceEquals(path.Identity.Token,actor)){Require(has && path.CaptureTick==Main.GameUpdateCount,"Real AI74 selected actor owns this publication.");published++;if(frame%5==0 && path.Count>1)freezes.Add(path);}
                            for(int i=freezes.Count-1;i>=0;i--)
                            {
                                var frozen=freezes[i];int offset=(int)((long)Main.GameUpdateCount-frozen.CaptureTick);if(offset>=frozen.Count){freezes.RemoveAt(i);continue;}if(offset<=0)continue;var point=frozen[offset];double error=Vector2.Distance(actor.position,new Vector2(point.Bounds.X,point.Bounds.Y));max=Math.Max(max,error);matches++;
                                Require(error<=.08 && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal) && point.NoTileCollide==actor.noTileCollide,"Frozen original AI74 type="+type+" mode="+mode+" frame="+frame+" capture="+frozen.CaptureTick+" offset="+offset+" error="+error+" phase="+actor.ai[0]+","+actor.ai[1]+" predictedV="+point.Vx+","+point.Vy+" actual="+actor.position+" V="+actor.velocity+" PC="+p.position+" PCV="+p.velocity+" stop="+frozen.Stop);
                                if(offset>=30){longMatches++;tangent=Math.Max(tangent,Vector2.Distance(actor.position,new Vector2(frozen[0].Bounds.X+frozen[0].Vx*offset,frozen[0].Bounds.Y+frozen[0].Vy*offset)));}
                            }
                            rows.Add(string.Join(",",type,mode,frame,actor.whoAmI,actor.generation,actor.ai[0],actor.ai[1],actor.position.X,actor.position.Y,actor.velocity.X,actor.velocity.Y,has,path!=null,path?.Count??0,path?.Stop.ToString()??"none",matches,longMatches,actions,random,retired));
                        }
                        Require(selected>100 && published>100 && actions>300 && longMatches>300 && tangent>8 && phases.Contains(0) && phases.Contains(1) && phases.Contains(2),"Natural AI74 observed phases and curved long forecast type="+type+" mode="+mode+" phases="+string.Join("/",phases)+" long="+longMatches+" tangent="+tangent);
                        Require(type==388?random>0 && retired>0 && phases.Contains(3):phases.Contains(4),"Original scatter/explosion or deterministic settle stage actually occurred type="+type+" mode="+mode+" births="+births+" retired="+retired);total+=matches;totalLong+=longMatches;Console.WriteLine("PASS natural AI74 type="+type+" mode="+mode+" matched="+matches+" long="+longMatches+" actions="+actions+" random="+random+" retired="+retired+" maxError="+max.ToString("R",CultureInfo.InvariantCulture));
                    }
                    catch(Exception error){failures.Add(type+"-"+mode+":"+error.Message);Console.WriteLine("FAIL original AI74 type="+type+" mode="+mode+" "+error);}
                    finally{before=after=null;}
                }
            }
            finally{before=after=null;observer.UnpatchAll(observer.Id);Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"dive-flight-scenes.csv"),rows);}
            Require(failures.Count==0,"Complete natural AI74 matrix: "+string.Join(" | ",failures));Console.WriteLine("PASS AI74 four natural scenes matched="+total+" long="+totalLong);
        }
        private static NpcMotionState Read(MethodInfo read,MethodInfo tracking,NPC n,IPredictionTerrain terrain,long session){var state=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,state,terrain};tracking.Invoke(null,args);return (NpcMotionState)args[1];}
        private static PredictionEnvironment Environment(Player p){return new PredictionEnvironment{Players=new PredictionPlayers(new[]{new MotionRect((int)p.position.X,(int)p.position.Y,p.width,p.height)},1,new[]{p.whoAmI}),PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerVelocityCaptured=true,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,PlayerDead=p.dead,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,WorldSurface=(float)Main.worldSurface,Multiplayer=Main.netMode==1,Expert=Main.expertMode,Day=Main.dayTime};}
        private static void World(){NativeCombatLiveContextChecks.FlightWorld();for(int i=0;i<Main.player.Length;i++){if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};Main.player[i].active=false;}NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.GameMode=0;Main.dayTime=false;Main.remixWorld=Main.getGoodWorld=Main.zenithWorld=false;Main.Map=new Terraria.Map.WorldMap(Main.maxTilesX,Main.maxTilesY);Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true};Main.LocalPlayer.position=new Vector2(1450,2400-Main.LocalPlayer.height);Main.LocalPlayer.ResetFallDamage();}
    }
}
