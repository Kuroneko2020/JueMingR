using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.DataStructures;
using Terraria.Net.Sockets;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionCoreNetworkChecks
    {
        private static void RootHealth(object host,Player first,Player second,object a,object b,object ca,object cb,List<byte[]> fa,List<byte[]> fb,int start=24,bool wall=false)
        {
            var original=Main.npc;var clients=Netplay.Clients;var buffers=NetMessage.buffer;var bestiary=Main.BestiaryTracker;var pylons=Main.PylonSystem;bool dedicated=Main.dedServ;int mode=Main.netMode,sx=Main.maxSectionsX,sy=Main.maxSectionsY;
            var players=new[]{Main.LocalPlayer,first,second};var positions=players.Select(p=>p.position).ToArray();var globals=(IList)AccessTools.Field(typeof(NPC),"pendingDamages").GetValue(null);var pending=globals.Cast<object>().ToArray();
            var actors=new[]{a,b};var oldViews=actors.Select(actor=>((IDictionary)Get(actor,"NpcViews")).Cast<DictionaryEntry>().ToArray()).ToArray();var oldQueues=actors.Select(actor=>((IEnumerable)Get(actor,"PendingStrikes")).Cast<object>().ToArray()).ToArray();
            var oldProjectiles=(Projectile[])Main.projectile.Clone();Projectile probingArrow=null;
            var packets=new Harmony("JueMingR.Tests.CompanionLinkedHealth");var hooks=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionHooks");
            var apply=typeof(Player).GetMethod("ApplyDamageToNPC",new[]{typeof(NPC),typeof(int),typeof(float),typeof(int),typeof(bool),typeof(PlayerNPCHitSource)});
            Func<object,IDisposable> enter=actor=>(IDisposable)hooks.GetMethod("Enter",Flags).Invoke(null,new object[]{actor,((Player)Get(actor,"Body")).Center,false});
            try
            {
                Call(host,"ClearWorldSources");globals.Clear();foreach(var actor in actors){((IDictionary)Get(actor,"NpcViews")).Clear();Call(Get(actor,"PendingStrikes"),"Clear");}Main.npc=Enumerable.Range(0,Main.maxNPCs).Select(i=>new NPC{whoAmI=i,active=false}).ToArray();
                Main.maxSectionsX=Main.maxSectionsY=1;Netplay.Clients=new RemoteClient[256];for(int i=0;i<256;i++)Netplay.Clients[i]=new RemoteClient{Id=i,Socket=new TcpSocket()};NetMessage.buffer=new MessageBuffer[257];for(int i=0;i<257;i++)NetMessage.buffer[i]=new MessageBuffer{whoAmI=i};
                foreach(int id in new[]{first.whoAmI,second.whoAmI}){Netplay.Clients[id].State=10;Netplay.Clients[id].Socket=Netplay.Connection.Socket;Netplay.Clients[id].TileSections[0,0]=true;NetMessage.buffer[id].broadcast=true;}
                Main.BestiaryTracker=new BestiaryUnlocksTracker();Main.PylonSystem=new TeleportPylonsSystem();serverFrames=new Dictionary<int,List<byte[]>>();packets.Patch(AccessTools.Method(typeof(NetMessage),"SendPacket"),prefix:new HarmonyMethod(typeof(NativeCompanionCoreNetworkChecks),nameof(CaptureServer)){priority=Priority.First,before=new[]{"JueMingR.Tests.QuickItemOutlets"}});
                players[0].position=new Vector2(900,918);first.position=new Vector2(1040,918);second.position=new Vector2(1080,918);Main.netMode=2;Main.dedServ=true;
                int rootSlot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1100,920,wall?113:NPCID.GiantWormHead,start);
                if(wall)
                {
                    Require(Main.wofNPCIndex==rootSlot,"actual native mouth OnSpawn establishes the SERVER's real root index");
                    for(int eye=0;eye<2;eye++){int slot=NPC.NewNPC(Main.npc[rootSlot].GetSpawnSourceForNPCFromNPCAI(),1100,880+eye*40,114,rootSlot+1,eye==0?1:-1);Main.npc[slot].AI();}
                }
                else for(int slot=0;slot<Main.maxNPCs;slot++)if(Main.npc[slot].active)Main.npc[slot].UpdateNPC(slot);
                var root=Main.npc[rootSlot];var segments=Main.npc.Where(n=>n.active && (wall?n.type==114:n.type==NPCID.GiantWormBody || n.type==NPCID.GiantWormTail) && n.realLife==rootSlot).ToArray();
                Require(rootSlot==start && (wall?segments.Length==2:root.realLife==rootSlot && segments.Length>=3 && segments.All(n=>n.ai[3]==rootSlot && Main.npc[(int)n.ai[1]].ai[0]==n.whoAmI)),"actual server native producer builds a genuine linked health family");
                int dummy=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1000,920,NPCID.Zombie,50);Main.netMode=1;Main.dedServ=dedicated;
                apply.Invoke(players[0],new object[]{Main.npc[dummy],1,0f,1,false,new PlayerNPCHitSource(0,0,"native human unrelated pending")});Require(globals.Count==1,"real human hit establishes an unrelated native pending object");var humanPending=globals[0];int humanHealth=Main.npc[dummy].life;
                Func<int,byte[]> state=slot=>{Main.netMode=2;Main.dedServ=true;serverFrames.Clear();try{NetMessage.SendData(23,first.whoAmI,-1,null,slot);return serverFrames[first.whoAmI].Single(p=>p[2]==23);}finally{Main.netMode=1;Main.dedServ=dedicated;}};
                fa.Clear();fb.Clear();Queue(ca,state(segments[0].whoAmI));Call(host,"PumpConnection",a);var observed=(NPC[])Get(host,"observedNpcs");
                Require(!(bool)Call(host,"ObservedNpcCombatReady",observed[segments[0].whoAmI]) && observed[segments[0].whoAmI].dontTakeDamage,"a fresh body cannot invent its missing root or a private body-health balance");Call(host,"FlushSourceRequests");
                Func<byte[],int,List<byte[]>> serve=(request,client)=>{Main.netMode=2;Main.dedServ=true;serverFrames.Clear();try{var decoder=new MessageBuffer{whoAmI=client};Buffer.BlockCopy(request,2,decoder.readBuffer,0,request.Length-2);decoder.ResetReader();int id;decoder.GetData(0,request.Length-2,out id);return serverFrames.TryGetValue(client,out var frames)?frames.ToList():new List<byte[]>();}finally{Main.netMode=1;Main.dedServ=dedicated;}};
                foreach(var frame in serve(fa.Last(p=>p[2]==154),first.whoAmI))Queue(ca,frame);Call(host,"PumpConnection",a);
                var snapshot=serve(fa.Last(p=>p[2]==8),first.whoAmI);Require(snapshot.Count(p=>p[2]==49)==1 && snapshot.Count(p=>p[2]==23)>=segments.Length+1,"actual native8 enumerates the complete live family and emits its own49");
                // The preexisting SP-role network adapter has no MP startup7.
                // Feed the real NPC/buff/end frames; TCP/full Ready handshake
                // is independently exercised by the same-actor interest leaf.
                foreach(var frame in snapshot.Where(p=>p[2]==23 || p[2]==54 || p[2]==49))Queue(ca,frame);Call(host,"PumpConnection",a);NativeQuickItemChecks.BeginWorldStep();Call(host,"AdvanceNpcObservation");
                Require(segments.All(n=>(bool)Call(host,"ObservedNpcCombatReady",observed[n.whoAmI]) && observed[n.whoAmI].realLife==rootSlot) && fa.Count(p=>p[2]==154)==1 && fa.Count(p=>p[2]==8)==1,"one completed coherent native snapshot qualifies all segment identities and the common root");
                int health=root.life;using(enter(a)){foreach(var n in segments.Take(2))apply.Invoke(first,new object[]{observed[n.whoAmI],1,0f,1,false,new PlayerNPCHitSource(0,0,"native linked segment")});}
                var own=(IEnumerable)Get(a,"PendingStrikes");var ownPending=own.Cast<object>().ToArray();var views=(IDictionary)Get(a,"NpcViews");var rootView=(NPC)Get(views[rootSlot],"Body");
                Require(ownPending.Length==2 && ownPending.All(p=>(short)Get(p,"index")==rootSlot && (byte)Get(p,"generation")==root.generation) && rootView.life==health-2 && root.life==health,"two actual segment strikes debit one private root and enqueue that actual root identity without mutating the source");
                using(enter(b))apply.Invoke(second,new object[]{observed[segments[1].whoAmI],1,0f,1,false,new PlayerNPCHitSource(0,0,"other actor linked segment")});
                Require(((IEnumerable)Get(b,"PendingStrikes")).Cast<object>().Count()==1 && ((NPC)Get(((IDictionary)Get(b,"NpcViews"))[rootSlot],"Body")).life==health-1 && globals.Count==1 && ReferenceEquals(globals[0],humanPending) && Main.npc[dummy].life==humanHealth,"another actor has its own common-root balance/queue and the human pending object remains intact");
                foreach(var hit in fa.Where(p=>p[2]==28).Take(2).ToArray())foreach(var reply in serve(hit,first.whoAmI).Where(p=>p[2]==162))Queue(ca,reply);Call(host,"PumpConnection",a);
                Require(root.life==health-2 && ((IEnumerable)Get(a,"PendingStrikes")).Cast<object>().Count()==0 && ((IEnumerable)Get(b,"PendingStrikes")).Cast<object>().Count()==1,"real native server28 decoder debits the actual common root and real162 releases only the requesting actor");
                using(enter(a))apply.Invoke(first,new object[]{observed[segments.Last().whoAmI],root.lifeMax*10,0f,1,false,new PlayerNPCHitSource(0,0,"native private root death")});
                Require((bool)Call(host,"PredictionDead",a,observed[segments.Last().whoAmI]) && !(bool)Call(host,"PredictionDead",b,observed[segments.Last().whoAmI]) && root.life==health-2,"root prediction death excludes every sibling for its owner only, without claiming server death");
                if(wall){Console.WriteLine("NPC ROOT WOF nativeOnSpawn/AI28=True sourceGlobal="+Main.wofNPCIndex+" root="+rootSlot+" eyes=2 native8Snapshot=True actualPrivateRoot/Server28=True humanPendingPreserved=True ownDeathOnly=True");return;}
                Main.netMode=2;Main.dedServ=true;var next=(NPC)AccessTools.Method(typeof(NPC),"NewNPCInstanceInSlot").Invoke(null,new object[]{rootSlot,(byte)(root.generation+1)});next.SetDefaults(NPCID.GiantWormHead);next.active=true;next.position=root.position;next.target=root.target;Main.netMode=1;Main.dedServ=dedicated;
                Require(next.generation!=root.generation,"native head slot recreation actually establishes a different generation");Queue(ca,state(rootSlot));Call(host,"PumpConnection",a);
                var guarded=segments.Select(n=>observed[n.whoAmI]).First(n=>!new Rectangle((int)n.Center.X-5,(int)n.Center.Y-5,10,10).Intersects(observed[rootSlot].Hitbox) && !new Rectangle((int)n.Center.X-5,(int)n.Center.Y-5,10,10).Intersects(observed[dummy].Hitbox));
                Require(guarded.active && !guarded.dontTakeDamage && !(bool)Call(host,"ObservedNpcCombatReady",guarded),"new head generation invalidates the old binding immediately, before the next world-step flag preparation");
                int hits=fa.Count(p=>p[2]==28),queued=((IEnumerable)Get(a,"PendingStrikes")).Cast<object>().Count(),nextHealth=next.life;
                using(enter(a))
                {
                    apply.Invoke(first,new object[]{guarded,5,0f,1,false,new PlayerNPCHitSource(0,0,"stale root direct gate")});
                    Call(first,"ProcessHitAgainstNPC",new Item(ItemID.CopperShortsword),guarded.Hitbox,5,0f,guarded.whoAmI);
                    // This is the actual projectile IMPACT primitive. Native
                    // ItemCheck/payment is unchanged and not signed by this
                    // fixture's direct native projectile birth.
                    probingArrow=Main.projectile[Projectile.NewProjectile(new EntitySource_Parent(first),guarded.Center,Vector2.Zero,ProjectileID.WoodenArrowFriendly,5,0,first.whoAmI)];
                    Require(ReferenceEquals(Call(host,"ProjectileActor",probingArrow),a) && probingArrow.Hitbox.Intersects(guarded.Hitbox),"normal native factory tags the owning actor and an actual colliding arrow reaches its Damage consumer");probingArrow.Damage();
                }
                Require(fa.Count(p=>p[2]==28)==hits && ((IEnumerable)Get(a,"PendingStrikes")).Cast<object>().Count()==queued && next.life==nextHealth && ReferenceEquals(Main.npc[rootSlot],next),"native direct/melee/projectile consumers cannot debit or emit28 for an old body bound to a reused head generation");
                Console.WriteLine("NPC ROOT nativeSegments="+segments.Length+" root="+rootSlot+" pendingIndexGen=True snapshots=1 actorA="+(health-2)+" actorB="+(health-1)+" sourceServer="+root.life+" humanPendingPreserved=True siblingPredictionDeadOwnOnly=True");
                Console.WriteLine("NPC ROOT generation="+root.generation+"->"+next.generation+" sameTickDirectMeleeProjectileBlocked=True newRootLife="+next.life);
            }
            finally
            {
                Main.netMode=mode;Main.dedServ=dedicated;packets.UnpatchAll(packets.Id);Main.npc=original;Call(host,"ClearWorldSources");Netplay.Clients=clients;NetMessage.buffer=buffers;Main.BestiaryTracker=bestiary;Main.PylonSystem=pylons;Main.maxSectionsX=sx;Main.maxSectionsY=sy;globals.Clear();foreach(var value in pending)globals.Add(value);for(int i=0;i<players.Length;i++)players[i].position=positions[i];serverFrames=null;
                if(probingArrow!=null){probingArrow.active=false;Main.projectile[probingArrow.whoAmI]=oldProjectiles[probingArrow.whoAmI];}
                for(int i=0;i<actors.Length;i++){var actor=actors[i];var views=(IDictionary)Get(actor,"NpcViews");views.Clear();foreach(var entry in oldViews[i])views.Add(entry.Key,entry.Value);var queue=Get(actor,"PendingStrikes");Call(queue,"Clear");foreach(var value in oldQueues[i])Call(queue,"Enqueue",value);Set(actor,"Target",null);Set(actor,"HasAim",false);}
            }
        }
    }
}
