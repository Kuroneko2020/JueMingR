using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Tools
{
    // A normal miss exhausts a motion opportunity, not the animal's lifetime.
    // Re-arm on leaving/re-entering, changed motion (including a real stop),
    // or changed net/facing. Compare the last actual swing's motion, since a
    // new window can arise while its final phases/selection are returning.
    // Merely advancing another update/coordinate must not wash a fishing loan.
    internal sealed class CaptureOpportunity
    {
        private struct Attempt
        {
            internal NPC Target;internal int Generation,Type,Net,Direction,MotionX,MotionY,Shape;
            internal float Gravity;internal bool Outside;
        }
        private readonly Attempt[] attempts=new Attempt[Main.maxNPCs];
        private static int Motion(float speed){return speed>0.5f?1:speed<-.5f?-1:0;}
        internal bool Allows(Player p,Item net,NPC target,int shape)
        {
            int slot=target.whoAmI;if(slot<0 || slot>=attempts.Length)return false;
            var a=attempts[slot];
            if(!ReferenceEquals(a.Target,target) || a.Generation!=target.generation || a.Type!=target.type)return true;
            Vector2 motion=target.velocity-p.velocity;
            bool changed=a.MotionX!=Motion(motion.X) || a.MotionY!=Motion(motion.Y);
            return a.Outside || changed || a.Net!=net.type || a.Shape!=shape || a.Direction!=p.direction || a.Gravity!=p.gravDir;
        }
        internal void Outside(NPC target)
        {int slot=target.whoAmI;if(slot<0 || slot>=attempts.Length)return;var a=attempts[slot];if(ReferenceEquals(a.Target,target)){a.Outside=true;attempts[slot]=a;}}
        internal void Finish(Player p,Item net,NPC target,int shape,bool outside,Vector2 attemptedMotion)
        {
            int slot=target.whoAmI;if(slot<0 || slot>=attempts.Length)return;
            attempts[slot]=new Attempt{Target=target,Generation=target.generation,Type=target.type,Net=net.type,Shape=shape,Direction=p.direction,Gravity=p.gravDir,MotionX=Motion(attemptedMotion.X),MotionY=Motion(attemptedMotion.Y),Outside=outside};
        }
        internal void Clear(){Array.Clear(attempts,0,attempts.Length);}
    }
}
