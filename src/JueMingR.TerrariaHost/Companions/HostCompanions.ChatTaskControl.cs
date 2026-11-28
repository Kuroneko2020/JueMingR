using System;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        internal bool StopChatTask(Guid ownerId, Guid profileId, Guid taskId)
        {
            if (!CanStopTask(ownerId, profileId, taskId) || StopTask(ownerId, profileId, taskId).TaskId != taskId) return false;
            // Stop this task's native control and retire late commands from
            // the current reply without changing persistent behavior options.
            InvalidateChatAction(profileId, "已停止任务及本次回复中尚未执行的指令。");
            return true;
        }
    }
}
