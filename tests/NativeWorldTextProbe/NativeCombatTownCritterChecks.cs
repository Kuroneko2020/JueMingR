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
    internal static class NativeCombatTownCritterChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static readonly int[] Members={230,303,337,540,625,687};
        private static Action<NPC> before,after;
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static Type Model {get{return typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcTownCritterMotion",true);}}
        internal static void Boundaries()
        {
            var known=Model.GetMethod("Known",Flags);var action=Model.GetMethod("Step",Flags);
            foreach(int type in Members)
            {
                var n=new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,type,type),Style=7};
                Require((bool)known.Invoke(null,new object[]{n}),"Complete selectable AI7 catalogue member "+type);
                object[] args={n,new PredictionEnvironment(),null,1,PredictionStop.None,null,0,false};
                Require(!(bool)action.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.MissingDependency,"AI7 never defaults required home/weather/players/closure "+type);
                n.Identity=new NpcIdentity(1,new object(),16,1,46,46);Require(!(bool)known.Invoke(null,new object[]{n}),"An unselectable shared-style town/critter is not blanket certification.");
            }
            var areas=new[]{new MotionRect(1,2,20,42)};var alive=new[]{true};var dead=new[]{false};var stinky=new[]{true};var ticks=new[]{120};var talks=new[]{16};
            var original=new PredictionPlayers(areas,1,new[]{0},areas,alive,dead,stinky,ticks,talks);areas[0].X=99;stinky[0]=false;ticks[0]=0;talks[0]=-1;
            Require(original.SearchAreaAt(0).X==1 && original.StinkyAt(0) && original.StinkyTicksAt(0)==120 && original.TalkNpcAt(0)==16,"AI7 player input is an immutable copied set.");
            var changed=new PredictionPlayers(new[]{new MotionRect(1,2,20,42)},1,new[]{0},new[]{new MotionRect(1,2,20,42)},alive,dead,new[]{true},new[]{119},new[]{16});
            Require(!original.Equals(changed),"Actual stinky/talk clocks invalidate cached samples.");
            Console.WriteLine("PASS AI7 exact members / required facts / immutable player facts");
        }
        internal static void Lifecycle(object context)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");long session=(long)Get(host,"Session");
            var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);
            var terrain=(IPredictionTerrain)Get(source,"Terrain");var action=Model.GetMethod("Step",Flags);var drown=Model.GetMethod("Drown",Flags);int actions=0,boundaries=0,waters=0;
            foreach(int type in Members)foreach(int variant in new[]{0,1,2,3,4,5,6,7,8,9,10,11})
            {
                World(variant%3);Main.netMode=variant==10 || variant==11?0:1;var p=Main.LocalPlayer;
                var n=Main.npc[16]=new NPC{whoAmI=16};n.SetDefaults(type);n.active=true;n.target=p.whoAmI;n.position=new Vector2(1700.25f,2400-n.height);n.direction=variant%2==0?1:-1;n.velocity=new Vector2(n.direction*.65f,variant==6?-2:0);n.oldVelocity=n.velocity;n.oldPosition=n.position;
                n.homeTileX=106;n.homeTileY=150;n.homeless=variant%2==0;n.ai[0]=variant==0?0:variant==2?2:variant==3?8:variant==4?5:1;n.ai[1]=variant==11?1:320;n.localAI[3]=variant==10?1:60;
                if(variant==4){Main.tile[106,149].active(true);Main.tile[106,149].type=15;Main.tile[106,149].frameY=18;Main.tile[106,149].frameX=18;}
                if(variant==5){for(int y=148;y<150;y++){Main.tile[107,y].active(true);Main.tile[107,y].type=1;}}
                if(variant==7 || variant==8){Pool(variant==8?3:0);n.position.Y=2340;n.wet=Collision.WetCollision(n.position,n.width,n.height);}
                if(variant==9){n.target=255;n.homeTileX=n.homeTileY=-1;}
                terrain.Reset();var state=Read(read,tracking,terrain,n,session);var e=Environment(p);state.TownRelationsCaptured=state.TownRelationsComplete=true;
                // This scalar oracle calls AI directly, so UpdateNPC has not
                // prepared gravity. Compare the actual native shared scratch;
                // production fullStep sets its value in NpcGravityMotion.
                state.Gravity=(float)typeof(NPC).GetField("gravity",Flags).GetValue(null);
                object[] args={state,e,terrain,0,PredictionStop.None,new[]{state},1,false};bool valid=(bool)action.Invoke(null,args);PredictionStop stop=(PredictionStop)args[4];
                try{n.AI();}catch(Exception error){Console.WriteLine("AI7 NATIVE scalar failed type="+type+" variant="+variant+" phase="+n.ai[0]+" sittingManager="+(Main.sittingManager!=null)+" error="+error);throw;}
                if(!valid){Require(stop==PredictionStop.RandomDecision,"Observed AI7 scalar only stops for actual random branch type="+type+" variant="+variant+" stop="+stop);boundaries++;continue;}
                state=(NpcMotionState)args[0];
                Require(state.Vx==n.velocity.X && state.Vy==n.velocity.Y && state.X==n.position.X && state.Y==n.position.Y && state.Direction==n.direction && state.DirectionY==n.directionY && state.A0==n.ai[0] && state.A1==n.ai[1] && state.L2==n.localAI[2] && state.L3==n.localAI[3],
                    "Native AI7 scalar type="+type+" variant="+variant+" predicted="+state.X+","+state.Y+" v="+state.Vx+","+state.Vy+" phase="+state.A0+","+state.A1+" L2/L3="+state.L2+","+state.L3+" dir="+state.Direction+" actual="+n.position+" v="+n.velocity+" phase="+n.ai[0]+","+n.ai[1]+" L2/L3="+n.localAI[2]+","+n.localAI[3]+" dir="+n.direction);actions++;
            }
            foreach(int liquid in new[]{0,1,2,3})foreach(bool actuated in new[]{false,true})foreach(byte slope in new byte[]{0,1,3})
            {
                World(0);Pool(liquid);var n=Main.npc[16]=new NPC{whoAmI=16};n.SetDefaults(303);n.position=new Vector2(1700,2320);
                var head=Main.tile[106,144];head.liquid=255;head.liquidType(liquid);head.active(true);head.type=1;head.inActive(actuated);head.slope(slope);terrain.Reset();var state=(NpcMotionState)read.Invoke(null,new object[]{n,session});
                object[] args={state,Environment(Main.LocalPlayer),terrain,false,PredictionStop.None};Require((bool)drown.Invoke(null,args),"AI7 drown query has its complete native cells.");
                Require((bool)args[3]==Collision.DrownCollision(n.position,n.width,n.height,1,true),"Native AI7 drown kinds/rawactive/actuated/slopes kind="+liquid+" actuated="+actuated+" slope="+slope);waters++;
            }
            int waterLines=0;var waterLine=Model.GetMethod("WaterLine",Flags);World(0);
            foreach(int level in new[]{0,1,15,16,127,255})foreach(int offset in new[]{-2,-1,0,1})
            {
                for(int y=143;y<=146;y++)Main.tile[106,y].liquid=0;Main.tile[106,145+offset].liquid=(byte)level;
                terrain.Reset();var state=new NpcMotionState{X=106*16,Y=145*16,Width=2,Height=2};object[] args={state,Environment(Main.LocalPlayer),terrain,false,0f,PredictionStop.None};
                Require((bool)waterLine.Invoke(null,args),"AI7 floating surface acquires actual four native cells.");float height;bool found=Collision.GetWaterLine(106,145,out height);
                Require((bool)args[3]==found && (float)args[4]==height,"Native AI7 GetWaterLine integer liquid fractions level="+level+" offset="+offset);waterLines++;
            }
            Console.WriteLine("PASS AI7 original floating surface queries="+waterLines);
            int transforms=0;
            var nativeTransform=typeof(NPC).GetMethod("FishTransformationDuringRain",Flags);var afterMove=Model.GetMethod("AfterMove",Flags);
            foreach(int form in new[]{230,55})foreach(int mode in new[]{0,1})foreach(int mask in new[]{0,1,2,3})
            {
                World(0);Main.netMode=mode;Main.raining=true;var p=Main.LocalPlayer;p.npcTypeNoAggro[230]=(mask&1)!=0;p.npcTypeNoAggro[55]=(mask&2)!=0;var n=Main.npc[16]=new NPC{whoAmI=16};n.SetDefaults(form);n.active=true;n.target=Main.myPlayer;n.direction=1;n.position=new Vector2(1700.25f,2370);n.velocity=new Vector2(-.7f,-.3f);n.wet=form==230;n.ai[0]=1;n.ai[1]=320;n.localAI[2]=4;
                terrain.Reset();var state=Read(read,tracking,terrain,n,session);var e=Environment(Main.LocalPlayer);object[] args={state,e,false,PredictionStop.None};
                Require((bool)afterMove.Invoke(null,args),"Known AI7/fish transform tail remains scalar.");nativeTransform.Invoke(n,null);state=(NpcMotionState)args[0];
                Require(state.EffectiveType==n.type && state.X==n.position.X && state.Y==n.position.Y && state.Width==n.width && state.Height==n.height && state.Vx==n.velocity.X && state.Vy==n.velocity.Y && state.Direction==n.direction && state.A0==n.ai[0] && state.A1==n.ai[1] && state.HomeTileY==n.homeTileY && (mode==1 || state.TownHomeTileX==n.homeTileX) && state.TargetNoAggro==p.npcTypeNoAggro[n.type],"Native wet/rain transform tail form="+form+" mode="+mode+" mask="+mask+" predictedForm="+state.EffectiveType+" actual="+n.type+" predicted="+state.X+","+state.Y+" actual="+n.position+" home="+state.TownHomeTileX+","+state.HomeTileY+" actualHome="+n.homeTileX+","+n.homeTileY);transforms++;
                if(mode==0)
                {
                    // Continue the frozen scalar state through the reverse
                    // transform, without taking a fresh observation of masks.
                    n.wet=n.type==230;state.Wet=n.wet;n.direction=state.Direction=1;args=new object[]{state,(PredictionEnvironment)args[1],false,PredictionStop.None};
                    Require((bool)afterMove.Invoke(null,args),"Both known fish forms can reverse in one forecast.");nativeTransform.Invoke(n,null);state=(NpcMotionState)args[0];
                    Require(state.EffectiveType==form && state.EffectiveType==n.type && state.Direction==n.direction && state.TargetNoAggro==p.npcTypeNoAggro[n.type] && state.FormClosestNoAggro==p.npcTypeNoAggro[form==230?55:230],"Frozen reverse fish transform retains form-specific masks form="+form+" mask="+mask);transforms++;
                }
                p.npcTypeNoAggro[230]=p.npcTypeNoAggro[55]=false;
            }
            Console.WriteLine("PASS original AI7 scalar actions="+actions+" actual random boundaries="+boundaries+" native liquid queries="+waters+" transform tails="+transforms);
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");Require(GetOptional(source,"Native")==null,"AI7 uses default scalar rolling, no isolated native worker.");
            var terrain=(IPredictionTerrain)Activator.CreateInstance(Get(source,"Terrain").GetType(),true);var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);long session=(long)Get(host,"Session");
            var rows=new List<string>{"type,mode,frame,phase,clock,x,y,vx,vy,selected,published,count,stop,matches,long,changes,actions,random"};var failures=new List<string>();int total=0,totalLong=0;
            var oracle=new Harmony("JueMingR.Tests.TownCritter.NativeAction");oracle.Patch(typeof(NPC).GetMethod("UpdateNPC",Flags),prefix:new HarmonyMethod(typeof(NativeCombatTownCritterChecks),"Before"),postfix:new HarmonyMethod(typeof(NativeCombatTownCritterChecks),"After"));
            try
            {
                foreach(int type in Members)foreach(int mode in new[]{0,1,2})try
                {
                    World(mode);if(mode==2)Pool(0);Main.raining=mode==2;var p=Main.LocalPlayer;
                    int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1700,mode==2?2340:2400,type,Start:16,Target:p.whoAmI);var n=Main.npc[slot];
                    Require(n.active && n.aiStyle==7,"Actual AI7 legal native birth "+type);n.homeTileX=106;n.homeTileY=150;n.homeless=true;
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                    var frozen=new List<NpcTrajectory>();int selected=0,published=0,matches=0,longMatches=0,changes=0,actions=0,random=0;float phase=n.ai[0];double max=0,tangent=0;NpcMotionState predicted=default(NpcMotionState);bool valid=false;bool sawFish=false;PredictionStop actionStop=PredictionStop.None;
                    before=actor=>
                    {
                            // The independent action oracle must not reset Source's live cells.
                            terrain.Reset();
                        if(!ReferenceEquals(actor,n) || actor.aiStyle!=7){valid=false;return;}
                        predicted=Read(read,tracking,terrain,actor,session);predicted.TownRelationsCaptured=predicted.TownRelationsComplete=true;var group=new[]{predicted};var e=Environment(p);e.PlayerTimelineActive=true;
                        valid=NpcMotion.Step(ref predicted,group,1,e,terrain,1,true,out actionStop);
                        Require(valid || actionStop==PredictionStop.RandomDecision || actionStop==PredictionStop.Despawn,"Actual current AI7 action has a complete motor type="+type+" mode="+mode+" stop="+actionStop);if(actionStop==PredictionStop.RandomDecision)random++;
                    };
                    after=actor=>
                    {
                        if(!ReferenceEquals(actor,n) || !valid)return;double error=Vector2.Distance(n.position,new Vector2(predicted.X,predicted.Y));
                        Require(error<=.002 && Math.Abs(predicted.Vx-n.velocity.X)<=.00001 && Math.Abs(predicted.Vy-n.velocity.Y)<=.00001 && predicted.A0==n.ai[0] && predicted.A1==n.ai[1],"Original AI7 full action type="+type+" mode="+mode+" error="+error+" predicted="+predicted.X+","+predicted.Y+" v="+predicted.Vx+","+predicted.Vy+" phase="+predicted.A0+","+predicted.A1+" actual="+n.position+" v="+n.velocity+" phase="+n.ai[0]+","+n.ai[1]+" form="+predicted.EffectiveType+"/"+n.type);actions++;
                    };
                    for(int frame=0;frame<1800;frame++)
                    {
                        int oldTime=n.timeLeft,oldLife=n.life;Vector2 oldPosition=n.position;
                        NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();
                        Require(p.active && !p.dead,"Native AI7 premise player remains alive type="+type+" mode="+mode+" frame="+frame+" HP="+p.statLife+" position="+p.position+" fallStart="+p.fallStart);
                        if(!n.active)
                        {
                            Console.WriteLine("AI7 RETIRE type="+type+" mode="+mode+" frame="+frame+" oldTime="+oldTime+" time="+n.timeLeft+" oldLife="+oldLife+" life="+n.life+" oldPosition="+oldPosition+" position="+n.position+" PC="+p.position+" phase="+n.ai[0]+","+n.ai[1]+" predictedActive="+predicted.Active+" predictedTime="+predicted.TimeLeft+" actionStop="+actionStop+" matched="+matches+" long="+longMatches);
                            // Natural walking can leave the time-reset rectangle
                            // while still being selected. Preserve the real750
                            // clock and require the same observed retirement,
                            // rather than extending lifetime to fill1800 steps.
                            Require(oldTime==1 && n.timeLeft==0 && n.life==oldLife && n.life>0 && actionStop==PredictionStop.Despawn && !predicted.Active && predicted.TimeLeft==0,"Native AI7 natural lifetime retirement agrees with its pure full action.");
                            Require(!(bool)Get(Get(host,"Selection"),"HasTarget") && cache.Read(0)==null,"Retired AI7 identity revokes actual selection and published trajectory.");
                            break;
                        }
                        sawFish|=n.type==55;if(phase!=n.ai[0]){phase=n.ai[0];changes++;}
                        bool has=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,n);if(has)selected++;
                        var path=NativeCombatPresentationChecks.CurrentPath(host);if(path!=null && ReferenceEquals(path.Identity.Token,n)){Require(has && path.SampleTick==Main.GameUpdateCount,"AI7 actual selected publication uses current sample.");published++;if(frame%5==0 && path.Count>1)frozen.Add(path);}
                        for(int i=frozen.Count-1;i>=0;i--)
                        {
                            var item=frozen[i];int offset=(int)((long)Main.GameUpdateCount-item.SampleTick);if(offset>=item.Count){frozen.RemoveAt(i);continue;}if(offset<=0)continue;
                            var point=item[offset];double error=Vector2.Distance(n.position,new Vector2(point.Bounds.X,point.Bounds.Y));max=Math.Max(max,error);matches++;
                            if(offset>=30){longMatches++;tangent=Math.Max(tangent,Vector2.Distance(n.position,new Vector2(item[0].Bounds.X+item[0].Vx*offset,item[0].Bounds.Y+item[0].Vy*offset)));}
                            Require(error<=.08 && point.NoTileCollide==n.noTileCollide && point.CanReceive==(!n.dontTakeDamage && !n.immortal),"Frozen AI7 type="+type+" mode="+mode+" frame="+frame+" offset="+offset+" error="+error.ToString("R",CultureInfo.InvariantCulture)+" predicted="+point.Bounds.X+","+point.Bounds.Y+" actual="+n.position+" v="+n.velocity+" phase="+n.ai[0]+","+n.ai[1]+" L2/L3="+n.localAI[2]+","+n.localAI[3]+" form="+n.type+" stop="+item.Stop);
                        }
                        rows.Add(string.Join(",",type,mode,frame,n.ai[0],n.ai[1],n.position.X,n.position.Y,n.velocity.X,n.velocity.Y,has,path!=null,path?.Count??0,path?.Stop.ToString()??"none",matches,longMatches,changes,actions,random));
                    }
                    Require(selected>100 && published>100 && matches>=300 && longMatches>=300,"AI7 real selection and restored long windows type="+type+" mode="+mode+" selected="+selected+" published="+published+" matches="+matches+" long="+longMatches);
                    if(type!=230 || mode!=2)Require(changes>=2 && actions>=300 && random>0,"Native AI7 natural random selections and later observed motors type="+type+" mode="+mode+" changes="+changes+" actions="+actions+" random="+random);
                    else Require(sawFish,"Native wet GoldfishWalker really transforms to the same actor's swimming form.");
                    total+=matches;totalLong+=longMatches;Console.WriteLine("PASS original AI7 natural type="+type+" mode="+mode+" matched="+matches+" long="+longMatches+" changes="+changes+" actions="+actions+" random="+random+" maxError="+max.ToString("R",CultureInfo.InvariantCulture)+" tangent="+tangent);
                }
                catch(Exception error){failures.Add(type+"-"+mode+":"+error.Message);Console.WriteLine("FAIL original AI7 natural type="+type+" mode="+mode+" "+error);}
                finally{before=after=null;}
            }
            finally{before=after=null;oracle.UnpatchAll(oracle.Id);}
            Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"town-critter-scenes.csv"),rows);Require(failures.Count==0,"Complete AI7 natural matrix: "+string.Join(" | ",failures));Console.WriteLine("PASS AI7 complete 18 natural scenes matched="+total+" long="+totalLong);
        }
        private static NpcMotionState Read(MethodInfo read,MethodInfo tracking,IPredictionTerrain terrain,NPC n,long session)
        {var state=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,state,terrain};tracking.Invoke(null,args);return (NpcMotionState)args[1];}
        private static PredictionEnvironment Environment(Player p)
        {
            var exact=new MotionRect(p.position.X,p.position.Y,p.width,p.height);int stinky=p.FindBuffIndex(120);
            return new PredictionEnvironment{Players=new PredictionPlayers(new[]{new MotionRect((int)p.position.X,(int)p.position.Y,p.width,p.height)},1,new[]{p.whoAmI},new[]{exact},new[]{!p.dead && !p.ghost},new[]{p.dead},new[]{p.stinky},new[]{stinky<0?0:p.buffTime[stinky]},new[]{p.talkNPC}),TownWeatherCaptured=true,Raining=Main.raining,Storming=Main.IsItStorming,PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,PlayerVelocityCaptured=true,PlayerDead=p.dead,PlayerWet=p.wet,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,WorldSurface=(float)Main.worldSurface,Day=Main.dayTime,Multiplayer=Main.netMode==1,Expert=Main.expertMode,SlimeRain=Main.slimeRain,Eclipse=Main.eclipse};
        }
        private static void World(int mode)
        {
            // Main.Initialize creates this real collection. AI7's observed
            // sitting phase registers its anchor even for these critters.
            if(Main.sittingManager==null)Main.sittingManager=new Terraria.DataStructures.AnchoredEntitiesCollection();
            NativeCombatLiveContextChecks.FlightWorld();for(int i=0;i<Main.player.Length;i++){if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};Main.player[i].active=false;}NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.GameMode=0;Main.raining=false;Main.dayTime=mode==0;Main.eclipse=Main.slimeRain=false;Main.windSpeedCurrent=Main.windSpeedTarget=0;Main.cloudAlpha=0;Main.Map=new Terraria.Map.WorldMap(Main.maxTilesX,Main.maxTilesY);
            Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};var p=Main.LocalPlayer;p.position=new Vector2(1450,2400-p.height);p.ResetFallDamage();p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.immune=true;p.immuneTime=1000000;for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
            if(mode==1){for(int x=93;x<=94;x++)for(int y=147;y<150;y++){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}for(int x=119;x<=120;x++)for(int y=147;y<150;y++){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}Main.tile[109,149].active(true);Main.tile[109,149].type=15;Main.tile[109,149].frameY=18;Main.tile[109,149].frameX=18;}
        }
        private static void Pool(int kind)
        {for(int x=95;x<118;x++)for(int y=145;y<150;y++){Main.tile[x,y].active(false);Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(kind);}}
    }
}
