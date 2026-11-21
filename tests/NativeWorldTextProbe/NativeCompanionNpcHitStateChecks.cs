using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.DataStructures;
using Terraria.GameContent.Items;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionNpcStateChecks
    {
        internal static void HitGeneration(object host,Player first,Player second,object actor,Func<int,byte[]> serialize,Action<byte[]> secondary,Action<byte[]> primary,List<byte[]> frames)
        {
            var human=Main.LocalPlayer;var players=new[]{human,first,second};var positions=players.Select(p=>p.position).ToArray();var original=Main.npc[Slot];var projectiles=(Projectile[])Main.projectile.Clone();var shots=new List<Projectile>();int mode=Main.netMode;
            var restore=PreserveNativeSlotData();var hooks=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionHooks");Func<IDisposable> enter=()=>(IDisposable)hooks.GetMethod("Enter",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{actor,first.Center,false});
            var other=Call(host,"Owned",second);Func<IDisposable> enterOther=()=>(IDisposable)hooks.GetMethod("Enter",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{other,second.Center,false});
            var actors=new[]{actor,other};var savedViews=actors.Select(a=>((IDictionary)Get(a,"NpcViews")).Cast<DictionaryEntry>().ToArray()).ToArray();var savedQueues=actors.Select(a=>((IEnumerable)Get(a,"PendingStrikes")).Cast<object>().ToArray()).ToArray();var globals=(IList)HarmonyLib.AccessTools.Field(typeof(NPC),"pendingDamages").GetValue(null);var pending=globals.Cast<object>().ToArray();
            var tags=first.TagEffectStack;var item=first.inventory[0];int selected=first.selectedItem,animation=first.itemAnimation,maximum=first.itemAnimationMax,maxTags=first.maxTagEffects;float range=first.whipRangeMultiplier,duration=first.tagEffectDuration;
            Func<Player,int,NPC,Projectile> shot=(owner,type,npc)=>{var p=Main.projectile[Projectile.NewProjectile(new EntitySource_Parent(owner),npc.Center,Vector2.UnitX,type,10,0,owner.whoAmI)];shots.Add(p);return p;};
            try
            {
                Call(host,"ClearWorldSources");human.position=new Vector2(1000,900);first.position=new Vector2(1050,900);second.position=new Vector2(1250,900);
                // An independent server birth legitimately clears SERVER slot
                // state. Its side effects must not pre-clear this client's
                // already established A-generation immunity before B arrives.
                Main.netMode=2;NPC source;try{source=Spawn(NPCID.Zombie,40);source.life=source.lifeMax=10000;source.defense=0;}finally{Main.netMode=1;}
                first.TagEffectStack=new TagEffectStack(first);first.maxTagEffects=2;first.tagEffectDuration=1;first.whipRangeMultiplier=1;first.inventory[0]=new Item(4672);first.selectedItemState.Select(0);first.itemAnimation=first.itemAnimationMax=30;Projectile whip;
                using(enter()){whip=Main.projectile[Projectile.NewProjectile(first.GetProjectileSource_Item(first.HeldItem),first.MountedCenter,new Vector2(4,0),841,10,0,first.whoAmI)];shots.Add(whip);whip.ai[0]=10;whip.AI();}
                var points=new List<Vector2>();Projectile.FillWhipControlPoints(whip,points,first,true,0);Require(points.Count>5,"actual AI-owned native whip produces collision points");source.Center=points[points.Count/2];
                secondary(serialize(Slot));var observed=((NPC[])Get(host,"observedNpcs"))[Slot];Console.WriteLine("NPC HIT INITIAL sourceGen="+source.generation+" observedGen="+observed.generation+" active="+observed.active+" ready="+Get(actor,"Ready")+" sourcePos="+source.position+" first="+first.position+" baseline="+Call(host,"NpcBaselineCurrent",Slot));Require(observed.generation==source.generation,"initial real secondary23 establishes generation A");Projectile local;int hits=frames.Count(p=>p[2]==28);using(enter()){local=shot(first,85,observed);local.Damage();}
                Require(local.active && local.damage>0 && local.localNPCImmunity[Slot]==-1 && frames.Count(p=>p[2]==28)==hits+1,"actual living damaging native85 hit establishes permanent local immunity to generation A");
                using(enter()){whip.Damage();first.itemAnimation=15;Call(first,"ProcessHitAgainstNPC",new Item(ItemID.CopperShortsword),observed.Hitbox,1,0f,Slot);}
                Require(first.TagEffectStack.IsNPCTagged(4672,Slot) && first.meleeNPCHitCooldown[Slot]==15,"actual AI whip and melee hits establish generation-A tag and cooldown before the new source birth");
                source.immune[human.whoAmI]=0;var staticHuman=shot(human,119,source);staticHuman.Damage();uint humanExpiry=Projectile.perIDStaticNPCImmunity[staticHuman.immunityIdentity,Slot];Require(humanExpiry>Main.GameUpdateCount,"actual human native119 hit establishes the shared A-generation static expiry");
                hits=frames.Count(p=>p[2]==28);using(enter())shot(first,119,observed).Damage();Require(frames.Count(p=>p[2]==28)==hits,"the same-incarnation human static hit also blocks an AI's same-type projectile");
                var client=PreserveNativeSlotData();NPC newer;try{Main.netMode=2;newer=Spawn(NPCID.Zombie,41);newer.life=newer.lifeMax=10000;newer.defense=0;newer.position=source.position;}finally{Main.netMode=1;client();Main.npc[Slot]=source;}
                byte[] packet;try{Main.npc[Slot]=newer;packet=serialize(Slot);}finally{Main.npc[Slot]=source;}
                Require(local.localNPCImmunity[Slot]==-1 && Projectile.perIDStaticNPCImmunity[staticHuman.immunityIdentity,Slot]==humanExpiry && Main.npc[Slot].generation==source.generation,"server source birth did not secretly clear the independent client's A-generation timers");
                secondary(packet);observed=((NPC[])Get(host,"observedNpcs"))[Slot];Require(observed.generation==newer.generation && observed.generation!=source.generation && ReferenceEquals(Main.npc[Slot],source),"secondary B arrives while primary A object/generation remains unchanged");local.Center=observed.Center;hits=frames.Count(p=>p[2]==28);using(enter())local.Damage();
                Require(first.meleeNPCHitCooldown[Slot]==0 && !first.TagEffectStack.IsNPCTagged(4672,Slot),"B admission clears only this AI incarnation's old melee cooldown and tag");
                Console.WriteLine("NPC HIT GENERATION secondary="+newer.generation+" primary="+source.generation+" native85Alive="+local.active+" local="+local.localNPCImmunity[Slot]+" newDamage28="+(frames.Count(p=>p[2]==28)-hits)+" humanStaticSame="+(Projectile.perIDStaticNPCImmunity[staticHuman.immunityIdentity,Slot]==humanExpiry));
                Require(frames.Count(p=>p[2]==28)==hits+1,"the same living native85 projectile can actually hit B after hitting A, without waiting for primary Reset");
                Require(Projectile.perIDStaticNPCImmunity[staticHuman.immunityIdentity,Slot]==humanExpiry && ReferenceEquals(Main.npc[Slot],source),"B admission preserves the human A-generation static timer and primary object");
                Projectile staticAi;uint observedExpiry;hits=frames.Count(p=>p[2]==28);using(enter()){staticAi=shot(first,119,observed);staticAi.Damage();observedExpiry=Projectile.perIDStaticNPCImmunity[staticAi.immunityIdentity,Slot];}
                Require(frames.Count(p=>p[2]==28)==hits+1 && observedExpiry>Main.GameUpdateCount && Projectile.perIDStaticNPCImmunity[staticAi.immunityIdentity,Slot]==humanExpiry,"B static hit is not blocked by human A and does not clear A's timer");
                var otherFrames=(System.Collections.IEnumerable)Get(other,"PendingStrikes");int otherHits=otherFrames.Cast<object>().Count();using(enterOther())shot(second,119,observed).Damage();Require(otherFrames.Cast<object>().Count()==otherHits,"ALL AI share B's same-type static cooldown rather than receiving one table per actor");
                using(enter()){whip.Damage();first.itemAnimation=15;Call(first,"ProcessHitAgainstNPC",new Item(ItemID.CopperShortsword),observed.Hitbox,1,0f,Slot);}
                Require(first.TagEffectStack.IsNPCTagged(4672,Slot) && first.meleeNPCHitCooldown[Slot]==15,"real B whip/melee hits establish NEW incarnation tag/cooldown before primary catches up");
                primary(packet);Require(Main.npc[Slot].generation==newer.generation && local.localNPCImmunity[Slot]==-1 && first.meleeNPCHitCooldown[Slot]==15 && first.TagEffectStack.IsNPCTagged(4672,Slot) && Projectile.perIDStaticNPCImmunity[staticAi.immunityIdentity,Slot]==observedExpiry,"late real primary NewNPCInstance/Reset adopts B static state and preserves AI B local/melee/tag");
                int primaryHealth=Main.npc[Slot].life;shot(human,119,Main.npc[Slot]).Damage();Require(Main.npc[Slot].life==primaryHealth,"after primary catches up, the same B incarnation again shares native static cooldown with the human");
                using(enter())staticAi.ResetStaticImmunityMatching(observedExpiry);Require(Projectile.perIDStaticNPCImmunity[staticAi.immunityIdentity,Slot]==0,"actual native matching-time clear mirrors the same-generation static cell back to primary");
                var shared=Main.npc[Slot];shared.spawnNeedsSyncing=true;primary(serialize(Slot));Require(local.localNPCImmunity[Slot]==0 && first.meleeNPCHitCooldown[Slot]==0 && !first.TagEffectStack.IsNPCTagged(4672,Slot),"a true same-generation primary spawn-bit reset still performs native shared local/melee/tag cleanup");
                Console.WriteLine("NPC HIT GENERATION nativeLocalMinus1=True ownMelee/TagReset=True humanOldStaticRetained=True sameGenerationHuman/AllAIStaticShared=True latePrimaryAdoptsNewState=True nativeMatchingClearMirrored=True trueSameGenReset=True");
            }
            finally{Main.netMode=mode;Main.npc[Slot]=original;Call(host,"ClearWorldSources");foreach(var p in shots){p.active=false;Main.projectile[p.whoAmI]=projectiles[p.whoAmI];}for(int i=0;i<players.Length;i++)players[i].position=positions[i];restore();first.TagEffectStack=tags;first.inventory[0]=item;first.selectedItemState.Select(selected);first.itemAnimation=animation;first.itemAnimationMax=maximum;first.maxTagEffects=maxTags;first.whipRangeMultiplier=range;first.tagEffectDuration=duration;globals.Clear();foreach(var value in pending)globals.Add(value);for(int i=0;i<actors.Length;i++){var views=(IDictionary)Get(actors[i],"NpcViews");views.Clear();foreach(var entry in savedViews[i])views.Add(entry.Key,entry.Value);var queue=Get(actors[i],"PendingStrikes");Call(queue,"Clear");foreach(var entry in savedQueues[i])Call(queue,"Enqueue",entry);}}
        }
    }
}
