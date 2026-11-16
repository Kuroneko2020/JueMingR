using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
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
        internal static void BattlePublication(object host,Player first,Player second,object a,object b,Action<int> publish,List<byte[]> fa,List<byte[]> fb)
        {
            var human=Main.LocalPlayer;var players=new[]{human,first,second};var positions=players.Select(p=>p.position).ToArray();var old=Main.npc[Slot];var actors=new[]{a,b};var savedViews=actors.Select(x=>((IDictionary)Get(x,"NpcViews")).Cast<DictionaryEntry>().ToArray()).ToArray();var savedQueues=actors.Select(x=>((IEnumerable)Get(x,"PendingStrikes")).Cast<object>().ToArray()).ToArray();var projectiles=(Projectile[])Main.projectile.Clone();var shots=new List<Projectile>();var sections=Main.sectionManager;
            var hooks=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionHooks");Func<object,IDisposable> enter=actor=>(IDisposable)hooks.GetMethod("Enter",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{actor,((Player)Get(actor,"Body")).Center,false});
            var globals=(IList)HarmonyLib.AccessTools.Field(typeof(NPC),"pendingDamages").GetValue(null);var pending=globals.Cast<object>().ToArray();
            Func<Player,NPC,Projectile> arrow=(owner,npc)=>{var shot=Main.projectile[Projectile.NewProjectile(new EntitySource_Parent(owner),npc.Center,Vector2.UnitX,ProjectileID.WoodenArrowFriendly,1,0,owner.whoAmI)];shots.Add(shot);return shot;};
            try
            {
                Call(host,"ClearWorldSources");foreach(var actor in actors){((IDictionary)Get(actor,"NpcViews")).Clear();Call(Get(actor,"PendingStrikes"),"Clear");}human.position=new Vector2(1000,918);first.position=new Vector2(1050,918);second.position=new Vector2(1250,918);
                var source=Spawn(NPCID.GraniteGolem,210);source.position.Y=960-source.height;source.UpdateNPC(Slot);Require(source.ai[2]==1 && !source.dontTakeDamage,"natural normal Granite first action publishes vulnerable positive1");publish(Slot);var observed=((NPC[])Get(host,"observedNpcs"))[Slot];long stamp=(long)Call(host,"NpcSampleTick",observed),revision=(long)Call(host,"NpcObservationRevision",observed);int hits=fa.Count(p=>p[2]==28);
                Require(!observed.dontTakeDamage && !(bool)Call(host,"ObservedNpcCombatReady",observed),"fresh unknown positive counter is a local qualification state, not a fabricated immunity flag");
                using(enter(a)){Call(first,"ProcessHitAgainstNPC",new Item(ItemID.CopperShortsword),observed.Hitbox,1,0f,Slot);arrow(first,observed).Damage();first.ApplyDamageToNPC(observed,1,0,1,false,new PlayerNPCHitSource(0,0,"unknown Granite action"));}
                Require(fa.Count(p=>p[2]==28)==hits && !((IEnumerable)Get(a,"PendingStrikes")).Cast<object>().Any(),"unknown Granite phase cannot reach any of the three actual native outgoing-hit/pending outlets");
                Main.sectionManager=new WorldSections(1,1);NativeQuickItemChecks.BeginWorldStep();Call(host,"AdvanceNpcObservation");Require(!(bool)Call(host,"ObservedNpcCombatReady",observed),"unreceived terrain cannot turn elapsed time into a qualified Granite action");Main.sectionManager=sections;NativeQuickItemChecks.BeginWorldStep();Call(host,"AdvanceNpcObservation");
                Require((bool)Call(host,"ObservedNpcCombatReady",observed) && !observed.dontTakeDamage && (long)Call(host,"NpcSampleTick",observed)==stamp && (long)Call(host,"NpcObservationRevision",observed)==revision && !(bool)Call(host,"FreshNpcObservation",observed),"actual qualified NPC action establishes normal hit inputs without relabelling the advanced raw sample");
                for(int packet=0;packet<3;packet++){source.UpdateNPC(Slot);publish(Slot);Require(source.ai[2]>0 && source.ai[2]<=10 && (bool)Call(host,"ObservedNpcCombatReady",observed) && !observed.dontTakeDamage,"continuous normal positive Granite packets do not repeatedly close established combat");}
                using(enter(a))Call(first,"ProcessHitAgainstNPC",new Item(ItemID.CopperShortsword),observed.Hitbox,1,0f,Slot);using(enter(b))arrow(second,observed).Damage();Require(((IEnumerable)Get(a,"PendingStrikes")).Cast<object>().Count()==1 && ((IEnumerable)Get(b,"PendingStrikes")).Cast<object>().Count()==1,"established Granite phase permits real native melee and another actor's actual projectile hit");
                Console.WriteLine("NPC BATTLE PHASE Granite nativePositive1=True noFakeDont=True unknown3OutletsBlocked=True terrainRequired=True actualActionReady=True positive23x3NoStarvation=True knownMelee/Projectile=True");

                Call(host,"ClearWorldSources");foreach(var actor in actors){((IDictionary)Get(actor,"NpcViews")).Clear();Call(Get(actor,"PendingStrikes"),"Clear");}source=Spawn(419,211);source.position.Y=960-source.height;human.Center=source.Center+new Vector2(-20,0);source.TargetClosest();source.UpdateNPC(Slot);
                Require(source.ai[2]==-1 && !source.reflectsProjectiles,"actual default0 naturally enters its legal charge at-1 without reflection");int actions=0;
                while(!source.reflectsProjectiles && actions++<12){NativeQuickItemChecks.BeginWorldStep();source.UpdateNPC(Slot);}
                Require(source.reflectsProjectiles && source.takenDamageMultiplier==3 && source.ai[2]<-7,"complete native charge/launch actions establish real spin without pre-filling a negative counter");publish(Slot);observed=((NPC[])Get(host,"observedNpcs"))[Slot];int damageFrames=fb.Count(p=>p[2]==28);
                using(enter(b)){var reflected=arrow(second,observed);reflected.Damage();Require(reflected.reflected && reflected.hostile && fb.Count(p=>p[2]==28)==damageFrames,"natural spin actually reflects the arrow and sends no damage28");}
                // A real ordinary hit is the other native exit producer. This
                // avoids making a high-speed flight leave the tiny world's
                // finite floor merely to wait for a landing event.
                human.ApplyDamageToNPC(source,1,0,1,false,new PlayerNPCHitSource(0,0,"native Solenian hit exit"));Require(source.justHit,"actual native ordinary hit establishes the source exit event, not a hand-filled justHit flag");
                NativeQuickItemChecks.BeginWorldStep();source.UpdateNPC(Slot);Call(host,"AdvanceNpcObservation");Require(source.ai[2]==60 && source.reflectsProjectiles && source.takenDamageMultiplier==3,"original just-hit action publishes60 while retaining its current reflection3");publish(Slot);Require(observed.reflectsProjectiles && observed.takenDamageMultiplier==3 && (bool)Call(host,"ObservedNpcCombatReady",observed),"real natural exit60 retains CURRENT reflection3 without an invented61 source or an unnecessary qualification gate");
                NativeQuickItemChecks.BeginWorldStep();source.UpdateNPC(Slot);Call(host,"AdvanceNpcObservation");Require(source.ai[2]==59 && !source.reflectsProjectiles && !observed.reflectsProjectiles && observed.takenDamageMultiplier==1,"real following positive action restores source and shared non-reflection together");
                using(enter(b)){var ordinary=arrow(second,observed);ordinary.Damage();Require(!ordinary.reflected && fb.Count(p=>p[2]==28)==damageFrames+1,"following normal action permits the actual arrow damage28 outlet");}
                Console.WriteLine("NPC BATTLE PHASE Solenian natural0->charge->launch->spin=True actualArrowReflection=True nativeHitExit60True=True next59False=True actualNormalArrow28=True sourceActions="+actions);
            }
            finally
            {
                Main.sectionManager=sections;Main.npc[Slot]=old;Call(host,"ClearWorldSources");foreach(var shot in shots){shot.active=false;Main.projectile[shot.whoAmI]=projectiles[shot.whoAmI];}for(int i=0;i<players.Length;i++)players[i].position=positions[i];globals.Clear();foreach(var value in pending)globals.Add(value);
                for(int i=0;i<actors.Length;i++){var actor=actors[i];var views=(IDictionary)Get(actor,"NpcViews");views.Clear();foreach(var entry in savedViews[i])views.Add(entry.Key,entry.Value);var queue=Get(actor,"PendingStrikes");Call(queue,"Clear");foreach(var entry in savedQueues[i])Call(queue,"Enqueue",entry);}
            }
        }
    }
}
