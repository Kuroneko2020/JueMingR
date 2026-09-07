using Microsoft.Xna.Framework;

namespace Terraria
{
    public partial class Main
    {
        public static bool drawingPlayerChat, editSign, editChest, blockInput;
        public static object CurrentInputTextTakerOverride;
        public static int keyCount;
        public static readonly int[] keyInt = new int[100];
        public static readonly string[] keyString = new string[100];
        internal static Vector2 NotesImeAnchor;
        public static void clrInput() { keyCount = 0; }
        public void HandleIME() { }
        public void SetIMEPanelAnchor(Vector2 anchor, float xAnchor) { NotesImeAnchor = anchor; }
        internal static void NotesText(string text)
        { foreach (char character in text) { keyInt[keyCount] = character; keyString[keyCount++] = character.ToString(); } }
    }
}
namespace Terraria.GameInput
{
    public static partial class PlayerInput { public static bool WritingText; }
}
