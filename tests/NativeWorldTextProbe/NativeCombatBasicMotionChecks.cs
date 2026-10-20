using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Linq.Expressions;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeCombatAttackMechanismChecks;

namespace NativeWorldTextProbe
{
    // Legal initial conditions only; the ordinary original-update/Host seam
    // supplies each later observation. Never edit a prediction or its cache.
    internal static class NativeCombatBasicMotionChecks
    {
        internal static void Run(object context,NpcPredictionCache cache,Action step,string output,List<double> hostCosts,List<double> worldCosts)
        {
            var host=Get(context,"CombatObservation");
            var rows=new List<string>{"scene,frame,tick,slot,generation,type,netId,x,y,vx,vy,ai0,ai1,ai2,ai3,wet,selected,published,count,stop,strokes"};
            try
            {
                string phases=Environment.GetEnvironmentVariable("JUEMINGR_ROLLING_PHASES");
                if(phases=="same-families")StructuralScenes(context,host,cache,step,output,hostCosts,worldCosts);
                else if(phases=="same-ground")foreach(int type in new[]{26,31,73,140,167,77})Ground(context,host,cache,step,rows,type);
                else if(phases!=null && phases.StartsWith("same-",StringComparison.Ordinal))NativeCombatSameCauseChecks.Run(context,host,cache,step,output,phases);
                else{Ground(context,host,cache,step,rows);EmptyFutureReason(context,host,cache,step);StructuralScenes(context,host,cache,step,output,hostCosts,worldCosts);}
            }
            finally{File.WriteAllLines(Path.Combine(output,"basic-motion-updates.csv"),rows);}
        }
        private static void Ground(object context,object host,NpcPredictionCache cache,Action step,List<string> rows,int type=77)
        {
            NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();
            Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};
            var p=Main.LocalPlayer;p.position=new Vector2(2200,2400-p.height);p.fallStart=p.fallStart2=(int)(p.position.Y/16);p.statLife=p.statLifeMax=p.statLifeMax2=500;
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1870,2400,type,Start:16,Target:Main.myPlayer);var n=Main.npc[slot];
            for(int x=119;x<=120;x++)for(int y=137;y<150;y++){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:25));
            int positive=0,missing=0,selectedCount=0,shown=0;bool movedAfter=false;
            for(int frame=0;frame<240;frame++)
            {
                // Release before vanilla's 60-update turn-away threshold, so
                // the measured segment includes pursuing through the obstacle.
                if(frame==60)for(int x=119;x<=120;x++)for(int y=137;y<150;y++)Main.tile[x,y].active(false);
                NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();
                var selection=Get(host,"Selection");var key=(NpcIdentity)Get(selection,"Target");bool selected=(bool)Get(selection,"HasTarget") && ReferenceEquals(key.Token,n);
                var path=NativeCombatPresentationChecks.CurrentPath(host);var layer=Get(host,"World");NativeCombatPresentationChecks.Project(layer);
                if(selected){selectedCount++;if(path!=null)shown++;if(n.ai[3]>0){positive++;if(path==null)missing++;}}
                Require(path==null || selected && path.Identity.Equals(key) && path.SampleTick==Main.GameUpdateCount,"Basic motion consumes only the current selected display window.");
                rows.Add(Csv("armored-block-recover",frame,Main.GameUpdateCount,slot,n.generation,n.type,n.netID,n.position.X,n.position.Y,n.velocity.X,n.velocity.Y,n.ai[0],n.ai[1],n.ai[2],n.ai[3],n.wet,selected,path!=null,path?.Count??0,path?.Stop.ToString()??"none",Get(layer,"StrokeCount")));
                movedAfter|=frame>60 && n.position.X>1920;
                if(!n.active || n.life<=0 || p.dead)throw new InvalidOperationException("Ground scenario ended before its planned original recovery.");
            }
            Console.WriteLine("BASIC-GROUND type="+type+" selected="+selectedCount+" shown="+shown+" positiveA3="+positive+" missingWithPositive="+missing+" recovered="+movedAfter);
            Require(positive>10 && movedAfter,"Original obstacle creates positive blocked counts and subsequently recovers.");
            Require(missing==0,"A positive ordinary original blocked count must not remove all predicted futures.");
        }
        private static void Require(bool value,string text){if(!value)throw new InvalidOperationException(text);}
        private static void EmptyFutureReason(object context,object host,NpcPredictionCache cache,Action step)
        {
            var selection=Get(host,"Selection");var key=(NpcIdentity)Get(selection,"Target");var n=Main.npc[key.Slot];
            n.buffImmune[BuffID.Slow]=false;n.AddBuff(BuffID.Slow,500);
            NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();
            Require(cache.Read(0)!=null,"Locked NPC non-motion Slow does not cancel a modeled body path.");
            n.buffImmune[198]=false;n.AddBuff(198,2);NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();
            var source=Get(host,"Prediction");key=(NpcIdentity)Get(selection,"Target");
            object[] reason={key,(long)Main.GameUpdateCount,PredictionStop.None,PredictionFailureLayer.None};
            bool hasReason=(bool)source.GetType().GetMethod("EmptyFutureReason",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(source,reason);
            Console.WriteLine("BASIC-REASON cacheEmpty="+(cache.Read(0)==null)+" reason="+reason[2]+" layer="+reason[3]);
            Require(cache.Read(0)==null,"An unmodeled current motion state supplies no invented future.");
            Require(NativeCombatPresentationChecks.CurrentPath(host)==null,"An unmodeled current motion state also retires its previously leased display.");
            Require(hasReason && (PredictionStop)reason[2]==PredictionStop.BuffTransition,"An exact selected target retains its real first-step motion stop reason without an HUD string.");
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:false,mouseCenter:true,clearLine:false,radius:25));step();
            Require(Get(host,"DisplayPath")==null,"Disabling path display leaves no displayed future.");
        }
        private struct MotionRow
        {
            internal long Tick,Capture,Sample,Lease;internal int Frame,Slot,Generation,Type,NetId,Style,Width,Height,Count,Strokes,Stop,Quality,Strategy,Life,PlayerLife;
            internal float X,Y,Vx,Vy,A0,A1,A2,A3,L1,P1X,P1Y,P15X,P15Y;
            internal bool Wet,Selected,Published,Eligible,Friendly,Immortal,DontTakeDamage;internal double Host,World,Observer;
        }
        private static void StructuralScenes(object context,object host,NpcPredictionCache cache,Action step,string output,List<double> hostCosts,List<double> worldCosts)
        {
            var source=Get(host,"Prediction");
            bool qualityMode=Environment.GetEnvironmentVariable("JUEMINGR_ROLLING_QUALITY")=="1";
            var selection=Get(host,"Selection");var layer=Get(host,"World");
            var readTarget=Reader<NpcIdentity>(selection,"Target");var readChosen=Reader<bool>(selection,"HasTarget");var readStrokes=Reader<int>(layer,"StrokeCount");
            Console.WriteLine("BASIC-OBSERVATION bounded numeric records; quality="+qualityMode+"; host includes Source; World prepare is CPU only; draw/FPS unmeasured");
            using(var quality=qualityMode?new NativeCombatRollingQualityChecks(source,output):null)
            {
                var failures=new List<string>();
                foreach(int type in new[]{55,57,58,65,102,157,241,465,592,607,615,688,692,56,43,163,238,164,165,236,237,239,240,530,531})
                {
                    string filter=Environment.GetEnvironmentVariable("JUEMINGR_ROLLING_PHASES");
                    if(!string.IsNullOrEmpty(filter) && filter!="same-families" && Array.IndexOf(filter.Split(','),"basic-"+type)<0)continue;
                    NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();
                    Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};
                    var p=Main.LocalPlayer;p.position=new Vector2(3000,2400-p.height);p.fallStart=p.fallStart2=(int)(p.position.Y/16);p.statLife=p.statLifeMax=p.statLifeMax2=500;
                    Main.dayTime=false;Main.GameMode=0;
                    bool fish=Array.IndexOf(new[]{55,57,58,65,102,157,241,465,592,607,615,688,692},type)>=0,plant=type==56 || type==43;
                    if(fish)for(int x=100;x<180;x++)for(int y=140;y<150;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(0);}
                    if(plant){Main.tile[120,145].active(true);Main.tile[120,145].inActive(true);Main.tile[120,145].type=1;}
                    if(!fish && !plant)for(int x=110;x<150;x++)for(int y=128;y<150;y++)Main.tile[x,y].wall=1;
                    int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),fish?2300:1900,fish?2260:2400,type,Start:16,Target:Main.myPlayer,ai0:plant?120:0,ai1:plant?145:0);
                    var n=Main.npc[slot];
                    if(fish){n.velocity=new Vector2(.5f,-3);n.wet=true;n.ai[0]=-1;n.direction=1;}
                    if(plant){n.position=new Vector2(120*16+8+(type==43?250:150),145*16+8);n.velocity=new Vector2(2,0);}
                    if(!fish && !plant && n.aiStyle==3){n.localAI[1]=12;n.velocity.X=2;}
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:25));
                    var receives=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true).GetMethod("Receives",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
                    var reason=source.GetType().GetMethod("EmptyFutureReason",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                    var records=new MotionRow[480];int selected=0,published=0,legal=0,eligibleCount=0,expectedBoundaries=0,retiredFrames=0,retirements=0,recoveries=0,flopRecoveries=0,flopAirMatches=0,unexpectedMissing=0,eligibleBlank=0,longestEligibleBlank=0,blank=0,longest=0,wetToDry=0,dryToWet=0,forms=0,turns=0,priorForm=n.type;
                    long recoveryAfter=-1,recoveryDeadline=-1;bool recoveryFlop=false,recoveryBoundary=false,recoveryExpired=false;NpcTrajectory recoveredAir=null;
                    bool priorWet=n.wet;float priorVx=n.velocity.X;int g0=GC.CollectionCount(0),g1=GC.CollectionCount(1),g2=GC.CollectionCount(2);string scene="basic-"+type;
                    quality?.ChangeScene(scene,false);
                    for(int frame=0;frame<records.Length;frame++)
                    {
                        NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();NativeCombatPresentationChecks.Project(layer);
                        long watch=Stopwatch.GetTimestamp();quality?.Observe();
                        var key=readTarget();bool chosen=readChosen() && ReferenceEquals(key.Token,n);
                        var path=NativeCombatPresentationChecks.CurrentPath(host);
                        if(recoveryAfter>=0 && recoveryDeadline>=0 && (long)Main.GameUpdateCount>recoveryDeadline)recoveryExpired=true;
                        if(recoveredAir!=null)
                        {
                            if((long)Main.GameUpdateCount==recoveredAir.SampleTick+1)
                            {
                                var point=recoveredAir[1];
                                Require(!n.wet && !point.Wet && Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),n.position)<=.08 && StoredBits(point.Vx)==StoredBits(n.velocity.X) && StoredBits(point.Vy)==StoredBits(n.velocity.Y),"The newly observed dry airborne velocity must retain the deterministic native next action after the random flop.");
                                flopAirMatches++;recoveredAir=null;
                            }
                        }
                        if(path!=null && recoveryAfter>=0 && path.CaptureTick>=recoveryAfter)
                        {
                            recoveries++;if(recoveryFlop){Require(!n.wet && n.velocity.Y!=0 && path.Count>1,"Random-flop recovery must observe the actual airborne state, not invent another random launch.");recoveredAir=path;flopRecoveries++;}
                            Console.WriteLine("BASIC-RECOVERY type="+type+" frame="+frame+" boundary="+recoveryAfter+" capture="+path.CaptureTick+" sample="+path.SampleTick+" count="+path.Count+" randomFlop="+recoveryFlop);
                            recoveryAfter=recoveryDeadline=-1;recoveryFlop=recoveryBoundary=false;
                        }
                        PredictionStop recordedStop=path?.Stop??PredictionStop.None;
                        if(chosen && path==null)
                        {
                            object[] args={key,(long)Main.GameUpdateCount,PredictionStop.None,PredictionFailureLayer.None};bool known=(bool)reason.Invoke(source,args);recordedStop=(PredictionStop)args[2];
                            bool special=type==615 && (n.ai[2]!=0 || n.ai[3]>=299) || type==688 && (n.ai[2]==1 || n.justHit);
                            // The native dolphin's next action may choose a
                            // threshold from [300,1200). This current observed
                            // clock proves the independent boundary even when
                            // Source has not published an outcome this update;
                            // its stale out-parameter is never that proof.
                            bool nativeChoice=NativeChoiceBoundary(n);
                            bool observedChoice=ObservedActionFlop(n,frame==0?default(MotionRow):records[frame-1]);
                            bool flop=false;
                            long finiteEnd;
                            bool retired=RetiredDisplay(host,key,n,frame==0?default(MotionRow):records[frame-1],out flop,out finiteEnd);
                            if(nativeChoice || observedChoice || special && known && (recordedStop==PredictionStop.RandomDecision || recordedStop==PredictionStop.UnsupportedMechanism || recordedStop==PredictionStop.PhaseBoundary))
                            {
                                expectedBoundaries++;
                                if(nativeChoice && recoveryAfter<0){recoveryAfter=(long)Main.GameUpdateCount;recoveryBoundary=true;retirements++;}
                                if(observedChoice){if(recoveryAfter<0){recoveryAfter=(long)Main.GameUpdateCount;retirements++;}recoveryFlop=true;recoveryBoundary=false;Console.WriteLine("BASIC-OBSERVED-CHOICE type="+type+" tick="+Main.GameUpdateCount+" actual="+n.velocity+" ai2="+n.ai[2]+" ai3="+n.ai[3]);}
                            }
                            else if(retired)
                            {
                                retiredFrames++;
                                // This first real prefix fixes the available
                                // recovery interval. Later raw calculations
                                // cannot roll an unexplained blank forward.
                                if(recoveryAfter<0){recoveryAfter=(long)Main.GameUpdateCount;recoveryDeadline=finiteEnd;recoveryFlop=flop;retirements++;}
                            }
                            else
                            {
                                if(unexpectedMissing==0)
                                {
                                    var raw=cache.Read(0);var prior=Get(host,"displayPath") as NpcTrajectory;
                                    long age=prior==null?-1:(long)Main.GameUpdateCount-prior.SampleTick;
                                    var point=prior!=null && age>=0 && age<prior.Count?prior[(int)age]:default(NpcTrajectoryPoint);
                                    Console.WriteLine("BASIC-FIRST-ABSENT type="+type+" frame="+frame+" now="+Main.GameUpdateCount+" known="+known+" reason="+recordedStop+" raw="+(raw!=null)+" rawIdentity="+(raw!=null && raw.Identity.Equals(key))+" rawCapture="+(raw?.CaptureTick??-1)+" rawSample="+(raw?.SampleTick??-1)+" rawCount="+(raw?.Count??0)+" rawPaused="+(raw!=null && raw.BudgetPaused)+" priorCapture="+(prior?.CaptureTick??-1)+" priorSample="+(prior?.SampleTick??-1)+" priorCount="+(prior?.Count??0)+" priorAge="+age+" lease="+Get(host,"displayLeaseTick")+" optionalTick="+Get(host,"optionalTick")+" shortTick="+Get(host,"displayShortTick")+" pathFailed="+Get(host,"pathFailed")+" native="+n.position+"/"+n.velocity+" phase="+n.ai[0]+" wet="+n.wet+" collide="+n.noTileCollide+" point="+point.Bounds.X+","+point.Bounds.Y+"/"+point.Vx+","+point.Vy+" phase="+point.Phase+" wet="+point.Wet+" collide="+point.NoTileCollide);
                                }
                                unexpectedMissing++;
                            }
                        }
                        if(n.active && n.life>0)legal++;
                        bool eligible=(bool)receives.Invoke(null,new object[]{n,false});if(eligible)eligibleCount++;
                        if(eligible && (!chosen || path==null)){eligibleBlank++;longestEligibleBlank=Math.Max(longestEligibleBlank,eligibleBlank);}else eligibleBlank=0;
                        if(chosen)selected++;if(chosen && path!=null){published++;blank=0;}else{blank++;longest=Math.Max(longest,blank);}
                        if(path!=null)Require(chosen && path.Identity.Equals(key) && path.SampleTick==Main.GameUpdateCount,"Structural scene consumes its current selected display window.");
                        if(priorWet && !n.wet)wetToDry++;if(!priorWet && n.wet)dryToWet++;if(priorForm!=n.type)forms++;if(priorVx*n.velocity.X<0)turns++;
                        if(priorForm!=n.type)Require(n.localAI[1]==12,"Native conversion frame preserves cooldown twelve.");
                        priorWet=n.wet;priorForm=n.type;priorVx=n.velocity.X;
                        var first=path!=null && path.Count>1?path[1]:default(NpcTrajectoryPoint);var near=path!=null && path.Count>15?path[15]:default(NpcTrajectoryPoint);
                        quality?.Capture(scene,frame,path,type==236 && frame<30?1:30);
                        records[frame]=new MotionRow{Eligible=eligible,Friendly=n.friendly,Immortal=n.immortal,DontTakeDamage=n.dontTakeDamage,Tick=(long)Main.GameUpdateCount,Capture=path?.CaptureTick??-1,Sample=path?.SampleTick??-1,Lease=(long)Get(host,"displayLeaseTick"),Frame=frame,Slot=slot,Generation=n.generation,Type=n.type,NetId=n.netID,Style=n.aiStyle,Width=n.width,Height=n.height,X=n.position.X,Y=n.position.Y,Vx=n.velocity.X,Vy=n.velocity.Y,A0=n.ai[0],A1=n.ai[1],A2=n.ai[2],A3=n.ai[3],L1=n.localAI[1],Wet=n.wet,Selected=chosen,Published=path!=null,Count=path?.Count??0,Stop=(int)recordedStop,Quality=(int)(path?.Quality??PredictionQuality.Conditional),Strategy=(int)(path?.Strategy??PredictionStrategy.Model),Strokes=readStrokes(),Life=n.life,PlayerLife=p.statLife,P1X=first.Bounds.X,P1Y=first.Bounds.Y,P15X=near.Bounds.X,P15Y=near.Bounds.Y,Host=hostCosts[hostCosts.Count-1],World=worldCosts[worldCosts.Count-1],Observer=(Stopwatch.GetTimestamp()-watch)*1000.0/Stopwatch.Frequency};
                        Require(n.active && n.life>0 && !p.dead,"Structural scene ended before its planned original updates.");
                    }
                    var lines=new string[records.Length+1];lines[0]="frame,tick,slot,generation,type,netId,style,width,height,x,y,vx,vy,ai0,ai1,ai2,ai3,local1,wet,selected,published,count,stop,quality,strategy,strokes,life,playerLife,p1x,p1y,p15x,p15y,hostMs,worldPrepareMs,observerMs,eligible,friendly,immortal,dontTakeDamage,captureTick,sampleTick,displayLeaseTick";
                    for(int i=0;i<records.Length;i++){var r=records[i];lines[i+1]=Csv(r.Frame,r.Tick,r.Slot,r.Generation,r.Type,r.NetId,r.Style,r.Width,r.Height,r.X,r.Y,r.Vx,r.Vy,r.A0,r.A1,r.A2,r.A3,r.L1,r.Wet,r.Selected,r.Published,r.Count,r.Stop,r.Quality,r.Strategy,r.Strokes,r.Life,r.PlayerLife,r.P1X,r.P1Y,r.P15X,r.P15Y,r.Host,r.World,r.Observer,r.Eligible,r.Friendly,r.Immortal,r.DontTakeDamage,r.Capture,r.Sample,r.Lease);}
                    File.WriteAllLines(Path.Combine(output,scene+"-continuous.csv"),lines);
                    Console.WriteLine("BASIC-STRUCTURE type="+type+" legal="+legal+" eligible="+eligibleCount+" selected="+selected+" shown="+published+" longestBlank="+longest+" longestEligibleBlank="+longestEligibleBlank+" wetDry="+wetToDry+" dryWet="+dryToWet+" forms="+forms+" vxTurns="+turns+" gc="+(GC.CollectionCount(0)-g0)+"/"+(GC.CollectionCount(1)-g1)+"/"+(GC.CollectionCount(2)-g2));
                    Console.WriteLine("BASIC-BOUNDARIES type="+type+" eligible="+eligibleCount+" expectedIndependentStop="+expectedBoundaries+" retiredFrames="+retiredFrames+" retirements="+retirements+" recoveries="+recoveries+" flopRecoveries="+flopRecoveries+" flopAirMatches="+flopAirMatches+" unexpectedMissing="+unexpectedMissing);
                    // Complete the existing independent scene matrix before
                    // reporting all failed contracts. A failure remains a
                    // failure; the next fixture cannot change its evidence.
                    try
                    {
                        if(eligibleCount==0)Require(selected==0 && published==0,"Non-target family members remain outside production target legality.");
                        else Require(selected>=eligibleCount-1 && published>0 && published+expectedBoundaries+retiredFrames>=eligibleCount-1 && unexpectedMissing==0,"Every actually eligible observation supplies a current route, its independently proven boundary, or an honestly retired prefix awaiting a real fresh capture.");
                        Require(!recoveryExpired && (recoveryAfter<0 || recoveryBoundary && NativeChoiceBoundary(n)) && flopRecoveries==flopAirMatches,"A retired display must recover from a genuine new capture within its first actual finite prefix and the original observation window; actual airborne deterministic motion is not excused by the preceding random choice.");
                        if(fish && eligibleCount>0 && type!=615 && type!=688)Require(wetToDry>0 && dryToWet>0,"Original fish actually exits water and returns.");
                        if(plant)Require(turns>0,"Original plant actually brakes and swings back.");
                        if(!fish && !plant)Require(forms>0,"Original spider actually crosses a local wall boundary.");
                    }
                    catch(InvalidOperationException failure){failures.Add(scene+": "+failure.Message);Console.WriteLine("BASIC-STRUCTURE FAIL "+scene+": "+failure.Message);}
                }
                Require(failures.Count==0,"Basic structural scene failures="+failures.Count+": "+string.Join(" | ",failures));
            }
        }
        private static bool NativeChoiceBoundary(NPC n)
        {
            // Original AI16 draws a new [300,1200) threshold on each native
            // action while this dolphin has not selected its next action.
            // This is the CURRENT native clock, not a remembered Source stop.
            if(n.aiStyle!=16)return false;
            if(n.type==615 && n.ai[2]==0 && n.ai[3]>=299)return true;
            // Common's next native server action chooses the dry launch;
            // selected dolphin actions and the floating/hit-response 688
            // branch return before Common and receive no such exemption.
            return Main.netMode!=1 && !n.wet && n.velocity.Y==0 && (n.type==615 && n.ai[2]==0 || n.type==688 && n.ai[2]!=1 && !n.justHit);
        }
        private static bool ObservedActionFlop(NPC n,MotionRow previous)
        {
            if(n.type!=615 && n.type!=688 || n.ai[2]!=0 || previous.A2!=0 || Main.netMode==1 || n.aiStyle!=16 || n.wet || previous.Wet || previous.Vy!=0 || previous.Tick+1!=(long)Main.GameUpdateCount || previous.Type!=n.type || previous.Generation!=n.generation)return false;
            bool x=false,y=false;
            for(int value=-20;value<20;value++)x|=StoredBits(n.velocity.X)==StoredBits(value*.1f);
            for(int value=-50;value<-20;value++){float launch=BitConverter.ToSingle(BitConverter.GetBytes(value*.1f),0);y|=StoredBits(n.velocity.Y)==StoredBits(launch+.3f);}
            return x && y && StoredBits(n.position.X)==StoredBits(previous.X+n.velocity.X) && StoredBits(n.position.Y)==StoredBits(previous.Y+n.velocity.Y);
        }
        private static bool RetiredDisplay(object host,NpcIdentity key,NPC n,MotionRow previous,out bool flop,out long finiteEnd)
        {
            flop=false;finiteEnd=-1;long now=(long)Main.GameUpdateCount;
            var prior=Get(host,"displayPath") as NpcTrajectory;
            if(prior==null || !prior.Identity.Equals(key) || (long)Get(host,"displayLeaseTick")==now || (bool)Get(host,"pathFailed"))return false;
            long age=now-prior.SampleTick;
            // These dry-flop representatives are explicitly qualified in the
            // AI16 contract. Prove the actual new random-action premise from
            // consecutive ORIGINAL observations and native discrete ranges.
            // Wet motion, airborne gravity, clients, shark damping and the two
            // separately bounded action families receive no such exception.
            bool candidate=Main.netMode!=1 && n.aiStyle==16 && n.type!=65 && n.type!=692 && n.type!=615 && n.type!=688 && !n.wet && !previous.Wet && previous.Vy==0 && previous.Tick+1==now && previous.Type==n.type && previous.Generation==n.generation && age==1 && prior.Count>1 && prior.Quality==PredictionQuality.StructuredApproximation && (prior.Assumptions&PredictionAssumption.RandomRepresentative)!=0;
            if(candidate)
            {
                var point=prior[1];bool nativeX=false,nativeY=false;
                for(int value=-20;value<20;value++)nativeX|=n.velocity.X==value*.1f;
                for(int value=-50;value<-20;value++)nativeY|=n.velocity.Y==value*.1f+.3f;
                bool storedX=false,storedY=false;
                for(int value=-20;value<20;value++)storedX|=StoredBits(n.velocity.X)==StoredBits(value*.1f);
                for(int value=-50;value<-20;value++){float launch=BitConverter.ToSingle(BitConverter.GetBytes(value*.1f),0);storedY|=StoredBits(n.velocity.Y)==StoredBits(launch+.3f);}
                Console.WriteLine("BASIC-FLOP-PREDICATE tick="+now+" candidate="+candidate+" nativeX="+nativeX+" nativeY="+nativeY+" storedX="+storedX+" storedY="+storedY+" representativeX="+(Math.Abs(point.Vx)==.8f)+" representativeY="+(point.Vy==-3.6f+.3f)+" positionX="+(n.position.X==previous.X+n.velocity.X)+" positionY="+(n.position.Y==previous.Y+n.velocity.Y)+" actualBits="+StoredBits(n.velocity.X)+","+StoredBits(n.velocity.Y)+","+StoredBits(n.position.X)+","+StoredBits(n.position.Y)+" expressionBits="+StoredBits(previous.X+n.velocity.X)+","+StoredBits(previous.Y+n.velocity.Y)+" representativeBits="+StoredBits(point.Vx)+","+StoredBits(point.Vy)+","+StoredBits(-3.6f+.3f));
                // Native stores each RNG product, gravity addition and final
                // position into Single fields. Compare those actual stores;
                // x86 extended intermediates are not another legal range.
                flop=StoredBits(point.Vy)==StoredBits(-3.6f+.3f) && StoredBits(Math.Abs(point.Vx))==StoredBits(.8f) && storedX && storedY && (point.Vx!=n.velocity.X || point.Vy!=n.velocity.Y) && StoredBits(n.position.X)==StoredBits(previous.X+n.velocity.X) && StoredBits(n.position.Y)==StoredBits(previous.Y+n.velocity.Y);
                if(flop){finiteEnd=prior.SampleTick+prior.Count-1;Console.WriteLine("BASIC-RETIRE type="+n.type+" tick="+now+" kind=observed-native-random-flop capture="+prior.CaptureTick+" sample="+prior.SampleTick+" assumptions="+prior.Assumptions+" quality="+prior.Quality+" actual="+n.velocity+" representative="+point.Vx+","+point.Vy);return true;}
            }
            int maximumAge=(int)host.GetType().GetField("DisplayMaximumAge",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).GetRawConstantValue();
            var raw=((NpcPredictionCache)Get(Get(host,"Prediction"),"Cache")).Read(0);
            if(age<=maximumAge || raw==null || !raw.Identity.Equals(key) || raw.SampleTick>now || raw.CaptureTick>raw.SampleTick || raw.Count<2 || raw.Count>NpcPredictionCache.Horizon+1)return false;
            long rawAge=now-raw.SampleTick;
            NpcTrajectory remaining;
            if(rawAge>=raw.Count-1 || !raw.TryWindow(now,raw.Count-(int)rawAge-1,raw.Version,out remaining))return false;
            var origin=remaining[0];
            bool fresh=origin.Bounds.X==n.position.X && origin.Bounds.Y==n.position.Y && origin.Bounds.Width==n.width && origin.Bounds.Height==n.height && origin.Vx==n.velocity.X && origin.Vy==n.velocity.Y && origin.Phase==n.ai[0] && origin.Wet==n.wet && origin.NoTileCollide==n.noTileCollide;
            if(fresh){finiteEnd=raw.SampleTick+raw.Count-1;Console.WriteLine("BASIC-RETIRE type="+n.type+" tick="+now+" kind=expired-display-with-finite-source-unleased age="+age+" maximumAge="+maximumAge+" sourceCapture="+raw.CaptureTick+" sourceSample="+raw.SampleTick+" sourceCount="+raw.Count+" remaining="+remaining.Count+" finiteEnd="+finiteEnd+" sourcePaused="+raw.BudgetPaused);}
            return fresh;
        }
        private static int StoredBits(float value){return BitConverter.ToInt32(BitConverter.GetBytes(value),0);}
        private static Func<T> Reader<T>(object owner,string name)
        {return Expression.Lambda<Func<T>>(Expression.PropertyOrField(Expression.Constant(owner),name)).Compile();}
        private static string Csv(params object[] values){var fields=new string[values.Length];for(int i=0;i<fields.Length;i++)fields[i]="\""+Convert.ToString(values[i],CultureInfo.InvariantCulture).Replace("\"","\"\"")+"\"";return string.Join(",",fields);}
    }
}
