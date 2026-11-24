using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.DataStructures;
using Terraria.Net.Sockets;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionNpcStateChecks
    {
        private static readonly List<byte[]> armedPackets=new List<byte[]>();private static int armedPeer;
        private static bool ArmedPacket(byte[] data,int remoteClient)
        {if(remoteClient==armedPeer)armedPackets.Add(data.Take(BitConverter.ToUInt16(data,0)).ToArray());return false;}
        private static void ArmedSection(Point point){}
        private static void ArmedClientSection(int client,Point point){}
        internal static void ArmedCycle(object host,Player first,Player second,object actor,Action<byte[]> deliver,bool fresh=false)
        {
            var human=Main.LocalPlayer;var players=new[]{human,first,second};var positions=players.Select(p=>p.position).ToArray();var velocities=players.Select(p=>p.velocity).ToArray();var original=Main.npc;var clients=Netplay.Clients;var buffers=NetMessage.buffer;int mode=Main.netMode,sx=Main.maxSectionsX,sy=Main.maxSectionsY;bool dedicated=Main.dedServ;
            int health=first.statLife,max=first.statLifeMax,max2=first.statLifeMax2,immuneTime=first.immuneTime;bool immune=first.immune;var cooldowns=(int[])first.hurtCooldowns.Clone();var activeTime=AccessTools.Field(typeof(ActiveSections),"LastActiveTime");var oldActive=activeTime.GetValue(null);var restore=PreserveNativeSlotData();
            var sourceTable=PlainNpcTable();var clientTable=PlainNpcTable();var plain=PlainNpcTable();var patch=new Harmony("JueMingR.Tests.CompanionArmedClientParity");var hooks=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionHooks");
            patch.Patch(AccessTools.Method(typeof(Main),"DamageVar",new[]{typeof(float),typeof(float)}),prefix:new HarmonyMethod(typeof(NativeCompanionNpcStateChecks),nameof(DamageInput)));
            patch.Patch(AccessTools.Method(typeof(NetMessage),"SendPacket"),prefix:new HarmonyMethod(typeof(NativeCompanionNpcStateChecks),nameof(ArmedPacket)){priority=Priority.First,before=new[]{"JueMingR.Tests.QuickItemOutlets"}});
            ActiveSections.SectionActivated+=ArmedSection;RemoteClient.NetSectionActivated+=ArmedClientSection;armedPeer=first.whoAmI;int published=0,received=0,starts=0,extendedHits=0;
            try
            {
                Call(host,"ClearWorldSources");Main.npc=clientTable;human.position=new Vector2(1000,918);first.position=new Vector2(1050,918);second.position=new Vector2(1250,918);Main.maxSectionsX=Main.maxSectionsY=1;activeTime.SetValue(null,new uint[2,2]);
                Netplay.Clients=new RemoteClient[256];for(int i=0;i<256;i++)Netplay.Clients[i]=new RemoteClient{Id=i};NetMessage.buffer=new MessageBuffer[257];for(int i=0;i<257;i++)NetMessage.buffer[i]=new MessageBuffer{whoAmI=i};
                // Server-only history exists before the fresh receiver joins.
                // It is not falsely attributed to the new client or used as
                // its expected damage. No registered receiver drops packets.
                Main.npc=sourceTable;Main.netMode=2;Main.dedServ=true;var source=Spawn(NPCID.ArmedZombie,200);source.position.Y=960-source.height;human.Center=source.Center+new Vector2(-20,0);source.TargetClosest();
                if(fresh)for(int action=0;action<20;action++){NativeQuickItemChecks.BeginWorldStep();source.UpdateNPC(Slot);}
                if(fresh)Require(source.ai[2]==0 && source.damage==(int)(source.defDamage*1.5),"real server history ends its swing with raw0/current21 before this receiver's first baseline");
                Main.npc=clientTable;Main.netMode=1;Main.dedServ=dedicated;
                Netplay.Clients[armedPeer]=Program.CreateRemoteClient(armedPeer,10,Netplay.Connection.Socket);Netplay.Clients[armedPeer].TileSections[0,0]=true;NetMessage.buffer[armedPeer].broadcast=true;
                Action<Action> server=action=>{Main.npc=sourceTable;Main.netMode=2;Main.dedServ=true;try{RemoteClient.CheckSection(armedPeer,source.Center,0);Require(Netplay.Clients[armedPeer].State==10 && Netplay.Clients[armedPeer].Socket.IsConnected() && NetMessage.buffer[armedPeer].broadcast && Netplay.Clients[armedPeer].IsSectionActive(source.NetSectionCoordinates),"real server mode has a connected/broadcast receiver in the actual active NPC section");action();}finally{Main.npc=clientTable;Main.netMode=1;Main.dedServ=dedicated;}};
                Action packets=()=>{foreach(var frame in armedPackets.Where(p=>p[2]==23 || p[2]==54)){PlainNpcPacket(host,plain,frame);deliver(frame);if(frame[2]==23)received++;}};
                // One first-baseline publication. Subsequent window traffic is
                // solely the original UpdateNPC/UpdateNetworkCode condition;
                // every actual NPC packet is fed to BOTH clients in FIFO order.
                armedPackets.Clear();server(()=>NetMessage.SendData(23,armedPeer,-1,null,Slot));Require(armedPackets.Count(p=>p[2]==23)==1,"native first baseline serializes one actual23 for this receiver");packets();var observed=((NPC[])Get(host,"observedNpcs"))[Slot];
                Require(plain[Slot].damage==observed.damage && plain[Slot].damage==plain[Slot].defDamage && plain[Slot].ai[2]==observed.ai[2],"first original client decode keeps defaults damage and the received clock, including fresh terminal0 without hidden server21");
                Console.WriteLine("NPC ARMED DECODE fresh="+fresh+" server="+source.damage+" ordinary="+plain[Slot].damage+" product="+observed.damage+" raw="+observed.ai[2]+" samePacket=True noServerHistoryImposed=True");
                Func<Rectangle,bool,float> contact=(victim,reference)=>
                {
                    var position=first.position;var velocity=first.velocity;try{first.position=new Vector2(victim.X,victim.Y);first.statLife=first.statLifeMax=first.statLifeMax2=1000;first.immune=false;first.immuneTime=0;Array.Clear(first.hurtCooldowns,0,first.hurtCooldowns.Length);contactInput=0;measuringContact=true;
                        if(reference)PlainNpc(host,plain,()=>first.Update_NPCCollision(),first.whoAmI);
                        else using((IDisposable)hooks.GetMethod("Enter",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{actor,first.Center,false}))first.Update_NPCCollision();
                        Require(first.statLife<1000,"actual native contact reaches Hurt in this reference/product consumer phase");return contactInput;}
                    finally{measuringContact=false;first.position=position;first.velocity=velocity;}
                };
                if(fresh)
                {
                    var victim=plain[Slot].Hitbox;var box=new Rectangle(victim.X,victim.Y,first.width,first.height);float ordinary=contact(box,true),actual=contact(box,false);Require(ordinary==actual && actual==plain[Slot].defDamage,"fresh raw0 contact matches the ordinary receiver14, not server-only current21");
                    Console.WriteLine("NPC ARMED FRESH ordinaryContact="+ordinary+" productContact="+actual+" serverOnlyDamage="+source.damage+" sameDecodePhase=True continueSharedFighterMotion=True nativeAi3="+plain[Slot].ai[3]+" vx="+plain[Slot].velocity.X+" justHit="+plain[Slot].justHit+" direction/sprite="+plain[Slot].direction+"/"+plain[Slot].spriteDirection);
                }
                float previous=plain[Slot].ai[2];bool crossed=false,rightExtension=false,freshHop=false;int freshContacts=0,hitInputs=0;
                for(int action=0;action<46;action++)
                {
                    NativeQuickItemChecks.BeginWorldStep();armedPackets.Clear();server(()=>source.UpdateNPC(Slot));published+=armedPackets.Count(p=>p[2]==23);packets();
                    if(action==22){human.Center=plain[Slot].Center+new Vector2(20,0);crossed=true;}
                    if(fresh && action==5)
                    {
                        // The same actual local hit input on each receiving
                        // body, not hand-filled justHit/ai3. Zero knockback
                        // isolates its one-action stuck-clock/reset lifetime.
                        PlainNpc(host,plain,()=>plain[Slot].StrikeNPC(1,0f,1,false,true));
                        PlainNpc(host,(NPC[])Get(host,"observedNpcs"),()=>observed.StrikeNPC(1,0f,1,false,true));
                        Require(plain[Slot].justHit && observed.justHit,"native Strike establishes the same actual hit input before the Fighter action");hitInputs++;
                    }
                    PlainNpc(host,plain,()=>plain[Slot].UpdateNPC(Slot));Call(host,"AdvanceNpcObservation");observed=((NPC[])Get(host,"observedNpcs"))[Slot];var ordinary=plain[Slot];
                    if(observed.position!=ordinary.position || observed.velocity!=ordinary.velocity || observed.ai[3]!=ordinary.ai[3])Console.WriteLine("NPC FIGHTER MOTION fresh="+fresh+" action="+action+" ordinary="+ordinary.position+"/"+ordinary.velocity+" ai3="+ordinary.ai[3]+" old="+ordinary.oldPosition+" product="+observed.position+"/"+observed.velocity+" ai3="+observed.ai[3]+" old="+observed.oldPosition);
                    Require(observed.position==ordinary.position && observed.velocity==ordinary.velocity && observed.ai[3]==ordinary.ai[3],"same packets/history/player/terrain and phase preserve ordinary Fighter geometry, velocity and ai3 through fresh unstuck and subsequent swings");
                    Require(observed.justHit==ordinary.justHit && !observed.justHit,"the native hit input is retired once after this local action and its one physics tail");
                    if(fresh && action==0){Require(ordinary.velocity.Y<0f && observed.velocity.Y==ordinary.velocity.Y,"the former fresh failure now takes the same real supported entry-zero-velocity unstuck hop");freshHop=true;Console.WriteLine("NPC FIGHTER FIRST HOP ordinary="+ordinary.position+"/"+ordinary.velocity+" product="+observed.position+"/"+observed.velocity);}
                    Require(observed.ai[2]==ordinary.ai[2] && observed.damage==ordinary.damage,"same actual packet/history/input/consumer phase keeps the ordinary contact clock and damage across subsequent local swings");
                    Require(observed.target==ordinary.target && observed.direction==ordinary.direction && (!crossed || ordinary.direction==1),"a real player crosses after packet consumption but before the local NPC action: the same native target primitive updates the current direction for both receivers");
                    if(previous==0 && ordinary.ai[2]>0)starts++;previous=ordinary.ai[2];var body=ordinary.Hitbox;var victim=new Rectangle(ordinary.spriteDirection<0?body.X-first.width-1:body.Right+1,body.Y+4,first.width,first.height);int expectedSetter=0,actualSetter=0;float expectedMultiplier=1,actualMultiplier=1;var expected=body;var actual=observed.Hitbox;
                    PlainNpc(host,plain,()=>NPC.GetMeleeCollisionData(victim,Slot,ref expectedSetter,ref expectedMultiplier,ref expected));using((IDisposable)hooks.GetMethod("Enter",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{actor,first.Center,false}))NPC.GetMeleeCollisionData(victim,Slot,ref actualSetter,ref actualMultiplier,ref actual);
                    if(expected!=actual || expectedMultiplier!=actualMultiplier || expectedSetter!=actualSetter)Console.WriteLine("NPC ARMED RECT action="+action+" sourceClock="+source.ai[2]+" published="+published+" ordinary="+ordinary.position+"/"+ordinary.velocity+" size="+ordinary.Size+" direction="+ordinary.direction+" sprite="+ordinary.spriteDirection+" clock="+ordinary.ai[2]+" rect="+expected+" product="+observed.position+"/"+observed.velocity+" size="+observed.Size+" direction="+observed.direction+" sprite="+observed.spriteDirection+" clock="+observed.ai[2]+" rect="+actual);
                    Require(expected==actual && expectedMultiplier==actualMultiplier && expectedSetter==actualSetter,"the actual native extended contact rectangle/multiplier matches the ordinary receiver's second and later local swing");
                    if(fresh && action<2)
                    {var box=new Rectangle(body.X,body.Y,first.width,first.height);float expectedInput=contact(box,true),actualInput=contact(box,false);Require(actualInput==expectedInput && actualInput==ordinary.damage*expectedMultiplier,"post-hop native contact consumes the same current local damage and geometry");freshContacts++;}
                    if(starts>=2 && ordinary.ai[2]==6){float expectedInput=contact(victim,true),actualInput=contact(victim,false);Require(!body.Intersects(victim) && actualInput==expectedInput && actualInput==ordinary.damage*expectedMultiplier,"second swing extended-only native contact reaches the same DamageVar input and real Hurt");extendedHits++;rightExtension|=crossed && ordinary.spriteDirection==1;}
                    var clock=observed.ai[2];var motion=observed.position;var velocity=observed.velocity;var stuckClock=observed.ai[3];Call(host,"AdvanceNpcObservation");Require(observed.ai[2]==clock && observed.ai[3]==stuckClock && observed.position==motion && observed.velocity==velocity,"same world step cannot advance the local attack, movement or physics tail twice");
                }
                Require(received==published+1 && (fresh?freshHop && freshContacts==2 && hitInputs==1:starts>=2 && extendedHits>0 && rightExtension),"fresh supported hop/actions/native contact or stable subsequent swings/new-side extended Hurt are proved, with every actual window23 delivered to both receivers");
                Console.WriteLine("NPC ARMED PARITY fresh="+fresh+" actions=46 exactGeometryVelocityAi3=True serverMode2=True receiverState10/Connected/Broadcast/ActiveSection=True naturalWindow23="+published+" npc23Delivered="+received+" localSwingEntries="+starts+" laterExtendedHurt="+extendedHits+" freshPostHopContacts="+freshContacts+" nativeHitInputs="+hitInputs+" duplicateWorldStepNoAdvance=True");
            }
            finally
            {
                measuringContact=false;patch.UnpatchAll(patch.Id);ActiveSections.SectionActivated-=ArmedSection;RemoteClient.NetSectionActivated-=ArmedClientSection;Main.npc=original;Main.netMode=mode;Main.dedServ=dedicated;Call(host,"ClearWorldSources");Netplay.Clients=clients;NetMessage.buffer=buffers;Main.maxSectionsX=sx;Main.maxSectionsY=sy;activeTime.SetValue(null,oldActive);for(int i=0;i<players.Length;i++){players[i].position=positions[i];players[i].velocity=velocities[i];}first.statLife=health;first.statLifeMax=max;first.statLifeMax2=max2;first.immune=immune;first.immuneTime=immuneTime;Array.Copy(cooldowns,first.hurtCooldowns,cooldowns.Length);restore();armedPackets.Clear();
            }
        }
    }
}
