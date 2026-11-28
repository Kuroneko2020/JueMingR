using System;
using System.Text;
using JueMingR.Features.Notes;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class CompanionWindow
    {
        private static readonly char[] TaskLineBreaks = { '\r', '\n' };
        private CompanionTaskSnapshot chatTask, chatTaskDetailsSource;
        private string chatTaskSummary, chatTaskReason;
        private bool chatTaskDetails, chatTaskStoppable;
        private F5Rect chatTaskHeading;
        private float chatTaskOffset, chatTaskMaximum, chatTaskDetailsWidth;
        private int chatTaskDetailsSkin = -1;
        private F5Element[] chatTaskDetailLines = Array.Empty<F5Element>();

        private void ObserveChatTask()
        {
            var task = host.GetTask(chatOwner, chatProfile);
            bool stoppable = task != null && host.CanStopTask(chatOwner, chatProfile, task.TaskId);
            if (task?.TaskId != chatTask?.TaskId)
            {
                ResetChatTask();
                contentInvalidated = true;
            }
            if (chatTaskStoppable != stoppable) contentInvalidated = true;
            if (!ReferenceEquals(task, chatTask) || chatTaskStoppable != stoppable)
            {
                chatTask = task;
                if (task != null)
                {
                    var ongoing = stoppable && !task.IsActive ? TaskOngoingActivity(task, out chatTaskReason) : null;
                    if (ongoing == null) chatTaskReason = task.Detail;
                    chatTaskReason = TaskFirstLine(chatTaskReason);
                    chatTaskSummary = (ongoing ?? TaskStatus(task.State)) + (TaskResultPending(task) ? " · 动作待核对" : "") +
                        "  " + TaskProgress(task) + "  " + TaskFirstLine(task.Title);
                }
            }
            chatTaskStoppable = stoppable;
        }

        private void ResetChatTask()
        {
            chatTask = chatTaskDetailsSource = null;
            chatTaskSummary = chatTaskReason = null;
            chatTaskStoppable = chatTaskDetails = false;
            chatTaskOffset = chatTaskMaximum = 0;
            chatTaskHeading = default(F5Rect);
            chatTaskDetailLines = Array.Empty<F5Element>();
        }

        private float BuildChatTask(float x, float top, float width, float row)
        {
            string disclosure = chatTaskDetails ? "返回聊天" : "任务详情";
            float actionsWidth = ConfigurationWidth(disclosure) + (chatTaskStoppable ? ConfigurationWidth("停止任务") + 8 : 0);
            chatTaskHeading = new F5Rect(x, top, width - actionsWidth - 8, Math.Max(34, row));
            var details = ChatAction("task-details", disclosure, () => { chatTaskDetails = !chatTaskDetails; Invalidate(); }, blur: false);
            var taskId = chatTask.TaskId;
            var owner = chatOwner;
            var profile = chatProfile;
            var stop = ChatAction("stop-task:" + taskId, "停止任务", () =>
            {
                if (!host.StopChatTask(owner, profile, taskId)) ShowNotice("任务已变化，请查看当前进度。");
            }, blur: false);
            return Math.Max(chatTaskHeading.Bottom, BuildAiActions(x + width - actionsWidth, top, actionsWidth, bodyView,
                chatTaskStoppable ? new[] { details, stop } : new[] { details }, alignLastRight: true)) + 2;
        }

        private static string TaskProgress(CompanionTaskSnapshot task)
        {
            if (task.StepCount == 0) return "";
            int done = Math.Max(0, Math.Min(task.StepIndex, task.StepCount));
            return task.IsActive && done < task.StepCount ? "第 " + (done + 1) + "/" + task.StepCount + " 步" : done + "/" + task.StepCount + " 步";
        }

        private static string TaskFirstLine(string text)
        {
            if (String.IsNullOrEmpty(text)) return "";
            int end = text.IndexOfAny(TaskLineBreaks);
            return end < 0 ? text : text.Substring(0, end);
        }

        private static bool TaskResultPending(CompanionTaskSnapshot task)
        {
            if (task.IsActive) return false;
            foreach (var attempt in task.Attempts)
                if (attempt.State == "running" || attempt.State == "waiting" || attempt.State == "blocked") return true;
            return false;
        }

        private static string TaskOngoingActivity(CompanionTaskSnapshot task, out string detail)
        {
            detail = task.Detail;
            for (int i = task.Attempts.Count - 1; i >= 0; i--)
            {
                var attempt = task.Attempts[i];
                if (attempt.State != "succeeded") continue;
                switch (attempt.Operation)
                {
                    case "stand_by": detail = attempt.Detail; return "待命中";
                    case "guard": detail = attempt.Detail; return "守卫中";
                    case "follow_owner": detail = attempt.Detail; return "跟随中";
                    case "fish_at": detail = attempt.Detail; return "钓鱼中";
                    default: return "活动继续";
                }
            }
            return "活动继续";
        }

        private static string TaskStatus(string state)
        {
            switch (state)
            {
                case "accepted": return "已接受";
                case "running": return "进行中";
                case "waiting": return "等待中";
                case "blocked": return "遇到阻碍";
                case "paused": return "已暂停";
                case "succeeded": return "已完成";
                case "cancelled": return "已停止";
                case "failed": return "未完成";
                case "rejected": return "未接受";
                case "skipped": return "未执行";
                case "unconfirmed": return "结果未确认";
                case "partial": return "部分完成";
                default: return "正在确认结果";
            }
        }

        private void PrepareChatTaskDetails()
        {
            if (!chatTaskDetails || chatTask == null) return;
            float width = Math.Max(1, chatHistoryView.Width - 22);
            if (!ReferenceEquals(chatTaskDetailsSource, chatTask) || chatTaskDetailsWidth != width || chatTaskDetailsSkin != skin)
            {
                // Task progress belongs to this separate bounded view. No
                // receipt is appended to, or remeasures, the chat history.
                var text = new StringBuilder().AppendLine(chatTask.Title);
                if (!String.IsNullOrWhiteSpace(chatTask.Detail)) text.AppendLine(chatTask.Detail);
                foreach (var attempt in chatTask.Attempts)
                {
                    text.AppendLine().Append("第 ").Append(attempt.StepIndex + 1).Append(" 步 · ").AppendLine(TaskStatus(attempt.State));
                    if (!String.IsNullOrWhiteSpace(attempt.Detail)) text.AppendLine(attempt.Detail);
                }
                chatTaskDetailLines = ChatLines(new NotesTextLayout(text.ToString().TrimEnd(), width, ChatGlyphWidth), chatLineHeight, chatTaskDetailLines);
                chatTaskDetailsSource = chatTask;
                chatTaskDetailsWidth = width;
                chatTaskDetailsSkin = skin;
            }
            chatTaskMaximum = Math.Max(0, chatTaskDetailLines.Length * chatLineHeight + 12 - chatHistoryView.Height);
            chatTaskOffset = Math.Min(chatTaskOffset, chatTaskMaximum);
        }

        private void DrawChatTaskHeading()
        {
            float half = chatTaskHeading.Height / 2;
            var first = new F5Rect(chatTaskHeading.X, chatTaskHeading.Y, chatTaskHeading.Width, half);
            renderer.Text(chatTaskSummary, first, Color.White, ChatStatusScale);
            renderer.Text(chatTaskReason, first.Offset(0, half), Color.LightSteelBlue, ChatStatusScale);
        }

        private void DrawChatTaskDetails()
        {
            Fill(chatHistoryView, new Color(29, 42, 66, 230));
            float top = chatHistoryView.Y + 6 - chatTaskOffset;
            for (int i = Math.Max(0, (int)(chatTaskOffset / chatLineHeight)); i < chatTaskDetailLines.Length && top + i * chatLineHeight < chatHistoryView.Bottom; i++)
                renderer.Label(Move(chatTaskDetailLines[i], chatHistoryView.X + 10, top));
        }
    }
}
