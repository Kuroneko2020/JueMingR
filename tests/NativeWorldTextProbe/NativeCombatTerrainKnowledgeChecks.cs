using System;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using JueMingR.Platform.WorldTargets;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Uses the original section ACK owner, independently of Tile allocation.
    // The same existing Tile/lease APIs run against the old and repaired host.
    internal static class NativeCombatTerrainKnowledgeChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static int sectionReads;
        private static void SectionRead(){sectionReads++;}
        internal static void Run(object context)
        {
            var tiles=Main.tile;var sections=Main.sectionManager;var connection=Netplay.Connection;
            var world=Main.ActiveWorldFileData;int worldId=Main.worldID;
            var audit=new Harmony("JueMingR.Tests.TerrainKnowledgeCost");audit.Patch(typeof(WorldSections).GetMethod("SectionLoaded",Flags),prefix:new HarmonyMethod(typeof(NativeCombatTerrainKnowledgeChecks),nameof(SectionRead)));
            int mode=Main.netMode,width=Main.maxTilesX,height=Main.maxTilesY;
            try
            {
                Main.maxTilesX=420;Main.maxTilesY=200;Main.tile=new Tile[420,200];
                for(int x=0;x<420;x++)for(int y=0;y<200;y++)Main.tile[x,y]=new Tile();
                Main.netMode=1;Main.sectionManager=new WorldSections(3,2);
                var host=Get(context,"Combat").GetType().Assembly;
                var terrain=(IPredictionTerrain)Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);
                PredictionTile cell;PredictionStop stop;
                using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))
                {
                    terrain.Reset();Main.tile[100,50]=null;
                    Require(!terrain.Tile(100,50,out cell,out stop) && stop==PredictionStop.TerrainUnavailable,"null terrain is unavailable");
                    // This is exactly the original background's new Tile():
                    // it does not set WorldSections' independent loaded bit.
                    Main.tile[100,50]=new Tile();terrain.Reset();
                    Require(!Main.sectionManager.TileLoaded(100,50),"fixture has no section ACK");
                    Require(!terrain.Tile(100,50,out cell,out stop) && stop==PredictionStop.TerrainUnavailable,"PRED-01: nonnull unacknowledged client terrain must remain unavailable");
                    Main.sectionManager.SetSectionLoaded(0,0);
                    terrain.Reset();Require(terrain.Tile(100,50,out cell,out stop) && !cell.Active && cell.Liquid==0,"ACK makes actual loaded air usable");
                    var fresh=(IPredictionTerrain)Activator.CreateInstance(terrain.GetType(),true);Require(fresh.Tile(100,50,out cell,out stop),"a newly constructed terrain opens its first empty read sample");
                    int cold=(int)Get(terrain,"Reads");sectionReads=0;for(int i=0;i<128;i++)Require(terrain.Tile(100,50,out cell,out stop),"hot known-air tile remains usable");
                    Require((int)Get(terrain,"Reads")==cold && sectionReads<=128,"hot cached reads keep bounded O(1) ACK guards without recapturing tile values");
                    var lease=Call(terrain,"FreezeCells");Require((bool)Get(lease,"Current"),"loaded air lease starts current");
                    terrain.Tile(100,50,out cell,out stop); // hot-cache path
                    Main.sectionManager=new WorldSections(3,2);
                    Require(!terrain.Tile(100,50,out cell,out stop) && stop==PredictionStop.TerrainUnavailable,"hot cached cells cannot outlive their network source");
                    Require(!(bool)Get(lease,"Current") && !(bool)Call(lease,"DisplayCurrent",true) && !(bool)Call(terrain,"BeginContinuation",lease),"ordinary/display/continuation leases reject the replaced unacknowledged section owner");
                    Main.sectionManager.SetAllSectionsLoaded();terrain.Reset();
                    Require(terrain.Tile(100,50,out cell,out stop),"new ACK recovers a fresh client sample");
                    // Native ACK storage is controlled only in this isolated
                    // fixture, retaining the same WorldSections identity so a
                    // hot proof cannot be protected by a pointer change alone.
                    var flags=(BitsByte[])typeof(WorldSections).GetField("data",Flags).GetValue(Main.sectionManager);
                    flags[0][0]=false;Require(!terrain.Tile(100,50,out cell,out stop) && stop==PredictionStop.TerrainUnavailable,"same-source ACK loss invalidates the hot cache");Main.sectionManager.SetSectionLoaded(0,0);
                    terrain.Reset();var wet=new object[]{1600f,800f,20,20,false,(byte)0,false};Require((bool)Call(terrain,"ProjectileWet",wet),"dry region obtains its complete cell proof");cold=(int)Get(terrain,"Reads");sectionReads=0;
                    Require((bool)Call(terrain,"ProjectileWet",wet) && !(bool)wet[4] && (int)Get(terrain,"Reads")==cold && sectionReads<=4,"warm dry region verifies only intersected sections and retains its cell-cache benefit");
                    flags[0][0]=false;Require(!(bool)Call(terrain,"ProjectileWet",wet),"warm dry proof cannot turn unknown water into zero liquid");Main.sectionManager.SetSectionLoaded(0,0);terrain.Reset();Require(terrain.Tile(100,50,out cell,out stop),"known air recovers after an explicit sample reset");
                    lease=Call(terrain,"FreezeCells");var original=Main.tile;Main.tile=(Tile[,])original.Clone();
                    Require(!(bool)Get(lease,"Current"),"same-size same-byte different world tile array retires a lease");Main.tile=original;
                    terrain.Reset();terrain.Tile(100,50,out cell,out stop);lease=Call(terrain,"FreezeCells");Main.ActiveWorldFileData=new Terraria.IO.WorldFileData{WorldId=worldId};Require(!(bool)Get(lease,"Current"),"same-byte same-ID world file identity replacement retires a lease");Main.ActiveWorldFileData=world;
                    // In .8 Main.worldID is a getter over this original owner.
                    // Preserve the file-reference counterexample above and
                    // separately check a numeric identity change in that file.
                    terrain.Reset();terrain.Tile(100,50,out cell,out stop);lease=Call(terrain,"FreezeCells");world.WorldId=worldId==1?2:1;Require(!(bool)Get(lease,"Current"),"same-file world ID change retires a lease");world.WorldId=worldId;
                    var client=new RemoteServer();Netplay.Connection=client;terrain.Reset();terrain.Tile(100,50,out cell,out stop);lease=Call(terrain,"FreezeCells");var socket=client.Socket;client.Socket=new Terraria.Net.Sockets.TcpSocket();Require(!(bool)Get(lease,"Current"),"same-connection socket replacement retires a client lease");client.Socket.Close();client.Socket=socket;
                    var native=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeTerrainSnapshot",true);
                    var capture=native.GetMethod("Capture",Flags);var snapshot=capture.Invoke(null,new object[]{1L,96,32,127,63});
                    Require((bool)Call(snapshot,"IsCurrent",1L),"acknowledged native chunks start current");
                    var pass=Activator.CreateInstance(native.GetNestedType("Comparison",Flags),true);
                    Require((bool)Call(snapshot,"IsCurrentObserved",1L,pass) && ((System.Collections.IDictionary)Get(pass,"Matched")).Count>0,"complete native comparison obtains its bounded matched-chunk proof");
                    flags=(BitsByte[])typeof(WorldSections).GetField("data",Flags).GetValue(Main.sectionManager);flags[0][0]=false;
                    Require(!(bool)Call(snapshot,"IsCurrentObserved",1L,pass) && ((System.Collections.IDictionary)Get(pass,"Matched")).Count==0,"same-owner unknown section revokes a matched-chunk proof before its fast reuse");Main.sectionManager.SetSectionLoaded(0,0);
                    Require((bool)Call(snapshot,"IsCurrentObserved",1L,pass),"ACK permits a newly checked complete native chunk again");
                    Main.sectionManager=new WorldSections(3,2);
                    Require(!(bool)Call(snapshot,"IsCurrent",1L),"native currentness cannot reuse nonnull unacknowledged chunks");
                    bool refused=false;try{capture.Invoke(null,new object[]{1L,96,32,127,63});}catch(TargetInvocationException error){refused=error.InnerException is System.IO.InvalidDataException;}
                    Require(refused,"native capture refuses unknown network terrain");
                    Main.netMode=0;Main.sectionManager=null;terrain.Reset();
                    Require(terrain.Tile(100,50,out cell,out stop) && !cell.Active,"single player needs no network section owner");
                    WorldCache(host);
                    Console.WriteLine("PASS terrain knowledge: null/non-null-unACK/ACK-air/hot/lease/display/continuation/world/native/single-player");
                }
            }
            finally{audit.UnpatchAll(audit.Id);world.WorldId=worldId;Main.ActiveWorldFileData=world;Main.tile=tiles;Main.sectionManager=sections;Netplay.Connection=connection;Main.netMode=mode;Main.maxTilesX=width;Main.maxTilesY=height;}
        }
        private static WorldTargetTile WorldRead(object owner)
        {return (WorldTargetTile)Call(owner,"Read",101,50);}
        private static WorldTargetTile WorldCurrent(Type type)
        {return (WorldTargetTile)type.GetMethod("ReadCurrent",Flags).Invoke(null,new object[]{101,50});}
        private static void WorldCache(Assembly host)
        {
            var tiles=Main.tile;var tile=tiles[101,50];var sections=Main.sectionManager;var connection=Netplay.Connection;var world=Main.ActiveWorldFileData;int mode=Main.netMode;
            var client=new RemoteServer();var firstSocket=client.Socket;var secondSocket=new Terraria.Net.Sockets.TcpSocket();
            try
            {
                Main.netMode=1;Netplay.Connection=client;Main.sectionManager=new WorldSections(3,2);Main.sectionManager.SetSectionLoaded(0,0);tiles[101,50]=new Tile();
                var type=host.GetType("JueMingR.TerrariaHost.World.WorldTileObservation",true);
                var owner=Activator.CreateInstance(type,Flags,null,new object[]{(Func<bool>)(()=>true)},null);
                var current=WorldCurrent(type);var cold=WorldRead(owner);
                Require(current.Readable && !current.Active && cold.Readable && !cold.Active,"actual shared world readers admit ACKed air");
                long key=((long)101<<32)|50;
                Require(((System.Collections.IDictionary)Get(owner,"cache")).Contains(key) && WorldRead(owner).Readable,"same-epoch actual world Read reaches its populated cache");
                // Keep the physical section owner and the warm entry. Native
                // ACK loss must be checked before returning that cached value.
                var flags=(BitsByte[])typeof(WorldSections).GetField("data",Flags).GetValue(Main.sectionManager);flags[0][0]=false;
                Require(!WorldRead(owner).Readable && !WorldCurrent(type).Readable,"same-owner ACK loss makes cached Read and direct ReadCurrent unavailable");
                Main.sectionManager.SetSectionLoaded(0,0);Require(WorldRead(owner).Readable && WorldCurrent(type).Readable,"actual shared world reads recover when ACK returns");
                // A new physical source can expose different data even with
                // identical dimensions, coordinates and section ACK bits.
                // ReadCurrent supplies the uncached value, independently of
                // CheckIdentity and the old cached all-air value above.
                var stone=new Tile();stone.active(true);stone.type=Terraria.ID.TileID.Stone;tiles[101,50]=stone;client.Socket=secondSocket;
                current=WorldCurrent(type);var socketRead=WorldRead(owner);
                Require(current.Readable && current.Active && current.Type==Terraria.ID.TileID.Stone && socketRead.Readable && socketRead.Active && socketRead.Type==current.Type,"same-connection socket replacement withdraws old cached air before actual Read");
                var dirt=new Tile();dirt.active(true);dirt.type=Terraria.ID.TileID.Dirt;tiles[101,50]=dirt;Main.ActiveWorldFileData=new Terraria.IO.WorldFileData{WorldId=world.WorldId};
                current=WorldCurrent(type);var fileRead=WorldRead(owner);
                Require(current.Readable && current.Type==Terraria.ID.TileID.Dirt && fileRead.Readable && fileRead.Active && fileRead.Type==current.Type,"same-ID world file replacement withdraws the previous source's cached stone");
                Main.tile=(Tile[,])tiles.Clone();Main.tile[101,50]=new Tile();current=WorldCurrent(type);var arrayRead=WorldRead(owner);
                Require(current.Readable && !current.Active && arrayRead.Readable && !arrayRead.Active,"same-size tile array replacement withdraws the previous cached active cell");
                Console.WriteLine("PASS actual WorldTileObservation: cached/direct ACK-air, same-owner ACK loss/recovery, socket/file/array source replacement");
            }
            finally{Main.tile=tiles;tiles[101,50]=tile;Main.sectionManager=sections;Netplay.Connection=connection;Main.ActiveWorldFileData=world;Main.netMode=mode;((Terraria.Net.Sockets.ISocket)secondSocket).Close();firstSocket?.Close();}
        }
    }
}
