using System;
using System.Collections;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.GameContent.UI;
using Terraria.GameContent.UI.Chat;
using Terraria.GameContent.UI.Minimap;
using Terraria.UI.Chat;

namespace JueMingR.TerrariaHost.ItemBrowser
{
    // Actor continuation only: these bounded, completed-frame observations prove
    // a frozen point without replaying UI commands at that point. Unsupported UI
    // families remain a veto. This is deliberately not a second UI dispatcher.
    internal sealed class NativeActorUiProof
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo Messages = typeof(RemadeChatMonitor).GetField("_messages", Fields);
        private static readonly FieldInfo ShowCount = typeof(RemadeChatMonitor).GetField("_showCount", Fields);
        private static readonly FieldInfo StartLine = typeof(RemadeChatMonitor).GetField("_startChatLine", Fields);
        private static readonly FieldInfo FrameOffset = typeof(MinimapFrame).GetField("_frameOffset", Fields);
        private static readonly FieldInfo FrameTexture = typeof(MinimapFrame).GetField("_frameTexture", Fields);
        private static readonly FieldInfo ActiveFrame = typeof(MinimapFrameManager).BaseType.GetField("ActiveSelection", Fields);
        private static readonly string[] Buttons = { "_resetButton", "_zoomInButton", "_zoomOutButton" };
        private struct Region { internal Vector2 Min, Max; internal bool Inclusive; }
        private Region[] committed = new Region[256], building = new Region[256], trigger = new Region[256];
        private int count, nextCount, triggerCount;
        private bool recording, failed, available, triggerValid, chatSeen, mapSeen;
        private ulong buffsSeen;
        private object frame, triggerFrame, chatSource, triggerChatSource;
        private float mapScale, triggerMapScale;
        private bool mini, triggerMini;
        internal bool Recording { get { return recording; } }
#if DEBUG
        internal long Passes { get; private set; }
        internal long Regions { get; private set; }
#endif

