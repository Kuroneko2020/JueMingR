using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;

namespace JueMingR.ArchitectureTests
{
    internal static class BasicNpcMotionChecks
    {
        internal static void Run()
        {
            SameCause();
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
        private static void SameCause()
        {
            string selected=Environment.GetEnvironmentVariable("JUEMINGR_SAME_CAUSE_CASE");
            var e=new PredictionEnvironment{PlayerX=1000,PlayerY=168,PlayerWidth=20,PlayerHeight=40,WorldWidth=4200,WorldHeight=1200,WorldSurface=400,Day=true};
            PredictionStop stop;
            if(selected==null || selected=="wall")
            {
                int[] ground={163,164,236,239,530},wall={238,165,237,240,531};
                for(int i=0;i<ground.Length;i++)
                {
                    var terrain=new LocalTerrain{Walls=true};var n=State(wall[i],40);n.Width=n.Height=36;n.NoGravity=true;
                    float acceleration=wall[i]==237?.12f:wall[i]==531?.16f:.08f;
                    Require(NpcMotion.Step(ref n,new[]{n},1,e,terrain,1,true,out stop) && Math.Abs(n.Vx-acceleration)<.0001f,"Every locked wall member enters its actual acceleration class: "+wall[i]);
                    var identity=n.Identity;terrain.Walls=false;n.L1=0;
                    Require(NpcMotion.Step(ref n,new[]{n},1,e,terrain,1,true,out stop) && n.EffectiveType==ground[i] && n.Style==3 && n.Width==50 && n.Height==20 && n.L1==12 && n.Identity.Equals(identity),"Wall loss preserves the family's ground form and real identity: "+wall[i]);
                    n.Vy=0;n.L1=0;terrain.Walls=true;
                    Require(NpcMotion.Step(ref n,new[]{n},1,e,terrain,1,true,out stop) && n.EffectiveType==wall[i] && n.Style==40 && n.L1==12,"Ground attachment returns to the same family, not jungle spider: "+ground[i]);
                }
            }
            if(selected==null || selected=="fish")
            {
                e.PlayerWet=true;
                foreach(int type in new[]{55,57,58,65,102,157,241,465,592,607,615,688,692})
                {
                    var n=State(type,16);n.Wet=n.NoGravity=true;n.A0=1;
                    float acceleration=type==157?.25f:type==65 || type==102 || type==692?.15f:.1f;
                    Require(NpcMotion.Step(ref n,new[]{n},1,e,new LocalTerrain(),1,true,out stop) && Math.Abs(n.Vx-acceleration)<.0001f,"Every ordinary AI16 member uses its wet parameter class: "+type);
                    if(type==55 || type==592 || type==607 || type==615 || type==688)Require(n.A0!=0,"Non-pursuing fish do not acquire the player's wet pursuit: "+type);
                    n.Wet=false;n.Vx=1;n.Vy=0;
                    Require(NpcMotion.Step(ref n,new[]{n},1,e,new LocalTerrain(),1,true,out stop),"Every ordinary AI16 member has a dry route: "+type);
                    if(type==65 || type==692)Require(Math.Abs(n.Vy-.3f)<.0001f && Math.Abs(n.Vx-.94f)<.0001f,"Dry shark has drag and gravity, not a random flop: "+type);
                }
                var leap=State(615,16);leap.NoGravity=leap.Wet=true;leap.A3=299;
                Require(!NpcMotion.Step(ref leap,new[]{leap},1,e,new LocalTerrain(),1,true,out stop) && stop==PredictionStop.RandomDecision,"615 stops at the first possible native random action, not before its ordinary swim.");
                var floatAction=State(688,16);floatAction.NoGravity=floatAction.Wet=true;floatAction.A2=1;floatAction.L0=40;
                Require(!NpcMotion.Step(ref floatAction,new[]{floatAction},1,e,new LocalTerrain(),1,true,out stop) && stop==PredictionStop.UnsupportedMechanism,"688's actual water-line float remains a separate mechanism.");
            }
            if(selected==null || selected=="slime")
            {
                e.PlayerWet=false;e.PlayerX=100;
                foreach(int type in new[]{183,81,304,667,244})
                {
                    var n=State(type,1);n.A2=1;n.A0=-1;n.A3=0;
                    Require(NpcMotion.Step(ref n,new[]{n},1,e,new LocalTerrain(),1,true,out stop) && n.Vy<0 && n.Vx<0,"Forced-active slime selects the player before the predicted jump: "+type);
                }
                var passive=State(1,1);passive.A2=1;passive.A0=-1;
                Require(NpcMotion.Step(ref passive,new[]{passive},1,e,new LocalTerrain(),1,true,out stop) && passive.Vx>0,"Daytime full-health passive slime retains its observed direction.");
                var crimson=State(183,1);crimson.A2=1;crimson.A0=-3;
                Require(NpcMotion.Step(ref crimson,new[]{crimson},1,e,new LocalTerrain(),1,true,out stop) && crimson.Vy<0,"Crimslime's extra clock increment changes the frozen pre-jump time.");
            }
            if(selected==null || selected=="ground")
            {
                e.Day=false;
                foreach(int type in new[]{26,31,73,140,167})
                {
                    var n=State(type,3);n.A3=4;n.Vx=1;
                    Require(NpcMotion.Step(ref n,new[]{n},1,e,new LocalTerrain(),1,true,out stop),"Positive ordinary fighter blocked count does not refuse all futures: "+type);
                }
                foreach(int type in new[]{110,111,206,214,215,216,291,292,293,350,379,380,381,382,409,411,424,426,466,498,499,500,501,502,503,504,505,506,520})
                {
                    var n=State(type,3);n.A2=1;n.A3=4;
                    Require(!NpcMotion.Step(ref n,new[]{n},1,e,new LocalTerrain(),1,true,out stop) && stop==PredictionStop.UnsupportedMechanism,"A real action which bypasses the shared count remains separate: "+type);
                    n.A2=0;
                    Require(NpcMotion.Step(ref n,new[]{n},1,e,new LocalTerrain(),1,true,out stop),"The same member's ordinary positive-count fallback remains available: "+type);
                }
                foreach(int type in new[]{425,471}){var n=State(type,3);n.A3=4;Require(!NpcMotion.Step(ref n,new[]{n},1,e,new LocalTerrain(),1,true,out stop),"Always-independent counter exception remains separate: "+type);}
                e.Day=true;e.Remix=true;e.PlayerX=100;var remix=State(26,3);remix.A3=4;
                Require(NpcMotion.Step(ref remix,new[]{remix},1,e,new LocalTerrain(),1,true,out stop) && remix.Direction<0,"Main.IsItDay is false in Remix, including a daytime observed clock.");
                var changed=remix;changed.CritterTurns=!remix.CritterTurns;Require(!NpcPredictionCache.Same(remix,changed),"The sampled fighter pursuit class participates in scalar equality.");
                var changedEnvironment=e;changedEnvironment.DontStarve=!e.DontStarve;Require(!e.Equals(changedEnvironment),"Fighter world predicates participate in environmental equality.");
                e.Day=false;e.Remix=false;
                var unsafePhase=State(120,3);unsafePhase.A3=180;
                Require(!NpcMotion.Step(ref unsafePhase,new[]{unsafePhase},1,e,new LocalTerrain(),1,true,out stop) && stop==PredictionStop.RandomDestination,"Chaos elemental's actual random destination remains a boundary.");
            }
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
