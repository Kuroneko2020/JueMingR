using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Normal native Player -> NPC -> Projectile -> Runtime updates, with the
    // real Set command and buffered selection. No fixture future, permission
    // override or manual source registration is used by this chain.
    internal static class NativeCombatPrismChainChecks
    {
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var tools=Get(context,"Tools");var input=Get(context,"Input");
            var attack=Get(combat,"Attack");var control=Get(attack,"Control");
            foreach(var entry in new[]{tools,Get(tools,"Items"),combat})
            {
                Console.WriteLine("PRISM INSTALL owner="+entry.GetType().Name+" available="+Get(entry,"Available")+" error="+GetOptional(entry,"SetupError"));
                Require((bool)Get(entry,"Available"),"actual native combat dependency installed: "+entry.GetType().Name+" error="+GetOptional(entry,"SetupError"));
            }
            NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
            var p=NativeToolExecutionChecks.Reset(context,tools,input,95,0,0);
            p.position=new Vector2(700,688-p.height);p.releaseUseItem=true;
            p.inventory[1].SetDefaults(3541);p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;
            p.statMana=p.statManaMax=p.statManaMax2=200;
            Main.screenPosition=new Vector2(550,350);
            var target=Main.npc[2];target.SetDefaults(3);target.whoAmI=2;target.active=true;target.Center=new Vector2(1100,650);
            target.velocity=target.netOffset=Vector2.Zero;target.aiStyle=-1;target.noGravity=true;target.life=target.lifeMax=100000;target.knockBackResist=0;
            Array.Clear(target.immune,0,target.immune.Length);
            var mouse=new Vector2(520,420);
            // CPU has no water scene render target. Reuse the exact existing
            // cosmetic outlet isolation; keep all parent/child AI and damage.
            var cosmetic=new Harmony("JueMingR.Tests.PrismChainRipple");
            cosmetic.Patch(typeof(Projectile).GetMethod("AI"),transpiler:new HarmonyMethod(typeof(NativeCombatBeamChecks).GetMethod("RippleOutlet",BindingFlags.NonPublic|BindingFlags.Static)));
            try
            {
                Step(context,mouse,false);Step(context,mouse,true);Step(context,mouse,false);
                Call(host,"Set",6,true);Committed(host);
                Require(((ObservationOptions)Get(host,"Options")).Aim,"real Set(6,true) commits aim");
                for(int i=0;i<35;i++)Step(context,mouse,false);
                Step(context,mouse,true);Step(context,mouse,false);
                // Select is the same native buffer used by the hotbar; Update
                // consumes it in Player.Update instead of changing selectedItem.
                p.selectedItemState.Select(1);
                for(int i=0;i<35;i++)Step(context,mouse,false);
                Require(p.selectedItem==1 && p.HeldItem.type==3541,"normal buffered selection reaches the new prism hand");
                Projectile parent=null;
                for(int i=0;i<80;i++)
                {
                    Step(context,mouse,true);
                    parent=Main.projectile.FirstOrDefault(q=>q.active && q.owner==p.whoAmI && q.type==633);
                    if(i==0 || i==10 || i==40 || i==79)State(host,combat,attack,control,parent,i);
                }
                Require(parent!=null,"original ItemCheck naturally creates a new 633 after another weapon");
                var toward=(target.Center-p.MountedCenter).SafeNormalize(Vector2.UnitX);
                var physical=(mouse-p.MountedCenter).SafeNormalize(Vector2.UnitY);
                var actual=parent.velocity.SafeNormalize(Vector2.UnitY);
                float targetDot=Vector2.Dot(actual,toward),mouseDot=Vector2.Dot(actual,physical);
                Console.WriteLine("PRISM C2 RESULT key="+parent.key+" targetDot="+targetDot+" physicalDot="+mouseDot+" children="+Main.projectile.Count(q=>q.active && q.type==632)+" mana="+p.statMana+" velocity="+parent.velocity);
                Require(targetDot>.95f && targetDot>mouseDot+.5f,"natural prism direction converges toward the selected target, separated from the physical mouse");
                Require(Main.projectile.Count(q=>q.active && q.type==632)==6,"native prism retains its six child beams");
                Console.WriteLine("PASS prism C2 real Set/other weapon/buffered selection/new633/off-axis mouse/normal budget; isolated native chain, not desktop gameplay or multiplayer");
            }
            finally{try{Call(host,"Set",6,false);Committed(host);for(int i=0;i<8;i++)Step(context,mouse,false);}finally{cosmetic.UnpatchAll(cosmetic.Id);}}
        }
        private static void Committed(object host)
        {var settings=(ObservationSettings)Get(host,"Settings");NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !settings.Busy;});Require(settings.CompletionSucceeded,"actual Set save completes: "+settings.Message);}
        internal static void RunLifecycle(object context)
        {RunLifecycleCore(context,new[]{3541,2882});}
        internal static void RunRemainingLifecycle(object context)
        {RunLifecycleCore(context,new[]{2882});}
        private static void RunLifecycleCore(object context,int[] weapons)
        {
            var cosmetic=new Harmony("JueMingR.Tests.PrismLifecycleRipple");
            cosmetic.Patch(typeof(Projectile).GetMethod("AI"),transpiler:new HarmonyMethod(typeof(NativeCombatBeamChecks).GetMethod("RippleOutlet",BindingFlags.NonPublic|BindingFlags.Static)));
            try
            {
                // 460 shares the same source gate but has a different native
                // charge/read window; no all-weapon matrix is needed here.
                foreach(int weapon in weapons)Lifecycle(context,weapon);
                Retirement(context);
            }
            finally{cosmetic.UnpatchAll(cosmetic.Id);}
        }
        private static void Lifecycle(object context,int weapon)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var input=Get(context,"Input");var attack=Get(combat,"Attack");var control=Get(attack,"Control");
            NativeCombatCadenceChecks.Save(combat,new CombatOptions());NativeCombatObservationChecks.Save(host,new ObservationOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,weapon,0,0);
            p.position=new Vector2(700,688-p.height);p.releaseUseItem=true;p.statMana=p.statManaMax=p.statManaMax2=200;
            p.inventory[1].SetDefaults(95);Main.screenPosition=new Vector2(550,350);
            var target=Main.npc[2];target.SetDefaults(3);target.whoAmI=2;target.active=true;target.Center=new Vector2(1100,650);
            target.velocity=target.netOffset=Vector2.Zero;target.aiStyle=-1;target.noGravity=true;target.life=target.lifeMax=100000;target.knockBackResist=0;Array.Clear(target.immune,0,target.immune.Length);
            var mouse=new Vector2(520,420);int nativeType=p.HeldItem.shoot;
            Step(context,mouse,false);
            try
            {
                using(NativeCombatUsageChecks.ObserveDrawSteps())
                {
                    int steps=NativeCombatUsageChecks.ObservedNpcSteps;
                    for(int i=0;i<12;i++)Step(context,mouse,true);
                    Require(NativeCombatUsageChecks.ObservedNpcSteps==steps && ((NpcPredictionCache)Get(Get(host,"Prediction"),"Cache")).Required==0,"both OFF: real supported birth has no NPC future work or forecast demand");
                }
                var parent=Main.projectile.Single(q=>q.active && q.owner==p.whoAmI && q.type==nativeType);int key=(int)parent.key;
                var registry=(IDictionary)Get(control,"shots");
                Console.WriteLine("PRISM C1 OFF weapon="+weapon+" key="+parent.key+" registered="+registry.Contains(key)+" sources="+registry.Count+" charge="+parent.ai[0]);
                Require(registry.Count==1 && registry.Contains(key),"both OFF retain only the real supported local source, without discovering the pool");
                var source=Get(registry[key],"Source");float charge=parent.ai[0];
                if(weapon==3541)Require(((ICollection)Get(registry[key],"Children")).Count==6,"OFF prism records its six actual native child identities");
                Require(Vector2.Dot(parent.velocity.SafeNormalize(Vector2.UnitY),(mouse-p.MountedCenter).SafeNormalize(Vector2.UnitY))>.95f,"OFF native parent follows physical direction");
                Call(host,"Set",6,true);Committed(host);
                for(int i=0;i<48;i++)Step(context,mouse,true);
                Require(parent.active && (int)parent.key==key && registry.Contains(key) && ReferenceEquals(source,Get(registry[key],"Source")),"enable preserves the same natural parent and birth source");
                Aligned(parent,p,target,mouse,"OFF birth -> enable",weapon);
                Call(host,"Set",6,true);Committed(host);
                for(int i=0;i<4;i++)Step(context,mouse,true);
                Require(parent.active && registry.Contains(key) && ReferenceEquals(source,Get(registry[key],"Source")),"already-on retry retains the actual source");
                Call(host,"Set",6,false);Committed(host);
                using(NativeCombatUsageChecks.ObserveDrawSteps())
                {
                    int steps=NativeCombatUsageChecks.ObservedNpcSteps;
                    for(int i=0;i<12;i++)Step(context,mouse,true);
                    Require(NativeCombatUsageChecks.ObservedNpcSteps==steps && ((NpcPredictionCache)Get(Get(host,"Prediction"),"Cache")).Required==0,"closing both consumers immediately withdraws future work");
                }
                Require(registry.Count==1 && registry.Contains(key),"final demand falling edge retains a still-legal birth");
                Call(host,"Set",6,true);Committed(host);
                for(int i=0;i<48;i++)Step(context,mouse,true);
                Aligned(parent,p,target,mouse,"close -> reopen",weapon);
                target.Center+=new Vector2(-30,-25);mouse=new Vector2(480,450);
                for(int i=0;i<12;i++)NativeCombatSelectionPreviewChecks.Step(context,mouse,true,false,true);
                Require(parent.active && (int)parent.key==key && parent.ai[0]>charge,"target/player/cursor movement retains native source and progresses native charge");
                Aligned(parent,p,target,mouse,"moving player/target/mouse",weapon);
                // A real UI owner denies input even though birth facts remain.
                Main.drawingPlayerChat=true;Step(context,mouse,true);
                Require(!(bool)Get(attack,"BasicPermission"),"UI occupation denies current execution");Main.drawingPlayerChat=false;
                // 633 checks channel each AI. 460 checks only its next native
                // five-action charge window; source retention must not force
                // that still-live body to die early or continue lending input.
                int releaseBound=Math.Max(1,NativeReleaseWindow(parent,p)),releaseUpdates=0;
                Console.WriteLine("PRISM C1 release start weapon="+weapon+" active="+parent.active+" charge="+parent.ai[0]+" phase="+parent.ai[1]+" rate="+p.GetSlowMagicUseRate()+" bound="+releaseBound);
                do
                {
                    Step(context,mouse,false);releaseUpdates++;
                    Require(!p.channel && !(bool)Get(attack,"BasicPermission"),"physical release immediately withdraws execution before the native death window");
                    if(!parent.active || (int)parent.key!=key)Require(!registry.Contains(key),"the native death callback immediately retires its original source");
                }while(parent.active && (int)parent.key==key && releaseUpdates<releaseBound);
                Console.WriteLine("PRISM C1 release end weapon="+weapon+" active="+parent.active+" updates="+releaseUpdates+" charge="+parent.ai[0]+" phase="+parent.ai[1]+" sources="+registry.Count);
                Require((!parent.active || (int)parent.key!=key) && !registry.Contains(key),"physical release preserves the native death window and retires its source after Kill");
                p.selectedItemState.Select(1);
                // Select buffers intent; native Update admits it only after
                // channel/reuse/animation AND itemTime close. ItemCheck spends
                // those tails later in the player update, so use their actual
                // remaining values plus the following admission opportunity.
                int selectionBound=Math.Max(p.itemAnimation,p.itemTime)+Math.Max(0,p.reuseDelay)+2,selectionUpdates=0;
                Console.WriteLine("PRISM C1 selection start weapon="+weapon+" selected="+p.selectedItem+" animation="+p.itemAnimation+" time="+p.itemTime+" reuse="+p.reuseDelay+" channel="+p.channel+" pending="+Get(p,"pendingItemReuse")+" canChange="+p.selectedItemState.CanChangeSelectedItemImmediately+" bound="+selectionBound);
                while(p.selectedItem!=1 && selectionUpdates<selectionBound)
                {
                    bool ready=p.selectedItemState.CanChangeSelectedItemImmediately;Step(context,mouse,false);selectionUpdates++;
                    Require((!parent.active || (int)parent.key!=key) && !registry.Contains(key),"old native parent and source stay retired during the legitimate hand tail");
                    if(ready)Require(p.selectedItem==1,"the first legal native selection opportunity consumes the buffered change");
                }
                Console.WriteLine("PRISM C1 selection end weapon="+weapon+" selected="+p.selectedItem+" updates="+selectionUpdates+" animation="+p.itemAnimation+" time="+p.itemTime+" reuse="+p.reuseDelay+" channel="+p.channel);
                Require(p.selectedItem==1 && registry.Count==0,"normal weapon switch leaves no old source");
                Console.WriteLine("PASS source lifecycle weapon="+weapon+" OFF birth/enable/retry/close-reopen/movement/UI/release/selection; no parent respawn or manual registration");
            }
            finally{Main.drawingPlayerChat=false;Call(host,"Set",6,false);Committed(host);for(int i=0;i<8;i++)Step(context,mouse,false);}
        }
        // Independent of HostHeldAttack.Consumes: AI075 increments the local
        // pre-charge counter, or the >=180 charge, before checking its native
        // five-action gate. Read only actual current scalars; never run AI or
        // rewrite its phase/channel to shorten a test's release tail.
        private static int NativeReleaseWindow(Projectile parent,Player player)
        {
            if(!parent.active)return 0;
            Require(parent.MaxUpdates==1,"release fixture uses the original single-update controller");
            if(parent.type==633)return 1;
            Require(parent.type==460,"release window has a declared native controller");
            float charge=parent.ai[0],phase=parent.ai[1],rate=player.GetSlowMagicUseRate();
            Require(rate>0 && rate<=1,"native slow-magic rate retains a finite five-action phase window");
            int actions=(int)Math.Ceiling(5/rate)+1;
            for(int i=1;i<=actions;i++)
            {
                charge+=rate;
                if(charge<180){phase++;if(phase>=5)return i;}
                else if((int)charge%5==0)return i;
            }
            throw new InvalidOperationException("Native release phase did not reach its independently bounded channel check.");
        }
        private static void Aligned(Projectile parent,Player p,NPC target,Vector2 mouse,string stage,int weapon)
        {
            float actual=Vector2.Dot(parent.velocity.SafeNormalize(Vector2.UnitY),(target.Center-p.MountedCenter).SafeNormalize(Vector2.UnitX));
            float physical=Vector2.Dot(parent.velocity.SafeNormalize(Vector2.UnitY),(mouse-p.MountedCenter).SafeNormalize(Vector2.UnitY));
            Console.WriteLine("PRISM C1 direction weapon="+weapon+" stage="+stage+" targetDot="+actual+" physicalDot="+physical+" charge="+parent.ai[0]);
            Require(actual>.95f && actual>physical+.5f,"same parent actually consumes auxiliary direction: "+stage+" weapon="+weapon);
        }
        private static Projectile failingKill;
        private static void KillFault(Projectile __instance)
        {if(ReferenceEquals(__instance,failingKill))throw new InvalidOperationException("prism-chain native Kill fault");}
        private static void Retirement(object context)
        {
            var host=Get(context,"CombatObservation");var control=Get(Get(Get(context,"Combat"),"Attack"),"Control");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),2797,0,0);
            p.position=new Vector2(700,688-p.height);p.releaseUseItem=true;p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;Main.screenPosition=new Vector2(550,350);
            var target=Main.npc[2];target.SetDefaults(3);target.whoAmI=2;target.active=true;target.Center=new Vector2(1100,650);target.velocity=Vector2.Zero;target.aiStyle=-1;target.noGravity=true;target.life=target.lifeMax=100000;
            var mouse=new Vector2(520,420);Step(context,mouse,false);Step(context,mouse,true);
            var bubbles=Main.projectile.Where(q=>q.active && q.type==444 && q.owner==p.whoAmI).ToArray();Require(bubbles.Length>=4,"original OFF bubble Shoot creates its normal random batch");
            var registry=(IDictionary)Get(control,"shots");Require(bubbles.All(q=>registry.Contains((int)q.key)),"OFF retains each actual later Kill consumer");
            Call(host,"Set",6,true);Committed(host);Step(context,mouse,true);
            var audit=new Harmony("JueMingR.Tests.PrismKillFailure");audit.Patch(typeof(Projectile).GetMethod("Kill"),prefix:new HarmonyMethod(typeof(NativeCombatPrismChainChecks),nameof(KillFault)){priority=Priority.Last});
            try
            {
                failingKill=bubbles[0];int failedKey=(int)failingKill.key,x=Main.mouseX,y=Main.mouseY;bool propagated=false;
                try{failingKill.Kill();}catch(InvalidOperationException error){propagated=error.Message=="prism-chain native Kill fault";}
                failingKill=null;
                Require(propagated && Main.mouseX==x && Main.mouseY==y && !registry.Contains(failedKey),"native Kill exception propagates after its borrowed cursor and original key retire");
                var live=bubbles[1];int key=(int)live.key;live.Kill();
                var bullet=Main.projectile.Last(q=>q.active && q.owner==p.whoAmI && q.type==14);
                Require(!registry.Contains(key) && Vector2.Dot(bullet.velocity.SafeNormalize(Vector2.UnitY),(target.Center-bullet.Center).SafeNormalize(Vector2.UnitX))>.95f,"bubble Kill consumes its source before retirement and creates the actual assisted bullet");
                int inactiveKey=(int)bubbles[2].key;bubbles[2].active=false;
                NativeQuickItemChecks.BeginWorldStep();Call(host,"Poll");
                Require(!registry.Contains(inactiveKey),"direct native deactivation retires from the registered set on the next world poll");
                Call(host,"Set",6,false);Committed(host);foreach(var q in bubbles)q.active=false;
                NativeQuickItemChecks.BeginWorldStep();Call(host,"Poll");Require(registry.Count==0,"OFF bounded maintenance retains no dead source without an AI callback");
                Console.WriteLine("PASS source retirement actual bubble Kill consumer/original exception/cursor return/direct inactive/OFF bounded cleanup");
            }
            finally{failingKill=null;audit.UnpatchAll(audit.Id);Call(host,"Set",6,false);Committed(host);}
        }
        private static void Step(object context,Vector2 mouse,bool held)
        {NativeCombatSelectionPreviewChecks.Step(context,mouse,held);}
        private static void State(object host,object combat,object attack,object control,Projectile parent,int step)
        {
            var shots=(IDictionary)Get(control,"shots");object source=null;
            if(parent!=null && shots.Contains((int)parent.key))source=Get(shots[(int)parent.key],"Source");
            Console.WriteLine("PRISM CHAIN step="+step+" GUC="+Main.GameUpdateCount+" parent="+(parent==null?"none":parent.key.ToString())+" sources="+shots.Count+" source="+(source!=null)+" selected="+Get(Get(host,"Selection"),"Target")+" basic="+Get(attack,"BasicPermission")+" receiver="+Get(attack,"CurrentReceiver")+" admitted="+Call(combat,"Admitted",Main.LocalPlayer,false)+" channel="+Main.LocalPlayer.channel+" use="+Main.LocalPlayer.controlUseItem+" mana="+Main.LocalPlayer.statMana+" cursor="+Main.MouseWorld+" workMs="+Get(Get(host,"PredictionWork"),"UsedMilliseconds")+" exceeded="+Get(Get(host,"PredictionWork"),"Exceeded"));
        }
    }
}
