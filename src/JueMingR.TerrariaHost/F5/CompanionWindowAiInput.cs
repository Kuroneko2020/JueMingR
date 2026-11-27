using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.Notes;
using JueMingR.Features.Text;
using JueMingR.TerrariaHost.Input;
using JueMingR.TerrariaHost.Notes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class CompanionWindow
    {
        private const int ChatPage = 2, ModelPage = 3;
        private readonly CompanionAiInput aiInput;
        private readonly List<F5Element> aiActionLayout = new List<F5Element>();

        private float BuildAiActions(float x, float y, float width, F5Rect clip,
            (string Key, string Label, Action Run, bool Enabled)[] actions, bool alignLastRight = false)
        {
            aiActionLayout.Clear();
            var layout = new F5RowLayout(aiActionLayout, measure);
            layout.Buttons(ref y, x, width, actions.Select(item => item.Label).ToArray());
            for (int i = 0; i < actions.Length; i++)
            {
                var action = actions[i];
                F5Rect rect = aiActionLayout[i].Rect;
                if (alignLastRight && i == actions.Length - 1) rect = rect.Offset(x + width - rect.Right, 0);
                if (rect.Y >= clip.Y && rect.Bottom <= clip.Bottom)
                    Button(action.Key, action.Label, rect, action.Run, action.Enabled);
            }
            return y;
        }

        private void DrawPageButtons(float row)
        {
            string[] names = { "背包", "配置", "聊天", "模型" };
            string[] keys = { "inventory", "configuration", "chat", "model" };
            for (int value = 0; value < names.Length; value++)
            {
                int target = value;
                Button(keys[value], names[value], new F5Rect(panel.Right - 306 + value * 66, panel.Y + 4, 60, row),
                    () => SelectPage(target), true, page == target);
            }
        }

        private bool BlurAiInput() => aiInput.Editor == null || aiInput.Chat ? RetireChatInput() : aiInput.CommitField();
        private bool RetireChatInput() { aiInput.End(false); return true; }

        private void LeaveAiPage()
        {
            if (page == ChatPage) { RetireChatInput(); chatConfirmation = 0; }
            if (page == ModelPage)
            {
                CloseModelDropdown();
                if (!BlurAiInput()) aiInput.End(true);
                CancelModelRequest();
                modelDiscoveryPickerPending = false;
                HideModelSecrets();
                modelConfirmDelete = false;
            }
        }

        private void CloseAiPresentation()
        {
            LeaveAiPage();
            RetireChatTarget();
            aiInput.End(true);
            chatBlocks.Clear();
            chatEntries = null;
            chatHistoryWidth = 0;
            chatStream = null;
            chatStreamLayout = null;
            chatView = null;
        }

        private void ObserveAiPage()
        {
            if (page == ChatPage) ObserveChat();
            else ObserveModel();
        }

        private void BuildAiPage(float row)
        {
            if (page == ChatPage) BuildChat(row);
            else BuildModel(row);
        }

        private void TranslateAiPage(float x, float y)
        {
            if (page == ChatPage)
            {
                chatHistoryView = chatHistoryView.Offset(x, y);
                chatDraftRect = chatDraftRect.Offset(x, y);
                chatTaskHeading = chatTaskHeading.Offset(x, y);
            }
            else
            {
                modelScrollView = modelScrollView.Offset(x, y);
                modelFooterView = modelFooterView.Offset(x, y);
                foreach (var fieldView in modelFields) fieldView.Rect = fieldView.Rect.Offset(x, y);
                for (int i = 0; i < modelRevealLabels.Count; i++) modelRevealLabels[i] = Move(modelRevealLabels[i], x, y);
            }
        }

        private void ScrollAiPage(Vector2 point, int wheel)
        {
            if (page == ChatPage && chatHistoryView.Contains(point.X, point.Y))
            {
                if (chatTaskDetails)
                {
                    chatTaskOffset = Math.Max(0, Math.Min(chatTaskMaximum, chatTaskOffset - Math.Sign(wheel) * 48));
                    return;
                }
                chatOffset = Math.Max(0, Math.Min(chatMaximum, chatOffset - Math.Sign(wheel) * 48));
                bool atLatest = chatOffset >= chatMaximum;
                if (followChat != atLatest) Invalidate();
                followChat = atLatest;
            }
            else if (page == ModelPage && modelScrollView.Contains(point.X, point.Y))
            {
                if (!BlurAiInput()) return;
                modelOffset = Math.Max(0, Math.Min(modelMaximum, modelOffset - Math.Sign(wheel) * 48));
            }
        }

        private void PrepareAiInput()
        {
            if (page == ChatPage) PrepareChatPresentation();
            else { PrepareModelInput(); PrepareModelDropdown(); }
        }

        private void DrawAiPage()
        {
            if (page == ChatPage) DrawChat();
            else DrawModel();
        }

        private void PublishAiImeAnchor(TextEditBuffer editor, F5Rect field, F5Rect caret)
        {
            var currentUi = Main.UIScaleMatrix;
            if (preparedGeneration != generation || matrix != currentUi || !aiInput.CanPresentEditor(editor) || Main.instance == null) return;
            // This runs inside the editor's actual render pass (including the
            // dropdown search). The active scissor includes the caller's clip.
            var clip = F5HintLayout.Intersect(field, F5ControlRenderer.LogicalClip(Main.spriteBatch.GraphicsDevice.ScissorRectangle, matrix));
            if (F5ControlRenderer.TryImeAnchor(caret, clip, matrix, currentUi, out var anchor)) Main.instance.SetIMEPanelAnchor(anchor, 0);
        }

        private sealed class CompanionAiInput : ITextEditSession, INotesClipboard
        {
            private readonly CompanionWindow window;
            private readonly TextEditInput input;
            private readonly NotesClipboard clipboard;
            private Func<string, bool> apply;
            internal string Field { get; private set; }
            internal bool Chat { get; private set; }
            internal bool Secret { get; private set; }
            public TextEditBuffer Editor { get; private set; }
            internal bool OwnsTextToken => input.OwnsTextToken;
            internal bool CanPresentEditor(TextEditBuffer editor) => input.CanPresentEditor(window.input, editor);
            internal string Composition => input.HasCurrentEditorLease ? input.Composition : "";
            internal string Error => input.Error ?? Editor?.Error;

            internal CompanionAiInput(CompanionWindow window)
            {
                this.window = window;
                clipboard = new NotesClipboard(() => Main.instance.Window.Handle);
                input = new TextEditInput(this, this);
            }

            internal void BeforeSample(bool active) => input.BeforeSample(active);
            internal void AfterSample(bool active, KeyboardState keys, bool focused) =>
                input.AfterSampleForCompanion(active, Chat ? window.chatDraftLayout : null, keys, focused, window.input, Chat);

            internal void EditChat(TextEditBuffer draft)
            {
                if (Chat && ReferenceEquals(Editor, draft)) return;
                End(false);
                Chat = true;
                Editor = draft;
                input.PrepareEditor();
            }

            internal bool EditField(string id, string value, bool secret, int maximum, Func<string, bool> commit)
            {
                if (Editor != null && Field == id) return true;
                if (!CommitField()) return false;
                Field = id;
                Secret = secret;
                apply = commit;
                Editor = new TextEditBuffer(value ?? "", true, maximum, maximum, "此处的内容过长。");
                input.PrepareEditor();
                return true;
            }

            // Model controls commit into the current draft before a subsequent
            // click can save it, test it or choose another preset.
            internal bool CommitField()
            {
                if (Editor == null) return true;
                if (Chat) { End(false); return true; }
                input.FinishComposition(false);
                if (input.HasComposition || Editor.Dirty && !apply(Editor.Text)) return false;
                End(false);
                return true;
            }

            internal void End(bool cancel)
            {
                input.Release(cancel);
                Editor = null;
                apply = null;
                Chat = Secret = false;
                Field = null;
                input.ForgetRetiredDraft();
                window.DropModelEditView();
            }

            internal bool CompleteChatComposition()
            {
                input.FinishComposition(false);
                return !input.HasComposition;
            }

            public bool RequestFinish() => Chat ? window.SendChat() : CommitField();
            public void CancelEdit() => End(true);
            public void PreserveUncommittedInput(TextEditBuffer value) { }
            public bool TryCopy(string value) => !Secret && clipboard.TryCopy(value);
            public bool TryPaste(out string value) => clipboard.TryPaste(out value);
        }
    }
}
