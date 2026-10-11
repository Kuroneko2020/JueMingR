using System;

namespace JueMingR.Platform.Combat
{
    // Token is compared by reference only. Neither models nor consumers may
    // dereference it. Slot reuse, transformation and Session changes all retire
    // a result even if the new occupant has identical coordinates.
    public struct NpcIdentity : IEquatable<NpcIdentity>
    {
        public readonly long Session;
        public readonly object Token;
        public readonly int Slot,Generation,Type,NetId;
        public NpcIdentity(long session,object token,int slot,int generation,int type,int netId)
        {Session=session;Token=token;Slot=slot;Generation=generation;Type=type;NetId=netId;}
        public bool Equals(NpcIdentity b){return Session==b.Session && ReferenceEquals(Token,b.Token) && Slot==b.Slot && Generation==b.Generation && Type==b.Type && NetId==b.NetId;}
        public override bool Equals(object b){return b is NpcIdentity && Equals((NpcIdentity)b);}
        public override int GetHashCode(){return Slot^Generation^Type^Session.GetHashCode();}
    }
    [Flags]
    public enum PredictionAssumption { None=0, TargetPlayerStationary=1, FixedTarget=2, NoNewHits=4, RandomRepresentative=8, LocalTerrain=16, NetworkObservation=32, ApproximateMechanism=64, HeldPlayerControls=128, CurrentConnection=256, ObservedLighting=512, UnmodeledDamageEffects=1024, CurrentPlayerObservation=2048, UnmodeledStatusEffects=4096, ObservedTrackingMotion=8192, NoPlayerMotionNeeded=16384 }
    public enum PredictionStop { None, UnsupportedMechanism, RandomDestination, MissingDependency, TerrainUnavailable, TerrainLimit, Slope, LiquidEffect, BuffTransition, Despawn, PhaseBoundary, InvalidState, RandomDecision }
    public enum PredictionStrategy { Model, NativeIsolated, SegmentedTrend, RollingConditional }
    public enum PredictionQuality { Conditional, LimitedObservation, ObservedTrend, StructuredApproximation }
    public enum PredictionFailureLayer { None, Source, PlayerPremise, NpcMotion }
    public struct MotionRect
    {
        public float X,Y,Width,Height;
        public MotionRect(float x,float y,float width,float height){X=x;Y=y;Width=width;Height=height;}
        public float CenterX {get{return X+Width*.5f;}}
        public float CenterY {get{return Y+Height*.5f;}}
    }
    // Independent scalar state: no shared ai/localAI/buff arrays, delegates or
    // Terraria entities. Every field here has one game-thread prediction owner.
    public struct NpcMotionState
    {
        public NpcIdentity Identity;
        public NpcIdentity ChildIdentity;
        // A slot alone cannot preserve a worm constraint after slot reuse.
        // The game-thread observer captures the current parent instance.
        public NpcIdentity ParentIdentity;
        // AI31 reuses the flame launch origin in its MechQueen motor. Its
        // rotation is therefore necessary movement state, not only drawing.
        public bool MotorRotationCaptured;
        public float MotorRotation;
        // AI31's ClearTarget action subsequently queries the real dummy slot
        // 255 rectangle. It is not the previous numbered-player timeline.
        public bool ClearedTargetAreaCaptured;
        public MotionRect ClearedTargetArea;
        // Exact direct-position dependency, distinct from Health.RealLife.
        // Models compare the opaque full owner identity without dereferencing.
        public NpcIdentity PositionOwner;
        // AI12 distinguishes a sampled invalid native owner from an absent
        // necessary full identity. Server orphan clocks differ from clients.
        public bool PositionOwnerCaptured,PositionOwnerValid,PositionInvalidRetireImmediately;
        public bool SkeletonRelationsCaptured,SkeletonRelationsComplete;
        // AI27 writes the raw WOF global in slot order. A captured nonnegative
        // owner may still be inactive until the next CheckBossIndexes action.
        public bool WallRelationsCaptured,WallRelationsComplete,WallBossCaptured,WallBossValid;
        public NpcIdentity WallBossIdentity;
        public int WallTop,WallBottom;
        public bool PlayerGross,ClosestPlayerGross;
        // AI54 writes crimsonBoss per slot; AI55 consumes its full identity.
        public bool BrainRelationsCaptured,BrainRelationsComplete,BrainBossCaptured,BrainBossValid,BrainKnockBackZero;
        public NpcIdentity BrainBossIdentity;
        public int BrainNetMode;
        // AI45..48 share the scheduled native golemBoss and fixed scaling.
        // Orphan strikes consume observed damage, not assumed instant death.
        public bool GolemRelationsCaptured,GolemRelationsComplete,GolemBossCaptured,GolemBossValid,GolemBalanceCaptured,GolemOrphanDamageCaptured;
        public NpcIdentity GolemBossIdentity;
        public float GolemBalance;
        public int GolemOrphanDamage;
        public bool PlayerTemple,ClosestPlayerTemple;
        // AI75 mounts, AI76 core feedback and AI93 cannon lifetimes share a
        // native slot-ordered component. Null part identities are sampled
        // invalid occupants only when this closure was explicitly captured.
        public bool WeaponRelationsCaptured,WeaponRelationsComplete;
        public NpcIdentity WeaponPart0,WeaponPart1,WeaponPart2,WeaponPart3;
        // A future AI415 spawn changes the global AI516 count. It ends the
        // next count consumer, without creating an actor in this value world.
        public int WeaponSpawnAt;
        // AI94 counters are consumed by observed souls after the NPC loop.
        // A possible DOT-created new soul ends its first required consumer,
        // without freezing another actor's health or inventing a projectile.
        public bool TowerRelationsCaptured,TowerRelationsComplete,TowerShieldCaptured;
        public int TowerShieldStrength,TowerShieldMaximum,TowerNewConsumerAt;
        // AI109 only consumes wounded geometry at its real ready action.
        // Corpse consumption is shared in native slot order in the private
        // group; these event fields never mutate the captured source pool.
        public bool MageRelationsCaptured,MageRelationsComplete,MageRaiseOccurred;
        public int MageHealingAt;
        public float MageRaiseX,MageRaiseY;
        public int PositionRelation;
        // FindClosestPlayer is Euclidean and does not change NPC.target.
        // These facts must stay separate from TargetClosest's numbered winner.
        public bool SearchPlayerCaptured,SearchPlayerAlive,SearchPlayerDead;
        public int SearchPlayerIndex;
        public float PositionParameter;
        // Only a modeled same-object form changes this value. Identity
        // remains the real starting instance; a real Transform retires it.
        public int MotionType;
        public int FighterFormLifeMax;
        // Exact difficulty-scaled defaults for a known deterministic form.
        // This is observed from locked scalar rules, never SetDefaults/AI.
        public int KnownFormLifeMax;
        public bool FormTargetNoAggro,FormClosestNoAggro;
        public bool FighterFormReady;
        public int EffectiveType {get{return MotionType==0?Identity.Type:MotionType;}}
        public NpcHealthState Health;
        public bool Friendly,Town,CollisionPart;
        public int HomeTileY;
        public bool TownFactsCaptured,TownRelationsCaptured,TownRelationsComplete,TownStinky,TownStinkyImmune,TownHomeless,TownCloseDoor;
        public int TownHomeTileX,TownDoorX,TownDoorY,TownStinkyTicks,TownDangerRange,TownPrettySafe;
        // AI_003's spawn/reveal action can precede its common motor.
        public int Alpha;
        // The numbered player supplies environmental/direct-player predicates.
        // Tracking geometry may instead belong to a guardian or encoded NPC.
        public MotionRect PlayerArea,TrackingArea;
        public int PlayerIndex,TrackingKind;
        // Headcrab attachment uses numbered-player facing/gravity, distinct
        // from NPC facing or guardian tracking. Held with that sampled player.
        public int PlayerDirection,ClosestPlayerDirection;
        public float PlayerGravityDirection,ClosestPlayerGravityDirection;
        // AI54's retreat reads the numbered target's real biome. Keep each
        // sampled player's fact with its identity across TargetClosest.
        public bool PlayerCrimson,ClosestPlayerCrimson,PlayerJungle,ClosestPlayerJungle;
        // Direct numbered-player velocity is not guardian tracking velocity
        // or the position delta used by a finite special-player estimate.
        public float PlayerVx,PlayerVy,ClosestPlayerVx,ClosestPlayerVy;
        public bool PlayerVelocityCaptured,ClosestPlayerVelocityCaptured;
        // The same captured motion fact supplies exact numbered-player Center;
        // PlayerArea remains native integer tracking/collision geometry.
        public float PlayerCenterX,PlayerCenterY,ClosestPlayerCenterX,ClosestPlayerCenterY;
        // AI58/59/60 also read native float top-left. Integer tracking rectangles
        // and center-minus-half are not equivalent at strict position gates.
        public bool PlayerPreciseAreaCaptured,ClosestPlayerPreciseAreaCaptured;
        public MotionRect PlayerPreciseArea,ClosestPlayerPreciseArea;
        // Future-only AI58 mode choice: daytime motor can continue without
        // inventing a random mode; a later night consumer must stop.
        public bool PumpkingModeUnknown;
        // Future-only AI60 clock interval. Random increments do not alter
        // motion until they can select different native phase branches.
        public bool IceQueenClockRange;
        public float IceQueenClockLower,IceQueenClockUpper;
        // GetTargetData(true), distinct from TargetClosest's candidate and
        // the numbered-player platform predicate. Invalid data has center0.
        public int LiquidTargetKind,LiquidTargetPlayer,WetCount;
        public float LiquidTargetY,LiquidTargetVy;
        public float TrackingVx,TrackingVy;
        // AI26 observes current wind/zone and a bounded current neighbor
        // impulse; forecasts never borrow or advance the live neighbor AI.
        public float RunPushX,RunPushY;
        // The producer captures the complete active same-type relation for
        // crowd motors. A finite incomplete group is never treated as empty.
        public bool CrowdCaptured,CrowdComplete;
        // Plantera reads a frame-ordered global independently of an encoded
        // tentacle position owner. These immutable facts belong to the sample;
        // future kernels resolve the changing global from the complete group.
        public bool PlantRelationsCaptured,PlantRelationsComplete,PlantBossValid;
        public NpcIdentity PlantBossIdentity;
        // GetTargetData validity includes ghost, unlike direct player reads.
        // Kept separate so acquiring this fact cannot redefine PlayerDead.
        public bool AimedTargetCaptured,AimedTargetValid;
        // DD2's Euclidean NPC/player search is distinct from TargetClosest.
        // Full candidate closure and the current encoded target belong to
        // this observation; an active NPC with zero life is still a valid aim.
        public bool DD2TargetFactsCaptured,DD2TargetFactsComplete,HostileProtected;
        public bool DD2PlayerPremiseCaptured;
        public int DD2PlayerPremiseIndex,DD2OldTarget;
        public NpcIdentity DD2TargetIdentity;
        public object DD2TargetPlayerToken;
        public MotionRect DD2FaceArea;
        public bool PlayerDesert,PlayerSandstorm,ClosestPlayerDesert,ClosestPlayerSandstorm,UncertainRunCooldown,RunRetirePlayer;
        // AI5 bees acquire one native non-bee search at the real sample. Facing
        // can use a guardian while GetTargetData still moves toward its owner.
        public NpcIdentity TrackingIdentity;
        public object TrackingPlayerToken;
        public MotionRect BeeFacingArea;
        public int BeeTarget,BeeOldTarget;
        public float BeeTankAggro,BeeFacingVx,BeeFacingVy;
        public bool BeeTargetValid,BeeFaceForced,BeeFoundTarget,BeeHasNpcCandidate,BeeTankPet;
        // Current global Queen identity is shared by the slot-ordered mechanical
        // models. Their closure advances the actual required scalar motors;
        // AI139 separately retains its qualified observed attachment pose.
        public NpcIdentity MechQueenIdentity,MechLinkIdentity;
        public MotionRect MechLinkArea;
        public float MechQueenVx,MechQueenVy,MechLinkVx,MechLinkVy,MechLinkRotation;
        public int MechLinkSlot;
        public bool MechFactsCaptured;
        // Direct-player lifecycle predicates differ from GetTargetData validity.
        public bool PlayerActive,PlayerNativeDead,PlayerGhost,ClosestPlayerActive,ClosestPlayerNativeDead,ClosestPlayerGhost;
        public bool EyeRelationsCaptured,EyeRelationsComplete,MechDestroyerCaptured,MechDestroyerPresent;
        public bool TargetCaptured,HasPlayer,PlayerDead,PlayerWet,PlayerIdle,PlayerGraveyard,PlayerAttackHidden;
        public MotionRect ClosestPlayerArea;
        public int ClosestPlayerIndex;
        public bool HasClosestPlayer,ClosestPlayerDead,ClosestPlayerWet,ClosestPlayerIdle,ClosestPlayerNoAggro,ClosestPlayerGraveyard,ClosestPlayerAttackHidden;
        // Unknown guardian visibility is distinct from known obstruction. It
        // matters only when a modeled native TargetClosest consumes the choice.
        public bool TargetChoiceUnknown,TargetChoiceUnavailable;
        public bool PlayerHeadCollision,PlayerDryHeadCollision;
        public float NetOffsetX,NetOffsetY,SmoothingRange;
        public bool ResetNetOffset;
        public float X,Y,Vx,Vy,OldX,OldY,OldVx,OldVy,Scale,Gravity,MaxFall,WaterSpeed,HoneySpeed,LavaSpeed,ShimmerSpeed;
        public float A0,A1,A2,A3,L0,L1,L2,L3;
        public float ObservedAccelerationX,ObservedAccelerationY,ObservedTurn;
        // Private forecast scratch for the transient fallback, never observed
        // native ai/localAI slots or state written back to a live NPC.
        public float TrendBaseVx,TrendBaseVy;
        public float TrendOutputVx,TrendOutputVy;
        // AI39's next random 2..4 bounce budget shares motion through its
        // first contact. A private lower bound ends before contact divergence.
        public bool UncertainBounceCount;
        public bool TrendInitialized,TrendApplied;
        public int UnmodeledDamageTicks;
        public int Width,Height,Style,Direction,DirectionY,SpriteDirection,Target,ParentSlot,ChildSlot,TimeLeft,ConfusedTicks,Life,LifeMax,BuffFingerprint,BuffExpires;
        public bool Active,ReflectsProjectiles,ProjectileReflectionUnknown,NoGravity,NoTileCollide,Wet,Honey,Lava,Shimmer,CollideX,CollideY,CanReceive,CanHarm,NewSegment,JustHit,StairFall,SpawnedFromStatue,Boss,InactivityImmune,TargetNoAggro,CritterTurns,NoContactDamage;
        public MotionRect Bounds {get{return new MotionRect(X,Y,Width,Height);}}
        // Compare only the bounded pure-value sample already acquired for this
        // forecast; this is not a scan or hash of the live world.
        public bool SameSample(NpcMotionState b)
        {return IceQueenClockRange==b.IceQueenClockRange && IceQueenClockLower==b.IceQueenClockLower && IceQueenClockUpper==b.IceQueenClockUpper && PumpkingModeUnknown==b.PumpkingModeUnknown && PlayerPreciseAreaCaptured==b.PlayerPreciseAreaCaptured && ClosestPlayerPreciseAreaCaptured==b.ClosestPlayerPreciseAreaCaptured && PredictionPlayers.Same(PlayerPreciseArea,b.PlayerPreciseArea) && PredictionPlayers.Same(ClosestPlayerPreciseArea,b.ClosestPlayerPreciseArea) && TowerRelationsCaptured==b.TowerRelationsCaptured && TowerRelationsComplete==b.TowerRelationsComplete && TowerShieldCaptured==b.TowerShieldCaptured && TowerShieldStrength==b.TowerShieldStrength && TowerShieldMaximum==b.TowerShieldMaximum && TowerNewConsumerAt==b.TowerNewConsumerAt && WallRelationsCaptured==b.WallRelationsCaptured && WallRelationsComplete==b.WallRelationsComplete && WallBossCaptured==b.WallBossCaptured && WallBossValid==b.WallBossValid && WallBossIdentity.Equals(b.WallBossIdentity) && WallTop==b.WallTop && WallBottom==b.WallBottom && PlayerGross==b.PlayerGross && ClosestPlayerGross==b.ClosestPlayerGross && BrainRelationsCaptured==b.BrainRelationsCaptured && BrainRelationsComplete==b.BrainRelationsComplete && BrainBossCaptured==b.BrainBossCaptured && BrainBossValid==b.BrainBossValid && BrainKnockBackZero==b.BrainKnockBackZero && BrainBossIdentity.Equals(b.BrainBossIdentity) && BrainNetMode==b.BrainNetMode && GolemRelationsCaptured==b.GolemRelationsCaptured && GolemRelationsComplete==b.GolemRelationsComplete && GolemBossCaptured==b.GolemBossCaptured && GolemBossValid==b.GolemBossValid && GolemBossIdentity.Equals(b.GolemBossIdentity) && GolemBalanceCaptured==b.GolemBalanceCaptured && GolemBalance==b.GolemBalance && GolemOrphanDamageCaptured==b.GolemOrphanDamageCaptured && GolemOrphanDamage==b.GolemOrphanDamage && PlayerTemple==b.PlayerTemple && ClosestPlayerTemple==b.ClosestPlayerTemple && MageRelationsCaptured==b.MageRelationsCaptured && MageRelationsComplete==b.MageRelationsComplete && MageRaiseOccurred==b.MageRaiseOccurred && MageHealingAt==b.MageHealingAt && MageRaiseX==b.MageRaiseX && MageRaiseY==b.MageRaiseY && WeaponRelationsCaptured==b.WeaponRelationsCaptured && WeaponRelationsComplete==b.WeaponRelationsComplete && WeaponPart0.Equals(b.WeaponPart0) && WeaponPart1.Equals(b.WeaponPart1) && WeaponPart2.Equals(b.WeaponPart2) && WeaponPart3.Equals(b.WeaponPart3) && WeaponSpawnAt==b.WeaponSpawnAt && TownFactsCaptured==b.TownFactsCaptured && TownRelationsCaptured==b.TownRelationsCaptured && TownRelationsComplete==b.TownRelationsComplete && TownStinky==b.TownStinky && TownStinkyImmune==b.TownStinkyImmune && TownHomeless==b.TownHomeless && TownCloseDoor==b.TownCloseDoor && TownHomeTileX==b.TownHomeTileX && TownDoorX==b.TownDoorX && TownDoorY==b.TownDoorY && TownStinkyTicks==b.TownStinkyTicks && TownDangerRange==b.TownDangerRange && TownPrettySafe==b.TownPrettySafe && ClearedTargetAreaCaptured==b.ClearedTargetAreaCaptured && PredictionPlayers.Same(ClearedTargetArea,b.ClearedTargetArea) && MotorRotationCaptured==b.MotorRotationCaptured && MotorRotation==b.MotorRotation && PlayerActive==b.PlayerActive && PlayerNativeDead==b.PlayerNativeDead && PlayerGhost==b.PlayerGhost && ClosestPlayerActive==b.ClosestPlayerActive && ClosestPlayerNativeDead==b.ClosestPlayerNativeDead && ClosestPlayerGhost==b.ClosestPlayerGhost && EyeRelationsCaptured==b.EyeRelationsCaptured && EyeRelationsComplete==b.EyeRelationsComplete && MechDestroyerCaptured==b.MechDestroyerCaptured && MechDestroyerPresent==b.MechDestroyerPresent && DD2TargetFactsCaptured==b.DD2TargetFactsCaptured && DD2TargetFactsComplete==b.DD2TargetFactsComplete && HostileProtected==b.HostileProtected && DD2PlayerPremiseCaptured==b.DD2PlayerPremiseCaptured && DD2PlayerPremiseIndex==b.DD2PlayerPremiseIndex && DD2OldTarget==b.DD2OldTarget && DD2TargetIdentity.Equals(b.DD2TargetIdentity) && ReferenceEquals(DD2TargetPlayerToken,b.DD2TargetPlayerToken) && PredictionPlayers.Same(DD2FaceArea,b.DD2FaceArea) && PlantRelationsCaptured==b.PlantRelationsCaptured && PlantRelationsComplete==b.PlantRelationsComplete && PlantBossValid==b.PlantBossValid && PlantBossIdentity.Equals(b.PlantBossIdentity) && PlayerJungle==b.PlayerJungle && ClosestPlayerJungle==b.ClosestPlayerJungle && PlayerCenterX==b.PlayerCenterX && PlayerCenterY==b.PlayerCenterY && ClosestPlayerCenterX==b.ClosestPlayerCenterX && ClosestPlayerCenterY==b.ClosestPlayerCenterY && PlayerVelocityCaptured==b.PlayerVelocityCaptured && ClosestPlayerVelocityCaptured==b.ClosestPlayerVelocityCaptured && PlayerVx==b.PlayerVx && PlayerVy==b.PlayerVy && ClosestPlayerVx==b.ClosestPlayerVx && ClosestPlayerVy==b.ClosestPlayerVy && PlayerCrimson==b.PlayerCrimson && ClosestPlayerCrimson==b.ClosestPlayerCrimson && SearchPlayerCaptured==b.SearchPlayerCaptured && SearchPlayerAlive==b.SearchPlayerAlive && SearchPlayerDead==b.SearchPlayerDead && SearchPlayerIndex==b.SearchPlayerIndex && PlayerDirection==b.PlayerDirection && ClosestPlayerDirection==b.ClosestPlayerDirection && PlayerGravityDirection==b.PlayerGravityDirection && ClosestPlayerGravityDirection==b.ClosestPlayerGravityDirection && KnownFormLifeMax==b.KnownFormLifeMax && FormTargetNoAggro==b.FormTargetNoAggro && FormClosestNoAggro==b.FormClosestNoAggro && CrowdCaptured==b.CrowdCaptured && CrowdComplete==b.CrowdComplete && AimedTargetCaptured==b.AimedTargetCaptured && AimedTargetValid==b.AimedTargetValid && MechFactsCaptured==b.MechFactsCaptured && MechQueenIdentity.Equals(b.MechQueenIdentity) && MechLinkIdentity.Equals(b.MechLinkIdentity) && PredictionPlayers.Same(MechLinkArea,b.MechLinkArea) && MechQueenVx==b.MechQueenVx && MechQueenVy==b.MechQueenVy && MechLinkVx==b.MechLinkVx && MechLinkVy==b.MechLinkVy && MechLinkRotation==b.MechLinkRotation && MechLinkSlot==b.MechLinkSlot && FighterFormReady==b.FighterFormReady && FighterFormLifeMax==b.FighterFormLifeMax && PlayerAttackHidden==b.PlayerAttackHidden && ClosestPlayerAttackHidden==b.ClosestPlayerAttackHidden && RunRetirePlayer==b.RunRetirePlayer && RunPushX==b.RunPushX && RunPushY==b.RunPushY && PlayerDesert==b.PlayerDesert && PlayerSandstorm==b.PlayerSandstorm && ClosestPlayerDesert==b.ClosestPlayerDesert && ClosestPlayerSandstorm==b.ClosestPlayerSandstorm && UncertainRunCooldown==b.UncertainRunCooldown && UncertainBounceCount==b.UncertainBounceCount && Identity.Equals(b.Identity) && TrackingIdentity.Equals(b.TrackingIdentity) && ReferenceEquals(TrackingPlayerToken,b.TrackingPlayerToken) && PredictionPlayers.Same(BeeFacingArea,b.BeeFacingArea) && BeeTarget==b.BeeTarget && BeeTargetValid==b.BeeTargetValid && BeeFaceForced==b.BeeFaceForced && BeeFoundTarget==b.BeeFoundTarget && BeeHasNpcCandidate==b.BeeHasNpcCandidate && BeeTankPet==b.BeeTankPet && BeeOldTarget==b.BeeOldTarget && BeeTankAggro==b.BeeTankAggro && BeeFacingVx==b.BeeFacingVx && BeeFacingVy==b.BeeFacingVy && PositionOwnerCaptured==b.PositionOwnerCaptured && PositionOwnerValid==b.PositionOwnerValid && PositionInvalidRetireImmediately==b.PositionInvalidRetireImmediately && SkeletonRelationsCaptured==b.SkeletonRelationsCaptured && SkeletonRelationsComplete==b.SkeletonRelationsComplete && ParentIdentity.Equals(b.ParentIdentity) && ChildIdentity.Equals(b.ChildIdentity) && PositionOwner.Equals(b.PositionOwner) && PositionRelation==b.PositionRelation && PositionParameter==b.PositionParameter && MotionType==b.MotionType && Health.Equals(b.Health) && Friendly==b.Friendly && Town==b.Town && CollisionPart==b.CollisionPart && HomeTileY==b.HomeTileY && Alpha==b.Alpha && PredictionPlayers.Same(PlayerArea,b.PlayerArea) && PredictionPlayers.Same(TrackingArea,b.TrackingArea) && PlayerIndex==b.PlayerIndex && TrackingKind==b.TrackingKind && LiquidTargetKind==b.LiquidTargetKind && LiquidTargetPlayer==b.LiquidTargetPlayer && WetCount==b.WetCount && LiquidTargetY==b.LiquidTargetY && LiquidTargetVy==b.LiquidTargetVy && TrackingVx==b.TrackingVx && TrackingVy==b.TrackingVy && TargetCaptured==b.TargetCaptured && HasPlayer==b.HasPlayer && PlayerDead==b.PlayerDead && PlayerWet==b.PlayerWet && PlayerIdle==b.PlayerIdle && PlayerGraveyard==b.PlayerGraveyard && PredictionPlayers.Same(ClosestPlayerArea,b.ClosestPlayerArea) && ClosestPlayerIndex==b.ClosestPlayerIndex && HasClosestPlayer==b.HasClosestPlayer && ClosestPlayerDead==b.ClosestPlayerDead && ClosestPlayerWet==b.ClosestPlayerWet && ClosestPlayerIdle==b.ClosestPlayerIdle && ClosestPlayerNoAggro==b.ClosestPlayerNoAggro && ClosestPlayerGraveyard==b.ClosestPlayerGraveyard && TargetChoiceUnknown==b.TargetChoiceUnknown && TargetChoiceUnavailable==b.TargetChoiceUnavailable && PlayerHeadCollision==b.PlayerHeadCollision && PlayerDryHeadCollision==b.PlayerDryHeadCollision && NetOffsetX==b.NetOffsetX && NetOffsetY==b.NetOffsetY && SmoothingRange==b.SmoothingRange && ResetNetOffset==b.ResetNetOffset && X==b.X && Y==b.Y && Vx==b.Vx && Vy==b.Vy && OldX==b.OldX && OldY==b.OldY && OldVx==b.OldVx && OldVy==b.OldVy && Scale==b.Scale && Gravity==b.Gravity && MaxFall==b.MaxFall && WaterSpeed==b.WaterSpeed && HoneySpeed==b.HoneySpeed && LavaSpeed==b.LavaSpeed && ShimmerSpeed==b.ShimmerSpeed && A0==b.A0 && A1==b.A1 && A2==b.A2 && A3==b.A3 && L0==b.L0 && L1==b.L1 && L2==b.L2 && L3==b.L3 && ObservedAccelerationX==b.ObservedAccelerationX && ObservedAccelerationY==b.ObservedAccelerationY && ObservedTurn==b.ObservedTurn && TrendBaseVx==b.TrendBaseVx && TrendBaseVy==b.TrendBaseVy && UnmodeledDamageTicks==b.UnmodeledDamageTicks && Width==b.Width && Height==b.Height && Style==b.Style && Direction==b.Direction && DirectionY==b.DirectionY && SpriteDirection==b.SpriteDirection && Target==b.Target && ParentSlot==b.ParentSlot && ChildSlot==b.ChildSlot && TimeLeft==b.TimeLeft && ConfusedTicks==b.ConfusedTicks && Life==b.Life && LifeMax==b.LifeMax && BuffFingerprint==b.BuffFingerprint && BuffExpires==b.BuffExpires && Active==b.Active && ReflectsProjectiles==b.ReflectsProjectiles && ProjectileReflectionUnknown==b.ProjectileReflectionUnknown && NoGravity==b.NoGravity && NoTileCollide==b.NoTileCollide && Wet==b.Wet && Honey==b.Honey && Lava==b.Lava && Shimmer==b.Shimmer && CollideX==b.CollideX && CollideY==b.CollideY && CanReceive==b.CanReceive && CanHarm==b.CanHarm && NewSegment==b.NewSegment && JustHit==b.JustHit && StairFall==b.StairFall && SpawnedFromStatue==b.SpawnedFromStatue && Boss==b.Boss && InactivityImmune==b.InactivityImmune && TargetNoAggro==b.TargetNoAggro && CritterTurns==b.CritterTurns && NoContactDamage==b.NoContactDamage;}

    }
    public struct NpcHealthState : IEquatable<NpcHealthState>
    {
        public NpcBuffLayout Buffs;
        public int Poison,Fire,Cursed,Venom,Frost,Fire3,Frost2,Shadow,Slimed,Oil,Accelerated,WetBuff,ShimmerTicks,Regen,RegenCount,RealLife,Immune255,Defense,DefaultDefense;
        public float ShimmerTransparency,DamageMultiplier;
        public bool DontTakeDamage,Immortal,LavaImmune,FireImmune,ShimmerImmune,ShimmerAction;
        public bool Equals(NpcHealthState b)
        {return Buffs.Equals(b.Buffs) && Poison==b.Poison && Fire==b.Fire && Cursed==b.Cursed && Venom==b.Venom && Frost==b.Frost && Fire3==b.Fire3 && Frost2==b.Frost2 && Shadow==b.Shadow && Slimed==b.Slimed && Oil==b.Oil && Accelerated==b.Accelerated && WetBuff==b.WetBuff && ShimmerTicks==b.ShimmerTicks && Regen==b.Regen && RegenCount==b.RegenCount && RealLife==b.RealLife && Immune255==b.Immune255 && Defense==b.Defense && DefaultDefense==b.DefaultDefense && ShimmerTransparency==b.ShimmerTransparency && DamageMultiplier==b.DamageMultiplier && DontTakeDamage==b.DontTakeDamage && Immortal==b.Immortal && LavaImmune==b.LavaImmune && FireImmune==b.FireImmune && ShimmerImmune==b.ShimmerImmune && ShimmerAction==b.ShimmerAction;}
    }
    public struct PredictionEnvironment : IEquatable<PredictionEnvironment>
    {
        public PredictionPlayers Players;
        public PredictionInvasionPlayers InvasionPlayers;
        public PredictionMageCorpses MageCorpses;
        public PredictionTowerShieldConsumers TowerConsumers;
        public PredictionGravitySources GravitySources;
        public PredictionWallPlayers WallPlayers;
        public PredictionWallSources WallSources;
        public PredictionWallSource WallSource;
        public bool PlayerGross,WallTimelineCaptured,WallTimelineGross;
        public int WallTimelineIndex,WallViewWidth;
        public MotionRect WallTimelineArea;
        public bool GravitySourceCaptured,GravitySourceValid;
        public NpcIdentity GravitySourceIdentity;
        public float GravitySourceX,GravitySourceY;
        public bool DD2OngoingCaptured,DD2Ongoing;
        public bool BloodMoon,PlayerProtected,SkeletronUp,WallBossUp;
        public bool TownWeatherCaptured,Raining,Storming;
        public float PlayerX,PlayerY,PlayerWidth,PlayerHeight,Wind,WindTarget,WorldSurface;
        // Derived per forecast action by the rolling owner, never an observed
        // world fact or a reason to move the capture origin/tick.
        public bool PlayerTimelineActive;
        public bool PlayerVelocityCaptured,HappyWindyCaptured,HappyWindy;
        public float PlayerVx,PlayerVy;
        public float RockLayer;
        // Original public gravity divides a float altitude by Main's double
        // surface. Preserve that operand without changing other AI premises.
        public double GravityWorldSurface;
        public bool Expert,Enraged,Day,PlayerWet,ClearLine,Multiplayer,Remix,SlimeRain,Eclipse,Graveyard,GoodWorld,Zenith,Anniversary,PlayerDead,PlayerIdleWithNegativeAggro,Corrupt,Crimson,Jungle,Temple,AnyLivingCorrupt,SkyblockLowTiles,MechQueenUp,SnowMoon,PumpkinMoon,DontStarve;
        public int WorldWidth,WorldHeight,InvasionType,PlayerIndex;
        public bool Equals(PredictionEnvironment b)
        {return (ReferenceEquals(TowerConsumers,b.TowerConsumers) || TowerConsumers!=null && TowerConsumers.Equals(b.TowerConsumers)) && WallSource.Equals(b.WallSource) && PlayerGross==b.PlayerGross && WallTimelineCaptured==b.WallTimelineCaptured && WallTimelineGross==b.WallTimelineGross && WallTimelineIndex==b.WallTimelineIndex && WallViewWidth==b.WallViewWidth && PredictionPlayers.Same(WallTimelineArea,b.WallTimelineArea) && (ReferenceEquals(WallPlayers,b.WallPlayers) || WallPlayers!=null && WallPlayers.Equals(b.WallPlayers)) && (ReferenceEquals(WallSources,b.WallSources) || WallSources!=null && WallSources.Equals(b.WallSources)) && GravitySourceCaptured==b.GravitySourceCaptured && GravitySourceValid==b.GravitySourceValid && GravitySourceIdentity.Equals(b.GravitySourceIdentity) && GravitySourceX==b.GravitySourceX && GravitySourceY==b.GravitySourceY && (ReferenceEquals(GravitySources,b.GravitySources) || GravitySources!=null && GravitySources.Equals(b.GravitySources)) && (ReferenceEquals(MageCorpses,b.MageCorpses) || MageCorpses!=null && MageCorpses.Equals(b.MageCorpses)) && TownWeatherCaptured==b.TownWeatherCaptured && Raining==b.Raining && Storming==b.Storming && DD2OngoingCaptured==b.DD2OngoingCaptured && DD2Ongoing==b.DD2Ongoing && (ReferenceEquals(InvasionPlayers,b.InvasionPlayers) || InvasionPlayers!=null && InvasionPlayers.Equals(b.InvasionPlayers)) && PlayerVelocityCaptured==b.PlayerVelocityCaptured && PlayerVx==b.PlayerVx && PlayerVy==b.PlayerVy && HappyWindyCaptured==b.HappyWindyCaptured && HappyWindy==b.HappyWindy && (ReferenceEquals(Players,b.Players) || Players!=null && Players.Equals(b.Players)) && SkeletronUp==b.SkeletronUp && WallBossUp==b.WallBossUp && BloodMoon==b.BloodMoon && PlayerProtected==b.PlayerProtected && PlayerIndex==b.PlayerIndex && MechQueenUp==b.MechQueenUp && RockLayer==b.RockLayer && WorldHeight==b.WorldHeight && PlayerDead==b.PlayerDead && PlayerIdleWithNegativeAggro==b.PlayerIdleWithNegativeAggro && Corrupt==b.Corrupt && Crimson==b.Crimson && Jungle==b.Jungle && Temple==b.Temple && AnyLivingCorrupt==b.AnyLivingCorrupt && SkyblockLowTiles==b.SkyblockLowTiles && PlayerX==b.PlayerX && PlayerY==b.PlayerY && PlayerWidth==b.PlayerWidth && PlayerHeight==b.PlayerHeight && Wind==b.Wind && WindTarget==b.WindTarget && GravityWorldSurface==b.GravityWorldSurface && WorldSurface==b.WorldSurface && Expert==b.Expert && Enraged==b.Enraged && Day==b.Day && PlayerWet==b.PlayerWet && ClearLine==b.ClearLine && Multiplayer==b.Multiplayer && Remix==b.Remix && SlimeRain==b.SlimeRain && WorldWidth==b.WorldWidth && Eclipse==b.Eclipse && Graveyard==b.Graveyard && GoodWorld==b.GoodWorld && Zenith==b.Zenith && Anniversary==b.Anniversary && InvasionType==b.InvasionType && SnowMoon==b.SnowMoon && PumpkinMoon==b.PumpkinMoon && DontStarve==b.DontStarve;}
    }
    // Already observed 629 consumers own their copied scalar state. Tokens
    // are identity only; future consumption never invokes live projectiles.
    public struct PredictionTowerShieldConsumer
    {
        public readonly object Token;
        public readonly int Slot,Identity,Owner,TimeLeft,ExtraUpdates,Penetrate;
        public readonly bool TargetCaptured,OwnerActive,Wet,SettingsKnown;
        public readonly NpcIdentity TargetIdentity;
        public readonly MotionRect Area;
        public readonly float Vx,Vy;
        public PredictionTowerShieldConsumer(object token,int slot,int identity,int owner,bool targetCaptured,NpcIdentity targetIdentity,MotionRect area,float vx,float vy,int timeLeft,int extraUpdates,int penetrate,bool ownerActive,bool wet,bool settingsKnown)
        {Token=token;Slot=slot;Identity=identity;Owner=owner;TargetCaptured=targetCaptured;TargetIdentity=targetIdentity;Area=area;Vx=vx;Vy=vy;TimeLeft=timeLeft;ExtraUpdates=extraUpdates;Penetrate=penetrate;OwnerActive=ownerActive;Wet=wet;SettingsKnown=settingsKnown;}
        public bool Same(PredictionTowerShieldConsumer b)
        {return ReferenceEquals(Token,b.Token) && Slot==b.Slot && Identity==b.Identity && Owner==b.Owner && TargetCaptured==b.TargetCaptured && TargetIdentity.Equals(b.TargetIdentity) && PredictionPlayers.Same(Area,b.Area) && Vx==b.Vx && Vy==b.Vy && TimeLeft==b.TimeLeft && ExtraUpdates==b.ExtraUpdates && Penetrate==b.Penetrate && OwnerActive==b.OwnerActive && Wet==b.Wet && SettingsKnown==b.SettingsKnown;}
    }
    public sealed class PredictionTowerShieldConsumers : IEquatable<PredictionTowerShieldConsumers>
    {
        private readonly PredictionTowerShieldConsumer[] values;
        public readonly float Left,Right,Top,Bottom;
        public int Count {get{return values.Length;}}
        public bool Captured {get{return true;}}
        public PredictionTowerShieldConsumers(PredictionTowerShieldConsumer[] source,int count,float left,float right,float top,float bottom)
        {if(source==null || count<0 || count>source.Length)throw new ArgumentOutOfRangeException();values=new PredictionTowerShieldConsumer[count];Array.Copy(source,values,count);Left=left;Right=right;Top=top;Bottom=bottom;}
        public PredictionTowerShieldConsumer this[int index] {get{return values[index];}}
        public bool Equals(PredictionTowerShieldConsumers b)
        {if(b==null || Count!=b.Count || Left!=b.Left || Right!=b.Right || Top!=b.Top || Bottom!=b.Bottom)return false;for(int i=0;i<Count;i++)if(!values[i].Same(b.values[i]))return false;return true;}
    }
    // WOF search has a gross eligibility gate and must not borrow the ordinary
    // Closest result. Tokens are opaque identity only; no method reads an actor.
    public struct PredictionWallPlayer
    {
        public readonly int Slot,Aggro;
        public readonly object Token,PetToken;
        public readonly MotionRect Area,PetArea;
        public readonly float Vx,Vy;
        public readonly bool Active,Dead,Ghost,Gross,Idle,NoAggro,HasPet;
        public PredictionWallPlayer(int slot,object token,MotionRect area,float vx,float vy,int aggro,bool active,bool dead,bool ghost,bool gross,bool idle,bool noAggro,bool hasPet,object petToken,MotionRect petArea)
        {Slot=slot;Token=token;Area=area;Vx=vx;Vy=vy;Aggro=aggro;Active=active;Dead=dead;Ghost=ghost;Gross=gross;Idle=idle;NoAggro=noAggro;HasPet=hasPet;PetToken=petToken;PetArea=petArea;}
        public bool Same(PredictionWallPlayer b)
        {return Slot==b.Slot && Aggro==b.Aggro && ReferenceEquals(Token,b.Token) && ReferenceEquals(PetToken,b.PetToken) && PredictionPlayers.Same(Area,b.Area) && PredictionPlayers.Same(PetArea,b.PetArea) && Vx==b.Vx && Vy==b.Vy && Active==b.Active && Dead==b.Dead && Ghost==b.Ghost && Gross==b.Gross && Idle==b.Idle && NoAggro==b.NoAggro && HasPet==b.HasPet;}
    }
    public sealed class PredictionWallPlayers : IEquatable<PredictionWallPlayers>
    {
        private readonly PredictionWallPlayer[] values;
        public int Count {get{return values.Length;}}
        public PredictionWallPlayers(PredictionWallPlayer[] source,int count)
        {if(source==null || count<0 || count>source.Length)throw new ArgumentOutOfRangeException();values=new PredictionWallPlayer[count];Array.Copy(source,values,count);}
        public PredictionWallPlayer this[int index] {get{return values[index];}}
        public bool Equals(PredictionWallPlayers b)
        {if(b==null || Count!=b.Count)return false;for(int i=0;i<Count;i++)if(!values[i].Same(b.values[i]))return false;return true;}
    }
    // Previous complete WOF geometry consumed by one genuine player action.
    // The same rolling group emits this prefix with its NPC trajectory.
    public struct PredictionWallSource : IEquatable<PredictionWallSource>
    {
        public readonly bool Captured,Valid;
        public readonly NpcIdentity Identity;
        public readonly MotionRect Area;
        public readonly int Direction;
        public readonly float Vx,Vy;
        public PredictionWallSource(bool captured,bool valid,NpcIdentity identity,MotionRect area,int direction,float vx,float vy)
        {Captured=captured;Valid=valid;Identity=identity;Area=area;Direction=direction;Vx=vx;Vy=vy;}
        public bool Equals(PredictionWallSource b)
        {return Captured==b.Captured && Valid==b.Valid && Identity.Equals(b.Identity) && PredictionPlayers.Same(Area,b.Area) && Direction==b.Direction && Vx==b.Vx && Vy==b.Vy;}
    }
    public sealed class PredictionWallSources : IEquatable<PredictionWallSources>
    {
        private readonly PredictionWallSource[] values;
        private readonly int first,count;
        public int Count {get{return count;}}
        public object PlayerToken {get;}
        public int PlayerIndex {get;}
        public NpcIdentity TargetIdentity {get;}
        public long CaptureTick {get;}
        public PredictionWallSources(PredictionWallSource[] source,int count,object playerToken,int playerIndex,NpcIdentity targetIdentity,long captureTick)
        {if(source==null || count<1 || count>source.Length || captureTick<0)throw new ArgumentOutOfRangeException();values=new PredictionWallSource[count];Array.Copy(source,values,count);this.count=count;PlayerToken=playerToken;PlayerIndex=playerIndex;TargetIdentity=targetIdentity;CaptureTick=captureTick;}
        private PredictionWallSources(PredictionWallSources prior,int offset,int length)
        {values=prior.values;first=prior.first+offset;count=length;PlayerToken=prior.PlayerToken;PlayerIndex=prior.PlayerIndex;TargetIdentity=prior.TargetIdentity;CaptureTick=prior.CaptureTick;}
        public PredictionWallSource this[int index] {get{if((uint)index>=(uint)count)throw new IndexOutOfRangeException();return values[first+index];}}
        internal PredictionWallSources Window(int offset,int length)
        {if(offset<0 || length<1 || offset+length>count)throw new ArgumentOutOfRangeException();return new PredictionWallSources(this,offset,length);}
        public bool Equals(PredictionWallSources b)
        {if(b==null || count!=b.count || !TargetIdentity.Equals(b.TargetIdentity) || CaptureTick!=b.CaptureTick || PlayerIndex!=b.PlayerIndex || !ReferenceEquals(PlayerToken,b.PlayerToken))return false;for(int i=0;i<count;i++)if(!this[i].Equals(b[i]))return false;return true;}
    }
    // A source for the NEXT player action after this NPC point. Arrays are
    // copied once with the same trajectory prefix; no second forecast/cache.
    public struct PredictionGravitySource : IEquatable<PredictionGravitySource>
    {
        public readonly bool Captured,Valid;
        public readonly NpcIdentity Identity;
        public readonly float X,Y;
        public PredictionGravitySource(bool captured,bool valid,NpcIdentity identity,float x,float y)
        {Captured=captured;Valid=valid;Identity=identity;X=x;Y=y;}
        public bool Equals(PredictionGravitySource b){return Captured==b.Captured && Valid==b.Valid && Identity.Equals(b.Identity) && X==b.X && Y==b.Y;}
    }
    public sealed class PredictionGravitySources : IEquatable<PredictionGravitySources>
    {
        private readonly PredictionGravitySource[] values;
        private readonly int first,count;
        public int Count {get{return count;}}
        public object PlayerToken {get;}
        public int PlayerIndex {get;}
        public bool FirstPlayerStepCompleted {get;}
        public NpcIdentity TargetIdentity {get;}
        public long CaptureTick {get;}
        public PredictionGravitySources(PredictionGravitySource[] source,int count,object playerToken,int playerIndex,bool firstPlayerStepCompleted,NpcIdentity targetIdentity,long captureTick)
        {
            if(source==null || count<1 || count>source.Length || captureTick<0)throw new ArgumentOutOfRangeException();
            values=new PredictionGravitySource[count];Array.Copy(source,values,count);this.count=count;
            PlayerToken=playerToken;PlayerIndex=playerIndex;FirstPlayerStepCompleted=firstPlayerStepCompleted;TargetIdentity=targetIdentity;CaptureTick=captureTick;
        }
        private PredictionGravitySources(PredictionGravitySources prior,int offset,int length)
        {values=prior.values;first=prior.first+offset;count=length;PlayerToken=prior.PlayerToken;PlayerIndex=prior.PlayerIndex;FirstPlayerStepCompleted=offset==0 && prior.FirstPlayerStepCompleted;TargetIdentity=prior.TargetIdentity;CaptureTick=prior.CaptureTick;}
        public PredictionGravitySource this[int index] {get{if((uint)index>=(uint)count)throw new IndexOutOfRangeException();return values[first+index];}}
        internal PredictionGravitySources Window(int offset,int length)
        {if(offset<0 || length<1 || offset+length>count)throw new ArgumentOutOfRangeException();return new PredictionGravitySources(this,offset,length);}
        public bool Equals(PredictionGravitySources b)
        {if(b==null || count!=b.count || !TargetIdentity.Equals(b.TargetIdentity) || CaptureTick!=b.CaptureTick || PlayerIndex!=b.PlayerIndex || !ReferenceEquals(PlayerToken,b.PlayerToken) || FirstPlayerStepCompleted!=b.FirstPlayerStepCompleted)return false;for(int i=0;i<count;i++)if(!this[i].Equals(b[i]))return false;return true;}
    }
    // Values copied once for actual DD2 consumers. No collection method
    // dereferences an actor or runs a player, pet, NPC, or targeting action.
    public struct PredictionInvasionPlayer
    {
        public readonly int Slot;
        public readonly object Token,PetToken;
        public readonly MotionRect Area,PetArea;
        public readonly float Vx,Vy,Aggro;
        public readonly uint NoAggroMask;
        public readonly bool Alive,Idle,HasPet;
        public PredictionInvasionPlayer(int slot,object token,MotionRect area,float vx,float vy,float aggro,uint noAggroMask,bool alive,bool idle,object petToken,MotionRect petArea)
        {Slot=slot;Token=token;Area=area;Vx=vx;Vy=vy;Aggro=aggro;NoAggroMask=noAggroMask;Alive=alive;Idle=idle;PetToken=petToken;PetArea=petArea;HasPet=petToken!=null;}
        public bool NoAggro(int type){int bit=type==578?28:type==551?27:type==620?26:type>=552 && type<=577?type-552:-1;return bit>=0 && (NoAggroMask&(1u<<bit))!=0;}
        public bool Same(PredictionInvasionPlayer b)
        {return Slot==b.Slot && ReferenceEquals(Token,b.Token) && ReferenceEquals(PetToken,b.PetToken) && PredictionPlayers.Same(Area,b.Area) && PredictionPlayers.Same(PetArea,b.PetArea) && Vx==b.Vx && Vy==b.Vy && Aggro==b.Aggro && NoAggroMask==b.NoAggroMask && Alive==b.Alive && Idle==b.Idle && HasPet==b.HasPet;}
    }
    public sealed class PredictionInvasionPlayers : IEquatable<PredictionInvasionPlayers>
    {
        private readonly PredictionInvasionPlayer[] values;
        public PredictionInvasionPlayers(PredictionInvasionPlayer[] source,int count)
        {if(source==null || count<0 || count>255 || count>source.Length)throw new ArgumentOutOfRangeException();values=new PredictionInvasionPlayer[count];Array.Copy(source,values,count);}
        public int Count {get{return values.Length;}}
        public PredictionInvasionPlayer this[int index] {get{return values[index];}}
        public bool Equals(PredictionInvasionPlayers b)
        {if(b==null || b.Count!=Count)return false;for(int i=0;i<Count;i++)if(!values[i].Same(b.values[i]))return false;return true;}
    }
    public sealed class PredictionMageCorpses : IEquatable<PredictionMageCorpses>
    {
        private readonly float[] xs,ys;
        public PredictionMageCorpses(float[] sourceX,float[] sourceY,int count)
        {
            if(sourceX==null || sourceY==null || count<0 || count>sourceX.Length || count>sourceY.Length)throw new ArgumentOutOfRangeException();
            xs=new float[count];ys=new float[count];Array.Copy(sourceX,xs,count);Array.Copy(sourceY,ys,count);
            for(int i=0;i<count;i++)if(float.IsNaN(xs[i]) || float.IsInfinity(xs[i]) || float.IsNaN(ys[i]) || float.IsInfinity(ys[i]))throw new ArgumentOutOfRangeException();
        }
        public bool Captured {get{return true;}}
        public int Count {get{return xs.Length;}}
        public float XAt(int index){return xs[index];}
        public float YAt(int index){return ys[index];}
        public bool Equals(PredictionMageCorpses b)
        {if(b==null || b.Count!=Count)return false;for(int i=0;i<Count;i++)if(xs[i]!=b.xs[i] || ys[i]!=b.ys[i])return false;return true;}
    }
    public sealed class PredictionPlayers : IEquatable<PredictionPlayers>
    {
        private readonly MotionRect[] areas;
        private readonly int[] slots;
        private readonly MotionRect[] searchAreas;
        private readonly bool[] searchAlive;
        private readonly bool[] searchDead;
        private readonly bool[] stinkies;
        private readonly int[] stinkyTicks,talkNpcs;
        public PredictionPlayers(MotionRect[] source,int count):this(source,count,null){}
        public PredictionPlayers(MotionRect[] source,int count,int[] playerSlots):this(source,count,playerSlots,null,null,null){}
        public PredictionPlayers(MotionRect[] source,int count,int[] playerSlots,MotionRect[] preciseSearchAreas,bool[] alive,bool[] dead)
            :this(source,count,playerSlots,preciseSearchAreas,alive,dead,null,null,null){}
        public PredictionPlayers(MotionRect[] source,int count,int[] playerSlots,MotionRect[] preciseSearchAreas,bool[] alive,bool[] dead,bool[] stinky,int[] stinkyTimes,int[] talks)
        {
            if(source==null || count<0 || count>255 || count>source.Length || playerSlots!=null && count>playerSlots.Length || (preciseSearchAreas==null)!=(alive==null) || (preciseSearchAreas==null)!=(dead==null) || preciseSearchAreas!=null && (count>preciseSearchAreas.Length || count>alive.Length || count>dead.Length))throw new ArgumentOutOfRangeException();
            areas=new MotionRect[count];Array.Copy(source,areas,count);if(playerSlots!=null){slots=new int[count];Array.Copy(playerSlots,slots,count);}
            // Only actual Euclidean-search consumers request this immutable
            // float/body/validity set. Legacy lifetime rectangles retain their
            // native integer semantics and the compatible constructors.
            if(preciseSearchAreas!=null){searchAreas=new MotionRect[count];searchAlive=new bool[count];searchDead=new bool[count];Array.Copy(preciseSearchAreas,searchAreas,count);Array.Copy(alive,searchAlive,count);Array.Copy(dead,searchDead,count);}
            if((stinky==null)!=(stinkyTimes==null) || (stinky==null)!=(talks==null) || stinky!=null && (count>stinky.Length || count>stinkyTimes.Length || count>talks.Length || preciseSearchAreas==null))throw new ArgumentOutOfRangeException();
            if(stinky!=null){stinkies=new bool[count];stinkyTicks=new int[count];talkNpcs=new int[count];Array.Copy(stinky,stinkies,count);Array.Copy(stinkyTimes,stinkyTicks,count);Array.Copy(talks,talkNpcs,count);}
        }
        public int Count {get{return areas.Length;}}
        public MotionRect this[int index] {get{return areas[index];}}
        // Active-player arrays are compacted. An unnumbered observation must
        // never be mistaken for the rolling owner's numbered player timeline.
        public int SlotAt(int index){return slots==null?-1:slots[index];}
        public bool SearchCaptured {get{return searchAreas!=null;}}
        public bool TownCaptured {get{return stinkies!=null;}}
        public bool StinkyAt(int index){if(!TownCaptured)throw new InvalidOperationException("Town player facts were not captured.");return stinkies[index];}
        public int StinkyTicksAt(int index){if(!TownCaptured)throw new InvalidOperationException("Town player clocks were not captured.");return stinkyTicks[index];}
        public int TalkNpcAt(int index){if(!TownCaptured)throw new InvalidOperationException("Town player talk state was not captured.");return talkNpcs[index];}
        public MotionRect SearchAreaAt(int index){if(searchAreas==null)throw new InvalidOperationException("Euclidean search facts were not captured.");return searchAreas[index];}
        public bool SearchAliveAt(int index){if(searchAlive==null)throw new InvalidOperationException("Euclidean search validity was not captured.");return searchAlive[index];}
        public bool SearchDeadAt(int index){if(searchDead==null)throw new InvalidOperationException("Euclidean search death fact was not captured.");return searchDead[index];}
        public bool Equals(PredictionPlayers b){if(b==null || b.Count!=Count || SearchCaptured!=b.SearchCaptured || TownCaptured!=b.TownCaptured)return false;for(int i=0;i<Count;i++)if(SlotAt(i)!=b.SlotAt(i) || !Same(areas[i],b.areas[i]) || SearchCaptured && (!Same(searchAreas[i],b.searchAreas[i]) || searchAlive[i]!=b.searchAlive[i] || searchDead[i]!=b.searchDead[i]) || TownCaptured && (stinkies[i]!=b.stinkies[i] || stinkyTicks[i]!=b.stinkyTicks[i] || talkNpcs[i]!=b.talkNpcs[i]))return false;return true;}
        public static bool Same(MotionRect a,MotionRect b){return a.X==b.X && a.Y==b.Y && a.Width==b.Width && a.Height==b.Height;}
    }
    public struct PredictionTile
    {
        // Root and background-wall AI use raw active(), including actuated
        // tiles. Collision continues to use Active/Solid (native nactive()).
        public bool Active,RawActive,RawSolid,Solid,SolidTop,Platform,Half,ProperPlatformFrame,SurfacePlatform;
        // Native teleport candidate tests read the lava kind bit even when
        // the amount is zero. Preserve that fact independently of Liquid.
        public bool Lava,ShimmerLiquid;
        public bool CanSit;
        // Raw-active cloud/special-platform protection, including actuated
        // tiles. This is independent of collision solidity and furniture frames.
        public bool FallSafe;
        public short FrameX,FrameY;
        public byte Slope,Liquid;
        public ushort Type,Wall;
        public bool TopSlope {get{return Slope==1 || Slope==2;}}
        public bool SolidNoPlatform {get{return Active && !Platform && (Solid || SolidTop);}}
        public bool SolidBottomSlope {get{return Active && (Solid || SolidTop) && !Half && (!TopSlope || Platform && ProperPlatformFrame);}}
    }
    public interface IPredictionTerrain
    {
        // Implementations copy only queried tile values on the game thread.
        // Unknown/complex terrain terminates a segment; it never becomes air.
        bool Solid(MotionRect area,out bool solid,out PredictionStop stop);
        bool CanHit(MotionRect source,MotionRect target,out bool clear,out PredictionStop stop);
        bool Tile(int x,int y,out PredictionTile tile,out PredictionStop stop);
        bool Move(ref NpcMotionState state,PredictionEnvironment environment,out PredictionStop stop);
        bool Unchanged {get;}
        void Reset();
    }
    public interface IPredictionResizeTerrain
    {
        // A known obstruction is canResize=false; unavailable input is false
        // with a stop reason. Neither condition invents an adjusted rectangle.
        bool Resize(MotionRect box,int width,int height,out MotionRect adjusted,out bool canResize,out PredictionStop stop);
    }
    public interface IPredictionWaterSurfaceTerrain
    {
        // Player-owned surface support after solid contact, before translation.
        // This capability never gives a fish a water constraint or changes AI.
        bool MoveWaterWalkingPlayer(ref NpcMotionState body,PredictionEnvironment environment,bool fallThrough,bool lavaWalk,out PredictionStop stop);
    }
    public interface IPredictionPlayerTerrain
    {
        // Player-owned platform, gravity and equipment semantics. Shared tile
        // acquisition does not make a player an NPC with synthetic aiStyle.
        bool MovePlayer(ref NpcMotionState body,PredictionEnvironment environment,ref PredictionPlayerMotion player,out PredictionStop stop);
    }
    public struct NpcTrajectoryPoint
    {
        public readonly int TickOffset;
        public readonly MotionRect Bounds;
        // Owner-hit-check reads the floating body after netOffset; native
        // Colliding instead receives the independently truncated rectangle.
        private readonly float ownerX,ownerY;
        public MotionRect OwnerBounds {get{return new MotionRect(ownerX,ownerY,Bounds.Width,Bounds.Height);}}
        // Bounds is physical motion. These integer rectangles follow native
        // hit testing after its observed network offset. Only projectile hits
        // use the extra eight-pixel margin on NPC 414; melee uses ReceiveBounds.
        public readonly MotionRect ReceiveBounds,ProjectileReceiveBounds;
        public readonly bool HasProjectileExtension;
        public readonly float Vx,Vy,Phase;
        // Future native melee owner gates consume this point, never live state.
        public readonly bool CanReceive,CanHarm,NewSegment,NoTileCollide,Wet,ReflectsProjectiles,ProjectileReflectionUnknown;
        public NpcTrajectoryPoint(int tick,NpcMotionState state)
        {TickOffset=tick;Bounds=state.Bounds;ownerX=state.X+state.NetOffsetX;ownerY=state.Y+state.NetOffsetY;Vx=state.Vx;Vy=state.Vy;Phase=state.A0;CanReceive=state.CanReceive;CanHarm=state.CanHarm;NewSegment=state.NewSegment;NoTileCollide=state.NoTileCollide;Wet=state.Wet;ReflectsProjectiles=state.ReflectsProjectiles;ProjectileReflectionUnknown=state.ProjectileReflectionUnknown;ReceiveBounds=new MotionRect((int)ownerX,(int)ownerY,state.Width,state.Height);HasProjectileExtension=state.Identity.Type==414;ProjectileReceiveBounds=HasProjectileExtension?new MotionRect(ReceiveBounds.X-8,ReceiveBounds.Y-8,ReceiveBounds.Width+16,ReceiveBounds.Height+16):ReceiveBounds;}
        private NpcTrajectoryPoint(int tick,NpcTrajectoryPoint source)
        {TickOffset=tick;Bounds=source.Bounds;ownerX=source.ownerX;ownerY=source.ownerY;ReceiveBounds=source.ReceiveBounds;ProjectileReceiveBounds=source.ProjectileReceiveBounds;HasProjectileExtension=source.HasProjectileExtension;Vx=source.Vx;Vy=source.Vy;Phase=source.Phase;CanReceive=source.CanReceive;CanHarm=source.CanHarm;NewSegment=source.NewSegment;NoTileCollide=source.NoTileCollide;Wet=source.Wet;ReflectsProjectiles=source.ReflectsProjectiles;ProjectileReflectionUnknown=source.ProjectileReflectionUnknown;}
        public NpcTrajectoryPoint AtOffset(int tick){return new NpcTrajectoryPoint(tick,this);}
        internal NpcTrajectoryPoint(int tick,MotionRect bounds,float ownerX,float ownerY,bool extension,float vx,float vy,float phase,bool canReceive,bool canHarm,bool newSegment,bool noTileCollide=false,bool wet=false,bool reflectsProjectiles=false,bool projectileReflectionUnknown=false)
        {TickOffset=tick;Bounds=bounds;this.ownerX=ownerX;this.ownerY=ownerY;ReceiveBounds=new MotionRect((int)ownerX,(int)ownerY,bounds.Width,bounds.Height);HasProjectileExtension=extension;ProjectileReceiveBounds=extension?new MotionRect(ReceiveBounds.X-8,ReceiveBounds.Y-8,ReceiveBounds.Width+16,ReceiveBounds.Height+16):ReceiveBounds;Vx=vx;Vy=vy;Phase=phase;CanReceive=canReceive;CanHarm=canHarm;NewSegment=newSegment;NoTileCollide=noTileCollide;Wet=wet;ReflectsProjectiles=reflectsProjectiles;ProjectileReflectionUnknown=projectileReflectionUnknown;}
    }
    public sealed class NpcTrajectory
    {
        private readonly NpcTrajectoryPoint[] points;
        // Only the synchronous rolling route uses the compact immutable copy.
        // Copy the exact floating receive body, then apply native truncation;
        // never infer netOffset from rounded positions. This reuses the same
        // two coordinate fields; compact storage does not grow for owner gates.
        // There is no pool or writable array shared with a previous result.
        private struct PackedPoint
        {
            private readonly MotionRect bounds;
            private readonly float ownerX,ownerY,vx,vy,phase;
            private readonly byte flags;
            internal PackedPoint(NpcTrajectoryPoint value)
            {bounds=value.Bounds;ownerX=value.OwnerBounds.X;ownerY=value.OwnerBounds.Y;vx=value.Vx;vy=value.Vy;phase=value.Phase;flags=(byte)((value.HasProjectileExtension?1:0)|(value.CanReceive?2:0)|(value.CanHarm?4:0)|(value.NewSegment?8:0)|(value.NoTileCollide?16:0)|(value.Wet?32:0)|(value.ReflectsProjectiles?64:0)|(value.ProjectileReflectionUnknown?128:0));}
            internal NpcTrajectoryPoint AtOffset(int tick)
            {return new NpcTrajectoryPoint(tick,bounds,ownerX,ownerY,(flags&1)!=0,vx,vy,phase,(flags&2)!=0,(flags&4)!=0,(flags&8)!=0,(flags&16)!=0,(flags&32)!=0,(flags&64)!=0,(flags&128)!=0);}
        }
        private readonly PackedPoint[] compact;
        private int StorageCount {get{return compact!=null?compact.Length:points.Length;}}
        private readonly int first,count;
        private readonly bool isWindow;
        private readonly PredictionStop sourceStop;
        public NpcIdentity Identity {get;}
        public long SampleTick {get;}
        // SampleTick is the first displayed point. CaptureTick retains the
        // original observation so repeated slicing cannot make old work fresh.
        public long CaptureTick {get;}
        public long Version {get;}
        public long RelationVersion {get;}
        public PredictionStrategy Strategy {get;}
        public PredictionQuality Quality {get;}
        public const string SamplePhase="CompletedWorldUpdate";
        public PredictionAssumption Assumptions {get;}
        public PredictionStop Stop {get;}
        public int Count {get{return count;}}
        public PredictionGravitySources GravitySources {get;}
        public PredictionWallSources WallSources {get;}
        public NpcTrajectoryPoint this[int index]
        {get{if((uint)index>=(uint)count)throw new IndexOutOfRangeException();return compact!=null?compact[first+index].AtOffset(index):points[first+index].AtOffset(index);}}
        public NpcTrajectory(NpcIdentity identity,long sampleTick,long version,PredictionAssumption assumptions,PredictionStop stop,NpcTrajectoryPoint[] source,int count,PredictionStrategy strategy=PredictionStrategy.Model,long relationVersion=0,PredictionQuality quality=PredictionQuality.Conditional,PredictionGravitySources gravitySources=null,PredictionWallSources wallSources=null)
        {
            if(source==null)throw new ArgumentNullException(nameof(source));
            if(sampleTick<0 || count<1 || count>source.Length)throw new ArgumentOutOfRangeException();
            for(int i=0;i<count;i++)if(source[i].TickOffset!=i)throw new ArgumentException("Prediction points must cover consecutive updates.",nameof(source));
            Identity=identity;SampleTick=CaptureTick=sampleTick;Version=version;Assumptions=assumptions;Stop=sourceStop=stop;
            Strategy=strategy;RelationVersion=relationVersion;Quality=quality;
            if(strategy==PredictionStrategy.RollingConditional)
            {compact=new PackedPoint[count];for(int i=0;i<count;i++)compact[i]=new PackedPoint(source[i]);}
            else{points=new NpcTrajectoryPoint[count];Array.Copy(source,points,count);}
            this.count=count;GravitySources=gravitySources;WallSources=wallSources;
            if(wallSources!=null && (strategy!=PredictionStrategy.RollingConditional || wallSources.Count!=count || !wallSources.TargetIdentity.Equals(identity) || wallSources.CaptureTick!=sampleTick))throw new ArgumentException("Wall source prefix must match trajectory.",nameof(wallSources));
            if(gravitySources!=null && (strategy!=PredictionStrategy.RollingConditional || gravitySources.Count!=count || !gravitySources.TargetIdentity.Equals(identity) || gravitySources.CaptureTick!=sampleTick))throw new ArgumentException("Gravity source prefix must match trajectory.",nameof(gravitySources));
        }
        private NpcTrajectory(NpcTrajectory prior,long sampleTick,long version,int first,int count,bool republish)
        {
            Identity=prior.Identity;SampleTick=sampleTick;CaptureTick=republish?sampleTick:prior.CaptureTick;Version=version;Assumptions=prior.Assumptions;
            Strategy=prior.Strategy;RelationVersion=prior.RelationVersion;Quality=prior.Quality;
            sourceStop=prior.sourceStop;points=prior.points;compact=prior.compact;this.first=first;this.count=count;isWindow=!republish;
            Stop=first+count==StorageCount?sourceStop:PredictionStop.None;
            GravitySources=prior.GravitySources==null?null:prior.GravitySources.Window(first-prior.first,count);
            WallSources=prior.WallSources==null?null:prior.WallSources.Window(first-prior.first,count);
        }
        private NpcTrajectory(NpcTrajectory prior,NpcIdentity identity)
        {
            Identity=identity;SampleTick=prior.SampleTick;CaptureTick=prior.CaptureTick;Version=prior.Version;Assumptions=prior.Assumptions;
            Strategy=prior.Strategy;RelationVersion=prior.RelationVersion;Quality=prior.Quality;
            GravitySources=prior.GravitySources;WallSources=prior.WallSources;Stop=prior.Stop;sourceStop=prior.sourceStop;points=prior.points;compact=prior.compact;first=prior.first;count=prior.count;isWindow=prior.isWindow;
        }
        // Background decoding owns only a value identity, never the game's
        // object token. The game-thread owner binds the original token after
        // validating its request. This cannot retarget, rebase time or copy the
        // already immutable point array into another large game-thread buffer.
        public NpcTrajectory BindIdentity(NpcIdentity identity)
        {
            var value=new NpcIdentity(identity.Session,null,identity.Slot,identity.Generation,identity.Type,identity.NetId);
            if(Identity.Token!=null || identity.Token==null || !Identity.Equals(value))throw new InvalidOperationException("Only the matching value identity can bind its original token.");
            return new NpcTrajectory(this,identity);
        }
        // Only an already-published immutable array may be shared. The public
        // construction path still copies mutable model work buffers. A newer
        // sampling identity never mutates records retained by another consumer.
        // The synchronous model may rebase after observing identical complete
        // inputs again. This is not an age waiver for an asynchronous result.
        public NpcTrajectory Republish(long sampleTick,long version)
        {
            if(isWindow || Strategy==PredictionStrategy.RollingConditional)throw new InvalidOperationException("A conditional result cannot be republished with a new capture time.");
            if(sampleTick<SampleTick)throw new ArgumentOutOfRangeException(nameof(sampleTick));
            return new NpcTrajectory(this,sampleTick,version,0,count,true);
        }
        public bool TryWindow(long currentTick,int requiredFuture,long version,out NpcTrajectory window)
        {
            window=null;if(currentTick<SampleTick || requiredFuture<0)return false;
            long age=currentTick-CaptureTick;if(age<0 || age>=StorageCount)return false;
            int available=StorageCount-(int)age;long wanted=(long)requiredFuture+1;
            // Unknown/failed work cannot stand in for a shorter valid horizon.
            // Actual natural ending may terminate it without invented padding.
            if(wanted>available && sourceStop!=PredictionStop.Despawn)return false;
            int length=(int)Math.Min(wanted,available);
            window=new NpcTrajectory(this,currentTick,version,(int)age,length,false);return true;
        }
    }
}
