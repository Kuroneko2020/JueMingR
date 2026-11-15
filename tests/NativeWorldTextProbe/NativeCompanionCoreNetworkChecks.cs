using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Net;
using System.Net.Sockets;
using HarmonyLib;
using JueMingR.Platform.Companions;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.Net.Sockets;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionCoreNetworkChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static List<byte[]> primary;
        private static Guid failedSave;
        private static Player failedCapture;
        private static bool SaveFailure(CompanionProfile profile){if(profile.Id==failedSave)throw new IOException("fixture terminal archive write failure");return true;}
        private static bool CaptureFailure(Player body){if(ReferenceEquals(body,failedCapture))throw new InvalidOperationException("fixture terminal native capture failure");return true;}
        [ThreadStatic] private static int selectionDepth,solverDepth;
        private static int selectionRays,selectionReads;
        private static void SelectionStart(){selectionDepth++;}
        private static void SelectionEnd(){selectionDepth--;}
        private static void SolverStart(){solverDepth++;}
        private static void SolverEnd(){solverDepth--;}
        private static void SelectionRay(){if(selectionDepth>0 && solverDepth==0)selectionRays++;}
        private static void SelectionRead(){if(selectionDepth>0 && solverDepth==0)selectionReads++;}
        private static bool Capture(byte[] data){primary.Add(data.Take(BitConverter.ToUInt16(data,0)).ToArray());return false;}
        private static byte[] Frame(byte id,Action<BinaryWriter> body=null)
        {using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){w.Write((ushort)0);w.Write(id);body?.Invoke(w);w.Flush();var data=s.ToArray();data[0]=(byte)data.Length;data[1]=(byte)(data.Length>>8);return data;}}
        private static void Write(NetworkStream stream,byte[] frame){stream.Write(frame,0,frame.Length);stream.Flush();}
        private static void Apply(byte[] frame)
        {var b=new MessageBuffer{whoAmI=256};Buffer.BlockCopy(frame,2,b.readBuffer,0,frame.Length-2);b.ResetReader();int type;b.GetData(0,frame.Length-2,out type);}
        private static byte[] Native(byte id,int slot)
        {NetMessage.SendData(id,-1,-1,null,slot);return primary.Last(p=>p[2]==id);}
        private static void Queue(object connection,byte[] frame){Call(connection,"ReceiveRelay",frame);}
        private static int Wood(Player p){return p.inventory.Where(i=>i.type==ItemID.Wood).Sum(i=>i.stack);}
        internal static void Run(object host,Player first,Guid other)
        {
            Require((bool)Call(host,"Join",other),"second profile joins native network consumer fixture");
            var second=(Player)Call(host,"GetBody",other);var a=Call(host,"Owned",first);var b=Call(host,"Owned",second);
            var fa=new List<byte[]>();var fb=new List<byte[]>();primary=new List<byte[]>();
            var type=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionConnection");
            Func<List<byte[]>,object> connection=frames=>Activator.CreateInstance(type,Flags,null,new object[]{new Action<byte[]>(p=>frames.Add(p)),new Action(()=>{})},null);
            var ca=connection(fa);var cb=connection(fb);Set(a,"Connection",ca);Set(b,"Connection",cb);
            int mode=Main.netMode;var socket=Netplay.Connection.Socket;var buffer=NetMessage.buffer[256];var sections=Main.sectionManager;
            var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var client=new TcpClient();client.Connect(IPAddress.Loopback,((IPEndPoint)listener.LocalEndpoint).Port);var peer=listener.AcceptTcpClient();Netplay.Connection.Socket=new TcpSocket(client);
            var patch=new Harmony("JueMingR.Tests.CompanionCoreNetwork");patch.Patch(AccessTools.Method(typeof(NetMessage),"SendPacketToServer"),prefix:new HarmonyMethod(typeof(NativeCompanionCoreNetworkChecks),nameof(Capture)));
            patch.Patch(AccessTools.Method(host.GetType(),"PrepareUse"),prefix:new HarmonyMethod(typeof(NativeCompanionCoreNetworkChecks),nameof(SelectionStart)),finalizer:new HarmonyMethod(typeof(NativeCompanionCoreNetworkChecks),nameof(SelectionEnd)));
            patch.Patch(AccessTools.Method(host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackAim"),"ActorPoint"),prefix:new HarmonyMethod(typeof(NativeCompanionCoreNetworkChecks),nameof(SolverStart)),finalizer:new HarmonyMethod(typeof(NativeCompanionCoreNetworkChecks),nameof(SolverEnd)));
            patch.Patch(AccessTools.Method(typeof(Collision),"CanHitLine",new[]{typeof(Vector2),typeof(int),typeof(int),typeof(Vector2),typeof(int),typeof(int)}),prefix:new HarmonyMethod(typeof(NativeCompanionCoreNetworkChecks),nameof(SelectionRay)));
            patch.Patch(AccessTools.Method(host.GetType(),"FreshNpcObservation"),prefix:new HarmonyMethod(typeof(NativeCompanionCoreNetworkChecks),nameof(SelectionRead)));
            selectionRays=selectionReads=0;
            var failures=new List<Exception>();
            Action<string,Action> leaf=(name,action)=>{try{action();Console.WriteLine("PASS core network leaf "+name);}catch(Exception e){Console.Error.WriteLine("FAIL core network leaf "+name+": "+e);failures.Add(e);}};
            try
            {
                NativeCompanionCoreLifecycleChecks.PrepareHuman();
                Main.netMode=1;NetMessage.buffer[256]=new MessageBuffer{whoAmI=256};
                Main.sectionManager=new WorldSections((Main.maxTilesX+199)/200,(Main.maxTilesY+149)/150);Main.sectionManager.SetSectionLoaded(0,0);
                foreach(var n in Main.npc)n.active=false;
                leaf("native buff luck successor stream",()=>
                {
                    fa.Clear();fb.Clear();primary.Clear();
                    var hooks=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionHooks");
                    // AddBuff outside an MP role sends55 to a remote player;
                    // native self-use owns this scope before it applies the buff.
                    using((IDisposable)hooks.GetMethod("Enter",Flags).Invoke(null,new object[]{a,first.Center,false}))first.AddBuff(BuffID.Lucky,14400);
                    NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",0UL);first.Update(first.whoAmI);second.Update(second.whoAmI);
                    Require(fa.Any(p=>p[2]==134 && p[3]==first.whoAmI) && !fb.Any(p=>p[2]==134 && p[3]==first.whoAmI) && !primary.Any(p=>p[2]==134 && p[3]==first.whoAmI) && !(bool)Get(first,"luckNeedsSync"),"native UpdateBuffs -> UpdateLuck consumes own dirty flag and sends134 only to own stream");
                });
                leaf("initial baseline and terminal failures",()=>BaselineAndFailures(host,listener));
                leaf("prediction real receive/player phases and two-role fairness",()=>Prediction(host,first,second,a,b,ca,cb));
                leaf("prism actual Update sync tail and map/flail",()=>Continuous(host,first,second,a,b,fa,fb));
                leaf("primary one-shot projectile update",()=>PrimaryProjectile(host,a,b,ca,cb,first,second));
                leaf("personal90 native pickup and save",()=>Personal(host,first,second,a,b,ca,cb,fa,fb));
            }
            finally
            {
                Main.netMode=mode;Main.sectionManager=sections;NetMessage.buffer[256]=buffer;Netplay.Connection.Socket=socket;
                patch.UnpatchAll(patch.Id);Call(host,"Recall",other);client.Close();peer.Close();listener.Stop();
            }
            if(failures.Count!=0)throw failures[0];
        }
        private static void PumpUntil(object host,Func<bool> complete)
        {NativeQuickItemChecks.Until(()=>{Call(host,"Update",0UL);return complete();});}
        private static byte[] World(Guid world)
        {return Frame(7,w=>{w.Write(0);w.Write((byte)0);w.Write((byte)0);for(int i=0;i<6;i++)w.Write((short)0);w.Write(0);w.Write("isolated native fixture");w.Write((byte)Main.GameMode);w.Write(world.ToByteArray());});}
        private static void BaselineAndFailures(object host,TcpListener listener)
        {
            var profiles=Get(host,"profiles");Require((bool)Call(host,"Create","网络基线"),"create real baseline profile");CompanionProfile profile=null;
            PumpUntil(host,()=>{profile=((IEnumerable)Get(host,"Profiles")).Cast<CompanionProfile>().FirstOrDefault(p=>p.Name=="网络基线");return profile!=null && (bool)Call(profiles,"IsSaved",profile.Id);});
            var humanNpc=Main.npc[17];humanNpc.SetDefaults(NPCID.Zombie);humanNpc.active=true;humanNpc.position=new Vector2(Main.spawnTileX*16,Main.spawnTileY*16);humanNpc.AddBuff(BuffID.Poisoned,321);var state=Native(23,17);var buffs=Native(54,17);int originalLife=humanNpc.life;
            try{
            Require((bool)Call(host,"Join",profile.Id),"real MP connection starts native handshake");NativeQuickItemChecks.Until(()=>listener.Pending());using(var remote=listener.AcceptTcpClient())
            {
                var stream=remote.GetStream();Write(stream,Frame(3,w=>w.Write((byte)3)));PumpUntil(host,()=>Call(host,"OwnedSlot",3)!=null);
                Write(stream,World(Main.ActiveWorldFileData.UniqueId));
                // Each original state appears exactly once, before 49/129.
                Write(stream,state);Write(stream,buffs);Write(stream,Frame(49));Write(stream,Frame(129));
                PumpUntil(host,()=>{var readyRole=Call(host,"OwnedSlot",3);return readyRole!=null && (bool)Get(readyRole,"Ready");});
                var observed=(NPC[])Get(host,"observedNpcs");Require(observed[17].active && observed[17].FindBuffIndex(BuffID.Poisoned)>=0 && ReferenceEquals(Main.npc[17],humanNpc) && humanNpc.life==originalLife,"initial 23/54 survives real 3/7/23/54/49/129 order without replacement human table");
                var actor=Call(host,"OwnedSlot",3);var role=(Player)Get(actor,"Body");var flow=Get(actor,"Connection");
                var originalDrop=Main.item[398];Main.item[398]=new WorldItem(new Item(ItemID.Wood)){whoAmI=398,stack=17,position=role.position,playerIndexTheItemIsReservedFor=role.whoAmI};
                try{Call(role,"GrabItems",role.whoAmI);Require(Wood(role)==17,"live joined actor obtains latest resource via native pickup before terminal failure");}
                finally{Main.item[398]=originalDrop;}
                var saveHook=new Harmony("JueMingR.Tests.CompanionTerminalArchive");var archive=Get(Get(profiles,"manager"),"archive");failedSave=profile.Id;
                saveHook.Patch(AccessTools.Method(archive.GetType(),"Save"),prefix:new HarmonyMethod(typeof(NativeCompanionCoreNetworkChecks),nameof(SaveFailure)));
                try
                {
                    Write(stream,Frame(2,w=>NetworkText.FromLiteral("fixture runtime kicked").Serialize(w)));
                    PumpUntil(host,()=>(bool)Call(profiles,"SaveFailed",profile.Id));
                    Require((bool)Get(actor,"Closing") && GetOptional(actor,"Connection")==null && Call(host,"OwnedSlot",3)==null && (bool)Get(flow,"stopped") && Wood((Player)Call(host,"GetBody",profile.Id))==17 && ((string)Call(host,"Status",profile.Id)).Contains("fixture runtime kicked"),"terminal flow closes while its only latest resource snapshot and cause survive archive failure");
                    var latest=Call(profiles,"Find",profile.Id);object[] reservation={latest,0L};Require(!(bool)profiles.GetType().GetMethod("TryReserve",Flags).Invoke(profiles,reservation),"failed terminal save cannot create duplicate activation");
                }
                finally{failedSave=Guid.Empty;saveHook.UnpatchAll(saveHook.Id);}
                Require((bool)Call(host,"RetrySave",profile.Id),"same latest failed snapshot has real retry outlet");
                PumpUntil(host,()=>!((IDictionary)Get(host,"actors")).Contains(profile.Id));
                Require(((string)Call(host,"Status",profile.Id)).Contains("fixture runtime kicked") && Wood((Player)Call(profiles,"LoadBody",Call(profiles,"Find",profile.Id)))==17,"archive retry/release preserves terminal cause and latest native resource payload");
            }
            CaptureRetry(host,listener,profile.Id);
            foreach(bool wrongWorld in new[]{false,true})
            {
                Require((bool)Call(host,"Join",profile.Id),"failed role can begin a fresh activation");NativeQuickItemChecks.Until(()=>listener.Pending());using(var remote=listener.AcceptTcpClient())
                {
                    var stream=remote.GetStream();Write(stream,Frame(3,w=>w.Write((byte)3)));PumpUntil(host,()=>Call(host,"OwnedSlot",3)!=null);
                    if(wrongWorld)Write(stream,World(Guid.NewGuid()));else Write(stream,Frame(2,w=>NetworkText.FromLiteral("fixture password rejected").Serialize(w)));
                    PumpUntil(host,()=>!((IDictionary)Get(host,"actors")).Contains(profile.Id));string status=(string)Call(host,"Status",profile.Id);
                    Require(status.Contains(wrongWorld?"世界与当前世界不一致":"fixture password rejected") && Call(host,"OwnedSlot",3)==null,"failure reason persists after socket/save/slot/activation release: "+status);
                    Require(remote.Client.Poll(100000,SelectMode.SelectRead),"terminal flow closes instead of infinite reconnect");
                }
            }
            }finally{humanNpc.active=false;var observations=(NPC[])GetOptional(host,"observedNpcs");if(observations!=null)observations[17].active=false;}
        }
        private static void CaptureRetry(object host,TcpListener listener,Guid id)
        {
            Require((bool)Call(host,"Join",id),"native saved profile joins again for capture producer failure");NativeQuickItemChecks.Until(()=>listener.Pending());
            using(var remote=listener.AcceptTcpClient())
            {
                var stream=remote.GetStream();Write(stream,Frame(3,w=>w.Write((byte)3)));PumpUntil(host,()=>Call(host,"OwnedSlot",3)!=null);
                Write(stream,World(Main.ActiveWorldFileData.UniqueId));Write(stream,Native(23,17));Write(stream,Native(54,17));Write(stream,Frame(49));Write(stream,Frame(129));
                PumpUntil(host,()=>{var ready=Call(host,"OwnedSlot",3);return ready!=null && (bool)Get(ready,"Ready");});
                var actor=Call(host,"OwnedSlot",3);var body=(Player)Get(actor,"Body");var profiles=Get(host,"profiles");
                Require(Wood(body)==17,"new true Join reads previously accepted native snapshot");
                var old=Main.item[398];Main.item[398]=new WorldItem(new Item(ItemID.Wood)){whoAmI=398,stack=3,position=body.position,playerIndexTheItemIsReservedFor=3};
                try{Call(body,"GrabItems",3);Require(Wood(body)==20,"native pickup makes a unique newer live state before failed capture");}finally{Main.item[398]=old;}
                // This case requires server termination to precede failed
                // close-capture. Let transport receive that one real frame
                // before Update can instead run the pending dirty autosave.
                var flow=Get(actor,"Connection");Write(stream,Frame(2,w=>NetworkText.FromLiteral("fixture capture kicked").Serialize(w)));
                NativeQuickItemChecks.Until(()=>{lock(Get(flow,"gate"))return ((IEnumerable)Get(flow,"incoming")).Cast<byte[]>().Any(p=>p[0]==2);});
                var patch=new Harmony("JueMingR.Tests.CompanionTerminalCapture");failedCapture=body;patch.Patch(AccessTools.Method(profiles.GetType(),"Capture"),prefix:new HarmonyMethod(typeof(NativeCompanionCoreNetworkChecks),nameof(CaptureFailure)));
                try
                {
                    PumpUntil(host,()=>(bool)Get(actor,"Closing"));Call(host,"Update",0UL);
                    Console.WriteLine("CAPTURE CLOSE wood="+Wood(body)+" cause="+GetOptional(actor,"TerminalFailure")+" status="+Call(host,"Status",id));
                    Require((bool)Call(profiles,"IsSaved",id) && !(bool)Get(actor,"CloseCaptured") && (bool)Call(host,"SaveFailed",id) && ((IDictionary)Get(host,"actors")).Contains(id) && GetOptional(actor,"Connection")==null && Call(host,"OwnedSlot",3)==null && Wood((Player)Call(host,"GetBody",id))==20,"old saved revision cannot release unique newer body when capture itself failed; retry is visible");
                }
                finally{failedCapture=null;patch.UnpatchAll(patch.Id);}
                Require((bool)Call(host,"RetrySave",id),"native capture retry reports accepted close even with retained connection failure cause");
                PumpUntil(host,()=>!((IDictionary)Get(host,"actors")).Contains(id));
                int savedWood=Wood((Player)Call(profiles,"LoadBody",Call(profiles,"Find",id)));string finalStatus=(string)Call(host,"Status",id);
                Console.WriteLine("CAPTURE RETRY savedWood="+savedWood+" status="+finalStatus);
                Require(savedWood==20 && finalStatus.Contains("fixture capture kicked"),"capture retry durably accepts latest state and preserves final failure explanation");
            }
        }
        private static void Prediction(object host,Player first,Player second,object a,object b,object ca,object cb)
        {
            first.position=new Vector2(800,900);second.position=new Vector2(1200,900);Main.LocalPlayer.position=new Vector2(1000,900);
            foreach(var p in new[]{first,second}){p.inventory[0].SetDefaults(ItemID.FlintlockPistol);p.inventory[54].SetDefaults(ItemID.MusketBall);p.inventory[54].stack=100;p.selectedItemState.Select(0);p.itemAnimation=p.itemTime=0;Require(!p.HeldItem.channel && !p.HeldItem.autoReuse,"native fixed firearm supplies real semi-auto release edges");}
            foreach(var actor in new[]{a,b})Call(host,"SetBehavior",((CompanionProfile)Get(actor,"Profile")).Id,new CompanionBehavior{Follow=false,AttackEnabled=true,PreferOwner=false});
            var n=Main.npc[18];n.SetDefaults(NPCID.DemonEye);n.active=true;n.position=first.position+new Vector2(120,-40);n.velocity=new Vector2(3,0);n.target=Main.myPlayer;n.noGravity=n.noTileCollide=true;n.life=n.lifeMax=10000;
            var m=Main.npc[19];m.SetDefaults(NPCID.DemonEye);m.active=true;m.position=second.position+new Vector2(120,-40);m.velocity=new Vector2(3,0);m.target=Main.myPlayer;m.noGravity=m.noTileCollide=true;m.life=m.lifeMax=10000;
            var existing=Main.projectile.Where(p=>p.active).Select(p=>(int)p.key).ToArray();
            try{
            long lastA=0,lastB=0;bool leadA=false,leadB=false,releaseCapture=false;
            for(int step=0;step<46;step++)
            {
                uint received=Main.GameUpdateCount;
                if(step%2==0)Apply(Native(23,18));
                long attemptA=(long)Get(a,"LastAimAttempt"),attemptB=(long)Get(b,"LastAimAttempt");
                int readsBefore=selectionReads;NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",0UL);
                Main.LocalPlayer.Update(Main.myPlayer);first.Update(first.whoAmI);second.Update(second.whoAmI);
                Require(selectionReads-readsBefore<=4 && selectionRays==0,"two real actors share one O(team) candidate scan per step with no selection rays");
                foreach(var actor in new[]{a,b})
                {
                    var timeline=(NpcTrajectory)GetOptional(actor,"AimTimeline");if(timeline==null)continue;
                    Require(timeline.SampleTick<Main.GameUpdateCount,"timeline stamps true prior sample rather than negative-age current GUC");
                    bool lead=timeline.Count>1 && Vector2.DistanceSquared((Vector2)Get(actor,"Aim"),((NPC)Get(actor,"Target")).Center)>1;
                    if(ReferenceEquals(actor,a))leadA|=lead;else leadB|=lead;
                    Console.WriteLine("NPC PHASE role="+((Player)Get(actor,"Body")).whoAmI+" receive="+received+" consume="+Main.GameUpdateCount+" sample="+timeline.SampleTick+" count="+timeline.Count+" use="+((Player)Get(actor,"Body")).controlUseItem);
                }
                releaseCapture|=!first.controlUseItem && (long)Get(a,"LastAimAttempt")!=attemptA || !second.controlUseItem && (long)Get(b,"LastAimAttempt")!=attemptB;
                n.UpdateNPC(18);m.UpdateNPC(19);Call(host,"AdvanceNpcObservation");
                // Secondary packets arrive only after this completed world,
                // then next player phase consumes them without reinjection.
                if(step%2==0){Queue(cb,Native(23,19));Call(host,"PumpConnection",b);}
                lastA=(long)Get(a,"LastAimAttempt");lastB=(long)Get(b,"LastAimAttempt");
                // Finish after both roles have a real lead and a natural
                // release edge, while their targets remain independent. Long
                // native eye motion can legitimately converge onto one NPC.
                var ta=(NPC)GetOptional(a,"Target");var tb=(NPC)GetOptional(b,"Target");
                if(step>=17 && leadA && leadB && releaseCapture && ta!=null && tb!=null && ta.whoAmI!=tb.whoAmI)break;
            }
            Require(lastA>0 && lastB>0 && leadA && leadB && releaseCapture,"each actor obtains nonzero lead, including a real semi-auto release-frame fresh capture");
            Console.WriteLine("COST candidate selection reads="+selectionReads+" rays="+selectionRays+" same shared work allowance; no FPS claim");
            var early=first.whoAmI<second.whoAmI?a:b;var late=ReferenceEquals(early,a)?b:a;
            var earlyBody=(Player)Get(early,"Body");var lateTarget=(NPC)Get(late,"Target");var earlyTarget=(NPC)Get(early,"Target");
            Require(earlyTarget!=null && lateTarget!=null && earlyTarget.whoAmI!=lateTarget.whoAmI,"independent current targets remain for death-before-later-slot counterexample");
            // A realizable retained scheduling history: the later slot has
            // waited longer, but its same-generation target dies before this
            // step's first player. No Control has refreshed its Target yet.
            Set(late,"LastAimAttempt",0L);Set(early,"LastAimAttempt",100L);uint epoch=Main.GameUpdateCount;
            var dead=Main.npc[lateTarget.whoAmI];dead.life=0;dead.active=false;DeliverObservation(host,a,b,ca,cb,Native(23,dead.whoAmI),dead.whoAmI);
            DeliverObservation(host,a,b,ca,cb,Native(23,earlyTarget.whoAmI),earlyTarget.whoAmI);
            NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",0UL);Main.LocalPlayer.Update(Main.myPlayer);earlyBody.Update(earlyBody.whoAmI);
            var leases=(IDictionary)Get(host,"npcSources");var earlyLease=leases[earlyTarget.whoAmI];
            Console.WriteLine("NPC DEATH WINDOW early="+earlyBody.whoAmI+" expectedTarget="+earlyTarget.whoAmI+" actual="+((NPC)GetOptional(early,"Target"))?.whoAmI+" earlyActive="+earlyTarget.active+" lateActive="+lateTarget.active+" waiting="+Get(earlyLease,"Waiting")+" baseline="+Get(earlyLease,"AwaitBaseline")+" epoch="+epoch+" clock="+Main.GameUpdateCount+" fresh="+Call(host,"FreshNpcObservation",earlyTarget)+" selected="+((Player)Get(Get(host,"aimSelected"),"Body")).whoAmI+" attempt="+Get(early,"LastAimAttempt"));
            var validTimeline=(NpcTrajectory)GetOptional(early,"AimTimeline");Require(validTimeline!=null && validTimeline.SampleTick==epoch,"later stale target death cannot steal earlier eligible role's only fresh publication window");
            ((Player)Get(late,"Body")).Update(((Player)Get(late,"Body")).whoAmI);
            }
            finally
            {
                // These slots/observations belong solely to this phase leaf.
                // Even its first failure must not leave a dead/stale target or
                // a source barrier masquerading as a later weapon failure.
                n.active=m.active=false;
                foreach(var p in Main.projectile)if(p.active && (p.owner==first.whoAmI || p.owner==second.whoAmI) && !existing.Contains((int)p.key))p.Kill();
                Call(host,"ClearWorldSources");
                foreach(var actor in new[]{a,b}){Set(actor,"Target",null);Set(actor,"AimTimeline",null);Set(actor,"AimTerrain",null);Set(actor,"AimPlayer",null);Set(actor,"HasAim",false);}
            }
        }
        private static void DeliverObservation(object host,object a,object b,object ca,object cb,byte[] frame,int slot)
        {
            var lease=((IDictionary)Get(host,"npcSources"))[slot];var source=GetOptional(lease,"Source");
            if(source==null)Apply(frame);else{var connection=ReferenceEquals(source,a)?ca:cb;Queue(connection,frame);Call(host,"PumpConnection",source);}
        }
        private static void Continuous(object host,Player first,Player second,object a,object b,List<byte[]> fa,List<byte[]> fb)
        {NativeCompanionCoreWeaponChecks.Run(host,first,second,a,b,fa,fb,primary);}
        private static void PrimaryProjectile(object host,object a,object b,object ca,object cb,Player first,Player second)
        {
            var setup=AccessTools.Method(typeof(Projectile),"NewProjectileSetup");var key=new ProjectileKey(255,42,123);
            // A server-owned incoming packet cannot be born with the client
            // NewProjectile factory (its key would belong to Main.myPlayer).
            // Serialize a native server-key shape, then remove only the source
            // template from the receiver pool before its single real arrival.
            var template=(Projectile)setup.Invoke(null,new object[]{key});template.SetDefaults(ProjectileID.WoodenArrowFriendly);template.active=true;template.owner=255;template.Center=first.Center;template.velocity=new Vector2(1,0);
            var birth=Native(27,template.whoAmI);Main.projectile[template.whoAmI]=new Projectile();Queue(ca,birth);Call(host,"PumpConnection",a);
            Projectile p;Require(key.TryGetActive(out p),"secondary baseline native receiver imports server-owned projectile once");
            p.Center=second.Center;p.velocity=new Vector2(4,1);var changed=Native(27,p.whoAmI);p.Center=first.Center;p.velocity=new Vector2(1,0);
            Queue(ca,changed);Call(host,"PumpConnection",a);Call(host,"FlushSourceRequests");
            var lease=((IDictionary)Get(host,"projectileSources"))[key.Spawner*1024+key.Index];Require((bool)Get(lease,"Waiting"),"secondary one-shot update is actually before source pong");Apply(changed);
            Require(p.velocity==new Vector2(4,1),"single legitimate primary 27 survives secondary source pending barrier without later retransmit");
            var ended=Native(29,(int)p.key);Apply(ended);Queue(ca,changed);Call(host,"PumpConnection",a);Require(!p.active,"primary29 exact-key tombstone rejects late old27");
            var next=new ProjectileKey(key.Spawner,key.Index,key.Generation+1);template=(Projectile)setup.Invoke(null,new object[]{next});template.SetDefaults(ProjectileID.WoodenArrowFriendly);template.active=true;template.owner=255;template.Center=second.Center;
            var nextBirth=Native(27,template.whoAmI);Main.projectile[template.whoAmI]=new Projectile();Queue(cb,nextBirth);Call(host,"PumpConnection",b);
            Require(next.TryGetActive(out p),"new generation first birth27 arrives exactly once from other stream without post-pong replay");Queue(ca,changed);Call(host,"PumpConnection",a);Require(next.TryGetActive(out p) && !key.TryGetActive(out template),"late old generation cannot replace next identity");
        }
        private static void Personal(object host,Player first,Player second,object a,object b,object ca,object cb,List<byte[]> fa,List<byte[]> fb)
        {NativeCompanionCorePersonalChecks.Run(host,first,second,a,b,ca,cb,fa,fb,primary);}
        private static int idleItemUpdates;
        private static void IdleUpdate(){idleItemUpdates++;}
        internal static void CheckIdle(object host)
        {
            Require(((IDictionary)Get(host,"actors")).Count==0,"idle workload has no live or pending roles");
            var items=Main.item;var npcs=Main.npc;var gate=new Harmony("JueMingR.Tests.CompanionIdleWork");idleItemUpdates=0;
            gate.Patch(AccessTools.Method(typeof(WorldItem),"UpdateItem"),prefix:new HarmonyMethod(typeof(NativeCompanionCoreNetworkChecks),nameof(IdleUpdate)));
            try
            {
                for(int i=0;i<100;i++){NativeQuickItemChecks.BeginWorldStep();Call(host,"AdvancePersonalItems");Call(host,"AdvanceNpcObservation");}
                Require(idleItemUpdates==0 && GetOptional(host,"observedNpcs")==null && GetOptional(host,"aimSelected")==null && ReferenceEquals(items,Main.item) && ReferenceEquals(npcs,Main.npc),"zero roles create/update no personal or observation world and retain no scheduler role");
                Console.WriteLine("PASS cost zero roles: 100 real clock steps, private item updates=0, observation/scheduler absent; no FPS claim.");
            }
            finally{gate.UnpatchAll(gate.Id);}
        }
    }
}
