using System;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionNpcStateChecks
    {
        internal static void Explosion(object host,Player first,Player second,object actor,Action<int> publish)
        {
            var human=Main.LocalPlayer;var players=new[]{human,first,second};var positions=players.Select(p=>p.position).ToArray();var old=Main.npc[Slot];int health=first.statLife,max=first.statLifeMax,max2=first.statLifeMax2,immuneTime=first.immuneTime;bool immune=first.immune;var cooldowns=(int[])first.hurtCooldowns.Clone();
            var patch=new Harmony("JueMingR.Tests.CompanionExplosionWindow");patch.Patch(AccessTools.Method(typeof(Main),"DamageVar",new[]{typeof(float),typeof(float)}),prefix:new HarmonyMethod(typeof(NativeCompanionNpcStateChecks),nameof(DamageInput)));var hooks=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionHooks");
            Action contact=()=>{using((IDisposable)hooks.GetMethod("Enter",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{actor,first.Center,false}))first.Update_NPCCollision();};
            try
            {
                human.position=new Vector2(1000,918);first.position=new Vector2(1050,918);second.position=new Vector2(1250,918);
                foreach(int type in new[]{572,388})
                {
                    Call(host,"ClearWorldSources");var source=Spawn(type,(byte)(type==572?230:231));source.position.Y=960-source.height;source.TargetClosest();var initialSize=source.Size;
                    if(type==572){source.ai[0]=1;source.ai[1]=1;}else human.Center=source.Center+new Vector2(-20,0);
                    source.UpdateNPC(Slot);Require(type==572?source.ai[0]==0 && source.ai[1]==2 && source.width<192:source.ai[0]==3 && source.ai[1]==1 && source.width==192,"actual original explosion producer distinguishes delayed walker ENTRY from same-action drone expansion");publish(Slot);var observed=((NPC[])Get(host,"observedNpcs"))[Slot];
                    Require(observed.Size==source.Size && observed.position==source.position && observed.damage==source.damage,"actual entry23 gives the same current shape/position/damage, without prematurely expanding delayed entry");
                    if(type==572){NativeQuickItemChecks.BeginWorldStep();source.UpdateNPC(Slot);Call(host,"AdvanceNpcObservation");Require(source.ai[0]==1 && observed.ai[0]==1 && source.width==192 && observed.Size==source.Size && observed.position==source.position && observed.velocity==source.velocity && observed.damage==source.damage,"first actual accepted walker action expands about its center once and restores native80 damage/stop/tail together");}
                    Require(source.damage==source.GetAttackDamage_ScaledByDifficulty(80f),"the explosion's actual current damage is the original scaled80 producer");
                    first.position=new Vector2(observed.Right.X-1,observed.Top.Y+4);first.statLife=first.statLifeMax=first.statLifeMax2=1000;first.immune=false;first.immuneTime=0;Array.Clear(first.hurtCooldowns,0,first.hurtCooldowns.Length);int humanLife=human.statLife,otherLife=second.statLife;contactInput=0;measuringContact=true;try{contact();}finally{measuringContact=false;}
                    Require(contactInput==source.damage && first.statLife<1000 && human.statLife==humanLife && second.statLife==otherLife && ReferenceEquals(Main.npc[Slot],source),"native contact consumes the expanded shared body and scaled80 damage for this actor only");int afterHit=first.statLife;
                    long stamp=(long)Call(host,"NpcSampleTick",observed),revision=(long)Call(host,"NpcObservationRevision",observed);int actions=0;
                    while(source.active && actions++<3){NativeQuickItemChecks.BeginWorldStep();source.UpdateNPC(Slot);Call(host,"AdvanceNpcObservation");Require(observed.active==source.active,"accepted local explosion clock ends its contact window on exactly the native source action without another23");}
                    Require(!observed.active && !source.active && (long)Call(host,"NpcSampleTick",observed)==stamp && (long)Call(host,"NpcObservationRevision",observed)==revision,"deterministic local window completion is not a new sample or a forged authoritative death packet");
                    // Independent unprotected contact probe: immunity from the
                    // earlier positive hit must not conceal a stale hitbox.
                    first.immune=false;first.immuneTime=0;Array.Clear(first.hurtCooldowns,0,first.hurtCooldowns.Length);contactInput=0;measuringContact=true;try{contact();}finally{measuringContact=false;}
                    Require(first.statLife==afterHit && contactInput==0,"expired local contact window produces no Hurt even for an independently unprotected overlapping actor");Console.WriteLine("NPC EXPLOSION type="+type+" entrySize="+initialSize+" current192/damage80=True nativeContact1000->"+afterHit+" no23Expires=True rawTick/RevisionUnchanged=True unprotectedExpiredNoHurt=True");
                }
            }
            finally{measuringContact=false;patch.UnpatchAll(patch.Id);Main.npc[Slot]=old;Call(host,"ClearWorldSources");for(int i=0;i<players.Length;i++)players[i].position=positions[i];first.statLife=health;first.statLifeMax=max;first.statLifeMax2=max2;first.immune=immune;first.immuneTime=immuneTime;Array.Copy(cooldowns,first.hurtCooldowns,cooldowns.Length);}
        }
    }
}
