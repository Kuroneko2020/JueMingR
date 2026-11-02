using System;
using System.Diagnostics;
using System.IO;

namespace JueMingR.Setup
{
    internal static class TargetSafety
    {
        internal const string TerrariaHash = "960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3";

        internal static string Validate(string directory, bool installing)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new IOException("请选择 Terraria 游戏目录。");
            string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!Directory.Exists(root)) throw new IOException("游戏目录不存在。");
            CheckPath(root, true, false);
            string exe = Path.Combine(root, "Terraria.exe");
            CheckPath(exe, false, false);
            if (installing && SetupPayload.FileHash(exe) != TerrariaHash) throw new IOException("该目录的 Terraria 不是当前已验证的 Windows 1.4.5.8 客户端。");
            // Removal remains available after a Steam update; this does not claim the new game is supported.
            if (!installing && System.Reflection.AssemblyName.GetAssemblyName(exe).Name != "Terraria") throw new IOException("该目录不是可识别的 Terraria 客户端目录。");
            foreach (string name in new[] { "JueMingR.dll", "Terraria.exe.config", "JueMingR.install.json" }) CheckPath(Path.Combine(root, name), false, true);
            CheckProcesses(root);
            return root;
        }

        // Check every existing ancestor: a leaf check alone misses a junction above the target.
        internal static void CheckPath(string path, bool directory, bool allowMissing)
        {
            string full = Path.GetFullPath(path);
            string cursor = full;
            bool leaf = true;
            while (!string.IsNullOrEmpty(cursor))
            {
                try
                {
                    FileAttributes attributes = File.GetAttributes(cursor);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("目标包含链接或重解析路径：" + cursor);
                    if (leaf && (((attributes & FileAttributes.Directory) != 0) != directory)) throw new IOException("目标不是预期的普通" + (directory ? "目录：" : "文件：") + cursor);
                }
                catch (FileNotFoundException) { if (leaf && !allowMissing) throw; }
                catch (DirectoryNotFoundException) { if (leaf && !allowMissing) throw; }
                leaf = false;
                cursor = Path.GetDirectoryName(cursor);
            }
        }

        internal static void CheckProcesses(string root)
        {
            foreach (string name in new[] { "Terraria", "JueMingR.PredictionWorker" })
            foreach (Process process in Process.GetProcessesByName(name))
            using (process)
            {
                try
                {
                    string executable = process.MainModule.FileName;
                    if (executable.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        throw new IOException("游戏或预测进程仍在使用该目录，请正常退出游戏后重试。");
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { throw new IOException("无法确认正在运行的游戏进程所属目录，请先正常退出游戏。"); }
            }
        }

        internal static void CheckUnlocked(string path)
        {
            CheckPath(path, false, true);
            if (File.Exists(path)) using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        }
    }
}
