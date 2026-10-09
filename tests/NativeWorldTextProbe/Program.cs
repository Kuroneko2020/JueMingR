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
        internal static string Repository { get; private set; }
        internal static string ProductionConfiguration { get; private set; } = "Debug";
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length < 3 || args.Length > 4) throw new ArgumentException("repository Content output [Full|SelectionCpuCosts|SelectionCpuChecks|WorkloadCpu|InformationCpu|GuidanceCpu|GuidanceVisual|DeathCpu|DeathVisual|ExplorationCpu|ExplorationRelease|MapVisual] required");
                // A formal whole-strategy request owns its scope. Inherited
                // debug-only variables cannot silently reduce the obligation.
                // Explicit subscopes retain the original fresh-process boundary.
                if(args.Length==4 && args[3]=="NpcStrategy")return NativeStrategyScopes.RunAll(args);
                if(args.Length==4 && (args[3]=="NpcFoundationContinuous" || args[3]=="NpcStrategyContinuous"))
                {
                    // A whole default-chain obligation is not the implicit
                    // debug subset left in its caller's environment. This is
                    // only the controlled child process, never the user's
                    // machine environment or an explicit strategy scope.
                    foreach(System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
                    {string key=(string)entry.Key;if(key=="JUEMINGR_FOUNDATION_SINGLE" || key.StartsWith("JUEMINGR_FOUNDATION_",StringComparison.Ordinal) && key.EndsWith("ONLY",StringComparison.Ordinal))Environment.SetEnvironmentVariable(key,null);}
                    Environment.SetEnvironmentVariable("JUEMINGR_BASIC_MOTION",null);
                    Environment.SetEnvironmentVariable("JUEMINGR_AIM_LIGHT_SEMANTICS_ONLY",null);
                    Environment.SetEnvironmentVariable("JUEMINGR_AIM_LIGHT_PAIR",null);
                    Console.WriteLine("FOUNDATION requested="+args[3]+" inherited-only-and-prior-shortcuts=cleared");
                }
                if(args.Length==4 && args[3].StartsWith("NpcStrategy:",StringComparison.Ordinal))
                {string part=args[3].Substring("NpcStrategy:".Length);NativeStrategyScopes.Require(part);Environment.SetEnvironmentVariable("JUEMINGR_STRATEGY_ONLY",part);args[3]="NpcStrategy";}
                Repository = Path.GetFullPath(args[0]);
                if (args.Length == 4 && args[3] == "ExplorationRelease") ProductionConfiguration = "Release";
                references = Path.Combine(Repository, "external", "TerrariaRefs");
                AppDomain.CurrentDomain.AssemblyResolve += Resolve;
                if(args.Length==4 && args[3]=="InputBoundary")PreloadInputCandidate();
                if(args.Length==4 && args[3]=="AimLightDiagnostics"){NativeAimLightChecks.Run(args[2]);return 0;}
                if(args.Length==4 && args[3]=="NpcPresentationOriginal")return NativeCombatPrivateImageChecks.PresentationOriginal(args[2]);
                if(args.Length==4 && args[3]=="NpcPrivateValues")return NativeCombatPrivateImageChecks.Run(args[1],args[2]);
                if(args.Length==4 && args[3]=="NpcPrivateSafety")return NativeCombatPrivateImageChecks.Run(args[1],args[2],true);
                return Run(args[1], args[2], args.Length == 4 ? args[3] : "Full");
            }
            catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        }
        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name).Name;
            if(name=="Terraria" && game!=null)return game;
            string path = Path.Combine(references, name == "Terraria" ? "Terraria.exe" : name + ".dll");
            if (name == "0Harmony") path = Path.Combine(Repository, "external", "Harmony", "0Harmony.dll");
            if (File.Exists(path)) { var value = Assembly.LoadFrom(path); if (name == "Terraria") game = value; return value; }
            if (game != null)
                foreach (string resource in game.GetManifestResourceNames())
                    if (resource.EndsWith("." + name + ".dll", StringComparison.Ordinal))
                        using (var stream = game.GetManifestResourceStream(resource)) using (var bytes = new MemoryStream())
                        { stream.CopyTo(bytes); return Assembly.Load(bytes.ToArray()); }
            return null;
        }
        private static void PreloadInputCandidate()
        {
            // Before NativeChecks JIT: a candidate Host alone does not prevent
            // adjacent old Features/Platform from binding this shared probe.
            string host=Environment.GetEnvironmentVariable("JUEMINGR_INPUT_BOUNDARY_CANDIDATE");
            if(String.IsNullOrEmpty(host))host=Path.Combine(Repository,"artifacts/build/Debug/work/bin/JueMingR.TerrariaHost/x86/Debug/net472/JueMingR.TerrariaHost.dll");
            string directory=Path.GetDirectoryName(Path.GetFullPath(host));
            foreach(string name in new[]{"JueMingR.Platform","JueMingR.Features","JueMingR.Infrastructure","JueMingR.TerrariaHost"})
            {
                string path=Path.Combine(directory,name+".dll");
                if(!File.Exists(path))throw new FileNotFoundException("Incomplete input candidate",path);
                // Probe's compile references bind in Default. Authenticate its
                // ordinary adjacent copies against the candidate before Host
                // LoadFrom, so Host reuses that same default type identity.
                Assembly loaded=name=="JueMingR.TerrariaHost"?Assembly.LoadFrom(path):Assembly.Load(AssemblyName.GetAssemblyName(path));
                Assembly disk=Assembly.ReflectionOnlyLoadFrom(path);
                if(loaded.ManifestModule.ModuleVersionId!=disk.ManifestModule.ModuleVersionId || InputAssemblyHash(loaded.Location)!=InputAssemblyHash(path) ||
                    name=="JueMingR.TerrariaHost" && !String.Equals(loaded.Location,path,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Input candidate identity mismatch: "+name+" at "+loaded.Location);
                Console.WriteLine("INPUT-CANDIDATE "+name+" "+loaded.ManifestModule.ModuleVersionId+" "+loaded.Location);
            }
        }
        private static string InputAssemblyHash(string path)
        {
            using(var stream=File.OpenRead(path))using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream));
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Run(string content, string output, string scope) { return NativeChecks.Run(content, output, scope); }
        internal static void UsePrivateGame(Assembly value){if(game!=null)throw new InvalidOperationException("Original game already loaded.");game=value;}
    }
}
