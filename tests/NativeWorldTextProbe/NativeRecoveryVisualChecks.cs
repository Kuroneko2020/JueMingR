using System;
using System.IO;
using System.Collections;
using System.Linq;
using JueMingR.Features.Recovery;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeRecoveryVisualChecks
    {
        internal static void Run(object context,ProbeGraphics graphics,string output)
        {
            Directory.CreateDirectory(output);Terraria.Localization.LanguageManager.Instance.SetLanguage("zh-Hans");
            Main.InitializeItemAnimations();
            object host=Get(context,"Recovery"),shell=Get(context,"Shell"),state=Get(shell,"State"),ui=Get(shell,"RecoveryUi"),renderer=Get(shell,"renderer");
            Call(state,"Navigate",10);Call(state,"RestoreVisible");Call(renderer,"RefreshResources");
            Action<int,int,float,string> draw=(w,h,scale,name)=>
            {
                Call(renderer,"Prepare",state,(float)w,(float)h,scale);Call(ui,"PrepareLayout",Matrix.CreateScale(scale));
                graphics.LoadItemTextures(((IEnumerable)Get(ui,"visible")).Cast<object>().Select(part=>(int)Get(part,"Type")).Where(t=>t>0 && t<ItemID.Count));
                graphics.LoadItemTextures((int[])Get(Get(ui,"PotionPopup"),"Candidates"));
                Call(ui,"Prepare",true,Matrix.CreateScale(scale),new Vector2(w,h));
                graphics.Image(Path.Combine(output,name),()=>{Call(renderer,"Draw",state,Matrix.CreateScale(scale),false,false);Call(ui,"Draw",Get(shell,"drawKeyboard"),true);Call(Get(ui,"PotionPopup"),"Draw");},Matrix.CreateScale(scale),w,h);
            };
            draw(960,760,1,"recovery-controls.png");draw(1280,720,1.5f,"recovery-controls-150.png");
            Call(renderer,"Prepare",state,960f,760f,1f);Call(ui,"PrepareLayout",Matrix.Identity);
            var button=((IEnumerable)Get(ui,"visible")).Cast<object>().First(part=>(int)Get(part,"Command")==-2 && (int)Get(part,"Value")==0);
            Call(ui,"Execute",button);draw(960,760,1,"recovery-life-catalogue.png");draw(960,440,1,"recovery-small-viewport.png");
            Call(ui,"Suspend");draw(960,760,1,"recovery-controls-reset.png");
            var buffPrefs=(RecoverySettings)Get(host,"Buffs");NativeRecoveryChecks.Save(buffPrefs,new RecoveryOptions(allowedBuffs:new int[]{ItemID.RegenerationPotion}));
            Main.LocalPlayer.inventory[9].SetDefaults(ItemID.IronskinPotion);
            Call(ui,"Refresh");draw(960,760,1,"recovery-buff-two-panes.png");draw(1280,720,1.5f,"recovery-buff-two-panes-150.png");
            NativeRecoveryChecks.Save(buffPrefs,new RecoveryOptions());
            Call(ui,"Suspend");Call(state,"Navigate",1);draw(960,760,1,"recovery-tax-misc.png");
            Call(state,"Close");Call(ui,"Suspend");
            var prefs=(RecoverySettings)Get(host,"Buffs");NativeRecoveryChecks.Save(prefs,new RecoveryOptions(followRemove:true,allowedBuffs:new int[]{ItemID.IronskinPotion}));
            var p=Main.LocalPlayer;Array.Clear(p.buffType,0,p.buffType.Length);Array.Clear(p.buffTime,0,p.buffTime.Length);p.AddBuff(BuffID.Ironskin,3600);
            NativeQuickItemChecks.Sample(Get(context,"Input"),new Microsoft.Xna.Framework.Input.Keys[0]);
            graphics.LoadBuffTexture(BuffID.Ironskin);Main.mouseX=Main.mouseY=15;Main.mouseRight=Main.mouseRightRelease=true;
            graphics.Image(Path.Combine(output,"manual-buff-icon-cancel.png"),()=>Main.DrawBuffIcon(-1,p.FindBuffIndex(BuffID.Ironskin),10,10),Matrix.Identity);
            Require(p.FindBuffIndex(BuffID.Ironskin)<0,"actual drawn native icon cancels buff");
            Call(host,"Poll");NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !prefs.Busy;});
            Require(!prefs.Value.BuffAllowed(ItemID.IronskinPotion),"actual manual icon cancellation removes corresponding allowed item");
            int fairy=NativeRecoveryPetChecks.ManualFairy(context);
            graphics.LoadBuffTexture(fairy);Main.mouseX=Main.mouseY=15;Main.mouseRight=Main.mouseRightRelease=true;
            graphics.Image(Path.Combine(output,"manual-fairy-icon-cancel.png"),()=>Main.DrawBuffIcon(-1,p.FindBuffIndex(fairy),10,10),Matrix.Identity);
            Require(p.FindBuffIndex(fairy)<0,"actual native fairy-variant icon cancels buff");
            Call(host,"Poll");NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !prefs.Busy;});
            Require(!prefs.Value.BuffAllowed(ItemID.FairyBell) && prefs.Value.BuffAllowed(1183),"variant cancellation removes Fairy Bell only, not another light-pet provider");
            Main.mouseRight=false;Main.mouseRightRelease=true;p.mouseInterface=false;NativeRecoveryChecks.Save(prefs,new RecoveryOptions());
            Console.WriteLine("PASS G07 graphics: native Chinese F5 at normal/150/small sizes, real catalogue and actual icon-cancel follow removal.");
        }
    }
}
