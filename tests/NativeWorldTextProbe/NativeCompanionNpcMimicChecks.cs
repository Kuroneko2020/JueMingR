using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.DataStructures;
using Terraria.GameContent;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionNpcStateChecks
    {
        internal static void MimicPhase(object host,Player first,Player second,object a,object b,Action<int> publish,List<byte[]> fa,List<byte[]> fb)
        {
            var human=Main.LocalPlayer;var players=new[]{human,first,second};var positions=players.Select(p=>p.position).ToArray();var old=new[]{Main.npc[Slot],Main.npc[25]};var sections=Main.sectionManager;var oldTarget=Main.player[10];int mode=Main.netMode;
            var actors=new[]{a,b};var savedViews=actors.Select(x=>((IDictionary)Get(x,"NpcViews")).Cast<DictionaryEntry>().ToArray()).ToArray();var savedQueues=actors.Select(x=>((IEnumerable)Get(x,"PendingStrikes")).Cast<object>().ToArray()).ToArray();var oldProjectiles=(Projectile[])Main.projectile.Clone();var shots=new List<Projectile>();
            int health=first.statLife,max=first.statLifeMax,max2=first.statLifeMax2,immuneTime=first.immuneTime;bool immune=first.immune;var cooldowns=(int[])first.hurtCooldowns.Clone();var hooks=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionHooks");
            Func<object,IDisposable> enter=actor=>(IDisposable)hooks.GetMethod("Enter",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{actor,((Player)Get(actor,"Body")).Center,false});
            Func<Player,NPC,Projectile> arrow=(owner,npc)=>{var p=Main.projectile[Projectile.NewProjectile(new EntitySource_Parent(owner),npc.Center,Vector2.UnitX,ProjectileID.WoodenArrowFriendly,1,0,owner.whoAmI)];shots.Add(p);return p;};
            try
            {
                Call(host,"ClearWorldSources");foreach(var actor in actors){((IDictionary)Get(actor,"NpcViews")).Clear();Call(Get(actor,"PendingStrikes"),"Clear");}human.position=new Vector2(1000,918);first.position=new Vector2(1050,918);second.position=new Vector2(1250,918);
                NPC source=null;
                // The native random branch itself chooses4. Inputs are a
                // grounded legal action-selection counter, not final flags.
                for(int attempt=0;attempt<32;attempt++)
                {
                    source=Spawn(473,(byte)(150+attempt));source.position.Y=960-source.height;source.ai[0]=2;source.ai[2]=211;source.TargetClosest();Main.netMode=0;try{source.UpdateNPC(Slot);}finally{Main.netMode=1;}if(source.ai[0]==4)break;
                }
                Console.WriteLine("NPC MIMIC sourceEntry ai="+String.Join(",",source.ai)+" flags="+source.noGravity+"/"+source.noTileCollide+" kb="+source.knockBackResist+" expectedKB="+source.GetKnockbackMultiplier_ScaledByDifficulty(.2f)+" position="+source.position+" velocity="+source.velocity+" active="+source.active+" target="+source.target);
                Require(source.ai[0]==4 && !source.noGravity && source.noTileCollide && source.knockBackResist==source.GetKnockbackMultiplier_ScaledByDifficulty(.2f),"actual native2->4 publication has entry flags/knockback, distinct from its next4 action");publish(Slot);var observed=((NPC[])Get(host,"observedNpcs"))[Slot];long tick=(long)Call(host,"NpcSampleTick",observed),revision=(long)Call(host,"NpcObservationRevision",observed);
                Require(!(bool)Call(host,"ObservedNpcCombatReady",observed) && !observed.noGravity,"fresh ambiguous phase is not relabelled as the future action's sample flag");int hits=fa.Count(p=>p[2]==28);
                using(enter(a)){Call(first,"ProcessHitAgainstNPC",new Item(ItemID.CopperShortsword),observed.Hitbox,1,0f,Slot);arrow(first,observed).Damage();first.ApplyDamageToNPC(observed,1,0f,1,false,new PlayerNPCHitSource(0,0,"unprepared mimic phase"));}
                Require(fa.Count(p=>p[2]==28)==hits && !((IEnumerable)Get(a,"PendingStrikes")).Cast<object>().Any(),"unestablished phase cannot enter actual melee/projectile/direct pending or28 outlets");
                var other=(NPC)AccessTools.Method(typeof(NPC),"NewNPCInstanceInSlot").Invoke(null,new object[]{25,(byte)190});other.SetDefaults(NPCID.Zombie);other.active=true;other.position=new Vector2(1220,918);publish(25);var target=((NPC[])Get(host,"observedNpcs"))[25];
                using(enter(b)){Call(host,"SelectTarget",b);second.ApplyDamageToNPC(target,1,0,1,false,new PlayerNPCHitSource(0,0,"other actor continues during phase preparation"));}
                Require(ReferenceEquals(Get(b,"Target"),target) && fb.Any(p=>p[2]==28) && ((IEnumerable)Get(b,"PendingStrikes")).Cast<object>().Count()==1,"another actor selects a different ready target and really attacks while the mimic phase is unavailable");
                first.Center=observed.Center;first.statLife=first.statLifeMax=first.statLifeMax2=1000;first.immune=false;first.immuneTime=0;Array.Clear(first.hurtCooldowns,0,first.hurtCooldowns.Length);int humanLife=human.statLife,secondLife=second.statLife;using(enter(a))first.Update_NPCCollision();
                Require(first.statLife<1000 && human.statLife==humanLife && second.statLife==secondLife && observed.position==source.position,"unestablished strike inputs do not disable actual native contact Hurt or freeze/replace received geometry");first.position=new Vector2(1050,918);
                Main.sectionManager=new WorldSections(1,1);NativeQuickItemChecks.BeginWorldStep();Call(host,"AdvanceNpcObservation");Require(!(bool)Call(host,"ObservedNpcCombatReady",observed),"a clock step without qualified terrain cannot establish the local phase");Main.sectionManager=sections;
                NativeQuickItemChecks.BeginWorldStep();Call(host,"AdvanceNpcObservation");Require((bool)Call(host,"ObservedNpcCombatReady",observed) && observed.noGravity && observed.noTileCollide && observed.knockBackResist==0 && (long)Call(host,"NpcSampleTick",observed)==tick && (long)Call(host,"NpcObservationRevision",observed)==revision && !(bool)Call(host,"FreshNpcObservation",observed),"actual qualified NPC world action establishes pure flags/KB without inventing a fresh sample or revising raw identity");
                for(int packet=0;packet<3;packet++){source.UpdateNPC(Slot);Require(source.ai[0]==4 && source.noGravity && source.knockBackResist==0,"actual native source remains in stable4");publish(Slot);Require((bool)Call(host,"ObservedNpcCombatReady",observed),"continuous same-phase23 preserves the established local phase instead of starving combat");}
                using(enter(a)){Call(first,"ProcessHitAgainstNPC",new Item(ItemID.CopperShortsword),observed.Hitbox,1,5f,Slot);first.ApplyDamageToNPC(observed,1,5f,1,false,new PlayerNPCHitSource(0,0,"prepared mimic direct strike"));}
                using(enter(b))arrow(second,observed).Damage();
                var view=(NPC)Get(((IDictionary)Get(a,"NpcViews"))[Slot],"Body");Require(((IEnumerable)Get(a,"PendingStrikes")).Cast<object>().Count()==2 && ((IEnumerable)Get(b,"PendingStrikes")).Cast<object>().Count()==2 && view.velocity==observed.velocity && view.knockBackResist==0,"established native melee/direct/projectile hits enter own pending/28, and actual private Strike uses zero current knockback");
                Main.player[10]=new Player{whoAmI=10,active=true,position=new Vector2(1100,1000)};source.ai[0]=4.1f;source.ai[2]=0;source.target=10;source.velocity=new Vector2(0,2);source.UpdateNPC(Slot);Require(source.ai[0]==4.1f && source.ai[2]==0 && source.noGravity && source.noTileCollide,"native current4.1 above a blocked lower target actually produces its conditional flags");publish(Slot);Main.player[10].active=false;
                for(int step=0;step<2;step++){NativeQuickItemChecks.BeginWorldStep();Call(host,"AdvanceNpcObservation");Require(!(bool)Call(host,"ObservedNpcCombatReady",observed),"lost necessary player premise cannot become ready solely because another clock/world step passed");}Main.player[10].active=true;NativeQuickItemChecks.BeginWorldStep();Call(host,"AdvanceNpcObservation");Require((bool)Call(host,"ObservedNpcCombatReady",observed),"restored actual target premise plus a successful world action establishes conditional4.1");
                var replacement=Spawn(473,(byte)(source.generation+1));replacement.ai[0]=4;replacement.UpdateNPC(Slot);publish(Slot);var next=((NPC[])Get(host,"observedNpcs"))[Slot];Require(!ReferenceEquals(next,observed) && !(bool)Call(host,"ObservedNpcCombatReady",next),"true native generation recreation cannot inherit the old local phase qualification");
                NativeQuickItemChecks.BeginWorldStep();Call(host,"AdvanceNpcObservation");Require((bool)Call(host,"ObservedNpcCombatReady",next),"new generation establishes its own phase after its own actual action");replacement.SetDefaults(474);replacement.ai[0]=4;replacement.UpdateNPC(Slot);publish(Slot);Require(ReferenceEquals(next,((NPC[])Get(host,"observedNpcs"))[Slot]) && next.netID==474 && !(bool)Call(host,"ObservedNpcCombatReady",next),"same-object native netID transformation invalidates prior local phase ownership");
                Console.WriteLine("NPC MIMIC actualEntry2->4=True unknownNativeHitOutletsBlocked=True otherActorAttack=True actualContact=True terrainRequired=True targetRequired=True worldPhaseEstablished=True samePhase23x3NoStarvation=True knownMelee/Direct/Projectile=True privateKB0=True rawTick/RevisionPreserved=True newGen/netIDNeedsPreparation=True");
            }
            finally
            {
                Main.netMode=mode;Main.sectionManager=sections;Main.player[10]=oldTarget;Main.npc[Slot]=old[0];Main.npc[25]=old[1];Call(host,"ClearWorldSources");foreach(var shot in shots){shot.active=false;Main.projectile[shot.whoAmI]=oldProjectiles[shot.whoAmI];}for(int i=0;i<players.Length;i++)players[i].position=positions[i];first.statLife=health;first.statLifeMax=max;first.statLifeMax2=max2;first.immune=immune;first.immuneTime=immuneTime;Array.Copy(cooldowns,first.hurtCooldowns,cooldowns.Length);
                for(int i=0;i<actors.Length;i++){var actor=actors[i];var views=(IDictionary)Get(actor,"NpcViews");views.Clear();foreach(var entry in savedViews[i])views.Add(entry.Key,entry.Value);var queue=Get(actor,"PendingStrikes");Call(queue,"Clear");foreach(var entry in savedQueues[i])Call(queue,"Enqueue",entry);Set(actor,"Target",null);Set(actor,"HasAim",false);}
            }
        }
    }
}
