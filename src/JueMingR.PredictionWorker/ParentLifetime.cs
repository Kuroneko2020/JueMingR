using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace JueMingR.PredictionWorker
{
    internal static class ParentLifetime
    {
        // EOF alone is insufficient while initializing or executing native AI.
        // Hold the exact process handle and wait without polling. PID reuse
        // cannot grant another process ownership over this worker.
        internal static void Bind(string parentId,string parentStarted)
        {
            int id=int.Parse(parentId,CultureInfo.InvariantCulture);
            long started=long.Parse(parentStarted,CultureInfo.InvariantCulture);
            if(id<=0 || id==Process.GetCurrentProcess().Id)throw new InvalidOperationException("Invalid prediction parent.");
            Process parent=Process.GetProcessById(id);
            try
            {
                // Acquire before querying identity: otherwise the framework may
                // use temporary handles and a reused PID can pass between calls.
                IntPtr handle=parent.Handle;if(handle==IntPtr.Zero)throw new InvalidOperationException("Prediction parent handle unavailable.");
                if(parent.StartTime.ToUniversalTime().Ticks!=started || parent.HasExited)throw new InvalidOperationException("Prediction parent identity changed.");
                var watcher=new Thread(()=>
                {
                    try{using(parent)parent.WaitForExit();Environment.Exit(0);}
                    catch{Environment.Exit(1);}
                }){IsBackground=true,Name="JueMingR prediction parent lifetime"};
                watcher.Start();
            }
            catch{parent.Dispose();throw;}
        }
    }
}
