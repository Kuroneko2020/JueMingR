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
        private long softAllowance=long.MaxValue;
        private int depth;
        private int geometryRemaining;
        public CombatPredictionWork(double milliseconds)
        {if(milliseconds<=0 || double.IsNaN(milliseconds) || double.IsInfinity(milliseconds))throw new ArgumentOutOfRangeException(nameof(milliseconds));allowance=Math.Max(1,(long)(milliseconds*Stopwatch.Frequency/1000));}
        public bool Exceeded {get{return exceeded;}}
        public double UsedMilliseconds {get{return (spent+(depth==0?0:Stopwatch.GetTimestamp()-entered))*1000.0/Stopwatch.Frequency;}}
        public Scope Enter(ulong update)
        {
            if(depth==0){if(!started || tick!=update){started=true;tick=update;spent=0;exceeded=false;geometryRemaining=0;softAllowance=long.MaxValue;}entered=Stopwatch.GetTimestamp();}
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
            return used<owner.softAllowance;
        }
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
    }
    // A temporary unresolved enhancement is not a physical miss or a fault
    // context. Rolling retains completed points and retires half-step scratch;
    // other enhancement owners may return unresolved. A scoped soft ceiling
    // does not exhaust the whole update. Basic input never checks either limit.
    public sealed class CombatPredictionBudgetExceededException:Exception { }
}
