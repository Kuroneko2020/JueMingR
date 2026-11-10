using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using JueMingR.Setup;

internal static class SetupFormChecks
{
    private const string FeedbackNotice = "当前测试版遇到问题欢迎群里反馈";
    internal static void Run()
    {
        Application.EnableVisualStyles();
        foreach (string version in new[] { "0.1.0", "0.1.0+fb509f6f711e0e2a89f3bba9397c5c9e32f5ddd3", "0.2.0-preview.3+abcdef", "0.2.0-preview.3" })
        {
            string expected = version.Split('+')[0];
            var package = new SetupVersionDisplay { Version = version };
            var installed = Ready("A"); installed.InstalledVersion = version;
            Check(package.Caption == expected && installed.InstalledCaption == "已安装决明版本：" + expected &&
                installed.Caption.Contains("决明版本：" + expected) && package.Version == version && installed.InstalledVersion == version,
                "version-captions-remove-only-build-metadata-preserve-pre-release-and-raw-identity:" + version);
        }
        Check(new SetupVersionDisplay { Problem = "读取失败：原因+详细", CleanupProblem = "清理失败+路径" }.Caption == "读取失败：原因+详细" + Environment.NewLine + "清理失败+路径" &&
            new SetupVersionDisplay().Caption == "无法确认", "version-display-leaves-unknown-read-and-cleanup-failures-unchanged");
        var rawVersion = new SetupVersionDisplay { Version = "0.1.3+isolated-source-identity" };
        using (var form = new SetupForm("A", new string[0], Ready,
            (operation, path) => { throw new Exception("Header check cannot execute an operation"); }, () => rawVersion))
        {
            Observe(form, "test-version-header", () =>
            {
                var caption = Find<TextBox>(form, "PayloadVersion");
                Pump(() => caption.Text == "0.1.3 " + FeedbackNotice);
                Size frame = form.Size;
                Check(NoticeVisible(caption) && caption.Multiline && caption.WordWrap, "form-test-notice-completely-visible-normal-width");
                Check(rawVersion.Version == "0.1.3+isolated-source-identity" && rawVersion.Caption == "0.1.3" &&
                    !Find<TextBox>(form, "InstalledIdentity").Text.Contains(FeedbackNotice) && !Find<TextBox>(form, "GameIdentity").Text.Contains(FeedbackNotice),
                    "form-test-notice-is-only-payload-presentation-not-version-identity");
                Capture(form, "setup-header-normal.png");
                // Restrict only the existing heading's usable content width. This
                // exercises real native wrapping without changing monitor settings.
                var heading = (TableLayoutPanel)caption.Parent; Padding original = heading.Padding;
                heading.Padding = new Padding(original.Left, original.Top, original.Right + Math.Max(0, caption.Width - 220), original.Bottom);
                heading.PerformLayout();
                typeof(SetupForm).GetMethod("UpdateLayoutMetrics", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(form, null);
                Application.DoEvents();
                Check(caption.Width <= 225 * form.DeviceDpi / 96F && NoticeVisible(caption) && form.Size == frame,
                    "form-test-notice-wraps-completely-at-narrow-usable-width-without-frame-growth");
                Capture(form, "setup-header-narrow.png");
                form.Close();
            });
        }
        int calls = 0;
        using (var started = new ManualResetEventSlim())
        using (var release = new ManualResetEventSlim())
        using (var form = new SetupForm("A", new[] { "A", "B" }, Ready, (operation, selected) =>
        {
            Interlocked.Increment(ref calls); started.Set();
            if (!release.Wait(5000)) throw new Exception("Test did not release operation.");
            var result = new SetupResult { operation = operation, gameDirectory = selected, status = "partial", exitCode = 3, message = "受控配置失败", failedStep = "replace-config" };
            result.completed.Add("replace-dll"); result.retained.Add("保留外部内容"); return result;
        }))
        {
            Observe(form, "operation", () =>
            {
            var install = Find<Button>(form, "InstallButton"); var path = Find<ComboBox>(form, "GameDirectory");
            Pump(() => install.Enabled);
            Size fixedFrame = form.Size;
            Check(SynchronizationContext.Current is WindowsFormsSynchronizationContext && Application.MessageLoop, "form-persistent-sta-message-loop");
            Check(Find<Control>(form, "OperationResult").Visible && Find<TextBox>(form, "OperationDetails").Visible && Find<TextBox>(form, "OperationDetails").Text == "", "form-default-visible-empty-log-no-fake-ready");
            Check(Find<Control>(form, "ProductTitle").Text == "决明" && form.Icon != null, "form-text-heading-window-icon");
            Check(form.Controls.Find("MaintenanceButton", true).Length == 0 && form.Controls.Find("ToggleDetails", true).Length == 0 && form.AcceptButton == null, "form-retired-menu-and-collapse-no-default-install");
            var uninstall = Find<LinkLabel>(form, "UninstallLink"); var clear = Find<LinkLabel>(form, "ClearConfigurationLink");
            Check(uninstall.LinkBehavior == LinkBehavior.NeverUnderline && clear.LinkBehavior == LinkBehavior.NeverUnderline && uninstall.TabStop && clear.TabStop && uninstall.VisitedLinkColor == uninstall.LinkColor && clear.AccessibleName == "清空配置", "form-maintenance-native-links-accessible-no-default-underline");
            install.PerformClick(); Pump(() => started.IsSet); install.PerformClick();
            ActivateLink(uninstall);
            Check(calls == 1 && !path.Enabled && !Find<Button>(form, "BrowseButton").Enabled && !uninstall.Enabled && !clear.Enabled, "form-busy-inputs-frozen-single-operation");
            form.Close(); Check(form.Visible && !form.IsDisposed, "form-busy-close-rejected");
            release.Set();
            Pump(() => install.Enabled && Find<TextBox>(form, "OperationSummary").Text.Contains("只完成了一部分"));
            Check(Find<TextBox>(form, "InstalledIdentity").Text == "已安装决明版本：0.1.0" && Find<TextBox>(form, "OperationSummary").Text.Contains("受控配置失败"), "form-current-product-version-does-not-erase-partial-result");
            Check(Find<TextBox>(form, "OperationDetails").Visible && Find<TextBox>(form, "OperationDetails").Text.Contains("replace-config") && Find<TextBox>(form, "OperationDetails").Text.Contains("目标：A"), "form-default-visible-real-fields-and-target");
            Check(form.Size == fixedFrame, "form-result-does-not-enlarge-fixed-frame");
            form.Size = form.MinimumSize; Application.DoEvents();
            var resultSummary = Find<TextBox>(form, "OperationSummary");
            var resultDetails = Find<TextBox>(form, "OperationDetails");
            Check(resultDetails.Visible && FullyVisible(resultDetails) && resultDetails.ClientSize.Height >= resultDetails.Font.Height * 4 && resultDetails.ScrollBars == ScrollBars.None && SummaryReasonVisible(resultSummary, "受控配置失败") && FullyVisible(uninstall) && FullyVisible(clear) && FullyVisible(install), "form-minimum-readable-log-no-visible-arrows-and-all-actions");
            Check(Find<Button>(form, "BrowseButton").Height == path.PreferredHeight && Find<Button>(form, "BrowseButton").Width < path.PreferredHeight * 2 && Find<TextBox>(form, "PayloadVersion").ScrollBars == ScrollBars.None && Find<TextBox>(form, "GameIdentity").ScrollBars == ScrollBars.None && Find<TextBox>(form, "InstalledIdentity").ScrollBars == ScrollBars.None, "form-compact-browse-and-version-no-arrows");
            Size stableMinimum = form.MinimumSize;
            form.Width += 20; Check(form.Size == fixedFrame, "form-fixed-frame-rejects-user-size-growth"); form.Width -= 20; Application.DoEvents();
            Check(form.MinimumSize == stableMinimum, "form-resize-no-height-growth");
            path.Text = "B";
            Check(Find<Control>(form, "OperationResult").Visible && resultDetails.Text == "" && !resultSummary.Visible && !install.Enabled && !uninstall.Enabled && !clear.Enabled, "form-target-change-clears-old-result-and-eligibility-retains-log");
            Pump(() => install.Enabled);
            form.Close();
            });
        }

        using (var firstStarted = new ManualResetEventSlim())
        using (var firstRelease = new ManualResetEventSlim())
        using (var form = new SetupForm("A", new string[0], path =>
        {
            if (path == "A") { firstStarted.Set(); if (!firstRelease.Wait(5000)) throw new Exception("Test did not release target read."); }
            var state = Ready(path); state.Version = path; state.CanInstall = path != "B"; state.InstallProblem = "当前安装器不支持此游戏版本。"; return state;
        }, (operation, selected) => { throw new Exception("No operation expected."); }))
        {
            Observe(form, "target-switch", () =>
            {
            Pump(() => firstStarted.IsSet);
            Find<ComboBox>(form, "GameDirectory").Text = "B"; firstRelease.Set();
            Pump(() => Find<TextBox>(form, "GameIdentity").Text.Contains("Terraria 版本：B"));
            Check(!Find<TextBox>(form, "GameIdentity").Text.Contains("Terraria 版本：A") && !Find<Button>(form, "InstallButton").Enabled && Find<LinkLabel>(form, "UninstallLink").Enabled && Find<LinkLabel>(form, "ClearConfigurationLink").Enabled, "form-stale-read-rejected-new-game-maintenance-independent");
            form.Close();
            });
        }
        int versionReads = 0, maintenanceCalls = 0;
        string completePackageVersion = "0.1.0+" + new string('a', 40);
        using (var form = new SetupForm("A", new string[0], path => new SetupTargetState { Directory = path, Version = "1.4.5.8", CanMaintain = true, Pair = SetupPairState.NotInstalled },
            (operation, selected) => { Check(operation == "uninstall" && selected == "B", "form-exact-uninstall-operation-and-target"); maintenanceCalls++; return new SetupResult { operation = operation, gameDirectory = selected, status = "noop" }; },
            () => { Interlocked.Increment(ref versionReads); return new SetupVersionDisplay { Version = completePackageVersion }; }))
        {
            Observe(form, "package-and-maintenance", () =>
            {
                Pump(() => Find<LinkLabel>(form, "UninstallLink").Enabled && Find<TextBox>(form, "PayloadVersion").Text == "0.1.0 " + FeedbackNotice);
                Find<ComboBox>(form, "GameDirectory").Text = "B";
                Pump(() => Find<LinkLabel>(form, "UninstallLink").Enabled);
                Check(versionReads == 1 && Find<TextBox>(form, "InstalledIdentity").Text == "当前未安装决明" && Find<LinkLabel>(form, "ClearConfigurationLink").Enabled && !Find<Button>(form, "InstallButton").Enabled, "form-package-read-once-independent-target-and-maintenance-pair");
                Check(Find<TextBox>(form, "PayloadVersion").Text == "0.1.0 " + FeedbackNotice && Find<TextBox>(form, "PayloadVersion").AccessibleName == "待安装决明版本", "form-header-product-version-with-accessible-meaning");
                ActivateLink(Find<LinkLabel>(form, "UninstallLink"));
                Pump(() => maintenanceCalls == 1 && Find<LinkLabel>(form, "UninstallLink").Enabled);
                Check(Find<TextBox>(form, "OperationSummary").Text.Contains("无需处理") && Find<TextBox>(form, "PayloadVersion").Text == "0.1.0 " + FeedbackNotice, "form-noop-result-preserved-with-product-version");
                form.Close();
            });
        }
        using (var versionStarted = new ManualResetEventSlim())
        using (var versionRelease = new ManualResetEventSlim())
        using (var versionFinished = new ManualResetEventSlim())
        using (var form = new SetupForm("A", new string[0], Ready, (operation, path) => new SetupResult { operation = operation, gameDirectory = path, status = "partial", message = "保留本次原因" },
            () => { versionStarted.Set(); if (!versionRelease.Wait(5000)) throw new Exception("Unreleased version read"); versionFinished.Set(); return new SetupVersionDisplay { Problem = "读取失败：受控原因" }; }))
        {
            Observe(form, "late-package-failure", () =>
            {
                Pump(() => versionStarted.IsSet && Find<Button>(form, "InstallButton").Enabled);
                Find<Button>(form, "InstallButton").PerformClick();
                Pump(() => Find<TextBox>(form, "OperationSummary").Text.Contains("保留本次原因"));
                versionRelease.Set();
                Pump(() => Find<TextBox>(form, "PayloadVersion").Text.Contains("读取失败"));
                Check(Find<TextBox>(form, "OperationSummary").Text.Contains("保留本次原因") && versionFinished.IsSet, "form-version-failure-cannot-replace-partial-result");
                form.Close();
            });
        }
        using (var readStarted = new ManualResetEventSlim())
        using (var readRelease = new ManualResetEventSlim())
        using (var readFinished = new ManualResetEventSlim())
        using (var form = new SetupForm("A", new string[0], path => { readStarted.Set(); if (!readRelease.Wait(5000)) throw new Exception("Unreleased closed read"); readFinished.Set(); return Ready(path); },
            (operation, path) => { throw new Exception("Closed Form cannot execute"); }))
        {
            Observe(form, "close-in-flight", () => { Pump(() => readStarted.IsSet); form.Close(); readRelease.Set(); Check(readFinished.Wait(3000), "form-closed-read-finishes-without-publishing"); });
        }
        using (var form = new SetupForm("A", new string[0], Ready, (operation, path) => { throw new Exception("Wheel check does not execute a service"); }))
        {
            Observe(form, "arrowless-wheel", () =>
            {
                var log = Find<TextBox>(form, "OperationDetails");
                var content = new System.Text.StringBuilder();
                for (int i = 0; i < 100; i++) content.AppendLine("Line " + i.ToString("D2") + " selected text remains intact");
                log.Text = content.ToString(); log.Select(0, 7); log.ScrollToCaret();
                string selection = log.SelectedText;
                var scroll = log.GetType().GetMethod("ScrollHiddenWheel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Action<int, int> wheel = (delta, lines) => scroll.Invoke(log, new object[] { delta, lines });
                Func<int> first = () => log.GetCharIndexFromPosition(new Point(1, 1));
                int start = first();
                wheel(-60, 3); Check(first() == start, "form-precise-wheel-retains-half-tick");
                wheel(-60, 3); Check(first() > start && log.ScrollBars == ScrollBars.None && log.SelectedText == selection, "form-arrowless-wheel-scrolls-without-changing-selection");
                wheel(120, 3); Check(first() == start, "form-arrowless-wheel-both-directions");
                wheel(-120, 0); Check(first() == start, "form-wheel-respects-disabled-system-setting");
                wheel(-120, 1); int lineAdvance = first(); wheel(120, 1);
                wheel(-120, -1); Check(first() > lineAdvance && log.SelectedText == selection, "form-wheel-native-page-setting");
                form.Close();
            });
        }
        Console.WriteLine("PASS production SetupForm STA controls; simulated read/service seams only; no real install or game process");
    }

    internal static void Preview(string scenario)
    {
        Application.EnableVisualStyles();
        string selected = @"C:\预览夹具\长目录名称用于核对路径输入与复制\Terraria";
        using (var form = new SetupForm(selected, new[] { selected }, path => {
            var state = Ready(path); if (scenario == "empty") { state.Pair = SetupPairState.NotInstalled; state.InstalledVersion = null; } else state.InstalledVersion = "0.0.0-dev+installed-4a36dac8-完整长开发版本-0123456789abcdef0123456789abcdef"; return state;
        }, (operation, path) => {
            Console.WriteLine("SIMULATED operation=" + operation + " target=" + path);
            var result = new SetupResult { operation = operation, gameDirectory = path, status = "partial", exitCode = 3, failedStep = "write-receipt", message = "受控预览：两文件已完成，但记录写入失败。" + new string('长', 160) };
            result.completed.Add("replace-dll"); result.completed.Add("replace-config"); for (int i = 0; i < 25; i++) result.retained.Add("预览保留内容 " + i + "：" + path); return result;
        }, () => new SetupVersionDisplay { Version = "0.0.0-dev+payload-f7a94f82-完整长开发版本-0123456789abcdef0123456789abcdef" }))
        {
            form.Shown += (sender, args) => { Console.WriteLine("SIMULATED SERVICES ONLY DeviceDpi=" + form.DeviceDpi + " Size=" + form.Size + " WorkingArea=" + Screen.FromControl(form).WorkingArea); if (scenario == "narrow") form.Size = form.MinimumSize; };
            Application.Run(form);
        }
    }

    private static SetupTargetState Ready(string path)
    {
        return new SetupTargetState { Directory = path, Version = "1.4.5.8", InstalledVersion = "0.1.0+installed-target", CanInstall = true, CanMaintain = true, Pair = SetupPairState.Installed };
    }

    private static bool FullyVisible(Control control)
    {
        if (!control.Visible || control.ClientSize.Width <= 0 || control.ClientSize.Height <= 0) return false;
        Rectangle visible = control.RectangleToScreen(control.ClientRectangle);
        for (Control parent = control.Parent; parent != null; parent = parent.Parent)
            if (!parent.RectangleToScreen(parent.ClientRectangle).Contains(visible)) return false;
        return true;
    }

    private static bool SummaryReasonVisible(TextBox summary, string reason)
    {
        int start = summary.Text.IndexOf(reason, StringComparison.Ordinal);
        if (start < 0 || !FullyVisible(summary)) return false;
        Point first = summary.GetPositionFromCharIndex(0), last = summary.GetPositionFromCharIndex(start + reason.Length - 1);
        return first.Y >= 0 && last.Y + summary.Font.Height <= summary.ClientSize.Height;
    }

    private static bool NoticeVisible(TextBox caption)
    {
        int start = caption.Text.IndexOf(FeedbackNotice, StringComparison.Ordinal);
        if (start < 0 || !FullyVisible(caption)) return false;
        for (int i = start; i < start + FeedbackNotice.Length; i++)
        {
            Point point = caption.GetPositionFromCharIndex(i);
            if (point.X < 0 || point.X >= caption.ClientSize.Width || point.Y < 0 || point.Y + caption.Font.Height > caption.ClientSize.Height) return false;
        }
        return true;
    }

    private static void Capture(Form form, string name)
    {
        using (var bitmap = new Bitmap(form.Width, form.Height))
        {
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name), System.Drawing.Imaging.ImageFormat.Png);
        }
    }

