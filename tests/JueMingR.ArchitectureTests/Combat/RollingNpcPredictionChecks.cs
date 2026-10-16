using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;

namespace JueMingR.ArchitectureTests
{
    internal static class RollingNpcPredictionChecks
    {
        internal static void Run()
        {
            SampleReentry();
            FiniteFlight();
            ImmutableGeometry();
            ArmoredDayPursuit();
            ContinuationPlayerPremise();
            SlimeContinuation();
            IncrementalPrefix();
            BudgetPrefix();
            SegmentedObservationHistory();
            MultiSlotPrefix();
            TowerFrameEndPrefix();
            MoonLeechFrameEndPrefix();
            SourceTimelinePrefix();
            PresentGravityTimelinePrefix();
            var terrain=new EmptyTerrain();var model=new RollingNpcPrediction();
            var state=new NpcMotionState{Identity=new NpcIdentity(1,new object(),4,2,999,999),X=100,Y=100,Vx=1,Vy=-1,Width=20,Height=20,Life=100,LifeMax=100,TimeLeft=750,Active=true,CanReceive=true,NoGravity=true,NoTileCollide=true,Health=new NpcHealthState{RealLife=-1}};
            var player=new PredictionPlayerMotion{X=150,Y=150,Width=20,Height=40,GravityDirection=1,Gravity=.4f,MaxFall=10,MaxSpeed=3,Acceleration=.08f,Slowdown=.2f};
            var env=new PredictionEnvironment{PlayerX=160,PlayerY=170,PlayerWidth=20,PlayerHeight=40,WorldWidth=4200,WorldHeight=1200,WorldSurface=400};
            var source=new[]{state};
            var brokenPlayer=player;brokenPlayer.MaxFall=float.NaN;
            var playerDependent=state;playerDependent.Identity=new NpcIdentity(1,new object(),4,2,2,2);playerDependent.Style=2;
            Require(model.Prepare(new[]{playerDependent},1,0,99,120,1,env,brokenPlayer,terrain)==null,"NaN necessary player fall limit is rejected before any forecast.");
            brokenPlayer=player;brokenPlayer.JumpSpeed=float.NaN;
            Require(model.Prepare(new[]{playerDependent},1,0,99,120,1,env,brokenPlayer,terrain)==null,"NaN necessary player jump speed is rejected before any forecast.");
            var first=model.Prepare(source,1,0,100,120,1,env,player,terrain);
            Require(first!=null && first.Count==121 && first.Strategy==PredictionStrategy.RollingConditional,"Unknown airborne mechanisms can provide a bounded trend beyond twelve ticks.");
            var second=model.Prepare(source,1,0,101,120,1,env,player,terrain);
            Require(second.CaptureTick==101 && second.Version>first.Version && !ReferenceEquals(first,second) && terrain.Resets==2,"Identical observed conditions still produce a fresh calculation.");
            bool refused=false;try{first.Republish(102,99);}catch(InvalidOperationException){refused=true;}Require(refused,"A conditional result cannot acquire a new capture time by republishing.");
            source[0].Vx=-4;source[0].Life=70;source[0].JustHit=true;
            var hit=model.Prepare(source,1,0,102,120,1,env,player,terrain);
            Require(hit!=null && hit[0].Vx==-4 && hit[1].Bounds.X<100 && first[0].Vx==1,"Nonfatal hits immediately use observed recoil while retained prior results stay immutable.");
            source[0].Life=0;Require(model.Prepare(source,1,0,103,120,1,env,player,terrain)==null,"Actual death cannot publish a living future.");
            source[0]=state;terrain.Limit=8;
            var unknown=model.Prepare(source,1,0,104,120,2,env,player,terrain);
            Require(unknown.Count<121 && unknown.Stop==PredictionStop.TerrainUnavailable,"Unknown geometry ends at the last computed prefix without padding.");
            source[0].Vx=float.NaN;Require(model.Prepare(source,1,0,105,120,2,env,player,terrain)==null,"Nonfinite source data is rejected before publication.");
            foreach(int liquid in new[]{0,1,2,3})
            {
                source[0]=state;
                if(liquid==0)source[0].WaterSpeed=float.NaN;else if(liquid==1)source[0].HoneySpeed=float.PositiveInfinity;else if(liquid==2)source[0].LavaSpeed=float.NaN;else source[0].ShimmerSpeed=float.NaN;
                Require(model.Prepare(source,1,0,106,120,2,env,player,terrain)==null,"All participating liquid coefficients must be finite: "+liquid);
            }
            source[0]=state;terrain.Limit=int.MaxValue;terrain.BadPlayer=true;
            var badFuture=model.Prepare(new[]{playerDependent},1,0,107,120,2,env,player,terrain);
            Require(badFuture.Count==1 && badFuture.Stop==PredictionStop.InvalidState,"A nonfinite advanced player cannot supply any future point.");
            Require(model.FailureLayer==PredictionFailureLayer.PlayerPremise,"First failed player future keeps its cause layer.");
            var repeatedFailure=model.Prepare(new[]{playerDependent},1,0,107,120,2,env,player,terrain);
            Require(ReferenceEquals(badFuture,repeatedFailure) && model.FailureLayer==PredictionFailureLayer.PlayerPremise,"Same sample failure reuse preserves its player premise layer.");
            terrain.BadPlayer=false;source[0]=state;
            source[0].Identity=new NpcIdentity(1,new object(),4,2,153,153);source[0].Style=39;
            source[0].NoGravity=false;source[0].Vx=source[0].OldVx=.2f;source[0].Vy=0;source[0].Direction=1;
            env.PlayerY=50;player.Y=30;
            var cliff=model.Prepare(source,1,0,108,1,2,env,player,terrain);
            Require(cliff.Count==2 && cliff[1].Vx<0,"Walking tortoise reverses at an unsupported forward cliff.");
            terrain.CliffSupport=true;
            var supported=model.Prepare(source,1,0,109,1,2,env,player,terrain);
            Require(supported.Count==2 && supported[1].Vx>0,"Forward support preserves the tortoise walking direction.");
            terrain.UnknownCliff=true;
            var missing=model.Prepare(source,1,0,110,1,2,env,player,terrain);
            Require(missing.Count==1 && missing.Stop==PredictionStop.TerrainUnavailable,"Unknown forward support cannot be treated as an empty cliff.");
        }
        private static void FiniteFlight()
        {
            var n=new NpcMotionState{Identity=new NpcIdentity(1,new object(),4,2,261,261),Style=50,X=100,Y=100,Vx=-4,Width=20,Height=20,Life=100,LifeMax=100,TimeLeft=750,Active=true,CanReceive=true,NoGravity=true,NoTileCollide=true,Health=new NpcHealthState{RealLife=-1}};
            var e=new PredictionEnvironment{PlayerX=300,PlayerY=200,PlayerWidth=20,PlayerHeight=40,WorldWidth=4200,WorldHeight=1200,WorldSurface=400};
            var terrain=new EmptyTerrain();PredictionStop stop;var group=new[]{n};
            Require(NpcMotion.Step(ref n,group,1,e,terrain,1,true,out stop) && Math.Abs(n.Vx+3.82f)<.0001f && Math.Abs(n.Vy-.02f)<.0001f && !n.NoTileCollide,"AI50 first sample uses pursuit damping, acceleration and 261 collision policy.");
        }
        private static void SampleReentry()
        {
            var terrain=new EmptyTerrain();var model=new RollingNpcPrediction();
            var n=new NpcMotionState{Identity=new NpcIdentity(1,new object(),4,2,999,999),X=100,Y=100,Vx=4,Width=20,Height=20,Life=100,LifeMax=100,TimeLeft=750,Active=true,CanReceive=true,NoGravity=true,NoTileCollide=true,Health=new NpcHealthState{RealLife=-1}};
            var p=new PredictionPlayerMotion{Width=20,Height=40,GravityDirection=1};
            var e=new PredictionEnvironment{WorldWidth=4200,WorldHeight=1200,WorldSurface=400};
            model.Prepare(new[]{n},1,0,100,120,1,e,p,terrain);
            n.X+=4;n.Vx=3.9992f;n.Vy=.079995f;
            var first=model.Prepare(new[]{n},1,0,101,30,1,e,p,terrain);int resets=terrain.Resets;
            var repeat=model.Prepare(new[]{n},1,0,101,30,1,e,p,terrain);
            Require(repeat.Version==first.Version && repeat[30].Bounds.X==first[30].Bounds.X && repeat[30].Bounds.Y==first[30].Bounds.Y && terrain.Resets==resets,"The same real sample keeps its nonzero trend and does not calculate again.");
            var grown=model.Prepare(new[]{n},1,0,101,120,1,e,p,terrain);
            Require(grown[30].Bounds.X==first[30].Bounds.X && grown[30].Bounds.Y==first[30].Bounds.Y && grown.Count==121,"Same-tick demand growth retains the confirmed trend premise.");
            Require(Math.Abs(grown[1].Vx-n.Vx)>.000001f && Math.Abs(grown[30].Vx-n.Vx)<.000001f && Math.Abs(grown[120].Vy-n.Vy)<.000001f,"Recent observed turn remains visible but does not permanently compound far velocity.");
            n.X+=20;n.Vx=-2;n.JustHit=true;
            var corrected=model.Prepare(new[]{n},1,0,101,120,1,e,p,terrain);
            Require(corrected[0].Bounds.X==n.X && corrected[1].Vx==-2 && corrected.Version>grown.Version,"Same-tick correction is a new result, not a second motion observation.");
            n.JustHit=false;n.X-=2;
            var next=model.Prepare(new[]{n},1,0,102,120,1,e,p,terrain);
            Require(next.CaptureTick==102 && next.Version>corrected.Version,"A new world step advances the sample clock.");
            n.Active=false;Require(model.Prepare(new[]{n},1,0,102,120,1,e,p,terrain)==null,"Same-tick death immediately retires the publication.");
        }
        private sealed class EmptyTerrain : IPredictionTerrain
        {
            internal int Resets,Moves,TotalMoves,Limit=int.MaxValue,BudgetThrowAt,BudgetTileAfterMove,TileCalls;
            internal int BudgetNpcSlot=-1,BudgetNpcAction;private int npcActions;
            internal bool Changed;
            internal bool BadPlayer,CliffSupport,UnknownCliff,PeriodicPlayer;private int playerMoves;
            public bool Unchanged=>!Changed;
            public void Reset(){Resets++;Moves=0;playerMoves=0;npcActions=0;}
            public bool Move(ref NpcMotionState n,PredictionEnvironment e,out PredictionStop stop)
            {stop=PredictionStop.None;TotalMoves++;if(++Moves>Limit){stop=PredictionStop.TerrainUnavailable;return false;}n.X+=n.Vx;n.Y+=n.Vy;if(BudgetThrowAt>0 && Moves==BudgetThrowAt || BudgetNpcAction>0 && !n.Friendly && n.Identity.Slot==BudgetNpcSlot && ++npcActions==BudgetNpcAction){n.X+=10000;throw new CombatPredictionBudgetExceededException();}if(BadPlayer && n.Friendly)n.Y=float.NaN;if(PeriodicPlayer && n.Friendly)n.X+=(++playerMoves%7==0?-6:1);return true;}
            public bool Solid(MotionRect area,out bool solid,out PredictionStop stop){solid=false;stop=PredictionStop.None;return true;}
            public bool CanHit(MotionRect a,MotionRect b,out bool clear,out PredictionStop stop){clear=true;stop=PredictionStop.None;return true;}
            public bool Tile(int x,int y,out PredictionTile tile,out PredictionStop stop)
            {TileCalls++;if(BudgetTileAfterMove>0 && Moves>=BudgetTileAfterMove)throw new CombatPredictionBudgetExceededException();tile=default(PredictionTile);stop=PredictionStop.None;if(x==9 && y==10){if(UnknownCliff){stop=PredictionStop.TerrainUnavailable;return false;}tile.Active=tile.Solid=CliffSupport;}return true;}
        }
        private static void SamePrefix(NpcTrajectory actual,NpcTrajectory expected,int last,string reason)
        {
            Require(actual!=null && expected!=null && actual.Count>last && expected.Count>last,reason+" has a real complete prefix.");
            Require((actual.GravitySources==null)==(expected.GravitySources==null) && (actual.WallSources==null)==(expected.WallSources==null),reason+" retains the same source families.");
            if(actual.GravitySources!=null)Require(actual.GravitySources.Count==actual.Count && expected.GravitySources.Count==expected.Count && actual.GravitySources.PlayerIndex==expected.GravitySources.PlayerIndex && ReferenceEquals(actual.GravitySources.PlayerToken,expected.GravitySources.PlayerToken) && actual.GravitySources.FirstPlayerStepCompleted==expected.GravitySources.FirstPlayerStepCompleted,reason+" gravity source owner/phase.");
            if(actual.WallSources!=null)Require(actual.WallSources.Count==actual.Count && expected.WallSources.Count==expected.Count && actual.WallSources.PlayerIndex==expected.WallSources.PlayerIndex && ReferenceEquals(actual.WallSources.PlayerToken,expected.WallSources.PlayerToken),reason+" wall source owner.");
            for(int i=0;i<=last;i++)
            {
                Require(actual[i].Bounds.Equals(expected[i].Bounds) && actual[i].ReceiveBounds.Equals(expected[i].ReceiveBounds) && actual[i].ProjectileReceiveBounds.Equals(expected[i].ProjectileReceiveBounds) && actual[i].Vx==expected[i].Vx && actual[i].Vy==expected[i].Vy && actual[i].Phase==expected[i].Phase && actual[i].CanReceive==expected[i].CanReceive,reason+" point "+i);
                if(actual.GravitySources!=null)Require(actual.GravitySources[i].Equals(expected.GravitySources[i]),reason+" gravity source "+i);
                if(actual.WallSources!=null)Require(actual.WallSources[i].Equals(expected.WallSources[i]),reason+" wall source "+i);
            }
        }
        private static NpcMotionState PrefixState()
        {
            var n=BasicNpcMotionChecks.State(999,0);n.X=n.Y=100;n.Vx=1;n.Vy=-1;n.NoGravity=n.NoTileCollide=true;return n;
        }
        private static void IncrementalPrefix()
        {
            var n=PrefixState();var e=new PredictionEnvironment{WorldWidth=4200,WorldHeight=1200,WorldSurface=400};
            var p=new PredictionPlayerMotion{Width=20,Height=40,GravityDirection=1};var terrain=new EmptyTerrain();var model=new RollingNpcPrediction();
            var twenty=model.Prepare(new[]{n},1,0,100,20,1,e,p,terrain,null,false,120);
            Require(twenty.Count==21 && terrain.TotalMoves==20 && twenty.CalculationToken!=null,"A planned long forecast initially publishes only its twenty complete actions.");
            var forty=model.Prepare(new[]{n},1,0,100,40,1,e,p,terrain,null,false,120);
            Require(forty.Count==41 && terrain.TotalMoves==40 && terrain.Resets==1 && ReferenceEquals(twenty.CalculationToken,forty.CalculationToken),"Same-sample extension computes only twenty new actions and keeps the condition generation.");
            var repeat=model.Prepare(new[]{n},1,0,100,40,1,e,p,terrain,null,false,120);
            Require(ReferenceEquals(repeat,forty) && terrain.TotalMoves==40,"Repeating a complete same-sample prefix performs no simulation.");
            var full=model.Prepare(new[]{n},1,0,100,120,1,e,p,terrain,null,false,120);
            var oracle=new RollingNpcPrediction().Prepare(new[]{n},1,0,100,120,1,e,p,new EmptyTerrain());
            Require(full.Count==121 && terrain.TotalMoves==120 && terrain.Resets==1,"Three segments total exactly120 actions without replaying prior work.");
            SamePrefix(full,oracle,120,"Segmented work equals the existing complete calculation");SamePrefix(twenty,oracle,20,"Previously published twenty-step storage remains immutable");SamePrefix(forty,oracle,40,"Previously published forty-step storage remains immutable");
            var changedPhase=model.Prepare(new[]{n},1,0,100,20,1,e,p,terrain,null,true,120);
            Require(!ReferenceEquals(full.CalculationToken,changedPhase.CalculationToken) && terrain.Resets==2,"BeforeNpc and CompletedWorld cannot share one conditional calculation.");
            var changedPlan=model.Prepare(new[]{n},1,0,100,20,1,e,p,terrain,null,true,40);
            Require(!ReferenceEquals(changedPhase.CalculationToken,changedPlan.CalculationToken),"Changing the planned premise range starts a new generation.");
            terrain.Changed=true;int resets=terrain.Resets;
            var changedCells=model.Prepare(new[]{n},1,0,100,20,1,e,p,terrain,null,true,40);
            Require(terrain.Resets==resets+1 && !ReferenceEquals(changedPlan.CalculationToken,changedCells.CalculationToken),"A relevant terrain change retires the mutable tail.");
            terrain.Changed=false;n.Identity=new NpcIdentity(1,new object(),1,2,999,999);
            var replaced=model.Prepare(new[]{n},1,0,100,20,1,e,p,terrain,null,true,40);
            Require(!ReferenceEquals(changedCells.CalculationToken,replaced.CalculationToken) && replaced.Identity.Equals(n.Identity),"Slot reuse never extends the former target's calculation.");
        }
        private static void BudgetPrefix()
        {
            var n=PrefixState();var e=new PredictionEnvironment{WorldWidth=4200,WorldHeight=1200,WorldSurface=400};var p=new PredictionPlayerMotion{Width=20,Height=40,GravityDirection=1};
            var terrain=new EmptyTerrain{BudgetThrowAt=6};var model=new RollingNpcPrediction();
            var paused=model.Prepare(new[]{n},1,0,10,20,1,e,p,terrain,null,false,120);
            var oracle=new RollingNpcPrediction().Prepare(new[]{n},1,0,10,120,1,e,p,new EmptyTerrain());
            Require(paused!=null && paused.BudgetPaused && paused.Stop==PredictionStop.None && paused.Count==6,"A half-step budget exit publishes only the preceding five complete actions.");
            SamePrefix(paused,oracle,5,"Dirty half-step state never enters the publication");
            terrain.BudgetThrowAt=0;var recovered=model.Prepare(new[]{n},1,0,10,40,1,e,p,terrain,null,false,120);
            Require(terrain.Resets==2,"Hard interruption discards scratch before any later extension.");SamePrefix(recovered,oracle,40,"Recovery cannot resume from the partially mutated actor");
            var first=new RollingNpcPrediction().Prepare(new[]{n},1,0,10,20,1,e,p,new EmptyTerrain{BudgetThrowAt=1},null,false,120);
            Require(first==null,"Without a complete future action a budget exit does not invent a trajectory.");
            var work=new CombatPredictionWork(1000);
            using(work.Enter(55))
            {
                Require(!CombatPredictionWork.TryContinue(double.Epsilon) && !work.Exceeded && CombatPredictionWork.TryContinue(),"Optional soft allowance can pause without exhausting the urgent total allowance.");
                using(CombatPredictionWork.Limit(1000))
                {
                    using(CombatPredictionWork.Limit(double.Epsilon))
                    {
                        Require(!CombatPredictionWork.TryContinue() && !work.Exceeded,"A scoped candidate ceiling reaches the existing work owner without locking urgent work.");
                        using(CombatPredictionWork.Limit(1000))Require(!CombatPredictionWork.TryContinue(),"Nested limits cannot widen the active candidate ceiling.");
                        bool stopped=false;try{CombatPredictionWork.Check();}catch(CombatPredictionBudgetExceededException){stopped=true;}
                        Require(stopped && !work.Exceeded,"Inner solvers can report unresolved at a soft ceiling without marking the total owner exceeded.");
                        stopped=false;try{CombatPredictionWork.CheckGeometry();}catch(CombatPredictionBudgetExceededException){stopped=true;}
                        Require(stopped && !work.Exceeded,"The existing geometry check interval also sees the scoped ceiling.");
                    }
                    Require(CombatPredictionWork.TryContinue() && !work.Exceeded,"Disposing the inner candidate scope restores the outer allowance.");
                }
                Require(CombatPredictionWork.TryContinue(),"Leaving candidate exploration returns the unused urgent allowance.");
            }
            double used=work.UsedMilliseconds;
            using(work.Enter(55))Require(work.UsedMilliseconds>=used,"A second phase on one GUC keeps prior charged work.");
            using(work.Enter(56))Require(!work.Exceeded,"A new real update opens a fresh total allowance.");
        }
        private static void MultiSlotPrefix()
        {
            var owner=PrefixState();owner.Identity=new NpcIdentity(1,new object(),4,1,999,999);owner.Y=800;
            var child=PrefixState();child.Identity=new NpcIdentity(1,new object(),9,1,999,999);child.Y=400;child.Vx=child.Vy=0;child.PositionRelation=3;child.PositionOwner=owner.Identity;
            var group=new[]{owner,child};var e=new PredictionEnvironment{WorldWidth=4200,WorldHeight=1200,WorldSurface=400};var p=new PredictionPlayerMotion{Width=20,Height=40,GravityDirection=1};
            var terrain=new EmptyTerrain();var model=new RollingNpcPrediction();
            var shortResult=model.Prepare(group,2,1,10,20,1,e,p,terrain,null,false,120);
            NpcTrajectory grown;var work=new CombatPredictionWork(1000);
            using(work.Enter(10))
            {
                NpcTrajectory paused;using(CombatPredictionWork.Limit(double.Epsilon))paused=model.Prepare(group,2,1,10,40,1,e,p,terrain,null,false,120);
                Require(paused.BudgetPaused && paused.Count==21 && terrain.TotalMoves==40 && !work.Exceeded && ReferenceEquals(shortResult.CalculationToken,paused.CalculationToken),"A soft candidate ceiling pauses at the last complete multi-slot frame without discarding its scratch.");
                grown=model.Prepare(group,2,1,10,40,1,e,p,terrain,null,false,120);
            }
            Require(terrain.TotalMoves==80 && terrain.Resets==1 && ReferenceEquals(shortResult.CalculationToken,grown.CalculationToken),"Two native slots extend twenty complete frames with forty additional actor actions.");
            var complete=model.Prepare(group,2,1,10,120,1,e,p,terrain,null,false,120);
            var oracle=new RollingNpcPrediction().Prepare(group,2,1,10,120,1,e,p,new EmptyTerrain());
            SamePrefix(complete,oracle,120,"A parent-relative multi-slot extension keeps native actor ordering");SamePrefix(shortResult,oracle,20,"Published multi-slot storage stays immutable");
            Require(complete[1].Bounds.X==owner.X+owner.Vx && complete[1].Bounds.Y==owner.Y+owner.Vy-400,"The later slot reads its owner's already completed movement, not its frame-start position.");
            terrain=new EmptyTerrain{BudgetThrowAt=12};model=new RollingNpcPrediction();
            var interrupted=model.Prepare(group,2,1,10,20,1,e,p,terrain,null,false,120);
            Require(interrupted.BudgetPaused && interrupted.Stop==PredictionStop.None && interrupted.Count==6,"A second-slot hard exit cannot publish the earlier slot's half-completed sixth world frame.");
            SamePrefix(interrupted,oracle,5,"Both actors are committed together at world-step boundaries");
            terrain.BudgetThrowAt=0;var recovered=model.Prepare(group,2,1,10,40,1,e,p,terrain,null,false,120);
            Require(terrain.Resets==2 && !ReferenceEquals(interrupted.CalculationToken,recovered.CalculationToken),"A half-updated dependency group cannot be the next extension's scratch.");SamePrefix(recovered,oracle,40,"Multi-slot recovery starts from the observed group");
        }
        private static void TowerFrameEndPrefix()
        {
            // Same shield/629 contact fixture as NativeCombatCelestialTowerChecks.
            // Current receive state is a sampled value; AI recomputes it before
            // the frame-end soul write becomes the next actor action's input.
            var owner=BasicNpcMotionChecks.State(422,94);owner.Identity=new NpcIdentity(1,new object(),16,1,422,422);owner.X=2000;owner.Y=1800;owner.Width=80;owner.Height=300;owner.Life=owner.LifeMax=20000;owner.NoGravity=owner.NoTileCollide=true;owner.InactivityImmune=true;
            owner.TowerRelationsCaptured=owner.TowerRelationsComplete=owner.TowerShieldCaptured=true;owner.TowerShieldStrength=1;owner.TowerShieldMaximum=100;owner.TowerNewConsumerAt=int.MaxValue;
            var peer=owner;peer.Identity=new NpcIdentity(1,new object(),20,1,422,422);peer.X=2200;
            var fact=new PredictionTowerShieldConsumer(new object(),0,1,255,true,owner.Identity,new MotionRect(owner.X+10,owner.Y+10,8,8),0,0,3600,2,-1,true,false,true);
            var e=new PredictionEnvironment{WorldWidth=1000,WorldHeight=700,GravityWorldSurface=180,WorldSurface=180,RockLayer=230,TowerConsumers=new PredictionTowerShieldConsumers(new[]{fact},1,0,16000,0,11200)};
            var p=new PredictionPlayerMotion{Width=20,Height=40,GravityDirection=1};var group=new[]{owner,peer};var model=new RollingNpcPrediction();var terrain=new EmptyTerrain();
            var first=model.Prepare(group,2,1,10,1,1,e,p,terrain,null,false,8);
            var complete=model.Prepare(group,2,1,10,8,1,e,p,terrain,null,false,8);
            var oracle=new RollingNpcPrediction().Prepare(group,2,1,10,8,1,e,p,new EmptyTerrain());
            SamePrefix(first,oracle,1,"The first published tower frame includes all NPC actions");SamePrefix(complete,oracle,8,"Tower consumer scratch survives prefix extension");
            Require(!first[1].CanReceive && complete[2].CanReceive && ReferenceEquals(first.CalculationToken,complete.CalculationToken),"The shared shield write after all slots affects the other tower on its next action, without replaying the soul.");
        }
        private static void MoonLeechFrameEndPrefix()
        {
            // Reuse the observed AI82/456 head/anchor facts from the native
            // MoonLeech fixture. Returning 456 reads the completed head; raw
            // head health is a dependency, not a request to replay its whole AI.
            var head=BasicNpcMotionChecks.State(396,79);head.Identity=new NpcIdentity(1,new object(),20,1,396,396);head.X=1900;head.Y=1500;head.Width=38;head.Height=56;head.NoGravity=head.NoTileCollide=true;
            var clot=BasicNpcMotionChecks.State(401,82);clot.Identity=new NpcIdentity(1,new object(),25,1,401,401);clot.X=1900;clot.Y=2100;clot.Width=clot.Height=20;clot.NoGravity=clot.NoTileCollide=true;clot.MoonRelationsCaptured=clot.MoonRelationsComplete=true;clot.PositionOwnerCaptured=clot.PositionOwnerValid=true;clot.PositionOwner=head.Identity;clot.A0=21;clot.MoonLeechAnchorCaptured=clot.MoonLeechAnchorValid=true;clot.MoonLeechKey=1;clot.MoonLeechAnchorX=2004;clot.MoonLeechAnchorY=2104;clot.MoonGlobalRetireAt=int.MaxValue;
            var p=new PredictionPlayerMotion{PlayerToken=new object(),PlayerIndex=0,X=2400,Y=2360,Width=20,Height=40,GravityDirection=1};
            var fact=new PredictionMoonLeech(new object(),0,1,255,3600,0,-1,true,head.Identity,true,p.PlayerToken,0,new MotionRect(p.X,p.Y,p.Width,p.Height),true,false,false,true,new MotionRect(2000,2100,8,8),3.125f,-1.25f,-21,0,0,true,false,false);
            var e=new PredictionEnvironment{WorldWidth=1000,WorldHeight=700,WorldSurface=180,GravityWorldSurface=180,MoonLeeches=new PredictionMoonLeeches(new[]{fact},1,0,16000,0,11200)};
            var group=new[]{head,clot};var roles=new[]{false,true};var terrain=new EmptyTerrain();var model=new RollingNpcPrediction();
            var first=model.Prepare(group,2,1,10,2,1,e,p,terrain,roles,false,8);
            var complete=model.Prepare(group,2,1,10,8,1,e,p,terrain,roles,false,8);
            var oracle=new RollingNpcPrediction().Prepare(group,2,1,10,8,1,e,p,new EmptyTerrain(),roles,false,8);
            Require(terrain.TileCalls>0 && terrain.Resets==1 && ReferenceEquals(first.CalculationToken,complete.CalculationToken),"The returning leech's actual frame-end fluid consumer continues under one sample.");
            SamePrefix(first,oracle,2,"A clot reads the previous completed projectile anchor");SamePrefix(complete,oracle,8,"The projectile tail writes the next clot anchor once per complete frame");
            terrain=new EmptyTerrain{BudgetTileAfterMove=3};model=new RollingNpcPrediction();
            var interrupted=model.Prepare(group,2,1,10,8,1,e,p,terrain,roles,false,8);
            Require(interrupted!=null && interrupted.BudgetPaused && interrupted.Stop==PredictionStop.None && interrupted.Count==3,"A hard exit after the NPC slots but inside MoonLeech EndFrame cannot publish the third frame.");
            SamePrefix(interrupted,oracle,2,"The last actor returning is not yet a complete world-step checkpoint");
            terrain.BudgetTileAfterMove=0;var recovered=model.Prepare(group,2,1,10,8,1,e,p,terrain,roles,false,8);
            Require(terrain.Resets==2 && !ReferenceEquals(interrupted.CalculationToken,recovered.CalculationToken),"Frame-end consumer interruption discards its mutated relation scratch.");SamePrefix(recovered,oracle,8,"A rebuilt projectile/NPC relationship does not resume a half-consumed tail");
        }
        private static void SourceTimelinePrefix()
        {
            // Explicit absent-source facts reuse the native gravity countdown
            // and invalid Wall-global boundaries; they must remain attached to
            // the real prefix and its player, even while the NPC needs no player.
            var n=PrefixState();n.WallRelationsCaptured=n.WallRelationsComplete=n.WallBossCaptured=true;n.WallBossValid=false;
            var p=new PredictionPlayerMotion{PlayerToken=new object(),PlayerIndex=0,Width=20,Height=40,GravityDirection=1,GravityControlCaptured=true,ForcedGravityTicks=2,WallFactsCaptured=true,WallSourceExpected=true};
            var e=new PredictionEnvironment{WorldWidth=4200,WorldHeight=1200,WorldSurface=400,GravitySourceCaptured=true,GravitySourceValid=false};
            var terrain=new EmptyTerrain();var model=new RollingNpcPrediction();var first=model.Prepare(new[]{n},1,0,10,2,1,e,p,terrain,null,false,8);
            var complete=model.Prepare(new[]{n},1,0,10,8,1,e,p,terrain,null,false,8);var oracle=new RollingNpcPrediction().Prepare(new[]{n},1,0,10,8,1,e,p,new EmptyTerrain(),null,false,8);
            Require(first.GravitySources!=null && first.WallSources!=null,"A short prefix carries both required source timelines.");
            SamePrefix(first,oracle,2,"Earlier gravity/Wall source copies remain immutable");SamePrefix(complete,oracle,8,"Extended gravity/Wall source arrays cover the actual complete length");
        }
        private static void PresentGravityTimelinePrefix()
        {
            // NativeCombatBrainSwarmChecks' known negative client phase avoids
            // the server's random destination consumer. Its GoodWorld source
            // is written at the Brain slot and moves before the NEXT player.
            var brain=BasicNpcMotionChecks.State(266,54);brain.Identity=new NpcIdentity(1,new object(),16,1,266,266);brain.X=1000;brain.Y=1000;brain.Width=160;brain.Height=110;brain.L0=1;brain.A0=-1;brain.BrainNetMode=1;brain.Vx=2;brain.Vy=-.25f;brain.NoGravity=brain.NoTileCollide=true;brain.InactivityImmune=true;
            brain.BrainRelationsCaptured=brain.BrainRelationsComplete=brain.BrainBossCaptured=brain.BrainBossValid=true;brain.BrainBossIdentity=brain.Identity;
            var later=PrefixState();later.Identity=new NpcIdentity(1,new object(),24,1,999,999);later.PositionRelation=3;later.PositionOwner=brain.Identity;later.Vx=later.Vy=0;
            var p=new PredictionPlayerMotion{PlayerToken=new object(),PlayerIndex=0,X=2000,Y=1600,Width=20,Height=40,GravityDirection=1,MaxFall=10,GravityControlCaptured=true,GravitySourceExpected=true};
            var e=new PredictionEnvironment{WorldWidth=1000,WorldHeight=700,WorldSurface=180,GravityWorldSurface=180,PlayerIndex=0,PlayerX=2010,PlayerY=1620,PlayerWidth=20,PlayerHeight=40,Crimson=true,GoodWorld=true,Multiplayer=true,GravitySourceCaptured=true,GravitySourceValid=true,GravitySourceIdentity=brain.Identity,GravitySourceX=brain.Bounds.CenterX,GravitySourceY=brain.Bounds.CenterY};
            var group=new[]{brain,later};var terrain=new EmptyTerrain();var model=new RollingNpcPrediction();
            var first=model.Prepare(group,2,0,10,2,1,e,p,terrain,null,false,8);
            var complete=model.Prepare(group,2,0,10,8,1,e,p,terrain,null,false,8);var oracle=new RollingNpcPrediction().Prepare(group,2,0,10,8,1,e,p,new EmptyTerrain(),null,false,8);
            Require(first!=null && first.Count==3 && first.GravitySources!=null && first.GravitySources[0].Valid && first.GravitySources[2].Valid && !first.GravitySources[0].Equals(first.GravitySources[2]),"A real present Brain source changes position across the short complete prefix.");
            SamePrefix(first,oracle,2,"Published moving-source endpoint is immutable across extension");SamePrefix(complete,oracle,8,"Moving gravity-source arrays match one complete calculation");
            Require(terrain.Resets==1 && ReferenceEquals(first.CalculationToken,complete.CalculationToken),"Changing predicted source positions does not recapture a same-sample extension.");
            terrain=new EmptyTerrain{BudgetNpcSlot=24,BudgetNpcAction=3};model=new RollingNpcPrediction();
            var interrupted=model.Prepare(group,2,0,10,8,1,e,p,terrain,null,false,8);
            Require(interrupted!=null && interrupted.BudgetPaused && interrupted.Count==3 && interrupted.Stop==PredictionStop.None,"A later-slot hard exit after the gravity writer preserves only two complete world steps.");
            SamePrefix(interrupted,oracle,2,"Half-step gravity writes cannot replace the completed source endpoint");
            terrain.BudgetNpcAction=0;var recovered=model.Prepare(group,2,0,10,8,1,e,p,terrain,null,false,8);
            Require(terrain.Resets==2 && !ReferenceEquals(interrupted.CalculationToken,recovered.CalculationToken),"Moving-source interruption rebuilds its private actor/player scratch.");SamePrefix(recovered,oracle,8,"Moving-source recovery does not resume the partially written world");
        }
        private static void SegmentedObservationHistory()
        {
            var n=PrefixState();n.Vx=4;n.Vy=0;var e=new PredictionEnvironment{WorldWidth=4200,WorldHeight=1200,WorldSurface=400};var p=new PredictionPlayerMotion{Width=20,Height=40,GravityDirection=1};
            var segmented=new RollingNpcPrediction();var complete=new RollingNpcPrediction();var a=new EmptyTerrain();var b=new EmptyTerrain();
            segmented.Prepare(new[]{n},1,0,100,20,1,e,p,a,null,false,120);segmented.Prepare(new[]{n},1,0,100,120,1,e,p,a,null,false,120);complete.Prepare(new[]{n},1,0,100,120,1,e,p,b);
            n.X+=4;n.Vx=3.9992f;n.Vy=.079995f;
            var shortResult=segmented.Prepare(new[]{n},1,0,101,20,1,e,p,a,null,false,120);var grown=segmented.Prepare(new[]{n},1,0,101,120,1,e,p,a,null,false,120);var expected=complete.Prepare(new[]{n},1,0,101,120,1,e,p,b);
            SamePrefix(grown,expected,120,"Real observation history is absorbed once despite segment calls");SamePrefix(shortResult,expected,20,"Initial segment uses the same observed acceleration and turn");
            n.X+=n.Vx;n.Y+=n.Vy;
            SamePrefix(segmented.Prepare(new[]{n},1,0,102,120,1,e,p,a),complete.Prepare(new[]{n},1,0,102,120,1,e,p,b),120,"The following sample keeps identical observation history");
        }
        private static void ArmoredDayPursuit()
        {
            var n=new NpcMotionState{Identity=new NpcIdentity(1,new object(),1,1,77,77),X=100,Y=100,OldX=99,Vx=-1,Style=3,Width=30,Height=44,Life=100,LifeMax=100,TimeLeft=750,Active=true,CanReceive=true,Direction=-1,Scale=1,Health=new NpcHealthState{RealLife=-1}};
            var p=new PredictionPlayerMotion{X=400,Y=100,Width=20,Height=40,GravityDirection=1};
            var e=new PredictionEnvironment{PlayerX=410,PlayerY=120,PlayerWidth=20,PlayerHeight=40,WorldWidth=4200,WorldHeight=1200,WorldSurface=400,Day=true};
            var path=new RollingNpcPrediction().Prepare(new[]{n},1,0,1,1,1,e,p,new EmptyTerrain());
            Require(path.Count==2 && path[1].Vx>-1,"Armored skeleton with blocked count below sixty still pursues the player on a daytime surface.");
        }
        private static void ContinuationPlayerPremise()
        {
            var n=new NpcMotionState{Identity=new NpcIdentity(1,new object(),1,1,2,2),X=100,Y=100,Vx=1,Style=2,Width=30,Height=30,Life=100,LifeMax=100,TimeLeft=750,Active=true,CanReceive=true,NoGravity=true,NoTileCollide=true,Scale=1,Health=new NpcHealthState{RealLife=-1}};
            var p=new PredictionPlayerMotion{X=500,Y=100,Width=20,Height=40,GravityDirection=1,MaxFall=10,ReleaseJump=true};
            var e=new PredictionEnvironment{PlayerX=510,PlayerY=120,PlayerWidth=20,PlayerHeight=40,WorldWidth=4200,WorldHeight=1200,WorldSurface=400};
            var model=new RollingNpcPrediction{CaptureContinuation=true};
            var stable=model.Prepare(new[]{n},1,0,1,120,1,e,p,new EmptyTerrain(),null,true);
            Require(stable.Count==121 && model.Continuation!=null,"An independent known motor with a genuinely fixed player can retain a continuation proof.");
            model.Clear();var periodic=model.Prepare(new[]{n},1,0,2,120,1,e,p,new EmptyTerrain{PeriodicPlayer=true},null,true);
            // BeforeNpc skips the first player move. The remaining 119 moves
            // contain exactly seventeen seven-action cycles: both endpoints
            // equal the initial position while every cycle moves in between.
            Require(periodic.Count==121 && model.Continuation==null,"Equal BeforeNpc player endpoints do not prove a stationary premise across the complete window.");
        }
        private static void SlimeContinuation()
        {
            foreach(bool advanced in new[]{false,true})foreach(int timer in new[]{-2,-1002,-2002})
            {
                var n=new NpcMotionState{Identity=new NpcIdentity(1,new object(),1,1,1,1),X=100,Y=100,Style=1,A0=timer,A1=-1,A2=1,Direction=1,Width=32,Height=24,Life=100,LifeMax=100,TimeLeft=750,Active=true,CanReceive=true,Scale=1,WaterSpeed=1,HoneySpeed=1,LavaSpeed=1,ShimmerSpeed=1,WindyPreludeCaptured=true,WindySlimeContainsItems=true,WindySlimePositiveValue=true,WindyBaseLifeMax=100,Health=new NpcHealthState{RealLife=-1}};
                var p=new PredictionPlayerMotion{X=500,Y=100,Width=20,Height=40,GravityDirection=1,MaxFall=10,ReleaseJump=true};
                var e=new PredictionEnvironment{PlayerX=510,PlayerY=120,PlayerWidth=20,PlayerHeight=40,WorldWidth=4200,WorldHeight=1200,WorldSurface=400};
                var owner=new RollingNpcPrediction{CaptureContinuation=true};var terrain=new EmptyTerrain();
                var original=owner.Prepare(new[]{n},1,0,10,120,1,e,p,terrain,null,advanced,120);var proof=owner.Continuation;
                Require(original.Count==121 && proof!=null,"Complete slime jump phases can retain a fixed-input continuation.");
                var next=(NpcMotionState)typeof(RollingNpcPrediction.CandidateContinuation).GetField("next",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(proof);
                Require(proof.Matches(next,11,120,e,p,advanced),"The complete next state matches in its original player phase.");
                var changed=next;changed.A0++;
                Require(!proof.Matches(changed,11,120,e,p,advanced),"Position equality cannot hide a changed jump clock.");
                changed=next;changed.JustHit=true;changed.Vx=-4;changed.Life--;
                Require(!proof.Matches(changed,11,120,e,p,advanced),"Actual recoil and health changes reject the prior future.");
                changed=next;changed.Identity=new NpcIdentity(1,new object(),1,2,1,1);
                Require(!proof.Matches(changed,11,120,e,p,advanced),"Same-slot replacement never inherits a continuation.");
                var moving=p;moving.X++;
                Require(!proof.Matches(next,11,120,e,moving,advanced) && !proof.Matches(next,11,120,e,p,!advanced),"Player motion and before/after-NPC phase are independent premises.");
                var shifted=e;shifted.Day=!e.Day;
                Require(!proof.Matches(next,11,120,shifted,p,advanced),"Environment changes require a new jump decision.");
                var oldLast=original[120];int before=terrain.TotalMoves;
                var actual=proof.Advance(owner,next,11,120,1,e,p,terrain,advanced);
                var expected=new RollingNpcPrediction().Prepare(new[]{next},1,0,11,120,1,e,p,new EmptyTerrain(),null,advanced,120);
                SamePrefix(actual,expected,120,"Verified slime suffix equals a fresh complete model");
                Require(terrain.TotalMoves-before<=4 && original.CaptureTick==10 && original[120].Bounds.Equals(oldLast.Bounds),"Only head/tail actions advance and the prior immutable publication survives.");
                var origin=actual.CalculationToken as RollingNpcPrediction.ContinuationOrigin;
                Require(actual.CaptureTick==11 && origin!=null && origin.OriginalCaptureTick==10 && origin.VerifiedFromTick==10 && ReferenceEquals(origin.OriginalCalculationToken,original.CalculationToken),"A new real observation records its original computation separately.");
                foreach(int clock in new[]{1,30})
                {
                    var immune=p;immune.WallImmunityCaptured=true;immune.WallImmune=true;immune.WallImmuneTime=clock;
                    var timed=new RollingNpcPrediction{CaptureContinuation=true};timed.Prepare(new[]{n},1,0,10,120,1,e,immune,new EmptyTerrain(),null,advanced,120);
                    var timedProof=timed.Continuation;Require(timedProof!=null,"A non-Wall native immunity clock is not a moving target-player premise.");
                    var timedNext=(NpcMotionState)typeof(RollingNpcPrediction.CandidateContinuation).GetField("next",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(timedProof);
                    var nativeNext=immune;nativeNext.WallImmuneTime--;if(nativeNext.WallImmuneTime<=0)nativeNext.WallImmune=false;
                    Require(timedProof.Matches(timedNext,11,120,e,nativeNext,advanced) && !timedProof.Matches(timedNext,11,120,e,immune,advanced),"The full new player must contain exactly one native immunity action, including its expiry transition.");
                    var refreshed=nativeNext;refreshed.WallImmune=true;refreshed.WallImmuneTime+=10;
                    Require(!timedProof.Matches(timedNext,11,120,e,refreshed,advanced),"A newly refreshed hit timer cannot use the expected one-action clock.");
                    var hurt=nativeNext;hurt.WallLife--;
                    Require(!timedProof.Matches(timedNext,11,120,e,hurt,advanced),"Advancing the known timer cannot waive an unrelated actual health change.");
                }
                owner.Clear();n.A0=-999;
                owner.Prepare(new[]{n},1,0,12,120,1,e,p,new EmptyTerrain(),null,advanced,120);
                Require(owner.Continuation==null,"Windy possession is not an ordinary standalone slime proof.");
                owner.Clear();n.A0=timer;n.WindyPreludeCaptured=false;n.Identity=new NpcIdentity(1,new object(),1,1,685,685);
                owner.Prepare(new[]{n},1,0,13,120,1,e,p,new EmptyTerrain(),null,advanced,120);
                Require(owner.Continuation==null,"Random slime branches keep the honest representative recomputation.");
            }
        }
        private static void Require(bool value,string text){if(!value)throw new InvalidOperationException(text);}
        private static void ImmutableGeometry()
        {
            var points=new NpcTrajectoryPoint[121];
            for(int i=0;i<points.Length;i++)
            {
                var state=new NpcMotionState{Identity=new NpcIdentity(1,new object(),4,2,414,414),X=100.125f+i*.1f,Y=-10.75f+i*.2f,Width=37,Height=51,NetOffsetX=-3.7f,NetOffsetY=12.3f,Vx=-0.0f,Vy=1e-30f,A0=-17.25f,CanReceive=i%2==0,CanHarm=i%3==0,NewSegment=i%7==0};
                points[i]=new NpcTrajectoryPoint(i,state);
            }
            var original=(NpcTrajectoryPoint[])points.Clone();
            var path=new NpcTrajectory(default(NpcIdentity),10,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
            Array.Clear(points,0,points.Length);
            for(int i=0;i<path.Count;i++)
            {
                var actual=path[i];var expected=original[i];
                Require(actual.TickOffset==i && actual.Bounds.Equals(expected.Bounds) && actual.ReceiveBounds.Equals(expected.ReceiveBounds) && actual.ProjectileReceiveBounds.Equals(expected.ProjectileReceiveBounds) && actual.HasProjectileExtension==expected.HasProjectileExtension && actual.CanReceive==expected.CanReceive && actual.CanHarm==expected.CanHarm && actual.NewSegment==expected.NewSegment && BitConverter.ToInt32(BitConverter.GetBytes(actual.Vx),0)==BitConverter.ToInt32(BitConverter.GetBytes(expected.Vx),0) && actual.Vy==expected.Vy && actual.Phase==expected.Phase,"Rolling immutable storage preserves every physical/receive/extended rectangle and scalar at "+i);
            }
            NpcTrajectory window;
            Require(path.TryWindow(20,30,2,out window) && window.Count==31 && window[0].Bounds.Equals(original[10].Bounds) && window[0].ReceiveBounds.Equals(original[10].ReceiveBounds) && window[30].ProjectileReceiveBounds.Equals(original[40].ProjectileReceiveBounds),"A window shares immutable geometry with the correct offsets and receive extension.");
            var valueIdentity=new NpcIdentity(9,null,6,1,414,414);var token=new object();
            var valuePath=new NpcTrajectory(valueIdentity,10,1,PredictionAssumption.NoNewHits,PredictionStop.None,original,original.Length,PredictionStrategy.RollingConditional);
            var bound=valuePath.BindIdentity(new NpcIdentity(9,token,6,1,414,414));
            Require(ReferenceEquals(bound.Identity.Token,token) && valuePath.Identity.Token==null && bound.CaptureTick==valuePath.CaptureTick && bound[120].ProjectileReceiveBounds.Equals(original[120].ProjectileReceiveBounds),"Binding a value identity retains compact immutable geometry and its original capture time.");
            bool refused=false;try{valuePath.BindIdentity(new NpcIdentity(9,token,7,1,414,414));}catch(InvalidOperationException){refused=true;}
            Require(refused,"A compact result cannot bind a different slot identity.");
        }
    }
}
