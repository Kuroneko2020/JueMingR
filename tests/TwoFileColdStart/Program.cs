using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

internal static class Program
{
    internal const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    private static Assembly target;
    private static int Main(string[] args)
    {
        try
        {
            Console.OutputEncoding=new System.Text.UTF8Encoding(false);
            Console.WriteLine("MANAGER "+AppDomain.CurrentDomain.DomainManager.GetType().Assembly.FullName);
            AppDomain.CurrentDomain.AssemblyResolve += ResolveReLogic;
            target=Assembly.Load("Terraria, Version=1.4.5.8, Culture=neutral, PublicKeyToken=null");
            target.GetType("Terraria.Program",true).GetField("SavePath",Flags).SetValue(null,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"isolated-user"));
            Assembly.Load("ReLogic, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            var state=AppDomain.CurrentDomain.DomainManager.GetType().GetField("state",Flags);
            var watch=Stopwatch.StartNew();
            while(Convert.ToString(state.GetValue(AppDomain.CurrentDomain.DomainManager))!="Installed")
            {
                string value=Convert.ToString(state.GetValue(AppDomain.CurrentDomain.DomainManager));
                if(value=="Failed"||watch.ElapsedMilliseconds>15000)throw new InvalidOperationException("Production bootstrap state="+value);
                Thread.Sleep(20);
            }
            Assembly host=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="JueMingR.TerrariaHost");
            string directory=Path.GetDirectoryName(host.Location);
            foreach(string name in new[]{"JueMingR.TerrariaHost","0Harmony"})
            {
                Assembly actual=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name==name);
                if(!String.Equals(Path.GetDirectoryName(actual.Location),directory,StringComparison.OrdinalIgnoreCase))throw new Exception("Wrong binding source: "+actual.Location);
                Console.WriteLine("BOUND "+actual.FullName+" "+actual.ManifestModule.ModuleVersionId+" "+actual.Location);
            }
            Console.WriteLine(File.ReadAllText(Path.Combine(directory,"phase-0-s-evidence.log")));
            if(args.Contains("--native"))NativeRun.Run();
            else if(args.Contains("--worker"))Worker(host);
            Console.WriteLine("PASS production CLR automatic manager / cold preparation / exact default-context bindings / real native Hook install");
            return 0;
        }
        catch(Exception exception){Console.Error.WriteLine(exception);return 1;}
    }
    private static Assembly ResolveReLogic(object sender,ResolveEventArgs args)
    {
        if(target==null)return null;
        // Emulate the original executable's own embedded-game-library resolver;
        // product components are never supplied by this test resolver.
        string simple=new AssemblyName(args.Name).Name;
        if(simple.StartsWith("JueMingR",StringComparison.Ordinal)||simple=="0Harmony")return null;
        string resource=target.GetManifestResourceNames().SingleOrDefault(name=>name.EndsWith("."+simple+".dll",StringComparison.Ordinal));
        if(resource==null)return null;
        using(Stream stream=target.GetManifestResourceStream(resource))
        using(var output=new MemoryStream()){stream.CopyTo(output);return Assembly.Load(output.ToArray());}
    }
    private static void Worker(Assembly host)
    {
        RuntimeHelpers.RunClassConstructor(host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeEntityDirectory",true).TypeHandle);
        string hash;using(var stream=File.OpenRead(host.Location))using(var sha=System.Security.Cryptography.SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
        Type identity=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionLaunchIdentity",true);
        object launch=Activator.CreateInstance(identity,Flags,null,new object[]{hash,AppDomain.CurrentDomain.BaseDirectory},null);
        object client=identity.GetMethod("Start",Flags).Invoke(launch,null);Type type=client.GetType();
        try
        {
            var watch=Stopwatch.StartNew();
            while((int)type.GetProperty("State",Flags).GetValue(client)!=1)
            {
                int state=(int)type.GetProperty("State",Flags).GetValue(client);
                if(state==4||watch.ElapsedMilliseconds>65000)throw new Exception("Worker state="+state+" failure="+type.GetProperty("Failure",Flags).GetValue(client));
                Thread.Sleep(20);
            }
            int child=(int)type.GetProperty("ChildId",Flags).GetValue(client);
            if(child<=0||child==Process.GetCurrentProcess().Id)throw new Exception("Worker did not create an independent process.");
            Console.WriteLine("WORKER_READY pid="+child+" directory="+identity.GetField("Directory",Flags).GetValue(launch));
        }
        finally
        {
            type.GetMethod("Stop",Flags).Invoke(client,null);
            var stop=Stopwatch.StartNew();while(!(bool)type.GetProperty("Closed",Flags).GetValue(client)&&stop.ElapsedMilliseconds<10000)Thread.Sleep(20);
            if(!(bool)type.GetProperty("Closed",Flags).GetValue(client))throw new Exception("Worker did not close its process and leases.");
            Console.WriteLine("WORKER_CLOSED");
            Console.WriteLine("WORKER_DIAGNOSTICS "+type.GetProperty("Diagnostics",Flags).GetValue(client));
        }
    }
}

// Uses original native initialization and Update, with owned data and silent
// audio. It does not invoke Host.Install or construct a substitute Runtime.
internal static class NativeRun
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Run()
    {
        Terraria.Main.musicVolume=0;Terraria.Main.soundVolume=0;Terraria.Main.ambientVolume=0;
        var mute=new HarmonyLib.Harmony("JueMingR.Tests.ColdStartAudio");
        mute.Patch(typeof(Terraria.Main).GetMethod("LoadSettings",Program.Flags),postfix:new HarmonyLib.HarmonyMethod(typeof(NativeRun),nameof(Mute)));
        mute.Patch(typeof(Terraria.Main).GetMethod("UpdateAudio",Program.Flags),prefix:new HarmonyLib.HarmonyMethod(typeof(NativeRun),nameof(Mute)));
        Terraria.Main.SkipAssemblyLoad=true;
        Terraria.Social.SocialAPI.Initialize(Terraria.Social.SocialMode.None);
        Terraria.Localization.LanguageManager.Instance.SetLanguage("en-US");Terraria.Lang.InitializeLegacyLocalization();
        try
        {
            using(var game=new ColdGame())
            {
                typeof(Microsoft.Xna.Framework.Game).GetMethod("StartGameLoop",Program.Flags).Invoke(game,null);
                var deadline=Stopwatch.StartNew();while(!game.Complete&&deadline.ElapsedMilliseconds<90000)game.Tick();
                if(!game.Complete)throw new Exception("Original initialized Update did not complete handoff.");
            }
        }
        finally{Terraria.Social.SocialAPI.Shutdown();}
    }
    private static void Mute(){Terraria.Main.musicVolume=0;Terraria.Main.soundVolume=0;Terraria.Main.ambientVolume=0;}
    private sealed class ColdGame:Terraria.Main
    {
        internal bool Complete;
        private int frames;
        internal ColdGame()
        {
            Content.RootDirectory="Content";
            Configuration.Put("Fullscreen",false);Configuration.Put("DisplayWidth",800);Configuration.Put("DisplayHeight",600);
            Configuration.Put("MusicVolume",0f);Configuration.Put("SoundVolume",0f);Configuration.Put("AmbientVolume",0f);
        }
        protected override bool ShowMissingRequirementMessage(Exception exception){throw new InvalidOperationException("Isolated native startup requirement failed.",exception);}
        protected override void Update(Microsoft.Xna.Framework.GameTime time)
        {
            base.Update(time);
            if(++frames<3)return;
            Assembly host=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="JueMingR.TerrariaHost");
            string evidence=File.ReadAllText(Path.Combine(Path.GetDirectoryName(host.Location),"phase-0-s-evidence.log"));
            if(evidence.Split('\n').Count(line=>line.Contains("RUNTIME_HANDOFF_COMPLETE"))!=1||evidence.Contains("|ERROR|"))throw new Exception("Original Update evidence: "+evidence);
            Console.WriteLine("NATIVE_ORIGINAL_UPDATE "+evidence);Complete=true;Exit();
        }
    }
}
