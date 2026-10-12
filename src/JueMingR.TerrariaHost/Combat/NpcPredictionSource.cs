using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class NpcPredictionSource
    {
        internal readonly NpcPredictionCache Cache=new NpcPredictionCache();
        internal readonly PredictionTerrain Terrain=new PredictionTerrain();
        internal readonly Prediction.NativePredictionSession Native;
        private readonly Prediction.SegmentedNpcPrediction segmented=new Prediction.SegmentedNpcPrediction();
        private bool usingSegmented;
        private readonly RollingNpcPrediction rolling=new RollingNpcPrediction();
        private NpcPredictionSource candidateSource;
        private bool candidateOnly;
        private static bool rollingCodePrepared;

        // New candidates keep a separate finite sampling/terrain/history owner.
        // The incumbent uses its existing selected owner once per exact sample;
        // evaluating other candidates never swaps or restores that history.
        internal NpcTrajectory TryCandidate(NpcIdentity identity,long tick,bool beforeNpc,int required)
        {return TryCandidateReuse(identity,tick,beforeNpc,required,false);}
        // reuseMiss is permission from the attack owner only after its own
        // prior no-contact result and ALL attack premises still match. Source
        // independently resamples the whole NPC/player input before skipping.
        internal NpcTrajectory TryCandidateReuse(NpcIdentity identity,long tick,bool beforeNpc,int required,bool reuseMiss)
        {
            if(required<=0 || !CombatSelection.Valid(identity,identity.Session))return null;
            // Distance order still decides which candidate is visited first.
            // Only the target already owned by this Source may use this route;
            // feeding every nearest candidate here would erase a farther
            // selected actor's observation/trend on every clear-line update.
            // Final preparation rechecks the normal full sample/player/cell
            // identity and returns the same rolling result only when exact.
            if(!candidateOnly && Native==null && !usingSegmented && ownsState && outcomeIdentity.Equals(identity) && Cache.Required>=required)
            {
                try{if(beforeNpc)PrepareAction(identity,tick);else Prepare(identity,tick);}
                catch(NpcObservationFailure){Clear();return null;}
                // Path and Aim own separate demands. Sharing an already
                // published sample must use its live consumer, not require
                // the display demand when only Aim is enabled.
                var selectedPath=Cache.Read(0)??Cache.Read(1);
                return selectedPath!=null && selectedPath.Identity.Equals(identity) && selectedPath.CaptureTick==tick?selectedPath:null;
            }
            if(candidateSource==null)candidateSource=new NpcPredictionSource{candidateOnly=true};
            candidateSource.Cache.Demand(0,1,Math.Min(required,NpcPredictionCache.Horizon));
            candidateSource.candidateReuseMiss=reuseMiss;
            try{if(beforeNpc)candidateSource.PrepareAction(identity,tick);else candidateSource.Prepare(identity,tick);}
            catch(NpcObservationFailure){candidateSource.Clear();return null;}
            finally{candidateSource.candidateReuseMiss=false;}
            var path=candidateSource.Cache.Read(0);
            return path!=null && path.Identity.Equals(identity) && path.CaptureTick==tick?path:null;
        }
        // currentTick is Main.GameUpdateCount at the real player observation,
        // NOT timeline.SampleTick. PrepareAction stamps GUC-1 (NPC point zero
        // is the previous world, while this player already moved). Thus the
        // next real player action consumes source index GUC-SampleTick: 1 for
        // fresh BeforeNpc, 0 for CompletedWorld, and 2 for BeforeNpc Age1.
        // Clock.MovePlayer still decides whether step0 moves; launch-delay
        // pre-roll consumes this same sequence through actual Advance calls.
        // A window slices source and NPC points by the same offset; capture
        // identity/time remain unchanged. No last Source/global is consulted.
        internal static bool TryBindPlayerGravity(NpcTrajectory timeline,long currentTick,ref PredictionPlayerMotion player,ref PredictionEnvironment environment)
        {
            if(timeline==null || timeline.GravitySources==null)return false;
            player.GravitySourceExpected=true;var values=timeline.GravitySources;
            if(!values.TargetIdentity.Equals(timeline.Identity) || values.CaptureTick!=timeline.CaptureTick || !ReferenceEquals(values.PlayerToken,player.PlayerToken) || values.PlayerIndex!=player.PlayerIndex)return false;
            long age=currentTick-timeline.SampleTick;if(age<0 || age>=values.Count)return false;
            environment.GravitySources=values;player.GravityAction=(int)age;return true;
        }
        // Identical token/window/ordinal contract to the gravity payload. A
        // prepared clone receives the already-bound motion; never rebind it.
        internal static bool TryBindPlayerWall(NpcTrajectory timeline,long currentTick,ref PredictionPlayerMotion player,ref PredictionEnvironment environment)
        {
            if(timeline==null || timeline.WallSources==null)return false;
            player.WallSourceExpected=true;var values=timeline.WallSources;
            if(!values.TargetIdentity.Equals(timeline.Identity) || values.CaptureTick!=timeline.CaptureTick || !ReferenceEquals(values.PlayerToken,player.PlayerToken) || values.PlayerIndex!=player.PlayerIndex)return false;
            long age=currentTick-timeline.SampleTick;if(age<0 || age>=values.Count)return false;
            environment.WallSources=values;player.WallAction=(int)age;return true;
        }
        private int observedCount,targetPlayer=-1;
        private long epoch,geometryTick=-1;
        private bool ownsState;
        // Only the current synchronous sample's scalar outcome is retained.
        // This is not a second trajectory/cache and cannot turn point0 into a future.
        private NpcIdentity outcomeIdentity;
        private long outcomeTick=-1;
        private PredictionStop outcomeStop;
        private PredictionFailureLayer outcomeLayer;
        private int outcomeFuture;
        internal int OutcomeStyle {get;private set;}
        internal float OutcomePhase {get;private set;}
        internal PredictionStrategy OutcomeStrategy {get;private set;}
        internal bool EmptyFutureReason(NpcIdentity identity,long tick,out PredictionStop stop,out PredictionFailureLayer layer)
        {stop=outcomeStop;layer=outcomeLayer;return outcomeTick==tick && outcomeIdentity.Equals(identity) && outcomeFuture==0 && stop!=PredictionStop.None && CombatSelection.Valid(identity,identity.Session);}
        private void Outcome(NpcIdentity identity,long tick,NpcTrajectory path,PredictionFailureLayer layer)
        {outcomeIdentity=identity;outcomeTick=tick;outcomeStop=path?.Stop??PredictionStop.InvalidState;outcomeFuture=Math.Max(0,(path?.Count??0)-1);outcomeLayer=layer;OutcomeStrategy=path?.Strategy??PredictionStrategy.RollingConditional;var n=Main.npc[identity.Slot];OutcomeStyle=n.aiStyle;OutcomePhase=n.ai[0];}
        // Authentication alone never enables the expensive comparison route.
        // Only isolated comparison fixtures explicitly opt into native proof.
        internal NpcPredictionSource(Prediction.PredictionLaunchIdentity launch=null,bool exactComparison=false)
        {
            if(exactComparison){if(launch==null)throw new ArgumentNullException(nameof(launch));Native=new Prediction.NativePredictionSession(launch,Cache);}
            else PrepareRollingCode();
        }
        private static void PrepareRollingCode()
        {
            // Composition and the lazy candidate owner are game-thread only.
            // JIT code belongs to this loaded assembly, not each Source world
            // lifetime; candidate construction must not repeat reflection.
            if(rollingCodePrepared)return;
            // Code preparation belongs to feature composition, once per owner.
            // Compile the bounded local kernels; do NOT sample Main, construct
            // forecasts or run a hidden warmup world. Startup is measured
            // separately; OFF world updates still do no prediction work.
            var types=new[]{typeof(NpcPredictionSource),typeof(NpcTrackingObservation),typeof(NpcPositionObservation),typeof(NpcCollisionRules),typeof(RollingNpcPrediction),typeof(NpcMotion),typeof(NpcHealth),typeof(PredictionTerrain),typeof(NpcTrajectory),
                typeof(MotionRect),typeof(NpcMotionState),typeof(NpcTrajectoryPoint),typeof(NpcBuffLayout),typeof(PredictionPlayers),typeof(PredictionMageCorpses),typeof(PredictionTowerShieldConsumer),typeof(PredictionTowerShieldConsumers),typeof(PredictionGravitySource),typeof(PredictionGravitySources),typeof(PredictionWallPlayer),typeof(PredictionWallPlayers),typeof(PredictionWallSource),typeof(PredictionWallSources),typeof(NpcTrajectory).GetNestedType("PackedPoint",System.Reflection.BindingFlags.NonPublic),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcRollingMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcFiniteFlightMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcPhasedFlightMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcAmbientFlightMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcRelatedMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcTerrainFlightMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcPlanteraMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcBigMimicMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcInvasionTargeting",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcInvasionFlightMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcInvasionLightningMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcInvasionCrystalMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcInvasionGroundMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcEyeBossMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcMechanicalBossMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcSnowmanMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcSegmentedWormMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcCasterMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcMartianTurretMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcKingSlimeMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcCelestialTowerMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcPumpkingMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcIceQueenMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcCultistBossMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcCultistRitualMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcMoonLordMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcMoonLeechMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcBirdMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcEmpressMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcDeerclopsMotion",true),
                typeof(PredictionDeerclopsPlayer),typeof(PredictionDeerclopsPlayers),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcWindyBalloonMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcWindySlimePrelude",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcQueenSlimeMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcTreeBossMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcSantaTankMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcSkeletonBossMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcAttachedWeaponMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcDiveFlightMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcBloodNautilusMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcGolemMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcBrainSwarmMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcQueenBeeMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcWallBossMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcBetsyMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcDarkMageMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcTownCritterMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcFlyingMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcEyeMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcBatMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcChargeMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcMimicMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcJellyfishMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcRunningMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcSupportMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcHungryMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcStraightMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcTargeting",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcPositionMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcGroundMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcFighterEntryMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcFighterFlightMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcFighterClockMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcSlimeControl",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.PlayerHorizontalMotion",true),
                typeof(PlayerVerticalMotion),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcGravityMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.FighterHorizontalMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcAquaticMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcAquaticActionMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcAnchoredMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcWallMotion",true),
                typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcWormMotion",true)};
            foreach(var type in types)
            {
                foreach(var method in type.GetMethods(System.Reflection.BindingFlags.DeclaredOnly|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static))
                    if(!method.IsAbstract && !method.ContainsGenericParameters)System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(method.MethodHandle);
                foreach(var constructor in type.GetConstructors(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance))
                    System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(constructor.MethodHandle);
            }
            // ReadPlayer's mode query is a leaf on the default route, even
            // though the remaining NativePlayerMotion code is comparison-only.
            foreach(string name in new[]{"Conditional","Mechanism","SupportedHover"})
                System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(typeof(Prediction.NativePlayerMotion).GetMethod(name,System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).MethodHandle);
            rollingCodePrepared=true;
        }
        private readonly NpcMotionState[] states=new NpcMotionState[NpcPredictionCache.Capacity];
        private readonly bool[] visited=new bool[NpcPredictionCache.Capacity];
        private readonly bool[] motionSlots=new bool[NpcPredictionCache.Capacity],motionRoles=new bool[NpcPredictionCache.Capacity];
        private readonly int[] pending=new int[NpcPredictionCache.Capacity];
        private readonly MotionRect[] playerAreas=new MotionRect[255];
        private readonly MotionRect[] playerSearchAreas=new MotionRect[255];
        private readonly bool[] playerSearchAlive=new bool[255];
        private readonly bool[] playerSearchDead=new bool[255];
        private readonly bool[] playerStinky=new bool[255];
        private readonly int[] playerStinkyTicks=new int[255],playerTalkNpc=new int[255];
        private readonly int[] playerSlots=new int[255];
        private PredictionPlayers players;
        private readonly PredictionInvasionPlayer[] invasionPlayerValues=new PredictionInvasionPlayer[255];
        private PredictionInvasionPlayers invasionPlayers;
        private readonly PredictionWallPlayer[] wallPlayerValues=new PredictionWallPlayer[255];
        private PredictionWallPlayers wallPlayers;
        private PredictionMageCorpses mageCorpses;
        private PredictionTowerShieldConsumer[] towerConsumerValues;
        private PredictionTowerShieldConsumers towerConsumers;
        private static readonly System.Reflection.FieldInfo mageCorpseField=typeof(Terraria.GameContent.Events.DD2Event).GetField("_deadGoblinSpots",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        internal void Clear()
        {
            ClearCandidateProofs();
            candidateSource?.Clear();
            outcomeTick=-1;OutcomeStyle=0;OutcomePhase=0;OutcomeStrategy=PredictionStrategy.Model;
            Native?.ClearTarget();Cache.Clear();invasionPlayers=null;wallPlayers=null;Array.Clear(wallPlayerValues,0,wallPlayerValues.Length);mageCorpses=null;towerConsumers=null;towerConsumerValues=null;Array.Clear(invasionPlayerValues,0,invasionPlayerValues.Length);
            if(moonLeeches!=null){Array.Clear(moonLeechValues,0,moonLeechValues.Length);moonLeeches=null;}
            if(!ownsState)return;
            ownsState=false;segmented.Clear();rolling.Clear();usingSegmented=false;observedCount=0;targetPlayer=-1;epoch++;
            if(!candidateOnly)Prediction.AimLightTrace.Cache(null,Native?.Worker,"source-clear");Terrain.Reset();Array.Clear(states,0,states.Length);
        }
        internal void ObservePlayerRelocation(Player player)
        {
            ClearCandidateProofs();
            candidateSource?.ObservePlayerRelocation(player);
            Native?.ObservePlayerRelocation();
            if(player!=null && player.whoAmI==targetPlayer){outcomeTick=-1;rolling.Clear();Cache.Clear();epoch++;}
        }
        internal void ObserveNpcReset(NPC npc)
        {
            ClearCandidateProofs();
            candidateSource?.ObserveNpcReset(npc);
            Native?.ObserveNpcReset(npc);
            if(npc!=null)RetireSlot(npc.whoAmI,null);
        }
        internal void ObserveNpcQueryUpdate(int slot,bool discontinuity)
        {
            ClearCandidateProofs();
            candidateSource?.ObserveNpcQueryUpdate(slot,discontinuity);
            Native?.ObserveNpcQueryUpdate(slot);
            if(discontinuity){RetireSlot(slot,null);return;}
            segmented.CorrectAnchor(slot);
            bool affected=usingSegmented && segmented.DependsOn(slot);
            for(int i=0;i<observedCount;i++)if(states[i].Identity.Slot==slot || states[i].PositionOwnerCaptured && states[i].Style==12 && states[i].PositionRelation==6 && (int)states[i].A1==slot || states[i].TrackingIdentity.Token!=null && states[i].TrackingIdentity.Slot==slot || states[i].WallBossIdentity.Token!=null && states[i].WallBossIdentity.Slot==slot || states[i].BrainBossIdentity.Token!=null && states[i].BrainBossIdentity.Slot==slot || states[i].GolemBossIdentity.Token!=null && states[i].GolemBossIdentity.Slot==slot || states[i].PlantBossIdentity.Token!=null && states[i].PlantBossIdentity.Slot==slot || states[i].MechQueenIdentity.Token!=null && states[i].MechQueenIdentity.Slot==slot || states[i].MechLinkIdentity.Token!=null && states[i].MechLinkIdentity.Slot==slot)affected=true;
            // A normal same-instance sync can change velocity/AI immediately;
            // revoke publication until the next completed sample, preserving
            // rolling/segment history and its relation identity. It is not a
            // birth, transform, teleport or permission to resurrect old data.
            if(affected){outcomeTick=-1;Cache.Clear();epoch++;}
        }
        private void RetireSlot(int slot,NPC token)
        {
            bool segmentedAffected=segmented.Reset(slot,token);
            bool affected=usingSegmented && segmentedAffected;
            for(int i=0;i<observedCount;i++)if(states[i].Identity.Slot==slot && (token==null || ReferenceEquals(states[i].Identity.Token,token)) || states[i].PositionOwnerCaptured && states[i].Style==12 && states[i].PositionRelation==6 && (int)states[i].A1==slot || states[i].TrackingIdentity.Token!=null && states[i].TrackingIdentity.Slot==slot && (token==null || ReferenceEquals(states[i].TrackingIdentity.Token,token)) || states[i].WallBossIdentity.Token!=null && states[i].WallBossIdentity.Slot==slot && (token==null || ReferenceEquals(states[i].WallBossIdentity.Token,token)) || states[i].BrainBossIdentity.Token!=null && states[i].BrainBossIdentity.Slot==slot && (token==null || ReferenceEquals(states[i].BrainBossIdentity.Token,token)) || states[i].GolemBossIdentity.Token!=null && states[i].GolemBossIdentity.Slot==slot && (token==null || ReferenceEquals(states[i].GolemBossIdentity.Token,token)) || states[i].PlantBossIdentity.Token!=null && states[i].PlantBossIdentity.Slot==slot && (token==null || ReferenceEquals(states[i].PlantBossIdentity.Token,token)) || states[i].MechQueenIdentity.Token!=null && states[i].MechQueenIdentity.Slot==slot && (token==null || ReferenceEquals(states[i].MechQueenIdentity.Token,token)) || states[i].MechLinkIdentity.Token!=null && states[i].MechLinkIdentity.Slot==slot && (token==null || ReferenceEquals(states[i].MechLinkIdentity.Token,token)))affected=true;
            if(affected){outcomeTick=-1;rolling.Clear();Cache.Clear();epoch++;}
        }
        internal void Stop(){Native?.Stop();Clear();}
        internal void EndWorld(){Native?.DetachWorld();Clear();}
        internal void Prepare(NpcIdentity identity,long tick)
        {PrepareWithIceLease(identity,tick,false);}
        internal void PrepareAction(NpcIdentity identity,long tick)
        {PrepareWithIceLease(identity,tick,true);}
        private void PrepareSample(NpcIdentity identity,long tick,bool firstPlayerStepCompleted)
        {
            CombatPredictionWork.Check();
            outcomeTick=-1;
            if(Cache.Required==0){Clear();return;}
            if(!CombatSelection.Valid(identity,identity.Session)){Clear();return;}
            ownsState=true;
            if(!SegmentedWormMotor(Main.npc[identity.Slot]) && Prediction.SegmentedNpcPrediction.Family(identity.Type)!=0)
            {
                if(!usingSegmented){Native?.ClearTarget();rolling.Clear();observedCount=0;targetPlayer=-1;usingSegmented=true;}
                Native?.DiscardRetiredResult();
                var path=segmented.Prepare(identity,tick,Cache.Required);Outcome(identity,tick,path,PredictionFailureLayer.NpcMotion);Cache.Publish(path);if(!candidateOnly)Prediction.AimLightTrace.Cache(path,null,"segmented");return;
            }
            if(usingSegmented){Cache.Clear();rolling.Clear();segmented.ReleaseTarget();usingSegmented=false;}
            if(Native!=null){Native.Prepare(identity,tick);return;}
            // Keep the last forecast cells only across an unchanged real step.
            // Revalidate this bounded set before reuse, including same-tick edits.
            if(geometryTick!=tick)Terrain.Reset();else if(!Terrain.Unchanged){epoch++;Terrain.Reset();}geometryTick=tick;
            Array.Clear(visited,0,visited.Length);Array.Clear(motionSlots,0,motionSlots.Length);int count=0,queued=1;pending[0]=identity.Slot;visited[identity.Slot]=motionSlots[identity.Slot]=true;
            int wallSlot=Main.wofNPCIndex;
            var directPlayer=Main.npc[identity.Slot].target>=0 && Main.npc[identity.Slot].target<Main.maxPlayers?Main.player[Main.npc[identity.Slot].target]:null;
            bool wallWanted=SegmentedWormMotor(Main.npc[identity.Slot]) && identity.Type==117 || WallFamily(Main.npc[identity.Slot]) || wallSlot>=0 || directPlayer!=null && (directPlayer.gross || directPlayer.tongued || directPlayer.FindBuffIndex(37)>=0 || directPlayer.FindBuffIndex(38)>=0);
            bool wallCaptured=false,wallComplete=true;
            int gravitySlot=NPC.brainOfGravity;bool gravityWanted=gravitySlot>=0 && gravitySlot<Main.maxNPCs && gravitySlot<Main.npc.Length && Main.npc[gravitySlot]!=null;
            bool plantCaptured=false,plantComplete=true,invasionCaptured=false,invasionComplete=true,eyeCaptured=false,eyeComplete=true,skeletonCaptured=false,skeletonComplete=true,townCaptured=false,townComplete=true,brainCaptured=false,brainComplete=true,golemCaptured=false,golemComplete=true,weaponCaptured=false,weaponComplete=true,mageCaptured=false,mageComplete=false,towerCaptured=false,towerComplete=true;
            int towerType=0;
            bool cultistCaptured=false,cultistComplete=true,cultistDooms=false,cultistDoomsComplete=true;
            bool moonCaptured=false,moonComplete=true;
            bool ritualCaptured=false,ritualComplete=true;
            bool windyCaptured=false,windyComplete=true,empressComplete=true;
            bool birdCaptured=false,birdComplete=true;
            bool casterSkeletonQueried=false,casterSkeletonComplete=true,casterWallQueried=false,casterWallComplete=true;
            bool wormCaptured=false,wormComplete=true,solarCaptured=false;
            for(int next=0;next<queued;next++)
            {
                int slot=pending[next];
                var n=Main.npc[slot];if(n==null || !n.active)continue;
                states[count++]=Read(n,identity.Session);
                if(motionSlots[slot] && CasterMotor(n))CaptureCasterDependencies(n,ref queued,ref casterSkeletonQueried,ref casterSkeletonComplete,ref casterWallQueried,ref casterWallComplete);
                if(motionSlots[slot] && (SegmentedWormMotor(n) || wormCaptured && SegmentedWormRequired(slot)))CaptureSegmentedWormDependencies(n,identity.Session,count-1,ref queued,ref wormCaptured,ref wormComplete,ref solarCaptured);
                if(!wallCaptured && (motionSlots[slot] && (WallFamily(n) || n.type==117 && SegmentedWormMotor(n)) || wallWanted && next==0))
                {
                    wallCaptured=true;
                    // Only bodies write this global. Eyes/hungry never feed
                    // the body motor, so unconsumed children stay unqueued.
                    for(int peer=0;peer<Main.maxNPCs;peer++)
                    {
                        if(peer>=Main.npc.Length){wallComplete=false;break;}
                        var member=Main.npc[peer];if(member!=null && member.active && member.type==113 && member.aiStyle==27 && !QueuePlantMotor(peer,ref queued))wallComplete=false;
                    }
                }
                if(!brainCaptured && (motionSlots[slot] && BrainMotor(n) || gravityWanted && next==0))
                {
                    brainCaptured=true;gravityWanted|=Main.getGoodWorld;
                    // All Brain writes are scheduled globals. Other Creepers
                    // contribute active/life, never unused orbit geometry.
                    for(int peer=0;peer<Main.maxNPCs;peer++)
                    {
                        if(peer>=Main.npc.Length){brainComplete=false;break;}
                        var member=Main.npc[peer];if(member==null || !member.active || !BrainMotor(member))continue;
                        if(member.type==266){if(!QueuePlantMotor(peer,ref queued))brainComplete=false;}
                        else if(!visited[peer]){if(queued>=pending.Length)brainComplete=false;else{visited[peer]=true;pending[queued++]=peer;}}
                    }
                }
                if(motionSlots[slot] && GolemMotor(n) && !golemCaptured)
                {
                    golemCaptured=true;
                    // Part flags and the scheduled global scan all active
                    // instances, including parts of a different native body.
                    for(int peer=0;peer<Main.maxNPCs;peer++)
                    {
                        if(peer>=Main.npc.Length){golemComplete=false;break;}
                        var member=Main.npc[peer];if(member==null || !member.active || !GolemMotor(member))continue;
                        if(!QueuePlantMotor(peer,ref queued))golemComplete=false;
                    }
                }
                if(motionSlots[slot] && Mage(n) && !mageCaptured)
                {
                    mageCaptured=true;bool ready=false,complete=true;
                    // Every caster shares corpse consumption and healing clocks.
                    // Hurt positions are required only when a caster can actually
                    // enter its ready scan in this requested window. Unknown hurt
                    // motors remain a conservative boundary, never frozen centers.
                    for(int peer=0;peer<Main.maxNPCs;peer++)
                    {
                        if(peer>=Main.npc.Length){complete=false;break;}
                        var member=Main.npc[peer];if(member==null || !member.active || !Mage(member))continue;
                        ready|=NpcMotion.MageReadyAction(Read(member,identity.Session),Cache.Required)>0;
                        if(!QueuePlantMotor(peer,ref queued))complete=false;
                    }
                    if(ready)for(int peer=0;peer<Main.maxNPCs;peer++)
                    {
                        if(peer>=Main.npc.Length){complete=false;break;}
                        var member=Main.npc[peer];if(member==null || !member.active)continue;
                        var observed=Read(member,identity.Session);var health=observed.Health;
                        bool hurt=member.life!=member.lifeMax || observed.UnmodeledDamageTicks>0 || health.Regen<0 || health.RegenCount<=-120 || health.Poison>0 || health.Fire>0 || health.Cursed>0 || health.Venom>0 || health.Frost>0 || health.Fire3>0 || health.Frost2>0 || health.Shadow>0;
                        if(hurt)
                        {
                            if(!QueuePlantMotor(peer,ref queued))complete=false;
                            int owner=health.RealLife;
                            if(owner>=0 && owner<Main.maxNPCs && owner<Main.npc.Length && Main.npc[owner]!=null && Main.npc[owner].active && !QueuePlantMotor(owner,ref queued))complete=false;
                        }
                        // Under NoNewHits a healthy row with no observed damage
                        // source cannot contribute to the wounded count. Omit
                        // its unrelated geometry/AI instead of pulling the world.
                    }
                    mageComplete=ready && complete;CaptureMageCorpses();
                }
                if(motionSlots[slot] && WeaponMotor(n))
                {
                    weaponCaptured=true;
                    var observed=states[count-1];for(int part=0;part<4;part++){var id=part==0?observed.WeaponPart0:part==1?observed.WeaponPart1:part==2?observed.WeaponPart2:observed.WeaponPart3;if(id.Token!=null && !QueuePlantMotor(id.Slot,ref queued))weaponComplete=false;}
                    // Only this real component and the global AI415/516 count
                    // consumers acquire motors, not every unrelated AI75.
                    for(int peer=0;peer<Main.maxNPCs;peer++)
                    {
                        if(peer>=Main.npc.Length){weaponComplete=false;break;}
                        var member=Main.npc[peer];if(member==null || !member.active)continue;
                        bool component=member.aiStyle==75 && ((int)member.ai[0]==slot || n.aiStyle==75 && member.ai[0]==n.ai[0]);
                        bool counter=(n.type==415 || n.type==416) && (member.type==415 || member.type==516);
                        if((component || counter) && !QueuePlantMotor(peer,ref queued))weaponComplete=false;
                    }
                }
                if(motionSlots[slot] && Tower(n) && !towerCaptured)
                {
                    towerCaptured=true;towerType=n.type;towerComplete=CaptureTowerConsumers(identity.Session,towerType);
                    // Kill(629) changes only its raw target type's global
                    // counter. Acquire every same-type tower and necessary
                    // soul, not unrelated shield families whose uncertainty
                    // cannot change this actor. Inactive targets remain raw
                    // Kill facts; active targets require their actual motor.
                    for(int peer=0;TowerStrength(towerType)!=0 && peer<Main.maxNPCs;peer++)
                    {if(peer>=Main.npc.Length){towerComplete=false;break;}var member=Main.npc[peer];if(member!=null && member.active && Tower(member) && member.type==towerType && !QueuePlantMotor(peer,ref queued))towerComplete=false;}
                    for(int consumer=0;consumer<towerConsumers.Count;consumer++)
                    {var towerTarget=towerConsumers[consumer].TargetIdentity;if(towerTarget.Token==null)continue;var member=Main.npc[towerTarget.Slot];if(member.active && !QueuePlantMotor(towerTarget.Slot,ref queued))towerComplete=false;}
                    // Existing damage or the Solar worm's known link-death
                    // consumer can produce an unseen soul under NoNewHits.
                    // These are health roles with an explicit earliest effect
                    // boundary, never unrelated full-world motor dependencies.
                    for(int peer=0;peer<Main.maxNPCs;peer++)
                    {
                        var member=peer<Main.npc.Length?Main.npc[peer]:null;if(member==null || !member.active || TowerProducer(member.type)!=towerType || TowerStrength(towerType)<=0)continue;
                        var observed=Read(member,identity.Session);var h=observed.Health;
                        bool loss=member.type==412 || member.type==413 || member.type==414 || observed.UnmodeledDamageTicks>0 || h.Regen<0 || h.RegenCount<0 || h.Poison>0 || h.Fire>0 || h.Cursed>0 || h.Venom>0 || h.Frost>0 || h.Fire3>0 || h.Frost2>0 || h.Shadow>0;
                        if(!loss)continue;
                        if(!visited[peer]){if(queued>=pending.Length){towerComplete=false;continue;}visited[peer]=true;pending[queued++]=peer;}
                        int towerLifeOwner=h.RealLife;
                        if(towerLifeOwner>=0 && towerLifeOwner<Main.maxNPCs && towerLifeOwner<Main.npc.Length && Main.npc[towerLifeOwner]!=null && !visited[towerLifeOwner])
                        {if(queued>=pending.Length)towerComplete=false;else{visited[towerLifeOwner]=true;pending[queued++]=towerLifeOwner;}}
                    }
                }
                if(motionSlots[slot] && TownCritter(n) && !townCaptured)
                {
                    townCaptured=true;
                    // AI7 scans dangerous/stinky actors independently of its
                    // numbered target. Acquire their real slot-order motors,
                    // not frozen proximity bits. Furniture occupancy is
                    // observed separately; unknown future occupants are an
                    // explicit dependency boundary rather than frozen seats.
                    for(int peer=0;peer<Main.maxNPCs;peer++)
                    {
                        if(peer>=Main.npc.Length){townComplete=false;break;}
                        var other=Main.npc[peer];if(other==null || !other.active || peer==slot)continue;
                        bool danger=n.type!=625 && (n.aiStyle!=16 || Main.raining) && (!NPCID.Sets.CritterThatCanTurnOnPlayers[other.type] && other.type!=690 && (!other.friendly && other.damage>0 || other.stinky) || TownCritter(other) && other.aiStyle==7);
                        bool seat=other.townNPC && other.aiStyle==7 && other.ai[0]==5;
                        if(danger){if(!QueuePlantMotor(peer,ref queued))townComplete=false;}
                        else if(seat && !visited[peer]){if(queued>=pending.Length){townComplete=false;continue;}visited[peer]=true;pending[queued++]=peer;}
                    }
                }
                if(motionSlots[slot] && InvasionTarget(n) && !invasionCaptured)
                {
                    invasionCaptured=true;
                    for(int peer=0;peer<Main.maxNPCs;peer++)
                    {
                        if(peer>=Main.npc.Length){invasionComplete=false;break;}
                        var crystal=Main.npc[peer];if(n.type!=620 && crystal!=null && crystal.active && crystal.type==548 && !QueuePlantMotor(peer,ref queued))invasionComplete=false;
                    }
                }
                if(motionSlots[slot] && InvasionTarget(n) && n.target>=300 && n.target<300+Main.maxNPCs)
                {int oldAim=n.target-300;var owner=Main.npc[oldAim];if(owner!=null && owner.active && !QueuePlantMotor(oldAim,ref queued))invasionComplete=false;}
                if(motionSlots[slot] && EyeMotor(n) && !eyeCaptured)
                {
                    eyeCaptured=true;
                    // Twins and every possible MechQueen writer form a slot-ordered
                    // closure. Frame-start Destroyer presence needs its real chain.
                    for(int peer=0;peer<Main.maxNPCs;peer++)
                    {
                        if(peer>=Main.npc.Length){eyeComplete=false;break;}
                        var member=Main.npc[peer];if(member!=null && member.active && (member.type==125 || member.type==126 || member.type==127 || member.type==135) && !QueuePlantMotor(peer,ref queued))eyeComplete=false;
                    }
                }
                if(motionSlots[slot] && PlantMotor(n) && !plantCaptured)
                {
                    plantCaptured=true;
                    // AI51 mutates the global by native actor slot, and its
                    // mean uses the first three AI52 worldwide. Acquire once
                    // before role promotion can leave an earlier health-only
                    // body without the necessary motor/target observation.
                    int hooks=0;
                    for(int peer=0;peer<Main.maxNPCs;peer++)
                    {
                        if(peer>=Main.npc.Length){plantComplete=false;break;}
                        var member=Main.npc[peer];if(member==null || !member.active)continue;
                        if(member.aiStyle!=51 && !(member.aiStyle==52 && hooks++<3))continue;
                        if(!QueuePlantMotor(peer,ref queued))plantComplete=false;
                    }
                    int global=NPC.plantBoss;
                    if(global>=0 && global<Main.maxNPCs && global<Main.npc.Length && Main.npc[global]!=null && Main.npc[global].active && Main.npc[global].type==262 && !QueuePlantMotor(global,ref queued))plantComplete=false;
                }
                if(motionSlots[slot] && RequiresCrowd(n))
                {
                    states[count-1].CrowdCaptured=states[count-1].CrowdComplete=true;
                    // Only an actual crowd motor acquires its same-type
                    // relation. Future slot-ordered kernels use scalar copies;
                    // no live actor is moved, updated or given an AI action.
                    for(int peer=0;peer<Main.maxNPCs;peer++)
                    {
                        if(peer>=Main.npc.Length){states[count-1].CrowdComplete=false;break;}
                        var member=Main.npc[peer];if(member==null || !member.active || CrowdFamily(member.type)!=CrowdFamily(n.type))continue;
                        if(peer>=motionSlots.Length || !RequiresCrowd(member)){states[count-1].CrowdComplete=false;continue;}
                        motionSlots[peer]=true;if(visited[peer])continue;
                        if(queued>=pending.Length){states[count-1].CrowdComplete=false;continue;}
                        visited[peer]=true;pending[queued++]=peer;
                    }
                }
                // AI111 chooses before its owner-follow gate. Observe that
                // first query before deciding whether an owner is necessary.
                if(n.type==111 && n.aiStyle==3 && n.ai[3]<0)NpcTrackingObservation.Capture(n,ref states[count-1],Terrain);
                NpcPositionObservation.Capture(n,ref states[count-1],identity.Session);
                if(motionSlots[slot] && MoonLordMotor(n))CaptureMoonLordDependencies(n,identity.Session,ref queued,ref moonCaptured,ref moonComplete);
                if(motionSlots[slot] && CultistRitualMotor(n))CaptureCultistRitualDependencies(n,identity.Session,ref queued,ref ritualCaptured,ref ritualComplete);
                if(motionSlots[slot] && EmpressMotor(n))CaptureEmpressDependencies(n,identity.Session,ref queued,ref empressComplete);
                if(motionSlots[slot] && WindyBalloonMotor(n))CaptureWindyBalloonDependencies(n,identity.Session,ref queued,ref windyCaptured,ref windyComplete);
                if(motionSlots[slot] && BirdMotor(n))CaptureBirdDependencies(n,identity.Session,ref queued,ref birdCaptured,ref birdComplete);
                if(motionSlots[slot] && CultistMotor(n) && !cultistCaptured)
                    CaptureCultistDependencies(n,identity.Session,ref queued,ref cultistCaptured,ref cultistComplete,ref cultistDooms,ref cultistDoomsComplete);
                if(motionSlots[slot] && SkeletonMotor(n) && !skeletonCaptured && !(n.type==36 && states[count-1].PositionOwnerCaptured && !states[count-1].PositionOwnerValid))
                {
                    skeletonCaptured=true;
                    // AI11 Expert counts every live hand, independently of its
                    // owner. Hand lifetimes need their actual head motors in
                    // the same native slot loop; never freeze orphan presence.
                    for(int peer=0;peer<Main.maxNPCs;peer++)
                    {
                        if(peer>=Main.npc.Length){skeletonComplete=false;break;}
                        var member=Main.npc[peer];if(member==null || !member.active || member.type!=35 && member.type!=68 && member.type!=36)continue;
                        if(!SkeletonMotor(member) || !QueuePlantMotor(peer,ref queued))skeletonComplete=false;
                    }
                }
                if(n.type==264 && n.aiStyle==53 && n.ai[3]>0)
                {
                    // This owner need only be active in original AI53; the
                    // independently scheduled global supplies the boss life.
                    states[count-1].PositionRelation=10;states[count-1].PositionParameter=-1;
                    int ownerSlot=(int)n.ai[3]-1;
                    if(ownerSlot>=0 && ownerSlot<Main.maxNPCs && ownerSlot<Main.npc.Length)
                    {
                        var owner=Main.npc[ownerSlot];
                        if(owner!=null && owner.active)
                        {
                            states[count-1].PositionOwner=CombatSelection.Identity(owner,identity.Session);states[count-1].PositionParameter=0;
                            if(!QueuePlantMotor(ownerSlot,ref queued))plantComplete=false;
                        }
                    }
                }
                if(n.type==523 && n.aiStyle==101)
                {
                    // AI101 reads its Cultist owner's Active/type and life.
                    // A selected Doom needs that exact owner's real AI84
                    // lifetime; owners acquired only for other Doom count
                    // rows remain health dependencies, not world-wide motors.
                    states[count-1].PositionRelation=9;states[count-1].PositionParameter=-1;
                    int ownerSlot=(int)n.ai[0];
                    if(ownerSlot>=0 && ownerSlot<Main.maxNPCs)
                    {
                        var owner=Main.npc[ownerSlot];
                        if(owner!=null && owner.active && owner.type==439)
                        {states[count-1].PositionOwner=CombatSelection.Identity(owner,identity.Session);states[count-1].PositionParameter=0;}
                    }
                }
                int parent=states[count-1].ParentSlot,child=states[count-1].ChildSlot;
                if(motionSlots[slot])
                {
                    var owner=states[count-1].PositionOwner;if(owner.Token!=null){if(states[count-1].PositionRelation!=9 || slot==identity.Slot && n.type==523 && n.aiStyle==101)motionSlots[owner.Slot]=true;if(!visited[owner.Slot]){visited[owner.Slot]=true;pending[queued++]=owner.Slot;}}
                    if(!(n.aiStyle==6 && wormCaptured && SegmentedWormRequired(slot)) && parent>=0 && parent<Main.maxNPCs){motionSlots[parent]=true;if(!visited[parent]){visited[parent]=true;pending[queued++]=parent;}}
                    if(!(n.aiStyle==6 && wormCaptured && SegmentedWormRequired(slot)) && child>=0 && child<Main.maxNPCs){motionSlots[child]=true;if(!visited[child]){visited[child]=true;pending[queued++]=child;}}
                }
                int lifeOwner=states[count-1].Health.RealLife;
                if(lifeOwner>=0 && lifeOwner<Main.maxNPCs && !visited[lifeOwner]){visited[lifeOwner]=true;pending[queued++]=lifeOwner;}
            }
            if(wormCaptured)FinishSegmentedWormDependencies(identity.Session,ref count,wormCaptured,wormComplete);
            if(towerCaptured)
            {
                for(int consumer=0;consumer<towerConsumers.Count;consumer++)
                {
                    var towerTarget=towerConsumers[consumer].TargetIdentity;if(towerTarget.Token==null || visited[towerTarget.Slot])continue;
                    if(count>=states.Length){towerComplete=false;continue;}
                    states[count++]=Read(Main.npc[towerTarget.Slot],identity.Session);visited[towerTarget.Slot]=true;
                }
                var healthEnvironment=new PredictionEnvironment{Expert=Main.expertMode,GoodWorld=Main.getGoodWorld,Multiplayer=Main.netMode==1};
                for(int i=0;i<count;i++)if(states[i].EffectiveType==towerType)
                {
                    states[i].TowerRelationsCaptured=true;states[i].TowerRelationsComplete=towerComplete;states[i].TowerShieldCaptured=true;states[i].TowerShieldStrength=TowerStrength(states[i].EffectiveType);states[i].TowerShieldMaximum=NPC.ShieldStrengthTowerMax;
                    for(int producer=0;producer<count;producer++)if(states[producer].Active && TowerProducer(states[producer].EffectiveType)==states[i].EffectiveType)
                        states[i].TowerNewConsumerAt=Math.Min(states[i].TowerNewConsumerAt,NpcMotion.TowerNewConsumerAction(states[producer],healthEnvironment,Cache.Required,states,count));
                }
            }
            if(plantCaptured)
            {
                int global=NPC.plantBoss;bool valid=global>=0 && global<Main.maxNPCs && global<Main.npc.Length && Main.npc[global]!=null && Main.npc[global].active && Main.npc[global].type==262;
                var globalIdentity=valid?CombatSelection.Identity(Main.npc[global],identity.Session):default(NpcIdentity);
                for(int i=0;i<count;i++)if(PlantMotor(Main.npc[states[i].Identity.Slot]))
                {states[i].PlantRelationsCaptured=true;states[i].PlantRelationsComplete=plantComplete;states[i].PlantBossValid=valid;states[i].PlantBossIdentity=globalIdentity;}
            }
            if(brainCaptured)
            {
                int global=NPC.crimsonBoss;bool valid=global>=0 && global<Main.maxNPCs && global<Main.npc.Length && Main.npc[global]!=null;
                var globalIdentity=valid?CombatSelection.Identity(Main.npc[global],identity.Session):default(NpcIdentity);
                if(valid && !visited[global]){if(count>=states.Length)brainComplete=false;else{states[count++]=Read(Main.npc[global],identity.Session);visited[global]=true;}}
                for(int i=0;i<count;i++)if(BrainMotor(Main.npc[states[i].Identity.Slot]))
                {states[i].BrainRelationsCaptured=true;states[i].BrainRelationsComplete=brainComplete;states[i].BrainBossCaptured=true;states[i].BrainBossValid=valid;states[i].BrainBossIdentity=globalIdentity;}
            }
            NpcIdentity gravityIdentity=default(NpcIdentity);bool gravityValid=gravitySlot>=0 && gravitySlot<Main.maxNPCs && gravitySlot<Main.npc.Length && Main.npc[gravitySlot]!=null;
            if(gravityWanted && gravityValid)
            {
                gravityIdentity=CombatSelection.Identity(Main.npc[gravitySlot],identity.Session);
                if(!visited[gravitySlot]){if(count>=states.Length)brainComplete=false;else{states[count++]=Read(Main.npc[gravitySlot],identity.Session);visited[gravitySlot]=true;}}
            }
            if(golemCaptured)
            {
                int global=NPC.golemBoss;bool valid=global>=0 && global<Main.maxNPCs && global<Main.npc.Length && Main.npc[global]!=null;
                var globalIdentity=valid?CombatSelection.Identity(Main.npc[global],identity.Session):default(NpcIdentity);
                // CheckBossIndexes will clear an inactive/foreign global at the
                // next frame. Keep its current full row for that actual check,
                // without acquiring unrelated AI or assuming its presence.
                if(valid && !visited[global])
                {if(count>=states.Length)golemComplete=false;else{states[count++]=Read(Main.npc[global],identity.Session);visited[global]=true;}}
                for(int i=0;i<count;i++)if(GolemMotor(Main.npc[states[i].Identity.Slot]))
                {states[i].GolemRelationsCaptured=true;states[i].GolemRelationsComplete=golemComplete;states[i].GolemBossCaptured=true;states[i].GolemBossValid=valid;states[i].GolemBossIdentity=globalIdentity;}
            }
            if(wallCaptured)
            {
                bool valid=wallSlot>=0 && wallSlot<Main.maxNPCs && wallSlot<Main.npc.Length && Main.npc[wallSlot]!=null;
                var owner=valid?Main.npc[wallSlot]:null;var rawIdentity=valid?CombatSelection.Identity(owner,identity.Session):default(NpcIdentity);
                if(valid && !visited[wallSlot]){if(count>=states.Length)wallComplete=false;else{states[count++]=Read(owner,identity.Session);visited[wallSlot]=true;}}
                for(int i=0;i<count;i++)
                {states[i].WallRelationsCaptured=true;states[i].WallRelationsComplete=wallComplete;states[i].WallBossCaptured=true;states[i].WallBossValid=valid;states[i].WallBossIdentity=rawIdentity;states[i].WallTop=Main.wofDrawAreaTop;states[i].WallBottom=Main.wofDrawAreaBottom;}
            }
            if(weaponCaptured)for(int i=0;i<count;i++)if(WeaponMotor(Main.npc[states[i].Identity.Slot]))
            {states[i].WeaponRelationsCaptured=true;states[i].WeaponRelationsComplete=weaponComplete;}
            if(mageCaptured)
            {
                int healing=FirstMageHealingConsumer(Cache.Required);
                for(int i=0;i<count;i++)if(Mage(Main.npc[states[i].Identity.Slot]))
                {states[i].MageRelationsCaptured=true;states[i].MageRelationsComplete=mageComplete;states[i].MageHealingAt=healing;}
            }
            if(skeletonCaptured)for(int i=0;i<count;i++)if(SkeletonMotor(Main.npc[states[i].Identity.Slot]))
            {states[i].SkeletonRelationsCaptured=true;states[i].SkeletonRelationsComplete=skeletonComplete;}
            if(townCaptured)for(int i=0;i<count;i++)if(TownCritter(Main.npc[states[i].Identity.Slot]))
            {states[i].TownRelationsCaptured=true;states[i].TownRelationsComplete=townComplete;}
            if(cultistCaptured)FinishCultistDependencies(count,cultistComplete,cultistDooms,cultistDoomsComplete);
            if(moonCaptured)FinishMoonLordDependencies(identity.Session,ref count,moonCaptured,moonComplete);
            if(ritualCaptured)FinishCultistRitualDependencies(identity.Session,ref count,ritualCaptured,ritualComplete);
            if(windyCaptured)FinishWindyBalloonDependencies(identity.Session,ref count,windyCaptured,windyComplete);
            if(birdCaptured)FinishBirdDependencies(identity.Session,ref count,birdCaptured,birdComplete);
            for(int i=0;i<count;i++)if(states[i].EmpressFactsCaptured)states[i].EmpressRelationsComplete=empressComplete;
            FinishCasterDependencies(count,casterSkeletonQueried,casterSkeletonComplete,casterWallQueried,casterWallComplete);
            // Sort only the required dependency chain by native slot order.
            for(int i=1;i<count;i++){var value=states[i];int j=i-1;while(j>=0 && states[j].Identity.Slot>value.Identity.Slot){states[j+1]=states[j];j--;}states[j+1]=value;}
            int selected=0;for(int i=0;i<count;i++)if(states[i].Identity.Equals(identity))selected=i;
            for(int i=0;i<count;i++){motionRoles[i]=states[i].Identity.Slot<motionSlots.Length && motionSlots[states[i].Identity.Slot];if(motionRoles[i]){var actor=Main.npc[states[i].Identity.Slot];NpcTrackingObservation.Capture(actor,ref states[i],Terrain);if(DeerclopsMotor(actor))CaptureDeerclopsInputs(actor,ref states[i],firstPlayerStepCompleted);}}
            if(invasionCaptured)for(int i=0;i<count;i++)if(InvasionTarget(Main.npc[states[i].Identity.Slot])){states[i].DD2TargetFactsCaptured=true;states[i].DD2TargetFactsComplete=invasionComplete;}
            if(eyeCaptured)for(int i=0;i<count;i++)if(EyeMotor(Main.npc[states[i].Identity.Slot]))
            {states[i].EyeRelationsCaptured=true;states[i].EyeRelationsComplete=eyeComplete;states[i].MechDestroyerCaptured=true;states[i].MechDestroyerPresent=NPC.npcsFoundForCheckActive[135];}
            bool searchNeeded=townCaptured;for(int i=0;i<count;i++)if(motionRoles[i] && (states[i].EffectiveType==657 && states[i].Style==121 || states[i].EffectiveType==371 && states[i].Style==70 || states[i].EffectiveType==398 && states[i].Style==77 || states[i].EffectiveType==399 && states[i].Style==80 || states[i].EffectiveType==50 && states[i].Style==15 || states[i].EffectiveType==222 && states[i].Style==43 || states[i].EffectiveType==689 && states[i].Style==24 && states[i].BirdNetMode!=1 && !Main.dayTime))searchNeeded=true;
            int playerCount=0;bool samePlayers=players!=null && players.SearchCaptured==searchNeeded && players.TownCaptured==townCaptured,anyCorrupt=false;
            for(int i=0;i<Main.maxPlayers;i++)
            {
                var p=Main.player[i];if(p==null || !p.active)continue;var area=new MotionRect((int)p.position.X,(int)p.position.Y,p.width,p.height);
                samePlayers&=players!=null && playerCount<players.Count && players.SlotAt(playerCount)==i && PredictionPlayers.Same(area,players[playerCount]);playerSlots[playerCount]=i;playerAreas[playerCount]=area;
                if(searchNeeded)
                {
                    var precise=new MotionRect(p.position.X,p.position.Y,p.width,p.height);bool alive=!p.dead && !p.ghost;
                    samePlayers&=players!=null && players.SearchCaptured && playerCount<players.Count && PredictionPlayers.Same(precise,players.SearchAreaAt(playerCount)) && alive==players.SearchAliveAt(playerCount) && p.dead==players.SearchDeadAt(playerCount);
                    playerSearchAreas[playerCount]=precise;playerSearchAlive[playerCount]=alive;playerSearchDead[playerCount]=p.dead;
                }
                if(townCaptured)
                {
                    int stinky=p.FindBuffIndex(120),remaining=stinky<0?0:p.buffTime[stinky];
                    samePlayers&=players!=null && players.TownCaptured && playerCount<players.Count && players.StinkyAt(playerCount)==p.stinky && players.StinkyTicksAt(playerCount)==remaining && players.TalkNpcAt(playerCount)==p.talkNPC;
                    playerStinky[playerCount]=p.stinky;playerStinkyTicks[playerCount]=remaining;playerTalkNpc[playerCount]=p.talkNPC;
                }
                if(invasionCaptured)
                {
                    uint noAggro=0;for(int type=552;type<=577;type++)if(p.npcTypeNoAggro[type])noAggro|=1u<<(type-552);if(p.npcTypeNoAggro[620])noAggro|=1u<<26;if(p.npcTypeNoAggro[551])noAggro|=1u<<27;if(p.npcTypeNoAggro[578])noAggro|=1u<<28;
                    Projectile pet=p.tankPet>=0 && p.tankPet<Main.maxProjectiles?Main.projectile[p.tankPet]:null;
                    invasionPlayerValues[playerCount]=new PredictionInvasionPlayer(i,p,new MotionRect(p.position.X,p.position.Y,p.width,p.height),p.velocity.X,p.velocity.Y,p.aggro,noAggro,!p.dead && !p.ghost,p.itemAnimation==0,pet,pet==null?default(MotionRect):new MotionRect(pet.position.X,pet.position.Y,pet.width,pet.height));
                }
                if(wallCaptured)wallPlayerValues[playerCount]=WallPlayer(p);
                playerCount++;anyCorrupt|=!p.dead && p.ZoneCorrupt;
            }
            if(!samePlayers || players.Count!=playerCount)players=new PredictionPlayers(playerAreas,playerCount,playerSlots,searchNeeded?playerSearchAreas:null,searchNeeded?playerSearchAlive:null,searchNeeded?playerSearchDead:null,townCaptured?playerStinky:null,townCaptured?playerStinkyTicks:null,townCaptured?playerTalkNpc:null);
            if(wallCaptured)
            {
                int wallCount=playerCount;
                if((Main.player[0]==null || !Main.player[0].active) && Main.player[0]!=null)wallPlayerValues[wallCount++]=WallPlayer(Main.player[0]);
                bool same=wallPlayers!=null && wallPlayers.Count==wallCount;for(int i=0;same && i<wallCount;i++)same=wallPlayerValues[i].Same(wallPlayers[i]);
                if(!same)wallPlayers=new PredictionWallPlayers(wallPlayerValues,wallCount);
            }
            if(invasionCaptured)
            {
                bool same=invasionPlayers!=null && invasionPlayers.Count==playerCount;for(int i=0;same && i<playerCount;i++)same=invasionPlayerValues[i].Same(invasionPlayers[i]);
                if(!same)invasionPlayers=new PredictionInvasionPlayers(invasionPlayerValues,playerCount);
                var invasionEnvironment=new PredictionEnvironment{InvasionPlayers=invasionPlayers,PlayerIndex=-1};
                for(int i=0;i<count;i++)if(InvasionTarget(Main.npc[states[i].Identity.Slot]))
                {states[i].DD2PlayerPremiseCaptured=true;states[i].DD2PlayerPremiseIndex=NpcMotion.InvasionPlayerPremise(states[i],invasionEnvironment);}
            }
            if(searchNeeded)for(int i=0;i<count;i++)if(motionRoles[i] && states[i].EffectiveType==399 && states[i].Style==80)
            {
                states[i].SearchPlayerCaptured=true;int winner=SearchPlayer(states[i].Bounds.CenterX,states[i].Bounds.CenterY);
                states[i].SearchPlayerIndex=winner<0?-1:players.SlotAt(winner);states[i].SearchPlayerAlive=winner>=0 && players.SearchAliveAt(winner);states[i].SearchPlayerDead=winner>=0 && players.SearchDeadAt(winner);
            }
            var current=Main.npc[identity.Slot];
            if(!wormCaptured && (current.aiStyle==6 || current.aiStyle==37))for(int i=0;i<count;i++)if(states[i].ParentSlot<0){current=Main.npc[states[i].Identity.Slot];break;}
            if(moonCaptured)CaptureMoonLeechInputs(identity.Session,count);
            var premiseEnv=new PredictionEnvironment{BirdWeatherCaptured=birdCaptured,BirdCloudAlpha=birdCaptured?Main.cloudAlpha:0,BirdInactiveZeroCaptured=birdCaptured && playerCount==0 && Main.player[0]!=null && !Main.player[0].active,BirdInactiveZero=birdCaptured && playerCount==0 && Main.player[0]!=null && !Main.player[0].active?new MotionRect(Main.player[0].position.X,Main.player[0].position.Y,Main.player[0].width,Main.player[0].height):default(MotionRect),MoonLeeches=moonCaptured?moonLeeches:null,TowerConsumers=towerCaptured?towerConsumers:null,WallPlayers=wallCaptured?wallPlayers:null,WallViewWidth=Main.MaxWorldViewSize.X,BloodMoon=Main.bloodMoon,Players=players,TownWeatherCaptured=townCaptured,Raining=townCaptured && Main.raining,Storming=townCaptured && Main.IsItStorming,MageCorpses=mageCaptured?mageCorpses:null,InvasionPlayers=invasionCaptured?invasionPlayers:null,PlayerIndex=-1,Expert=Main.expertMode,SnowMoon=Main.snowMoon,WorldHeight=Main.maxTilesY,Eclipse=Main.eclipse,PumpkinMoon=Main.pumpkinMoon,SlimeRain=Main.slimeRain,Day=Main.dayTime,Remix=Main.remixWorld,GravityWorldSurface=Main.worldSurface,GravityRockLayer=Main.rockLayer,WorldSurface=(float)Main.worldSurface,Multiplayer=Main.netMode==1,GoodWorld=Main.getGoodWorld,Anniversary=Main.tenthAnniversaryWorld};
            int oldPlayer=states[selected].PlayerIndex;
            premiseEnv.Graveyard=oldPlayer>=0 && oldPlayer<Main.maxPlayers && Main.player[oldPlayer]!=null && Main.player[oldPlayer].ZoneGraveyard;
            int target=NpcMotion.PlayerPremiseTarget(states[selected],premiseEnv,Terrain,states,count);bool needsPlayer=false;
            bool followPremise=states[selected].EffectiveType==111 && states[selected].A3<0;
            // Only real movement consumers acquire a future-player prerequisite.
            // A required numbered target is never replaced by the local player.
            // Relation6 acquires the premise at its native actor slot, after
            // earlier owners move. Frame-start geometry can invent Aim as
            // well as miss it; merely observing its potential player must not
            // reject an otherwise independent raising/home action here.
            for(int i=0;i<count;i++)if(motionRoles[i] && states[i].PositionRelation!=6 && NpcMotion.NeedsPlayerMotion(states[i],premiseEnv,Cache.Required,states,count,Terrain))
            {
                // AI111 consumes the first player's next position to decide
                // whether this potential owner is needed at all. Prepare that
                // query's winner first; an unused owner's different player
                // cannot replace it. A still-needed owner's distinct future
                // remains an honest actor-time MissingDependency boundary.
                if(!needsPlayer && !followPremise)target=NpcMotion.PlayerPremiseTarget(states[i],premiseEnv,Terrain,states,count);needsPlayer=true;
            }
            var player=target>=0 && target<Main.maxPlayers?Main.player[target]:null;
            bool playerAlive=player!=null && player.active && !player.dead && !player.ghost;
            if(needsPlayer && !playerAlive){rolling.Clear();Outcome(identity,tick,null,PredictionFailureLayer.PlayerPremise);Cache.Publish(null);return;}
            targetPlayer=target;observedCount=count;
            var env=new PredictionEnvironment{BirdWeatherCaptured=birdCaptured,BirdCloudAlpha=birdCaptured?Main.cloudAlpha:0,BirdInactiveZeroCaptured=birdCaptured && playerCount==0 && Main.player[0]!=null && !Main.player[0].active,BirdInactiveZero=birdCaptured && playerCount==0 && Main.player[0]!=null && !Main.player[0].active?new MotionRect(Main.player[0].position.X,Main.player[0].position.Y,Main.player[0].width,Main.player[0].height):default(MotionRect),MoonLeeches=moonCaptured?moonLeeches:null,WallPlayers=wallCaptured?wallPlayers:null,WallViewWidth=Main.MaxWorldViewSize.X,PlayerGross=player!=null && player.gross,WallTimelineCaptured=wallCaptured && player!=null,WallTimelineIndex=target,WallTimelineArea=player==null?default(MotionRect):new MotionRect(player.position.X,player.position.Y,player.width,player.height),WallTimelineGross=player!=null && player.gross,GravitySourceCaptured=!gravityWanted || brainComplete,GravitySourceValid=gravityValid,GravitySourceIdentity=gravityIdentity,GravitySourceX=gravityValid?Main.npc[gravitySlot].Center.X:0,GravitySourceY=gravityValid?Main.npc[gravitySlot].Center.Y:0,TownWeatherCaptured=townCaptured,Raining=townCaptured && Main.raining,Storming=townCaptured && Main.IsItStorming,BloodMoon=Main.bloodMoon,SkeletronUp=Main.getGoodWorld && current.aiStyle==9 && current.type==33 && NPC.AnyNPCs(35),WallBossUp=Main.getGoodWorld && current.aiStyle==9 && current.type==25 && NPC.AnyNPCs(113),PlayerProtected=playerAlive && player.insideUnbreakableWalls,PlayerVelocityCaptured=player!=null,PlayerVx=player==null?0:player.velocity.X,PlayerVy=player==null?0:player.velocity.Y,HappyWindyCaptured=true,HappyWindy=Main.IsItAHappyWindyDay,PlayerIndex=target,PlayerX=player==null?0:player.Center.X,PlayerY=player==null?0:player.Center.Y,PlayerWidth=player==null?0:player.width,PlayerHeight=player==null?0:player.height,PlayerWet=player!=null && player.wet,Wind=Main.windSpeedCurrent,WindTarget=Main.windSpeedTarget,Expert=Main.expertMode,Day=Main.dayTime,WorldWidth=Main.maxTilesX,GravityWorldSurface=Main.worldSurface,WorldSurface=(float)Main.worldSurface,Multiplayer=Main.netMode==1,Remix=Main.remixWorld,SlimeRain=Main.slimeRain,
                Enraged=player!=null && (player.position.Y<800 || player.position.Y>Main.worldSurface*16 || player.position.X>6400 && player.position.X<Main.maxTilesX*16-6400),
                TowerConsumers=towerCaptured?towerConsumers:null,DD2OngoingCaptured=true,DD2Ongoing=Terraria.GameContent.Events.DD2Event.Ongoing,MageCorpses=mageCaptured?mageCorpses:null,InvasionPlayers=invasionCaptured?invasionPlayers:null,MechQueenUp=NPC.mechQueen>=0 && NPC.mechQueen<Main.maxNPCs && Main.npc[NPC.mechQueen]!=null && Main.npc[NPC.mechQueen].active && Main.npc[NPC.mechQueen].type==127,Players=players,WorldHeight=Main.maxTilesY,GravityRockLayer=Main.rockLayer,RockLayer=(float)Main.rockLayer,PlayerDead=!playerAlive,PlayerIdleWithNegativeAggro=player!=null && player.itemAnimation==0 && player.aggro<0,Corrupt=player!=null && player.ZoneCorrupt,Crimson=player!=null && player.ZoneCrimson,Jungle=player!=null && player.ZoneJungle,Temple=player!=null && player.ZoneLihzhardTemple,AnyLivingCorrupt=anyCorrupt,SkyblockLowTiles=WorldGen.Skyblock.lowTiles,ClearLine=false,Eclipse=Main.eclipse,Graveyard=player!=null && player.ZoneGraveyard,GoodWorld=Main.getGoodWorld,Zenith=Main.zenithWorld,Anniversary=Main.tenthAnniversaryWorld,InvasionType=Main.invasionType,SnowMoon=Main.snowMoon,PumpkinMoon=Main.pumpkinMoon,DontStarve=Main.dontStarveWorld};
            // Main updates players by slot before NPCs. At local ItemCheck,
            // later numbered players have not naturally moved yet.
            var playerMotion=!playerAlive?default(PredictionPlayerMotion):ReadPlayer(player);
            bool playerAdvanced=firstPlayerStepCompleted && target<=Main.myPlayer;
            if(candidateOnly && candidateReuseMiss && ReuseCandidateInputs(identity,firstPlayerStepCompleted,playerAdvanced,count,selected,Cache.Required,env,playerMotion))
            {
                // This is the caller's still-valid no-contact verdict, not a
                // newly published future. Keep conditional capture time intact.
                Cache.Publish(null);return;
            }
            rolling.CaptureContinuation=candidateOnly;
            var result=candidateOnly?ContinueCandidate(identity,tick,firstPlayerStepCompleted,playerAdvanced,count,selected,Cache.Required,env,playerMotion):null;
            if(result==null)result=rolling.Prepare(states,count,selected,tick,Cache.Required,epoch,env,playerMotion,Terrain,motionRoles,playerAdvanced);
            if(candidateOnly && result!=null && result.Stop==PredictionStop.None && result.Count==Cache.Required+1 && rolling.FixedInputResult)
                RememberCandidateInputs(identity,firstPlayerStepCompleted,playerAdvanced,count,selected,Cache.Required,env,playerMotion);
            Outcome(identity,tick,result,rolling.FailureLayer);Cache.Publish(result);
        }
        private static bool WeaponMotor(NPC n){return n.aiStyle==75 && (n.type==390 || n.type==392 || n.type==393 || n.type==394 || n.type==416 || n.type==492) || n.type==395 && n.aiStyle==76 || n.type==491 && n.aiStyle==93 || (n.type==391 || n.type==415) && n.aiStyle==3;}
        private static bool EyeMotor(NPC n){return n.type==125 && n.aiStyle==30 || n.type==126 && n.aiStyle==31 || n.type==127 && n.aiStyle==32;}
        private static bool SkeletonMotor(NPC n){return (n.type==35 || n.type==68) && n.aiStyle==11 || n.type==36 && n.aiStyle==12;}
        private static bool TownCritter(NPC n){int type=n.type;return n.aiStyle==7 && (type==230 || type==303 || type==337 || type==540 || type==625 || type==687) || type==55 && n.aiStyle==16;}
        private static bool BrainMotor(NPC n){return n.type==266 && n.aiStyle==54 || n.type==267 && n.aiStyle==55;}
        private static bool GolemMotor(NPC n){return n.type==245 && n.aiStyle==45 || n.type==246 && n.aiStyle==46 || (n.type==247 || n.type==248) && n.aiStyle==47 || n.type==249 && n.aiStyle==48;}
        private static int TowerProducer(int type)
        {return type>=412 && type<=419 || type==518?517:type==425 || type==426 || type==427 || type==429?422:type==420 || type==421 || type==423 || type==424?507:type==402 || type==405 || type==407 || type==409 || type==411?493:0;}
        private static bool Tower(NPC n){return n!=null && n.aiStyle==94 && (n.type==422 || n.type==493 || n.type==507 || n.type==517);}
        private static int TowerStrength(int type){return type==517?NPC.ShieldStrengthTowerSolar:type==422?NPC.ShieldStrengthTowerVortex:type==507?NPC.ShieldStrengthTowerNebula:NPC.ShieldStrengthTowerStardust;}
        private bool CaptureTowerConsumers(long session,int towerType)
        {
            // Zero cannot increase through Kill(629), and zero Kill does not
            // set AI3. No other tower or projectile motor is then a necessary
            // premise for this already exposed actor's future motion/receive.
            if(TowerStrength(towerType)==0)
            {
                if(towerConsumers==null || towerConsumers.Count!=0 || towerConsumers.Left!=Main.leftWorld || towerConsumers.Right!=Main.rightWorld || towerConsumers.Top!=Main.topWorld || towerConsumers.Bottom!=Main.bottomWorld)
                    towerConsumers=new PredictionTowerShieldConsumers(new PredictionTowerShieldConsumer[0],0,Main.leftWorld,Main.rightWorld,Main.topWorld,Main.bottomWorld);
                return true;
            }
            if(towerConsumerValues==null)towerConsumerValues=new PredictionTowerShieldConsumer[Main.maxProjectiles];
            int count=0;bool complete=Main.projectile!=null && Main.projectile.Length>=Main.maxProjectiles;
            if(Main.projectile!=null)for(int slot=0;slot<Main.maxProjectiles && slot<Main.projectile.Length;slot++)
            {
                var p=Main.projectile[slot];if(p==null || !p.active || p.type!=629)continue;
                bool finite=Finite(p.ai[0]);int target=finite?(int)p.ai[0]:-2;bool targetCaptured=target==-1;
                var targetIdentity=default(NpcIdentity);
                // Invalid or other-type raw targets cannot decrement this
                // counter. Their world effects are not a reason to replay an
                // unrelated motor in this selected trajectory.
                if(target<0 || target>=Main.maxNPCs || target>=Main.npc.Length || Main.npc[target]==null || Main.npc[target].type!=towerType)continue;
                if(target>=0 && target<Main.maxNPCs && target<Main.npc.Length && Main.npc[target]!=null)
                {targetCaptured=true;targetIdentity=CombatSelection.Identity(Main.npc[target],session);}
                bool settings=finite && p.aiStyle==122 && !p.tileCollide && p.ignoreWater && p.damage==0 && !p.friendly && !p.minion && !p.sentry && p.extraUpdates>=0 && p.owner>=0 && p.owner<=255;
                bool ownerActive=p.owner==255 || p.owner>=0 && p.owner<Main.player.Length && Main.player[p.owner]!=null && Main.player[p.owner].active;
                towerConsumerValues[count++]=new PredictionTowerShieldConsumer(p,slot,(int)p.key,p.owner,targetCaptured,targetIdentity,new MotionRect(p.position.X,p.position.Y,p.width,p.height),p.velocity.X,p.velocity.Y,p.timeLeft,p.extraUpdates,p.penetrate,ownerActive,p.wet,settings);
            }
            bool same=towerConsumers!=null && towerConsumers.Count==count && towerConsumers.Left==Main.leftWorld && towerConsumers.Right==Main.rightWorld && towerConsumers.Top==Main.topWorld && towerConsumers.Bottom==Main.bottomWorld;
            if(same)for(int i=0;i<count;i++)if(!towerConsumerValues[i].Same(towerConsumers[i])){same=false;break;}
            if(!same)towerConsumers=new PredictionTowerShieldConsumers(towerConsumerValues,count,Main.leftWorld,Main.rightWorld,Main.topWorld,Main.bottomWorld);
            return complete;
        }
        private static bool Mage(NPC n){return n.aiStyle==109 && (n.type==564 || n.type==565);}
        private static bool InvasionTarget(NPC n){int type=n.type;return Mage(n) || n.aiStyle==110 && type==551 || n.aiStyle==111 && type==578 || InvasionFlight(n) || n.aiStyle==107 && (type>=552 && type<=557 || type>=561 && type<=563 || type>=566 && type<=573 || type==576 || type==577 || type==620);}
        private static bool InvasionFlight(NPC n)
        {return n!=null && n.aiStyle==108 && (n.type==558 || n.type==559 || n.type==560 || n.type==574 || n.type==575);}
        private static bool RequiresCrowd(NPC n)
        {return InvasionFlight(n) || n.aiStyle==111 && n.type==578 || n.aiStyle==86 && (n.type==472 || n.type==521) || n.aiStyle==122 && n.type==662 || n.aiStyle==44 && (n.type==509 || n.type==581) || n.aiStyle==85 && (n.type==405 || n.type==421) || n.aiStyle==90 && n.type==479 || n.aiStyle==95 && n.type==406 || n.aiStyle==89 && n.type==478;}
        private static int CrowdFamily(int type)
        {return type==406?405:type==478?479:type;}
        private static bool PlantMotor(NPC n)
        {return n!=null && (n.type==262 && n.aiStyle==51 || n.type==263 && n.aiStyle==52 || n.type==264 && n.aiStyle==53);}
        private static bool WallFamily(NPC n){return n!=null && (n.type==113 && n.aiStyle==27 || n.type==114 && n.aiStyle==28 || n.type==115 && n.aiStyle==29);}
        private static PredictionWallPlayer WallPlayer(Player p)
        {
            var pet=p.tankPet>=0 && p.tankPet<Main.maxProjectiles?Main.projectile[p.tankPet]:null;
            return new PredictionWallPlayer(p.whoAmI,p,new MotionRect(p.position.X,p.position.Y,p.width,p.height),p.velocity.X,p.velocity.Y,p.aggro,p.active,p.dead,p.ghost,p.gross,p.itemAnimation==0,p.npcTypeNoAggro[113],pet!=null,pet,pet==null?default(MotionRect):new MotionRect(pet.position.X,pet.position.Y,pet.width,pet.height));
        }
        private static bool WallHealthKnown(Player p)
        {
            // This first profile certifies canonical regen only. Observed DOT
            // or equipment health sources remain a necessary consumer boundary;
            // completed lifeRegen is not replayed as an alleged base value.
            for(int i=0;i<p.armor.Length;i++)if(p.armor[i]!=null && p.armor[i].type!=0)return false;
            for(int i=0;i<p.buffType.Length;i++)if(p.buffTime[i]>0 && p.buffType[i]!=37 && p.buffType[i]!=38)return false;
            return !p.sitting.isSitting && !p.sleeping.isSleeping && !p.usedAegisCrystal && !p.crimsonRegen && !p.rabid && !p.bleed && !p.wellFed && !p.honey && !p.shinyStone && !p.heartyMeal && !p.palladiumRegen && p.soulDrain==0 && !Player.SceneMetrics.HasCampfire && !Player.SceneMetrics.HasHeartLantern;
        }
        private static bool WallFallKnown(Player p)
        {
            // Canonical equipment/buffs prove the old completed fall modifiers
            // can clear at ResetEffects. Do not reject their current values:
            // noFallDmg, hasWings and extraFall are consumed BEFORE that reset.
            // Mounts, stone, rockets and alternate movement need their own fall
            // consumers; uncertainty is stopped only when Wall needs this step.
            return WallHealthKnown(p) && !p.CCed && !p.mount.Active && !p.stoned && p.rocketDelay==0 && !p.isPerformingJump_DownDash &&
                !p.pulley && p.grapCount==0 && (p.grappling==null || p.grappling.Length==0 || p.grappling[0]<0) &&
                !p.GoingDownWithGrapple && !p.onTrack && !p.IsRidingTracks && !p.gravControl && !p.gravControl2;
        }
        private void CaptureMageCorpses()
        {
            var spots=mageCorpseField?.GetValue(null) as System.Collections.Generic.List<Microsoft.Xna.Framework.Vector2>;
            if(spots==null){mageCorpses=null;return;}
            bool same=mageCorpses!=null && mageCorpses.Count==spots.Count;
            for(int i=0;same && i<spots.Count;i++)same=mageCorpses.XAt(i)==spots[i].X && mageCorpses.YAt(i)==spots[i].Y;
            if(same)return;
            var xs=new float[spots.Count];var ys=new float[spots.Count];
            for(int i=0;i<spots.Count;i++)
            {
                var spot=spots[i];if(float.IsNaN(spot.X) || float.IsInfinity(spot.X) || float.IsNaN(spot.Y) || float.IsInfinity(spot.Y)){mageCorpses=null;return;}
                xs[i]=spot.X;ys[i]=spot.Y;
            }
            mageCorpses=new PredictionMageCorpses(xs,ys,spots.Count);
        }
        private static int FirstMageHealingConsumer(int maximum)
        {
            int earliest=int.MaxValue;
            // NPCs act before projectiles. AI133 increments its float clock and
            // can heal before owner-inactive/timeLeft retirement. Therefore a
            // pulse in projectile action f first affects NPC action f+1, even
            // when the captured projectile already has timeLeft<=0.
            for(int i=0;i<Main.maxProjectiles && i<Main.projectile.Length;i++)
            {
                var q=Main.projectile[i];if(q==null || !q.active || q.type!=674)continue;
                float x=q.position.X,y=q.position.Y,clock=q.ai[0];int remaining=q.timeLeft;
                if(x<=Main.leftWorld || x+q.width>=Main.rightWorld || y<=Main.topWorld || y+q.height>=Main.bottomWorld || float.IsNaN(x) || float.IsNaN(y))continue;
                // Non-default motion/update settings require a separate effect
                // model. End before their earliest possible health consumer,
                // rather than assuming the ordinary static healing marker.
                if(q.aiStyle!=133 || q.extraUpdates!=0 || q.tileCollide || !q.ignoreWater || !Finite(clock) || !Finite(q.velocity.X) || !Finite(q.velocity.Y))
                {earliest=Math.Min(earliest,2);continue;}
                for(int action=1;action<maximum && action+1<earliest;action++)
                {
                    if(x<=Main.leftWorld || x+q.width>=Main.rightWorld || y<=Main.topWorld || y+q.height>=Main.bottomWorld || float.IsNaN(x) || float.IsNaN(y))break;
                    clock+=1f;if(clock>=40f){earliest=action+1;break;}
                    if(q.owner<255 && (q.owner<0 || q.owner>=Main.player.Length || Main.player[q.owner]==null || !Main.player[q.owner].active))break;
                    x+=q.velocity.X;y+=q.velocity.Y;
                    remaining--;if(remaining<=0 || q.penetrate==0)break;
                }
            }
            return earliest;
        }
        private static bool Finite(float value){return !float.IsNaN(value) && !float.IsInfinity(value);}
        private bool QueuePlantMotor(int slot,ref int queued)
        {
            if(slot<0 || slot>=motionSlots.Length)return false;
            motionSlots[slot]=true;if(visited[slot])return true;
            if(queued>=pending.Length)return false;
            visited[slot]=true;pending[queued++]=slot;return true;
        }
        private int SearchPlayer(float x,float y)
        {
            float distance=float.MaxValue;int winner=-1;
            // Native FindClosestPlayer prefers active/alive/non-ghost bodies,
            // then falls back to all active bodies. Strict < preserves slot
            // order at ties; no aggro or guardian score belongs to this query.
            for(int pass=0;pass<2 && winner<0;pass++)for(int i=0;i<players.Count;i++)
            {
                if(pass==0 && !players.SearchAliveAt(i))continue;
                var area=players.SearchAreaAt(i);float dx=x-area.CenterX,dy=y-area.CenterY,value=dx*dx+dy*dy;
                if(value<distance){distance=value;winner=i;}
            }
            return winner;
        }
        internal static int KnownFormType(int type)
        {return type==390?382:type==416?518:type==230?55:type==55?230:type==195?196:type==406?405:type==478?479:type==689?317:0;}
        internal static PredictionPlayerMotion ReadPlayer(Player p)
        {
            bool badHook=false;
            if(p.grappling!=null && p.grappling.Length>0 && p.grappling[0]>=0)
            {
                // Original forces consume grapCount, not unused array capacity
                // (whose trailing zero does not attach projectile slot zero).
                badHook=p.grapCount<1 || p.grapCount>p.grappling.Length;
                for(int i=0;i<Math.Min(p.grapCount,p.grappling.Length);i++)
                {int slot=p.grappling[i];var hook=slot>=0 && slot<Main.maxProjectiles?Main.projectile[slot]:null;if(hook==null || !hook.active || hook.owner!=p.whoAmI || hook.aiStyle!=7 || hook.ai==null || hook.ai.Length<1 || hook.ai[0]!=2)badHook=true;}
            }
            bool hover=p.mount.Active && (p.mount.Type==MountID.WitchBroom || p.mount.Type==5) && !p.CCed && !p.pulley && !p.shimmering && !p.tongued && (p.grappling==null || p.grappling.Length==0 || p.grappling[0]<0);
            bool floating=PredictionTerrain.FloatingObserved(p);
            // jumpSpeed/Height are shared native scratch, not this player's
            // completed observation. Derive ordinary values from owned effects.
            bool stool=p.portableStoolInfo.IsInUse;
            var held=p.HeldItem;bool umbrella=held!=null && (held.type==946 || held.type==4707) && held.holdStyle==2 && !p.petting.isPetting;
            // These exact items use no ammunition/mana/consumption. Observe
            // only their native entry gates and duration; never TryStartUse,
            // ItemCheck, equipment updates, resource writes or attacks.
            bool umbrellaUse=umbrella && !p.selectedItemState.HasBufferedChange && (!p.noItems || p.mount.Active && p.mount.DismountOnItemUse && p.mount.CanDismount(p) && !p.cursed) &&
                (p.whoAmI!=Main.myPlayer || !Main.LocalPlayerHasPendingInventoryActions()) && (p.whoAmI==Main.myPlayer || p.lastItemUseAttemptSuccess);
            var motion=new PredictionPlayerMotion{X=p.position.X,Y=p.position.Y,Vx=p.velocity.X,Vy=p.velocity.Y,Width=p.width,Height=p.height,
                WallFactsCaptured=true,WallSourceExpected=Main.wofNPCIndex>=0,WallHorrifiedTicks=p.FindBuffIndex(37)<0?0:p.buffTime[p.FindBuffIndex(37)],WallTonguedTicks=p.FindBuffIndex(38)<0?0:p.buffTime[p.FindBuffIndex(38)],WallGross=p.gross,WallTongued=p.tongued,WallLocal=p.whoAmI==Main.myPlayer,WallImmunityCaptured=true,WallImmune=p.immune,WallImmuneTime=p.immuneTime,WallHealthCaptured=true,WallHealthKnown=WallHealthKnown(p),WallLife=p.statLife,WallLifeMax=p.statLifeMax2,WallRegenCount=p.lifeRegenCount,WallRegenTime=p.lifeRegenTime,WallFallCaptured=true,WallFallKnown=WallFallKnown(p),WallFallStart=p.fallStart,WallExtraFall=p.extraFall,WallNoFallDamage=p.noFallDmg,WallHasWings=p.hasWings,
                GravityControlCaptured=true,ForcedGravityTicks=p.forcedGravity,GravityControl=p.gravControl,GravityControl2=p.gravControl2,GravityReleaseUp=p.releaseUp,GravitySourceExpected=p.forcedGravity>0 || NPC.brainOfGravity>=0 && NPC.brainOfGravity<Main.maxNPCs,
                IndependentIgnorePlatforms=p.mount.Active && (p.mount.Cart || p.mount.Type==12 || p.mount.Type==7 || p.mount.Type==8 || p.mount.Type==23 || p.mount.Type==44 || p.mount.Type==48 || p.mount.Type==55 && p.slideDir!=0) || p.GoingDownWithGrapple || p.pulley,
                BordersCaptured=true,BordersNoLimits=Terraria.Testing.DebugOptions.noLimits,BordersRemix=Main.remixWorld,BordersSkyblock=Main.skyblockWorld,BordersDead=p.dead,BordersGodMode=p.creativeGodMode,BordersForcedGravity=p.forcedGravity>0,
                BorderLeft=Main.leftWorld,BorderRight=Main.rightWorld,BorderTop=Main.topWorld,BorderBottom=Main.bottomWorld,
                UmbrellaHold=umbrella,UmbrellaUseHeld=umbrella && (p.controlUseItem || p.whoAmI==Main.myPlayer && Terraria.GameInput.PlayerInput.ShouldFastUseItem),UmbrellaReleaseUse=umbrella && p.releaseUseItem,UmbrellaUseAllowed=umbrellaUse,UmbrellaItemCheckBlocked=umbrella && p.CCed,
                UmbrellaNativeAutoReuse=umbrella && held.autoReuse && !p.noItems,UmbrellaAutoReuse=umbrella && (p.autoReuseGlove || p.stressBall || p.autoReuseAllWeapons),
                UmbrellaAnimation=umbrella?p.itemAnimation:0,UmbrellaAnimationDuration=umbrella?HostMeleeAttack.AnimationDuration(p,held):0,UmbrellaReuseDelay=umbrella?p.reuseDelay:0,
                PlayerToken=p,PlayerIndex=p.whoAmI,ObservationMechanism=Prediction.NativePlayerMotion.Mechanism(p)|(floating?256:0)|(p.isLockedToATile?512:0)|(stool?1024:0),Rope=p.pulley,InvalidMechanism=badHook,
                FloatingNow=floating,FloatMount37=p.mount.Active && p.mount.Type==37,ShimmerImmune=PredictionTerrain.PlayerShimmerImmune(p),
                Gravity=p.gravity,GravityDirection=p.gravDir,MaxFall=p.maxFallSpeed,Acceleration=hover?p.mount.Acceleration:p.runAcceleration,
                Slowdown=hover?.2f:p.runSlowdown,MaxSpeed=hover?p.mount.RunSpeed:p.maxRunSpeed,FastMaxSpeed=hover?p.mount.DashSpeed:p.chilled && p.oldStyleParkour?p.maxRunSpeed:p.accRunSpeed,
                WindSpeed=Main.windSpeedCurrent,WindPushed=p.windPushed && p.CanBePushedByWind(),TrackBoost=p.trackBoost,
                DashDelay=p.dashDelay,Wings=p.wingsLogic>0,CanFly=p.mount.CanFly(p),OnWrongGround=p.onWrongGround,PortalPhysics=p.PortalPhysicsEnabled,Jump=p.jump,
                VerticalProfile=true,Wet=p.wet,Honey=p.honeyWet,Lava=p.lavaWet,Shimmer=p.shimmerWet,DefaultGravity=Player.defaultGravity,JumpSpeedBoost=p.jumpSpeedBoost,
                Mounted=p.mount.Active,MountAdditive=p.mount.Active && p.mount.MovementStatsAreAdditive,MountJumpHeight=p.mount.Active?p.mount.JumpHeight(p.velocity.X):0,MountJumpSpeed=p.mount.Active?p.mount.JumpSpeed(p.velocity.X):0,JumpHeightExtra=stool?5:0,MerfolkEquipment=MerfolkEquipment(p),
                JumpBoost=p.jumpBoost,WereWolf=p.wereWolf,MoonLordLegs=p.moonLordLegs,Sticky=p.sticky,Dazed=p.dazed,SlowFall=p.slowFall,Vortex=p.vortexDebuff,DownDash=p.isPerformingJump_DownDash,Flipper=p.accFlipper,BaseFlipper=IndependentFlipper(p),HoverUp=p.TryingToHoverUp,HoverDown=p.TryingToHoverDown,SwimTime=p.swimTime,WetSlime=p.wetSlime,
                IgnorePlatforms=p.gravDir<0 || p.mount.Active && (p.mount.Cart || p.mount.Type==12 || p.mount.Type==7 || p.mount.Type==8 || p.mount.Type==23 || p.mount.Type==44 || p.mount.Type==48 || p.mount.Type==55 && p.slideDir!=0) || p.GoingDownWithGrapple || p.pulley,
                IgnoreWater=p.ignoreWater,Merman=p.merman,Trident=p.trident,OnTrack=p.onTrack,Cart=p.mount.Active && p.mount.Cart,SkipSlope=p.mount.Active && p.mount.Type==48,SkipConveyor=p.grapCount>0 || p.pulley || p.shimmering || p.tongued || p.isLockedToATile,
                RidingTracks=p.IsRidingTracks,StepMount=p.mount.Active && (p.mount.Type==7 || p.mount.Type==8 || p.mount.Type==12 || p.mount.Type==44 || p.mount.Type==49),Carpet=p.carpetFrame!=-1,Grappled=p.grappling!=null && p.grappling.Length>0 && p.grappling[0]>=0,UnsupportedGeometry=p.shimmering || p.tongued || p.pulley || p.grappling!=null && p.grappling.Length>0 && p.grappling[0]>=0,FloatInWater=p.ShouldFloatInWater,StairFall=p.stairFall,GfxOffset=p.gfxOffY,StepSpeed=p.stepSpeed,
                Left=p.controlLeft,Right=p.controlRight,Up=p.controlUp,Down=p.controlDown,HoldJump=p.controlJump,ReleaseJump=p.releaseJump,AutoJump=p.autoJump,Hover=hover,Complex=stool || Prediction.NativePlayerMotion.Conditional(p),WaterWalk=p.waterWalk || p.waterWalk2,LavaWalk=p.waterWalk};
            PlayerVerticalMotion.Parameters(ref motion,new PredictionEnvironment{WorldWidth=Main.maxTilesX,GravityWorldSurface=Main.worldSurface,Remix=Main.remixWorld});
            // Non-hover mounts/stool retain the established finite observation
            // premise. These samples still report their owned current jump.
            return motion;
        }
        internal static bool MerfolkEquipment(Player p)
        {
            // accMerman is cleared by Player.Update before the completed
            // observation. Read the finite owning accessories without running
            // equipment updates or using another player's static scratch.
            for(int slot=3;slot<10;slot++)
            {var item=p.GetEffectiveArmor(slot);if(!p.IsItemSlotUnlockedAndUsable(slot) || item.expertOnly && !Main.expertMode)continue;if(item.type==497 || item.type==861 || item.type==3110)return true;}
            return false;
        }
        internal static bool IndependentFlipper(Player p)
        {
            if(p.accFlipper && !p.merman && !(p.wet && p.ShouldFloatInWater))return true;
            // These are the finite native non-merfolk producers. Distinguish
            // them even when the completed aggregate also includes merfolk.
            for(int i=0;i<p.buffType.Length;i++)if(p.buffTime[i]>0)
            {int b=p.buffType[i];if(b==109 || b==131 || b==168 || b==265 || b==279 || b==305)return true;}
            for(int slot=3;slot<10;slot++)
            {
                var item=p.GetEffectiveArmor(slot);if(!p.IsItemSlotUnlockedAndUsable(slot) || item.expertOnly && !Main.expertMode)continue;
                int t=item.type;if(t==187 || t==394 || t==1860 || t==1861 || t==3994 || t==3995)return true;
            }
            return false;
        }
        internal static NpcMotionState Read(NPC n,long session)
        {
            if(n.ai==null || n.ai.Length<4 || n.localAI==null || n.localAI.Length<4 || n.immune==null || n.immune.Length<256 || n.buffType==null || n.buffType.Length!=20 || n.buffTime==null || n.buffTime.Length!=20 || n.buffImmune==null || n.buffImmune.Length<BuffID.Count)
                throw new NpcObservationFailure(CombatSelection.Identity(n,session));
            var health=new NpcHealthState{Regen=n.lifeRegen,RegenCount=n.lifeRegenCount,RealLife=n.realLife,DontTakeDamage=n.dontTakeDamage,Immortal=n.immortal,Immune255=n.immune[255],Defense=n.defense,DefaultDefense=n.defDefense,DamageMultiplier=n.takenDamageMultiplier,LavaImmune=n.lavaImmune,FireImmune=n.buffImmune[24],ShimmerImmune=n.buffImmune[353],ShimmerTransparency=n.shimmerTransparency,ShimmerAction=n.SpawnedFromStatue || NPCID.Sets.ShimmerTransformToNPC[n.type]>=0 || NPCID.Sets.ShimmerTransformToItem[n.type]>=0 || NPCID.Sets.ShimmerTownTransform[n.type]};
            int confused=0,buffHash=0,expires=0,attached=0,stinkyTicks=0;
            for(int i=0;i<n.buffType.Length;i++)if(n.buffType[i]>0 && n.buffTime[i]>0)
            {
                int time=n.buffTime[i],type=n.buffType[i];
                if(type==120){stinkyTicks=Math.Max(stinkyTicks,time);if(TownCritter(n))continue;}
                if(type==25 && TownCritter(n) && !n.isLikeATownNPC)continue;
                if(type==BuffID.Confused){confused=Math.Max(confused,time);continue;}
                if(ReadTimer(ref health,type,time))continue;
                // Locked .8 UpdateNPC_BuffApplyDOTs: attached stacks require
                // projectile ownership; other listed DOTs still have a real
                // damage timer. Their unmodeled future damage is a condition,
                // while current life/known DOT settlement remains real. Stinky
                // and tipsy are non-motion only outside AI_007 town movement.
                if((type==120 || type==25) && n.aiStyle!=7)continue;
                if(type==151 || type==169 || type==183 || type==186 || type==189 || type==337 || type==344 || type==362 || type==30 || type==375 || type==395 || type==397){attached=Math.Max(attached,time);continue;}
                // Visual and defence-only effects do not change motion under
                // the explicit NoNewHits premise; their clocks need no model.
                if(type==119 || type==320 || type==36 || type==69 || type==72 || type==203 || type==310 || type==399 || type==400)continue;
                // Locked .8: Slow has no NPC flag/AI action; Dryad's Ward
                // changes defence only under the NoNewHits motion premise.
                if(type==BuffID.Slow || type==165)continue;
                buffHash=unchecked((buffHash*397^type)*397^time);expires=expires==0?time:Math.Min(expires,time);
            }
            int parent=(n.aiStyle==6 || n.aiStyle==37) && n.ai[1]>0 && n.ai[1]<Main.maxNPCs?(int)n.ai[1]:-1;var parentNpc=parent>=0?Main.npc[parent]:null;
            int child=(n.aiStyle==6 || n.aiStyle==37) && n.ai[0]>0 && n.ai[0]<Main.maxNPCs?(int)n.ai[0]:-1;var linked=child>=0?Main.npc[child]:null;
            if(health.Fire>0 || health.Fire3>0 || n.buffType[19]!=0){health.Buffs.Captured=true;for(int i=0;i<20;i++)health.Buffs.Set(i,n.buffType[i],n.buffTime[i],Main.debuff[n.buffType[i]]);}
            var cleared=n.type==126 && n.aiStyle==31 && Main.player.Length>255?Main.player[255]:null;
            int orphanDamage=GolemMotor(n) && n.aiStyle!=45?(int)Math.Max(1d,Main.CalculateDamageNPCsTake(9999,n.defense)):0;
            if(orphanDamage>0 && n.takenDamageMultiplier>1)orphanDamage=(int)(orphanDamage*n.takenDamageMultiplier);
            var state=new NpcMotionState{RitualConfused=n.confused,CultistNetMode=CultistMotor(n) || CultistRitualMotor(n)?Main.netMode:0,CultistForeignDoomBirthAt=int.MaxValue,BrainNetMode=BrainMotor(n)?Main.netMode:0,BrainKnockBackZero=BrainMotor(n) && n.knockBackResist==0,ReflectsProjectiles=n.reflectsProjectiles,GolemBalanceCaptured=GolemMotor(n),GolemBalance=GolemMotor(n)?n.GetMyBalance():0,GolemOrphanDamageCaptured=GolemMotor(n) && n.aiStyle!=45,GolemOrphanDamage=Math.Max(1,orphanDamage),TowerNewConsumerAt=int.MaxValue,MageHealingAt=int.MaxValue,TownFactsCaptured=TownCritter(n),TownStinky=n.stinky,TownStinkyTicks=stinkyTicks,TownStinkyImmune=n.buffImmune[120],TownHomeless=n.homeless,TownHomeTileX=n.homeTileX,TownCloseDoor=n.closeDoor,TownDoorX=n.doorX,TownDoorY=n.doorY,TownDangerRange=NPCID.Sets.DangerDetectRange[n.type]==-1?200:NPCID.Sets.DangerDetectRange[n.type],TownPrettySafe=NPCID.Sets.PrettySafe[n.type],ClearedTargetAreaCaptured=cleared!=null,ClearedTargetArea=cleared==null?default(MotionRect):new MotionRect(cleared.position.X,cleared.position.Y,cleared.width,cleared.height),MotorRotationCaptured=MotorRotationNeeded(n),MotorRotation=MotorRotationNeeded(n)?n.rotation:0,HostileProtected=n.dontTakeDamageFromHostiles,KnownFormLifeMax=n.type==230 || n.type==55?n.lifeMax:KnownFormType(n.type)==0?0:(int)((n.type==390?350f:n.type==416?1000f:n.type==478?700f:300f)*Terraria.DataStructures.GameDifficultyData.EnemyMaxLifeMultiplier.Sample(Main.Difficulty)),FighterFormLifeMax=n.type==427?(int)(1000f*Terraria.DataStructures.GameDifficultyData.EnemyMaxLifeMultiplier.Sample(Main.Difficulty)):0,Alpha=n.alpha,UnmodeledDamageTicks=attached,NetOffsetX=n.netOffset.X,NetOffsetY=n.netOffset.Y,SmoothingRange=Main.multiplayerNPCSmoothingRange,ResetNetOffset=Main.netMode==2 || NPC.offSetDelayTime>0 || NPCID.Sets.NoMultiplayerSmoothingByType[n.type] || NPCID.Sets.NoMultiplayerSmoothingByAI[n.aiStyle] || n.townNPC && n.ai[0]==25,Friendly=n.friendly,ParentIdentity=parentNpc!=null?CombatSelection.Identity(parentNpc,session):default(NpcIdentity),ChildSlot=child,ChildIdentity=linked!=null && linked.active && linked.aiStyle==n.aiStyle?CombatSelection.Identity(linked,session):default(NpcIdentity),LavaSpeed=n.lavaMovementSpeed,ShimmerSpeed=n.shimmerMovementSpeed,Lava=n.lavaWet,Shimmer=n.shimmerWet,Health=health,Identity=CombatSelection.Identity(n,session),X=n.position.X,Y=n.position.Y,OldX=n.oldPosition.X,OldY=n.oldPosition.Y,StairFall=n.stairFall,Vx=n.velocity.X,Vy=n.velocity.Y,OldVx=n.oldVelocity.X,OldVy=n.oldVelocity.Y,Width=n.width,Height=n.height,Scale=n.scale,Style=n.aiStyle,Direction=n.direction,DirectionY=n.directionY,Target=n.target,
                CollisionPart=HasCollisionPart(n),Town=n.townNPC,HomeTileY=n.homeTileY,CritterTurns=NPCID.Sets.CritterThatCanTurnOnPlayers[n.type],Boss=n.boss,InactivityImmune=n.DoesntDespawnToInactivity() || n.townNPC,SpriteDirection=n.spriteDirection,SpawnedFromStatue=n.SpawnedFromStatue,ParentSlot=(n.aiStyle==6 || n.aiStyle==37) && n.ai[1]>0?(int)n.ai[1]:-1,TimeLeft=n.timeLeft,ConfusedTicks=confused,Life=n.life,LifeMax=n.lifeMax,BuffFingerprint=buffHash,BuffExpires=expires,WaterSpeed=n.waterMovementSpeed,HoneySpeed=n.honeyMovementSpeed,
                A0=n.ai[0],A1=n.ai[1],A2=n.ai[2],A3=n.ai[3],L0=n.localAI[0],L1=n.localAI[1],L2=n.localAI[2],L3=n.localAI[3],Active=n.active,NoGravity=n.noGravity,NoTileCollide=n.noTileCollide,Wet=n.wet,WetCount=n.wetCount,Honey=n.honeyWet,CollideX=n.collideX,CollideY=n.collideY,CanReceive=CombatSelection.Receives(n,true),CanHarm=!n.friendly && n.damage>0,NoContactDamage=n.damage==0,JustHit=n.justHit};
            if(n.type==392 || n.type==491)
            {
                for(int i=0;i<4;i++)
                {
                    int slot=(int)(n.type==392?n.localAI[i]:n.ai[i]),kind=n.type==491?492:i<2?393:394;NpcIdentity id=default(NpcIdentity);
                    if(slot>=0 && slot<Main.maxNPCs && slot<Main.npc.Length){var part=Main.npc[slot];if(part!=null && part.active && part.type==kind)id=CombatSelection.Identity(part,session);}
                    if(i==0)state.WeaponPart0=id;else if(i==1)state.WeaponPart1=id;else if(i==2)state.WeaponPart2=id;else state.WeaponPart3=id;
                }
            }
            if(EmpressMotor(n))CaptureEmpressSample(n,ref state,session);
            if(MoonLordMotor(n))CaptureMoonLordSample(n,ref state,session);
            if(CultistRitualMotor(n))CaptureCultistRitualSample(n,ref state,session);
            if(n.type==594 && n.aiStyle==113 || n.type==1 && n.aiStyle==1)CaptureWindyBalloonSample(n,ref state,session);
            if(BirdMotor(n))CaptureBirdSample(n,ref state,session);
            if(SegmentedWormMotor(n))CaptureSegmentedWormSample(n,ref state,session);
            return state;
        }
        private static bool MotorRotationNeeded(NPC n){return n.type==618 && n.aiStyle==117 || n.type==126 && n.aiStyle==31 || n.type==395 && n.aiStyle==76 || n.type==491 && n.aiStyle==93 || n.aiStyle==75 && (n.type==392 || n.type==393 || n.type==394 || n.type==492);}
        private static bool HasCollisionPart(NPC n)
        {
            int part=n.type==391?390:n.type==415?416:-1;if(part<0)return false;
            // Only these two native movement boxes depend on an attached live
            // part. Sample once on the game thread, never rescan for 120 steps.
            for(int i=0;i<Main.maxNPCs;i++)
            {var other=Main.npc[i];if(other!=null && other.active && other.type==part && other.ai[0]==n.whoAmI)return true;}
            return false;
        }
        private static bool ReadTimer(ref NpcHealthState h,int type,int time)
        {
            switch(type)
            {case 153:h.Shadow=time;break;case 20:h.Poison=time;break;case 24:h.Fire=time;break;case 39:h.Cursed=time;break;case 70:h.Venom=time;break;case 44:h.Frost=time;break;case 323:h.Fire3=time;break;case 324:h.Frost2=time;break;case 137:h.Slimed=time;break;case 204:h.Oil=time;break;case 398:h.Accelerated=time;break;case 103:h.WetBuff=time;break;case 353:h.ShimmerTicks=time;break;default:return false;}return true;
        }
    }
}
