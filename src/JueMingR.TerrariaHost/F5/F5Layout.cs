using System;
using System.Collections.Generic;

namespace JueMingR.TerrariaHost.F5
{
    internal struct F5Size
    {
        internal readonly float Width;
        internal readonly float Height;
        internal readonly float OffsetX, OffsetY;
        internal F5Size(float width, float height, float offsetX = 0, float offsetY = 0)
        { Width = width; Height = height; OffsetX = offsetX; OffsetY = offsetY; }
    }

    internal struct F5Rect
    {
        internal readonly float X, Y, Width, Height;
        internal float Right { get { return X + Width; } }
        internal float Bottom { get { return Y + Height; } }
        internal F5Rect(float x, float y, float width, float height)
        { X = x; Y = y; Width = width; Height = height; }
        internal bool Contains(float x, float y)
        { return x >= X && y >= Y && x < Right && y < Bottom; }
        internal F5Rect Offset(float x, float y)
        { return new F5Rect(X + x, Y + y, Width, Height); }
    }

    internal enum F5ElementKind { Panel, Text, Button, BiomeStatus, Field }
    internal enum F5Command { None, EnableBiome, DisableBiome }

    internal sealed class F5Element
    {
        internal readonly F5ElementKind Kind;
        internal readonly F5Rect Rect;
        internal readonly string Text;
        internal readonly F5Size TextSize;
        internal readonly float TextScale;
        internal readonly F5Command Command;
        internal readonly bool Accent;
        internal F5Element(F5ElementKind kind, F5Rect rect, string text,
            F5Size size, float scale, F5Command command, bool accent)
        { Kind = kind; Rect = rect; Text = text; TextSize = size; TextScale = scale; Command = command; Accent = accent; }
    }

    // All rectangles are window-local or page-local. Origin and scrolling never
    // enter this cache. The renderer and input controller consume this same data.
    internal sealed class F5Layout
    {
        internal static readonly string[] Pages =
        { "物品", "杂项", "地图", "查询", "笔记", "关于", "蓝图", "钓鱼", "战斗", "信息", "增益", "移动" };
        private static readonly string[] Hints = { "开启群系显示", "关闭群系显示", "群系显示暂不可用" };
        private readonly F5Size[] hintSizes = new F5Size[Hints.Length];
        private readonly Dictionary<string, F5Size> textSizes = new Dictionary<string, F5Size>(StringComparer.Ordinal);
        private readonly List<F5Element> elements = new List<F5Element>(160);
        private readonly F5Size[] navSizes = new F5Size[12];
        private readonly F5Size[] biomeStatusSizes = new F5Size[3];
        private Func<string, F5Size> measure;
        private object fontIdentity;
        private float screenWidth, screenHeight, uiScale;
        private int page = -1;
        internal int Generation { get; private set; }
        internal int MeasurementCount { get; private set; }
        internal int FontMetricsGeneration { get; private set; }
        internal F5Rect Window { get; private set; }
        internal F5Rect Viewport { get; private set; }
        internal F5Rect ContentPanel { get; private set; }
        internal F5Rect Title { get { return new F5Rect(4, 4, 572, 30); } }
        internal F5Size TitleSize { get; private set; }
        internal float ContentHeight { get; private set; }
        internal IList<F5Element> Elements { get { return elements; } }
        internal float MaxScroll { get { return Math.Max(0, ContentHeight - Viewport.Height); } }

        internal static F5Size WindowSize(float width, float height, float scale)
        {
            if (scale <= 0 || float.IsNaN(scale) || float.IsInfinity(scale) ||
                width / scale < 604 || height / scale < 220)
                throw new InvalidOperationException("F5 viewport is smaller than its readable window.");
            return new F5Size(580, Math.Min(740, height / scale - 24));
        }

        internal bool Matches(float width, float height, float scale, int currentPage)
        { return Generation > 0 && width == screenWidth && height == screenHeight && scale == uiScale && page == currentPage; }

