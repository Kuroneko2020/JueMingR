using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // C uses the existing locked-original Update loop. Every frozen trajectory
    // is captured BEFORE the following player/NPC actions; refreshed windows
    // cannot retrospectively supply a better prediction for that action.
    internal static class NativeCombatFoundationContinuousChecks
    {
        internal static void Run(object context,NpcPredictionCache cache,Action step,string output)
        {
            GateControls();
            NativeCombatLiveContextChecks.FlightWorld();foreach(var npc in Main.npc)npc.active=false;foreach(var projectile in Main.projectile)projectile.active=false;
            Main.tileSolid[TileID.Stone]=true;Main.tileRope[TileID.Rope]=true;Main.worldSurface=140;Main.dayTime=false;
            Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();
            var host=Get(context,"CombatObservation");Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};var p=Main.LocalPlayer;
            p.position=new Vector2(900,2400-p.height);p.fallStart=p.fallStart2=(int)(p.position.Y/16);p.velocity=Vector2.Zero;p.controlLeft=p.controlRight=p.controlUp=p.controlDown=p.controlJump=false;
            p.armor[3].SetDefaults(54);p.armor[4].SetDefaults(4404);p.statLife=p.statLifeMax=p.statLifeMax2=400;
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),650,2100,2,Start:16,Target:p.whoAmI);var n=Main.npc[slot];
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,marker:true,mouseCenter:true,clearLine:false,radius:25));
            if(Environment.GetEnvironmentVariable("JUEMINGR_FOUNDATION_SINGLE")=="1")
            {
                p.controlRight=true;
                void State(string label){Console.WriteLine("C SINGLE "+label+" player="+p.whoAmI+" active="+p.active+" dead="+p.dead+" film="+p.isControlledByFilm+" right="+p.controlRight+" life="+p.statLife+" pos="+p.position+" vel="+p.velocity+" npcSlot="+n.whoAmI+" npcType="+n.type+" npcStyle="+n.aiStyle+" npcActive="+n.active+" npcFriendly="+n.friendly+" npcImmortal="+n.immortal+" dontTake="+n.dontTakeDamage+" npcLife="+n.life+" npcPos="+n.position+" npcV="+n.velocity+" npcTarget="+n.target+" required="+cache.Required+" selected="+Get(Get(host,"Selection"),"HasTarget")+" runtime="+Get(Get(context,"Runtime"),"SharedRuntime")+" menu="+Main.gameMenu+" pause="+Main.gamePaused+" net="+Main.netMode);}
                State("before");NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);Console.WriteLine("C SINGLE mouse="+Get(Get(host,"Selection"),"HasMouse")+" point="+Get(Get(host,"Selection"),"RealMouse")+" focused="+Get(Get(context,"Input"),"SampleFocused")+" options="+Get(host,"Options"));step();State("after");Console.WriteLine("C SINGLE afterMouse="+Get(Get(host,"Selection"),"HasMouse"));NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();State("after-next-sample");return;
            }
            var availability=new List<FrameEvidence>();
            NpcIdentity Expected(){return new NpcIdentity((long)Get(host,"Session"),n,n.whoAmI,n.generation,n.type,n.netID);}
            // This fixture deliberately replaced Main.LocalPlayer above.
            // ItemSessionProbe's token changes; SingleFeatureRuntime advances
            // its generation exactly once on the next update. Declare that
            // known session transition BEFORE demand's first legal update.
            long startupSession=checked((long)Get(Get(Get(context,"Runtime"),"SharedRuntime"),"Generation")+1);
            var expectedWindow=new NpcIdentity(startupSession,n,n.whoAmI,n.generation,n.type,n.netID);
            var identitySwitches=new List<string>();
            void Declare(string phase)
            {var next=Expected();if(!next.Equals(expectedWindow))identitySwitches.Add(phase);expectedWindow=next;}
            FrameEvidence Observe(string phase,bool acquiring,int frame,NpcTrajectory frozen,bool exact)
            {
                var path=cache.Read(0);var selection=Get(host,"Selection");var expected=expectedWindow;bool has=(bool)Get(selection,"HasTarget");
                var evidence=new FrameEvidence{Phase=phase,Acquiring=acquiring,Frame=frame,Expected=expected,Selected=has,Selection=has?(NpcIdentity)Get(selection,"Target"):default(NpcIdentity),Published=path!=null,Publication=path?.Identity??default(NpcIdentity),Tick=(long)Main.GameUpdateCount,SampleTick=path?.SampleTick??-1,CaptureTick=path?.CaptureTick??-1,Legal=n.active && n.life>0 && !n.friendly && !n.immortal && !n.dontTakeDamage && p.active && !p.dead,Size=Math.Min(n.width,n.height),Exact=exact};
                if(frozen!=null && frame>0 && frame<frozen.Count)
                {var point=frozen[frame];double dx=point.Bounds.X-n.position.X,dy=point.Bounds.Y-n.position.Y;evidence.Error=Math.Sqrt(dx*dx+dy*dy);evidence.ErrorY=Math.Abs(dy);evidence.HasFrozen=true;evidence.PredictedVx=point.Vx;evidence.PredictedVy=point.Vy;evidence.ActualVx=n.velocity.X;evidence.ActualVy=n.velocity.Y;}
                availability.Add(evidence);return evidence;
            }
            // Availability starts with the FIRST legal demanded update, before
            // a frozen path exists. Only these declared startup observations
            // permit acquisition latency; blanks cannot invent recovery phases.
            NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();Observe("startup",true,0,null,false);
            NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();Observe("startup",true,0,null,false);
            var rows=new List<string>{"phase,frame,tick,selected,published,count,stop,assumptions,grapCount,pulley,wet,down,playerX,playerY,npcX,npcY,npcType,npcVy,npcCollideY,frozenTick,frozenHorizon,frozenX,frozenY,errorX,errorY,frozenError"};
            var summary=new List<string>{"phase,frames,selected,published,current120,complexObserved,maxFrozenError,lastPlayerX,lastPlayerY"};
            int actions=0;
            void Window(string name,int frames,bool playerRule=false,bool events=false)
            {
                // Refresh the completed observation after a scripted input or
                // legal mechanism transition, before executing another action.
                // Only the scripted SetDefaults/entry between windows can
                // establish a new expectation. In-window replacement or real
                // transformation must not re-sign its identity as correct.
                Declare(name);
                NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);NativeCombatObservationChecks.Fresh(context,host);var frozen=cache.Read(0);
                var snapshot=Observe(name,false,0,null,false);snapshot.Snapshot=true;availability[availability.Count-1]=snapshot;
                Require(frozen!=null && frozen.Count>frames && frozen.Identity.Equals(Expected()),"C a complete current-instance identity must be frozen before the action window.");
                List<PredictionPlayerMotion> playerFuture=null;
                if(playerRule)
                {
                    var source=Get(host,"Prediction");var read=source.GetType().GetMethod("ReadPlayer",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
                    var sampled=(PredictionPlayerMotion)read.Invoke(null,new object[]{p});Require(!sampled.Complex,"A declared ordinary player window must use modeled held controls.");
                    var terrain=(IPredictionTerrain)Activator.CreateInstance(Get(source,"Terrain").GetType(),true);terrain.Reset();var rolling=new RollingNpcPrediction();
                    var advance=typeof(RollingNpcPrediction).GetMethod("AdvancePlayer",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                    var environment=new PredictionEnvironment{WorldWidth=Main.maxTilesX,GravityWorldSurface=Main.worldSurface,Remix=Main.remixWorld};
                    playerFuture=new List<PredictionPlayerMotion>{sampled};
                    for(int i=1;i<=frames;i++){var args=new object[]{sampled,environment,terrain,PredictionStop.None};Require((bool)advance.Invoke(rolling,args),"A actual rolling player consumer freezes all declared actions, stop="+args[3]);sampled=(PredictionPlayerMotion)args[0];playerFuture.Add(sampled);}
                }
                int selected=0,published=0,full=0,complex=0;double maxError=0;
                for(int frame=1;frame<=frames;frame++)
                {
                    float beforeY=n.position.Y,beforeVx=n.velocity.X,beforeVy=n.velocity.Y;
                    float playerBeforeY=p.position.Y,playerBeforeVx=p.velocity.X,playerBeforeVy=p.velocity.Y;
                    NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();actions++;var path=cache.Read(0);bool has=(bool)Get(Get(host,"Selection"),"HasTarget");if(has)selected++;if(path!=null){published++;if(path.Count==121)full++;if((path.Assumptions&PredictionAssumption.CurrentPlayerObservation)!=0)complex++;}
                    var evidence=Observe(name,false,frame,frozen,name.StartsWith("fighter104-",StringComparison.Ordinal));
                    evidence.ExactY=name.StartsWith("gravity258-",StringComparison.Ordinal);availability[availability.Count-1]=evidence;
                    if(events)
                    {
                        var point=frozen[frame];var prior=frozen[frame-1];
                        Actions(ref evidence,prior.Bounds.Y,prior.Vx,prior.Vy,point.Bounds.Y,point.Vx,point.Vy,beforeY,beforeVx,beforeVy,n.position.Y,n.velocity.X,n.velocity.Y);
                        availability[availability.Count-1]=evidence;
                    }
                    if(playerRule)
                    {
                        var point=playerFuture[frame];var prior=playerFuture[frame-1];var proof=evidence;proof.Phase=name+"-player";proof.Snapshot=true;proof.HasFrozen=proof.Exact=true;proof.Size=Math.Min(p.width,p.height);
                        double dx=point.X-p.position.X,dy=point.Y-p.position.Y;proof.Error=Math.Sqrt(dx*dx+dy*dy);proof.ErrorY=Math.Abs(dy);proof.PredictedVx=point.Vx;proof.PredictedVy=point.Vy;proof.ActualVx=p.velocity.X;proof.ActualVy=p.velocity.Y;
                        Actions(ref proof,prior.Y,prior.Vx,prior.Vy,point.Y,point.Vx,point.Vy,playerBeforeY,playerBeforeVx,playerBeforeVy,p.position.Y,p.velocity.X,p.velocity.Y);
                        // Supplemental same-outlet player proof is not another
                        // world update in the NPC availability denominator.
                        availability.Add(proof);
                    }
                    double error=double.NaN,fx=double.NaN,fy=double.NaN,ex=double.NaN,ey=double.NaN;
                    if(frozen!=null && frame<frozen.Count && frozen.Identity.Token==n)
                    {var predicted=frozen[frame].Bounds;fx=predicted.X;fy=predicted.Y;ex=fx-n.position.X;ey=fy-n.position.Y;error=Math.Sqrt(ex*ex+ey*ey);maxError=Math.Max(maxError,error);}
                    rows.Add(string.Join(",",name,frame,Main.GameUpdateCount,has,path!=null,path?.Count??0,path?.Stop.ToString()??"none",(int)(path?.Assumptions??PredictionAssumption.None),p.grapCount,p.pulley,p.wet,p.controlDown,F(p.position.X),F(p.position.Y),F(n.position.X),F(n.position.Y),n.type,F(n.velocity.Y),n.collideY,frozen?.CaptureTick??-1,frozen?.Count-1??0,F(fx),F(fy),F(ex),F(ey),F(error)));
                }
                summary.Add(string.Join(",",name,frames,selected,published,full,complex,F(maxError),F(p.position.X),F(p.position.Y)));
                Console.WriteLine("C FROZEN "+summary[summary.Count-1]);
                Require(Evaluate(availability)==null,"C actual continuous acceptance: "+Evaluate(availability));
                if(name=="fighter104-near-pounce")Require(availability.Exists(f=>f.Phase==name && f.ActualJump && f.PredictedJump),"B declared near-pounce positive window must contain a real and same-phase predicted takeoff.");
                if(name=="fighter104-stepup")Require(availability.Exists(f=>f.Phase==name && f.ActualStep && f.PredictedStep),"B declared step positive window must contain a real and same-phase predicted step.");
                if(name=="fighter104-speed2")Require(!availability.Exists(f=>f.Phase==name && (f.ActualJump || f.PredictedJump)),"B declared far target without obstacle must not activate a near pounce.");
            }
            try
            {
                bool fighterOnly=Environment.GetEnvironmentVariable("JUEMINGR_FOUNDATION_FIGHTER_ONLY")=="1";
                bool liquidOnly=Environment.GetEnvironmentVariable("JUEMINGR_FOUNDATION_PLAYER_LIQUID_ONLY")=="1";
                bool npcLiquidOnly=Environment.GetEnvironmentVariable("JUEMINGR_FOUNDATION_NPC_LIQUID_ONLY")=="1";
                if(Environment.GetEnvironmentVariable("JUEMINGR_FOUNDATION_GRAVITY_ONLY")!="1" && !fighterOnly && !liquidOnly && !npcLiquidOnly)
                {
                p.controlRight=true;Window("dry-float-boots-fast-run",120);Require(p.canFloatInWater && !p.wet && p.velocity.X>3,"C original full Player.Update supplies equipped dry fast-running state.");
                p.controlRight=false;p.controlLeft=true;Window("reverse",60);
                p.controlLeft=false;Window("release-friction",30);
                p.controlJump=true;p.releaseJump=true;Window("vertical-jump-start-hold",12,true);Require(p.jump>0 && p.velocity.Y<0,"A original held jump really starts and maintains.");
                p.controlJump=false;Window("vertical-jump-release-land",30,true);Require(p.jump==0,"A original release clears jump timer.");
                var hook=Main.projectile[4];hook.SetDefaults(13);hook.whoAmI=4;hook.active=true;hook.owner=p.whoAmI;hook.ai[0]=2;hook.position=new Vector2(p.Center.X+100,2400);hook.velocity=Vector2.Zero;p.grappling[0]=4;p.grapCount=1;
                Window("grapple-pull-attached",24);Require(p.grapCount>0,"C original attached hook remains owned and active.");p.controlJump=true;p.releaseJump=true;Window("grapple-release",12);Require(p.grapCount==0,"C original jump releases grapple.");p.controlJump=false;
                for(int y=120;y<150;y++){Main.tile[60,y].active(true);Main.tile[60,y].type=TileID.Rope;}
                p.position=new Vector2(60*16+8-p.width/2,2240);p.fallStart=p.fallStart2=(int)(p.position.Y/16);p.velocity=Vector2.Zero;p.pulley=true;p.pulleyDir=2;p.controlLeft=p.controlRight=false;
                Window("rope-idle",12);Require(p.pulley,"C original rope remains attached while idle.");float ropeY=p.position.Y;p.controlUp=true;Window("rope-move",24);Require(p.pulley && p.position.Y<ropeY,"C original rope movement changes position without ordinary horizontal controls.");p.controlUp=false;p.controlJump=true;p.releaseJump=true;Window("rope-exit",12);Require(!p.pulley,"C original jump exits rope.");p.controlJump=false;
                for(int x=50;x<75;x++)for(int y=146;y<150;y++){Main.tile[x,y].active(false);Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(0);}
                p.pulley=false;p.position=new Vector2(900,2300);p.fallStart=p.fallStart2=(int)(p.position.Y/16);p.velocity=Vector2.Zero;p.controlDown=false;Window("float-enter-surface",36);Require(p.wet && p.canFloatInWater,"C original equipped player really reaches water.");
                p.controlDown=true;Window("float-down",18);p.controlDown=false;p.position=new Vector2(1300,2400-p.height);p.fallStart=p.fallStart2=(int)(p.position.Y/16);p.velocity=Vector2.Zero;Window("float-exit",18);
                }
                else{p.position=new Vector2(1300,2400-p.height);p.fallStart=p.fallStart2=(int)(p.position.Y/16);p.velocity=Vector2.Zero;}
                bool full=Environment.GetEnvironmentVariable("JUEMINGR_FOUNDATION_GRAVITY_ONLY")!="1" && !fighterOnly && !liquidOnly && !npcLiquidOnly;
                if(liquidOnly || full)
                {
                    p.armor[3].TurnToAir();p.armor[4].TurnToAir();p.canFloatInWater=false;p.controlLeft=p.controlRight=p.controlUp=p.controlDown=p.controlJump=false;
                    foreach(int liquid in new[]{0,2})
                    {
                        for(int x=50;x<75;x++)for(int y=146;y<150;y++){Main.tile[x,y].active(false);Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(liquid);}
                        p.position=new Vector2(900,2296);p.velocity=new Vector2(0,4);p.wet=p.honeyWet=p.lavaWet=p.shimmerWet=false;p.jump=0;p.releaseJump=true;
                        p.fallStart=p.fallStart2=(int)(p.position.Y/16);Window("vertical-fluid"+liquid+"-enter-next",12,true);
                        Require(p.wet && p.honeyWet==(liquid==2),"A full original player naturally enters declared fluid.");
                        p.position.X=1186;p.velocity.X=3;p.controlRight=true;Window("vertical-fluid"+liquid+"-exit-next",18,true);
                        Require(!p.wet,"A full original player naturally leaves declared fluid.");p.controlRight=false;
                    }
                    NativeCombatFoundationChecks.NativeVertical(Get(host,"Prediction"));
                }
                if(npcLiquidOnly || full)
                {
                    p.controlLeft=p.controlRight=p.controlJump=false;p.velocity=Vector2.Zero;
                    foreach(int scenario in new[]{0,1,2})
                    {
                        int type=scenario==0?541:620;bool above=scenario==2;
                        p.position=new Vector2(1300,above?2100:2400-p.height);p.velocity=Vector2.Zero;p.fallStart=p.fallStart2=(int)p.position.Y/16;
                        n.SetDefaults(type);n.whoAmI=slot;n.active=true;n.alpha=0;n.target=p.whoAmI;n.position=new Vector2(1600,2250);n.velocity=new Vector2(2,1);n.wet=n.honeyWet=true;n.wetCount=1;n.oldVelocity=n.velocity;
                        // Real Source→rolling→Cache must consume the old wet
                        // base for step1, then cleared eligibility for step2.
                        NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);
                        Require(path!=null && path.Count>2,"C actual Source captures finite liquid transition type="+type);
                        var probe=new NPC();probe.SetDefaults(type);probe.whoAmI=199;probe.target=p.whoAmI;probe.position=n.position;probe.velocity=n.velocity;probe.wet=probe.honeyWet=true;probe.wetCount=1;
                        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static;
                        var gravity=typeof(NPC).GetMethod("UpdateNPC_UpdateGravity",flags);var collision=typeof(NPC).GetMethod("UpdateCollision",flags);var g=typeof(NPC).GetField("gravity",flags);
                        gravity.Invoke(probe,new object[]{0f});float first=(float)g.GetValue(null);collision.Invoke(probe,null);float vy1=probe.velocity.Y+first;
                        gravity.Invoke(probe,new object[]{0f});float second=(float)g.GetValue(null);
                        Require(Math.Abs(path[1].Vy-vy1)<.0001,"C actual default Source first old-fluid→collision impulse type="+type+" expected="+vy1+" actual="+path[1].Vy);
                        Require(type!=541 || Math.Abs(path[2].Vy-path[1].Vy-second)<.0001,"C541 actual Source next gravity uses cleared wet state expected="+second+" delta="+(path[2].Vy-path[1].Vy));
                        Console.WriteLine("C LIQUID SOURCE type="+type+" above="+above+" oldGravity="+first+" nextGravity="+second+" frozenVy1="+path[1].Vy+" nativeImpulseVy1="+vy1);
                        Require(!above || path[1].Vy<0,"C620 true player above must freeze the exit impulse before action.");
                        Window("npc-fluid"+type+(above?"-above":"-below")+"-exit-next",4,false,type==620);
                        Require(!n.wet && !n.honeyWet,"C actual UpdateNPC clears declared old fluid qualification type="+type);
                    }
                }
                if(!fighterOnly && !liquidOnly && !npcLiquidOnly)
                {
                p.position=new Vector2(1300,2400-p.height);p.velocity=Vector2.Zero;p.fallStart=p.fallStart2=(int)p.position.Y/16;
                n.SetDefaults(258);n.whoAmI=slot;n.active=true;n.target=p.whoAmI;n.position=new Vector2(1600,2310);n.velocity=new Vector2(-1,9);n.ai[3]=1;
                Declare("gravity258-first-fall");
                NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);NativeCombatObservationChecks.Fresh(context,host);var falling=cache.Read(0);
                Require(falling!=null && falling.Count==121 && Math.Abs(falling[1].Vy-3.1f)<.0001f,"C actual Source frozen258 first step preserves pre-AI public clip and terminal gravity.");
                NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();actions++;Observe("gravity258-first-fall",false,1,falling,true);Require(Math.Abs(n.velocity.Y-3.1f)<.0001f,"C original UpdateNPC confirms the same clipped first fall step.");
                Console.WriteLine("C GRAVITY FIRST sourceCapture="+falling.CaptureTick+" frozenVy1="+falling[1].Vy+" actualVy1="+n.velocity.Y+" phase="+n.ai[2]);
                Window("gravity258-fall-land",48,false,true);Require(n.collideY && n.velocity.Y==0,"C original special-gravity actor actually reaches its ground phase.");
                n.position.Y=2320;n.velocity.Y=-2;n.oldVelocity=n.velocity;Window("gravity258-rise-fall",48,false,true);
                }
                if(fighterOnly || full)
                {
                    n.SetDefaults(104);n.whoAmI=slot;n.active=true;n.target=p.whoAmI;n.position=new Vector2(700,2400-n.height);n.oldPosition=n.position-new Vector2(2,0);n.velocity=new Vector2(2,0);n.direction=n.spriteDirection=1;n.ai[3]=0;
                    Window("fighter104-speed2",120);Require(n.velocity.X==2,"C original ordinary fighter retains its legal speed above old invented1.5.");
                    p.position.X=400;Window("fighter104-reverse",30);Require(n.direction==-1 && n.velocity.X<0,"C original fighter reverses under the new actual numbered-player position.");
                    n.velocity=new Vector2(-7,0);n.oldVelocity=n.velocity;Window("fighter104-overspeed-brake",30);Require(n.velocity.X==-2,"C original true ground overspeed brake converges to the real speed2 threshold.");
                    n.position=new Vector2(p.position.X-70,2400-n.height);n.oldPosition=n.position-new Vector2(2,0);n.velocity=new Vector2(2,0);n.oldVelocity=n.velocity;n.direction=n.spriteDirection=1;n.ai[3]=0;
                    Window("fighter104-near-pounce",24,false,true);
                    foreach(int scene in new[]{0,1,2})
                    {
                        for(int x=40;x<60;x++)for(int y=144;y<150;y++){Main.tile[x,y].active(false);Main.tile[x,y].liquid=0;}
                        p.position=new Vector2(1100,2400-p.height);p.velocity=Vector2.Zero;p.controlLeft=p.controlRight=p.controlJump=false;
                        if(scene!=2){n.position=new Vector2(700,2400-n.height);n.oldPosition=n.position-new Vector2(2,0);n.velocity=new Vector2(2,0);n.oldVelocity=n.velocity;n.direction=n.spriteDirection=1;n.ai[3]=0;n.collideX=n.collideY=false;}
                        int column=(int)((n.position.X+2+n.width/2+(n.width/2+1))/16);
                        if(scene==0){Main.tile[column,149].active(true);Main.tile[column,149].type=TileID.Stone;}
                        if(scene==1)for(int y=144;y<150;y++){Main.tile[column,y].active(true);Main.tile[column,y].type=TileID.Stone;}
                        // At this declared far target the near-pounce gate is
                        // false; the roof case remains blocked, then the next
                        // predeclared window removes it before any new action.
                        Window(scene==0?"fighter104-stepup":scene==1?"fighter104-roof-blocked":"fighter104-obstruction-recovered",12,false,true);
                        Require(scene!=0 || n.position.Y<2400-n.height,"B actual fighter crosses the declared legal step.");
                        Require(scene!=1 || n.position.X<column*16,"B actual fighter remains outside the blocked wall/roof.");
                        Require(scene!=2 || n.position.X>700,"B removing the real obstruction resumes motion from its blocked state.");
                    }
                }
                NativeCombatObservationChecks.Save(host,new ObservationOptions());step();Require(cache.Required==0 && cache.Read(0)==null,"C OFF retires publication after actual native actions.");
                Console.WriteLine("PASS C actual full Player.Update / NPC.UpdateNPC / shared Host frozen-action denominator="+actions+". Errors are conditional model limits, not an exact 120-tick physics claim.");
            }
            finally
            {
                File.WriteAllLines(Path.Combine(output,"foundation-continuous.csv"),rows);File.WriteAllLines(Path.Combine(output,"foundation-continuous-summary.csv"),summary);
                var coverage=new List<string>{"phase,snapshot,legal,selected,published,acquiring,frame,tick,sampleTick,captureTick,expectedSession,expectedSlot,expectedGeneration,expectedType,expectedNetId,frozenError,errorY,predictedVx,predictedVy,actualVx,actualVy,checkActions,predictedJump,actualJump,predictedTurn,actualTurn,predictedStep,actualStep"};
                foreach(var item in availability)coverage.Add(string.Join(",",item.Phase,item.Snapshot,item.Legal,item.Selected,item.Published,item.Acquiring,item.Frame,item.Tick,item.SampleTick,item.CaptureTick,item.Expected.Session,item.Expected.Slot,item.Expected.Generation,item.Expected.Type,item.Expected.NetId,F(item.Error),F(item.ErrorY),F(item.PredictedVx),F(item.PredictedVy),F(item.ActualVx),F(item.ActualVy),item.CheckActions,item.PredictedJump,item.ActualJump,item.PredictedTurn,item.ActualTurn,item.PredictedStep,item.ActualStep));
                File.WriteAllLines(Path.Combine(output,"foundation-availability.csv"),coverage);
                int legal=0,selected=0,published=0,blank=0,longest=0,first=-1;
                foreach(var item in availability){if(item.Snapshot)continue;if(item.Legal)legal++;if(item.Selected)selected++;if(item.Published)published++;bool usable=item.Selected && item.Published && item.SampleTick==item.Tick;blank=usable?0:blank+1;longest=Math.Max(longest,blank);if(first<0 && usable)first=legal;}
                string totals="legal="+legal+",selected="+selected+",published="+published+",firstUsableLegalUpdate="+first+",longestBlank="+longest+",declaredIdentitySwitches="+identitySwitches.Count+",switchWindows="+string.Join("|",identitySwitches)+",switchLegalWait=0,declaredRecovery=0";
                File.WriteAllText(Path.Combine(output,"foundation-availability-summary.txt"),totals);Console.WriteLine("C AVAILABILITY "+totals);
            }
        }
        private struct FrameEvidence
        {
            internal string Phase;
            internal NpcIdentity Expected,Selection,Publication;
            internal bool Legal,Selected,Published,Acquiring,HasFrozen,Exact,ExactY,Snapshot,CheckActions,PredictedJump,ActualJump,PredictedTurn,ActualTurn,PredictedStep,ActualStep;
            internal long Tick,SampleTick,CaptureTick;
            internal int Frame,Size;
            internal double Error,ErrorY;
            internal float PredictedVx,PredictedVy,ActualVx,ActualVy;
        }
        // One outlet for real observations and isolated fault controls. These
        // limits were proposed and accepted BEFORE inspecting candidate runs.
        private static string Evaluate(IList<FrameEvidence> frames)
        {
            int legal=0,selected=0,published=0,blank=0,longest=0,wait=0;bool acquired=false;
            foreach(var f in frames)
            {
                if(!f.Legal)return f.Phase+": fixture entered an undeclared illegal/terminal interval";
                if(!f.Snapshot)legal++;
                if(f.Selected){selected++;if(!f.Selection.Equals(f.Expected))return f.Phase+": wrong selected full identity";}
                if(f.Published)
                {
                    if(!f.Snapshot)published++;
                    if(!f.Publication.Equals(f.Expected))return f.Phase+": wrong publication full identity";
                    // The existing native Step increments the tick before its
                    // completed Host.Update; Fresh uses that same current tick.
                    // Capture may retain rolling history, Sample must be NOW.
                    if(f.SampleTick!=f.Tick || f.CaptureTick>f.SampleTick)return f.Phase+": stale publication tick";
                }
                if(f.Snapshot && (!f.Selected || !f.Published))return f.Phase+": missing pre-action snapshot";
                bool usable=f.Selected && f.Published;
                if(!f.Snapshot)
                {
                    blank=usable?0:blank+1;longest=Math.Max(longest,blank);
                    if(f.Acquiring && !acquired){if(!usable && ++wait>2)return f.Phase+": acquisition exceeds two legal updates";if(usable)acquired=true;}
                    else if(!usable)return f.Phase+": blank in stable legal interval";
                }
                if(f.CheckActions && (f.PredictedJump!=f.ActualJump || f.PredictedTurn!=f.ActualTurn || f.PredictedStep!=f.ActualStep))return f.Phase+": deterministic jump/turn/step event differs at "+f.Frame+" expected="+f.ActualJump+"/"+f.ActualTurn+"/"+f.ActualStep+" predicted="+f.PredictedJump+"/"+f.PredictedTurn+"/"+f.PredictedStep;
                if(f.HasFrozen)
                {
                    if(double.IsNaN(f.Error) || double.IsInfinity(f.Error))return f.Phase+": non-finite frozen error";
                    double limit=f.Exact?.01:f.Frame<=15?f.Size*.5:f.Frame<=30?f.Size:double.PositiveInfinity;
                    if(f.Error>limit || f.ExactY && f.ErrorY>.01)return f.Phase+": frozen quality exceeds prior acceptance window at "+f.Frame+" error="+f.Error+" limit="+limit+" errorY="+f.ErrorY;
                    if(f.Exact && (Math.Abs(f.PredictedVx-f.ActualVx)>.01 || Math.Abs(f.PredictedVy-f.ActualVy)>.01))return f.Phase+": deterministic motion/action differs";
                }
            }
            if(legal==0 || published==0)return "no usable legal denominator";
            return null;
        }
        private static void GateControls()
        {
            object token=new object();var expected=new NpcIdentity(7,token,2,9,104,104);
            var good=new List<FrameEvidence>();
            for(int i=1;i<=30;i++)good.Add(new FrameEvidence{Phase="gate-control",Legal=true,Selected=true,Published=true,Expected=expected,Selection=expected,Publication=expected,HasFrozen=true,Frame=i,Size=40,Error=i<=15?19:39});
            Require(Evaluate(good)==null,"T01 bounded useful approximation must pass the real acceptance outlet.");
            var empty=new List<FrameEvidence>(good);for(int i=1;i<empty.Count;i++){var f=empty[i];f.Selected=f.Published=false;empty[i]=f;}
            Require(Evaluate(empty)!=null,"T01 almost-all-blank must fail the same outlet.");
            foreach(var wrong in new[]{new NpcIdentity(7,new object(),2,9,104,104),new NpcIdentity(7,token,2,10,104,104)})
            {var identity=new List<FrameEvidence>(good);var f=identity[5];f.Selection=f.Publication=wrong;identity[5]=f;Require(Evaluate(identity)!=null,"T01 wrong instance/generation must fail the same outlet.");}
            var stale=new List<FrameEvidence>(good);var old=stale[5];old.Tick=8;old.SampleTick=7;old.CaptureTick=7;stale[5]=old;Require(Evaluate(stale)!=null,"T01 same-identity old tick must fail the same outlet.");
            var direction=new List<FrameEvidence>(good);var d=direction[0];d.Error=0;d.CheckActions=true;d.ActualTurn=true;direction[0]=d;Require(Evaluate(direction)!=null,"T01 wrong action direction fails even at zero positional error in a conditional trend.");
            var jump=new List<FrameEvidence>(good);d=jump[0];d.Error=0;d.CheckActions=true;d.ActualJump=true;jump[0]=d;Require(Evaluate(jump)!=null,"T01 missed jump fails before position error reaches a body width in a conditional trend.");
            Console.WriteLine("PASS T01 same-outlet blank/full-identity/action negative controls and bounded approximation positive control.");
        }
        private static string F(double value){return value.ToString("R",CultureInfo.InvariantCulture);}
        private static void Actions(ref FrameEvidence f,float py,float pxv,float pyv,float y,float xv,float yv,float ay,float axv,float ayv,float actualY,float actualXv,float actualYv)
        {
            // Upward onset covers grounded takeoff and the declared620
            // falling→rising liquid-exit impulse without a size-error escape.
            f.CheckActions=true;f.PredictedJump=pyv>=0 && yv<0;f.ActualJump=ayv>=0 && actualYv<0;
            f.PredictedTurn=pxv!=0 && xv!=0 && Math.Sign(pxv)!=Math.Sign(xv);f.ActualTurn=axv!=0 && actualXv!=0 && Math.Sign(axv)!=Math.Sign(actualXv);
            f.PredictedStep=pyv==0 && yv==0 && y<py-.01f;f.ActualStep=ayv==0 && actualYv==0 && actualY<ay-.01f;
        }
    }
}
