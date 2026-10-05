using System;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatSyncChecks
    {
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");foreach(var n in Main.npc)n.active=false;
            Main.netMode=1;Main.LocalPlayer.position=new Vector2(600,700);Main.dayTime=false;
            for(int i=0;i<Main.maxPlayers;i++)if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};
            var target=(NPC)typeof(NPC).GetMethod("NewNPCInstanceInSlot",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{2,(byte)7});target.SetDefaults(2);target.whoAmI=2;target.active=true;target.target=Main.myPlayer;target.position=new Vector2(800,900);target.velocity=new Vector2(1,0);target.spawnNeedsSyncing=false;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true));NativeCombatObservationChecks.Fresh(context,host);Require(cache.Read(0)!=null,"Real Host starts a published path before native packet.");
            var before=target.position;target.position.X+=600;var packet=CombatNetworkFixture.Serialize(23,2);target.position=before;
            Require(packet[3]==2 && packet[4]==7,"Original serializer has separate byte slot/generation.");CombatNetworkFixture.Receive(packet);
            Console.WriteLine("SYNC23 native="+target.position+" generation="+target.generation+" cached="+(cache.Read(0)!=null));
            Require(target.position.X==1400 && cache.Read(0)==null,"Actual nonzero-generation packet large correction retires old published geometry.");
            foreach(int type in new[]{2,14})
            {
                target=(NPC)typeof(NPC).GetMethod("NewNPCInstanceInSlot",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{2,(byte)7});
                target.SetDefaults(type);target.active=true;target.target=Main.myPlayer;target.position=new Vector2(800,900);target.velocity=new Vector2(1,0);target.spawnNeedsSyncing=false;
                NativeCombatObservationChecks.Fresh(context,host);target.position.X++;NativeCombatObservationChecks.Fresh(context,host);
                var published=cache.Read(0);Require(published!=null,"Established ordinary/segment history type="+type);
                long epoch=(long)Get(source,"epoch"),prior=(long)Get(Get(source,"rolling"),"priorTick"),relation=(long)Get(Get(source,"segmented"),"relationVersion");
                var token=target;before=target.position;target.position.X+=2;target.velocity.X=2;packet=CombatNetworkFixture.Serialize(23,2);target.position=before;target.velocity.X=1;CombatNetworkFixture.Receive(packet);
                Require(ReferenceEquals(Main.npc[2],token) && target.generation==7 && target.type==type && cache.Read(0)==null,"Ordinary sync revokes published geometry without replacing identity.");
                Require((long)Get(source,"epoch")==epoch && (long)Get(Get(source,"rolling"),"priorTick")==prior && (long)Get(Get(source,"segmented"),"relationVersion")==relation,"Ordinary byte-generation packet preserves rolling and segment relation history.");
                NativeCombatObservationChecks.Fresh(context,host);var next=cache.Read(0);Require(next!=null && (type!=14 || next.Quality==JueMingR.Platform.Combat.PredictionQuality.ObservedTrend),"Next completed sample retains measured segmented trend after normal sync.");
                target.position.X+=600;packet=CombatNetworkFixture.Serialize(23,2);target.position.X-=600;CombatNetworkFixture.Receive(packet);Require(cache.Read(0)==null && (long)Get(source,"epoch")>epoch || type==14 && cache.Read(0)==null && (long)Get(Get(source,"segmented"),"relationVersion")>relation,"Same-identity large correction retires history separately.");
                NativeCombatObservationChecks.Fresh(context,host);Require(cache.Read(0)!=null,"Large correction can resample real current target.");
                Console.WriteLine("SYNC23 ordinary/correction type="+type+" tokenSame="+ReferenceEquals(Main.npc[2],token));
                foreach(int change in new[]{0,1,2})
                {
                    token=Main.npc[2];NativeCombatObservationChecks.Fresh(context,host);var oldIdentity=cache.Read(0).Identity;
                    byte[] changed;
                    if(change==0){token.spawnNeedsSyncing=true;changed=CombatNetworkFixture.Serialize(23,2);token.spawnNeedsSyncing=false;}
                    else if(change==1){changed=CombatNetworkFixture.Serialize(23,2);changed[4]=(byte)(token.generation+1);}
                    else{int oldNet=token.netID;token.netID=type==14?13:6;changed=CombatNetworkFixture.Serialize(23,2);token.netID=oldNet;}
                    CombatNetworkFixture.Receive(changed);
                    Require(cache.Read(0)==null && (change==2?ReferenceEquals(Main.npc[2],token):!ReferenceEquals(Main.npc[2],token)),"Real forced instance / generation / same-token netID transformation retires cached identity: "+change);
                    Require(!oldIdentity.Equals(new JueMingR.Platform.Combat.NpcIdentity(oldIdentity.Session,Main.npc[2],2,Main.npc[2].generation,Main.npc[2].type,Main.npc[2].netID)),"Changed identity cannot continue old target.");
                    NativeCombatObservationChecks.Fresh(context,host);Require(cache.Read(0)!=null,"Reacquire actual changed target.");
                }
            }
            NativeCombatObservationChecks.Save(host,new ObservationOptions());Main.netMode=0;
            Console.WriteLine("PASS SYNC23 real original serialization/dispatch: nonzero generation, ordinary history, forced instance, generation, same-token transform and large correction.");
        }
    }
}
