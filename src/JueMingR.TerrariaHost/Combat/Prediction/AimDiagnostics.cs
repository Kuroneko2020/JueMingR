#if JMR_AIM_DIAGNOSTICS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using JueMingR.Platform.Combat;
using Terraria;
namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Optional passive observer: never owns production state or its mailbox.
    internal static class AimDiagnostics
    {
        private static AimDiagnosticRecorder recorder;
        private static long requests,generations,tokens,lastUpdate,observations;
        private static readonly Dictionary<object,long> identities=new Dictionary<object,long>();
        internal static string Root;
        [ThreadStatic] internal static long Request,Generation,Sequence,Observation;
        internal static bool DetailActive=>recorder!=null && recorder.Detailed;
        internal static bool Active=>recorder!=null && recorder.Accepting;
        internal static long NextObservation(){return Interlocked.Increment(ref observations);}
        internal static void Missing(string stage,Exception error){try{recorder?.Missing(stage,error);}catch{}}
        internal static long NextRequest(){return Interlocked.Increment(ref requests);}
        internal static long NextGeneration(){return Interlocked.Increment(ref generations);}
        internal static void Initialize(string gameRoot)
        {
            try
            {
                if(recorder!=null)return;
                string root=Path.GetFullPath(gameRoot),game=Path.Combine(root,"Terraria.exe");
                if(!File.Exists(game) || !string.Equals(Path.GetFullPath(typeof(Main).Assembly.Location),game,StringComparison.OrdinalIgnoreCase))return;
                string directory=Path.Combine(root,"JueMingRData","logs","aim-diagnostics"),arm=Path.Combine(directory,"arm.txt");
                if(!File.Exists(arm))return;
                for(var info=new DirectoryInfo(directory);info!=null && info.FullName.Length>=root.Length;info=info.Parent)
                    if(info.Exists && (info.Attributes&FileAttributes.ReparsePoint)!=0)return;
                string token=File.ReadAllText(arm).Trim();Guid nonce;if(!Guid.TryParseExact(token,"N",out nonce))return;
                // Consuming once prevents the next ordinary start being armed.
                File.Move(arm,Path.Combine(directory,"consumed-"+token+".txt"));
                Root=Path.Combine(directory,DateTime.UtcNow.ToString("yyyyMMddTHHmmss",CultureInfo.InvariantCulture)+"-"+Process.GetCurrentProcess().Id+"-"+token);
                Directory.CreateDirectory(Root);
                recorder=new AimDiagnosticRecorder(Path.Combine(Root,"host"),8L*1024*1024*1024,32L*1024*1024,Root);
                AppDomain.CurrentDomain.ProcessExit+=(sender,args)=>recorder.Stop("host-process-exit");
                Event("identity",-1,"host="+typeof(AimDiagnostics).Assembly.FullName+";revision="+typeof(AimDiagnostics).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion+";game="+typeof(Main).Assembly.FullName+";hostSha="+AimDiagnosticRecorder.Hash(File.ReadAllBytes(typeof(AimDiagnostics).Assembly.Location))+";gameSha="+AimDiagnosticRecorder.Hash(File.ReadAllBytes(game)));
            }
            catch(Exception error){Missing("initialize",error);try{if(recorder==null && Root!=null && Directory.Exists(Root))File.WriteAllText(Path.Combine(Root,"initialization-failure-"+Process.GetCurrentProcess().Id+".tsv"),"stage\tinitialize\nerror\t"+error.GetType().Name+": "+error.Message);}catch{}}
        }
        internal static void InitializeWorker()
        {
            try
            {
                string root=Environment.GetEnvironmentVariable("JMR_AIM_DIAGNOSTIC_ROOT"),generation=Environment.GetEnvironmentVariable("JMR_AIM_DIAGNOSTIC_GENERATION");
                long g;if(string.IsNullOrEmpty(root) || !long.TryParse(generation,out g) || !Directory.Exists(root))return;
                Root=Path.GetFullPath(root);Generation=g;
                recorder=new AimDiagnosticRecorder(Path.Combine(Root,"worker-"+g),8L*1024*1024*1024,32L*1024*1024,Root);
                AppDomain.CurrentDomain.ProcessExit+=(sender,args)=>recorder.Stop("worker-process-exit");
                Event("worker-identity",-1,"generation="+g+";host="+typeof(AimDiagnostics).Assembly.FullName);
            }
            catch(Exception error){Missing("initialize",error);try{if(recorder==null && Root!=null && Directory.Exists(Root))File.WriteAllText(Path.Combine(Root,"initialization-failure-"+Process.GetCurrentProcess().Id+".tsv"),"stage\tinitialize\nerror\t"+error.GetType().Name+": "+error.Message);}catch{}}
        }
        internal static void Event(string kind,long tick,string detail,byte[] bytes=null,bool detailed=false,long request=-1,long generation=-1,long sequence=-1)
        {try{recorder?.Record(kind,tick,request<0?Request:request,generation<0?Generation:generation,sequence<0?Sequence:sequence,detail,bytes,detailed);}catch(Exception error){Missing("event",error);}}
        internal static void Trigger(string reason){try{recorder?.Trigger(reason);}catch(Exception error){Missing("trigger",error);}}
        private static long Token(object value){long token;if(!identities.TryGetValue(value,out token)){token=++tokens;identities[value]=token;}return token;}
        internal static string Identity(NpcIdentity identity)
        {
            long token=identity.Token==null?0:Token(identity.Token);
            return "session="+identity.Session+";slot="+identity.Slot+";generation="+identity.Generation+";type="+identity.Type+";netId="+identity.NetId+";token="+token;
        }
        internal static void Field(BinaryWriter writer,string name,string type)
        {try{var hash=writer as NativePredictionAlignment.ValueHashWriter;if(hash!=null)hash.DiagnosticTape?.BeginField(name,type);}catch(Exception error){Missing("field",error);}}
        internal static void Tape(NativePredictionAlignment.ValueHashWriter writer,long tick,string subject)
        {try{if(DetailActive && writer.DiagnosticTape!=null){var watch=Stopwatch.StartNew();byte[] bytes=writer.DiagnosticTape.Encode();Event("alignment-fields",tick,subject+";observation="+Observation+";tapeEncodeMs="+watch.Elapsed.TotalMilliseconds.ToString("R",CultureInfo.InvariantCulture)+";bytes="+bytes.Length,bytes,true);}}catch(Exception error){Missing("tape-copy",error);}}
        internal static void Update(long tick,long session,string detail)
        {
            if(!Active)return;long start=Stopwatch.GetTimestamp();
            var text=new StringBuilder(detail);var p=Main.LocalPlayer;
            text.Append(";intervalMs=").Append(lastUpdate==0?0:(start-lastUpdate)*1000.0/Stopwatch.Frequency);lastUpdate=start;
            text.Append(";worldId=").Append(Main.ActiveWorldFileData?.UniqueId).Append(";worldName=").Append(Main.ActiveWorldFileData?.Name);
            text.Append(";menu=").Append(Main.gameMenu).Append(";paused=").Append(Main.gamePaused).Append(";netMode=").Append(Main.netMode).Append(";worldSize=").Append(Main.maxTilesX).Append('x').Append(Main.maxTilesY);
            if(p!=null)text.Append(";playerPosition=").Append(p.position).Append(";playerVelocity=").Append(p.velocity).Append(";playerName=").Append(p.name).Append(";player=").Append(p.whoAmI).Append(";active=").Append(p.active).Append(";dead=").Append(p.dead).Append(";life=").Append(p.statLife).Append(";immune=").Append(p.immune).Append('/').Append(p.immuneTime).Append(";mount=").Append(p.mount.Active).Append('/').Append(p.mount.Type).Append(";zone=").Append((byte)p.zone1).Append('/').Append((byte)p.zone2).Append('/').Append((byte)p.zone3).Append('/').Append((byte)p.zone4).Append('/').Append((byte)p.zone5);
            Event("update",tick,text.ToString());
            if(!DetailActive)return;
            var pool=new StringBuilder();
            if(Main.npc!=null)for(int i=0;i<Main.npc.Length;i++){var n=Main.npc[i];if(n==null || !n.active)continue;pool.Append("npc\t").Append(Identity(CombatSelection.Identity(n,session))).Append("\tpos=").Append(n.position).Append("\tvel=").Append(n.velocity).Append("\tlife=").Append(n.life).Append("\tai=").Append(string.Join(",",n.ai)).Append("\tlocalAI=").Append(string.Join(",",n.localAI)).Append("\tjustHit=").Append(n.justHit).Append("\tfriendly=").Append(n.friendly).Append("\n");}
            if(Main.projectile!=null)for(int i=0;i<Main.projectile.Length;i++){var shot=Main.projectile[i];if(shot==null || !shot.active)continue;pool.Append("projectile\t").Append(i).Append("\ttoken=").Append(Token(shot)).Append("\tkey=").Append((uint)shot.key).Append("\towner=").Append(shot.owner).Append("\ttype=").Append(shot.type).Append("\tpos=").Append(shot.position).Append("\tvel=").Append(shot.velocity).Append("\tdamage=").Append(shot.damage).Append("\tfaction=").Append(shot.friendly).Append('/').Append(shot.hostile).Append("\n");}
            Event("pool",tick,pool.ToString(),null,true);
            Event("copy-cost",tick,"elapsedMs="+((Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency).ToString("R",CultureInfo.InvariantCulture));
        }
    }
}
#endif
