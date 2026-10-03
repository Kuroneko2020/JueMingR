using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using JueMingR.Platform.Combat;

namespace NativeWorldTextProbe
{
    // The actual authenticated client must prepare the same child that later
    // handles previously unseen targets; no test-side native warmup counts.
    internal static class NativeCombatWorkerPreparationChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        internal static void Run(Assembly host,string layout,string output)
        {
            var type=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWorkerClient",true);
            var names=(string[])type.GetField("PayloadNames",Flags).GetValue(null);
            var hashes=new string[names.Length];
            for(int i=0;i<names.Length;i++)using(var stream=File.OpenRead(Path.Combine(layout,names[i])))using(var sha=SHA256.Create())hashes[i]=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
            object client=Activator.CreateInstance(type,Flags,null,new object[]{layout,Path.Combine(Program.Repository,"external/TerrariaRefs/Terraria.exe"),hashes,60000,10000},null);
            try
            {
                Wait(type,client,1,65000);
                int pid=(int)type.GetProperty("ChildId",Flags).GetValue(client);
                Console.WriteLine("PREPARATION Ready-ms="+type.GetProperty("ReadyMilliseconds",Flags).GetValue(client));
                var capture=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true).GetMethod("CaptureScene",Flags);
                foreach(bool linked in new[]{false,true})
                {
                    int[] slots=NativeCombatWorkerChecks.Scene(linked);int selected=linked?1:0;
                    byte[] frozen=(byte[])capture.Invoke(null,new object[]{slots,new[]{0,1,2},selected,1000L,120});
                    Require((bool)type.GetMethod("TrySend",Flags).Invoke(client,new object[]{frozen}),"Prepared child accepts the first fresh target.");
                    Wait(type,client,3,15000);
                    byte[] result=(byte[])type.GetMethod("TryTake",Flags).Invoke(client,null);
                    string name=linked?"prepared-skeletron":"prepared-harpy";
                    Console.WriteLine(name+" exchange-ms="+type.GetProperty("ExchangeMilliseconds",Flags).GetValue(client));
                    NativeCombatWorkerChecks.Compare(result,selected,output,name);
                    Require((int)type.GetProperty("ChildId",Flags).GetValue(client)==pid,"An unseen type reuses the prepared process.");
                    if(!linked)
                    {
                        var reset=type.GetMethod("ResetWorld",Flags);
                        Require(reset!=null,"World retirement needs an acknowledged private reset without discarding execution preparation.");
                        Require((bool)type.GetMethod("TrySend",Flags).Invoke(client,new object[]{frozen}),"Old world has a request in flight during retirement.");
                        reset.Invoke(client,null);Wait(type,client,1,15000);
                        Require(type.GetMethod("TryTake",Flags).Invoke(client,null)==null,"Retired world response never enters the mailbox.");
                        Require((int)type.GetProperty("ChildId",Flags).GetValue(client)==pid,"Reset retains this prepared child.");
                        var region=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true).GetMethod("CaptureSceneRegion",Flags);
                        byte[] stale=(byte[])region.Invoke(null,new object[]{slots,new[]{0,1,2},selected,1000L,120,1L,0,0,Terraria.Main.maxTilesX-1,Terraria.Main.maxTilesY-1,true});
                        Require((bool)type.GetMethod("TrySend",Flags).Invoke(client,new object[]{stale}),"Probe attempts the old terrain reference after reset.");Wait(type,client,3,15000);
                        using(var reader=new BinaryReader(new MemoryStream((byte[])type.GetMethod("TryTake",Flags).Invoke(client,null),false)))
                        {Require(reader.ReadInt32()==-NativeCombatWorkerChecks.ExpectedProtocol,"Old-world reference must fail after reset.");reader.ReadString();Require(reader.ReadString().Contains("Terrain cache reference is unavailable"),"Refusal identifies cleared world terrain.");}
                    }
                }
                TypedRetirement(host,type,client);
            }
            finally
            {
                type.GetMethod("Stop",Flags).Invoke(client,null);
                var watch=Stopwatch.StartNew();while(!(bool)type.GetProperty("Closed",Flags).GetValue(client)&&watch.ElapsedMilliseconds<6000)Thread.Sleep(10);
                Require((bool)type.GetProperty("Closed",Flags).GetValue(client),"Owned preparation child is reclaimed.");
            }
            string diagnostics=(string)type.GetProperty("Diagnostics",Flags).GetValue(client)??"";
            File.WriteAllText(Path.Combine(output,"preparation-worker.log"),diagnostics);
            int prepared=diagnostics.IndexOf("PREPARED native-methods=",StringComparison.Ordinal),ready=diagnostics.IndexOf("READY native-initialize-ms=",StringComparison.Ordinal);
            Require(prepared>=0 && ready>prepared,"Common execution must be prepared in the serving worker before Ready, without a selected NPC.");
            Require(!diagnostics.Contains("COMPILE first-demand"),"No giant entry preparation remains in the first-target request path.");
            Console.WriteLine("PASS serving worker prepares before Ready / unseen Harpy and linked Skeletron frozen 120 / one child / bounded exit");
        }
        private static void TypedRetirement(Assembly host,Type clientType,object client)
        {
            NativeCombatWorkerChecks.Scene(false);
            var wire=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true);
            var capture=wire.GetMethod("CaptureSceneValues",Flags);var send=clientType.GetMethod("TrySendValues",Flags);var take=clientType.GetMethod("TryTakeResult",Flags);var reset=clientType.GetMethod("ResetWorld",Flags);
            var n=Terraria.Main.npc[0];var identity=new NpcIdentity(1,null,0,n.generation,n.type,n.netID);
            Func<object> snapshot=()=>capture.Invoke(null,new object[]{new[]{0},new[]{0,1,2},0,1000L,120});
            Require((bool)send.Invoke(client,new object[]{snapshot(),identity,1000L,false}),"Typed capture enters the real background encoder.");
            reset.Invoke(client,null);reset.Invoke(client,null);Wait(clientType,client,1,15000);
            Require(take.Invoke(client,null)==null,"Repeated retirement suppresses queued or in-flight typed work.");
            Require((bool)send.Invoke(client,new object[]{snapshot(),identity,1000L,false}),"Fresh typed capture succeeds after the newest world-clear acknowledgement.");Wait(clientType,client,3,15000);
            Require(clientType.GetMethod("TryTake",Flags).Invoke(client,null)==null && (int)clientType.GetProperty("State",Flags).GetValue(client)==3,"The legacy raw consumer cannot consume a decoded mailbox.");
            reset.Invoke(client,null);Wait(clientType,client,1,15000);Require(take.Invoke(client,null)==null,"Already decoded old-world work is also retired before main-thread acceptance.");
            Require((bool)send.Invoke(client,new object[]{snapshot(),identity,1000L,true}),"Typed request remains usable after decoded-result retirement.");Wait(clientType,client,3,15000);
            var reply=take.Invoke(client,null);var result=reply.GetType().GetField("Result",Flags).GetValue(reply);var trajectory=(NpcTrajectory)result.GetType().GetField("Trajectory",Flags).GetValue(result);
            Require(trajectory!=null && trajectory.Count==121 && trajectory.CaptureTick==1000 && trajectory.Identity.Equals(identity) && trajectory.Identity.Token==null,"Background decoder returns the whole frozen horizon with only the original value identity.");
            Require((trajectory.Assumptions&PredictionAssumption.NetworkObservation)!=0,"Typed transport preserves host client observation even though its private world is offline.");
            bool denied=false;try{send.Invoke(client,new object[]{snapshot(),new NpcIdentity(1,n,0,n.generation,n.type,n.netID),1000L,false});}catch(TargetInvocationException e){denied=e.InnerException is ArgumentException;}Require(denied,"A live NPC token cannot enter background work.");
            Console.WriteLine("PASS typed background requests / repeated world retirement / decoded mailbox retirement / fresh ACK recovery / token exclusion");
        }
        private static void Wait(Type type,object client,int state,int timeout)
        {
            var watch=Stopwatch.StartNew();
            while((int)type.GetProperty("State",Flags).GetValue(client)!=state&&watch.ElapsedMilliseconds<timeout)
            {if((int)type.GetProperty("State",Flags).GetValue(client)==4)throw new InvalidOperationException((string)type.GetProperty("Failure",Flags).GetValue(client));Thread.Sleep(10);}
            Require((int)type.GetProperty("State",Flags).GetValue(client)==state,"Prepared transport reaches state "+state);
        }
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
