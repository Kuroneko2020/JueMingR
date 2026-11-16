using System;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionNpcStateChecks
    {
        internal static void ArmedCycle(object host,Player first,Player second,object actor,Action<int> publish,bool fresh=false)
        {
            var human=Main.LocalPlayer;var players=new[]{human,first,second};var positions=players.Select(p=>p.position).ToArray();var original=Main.npc[Slot];int health=first.statLife,max=first.statLifeMax,max2=first.statLifeMax2,immuneTime=first.immuneTime;bool immune=first.immune;var cooldowns=(int[])first.hurtCooldowns.Clone();var patch=new Harmony("JueMingR.Tests.CompanionArmedContactClock");
            var hooks=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionHooks");patch.Patch(AccessTools.Method(typeof(Main),"DamageVar",new[]{typeof(float),typeof(float)}),prefix:new HarmonyMethod(typeof(NativeCompanionNpcStateChecks),nameof(DamageInput)));
            try
            {
                Call(host,"ClearWorldSources");first.position=new Vector2(1050,918);second.position=new Vector2(1250,918);var source=Spawn(NPCID.ArmedZombie,200);source.position.Y=960-source.height;human.Center=source.Center+new Vector2(-20,0);source.TargetClosest();source.UpdateNPC(Slot);
                Require(source.ai[2]==1 && source.damage==source.defDamage,"complete native armed NPC naturally enters its close attack at1 with base contact damage");NPC observed=null;long tick=0,revision=0;if(!fresh){publish(Slot);observed=((NPC[])Get(host,"observedNpcs"))[Slot];tick=(long)Call(host,"NpcSampleTick",observed);revision=(long)Call(host,"NpcObservationRevision",observed);}
                for(int step=1;step<=19;step++)
                {
                    NativeQuickItemChecks.BeginWorldStep();source.UpdateNPC(Slot);Require(source.ai[2]==(step==19?0:step+1) && source.damage==(int)(source.defDamage*1.5),"native source completes its actual accepted attack through terminal0, retaining boosted damage on that terminal action");
                    if(!fresh){Call(host,"AdvanceNpcObservation");Require(observed.ai[2]==source.ai[2] && observed.damage==source.damage && (long)Call(host,"NpcSampleTick",observed)==tick && (long)Call(host,"NpcObservationRevision",observed)==revision && !(bool)Call(host,"FreshNpcObservation",observed),"no23 current damage AND native contact clock advance together without making a fresh sample");}
                }
                publish(Slot);var current=((NPC[])Get(host,"observedNpcs"))[Slot];if(!fresh)Require(ReferenceEquals(current,observed) && current.ai[2]==0 && current.damage==source.damage,"terminal0 real packet preserves the same instance's actually established last-action damage instead of prematurely restoring base");
                first.Center=current.Center;first.statLife=first.statLifeMax=first.statLifeMax2=1000;first.immune=false;first.immuneTime=0;Array.Clear(first.hurtCooldowns,0,first.hurtCooldowns.Length);int humanLife=human.statLife,secondLife=second.statLife;contactInput=0;measuringContact=true;
                try{using((IDisposable)hooks.GetMethod("Enter",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{actor,first.Center,false}))first.Update_NPCCollision();}finally{measuringContact=false;}
                Console.WriteLine("NPC ARMED fresh="+fresh+" sourceNaturalCycle1..19..0=True sourceDamage="+source.damage+" observedDamage="+current.damage+" actualContactInput="+contactInput+" health=1000->"+first.statLife+" rawClock="+current.ai[2]);
                Require(contactInput==source.damage && first.statLife<1000 && human.statLife==humanLife && second.statLife==secondLife && ReferenceEquals(Main.npc[Slot],source),"actual native contact must use the natural terminal action's boosted damage, for both established and fresh baseline");
                NativeQuickItemChecks.BeginWorldStep();source.UpdateNPC(Slot);Call(host,"AdvanceNpcObservation");Require(current.damage==source.damage && current.damage==source.defDamage,"following actual local action restores base damage, without inventing a new attack decision");Console.WriteLine("NPC ARMED established terminal history/contact=True followingBaseRestore=True");
            }
            finally
            {measuringContact=false;patch.UnpatchAll(patch.Id);Main.npc[Slot]=original;Call(host,"ClearWorldSources");for(int i=0;i<players.Length;i++)players[i].position=positions[i];first.statLife=health;first.statLifeMax=max;first.statLifeMax2=max2;first.immune=immune;first.immuneTime=immuneTime;Array.Copy(cooldowns,first.hurtCooldowns,cooldowns.Length);}
        }
    }
}
