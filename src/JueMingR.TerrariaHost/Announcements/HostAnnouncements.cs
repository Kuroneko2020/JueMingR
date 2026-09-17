using System;
using System.Diagnostics;
using System.IO;
using JueMingR.Features.Announcements;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Settings;
using JueMingR.TerrariaHost.ItemBrowser;
using Terraria;
using Terraria.Chat;
using Terraria.UI.Chat;

namespace JueMingR.TerrariaHost.Announcements
{
    internal sealed class HostAnnouncements : IDisposable, F5.IAnnouncementControls
    {
        internal const string ActionId = "announcement.send";
        private readonly PreferenceDocument<AnnouncementSettings> preferences;
        private readonly AnnouncementCooldown cooldown = new AnnouncementCooldown();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private object world, player, connection;
        private long feedbackAt = -1;
        public bool Enabled { get { return preferences.Snapshot.IsLoaded && preferences.Snapshot.Value.Enabled; } }
        public bool CanConfigure { get { return preferences.Snapshot.IsLoaded; } }
        internal string Status { get; private set; } = "默认关闭；请设置自己的宣告快捷键";
        internal HostAnnouncements(string directory)
        {
            preferences = new PreferenceDocument<AnnouncementSettings>(new AtomicFileDocument(Path.Combine(directory, "JueMingRData", "config", "features", "announcements.json"), 4096, true), new AnnouncementCodec(), new AnnouncementSettings(false));
            AppDomain.CurrentDomain.ProcessExit += OnExit;
        }
        public bool SetEnabled(bool enabled) { return preferences.Set(new AnnouncementSettings(enabled)); }
        internal void Update()
        {
            if (!preferences.Snapshot.IsLoaded && clock.ElapsedMilliseconds >= 2000) preferences.AbandonSlowLoad();
            if (!ReferenceEquals(world, Main.ActiveWorldFileData) || !ReferenceEquals(player, Main.LocalPlayer) || !ReferenceEquals(connection, Netplay.Connection) || Main.gameMenu)
            { world = Main.ActiveWorldFileData; player = Main.LocalPlayer; connection = Netplay.Connection; cooldown.Clear(); }
        }
        internal void Submit(TargetValue value)
        {
            if (!Enabled || value == null) return;
            if (value.UiSlot && value.ItemType <= 0) { Feedback("这里是空槽，未发送宣告"); return; }
            if (!cooldown.TryTake(clock.ElapsedMilliseconds, value.EmptyAir)) return;
            string text = SafeChatText.Build(value.Entries, 1024); if (text.Length == 0) { Feedback("目标文字无法安全发送"); return; }
            try
            {
                var message = ChatManager.Commands.CreateOutgoingMessage(text);
                if (Main.netMode == 1) ChatHelper.SendChatMessageFromClient(message);
                else if (Main.netMode == 0) ChatManager.Commands.ProcessIncomingMessage(message, Main.myPlayer);
                else return;
                Feedback(Main.netMode == 1 ? "已提交宣告；是否收到请由队友确认" : "已在本地显示宣告");
            }
            catch { Feedback("宣告提交失败，未自动重试"); }
        }
        internal void Feedback(string message)
        {
            Status = message; long now = clock.ElapsedMilliseconds;
            if (feedbackAt >= 0 && now - feedbackAt < 1500) return;
            feedbackAt = now; if (!Main.gameMenu && !Main.hideUI) Main.NewText(message, 255, 217, 102);
        }
        private void OnExit(object sender, EventArgs e) { Dispose(); }
        public void Dispose() { AppDomain.CurrentDomain.ProcessExit -= OnExit; preferences.Stop(750); }
    }
}
