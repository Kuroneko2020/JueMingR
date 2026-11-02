using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Threading;

namespace JueMingR.Setup
{
    internal sealed class SetupResult
    {
        public string schema = "JueMingR.Setup.Result.1";
        public string operation;
        public string status;
        public int exitCode;
        public string message;
        public string gameDirectory;
        public string failedStep;
        public List<string> completed = new List<string>();
        public List<string> retained = new List<string>();
        public string dllSha256;
        public string configSha256;
    }

    internal sealed class InstallService
    {
        private readonly Func<SetupPayload> payloadReader;
        // Tests may fail a real commit boundary without adding a production CLI fault switch.
        private readonly Action<string> beforeStep;
        internal InstallService() : this(SetupPayload.Read, null) { }
        internal InstallService(Func<SetupPayload> payloadReader, Action<string> beforeStep)
        { this.payloadReader = payloadReader; this.beforeStep = beforeStep; }

        internal SetupResult Execute(string operation, string directory)
        {
            var result = new SetupResult { operation = operation, failedStep = "validate-target" };
            string dllStage = null, configStage = null;
            Mutex gate = null; bool gateHeld = false;
            try
            {
                string root = TargetSafety.Validate(directory, operation == "install");
                result.gameDirectory = root;
                gate = new Mutex(false, @"Local\JueMingR.Setup." + SetupPayload.Hash(Encoding.UTF8.GetBytes(root.ToUpperInvariant())));
                try { gateHeld = gate.WaitOne(0); } catch (AbandonedMutexException) { gateHeld = true; }
                if (!gateHeld) throw new IOException("该游戏目录已有安装或卸载正在进行，请等待它完成。");
                string dll = Path.Combine(root, "JueMingR.dll"), config = Path.Combine(root, "Terraria.exe.config");
                string receipt = Path.Combine(root, "JueMingR.install.json");
                TargetSafety.CheckUnlocked(dll);
                TargetSafety.CheckUnlocked(config);
                if (OwnedFiles.IsOwnReceipt(receipt)) TargetSafety.CheckUnlocked(receipt);
                if (operation == "install")
                {
                    result.failedStep = "verify-payload";
                    SetupPayload payload = payloadReader();
                    var cleanup = OwnedFiles.ReadLegacy(root, result.retained);
                    result.failedStep = "prepare-new-files";
                    dllStage = Prepare(root, payload.Dll, payload.DllHash);
                    configStage = Prepare(root, payload.Config, payload.ConfigHash);
                    // These are two independent single-file commits. A later failure never promises rollback.
                    Step(result, "replace-dll"); Commit(dllStage, dll); dllStage = null; result.completed.Add("replace-dll");
                    Step(result, "replace-config"); Commit(configStage, config); configStage = null; result.completed.Add("replace-config");
                    result.failedStep = "verify-installed-pair";
                    if (SetupPayload.FileHash(dll) != payload.DllHash || SetupPayload.FileHash(config) != payload.ConfigHash) throw new IOException("安装后文件校验失败。");
                    result.dllSha256 = payload.DllHash; result.configSha256 = payload.ConfigHash;
                    result.completed.Add("verify-installed-pair");
                    if (!File.Exists(receipt) || OwnedFiles.IsOwnReceipt(receipt))
                    {
                        Step(result, "write-receipt");
                        byte[] record = Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(new { schema = "JueMingR.Install.1", dllSha256 = payload.DllHash, configSha256 = payload.ConfigHash, version = payload.Version }));
                        string stage = Prepare(root, record, SetupPayload.Hash(record));
                        try { Commit(stage, receipt); } finally { DeleteStage(stage); }
                        result.completed.Add("write-receipt");
                    }
                    else result.retained.Add("JueMingR.install.json：已有未知记录，已保留；可通过当前程序管理安装。");
                    OwnedFiles.RemoveConfirmed(cleanup, result);
                    result.status = "success"; result.exitCode = 0;
                    result.message = "安装完成，可以照常从 Steam 或原游戏入口启动 Terraria。";
                }
                else if (operation == "uninstall")
                {
                    result.failedStep = "identify-current-files";
                    bool rConfig = File.Exists(config) && ConfigOwnership.IsR(File.ReadAllBytes(config));
                    var cleanup = OwnedFiles.ReadCurrentBundle(dll, root, result.retained);
                    bool rDll = cleanup != null;
                    var legacy = OwnedFiles.ReadLegacy(root, result.retained);
                    bool receiptOwnsDll = OwnedFiles.ReceiptMatches(receipt, dll);
                    // A stale receipt never owns a later third-party config. Only the current R entry is removed.
                    if (rConfig)
                    {
                        Step(result, "remove-config");
                        if ((cleanup != null && cleanup.ConfigHash == SetupPayload.FileHash(config)) || OwnedFiles.ReceiptConfigMatches(receipt, config)) File.Delete(config);
                        else
                        {
                            // A modified shared config still owns its unrelated settings; remove only R's two entry elements.
                            byte[] removed = ConfigOwnership.RemoveEntry(File.ReadAllBytes(config));
                            string stage = Prepare(root, removed, SetupPayload.Hash(removed));
                            try { Commit(stage, config); } finally { DeleteStage(stage); }
                            result.retained.Add("Terraria.exe.config：已移除决明加载入口，保留其他配置。");
                        }
                        result.completed.Add("remove-config");
                    }
                    else if (File.Exists(config)) result.retained.Add("Terraria.exe.config：当前不是可确认的决明入口，已保留。");
                    if (rDll || receiptOwnsDll) { Step(result, "remove-dll"); File.Delete(dll); result.completed.Add("remove-dll"); }
                    else if (File.Exists(dll)) result.retained.Add("JueMingR.dll：无法确认归属，已保留。");
                    if (cleanup != null) OwnedFiles.RemoveConfirmed(cleanup, result);
                    OwnedFiles.RemoveConfirmed(legacy, result);
                    if (OwnedFiles.IsOwnReceipt(receipt)) { File.Delete(receipt); result.completed.Add("remove-receipt"); }
                    result.status = result.completed.Count == 0 ? "noop" : "success"; result.exitCode = 0;
                    result.message = result.completed.Count == 0 ? "未发现可移除的决明程序。" : "决明已移除。笔记、蓝图库、设置和游戏存档均保留；不会恢复其他工具。";
                }
                else throw new ArgumentException("未知操作。");
                result.failedStep = null;
            }
            catch (Exception error) when (!(error is OutOfMemoryException || error is StackOverflowException))
            {
                result.exitCode = result.completed.Count == 0 ? 2 : 3;
                result.status = result.completed.Count == 0 ? "failure" : "partial";
                result.message = (result.completed.Count == 0 ? "操作未完成：" : "操作只完成了一部分：") + error.Message + (result.completed.Count != 0 && operation == "install" ? " 已替换的文件不会恢复；排除上述问题后重新安装可以修复。" : "");
            }
            finally { DeleteStage(dllStage); DeleteStage(configStage); if (gateHeld) gate.ReleaseMutex(); if (gate != null) gate.Dispose(); }
            return result;
        }

        private void Step(SetupResult result, string step) { result.failedStep = step; if (beforeStep != null) beforeStep(step); }
        private static string Prepare(string root, byte[] bytes, string hash)
        {
            string path = Path.Combine(root, ".juemingr-new-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { output.Write(bytes, 0, bytes.Length); output.Flush(true); }
                if (SetupPayload.FileHash(path) != hash) throw new IOException("新文件写入校验失败。");
                return path;
            }
            catch { DeleteStage(path); throw; }
        }

        private static void Commit(string stage, string target)
        {
            TargetSafety.CheckUnlocked(target);
            if (!MoveFileEx(stage, target, 0x1 | 0x8)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "无法替换文件：" + Path.GetFileName(target));
        }

        private static void DeleteStage(string path)
        {
            if (path == null) return;
            try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MoveFileEx(string existing, string replacement, int flags);
    }
}
