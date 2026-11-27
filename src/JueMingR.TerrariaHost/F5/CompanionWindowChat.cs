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
        private const float ChatScale = .76f, ChatStatusScale = .65f;
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
                updated.Lifecycle != chatView.Lifecycle || updated.ActionReason != chatView.ActionReason || updated.Status != chatView.Status ||
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
            float width = Math.Min(760, bodyView.Width - 24);
            float x = bodyView.X + (bodyView.Width - width) / 2, top = bodyView.Y;
            var profile = CurrentProfile();
            if (profile == null)
            {
                Text("选择或创建队友后即可聊天。", new F5Rect(x, top, width, row));
                BuildAiActions(x, top + row + 4, width, bodyView, new[] { ChatAction("open-model", "去设置模型", OpenModelSetupFromChat) });
                return;
            }

            float line = Math.Max(20, row - 10);
            string role = chatView?.Lifecycle ?? "正在读取状态";
            Text(profile.Name + " · " + role + (chatView?.CanAct == false ? " · 仅聊天" : ""), new F5Rect(x, top, width, line));
            top += line;
            bool connected = !String.IsNullOrWhiteSpace(chatView?.Connection);
            Text(connected ? chatView.Connection : "模型连接当前不可用", new F5Rect(x, top, width, line), ChatStatusScale);
            top += line;
            var status = String.IsNullOrWhiteSpace(chatView?.Status) ? chatView?.ActionReason : chatView.Status;
            if (!String.IsNullOrWhiteSpace(status))
            {
                Text(status, new F5Rect(x, top, width, line), ChatStatusScale);
                top += line;
            }
            top = BuildChatActions(x, top + 3, width) + 3;

            float bottom = bodyView.Bottom - 3;
            float fieldHeight = Math.Max(line + 6, Math.Min(72, (bottom - top) * .38f));
            float editTop = bottom - fieldHeight - row - 4;
            chatHistoryView = new F5Rect(x, top, width, Math.Max(0, editTop - top - 5));
            chatDraftRect = new F5Rect(x, editTop, width, fieldHeight);
            var fieldButton = Button("chat-draft", "", chatDraftRect, FocusChatDraft);
            fieldButton.Element = null;
            float actionTop = bottom - row;
            var actions = new List<(string, string, Action, bool)>(3);
            if (!followChat) actions.Add(ChatAction("latest", "回到最新", () => { followChat = true; chatOffset = chatMaximum; Invalidate(); }));
            if (!String.IsNullOrWhiteSpace(status)) actions.Add(ChatAction("copy-status", "复制状态", () =>
                ShowNotice(aiInput.TryCopy(status) ? "已复制当前状态。" : "无法复制当前状态。")));
            actions.Add(connected ? ChatAction("send", "发送", () => SendChat(), chatView.CanSend, blur: false)
                : ChatAction("configure", "去设置模型", OpenModelSetupFromChat));
            BuildAiActions(x, actionTop, width, bodyView, actions.ToArray(), alignLastRight: true);
        }

        private float BuildChatActions(float x, float top, float width)
        {
            if (chatConfirmation != 0)
            {
                string question = chatConfirmation == 1 ? "清空聊天记录？队友会继续当前活动。" : "另起一段对话？聊天记录会保留。";
                Text(question, new F5Rect(x, top, width, rowHeight), ChatStatusScale);
                return BuildAiActions(x, top + rowHeight, width, bodyView, new[]
                {
                    ChatAction("confirm", "确认", () =>
                    {
                        bool done = chatConfirmation == 1 ? host.ClearChat(chatOwner, chatProfile) : host.RestartChatContext(chatOwner, chatProfile);
                        ConfirmChat(0);
                        if (!done) ShowNotice("队友状态已变化，请重新选择。");
                    }),
                    ChatAction("cancel", "取消", () => ConfirmChat(0))
                });
            }
            bool hasHistory = chatView?.Entries.Count > 0;
            var actions = new List<(string, string, Action, bool)>(5)
            {
                ChatAction("copy", "复制", CopyChat, hasHistory),
                ChatAction("clear", "清空", () => ConfirmChat(1), hasHistory),
                ChatAction("restart", "另起对话", () => ConfirmChat(2), hasHistory)
            };
            if (chatView?.IsRunning == true) actions.Add(ChatAction("stop-reply", "停止回复", () => host.StopChatReply(chatOwner, chatProfile)));
            if (chatView?.CanStopActivity == true) actions.Add(ChatAction("stop-activity", "停止活动", () =>
            {
                if (!host.StopChatActivity(chatOwner, chatProfile)) ShowNotice("当前没有可停止的活动。");
            }));
            return BuildAiActions(x, top, width, bodyView, actions.ToArray());
        }

        private void OpenModelSetupFromChat()
        {
            SelectPage(ModelPage);
            OpenModelPage(ModelPageView.Setup);
        }

        private (string Key, string Label, Action Run, bool Enabled) ChatAction(string key, string text, Action action,
            bool enabled = true, bool blur = true) => ("chat-" + key, text, () => { if (!blur || BlurAiInput()) action(); }, enabled);

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
                case "query": return "查询结果";
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
                string display = FormatChatReply(stream);
                string previous = chatStreamLayout?.Text;
                int prefix = previous != null && display.StartsWith(previous, StringComparison.Ordinal) ? previous.Length : 0;
                chatStreamLayout = display.Length == 0 ? null : new NotesTextLayout(display, historyWidth, ChatGlyphWidth,
                    Array.AsReadOnly(TextElements.Boundaries(display)), chatStreamLayout, prefix);
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
            int anchorIndex = followChat ? chatBlocks.Count : FirstChatBlockAt(chatOffset);
            ChatBlock anchor = anchorIndex < chatBlocks.Count ? chatBlocks[anchorIndex] : null;
            Guid anchorId = anchor?.Entry.Id ?? Guid.Empty;
            float anchorInset = anchor == null ? 0 : chatOffset - anchor.Top;
            var previous = new Dictionary<Guid, ChatBlock>(chatBlocks.Count);
            foreach (ChatBlock block in chatBlocks) previous[block.Entry.Id] = block;
            chatBlocks.Clear();
            float top = 5;
            bool restoredAnchor = false;
            foreach (var entry in entries)
            {
                previous.TryGetValue(entry.Id, out ChatBlock block);
                // An oldest-row trim or an activity update changes offsets,
                // but only changed public text needs fresh glyph work.
                F5Element[] lines = block?.Lines;
                if (block == null || block.Entry.Text != entry.Text || block.Entry.Kind != entry.Kind)
                {
                    string display = entry.Text ?? "";
                    if (entry.Kind == "assistant") display = FormatChatReply(display);
                    var layout = new NotesTextLayout(display, chatHistoryWidth, ChatGlyphWidth);
                    lines = ChatLines(layout, chatLineHeight, lines);
                }
                block = block ?? new ChatBlock();
                block.Entry = entry;
                block.Lines = lines;
                block.Top = top;
                top = block.Bottom = top + (lines.Length + 1) * chatLineHeight + 11;
                chatBlocks.Add(block);
                if (anchor != null && entry.Id == anchorId)
                {
                    chatOffset = block.Top + Math.Min(anchorInset, block.Bottom - block.Top - 1);
                    restoredAnchor = true;
                }
            }
            if (anchor != null && !restoredAnchor) chatOffset = 0;
            chatEntries = entries;
        }

        // Display simple prose formatting. Raw history and clipboard text retain
        // the original Markdown, including content in fenced code and long replies.
        private static string FormatChatReply(string value)
        {
            if (value.IndexOf('*') < 0 && value.IndexOf('-') < 0 && value.IndexOf('`') < 0 && value.IndexOf('#') < 0) return value;
            var result = new StringBuilder(value.Length);
            bool fenced = false;
            string[] lines = value.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) result.Append('\n');
                string line = lines[i].TrimEnd('\r');
                if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    fenced = !fenced;
                    result.Append(line);
                    continue;
                }
                if (fenced) { result.Append(line); continue; }
                int at = 0;
                if (line.StartsWith("- ", StringComparison.Ordinal)) { result.Append("• "); at = 2; }
                else
                {
                    int marks = 0;
                    while (marks < line.Length && line[marks] == '#') marks++;
                    if (marks > 0 && marks <= 6 && marks < line.Length && line[marks] == ' ') at = marks + 1;
                }
                while (at < line.Length)
                {
                    char marker = line[at];
                    int markerLength = marker == '`' ? 1 : marker == '*' && at + 1 < line.Length && line[at + 1] == '*' ? 2 : 0;
                    if (markerLength > 0 && (at == 0 || line[at - 1] != '\\'))
                    {
                        int content = at + markerLength;
                        int end = line.IndexOf(markerLength == 1 ? "`" : "**", content, StringComparison.Ordinal);
                        if (end > content && !Char.IsWhiteSpace(line[content]) && !Char.IsWhiteSpace(line[end - 1]))
                        {
                            result.Append(line, content, end - content);
                            at = end + markerLength;
                            continue;
                        }
                    }
                    result.Append(marker);
                    at++;
                }
            }
            return result.ToString();
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
            float end = chatOffset + chatHistoryView.Height;
            for (int i = FirstChatBlockAt(chatOffset); i < chatBlocks.Count && chatBlocks[i].Top < end; i++)
            {
                var block = chatBlocks[i];
                bool action = block.Entry.Kind == "action" || block.Entry.Kind == "status";
                Color tint = action ? new Color(70, 66, 48, 210) : new Color(41, 55, 80, 205);
                if (block.Entry.Kind == "user") tint = new Color(52, 72, 111, 225);
                if (block.Entry.Kind == "query") tint = new Color(40, 70, 74, 215);
                DrawChatBlock(block.Top, block.Bottom, ChatEntryName(block.Entry.Kind) + " · " + block.Entry.Time.ToLocalTime().ToString("HH:mm"), block.Lines,
                    tint);
            }
            if (chatStreamLines.Length == 0) return;
            float top = chatBlocks.Count == 0 ? 5 : chatBlocks[chatBlocks.Count - 1].Bottom;
            if (top < end) DrawChatBlock(top, top + (chatStreamLines.Length + 1) * chatLineHeight + 11, "队友正在回复…", chatStreamLines, new Color(51, 74, 93, 220));
        }

        private int FirstChatBlockAt(float offset)
        {
            int low = 0, high = chatBlocks.Count;
            while (low < high)
            {
                int mid = (low + high) / 2;
                if (chatBlocks[mid].Bottom <= offset) low = mid + 1;
                else high = mid;
            }
            return low;
        }

        private void DrawChatBlock(float top, float bottom, string header, F5Element[] lines, Color tint)
        {
            float y = chatHistoryView.Y + top - chatOffset;
            float x = chatHistoryView.X + 10;
            Fill(new F5Rect(x - 5, y, chatHistoryView.Width - 10, bottom - top - 5), tint);
            if (y + chatLineHeight >= chatHistoryView.Y)
                renderer.Text(header, new F5Rect(x, y, chatHistoryView.Width - 20, chatLineHeight), Color.LightSteelBlue, ChatStatusScale);
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
