using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace NativeWorldTextProbe
{
    // This list is the existing strategy check's formal responsibility set,
    // not a new scheduler. Every child still initializes its original fixture.
    internal static class NativeStrategyScopes
    {
        internal static readonly string[] All={"trend","bee","facing","moving","retarget","switchface","eyes","bats","batform","vultures","hoppers","tortoises","swords","swordpremise","mimics","mimicpremise","mimicbirth","mimicair","jellyfish","jellypremise","runners","support","supportmissing","hungry","hungrydefense","straight","fighterentry","fighterflight","fighterform","fighterfollow","followmoving","parentboundary","parentdepart","parentrecovery","fighterpit","fighterformface","fighterpremise","fighterclocks","fightermoving","rollchoice","roll417","flydead","flyrecoil","flymech","beepet","flytail"};
        internal static void Require(string scope)
        {if(Array.IndexOf(All,scope)<0)throw new ArgumentException("Unknown NpcStrategy subscope: "+scope);}
        private static string Quote(string value)
        {return "\""+System.Text.RegularExpressions.Regex.Replace(System.Text.RegularExpressions.Regex.Replace(value,"(\\\\*)\"","$1$1\\\""),"(\\\\+)$","$1$1")+"\"";}
        internal static int RunAll(string[] args)
        {
            Directory.CreateDirectory(args[2]);Console.WriteLine("STRATEGY requested="+string.Join(",",All)+" inherited-only="+(Environment.GetEnvironmentVariable("JUEMINGR_STRATEGY_ONLY")??"none")+" disposition=ignored-for-full-request");
            var receipts=new System.Collections.Generic.List<string>();
            foreach(string scope in All)
            {
                var start=new ProcessStartInfo(Assembly.GetExecutingAssembly().Location){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
                start.Arguments=Quote(args[0])+" "+Quote(args[1])+" "+Quote(Path.Combine(args[2],scope))+" "+Quote("NpcStrategy:"+scope);
                start.EnvironmentVariables.Remove("JUEMINGR_STRATEGY_ONLY");
                using(var child=Process.Start(start))
                {var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();child.WaitForExit();string log=stdout.GetAwaiter().GetResult()+stderr.GetAwaiter().GetResult();File.WriteAllText(Path.Combine(args[2],scope+".log"),log);Console.Write(log);receipts.Add(scope+",EXECUTED,"+child.ExitCode);File.WriteAllLines(Path.Combine(args[2],"strategy-scopes.csv"),receipts);Console.WriteLine("STRATEGY executed="+scope+" NativeExit="+child.ExitCode);if(child.ExitCode!=0)return child.ExitCode;}
            }
            Console.WriteLine("PASS STRATEGY requested="+All.Length+" executed="+receipts.Count+" reused=0 unexecuted=0 fresh-process-per-scope");return 0;
        }
    }
}
