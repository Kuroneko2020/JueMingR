using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Original NewNPC/AI populates the chain; observations are pure values.
    // AI-only and full UpdateNPC are separate oracles, never extra live AI in
    // the forecast. Each leaf stops on its first mismatch and retains output.
    internal static class NativeCombatSegmentedWormChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static readonly int[] Heads={87,117,454,510,513,621};
        private static readonly List<string> outlets=new List<string>();
        private static int rawHealthSlot=-1;
        private static Action<NPC> before,after;
        private static Action<Player> playerBefore,playerAfter;
        private static Action<Player,double> hurtAfter;
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static void PlayerBefore(Player __instance){playerBefore?.Invoke(__instance);}
        private static void PlayerAfter(Player __instance){playerAfter?.Invoke(__instance);}
        private static void AfterHurt(Player __instance,double __result){if(__result>0)hurtAfter?.Invoke(__instance,__result);}
        private static Type Model{get{return typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcSegmentedWormMotion",true);}}
        private static bool Family(int t){return t>=87 && t<=92 || t>=117 && t<=119 || t>=454 && t<=459 || t>=510 && t<=515 || t>=621 && t<=623;}
        private static bool IsolateNetwork(int msgType,int remoteClient,int ignoreClient,Terraria.Localization.NetworkText text,int number,float number2,float number3,float number4,int number5,int number6,int number7)
        {
            if((msgType!=23 && msgType!=28) || number<0 || number>=Main.maxNPCs || !Family(Main.npc[number].type) && number!=rawHealthSlot)return true;
            outlets.Add(string.Join("/",msgType,remoteClient,ignoreClient,text,number,number2,number3,number4,number5,number6,number7));return false;
        }
        private static Harmony Network()
        {
            var h=new Harmony("JueMingR.Probe.SegmentedWormNetwork");
            h.Patch(typeof(NetMessage).GetMethod("SendData",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSegmentedWormChecks).GetMethod(nameof(IsolateNetwork),Flags)){priority=Priority.First+100});return h;
        }
        internal static void Boundaries()
        {
            var k=Model.GetMethod("Step",Flags);var born=Model.GetMethod("BirthPending",Flags);int cases=0;
            foreach(int type in Heads)
            {
                var n=new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,type,type),Style=6,Active=true};
                Require((bool)born.Invoke(null,new object[]{n,new PredictionEnvironment()}),"Native server birth consumer type="+type);
                Require(!(bool)born.Invoke(null,new object[]{n,new PredictionEnvironment{Multiplayer=true}}),"Native client cannot duplicate authoritative chain type="+type);cases+=2;
            }
            var parent=new NpcMotionState{Identity=new NpcIdentity(1,new object(),0,1,87,87),Active=true,Style=6,Life=0,Width=32,Height=32,X=1900,Y=1500,Alpha=0};
            var tail=new NpcMotionState{Identity=new NpcIdentity(1,new object(),2,1,92,92),Style=6,Active=true,Life=100,LifeMax=100,Width=32,Height=32,X=1850,Y=1530,A1=1,ParentIdentity=parent.Identity,WormRelationsCaptured=true,WormRelationsComplete=true,WormLinkFactsCaptured=true,TargetCaptured=true,Target=0};
            // A1=1 explicitly consumes the supplied full parent identity; the
            // raw snapshot's identity slot must agree with its native ai slot.
            parent.Identity=new NpcIdentity(1,parent.Identity.Token,1,1,87,87);tail.ParentIdentity=parent.Identity;
            var group=new[]{parent,tail};object[] args={tail,new PredictionEnvironment(),null,1,PredictionStop.None,group,2,false};
            Require((bool)k.Invoke(null,args),"AI raw guard accepts active style6 with zero life, independently of prior health consumer.");cases++;
            args[0]=tail;args[5]=new[]{tail};args[6]=1;
            Require(!(bool)k.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.MissingDependency,"Missing full parent is not a known orphan.");cases++;
            parent.Active=false;group[0]=parent;args[0]=tail;args[5]=group;args[6]=2;
            Require(!(bool)k.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.Despawn,"Known inactive parent causes native server retirement.");cases++;
            parent.Active=true;parent.Style=73;group[0]=parent;args[0]=tail;
            Require(!(bool)k.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.Despawn,"Known active wrong style causes retirement; type whitelist is not used.");cases++;
            Console.WriteLine("PASS SegmentedWorm birth/fullidentity/known-orphan boundary cases="+cases);
        }
        internal static void Lifecycle(object context)
        {
            var network=Network();int actions=0,orphan=0,births=0;outlets.Clear();
            try
            {
                foreach(int type in Heads)
                {
                    Setup(context);var head=Birth(type,type==87?0:16);var terrain=Terrain(context);var observed=Capture(context);int selected=Index(observed,head);
                    object[] birth={observed[selected],EnvironmentFor(head),terrain,0,PredictionStop.None,observed,observed.Length,false};
                    Require(!(bool)Model.GetMethod("Step",Flags).Invoke(null,birth) && (PredictionStop)birth[4]==PredictionStop.PhaseBoundary,"No future birth identity is invented before actual NewNPC type="+type);
                    SpawnChain(head);int count=Count(type);Require(count>=4,"Original AI completes an actual chain type="+type+" count="+count);births+=count-1;
                    for(int frame=0;frame<12;frame++)
                    for(int slot=0;slot<Main.maxNPCs;slot++)
                    {
                        var actor=Main.npc[slot];if(!actor.active || !SameChain(actor,head))continue;
                        observed=Capture(context);selected=Index(observed,actor);terrain.Reset();
                        object[] args={observed[selected],EnvironmentFor(actor),terrain,0,PredictionStop.None,observed,observed.Length,actor.confused};
                        bool valid=(bool)Model.GetMethod("Step",Flags).Invoke(null,args);Require(valid,"Original observed chain AI is deterministic type="+actor.type+" slot="+slot+" stop="+args[4]);
                        var expected=(NpcMotionState)args[0];actor.AI();Compare(expected,actor,"AI type="+actor.type+" slot="+slot+" frame="+frame);actions++;
                    }
                    if(type==621)
                    {
                        Main.dayTime=true;observed=Capture(context);selected=Index(observed,head);terrain.Reset();
                        object[] args={observed[selected],EnvironmentFor(head),terrain,0,PredictionStop.None,observed,observed.Length,false};
                        Require((bool)Model.GetMethod("Step",Flags).Invoke(null,args),"Native BloodEel daylight has a deterministic retreat prefix.");
                        var expected=(NpcMotionState)args[0];head.AI();Compare(expected,head,"BloodEel actual daylight");Require(head.timeLeft<=60 && expected.TimeLeft==head.timeLeft,"Original daylight EncourageDespawn60 remains exact.");Main.dayTime=false;actions++;
                    }
                    if(type==117)
                    {
                        // AI6 reads this nonnegative raw global without an
                        // active/type whitelist. These are observed legal raw
                        // states, and actual Strike establishes each threshold.
                        int rawSlot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),2500,2200,387,Start:100,Target:Main.myPlayer);var raw=Main.npc[rawSlot];rawHealthSlot=rawSlot;
                        Require(raw.active && raw.type==387,"Actual foreign raw-global object exists.");for(int i=0;i<121;i++)raw.AI();Main.wofNPCIndex=rawSlot;
                        foreach(float threshold in new[]{.5f,.25f,.1f})
                        {
                            while((float)raw.life/raw.lifeMax>=threshold && raw.active)raw.StrikeNPCNoInteraction(raw.defense+1,0,0);
                            Require(raw.active && raw.life>0 && (float)raw.life/raw.lifeMax<threshold,"Real positive receiving crosses raw Leech speed threshold="+threshold);
                            observed=Capture(context);selected=Index(observed,head);terrain.Reset();object[] args={observed[selected],EnvironmentFor(head),terrain,0,PredictionStop.None,observed,observed.Length,false};
                            Require((bool)Model.GetMethod("Step",Flags).Invoke(null,args),"Raw global health threshold supplies the current Leech motor.");var expected=(NpcMotionState)args[0];head.AI();Compare(expected,head,"Leech actual raw threshold="+threshold);actions++;
                        }
                        Main.wofNPCIndex=-1;rawHealthSlot=-1;
                    }
                    var child=Child(head);Require(child!=null,"Actual head has an observed first child.");
                    if(type==87)Require(NPCID.Sets.CannotSpawnInSlot0[type] && head.whoAmI>0 && child.ai[1]==head.whoAmI,"Original NewNPC honors CannotSpawnInSlot0 and the first body retains its actual allocated parent.");
                    // Actual positive receiving, then real death; no private
                    // ai/alpha/life rewrite establishes these transitions.
                    int life=head.life;head.StrikeNPCNoInteraction(Math.Max(30,head.defense+30),0,0);
                    Require(head.life<life && head.life>0,"Original positive Strike changes the shared owner type="+type);
                    observed=Capture(context);selected=Index(observed,child);Require(observed[selected].Health.RealLife==head.whoAmI,"Original child retains its real-life owner after receiving.");
                    head.StrikeNPCNoInteraction(head.lifeMax+head.defense+100,0,0);Require(!head.active,"Actual head death retires its identity type="+type);
                    foreach(int mode in new[]{1,2})
                    {
                        // Snapshot from this actual death. Client's original
                        // AI skips its guard; server consumes the inactive raw
                        // value, without requiring a fictitious active owner.
                        Main.netMode=mode;observed=Capture(context);selected=Index(observed,child);terrain.Reset();
                        object[] args={observed[selected],EnvironmentFor(child),terrain,0,PredictionStop.None,observed,observed.Length,child.confused};
                        bool valid=(bool)Model.GetMethod("Step",Flags).Invoke(null,args);
                        if(mode==1){Require(valid,"Client raw orphan keeps its native constraint action.");var expected=(NpcMotionState)args[0];child.AI();Compare(expected,child,"client orphan");}
                        else {Require(!valid && (PredictionStop)args[4]==PredictionStop.Despawn,"Server known orphan has exact native retirement.");child.AI();Require(!child.active,"Original server retires the orphan.");orphan++;break;}
                    }
                    Main.netMode=0;
                }
                Require(births>0 && orphan==6 && outlets.Count>0,"All six real births and receiving/orphan network consumers were retained.");
                Console.WriteLine("PASS SegmentedWorm original lifecycle actions="+actions+" parts="+births+" orphans="+orphan+" outlets="+outlets.Count);
            }
            finally{Main.netMode=0;rawHealthSlot=-1;network.UnpatchAll(network.Id);}
        }
        private sealed class Frozen{internal readonly NpcTrajectory Path;internal Frozen(NpcTrajectory path){Path=path;}}
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            string filter=System.Environment.GetEnvironmentVariable("JUEMINGR_SEGMENTED_WORM_SCENE");int leaf=-1;
            if(!string.IsNullOrEmpty(filter) && (!int.TryParse(filter,out leaf) || leaf<0 || leaf>Heads.Length))throw new ArgumentException("JUEMINGR_SEGMENTED_WORM_SCENE must be0..6 or unset.");
            Console.WriteLine("SEGMENTED-WORM actualLeaves="+(leaf<0?"0,1,2,3,4,5,6":leaf.ToString(CultureInfo.InvariantCulture)));
            if(leaf==6){WallLeech(context,cache,step,output);return;}
            var host=Get(context,"CombatObservation");var terrain=Terrain(context);var network=Network();
            var audit=new Harmony("JueMingR.Probe.SegmentedWormSlot");audit.Patch(typeof(NPC).GetMethod("UpdateNPC",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSegmentedWormChecks),nameof(Before)),postfix:new HarmonyMethod(typeof(NativeCombatSegmentedWormChecks),nameof(After)));
            var csv=new List<string>{"scene,frame,head,parts,actions,matched,long,bodyActions,receive,hit,fresh,restored,error"};int total=0;
            try
            {
                for(int scene=0;scene<Heads.Length;scene++)
                {
                    if(leaf>=0 && scene!=leaf)continue;Setup(context);var head=Birth(Heads[scene],scene==0?0:16);
                    // Complete birth uses native slot order; no predicted count
                    // or chosen random length replaces the actual new rows.
                    SpawnChain(head);int pieces=Count(head.type);Require(pieces>=4,"Actual native chain is complete before its first publication.");
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                    var frozen=new List<Frozen>();NpcMotionState predicted=default(NpcMotionState);NPC observedActor=null;bool valid=false;PredictionStop stop=PredictionStop.None;
                    int actions=0,bodyActions=0,receiving=0,matched=0,longMatches=0,fresh=0,restored=0;long hitTick=-1;bool hit=false;NpcTrajectory previous=null;double maximum=0;
                    before=n=>
                    {
                        observedActor=null;if(!n.active || !SameChain(n,head))return;observedActor=n;var group=Capture(context);int index=Index(group,n);terrain.Reset();predicted=group[index];
                        valid=NpcMotion.Step(ref predicted,group,group.Length,EnvironmentFor(n),terrain,1,true,out stop);
                        Require(valid || stop==PredictionStop.Despawn,"Original chain full action rejected type="+n.type+" slot="+n.whoAmI+" A="+string.Join("/",n.ai)+" stop="+stop);
                    };
                    after=n=>
                    {
                        if(!ReferenceEquals(observedActor,n))return;
                        if(!valid){Require(!n.active,"Known native chain retirement agrees with current full action.");return;}
                        Compare(predicted,n,"full current type="+n.type+" slot="+n.whoAmI);actions++;if(n!=head)bodyActions++;if(!n.dontTakeDamage)receiving++;
                    };
                    for(int frame=0;frame<480 && head.active;frame++)
                    {
                        if(frame==180)
                        {
                            previous=cache.Read(0);int life=head.life;head.StrikeNPCNoInteraction(head.defense+15,0,0);
                            Require(head.life<life && head.life>0,"Actual selected chain positive receiving keeps the owner alive.");hit=true;hitTick=(long)Main.GameUpdateCount;frozen.Clear();
                        }
                        NativeCombatModeledImpactChecks.SampleMouse(context,head.Center);step();if(!head.active)break;
                        var path=cache.Read(0);if(hit && Main.GameUpdateCount>hitTick)
                        {
                            Require(path==null || !ReferenceEquals(path,previous),"Real shared-life receive withdraws the preceding production publication.");
                            var prepared=GetOptional(Get(host,"World"),"preparedPath") as NpcTrajectory;Require(prepared==null || ReferenceEquals(prepared,path),"Prepared display cannot retain a pre-hit chain path.");
                        }
                        if(path!=null && ReferenceEquals(path.Identity.Token,head) && (frozen.Count==0 || !ReferenceEquals(frozen[frozen.Count-1].Path,path)))
                        {frozen.Add(new Frozen(path));if(hit && path.CaptureTick>hitTick)fresh++;}
                        foreach(var f in frozen)
                        {
                            int age=(int)((long)Main.GameUpdateCount-f.Path.CaptureTick);if(age<=0 || age>=f.Path.Count)continue;var point=f.Path[age];
                            double error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),head.position);maximum=Math.Max(maximum,error);
                            Require(error<=.08 && Math.Abs(point.Vx-head.velocity.X)<=.0001 && Math.Abs(point.Vy-head.velocity.Y)<=.0001 && point.CanReceive==(!head.dontTakeDamage && !head.immortal && !head.friendly),"Frozen chain scene="+scene+" frame="+frame+" age="+age+" error="+error.ToString("R")+" predicted="+point.Bounds.X+","+point.Bounds.Y+" V="+point.Vx+","+point.Vy+" native="+head.position+" V="+head.velocity+" stop="+f.Path.Stop);
                            matched++;if(age>=30){longMatches++;if(hit && f.Path.CaptureTick>hitTick)restored++;}
                        }
                        csv.Add(string.Join(",",scene,frame,head.type,pieces,actions,matched,longMatches,bodyActions,receiving,hit,fresh,restored,maximum.ToString("R",CultureInfo.InvariantCulture)));
                    }
                    Require(bodyActions>0 && receiving>30 && matched>=30 && longMatches>=30 && hit && fresh>0 && restored>=30,"Complete real chain has receiving futures, full body actions, hit invalidation and useful recovery.");
                    total+=matched;Console.WriteLine("PASS SegmentedWorm natural scene="+scene+" head="+head.type+" parts="+pieces+" actions="+actions+" body="+bodyActions+" matched="+matched+" long="+longMatches+" fresh="+fresh+" restored="+restored+" max="+maximum.ToString("R",CultureInfo.InvariantCulture));before=after=null;
                }
                Console.WriteLine("PASS SegmentedWorm totalMatched="+total);
            }
            finally{before=after=null;audit.UnpatchAll(audit.Id);network.UnpatchAll(network.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"segmented-worm-scenes.csv"),csv);}
            if(leaf<0)WallLeech(context,cache,step,output);
        }
        private static void WallLeech(object context,NpcPredictionCache cache,Action step,string output)
        {
            // Reuse the already-certified canonical Wall arena/player profile.
            // Keep its real ai1 clock and NewNPC birth; no early Leech or AI
            // rewrite substitutes for the lower-slot consumer of this global.
            typeof(NativeCombatWallBossChecks).GetMethod("Setup",Flags).Invoke(null,new object[]{context,0});
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var terrain=Terrain(context);
            var readPlayer=source.GetType().GetMethod("ReadPlayer",Flags);var bind=source.GetType().GetMethod("TryBindPlayerWall",Flags);
            var body=Main.npc[NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),800,4200,113,Start:16,Target:255)];rawHealthSlot=body.whoAmI;
            var network=Network();var hooks=new Harmony("JueMingR.Probe.SegmentedWallLeech");
            var csv=new List<string>{"frame,tick,wallSlot,headSlot,wallLife,headLife,wallActions,eyeActions,hungryActions,chainActions,playerActions,tongue,matched,long,fresh,restored,receiving,expired,max"};
            int birthFrames=0,wallActions=0,eyeActions=0,hungryActions=0,chainActions=0,pcActions=0,tongue=0,matched=0,longMatches=0,fresh=0,restored=0,receiving=0,expired=0,bindings=0,latestRecovery=0,tailActions=0;double maximum=0;
            try
            {
                NPC head=null;
                for(;birthFrames<3000 && body.active && head==null;birthFrames++)
                {
                    step();for(int slot=0;slot<Main.maxNPCs;slot++)if(Main.npc[slot].active && Main.npc[slot].type==117){head=Main.npc[slot];break;}
                }
                Require(head!=null && body.active && birthFrames>=2700,"Original Wall clock, not fixture ai writes, releases the actual Leech.");
                step();step();Require(head.active && head.whoAmI<body.whoAmI && Child(head)!=null && Child(head).active,"Actual lower-slot Leech and complete descendant identities precede their Wall writer.");
                Console.WriteLine("SEGMENTED-WALL actualBirth frames="+birthFrames+" ownerSlot="+body.whoAmI+" headSlot="+head.whoAmI+" clock="+body.ai[1]+"/"+body.ai[2]+" parts="+Count(117));
                var frozen=new List<NpcTrajectory>();NPC pending=null;NpcMotionState predicted=default(NpcMotionState);bool valid=false;PredictionStop stop=PredictionStop.None;
                PredictionPlayerMotion pc=default(PredictionPlayerMotion);bool pcValid=false,pcHit=false;PredictionStop pcStop=PredictionStop.None;
                bool changed=false,awaitPublication=false;long changedTick=-1;NpcTrajectory oldPublication=null;
                Action invalidate=()=>
                {
                    foreach(var old in frozen)Require((old.Assumptions&PredictionAssumption.NoNewHits)!=0,"Old composite paths explicitly retain the receiving premise.");
                    expired+=frozen.Count;frozen.Clear();oldPublication=cache.Read(0);changedTick=(long)Main.GameUpdateCount;changed=awaitPublication=true;latestRecovery=0;
                };
                before=n=>
                {
                    pending=null;if(!n.active || !SameChain(n,head) && (n.type<113 || n.type>115))return;
                    pending=n;var group=Capture(context);int index=Index(group,n);Require(index>=0,"Real composite slot is present in the current value oracle.");terrain.Reset();predicted=group[index];
                    valid=NpcMotion.Step(ref predicted,group,group.Length,EnvironmentFor(n),terrain,1,true,out stop);
                    Require(valid || stop==PredictionStop.Despawn || stop==PredictionStop.PhaseBoundary,"Composite current action cannot silently freeze a dependency type="+n.type+" slot="+n.whoAmI+" stop="+stop);
                };
                after=n=>
                {
                    if(!ReferenceEquals(pending,n))return;
                    if(!valid){if(stop==PredictionStop.Despawn)Require(!n.active,"Actual composite dependency retirement agrees with pure current action.");return;}
                    if(SameChain(n,head)){Compare(predicted,n,"Wall composite type="+n.type+" slot="+n.whoAmI);chainActions++;}
                    else
                    {
                        double error=Vector2.Distance(new Vector2(predicted.X,predicted.Y),n.position);
                        Require(error<=.08 && Math.Abs(predicted.Vx-n.velocity.X)<=.0001 && Math.Abs(predicted.Vy-n.velocity.Y)<=.0001 && predicted.CanReceive==(!n.dontTakeDamage && !n.immortal),"Original composite Wall slot type="+n.type+" slot="+n.whoAmI+" error="+error+" predictedV="+predicted.Vx+","+predicted.Vy+" actualV="+n.velocity);
                        if(n.type==113){wallActions++;Require(predicted.WallTop==Main.wofDrawAreaTop && predicted.WallBottom==Main.wofDrawAreaBottom,"Later Wall writer preserves its real scan-band update.");}else if(n.type==114)eyeActions++;else hungryActions++;
                    }
                };
                playerBefore=p=>
                {
                    if(!ReferenceEquals(p,Main.LocalPlayer))return;pcHit=false;pc=(PredictionPlayerMotion)readPlayer.Invoke(null,new object[]{p});terrain.Reset();pcValid=PlayerMotionContinuation.Advance(ref pc,EnvironmentFor(head),terrain,out pcStop);
                    Require(pcValid || pcStop==PredictionStop.PhaseBoundary || pcStop==PredictionStop.Despawn || pcStop==PredictionStop.MissingDependency,"Original composite PC has an explicit necessary consumer boundary: "+pcStop);
                };
                hurtAfter=(p,amount)=>{if(!ReferenceEquals(p,Main.LocalPlayer))return;pcHit=true;receiving++;invalidate();Console.WriteLine("SEGMENTED-WALL receiving tick="+Main.GameUpdateCount+" amount="+amount+" life="+p.statLife+" fall="+p.fallStart+","+p.fallStart2);};
                playerAfter=p=>
                {
                    if(!ReferenceEquals(p,Main.LocalPlayer))return;if(p.tongued)tongue++;if(!pcValid || pcHit)return;
                    double error=Vector2.Distance(new Vector2(pc.X,pc.Y),p.position);
                    Require(error<=.08 && Math.Abs(pc.Vx-p.velocity.X)<=.0001 && Math.Abs(pc.Vy-p.velocity.Y)<=.0001 && pc.WallGross==p.gross && pc.WallTongued==p.tongued && (!pc.WallHealthKnown || pc.WallLife==p.statLife && pc.WallRegenCount==p.lifeRegenCount && pc.WallRegenTime==p.lifeRegenTime),"Actual composite PlayerWall movement/health consumer error="+error+" predicted="+pc.X+","+pc.Y+" actual="+p.position+" stop="+pcStop);pcActions++;
                };
                hooks.Patch(typeof(NPC).GetMethod("UpdateNPC",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSegmentedWormChecks),nameof(Before)),postfix:new HarmonyMethod(typeof(NativeCombatSegmentedWormChecks),nameof(After)));
                hooks.Patch(typeof(Player).GetMethod("Update",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSegmentedWormChecks),nameof(PlayerBefore)),postfix:new HarmonyMethod(typeof(NativeCombatSegmentedWormChecks),nameof(PlayerAfter)));
                var hurt=typeof(Player).GetMethod("Hurt",Flags,null,new[]{typeof(Terraria.DataStructures.PlayerDeathReason),typeof(int),typeof(int),typeof(bool),typeof(bool),typeof(bool),typeof(int),typeof(bool)},null);
                hooks.Patch(hurt,postfix:new HarmonyMethod(typeof(NativeCombatSegmentedWormChecks),nameof(AfterHurt)));
                NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                // Retain the original480-action denominator. A genuine last
                // Hurt may occur after its NPC actions: allow at most80 more
                // real actions solely to observe the new publication and a
                // >=30-action future from that latest receiving input. Another
                // hit resets this obligation; exhausting the bound still fails.
                for(int frame=0;frame<560 && head.active && body.active;frame++)
                {
                    if(frame>=480){if(!awaitPublication && latestRecovery>0)break;tailActions++;}
                    if(frame==180)
                    {
                        int life=body.life;body.StrikeNPCNoInteraction(body.life-(int)(body.lifeMax*.45f)+body.defense+20,0,0);
                        Require(body.life>0 && body.life<life && (double)body.life<body.lifeMax*.5,"Real Wall receiving crosses Leech's dynamic raw-owner speed threshold.");invalidate();
                    }
                    NativeCombatModeledImpactChecks.SampleMouse(context,head.Center);step();if(!head.active || !body.active)break;
                    var path=cache.Read(0);if(changed && Main.GameUpdateCount>changedTick)
                    {
                        Require(path==null || !ReferenceEquals(path,oldPublication),"Actual composite receiving withdraws its preceding production publication.");
                        var prepared=GetOptional(Get(host,"World"),"preparedPath") as NpcTrajectory;Require(prepared==null || ReferenceEquals(prepared,path),"The actual prepared path cannot retain the old composite receiving premise.");
                    }
                    if(path!=null && ReferenceEquals(path.Identity.Token,head) && !frozen.Contains(path))
                    {
                        Require(path.WallSources!=null && path.WallSources.Count==path.Count,"Production Leech closure carries the true Wall player-source timeline.");
                        var motion=(PredictionPlayerMotion)readPlayer.Invoke(null,new object[]{Main.LocalPlayer});object[] binding={path,(long)Main.GameUpdateCount,motion,EnvironmentFor(head)};
                        Require((bool)bind.Invoke(null,binding) && ((PredictionPlayerMotion)binding[2]).WallSourceExpected,"Actual full-token Leech publication binds its PlayerWall payload for the consumer.");bindings++;
                        frozen.Add(path);if(changed && path.CaptureTick>changedTick){fresh++;awaitPublication=false;}
                    }
                    foreach(var old in frozen)
                    {
                        int age=(int)((long)Main.GameUpdateCount-old.CaptureTick);if(age<1 || age>=old.Count)continue;var point=old[age];double error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),head.position);maximum=Math.Max(maximum,error);
                        Require(error<=.08 && Math.Abs(point.Vx-head.velocity.X)<=.0001 && Math.Abs(point.Vy-head.velocity.Y)<=.0001 && point.CanReceive==(!head.dontTakeDamage && !head.immortal && !head.friendly),"Frozen Wall/Leech scene6 frame="+frame+" age="+age+" error="+error+" owner="+body.whoAmI+" life="+body.life+" predictedV="+point.Vx+","+point.Vy+" actualV="+head.velocity+" stop="+old.Stop);matched++;if(age>=30){longMatches++;if(changed && old.CaptureTick>changedTick){restored++;latestRecovery++;}}
                    }
                    csv.Add(string.Join(",",frame,Main.GameUpdateCount,body.whoAmI,head.whoAmI,body.life,head.life,wallActions,eyeActions,hungryActions,chainActions,pcActions,tongue,matched,longMatches,fresh,restored,receiving,expired,maximum.ToString("R",CultureInfo.InvariantCulture)));
                }
                Console.WriteLine("SEGMENTED-WALL closeout wallActions="+wallActions+" tailActions="+tailActions+" latestReceivingTick="+changedTick+" publicationPending="+awaitPublication+" latestRecoveryLong="+latestRecovery+" fresh="+fresh+" restored="+restored+" matched="+matched+" long="+longMatches+" max="+maximum.ToString("R",CultureInfo.InvariantCulture));
                Require(wallActions>=480 && eyeActions>30 && hungryActions>30 && chainActions>30 && pcActions>30 && tongue>0 && bindings>0 && longMatches>=30 && changed && expired>0 && !awaitPublication && latestRecovery>0 && fresh>0 && restored>=30,"Real lower-slot Leech/Wall closure keeps its480-action prefix, actual Tongue consumers and bounded latest receiving recovery.");
                Console.WriteLine("PASS SegmentedWorm natural scene=6 head117 wall113 birthFrames="+birthFrames+" wall="+wallActions+" eyes="+eyeActions+" hungry="+hungryActions+" chain="+chainActions+" pc="+pcActions+" tongue="+tongue+" bindings="+bindings+" matched="+matched+" long="+longMatches+" expired="+expired+" fresh="+fresh+" restored="+restored+" receiving="+receiving+" max="+maximum.ToString("R",CultureInfo.InvariantCulture));
            }
            finally{before=after=null;playerBefore=playerAfter=null;hurtAfter=null;rawHealthSlot=-1;hooks.UnpatchAll(hooks.Id);network.UnpatchAll(network.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"segmented-wall-leech-scene6.csv"),csv);}
        }
        private static void Compare(NpcMotionState expected,NPC actual,string label)
        {
            double error=Vector2.Distance(new Vector2(expected.X,expected.Y),actual.position);
            Require(error<=.08 && Math.Abs(expected.Vx-actual.velocity.X)<=.0001 && Math.Abs(expected.Vy-actual.velocity.Y)<=.0001 && expected.Alpha==actual.alpha && expected.Health.DontTakeDamage==actual.dontTakeDamage && expected.Health.RealLife==actual.realLife && expected.L0==actual.localAI[0] && expected.SpriteDirection==actual.spriteDirection,"Original AI6 "+label+" error="+error.ToString("R")+" expected="+expected.X.ToString("R")+","+expected.Y.ToString("R")+" V="+expected.Vx.ToString("R")+","+expected.Vy.ToString("R")+" actual="+actual.position+" V="+actual.velocity+" alpha="+expected.Alpha+"/"+actual.alpha+" lifeOwner="+expected.Health.RealLife+"/"+actual.realLife);
        }
        private static IPredictionTerrain Terrain(object context){return (IPredictionTerrain)Get(Get(Get(context,"CombatObservation"),"Prediction"),"Terrain");}
        private static NpcMotionState[] Capture(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");long session=(long)Get(host,"Session");
            var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);
            // This oracle observes all existing NPC values so native slot
            // writes are visible. Production Source remains necessary-only.
            var rows=new List<NpcMotionState>();
            for(int slot=0;slot<Main.maxNPCs;slot++)
            {
                var n=Main.npc[slot];if(n==null || !n.active && !Family(n.type) && slot!=Main.wofNPCIndex)continue;
                var state=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,state,Terrain(context)};tracking.Invoke(null,args);state=(NpcMotionState)args[1];
                if(Family(n.type))
                {
                    state.WormRelationsCaptured=state.WormRelationsComplete=state.WormLinkFactsCaptured=true;
                    int parent=(int)n.ai[1],child=(int)n.ai[0];
                    if(parent>=0 && parent<Main.npc.Length && Main.npc[parent]!=null)state.ParentIdentity=((NpcMotionState)read.Invoke(null,new object[]{Main.npc[parent],session})).Identity;
                    if(child>=0 && child<Main.npc.Length && Main.npc[child]!=null)state.ChildIdentity=((NpcMotionState)read.Invoke(null,new object[]{Main.npc[child],session})).Identity;
                    state.WallBossCaptured=true;state.WallBossValid=Main.wofNPCIndex>=0 && Main.wofNPCIndex<Main.maxNPCs && Main.npc[Main.wofNPCIndex]!=null;
                    if(state.WallBossValid)state.WallBossIdentity=((NpcMotionState)read.Invoke(null,new object[]{Main.npc[Main.wofNPCIndex],session})).Identity;
                }
                rows.Add(state);
            }
            bool wallValid=Main.wofNPCIndex>=0 && Main.wofNPCIndex<Main.maxNPCs && Main.npc[Main.wofNPCIndex]!=null;
            if(wallValid && Main.npc[Main.wofNPCIndex].active && Main.npc[Main.wofNPCIndex].type==113)
            {
                var wallIdentity=((NpcMotionState)read.Invoke(null,new object[]{Main.npc[Main.wofNPCIndex],session})).Identity;
                // All observed rows receive the native raw-global writer facts;
                // this pure current-slot oracle does not change Source's roles.
                for(int i=0;i<rows.Count;i++){var state=rows[i];state.WallRelationsCaptured=state.WallRelationsComplete=state.WallBossCaptured=state.WallBossValid=true;state.WallBossIdentity=wallIdentity;state.WallTop=Main.wofDrawAreaTop;state.WallBottom=Main.wofDrawAreaBottom;rows[i]=state;}
            }
            return rows.ToArray();
        }
        private static PredictionEnvironment EnvironmentFor(NPC n)
        {
            if(Main.wofNPCIndex>=0 && Main.wofNPCIndex<Main.maxNPCs && Main.npc[Main.wofNPCIndex].active && Main.npc[Main.wofNPCIndex].type==113)
            {
                var composite=(PredictionEnvironment)typeof(NativeCombatWallBossChecks).GetMethod("EnvironmentFor",Flags).Invoke(null,new object[]{n});composite.GravityRockLayer=Main.rockLayer;return composite;
            }
            int target=n.target>=0 && n.target<255?n.target:Main.myPlayer;var p=Main.player[target];var slots=new List<int>();var areas=new List<MotionRect>();bool corrupt=false;
            for(int i=0;i<255;i++)if(Main.player[i].active){var q=Main.player[i];slots.Add(i);areas.Add(new MotionRect((int)q.position.X,(int)q.position.Y,q.width,q.height));if(!q.dead && q.ZoneCorrupt)corrupt=true;}
            return new PredictionEnvironment{Players=new PredictionPlayers(areas.ToArray(),areas.Count,slots.ToArray()),PlayerIndex=target,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerVelocityCaptured=true,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,PlayerTimelineActive=p.active && !p.dead && !p.ghost,PlayerDead=!p.active || p.dead,Expert=Main.expertMode,GoodWorld=Main.getGoodWorld,Day=Main.dayTime,AnyLivingCorrupt=corrupt,Multiplayer=Main.netMode==1,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,GravityWorldSurface=Main.worldSurface,RockLayer=(float)Main.rockLayer,GravityRockLayer=Main.rockLayer};
        }
        private static int Index(NpcMotionState[] rows,NPC n){for(int i=0;i<rows.Length;i++)if(ReferenceEquals(rows[i].Identity.Token,n))return i;return -1;}
        private static bool SameChain(NPC n,NPC head){return Family(n.type) && (ReferenceEquals(n,head) || n.realLife==head.whoAmI || n.ai[3]==head.whoAmI);}
        private static NPC Child(NPC n){int slot=(int)n.ai[0];return slot>=0 && slot<Main.maxNPCs?Main.npc[slot]:null;}
        private static int Count(int type){int count=0;foreach(var n in Main.npc)if(n.active && Family(n.type))count++;return count;}
        private static NPC Birth(int type,int start)
        {
            // A new NPC has direction0. Forcing target0 here bypasses AI6's
            // initial TargetClosest; Wyvern's upward branch then preserves a
            // zero vector and puts its newborn child at zero link distance.
            // Keep the actual default255 and let native AI establish facing.
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1800,2200,type,Start:start);
            Require(slot>=0 && slot<Main.maxNPCs && Main.npc[slot].active && Main.npc[slot].type==type,"Actual worm NewNPC type="+type);
            var n=Main.npc[slot];Console.WriteLine("SEGMENTED birth type="+type+" slot="+slot+" target="+n.target+" direction="+n.direction+" position="+n.position+" velocity="+n.velocity);return n;
        }
        private static void SpawnChain(NPC head)
        {
            // AI births put descendants at the parent's current bottom. The
            // native parent's movement must finish before a later slot links;
            // repeating AI alone can manufacture zero distance and NaN here.
            for(int frame=0;frame<2;frame++)
            {
                NativeQuickItemChecks.BeginWorldStep();Main.EverLastingTicker++;NPC.UpdateProtectedSpawnSlots();
                for(int slot=0;slot<Main.maxNPCs;slot++)
                {
                    var n=Main.npc[slot];if(!n.active || !SameChain(n,head))continue;
                    string headBefore=frame==0 && ReferenceEquals(n,head)?"target="+n.target+" direction="+n.direction+" position="+n.position+" velocity="+n.velocity:null;
                    n.UpdateNPC(slot);
                    if(headBefore!=null)Console.WriteLine("SEGMENTED first-head-action type="+head.type+" slot="+slot+" tick="+Main.GameUpdateCount+" before["+headBefore+"] after[target="+n.target+" direction="+n.direction+" position="+n.position+" velocity="+n.velocity+"]");
                    Require(Finite(n.position.X) && Finite(n.position.Y) && Finite(n.velocity.X) && Finite(n.velocity.Y),"Original full birth action must provide a finite oracle state: head="+head.type+" slot="+slot+" type="+n.type+" frame="+frame+" position="+n.position+" velocity="+n.velocity+" parent="+n.ai[1]);
                }
            }
        }
        private static bool Finite(float value){return !float.IsNaN(value) && !float.IsInfinity(value);}
        private static void Setup(object context)
        {
            NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.GameMode=0;Main.dayTime=false;Main.getGoodWorld=false;Main.wofNPCIndex=-1;
            // Scene(false) bypasses Main.Initialize. Genuine owner death must
            // still execute the populated original drop rules/resolver, and
            // WorldItem asserts each real pool index while dropping/stacking.
            if(Main.ItemDropSolver==null)
            {
                Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();
                Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);
            }
            for(int i=0;i<Main.item.Length;i++)Main.item[i].whoAmI=i;
            for(int i=0;i<2;i++){NativeQuickItemChecks.BeginWorldStep();Main.EverLastingTicker++;NPC.UpdateProtectedSpawnSlots();}
            for(int i=0;i<255;i++)Main.player[i].active=false;
            var p=Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};p.position=new Vector2(1750,2400-p.height);p.ResetFallDamage();p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.immune=true;p.immuneTime=1000000;for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());
        }
    }
}
