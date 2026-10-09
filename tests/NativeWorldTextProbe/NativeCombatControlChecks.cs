using System;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatControlChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        internal static void Run(object context)
        {
            NativeCombatYoyoChecks.Run(context);
            NativeCombatReceiveChecks.Run(context);
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var tools=Get(context,"Tools");var input=Get(context,"Input");
            foreach(int type in new[]{113,218,495})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,type,0,0);p.position=new Vector2(700,646);p.channel=true;p.controlUseItem=true;Main.screenPosition=new Vector2(600,500);
                Main.leftWorld=Main.topWorld=0;Main.rightWorld=Main.maxTilesX*16;Main.bottomWorld=Main.maxTilesY*16;
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(1100,646);n.life=n.lifeMax=10000;n.target=0;Array.Clear(n.immune,0,n.immune.Length);
                NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,1,true);
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var shot=Main.projectile.Single(q=>q.active && q.owner==0);
                NativeCombatAimChecks.Prepare(host,attack,n,1,true);int x=Main.mouseX,y=Main.mouseY;shot.AI();
                Require(shot.ai[0]>1000 && shot.velocity.X>0,"natural guided AI consumes prepared future, not physical cursor: "+type+" point="+shot.ai[0]+","+shot.ai[1]);
                Require(Main.mouseX==x && Main.mouseY==y,"guided AI returns borrowed coordinates");
                var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");Require(plan!=null,"guided red requires an actual replayed body contact");
                Vector2 center=shot.Center,velocity=shot.velocity,aim=new Vector2(plan.AimX,plan.AimY);
                for(int step=0;step<5;step++)
                {
                    Vector2 delta=aim-center;velocity=delta.Length()>=64?delta.SafeNormalize(Vector2.Zero)*Math.Min(32,delta.Length()):velocity*.3f+delta*.3f;center+=velocity;
                    shot.Update(shot.whoAmI);Require(Vector2.DistanceSquared(center,shot.Center)<.01f,"controlled native AI_009 kinematics match finite replay");
                }
                p.channel=false;shot.AI();Require(shot.ai[0]<0 && shot.active,"physical release preserves native autonomous transition");
                p.channel=true;Main.mouseX=10;Main.mouseY=10;shot.AI();Require(shot.ai[0]<0,"released projectile cannot regain aim permission");
                Console.WriteLine("PASS actual guided AI/release: weapon="+type+" released="+shot.ai[0]);
                // A separate live attack reaches the same shared future;
                // do not conflate released autonomous hits with aim support.
                foreach(var q in Main.projectile)q.active=false;p.channel=true;NativeCombatAimChecks.Prepare(host,attack,n,1,true);
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});shot=Main.projectile.Single(q=>q.active && q.owner==0);NativeCombatAimChecks.Prepare(host,attack,n,1,true);plan=(AttackContact)GetOptional(attack,"ExpectedImpact");int first=-1;
                for(int tick=0;tick<30 && first<0;tick++){n.position.X=1100+tick;int life=n.life;shot.Update(shot.whoAmI);if(n.life<life)first=tick;}
                Require(first==plan.Tick,"first real controlled Damage uses the prepared timeline index: "+type+" actual="+first+" plan="+plan.Tick);
                Console.WriteLine("PASS actual guided contact: weapon="+type+" firstDamage="+first+" impact="+plan.ImpactX+","+plan.ImpactY);
            }
            Bubble(context,combat,host,attack,tools,input);
            Held(context,combat,host,attack,tools,input);
            NativeCombatHeldSequenceChecks.Run(context);
            NativeCombatBeamChecks.Run(context);
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
        }
        private static void Bubble(object context,object combat,object host,object attack,object tools,object input)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,2797,0,0);p.position=new Vector2(700,646);p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;Main.screenPosition=new Vector2(600,500);
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(1100,646);n.life=n.lifeMax=10000;n.target=0;Array.Clear(n.immune,0,n.immune.Length);
            NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0);
            typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var bubbles=Main.projectile.Where(q=>q.active && q.owner==0 && q.type==444).ToArray();Require(bubbles.Length>=4 && bubbles.Length<=5,"native bubble count remains random");
            foreach(var q in bubbles)Require(q.localAI[0]==14 && q.localAI[1]>0,"bubble seals actual ammunition and speed at native birth");
            // Physical release does not revoke this already-started second
            // stage. Changing inventory cannot re-pick the sealed ammunition.
            NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),false);Call(combat,"Sample");p.channel=p.controlUseItem=false;p.inventory[54].SetDefaults(278);p.inventory[54].stack=999;
            NativeCombatAimChecks.Prepare(host,attack,n,0);int x=Main.mouseX,y=Main.mouseY;var bubble=bubbles[0];bubble.Kill();var child=Main.projectile.Single(q=>q.active && q.owner==0 && q.type==14);
            Require(child.velocity.X>0 && child.type==14,"released bubble natural Kill consumes future using sealed ammo, not current HeldItem ammo");
            Require(Main.mouseX==x && Main.mouseY==y,"bubble Kill returns physical input");
            Console.WriteLine("PASS natural bubble Kill after release/ammo change: child="+child.type+" velocity="+child.velocity);
            Vector2 oldVelocity=child.velocity;NativeCombatObservationChecks.Save(host,new ObservationOptions());bubbles[1].Kill();
            Require(child.velocity==oldVelocity && Main.projectile.Where(q=>q.active && q.owner==0 && q.type==14).Any(q=>!ReferenceEquals(q,child) && q.velocity.X<0),"Aim OFF immediately returns original bubble input and leaves previously born bullets untouched");
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));p.inventory[p.selectedItem]=new Item();p.inventory[p.selectedItem].SetDefaults(2797);NativeCombatAimChecks.Prepare(host,attack,n,0);bubbles[2].Kill();
            Require(GetOptional(attack,"ExpectedImpact")==null,"same-type weapon replacement cannot re-authorize old source bubbles/red");
            Console.WriteLine("PASS bubble permission exits: OFF and same-type new weapon retain native death and existing child velocity.");
        }
        private static void Held(object context,object combat,object host,object attack,object tools,object input)
        {
            foreach(var row in new[]{new[]{3475,97,615},new[]{3540,40,630},new[]{3854,40,705},new[]{3541,0,633},new[]{2882,0,460},new[]{4923,0,927}})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,row[0],0,0);p.position=new Vector2(700,646);p.channel=p.controlUseItem=true;p.statMana=p.statManaMax2=1000;p.slowMagicUse=false;Main.screenPosition=new Vector2(600,500);p.itemAnimationMax=p.HeldItem.useAnimation;
                if(row[1]>0){p.inventory[54].SetDefaults(row[1]);p.inventory[54].stack=999;}
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(1100,646);n.life=n.lifeMax=10000;n.target=0;Array.Clear(n.immune,0,n.immune.Length);
                NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,1,true);
                Require(GetOptional(attack,"ExpectedImpact")==null,"controller birth is not an immediate ordinary bullet/beam contact: "+row[0]);
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var shot=Main.projectile.Single(q=>q.active && q.owner==0 && q.type==row[2]);
                NativeCombatAimChecks.Prepare(host,attack,n,1,true);Vector2 old=shot.velocity;int x=Main.mouseX,y=Main.mouseY;
                for(int i=0;i<5;i++)shot.AI();
                Require(shot.velocity.X>0 && Main.mouseX==x && Main.mouseY==y,"held natural AI consumes future input in its genuine window: "+row[0]+" velocity="+shot.velocity);
                if(row[2]==633){Require(Main.projectile.Count(q=>q.active && q.type==632)==6,"prism keeps exactly six native child keys");Require(Vector2.Dot(old.SafeNormalize(Vector2.UnitX),shot.velocity.SafeNormalize(Vector2.UnitX))>.9f,"prism preserves native turning inertia");}
                if(row[2]==460)Require(Main.projectile.Any(q=>q.active && q.type==459) && !Main.projectile.Any(q=>q.active && q.type==461),"early charge creates orb, not premature beam");
                int exitWindow=Math.Max(6,Math.Max(p.itemAnimationMax+1,(int)Math.Ceiling(shot.ai[1])+2));p.channel=false;for(int i=0;i<exitWindow && shot.active;i++)shot.AI();Require(!shot.active,"native channel exit retains its real window: "+row[0]+" bound="+exitWindow);
                Console.WriteLine("PASS actual held AI/input/exit: weapon="+row[0]+" role="+row[2]);
            }
        }
    }
}
