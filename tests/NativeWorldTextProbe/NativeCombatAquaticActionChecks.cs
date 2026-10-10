using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatAquaticActionChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Boundaries()
        {
            var action=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcAquaticActionMotion",true).GetMethod("Step",Flags);
            var n=new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,615,615),Style=16,A3=298,Wet=true};object[] args={n,new PredictionEnvironment(),null,false,PredictionStop.None};
            Require((bool)action.Invoke(null,args) && !(bool)args[3] && ((NpcMotionState)args[0]).A3==299,"Every native threshold shares the exact pre-300 swim clock.");
            args[0]=(NpcMotionState)args[0];Require(!(bool)action.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.RandomDecision,"Unobserved random choice remains stopped before its earliest threshold.");
            foreach(float selected in new[]{1f,2f})
            {
                n.A2=selected;n.A3=299;n.CollideY=true;args[0]=n;
                Require((bool)action.Invoke(null,args) && (bool)args[3] && ((NpcMotionState)args[0]).A2==0 && ((NpcMotionState)args[0]).A3==0,"Known collision releases both already selected actions without entering a new random choice.");
            }
            n=new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,688,688),Style=16,A2=1,L0=10,X=1600,Y=2200,Width=32,Height=16};
            args=new object[]{n,new PredictionEnvironment{WorldWidth=400,WorldHeight=240},null,false,PredictionStop.None};
            Require(!(bool)action.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.MissingDependency,"In-world floating requires actual liquid facts.");
            n.X=0;args[0]=n;Require((bool)action.Invoke(null,args),"Original waterline margin returns known no-line without reading outside tiles.");
            Console.WriteLine("PASS aquatic action random/observed-action/liquid dependency boundaries");
        }
        internal static void Lifecycle(object context)
        {
            object source=Get(Get(context,"CombatObservation"),"Prediction");long session=(long)Get(Get(context,"CombatObservation"),"Session");var read=source.GetType().GetMethod("Read",Flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var action=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcAquaticMotion",true).GetMethod("Step",Flags);int cases=0;
            foreach(int member in new[]{615,688})foreach(int variant in new[]{0,1,2,3,4,5,6,7})
            {
                NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Pool(false);
                Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true};Main.LocalPlayer.position=new Vector2(1100,2358);
                var actor=Main.npc[16]=new NPC{whoAmI=16};actor.SetDefaults(member);actor.active=true;actor.target=Main.myPlayer;actor.direction=1;actor.directionY=1;actor.position=new Vector2(1600,variant==0?2220:variant==1?2230:variant==2?2240:2500);actor.velocity=new Vector2(2,-1);actor.wet=variant!=4;
                actor.ai[2]=member==615?(variant%2)+1:1;actor.ai[3]=variant==7?299:variant==3?1:58;actor.localAI[0]=variant==6?1:120;actor.collideX=variant==5;
                if(member==688 && variant==7){actor.ai[2]=0;actor.StrikeNPCNoInteraction(1,0,0);Require(actor.justHit && actor.active,"Original isolated hit prepares deterministic float state.");}
                // Four original neighboring waterline amounts include integer
                // /16 rounding, deep no-line, surface and dry motor branches.
                int column=(int)(actor.Center.X/16),row=(int)(actor.position.Y/16);
                if(variant<=2){for(int y=row-2;y<=row+1;y++)Main.tile[column,y].liquid=0;Main.tile[column,row+variant-1].liquid=(byte)(variant==0?255:variant==1?128:1);}
                terrain.Reset();var state=(NpcMotionState)read.Invoke(null,new object[]{actor,session});
                var e=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,PlayerX=Main.LocalPlayer.Center.X,PlayerY=Main.LocalPlayer.Center.Y};actor.AI();object[] args={state,e,terrain,1,1,PredictionStop.None,false};
                Require((bool)action.Invoke(null,args),"Observed aquatic native action must continue member="+member+" variant="+variant+" stop="+args[5]);state=(NpcMotionState)args[0];
                Require(state.Vx==actor.velocity.X && state.Vy==actor.velocity.Y && state.A2==actor.ai[2] && state.A3==actor.ai[3] && state.L0==actor.localAI[0],"Original aquatic action exact velocity/clock member="+member+" variant="+variant+" predicted="+state.Vx+","+state.Vy+" actual="+actor.velocity+" A2="+state.A2+"/"+actor.ai[2]+" A3="+state.A3+"/"+actor.ai[3]+" L0="+state.L0+"/"+actor.localAI[0]);cases++;
            }
            Console.WriteLine("PASS original aquatic selected-action/waterline/hit scalar cases="+cases);
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            object host=Get(context,"CombatObservation");Require(GetOptional(Get(host,"Prediction"),"Native")==null,"Aquatic actions keep the default rolling producer.");
            var rows=new List<string>{"scene,frame,tick,action,clock,localClock,x,y,vx,vy,wet,hit,selected,published,count,stop,matched,longMatches,actionMatches"};var failures=new List<string>();int total=0,totalLong=0,scenes=0;var selectedActions=new HashSet<int>();
            try
            {
                foreach(int member in new[]{615,688})foreach(int variant in new[]{0,1})
                {
                    string scene=member+"-aquatic-"+variant;scenes++;
                    try
                    {
                        NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.GameMode=0;Main.dayTime=false;Pool(variant==1);
                        Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};var p=Main.LocalPlayer;p.position=new Vector2(950,2400-p.height);p.fallStart=p.fallStart2=(int)(p.position.Y/16);p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.creativeGodMode=true;p.immune=true;p.immuneTime=100000;for(int i=0;i<3;i++)p.armor[i].SetDefaults(696+i);
                        int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1600,2500,member,Start:16,Target:Main.myPlayer);var actor=Main.npc[slot];actor.timeLeft=750;
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                        var freezes=new List<Frozen>();int matches=0,longMatches=0,actionMatches=0,selected=0,published=0;bool hit=false,sawFloat=false,sawRelease=false;double maximum=0;
                        for(int frame=0;frame<(member==615?2400:360) && actor.active;frame++)
                        {
                            // Only a real isolated Strike drives 688's native
                            // justHit transition. 615 chooses its own random
                            // action and destination; no ai slot is assigned.
                            if(member==688 && frame==10){actor.StrikeNPCNoInteraction(1,0,0);Require(actor.justHit && actor.active,"Actual nonlethal fish hit.");hit=true;freezes.Clear();}
                            NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();if(!actor.active)break;
                            if(actor.ai[2]==1 && member==688)sawFloat=true;if(sawFloat && actor.ai[2]==0)sawRelease=true;
                            if(member==615 && actor.ai[2]!=0)selectedActions.Add((int)actor.ai[2]);
                            bool has=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,actor);if(has)selected++;
                            var path=cache.Read(0);if(path!=null && ReferenceEquals(path.Identity.Token,actor))
                            {Require(has && path.CaptureTick==Main.GameUpdateCount,"Fresh aquatic selected publication.");published++;if(published==1 || frame%15==0 || freezes.Count==0 && actor.ai[2]!=0)freezes.Add(new Frozen(path,(int)actor.ai[2]));}
                            for(int i=freezes.Count-1;i>=0;i--)
                            {
                                var frozen=freezes[i];int offset=(int)((long)Main.GameUpdateCount-frozen.Path.CaptureTick);if(offset>=frozen.Path.Count){freezes.RemoveAt(i);continue;}if(offset<=0)continue;
                                var point=frozen.Path[offset];double error=Vector2.Distance(actor.position,new Vector2(point.Bounds.X,point.Bounds.Y));maximum=Math.Max(maximum,error);matches++;if(offset>=30)longMatches++;if(frozen.Action!=0)actionMatches++;
                                Require(error<=.08 && point.NoTileCollide==actor.noTileCollide,"Frozen native aquatic action "+scene+" offset="+offset+" capturedAction="+frozen.Action+" actualAction="+actor.ai[2]+" error="+error.ToString("R",CultureInfo.InvariantCulture)+" predicted="+point.Bounds.X+","+point.Bounds.Y+" pv="+point.Vx+","+point.Vy+" actual="+actor.position+" av="+actor.velocity+" wet="+actor.wet+" clock="+actor.ai[3]+" localClock="+actor.localAI[0]+" stop="+frozen.Path.Stop);
                            }
                            rows.Add(string.Join(",",scene,frame,Main.GameUpdateCount,actor.ai[2],actor.ai[3],actor.localAI[0],actor.position.X,actor.position.Y,actor.velocity.X,actor.velocity.Y,actor.wet,hit,has,path!=null,path?.Count??0,path?.Stop.ToString()??"none",matches,longMatches,actionMatches));
                        }
                        Require(selected>0 && published>0 && longMatches>=30 && actionMatches>=30,"Natural already selected aquatic action must publish long continuing geometry "+scene+" selected="+selected+" published="+published+" long="+longMatches+" actionMatched="+actionMatches);
                        if(member==688)Require(hit && sawFloat && sawRelease,"Same original hit fish must enter float and return to swim.");
                        total+=matches;totalLong+=longMatches;Console.WriteLine("AQUATIC_ACTION scene="+scene+" matched="+matches+" long="+longMatches+" actionMatched="+actionMatches+" maxError="+maximum+" naturalActions="+string.Join("|",selectedActions));
                    }
                    catch(InvalidOperationException failure){failures.Add(scene+": "+failure.Message);Console.WriteLine("AQUATIC_ACTION FAIL "+scene+": "+failure.Message);}
                }
                Require(selectedActions.Contains(1) && selectedActions.Contains(2),"Both randomly chosen original dolphin actions must really be observed; scalar-only coverage cannot replace either.");
                Require(failures.Count==0,"Aquatic natural action failures="+failures.Count+": "+string.Join(" | ",failures));Console.WriteLine("PASS original aquatic action continuous scenes="+scenes+" matched="+total+" long="+totalLong);
            }
            finally{Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"aquatic-actions-continuous.csv"),rows);}
        }
        private static void Pool(bool roof)
        {
            for(int x=65;x<146;x++)for(int y=138;y<=180;y++){Main.tile[x,y].active(y==180);Main.tile[x,y].type=1;Main.tile[x,y].liquid=(byte)(y>=140 && y<180?255:0);Main.tile[x,y].liquidType(0);}
            if(roof)for(int x=65;x<146;x++){Main.tile[x,139].active(true);Main.tile[x,139].type=1;}
        }
        private sealed class Frozen
        {internal readonly NpcTrajectory Path;internal readonly int Action;internal Frozen(NpcTrajectory path,int action){Path=path;Action=action;}}
    }
}