    private static void ActivateLink(LinkLabel link)
    {
        typeof(LinkLabel).GetMethod("OnLinkClicked", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(link, new object[] { new LinkLabelLinkClickedEventArgs(null) });
    }

    private static void Observe(SetupForm form, string scenario, Action observation)
    {
        Exception failure = null;
        bool entered = false, completed = false;
        // Production starts Application.Run(Form). Show()+standalone DoEvents() can
        // uninstall its temporary synchronization context between test callbacks, so
        // async continuations/timers no longer belong to a persistent UI message loop.
        // Keep the actual production Form in that same hosting contract for the checks.
        form.Shown += (sender, args) => form.BeginInvoke(new Action(() =>
        {
            try { entered = true; observation(); completed = true; }
            catch (Exception error) { failure = error; }
            // Disposing this main Form ends its ApplicationContext. ExitThread here
            // also leaves a thread-wide quit request that can skip the next Form's Run.
            finally { form.Dispose(); }
        }));
        Application.Run(form);
        if (failure != null) throw new Exception("Production Form observation failed.", failure);
        Check(entered && completed, "form-" + scenario + "-observation-completed");
    }

    private static T Find<T>(Control form, string name) where T : Control
    {
        Control[] controls = form.Controls.Find(name, true);
        if (controls.Length != 1) throw new Exception("Expected one control: " + name);
        return (T)controls[0];
    }

    private static void Pump(Func<bool> done)
    {
        var watch = Stopwatch.StartNew();
        while (!done()) { if (watch.ElapsedMilliseconds > 7000) throw new Exception("Timed out waiting for production Form."); Application.DoEvents(); Thread.Sleep(5); }
        Application.DoEvents();
    }

    private static void Check(bool value, string name) { if (!value) throw new Exception("FAIL " + name); Console.WriteLine("PASS " + name); }
}
