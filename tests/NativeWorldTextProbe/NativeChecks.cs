using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using JueMingR.Features.WorldObjectText;
using JueMingR.Platform.WorldObjectText;
using JueMingR.TerrariaHost.WorldObjectText;
using Microsoft.Xna.Framework;
using Terraria.UI.Chat;

namespace NativeWorldTextProbe
{
    internal static class NativeChecks
    {
        internal static int Run(string content, string output)
        {
            string isolated = Path.Combine(Path.GetTempPath(), "JueMingR-native-text-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(isolated);
            // Main's beforefieldinit constructor reads Program.SavePath. This is
            // set before a separate no-inline method touches any Main field.
            Terraria.Program.SavePath = isolated;
            return Check(content, output);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Check(string content, string output)
        {
            var actual = typeof(ChatManager).Assembly;
            string hash; using (var stream = File.OpenRead(actual.Location)) using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            Require(hash == "960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3" && actual.ManifestModule.ModuleVersionId == new Guid("2c29f6c3-4bd9-4add-9c58-da159804e083"), "actual fixed EXE identity");
            ChatManager.Register<Terraria.GameContent.UI.Chat.ColorTagHandler>("c", "color");
            ChatManager.Register<Terraria.GameContent.UI.Chat.ItemTagHandler>("i", "item");
            Terraria.Localization.LanguageManager.Instance.SetLanguage("en-US"); Terraria.Lang.InitializeLegacyLocalization();
            Require(Terraria.Lang.chestType[10].Value == "Ivy Chest" && Terraria.Lang.dresserType[0].Value == "Dresser", "actual native language tables loaded");
            var parsed = ChatManager.ParseMessage("[c/ff0000:literal]", Color.White);
            Require(parsed.Count == 1 && parsed[0].Text == "literal" && parsed[0].Color == Color.Red, "actual native color parser");
            Console.WriteLine("PASS: real ChatManager assembly, MVID, hash and parser; no fixture Terraria assembly.");
            using (var graphics = new ProbeGraphics(content))
            {
                var style = WorldObjectSettings.Default.Style(WorldObjectKind.Sign).WithMode(WorldObjectMode.Characters).WithLimits(3, 3);
                foreach (string blank in new[] { new string('\n', 11), "  " })
                {
                    var empty = new NativeWorldTextLayout(graphics.Font, blank, style.WithLimits(3, 1), 460);
                    while (!empty.Ready) empty.Step(512);
                    Require(!empty.HasInk, "truncating an all-blank display must not manufacture an ellipsis label");
                }
                var slash = new NativeWorldTextLayout(graphics.Font, "[c/ff0000:A\\B]", style.WithMode(WorldObjectMode.All), 460);
                while (!slash.Ready) slash.Step(512);
                Require(!slash.Truncated && slash.VisibleUnits == 3 && slash.Snippets.Count == 3 && slash.Snippets[1].Snippet.Text == "\\", "a valid colored backslash remains literal and cannot disable labels");
                var layout = new NativeWorldTextLayout(graphics.Font, "[c/ff0000:Á]😀BZ", style, 460);
                while (!layout.Ready) layout.Step(512);
                Require(layout.Truncated && layout.VisibleUnits == 3 && layout.Lines == 1, "native layout truncates complete visible units");
                var lines = new NativeWorldTextLayout(graphics.Font, "a\r\n\r\nb\r\nc", style.WithMode(WorldObjectMode.Lines).WithLimits(3, 80), 460);
                while (!lines.Ready) lines.Step(512);
                Require(lines.Lines == 3 && lines.Truncated, "blank lines count toward final line cap");
                var wrapped = new NativeWorldTextLayout(graphics.Font, new string('W', 1000000), style.WithMode(WorldObjectMode.All), 120);
                while (!wrapped.Ready) wrapped.Step(512);
                Require(wrapped.Lines == 10 && wrapped.Truncated && wrapped.SourceWork < 4096, "long cold input stops at the final wrapped-line guard");
                var icon = new NativeWorldTextLayout(graphics.Font, "A[i/s20:8]B", style.WithLimits(3, 2), 460);
                while (!icon.Ready) icon.Step(512);
                int icons = 0; foreach (var snippet in icon.Snippets) if (snippet.Snippet.DeleteWhole) icons++;
                Require(icon.Truncated && icon.VisibleUnits == 2 && icons == 1, "real item tag counts as one indivisible visible unit");
                float inventoryScale = Terraria.Main.inventoryScale;
                graphics.Preview(output, layout, lines, wrapped, slash, icon);
                Require(Terraria.Main.inventoryScale == inventoryScale, "native item drawing restores inventory scale");
                NativeWorldChecks.Run(graphics, output);
                Console.WriteLine("PASS: real-font bounded wrapping, Unicode, native colors and production positioned-snippet drawing.");
            }
            return 0;
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
