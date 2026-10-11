using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // This owner borrows the cursor consumed by vanilla, and only for a
    // tile-target consumer its paired native tile coordinates. It never owns
    // player controls, release/channel, ammo, RNG or projectile fields. Nested
    // and repeated receipts restore only coordinates still written by them.
    internal sealed class CombatCursorScope
    {
        private readonly int oldX,oldY,ownX,ownY;
        private readonly int oldTileX,oldTileY,ownTileX,ownTileY;
        private readonly bool tileTarget;
        private static CombatCursorScope owner;
        private readonly CombatCursorScope previous;
        private bool ended;
        private CombatCursorScope(int x,int y,bool tileTarget)
        {
            oldX=Main.mouseX;oldY=Main.mouseY;ownX=x;ownY=y;previous=owner;owner=this;Main.mouseX=x;Main.mouseY=y;
            this.tileTarget=tileTarget;
            if(!tileTarget)return;
            // Player computed these before ItemCheck. Borrow both values at
            // the actual Shoot consumer using the same integer cursor; do not
            // leave a physical-mouse AI point paired with an assisted velocity.
            oldTileX=Player.tileTargetX;oldTileY=Player.tileTargetY;
            ownTileX=Math.Max(5,Math.Min(Main.maxTilesX-5,(int)(((float)x+Main.screenPosition.X)/16f)));
            float worldY=Main.LocalPlayer.gravDir==-1f?Main.screenPosition.Y+Main.screenHeight-y:y+Main.screenPosition.Y;
            ownTileY=Math.Max(5,Math.Min(Main.maxTilesY-5,(int)(worldY/16f)));
            Player.tileTargetX=ownTileX;Player.tileTargetY=ownTileY;
        }
        internal static CombatCursorScope Begin(Vector2 world,bool reverseGravity=true)
        {return BeginCore(world,reverseGravity,false);}
        internal static CombatCursorScope BeginTileTarget(Vector2 world,bool reverseGravity=true)
        {return BeginCore(world,reverseGravity,true);}
        private static CombatCursorScope BeginCore(Vector2 world,bool reverseGravity,bool tileTarget)
        {
            var screen=world-Main.screenPosition;if(reverseGravity)screen=Main.ReverseGravitySupport(screen);
            if(float.IsNaN(screen.X) || float.IsInfinity(screen.X) || float.IsNaN(screen.Y) || float.IsInfinity(screen.Y) ||
                screen.X<int.MinValue || screen.X>int.MaxValue || screen.Y<int.MinValue || screen.Y>int.MaxValue)return null;
            return new CombatCursorScope((int)screen.X,(int)screen.Y,tileTarget);
        }
        internal void End()
        {
            if(ended)return;ended=true;
            // A late outer receipt cannot revoke a successor even if both
            // happen to write the same coordinates. Retire deferred parents
            // only after their innermost owner returns the cursor.
            while(owner!=null && owner.ended)
            {
                var retiring=owner;if(Main.mouseX==retiring.ownX)Main.mouseX=retiring.oldX;if(Main.mouseY==retiring.ownY)Main.mouseY=retiring.oldY;
                if(retiring.tileTarget){if(Player.tileTargetX==retiring.ownTileX)Player.tileTargetX=retiring.oldTileX;if(Player.tileTargetY==retiring.ownTileY)Player.tileTargetY=retiring.oldTileY;}
                owner=retiring.previous;
            }
        }
    }
}
