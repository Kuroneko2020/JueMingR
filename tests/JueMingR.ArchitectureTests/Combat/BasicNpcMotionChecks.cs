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
        }
        internal static NpcMotionState State(int type,int style)
        {return new NpcMotionState{Identity=new NpcIdentity(1,new object(),1,1,type,type),Style=style,X=168,Y=168,OldX=168,OldY=168,Width=30,Height=30,Scale=1,Direction=1,DirectionY=1,Life=100,LifeMax=100,TimeLeft=750,Active=true,CanReceive=true,WaterSpeed=1,HoneySpeed=1,LavaSpeed=1,ShimmerSpeed=1,Health=new NpcHealthState{RealLife=-1}};}
        internal sealed class LocalTerrain : IPredictionTerrain
        {
            public bool Unchanged=>true;
            public void Reset(){}
            public bool Move(ref NpcMotionState n,PredictionEnvironment e,out PredictionStop stop){stop=PredictionStop.None;n.X+=n.Vx;n.Y+=n.Vy;return true;}
            public bool Solid(MotionRect a,out bool solid,out PredictionStop stop){solid=false;stop=PredictionStop.None;return true;}
            public bool CanHit(MotionRect a,MotionRect b,out bool clear,out PredictionStop stop){clear=true;stop=PredictionStop.None;return true;}
            public bool Tile(int x,int y,out PredictionTile tile,out PredictionStop stop){tile=new PredictionTile{Active=x==10 && y==10,RawActive=x==10 && y==10};stop=PredictionStop.None;return true;}
        }
        internal static void Require(bool value,string text){if(!value)throw new InvalidOperationException(text);}
    }
}
