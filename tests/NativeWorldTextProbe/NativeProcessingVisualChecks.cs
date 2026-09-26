using System;
using System.IO;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Processing;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.UI.Gamepad;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeProcessingVisualChecks
    {
        internal static void Run(object context,ProbeGraphics graphics,string output)
        {
            Directory.CreateDirectory(output);Terraria.Localization.LanguageManager.Instance.SetLanguage("zh-Hans");Main.InitializeItemAnimations();
            var shell=Get(context,"Shell");var state=Get(shell,"State");var ui=Get(shell,"ReforgeUi");var renderer=Get(shell,"renderer");
            var settings=((ProcessingSettings[])Get(Get(context,"Processing"),"Settings"))[2];
            NativeProcessingUiChecks.Save(settings,new ProcessingOptions(false,Lang.prefix.Skip(1).Select(p=>p.Value).Where(s=>!string.IsNullOrEmpty(s)).Distinct()));
            Call(state,"Navigate",1);Call(state,"RestoreVisible");Call(renderer,"RefreshResources");
            foreach(var size in new[]{new[]{960,760,100},new[]{1280,720,150},new[]{960,440,100}})
            {
                float scale=size[2]/100f;NativeProcessingUiChecks.Prepare(context,size[0],size[1],scale);Call(Get(shell,"RecoveryUi"),"Prepare",true,Matrix.CreateScale(scale),new Vector2(size[0],size[1]));Call(ui,"Prepare",true,Matrix.CreateScale(scale),Get(Get(shell,"RecoveryUi"),"ContentBottom"));
                graphics.Image(Path.Combine(output,"processing-misc-"+size[0]+"-"+size[1]+"-"+size[2]+".png"),()=>{Call(renderer,"Draw",state,Matrix.CreateScale(scale),false,false);Call(Get(shell,"RecoveryUi"),"Draw",Get(shell,"drawKeyboard"),true);Call(ui,"Draw",Get(shell,"drawKeyboard"));},Matrix.CreateScale(scale),size[0],size[1]);
            }
            Call(state,"Navigate",0);var items=Get(shell,"items");Call(renderer,"Prepare",state,960f,760f,1f);Call(items,"Prepare",true,Matrix.Identity,new Vector2(960,760));
            graphics.LoadItemTextures(((System.Collections.IEnumerable)Get(items,"controls")).Cast<object>().Select(p=>(int)Get(p,"Type")).Where(t=>t>0));
            graphics.Image(Path.Combine(output,"processing-items.png"),()=>{Call(renderer,"Draw",state,Matrix.Identity,false,false);Call(items,"Draw",Get(shell,"drawKeyboard"),true);},Matrix.Identity,960,760);
            Call(state,"Close");Call(ui,"Suspend");Call(items,"Suspend");
            NativeReforgeOuterChecks.Run(context,graphics,output);
            Console.WriteLine("PASS G08 graphics: real F5 Chinese controls at normal/150/short viewports and complete original DrawInventory reforge seam.");
        }
    }
    internal static class NativeReforgeOuterChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static void Run(object context,ProbeGraphics graphics,string output)
        {
            NativeProcessingFlowChecks.InitializeShop();var p=Main.LocalPlayer;
            typeof(Main).GetField("_achievementAdvisor",Flags).SetValue(Main.instance,new Terraria.UI.AchievementAdvisor());
            typeof(Terraria.GameContent.Creative.CreativeUI).GetField("_initialized",Flags).SetValue(Main.CreativeMenu,true);
            foreach(int id in Enumerable.Range(0,58).Concat(Enumerable.Range(180,10)).Concat(Enumerable.Range(300,11)))
                if(!UILinkPointNavigator.Points.ContainsKey(id))UILinkPointNavigator.Points.Add(id,new UILinkPoint(id,true,-1,-1,-1,-1));
            foreach(int id in new[]{3,4,7,9,12,13,15,21})graphics.LoadTexture("InventoryBack"+id,"Images/Inventory_Back"+id);
            graphics.LoadTexture("InventoryTickOn","Images/Inventory_Tick_On");graphics.LoadTexture("Trash","Images/Trash");graphics.LoadTexture("Extra","Images/Extra_54",54);
            graphics.LoadTexture("HbLock","Images/Lock_1",1);
            foreach(int id in new[]{3,4,8})graphics.LoadTexture("EquipPage","Images/UI/DisplaySlots_"+id,id);
            graphics.LoadTexture("BestiaryMenuButton","Images/UI/Bestiary");graphics.LoadTexture("EmoteMenuButton","Images/UI/Emotes");
            for(int id=0;id<2;id++)graphics.LoadTexture("Reforge","Images/UI/Reforge_"+id,id);
            graphics.LoadTexture("ChestStack","Images/UI/ChestStack_0",0);graphics.LoadTexture("InventorySort","Images/UI/Sort_0",0);
            for(int id=0;id<4;id++)graphics.LoadTexture("Coin","Images/Coin_"+id,id);graphics.LoadItemTextures(new[]{53,71,72,73,74});
            foreach(var item in p.inventory.Concat(p.miscEquips).Concat(p.miscDyes))item.TurnToAir();for(int a=0;a<4;a++)foreach(var item in NativeCoinChecks.Bank(p,a).item)item.TurnToAir();
            Array.Clear(p.buffType,0,p.buffType.Length);p.unlockedSuperCart=false;p.hideMisc[0]=p.hideMisc[1]=false;p.hbLocked=false;p.difficulty=0;p.trashItem.TurnToAir();
            Main.EquipPage=Main.EquipPageSelected=2;Main.mapEnabled=false;Main.PipsUseGrid=false;Main.teamBasedSpawnsSeed=false;Main.playerInventory=Main.InReforgeMenu=true;Main.InGuideCraftMenu=false;Main.SetNPCShopIndex(0);p.chest=-1;
            Main.npc[0].SetDefaults(107);Main.npc[0].active=true;Main.npc[0].homeless=true;p.SetTalkNPC(0);p.currentShoppingSettings.PriceAdjustment=1f;p.discountAvailable=false;
            Main.reforgeItem=new Item();Main.reforgeItem.SetDefaults(53);Main.mouseItem.TurnToAir();p.inventory[50]=NativeCoinChecks.Coin(74,10);
            typeof(Main).GetField("reforgeCooldown",Flags).SetValue(null,0); // Isolated initial state only; production never resets native cooldown.
            var host=Get(context,"Processing");var owner=Get(host,"Reforge");var input=Get(context,"Input");var settings=((ProcessingSettings[])Get(host,"Settings"))[2];
            NativeProcessingUiChecks.Save(settings,new ProcessingOptions(true,Main.reforgeItem.GetRollablePrefixes().Select(id=>Lang.prefix[id].Value).Distinct()));
            Main.screenWidth=960;Main.screenHeight=760;Main.UIScale=1;typeof(PlayerInput).GetField("_originalScreenWidth",Flags).SetValue(null,960);typeof(PlayerInput).GetField("_originalScreenHeight",Flags).SetValue(null,760);
            NativeReforgeChecks.Sample(input,false);Call(owner,"Update");Draw(graphics);
            Require((bool)Get(owner,"quoteReady"),"actual outer DrawInventory supplies quote and geometry");
            long fee=(long)Get(owner,"quote"),before=NativeReforgeChecks.Total(p);
            NativeReforgeChecks.Sample(input,true);Call(owner,"Update");Draw(graphics);Draw(graphics);
            Require(before-NativeReforgeChecks.Total(p)==fee && Main.reforgeItem.prefix!=0,"actual draw tails cannot charge after one auto-hit update");
            NativeReforgeChecks.Sample(input,false,200,600);Call(owner,"Update");Draw(graphics);Draw(graphics); // Native hover exit ends any top-tier cooldown.
            NativeReforgeChecks.Sample(input,false);Call(owner,"Update");Draw(graphics);
            before=NativeReforgeChecks.Total(p);fee=(long)Get(owner,"quote");NativeReforgeChecks.Sample(input,true);Call(owner,"Update");Draw(graphics);Draw(graphics);
            Require(before-NativeReforgeChecks.Total(p)==fee,"already-matched fresh press pays exactly once through complete original DrawInventory; delta="+(before-NativeReforgeChecks.Total(p))+" fee="+fee+" cooldown="+typeof(Main).GetField("reforgeCooldown",Flags).GetValue(null)+" quote="+Get(owner,"quoteReady")+" manual="+Get(owner,"manual"));
            graphics.Image(Path.Combine(output,"processing-native-reforge.png"),()=>Call(Main.instance,"DrawInventory"),Main.UIScaleMatrix,960,760);
            NativeReforgeChecks.Sample(input,false,200,600);Call(owner,"Update");Draw(graphics);Draw(graphics);
            Main.reforgeItem.ResetPrefix();Main.screenWidth=1920;Main.screenHeight=1080;PlayerInput.CacheOriginalScreenDimensions();Main.UIScale=1.5f;
            NativeReforgeChecks.Sample(input,false,180,465);Call(owner,"Update");Draw(graphics);
            before=NativeReforgeChecks.Total(p);fee=(long)Get(owner,"quote");PlayerInput.SetZoom_Unscaled();
            NativeReforgeChecks.Sample(input,true,180,465);Call(owner,"Update");
            Require(before-NativeReforgeChecks.Total(p)==fee,"150% first automatic payment happens in Update, before ordinary Draw fallback");Draw(graphics);
            Require(before-NativeReforgeChecks.Total(p)==fee && Main.reforgeItem.prefix!=0,"physical pointer and native UI quote remain valid across 150% Draw to unscaled Update");
            NativeReforgeChecks.Sample(input,false);Call(owner,"Update");Main.InReforgeMenu=false;p.SetTalkNPC(-1);Main.playerInventory=false;
        }
        private static void Draw(ProbeGraphics graphics){Main.mouseX=PlayerInput.MouseInfo.X;Main.mouseY=PlayerInput.MouseInfo.Y;typeof(PlayerInput).GetMethod("CacheOriginalInput",Flags).Invoke(null,null);PlayerInput.SetZoom_UI();graphics.Pixels(()=>Call(Main.instance,"DrawInventory"),Main.UIScaleMatrix);}
    }
}
