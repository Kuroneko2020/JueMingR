using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatMaterialCacheChecks
    {
        internal static void Inventory()
        {
            string build=Environment.GetEnvironmentVariable("JUEMINGR_NPC_WORKER_BUILD"),configuration=Environment.GetEnvironmentVariable("JUEMINGR_NPC_CONFIGURATION")??"Debug";
            var host=Assembly.LoadFrom(Path.Combine(build,"JueMingR.TerrariaHost","x86",configuration,"net472","JueMingR.TerrariaHost.dll"));
            var type=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeInstructionInventory",true);var inventory=Activator.CreateInstance(type,true);var read=type.GetMethod("Read",BindingFlags.Instance|BindingFlags.NonPublic);int count=0;
            foreach(var owner in new[]{typeof(Terraria.NPC),typeof(Terraria.Projectile),typeof(Terraria.Gore),typeof(Terraria.GameContent.Events.DD2Event)})
            foreach(var method in owner.GetMethods(BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic))
            {
                if(method.GetMethodBody()==null)continue;
                var expected=HarmonyLib.PatchProcessor.GetOriginalInstructions(method).Where(i=>i.opcode.OperandType==System.Reflection.Emit.OperandType.InlineMethod || i.opcode.OperandType==System.Reflection.Emit.OperandType.InlineField).ToArray();
                var actual=((System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>)read.Invoke(inventory,new object[]{method})).ToArray();
                Check(actual.Length==expected.Length && actual.Zip(expected,(a,b)=>a.opcode==b.opcode && Equals(a.operand,b.operand)).All(x=>x),"complete operand discovery matches Harmony: "+method);count++;
            }
            Console.WriteLine("PASS full same-scope method/field operand inventory equals Harmony; methods="+count);
        }
        internal static void Prepare(string output)
        {
            Directory.CreateDirectory(output);
            string build=Environment.GetEnvironmentVariable("JUEMINGR_NPC_WORKER_BUILD"),configuration=Environment.GetEnvironmentVariable("JUEMINGR_NPC_CONFIGURATION")??"Debug";
            var host=Assembly.LoadFrom(Path.Combine(build,"JueMingR.TerrariaHost","x86",configuration,"net472","JueMingR.TerrariaHost.dll"));
            var rewrite=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeTileImage",true).GetMethod("Rewrite",BindingFlags.Static|BindingFlags.NonPublic);
            byte[] original=File.ReadAllBytes(Path.Combine(Program.Repository,"external/TerrariaRefs/Terraria.exe"));
            var harmony=Assembly.LoadFrom(Path.Combine(Program.Repository,"external/Harmony/0Harmony.dll"));
            string expected=null;
            for(int i=0;i<2;i++)
            {
                byte[] value=(byte[])rewrite.Invoke(null,new object[]{original,harmony});string hash;
                using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(value)).Replace("-","");
                Check(expected==null || expected==hash,"independent complete rewrites must be byte deterministic");expected=hash;
            }
            var rows=host.GetCustomAttributes<AssemblyMetadataAttribute>().Where(a=>a.Key.StartsWith("Prediction.Rule.",StringComparison.Ordinal)).OrderBy(a=>a.Key).Select(a=>a.Key.Substring(16)+"|"+a.Value);
            File.WriteAllLines(Path.Combine(output,"material-approval.txt"),new[]{"native-material-v1",expected}.Concat(rows));
            Console.WriteLine("PASS two independent full audited rewrites; deterministic SHA="+expected);
        }
        internal static void Run(string output)
        {
            Directory.CreateDirectory(output);
            string build=Environment.GetEnvironmentVariable("JUEMINGR_NPC_WORKER_BUILD");
            string configuration=Environment.GetEnvironmentVariable("JUEMINGR_NPC_CONFIGURATION")??"Debug";
            var assembly=Assembly.LoadFrom(Path.Combine(build,"JueMingR.PredictionWorker","x86",configuration,"net472","JueMingR.PredictionWorker.exe"));
            var method=assembly.GetType("JueMingR.PredictionWorker.PredictionMaterialCache",true).GetMethod("Load",BindingFlags.Static|BindingFlags.NonPublic);
            byte[] payload={1,2,3,4};string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(payload)).Replace("-","");
            string key=new string('A',64),other=new string('B',64);int builds=0;
            Func<byte[]> buildPayload=()=>{Interlocked.Increment(ref builds);Thread.Sleep(20);return (byte[])payload.Clone();};
            Func<string,string,Func<byte[]>,byte[]> load=(id,expected,builder)=>(byte[])method.Invoke(null,new object[]{output,id,expected,builder});
            load(key,hash,buildPayload);load(key,hash,buildPayload);Check(builds==1,"valid hit never rebuilds");
            string path=Path.Combine(output,key+".image");File.WriteAllBytes(path,new byte[]{4,3,2,1});load(key,hash,buildPayload);Check(builds==2,"corrupt entry rebuilt");
            string interrupted=Path.Combine(output,other+"."+new string('1',32)+".tmp");File.WriteAllBytes(interrupted,payload);
            var one=Task.Run(()=>load(other,hash,buildPayload));var two=Task.Run(()=>load(other,hash,buildPayload));Task.WaitAll(one,two);Check(builds==3,"concurrent identity builds once; partial temporary never reused");
            Check(!File.Exists(interrupted),"terminated publication is reclaimed under its identity lease");
            string old=new string('E',64),busy=Path.Combine(output,old+"."+new string('2',32)+".tmp");File.WriteAllBytes(busy,payload);
            using(var lease=new FileStream(Path.Combine(output,old+".lock"),FileMode.Create,FileAccess.ReadWrite,FileShare.None))
            {load(other,hash,buildPayload);Check(File.Exists(busy),"active different generator is not touched");}
            load(other,hash,buildPayload);Check(!File.Exists(busy),"old component interrupted publication is reclaimed after lease release");
            for(int i=0;i<70;i++)File.WriteAllBytes(Path.Combine(output,other+"."+i.ToString("x32")+".tmp"),payload);
            load(other,hash,buildPayload);Check(Directory.GetFiles(output,"*.tmp").Length==6,"one startup cleanup is bounded to 64 orphan candidates");
            load(other,hash,buildPayload);Check(Directory.GetFiles(output,"*.tmp").Length==0 && Directory.GetFiles(output,"*.lock").Length==0,"later hit drains remaining interrupted data and released lock files");
            bool refused=false;try{load(new string('C',64),hash,()=>new byte[]{9});}catch(TargetInvocationException e){refused=e.InnerException is InvalidDataException;}
            Check(refused && !File.Exists(Path.Combine(output,new string('C',64)+".image")),"untrusted generated bytes are never published");
            load(key,null,buildPayload);load(key,null,buildPayload);Check(builds==5,"uncertified rules cannot reuse even an otherwise valid cache");
            // A copied valid file is not sufficient for another component key.
            File.Copy(path,Path.Combine(output,new string('D',64)+".image"));load(new string('D',64),hash,buildPayload);Check(builds==6,"exact component identity is checked inside file");
            Console.WriteLine("PASS material cache: hit, corrupt, concurrent, interrupted, trusted expected hash, uncertified rules, wrong identity");
        }
        private static void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);}
    }
}
