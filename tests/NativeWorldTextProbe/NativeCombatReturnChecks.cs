using System;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatReturnChecks
    {
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");
            foreach(var row in new[]{new[]{2,160,-10,140,0,1},new[]{2,10,-10,10,0,0},new[]{4,100,-15,85,0,1},new[]{4,20,-30,5,0,0},new[]{2,160,-10,140,1,1},new[]{2,160,-10,140,-1,0}})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,162,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.channel=p.controlUseItem=true;Main.screenPosition=new Vector2(600,500);
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.Center=p.MountedCenter+new Vector2(row[3],0);n.life=n.lifeMax=10000;n.target=0;n.netOffset=Vector2.Zero;Array.Clear(n.immune,0,n.immune.Length);
                NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0,true);
                typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var q=Main.projectile.Single(s=>s.active && s.type==25);
                q.ai[0]=row[0];q.Center=p.MountedCenter+new Vector2(row[1],0);q.velocity=new Vector2(row[2],0);q.localNPCImmunity[2]=row[4];q.ownerHitCheck=false;p.channel=p.controlUseItem=false;
                NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),false);Call(combat,"Sample");NativeCombatAimChecks.Prepare(host,attack,n,0,true);var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");int x=Main.mouseX,y=Main.mouseY;q.Update(q.whoAmI);bool native=n.life<10000;
                Console.WriteLine("RETURN flail: state="+row[0]+" distance="+row[1]+" local="+row[4]+" contact="+plan?.Tick+" native="+native+" active="+q.active+" velocity="+q.velocity);
                Require(native==(row[5]==1),"native return/old-distance/overshoot/local gate expected branch");Require((plan!=null)==native && (!native || plan.Tick==0),"known return plan matches first actual Damage or native early exit");Require(Main.mouseX==x && Main.mouseY==y,"return never borrows cursor input");
            }
            Boomerang(context,combat,host,attack,input);
        }
        private static void Boomerang(object context,object combat,object host,object attack,object input)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,284,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.itemAnimationMax=p.itemAnimation=p.HeldItem.useAnimation;Main.screenPosition=new Vector2(600,500);
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(820,646);n.life=n.lifeMax=10000;n.target=0;n.netOffset=Vector2.Zero;Array.Clear(n.immune,0,n.immune.Length);
            // A frozen target makes the return path repeatable. Its original
            // outer Update still owns the immunity decrement; no projectile
            // substep or test counter pretends to advance that NPC clock.
            n.aiStyle=-1;n.noGravity=true;n.velocity=Vector2.Zero;n.immune[0]=35;
            NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0,true);var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");
            typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var q=Main.projectile.Single(s=>s.active && s.type==52);int first=-1;
            for(int step=1;step<=120 && q.active;step++){NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(n.whoAmI);q.Update(q.whoAmI);if(step==30 || step==35)Console.WriteLine("RETURN premise: step="+step+" immune="+n.immune[0]+" target="+n.position+" projectile="+q.Center);if(n.life<10000){first=step;break;}}
            Console.WriteLine("RETURN boomerang: first="+first+" expected="+plan?.Tick+" state="+q.ai[0]+" velocity="+q.velocity+" center="+q.Center);Require(first>30 && q.ai[0]==1,"normal boomerang can hit after native outbound clock enters return, without another cursor read");Require(plan!=null && plan.Tick==first,"finite return model agrees with first natural boomerang Damage");
        }
    }
}
