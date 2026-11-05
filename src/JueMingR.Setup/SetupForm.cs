using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace JueMingR.Setup
{
    internal sealed class SetupForm : Form
    {
        private static readonly Color Ink = Color.FromArgb(36, 45, 49), Muted = Color.FromArgb(98, 108, 112), Accent = Color.FromArgb(24, 121, 43);
        private readonly ComboBox directory = new ComboBox { Dock = DockStyle.Fill, Name = "GameDirectory", AccessibleName = "游戏目录", DropDownStyle = ComboBoxStyle.DropDown, IntegralHeight = false, DropDownHeight = 160, TabIndex = 0 };
        private readonly TextBox identity = ReadableText("GameIdentity", "Terraria 版本");
        private readonly TextBox installedIdentity = ReadableText("InstalledIdentity", "已安装决明版本");
        private readonly TextBox feedback = ReadableText("TargetFeedback", "目录检查反馈");
        private readonly TextBox payloadVersion = ReadableText("PayloadVersion", "待安装决明版本");
        private readonly TextBox summary = ReadableText("OperationSummary", "操作结果摘要");
        private readonly TextBox details = ReadableText("OperationDetails", "操作结果详情");
        private readonly Button install = ActionButton("安装 / 更新", "InstallButton", true);
        private readonly Button browse = ActionButton("…", "BrowseButton", false);
        private readonly LinkLabel uninstall = MaintenanceLink("卸载决明", "UninstallLink", 0);
        private readonly LinkLabel clear = MaintenanceLink("清空配置", "ClearConfigurationLink", 1);
        private readonly Label identityDivider = new Label { Text = "│", AutoSize = true, ForeColor = Color.Silver, Margin = new Padding(10, 0, 10, 0), TabStop = false };
        private readonly FlowLayoutPanel identities = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = Padding.Empty };
        private readonly TableLayoutPanel resultPanel = new TableLayoutPanel { Name = "OperationResult", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 8, 0, 8) };
        private readonly TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20, 12, 20, 8), ColumnCount = 1, RowCount = 5, AutoScroll = true };
        private readonly Timer checkDelay = new Timer { Interval = 350 };
        private readonly ToolTip fullText = new ToolTip { AutoPopDelay = 30000 };
        private readonly Func<string, SetupTargetState> inspect;
        private readonly Func<string, string, SetupResult> execute;
        private readonly Func<SetupVersionDisplay> readVersion;
        private readonly string emptyTargetPrompt;
        private readonly Font titleFont, labelFont, actionFont;
        private SetupTargetState target;
        private bool busy, checking, closing, resourcesDisposed, measuringLayout, versionStarted;
        private int targetGeneration;

        internal SetupForm(string initialDirectory = null)
            : this(initialDirectory, SteamDiscovery.FindInstalled(), SetupTargetState.Read, (operation, path) => new InstallService().Execute(operation, path)) { }

        // These finite display/service seams exercise the production Form in isolated STA
        // checks without discovering a daily installation or operating on player data.
        internal SetupForm(string initialDirectory, string[] candidates, Func<string, SetupTargetState> inspect, Func<string, string, SetupResult> execute, Func<SetupVersionDisplay> readVersion = null)
        {
            this.inspect = inspect; this.execute = execute; this.readVersion = readVersion ?? SetupVersionReader.Read;
            emptyTargetPrompt = candidates.Length > 1 ? "找到多个游戏目录，请选择。" : "请先选择游戏目录，可输入路径或选择目录。";
            Text = "决明安装程序"; Name = "JueMingRSetup"; Font = new Font("Microsoft YaHei UI", 9.5F);
            titleFont = new Font(Font.FontFamily, 22F, FontStyle.Bold); labelFont = new Font(Font, FontStyle.Bold);
            actionFont = new Font(Font.FontFamily, 11.5F, FontStyle.Bold); install.Font = actionFont;
            ForeColor = Ink; BackColor = Color.FromArgb(247, 249, 248); AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(640, 360); FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
            // Complete development versions remain editable-text values for selection /
            // horizontal caret navigation and copying, rather than enlarging the window.
            foreach (TextBox box in new[] { payloadVersion, identity, installedIdentity, feedback, summary })
            {
                box.Multiline = false; box.WordWrap = false;
                box.TextChanged += (sender, args) => fullText.SetToolTip(box, box.Text);
            }
            using (Stream stream = typeof(SetupForm).Assembly.GetManifestResourceStream("JueMingR.Setup.Brand.Icon")) Icon = new Icon(stream);
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 5; i++) layout.RowStyles.Add(new RowStyle(i == 3 ? SizeType.Percent : SizeType.AutoSize, i == 3 ? 100 : 0));

            var heading = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Margin = new Padding(0, 0, 0, 10) };
            heading.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); heading.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            heading.Controls.Add(new Label { Text = "决明", Name = "ProductTitle", AutoSize = true, Font = titleFont, ForeColor = Accent, Margin = new Padding(0, 0, 12, 8) }, 0, 0);
            heading.Controls.Add(new Label { Text = "│", AutoSize = true, ForeColor = Color.Silver, Margin = new Padding(0, 14, 12, 0) }, 1, 0);
            payloadVersion.Text = "正在读取…"; payloadVersion.ForeColor = Muted; payloadVersion.Margin = new Padding(0, 10, 0, 8); payloadVersion.TabIndex = 0; heading.Controls.Add(payloadVersion, 2, 0);
            var topLine = Separator(); heading.Controls.Add(topLine, 0, 1); heading.SetColumnSpan(topLine, 3); layout.Controls.Add(heading, 0, 0);

            var pathArea = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, TabIndex = 1 };
            pathArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pathArea.Controls.Add(new Label { Text = "游戏目录（Terraria）", AutoSize = true, Font = labelFont, Margin = new Padding(0, 0, 0, 6) });
            var pathRow = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            directory.Margin = new Padding(0, 0, 10, 0); pathRow.Controls.Add(directory, 0, 0); browse.Margin = Padding.Empty; browse.TabIndex = 1; browse.AccessibleName = "选择游戏目录";
            browse.AutoSize = false; browse.MinimumSize = Size.Empty; browse.Padding = Padding.Empty; pathRow.Controls.Add(browse, 1, 0); pathArea.Controls.Add(pathRow); layout.Controls.Add(pathArea, 0, 1);

            var information = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 8, 0, 0), TabIndex = 2 };
            information.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            identity.ForeColor = installedIdentity.ForeColor = Muted; identity.Dock = installedIdentity.Dock = DockStyle.None;
            identity.Margin = installedIdentity.Margin = Padding.Empty; identity.TabIndex = 0; installedIdentity.TabIndex = 1;
            identities.WrapContents = false;
            identities.Controls.Add(identity); identities.Controls.Add(identityDivider); identities.Controls.Add(installedIdentity); information.Controls.Add(identities, 0, 0);
            feedback.ForeColor = Color.FromArgb(149, 54, 36); feedback.Margin = new Padding(0, 5, 0, 0); feedback.TabIndex = 2; information.Controls.Add(feedback, 0, 1); layout.Controls.Add(information, 0, 2);

            resultPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); resultPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); resultPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            summary.Margin = new Padding(0, 0, 0, 6); summary.TabIndex = 0; summary.Visible = false; resultPanel.Controls.Add(summary, 0, 0);
            details.BackColor = Color.White; details.BorderStyle = BorderStyle.FixedSingle; details.TabIndex = 1; details.Margin = Padding.Empty; resultPanel.Controls.Add(details, 0, 1); resultPanel.TabIndex = 3; layout.Controls.Add(resultPanel, 0, 3);

            var actions = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty, TabIndex = 4 };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var bottomLine = Separator(); bottomLine.Margin = new Padding(0, 0, 0, 8); actions.Controls.Add(bottomLine, 0, 0); actions.SetColumnSpan(bottomLine, 2);
            var maintenanceLinks = new FlowLayoutPanel { Name = "MaintenanceLinks", AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty, Anchor = AnchorStyles.Left, TabIndex = 0 };
            maintenanceLinks.Controls.Add(uninstall); maintenanceLinks.Controls.Add(clear); install.TabIndex = 1; install.Margin = Padding.Empty; install.Anchor = AnchorStyles.Right;
            actions.Controls.Add(maintenanceLinks, 0, 1); actions.Controls.Add(install, 1, 1); layout.Controls.Add(actions, 0, 4); Controls.Add(layout);

            browse.Click += (sender, args) =>
            {
                if (busy) return;
                using (var dialog = new FolderBrowserDialog { Description = "选择包含 Terraria.exe 的游戏目录", ShowNewFolderButton = false, SelectedPath = directory.Text })
                    if (dialog.ShowDialog(this) == DialogResult.OK) directory.Text = dialog.SelectedPath;
            };
            directory.TextChanged += (sender, args) => QueueCheck(true);
            install.Click += async (sender, args) => await RunOperation("install");
            uninstall.LinkClicked += async (sender, args) => await RunOperation("uninstall");
            clear.LinkClicked += async (sender, args) =>
            {
                if (busy || target == null || !target.CanMaintain) return;
                string selected = directory.Text;
                if (MessageBox.Show(this, "清空决明按键、窗口与功能偏好设置？笔记和蓝图库将保留。", "清空配置", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK && directory.Text == selected)
                    await RunOperation("clear-config");
            };
            checkDelay.Tick += async (sender, args) => { checkDelay.Stop(); await CheckTarget(); };
            Shown += async (sender, args) => { directory.Select(); await ReadPackageVersion(); };
            Resize += (sender, args) => UpdateLayoutMetrics();
            FormClosing += (sender, args) => { if (busy) args.Cancel = true; else { closing = true; targetGeneration++; checkDelay.Stop(); } };
            directory.Items.AddRange(candidates); directory.Text = initialDirectory ?? SteamDiscovery.AutomaticSelection(candidates);
            QueueCheck(false); UpdateLayoutMetrics();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !resourcesDisposed)
            {
                resourcesDisposed = true; closing = true; targetGeneration++; checkDelay.Stop(); checkDelay.Dispose(); fullText.Dispose();
                if (Icon != null) { Icon.Dispose(); Icon = null; }
                titleFont.Dispose(); labelFont.Dispose(); actionFont.Dispose(); Font.Dispose();
            }
            base.Dispose(disposing);
        }

        private static TextBox ReadableText(string name, string accessibleName)
        {
            return new ArrowlessTextBox { Multiline = true, ReadOnly = true, WordWrap = true, BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(247, 249, 248), ForeColor = Ink, Dock = DockStyle.Fill, Name = name, AccessibleName = accessibleName, ScrollBars = ScrollBars.None };
        }

        private sealed class ArrowlessTextBox : TextBox
        {
            private int wheelRemainder;

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                base.OnMouseWheel(e);
                if (!Multiline || ScrollBars != ScrollBars.None) return;
                ScrollHiddenWheel(e.Delta, SystemInformation.MouseWheelScrollLines);
                var handled = e as HandledMouseEventArgs;
                if (handled != null) handled.Handled = true;
            }

            // The native edit control receives wheel messages with ScrollBars.None but
            // does not scroll them. Keep arrows hidden while using its own line/page
            // scrolling messages; these do not move the caret or replace the selection.
            // Remainder belongs to this control, so precise wheels retain partial ticks.
            private void ScrollHiddenWheel(int delta, int configuredLines)
            {
                if (configuredLines == 0) { wheelRemainder = 0; return; }
                int total = wheelRemainder + delta;
                int ticks = total / SystemInformation.MouseWheelScrollDelta;
                wheelRemainder = total % SystemInformation.MouseWheelScrollDelta;
                if (ticks == 0) return;
                if (configuredLines == -1)
                {
                    // WHEEL_PAGESCROLL uses the native edit's page size, including its
                    // normal line overlap, rather than inventing a fixed pixel amount.
                    for (int i = 0; i < Math.Abs(ticks); i++)
                    {
                        Message page = Message.Create(Handle, 0x00B5, new IntPtr(ticks > 0 ? 2 : 3), IntPtr.Zero); // EM_SCROLL, SB_PAGEUP/DOWN
                        base.WndProc(ref page);
                    }
                }
                else if (configuredLines > 0)
                {
                    Message lines = Message.Create(Handle, 0x00B6, IntPtr.Zero, new IntPtr(-ticks * configuredLines)); // EM_LINESCROLL
                    base.WndProc(ref lines);
                }
            }
        }
        private static Panel Separator() { return new Panel { Dock = DockStyle.Fill, Height = 1, MinimumSize = new Size(0, 1), BackColor = Color.FromArgb(215, 220, 217), Margin = Padding.Empty }; }
        private static LinkLabel MaintenanceLink(string text, string name, int tabIndex)
        {
            return new LinkLabel { Text = text, Name = name, AccessibleName = text, AutoSize = true, LinkBehavior = LinkBehavior.NeverUnderline, LinkColor = Muted, ActiveLinkColor = Accent, VisitedLinkColor = Muted, DisabledLinkColor = Color.FromArgb(160, 166, 163), TabStop = true, TabIndex = tabIndex, Padding = new Padding(0, 9, 0, 9), Margin = new Padding(0, 0, 16, 0) };
        }
        private static Button ActionButton(string text, string name, bool primary)
        {
            var button = new Button { Text = text, Name = name, AccessibleName = text, AutoSize = true, MinimumSize = new Size(primary ? 144 : 40, 38), Padding = new Padding(primary ? 16 : 8, 3, primary ? 16 : 8, 3), Margin = Padding.Empty, FlatStyle = FlatStyle.Flat, BackColor = primary ? Accent : Color.White, ForeColor = primary ? Color.White : Ink, UseVisualStyleBackColor = false };
            button.FlatAppearance.BorderColor = primary ? Accent : Color.FromArgb(214, 222, 218); return button;
        }

        private async Task ReadPackageVersion()
        {
            if (versionStarted || closing) return;
            versionStarted = true;
            SetupVersionDisplay read;
            try { read = await Task.Run(readVersion); }
            catch (Exception error) when (!(error is OutOfMemoryException || error is StackOverflowException)) { read = new SetupVersionDisplay { Problem = "读取失败：" + error.Message }; }
            // Package identity belongs to this window, not the selected target generation.
            // Its finite failure also must not replace an operation's summary/details.
            if (closing || IsDisposed) return;
            payloadVersion.Text = read.Caption; UpdateLayoutMetrics();
        }

        private void QueueCheck(bool clearResult)
        {
            targetGeneration++; checkDelay.Stop(); target = null;
            if (clearResult) { summary.Text = details.Text = ""; summary.Visible = false; }
            if (busy || closing) return;
            identity.Text = "Terraria 版本：待检查"; installedIdentity.Text = "已安装决明版本：待检查";
            feedback.Text = string.IsNullOrWhiteSpace(directory.Text) ? emptyTargetPrompt : "正在检查所选目录…";
            ApplyEligibility(); UpdateLayoutMetrics();
            // Editing schedules one finite read after the quiet interval; no polling.
            if (!string.IsNullOrWhiteSpace(directory.Text)) checkDelay.Start();
        }

        private async Task CheckTarget()
        {
            if (busy || closing || checking) return;
            int generation = targetGeneration; string selected = directory.Text; checking = true;
            try
            {
                SetupTargetState read = await Task.Run(() => inspect(selected));
                // A's late read must never publish into B, restore operation buttons,
                // or access controls after this window has closed.
                if (closing || IsDisposed || busy || generation != targetGeneration) return;
                target = read; identity.Text = read.TerrariaCaption; installedIdentity.Text = read.InstalledCaption; feedback.Text = read.Feedback; ApplyEligibility(); UpdateLayoutMetrics();
            }
            catch (Exception error) when (!(error is OutOfMemoryException || error is StackOverflowException))
            {
                if (!closing && !IsDisposed && !busy && generation == targetGeneration)
                { target = null; identity.Text = "Terraria 版本：无法确认"; installedIdentity.Text = "已安装决明版本：无法确认"; feedback.Text = "状态暂无法确认：" + error.Message; ApplyEligibility(); UpdateLayoutMetrics(); }
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
            // Freeze both inputs before yielding; disabling controls alone cannot reject
            // an already queued/reentrant callback. Service revalidates the frozen target.
            string selected = directory.Text; string acceptedOperation = operation;
            busy = true; targetGeneration++; checkDelay.Stop(); ApplyEligibility();
            summary.Visible = true; details.Text = ""; summary.ForeColor = Ink;
            summary.Text = "正在" + SetupResultPresentation.OperationName(operation) + "，请稍候…"; UpdateLayoutMetrics();
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
                busy = false; UpdateLayoutMetrics();
                // Refresh files independently: even an installed pair cannot turn the
                // service's partial result (e.g. receipt failure) into success.
                QueueCheck(false);
            }
        }

        private void ApplyEligibility()
        {
            directory.Enabled = browse.Enabled = !busy;
            install.Enabled = !busy && target != null && target.CanInstall;
            uninstall.Enabled = clear.Enabled = !busy && target != null && target.CanMaintain;
            install.Text = target != null && target.Pair == SetupPairState.NotInstalled ? "安装决明" : "安装 / 更新"; install.AccessibleName = install.Text;
        }

        private int TextHeight(TextBox box, int maximumLines)
        {
            if (!box.Multiline) return box.Font.Height + 5;
            int width = Math.Max(40, box.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
            int measured = TextRenderer.MeasureText(box.Text.Length == 0 ? " " : box.Text, box.Font, new Size(width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height + 5;
            return maximumLines == 0 ? measured : Math.Min(box.Font.Height * maximumLines + 5, measured);
        }

        private void UpdateLayoutMetrics()
        {
            if (measuringLayout || closing || layout.RowStyles.Count == 0) return;
            measuringLayout = true;
            try
            {
                layout.PerformLayout();
                browse.Size = new Size(directory.PreferredHeight + (int)(8 * DeviceDpi / 96F), directory.PreferredHeight);
                payloadVersion.MinimumSize = new Size(0, TextHeight(payloadVersion, 0));
                int available = Math.Max(80, layout.ClientSize.Width - layout.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
                int gameWidth = Math.Min(available / 2, TextRenderer.MeasureText(identity.Text, identity.Font).Width + 8);
                identityDivider.Visible = true;
                identity.Width = gameWidth; installedIdentity.Width = Math.Max(40, available - gameWidth - identityDivider.PreferredWidth - identityDivider.Margin.Horizontal);
                identity.Height = TextHeight(identity, 0); installedIdentity.Height = TextHeight(installedIdentity, 0);
                feedback.Visible = !string.IsNullOrWhiteSpace(feedback.Text); feedback.MinimumSize = new Size(0, feedback.Visible ? TextHeight(feedback, 3) : 0);
                summary.MinimumSize = new Size(0, summary.Visible ? TextHeight(summary, 4) : 0);
                // A fixed compact frame reserves the same font-based log viewport for
                // long versions and partial results; only tiny work areas need root scroll.
                int viewport = details.Font.Height * 6 + SystemInformation.BorderSize.Height * 2;
                details.MinimumSize = new Size(0, viewport);
                int resultMinimum = viewport + (summary.Visible ? summary.MinimumSize.Height + summary.Margin.Vertical : 0);
                resultPanel.MinimumSize = new Size(0, resultMinimum);
                layout.PerformLayout();
                Rectangle work = Screen.FromControl(this).WorkingArea;
                Size designed = SizeFromClientSize(new Size((int)(640 * DeviceDpi / 96F), (int)(360 * DeviceDpi / 96F)));
                Size frame = new Size(Math.Min(work.Width, designed.Width), Math.Min(work.Height, designed.Height));
                // User-approved fixed proportions replace text-driven window growth.
                // On a smaller desktop clamp the frame and keep native root scrolling;
                // do not demand an off-screen minimum or change display settings.
                if (MinimumSize != frame || MaximumSize != frame)
                { MinimumSize = Size.Empty; MaximumSize = frame; MinimumSize = frame; }
                if (Size != frame) Size = frame;
                if (Bottom > work.Bottom) Top = Math.Max(work.Top, work.Bottom - Height);
                if (Right > work.Right) Left = Math.Max(work.Left, work.Right - Width);
            }
            finally { measuringLayout = false; }
        }
    }
}
