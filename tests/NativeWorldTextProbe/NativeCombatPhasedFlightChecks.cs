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
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatPhasedFlightChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        // Uses the existing isolated full UpdatePlayers -> UpdateNPCs ->
        // UpdateProjectiles -> Host publication callback. Captures are immutable
        // forecasts of the SAME continuing native actor, never fresh defaults
        // substituted for its natural turns. No real game/session is operated.
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");
            Require(GetOptional(source,"Native")==null,"Phased flight uses the default rolling producer, no native worker.");
            var rows=new List<string>{"scene,frame,type,tick,phase,x,y,selected,published,count,stop,matched,maxError,tangentError"};
            int matched=0,curved=0,scenes=0;var failures=new List<string>();
            try
            {
                foreach(int type in new[]{224,509,581,587,405,421,467,477,478,479,288,347,352,406,407,420,519,522,483})
                foreach(int phase in type==477?new[]{0,1,2,3,31,32,41,42}:type==479?new[]{0,1,2,21}:type==421?new[]{0,1,2,3,4,5,6}:type==405 || type==467 || type==483?new[]{0,1,2,3,4}:type==352?new[]{0,1,2,3}:new[]{0})
                {
                    string scene=type+"-phase-"+phase;scenes++;try{
                    NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();
                    Main.eclipse=true;Main.dayTime=false;Main.netMode=0;Main.GameMode=0;
                    Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};
                    var p=Main.LocalPlayer;p.position=new Vector2(1100,2400-p.height);p.fallStart=p.fallStart2=(int)(p.position.Y/16);p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.creativeGodMode=true;p.immune=true;p.immuneTime=100000;for(int cooldown=0;cooldown<p.hurtCooldowns.Length;cooldown++)p.hurtCooldowns[cooldown]=100000;
                    for(int i=0;i<3;i++)p.armor[i].SetDefaults(696+i);
                    int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1700,2100,type,Start:16,Target:Main.myPlayer);var n=Main.npc[slot];
                    n.ai[0]=phase==31?3.1f:phase==32?3.2f:phase==41?4.1f:phase==42?4.2f:phase==21?2.1f:phase;
                    n.velocity=new Vector2(-2,1);n.timeLeft=750;
                    if(phase==3 && type!=477){n.ai[1]=n.Center.X-100;n.ai[2]=n.Center.Y+80;}
                    if(phase==32)n.ai[1]=-1;
                    if(type==421 && phase==6){n.ai[0]=1;n.Center=p.Top+new Vector2(0,-30);}
                    if(type==406)n.ai[0]=285;if(type==478)n.ai[0]=880;
                    if(type==352)
                    {
                        n.ai[0]=0;
                        if(phase==1)n.Center=p.Center+new Vector2(270,-6);
                        if(phase==2){n.Center=p.Center+new Vector2(60,-6);n.velocity=new Vector2(-4,2);}
                        if(phase==3)Main.dayTime=true;
                    }
                    if(type==522){n.ai[0]=58;n.ai[1]=.025f;n.ai[2]=-3;n.ai[3]=1;}
                    if(phase==41 || phase==42){n.ai[1]=(int)n.Center.X/16-8;n.ai[2]=(int)n.Center.Y/16+8;}
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                    var captures=new List<NpcTrajectory>();var formTicks=new HashSet<long>();double maxError=0,tangent=0;int localMatches=0,selectedCount=0,published=0,formMatches=0,formEdges=0;long formCapture=-1,formEnd=-1;bool ended=false;
                    for(int frame=0;frame<180;frame++)
                    {
                        NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();
                        if(!n.active || n.life<=0 || p.dead){ended=true;break;}
                        var selected=(NpcIdentity)Get(Get(host,"Selection"),"Target");bool has=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(selected.Token,n);
                        var path=NativeCombatPresentationChecks.CurrentPath(host);if(has)selectedCount++;
                        if(path!=null)
                        {
                            Require(has && path.Identity.Equals(selected) && path.SampleTick==Main.GameUpdateCount,"Fresh same-actor default path "+scene);published++;
                            // The two natural hatches happen before the next
                            // fifteen-frame sample. Freeze each genuinely new
                            // pre-hatch observation or actually extended tail,
                            // not repeated slices of one calculation, so its
                            // available cross-form future
                            // is checked independently of publication cadence.
                            bool beforeForm=(type==406 || type==478) && n.type==type;
                            long end=path.SampleTick+path.Count-1;
                            if(beforeForm?path.CaptureTick!=formCapture || end>formEnd:published==1 || frame%15==0)
                            {captures.Add(path);if(beforeForm){formCapture=path.CaptureTick;formEnd=end;}}
                        }
                        for(int c=captures.Count-1;c>=0;c--)
                        {
                            var frozen=captures[c];int offset=(int)((long)Main.GameUpdateCount-frozen.SampleTick);
                            if(offset>=frozen.Count){captures.RemoveAt(c);continue;}if(offset<=0)continue;
                            var point=frozen[offset];double error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),n.position);
                            Require(point.NoTileCollide==n.noTileCollide,"Frozen future collision gate "+scene+" offset="+offset);
                            // Conditions intentionally exclude newly born crowd
                            // interaction/hits/random decisions. Those horizons
                            // end explicitly; deterministic motor/stage prefixes
                            // must agree beyond the former twelve-action trend.
                            maxError=Math.Max(maxError,error);localMatches++;matched++;
                            if(n.type!=type && frozen.Identity.Type==type)
                            {
                                Require(n.type==(type==406?405:479) && point.Bounds.Width==n.width && point.Bounds.Height==n.height && point.Phase==n.ai[0] && point.CanReceive==(!n.dontTakeDamage && !n.immortal),"Frozen Transform preserves the actual new form, geometry, phase and receiving state "+scene+" offset="+offset);
                                if(frozen[offset-1].Bounds.Width!=point.Bounds.Width || frozen[offset-1].Bounds.Height!=point.Bounds.Height)
                                {Require(point.NewSegment,"The real Transform geometry starts a new frozen segment "+scene);formEdges++;}
                                formMatches++;formTicks.Add((long)Main.GameUpdateCount);
                            }
                            if(offset>=30)tangent=Math.Max(tangent,Vector2.Distance(n.position,new Vector2(frozen[0].Bounds.X+frozen[0].Vx*offset,frozen[0].Bounds.Y+frozen[0].Vy*offset)));
                            Require(error<=.08,"Phased full-native frozen prefix "+scene+" offset="+offset+" error="+error.ToString("R",CultureInfo.InvariantCulture)+" stop="+frozen.Stop+
                                " predicted="+point.Bounds.X.ToString("R",CultureInfo.InvariantCulture)+","+point.Bounds.Y.ToString("R",CultureInfo.InvariantCulture)+" pv="+point.Vx+","+point.Vy+
                                " actual="+n.position+" av="+n.velocity+" size="+n.width+","+n.height+" direction="+n.direction+","+n.directionY+" player="+p.position+" playerV="+p.velocity+" playerLife="+p.statLife+" justHit="+n.justHit);
                        }
                        rows.Add(string.Join(",",scene,frame,type,Main.GameUpdateCount,n.ai[0].ToString("R",CultureInfo.InvariantCulture),n.position.X.ToString("R",CultureInfo.InvariantCulture),n.position.Y.ToString("R",CultureInfo.InvariantCulture),has,path!=null,path?.Count??0,path?.Stop.ToString()??"none",localMatches,maxError.ToString("R",CultureInfo.InvariantCulture),tangent.ToString("R",CultureInfo.InvariantCulture)));
                    }
                    Require(selectedCount>0 && published>0 && localMatches>=30,"Natural mechanism must publish and match continuing actor "+scene+" selected="+selectedCount+" published="+published+" matched="+localMatches+" ended="+ended);
                    if(type==406 || type==478)Require(formMatches>=30 && formEdges>0,"Known Transform frozen forecasts must continue through the real form boundary into the live new form "+scene+" matched="+formMatches+" distinct="+formTicks.Count+" edges="+formEdges);
                    if(tangent>8)curved++;
                    Console.WriteLine("PHASED scene="+scene+" selected="+selectedCount+" published="+published+" matched="+localMatches+" formMatches="+formMatches+" distinctFormUpdates="+formTicks.Count+" formEdges="+formEdges+" maxError="+maxError.ToString("R",CultureInfo.InvariantCulture)+" tangentError="+tangent.ToString("R",CultureInfo.InvariantCulture));
                    }catch(InvalidOperationException failure){failures.Add(scene+": "+failure.Message);Console.WriteLine("PHASED FAIL "+scene+": "+failure.Message);}
                }
                ProbeLifetimeOracle(context);
                PlayerHitRecovery(context,cache,step);
                Require(failures.Count==0,"Natural phased scene failures="+failures.Count+": "+string.Join(" | ",failures));
                Require(curved>=5,"Independent unmodeled tangent must fail multiple steering/phase families, curved="+curved);
                Console.WriteLine("PASS phased default full-native continuous scenes="+scenes+" matched="+matched+" tangent-counterexamples="+curved);
            }
            finally{Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"phased-flight-continuous.csv"),rows);}
        }

        private static void ProbeLifetimeOracle(object context)
        {
            // Compare the real common lifetime method, not the related motor.
            // Far-distance and empty-player cases protect the actual switch
            // gate that ordinary nearby continuous scenes cannot exercise.
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");
            MethodInfo read=source.GetType().GetMethod("Read",Flags),lifetime=typeof(NpcMotion).GetMethod("CheckActive",Flags);
            long session=(long)Get(host,"Session");
            NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;
            for(int i=0;i<Main.maxPlayers;i++)Main.player[i].active=false;
            var player=Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true};player.position=new Vector2(1000,2300);
            foreach(int phase in new[]{0,1,2})foreach(bool activePlayer in new[]{true,false})
            {
                player.active=activePlayer;
                var actor=Main.npc[16]=new NPC{whoAmI=16};actor.SetDefaults(NPCID.MartianProbe);actor.active=true;actor.position=new Vector2(9000,2300);actor.ai[0]=phase;actor.timeLeft=2;
                var state=(NpcMotionState)read.Invoke(null,new object[]{actor,session});
                var area=new MotionRect((int)player.position.X,(int)player.position.Y,player.width,player.height);
                var environment=new PredictionEnvironment{Players=new PredictionPlayers(new[]{area},activePlayer?1:0,new[]{Main.myPlayer})};
                object[] args={state,environment,PredictionStop.None};lifetime.Invoke(null,args);actor.CheckActive();state=(NpcMotionState)args[0];
                Require(state.Active==actor.active && state.TimeLeft==actor.timeLeft,"Probe native far lifetime phase="+phase+" activePlayer="+activePlayer);
                if(activePlayer && phase==0)
                {args[0]=state;lifetime.Invoke(null,args);actor.CheckActive();state=(NpcMotionState)args[0];Require(!state.Active && !actor.active && state.TimeLeft==actor.timeLeft,"Probe phase0 far lifetime still expires normally.");}
            }
            Console.WriteLine("PASS original Probe far active-player / empty-player common lifetime oracle");
        }

        private static void PlayerHitRecovery(object context,NpcPredictionCache cache,Action step)
        {
            object host=Get(context,"CombatObservation");
            NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.GameMode=0;Main.dayTime=false;
            Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};
            var player=Main.LocalPlayer;player.position=new Vector2(1100,2400-player.height);player.fallStart=player.fallStart2=(int)(player.position.Y/16);
            player.statLife=player.statLifeMax=player.statLifeMax2=5000;
            for(int armor=0;armor<3;armor++)player.armor[armor].SetDefaults(696+armor);
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1120,2400,509,Start:16,Target:Main.myPlayer);
            var actor=Main.npc[slot];actor.position=new Vector2(player.position.X+12,player.position.Y+5);actor.timeLeft=750;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
            bool hit=false;
            for(int action=0;action<30 && !hit;action++)
            {NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();hit=player.statLife<5000;}
            Require(hit,"Full native NPC contact must produce the real new-hit premise boundary.");
            // The hit is native, not a synthetic velocity edit. Once sampled,
            // its knockback must seed the new held-controls player timeline.
            player.immune=true;player.immuneTime=100000;for(int i=0;i<player.hurtCooldowns.Length;i++)player.hurtCooldowns[i]=100000;
            NpcTrajectory frozen=null;int recovery=0,matches=0;double maximum=0;
            for(int action=0;action<80;action++)
            {
                NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();var path=cache.Read(0);
                if(frozen==null && path!=null && ReferenceEquals(path.Identity.Token,actor) && path.CaptureTick==Main.GameUpdateCount && path.Count>30){frozen=path;recovery=action+1;}
                if(frozen==null)continue;int offset=(int)((long)Main.GameUpdateCount-frozen.CaptureTick);if(offset<=0 || offset>=frozen.Count)continue;
                double error=Vector2.Distance(new Vector2(frozen[offset].Bounds.X,frozen[offset].Bounds.Y),actor.position);maximum=Math.Max(maximum,error);matches++;
                Require(error<=.08,"Re-sampled post-hit frozen NPC route offset="+offset+" error="+error+" player="+player.position+" playerV="+player.velocity+" life="+player.statLife);
            }
            Require(frozen!=null && recovery<=12 && matches>=30,"Real player-hit recovery must republish and match, recovery="+recovery+" matches="+matches);
            Console.WriteLine("PASS phased real player-hit recovery actions="+recovery+" frozenMatches="+matches+" maxError="+maximum);
        }

        internal static void Catalogue(object context,string output)
        {
            foreach(string name in new[]{"Initialize_TileAndNPCData1","Initialize_TileAndNPCData2"})typeof(Main).GetMethod(name,Flags).Invoke(null,null);
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");long session=(long)Get(host,"Session");
            MethodInfo read=source.GetType().GetMethod("Read",Flags),structured=typeof(NpcMotion).GetMethod("StructuredModel",Flags);
            MethodInfo position=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcPositionObservation",true).GetMethod("Capture",Flags);
            var rows=new List<string>{"requestedNetId,type,netId,style,defaultSelectable,mechanism,kernel,conditionalStages"};var unresolved=new SortedDictionary<int,int>();int selectable=0;
            for(int requested=-65;requested<NPCID.Count;requested++)
            {
                if(requested==0)continue;
                var n=new NPC();n.SetDefaults(requested);n.whoAmI=0;n.active=true;
                bool valid=n.lifeMax>0 && !n.dontTakeDamage && (n.type==NPCID.TargetDummy || !n.friendly && !n.immortal);
                string mechanism="requires-natural-selectable-state",kernel="none";
                if(valid)
                {
                    selectable++;var state=(NpcMotionState)read.Invoke(null,new object[]{n,session});
                    // The directory observes each fresh scalar input through
                    // the same read-only relation capture as production. This
                    // neither installs the dummy into Main.npc nor runs AI or
                    // carries an earlier directory object's observation forward.
                    object[] positionArgs={n,state,session};position.Invoke(null,positionArgs);state=(NpcMotionState)positionArgs[1];
                    bool known=(bool)structured.Invoke(null,new object[]{state});
                    int segmented=(int)host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.Prediction.SegmentedNpcPrediction").GetMethod("Family",Flags).Invoke(null,new object[]{n.type});
                    mechanism=segmented!=0?"segmented-observation":known?"structural-routing":n.aiStyle==3?"partial-fighter-motor":"generic-observation-unresolved";
                    foreach(string model in new[]{"NpcWallBossMotion","NpcDarkMageMotion","NpcAttachedWeaponMotion","NpcTownCritterMotion","NpcDiveFlightMotion","NpcBloodNautilusMotion","NpcBrainSwarmMotion","NpcQueenBeeMotion","NpcGolemMotion","NpcBetsyMotion","NpcSkeletonBossMotion","NpcCelestialTowerMotion","NpcPumpkingMotion","NpcIceQueenMotion","NpcCultistBossMotion","NpcCultistRitualMotion","NpcMoonLordMotion","NpcMoonLeechMotion","NpcQueenSlimeMotion","NpcBirdMotion","NpcEmpressMotion","NpcDeerclopsMotion","NpcWindyBalloonMotion","NpcTreeBossMotion","NpcSantaTankMotion","NpcKingSlimeMotion","NpcSnowmanMotion","NpcMartianTurretMotion","NpcInvasionGroundMotion","NpcInvasionLightningMotion","NpcInvasionFlightMotion","NpcInvasionCrystalMotion","NpcEyeBossMotion","NpcMechanicalBossMotion","NpcBigMimicMotion","NpcPlanteraMotion","NpcTerrainFlightMotion","NpcRelatedMotion","NpcAmbientFlightMotion","NpcPhasedFlightMotion","NpcFiniteFlightMotion"})
                    {
                        var modelType=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat."+model,true);
                        if((bool)modelType.GetMethod("Known",Flags).Invoke(null,new object[]{state})){kernel=model;break;}
                    }
                    if(state.PositionRelation>=1 && state.PositionRelation<=4 && kernel=="none")kernel="NpcPositionMotion relation"+state.PositionRelation;
                    if(segmented!=0)kernel="Segmented-family-"+segmented;
                    else if(kernel=="none" && n.aiStyle==3)kernel="Ground/entry/clock/form/shared-horizontal";
                    if(!known && segmented==0){if(!unresolved.ContainsKey(n.aiStyle))unresolved.Add(n.aiStyle,0);unresolved[n.aiStyle]++;}
                }
                rows.Add(string.Join(",",requested,n.type,n.netID,n.aiStyle,valid,mechanism,kernel,"not-proved-by-default-census"));
            }
            PositionOwnerOracle(context);
            Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"current-npc-routing-catalogue.csv"),rows);
            Console.WriteLine("CATALOGUE selectable defaults+netVariants="+selectable+" remaining incomplete styles="+unresolved.Count+" (partial fighters distinguished; routing only; natural stages not certified)");
            foreach(var pair in unresolved)Console.WriteLine("UNRESOLVED style="+pair.Key+" defaultMembers="+pair.Value);
        }
        private static void PositionOwnerOracle(object context)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");long session=(long)Get(host,"Session");
            MethodInfo read=source.GetType().GetMethod("Read",Flags),capture=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcPositionObservation",true).GetMethod("Capture",Flags),action=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcPositionMotion",true).GetMethod("Step",Flags);
            int actions=0,orphans=0;
            foreach(int mode in new[]{0,1,2})
            {
                typeof(NativeCombatDiveFlightChecks).GetMethod("World",Flags).Invoke(null,null);Main.netMode=0;
                int parentSlot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),2200,1800,383,Start:16,Target:Main.myPlayer);var parent=Main.npc[parentSlot];
                int childSlot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),2200,1800,384,Start:24,ai0:parentSlot,Target:Main.myPlayer);var child=Main.npc[childSlot];Main.netMode=mode;
                // Continue the same original child/owner through the actual
                // owner's geometry, zero-life validity and subsequent orphan.
                for(int frame=0;frame<64;frame++)
                {
                    parent.position+=new Vector2(1.173f,-.217f);if(frame==20)parent.life=0;if(frame==30){child.width=23;child.height=31;}
                    var n=(NpcMotionState)read.Invoke(null,new object[]{child,session});object[] cap={child,n,session};capture.Invoke(null,cap);n=(NpcMotionState)cap[1];var owner=(NpcMotionState)read.Invoke(null,new object[]{parent,session});
                    object[] args={n,new[]{owner,n},2,1,new PredictionEnvironment(),false,PredictionStop.None};Require((bool)action.Invoke(null,args),"AI72 active zero-life owner remains native-valid.");n=(NpcMotionState)args[0];child.AI();
                    Require(n.X==child.position.X && n.Y==child.position.Y && n.Vx==child.velocity.X && n.Vy==child.velocity.Y && child.active && n.PositionOwner.Equals(owner.Identity),"Original continuing AI72 owner constraint mode="+mode+" frame="+frame);actions++;
                }
                var frozen=(NpcMotionState)read.Invoke(null,new object[]{child,session});object[] oldCapture={child,frozen,session};capture.Invoke(null,oldCapture);frozen=(NpcMotionState)oldCapture[1];parent.active=false;
                var orphan=(NpcMotionState)read.Invoke(null,new object[]{child,session});object[] orphanCapture={child,orphan,session};capture.Invoke(null,orphanCapture);orphan=(NpcMotionState)orphanCapture[1];
                object[] deadArgs={orphan,new[]{orphan},1,1,new PredictionEnvironment(),false,PredictionStop.None};Require(!(bool)action.Invoke(null,deadArgs) && (PredictionStop)deadArgs[6]==PredictionStop.Despawn,"AI72 observed lost owner is a known retirement.");child.AI();orphan=(NpcMotionState)deadArgs[0];Require(!child.active && child.life==0 && !orphan.Active && orphan.Life==0,"Original AI72 orphan retires in all native net modes.");orphans++;
                // NewNPC protects a just-retired birth slot for two native
                // frame-maintenance passes; raw AI actions never age that guard.
                Main.netMode=0;NPC.UpdateProtectedSpawnSlots();NPC.UpdateProtectedSpawnSlots();int replacement=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),2200,1800,383,Start:parentSlot,Target:Main.myPlayer);Require(replacement==parentSlot,"Original slot reuse fixture binds the intended retired slot.");var fresh=Main.npc[replacement];var freshState=(NpcMotionState)read.Invoke(null,new object[]{fresh,session});
                Require(!frozen.PositionOwner.Equals(freshState.Identity),"Real NPC generation changes even if the object/slot is reused.");object[] stale={frozen,new[]{freshState,frozen},2,1,new PredictionEnvironment(),false,PredictionStop.None};Require(!(bool)action.Invoke(null,stale) && (PredictionStop)stale[6]==PredictionStop.MissingDependency,"Old full owner identity cannot bind a replacement generation.");
                child.active=true;child.life=child.lifeMax;var resumed=(NpcMotionState)read.Invoke(null,new object[]{child,session});object[] freshCapture={child,resumed,session};capture.Invoke(null,freshCapture);resumed=(NpcMotionState)freshCapture[1];Require(resumed.PositionOwner.Equals(freshState.Identity),"A fresh real observation acquires the actual new owner generation.");
            }
            Console.WriteLine("PASS AI72 current native same-object constraints="+actions+" lost-owner retirements="+orphans+" full-generation recovery=3; routing census is separate from full boss stage coverage");
        }
    }
}
