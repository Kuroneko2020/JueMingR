using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
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
            int previousNetMode=Main.netMode;var previousSections=Main.sectionManager;
            try
            {
            if(!(bool)Get(host,"PathAccess"))NativeHiddenEntryChecks.RevealForComposition(context);
            // The isolated harness supplied every tile in its fixed world.
            // This client packet oracle starts from received terrain, so give
            // that world the original section owner's loaded qualification.
            Main.netMode=1;Main.sectionManager=new WorldSections(Netplay.GetSectionX(Main.maxTilesX-1)+1,Netplay.GetSectionY(Main.maxTilesY-1)+1);Main.sectionManager.SetAllSectionsLoaded();
            Main.LocalPlayer.position=new Vector2(600,700);Main.dayTime=false;
            for(int i=0;i<Main.maxPlayers;i++)if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};
            var target=(NPC)typeof(NPC).GetMethod("NewNPCInstanceInSlot",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{2,(byte)7});target.SetDefaults(2);target.whoAmI=2;target.active=true;target.target=Main.myPlayer;target.position=new Vector2(800,900);target.velocity=new Vector2(1,0);target.spawnNeedsSyncing=false;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true));NativeCombatObservationChecks.Fresh(context,host);
            var selection=Get(host,"Selection");
            Console.WriteLine("SYNC PREREQUISITES pathAccess="+Get(host,"PathAccess")+" path="+Get(host,"Path")+" settingsReady="+Get(Get(host,"Settings"),"Ready")+" required="+cache.Required+" hasTarget="+Get(selection,"HasTarget")+" netMode="+Main.netMode+" sections="+(Main.sectionManager!=null)+" playerTileLoaded="+(Main.sectionManager!=null && Main.sectionManager.TileLoaded((int)Main.LocalPlayer.Center.X/16,(int)Main.LocalPlayer.Center.Y/16))+" targetTileLoaded="+(Main.sectionManager!=null && Main.sectionManager.TileLoaded((int)target.Center.X/16,(int)target.Center.Y/16)));
            // Inspect stored values only: Read rejects absent demand/result or
            // a prefix that cannot satisfy this reader's unchanged minimum.
            var demands=GetOptional(cache,"demands") as int[];var minimums=GetOptional(cache,"minimums") as int[];
            var raw=GetOptional(cache,"result") as JueMingR.Platform.Combat.NpcTrajectory;var outcomeIdentity=GetOptional(source,"outcomeIdentity");var work=GetOptional(host,"PredictionWork") as CombatPredictionWork;
            Console.WriteLine("SYNC SOURCE FACTS demand0="+(demands!=null && demands.Length>0?demands[0]:-1)+" minimum0="+(minimums!=null && minimums.Length>0?minimums[0]:-1)+" raw="+(raw!=null)+" rawCount="+(raw==null?-1:raw.Count)+" rawStop="+(raw==null?"none":raw.Stop.ToString())+" rawSample="+(raw==null?-1:raw.SampleTick)+" guc="+Main.GameUpdateCount+" outcomeTick="+(GetOptional(source,"outcomeTick")??"absent")+" outcomeStop="+(GetOptional(source,"outcomeStop")??"absent")+" outcomeLayer="+(GetOptional(source,"outcomeLayer")??"absent")+" outcomeFuture="+(GetOptional(source,"outcomeFuture")??"absent")+" identityMatch="+(outcomeIdentity!=null && outcomeIdentity.Equals(Get(selection,"Target")))+" native="+(GetOptional(source,"Native")!=null)+" work="+(work!=null)+" budgetMs="+(work==null?-1:work.UsedMilliseconds)+" exceeded="+(work!=null && work.Exceeded));
            // Packet invalidation requires an actual prior publication, not a
            // cold first-GUC latency promise. Only the measured zero-future
            // budget pause may wait through the existing display cadence.
            bool coldBudgetMiss=raw==null && work!=null && work.Exceeded && demands!=null && demands.Length>0 && demands[0]>0 && minimums!=null && minimums.Length>0 && minimums[0]==1 && GetOptional(source,"Native")==null && (long)Get(source,"outcomeTick")==Main.GameUpdateCount && (int)Get(source,"outcomeFuture")==0 && object.Equals(Get(source,"outcomeLayer"),JueMingR.Platform.Combat.PredictionFailureLayer.None) && object.Equals(Get(source,"outcomeStop"),JueMingR.Platform.Combat.PredictionStop.InvalidState) && outcomeIdentity!=null && outcomeIdentity.Equals(Get(selection,"Target"));
            if(coldBudgetMiss)
            {
                int limit=(int)Get(host,"DisplayRefreshUpdates")*3,advanced=0;uint firstGuc=Main.GameUpdateCount;string waitStop="limit";var expectedIdentity=Get(selection,"Target");
                while(advanced<limit && cache.Read(0)==null)
                {
                    NativeCombatObservationChecks.Fresh(context,host);advanced++;var publication=cache.Read(0);var currentRaw=GetOptional(cache,"result") as JueMingR.Platform.Combat.NpcTrajectory;
                    long outcomeAt=(long)Get(source,"outcomeTick");int future=(int)Get(source,"outcomeFuture");var stop=Get(source,"outcomeStop");var layer=Get(source,"outcomeLayer");var identity=Get(selection,"Target");bool sameIdentity=expectedIdentity.Equals(identity),sameOutcome=object.Equals(Get(source,"outcomeIdentity"),identity);
                    Console.WriteLine("SYNC COLD PUBLICATION advanced="+advanced+" guc="+Main.GameUpdateCount+" optionalTick="+Get(host,"optionalTick")+" published="+(publication!=null)+" rawCount="+(currentRaw==null?-1:currentRaw.Count)+" rawStop="+(currentRaw==null?"none":currentRaw.Stop.ToString())+" outcomeTick="+outcomeAt+" outcomeStop="+stop+" outcomeLayer="+layer+" outcomeFuture="+future+" sameIdentity="+sameIdentity+" sameOutcome="+sameOutcome+" budgetMs="+work.UsedMilliseconds+" exceeded="+work.Exceeded);
                    if(!sameIdentity || outcomeAt==Main.GameUpdateCount && !sameOutcome){waitStop="identity-changed";Require(false,"Sync first-publication identity changed; another target's path cannot establish this packet oracle.");break;}
                    if(publication!=null){waitStop="published";break;}
                    if(outcomeAt==Main.GameUpdateCount && !(currentRaw==null && future==0 && object.Equals(layer,JueMingR.Platform.Combat.PredictionFailureLayer.None) && object.Equals(stop,JueMingR.Platform.Combat.PredictionStop.InvalidState) && work.Exceeded)){waitStop="not-qualified-budget-miss";break;}
                }
                Console.WriteLine("SYNC COLD PUBLICATION END firstGuc="+firstGuc+" finalGuc="+Main.GameUpdateCount+" advanced="+advanced+" limit="+limit+" stop="+waitStop);
            }
            Require(cache.Read(0)!=null,"Real Host starts a published path before native packet.");
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
                var correctionIdentity=ActualSyncIdentity(host,2);
                NativeCombatObservationChecks.Fresh(context,host);
                WaitForReadmission(context,host,source,cache,correctionIdentity,"large-correction type="+type);
                Require(cache.Read(0)!=null,"Large correction can resample real current target.");
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
                    var changedIdentity=ActualSyncIdentity(host,2);
                    NativeCombatObservationChecks.Fresh(context,host);
                    WaitForReadmission(context,host,source,cache,changedIdentity,"changed-target type="+type+" change="+change);
                    Require(cache.Read(0)!=null,"Reacquire actual changed target.");
                }
            }
            SegmentedBoundaries(context,host,source,cache);
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
            Console.WriteLine("PASS SYNC23 real original serialization/dispatch: nonzero generation, ordinary history, forced instance, generation, same-token transform and large correction.");
            }
            finally{Main.sectionManager=previousSections;Main.netMode=previousNetMode;}
        }
        private static NpcIdentity ActualSyncIdentity(object host,int slot)
        {
            var npc=Main.npc[slot];
            return new NpcIdentity((long)Get(host,"Session"),npc,slot,npc==null?-1:npc.generation,npc==null?-1:npc.type,npc==null?-1:npc.netID);
        }
        private static void WaitForReadmission(object context,object host,object source,NpcPredictionCache cache,NpcIdentity expected,string stage)
        {
            // Only these two positive reacquisition checks have no one-frame
            // publication promise. Packet retirement does not reset either
            // Host clock. Wait only while BOTH admission clocks are not due;
            // the first admission/outcome returns to the unchanged assertion.
            // Normal-sync motion history and all same-GUC checks stay separate.
            int limit=(int)Get(host,"DisplayRefreshUpdates")*3,advanced=0;
            while(ReadmissionPending(host,source,cache,expected,stage,advanced,limit))
            {
                if(advanced>=limit)break;
                NativeCombatObservationChecks.Fresh(context,host);advanced++;
            }
        }
        private static bool ReadmissionPending(object host,object source,NpcPredictionCache cache,NpcIdentity expected,string stage,int advanced,int limit)
        {
            var selection=Get(host,"Selection");var selected=(NpcIdentity)Get(selection,"Target");
            var path=cache.Read(0);var raw=GetOptional(cache,"result") as NpcTrajectory;var budget=GetOptional(host,"PredictionWork") as CombatPredictionWork;
            var demands=GetOptional(cache,"demands") as int[];var minimums=GetOptional(cache,"minimums") as int[];
            long now=Main.GameUpdateCount,fullTick=(long)Get(host,"optionalTick"),shortTick=(long)Get(host,"displayShortTick"),outcomeAt=(long)Get(source,"outcomeTick");
            bool hasTarget=(bool)Get(selection,"HasTarget"),selectedMatches=hasTarget && selected.Equals(expected);
            bool liveMatches=Main.npc[expected.Slot]!=null && Main.npc[expected.Slot].active && expected.Equals(ActualSyncIdentity(host,expected.Slot));
            bool optionalTargetMatches=expected.Equals((NpcIdentity)Get(host,"optionalTarget"));var outcomeIdentity=GetOptional(source,"outcomeIdentity");
            bool optional=(bool)Get(host,"OptionalPreparation"),pathEnabled=(bool)Get(host,"Path"),pathFailed=(bool)Get(host,"pathFailed"),targetFailed=(bool)Get(host,"TargetPredictionFailed");
            int demand=demands!=null && demands.Length>0?demands[0]:0,minimum=minimums!=null && minimums.Length>0?minimums[0]:0;
            bool exceeded=budget!=null && budget.Exceeded;
            bool pending=path==null && raw==null && pathEnabled && !pathFailed && !targetFailed && demand>0 && minimum==1 &&
                liveMatches && selectedMatches && outcomeAt!=now && !optional && optionalTargetMatches &&
                fullTick>=0 && fullTick<now && now-fullTick<(int)Get(host,"DisplayRefreshUpdates") &&
                shortTick>=0 && shortTick<now && now-shortTick<(int)Get(host,"DisplayShortRefreshUpdates") && budget!=null && !exceeded;
            Console.WriteLine("SYNC READMISSION stage="+stage+" advanced="+advanced+" limit="+limit+" guc="+now+" hasTarget="+hasTarget+" selectedMatches="+selectedMatches+" liveMatches="+liveMatches+" expected="+expected.Slot+"/"+expected.Generation+"/"+expected.Type+" path="+(path!=null)+" rawCount="+(raw==null?-1:raw.Count)+" rawStop="+(raw==null?"none":raw.Stop.ToString())+" rawSample="+(raw==null?-1:raw.SampleTick)+" demand0="+demand+" minimum0="+minimum+" pathEnabled="+pathEnabled+" pathFailed="+pathFailed+" targetFailed="+targetFailed+" optional="+optional+" optionalTick="+fullTick+" displayShortTick="+shortTick+" optionalTargetMatches="+optionalTargetMatches+" outcomeTick="+outcomeAt+" outcomeStop="+Get(source,"outcomeStop")+" outcomeLayer="+Get(source,"outcomeLayer")+" outcomeFuture="+Get(source,"outcomeFuture")+" outcomeMatches="+object.Equals(outcomeIdentity,expected)+" budgetMs="+(budget==null?-1:budget.UsedMilliseconds)+" exceeded="+exceeded+" waitEligible="+pending);
            Require(liveMatches && selectedMatches,"Sync readmission identity changed or selected target became unavailable; another target cannot satisfy this packet oracle.");
            Require(path==null || path.Identity.Equals(expected),"Sync readmission published another target's path.");
            Require(outcomeAt!=now || object.Equals(outcomeIdentity,expected),"Sync readmission Source outcome belongs to another target.");
            return pending;
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
            var selected=Main.npc[4];selected.SetDefaults(14);selected.whoAmI=4;selected.active=true;selected.target=Main.myPlayer;selected.position=new Vector2(720,800);selected.velocity=Vector2.Zero;
            NativeCombatObservationChecks.Fresh(context,host);var unrelated=cache.Read(0);Require(ReferenceEquals(unrelated.Identity.Token,selected),"Current segmented A replaces previously observed unrelated B.");
            before=target.position;target.position.X+=2;packet=CombatNetworkFixture.Serialize(23,2);target.position=before;CombatNetworkFixture.Receive(packet);
            Require(ReferenceEquals(cache.Read(0),unrelated),"Unrelated old B sync does not revoke current A publication.");Call(host,"Update",Main.GameUpdateCount);
            Require(ReferenceEquals(cache.Read(0),unrelated),"Unrelated old B history correction cannot invalidate equivalent A result reuse.");
            selected.ai[1]=2;Call(host,"Update",Main.GameUpdateCount);var related=cache.Read(0);
            before=target.position;target.position.X+=2;packet=CombatNetworkFixture.Serialize(23,2);target.position=before;CombatNetworkFixture.Receive(packet);
            Require(cache.Read(0)==null,"True current parent B sync still retires A publication.");Call(host,"Update",Main.GameUpdateCount);
            Require(cache.Read(0)!=null && !ReferenceEquals(cache.Read(0),related),"True current dependency correction rebuilds A.");selected.active=false;
            var worm=target;worm.active=false;var ordinary=Main.npc[3];ordinary.SetDefaults(2);ordinary.whoAmI=3;ordinary.active=true;ordinary.target=Main.myPlayer;ordinary.position=new Vector2(810,900);ordinary.velocity=new Vector2(1,0);
            NativeCombatObservationChecks.Fresh(context,host);var normal=cache.Read(0);Require(normal!=null && ReferenceEquals(normal.Identity.Token,ordinary),"Segmented to ordinary directly selects current Source target.");
            Call(source,"ObserveNpcReset",worm);Require(ReferenceEquals(cache.Read(0),normal),"Dormant worm reset cannot revoke new ordinary publication.");
            Call(source,"ObserveNpcQueryUpdate",2,true);Require(ReferenceEquals(cache.Read(0),normal),"Dormant discontinuity cannot clear new ordinary history.");
            Call(source,"ObserveNpcQueryUpdate",3,true);Require(cache.Read(0)==null,"Current dependency still revokes invalid publication.");
            Console.WriteLine("PASS CLOSEOUT default Source direct motion/equivalence, actual .8 correction/changed speed, shape and dormant retirement.");
        }
    }
}
