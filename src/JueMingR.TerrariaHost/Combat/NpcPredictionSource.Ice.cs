using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class NpcPredictionSource
    {
        private static readonly ConditionalWeakTable<NpcTrajectory,PredictionTerrain.IceReadSet> icePaths=new ConditionalWeakTable<NpcTrajectory,PredictionTerrain.IceReadSet>();
        private static readonly ConditionalWeakTable<RollingNpcPrediction.CandidateContinuation,PredictionTerrain.IceReadSet> iceContinuations=new ConditionalWeakTable<RollingNpcPrediction.CandidateContinuation,PredictionTerrain.IceReadSet>();
        private bool iceCaptureWasActive;
        // Proof-only reference, never an alternate trajectory/input publisher.
        // Strict consumer minima may hide the prefix before its extension.
        private NpcTrajectory icePrefix;
        private void PrepareWithIceLease(NpcIdentity identity,long tick,bool beforeNpc,int advanceTo,int plannedHorizon)
        {
            var player=Main.LocalPlayer;
            bool capture=player!=null && player.HeldItem!=null && player.HeldItem.type==496 && Native==null && (Prediction.SegmentedNpcPrediction.Family(identity.Type)==0 || identity.Slot>=0 && identity.Slot<Main.npc.Length && SegmentedWormMotor(Main.npc[identity.Slot]));
            if(!capture){iceCaptureWasActive=false;icePrefix=null;PrepareSample(identity,tick,beforeNpc,advanceTo,plannedHorizon);return;}
            // A same-tick result prepared before selecting Ice Rod has no
            // complete read proof. Recompute once rather than inventing one
            // from a possibly reset terrain cache.
            if(!iceCaptureWasActive)rolling.Clear();iceCaptureWasActive=true;
            var prefix=icePrefix;
            Terrain.BeginIcePathReadCapture();bool success=false;
            try{PrepareSample(identity,tick,beforeNpc,advanceTo,plannedHorizon);success=true;}
            finally
            {
                var reads=Terrain.EndIcePathReadCapture();NpcTrajectory path=null;
                if(success)for(int consumer=0;consumer<4 && path==null;consumer++)path=Cache.Read(consumer);
                if(path!=null && path.CaptureTick==tick && path.Strategy==PredictionStrategy.RollingConditional)
                {
                    PredictionTerrain.IceReadSet prior;
                    if(icePaths.TryGetValue(path,out prior))reads.Include(prior);
                    // A new immutable extension still owns every cell read by
                    // its already published prefix. Include conservatively on
                    // a same-sample rebuild as well; extra cells only revoke a
                    // proof and cannot grant an unsafe Ice Rod write.
                    if(prefix!=null && prefix.CaptureTick==path.CaptureTick && prefix.Identity.Equals(path.Identity) && icePaths.TryGetValue(prefix,out prior))reads.Include(prior);
                    icePaths.Remove(path);icePaths.Add(path,reads);
                    icePrefix=path;
                    var continuation=rolling.Continuation;
                    if(continuation!=null){iceContinuations.Remove(continuation);iceContinuations.Add(continuation,reads);}
                }
                else icePrefix=null;
            }
        }
        private bool IncludeIceContinuation(RollingNpcPrediction.CandidateContinuation continuation)
        {
            if(!iceCaptureWasActive)return true;
            PredictionTerrain.IceReadSet prior;
            return continuation!=null && iceContinuations.TryGetValue(continuation,out prior) && Terrain.IncludeIcePathReads(prior);
        }
        // Exact immutable path keys prevent an old proof from being reused
        // for another target, a rebased window, or a new preparation. Unknown
        // native/segmented dependencies remain unknown; no NoTile heuristic.
        internal static bool IceTrajectoryUnaffected(NpcTrajectory path,IEnumerable<Point> changedCells)
        {PredictionTerrain.IceReadSet reads;return path!=null && icePaths.TryGetValue(path,out reads) && reads.Unaffected(changedCells);}
    }
}
