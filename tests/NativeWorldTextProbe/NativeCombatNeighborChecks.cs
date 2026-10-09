using System;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Features.Fishing;
using JueMingR.Features.Recovery;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // These combinations keep the real Aim provider installed and begin with
    // a useful prepared ordinary attack. They use the existing original-game
    // component drivers; no provider substitute, extra attack or AI skip.
    internal static class NativeCombatNeighborChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static void Run(object context)
        {
            var input=Get(context,"Input");var tools=Get(context,"Tools");var observation=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");
            // The Integration entry already completed original content/entity
            // initialization. TileObjectData is locked after startup.
            try
            {
                var p=Ready(context);
                var recovery=Get(context,"Recovery");var settings=(RecoverySettings)Get(recovery,"Potions");NativeQuickItemChecks.Until(()=>{Call(recovery,"Poll");return settings.Loaded;});
                p.statLifeMax2=500;p.statLife=449;p.potionDelay=0;p.inventory[3].SetDefaults(188);p.inventory[3].stack=4;
                NativeRecoveryChecks.Save(settings,new RecoveryOptions(2));NativeToolExecutionChecks.Sample(context,input,Main.npc[2].Center,true);Call(Get(context,"Combat"),"Sample");NativeQuickItemChecks.BeginWorldStep();NativeCombatAimChecks.Prepare(observation,attack,Main.npc[2],0,true);int x=Main.mouseX,y=Main.mouseY;var weapon=p.HeldItem;
                Call(recovery,"Update",(ulong)Main.GameUpdateCount);
                Console.WriteLine("NEIGHBOR heal life="+p.statLife+" stack="+p.inventory[3].stack+" delay="+p.potionDelay+" weapon="+ReferenceEquals(p.HeldItem,weapon)+" use="+p.controlUseItem+" cursor="+(Main.mouseX==x && Main.mouseY==y)+" admit="+Call(recovery,"Admit",p)+" error="+GetOptional(recovery,"Error"));
                Require(p.statLife==500 && p.inventory[3].stack==3 && p.potionDelay>0 && ReferenceEquals(p.HeldItem,weapon) && Main.mouseX==x && Main.mouseY==y && p.controlUseItem,"real native QuickHeal consumes only its supply while the useful Aim plan retains the selected weapon and physical input");
                NativeRecoveryChecks.Save(settings,new RecoveryOptions());Restore(context);
                Console.WriteLine("NEIGHBOR recovery actual heal500/stack3/native cooldown; real Aim provider/weapon/cursor preserved");

                p=Ready(context);p.inventory[1].SetDefaults(ItemID.CopperPickaxe);p.selectedItemState.Select(1);p.selectedItemState.Update();p.itemTime=p.itemAnimation=0;p.releaseUseItem=true;
                NativeToolsChecks.Tile(48,41,6);NativeToolsChecks.Tile(48,40,6);var point=new Vector2(48*16+8,41*16+8);NativeToolsChecks.SetMode(tools,2,2);
                bool removed=false;for(int i=0;i<100 && !removed;i++)
                {NativeToolExecutionChecks.Sample(context,input,point,true);Call(Get(context,"Combat"),"Sample");NativeQuickItemChecks.BeginWorldStep();x=Main.mouseX;y=Main.mouseY;p.Update(0);Call(context,"UpdateRuntime");Require(Main.mouseX==x && Main.mouseY==y,"tool goal returns the actual physical cursor while Aim is enabled");removed=!Main.tile[48,41].active();}
                Console.WriteLine("NEIGHBOR mine removed="+removed+" impact="+(GetOptional(attack,"ExpectedImpact")!=null)+" selected="+p.selectedItem+" held="+p.HeldItem.type+" position="+p.position+" use="+p.controlUseItem+" animation="+p.itemAnimation+" toolTime="+p.toolTime+" mode="+Call(tools,"Mode",2)+" error="+GetOptional(tools,"Error"));
                Require(removed && GetOptional(attack,"ExpectedImpact")==null,"actual mining uses its native tile goal, not the prior NPC attack point");
                for(int i=0;i<100 && Main.tile[48,40].active();i++){NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),false);Call(Get(context,"Combat"),"Sample");NativeQuickItemChecks.BeginWorldStep();x=Main.mouseX;y=Main.mouseY;p.Update(0);Call(context,"UpdateRuntime");Require(Main.mouseX==x && Main.mouseY==y,"automatic retained vein returns physical mouse after its native tool scope");}
                Require(!Main.tile[48,40].active(),"retained native vein continues after the physical first-hit press is released, with Aim on");NativeToolsChecks.SetMode(tools,2,0);Restore(context);
                Console.WriteLine("NEIGHBOR mining actual ore removed; Aim prior plan retired; tool cursor returned; gun plan restored");

                p=Ready(context);var processing=Get(context,"Processing");p.inventory[12].SetDefaults(1774);p.inventory[12].stack=3;Main.playerInventory=true;
                Call(processing,"Set",0,true);NativeQuickItemChecks.Until(()=>{Call(processing,"Poll");return (bool)Call(processing,"Value",0);});
                NativeProcessingChecks.Sample(input,true);NativeQuickItemChecks.BeginWorldStep();Call(context,"UpdateRuntime");
                Require(p.inventory[12].stack==2 && GetOptional(attack,"ExpectedImpact")==null,"real continuous bag owns the UI gesture and invalidates the gameplay attack lease");
                NativeProcessingChecks.Sample(input,false);Call(context,"UpdateRuntime");Call(processing,"Set",0,false);NativeQuickItemChecks.Until(()=>{Call(processing,"Poll");return !(bool)Call(processing,"Value",0);});Main.playerInventory=false;Restore(context);
                Console.WriteLine("NEIGHBOR processing actual bag once; physical release stops; gameplay Aim restored after UI return");

                Fishing(context,input,tools,attack);
                Console.WriteLine("PASS Aim-on neighbors: original recovery, mining, UI bag and fishing claim/return with useful real-provider attack preparation.");
            }
            finally{Main.playerInventory=false;for(int i=0;i<3;i++)NativeToolsChecks.SetMode(tools,i,0);NativeFishingChecks.Save(Get(context,"Fishing"),new FishingOptions());NativeCombatObservationChecks.Save(observation,new ObservationOptions());}
        }
        private static Player Ready(object context)
        {
            var input=Get(context,"Input");var observation=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");
            NativeCombatObservationChecks.Save(observation,new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;p.releaseUseItem=true;p.controlUseItem=true;Main.screenPosition=new Vector2(600,400);Main.SmartCursorWanted_Mouse=Main.SmartCursorWanted_GamePad=false;
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.target=0;n.aiStyle=-1;n.noGravity=true;n.Center=new Vector2(1020,667);n.velocity=n.netOffset=Vector2.Zero;n.life=n.lifeMax=10000;Array.Clear(n.immune,0,n.immune.Length);
            NativeCombatObservationChecks.Save(observation,new ObservationOptions().Toggle(6));NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(Get(context,"Combat"),"Sample");NativeCombatAimChecks.Prepare(observation,attack,n,0,true);
            Require(GetOptional(attack,"ExpectedImpact")!=null,"neighbor combination begins with an enabled, useful ordinary Aim plan and production provider");return p;
        }
        private static void Restore(object context)
        {
            var p=Main.LocalPlayer;var input=Get(context,"Input");
            // Let the original animation/return boundary authorize selection;
            // selecting during the old tool animation merely buffers intent.
            for(int i=0;i<80 && !p.selectedItemState.CanChangeSelectedItemImmediately;i++){NativeToolExecutionChecks.Sample(context,input,Main.npc[2].Center,false);Call(Get(context,"Combat"),"Sample");NativeQuickItemChecks.BeginWorldStep();p.Update(0);Call(context,"UpdateRuntime");}
            Require(p.selectedItemState.CanChangeSelectedItemImmediately,"native prior use reaches its lawful selection-return boundary");p.selectedItemState.Select(0);p.selectedItemState.Update();p.controlUseItem=true;p.channel=false;
            var observation=Get(context,"CombatObservation");var n=Main.npc[2];NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(Get(context,"Combat"),"Sample");NativeCombatAimChecks.Prepare(observation,Get(Get(context,"Combat"),"Attack"),n,0,true);
            Require(p.HeldItem.type==95 && GetOptional(Get(Get(context,"Combat"),"Attack"),"ExpectedImpact")!=null,"normal gun source/real provider returns after the neighbor ends");
        }
        private static void Fishing(object context,object input,object tools,object attack)
        {
            var p=Ready(context);var host=Get(context,"Fishing");p.inventory[1].SetDefaults(ItemID.WoodFishingPole);p.selectedItemState.Select(1);p.selectedItemState.Update();p.itemTime=p.itemAnimation=0;p.releaseUseItem=true;p.armor[3].SetDefaults(ItemID.HighTestFishingLine);p.inventory[54].SetDefaults(ItemID.Worm);p.inventory[54].stack=100;
            for(int tx=44;tx<74;tx++)for(int ty=42;ty<61;ty++){Main.tile[tx,ty].ClearEverything();if(ty>=44 && ty<60)Main.tile[tx,ty].liquid=255;if(ty==60)NativeToolsChecks.Tile(tx,ty,1);}
            Main.FishDropsDB=new Terraria.GameContent.FishDropRules.FishDropRuleList();new Terraria.GameContent.FishDropRules.GameContentFishDropPopulator(Main.FishDropsDB).Populate();
            var chum=typeof(Main).GetField("ChumBucketProjectileHelper",Flags);var previous=chum.GetValue(Main.instance);chum.SetValue(Main.instance,Activator.CreateInstance(chum.FieldType));
            try
            {
                NativeFishingChecks.Save(host,new FishingOptions(auto:true));var b=NativeFishingChecks.CastToWaiting(context,input);int key=(int)b.key,before=p.inventory.Where(i=>i.type==ItemID.Bass).Sum(i=>i.stack);
                Require(GetOptional(attack,"ExpectedImpact")==null,"native rod/window does not consume the old gun contact");b.ai[1]=-120;b.localAI[1]=ItemID.Bass;b.localAI[2]=ItemID.Worm;
                Projectile child=null;for(int i=0;i<240 && child==null;i++){NativeFishingChecks.Step(context,input,new Vector2(300,640),false,0);child=Main.projectile.FirstOrDefault(q=>q.active && q.bobber && q.owner==0 && (int)q.key!=key);}
                Require(child!=null && child.velocity.X>0 && !Main.projectile.Any(q=>q.active && (int)q.key==key) && p.inventory.Where(i=>i.type==ItemID.Bass).Sum(i=>i.stack)==before+1,"actual fishing pull/product/recast retains the original rightward fishing aim despite current left mouse and Aim enabled");
                NativeFishingChecks.Save(host,new FishingOptions());foreach(var q in Main.projectile.Where(q=>q.active && q.bobber))q.active=false; // component teardown only, after native pull/recast proof
                p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;for(int tx=5;tx<115;tx++)for(int ty=42;ty<61;ty++)Main.tile[tx,ty].ClearEverything();for(int tx=5;tx<115;tx++)NativeToolsChecks.Tile(tx,43,1);p.position=new Vector2(700,646);p.velocity=Vector2.Zero;Restore(context);
                Console.WriteLine("NEIGHBOR fishing actual Bass/pull/recast right velocity="+child.velocity+"; current left mouse returns; gun source restored");
            }
            finally{chum.SetValue(Main.instance,previous);NativeFishingChecks.Save(host,new FishingOptions());}
        }
    }
}
