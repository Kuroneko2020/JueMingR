using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using JueMingR.Setup;

internal static class SetupFormChecks
{
    internal static void Run()
    {
        Application.EnableVisualStyles();
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
            Check(SynchronizationContext.Current is WindowsFormsSynchronizationContext && Application.MessageLoop, "form-persistent-sta-message-loop");
            Check(!Find<Control>(form, "OperationResult").Visible, "form-no-empty-log");
            Check(Find<Control>(form, "ProductTitle").Text == "决明" && form.Icon != null, "form-text-heading-window-icon");
            install.PerformClick(); Pump(() => started.IsSet); install.PerformClick();
            Check(calls == 1 && !path.Enabled && !Find<Button>(form, "BrowseButton").Enabled && !Find<Button>(form, "MaintenanceButton").Enabled, "form-busy-inputs-frozen-single-operation");
            form.Close(); Check(form.Visible && !form.IsDisposed, "form-busy-close-rejected");
            release.Set();
            Pump(() => install.Enabled && Find<TextBox>(form, "OperationSummary").Text.Contains("只完成了一部分"));
            Check(Find<TextBox>(form, "GameIdentity").Text.Contains("已安装决明") && Find<TextBox>(form, "OperationSummary").Text.Contains("受控配置失败"), "form-current-state-does-not-erase-partial-result");
            Check(!Find<TextBox>(form, "OperationDetails").Visible && Find<TextBox>(form, "OperationDetails").Text.Contains("replace-config") && Find<TextBox>(form, "OperationDetails").Text.Contains("目标：A"), "form-details-collapsed-real-fields-and-target");
            form.Size = form.MinimumSize; Application.DoEvents();
            var resultSummary = Find<TextBox>(form, "OperationSummary");
            var detailsLink = Find<LinkLabel>(form, "ToggleDetails");
            Check(SummaryReasonVisible(resultSummary, "受控配置失败") && FullyVisible(detailsLink) && FullyVisible(install), "form-minimum-result-reason-and-details-entry-visible");
            int collapsedMinimum = form.MinimumSize.Height;
            ActivateLink(detailsLink); form.Size = form.MinimumSize; Application.DoEvents();
            var resultDetails = Find<TextBox>(form, "OperationDetails");
            Check(resultDetails.Visible && FullyVisible(resultDetails) && resultDetails.ClientSize.Height >= resultDetails.Font.Height * 2 && resultDetails.ScrollBars == ScrollBars.Vertical && SummaryReasonVisible(resultSummary, "受控配置失败") && FullyVisible(detailsLink) && FullyVisible(install), "form-expanded-minimum-readable-scrollable-details");
            int expandedMinimum = form.MinimumSize.Height;
            ActivateLink(detailsLink); Application.DoEvents();
            Check(!resultDetails.Visible && form.MinimumSize.Height < expandedMinimum && form.MinimumSize.Height <= collapsedMinimum && SummaryReasonVisible(resultSummary, "受控配置失败"), "form-collapse-retains-summary-and-compact-result");
            int resultMinimum = form.MinimumSize.Height;
            path.Text = "B";
            Check(!Find<Control>(form, "OperationResult").Visible && !install.Enabled, "form-target-change-clears-old-result-and-eligibility");
            Pump(() => install.Enabled);
            Check(form.MinimumSize.Height < resultMinimum, "form-target-change-retires-result-minimum-height");
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
            Pump(() => Find<TextBox>(form, "GameIdentity").Text.Contains("Terraria B"));
            Check(!Find<TextBox>(form, "GameIdentity").Text.Contains("Terraria A") && !Find<Button>(form, "InstallButton").Enabled && Find<Button>(form, "MaintenanceButton").Enabled, "form-stale-read-rejected-new-game-maintenance-visible");
            Find<Button>(form, "MaintenanceButton").PerformClick(); Application.DoEvents();
            var menu = (ContextMenuStrip)typeof(SetupForm).GetField("maintenanceMenu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(form);
            Check(menu.Items.Count == 2 && menu.Items[0].Enabled && menu.Items[1].Enabled, "form-maintenance-actions-independent-of-install-gate");
            menu.Close(); form.Close();
            });
        }
        Console.WriteLine("PASS production SetupForm STA controls; simulated read/service seams only; no real install or game process");
    }

    private static SetupTargetState Ready(string path)
    {
        return new SetupTargetState { Directory = path, Version = "1.4.5.8", InstalledVersion = "0.0.0-dev+installed-target", CanInstall = true, CanMaintain = true, Pair = SetupPairState.Installed };
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
