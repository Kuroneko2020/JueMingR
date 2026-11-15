using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionCorePersonalChecks
    {
        private static int pickingSlot;
        private static object pickingActor;
        private static bool syncOwnedView;
        private static int secondSlot,updatesA,updatesB;
        private static void PrivateUpdate(WorldItem __instance)
        {if(!__instance.instanced)return;if(Main.myPlayer==pickingSlot)updatesA++;else if(Main.myPlayer==secondSlot)updatesB++;}
        private static void SyncView(int msgType,int number)
        {if(msgType==21 && number==399 && Main.myPlayer==pickingSlot && Main.item[number].instanced)syncOwnedView|=ReferenceEquals(((IDictionary)Get(pickingActor,"PersonalItems"))[399],Main.item[number]);}
        private static int Wood(Player p){return p.inventory.Where(i=>i.type==ItemID.Wood).Sum(i=>i.stack);}
        private static WorldItem Drop(int count,Vector2 point,int owner)
        {return new WorldItem(new Item(ItemID.Wood)){whoAmI=399,stack=count,position=point,playerIndexTheItemIsReservedFor=owner,instanced=true};}
        private static byte[] Frame(WorldItem item,List<byte[]> primary)
        {
            var old=Main.item[399];Main.item[399]=item;
            try{NetMessage.SendData(90,-1,-1,null,399);return primary.Last(p=>p[2]==90);}
            finally{Main.item[399]=old;}
        }
        private static void Apply(byte[] frame)
        {var decoder=new MessageBuffer{whoAmI=256};Buffer.BlockCopy(frame,2,decoder.readBuffer,0,frame.Length-2);decoder.ResetReader();int type;decoder.GetData(0,frame.Length-2,out type);}
        private static byte[] Shared(byte id,WorldItem item,List<byte[]> primary)
        {
            var previous=Main.item[399];Main.item[399]=item;
            try{NetMessage.SendData(id,-1,-1,null,399);return primary.Last(p=>p[2]==id);}
            finally{Main.item[399]=previous;}
        }
        private static void Deliver(object host,object ca,object cb,object a,object b,byte[] frame)
        {Apply(frame);Call(ca,"ReceiveRelay",frame);Call(cb,"ReceiveRelay",frame);Call(host,"PumpConnection",a);Call(host,"PumpConnection",b);}
        internal static void Run(object host,Player first,Player second,object a,object b,object ca,object cb,List<byte[]> fa,List<byte[]> fb,List<byte[]> primary)
        {
            var human=Main.LocalPlayer;var original=Main.item[399];var profile=(CompanionProfile)Get(a,"Profile");
            int beforeA=Wood(first),beforeB=Wood(second),beforeH=Wood(human);
            var sync=new Harmony("JueMingR.Tests.CompanionPersonalSync");pickingSlot=first.whoAmI;pickingActor=a;syncOwnedView=false;
            // SyncItem is a one-call wrapper and can be caller-inlined. Observe
            // its real native primitive before instanced's intentional return.
            sync.Patch(AccessTools.Method(typeof(NetMessage),"SendData"),prefix:new HarmonyMethod(typeof(NativeCompanionCorePersonalChecks),nameof(SyncView)));
            secondSlot=second.whoAmI;updatesA=updatesB=0;sync.Patch(AccessTools.Method(typeof(WorldItem),"UpdateItem"),prefix:new HarmonyMethod(typeof(NativeCompanionCorePersonalChecks),nameof(PrivateUpdate)));
            try
            {
                Call(host,"SetBehavior",profile.Id,new CompanionBehavior{Follow=true,AttackEnabled=false});
                var target=first.Center+new Vector2(180,0);human.Center=target;
                var humanDrop=Drop(5,human.position,human.whoAmI);Main.item[399]=humanDrop;
                Call(ca,"ReceiveRelay",Frame(Drop(7,target,first.whoAmI),primary));Call(host,"PumpConnection",a);
                Call(cb,"ReceiveRelay",Frame(Drop(11,second.position,second.whoAmI),primary));Call(host,"PumpConnection",b);
                Require(ReferenceEquals(Main.item[399],humanDrop) && Wood(first)==beforeA && Wood(second)==beforeB,"90 only creates sparse private instances; no receive-time inventory grant or shared overwrite");
                Call(host,"AdvancePersonalItems");Call(host,"AdvancePersonalItems");
                Require(updatesA==1 && updatesB==1 && ReferenceEquals(Main.item[399],humanDrop),"two actual private instances update once per world step, not twice or an entire per-role pool");
                Call(first,"GrabItems",first.whoAmI);Require(Wood(first)==beforeA,"far personal instance obeys native pickup distance");
                Call(second,"GrabItems",second.whoAmI);Call(second,"GrabItems",second.whoAmI);
                Call(human,"GrabItems",human.whoAmI);Call(human,"GrabItems",human.whoAmI);
                Console.WriteLine("PERSONAL COUNTS human="+beforeH+" -> "+Wood(human)+" second="+beforeB+" -> "+Wood(second)+" humanAnim="+human.itemAnimation+" selected="+human.HeldItem.type+" humanDead="+human.dead+" reserved="+humanDrop.playerIndexTheItemIsReservedFor+" active="+humanDrop.active);
                Require(Wood(second)==beforeB+11 && Wood(human)==beforeH+5 && ReferenceEquals(Main.item[399],humanDrop),"human/second role same native slot each collect their own share exactly once");
                var sparse=(IDictionary)Get(a,"PersonalItems");var individual=(WorldItem)sparse[399];
                var inventory=first.inventory.ToArray();for(int i=0;i<50;i++){first.inventory[i]=new Item(ItemID.CopperPickaxe);}
                first.inventory[58]=new Item(ItemID.CopperPickaxe);individual.position=first.position;
                try{Call(first,"GrabItems",first.whoAmI);Require(individual.active && individual.stack==7,"full real inventory cannot accept private share");}
                finally{Array.Copy(inventory,first.inventory,inventory.Length);}
                individual.position=target;
                for(int i=0;i<120 && sparse.Contains(399);i++)
                {NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",0UL);first.Update(first.whoAmI);Call(host,"AdvancePersonalItems");}
                Require(Wood(first)==beforeA+7 && !sparse.Contains(399) && Wood(second)==beforeB+11,"actual native follow/pull/pickup removes only owned sparse instance");
                Set(a,"NextPublish",0U);Call(host,"Publish",a);
                Require(fa.Any(p=>p.Length>11 && p[2]==5 && p[3]==first.whoAmI && BitConverter.ToInt16(p,9)==ItemID.Wood),"personal native pickup publishes actual actor inventory on its own connection");
                foreach(var frame in fa.Where(p=>p[2]==21))Console.WriteLine("PERSONAL SYNC bytes="+BitConverter.ToString(frame)+" length="+frame.Length);
                Require(syncOwnedView && !fa.Any(p=>p.Length>=5 && (p[2]==21 || p[2]==151) && BitConverter.ToInt16(p,3)==399),"native personal SyncItem retains owned view and suppresses shared21/151 as required for instanced items");
                // Actual slot reuse: a new private share expires on its own
                // FIFO151, then ordinary21/22/151 use the same native slot.
                // Duplicate observer copies must never decode shared state.
                Deliver(host,ca,cb,a,b,Shared(151,humanDrop,primary));
                Call(cb,"ReceiveRelay",Frame(Drop(4,second.Center+new Vector2(400,0),second.whoAmI),primary));Call(host,"PumpConnection",b);
                Require(((IDictionary)Get(b,"PersonalItems")).Contains(399),"new private instance owns reused native slot");
                Deliver(host,ca,cb,a,b,Shared(151,humanDrop,primary));
                Require(!((IDictionary)Get(b,"PersonalItems")).Contains(399) && Wood(second)==beforeB+11,"own151 retires uncollected private instance without granting resources");
                var common=Drop(2,first.position,first.whoAmI);common.instanced=false;
                Deliver(host,ca,cb,a,b,Shared(21,common,primary));var shared=Main.item[399];
                Deliver(host,ca,cb,a,b,Shared(22,common,primary));
                Require(ReferenceEquals(Main.item[399],shared) && !shared.instanced && shared.stack==2,"shared21/22 decode once on primary despite secondary copies");
                Call(first,"GrabItems",first.whoAmI);Call(first,"GrabItems",first.whoAmI);Call(second,"GrabItems",second.whoAmI);Call(human,"GrabItems",human.whoAmI);
                Require(Wood(first)==beforeA+9 && Wood(second)==beforeB+11 && Wood(human)==beforeH+5,"same-slot private and shared pickup remain exactly-once and separately owned");
                Deliver(host,ca,cb,a,b,Shared(151,shared,primary));
                int previousUpdates=updatesA+updatesB;NativeQuickItemChecks.BeginWorldStep();Call(host,"AdvancePersonalItems");Call(host,"AdvancePersonalItems");
                Require(updatesA+updatesB==previousUpdates,"stable online roles with no private instances perform no private item updates");
                Require((bool)Get(a,"Dirty"),"actual pickup marks archive resource change");
                Call(host,"Recall",profile.Id);NativeQuickItemChecks.Until(()=>{Call(host,"Update",0UL);return !((IDictionary)Get(host,"actors")).Contains(profile.Id);});
                int oldMode=Main.netMode;Main.netMode=0;
                try{Require((bool)Call(host,"Join",profile.Id),"saved resource profile truly reloads through Join/LoadBody");var restored=(Player)Call(host,"GetBody",profile.Id);Require(Wood(restored)==beforeA+9,"actual recall/save/rejoin retains collected quantity with no second share");}
                finally{Main.netMode=oldMode;}
                // This is a shared parent fixture profile (its wood150 backs
                // the existing disk-reader leaf). Return only this leaf's
                // second-role gain through the real authorized resource API.
                int woodSlot=Array.FindIndex(second.inventory,item=>item.type==ItemID.Wood);
                for(int i=0;i<11;i++)Require((bool)Call(host,"Transfer",((CompanionProfile)Get(b,"Profile")).Id,false,CompanionInventoryArea.Inventory,woodSlot,false),"fixture cleanup returns only the second role's collected share through native resource operations");
                Require(Wood(second)==beforeB,"independent parent persistence sample remains at its original quantity");
                Console.WriteLine("PASS cost sparse personal instances: exactly1+1 per step, same-step duplicate0, empty stable updates0; latest pickup/save retained.");
            }
            finally{Main.item[399]=original;sync.UnpatchAll(sync.Id);}
        }
    }
}
