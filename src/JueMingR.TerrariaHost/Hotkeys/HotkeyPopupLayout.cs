using System;
using System.Collections.Generic;
using JueMingR.TerrariaHost.F5;

namespace JueMingR.TerrariaHost.Hotkeys
{
    internal sealed class HotkeyPopupLayout
    {
        internal readonly List<F5Element> Text = new List<F5Element>();
        internal readonly List<F5Element> Buttons = new List<F5Element>();
        internal F5Rect Panel;
        internal int Generation { get; private set; }
        internal float Width, Height;
        private object font;
        private string previousTitle, previousBinding, previousStatus;
        private bool previousCapture;
        private F5Rect anchor;
        internal bool Matches(float width, float height, object currentFont)
        { return Width == width && Height == height && ReferenceEquals(font, currentFont); }
        internal void Build(float width, float height, object font, F5Rect anchor, string title, string binding, string status, bool capturing, Func<string, float, F5Size> measure)
        {
            if (Matches(width, height, font) && previousTitle == title && previousBinding == binding && previousStatus == status && previousCapture == capturing && this.anchor.X == anchor.X && this.anchor.Y == anchor.Y) return;
            Width = width; Height = height; this.font = font; previousTitle = title; previousBinding = binding; previousStatus = status; previousCapture = capturing; this.anchor = anchor; Generation++;
            Text.Clear(); Buttons.Clear(); float w = Math.Min(410, width - 24), y = 12;
            if (w < 240) throw new InvalidOperationException("Viewport cannot fit readable hotkey controls.");
            var rows = new F5RowLayout(Text, measure);
            rows.TextLines(title + " · 快捷键", 12, ref y, w - 24, .75f); y += 8;
            rows.TextLines("当前：" + binding, 12, ref y, w - 24, .7f); y += 8;
            rows.TextLines("最多三个左右修饰键 + 一个键盘或鼠标键。Esc 取消；清除请点按钮。滚轮不录入。", 12, ref y, w - 24, .65f); y += 8;
            rows.TextLines(status, 12, ref y, w - 24, .7f); y += 10;
            new F5RowLayout(Buttons, measure).Buttons(ref y, 12, w - 24, new[] { capturing ? "取消录入" : "开始录入", "清除", "关闭" });
            float h = y + 10;
            if (h > height - 24) throw new InvalidOperationException("Viewport cannot fit readable hotkey feedback.");
            float x = Math.Max(12, Math.Min(width - w - 12, anchor.Right - w));
            float top = anchor.Bottom + 6;
            if (top + h > height - 12) top = anchor.Y - h - 6;
            Panel = new F5Rect((float)Math.Floor(x), (float)Math.Floor(Math.Max(12, Math.Min(height - h - 12, top))), w, h);
        }
        internal int Hit(float x, float y)
        { for (int i = 0; i < Buttons.Count; i++) if (Buttons[i].Rect.Offset(Panel.X, Panel.Y).Contains(x, y)) return i; return -1; }
    }
}
