using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;

namespace JueMingR.ArchitectureTests
{
    internal static class BasicNpcMotionChecks
    {
        internal static void Run()
        {
            var terrain=new LocalTerrain();var e=new PredictionEnvironment{PlayerX=1000,PlayerY=168,PlayerWidth=20,PlayerHeight=40,WorldWidth=4200,WorldHeight=1200,WorldSurface=400};
            var fish=State(157,16);fish.NoGravity=true;fish.Vy=-2;
            PredictionStop stop; if(Environment.GetEnvironmentVariable("JUEMINGR_BASIC_CASE")!="plant")Require(NpcMotion.Step(ref fish,new[]{fish},1,e,terrain,1,true,out stop) && Math.Abs(fish.Vy+1.7f)<.0001f,"Dry fish applies AI16 gravity even with NoGravity set.");
            var plant=State(56,13);plant.NoGravity=plant.NoTileCollide=true;plant.A0=plant.A1=10;plant.X=400;plant.Vx=2;
            Require(NpcMotion.Step(ref plant,new[]{plant},1,e,terrain,1,true,out stop) && plant.Vx<2,"Rooted plant brakes beyond its bounded target instead of following a free-flight trend.");
            var spider=State(237,40);spider.Width=spider.Height=36;spider.NoGravity=true;terrain.Walls=true;
            Require(NpcMotion.Step(ref spider,new[]{spider},1,e,terrain,1,true,out stop) && spider.Vx>0 && spider.Style==40,"Wall spider accelerates toward the player while local background walls support its form.");
            var origin=spider.Identity;terrain.Walls=false;spider.L1=0;
            var otherForm=spider;otherForm.MotionType=236;
            Require(!NpcPredictionCache.Same(spider,otherForm),"A private form change is part of scalar state equality, without rewriting identity.");
            Require(NpcMotion.Step(ref spider,new[]{spider},1,e,terrain,1,true,out stop) && spider.Style==3 && !spider.NoGravity && spider.Width==50 && spider.Height==20 && spider.L1==12 && spider.Identity.Equals(origin),"Wall loss changes the private body, preserving real identity and conversion-frame cooldown twelve.");
            spider.Vy=0;spider.L1=0;terrain.Walls=true;
            Require(NpcMotion.Step(ref spider,new[]{spider},1,e,terrain,1,true,out stop) && spider.Style==40 && spider.NoGravity && spider.Width==36 && spider.L1==12 && spider.Identity.Equals(origin),"Ground attachment changes the private body without retiring or forging its real origin.");
            terrain.Walls=false;var dry=State(58,16);dry.NoGravity=true;
            Require(NpcMotion.Step(ref dry,new[]{dry},1,e,terrain,1,true,out stop) && dry.Vy>=-4.7f && dry.Vy<=-1.8f && Math.Abs(dry.Vx)<=2 && (NpcMotion.Assumptions(dry)&PredictionAssumption.RandomRepresentative)!=0,"Grounded dry flop uses an original legal range and an explicit representative premise.");
            var affected=State(77,3);affected.BuffFingerprint=123;affected.BuffExpires=3;
            Require(NpcMotion.Step(ref affected,new[]{affected},1,e,terrain,1,true,out stop),"A modeled structural family can conditionally retain current movement with an unclassified state.");
            Require(!NpcMotion.Step(ref affected,new[]{affected},1,e,terrain,3,true,out stop) && stop==PredictionStop.BuffTransition,"Unknown state expiration is a real boundary, not declared harmless forever.");
            var before=State(77,3);before.Vx=1;var confused=before;confused.ConfusedTicks=2;
            Require(NpcMotion.Step(ref confused,new[]{confused},1,e,terrain,1,true,out stop) && confused.Vx<before.Vx,"Observed confusion changes fighter direction immediately.");
            PlayerPremise(e);
            terrain.Walls=true;terrain.WallCells=4;spider=State(237,40);spider.Width=spider.Height=36;spider.NoGravity=true;
            Require(NpcMotion.Step(ref spider,new[]{spider},1,e,terrain,1,true,out stop) && spider.Style==3,"Four background-wall cells are insufficient for sticking.");
            terrain.WallCells=5;spider=State(237,40);spider.Width=spider.Height=36;spider.NoGravity=true;
            Require(NpcMotion.Step(ref spider,new[]{spider},1,e,terrain,1,true,out stop) && spider.Style==40,"Five eligible background-wall cells permit sticking.");
            terrain.WallCells=9;terrain.WallSolid=true;spider=State(237,40);spider.Width=spider.Height=36;spider.NoGravity=true;
            Require(NpcMotion.Step(ref spider,new[]{spider},1,e,terrain,1,true,out stop) && spider.Style==3,"Actuated raw-active solid foreground still prevents wall adhesion.");
            terrain.Unknown=true;spider=State(237,40);spider.Width=spider.Height=36;spider.NoGravity=true;
            Require(!NpcMotion.Step(ref spider,new[]{spider},1,e,terrain,1,true,out stop) && stop==PredictionStop.TerrainUnavailable,"Unknown wall input is not a known wall-free cell.");
            terrain.Unknown=terrain.WallSolid=terrain.Walls=false;terrain.ActuatedRoot=true;plant=State(56,13);plant.A0=plant.A1=10;plant.NoGravity=plant.NoTileCollide=true;
            Require(NpcMotion.Step(ref plant,new[]{plant},1,e,terrain,1,true,out stop),"An actuated but raw-active root remains a real plant anchor.");
            terrain.Root=false;
            Require(!NpcMotion.Step(ref plant,new[]{plant},1,e,terrain,1,true,out stop) && stop==PredictionStop.Despawn,"Actual root removal ends the plant body forecast.");
            var unsupported=State(999,13);unsupported.NoGravity=true;
            Require(!NpcMotion.Step(ref unsupported,new[]{unsupported},1,e,terrain,1,true,out stop) && stop==PredictionStop.UnsupportedMechanism,"An unmodeled structural family cannot silently use a free-flight trend.");
        }
        private static void PlayerPremise(PredictionEnvironment e)
        {
            var plant=State(56,13);plant.A0=plant.A1=10;plant.NoGravity=plant.NoTileCollide=true;
            var player=new PredictionPlayerMotion{X=800,Y=100,Width=20,Height=40,GravityDirection=1,Vx=-10,Complex=true};
            var firstTerrain=new LocalTerrain{FailPlayerAt=1};var laterTerrain=new LocalTerrain{FailPlayerAt=20};
            var a=new RollingNpcPrediction().Prepare(new[]{plant},1,0,10,120,1,e,player,firstTerrain);
            var b=new RollingNpcPrediction().Prepare(new[]{plant},1,0,10,120,1,e,player,laterTerrain);
            Require(a.Count==121 && b.Count==121 && b.Quality==PredictionQuality.StructuredApproximation && (b.Assumptions&PredictionAssumption.CurrentPlayerObservation)!=0 && (b.Assumptions&PredictionAssumption.HeldPlayerControls)==0,"Unavailable player geometry can use one bounded structural current-observation premise and reports a structural model, not trend.");
            for(int i=0;i<a.Count;i++)Require(a[i].Bounds.Equals(b[i].Bounds),"A mid-forecast failure restarts consistently instead of splicing an artificial target reversal.");
            var unknown=new LocalTerrain{FailPlayerAt=1,Unknown=true};
            var c=new RollingNpcPrediction().Prepare(new[]{plant},1,0,10,120,1,e,player,unknown);
            Require(c.Count==1 && c.Stop==PredictionStop.TerrainUnavailable,"Fallback never substitutes air for an unknown root.");
            var other=State(999,0);other.NoGravity=true;
            var d=new RollingNpcPrediction().Prepare(new[]{other},1,0,10,120,1,e,player,new LocalTerrain{FailPlayerAt=1});
            Require(d.Count==1 && d.Stop==PredictionStop.TerrainUnavailable,"Unknown free-trend families retain the required player geometry boundary.");
        }
        internal static NpcMotionState State(int type,int style)
        {return new NpcMotionState{Identity=new NpcIdentity(1,new object(),1,1,type,type),Style=style,X=168,Y=168,OldX=168,OldY=168,Width=30,Height=30,Scale=1,Direction=1,DirectionY=1,Life=100,LifeMax=100,TimeLeft=750,Active=true,CanReceive=true,WaterSpeed=1,HoneySpeed=1,LavaSpeed=1,ShimmerSpeed=1,Health=new NpcHealthState{RealLife=-1}};}
        internal sealed class LocalTerrain : IPredictionTerrain,IPredictionResizeTerrain
        {
            internal bool Walls,Unknown,WallSolid,ActuatedRoot,Root=true;internal int WallCells=9,FailPlayerAt,PlayerMoves;
            public bool Unchanged=>true;
            public void Reset(){PlayerMoves=0;}
            public bool Resize(MotionRect box,int width,int height,out MotionRect adjusted,out bool canResize,out PredictionStop stop)
            {adjusted=new MotionRect((int)box.X+(width-(int)box.Width)/2,(int)box.Y+(int)box.Height-height,width,height);canResize=true;stop=PredictionStop.None;return true;}
            public bool Move(ref NpcMotionState n,PredictionEnvironment e,out PredictionStop stop){stop=PredictionStop.None;if(n.Friendly && FailPlayerAt>0 && ++PlayerMoves>=FailPlayerAt){stop=PredictionStop.TerrainUnavailable;return false;}n.X+=n.Vx;n.Y+=n.Vy;return true;}
            public bool Solid(MotionRect a,out bool solid,out PredictionStop stop){solid=false;stop=PredictionStop.None;return true;}
            public bool CanHit(MotionRect a,MotionRect b,out bool clear,out PredictionStop stop){clear=true;stop=PredictionStop.None;return true;}
            public bool Tile(int x,int y,out PredictionTile tile,out PredictionStop stop){int cell=(x-10)*3+y-10;bool wall=Walls && cell>=0 && cell<WallCells;tile=new PredictionTile{Active=x==10 && y==10 && Root && !ActuatedRoot,RawActive=x==10 && y==10 && Root || wall && WallSolid,RawSolid=wall && WallSolid,Wall=(ushort)(wall?1:0)};stop=Unknown?PredictionStop.TerrainUnavailable:PredictionStop.None;return !Unknown;}
        }
        internal static void Require(bool value,string text){if(!value)throw new InvalidOperationException(text);}
    }
}
