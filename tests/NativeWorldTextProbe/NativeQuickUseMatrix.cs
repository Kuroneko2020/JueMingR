using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.QuickItems;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeQuickUseMatrix
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static int shots;
        internal static void Run(object context,QuickItemEntry entry)
        {
            object quick=Get(context,"QuickItems"),use=Get(quick,"Use"),input=Get(context,"Input"),shell=Get(context,"Shell");
            var settings=(QuickItemSettings)Get(quick,"Settings");Player p=Main.LocalPlayer;
            var audit=new Harmony("JueMingR.Tests.QuickUseMatrix");
            audit.Patch(typeof(Player).GetMethod("ItemCheck_Shoot",Flags),postfix:new HarmonyMethod(typeof(NativeQuickUseMatrix).GetMethod(nameof(Shot),Flags)));
            try
            {
                Change(quick,entry.With(ItemID.ClockworkAssaultRifle,QuickItemMode.Use,false,true));
                Reset(p);p.inventory[17].SetDefaults(ItemID.ClockworkAssaultRifle);p.inventory[54].SetDefaults(ItemID.MusketBall);p.inventory[54].stack=30;
                p.selectedItemState.Select(17);p.selectedItemState.Update();shots=0;
                for(int i=0;i<100;i++) {NativeQuickItemChecks.Sample(input,new Keys[0]);PlayerInput.Triggers.Current.MouseLeft=i==0;NativeQuickItemChecks.NativeFrame(p);}
                int ordinaryShots=shots,ordinaryAmmo=30-p.inventory[54].stack;
                Require(ordinaryShots>1 && ordinaryAmmo>0,"real burst baseline starts and consumes native ammunition");
                Reset(p);p.inventory[17].SetDefaults(ItemID.ClockworkAssaultRifle);p.inventory[54].SetDefaults(ItemID.MusketBall);p.inventory[54].stack=30;shots=0;
                Press(input,shell,Keys.J);
                for(int i=0;i<100;i++) {NativeQuickItemChecks.NativeFrame(p);NativeQuickItemChecks.Sample(input,new[]{Keys.W});Call(shell,"ProcessInput");}
                Require(shots==ordinaryShots && 30-p.inventory[54].stack==ordinaryAmmo,"quick native burst matches ordinary click; ordinary="+ordinaryShots+"/"+ordinaryAmmo+" quick="+shots+"/"+(30-p.inventory[54].stack));
                Returned(p,use,2,"burst/reuse completion");
                // Ordinary consumable and tool: real native effect/quantity,
                // no manufactured completion or replacement of ItemCheck.
                Change(quick,entry.With(ItemID.LesserHealingPotion,QuickItemMode.Use,false,true));
                Reset(p);p.statLifeMax2=100;p.statLife=20;p.potionDelay=0;p.inventory[17].SetDefaults(ItemID.LesserHealingPotion);p.inventory[17].stack=1;
                Press(input,shell,Keys.J);NativeQuickItemChecks.NativeFrame(p);Frames(input,shell,p,100);
                Require(p.statLife>20 && p.inventory[17].stack==0,"last potion applies native healing and is consumed once");Returned(p,use,2,"last consumable");
                foreach(int target in new[]{5391,5453,5060,5454,5455,5329,5360,6169,6195,5325,5526})
                {
                    Change(quick,entry.With(target,QuickItemMode.SetState,false,true));Reset(p);
                    int source=target;do{source=QuickItemRules.NextState(source);}while(QuickItemRules.NextState(source)!=target);
                    p.inventory[17].SetDefaults(source);p.inventory[17].favorited=true;Item physical=p.inventory[17];int prefix=physical.prefix;
                    Press(input,shell,Keys.J);NativeQuickItemChecks.NativeFrame(p);
                    Require(ReferenceEquals(p.inventory[17],physical) && physical.type==target && physical.stack==1 && physical.prefix==prefix && physical.favorited,"native state edge preserves physical attributes: "+source+"→"+target);
                    Returned(p,use,2,"state-only no selected override");
                }
                Change(quick,entry.With(50,QuickItemMode.Use,true,true));Reset(p);p.inventory[17].SetDefaults(3199);
                Press(input,shell,Keys.J);NativeQuickItemChecks.NativeFrame(p);Require(p.selectedItem==17,"compatible ice mirror selected from main bag");
                // Later manual choices have precedence over the native return.
                p.selectedItemState.Select(4);p.selectedItemState.Select(6);Frames(input,shell,p,130);Returned(p,use,6,"latest manual selection during use");
                Handoff(context,entry);
                MouseHandoff(context,entry,ordinaryShots,ordinaryAmmo);
                Console.WriteLine("PASS: real native burst differential, last consumable, all 11 state families, compatible provider and manual selection precedence.");
            }
            finally {foreach(var method in audit.GetPatchedMethods().ToArray())audit.Unpatch(method,HarmonyPatchType.All,audit.Id);Change(quick,entry);Reset(p);}
        }
        private static void Shot(Item sItem){if(sItem.type==ItemID.ClockworkAssaultRifle)shots++;}
        private static void MouseHandoff(object context,QuickItemEntry entry,int ordinaryShots,int ordinaryAmmo)
        {
            object quick=Get(context,"QuickItems"),input=Get(context,"Input"),shell=Get(context,"Shell"),use=Get(quick,"Use");Player p=Main.LocalPlayer;
            var settings=(QuickItemSettings)Get(quick,"Settings");var bindings=(HotkeyBindings)Get(Get(shell,"hotkeys"),"Bindings");
            var b=new QuickItemEntry("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",50,QuickItemMode.Use,false,true);string reason;
            Require(settings.TryChange(settings.Current.Change(entry.With(ItemID.ClockworkAssaultRifle,QuickItemMode.Use,false,true)).Change(b),entry.Id,out reason),"mouse A/B save");
            Until(()=>{Call(quick,"Poll");return !settings.Busy;});
            NativeQuickGestureChecks.Bind(bindings,entry.ActionId,"Mouse1");NativeQuickGestureChecks.Bind(bindings,b.ActionId,"Mouse2");
            try
            {
                Reset(p);p.inventory[17].SetDefaults(ItemID.ClockworkAssaultRifle);p.inventory[18].SetDefaults(50);
                p.inventory[54].SetDefaults(ItemID.MusketBall);p.inventory[54].stack=30;shots=0;int recalls=NativeQuickItemChecks.Recalls;
                NativeQuickGestureChecks.Frame(input,shell,p,0);NativeQuickGestureChecks.Frame(input,shell,p,1);
                NativeQuickGestureChecks.Frame(input,shell,p,3); // Busy B is consumed, never queued.
                int frames=0;while(!p.selectedItemState.CanChangeSelectedItemImmediately && frames++<120)NativeQuickGestureChecks.Frame(input,shell,p,1);
                Require(frames<120 && shots==ordinaryShots && 30-p.inventory[54].stack==ordinaryAmmo,"Mouse1 native burst includes final shot/ammo and does not repeat while held");
                Require(NativeQuickItemChecks.Recalls==recalls,"busy Mouse2 never used B");
                NativeQuickGestureChecks.Frame(input,shell,p,3);
                Require(p.selectedItem==18 && p.itemAnimation>0,"fresh Mouse2 starts B on first free frame while Mouse1 is still held");
                for(int i=0;i<160;i++)NativeQuickGestureChecks.Frame(input,shell,p,3);
                Require(shots==ordinaryShots && NativeQuickItemChecks.Recalls==recalls+1,"held A/B each produces one completed use");
                Returned(p,use,2,"Mouse1/Mouse2 fast handoff final state");
            }
            finally
            {
                NativeQuickGestureChecks.Frame(input,shell,p,0);NativeQuickGestureChecks.Bind(bindings,entry.ActionId,"J");
                Require((bool)Call(quick,"Delete",b.Id),"remove mouse B");Until(()=>{Call(quick,"Poll");bindings.Poll();return !settings.Busy && !bindings.Busy;});
            }
        }
        private static void Handoff(object context,QuickItemEntry entry)
        {
            object quick=Get(context,"QuickItems"),input=Get(context,"Input"),shell=Get(context,"Shell"),use=Get(quick,"Use");Player p=Main.LocalPlayer;
            var settings=(QuickItemSettings)Get(quick,"Settings");var bindings=(HotkeyBindings)Get(Get(shell,"hotkeys"),"Bindings");
            var a=entry.With(ItemID.ClockworkAssaultRifle,QuickItemMode.Use,false,true);
            var b=new QuickItemEntry("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",50,QuickItemMode.Use,false,true);string reason;
            Require(settings.TryChange(settings.Current.Change(a).Change(b),a.Id,out reason),"A/B entry save");Until(()=>{Call(quick,"Poll");return !settings.Busy;});
            HotkeyChord key;HotkeyChord.TryParse("K",out key,out reason);long command;Require(bindings.TrySet(b.ActionId,key,null,out command,out reason),"B binding");Until(()=>{bindings.Poll();return !bindings.Busy;});
            Reset(p);p.inventory[7]=new Item();p.inventory[17].SetDefaults(ItemID.ClockworkAssaultRifle);p.inventory[18].SetDefaults(50);p.inventory[54].stack=30;
            Press(input,shell,Keys.J);NativeQuickItemChecks.NativeFrame(p);long old=(long)Get(use,"Operation");
            Press(input,shell,Keys.K);Require((long)Get(use,"Operation")==old,"B while genuinely busy is rejected without queuing");NativeQuickItemChecks.NativeFrame(p);
            int frames=0;while(!p.selectedItemState.CanChangeSelectedItemImmediately && frames++<120){NativeQuickItemChecks.Sample(input,new[]{Keys.W});Call(shell,"ProcessInput");NativeQuickItemChecks.NativeFrame(p);}
            Require(frames<120 && (bool)Get(use,"Active"),"A ends naturally immediately before next native selection update");
            Press(input,shell,Keys.K);Require((long)Get(use,"Operation")!=old && (bool)Get(use,"Active"),"B new press accepted on first actually available frame");
            NativeQuickItemChecks.NativeFrame(p);Require(p.selectedItem==18 && p.itemAnimation>0,"B actually begins using its provider");
            long current=(long)Get(use,"Operation");Main.mouseLeft=true;
            Call(use,"EndItemCheck",p,old,true,true,new InvalidOperationException("isolated late A callback"));
            Require((long)Get(use,"Operation")==current && Main.mouseLeft && p.selectedItem==18,"late A cannot clear B input/selection/ownership");Main.mouseLeft=false;
            Frames(input,shell,p,130);Returned(p,use,2,"rapid A/B full completion");
            Require((bool)Call(quick,"Delete",b.Id),"remove isolated B entry");Until(()=>{Call(quick,"Poll");bindings.Poll();return !settings.Busy && !bindings.Busy;});
        }
        internal static void Change(object quick,QuickItemEntry entry)
        {var settings=(QuickItemSettings)Get(quick,"Settings");string reason;Require(settings.TryChange(settings.Current.Change(entry),entry.Id,out reason),"matrix save: "+reason);Until(()=>{Call(quick,"Poll");return !settings.Busy;});}
        internal static void Until(Func<bool> predicate){var until=DateTime.UtcNow.AddSeconds(8);while(!predicate()){if(DateTime.UtcNow>until)throw new Exception("matrix worker timeout");Thread.Sleep(1);}}
        internal static void Reset(Player p)
        {
            p.itemAnimation=p.itemTime=p.reuseDelay=0;p.channel=false;p.controlUseItem=false;p.releaseUseItem=true;p.noItems=false;
            p.selectedItemState.Select(2);p.selectedItemState.Update();Main.mouseItem=new Item();Main.mouseLeft=Main.mouseRight=false;Main.blockMouse=false;
            for(int i=0;i<Main.projectile.Length;i++)Main.projectile[i]=new Projectile();
        }
        internal static void Press(object input,object shell,Keys key)
        {NativeQuickItemChecks.Sample(input,new Keys[0]);Call(shell,"ProcessInput");NativeQuickItemChecks.Sample(input,new[]{key});Call(shell,"ProcessInput");}
        internal static void Frames(object input,object shell,Player p,int count)
        {for(int i=0;i<count;i++){NativeQuickItemChecks.Sample(input,new[]{Keys.W,Keys.Space});Call(shell,"ProcessInput");NativeQuickItemChecks.NativeFrame(p);}}
        internal static void Returned(Player p,object use,int selected,string scenario)
        {Require(p.selectedItem==selected && !(bool)Get(use,"Active") && !p.controlUseItem && !Main.mouseLeft && Main.mouseItem.IsAir,scenario+" returns selection and input; selected="+p.selectedItem);}
    }
}
