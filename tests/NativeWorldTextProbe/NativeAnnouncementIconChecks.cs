using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using JueMingR.Features.Announcements;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Chat;
using Terraria.UI.Chat;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeAnnouncementIconChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private static readonly Color Gold = new Color(255, 217, 102);
        internal static void Run(Assembly assembly)
        {
            ChatManager.Register<ColorTagHandler>("c", "color"); ChatManager.Register<ItemTagHandler>("i", "item");
            Require(!ChatManager.ParseMessage("[c/FFD966:名字 [i:2]]", Color.White).Any(IsIcon), "native color tags do not recursively parse item tags");
            var observation = assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.NativeTargetObservation");
            foreach (int type in new[] { 2, 560 })
            {
                object value = observation.GetMethod("UiItem", Flags).Invoke(null, new object[] { type, 9999 });
                string message = SafeChatText.Build((List<string>)Get(value, "Entries"));
                AssertMessage(message, "这里有 9999 个 " + Lang.GetItemNameValue(type) + " ", type);
            }
            string mixed = SafeChatText.BuildItems(new[] { "8 个 木材 [i:9]", "11 个 火把 [i:8]" });
            AssertMessage(mixed, "这里有 8 个 木材 和11 个 火把 ", 9, 8);
            foreach (string bad in new[] { "[i/s999:9]", "[i/p1:9]", "[c/ff0000:坏]", "[i:0]", "[i:-1]", "[i:+9]", "[i:09]", "[i:2147483648]", "[item:9]", "[i:9", "[i:9]]" })
                Require(!ChatManager.ParseMessage(SafeChatText.Build(new[] { bad }), Color.White).Any(IsIcon), "noncanonical/untrusted markup is rejected: " + bad);
            string clean = SafeChatText.CleanName("/help[i:8]Á😀[c/ff0000:坏]", 80);
            AssertMessage(SafeChatText.Build(new[] { clean + " [i:2]" }), "这里有 helpÁ😀 ", 2);
            string[] crowded = Enumerable.Range(0, 24).Select(i => new string('中', 18) + " [i:9]").ToArray();
            foreach (int budget in new[] { 32, 80, 1024 })
            {
                string limited = SafeChatText.Build(crowded, budget);
                Require(Encoding.UTF8.GetByteCount(limited) <= budget && (budget == 32 ? limited == "" : limited.Contains("部分内容已省略")), "complete icon/color framing and omission fit byte budget " + budget);
                var snippets = ChatManager.ParseMessage(limited, Color.White);
                Require(snippets.Where(s => !IsIcon(s)).All(s => s.Color == Gold && !s.Text.Contains("[") && !s.Text.Contains("]")), "budget never cuts tags or leaks literal markup");
                Require(snippets.Count(IsIcon) == snippets.Where(s => !IsIcon(s)).Sum(s => s.Text.Count(c => c == '中')) / 18, "budget keeps each whole name with its icon");
            }
            Console.WriteLine("PASS: actual native announcement item icons, quantities, gold text, injection and complete UTF-8 framing.");
        }
        internal static void AssertMessage(string message, string expectedText, params int[] itemTypes)
        {
            var parsed = ChatManager.ParseMessage(message, Color.White);
            var icons = parsed.Where(IsIcon).Select(s => (Item)s.GetType().GetField("_item", Flags).GetValue(s)).ToArray();
            Require(icons.Select(i => i.type).SequenceEqual(itemTypes) && icons.All(i => i.stack == 1 && i.prefix == 0), "native icon identities are reliable item types without invented stack/prefix");
            Require(string.Concat(parsed.Where(s => !IsIcon(s)).Select(s => s.Text)) == expectedText, "native visible text retains names, exact quantities and icon positions");
            Require(parsed.Where(s => !IsIcon(s)).All(s => s.Color == Gold) && Encoding.UTF8.GetByteCount(message) <= 1024, "all visible text stays gold within the full encoded budget");
        }
        private static bool IsIcon(TextSnippet value) { return value.GetType().Name == "ItemSnippet"; }
        internal static void Draw(ProbeGraphics graphics, string output)
        {
            var language = Terraria.Localization.LanguageManager.Instance.ActiveCulture;
            try
            {
                Terraria.Localization.LanguageManager.Instance.SetLanguage("zh-Hans");
                graphics.LoadItemTextures(new[] { 2, 560, 9, 8, 3270, 4 });
                var assembly = Assembly.LoadFrom(Path.Combine(Program.Repository, "artifacts/build/Debug/work/bin/JueMingR.TerrariaHost/x86/Debug/net472/JueMingR.TerrariaHost.dll"));
                var observation = assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.NativeTargetObservation");
                Func<int, string> label = type => (string)observation.GetMethod("ItemName", Flags).Invoke(null, new object[] { type });
                object crown = observation.GetMethod("UiItem", Flags).Invoke(null, new object[] { 560, 9999 });
                var messages = new[] {
                    SafeChatText.Build(new[] { label(2) }),
                    SafeChatText.Build((List<string>)Get(crown, "Entries")),
                    SafeChatText.BuildItems(new[] { "8 个 " + label(9), "11 个 " + label(8) }),
                    SafeChatText.Build(new[] { "放在" + label(3270) + "的1 个 " + label(4) })
                };
                graphics.Image(Path.Combine(output, "announcement-item-icons.png"), () => {
                    for (int i = 0; i < messages.Length; i++)
                        ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, graphics.Font, messages[i], new Vector2(30, 35 + i * 90), Color.White, 0, Vector2.Zero, Vector2.One);
                }, Matrix.Identity);
                var icon = ChatManager.ParseMessage("[i:560]", Color.White).ToArray();
                var pixels = graphics.Pixels(() => { int hovered; ChatManager.DrawColorCodedString(Main.spriteBatch, graphics.Font, icon, new Vector2(30, 30), Color.White, 0, Vector2.Zero, Vector2.One, out hovered); }, Matrix.Identity);
                Require(pixels.Count(p => p.A != 0) > 20, "real original item texture produces icon pixels");
                Console.WriteLine("PASS: native chat parser and actual XNB item textures draw the announcement icon preview.");
            }
            finally { Terraria.Localization.LanguageManager.Instance.SetLanguage(language); }
        }
    }
}
