using System;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatReceiveChecks
    {
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,218,0,0);p.position=new Vector2(700,646);p.channel=p.controlUseItem=true;Main.screenPosition=new Vector2(600,400);
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(800,646);n.life=n.lifeMax=10000;n.target=0;Array.Clear(n.immune,0,n.immune.Length);n.immune[0]=20;
            NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,new Vector2(650,450),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));PrepareAction(host,attack,n);
            typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var shot=Main.projectile.Single(q=>q.active && q.type==34);PrepareAction(host,attack,n);var contact=(AttackContact)GetOptional(attack,"ExpectedImpact");
            Require(contact!=null && contact.Tick>=20,"known owner immune window must delay guided contact, not just geometric overlap: "+contact?.Tick);
            long expected=contact.Timeline.SampleTick+contact.Tick;int life=n.life;long first=-1;
            for(int frame=1;frame<=25 && n.life==life;frame++)
            {if(frame>1){NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,new Vector2(650,450),true);Call(combat,"Sample");PrepareAction(host,attack,n);}if(n.immune[0]>0)n.immune[0]--;shot.Update(shot.whoAmI);if(n.life<life)first=Main.GameUpdateCount;}
            Require(first==expected,"first actual Damage aligns known receive expiry on action time: actual="+first+" expected="+expected);
            n.immune[0]=0;shot.localNPCImmunity[n.whoAmI]=-1;PrepareAction(host,attack,n);Require(GetOptional(attack,"ExpectedImpact")==null,"permanent local immunity forbids a red contact");
            Console.WriteLine("PASS guided known owner/local receive window: first="+first+" expected="+expected);
            Boundaries(attack,p,n,shot);
        }
        private static void Boundaries(object attack,Player p,NPC n,Projectile shot)
        {
            var gate=attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackReceive",true);var capture=gate.GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic);var allows=gate.GetMethod("Allows",BindingFlags.Instance|BindingFlags.NonPublic);
            Func<int,bool> available=steps=>(bool)allows.Invoke(capture.Invoke(null,new object[]{p,shot,n.whoAmI,true,false}),new object[]{steps});
            n.immune[0]=0;shot.localNPCImmunity[n.whoAmI]=2;shot.extraUpdates=2;
            Require(!available(1) && available(2),"three subupdates do not expire a two-outer-update local timer");
            // Keep the controlled native motor away from the NPC during this
            // counter check: Update still runs all real AI/movement/Damage.
            shot.Center=new Vector2(710,667);shot.velocity=Vector2.Zero;p.channel=false;shot.ai[0]=-1;shot.ai[1]=-1;shot.Update(shot.whoAmI);
            Require(shot.localNPCImmunity[n.whoAmI]==1,"original extraUpdates=2 decrements local immunity once");
            shot.localNPCImmunity[n.whoAmI]=-1;shot.usesIDStaticNPCImmunity=true;uint previous=Projectile.perIDStaticNPCImmunity[shot.immunityIdentity,n.whoAmI];
            try
            {
                Projectile.perIDStaticNPCImmunity[shot.immunityIdentity,n.whoAmI]=Main.GameUpdateCount;
                Require(available(1),"native local/static OR allows exactly-expired static despite negative local");
                Projectile.perIDStaticNPCImmunity[shot.immunityIdentity,n.whoAmI]=Main.GameUpdateCount+2;
                Require(!available(2) && available(3),"current-action static expiry waits two world count increments");
                n.immune[0]=5;Require(!available(3) && available(5),"owner remains a separate gate after static OR");
            }
            finally{Projectile.perIDStaticNPCImmunity[shot.immunityIdentity,n.whoAmI]=previous;shot.usesIDStaticNPCImmunity=false;}
            foreach(var q in Main.projectile)q.active=false;
            n.immune[0]=5;n.life=10000;int projectile=Projectile.NewProjectile(new EntitySource_Parent(p),n.Center-new Vector2(12,0),new Vector2(12,0),14,30,0,p.whoAmI);var single=Main.projectile[projectile];
            object bypass=capture.Invoke(null,new object[]{p,single,n.whoAmI,true,false});Require(single.maxPenetrate==1 && (bool)allows.Invoke(bypass,new object[]{1}),"single penetration bypasses owner immune");
            single.Update(single.whoAmI);Require(n.life<10000,"original single-penetration Damage bypasses owner immune");
            Console.WriteLine("PASS native receive boundaries: local outer clock/negative, static equality/OR, owner gate and single penetration");
        }
        private static void PrepareAction(object host,object attack,NPC n)
        {
            var settings=((ObservationSettings)Get(host,"Settings")).Value;var selection=Get(host,"Selection");Call(selection,"Update",settings,(long)Get(host,"Session"),true,null);var identity=(NpcIdentity)Get(selection,"Target");Require(identity.Slot==n.whoAmI,"same shared receive target");
            var points=new NpcTrajectoryPoint[121];for(int i=0;i<points.Length;i++)points[i]=new NpcTrajectoryPoint(i,new NpcMotionState{Identity=identity,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true,Active=true});
            var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");cache.Demand(0,1,120);cache.Demand(1,1,120);cache.Publish(new NpcTrajectory(identity,(long)Main.GameUpdateCount-1,1,PredictionAssumption.None,PredictionStop.None,points,points.Length));Call(attack,"PrepareAction");
        }
    }
}