        internal void Ensure(float width, float height, float scale, int currentPage,
            object font, Func<string, F5Size> measureText)
        {
            if (font == null || measureText == null || currentPage < 0 || currentPage >= Pages.Length)
                throw new ArgumentException("F5 layout inputs are unavailable.");
            bool metricsChanged = false;
            if (!ReferenceEquals(fontIdentity, font))
            {
                measure = measureText;
                var keys = new List<string>(textSizes.Keys);
                foreach (string key in keys)
                {
                    F5Size old = textSizes[key];
                    F5Size value = MeasureChecked(key);
                    textSizes[key] = value;
                    metricsChanged |= old.Width != value.Width || old.Height != value.Height ||
                        old.OffsetX != value.OffsetX || old.OffsetY != value.OffsetY;
                }
                fontIdentity = font;
                if (metricsChanged || Generation == 0) FontMetricsGeneration++;
            }
            if (Matches(width, height, scale, currentPage) && !metricsChanged) return;
            F5Size size = WindowSize(width, height, scale);
            Window = new F5Rect(0, 0, size.Width, size.Height);
            ContentPanel = new F5Rect(12, 131, 556, size.Height - 143);
            Viewport = new F5Rect(20, 139, 522, size.Height - 159);
            TitleSize = TextSize("JueMingR", 0.75f);
            for (int i = 0; i < Hints.Length; i++)
            {
                hintSizes[i] = TextSize(Hints[i], 0.65f);
                if (hintSizes[i].Width > Window.Width - 32 || hintSizes[i].Height > Window.Height - 32)
                    throw new InvalidOperationException("F5 hover text cannot fit its window.");
            }
            if (TitleSize.Height > Title.Height - 2) throw new InvalidOperationException("F5 title font exceeds the approved title bar.");
            for (int i = 0; i < Pages.Length; i++)
            {
                navSizes[i] = TextSize(Pages[i], 0.75f);
                if (navSizes[i].Width + 23 > Navigation(i).Width - 8 || navSizes[i].Height > 28)
                    throw new InvalidOperationException("F5 navigation font exceeds readable bounds.");
            }
            elements.Clear();
            float y = 0;
            if (currentPage == 9) BuildInformation(ref y);
            else if (currentPage == 7) BuildFishing(ref y);
            ContentHeight = Math.Max(0, y - 6);
            screenWidth = width; screenHeight = height; uiScale = scale; page = currentPage;
            Generation++;
        }

        internal F5Rect Navigation(int index)
        { return new F5Rect(12 + index % 6 * (560f / 6), 46 + index / 6 * 40, 560f / 6 - 4, 32); }
        internal F5Size NavigationSize(int index) { return navSizes[index]; }
        internal F5Rect NavigationIcon(int index)
        {
            F5Rect nav = Navigation(index);
            return new F5Rect(nav.X + (nav.Width - navSizes[index].Width - 23) / 2, nav.Y + (nav.Height - 18) / 2, 18, 18);
        }
        internal F5Rect NavigationLabel(int index)
        {
            F5Rect icon = NavigationIcon(index), nav = Navigation(index);
            return new F5Rect(icon.Right + 5, nav.Y + (nav.Height - navSizes[index].Height) / 2,
                navSizes[index].Width, navSizes[index].Height);
        }
        internal static int HintIndex(F5Element element, bool biomeFailed)
        { return element.Command == F5Command.None ? -1 : biomeFailed ? 2 : element.Command == F5Command.EnableBiome ? 0 : 1; }
        internal static string HintText(int index) { return Hints[index]; }
        internal F5Size HintSize(int index) { return hintSizes[index]; }
        internal F5Size BiomeStatusSize(bool failed, bool enabled) { return biomeStatusSizes[failed ? 2 : enabled ? 0 : 1]; }
        internal F5Rect ScrollTrack { get { return new F5Rect(550, 139, 10, Viewport.Height); } }
        internal F5Rect ScrollTrackVisual { get { return new F5Rect(553, 139, 4, Viewport.Height); } }
        internal F5Rect ScrollThumbVisual(float scroll)
        { F5Rect thumb = ScrollThumb(scroll); return new F5Rect(552, thumb.Y, 6, thumb.Height); }
        internal F5Rect ScrollThumb(float scroll)
        {
            float height = MaxScroll <= 0 ? Viewport.Height : Math.Max(24, Viewport.Height * Viewport.Height / ContentHeight);
            float y = MaxScroll <= 0 ? 0 : Math.Max(0, Math.Min(MaxScroll, scroll)) / MaxScroll * (Viewport.Height - height);
            return new F5Rect(550, 139 + y, 10, height);
        }

