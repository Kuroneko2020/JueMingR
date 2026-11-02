using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace JueMingR.Setup
{
    internal sealed class SetupForm : Form
    {
        private static readonly Color Ink = Color.FromArgb(32, 43, 61), Muted = Color.FromArgb(100, 112, 132), Accent = Color.FromArgb(41, 99, 220);
        private readonly ComboBox directory = new ComboBox { Dock = DockStyle.Fill, Name = "GameDirectory", DropDownStyle = ComboBoxStyle.DropDown, IntegralHeight = false, DropDownHeight = 160 };
        private readonly Label identity = Caption("请选择包含 Terraria.exe 的目录。", "GameIdentity");
        private readonly Label discovery = Caption("", "DiscoveryStatus");
        private readonly TextBox outcome = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Name = "OperationResult", BorderStyle = BorderStyle.None, BackColor = Color.White, ForeColor = Ink, Text = "就绪。" };
        private readonly Button install = ActionButton("安装 / 更新", "InstallButton", true);
        private readonly Button uninstall = ActionButton("卸载决明", "UninstallButton", false);
        private readonly Button clear = ActionButton("清空配置", "ClearConfigurationButton", false);
        private readonly Button browse = ActionButton("浏览…", "BrowseButton", false);
        private bool busy;

        internal SetupForm(string initialDirectory = null)
        {
            Text = "决明安装程序"; Name = "JueMingRSetup"; Font = new Font("Microsoft YaHei UI", 9.5F);
            ForeColor = Ink; BackColor = Color.FromArgb(243, 246, 251); AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(720, 460); MinimumSize = new Size(680, 490); StartPosition = FormStartPosition.CenterScreen;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28, 22, 28, 20), ColumnCount = 1, RowCount = 5 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var heading = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1, Margin = new Padding(0, 0, 0, 18) };
            using (Stream stream = typeof(SetupForm).Assembly.GetManifestResourceStream("JueMingR.Setup.Brand.Icon")) Icon = new Icon(stream);
            FormClosed += (sender, args) => Icon.Dispose();
            heading.Controls.Add(new Label { Text = "决明", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font(Font.FontFamily, 25F, FontStyle.Bold), Margin = new Padding(0) }, 0, 0); layout.Controls.Add(heading);
            var card = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(18), ColumnCount = 1, Margin = new Padding(0, 0, 0, 16) };
            card.Controls.Add(new Label { Text = "游戏目录", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 0, 0, 10) });
            var pathRow = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0) };
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            directory.Margin = new Padding(0, 6, 10, 0); pathRow.Controls.Add(directory); browse.Margin = new Padding(0); pathRow.Controls.Add(browse); card.Controls.Add(pathRow);
            discovery.Margin = new Padding(0, 9, 0, 0); card.Controls.Add(discovery);
            identity.Margin = new Padding(0, 6, 0, 0); card.Controls.Add(identity); layout.Controls.Add(card);
            var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 16), WrapContents = true };
            actions.Controls.Add(install); actions.Controls.Add(uninstall); actions.Controls.Add(clear); layout.Controls.Add(actions);
            var status = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(18, 12, 18, 12), Margin = new Padding(0) };
            status.Controls.Add(outcome); layout.Controls.Add(status);
            var footer = Caption("卸载保留笔记、蓝图库和决明R设置。", "RetentionNotice"); footer.Margin = new Padding(0, 14, 0, 0); layout.Controls.Add(footer); Controls.Add(layout);
            browse.Click += (sender, args) => { using (var dialog = new FolderBrowserDialog { Description = "选择包含 Terraria.exe 的游戏目录", ShowNewFolderButton = false, SelectedPath = directory.Text }) if (dialog.ShowDialog(this) == DialogResult.OK) directory.Text = dialog.SelectedPath; };
            directory.TextChanged += (sender, args) => ShowIdentity();
            install.Click += async (sender, args) => await RunOperation("install");
            uninstall.Click += async (sender, args) => await RunOperation("uninstall");
            clear.Click += async (sender, args) =>
            {
                if (MessageBox.Show(this, "清空决明按键和偏好设置？笔记和蓝图库将保留。", "清空配置", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK) await RunOperation("clear-config");
            };
            FormClosing += (sender, args) => { if (busy) args.Cancel = true; };
            string[] candidates = SteamDiscovery.FindInstalled(); directory.Items.AddRange(candidates);
            discovery.Text = candidates.Length == 0 ? "未找到游戏目录，可浏览选择。" : candidates.Length == 1 ? "已自动找到。" : "找到多个目录，请选择。";
            directory.Text = initialDirectory ?? SteamDiscovery.AutomaticSelection(candidates); ShowIdentity();
        }

        private static Label Caption(string text, string name) { return new Label { Text = text, Name = name, AutoSize = true, ForeColor = Muted, Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 0) }; }
        private static Button ActionButton(string text, string name, bool primary)
        {
            var button = new Button { Text = text, Name = name, AutoSize = true, MinimumSize = new Size(112, 38), Padding = new Padding(12, 3, 12, 3), Margin = new Padding(0, 0, 10, 0), FlatStyle = FlatStyle.Flat, BackColor = primary ? Accent : Color.White, ForeColor = primary ? Color.White : Ink, UseVisualStyleBackColor = false, Cursor = Cursors.Hand };
            button.FlatAppearance.BorderColor = primary ? Accent : Color.FromArgb(215, 222, 234); button.FlatAppearance.BorderSize = 1; return button;
        }
        private void ShowIdentity()
        {
            try
            {
                string root = TargetSafety.ValidateGameDirectory(directory.Text, false);
                string current = Path.Combine(root, "JueMingR.dll");
                string state = File.Exists(current) && System.Reflection.AssemblyName.GetAssemblyName(current).Name == "JueMingR" ? "决明已安装" : "未安装决明";
                identity.Text = "Terraria " + System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(root, "Terraria.exe")).FileVersion + "  ·  " + state;
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException || error is BadImageFormatException) { identity.Text = string.IsNullOrWhiteSpace(directory.Text) ? "请选择包含 Terraria.exe 的目录。" : "目录不可用，请选择包含 Terraria.exe 的游戏目录。"; }
        }
        private async Task RunOperation(string operation)
        {
            string selected = directory.Text; busy = true; SetEnabled(false); outcome.Text = "正在处理，请稍候…";
            try
            {
                SetupResult result = await Task.Run(() => new InstallService().Execute(operation, selected));
                outcome.Text = result.message + (result.retained.Count == 0 ? "" : Environment.NewLine + string.Join(Environment.NewLine, result.retained));
            }
            finally { busy = false; SetEnabled(true); ShowIdentity(); }
        }
        private void SetEnabled(bool enabled) { install.Enabled = uninstall.Enabled = clear.Enabled = browse.Enabled = directory.Enabled = enabled; }
    }
}
