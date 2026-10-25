using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackObstacles
    {
        internal bool LunarNear(int tick,float x,float y,PredictionTerrain terrain)
        {
            // Selected motion is the actual Source point. Other bodies use the
            // already captured stationary-receiver condition, with their native
            // size/position. Neither netOffset nor type414's damage extension
            // changes NPC.Center in AI's strict sixty-pixel test.
            var b=timeline[tick].Bounds;
            if(selectedChaseable && timeline[tick].CanReceive && Near(b.CenterX,b.CenterY,x,y,terrain))return true;
            foreach(var receiver in receivers)
                if(receiver.Chaseable && Near(receiver.Position.X+receiver.RawBox.Width*.5f,receiver.Position.Y+receiver.RawBox.Height*.5f,x,y,terrain))return true;
            return false;
        }
        private static bool Near(float cx,float cy,float x,float y,PredictionTerrain terrain)
        {return Vector2.DistanceSquared(new Vector2(cx,cy),new Vector2(x,y))<3600 && terrain.MeleeLine(x,y,cx,cy);}
    }
}
