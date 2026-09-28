using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ObjectData;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Full native Update retains selection, equipment resets, SmartCursor,
    // movement, Wrapped and each consumer. Only unrelated achievement outlets
    // are isolated. Synthetic input never reads the desktop keyboard/mouse.
    internal static class NativeToolExecutionChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static int consumers,catchCalls,catchSkipped,catchHits;
        private static NPC watched;
        private static ProbeGraphics textures;
        internal static void Run(object context,ProbeGraphics graphics,bool release=false)
        {
            textures=graphics;
            object host=Get(context,"Tools"),input=Get(context,"Input");
            NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return ((JueMingR.Features.Tools.ToolSettings[])Get(host,"Settings")).All(s=>s.Loaded);});
            typeof(Main).GetMethod("Initialize_TileAndNPCData1",Flags).Invoke(null,null);
            typeof(Main).GetMethod("Initialize_TileAndNPCData2",Flags).Invoke(null,null);TileObjectData.Initialize();
            Terraria.GameContent.Creative.CreativePowerManager.Initialize();
            Terraria.DataStructures.ArmorSetBonuses.Initialize();Terraria.DataStructures.ArmorSetBonuses.BuildLookup();
            for(int i=1;i<Main.player.Length;i++)if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};
            for(int i=0;i<Main.item.Length;i++)Main.item[i].whoAmI=i;
            PopupText.popupText=new PopupText[20];for(int i=0;i<20;i++)PopupText.popupText[i]=new PopupText();
            if(graphics!=null)
            {
                graphics.LoadItemTextures(new[]{0,ItemID.StaffofRegrowth,ItemID.AcornAxe,ItemID.ShroomiteDiggingClaw,ItemID.CopperPickaxe,ItemID.AdamantiteDrill,ItemID.Drax,1991,3183,4821});graphics.LoadTexture("Npc","Images/NPC_46",46);
                new Harmony("JueMingR.Tests.QuickItemOutlets").Unpatch(typeof(Item).GetMethod("GetDrawHitbox"),HarmonyPatchType.Prefix,"JueMingR.Tests.QuickItemOutlets");
            }
            var watch=new Harmony("JueMingR.Tests.G09Execution");
            foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})watch.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeToolExecutionChecks),nameof(SkipOutlet)));
            foreach(string name in new[]{"ItemCheck_UseMiningTools","PlaceThing_Tiles","ItemCheck_CatchCritters"})watch.Patch(typeof(Player).GetMethod(name,Flags),postfix:new HarmonyMethod(typeof(NativeToolExecutionChecks),nameof(Consumed)));
            watch.Patch(typeof(Player).GetMethod("ItemCheck_CatchCritters",Flags),postfix:new HarmonyMethod(typeof(NativeToolExecutionChecks),nameof(Caught)));
            var failures=new List<string>();
            try
            {
                foreach(bool smart in new[]{false,true})foreach(int empty in new[]{0,1,3})
                {
                    foreach(int tool in new[]{ItemID.StaffofRegrowth,ItemID.AcornAxe})foreach(int slot in new[]{0,12})
                        Compare(context,host,input,tool,slot,true,smart,empty,failures);
                    Compare(context,host,input,ItemID.ShroomiteDiggingClaw,0,false,smart,empty,failures);
                }
                foreach(int tool in new[]{ItemID.CopperPickaxe,ItemID.AdamantiteDrill,ItemID.Drax})Compare(context,host,input,tool,0,false,false,1,failures);
                if(graphics!=null)foreach(int net in new[]{1991,3183,4821})foreach(int direction in new[]{-1,1})foreach(bool smart in new[]{false,true})
                {
                    int reference=Capture(context,host,input,false,net,direction,smart),automatic=Capture(context,host,input,true,net,direction,smart);
                    if(reference<0 || automatic<0 || automatic>reference+1)failures.Add("catch net="+net+" direction="+direction+" smart="+smart+" native="+reference+" auto="+automatic);
                }
                if(graphics!=null)foreach(int net in new[]{1991,3183,4821})
                {
                    int reference=Capture(context,host,input,false,net,-1,false,1),automatic=Capture(context,host,input,true,net,-1,false,1);
                    if(reference<0 || automatic<0 || automatic>reference+1)failures.Add("catch from behind net="+net+" native="+reference+" auto="+automatic);
                }
                Require(failures.Count==0,"full native execution: "+string.Join("; ",failures));
                MiningManual(context,host,input);Safety(context,host,input);
                NativeToolWaitChecks.Run(context,host,input,release);
                Console.WriteLine("PASS G09 full Player.Update/ItemCheckWrapped: true automatic first hit, continuous effects, unsampled outer updates, focused retention and safety cancellation"+(graphics==null?"; texture-free CPU cadence, capture not run.":"; three real net textures, native rabbit AI from both sides and actual catch output."));
            }
            finally{for(int i=0;i<3;i++)NativeToolsChecks.SetMode(host,i,0);foreach(var method in watch.GetPatchedMethods().ToArray())watch.Unpatch(method,HarmonyPatchType.All,watch.Id);}
        }
        private static bool SkipOutlet(){return false;}
        private static void Consumed(bool __runOriginal){if(__runOriginal)consumers++;}
        private static void Caught(Rectangle __1,bool __runOriginal)
        {if(!__runOriginal){catchSkipped++;return;}catchCalls++;if(watched!=null && __1.Intersects(watched.Hitbox))catchHits++;}
        internal static void Sample(object context,object input,Vector2 world,bool held)
        {
            Call(input,"BeginUpdate");
            Vector2 pixel=Vector2.Transform(world-Main.screenPosition,Main.GameViewMatrix.ZoomMatrix);
            PlayerInput.MouseInfo=new MouseState((int)pixel.X,(int)pixel.Y,0,held?ButtonState.Pressed:ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);
            var tokens=new List<string>();if(held)tokens.Add("Mouse1");PlayerInput.Triggers.Reset();Call(input,"AfterNativeMouse",tokens);
            foreach(string token in tokens)PlayerInput.CurrentProfile.InputModes[InputMode.Keyboard].Processkey(PlayerInput.Triggers.Current,token,InputMode.Keyboard);
            PlayerInput.Triggers.Update();Main.mouseLeft=PlayerInput.Triggers.Current.MouseLeft;Main.mouseRight=false;
            PlayerInput.MouseX=(int)pixel.X;PlayerInput.MouseY=(int)pixel.Y;PlayerInput.UpdateMainMouse();PlayerInput.CacheZoomableValues();
            Main.oldKeyState=Main.keyState;Main.keyState=new KeyboardState();Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");Call(Get(context,"Shell"),"ProcessInput");
            PlayerInput.SetZoom_World();
        }
        internal static void Outer(object context,object input,int count)
        {
            // Fixed .8 Main.DoUpdate may return before HandleInput and world
            // update while Main.Update's prefix/postfix still execute. This is
            // the exact R seam sequence, with NO native use on these epochs.
            int before=consumers;for(int i=0;i<count;i++){Call(input,"BeginUpdate");Call(context,"UpdateRuntime");}
            Require(consumers==before && (count==0 || !(bool)Get(input,"CanStartActions")),"unsampled outer update cannot execute a native consumer or authorize new input");
        }
        internal static Player Reset(object context,object host,object input,int tool,int slot,int selected)
        {
            for(int i=0;i<3;i++)NativeToolsChecks.SetMode(host,i,0);
            textures?.LoadItemTextures(Main.LocalPlayer.inventory.Where(item=>!item.IsAir).Select(item=>item.type).Distinct());
            for(int i=0;i<80;i++)NativeToolsChecks.Frame(context,input);
            var p=Main.LocalPlayer;foreach(var item in p.inventory)item.TurnToAir();foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;foreach(var item in Main.item)item.TurnToAir();
            for(int x=5;x<115;x++)for(int y=5;y<115;y++)Main.tile[x,y].ClearEverything();
            Main.screenPosition=new Vector2(300,400);Main.GameViewMatrix.Zoom=new Vector2(1.25f);Main.worldSurface=60;
            for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);
            Main.dayTime=true;Main.time=27000;Main.raining=false;
            p.statLife=p.statLifeMax=400;p.position=new Vector2(640,646);p.velocity=Vector2.Zero;p.direction=1;p.gravDir=1;
            p.itemAnimation=p.itemTime=p.toolTime=0;p.controlUseItem=p.channel=false;p.hitTile=new HitTile();
            p.inventory[slot].SetDefaults(tool);p.selectedItemState.Select(selected);p.selectedItemState.Update();
            textures?.LoadItemTextures(new[]{tool});
            return p;
        }
        private static void Compare(object context,object host,object input,int tool,int slot,bool herbs,bool smart,int empty,List<string> failures)
        {
            var native=Cadence(context,host,input,tool,slot,herbs,false,smart,empty);
            var automatic=Cadence(context,host,input,tool,slot,herbs,true,smart,empty);
            string label="tool="+tool+" slot="+slot+" smart="+smart+" empty="+empty;
            Console.WriteLine("G09 full cadence "+label+" native="+string.Join(",",native)+" auto="+string.Join(",",automatic));
            if(native.Count!=(herbs?7:22) || automatic.Count!=native.Count || automatic.Where((f,i)=>f>native[i]+1).Any())failures.Add(label+" lost native progress");
        }
        private static List<int> Cadence(object context,object host,object input,int tool,int slot,bool herbs,bool automatic,bool smart,int empty)
        {
            var p=Reset(context,host,input,tool,slot,automatic?0:slot);var points=new List<Point>();
            if(herbs)Require(!p.inventory.Any(item=>item.type==ItemID.DaybloomSeeds),"full-update regrowth starts without inventory seeds");
            for(int x=37;x<=44;x++)if(x!=40)
            {
                if(herbs){Require(WorldGen.PlaceTile(x,42,78,mute:true,forced:true,plr:0),"full-update actual pot");NativeToolsChecks.Tile(x,41,84);points.Add(new Point(x,41));}
                else for(int y=38;y<=40;y++){NativeToolsChecks.Tile(x,y,6);points.Add(new Point(x,y));}
            }
            if(!herbs){NativeToolsChecks.Tile(40,38,6);points.Add(new Point(40,38));}
            Main.SmartCursorWanted_Mouse=smart;Main.SmartCursorWanted_GamePad=false;
            if(automatic)NativeToolsChecks.SetMode(host,herbs?1:2,herbs?1:2);
            var effects=new List<int>();var completed=new bool[points.Count];int target=-1;
            for(int frame=0;frame<3000 && effects.Count<points.Count;frame++)
            {
                bool held=!automatic || !herbs && effects.Count==0;
                if(target<0 || completed[target])target=Enumerable.Range(0,points.Count).Where(i=>!completed[i]).OrderBy(i=>Vector2.DistanceSquared(p.Center,new Vector2(points[i].X*16+8,points[i].Y*16+8))).First();
                Vector2 mouse=held?new Vector2(points[target].X*16+8,points[target].Y*16+8):new Vector2(480,540);
                Sample(context,input,mouse,held);NativeQuickItemChecks.BeginWorldStep();p.Update(0);
                if(herbs)Require(p.HeldItem.type==tool,"native regrowth keeps the actual regeneration tool; dropped seeds may be picked up normally");
                foreach(var q in Main.projectile.Where(q=>q.active && q.owner==0 && (q.aiStyle==20 || q.type==445)))q.AI();
                Call(context,"UpdateRuntime");Outer(context,input,empty);
                for(int i=0;i<points.Count;i++)if(!completed[i]){var t=Main.tile[points[i].X,points[i].Y];if(herbs?t.active() && t.type==82 && t.frameX==0:!t.active()){completed[i]=true;effects.Add(frame);}}
            }
            return effects;
        }
        private static int Capture(object context,object host,object input,bool automatic,int net,int direction,bool smart,int facing=0)
        {
            var p=Reset(context,host,input,net,12,automatic?0:12);Main.rand=new Terraria.Utilities.UnifiedRandom(7123);p.direction=facing==0?direction:facing;p.releaseUseItem=true;
            Main.SmartCursorWanted_Mouse=smart;
            var target=Main.npc[0];target.SetDefaults(46);target.whoAmI=0;target.active=true;target.position=new Vector2(direction==1?725:1300-725-target.width,668);target.velocity=new Vector2(-direction,0);target.direction=-direction;target.ai[0]=1;target.ai[1]=400;target.target=0;
            watched=target;catchCalls=catchSkipped=catchHits=0;Call(Get(host,"Npcs"),"BeginTick");if(automatic)NativeToolsChecks.SetMode(host,0,1);
            int caught=-1;
            for(int frame=0;frame<160;frame++)
            {
                // Both sides keep their physical cursor far opposite the rabbit.
                Sample(context,input,new Vector2(direction==1?480:820,540),!automatic);NativeQuickItemChecks.BeginWorldStep();p.Update(0);
                if(!target.active)
                {
                    Require(Main.item.Where(w=>w.active && w.type==target.catchItem).Sum(w=>w.stack)==1 && target.catchItem==2019,"actual native rabbit catch creates exactly one correct world item");caught=frame;break;
                }
                target.UpdateNPC(0);Call(context,"UpdateRuntime");Outer(context,input,3);
            }
            Console.WriteLine("G09 full catch auto="+automatic+" net="+net+" side="+direction+" facing="+p.direction+" smart="+smart+" caught="+caught+" consumers="+catchCalls+" skipped="+catchSkipped+" intersections="+catchHits);
            watched=null;return caught;
        }
        private static void Safety(object context,object host,object input)
        {
            foreach(string boundary in new[]{"focus","f5","ui","off","selection","source","death","session"})
            {
                var p=Reset(context,host,input,ItemID.StaffofRegrowth,12,0);object use=Get(host,"Use");
                for(int x=41;x<=44;x++){Require(WorldGen.PlaceTile(x,42,78,mute:true,forced:true,plr:0),"safety actual pot");NativeToolsChecks.Tile(x,41,84);}
                NativeToolsChecks.SetMode(host,1,1);Sample(context,input,new Vector2(480,540),false);NativeQuickItemChecks.BeginWorldStep();p.Update(0);Call(context,"UpdateRuntime");
                Require((bool)Get(use,"Active"),"safety obtains actual continuous use");long token=(long)Get(use,"Operation");int animation=p.itemAnimation,time=p.itemTime,toolTime=p.toolTime;Outer(context,input,3);
                Require((bool)Get(use,"Active") && !(bool)Get(use,"cancelled") && (long)Get(use,"Operation")==token,"unsampled epochs retain the same valid operation");
                Require(p.itemAnimation==animation && p.itemTime==time && p.toolTime==toolTime,"unsampled retention never advances or resets native timers");
                bool updates=Main.CanUpdateGameplay;
                if(boundary=="focus"){Main.ToggleGameplayUpdates(true);Set(input,"foregroundWindow",(Func<IntPtr>)(()=>IntPtr.Zero));}
                else if(boundary=="f5")NativeF5AutomationChecks.Open(context);
                else if(boundary=="ui")Main.drawingPlayerChat=true;
                else if(boundary=="off")NativeToolsChecks.SetMode(host,1,0);
                else if(boundary=="selection")p.selectedItemState.Select(1);
                else if(boundary=="death")p.dead=true;
                else if(boundary=="session")Main.gameMenu=true;
                else p.inventory[12]=new Item();
                Outer(context,input,1);
                if(boundary=="focus" || boundary=="f5")
                {
                    Require((bool)Get(use,"Active") && !(bool)Get(use,"cancelled") && (long)Get(use,"Operation")==token,"background empty update retains original operation");
                    Require(p.itemAnimation==animation && p.itemTime==time && p.toolTime==toolTime,"background empty update never advances timers");
                    for(int f=0;f<80;f++){Sample(context,input,new Vector2(480,540),false);NativeQuickItemChecks.BeginWorldStep();p.Update(0);Call(context,"UpdateRuntime");}
                    Require(Enumerable.Range(41,4).Any(x=>Main.tile[x,41].type!=84),"real player simulation continues herb harvesting with "+boundary);
                    if(boundary=="f5")Require((bool)Get(Get(Get(context,"Shell"),"State"),"Visible"),"harvesting completes while F5 remains open");
                    Main.ToggleGameplayUpdates(updates);
                }
                else Require(!(bool)Get(use,"Active") || (bool)Get(use,"cancelled"),"unsampled update still cancels "+boundary);
                Main.drawingPlayerChat=false;p.dead=false;Main.gameMenu=false;Call(context,"UpdateRuntime");Set(input,"foregroundWindow",(Func<IntPtr>)(()=>new IntPtr(1)));
                if(boundary=="f5"){Call(Get(Get(context,"Shell"),"State"),"Close");Call(Get(context,"Shell"),"EndPointerLayer");p.mouseInterface=false;}
                if(boundary=="focus"){Sample(context,input,new Vector2(480,540),true);Require(!(bool)Get(input,"CanStartActions"),"reactivation held chord stays quarantined");Sample(context,input,new Vector2(480,540),false);}
                NativeToolsChecks.SetMode(host,1,0);
            }
        }
        private static void MiningManual(object context,object host,object input)
        {
            var p=Reset(context,host,input,ItemID.ShroomiteDiggingClaw,0,0);object use=Get(host,"Use");
            NativeToolsChecks.Tile(42,40,6);NativeToolsChecks.Tile(43,40,6);NativeToolsChecks.SetMode(host,2,2);
            var first=new Vector2(42*16+8,40*16+8);Sample(context,input,first,true);NativeQuickItemChecks.BeginWorldStep();p.Update(0);Call(context,"UpdateRuntime");
            Require(!Main.tile[42,40].active() && Main.tile[43,40].active(),"real first manual pick establishes a connected remaining target");
            for(int i=0;i<4;i++){Sample(context,input,first,true);NativeQuickItemChecks.BeginWorldStep();p.Update(0);Call(context,"UpdateRuntime");Outer(context,input,1);}
            Require(!(bool)Get(use,"Active") && Main.tile[43,40].active(),"continued physical hold keeps manual target and cannot be stolen by automatic mining");
            Sample(context,input,new Vector2(480,540),false);NativeQuickItemChecks.BeginWorldStep();p.Update(0);Call(context,"UpdateRuntime");
            Require((bool)Get(use,"Active") && p.selectedItem==0 && !p.selectedItemState.HasActiveOverride,"release continues same held source inside the existing animation without a selection override");
            Sample(context,input,first,true);NativeQuickItemChecks.BeginWorldStep();p.Update(0);Call(context,"UpdateRuntime");
            Require(!(bool)Get(use,"Active") || (bool)Get(use,"cancelled"),"fresh physical press immediately owns the native ItemCheck");
            NativeToolsChecks.SetMode(host,2,0);
        }
    }
}
