using System;
using System.Collections.Generic;
using JueMingR.Features.Notes;
using JueMingR.Features.Text;
using JueMingR.Platform.Llm;
using JueMingR.TerrariaHost.Input;
using Microsoft.Xna.Framework;
using Terraria;
using TextElements = JueMingR.Features.Text.TextElements;

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
            float buttonWidth = (modelWidth - 6 * (options.Length - 1)) / options.Length;
            for (int i = 0; i < options.Length; i++)
            {
                var option = options[i];
                var rect = new F5Rect(modelX + i * (buttonWidth + 6), modelY, buttonWidth, modelRow);
                if (ModelVisible(rect)) Button("model-" + option.Key, option.Label, rect, () => RunModelAction(option.Run), option.Enabled);
            }
            modelY += modelRow + 5;
        }

        private void ModelRowAction(string key, string text, float x, float y, float width, Action run, bool enabled)
        {
            var rect = new F5Rect(x, y, width, modelRow);
            if (ModelVisible(rect)) Button("model-" + key, text, rect, () => RunModelAction(run), enabled);
        }

        private void ModelChoice(string id, string value, Action run, bool selectedValue)
        {
            var rect = new F5Rect(modelX, modelY, modelWidth, modelRow);
            if (ModelVisible(rect))
            {
                var c = Button("model-" + id, FitModelText(value, modelWidth - 20), rect, () => RunModelAction(run), modelWrite == null, selectedValue);
                c.Hint = value;
            }
            modelY += modelRow + 4;
        }

        private void ModelChoose(string id, string label, string value, Action run, bool enabled = true)
        {
            var rect = new F5Rect(modelX, modelY, modelWidth, modelRow);
            if (ModelVisible(rect))
            {
                float left = Math.Min(138, modelWidth * .36f);
                Text(label, new F5Rect(modelX, modelY, left, modelRow), .65f);
                var c = Button("model-" + id, FitModelText(value, modelWidth - left - 20), new F5Rect(modelX + left, modelY, modelWidth - left, modelRow),
                    () => RunModelAction(run), enabled && modelWrite == null);
                c.Hint = value;
            }
            modelY += modelRow + 4;
        }

        private string FitModelText(string value, float width)
        {
            value = value ?? "选择";
            if (MeasureOutlined(value, .7f).Width <= width) return value;
            var boundaries = TextElements.Boundaries(value);
            int low = 0, high = boundaries.Length - 1;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                if (MeasureOutlined(value.Substring(0, boundaries[mid]) + "…", .7f).Width <= width) low = mid;
                else high = mid - 1;
            }
            return value.Substring(0, boundaries[low]) + "…";
        }

        private void RunModelAction(Action run)
        {
            if (!BlurAiInput()) return;
            try { run(); }
            catch (LlmException error) { ModelMessage(error.Message); }
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
                float left = Math.Min(138, modelWidth * .36f);
                Text(label, new F5Rect(modelX, modelY, left - 4, modelRow), .65f);
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
            catch (LlmException error) { ModelMessage(error.Message); return false; }
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
            AddModelField(id, label, status, value, setter, maximum, true, true, compact && hasSecret ? 124 : 0);
            string revealLabel = modelRevealedId == id ? "隐藏" : "显示";
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
                    ModelRowAction(id + "-reveal", revealLabel, right - 118, y, 56, Reveal, modelWrite == null);
                    ModelRowAction(id + "-clear", "清除", right - 56, y, 56, Clear, modelWrite == null);
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
            renderer.Pass(matrix, panel, () => ScrollIndicator(modelScrollView, modelOffset, modelMaximum));
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
                renderer.Text(fieldView.Value, new F5Rect(rect.X + 7, rect.Y, rect.Width - 14, rect.Height), Color.White, .65f);
        }

        private static F5Element ModelEditElement(string text, F5Size size, float row) => new F5Element(F5ElementKind.Text,
            new F5Rect(7, (row - size.Height) / 2, size.Width, size.Height), text, size, .7f, F5Command.None);
    }
}
