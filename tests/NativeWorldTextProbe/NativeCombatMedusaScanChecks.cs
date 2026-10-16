using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Testing;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatMedusaScanChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static Projectile parent,receiving;private static int first,step;private static readonly List<Projectile> born=new List<Projectile>();
        private static object Static(Type t,string name,params object[] args){return t.GetMethod(name,Flags).Invoke(null,args);}
        private static void Birth(IEntitySource __0,int __result){if((__0 as EntitySource_Parent)?.Entity==parent && __result>=0 && __result<1000 && Main.projectile[__result].type==536)born.Add(Main.projectile[__result]);}
        private static void Damage(Projectile __instance){receiving=__instance;}private static void DamageDone(){receiving=null;}
        private static void Strike(NPC __instance,int __result){if(__result>0 && __instance.whoAmI==2 && receiving?.type==536 && first==0)first=step;}
        private static Player Reset(object context)
        {NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3269,0,0);p.position=new Vector2(850,646);p.velocity=Vector2.Zero;p.ResetEffects();p.itemAnimationMax=p.itemAnimation=p.HeldItem.useAnimation;p.channel=true;p.statMana=p.statManaMax2=200;p.manaCost=1;p.manaPotionDelay=p.potionDelay=0;p.cursed=p.dead=p.slowMagicUse=p.manaFlower=false;Main.windPhysics=false;for(int i=1;i<58;i++)p.inventory[i].TurnToAir();foreach(var item in p.bank4.item)item.TurnToAir();foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;for(int x=35;x<115;x++)for(int y=10;y<65;y++)Main.tile[x,y].ClearEverything();for(int x=35;x<115;x++)NativeToolsChecks.Tile(x,43,1);born.Clear();first=step=0;return p;}
        private static NPC Target(int slot,Vector2 center)
        {var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;n.realLife=-1;n.friendly=n.immortal=n.dontTakeDamage=false;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static NpcTrajectory Timeline(NPC n)
        {var identity=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=identity,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(identity,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);}
        private static object Terrain(){var t=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static AttackContact Known(Player p,NPC n,out object root)
        {var path=Timeline(n);var phase=Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc");var clock=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new[]{(object)path,phase},null);root=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,parent,path,0,true,false},null);return (AttackContact)Static(model,"KnownFamily",p,new[]{parent},path,clock,Terrain(),root);}
        private static Projectile Shoot(Player p)
        {Main.mouseX=1300-(int)Main.screenPosition.X;Main.mouseY=667-(int)Main.screenPosition.Y;typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});return Main.projectile.Single(q=>q.active && q.type==535);}
        private static void Advance(int limit)
        {for(step=1;step<=limit && first==0;step++){NativeQuickItemChecks.BeginWorldStep();foreach(var n in Main.npc)if(n.active)n.UpdateNPC(n.whoAmI);for(int slot=0;slot<1000;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;try{Main.projectile[slot].Update(slot);}finally{Main.ProjectileUpdateLoopIndex=-1;}}}}
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostMedusaScanAttack",true);var audit=new Harmony("JueMingR.Tests.MedusaScan");bool wind=Main.windPhysics,v2=DebugOptions.ManaV2,delay=DebugOptions.ManaPotionDelay;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatMedusaScanChecks),nameof(Birth)));audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatMedusaScanChecks),nameof(Damage)),postfix:new HarmonyMethod(typeof(NativeCombatMedusaScanChecks),nameof(DamageDone)));audit.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatMedusaScanChecks),nameof(Strike)));
            try{Mana(context);Anchor(context);Receive(context);Boundaries(context);Console.WriteLine("PASS MEDUSA SCAN actual535 hand/channel, finite mana, nearest3/LOS/320 and synchronous536 receive.");}finally{Main.windPhysics=wind;DebugOptions.ManaV2=v2;DebugOptions.ManaPotionDelay=delay;Main.ProjectileUpdateLoopIndex=-1;audit.UnpatchAll(audit.Id);}
        }
        private static void Mana(object context)
        {
            foreach(bool v2 in new[]{false,true})foreach(int mode in new[]{0,1,2,3,4,5})
            {
                var p=Reset(context);DebugOptions.ManaV2=v2;DebugOptions.ManaPotionDelay=false;p.statMana=mode==0?100:0;p.manaFlower=mode>=2;if(mode>=2){var item=mode==5?p.bank4.item[0]:p.inventory[1];item.SetDefaults(110);item.stack=2;}if(mode==3)p.manaPotionDelay=10;if(mode==4)p.cursed=true;if(mode==5){p.inventory[2].SetDefaults(4131);p.inventory[2].stack=1;}
                parent=Shoot(p);var state=Activator.CreateInstance(model.GetNestedType("State",Flags),Flags,null,new object[]{p,new[]{parent}},null);var budget=Call(state,"NewBudget");var shadow=(Player)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostMeleeAttack",true),"PlayerCopy",p);bool expected=(bool)Call(budget,"Pay",shadow,0);bool actual=p.CheckMana(p.HeldItem.mana,true);Require(expected==actual && shadow.statMana==p.statMana && shadow.slowMagicUse==p.slowMagicUse,"native CheckMana resource payment/ManaV2 slow branch agrees mode="+mode+" v2="+v2);
                if(mode==2 || mode==5)Require(p.statMana>0 && (mode==5?p.bank4.item[0]:p.inventory[1]).stack==1,"native current inventory/voidbag auto mana is actually consumed");
            }
            Console.WriteLine("PASS MEDUSA original12 CheckMana branches including current inventory/voidbag, blocked QuickMana and ManaV2 slow state");
        }
        private static void Anchor(object context)
        {
            foreach(int facing in new[]{-1,1})foreach(float gravity in new[]{-1f,1f})
            {var p=Reset(context);p.direction=facing;p.gravDir=gravity;parent=Shoot(p);var q=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostNebulaBlazeAttack",true),"Copy",parent);var body=(Player)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostMeleeAttack",true),"PlayerCopy",p);for(int action=0;action<8;action++){Main.mouseX=(action%2==0?600:1300)-(int)Main.screenPosition.X;Static(model,"Anchor",q,body,Main.screenPosition.X+Main.mouseX);parent.Update(parent.whoAmI);Require(parent.position==q.position && parent.velocity==q.velocity && parent.direction==q.direction && p.direction==body.direction,"native535 real physical facing/old hand anchor/gravity and post-AI movement remain exact without input override");}}
            Console.WriteLine("PASS MEDUSA native4 facing/gravity anchors x8 physical opposite mouse actions");
        }
        private static void Receive(object context)
        {
            DebugOptions.ManaV2=true;foreach(int clock in new[]{0,16,60})
            {var p=Reset(context);parent=Shoot(p);parent.ai[0]=clock;var n=Target(2,parent.Center+new Vector2(200,0));object root;var expected=Known(p,n,out root);Require(expected!=null,"observed535 finite scan has a real useful536 receive");Advance(90);Require(first==expected.Tick && born.Count==5 && born.All(q=>q.damage==0),"actual channel cooldown->one damaging536 and fourdecorative536 matches future first Strike and keeps all survivors harmless");Console.WriteLine("MEDUSA cooldown="+clock+" predicted="+expected.Tick+" native="+first);}
        }
        private static void Boundaries(object context)
        {
            foreach(int mode in new[]{0,1,2,3})
            {var p=Reset(context);parent=Shoot(p);parent.Update(parent.whoAmI);var n=Target(2,parent.Center+new Vector2(mode==0?319:mode==1?320:220,0));if(mode==2){int x=(int)(parent.Center.X+100)/16;for(int y=10;y<65;y++)NativeToolsChecks.Tile(x,y,1);}if(mode==3){Target(1,parent.Center+new Vector2(60,0));Target(3,parent.Center+new Vector2(110,0));Target(4,parent.Center+new Vector2(160,0));}object root;var expected=Known(p,n,out root);Require(mode==0?expected!=null:expected==null,"native strict320/LOS/closest3 gates determine actual scan eligibility");Advance(35);Require(mode==0?first==expected.Tick:first==0 && n.life==10000,"original535 preserves all scan negative controls instead of using HasPoint as damage");}
            var owner=Reset(context);parent=Shoot(owner);var selected=Target(2,parent.Center+new Vector2(200,0));owner.channel=false;object lease;Require(Known(owner,selected,out lease)==null,"released actual source has no scan Contact");Advance(1);Require(!parent.active && first==0,"native channel release retires before scan");
            owner=Reset(context);parent=Shoot(owner);selected=Target(2,parent.Center+new Vector2(200,0));Known(owner,selected,out lease);owner.statMana--;Require(!(bool)Get(lease,"Unchanged"),"same-GUC resource edit revokes preparation; no mana/inventory scan is borrowed by Draw");
        }
    }
}
