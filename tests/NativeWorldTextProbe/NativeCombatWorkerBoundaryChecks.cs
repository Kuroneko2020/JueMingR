using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Terraria;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatWorkerBoundaryChecks
    {
        internal static void ParentProbe(string layout)
        {
            Console.OutputEncoding=new UTF8Encoding(false);
            // A bind acknowledgement distinguishes a functioning lifetime watch
            // from a worker that only rejects an already-dead parent at startup.
            Process child=NativeCombatWorkerChecks.Start(layout);
            File.WriteAllText(Path.Combine(layout,"lifetime-child.txt"),child.Id+"\n"+child.StartTime.ToUniversalTime().Ticks);
            var bound=child.StandardError.ReadLineAsync();
            if(!bound.Wait(10000) || bound.Result!="LIFETIME parent-bound")
            {
                if(!child.HasExited)child.Kill();
                throw new InvalidOperationException("Worker did not acknowledge its exact parent before fixture exit.");
            }
            // Keep its stdin open; exiting the parent during initialization must
            // stop the worker through the independent process-handle watcher.
            Console.WriteLine("BOUND");Console.Out.Flush();
            if(Console.ReadLine()!="EXIT")throw new InvalidOperationException("Lifetime fixture controller disconnected.");
        }
        internal static void ParentExit(string layout)
        {
            var start=new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"\""+Program.Repository+"\" --cpu \""+layout+"\" NpcWorkerParentExit")
            {UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=layout,RedirectStandardInput=true,RedirectStandardError=true,RedirectStandardOutput=true};
            using(var parent=Process.Start(start))
            {
                try
                {
                    var error=parent.StandardError.ReadToEndAsync();var ready=parent.StandardOutput.ReadLineAsync();
                    Require(ready.Wait(10000) && ready.Result=="BOUND","fixture acknowledges bound child");
                    string[] identity=File.ReadAllLines(Path.Combine(layout,"lifetime-child.txt"));
                    using(var child=Process.GetProcessById(int.Parse(identity[0])))
                    {
                        // Retain this exact handle before requesting parent exit;
                        // a promptly reaped child is success, not an OpenProcess
                        // race in the test after its parent has already exited.
                        IntPtr handle=child.Handle;
                        Require(child.StartTime.ToUniversalTime().Ticks==long.Parse(identity[1]),"fixture child identity");
                        parent.StandardInput.WriteLine("EXIT");parent.StandardInput.Flush();
                        Require(parent.WaitForExit(15000),"fixture parent exits within bound");
                        Require(parent.ExitCode==0,"fixture parent exits: "+error.Result);
                        if(!child.WaitForExit(3000)){child.Kill();throw new InvalidOperationException("Owned worker survived its exact parent's exit.");}
                    }
                }
                finally{if(!parent.HasExited)parent.Kill();}
            }
            Console.WriteLine("PASS exact-parent exit stops worker during initialization.");
        }
        internal static void Terrain(Assembly host,string layout,string output)
        {
            var wire=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true);
            var capture=wire.GetMethod("CaptureSceneRegion",BindingFlags.NonPublic|BindingFlags.Static);
            NativeCombatWorkerChecks.Scene(false);
            using(var child=NativeCombatWorkerChecks.Start(layout))
            {
                var errors=child.StandardError.ReadToEndAsync();
                var scene=NativeCombatWorkerChecks.AcquireFrozen(host,child,new[]{0},new int[0],0);
                Func<long,int,bool,byte[]> snapshot=(world,end,references)=>(byte[])capture.Invoke(null,new object[]{scene.Npcs,scene.Projectiles,0,1000L,120,world,0,0,end,end,references});
                byte[] complete=snapshot(1,119,false),first=NativeCombatWorkerChecks.Exchange(child,complete);
                Require(BitConverter.ToInt32(first,0)==NativeCombatWorkerChecks.ExpectedProtocol,"complete fixture accepted");
                byte[] cached=snapshot(1,119,true),second=NativeCombatWorkerChecks.Exchange(child,cached);
                Require(first.Take(first.Length-32).SequenceEqual(second.Take(second.Length-32)),"immutable terrain cache survives private simulation");
                Require(cached.Length<complete.Length-190000,"unchanged chunk values omitted from wire");
                Failure(NativeCombatWorkerChecks.Exchange(child,snapshot(1,31,false)),"terrain",true);
                // A former large snapshot cannot silently supply terrain outside
                // the currently declared set, and another world cannot reuse it.
                Failure(NativeCombatWorkerChecks.Exchange(child,snapshot(2,119,true)),"cache reference",false);
                byte[] corrupted=snapshot(2,119,false);corrupted[corrupted.Length-1]^=1;
                Failure(NativeCombatWorkerChecks.Exchange(child,corrupted),"fingerprint",false);
                byte[] recovered=NativeCombatWorkerChecks.Exchange(child,snapshot(2,119,false));
                Require(first.Take(first.Length-32).SequenceEqual(recovered.Take(recovered.Length-32)),"failed requests do not poison next private scene");
                NativeCombatWorkerChecks.Exit(child,"terrain helper exits");Require(child.ExitCode==0,"terrain helper success");
                File.WriteAllText(Path.Combine(output,"terrain-boundary.log"),"full-bytes="+complete.Length+" cached-bytes="+cached.Length+"\n"+errors.Result,Encoding.UTF8);
            }
            Console.WriteLine("PASS terrain cache / unknown rejection / world version / corrupt values / post-failure recovery.");
        }
        internal static void Effects(Assembly host)
        {
            // Separate from the frozen oracle above: these actual native calls
            // are intercepted before queuing threads or touching any filesystem.
            var type=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeEffectBoundary",true);
            using(var boundary=(IDisposable)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{true},null))
            {
                const BindingFlags flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
                foreach(var method in new[]{typeof(WorldGen).GetMethod("saveToonWhilePlaying",flags),typeof(Terraria.Audio.SoundEngine).GetMethod("Initialize",flags),typeof(Terraria.Social.SocialAPI).GetMethod("Initialize",flags)})
                {
                    type.GetMethod("Begin",flags).Invoke(null,null);bool refused=false;
                    try{method.Invoke(null,new object[method.GetParameters().Length]);}
                    catch(TargetInvocationException error){refused=error.InnerException is InvalidDataException;}
                    Require(refused && (string)type.GetProperty("Failure",flags).GetValue(null)==method.DeclaringType.FullName+"."+method.Name,"external boundary refuses "+method.Name);
                }
                bool cloneRefused=false;
                try{typeof(Terraria.Testing.Cloning.DeepCloneContext).GetConstructors()[0].Invoke(new object[]{false});}
                catch(TargetInvocationException error){cloneRefused=error.InnerException is InvalidDataException;}
                Require(cloneRefused,"dynamic clone context cannot execute");
            }
            Console.WriteLine("PASS actual background-save / audio / social / dynamic-clone entry fences; no real external operation attempted.");
        }
        private static void Failure(byte[] bytes,string expected,bool terrain)
        {
            using(var reader=new BinaryReader(new MemoryStream(bytes,false)))
            {
                Require(reader.ReadInt32()==-NativeCombatWorkerChecks.ExpectedProtocol,"request rejected, no successful future");reader.ReadString();string message=reader.ReadString();
                Require(message.IndexOf(expected,StringComparison.OrdinalIgnoreCase)>=0,"specific failure reason: "+message);
                int x=reader.ReadInt32(),y=reader.ReadInt32();if(terrain)Require(x>=0 && y>=0,"missing coordinate survives native catch");
            }
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
