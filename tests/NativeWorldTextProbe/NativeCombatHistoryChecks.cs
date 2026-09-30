using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;

namespace NativeWorldTextProbe
{
    // Explicit diagnostic only: retain the actual transport bytes and native
    // history. Its allocation/patching costs are never performance evidence.
    internal static class NativeCombatHistoryChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static readonly List<byte[]> requests=new List<byte[]>();
        private static readonly SortedDictionary<long,string[]> frames=new SortedDictionary<long,string[]>();
        private static readonly SortedDictionary<long,Vector2> cameras=new SortedDictionary<long,Vector2>();
        private static readonly List<KeyValuePair<long,string>> dust=new List<KeyValuePair<long,string>>();
        private static string destination;private static Assembly host;
        private static bool enabled,replaying,matchCamera;private static int capturedBytes,currentNpc=-1;
        internal static bool Completed{get;private set;}
        private static object Get(object owner,string name)=>owner.GetType().GetField(name,Flags)?.GetValue(owner)??owner.GetType().GetProperty(name,Flags).GetValue(owner);
        private static void Patch(Harmony h,MethodBase method,string before=null,string after=null)
        {h.Patch(method,before==null?null:new HarmonyMethod(typeof(NativeCombatHistoryChecks).GetMethod(before,Flags)),after==null?null:new HarmonyMethod(typeof(NativeCombatHistoryChecks).GetMethod(after,Flags)));}
        internal static void Start(Harmony patches,Assembly assembly,string output)
        {
            if(Environment.GetEnvironmentVariable("JUEMINGR_NPC_HISTORY_TRACE")!="1")return;
            host=assembly;destination=output;enabled=true;
            Patch(patches,host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeCapturedValues",true).GetMethod("Encode",Flags),after:nameof(Encoded));
            Patch(patches,host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionSession",true).GetMethod("Receive",Flags),nameof(Receiving),nameof(Received));
            TraceHooks(patches);Console.WriteLine("DIAGNOSTIC actual request/history retention enabled; this run is not a cost benchmark");
        }
        private static void TraceHooks(Harmony patches)
        {
            Patch(patches,typeof(NPC).GetMethod("UpdateNPC",Flags),nameof(NpcStart),nameof(NpcEnd));
            Patch(patches,typeof(Dust).GetMethod("NewDust",Flags),nameof(DustStart),nameof(DustEnd));
            Patch(patches,host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true).GetMethod("Observe",Flags),nameof(Observe));
        }
        private static void Encoded(byte[] __result)
        {
            if(!enabled || Completed)return;
            lock(requests)
            {
                if(__result.Length>32*1024*1024-capturedBytes)throw new InvalidOperationException("Bounded history diagnostic request capacity exceeded.");
                // Encode owns a new array; the transport does not mutate it.
                requests.Add(__result);capturedBytes+=__result.Length;
            }
        }
        private static void Receiving(object __instance,object response,ref object[] __state)
        {if(enabled && !Completed)__state=new[]{Get(__instance,"pending"),Get(response,"Result")};}
        private static void Received(object __instance,object[] __state)
        {
            if(__state==null || __state[0]==null || Completed)return;
            object request=__state[0];if(((int[])Get(request,"Npcs")).Length<60)return;
            object measurement=((IEnumerable)Get(__instance,"Measurements")).Cast<object>().Last();string outcome=(string)Get(measurement,"Outcome");
            if(!outcome.StartsWith("history ",StringComparison.Ordinal) || !outcome.Contains("NPC slot="))return;
            long tick=(long)Get(request,"Tick");Completed=true;
            lock(requests)using(var writer=new BinaryWriter(File.Create(Path.Combine(destination,"history-requests.bin"))))
            {writer.Write(requests.Count);foreach(byte[] bytes in requests){writer.Write(bytes.Length);writer.Write(bytes);}}
            var resultFrames=(Array)Get(__state[1],"Frames");
            using(var writer=new BinaryWriter(File.Create(Path.Combine(destination,"history-response.bin"))))
            {
                writer.Write(resultFrames.Length);var write=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true).GetMethod("Write",Flags);
                foreach(object frame in resultFrames)write.Invoke(null,new object[]{writer,frame});
            }
            var observed=((IEnumerable)Get(request,"History")).Cast<object>().ToArray();
            using(var writer=new BinaryWriter(File.Create(Path.Combine(destination,"history-native-proof.bin"))))
            {
                writer.Write(observed.Length);var write=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true).GetMethod("Write",Flags);
                foreach(object frame in observed)write.Invoke(null,new object[]{writer,frame});
            }
            File.WriteAllText(Path.Combine(destination,"history-target.txt"),tick+Environment.NewLine+outcome);
            File.WriteAllLines(Path.Combine(destination,"history-original.txt"),frames.Where(p=>p.Key>=tick).SelectMany(p=>p.Value));
            File.WriteAllLines(Path.Combine(destination,"history-dust-original.txt"),dust.Where(p=>p.Key>tick).Select(p=>p.Value));
            File.WriteAllLines(Path.Combine(destination,"history-camera.txt"),cameras.Where(p=>p.Key>tick).Select(p=>p.Key+","+p.Value.X.ToString("R",CultureInfo.InvariantCulture)+","+p.Value.Y.ToString("R",CultureInfo.InvariantCulture)));
            Console.WriteLine("DIAGNOSTIC retained actual rejected request tick="+tick+" "+outcome+" with ordered terrain baselines and response proof");
        }
        private static void Observe(long tick)
        {
            if(!enabled || Completed)return;
            frames[tick]=Main.npc.Where(n=>n.active && n.type>=134 && n.type<=136).Select(n=>State(tick,n)).ToArray();
            while(frames.Count>80)frames.Remove(frames.Keys.First());
        }
        private static string State(long tick,NPC n)
        {
            return string.Join(",",new[]{tick.ToString(),n.whoAmI.ToString(),n.type.ToString(),n.generation.ToString(),n.alpha.ToString(),Bits(n.position.X),Bits(n.position.Y),Bits(n.velocity.X),Bits(n.velocity.Y),Bits(n.rotation),n.direction.ToString(),n.spriteDirection.ToString(),n.target.ToString()}.Concat(n.ai.Select(Bits)).Concat(n.localAI.Select(Bits)));
        }
        private static string Bits(float value)=>BitConverter.ToUInt32(BitConverter.GetBytes(value),0).ToString("X8",CultureInfo.InvariantCulture);
        private static void NpcStart(NPC __instance)
        {
            if(!enabled || Completed)return;currentNpc=__instance.whoAmI;long tick=Main.GameUpdateCount;
            if(replaying){Vector2 camera;if(matchCamera && cameras.TryGetValue(tick,out camera))Main.screenPosition=camera;}
            else{cameras[tick]=Main.screenPosition;while(cameras.Count>80)cameras.Remove(cameras.Keys.First());}
        }
        private static void NpcEnd(){currentNpc=-1;}
        private static string RandomState()
        {
            var random=Main.rand;var type=random.GetType();return Get(random,"inext")+":"+string.Join("/",(int[])type.GetField("SeedArray",Flags).GetValue(random));
        }
        private static void DustStart(Vector2 Position,int Type,ref string __state)
        {
            if(!enabled || Completed || Type!=182)return;
            int padding=(int)(400f*(1f-Dust.dCount));bool intersects=new Rectangle((int)(Main.screenPosition.X-padding),(int)(Main.screenPosition.Y-padding),Main.screenWidth+padding*2,Main.screenHeight+padding*2).Intersects(new Rectangle((int)Position.X,(int)Position.Y,10,10));
            int free=Array.FindIndex(Main.dust,d=>!d.active);
            __state=Main.GameUpdateCount+","+currentNpc+","+Bits(Position.X)+","+Bits(Position.Y)+","+Bits(Main.screenPosition.X)+","+Bits(Main.screenPosition.Y)+","+intersects+","+free+","+Bits(Dust.dCount)+","+Main.maxDustToDraw+","+RandomState();
        }
        private static void DustEnd(int __result,string __state)
        {
            if(__state==null)return;dust.Add(new KeyValuePair<long,string>(Main.GameUpdateCount,__state+","+__result+","+RandomState()));
            if(dust.Count>3000)dust.RemoveRange(0,dust.Count-3000);
        }
        internal static void Replay(Assembly assembly,object sandbox,string output)
        {
            host=assembly;long target=long.Parse(File.ReadAllLines(Path.Combine(output,"history-target.txt"))[0],CultureInfo.InvariantCulture);
            using(var reader=new BinaryReader(File.OpenRead(Path.Combine(output,"history-requests.bin"))))
            {int count=reader.ReadInt32();for(int i=0;i<count;i++)requests.Add(reader.ReadBytes(reader.ReadInt32()));}
            foreach(string line in File.ReadAllLines(Path.Combine(output,"history-camera.txt")))
            {var parts=line.Split(',');cameras.Add(long.Parse(parts[0],CultureInfo.InvariantCulture),new Vector2(float.Parse(parts[1],CultureInfo.InvariantCulture),float.Parse(parts[2],CultureInfo.InvariantCulture)));}
            replaying=true;var patches=new Harmony("JueMingR.Tests.ReplayNativeHistory");TraceHooks(patches);
            try
            {
                foreach(bool camera in new[]{false,true})
                {
                    enabled=false;Completed=false;frames.Clear();dust.Clear();sandbox.GetType().GetMethod("ClearWorld",Flags).Invoke(sandbox,null);matchCamera=camera;
                    foreach(byte[] payload in requests)
                    {
                        long tick=BitConverter.ToInt64(payload,4);if(tick>target)break;enabled=tick==target;
                        try{sandbox.GetType().GetMethod("Predict",Flags).Invoke(sandbox,new object[]{payload,true});}
                        catch(TargetInvocationException){if(enabled)throw;continue;}
                        if(!enabled)continue;
                        byte[] proof=(byte[])sandbox.GetType().GetProperty("Alignment",Flags).GetValue(sandbox),expected=File.ReadAllBytes(Path.Combine(output,"history-response.bin"));
                        if(!camera && (proof.Length!=expected.Length+8 || !proof.Take(expected.Length).SequenceEqual(expected)))throw new InvalidOperationException("Replay must reproduce the actual worker response proof before interpreting differences.");
                        using(var original=new BinaryReader(File.OpenRead(Path.Combine(output,"history-native-proof.bin"))))using(var predicted=new BinaryReader(new MemoryStream(proof)))
                        {
                            var alignment=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true);var read=alignment.GetMethod("Read",Flags);int count=original.ReadInt32();predicted.ReadInt32();int different=0;string first=null;
                            for(int frame=0;frame<count;frame++){object actual=read.Invoke(null,new object[]{original}),future=read.Invoke(null,new object[]{predicted});string difference=(string)alignment.GetMethod("Difference",Flags).Invoke(null,new[]{future,actual});if(difference!=null){different++;if(first==null)first=frame+": "+difference;}}
                            Console.WriteLine("DIAGNOSTIC camera-control="+camera+" original-history-count="+count+" different="+different+" first="+first);
                        }
                        string name=camera?"camera-control":"private";File.WriteAllLines(Path.Combine(output,"history-"+name+".txt"),frames.SelectMany(p=>p.Value));File.WriteAllLines(Path.Combine(output,"history-dust-"+name+".txt"),dust.Select(p=>p.Value));
                        Console.WriteLine("DIAGNOSTIC replay "+name+" target="+target+" original-worker-proof="+(!camera?"identical":"single-variable camera control")+"; not a production fix");break;
                    }
                }
            }
            finally{enabled=false;patches.UnpatchAll(patches.Id);}
        }
    }
}
