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
using Terraria.Net.Sockets;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionNpcStateChecks
    {
        private static bool measuringContact,measuringAura;private static float contactInput;private static int auraBaseDamage;
        private static void DamageInput(float __0){if(measuringContact)contactInput=__0;}
        private static void DamageOutput(int __result){if(measuringAura)auraBaseDamage=__result;}
        private static readonly List<byte[]> battleServer=new List<byte[]>();
        private static bool BattlePacket(byte[] data){battleServer.Add(data.Take(BitConverter.ToUInt16(data,0)).ToArray());return false;}
        internal static void Battle(object host,Player first,Player second,object actor,Action<int> publish,List<byte[]> frames)
        {
            var human=Main.LocalPlayer;var players=new[]{human,first,second};var positions=players.Select(p=>p.position).ToArray();var original=Main.npc[Slot];var shots=new List<Projectile>();
            var oldProjectiles=(Projectile[])Main.projectile.Clone();var views=(IDictionary)Get(actor,"NpcViews");var oldViews=views.Cast<DictionaryEntry>().ToArray();var queue=Get(actor,"PendingStrikes");var oldQueue=((IEnumerable)queue).Cast<object>().ToArray();
            int health=first.statLife,max=first.statLifeMax,max2=first.statLifeMax2,immuneTime=first.immuneTime,ap=first.armorPenetration;bool immune=first.immune;var cooldowns=(int[])first.hurtCooldowns.Clone();
            var clients=Netplay.Clients;var buffers=NetMessage.buffer;int mode=Main.netMode;bool dedicated=Main.dedServ;var patch=new Harmony("JueMingR.Tests.CompanionBattleConsumers");
            patch.Patch(AccessTools.Method(typeof(Main),"DamageVar",new[]{typeof(float),typeof(float)}),prefix:new HarmonyMethod(typeof(NativeCompanionNpcStateChecks),nameof(DamageInput)),postfix:new HarmonyMethod(typeof(NativeCompanionNpcStateChecks),nameof(DamageOutput)));
            patch.Patch(AccessTools.Method(typeof(NetMessage),"SendPacket"),prefix:new HarmonyMethod(typeof(NativeCompanionNpcStateChecks),nameof(BattlePacket)){priority=Priority.First,before=new[]{"JueMingR.Tests.QuickItemOutlets"}});
            var hooks=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionHooks");
            Func<IDisposable> enter=()=>(IDisposable)hooks.GetMethod("Enter",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{actor,first.Center,false});
            Func<NPC,Projectile> arrow=n=>{var p=Main.projectile[Projectile.NewProjectile(new EntitySource_Parent(first),n.Center,Vector2.UnitX,ProjectileID.WoodenArrowFriendly,40,0,first.whoAmI)];shots.Add(p);return p;};
            try
            {
                Call(host,"ClearWorldSources");views.Clear();Call(queue,"Clear");human.position=new Vector2(1000,918);first.position=new Vector2(1050,918);second.position=new Vector2(1250,918);
                var source=Spawn(NPCID.GraniteGolem,130);source.position.Y=960-source.height;source.ai[2]=300;source.UpdateNPC(Slot);Require(source.ai[2]==-120 && !source.dontTakeDamage,"native Granite entry chooses the defense timer before its following protected action");source.UpdateNPC(Slot);
                Require(source.ai[2]<0 && source.dontTakeDamage,"native following Granite action actually produces hit immunity");publish(Slot);var observed=((NPC[])Get(host,"observedNpcs"))[Slot];int count=frames.Count(p=>p[2]==28),sourceLife=source.life;
                using(enter()){Call(first,"ProcessHitAgainstNPC",new Item(ItemID.CopperShortsword),observed.Hitbox,40,0f,Slot);arrow(observed).Damage();}
                Require(observed.dontTakeDamage && frames.Count(p=>p[2]==28)==count && ((IEnumerable)queue).Cast<object>().Count()==0 && source.life==sourceLife,"real melee and projectile front gates read shared immunity and emit no invalid28/private pending");
                source.ai[2]=30;source.UpdateNPC(Slot);Require(!source.dontTakeDamage,"native normal Granite phase revokes immunity");publish(Slot);Require(!observed.dontTakeDamage,"same identity decoded normal phase revokes shared immunity");using(enter())arrow(observed).Damage();Require(frames.Count(p=>p[2]==28)>count && source.life==sourceLife,"actual native projectile accepts the now-vulnerable shared phase into private prediction/28 only");
                Console.WriteLine("NPC BATTLE Granite nativeEntry=True melee/projectile immunity=True reverse=True sourcePreserved=True");

                Call(host,"ClearWorldSources");views.Clear();Call(queue,"Clear");source=Spawn(690,131);source.position.Y=960-source.height;source.UpdateNPC(Slot);publish(Slot);observed=((NPC[])Get(host,"observedNpcs"))[Slot];
                Require(observed.immortal && observed.dontTakeDamage,"native dormant StatueMimic produces both distinct hit fields in the shared copy");source.ai[0]=1;source.UpdateNPC(Slot);Require(!source.dontTakeDamage && !source.immortal,"native awake Statue action revokes both fields");publish(Slot);NativeQuickItemChecks.BeginWorldStep();Call(host,"AdvanceNpcObservation");Require(!observed.dontTakeDamage && !observed.immortal,"first actual local awake action revokes shared immortality as well as selection immunity");

                Call(host,"ClearWorldSources");views.Clear();Call(queue,"Clear");source=Spawn(NPCID.ArmedZombie,134);source.position.Y=960-source.height;source.velocity=Vector2.Zero;human.Center=source.Center+new Vector2(-20,0);source.UpdateNPC(Slot);Require(source.ai[2]==1 && source.damage==source.defDamage,"native armed fighter naturally enters its close attack with base contact damage");source.UpdateNPC(Slot);Require(source.ai[2]==2 && source.damage==(int)(source.defDamage*1.5),"following native attack action establishes the boosted current damage");publish(Slot);observed=((NPC[])Get(host,"observedNpcs"))[Slot];
                first.Center=observed.Center;first.statLife=first.statLifeMax=first.statLifeMax2=1000;first.immune=false;first.immuneTime=0;Array.Clear(first.hurtCooldowns,0,first.hurtCooldowns.Length);int humanLife=human.statLife,secondLife=second.statLife;contactInput=0;measuringContact=true;
                try{using(enter())first.Update_NPCCollision();}finally{measuringContact=false;}
                Require(observed.damage==source.damage && contactInput==source.damage && first.statLife<1000 && human.statLife==humanLife && second.statLife==secondLife && ReferenceEquals(Main.npc[Slot],source),"actual original contact DamageVar/Hurt consumes current boosted shared damage and hurts only its actor");
                Console.WriteLine("NPC BATTLE Armed naturalEntry=True nativeContactInput="+contactInput+" health=1000->"+first.statLife+" human/other preserved=True");human.position=new Vector2(1000,918);first.position=new Vector2(1050,918);

                Call(host,"ClearWorldSources");views.Clear();Call(queue,"Clear");source=Spawn(419,132);source.position.Y=960-source.height;source.velocity=new Vector2(1,-2);source.ai[2]=-7;source.UpdateNPC(Slot);
                Require(source.reflectsProjectiles && source.takenDamageMultiplier==3f,"native Solenian active spin produces reflection and multiplier together");publish(Slot);observed=((NPC[])Get(host,"observedNpcs"))[Slot];count=frames.Count(p=>p[2]==28);Projectile reflected;
                using(enter()){reflected=arrow(observed);reflected.Damage();}
                Require(observed.reflectsProjectiles && observed.takenDamageMultiplier==3 && reflected.reflected && reflected.hostile && !reflected.friendly && frames.Count(p=>p[2]==28)==count && ((IEnumerable)queue).Cast<object>().Count()==0,"actual native projectile reflection consumes shared spin, changes the projectile and prevents damage28/pending");
                source.ai[2]=59;source.UpdateNPC(Slot);publish(Slot);Require(!observed.reflectsProjectiles && observed.takenDamageMultiplier==1f,"native positive cooldown restores BOTH reflection and damage multiplier");
                Console.WriteLine("NPC BATTLE Solenian nativeReflection=True hostileArrow=True noDamage28=True multiplier3->1=True");

                Call(host,"ClearWorldSources");views.Clear();Call(queue,"Clear");source=Spawn(471,133);source.position.Y=880;source.ai[3]=1;source.UpdateNPC(Slot);publish(Slot);observed=((NPC[])Get(host,"observedNpcs"))[Slot];
                Require(!(bool)Call(host,"ObservedNpcCombatReady",observed),"received conditional Summoner phase needs its actual local field-producing action before client armor compensation");NativeQuickItemChecks.BeginWorldStep();Call(host,"AdvanceNpcObservation");
                Require(source.defense==source.defDefense+10 && observed.defense==source.defense,"native Summoner defense stage reaches the shared damage input");first.armorPenetration=100;int expected=40+observed.checkArmorPenetration(first.GetArmorPenetration(false),0);count=frames.Count(p=>p[2]==28);
                using(enter())first.ApplyDamageToNPC(observed,40,0,1,false,new PlayerNPCHitSource(0,0,"shared defense armor penetration"));var hit=frames.SkipWhile(p=>p[2]!=28).Last(p=>p[2]==28);
                // Native28: slot byte, generation byte, then damage Int16.
                int serializedDamage=BitConverter.ToInt16(hit,5);
                Require(frames.Count(p=>p[2]==28)==count+1 && serializedDamage==expected && expected>40,"native checkArmorPenetration reads current defense before serializing the actual damage28");
                Console.WriteLine("NPC BATTLE Summoner defense="+observed.defense+" nativeAPDamage28="+serializedDamage+" expected="+expected);
                views.Clear();Call(queue,"Clear");first.armorPenetration=0;count=frames.Count(p=>p[2]==28);Projectile aura;
                using(enter())
                {
                    aura=Main.projectile[Projectile.NewProjectile(new EntitySource_Parent(first),observed.Center,Vector2.Zero,688,40,0,first.whoAmI)];shots.Add(aura);
                    // Complete native aura setup/clock, not a fabricated final
                    // hitbox or percent-penetration flag. Its natural pulse
                    // occurs when AI wraps the real30-action counter to zero.
                    for(int step=0;step<30;step++)aura.AI();Require(aura.ai[0]==0 && aura.localAI[0]==1 && aura.Colliding(aura.Hitbox,observed.Hitbox),"native LightningAura establishes its actual shape and reaches a real eligible pulse");measuringAura=true;try{aura.Damage();}finally{measuringAura=false;}
                }
                var auraHit=frames.SkipWhile(p=>p[2]!=28).Last(p=>p[2]==28);int auraDamage=BitConverter.ToInt16(auraHit,5);
                Console.WriteLine("NPC BATTLE Aura actual28count="+(frames.Count(p=>p[2]==28)-count)+" randomBase="+auraBaseDamage+" damage28="+auraDamage+" compensation="+observed.checkArmorPenetration(0,1));
                Require(frames.Count(p=>p[2]==28)==count+1 && auraBaseDamage>0 && auraDamage==auraBaseDamage+observed.checkArmorPenetration(0,1),"actual LightningAura Damage applies native100percent armor compensation using shared current defense after real DamageVar");
                Netplay.Clients=new RemoteClient[256];for(int i=0;i<256;i++)Netplay.Clients[i]=new RemoteClient{Id=i,Socket=new TcpSocket()};NetMessage.buffer=new MessageBuffer[257];for(int i=0;i<257;i++)NetMessage.buffer[i]=new MessageBuffer{whoAmI=i};Netplay.Clients[first.whoAmI].Socket=Netplay.Connection.Socket;Netplay.Clients[first.whoAmI].State=10;
                int serverLife=source.life;Main.netMode=2;Main.dedServ=true;battleServer.Clear();var decoder=new MessageBuffer{whoAmI=first.whoAmI};Buffer.BlockCopy(auraHit,2,decoder.readBuffer,0,auraHit.Length-2);decoder.ResetReader();int decoded;decoder.GetData(0,auraHit.Length-2,out decoded);Main.netMode=1;Main.dedServ=dedicated;
                Require(decoded==28 && source.life==serverLife-auraBaseDamage && battleServer.Any(p=>p[2]==162),"real native server28 decoder subtracts current defense from compensated aura damage and emits162");
                Console.WriteLine("NPC BATTLE Aura native30pulse=True damage28="+auraDamage+" serverLife="+serverLife+"->"+source.life+" actual162=True");
            }
            finally
            {
                measuringContact=measuringAura=false;patch.UnpatchAll(patch.Id);Main.netMode=mode;Main.dedServ=dedicated;Netplay.Clients=clients;NetMessage.buffer=buffers;battleServer.Clear();
                foreach(var shot in shots){shot.active=false;Main.projectile[shot.whoAmI]=oldProjectiles[shot.whoAmI];}Main.npc[Slot]=original;Call(host,"ClearWorldSources");for(int i=0;i<players.Length;i++)players[i].position=positions[i];
                first.statLife=health;first.statLifeMax=max;first.statLifeMax2=max2;first.immune=immune;first.immuneTime=immuneTime;first.armorPenetration=ap;Array.Copy(cooldowns,first.hurtCooldowns,cooldowns.Length);views.Clear();foreach(var entry in oldViews)views.Add(entry.Key,entry.Value);Call(queue,"Clear");foreach(var value in oldQueue)Call(queue,"Enqueue",value);
            }
        }
    }
}
