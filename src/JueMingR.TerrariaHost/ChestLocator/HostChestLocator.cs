using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.Announcements;
using JueMingR.Features.WorldObjectText;
using JueMingR.Platform.WorldObjectText;
using JueMingR.Platform.WorldTargets;
using JueMingR.TerrariaHost.ItemBrowser;
using JueMingR.TerrariaHost.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.ChestLocator
{
    internal sealed class ChestMatch
    {
        internal Chest Chest; internal WorldObject Object; internal long Revision, ObservedTick;
        internal readonly List<KeyValuePair<int, long>> Counts = new List<KeyValuePair<int, long>>();
        internal readonly Dictionary<int, int> Slots = new Dictionary<int, int>();
        internal string Name, Label, WorldLabel;
    }
    internal sealed class HostChestLocator : IDisposable
    {
        internal readonly ChestReceiveObserver Receiver = new ChestReceiveObserver();
        private readonly WorldTileObservation world;
        private readonly HostItemKnowledge knowledge;
        internal readonly List<ChestMatch> Results = new List<ChestMatch>();
        private readonly HashSet<long> visited = new HashSet<long>();
        private HashSet<int> types;
        private WorldTargetView view;
        private Vector2 playerCenter;
        private int x, y, candidates, unknown;
        private long tick, started, generation;
        private long localMutation;
        private bool scanning;
        internal string Status { get; private set; } = "提交后查找附近箱子；未知库存不当作空箱";
        internal long Revision { get; private set; }
        internal IReadOnlyList<string> Details { get; private set; } = new string[0];
        internal Action PositiveResult { get; set; }
#if DEBUG
        internal long CellReads, SlotReads;
#endif
        internal HostChestLocator(WorldTileObservation world, HostItemKnowledge knowledge) { this.world = world; this.knowledge = knowledge; }
        internal void Submit(string query)
        {
            Clear();
            // The 25th type rejects the request before view/tile/content access.
            if (knowledge.Native.Catalog == null) { Status = "物品目录尚未就绪"; return; }
            int[] matches = knowledge.Native.Catalog.Search(query, 0, false);
            if (matches.Length > 24) { Status = "匹配超过 24 种，请缩小名称或输入 ID"; return; }
            if (matches.Length == 0) { Status = "没有匹配物品，输入已保留"; return; }
            if (Main.netMode == 1 && (!Receiver.Ready || !Receiver.Trusted || Receiver.Pending))
            { Status = "箱内容尚未完成可信接收；请等待正常同步后重试"; return; }
            if (!world.TryView(96, out view)) { Status = "当前无法读取附近世界"; return; }
            types = new HashSet<int>(matches); playerCenter = Main.LocalPlayer.Center;
            x = view.X; y = view.Y; started = tick; scanning = true; generation = Receiver.Generation;
            Status = "正在查找附近箱子…";
        }
        internal void Clear()
        { scanning = false; types = null; Results.Clear(); visited.Clear(); candidates = unknown = 0; Details = new string[0]; Revision++; Status = "已清除定位结果"; }
        internal void Update(long currentTick)
        {
            Receiver.Tick = currentTick; Receiver.Update();
            if (currentTick < tick || Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active || Receiver.Generation != generation || Receiver.LocalMutation != localMutation ||
                (scanning || Results.Count > 0) && (currentTick - started > 3600 || Main.LocalPlayer.chest >= 0)) Clear();
            tick = currentTick; generation = Receiver.Generation; localMutation = Receiver.LocalMutation;
            // Snapshot validation is identity/revision only. It never reads slots.
            for (int i = Results.Count - 1; i >= 0; i--)
            {
                ChestMatch result = Results[i]; var shape = result.Object;
                WorldObject actual;
                if (result.Chest.index >= Main.chest.Length || !ReferenceEquals(Main.chest[result.Chest.index], result.Chest) ||
                    !WorldObjectResolver.TryResolve(shape.TileX, shape.TileY, world.Read(shape.TileX, shape.TileY), world.Read, out actual) || actual.Type != shape.Type || actual.Style != shape.Style ||
                    Main.netMode == 1 && Receiver.Knowledge.Revision(result.Chest.index) != result.Revision) { Results.RemoveAt(i); Status = "部分定位证据已变化，保留 " + Results.Count + " 个有效结果；可重新定位"; PrepareDetails(); }
            }
            if (!scanning) return;
            for (int budget = 0; budget < 512 && scanning; budget++)
            {
                if (x >= view.X + view.Width) { Finish(false); break; }
                int cx = x, cy = y; if (++y >= view.Y + view.Height) { y = view.Y; x++; }
                if (Vector2.DistanceSquared(new Vector2(cx * 16 + 8, cy * 16 + 8), playerCenter) > 1696f * 1696f) continue;
                var tile = world.Read(cx, cy);
#if DEBUG
                CellReads++;
#endif
                if (!tile.Readable) { unknown++; continue; }
                WorldObject value;
                if (!WorldObjectResolver.TryResolve(cx, cy, tile, world.Read, out value) || value.Kind != WorldObjectKind.Chest || !visited.Add(value.Key)) continue;
                int index = Chest.FindChest(value.TileX, value.TileY);
                if (index < 0 || index >= Main.chest.Length) continue;
                Chest chest = Main.chest[index]; if (chest == null || chest.x != value.TileX || chest.y != value.TileY) continue;
                if (++candidates > 64) { Finish(true); break; }
                if (chest.item == null || chest.maxItems <= 0 || chest.maxItems > chest.item.Length ||
                    Main.netMode == 1 && (!Receiver.Trusted || Receiver.Pending || !Receiver.Knowledge.Complete(index, chest, chest.x, chest.y))) { unknown++; continue; }
                var counts = new Dictionary<int, long>(); var slots = new Dictionary<int, int>();
                for (int slot = 0; slot < chest.maxItems; slot++)
                {
                    Item item = chest.item[slot];
#if DEBUG
                    SlotReads++;
#endif
                    if (item == null || item.type <= 0 || item.stack <= 0 || !types.Contains(item.type)) continue;
                    long count; counts.TryGetValue(item.type, out count); counts[item.type] = count + item.stack;
                    int occupied; slots.TryGetValue(item.type, out occupied); slots[item.type] = occupied + 1;
                }
                if (counts.Count == 0) continue;
                if (Results.Count == 24) { Finish(true); break; }
                string name = WorldObjectResolver.ContainerName(value, SafeChatText.CleanName(chest.name, 40), NativeName);
                var result = new ChestMatch { Chest = chest, Object = value, Revision = Receiver.Knowledge.Revision(index),
                    ObservedTick = Main.netMode == 1 ? Receiver.Knowledge.Tick(index) : tick, Name = name };
                foreach (var pair in counts.OrderBy(p => p.Key)) { result.Counts.Add(pair); result.Slots[pair.Key] = slots[pair.Key]; }
                result.Label = name + " · " + counts.Values.Sum() + " 件 / " + slots.Values.Sum() + " 槽 / " + counts.Count + " 种";
                result.WorldLabel = counts.Values.Sum() + "个";
                Results.Add(result);
            }
        }
        private void Finish(bool truncated)
        {
            scanning = false;
            Status = "已确认 " + Results.Count + " 箱 / " + Results.Sum(r => r.Slots.Values.Sum()) + " 槽 / " + Results.Sum(r => r.Counts.Sum(c => c.Value)) + " 件" + (unknown > 0 ? "；部分库存或区块未知" : "") + (truncated ? "；达到查询上限，结果未完整列出" : "") +
                (Main.netMode == 1 ? "；最近接收的数据" : "");
            PrepareDetails(); if (Results.Count > 0) PositiveResult?.Invoke();
        }
        private void PrepareDetails()
        {
            var lines = new List<string> { Status };
            foreach (var result in Results)
            {
                lines.Add(result.Label + "（" + result.Object.TileX + "," + result.Object.TileY + "）");
                foreach (var pair in result.Counts) lines.Add("  " + (knowledge.Native.Catalog?.Find(pair.Key)?.Name ?? "#" + pair.Key) + " ×" + pair.Value + " / " + result.Slots[pair.Key] + " 槽");
                lines.Add(Main.netMode == 1 ? "最近单槽接收距本次查询 " + Math.Max(0, tick - result.ObservedTick) + " 个游戏更新；各槽非整体刷新" : "本次本机读取快照；不持续监控库存");
            }
            Details = lines.AsReadOnly(); Revision++;
        }
        private static string NativeName(ContainerNameFamily family, int style)
        {
            if (family == ContainerNameFamily.Item) return Lang.GetItemNameValue(style);
            var names = family == ContainerNameFamily.Chest ? Lang.chestType : family == ContainerNameFamily.Chest2 ? Lang.chestType2 : Lang.dresserType;
            return names != null && style >= 0 && style < names.Length ? names[style].Value : "容器";
        }
        internal void Draw()
        {
            if (Results.Count == 0 || !Rendering.WorldPresentation.CanDraw || Main.spriteBatch == null || TextureAssets.MagicPixel?.Value == null || FontAssets.MouseText?.Value == null) return;
            float pulse = .22f + .10f * (float)Math.Sin(tick * .09), gravity = Main.LocalPlayer.gravDir;
            foreach (var result in Results)
            {
                var value = result.Object; Vector2 point = WorldTargets.WorldTargetWorldLayer.Screen(value.TileX * 16, (value.TileY + (gravity == -1 ? 2 : 0)) * 16, gravity);
                Rectangle box = new Rectangle((int)point.X, (int)point.Y, value.Width * 16, 32);
                Matrix zoom = Main.GameViewMatrix.ZoomMatrix;
                Vector2 near = Vector2.Transform(new Vector2(box.Left, box.Top - 24), zoom), far = Vector2.Transform(new Vector2(box.Right, box.Bottom), zoom);
                if (far.X < 0 || near.X > Main.screenWidth || far.Y < 0 || near.Y > Main.screenHeight) continue;
                Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, box, new Rectangle(0, 0, 1, 1), Color.LightGreen * pulse);
                // Four bounded edges share the fill pulse; stable rendering never reads inventory slots.
                Color border = Color.LightGreen * (.7f + pulse);
                Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(box.X, box.Y, box.Width, 2), border);
                Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(box.X, box.Bottom - 2, box.Width, 2), border);
                Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(box.X, box.Y, 2, box.Height), border);
                Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(box.Right - 2, box.Y, 2, box.Height), border);
                Utils.DrawBorderString(Main.spriteBatch, result.WorldLabel, new Vector2(box.Center.X, box.Y - 22), Color.LightGreen, .7f, .5f);
            }
        }
        public void Dispose() { Receiver.Dispose(); Clear(); }
    }
}
