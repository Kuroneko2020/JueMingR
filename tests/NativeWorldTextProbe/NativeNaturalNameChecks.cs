using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Utilities;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeNaturalNameChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static void Run(Assembly assembly)
        {
            string previous = Language.ActiveCulture.Name; int cases = 0;
            try
            {
                foreach (string locale in new[] { "en-US", "zh-Hans" })
                {
                    LanguageManager.Instance.SetLanguage(locale); Lang.InitializeLegacyLocalization(); MapHelperInitialize();
                    using (var world = new World(assembly))
                    {
                        // These inputs are from the locked WorldGen source and
                        // independent ContentSamples IDs, not resolver tables.
                        foreach (var row in new[] { new[] { 3,144,5 }, new[] {110,144,5}, new[] {24,144,60}, new[] {201,270,2887}, new[] {71,0,183}, new[] {61,144,331}, new[] {61,162,223}, new[] {236,0,1291} })
                        {
                            int type = row[0], frame = row[1], item = row[2];
                            Require(ContentSamples.ItemsByType.ContainsKey(item) && Lang.GetItemNameValue(item) == ContentSamples.ItemsByType[item].Name, "native sample/locale oracle exists");
                            object value = world.At(type, frame); string text = Text(value);
                            Console.WriteLine("NATURAL-ORACLE locale="+locale+" type="+type+" frame="+frame+" expected="+Lang.GetItemNameValue(item)+" actual="+text);
                            Require((int)Get(value,"ItemType")==item && text.Contains(Lang.GetItemNameValue(item)) && text.Contains("[i:"+item+"]"), "actual World names native plant identity, not grass/seed/unknown: "+type+"/"+frame); cases++;
                        }
                        int dyeStyle=0;
                        foreach(int id in Enumerable.Range(1107,8).Concat(Enumerable.Range(3385,4)))
                        {
                            // WorldGen.PlaceDye uses 34*style; its locked drop
                            // branches identify these native samples. These
                            // harvested items need not themselves place tile227.
                            var sample=ContentSamples.ItemsByType[id]; Require(sample.type==id,"native dye harvest sample exists");
                            object value=world.At(227,dyeStyle++*34);
                            Require((int)Get(value,"ItemType")==id && Text(value).Contains(Lang.GetItemNameValue(id)),"native dye sample/style/locale oracle "+id); cases++;
                        }
                        Require(Text(world.At(3,0))=="野草" && Text(world.At(73,0))=="高茎草","ordinary short/tall grass descriptions never become grass seed items");
                        // Actual WorldGen.GetTileMossColor is an independent
                        // native oracle for X22, unlike ordinary X18 plants.
                        foreach(int moss in new[]{180,181,182,183,381,534,536,539,625,627})
                        foreach(int direction in new[]{0,54,108,162}) foreach(int variant in new[]{0,18,36})
                        {
                            int frame=22*WorldGen.GetTileMossColor(moss);
                            object value=world.At(184,frame,direction+variant);
                            Console.WriteLine("MOSS-ORACLE support="+moss+" frame="+frame+","+(direction+variant)+" actual="+Text(value)+" issue="+GetOptional(value,"Issue"));
                            Require(Text(value)=="长苔藓" && (int)Get(value,"ItemType")==0,"native mixed-axis moss frame remains a normal descriptive identity");cases++;
                        }
                        var mossRandom=Main.rand;Main.rand=new UnifiedRandom(719);var mossOracle=new UnifiedRandom(719);
                        try{for(int i=0;i<24;i++)world.At(184,110,108);Require(Main.rand.Next()==mossOracle.Next(),"moss naming consumes no shared RNG");}finally{Main.rand=mossRandom;}
                        string[] colors={"蓝色野花","洋红色野花","粉色野花","红色野花","黄色野花","蓝紫色野花","白色野花"};
                        for(int i=0;i<colors.Length;i++) foreach(int style in ItemID.Sets.flowerPacketInfo[4041+i].stylesOnPurity)
                        foreach(int type in new[]{3,73})
                        {
                            object value=world.At(type,style*18); Require(Text(value)==colors[i] && (int)Get(value,"ItemType")==0,"native flowerPacketInfo style declaration describes a flower, never its packet/seed");cases++;
                        }
                        foreach(var row in new[]{Tuple.Create(24,"腐化植物"),Tuple.Create(201,"猩红植物"),Tuple.Create(61,"丛林植物"),Tuple.Create(74,"丛林高茎植物"),Tuple.Create(110,"神圣植物"),Tuple.Create(113,"神圣高茎植物"),Tuple.Create(637,"灰烬植物"),Tuple.Create(52,"藤蔓"),Tuple.Create(382,"开花藤蔓"),Tuple.Create(62,"丛林藤蔓"),Tuple.Create(115,"神圣藤蔓"),Tuple.Create(205,"猩红藤蔓"),Tuple.Create(636,"腐化藤蔓"),Tuple.Create(638,"灰烬藤蔓"),Tuple.Create(528,"蘑菇藤蔓"),Tuple.Create(184,"长苔藓"),Tuple.Create(518,"睡莲叶"),Tuple.Create(519,"香蒲"),Tuple.Create(549,"水草"),Tuple.Create(529,"海滨草"),Tuple.Create(530,"绿洲植物"),Tuple.Create(32,"腐化荆棘"),Tuple.Create(69,"丛林荆棘"),Tuple.Create(352,"猩红荆棘"),Tuple.Create(444,"蜂巢"),Tuple.Create(185,"小型天然杂物"),Tuple.Create(186,"大型天然杂物"),Tuple.Create(187,"大型天然杂物"),Tuple.Create(233,"植物丛"),Tuple.Create(647,"大型天然杂物"),Tuple.Create(648,"大型天然杂物"),Tuple.Create(649,"小型天然杂物"),Tuple.Create(650,"小型天然杂物"),Tuple.Create(651,"植物丛"),Tuple.Create(652,"植物丛")})
                        {
                            object value=world.At(row.Item1,0); Require(Text(value)==row.Item2 && (int)Get(value,"ItemType")==0,"approved finite natural description "+row.Item1); cases++;
                        }
                        foreach(int frame in new[]{108,126,180,396}) Require(Text(world.At(61,frame))=="丛林花" && (int)Get(world.At(61,frame),"ItemType")==0,"jungle flower is not its random jungle-rose drop");
                        foreach(var row in new[]{Tuple.Create(5,"Tree"),Tuple.Create(20,"Sapling"),Tuple.Create(590,"Sapling"),Tuple.Create(595,"Sapling"),Tuple.Create(615,"Sapling"),Tuple.Create(323,"PalmTree"),Tuple.Create(72,"GiantMushroom"),Tuple.Create(231,"Larva"),Tuple.Create(238,"PlanterasBulb"),Tuple.Create(488,"FallenLog"),Tuple.Create(704,"FallenLog")})
                            Require(Text(world.At(row.Item1,0))==Language.GetTextValue("MapObject."+row.Item2),"native map-object locale oracle "+row.Item1);
                        foreach(int group in Enumerable.Range(0,13)) foreach(int y in new[]{0,18,36,54,72,90})
                        {
                            if(group==2 || group==3){Require(GetOptional(world.At(165,group*54,y),"LocalMessage")!=null,"spider/hive native groups do not authorize rock/ice names");continue;}
                            bool ice=group==0 || group>=10, ceiling=y<36 || y==72;
                            string expected=ice?(ceiling?"冰锥":"冰笋"):(ceiling?"钟乳石":"石笋");
                            object value=world.At(165,group*54,y);Require(Text(value)==expected && (int)Get(value,"ItemType")==0,"locked stalactite material-group and native orientation oracle");cases++;
                        }
                        foreach(var row in new[]{new[]{3,145,0},new[]{61,163,0},new[]{227,1,0},new[]{227,408,0},new[]{227,0,18},new[]{165,-18,0},new[]{165,702,0},new[]{165,0,108},new[]{184,18,0},new[]{184,22,1},new[]{184,-22,0},new[]{184,242,0},new[]{184,22,216}})
                            Require((int)Get(world.At(row[0],row[1],row[2]),"ItemType")==0 && GetOptional(world.At(row[0],row[1],row[2]),"LocalMessage")!=null,"bad natural frame is local unknown, never rounded into a valid identity");
                        var random=Main.rand;Main.rand=new UnifiedRandom(196);var oracle=new UnifiedRandom(196);
                        try{for(int i=0;i<40;i++)world.At(61,144);Require(Main.rand.Next()==oracle.Next(),"naming never consumes gameplay RNG or predicts random spore quantity");}finally{Main.rand=random;}
                    }
                }
                Console.WriteLine("PASS natural names L2: "+cases+" native source/frame/ContentSamples/locale cases; exact flower declarations, descriptive no-ID identities, independent material/orientation cases and invalid-frame negatives. No KillTile/drop/RNG/Main loop/OS input.");
            }
            finally {LanguageManager.Instance.SetLanguage(previous);Lang.InitializeLegacyLocalization();MapHelperInitialize();}
        }
        private static void MapHelperInitialize(){Terraria.Map.MapHelper.Initialize();}
        internal static string Text(object value){return string.Join("|",(List<string>)Get(value,"Entries"));}

        // Shared bounded native world fixture, not a replacement observation
        // implementation. Only visibility endpoints are controlled; real World,
        // TileObjectData/ContentSamples/catalog and tile-entity dictionaries run.
        internal sealed class World : IDisposable
        {
            private readonly Tile[,] tiles=Main.tile; private readonly Player[] players=Main.player; private readonly NPC[] npcs=Main.npc; private readonly WorldItem[] items=Main.item;
            private readonly int width=Main.maxTilesX,height=Main.maxTilesY,mode=Main.netMode;private readonly bool map=Main.mapFullscreen;
            private readonly Terraria.WorldSections sections=Main.sectionManager;
            private readonly Harmony visibility=new Harmony("JueMingR.Tests.NaturalWorld."+Guid.NewGuid().ToString("N"));
            internal readonly object Catalog;private readonly MethodInfo observe;private long mapping;
            internal World(Assembly assembly,bool warm=true)
            {
                Main.netMode=0;Main.mapFullscreen=false;Main.maxTilesX=Main.maxTilesY=64;Main.tile=new Tile[64,64];
                for(int x=0;x<64;x++)for(int y=0;y<64;y++)Main.tile[x,y]=new Tile();
                Main.player=new Player[players.Length];for(int i=0;i<Main.player.Length;i++)Main.player[i]=new Player{position=new Vector2(800,800)};Main.player[Main.myPlayer].active=true;
                Main.npc=new NPC[npcs.Length];Main.item=new WorldItem[items.Length];
                Main.sectionManager=new Terraria.WorldSections(1,1);Main.sectionManager.SetAllSectionsLoaded();
                visibility.Patch(typeof(Lighting).GetMethod("GetColor",new[]{typeof(int),typeof(int)}),prefix:new HarmonyMethod(typeof(World).GetMethod(nameof(Light),F)));
                visibility.Patch(typeof(Main).GetMethod("ShouldShowInvisibleBlocksAndWalls",F),prefix:new HarmonyMethod(typeof(World).GetMethod(nameof(NoEcho),F)));
                Catalog=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.NativeItemCatalog"),true);Call(Catalog,"BindPlacementClock",(Func<long>)(()=>mapping));
                if(warm)Warm();
                observe=assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.NativeTargetObservation").GetMethod("World",F);
            }
            internal object At(int type,int xFrame=0,int yFrame=0)
            {var tile=new Tile{type=(ushort)type,frameX=(short)xFrame,frameY=(short)yFrame};tile.active(true);tile.fullbrightBlock(true);Main.tile[20,20]=tile;return Observe();}
            internal object Observe(float x=328,float y=328,bool announcement=true){return observe.Invoke(null,new object[]{new Vector2(x,y),Catalog,announcement});}
            internal void Warm(){for(int i=0;i<200 && !(bool)Get(Catalog,"PlacementReady");i++){mapping++;object count=GetOptional(Catalog,"PlacementReads");Call(Catalog,"StepPlacement");if(count!=null)Require((long)Get(Catalog,"PlacementReads")-(long)count<=64,"query cold placement keeps actual per-mapping budget");}Require((bool)Get(Catalog,"PlacementReady"),"real native placement catalogue warm for outcome oracle");}
            private static bool Light(ref Color __result){__result=Color.White;return false;}
            private static bool NoEcho(ref bool __result){__result=false;return false;}
            public void Dispose(){((IDisposable)Catalog).Dispose();visibility.UnpatchAll(visibility.Id);Main.tile=tiles;Main.player=players;Main.npc=npcs;Main.item=items;Main.maxTilesX=width;Main.maxTilesY=height;Main.netMode=mode;Main.mapFullscreen=map;Main.sectionManager=sections;}
        }
    }
}
