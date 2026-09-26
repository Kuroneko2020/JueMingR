using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeFishingBorrowChecks
    {
        // A deliberately eager future participant: ordinarily a disappeared
        // bobber would submit its own recast. The production loan contract
        // transfers that one decision, including terminal failure, to capture.
        private sealed class Participant
        {
            private long loan;private bool transferred;
            internal int Attempts;
            internal void Observe(object fish,bool bobberPresent)
            {
                long current=(long)Get(fish,"Token");
                if((bool)Get(fish,"OwnsRecast")){loan=current;transferred=true;return;}
                if(transferred && loan==current && Get(fish,"Phase").ToString()!="Idle")return;
                if(!bobberPresent)Attempts++;
            }
        }
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static int casts;
        private static Vector2 castTarget;
        private static void Observe(Item __0){if(__0.fishingPole>0){casts++;castTarget=Main.MouseWorld;}}
        internal static void Run(object context)
        {
            object host=Get(context,"Tools"),fish=Get(host,"Fishing"),input=Get(context,"Input");var p=Main.LocalPlayer;
            NativeToolsChecks.SetMode(host,0,1);foreach(var item in p.inventory)item.TurnToAir();p.inventory[17].SetDefaults(ItemID.WoodFishingPole);p.inventory[12].SetDefaults(1991);
            p.itemAnimation=p.itemTime=0;p.selectedItemState.Select(17);p.selectedItemState.Update();p.position=new Vector2(640,640);
            for(int i=0;i<Main.npc.Length;i++)Main.npc[i].active=false;Call(Get(host,"Npcs"),"BeginTick");
            var original=new Vector2(880,620);Main.mouseX=(int)original.X;Main.mouseY=(int)original.Y;Main.screenPosition=Vector2.Zero;
            Call(fish,"ObserveCast",p,p.HeldItem);var b=Bobber(p,0);b.ai[0]=1;
            Require((long)Call(fish,"Prepare",p)==0 && !(bool)Get(fish,"Active"),"manual reel-in does not authorize recovery");b.ai[0]=0;
            p.inventory[14].SetDefaults(213);for(int x=40;x<45;x++)for(int y=39;y<44;y++)Main.tile[x,y].ClearEverything();NativeToolsChecks.Tile(42,42,1);Require(WorldGen.PlaceTile(42,41,78,mute:true,forced:true,plr:0),"fishing neighbour pot setup");NativeToolsChecks.Tile(42,40,84);NativeToolsChecks.SetMode(host,1,1);
            for(int i=0;i<40;i++){NativeToolsChecks.Frame(context,input);Call(b,"AI_061_FishingBobber");}
            Require(b.active && p.selectedItem==17 && Main.tile[42,40].type==84,"automatic herbs yield to a real waiting bobber without ending ordinary fishing");NativeToolsChecks.SetMode(host,1,0);p.inventory[14].TurnToAir();
            long first=(long)Call(fish,"Prepare",p);Require(first>0 && (bool)Get(fish,"OwnsRecast"),"actual borrowed rod publishes single recast owner");
            var participant=new Participant();participant.Observe(fish,true);
            Override(p,12);
            object items=Get(host,"Items");p.inventory[20].SetDefaults(9);p.trashItem.TurnToAir();
            Call(items,"Change",new JueMingR.Features.Items.ItemAutomationSettings(false,false,true,new int[0],new[]{p.inventory[17].type,9},false));
            NativeQuickItemChecks.Until(()=>{Call(items,"PollPreferences");return (bool)Get(items,"ControlsEnabled");});
            for(int i=0;i<14;i++){NativeQuickItemChecks.Sample(input,new Microsoft.Xna.Framework.Input.Keys[0]);Call(context,"UpdateRuntime");}
            Require(p.inventory[17].fishingPole>0 && p.inventory[20].IsAir && p.trashItem.type==9,"actual automatic discard protects only the lent rod while an unrelated selected discard still executes");
            Call(items,"Change",JueMingR.Features.Items.ItemAutomationSettings.Default);NativeQuickItemChecks.Until(()=>{Call(items,"PollPreferences");return (bool)Get(items,"ControlsEnabled");});
            Call(b,"AI_061_FishingBobber");Require(!b.active,"real original bobber AI ends after selecting net");
            Call(fish,"NetFinished",first,false,false);p.itemAnimation=p.itemTime=0;p.selectedItemState.Update();
            Require(p.selectedItem==17,"native selection returns to exact main-bag rod above hotbar");
            var audit=new Harmony("JueMingR.Tests.G09Fishing");var method=typeof(Player).GetMethod("ItemCheck_StartActualUse",Flags);
            audit.Patch(method,postfix:new HarmonyMethod(typeof(NativeFishingBorrowChecks),nameof(Observe)));casts=0;
            try{for(int i=0;i<140;i++){participant.Observe(fish,Main.projectile.Any(q=>q.active && q.bobber));NativeToolsChecks.Frame(context,input);}}
            finally{audit.Unpatch(method,HarmonyPatchType.All,audit.Id);}
            Require(casts==1,"one actual native rod use after borrowed-net loss, actual="+casts+" phase="+Get(fish,"Phase"));
            Require(Main.projectile.Any(x=>x.active && x.bobber && x.owner==0),"native ItemCheck generated a real new bobber");
            Require(Vector2.DistanceSquared(original,castTarget)<2,"recast uses original world target instead of current mouse");
            Require((bool)Get(fish,"RecastAttempted") && (bool)Get(fish,"RecastObserved") && Get(fish,"Phase").ToString()=="Completed","terminal contract distinguishes attempted and observed native recast");
            participant.Observe(fish,false); // Even a later missing observation cannot replay the ended loan.
            Require(casts+participant.Attempts==1,"capture plus an eager synthetic G10 participant owns one attempt through active and terminal loan states");
            var ordinary=new Participant();ordinary.Observe(new NoLoan(),false);Require(ordinary.Attempts==1,"synthetic participant actually requests recast when no loan owns it");
            foreach(var projectile in Main.projectile)projectile.active=false;b=Bobber(p,0);long second=(long)Call(fish,"Prepare",p);
            Call(fish,"NetFinished",first,true,true);Require(Get(fish,"Phase").ToString()=="Borrowed","old capture completion cannot end new borrowing token");
            Call(fish,"NetFinished",second,true,false);for(int i=0;i<4;i++)NativeToolsChecks.Frame(context,input);
            Require(!((bool)Get(fish,"RecastAttempted")) && b.active && b.ai[0]==0,"surviving original bobber causes zero pull/recast");
            foreach(var projectile in Main.projectile)projectile.active=false;Bobber(p,0);long third=(long)Call(fish,"Prepare",p);Call(fish,"Cancel");Call(fish,"NetFinished",third,true,false);
            Require(Get(fish,"Phase").ToString()=="Cancelled","late callback cannot rewrite cancelled result");
            NativeToolsChecks.SetMode(host,0,0);foreach(var projectile in Main.projectile)projectile.active=false;
            Console.WriteLine("PASS G09 fishing: original bobber AI, main-bag rod return, one actual native recast toward original target, surviving/reeling bobbers and terminal token isolation.");
        }
        private sealed class NoLoan {public long Token {get{return 0;}}public bool OwnsRecast {get{return false;}}public string Phase {get{return "Idle";}}}
        private static Projectile Bobber(Player p,int index){var b=Main.projectile[index];b.SetDefaults(p.inventory[17].shoot);b.owner=p.whoAmI;b.active=true;b.position=new Vector2(850,650);Require(b.bobber,"original rod projectile metadata");return b;}
        private static void Override(Player p,int slot){object state=p.selectedItemState;typeof(Player.SelectedItemState).GetMethod("OverrideSelection",Flags).Invoke(state,new object[]{slot});p.selectedItemState=(Player.SelectedItemState)state;}
    }
}
