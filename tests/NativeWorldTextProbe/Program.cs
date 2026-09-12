using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NativeWorldTextProbe
{
    internal static class Program
    {
        private static string references;
        private static Assembly game;
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length != 3) throw new ArgumentException("repository Content output required");
                references = Path.Combine(Path.GetFullPath(args[0]), "external", "TerrariaRefs");
                AppDomain.CurrentDomain.AssemblyResolve += Resolve;
                return Run(args[1], args[2]);
            }
            catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        }
        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name).Name;
            string path = Path.Combine(references, name == "Terraria" ? "Terraria.exe" : name + ".dll");
            if (File.Exists(path)) { var value = Assembly.LoadFrom(path); if (name == "Terraria") game = value; return value; }
            if (game != null)
                foreach (string resource in game.GetManifestResourceNames())
                    if (resource.EndsWith("." + name + ".dll", StringComparison.Ordinal))
                        using (var stream = game.GetManifestResourceStream(resource)) using (var bytes = new MemoryStream())
                        { stream.CopyTo(bytes); return Assembly.Load(bytes.ToArray()); }
            return null;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Run(string content, string output) { return NativeChecks.Run(content, output); }
    }
}
