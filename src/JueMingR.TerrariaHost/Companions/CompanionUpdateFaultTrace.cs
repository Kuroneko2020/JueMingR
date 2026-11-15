#if JMR_COMPANION_FAULT_DIAGNOSTIC
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    // Task184 one-use observation only. Ordinary builds contain neither this
    // type nor its call sites. Claim-before-arm prevents a normal restart from
    // reusing permission; no character data or password is inspected here.
    internal static class CompanionUpdateFaultTrace
    {
        private static readonly object gate=new object();
        private static bool initialized,active,finished;
        private static string output,identity;
        private static Timer timeout;
        private static Task writer;
        internal static void Install(string gameDirectory)
        {
            lock(gate)
            {
                if(initialized)return;initialized=true;
                try
                {
                    string directory=Path.Combine(gameDirectory,"JueMingRData","diagnostics","companion-update");
                    string arm=Path.Combine(directory,"armed");if(!File.Exists(arm))return;
                    // Consume even an invalid/stale request. It cannot turn a
                    // later ordinary launch into another diagnostic attempt.
                    string claim=Path.Combine(directory,"claimed-"+Guid.NewGuid().ToString("N")+".arm");File.Move(arm,claim);
                    if(new FileInfo(claim).Length>512)return;
                    string[] lines=File.ReadAllLines(claim);Guid run,host;DateTime expiry;
                    if(lines.Length!=4 || lines[0]!="JueMingR.CompanionUpdateFault.1" || !Guid.TryParse(lines[1],out run) ||
                        !Guid.TryParse(lines[2],out host) || host!=typeof(CompanionUpdateFaultTrace).Module.ModuleVersionId ||
                        !DateTime.TryParse(lines[3],CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out expiry) || expiry.ToUniversalTime()<=DateTime.UtcNow)return;
                    output=Path.Combine(directory,run.ToString("N")+".log");
                    identity="format=JueMingR.CompanionUpdateFault.1\nrun="+run.ToString("N")+"\nhostMvid="+host+
                        "\nhostVersion="+typeof(CompanionUpdateFaultTrace).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion+
                        "\nnativeMvid="+typeof(Main).Module.ModuleVersionId+"\nstartedUtc="+DateTime.UtcNow.ToString("O",CultureInfo.InvariantCulture)+"\nseconds=600;maxBytes=65536;records=1\n";
                    active=true;timeout=new Timer(_=>Stop("time-limit-no-fault"),null,600000,Timeout.Infinite);
                    AppDomain.CurrentDomain.ProcessExit+=Exit;
                }
                catch {active=false;finished=true;}
            }
        }
        internal static void Phase(HostCompanions.Actor actor,string stage)
        {
            // Every producer shares the same retired permission. Missing arm,
            // first fault and timeout do not keep collecting per-frame phases.
            if(Volatile.Read(ref active))actor.FaultPhase=stage;
        }
        internal static void Capture(HostCompanions.Actor actor,Exception error)
        {
            try { lock(gate)
            {
                if(!active || finished)return;
                // Snapshot only these necessary identities on the game thread.
                // The writer receives text, never Player/Main or live actors.
                // The native body itself may have no deeper frame even before
                // Harmony rethrows. Record only bounded null facts and control
                // indices here, once; never inspect item contents or Asset.Value.
                var body=actor.Body;int selected=body.selectedItem;
                bool slotValid=body.inventory!=null && selected>=0 && selected<body.inventory.Length;
                string record="end=first-player-fault\nutc="+DateTime.UtcNow.ToString("O",CultureInfo.InvariantCulture)+
                    "\nphase="+actor.FaultPhase+"\nprofile="+actor.Profile.Id.ToString("N")+"\ngeneration="+actor.Generation+
                    "\nslot="+actor.Body.whoAmI+"\nnetMode="+Main.netMode+"\ngameUpdate="+Main.GameUpdateCount+
                    "\nworld="+(Main.ActiveWorldFileData==null?"none":Main.ActiveWorldFileData.UniqueId.ToString("N"))+
                    "\nthread="+Thread.CurrentThread.ManagedThreadId+"\nselectedSlot="+selected+"\nitemAnimation="+body.itemAnimation+
                    "\nnullFacts=inventory:"+(body.inventory==null)+";slotValid:"+slotValid+";selectedItem:"+(!slotValid || body.inventory[selected]==null)+
                    ";mount:"+(body.mount==null)+";lastVisualizedSelectedItem:"+(body.lastVisualizedSelectedItem==null)+
                    ";threadRandom:"+(Main.rand==null)+";mainInstance:"+(Main.instance==null)+"\nexception=\n"+error;
                string home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if(!String.IsNullOrEmpty(home))record=record.Replace(home,"<user>");
                Finish(record);
            }} catch { lock(gate){active=false;finished=true;timeout?.Dispose();timeout=null;} }
        }
        private static void Stop(string reason){try{lock(gate){if(active && !finished)Finish("end="+reason+"\nexception=NOT_CAPTURED\n");}}catch{lock(gate){active=false;finished=true;timeout?.Dispose();timeout=null;}}}
        private static void Finish(string record)
        {
            active=false;finished=true;timeout?.Dispose();timeout=null;
            // Keep the exit drain until process exit: the single final writer
            // may still be queued when the owner closes immediately after error.
            string path=output,text=identity+record;
            // NullReference stacks are normally far below this cap. A larger
            // exception is explicitly incomplete, never reported as full data.
            if(Encoding.UTF8.GetByteCount(text)>65536)text=text.Substring(0,Math.Min(text.Length,12000))+"\nTRUNCATED: output limit; full exception unavailable.\n";
            writer=Task.Run(()=>{try{using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read))using(var w=new StreamWriter(stream,new UTF8Encoding(false))){w.Write(text);}}catch{}});
        }
        private static void Exit(object sender,EventArgs args)
        {Stop("process-exit-no-fault");Task pending;lock(gate)pending=writer;try{pending?.Wait(750);}catch{}AppDomain.CurrentDomain.ProcessExit-=Exit;}
    }
}
#endif
