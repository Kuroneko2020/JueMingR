using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
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
        private const int Slot=24;
        private static NPC Spawn(int type,byte generation)
        {var n=(NPC)AccessTools.Method(typeof(NPC),"NewNPCInstanceInSlot").Invoke(null,new object[]{Slot,generation});n.SetDefaults(type);n.active=true;n.position=new Vector2(1100,880);return n;}
        internal static Action PreserveNativeSlotData()
        {
            // Source births deliberately execute the original GLOBAL reset.
            // Preserve its complete mutable slot data once per cold fixture,
            // so a source witness cannot contaminate the next network leaf.
            var arrays=new List<Tuple<Array,Array>>();Action<Array> save=array=>{if(array!=null)arrays.Add(Tuple.Create(array,(Array)array.Clone()));};
            save(Projectile.perIDStaticNPCImmunity);foreach(var p in Main.projectile)save(p?.localNPCImmunity);
            foreach(var p in Main.player)
            {
                if(p==null)continue;save(p.meleeNPCHitCooldown);var stack=p.TagEffectStack;if(stack==null)continue;
                var states=(TagEffectState[])AccessTools.Field(typeof(TagEffectStack),"_effectStates").GetValue(stack);
                foreach(var state in states)if(state!=null){save((int[])AccessTools.Field(typeof(TagEffectState),"TimeLeftOnNPC").GetValue(state));save((int[])AccessTools.Field(typeof(TagEffectState),"ProcTimeLeftOnNPC").GetValue(state));}
            }
            save((int[])AccessTools.Field(typeof(Terraria.GameContent.PortalHelper),"PortalCooldownForNPCs").GetValue(null));
            int wall=Main.wofNPCIndex,golem=NPC.golemBoss,deer=NPC.deerclopsBoss,plant=NPC.plantBoss,crimson=NPC.crimsonBoss;
            return ()=>{foreach(var pair in arrays)Array.Copy(pair.Item2,pair.Item1,pair.Item1.Length);Main.wofNPCIndex=wall;NPC.golemBoss=golem;NPC.deerclopsBoss=deer;NPC.plantBoss=plant;NPC.crimsonBoss=crimson;};
        }
        internal static void SourceAmbiguity(object host,Func<int,byte[]> serialize)
        {
            var old=Main.npc[Slot];var human=Main.LocalPlayer;var position=human.position;
            try
            {
                Call(host,"ClearWorldSources");human.position=new Vector2(1000,880);
                var ordinary=Spawn(NPCID.GraniteGolem,110);ordinary.velocity=new Vector2(1,2);ordinary.ai[2]=0;ordinary.AI();
                Require(ordinary.ai[2]==1 && !ordinary.dontTakeDamage,"native normal Granite action produces positive counter1 and vulnerable state");var packet=serialize(Slot);
                var exit=Spawn(NPCID.GraniteGolem,110);exit.position=ordinary.position;exit.target=ordinary.target;exit.direction=ordinary.direction;exit.directionY=ordinary.directionY;exit.spriteDirection=ordinary.spriteDirection;Array.Copy(ordinary.ai,exit.ai,NPC.maxAI);exit.ai[2]=-10;
                exit.velocity=new Vector2((ordinary.velocity.X-exit.direction*.3f)/.9f,ordinary.velocity.Y);exit.AI();
                var other=serialize(Slot);int firstDifference=Enumerable.Range(0,Math.Min(packet.Length,other.Length)).FirstOrDefault(i=>packet[i]!=other[i]);
                Console.WriteLine("NPC SOURCE Granite normalAI="+ordinary.ai[2]+"/"+ordinary.dontTakeDamage+" velocity="+ordinary.velocity+" exitAI="+exit.ai[2]+"/"+exit.dontTakeDamage+" velocity="+exit.velocity+" packetsEqual="+packet.SequenceEqual(other)+" firstDifference="+firstDifference+" lengths="+packet.Length+"/"+other.Length);
                Require(exit.ai[2]==1 && exit.dontTakeDamage,"hand-selected pre-counter-10/vy2 reaches native exit counter1 while still invulnerable; joint natural history is not established");
                Require(packet.SequenceEqual(other),"two hand-selected primitive inputs can produce identical23 and distinct hit qualification; this does not prove natural baseline ambiguity");
            }
            finally{Main.npc[Slot]=old;human.position=position;Call(host,"ClearWorldSources");}
        }
        internal static void PublicationAmbiguity(object host,Func<int,byte[]> serialize)
        {
            var old=Main.npc[Slot];var human=Main.LocalPlayer;var position=human.position;
            try
            {
                Call(host,"ClearWorldSources");human.position=new Vector2(1000,880);
                foreach(int type in new[]{NPCID.GraniteGolem,NPCID.ArmedZombie})
                {
                    bool armed=type==NPCID.ArmedZombie;var start=new Vector2(1100,880);var ordinary=Spawn(type,111);
                    // Normal Fighter subtracts.07 while the attack multiplies
                    // by.9. Use the common exact float output.9; ai3 is also a
                    // legal prior stall counter and normal AI decrements2→1.
                    ordinary.velocity=new Vector2(armed?.9f+.07f:1,2);if(armed)ordinary.ai[3]=2;ordinary.UpdateNPC(Slot);var packet=serialize(Slot);
                    var exit=Spawn(type,111);exit.position=start;exit.target=ordinary.target;exit.direction=ordinary.direction;exit.directionY=ordinary.directionY;exit.spriteDirection=ordinary.spriteDirection;Array.Copy(ordinary.ai,exit.ai,NPC.maxAI);exit.ai[2]=armed?19:-10;
                    exit.velocity=new Vector2((ordinary.velocity.X-(armed?0:exit.direction*.3f))/.9f,2);exit.UpdateNPC(Slot);var other=serialize(Slot);
                    Console.WriteLine("NPC PUBLICATION type="+type+" fullUpdate=True normal="+ordinary.position+"/"+ordinary.velocity+"/ai="+String.Join(",",ordinary.ai)+"/damage="+ordinary.damage+"/dont="+ordinary.dontTakeDamage+" exit="+exit.position+"/"+exit.velocity+"/ai="+String.Join(",",exit.ai)+"/damage="+exit.damage+"/dont="+exit.dontTakeDamage+" full23Equal="+packet.SequenceEqual(other));
                    Require(armed?ordinary.ai[2]==0 && exit.ai[2]==0 && ordinary.damage==ordinary.defDamage && exit.damage==(int)(exit.defDamage*1.5):ordinary.ai[2]==1 && exit.ai[2]==1 && !ordinary.dontTakeDamage && exit.dontTakeDamage,"full native UpdateNPC preserves the distinct current combat state through gravity/collision/FindFrame/network stage");
                    Require(packet.SequenceEqual(other),"same full native23 at the publication boundary cannot encode these distinct current native combat fields");
                }
            }
            finally{Main.npc[Slot]=old;human.position=position;Call(host,"ClearWorldSources");}
        }
        internal static void Run(object host,Player first,Player second,object a,object b,Action<int> primary,Action<int> secondary,bool cleanup=false)
        {
            var original=Main.npc[Slot];var human=Main.LocalPlayer;var positions=new[]{human.position,first.position,second.position};
            try
            {
                human.position=new Vector2(1000,900);first.position=new Vector2(1050,900);second.position=new Vector2(1250,900);Call(host,"ClearWorldSources");
                if(cleanup){SlotCleanup(host,human,secondary);return;}byte generation=80;
                foreach(int type in new[]{NPCID.CaveBat,NPCID.CursedHammer,NPCID.FungiSpore})
                {
                    var source=Spawn(type,generation++);source.velocity=new Vector2(2,-1);source.UpdateNPC(Slot);var position=source.position;primary(Slot);var observed=((NPC[])Get(host,"observedNpcs"))[Slot];
                    Require(!ReferenceEquals(source,observed) && ReferenceEquals(Main.npc[Slot],source) && observed.noGravity==source.noGravity && observed.noTileCollide==source.noTileCollide && observed.position==position,"real native common-family AI -> native23 -> product decoded shared flags/geometry match source: "+type);
                    Require(type!=NPCID.FungiSpore || !observed.noTileCollide,"FungiSpore native reverse-default collision is retained, not generalized spore passage");
                    var before=observed.position;var velocity=observed.velocity;NativeQuickItemChecks.BeginWorldStep();Call(host,"AdvanceNpcObservation");
                    Require(!observed.noGravity || observed.velocity==velocity,"a no23 common flying observation receives no false gravity: "+type);
                    Require(observed.position!=before || velocity==Vector2.Zero,"shared native geometry advances the actual decoded body");
                    Console.WriteLine("NPC STATE family="+type+" flags="+observed.noGravity+"/"+observed.noTileCollide+" source="+position+" no23="+observed.position);
                }
            }
            finally{Main.npc[Slot]=original;human.position=positions[0];first.position=positions[1];second.position=positions[2];Call(host,"ClearWorldSources");}
        }
        private static void SlotCleanup(object host,Player human,Action<int> publish)
        {
            var locals=Main.projectile.Select(p=>p.localNPCImmunity[Slot]).ToArray();var statics=new uint[Projectile.perIDStaticNPCImmunity.GetLength(0)];for(int i=0;i<statics.Length;i++)statics[i]=Projectile.perIDStaticNPCImmunity[i,Slot];
            var melee=Main.player.Take(255).Select(p=>p.meleeNPCHitCooldown[Slot]).ToArray();var tags=human.TagEffectStack;
            var item=human.inventory[0];int selected=human.selectedItem,animation=human.itemAnimation,maximum=human.itemAnimationMax,maxTags=human.maxTagEffects;float range=human.whipRangeMultiplier,duration=human.tagEffectDuration;
            var projectiles=new List<Projectile>();
            try
            {
                human.TagEffectStack=new TagEffectStack(human);human.maxTagEffects=2;human.tagEffectDuration=1;human.whipRangeMultiplier=1;
                foreach(byte generation in new byte[]{90,91})
                {
                    var source=Spawn(NPCID.Zombie,generation);source.life=source.lifeMax=10000;source.defense=0;
                    Require(human.meleeNPCHitCooldown[Slot]==0 && Main.projectile.All(p=>p.localNPCImmunity[Slot]==0) && Enumerable.Range(0,statics.Length).All(t=>Projectile.perIDStaticNPCImmunity[t,Slot]==0) && !human.TagEffectStack.IsNPCTagged(4672,Slot),"true native birth/generation outside observation still performs global slot cleanup");
                    human.inventory[0]=new Item(4672);human.selectedItemState.Select(0);human.itemAnimation=human.itemAnimationMax=30;
                    var whip=Main.projectile[Projectile.NewProjectile(human.GetProjectileSource_Item(human.HeldItem),human.MountedCenter,new Vector2(4,0),841,1,0,human.whoAmI)];projectiles.Add(whip);whip.ai[0]=10;whip.AI();
                    var points=new List<Vector2>();Projectile.FillWhipControlPoints(whip,points,human,true,0);Require(points.Count>5,"real native whip publishes collision points");source.Center=points[points.Count/2];whip.Damage();
                    Require(human.TagEffectStack.IsNPCTagged(4672,Slot),"actual human-owned native whip hit establishes the tag, not a prefilled final tag marker");
                    source.immune[human.whoAmI]=0;
                    foreach(int type in new[]{76,119})
                    {
                        var shot=Main.projectile[Projectile.NewProjectile(new EntitySource_Parent(human),source.Center,Vector2.UnitX,type,1,0,human.whoAmI)];projectiles.Add(shot);shot.Damage();source.immune[human.whoAmI]=0;
                        Require(type==76?shot.localNPCImmunity[Slot]!=0:Projectile.perIDStaticNPCImmunity[shot.immunityIdentity,Slot]>Main.GameUpdateCount,"actual native human-owned projectile hit establishes its local/static immunity");
                    }
                    human.itemAnimation=15;Call(human,"ProcessHitAgainstNPC",new Item(ItemID.CopperShortsword),source.Hitbox,1,0f,Slot);
                    Require(human.meleeNPCHitCooldown[Slot]==15,"actual native human melee hit establishes its own cooldown");
                    var expectedLocal=projectiles.Select(p=>p.localNPCImmunity[Slot]).ToArray();var expectedStatic=new uint[statics.Length];for(int i=0;i<statics.Length;i++)expectedStatic[i]=Projectile.perIDStaticNPCImmunity[i,Slot];int health=source.life;
                    publish(Slot);var observed=((NPC[])Get(host,"observedNpcs"))[Slot];
                    Console.WriteLine("NPC SLOT decoded="+observed.generation+" expected="+generation+" humanRef="+ReferenceEquals(Main.npc[Slot],source)+" melee="+human.meleeNPCHitCooldown[Slot]+" tag="+human.TagEffectStack.IsNPCTagged(4672,Slot)+" localSame="+projectiles.Select(p=>p.localNPCImmunity[Slot]).SequenceEqual(expectedLocal)+" staticSame="+Enumerable.Range(0,statics.Length).All(t=>Projectile.perIDStaticNPCImmunity[t,Slot]==expectedStatic[t]));
                    Require(observed.generation==generation && !ReferenceEquals(observed,source) && ReferenceEquals(Main.npc[Slot],source) && source.life==health && human.meleeNPCHitCooldown[Slot]==15 && human.TagEffectStack.IsNPCTagged(4672,Slot) && projectiles.Select(p=>p.localNPCImmunity[Slot]).SequenceEqual(expectedLocal) && Enumerable.Range(0,statics.Length).All(t=>Projectile.perIDStaticNPCImmunity[t,Slot]==expectedStatic[t]),"actual observation23 instance creation preserves human hit cooldowns/tags and projectile local/static immunity");
                    source.spawnNeedsSyncing=true;publish(Slot);var recreated=((NPC[])Get(host,"observedNpcs"))[Slot];
                    Require(!ReferenceEquals(recreated,observed) && recreated.generation==generation && ReferenceEquals(Main.npc[Slot],source) && human.meleeNPCHitCooldown[Slot]==15 && human.TagEffectStack.IsNPCTagged(4672,Slot) && projectiles.Select(p=>p.localNPCImmunity[Slot]).SequenceEqual(expectedLocal) && Enumerable.Range(0,statics.Length).All(t=>Projectile.perIDStaticNPCImmunity[t,Slot]==expectedStatic[t]),"same-generation native spawn sync bit also isolates copy cleanup");source.spawnNeedsSyncing=false;
                    Console.WriteLine("NPC STATE slot cleanup generation="+generation+" nativeHumanHits=True tag=True melee=15 local/static=retained copyRecreated=True");
                }
                byte bossGeneration=92;
                foreach(int type in new[]{113,245,668,262,266})
                {
                    Call(host,"ClearWorldSources");var source=Spawn(type,bossGeneration++);source.OnSpawn(new EntitySource_Sync());
                    int[] indices={Main.wofNPCIndex,NPC.golemBoss,NPC.deerclopsBoss,NPC.plantBoss,NPC.crimsonBoss};
                    int owner=type==113?0:type==245?1:type==668?2:type==262?3:4;
                    Require(indices[owner]==Slot,"real native OnSpawn establishes this boss's actual global slot index");publish(Slot);
                    Require(new[]{Main.wofNPCIndex,NPC.golemBoss,NPC.deerclopsBoss,NPC.plantBoss,NPC.crimsonBoss}.SequenceEqual(indices) && ReferenceEquals(Main.npc[Slot],source),"observation copy reset/OnSpawn preserves all human-world boss indices for native type"+type);
                    // A newer secondary sample may arrive before the primary
                    // has replaced its old boss. Use actual native objects on
                    // each side; Zombie.OnSpawn cannot recreate a cleared boss
                    // index and therefore cannot conceal an erroneous reset.
                    var newer=Spawn(NPCID.Zombie,(byte)(bossGeneration+1));var humanBoss=Spawn(type,bossGeneration);bossGeneration+=2;humanBoss.OnSpawn(new EntitySource_Sync());
                    indices=new[]{Main.wofNPCIndex,NPC.golemBoss,NPC.deerclopsBoss,NPC.plantBoss,NPC.crimsonBoss};
                    try{Main.npc[Slot]=newer;publish(Slot);}finally{Main.npc[Slot]=humanBoss;}
                    Require(new[]{Main.wofNPCIndex,NPC.golemBoss,NPC.deerclopsBoss,NPC.plantBoss,NPC.crimsonBoss}.SequenceEqual(indices) && ReferenceEquals(Main.npc[Slot],humanBoss) && ((NPC[])Get(host,"observedNpcs"))[Slot].type==NPCID.Zombie,"different-type secondary observation cannot clear the human's still-existing native boss slot index"+type);
                }
                Console.WriteLine("NPC STATE slot cleanup fiveNativeBossIndices=True observationOnSpawnIsolated=True");
            }
            finally
            {
                foreach(var p in projectiles)p.active=false;for(int i=0;i<locals.Length;i++)Main.projectile[i].localNPCImmunity[Slot]=locals[i];for(int i=0;i<statics.Length;i++)Projectile.perIDStaticNPCImmunity[i,Slot]=statics[i];for(int i=0;i<melee.Length;i++)Main.player[i].meleeNPCHitCooldown[Slot]=melee[i];
                human.TagEffectStack=tags;human.inventory[0]=item;human.selectedItemState.Select(selected);human.itemAnimation=animation;human.itemAnimationMax=maximum;human.maxTagEffects=maxTags;human.whipRangeMultiplier=range;human.tagEffectDuration=duration;
            }
        }
    }
}
