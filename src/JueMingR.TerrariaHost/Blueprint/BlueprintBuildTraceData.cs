#if JMR_BLUEPRINT_DIAGNOSTIC
namespace JueMingR.TerrariaHost.Blueprint
{
    // Numeric dictionaries are emitted once, off-thread. Producers retain no
    // Terraria references and never turn an animation/spend into a placement.
    internal enum BuildTraceStage
    {
        Outer, BlueprintUpdate, AutomaticGate, Advance, Discover, Near, Frontier, Forward,
        Prepare, Geometry, ObjectPreflight, FlightCurrent, BuildChoice, BuildRefresh, NativeBaseline, ToolsChoose, ToolsAdmit,
        Selection, Acquire, Refresh, BeforeSync, ResolveBefore, ResolveAfter, ItemBegin,
        ConsumerGate, NativeChoiceGate, Consumer, Choice, Started, Checked, End, Match, Signals, Retry, Wake, FailClosed, Count
    }
    internal enum BuildTraceReason
    {
        None, Passed, AutomaticGateFalse, NoPlayer, InventoryNotReady, PreparedRetained,
        PreparedCleared, NoCandidate, SourceBudgetEmpty, SearchBudgetEmpty, ActiveTarget,
        TargetStale, Unsupported, Unconfirmed, WorldBounds, RetryWaiting, ContainerWaiting,
        MissingMaterial, SourceEligible, SourceFiltered, PlayerReachOrPulley, GeometryRejected,
        Dependencies, Prepared, SupplyPresent, SupplyMissing, SourceMissing, SourceExhausted,
        SourceNotCurrent, SourceOffset, SourceBoundsEnd, SourceEnd, SourceStep, Candidate,
        NearReset, ResetVersion, ResetWake, ResetPlayer, ResetSupply, ResetBounds, NearStep, FrontierStep, ForwardStep, BudgetGranted, FrontierEnd, ForwardVisit, ForwardZeroBudget, SignalPosition, SignalDirection,
        SignalReach, SignalBody, SignalEligibility, SignalItemReach, RetryRequested, RetryDue,
        WakeBlocked, WakeNotExhausted, WakeCandidate, WakeDemand, WakeReading, WakeProgress, WakeMaterialNotCurrent, WakeRangeNotCurrent, WakeNoPermission,
        WakePerformed, RetryConsumed, WaitRegistered, DynamicWait,
        NoPrepared, ChooseGate, ToolsGate, InputGate, InterfaceGate, MouseGate, ToolsOccupied,
        OtherTool, BuildChosen, AcquireRejected, OwnershipDenied, Acquired, Ready, Pending,
        Yield, Invalid, ManualInput, SelectionWait, NativeTimerWait, DirectionPhase,
        TargetResolution, TargetConsumer, Entered, NativeRejected, NativeChosen, NativeChecked,
        NoConsumption, ConsumptionObserved, UnknownOutcome, Ended, MatchPublished,
        ReadUnavailable, Occupied, SupportMissing, SourceOrReachGate, LiquidRejected, ObjectMetadata, ObjectCanPlace,
        ObjectChoice, ObjectPostConditions, EntityBlocked, ContainerCapacity, BlueprintFailed,
        Count
    }
    internal struct BuildTraceOuter
    {
        internal long Frame, UpdateTick, InputState, InputMapping, UiState;
        internal uint GameUpdateCount, WorldAtInputBegin;
        internal int WorldId, NetMode, PlayerIndex, Selected, PlayerState, TileX, TileY, Direction, Animation, ItemTime, Reuse, Chest, TalkNpc;
        internal float X, Y, VelocityX, VelocityY;
        internal bool WorldPresent;
    }
    internal struct BuildTraceBlueprint
    {
        internal long Session, State, SceneRevision, CatalogRevision, InventoryRevision, EligibilityRevision, SupplyRevision;
        internal ulong MaterialTick, ProjectionMatchTick;
        internal int SceneCount, CaptureStatus, ProjectionMode, SourceCount, SourceBudget, SearchBudget;
        internal long MaterialState, SceneRestoreState;
    }
    internal struct BuildTraceSource
    {
        internal long Instance, Version, Wake, NearVersion, NearWake, NearPlayer, NearSupply;
        internal int Range, NearCursor, ForwardCursor, FrontierCursor, Left, Top, Width, Height, Flags, RetryOrdinal;
        internal int ResetBefore, ResetAfter, ResetReasons;
        internal ulong RetryAt, MaterialTick;
    }
    internal struct BuildTraceTarget
    {
        internal long Session, Instance, RangeVersion, SupportStamp, Catalog, Flight, Token, NativeGeneration, DetailId;
        internal int Range, Ordinal, OriginX, OriginY, X, Y, Left, Top, Width, Height;
        internal int Kind, TileType, WallType, ItemType, Style, Alternate, ExpectedDirection;
        internal int SourceSlot, SourceType, StackBefore, StackAfter, TileBoost, ActualDirection;
        internal int ActualX, ActualY, Animation, ItemTime, Reuse, Flags, Site, Reason;
        internal int RangeX,RangeY,BlockRange,Pulley,PulleyDirection,ChoiceType,ChoiceStyle,ChoiceAlternate,ChoiceRandom,ChoiceX,ChoiceY,CanPlaceResult;
        internal long At,ChoiceAt,CanPlaceAt;
    }
    internal struct BuildTraceEvent
    {
        internal long Sequence, At, Flight, Token, Generation, A, B, C, D, TargetDetailId;
        internal int Stage, Reason, Target;
    }
    internal struct BuildTraceMaterial
    {
        internal int ItemType, MatchingSlots, EligibleSlots, CoveredSlots, PrepareVisited, PrepareEligible, PrepareFiltered;
        internal long Stack, EligibleStack, SupplyRevision, RequestedAt, SampleAt;
        internal ulong SampleTick;
        internal bool Sampled, Supplied, SupplyObserved;
    }
    internal struct BuildTraceSecond
    {
        internal long OuterCalls, MovingUpdates, StationaryUpdates, AutoEnabledUpdates, SceneUpdates;
        internal long GateEvaluated, GatePassed;
        internal BuildTraceOuter Outer;
        internal BuildTraceBlueprint Blueprint;
        internal bool BlueprintObserved;
        internal int SourceCount;
    }
}
#endif
