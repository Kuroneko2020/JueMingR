using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatInteractionChecks
    {
        private static readonly List<byte[]> packets=new List<byte[]>();
        internal static void Run(object context)
        {
            // Vanilla compares UI identity here. Both null would incorrectly
            // mean the bestiary is open; no bestiary drawing is under test.
            Main.BestiaryUI=(Terraria.GameContent.UI.States.UIBestiaryTest)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Terraria.GameContent.UI.States.UIBestiaryTest));
            Main.instance.currentNPCShowingChatBubble=-1;
            var combat=Get(context,"Combat");var tools=Get(context,"Tools");var input=Get(context,"Input");
            foreach(bool enabled in new[]{false,true})foreach(bool door in new[]{false,true})foreach(int weapon in new[]{162,198})
            {
                NativeCombatCadenceChecks.Save(combat,new CombatOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,weapon,0,0);p.inventory[1].SetDefaults(671);
                p.chest=-1;Main.playerInventory=false;Main.SmartCursorWanted_Mouse=false;Main.SmartCursorWanted_GamePad=false;Main.ClearSmartInteract();
                if(door){NativeToolsChecks.Tile(43,39,1);for(int y=0;y<3;y++){var tile=Main.tile[43,40+y];tile.active(true);tile.type=10;tile.frameX=0;tile.frameY=(short)(y*18);}}
                else Chest();
                NativeCombatCadenceChecks.Step(context,false,false,0);p.releaseUseTile=true;Main.mouseRightRelease=true;
                NativeCombatCadenceChecks.Save(combat,new CombatOptions(enabled?6:0));
                NativeCombatCadenceChecks.Step(context,false,true,0,point:new Vector2(43*16+8,41*16+8));
                Require(door?Main.tile[43,41].type==11:p.chest==0,"native ordinary mouse opens "+(door?"door":"chest")+" weapon="+weapon+" combat="+enabled);
                Require(!(bool)Get(Get(combat,"Use"),"Active") && p.itemAnimation==0,"native interaction precedes combat use");
                NativeCombatCadenceChecks.Step(context,false,false,0);NativeCombatCadenceChecks.Save(combat,new CombatOptions());p.chest=-1;Main.playerInventory=false;Main.chest[0]=null;
            }
            Network(context,combat,tools,input);
            Console.WriteLine("PASS G11A original ordinary-mouse chest/door priority and original client packet13 press/release/selection after right-click arbitration.");
        }
        internal static void Chest()
        {
            for(int y=0;y<2;y++)for(int x=0;x<2;x++){var tile=Main.tile[43+x,41+y];tile.active(true);tile.type=21;tile.frameX=(short)(x*18);tile.frameY=(short)(y*18);}
            Main.chest[0]=null;Terraria.Chest.CreateWorldChest(0,43,41);
        }
        private static void Network(object context,object combat,object tools,object input)
        {
            var p=NativeToolExecutionChecks.Reset(context,tools,input,198,0,0);p.inventory[1].SetDefaults(671);NativeCombatCadenceChecks.Step(context,false,false,0);
            NativeCombatCadenceChecks.Save(combat,new CombatOptions(4));
            var audit=new Harmony("JueMingR.Tests.CombatInteractionPackets");audit.Patch(AccessTools.Method(typeof(NetMessage),"SendData"),postfix:new HarmonyMethod(typeof(NativeCombatInteractionChecks),nameof(Capture)));
            Netplay.Connection=new RemoteServer{PendingTermination=true};NetMessage.buffer[256]=new MessageBuffer();Main.clientPlayer=new Player();Main.netMode=1;packets.Clear();
            try
            {
                for(int i=0;i<160;i++)NativeCombatCadenceChecks.Step(context,false,true,0);
                Require(packets.Any(b=>(b[4]&32)!=0 && b[8]==0) && packets.Any(b=>(b[4]&32)!=0 && b[8]==1),"native packet13 publishes both actual right-held weapon uses");
                Require(packets.Any(b=>(b[4]&32)==0),"native packet13 publishes release phases");
                for(int i=0;i<40;i++)NativeCombatCadenceChecks.Step(context,false,false,0);
                Require((packets.Last()[4]&32)==0 && Main.clientPlayer.controlUseItem==p.controlUseItem,"original synced-control snapshot ends released");
            }
            finally{Main.netMode=0;NativeCombatCadenceChecks.Save(combat,new CombatOptions());foreach(var m in audit.GetPatchedMethods().ToArray())audit.Unpatch(m,HarmonyPatchType.All,audit.Id);}
        }
        private static void Capture(int __0)
        {
            if(Main.netMode!=1 || __0!=13)return;
            byte[] buffer=NetMessage.buffer[256].writeBuffer;int length=BitConverter.ToUInt16(buffer,0);
            Require(length>=9 && buffer[2]==13,"actual original control packet serialized");packets.Add(buffer.Take(length).ToArray());
        }
    }
}
