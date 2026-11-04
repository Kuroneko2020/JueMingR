using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using Terraria;
using Terraria.GameContent.Creative;
using Terraria.IO;
using Terraria.Net;
using Terraria.Net.Sockets;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // The real outer save owns IOLock before this pause, but has not entered
    // InternalSavePlayerFile. The existing independent no-R reader is the oracle.
    internal static class NativeJourneyDisconnectSaveChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static ManualResetEvent entered,resume;
        private static int writerThread,packets;
        private static bool auditPackets;
        internal static void Run(object context,string content,string output)
        {
            object journey=Get(context,"Journey");var oldPlayerFile=Main.ActivePlayerFileData;var oldWorldFile=Main.ActiveWorldFileData;
            int oldMode=Main.netMode;var oldSocket=Netplay.Connection.Socket;
            var h=new Harmony("NativeJourneyDisconnectSaveChecks");
            var pause=typeof(Player).GetMethod("InternalSavePlayer",Flags);
            h.Patch(pause,prefix:new HarmonyMethod(typeof(NativeJourneyDisconnectSaveChecks),nameof(PauseBeforePlayerFile)){priority=Priority.First});
            foreach(var method in typeof(NetManager).GetMethods(Flags).Where(m=>!m.IsStatic && !m.ContainsGenericParameters && (m.Name.StartsWith("SendTo",StringComparison.Ordinal) || m.Name.StartsWith("Broadcast",StringComparison.Ordinal))))
                h.Patch(method,prefix:new HarmonyMethod(typeof(NativeJourneyDisconnectSaveChecks),nameof(CountPacket)));
            try
            {
                foreach(string edge in new[]{"mode","socket-new-session","normal-exit","server-mode","slot-reused"})
                {
                    Call(journey,"SetEnabled",false);CreativePowerManager.Instance.Reset();NativeJourneyChecks.ConfigureFiles(0,0,"disconnect-"+edge);Call(context,"UpdateRuntime");
                    Personal(false,true,.25f);Main.LocalPlayer.inventory[12].SetDefaults(Terraria.ID.ItemID.Torch);Main.LocalPlayer.inventory[12].stack=41;
                    byte[] world=Capture(w=>CreativePowerManager.Instance.SaveToWorld(w)),baseline=Capture(w=>CreativePowerManager.Instance.SaveToPlayer(Main.LocalPlayer,w));
                    WorldFile.SaveWorld();Main.netMode=1;Call(context,"UpdateRuntime");
                    Require((bool)Call(journey,"SetEnabled",true),"disconnect-save fixture enters a real original Journey window");
                    Personal(true,false,.85f);Require(!Capture(w=>CreativePowerManager.Instance.SaveToPlayer(Main.LocalPlayer,w)).SequenceEqual(baseline),"native borrowed personal powers differ before the real save");
                    var time=CreativePowerManager.Instance.GetPower<CreativePowers.ModifyTimeRate>();Shared(time,.3f);
                    typeof(CreativePowers.ASharedSliderPower).GetField("_currentTargetValue",Flags).SetValue(time,.9f);
                    typeof(CreativePowers.ASharedSliderPower).GetField("_needsToCommitChange",Flags).SetValue(time,true);
                    entered=new ManualResetEvent(false);resume=new ManualResetEvent(false);Exception failure=null;
                    var file=Main.ActivePlayerFileData;var thread=new Thread(()=>{writerThread=Thread.CurrentThread.ManagedThreadId;try{Player.SavePlayer(file,skipMapSave:true);}catch(Exception e){failure=e;}});
                    ISocket replacement=null;int oldIndex=Main.myPlayer,oldWho=Main.LocalPlayer.whoAmI,otherIndex=oldIndex==0?1:0;
                    Player original=Main.LocalPlayer,otherPlayer=Main.player[otherIndex];
                    try
                    {
                        thread.Start();Require(entered.WaitOne(15000),"original SavePlayer reached IO-locked pre-file boundary");
                        Console.WriteLine("JOURNEY-DISCONNECT SAVE_PAUSED edge="+edge+" stage=InternalSavePlayer-before-file thread="+writerThread);
                        if(edge=="slot-reused")
                        {
                            // Same Player/File references, but the old native
                            // index now belongs to another Player. Save must
                            // retain A without restoring into either new slot.
                            Main.player[oldIndex]=new Player{whoAmI=oldIndex};Main.player[otherIndex]=original;Main.myPlayer=otherIndex;original.whoAmI=otherIndex;
                            Personal(true,false,.93f);
                            CreativePowerManager.Instance.GetPower<CreativePowers.GodmodePower>().SetEnabledState(oldIndex,true);
                            CreativePowerManager.Instance.GetPower<CreativePowers.FarPlacementRangePower>().SetEnabledState(oldIndex,false);
                            Spawn(oldIndex,.61f);
                        }
                        Shared(time,.6f);packets=0;auditPackets=true;
                        try
                        {
                            // Exit an ACTIVE real Runtime session; a normal
                            // feature-disable command is covered by Menu.
                            if(edge=="normal-exit"){Main.gameMenu=true;Call(context,"UpdateRuntime");}
                            else{if(edge=="socket-new-session")Netplay.Connection.Socket=replacement=new TcpSocket();else Main.netMode=edge=="server-mode"?2:0;Call(journey,"Update",(ulong)1);}
                        }
                        finally{auditPackets=false;}
                        Require(!(bool)Get(journey,"IsActive") && !(bool)Get(journey,"Enabled") && GetOptional(journey,"Persistence")==null,"connection edge revokes Journey immediately without retaining runtime eligibility");
                        Require(packets==0 && (float)typeof(CreativePowers.ASharedSliderPower).GetField("_sliderCurrentValueCache",Flags).GetValue(time)==.6f,"retirement preserves newer shared native state and sends zero additional packets");
                        bool personalRestored=Capture(w=>CreativePowerManager.Instance.SaveToPlayer(Main.LocalPlayer,w)).SequenceEqual(baseline);
                        Console.WriteLine("JOURNEY-DISCONNECT RETIRED edge="+edge+" packets="+packets+" personalRestored="+personalRestored);
                        if(edge=="slot-reused")
                        {
                            var god=CreativePowerManager.Instance.GetPower<CreativePowers.GodmodePower>();var range=CreativePowerManager.Instance.GetPower<CreativePowers.FarPlacementRangePower>();
                            var spawn=CreativePowerManager.Instance.GetPower<CreativePowers.SpawnRateSliderPerPlayerPower>();
                            var values=(float[])typeof(CreativePowers.APerPlayerSliderPower).GetField("_cachePerPlayer",Flags).GetValue(spawn);
                            Require(god.IsEnabledForPlayer(oldIndex) && god.IsEnabledForPlayer(otherIndex) && !range.IsEnabledForPlayer(oldIndex) && !range.IsEnabledForPlayer(otherIndex) && values[oldIndex]==.61f && values[otherIndex]==.93f && (float)typeof(CreativePowers.APerPlayerSliderPower).GetField("_sliderCurrentValueCache",Flags).GetValue(spawn)==.93f,
                                "slot retirement cannot overwrite another player's old index, the moved local index or current native slider cache");
                        }
                        byte[] liveB=null;
                        byte[] offBaseline=baseline;
                        if(edge=="socket-new-session")
                        {
                            Personal(true,true,.4f);offBaseline=Capture(w=>CreativePowerManager.Instance.SaveToPlayer(Main.LocalPlayer,w));
                            Require((bool)Call(journey,"SetEnabled",true),"new endpoint can explicitly start B while old protected A save waits");Personal(false,false,.95f);
                            liveB=Capture(w=>CreativePowerManager.Instance.SaveToPlayer(Main.LocalPlayer,w));
                        }
                        resume.Set();Require(thread.Join(15000) && failure==null,"original deferred player save completes: "+failure);
                        if(liveB!=null)Require((bool)Get(journey,"IsActive") && Capture(w=>CreativePowerManager.Instance.SaveToPlayer(Main.LocalPlayer,w)).SequenceEqual(liveB),"old A save completion cannot retire or mutate later B runtime state");
                        NativeJourneyChecks.WriteManifest(output,"disconnect-"+edge,0,0,world,baseline);
                        // Always read the actual encrypted output in a fresh CLR,
                        // even on RED; a live-memory comparison cannot sign persistence.
                        NativeJourneyChecks.ReadInNewProcess(content,output);
                        if(edge!="slot-reused")Require(personalRestored,"the same local Player/File/slot also restores its live personal baseline; outer SaveView alone is insufficient");
                        Console.WriteLine("PASS JOURNEY-DISCONNECT independent-original-reader edge="+edge+" baseline=A");
                        if(edge!="slot-reused")
                        {
                            Call(journey,"SetEnabled",false);Main.netMode=0;
                            Main.ActivePlayerFileData=new PlayerFileData(Path.Combine(Path.GetDirectoryName(file.Path),"off-"+edge+".plr"),false){Player=Main.LocalPlayer,Metadata=file.Metadata};
                            Player.SavePlayer(Main.ActivePlayerFileData,skipMapSave:true);
                            NativeJourneyChecks.WriteManifest(output,"disconnect-"+edge+"-off",0,0,world,offBaseline);
                            NativeJourneyChecks.ReadInNewProcess(content,output);
                            Console.WriteLine("PASS JOURNEY-DISCONNECT later-OFF-original-reader edge="+edge);
                        }
                    }
                    finally
                    {
                        resume.Set();thread.Join(15000);writerThread=0;entered.Dispose();resume.Dispose();
                        Main.myPlayer=oldIndex;Main.player[oldIndex]=original;Main.player[otherIndex]=otherPlayer;original.whoAmI=oldWho;
                        Call(journey,"SetEnabled",false);Netplay.Connection.Socket=oldSocket;if(replacement!=null)replacement.Close();Main.netMode=0;
                    }
                }
            }
            finally
            {
                auditPackets=false;h.Unpatch(pause,HarmonyPatchType.All,h.Id);
                foreach(var method in typeof(NetManager).GetMethods(Flags).Where(m=>!m.IsStatic && !m.ContainsGenericParameters && (m.Name.StartsWith("SendTo",StringComparison.Ordinal) || m.Name.StartsWith("Broadcast",StringComparison.Ordinal))))h.Unpatch(method,HarmonyPatchType.All,h.Id);
                Call(journey,"SetEnabled",false);CreativePowerManager.Instance.Reset();Main.netMode=oldMode;Netplay.Connection.Socket=oldSocket;Main.ActivePlayerFileData=oldPlayerFile;Main.ActiveWorldFileData=oldWorldFile;Call(context,"UpdateRuntime");
            }
        }
        private static void PauseBeforePlayerFile(){if(Thread.CurrentThread.ManagedThreadId!=writerThread)return;entered.Set();if(!resume.WaitOne(15000))throw new TimeoutException("disconnect-save pre-file pause");}
        private static void CountPacket(){if(auditPackets)Interlocked.Increment(ref packets);}
        private static void Personal(bool god,bool range,float spawn)
        {
            CreativePowerManager.Instance.GetPower<CreativePowers.GodmodePower>().SetEnabledState(Main.myPlayer,god);
            CreativePowerManager.Instance.GetPower<CreativePowers.FarPlacementRangePower>().SetEnabledState(Main.myPlayer,range);
            Spawn(Main.myPlayer,spawn);
            Main.LocalPlayer.creativeGodMode=god;
        }
        private static void Spawn(int index,float spawn)
        {using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){w.Write((byte)index);w.Write(spawn);w.Flush();s.Position=0;CreativePowerManager.Instance.GetPower<CreativePowers.SpawnRateSliderPerPlayerPower>().DeserializeNetMessage(new BinaryReader(s),index);}}
        private static void Shared(CreativePowers.ASharedSliderPower power,float value)
        {using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){w.Write(value);w.Flush();s.Position=0;power.DeserializeNetMessage(new BinaryReader(s),Main.myPlayer);}}
        private static byte[] Capture(Action<BinaryWriter> write)
        {using(var s=new MemoryStream()){using(var w=new BinaryWriter(s,System.Text.Encoding.UTF8,true))write(w);return s.ToArray();}}
    }
}
