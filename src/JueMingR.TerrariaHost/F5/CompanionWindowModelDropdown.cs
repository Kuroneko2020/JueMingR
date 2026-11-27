using System;
using System.Linq;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class CompanionWindow
    {
        private sealed class ModelDropdownChoice
        {
            internal readonly string Id, Label;
            internal readonly Action Select;
            internal readonly bool Selected;
            internal ModelDropdownChoice(string id, string label, Action select, bool selected)
            { Id = id; Label = label; Select = select; Selected = selected; }
        }

        private ModelPicker modelDropdown;
        private string modelDropdownAnchor, modelDropdownQuery = "";
        private ModelDropdownChoice[] modelDropdownChoices = Array.Empty<ModelDropdownChoice>();
        private int modelDropdownOffset, modelDropdownVisibleRows, modelDropdownArmed = -1;
        private int modelDropdownGeneration = -1, modelDropdownSkin = -1;
        private float modelDropdownWidth, modelDropdownRow;
        private F5Rect modelDropdownPanel;
        private readonly ModelFieldView modelDropdownSearch = new ModelFieldView { Id = "dropdown-search" };
        private bool ModelDropdownOpen => modelDropdown != ModelPicker.None;

        private void OpenModelDropdown(string anchor, ModelPicker picker)
        {
            if (modelWrite != null || !BlurAiInput()) return;
            modelDropdown = picker;
            modelDropdownAnchor = "model-" + anchor + "/";
            modelDropdownQuery = "";
            HideModelSecrets();
            hint.Hide();
            RefreshModelDropdownChoices();
        }

        private void RefreshModelDropdownChoices()
        {
            modelDropdownChoices = ModelPresetChoices(modelDropdown, modelDropdownQuery).ToArray();
            MeasureModelDropdown();
            modelDropdownOffset = 0;
            modelDropdownArmed = -1;
        }

        private void MeasureModelDropdown()
        {
            modelDropdownSkin = skin;
            modelDropdownWidth = 220;
            foreach (var choice in modelDropdownChoices) modelDropdownWidth = Math.Max(modelDropdownWidth, measure(choice.Label, .7f).Width + 34);
        }

        private void CloseModelDropdown()
        {
            if (!ModelDropdownOpen) return;
            if (modelDropdownSkin != skin) { MeasureModelDropdown(); modelDropdownArmed = -1; }
            if (aiInput.Field == modelDropdownSearch.Id) aiInput.End(true);
            modelDropdown = ModelPicker.None;
            modelDropdownAnchor = null;
            modelDropdownChoices = Array.Empty<ModelDropdownChoice>();
            modelDropdownArmed = -1;
            modelDropdownPanel = modelDropdownSearch.Rect = default(F5Rect);
            modelDropdownSearch.Value = null;
        }

        private void PrepareModelDropdown()
        {
            if (!ModelDropdownOpen) return;
            var anchor = controls.Find(item => item.Key.StartsWith(modelDropdownAnchor, StringComparison.Ordinal));
            if (anchor == null || !anchor.Enabled) { CloseModelDropdown(); return; }
            bool search = modelDropdown == ModelPicker.Provider;
            float row = Math.Max(27, modelRow - 4);
            float extra = search ? row + 4 : 0;
            modelDropdownVisibleRows = Math.Min(Math.Max(1, modelDropdownChoices.Length), Math.Max(1, Math.Min(8, (int)((panel.Height - 44 - extra) / row))));
            modelDropdownOffset = Math.Min(modelDropdownOffset, Math.Max(0, modelDropdownChoices.Length - modelDropdownVisibleRows));
            float width = Math.Min(panel.Width - 24, Math.Max(modelDropdownWidth, anchor.Rect.Width));
            float height = modelDropdownVisibleRows * row + extra + 12;
            var next = F5PopupPlacement.Place(anchor.Rect.Offset(-panel.X, -panel.Y), width, height, panel.Width, panel.Height).Offset(panel.X, panel.Y);
            if (!next.Equals(modelDropdownPanel) || row != modelDropdownRow || modelDropdownGeneration != generation) modelDropdownArmed = -1;
            modelDropdownGeneration = generation;
            modelDropdownPanel = next;
            modelDropdownRow = row;
            modelDropdownSearch.Rect = search ? new F5Rect(next.X + 7, next.Y + 5, next.Width - 14, row) : default(F5Rect);
            modelDropdownSearch.Value = modelDropdownQuery.Length == 0 ? "搜索服务商" : modelDropdownQuery;
            if (aiInput.Field == modelDropdownSearch.Id) PrepareModelEditField(modelDropdownSearch);
        }

        private float ModelDropdownListTop => modelDropdownSearch.Rect.Height == 0 ? modelDropdownPanel.Y + 6 : modelDropdownSearch.Rect.Bottom + 4;

        private int ModelDropdownHit(Vector2 point)
        {
            if (!modelDropdownPanel.Contains(point.X, point.Y) || point.Y < ModelDropdownListTop) return -1;
            int row = (int)((point.Y - ModelDropdownListTop) / modelDropdownRow);
            return row < modelDropdownVisibleRows && row + modelDropdownOffset < modelDropdownChoices.Length ? row + modelDropdownOffset : -1;
        }

        private bool ProcessModelDropdown(Vector2 point, bool left, bool right, int wheel, bool escape)
        {
            if (!ModelDropdownOpen) return false;
            OwnsPointer = true;
            ConsumeWheel = wheel != 0;
            if (left) leftTail = true;
            if (right) rightTail = true;
            bool leftPress = left && !previousLeft, rightPress = right && !previousRight;
            if (leftPress) input.ClaimUiMousePress(256);
            if (rightPress) input.ClaimUiMousePress(257);
            if (escape)
            {
                input.ConsumeCancellation((int)Microsoft.Xna.Framework.Input.Keys.Escape);
                CloseModelDropdown();
            }
            else if ((leftPress || rightPress) && !modelDropdownPanel.Contains(point.X, point.Y)) CloseModelDropdown();
            else
            {
                if (wheel != 0)
                {
                    modelDropdownOffset = Math.Max(0, Math.Min(modelDropdownChoices.Length - modelDropdownVisibleRows, modelDropdownOffset - Math.Sign(wheel) * 2));
                    modelDropdownArmed = -1;
                }
                int item = ModelDropdownHit(point);
                if (leftPress)
                {
                    if (modelDropdownSearch.Rect.Width > 0 && modelDropdownSearch.Rect.Contains(point.X, point.Y))
                    {
                        aiInput.EditField(modelDropdownSearch.Id, modelDropdownQuery, false, 120, value =>
                        {
                            modelDropdownQuery = value.Trim();
                            RefreshModelDropdownChoices();
                            return true;
                        });
                    }
                    else if (wheel == 0) modelDropdownArmed = item;
                }
                if (!left && previousLeft)
                {
                    if (item >= 0 && item == modelDropdownArmed)
                    {
                        var choice = modelDropdownChoices[item];
                        CloseModelDropdown();
                        RunModelAction(choice.Select);
                    }
                    modelDropdownArmed = -1;
                }
            }
            ReleaseTails(left, right, true);
            previousLeft = left;
            previousRight = right;
            return true;
        }

        private void DrawModelDropdown()
        {
            if (!ModelDropdownOpen || modelDropdownPanel.Width <= 0) return;
            renderer.Pass(matrix, modelDropdownPanel, () =>
            {
                renderer.Panel(modelDropdownPanel);
                if (modelDropdownSearch.Rect.Width > 0)
                {
                    renderer.ItemButton(modelDropdownSearch.Rect, true, modelDropdownSearch.Rect.Contains(pointer.X, pointer.Y));
                    DrawModelField(modelDropdownSearch);
                }
                int hover = ModelDropdownHit(pointer);
                for (int row = 0; row < modelDropdownVisibleRows && row + modelDropdownOffset < modelDropdownChoices.Length; row++)
                {
                    int index = row + modelDropdownOffset;
                    var choice = modelDropdownChoices[index];
                    var bounds = new F5Rect(modelDropdownPanel.X + 6, ModelDropdownListTop + row * modelDropdownRow, modelDropdownPanel.Width - 16, modelDropdownRow);
                    if (index == hover || choice.Selected)
                        Fill(bounds, index == modelDropdownArmed ? new Color(58, 81, 133) : new Color(51, 64, 109));
                    renderer.Text(choice.Label, new F5Rect(bounds.X + 8, bounds.Y, bounds.Width - 18, bounds.Height),
                        choice.Selected ? Color.LightGreen : Color.White, .7f);
                }
                if (modelDropdownChoices.Length == 0)
                    renderer.Text("没有匹配的服务商", new F5Rect(modelDropdownPanel.X + 14, ModelDropdownListTop,
                        modelDropdownPanel.Width - 28, modelDropdownRow), Color.LightSteelBlue, .7f);
                ScrollIndicator(new F5Rect(modelDropdownPanel.X + 6, ModelDropdownListTop, modelDropdownPanel.Width - 16,
                    modelDropdownVisibleRows * modelDropdownRow), modelDropdownOffset, Math.Max(0, modelDropdownChoices.Length - modelDropdownVisibleRows));
            });
        }
    }
}
