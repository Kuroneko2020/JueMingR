using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatBoundaryChecks
    {
        internal static Delegate Provider(Type delegateType,Func<object,object> callback)
        {
            var invoke=delegateType.GetMethod("Invoke");var arg=Expression.Parameter(invoke.GetParameters()[0].ParameterType,"request");
            return Expression.Lambda(delegateType,Expression.Convert(Expression.Invoke(Expression.Constant(callback),Expression.Convert(arg,typeof(object))),invoke.ReturnType),arg).Compile();
        }
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var use=Get(combat,"Use");var tools=Get(context,"Tools");var input=Get(context,"Input");
            Idle(context,combat,use,tools,input);
            NativeCombatCadenceChecks.Save(combat,new CombatOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,ItemID.CopperShortsword,0,0);
            NativeCombatCadenceChecks.Save(combat,new CombatOptions(1));NativeToolExecutionChecks.Sample(context,input,p.Center+new Vector2(200,0),true);Call(combat,"Sample");Call(use,"Sync",p);
            p.releaseUseItem=false;p.itemAnimation=p.itemTime=p.reuseDelay=0;
            var fault=new Harmony("JueMingR.Tests.CombatAfterReuseFailure");
            fault.Patch(AccessTools.Method(typeof(Player),"ItemCheck_AutoReuseLogic"),postfix:new HarmonyMethod(typeof(NativeCombatBoundaryChecks),nameof(ThrowAfterReuse)){priority=Priority.Last});
            bool failed=false;
            try{p.ItemCheck();}catch(InvalidOperationException){failed=true;}
            finally{foreach(var m in fault.GetPatchedMethods().ToArray())fault.Unpatch(m,HarmonyPatchType.All,fault.Id);}
            Require(failed && !p.releaseUseItem && !(bool)Get(use,"Active"),"actual ItemCheck exception retires only the unconsumed synthetic release");
            NativeCombatCadenceChecks.Save(combat,new CombatOptions());p.controlUseItem=false;Main.mouseLeft=false;p.ItemCheck();Require(p.itemAnimation==0,"next ordinary ItemCheck cannot inherit synthetic start");
            // Nested and late finalizers borrow real controls, but cannot erase
            // a newer operation or somebody else's later coordinate write.
            NativeToolExecutionChecks.Sample(context,input,p.Center,false);Call(combat,"Sample");Call(use,"Sync",p);
            NativeToolExecutionChecks.Sample(context,input,p.Center+new Vector2(200,0),true);Call(combat,"Sample");
            Call(use,"Sync",p);NativeCombatCadenceChecks.Save(combat,new CombatOptions(1));Call(use,"Sync",p);
            var outer=Call(use,"Begin",p);var inner=Call(use,"Begin",p);Require(outer!=null && inner!=null,"nested scopes admitted");
            Call(use,"End",inner,null);Call(use,"End",outer,null);Call(use,"Stop");
            Call(use,"Sync",p);long token=(long)Get(use,"Operation");Call(use,"End",outer,new InvalidOperationException("late"));
            Require((long)Get(use,"Operation")==token && (bool)Get(use,"Active"),"late finalizer cannot stop new operation");
            Call(use,"Stop");
            var aim=Get(combat,"Aim");var property=aim.GetType().GetProperty("Provider",BindingFlags.Instance|BindingFlags.NonPublic);int calls=0;object old=null;
            Type result=aim.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.CombatAimPoint");
            property.SetValue(aim,Provider(property.PropertyType,request=>{calls++;old=Activator.CreateInstance(result,BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{request,(object)(p.Center-new Vector2(200,0))},null);return old;}));
            try
            {
                NativeCombatCadenceChecks.Save(combat,new CombatOptions());p=NativeToolExecutionChecks.Reset(context,tools,input,5462,0,0);p.inventory[1].SetDefaults(6153);p.statMana=p.statManaMax=p.statManaMax2=400;
                NativeCombatCadenceChecks.Save(combat,new CombatOptions(4));
                for(int i=0;i<350;i++){NativeCombatCadenceChecks.Step(context,false,true,0);Require(Main.mouseX==NativeCombatCadenceChecks.ManualMouseX && Main.mouseY==NativeCombatCadenceChecks.ManualMouseY,"future point is restored after native item/projectile stages");}
                Require(calls>5,"actual charge consumers request prepared points");
                property.SetValue(aim,Provider(property.PropertyType,r=>old));int before=calls;
                for(int i=0;i<60;i++)NativeCombatCadenceChecks.Step(context,false,true,0);
                Require(calls==before && Main.mouseX==NativeCombatCadenceChecks.ManualMouseX,"expired request cannot overwrite manual aim");
                NativeCombatCadenceChecks.Save(combat,new CombatOptions());p=NativeToolExecutionChecks.Reset(context,tools,input,ItemID.CopperShortsword,0,0);NativeCombatCadenceChecks.Save(combat,new CombatOptions(1));
                property.SetValue(aim,Provider(property.PropertyType,r=>{calls++;return null;}));for(int i=0;i<30;i++)NativeCombatCadenceChecks.Step(context,true,false,0);
                Require(calls==before,"ordinary auto click never consults deferred aim seam");
            }
            finally{property.SetValue(aim,null);NativeCombatCadenceChecks.Save(combat,new CombatOptions());}
            p=NativeToolExecutionChecks.Reset(context,tools,input,198,0,0);p.inventory[1].SetDefaults(671);p.inventory[2].SetDefaults(3772);NativeCombatCadenceChecks.Save(combat,new CombatOptions(4));
            for(int i=0;i<120;i++)NativeCombatCadenceChecks.Step(context,false,true,0);
            p.selectedItemState.Select(2);for(int i=0;i<120;i++)NativeCombatCadenceChecks.Step(context,false,true,0);
            Require(p.selectedItem==2 && !(bool)Get(use,"Active"),"later real manual selection retires old held quick-switch gesture");NativeCombatCadenceChecks.Step(context,false,false,0);NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            Console.WriteLine("PASS G11A native exception cleanup, nested/late scopes, charge-point stage boundary, stale aim rejection and manual restoration.");
        }
        private static void Idle(object context,object combat,object use,object tools,object input)
        {
            NativeCombatCadenceChecks.Save(combat,new CombatOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,ItemID.CopperPickaxe,0,0);
            foreach(int mask in new[]{0,255})foreach(bool held in new[]{false,true})
            {
                NativeCombatCadenceChecks.Save(combat,new CombatOptions(mask));NativeToolExecutionChecks.Sample(context,input,p.Center,held);Call(combat,"Sample");Call(combat,"Poll");
                var facing=Get(combat,"Facing");var report=Get(combat,"Reports");int discovery=Count(use,"ProjectileDiscoveryReads"),candidates=Count(use,"SwitchCandidateReads"),npcs=Count(facing,"CandidateReads"),recent=Count(report,"RecentReads");
                for(int i=0;i<100;i++){NativeQuickItemChecks.BeginWorldStep();Call(use,"Sync",p);Call(facing,"Apply",p);Call(report,"Poll");}
                Require(Count(use,"ProjectileDiscoveryReads")==discovery && Count(use,"SwitchCandidateReads")==candidates && Count(facing,"CandidateReads")==npcs && Count(report,"RecentReads")==recent,"actual off/idle/ineligible paths stop projectile/slot/NPC/recent reads mask="+mask+" held="+held);
            }
            NativeCombatCadenceChecks.Save(combat,new CombatOptions());
        }
        private static int Count(object owner,string name){return (int)(GetOptional(owner,name)??0);}
        private static void ThrowAfterReuse(){throw new InvalidOperationException("isolated after-AutoReuse failure");}
    }
}
