using System;
using System.Collections.Generic;
using System.Text;
using JueMingR.Features.Notes;
using JueMingR.Features.Text;
using JueMingR.Platform.Llm;
using JueMingR.TerrariaHost.Companions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using TextElements = JueMingR.Features.Text.TextElements;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class CompanionWindow
    {
        private const float ChatScale = .65f;
        private sealed class ChatDraft
        {
            internal TextEditBuffer Text = NewChatDraft();
            internal float Offset;
            internal bool FollowLatest = true;
        }
        private sealed class ChatBlock
        {
            internal LlmHistoryEntry Entry;
            internal F5Element[] Lines;
            internal float Top, Bottom;
        }
        private readonly Dictionary<Tuple<Guid, Guid>, ChatDraft> chatDrafts = new Dictionary<Tuple<Guid, Guid>, ChatDraft>();
        private readonly List<ChatBlock> chatBlocks = new List<ChatBlock>();
        private Guid chatOwner, chatProfile;
        private ChatDraft currentChatDraft;
        private CompanionChatView chatView;
        private IReadOnlyList<LlmHistoryEntry> chatEntries;
        private string chatStream;
        private NotesTextLayout chatDraftLayout, chatStreamLayout;
        private F5Element[] chatStreamLines = Array.Empty<F5Element>();
        private F5Element[] chatDraftLines = Array.Empty<F5Element>();
        private F5Rect chatHistoryView, chatDraftRect;
        private float chatOffset, chatMaximum, chatDraftOffset, chatDraftWidth, chatHistoryWidth, chatLineHeight;
        private int chatHistorySkin = -1, chatConfirmation;
        private bool followChat = true, chatInputShift;

        private static TextEditBuffer NewChatDraft() => new TextEditBuffer("", false, 8192, 8192, "消息过长。");

        private void ObserveChat()
        {
            var profile = CurrentProfile();
            if ((profile?.OwnerId ?? Guid.Empty) != chatOwner || (profile?.Id ?? Guid.Empty) != chatProfile)
            {
                RetireChatTarget();
                if (profile != null)
                {
                    chatOwner = profile.OwnerId;
                    chatProfile = profile.Id;
                    var key = Tuple.Create(chatOwner, chatProfile);
                    if (!chatDrafts.TryGetValue(key, out currentChatDraft)) chatDrafts.Add(key, currentChatDraft = new ChatDraft());
                    chatOffset = currentChatDraft.Offset;
                    followChat = currentChatDraft.FollowLatest;
                }
                Invalidate();
            }
            if (profile == null) return;
            var updated = host.GetChat(chatOwner, chatProfile);
            if (chatView == null || updated == null || updated.IsRunning != chatView.IsRunning || updated.CanSend != chatView.CanSend ||
                updated.CanStopActivity != chatView.CanStopActivity || updated.Connection != chatView.Connection ||
                updated.IsActive != chatView.IsActive || updated.Status != chatView.Status ||
                (updated.Entries.Count == 0) != (chatView.Entries.Count == 0)) contentInvalidated = true;
            chatView = updated;
        }

        private void RetireChatTarget()
        {
            if (aiInput.Chat) aiInput.End(false);
            if (currentChatDraft != null)
            {
                currentChatDraft.Offset = chatOffset;
                currentChatDraft.FollowLatest = followChat;
                if (currentChatDraft.Text.Text.Length == 0 && followChat) chatDrafts.Remove(Tuple.Create(chatOwner, chatProfile));
            }
            currentChatDraft = null;
            chatOwner = chatProfile = Guid.Empty;
            chatConfirmation = 0;
            chatView = null;
            chatDraftLayout = null;
            chatDraftLines = Array.Empty<F5Element>();
            chatDraftOffset = 0;
            chatStream = null;
            chatStreamLayout = null;
            chatStreamLines = Array.Empty<F5Element>();
            chatEntries = null;
            chatBlocks.Clear();
        }

        private void BuildChat(float row)
        {
            ObserveChat();
            chatHistoryView = chatDraftRect = default(F5Rect);
            var profile = CurrentProfile();
            if (profile == null)
            {
                Text("选择或创建队友后即可聊天。", new F5Rect(bodyView.X + 8, bodyView.Y, bodyView.Width - 16, row));
                Button("chat-open-model", "前往模型页配置", new F5Rect(bodyView.X + 8, bodyView.Y + row + 4, ConfigurationWidth("前往模型页配置"), row), () => SelectPage(ModelPage));
                return;
            }

            float x = bodyView.X + 8, width = bodyView.Width - 16, top = bodyView.Y;
            float line = Math.Max(20, row - 10);
            string role = chatView?.IsActive == true ? "已激活" : "未激活 · 仅聊天";
            Text(profile.Name + " · " + role, new F5Rect(x, top, width, line));
            top += line;
            Text(chatView?.Connection ?? "尚未配置模型连接", new F5Rect(x, top, width, line), ChatScale);
            top += line;
            Text(chatView?.Status ?? "可以在模型页配置直连。", new F5Rect(x, top, width, line), ChatScale);
            top += line + 3;
            float buttonWidth = (width - 24) / 5;
            ChatButton("copy", "复制", x, top, buttonWidth, row, CopyChat, chatView?.Entries.Count > 0);
            ChatButton("clear", "清空", x + buttonWidth + 6, top, buttonWidth, row, () => ConfirmChat(1), chatView?.Entries.Count > 0);
            ChatButton("restart", "新上下文", x + 2 * (buttonWidth + 6), top, buttonWidth, row, () => ConfirmChat(2), chatView != null);
            ChatButton("stop-reply", "停止回复", x + 3 * (buttonWidth + 6), top, buttonWidth, row,
                () => host.StopChatReply(chatOwner, chatProfile), chatView?.IsRunning == true);
            ChatButton("stop-activity", "停止活动", x + 4 * (buttonWidth + 6), top, buttonWidth, row,
                () => { if (!host.StopChatActivity(chatOwner, chatProfile)) ShowNotice("当前没有可停止的活动。"); }, chatView?.CanStopActivity == true);
            top += row + 3;

            float sendWidth = ConfigurationWidth("回到最新");
            float bottom = bodyView.Bottom - 3;
            if (chatConfirmation != 0)
            {
                string question = chatConfirmation == 1 ? "清空聊天记录？当前活动将继续。" : "开启新上下文？可见聊天记录将保留。";
                Text(question, new F5Rect(x, top, width - 116, row), ChatScale);
                ChatButton("confirm", "确认", x + width - 112, top, 52, row, () =>
                {
                    bool done = chatConfirmation == 1 ? host.ClearChat(chatOwner, chatProfile) : host.RestartChatContext(chatOwner, chatProfile);
                    ConfirmChat(0);
                    if (!done) ShowNotice("队友状态已变化，请重新选择。");
                });
                ChatButton("cancel", "取消", x + width - 54, top, 54, row, () => ConfirmChat(0));
                top += row + 4;
            }
            float fieldHeight = Math.Max(line + 6, Math.Min(72, (bottom - top) * .38f));
            float editTop = bottom - fieldHeight - row - 4;
            chatHistoryView = new F5Rect(x, top, width, Math.Max(0, editTop - top - 5));
            chatDraftRect = new F5Rect(x, editTop, width, fieldHeight);
            var fieldButton = Button("chat-draft", "", chatDraftRect, FocusChatDraft);
            fieldButton.Element = null;
            float actionTop = bottom - row;
            ChatButton("latest", "回到最新", x, actionTop, sendWidth, row, () => { followChat = true; chatOffset = chatMaximum; });
            if (!String.IsNullOrWhiteSpace(chatView?.Status))
                ChatButton("copy-status", "复制状态", x + sendWidth + 6, actionTop, sendWidth, row, () =>
                {
                    if (!String.IsNullOrWhiteSpace(chatView?.Status))
                        ShowNotice(aiInput.TryCopy(chatView.Status) ? "已复制当前状态。" : "无法复制当前状态。");
                });
            if (chatView?.CanSend == true)
                ChatButton("send", "发送", x + width - sendWidth, actionTop, sendWidth, row, () => SendChat(), blur: false);
            else
            {
                float configureWidth = ConfigurationWidth("前往模型页配置");
                ChatButton("configure", "前往模型页配置", x + width - configureWidth, actionTop, configureWidth, row, () => SelectPage(ModelPage));
            }
        }

        private void ChatButton(string key, string text, float x, float top, float width, float row, Action action,
            bool enabled = true, bool blur = true)
        {
            Button("chat-" + key, text, new F5Rect(x, top, width, row), () => { if (!blur || BlurAiInput()) action(); }, enabled);
        }

        private void ConfirmChat(int operation)
        {
            chatConfirmation = operation;
            Invalidate();
        }

        private void FocusChatDraft()
        {
            if (currentChatDraft == null) return;
            aiInput.EditChat(currentChatDraft.Text);
            if (chatDraftLayout == null) return;
            float x = pointer.X - chatDraftRect.X - 7;
            int line = (int)((pointer.Y - chatDraftRect.Y - 3 + chatDraftOffset) / chatLineHeight);
            int offset = chatDraftLayout.Hit(x, line);
            if (offset >= 0) currentChatDraft.Text.MoveTo(offset, chatInputShift);
        }

        private bool SendChat()
        {
            if (page != ChatPage || currentChatDraft == null || !aiInput.CompleteChatComposition()) return false;
            string text = currentChatDraft.Text.Text;
            if (String.IsNullOrWhiteSpace(text)) return false;
            if (!host.SendChat(chatOwner, chatProfile, text, out string reason))
            {
                ShowNotice(reason ?? "消息未发送。草稿已保留。");
                return false;
            }
            // Retire this text only after the runtime admits the frozen owner,
            // target and message. An old stream never gets this draft back.
            currentChatDraft.Text.SelectAll();
            currentChatDraft.Text.DeleteSelection();
            currentChatDraft.Text.AcceptBaseline("");
            chatDraftOffset = 0;
            followChat = true;
            return true;
        }

        private void CopyChat()
        {
            if (chatView == null) return;
            var result = new StringBuilder();
            foreach (var entry in chatView.Entries)
                result.Append(ChatEntryName(entry.Kind)).Append(' ').Append(entry.Time.ToLocalTime().ToString("HH:mm")).AppendLine().AppendLine(entry.Text);
            if (chatView.DraftReply?.Length > 0) result.AppendLine(chatView.DraftReply);
            if (result.Length > 0) ShowNotice(aiInput.TryCopy(result.ToString()) ? "已复制聊天记录。" : "无法复制聊天记录。");
        }

        private static string ChatEntryName(string kind)
        {
            switch (kind)
            {
                case "user": return "你";
                case "assistant": return "队友";
                case "action": return "游戏动作";
                case "status": return "活动状态";
                default: return "消息";
            }
        }

        private float ChatGlyphWidth(string value)
        {
            var font = FontAssets.MouseText?.Value;
            return font == null ? MeasureOutlined(value, ChatScale).Width : font.MeasureString(value).X * ChatScale;
        }

        private void PrepareChatPresentation()
        {
            if (currentChatDraft == null || chatDraftRect.Width <= 16) return;
            chatLineHeight = Math.Max(21, (FontAssets.MouseText?.Value?.LineSpacing ?? rowHeight) * ChatScale + 2);
            float historyWidth = Math.Max(1, chatHistoryView.Width - 20);
            if (chatHistoryWidth != historyWidth || chatHistorySkin != skin)
            {
                chatBlocks.Clear(); chatEntries = null; chatStream = null; chatStreamLayout = null;
                chatStreamLines = chatDraftLines = Array.Empty<F5Element>();
                chatDraftLayout = null; chatHistoryWidth = historyWidth; chatHistorySkin = skin;
            }
            var entries = chatView?.Entries;
            if (!ReferenceEquals(chatEntries, entries)) UpdateChatHistory(entries);
            string stream = chatView?.DraftReply ?? "";
            if (stream != chatStream)
            {
                // The completed cards keep their layout. A stream normally only
                // lengthens its last lines, so keep the previous metrics prefix.
                int prefix = chatStream != null && stream.StartsWith(chatStream, StringComparison.Ordinal) ? chatStream.Length : 0;
                chatStreamLayout = stream.Length == 0 ? null : new NotesTextLayout(stream, historyWidth, ChatGlyphWidth,
                    Array.AsReadOnly(TextElements.Boundaries(stream)), chatStreamLayout, prefix);
                chatStream = stream;
                if (chatStreamLayout != null) while (!chatStreamLayout.Complete) chatStreamLayout.Continue(8192);
                chatStreamLines = ChatLines(chatStreamLayout, chatLineHeight, chatStreamLines);
            }
            float below = chatBlocks.Count == 0 ? 0 : chatBlocks[chatBlocks.Count - 1].Bottom;
            if (chatStreamLines.Length > 0) below += (chatStreamLines.Length + 1) * chatLineHeight + 11;
            chatMaximum = Math.Max(0, below + 5 - chatHistoryView.Height);
            chatOffset = followChat ? chatMaximum : Math.Min(chatMaximum, chatOffset);

            TextEditBuffer draft = currentChatDraft.Text;
            float draftWidth = chatDraftRect.Width - 16;
            if (chatDraftLayout == null || chatDraftWidth != draftWidth || !ReferenceEquals(chatDraftLayout.Text, draft.Text))
            {
                chatDraftLayout = new NotesTextLayout(draft.Text, draftWidth, ChatGlyphWidth, draft.Boundaries, chatDraftLayout, draft.LastChangeStart);
                while (!chatDraftLayout.Complete) chatDraftLayout.Continue(8192);
                chatDraftLines = ChatLines(chatDraftLayout, chatLineHeight, chatDraftLines);
                chatDraftWidth = draftWidth;
            }
            int caretLine = chatDraftLayout.LineOf(draft.Caret);
            chatDraftOffset = Math.Max(0, Math.Min(caretLine * chatLineHeight,
                Math.Max(chatDraftOffset, (caretLine + 1) * chatLineHeight - chatDraftRect.Height + 7)));
        }

        private void UpdateChatHistory(IReadOnlyList<LlmHistoryEntry> entries)
        {
            if (entries == null) { chatBlocks.Clear(); chatEntries = null; return; }
            int keep = 0;
            while (keep < entries.Count && keep < chatBlocks.Count && ReferenceEquals(entries[keep], chatBlocks[keep].Entry)) keep++;
            if (chatBlocks.Count > keep) chatBlocks.RemoveRange(keep, chatBlocks.Count - keep);
            float top = keep == 0 ? 5 : chatBlocks[keep - 1].Bottom;
            for (int i = keep; i < entries.Count; i++)
            {
                var entry = entries[i];
                var layout = new NotesTextLayout(entry.Text ?? "", chatHistoryWidth, ChatGlyphWidth);
                var lines = ChatLines(layout, chatLineHeight);
                float bottom = top + (lines.Length + 1) * chatLineHeight + 11;
                chatBlocks.Add(new ChatBlock { Entry = entry, Lines = lines, Top = top, Bottom = bottom });
                top = bottom;
            }
            chatEntries = entries;
        }

        private F5Element[] ChatLines(NotesTextLayout layout, float lineHeight, F5Element[] previous = null)
        {
            if (layout == null) return Array.Empty<F5Element>();
            var lines = new F5Element[layout.Lines.Count];
            for (int i = 0; i < lines.Length; i++)
            {
                var line = layout.Lines[i];
                F5Element prior = previous != null && i < previous.Length ? previous[i] : null;
                if (prior != null && prior.Rect.Height == lineHeight && prior.Text.Length == line.End - line.Start &&
                    String.CompareOrdinal(prior.Text, 0, layout.Text, line.Start, prior.Text.Length) == 0)
                {
                    lines[i] = prior;
                    continue;
                }
                string value = layout.Text.Substring(line.Start, line.End - line.Start);
                var size = MeasureOutlined(value, ChatScale);
                lines[i] = new F5Element(F5ElementKind.Text, new F5Rect(0, i * lineHeight, size.Width, lineHeight),
                    value, size, ChatScale, F5Command.None);
            }
            return lines;
        }

        private void DrawChat()
        {
            if (currentChatDraft == null || chatDraftRect.Width <= 16) return;
            if (chatHistoryView.Height > 0) renderer.Pass(matrix, chatHistoryView, DrawChatHistory);
            renderer.Pass(matrix, chatDraftRect, DrawChatDraft);
            renderer.Pass(matrix, panel, () => ScrollIndicator(chatHistoryView, (int)chatOffset, (int)chatMaximum));
        }

        private void DrawChatHistory()
        {
            Fill(chatHistoryView, new Color(23, 32, 54, 220));
            int low = 0, high = chatBlocks.Count;
            while (low < high)
            {
                int mid = (low + high) / 2;
                if (chatBlocks[mid].Bottom < chatOffset) low = mid + 1;
                else high = mid;
            }
            float end = chatOffset + chatHistoryView.Height;
            for (int i = low; i < chatBlocks.Count && chatBlocks[i].Top < end; i++)
            {
                var block = chatBlocks[i];
                bool action = block.Entry.Kind == "action" || block.Entry.Kind == "status";
                DrawChatBlock(block.Top, block.Bottom, ChatEntryName(block.Entry.Kind) + " · " + block.Entry.Time.ToLocalTime().ToString("HH:mm"), block.Lines,
                    action ? new Color(70, 66, 48, 210) : new Color(50, 66, 99, 205));
            }
            if (chatStreamLines.Length == 0) return;
            float top = chatBlocks.Count == 0 ? 5 : chatBlocks[chatBlocks.Count - 1].Bottom;
            if (top < end) DrawChatBlock(top, top + (chatStreamLines.Length + 1) * chatLineHeight + 11, "队友正在回复…", chatStreamLines, new Color(51, 74, 93, 220));
        }

        private void DrawChatBlock(float top, float bottom, string header, F5Element[] lines, Color tint)
        {
            float y = chatHistoryView.Y + top - chatOffset;
            float x = chatHistoryView.X + 10;
            Fill(new F5Rect(x - 5, y, chatHistoryView.Width - 10, bottom - top - 5), tint);
            if (y + chatLineHeight >= chatHistoryView.Y)
                renderer.Text(header, new F5Rect(x, y, chatHistoryView.Width - 20, chatLineHeight), Color.LightSteelBlue, ChatScale);
            y += chatLineHeight;
            int start = Math.Max(0, (int)((chatHistoryView.Y - y) / chatLineHeight));
            for (int i = start; i < lines.Length && y + i * chatLineHeight < chatHistoryView.Bottom; i++)
                renderer.Label(Move(lines[i], x, y));
        }

        private void DrawChatDraft()
        {
            if (chatDraftLayout == null) return;
            var draft = currentChatDraft.Text;
            float x = chatDraftRect.X + 7, y = chatDraftRect.Y + 3 - chatDraftOffset;
            bool editingChat = aiInput.Chat;
            if (!editingChat && draft.Text.Length == 0)
            {
                renderer.Text("输入消息 · Enter 发送 · Shift+Enter 换行", new F5Rect(x, chatDraftRect.Y + 2, chatDraftRect.Width - 14, chatLineHeight), Color.LightSteelBlue, ChatScale);
                return;
            }
            int first = Math.Max(0, (int)(chatDraftOffset / chatLineHeight));
            for (int i = first; i < chatDraftLayout.Lines.Count && y + i * chatLineHeight < chatDraftRect.Bottom; i++)
            {
                var line = chatDraftLayout.Lines[i];
                float top = y + i * chatLineHeight;
                if (editingChat && draft.HasSelection && line.Start <= draft.SelectionEnd && line.End >= draft.SelectionStart)
                {
                    float a = draft.SelectionStart <= line.Start ? 0 : chatDraftLayout.CaretX(draft.SelectionStart);
                    float b = draft.SelectionEnd >= line.End ? chatDraftLines[i].Rect.Width : chatDraftLayout.CaretX(draft.SelectionEnd);
                    if (b > a) Fill(new F5Rect(x + a, top, b - a, chatLineHeight), new Color(62, 101, 170));
                }
                renderer.Label(Move(chatDraftLines[i], x, y));
            }
            if (!editingChat) return;
            float caretX = x + chatDraftLayout.CaretX(draft.Caret);
            float caretY = y + chatDraftLayout.LineOf(draft.Caret) * chatLineHeight;
            Fill(new F5Rect(caretX, caretY, 1, chatLineHeight - 2), Color.White);
            string composition = aiInput.Composition;
            if (composition.Length > 0)
            {
                float width = Math.Min(chatDraftRect.Width - 8, ChatGlyphWidth(composition) + 8);
                float left = Math.Min(caretX, chatDraftRect.Right - width - 2);
                Fill(new F5Rect(left, caretY, width, chatLineHeight), new Color(20, 31, 49));
                renderer.Text(composition, new F5Rect(left + 3, caretY, width - 6, chatLineHeight), Color.Gold, ChatScale);
            }
            if (aiInput.OwnsTextToken) Main.instance?.SetIMEPanelAnchor(new Vector2(caretX, Math.Min(chatDraftRect.Bottom, caretY + chatLineHeight) + 32), 0);
            if (!String.IsNullOrEmpty(aiInput.Error)) renderer.Text(aiInput.Error,
                new F5Rect(x, chatDraftRect.Bottom - chatLineHeight, chatDraftRect.Width - 14, chatLineHeight), Color.Gold, ChatScale);
        }
    }
}