        private void BuildInformation(ref float y)
        {
            string[] names = { "敌怪显名", "动物显名", "NPC显名", "宝箱显名", "牌子显示", "墓碑显示",
                "显示生命水晶", "显示魔力水晶", "显示碎岩龟", "显示生命果", "显示龙蛋", "调整信息窗位置",
                "群系显示", "世界感染", "幸运值", "完整鱼获", "过滤鱼获", "渔夫任务" };
            for (int i = 0; i < names.Length; i++)
            {
                string[] actions = i == 12 ? new[] { "开启", "关闭" } : i == 11 ? new[] { "开始" } :
                    i == 2 ? new[] { "配置", "名字", "类型", "关闭", "键" } :
                    i == 3 ? new[] { "配置", "始终", "开过", "关闭", "键" } :
                    i == 4 || i == 5 ? new[] { "全部", "前几行", "前几字", "关闭" } :
                    new[] { "配置", "开启", "关闭", "键" };
                Row(ref y, 0, 522, names[i], actions, i == 12);
            }
        }

        private void BuildFishing(ref float y)
        {
            string[] names = { "自动钓鱼", "自动换装", "自动配装", "自动存放鱼", "切杆跳过", "快捷改名" };
            foreach (string name in names)
                Row(ref y, 0, 522, name, name == "快捷改名" ? new[] { null, "快捷改名" } :
                    name == "自动存放鱼" ? new[] { "所有", "任务鱼", "关闭", "键" } : new[] { "开启", "关闭", "键" }, false);
            float top = y, left = y + 8, right = y + 8;
            int leftPanel = elements.Count;
            Panel(new F5Rect(0, top, 364, 0));
            Row(ref left, 8, 348, "过滤名单", new string[0], false);
            Buttons(ref left, 8, 348, new[] { "精确匹配", "关键词" }, false);
            Buttons(ref left, 8, 348, new[] { "添加当前", "+", "清空", "保存预设", "预设列表" }, false);
            int rightPanel = elements.Count;
            Panel(new F5Rect(380, top, 142, 0));
            Row(ref right, 388, 126, "过滤模式", new string[0], false);
            Buttons(ref right, 388, 126, new[] { "关闭过滤" }, false);
            right += 12;
            Row(ref right, 388, 126, "特殊过滤", new string[0], false);
            foreach (string name in new[] { "匣子", "怪物", "任务鱼" })
                Row(ref right, 388, 126, name, new string[0], false);
            float blockHeight = Math.Max(left, right) - top + 8;
            elements[leftPanel] = new F5Element(F5ElementKind.Panel, new F5Rect(0, top, 364, blockHeight), null, default(F5Size), 0, F5Command.None, false);
            elements[rightPanel] = new F5Element(F5ElementKind.Panel, new F5Rect(380, top, 142, blockHeight), null, default(F5Size), 0, F5Command.None, false);
            y = top + blockHeight + 6;
        }

        private void Row(ref float y, float x, float width, string label, string[] actions, bool biome)
        {
            float actionWidth = 0;
            foreach (string action in actions)
            {
                if (action == null) { actionWidth += 124; continue; }
                F5Size size = TextSize(action, 0.70f);
                actionWidth += Math.Max(30, size.Width + 16) + 4;
            }
            if (actions.Length > 0) actionWidth -= 4;
            bool below = actionWidth > width - 120;
            float textWidth = below || actions.Length == 0 ? width - 16 : width - actionWidth - 28;
            int panel = elements.Count;
            Panel(new F5Rect(x, y, width, 34));
            int firstText = elements.Count;
            float labelY = y + 4;
            TextLines(label, x + 8, ref labelY, textWidth, 0.75f, biome);
            if (width >= 300)
            {
                if (biome)
                {
                    F5Size on = TextSize("当前：已开启", 0.46f), off = TextSize("当前：已关闭", 0.46f);
                    F5Size unavailable = TextSize("当前：不可用", 0.46f);
                    biomeStatusSizes[0] = on; biomeStatusSizes[1] = off; biomeStatusSizes[2] = unavailable;
                    F5Size statusSize = new F5Size(Math.Max(Math.Max(on.Width, off.Width), unavailable.Width),
                        Math.Max(Math.Max(on.Height, off.Height), unavailable.Height));
                    if (statusSize.Width > textWidth) throw new InvalidOperationException("F5 biome status cannot fit its row.");
                    elements.Add(new F5Element(F5ElementKind.BiomeStatus, new F5Rect(x + 8, labelY, statusSize.Width, statusSize.Height),
                        null, statusSize, 0.46f, F5Command.None, false));
                    labelY += statusSize.Height + 1;
                }
            }
            float textHeight = labelY - 1 - (y + 4);
            float buttonHeight = 30;
            foreach (string action in actions)
                if (action != null) buttonHeight = Math.Max(buttonHeight, TextSize(action, 0.70f).Height + 6);
            float rowHeight = Math.Max(38, Math.Max(textHeight, below ? 0 : buttonHeight) + 8);
            float shift = (rowHeight - textHeight) / 2 - 4;
            for (int i = firstText; i < elements.Count; i++)
            {
                F5Element text = elements[i];
                elements[i] = new F5Element(text.Kind, text.Rect.Offset(0, shift), text.Text,
                    text.TextSize, text.TextScale, text.Command, text.Accent);
            }
            float end = y + rowHeight;
            if (actions.Length > 0)
            {
                float buttonY = below ? end + 2 : y + (rowHeight - buttonHeight) / 2;
                Buttons(ref buttonY, below ? x + 8 : x + width - 8 - actionWidth,
                    below ? width - 16 : actionWidth, actions, biome);
                if (below) end = buttonY;
            }
            elements[panel] = new F5Element(F5ElementKind.Panel, new F5Rect(x, y, width, end - y), null, default(F5Size), 0, F5Command.None, false);
            y = end + 6;
        }

