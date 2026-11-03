namespace JueMingR.TerrariaHost.F5
{
    // Owners select real binding semantics; a keyboard glyph alone does not
    // distinguish a feature toggle from an item-use or one-shot action.
    internal static class F5HotkeyHints
    {
        internal const string Toggle = "双击设置功能开关快捷键";
        internal const string MiningSelection = "双击设置选择挖矿区域的快捷键";
        internal static string ItemUse(string binding)
        { return System.String.IsNullOrEmpty(binding) ? "双击设置快捷键" : binding + " · 双击设置快捷键"; }
    }
}
