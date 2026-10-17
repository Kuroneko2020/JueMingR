using System;
using System.Runtime.ExceptionServices;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackAim
    {
        private readonly FaultGate executionFaults=new FaultGate();
        private FaultContext FaultContextFor(NpcIdentity target)
        {
            var player=combat.Player;var item=player?.HeldItem;
            return new FaultContext{Player=player,Weapon=item,Slot=player?.selectedItem??-1,Type=item?.type??0,Prefix=item?.prefix??0,Target=target,Session=observation.Session,Selection=combat.Tools.SelectionIntent,Aim=observation.Options.Aim,Path=observation.Options.Path};
        }
        internal void RetryFaults()
        {
            executionFaults.Clear();preview.Faults.Clear();
            for(int i=0;i<candidatePlans.Length;i++)candidatePlans[i]?.Faults.Clear();
        }
        private static void PropagateFatal(Exception error)
        {
            // Reflection may wrap a fatal native/model exception. It must not
            // become a recoverable feature fault merely because of that seam.
            for(var cause=error;cause!=null;cause=cause.InnerException)
                if(cause is OutOfMemoryException || cause is AccessViolationException || cause is CombatPredictionBudgetExceededException)ExceptionDispatchInfo.Capture(cause).Throw();
        }
        private struct FaultContext
        {
            internal Player Player;internal Item Weapon;internal int Slot,Type,Prefix;internal NpcIdentity Target;internal long Session,Selection;internal bool Aim,Path;
            internal bool Same(FaultContext other)
            {return ReferenceEquals(Player,other.Player) && ReferenceEquals(Weapon,other.Weapon) && Slot==other.Slot && Type==other.Type && Prefix==other.Prefix && Target.Equals(other.Target) && Session==other.Session && Selection==other.Selection && Aim==other.Aim && Path==other.Path;}
        }
        private sealed class FaultGate
        {
            private FaultContext[] contexts;private int count,cursor;
            internal bool Blocks(FaultContext context)
            {for(int i=0;i<count;i++)if(contexts[i].Same(context))return true;return false;}
            internal void Record(FaultContext context)
            {
                if(Blocks(context))return;
                if(contexts==null)contexts=new FaultContext[4];
                contexts[cursor]=context;cursor=(cursor+1)%contexts.Length;count=Math.Min(contexts.Length,count+1);
            }
            internal void Clear(){if(contexts!=null)Array.Clear(contexts,0,contexts.Length);count=cursor=0;}
        }
    }
}
