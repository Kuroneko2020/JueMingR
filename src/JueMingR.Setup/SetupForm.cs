using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace JueMingR.Setup
{
    internal sealed class SetupForm : Form
    {
        private static readonly Color Ink = Color.FromArgb(36, 45, 49), Muted = Color.FromArgb(98, 108, 112), Accent = Color.FromArgb(30, 115, 97);
        private readonly ComboBox directory = new ComboBox { Dock = DockStyle.Fill, Name = "GameDirectory", AccessibleName = "游戏目录", DropDownStyle = ComboBoxStyle.DropDown, IntegralHeight = false, DropDownHeight = 160, TabIndex = 0 };
        private readonly TextBox identity = ReadableText("GameIdentity", "目录和安装状态");
        private readonly TextBox summary = ReadableText("OperationSummary", "操作结果摘要");
        private readonly TextBox details = ReadableText("OperationDetails", "操作结果详情");
        private readonly Button install = ActionButton("安装 / 更新", "InstallButton", true);
        private readonly Button maintenance = ActionButton("维护 ▾", "MaintenanceButton", false);
        private readonly Button browse = ActionButton("选择目录…", "BrowseButton", false);
        private readonly LinkLabel toggleDetails = new LinkLabel { Text = "详情", Name = "ToggleDetails", AccessibleName = "展开操作详情", AutoSize = true, Margin = new Padding(0, 4, 0, 4), TabIndex = 0 };
        private readonly ContextMenuStrip maintenanceMenu = new ContextMenuStrip { Name = "MaintenanceMenu", ShowImageMargin = false };
        private readonly ToolStripMenuItem uninstall = new ToolStripMenuItem("卸载决明") { Name = "UninstallMenuItem", ToolTipText = "保留全部按键、偏好、笔记和蓝图库。" };
        private readonly ToolStripMenuItem clear = new ToolStripMenuItem("清空配置…") { Name = "ClearConfigurationMenuItem", ToolTipText = "重置按键、窗口与功能偏好，保留笔记和蓝图库。" };
        private readonly TableLayoutPanel resultPanel = new TableLayoutPanel { Name = "OperationResult", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Visible = false, Margin = new Padding(0, 12, 0, 12) };
        private readonly TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(26, 20, 26, 20), ColumnCount = 1, RowCount = 5, AutoScroll = true };
        private readonly Timer checkDelay = new Timer { Interval = 350 };
        private readonly Func<string, SetupTargetState> inspect;
        private readonly Func<string, string, SetupResult> execute;
        private readonly string emptyTargetPrompt;
        private SetupTargetState target;
        private bool busy, checking, expanded, closing, resourcesDisposed, measuringLayout;
        private int targetGeneration;
        private int resultHeight;

        internal SetupForm(string initialDirectory = null)
            : this(initialDirectory, SteamDiscovery.FindInstalled(), SetupTargetState.Read, (operation, path) => new InstallService().Execute(operation, path)) { }

        // The internal seams let isolated STA checks exercise this production Form without
        // discovering a real installation or directing a service operation at player data.
        internal SetupForm(string initialDirectory, string[] candidates, Func<string, SetupTargetState> inspect, Func<string, string, SetupResult> execute)
        {
            this.inspect = inspect; this.execute = execute;
            emptyTargetPrompt = candidates.Length > 1 ? "找到多个游戏目录，请选择。" : "请先选择游戏目录，可输入路径或选择目录。";
            Text = "决明安装程序"; Name = "JueMingRSetup"; Font = new Font("Microsoft YaHei UI", 9.5F);
            ForeColor = Ink; BackColor = Color.FromArgb(247, 249, 248); AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(660, 330); MinimumSize = new Size(540, 350); StartPosition = FormStartPosition.CenterScreen;
            using (Stream stream = typeof(SetupForm).Assembly.GetManifestResourceStream("JueMingR.Setup.Brand.Icon")) Icon = new Icon(stream);
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 5; i++) layout.RowStyles.Add(new RowStyle(i == 3 ? SizeType.Percent : SizeType.AutoSize, i == 3 ? 100 : 0));
            var heading = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 0, 0, 18) };
            heading.Controls.Add(new Label { Text = "决明", Name = "ProductTitle", AutoSize = true, Font = new Font(Font.FontFamily, 25F, FontStyle.Bold), Margin = Padding.Empty });
            heading.Controls.Add(new Label { Text = "Terraria 客户端辅助工具", AutoSize = true, ForeColor = Muted, Margin = new Padding(0, 4, 0, 0) });
            layout.Controls.Add(heading, 0, 0);
            var pathArea = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
            pathArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pathArea.Controls.Add(new Label { Text = "游戏目录", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 0, 0, 8) });
            var pathRow = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            directory.Margin = new Padding(0, 5, 10, 0); pathRow.Controls.Add(directory, 0, 0); browse.Margin = Padding.Empty; browse.TabIndex = 1; pathRow.Controls.Add(browse, 1, 0); pathArea.Controls.Add(pathRow);
            layout.Controls.Add(pathArea, 0, 1);
            identity.ForeColor = Muted; identity.Margin = new Padding(0, 10, 0, 8); identity.TabIndex = 2; layout.Controls.Add(identity, 0, 2);
            resultPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            resultPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); resultPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); resultPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            summary.Margin = Padding.Empty; summary.TabIndex = 0; resultPanel.Controls.Add(summary, 0, 0); resultPanel.Controls.Add(toggleDetails, 0, 1);
            details.ScrollBars = ScrollBars.Vertical; details.BackColor = Color.White; details.BorderStyle = BorderStyle.FixedSingle; details.Visible = false; details.TabIndex = 1; resultPanel.Controls.Add(details, 0, 2); resultPanel.TabIndex = 3;
            layout.Controls.Add(resultPanel, 0, 3);
            var actions = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 12, 0, 0), TabIndex = 4 };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            maintenance.Anchor = AnchorStyles.Left; maintenance.TabIndex = 0; install.TabIndex = 1; install.Margin = Padding.Empty;
            actions.Controls.Add(maintenance, 0, 0); actions.Controls.Add(install, 1, 0); layout.Controls.Add(actions, 0, 4); Controls.Add(layout);
            maintenanceMenu.Items.Add(uninstall); maintenanceMenu.Items.Add(clear);
            maintenance.Click += (sender, args) => { if (!busy) maintenanceMenu.Show(maintenance, new Point(0, maintenance.Height)); };
            browse.Click += (sender, args) =>
            {
                if (busy) return;
                using (var dialog = new FolderBrowserDialog { Description = "选择包含 Terraria.exe 的游戏目录", ShowNewFolderButton = false, SelectedPath = directory.Text })
                    if (dialog.ShowDialog(this) == DialogResult.OK) directory.Text = dialog.SelectedPath;
            };
            directory.TextChanged += (sender, args) => QueueCheck(true);
            install.Click += async (sender, args) => await RunOperation("install");
            uninstall.Click += async (sender, args) => await RunOperation("uninstall");
            clear.Click += async (sender, args) =>
            {
                if (busy || target == null || !target.CanMaintain) return;
                string selected = directory.Text;
                if (MessageBox.Show(this, "清空决明按键、窗口与功能偏好设置？笔记和蓝图库将保留。", "清空配置", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK && directory.Text == selected)
                    await RunOperation("clear-config");
            };
            toggleDetails.LinkClicked += (sender, args) => { expanded = !expanded; UpdateResultLayout(); };
            checkDelay.Tick += async (sender, args) => { checkDelay.Stop(); await CheckTarget(); };
            Resize += (sender, args) => UpdateTextHeights();
            FormClosing += (sender, args) => { if (busy) args.Cancel = true; else { closing = true; targetGeneration++; checkDelay.Stop(); } };
            directory.Items.AddRange(candidates);
            directory.Text = initialDirectory ?? SteamDiscovery.AutomaticSelection(candidates);
            QueueCheck(false);
            UpdateTextHeights();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !resourcesDisposed)
            {
                resourcesDisposed = true;
                closing = true; targetGeneration++; checkDelay.Stop(); checkDelay.Dispose(); maintenanceMenu.Dispose();
                if (Icon != null) { Icon.Dispose(); Icon = null; }
            }
            base.Dispose(disposing);
        }

        private static TextBox ReadableText(string name, string accessibleName)
        {
            return new TextBox { Multiline = true, ReadOnly = true, WordWrap = true, BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(247, 249, 248), ForeColor = Ink, Dock = DockStyle.Fill, Name = name, AccessibleName = accessibleName, ScrollBars = ScrollBars.Vertical };
        }

        private static Button ActionButton(string text, string name, bool primary)
        {
            var button = new Button { Text = text, Name = name, AccessibleName = text, AutoSize = true, MinimumSize = new Size(112, 38), Padding = new Padding(12, 3, 12, 3), Margin = Padding.Empty, FlatStyle = FlatStyle.Flat, BackColor = primary ? Accent : Color.White, ForeColor = primary ? Color.White : Ink, UseVisualStyleBackColor = false };
            button.FlatAppearance.BorderColor = primary ? Accent : Color.FromArgb(214, 222, 218); return button;
        }

        private void QueueCheck(bool clearResult)
        {
            targetGeneration++; checkDelay.Stop(); target = null;
            if (clearResult) { expanded = false; resultPanel.Visible = false; details.Visible = false; UpdateLayoutMetrics(true); }
            if (busy || closing) return;
            identity.Text = string.IsNullOrWhiteSpace(directory.Text) ? emptyTargetPrompt : "正在检查所选目录…";
            ApplyEligibility(); UpdateTextHeights();
            // Text editing only schedules one finite read after the quiet interval; no polling.
            if (!string.IsNullOrWhiteSpace(directory.Text)) checkDelay.Start();
        }

        private async Task CheckTarget()
        {
            if (busy || closing || checking) return;
            int generation = targetGeneration; string selected = directory.Text; checking = true;
            try
            {
                SetupTargetState read = await Task.Run(() => inspect(selected));
                // A's late read must never publish into B, restore buttons during an operation,
                // or access controls after this window has closed.
                if (closing || IsDisposed || busy || generation != targetGeneration) return;
                target = read; identity.Text = read.Caption; ApplyEligibility(); UpdateTextHeights();
            }
            catch (Exception error) when (!(error is OutOfMemoryException || error is StackOverflowException))
            {
                if (!closing && !IsDisposed && !busy && generation == targetGeneration)
                { target = null; identity.Text = "状态暂无法确认：" + error.Message; ApplyEligibility(); UpdateTextHeights(); }
            }
            finally
            {
                checking = false;
                if (!closing && !IsDisposed && !busy && generation != targetGeneration) checkDelay.Start();
            }
        }

        private async Task RunOperation(string operation)
        {
            if (busy || closing || target == null || (operation == "install" ? !target.CanInstall : !target.CanMaintain)) return;
            // Freeze both inputs before yielding. Disabling controls alone does not prevent
            // a queued/reentrant callback from launching another operation.
            string selected = directory.Text; string acceptedOperation = operation;
            busy = true; targetGeneration++; checkDelay.Stop(); maintenanceMenu.Close(); ApplyEligibility();
            expanded = false; resultPanel.Visible = true; toggleDetails.Visible = false; details.Visible = false;
            summary.ForeColor = Ink; summary.Text = "正在" + SetupResultPresentation.OperationName(operation) + "，请稍候…"; UpdateResultLayout();
            try
            {
                SetupResult result = await Task.Run(() => execute(acceptedOperation, selected));
                summary.Text = SetupResultPresentation.Summary(result); details.Text = SetupResultPresentation.Details(result, selected);
                summary.ForeColor = result.status == "partial" || result.status == "failure" ? Color.FromArgb(149, 54, 36) : Ink;
            }
            catch (Exception error) when (!(error is OutOfMemoryException || error is StackOverflowException))
            {
                var result = new SetupResult { operation = acceptedOperation, status = "failure", exitCode = 2, message = error.Message };
                summary.Text = SetupResultPresentation.Summary(result); details.Text = SetupResultPresentation.Details(result, selected); summary.ForeColor = Color.FromArgb(149, 54, 36);
            }
            finally
            {
                busy = false; toggleDetails.Visible = true; UpdateResultLayout();
                // Refresh current files independently. A failed state read must not replace
                // the service's success/partial/failure result or its actual target.
                QueueCheck(false);
            }
        }

        private void ApplyEligibility()
        {
            directory.Enabled = browse.Enabled = !busy;
            maintenance.Enabled = !busy;
            install.Enabled = !busy && target != null && target.CanInstall;
            uninstall.Enabled = clear.Enabled = !busy && target != null && target.CanMaintain;
            install.Text = target != null && target.Pair == SetupPairState.NotInstalled ? "安装决明" : "安装 / 更新";
            install.AccessibleName = install.Text;
        }

        private void UpdateTextHeights()
        {
            UpdateLayoutMetrics(false);
        }

        private int TextHeight(TextBox box, int maximum)
        {
            int width = Math.Max(80, box.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
            int measured = TextRenderer.MeasureText(box.Text.Length == 0 ? " " : box.Text, box.Font, new Size(width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height + 5;
            return Math.Min((int)(maximum * DeviceDpi / 96F), measured);
        }

        private void UpdateResultLayout()
        {
            details.Visible = expanded; toggleDetails.Text = expanded ? "收起" : "详情"; toggleDetails.AccessibleName = expanded ? "收起操作详情" : "展开操作详情";
            UpdateLayoutMetrics(true);
        }

        private void UpdateLayoutMetrics(bool adjustHeight)
        {
            if (measuringLayout || closing || layout.RowStyles.Count == 0) return;
            measuringLayout = true;
            try
            {
                layout.PerformLayout();
                identity.MinimumSize = new Size(0, TextHeight(identity, 88));
                summary.MinimumSize = new Size(0, TextHeight(summary, 104));
                // Percent rows only receive leftover space. Reserve measured text/link
                // heights as real minima so shrinking cannot erase a partial/failure result.
                int detailViewport = expanded ? details.Font.Height * 4 + SystemInformation.BorderSize.Height * 2 : 0;
                details.MinimumSize = new Size(0, detailViewport);
                int resultMinimum = resultPanel.Visible ? summary.MinimumSize.Height + (toggleDetails.Visible ? toggleDetails.GetPreferredSize(Size.Empty).Height + toggleDetails.Margin.Vertical : 0) + detailViewport + (expanded ? details.Margin.Vertical : 0) : 0;
                resultPanel.MinimumSize = new Size(0, resultMinimum);
                layout.PerformLayout();
                int fixedHeight = layout.Padding.Vertical;
                int[] rows = layout.GetRowHeights();
                for (int i = 0; i < rows.Length; i++) if (i != 3) fixedHeight += rows[i];
                int desired = resultPanel.Visible ? resultMinimum + resultPanel.Margin.Vertical : 0;
                int previousHeight = Height, delta = desired - resultHeight; resultHeight = desired;
                Rectangle work = Screen.FromControl(this).WorkingArea;
                int requiredHeight = SizeFromClientSize(new Size(0, fixedHeight + desired)).Height;
                MinimumSize = new Size((int)(540 * DeviceDpi / 96F), Math.Min(work.Height, Math.Max((int)(350 * DeviceDpi / 96F), requiredHeight)));
                // On a shorter work area the root can scroll and detail text scrolls in its
                // own viewport. Do not demand a minimum window taller than the screen.
                if (adjustHeight) Height = Math.Max(MinimumSize.Height, Math.Min(work.Height, previousHeight + delta));
                if (Bottom > work.Bottom) Top = Math.Max(work.Top, work.Bottom - Height);
            }
            finally { measuringLayout = false; }
        }
    }
}
