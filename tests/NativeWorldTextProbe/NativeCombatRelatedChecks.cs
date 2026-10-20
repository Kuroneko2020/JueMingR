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
    internal static class NativeCombatRelatedChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Action<NPC> beforeRetirement,afterRetirement;
        private static void BeforeRetirement(NPC __instance){beforeRetirement?.Invoke(__instance);}
        private static void AfterRetirement(NPC __instance){afterRetirement?.Invoke(__instance);}
        private static Exception ProjectileFailure(Projectile __instance,Exception __exception)
        {
            if(__exception!=null)
            {
                int slot=(int)__instance.ai[0]-1;
                NPC owner=slot>=0 && slot<Main.maxNPCs?Main.npc[slot]:null;
                Console.WriteLine("RELATED ORIGINAL-PROJECTILE-EXCEPTION type="+__instance.type+" slot="+__instance.whoAmI+" key="+(int)__instance.key+" position="+__instance.position+" size="+__instance.width+","+__instance.height+" velocity="+__instance.velocity+" ai="+string.Join("/",__instance.ai)+" world="+Main.maxTilesX+","+Main.maxTilesY+" npcOwner="+slot+" npcType="+(owner?.type??-1)+" npcPosition="+owner?.position+" npcTarget="+(owner?.target??-1)+" exception="+__exception);
            }
            return __exception;
        }
        internal static void Boundaries()
        {
            Type model=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcRelatedMotion",true);
            var action=model.GetMethod("Step",Flags);var known=model.GetMethod("Known",Flags);
            var owner=new NpcMotionState{Identity=new NpcIdentity(1,new object(),17,1,439,439),Active=true,LifeMax=1001};
            foreach(int life in new[]{1001,500,499,250,249})
            {
                owner.Life=life;
                var state=new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,523,523),Style=101,PositionRelation=9,PositionOwner=owner.Identity,Active=true,Width=42,Height=42,A1=116,Vx=2,Vy=-1};
                var arguments=new object[]{state,new PredictionEnvironment(),null,1,PredictionStop.None,new[]{owner},1,false};
                Require((bool)action.Invoke(null,arguments),"Captured Cultist life permits exact AncientDoom clock.");state=(NpcMotionState)arguments[0];
                int rate=life<1001/4?3:life<1001/2?2:1;
                Require(state.A1==116+rate && state.Vx==2 && state.Vy==-1,"Strict integer half/quarter boundary never invents zero velocity: life="+life);
                Require(state.Alpha==(int)(255f-Math.Min(1,state.A1/120f)*255f),"Native finite opacity growth.");
                state.A1=420-rate;arguments[0]=state;
                Require(!(bool)action.Invoke(null,arguments) && (PredictionStop)arguments[4]==PredictionStop.Despawn && !((NpcMotionState)arguments[0]).Active,"AncientDoom ends before publishing its real expiry action.");
            }
            var brain=new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,266,266),Style=54,A0=-1,L0=1,L1=58,X=100,Y=100,Width=160,Height=110,Active=true,JustHit=true};
            var environment=new PredictionEnvironment{PlayerX=500,PlayerY=400,PlayerWidth=20,PlayerHeight=42,Crimson=true};
            var parameters=new object[]{brain,environment,null,1,PredictionStop.None,null,0,false};
            Require((bool)action.Invoke(null,parameters),"A currently observed hit cannot move the earliest random Brain boundary before clock60.");
            brain=(NpcMotionState)parameters[0];Require(brain.L1==59,"Conservative earliest random clock after observed hit.");parameters[0]=brain;
            Require(!(bool)action.Invoke(null,parameters) && (PredictionStop)parameters[4]==PredictionStop.RandomDestination,"Stop before the first possibly random Brain destination action.");
            brain.A0=0;parameters[0]=brain;Require(!(bool)known.Invoke(null,new object[]{brain}),"Brain first phase cannot inherit the receiving second-phase contract.");
            var core=new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,398,398),Style=77,A0=1,L3=1,Width=46,Height=66};
            Require((bool)known.Invoke(null,new object[]{core}),"Initialized receiving MoonLord core phase is known.");core.A0=0;
            Require(!(bool)known.Invoke(null,new object[]{core}),"MoonLord parts/death gate is not a complete core model.");
            brain.A0=-3;brain.A3=255;brain.L3=60;environment.Crimson=false;parameters[0]=brain;parameters[1]=environment;
            Require((bool)action.Invoke(null,parameters),"Brain tail consumes the captured non-Crimson premise.");brain=(NpcMotionState)parameters[0];
            Require(brain.A0==2 && brain.Alpha==10 && brain.L3==61 && (bool)known.Invoke(null,new object[]{brain}),"Outside-Crimson tail changes the receiving action into the separately modeled retreat motor.");
            parameters[0]=brain;Require((bool)action.Invoke(null,parameters) && ((NpcMotionState)parameters[0]).L3==62,"Positive retreat retains native retirement clock and cannot inherit negative-phase inertia.");
            core.A0=1;core.X=-core.Width*.5f;core.Y=-core.Height*.5f;core.Active=true;
            var coreEnvironment=new PredictionEnvironment{PlayerX=2400,PlayerY=0};
            object[] coreArgs={core,coreEnvironment,null,1,PredictionStop.None,null,0,false};
            Require((bool)action.Invoke(null,coreArgs),"MoonLord server exact distance2400 retains the receiving motor.");
            coreEnvironment.PlayerX=2400.01f;coreArgs[0]=core;coreArgs[1]=coreEnvironment;
            Require(!(bool)action.Invoke(null,coreArgs) && (PredictionStop)coreArgs[4]==PredictionStop.PhaseBoundary,"MoonLord server strict distance excess stops before unacquired core/parts/eyes teleport.");
            coreEnvironment.Multiplayer=true;coreArgs[0]=core;coreArgs[1]=coreEnvironment;
            Require((bool)action.Invoke(null,coreArgs),"MoonLord client does not originate the server's distant multi-part relocation.");
            var saucer=new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,395,395),Style=76,Active=true,Width=120,Height=90,X=100,Y=100,A0=1,A1=149,Vx=6,Vy=-3,TimeLeft=750};
            var saucerEnvironment=new PredictionEnvironment{PlayerX=500,PlayerY=400,WorldWidth=400,WorldHeight=240};
            object[] saucerArgs={saucer,saucerEnvironment,null,1,PredictionStop.None,null,0,false};
            Require((bool)action.Invoke(null,saucerArgs),"Native initialized client/server Saucer receiving damping phase does not require a server-only local gate.");
            saucer=(NpcMotionState)saucerArgs[0];Require(saucer.A0==2 && saucer.A1==0 && saucer.CanReceive && saucer.Vx==6*.96f && saucer.Vy==-3*.96f,"Saucer clock150 enters the receiving pursuit phase after native damping.");
            saucerEnvironment.PlayerX=10000;saucerEnvironment.WorldWidth=1000;saucerArgs[0]=saucer;saucerArgs[1]=saucerEnvironment;
            Require((bool)action.Invoke(null,saucerArgs),"Alive far target executes native retreat without an invented destination.");saucer=(NpcMotionState)saucerArgs[0];
            Require(saucer.A0==2 && saucer.A1==0 && saucer.A2==0 && saucer.A3==0 && saucer.TimeLeft==300,"Native far alive -2 restores2 in the same action and resets all attack clocks.");
            saucer.Y=-100.01f;saucerArgs[0]=saucer;
            Require(!(bool)action.Invoke(null,saucerArgs) && (PredictionStop)saucerArgs[4]==PredictionStop.Despawn && ((NpcMotionState)saucerArgs[0]).Y==-100,"Saucer native edge clamp precedes retirement.");
            ProbeBoundaries(action);
            Console.WriteLine("PASS related life clock/integer thresholds/earliest randomness and exact receiving-stage boundaries");
        }

        private static void ProbeBoundaries(MethodInfo action)
        {
            // Euclidean winner (-240,240) differs from Manhattan (350,1),
            // and chooses the opposite escape direction at native clock60.
            var areas=new[]{new MotionRect(350,1,0,0),new MotionRect(-240,240,0,0)};
            var players=new PredictionPlayers(areas,2,new[]{0,1},areas,new[]{true,true},new[]{false,false});
            var state=new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,399,399),Style=80,Active=true,A0=1,A1=59,Direction=1,SearchPlayerCaptured=true,SearchPlayerIndex=1,SearchPlayerAlive=true};
            var e=new PredictionEnvironment{Players=players,PlayerTimelineActive=true,PlayerIndex=1,PlayerX=-240,PlayerY=240};
            object[] args={state,e,null,1,PredictionStop.None,null,0,false};
            Require((bool)action.Invoke(null,args) && ((NpcMotionState)args[0]).A0==2 && ((NpcMotionState)args[0]).A3==1,"Euclidean closest player chooses the native escape direction independently of Manhattan targeting.");
            // Dead fallback returns without scanning; an active nondead ghost
            // can scan in native AI and must stop for its unavailable timeline.
            state.A0=0;state.A1=0;areas=new[]{new MotionRect(100,100,0,0)};
            foreach(bool dead in new[]{true,false})
            {
                e.Players=new PredictionPlayers(areas,1,new[]{1},areas,new[]{false},new[]{dead});e.PlayerX=100;e.PlayerY=100;
                args[0]=state;args[1]=e;
                bool valid=(bool)action.Invoke(null,args);
                Require(valid==dead && (dead || (PredictionStop)args[4]==PredictionStop.MissingDependency),"Dead and ghost fallback have distinct native receiving geometry.");
            }
            state.A0=2;state.A1=100;state.A3=-1;state.Vx=-5;args[0]=state;
            Require((bool)action.Invoke(null,args) && ((NpcMotionState)args[0]).Vx==-5.05f,"Native left escape is not symmetrically clamped at -4.");
            state.A1=179;args[0]=state;
            Require(!(bool)action.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.Despawn,"Probe server escape expires on action180.");
            e.Multiplayer=true;args[0]=state;args[1]=e;
            Require((bool)action.Invoke(null,args) && ((NpcMotionState)args[0]).Active,"Probe client does not originate invasion or server retirement.");
        }

        internal static void Lifecycle(object context)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");long session=(long)Get(host,"Session");
            MethodInfo read=source.GetType().GetMethod("Read",Flags);
            MethodInfo action=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcRelatedMotion",true).GetMethod("Step",Flags);
            NPC oldChild=Main.npc[16],oldOwner=Main.npc[17];int oldMode=Main.netMode,cases=0;Main.netMode=1;
            try
            {
                // Scalar boundary oracle supplements the continuous producer
                // check. Only this isolated fixture's AI runs, with no native
                // owner update and no claimed future player/world simulation.
                foreach(int life in new[]{1001,500,499,250,249})foreach(float clock in new[]{0f,118f,417f,418f,419f})
                {
                    var owner=Main.npc[17]=new NPC{whoAmI=17};owner.SetDefaults(NPCID.CultistBoss);owner.active=true;owner.lifeMax=1001;owner.life=life;
                    var actor=Main.npc[16]=new NPC{whoAmI=16};actor.SetDefaults(NPCID.AncientDoom);actor.active=true;actor.ai[0]=17;actor.ai[1]=clock;actor.position=new Vector2(1200,2000);actor.velocity=new Vector2(2,-1);
                    var state=(NpcMotionState)read.Invoke(null,new object[]{actor,session});var group=new[]{(NpcMotionState)read.Invoke(null,new object[]{owner,session})};
                    // This boundary check supplies the same explicit relation
                    // values as production; Continuous certifies acquisition.
                    state.PositionRelation=9;state.PositionOwner=group[0].Identity;state.PositionParameter=0;
                    actor.AI();object[] arguments={state,new PredictionEnvironment{Multiplayer=true},null,1,PredictionStop.None,group,1,false};bool valid=(bool)action.Invoke(null,arguments);state=(NpcMotionState)arguments[0];
                    Require(valid==actor.active && state.Active==actor.active,"Native AncientDoom exact alive/expiry action life="+life+" clock="+clock);
                    if(actor.active)Require(state.A1==actor.ai[1] && state.Scale==actor.scale && state.Alpha==actor.alpha && state.X==actor.position.X && state.Y==actor.position.Y && state.Vx==actor.velocity.X && state.Vy==actor.velocity.Y,"Native AncientDoom clock/opacity/center/observed velocity life="+life+" clock="+clock);
                    else Require((PredictionStop)arguments[4]==PredictionStop.Despawn,"Native AncientDoom expiration is explicit retirement.");cases++;
                }
                foreach(bool active in new[]{false,true})
                {
                    var owner=Main.npc[17]=new NPC{whoAmI=17};owner.SetDefaults(active?NPCID.BlueSlime:NPCID.CultistBoss);owner.active=active;
                    var actor=Main.npc[16]=new NPC{whoAmI=16};actor.SetDefaults(NPCID.AncientDoom);actor.active=true;actor.ai[0]=17;
                    var state=(NpcMotionState)read.Invoke(null,new object[]{actor,session});state.PositionRelation=9;state.PositionParameter=-1;
                    actor.AI();object[] arguments={state,new PredictionEnvironment{Multiplayer=true},null,1,PredictionStop.None,null,0,false};
                    Require(!(bool)action.Invoke(null,arguments) && !actor.active && (PredictionStop)arguments[4]==PredictionStop.Despawn,"Original invalid owner retires without requiring a private owner AI.");cases++;
                }
            }
            finally{Main.npc[16]=oldChild;Main.npc[17]=oldOwner;Main.netMode=oldMode;}
            ProbeSearchOracle(read,source,session,action);
            Console.WriteLine("PASS related original AncientDoom lifecycle/identity scalar boundary cases="+cases);
        }
        private static void ProbeSearchOracle(MethodInfo read,object source,long session,MethodInfo action)
        {
            var oldPlayers=Main.player;NPC oldActor=Main.npc[16];int oldMode=Main.netMode;Main.netMode=1;
            try
            {
                Main.player=new Player[oldPlayers.Length];for(int i=0;i<Main.player.Length;i++)Main.player[i]=new Player{whoAmI=i};
                var actor=Main.npc[16]=new NPC{whoAmI=16};actor.SetDefaults(NPCID.MartianProbe);actor.active=true;actor.position=new Vector2(-actor.width*.5f,-actor.height*.5f);actor.ai[0]=1;actor.ai[1]=59;
                var a=Main.player[0];var b=Main.player[1];a.active=b.active=true;
                a.position=new Vector2(350-a.width*.5f,1-a.height*.5f);b.position=new Vector2(-240-b.width*.5f,240-b.height*.5f);
                float distance;Require(actor.FindClosestPlayer(out distance)==1,"Original Euclidean search defeats Manhattan targeting in the two-player counterexample.");
                var areas=new[]{new MotionRect(a.position.X,a.position.Y,a.width,a.height),new MotionRect(b.position.X,b.position.Y,b.width,b.height)};
                var players=new PredictionPlayers(areas,2,new[]{0,1},areas,new[]{true,true},new[]{false,false});
                var state=(NpcMotionState)read.Invoke(null,new object[]{actor,session});
                var e=new PredictionEnvironment{Multiplayer=true,Players=players,PlayerTimelineActive=true,PlayerIndex=1,PlayerX=b.Center.X,PlayerY=b.Center.Y};
                actor.AI();object[] args={state,e,null,1,PredictionStop.None,null,0,false};
                Require((bool)action.Invoke(null,args),"Captured actual Euclidean winner permits native clock60 escape.");state=(NpcMotionState)args[0];
                Require(state.A0==actor.ai[0] && state.A1==actor.ai[1] && state.A3==actor.ai[3] && state.Vx==actor.velocity.X && state.Vy==actor.velocity.Y,"Original two-player scan-to-escape action agrees with scalar kernel.");
                a.active=false;b.ghost=true;b.dead=false;Require(actor.FindClosestPlayer(out distance)==1,"Original fallback includes active nondead ghost.");
                b.dead=true;Require(actor.FindClosestPlayer(out distance)==1,"Original fallback includes active dead player independently of ghost.");
                b.active=false;Require(actor.FindClosestPlayer(out distance)==-1,"Original empty active-player search retains missing winner.");
            }
            finally{Main.player=oldPlayers;Main.npc[16]=oldActor;Main.netMode=oldMode;}
            Console.WriteLine("PASS original MartianProbe Euclidean/two-pass/dead/ghost search and scan-to-escape oracle");
        }

        // Uses the existing isolated full Player -> NPC -> Projectile -> host
        // schedule. The native phase/birth transitions below happen before a
        // freeze; every frozen point is checked against that continuing actor.
        // No real game, user save, extra live AI or solver-private input is used.
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");
            Require(GetOptional(source,"Native")==null,"Related controllers use the default scalar rolling producer.");
            var rows=new List<string>{"scene,frame,type,tick,phase,selected,published,count,stop,matched,maxError,tangentError,crossPhase"};
            var failures=new List<string>();int scenes=0,matched=0,curved=0,crossed=0;
            bool oldGood=Main.getGoodWorld;int oldInvasion=Main.invasionType;Main.getGoodWorld=false;
            var diagnostic=new Harmony("JueMingR.Probe.RelatedProjectileFailure");
            var projectileUpdate=typeof(Projectile).GetMethod("Update",Flags);
            var npcUpdate=typeof(NPC).GetMethod("UpdateNPC",Flags);
            diagnostic.Patch(projectileUpdate,finalizer:new HarmonyMethod(typeof(NativeCombatRelatedChecks).GetMethod(nameof(ProjectileFailure),Flags)));
            diagnostic.Patch(npcUpdate,prefix:new HarmonyMethod(typeof(NativeCombatRelatedChecks).GetMethod(nameof(BeforeRetirement),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatRelatedChecks).GetMethod(nameof(AfterRetirement),Flags)));
            try
            {
                for(int variant=0;variant<11;variant++)
                {
                    int type=variant==0?NPCID.AncientDoom:variant==1 || variant==6?NPCID.BrainofCthulhu:variant==2?NPCID.MoonLordCore:variant>=7?NPCID.MartianSaucerCore:NPCID.MartianProbe;
                    string scene=type+"-natural-related"+(variant==3?"-no-raid":variant==4?"-raid":variant==5?"-wall":variant==6?"-outside-crimson":variant==7?"-saucer-flat":variant==8?"-saucer-gap":variant==9?"-saucer-edge":variant==10?"-saucer-far":"");scenes++;
                    try
                    {
                        Setup(context);Main.invasionType=variant==4?4:0;
                        NPC actor=type==NPCID.AncientDoom?BirthDoom(context,step):type==NPCID.BrainofCthulhu?ReceivingBrain(context,step):type==NPCID.MoonLordCore?ReceivingCore(context,step):variant>=7?ReceivingSaucer(context,step):ProbeBirth(variant==5);
                        Require(actor!=null && actor.active,"Natural related actor is available: "+scene);
                        if(variant==6)BrainBiome(false);
                        if(variant>=8)SaucerEnvironment(variant);
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                        var freezes=new List<NpcTrajectory>();int selectedCount=0,published=0,localMatches=0,longMatches=0,crossMatches=0,wallTurns=0,retireMatches=0;double maximum=0,tangent=0;
                        int previousDirection=actor.direction;
                        int nativeRetireMatches=0,deadActions=0;NpcMotionState retireState=default(NpcMotionState);PredictionStop retireStop=PredictionStop.None;bool retirePending=false;
                        if(variant==9)
                        {
                            var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);var terrain=(IPredictionTerrain)Activator.CreateInstance(Get(source,"Terrain").GetType(),true);long session=(long)Get(host,"Session");
                            beforeRetirement=n=>
                            {
                            // The independent action oracle must not reset Source's live cells.
                            terrain.Reset();
                                retirePending=false;if(!ReferenceEquals(n,actor) || !Main.LocalPlayer.dead)return;
                                retireState=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,retireState,terrain};tracking.Invoke(null,args);retireState=(NpcMotionState)args[1];var p=Main.LocalPlayer;
                                var e=new PredictionEnvironment{Players=new PredictionPlayers(new[]{new MotionRect(p.Hitbox.X,p.Hitbox.Y,p.width,p.height)},1,new[]{p.whoAmI}),PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerDead=true,Expert=Main.expertMode,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,GravityWorldSurface=Main.worldSurface};
                                terrain.Reset();bool advanced=NpcMotion.Step(ref retireState,null,0,e,terrain,1,true,out retireStop);
                                Require(advanced || retireStop==PredictionStop.Despawn,"Actual dead-target action has a deterministic retirement motor: "+retireStop);retirePending=true;
                            };
                            afterRetirement=n=>
                            {
                                if(!retirePending || !ReferenceEquals(n,actor))return;retirePending=false;
                                if(retireStop==PredictionStop.Despawn){Require(!n.active && !retireState.Active,"Original edge action retires exactly with the dead-target scalar branch.");nativeRetireMatches++;}
                                else Require(n.active && Vector2.Distance(n.position,new Vector2(retireState.X,retireState.Y))<=.08 && Math.Abs(n.velocity.X-retireState.Vx)<=.0001 && Math.Abs(n.velocity.Y-retireState.Vy)<=.0001,"Scheduled original dead-target retreat matches its natural one-action fork.");deadActions++;
                            };
                        }
                        for(int frame=0;frame<(variant>=7?500:type==NPCID.AncientDoom?450:type==NPCID.MartianProbe?450:220) && actor.active;frame++)
                        {
                            if(variant==9 && frame==200)
                            {
                                // Living BordersMovement clamps topY to640,
                                // so a ledge at208 cannot drive the top exit.
                                // Preserve genuine long live-player windows,
                                // then establish retreat with an actual death.
                                Main.LocalPlayer.KillMe(Terraria.DataStructures.PlayerDeathReason.ByOther(0),1000000,0);
                                Require(Main.LocalPlayer.dead,"Original player death establishes Saucer retirement.");freezes.Clear();
                            }
                            NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();
                            if(!actor.active)
                            {
                                foreach(var frozen in freezes)if(frozen.Stop==PredictionStop.Despawn && (long)Main.GameUpdateCount-frozen.SampleTick==frozen.Count)retireMatches++;
                                break;
                            }
                            if(type==NPCID.MartianProbe && previousDirection!=0 && previousDirection!=actor.direction)wallTurns++;
                            previousDirection=actor.direction;
                            bool selected=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,actor);
                            var path=NativeCombatPresentationChecks.CurrentPath(host);if(selected)selectedCount++;
                            if(path!=null && ReferenceEquals(path.Identity.Token,actor))
                            {
                                Require(selected && path.SampleTick==Main.GameUpdateCount,"Same-actor completed-update related publication: "+scene);published++;
                                if(frame%5==0 && path.Count>1)freezes.Add(path);
                            }
                            for(int c=freezes.Count-1;c>=0;c--)
                            {
                                var frozen=freezes[c];int offset=(int)((long)Main.GameUpdateCount-frozen.SampleTick);
                                if(offset>=frozen.Count){freezes.RemoveAt(c);continue;}if(offset<=0)continue;
                                var point=frozen[offset];double error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),actor.position);maximum=Math.Max(maximum,error);localMatches++;matched++;
                                Require(point.CanReceive==(!actor.dontTakeDamage && !actor.immortal) && point.NoTileCollide==actor.noTileCollide,"Future related receiving/collision phase: "+scene+" offset="+offset);
                                if(offset>=30){longMatches++;tangent=Math.Max(tangent,Vector2.Distance(actor.position,new Vector2(frozen[0].Bounds.X+frozen[0].Vx*offset,frozen[0].Bounds.Y+frozen[0].Vy*offset)));}
                                if(point.Phase!=frozen[0].Phase)crossMatches++;
                                Require(error<=.08,"Frozen natural related forecast "+scene+" offset="+offset+" error="+error.ToString("R",CultureInfo.InvariantCulture)+" stop="+frozen.Stop+" phase="+actor.ai[0]+" forecastPhase="+point.Phase+" actual="+actor.position+" predicted="+point.Bounds.X+","+point.Bounds.Y+" velocity="+actor.velocity+" player="+Main.LocalPlayer.position);
                            }
                            rows.Add(string.Join(",",scene,frame,type,Main.GameUpdateCount,actor.ai[0].ToString("R",CultureInfo.InvariantCulture),selected,path!=null,path?.Count??0,path?.Stop.ToString()??"none",localMatches,maximum.ToString("R",CultureInfo.InvariantCulture),tangent.ToString("R",CultureInfo.InvariantCulture),crossMatches));
                        }
                        Require(selectedCount>0 && published>0 && longMatches>=30,"Natural default related producer must publish and match beyond its former tangent horizon: "+scene+" selected="+selectedCount+" published="+published+" longMatches="+longMatches);
                        if(type==NPCID.BrainofCthulhu && variant==1)Require(crossMatches>=30,"Brain forecasts cross the ORIGINAL chosen fade/teleport/fade phases, matches="+crossMatches);
                        if(variant==6)Require(!Main.LocalPlayer.ZoneCrimson && actor.localAI[3]>=60 && (actor.ai[0]==2 || !actor.active),"Original receiving Brain develops the outside-Crimson retirement motor.");
                        if(type==NPCID.MartianProbe && variant!=5)Require(crossMatches>=30 && !actor.active && retireMatches>0,"Probe crosses native patrol/scan/escape and exact server retirement with/without existing raid: "+scene+" cross="+crossMatches+" retire="+retireMatches);
                        if(type==NPCID.AncientDoom)Require(!actor.active && retireMatches>0,"Natural owner-linked Doom expires on the exact forecast action.");
                        if(variant==5)Require(wallTurns>0,"Probe patrol actually collides with the solid wall and turns.");
                        if(variant==7 || variant==8)Require(crossMatches>=30,"Saucer forecast crosses the actual receiving damping/pursuit/terrain attack phases: "+scene);
                        if(variant==9)Require(!actor.active && nativeRetireMatches>0 && deadActions>=30,"Actual player death reaches the original edge through matched natural actions: "+scene+" active="+actor.active+" retire="+nativeRetireMatches+" deadActions="+deadActions);
                        if(variant==10)Require(!actor.active && retireMatches>0,"Saucer reaches the original world edge and exact forecast retirement: "+scene+" active="+actor.active+" position="+actor.position+" velocity="+actor.velocity+" retireMatches="+retireMatches);
                        if(type!=NPCID.AncientDoom)Require(tangent>8,"The old long tangent must disagree with natural boss steering: "+scene+" tangent="+tangent);
                        if(tangent>8)curved++;if(crossMatches>=30)crossed++;
                        Console.WriteLine("RELATED scene="+scene+" selected="+selectedCount+" published="+published+" matched="+localMatches+" longMatches="+longMatches+" maxError="+maximum.ToString("R",CultureInfo.InvariantCulture)+" tangentError="+tangent.ToString("R",CultureInfo.InvariantCulture)+" crossPhase="+crossMatches);
                    }
                    catch(Exception failure) when(!(failure is OutOfMemoryException)){failures.Add(scene+": "+failure.Message);Console.WriteLine("RELATED FAIL "+scene+": "+failure);}
                    finally{beforeRetirement=afterRetirement=null;}
                }
                Require(failures.Count==0,"Natural related scene failures="+failures.Count+": "+string.Join(" | ",failures));
                Require(curved>=2 && crossed>=1,"Related receiving-stage original turns and teleport transitions were exercised.");
                Console.WriteLine("PASS related full-native default continuous scenes="+scenes+" matched="+matched+" curved="+curved+" crossed="+crossed);
            }
            finally{beforeRetirement=afterRetirement=null;diagnostic.Unpatch(projectileUpdate,HarmonyPatchType.Finalizer,diagnostic.Id);diagnostic.Unpatch(npcUpdate,HarmonyPatchType.All,diagnostic.Id);Main.getGoodWorld=oldGood;Main.invasionType=oldInvasion;Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"related-motion-continuous.csv"),rows);}
        }
        private static void Setup(object context)
        {
            NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.GameMode=0;Main.dayTime=false;
            // Genuine part deaths execute NPCLoot. Use the same original
            // database/resolver initialization as the established native arena;
            // skipping deaths or suppressing loot would hide the phase proof.
            if(Main.ItemDropSolver==null)
            {Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);}
            Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};var player=Main.LocalPlayer;
            player.position=new Vector2(1100,2400-player.height);player.fallStart=player.fallStart2=(int)(player.position.Y/16);player.statLife=player.statLifeMax=player.statLifeMax2=5000;player.creativeGodMode=true;player.immune=true;player.immuneTime=100000;
            for(int i=0;i<player.hurtCooldowns.Length;i++)player.hurtCooldowns[i]=100000;
            for(int i=0;i<3;i++)player.armor[i].SetDefaults(696+i);
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions(path:false,mouseCenter:true,clearLine:false,radius:50));
        }
        private static NPC BirthDoom(object context,Action step)
        {
            Main.GameMode=1;
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1700,2100,NPCID.CultistBoss,Start:16,Target:Main.myPlayer);var owner=Main.npc[slot];
            // R06 exposed that fresh Cultist initialization overwrites ai0,
            // and normal/full-life Cultists cannot choose the Doom attack.
            // Complete the real intro and apply one legitimate quarter-life
            // hit, which raises the original expert Doom choice probability.
            // Native RNG still chooses the phase; diagnostics record each one.
            owner.timeLeft=750;bool hit=false;var phases=new Dictionary<int,int>();
            for(int frame=0;frame<9600 && owner.active;frame++)
            {
                NativeCombatModeledImpactChecks.SampleMouse(context,owner.Center);step();
                int phase=(int)owner.ai[0],seen;phases.TryGetValue(phase,out seen);phases[phase]=seen+1;
                if(!hit && !owner.dontTakeDamage && owner.localAI[0]!=0)
                {owner.StrikeNPCNoInteraction(owner.lifeMax*4/5+owner.defense+100,0,0);Require(owner.active && owner.life<owner.lifeMax/4,"Actual Cultist hit enters the native expert quarter-life Doom selection gate.");hit=true;Console.WriteLine("RELATED NATURAL-CULTIST-HIT life="+owner.life+"/"+owner.lifeMax+" expert="+Main.expertMode+" phase="+owner.ai[0]+" attack="+owner.ai[3]);}
                for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type==NPCID.AncientDoom)
                {Require(hit && owner.localAI[0]!=0 && Main.npc[i].ai[0]==slot,"Natural expert Cultist birth encodes the necessary full owner slot.");return Main.npc[i];}
            }
            var visits=new List<string>();foreach(var pair in phases)visits.Add(pair.Key+":"+pair.Value);
            throw new InvalidOperationException("Cultist native attack did not create AncientDoom within its finite attack window. active="+owner.active+" life="+owner.life+"/"+owner.lifeMax+" expert="+Main.expertMode+" position="+owner.position+" target="+owner.target+" attack="+owner.ai[3]+" visited="+string.Join("/",visits));
        }
        private static NPC ProbeBirth(bool wall)
        {
            if(wall)for(int y=125;y<150;y++)
            {Main.tile[103,y]=new Tile();Main.tile[103,y].active(true);Main.tile[103,y].type=TileID.Stone;}
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1700,2100,NPCID.MartianProbe,Start:16,Target:Main.myPlayer);
            var actor=Main.npc[slot];actor.timeLeft=750;
            return actor;
        }
        private static NPC ReceivingBrain(object context,Action step)
        {
            BrainBiome(true);
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1700,2100,NPCID.BrainofCthulhu,Start:16,Target:Main.myPlayer);var actor=Main.npc[slot];actor.timeLeft=750;int actualHits=0;
            for(int frame=0;frame<30;frame++)
            {
                NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();
                for(int i=0;i<Main.maxNPCs;i++)
                {
                    var part=Main.npc[i];if(part.active && part.type==NPCID.Creeper && !part.dontTakeDamage)
                    {part.StrikeNPCNoInteraction(1000000,0,0);actualHits++;}
                }
                if(actor.ai[0]<0 && !actor.dontTakeDamage)
                {Require(actualHits>0 && actor.localAI[0]!=0,"Native Creeper damage and Brain transition establish receiving second phase.");return actor;}
            }
            throw new InvalidOperationException("Brain did not naturally enter its receiving phase after native Creeper hits.");
        }
        private static void BrainBiome(bool crimson)
        {
            // Real isolated arena tiles drive the native scene/biome scanner;
            // no observed biome bit is hand-written to hide the AI54 tail.
            for(int x=30;x<110;x++)for(int y=150;y<180;y++)Main.tile[x,y].type=crimson?TileID.Crimstone:TileID.Stone;
            Main.PlayerSceneMetrics.Reset();Main.LocalPlayer.UpdateSceneMetrics();Main.LocalPlayer.UpdateBiomes();
            Require(Main.LocalPlayer.ZoneCrimson==crimson,"Original arena scan establishes the requested Crimson presence.");
        }
        private static NPC ReceivingCore(object context,Action step)
        {
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1700,2100,NPCID.MoonLordCore,Start:16,Target:Main.myPlayer);var actor=Main.npc[slot];actor.timeLeft=750;int actualHits=0;
            // Parts are struck only when native AI has opened them. Their real
            // checkDead/spawn-eye transition then exposes the initialized core;
            // no test writes a fictitious damageable core phase or localAI gate.
            for(int frame=0;frame<1600;frame++)
            {
                NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();
                for(int i=0;i<Main.maxNPCs;i++)
                {
                    var part=Main.npc[i];if(part.active && (part.type==NPCID.MoonLordHead || part.type==NPCID.MoonLordHand) && part.ai[3]==slot && part.ai[0]!=-2 && !part.dontTakeDamage)
                    {part.StrikeNPCNoInteraction(1000000,0,0);actualHits++;}
                }
                if(actor.ai[0]==1 && !actor.dontTakeDamage)
                {Require(actualHits==3 && actor.localAI[3]!=0,"Three native receiving-part hits expose the real initialized core.");return actor;}
            }
            throw new InvalidOperationException("MoonLord parts did not naturally expose the core within the finite native cycle window.");
        }
        private static NPC ReceivingSaucer(object context,Action step,int x=1700,int y=2100)
        {
            Main.GameMode=1;
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),x,y,NPCID.MartianSaucerCore,Start:16,Target:Main.myPlayer);
            var actor=Main.npc[slot];actor.timeLeft=750;int actualHits=0;
            for(int frame=0;frame<30;frame++)
            {
                NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();
                for(int i=0;i<Main.maxNPCs;i++)
                {
                    var part=Main.npc[i];if(part.active && (part.type==NPCID.MartianSaucerTurret || part.type==NPCID.MartianSaucerCannon) && part.ai[0]==slot && !part.dontTakeDamage)
                    {part.StrikeNPCNoInteraction(1000000,0,0);actualHits++;}
                }
                if(actor.ai[0]==1 && !actor.dontTakeDamage)
                {Require(actualHits==4 && actor.localAI[3]!=0,"Four original part deaths expose the expert Saucer receiving phase.");return actor;}
            }
            throw new InvalidOperationException("Saucer did not enter the original receiving phase after four genuine part hits.");
        }
        private static void SaucerEnvironment(int variant)
        {
            var player=Main.LocalPlayer;
            // The original447 beam reads one tile below its scan. Keep an
            // actual deep pit and solid bottom, rather than a void extending
            // to the original array boundary.
            if(variant==8)for(int x=90;x<130;x++)for(int y=150;y<220;y++)Main.tile[x,y].active(false);
            if(variant==10)
            {
                // A real wide isolated world leaves the target alive at more
                // than5600px. Native -2 restores phase2 in the same action,
                // resets its clocks and keeps the ascending retreat motor.
                var tiles=new Tile[1000,240];
                for(int x=0;x<1000;x++)for(int y=0;y<240;y++)
                {var tile=new Tile();if(y>=150){tile.active(true);tile.type=TileID.Stone;}tiles[x,y]=tile;}
                Main.tile=tiles;Main.maxTilesX=1000;Main.rightWorld=16000;
                player.position=new Vector2(10000,2400-player.height);
            }
            player.velocity=Vector2.Zero;player.fallStart=player.fallStart2=(int)(player.position.Y/16);
        }
    }
}
