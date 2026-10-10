using System;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatAmmoChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var tools=Get(context,"Tools");var input=Get(context,"Input");
            // One native outcome matrix; every row resolves its actual .8 Item
            // fields, ammo selection and projectile defaults. No old-version
            // identity or guessed per-tick speed stands in for these values.
            foreach(var row in new[]{new[]{95,278,981},new[]{905,71,158},new[]{905,72,159},new[]{905,73,160},new[]{905,74,161},new[]{120,40,2},new[]{682,40,117},new[]{725,40,120},new[]{2223,40,357},new[]{3052,40,495},new[]{1254,97,242},new[]{1254,278,981},new[]{759,771,134},new[]{758,771,133}})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,row[0],0,0);p.position=new Vector2(700,646);p.velocity=Vector2.Zero;Main.screenPosition=new Vector2(600,500);
                p.inventory[54].SetDefaults(row[1]);p.inventory[54].stack=999;var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(row[0]==682 || row[0]==725 || row[0]==3052?1400:1100,646);n.life=n.lifeMax=10000;n.target=0;n.defense=0;
                // SetDefaults retains the slot's owner-immunity array. These
                // independent stationary rows do not tick NPCs; start eligible
                // instead of inheriting the preceding piercing shot's cooldown.
                Array.Clear(n.immune,0,n.immune.Length);
                NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0);
                var contact=(AttackContact)GetOptional(attack,"ExpectedImpact");Require(contact!=null,"actual ammo family prepares contact: weapon="+row[0]+" ammo="+row[1]);
                var captured=Get(attack,"ammo");Require((int)Get(captured,"Projectile")==row[2],"deterministic post-ammo shoot conversion order");float raw=(float)Get(captured,"Speed");var delta=new Vector2(contact.AimX,contact.AimY)-p.RotatedRelativePoint(p.MountedCenter);delta.Normalize();var expected=delta*raw;
                var models=combat.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels");var args=new object[]{captured,default(AttackMotion)};Require((bool)models.GetMethod("TryRead",Flags).Invoke(null,args),"resolved member has an explicit model");var motion=(AttackMotion)args[1];
                Require((row[2]==117 || row[2]==120 || row[2]==495 || row[2]==2 || row[2]==133)?motion.Gravity>0:motion.Gravity==0,"straight members cannot fall through into a later arrow's gravity branch");
                if(ContentSamples.ProjectilesByType[row[2]].aiStyle==1)while(expected.X>=16 || expected.X<=-16 || expected.Y>=16 || expected.Y< -16)expected*=.97f;
                int mouseX=Main.mouseX,mouseY=Main.mouseY;typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var shots=Main.projectile.Where(q=>q.active && q.owner==0).ToArray();Require(shots.Length==1 && shots[0].type==row[2],"native final projectile identity agrees with pure snapshot");var shot=shots[0];Require(Vector2.DistanceSquared(shot.velocity,expected)<.0001f,"native component birth limit preserves the modeled velocity");Require(Main.mouseX==mouseX && Main.mouseY==mouseY && shot.damage>0,"natural damage and physical cursor ownership remain valid");
                if(row[0]==905)Require(Math.Abs(raw-(10+row[1]-70))<.0001f && shot.extraUpdates==1,"coin's ammo speed is per subupdate, despite zero weapon damage");
                int firstDamage=0;for(int tick=1;tick<=contact.Tick && shot.active;tick++){shot.Update(shot.whoAmI);if(n.life<10000){firstDamage=tick;break;}}
                Require(firstDamage==contact.Tick,"first native Damage matches modeled tick: expected="+contact.Tick+" actual="+firstDamage+" weapon="+row[0]);
                Console.WriteLine("PASS ammo -> native ShotOutcome/Damage: weapon="+row[0]+" ammo="+row[1]+" projectile="+shot.type+" speed="+raw+" birth="+expected+" updates="+(shot.extraUpdates+1)+" firstDamage="+firstDamage);
                Array.Clear(n.immune,0,n.immune.Length);
            }
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
        }
    }
}
