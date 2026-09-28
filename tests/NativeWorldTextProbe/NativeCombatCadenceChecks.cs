using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatCadenceChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static readonly List<int> starts=new List<int>();
        private static readonly List<int> shots=new List<int>();
        private static bool watchSwitch;
        private static int lastSlot;
        private static uint switchedAt;
        private static readonly List<uint> switchDelays=new List<uint>();
        private static int splits;
        internal static int ManualMouseX,ManualMouseY;
        internal static void Run(object context)
        {
            object combat=Get(context,"Combat"),tools=Get(context,"Tools"),input=Get(context,"Input");
            var settings=(CombatSettings)Get(combat,"Settings");
            NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return settings.Loaded && ((JueMingR.Features.Tools.ToolSettings[])Get(tools,"Settings")).All(s=>s.Loaded);});
            typeof(Main).GetMethod("Initialize_TileAndNPCData1",Flags).Invoke(null,null);typeof(Main).GetMethod("Initialize_TileAndNPCData2",Flags).Invoke(null,null);
            Terraria.ObjectData.TileObjectData.Initialize();Terraria.GameContent.Creative.CreativePowerManager.Initialize();
            Terraria.DataStructures.ArmorSetBonuses.Initialize();Terraria.DataStructures.ArmorSetBonuses.BuildLookup();
            // Select the real color engine (its AddLight list is constructor
            // initialized). Rebuilding the unrelated legacy capture renderer
            // requires a camera/device and belongs to the graphical fixture.
            Lighting.Mode=Terraria.Graphics.Light.LightMode.Color;
            if(Main.instance==null){Main.instance=(Main)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Main));GC.SuppressFinalize(Main.instance);}
            for(int i=1;i<Main.player.Length;i++)if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};
            for(int i=0;i<Main.item.Length;i++)Main.item[i].whoAmI=i;
            PopupText.popupText=new PopupText[20];for(int i=0;i<20;i++)PopupText.popupText[i]=new PopupText();
            var audit=new Harmony("JueMingR.Tests.CombatCadence");
            foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),nameof(SkipAchievement)));
            audit.Patch(typeof(Player).GetMethod("ItemCheck_StartActualUse",Flags),postfix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),nameof(Started)));
            audit.Patch(typeof(Player).GetMethod("TryUpdateChannel",Flags),postfix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),nameof(Created)));
            audit.Patch(typeof(Player).GetNestedType("SelectedItemState",Flags).GetMethod("Update",Flags),postfix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),nameof(Selected)));
            try
            {
                foreach(int empty in new[]{0,1,3})
                {
                    Save(combat,new CombatOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,ItemID.CopperShortsword,0,0);starts.Clear();shots.Clear();
                    Save(combat,new CombatOptions(1));
                    for(int t=0;t<240;t++)Step(context,true,false,empty);
                    Require(starts.Count>=8 && shots.Count>=8,"real continuous shortsword uses, empty="+empty+" starts="+starts.Count+" shots="+shots.Count);
                    int before=starts.Count;for(int t=0;t<70;t++)Step(context,false,false,empty);
                    Require(starts.Count==before && !(bool)Get(Get(combat,"Use"),"Active") && !Main.mouseLeft,"release ends cadence without synthetic input");
                    Save(combat,new CombatOptions());p=NativeToolExecutionChecks.Reset(context,tools,input,198,0,0);p.inventory[1].SetDefaults(671);starts.Clear();shots.Clear();
                    Save(combat,new CombatOptions(4));
                    watchSwitch=true;lastSlot=p.selectedItem;switchedAt=0;switchDelays.Clear();
                    for(int t=0;t<300;t++)
                    {try{Step(context,false,true,empty);}catch{Console.WriteLine("Quick-switch native failure at step="+t+" slot="+p.selectedItem+" animation="+p.itemAnimation+" time="+p.itemTime+" starts="+string.Join(",",starts)+" shots="+string.Join(",",shots)+" interact="+p.tileInteractionHappened+" tile="+p.controlUseTile+" releaseTile="+p.releaseUseTile+" animation="+p.itemAnimation+" active="+Get(Get(combat,"Use"),"Active")+" blocked="+Get(Get(combat,"Use"),"blockedGesture"));throw;}}
                    Require(shots.Count(s=>s==p.inventory[0].shoot)>=2 && shots.Count(s=>s==p.inventory[1].shoot)>=2,"real release shots of two weapons, empty="+empty+" shots="+string.Join(",",shots)+" selected="+p.selectedItem+" anim="+p.itemAnimation+" time="+p.itemTime);
                    Require(switchDelays.Count>1 && switchDelays.All(delay=>delay>=12),"extra interval begins at confirmed native selection, delays="+string.Join(",",switchDelays));watchSwitch=false;
                    int selected=p.selectedItem;for(int t=0;t<60;t++)Step(context,false,false,empty);
                    Require(p.selectedItem==selected,"stopping quick switch retains last real slot");
                }
                NativeCombatInteractionChecks.Run(context);
                Dedicated(context,combat,tools,input);
                NativeCombatHitChecks.FlailReceipts(context);
                Cursor(context,combat,tools,input);
                NativeCombatBoundaryChecks.Run(context);
                NativeCombatHandoffChecks.Run(context);
                Console.WriteLine("PASS G11A full native Player.Update and Projectile.Update: continuous click and release/switch with 0/1/3 unsampled outer updates.");
            }
            finally{watchSwitch=false;Save(combat,new CombatOptions());foreach(var method in audit.GetPatchedMethods().ToArray())audit.Unpatch(method,HarmonyPatchType.All,audit.Id);}
        }
        private static void Cursor(object context,object combat,object tools,object input)
        {
            Save(combat,new CombatOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,ItemID.CopperShortsword,0,0);
            Main.playerInventory=true;Main.mouseItem=new Item();Main.mouseItem.SetDefaults(ItemID.CopperShortsword);starts.Clear();shots.Clear();
            Save(combat,new CombatOptions(1));
            try
            {
                Step(context,true,false,0);
                Require(p.selectedItem==58 && p.selectedItemState.HasActiveOverride,"real native cursor selection must occur, actual="+p.selectedItem+" override="+p.selectedItemState.HasActiveOverride+" cursor="+Main.mouseItem.type+" slot="+p.inventory[58].type);
                Require((bool)Get(Get(combat,"Use"),"Active"),"legal original cursor override may acquire combat input");
                for(int t=0;t<180;t++)Step(context,true,false,1);
                Require(starts.Count>=6 && Main.mouseItem.type==ItemID.CopperShortsword,"real cloned cursor item repeats without inventory replacement");
            }
            finally{Main.mouseItem=new Item();Main.playerInventory=false;Save(combat,new CombatOptions());}
        }
        private static void Dedicated(object context,object combat,object tools,object input)
        {
            foreach(int interval in new[]{0,30})foreach(var pair in new[]{new[]{3352,3772},new[]{5462,6153},new[]{198,198}})
            {
                Save(combat,new CombatOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,pair[0],0,0);p.inventory[1].SetDefaults(pair[1]);
                p.statMana=p.statManaMax=p.statManaMax2=400;starts.Clear();shots.Clear();
                Save(combat,new CombatOptions(4,interval));watchSwitch=true;lastSlot=0;switchedAt=0;switchDelays.Clear();
                for(int t=0;t<720;t++)Step(context,false,true,1);
                Require(starts.Count>=4 && shots.Contains(p.inventory[0].shoot) && shots.Contains(p.inventory[1].shoot),"native extended/same-type pair "+string.Join("/",pair)+" interval="+interval+" starts="+string.Join(",",starts)+" shots="+string.Join(",",shots)+" interact="+p.tileInteractionHappened+" tile="+p.controlUseTile+" releaseTile="+p.releaseUseTile+" animation="+p.itemAnimation+" active="+Get(Get(combat,"Use"),"Active")+" blocked="+Get(Get(combat,"Use"),"blockedGesture"));
                Require(switchDelays.Count>1 && switchDelays.All(v=>v>=interval),"all release families honor confirmed-selection interval "+interval+": "+string.Join(",",switchDelays));watchSwitch=false;
            }
            foreach(int mode in new[]{1,3,4})
            {
                Save(combat,new CombatOptions());int type=mode==1?162:mode==3?2269:3262;
                var p=NativeToolExecutionChecks.Reset(context,tools,input,type,0,0);foreach(var item in p.armor)item.TurnToAir();
                if(mode==4)p.armor[3].SetDefaults(ItemID.MagicString);
                if(mode==3){p.inventory[54].SetDefaults(ItemID.MusketBall);p.inventory[54].stack=100;}
                starts.Clear();shots.Clear();splits=0;Save(combat,new CombatOptions((1<<mode)|1));
                for(int t=0;t<540;t++)Step(context,mode!=1,mode==1,1);
                Require(starts.Count>=3 && shots.Count>=3,"continuous dedicated mode="+mode+" starts="+starts.Count+" shots="+shots.Count);
                if(mode==4)Require(p.magicString && splits>=3,"real accessory drives repeated native magic-string split, actual="+splits);
                if(mode==3)Require(p.inventory[54].stack<100 && p.revolverCritChanceBonus>0,"native revolver consumes bullets and accumulates release bonus");
                for(int t=0;t<90;t++)Step(context,false,false,1);
                Require(!(bool)Get(Get(combat,"Use"),"Active"),"dedicated gesture released");
            }
        }
        internal static void Save(object combat,CombatOptions value)
        {
            var settings=(CombatSettings)Get(combat,"Settings");Require(settings.Set(value),"combat isolated settings accepted");
            NativeQuickItemChecks.Until(()=>{Call(combat,"Poll");return !settings.Busy;});Require(settings.CompletionSucceeded,"combat settings persisted");
        }
        internal static void Step(object context,bool left,bool right,int empty,Keys[] keys=null,Vector2? point=null)
        {
            var input=Get(context,"Input");Call(input,"BeginUpdate");
            Vector2 pixel=Vector2.Transform((point??new Vector2(830,654))-Main.screenPosition,Main.GameViewMatrix.ZoomMatrix);
            PlayerInput.MouseInfo=new MouseState((int)pixel.X,(int)pixel.Y,0,left?ButtonState.Pressed:ButtonState.Released,ButtonState.Released,right?ButtonState.Pressed:ButtonState.Released,ButtonState.Released,ButtonState.Released);
            var tokens=new List<string>();if(left)tokens.Add("Mouse1");if(right)tokens.Add("Mouse2");PlayerInput.Triggers.Reset();Call(input,"AfterNativeMouse",tokens);
            foreach(string name in tokens)PlayerInput.CurrentProfile.InputModes[InputMode.Keyboard].Processkey(PlayerInput.Triggers.Current,name,InputMode.Keyboard);
            PlayerInput.Triggers.Update();Main.mouseLeft=PlayerInput.Triggers.Current.MouseLeft;Main.mouseRight=PlayerInput.Triggers.Current.MouseRight;
            PlayerInput.MouseX=(int)pixel.X;PlayerInput.MouseY=(int)pixel.Y;PlayerInput.UpdateMainMouse();PlayerInput.CacheZoomableValues();
            Main.oldKeyState=Main.keyState;Main.keyState=new KeyboardState(keys??new Keys[0]);Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");Call(Get(context,"Shell"),"ProcessInput");Call(Get(context,"Combat"),"Sample");PlayerInput.SetZoom_World();
            ManualMouseX=Main.mouseX;ManualMouseY=Main.mouseY;
            NativeQuickItemChecks.BeginWorldStep();Main.LocalPlayer.Update(0);
            for(int i=0;i<Main.maxProjectiles;i++)if(Main.projectile[i].active)Main.projectile[i].Update(i);
            Call(context,"UpdateRuntime");NativeToolExecutionChecks.Outer(context,input,empty);
        }
        private static bool SkipAchievement(){return false;}
        private static void Started(Player __instance,Item __0){if(__instance.whoAmI==Main.myPlayer){starts.Add(__0.type);if(watchSwitch && switchedAt!=0){switchDelays.Add(unchecked(Main.GameUpdateCount-switchedAt));switchedAt=0;}}}
        private static void Selected(Player ___player){if(watchSwitch && ___player.whoAmI==Main.myPlayer && ___player.selectedItem!=lastSlot){lastSlot=___player.selectedItem;switchedAt=Main.GameUpdateCount;}}
        private static void Created(Player __instance,Projectile __0){if(__instance.whoAmI==Main.myPlayer){shots.Add(__0.type);if(__0.aiStyle==99 && __0.ai[0]==-2)splits++;}}
    }
}
