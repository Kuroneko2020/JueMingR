using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ObjectData;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Native producers determine all positive frames; source canonical samples
    // determine expectations independently of the identity resolver.
    internal static class NativeLegacyPlacementIdentityChecks
    {
        private static void PrepareNativeMetadata()
        {
            if(Main.tileFrameImportant[TileID.PlanterBox] && Main.tileSolid[TileID.PlanterBox])return;
            // BrowserCpu deliberately skips Main.Initialize. Invoke the native
            // data producers, rather than inventing material/merge flags for
            // the new legacy positive fixtures.
            foreach(string method in new[]{"Initialize_TileAndNPCData1","Initialize_TileAndNPCData2"})
                typeof(Main).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            TileObjectData.Initialize();Framing.Initialize();Main.SetupTileMerge();
        }
        private static void PrepareTrackMetadata()
        {
            // In the locked client Initialize, dedServ only gates its leading
            // MinecartMount texture-width assertion. All semantic frame/type/
            // connection tables are built by the same unconditional native
            // body. Restore the flag before PlaceTile/FrameTrack/World so their
            // client paths remain real; no fake texture or track table is used.
            bool dedicated=Main.dedServ;
            try{Main.dedServ=true;Minecart.Initialize();}
            finally{Main.dedServ=dedicated;}
        }
        internal static IEnumerable<Item> Samples(int tile) => ContentSamples.ItemsByType.Where(p => p.Key == p.Value.type && p.Value.createTile == tile).Select(p => p.Value).OrderBy(i => i.type);
        private static IEnumerable<Item> PlatformSamples() => ContentSamples.ItemsByType.Where(p=>p.Key==p.Value.type && p.Value.createTile>=0 && TileID.Sets.Platforms[p.Value.createTile]).Select(p=>p.Value).OrderBy(i=>i.type);
        internal static void Planter(Item sample)
        {
            PrepareNativeMetadata();
            for (int x = 17; x <= 23; x++) for (int y = 18; y <= 22; y++) Main.tile[x,y] = new Tile();
            Require(WorldGen.PlaceTile(20,20,TileID.PlanterBox,true,true,0,sample.placeStyle), "native canonical planter placement " + sample.type);
            WorldGen.SquareTileFrame(20,20);
        }
        internal static void PlaceSource(Item sample)
        {
            if(sample.createTile==380){Planter(sample);return;}
            PrepareNativeMetadata();
            for(int x=16;x<=24;x++)for(int y=16;y<=24;y++)Main.tile[x,y]=new Tile();
            if(sample.createTile==387 || sample.createTile==388)
            {
                if(sample.createTile==387)
                {
                    foreach(int x in new[]{19,22}){Main.tile[x,20].active(true);Main.tile[x,20].type=TileID.Dirt;}
                    Require(TileObject.Place(new TileObject{xCoord=20,yCoord=20,type=387,style=sample.placeStyle}),"native consumer trapdoor body");
                    Require(WorldGen.ShiftTrapdoor(20,20,true),"native consumer opened trapdoor");
                }
                else
                {
                    foreach(int y in new[]{19,25}){Main.tile[20,y].active(true);Main.tile[20,y].type=TileID.Dirt;}
                    Require(TileObject.Place(new TileObject{xCoord=20,yCoord=20,type=388,style=sample.placeStyle}),"native consumer tall gate body");
                    Require(WorldGen.ShiftTallGate(20,20,false),"native consumer opened tall gate");
                }
            }
            else if(sample.createTile==10)
            {
                foreach(int y in new[]{19,23}){Main.tile[20,y].active(true);Main.tile[20,y].type=TileID.Dirt;}
                Require(TileObject.Place(new TileObject{xCoord=20,yCoord=20,type=10,style=sample.placeStyle}),"native consumer door body");
                Require(WorldGen.OpenDoor(20,20,1),"native consumer open door");
            }
            else
            {
                if(sample.createTile==314)PrepareTrackMetadata();
                Require(WorldGen.PlaceTile(20,20,sample.createTile,true,true,0,sample.placeStyle),"native consumer legacy placement");
                WorldGen.SquareTileFrame(20,20);
                if(sample.createTile==19){Require(WorldGen.PlaceTile(21,20,19,true,true,0,sample.placeStyle),"native consumer joined platform");WorldGen.SquareTileFrame(20,20);}
            }
        }
        internal static void Run(Assembly assembly)
        {
            var random=Main.rand;
            try
            {
            Main.rand=new Terraria.Utilities.UnifiedRandom(81108);
            PrepareNativeMetadata();
            using (var world = new NativeNaturalNameChecks.World(assembly))
            {
                var planters = Samples(TileID.PlanterBox).ToArray();
                Require(planters.Select(i=>i.type).SequenceEqual(new[]{3215,3216,3217,3218,3219,3220,3221,3222}), "locked complete canonical planter family");
                int count=0;
                foreach (var sample in planters)
                {
                    Require(Main.tileFrameImportant[380] && !TileID.Sets.Platforms[380] && TileObjectData.GetTileData(380,sample.placeStyle)==null, "native planter legacy profile");
                    var frames=new HashSet<int>();
                    // Isolated, right neighbour, left neighbour, both neighbours.
                    foreach (int mask in new[]{0,1,2,3})
                    {
                        Planter(sample);
                        if((mask&1)!=0) Require(WorldGen.PlaceTile(19,20,380,true,true,0,sample.placeStyle), "native left planter");
                        if((mask&2)!=0) Require(WorldGen.PlaceTile(21,20,380,true,true,0,sample.placeStyle), "native right planter");
                        WorldGen.SquareTileFrame(20,20);
                        Tile tile=Main.tile[20,20]; frames.Add(tile.frameX);
                        Console.WriteLine("PLANTER-ORACLE item="+sample.type+" style="+sample.placeStyle+" mask="+mask+" frame="+tile.frameX+","+tile.frameY);
                        Assert(world,sample.type);count++;
                    }
                    Require(frames.SetEquals(new[]{0,18,36,54}), "native producers reach four planter connections");
                }
                // Explicit malformed inputs, not native positive coverage.
                Planter(planters[0]);Tile valid=(Tile)Main.tile[20,20].Clone();
                foreach (int x in new[]{-18,1,72}) {Main.tile[20,20]=(Tile)valid.Clone();Main.tile[20,20].frameX=(short)x;Require((int)Call(world.Catalog,"PlacedItem",Main.tile[20,20])==0,"invalid planter X");}
                foreach (int y in new[]{-18,1,144,short.MaxValue}) {Main.tile[20,20]=(Tile)valid.Clone();Main.tile[20,20].frameY=(short)y;Require((int)Call(world.Catalog,"PlacedItem",Main.tile[20,20])==0,"invalid planter Y");}
                Main.tile[20,20]=(Tile)valid.Clone();Main.tile[20,20].halfBrick(true);Require((int)Call(world.Catalog,"PlacedItem",Main.tile[20,20])==0,"planter half brick rejected");
                Main.tile[20,20]=(Tile)valid.Clone();Main.tile[20,20].slope(1);Require((int)Call(world.Catalog,"PlacedItem",Main.tile[20,20])==0,"planter slope rejected");
                object names=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.NativePlacementNames"),true);
                Call(names,"Capture",planters[0]);Call(names,"Capture",planters[0]);Require((int)Call(names,"Find",valid)==planters[0].type,"duplicate declaration retains unique identity");
                var conflicting=new Item();conflicting.SetDefaults(9);conflicting.createTile=380;conflicting.placeStyle=planters[0].placeStyle;Call(names,"Capture",conflicting);Require((int)Call(names,"Find",valid)==0,"different canonical claims stay ambiguous");
                Platforms(world);Doors(world);OperatedGates(world);Tracks(world);
                Console.WriteLine("PASS legacy identity: "+count+" native planter appearances, exact World(true/false), malformed and duplicate/conflicting candidate controls.");
            }
            using(var cold=new NativeNaturalNameChecks.World(assembly,false))
            {
                Item sample=Samples(380).First();Planter(sample);object frozen=cold.Observe();
                Require(GetOptional(frozen,"Placement")!=null,"cold planter retains placement demand");
                cold.Warm();Require((bool)Call(frozen,"StillMatches"),"cold frozen native planter matches");
                Require((bool)assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.NativeTargetObservation").GetMethod("ResolvePlacement",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new[]{frozen,cold.Catalog}),"cold planter resolves through shared catalogue");
                Require((int)Get(frozen,"ItemType")==sample.type,"cold/warm planter identity agrees");
                foreach(string change in new[]{"reference","type","frame","active","hidden","section"})
                {
                    Planter(sample);object value=cold.Observe();
                    if(change=="reference")Main.tile[20,20]=(Tile)Main.tile[20,20].Clone();
                    if(change=="type")Main.tile[20,20].type=0;
                    if(change=="frame")Main.tile[20,20].frameY+=18;
                    if(change=="active")Main.tile[20,20].active(false);
                    if(change=="hidden")Main.tile[20,20].invisibleBlock(true);
                    if(change=="section"){Main.netMode=1;Main.sectionManager=new Terraria.WorldSections(1,1);}
                    Require(!(bool)Call(value,"StillMatches"),"frozen planter rejects changed "+change);
                }
            }
            }
            finally{Main.rand=random;}
        }
        private static void Assert(NativeNaturalNameChecks.World world,int item,int x=20,int y=20)
        {
            Tile input=Main.tile[x,y];int actual=(int)Call(world.Catalog,"PlacedItem",input);
            Require(actual==item,"native placed identity "+item+" at "+x+","+y+" actual="+actual+" tile="+input.type+" active="+input.active()+" frame="+input.frameX+","+input.frameY+" slope="+input.slope()+" half="+input.halfBrick());
            foreach(bool announce in new[]{true,false})
            {
                object value=world.Observe(x*16+8,y*16+8,announce);
                Require((int)Get(value,"ItemType")==item && NativeNaturalNameChecks.Text(value).Contains("[i:"+item+"]"),"native World consumer identity "+item+" announce="+announce);
            }
        }
        private static void Platforms(NativeNaturalNameChecks.World world)
        {
            int count=0;
            foreach(var sample in PlatformSamples())
            {
                var root=TileObjectData.GetTileData(sample.createTile,sample.placeStyle);
                Console.WriteLine("PLATFORM-PROFILE item="+sample.type+" style="+sample.placeStyle+" alternates="+root.AlternatesCount+" random="+root.RandomStyleRange);
                for(int alternate=0;alternate<=root.AlternatesCount;alternate++)
                {
                    var data=TileObjectData.GetTileData(sample.createTile,sample.placeStyle,alternate);
                    Console.WriteLine("PLATFORM-ALTERNATE item="+sample.type+" alt="+alternate+" style="+data.Style+" dir="+data.Direction+" size="+data.Width+"x"+data.Height+" coord="+data.CoordinateFullWidth+"x"+data.CoordinateFullHeight+" horizontal="+data.StyleHorizontal+" multiplier="+data.StyleMultiplier+" wrap="+data.StyleWrapLimit+" skip="+data.StyleLineSkip+" random="+data.RandomStyleRange+" specific="+(data.SpecificRandomStyles==null?"null":string.Join(",",data.SpecificRandomStyles))+" selector="+(data.GetStyleOverride!=null)+" encoded="+data.CalculatePlacementStyle(sample.placeStyle,alternate,0));
                }
                foreach(int mask in new[]{0,1,2,3})
                {
                    for(int x=17;x<=23;x++)for(int y=18;y<=22;y++)Main.tile[x,y]=new Tile();
                    foreach(int x in new[]{20}.Concat((mask&1)!=0?new[]{19}:new int[0]).Concat((mask&2)!=0?new[]{21}:new int[0]))
                        Require(WorldGen.PlaceTile(x,20,sample.createTile,true,true,0,sample.placeStyle),"native platform placement");
                    WorldGen.SquareTileFrame(20,20);
                    Console.WriteLine("PLATFORM-ORACLE item="+sample.type+" tile="+sample.createTile+" mask="+mask+" frame="+Main.tile[20,20].frameX+","+Main.tile[20,20].frameY+" actual="+Call(world.Catalog,"PlacedItem",Main.tile[20,20]));
                    if(sample.type==ItemID.EchoPlatform)
                    {
                        // Native echo platforms are intrinsically invisible;
                        // a correct Find result does not override World vision.
                        foreach(bool announce in new[]{true,false})
                        {
                            object hidden=world.Observe(328,328,announce);
                            Require((int)Get(hidden,"ItemType")==0 && Get(hidden,"Issue").ToString()=="NotVisible","native echo platform stays hidden without echo vision");
                        }
                        world.EchoVision=true;
                        try{Assert(world,sample.type);}finally{world.EchoVision=false;}
                    }
                    else Assert(world,sample.type);
                    count++;
                }
            }
            Console.WriteLine("PASS platform finite connection identity: "+count+" native canonical connection appearances.");
        }
        private static void Doors(NativeNaturalNameChecks.World world)
        {
            Console.WriteLine("DOOR-DECLARATIONS closed="+Samples(10).Count()+" open="+Samples(11).Count());int count=0;
            foreach(var sample in Samples(10))foreach(int direction in new[]{-1,1})
            {
                for(int x=16;x<=24;x++)for(int y=16;y<=24;y++)Main.tile[x,y]=new Tile();
                foreach(int y in new[]{19,23}){Main.tile[20,y].active(true);Main.tile[20,y].type=TileID.Dirt;}
                // Native TileObject writer makes complete metadata body; native
                // operations own every subsequent pose and decoration frame.
                Require(TileObject.Place(new TileObject{xCoord=20,yCoord=20,type=10,style=sample.placeStyle}),"native door body");
                for(int y=20;y<23;y++)Assert(world,sample.type,20,y);
                if(WorldGen.IsLockedDoor(20,20)){Require(!WorldGen.OpenDoor(20,20,direction),"locked native door remains closed");continue;}
                Require(WorldGen.OpenDoor(20,20,direction),"native door opens "+sample.type);
                int left=direction<0?19:20;
                for(int x=left;x<left+2;x++)for(int y=20;y<23;y++){Assert(world,sample.type,x,y);count++;}
                Require(WorldGen.CloseDoor(20,20,true),"native door closes");
                for(int y=20;y<23;y++)Assert(world,sample.type,20,y);
            }
            Console.WriteLine("PASS native door canonical poses/cells="+count);
        }
        private static void Tracks(NativeNaturalNameChecks.World world)
        {
            PrepareTrackMetadata();int count=0;
            foreach(var sample in Samples(314))
            {
                for(int x=17;x<=23;x++)for(int y=18;y<=22;y++)Main.tile[x,y]=new Tile();
                Require(WorldGen.PlaceTile(20,20,314,true,true,0,sample.placeStyle),"native track place");
                Minecart.FrameTrack(20,20,false,true);
                Console.WriteLine("TRACK-ORACLE item="+sample.type+" style="+sample.placeStyle+" frame="+Main.tile[20,20].frameX+","+Main.tile[20,20].frameY+" nativeItem="+Minecart.GetTrackItem(Main.tile[20,20]));
                Assert(world,sample.type);count++;
                Tile valid=(Tile)Main.tile[20,20].Clone();
                foreach(int frame in new[]{-2,36,short.MaxValue}){Main.tile[20,20]=(Tile)valid.Clone();Main.tile[20,20].frameY=(short)frame;Require((int)Call(world.Catalog,"PlacedItem",Main.tile[20,20])==0,"invalid secondary track index");}
                foreach(int frame in new[]{-1,36,short.MaxValue}){Main.tile[20,20]=(Tile)valid.Clone();Main.tile[20,20].frameX=(short)frame;Require((int)Call(world.Catalog,"PlacedItem",Main.tile[20,20])==0,"invalid primary track index");}
            }
            Console.WriteLine("PASS native track producers/identities="+count);
        }
        private static void OperatedGates(NativeNaturalNameChecks.World world)
        {
            Console.WriteLine("OPERATED-DECLARATIONS trapClosed="+Samples(387).Count()+" trapOpen="+Samples(386).Count()+" gateClosed="+Samples(388).Count()+" gateOpen="+Samples(389).Count());
            foreach(var sample in Samples(387))foreach(bool above in new[]{false,true})
            {
                for(int x=16;x<=24;x++)for(int y=16;y<=26;y++)Main.tile[x,y]=new Tile();
                foreach(int x in new[]{19,22}){Main.tile[x,20].active(true);Main.tile[x,20].type=TileID.Dirt;}
                Require(TileObject.Place(new TileObject{xCoord=20,yCoord=20,type=387,style=sample.placeStyle}),"native closed trapdoor");
                for(int x=20;x<22;x++)Assert(world,sample.type,x,20);
                Require(WorldGen.ShiftTrapdoor(20,20,above),"native trapdoor opens side="+above);
                int top=above?20:19;
                for(int x=20;x<22;x++)for(int y=top;y<top+2;y++)Assert(world,sample.type,x,y);
                Require(WorldGen.ShiftTrapdoor(20,top,above),"native trapdoor closes");
                for(int x=20;x<22;x++)Assert(world,sample.type,x,20);
            }
            foreach(var sample in Samples(388))
            {
                var data=TileObjectData.GetTileData(388,sample.placeStyle);
                for(int alternate=0;alternate<=data.AlternatesCount;alternate++)
                {
                    for(int x=16;x<=24;x++)for(int y=16;y<=26;y++)Main.tile[x,y]=new Tile();
                    foreach(int y in new[]{19,25}){Main.tile[20,y].active(true);Main.tile[20,y].type=TileID.Dirt;}
                    Require(TileObject.Place(new TileObject{xCoord=20,yCoord=20,type=388,style=sample.placeStyle,alternate=alternate}),"native closed tall gate");
                    for(int y=20;y<25;y++)Assert(world,sample.type,20,y);
                    Require(WorldGen.ShiftTallGate(20,20,false),"native tall gate opens");
                    for(int y=20;y<25;y++)Assert(world,sample.type,20,y);
                    Require(WorldGen.ShiftTallGate(20,20,true),"native tall gate closes");
                    for(int y=20;y<25;y++)Assert(world,sample.type,20,y);
                }
            }
            Console.WriteLine("PASS native trapdoor both opening sides and tall gate registered alternates, complete cells and real close restoration.");
        }
    }
}
