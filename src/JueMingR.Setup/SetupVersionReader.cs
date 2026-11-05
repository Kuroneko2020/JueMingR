using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace JueMingR.Setup
{
    internal sealed class SetupVersionDisplay
    {
        internal string Version;
        internal string Problem;
        internal string CleanupProblem;
        internal string Caption { get { return (Version ?? Problem ?? "无法确认") + (string.IsNullOrEmpty(CleanupProblem) ? "" : Environment.NewLine + CleanupProblem); } }
    }

    internal static class SetupVersionReader
    {
        internal const string ResourceName = "JueMingR.Setup.Payload.JueMingR.dll";

        internal static SetupVersionDisplay Read()
        {
            return Read(typeof(SetupVersionReader).Assembly, Path.GetTempPath());
        }

        // Display only: read the exact embedded DLL's Win32 ProductVersion without loading
        // its code. The Form owns one finite read; service payload verification is separate.
        internal static SetupVersionDisplay Read(Assembly source, string temporaryRoot)
        {
            var result = new SetupVersionDisplay();
            string temporary = null, expectedHash = null;
            bool created = false;
            try
            {
                byte[] bytes;
                using (Stream resource = source.GetManifestResourceStream(ResourceName))
                {
                    if (resource == null) { result.Problem = "未封装载荷"; return result; }
                    using (var copy = new MemoryStream()) { resource.CopyTo(copy); bytes = copy.ToArray(); }
                }
                expectedHash = SetupPayload.Hash(bytes);
                string root = Path.GetFullPath(temporaryRoot);
                TargetSafety.CheckPath(root, true, false);
                temporary = Path.Combine(root, "juemingr-version-" + Guid.NewGuid().ToString("N") + ".dll");
                TargetSafety.CheckPath(temporary, false, true);
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    created = true;
                    output.Write(bytes, 0, bytes.Length);
                }
                string version = FileVersionInfo.GetVersionInfo(temporary).ProductVersion;
                if (string.IsNullOrWhiteSpace(version) || version == "0.0.0.0") result.Problem = "无法确认";
                else result.Version = version;
            }
            catch (Exception error) when (SetupTargetState.IsReadError(error) || error is System.ComponentModel.Win32Exception)
            { result.Problem = "读取失败：" + error.Message; }
            finally
            {
                if (created)
                {
                    try
                    {
                        // Never recurse or remove a replacement. An interrupted write or
                        // changed file is retained and reported rather than guessed to be ours.
                        TargetSafety.CheckPath(temporary, false, false);
                        if (SetupPayload.FileHash(temporary) != expectedHash) throw new IOException("临时文件内容已变化，已保留：" + temporary);
                        File.Delete(temporary);
                    }
                    catch (Exception error) when (SetupTargetState.IsReadError(error))
                    { result.CleanupProblem = "版本读取临时文件未清理：" + error.Message; }
                }
            }
            return result;
        }
    }
}
