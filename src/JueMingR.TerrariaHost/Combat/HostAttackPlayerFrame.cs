using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // One private continuation boundary, passed explicitly between solvers.
    // A MemberwiseClone is a shape reader, never a new player observation:
    // the original token, gravity-source ordinal and forced clock travel
    // with the already advanced values. This owns no cache or live mutation.
    internal readonly struct HostAttackPlayerFrame
    {
        internal readonly PredictionPlayerMotion Motion;
        internal readonly PredictionEnvironment Environment;
        internal HostAttackPlayerFrame(PredictionPlayerMotion motion,PredictionEnvironment environment)
        {Motion=motion;Environment=environment;}
        internal static HostAttackPlayerFrame Capture(Player player,NpcTrajectory timeline)
        {
            var motion=NpcPredictionSource.ReadPlayer(player);
            var world=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,Remix=Main.remixWorld,Expert=Main.expertMode,WallViewWidth=Main.MaxWorldViewSize.X};
            NpcPredictionSource.TryBindPlayerGravity(timeline,(long)Main.GameUpdateCount,ref motion,ref world);
            NpcPredictionSource.TryBindPlayerWall(timeline,(long)Main.GameUpdateCount,ref motion,ref world);
            return new HostAttackPlayerFrame(motion,world);
        }
        internal static void ApplyPose(Player privateBody,PredictionPlayerMotion motion)
        {
            privateBody.position=new Vector2(motion.X,motion.Y);privateBody.velocity=new Vector2(motion.Vx,motion.Vy);
            privateBody.gravDir=motion.GravityDirection;privateBody.forcedGravity=motion.ForcedGravityTicks;
            privateBody.jump=motion.Jump;privateBody.releaseJump=motion.ReleaseJump;privateBody.releaseUp=motion.GravityReleaseUp;
            privateBody.wet=motion.Wet;privateBody.honeyWet=motion.Honey;privateBody.lavaWet=motion.Lava;privateBody.shimmerWet=motion.Shimmer;
            if(motion.WallFactsCaptured)privateBody.tongued=motion.WallTongued;
        }
    }
}
