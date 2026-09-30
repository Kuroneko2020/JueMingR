using System;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Security.Cryptography;
using JueMingR.TerrariaHost.Combat.Prediction;

namespace JueMingR.PredictionWorker
{
    // This executable deliberately has no game or product project reference.
    // It never calls Terraria's entry point or the product Composition Root.
    internal static class Program
    {
        private const string GameHash=PredictionPipeProtocol.GameHash;
        private static Assembly game;
        private static string payloadDirectory;
        private static readonly System.Collections.Generic.Dictionary<string,Assembly> embedded=new System.Collections.Generic.Dictionary<string,Assembly>(StringComparer.Ordinal);
        private static int Main(string[] args)
        {
            try
            {
                Console.OutputEncoding=new System.Text.UTF8Encoding(false);
                bool anonymous=(args.Length==8 || args.Length==9) && args[0]=="--anonymous-pipes";
                if(!anonymous && (args.Length!=5 || args[0]!="--pipes") || IntPtr.Size!=4 || AppDomain.CurrentDomain.DomainManager!=null)
                    throw new InvalidOperationException("Invalid worker startup.");
                ParentLifetime.Bind(args[3],args[4]);
                Console.Error.WriteLine("LIFETIME parent-bound");
                payloadDirectory=AppDomain.CurrentDomain.BaseDirectory;
                string gamePath=Path.GetFullPath(args[1]);
                if(!string.Equals(Path.GetFileName(gamePath),"Terraria.exe",StringComparison.OrdinalIgnoreCase) || Hash(gamePath)!=GameHash)
                    throw new InvalidOperationException("Game identity mismatch.");
                string hostPath=Path.Combine(payloadDirectory,"JueMingR.TerrariaHost.dll");
                if(args[2].Length!=64 || Hash(hostPath)!=args[2])throw new InvalidOperationException("Payload identity mismatch.");
                AppDomain.CurrentDomain.AssemblyResolve+=Resolve;
                // Load the authenticated image into the resolver-owned context.
                // LoadFrom would also probe the game's directory and could bind
                // a second ReLogic identity beside prepared compile references.
                byte[] gameImage=File.ReadAllBytes(gamePath);
                using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(gameImage)).Replace("-","")!=GameHash)throw new InvalidOperationException("Game changed during startup.");
                string harmonyPath=Path.Combine(payloadDirectory,"0Harmony.dll");
                if(Hash(harmonyPath)!="7B9E756306FA3D7620E02A857C8927A6AB04973F9BD8A77D3866700A6DEAC55C")throw new InvalidOperationException("Prediction metadata engine identity mismatch.");
                var harmony=Assembly.LoadFrom(harmonyPath);
                var host=Assembly.LoadFrom(hostPath);
                var watch=PredictionPipeProtocol.Measure?System.Diagnostics.Stopwatch.StartNew():null;
                var rewrite=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeTileImage",true).GetMethod("Rewrite",BindingFlags.Static|BindingFlags.NonPublic);
                string expected=(string)host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeMaterialIdentity",true).GetMethod("Expected",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
                string key=PredictionMaterialCache.Hash(System.Text.Encoding.UTF8.GetBytes("material-v1|"+GameHash+"|"+args[2]+"|"+Hash(typeof(Program).Assembly.Location)+"|"+Hash(typeof(Program).Assembly.Location+".config")+"|"+Hash(harmonyPath)+"|protocol"+PredictionPipeProtocol.Protocol+"|x86|"+Environment.Version));
                string cache=anonymous && args.Length==9?args[8]:Path.Combine(payloadDirectory,"prediction-materials");
                byte[] guarded=PredictionMaterialCache.Load(cache,key,expected,()=> (byte[])rewrite.Invoke(null,new object[]{gameImage,harmony}));
                if(watch!=null)Console.Error.WriteLine("TERRAIN private-image-ms="+watch.Elapsed.TotalMilliseconds.ToString("F1"));
                game=Assembly.Load(guarded);
                var entry=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWorkerEntry",true);
                var run=entry.GetMethod("Run",BindingFlags.NonPublic|BindingFlags.Static,null,new[]{typeof(Stream),typeof(Stream),typeof(byte[])},null);
                if(run==null)throw new InvalidOperationException("Worker entry mismatch.");
                // Native invariant diagnostics use Console.WriteLine. Keep the
                // original stdout handle exclusively for framed protocol data.
                Stream output=Console.OpenStandardOutput();Console.SetOut(Console.Error);
                if(!anonymous)return (int)run.Invoke(null,new object[]{Console.OpenStandardInput(),output,null});
                using(var inputPipe=new AnonymousPipeClientStream(PipeDirection.In,args[5]))
                using(var outputPipe=new AnonymousPipeClientStream(PipeDirection.Out,args[6]))
                using(var self=System.Diagnostics.Process.GetCurrentProcess())
                {
                    byte[] ready=PredictionPipeProtocol.Ready(Guid.ParseExact(args[7],"N"),int.Parse(args[3],System.Globalization.CultureInfo.InvariantCulture),long.Parse(args[4],System.Globalization.CultureInfo.InvariantCulture),self.Id,self.StartTime.ToUniversalTime().Ticks,args[2]);
                    return (int)run.Invoke(null,new object[]{inputPipe,outputPipe,ready});
                }
            }
            catch(Exception error)
            {
                var actual=error is TargetInvocationException && error.InnerException!=null?error.InnerException:error;
                Console.Error.WriteLine(actual);
                var loader=actual as ReflectionTypeLoadException;
                if(loader!=null)foreach(var failure in loader.LoaderExceptions)Console.Error.WriteLine(failure);
                return 1;
            }
        }
        private static Assembly Resolve(object sender,ResolveEventArgs args)
        {
            var wanted=new AssemblyName(args.Name);string name=wanted.Name;
            if(name=="Terraria")return game;
            // The fixed game hash authenticates its embedded managed libraries.
            // No directory search, downloads or game startup resolver is used.
            if(game!=null)
            {
                string suffix="."+name+".dll";
                foreach(string resource in game.GetManifestResourceNames())
                    if(resource.EndsWith(suffix,StringComparison.Ordinal))
                    lock(embedded)
                    {
                        Assembly prior;if(embedded.TryGetValue(resource,out prior))return prior;
                        using(var input=game.GetManifestResourceStream(resource))using(var data=new MemoryStream())
                        {
                            if(input.Length>32*1024*1024)throw new InvalidDataException("Oversized embedded dependency.");
                            input.CopyTo(data);var dependency=Assembly.Load(data.ToArray());
                            if(!AssemblyName.ReferenceMatchesDefinition(wanted,dependency.GetName()))throw new InvalidDataException("Embedded dependency identity mismatch.");
                            embedded.Add(resource,dependency);return dependency;
                        }
                    }
            }
            return null;
        }
        private static string Hash(string path)
        {using(var input=File.OpenRead(path))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(input)).Replace("-","");}
    }
}
