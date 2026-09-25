using System;
using System.Collections.Generic;
using System.Diagnostics;
using JueMingR.TerrariaHost.F5;

namespace JueMingR.TerrariaHost.About
{
    // One page owner; F5 remains the only gesture and layout-generation owner.
    internal sealed class AboutPage
    {
        internal const string Group = "915753352";
        private Func<string, bool> copy;
        private Func<double> milliseconds;
        private F5Element[] results;
        private int outcome;
        private double expires;
        internal int Revision { get; private set; }
        private bool imageFailed;
        internal void SetImageFailure(bool failed) { if (imageFailed != failed) { imageFailed = failed; Revision++; } }
        internal void Attach(Func<string, bool> clipboard, Func<double> clock = null)
        { copy = clipboard; milliseconds = clock ?? Milliseconds; outcome = 0; }
        private static double Milliseconds() { return Stopwatch.GetTimestamp() * (1000.0 / Stopwatch.Frequency); }
        internal static bool Owns(F5Command command)
        { return command == F5Command.AboutCopyGroup; }
        internal void Execute(F5Command command)
        {
            if (!Owns(command)) return;
            bool success = false;
            try { success = copy != null && copy(Group); }
            catch { /* The mechanical outlet gives no reliable cause; never claim success. */ }
            outcome = success ? 1 : 2;
            expires = (milliseconds ?? Milliseconds)() + 3000;
        }
        internal void Leave()
        { outcome = 0; }
        internal string Hint(F5Command command)
        { return Owns(command) ? "点击复制" : null; }
        internal F5Element Display(F5Element element)
        {
            if (!Owns(element.Command) || outcome == 0 || results == null) return element;
            // Only pending feedback reads the monotonic clock. Expiry changes
            // the premeasured label, never geometry, input generation or clipboard.
            if ((milliseconds ?? Milliseconds)() >= expires) { outcome = 0; return element; }
            return results[outcome];
        }
        internal void Build(List<F5Element> elements, Func<string, float, F5Size> measure, float viewportHeight, ref float y)
        {
            // Preserve Z's 78 / 8 / two-column / 8 / 54 composition and ornaments.
            // Default size presents the whole canvas. Short windows scroll without shrinking text or QR codes.
            float columns = Math.Max(420, viewportHeight - 148), leftWidth = 254, rightX = 268, rightWidth = 254;
            Card(elements, 0, y, 522, columns + 148, "backdrop");
            Card(elements, 0, y, 522, 78, "header");
            FixedCenter(elements, measure, "决明R", 154, y + 18, 214, 42, 1.42f, F5ElementKind.AboutHeading);
            y += 86; float top = y;
            Card(elements, 0, top, leftWidth, columns, "portrait");
            FixedCenter(elements, measure, "广 告 位 招 租", 24, top + columns / 2 - 26, leftWidth - 48, 22, .80f, F5ElementKind.AboutMuted);
            FixedCenter(elements, measure, "(还没想好这块咋做)", 24, top + columns / 2, leftWidth - 48, 22, .62f, F5ElementKind.AboutMuted);
            float infoHeight = Math.Min(178, Math.Max(164, columns * .40f));
            Card(elements, rightX, top, rightWidth, infoHeight, "info");
            FixedCenter(elements, measure, "决明R", rightX + 46, top + 11, rightWidth - 64, 19, .70f, F5ElementKind.AboutHeading);
            float textY = top + 40 + Math.Max(0, (infoHeight - 156) / 2);
            FixedCenter(elements, measure, "为 Terraria 玩家打造的轻量辅助工具。", rightX + 18, textY, rightWidth - 36, 23, .74f, F5ElementKind.Text);
            FixedCenter(elements, measure, "追求稳定、简洁，并尽量保留原版体验。", rightX + 18, textY + 23, rightWidth - 36, 23, .72f, F5ElementKind.Text);
            FixedCenter(elements, measure, "项目仍在持续打磨中。", rightX + 18, textY + 58, rightWidth - 36, 23, .72f, F5ElementKind.Text);
            FixedCenter(elements, measure, "欢迎反馈问题与建议，一起把它做得更好。", rightX + 18, textY + 81, rightWidth - 36, 23, .68f, F5ElementKind.AboutMuted);
            float feedbackY = top + infoHeight + 8;
            Card(elements, rightX, feedbackY, rightWidth, 78, "feedback");
            FixedCenter(elements, measure, "QQ 问题反馈群", rightX + 52, feedbackY + 9, rightWidth - 70, 20, .70f, F5ElementKind.Text);
            ActionButton(elements, measure, new F5Rect(rightX + 28, feedbackY + 42, rightWidth - 56, 26), Group, F5Command.AboutCopyGroup, .88f);
            float qrY = feedbackY + 86, qrHeight = top + columns - qrY;
            Card(elements, rightX, qrY, rightWidth, qrHeight, "qr");
            FixedCenter(elements, measure, "喜欢的话，可以请我喝杯奶茶。", rightX + 42, qrY + 9, rightWidth - 58, 22, .68f, F5ElementKind.Text);
            float qrSize = Math.Max(76, Math.Min(96, Math.Min((rightWidth - 42) / 2, qrHeight - 66)));
            float qrX = rightX + (rightWidth - qrSize * 2 - 16) / 2;
            elements.Add(new F5Element(F5ElementKind.Image, new F5Rect(qrX, qrY + 42, qrSize, qrSize), "wechat.png", default(F5Size), 1, F5Command.None));
            elements.Add(new F5Element(F5ElementKind.Image, new F5Rect(qrX + qrSize + 16, qrY + 42, qrSize, qrSize), "alipay.jpg", default(F5Size), 1, F5Command.None));
            y = top + columns + 8; Card(elements, 0, y, 522, 54, "footer");
            FixedCenter(elements, measure, "感谢每一位使用与支持决明R的冒险者！", 18, y + 7, 486, 20, .66f, F5ElementKind.Text);
            FixedCenter(elements, measure, imageFailed ? "赞助图片未能载入，请重新启动后再试。" : "你们的反馈与陪伴，是我们持续前进的动力！", 18, y + 28, 486, 20, .60f, F5ElementKind.AboutMuted);
            y += 60; // F5 removes the conventional six-unit trailing row gap.
        }
        private static void Card(List<F5Element> elements, float x, float y, float width, float height, string style)
        { elements.Add(new F5Element(F5ElementKind.AboutCard, new F5Rect(x, y, width, height), style, default(F5Size), 0, F5Command.None)); }
        private static void FixedCenter(List<F5Element> elements, Func<string, float, F5Size> measure, string text, float x, float y, float width, float height, float preferred, F5ElementKind kind)
        {
            F5Size size = measure(text, preferred); float scale = FittedScale(size, width, height, preferred);
            size = measure(text, scale);
            elements.Add(new F5Element(kind, new F5Rect(x + (width - size.Width) / 2, y + (height - size.Height) / 2, size.Width, size.Height), text, size, scale, F5Command.None));
        }
        private void ActionButton(List<F5Element> elements, Func<string, float, F5Size> measure, F5Rect rect, string label, F5Command command, float preferred)
        {
            string[] labels = { label, "复制成功", "复制失败" }; var variants = new F5Element[3];
            for (int i = 0; i < 3; i++)
            {
                var size = measure(labels[i], preferred); float scale = FittedScale(size, rect.Width - 56, rect.Height - 4, preferred);
                variants[i] = new F5Element(F5ElementKind.Button, rect, labels[i], measure(labels[i], scale), scale, command);
            }
            results = variants; elements.Add(variants[0]);
        }
        private static float FittedScale(F5Size size, float width, float height, float preferred)
        {
            // F5 metrics include a constant four-unit outline; it does not shrink
            // with glyph scale and must be removed before solving the fit.
            return preferred * Math.Min(1, Math.Min(Math.Max(0, width - 4) / Math.Max(1, size.Width - 4), Math.Max(0, height - 4) / Math.Max(1, size.Height - 4)));
        }
    }
}
