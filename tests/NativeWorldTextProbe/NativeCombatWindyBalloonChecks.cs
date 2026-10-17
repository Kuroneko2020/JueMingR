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
    // Original594/controlled1 oracles cover real birth, both native
    // slot orders, selected-item prelude and real release/republication have
    // executable checks against the original native actions.
    internal static class NativeCombatWindyBalloonChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Action<NPC> before,after;
        private static readonly List<string> network=new List<string>();
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static bool Network(int msgType,int remoteClient,int ignoreClient,Terraria.Localization.NetworkText text,int number,float number2,float number3,float number4,int number5,int number6,int number7)
        {
            if((msgType!=23 && msgType!=28) || number<0 || number>=Main.maxNPCs || !Family(Main.npc[number]))return true;
            network.Add(string.Join("/",msgType,remoteClient,ignoreClient,text,number,number2,number3,number4,number5,number6,number7));return false;
        }
        internal static void Boundaries()
        {
            var model=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcWindyBalloonMotion",true).GetMethod("Step",Flags);
            var parent=new NpcMotionState{Identity=new NpcIdentity(1,new object(),20,1,594,594),Style=113,Active=true,Life=1,Width=20,Height=20,X=1500,Y=2200,A1=1,A3=5,L0=1,WindyRelationsCaptured=true,WindyRelationsComplete=true,WindySlaveCaptured=true};
            var e=new PredictionEnvironment{WindTarget=.5f};object[] args={parent,e,null,1,PredictionStop.None,null,0,false};
            Require((bool)model.Invoke(null,args) && ((NpcMotionState)args[0]).Vy==-.2f && ((NpcMotionState)args[0]).Vx==.15f,"Known absent slave uses true windTarget drift without a player or invented raw geometry.");
            parent.L0=0;args[0]=parent;
            Require(!(bool)model.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.PhaseBoundary,"Unobserved native slave birth stops before guessing identity or variant scale.");
            parent.L0=1;parent.WindySlaveValid=true;parent.WindySlaveIdentity=new NpcIdentity(1,new object(),5,1,1,1);args[0]=parent;
            Require(!(bool)model.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.MissingDependency,"Captured valid slave absent from its full-identity closure is a dependency failure, not independent drift.");
        }
        internal static void Lifecycle(object context)
        {
            NativeCombatLiveContextChecks.FlightWorld();
            var source=Get(Get(context,"CombatObservation"),"Prediction");var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var rules=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcCollisionRules",true).GetMethod("MovementBounds",Flags);
            var prelude=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcWindySlimePrelude",true);
            var line=prelude.GetMethod("CanHitNotSolidOrPlatforms",Flags);
            var n=new NPC();n.SetDefaults(594);
            foreach(float scale in new[]{.5f,.75f,1f,1.25f,1.5f})foreach(float x in new[]{1600f,1600.125f})
            {
                n.position=new Vector2(x,2250.375f);n.ai[1]=scale;
                n.GetTileCollisionParameters(out Vector2 position,out int width,out int height);
                var state=new NpcMotionState{Identity=new NpcIdentity(1,n,0,1,594,594),Style=113,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,A1=scale};
                var actual=(MotionRect)rules.Invoke(null,new object[]{state});
                Require(actual.X==position.X && actual.Y==position.Y && actual.Width==width && actual.Height==height,"Actual594 GetTileCollisionParameters exact profile scale="+scale+" x="+x);
            }
            // Native CanHitWithCheck uses the same corridor as ordinary
            // CanHit but adds raw-active solid/platform checks at every step.
            // Inactive vs actuated platform are deliberately separate cases.
            for(int variant=0;variant<8;variant++)
            {
                NativeCombatLiveContextChecks.FlightWorld();
                for(int x=98;x<=109;x++)for(int y=98;y<=102;y++)
                {Main.tile[x,y].active(false);Main.tile[x,y].inActive(false);Main.tile[x,y].slope(0);Main.tile[x,y].halfBrick(false);Main.tile[x,y].type=0;}
                var obstacle=Main.tile[104,100];
                if(variant>0)
                {
                    obstacle.active(true);obstacle.type=variant==1 || variant==2 || variant==3?(ushort)19:(ushort)1;
                    if(variant==2)obstacle.active(false);if(variant==3)obstacle.inActive(true);
                    if(variant==5)obstacle.slope(1);if(variant==6)obstacle.halfBrick(true);if(variant==7)obstacle.inActive(true);
                }
                Vector2 from=new Vector2(100*16+.125f,100*16+.375f),to=new Vector2(108*16+.625f,100*16+.875f);
                bool native=Collision.CanHitWithCheck(from,20,20,to,21,21,DelegateMethods.NotSolidOrPlatforms);
                var e=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY};terrain.Reset();
                object[] args={new MotionRect(from.X,from.Y,20,20),new MotionRect(to.X,to.Y,21,21),e,terrain,false,PredictionStop.None};
                Require((bool)line.Invoke(null,args) && (bool)args[4]==native,"Actual metal prelude CanHitWithCheck variant="+variant+" native="+native+" stop="+args[5]);
            }
            var observer=new Harmony("JueMingR.Probe.WindySelectedItemLifecycle");var send=typeof(NetMessage).GetMethod("SendData",Flags);
            observer.Patch(send,prefix:new HarmonyMethod(typeof(NativeCombatWindyBalloonChecks).GetMethod(nameof(Network),Flags)){priority=Priority.First+100});
            try
            {
                int comparisons=0;
                foreach(int item in new[]{-1,2,3,751,29,174,364,1104,365,1105,366,1106,3736,3347,5507,8})foreach(float vy in new[]{-1.25f,0f,1.25f})
                {
                    Setup(context,0);var pair=Birth(context,20);var child=pair[1];
                    // These are isolated selected-item input counterexamples,
                    // not assertions that the native lottery chose this item.
                    // The same actual born NPC/state is sampled then receives
                    // one complete original UpdateNPC, including its prelude.
                    child.ai[1]=item;child.velocity.Y=vy;Main.getGoodWorld=item==8;
                    var group=Capture(context,child);var expected=group[Index(group,child)];terrain.Reset();PredictionStop stop;
                    bool valid=NpcMotion.Step(ref expected,group,group.Length,EnvironmentFor(child),terrain,1,true,out stop);child.UpdateNPC(child.whoAmI);
                    Require(valid && PoseMatches(expected,child) && expected.Life==child.life && expected.LifeMax==child.lifeMax && expected.Width==child.width && expected.Height==child.height && expected.A0==child.ai[0],"Actual controlled item pre-return full action item="+item+" vy="+vy+" stop="+stop+" expectedA0="+expected.A0+" nativeA0="+child.ai[0]);comparisons++;
                    if(child.ai[0]!=-999)
                    {
                        group[Index(group,child)]=expected;
                        var needed=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcWindyBalloonMotion",true).GetMethod("NeededWriter",Flags);
                        Require(!(bool)needed.Invoke(null,new object[]{group[Index(group,pair[0])],group,group.Length}),"Actual selected item leaves controlled mode; its old unused parent cannot poison the released slime horizon.");
                    }
                }
                SourceAndDeath(context);
                LostSlaveCollision(context);
                ParentScaleField(context);
                Console.WriteLine("PASS Windy native collision-profile10 / raw-delegate corridor8 / selected-item complete-action="+comparisons);
            }
            finally{Main.getGoodWorld=false;observer.Unpatch(send,HarmonyPatchType.Prefix,observer.Id);}
        }

        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            string selection=Environment.GetEnvironmentVariable("JUEMINGR_WINDY_BALLOON_SCENE");int leaf=-1;
            if(!string.IsNullOrEmpty(selection) && (!int.TryParse(selection,out leaf) || leaf<0 || leaf>5))throw new ArgumentException("JUEMINGR_WINDY_BALLOON_SCENE must be0..5 or unset.");
            Console.WriteLine("WINDY actualLeaves="+(leaf<0?"0,1,2,3,4,5":leaf.ToString(CultureInfo.InvariantCulture)));
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var terrain=(IPredictionTerrain)Get(source,"Terrain");var world=Get(host,"World");
            var observer=new Harmony("JueMingR.Probe.WindyNaturalSlots");var update=typeof(NPC).GetMethod("UpdateNPC",Flags);var send=typeof(NetMessage).GetMethod("SendData",Flags);
            observer.Patch(update,prefix:new HarmonyMethod(typeof(NativeCombatWindyBalloonChecks).GetMethod(nameof(Before),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatWindyBalloonChecks).GetMethod(nameof(After),Flags)));
            observer.Patch(send,prefix:new HarmonyMethod(typeof(NativeCombatWindyBalloonChecks).GetMethod(nameof(Network),Flags)){priority=Priority.First+100});network.Clear();
            var records=new List<string>{"scene,frame,parentActions,childActions,matches,long,pop,slaveDeath,released,fresh,recoveryLong,error"};
            try
            {
                for(int scene=0;scene<6;scene++)
                {
                    if(leaf>=0 && leaf!=scene)continue;Setup(context,scene);
                    var pair=Birth(context,scene==1 || scene==4?0:20);var parent=pair[0];var child=pair[1];
                    Require(scene==1 || scene==4?parent.whoAmI<child.whoAmI:child.whoAmI<parent.whoAmI,"Actual NewNPC produces the requested different native slot order, without moving identities.");
                    var selected=scene==2 || scene>=4?child:parent;
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                    var frozen=new List<NpcTrajectory>();int parentActions=0,childActions=0,matched=0,longMatches=0,pop=0,slaveDeath=0,released=0,fresh=0,recoveryLong=0;double max=0;
                    NPC pending=null;NpcMotionState predicted=default(NpcMotionState);NpcMotionState[] predictedGroup=null;bool valid=false,parentHadSlave=false;PredictionStop stop=PredictionStop.None;NpcMotionState initial=default(NpcMotionState);PredictionEnvironment actionEnvironment=default(PredictionEnvironment);
                    long changedAt=-1;NpcTrajectory publicationBefore=null;bool publicationPending=false,popObserved=false;int legalBoundaries=0;
                    before=n=>
                    {
                        pending=null;if(!ReferenceEquals(n,parent) && !ReferenceEquals(n,child) || !n.active)return;
                        predictedGroup=Capture(context,n);predicted=predictedGroup[Index(predictedGroup,n)];initial=predicted;actionEnvironment=EnvironmentFor(n);parentHadSlave=ReferenceEquals(n,parent) && predicted.WindySlaveValid;terrain.Reset();
                        valid=NpcMotion.Step(ref predicted,predictedGroup,predictedGroup.Length,actionEnvironment,terrain,1,true,out stop);pending=n;
                        if(!valid && stop==PredictionStop.PhaseBoundary)
                        {
                            Require(n.type==594 && n.localAI[0]==0 && Main.netMode!=1 || n.type==1 && (n.ai[1]==0 || n.ai[1]==150 || n.ai[1]==314),"Current original action reaches the explicit slave-birth/item/terrain consumer, not an unnamed unsupported phase.");
                            legalBoundaries++;return;
                        }
                        Require(valid || stop==PredictionStop.Despawn,"Natural Windy current-action mechanism is modeled or its exact birth/item/terrain consumer boundary: "+stop);
                    };
                    after=n=>
                    {
                        if(!ReferenceEquals(n,pending))return;
                        if(!valid)
                        {
                            if(stop==PredictionStop.Despawn)
                            {
                                if(n.active)
                                {
                                    var observedRows=Capture(context,n);
                                    Console.WriteLine("WINDY FIRST invalid-retire scene="+scene+" tick="+Main.GameUpdateCount+" input="+State(initial)+" pure="+State(predicted)+" native="+State(observedRows[Index(observedRows,n)]));
                                    Console.WriteLine("WINDY FIRST env player="+actionEnvironment.PlayerIndex+" center="+R(actionEnvironment.PlayerX)+","+R(actionEnvironment.PlayerY)+" size="+actionEnvironment.PlayerWidth+","+actionEnvironment.PlayerHeight+" aliveTimeline="+actionEnvironment.PlayerTimelineActive+" dead="+actionEnvironment.PlayerDead+" players="+(actionEnvironment.Players==null?"null":actionEnvironment.Players.Count.ToString())+" world="+actionEnvironment.WorldWidth+","+actionEnvironment.WorldHeight);
                                    for(int slot=0;slot<Main.maxPlayers;slot++){var raw=Main.player[slot];if(raw!=null && raw.active)Console.WriteLine("WINDY FIRST activePC slot="+slot+" pos="+R(raw.position.X)+","+R(raw.position.Y)+" size="+raw.width+","+raw.height+" dead="+raw.dead+" ghost="+raw.ghost); }
                                }
                                Require(!n.active,"Windy deterministic retirement is an actual original retirement.");
                                if(parentHadSlave)
                                {
                                    var retiredWrite=predictedGroup[Index(predictedGroup,child)];
                                    Require(retiredWrite.X==child.position.X && retiredWrite.Y==child.position.Y && retiredWrite.A0==child.ai[0],"Actual collision/health death commits the real slave release before later slot processing.");
                                }
                            }
                            return;
                        }
                        bool sameAction=PoseMatches(predicted,n) && predicted.A0==n.ai[0] && predicted.A3==n.ai[3] && predicted.Width==n.width && predicted.Height==n.height;
                        if(!sameAction)
                        {
                            var observedRows=Capture(context,n);
                            Console.WriteLine("WINDY FIRST current-action scene="+scene+" tick="+Main.GameUpdateCount+" input="+State(initial)+" pure="+State(predicted)+" native="+State(observedRows[Index(observedRows,n)]));
                        }
                        Require(sameAction,"Natural Windy current-action type="+n.type+" slot="+n.whoAmI+" expected="+predicted.X+","+predicted.Y+" actual="+n.position);
                        if(ReferenceEquals(n,parent))
                        {
                            parentActions++;
                            // Child has not executed another slot while inside
                            // this parent call. Its exact direct write must be
                            // checked even when its slot ran earlier this frame.
                            if(parentHadSlave)
                            {
                                var copy=predictedGroup[Index(predictedGroup,child)];
                                Require(copy.X==child.position.X && copy.Y==child.position.Y && copy.Vx==child.velocity.X && copy.Vy==child.velocity.Y && copy.A0==child.ai[0],"Actual parent direct slave write preserves original before-tail slot order.");
                            }
                        }
                        else childActions++;
                    };
                    for(int frame=0;frame<1200;frame++)
                    {
                        if(scene==2 && frame==300)
                        {
                            publicationBefore=cache.Read(0);changedAt=(long)Main.GameUpdateCount;publicationPending=true;frozen.Clear();
                            parent.StrikeNPCNoInteraction(100000,0,0);Require(!parent.active && child.active && child.ai[0]==0,"Real parent Strike/checkDead/HitEffect releases the actual born slave.");pop++;popObserved=true;
                        }
                        if(scene==3 && slaveDeath==0 && parentActions>30 && childActions>30 && longMatches>=30 && parent.active && child.active && !parent.collideX && !parent.collideY && !parent.wet)
                        {
                            // The drift branch is only reachable without an
                            // already observed collision. Native no-slave AI
                            // immediately pops on either collision flag. Wait
                            // for real air state after both observed role and
                            // long-window denominators exist; never move/reset
                            // the actor to manufacture a continuation window.
                            var deathRows=Capture(context,parent);
                            Console.WriteLine("WINDY real slave-death air input frame="+frame+" parent="+State(deathRows[Index(deathRows,parent)]));
                            publicationBefore=cache.Read(0);changedAt=(long)Main.GameUpdateCount;publicationPending=true;frozen.Clear();
                            child.StrikeNPCNoInteraction(100000,0,0);Require(!child.active && parent.active,"Real slave death leaves the actual balloon to consume its known invalid raw slot.");slaveDeath++;
                        }
                        NativeCombatModeledImpactChecks.SampleMouse(context,selected.Center);step();
                        if((scene==4 || scene==5) && !parent.active && !popObserved)
                        {popObserved=true;pop++;Require(child.active && child.ai[0]!=-999,"Actual static wet/wall collision pops and releases the slave through original HitEffect.");changedAt=(long)Main.GameUpdateCount;publicationPending=true;publicationBefore=frozen.Count>0?frozen[frozen.Count-1]:null;frozen.Clear();}
                        if(popObserved && child.active && child.ai[0]!=-999)released++;
                        var path=cache.Read(0);var prepared=Optional(world,"preparedPath") as NpcTrajectory;
                        if(publicationPending)
                        {
                            Require(path==null || !ReferenceEquals(path,publicationBefore) && path.CaptureTick>=changedAt,"Original receiving/relationship loss withdraws the previous production publication.");
                            Require(prepared==null || ReferenceEquals(prepared,path),"Presentation consumes the fresh Windy publication after actual relationship change.");publicationPending=false;
                        }
                        if(path!=null && ReferenceEquals(path.Identity.Token,selected) && selected.active)
                        {
                            if(changedAt>=0){Require(path.CaptureTick>=changedAt,"Restored Windy window samples the actual changed parent/slave facts.");fresh++;}
                            if(frame%5==0 && path.Count>1 && !frozen.Contains(path))frozen.Add(path);
                        }
                        for(int i=frozen.Count-1;i>=0;i--)
                        {
                            var old=frozen[i];int offset=(int)(Main.GameUpdateCount-old.CaptureTick);
                            if(offset>=old.Count || !selected.active){frozen.RemoveAt(i);continue;}if(offset<1)continue;
                            double error=Vector2.Distance(new Vector2(old[offset].Bounds.X,old[offset].Bounds.Y),selected.position);max=Math.Max(max,error);
                            Require(error<=.08,"Frozen natural Windy scene="+scene+" frame="+frame+" offset="+offset+" error="+error);
                            matched++;if(offset>=30){longMatches++;if(changedAt>=0 && old.CaptureTick>=changedAt)recoveryLong++;}
                        }
                        records.Add(string.Join(",",scene,frame,parentActions,childActions,matched,longMatches,pop,slaveDeath,released,fresh,recoveryLong,max.ToString("R",CultureInfo.InvariantCulture)));
                    }
                    before=after=null;Require(parentActions>30 && childActions>30 && matched>0 && longMatches>=30,"Both real parent/slave roles have an observed useful prefix and long frozen prediction.");
                    if(scene==2 || scene==4 || scene==5)Require(pop>0 && released>0 && fresh>0 && recoveryLong>=30,"Actual native pop releases the child and restores a long ordinary-slime publication.");
                    if(scene==3)Require(slaveDeath==1 && parent.ai[3]==-1 && fresh>0 && recoveryLong>=30,"Actual child death restores useful deterministic wind drift before any subsequent native boundary/retirement.");
                    Console.WriteLine("WINDY scene="+scene+" parent="+parentActions+" child="+childActions+" matched="+matched+" long="+longMatches+" pop="+pop+" slaveDeath="+slaveDeath+" release="+released+" fresh="+fresh+" recoveryLong="+recoveryLong+" boundaries="+legalBoundaries+" max="+max);
                }
            }
            finally
            {before=after=null;observer.Unpatch(update,HarmonyPatchType.All,observer.Id);observer.Unpatch(send,HarmonyPatchType.Prefix,observer.Id);Console.WriteLine("WINDY original network parameters="+string.Join(";",network));File.WriteAllLines(Path.Combine(output,"windy-balloon-continuous.csv"),records);}
        }
        private static void SourceAndDeath(object context)
        {
            Setup(context,0);var first=Birth(context,20);var second=Birth(context,30);var unrelated=Birth(context,40);
            var rows=SourceRows(context,first[1]);
            Require(rows.Length==2 && Contains(rows,first[0]) && Contains(rows,first[1]) && !Contains(rows,second[0]) && !Contains(rows,unrelated[0]),"Production Source acquires only the actual held-slime incoming writer and its necessary slave, not every balloon/slime in the world.");
            // A deliberate native raw-input counterexample, not a claim that
            // NewNPC naturally gives two parents the same child. Both real
            // actor identities remain allocated at their original slots.
            second[0].ai[3]=first[1].whoAmI;
            rows=SourceRows(context,first[1]);
            Require(rows.Length==3 && Contains(rows,first[0]) && Contains(rows,second[0]) && Contains(rows,first[1]) && !Contains(rows,second[1]) && !Contains(rows,unrelated[0]),"Production Source closes all actual incoming raw-slot writers while excluding unrelated original identities.");
            foreach(var row in rows)Require(row.WindyRelationsCaptured && row.WindyRelationsComplete,"Every necessary Windy motor row has independent complete-relation facts.");
            first[1].life=0;rows=SourceRows(context,first[0]);
            Require(rows[Index(rows,first[0])].WindySlaveValid && Contains(rows,first[1]),"Native active/type1/-999 owner validity has no invented Life>0 gate.");
            first[1].life=first[1].lifeMax;
            // A legal raw object with the wrong type is known invalid; its
            // geometry never participates in this parent's wind drift.
            first[0].ai[3]=unrelated[0].whoAmI;rows=SourceRows(context,first[0]);
            var state=rows[Index(rows,first[0])];
            Require(rows.Length==1 && state.WindySlaveCaptured && !state.WindySlaveValid && ReferenceEquals(state.WindySlaveIdentity.Token,unrelated[0]),"In-range foreign raw slot is sampled as known invalid without acquiring an unknown/unused foreign motor.");
            var model=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcWindyBalloonMotion",true).GetMethod("Step",Flags);
            var source=Get(Get(context,"CombatObservation"),"Prediction");var terrain=(IPredictionTerrain)Get(source,"Terrain");terrain.Reset();
            object[] args={state,EnvironmentFor(first[0]),terrain,1,PredictionStop.None,rows,rows.Length,false};
            Require((bool)model.Invoke(null,args) && ((NpcMotionState)args[0]).A3==-1,"Known invalid legal raw slot follows native AI3=-1, not MissingDependency.");
            first[0].ai[3]=-7;rows=SourceRows(context,first[0]);state=rows[Index(rows,first[0])];args[0]=state;args[5]=rows;args[6]=rows.Length;
            Require((bool)model.Invoke(null,args) && ((NpcMotionState)args[0]).A3==-7,"Already out-of-range raw slot preserves its original AI3 value.");

            Setup(context,0);var death=Birth(context,20);var parent=death[0];var child=death[1];
            // This is an isolated already-observed residual health-clock
            // boundary. Both sides receive the same real native input; no
            // buff immunity is cleared and no future injury is fabricated.
            parent.lifeRegenCount=-120;var group=Capture(context,parent);var expected=group[Index(group,parent)];terrain.Reset();PredictionStop stop;
            bool valid=NpcMotion.Step(ref expected,group,group.Length,EnvironmentFor(parent),terrain,1,true,out stop);
            parent.UpdateNPC(parent.whoAmI);var released=group[Index(group,child)];
            Require(!valid && stop==PredictionStop.Despawn && !parent.active && child.active && child.ai[0]==0 && released.A0==0 && released.X==child.position.X && released.Y==child.position.Y,"Committed original health death executes the real594 HitEffect slave release before AI, despite copied death Life=1.");
            Console.WriteLine("PASS Windy production closure single/multiple writers, known-invalid raw object/index, active-life0 and original residual-health death release.");
        }
        private static bool Contains(NpcMotionState[] rows,NPC n)
        {for(int i=0;i<rows.Length;i++)if(ReferenceEquals(rows[i].Identity.Token,n))return true;return false;}
        private static void LostSlaveCollision(object context)
        {
            Setup(context,0);var pair=Birth(context,20);var parent=pair[0];var child=pair[1];
            child.StrikeNPCNoInteraction(100000,0,0);
            Require(!child.active && parent.active,"Actual slave death leaves the parent until its own original action.");
            // This is an explicit sampled collision-input counterexample,
            // separate from Continuous's natural air-state death event. It
            // proves why a grounded lost-slave action cannot restore drift.
            parent.collideY=true;
            var rows=Capture(context,parent);var expected=rows[Index(rows,parent)];
            var terrain=(IPredictionTerrain)Get(Get(Get(context,"CombatObservation"),"Prediction"),"Terrain");terrain.Reset();PredictionStop stop;
            bool valid=NpcMotion.Step(ref expected,rows,rows.Length,EnvironmentFor(parent),terrain,1,true,out stop);
            parent.UpdateNPC(parent.whoAmI);
            Require(!valid && stop==PredictionStop.Despawn && !parent.active && parent.ai[3]==-1 && expected.A3==-1,"Original lost-slave grounded branch actually pops before any independent drift window.");
            Console.WriteLine("PASS Windy actual slave death + sampled ground-collision original immediate pop; air-state natural recovery remains separate.");
        }
        private static void ParentScaleField(object context)
        {
            var terrain=(IPredictionTerrain)Get(Get(Get(context,"CombatObservation"),"Prediction"),"Terrain");
            foreach(float scale in new[]{.9f,1.05f,1.15f})
            {
                Setup(context,0);var pair=Birth(context,0);var parent=pair[0];
                // Explicit legal scale-input counterexample on a real born
                // parent/slave pair. This does not claim the failed natural
                // R51 random birth's unprinted AI1 has been reconstructed.
                parent.ai[1]=scale;
                var nativeSize=new Vector2(20f,20f)*scale;
                var rows=Capture(context,parent);var expected=rows[Index(rows,parent)];terrain.Reset();PredictionStop stop;
                bool valid=NpcMotion.Step(ref expected,rows,rows.Length,EnvironmentFor(parent),terrain,1,true,out stop);
                parent.UpdateNPC(parent.whoAmI);
                Require(valid && expected.Width==(int)nativeSize.X && expected.Height==(int)nativeSize.Y && expected.Width==parent.width && expected.Height==parent.height && PoseMatches(expected,parent),"Actual original balloon Vector2 float-field resize and full action scale="+R(scale)+" size="+nativeSize+" pure="+expected.Width+","+expected.Height+" native="+parent.width+","+parent.height+" stop="+stop);
            }
            Console.WriteLine("PASS Windy actual Vector2 field resize at3 legal integer-boundary scale inputs.");
        }
        private static NpcMotionState[] SourceRows(object context,NPC selected)
        {
            var host=Get(context,"CombatObservation");var type=Get(host,"Prediction").GetType();
            // A private pure-value Source owner exercises the actual capture
            // path without mutating the presentation's main publication.
            var isolated=Activator.CreateInstance(type,Flags,null,new object[]{null,false},CultureInfo.InvariantCulture);
            try
            {
                var cache=(NpcPredictionCache)Get(isolated,"Cache");cache.Demand(0,1,32);
                long session=(long)Get(host,"Session");var read=type.GetMethod("Read",Flags);
                var state=(NpcMotionState)read.Invoke(null,new object[]{selected,session});
                type.GetMethod("Prepare",Flags).Invoke(isolated,new object[]{state.Identity,(long)Main.GameUpdateCount});
                int count=(int)Get(isolated,"observedCount");var all=(NpcMotionState[])Get(isolated,"states");
                Require(count>0,"Production Source completes its real input capture before the narrow closure assertion.");
                var result=new NpcMotionState[count];Array.Copy(all,result,count);return result;
            }
            finally{type.GetMethod("Clear",Flags).Invoke(isolated,null);}
        }
        private static NPC[] Birth(object context,int start)
        {
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1600,2130,594,Start:start,Target:Main.myPlayer);
            Require(slot>=0 && slot<Main.maxNPCs,"Actual balloon allocation has a legal full slot.");var parent=Main.npc[slot];Require(parent.localAI[0]==0,"Actual594 birth precedes the original randomized slave creation.");
            // Execute the one real spawning actor action. The subsequent
            // whole world steps keep native slot order. Running a whole frame
            // here would also execute a higher-slot newborn's randomized
            // item prelude before this birth assertion could observe it.
            parent.UpdateNPC(slot);
            var child=parent.AI_113_WindyBalloon_GetSlaveNPC();Require(child!=null && child.active && child.type==1 && child.ai[0]==-999 && parent.localAI[0]==1 && parent.ai[1]==child.scale,"Original594 action creates its actual variant/scale/controlled slave.");
            return new[]{parent,child};
        }
        private static NpcMotionState[] Capture(object context,NPC selected)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var terrain=(IPredictionTerrain)Get(source,"Terrain");long session=(long)Get(host,"Session");
            var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);var required=new HashSet<int>{selected.whoAmI};
            for(int slot=0;slot<Main.maxNPCs;slot++)if(Main.npc[slot].active && Main.npc[slot].type==594)
            {required.Add(slot);int raw=(int)Main.npc[slot].ai[3];if(raw>=0 && raw<Main.maxNPCs)required.Add(raw);}
            var rows=new List<NpcMotionState>();for(int slot=0;slot<Main.maxNPCs;slot++)if(required.Contains(slot))
            {
                var n=Main.npc[slot];var state=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,state,terrain};tracking.Invoke(null,args);state=(NpcMotionState)args[1];
                state.WindyRelationsCaptured=state.WindyRelationsComplete=true;rows.Add(state);
            }
            return rows.ToArray();
        }
        private static int Index(NpcMotionState[] rows,NPC n){for(int i=0;i<rows.Length;i++)if(ReferenceEquals(rows[i].Identity.Token,n))return i;throw new InvalidOperationException("Missing actual Windy actor.");}
        private static string R(float value){return value.ToString("R",CultureInfo.InvariantCulture);}
        private static string State(NpcMotionState n)
        {
            return "type/slot="+n.EffectiveType+"/"+n.Identity.Slot+" pos="+R(n.X)+","+R(n.Y)+" V="+R(n.Vx)+","+R(n.Vy)+" old="+R(n.OldX)+","+R(n.OldY)+" oldV="+R(n.OldVx)+","+R(n.OldVy)+" shape="+n.Width+","+n.Height+" active="+n.Active+" TL="+n.TimeLeft+" life="+n.Life+"/"+n.LifeMax+" AI="+R(n.A0)+","+R(n.A1)+","+R(n.A2)+","+R(n.A3)+" L="+R(n.L0)+","+R(n.L1)+","+R(n.L2)+","+R(n.L3)+" collision="+n.CollideX+","+n.CollideY+" liquid="+n.Wet+","+n.Honey+","+n.Lava+" flags="+n.NoGravity+","+n.NoTileCollide+","+n.InactivityImmune+","+n.Boss+" target="+n.Target+" player="+n.PlayerIndex+"/"+n.HasPlayer+" slave="+n.WindySlaveValid+"/"+n.WindySlaveIdentity.Slot+" regen="+n.Health.Regen+"/"+n.Health.RegenCount;
        }
        private static bool PoseMatches(NpcMotionState expected,NPC actual)
        {return Vector2.Distance(new Vector2(expected.X,expected.Y),actual.position)<=.08 && Math.Abs(expected.Vx-actual.velocity.X)<=.0001 && Math.Abs(expected.Vy-actual.velocity.Y)<=.0001;}
        private static bool Family(NPC n){return n!=null && (n.type==594 || n.type==1);}
        private static object Optional(object n,string member)
        {var f=n.GetType().GetField(member,Flags);return f!=null?f.GetValue(n):n.GetType().GetProperty(member,Flags)?.GetValue(n);}
        private static PredictionEnvironment EnvironmentFor(NPC n)
        {
            var p=Main.player[n.target>=0 && n.target<Main.player.Length?n.target:255];
            // CheckActive consumes ALL active-player hitboxes independently
            // of AI's numbered target. A held child's unused255 target must
            // not replace that original lifetime input with its dummy box.
            var areas=new List<MotionRect>();var slots=new List<int>();
            for(int slot=0;slot<Main.maxPlayers;slot++)
            {var active=Main.player[slot];if(active==null || !active.active)continue;areas.Add(new MotionRect((int)active.position.X,(int)active.position.Y,active.width,active.height));slots.Add(slot);}
            return new PredictionEnvironment{Players=new PredictionPlayers(areas.ToArray(),areas.Count,slots.ToArray()),PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,PlayerVelocityCaptured=true,PlayerTimelineActive=p.active && !p.dead && !p.ghost,PlayerDead=!p.active || p.dead,WindTarget=Main.windSpeedTarget,Wind=Main.windSpeedCurrent,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,GravityWorldSurface=Main.worldSurface,RockLayer=(float)Main.rockLayer,Remix=Main.remixWorld,SkyblockLowTiles=WorldGen.Skyblock.lowTiles,Day=Main.dayTime,GoodWorld=Main.getGoodWorld,Multiplayer=Main.netMode==1};
        }
        private static void Setup(object context,int scene)
        {
            NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();
            // Consume the real frame-entry protection countdown after the
            // isolated world reset; never clear protected slot state by hand.
            for(int i=0;i<2;i++){NativeQuickItemChecks.BeginWorldStep();Main.EverLastingTicker++;NPC.UpdateProtectedSpawnSlots();}
            Main.netMode=0;Main.GameMode=0;Main.dayTime=true;Main.getGoodWorld=false;Main.windSpeedTarget=Main.windSpeedCurrent=.35f;
            if(Main.ItemDropSolver==null){Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);}for(int i=0;i<Main.item.Length;i++)Main.item[i].whoAmI=i;
            if(scene==4)for(int x=150;x<157;x++)for(int y=130;y<150;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(0);}
            if(scene==5)for(int y=100;y<150;y++){Main.tile[150,y].active(true);Main.tile[150,y].type=1;}
            for(int i=0;i<255;i++)Main.player[i].active=false;
            var p=Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};p.position=new Vector2(1750,2400-p.height);p.ResetFallDamage();p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.immune=true;p.immuneTime=1000000;for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions(path:false,mouseCenter:true,clearLine:false,radius:50));
        }
    }
}
