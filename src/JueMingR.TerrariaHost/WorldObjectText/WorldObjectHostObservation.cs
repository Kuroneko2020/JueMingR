using System;
using System.Collections.Generic;
using JueMingR.Features.WorldObjectText;
using JueMingR.Platform.WorldObjectText;
using JueMingR.Platform.WorldTargets;
using JueMingR.TerrariaHost.World;
using Terraria;

namespace JueMingR.TerrariaHost.WorldObjectText
{
    internal sealed class WorldObjectHostObservation : IWorldObjectSource
    {
        private readonly WorldTileObservation world;
        private readonly SlotIndex signs = new SlotIndex();
        private readonly Func<ContainerNameFamily, int, string> nativeName = NativeName;
        private readonly Dictionary<int, NameCheck> names = new Dictionary<int, NameCheck>();
        private readonly Queue<int> nameOrder = new Queue<int>();
        private int nameBudget;
        internal WorldObjectHostObservation(WorldTileObservation world) { this.world = world; }
        public bool HasDetector { get { return Main.LocalPlayer != null && Main.LocalPlayer.accOreFinder; } }
        internal void EndSession() { signs.Clear(); names.Clear(); nameOrder.Clear(); }
        public bool TryBegin(bool chestNames, bool signText, out WorldObjectView view)
        {
            view = default(WorldObjectView); WorldTargetView visible, discovery;
            // Padding covers centered multiline labels at the largest scale.
            // It never changes which geometrical objects count as visible first.
            if (!world.TryView(0, out visible) || !world.TryView(480, out discovery)) return false;
            view = new WorldObjectView { Visible = visible, Discovery = discovery, PlayerX = Main.LocalPlayer.Center.X, PlayerY = Main.LocalPlayer.Center.Y };
            nameBudget = chestNames ? 512 : 0;
            if (signText) ReadSigns(); // One shared S pass for signs and tombstones.
            return true;
        }
        public WorldTargetTile Read(int x, int y) { return world.Read(x, y); }
        public bool TryText(WorldObject value, out string text)
        {
            int slot;
            if (value.Kind == WorldObjectKind.Chest)
            {
                // Fixed .8 FindChest is a direct dictionary lookup. Do not scan
                // C or reconstruct a second global container-name index.
                slot = Chest.FindChest(value.TileX, value.TileY);
                if (slot >= 0 && Main.chest != null && slot < Main.chest.Length)
                {
                    var chest = Main.chest[slot];
                    if (chest != null && chest.x == value.TileX && chest.y == value.TileY && !String.IsNullOrEmpty(chest.name))
                    {
                        NameCheck check;
                        if (!names.TryGetValue(slot, out check))
                        { if (names.Count == 512) names.Remove(nameOrder.Dequeue()); check = new NameCheck(); names.Add(slot, check); nameOrder.Enqueue(slot); }
                        if (!ReferenceEquals(check.Text, chest.name)) { check.Text = chest.name; check.Cursor = 0; check.Complete = check.HasInk = false; }
                        // A long leading/all-whitespace name cannot hide an L-wide
                        // IsNullOrWhiteSpace inside every warm TryText. Each source
                        // epoch has one shared budget; unchanged results are scalars.
                        while (!check.Complete && nameBudget > 0)
                        {
                            nameBudget--;
                            if (!Char.IsWhiteSpace(check.Text[check.Cursor++])) check.HasInk = check.Complete = true;
                            if (check.Cursor == check.Text.Length) check.Complete = true;
                        }
                        if (check.HasInk) { text = check.Text; return true; }
                    }
                }
                text = WorldObjectResolver.ContainerName(value, null, nativeName); return true;
            }
            text = null;
            if (!signs.Map.TryGetValue(value.Key, out slot) || Main.sign == null || slot >= Main.sign.Length) return false;
            var sign = Main.sign[slot]; if (sign == null || sign.x != value.TileX || sign.y != value.TileY) return false;
            text = sign.text; return true;
        }
        private void ReadSigns()
        {
            var values = Main.sign; if (values == null) { signs.Clear(); return; }
            signs.Use(values, values.Length);
            for (int end = Math.Min(values.Length, signs.Cursor + 512); signs.Cursor < end; signs.Cursor++)
            { var value = values[signs.Cursor]; signs.Put(signs.Cursor, value == null ? -1 : WorldObject.PositionKey(value.x, value.y)); }
            if (signs.Cursor == values.Length) signs.Cursor = 0;
        }
        private static string NativeName(ContainerNameFamily family, int style)
        {
            if (family == ContainerNameFamily.Item) return Lang.GetItemNameValue(style);
            var table = family == ContainerNameFamily.Chest ? Lang.chestType : family == ContainerNameFamily.Chest2 ? Lang.chestType2 : Lang.dresserType;
            string value = table != null && style >= 0 && style < table.Length ? table[style]?.Value : null;
            return String.IsNullOrEmpty(value) ? family == ContainerNameFamily.Dresser ? "衣柜（名称信息未就绪）" : "宝箱（名称信息未就绪）" : value;
        }
        private sealed class SlotIndex
        {
            internal readonly Dictionary<long, int> Map = new Dictionary<long, int>();
            private object source; private long[] keys; private bool[] valid;
            internal int Cursor;
            internal void Clear() { source = null; keys = null; valid = null; Map.Clear(); Cursor = 0; }
            internal void Use(object value, int length)
            { if (ReferenceEquals(source, value)) return; Clear(); source = value; keys = new long[length]; valid = new bool[length]; }
            internal void Put(int index, long key)
            {
                int old; if (valid[index] && keys[index] != key && Map.TryGetValue(keys[index], out old) && old == index) Map.Remove(keys[index]);
                valid[index] = key >= 0; keys[index] = key; if (key >= 0) Map[key] = index;
            }
        }
        private sealed class NameCheck { internal string Text; internal int Cursor; internal bool Complete, HasInk; }
    }
}
