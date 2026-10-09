using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // This owner borrows only the cursor consumed by vanilla. It never owns
    // player controls, release/channel, ammo, RNG or projectile fields. Nested
    // and repeated receipts restore only coordinates still written by them.
    internal sealed class CombatCursorScope
    {
        private readonly int oldX,oldY,ownX,ownY;
        private static CombatCursorScope owner;
        private readonly CombatCursorScope previous;
        private bool ended;
        private CombatCursorScope(int x,int y){oldX=Main.mouseX;oldY=Main.mouseY;ownX=x;ownY=y;previous=owner;owner=this;Main.mouseX=x;Main.mouseY=y;}
        internal static CombatCursorScope Begin(Vector2 world)
        {
            var screen=Main.ReverseGravitySupport(world-Main.screenPosition);
            if(float.IsNaN(screen.X) || float.IsInfinity(screen.X) || float.IsNaN(screen.Y) || float.IsInfinity(screen.Y) ||
                screen.X<int.MinValue || screen.X>int.MaxValue || screen.Y<int.MinValue || screen.Y>int.MaxValue)return null;
            return new CombatCursorScope((int)screen.X,(int)screen.Y);
        }
        internal void End()
        {
            if(ended)return;ended=true;
            // A late outer receipt cannot revoke a successor even if both
            // happen to write the same coordinates. Retire deferred parents
            // only after their innermost owner returns the cursor.
            while(owner!=null && owner.ended)
            {var retiring=owner;if(Main.mouseX==retiring.ownX)Main.mouseX=retiring.oldX;if(Main.mouseY==retiring.ownY)Main.mouseY=retiring.oldY;owner=retiring.previous;}
        }
    }
}