        private void Buttons(ref float y, float x, float width, string[] labels, bool biome)
        {
            float cursor = x, rowHeight = 30;
            foreach (string label in labels)
                if (label != null) rowHeight = Math.Max(rowHeight, TextSize(label, 0.70f).Height + 6);
            foreach (string label in labels)
            {
                F5Size size = label == null ? default(F5Size) : TextSize(label, 0.70f);
                float w = label == null ? 120 : Math.Max(30, size.Width + 16), h = rowHeight;
                if (w > width) throw new InvalidOperationException("F5 button text exceeds its available column.");
                if (cursor > x && cursor + w > x + width + 0.01f) { y += rowHeight + 4; cursor = x; }
                F5Command command = !biome ? F5Command.None : label == "开启" ? F5Command.EnableBiome : F5Command.DisableBiome;
                elements.Add(new F5Element(label == null ? F5ElementKind.Field : F5ElementKind.Button,
                    new F5Rect(cursor, y, w, h), label, size, 0.70f, command, false));
                cursor += w + 4;
            }
            y += rowHeight + 4;
        }

        private void Panel(F5Rect rect)
        { elements.Add(new F5Element(F5ElementKind.Panel, rect, null, default(F5Size), 0, F5Command.None, false)); }

        private void TextLines(string text, float x, ref float y, float width, float scale, bool accent)
        {
            string remaining = text;
            while (remaining.Length > 0)
            {
                int count = remaining.Length;
                while (count > 0 && TextSize(remaining.Substring(0, count), scale).Width > width) count--;
                if (count == 0) throw new InvalidOperationException("F5 font cannot fit a readable character.");
                string line = remaining.Substring(0, count);
                F5Size size = TextSize(line, scale);
                elements.Add(new F5Element(F5ElementKind.Text, new F5Rect(x, y, size.Width, size.Height), line, size, scale, F5Command.None, accent));
                y += size.Height + 1;
                remaining = remaining.Substring(count);
            }
        }

        private F5Size TextSize(string text, float scale)
        {
            F5Size size;
            if (!textSizes.TryGetValue(text, out size))
            {
                if (textSizes.Count >= 1024) throw new InvalidOperationException("F5 fixed-text cache exceeded its bound.");
                size = MeasureChecked(text);
                textSizes.Add(text, size);
            }
            // Terraria's four-way border is +/- 2 logical units at every font scale.
            return new F5Size(size.Width * scale + 4, size.Height * scale + 4,
                size.OffsetX * scale - 2, size.OffsetY * scale - 2);
        }

        private F5Size MeasureChecked(string text)
        {
            F5Size size = measure(text);
            MeasurementCount++;
            if (size.Width < 0 || size.Height <= 0 || float.IsNaN(size.Width) || float.IsInfinity(size.Width) ||
                float.IsNaN(size.Height) || float.IsInfinity(size.Height) || float.IsNaN(size.OffsetX) ||
                float.IsInfinity(size.OffsetX) || float.IsNaN(size.OffsetY) || float.IsInfinity(size.OffsetY))
                throw new InvalidOperationException("F5 font returned invalid metrics.");
            return size;
        }
    }
}
