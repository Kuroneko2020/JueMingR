using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Optimization equivalence against the unshortened pure continuation,
    // not an independent full Player.Update oracle. PlayerPolicy/Geometry,
    // Borders and Umbrella checks retain their separate original-game proof.
    internal static class NativeCombatPlayerFixedPointChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private delegate bool Advance(ref PredictionPlayerMotion p,PredictionEnvironment e,IPredictionTerrain terrain,out PredictionStop stop);
        private sealed class Pair
        {
            internal PredictionPlayerMotion Actual,Reference;
            internal PredictionEnvironment Environment;
            internal readonly RollingNpcPrediction Rolling=new RollingNpcPrediction();
            internal readonly IPredictionTerrain ActualTerrain,ReferenceTerrain;
            private readonly Advance advance;
            private readonly string label;
            private readonly List<string> failures;
            private int step;
            internal Pair(Type terrainType,PredictionPlayerMotion p,PredictionEnvironment e,string name,List<string> errors)
            {
                Actual=Reference=p;Environment=e;label=name;failures=errors;
                ActualTerrain=(IPredictionTerrain)Activator.CreateInstance(terrainType,true);
                ReferenceTerrain=(IPredictionTerrain)Activator.CreateInstance(terrainType,true);
                ActualTerrain.Reset();ReferenceTerrain.Reset();
                var method=typeof(RollingNpcPrediction).GetMethod("AdvancePlayer",Flags);
                Require(method!=null,"fixed point checks have the actual rolling consumer");
                // Normal API invocation has its required ref/out signature;
                // there are no Harmony observers or business-argument bindings.
                advance=(Advance)Delegate.CreateDelegate(typeof(Advance),Rolling,method);
            }
            internal void Next(PredictionStop expected=PredictionStop.None)
            {
                PredictionStop actualStop,referenceStop;
                bool actual=advance(ref Actual,Environment,ActualTerrain,out actualStop);
                bool reference=PlayerMotionContinuation.Advance(ref Reference,Environment,ReferenceTerrain,out referenceStop);
                Require(referenceStop==expected && reference==(expected==PredictionStop.None),"unshortened fixture boundary "+label+" step="+step+" stop="+referenceStop);
                if(actual!=reference || actualStop!=referenceStop || !Actual.SameSample(Reference))
                {
                    string error=label+" step="+step+" success="+actual+"/"+reference+" stop="+actualStop+"/"+referenceStop+" first="+Difference(Actual,Reference);
                    failures.Add(error);Console.WriteLine("FIXED-POINT-MISMATCH "+error);
                }
                step++;
            }
            internal void Steps(int count){for(int i=0;i<count;i++)Next();}
            internal void FreshSample(long tick)
            {
                // A stationary modeled actor isolates the real Prepare reset
                // without adding a second player solver or a new terrain API.
                var npc=new NpcMotionState{Identity=new NpcIdentity(1,new object(),0,0,488,488),X=900,Y=800,Width=20,Height=20,Active=true,CanReceive=true,Life=10,LifeMax=10,NoGravity=true,NoTileCollide=true,Health=new NpcHealthState{Immortal=true,DontTakeDamage=true,LavaImmune=true,ShimmerImmune=true}};
                var path=Rolling.Prepare(new[]{npc},1,0,tick,1,tick,Environment,Actual,ActualTerrain);
                Require(path!=null && path.Count==2,"fresh actual Prepare owns a new full terrain/player sample");
                ReferenceTerrain.Reset();
            }
        }
        private sealed class Map:IDisposable
        {
            private readonly Tile[] prior=new Tile[14*15];
            private readonly bool solid=Main.tileSolid[1],platformSolid=Main.tileSolid[19],platformTop=Main.tileSolidTop[19];
            internal Map()
            {
                Require(Main.maxTilesX>69 && Main.maxTilesY>65,"fixed point fixture fits the existing isolated world");
                int i=0;for(int x=55;x<=68;x++)for(int y=50;y<=64;y++)prior[i++]=Main.tile[x,y];
                Main.tileSolid[1]=Main.tileSolid[19]=Main.tileSolidTop[19]=true;Layout();
            }
            internal void Layout(bool wet=false,bool wall=false,bool platform=false,bool slope=false)
            {
                for(int x=55;x<=68;x++)for(int y=50;y<=64;y++)
                {
                    var tile=new Tile();Main.tile[x,y]=tile;
                    if(y==60){tile.active(true);tile.type=(ushort)(platform?19:1);tile.frameY=0;}
                    if(wet && y>=56 && y<60){tile.liquid=255;tile.liquidType(0);}
                    if(wall && x==62 && y<60){tile.active(true);tile.type=1;tile.liquid=0;}
                }
                if(slope)Main.tile[60,60].slope(1);
            }
            public void Dispose()
            {
                int i=0;for(int x=55;x<=68;x++)for(int y=50;y<=64;y++)Main.tile[x,y]=prior[i++];
                Main.tileSolid[1]=solid;Main.tileSolid[19]=platformSolid;Main.tileSolidTop[19]=platformTop;
            }
        }
        internal static void Run(object context)
        {
            var source=Get(Get(context,"CombatObservation"),"Prediction");var terrainType=Get(source,"Terrain").GetType();var read=source.GetType().GetMethod("ReadPlayer",Flags);
            var environment=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,WorldSurface=(float)Main.worldSurface};
            var failures=new List<string>();int cases=0;
            Console.WriteLine("FIXED-POINT actual Features MVID="+typeof(RollingNpcPrediction).Assembly.ManifestModule.ModuleVersionId+"; reference=unshortened PlayerMotionContinuation, not full native update; no performance admission");
            using(var map=new Map())
            {
                // Each timer is independent so g12's idle XY/V shortcut cannot
                // hide one frozen field behind another field's invalidation.
                foreach(string timer in new[]{"WetSlime","SwimTime","GfxOffset"})
                {
                    bool wet=timer=="SwimTime";map.Layout(wet);
                    var p=Sample(read,wet);if(timer=="WetSlime")p.WetSlime=2;if(timer=="SwimTime")p.SwimTime=3;if(timer=="GfxOffset"){p.GfxOffset=4;p.StepSpeed=1;}
                    var pair=new Pair(terrainType,p,environment,timer,failures);pair.Steps(5);
                    Require(pair.Reference.WetSlime==0 && pair.Reference.SwimTime==0 && pair.Reference.GfxOffset==0,"timer reference actually reaches its zero boundary: "+timer);cases++;
                }
                map.Layout(wall:true);var blocked=Sample(read);blocked.X=972;blocked.Right=true;
                var held=new Pair(terrainType,blocked,environment,"pressed blocked",failures);held.Steps(8);
                Require(held.Reference.X==972 && held.Reference.Vx==0,"real stone wall stops held horizontal acceleration");cases++;
                held.Actual.HoldJump=held.Reference.HoldJump=true;held.Next();
                Require(held.Reference.Vy<0 && !held.Reference.ReleaseJump,"held jump leaves the proved grounded input");
                held.Actual.HoldJump=held.Reference.HoldJump=false;held.Next();
                Require(held.Reference.ReleaseJump && held.Reference.Jump==0,"release retains its ordinary jump clock");cases++;

                map.Layout();var boost=new Pair(terrainType,Sample(read),environment,"track boost",failures);boost.Steps(2);
                boost.Actual.TrackBoost=boost.Reference.TrackBoost=.75f;boost.Next();
                Require(boost.Reference.TrackBoost==0 && boost.Reference.Vx>0,"track boost is consumed once after the stable input changes");cases++;

                var gravity=new Pair(terrainType,Sample(read),environment,"gravity prefix",failures);gravity.Steps(2);
                gravity.Actual.ForcedGravityTicks=gravity.Reference.ForcedGravityTicks=2;gravity.Environment.GravitySourceCaptured=true;
                gravity.Next();gravity.Next();
                Require(gravity.Reference.ForcedGravityTicks==0 && gravity.Reference.GravityDirection==1 && gravity.Reference.GravityAction==4,"forced clock, inversion and return run before every physical decision");cases++;

                var border=new Pair(terrainType,Sample(read),environment,"border tail",failures);border.Steps(2);
                var b=border.Reference;b.BordersNoLimits=false;b.BordersCaptured=true;b.BorderLeft=0;b.BorderRight=1590;b.BorderTop=0;b.BorderBottom=Main.bottomWorld;border.Actual=border.Reference=b;border.Next();
                Require(border.Reference.X==930 && border.Reference.Vx==0,"border clamp remains outside the reused physical kernel");cases++;

                var umbrella=new Pair(terrainType,Sample(read),environment,"umbrella item tail",failures);umbrella.Steps(2);
                umbrella.Actual.UmbrellaHold=umbrella.Reference.UmbrellaHold=true;umbrella.Actual.UmbrellaAnimation=umbrella.Reference.UmbrellaAnimation=4;
                umbrella.Steps(3);Require(umbrella.Reference.UmbrellaAnimation==1,"item tail advances on every stable body step");cases++;

                var missing=new Pair(terrainType,Sample(read),environment,"wall prefix missing",failures);missing.Steps(2);
                missing.Actual.WallSourceExpected=missing.Reference.WallSourceExpected=true;missing.Next(PredictionStop.MissingDependency);cases++;

                map.Layout(platform:true);var platformPair=new Pair(terrainType,Sample(read),environment,"platform Down",failures);platformPair.Steps(2);
                platformPair.Actual.Down=platformPair.Reference.Down=true;platformPair.Steps(2);
                Require(platformPair.Reference.Y>928,"Down changes actual platform collision instead of reusing a ground contact");cases++;
                map.Layout(slope:true);var slopePair=new Pair(terrainType,Sample(read),environment,"slope state",failures);slopePair.Steps(4);cases++;

                foreach(bool wet in new[]{false,true})
                {
                    map.Layout();var fresh=new Pair(terrainType,Sample(read),environment,wet?"fresh liquid sample":"fresh removed floor",failures);fresh.Steps(3);
                    map.Layout(wet);if(!wet)for(int x=55;x<=68;x++)Main.tile[x,60].active(false);
                    fresh.FreshSample(wet?102:101);fresh.Next();
                    Require(wet?fresh.Reference.Wet:fresh.Reference.Y>928,"fresh Prepare reads the changed liquid/terrain premise");cases++;
                }
            }
            Require(failures.Count==0,"actual rolling fixed point differs from unshortened continuation in "+failures.Count+" actions; first="+(failures.Count==0?"none":failures[0]));
            Console.WriteLine("PASS fixed point pure-continuation equivalence mechanism leaves="+cases+"; original geometry oracle remains separate; no FPS/cost claim");
        }
        private static PredictionPlayerMotion Sample(MethodInfo read,bool wet=false)
        {
            var p=new Player{whoAmI=1,active=true,width=20,height=32,position=new Vector2(961,928),gravity=.4f,gravDir=1,releaseJump=true,maxRunSpeed=3,accRunSpeed=6,runAcceleration=.08f,runSlowdown=.2f,wet=wet,carpetFrame=-1};
            var motion=(PredictionPlayerMotion)read.Invoke(null,new object[]{p});
            Require(!motion.Complex && !motion.Hover && !motion.UnsupportedGeometry,"fixed point body fixtures use the ordinary physical kernel");
            // Scalar body fixtures deliberately avoid unrelated world borders;
            // the separate border leaf below supplies its actual edge facts.
            motion.BordersNoLimits=true;return motion;
        }
        private static string Difference(PredictionPlayerMotion a,PredictionPlayerMotion b)
        {
            foreach(var f in typeof(PredictionPlayerMotion).GetFields(BindingFlags.Instance|BindingFlags.Public))
            {object x=f.GetValue(a),y=f.GetValue(b);bool same=f.Name=="PlayerToken"?ReferenceEquals(x,y):f.FieldType==typeof(float)?(float)x==(float)y:Equals(x,y);if(!same)return f.Name+"="+x+"/"+y;}
            return "<none>";
        }
    }
}
