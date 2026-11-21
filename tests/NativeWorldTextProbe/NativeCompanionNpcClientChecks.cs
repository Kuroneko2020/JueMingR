using System;
using System.Linq;
using HarmonyLib;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionNpcStateChecks
    {
        private static NPC[] PlainNpcTable()
        {return Enumerable.Range(0,Main.maxNPCs).Select(i=>new NPC{whoAmI=i,active=false}).ToArray();}
        private static void PlainNpc(object host,NPC[] table,Action action,int? player=null)
        {
            // One original-client reference window. It must NOT pass through
            // the product's observation decode/reset/static-matrix owner or
            // Prepare methods. Only this NPC table and native consumers run;
            // exact Hook context/table/player/gravity state is restored.
            var hooks=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionHooks");var owner=AccessTools.Field(hooks,"host");var scope=AccessTools.Field(hooks,"current");var oldOwner=owner.GetValue(null);var oldScope=scope.GetValue(null);var npcs=Main.npc;int mode=Main.netMode,who=Main.myPlayer;bool dedicated=Main.dedServ;var gravity=AccessTools.Field(typeof(NPC),"gravity");var oldGravity=gravity.GetValue(null);
            bool stair=Collision.stair,fall=Collision.stairFall,honey=Collision.honey,shimmer=Collision.shimmer,sloping=Collision.sloping,mine=Collision.landMine,up=Collision.up,down=Collision.down;
            try{owner.SetValue(null,null);scope.SetValue(null,null);Main.npc=table;Main.netMode=1;Main.dedServ=true;if(player.HasValue)Main.myPlayer=player.Value;action();}
            finally{Main.npc=npcs;Main.netMode=mode;Main.myPlayer=who;Main.dedServ=dedicated;owner.SetValue(null,oldOwner);scope.SetValue(null,oldScope);gravity.SetValue(null,oldGravity);Collision.stair=stair;Collision.stairFall=fall;Collision.honey=honey;Collision.shimmer=shimmer;Collision.sloping=sloping;Collision.landMine=mine;Collision.up=up;Collision.down=down;}
        }
        private static void PlainNpcPacket(object host,NPC[] table,byte[] packet)
        {
            var restore=PreserveNativeSlotData();
            try{PlainNpc(host,table,()=>{var decoder=new MessageBuffer{whoAmI=256};Buffer.BlockCopy(packet,2,decoder.readBuffer,0,packet.Length-2);decoder.ResetReader();int id;decoder.GetData(0,packet.Length-2,out id);Require(id==packet[2],"plain original decoder consumes the actual same packet type");});}
            finally{restore();}
        }
    }
}
