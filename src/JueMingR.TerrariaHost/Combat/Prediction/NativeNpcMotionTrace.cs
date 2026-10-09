using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Worker-only, selected-instance motion classification. A native movement
    // phase explains its own displacement (liquid slowdown, collision axes,
    // slopes, steps, conveyor); velocity at the end of AI cannot explain it.
    // Unexplained relocations remain sticky, including before/after a known
    // phase. This observes original execution and never changes its outcome.
    internal static class NativeNpcMotionTrace
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static NPC selected;
        private static Vector2 cursor;
        private static bool updating,collision,freeMove,discontinuous;
        private static int depth;
        private struct Phase { internal bool Active; internal Vector2 Offset; }
        internal static void Install(Harmony patches)
        {
            foreach(string name in new[]{"Collision_WalkDownSlopes","Collision_MoveWhileDry","Collision_MoveWhileWet","Collision_MoveSlopesAndStairFall","Collision_MoveSnailOnSlopes"})
                patches.Patch(typeof(NPC).GetMethod(name,Flags)??throw new MissingMethodException(name),prefix:Hook(nameof(MoveBefore)),postfix:Hook(nameof(MoveAfter)));
            foreach(string name in new[]{"StepUp","StepDown"})
                patches.Patch(typeof(Collision).GetMethod(name,Flags)??throw new MissingMethodException(name),prefix:Hook(nameof(StepBefore)),postfix:Hook(nameof(StepAfter)));
            patches.Patch(typeof(Collision).GetMethod("StepConveyorBelt",Flags),prefix:Hook(nameof(ConveyorBefore)),postfix:Hook(nameof(ConveyorAfter)));
            patches.Patch(typeof(NPC).GetMethod("UpdateCollision",Flags),prefix:Hook(nameof(CollisionBefore)));
            patches.Patch(typeof(NPC).GetMethod("CheckDialogue",Flags),prefix:Hook(nameof(DialogueBefore)));
            patches.Patch(typeof(NPC).GetMethod("Teleport",Flags),prefix:Hook(nameof(TeleportBefore)));
        }
        private static HarmonyMethod Hook(string name){return new HarmonyMethod(typeof(NativeNpcMotionTrace).GetMethod(name,Flags));}
        internal static void Begin(NPC target){Clear();selected=target;cursor=target.position;}
        internal static void Enter(NPC actor){updating=ReferenceEquals(actor,selected);}
        internal static void Leave(){updating=false;}
        internal static bool Complete()
        {
            if(depth!=0)throw new InvalidDataException("Incomplete native motion phase.");
            return selected!=null && (discontinuous || !Near(cursor,selected.position));
        }
        internal static void Clear(){selected=null;cursor=default(Vector2);updating=collision=freeMove=discontinuous=false;depth=0;}
        private static bool Own(Entity actor){return updating && ReferenceEquals(actor,selected);}
        [StructLayout(LayoutKind.Explicit)]
        private struct CoordinateBits
        {
            [FieldOffset(0)]internal float Value;
            [FieldOffset(0)]internal int Bits;
        }
        private static float CoordinateRoundoff(float a,float b)
        {
            // Native particle code temporarily adds/subtracts netOffset.
            // At large world coordinates one float ULP exceeds .002px;
            // this one-operation rounding is not a physical relocation.
            // Bound only this motion trace, independently on each axis.
            // Explicit Teleport is sticky even below this bound; acceptance
            // of NPC positions/velocity and all AI premises is unchanged.
            var value=new CoordinateBits{Value=Math.Max(Math.Abs(a),Math.Abs(b))};
            int exponent=(value.Bits>>23)&255;
            return exponent<=141 || exponent==255?.002f:new CoordinateBits{Bits=(exponent-23)<<23}.Value;
        }
        private static bool Near(Vector2 a,Vector2 b)
        {return Math.Abs(a.X-b.X)<=CoordinateRoundoff(a.X,b.X) && Math.Abs(a.Y-b.Y)<=CoordinateRoundoff(a.Y,b.Y);}
        private static Phase Start(bool own)
        {
            if(!own)return default(Phase);
            if(depth++==0 && !Near(cursor,selected.position))discontinuous=true;
            return new Phase{Active=true};
        }
        private static void Finish(Phase phase,Vector2 position)
        {if(phase.Active && --depth==0)cursor=position;}
        private static void MoveBefore(NPC __instance,out Phase __state){__state=Start(Own(__instance));}
        private static void MoveAfter(NPC __instance,Phase __state){Finish(__state,__instance.position);}
        private static void ConveyorBefore(Entity __0,out Phase __state){__state=Start(Own(__0));}
        private static void ConveyorAfter(Entity __0,Phase __state){Finish(__state,__0.position);}
        private static void StepBefore(ref Vector2 __0,out Phase __state)
        {
            __state=Start(updating);
            // AI_069 uses a local collision position and assigns it back with
            // an offset. The two other native calls pass position directly.
            // Capture this offset before the ref changes; adding the delta to
            // selected.position afterwards would count direct refs twice.
            if(__state.Active)__state.Offset=selected.position-__0;
        }
        private static void StepAfter(ref Vector2 __0,Phase __state){Finish(__state,__0+__state.Offset);}
        private static void CollisionBefore(NPC __instance){if(Own(__instance))collision=true;}
        private static void DialogueBefore(NPC __instance)
        {
            // Locked UpdateNPC noTileCollide branch sets oldPosition, adds
            // velocity, removes water-perishable buffs, then CheckDialogue.
            // Observe HERE, before FindFrame/network tails can change values.
            if(!Own(__instance) || collision || freeMove || !__instance.noTileCollide)return;
            freeMove=true;
            if(!Near(cursor,__instance.oldPosition) || !Near(__instance.position,__instance.oldPosition+__instance.velocity))discontinuous=true;
            cursor=__instance.position;
        }
        private static void TeleportBefore(NPC __instance,Vector2 __0)
        {if(ReferenceEquals(__instance,selected) && __instance.position!=__0)discontinuous=true;}
    }
}
