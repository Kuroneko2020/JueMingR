using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using HarmonyLib;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Net.Sockets;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionCoreNetworkChecks
    {
        private static byte[] ReadUntil(NetworkStream stream,byte id)
        {
            stream.ReadTimeout=5000;
            for(int i=0;i<1000;i++)
            {
                int low=stream.ReadByte(),high=stream.ReadByte();if(low<0 || high<0)throw new EndOfStreamException();int size=low+(high<<8);var packet=new byte[size];packet[0]=(byte)low;packet[1]=(byte)high;
                for(int at=2;at<size;){int read=stream.Read(packet,at,size-at);if(read==0)throw new EndOfStreamException();at+=read;}
                if(packet[2]==id)return packet;
            }
            throw new InvalidDataException("fixture stream did not contain requested native packet "+id);
        }
        private static void InterestReentry(object host,TcpListener listener,Player first,Player second,bool firstEntry=false)
        {
            var human=Main.LocalPlayer;var positions=new[]{human.position,first.position,second.position};var oldNpcs=new[]{Main.npc[24],Main.npc[25]};var sourceNpcs=new NPC[2];var friend=Main.player[4];var profiles=Get(host,"profiles");
            int width=Main.maxTilesX,height=Main.maxTilesY,sx=Main.maxSectionsX,sy=Main.maxSectionsY,spawn=Main.spawnTileX;float right=Main.rightWorld,bottom=Main.bottomWorld;var tile=Main.tile;var sections=Main.sectionManager;var clients=Netplay.Clients;var buffers=NetMessage.buffer;
            var packets=new Harmony("JueMingR.Tests.CompanionInterestSnapshot");CompanionProfile profile=null;object actor=null;int snapshots=0;var bestiary=Main.BestiaryTracker;var pylons=Main.PylonSystem;
            var oldProjectiles=(Projectile[])Main.projectile.Clone();var initiallyActive=Main.projectile.Select(p=>p.active).ToArray();var fired=new List<Projectile>();
            try
            {
                Main.maxTilesX=600;Main.maxTilesY=150;Main.maxSectionsX=3;Main.maxSectionsY=1;Main.rightWorld=9600;Main.bottomWorld=2400;Main.tile=new Tile[600,150];for(int x=0;x<tile.GetLength(0);x++)for(int y=0;y<tile.GetLength(1);y++)Main.tile[x,y]=tile[x,y];for(int x=2;x<200;x++)for(int y=2;y<148;y++){if(Main.tile[x,y]==null)Main.tile[x,y]=new Tile();if(y==60){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}}
                Main.sectionManager=new WorldSections(3,1);Main.sectionManager.SetSectionLoaded(0,0);
                // Native server8's ordinary join tail needs these two normal
                // Main.Initialize owners, absent from the small combat fixture.
                Main.BestiaryTracker=new BestiaryUnlocksTracker();Main.PylonSystem=new TeleportPylonsSystem();
                // An ordinary remote friend keeps the native target stable.
                // Source selection considers owner/actors, not this friend.
                Main.player[4]=new Player{whoAmI=4,active=true,position=new Vector2(640,918)};
                Netplay.Clients=new RemoteClient[256];for(int i=0;i<256;i++)Netplay.Clients[i]=new RemoteClient{Id=i};NetMessage.buffer=new MessageBuffer[257];for(int i=0;i<257;i++)NetMessage.buffer[i]=new MessageBuffer{whoAmI=i};Netplay.Clients[3]=Program.CreateRemoteClient(3,10,Netplay.Connection.Socket);for(int x=0;x<3;x++)Netplay.Clients[3].TileSections[x,0]=true;NetMessage.buffer[3].broadcast=true;
                serverFrames=new Dictionary<int,List<byte[]>>();packets.Patch(AccessTools.Method(typeof(NetMessage),"SendPacket"),prefix:new HarmonyMethod(typeof(NativeCompanionCoreNetworkChecks),nameof(CaptureServer)){priority=Priority.First,before=new[]{"JueMingR.Tests.QuickItemOutlets"}});
                human.position=new Vector2(1000,918);first.position=new Vector2(5000,918);second.position=new Vector2(5200,918);Call(host,"ClearWorldSources");
                for(int slot=24;slot<=25;slot++){var n=(NPC)AccessTools.Method(typeof(NPC),"NewNPCInstanceInSlot").Invoke(null,new object[]{slot,(byte)100});sourceNpcs[slot-24]=n;n.SetDefaults(NPCID.Mimic);n.active=true;n.position=new Vector2(300+(slot-24)*30,960-n.height);n.UpdateNPC(slot);n.AddBuff(BuffID.Ichor,500);Require(n.target==4 && n.ai[0]==0 && n.velocity==Vector2.Zero && n.FindBuffIndex(BuffID.Ichor)>=0,"real dormant Mimic remains active/stationary with a native accepted buff and closer ordinary friend outside its wake rectangle");}
                if(firstEntry)Main.spawnTileX=175;
                string name=firstEntry?"兴趣首次":"兴趣返回";Require((bool)Call(host,"Create",name),"create one baseline profile for a real connection");PumpUntil(host,()=>{profile=((IEnumerable)Get(host,"Profiles")).Cast<CompanionProfile>().FirstOrDefault(p=>p.Name==name);return profile!=null && (bool)Call(profiles,"IsSaved",profile.Id);});
                Require((bool)Call(host,"Join",profile.Id),"one real Join establishes the actor, never used as a reentry repair");NativeQuickItemChecks.Until(()=>listener.Pending());
                using(var remote=listener.AcceptTcpClient())
                {
                    var stream=remote.GetStream();Write(stream,Frame(3,w=>w.Write((byte)3)));PumpUntil(host,()=>ReservedSlot(host,3)!=null);Write(stream,World(Main.ActiveWorldFileData.UniqueId));foreach(int slot in new[]{24,25}){Write(stream,Native(23,slot));Write(stream,Native(54,slot));}Write(stream,Frame(49));FinishReservedSpawn(host,stream,3);
                    PumpUntil(host,()=>{actor=Call(host,"OwnedSlot",3);return actor!=null && (bool)Get(actor,"Ready");});var role=(Player)Get(actor,"Body");role.position=new Vector2(firstEntry?2800:700,918);Call(host,"SetBehavior",profile.Id,new CompanionBehavior{Follow=false,AttackEnabled=false});
                    var observations=(NPC[])Get(host,"observedNpcs");var identities=new[]{observations[24],observations[25]};var connection=Get(actor,"Connection");var flow=Get(actor,"WorldSource");
                    Console.WriteLine("INTEREST INITIAL spawn="+Get(actor,"SpawnSent")+" ready="+Get(actor,"Ready")+" active="+observations[24].active+"/"+observations[25].active+" buff="+observations[24].FindBuffIndex(BuffID.Ichor)+" sourceBuff="+Main.npc[24].FindBuffIndex(BuffID.Ichor)+" body="+role.position);
                    if(firstEntry)
                    {
                        Require((bool)Get(actor,"Ready") && !observations[24].active && !observations[25].active && ((long[])Get(host,"observedRevisions"))[24]==0 && !(bool)Get(((IDictionary)Get(host,"npcSources"))[24],"NpcBaseline") && (bool)Get(((IDictionary)Get(host,"npcSources"))[24],"FilteredCandidate"),"first real distant23 is filtered: no NPC was decoded, no revision/baseline was fabricated, only its bounded real candidate remains");
                    }
                    else Require((bool)Get(actor,"SpawnSent") && (bool)Get(actor,"Ready") && observations[24].active && observations[25].active && observations[24].FindBuffIndex(BuffID.Ichor)>=0,"initial real 3/7/23/54/49/129 provides both current NPC baselines");
                    bool quietWindow=true;int quietSteps=0;
                    Action step=()=>
                    {
                        bool inside=Vector2.DistanceSquared(role.Center,identities[0].Center)<2200*2200;NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",0UL);human.Update(human.whoAmI);Main.player[4].Update(4);role.Update(role.whoAmI);
                        Require(role.position.X>=640 && Netplay.GetSectionX((int)role.Center.X/16)==0 && !role.outOfRange && inside==(Vector2.DistanceSquared(role.Center,identities[0].Center)<2200*2200),"real Player.Update retains the legal same-section inside/outside interest premise throughout departure/return");
                        serverFrames.Clear();bool dedicated=Main.dedServ;Main.netMode=2;Main.dedServ=true;try{foreach(var n in sourceNpcs)if(n.active)n.UpdateNPC(n.whoAmI);}finally{Main.netMode=1;Main.dedServ=dedicated;}
                        if(quietWindow){quietSteps++;Require(sourceNpcs.All(n=>n.active && n.ai[0]==0 && n.velocity==Vector2.Zero && n.target==4) && !serverFrames.Values.SelectMany(p=>p).Any(p=>p[2]==23),"actual original server NPC.Update keeps the same friend target and emits no ordinary23 while the actor leaves/returns");}
                        Call(host,"AdvanceNpcObservation");Call(host,"FlushSourceRequests");
                    };
                    if(!firstEntry)
                    {
                        first.position=role.position;role.position=new Vector2(2800,918);step();Require(observations[24].active && (long)Get(flow,"Sent")==0,"another teammate retaining interest avoids an unnecessary whole snapshot");first.position=new Vector2(5000,918);step();
                        Require(!observations[24].active && !observations[25].active && (bool)Get(((IDictionary)Get(host,"npcSources"))[24],"InterestRetired"),"same active actor's actual departure retires both known observations");
                    }
                    else{step();Require((long)Get(flow,"Sent")==0 && ((long[])Get(host,"observedRevisions"))[24]==0,"staying outside the first filtered region creates no polling snapshot or synthetic observation");}
                    Require(Netplay.GetSectionX((int)role.Center.X/16)==0 && Main.sectionManager.SectionLoaded(0,0),"departure remains in the same already-loaded section");role.position=new Vector2(700,918);step();
                    Console.WriteLine("INTEREST REENTRY actor="+role.position+" desiredSame="+ReferenceEquals(Call(host,"Observer",identities[0].Center),actor)+" sent="+Get(flow,"Sent")+" outstanding="+Get(flow,"Outstanding"));
                    Require(ReferenceEquals(Call(host,"Observer",identities[0].Center),actor) && (long)Get(flow,"Sent")==1 && (bool)Get(flow,"Outstanding"),"two reentering slots coalesce into one source barrier");
                    Action<byte[]> respond=request=>
                    {
                        serverFrames.Clear();var decoder=new MessageBuffer{whoAmI=3};Buffer.BlockCopy(request,2,decoder.readBuffer,0,request.Length-2);decoder.ResetReader();int type;
                        bool dedicated=Main.dedServ;Main.netMode=2;Main.dedServ=true;try{decoder.GetData(0,request.Length-2,out type);}catch{Console.WriteLine("INTEREST SERVER request="+request[2]+" sent="+String.Join(",",serverFrames.SelectMany(p=>p.Value).Select(p=>p[2]))+" bestiary="+(Main.BestiaryTracker!=null)+" pylons="+(Main.PylonSystem!=null));throw;}finally{Main.netMode=1;Main.dedServ=dedicated;}
                        foreach(var frame in serverFrames[3])Write(stream,frame);
                    };
                    Action snapshot=()=>
                    {
                        respond(ReadUntil(stream,154));PumpUntil(host,()=>(bool)Get(flow,"Snapshot"));var request=ReadUntil(stream,8);snapshots++;
                        respond(request);Require(serverFrames[3].Count(p=>p[2]==49)==1 && !serverFrames[3].Any(p=>p[2]==10),"actual native8 completes enumeration without retransmitting already-sent terrain");
                        PumpUntil(host,()=>!(bool)Get(flow,"Snapshot"));
                    };
                    snapshot();if(firstEntry){Require(((long[])Get(host,"observedRevisions"))[24]>0,"only the actual native snapshot response creates the first consumed NPC revision");identities=new[]{observations[24],observations[25]};}
                    Require(observations[24].active && observations[25].active && observations[24].FindBuffIndex(BuffID.Ichor)>=0 && ReferenceEquals(observations[24],identities[0]) && ReferenceEquals(Get(actor,"Connection"),connection) && ReferenceEquals(Call(host,"OwnedSlot",3),actor) && (bool)Get(actor,"Ready") && ReferenceEquals(Main.npc[24],sourceNpcs[0]) && ReferenceEquals(Main.npc[25],sourceNpcs[1]),"real post-barrier23/54/49 restores same actors/identities without Join/Ready or NPC respawn");
                    var humanNpc=Main.npc[24];Require(humanNpc.type==NPCID.Mimic && humanNpc.ai[0]==0,"baseline request never wakes or replaces the original human NPC");
                    // End the same reentry chain at the actual player consumer.
                    // No new Join, extraNPC23 or fabricated ammo deduction.
                    Require(role.itemAnimation==0 && role.itemTime==0,"the real idle return leaves the role's ordinary use cooldown ready");role.inventory[0]=new Item(ItemID.FlintlockPistol);role.inventory[54]=new Item(ItemID.MusketBall){stack=20};role.selectedItemState.Select(0);Call(host,"SetBehavior",profile.Id,new CompanionBehavior{Follow=false,AttackEnabled=true});step();
                    fired.AddRange(Main.projectile.Where(p=>p.active && !initiallyActive[p.whoAmI] && p.owner==role.whoAmI && p.type==ProjectileID.Bullet));
                    Require((ReferenceEquals(Get(actor,"Target"),identities[0]) || ReferenceEquals(Get(actor,"Target"),identities[1])) && (Vector2)Get(actor,"Aim")==((NPC)Get(actor,"Target")).Center+((NPC)Get(actor,"Target")).netOffset && role.inventory[54].stack==19 && fired.Count==1 && fired[0].velocity.X<0,"same restored Actor really selects its restored observation, basic-aims and reaches native ItemCheck/bullet birth with actual20->19 ammo payment");Call(host,"SetBehavior",profile.Id,new CompanionBehavior{Follow=false,AttackEnabled=false});
                    Console.WriteLine("NPC INTEREST restoredControlTarget=True basicAim=True nativeItemCheckBullet=True ammo20->19=True noExtra23=True");
                    for(int i=0;i<20;i++)step();Require((long)Get(flow,"Sent")==1,"stable restored interest has no repeated per-frame snapshot");
                    role.position=new Vector2(2800,918);step();Require(!observations[24].active,"second real departure re-arms the interest edge");quietWindow=false;Main.npc[24].position=new Vector2(8000,918);Main.npc[25].active=false;role.position=new Vector2(700,918);step();snapshot();
                    Require(!observations[24].active && !observations[25].active,"native snapshot filters the moved-out NPC and confirms the truly inactive slot absent");
                    for(int i=0;i<50;i++)step();Require((long)Get(flow,"Sent")==2 && snapshots==2 && !(bool)Get(flow,"Outstanding") && !(bool)Get(flow,"Requested") && !(bool)Get(flow,"Snapshot"),"completed filtered/absent enumeration terminates without every-frame full refresh");
                    Console.WriteLine("NPC INTEREST firstFilteredEntry="+firstEntry+" sameActor=True sameLoadedSection=True coalescedSlots=2 snapshots="+snapshots+" nativeQuietSteps="+quietSteps+" natural23=0 targetFriend=4 steady50AdditionalRequests=0 humanOriginal=True");
                }
            }
            finally
            {
                Main.netMode=1;packets.UnpatchAll(packets.Id);try{if(profile!=null){Call(host,"Recall",profile.Id);PumpUntil(host,()=>!((IDictionary)Get(host,"actors")).Contains(profile.Id));}}finally{foreach(var shot in fired){shot.active=false;Main.projectile[shot.whoAmI]=oldProjectiles[shot.whoAmI];}serverFrames=null;Main.npc[24]=oldNpcs[0];Main.npc[25]=oldNpcs[1];Call(host,"ClearWorldSources");Main.player[4]=friend;human.position=positions[0];first.position=positions[1];second.position=positions[2];Main.tile=tile;Main.maxTilesX=width;Main.maxTilesY=height;Main.maxSectionsX=sx;Main.maxSectionsY=sy;Main.spawnTileX=spawn;Main.rightWorld=right;Main.bottomWorld=bottom;Main.sectionManager=sections;Netplay.Clients=clients;NetMessage.buffer=buffers;Main.BestiaryTracker=bestiary;Main.PylonSystem=pylons;}
            }
        }
    }
}
