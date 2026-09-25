using System.Reflection;
using Terraria;
namespace JueMingR.TerrariaHost.About
{
    internal static class RuntimeVersion
    {
        internal static string Read()
        {
            var host = typeof(RuntimeVersion).Assembly;
            var attribute = host.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            string identity = attribute == null ? "构建标识暂不可取得" : attribute.InformationalVersion;
            if (identity.EndsWith("+unidentified", System.StringComparison.Ordinal)) identity += "（构建标识暂不可取得）";
            return "决明R · 开发构建\n构建：" + identity + "\n当前 Terraria：" + typeof(Main).Assembly.GetName().Version;
        }
    }
}
