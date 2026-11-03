using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace JueMingR.Setup
{
    internal sealed class SetupPayload
    {
        internal byte[] Dll;
        internal byte[] Config;
        internal string DllHash;
        internal string ConfigHash;
        internal string Version;
        internal string PayloadId;

        internal static SetupPayload Read()
        {
            Assembly source = Assembly.GetExecutingAssembly();
            byte[] dll = Resource(source, "JueMingR.Setup.Payload.JueMingR.dll");
            byte[] config = Resource(source, "JueMingR.Setup.Payload.Terraria.exe.config");
            string[] lines = Encoding.UTF8.GetString(Resource(source, "JueMingR.Setup.Payload.identity")).TrimStart('\ufeff').Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length != 3 || lines[0] != "schema=JueMingR.Setup.Payload.1" || lines[1] != "dll.sha256=" + Hash(dll) || lines[2] != "config.sha256=" + Hash(config))
                throw new InvalidDataException("安装程序内的文件校验失败，请重新取得安装程序。");
            if (!ConfigOwnership.IsR(config)) throw new InvalidDataException("安装程序的加载配置无效。");
            string payloadId;
            AppDomain domain = AppDomain.CreateDomain("JueMingR.Setup.Verify." + Guid.NewGuid().ToString("N"));
            try
            {
                var reader = (BundleMetadataReader)domain.CreateInstanceFromAndUnwrap(typeof(BundleMetadataReader).Assembly.Location, typeof(BundleMetadataReader).FullName);
                if (reader.AssemblyName(dll) != "JueMingR" || !reader.ReadBytes(dll, "JueMingR.Bundle.config").SequenceEqual(config)) throw new InvalidDataException("安装程序的两文件不属于同一载荷。");
                payloadId = Hash(reader.ReadBytes(dll, "JueMingR.Bundle.index")).ToLowerInvariant();
            }
            finally { AppDomain.Unload(domain); }
            return new SetupPayload { Dll = dll, Config = config, DllHash = Hash(dll), ConfigHash = Hash(config), PayloadId = payloadId, Version = "payload-" + payloadId };
        }

        internal static byte[] Resource(Assembly source, string name)
        {
            using (Stream stream = source.GetManifestResourceStream(name))
            {
                if (stream == null) throw new InvalidDataException("此安装程序未封装完整载荷，不能安装。");
                using (var memory = new MemoryStream()) { stream.CopyTo(memory); return memory.ToArray(); }
            }
        }

        internal static string Hash(byte[] bytes)
        {
            using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
        }

        internal static string FileHash(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
        }
    }

    internal static class ConfigOwnership
    {
        internal const string ManagerType = "JueMingR.Bootstrap.Phase0SAppDomainManager";
        internal static byte[] RemoveEntry(byte[] bytes)
        {
            using (var input = new MemoryStream(bytes))
            using (var reader = System.Xml.XmlReader.Create(input, new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null }))
            {
                XDocument document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
                XElement runtime = document.Root.Element("runtime");
                runtime.Element("appDomainManagerAssembly").Remove(); runtime.Element("appDomainManagerType").Remove();
                using (var output = new MemoryStream()) { document.Save(output, SaveOptions.DisableFormatting); return output.ToArray(); }
            }
        }
        internal static bool IsR(byte[] bytes)
        {
            try
            {
                using (var input = new MemoryStream(bytes))
                using (var reader = System.Xml.XmlReader.Create(input, new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null }))
                {
                    XElement root = XDocument.Load(reader).Root;
                    XElement runtime = root == null ? null : root.Element("runtime");
                    if (root == null || root.Name != "configuration" || runtime == null || root.Elements("runtime").Count() != 1) return false;
                    XElement asm = runtime.Element("appDomainManagerAssembly"), type = runtime.Element("appDomainManagerType");
                    if (asm == null || type == null || runtime.Elements("appDomainManagerAssembly").Count() != 1 || runtime.Elements("appDomainManagerType").Count() != 1 || (string)type.Attribute("value") != ManagerType) return false;
                    return new AssemblyName((string)asm.Attribute("value")).Name == "JueMingR";
                }
            }
            catch (Exception error) when (error is System.Xml.XmlException || error is ArgumentException || error is FileLoadException) { return false; }
        }
    }
}
