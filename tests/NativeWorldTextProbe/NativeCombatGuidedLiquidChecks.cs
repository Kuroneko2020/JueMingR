using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatGuidedLiquidChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static NPC receiver;
        private static int frame,firstDamage;
        private static void Struck(NPC __instance,int __result)
        {if(ReferenceEquals(receiver,__instance) && __result>0 && firstDamage<0)firstDamage=frame;}
        internal static void Run(object context)
        {
            var audit=new Harmony("JueMingR.Tests.GuidedLiquidReceive");
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatGuidedLiquidChecks).GetMethod(nameof(Struck),Flags)));
            try
            {
                foreach(int weapon in new[]{113,495})foreach(int liquid in new[]{0,2})Check(context,weapon,liquid);
                Check(context,218,0);Check(context,218,1);
            }
            finally{receiver=null;audit.UnpatchAll(audit.Id);}
        }
        private static void Check(object context,int weapon,int liquid)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var input=Get(context,"Input");var attack=Get(combat,"Attack");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,weapon,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.channel=p.controlUseItem=true;p.releaseUseItem=true;Main.screenPosition=new Vector2(600,400);
            p.statMana=p.statManaMax=p.statManaMax2=1000;
            var n=NativeCombatControlPreviewChecks.Dummy(2,56,40);
            receiver=n;firstDamage=-1;frame=-1;
            for(int x=43;x<=60;x++)for(int y=37;y<=42;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(liquid);}
            NativeToolExecutionChecks.Sample(context,input,new Vector2(600,500),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,false,false,true,50,false,true));
            NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,0,phaseName:"BeforeNpc");
            typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});
            var shot=Main.projectile.Single(q=>q.active && q.owner==0 && q.type==p.HeldItem.shoot);
            NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,0,true);
            var entry=((IDictionary)Get(Get(attack,"Control"),"shots"))[(int)shot.key];var contact=(AttackContact)GetOptional(entry,"Contact");
            bool dies=weapon==218 && liquid==0;
            Require(dies?contact==null:contact!=null,"guided liquid Contact respects ignoreWater/water death/lava gate: weapon="+weapon+" liquid="+liquid);
            int first=-1;Vector2 aim=contact==null?n.Center:new Vector2(contact.AimX,contact.AimY);
            for(int tick=0;tick<40 && shot.active && first<0;tick++)
            {
                Vector2 old=shot.Center,velocity=shot.velocity,delta=aim-old;
                Vector2 expected=old+(delta.Length()>=64?delta.SafeNormalize(Vector2.Zero)*32:velocity*.3f+delta*.3f);
                // Native 488 is immortal: StrikeNPC returns positive damage
                // without subtracting life. Its real receive, not life loss,
                // is the outcome oracle, as in the existing knife checks.
                frame=tick;shot.Update(shot.whoAmI);first=firstDamage;
                if(!dies && shot.active && first<0)Require(Vector2.DistanceSquared(expected,shot.Center)<.01f,"ignoring water/honey and flame lava gate retain original full-speed AI009 displacement: weapon="+weapon+" liquid="+liquid+" tick="+tick);
                if(weapon!=218)Require(!shot.wet,"native ignoreWater missiles never acquire a liquid slowdown flag");
                if(weapon==218 && liquid==1 && shot.active)Require(!shot.wet && !shot.lavaWet,"native flame-lash lava exception clears wet/lava before movement");
            }
            Require(dies?!shot.active && first<0:first>=0 && first==contact.Tick,"guided liquid model agrees with original death or first Damage clock: weapon="+weapon+" liquid="+liquid+" actual="+first+" predicted="+(contact?.Tick??-1));
            Console.WriteLine("PASS guided liquid weapon="+weapon+" liquid="+liquid+" nativeFirstDamage="+first+" nativeAlive="+shot.active+" nativeImmortal="+n.immortal);
        }
    }
}
