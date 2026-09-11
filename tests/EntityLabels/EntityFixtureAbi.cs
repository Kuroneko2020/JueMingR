using Microsoft.Xna.Framework;

namespace Terraria
{
    public sealed partial class Player { public float gravDir = 1; }
    public partial class Entity { public int width = 32, height = 32; }
    // Deliberately controlled field facts. These tests exercise the production
    // reader and never execute native NPC AI or copy the game implementation.
    public sealed partial class NPC
    {
        public int type, netID, life = 100, lifeMax = 100, realLife = -1;
        public short catchItem;
        public bool townNPC, hide;
        public bool CountsAsACritter { get; set; }
        public byte generation { get; set; }
        public float gfxOffY;
        public Vector2 netOffset;
        public readonly float[] ai = new float[4];
        public string TypeName { get; set; } = "测试对象";
        public string GivenName { get; set; }
        public string GivenOrTypeName { get { return GivenName ?? TypeName; } }
    }
    public partial class Main
    {
        public static readonly int maxNPCs = 200;
        public static int screenWidth = 800, wofNPCIndex = -1;
        public static Vector2 screenPosition;
        public static Graphics.SpriteViewMatrix GameViewMatrix = new Graphics.SpriteViewMatrix();
    }
}
namespace Terraria.Graphics
{
    public sealed class SpriteViewMatrix { public Matrix ZoomMatrix { get; set; } = Matrix.Identity; }
}
namespace Terraria.ID
{
    public static class NPCID
    {
        public static readonly short Count = 800;
        public const int SkeletonMerchant = 453, TargetDummy = 488, WindyBalloon = 594, BoundTownSlimePurple = 686;
        public static class Sets
        {
            public static readonly bool[] CountsAsCritter = new bool[Count];
            public static readonly bool[] IsGoldCritter = new bool[Count];
        }
    }
}
