using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatTerrainRelevanceChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static bool hold;private static object heldWorker;
        private static bool Mailbox(object __instance){return !hold || !ReferenceEquals(__instance,heldWorker);}
        internal static void Run(object context,object native,NpcPredictionCache cache,Action step,string output)
        {
            var rows=new List<string>{"case,tick,shown,captureTick,reason,tilesCompared,tilesCaptured,chunkHits"};
            object worker=Get(native,"Worker");heldWorker=worker;
            var owner=new Harmony("JueMingR.probe.terrain-mailbox");
            owner.Patch(worker.GetType().GetMethod("TryTakeResult",Flags),prefix:new HarmonyMethod(typeof(NativeCombatTerrainRelevanceChecks).GetMethod("Mailbox",Flags)));
            try
            {
                Ground(native,cache,step);
                foreach(string kind in new[]{"air-wall","platform","slope","water"})
                {
                    Ground(native,cache,step);var path=cache.Read(0);object accepted=Get(native,"accepted"),request=Get(native,"acceptedRequest"),usage=Get(accepted,"TerrainUsage");
                    int x,y;Cell(path,usage,kind,out x,out y);var tile=Main.tile[x,y];var saved=new Tile();saved.CopyFrom(tile);
                    if(kind=="air-wall")tile.active(true);
                    else if(kind=="platform")tile.inActive(true);
                    else if(kind=="slope")tile.slope(1);
                    else tile.liquid=255;
                    object terrain=Get(request,"Terrain");
                    Require(!(bool)Call(terrain,"IsCurrentRelevant",path.Identity.Session,usage,null),"Actual native read set notices the independent "+kind+" change.");
                    step();Record(rows,kind,native,cache);
                    Require(!ReferenceEquals(request,Get(native,"acceptedRequest")),"Real Host does not retain the old accepted "+kind+" request.");
                    Require(cache.Read(0)==null || cache.Read(0).CaptureTick>=Main.GameUpdateCount-1,"No old current+120 route survives "+kind+".");
                    string retirement=(string)Get(native,"Reason");double impact=0;
                    for(int future=1;future<=20;future++)
                    {
                        if(future>1)step();var actual=Main.npc[path.Identity.Slot];var predicted=path[future].Bounds;
                        impact=Math.Max(impact,Vector2.Distance(actual.position,new Vector2(predicted.X,predicted.Y)));Record(rows,kind+"-actual-"+future,native,cache);
                    }
                    Console.WriteLine("TERRAIN negative="+kind+" cell="+x+","+y+" reason="+retirement+" original-motion-delta-max="+impact);
                    if(kind=="air-wall")Require(impact>.002,"The single new wall actually changes native Zombie motion relative to the accepted future.");
                    tile.CopyFrom(saved);
                }
                Ground(native,cache,step);
                FreshRemote(native,cache,step,rows);
                Ground(native,cache,step);
                // Delay only the real reply's consumption, never forge bytes
                // or change the worker result. Two completed original updates
                // make change+return observable while this request is pending.
                hold=true;for(int i=0;i<12 && Get(native,"pending")==null;i++)step();
                var pending=Get(native,"pending");Require(pending!=null,"Transient case has a real sealed request in flight.");
                long capture=(long)Get(pending,"Tick"),rejected=(long)Get(native,"Rejected");
                int tx,ty;Cell(cache.Read(0),Get(Get(native,"accepted"),"TerrainUsage"),"air-wall",out tx,out ty);
                Main.tile[tx,ty].active(true);step();Record(rows,"transient-change",native,cache);Main.tile[tx,ty].active(false);step();Record(rows,"transient-return",native,cache);
                Require(!(bool)Get(pending,"Retired") && (bool)Call(Get(pending,"TerrainChanges"),"Contains",tx/32*128+ty/32,tx,ty),"Observed change+return is retained without cancelling unknown relevance.");
                hold=false;for(int i=0;i<30 && ReferenceEquals(pending,Get(native,"pending"));i++)step();
                Record(rows,"transient-receive",native,cache);
                Require((long)Get(native,"Rejected")>rejected && ((string)Get(native,"Reason")??"").Contains("intervening relevant terrain changed"),"Actual pending result is rejected for its intervening relevant change, even after return.");
                Require(cache.Read(0)==null || cache.Read(0).CaptureTick!=capture,"Changed in-flight capture never becomes a published window.");
                Ground(native,cache,step);var blocks=new HashSet<int>();int shown=0,blank=0,longest=0;
                Main.LocalPlayer.controlRight=true;
                for(int frame=0;frame<480;frame++)
                {
                    step();blocks.Add((int)Main.npc[16].Center.X/512);Record(rows,"moving-cross-block",native,cache);
                    if(cache.Read(0)!=null){shown++;blank=0;}else longest=Math.Max(longest,++blank);
                }
                Main.LocalPlayer.controlRight=false;
                Require(blocks.Count>1 && shown>0,"Original moving player/target really crosses native sample chunks and still consumes routes.");
                Console.WriteLine("MOVING frames=480 shown="+shown+" longest-blank="+longest+" actual-target-blocks="+blocks.Count);
                Console.WriteLine("PASS real terrain single-factor retirement / unknown air / exact remote fresh bytes / intervening change+return receive rejection");
                Require(ReferenceEquals(worker,Get(native,"Worker")) && !(bool)Get(native,"Failed"),"All terrain cases keep the same healthy worker/Session.");
            }
            finally{hold=false;heldWorker=null;owner.UnpatchAll(owner.Id);File.WriteAllLines(Path.Combine(output,"terrain-relevance.csv"),rows);}
        }
        private static void Ground(object native,NpcPredictionCache cache,Action step)
        {
            hold=false;
            NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();
            var player=Main.LocalPlayer;player.controlLeft=player.controlRight=player.controlJump=player.controlUp=player.controlDown=false;player.position=new Vector2(1100,2400-player.height);player.velocity=Vector2.Zero;
            player.immune=true;player.immuneTime=100000;player.statLife=player.statLifeMax=player.statLifeMax2=400;player.fallStart=player.fallStart2=150;player.wet=player.honeyWet=player.lavaWet=player.shimmerWet=false;
            var npc=Main.npc[16];npc.SetDefaults(3);npc.whoAmI=16;npc.active=true;npc.position=new Vector2(1500,2400-npc.height);npc.target=Main.myPlayer;npc.timeLeft=750;
            // The solid type under the air case is already observed; only its
            // active bit changes later. Platform has a real native support row.
            for(int x=40;x<110;x++){Main.tile[x,148].type=1;Main.tile[x,150].type=19;Main.tile[x,150].frameY=0;}
            var watch=Stopwatch.StartNew();do{step();}while((cache.Read(0)==null || cache.Read(0).Identity.Slot!=16) && watch.Elapsed.TotalSeconds<10);
            Require(cache.Read(0)!=null && cache.Read(0).Count==121 && Get(native,"accepted")!=null,"Original walking Zombie has an actually accepted current+120 window.");
        }
        private static void Cell(NpcTrajectory path,object usage,string kind,out int x,out int y)
        {
            Require(path!=null,"A real accepted route supplies the future region.");var future=path[20];
            x=(int)future.Bounds.CenterX/16;y=kind=="platform" || kind=="slope"?150:148;
            Require((bool)Call(usage,"Contains",x/32*128+y/32,x,y),"Candidate cell is in actual native terrain accesses, not a guessed radius: "+kind);
            if(kind=="air-wall" || kind=="water")Require(!Main.tile[x,y].active(),"Negative begins with genuinely observed air.");
        }
        private static void FreshRemote(object native,NpcPredictionCache cache,Action step,List<string> rows)
        {
            var path=cache.Read(0);object prior=Get(native,"terrain"),usage=Get(Get(native,"accepted"),"TerrainUsage");int x=132,y=104;
            Require(!(bool)Call(usage,"Contains",x/32*128+y/32,x,y),"Remote freshness cell is outside actual computation, but inside sampled bytes.");
            byte before=Value(prior,x,y,4),header=Value(prior,x,y,9);Main.tile[x,y].liquid=(byte)(before==93?94:93);
            Require(!(bool)Call(prior,"IsCurrent",path.Identity.Session) && (bool)Call(prior,"IsCurrentRelevant",path.Identity.Session,usage,null),"Exact new sampling and old route validity have distinct answers.");
            for(int i=0;i<24;i++)
            {
                step();Record(rows,"fresh-remote",native,cache);Require(cache.Read(0)!=null,"Irrelevant changed liquid never revokes the accepted consumer.");
                var current=Get(native,"terrain");if(Value(current,x,y,4)==Main.tile[x,y].liquid)
                {
                    Require(!ReferenceEquals(prior,current),"New request cannot label prior remote bytes as fresh.");
                    Console.WriteLine("FRESH remote="+x+","+y+" liquid="+before+"->"+Value(current,x,y,4)+" currentCapture="+Get(Get(native,"pending")??Get(native,"acceptedRequest"),"Tick"));
                    Main.tile[x,y].bTileHeader3^=8;
                    for(int frame=0;frame<24;frame++)
                    {
                        step();Record(rows,"fresh-checking-liquid",native,cache);Require(cache.Read(0)!=null,"Unrelated checkingLiquid does not retire the consumer.");
                        var next=Get(native,"terrain");if(Value(next,x,y,9)==Main.tile[x,y].bTileHeader3)
                        {Require(!ReferenceEquals(current,next),"checkingLiquid change forces exact new bytes, never prior references-only confirmation.");Console.WriteLine("FRESH remote="+x+","+y+" header3="+header+"->"+Value(next,x,y,9));return;}
                    }
                    throw new InvalidOperationException("New production snapshot did not acquire latest checkingLiquid byte.");
                }
            }
            throw new InvalidOperationException("New real production capture did not acquire latest remote liquid byte.");
        }
        private static byte Value(object terrain,int x,int y,int field)
        {
            foreach(var chunk in (IEnumerable)Get(terrain,"Chunks"))if((int)Get(chunk,"X")==x/32 && (int)Get(chunk,"Y")==y/32)
            {int height=Math.Min(32,(int)Get(terrain,"Height")-y/32*32);return ((byte[])Get(chunk,"Values"))[(x%32*height+y%32)*14+field];}
            throw new InvalidOperationException("Freshness cell must really be captured.");
        }
        private static void Record(List<string> rows,string name,object native,NpcPredictionCache cache)
        {var path=cache.Read(0);var costs=Get(native,"terrainComparison");rows.Add(string.Join(",",name,Main.GameUpdateCount,path!=null?1:0,path==null?-1:path.CaptureTick,"\""+((string)Get(native,"Reason")??"").Replace("\"","\"\"")+"\"",Get(costs,"TilesRead"),Get(costs,"TilesCaptured"),Get(costs,"ChunkHits")));}
        private static object Get(object owner,string name){var field=owner.GetType().GetField(name,Flags);return field!=null?field.GetValue(owner):owner.GetType().GetProperty(name,Flags).GetValue(owner);}
        private static object Call(object owner,string name,params object[] args){return owner.GetType().GetMethod(name,Flags).Invoke(owner,args);}
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
