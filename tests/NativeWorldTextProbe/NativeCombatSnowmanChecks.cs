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
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatSnowmanChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Action<NPC> before,after;
        private static Action<int,int,int,bool> placed;
        private static Action<Projectile> killing;
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static void Placed(int i,int j,int Type,bool __result){placed?.Invoke(i,j,Type,__result);}
        private static void Killing(Projectile __instance){killing?.Invoke(__instance);}
        internal static void Boundaries()
        {
            var type=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcSnowmanMotion",true);
            var firing=type.GetMethod("FiringDistance",Flags);int cases=0;
            foreach(float fraction in new[]{0f,.125f,.875f})
            foreach(int dx in new[]{-911,-910,909,910})
            foreach(int dy in new[]{-551,-550,549,550})
            {
                var target=new Vector2(3200+fraction,1800+fraction);
                var shoot=target+new Vector2(dx,dy);var state=new NpcMotionState{X=shoot.X-13,Y=shoot.Y-20,Width=26,Height=40};
                var env=new PredictionEnvironment{PlayerX=target.X,PlayerY=target.Y};
                Require((bool)firing.Invoke(null,new object[]{state,env})==NPC.AI_GlobalFiringDistanceCheck(shoot,target),"AI38 native integer firing rectangle boundary.");cases++;
            }
            var step=type.GetMethod("Step",Flags);
            var n=new NpcMotionState{Identity=new NpcIdentity(1,new object(),10,1,145,145),Style=38,Width=26,Height=40,X=3000,Y=2000,Direction=1,A1=3,TargetCaptured=true,PlayerActive=true};
            var e=new PredictionEnvironment{PlayerX=1000,PlayerY=2000};object[] args={n,e,null,1,PredictionStop.None,null,0,false};
            Require((bool)step.Invoke(null,args) && ((NpcMotionState)args[0]).A1==0 && ((NpcMotionState)args[0]).Vy==0,"Out-of-view pause exits without hopping in the same native branch.");
            n.A2=15;e.PlayerX=3100;e.Multiplayer=true;args[0]=n;args[1]=e;
            Require((bool)step.Invoke(null,args) && ((NpcMotionState)args[0]).A1==3 && ((NpcMotionState)args[0]).A2==16,"Client pause clock waits for the authoritative server reset.");
            e.Multiplayer=false;args[0]=n;args[1]=e;
            Require((bool)step.Invoke(null,args) && ((NpcMotionState)args[0]).A1==0 && ((NpcMotionState)args[0]).A2==0,"Server resets the observed sixteen-action pause.");
            Console.WriteLine("PASS Frost Legion native firing boundaries="+cases+" and server/client pause branches");
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");long session=(long)Get(host,"Session");
            var read=source.GetType().GetMethod("Read",Flags);var capture=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);
            var terrain=(IPredictionTerrain)Get(source,"Terrain");var rows=new List<string>{"scene,frame,type,hop,cycle,pause,turn,selected,published,count,stop,matched,longMatched,error,cross,actions"};var failures=new List<string>();
            var observer=new Harmony("JueMingR.Probe.FrostLegionOracle");var update=typeof(NPC).GetMethod("UpdateNPC",Flags);
            observer.Patch(update,prefix:new HarmonyMethod(typeof(NativeCombatSnowmanChecks).GetMethod(nameof(Before),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatSnowmanChecks).GetMethod(nameof(After),Flags)));
            var placeTile=typeof(WorldGen).GetMethod("PlaceTile",Flags,null,new[]{typeof(int),typeof(int),typeof(int),typeof(bool),typeof(bool),typeof(int),typeof(int)},null);
            var kill=typeof(Projectile).GetMethod("Kill",Flags);
            // Observe the actual emitted snowball's terrain effect. Neither
            // original Kill nor PlaceTile is suppressed or called twice.
            observer.Patch(placeTile,postfix:new HarmonyMethod(typeof(NativeCombatSnowmanChecks).GetMethod(nameof(Placed),Flags)));
            observer.Patch(kill,prefix:new HarmonyMethod(typeof(NativeCombatSnowmanChecks).GetMethod(nameof(Killing),Flags)));
            int totalMatches=0,totalActions=0;
            try
            {
                for(int scene=0;scene<12;scene++)
                {
                    try
                    {
                        int type=143+scene/4,mode=scene%4;Setup(context,mode);int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1800,2400,type,Start:16,Target:Main.myPlayer);var actor=Main.npc[slot];
                        Require(actor.active && actor.aiStyle==38,"Real Frost Legion birth uses the native shared AI.");
                        if(mode==2){actor.AddBuff(BuffID.Confused,1200);Require(actor.FindBuffIndex(BuffID.Confused)>=0,"Actual accepted native confusion buff establishes the confused counterexample.");}
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                        var freezes=new List<NpcTrajectory>();var phases=new Dictionary<NpcTrajectory,string>();int actions=0,matched=0,longMatches=0,cross=0,selectedCount=0,published=0,pauses=0,resumed=0,turns=0;double maxError=0,tangent=0;
                        var cumulative=new Dictionary<NpcTrajectory,NpcMotionState>();var frozenCells=new Dictionary<NpcTrajectory,System.Collections.IDictionary>();var firstDifferences=new HashSet<NpcTrajectory>();var changedPremises=new HashSet<NpcTrajectory>();var negativeReported=new HashSet<NpcTrajectory>();NpcMotionState originalBefore=default(NpcMotionState);int nativeFrame=-1,invalidated=0,negativeDifferences=0,recoveredLong=0;long mutationTick=-1;bool mutationPending=false;NpcTrajectory publicationBeforeMutation=null;double negativeMax=0;
                        placed=(x,y,t,result)=>
                        {
                            if(scene!=9)return;var tile=Main.tile[x,y];Console.WriteLine("SNOWMAN ORIGINAL-PLACE scene="+scene+" frame="+nativeFrame+" tick="+Main.GameUpdateCount+" xy="+x+","+y+" requested="+t+" result="+result+" actual="+TileState(tile));
                            if(!result || tile==null || !tile.active())return;
                            foreach(var frozen in freezes)
                            {
                                object captured=frozenCells[frozen][y*Main.maxTilesX+x];
                                if(captured!=null && (!(bool)Get(captured,"Active") || (ushort)Get(captured,"Type")!=tile.type) && changedPremises.Add(frozen))invalidated++;
                            }
                            publicationBeforeMutation=cache.Read(0);mutationTick=(long)Main.GameUpdateCount;mutationPending=true;
                        };
                        killing=p=>{if(scene==9 && p.type==109)Console.WriteLine("SNOWMAN ORIGINAL-SNOWBALL-KILL frame="+nativeFrame+" tick="+Main.GameUpdateCount+" slot="+p.whoAmI+" key="+(int)p.key+" position="+p.position+" velocity="+p.velocity+" size="+p.width+","+p.height+" ai="+string.Join("/",p.ai)+" noDrop="+p.noDropItem+" owner="+p.owner);};
                        bool wasPaused=false;int previousDirection=actor.direction,confusedHops=0;float beforeVy=0;var flightPhases=new HashSet<int>();NPC pending=null;NpcMotionState predicted=default(NpcMotionState);bool valid=false;
                        before=n=>
                        {
                            pending=n;valid=false;if(!ReferenceEquals(n,actor) || !n.active)return;
                            beforeVy=n.velocity.Y;predicted=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,predicted,terrain};capture.Invoke(null,args);predicted=(NpcMotionState)args[1];
                            originalBefore=predicted;
                            var p=Main.player[n.target>=0 && n.target<255?n.target:Main.myPlayer];var box=p.Hitbox;
                            var e=new PredictionEnvironment{Players=new PredictionPlayers(new[]{new MotionRect(box.X,box.Y,box.Width,box.Height)},1,new[]{p.whoAmI}),PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerDead=p.dead || !p.active,PlayerVelocityCaptured=true,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,PlayerTimelineActive=p.active && !p.dead && !p.ghost,Expert=Main.expertMode,Day=Main.dayTime,Multiplayer=Main.netMode==1,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,GravityWorldSurface=Main.worldSurface,RockLayer=(float)Main.rockLayer};
                            terrain.Reset();PredictionStop stop;valid=NpcMotion.Step(ref predicted,null,0,e,terrain,1,true,out stop);
                            Require(valid || stop==PredictionStop.Despawn || stop==PredictionStop.MissingDependency,"Native deterministic Frost Legion action may only stop for an actual missing premise or retirement: "+stop);
                        };
                        after=n=>
                        {
                            if(!valid || !ReferenceEquals(n,pending))return;double error=Vector2.Distance(new Vector2(predicted.X,predicted.Y),n.position);
                            Require(error<=.08 && predicted.Vx==n.velocity.X && predicted.Vy==n.velocity.Y && predicted.A0==n.ai[0] && predicted.A1==n.ai[1] && predicted.A2==n.ai[2] && predicted.A3==n.ai[3] && predicted.L2==n.localAI[2] && predicted.Direction==n.direction,"Original Frost Legion action scene="+scene+" error="+error+" predictedV="+predicted.Vx+","+predicted.Vy+" actual="+n.velocity+" ai="+n.ai[0]+","+n.ai[1]+","+n.ai[2]+","+n.ai[3]);actions++;
                            if(mode==2){Require(n.ai[0]==0 && n.ai[1]==0,"Original confusion resets both hop counters after every grounded hop.");if(beforeVy==0 && n.velocity.Y<0)confusedHops++;flightPhases.Add(Math.Sign(n.velocity.Y));}
                        };
                        for(int frame=0;frame<900 && actor.active;frame++)
                        {
                            nativeFrame=frame;
                            NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();if(!actor.active)break;
                            if(mutationPending)
                            {
                                var current=cache.Read(0);var prepared=GetOptional(Get(host,"World"),"preparedPath") as NpcTrajectory;
                                Require(current==null || current.CaptureTick==Main.GameUpdateCount && !ReferenceEquals(current,publicationBeforeMutation),"Actual snow placement retires the preceding production publication.");
                                Require(prepared==null || ReferenceEquals(prepared,current),"The actual completed presentation consumes only the replacement publication.");
                                Console.WriteLine("SNOWMAN TERRAIN-PUBLICATION frame="+frame+" tick="+Main.GameUpdateCount+" oldCapture="+(publicationBeforeMutation?.CaptureTick??-1)+" currentCapture="+(current?.CaptureTick??-1)+" preparedCurrent="+ReferenceEquals(prepared,current)+" affectedFrozen="+invalidated);mutationPending=false;
                            }
                            // Confused still jumps, but native resets ai0/ai1
                            // after EVERY grounded hop. Its genuine phase
                            // crossing is ascent/descent, not a third-hop clock
                            // that original confusion explicitly prevents.
                            string phase=actor.ai[0]+"/"+actor.ai[1]+"/"+(actor.ai[1]>=3 && type!=143)+(mode==2?"/flight="+Math.Sign(actor.velocity.Y):"");bool paused=type!=143 && actor.ai[1]>=3;if(paused)pauses++;if(wasPaused && !paused)resumed++;wasPaused=paused;
                            if(actor.direction!=previousDirection){turns++;previousDirection=actor.direction;}
                            bool selected=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,actor);if(selected)selectedCount++;
                            var path=cache.Read(0);if(path!=null && ReferenceEquals(path.Identity.Token,actor)){Require(selected && path.CaptureTick==Main.GameUpdateCount,"Current actual Frost Legion actor owns the full production publication.");published++;if(frame%5==0 && path.Count>1){freezes.Add(path);phases[path]=phase;if(scene==9){var sampled=(NpcMotionState)read.Invoke(null,new object[]{actor,session});object[] captured={actor,sampled,terrain};capture.Invoke(null,captured);cumulative[path]=(NpcMotionState)captured[1];var copy=new System.Collections.Hashtable();foreach(System.Collections.DictionaryEntry cell in (System.Collections.IDictionary)Get(terrain,"cells"))copy.Add(cell.Key,cell.Value);frozenCells[path]=copy;}}}
                            for(int i=freezes.Count-1;i>=0;i--)
                            {
                                var frozen=freezes[i];int offset=(int)((long)Main.GameUpdateCount-frozen.CaptureTick);if(offset>=frozen.Count){freezes.RemoveAt(i);cumulative.Remove(frozen);frozenCells.Remove(frozen);firstDifferences.Remove(frozen);changedPremises.Remove(frozen);negativeReported.Remove(frozen);phases.Remove(frozen);continue;}if(offset<=0)continue;
                                string replayDetail="";
                                if(scene==9)
                                {
                                    var replay=cumulative[frozen];var previous=replay;var pc=Main.LocalPlayer;var box=pc.Hitbox;
                                    var e=new PredictionEnvironment{Players=new PredictionPlayers(new[]{new MotionRect(box.X,box.Y,box.Width,box.Height)},1,new[]{pc.whoAmI}),PlayerIndex=pc.whoAmI,PlayerX=pc.Center.X,PlayerY=pc.Center.Y,PlayerWidth=pc.width,PlayerHeight=pc.height,PlayerDead=pc.dead || !pc.active,PlayerVelocityCaptured=true,PlayerVx=pc.velocity.X,PlayerVy=pc.velocity.Y,PlayerTimelineActive=pc.active && !pc.dead && !pc.ghost,Expert=Main.expertMode,Day=Main.dayTime,Multiplayer=Main.netMode==1,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,GravityWorldSurface=Main.worldSurface,RockLayer=(float)Main.rockLayer};
                                    terrain.Reset();PredictionStop replayStop;bool replayValid=NpcMotion.Step(ref replay,null,0,e,terrain,offset,true,out replayStop);cumulative[frozen]=replay;
                                    double replayError=Vector2.Distance(new Vector2(replay.X,replay.Y),actor.position);
                                    replayDetail=" cumulativeValid="+replayValid+" cumulativeStop="+replayStop+" cumulativeError="+replayError+" cumulativeBefore="+State(previous)+" cumulativeAfter="+State(replay)+" originalBefore="+State(originalBefore);
                                    if(replayValid && (replayError>.01 || replay.A0!=actor.ai[0] || replay.A1!=actor.ai[1]) && firstDifferences.Add(frozen))Console.WriteLine("SNOWMAN FIRST-CUMULATIVE-DIFFERENCE frame="+frame+" capture="+frozen.CaptureTick+" offset="+offset+replayDetail+" originalAfter="+actor.position+" velocity="+actor.velocity);
                                }
                                var point=frozen[offset];double error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),actor.position);
                                if(changedPremises.Contains(frozen))
                                {
                                    negativeMax=Math.Max(negativeMax,error);
                                    if(error>.08 && negativeReported.Add(frozen)){negativeDifferences++;Console.WriteLine("SNOWMAN CHANGED-TERRAIN-NEGATIVE frame="+frame+" capture="+frozen.CaptureTick+" offset="+offset+" error="+error+" mutationTick="+mutationTick+" old="+PointState(point)+" native="+State(originalBefore)+replayDetail);}
                                    continue;
                                }
                                maxError=Math.Max(maxError,error);matched++;if(scene==9 && mutationTick>=0 && frozen.CaptureTick>=mutationTick && offset>=30)recoveredLong++;
                                if(scene==9 && error>.08)
                                {
                                    var prior=frozen[offset-1];Console.WriteLine("SNOWMAN FROZEN-CONTACT frame="+frame+" capture="+frozen.CaptureTick+" offset="+offset+" prior="+PointState(prior)+" after="+PointState(point)+" nativeBefore="+State(originalBefore)+" capturedCells="+frozenCells[frozen].Count+" scan="+ContactCells(prior,frozenCells[frozen]));
                                }
                                Require(error<=.08 && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal) && point.NoTileCollide==actor.noTileCollide,"Frozen native Frost Legion hops scene="+scene+" frame="+frame+" capture="+frozen.CaptureTick+" offset="+offset+" error="+error+" stop="+frozen.Stop+" originalPhase="+phases[frozen]+" phase="+phase+" predicted="+point.Bounds.X+","+point.Bounds.Y+" predictedV="+point.Vx+","+point.Vy+" actual="+actor.position+" actualV="+actor.velocity+" ai="+string.Join("/",actor.ai)+" local="+string.Join("/",actor.localAI)+" collide="+actor.collideX+","+actor.collideY+" direction="+actor.direction+" player="+Main.LocalPlayer.position+" playerV="+Main.LocalPlayer.velocity+replayDetail);
                                if(offset>=30){longMatches++;tangent=Math.Max(tangent,Vector2.Distance(actor.position,new Vector2(frozen[0].Bounds.X+frozen[0].Vx*offset,frozen[0].Bounds.Y+frozen[0].Vy*offset)));}if(phases[frozen]!=phase)cross++;
                            }
                            rows.Add(string.Join(",",scene,frame,type,actor.ai[0],actor.ai[1],actor.ai[2],actor.ai[3],selected,path!=null,path?.Count??0,path?.Stop.ToString()??"none",matched,longMatches,maxError.ToString("R",CultureInfo.InvariantCulture),cross,actions));
                        }
                        before=after=null;placed=null;killing=null;Require(actions>=500 && selectedCount>0 && published>0 && longMatches>=30 && tangent>8 && cross>=30,"Natural full Frost Legion paths curve and cross actual hop phases over long windows: "+scene+" actions="+actions+" pub="+published+" long="+longMatches+" cross="+cross);
                        if(type!=143 && mode!=2)Require(pauses>=16 && resumed>0,"Real third hop enters and completes the native pause: "+scene+" paused="+pauses+" resumed="+resumed);
                        if(mode==2)Require(pauses==0 && confusedHops>=5 && flightPhases.Contains(-1) && flightPhases.Contains(1),"Actual confusion resets counters and pauses while natural repeated hops cross ascent and descent: hops="+confusedHops);
                        if(mode==1)Require(turns>0,"Real wall contact exercises native same-position turn recovery.");
                        if(scene==9){Require(invalidated>0 && negativeDifferences>0 && negativeMax>.08 && recoveredLong>=30,"The real terrain mutation retains the old frozen disagreement as a negative case and recovers long qualified paths on new samples.");Console.WriteLine("SNOWMAN TERRAIN-RECOVERY invalidated="+invalidated+" negativeDifferences="+negativeDifferences+" negativeMax="+negativeMax+" recoveredLong="+recoveredLong);}
                        totalActions+=actions;totalMatches+=matched;Console.WriteLine("SNOWMAN scene="+scene+" type="+type+" actions="+actions+" matched="+matched+" long="+longMatches+" cross="+cross+" pauses="+pauses+" resumed="+resumed+" turns="+turns+" error="+maxError);
                    }
                    catch(Exception failure) when(!(failure is OutOfMemoryException)){before=after=null;placed=null;killing=null;failures.Add(scene+": "+failure.Message);Console.WriteLine("SNOWMAN FAIL scene="+scene+": "+failure);}
                }
                Require(failures.Count==0,"Native Frost Legion scene failures="+failures.Count+": "+string.Join(" | ",failures));Console.WriteLine("PASS native Frost Legion scenes=12 matched="+totalMatches+" actions="+totalActions);
            }
            finally{before=after=null;placed=null;killing=null;observer.Unpatch(update,HarmonyPatchType.All,observer.Id);observer.Unpatch(placeTile,HarmonyPatchType.All,observer.Id);observer.Unpatch(kill,HarmonyPatchType.All,observer.Id);Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"snowman-continuous.csv"),rows);}
        }
        private static void Setup(object context,int mode)
        {
            NativeCombatLiveContextChecks.FlightWorld();Main.Map=new Terraria.Map.WorldMap(Main.maxTilesX,Main.maxTilesY);NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.dayTime=false;Main.time=1800;Main.GameMode=mode==3?1:0;
            Main.getGoodWorld=Main.remixWorld=Main.zenithWorld=false;
            if(mode==1)for(int y=140;y<150;y++){Main.tile[98,y].active(true);Main.tile[98,y].type=TileID.Stone;}
            if(mode==3)for(int x=85;x<105;x++){Main.tile[x,150].halfBrick(x%3==0);if(x%3==1)Main.tile[x,150].slope(1);if(x%3==2){Main.tile[x,150].type=TileID.Platforms;Main.tile[x,150].frameX=Main.tile[x,150].frameY=0;}}
            for(int i=0;i<255;i++)Main.player[i].active=false;
            Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};var p=Main.LocalPlayer;p.position=new Vector2(1100,2400-p.height);p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.creativeGodMode=true;p.immune=true;p.immuneTime=1000000;p.fallStart=p.fallStart2=(int)p.position.Y/16;
            for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions(path:false,mouseCenter:true,clearLine:false,radius:50));
        }
        private static string State(NpcMotionState n)
        {return n.X.ToString("R",CultureInfo.InvariantCulture)+","+n.Y.ToString("R",CultureInfo.InvariantCulture)+" bits="+Bits(n.X)+","+Bits(n.Y)+" size="+n.Width+","+n.Height+" V="+n.Vx.ToString("R",CultureInfo.InvariantCulture)+","+n.Vy.ToString("R",CultureInfo.InvariantCulture)+" Vbits="+Bits(n.Vx)+","+Bits(n.Vy)+" ai="+n.A0+"/"+n.A1+"/"+n.A2+"/"+n.A3+" local2="+n.L2.ToString("R",CultureInfo.InvariantCulture)+" collide="+n.CollideX+","+n.CollideY+" dir="+n.Direction+" oldV="+n.OldVx.ToString("R",CultureInfo.InvariantCulture)+","+n.OldVy.ToString("R",CultureInfo.InvariantCulture);}
        private static string Bits(float value){return BitConverter.ToInt32(BitConverter.GetBytes(value),0).ToString("X8");}
        private static string PointState(NpcTrajectoryPoint p)
        {return p.Bounds.X.ToString("R",CultureInfo.InvariantCulture)+","+p.Bounds.Y.ToString("R",CultureInfo.InvariantCulture)+" bits="+Bits(p.Bounds.X)+","+Bits(p.Bounds.Y)+" size="+p.Bounds.Width+","+p.Bounds.Height+" V="+p.Vx.ToString("R",CultureInfo.InvariantCulture)+","+p.Vy.ToString("R",CultureInfo.InvariantCulture)+" Vbits="+Bits(p.Vx)+","+Bits(p.Vy);}
        private static string TileState(Tile t)
        {return t==null?"null":"active="+t.active()+" inactive="+t.inActive()+" type="+t.type+" slope="+t.slope()+" half="+t.halfBrick()+" frame="+t.frameX+","+t.frameY+" liquid="+t.liquid;}
        private static string ContactCells(NpcTrajectoryPoint previous,System.Collections.IDictionary cells)
        {
            var items=new List<string>();var b=previous.Bounds;
            for(int x=Math.Max(0,(int)(b.X/16)-1);x<Math.Min(Main.maxTilesX-1,(int)((b.X+b.Width)/16)+2);x++)
            for(int y=Math.Max(0,(int)(b.Y/16)-1);y<Math.Min(Main.maxTilesY-40,(int)((b.Y+b.Height)/16)+2);y++)
            {
                object cell=cells[y*Main.maxTilesX+x];string captured=cell==null?"not-captured":"active="+Get(cell,"Active")+" solid="+Get(cell,"Solid")+" type="+Get(cell,"Type")+" slope="+Get(cell,"Slope")+" half="+Get(cell,"Half")+" liquid="+Get(cell,"Liquid");
                items.Add(x+","+y+" captured{"+captured+"} native{"+TileState(Main.tile[x,y])+"}");
            }
            return string.Join(";",items);
        }
    }
}
