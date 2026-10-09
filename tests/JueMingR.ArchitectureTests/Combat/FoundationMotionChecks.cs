using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;

namespace JueMingR.ArchitectureTests
{
    // Locked .8 UpdateNPC_UpdateGravity values are independent expectations,
    // before any AI or movement. This is a scalar rule check, not native AI.
    internal static class FoundationMotionChecks
    {
        internal static void Run()
        {
            var e=new PredictionEnvironment{PlayerX=1200,PlayerY=5010,PlayerWidth=20,PlayerHeight=40,WorldWidth=4200,WorldHeight=1200,WorldSurface=400};
            var terrain=new OpenTerrain();PredictionStop stop;
            foreach(int type in new[]{258,425,427,426,576,577,541,17,999})
            foreach(bool phase in new[]{false,true})
            foreach(int liquid in new[]{0,1,2,3})
            {
                var n=State(type,99);n.Vy=40;n.A0=phase?1:0;n.A1=phase?2:0;n.A2=phase?1:0;if(type==17){n.Style=7;n.A0=phase?25:0;}
                n.Wet=liquid!=0;n.Honey=liquid==2;n.Shimmer=liquid==3;
                float gravity=.3f,pre=40,fall=10;
                if(type==258 || type==426){gravity=.1f;pre=3;}
                else if(type==425 && phase)gravity=.1f;
                else if((type==576 || type==577) && phase){gravity=.45f;pre=32;}
                else if(type==427 && phase){gravity=.1f;pre=4;}
                else if(type==541 || type==17 && phase)gravity=0;
                if(liquid!=0){gravity=liquid==3?.15f:liquid==2?.1f:.2f;fall=liquid==3?5.5f:liquid==2?4:7;}
                Require(NpcMotion.Step(ref n,new[]{n},1,e,terrain,1,true,out stop),"Public gravity scalar step exists.");
                Require(Math.Abs(n.Gravity-gravity)<.00001f && n.MaxFall==fall && Math.Abs(n.Vy-Math.Min(fall,pre+gravity))<.00001f,"Locked public gravity type/phase/pre-AI clip/wet override: "+type+" "+phase+" "+liquid);
            }
            var transformed=State(999,99);transformed.MotionType=258;transformed.Vy=4;
            Require(NpcMotion.Step(ref transformed,new[]{transformed},1,e,terrain,1,true,out stop) && Math.Abs(transformed.Vy-3.1f)<.00001f,"Current private effective type controls pre-AI public physics without rewriting origin identity.");
            var wolf=State(104,3);wolf.Vx=2;wolf.Vy=0;wolf.OldX=wolf.X-2;
            Require(NpcMotion.Step(ref wolf,new[]{wolf},1,e,terrain,1,true,out stop) && wolf.Vx==2,"Ordinary native speed-two fighter must not be decelerated toward invented 1.5.");
            var dependent=State(2,2);dependent.NoGravity=true;
            var player=new PredictionPlayerMotion{X=1200,Y=5000,Vx=2,Width=20,Height=40,GravityDirection=1,MaxFall=10,MaxSpeed=3,Acceleration=.08f,Slowdown=.2f,Grappled=true,UnsupportedGeometry=true,Complex=true};
            var boundary=new PlayerBoundaryTerrain();var conditional=new RollingNpcPrediction().Prepare(new[]{dependent},1,0,1,120,1,e,player,boundary);
            Require(conditional!=null && conditional.Count==121 && (conditional.Assumptions&PredictionAssumption.CurrentPlayerObservation)!=0,"Legal grapple supplies an explicit bounded player premise to a complete target route instead of failing first-step geometry.");
            Require(boundary.LastPlayerX==player.X+player.Width*.5f+24 && boundary.PlayerMoves==0,"Moving grapple estimate ends after twelve updates and bypasses only the player premise.");
            player.Grappled=false;player.Rope=true;player.Vx=0;
            boundary=new PlayerBoundaryTerrain();conditional=new RollingNpcPrediction().Prepare(new[]{dependent},1,0,1,120,1,e,player,boundary);
            Require(conditional.Count==121 && boundary.LastPlayerX==player.X+player.Width*.5f,"Idle rope has an explicit stationary observed premise.");
            player.Rope=false;player.FloatInWater=true;player.FloatingNow=true;player.Vy=-.4f;
            boundary=new PlayerBoundaryTerrain();conditional=new RollingNpcPrediction().Prepare(new[]{dependent},1,0,1,120,1,e,player,boundary);
            Require(conditional.Count==121 && boundary.PlayerMoves==0,"Qualified floating is not forced through ordinary wet player collision.");
            player.FloatInWater=player.FloatingNow=false;player.Vx=float.NaN;
            Require(new RollingNpcPrediction().Prepare(new[]{dependent},1,0,1,120,1,e,player,new OpenTerrain())==null,"A complex player premise never waives invalid numeric state.");
            player.Vx=0;player.Grappled=true;player.InvalidMechanism=true;
            Require(new RollingNpcPrediction().Prepare(new[]{dependent},1,0,1,120,1,e,player,new OpenTerrain())==null,"Bad grapple ownership remains a player boundary.");
            var free=State(999,99);free.NoGravity=true;
            conditional=new RollingNpcPrediction().Prepare(new[]{free},1,0,1,120,1,e,default(PredictionPlayerMotion),new OpenTerrain());
            Require(conditional.Count==121 && (conditional.Assumptions&PredictionAssumption.NoPlayerMotionNeeded)!=0,"Independent trend does not acquire an irrelevant invalid player prerequisite.");
            var slime=State(1,1);slime.A0=-1;slime.A2=1;slime.Vy=0;slime.TargetCaptured=true;slime.PlayerIndex=0;slime.PlayerDead=true;slime.HasClosestPlayer=true;slime.ClosestPlayerIndex=1;slime.ClosestPlayerArea=new MotionRect(1200,5000,20,40);
            Require(NpcMotion.PlayerPremiseTarget(slime,new PredictionEnvironment{PlayerIndex=-1,WorldSurface=400})==1,"Ordinary slime acquires the live first-jump target before Source rejects the old dead player.");
            slime.Y=900;slime.Health.Fire=60;slime.Health.RegenCount=-112;slime.A3=-1;
            Require(NpcMotion.PlayerPremiseTarget(slime,new PredictionEnvironment{PlayerIndex=-1,WorldSurface=140,Day=true})==1 && slime.Life==slime.LifeMax && slime.Health.RegenCount==-112,"First target choice shares AI-entry DOT life while the captured actor remains immutable.");
            slime.Health.RealLife=2;
            Require(NpcMotion.PlayerPremiseTarget(slime,new PredictionEnvironment{PlayerIndex=-1,WorldSurface=140,Day=true})==0,"Shared-owner DOT does not falsely damage this actor or alter its target-choice life.");
        }
        private static NpcMotionState State(int type,int style)
        {return new NpcMotionState{Identity=new NpcIdentity(1,new object(),1,1,type,type),X=1000,Y=5000,Width=20,Height=20,Scale=1,Style=style,Direction=1,DirectionY=1,SpriteDirection=1,Life=100,LifeMax=100,TimeLeft=750,Active=true,CanReceive=true,NoTileCollide=true,Health=new NpcHealthState{RealLife=-1}};}
        private static void Require(bool valid,string text){if(!valid)throw new InvalidOperationException(text);}
        private sealed class OpenTerrain : IPredictionTerrain
        {
            public bool Unchanged=>true;
            public void Reset(){}
            public bool Move(ref NpcMotionState n,PredictionEnvironment e,out PredictionStop stop){n.X+=n.Vx;n.Y+=n.Vy;stop=PredictionStop.None;return true;}
            public bool Solid(MotionRect area,out bool solid,out PredictionStop stop){solid=false;stop=PredictionStop.None;return true;}
            public bool CanHit(MotionRect a,MotionRect b,out bool clear,out PredictionStop stop){clear=true;stop=PredictionStop.None;return true;}
            public bool Tile(int x,int y,out PredictionTile tile,out PredictionStop stop){tile=default(PredictionTile);stop=PredictionStop.None;return true;}
        }
        private sealed class PlayerBoundaryTerrain : IPredictionTerrain,IPredictionPlayerTerrain
        {
            internal float LastPlayerX;internal int PlayerMoves;
            public bool Unchanged=>true;
            public void Reset(){}
            public bool Move(ref NpcMotionState n,PredictionEnvironment e,out PredictionStop stop){LastPlayerX=e.PlayerX;n.X+=n.Vx;n.Y+=n.Vy;stop=PredictionStop.None;return true;}
            public bool MovePlayer(ref NpcMotionState n,PredictionEnvironment e,ref PredictionPlayerMotion p,out PredictionStop stop){PlayerMoves++;stop=PredictionStop.UnsupportedMechanism;return false;}
            public bool Solid(MotionRect area,out bool solid,out PredictionStop stop){solid=false;stop=PredictionStop.None;return true;}
            public bool CanHit(MotionRect a,MotionRect b,out bool clear,out PredictionStop stop){clear=true;stop=PredictionStop.None;return true;}
            public bool Tile(int x,int y,out PredictionTile tile,out PredictionStop stop){tile=default(PredictionTile);stop=PredictionStop.None;return true;}
        }
    }
}
