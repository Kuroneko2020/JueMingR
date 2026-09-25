using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Recovery;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Utilities;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Full production composition, synthetic players/items and a blocked network
    // outlet. Neither a real game process nor a saved character is involved.
    internal static class NativeRecoveryBackgroundChecks
    {
        private static bool throwAfterUse;
        internal static void Run(object context)
        {
            object host=Get(context,"Recovery"),input=Get(context,"Input"),shell=Get(context,"Shell"),runtime=Get(host,"Runtime"),use=Get(host,"BuffUse");
            var buffs=(RecoverySettings)Get(host,"Buffs");var potions=(RecoverySettings)Get(host,"Potions");var services=(RecoverySettings)Get(host,"Services");
            var p=Main.LocalPlayer;bool updates=Main.CanUpdateGameplay;var foreground=Get(input,"foregroundWindow");var bobber=Main.projectile[0];
            var patch=new Harmony("JueMingR.Tests.BackgroundBuffFault");
            var outlet=typeof(Player).GetMethod("QuickBuff_UseItemForBuff",BindingFlags.Instance|BindingFlags.NonPublic);
            try
            {
                Main.ToggleGameplayUpdates(true);Main.gamePaused=false;Main.npcChatText="";p.SetTalkNPC(-1);p.potionDelay=0;
                foreach(var item in p.inventory)item.TurnToAir();foreach(var item in p.bank4.item)item.TurnToAir();
                Array.Clear(p.buffType,0,p.buffType.Length);Array.Clear(p.buffTime,0,p.buffTime.Length);
                p.inventory[0].SetDefaults(ItemID.WoodFishingPole);p.selectedItemState.Select(0);p.selectedItemState.Update();
                p.inventory[2].SetDefaults(ItemID.FishingPotion);p.inventory[2].stack=3;p.inventory[3].SetDefaults(ItemID.SonarPotion);p.inventory[3].stack=3;
                p.inventory[4].SetDefaults(ItemID.HealingPotion);p.inventory[4].stack=3;p.statLifeMax2=500;p.statLife=300;
                NativeRecoveryChecks.Save(potions,new RecoveryOptions(1,mana:true));NativeRecoveryChecks.Save(services,new RecoveryOptions(nurse:true,furniture:true,tax:true));
                NativeRecoveryChecks.Save(buffs,new RecoveryOptions(buffs:true,followAdd:true,followRemove:true,allowedBuffs:new int[]{ItemID.FishingPotion,ItemID.SonarPotion}));
                long generation=(long)Get(runtime,"Generation");
                Set(input,"foregroundWindow",(Func<IntPtr>)(()=>new IntPtr(2)));FocusHelper.IsSelectedApplication=false;
                NativeQuickItemChecks.Sample(input,new Keys[0]);
                Require(!(bool)Get(input,"IsFocused") && !(bool)Get(input,"CanStartActions") && !((Func<bool>)Get(host,"CanGameplay"))(),"real focus quarantine still denies all foreground input");
                // mouseInterface is reset in native Draw and may remain true
                // while minimized. Buffs ignore it only in background.
                p.mouseInterface=true;p.channel=true;p.controlUseItem=true;p.itemAnimation=12;p.itemTime=8;
                var rod=p.inventory[0];Main.projectile[0]=new Projectile{active=true,bobber=true,owner=p.whoAmI};Main.projectile[0].ai[0]=0;
                object nurse=Get(host,"Nurse"),tax=Get(host,"Tax"),furniture=Get(host,"Furniture"),healing=Get(host,"PotionsUse");
                Func<long[]> otherCalls=()=>new[]{(long)Get(nurse,"NativeCalls"),(long)Get(tax,"NativeCalls"),(long)Get(furniture,"NativeCalls"),(long)Get(healing,"NativeCalls")};var before=otherCalls();
                Call(context,"UpdateRuntime");Call(host,"Update",10000UL);Call(host,"Update",10001UL);
                Require((long)Get(runtime,"Generation")==generation,"focus change keeps actual player/world session identity");
                Require(p.FindBuffIndex(BuffID.Fishing)>=0 && p.FindBuffIndex(BuffID.Sonar)>=0 && p.inventory[2].stack==2 && p.inventory[3].stack==2,"background actual QuickBuff completes two missing effects once");
                Require(ReferenceEquals(rod,p.inventory[0]) && p.selectedItem==0 && p.channel && p.controlUseItem && p.itemAnimation==12 && p.itemTime==8 && Main.projectile[0].active && Main.projectile[0].ai[0]==0,"background buff preserves held rod, casting state and native bobber");
                Require(before.SequenceEqual(otherCalls()) && p.statLife==300 && p.inventory[4].stack==3,"background exception does not enable healing, mana or nearby services");
                Call(host,"Poll");Require(!buffs.Busy && buffs.Value.AllowedBuffs.Count==2,"automatic use never becomes a manual follow event");
                p.ClearBuff(BuffID.Fishing);p.ClearBuff(BuffID.Sonar);
                Action<Action,Action,string> blocked=(enter,leave,name)=>{enter();try{Call(host,"Update",10100UL);Require(p.inventory[2].stack==2 && p.FindBuffIndex(BuffID.Fishing)<0,"background refuses "+name);}finally{leave();}};
                blocked(()=>Main.gamePaused=true,()=>Main.gamePaused=false,"pause");
                blocked(()=>Main.ToggleGameplayUpdates(false),()=>Main.ToggleGameplayUpdates(true),"stopped native gameplay updates");
                blocked(()=>Main.mapFullscreen=true,()=>Main.mapFullscreen=false,"fullscreen map");
                blocked(()=>Call(Get(shell,"State"),"RestoreVisible"),()=>Call(Get(shell,"State"),"Close"),"F5 controls");
                blocked(()=>Main.drawingPlayerChat=true,()=>Main.drawingPlayerChat=false,"chat");
                blocked(()=>Main.mouseItem.SetDefaults(ItemID.DirtBlock),()=>Main.mouseItem.TurnToAir(),"mouse item");
                blocked(()=>p.chest=0,()=>p.chest=-1,"open container");
                Call(host,"Update",10101UL);Require(p.inventory[2].stack==1,"same source resumes after actual safety gate clears");

                p.ClearBuff(BuffID.Fishing);p.inventory[2].stack=3;
                patch.Patch(outlet,postfix:new HarmonyMethod(typeof(NativeRecoveryBackgroundChecks).GetMethod(nameof(AfterUse),BindingFlags.Static|BindingFlags.NonPublic)));
                throwAfterUse=true;Call(host,"Update",10102UL);
                Require(p.inventory[2].stack==2 && (((ulong[])Get(host,"UnknownSlots"))[0]&(1UL<<2))!=0,"exception after real background consumption protects unknown source");
                p.ClearBuff(BuffID.Fishing);
                Set(input,"foregroundWindow",foreground);FocusHelper.IsSelectedApplication=true;
                NativeQuickItemChecks.Sample(input,new Keys[0]);NativeQuickItemChecks.Sample(input,new Keys[0]);
                Require(!(bool)Call(host,"AdmitBuff",p),"foreground still honors mouse interface");p.mouseInterface=false;
                Call(context,"UpdateRuntime");Call(host,"Update",10103UL);
                Set(input,"foregroundWindow",(Func<IntPtr>)(()=>new IntPtr(2)));FocusHelper.IsSelectedApplication=false;NativeQuickItemChecks.Sample(input,new Keys[0]);Call(context,"UpdateRuntime");
                for(ulong t=10104;t<10704;t++)Call(host,"Update",t);
                Require(p.inventory[2].stack==2 && p.FindBuffIndex(BuffID.Fishing)<0 && p.FindBuffIndex(BuffID.Sonar)>=0 && (long)Get(runtime,"Generation")==generation,"focus cycling never replays unknown consumption; independent buff still progresses");
                NativeRecoveryChecks.Save(buffs,new RecoveryOptions());long reads=(long)Get(use,"CandidateReads"),definitions=(long)Get(use,"DefinitionReads");
                for(ulong t=10800;t<11400;t++)Call(host,"Update",t);
                Require(reads==(long)Get(use,"CandidateReads") && definitions==(long)Get(use,"DefinitionReads"),"background OFF retains zero buff scan work");
                Console.WriteLine("PASS G07 background: actual focus quarantine, native buffs, fishing state, UI/pause gates, other features unchanged, unknown consumption survives focus cycling.");
            }
            finally
            {
                throwAfterUse=false;patch.Unpatch(outlet,HarmonyPatchType.All,patch.Id);Set(input,"foregroundWindow",foreground);FocusHelper.IsSelectedApplication=true;
                Main.ToggleGameplayUpdates(updates);Main.projectile[0]=bobber;p.mouseInterface=false;p.channel=p.controlUseItem=false;p.itemAnimation=p.itemTime=0;
                NativeRecoveryChecks.Save(buffs,new RecoveryOptions());NativeRecoveryChecks.Save(potions,new RecoveryOptions());NativeRecoveryChecks.Save(services,new RecoveryOptions());
                Main.gameMenu=true;Call(context,"UpdateRuntime");Main.gameMenu=false;Call(context,"UpdateRuntime");NativeQuickItemChecks.Sample(input,new Keys[0]);NativeQuickItemChecks.Sample(input,new Keys[0]);
            }
        }
        private static void AfterUse(){if(throwAfterUse){throwAfterUse=false;throw new InvalidOperationException("isolated failure after actual buff consumption");}}
    }
}
