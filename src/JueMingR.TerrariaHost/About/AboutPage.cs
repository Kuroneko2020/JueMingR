using System;
using System.Collections.Generic;
using JueMingR.TerrariaHost.F5;

namespace JueMingR.TerrariaHost.About
{
    // One page owner; F5 remains the only gesture and layout-generation owner.
    internal sealed class AboutPage
    {
        internal const string Group = "915753352";
        private Func<string> versionSource;
        private Func<string, bool> copy;
        private string version;
        private readonly Dictionary<F5Command, F5Element[]> results = new Dictionary<F5Command, F5Element[]>();
        private readonly Dictionary<F5Command, int> outcome = new Dictionary<F5Command, int>();
        internal bool Help { get; private set; }
        internal int Revision { get; private set; }
        internal string Notice { get; private set; }
        private bool imageFailed;
        internal void SetImageFailure(bool failed) { if (imageFailed != failed) { imageFailed = failed; Revision++; } }
        internal string Version { get { return version ?? (version = versionSource == null ? "运行版本暂不可取得" : versionSource()); } }
        internal void Attach(Func<string> source, Func<string, bool> clipboard)
        { versionSource = source; copy = clipboard; version = null; Revision++; }
        internal void SetNotice(string value)
        { if (Notice != value) { Notice = value; Revision++; } }
        internal static bool Owns(F5Command command)
        { return command >= F5Command.AboutHelp && command <= F5Command.AboutCopyVersion; }
        internal void Execute(F5Command command)
        {
            if (!Owns(command)) return;
            if (command == F5Command.AboutHelp || command == F5Command.AboutBack)
            { Help = command == F5Command.AboutHelp; outcome.Clear(); Revision++; return; }
            bool success = false;
            try { success = copy != null && copy(command == F5Command.AboutCopyGroup ? Group : Version); }
            catch { /* The mechanical outlet gives no reliable cause; never claim success. */ }
            outcome[command] = success ? 1 : 2;
        }
        internal void Leave()
        { outcome.Clear(); if (Help) { Help = false; Revision++; } }
        internal string Hint(F5Command command)
        { int value; return outcome.TryGetValue(command, out value) && value == 2 ? "无法写入剪贴板。可以重新点击；群号和版本也可直接查看。" : command == F5Command.AboutCopyGroup ? "点击复制 QQ 群号。反馈请附现象、操作步骤和版本；截图自行选择发送。" : null; }
        internal F5Element Display(F5Element element)
        {
            int value; F5Element[] variants;
            return outcome.TryGetValue(element.Command, out value) && results.TryGetValue(element.Command, out variants) ? variants[value] : element;
        }
        internal void Build(List<F5Element> elements, Func<string, float, F5Size> measure, float viewportHeight, ref float y)
        {
            results.Clear();
            var rows = new F5RowLayout(elements, measure);
            if (Help)
            {
                Card(elements, 0, y, 522, 78, "header");
                FixedCenter(elements, measure, "使用帮助", 154, y + 20, 214, 38, 1.05f, F5ElementKind.AboutHeading);
                y += 90; Button(elements, measure, ref y, "返回关于", F5Command.AboutBack, 186, 150);
                if (!string.IsNullOrEmpty(Notice)) { rows.TextLines(Notice, 18, ref y, 486, .75f); y += 10; }
                if (imageFailed) { rows.TextLines("赞助图片未能载入，请重新启动后再试。", 18, ref y, 486, .75f); y += 10; }
                foreach (string line in Version.Split('\n')) { rows.TextLines(line.TrimEnd('\r'), 18, ref y, 486, .72f); y += 4; }
                rows.TextLines("适用：Windows · Terraria 1.4.5.8", 18, ref y, 486, .70f); y += 8;
                Button(elements, measure, ref y, "复制版本信息", F5Command.AboutCopyVersion, 18, 150); y += 8;
                rows.TextLines("赞助完全自愿，不影响任何功能。反馈请附现象、操作步骤和上面的运行版本；截图由你自行选择发送。", 18, ref y, 486, .72f); y += 12;
                for (int i = 1; i < HelpContent.Paragraphs.Length; i++)
                {
                    string paragraph = HelpContent.Paragraphs[i]; int colon = paragraph.IndexOf('：');
                    string heading = colon < 0 ? paragraph : paragraph.Substring(0, colon);
                    AddText(elements, measure, heading, 18, y, .86f, F5ElementKind.AboutHeading);
                    y += measure(heading, .86f).Height + 10;
                    rows.TextLines(colon < 0 ? "" : paragraph.Substring(colon + 1), 18, ref y, 486, .75f);
                    y += 12; Rule(elements, 18, y, 486); y += 18;
                }
                Button(elements, measure, ref y, "返回关于", F5Command.AboutBack, 186, 150); y += 6; return;
            }
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
            // The existing empty portrait panel hosts the required repeatable entry.
            // Full loaded build identity remains inspectable and copyable in Help.
            ActionButton(elements, measure, new F5Rect(28, top + columns - 60, leftWidth - 56, 30), "使用帮助 / 版本信息", F5Command.AboutHelp, .70f);
            float infoHeight = Math.Min(178, Math.Max(164, columns * .40f));
            Card(elements, rightX, top, rightWidth, infoHeight, "info");
            FixedCenter(elements, measure, Version.Split('\n')[0], rightX + 46, top + 11, rightWidth - 64, 19, .60f, F5ElementKind.AboutHeading);
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
            FixedCenter(elements, measure, "微信", qrX, qrY + qrSize + 49, qrSize, 20, .66f, F5ElementKind.Text);
            FixedCenter(elements, measure, "支付宝", qrX + qrSize + 16, qrY + qrSize + 49, qrSize, 20, .66f, F5ElementKind.Text);
            y = top + columns + 8; Card(elements, 0, y, 522, 54, "footer");
            FixedCenter(elements, measure, "感谢每一位使用与支持决明R的冒险者！", 18, y + 7, 486, 20, .66f, F5ElementKind.Text);
            FixedCenter(elements, measure, imageFailed ? "赞助图片未能载入，详见使用帮助。" : Notice != null ? "首次提示记录遇到问题，详见使用帮助。" : "你们的反馈与陪伴，是我们持续前进的动力！", 18, y + 28, 486, 20, .60f, F5ElementKind.AboutMuted);
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
            string[] labels = { label, "已复制", "复制失败" }; var variants = new F5Element[3];
            for (int i = 0; i < 3; i++)
            {
                var size = measure(labels[i], preferred); float scale = FittedScale(size, rect.Width - 56, rect.Height - 4, preferred);
                variants[i] = new F5Element(F5ElementKind.Button, rect, labels[i], measure(labels[i], scale), scale, command);
            }
            results[command] = variants; elements.Add(variants[0]);
        }
        private static float FittedScale(F5Size size, float width, float height, float preferred)
        {
            // F5 metrics include a constant four-unit outline; it does not shrink
            // with glyph scale and must be removed before solving the fit.
            return preferred * Math.Min(1, Math.Min(Math.Max(0, width - 4) / Math.Max(1, size.Width - 4), Math.Max(0, height - 4) / Math.Max(1, size.Height - 4)));
        }
        private static void AddText(List<F5Element> elements, Func<string, float, F5Size> measure, string text, float x, float y, float scale, F5ElementKind kind)
        { var size = measure(text, scale); elements.Add(new F5Element(kind, new F5Rect(x, y, size.Width, size.Height), text, size, scale, F5Command.None)); }
        private static void Rule(List<F5Element> elements, float x, float y, float width)
        { elements.Add(new F5Element(F5ElementKind.AboutOrnament, new F5Rect(x, y, width, 5), null, default(F5Size), 0, F5Command.None)); }
        private void Button(List<F5Element> elements, Func<string, float, F5Size> measure, ref float y, string label, F5Command command, float x, float minimumWidth)
        {
            string[] labels = { label, "已复制", "复制失败" };
            var sizes = new F5Size[3]; float width = 0, height = 30;
            for (int i = 0; i < 3; i++) { sizes[i] = measure(labels[i], .70f); width = Math.Max(width, sizes[i].Width + 24); height = Math.Max(height, sizes[i].Height + 10); }
            width = Math.Max(width, minimumWidth);
            x = Math.Min(x, 504 - width);
            var variants = new F5Element[3];
            for (int i = 0; i < 3; i++) variants[i] = new F5Element(F5ElementKind.Button, new F5Rect(x, y, width, height), labels[i], sizes[i], .70f, command);
            results[command] = variants; elements.Add(variants[0]); y += height + 10;
        }
    }
}
