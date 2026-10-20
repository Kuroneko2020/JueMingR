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
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatEyeBossChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Action<NPC> before,after;
        private static Action<Projectile> killing;
        private static Action<Projectile,string,List<int>> explosion;
        private static Action<bool> worldPrepare;
        private static Action<MethodBase,long> budgetStage;
        private static void BudgetBefore(out long __state){__state=System.Diagnostics.Stopwatch.GetTimestamp();}
        private static void BudgetAfter(MethodBase __originalMethod,long __state){budgetStage?.Invoke(__originalMethod,__state);}
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static void Killing(Projectile __instance){killing?.Invoke(__instance);}
        private static void WorldBefore(){worldPrepare?.Invoke(false);}
        private static void WorldAfter(){worldPrepare?.Invoke(true);}
        private static void ExplodeBefore(Projectile __instance,Vector2 compareSpot,int radius,int minI,int maxI,int minJ,int maxJ,out List<Tuple<int,int,string>> __state)
        {
            __state=null;if(explosion==null)return;__state=new List<Tuple<int,int,string>>();
            for(int x=Math.Max(0,minI);x<=Math.Min(Main.maxTilesX-1,maxI);x++)for(int y=Math.Max(0,minJ);y<=Math.Min(Main.maxTilesY-1,maxJ);y++)__state.Add(Tuple.Create(x,y,TileText(x,y)));
            explosion(__instance,"before center="+compareSpot+" radius="+radius+" scan="+minI+","+maxI+","+minJ+","+maxJ,null);
        }
        private static void ExplodeAfter(Projectile __instance,List<Tuple<int,int,string>> __state)
        {
            if(__state==null)return;var changed=new List<string>();var keys=new List<int>();foreach(var cell in __state){string actual=TileText(cell.Item1,cell.Item2);if(actual!=cell.Item3){changed.Add(cell.Item1+","+cell.Item2+" before{"+cell.Item3+"} after{"+actual+"}");keys.Add(cell.Item2*Main.maxTilesX+cell.Item1);}}
            explosion?.Invoke(__instance,"after changed="+changed.Count+" cells="+string.Join(";",changed),keys);
        }
        internal static void Boundaries()
        {
            var eye=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcEyeBossMotion",true).GetMethod("Step",Flags);
            var n=new NpcMotionState{Identity=new NpcIdentity(1,new object(),10,1,4,4),Style=4,Active=true,Life=300,LifeMax=1000,Width=110,Height=110,A0=3,A1=3};
            var e=new PredictionEnvironment{Expert=true,PlayerX=500,PlayerY=500};object[] args={n,e,null,1,PredictionStop.None,null,0,false};
            Require(!(bool)eye.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.RandomDestination,"Server quick-charge stops before native motor random draws.");
            e.Multiplayer=true;args[0]=n;args[1]=e;Require((bool)eye.Invoke(null,args),"Client quick-selection waits for the server without inventing random velocity.");
            n.A1=4;n.A2=18;n.Vx=20;e.Multiplayer=false;args[0]=n;args[1]=e;
            Require((bool)eye.Invoke(null,args) && ((NpcMotionState)args[0]).Vx==20,"An observed random charge retains its deterministic flight before native braking.");
            e.Day=true;n.A0=0;n.A1=0;n.Vy=3;n.TimeLeft=750;args[0]=n;args[1]=e;
            Require((bool)eye.Invoke(null,args) && ((NpcMotionState)args[0]).Vy==3-.04f && ((NpcMotionState)args[0]).TimeLeft==10,"Day retreat retains native acceleration and encourages rather than invents immediate retirement.");
            Console.WriteLine("PASS eye server/client true motor RNG and observed-charge/day boundaries");
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");long session=(long)Get(host,"Session");
            Require(GetOptional(source,"Native")==null,"Eye bosses use the actual scalar rolling producer.");
            var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);
            var terrain=(IPredictionTerrain)Activator.CreateInstance(Get(source,"Terrain").GetType(),true);var rows=new List<string>{"scene,frame,type,phase,subphase,selected,published,count,stop,displayMatched,modelLongMatched,error,modelCross,actions,random,componentCaptures,componentMatched,sourceRetired,displayRetired,displayRecoveries"};var failures=new List<string>();
            bool oldGood=Main.getGoodWorld,oldRemix=Main.remixWorld,oldZenith=Main.zenithWorld;var observer=new Harmony("JueMingR.Probe.EyeBossOracle");var update=typeof(NPC).GetMethod("UpdateNPC",Flags);
            observer.Patch(update,prefix:new HarmonyMethod(typeof(NativeCombatEyeBossChecks).GetMethod(nameof(Before),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatEyeBossChecks).GetMethod(nameof(After),Flags)));
            MethodInfo kill=typeof(Projectile).GetMethod("Kill",Flags),explode=typeof(Projectile).GetMethod("ExplodeTiles",Flags);var world=Get(host,"World");var prepareWorld=world.GetType().GetMethod("Prepare",Flags);
            // These are original event and consumer observations only. They
            // never repeat explosion, mutate terrain or retire a cached result.
            observer.Patch(kill,prefix:new HarmonyMethod(typeof(NativeCombatEyeBossChecks).GetMethod(nameof(Killing),Flags)));
            observer.Patch(explode,prefix:new HarmonyMethod(typeof(NativeCombatEyeBossChecks).GetMethod(nameof(ExplodeBefore),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatEyeBossChecks).GetMethod(nameof(ExplodeAfter),Flags)));
            observer.Patch(prepareWorld,prefix:new HarmonyMethod(typeof(NativeCombatEyeBossChecks).GetMethod(nameof(WorldBefore),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatEyeBossChecks).GetMethod(nameof(WorldAfter),Flags)));
            // Bounded, test-only attribution. No production observer or file I/O
            // is installed; two existing warm/cold frames answer this question.
            bool budgetStages=Environment.GetEnvironmentVariable("JUEMINGR_EYE_BUDGET_STAGES")=="1";
            var budgetMethods=new[]{source.GetType().GetMethod("PrepareSample",Flags),typeof(RollingNpcPrediction).GetMethod("Prepare",Flags),host.GetType().GetMethod("CaptureDisplay",Flags,null,new[]{typeof(NpcTrajectory),Get(source,"Terrain").GetType().GetNestedType("CellLease",Flags)},null)};
            var stageRows=new List<Tuple<string,double,double,bool,int>>();
            if(budgetStages)foreach(var method in budgetMethods)
                observer.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatEyeBossChecks).GetMethod(nameof(BudgetBefore),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatEyeBossChecks).GetMethod(nameof(BudgetAfter),Flags)));
            int totalActions=0,totalMatched=0,totalRandom=0;
            try
            {
                // A process-level narrow feedback route preserves these exact
                // existing scenes and every expectation. Default is all 18.
                bool comboOnly=Environment.GetEnvironmentVariable("JUEMINGR_EYE_COMBO_ONLY")=="1";
                int single=-1;string singleText=Environment.GetEnvironmentVariable("JUEMINGR_EYE_COMBO_SCENE");
                if(!string.IsNullOrEmpty(singleText))Require(comboOnly && int.TryParse(singleText,out single) && single>=15 && single<=17,"Narrow Eye feedback selects an existing combo scene without changing its natural window or expectations.");
                int first=single>=0?single:comboOnly?15:0,last=single>=0?single+1:18;
                for(int scene=first;scene<last;scene++)
                {
                    try
                    {
                        Console.WriteLine("EYE START scene="+scene);
                        Setup(context);bool combo=scene>=15;int type=scene<6?4:scene<9?125:scene<12?126:scene<15?127:scene==15?125:scene==16?126:127;
                        Main.GameMode=scene==0 || scene==6 || scene==9 || scene==12?0:1;
                        Main.getGoodWorld=scene==5 || scene==8 || scene==11 || combo;Main.remixWorld=Main.zenithWorld=combo;
                        NPC actor;
                        if(combo)
                        {
                            Require(NPC.SpawnMechQueen(Main.myPlayer),"Actual special-seed SpawnMechQueen establishes all related bosses.");
                            actor=Find(type);Require(actor!=null && NPC.mechQueen>=0,"Real MechQueen identity and selected eye exist.");
                        }
                        else
                        {
                            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1800,1800,type,Start:16,Target:Main.myPlayer);actor=Main.npc[slot];
                            if(type==125 || type==126)NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),2100,1700,type==125?126:125,Start:1,Target:Main.myPlayer);
                        }
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                        var freezes=new List<NpcTrajectory>();var phases=new Dictionary<NpcTrajectory,string>();var leases=new Dictionary<NpcTrajectory,HashSet<int>>();var components=new HashSet<NpcTrajectory>();var changedPremises=new HashSet<NpcTrajectory>();var negativeReported=new HashSet<NpcTrajectory>();var observed=new HashSet<string>();int matched=0,longMatches=0,actions=0,random=0,cross=0,published=0,selectedCount=0,invalidated=0,negativeDifferences=0,recoveredLong=0,mutations=0,publicationRecoveries=0,componentCaptures=0,componentMatches=0,sourceRetired=0,displayRetired=0,missingRun=0,maxMissingRun=0,displayMinFuture=int.MaxValue,displayMaxFuture=0;double maxError=0,tangent=0,negativeMax=0;long lastMutationTick=-1,recoveredMutation=-1;
                        object componentSource=combo?Activator.CreateInstance(source.GetType(),Flags,null,new object[]{null,false},null):null;
                        var componentCache=combo?(NpcPredictionCache)Get(componentSource,"Cache"):null;componentCache?.Demand(0,1,120);
                        NpcTrajectory sourceSnapshot=null;object sourceSnapshotLease=null,sourceBeforeLease=null,displayBeforeLease=null;NpcTrajectory displayBeforeExplosion=null;
                        bool hit=false,reportedMissing=false,reportedSteadyMissing=false;NPC pending=null;NpcMotionState predicted=default(NpcMotionState);bool valid=false;int pendingWormRole=-1;var wormActions=new int[6];
                        int diagnosticFrame=-1,replayFrame=-1;long replayCapture=0;NpcMotionState[] replayGroup=null;NpcMotionState replayBefore=default(NpcMotionState),replayAfter=default(NpcMotionState),nativeBefore=default(NpcMotionState);NPC replayPending=null;bool replayValid=false,reportedReplay=false;PredictionStop replayStop=PredictionStop.None;string replayDetail="",actorReplayDetail="";
                        bool diagnostic=combo && (single>=0 || Environment.GetEnvironmentVariable("JUEMINGR_EYE_CUMULATIVE_DIAGNOSTIC")=="1");
                        bool checkExplosionPublication=false;long explosionTick=-1;NpcTrajectory publicationBeforeExplosion=null;
                        killing=p=>{if(diagnostic && p.type==102)Console.WriteLine("EYE ORIGINAL-BOMB-KILL frame="+diagnosticFrame+" tick="+Main.GameUpdateCount+" slot="+p.whoAmI+" key="+(int)p.key+" pos="+p.position+" size="+p.width+","+p.height+" V="+p.velocity+" ai="+string.Join("/",p.ai)+" life="+p.timeLeft+" good="+Main.getGoodWorld+" anniversary="+Main.tenthAnniversaryWorld);};
                        explosion=(p,detail,changedKeys)=>
                        {
                            var current=cache.Read(0);
                            if(detail.StartsWith("before",StringComparison.Ordinal))
                            {
                                publicationBeforeExplosion=current;explosionTick=(long)Main.GameUpdateCount;
                                sourceBeforeLease=HasFutureAt(current,explosionTick) && ReferenceEquals(current,sourceSnapshot)?sourceSnapshotLease:null;
                                displayBeforeExplosion=GetOptional(host,"displayPath") as NpcTrajectory;displayBeforeLease=HasFutureAt(displayBeforeExplosion,explosionTick)?GetOptional(host,"displayTerrain"):null;
                            }
                            else if(changedKeys!=null && changedKeys.Count>0)
                            {
                                checkExplosionPublication=true;mutations++;lastMutationTick=(long)Main.GameUpdateCount;
                                foreach(var frozen in freezes)foreach(int key in changedKeys)if(leases[frozen].Contains(key)){if(changedPremises.Add(frozen))invalidated++;break;}
                            }
                            if(diagnostic)Console.WriteLine("EYE ORIGINAL-EXPLODE frame="+diagnosticFrame+" tick="+Main.GameUpdateCount+" type="+p.type+" slot="+p.whoAmI+" key="+(int)p.key+" "+detail+" cachedCapture="+(current?.CaptureTick??-1)+" cachedRelationVersion="+(current?.RelationVersion??-1)+" terrainUnchanged="+terrain.Unchanged+" pc="+PlayerRaw());
                        };
                        worldPrepare=completed=>
                        {
                            if(!checkExplosionPublication)return;var current=cache.Read(0);var prepared=GetOptional(world,"preparedPath") as NpcTrajectory;
                            Console.WriteLine("EYE POST-EXPLOSION-WORLD-PREPARE completed="+completed+" frame="+diagnosticFrame+" tick="+Main.GameUpdateCount+" explosionTick="+explosionTick+" cachedCapture="+(current?.CaptureTick??-1)+" cachedRelationVersion="+(current?.RelationVersion??-1)+" cachedSameBeforeExplosion="+ReferenceEquals(current,publicationBeforeExplosion)+" terrainUnchanged="+terrain.Unchanged+" preparedCapture="+(prepared?.CaptureTick??-1)+" preparedCurrent="+ReferenceEquals(prepared,current));
                            if(completed)
                            {
                                // A real explosion need not intersect either
                                // owner's dependency set. Raw and Display have
                                // independent identities and current leases.
                                bool sourceChanged=LeaseChanged(sourceBeforeLease),displayChanged=LeaseChanged(displayBeforeLease);
                                var display=GetOptional(host,"DisplayPath") as NpcTrajectory;
                                if(sourceChanged){Require(current==null || current.CaptureTick>=explosionTick && !ReferenceEquals(current,publicationBeforeExplosion),"A real changed Source dependency retires its actual old publication.");sourceRetired++;}
                                if(displayChanged){Require(display==null || display.CaptureTick>=explosionTick && !ReferenceEquals(display,displayBeforeExplosion),"A real changed Display dependency retires its actual old lease.");displayRetired++;}
                                Require(ReferenceEquals(prepared,display),"Actual World.Prepare consumes exactly the current Host Display lease, independently of latest Source raw.");
                                Console.WriteLine("EYE TERRAIN-OWNERS scene="+scene+" frame="+diagnosticFrame+" sourceKnown="+(sourceBeforeLease!=null)+" sourceAffected="+sourceChanged+" displayKnown="+(displayBeforeLease!=null)+" displayAffected="+displayChanged+" sourceRetired="+sourceRetired+" displayRetired="+displayRetired);
                                checkExplosionPublication=false;
                            }
                        };
                        MethodInfo readPlayer=source.GetType().GetMethod("ReadPlayer",Flags),advancePlayer=typeof(RollingNpcPrediction).GetMethod("AdvancePlayer",Flags);
                        var frozenPlayerOwner=new RollingNpcPrediction();var currentTerrainPlayerOwner=new RollingNpcPrediction();
                        IPredictionTerrain frozenPlayerTerrain=null,currentPlayerTerrain=null;PredictionPlayerMotion frozenPlayer=default(PredictionPlayerMotion),currentTerrainPlayer=default(PredictionPlayerMotion);PredictionEnvironment frozenPlayerEnvironment=default(PredictionEnvironment);
                        int playerReplayFrame=-1,frozenTileCount=0;long playerReplayCapture=0;bool playerReplayStarted=false,reportedPlayerReplay=false;
                        Action<NPC> replayPlayer=n=>
                        {
                            if(!diagnostic || diagnosticFrame<315 || playerReplayFrame==diagnosticFrame)return;
                            playerReplayFrame=diagnosticFrame;var actual=Main.LocalPlayer;
                            if(!playerReplayStarted)
                            {
                                playerReplayStarted=true;frozenPlayer=currentTerrainPlayer=(PredictionPlayerMotion)readPlayer.Invoke(null,new object[]{actual});frozenPlayerEnvironment=EnvironmentFor(n);playerReplayCapture=(long)Main.GameUpdateCount;
                                frozenPlayerTerrain=(IPredictionTerrain)Activator.CreateInstance(terrain.GetType(),true);currentPlayerTerrain=(IPredictionTerrain)Activator.CreateInstance(terrain.GetType(),true);frozenPlayerTerrain.Reset();
                                // A bounded private PC corridor is captured once.
                                // Its cells remain the actual frame316 values;
                                // the production terrain/cache is never reset
                                // or replaced by this diagnostic owner.
                                int left=Math.Max(0,(int)((actual.position.X-512)/16)),right=Math.Min(Main.maxTilesX-1,(int)((actual.position.X+512)/16));
                                int top=Math.Max(0,(int)((actual.position.Y-128)/16)),bottom=Math.Min(Main.maxTilesY-1,(int)((actual.position.Y+384)/16));
                                for(int x=left;x<=right;x++)for(int y=top;y<=bottom;y++){PredictionTile cell;PredictionStop stop;Require(frozenPlayerTerrain.Tile(x,y,out cell,out stop),"Private Eye PC corridor uses actual available captured cells: "+stop);}
                                frozenTileCount=((System.Collections.IDictionary)Get(frozenPlayerTerrain,"cells")).Count;
                                Console.WriteLine("EYE PLAYER CUMULATIVE START frame="+diagnosticFrame+" capture="+playerReplayCapture+" cells="+frozenTileCount+" player="+PlayerState(frozenPlayer)+" native="+PlayerRaw());return;
                            }
                            if(reportedPlayerReplay)return;
                            var beforeFrozen=frozenPlayer;var beforeCurrent=currentTerrainPlayer;
                            object[] frozenArgs={frozenPlayer,frozenPlayerEnvironment,frozenPlayerTerrain,PredictionStop.None};bool frozenValid=(bool)advancePlayer.Invoke(frozenPlayerOwner,frozenArgs);frozenPlayer=(PredictionPlayerMotion)frozenArgs[0];
                            currentPlayerTerrain.Reset();object[] currentArgs={currentTerrainPlayer,frozenPlayerEnvironment,currentPlayerTerrain,PredictionStop.None};bool currentValid=(bool)advancePlayer.Invoke(currentTerrainPlayerOwner,currentArgs);currentTerrainPlayer=(PredictionPlayerMotion)currentArgs[0];
                            var original=(PredictionPlayerMotion)readPlayer.Invoke(null,new object[]{actual});double frozenError=Vector2.Distance(new Vector2(frozenPlayer.X,frozenPlayer.Y),actual.position),currentError=Vector2.Distance(new Vector2(currentTerrainPlayer.X,currentTerrainPlayer.Y),actual.position);
                            bool addedCells=((System.Collections.IDictionary)Get(frozenPlayerTerrain,"cells")).Count!=frozenTileCount;
                            if(!frozenValid || !currentValid || addedCells || frozenError>.0001 || currentError>.0001 || Math.Abs(frozenPlayer.Vx-original.Vx)>.000001 || Math.Abs(frozenPlayer.Vy-original.Vy)>.000001 || frozenPlayer.Wet!=original.Wet || frozenPlayer.SwimTime!=original.SwimTime || frozenPlayer.Jump!=original.Jump)
                            {
                                reportedPlayerReplay=true;Console.WriteLine("EYE FIRST-PLAYER-CUMULATIVE-DIFFERENCE frame="+diagnosticFrame+" capture="+playerReplayCapture+" offset="+((long)Main.GameUpdateCount-playerReplayCapture)+" frozenValid="+frozenValid+" frozenStop="+frozenArgs[3]+" frozenError="+frozenError+" currentTerrainValid="+currentValid+" currentTerrainStop="+currentArgs[3]+" currentTerrainError="+currentError+" capturedCorridorExceeded="+addedCells+" frozenBefore="+PlayerState(beforeFrozen)+" frozenAfter="+PlayerState(frozenPlayer)+" currentTerrainBefore="+PlayerState(beforeCurrent)+" currentTerrainAfter="+PlayerState(currentTerrainPlayer)+" native="+PlayerState(original)+" raw="+PlayerRaw()+" feet="+PlayerTiles(actual,frozenPlayerTerrain)+" bombs="+PrimeBombs(actual));
                            }
                        };
                        Action<NPC> replayBeforeAction=n=>
                        {
                            replayPending=null;replayValid=false;if(!diagnostic || diagnosticFrame<300 || !n.active || !(n.type==125 || n.type==126 || n.type==127 || n.aiStyle==37 && n.type>=134 && n.type<=136))return;
                            replayPlayer(n);
                            // Fork once from the complete natural collection.
                            // All later actors advance only in their actual
                            // native slot order, with the current real player
                            // supplied to isolate NPC state from PC forecasts.
                            if(replayGroup==null){var captured=Capture(read,tracking,terrain,session);var closure=new List<NpcMotionState>();foreach(var row in captured)if(row.EffectiveType==125 || row.EffectiveType==126 || row.EffectiveType==127 || row.Style==37 && row.EffectiveType>=134 && row.EffectiveType<=136)closure.Add(row);replayGroup=closure.ToArray();replayCapture=(long)Main.GameUpdateCount;Console.WriteLine("EYE CUMULATIVE START scene="+scene+" frame="+diagnosticFrame+" capture="+replayCapture+" complete Eye/Destroyer motor group="+replayGroup.Length);}
                            var env=EnvironmentFor(n);
                            if(replayFrame!=diagnosticFrame)
                            {
                                bool present=false;foreach(var row in replayGroup)if(row.Active && row.EffectiveType==135)present=true;
                                for(int i=0;i<replayGroup.Length;i++)
                                {
                                    if(replayFrame>=0 && replayGroup[i].MechDestroyerCaptured)replayGroup[i].MechDestroyerPresent=present;
                                    if(replayGroup[i].HasPlayer && replayGroup[i].PlayerIndex==env.PlayerIndex){replayGroup[i].PlayerCenterX=env.PlayerX;replayGroup[i].PlayerCenterY=env.PlayerY;replayGroup[i].PlayerVx=env.PlayerVx;replayGroup[i].PlayerVy=env.PlayerVy;replayGroup[i].PlayerVelocityCaptured=true;}
                                    if(replayGroup[i].HasClosestPlayer && replayGroup[i].ClosestPlayerIndex==env.PlayerIndex){replayGroup[i].ClosestPlayerArea=new MotionRect(env.PlayerX-env.PlayerWidth*.5f,env.PlayerY-env.PlayerHeight*.5f,env.PlayerWidth,env.PlayerHeight);replayGroup[i].ClosestPlayerCenterX=env.PlayerX;replayGroup[i].ClosestPlayerCenterY=env.PlayerY;replayGroup[i].ClosestPlayerVx=env.PlayerVx;replayGroup[i].ClosestPlayerVy=env.PlayerVy;replayGroup[i].ClosestPlayerVelocityCaptured=true;}
                                }
                                replayFrame=diagnosticFrame;
                            }
                            int index=-1;for(int i=0;i<replayGroup.Length;i++)if(replayGroup[i].Identity.Equals(new NpcIdentity(session,n,n.whoAmI,n.generation,n.type,n.netID))){index=i;break;}
                            if(index<0){if(!reportedReplay){reportedReplay=true;Console.WriteLine("EYE FIRST-CUMULATIVE-DIFFERENCE new actual identity scene="+scene+" frame="+diagnosticFrame+" type="+n.type+" slot="+n.whoAmI+" generation="+n.generation+" capture="+replayCapture);}return;}
                            replayPending=n;replayBefore=replayGroup[index];replayAfter=replayBefore;nativeBefore=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] facts={n,nativeBefore,terrain};tracking.Invoke(null,facts);nativeBefore=(NpcMotionState)facts[1];
                            terrain.Reset();replayValid=NpcMotion.Step(ref replayAfter,replayGroup,replayGroup.Length,env,terrain,diagnosticFrame-299,true,out replayStop);replayGroup[index]=replayAfter;
                        };
                        Action<NPC> replayAfterAction=n=>
                        {
                            if(!ReferenceEquals(replayPending,n))return;double error=Vector2.Distance(new Vector2(replayAfter.X,replayAfter.Y),n.position);
                            replayDetail=" cumulativeValid="+replayValid+" cumulativeStop="+replayStop+" cumulativeError="+error+" cumulativeCapture="+replayCapture+" cumulativeBefore="+State(replayBefore)+" cumulativeAfter="+State(replayAfter)+" originalBefore="+State(nativeBefore)+" originalAfter="+n.position+" velocity="+n.velocity+" rotation="+n.rotation+" phase="+string.Join("/",n.ai)+" local="+string.Join("/",n.localAI)+" anchor="+QueenAnchor(replayGroup)+" pc="+PlayerRaw();
                            if(ReferenceEquals(n,actor))actorReplayDetail=replayDetail;
                            bool difference=!replayValid || error>.0001 || Math.Abs(replayAfter.Vx-n.velocity.X)>.000001 || Math.Abs(replayAfter.Vy-n.velocity.Y)>.000001 || replayAfter.A0!=n.ai[0] || replayAfter.A1!=n.ai[1] || replayAfter.A2!=n.ai[2] || replayAfter.A3!=n.ai[3] || replayAfter.MotorRotationCaptured && Math.Abs(replayAfter.MotorRotation-n.rotation)>.000001;
                            if(difference && !reportedReplay){reportedReplay=true;Console.WriteLine("EYE FIRST-CUMULATIVE-DIFFERENCE scene="+scene+" frame="+diagnosticFrame+" type="+n.type+" slot="+n.whoAmI+replayDetail);}
                        };
                        before=n=>
                        {
                            // The independent action oracle must not reset Source's live cells.
                            terrain.Reset();
                            replayBeforeAction(n);
                            pending=n;valid=false;pendingWormRole=combo?DestroyerRole(n):-1;
                            if(!n.active || !(n.type==4 || n.type==125 || n.type==126 || n.type==127 || pendingWormRole>=0))return;
                            var group=Capture(read,tracking,terrain,session);int index=-1;for(int i=0;i<group.Length;i++)if(ReferenceEquals(group[i].Identity.Token,n)){index=i;break;}if(index<0)return;
                            predicted=group[index];var p=Main.player[n.target>=0 && n.target<255?n.target:Main.myPlayer];
                            var areas=new List<MotionRect>();var slots=new List<int>();for(int i=0;i<255;i++)if(Main.player[i].active){var box=Main.player[i].Hitbox;areas.Add(new MotionRect(box.X,box.Y,box.Width,box.Height));slots.Add(i);}
                            var e=new PredictionEnvironment{Players=new PredictionPlayers(areas.ToArray(),areas.Count,slots.ToArray()),PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerDead=p.dead || !p.active,PlayerVelocityCaptured=true,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,PlayerTimelineActive=p.active && !p.dead && !p.ghost,Expert=Main.expertMode,Day=Main.dayTime,GoodWorld=Main.getGoodWorld,Remix=Main.remixWorld,Zenith=Main.zenithWorld,Multiplayer=Main.netMode==1,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,GravityWorldSurface=Main.worldSurface,RockLayer=(float)Main.rockLayer,MechQueenUp=NPC.mechQueen>=0 && NPC.mechQueen<Main.maxNPCs && Main.npc[NPC.mechQueen].active && Main.npc[NPC.mechQueen].type==127};
                            terrain.Reset();PredictionStop stop;valid=NpcMotion.Step(ref predicted,group,group.Length,e,terrain,1,true,out stop);
                            if(!valid){if(stop==PredictionStop.RandomDestination)random++;else Require(stop==PredictionStop.MissingDependency || stop==PredictionStop.PhaseBoundary || stop==PredictionStop.Despawn || stop==PredictionStop.InvalidState,"Explicit original eye dependency/action boundary: "+stop);}
                        };
                        after=n=>
                        {
                            replayAfterAction(n);
                            if(!valid || !ReferenceEquals(n,pending))return;double error=Vector2.Distance(new Vector2(predicted.X,predicted.Y),n.position);
                            Require(error<=.08 && Math.Abs(predicted.Vx-n.velocity.X)<=.0001 && Math.Abs(predicted.Vy-n.velocity.Y)<=.0001 && predicted.Active==n.active && predicted.ReflectsProjectiles==n.reflectsProjectiles,"Original eye/Destroyer action scene="+scene+" type="+n.type+" slot="+n.whoAmI+" role="+pendingWormRole+" phase="+n.ai[0]+"/"+n.ai[1]+" error="+error+" predicted="+predicted.Vx+","+predicted.Vy+" actual="+n.velocity);
                            if(pendingWormRole>=0){Require(predicted.Alpha==n.alpha,"Original Destroyer parent-dependent alpha agrees before its motor.");wormActions[pendingWormRole]++;}else actions++;
                            if(n.type==126 && predicted.MotorRotationCaptured)Require(Math.Abs(predicted.MotorRotation-n.rotation)<=.00001f,"Native Spazmatism rotation is a real Queen flame-feedback motor input: predicted="+predicted.MotorRotation+" actual="+n.rotation);
                        };
                        for(int frame=0;frame<1800 && actor.active;frame++)
                        {
                            diagnosticFrame=frame;
                            if(frame%240==0)Console.WriteLine("EYE PROGRESS scene="+scene+" frame="+frame+" slot="+actor.whoAmI+" type="+actor.type+" phase="+string.Join("/",actor.ai)+" actions="+actions+" worm="+string.Join(",",wormActions));
                            if(frame==700 && type==4 || frame==1000 && (type==125 || type==126))
                            {
                                float ratio=type==4?(scene==1?.60f:scene==2?.30f:scene==3 || scene==5?.10f:scene==4?.03f:.40f):.35f;
                                actor.StrikeNPCNoInteraction(actor.life-(int)(actor.lifeMax*ratio)+actor.defense+10,0,0);Require(actor.active && actor.life>0 && actor.life<actor.lifeMax*(type==4?.65:.4),"Actual hit leaves a living boss below its native transform/charge threshold.");hit=true;freezes.Clear();leases.Clear();changedPremises.Clear();negativeReported.Clear();
                            }
                            if(scene==14 && frame==700){Main.dayTime=true;freezes.Clear();leases.Clear();changedPremises.Clear();negativeReported.Clear();}
                            // Stable held-input intervals are actual Player
                            // futures; changing intent retires old freezes.
                            if(frame%240==0){Main.LocalPlayer.controlRight=frame%480==0;Main.LocalPlayer.controlLeft=!Main.LocalPlayer.controlRight;freezes.Clear();leases.Clear();changedPremises.Clear();negativeReported.Clear();}
                            if(budgetStages && combo && (frame==1 || frame==30))budgetStage=(method,begin)=>stageRows.Add(Tuple.Create(method.DeclaringType.Name+"."+method.Name,(System.Diagnostics.Stopwatch.GetTimestamp()-begin)*1000.0/System.Diagnostics.Stopwatch.Frequency,((CombatPredictionWork)Get(host,"PredictionWork")).UsedMilliseconds,((CombatPredictionWork)Get(host,"PredictionWork")).Exceeded,cache.Read(0)?.Count??0));
                            NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();budgetStage=null;
                            foreach(var row in stageRows)Console.WriteLine("EYE BUDGET-STAGE scene="+scene+" frame="+frame+" stage="+row.Item1+" ms="+row.Item2.ToString("R",CultureInfo.InvariantCulture)+" ownerMs="+row.Item3.ToString("R",CultureInfo.InvariantCulture)+" hard="+row.Item4+" raw="+row.Item5);stageRows.Clear();if(!actor.active)break;
                            if(budgetStages && combo && frame==30)MeasureCompleteFirstStep(host,source,actor,scene);
                            // Freeze a newly published raw owner's exact cells
                            // before the NEXT real native terrain event. Never
                            // replace them with a later failed scratch read set.
                            var currentRaw=cache.Read(0);
                            if(!ReferenceEquals(currentRaw,sourceSnapshot)){sourceSnapshot=currentRaw;sourceSnapshotLease=currentRaw==null?null:CallResult(Get(source,"Terrain"),"FreezeCells");}
                            string phase=actor.ai[0]+"/"+actor.ai[1];observed.Add(phase);
                            bool selected=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,actor);if(selected)selectedCount++;
                            var path=NativeCombatPresentationChecks.CurrentPath(host);if(path!=null && ReferenceEquals(path.Identity.Token,actor)){Require(selected && path.SampleTick==Main.GameUpdateCount,"Current actual eye identity owns the consumable Display lease.");published++;if(lastMutationTick>=0 && path.CaptureTick>=lastMutationTick && recoveredMutation!=lastMutationTick){publicationRecoveries++;recoveredMutation=lastMutationTick;}if(frame%5==0 && path.Count>1){freezes.Add(path);phases[path]=phase;var keys=new HashSet<int>();if(combo)foreach(System.Collections.DictionaryEntry cell in NativeCombatPresentationChecks.CurrentCells(host))keys.Add((int)cell.Key);leases[path]=keys;}}
                            if(selected && path==null){missingRun++;maxMissingRun=Math.Max(maxMissingRun,missingRun);}else missingRun=0;
                            if(path!=null){displayMinFuture=Math.Min(displayMinFuture,path.Count-1);displayMaxFuture=Math.Max(displayMaxFuture,path.Count-1);}
                            if(combo && selected && path==null && (!reportedMissing || frame>=30 && !reportedSteadyMissing))
                            {
                                MissingDisplay(host,source,cache,actor,scene,frame);reportedMissing=true;if(frame>=30)reportedSteadyMissing=true;
                            }
                            // Model quality is independent of the optional
                            // Display window. Overlap the existing 120-action
                            // horizon at fixed half-window sample intervals;
                            // no seed, position or native window is searched.
                            if(combo && selected && frame%60==0)
                            {
                                object liveDisplay=GetOptional(host,"displayPath");long liveLease=(long)Get(host,"displayLeaseTick");var owner=(CombatPredictionWork)Get(host,"PredictionWork");double spent=owner.UsedMilliseconds;
                                var identity=new NpcIdentity(session,actor,actor.whoAmI,actor.generation,actor.type,actor.netID);
                                Call(componentSource,"PreparePrefix",identity,(long)Main.GameUpdateCount,120,120);
                                Require(ReferenceEquals(currentRaw,cache.Read(0)) && ReferenceEquals(liveDisplay,GetOptional(host,"displayPath")) && liveLease==(long)Get(host,"displayLeaseTick") && spent==owner.UsedMilliseconds,"Independent full-model Source cannot publish, renew Display, or spend/reset the live budget.");
                                var component=componentCache.Read(0);
                                if(component!=null)
                                {
                                    Require(component.Identity.Equals(identity) && component.CaptureTick==(long)Main.GameUpdateCount && component.SampleTick==component.CaptureTick && !component.BudgetPaused && (component.Stop!=PredictionStop.None || component.Count==121),"Independent Eye model preserves the full requested horizon or its real mechanism stop.");
                                    freezes.Add(component);components.Add(component);phases[component]=phase;leases[component]=LeaseKeys(CallResult(Get(componentSource,"Terrain"),"FreezeCells"));componentCaptures++;
                                }
                            }
                            for(int i=freezes.Count-1;i>=0;i--)
                            {
                                var frozen=freezes[i];int offset=(int)((long)Main.GameUpdateCount-frozen.SampleTick);if(offset>=frozen.Count){freezes.RemoveAt(i);leases.Remove(frozen);components.Remove(frozen);changedPremises.Remove(frozen);negativeReported.Remove(frozen);phases.Remove(frozen);continue;}if(offset<=0)continue;bool component=components.Contains(frozen);
                                var point=frozen[offset];double error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),actor.position);
                                if(changedPremises.Contains(frozen))
                                {
                                    negativeMax=Math.Max(negativeMax,error);
                                    if(error>.08 && negativeReported.Add(frozen)){negativeDifferences++;Console.WriteLine("EYE CHANGED-TERRAIN-NEGATIVE scene="+scene+" frame="+frame+" capture="+frozen.CaptureTick+" offset="+offset+" error="+error+" mutationTick="+lastMutationTick+" predicted="+point.Bounds.X+","+point.Bounds.Y+" actual="+actor.position+" pc="+PlayerRaw()+actorReplayDetail);}
                                    continue;
                                }
                                maxError=Math.Max(maxError,error);if(component)componentMatches++;else matched++;if(lastMutationTick>=0 && frozen.CaptureTick>=lastMutationTick && offset>=30)recoveredLong++;
                                Require(error<=.08 && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal) && point.NoTileCollide==actor.noTileCollide && point.ReflectsProjectiles==actor.reflectsProjectiles && !point.ProjectileReflectionUnknown,"Frozen eye phase motor component="+component+" scene="+scene+" frame="+frame+" capture="+frozen.CaptureTick+" offset="+offset+" phase="+phase+" error="+error+" predicted="+point.Bounds.X+","+point.Bounds.Y+" predictedV="+point.Vx+","+point.Vy+" actual="+actor.position+" actualV="+actor.velocity+" stop="+frozen.Stop+" pc="+PlayerRaw()+actorReplayDetail);
                                if((!combo || component) && offset>=30){longMatches++;tangent=Math.Max(tangent,Vector2.Distance(actor.position,new Vector2(frozen[0].Bounds.X+frozen[0].Vx*offset,frozen[0].Bounds.Y+frozen[0].Vy*offset)));}if((!combo || component) && phases[frozen]!=phase)cross++;
                            }
                            rows.Add(string.Join(",",scene,frame,type,actor.ai[0],actor.ai[1],selected,path!=null,path?.Count??0,path?.Stop.ToString()??"none",matched,longMatches,maxError.ToString("R",CultureInfo.InvariantCulture),cross,actions,random,componentCaptures,componentMatches,sourceRetired,displayRetired,publicationRecoveries));
                        }
                        before=after=null;killing=null;explosion=null;worldPrepare=null;
                        Console.WriteLine("EYE AVAILABILITY scene="+scene+" selected="+selectedCount+" pub="+published+" displayMatched="+matched+" displayMinFuture="+(published==0?0:displayMinFuture)+" displayMaxFuture="+displayMaxFuture+" maximumMissingRun="+maxMissingRun+" componentCaptures="+componentCaptures+" componentMatched="+componentMatches+" componentLong="+longMatches+" componentCross="+cross+" sourceRetired="+sourceRetired+" displayRetired="+displayRetired+" displayRecoveries="+publicationRecoveries);
                        Require(actions>=300,"Hundreds of original scheduled boss actions were compared: "+scene);
                        Require(selectedCount>0 && published>0 && matched>0 && (!combo || componentCaptures>0 && componentMatches>0) && longMatches>=30 && tangent>8 && cross>=30,"Actual Eye Display consumes native futures and independent long models cross deterministic phases: "+scene+" selected="+selectedCount+" pub="+published+" displayMatched="+matched+" componentCaptures="+componentCaptures+" componentMatched="+componentMatches+" componentLong="+longMatches+" cross="+cross+" tangent="+tangent);
                        if(combo)
                        {
                            Require(mutations>0 && publicationRecoveries>0 && recoveredLong>=30,"Actual combo explosions are followed by real new-capture Display recovery and independently matched long-model futures on new terrain samples.");
                            if(scene==17)Require(invalidated>0 && negativeDifferences>0 && negativeMax>.08,"Scene17 retains the original changed-terrain frozen mismatch as a negative premise case.");
                            Console.WriteLine("EYE TERRAIN-RECOVERY scene="+scene+" mutations="+mutations+" displayRecoveries="+publicationRecoveries+" sourceRetired="+sourceRetired+" displayRetired="+displayRetired+" invalidated="+invalidated+" oldNegative="+negativeDifferences+" oldMax="+negativeMax+" componentRecoveredLong="+recoveredLong);
                        }
                        if(type!=127)Require(hit && observed.Contains("1/0") && observed.Contains("2/0") && (observed.Contains("3/0") || type==4 && scene>=3),"Actual receiving transform phases and second form were reached: "+scene);
                        if(type==4 && scene>=2 && scene<=5)Require(random>0 && (observed.Contains("3/3") || observed.Contains("3/4")),"Original low-life motor RNG and observed charges were exercised.");
                        if(scene==14)Require(actor.ai[1]==2,"Actual dawn changes Prime to the native daytime chase.");
                        if(combo){for(int i=0;i<wormActions.Length;i++)Require(wormActions[i]>=300,"Genuine whole-chain MechQueen geometry role="+i+" compares hundreds of original slot actions.");Console.WriteLine("EYE destroyer scene="+scene+" head,count1,count9,count10,count11,tail="+string.Join(",",wormActions));}
                        totalActions+=actions;totalMatched+=matched;totalRandom+=random;Console.WriteLine("EYE scene="+scene+" type="+type+" combo="+combo+" actions="+actions+" selected="+selectedCount+" pub="+published+" displayMatched="+matched+" displayMinFuture="+(published==0?0:displayMinFuture)+" displayMaxFuture="+displayMaxFuture+" maximumMissingRun="+maxMissingRun+" componentCaptures="+componentCaptures+" componentMatched="+componentMatches+" componentLong="+longMatches+" componentCross="+cross+" random="+random+" error="+maxError+" phases="+string.Join(";",observed));
                    }
                    catch(Exception failure) when(!(failure is OutOfMemoryException)){if(!JueMingR.Testing.BusinessAssertionProtocol.IsAssertion(failure))throw;before=after=null;killing=null;explosion=null;worldPrepare=null;failures.Add(scene+": "+failure.Message);Console.WriteLine("EYE FAIL scene="+scene+": "+failure);}
                }
                Require(failures.Count==0,"Original eye natural failures="+failures.Count+": "+string.Join(" | ",failures));Console.WriteLine("PASS eye/Prime/MechQueen natural scenes="+(last-first)+" scope="+(single>=0?"existing-combo-scene-"+single:comboOnly?"existing-combo-scenes-15-16-17":"full-default")+" matched="+totalMatched+" originalActions="+totalActions+" random="+totalRandom);
            }
            finally{before=after=null;killing=null;explosion=null;worldPrepare=null;budgetStage=null;observer.Unpatch(update,HarmonyPatchType.All,observer.Id);observer.Unpatch(kill,HarmonyPatchType.All,observer.Id);observer.Unpatch(explode,HarmonyPatchType.All,observer.Id);observer.Unpatch(prepareWorld,HarmonyPatchType.All,observer.Id);if(budgetStages)foreach(var method in budgetMethods)observer.Unpatch(method,HarmonyPatchType.All,observer.Id);Main.getGoodWorld=oldGood;Main.remixWorld=oldRemix;Main.zenithWorld=oldZenith;Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"eye-boss-continuous.csv"),rows);}
        }
        private static object CallResult(object target,string method)
        {return target.GetType().GetMethod(method,Flags).Invoke(target,null);}
        private static bool HasFutureAt(NpcTrajectory path,long tick)
        {return path!=null && tick>=path.SampleTick && tick-path.SampleTick<path.Count-1;}
        private static HashSet<int> LeaseKeys(object lease)
        {var keys=new HashSet<int>();if(lease!=null)foreach(object pair in (Array)Get(lease,"values"))keys.Add((int)Get(pair,"Key"));return keys;}
        private static bool LeaseChanged(object lease)
        {
            if(lease==null)return false;
            int width=(int)Get(lease,"width");if(width!=Main.maxTilesX || (int)Get(lease,"height")!=Main.maxTilesY)return true;
            var read=lease.GetType().DeclaringType.GetMethod("ReadCell",Flags);
            foreach(object pair in (Array)Get(lease,"values"))
            {int key=(int)Get(pair,"Key");object[] args={key%width,key/width,null};if(!(bool)read.Invoke(null,args) || !Get(pair,"Value").Equals(args[2]))return true;}
            return false;
        }
        // A single independent private Source answers whether the COMPLETE
        // unit can fit, after a timed production attempt stopped mid-unit.
        // It cannot publish into Host, lend a lease or alter production state.
        private static void MeasureCompleteFirstStep(object host,object source,NPC actor,int scene)
        {
            var liveCache=(NpcPredictionCache)Get(source,"Cache");var liveRaw=liveCache.Read(0);object liveDisplay=GetOptional(host,"displayPath");long liveLease=(long)Get(host,"displayLeaseTick");var work=(CombatPredictionWork)Get(host,"PredictionWork");double liveSpent=work.UsedMilliseconds;
            object independent=Activator.CreateInstance(source.GetType(),Flags,null,new object[]{null,false},CultureInfo.InvariantCulture);
            var ownCache=(NpcPredictionCache)Get(independent,"Cache");ownCache.Demand(0,1,120);
            var identity=new NpcIdentity((long)Get(host,"Session"),actor,actor.whoAmI,actor.generation,actor.type,actor.netID);
            var clock=System.Diagnostics.Stopwatch.StartNew();source.GetType().GetMethod("PreparePrefix",Flags).Invoke(independent,new object[]{identity,(long)Main.GameUpdateCount,1,120});clock.Stop();
            double completeMs=clock.Elapsed.TotalMilliseconds;var model=Get(independent,"rolling");var sample=(NpcMotionState[])((NpcMotionState[])Get(independent,"states")).Clone();int count=(int)Get(model,"resultCount"),selected=(int)Get(model,"resultSelected");
            var environment=(PredictionEnvironment)Get(model,"resultEnvironment");var player=(PredictionPlayerMotion)Get(model,"resultPlayer");var cells=(IPredictionTerrain)Activator.CreateInstance(Get(independent,"Terrain").GetType(),true);
            var isolated=new RollingNpcPrediction();var roles=(bool[])((bool[])Get(independent,"motionRoles")).Clone();
            clock.Restart();var raw=isolated.Prepare(sample,count,selected,(long)Main.GameUpdateCount,1,(long)Get(model,"resultEpoch"),environment,player,cells,roles,(bool)Get(model,"resultPlayerAdvanced"),120);clock.Stop();
            Console.WriteLine("EYE COMPLETE-UNIT scene="+scene+" frame=30 independent=true chargedToProduction=false closure="+count+" sourceAndStepMs="+completeMs.ToString("R",CultureInfo.InvariantCulture)+" isolatedStepMs="+clock.Elapsed.TotalMilliseconds.ToString("R",CultureInfo.InvariantCulture)+" sourceCount="+(ownCache.Read(0)?.Count??0)+" modelCount="+(raw?.Count??0)+" stop="+(raw?.Stop.ToString()??"null"));
            Require(ReferenceEquals(liveRaw,liveCache.Read(0)) && ReferenceEquals(liveDisplay,GetOptional(host,"displayPath")) && liveLease==(long)Get(host,"displayLeaseTick") && liveSpent==work.UsedMilliseconds,"Independent complete-unit cost measurement cannot change the live publication, display lease or settled budget.");
        }
        // One pure-value vector at the first selected miss and first steady
        // miss. This observes existing owners; it never prepares or renews one.
        private static void MissingDisplay(object host,object source,NpcPredictionCache cache,NPC actor,int scene,int frame)
        {
            var selection=Get(host,"Selection");var work=Get(host,"PredictionWork");var raw=GetOptional(cache,"result") as NpcTrajectory;
            Console.WriteLine("EYE MISSING-DISPLAY scene="+scene+" frame="+frame+" tick="+Main.GameUpdateCount+" actor="+actor.whoAmI+"/"+actor.generation+"/"+actor.type+" selection="+Identity((NpcIdentity)Get(selection,"Target"))+" host{"+Fields(host,"Path","pathFailed","TargetPredictionFailed","optionalTick","displayShortTick","displayLeaseTick")+"} settings{"+Fields(Get(host,"Settings"),"CanRun")+"} option{"+Fields(Get(host,"Options"),"Path")+"} hooks{"+Fields(Get(host,"Hooks"),"Ready")+"} world{"+Fields(Get(host,"World"),"Failed")+"} demand="+cache.Required+"/"+cache.MinimumRequired+" work{"+Fields(work,"UsedMilliseconds","Exceeded")+"} read="+Trajectory(cache.Read(0))+" raw="+Trajectory(raw)+" display="+Trajectory(GetOptional(host,"displayPath") as NpcTrajectory));
            long outcome=(long)Get(source,"outcomeTick");var key=(NpcIdentity)Get(selection,"Target");object[] empty={key,(long)Main.GameUpdateCount,PredictionStop.None,PredictionFailureLayer.None};bool emptyValid=(bool)source.GetType().GetMethod("EmptyFutureReason",Flags).Invoke(source,empty);
            Console.WriteLine("EYE MISSING-SOURCE "+Fields(source,"outcomeTick","outcomeFuture","outcomeStop","outcomeLayer","geometryTick","observedCount")+" outcomeIdentity="+Identity((NpcIdentity)Get(source,"outcomeIdentity"))+" emptyValid="+emptyValid+(emptyValid?" emptyStop="+empty[2]+" emptyLayer="+empty[3]:""));
            var rolling=Get(source,"rolling");long resultTick=(long)Get(rolling,"resultTick");
            Console.WriteLine("EYE MISSING-ROLLING current="+(resultTick==(long)Main.GameUpdateCount)+" "+Fields(rolling,"resultTick","resultCount","resultSelected","FailureLayer","resumable")+(resultTick==(long)Main.GameUpdateCount?" result="+Trajectory(GetOptional(rolling,"result") as NpcTrajectory)+" progress{"+Fields(Get(rolling,"progress"),"Length")+"}":""));
            var states=(NpcMotionState[])Get(source,"states");var roles=(bool[])Get(source,"motionRoles");int count=(int)Get(source,"observedCount");var summary=new Dictionary<string,int>();bool complete=true;string firstBad="none";var details=new List<string>();
            for(int i=0;i<count;i++)
            {
                var n=states[i];string kind=n.EffectiveType+"/"+n.Style+"/motor="+roles[i];int prior;summary.TryGetValue(kind,out prior);summary[kind]=prior+1;
                bool member=n.EffectiveType==125 || n.EffectiveType==126 || n.EffectiveType==127 || n.Style==37;
                if(member && (!n.EyeRelationsCaptured || !n.EyeRelationsComplete || !n.MechFactsCaptured)){complete=false;if(firstBad=="none")firstBad=Identity(n.Identity);}
                if(n.EffectiveType==125 || n.EffectiveType==126 || n.EffectiveType==127)details.Add(Identity(n.Identity)+" eye="+n.EyeRelationsCaptured+"/"+n.EyeRelationsComplete+" mech="+n.MechFactsCaptured+" queen="+Identity(n.MechQueenIdentity)+" link="+Identity(n.MechLinkIdentity)+" destroyer="+n.MechDestroyerCaptured+"/"+n.MechDestroyerPresent);
            }
            var kinds=new List<string>();foreach(var item in summary)kinds.Add(item.Key+"="+item.Value);
            var player=Main.LocalPlayer;
            Console.WriteLine("EYE MISSING-CLOSURE sourceCurrent="+(outcome==(long)Main.GameUpdateCount)+" count="+count+" kinds="+string.Join(";",kinds)+" relationsComplete="+complete+" firstBad="+firstBad+" members="+string.Join(";",details)+" mechQueen="+NPC.mechQueen+" player="+player.active+"/"+player.dead+"/"+player.ghost+(resultTick==(long)Main.GameUpdateCount?" capturedPlayer{"+Fields(Get(rolling,"resultPlayer"),"InvalidMechanism")+"}":""));
        }
        private static string Fields(object owner,params string[] names)
        {var values=new List<string>();foreach(string name in names)values.Add(name+"="+(GetOptional(owner,name)??"<missing>"));return string.Join(" ",values);}
        private static string Identity(NpcIdentity n)
        {return n.Session+"/"+n.Slot+"/"+n.Generation+"/"+n.Type+"/"+n.NetId+"/token="+(n.Token!=null);}
        private static string Trajectory(NpcTrajectory path)
        {return path==null?"null":Identity(path.Identity)+" cap="+path.CaptureTick+" sample="+path.SampleTick+" count="+path.Count+" stop="+path.Stop+" quality="+path.Quality+" assumptions="+path.Assumptions+" paused="+path.BudgetPaused+" p0="+path[0].Bounds.X+","+path[0].Bounds.Y+" V="+path[0].Vx+","+path[0].Vy;}
        private static void Setup(object context)
        {
            NativeCombatLiveContextChecks.FlightWorld();Main.Map=new Terraria.Map.WorldMap(Main.maxTilesX,Main.maxTilesY);NPC.ClearAll();Projectile.ClearAll();NPC.mechQueen=-1;Main.netMode=0;Main.dayTime=false;Main.time=1800;Main.getGoodWorld=Main.remixWorld=Main.zenithWorld=false;
            if(Main.ItemDropSolver==null){Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);}
            for(int i=0;i<255;i++)Main.player[i].active=false;
            Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};var p=Main.LocalPlayer;p.position=new Vector2(1100,2400-p.height);p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.creativeGodMode=true;p.immune=true;p.immuneTime=1000000;p.fallStart=p.fallStart2=(int)p.position.Y/16;for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions(path:false,mouseCenter:true,clearLine:false,radius:50));
        }
        private static NPC Find(int type){for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type==type)return Main.npc[i];return null;}
        private static PredictionEnvironment EnvironmentFor(NPC n)
        {
            var p=Main.player[n.target>=0 && n.target<255?n.target:Main.myPlayer];var areas=new List<MotionRect>();var slots=new List<int>();for(int i=0;i<255;i++)if(Main.player[i].active){var box=Main.player[i].Hitbox;areas.Add(new MotionRect(box.X,box.Y,box.Width,box.Height));slots.Add(i);}
            return new PredictionEnvironment{Players=new PredictionPlayers(areas.ToArray(),areas.Count,slots.ToArray()),PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerDead=p.dead || !p.active,PlayerVelocityCaptured=true,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,PlayerTimelineActive=p.active && !p.dead && !p.ghost,Expert=Main.expertMode,Day=Main.dayTime,GoodWorld=Main.getGoodWorld,Remix=Main.remixWorld,Zenith=Main.zenithWorld,Multiplayer=Main.netMode==1,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,GravityWorldSurface=Main.worldSurface,RockLayer=(float)Main.rockLayer,MechQueenUp=NPC.mechQueen>=0 && NPC.mechQueen<Main.maxNPCs && Main.npc[NPC.mechQueen].active && Main.npc[NPC.mechQueen].type==127};
        }
        private static string State(NpcMotionState n)
        {
            return "slot="+n.Identity.Slot+":"+n.Identity.Generation+" type="+n.EffectiveType+" pos="+F(n.X)+","+F(n.Y)+" V="+F(n.Vx)+","+F(n.Vy)+" oldPos="+F(n.OldX)+","+F(n.OldY)+" oldV="+F(n.OldVx)+","+F(n.OldVy)+" ai="+F(n.A0)+"/"+F(n.A1)+"/"+F(n.A2)+"/"+F(n.A3)+" local="+F(n.L0)+"/"+F(n.L1)+"/"+F(n.L2)+"/"+F(n.L3)+" target="+n.Target+" player="+n.PlayerIndex+":"+F(n.PlayerCenterX)+","+F(n.PlayerCenterY)+" V="+F(n.PlayerVx)+","+F(n.PlayerVy)+" queen="+n.MechQueenIdentity.Slot+":"+n.MechQueenIdentity.Generation+" present="+n.MechDestroyerPresent+" rotation="+n.MotorRotationCaptured+":"+F(n.MotorRotation)+" direction="+n.Direction+","+n.DirectionY+","+n.SpriteDirection+" timeLeft="+n.TimeLeft+" gravity="+F(n.Gravity)+","+F(n.MaxFall)+" netOffset="+F(n.NetOffsetX)+","+F(n.NetOffsetY)+" collide="+n.CollideX+","+n.CollideY;
        }
        private static string QueenAnchor(NpcMotionState[] group)
        {
            string predicted="absent";foreach(var row in group)if(row.Active && row.EffectiveType==127){predicted=State(row)+" rawCenter="+F(row.Bounds.CenterX)+","+F(row.Bounds.CenterY)+" anchorY="+F(row.Bounds.CenterY-14);break;}
            int slot=NPC.mechQueen;if(slot<0 || slot>=Main.maxNPCs)return "replay["+predicted+"] nativeGlobal="+slot;
            var actual=Main.npc[slot];return "replay["+predicted+"] native slot="+slot+":"+actual.generation+" pos="+actual.position+" V="+actual.velocity+" rawCenter="+actual.Center+" anchorY="+F(actual.Center.Y-14)+" rotation="+actual.rotation+" phase="+string.Join("/",actual.ai);
        }
        private static string PlayerRaw()
        {
            var p=Main.LocalPlayer;return "slot="+p.whoAmI+" pos="+F(p.position.X)+","+F(p.position.Y)+" V="+F(p.velocity.X)+","+F(p.velocity.Y)+" size="+p.width+","+p.height+" controls="+p.controlLeft+","+p.controlRight+","+p.controlUp+","+p.controlDown+","+p.controlJump+" releaseJump="+p.releaseJump+" film="+p.isControlledByFilm+" dead="+p.dead+" ghost="+p.ghost+" hp="+p.statLife+" immune="+p.immune+":"+p.immuneTime+" fall="+p.fallStart+","+p.fallStart2+" respawn="+p.respawnTimer;
        }
        private static string PlayerState(PredictionPlayerMotion p)
        {
            return "pos="+F(p.X)+","+F(p.Y)+" V="+F(p.Vx)+","+F(p.Vy)+" gravity="+F(p.Gravity)+","+F(p.DefaultGravity)+","+F(p.GravityDirection)+" maxFall="+F(p.MaxFall)+" accel="+F(p.Acceleration)+" speed="+F(p.MaxSpeed)+","+F(p.FastMaxSpeed)+" controls="+p.Left+","+p.Right+","+p.Up+","+p.Down+","+p.HoldJump+" release="+p.ReleaseJump+" jump="+p.Jump+","+p.JumpHeight+" vertical="+p.VerticalProfile+" swim="+p.SwimTime+" wet="+p.Wet+","+p.Honey+","+p.Lava+","+p.Shimmer+" mount="+p.Mounted+","+p.Hover+" border="+p.BordersRemix+","+p.BordersGodMode+" complex="+p.Complex+","+p.InvalidMechanism;
        }
        private static string PlayerTiles(Player p,IPredictionTerrain captured)
        {
            var result=new List<string>();int left=(int)(p.position.X/16)-1,right=(int)((p.position.X+p.width)/16)+1,bottom=(int)((p.position.Y+p.height)/16);
            for(int x=left;x<=right;x++)for(int y=bottom-1;y<=bottom+1;y++)
            {
                if(x<0 || y<0 || x>=Main.maxTilesX || y>=Main.maxTilesY)continue;var actual=Main.tile[x,y];PredictionTile old;PredictionStop stop;bool available=captured.Tile(x,y,out old,out stop);
                result.Add(x+":"+y+" captured="+(available?old.Active+":"+old.Type+":"+old.Liquid+":"+old.Slope+":"+old.Half:stop.ToString())+" native="+(actual==null?"null":actual.active()+":"+actual.type+":"+actual.liquid+":"+actual.slope()+":"+actual.halfBrick()));
            }return string.Join(";",result);
        }
        private static string PrimeBombs(Player p)
        {
            var result=new List<string>();for(int i=0;i<Main.maxProjectiles;i++){var bomb=Main.projectile[i];if(bomb.type!=102 || Vector2.Distance(bomb.Center,p.Center)>600)continue;result.Add(i+":"+bomb.active+" pos="+bomb.position+" V="+bomb.velocity+" life="+bomb.timeLeft+" ai="+string.Join("/",bomb.ai));}return string.Join(";",result);
        }
        private static string TileText(int x,int y)
        {var t=Main.tile[x,y];return t==null?"null":"active="+t.active()+" inactive="+t.inActive()+" type="+t.type+" slope="+t.slope()+" half="+t.halfBrick()+" wall="+t.wall+" liquid="+t.liquid;}
        private static string F(float value){return value.ToString("R",CultureInfo.InvariantCulture);}
        private static int DestroyerRole(NPC n)
        {
            if(!n.active || n.aiStyle!=37)return -1;if(n.type==134)return 0;if(n.type==136)return 5;if(n.type!=135)return -1;
            // Inspect only the actual pre-action parent values. Count ten is
            // retained when the tenth ancestor is the head: native checks that
            // type before its >=10 cutoff. Count eleven has normal spacing.
            int slot=(int)n.ai[1],distance=0;
            while(slot>0 && slot<Main.maxNPCs && distance<11)
            {
                var parent=Main.npc[slot];if(!parent.active || parent.type<134 || parent.type>136)return -1;distance++;
                if(parent.type==134)return distance==1?1:distance==9?2:distance==10?3:distance==11?4:-1;
                slot=(int)parent.ai[1];
            }
            return -1;
        }
        private static NpcMotionState[] Capture(MethodInfo read,MethodInfo tracking,IPredictionTerrain terrain,long session)
        {
            var states=new List<NpcMotionState>();for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active)
            {
                var n=Main.npc[i];var s=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,s,terrain};tracking.Invoke(null,args);s=(NpcMotionState)args[1];s.EyeRelationsCaptured=s.EyeRelationsComplete=true;s.MechDestroyerCaptured=true;s.MechDestroyerPresent=NPC.npcsFoundForCheckActive[135];states.Add(s);
            }
            return states.ToArray();
        }
    }
}
