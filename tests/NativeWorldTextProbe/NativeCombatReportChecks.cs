using System;
using System.Linq;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.Combat;
using Terraria;
using Terraria.Chat.Commands;
using Terraria.GameContent;
using Terraria.Net;
using Terraria.GameContent.NetModules;
using Terraria.UI.Chat;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatReportChecks
    {
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var reports=GetOptional(combat,"Reports");
            Require(reports!=null,"independent automatic native report owner is absent");
            Require((bool)Get(reports,"Ready"),"report hooks must be available: "+GetOptional(reports,"Error"));
            NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return ((CombatSettings)Get(combat,"Settings")).Loaded;});
            for(int i=0;i<Main.player.Length;i++){if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};Main.player[i].active=i==0;}
            Main.LocalPlayer.name="Combat report fixture";Main.worldName="Combat report isolated world";
            Terraria.GameContent.Creative.CreativePowerManager.Initialize();
            ChatManager.Commands.AddCommand<BossDamageCommand>();
            if(NetManager.Instance.GetModule<NetTextModule>()==null)NetManager.Instance.Register<NetTextModule>();
            var sink=new Harmony("JueMingR.Tests.CombatReportText");sink.Patch(AccessTools.Method(typeof(Main),"NewTextMultiline"),prefix:new HarmonyMethod(typeof(NativeCombatReportChecks),nameof(Text)));
            sink.Patch(AccessTools.Method(typeof(Terraria.Chat.ChatHelper),"DisplayMessage"),postfix:new HarmonyMethod(typeof(NativeCombatReportChecks),nameof(Displayed)));
            try
            {
                NPCDamageTracker.Reset();Boss(4,true);NativeCombatCadenceChecks.Save(combat,new CombatOptions(64));
                Advance(reports,2);Require(lines==0,"first valid observation only establishes the existing-record baseline");
                Boss(50,false);Boss(222,true);Advance(reports,1);
                Require(lines==3 && Get(reports,"State").ToString()=="Observed","new batch executes actual native command and observes all three recent reports, lines="+lines+" state="+Get(reports,"State"));
                int stableReads=(int)(GetOptional(reports,"RecentReads")??0);Advance(reports,30);Require(lines==3 && (int)(GetOptional(reports,"RecentReads")??0)==stableReads,"unchanged local records neither repeat nor re-enumerate");
                Main.LocalPlayer.dead=true;Call(reports,"Poll");int deadBefore=lines;
                Boss(4,false);Advance(reports,1);
                Require(lines==deadBefore+3,"death retains report-only session identity and observes boss escape");
                Main.LocalPlayer.dead=false;
                foreach(int group in new[]{1,2,3,4,-2,-1})
                {
                    var tracker=new InvasionDamageTracker(group);NPCDamageTracker.Start(tracker);tracker.AddDamage(0,25);NPCDamageTracker.Update();
                    int before=lines;Advance(reports,1);Require(lines==before+3,"native event group "+group+" retains recent scope");
                }
                Client(context,combat,reports);
                Console.WriteLine("PASS G11A actual boss/escape/six-event reports, baseline/batch/unknown, native client end packets and original broadcast round trip; all sockets are in-memory.");
            }
            finally
            {
                Main.netMode=0;Main.dedServ=false;Main.myPlayer=0;Main.LocalPlayer.dead=false;NativeCombatCadenceChecks.Save(combat,new CombatOptions());NPCDamageTracker.Reset();
                foreach(var method in sink.GetPatchedMethods().ToArray())sink.Unpatch(method,HarmonyPatchType.All,sink.Id);
            }
        }
        private static int lines;
        private static bool Text(){return false;}
        private static void Displayed(bool __runOriginal){if(__runOriginal)lines++;}
        private static void Advance(object reports,int ticks)
        {for(int i=0;i<ticks;i++){NativeQuickItemChecks.BeginWorldStep();Call(reports,"Poll");}}
        private static void Boss(int type,bool killed)
        {
            foreach(var n in Main.npc)n.active=false;
            var npc=Main.npc[0];npc.SetDefaults(type);npc.whoAmI=0;npc.active=true;npc.life=npc.lifeMax;
            NPCDamageTracker.AddDamage(npc,0,30);if(killed)NPCDamageTracker.BossKilled(npc);npc.active=false;
            NPC.ClearFoundActiveNPCs();NPC.UpdateFoundActiveNPCs();NPCDamageTracker.Update();
        }
        private static void Client(object context,object combat,object reports)
        {
            NativeCombatCadenceChecks.Save(combat,new CombatOptions());NPCDamageTracker.Reset();foreach(var n in Main.npc)n.active=false;
            var socket=new CombatSocket();Netplay.Connection=new RemoteServer{Socket=socket,IsActive=true,State=10};Main.netMode=1;
            Call(context,"UpdateRuntime");NativeCombatCadenceChecks.Save(combat,new CombatOptions(64));
            Require(!NPCDamageTracker.RecentAttempts().Any(),"ordinary-client trigger has no local recent trackers");
            CombatNetworkFixture.Npc(0,1,4,true);CombatNetworkFixture.Npc(0,1,4,false);Advance(reports,3);
            Require(socket.Packets.Count==1 && socket.Packets[0][2]==82 && Get(reports,"State").ToString()=="Unknown","native client requests typed command once without fictional ACK");
            CombatNetworkFixture.Npc(0,1,4,false);Advance(reports,20);Require(socket.Packets.Count==1,"duplicate terminal packet does not request again");
            byte[] request=socket.Packets[0];
            // Server simulation runs only in this isolated process. Product
            // runtime is not polled while its client globals are temporarily lent.
            Main.netMode=2;Main.dedServ=true;Main.myPlayer=255;
            var a=new CombatSocket();var b=new CombatSocket();Netplay.Clients[0]=new RemoteClient{Id=0,State=10,Socket=a};Netplay.Clients[1]=new RemoteClient{Id=1,State=10,Socket=b};Main.player[1].active=true;Main.player[1].name="Second fixture player";
            Boss(4,true);Boss(50,false);CombatNetworkFixture.Receive(request,0);
            Require(a.Packets.Count==2 && b.Packets.Count==2,"original server command broadcasts each recent report to both active players");
            Main.netMode=1;Main.dedServ=false;Main.myPlayer=0;Main.player[1].active=false;NPCDamageTracker.Reset();
            foreach(byte[] packet in a.Packets)CombatNetworkFixture.Receive(packet);
            Require(Get(reports,"State").ToString()=="Observed","real native deserialization/display establishes observed report without local trackers");
            int sent=socket.Packets.Count;
            int baselineReads=(int)(GetOptional(reports,"BaselineNpcReads")??0);Main.LocalPlayer.name="Changed fixture name";Advance(reports,20);
            Require(socket.Packets.Count==sent && (int)(GetOptional(reports,"BaselineNpcReads")??0)==baselineReads,"rename is neither connection identity nor reason to rescan/replay");
            CombatNetworkFixture.Npc(1,2,125,true);CombatNetworkFixture.Npc(2,3,126,true);
            CombatNetworkFixture.Npc(1,2,125,false);Advance(reports,5);Require(socket.Packets.Count==sent,"one twin alone cannot end the server-observed group");
            Main.npc[2].life=0;Main.npc[2].active=false;Advance(reports,5);Require(socket.Packets.Count==sent,"local predicted disappearance cannot end the retained server member");
            CombatNetworkFixture.Npc(2,3,126,false);Advance(reports,3);Require(socket.Packets.Count==++sent,"final twin terminal packet triggers one request");
            foreach(int group in new[]{1,2,3,4,-2,-1})
            {CombatNetworkFixture.Event(group>0?group:0,group==-2,group==-1);CombatNetworkFixture.Event(0);Advance(reports,3);Require(socket.Packets.Count==++sent,"native world-info event end "+group);}
            socket.Connected=false;CombatNetworkFixture.Npc(3,4,4,true);CombatNetworkFixture.Npc(3,4,4,false);Advance(reports,3);
            Require(Get(reports,"State").ToString()=="Pending" && socket.Packets.Count==sent,"preflight failure retains an uninvoked opportunity");
            PendingSave(combat,reports);
            Main.LocalPlayer.dead=true;socket.Connected=true;Advance(reports,16);Require(socket.Packets.Count==++sent,"death does not erase a queued report before invocation");Main.LocalPlayer.dead=false;
            socket.ThrowSend=true;CombatNetworkFixture.Npc(3,4,4,true);CombatNetworkFixture.Npc(3,4,4,false);Advance(reports,3);
            Require(Get(reports,"State").ToString()=="Unknown","swallowed socket failure stays unknown");socket.ThrowSend=false;Advance(reports,60);Require(socket.Packets.Count==sent,"unknown send is never blindly retried");
            var successor=new CombatSocket();Netplay.Connection.Socket=successor;Advance(reports,3);
            Require(successor.Packets.Count==0 && Get(reports,"State").ToString()=="NotInvoked","new socket silently rebuilds baseline without old unknown request");
        }
        private static readonly ManualResetEvent saveEntered=new ManualResetEvent(false),saveRelease=new ManualResetEvent(false);
        private static void BlockCombatSave(object __instance)
        {if(((string)Get(__instance,"path")).EndsWith("combat.json",StringComparison.OrdinalIgnoreCase)){saveEntered.Set();if(!saveRelease.WaitOne(5000))throw new TimeoutException("isolated save gate");}}
        private static void PendingSave(object combat,object reports)
        {
            var fault=new Harmony("JueMingR.Tests.CombatPendingSave");Type document=Get(Get(Get(combat,"Settings"),"worker"),"storage").GetType();
            var method=AccessTools.Method(document,"Write");fault.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatReportChecks),nameof(BlockCombatSave)));
            var settings=(CombatSettings)Get(combat,"Settings");saveEntered.Reset();saveRelease.Reset();
            try
            {
                Require(settings.Set(new CombatOptions(65)),"unrelated ability change accepted during pending report");Require(saveEntered.WaitOne(2000),"real document worker entered isolated blocked write");Call(combat,"Poll");
                Require(Get(reports,"State").ToString()=="Pending","uncommitted unrelated setting cannot erase report observation/pending state");
            }
            finally{saveRelease.Set();fault.Unpatch(method,HarmonyPatchType.All,fault.Id);NativeQuickItemChecks.Until(()=>{Call(combat,"Poll");return !settings.Busy;});}
        }
    }
}
