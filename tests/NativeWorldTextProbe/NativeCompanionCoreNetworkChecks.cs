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
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.Net.Sockets;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionCoreNetworkChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static List<byte[]> primary;
        private static string specialistLeaf;
        internal static void RunObservationCombat(object host, Player first, Guid other)
        {
            specialistLeaf = "npc observation combat consumers";
            try { Run(host, first, other); }
            finally { specialistLeaf = null; }
        }
        internal static void RunSpecialist(object host,Player first,Guid other,string leaf)
        {
            if(leaf!="movement planning independent of attack" && leaf!="held player continuation without another NPC23" && leaf!="primary one-shot projectile update" && leaf!="npc observation physical properties" && leaf!="npc observation shared state" && leaf!="npc observation slot isolation" && leaf!="npc observation interest reentry" && leaf!="npc observation source ambiguity" && leaf!="npc observation publication ambiguity" && leaf!="npc observation current geometry" && leaf!="npc observation club contact" && leaf!="npc observation linked health" && leaf!="npc observation battle state" && leaf!="npc observation shape phases" && leaf!="npc observation support flags" && leaf!="npc observation zero root" && leaf!="npc observation wall root" && leaf!="npc observation mimic local phase" && leaf!="npc observation battle publication" && leaf!="npc observation armed history" && leaf!="npc observation armed fresh contact" && leaf!="npc observation conditional phase" && leaf!="npc observation explosion window" && leaf!="npc observation hit generation" && leaf!="npc observation first interest" && leaf!="npc observation statue client" && leaf!="npc observation snail client")throw new ArgumentException("Unknown companion specialist leaf");
            specialistLeaf=leaf;Console.WriteLine("SPECIALIST ONLY "+leaf+"; not the complete network group");
            try{Run(host,first,other);}finally{specialistLeaf=null;}
        }
        private static Player heldBody;
        private static uint heldResumeTick,heldShotTick;
        private static int heldResumeAge,heldOrdinal;
        private static Vector2 heldShotOrigin,heldShotPoint,heldBasic;
        private static void DumpHeld(object actor)
        {
            var timeline=(NpcTrajectory)GetOptional(actor,"AimTimeline");var terrain=GetOptional(actor,"AimTerrain");var target=(NPC)GetOptional(actor,"Target");
            int age=timeline==null?-1:(int)((long)Main.GameUpdateCount-timeline.SampleTick-1);
            Console.WriteLine("HELD PATH count="+(timeline?.Count??0)+" stop="+timeline?.Stop+" age="+age+" anchor="+Get(actor,"AimPlayerAnchor")+" last="+Get(actor,"AimPlayerTick")+" lease="+(terrain==null?"null":Get(terrain,"Current"))+" actual="+target?.position+"/"+target?.velocity+" expected="+(timeline!=null && age>=0 && age<timeline.Count?timeline[age].Bounds.X+","+timeline[age].Bounds.Y+"/"+timeline[age].Vx+","+timeline[age].Vy:"outside"));
        }
        private static void HeldShoot(Player __instance)
        {if(ReferenceEquals(__instance,heldBody)){heldShotTick=Main.GameUpdateCount;heldShotOrigin=__instance.RotatedRelativePoint(__instance.MountedCenter);heldShotPoint=Main.MouseWorld;}}
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
        internal static void ApplyLatestNpc(int slot){Apply(primary.Last(p=>p[2]==23 && p[3]==slot));}
        private static void Queue(object connection,byte[] frame){Call(connection,"ReceiveRelay",frame);}
        private static int Wood(Player p){return p.inventory.Where(i=>i.type==ItemID.Wood).Sum(i=>i.stack);}
        internal static void Run(object host,Player first,Guid other)
        {
            var restoreNpcSlots=NativeCompanionNpcStateChecks.PreserveNativeSlotData();
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
            Action<string,Action> leaf=(name,action)=>{if(specialistLeaf!=null && specialistLeaf!=name)return;try{action();Console.WriteLine("PASS core network leaf "+name);}catch(Exception e){Console.Error.WriteLine("FAIL core network leaf "+name+": "+e);failures.Add(e);}};
            try
            {
                NativeCompanionCoreLifecycleChecks.PrepareHuman();
                Main.netMode=1;NetMessage.buffer[256]=new MessageBuffer{whoAmI=256};
                Call(host,"BeginPasswordSession");
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
                leaf("npc observation physical properties",()=>{NativeCompanionNpcPhysicsChecks.Run(host,first,second,a,b,slot=>Apply(Native(23,slot)));HeldContinuation(host,first,second,a,b,true);});
                if (specialistLeaf == "npc observation combat consumers") leaf(specialistLeaf, () =>
                {
                    NativeCompanionNpcPhysicsChecks.Run(host, first, second, a, b, slot => Apply(Native(23, slot)));
                    NativeCompanionNpcStateChecks.Geometry(host, first, second, a, b, slot => Apply(Native(23, slot)));
                });
                leaf("npc observation shared state",()=>NativeCompanionNpcStateChecks.Run(host,first,second,a,b,slot=>Apply(Native(23,slot)),slot=>{Queue(ca,Native(23,slot));Call(host,"PumpConnection",a);}));
                leaf("npc observation slot isolation",()=>NativeCompanionNpcStateChecks.Run(host,first,second,a,b,slot=>Apply(Native(23,slot)),slot=>{Queue(ca,Native(23,slot));Call(host,"PumpConnection",a);},true));
                leaf("npc observation interest reentry",()=>InterestReentry(host,listener,first,second));
                leaf("npc observation first interest",()=>InterestReentry(host,listener,first,second,true));
                leaf("npc observation statue client",()=>NativeCompanionNpcStateChecks.LocalState(host,first,second,slot=>Native(23,slot),Apply));
                leaf("npc observation snail client",()=>NativeCompanionNpcStateChecks.LocalState(host,first,second,slot=>Native(23,slot),Apply,true));
                // Hand-selected source-domain diagnostics are retained for
                // explicit investigation only. They prove no natural-domain
                // impossibility and are not product correctness gates.
                if(specialistLeaf=="npc observation source ambiguity")leaf(specialistLeaf,()=>NativeCompanionNpcStateChecks.SourceAmbiguity(host,slot=>Native(23,slot)));
                if(specialistLeaf=="npc observation publication ambiguity")leaf(specialistLeaf,()=>NativeCompanionNpcStateChecks.PublicationAmbiguity(host,slot=>Native(23,slot)));
                leaf("npc observation current geometry",()=>NativeCompanionNpcStateChecks.Geometry(host,first,second,a,b,slot=>Apply(Native(23,slot))));
                leaf("npc observation club contact",()=>NativeCompanionNpcStateChecks.Geometry(host,first,second,a,b,slot=>Apply(Native(23,slot)),true));
                leaf("npc observation conditional phase",()=>NativeCompanionNpcStateChecks.Conditional(host,first,second,slot=>Apply(Native(23,slot))));
                leaf("npc observation explosion window",()=>NativeCompanionNpcStateChecks.Explosion(host,first,second,a,slot=>Apply(Native(23,slot))));
                leaf("npc observation hit generation",()=>NativeCompanionNpcStateChecks.HitGeneration(host,first,second,a,slot=>Native(23,slot),packet=>{Queue(ca,packet);Call(host,"PumpConnection",a);},Apply,fa));
                leaf("npc observation battle publication",()=>NativeCompanionNpcStateChecks.BattlePublication(host,first,second,a,b,slot=>Apply(Native(23,slot)),fa,fb));
                leaf("npc observation armed history",()=>NativeCompanionNpcStateChecks.ArmedCycle(host,first,second,a,Apply));
                leaf("npc observation armed fresh contact",()=>NativeCompanionNpcStateChecks.ArmedCycle(host,first,second,a,Apply,true));
                leaf("npc observation mimic local phase",()=>NativeCompanionNpcStateChecks.MimicPhase(host,first,second,a,b,slot=>Apply(Native(23,slot)),fa,fb));
                leaf("npc observation support flags",()=>NativeCompanionNpcStateChecks.Shape(host,first,second,slot=>Apply(Native(23,slot)),true));
                leaf("npc observation shape phases",()=>NativeCompanionNpcStateChecks.Shape(host,first,second,slot=>Apply(Native(23,slot))));
                leaf("npc observation battle state",()=>NativeCompanionNpcStateChecks.Battle(host,first,second,a,slot=>Apply(Native(23,slot)),fa));
                leaf("npc observation zero root",()=>RootHealth(host,first,second,a,b,ca,cb,fa,fb,0,true));
                leaf("npc observation wall root",()=>RootHealth(host,first,second,a,b,ca,cb,fa,fb,24,true));
                leaf("npc observation linked health",()=>RootHealth(host,first,second,a,b,ca,cb,fa,fb));
                leaf("movement planning independent of attack",()=>NativeCompanionCoreLifecycleChecks.Planning(host,first,second));
                leaf("prediction real receive/player phases and two-role fairness",()=>Prediction(host,first,second,a,b,ca,cb));
                leaf("held player continuation without another NPC23",()=>HeldContinuation(host,first,second,a,b));
                leaf("prism actual Update sync tail and map/flail",()=>Continuous(host,first,second,a,b,fa,fb));
                leaf("primary one-shot projectile update",()=>PrimaryProjectile(host,a,b,ca,cb,first,second,fa));
                leaf("personal90 native pickup and save",()=>Personal(host,first,second,a,b,ca,cb,fa,fb));
            }
            finally
            {
                Main.netMode=mode;Main.sectionManager=sections;NetMessage.buffer[256]=buffer;Netplay.Connection.Socket=socket;
                patch.UnpatchAll(patch.Id);try{Call(host,"Recall",other);}finally{client.Close();peer.Close();listener.Stop();restoreNpcSlots();}
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
            var farBodies=((IEnumerable)Get(host,"ActiveActors")).Cast<object>().Select(a=>(Player)Get(a,"Body")).ToArray();var positions=farBodies.Select(p=>p.position).ToArray();var humanPosition=Main.LocalPlayer.position;
            for(int i=0;i<farBodies.Length;i++)farBodies[i].position=new Vector2(4000+i*100,900);
            Main.LocalPlayer.position=new Vector2(Main.spawnTileX*16+80,918);Call(host,"ClearWorldSources");
            var humanNpc=Main.npc[17];humanNpc.SetDefaults(NPCID.Zombie);humanNpc.active=true;humanNpc.position=new Vector2(Main.spawnTileX*16+160,960-humanNpc.height);humanNpc.AddBuff(BuffID.Poisoned,321);var state=Native(23,17);var buffs=Native(54,17);int originalLife=humanNpc.life;
            var oldSixteen=Main.npc[16];var factory=AccessTools.Method(typeof(NPC),"NewNPCInstanceInSlot");
            var oldGeneration=(NPC)factory.Invoke(null,new object[]{16,(byte)1});oldGeneration.SetDefaults(NPCID.Zombie);oldGeneration.active=true;oldGeneration.position=new Vector2(1240,960-oldGeneration.height);oldGeneration.AddBuff(BuffID.Poisoned,300);var oldState=Native(23,16);var oldBuff=Native(54,16);
            var nextGeneration=(NPC)factory.Invoke(null,new object[]{16,(byte)2});nextGeneration.SetDefaults(NPCID.Zombie);nextGeneration.active=true;nextGeneration.position=oldGeneration.position;var nextState=Native(23,16);
            try{
            Require((bool)Call(host,"Join",profile.Id),"real MP connection starts native handshake");NativeQuickItemChecks.Until(()=>listener.Pending());using(var remote=listener.AcceptTcpClient())
            {
                var stream=remote.GetStream();Write(stream,Frame(3,w=>w.Write((byte)3)));PumpUntil(host,()=>Call(host,"OwnedSlot",3)!=null);
                Apply(state);var pendingLease=((IDictionary)Get(host,"npcSources"))[17];
                Require(!(bool)Get(pendingLease,"NpcBaseline") && !((NPC[])Get(host,"observedNpcs"))[17].active && ReferenceEquals(Main.npc[17],humanNpc),"attached but not ready primary23 records a source without inventing an AI consumed baseline");
                Write(stream,World(Main.ActiveWorldFileData.UniqueId));
                // Each original state appears exactly once, before 49/129.
                Write(stream,state);Write(stream,buffs);Write(stream,oldState);Write(stream,oldBuff);Write(stream,nextState);Write(stream,Frame(49));Write(stream,Frame(129));
                PumpUntil(host,()=>{var readyRole=Call(host,"OwnedSlot",3);return readyRole!=null && (bool)Get(readyRole,"Ready");});
                var observed=(NPC[])Get(host,"observedNpcs");Require(observed[17].active && observed[17].FindBuffIndex(BuffID.Poisoned)>=0 && ReferenceEquals(Main.npc[17],humanNpc) && humanNpc.life==originalLife,"initial 23/54 survives real 3/7/23/54/49/129 order without replacement human table");
                var actor=Call(host,"OwnedSlot",3);var role=(Player)Get(actor,"Body");var flow=Get(actor,"Connection");
                Require(observed[17].generation==humanNpc.generation && (bool)Get(pendingLease,"NpcBaseline") && ReferenceEquals(Get(pendingLease,"Source"),actor) && Call(host,"Observer",observed[17].Center)==null,"first successful buffered baseline is usable even when human stays closer than the hysteresis margin");
                Require(observed[16].generation==nextGeneration.generation && observed[16].FindBuffIndex(BuffID.Poisoned)<0,"same-stream old23/old54/new23 cannot attach the old generation's buffs to the new native identity");
                Call(host,"SetBehavior",profile.Id,new CompanionBehavior{Follow=false,AttackEnabled=true});NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",0UL);role.Update(role.whoAmI);
                Require(ReferenceEquals(GetOptional(actor,"Target"),observed[17]) && ReferenceEquals(Main.npc[17],humanNpc),"initial interleaved NPC and buff baseline participates in actual Player.Update target selection without replacing human table");
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
            NativeQuickItemChecks.BeginWorldStep();Call(host,"AdvanceNpcObservation");
            Require(!(bool)Get(((IDictionary)Get(host,"npcSources"))[17],"NpcBaseline"),"explicit loss of all ready-actor interest retires the consumed observation qualification");
            CaptureRetry(host,listener,profile.Id);
            foreach(bool wrongWorld in new[]{false,true})
            {
                Require((bool)Call(host,"Join",profile.Id),"failed role can begin a fresh activation");NativeQuickItemChecks.Until(()=>listener.Pending());using(var remote=listener.AcceptTcpClient())
                {
                    var stream=remote.GetStream();Write(stream,Frame(3,w=>w.Write((byte)3)));PumpUntil(host,()=>Call(host,"OwnedSlot",3)!=null);
                    if(wrongWorld)Write(stream,World(Guid.NewGuid()));else Write(stream,Frame(2,w=>NetworkText.FromLiteral("fixture unrelated refusal").Serialize(w)));
                    PumpUntil(host,()=>!((IDictionary)Get(host,"actors")).Contains(profile.Id));string status=(string)Call(host,"Status",profile.Id);
                    Require(status.Contains(wrongWorld?"世界与当前世界不一致":"fixture unrelated refusal") && Call(host,"OwnedSlot",3)==null,"failure reason persists after socket/save/slot/activation release: "+status);
                    Require(remote.Client.Poll(100000,SelectMode.SelectRead),"terminal flow closes instead of infinite reconnect");
                }
            }
            }finally{humanNpc.active=false;Main.npc[16]=oldSixteen;var observations=(NPC[])GetOptional(host,"observedNpcs");if(observations!=null){observations[17].active=false;observations[16].active=false;}for(int i=0;i<farBodies.Length;i++)farBodies[i].position=positions[i];Main.LocalPlayer.position=humanPosition;}
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
                var currentNpc=((NPC[])Get(host,"observedNpcs"))[17];Require(currentNpc.active && currentNpc.generation==Main.npc[17].generation && currentNpc.FindBuffIndex(BuffID.Poisoned)>=0 && ReferenceEquals(Get(((IDictionary)Get(host,"npcSources"))[17],"Source"),actor),"fresh real Ready baseline and matching54 recover after explicit interest retirement without another coincidental state packet");
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
            foreach(var actor in new[]{a,b})Call(host,"SetBehavior",((CompanionProfile)Get(actor,"Profile")).Id,new CompanionBehavior{Follow=false,AttackEnabled=true});
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
        private static void HeldContinuation(object host,Player first,Player second,object a,object b,bool floating=false)
        {
            var human=Main.LocalPlayer;var bodies=new[]{human,first,second};var positions=bodies.Select(p=>p.position).ToArray();var velocities=bodies.Select(p=>p.velocity).ToArray();
            var behaviorA=((CompanionProfile)Get(a,"Profile")).Behavior;var behaviorB=((CompanionProfile)Get(b,"Profile")).Behavior;var brainA=Get(a,"Behavior");var brainB=Get(b,"Behavior");
            var items=new[]{first.inventory[0],first.inventory[54]};int selected=first.selectedItem;var oldNpc=Main.npc[21];var tiles=new List<Tuple<int,int,Tile>>();double surface=Main.worldSurface;
            var existing=new HashSet<int>(Main.projectile.Where(p=>p.active).Select(p=>(int)p.key));
            var monitor=new Harmony("JueMingR.Tests.CompanionHeldContinuation");var aim=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackAim");
            // Inspect retained actor state after Player.Update. Observation
            // wrappers inside the real two-ms scope distort first publication.
            // This correctness fixture prepares only code, without invoking a
            // hidden world or changing the real shared enhancement allowance.
            // Cold startup deferrals remain recorded by the earlier runs.
            var code=new[]{typeof(PlayerMotionContinuation),host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain+CellLease"),host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot"),host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels"),host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles"),host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackReceive"),host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPlayerFrame")};
            foreach(var type in code){Require(type!=null,"held correctness code preparation names actual participating types");foreach(var method in type.GetMethods(Flags|BindingFlags.DeclaredOnly))if(!method.IsAbstract && !method.ContainsGenericParameters)System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(method.MethodHandle);foreach(var constructor in type.GetConstructors(Flags))System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(constructor.MethodHandle);}
            Console.WriteLine("HELD code-only preparation types="+code.Length+"; no world warmup, unchanged shared two-ms owner; no cold-performance claim");
            monitor.Patch(AccessTools.Method(typeof(Player),"ItemCheck_Shoot"),prefix:new HarmonyMethod(typeof(NativeCompanionCoreNetworkChecks),nameof(HeldShoot)));
            try
            {
                Main.worldSurface=40;
                foreach(bool laterSlot in floating?new[]{false}:new[]{false,true})
                {
                    Call(host,"ClearWorldSources");foreach(var actor in new[]{a,b}){Set(actor,"Target",null);Set(actor,"AimTimeline",null);Set(actor,"AimTerrain",null);Set(actor,"AimPlayer",null);Set(actor,"HasAim",false);}
                    human.position=new Vector2(laterSlot?1240:1200,918);first.position=new Vector2(1000,918);second.position=new Vector2(laterSlot?1200:1240,918);
                    foreach(var p in bodies)p.velocity=Vector2.Zero;
                    first.inventory[0]=new Item(ItemID.VenusMagnum);first.inventory[54]=new Item(ItemID.MusketBall){stack=100};first.selectedItemState.Select(0);first.itemAnimation=first.itemTime=first.reuseDelay=0;first.releaseUseItem=true;
                    Set(a,"Behavior",Activator.CreateInstance(brainA.GetType(),true));Set(b,"Behavior",Activator.CreateInstance(brainB.GetType(),true));
                    Call(host,"SetBehavior",((CompanionProfile)Get(a,"Profile")).Id,new CompanionBehavior{Follow=false,AttackEnabled=true});
                    Call(host,"SetBehavior",((CompanionProfile)Get(b,"Profile")).Id,new CompanionBehavior{Follow=false,AttackEnabled=false});
                    NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",0UL);foreach(var p in bodies)p.Update(p.whoAmI);
                    var controlled=laterSlot?second:human;Console.WriteLine("HELD PREMISE slot="+controlled.whoAmI+" pos="+controlled.position+" velocity="+controlled.velocity+" solid="+Collision.SolidCollision(controlled.position,controlled.width,controlled.height)+" bounds="+Main.leftWorld+","+Main.rightWorld);Require(!Collision.SolidCollision(controlled.position,controlled.width,controlled.height),"ordinary held player has an actual clear body after native world-border clamp");var npc=(NPC)AccessTools.Method(typeof(NPC),"NewNPCInstanceInSlot").Invoke(null,new object[]{21,(byte)(laterSlot?11:10)});npc.SetDefaults(floating?NPCID.DemonEye:NPCID.Zombie);npc.active=true;npc.position=new Vector2(1110,floating?878:960-npc.height);npc.velocity=floating?new Vector2(4*(2-npc.scale),1.5f*(2-npc.scale)):new Vector2(2-npc.scale,0);if(floating){npc.UpdateNPC(21);Require(npc.noGravity,"real original AI_002 prepares the flying source before23");}else npc.TargetClosest();Require(npc.target==controlled.whoAmI,"native TargetClosest actually chooses the intended earlier/later player before its real23 baseline");
                    uint sample=Main.GameUpdateCount;Apply(Native(23,21));heldBody=first;heldResumeTick=heldShotTick=UInt32.MaxValue;
                    NpcTrajectory captured=null;int resumed=0;bool agedShot=false;var seen=new HashSet<int>(Main.projectile.Where(p=>p.active).Select(p=>(int)p.key));
                    // Cold enhancement may legally defer first publication.
                    // Establish it through bounded real subsequent server
                    // samples, then inject NO 23 throughout retained use.
                    // A full native weapon cycle supplies the aged shot.
                    int steps=first.HeldItem.useAnimation+2;
                    for(int step=0,attempts=0;step<steps;)
                    {
                        NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",0UL);human.Update(human.whoAmI);first.Update(first.whoAmI);
                        var timeline=(NpcTrajectory)GetOptional(a,"AimTimeline");
                        var premise=(PredictionPlayerMotion?)GetOptional(a,"AimPlayer");heldBasic=((NPC)Get(a,"Target")).Center+((NPC)Get(a,"Target")).netOffset;
                        if(step>0 && premise.HasValue && (uint)Get(a,"AimPlayerTick")==Main.GameUpdateCount-(laterSlot?1U:0U) && GetOptional(a,"AimTerrain")!=null && (Vector2)Get(a,"Aim")!=heldBasic)
                        {heldResumeTick=Main.GameUpdateCount;heldResumeAge=(int)((long)Main.GameUpdateCount-timeline.SampleTick-1);heldOrdinal=premise.Value.GravityAction;}
                        if(step==0){var work=(CombatPredictionWork)Get(host,"work");var target=(NPC)GetOptional(a,"Target");Console.WriteLine("HELD WORK guc="+Main.GameUpdateCount+" usedMs="+work.UsedMilliseconds+" exceeded="+work.Exceeded+" native="+npc.active+"/"+npc.life+" target="+target?.active+"/"+target?.life+"/"+target?.velocity+" fresh="+(target!=null && (bool)Call(host,"FreshNpcObservation",target))+" predictedDead="+(target!=null && (bool)Call(host,"PredictionDead",a,target))+" attempt="+Get(a,"LastAimAttempt"));}
                        if(step==0 && (timeline==null || GetOptional(a,"AimTerrain")==null))
                        {
                            Console.WriteLine("HELD FIRST PUBLICATION deferred attempt="+(++attempts)+" count="+(timeline?.Count??0)+" lease="+(GetOptional(a,"AimTerrain")!=null));
                            Require(attempts<4,"ordinary first publication completes within four real server samples under the unchanged shared budget");
                            second.Update(second.whoAmI);npc.UpdateNPC(21);Call(host,"AdvanceNpcObservation");sample=Main.GameUpdateCount;Apply(Native(23,21));continue;
                        }
                        if(step==0){var prediction=Get(host,"prediction");var raw=(NpcTrajectory)GetOptional(Get(prediction,"Cache"),"result");var t=(NPC)Get(a,"Target");Console.WriteLine("HELD RAW count="+(raw?.Count??0)+" stop="+raw?.Stop+" kind="+raw?.PlayerCondition?.Kind+" outcome="+Get(prediction,"outcomeTick")+"/"+Get(prediction,"outcomeLayer")+"/"+Get(prediction,"outcomeStop")+" targetPlayer="+t.target+" controlled="+controlled.active+"/"+controlled.dead+"/"+controlled.ghost+" sourceNative="+(GetOptional(prediction,"Native")!=null));}
                        if(step==0 || heldResumeTick!=Main.GameUpdateCount)DumpHeld(a);
                        if(step==0){captured=timeline;var selectedRole=GetOptional(host,"aimSelected");var target=(NPC)GetOptional(a,"Target");Console.WriteLine("HELD CAPTURE timeline="+(timeline==null?"null":timeline.Count.ToString())+" lease="+(GetOptional(a,"AimTerrain")!=null)+" aim="+Get(a,"Aim")+" selected="+(selectedRole==null?-1:((Player)Get(selectedRole,"Body")).whoAmI)+" attempt="+Get(a,"LastAimAttempt")+" fresh="+(target!=null && (bool)Call(host,"FreshNpcObservation",target))+" item="+first.HeldItem.type+" target="+target);Require(captured!=null && captured.PlayerCondition.Kind==PredictionPlayerConditionKind.HeldControls && captured.PlayerCondition.Sample.PlayerIndex==controlled.whoAmI,"one real23 establishes the intended ordinary HeldControls player");Require((uint)Get(a,"AimPlayerAnchor")==Main.GameUpdateCount-(laterSlot?1U:0U),"capture anchor respects actual earlier/later player slot phase");}
                        Require(ReferenceEquals(timeline,captured) && timeline.SampleTick==sample,"multiple player phases retain the same true NPC sample without reinjection/restamping");
                        if(step>0){Console.WriteLine("HELD RESUME slot="+controlled.whoAmI+" age="+step+" hit="+(heldResumeTick==Main.GameUpdateCount)+" ordinal="+heldOrdinal);Require(heldResumeTick==Main.GameUpdateCount && heldResumeAge==step && heldOrdinal==step,"every ordinary aged phase actually resumes its cached action at the cumulative player ordinal");resumed++;}
                        foreach(var shot in Main.projectile.Where(p=>p.active && p.owner==first.whoAmI && seen.Add((int)p.key)))
                        {
                            if(step==0)continue;
                            var actual=Vector2.Normalize(shot.velocity);var expected=Vector2.Normalize(heldShotPoint-heldShotOrigin);
                            var basicPoint=new Vector2((int)(heldBasic.X-Main.screenPosition.X)+Main.screenPosition.X,(int)(heldBasic.Y-Main.screenPosition.Y)+Main.screenPosition.Y);var basic=Vector2.Normalize(basicPoint-heldShotOrigin);
                            float error=Vector2.DistanceSquared(actual,expected),basicError=Vector2.DistanceSquared(actual,basic);
                            Require(heldShotTick==Main.GameUpdateCount && heldResumeTick==Main.GameUpdateCount && error<1e-12f && basicError>error*16+1e-12f,"aged cache changes the actual native shot direction, distinguished from quantized current-center fallback");agedShot=true;
                            Console.WriteLine("HELD SHOT slot="+controlled.whoAmI+" age="+step+" aimError="+error+" basicError="+basicError);
                        }
                        second.Update(second.whoAmI);npc.UpdateNPC(21);Call(host,"AdvanceNpcObservation");
                        step++;
                    }
                    Require(resumed==steps-1 && agedShot,"multi-step ordinary retained action reaches a real aged native shot in both player slot phases");
                    controlled.velocity=new Vector2(1,controlled.velocity.Y);
                    NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",0UL);human.Update(human.whoAmI);first.Update(first.whoAmI);
                    Require(heldResumeTick!=Main.GameUpdateCount && (Vector2)Get(a,"Aim")==((NPC)Get(a,"Target")).Center+((NPC)Get(a,"Target")).netOffset,"changed actual player motion rejects the retained action locally and keeps legal basic aim");
                    foreach(var p in Main.projectile)if(p.active && p.owner==first.whoAmI && !existing.Contains((int)p.key))p.Kill();
                    npc.active=false;heldBody=null;
                }
            }
            finally
            {
                heldBody=null;monitor.UnpatchAll(monitor.Id);Main.worldSurface=surface;foreach(var tile in tiles)Main.tile[tile.Item1,tile.Item2]=tile.Item3;
                foreach(var p in Main.projectile)if(p.active && p.owner==first.whoAmI && !existing.Contains((int)p.key))p.Kill();Main.npc[21]=oldNpc;Call(host,"ClearWorldSources");
                for(int i=0;i<bodies.Length;i++){bodies[i].position=positions[i];bodies[i].velocity=velocities[i];}
                first.inventory[0]=items[0];first.inventory[54]=items[1];first.selectedItemState.Select(selected);first.itemAnimation=first.itemTime=first.reuseDelay=0;first.channel=false;
                Call(host,"SetBehavior",((CompanionProfile)Get(a,"Profile")).Id,behaviorA);Call(host,"SetBehavior",((CompanionProfile)Get(b,"Profile")).Id,behaviorB);Set(a,"Behavior",brainA);Set(b,"Behavior",brainB);
                foreach(var actor in new[]{a,b}){Set(actor,"Target",null);Set(actor,"AimTimeline",null);Set(actor,"AimTerrain",null);Set(actor,"AimPlayer",null);Set(actor,"HasAim",false);}
            }
        }
        private static void Continuous(object host,Player first,Player second,object a,object b,List<byte[]> fa,List<byte[]> fb)
        {NativeCompanionCoreWeaponChecks.Run(host,first,second,a,b,fa,fb,primary);}
        private static void PrimaryProjectile(object host,object a,object b,object ca,object cb,Player first,Player second,List<byte[]> outgoing)
        {
            var human=Main.LocalPlayer;var positions=new[]{human.position,first.position,second.position};
            human.position=new Vector2(1000,900);first.position=new Vector2(800,900);second.position=new Vector2(1200,900);
            try{
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
            SecondaryAfterPrimary(host,a,ca,first,second,p,outgoing);
            var ended=Native(29,(int)p.key);Apply(ended);Queue(ca,changed);Call(host,"PumpConnection",a);Require(!p.active,"primary29 exact-key tombstone rejects late old27");
            var next=new ProjectileKey(key.Spawner,key.Index,key.Generation+1);template=(Projectile)setup.Invoke(null,new object[]{next});template.SetDefaults(ProjectileID.WoodenArrowFriendly);template.active=true;template.owner=255;template.Center=second.Center;
            var nextBirth=Native(27,template.whoAmI);Main.projectile[template.whoAmI]=new Projectile();Queue(cb,nextBirth);Call(host,"PumpConnection",b);
            Require(next.TryGetActive(out p),"new generation first birth27 arrives exactly once from other stream without post-pong replay");Queue(ca,changed);Call(host,"PumpConnection",a);Require(next.TryGetActive(out p) && !key.TryGetActive(out template),"late old generation cannot replace next identity");
            }finally{human.position=positions[0];first.position=positions[1];second.position=positions[2];}
        }
        private static Dictionary<int,List<byte[]>> serverFrames;
        private static bool CaptureServer(byte[] data,int remoteClient)
        {List<byte[]> frames;if(!serverFrames.TryGetValue(remoteClient,out frames))serverFrames[remoteClient]=frames=new List<byte[]>();frames.Add(data.Take(BitConverter.ToUInt16(data,0)).ToArray());return false;}
        private static void SectionActivated(Point point){}
        private static void ClientSectionActivated(int client,Point point){}
        private static void SecondaryAfterPrimary(object host,object actor,object connection,Player first,Player second,Projectile projectile,List<byte[]> outgoing)
        {
            int mode=Main.netMode,width=Main.maxTilesX,height=Main.maxTilesY,sx=Main.maxSectionsX,sy=Main.maxSectionsY;float right=Main.rightWorld,bottom=Main.bottomWorld;
            var tile=Main.tile;var clients=Netplay.Clients;var buffers=NetMessage.buffer;var human=Main.LocalPlayer;var humanPosition=human.position;var secondPosition=second.position;
            var activeTime=AccessTools.Field(typeof(ActiveSections),"LastActiveTime");var active=activeTime.GetValue(null);var center=projectile.Center;var previousVelocity=projectile.velocity;
            var packets=new Harmony("JueMingR.Tests.CompanionSectionProjectile");serverFrames=new Dictionary<int,List<byte[]>>();
            packets.Patch(AccessTools.Method(typeof(NetMessage),"SendPacket"),prefix:new HarmonyMethod(typeof(NativeCompanionCoreNetworkChecks),nameof(CaptureServer)){priority=Priority.First,before=new[]{"JueMingR.Tests.QuickItemOutlets"}});
            ActiveSections.SectionActivated+=SectionActivated;RemoteClient.NetSectionActivated+=ClientSectionActivated;
            try
            {
                // Only this native network component gets three valid section
                // columns. Known original cells retain their exact objects;
                // distant cells remain unknown and no player/world AI runs.
                Main.maxTilesX=600;Main.maxTilesY=150;Main.maxSectionsX=3;Main.maxSectionsY=1;Main.rightWorld=9600;Main.bottomWorld=2400;
                Main.tile=new Tile[600,150];for(int x=0;x<tile.GetLength(0);x++)for(int y=0;y<tile.GetLength(1);y++)Main.tile[x,y]=tile[x,y];
                activeTime.SetValue(null,new uint[4,2]);Netplay.Clients=new RemoteClient[256];NetMessage.buffer=new MessageBuffer[257];for(int i=0;i<257;i++)NetMessage.buffer[i]=new MessageBuffer{whoAmI=i};
                int humanSlot=human.whoAmI,actorSlot=first.whoAmI;
                foreach(int id in new[]{humanSlot,actorSlot}){var client=new RemoteClient{Id=id,State=10,Socket=Netplay.Connection.Socket};for(int x=0;x<3;x++)client.TileSections[x,0]=true;Netplay.Clients[id]=client;NetMessage.buffer[id].broadcast=true;}
                human.position=first.position;second.position=new Vector2(8700,900);
                Main.netMode=2;RemoteClient.CheckSection(humanSlot,human.Center);RemoteClient.CheckSection(actorSlot,first.Center);
                uint start=Main.GameUpdateCount;human.position=new Vector2(7600,900);
                for(int step=1;step<=ActiveSections.SectionInactiveTime+1;step++)
                {
                    NativeQuickItemChecks.BeginWorldStep();RemoteClient.CheckSection(humanSlot,human.Center);RemoteClient.CheckSection(actorSlot,first.Center);
                    if(step==ActiveSections.SectionInactiveTime)Require(Netplay.Clients[humanSlot].IsSectionActive(projectile.NetSectionCoordinates),"native projectile section grace remains active at its exact60 boundary");
                }
                Require(!Netplay.Clients[humanSlot].IsSectionActive(projectile.NetSectionCoordinates) && Netplay.Clients[actorSlot].IsSectionActive(projectile.NetSectionCoordinates),"real CheckSection clock order expires primary section while keeping actor section active");
                Action<Vector2> send=velocity=>{serverFrames.Clear();projectile.velocity=velocity;try{Main.netMode=2;NetMessage.SendData(27,-1,-1,null,projectile.whoAmI);}finally{projectile.velocity=previousVelocity;Main.netMode=1;}};
                send(new Vector2(3,2));
                var skipped=(bool[])Get(projectile,"netSyncSkippedForPlayer");Require(!serverFrames.ContainsKey(humanSlot) && serverFrames.ContainsKey(actorSlot) && serverFrames[actorSlot].Count(p=>p[2]==27)==1 && skipped[humanSlot] && !skipped[actorSlot],"actual server27 broadcast filter emits this ordinary key only to active actor, not merely to an already-sent section");
                Queue(connection,serverFrames[actorSlot].Single(p=>p[2]==27));Call(host,"PumpConnection",actor);Call(host,"FlushSourceRequests");
                var key=projectile.key;var lease=((IDictionary)Get(host,"projectileSources"))[key.Spawner*1024+key.Index];
                Require((bool)Get(lease,"Waiting") && projectile.velocity==previousVelocity,"first actor-only27 waits for its real ordered handoff instead of rewriting primary history");
                var ping=outgoing.Last(p=>p[2]==154);Main.netMode=2;var decoder=new MessageBuffer{whoAmI=actorSlot};Buffer.BlockCopy(ping,2,decoder.readBuffer,0,ping.Length-2);decoder.ResetReader();serverFrames.Clear();int type;decoder.GetData(0,ping.Length-2,out type);Main.netMode=1;
                Queue(connection,serverFrames[actorSlot].Single(p=>p[2]==154));Call(host,"PumpConnection",actor);
                Require(!(bool)Get(lease,"Waiting") && (bool)Get(lease,"AwaitBaseline"),"actual native server ping response completes source ordering, without inventing a state baseline");
                send(new Vector2(7,-2));var successor=serverFrames[actorSlot].Single(p=>p[2]==27);Require(!serverFrames.ContainsKey(humanSlot),"post-barrier changed state still has no primary27 recipient");Queue(connection,successor);Call(host,"PumpConnection",actor);
                Require(projectile.active && projectile.velocity==new Vector2(7,-2) && ReferenceEquals(Get(lease,"Source"),actor) && !(bool)Get(lease,"AwaitBaseline"),"one new post-barrier actor27 really reaches native consumer for the same primary-seen key");
                Console.WriteLine("PROJECTILE AREA start="+start+" end="+Main.GameUpdateCount+" primary27=0 actor27=1 nativePong=True successor="+projectile.velocity);
            }
            finally
            {
                Main.netMode=mode;packets.UnpatchAll(packets.Id);ActiveSections.SectionActivated-=SectionActivated;RemoteClient.NetSectionActivated-=ClientSectionActivated;
                Main.tile=tile;Main.maxTilesX=width;Main.maxTilesY=height;Main.maxSectionsX=sx;Main.maxSectionsY=sy;Main.rightWorld=right;Main.bottomWorld=bottom;activeTime.SetValue(null,active);Netplay.Clients=clients;NetMessage.buffer=buffers;
                human.position=humanPosition;second.position=secondPosition;projectile.Center=center;serverFrames=null;
            }
        }
        private static void Personal(object host,Player first,Player second,object a,object b,object ca,object cb,List<byte[]> fa,List<byte[]> fb)
        {NativeCompanionCorePersonalChecks.Run(host,first,second,a,b,ca,cb,fa,fb,primary);}
        private static int idleItemUpdates;
        private static void IdleUpdate(){idleItemUpdates++;}
        internal static void CheckIdle(object host)
        {
            Require(((IDictionary)Get(host,"actors")).Count==0,"idle workload has no live or pending roles");
            var items=Main.item;var npcs=Main.npc;var staticHits=Projectile.perIDStaticNPCImmunity;var gate=new Harmony("JueMingR.Tests.CompanionIdleWork");idleItemUpdates=0;
            gate.Patch(AccessTools.Method(typeof(WorldItem),"UpdateItem"),prefix:new HarmonyMethod(typeof(NativeCompanionCoreNetworkChecks),nameof(IdleUpdate)));
            try
            {
                for(int i=0;i<100;i++){NativeQuickItemChecks.BeginWorldStep();Call(host,"AdvancePersonalItems");Call(host,"AdvanceNpcObservation");}
                Require(idleItemUpdates==0 && GetOptional(host,"observedNpcs")==null && GetOptional(host,"observedNpcStaticHits")==null && GetOptional(host,"primaryNpcStaticHits")==null && GetOptional(host,"aimSelected")==null && ReferenceEquals(items,Main.item) && ReferenceEquals(npcs,Main.npc) && ReferenceEquals(staticHits,Projectile.perIDStaticNPCImmunity),"zero roles create/update no personal or observation world, retain no hit-state matrix/scheduler role and restore the exact native static table");
                Console.WriteLine("PASS cost zero roles: 100 real clock steps, private item updates=0, observation/scheduler absent; no FPS claim.");
            }
            finally{gate.UnpatchAll(gate.Id);}
        }
    }
}
