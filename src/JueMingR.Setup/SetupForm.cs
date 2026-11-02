using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace JueMingR.Setup
{
    internal sealed class SetupForm : Form
    {
        private readonly TextBox directory = new TextBox { Dock = DockStyle.Fill, Name = "GameDirectory" };
        private readonly Label identity = new Label { AutoSize = true, Dock = DockStyle.Fill, MaximumSize = new Size(610, 0), Name = "GameIdentity" };
        private readonly TextBox outcome = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Name = "OperationResult" };
        private readonly Button install = new Button { Text = "安装 / 更新", AutoSize = true, Name = "InstallButton" };
        private readonly Button uninstall = new Button { Text = "卸载决明", AutoSize = true, Name = "UninstallButton" };
        private readonly Button browse = new Button { Text = "浏览…", AutoSize = true, Name = "BrowseButton" };
        private bool busy;

        internal SetupForm()
        {
            Text = "决明 R 安装程序"; Name = "JueMingRSetup"; Font = new Font("Microsoft YaHei UI", 9F);
            AutoScaleMode = AutoScaleMode.Font; ClientSize = new Size(680, 410); MinimumSize = new Size(620, 410); StartPosition = FormStartPosition.CenterScreen;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 7 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { Text = "选择 Terraria 游戏目录", AutoSize = true, Margin = new Padding(0, 0, 0, 8) });
            var pathRow = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0) };
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); pathRow.Controls.Add(directory, 0, 0); pathRow.Controls.Add(browse, 1, 0); layout.Controls.Add(pathRow);
            identity.Margin = new Padding(0, 8, 0, 12); layout.Controls.Add(identity);
            layout.Controls.Add(new Label { Text = "安装将替换现有加载配置，其他由该配置加载的工具将停止启用。", AutoSize = true, MaximumSize = new Size(610, 0), Margin = new Padding(0, 0, 0, 10) });
            var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12) }; actions.Controls.Add(install); actions.Controls.Add(uninstall); layout.Controls.Add(actions);
            layout.Controls.Add(outcome); layout.Controls.Add(new Label { Text = "卸载保留笔记、蓝图库、设置和游戏存档。", AutoSize = true, Margin = new Padding(0, 10, 0, 0) }); Controls.Add(layout);
            browse.Click += (sender, args) => { using (var dialog = new FolderBrowserDialog { Description = "选择包含 Terraria.exe 的游戏目录", ShowNewFolderButton = false, SelectedPath = directory.Text }) if (dialog.ShowDialog(this) == DialogResult.OK) directory.Text = dialog.SelectedPath; };
            directory.TextChanged += (sender, args) => ShowIdentity();
            install.Click += async (sender, args) => await RunOperation("install"); uninstall.Click += async (sender, args) => await RunOperation("uninstall");
            FormClosing += (sender, args) => { if (busy) args.Cancel = true; };
            directory.Text = Discover().FirstOrDefault() ?? "";
            ShowIdentity();
        }

        private void ShowIdentity()
        {
            try
            {
                string exe = Path.Combine(directory.Text, "Terraria.exe");
                if (!File.Exists(exe)) { identity.Text = "请选择包含 Terraria.exe 的目录。"; return; }
                string current = Path.Combine(directory.Text, "JueMingR.dll");
                string version = "未安装";
                if (File.Exists(current))
                {
                    version = System.Reflection.AssemblyName.GetAssemblyName(current).Name == "JueMingR" ? System.Diagnostics.FileVersionInfo.GetVersionInfo(current).ProductVersion.Split('+')[0] : "未识别的同名文件";
                    if (string.IsNullOrWhiteSpace(version)) version = "已安装";
                }
                identity.Text = "Terraria " + System.Diagnostics.FileVersionInfo.GetVersionInfo(exe).FileVersion + "　决明：" + version;
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException || error is BadImageFormatException) { identity.Text = "暂时无法读取该目录，请检查路径。"; }
        }

        private async Task RunOperation(string operation)
        {
            string selected = directory.Text; busy = true; install.Enabled = uninstall.Enabled = browse.Enabled = directory.Enabled = false; outcome.Text = "正在处理，请稍候…";
            try
            {
                SetupResult result = await Task.Run(() => new InstallService().Execute(operation, selected));
                outcome.Text = result.message + (result.retained.Count == 0 ? "" : Environment.NewLine + string.Join(Environment.NewLine, result.retained));
            }
            finally { busy = false; install.Enabled = uninstall.Enabled = browse.Enabled = directory.Enabled = true; ShowIdentity(); }
        }

        private static IEnumerable<string> Discover()
        {
            // Only known Steam roots and their declared libraries; there is no drive-wide search.
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
            { string steam = key == null ? null : key.GetValue("SteamPath") as string; if (!string.IsNullOrWhiteSpace(steam)) roots.Add(steam); }
            string standard = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"); roots.Add(standard);
            foreach (string root in roots.ToArray())
            {
                string libraries = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                try
                {
                    if (File.Exists(libraries) && new FileInfo(libraries).Length < 1048576)
                        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(libraries), "\"path\"\\s+\"([^\"]+)\"")) roots.Add(match.Groups[1].Value.Replace("\\\\", "\\"));
                }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            return roots.Select(r => Path.Combine(r, "steamapps", "common", "Terraria")).Where(r => File.Exists(Path.Combine(r, "Terraria.exe"))).ToArray();
        }
    }
}
