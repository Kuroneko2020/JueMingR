using System;
using System.Collections.Generic;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Items;

namespace JueMingR.TerrariaHost.CoinDeposit
{
    // A small projection inside the existing Items page. Layout changes only
    // when published status/preferences or the shared viewport actually change.
    internal sealed class CoinPanel
    {
        private static readonly F5RowDescription description = new F5RowDescription(HostCoinDeposit.ActionId,
            "将未收藏钱币存入附近个人银行。主动从箱子或银行取钱后先留在身上；离开全部银行范围，或关闭再开启后恢复。空账户首存仅限存钱罐。");
        private readonly HostCoinDeposit host;
        private readonly List<F5Element> rows = new List<F5Element>();
        private long revision = -1;
        private string status, detail;
        private bool enabled, available;
        private F5Rect statusRect, projectedStatus;
        internal float Height { get; private set; }
        internal bool NeedsBuild { get { return revision != host.Settings.Revision || status != host.Status || detail != (host.Settings.Message ?? host.Detail) || available != host.ControlsEnabled; } }
        internal CoinPanel(HostCoinDeposit host) { this.host = host; }
        internal void Execute(ItemUiControl control) { host.SetEnabled(control.Argument != 0); }
        internal void Build(float start, float width, Func<string, float, F5Size> measure)
        {
            revision = host.Settings.Revision; status = host.Status; detail = host.Settings.Message ?? host.Detail;
            enabled = host.Settings.Enabled; available = host.ControlsEnabled; rows.Clear();
            float y = start + 6;
            var layout = new F5RowLayout(rows, measure);
            layout.Row(ref y, 0, width, "自动存钱", new[] { "开启", "关闭", "键" }, description: description);
            float top = y;
            layout.TextLines(status, 8, ref y, width - 16, .65f);
            statusRect = new F5Rect(8, top, width - 16, y - top);
            Height = y + 5;
        }
        internal void Project(F5Rect view, float scroll, List<ItemUiControl> controls, List<F5Element> elements)
        {
            projectedStatus = statusRect.Offset(view.X, view.Y - scroll);
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
                else elements.Add(projected);
            }
        }
        internal string Hint(float x, float y, out F5Rect rect)
        { rect = projectedStatus; return projectedStatus.Contains(x, y) ? detail : null; }
    }
}
