using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using HarmonyLib;

namespace NativeWorldTextProbe
{
    // Separate probe process: bind Terraria to authenticated private bytes
    // before any native fixture is JIT compiled. No production IPC test seam.
    internal static class NativeCombatPrivateImageChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        internal static int Run(string layout,string output,bool safetyOnly=false)
        {
            if(safetyOnly)OriginalProcess(output);
            byte[] original=File.ReadAllBytes(Path.Combine(Program.Repository,"external/TerrariaRefs/Terraria.exe"));
            if(Hash(original)!="960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3")throw new InvalidDataException("Private fixture game identity.");
            var host=Assembly.LoadFrom(Path.Combine(layout,"JueMingR.TerrariaHost.dll"));
            string expected=(string)host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeMaterialIdentity",true).GetMethod("Expected",Flags).Invoke(null,null);
            if(expected==null)throw new InvalidDataException("Private fixture requires an approved material digest.");
            byte[] image=null;
            foreach(string path in Directory.EnumerateFiles(Path.Combine(layout,"prediction-materials"),"*.image"))
                using(var stream=File.OpenRead(path))using(var reader=new BinaryReader(stream))
                {
                    if(stream.Length<73 || stream.Length>64*1024*1024+72 || reader.ReadInt32()!=0x504D4331)continue;
                    reader.ReadBytes(64);int count=reader.ReadInt32();if(count!=stream.Length-stream.Position)continue;
                    byte[] candidate=reader.ReadBytes(count);if(Hash(candidate)==expected){image=candidate;break;}
                }
            if(image==null)throw new InvalidDataException("No matching authenticated private image.");
            var native=Assembly.Load(image);Program.UsePrivateGame(native);
            native.GetType("Terraria.Program",true).GetField("SavePath",Flags).SetValue(null,Path.Combine(Path.GetFullPath(output),"private-unused"));
            Console.WriteLine("PRIVATE image="+expected+" host-mvid="+host.ManifestModule.ModuleVersionId);
            return NativeValues(host,output,safetyOnly);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int NativeValues(Assembly host,string output,bool safetyOnly)
        {
            var type=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionSandbox",true);
            using(var sandbox=(IDisposable)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[0],null))
            {
                if(safetyOnly)
                {
                    Terraria.Main.dedServ=false;Terraria.FocusHelper.IsSelectedApplication=false;
                    var queue=typeof(Terraria.Main).GetField("_mainThreadActions",Flags).GetValue(null);var count=queue.GetType().GetProperty("Count");int before=(int)count.GetValue(queue);
                    if(!Terraria.FocusHelper.AllowTaskbarFlash)throw new InvalidOperationException("Notification fixture must allow native flashing.");
                    typeof(Terraria.Main).GetField("_flashNotificationType",Flags).SetValue(null,Terraria.GameContent.GameNotificationType.All);
                    for(int i=0;i<10;i++){Terraria.Main.NotifyOfEvent(Terraria.GameContent.GameNotificationType.SpawnOrDeath);type.GetMethod("ClearWorld",Flags).Invoke(sandbox,null);Terraria.Main.dedServ=false;}
                    if((int)count.GetValue(queue)!=before)throw new InvalidOperationException("Private notification must not enqueue external callbacks across world resets.");
                    Console.WriteLine("PASS actual private presentation notification / repeated requests and world cleanup / no external callback queued");
                    var observed=PresentationValues(host).ToArray();var expected=File.ReadAllLines(Path.Combine(output,"presentation-original.txt"));
                    File.WriteAllLines(Path.Combine(output,"presentation-private.txt"),observed);
                    if(!expected.SequenceEqual(observed))throw new InvalidOperationException("Private Rectangle/Camera bridge differs from independent original values.");
                    Console.WriteLine("PASS actual private Rectangle/Camera bridges / independent CPU native values / padding and unloaded / camera identity and 31 updates");
                    if(Environment.GetEnvironmentVariable("JUEMINGR_NPC_GUARD_COSTS")=="1")GuardCosts(host);
                    return 0;
                }
                if(Environment.GetEnvironmentVariable("JUEMINGR_NPC_HISTORY_TRACE")=="1"){NativeCombatHistoryChecks.Replay(host,sandbox,output);return 0;}
                bool profile=Environment.GetEnvironmentVariable("JUEMINGR_NPC_GUARD_PROFILE")=="1";
                byte[] snapshot=File.ReadAllBytes(Path.Combine(output,profile?"guard-profile.bin":"difference-one-step.bin"));int selected=BitConverter.ToInt32(snapshot,16);
                var timing=new Harmony("JueMingR.Tests.CountNativeFieldAccesses");
                try
                {
                    if(profile)
                    {
                        profileDirectory=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeEntityDirectory",true);
                        profileNames=(Dictionary<int,string>)profileDirectory.GetField("FieldIdentities",Flags).GetValue(null);
                        profileFirst=profileNames.Keys.Min();profileCounts=new long[profileNames.Keys.Max()-profileFirst+1];
                        timing.Patch(profileDirectory.GetMethod("Begin",Flags),postfix:new HarmonyMethod(typeof(NativeCombatPrivateImageChecks).GetMethod("CountGuard",Flags)));
                        foreach(string name in new[]{"NativePredictionAlignment","NativeDependencyTimeline"})
                            timing.Patch(host.GetType("JueMingR.TerrariaHost.Combat.Prediction."+name,true).GetMethod(name=="NativePredictionAlignment"?"Observe":"Record",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPrivateImageChecks).GetMethod("ProfileStart",Flags)),postfix:new HarmonyMethod(typeof(NativeCombatPrivateImageChecks).GetMethod("ProfileEnd",Flags)));
                    }
                    byte[] result=(byte[])type.GetMethod("Predict",Flags).Invoke(sandbox,new object[]{snapshot,true});
                    if(BitConverter.ToInt32(result,0)<0)using(var reader=new BinaryReader(new MemoryStream(result))){reader.ReadInt32();throw new InvalidOperationException(reader.ReadString()+": "+reader.ReadString());}
                    if(profile)
                    {
                        var rows=profileNames.OrderByDescending(p=>profileCounts[p.Key-profileFirst]).Select(p=>p.Value+","+profileCounts[p.Key-profileFirst]).ToArray();
                        File.WriteAllLines(Path.Combine(output,"guard-counts.csv"),new[]{"field,calls"}.Concat(rows));
                        Console.WriteLine("DIAGNOSTIC field-calls="+profileCounts.Sum()+"; counted replay includes instrumentation, not a cost benchmark");
                        Console.WriteLine("DIAGNOSTIC observe-ms="+(profileObserve*1000.0/System.Diagnostics.Stopwatch.Frequency).ToString("F3",CultureInfo.InvariantCulture)+" dependency-record-ms="+(profileDependencies*1000.0/System.Diagnostics.Stopwatch.Frequency).ToString("F3",CultureInfo.InvariantCulture));
                        foreach(string row in rows.Take(12))Console.WriteLine(row);
                    }
                    else File.WriteAllLines(Path.Combine(output,"difference-private.txt"),Dump(Terraria.Main.npc[selected],Terraria.Main.LocalPlayer));
                }
                finally{timing.UnpatchAll(timing.Id);}
            }
            return 0;
        }
        private static Type profileDirectory;
        private static Dictionary<int,string> profileNames;
        private static long[] profileCounts;
        private static int profileFirst;
        private static long profileObserve,profileDependencies;
        private static void ProfileStart(ref long __state){__state=System.Diagnostics.Stopwatch.GetTimestamp();}
        private static void ProfileEnd(MethodBase __originalMethod,long __state)
        {long elapsed=System.Diagnostics.Stopwatch.GetTimestamp()-__state;if(__originalMethod.Name=="Observe")profileObserve+=elapsed;else profileDependencies+=elapsed;}
        private static void CountGuard()
        {
            var handler=typeof(Terraria.Main).Assembly.GetType("JueMingR.PrivatePrediction.EntityGuard",true).GetField("Handler",Flags);
            var original=(Action<object,int,int>)handler.GetValue(null);
            handler.SetValue(null,(Action<object,int,int>)((value,field,mode)=>{profileCounts[field-profileFirst]++;original(value,field,mode);}));
        }
        private static void GuardCosts(Assembly host)
        {
            var directory=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeEntityDirectory",true);
            var npc=Terraria.Main.npc[0];npc.active=true;npc.type=134;npc.whoAmI=0;
            byte[] bytes;using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){directory.GetMethod("Write",Flags).Invoke(null,new object[]{writer});writer.Flush();bytes=stream.ToArray();}
            directory.GetMethod("Reset",Flags).Invoke(null,null);
            using(var reader=new BinaryReader(new MemoryStream(bytes,false)))directory.GetMethod("Read",Flags).Invoke(null,new object[]{reader});
            directory.GetMethod("KnowNpc",Flags).Invoke(null,new object[]{0});directory.GetMethod("Begin",Flags).Invoke(null,null);
            try
            {
                var identities=(Dictionary<int,string>)directory.GetField("FieldIdentities",Flags).GetValue(null);
                int immune=identities.Single(p=>p.Value=="Terraria.NPC.immune").Key;
                var check=(Action<object,int,int>)Delegate.CreateDelegate(typeof(Action<object,int,int>),directory.GetMethod("Check",Flags));
                for(int i=0;i<100000;i++)check(npc,immune,0);
                for(int round=0;round<5;round++)
                {
                    var timer=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<5000000;i++)check(npc,immune,0);timer.Stop();
                    Console.WriteLine("GUARD micro round="+round+" calls=5000000 elapsed-ms="+timer.Elapsed.TotalMilliseconds.ToString("F3",CultureInfo.InvariantCulture));
                }
            }
            finally{directory.GetMethod("Reset",Flags).Invoke(null,null);}
        }
        private static void OriginalProcess(string output)
        {
            Directory.CreateDirectory(output);
            var start=new System.Diagnostics.ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"\""+Program.Repository+"\" unused \""+Path.GetFullPath(output)+"\" NpcPresentationOriginal")
            {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            using(var child=System.Diagnostics.Process.Start(start))
            {
                var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();
                if(!child.WaitForExit(15000)){child.Kill();throw new InvalidOperationException("Bounded original presentation probe timed out.");}
                File.WriteAllText(Path.Combine(output,"presentation-original.log"),stdout.Result+stderr.Result);
                if(child.ExitCode!=0)throw new InvalidOperationException("Original presentation probe failed: "+stderr.Result);
            }
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int PresentationOriginal(string output)
        {Directory.CreateDirectory(output);Terraria.Program.SavePath=Path.Combine(Path.GetFullPath(output),"original-unused");File.WriteAllLines(Path.Combine(output,"presentation-original.txt"),PresentationValues(null));return 0;}
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IEnumerable<string> PresentationValues(Assembly host)
        {
            bool isolated=host!=null;var rows=new List<string>();const int goreType=17;
            var metadata=host?.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeAssetSnapshot",true);
            var guard=isolated?typeof(Terraria.Main).Assembly.GetType("JueMingR.PrivatePrediction.PresentationGuard",true):null;
            foreach(bool loaded in new[]{true,false})
            {
                if(isolated)
                {
                    using(var bytes=new MemoryStream())using(var w=new BinaryWriter(bytes))
                    {w.Write(1);w.Write(Terraria.GameContent.TextureAssets.Npc.Length+goreType);w.Write(true);w.Write(loaded);w.Write(loaded?31:0);w.Write(loaded?47:0);w.Flush();bytes.Position=0;using(var r=new BinaryReader(bytes))metadata.GetMethod("Read",Flags).Invoke(null,new object[]{r});}
                }
                else Terraria.GameContent.TextureAssets.Gore[goreType]=(ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>)typeof(NativeCombatWorkerAssetChecks).GetMethod(loaded?"Loaded":"Asset",Flags).Invoke(null,loaded?new object[]{"rectangle-original",31,47}:new object[]{"rectangle-unloaded"});
                foreach(var frame in new[]{new Terraria.DataStructures.SpriteFrame(2,4,1,2){PaddingX=3,PaddingY=5},new Terraria.DataStructures.SpriteFrame(1,1){PaddingX=9,PaddingY=13}})
                {
                    var gore=new Terraria.Gore{type=goreType,scale=1.5f,position=new Microsoft.Xna.Framework.Vector2(12.75f,-9.5f),Frame=frame};
                    rows.Add("gore "+loaded+" "+gore.Width.ToString("R",CultureInfo.InvariantCulture)+" "+gore.Height.ToString("R",CultureInfo.InvariantCulture)+" "+gore.AABBRectangle);
                    if(!loaded)continue;
                    var asset=Terraria.GameContent.TextureAssets.Gore[goreType];object[] args={frame,asset};
                    var rectangle=isolated?(Microsoft.Xna.Framework.Rectangle)guard.GetMethod("Rectangle",Flags).Invoke(null,args):frame.GetSourceRectangle(asset.Value);
                    rows.Add("rectangle "+rectangle);if(!Equals(frame,isolated?args[0]:gore.Frame))throw new InvalidOperationException("Rectangle bridge mutated its source frame.");
                }
            }
            var prior=Terraria.Main.instance;Terraria.Main.netMode=1;Terraria.Main.screenWidth=800;Terraria.Main.screenHeight=600;
            Terraria.Graphics.CameraModifiers.CameraModifierStack camera;
            var boundary=host?.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeEffectBoundary",true);
            if(isolated){Terraria.Main.instance=null;boundary.GetMethod("Begin",Flags).Invoke(null,null);camera=(Terraria.Graphics.CameraModifiers.CameraModifierStack)boundary.GetField("camera",Flags).GetValue(null);}
            else
            {Terraria.Main.instance=(Terraria.Main)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Terraria.Main));GC.SuppressFinalize(Terraria.Main.instance);camera=new Terraria.Graphics.CameraModifiers.CameraModifierStack();typeof(Terraria.Main).GetField("CameraModifiers",Flags).SetValue(Terraria.Main.instance,camera);}
            var directory=host?.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeEntityDirectory",true);
            try
            {
                var npc=new Terraria.NPC{whoAmI=0,type=668,active=true,position=new Microsoft.Xna.Framework.Vector2(400,300),width=100,height=120};npc.ai[1]=36;
                var invoke=typeof(Terraria.NPC).GetMethod("AI_123_Deerclops_MakeSpikesForward",Flags);
                if(isolated)
                {
                    directory.GetMethod("Reset",Flags).Invoke(null,null);Terraria.Main.npc[0]=npc;
                    using(var bytes=new MemoryStream())using(var w=new BinaryWriter(bytes)){directory.GetMethod("Write",Flags).Invoke(null,new object[]{w});w.Flush();bytes.Position=0;using(var r=new BinaryReader(bytes))directory.GetMethod("Read",Flags).Invoke(null,new object[]{r});}
                    directory.GetMethod("Begin",Flags).Invoke(null,null);bool denied=false;
                    try{invoke.Invoke(npc,new object[]{1,default(Terraria.DataStructures.NPCAimedTarget)});}catch(TargetInvocationException e){denied=e.InnerException is InvalidDataException;}
                    if(!denied)throw new InvalidOperationException("Actual private camera caller must require its NPC page.");
                    directory.GetMethod("KnowNpc",Flags).Invoke(null,new object[]{0});
                }
                var modifiers=(IList)camera.GetType().GetField("_modifiers",Flags).GetValue(camera);
                foreach(int tick in new[]{35,36,37,36})
                {Terraria.Main.UseScreenShake=true;npc.ai[1]=tick;invoke.Invoke(npc,new object[]{1,default(Terraria.DataStructures.NPCAimedTarget)});rows.Add("camera "+tick+" count="+modifiers.Count);}
                if(modifiers.Count!=1)throw new InvalidOperationException("Native camera identity replaces the prior impulse.");
                rows.AddRange(Fields(modifiers[0],"camera-value"));rows.Add("camera-identity "+((Terraria.Graphics.CameraModifiers.ICameraModifier)modifiers[0]).UniqueIdentity);
                for(int step=0;step<31;step++){var position=new Microsoft.Xna.Framework.Vector2(50,60);camera.ApplyTo(ref position);rows.Add("camera-step "+step+" "+position.X.ToString("R",CultureInfo.InvariantCulture)+" "+position.Y.ToString("R",CultureInfo.InvariantCulture)+" "+modifiers.Count);}
                npc.ai[1]=36;invoke.Invoke(npc,new object[]{1,default(Terraria.DataStructures.NPCAimedTarget)});Terraria.Main.UseScreenShake=false;invoke.Invoke(npc,new object[]{1,default(Terraria.DataStructures.NPCAimedTarget)});rows.Add("camera-off count="+modifiers.Count);
                if(modifiers.Count!=0)throw new InvalidOperationException("Disabled screen shake still removes the prior same-identity impulse.");
            }
            finally{directory?.GetMethod("Reset",Flags).Invoke(null,null);Terraria.Main.instance=prior;}
            return rows;
        }
        internal static string[] Dump(object npc,object player)
        {return Fields(npc,"NPC").Concat(Fields(player,"Player")).ToArray();}
        private static IEnumerable<string> Fields(object value,string label)
        {
            var fields=new List<FieldInfo>();for(Type type=value.GetType();type!=null && type!=typeof(object);type=type.BaseType)fields.AddRange(type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly));
            foreach(var field in fields.OrderBy(f=>f.DeclaringType.FullName+"."+f.Name,StringComparer.Ordinal))
            {
                Type type=field.FieldType;if(!(type.IsPrimitive || type.IsEnum || type.IsValueType || type.IsArray))continue;
                yield return label+"."+field.Name+"="+Value(field.GetValue(value));
            }
        }
        private static string Value(object value)
        {
            if(value==null)return "null";
            if(value is Array array)return "["+string.Join(",",array.Cast<object>().Select(Value))+"]";
            if(value is float single)return single.ToString("R",CultureInfo.InvariantCulture);
            if(value is double number)return number.ToString("R",CultureInfo.InvariantCulture);
            return Convert.ToString(value,CultureInfo.InvariantCulture);
        }
        private static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
    }
}
