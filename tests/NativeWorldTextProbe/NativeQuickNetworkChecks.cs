using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.QuickItems;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework.Input;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeQuickNetworkChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static readonly List<byte[]> packets=new List<byte[]>();
        internal static void Run(object context,QuickItemEntry entry)
        {
            var audit=new Harmony("JueMingR.Tests.QuickPacketSerialization");
            MethodInfo send=typeof(NetMessage).GetMethod("SendData",Flags);
            audit.Patch(send,postfix:new HarmonyMethod(typeof(NativeQuickNetworkChecks).GetMethod(nameof(Capture),Flags)));
            object input=Get(context,"Input"),shell=Get(context,"Shell"),quick=Get(context,"QuickItems"),use=Get(quick,"Use");Player p=Main.LocalPlayer;
            var bindings=(HotkeyBindings)Get(Get(shell,"hotkeys"),"Bindings");
            try
            {
                // No connected socket exists; the enclosing fixture throws if
                // any final SendPacket is attempted. SendData remains original,
                // including OnControlsSynced's real client baseline mutation.
                Netplay.Connection=new RemoteServer();Netplay.Connection.PendingTermination=true;NetMessage.buffer[256]=new MessageBuffer();Main.netMode=1;
                NativeQuickGestureChecks.Bind(bindings,entry.ActionId,"Mouse1");
                foreach(int target in new[]{50,3199,3124,5358,4263,5360,4819,5361,5359})
                {
                    NativeQuickUseMatrix.Change(quick,entry.With(target,QuickItemMode.Use,false,true));NativeQuickUseMatrix.Reset(p);
                    p.inventory[7]=new Item();p.inventory[18]=new Item();p.inventory[17].SetDefaults(target);Main.clientPlayer=new Player();packets.Clear();
                    NativeQuickGestureChecks.Sample(input,0);Call(shell,"ProcessInput");NativeQuickGestureChecks.Sample(input,1);Call(shell,"ProcessInput");Frame(p);
                    for(int i=0;i<150;i++){NativeQuickGestureChecks.Sample(input,1,Keys.W);Call(shell,"ProcessInput");Frame(p);}
                    NativeQuickUseMatrix.Returned(p,use,2,"client provider "+target);
                    var controls=packets.Where(b=>b[2]==13).ToArray();
                    Require(controls.Any(b=>b[8]==17 && (b[4]&32)!=0),"real packet13 carries provider/use: "+target);
                    Require(controls.Any(b=>b[8]==17 && (b[4]&32)==0) && controls.Last()[8]==2 && (controls.Last()[4]&32)==0,"real packet13 releases pulse then returns selection: "+target);
                    int destination=target==4263 || target==5360?1:target==4819 || target==5361?2:target==5359?3:0;
                    var teleports=packets.Where(b=>b[2]==73).ToArray();
                    Require(destination==0?teleports.Length==0:teleports.Length==1 && teleports[0][3]==destination,"actual native destination packet73 exactly matches target: "+target);
                }
                NativeQuickGestureChecks.Sample(input,0);Call(shell,"ProcessInput");NativeQuickGestureChecks.Bind(bindings,entry.ActionId,"J");
                // A real family transition occurs after this frame's inventory
                // sync. Next native sync must publish it without custom packets.
                NativeQuickUseMatrix.Change(quick,entry.With(5360,QuickItemMode.SetStateAndUse,false,true));NativeQuickUseMatrix.Reset(p);p.inventory[17].SetDefaults(5361);packets.Clear();
                NativeQuickUseMatrix.Press(input,shell,Keys.J);Frame(p);
                for(int i=0;i<150;i++){NativeQuickItemChecks.Sample(input,new[]{Keys.W});Call(shell,"ProcessInput");Frame(p);}
                Require(p.inventory[17].type==5360 && packets.Any(b=>b[2]==5) && packets.Count(b=>b[2]==73 && b[3]==1)==1,"native inventory and ocean effect synchronization after exact state conversion");
                foreach(int source in new[]{5358,5360,5361,5359,5437})foreach(int target in new[]{5358,5360,5361,5359})
                {
                    NativeQuickUseMatrix.Change(quick,entry.With(target,QuickItemMode.SetStateAndUse,false,true));NativeQuickUseMatrix.Reset(p);
                    p.inventory[17].SetDefaults(source);Item physical=p.inventory[17];physical.favorited=true;int prefix=physical.prefix,recalls=NativeQuickItemChecks.Recalls;
                    var inventory=p.inventory.ToArray();packets.Clear();NativeQuickUseMatrix.Press(input,shell,Keys.J);Frame(p);
                    for(int i=0;i<150;i++){NativeQuickItemChecks.Sample(input,new[]{Keys.W});Call(shell,"ProcessInput");Frame(p);}
                    NativeQuickUseMatrix.Returned(p,use,2,"phone "+source+"→"+target);
                    Require(inventory.Select((item,i)=>ReferenceEquals(item,p.inventory[i])).All(v=>v) && physical.type==target && physical.stack==1 && physical.prefix==prefix && physical.favorited,"phone matrix preserves physical attributes");
                    int destination=target==5360?1:target==5361?2:target==5359?3:0;
                    Require(destination==0?NativeQuickItemChecks.Recalls==recalls+1 && !packets.Any(b=>b[2]==73):packets.Count(b=>b[2]==73)==1 && packets.Any(b=>b[2]==73 && b[3]==destination),"phone matrix exact native destination: "+source+"→"+target);
                    Require(packets.Any(b=>b[2]==13 && b[8]==17 && (b[4]&32)!=0) && packets.Last(b=>b[2]==13)[8]==2,"phone matrix native pulse and return packet");
                }
                Console.WriteLine("PASS: native packet13/73 for 9 providers, all 4x4 phone destinations plus 4 internal-state recovery paths and inventory sync. No server/socket execution.");
            }
            finally{Main.netMode=0;NativeQuickGestureChecks.Sample(input,0);NativeQuickGestureChecks.Bind(bindings,entry.ActionId,"J");audit.Unpatch(send,HarmonyPatchType.All,audit.Id);NativeQuickUseMatrix.Change(quick,entry);NativeQuickUseMatrix.Reset(p);}
        }
        private static void Frame(Player p)
        {typeof(Main).GetMethod("TrySyncingMyPlayer",Flags).Invoke(null,null);NativeQuickItemChecks.NativeFrame(p);}
        private static void Capture(int __0)
        {
            if(Main.netMode!=1 || __0!=5 && __0!=13 && __0!=73)return;
            byte[] buffer=NetMessage.buffer[256].writeBuffer;int length=BitConverter.ToUInt16(buffer,0);
            Require(length>=3 && length<=buffer.Length && buffer[2]==__0,"actual original serialized packet boundary");packets.Add(buffer.Take(length).ToArray());
        }
    }
}
