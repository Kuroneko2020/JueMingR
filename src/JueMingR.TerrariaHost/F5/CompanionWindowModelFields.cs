using System;
using System.Collections.Generic;
using JueMingR.Features.Notes;
using JueMingR.Features.Text;
using JueMingR.Platform.Llm;
using JueMingR.TerrariaHost.Input;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class CompanionWindow
    {
        private bool ModelVisible(F5Rect rect) => rect.Y >= modelScrollView.Y && rect.Bottom <= modelScrollView.Bottom;

        private void ModelHeading(string title)
        {
            modelY += 6;
            var rect = new F5Rect(modelX, modelY, modelWidth, modelRow - 4);
            if (ModelVisible(rect)) Text(title, rect, .75f);
            modelY += modelRow;
        }

        private void ModelDisclosure(string key, string title, bool expanded, Action toggle)
        {
            string label = (expanded ? "▾  " : "›  ") + title;
            var rect = new F5Rect(modelX, modelY, ConfigurationWidth(label), modelRow);
            if (ModelVisible(rect)) Button("model-" + key, label, rect, () => RunModelAction(toggle)).TextOnly = true;
            modelY += modelRow + 3;
        }

        private void ModelParagraph(string value)
        {
            if (String.IsNullOrEmpty(value)) return;
            float lineHeight = Math.Max(17, modelRow - 10);
            if (!modelParagraphs.TryGetValue(value, out var lines))
            {
                if (modelParagraphs.Count >= 96) modelParagraphs.Clear();
                lines = ChatLines(new NotesTextLayout(value, modelWidth - 2, ChatGlyphWidth), lineHeight);
                modelParagraphs[value] = lines;
            }
            foreach (var line in lines)
            {
                F5Element element = Move(line, modelX, modelY);
                if (ModelVisible(element.Rect)) labels.Add(element);
            }
            modelY += lines.Length * lineHeight + 5;
        }

        private void ModelButtonRow(params (string Key, string Label, Action Run, bool Enabled)[] options)
        {
            ModelActions(options, modelScrollView);
        }

        private void ModelActions((string Key, string Label, Action Run, bool Enabled)[] options, F5Rect clip, bool alignLastRight = false)
        {
            var commands = new (string, string, Action, bool)[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                var option = options[i];
                commands[i] = ("model-" + option.Key, option.Label, () => RunModelAction(option.Run), option.Enabled);
            }
            modelY = BuildAiActions(modelX, modelY, modelWidth, clip, commands, alignLastRight) + 3;
        }

        private void ModelRowAction(string key, string text, float x, float y, float width, Action run, bool enabled)
        {
            var rect = new F5Rect(x, y, width, modelRow);
            if (ModelVisible(rect)) Button("model-" + key, text, rect, () => RunModelAction(run), enabled);
        }

        private void ModelChoice(string id, string value, Action run, bool selectedValue)
        {
            float width = Math.Min(modelWidth, Math.Max(160, ConfigurationWidth(value)));
            var rect = new F5Rect(modelX, modelY, width, modelRow);
            if (ModelVisible(rect))
            {
                var c = Button("model-" + id, "", rect, () => RunModelAction(run), modelWrite == null, selectedValue);
                c.Element = null;
                Text(value, new F5Rect(rect.X + 8, rect.Y, rect.Width - 16, rect.Height), .7f);
                c.Hint = value;
            }
            modelY += modelRow + 4;
        }

        private void ModelChoose(string id, string label, string value, Action run, bool enabled = true)
        {
            var rect = new F5Rect(modelX, modelY, modelWidth, modelRow);
            if (ModelVisible(rect))
            {
                float left = ModelLabelWidth;
                Text(label, new F5Rect(modelX, modelY, left, modelRow), .7f);
                F5Rect field = new F5Rect(modelX + left, modelY, Math.Min(330, modelWidth - left), modelRow);
                var c = Button("model-" + id, "", field, () => RunModelAction(run), enabled && modelWrite == null);
                c.Element = null;
                modelFields.Add(new ModelFieldView { Id = id, Rect = field, Value = value });
                c.Hint = value;
            }
            modelY += modelRow + 4;
        }

        private float ModelLabelWidth => Math.Min(146, modelWidth * .35f);

        private void ModelDropdownField(string id, string label, string value, ModelPicker picker, bool hasChoices = true)
        {
            if (!hasChoices)
            {
                var rect = new F5Rect(modelX, modelY, modelWidth, modelRow - 4);
                if (ModelVisible(rect))
                {
                    Text(label, new F5Rect(modelX, modelY, ModelLabelWidth, rect.Height), .7f);
                    Text(value, new F5Rect(modelX + ModelLabelWidth + 7, modelY, modelWidth - ModelLabelWidth - 7, rect.Height), .7f);
                }
                modelY += modelRow;
                return;
            }
            int count = controls.Count;
            ModelChoose(id, label, value, () => OpenModelDropdown(id, picker));
            if (controls.Count != count)
            {
                controls[count].ModelSelector = true;
                modelFields[modelFields.Count - 1].Dropdown = true;
            }
        }

        private void RunModelAction(Action run)
        {
            if (!BlurAiInput()) return;
            try { run(); }
            catch (LlmException error) { ModelFailure("操作未完成", error.Message); }
        }

        private void ModelField(string id, string label, string value, Action<string> setter, int maximum,
            string empty = "点击填写", bool invalidatesProbe = true, float trailing = 0)
        {
            AddModelField(id, label, String.IsNullOrEmpty(value) ? empty : value, value, setter, maximum, false, invalidatesProbe, trailing);
        }

        private void AddModelField(string id, string label, string display, string value, Action<string> write,
            int maximum, bool secret, bool invalidatesProbe, float trailing = 0)
        {
            var rect = new F5Rect(modelX, modelY, modelWidth, modelRow);
            if (ModelVisible(rect))
            {
                float left = ModelLabelWidth;
                Text(label, new F5Rect(modelX, modelY, left - 4, modelRow), .7f);
                var fieldView = new ModelFieldView { Id = id, Value = display, Rect = new F5Rect(modelX + left, modelY, modelWidth - left - trailing, modelRow), Secret = secret };
                var c = Button("model-field-" + id, "", fieldView.Rect, () =>
                {
                    HideModelSecrets();
                    aiInput.EditField(id, value, secret, maximum, text => WriteModelField(id, text, write, invalidatesProbe));
                }, modelWrite == null);
                c.Element = null;
                modelFields.Add(fieldView);
            }
            modelY += modelRow + 4;
        }

        private bool WriteModelField(string id, string value, Action<string> write, bool invalidatesProbe)
        {
            string oldScope = modelDraft.CredentialScope();
            try { write(value); }
            catch (LlmException error) { ModelFailure("字段尚未保存", error.Message); return false; }
            if (invalidatesProbe) ModelDraftChanged(oldScope, id == "api-key" || id == "proxy-secret" || id == "extra-secret-headers");
            else contentInvalidated = true;
            return true;
        }

        private void ModelDraftChanged(string oldScope, bool secretEdit = false)
        {
            RetireModelRead();
            modelStatus = null;
            HideModelSecrets();
            if (!secretEdit && oldScope != modelDraft.CredentialScope() &&
                (modelDraft.SecretVersion != Guid.Empty || modelDraft.ApiKey != null || modelDraft.SecretHeaders != null || modelDraft.ProxyCredentials != null))
            {
                // The prior credential scope may still exist on disk. Do not
                // grant this draft permission to read or send those credentials.
                modelDraft.ApiKey = modelDraft.SecretHeaders = modelDraft.ProxyCredentials = null;
                modelDraft.HasStoredKey = false;
                modelDraft.SecretScope = null;
                modelDraft.SecretVersion = Guid.Empty;
                ModelMessage("连接范围已改变。请填写此入口的凭据。");
            }
            contentInvalidated = true;
        }

        private void ModelSecretField(string id, string label, string status, string value, Action<string> setter,
            int maximum, Func<string> reveal, bool compact = false)
        {
            bool hasSecret = !String.IsNullOrEmpty(value) || value == null && modelDraft.HasStoredKey;
            float y = modelY;
            string revealLabel = modelRevealedId == id ? "隐藏" : "显示";
            float revealWidth = ConfigurationWidth(revealLabel), clearWidth = ConfigurationWidth("清除");
            AddModelField(id, label, status, value, setter, maximum, true, true, compact && hasSecret ? revealWidth + clearWidth + 12 : 0);
            void Reveal()
            {
                if (modelRevealedId == id) HideModelSecrets();
                else
                {
                    HideModelSecrets();
                    string raw = reveal();
                    if (String.IsNullOrEmpty(raw)) { ModelMessage("尚未填写" + label + "。"); return; }
                    modelRevealedId = id;
                    modelRevealedValue = raw;
                }
                Invalidate();
            }
            void Clear()
            {
                setter("");
                ModelDraftChanged(modelDraft.CredentialScope(), true);
                ModelMessage("保存连接后将清除" + label + "。");
            }
            if (compact)
            {
                if (hasSecret)
                {
                    float right = modelX + modelWidth;
                    ModelRowAction(id + "-reveal", revealLabel, right - clearWidth - revealWidth - 6, y, revealWidth, Reveal, modelWrite == null);
                    ModelRowAction(id + "-clear", "清除", right - clearWidth, y, clearWidth, Clear, modelWrite == null);
                }
            }
            else ModelButtonRow((id + "-reveal", revealLabel + label, Reveal, modelWrite == null),
                (id + "-clear", "清除" + label, Clear, modelWrite == null));
            if (modelRevealedId == id) BuildModelRevealedValue();
        }

        private void BuildModelRevealedValue()
        {
            float lineHeight = Math.Max(17, modelRow - 10);
            if (modelRevealLines == null || modelRevealWidth != modelWidth)
            {
                // The player explicitly opened this view. The lines stay in a
                // short-lived projection, separate from general label caches.
                var layout = new NotesTextLayout(modelRevealedValue, modelWidth - 14, ChatGlyphWidth);
                modelRevealLines = ChatLines(layout, lineHeight);
                modelRevealWidth = modelWidth;
            }
            foreach (var line in modelRevealLines)
            {
                var element = Move(line, modelX + 7, modelY);
                if (ModelVisible(element.Rect)) modelRevealLabels.Add(element);
            }
            modelY += modelRevealLines.Length * lineHeight + 5;
        }

        private void HideModelSecrets()
        {
            modelRevealedId = modelRevealedValue = null;
            modelRevealLines = null;
            modelRevealLabels.Clear();
            modelRevealWidth = 0;
        }

        private void DropModelEditView()
        {
            modelPresentedEditor = null;
            modelComposition = null;
            modelEditView.Text = "";
        }

        private void PrepareModelInput()
        {
            if (aiInput.Editor != null)
            {
                var active = modelFields.Find(fieldView => fieldView.Id == aiInput.Field);
                if (active != null) PrepareModelEditField(active);
            }
        }

        private void PrepareModelEditField(ModelFieldView active)
        {
            TextEditBuffer buffer = aiInput.Editor;
            string composition = aiInput.Composition;
            if (aiInput.Secret) composition = new string('•', composition.Length);
            if (ReferenceEquals(modelPresentedEditor, buffer) && modelEditRevision == buffer.Revision && modelCaretRevision == buffer.CaretRevision &&
                modelEditWidth == active.Rect.Width && modelComposition == composition) return;
            // Inactive fields retain the status label; the active masked view
            // constructs its own buffer and never puts raw input in a cache.
            var shown = buffer;
            if (aiInput.Secret)
            {
                shown = new TextEditBuffer(new string('•', buffer.Text.Length), true, 8192, 8192, "");
                shown.MoveTo(buffer.Anchor);
                shown.MoveTo(buffer.Caret, true);
            }
            modelEditView.Prepare(shown, composition, active.Rect.Width - 14, MeasureOutlined);
            modelPresentedEditor = buffer;
            modelEditRevision = buffer.Revision;
            modelCaretRevision = buffer.CaretRevision;
            modelEditWidth = active.Rect.Width;
            modelComposition = composition;
        }

        private void DrawModel()
        {
            renderer.Pass(matrix, modelScrollView, () =>
            {
                foreach (var fieldView in modelFields) DrawModelField(fieldView);
                foreach (var item in modelRevealLabels) renderer.Label(item, Color.Gold);
            });
            renderer.Pass(matrix, panel, () =>
            {
                ScrollIndicator(modelScrollView, modelOffset, modelMaximum);
                if (modelFooterView.Width > 0) renderer.Divider(new F5Rect(modelFooterView.X + 5, modelFooterView.Y, modelFooterView.Width - 10, 1));
            });
        }

        private void DrawModelField(ModelFieldView fieldView)
        {
            F5Rect rect = fieldView.Rect;
            if (aiInput.Field == fieldView.Id && modelPresentedEditor != null)
            {
                var edit = modelEditView;
                if (edit.SelectionRight > edit.SelectionLeft)
                    Fill(new F5Rect(rect.X + 7 + edit.SelectionLeft, rect.Y + 4, edit.SelectionRight - edit.SelectionLeft, rect.Height - 8), new Color(62, 101, 170));
                renderer.Label(Move(ModelEditElement(edit.Text, edit.Size, rect.Height), rect.X, rect.Y));
                Fill(new F5Rect(rect.X + 7 + edit.Caret, rect.Y + 4, 1, rect.Height - 8), Color.White);
                if (aiInput.OwnsTextToken) Main.instance?.SetIMEPanelAnchor(new Vector2(rect.X + 7 + edit.Caret, rect.Bottom + 32), 0);
                if (!String.IsNullOrEmpty(aiInput.Error)) renderer.Text(aiInput.Error,
                    new F5Rect(rect.X + 7, rect.Bottom - 15, rect.Width - 14, 15), Color.Gold, .56f);
            }
            else
            {
                renderer.Text(fieldView.Value, new F5Rect(rect.X + 7, rect.Y, rect.Width - (fieldView.Dropdown ? 34 : 14), rect.Height), Color.White, .7f);
                if (fieldView.Dropdown) renderer.Text("▾", new F5Rect(rect.Right - 23, rect.Y, 20, rect.Height), Color.LightSteelBlue, .7f);
            }
        }

        private static F5Element ModelEditElement(string text, F5Size size, float row) => new F5Element(F5ElementKind.Text,
            new F5Rect(7, (row - size.Height) / 2, size.Width, size.Height), text, size, .7f, F5Command.None);
    }
}
