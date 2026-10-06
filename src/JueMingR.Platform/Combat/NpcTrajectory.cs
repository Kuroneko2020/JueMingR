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
        // Exact direct-position dependency, distinct from Health.RealLife.
        // Models compare the opaque full owner identity without dereferencing.
        public NpcIdentity PositionOwner;
        public int PositionRelation;
        public float PositionParameter;
        // Only the predicted attachment-family body changes this value. Identity
        // remains the real starting instance; a real Transform retires it.
        public int MotionType;
        public int EffectiveType {get{return MotionType==0?Identity.Type:MotionType;}}
        public NpcHealthState Health;
        public bool Friendly,Town,CollisionPart;
        public int HomeTileY;
        // AI_003's spawn/reveal action can precede its common motor.
        public int Alpha;
        // The numbered player supplies environmental/direct-player predicates.
        // Tracking geometry may instead belong to a guardian or encoded NPC.
        public MotionRect PlayerArea,TrackingArea;
        public int PlayerIndex,TrackingKind;
        // GetTargetData(true), distinct from TargetClosest's candidate and
        // the numbered-player platform predicate. Invalid data has center0.
        public int LiquidTargetKind,LiquidTargetPlayer,WetCount;
        public float LiquidTargetY,LiquidTargetVy;
        public float TrackingVx,TrackingVy;
        public bool TargetCaptured,HasPlayer,PlayerDead,PlayerWet,PlayerIdle,PlayerGraveyard;
        public MotionRect ClosestPlayerArea;
        public int ClosestPlayerIndex;
        public bool HasClosestPlayer,ClosestPlayerDead,ClosestPlayerWet,ClosestPlayerIdle,ClosestPlayerNoAggro,ClosestPlayerGraveyard;
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
        public int UnmodeledDamageTicks;
        public int Width,Height,Style,Direction,DirectionY,SpriteDirection,Target,ParentSlot,ChildSlot,TimeLeft,ConfusedTicks,Life,LifeMax,BuffFingerprint,BuffExpires;
        public bool Active,NoGravity,NoTileCollide,Wet,Honey,Lava,Shimmer,CollideX,CollideY,CanReceive,CanHarm,NewSegment,JustHit,StairFall,SpawnedFromStatue,Boss,InactivityImmune,TargetNoAggro,CritterTurns,NoContactDamage;
        public MotionRect Bounds {get{return new MotionRect(X,Y,Width,Height);}}
        // Compare only the bounded pure-value sample already acquired for this
        // forecast; this is not a scan or hash of the live world.
        public bool SameSample(NpcMotionState b)
        {return Identity.Equals(b.Identity) && ChildIdentity.Equals(b.ChildIdentity) && PositionOwner.Equals(b.PositionOwner) && PositionRelation==b.PositionRelation && PositionParameter==b.PositionParameter && MotionType==b.MotionType && Health.Equals(b.Health) && Friendly==b.Friendly && Town==b.Town && CollisionPart==b.CollisionPart && HomeTileY==b.HomeTileY && Alpha==b.Alpha && PredictionPlayers.Same(PlayerArea,b.PlayerArea) && PredictionPlayers.Same(TrackingArea,b.TrackingArea) && PlayerIndex==b.PlayerIndex && TrackingKind==b.TrackingKind && LiquidTargetKind==b.LiquidTargetKind && LiquidTargetPlayer==b.LiquidTargetPlayer && WetCount==b.WetCount && LiquidTargetY==b.LiquidTargetY && LiquidTargetVy==b.LiquidTargetVy && TrackingVx==b.TrackingVx && TrackingVy==b.TrackingVy && TargetCaptured==b.TargetCaptured && HasPlayer==b.HasPlayer && PlayerDead==b.PlayerDead && PlayerWet==b.PlayerWet && PlayerIdle==b.PlayerIdle && PlayerGraveyard==b.PlayerGraveyard && PredictionPlayers.Same(ClosestPlayerArea,b.ClosestPlayerArea) && ClosestPlayerIndex==b.ClosestPlayerIndex && HasClosestPlayer==b.HasClosestPlayer && ClosestPlayerDead==b.ClosestPlayerDead && ClosestPlayerWet==b.ClosestPlayerWet && ClosestPlayerIdle==b.ClosestPlayerIdle && ClosestPlayerNoAggro==b.ClosestPlayerNoAggro && ClosestPlayerGraveyard==b.ClosestPlayerGraveyard && TargetChoiceUnknown==b.TargetChoiceUnknown && TargetChoiceUnavailable==b.TargetChoiceUnavailable && PlayerHeadCollision==b.PlayerHeadCollision && PlayerDryHeadCollision==b.PlayerDryHeadCollision && NetOffsetX==b.NetOffsetX && NetOffsetY==b.NetOffsetY && SmoothingRange==b.SmoothingRange && ResetNetOffset==b.ResetNetOffset && X==b.X && Y==b.Y && Vx==b.Vx && Vy==b.Vy && OldX==b.OldX && OldY==b.OldY && OldVx==b.OldVx && OldVy==b.OldVy && Scale==b.Scale && Gravity==b.Gravity && MaxFall==b.MaxFall && WaterSpeed==b.WaterSpeed && HoneySpeed==b.HoneySpeed && LavaSpeed==b.LavaSpeed && ShimmerSpeed==b.ShimmerSpeed && A0==b.A0 && A1==b.A1 && A2==b.A2 && A3==b.A3 && L0==b.L0 && L1==b.L1 && L2==b.L2 && L3==b.L3 && ObservedAccelerationX==b.ObservedAccelerationX && ObservedAccelerationY==b.ObservedAccelerationY && ObservedTurn==b.ObservedTurn && TrendBaseVx==b.TrendBaseVx && TrendBaseVy==b.TrendBaseVy && UnmodeledDamageTicks==b.UnmodeledDamageTicks && Width==b.Width && Height==b.Height && Style==b.Style && Direction==b.Direction && DirectionY==b.DirectionY && SpriteDirection==b.SpriteDirection && Target==b.Target && ParentSlot==b.ParentSlot && ChildSlot==b.ChildSlot && TimeLeft==b.TimeLeft && ConfusedTicks==b.ConfusedTicks && Life==b.Life && LifeMax==b.LifeMax && BuffFingerprint==b.BuffFingerprint && BuffExpires==b.BuffExpires && Active==b.Active && NoGravity==b.NoGravity && NoTileCollide==b.NoTileCollide && Wet==b.Wet && Honey==b.Honey && Lava==b.Lava && Shimmer==b.Shimmer && CollideX==b.CollideX && CollideY==b.CollideY && CanReceive==b.CanReceive && CanHarm==b.CanHarm && NewSegment==b.NewSegment && JustHit==b.JustHit && StairFall==b.StairFall && SpawnedFromStatue==b.SpawnedFromStatue && Boss==b.Boss && InactivityImmune==b.InactivityImmune && TargetNoAggro==b.TargetNoAggro && CritterTurns==b.CritterTurns && NoContactDamage==b.NoContactDamage;}

    }
    public struct NpcHealthState : IEquatable<NpcHealthState>
    {
        public NpcBuffLayout Buffs;
        public int Poison,Fire,Cursed,Venom,Frost,Fire3,Frost2,Shadow,Slimed,Oil,Accelerated,WetBuff,ShimmerTicks,Regen,RegenCount,RealLife,Immune255,Defense;
        public float ShimmerTransparency,DamageMultiplier;
        public bool DontTakeDamage,Immortal,LavaImmune,FireImmune,ShimmerImmune,ShimmerAction;
        public bool Equals(NpcHealthState b)
        {return Buffs.Equals(b.Buffs) && Poison==b.Poison && Fire==b.Fire && Cursed==b.Cursed && Venom==b.Venom && Frost==b.Frost && Fire3==b.Fire3 && Frost2==b.Frost2 && Shadow==b.Shadow && Slimed==b.Slimed && Oil==b.Oil && Accelerated==b.Accelerated && WetBuff==b.WetBuff && ShimmerTicks==b.ShimmerTicks && Regen==b.Regen && RegenCount==b.RegenCount && RealLife==b.RealLife && Immune255==b.Immune255 && Defense==b.Defense && ShimmerTransparency==b.ShimmerTransparency && DamageMultiplier==b.DamageMultiplier && DontTakeDamage==b.DontTakeDamage && Immortal==b.Immortal && LavaImmune==b.LavaImmune && FireImmune==b.FireImmune && ShimmerImmune==b.ShimmerImmune && ShimmerAction==b.ShimmerAction;}
    }
    public struct PredictionEnvironment : IEquatable<PredictionEnvironment>
    {
        public PredictionPlayers Players;
        public bool BloodMoon,PlayerProtected;
        public float PlayerX,PlayerY,PlayerWidth,PlayerHeight,Wind,WorldSurface;
        public float RockLayer;
        // Original public gravity divides a float altitude by Main's double
        // surface. Preserve that operand without changing other AI premises.
        public double GravityWorldSurface;
        public bool Expert,Enraged,Day,PlayerWet,ClearLine,Multiplayer,Remix,SlimeRain,Eclipse,Graveyard,GoodWorld,PlayerDead,PlayerIdleWithNegativeAggro,Corrupt,Crimson,AnyLivingCorrupt,SkyblockLowTiles,MechQueenUp,SnowMoon,PumpkinMoon,DontStarve;
        public int WorldWidth,WorldHeight,InvasionType,PlayerIndex;
        public bool Equals(PredictionEnvironment b)
        {return (ReferenceEquals(Players,b.Players) || Players!=null && Players.Equals(b.Players)) && BloodMoon==b.BloodMoon && PlayerProtected==b.PlayerProtected && PlayerIndex==b.PlayerIndex && MechQueenUp==b.MechQueenUp && RockLayer==b.RockLayer && WorldHeight==b.WorldHeight && PlayerDead==b.PlayerDead && PlayerIdleWithNegativeAggro==b.PlayerIdleWithNegativeAggro && Corrupt==b.Corrupt && Crimson==b.Crimson && AnyLivingCorrupt==b.AnyLivingCorrupt && SkyblockLowTiles==b.SkyblockLowTiles && PlayerX==b.PlayerX && PlayerY==b.PlayerY && PlayerWidth==b.PlayerWidth && PlayerHeight==b.PlayerHeight && Wind==b.Wind && GravityWorldSurface==b.GravityWorldSurface && WorldSurface==b.WorldSurface && Expert==b.Expert && Enraged==b.Enraged && Day==b.Day && PlayerWet==b.PlayerWet && ClearLine==b.ClearLine && Multiplayer==b.Multiplayer && Remix==b.Remix && SlimeRain==b.SlimeRain && WorldWidth==b.WorldWidth && Eclipse==b.Eclipse && Graveyard==b.Graveyard && GoodWorld==b.GoodWorld && InvasionType==b.InvasionType && SnowMoon==b.SnowMoon && PumpkinMoon==b.PumpkinMoon && DontStarve==b.DontStarve;}
    }
    public sealed class PredictionPlayers : IEquatable<PredictionPlayers>
    {
        private readonly MotionRect[] areas;
        public PredictionPlayers(MotionRect[] source,int count){if(source==null || count<0 || count>255 || count>source.Length)throw new ArgumentOutOfRangeException();areas=new MotionRect[count];Array.Copy(source,areas,count);}
        public int Count {get{return areas.Length;}}
        public MotionRect this[int index] {get{return areas[index];}}
        public bool Equals(PredictionPlayers b){if(b==null || b.Count!=Count)return false;for(int i=0;i<Count;i++)if(!Same(areas[i],b.areas[i]))return false;return true;}
        public static bool Same(MotionRect a,MotionRect b){return a.X==b.X && a.Y==b.Y && a.Width==b.Width && a.Height==b.Height;}
    }
    public struct PredictionTile
    {
        // Root and background-wall AI use raw active(), including actuated
        // tiles. Collision continues to use Active/Solid (native nactive()).
        public bool Active,RawActive,RawSolid,Solid,SolidTop,Platform,Half,ProperPlatformFrame,SurfacePlatform;
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
        // Bounds is physical motion. These integer rectangles follow native
        // hit testing after its observed network offset. Only projectile hits
        // use the extra eight-pixel margin on NPC 414; melee uses ReceiveBounds.
        public readonly MotionRect ReceiveBounds,ProjectileReceiveBounds;
        public readonly bool HasProjectileExtension;
        public readonly float Vx,Vy,Phase;
        public readonly bool CanReceive,CanHarm,NewSegment;
        public NpcTrajectoryPoint(int tick,NpcMotionState state)
        {TickOffset=tick;Bounds=state.Bounds;Vx=state.Vx;Vy=state.Vy;Phase=state.A0;CanReceive=state.CanReceive;CanHarm=state.CanHarm;NewSegment=state.NewSegment;ReceiveBounds=new MotionRect((int)(state.X+state.NetOffsetX),(int)(state.Y+state.NetOffsetY),state.Width,state.Height);HasProjectileExtension=state.Identity.Type==414;ProjectileReceiveBounds=HasProjectileExtension?new MotionRect(ReceiveBounds.X-8,ReceiveBounds.Y-8,ReceiveBounds.Width+16,ReceiveBounds.Height+16):ReceiveBounds;}
        private NpcTrajectoryPoint(int tick,NpcTrajectoryPoint source)
        {TickOffset=tick;Bounds=source.Bounds;ReceiveBounds=source.ReceiveBounds;ProjectileReceiveBounds=source.ProjectileReceiveBounds;HasProjectileExtension=source.HasProjectileExtension;Vx=source.Vx;Vy=source.Vy;Phase=source.Phase;CanReceive=source.CanReceive;CanHarm=source.CanHarm;NewSegment=source.NewSegment;}
        public NpcTrajectoryPoint AtOffset(int tick){return new NpcTrajectoryPoint(tick,this);}
        internal NpcTrajectoryPoint(int tick,MotionRect bounds,MotionRect receive,bool extension,float vx,float vy,float phase,bool canReceive,bool canHarm,bool newSegment)
        {TickOffset=tick;Bounds=bounds;ReceiveBounds=receive;HasProjectileExtension=extension;ProjectileReceiveBounds=extension?new MotionRect(receive.X-8,receive.Y-8,receive.Width+16,receive.Height+16):receive;Vx=vx;Vy=vy;Phase=phase;CanReceive=canReceive;CanHarm=canHarm;NewSegment=newSegment;}
    }
    public sealed class NpcTrajectory
    {
        private readonly NpcTrajectoryPoint[] points;
        // Only the synchronous rolling route uses the compact immutable copy.
        // Receive coordinates are copied exactly (never inferred by subtracting
        // rounded positions); widths and the type414 extension are redundant.
        // There is no pool or writable array shared with a previous result.
        private struct PackedPoint
        {
            private readonly MotionRect bounds;
            private readonly float receiveX,receiveY,vx,vy,phase;
            private readonly byte flags;
            internal PackedPoint(NpcTrajectoryPoint value)
            {bounds=value.Bounds;receiveX=value.ReceiveBounds.X;receiveY=value.ReceiveBounds.Y;vx=value.Vx;vy=value.Vy;phase=value.Phase;flags=(byte)((value.HasProjectileExtension?1:0)|(value.CanReceive?2:0)|(value.CanHarm?4:0)|(value.NewSegment?8:0));}
            internal NpcTrajectoryPoint AtOffset(int tick)
            {return new NpcTrajectoryPoint(tick,bounds,new MotionRect(receiveX,receiveY,bounds.Width,bounds.Height),(flags&1)!=0,vx,vy,phase,(flags&2)!=0,(flags&4)!=0,(flags&8)!=0);}
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
        public NpcTrajectoryPoint this[int index]
        {get{if((uint)index>=(uint)count)throw new IndexOutOfRangeException();return compact!=null?compact[first+index].AtOffset(index):points[first+index].AtOffset(index);}}
        public NpcTrajectory(NpcIdentity identity,long sampleTick,long version,PredictionAssumption assumptions,PredictionStop stop,NpcTrajectoryPoint[] source,int count,PredictionStrategy strategy=PredictionStrategy.Model,long relationVersion=0,PredictionQuality quality=PredictionQuality.Conditional)
        {
            if(source==null)throw new ArgumentNullException(nameof(source));
            if(sampleTick<0 || count<1 || count>source.Length)throw new ArgumentOutOfRangeException();
            for(int i=0;i<count;i++)if(source[i].TickOffset!=i)throw new ArgumentException("Prediction points must cover consecutive updates.",nameof(source));
            Identity=identity;SampleTick=CaptureTick=sampleTick;Version=version;Assumptions=assumptions;Stop=sourceStop=stop;
            Strategy=strategy;RelationVersion=relationVersion;Quality=quality;
            if(strategy==PredictionStrategy.RollingConditional)
            {compact=new PackedPoint[count];for(int i=0;i<count;i++)compact[i]=new PackedPoint(source[i]);}
            else{points=new NpcTrajectoryPoint[count];Array.Copy(source,points,count);}
            this.count=count;
        }
        private NpcTrajectory(NpcTrajectory prior,long sampleTick,long version,int first,int count,bool republish)
        {
            Identity=prior.Identity;SampleTick=sampleTick;CaptureTick=republish?sampleTick:prior.CaptureTick;Version=version;Assumptions=prior.Assumptions;
            Strategy=prior.Strategy;RelationVersion=prior.RelationVersion;Quality=prior.Quality;
            sourceStop=prior.sourceStop;points=prior.points;compact=prior.compact;this.first=first;this.count=count;isWindow=!republish;
            Stop=first+count==StorageCount?sourceStop:PredictionStop.None;
        }
        private NpcTrajectory(NpcTrajectory prior,NpcIdentity identity)
        {
            Identity=identity;SampleTick=prior.SampleTick;CaptureTick=prior.CaptureTick;Version=prior.Version;Assumptions=prior.Assumptions;
            Strategy=prior.Strategy;RelationVersion=prior.RelationVersion;Quality=prior.Quality;
            Stop=prior.Stop;sourceStop=prior.sourceStop;points=prior.points;compact=prior.compact;first=prior.first;count=prior.count;isWindow=prior.isWindow;
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