        internal void Invalidate() { recording = available = triggerValid = false; }
        internal void Begin()
        {
#if DEBUG
            Passes++;
#endif
            recording = true; failed = false; nextCount = 0; chatSeen = mapSeen = false; buffsSeen = 0; chatDepth = 0;
            mini = Main.mapEnabled && Main.mapReady && Main.mapStyle == 1;
            mapScale = Main.MapScale; frame = mini ? ActiveFrame?.GetValue(Main.MinimapFrameManagerInstance) : null;
            chatSource = Main.chatMonitor;
            if (!Supported()) failed = true;
        }
        private static bool Supported()
        {
            // These families have commands/geometry outside the observations
            // below. Their presence cannot be inferred from another pointer's
            // hover, even when the inventory itself is closed.
            return !Main.playerInventory && Main.LocalPlayer.chest == -1 && Main.EquipPage != 1 &&
                !PlayerInput.UsingGamepad && !WiresUI.Open && Main.InGameUI?.IsVisible != true &&
                !Main.ingameOptionsWindow && !Main.inFancyUI && !Main.onlyDrawFancyUI && !Main.hideUI;
        }
        private void Add(Rectangle rect)
        {
            if (!recording || rect.Width <= 0 || rect.Height <= 0) return;
            if (nextCount == building.Length) { failed = true; return; }
            building[nextCount++] = new Region { Min=new Vector2(rect.Left,rect.Top), Max=new Vector2(rect.Right,rect.Bottom) };
#if DEBUG
            Regions++;
#endif
        }
        internal void Buff(int slot, int x, int y, bool ran)
        {
            if (!recording) return;
            if (!ran || slot < 0 || slot >= 64 || slot >= Main.LocalPlayer.buffType.Length) { failed = true; return; }
            int type = Main.LocalPlayer.buffType[slot];
            if (type <= 0) { failed = true; return; }
            try
            {
                var texture = Terraria.GameContent.TextureAssets.Buff[type].Value;
                // Native uses strict inequalities. Include the texture edge
                // conservatively too, including fractional UI coordinates.
                Add(new Rectangle(x, y, texture.Width, texture.Height));
                buffsSeen |= 1UL << slot;
            }
            catch { failed = true; }
        }
        private int chatDepth, expectedChatLines, observedChatLines;
        internal void BeginChat(object source)
        {
            if (!recording) return;
            chatDepth++; expectedChatLines = observedChatLines = 0;
            if (chatDepth != 1 || !ReferenceEquals(source, Main.chatMonitor) || !(source is RemadeChatMonitor)) { failed = true; return; }
            try
            {
                var messages = (IList)Messages.GetValue(source);
                int skip = (int)StartLine.GetValue(source), max = (int)ShowCount.GetValue(source);
                if (skip < 0 || max < 0 || max > 100) { failed = true; return; }
                for (int i = 0; i < messages.Count && expectedChatLines < max; i++)
                {
                    var message = (ChatMessageContainer)messages[i]; int lines = message.LineCount;
                    if (skip >= lines) { skip -= lines; continue; }
                    if (!message.Prepared || !message.CanBeShownWhenChatIsClosed) break;
                    expectedChatLines += Math.Min(max - expectedChatLines, lines - skip); skip = 0;
                }
            }
            catch { failed = true; }
        }
        internal void ChatSnippets(System.Collections.Generic.IEnumerable<PositionedSnippet> snippets, Vector2 position, Color? colorOverride, bool ran)
        {
            if (!recording || chatDepth == 0 || colorOverride.HasValue) return;
            // Remade's real foreground call receives the already materialized
            // LayoutSnippets list. Never enumerate a lazy layout, parse text,
            // invoke UniqueDraw, or execute snippet hover/click callbacks here.
            var list = snippets as System.Collections.Generic.List<PositionedSnippet>;
            if (!ran || list == null) { failed = true; return; }
            observedChatLines++;
            for (int i=0;i<list.Count;i++)
            {
                var snippet = list[i]; if (!snippet.Snippet.CheckForHover) continue;
                Vector2 min = position + snippet.Position, max = min + snippet.Size;
                if (nextCount == building.Length) { failed = true; return; }
                // Utils.Between includes all four edges, including fractional
                // coordinates. Keep the actual foreground positioned bounds.
                building[nextCount++] = new Region { Min=min, Max=max, Inclusive=true };
#if DEBUG
                Regions++;
#endif
            }
        }
        internal void Chat(object source, bool ran)
        {
            if (!recording) return;
            if (chatDepth != 1 || !ran || !ReferenceEquals(source, Main.chatMonitor) || expectedChatLines != observedChatLines) failed = true;
            else chatSeen = true;
            chatDepth = Math.Max(0,chatDepth-1);
        }
        internal void ChatFailed() { if (recording) { failed = true; chatDepth = 0; } }
        internal void Map(MinimapFrame source, bool ran)
        {
            if (!recording || !mini) return;
            if (!ran || !ReferenceEquals(source, frame) || mapScale <= 0) { failed = true; return; }
            try
            {
                Vector2 position = source.MinimapPosition, origin = position + (Vector2)FrameOffset.GetValue(source);
                var asset = (ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>)FrameTexture.GetValue(source);
                MapRect(new Rectangle((int)position.X - 6, (int)position.Y - 6, 244, 244));
                MapRect(new Rectangle((int)origin.X, (int)origin.Y, asset.Value.Width, asset.Value.Height));
                foreach (string name in Buttons)
                {
                    object button = typeof(MinimapFrame).GetField(name, Fields).GetValue(source);
                    var texture = (ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>)button.GetType().GetField("_hoverTexture", Fields).GetValue(button);
                    Vector2 size = new Vector2(texture.Value.Width, texture.Value.Height);
                    Vector2 center = origin + (Vector2)button.GetType().GetField("_position", Fields).GetValue(button) + size / 2;
                    Vector2 half = Vector2.Max(size, new Vector2(22)) / 2;
                    MapRect(new Rectangle((int)Math.Floor(center.X-half.X), (int)Math.Floor(center.Y-half.Y), (int)Math.Ceiling(half.X*2), (int)Math.Ceiling(half.Y*2)));
                }
                mapSeen = true;
            }
            catch { failed = true; }
        }
        private void MapRect(Rectangle rect)
        { Add(new Rectangle((int)Math.Floor(rect.Left*mapScale), (int)Math.Floor(rect.Top*mapScale), (int)Math.Ceiling(rect.Width*mapScale)+1, (int)Math.Ceiling(rect.Height*mapScale)+1)); }
        private bool Complete
        {
            get
            {
                if (failed || !Supported() || !chatSeen || mini && !mapSeen) return false;
                var buffs = Main.LocalPlayer.buffType;
                for (int i = 0; i < buffs.Length; i++) if (buffs[i] > 0 && (i >= 64 || (buffsSeen & (1UL << i)) == 0)) return false;
                return mini == (Main.mapEnabled && Main.mapReady && Main.mapStyle == 1) && mapScale == Main.MapScale &&
                    (!mini || ReferenceEquals(frame, ActiveFrame?.GetValue(Main.MinimapFrameManagerInstance)));
            }
        }
        internal void Freeze()
        {
            triggerValid = available && Supported(); triggerCount = count;
            Array.Copy(committed, trigger, count); triggerFrame = frame; triggerMapScale = mapScale; triggerMini = mini; triggerChatSource = chatSource;
        }
        internal bool Proves(Vector2 point)
        {
            if (!recording || !triggerValid || !Complete || triggerMini != mini || triggerMapScale != mapScale || !ReferenceEquals(triggerFrame, frame) || !ReferenceEquals(triggerChatSource,chatSource)) return false;
            return !Contains(trigger, triggerCount, point) && !Contains(building, nextCount, point);
        }
        internal bool Owns(Vector2 point)
        {
            if (!available || !Supported() || !ReferenceEquals(chatSource,Main.chatMonitor) || mini != (Main.mapEnabled && Main.mapReady && Main.mapStyle == 1) || mapScale != Main.MapScale ||
                mini && !ReferenceEquals(frame,ActiveFrame?.GetValue(Main.MinimapFrameManagerInstance)) ||
                triggerValid && (!ReferenceEquals(triggerChatSource,Main.chatMonitor) || triggerMini != mini || triggerMapScale != mapScale || !ReferenceEquals(triggerFrame,frame))) return true;
            return Contains(committed, count, point) || recording && Contains(building, nextCount, point);
        }
        private static bool Contains(Region[] regions, int length, Vector2 point)
        { for (int i=0;i<length;i++) { Region r=regions[i]; if (point.X>=r.Min.X && point.Y>=r.Min.Y && (r.Inclusive ? point.X<=r.Max.X && point.Y<=r.Max.Y : point.X<r.Max.X && point.Y<r.Max.Y)) return true; } return false; }
        internal void End(bool complete)
        {
            if (!recording) return;
            available = complete && Complete; recording = false;
            if (!available) return;
            var old = committed; committed = building; building = old; count = nextCount;
        }
    }
}
