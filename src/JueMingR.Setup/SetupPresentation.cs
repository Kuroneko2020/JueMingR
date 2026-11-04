using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace JueMingR.Setup
{
    internal enum SetupPairState { NotInstalled, Installed, Incomplete, Unknown }

    internal sealed class SetupTargetState
    {
        internal string Directory;
        internal string Version;
        internal string InstalledVersion;
        internal string InstalledVersionProblem;
        internal string Problem;
        internal string InstallProblem;
        internal bool CanMaintain;
        internal bool CanInstall;
        internal SetupPairState Pair = SetupPairState.Unknown;

        internal string TerrariaCaption { get { return "Terraria 版本：" + (string.IsNullOrWhiteSpace(Version) ? "无法确认" : Version); } }
        internal string InstalledCaption
        {
            get
            {
                if (CanMaintain && Pair == SetupPairState.NotInstalled) return "当前未安装决明";
                string value = !CanMaintain ? "无法确认" : Pair == SetupPairState.NotInstalled ? "未安装" : Pair == SetupPairState.Incomplete ? "安装不完整" : Pair == SetupPairState.Unknown ? "安装状态无法确认" :
                    !string.IsNullOrEmpty(InstalledVersionProblem) ? "读取失败" : string.IsNullOrWhiteSpace(InstalledVersion) || InstalledVersion == "0.0.0.0" ? "无法确认" : InstalledVersion;
                return "已安装决明版本：" + value;
            }
        }
        internal string Feedback { get { return string.Join(Environment.NewLine, new[] { Problem, CanInstall ? null : InstallProblem, InstalledVersionProblem }).Trim(); } }

        internal string Caption
        {
            get
            {
                if (!CanMaintain) return Problem;
                string state = Pair == SetupPairState.Installed ? "已安装决明" : Pair == SetupPairState.NotInstalled ? "未安装决明" : Pair == SetupPairState.Incomplete ? "安装不完整" : "安装状态无法确认";
                string installedVersion = Pair == SetupPairState.Installed ? Environment.NewLine + "决明版本：" + (string.IsNullOrWhiteSpace(InstalledVersion) || InstalledVersion == "0.0.0.0" ? "无法确认" : InstalledVersion) : "";
                return "Terraria " + Version + " · " + state + installedVersion + (CanInstall ? "" : Environment.NewLine + InstallProblem) + (string.IsNullOrEmpty(Problem) ? "" : Environment.NewLine + Problem);
            }
        }

        internal static SetupTargetState Read(string directory)
        {
            var state = new SetupTargetState();
            if (string.IsNullOrWhiteSpace(directory)) { state.Problem = "请先选择游戏目录。"; return state; }
            try
            {
                state.Directory = TargetSafety.ValidateGameDirectory(directory, false);
                state.Version = FileVersionInfo.GetVersionInfo(Path.Combine(state.Directory, "Terraria.exe")).FileVersion;
                state.CanMaintain = true;
                try { TargetSafety.ValidateGameDirectory(state.Directory, true); state.CanInstall = true; }
                catch (Exception error) when (IsReadError(error)) { state.InstallProblem = error.Message; }
                state.ReadPair();
                if (state.Pair == SetupPairState.Installed)
                {
                    // Read the selected installed DLL's informational/product version, not
                    // this Setup's version or the uninformative assembly/file 0.0.0.0.
                    try { state.InstalledVersion = FileVersionInfo.GetVersionInfo(Path.Combine(state.Directory, "JueMingR.dll")).ProductVersion; }
                    catch (Exception error) when (IsReadError(error)) { state.InstalledVersionProblem = "已安装版本读取失败：" + error.Message; }
                }
            }
            catch (Exception error) when (IsReadError(error))
            {
                // A readable game can remain maintainable even when its selected pair cannot be read.
                state.Problem = (state.CanMaintain ? "文件状态暂无法确认：" : "目录不可用：") + error.Message;
            }
            return state;
        }

        private void ReadPair()
        {
            string dll = Path.Combine(Directory, "JueMingR.dll"), config = Path.Combine(Directory, "Terraria.exe.config");
            bool hasDll = Exists(dll), hasConfig = Exists(config);
            byte[] configBytes = hasConfig ? File.ReadAllBytes(config) : null;
            bool rEntry = hasConfig && ConfigOwnership.IsR(configBytes);
            // null from the ownership reader means unknown, never absence. A complete manual
            // pair needs no receipt or expanded runtime, but must match the embedded config hash.
            OwnedSet bundle = hasDll ? OwnedFiles.ReadCurrentBundle(dll, Directory, new List<string>()) : null;
            if (hasDll && bundle == null) { Pair = SetupPairState.Unknown; return; }
            if (!hasDll && !hasConfig) { Pair = SetupPairState.NotInstalled; return; }
            if (!hasConfig) { Pair = SetupPairState.Incomplete; return; }
            if (rEntry)
            {
                Pair = !hasDll ? SetupPairState.Incomplete : SetupPayload.Hash(configBytes) == bundle.ConfigHash ? SetupPairState.Installed : SetupPairState.Unknown;
                return;
            }
            // A valid non-R loader (or no loader) proves this is not an active R pair.
            // Malformed/unreadable XML is not enough evidence to call it uninstalled.
            using (var input = new MemoryStream(configBytes))
            using (var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            {
                XElement root = XDocument.Load(reader).Root;
                Pair = root != null && root.Name == "configuration" ? hasDll ? SetupPairState.Incomplete : SetupPairState.NotInstalled : SetupPairState.Unknown;
            }
        }

        private static bool Exists(string path)
        {
            TargetSafety.CheckPath(path, false, true);
            try { File.GetAttributes(path); return true; }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
        }

        internal static bool IsReadError(Exception error)
        {
            return error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is BadImageFormatException || error is System.Security.SecurityException || error is XmlException || error is NotSupportedException;
        }
    }

    internal static class SetupResultPresentation
    {
        internal static string OperationName(string operation) { return operation == "install" ? "安装" : operation == "uninstall" ? "卸载" : operation == "clear-config" ? "清空配置" : operation; }

        internal static string Summary(SetupResult result)
        {
            string name = OperationName(result.operation);
            switch (result.status)
            {
                case "success": return name + "完成。" + (result.retained.Count == 0 ? "" : "部分内容已保留，可查看详情。");
                case "noop": return name + "：无需处理。" + (result.retained.Count == 0 ? "" : "保留内容见详情。");
                case "partial": return name + "只完成了一部分。" + result.message + (result.operation == "install" ? " 可重新安装修复。" : "");
                case "failure": return name + "未完成：" + result.message;
                default: return name + "结果无法确认：" + result.message;
            }
        }

        internal static string Details(SetupResult result, string selected)
        {
            var text = new StringBuilder();
            text.AppendLine("目标：" + (string.IsNullOrEmpty(result.gameDirectory) ? selected + "（目标核验未完成）" : result.gameDirectory));
            text.AppendLine("操作：" + OperationName(result.operation) + " [" + result.operation + "]");
            text.AppendLine("结果：" + result.status + " · 退出码 " + result.exitCode);
            text.AppendLine(result.message);
            if (!string.IsNullOrEmpty(result.failedStep)) text.AppendLine("失败步骤：" + Step(result.failedStep));
            if (result.completed.Count != 0) { text.AppendLine("已完成："); foreach (string step in result.completed) text.AppendLine("• " + Step(step)); }
            if (result.retained.Count != 0) { text.AppendLine("已保留："); foreach (string item in result.retained) text.AppendLine("• " + item); }
            if (!string.IsNullOrEmpty(result.dllSha256)) text.AppendLine("DLL SHA-256：" + result.dllSha256);
            if (!string.IsNullOrEmpty(result.configSha256)) text.AppendLine("配置 SHA-256：" + result.configSha256);
            return text.ToString().TrimEnd();
        }

        private static string Step(string step)
        {
            string name;
            switch (step)
            {
                case "validate-target": name = "核验目标目录"; break;
                case "verify-payload": name = "核验安装文件"; break;
                case "prepare-new-files": name = "准备新文件"; break;
                case "replace-dll": name = "替换程序文件"; break;
                case "replace-config": name = "替换加载配置"; break;
                case "verify-installed-pair": name = "核验安装后的两文件"; break;
                case "write-receipt": name = "记录安装结果"; break;
                default: name = step.StartsWith("clear:", StringComparison.Ordinal) ? "清除配置文件" : step.StartsWith("remove-", StringComparison.Ordinal) ? "移除自有文件或入口" : "处理步骤"; break;
            }
            return name + " [" + step + "]";
        }
    }
}
