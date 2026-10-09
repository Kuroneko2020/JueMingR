using System;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatIntegrationChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static int births;
        private static void Born(Player __instance,Projectile __0){if(__instance.whoAmI==0)births++;}
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");var harmony=new Harmony("JueMingR.Tests.AttackIntegration");
            foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})harmony.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks).GetMethod("SkipAchievement",Flags)));
            harmony.Patch(typeof(Player).GetMethod("TryUpdateChannel",Flags),postfix:new HarmonyMethod(typeof(NativeCombatIntegrationChecks).GetMethod("Born",Flags)));
            try
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;p.releaseUseItem=true;Main.screenPosition=new Vector2(600,400);
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(1020,646);n.velocity=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=10000;n.shimmerTransparency=0;Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);Array.Clear(n.immune,0,n.immune.Length);
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));births=0;Step(context,p,n,false);Step(context,p,n,true);
                Console.WriteLine("WINDOW first births="+births+" time="+p.itemTime+" animation="+p.itemAnimation+" release="+p.releaseUseItem+" impact="+(GetOptional(attack,"ExpectedImpact")!=null));
                Require(births==1 && GetOptional(attack,"ExpectedImpact")==null,"first real shot has no zero-delay consumable Contact during ordinary cooldown");
                for(int i=0;i<22;i++)Step(context,p,n,true);Require(births==1 && p.itemAnimation==0 && p.itemTime==0 && GetOptional(attack,"ExpectedImpact")==null,"held non-autoreuse gun cannot reopen a use or advertise a consumed plan at zero timers");
                Step(context,p,n,false);Step(context,p,n,true);Require(births==2,"real release followed by fresh press restores a natural assisted attack");
                Console.WriteLine("PASS ordinary first click/cooldown/held-zero/release-fresh-press: two real source births, no synthetic Shoot/timer writes");
                var window=attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackWindow",true);var next=window.GetMethod("NextAction",Flags);var itemCheck=typeof(Player).GetMethod("ItemCheck",Flags);
                // These explicit entry states compare the same original action
                // gate; they do not replace the natural held-use sequence above.
                foreach(int animation in new[]{1,2})
                {
                    p.itemAnimationMax=16;p.itemAnimation=animation;p.itemTime=1;p.controlUseItem=true;p.releaseUseItem=false;p.autoReuseAllWeapons=true;int prior=births;
                    bool can=(bool)next.Invoke(null,new object[]{combat,p,p.HeldItem});itemCheck.Invoke(p,new object[0]);Require(can==(animation==2) && births-prior==(animation==2?1:0),"read-only entry 1/1 vs 1/2 matches original action, attachment reuse does not clear the tail animation");
                }
                p.autoReuseAllWeapons=false;Console.WriteLine("PASS native ordinary entry time1 animation1/2; attachment release and item auto-reuse remain distinct");
                FacingSelection(context,combat,host,attack,input);
            }
            finally{harmony.UnpatchAll(harmony.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());}
        }
        private static void FacingSelection(object context,object combat,object host,object attack,object input)
        {
            NativeCombatCadenceChecks.Save(combat,new CombatOptions(32));var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;p.releaseUseItem=true;Main.screenPosition=new Vector2(600,400);
            var right=Main.npc[2];right.SetDefaults(3);right.whoAmI=2;right.active=true;right.position=new Vector2(940,646);right.aiStyle=-1;right.noGravity=true;right.life=right.lifeMax=10000;
            var left=Main.npc[3];left.SetDefaults(3);left.whoAmI=3;left.active=true;left.position=new Vector2(620,646);left.aiStyle=-1;left.noGravity=true;left.life=left.lifeMax=10000;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,false,25,false,true));
            foreach(var target in new[]{right,left})
            {
                NativeToolExecutionChecks.Sample(context,input,target.Center,true);Call(combat,"Sample");Call(host,"SampleMouse");NativeQuickItemChecks.BeginWorldStep();p.Update(0);
                var selected=(JueMingR.Platform.Combat.NpcIdentity)Get(Get(host,"Selection"),"Target");Console.WriteLine("FACING wanted="+target.whoAmI+" selected="+selected.Slot+" direction="+p.direction);Require(selected.Slot==target.whoAmI && p.direction==(ReferenceEquals(target,right)?1:-1),"same action fresh physical selection drives the shared Facing target before native pose");
                target.UpdateNPC(target.whoAmI);Call(context,"UpdateRuntime");
            }
            Console.WriteLine("PASS fresh physical mouse right/left target change feeds shared Facing in the same natural player action");
        }
        private static void Step(object context,Player p,NPC n,bool left)
        {
            var input=Get(context,"Input");NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),left);Call(Get(context,"Combat"),"Sample");Call(Get(context,"CombatObservation"),"SampleMouse");int x=Main.mouseX,y=Main.mouseY;
            NativeQuickItemChecks.BeginWorldStep();p.Update(0);n.UpdateNPC(n.whoAmI);Call(context,"UpdateRuntime");Call(context,"UpdateShell");Require(Main.mouseX==x && Main.mouseY==y,"natural use returns physical cursor after action and presentation");
        }
    }
}
