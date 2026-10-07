using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;

namespace JueMingR.ArchitectureTests
{
    internal static class RollingPremiseChecks
    {
        internal static void Run()
        {
            var fish=new NpcMotionState{Identity=new NpcIdentity(1,new object(),2,1,58,58),X=100,Y=100,Width=20,Height=20,Vx=1,Style=16,Direction=1,DirectionY=1,Life=100,LifeMax=100,TimeLeft=750,Active=true,CanReceive=true,NoGravity=true,Wet=true,Health=new NpcHealthState{RealLife=-1}};
            var player=new PredictionPlayerMotion{X=500,Y=100,Width=20,Height=40,GravityDirection=1,Vx=3,Right=true,MaxSpeed=3,MaxFall=10};
            var environment=new PredictionEnvironment{PlayerX=510,PlayerY=120,PlayerWidth=20,PlayerHeight=40,PlayerWet=true,WorldWidth=4200,WorldHeight=1200,WorldSurface=400};
            var late=new PremiseTerrain{FailAt=10};var repaired=new RollingNpcPrediction().Prepare(new[]{fish},1,0,1,120,1,environment,player,late);
            var fixedPlayer=player;fixedPlayer.Vx=0;fixedPlayer.Right=false;fixedPlayer.Complex=true;
            var fixedPath=new RollingNpcPrediction().Prepare(new[]{fish},1,0,1,120,1,environment,fixedPlayer,new PremiseTerrain());
            Require(late.PlayerCalls==10 && late.Resets==2 && repaired.Count==121 && fixedPath.Count==121,"Late player geometry failure restarts at most once, without dropping a valid structural whole future.");
            Require((repaired.Assumptions&PredictionAssumption.CurrentPlayerObservation)!=0 && (repaired.Assumptions&PredictionAssumption.HeldPlayerControls)==0,"Restart has one explicit player-observation premise for the whole path.");
            for(int i=0;i<121;i++)Require(repaired[i].Bounds.Equals(fixedPath[i].Bounds) && repaired[i].Vx==fixedPath[i].Vx && repaired[i].Vy==fixedPath[i].Vy,"No retained conditional prefix or halfway jump to origin: "+i);
            var eye=fish;eye.Identity=new NpcIdentity(1,new object(),3,1,2,2);eye.Style=2;eye.Wet=false;
            var denied=new RollingNpcPrediction().Prepare(new[]{fish,eye},2,0,1,120,1,environment,player,new PremiseTerrain{FailAt=10},new[]{true,true});
            Require(denied.Count==10 && denied.Stop==PredictionStop.TerrainUnavailable && (denied.Assumptions&PredictionAssumption.CurrentPlayerObservation)==0,"Selected fish's fallback permission cannot silently replace an actual eye dependency's player timeline.");
            var healthOnly=new RollingNpcPrediction().Prepare(new[]{fish,eye},2,0,1,120,1,environment,player,new PremiseTerrain{FailAt=10},new[]{true,false});
            Require(healthOnly.Count==121 && (healthOnly.Assumptions&PredictionAssumption.CurrentPlayerObservation)!=0,"Health-only owner does not add motion/player-premise requirements.");
            var stationary=fish;stationary.Identity=new NpcIdentity(1,new object(),2,1,488,488);stationary.Style=0;var corrupt=player;corrupt.MaxFall=float.NaN;
            var unaffected=new RollingNpcPrediction().Prepare(new[]{stationary},1,0,2,120,1,environment,corrupt,new PremiseTerrain{FailAt=1});
            Require(unaffected.Count==121 && (unaffected.Assumptions&PredictionAssumption.NoPlayerMotionNeeded)!=0,"Stationary actual model ignores an unrelated corrupt player future.");
            var unknown=fish;unknown.Identity=new NpcIdentity(1,new object(),2,1,999,999);unknown.Style=3;unknown.Wet=false;unknown.NoTileCollide=true;
            var trend=new RollingNpcPrediction().Prepare(new[]{unknown},1,0,2,2,1,environment,player,new PremiseTerrain());
            Require(trend.Quality==PredictionQuality.LimitedObservation,"Generic fighter approximation is not upgraded by a player fallback permission.");
            stationary.BuffFingerprint=123;stationary.BuffExpires=100;
            var effect=new RollingNpcPrediction().Prepare(new[]{stationary},1,0,3,120,1,environment,player,new PremiseTerrain());
            Require(effect.Count==1 && effect.Stop==PredictionStop.BuffTransition,"A known stationary model does not prove an unknown status effect harmless.");
            fish.BuffFingerprint=123;fish.BuffExpires=5;
            var qualified=new RollingNpcPrediction().Prepare(new[]{fish},1,0,4,120,1,environment,player,new PremiseTerrain());
            Require(qualified.Count==5 && qualified.Stop==PredictionStop.BuffTransition && qualified.Quality==PredictionQuality.StructuredApproximation && (qualified.Assumptions&PredictionAssumption.UnmodeledStatusEffects)!=0,"Explicit effect continuation is qualified only to expiry and quality still follows the motion model.");
            var uncertain=eye;uncertain.TargetChoiceUnknown=true;uncertain.NoTileCollide=true;uncertain.TargetCaptured=uncertain.HasPlayer=uncertain.HasClosestPlayer=true;uncertain.PlayerIndex=uncertain.ClosestPlayerIndex=0;uncertain.ClosestPlayerArea=new MotionRect(500,100,20,40);
            PredictionStop stop;
            Require(!NpcMotion.Step(ref uncertain,new[]{uncertain},1,environment,new PremiseTerrain(),1,true,out stop) && stop==PredictionStop.TerrainUnavailable,"An actual TargetClosest consumer cannot call unknown guardian visibility blocked.");
            var free=eye;free.Style=0;free.TargetChoiceUnknown=true;
            Require(NpcMotion.Step(ref free,new[]{free},1,environment,new PremiseTerrain(),1,true,out stop),"Unconsumed tracking choice cannot block a free trend.");
            var escaping=uncertain;escaping.TargetChoiceUnavailable=false;
            var day=environment;day.Day=true;
            Require(NpcMotion.Step(ref escaping,new[]{escaping},1,day,new PremiseTerrain(),1,true,out stop) && !escaping.TargetChoiceUnavailable,"Dry daytime eye escape skips native TargetClosest.");
            var dayTerrain=new PremiseTerrain{FailAt=1};var dayPath=new RollingNpcPrediction().Prepare(new[]{escaping},1,0,5,120,1,day,corrupt,dayTerrain);
            Require(dayPath.Count==121 && dayTerrain.PlayerCalls==0 && (dayPath.Assumptions&PredictionAssumption.NoPlayerMotionNeeded)!=0,"Complete daytime eye rolling ignores unrelated invalid player future.");
            var bee=uncertain;bee.TargetChoiceUnavailable=false;bee.Identity=new NpcIdentity(1,new object(),4,1,210,210);bee.Style=5;
            bee.BeeTargetValid=bee.BeeFoundTarget=bee.BeeTankPet=true;bee.BeeFacingArea=new MotionRect(120,100,20,40);bee.BeeOldTarget=bee.Target=0;
            Require(NpcMotion.Step(ref bee,new[]{bee},1,environment,new PremiseTerrain(),1,true,out stop) && bee.BeeFaceForced && !bee.TargetChoiceUnavailable,"AI5 bee uses its captured finite guardian choice, not the ordinary closest-player query.");
            var missingBee=bee;
            Require(!NpcMotion.Step(ref missingBee,new[]{missingBee},1,environment,new PremiseTerrain{MissingHit=true},2,true,out stop) && stop==PredictionStop.TerrainUnavailable,"AI5 bee cannot treat necessary guardian LOS as blocked when it is unavailable.");
            var anchor=uncertain;anchor.TargetChoiceUnavailable=false;anchor.Identity=new NpcIdentity(1,new object(),4,1,56,56);anchor.Style=13;anchor.A0=6;anchor.A1=6;anchor.ClosestPlayerIndex=1;
            Require(NpcMotion.Step(ref anchor,new[]{anchor},1,environment,new PremiseTerrain{RootActive=true},1,true,out stop) && anchor.Target==1 && !anchor.TargetChoiceUnavailable,"Anchor updates known numbered player without requiring unknown guardian orientation.");
            escaping.Wet=true;
            Require(!NpcMotion.Step(ref escaping,new[]{escaping},1,day,new PremiseTerrain(),2,true,out stop) && stop==PredictionStop.TerrainUnavailable,"Later wet eye TargetClosest still consumes its visibility choice.");
        }
        private sealed class PremiseTerrain : IPredictionTerrain
        {
            internal int FailAt=int.MaxValue,PlayerCalls,Resets;
            internal bool RootActive,MissingHit;
            public bool Unchanged=>true;
            public void Reset(){Resets++;}
            public bool Move(ref NpcMotionState n,PredictionEnvironment e,out PredictionStop stop)
            {stop=PredictionStop.None;if(n.Friendly){if(++PlayerCalls==FailAt){stop=PredictionStop.TerrainUnavailable;return false;}n.Wet=e.PlayerWet;}n.X+=n.Vx;n.Y+=n.Vy;return true;}
            public bool CanHit(MotionRect a,MotionRect b,out bool clear,out PredictionStop stop){clear=!MissingHit;stop=MissingHit?PredictionStop.TerrainUnavailable:PredictionStop.None;return !MissingHit;}
            public bool Solid(MotionRect box,out bool solid,out PredictionStop stop){solid=false;stop=PredictionStop.None;return true;}
            public bool Tile(int x,int y,out PredictionTile tile,out PredictionStop stop){tile=new PredictionTile{RawActive=RootActive};stop=PredictionStop.None;return true;}
        }
        private static void Require(bool value,string text){if(!value)throw new InvalidOperationException(text);}
    }
}
