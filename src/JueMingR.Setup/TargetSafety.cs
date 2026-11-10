using System;
using System.Diagnostics;
using System.IO;

namespace JueMingR.Setup
{
    internal static class TargetSafety
    {
        // Build/test baseline only. A game update is not an installation veto.
        internal const string TerrariaHash = "960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3";

        internal static string Validate(string directory, bool installing)
        {
            string root = ValidateGameDirectory(directory, installing);
            foreach (string name in new[] { "JueMingR.dll", "Terraria.exe.config", "JueMingR.install.json" }) CheckPath(Path.Combine(root, name), false, true);
            CheckProcesses(root);
            return root;
        }

        // Discovery and operations share the same game identity/path rule. Discovery is read-only
        // and may still list a running installation; the operation enforces the process gate.
        internal static string ValidateGameDirectory(string directory, bool installing)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new IOException("请选择 Terraria 游戏目录。");
            string root = NormalizeDirectory(directory);
            if (!Directory.Exists(root)) throw new IOException("游戏目录不存在。");
            CheckPath(root, true, false);
            string exe = Path.Combine(root, "Terraria.exe");
            CheckPath(exe, false, false);
            if (System.Reflection.AssemblyName.GetAssemblyName(exe).Name != "Terraria") throw new IOException("该目录不是可识别的 Terraria 客户端目录。");
            // Installation admits unverified versions; actual ABI gates decide
            // activation. Keep the managed Windows/x86 client boundary, and let
            // removal remain available if a later update changes its platform.
            if (installing)
            {
                if(System.Reflection.AssemblyName.GetAssemblyName(exe).ProcessorArchitecture!=System.Reflection.ProcessorArchitecture.X86)
                    throw new IOException("当前加载组件需要 Windows x86 Terraria 客户端。");
                VerifyWindowsClient(exe);
            }
            return root;
        }

        private static void VerifyWindowsClient(string path)
        {
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var reader=new BinaryReader(stream))
            {
                if(stream.Length<64 || reader.ReadUInt16()!=0x5A4D)throw new IOException("Terraria.exe 不是有效的 Windows 客户端。");
                stream.Position=60;int offset=reader.ReadInt32();
                if(offset<64 || offset>stream.Length-248)throw new IOException("Terraria.exe 的 PE 标头无效。");
                stream.Position=offset;
                if(reader.ReadUInt32()!=0x4550 || reader.ReadUInt16()!=0x14c)throw new IOException("当前加载组件需要 Windows x86 Terraria 客户端。");
                stream.Position=offset+20;int optionalLength=reader.ReadUInt16();int characteristics=reader.ReadUInt16();
                if(optionalLength<224 || (characteristics&0x2000)!=0 || reader.ReadUInt16()!=0x10b)throw new IOException("Terraria.exe 不是有效的 Windows x86 可执行文件。");
                stream.Position=offset+24+68;
                if(reader.ReadUInt16()!=2)throw new IOException("该目录不是 Windows Terraria 图形客户端目录。");
                stream.Position=offset+24+208;
                if(reader.ReadUInt32()==0 || reader.ReadUInt32()<72)throw new IOException("Terraria.exe 缺少受管 CLR 标头。");
            }
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
                    if (IsProcessInDirectory(root, executable))
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

        internal static string NormalizeDirectory(string directory)
        {
            string full = Path.GetFullPath(directory);
            int minimumLength = Path.GetPathRoot(full).Length;
            int length = full.Length;
            // A drive root must never become drive-relative (D:\ -> D:).
            while (length > minimumLength && IsSeparator(full[length - 1])) length--;
            return length == full.Length ? full : full.Substring(0, length);
        }

        internal static bool IsProcessInDirectory(string directory, string executable)
        {
            string root = NormalizeDirectory(directory);
            string prefix = IsSeparator(root[root.Length - 1]) ? root : root + Path.DirectorySeparatorChar;
            return Path.GetFullPath(executable).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSeparator(char value) { return value == Path.DirectorySeparatorChar || value == Path.AltDirectorySeparatorChar; }
    }
}
