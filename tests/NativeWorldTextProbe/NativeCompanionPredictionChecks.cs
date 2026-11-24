using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.GameContent;
using System.Net;
using System.Net.Sockets;
using Terraria.Net.Sockets;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionPredictionChecks
    {
        private static List<byte[]> primaryFrames;
        internal static void Run(object host,Player first,Guid other)
        {
            Require((bool)Call(host,"Join",other),"second real saved actor joins prediction counterexample");
            var second=(Player)Call(host,"GetBody",other);var a=Call(host,"Owned",first);var b=Call(host,"Owned",second);
            var humanPosition=Main.LocalPlayer.position;Main.LocalPlayer.position=new Vector2(680,900);
            var framesA=new List<byte[]>();var framesB=new List<byte[]>();
            primaryFrames=new List<byte[]>();
            var type=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionConnection");
            Func<List<byte[]>,object> connection=frames=>Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{new Action<byte[]>(p=>frames.Add(p)),new Action(()=>{})},null);
            var ca=connection(framesA);var cb=connection(framesB);Set(a,"Connection",ca);Set(b,"Connection",cb);
            var isolation=new Harmony("JueMingR.Tests.CompanionPredictionOutlets");
            isolation.Patch(AccessTools.Method(typeof(NetMessage),"SendPacketToServer"),prefix:new HarmonyMethod(typeof(NativeCompanionPredictionChecks),nameof(SkipPrimarySocket)));
            var npc=Main.npc[0];npc.SetDefaults(NPCID.Zombie);npc.active=true;npc.life=npc.lifeMax=1000;npc.position=first.position+new Vector2(80,0);npc.defense=0;Array.Clear(npc.immune,0,npc.immune.Length);
            var globals=(IList)AccessTools.Field(typeof(NPC),"pendingDamages").GetValue(null);var previous=globals.Cast<object>().ToArray();globals.Clear();
            var oldBuffer=NetMessage.buffer[256];NetMessage.buffer[256]=new MessageBuffer{whoAmI=256};
            var oldSocket=Netplay.Connection.Socket;var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
            var client=new TcpClient();client.Connect(IPAddress.Loopback,((IPEndPoint)listener.LocalEndpoint).Port);var accepted=listener.AcceptTcpClient();
            Netplay.Connection.Socket=new TcpSocket(client);
            var apply=typeof(Player).GetMethod("ApplyDamageToNPC",new[]{typeof(NPC),typeof(int),typeof(float),typeof(int),typeof(bool),typeof(PlayerNPCHitSource)});
            var hooks=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionHooks");
            Func<object,IDisposable> enter=actor=>(IDisposable)hooks.GetMethod("Enter",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{actor,npc.Center,false});
            Main.netMode=1;bool meter=first.accDreamCatcher;
            try
            {
                // Real native bytes enter the AI observer through its ordered
                // transport/barrier path; a later old primary snapshot cannot
                // alter that separate table or the human native prediction.
                npc.life=800;NetMessage.SendData(23,-1,-1,null,0);var newer=primaryFrames.Last(p=>p[2]==23);
                Call(ca,"ReceiveRelay",newer);Call(host,"PumpConnection",a);Call(host,"FlushSourceRequests");
                Call(ca,"ReceiveRelay",new byte[]{3,0,154});Call(host,"PumpConnection",a);
                Call(ca,"ReceiveRelay",newer);Call(host,"PumpConnection",a);
                var observed=(NPC[])Get(host,"observedNpcs");Require(observed[0].life==800 && !ReferenceEquals(observed[0],npc),"AI newer native 23 owns a separate shared observation object");
                npc.life=900;NetMessage.SendData(23,-1,-1,null,0);var older=primaryFrames.Last(p=>p[2]==23).Skip(2).ToArray();npc.life=1000;
                var primary=new MessageBuffer{whoAmI=256};Buffer.BlockCopy(older,0,primary.readBuffer,0,older.Length);primary.ResetReader();int primaryType;primary.GetData(0,older.Length,out primaryType);
                Require(npc.life==900 && observed[0].life==800,"old primary 23 remains human-native and cannot overwrite newer AI observation");
                first.inventory[0].SetDefaults(39);first.inventory[54].SetDefaults(40);first.inventory[54].stack=20;first.selectedItemState.Select(0);first.itemAnimation=first.itemTime=0;first.channel=false;
                Set(a,"NextPublish",0U);Call(host,"Publish",a);framesA.Clear();framesB.Clear();
                for(int i=0;i<30;i++){NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",(ulong)i);first.Update(first.whoAmI);}
                Require(first.inventory[54].stack<20 && framesA.Any(p=>p.Length>=11 && p[2]==5 && p[3]==first.whoAmI && BitConverter.ToInt16(p,4)==54 && BitConverter.ToInt16(p,6)<20),"actual native ammo consumption publishes its changed native slot on its own stream");
                int wood=first.inventory.Where(i=>i.type==ItemID.Wood).Sum(i=>i.stack);var drop=Main.item[399]=new WorldItem(new Item(ItemID.Wood));drop.stack=7;drop.whoAmI=399;drop.playerIndexTheItemIsReservedFor=first.whoAmI;drop.grabDelayTime=0;drop.position=first.position;
                Call(first,"GrabItems",first.whoAmI);Require(!drop.active && first.inventory.Where(i=>i.type==ItemID.Wood).Sum(i=>i.stack)==wood+7,"actual native reserved world item enters this actor inventory once");
                Set(a,"NextPublish",0U);Call(host,"Publish",a);
                Require(framesA.Any(p=>p.Length>=11 && p[2]==5 && p[3]==first.whoAmI && BitConverter.ToInt16(p,9)==ItemID.Wood) && !framesB.Any(p=>p[2]==5),"actual pickup publishes correct player/slot/type only on owning connection");
                apply.Invoke(Main.LocalPlayer,new object[]{npc,10,0f,1,false,new PlayerNPCHitSource(0,0,"companion-native-check")});
                Require(globals.Count==1,"human native strike creates its original global pending object");var humanPending=globals[0];int canonical=npc.life;
                var arrow=Main.projectile.First(q=>q.active && q.owner==first.whoAmI && q.type==1 && Call(host,"ProjectileActor",q)!=null);
                arrow.position=npc.position;arrow.velocity=Vector2.Zero;arrow.damage=30;arrow.penetrate=3;Array.Clear(npc.immune,0,npc.immune.Length);
                // The preceding real Player.Update resets equipment-derived
                // flags. Establish this independent return-value consumer at
                // its actual hit stage, rather than before those updates.
                bool meterAfterUpdates=first.accDreamCatcher;first.accDreamCatcher=true;int dps=first.dpsDamage;
                arrow.Damage();
                Console.WriteLine("COMPANION hit consumer meterAfterUpdates="+meterAfterUpdates+" meterAtHit="+first.accDreamCatcher+" dpsBefore="+dps+" dpsAfter="+first.dpsDamage+" own28="+framesA.Count(p=>p[2]==28));
                Require(first.dpsDamage>dps && framesA.Any(p=>p[2]==28),"real projectile Strike return reaches native DPS OnHit consumer and own byte stream");
                Require(ReferenceEquals(Main.npc[0],npc) && npc.life==canonical,"AI native hit restores canonical NPC identity and health after synchronous consumers");
                using(enter(b))apply.Invoke(second,new object[]{observed[0],20,0f,1,false,new PlayerNPCHitSource(0,0,"companion-native-check")});
                Require(Count(a)==1 && Count(b)==1 && globals.Count==1 && ReferenceEquals(globals[0],humanPending),"two real AI strikes own separate native pending objects without modifying human queue");
                Call(host,"StrikeAcknowledged",b);Require(Count(a)==1 && Count(b)==0 && ReferenceEquals(globals[0],humanPending),"reverse stream ACK releases B only and leaves A/human intact");
                // Serialize a real native 23 baseline on A's existing byte outlet.
                int oldLife=npc.life;npc.life=800;NetMessage.SendData(23,-1,-1,null,0);npc.life=oldLife;
                var frame=primaryFrames.Last(p=>p[2]==23);var packet=frame.Skip(2).ToArray();
                Call(host,"ObservePredictedNpc",a,packet,0,packet.Length);Call(host,"ObservePredictedNpc",b,packet,0,packet.Length);
                var viewA=(NPC)Get(((IDictionary)Get(a,"NpcViews"))[0],"Body");var viewB=(NPC)Get(((IDictionary)Get(b,"NpcViews"))[0],"Body");
                Require(viewA.life<800 && viewB.life==800,"own 23 baseline subtracts only its still unacknowledged native prediction");
                var hit=framesA.First(p=>p[2]==28).Skip(2).ToArray();var decoder=new MessageBuffer{whoAmI=256};Buffer.BlockCopy(hit,0,decoder.readBuffer,0,hit.Length);decoder.ResetReader();int message;
                int life=npc.life;decoder.GetData(0,hit.Length,out message);Require(npc.life<life,"primary original external 28 executes immediately while human native pending exists");
                Call(host,"StrikeAcknowledged",a);Require(Count(a)==0 && globals.Count==1 && ReferenceEquals(globals[0],humanPending),"AI ACK never dequeues human pending");
                AccessTools.Method(typeof(NPC),"AckDamage").Invoke(null,null);Require(globals.Count==0,"original primary ACK releases original human pending");
                SourceWindows(host,a,b,ca,cb,first,second);
                Console.WriteLine("PASS companions prediction: separate AI-new/primary-old native observations, real ammo/pickup slot bytes, actual Projectile OnHit return/DPS, scoped NPC identity, two-flow reverse native ACK and independent own-23 correction; primary 28 preserved.");
            }
            finally
            {
                Main.netMode=0;Main.LocalPlayer.position=humanPosition;first.accDreamCatcher=meter;globals.Clear();foreach(var item in previous)globals.Add(item);npc.active=false;primaryFrames=null;
                Netplay.Connection.Socket=oldSocket;NetMessage.buffer[256]=oldBuffer;client.Close();accepted.Close();listener.Stop();
                Set(a,"Connection",null);Set(b,"Connection",null);((IDisposable)ca).Dispose();((IDisposable)cb).Dispose();
                isolation.UnpatchAll(isolation.Id);Call(host,"Recall",other);
                NativeQuickItemChecks.Until(()=>{Call(host,"Update",0UL);return !((IDictionary)Get(host,"actors")).Contains(other);});
            }
        }
        private static int Count(object actor){return (int)Get(Get(actor,"PendingStrikes"),"Count");}
        private static void SourceWindows(object host,object a,object b,object ca,object cb,Player first,Player second)
        {
            // Complete the earlier full-NPC enumeration before another barrier.
            Call(ca,"ReceiveRelay",new byte[]{3,0,49});Call(host,"PumpConnection",a);
            var setup=AccessTools.Method(typeof(Projectile),"NewProjectileSetup");
            var key1=new ProjectileKey(255,42,1);var key2=new ProjectileKey(255,42,2);var key3=new ProjectileKey(255,42,3);var unrelated=new ProjectileKey(255,99,88);
            var template=(Projectile)setup.Invoke(null,new object[]{key1});template.SetDefaults(44);template.owner=255;template.active=true;template.damage=10;
            var oldSecond=second.position;second.position=new Vector2(1800,900);
            Func<ProjectileKey,Vector2,byte[]> packet=(key,point)=>
            {var oldKey=template.key;var oldPosition=template.position;template.key=key;template.position=point;try{NetMessage.SendData(27,-1,-1,null,template.whoAmI);return primaryFrames.Last(p=>p[2]==27);}finally{template.key=oldKey;template.position=oldPosition;}};
            Action<object,object,byte[]> receive=(connection,actor,frame)=>{Call(connection,"ReceiveRelay",frame);Call(host,"PumpConnection",actor);};
            Action<object,object> pong=(connection,actor)=>{Call(host,"FlushSourceRequests");receive(connection,actor,new byte[]{3,0,154});};
            try
            {
                var firstFrame=packet(key1,first.Center+new Vector2(30,0));receive(ca,a,firstFrame);pong(ca,a);receive(ca,a,firstFrame);
                Projectile value;Require(key1.TryGetActive(out value),"first observer imports real native projectile baseline");
                var next=packet(key2,second.Center);receive(cb,b,next);
                Require(key2.TryGetActive(out value) && !Main.projectile.Any(p=>p.active && (int)p.key==(int)key1),"single birth27 for new generation from different observer imports immediately and retires old identity before lookup reuse");
                var returning=packet(key2,first.Center+new Vector2(30,0));receive(ca,a,returning);Call(host,"FlushSourceRequests");
                NetMessage.SendData(29,-1,-1,null,(int)key2,value.Center.X,value.Center.Y);var kill=primaryFrames.Last(p=>p[2]==29).Skip(2).ToArray();
                var primary=new MessageBuffer{whoAmI=256};Buffer.BlockCopy(kill,0,primary.readBuffer,0,kill.Length);primary.ResetReader();int type;primary.GetData(0,kill.Length,out type);
                Require(!key2.TryGetActive(out value),"primary native 29 terminates enemy projectile during pending observer barrier");
                var other=(Projectile)setup.Invoke(null,new object[]{unrelated});other.SetDefaults(1);other.active=true;other.owner=255;
                receive(ca,a,new byte[]{3,0,154});receive(ca,a,returning);
                Require(!key2.TryGetActive(out value) && unrelated.TryGetActive(out value),"late dead-generation 27 cannot resurrect after pool reuse or kill unrelated identity");
                receive(ca,a,packet(key3,first.Center+new Vector2(30,0)));Require(key3.TryGetActive(out value),"next live generation imports after terminal tombstone");
                // The source is now intentionally closed before its pending NPC
                // barrier responds. The surviving stream must acquire a fresh
                // baseline rather than wait forever on the detached object.
                var npc=Main.npc[0];var old=npc.position;npc.position=second.position;NetMessage.SendData(23,-1,-1,null,0);var moving=primaryFrames.Last(p=>p[2]==23);npc.position=old;
                receive(cb,b,moving);Call(host,"FlushSourceRequests");Call(host,"Detach",b);Call(host,"FlushSourceRequests");
                var primaryPong=new MessageBuffer{whoAmI=256};primaryPong.readBuffer[0]=154;primaryPong.ResetReader();primaryPong.GetData(0,1,out type);
                receive(ca,a,moving);pong(ca,a);receive(ca,a,moving);
                Require(((NPC[])Get(host,"observedNpcs"))[0].position.X==second.position.X,"detached pending NPC source recovers via surviving stream and fresh 23");
                Console.WriteLine("PASS companion sources: new-generation observer handoff, primary29 during barrier, late27 after pool reuse, unrelated projectile survival and detached pending NPC recovery.");
            }
            finally
            {
                second.position=oldSecond;foreach(var key in new[]{key1,key2,key3,unrelated}){Projectile value;if(key.TryGetActive(out value))value.Kill();}
            }
        }
        private static bool SkipPrimarySocket(byte[] data){int length=data[0]|data[1]<<8;var packet=new byte[length];Buffer.BlockCopy(data,0,packet,0,length);primaryFrames.Add(packet);return false;}
    }
}
