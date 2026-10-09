using System;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatAimQualificationChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var control=Get(attack,"Control");var input=Get(context,"Input");
            NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            foreach(int weapon in new[]{95,113,2797,3278})foreach(int change in new[]{0,1,2})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,weapon,0,0);p.position=new Vector2(700,646);p.channel=p.controlUseItem=true;p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;Main.screenPosition=new Vector2(600,400);
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.Center=p.Center+new Vector2(75,0);n.velocity=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=10000;Array.Clear(n.immune,0,n.immune.Length);
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,true));NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");Call(host,"SampleMouse");NativeCombatAimChecks.Prepare(host,attack,n,0,true);
                Projectile shot=null;
                if(weapon!=95){typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});shot=Main.projectile.First(q=>q.active && q.owner==0);if(weapon==2797)shot.timeLeft=2;NativeCombatAimChecks.Prepare(host,attack,n,0,true);}
                var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");Require(plan!=null,"qualification fixture starts with a real prepared contact: "+weapon);Call(attack,"BindPresentation",plan);Require((bool)Call(attack,"PresentationCurrent",plan),"healthy same-frame presentation lease");
                // Pose changes are not qualification changes. The published
                // conditional future remains the same object in this check.
                n.position+=new Vector2(.25f,0);Require((bool)Call(attack,"PresentationCurrent",plan),"ordinary stage displacement does not revoke receive eligibility");
                Reject();
                Require(!(bool)Call(attack,"PresentationCurrent",plan),"current receive refusal retires bound red presentation without a pool scan");
                Restore();NativeCombatAimChecks.Prepare(host,attack,n,0,true);Reject();
                object scope=weapon==95?Call(attack,"BeginShot",p,p.HeldItem,true):weapon==2797?Call(control,"BeginKill",shot):Call(control,"BeginAI",shot);Require(scope==null,"invalid current receiver cannot borrow natural consumer input");
                if(weapon!=95){Restore();NativeCombatAimChecks.Prepare(host,attack,n,0,true);Reject();Require(Call(control,"BeginOpening",p,p.HeldItem)==null,"invalid current receiver cannot borrow the next native birth point");}
                Restore();NativeCombatAimChecks.Prepare(host,attack,n,0,true);Reject();Require(GetOptional(attack,"ExpectedImpact")==null,"same identity's current receive refusal retires contact: weapon="+weapon+" change="+change);
                if(shot!=null)Require(shot.active,"receive revocation never kills the original controlled projectile");
                n.dontTakeDamage=n.friendly=false;n.life=n.lifeMax=1;NativeCombatAimChecks.Prepare(host,attack,n,0,true);Require(GetOptional(attack,"ExpectedImpact")!=null,"1HP receiver remains eligible after fresh prepare");
                n.SetDefaults(488);n.whoAmI=2;n.active=true;n.Center=p.Center+new Vector2(75,0);n.aiStyle=-1;n.noGravity=true;Array.Clear(n.immune,0,n.immune.Length);Require(n.immortal,"dummy keeps actual immortal distinction");NativeCombatAimChecks.Prepare(host,attack,n,0,true);Require(GetOptional(attack,"ExpectedImpact")!=null,"opt-in dummy remains an actual receiver, not a homing filter");
                Console.WriteLine("PASS receive lease weapon="+weapon+" change="+change+" sourceStillActive="+(shot==null || shot.active)+" dummyImmortal="+n.immortal);
                void Reject(){if(change==0)n.dontTakeDamage=true;else if(change==1)n.friendly=true;else n.life=0;}
                void Restore(){n.dontTakeDamage=n.friendly=false;n.life=n.lifeMax=10000;}
            }
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
        }
    }
}
