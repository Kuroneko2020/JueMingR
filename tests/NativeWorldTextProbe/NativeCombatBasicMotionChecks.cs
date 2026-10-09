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
            var rows=new List<string>{"scene,frame,tick,slot,generation,type,netId,x,y,vx,vy,ai0,ai1,ai2,ai3,wet,selected,published,count,stop,strokes,text"};
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
                var path=cache.Read(0);var layer=Get(host,"World");NativeCombatPresentationChecks.Project(layer);
                if(selected){selectedCount++;if(path!=null)shown++;if(n.ai[3]>0){positive++;if(path==null)missing++;}}
                if(path!=null && (!selected || !path.Identity.Equals(key) || path.CaptureTick!=Main.GameUpdateCount))throw new InvalidOperationException("Basic motion consumed a stale or different instance.");
                rows.Add(Csv("armored-block-recover",frame,Main.GameUpdateCount,slot,n.generation,n.type,n.netID,n.position.X,n.position.Y,n.velocity.X,n.velocity.Y,n.ai[0],n.ai[1],n.ai[2],n.ai[3],n.wet,selected,path!=null,path?.Count??0,path?.Stop.ToString()??"none",Get(layer,"StrokeCount"),Get(layer,"pathText")));
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
            string text=(string)Get(Get(host,"World"),"pathText");
            Console.WriteLine("BASIC-REASON cacheEmpty="+(cache.Read(0)==null)+" text="+(text??"<null>"));
            Require(cache.Read(0)==null,"An unmodeled current motion state supplies no invented future.");
            Require(!string.IsNullOrEmpty(text) && text.Contains("状态"),"An exact selected target retains a player-readable first-step motion stop reason.");
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:false,mouseCenter:true,clearLine:false,radius:25));step();
            Require(Get(Get(host,"World"),"pathText")==null,"Disabling path display clears its empty-future reason.");
        }
        private struct MotionRow
        {
            internal long Tick;internal int Frame,Slot,Generation,Type,NetId,Style,Width,Height,Count,Strokes,Stop,Quality,Strategy,Life,PlayerLife;
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
                    var records=new MotionRow[480];int selected=0,published=0,legal=0,eligibleCount=0,expectedBoundaries=0,unexpectedMissing=0,eligibleBlank=0,longestEligibleBlank=0,blank=0,longest=0,wetToDry=0,dryToWet=0,forms=0,turns=0,priorForm=n.type;
                    bool priorWet=n.wet;float priorVx=n.velocity.X;int g0=GC.CollectionCount(0),g1=GC.CollectionCount(1),g2=GC.CollectionCount(2);string scene="basic-"+type;
                    quality?.ChangeScene(scene,false);
                    for(int frame=0;frame<records.Length;frame++)
                    {
                        NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();NativeCombatPresentationChecks.Project(layer);
                        long watch=Stopwatch.GetTimestamp();quality?.Observe();
                        var key=readTarget();bool chosen=readChosen() && ReferenceEquals(key.Token,n);
                        var path=cache.Read(0);
                        PredictionStop recordedStop=path?.Stop??PredictionStop.None;
                        if(chosen && path==null)
                        {
                            object[] args={key,(long)Main.GameUpdateCount,PredictionStop.None,PredictionFailureLayer.None};bool known=(bool)reason.Invoke(source,args);recordedStop=(PredictionStop)args[2];
                            bool special=type==615 && (n.ai[2]!=0 || n.ai[3]>=299) || type==688 && (n.ai[2]==1 || n.justHit);
                            if(special && known && (recordedStop==PredictionStop.RandomDecision || recordedStop==PredictionStop.UnsupportedMechanism || recordedStop==PredictionStop.PhaseBoundary))expectedBoundaries++;
                            else unexpectedMissing++;
                        }
                        if(n.active && n.life>0)legal++;
                        bool eligible=(bool)receives.Invoke(null,new object[]{n,false});if(eligible)eligibleCount++;
                        if(eligible && (!chosen || path==null)){eligibleBlank++;longestEligibleBlank=Math.Max(longestEligibleBlank,eligibleBlank);}else eligibleBlank=0;
                        if(chosen)selected++;if(chosen && path!=null){published++;blank=0;}else{blank++;longest=Math.Max(longest,blank);}
                        if(path!=null)Require(chosen && path.Identity.Equals(key) && path.CaptureTick==Main.GameUpdateCount,"Structural scene consumed a stale or different form.");
                        if(priorWet && !n.wet)wetToDry++;if(!priorWet && n.wet)dryToWet++;if(priorForm!=n.type)forms++;if(priorVx*n.velocity.X<0)turns++;
                        if(priorForm!=n.type)Require(n.localAI[1]==12,"Native conversion frame preserves cooldown twelve.");
                        priorWet=n.wet;priorForm=n.type;priorVx=n.velocity.X;
                        var first=path!=null && path.Count>1?path[1]:default(NpcTrajectoryPoint);var near=path!=null && path.Count>15?path[15]:default(NpcTrajectoryPoint);
                        quality?.Capture(scene,frame,path,type==236 && frame<30?1:30);
                        records[frame]=new MotionRow{Eligible=eligible,Friendly=n.friendly,Immortal=n.immortal,DontTakeDamage=n.dontTakeDamage,Tick=(long)Main.GameUpdateCount,Frame=frame,Slot=slot,Generation=n.generation,Type=n.type,NetId=n.netID,Style=n.aiStyle,Width=n.width,Height=n.height,X=n.position.X,Y=n.position.Y,Vx=n.velocity.X,Vy=n.velocity.Y,A0=n.ai[0],A1=n.ai[1],A2=n.ai[2],A3=n.ai[3],L1=n.localAI[1],Wet=n.wet,Selected=chosen,Published=path!=null,Count=path?.Count??0,Stop=(int)recordedStop,Quality=(int)(path?.Quality??PredictionQuality.Conditional),Strategy=(int)(path?.Strategy??PredictionStrategy.Model),Strokes=readStrokes(),Life=n.life,PlayerLife=p.statLife,P1X=first.Bounds.X,P1Y=first.Bounds.Y,P15X=near.Bounds.X,P15Y=near.Bounds.Y,Host=hostCosts[hostCosts.Count-1],World=worldCosts[worldCosts.Count-1],Observer=(Stopwatch.GetTimestamp()-watch)*1000.0/Stopwatch.Frequency};
                        Require(n.active && n.life>0 && !p.dead,"Structural scene ended before its planned original updates.");
                    }
                    var lines=new string[records.Length+1];lines[0]="frame,tick,slot,generation,type,netId,style,width,height,x,y,vx,vy,ai0,ai1,ai2,ai3,local1,wet,selected,published,count,stop,quality,strategy,strokes,life,playerLife,p1x,p1y,p15x,p15y,hostMs,worldPrepareMs,observerMs,eligible,friendly,immortal,dontTakeDamage";
                    for(int i=0;i<records.Length;i++){var r=records[i];lines[i+1]=Csv(r.Frame,r.Tick,r.Slot,r.Generation,r.Type,r.NetId,r.Style,r.Width,r.Height,r.X,r.Y,r.Vx,r.Vy,r.A0,r.A1,r.A2,r.A3,r.L1,r.Wet,r.Selected,r.Published,r.Count,r.Stop,r.Quality,r.Strategy,r.Strokes,r.Life,r.PlayerLife,r.P1X,r.P1Y,r.P15X,r.P15Y,r.Host,r.World,r.Observer,r.Eligible,r.Friendly,r.Immortal,r.DontTakeDamage);}
                    File.WriteAllLines(Path.Combine(output,scene+"-continuous.csv"),lines);
                    Console.WriteLine("BASIC-STRUCTURE type="+type+" legal="+legal+" eligible="+eligibleCount+" selected="+selected+" shown="+published+" longestBlank="+longest+" longestEligibleBlank="+longestEligibleBlank+" wetDry="+wetToDry+" dryWet="+dryToWet+" forms="+forms+" vxTurns="+turns+" gc="+(GC.CollectionCount(0)-g0)+"/"+(GC.CollectionCount(1)-g1)+"/"+(GC.CollectionCount(2)-g2));
                    Console.WriteLine("BASIC-BOUNDARIES type="+type+" eligible="+eligibleCount+" expectedIndependentStop="+expectedBoundaries+" unexpectedMissing="+unexpectedMissing);
                    if(eligibleCount==0)Require(selected==0 && published==0,"Non-target family members remain outside production target legality.");
                    else Require(selected>=eligibleCount-1 && published+expectedBoundaries>=eligibleCount-1 && unexpectedMissing==0 && (type==615 || type==688 || longestEligibleBlank<2),"Every actually eligible observation either supplies a route or its explicit independent mechanism boundary.");
                    if(fish && eligibleCount>0 && type!=615 && type!=688)Require(wetToDry>0 && dryToWet>0,"Original fish actually exits water and returns.");
                    if(plant)Require(turns>0,"Original plant actually brakes and swings back.");
                    if(!fish && !plant)Require(forms>0,"Original spider actually crosses a local wall boundary.");
                }
            }
        }
        private static Func<T> Reader<T>(object owner,string name)
        {return Expression.Lambda<Func<T>>(Expression.PropertyOrField(Expression.Constant(owner),name)).Compile();}
        private static string Csv(params object[] values){var fields=new string[values.Length];for(int i=0;i<fields.Length;i++)fields[i]="\""+Convert.ToString(values[i],CultureInfo.InvariantCulture).Replace("\"","\"\"")+"\"";return string.Join(",",fields);}
    }
}
