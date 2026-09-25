using System;
using System.Collections.Generic;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Items;

namespace JueMingR.TerrariaHost.CoinDeposit
{
    // Ordinary scheduling and errors do not own any row height. Only controls
    // invalidate geometry; current reasons use the projected name on demand.
    internal sealed class CoinPanel
    {
        private static readonly F5RowDescription description = new F5RowDescription(HostCoinDeposit.ActionId,
            "将未收藏的钱币存入附近个人银行，主动取钱后暂停。");
        private readonly HostCoinDeposit host;
        private readonly List<F5Element> rows = new List<F5Element>();
        private long revision = -1;
        private bool enabled, available;
        private readonly List<F5Rect> names = new List<F5Rect>();
        internal float Height { get; private set; }
        internal bool NeedsBuild { get { return revision != host.Settings.Revision || available != host.ControlsEnabled; } }
        internal CoinPanel(HostCoinDeposit host) { this.host = host; }
        internal void Execute(ItemUiControl control) { host.SetEnabled(control.Argument != 0); }
        internal void Build(float start, float width, Func<string, float, F5Size> measure)
        {
            revision = host.Settings.Revision;
            enabled = host.Settings.Enabled; available = host.ControlsEnabled; rows.Clear();
            float y = start;
            var layout = new F5RowLayout(rows, measure);
            layout.Row(ref y, 0, width, "自动存钱", new[] { "开启", "关闭", "键" }, description: description);
            // Row already includes the gap to the next complete feature block.
            Height = y;
        }
        internal void Project(F5Rect view, float scroll, List<ItemUiControl> controls, List<F5Element> elements)
        {
            names.Clear();
            foreach (var e in rows)
            {
                if (e.Rect.Bottom <= scroll || e.Rect.Y >= scroll + view.Height) continue;
                var rect = e.Rect.Offset(view.X, view.Y - scroll);
                var projected = new F5Element(e.Kind, rect, e.Text, e.TextSize, e.TextScale, e.Command,
                    e.Kind == F5ElementKind.Hotkey ? HostCoinDeposit.ActionId : e.HotkeyTarget, e.Description, e.HintRect.Offset(view.X, view.Y - scroll));
                if (e.Kind == F5ElementKind.Button || e.Kind == F5ElementKind.Hotkey)
                    controls.Add(new ItemUiControl { Command = e.Kind == F5ElementKind.Hotkey ? ItemUiCommand.Hotkey : ItemUiCommand.Coin,
                        Argument = e.Text == "开启" ? 1 : 0, Generation = unchecked((int)revision), Rect = rect, Element = projected,
                        Enabled = available, Selected = e.Kind == F5ElementKind.Button && enabled == (e.Text == "开启") });
                else { elements.Add(projected); if (e.Description != null) names.Add(projected.HintRect); }
            }
        }
        internal string Hint(float x, float y, out F5Rect rect)
        {
            foreach (var name in names) if (name.Contains(x, y)) { rect = name; return host.NameHint ?? description.Text; }
            rect = default(F5Rect); return null;
        }
    }
}
