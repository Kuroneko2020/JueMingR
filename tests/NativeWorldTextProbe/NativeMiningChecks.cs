using System;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Tools;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using HarmonyLib;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeMiningChecks
    {
        internal static void Run(object context)
        {
            var host=Get(context,"Tools");var input=Get(context,"Input");var p=Main.LocalPlayer;var eligibility=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Tools.MiningEligibility").GetMethod("CanProgress",BindingFlags.Static|BindingFlags.NonPublic);
            var native=typeof(Player).GetMethod("GetPickaxeDamage",BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(var item in p.inventory)item.TurnToAir();p.inventory[0].SetDefaults(ItemID.CopperPickaxe);p.selectedItemState.Select(0);p.selectedItemState.Update();p.position=new Vector2(640,640);
            foreach(int type in Enumerable.Range(0,TileID.Count).Where(MiningRegion.Supported))foreach(int power in new[]{1,35,50,55,65,100,110,150,200,210})foreach(double surface in new[]{20d,60d})
            {
                for(int x=37;x<48;x++)for(int y=37;y<45;y++)Main.tile[x,y].ClearEverything();NativeToolsChecks.Tile(42,40,type);p.HeldItem.pick=power;Main.worldSurface=surface;
                int damage=(int)native.Invoke(p,new object[]{42,40,power,0,Main.tile[42,40]});
                bool expected=damage>0 && WorldGen.CanKillTile(42,40) && p.IsInTileInteractionRange(42,40,TileReachCheckSettings.Simple,p.HeldItem.tileBoost);
                bool actual=(bool)eligibility.Invoke(null,new object[]{p,p.HeldItem,42,40,type});Require(expected==actual,"native positive progress oracle type="+type+" power="+power+" surface="+surface);
            }
            Main.worldSurface=60;p.inventory[0].SetDefaults(ItemID.CopperPickaxe);NativeToolsChecks.Tile(42,40,6);NativeToolsChecks.SetMode(host,2,1);
            // Account achievement persistence is outside this isolated test;
            // native damage, tool timers, tile removal and drops remain real.
            var isolation=new Harmony("JueMingR.Tests.G09MiningAchievement");
            var achievement=typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod("HandleMining",BindingFlags.Static|BindingFlags.Public);
            isolation.Patch(achievement,prefix:new HarmonyMethod(typeof(NativeMiningChecks),nameof(SkipAchievement)));
            Gravity(context,input,host,p);
            NativeToolsChecks.Tile(42,40,6);NativeToolsChecks.SetMode(host,2,1);p.inventory[0].SetDefaults(ItemID.CopperPickaxe);
            Require((bool)Call(Get(host,"Mining"),"Select",p,42,40,6,false),"current pick selected real region");
            try{for(int f=0;f<160 && Main.tile[42,40].active();f++)NativeToolsChecks.Frame(context,input);}
            finally{isolation.Unpatch(achievement,HarmonyPatchType.All,isolation.Id);}
            Require(!Main.tile[42,40].active(),"native ItemCheck/PickTile accumulates low-pick progress to actual removal");
            NativeToolsChecks.SetMode(host,2,0);for(int f=0;f<40;f++)NativeToolsChecks.Frame(context,input);
            for(int x=37;x<48;x++)for(int y=34;y<65;y++)Main.tile[x,y].ClearEverything();
            for(int x=39;x<45;x++)for(int y=38;y<43;y++)NativeToolsChecks.Tile(x,y,6);
            p.hitTile=new HitTile();NativeToolsChecks.SetMode(host,2,1);Require((bool)Call(Get(host,"Mining"),"Select",p,42,40,6,false),"thirty-cell low-pick region");
            isolation.Patch(achievement,prefix:new HarmonyMethod(typeof(NativeMiningChecks),nameof(SkipAchievement)));
            try{for(int f=0;f<220 && Enumerable.Range(39,6).All(x=>Enumerable.Range(38,5).All(y=>Main.tile[x,y].active()));f++)NativeToolsChecks.Frame(context,input);}
            finally{isolation.Unpatch(achievement,HarmonyPatchType.All,isolation.Id);}
            Require(Enumerable.Range(39,6).Any(x=>Enumerable.Range(38,5).Any(y=>!Main.tile[x,y].active())),"low pick keeps one target long enough to progress despite more targets than native hit cache");
            NativeToolsChecks.SetMode(host,2,0);for(int f=0;f<40;f++)NativeToolsChecks.Frame(context,input);
            for(int x=37;x<48;x++)for(int y=34;y<65;y++)Main.tile[x,y].ClearEverything();
            foreach(int furnishing in new[]{470,475})
            {
                NativeToolsChecks.Tile(42,40,6);NativeToolsChecks.Tile(42,39,furnishing);p.HeldItem.pick=210;
                p.PickTile(42,40,210);
                Require(Main.tile[42,40].active() && WorldGen.CheckTileBreakability(42,40)==2,"real native protected furnishing keeps supporting ore");
                Require(!(bool)eligibility.Invoke(null,new object[]{p,p.HeldItem,42,40,6}),"protected furnishing support must be red and never scheduled");Main.tile[42,39].ClearEverything();
            }
            NativeToolsChecks.Tile(42,40,22);var clinger=Main.npc[2];clinger.SetDefaults(101);clinger.whoAmI=2;clinger.active=true;clinger.ai[0]=42;clinger.ai[1]=40;clinger.position=new Vector2(672,620);p.HeldItem.pick=210;
            Terraria.GameContent.FixExploitManEaters.Update();clinger.AI();Require(Terraria.GameContent.FixExploitManEaters.SpotProtected(42,40),"real clinger AI protects supported demonite anchor");p.PickTile(42,40,210);
            Require(Main.tile[42,40].active() && !(bool)eligibility.Invoke(null,new object[]{p,p.HeldItem,42,40,22}),"real protected anchor stays intact and must be red");
            clinger.active=false;Terraria.GameContent.FixExploitManEaters.Update();Require((bool)eligibility.Invoke(null,new object[]{p,p.HeldItem,42,40,22}),"cleared native anchor protection restores progress eligibility");
            foreach(int id in new[]{388,579,990,1294,2798})
            {
                foreach(var projectile in Main.projectile)projectile.active=false;for(int x=37;x<48;x++)for(int y=37;y<45;y++)Main.tile[x,y].ClearEverything();
                p.inventory[0].SetDefaults(id);p.itemAnimation=p.itemTime=p.toolTime=0;p.selectedItemState.Select(0);p.selectedItemState.Update();p.direction=1;
                NativeToolsChecks.Tile(42,40,6);NativeToolsChecks.SetMode(host,2,1);Require((bool)Call(Get(host,"Mining"),"Select",p,42,40,6,false),"drill/combined tool selected "+id);
                isolation.Patch(achievement,prefix:new HarmonyMethod(typeof(NativeMiningChecks),nameof(SkipAchievement)));
                try
                {
                    for(int f=0;f<100 && Main.tile[42,40].active();f++)
                    {
                        NativeToolsChecks.Frame(context,input);int mx=Main.mouseX,my=Main.mouseY,tx=Player.tileTargetX,ty=Player.tileTargetY;
                        foreach(var projectile in Main.projectile.Where(q=>q.active && q.owner==0 && (q.aiStyle==20 || q.type==445)))
                        {projectile.AI();if(projectile.active)Require(projectile.velocity.X>0,"owned drill AI retains submitted rightward target "+id);}
                        Require(Main.mouseX==mx && Main.mouseY==my && Player.tileTargetX==tx && Player.tileTargetY==ty,"projectile aim lease restores actual cursor/tile target");
                    }
                }
                finally{isolation.Unpatch(achievement,HarmonyPatchType.All,isolation.Id);}
                Require(!Main.tile[42,40].active(),"actual drill/compound native removal "+id);NativeToolsChecks.SetMode(host,2,0);
                for(int f=0;f<40;f++){NativeToolsChecks.Frame(context,input);foreach(var projectile in Main.projectile.Where(q=>q.active && q.owner==0 && (q.aiStyle==20 || q.type==445)))projectile.AI();}
                Require(!Main.projectile.Any(q=>q.active && q.owner==0 && (q.aiStyle==20 || q.type==445)),"native channel exits without forcing projectile or animation rollback");
            }
            ManualHandoff(context,input,host,p,achievement,isolation);
            object mining=Get(host,"Mining");p.inventory[0].SetDefaults(1294);p.selectedItemState.Select(0);p.selectedItemState.Update();
            for(int x=37;x<48;x++)for(int y=34;y<65;y++)Main.tile[x,y].ClearEverything();NativeToolsChecks.Tile(42,40,6);NativeToolsChecks.Tile(45,40,6);NativeToolsChecks.SetMode(host,2,2);
            isolation.Patch(achievement,prefix:new HarmonyMethod(typeof(NativeMiningChecks),nameof(SkipAchievement)));
            try
            {
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft=true;p.PickTile(42,40,210);
                Require(((MiningRegion)Get(mining,"Region")).Count==0,"foreign PickTile, including pet consumers, cannot establish manual vein even while attack is held");
                NativeToolsChecks.Tile(42,40,6);NativeQuickItemChecks.Sample(input,new Microsoft.Xna.Framework.Input.Keys[0]);Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft=true;Main.mouseLeft=true;Main.mouseX=680;Main.mouseY=648;Player.tileTargetX=42;Player.tileTargetY=40;
                p.itemAnimation=p.itemTime=p.toolTime=0;NativeQuickItemChecks.NativeFrame(p);Call(context,"UpdateRuntime");
                Require(!Main.tile[42,40].active() && ((MiningRegion)Get(mining,"Region")).Count==1 && ((MiningRegion)Get(mining,"Region"))[0].X==45,"actual physical held-tool first removal selects adjacent remainder");
            }
            finally{isolation.Unpatch(achievement,HarmonyPatchType.All,isolation.Id);Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft=false;Main.mouseLeft=false;}
            NativeToolsChecks.SetMode(host,2,0);for(int f=0;f<40;f++)NativeToolsChecks.Frame(context,input);
            for(int x=37;x<48;x++)for(int y=34;y<65;y++)Main.tile[x,y].ClearEverything();
            NativeToolsChecks.Tile(41,38,123);NativeToolsChecks.Tile(41,39,123);NativeToolsChecks.Tile(41,42,123);NativeToolsChecks.SetMode(host,2,1);
            Require((bool)Call(mining,"Select",p,41,38,123,false),"gravity observation fixture selected");
            Main.tile[41,38].ClearEverything();Main.tile[41,39].ClearEverything();NativeToolsChecks.Tile(41,44,123);NativeToolsChecks.Tile(41,45,123);Call(mining,"Update");
            var region=(MiningRegion)Get(mining,"Region");Require(region.Count==3 && Enumerable.Range(0,region.Count).Select(i=>region[i].Y).OrderBy(y=>y).SequenceEqual(new[]{42,44,45}),"multiple disappeared selected members retain separate bounded falling opportunities and ignore pre-existing lower cells");
            var unreadable=Main.tile[41,44];Main.tile[41,44]=null;Call(mining,"Update");Require(region.Count==3,"unreadable gravity member remains unknown, not removed");Main.tile[41,44]=unreadable;NativeToolsChecks.SetMode(host,2,0);
            for(int x=37;x<48;x++)for(int y=34;y<65;y++)Main.tile[x,y].ClearEverything();NativeToolsChecks.Tile(41,38,123);NativeToolsChecks.Tile(41,39,123);NativeToolsChecks.Tile(41,44,0);NativeToolsChecks.SetMode(host,2,1);Require((bool)Call(mining,"Select",p,41,38,123,false),"dirt replacement evidence fixture");
            Main.tile[41,38].ClearEverything();NativeToolsChecks.Tile(41,39,0);Call(mining,"Update");NativeToolsChecks.Tile(41,39,123);NativeToolsChecks.Tile(41,44,123);Call(mining,"Update");Require(((MiningRegion)Get(mining,"Region")).Count==0,"active Dirt is occupied, never proof of air for prior or replaced selected cells");NativeToolsChecks.SetMode(host,2,0);
            GravityCapacity(context,host,mining,p);
            Console.WriteLine("PASS G09 mining: all 37 materials x ten powers x two depths and actual low-power native removal.");
            Console.WriteLine("PASS G09 held-tool causal first hit, foreign PickTile exclusion and bounded multiple-member gravity observations (physics not simulated).");
        }
        private static bool SkipAchievement(){return false;}
        private static void GravityCapacity(object context,object host,object mining,Player p)
        {
            for(int x=25;x<67;x++)for(int y=25;y<66;y++)Main.tile[x,y].ClearEverything();
            for(int x=30;x<62;x++)for(int y=30;y<46;y++)NativeToolsChecks.Tile(x,y,123);NativeToolsChecks.SetMode(host,2,1);Require((bool)Call(mining,"Select",p,42,40,123,false) && ((MiningRegion)Get(mining,"Region")).Count==512,"full 512-member gravity observation region");
            for(int x=30;x<62;x++)for(int y=30;y<46;y++)Main.tile[x,y].ClearEverything();Call(context,"UpdateRuntime");Require((int)Get(mining,"falls")==512,"all observed disappeared members receive bounded witnesses");
            for(int f=0;f<25;f++)Call(context,"UpdateRuntime");for(int x=30;x<62;x++)NativeToolsChecks.Tile(x,47,123);
            for(int f=0;f<105;f++)Call(context,"UpdateRuntime");
            Require(((MiningRegion)Get(mining,"Region")).Count==32 && (int)Get(mining,"falls")>0,"full queue observes all delayed lower columns before its bounded observation window closes");
            for(int f=0;f<75;f++)Call(context,"UpdateRuntime");Require((int)Get(mining,"falls")==0,"expired full queue retires with bounded eight-per-update cleanup");NativeToolsChecks.SetMode(host,2,0);
            for(int x=25;x<67;x++)for(int y=25;y<66;y++)Main.tile[x,y].ClearEverything();
            Console.WriteLine("PASS G09 bounded gravity observation workload: 512 vanished cells, delayed columns, two-round opportunity and finite retirement (separate from native physics cases).");
        }
        private static void ManualHandoff(object context,object input,object host,Player p,MethodInfo achievement,Harmony isolation)
        {
            for(int x=35;x<49;x++)for(int y=34;y<65;y++)Main.tile[x,y].ClearEverything();
            p.inventory[0].SetDefaults(1294);p.itemAnimation=p.itemTime=p.toolTime=0;NativeToolsChecks.SetMode(host,2,2);
            NativeToolsChecks.Tile(38,40,6);NativeToolsChecks.Tile(45,40,6);NativeToolsChecks.Tile(45,37,6);
            var mining=Get(host,"Mining");Require((bool)Call(mining,"Select",p,38,40,6,false),"old automatic vein selected");
            isolation.Patch(achievement,prefix:new HarmonyMethod(typeof(NativeMiningChecks),nameof(SkipAchievement)));
            try
            {
                for(int f=0;f<12 && !(bool)Get(Get(host,"Use"),"Active");f++)NativeToolsChecks.Frame(context,input);
                Require((bool)Get(Get(host,"Use"),"Active") && p.itemAnimation>0,"old native animation remains in flight");
                NativeQuickItemChecks.Sample(input,new Microsoft.Xna.Framework.Input.Keys[0]);Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft=true;Main.mouseLeft=true;Main.mouseX=728;Main.mouseY=648;Player.tileTargetX=45;Player.tileTargetY=40;
                // Advance to the next normal hit boundary while retaining
                // the already-running animation and its old operation lease.
                p.itemTime=p.toolTime=1;NativeQuickItemChecks.NativeFrame(p);Call(context,"UpdateRuntime");
                var region=(MiningRegion)Get(mining,"Region");Require(!Main.tile[45,40].active() && region.Count==1 && region[0].X==45 && region[0].Y==37,"new real manual first hit takes over during the previous automatic animation; tile="+Main.tile[45,40].active()+" region="+region.Count+" itemTime="+p.itemTime+" animation="+p.itemAnimation+" inUse="+Get(Get(host,"Use"),"InNativeUse"));
            }
            finally{isolation.Unpatch(achievement,HarmonyPatchType.All,isolation.Id);Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft=false;Main.mouseLeft=false;NativeToolsChecks.SetMode(host,2,0);for(int f=0;f<40;f++)NativeToolsChecks.Frame(context,input);}
        }
        private static void Gravity(object context,object input,object host,Player p)
        {
            foreach(int material in new[]{123,224})
            {
            int projectileType=material==123?71:179;
            NativeToolsChecks.SetMode(host,2,0);for(int f=0;f<40;f++)NativeToolsChecks.Frame(context,input);
            for(int x=37;x<48;x++)for(int y=34;y<65;y++)Main.tile[x,y].ClearEverything();foreach(var projectile in Main.projectile)projectile.active=false;
            for(int y=37;y<=40;y++)NativeToolsChecks.Tile(43,y,material);NativeToolsChecks.Tile(43,41,1);
            p.inventory[0].SetDefaults(1294);p.itemAnimation=p.itemTime=p.toolTime=0;p.selectedItemState.Select(0);p.selectedItemState.Update();NativeToolsChecks.SetMode(host,2,2);
            NativeQuickItemChecks.Sample(input,new Microsoft.Xna.Framework.Input.Keys[0]);Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft=true;Main.mouseLeft=true;Main.mouseX=696;Main.mouseY=648;Player.tileTargetX=43;Player.tileTargetY=40;
            NativeQuickItemChecks.NativeFrame(p);Call(context,"UpdateRuntime");
            object mining=Get(host,"Mining");
            Require(!Main.tile[43,40].active() && Main.projectile.Count(q=>q.active && q.type==projectileType)==3,"real first manual hit recursively spawns three falling projectiles before PickTile returns: "+material);
            Require((int)Get(mining,"falls")>0,"pre-hit gravity evidence survives the entirely empty post-hit vein");
            Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft=false;Main.mouseLeft=false;
            ulong tick=(ulong)Get(host,"Tick");
            for(int frame=0;frame<100;frame++){for(int i=0;i<Main.projectile.Length;i++)if(Main.projectile[i].active && Main.projectile[i].type==projectileType)Main.projectile[i].Update(i);Call(context,"UpdateRuntime");}
            Require((ulong)Get(host,"Tick")>=tick+100,"gravity waiting uses real runtime updates");
            var region=(MiningRegion)Get(mining,"Region");
            Require(Enumerable.Range(38,3).All(y=>Main.tile[43,y].active() && Main.tile[43,y].type==material),"real projectile movement/collision/Kill placed the fallen column");
            Require(region.Count==3 && Enumerable.Range(0,region.Count).Select(i=>region[i].Y).OrderBy(y=>y).SequenceEqual(new[]{38,39,40}),"selected gravity members can land back in their proven-vacated selected cells");
            NativeToolsChecks.SetMode(host,2,0);for(int f=0;f<40;f++)NativeToolsChecks.Frame(context,input);for(int x=37;x<48;x++)for(int y=34;y<65;y++)Main.tile[x,y].ClearEverything();
            }
            Console.WriteLine("PASS G09 actual first-hit gravity: recursive native spawn, projectile movement/collision/placement and retained three-cell region.");
        }
    }
}
