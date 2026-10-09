using System;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatWhipChecks
    {
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,5480,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.itemAnimationMax=p.itemAnimation=30;Main.screenPosition=new Vector2(600,500);
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(850,646);n.life=n.lifeMax=10000;n.target=0;Array.Clear(n.immune,0,n.immune.Length);
            NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0,true);
            typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var primary=Main.projectile.Single(q=>q.active && q.type==1035);Require(primary.ai[2]==0,"normal native 5480 birth is the mouse-consuming primary, not repeated-shot role");
            primary.ai[0]=19;primary.ai[2]=1;var velocity=primary.velocity;
            NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),false);Call(combat,"Sample");p.channel=p.controlUseItem=false;
            Require((bool)Get(attack,"Permission"),"released real primary whip retains demand for its natural later cursor window");NativeCombatAimChecks.Prepare(host,attack,n,0,true);
            Main.ProjectileUpdateLoopIndex=primary.whoAmI;
            var records=(System.Collections.IDictionary)Get(Get(attack,"Control"),"shots");var parentPlan=GetOptional(records[(int)primary.key],"Contact");
            try
            {
                primary.AI();Require(Main.projectile.Count(q=>q.active && q.type==1035)==1,"strict pre-threshold primary does not spawn child");
                Require(ReferenceEquals(parentPlan,GetOptional(records[(int)primary.key],"Contact")),"AI source lease does not re-prepare parent between AI and native movement");
                int x=Main.mouseX,y=Main.mouseY;primary.AI();var child=Main.projectile.Single(q=>q.active && q.type==1035 && !ReferenceEquals(q,primary));
                Require(child.ai[2]==12 && child.velocity.X>0 && primary.velocity==velocity,"released primary consumes prepared cursor only for child; original parent velocity stays sealed");Require(Main.mouseX==x && Main.mouseY==y,"native derived input restores physical mouse after AI");
                Console.WriteLine("PASS native 1035 primary strict window and released derived input: parent="+primary.key+" child="+child.key+" velocity="+child.velocity);
                Require(GetOptional(attack,"ExpectedImpact")==null && !records.Contains((int)child.key),"representative parent packet does not borrow an already born child's contact for its next window");int first=-1;var sealedChild=child.velocity;
                for(int step=0;step<30 && child.active;step++){child.Update(child.whoAmI);if(n.life<10000){first=step;break;}}
                Console.WriteLine("WHIP representative child actual Damage: first="+first+" life="+n.life);Require(first>=0,"representative input has a useful natural child damage outcome");Require(child.velocity==sealedChild && Main.mouseX==x && Main.mouseY==y,"derived child cannot borrow cursor or redirect its sealed velocity");
                primary.ai[2]=3;Require(!(bool)Get(attack,"Permission"),"after final primary window only fixed child damage remains, with no aim demand");primary.ai[0]=59;primary.AI();Require(!primary.active,"primary reaches native duration before another derived cursor window");
            }
            finally{Main.ProjectileUpdateLoopIndex=-1;}
            foreach(var q in Main.projectile)q.active=false;Array.Clear(n.immune,0,n.immune.Length);n.life=10000;p.itemAnimationMax=p.itemAnimation=20;
            NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatAimChecks.Prepare(host,attack,n,0,true);
            typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var extra=Main.projectile.Single(q=>q.active && q.type==1035);Require(extra.ai[2]>=1000 && extra.velocity.X>0,"legal false extra swing consumes representative direction and preserves native extra role/scatter");float duration;int count;float range;Projectile.GetWhipSettings(extra,out duration,out count,out range);Require(duration==60,"1035 extra duration is child role's fixed 60, not primary's animation20*2");int size=Main.projectile.Count(q=>q.active);extra.AI();Require(Main.projectile.Count(q=>q.active)==size,"1035 extra never acquires primary cursor/derived birth windows");
            var old=p.HeldItem;p.inventory[0]=old.Clone();NativeCombatAimChecks.Prepare(host,attack,n,0,true);Require(GetOptional(attack,"ExpectedImpact")==null,"same-type weapon instance replacement retires old main/child/extra capability without native removal");Require(extra.active,"permission retirement does not destroy original extra whip");
            Console.WriteLine("PASS native 1035 actual child Damage, duration/extra role and same-type source exit");
            Moving(context,combat,host,attack,input);
        }
        private static void Moving(object context,object combat,object host,object attack,object input)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,5480,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.itemAnimationMax=p.itemAnimation=30;Main.screenPosition=new Vector2(600,500);
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(850,646);n.velocity=new Vector2(1,0);n.life=n.lifeMax=10000;n.target=0;Array.Clear(n.immune,0,n.immune.Length);
            NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,1,true);
            typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var primary=Main.projectile.Single(q=>q.active && q.type==1035);primary.ai[0]=19;primary.ai[2]=1;
            NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),false);Call(combat,"Sample");p.channel=p.controlUseItem=false;NativeCombatAimChecks.Prepare(host,attack,n,1,true);primary.AI();primary.AI();var child=Main.projectile.Single(q=>q.active && q.type==1035 && !ReferenceEquals(q,primary));int first=-1;
            for(int step=0;step<30 && child.active;step++){if(step>0)n.position+=n.velocity;child.Update(child.whoAmI);if(n.life<10000){first=step;break;}}
            Require(first>=0 && GetOptional(attack,"ExpectedImpact")==null,"finite native child-shape representative has a useful moving-target outcome without claiming a reliable red contact");Console.WriteLine("PASS native moving-target 1035 representative child Damage: first="+first+" velocity="+child.velocity+" target="+n.position);
        }
    }
}
