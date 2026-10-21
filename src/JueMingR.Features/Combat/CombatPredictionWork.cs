using System;
using System.Diagnostics;

namespace JueMingR.Features.Combat
{
    // The combat observation owner charges only synchronous enhancement work.
    // Native actions between scopes are excluded; nested consumers share the
    // same update allowance instead of restarting or double-counting it.
    public sealed class CombatPredictionWork
    {
        [ThreadStatic] private static CombatPredictionWork active;
        private readonly long allowance;
        private ulong tick;
        private bool started,exceeded;
        private long spent,entered;
        private long optionalSpent,optionalEntered;
        private long softAllowance=long.MaxValue,optionalAllowance=long.MaxValue;
        private long progressAllowance;
        private bool progressClaimed,progressActive;
        private int depth;
        private int optionalDepth;
        private int geometryRemaining;
        public CombatPredictionWork(double milliseconds)
        {if(milliseconds<=0 || double.IsNaN(milliseconds) || double.IsInfinity(milliseconds))throw new ArgumentOutOfRangeException(nameof(milliseconds));allowance=Math.Max(1,(long)(milliseconds*Stopwatch.Frequency/1000));}
        public bool Exceeded {get{return exceeded;}}
        public double UsedMilliseconds {get{return (spent+(depth==0?0:Stopwatch.GetTimestamp()-entered))*1000.0/Stopwatch.Frequency;}}
        public Scope Enter(ulong update)
        {
            if(depth==0){if(!started || tick!=update){started=true;tick=update;spent=optionalSpent=0;exceeded=progressClaimed=progressActive=false;geometryRemaining=0;softAllowance=optionalAllowance=long.MaxValue;}entered=Stopwatch.GetTimestamp();}
            var previous=active;active=this;depth++;return new Scope(this,previous);
        }
        public static void Check()
        {
            if(!TryContinue())throw new CombatPredictionBudgetExceededException();
        }
        // Call only at a complete-work boundary. A normal exhausted allowance
        // can defer an optional tail without turning a valid prefix into a fault.
        public static bool TryContinue()
        {
            var owner=active;if(owner==null)return true;
            long used=owner.spent+Stopwatch.GetTimestamp()-owner.entered;
            if(owner.exceeded || used>=owner.allowance)
            {owner.exceeded=true;return false;}
            return used<owner.softAllowance && used<(owner.progressActive?owner.progressAllowance:owner.optionalAllowance);
        }
        // A real first-publication demand can finish ONE complete progress unit
        // under the already enclosing optional allowance. Inner Optional scopes
        // still charge every tick but cooperate at that unit's boundary. Limit
        // and the hard owner are never widened; repeated entries cannot regrant.
        public static ProgressScope FirstPublicationProgress()
        {
            var owner=active;
            if(owner==null || owner.progressClaimed || !TryContinue())return default(ProgressScope);
            owner.progressClaimed=owner.progressActive=true;owner.progressAllowance=owner.optionalAllowance;
            return new ProgressScope(owner);
        }
        // Rolling calls this only after a complete world step. The first unit
        // cannot turn a long tail into urgent work, including nested consumers.
        public static bool TryContinueTail()
        {if(active!=null)active.progressActive=false;return TryContinue();}
        // A candidate has a lower cumulative ceiling inside the SAME real
        // update owner. Nesting can only tighten it; disposing restores the
        // urgent allowance without resetting spent time or geometry checks.
        public static LimitScope Limit(double cumulativeMilliseconds)
        {
            if(cumulativeMilliseconds<=0 || double.IsNaN(cumulativeMilliseconds) || double.IsInfinity(cumulativeMilliseconds))throw new ArgumentOutOfRangeException(nameof(cumulativeMilliseconds));
            var owner=active;if(owner==null)return default(LimitScope);
            long previous=owner.softAllowance;
            double ticks=cumulativeMilliseconds*Stopwatch.Frequency/1000;
            long limit=ticks>=long.MaxValue?long.MaxValue:Math.Max(1,(long)ticks);
            owner.softAllowance=Math.Min(previous,limit);
            return new LimitScope(owner,previous);
        }
        // Optional entries borrow the same remainder throughout a real update.
        // Core time does not consume this slice, but every entry still inherits
        // the overall allowance and any tighter enclosing scope.
        public static OptionalScope Optional(double milliseconds)
        {
            if(milliseconds<=0 || double.IsNaN(milliseconds) || double.IsInfinity(milliseconds))throw new ArgumentOutOfRangeException(nameof(milliseconds));
            var owner=active;if(owner==null)return default(OptionalScope);
            long used=owner.spent+Stopwatch.GetTimestamp()-owner.entered;
            if(owner.optionalDepth==0)owner.optionalEntered=used;
            long charged=owner.optionalSpent+Math.Max(0,used-owner.optionalEntered);
            long available=Math.Max(0,(long)(milliseconds*Stopwatch.Frequency/1000)-charged);
            long previous=owner.optionalAllowance;
            owner.optionalAllowance=Math.Min(previous,used+available);
            owner.optionalDepth++;
            return new OptionalScope(owner,previous,used);
        }
        // A lower optional-work threshold leaves the total allowance intact
        // for urgent consumers. Only the actual shared hard limit sets Exceeded.
        public static bool TryContinue(double milliseconds)
        {
            if(milliseconds<=0 || double.IsNaN(milliseconds) || double.IsInfinity(milliseconds))throw new ArgumentOutOfRangeException(nameof(milliseconds));
            if(!TryContinue())return false;
            var owner=active;
            return owner==null || owner.spent+Stopwatch.GetTimestamp()-owner.entered<milliseconds*Stopwatch.Frequency/1000;
        }
        public static void CheckGeometry()
        {
            var owner=active;if(owner==null)return;
            if(owner.exceeded)throw new CombatPredictionBudgetExceededException();
            // Cell acquisition is a finite in-memory operation. Its first
            // request checks the clock, then at most 32 requests separate real
            // checks. This counter belongs to the whole real update, so short
            // scopes/nesting/candidate owners cannot restart that interval.
            // Future-action and other CPU-loop Check calls remain immediate;
            // this cooperative interval is not a hard real-time guarantee.
            if(owner.geometryRemaining==0){owner.geometryRemaining=31;Check();}
            else owner.geometryRemaining--;
        }
        private void Leave(CombatPredictionWork previous)
        {if(--depth==0)spent+=Stopwatch.GetTimestamp()-entered;active=previous;}
        public readonly struct Scope:IDisposable
        {
            private readonly CombatPredictionWork owner,previous;
            internal Scope(CombatPredictionWork owner,CombatPredictionWork previous){this.owner=owner;this.previous=previous;}
            public void Dispose(){owner?.Leave(previous);}
        }
        public readonly struct LimitScope:IDisposable
        {
            private readonly CombatPredictionWork owner;
            private readonly long previous;
            internal LimitScope(CombatPredictionWork owner,long previous){this.owner=owner;this.previous=previous;}
            public void Dispose(){if(owner!=null)owner.softAllowance=previous;}
        }
        public readonly struct OptionalScope:IDisposable
        {
            private readonly CombatPredictionWork owner;
            private readonly long previous,begin;
            internal OptionalScope(CombatPredictionWork owner,long previous,long begin){this.owner=owner;this.previous=previous;this.begin=begin;}
            public void Dispose()
            {
                if(owner==null)return;
                // Nested optional consumers tighten the same allowance. Only
                // their outer owner charges the interval, avoiding double cost.
                if(--owner.optionalDepth==0)owner.optionalSpent+=Math.Max(0,owner.spent+Stopwatch.GetTimestamp()-owner.entered-begin);
                owner.optionalAllowance=previous;
            }
        }
        public readonly struct ProgressScope:IDisposable
        {
            private readonly CombatPredictionWork owner;
            internal ProgressScope(CombatPredictionWork owner){this.owner=owner;}
            public void Dispose(){if(owner!=null)owner.progressActive=false;}
        }
    }
    // A temporary unresolved enhancement is not a physical miss or a fault
    // context. Rolling retains completed points and retires half-step scratch;
    // other enhancement owners may return unresolved. A scoped soft ceiling
    // does not exhaust the whole update. Basic input never checks either limit.
    public sealed class CombatPredictionBudgetExceededException:Exception { }
}
