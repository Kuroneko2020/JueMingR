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
            ShootingClockProof(host);
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
                PrivateImageIdentity(host,type,client,hashes,output);
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
                TailPrefix(host,type,client,capture,output);
                NativeCombatMinionPurposeChecks.Run(host,bytes=>{Require(Send(type,client,bytes),"Minion proof request shares the real worker.");Wait(type,client,3,15000);return Tuple.Create(Take(type,client),(byte[])type.GetProperty("Alignment",Flags).GetValue(client));});
                NativeCombatPurposeChecks.Run(host,bytes=>
                {Require(Send(type,client,bytes),"Purpose request shares the ready real transport.");Wait(type,client,3,15000);return Tuple.Create(Take(type,client),(byte[])type.GetProperty("Alignment",Flags).GetValue(client));},output);
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
        private static void PrivateImageIdentity(Assembly host,Type clientType,object client,string[] hashes,string output)
        {
            // This is the production Program's material-v1 tuple, evaluated
            // once after its authenticated child actually reached Ready.
            string gameHash;using(var stream=File.OpenRead(Path.Combine(Program.Repository,"external/TerrariaRefs/Terraria.exe")))using(var sha=SHA256.Create())gameHash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
            int protocol=(int)host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionPipeProtocol",true).GetField("Protocol",Flags).GetRawConstantValue();
            string key;using(var sha=SHA256.Create())key=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes("material-v1|"+gameHash+"|"+hashes[2]+"|"+hashes[0]+"|"+hashes[1]+"|"+hashes[6]+"|protocol"+protocol+"|x86|"+Environment.Version))).Replace("-","");
            string expected=(string)host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeMaterialIdentity",true).GetMethod("Expected",Flags).Invoke(null,null);
            Require(expected!=null && expected.Length==64,"Current authenticated Host approves the private material digest.");
            string directory=(string)clientType.GetField("cacheDirectory",Flags).GetValue(client);
            string path=Path.Combine(Path.GetFullPath(directory),key+".image");
            string digest;
            using(var stream=File.OpenRead(path))using(var reader=new BinaryReader(stream))
            {
                Require(reader.ReadInt32()==0x504D4331 && Encoding.ASCII.GetString(reader.ReadBytes(64))==key,"Actual production private image stores the candidate-specific material key.");
                int length=reader.ReadInt32();Require(length>0 && length<=64*1024*1024 && stream.Length-stream.Position==length,"Actual private material envelope is complete.");
                byte[] image=reader.ReadBytes(length);using(var sha=SHA256.Create())digest=BitConverter.ToString(sha.ComputeHash(image)).Replace("-","");
                Require(digest==expected,"Actual material bytes match the authenticated Host's approved digest.");
            }
            File.WriteAllLines(Path.Combine(output,"transport-private-image-identity.txt"),new[]{"key="+key,"path="+path,"digest="+digest,"hostHash="+hashes[2],"workerHash="+hashes[0],"configHash="+hashes[1],"harmonyHash="+hashes[6],"gameHash="+gameHash,"protocol="+protocol,"runtime="+Environment.Version});
            Console.WriteLine("PASS production private-image key="+key+" digest="+digest+" path="+path);
        }
        private static object Create(Type type,string layout,string[] hashes){return Activator.CreateInstance(type,Flags,null,new object[]{layout,Path.Combine(Program.Repository,"external/TerrariaRefs/Terraria.exe"),hashes,60000,10000},null);}
        private static void ShootingClockProof(Assembly host)
        {
            var alignment=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true);
            var observe=alignment.GetMethod("Observe",Flags);var difference=alignment.GetMethod("Difference",Flags);
            Func<object> frame=()=>observe.Invoke(null,new object[]{1000L,new[]{0},new int[0],0});
            foreach(int kind in new[]{42,176,231,232,233,234,235})
            {
                NativeCombatWorkerChecks.Scene(false);var n=Terraria.Main.npc[0];n.SetDefaults(kind);n.active=true;n.whoAmI=0;n.target=0;n.ai[1]=80;
                object original=frame();n.ai[1]=81;
                Require(difference.Invoke(null,new[]{original,frame()})==null,"Same original firing mechanism admits only future random clock equivalence: "+kind);
                original.GetType().GetField("IsSample",Flags).SetValue(original,true);
                Require(difference.Invoke(null,new[]{original,frame()})!=null,"Frame zero retains exact observed firing clock: "+kind);
                original.GetType().GetField("IsSample",Flags).SetValue(original,false);
                n.ai[1]=80;int mode=Terraria.Main.netMode;
                try
                {Terraria.Main.netMode=1;object network=frame();network.GetType().GetField("IsSample",Flags).SetValue(network,true);Terraria.Main.netMode=0;
                 Require(difference.Invoke(null,new[]{network,frame()})==null,"Equal real frame-zero timer cannot disagree solely because the private world is offline.");
                 n.ai[1]=81;Require(difference.Invoke(null,new[]{network,frame()})!=null,"Network-observed frame zero still proves raw clock bits.");}
                finally{Terraria.Main.netMode=mode;}
                foreach(float boundary in new[]{0f,101f,129.9999f,130f,-1f})
                {n.ai[1]=boundary;Require(difference.Invoke(null,new[]{original,frame()})!=null,"Reset/sound/next-update firing/unsupported clock stays strict: "+kind+" clock="+boundary);}
                n.ai[1]=81;n.ai[0]+=1;Require(difference.Invoke(null,new[]{original,frame()})!=null,"Other AI phase stays strict.");n.ai[0]-=1;
                n.life--;Require(difference.Invoke(null,new[]{original,frame()})!=null,"Actual damage stays strict.");n.life++;
                n.velocity.X+=1;Require(difference.Invoke(null,new[]{original,frame()})!=null,"Real motion stays strict.");
            }
            NativeCombatWorkerChecks.Scene(false);var other=Terraria.Main.npc[0];other.ai[1]=80;object exact=frame();other.ai[1]=81;
            Require(difference.Invoke(null,new[]{exact,frame()})!=null,"Another original AI mechanism does not inherit timer equivalence.");
            Console.WriteLine("PASS AI_005 firing mechanism future clock / frame-zero raw bits / 0-101-130 boundaries / other AI damage motion strict");
        }
        private static int ContinuationOffset(Assembly host,byte[] proof,long tick)
        {
            using(var stream=new MemoryStream(proof,false))using(var reader=new BinaryReader(stream))
            {
                var frameType=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true);var read=frameType.GetMethod("Read",Flags);
                int count=reader.ReadInt32();object first=read.Invoke(null,new object[]{reader});
                for(int i=1;i<count;i++)read.Invoke(null,new object[]{reader});
                reader.ReadInt64();host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeTerrainUsage",true).GetMethod("Read",Flags).Invoke(null,new object[]{reader});
                int roles=((int[])first.GetType().GetField("Npcs",Flags).GetValue(first)).Length+((int[])first.GetType().GetField("Projectiles",Flags).GetValue(first)).Length;
                for(int i=0;i<roles;i++)reader.ReadBoolean();int offset=(int)stream.Position;reader.ReadInt32();reader.ReadInt32();
                host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeImpactProof",true).GetMethod("Read",Flags).Invoke(null,new object[]{reader,tick,count});
                Require(stream.Position==stream.Length,"Complete transport proof includes the bounded impact history.");return offset;
            }
        }
        private static void TailPrefix(Assembly host,Type clientType,object client,MethodInfo capture,string output)
        {
            foreach(int count in new[]{123,133})
            {
                NativeCombatWorkerChecks.Scene(false);Terraria.Main.npc[0].ai[0]=30-count;
                byte[] request=(byte[])capture.Invoke(null,new object[]{new[]{0},new int[0],0,1000L,180});
                Require(Send(clientType,client,request),"Real short-prefix request.");Wait(clientType,client,3,15000);
                byte[] core=Take(clientType,client),alignment=(byte[])clientType.GetProperty("Alignment",Flags).GetValue(client);
                var actor=Terraria.Main.npc[0];var id=new JueMingR.Platform.Combat.NpcIdentity(1,actor,0,actor.generation,actor.type,actor.netID);
                var resultType=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionResult",true);
                object result=resultType.GetMethod("Read",Flags).Invoke(null,new object[]{core,alignment,id,1000L,1L,false});
                var kind=resultType.GetField("ContinuationKind",Flags);var slot=resultType.GetField("ContinuationSlot",Flags);
                Require(kind!=null && slot!=null,"Successful true prefixes must preserve the uncompleted birth's next-capture hint.");
                Require((int)kind.GetValue(result)==2 && (int)slot.GetValue(result)==0,"Hint preserves the actual first unsampled projectile page.");
                var completed=(System.Collections.Generic.SortedSet<int>)resultType.GetField("Projectiles",Flags).GetValue(result);
                Require(!completed.Contains(0),"Uncompleted birth is not a dependency of the completed prefix.");
                var trajectory=(JueMingR.Platform.Combat.NpcTrajectory)resultType.GetField("Trajectory",Flags).GetValue(result);
                JueMingR.Platform.Combat.NpcTrajectory remaining;
                Require(trajectory.Count==count && trajectory.TryWindow(1000+count-121,120,2,out remaining) && !trajectory.TryWindow(1000+count-120,120,3,out remaining),"123/133 prefix retains strict real-age current+120 boundary.");
                var read=resultType.GetMethod("Read",Flags);
                foreach(var invalid in new[]{new[]{0,0},new[]{-1,-1},new[]{3,0},new[]{1,-1},new[]{1,201},new[]{2,1001}})
                {
                    var broken=(byte[])alignment.Clone();int hint=ContinuationOffset(host,alignment,1000L);Buffer.BlockCopy(BitConverter.GetBytes(invalid[0]),0,broken,hint,4);Buffer.BlockCopy(BitConverter.GetBytes(invalid[1]),0,broken,hint+4,4);
                    Rejected(()=>read.Invoke(null,new object[]{core,broken,id,1000L,1L,false}),"Invalid continuation kind/slot cannot enter a new capture.");
                }
                byte[] continuation=null,continuationProof=null;int age=count==123?2:8;
                NativeCombatWorkerChecks.Compare(core,0,output,"transport-prefix-"+count,expectedHorizon:count-1,nativeStep:(step,npc)=>
                {
                    if(step!=age)return;
                    byte[] fresh=(byte[])capture.Invoke(null,new object[]{new[]{0},new[]{(int)slot.GetValue(result)},0,1000L+age,180});
                    Require(Send(clientType,client,fresh),"The same worker receives a fresh world observation at the actual prefix age.");Wait(clientType,client,3,15000);
                    continuation=Take(clientType,client);continuationProof=(byte[])clientType.GetProperty("Alignment",Flags).GetValue(client);
                });
                object next=read.Invoke(null,new object[]{continuation,continuationProof,id,1000L+age,2L,false});
                var nextPath=(JueMingR.Platform.Combat.NpcTrajectory)resultType.GetField("Trajectory",Flags).GetValue(next);
                Require(nextPath!=null && nextPath.Count>count && ((System.Collections.Generic.SortedSet<int>)resultType.GetField("Projectiles",Flags).GetValue(next)).Contains(0),"Fresh capture consumes the actual birth page and extends beyond the old prefix without stitching frames.");
            }
            // Original harpy AI fires at ai[0]==30. This real initial clock
            // puts the first unsampled projectile allocation at update 150.
            // No synthetic responder or change to the isolation gate is used.
            NativeCombatWorkerChecks.Scene(false);Terraria.Main.npc[0].ai[0]=-120;
            byte[] snapshot=(byte[])capture.Invoke(null,new object[]{new[]{0},new int[0],0,1000L,180});
            Require(Send(clientType,client,snapshot),"Late birth uses the actual ready worker.");Wait(clientType,client,3,15000);
            byte[] prefix=Take(clientType,client),proof=(byte[])clientType.GetProperty("Alignment",Flags).GetValue(client);
            using(var reader=new BinaryReader(new MemoryStream(prefix,false)))
                Require(reader.ReadInt32()==NativeCombatWorkerChecks.ExpectedProtocol && reader.ReadInt64()==1000 && reader.ReadInt32()==0 && reader.ReadInt32()==150,"Only completed updates 0..149 survive birth failure at 150.");
            var n=Terraria.Main.npc[0];var identity=new JueMingR.Platform.Combat.NpcIdentity(1,n,0,n.generation,n.type,n.netID);
            object decoded=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionResult",true).GetMethod("Read",Flags).Invoke(null,new object[]{prefix,proof,identity,1000L,1L,false});
            var path=(JueMingR.Platform.Combat.NpcTrajectory)decoded.GetType().GetField("Trajectory",Flags).GetValue(decoded);
            JueMingR.Platform.Combat.NpcTrajectory window;
            Require(path.TryWindow(1029,120,2,out window) && window.Count==121 && window.CaptureTick==1000 && window.SampleTick==1029,"Age29 genuinely retains current+120 without rewriting capture time.");
            Require(!path.TryWindow(1030,120,3,out window),"Age30 cannot manufacture the missing two-second extent.");
            NativeCombatWorkerChecks.Compare(prefix,0,output,"transport-late-prefix",expectedHorizon:149);
            NativeCombatWorkerChecks.Scene(false);Terraria.Main.npc[0].ai[0]=-90;
            snapshot=(byte[])capture.Invoke(null,new object[]{new[]{0},new int[0],0,1000L,180});
            Require(Send(clientType,client,snapshot),"Early birth negative shares the same worker.");Wait(clientType,client,3,15000);
            using(var reader=new BinaryReader(new MemoryStream(Take(clientType,client),false)))Require(reader.ReadInt32()==-NativeCombatWorkerChecks.ExpectedProtocol,"Failure at120 leaves119 updates and remains a real refusal.");
            Console.WriteLine("PASS true late birth prefix149 / native oracle / age29 current+120 / age30 refusal / early119 refusal");
        }
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
