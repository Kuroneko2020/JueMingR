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
    internal static class NativeCombatAmbientFlightChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Action<NPC> beforeAction;
        private static void BeforeAction(NPC __instance){beforeAction?.Invoke(__instance);}
        internal static void Boundaries()
        {
            Type model=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcAmbientFlightMotion",true);
            MethodInfo known=model.GetMethod("Known",Flags),action=model.GetMethod("Step",Flags);
            int[] types={472,521,195,662,686},styles={86,86,42,122,125};
            for(int i=0;i<types.Length;i++)
            {
                var state=new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,types[i],types[i]),Style=styles[i]};
                Require((bool)known.Invoke(null,new object[]{state}),"Exact locked ambient member "+types[i]);
                state.Identity=new NpcIdentity(1,new object(),16,1,224,224);
                Require(!(bool)known.Invoke(null,new object[]{state}),"Shared style alone cannot claim ambient family coverage "+styles[i]);
            }
            // This boundary is established by the REAL referenced XNA scalar
            // behavior used in locked native NPC.cs, independent of our model.
            Vector2 zero=Vector2.Zero;zero.Normalize();
            Require(float.IsNaN(zero.X) && float.IsNaN(zero.Y),"Native zero normalization is invalid before post-normalize zero fallback.");
            foreach(int type in new[]{472,521,662})
            {
                var state=new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,type,type),Style=type==662?122:86,Width=18,Height=40,Active=true,Target=0,L0=120,CrowdCaptured=true,CrowdComplete=true,AimedTargetCaptured=true,AimedTargetValid=true};
                var peer=state;peer.Identity=new NpcIdentity(1,new object(),17,1,type,type);
                var group=new[]{state,peer};var environment=new PredictionEnvironment{PlayerX=200,PlayerY=40,PlayerWidth=20,PlayerHeight=42};
                var arguments=new object[]{state,environment,null,false,1,PredictionStop.None,group,2};
                bool result=(bool)action.Invoke(null,arguments);
                Require(!result && (PredictionStop)arguments[5]==PredictionStop.InvalidState,"Coincident native crowd cannot publish a finite invented escape "+type);
                state.CrowdComplete=false;arguments=new object[]{state,environment,null,false,1,PredictionStop.None,group,2};
                result=(bool)action.Invoke(null,arguments);
                Require(!result && (PredictionStop)arguments[5]==PredictionStop.MissingDependency,"Incomplete same-family closure must stop explicitly "+type);
            }
            Console.WriteLine("PASS ambient exact-member and native zero-normalization/closure boundaries");
        }
        // The host callback owns the full existing isolated native schedule.
        // Every frozen forecast is compared to the SAME continuing actor; a
        // refreshed line after a turn cannot certify an earlier forecast.
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");
            Require(GetOptional(source,"Native")==null,"Ambient families use the default finite rolling producer.");
            var rows=new List<string>{"scene,frame,type,tick,phase,eligible,selected,published,count,stop,matched,maxError,tangentError,crossedTurn"};
            int scenes=0,matched=0,longMatches=0,curved=0,crossed=0;var failures=new List<string>();
            float oldWind=Main.windSpeedTarget;
            try
            {
                foreach(int type in new[]{472,521,195,662,686})
                foreach(int phase in type==472 || type==521?new[]{0,1,2,3,4}:type==195?new[]{0,1,2}:new[]{0,1,2})
                {
                    bool crowd=(type==472 || type==521 || type==662) && phase==0;
                    string scene=type+"-phase-"+phase+(crowd?"-crowd":"");scenes++;try{
                    NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.GameMode=0;Main.dayTime=false;
                    Main.windSpeedTarget=phase==1?.8f:phase==2?-.6f:0;
                    Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};
                    var player=Main.LocalPlayer;player.position=new Vector2(1100,2400-player.height);player.fallStart=player.fallStart2=(int)(player.position.Y/16);player.statLife=player.statLifeMax=player.statLifeMax2=5000;player.creativeGodMode=true;player.immune=true;player.immuneTime=100000;for(int cooldown=0;cooldown<player.hurtCooldowns.Length;cooldown++)player.hurtCooldowns[cooldown]=100000;
                    for(int i=0;i<3;i++)player.armor[i].SetDefaults(696+i);
                    int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1700,2100,type,Start:16,Target:Main.myPlayer);var npc=Main.npc[slot];npc.timeLeft=750;
                    npc.velocity=new Vector2(-2,1);
                    if(type==472 || type==521)
                    {
                        npc.ai[0]=phase;npc.ai[1]=phase==1?1:-1;npc.localAI[0]=120;
                        if(phase==1){npc.position.X=1850;npc.velocity=new Vector2(7,-2);}
                        else if(phase==2){npc.velocity=new Vector2(4.5f,-4);npc.ai[1]=-1;}
                        else if(phase==3)npc.velocity=new Vector2(-2,-3);
                        // Independent natural birth state, retained alongside
                        // crowd and later turn cases. Both native members
                        // apply their own first-action facing/velocity impulse.
                        else if(phase==4){npc.ai[0]=npc.ai[1]=npc.localAI[0]=0;npc.velocity=Vector2.Zero;}
                    }
                    if(type==195 && phase==2)npc.AddBuff(Terraria.ID.BuffID.Confused,300);
                    if(type==195){npc.position=new Vector2(1160,2400-npc.height);npc.velocity=Vector2.Zero;npc.ai[0]=phase;}
                    if(type==662 && phase==2){npc.position=new Vector2(1120,2300);npc.velocity=new Vector2(4,1);}
                    if(type==686)
                    {
                        npc.localAI[0]=1;npc.direction=1;npc.velocity=new Vector2(-3,1);
                        npc.position=phase==1?new Vector2(1260,2320):new Vector2(1700,2250);
                        if(phase==2)
                        {
                            // A real nearby liquid tile changes the scan motor
                            // while the actor remains dry, rather than changing
                            // its AI/defaults to isolate an invented behavior.
                            for(int x=100;x<135;x++){Main.tile[x,147].liquid=160;}
                        }
                    }
                    if(crowd)
                    {
                        int other=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),(int)npc.Center.X+20,(int)npc.Bottom.Y,type,Start:slot+1,Target:Main.myPlayer);
                        Main.npc[other].position=npc.position+new Vector2(24,10);Main.npc[other].velocity=new Vector2(2,-1);Main.npc[other].timeLeft=750;
                        if(type==472 || type==521){Main.npc[other].localAI[0]=120;Main.npc[other].ai[0]=1;Main.npc[other].ai[1]=-1;}
                    }
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                    var captures=new List<NpcTrajectory>();double errorMax=0,tangent=0;int localMatches=0,localLong=0,transformedMatches=0,selectedCount=0,published=0,eligible=0;bool turned=false;
                    NpcTrajectory birth=null;int birthMatches=0;Harmony birthHook=null;
                    if((type==472 || type==521) && phase==4)
                    {
                        // Observe the first native NPC action AFTER that tick's
                        // real Player.Update and BEFORE its own AI. Capturing
                        // only at the completed tick misses the L0==0 impulse.
                        birthHook=new Harmony("JueMingR.Probe.AmbientBirth");
                        beforeAction=actor=>
                        {
                            if(!ReferenceEquals(actor,npc) || birth!=null)return;
                            Require(actor.localAI[0]==0,"Birth must be captured before its first AI action "+type);
                            var identity=new NpcIdentity((long)Get(host,"Session"),actor,actor.whoAmI,actor.generation,actor.type,actor.netID);
                            var selectedCache=cache.Read(0);
                            birth=(NpcTrajectory)source.GetType().GetMethod("TryCandidate",Flags).Invoke(source,new object[]{identity,(long)Main.GameUpdateCount,true,120});
                            Require(birth!=null && birth.Count>30,"Before-NPC birth producer must publish a useful private forecast "+type);
                            Require(ReferenceEquals(selectedCache,cache.Read(0)),"Birth candidate cannot change selected publication "+type);
                        };
                        birthHook.Patch(typeof(NPC).GetMethod("UpdateNPC",Flags),prefix:new HarmonyMethod(typeof(NativeCombatAmbientFlightChecks).GetMethod(nameof(BeforeAction),Flags)));
                    }
                    try{
                    for(int frame=0;frame<180;frame++)
                    {
                        NativeCombatModeledImpactChecks.SampleMouse(context,npc.Center);step();
                        if(!npc.active || npc.life<=0 || player.dead || npc.type!=type && !(type==195 && npc.type==196))break;
                        if(birth!=null)
                        {
                            int offset=(int)((long)Main.GameUpdateCount-birth.CaptureTick)+1;
                            if(offset>0 && offset<birth.Count)
                            {
                                double error=Vector2.Distance(new Vector2(birth[offset].Bounds.X,birth[offset].Bounds.Y),npc.position);birthMatches++;
                                Require(error<=.08 && birth[offset].NoTileCollide==npc.noTileCollide,"Before-NPC frozen birth "+scene+" offset="+offset+" error="+error+" predicted="+birth[offset].Bounds.X+","+birth[offset].Bounds.Y+" actual="+npc.position+" localAI="+npc.localAI[0]);
                            }
                        }
                        if(!npc.dontTakeDamage && !npc.friendly && !npc.immortal)eligible++;
                        var target=(NpcIdentity)Get(Get(host,"Selection"),"Target");bool selected=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(target.Token,npc);
                        var path=cache.Read(0);if(selected)selectedCount++;
                        if(path!=null)
                        {
                            Require(selected && path.Identity.Equals(target) && path.CaptureTick==Main.GameUpdateCount,"Fresh selected same-actor ambient path "+scene);
                            published++;if(published==1 || frame%15==0)captures.Add(path);
                        }
                        for(int c=captures.Count-1;c>=0;c--)
                        {
                            var frozen=captures[c];int offset=(int)((long)Main.GameUpdateCount-frozen.CaptureTick);
                            if(offset>=frozen.Count){captures.RemoveAt(c);continue;}if(offset<=0)continue;
                            double error=Vector2.Distance(new Vector2(frozen[offset].Bounds.X,frozen[offset].Bounds.Y),npc.position);errorMax=Math.Max(errorMax,error);localMatches++;matched++;
                            Require(frozen[offset].NoTileCollide==npc.noTileCollide,"Frozen ambient future collision gate "+scene+" offset="+offset);
                            Require(error<=.08,"Ambient full-native frozen prefix "+scene+" offset="+offset+" error="+error.ToString("R",CultureInfo.InvariantCulture)+" stop="+frozen.Stop+" predicted="+frozen[offset].Bounds.X+","+frozen[offset].Bounds.Y+" pv="+frozen[offset].Vx+","+frozen[offset].Vy+" actual="+npc.position+" av="+npc.velocity+" phase="+npc.ai[0]+","+npc.ai[1]+" player="+player.position+" playerV="+player.velocity+" playerLife="+player.statLife+" peer="+(crowd?Main.npc[slot+1].position.ToString():"none")+" peerV="+(crowd?Main.npc[slot+1].velocity.ToString():"none")+" peerPhase="+(crowd?Main.npc[slot+1].ai[0]+","+Main.npc[slot+1].ai[1]:"none"));
                            if(offset>=30)
                            {
                                localLong++;longMatches++;tangent=Math.Max(tangent,Vector2.Distance(npc.position,new Vector2(frozen[0].Bounds.X+frozen[0].Vx*offset,frozen[0].Bounds.Y+frozen[0].Vy*offset)));
                                if(frozen[0].Phase!=npc.ai[0])turned=true;
                                if(type==195 && frozen.Identity.Type==195 && npc.type==196)transformedMatches++;
                            }
                        }
                        rows.Add(string.Join(",",scene,frame,type,Main.GameUpdateCount,npc.ai[0].ToString("R",CultureInfo.InvariantCulture),eligible,selected,published,path?.Count??0,path?.Stop.ToString()??"none",localMatches,errorMax.ToString("R",CultureInfo.InvariantCulture),tangent.ToString("R",CultureInfo.InvariantCulture),turned));
                    }
                    }finally{beforeAction=null;birthHook?.UnpatchAll(birthHook.Id);}
                    if(birthHook!=null){Require(birthMatches>=30,"Before-NPC birth must retain thirty real future actions "+scene);Console.WriteLine("PASS ambient BeforeNpc birth "+scene+" matched="+birthMatches);}
                    Require(selectedCount>0 && published>0 && localMatches>0,"Ambient natural mechanism must actually select/publish/compare "+scene);
                    Require(localLong>=30,"Ambient natural forecast must match beyond thirty future actions "+scene);
                    if(type==195)Require(transformedMatches>=30,"Known LostGirl transform must preserve the frozen forecast into the real Nymph motor "+scene);
                    if(tangent>8)curved++;if(turned)crossed++;
                    Console.WriteLine("AMBIENT scene="+scene+" eligible="+eligible+" selected="+selectedCount+" published="+published+" matched="+localMatches+" longMatched="+localLong+" maxError="+errorMax.ToString("R",CultureInfo.InvariantCulture)+" tangentError="+tangent.ToString("R",CultureInfo.InvariantCulture)+" crossedTurn="+turned);
                    }catch(InvalidOperationException failure){failures.Add(scene+": "+failure.Message);Console.WriteLine("AMBIENT FAIL "+scene+": "+failure.Message);}
                }
                ContactPremise(context,cache,step,output);
                Require(failures.Count==0,"Natural ambient scene failures="+failures.Count+": "+string.Join(" | ",failures));
                Require(curved>=3 && crossed>=2,"Ambient steering and natural turns must reject tangent continuation, curved="+curved+" crossed="+crossed);
                Console.WriteLine("PASS ambient full-native continuous scenes="+scenes+" matched="+matched+" longMatched="+longMatches+" curved="+curved+" crossed="+crossed);
            }
            finally{Main.windSpeedTarget=oldWind;Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"ambient-flight-continuous.csv"),rows);}
        }

        private static void ContactPremise(object context,NpcPredictionCache cache,Action step,string output)
        {
            object host=Get(context,"CombatObservation");
            NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.GameMode=0;Main.dayTime=false;Main.windSpeedTarget=0;
            Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};
            var player=Main.LocalPlayer;player.position=new Vector2(1100,2400-player.height);player.fallStart=player.fallStart2=(int)(player.position.Y/16);player.statLife=player.statLifeMax=player.statLifeMax2=5000;player.creativeGodMode=true;
            for(int i=0;i<3;i++)player.armor[i].SetDefaults(696+i);
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1700,2100,472,Start:16,Target:Main.myPlayer);var actor=Main.npc[slot];actor.velocity=new Vector2(-2,1);actor.timeLeft=750;actor.localAI[0]=120;
            int peer=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),(int)actor.Center.X+20,(int)actor.Bottom.Y,472,Start:slot+1,Target:Main.myPlayer);
            Main.npc[peer].position=actor.position+new Vector2(24,10);Main.npc[peer].velocity=new Vector2(2,-1);Main.npc[peer].timeLeft=750;Main.npc[peer].localAI[0]=120;Main.npc[peer].ai[0]=1;Main.npc[peer].ai[1]=-1;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
            var frozen=new List<NpcTrajectory>();var rows=new List<string>{"frame,tick,playerLife,playerX,playerY,playerVx,playerVy,npcX,npcY,peerX,peerY,firstHit,firstDivergence,maxFrozenError"};
            int hit=-1,divergence=-1,postHitMatches=0;long hitTick=-1;double postHitError=0;NpcTrajectory recovered=null;
            try
            {
                for(int frame=0;frame<180;frame++)
                {
                    NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();
                    if(hit<0 && player.statLife<5000)
                    {
                        hit=frame;hitTick=Main.GameUpdateCount;
                        // Preserve the actual first contact/knockback. Later
                        // immunity makes recovery a separate NoNewHits window.
                        player.immune=true;player.immuneTime=100000;for(int i=0;i<player.hurtCooldowns.Length;i++)player.hurtCooldowns[i]=100000;
                    }
                    var path=cache.Read(0);
                    if(path!=null && ReferenceEquals(path.Identity.Token,actor))
                    {
                        if(hit<0 && (frozen.Count==0 || frame%15==0))frozen.Add(path);
                        if(hit>=0 && recovered==null && path.Count>30)recovered=path;
                    }
                    double maximum=0;
                    foreach(var prior in frozen)
                    {
                        int offset=(int)((long)Main.GameUpdateCount-prior.CaptureTick);if(offset<=0 || offset>=prior.Count)continue;
                        double error=Vector2.Distance(new Vector2(prior[offset].Bounds.X,prior[offset].Bounds.Y),actor.position);maximum=Math.Max(maximum,error);
                        if(error>.08 && divergence<0)divergence=frame;
                        Require(error<=.08 || hit>=0 && Main.GameUpdateCount>=hitTick,"Ambient crowd cannot diverge before the real new-hit premise boundary frame="+frame+" error="+error);
                    }
                    if(recovered!=null)
                    {
                        int offset=(int)((long)Main.GameUpdateCount-recovered.CaptureTick);
                        if(offset>0 && offset<recovered.Count)
                        {
                            double error=Vector2.Distance(new Vector2(recovered[offset].Bounds.X,recovered[offset].Bounds.Y),actor.position);postHitError=Math.Max(postHitError,error);postHitMatches++;
                            Require(error<=.08,"Ambient crowd re-sampled hit recovery error="+error+" offset="+offset);
                        }
                    }
                    rows.Add(string.Join(",",frame,Main.GameUpdateCount,player.statLife,player.position.X,player.position.Y,player.velocity.X,player.velocity.Y,actor.position.X,actor.position.Y,Main.npc[peer].position.X,Main.npc[peer].position.Y,hit,divergence,maximum.ToString("R",CultureInfo.InvariantCulture)));
                }
                Require(hit>=0 && divergence>=hit,"Original no-immunity crowd must expose real hit then obsolete-premise divergence, hit="+hit+" divergence="+divergence);
                Require(recovered!=null && recovered.CaptureTick-hitTick<=12 && postHitMatches>=30,"Ambient contact must recover useful frozen forecast, matched="+postHitMatches);
                Console.WriteLine("PASS ambient contact premise hitFrame="+hit+" firstDivergenceFrame="+divergence+" recoveredMatches="+postHitMatches+" recoveredMaxError="+postHitError);
            }
            finally{Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"ambient-contact-premise.csv"),rows);}
        }
    }
}
