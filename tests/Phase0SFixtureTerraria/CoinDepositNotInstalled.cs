using System;
using System.Collections.Generic;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Items;

// This old source-linked UI fixture has no G06 adapter. Every use fails;
// actual currency tests load the real Host and fixed .8 executable instead.
namespace JueMingR.TerrariaHost.CoinDeposit
{
    internal sealed class HostCoinDeposit
    { internal void TakeFeedback(Action<string> display) { throw new InvalidOperationException("Use the real G06 native fixture."); } }
    internal sealed class CoinPanel
    {
        private static Exception Missing() { return new InvalidOperationException("Use the real G06 native fixture."); }
        internal CoinPanel(HostCoinDeposit host) { throw Missing(); }
        internal bool NeedsBuild { get { throw Missing(); } }
        internal float Height { get { throw Missing(); } }
        internal void Execute(ItemUiControl control) { throw Missing(); }
        internal void Build(float start, float width, Func<string, float, F5Size> measure) { throw Missing(); }
        internal void Project(F5Rect view, float scroll, List<ItemUiControl> controls, List<F5Element> elements) { throw Missing(); }
        internal string Hint(float x, float y, out F5Rect rect) { throw Missing(); }
    }
}
