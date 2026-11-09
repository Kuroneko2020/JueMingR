using System;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using HarmonyLib;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // This is an isolated test of the evidence boundary, not another gameplay
    // oracle. It reuses the actual Set/selection/native C2 chain unchanged.
    internal static class NativeCombatPrismDiagnosticChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static int nativeCalls;private static bool injecting;
        private static void Conditions(Projectile __instance)
        {
            if(!injecting || __instance.type!=633 || __instance.owner!=Main.myPlayer)return;
            nativeCalls++;Main.hideUI=nativeCalls>=10 && nativeCalls<16;
            Main.npc[2].dontTakeDamage=nativeCalls>=22 && nativeCalls<25;
        }
        internal static void Run(object context)
        {
            var host=Get(context,"Combat").GetType().Assembly;var trace=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.AimLightTrace");
            var type=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.AimLightLog");
            if(type==null)
            {
                Require(trace.GetMethod("PrismToolBefore",Flags)==null && host.GetType("JueMingR.TerrariaHost.F5.F5Shell").GetMethod("PrismCombatInterface",Flags)==null,"ordinary Host excludes diagnostic recorder and all new consumer branches");
                Console.WriteLine("PASS ordinary Host has no prism recorder/gate adapter");return;
            }
            var field=trace.GetField("log",Flags);var original=field.GetValue(null);
            string root=Path.Combine(Terraria.Program.SavePath,"prism-consumer-evidence");Directory.CreateDirectory(root);
            string hash;using(var bytes=File.OpenRead(host.Location))using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");
            var noArm=Activator.CreateInstance(type,Flags,null,new object[]{Path.Combine(root,"off"),hash},null);field.SetValue(null,noArm);
            NativeCombatPrismChainChecks.Run(context);
            Require(!(bool)type.GetProperty("Accepting",Flags).GetValue(noArm) && (int)trace.GetField("prismCount",Flags).GetValue(null)==0 && !(bool)trace.GetField("prismTriggered",Flags).GetValue(null),"unarmed real native 633 callbacks capture no rows, format no diagnostic records and do not trigger");
            string armedRoot=Path.Combine(root,"armed");Directory.CreateDirectory(armedRoot);File.WriteAllText(Path.Combine(armedRoot,"arm.txt"),"prism:"+hash);
            var log=Activator.CreateInstance(type,Flags,null,new object[]{armedRoot,hash},null);field.SetValue(null,log);
            for(int i=0;i<200 && !(bool)type.GetProperty("Accepting",Flags).GetValue(log);i++)Thread.Sleep(10);
            Require((bool)type.GetProperty("Accepting",Flags).GetValue(log),"test arm accepts the actual complete diagnostic Host hash");
            var audit=new Harmony("JueMingR.Tests.PrismConsumerConditions");
            audit.Patch(typeof(Projectile).GetMethod("AI"),prefix:new HarmonyMethod(typeof(NativeCombatPrismDiagnosticChecks),nameof(Conditions)){priority=Priority.First});
            try
            {
                injecting=true;nativeCalls=0;NativeCombatPrismChainChecks.Run(context);injecting=false;Main.hideUI=false;
                for(int i=0;i<200 && !(bool)type.GetProperty("Closed",Flags).GetValue(log);i++)Thread.Sleep(10);
                Require((bool)type.GetProperty("Closed",Flags).GetValue(log),"native parent retirement automatically finishes the finite recorder");
                string text=File.ReadAllText(Directory.GetFiles(armedRoot,"trace.tsv",SearchOption.AllDirectories).Single());
                // Persist the original before any assertions, outside the
                // disposable native SavePath, including on evidence failures.
                Directory.CreateDirectory(Program.OutputDirectory);
                File.WriteAllText(Path.Combine(Program.OutputDirectory,"prism-consumer-trace.tsv"),text);
                ValidateText(text);
                Require((bool)trace.GetField("prismFinished",Flags).GetValue(null),"recorder remains stopped after native retirement");
                Console.WriteLine("PASS actual prism evidence: unarmed OFF / precise real CanInterface refusal / normal borrow-native-inertia-return / bounded native retirement; test-only hideUI counterexample is not the owner's root cause");
            }
            finally{injecting=false;Main.hideUI=false;Main.npc[2].dontTakeDamage=false;audit.UnpatchAll(audit.Id);field.SetValue(null,original);}
        }
        internal static void ReadEvidence(string input,string output)
        {
            Directory.CreateDirectory(output);
            File.Copy(input,Path.Combine(output,"prism-consumer-trace.tsv"));
            ValidateText(File.ReadAllText(input));
            Console.WriteLine("PASS original prism trace predicates with corrected TSV payload reader; offline only, no native chain rerun");
        }
        private static void ValidateText(string text)
        {
                string[] rows=text.Split('\n').Where(line=>line.Contains("\tprism-consumer\t")).ToArray();
                Require(rows.Length>0 && rows.Length<=192 && text.Contains("nativeAi075=") && text.Contains("observedBirths=1"),"same-parent native prefix, real birth and Tool consumer have bounded independent arrivals");
                Require(rows.Any(row=>row.Contains("firstReject=HideUi;") && row.Contains("scope=False") && row.Contains("native=True")),"actual consumer records the precise conditional hideUI refusal rather than a Step-end inference");
                Require(rows.Any(row=>row.Contains("firstReject=TargetReceives;") && (int.Parse(Value(row,"live").Split(',')[5],CultureInfo.InvariantCulture)&4)!=0),"a real consumer target refusal records the same live dontTakeDamage fact with identity, life and dummy qualification");
                Require(rows.Any(row=>row.Contains("firstReject=none;") && row.Contains("point=True:") && row.Contains("scope=True") && row.Contains("native=True") && row.Contains("returned=True:")),"actual accepted consumer records point, borrowed/native cursor, native result and cursor return");
                Require(rows.Where(row=>row.Contains("scope=True") && row.Contains("native=True")).All(row=>Value(row,"borrowed")==Value(row,"nativeCursor") && Value(row,"returned")=="True:"+Value(row,"beforeCursor")),"the actual native steering entry reads the borrowed coordinates and the real outer receipt returns its original cursor");
                Require(rows.Where(row=>row.Contains("scope=True")).All(row=>Value(row,"frame")==Value(row,"sampledFrame") && int.Parse(Value(row,"boundary"),CultureInfo.InvariantCulture)>=0 && int.Parse(Value(row,"journey"),CultureInfo.InvariantCulture)>=0 && int.Parse(Value(row,"text"),CultureInfo.InvariantCulture)>=0 && int.Parse(Value(row,"pointer"),CultureInfo.InvariantCulture)>=0),"accepted consumers contain their exact sample epoch, cached input boundary and evaluated shell aggregate facts");
                Require(rows.Where(row=>row.Contains("scope=True") && row.Contains("expected=True:")).All(row=>double.Parse(row.Split(';').Single(part=>part.StartsWith("steeringErrorSquared=",StringComparison.Ordinal)).Substring("steeringErrorSquared=".Length),CultureInfo.InvariantCulture)<.001),"native post-AI matches its actual consumed cursor and original .92 inertia, including initially incomplete alignment");
                Require(rows.Where(row=>row.Contains("scope=True")).All(row=>!row.Contains("observedFirstFalse=HideUi")),"a prior rejected frame cannot contaminate later successful consumers");
                Require(!text.Contains("\thost-gate\t") && !text.Contains("\tcache-publish\t") && text.Contains("reason=prism-native-parent-retired"),"prism mode suppresses broad trace and ends with its bounded native retirement");
        }
        private static string Value(string row,string key)
        {string prefix=key+"=";string detail=row.Trim().Split('\t')[4];return detail.Split(';').Single(field=>field.StartsWith(prefix,StringComparison.Ordinal)).Substring(prefix.Length);}
    }
}
