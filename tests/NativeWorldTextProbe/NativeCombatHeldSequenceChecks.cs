using System;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatHeldSequenceChecks
    {
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3475,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.channel=p.controlUseItem=true;p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;Main.screenPosition=new Vector2(600,500);Main.rand=new Terraria.Utilities.UnifiedRandom(7123);
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(1100,646);n.velocity=n.netOffset=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=10000;n.target=0;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);
            NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0,true);
            typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var parent=Main.projectile.Single(q=>q.active && q.type==615);parent.ai[0]=5;parent.ai[1]=2;
            NativeCombatAimChecks.Prepare(host,attack,n,0,true);Vector2 old=parent.velocity;int mx=Main.mouseX,my=Main.mouseY;parent.AI();Require(parent.ai[1]==1 && parent.velocity==old && !Main.projectile.Any(q=>q.active && (q.type==14 || q.type==616)),"non-window AI does not consume a new cursor or fire ammunition");
            NativeCombatAimChecks.Prepare(host,attack,n,0,true);parent.AI();var first=Main.projectile.Single(q=>q.active && q.type==14);string firstKey=first.key.ToString();Vector2 firstVelocity=first.velocity;Require(firstVelocity.X>0 && Main.mouseX==mx && Main.mouseY==my,"real first firing window uses shared input and restores physical cursor");
            // Each natural window reselects ammo. The born first bullet remains
            // an independent native projectile, even when a later type changes.
            n.position.Y=520;p.inventory[54].SetDefaults(278);p.inventory[54].stack=999;parent.ai[1]=1;NativeCombatAimChecks.Prepare(host,attack,n,0,true);parent.AI();var silver=Main.projectile.Single(q=>q.active && q.type==981);Vector2 limited=Vector2.Normalize(silver.velocity)*18.5f;while(Math.Abs(limited.X)>=16 || Math.Abs(limited.Y)>=16)limited*=.97f;Console.WriteLine("VORTEX ammo difference: first="+firstVelocity+" silver="+silver.velocity+" limited="+limited);Require(silver.velocity.X>0 && silver.velocity.Y<-3 && Vector2.DistanceSquared(silver.velocity,limited)<.0001f && first.key.ToString()==firstKey && first.velocity==firstVelocity,"next real PickAmmo uses .8 silver identity/speed without modifying the earlier bullet");
            Console.WriteLine("PASS Vortex repeated native ammo windows: first="+firstKey+"/14 v="+firstVelocity+" next="+silver.key+"/981 v="+silver.velocity);
            // Native initial ai0 is random; freeze the actual counter boundary,
            // not a claim that the first extra rocket is always the seventh shot.
            n.position.Y=646;parent.ai[0]=34;parent.ai[1]=1;NativeCombatAimChecks.Prepare(host,attack,n,0,true);parent.AI();var rocket=Main.projectile.Single(q=>q.active && q.type==616);var entries=(System.Collections.IDictionary)Get(Get(attack,"Control"),"shots");Require(!entries.Contains((int)rocket.key) && !entries.Contains((int)first.key) && !entries.Contains((int)silver.key),"born bullets and autonomous rocket are not cursor-controlled roles");
            string rocketKey=rocket.key.ToString();Vector2 birth=rocket.Center,rocketVelocity=rocket.velocity;Require(rocketVelocity.X>0 && Math.Abs(rocketVelocity.Length()-8)<.001f && Vector2.DistanceSquared(birth,p.RotatedRelativePoint(p.MountedCenter))<.001f,"additional rocket uses its own speed and current native mounted origin");
            foreach(var q in Main.projectile)if(!ReferenceEquals(q,rocket))q.active=false;NativeCombatObservationChecks.Save(host,new ObservationOptions());int firstDamage=0,locked=0;
            for(int tick=1;tick<=60 && rocket.active;tick++){NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(2);int life=n.life;rocket.Update(rocket.whoAmI);if(rocket.ai[1]>0)locked=(int)rocket.ai[1];if(n.life<life && firstDamage==0)firstDamage=tick;}
            Console.WriteLine("VORTEX native additional result: key="+rocketKey+" firstDamage="+firstDamage+" nativeTarget="+locked+" birth="+birth+" velocity="+rocketVelocity+" life="+n.life);
            Require(firstDamage>0 && locked==3,"additional rocket retains its independent native target acquisition and real Damage after aim is off");
        }
    }
}
