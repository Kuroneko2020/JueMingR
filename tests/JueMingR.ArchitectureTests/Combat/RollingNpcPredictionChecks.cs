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
            internal int Resets,Moves,Limit=int.MaxValue;
            internal bool BadPlayer,CliffSupport,UnknownCliff;
            public bool Unchanged=>true;
            public void Reset(){Resets++;Moves=0;}
            public bool Move(ref NpcMotionState n,PredictionEnvironment e,out PredictionStop stop)
            {stop=PredictionStop.None;if(++Moves>Limit){stop=PredictionStop.TerrainUnavailable;return false;}n.X+=n.Vx;n.Y+=n.Vy;if(BadPlayer && n.Friendly)n.Y=float.NaN;return true;}
            public bool Solid(MotionRect area,out bool solid,out PredictionStop stop){solid=false;stop=PredictionStop.None;return true;}
            public bool CanHit(MotionRect a,MotionRect b,out bool clear,out PredictionStop stop){clear=true;stop=PredictionStop.None;return true;}
            public bool Tile(int x,int y,out PredictionTile tile,out PredictionStop stop)
            {tile=default(PredictionTile);stop=PredictionStop.None;if(x==9 && y==10){if(UnknownCliff){stop=PredictionStop.TerrainUnavailable;return false;}tile.Active=tile.Solid=CliffSupport;}return true;}
        }
        private static void ArmoredDayPursuit()
        {
            var n=new NpcMotionState{Identity=new NpcIdentity(1,new object(),1,1,77,77),X=100,Y=100,OldX=99,Vx=-1,Style=3,Width=30,Height=44,Life=100,LifeMax=100,TimeLeft=750,Active=true,CanReceive=true,Direction=-1,Scale=1,Health=new NpcHealthState{RealLife=-1}};
            var p=new PredictionPlayerMotion{X=400,Y=100,Width=20,Height=40,GravityDirection=1};
            var e=new PredictionEnvironment{PlayerX=410,PlayerY=120,PlayerWidth=20,PlayerHeight=40,WorldWidth=4200,WorldHeight=1200,WorldSurface=400,Day=true};
            var path=new RollingNpcPrediction().Prepare(new[]{n},1,0,1,1,1,e,p,new EmptyTerrain());
            Require(path.Count==2 && path[1].Vx>-1,"Armored skeleton with blocked count below sixty still pursues the player on a daytime surface.");
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
