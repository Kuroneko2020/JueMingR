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
        private int depth;
        private int geometryRemaining;
        public CombatPredictionWork(double milliseconds)
        {if(milliseconds<=0 || double.IsNaN(milliseconds) || double.IsInfinity(milliseconds))throw new ArgumentOutOfRangeException(nameof(milliseconds));allowance=Math.Max(1,(long)(milliseconds*Stopwatch.Frequency/1000));}
        public bool Exceeded {get{return exceeded;}}
        public double UsedMilliseconds {get{return (spent+(depth==0?0:Stopwatch.GetTimestamp()-entered))*1000.0/Stopwatch.Frequency;}}
        public Scope Enter(ulong update)
        {
            if(depth==0){if(!started || tick!=update){started=true;tick=update;spent=0;exceeded=false;geometryRemaining=0;}entered=Stopwatch.GetTimestamp();}
            var previous=active;active=this;depth++;return new Scope(this,previous);
        }
        public static void Check()
        {
            var owner=active;if(owner==null)return;
            if(owner.exceeded || owner.spent+Stopwatch.GetTimestamp()-owner.entered>=owner.allowance)
            {owner.exceeded=true;throw new CombatPredictionBudgetExceededException();}
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
    }
    // A temporary unresolved enhancement is not a physical miss or a fault
    // context. Only outer combat scopes catch this signal and retire scratch;
    // the next real update resets the allowance. Basic input never checks it.
    public sealed class CombatPredictionBudgetExceededException:Exception { }
}
