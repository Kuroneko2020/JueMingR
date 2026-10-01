using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatWorkerTransportChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
        internal static void Run(Assembly host,string layout,string output)
        {
            Type type=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWorkerClient");
            Require(type!=null,"Raw owned-pipe client and post-initialization Ready are required before live capture.");
            Encoding encoding=Console.InputEncoding;
            var names=new[]{"JueMingR.PredictionWorker.exe","JueMingR.PredictionWorker.exe.config","JueMingR.TerrariaHost.dll","JueMingR.Platform.dll","JueMingR.Features.dll","JueMingR.Infrastructure.dll","0Harmony.dll"};
            var hashes=new string[names.Length];for(int i=0;i<names.Length;i++)using(var stream=File.OpenRead(Path.Combine(layout,names[i])))using(var sha=SHA256.Create())hashes[i]=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
            ProtocolBoundaries(host,hashes[2]);
            var wrong=(string[])hashes.Clone();wrong[1]=new string('0',64);
            object refused=Create(type,layout,wrong);Wait(type,refused,4,5000);Closed(type,refused);
            Require((int)type.GetProperty("ChildId",Flags).GetValue(refused)==0,"Wrong config identity is refused before CLR startup, not inside worker Main.");
            object cancelled=Create(type,layout,hashes);type.GetMethod("Stop",Flags).Invoke(cancelled,null);Wait(type,cancelled,6,6000);Closed(type,cancelled);
            object client=Activator.CreateInstance(type,Flags,null,new object[]{layout,Path.Combine(Program.Repository,"external/TerrariaRefs/Terraria.exe"),hashes,60000,10000},null);
            try
            {
                Require(State(type,client)==0 && !Send(type,client,new byte[]{1}),"Starting client cannot accept an already-ageing scene.");
                Wait(type,client,1,65000);
                Require(Console.InputEncoding.CodePage==encoding.CodePage && Equal(Console.InputEncoding.GetPreamble(),encoding.GetPreamble()),"Live pipe startup preserves the parent's console encoding and preamble.");
                int pid=(int)type.GetProperty("ChildId",Flags).GetValue(client);
                Require(pid>0 && pid!=Process.GetCurrentProcess().Id,"Ready binds a distinct owned child.");
                foreach(string name in names)
                {
                    bool locked=false;try{using(var candidate=new FileStream(Path.Combine(layout,name),FileMode.Open,FileAccess.Write,FileShare.ReadWrite)){} }
                    catch(IOException){locked=true;}
                    Require(locked,"Authenticated payload remains write-locked across worker lifetime: "+name);
                }
                NativeCombatWorkerChecks.Scene(false);
                var capture=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true).GetMethod("CaptureScene",Flags);
                // The selected NPC and its bounded spawn slots are frozen only
                // after Ready; no live entity crosses the background boundary.
                byte[] snapshot=(byte[])capture.Invoke(null,new object[]{new[]{0},new[]{0,1,2},0,1000L,120});
                Require(Send(type,client,snapshot) && !Send(type,client,snapshot),"Exactly one in-flight frame; second submission cannot queue or cancel it.");
                Wait(type,client,3,15000);
                byte[] future=Take(type,client);Require(future!=null && State(type,client)==1 && Take(type,client)==null,"Result is transferred once; only then is another request allowed.");
                File.WriteAllBytes(Path.Combine(output,"transport-harpy-frozen.bin"),future);
                NativeCombatWorkerChecks.Compare(future,0,output,"transport-harpy");
                if(Environment.GetEnvironmentVariable("JUEMINGR_NPC_CAPACITY_CHECK")=="1")Capacity(host,type,client,capture,output,pid,snapshot);
                Require(Send(type,client,new byte[]{1}),"Malformed native payload remains bounded within a valid transport envelope.");Wait(type,client,3,15000);
                using(var reader=new BinaryReader(new MemoryStream(Take(type,client),false)))Require(reader.ReadInt32()==-NativeCombatWorkerChecks.ExpectedProtocol,"Native request refusal keeps its matching transport sequence.");
                Require(Send(type,client,snapshot),"Worker remains available after a native request refusal.");Wait(type,client,3,15000);
                byte[] repeated=Take(type,client);Require(repeated.Length==future.Length,"Repeated frozen result length.");
                for(int i=0;i<future.Length-32;i++)Require(repeated[i]==future[i],"Failure then repeat preserves every non-timing result byte.");
                Require((int)type.GetProperty("ChildId",Flags).GetValue(client)==pid,"Repeated requests reuse the same owned child.");
                NativeCombatLiquidChecks.Run(host,output,bytes=>{Require(Send(type,client,bytes),"Frozen liquid request uses the ready production transport.");Wait(type,client,3,15000);return Take(type,client);},()=> (byte[])type.GetProperty("Alignment",Flags).GetValue(client));
                Require(Send(type,client,snapshot),"A final request is queued or executing during Stop.");
                var stop=Stopwatch.StartNew();type.GetMethod("Stop",Flags).Invoke(client,null);
                Require(stop.ElapsedMilliseconds<1000,"Stop schedules background cleanup without waiting for process exit.");
                Wait(type,client,6,6000);Closed(type,client);Require(Take(type,client)==null && !Send(type,client,snapshot),"Stopped owner cannot publish a late reply or accept another frame.");
                Console.WriteLine("PASS raw anonymous pipes / Ready before capture / parent console preserved / one in-flight / native frozen 120 / refusal-repeat / cold and busy Stop / config rejected before Start / bounded framing and wrong identities");
            }
            finally{type.GetMethod("Stop",Flags).Invoke(client,null);}
        }
        private static object Create(Type type,string layout,string[] hashes){return Activator.CreateInstance(type,Flags,null,new object[]{layout,Path.Combine(Program.Repository,"external/TerrariaRefs/Terraria.exe"),hashes,60000,10000},null);}
        private static void Capacity(Assembly host,Type clientType,object client,MethodInfo capture,string output,int pid,byte[] normal)
        {
            // Real sealed scene values, not an alternate protocol responder.
            // These harmless active shots fill the full native dependency
            // timeline and alignment independently below the frame budget.
            NativeCombatWorkerChecks.Scene(false);
            var slots=new int[303];
            for(int i=0;i<300;i++)
            {
                var shot=Terraria.Main.projectile[i];shot.SetDefaults(1);shot.whoAmI=i;shot.active=true;shot.position=new Microsoft.Xna.Framework.Vector2(900,900);shot.velocity=Microsoft.Xna.Framework.Vector2.Zero;
                shot.aiStyle=0;shot.friendly=shot.hostile=false;shot.damage=0;shot.tileCollide=false;shot.ignoreWater=true;shot.timeLeft=10000;
            }
            for(int i=0;i<slots.Length;i++)slots[i]=i;
            byte[] large=(byte[])capture.Invoke(null,new object[]{new[]{0},slots,0,1000L,180});
            File.WriteAllText(Path.Combine(output,"capacity-request.txt"),"requestBytes="+large.Length+" activeShots=300 horizon=180\n");
            Require(Send(clientType,client,large),"Capacity counterexample uses the ready real worker.");Wait(clientType,client,3,30000);
            byte[] refused=Take(clientType,client);
            Require(refused.Length<4096 && (int)clientType.GetProperty("MissingAsset",Flags).GetValue(client)==-1 && clientType.GetProperty("Alignment",Flags).GetValue(client)==null,"Capacity refusal stays small and carries neither asset page request nor alignment.");
            using(var reader=new BinaryReader(new MemoryStream(refused,false)))
            {
                Require(reader.ReadInt32()==-NativeCombatWorkerChecks.ExpectedProtocol,"Full result over capacity is a matching native refusal.");
                string name=reader.ReadString(),reason=reader.ReadString();File.WriteAllText(Path.Combine(output,"capacity-refusal.txt"),name+": "+reason);
                Require(name=="PredictionCapacityException" && reason.Contains("core=") && reason.Contains("alignment="),"Combined capacity has an explicit bounded reason.");
                var sizes=System.Text.RegularExpressions.Regex.Match(reason,@"core=(\d+) alignment=(\d+) payload=(\d+) limit=(\d+)");Require(sizes.Success,"Capacity reason carries real generated sizes.");
                long core=long.Parse(sizes.Groups[1].Value),proof=long.Parse(sizes.Groups[2].Value),payload=long.Parse(sizes.Groups[3].Value),limit=long.Parse(sizes.Groups[4].Value);
                Require(core<limit && proof<limit && payload==core+proof+16 && payload>limit,"Each result part fits independently; wrapper plus combined parts actually exceeds the frame budget.");
                File.WriteAllText(Path.Combine(output,"capacity-sizes.txt"),"core="+core+" alignment="+proof+" resultMetadata=16 envelope=17 payload="+payload+" frame="+(payload+17)+" limit=4194304 refusalCore="+refused.Length+"\n");
                reader.ReadInt32();reader.ReadInt32();Require(reader.ReadInt32()==-1,"Capacity refusal cannot masquerade as a missing page.");
            }
            Require(Send(clientType,client,normal),"Ordinary input runs after capacity refusal without toggling or clearing Failed.");Wait(clientType,client,3,15000);
            byte[] result=Take(clientType,client);using(var reader=new BinaryReader(new MemoryStream(result,false)))Require(reader.ReadInt32()==NativeCombatWorkerChecks.ExpectedProtocol,"Ordinary request succeeds automatically after oversized request.");
            Require((int)clientType.GetProperty("ChildId",Flags).GetValue(client)==pid,"Capacity rejection keeps the healthy owned worker and sequence.");
        }
        private static void Closed(Type type,object value)
        {var watch=Stopwatch.StartNew();while(!(bool)type.GetProperty("Closed",Flags).GetValue(value) && watch.ElapsedMilliseconds<6000)Thread.Sleep(10);Require((bool)type.GetProperty("Closed",Flags).GetValue(value),"Terminal client released its owned process and pipe resources.");}
        private static void ProtocolBoundaries(Assembly host,string hash)
        {
            Type protocol=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionPipeProtocol",true);var nonce=Guid.NewGuid();
            object[] identity={nonce,15,120L,20,130L,hash};byte[] ready=(byte[])protocol.GetMethod("Ready",Flags).Invoke(null,identity);
            Action<byte[]> verify=bytes=>protocol.GetMethod("VerifyReady",Flags).Invoke(null,new object[]{bytes,nonce,15,120L,20,130L,hash});verify(ready);
            foreach(int offset in new[]{0,4,8,12,28,32,40,44,52,84}){byte[] bad=(byte[])ready.Clone();bad[offset]^=1;Rejected(()=>verify(bad),"Ready magic/version/protocol/nonce/parent/child/game/payload identity");}
            byte[] payload={4,5,6};byte[] envelope=(byte[])protocol.GetMethod("Envelope",Flags).Invoke(null,new object[]{payload,7L,true});
            var open=protocol.GetMethod("OpenEnvelope",Flags);
            Require(Equal((byte[])open.Invoke(null,new object[]{envelope,7L,true}),payload),"Exchange carries the exact immutable payload.");
            Rejected(()=>open.Invoke(null,new object[]{envelope,8L,true}),"Wrong request sequence");Rejected(()=>open.Invoke(null,new object[]{envelope,7L,false}),"Wrong message direction");
            Rejected(()=>open.Invoke(null,new object[]{ready,7L,true}),"Duplicate Ready cannot be mistaken for a result");
            var read=protocol.GetMethod("ReadFrame",Flags);
            var wrap=protocol.GetMethod("Result",Flags);int maximum=(int)protocol.GetField("MaximumPayload",Flags).GetRawConstantValue();
            byte[] boundary=(byte[])wrap.Invoke(null,new object[]{new byte[maximum-19],new byte[]{7,8,9},-1});
            byte[] bounded=(byte[])protocol.GetMethod("Envelope",Flags).Invoke(null,new object[]{boundary,9L,true});
            Require(bounded.Length==maximum+17,"Complete legal response can occupy the exact frame limit.");
            using(var stream=new MemoryStream()){protocol.GetMethod("WriteFrame",Flags).Invoke(null,new object[]{stream,bounded});stream.Position=0;Require(Equal((byte[])read.Invoke(null,new object[]{stream}),bounded),"Exact-capacity frame survives production framing intact.");}
            Require(read.Invoke(null,new object[]{new MemoryStream()})==null,"Clean EOF differs from a truncated frame.");
            foreach(byte[] bad in new[]{new byte[]{1},new byte[]{0,0,0,0},new byte[]{1,0,64,0},new byte[]{3,0,0,0,1,2}})
                Rejected(()=>read.Invoke(null,new object[]{new MemoryStream(bad,false)}),"Truncated header/payload and invalid bounded length");
        }
        private static void Rejected(Action action,string context)
        {bool refused=false;try{action();}catch(TargetInvocationException error){refused=error.InnerException is InvalidDataException || error.InnerException is EndOfStreamException;}Require(refused,context+" rejected");}
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private static bool Equal(byte[] a,byte[] b){if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
        private static int State(Type type,object value){return (int)type.GetProperty("State",Flags).GetValue(value);}
        private static bool Send(Type type,object value,byte[] bytes){return (bool)type.GetMethod("TrySend",Flags).Invoke(value,new object[]{bytes});}
        private static byte[] Take(Type type,object value){return (byte[])type.GetMethod("TryTake",Flags).Invoke(value,null);}
        private static void Wait(Type type,object value,int state,int milliseconds)
        {
            var watch=Stopwatch.StartNew();while(State(type,value)!=state && watch.ElapsedMilliseconds<milliseconds)
            {if(State(type,value)==4)throw new InvalidOperationException("Transport fault: "+type.GetProperty("Failure",Flags).GetValue(value));Thread.Sleep(10);}
            Require(State(type,value)==state,"Transport reaches state="+state+" within fixture bound; current="+State(type,value));
        }
    }
}
