using System;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Utilities;
using Terraria.Net.Sockets;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionNpcStateChecks
    {
        private static NPC[] shapeControl;private static bool captureShape;private static Vector2 plainShapePosition,productShapePosition,plainShapeSize,productShapeSize;private static float plainScale,productScale;private static int plainShapeCalls,productShapeCalls;
        private static void PlainShapeAfter(NPC __instance)
        {if(captureShape && __instance.type==359 && ReferenceEquals(Main.npc,shapeControl)){plainShapePosition=__instance.position;plainShapeSize=__instance.Size;plainScale=__instance.scale;plainShapeCalls++;}}
        private static void ProductShapeAfter(NPC npc,bool decoded)
        {if(captureShape && !decoded && npc.type==359){productShapePosition=npc.position;productShapeSize=npc.Size;productScale=npc.scale;productShapeCalls++;}}
        internal static void LocalState(object host,Player first,Player second,Func<int,byte[]> serialize,Action<byte[]> deliver,bool snail=false)
        {
            var human=Main.LocalPlayer;var players=new[]{human,first,second};var positions=players.Select(p=>p.position).ToArray();var original=Main.npc[Slot];var clients=Netplay.Clients;var buffers=NetMessage.buffer;int mode=Main.netMode;bool dedicated=Main.dedServ;var random=Main.rand;var restore=PreserveNativeSlotData();var plain=PlainNpcTable();var patch=new Harmony("JueMingR.Tests.CompanionLocalClientState");
            try
            {
                Call(host,"ClearWorldSources");human.position=new Vector2(1000,918);first.position=new Vector2(1050,918);second.position=new Vector2(1250,918);
                // Normal source-side network owners are absent from the tiny
                // component fixture. Populate disconnected server peers so the
                // unmodified network tail can run; no SendData is suppressed.
                Netplay.Clients=new RemoteClient[256];for(int i=0;i<256;i++)Netplay.Clients[i]=new RemoteClient{Id=i,Socket=new TcpSocket()};NetMessage.buffer=new MessageBuffer[257];for(int i=0;i<257;i++)NetMessage.buffer[i]=new MessageBuffer{whoAmI=i};
                Main.netMode=2;Main.dedServ=true;var source=Spawn(snail?359:690,0);source.position.Y=960-source.height;human.Center=source.Center+new Vector2(-20,0);source.TargetClosest();Main.rand=new UnifiedRandom(1);source.UpdateNPC(Slot);if(!snail)source.UpdateNPC(Slot);Main.netMode=1;Main.dedServ=dedicated;
                byte[] packet=serialize(Slot);PlainNpcPacket(host,plain,packet);deliver(packet);var observed=((NPC[])Get(host,"observedNpcs"))[Slot];var reference=plain[Slot];
                Require(reference.Size==observed.Size && reference.scale==observed.scale && reference.position==observed.position && reference.noTileCollide==observed.noTileCollide && reference.immortal==observed.immortal && reference.dontTakeDamage==observed.dontTakeDamage,"actual same23 first decoder phase matches ordinary defaults rather than unpublished source fields");
                if(!snail)
                {
                    Require(source.ai[0]==1 && source.noTileCollide && source.velocity.Y<0 && !reference.noTileCollide,"real source has already jumped while the first plain/product receiver correctly retains its own decoded defaults before the next action");int jumps=0;float previous=reference.velocity.Y;long stamp=(long)Call(host,"NpcSampleTick",observed),revision=(long)Call(host,"NpcObservationRevision",observed);
                    for(int action=0;action<100;action++)
                    {
                        NativeQuickItemChecks.BeginWorldStep();PlainNpc(host,plain,()=>reference.UpdateNPC(Slot));Call(host,"AdvanceNpcObservation");
                        if(reference.velocity.Y<0 && previous>=0)jumps++;previous=reference.velocity.Y;
                        if(reference.position!=observed.position || reference.velocity!=observed.velocity || reference.noTileCollide!=observed.noTileCollide)Console.WriteLine("NPC STATUE DIFF action="+action+" ordinary="+reference.position+"/"+reference.velocity+"/tile="+reference.noTileCollide+" ai="+String.Join(",",reference.ai)+" product="+observed.position+"/"+observed.velocity+"/tile="+observed.noTileCollide+" ai="+String.Join(",",observed.ai));
                        Require(reference.position==observed.position && reference.velocity==observed.velocity && reference.noTileCollide==observed.noTileCollide && reference.ai[2]==observed.ai[2] && reference.ai[3]==observed.ai[3] && reference.immortal==observed.immortal && reference.dontTakeDamage==observed.dontTakeDamage,"next actual AI126 local movement/clock/gravity/collision phase matches the ordinary receiver, including later jumps");
                        var position=observed.position;Call(host,"AdvanceNpcObservation");Require(observed.position==position,"second consumer in the same world step does not add another AI126 action or physics tail");
                    }
                    Require(jumps>=2 && (long)Call(host,"NpcSampleTick",observed)==stamp && (long)Call(host,"NpcObservationRevision",observed)==revision && (bool)Call(host,"ObservedNpcCombatReady",observed),"ordinary local jumps recur from the available baseline without rebasing it or blanket-blocking combat");Console.WriteLine("NPC STATUE PARITY sourcePublishedJump=True firstDecodeOwnDefaults=True localActions=100 laterJumps="+jumps+" exactMotion/NoTile/Clocks/Immunity=True sameGUCOnce=True rawSampleUnchanged=True singleAvailableBaseline=True notServerNo23Claim=True");
                }
                else
                {
                    Require(source.ai[3]!=0 && reference.width==12 && (int)(12*source.ai[3])!=12,"native server random scale publication precedes its shape application; this representative has a real dimension change");shapeControl=plain;patch.Patch(AccessTools.Method(typeof(NPC),"AI"),postfix:new HarmonyMethod(typeof(NativeCompanionNpcStateChecks),nameof(PlainShapeAfter)));patch.Patch(AccessTools.Method(host.GetType(),"PrepareNpcObservationState"),postfix:new HarmonyMethod(typeof(NativeCompanionNpcStateChecks),nameof(ProductShapeAfter)));
                    Action local=()=>{plainShapeCalls=productShapeCalls=0;captureShape=true;try{NativeQuickItemChecks.BeginWorldStep();PlainNpc(host,plain,()=>plain[Slot].UpdateNPC(Slot));Call(host,"AdvanceNpcObservation");}finally{captureShape=false;}Require(plainShapeCalls==1 && productShapeCalls==1 && plainShapePosition==productShapePosition && plainShapeSize==productShapeSize && plainScale==productScale,"same local shape-producing phase applies native359 size and exact position correction once, not source hidden history or repeated defaults");};
                    local();Require(observed.width==plain[Slot].width && observed.width==(int)(12*source.ai[3]),"first client action really applies the published scale");
                    Main.netMode=2;Main.dedServ=true;source.UpdateNPC(Slot);Main.netMode=1;Main.dedServ=dedicated;packet=serialize(Slot);PlainNpcPacket(host,plain,packet);deliver(packet);reference=plain[Slot];Require(reference.Size==observed.Size && reference.position==observed.position,"same-form synchronization preserves each client's already applied shape");local();
                    PlainNpcPacket(host,plain,packet);deliver(packet);Require(reference.Size==observed.Size && reference.position==observed.position,"repeat same-form23 does not restore defaults or perform an extra shape shift");local();
                    byte old=source.generation;Main.netMode=2;Main.dedServ=true;source=Spawn(359,0);source.position.Y=960-source.height;Main.rand=new UnifiedRandom(1);source.UpdateNPC(Slot);Main.netMode=1;Main.dedServ=dedicated;Require(source.generation!=old,"actual server new instance assigns the next generation");packet=serialize(Slot);var before=reference;PlainNpcPacket(host,plain,packet);deliver(packet);reference=plain[Slot];observed=((NPC[])Get(host,"observedNpcs"))[Slot];Require(!ReferenceEquals(before,reference) && reference.width==12 && observed.width==12,"new generation rebuilds both native defaults before its first local form application");local();Console.WriteLine("NPC SNAIL PARITY nativeScale="+source.ai[3]+" firstDecode12=True firstLocalShapeAndPosition=True sameForm23AndRepeatNoExtraShift=True newGenerationDefaultsThenApply=True shapePhaseOnly=True noFullCritterMotionClaim=True");
                }
            }
            finally{captureShape=false;shapeControl=null;patch.UnpatchAll(patch.Id);Main.netMode=mode;Main.dedServ=dedicated;Main.rand=random;Main.npc[Slot]=original;Call(host,"ClearWorldSources");Netplay.Clients=clients;NetMessage.buffer=buffers;for(int i=0;i<players.Length;i++)players[i].position=positions[i];restore();}
        }
    }
}
