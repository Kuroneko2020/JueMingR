using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.Companions;
using JueMingR.TerrariaHost.Hotkeys;
using JueMingR.TerrariaHost.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class CompanionWindow
    {
        private sealed class SupplyCard
        {
            internal CompanionSupplyCatalog.Option Option;
            internal bool Allowed, Partial, Complete;
            internal int Stock = -1;
            internal string Detail;
            internal F5Rect Rect;
        }

        private sealed class SupplyPane
        {
            internal readonly List<SupplyCard> Cards = new List<SupplyCard>();
            internal F5Rect View;
            internal int FirstRow, Maximum;
        }

        private readonly SupplyPane supplyAvailable = new SupplyPane(), supplyChosen = new SupplyPane();
        private readonly List<SupplyCard> supplyCells = new List<SupplyCard>();
        private readonly PreparedItemIcons supplyIcons = new PreparedItemIcons();
        private readonly Dictionary<int, int> supplyStock = new Dictionary<int, int>();
        private Control supplyAnchor;
        private CompanionSetting? supplySetting;
        private CompanionSupplyKind supplyKind;
        private IReadOnlyList<CompanionSupplyCatalog.Option> supplyOptions;
        private Guid supplyProfile;
        private long supplyEpoch, supplySession, supplyRevision;
        private bool supplyLayoutDirty, supplyLeftGesture, supplyRightGesture;
        private int[] supplySaved;
        private SupplyCard supplyPress;
        private bool supplyClosePress;
        private F5Rect supplyPressedRect, supplyPanel, supplyClose, supplyTitleRect, supplyExplanationRect, supplyFooter;
        private F5Rect supplyAnchorRect;
        private Matrix supplyMatrix;
        private Vector2 supplyScreen;
        private int supplySkin;
        private string supplyTitle, supplyExplanation, supplySummary;
        private uint nextSupplyStock;
        internal bool SupplyPopupVisible => supplySetting.HasValue;

        private void ToggleSupplyPopup(CompanionProfile profile, CompanionSetting setting, Control anchor)
        {
            bool same = supplySetting == setting;
            CloseSupplyPopup();
            if (same) return;
            supplySetting = setting;
            supplyKind = setting == CompanionSetting.Life ? CompanionSupplyKind.Life :
                setting == CompanionSetting.Mana ? CompanionSupplyKind.Mana :
                setting == CompanionSetting.Growth ? CompanionSupplyKind.Growth : CompanionSupplyKind.Buff;
            supplyAnchor = anchor;
            supplyProfile = profile.Id; supplyEpoch = profile.DataEpoch; supplySession = host.CreationSessionToken;
            supplyTitle = CompanionHotkeys.Name(setting) + " · 配置";
            supplyExplanation = supplyKind == CompanionSupplyKind.Buff ? "点击左栏添加，右栏移除" :
                supplyKind == CompanionSupplyKind.Growth ? "已勾选的成长物品允许自动使用" : "已勾选的药品允许自动使用";
            supplyLayoutDirty = true;
            hint.Hide();
        }

        private void CloseSupplyPopup()
        {
            supplySetting = null; supplyAnchor = null; supplyOptions = null; supplySaved = null;
            supplyPress = null; supplyClosePress = false;
            supplyLeftGesture = supplyRightGesture = false;
            supplyAvailable.Cards.Clear(); supplyChosen.Cards.Clear(); supplyCells.Clear(); supplyIcons.Clear(); supplyStock.Clear();
            supplyAvailable.FirstRow = supplyChosen.FirstRow = 0;
        }

        private void PrepareSupplyPopup(bool resources)
        {
            var profile = CurrentProfile();
            if (!Visible || !ready || page != 1 || supplyAnchor == null || profile == null || profile.Id != supplyProfile ||
                profile.DataEpoch != supplyEpoch || supplySession != host.CreationSessionToken)
            { CloseSupplyPopup(); return; }

            var options = CompanionSupplyCatalog.Options(supplyKind);
            if (supplyRevision != profile.Revision || !ReferenceEquals(options, supplyOptions))
            {
                var behavior = profile.Behavior;
                int[] saved = SupplyTypes(behavior, supplyKind);
                if (!ReferenceEquals(options, supplyOptions) || !saved.SequenceEqual(supplySaved))
                {
                    supplyOptions = options; supplySaved = saved;
                    BuildSupplyCards();
                    supplyLayoutDirty = true;
                }
                supplyRevision = profile.Revision;
            }
            if (supplyMatrix != matrix || supplyScreen != screen || supplySkin != skin || !supplyAnchorRect.Equals(supplyAnchor.Rect))
            {
                supplyMatrix = matrix; supplyScreen = screen; supplySkin = skin; supplyAnchorRect = supplyAnchor.Rect;
                supplyPress = null; supplyClosePress = false;
                supplyLayoutDirty = true;
            }
            bool layout = supplyLayoutDirty;
            if (layout) { LayoutSupplyPopup(); supplyLayoutDirty = false; }
            if (resources) supplyIcons.Prepare();
            if (layout || unchecked((int)(Main.GameUpdateCount - nextSupplyStock)) >= 0)
            {
                nextSupplyStock = Main.GameUpdateCount + 20;
                RefreshSupplyStock(host.GetBody(selected));
            }
        }

        private void BuildSupplyCards()
        {
            supplyAvailable.Cards.Clear(); supplyChosen.Cards.Clear();
            var saved = new HashSet<int>(supplySaved);
            bool exclusions = supplyKind == CompanionSupplyKind.Life || supplyKind == CompanionSupplyKind.Mana;
            int selectedCount = 0;
            foreach (var option in supplyOptions)
            {
                int matches = option.Types.Count(saved.Contains);
                int allowed = exclusions ? option.Types.Length - matches : matches;
                var card = new SupplyCard { Option = option, Allowed = allowed == option.Types.Length, Partial = allowed > 0 && allowed < option.Types.Length };
                if (card.Allowed) selectedCount++;
                (supplyKind == CompanionSupplyKind.Buff && card.Allowed ? supplyChosen : supplyAvailable).Cards.Add(card);
            }
            supplySummary = "已选 " + selectedCount + " / " + supplyOptions.Count;
        }

        private void LayoutSupplyPopup()
        {
            bool split = supplyKind == CompanionSupplyKind.Buff;
            float width = Math.Min(split ? 466 : 368, screen.X / matrix.M11 - 24);
            float paneWidth = split ? (width - 24) / 2 : width - 16;
            int columns = ItemsLayout.Columns(paneWidth, ItemsLayout.CandidateWidth);
            float header = Math.Max(38, measure(supplyTitle, .7f).Height + 12);
            float section = split ? 28 : 0;
            float step = ItemsLayout.CandidateSize + ItemsLayout.Gap;
            float fixedHeight = header + section + 32;
            int maxRows = Math.Max(1, Math.Min(3, (int)((screen.Y / matrix.M22 - 24 - fixedHeight) / step)));
            int totalRows = (Math.Max(supplyAvailable.Cards.Count, supplyChosen.Cards.Count) + columns - 1) / columns;
            int rows = Math.Min(maxRows, Math.Max(1, totalRows));
            float gridHeight = rows * step - ItemsLayout.Gap;
            supplyPanel = F5PopupPlacement.Place(supplyAnchorRect, width, fixedHeight + gridHeight, screen.X / matrix.M11, screen.Y / matrix.M22);
            float x = supplyPanel.X + 8, y = supplyPanel.Y;
            supplyClose = new F5Rect(supplyPanel.Right - 36, y + (header - 28) / 2, 28, 28);
            supplyTitleRect = new F5Rect(x, y, measure(supplyTitle, .7f).Width + 4, header);
            supplyExplanationRect = new F5Rect(supplyTitleRect.Right + 8, y, supplyClose.X - supplyTitleRect.Right - 12, header);
            y += header + section;
            supplyCells.Clear(); supplyIcons.BeginVisibleSet();
            LayoutSupplyPane(supplyAvailable, new F5Rect(x, y, paneWidth, gridHeight), columns, rows);
            if (split) LayoutSupplyPane(supplyChosen, new F5Rect(x + paneWidth + 8, y, paneWidth, gridHeight), columns, rows);
            supplyIcons.EndVisibleSet();
            supplyFooter = new F5Rect(x, y + gridHeight + 3, width - 16, 25);
        }

        private void LayoutSupplyPane(SupplyPane pane, F5Rect view, int columns, int rows)
        {
            pane.View = view;
            pane.Maximum = Math.Max(0, (pane.Cards.Count + columns - 1) / columns - rows);
            pane.FirstRow = Math.Max(0, Math.Min(pane.FirstRow, pane.Maximum));
            int first = pane.FirstRow * columns;
            for (int i = first; i < Math.Min(pane.Cards.Count, first + rows * columns); i++)
            {
                var card = pane.Cards[i];
                card.Rect = new F5Rect(view.X + 4 + i % columns * (ItemsLayout.CandidateWidth + ItemsLayout.Gap),
                    view.Y + (i / columns - pane.FirstRow) * (ItemsLayout.CandidateSize + ItemsLayout.Gap),
                    ItemsLayout.CandidateWidth, ItemsLayout.CandidateSize);
                supplyIcons.AddVisible(card.Option.Icon.type);
                supplyCells.Add(card);
            }
        }

        private void RefreshSupplyStock(Player body)
        {
            // Read the usable inventory/bag once for the visible menu. Stock is
            // feedback only: an empty stack never removes a lasting permission.
            CompanionSupplyCatalog.CountStock(body, supplyKind, supplyStock);
            foreach (var card in supplyCells)
            {
                int count = 0;
                foreach (int type in card.Option.Types)
                    if (supplyStock.TryGetValue(type, out int quantity)) count += quantity;
                bool complete = supplyKind == CompanionSupplyKind.Growth && CompanionSupplyCatalog.GrowthComplete(body, card.Option.Icon.type);
                if (card.Stock == count && card.Complete == complete) continue;
                card.Stock = count; card.Complete = complete;
                string permission = card.Partial ? "部分允许" : card.Allowed ? "已允许" : "未允许";
                card.Detail = card.Option.Name + " · " + permission + " · " + (complete ? "已完成" : count == 0 ? "缺货" : "持有 " + count);
            }
        }

        private SupplyCard SupplyCardAt(Vector2 point)
        {
            foreach (var card in supplyCells) if (card.Rect.Contains(point.X, point.Y)) return card;
            return null;
        }

        private bool ProcessSupplyPopup(Vector2 point, bool left, bool right, int wheel, bool resources)
        {
            if (!SupplyPopupVisible) return false;
            if (input.Hotkeys.IsNew((int)Keys.Escape))
            {
                input.ConsumeCancellation((int)Keys.Escape);
                CloseSupplyPopup();
            }
            else
            {
                bool inside = supplyPanel.Contains(point.X, point.Y);
                if (dragging || !inside && !supplyLeftGesture && !supplyRightGesture) return false;
                OwnsPointer = true;
                if (left) leftTail = true;
                if (right) rightTail = true;
                if (inside && right && !previousRight) supplyRightGesture = true;
                var hit = SupplyCardAt(point);
                if (left && !previousLeft)
                {
                    supplyLeftGesture = true; supplyPress = hit; supplyPressedRect = hit?.Rect ?? default(F5Rect);
                    supplyClosePress = supplyClose.Contains(point.X, point.Y);
                }
                if (wheel != 0)
                {
                    ConsumeWheel = true;
                    var pane = supplyKind == CompanionSupplyKind.Buff && point.X >= supplyChosen.View.X ? supplyChosen : supplyAvailable;
                    pane.FirstRow = Math.Max(0, Math.Min(pane.Maximum, pane.FirstRow - Math.Sign(wheel)));
                    supplyPress = null; supplyClosePress = false;
                    supplyLayoutDirty = true;
                }
                else if (!left && previousLeft && supplyLeftGesture)
                {
                    if (supplyClosePress && supplyClose.Contains(point.X, point.Y)) CloseSupplyPopup();
                    // Add/remove is a captured direction. A profile save can
                    // replace every card while the pointer is held; only the
                    // same option, position and permission may accept release.
                    else if (supplyPress != null && hit != null && ReferenceEquals(hit.Option, supplyPress.Option) &&
                        hit.Allowed == supplyPress.Allowed && hit.Partial == supplyPress.Partial && hit.Rect.Equals(supplyPressedRect))
                    {
                        bool exclusions = supplyKind == CompanionSupplyKind.Life || supplyKind == CompanionSupplyKind.Mana;
                        EditBehavior(b => UpdateSupplyTypes(b, supplyKind, hit.Option.Types, exclusions == hit.Allowed));
                    }
                }
                if (!left) { supplyLeftGesture = false; supplyPress = null; supplyClosePress = false; }
                if (!right) supplyRightGesture = false;
                if (SupplyPopupVisible && (supplyLayoutDirty || contentInvalidated)) PrepareSupplyPopup(resources);
            }
            armedKey = null; hint.Hide();
            ReleaseTails(left, right, true); previousLeft = left; previousRight = right;
            return true;
        }

        private void DrawSupplyPopup()
        {
            var hover = SupplyCardAt(pointer);
            renderer.Pass(matrix, supplyPanel, () =>
            {
                renderer.Panel(supplyPanel);
                renderer.Text(supplyTitle, supplyTitleRect, Color.White, .7f);
                renderer.Text(supplyExplanation, supplyExplanationRect, Color.White, .6f);
                renderer.Cross(supplyClose, true);
                if (supplyKind == CompanionSupplyKind.Buff)
                {
                    DrawSupplyPane(supplyAvailable, "可选增益", "暂无可选增益");
                    DrawSupplyPane(supplyChosen, "已选增益", "名单为空");
                }
                else DrawSupplyScroll(supplyAvailable);
                foreach (var card in supplyCells)
                {
                    renderer.ItemButton(card.Rect, true, ReferenceEquals(card, hover));
                    renderer.PreparedItem(card.Option.Icon.type, supplyIcons.Get(card.Option.Icon.type), ItemsLayout.IconBounds(card.Rect, true));
                    if (card.Partial) Fill(new F5Rect(card.Rect.Right - 13, card.Rect.Y + 6, 9, 2), Color.Gold);
                    else if (card.Allowed && supplyKind == CompanionSupplyKind.Buff && card.Stock == 0)
                        renderer.Cross(new F5Rect(card.Rect.Right - 16, card.Rect.Y, 16, 16), true);
                    else if (card.Allowed) renderer.Selection(card.Rect);
                }
                renderer.Text(hover?.Detail ?? supplySummary, supplyFooter, Color.White, .65f);
            });
        }

        private void DrawSupplyPane(SupplyPane pane, string title, string empty)
        {
            var view = pane.View;
            var bounds = new F5Rect(view.X, view.Y - 27, view.Width, view.Height + 28);
            Fill(bounds, Color.Black * .18f); DrawFrame(bounds, Color.White * .23f);
            renderer.Text(title, new F5Rect(view.X + 6, bounds.Y, view.Width - 12, 26), Color.White, .65f);
            if (pane.Cards.Count == 0) renderer.Text(empty, new F5Rect(view.X + 6, view.Y, view.Width - 12, 32), Color.LightGray, .65f);
            DrawSupplyScroll(pane);
        }

        private void DrawSupplyScroll(SupplyPane pane)
        {
            var view = pane.View;
            ScrollIndicator(new F5Rect(view.X, view.Y + 2, view.Width - 7, view.Height - 4), pane.FirstRow, pane.Maximum);
        }
    }
}
