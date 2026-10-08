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
                Require((long)Get(source,"epoch")>epoch && (long)Get(Get(source,"rolling"),"priorTick")==prior && (long)Get(Get(source,"segmented"),"relationVersion")==relation,"Normal packet retires publication epoch while preserving rolling and segment relation history.");
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
            SegmentedBoundaries(context,host,source,cache);
            NativeCombatObservationChecks.Save(host,new ObservationOptions());Main.netMode=0;
            Console.WriteLine("PASS SYNC23 real original serialization/dispatch: nonzero generation, ordinary history, forced instance, generation, same-token transform and large correction.");
        }
        private static void SegmentedBoundaries(object context,object host,object source,NpcPredictionCache cache)
        {
            foreach(var n in Main.npc)n.active=false;
            var target=Main.npc[2];target.SetDefaults(14);target.whoAmI=2;target.active=true;target.target=Main.myPlayer;target.spawnNeedsSyncing=false;target.position=new Vector2(800,900);target.velocity=Vector2.Zero;
            NativeCombatObservationChecks.Fresh(context,host);target.position.X+=2;NativeCombatObservationChecks.Fresh(context,host);
            var original=cache.Read(0);Require(original!=null && original[1].Vx==2,"Default Source observes directly assigned segmented motion.");
            Call(host,"Update",Main.GameUpdateCount);bool equivalent=ReferenceEquals(cache.Read(0),original);
            var before=target.position;target.position.X+=8;var packet=CombatNetworkFixture.Serialize(23,2);target.position=before;CombatNetworkFixture.Receive(packet);
            Require(cache.Read(0)==null && target.position.X==before.X+8,"Original .8 packet applies correction before R postfix invalidation.");
            Call(host,"Update",Main.GameUpdateCount);var corrected=cache.Read(0);
            Require(corrected!=null && corrected[0].Bounds.X==target.position.X && corrected[1].Vx==2 && !ReferenceEquals(corrected,original),"Same-time correction moves origin without inventing speed.");
            target.position.X+=3;NativeCombatObservationChecks.Fresh(context,host);var accelerated=cache.Read(0);
            Require(accelerated!=null && accelerated[1].Vx==3,"Next actual update measures changed speed from corrected baseline.");
            Require(equivalent,"Equivalent same-time Source request retains immutable trajectory.");
            target.width+=2;Call(host,"Update",Main.GameUpdateCount);var resized=cache.Read(0);
            Require(resized!=null && resized[0].Bounds.Width==target.width && !ReferenceEquals(resized,accelerated),"Same-time shape change recalculates result.");target.width-=2;
            cache.Release(0);cache.Demand(1,1,30);Call(source,"Prepare",resized.Identity,(long)Main.GameUpdateCount);var shortPath=cache.Read(1);
            cache.Demand(1,1,60);Call(source,"Prepare",resized.Identity,(long)Main.GameUpdateCount);var longer=cache.Read(1);
            Require(shortPath.Count==31 && longer.Count==61 && !ReferenceEquals(shortPath,longer),"Changed same-time consumer horizon cannot reuse incomplete immutable result.");cache.Release(1);
            target=(NPC)typeof(NPC).GetMethod("NewNPCInstanceInSlot",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{2,(byte)(target.generation+1)});
            target.SetDefaults(14);target.whoAmI=2;target.active=true;target.target=Main.myPlayer;target.position=new Vector2(810,900);target.velocity=new Vector2(2,0);NativeCombatObservationChecks.Fresh(context,host);var fallback=cache.Read(0);
            target.velocity=new Vector2(4,0);Call(host,"Update",Main.GameUpdateCount);var changedFallback=cache.Read(0);
            Require(fallback.Quality==JueMingR.Platform.Combat.PredictionQuality.LimitedObservation && changedFallback[1].Vx==4 && !ReferenceEquals(fallback,changedFallback),"Fresh limited observation rechecks actual same-time native velocity.");
            var worm=target;worm.active=false;var ordinary=Main.npc[3];ordinary.SetDefaults(2);ordinary.whoAmI=3;ordinary.active=true;ordinary.target=Main.myPlayer;ordinary.position=new Vector2(810,900);ordinary.velocity=new Vector2(1,0);
            NativeCombatObservationChecks.Fresh(context,host);var normal=cache.Read(0);Require(normal!=null && ReferenceEquals(normal.Identity.Token,ordinary),"Segmented to ordinary directly selects current Source target.");
            Call(source,"ObserveNpcReset",worm);Require(ReferenceEquals(cache.Read(0),normal),"Dormant worm reset cannot revoke new ordinary publication.");
            Call(source,"ObserveNpcQueryUpdate",2,true);Require(ReferenceEquals(cache.Read(0),normal),"Dormant discontinuity cannot clear new ordinary history.");
            Call(source,"ObserveNpcQueryUpdate",3,true);Require(cache.Read(0)==null,"Current dependency still revokes invalid publication.");
            Console.WriteLine("PASS CLOSEOUT default Source direct motion/equivalence, actual .8 correction/changed speed, shape and dormant retirement.");
        }
    }
}
