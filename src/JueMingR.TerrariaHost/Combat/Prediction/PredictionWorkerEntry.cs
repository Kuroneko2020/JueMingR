using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    internal static class PredictionWorkerEntry
    {
        // No Main access in this method: its beforefieldinit initializer must
        // never capture the player's default Documents path in this process.
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int Run(Stream input,Stream output,byte[] ready)
        {
            if(AppDomain.CurrentDomain.FriendlyName.IndexOf("JueMingR.PredictionWorker",StringComparison.Ordinal)<0)
                throw new InvalidOperationException("Private simulation requires the owned worker executable.");
            Terraria.Program.SavePath=Path.Combine(Path.GetTempPath(),"JueMingR-prediction-unused-"+Guid.NewGuid().ToString("N"));
            return Simulate(input,output,ready);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Simulate(Stream input,Stream output,byte[] ready)
        {
            var initialize=PredictionPipeProtocol.Measure?System.Diagnostics.Stopwatch.StartNew():null;
            using(var sandbox=new PredictionSandbox())
            {
                if(initialize!=null)Console.Error.WriteLine("READY native-initialize-ms="+initialize.Elapsed.TotalMilliseconds.ToString("F3"));
                // Only fully initialized native state may invite a fresh
                // game-thread capture. Parent-bound is not a Ready signal.
                if(ready!=null)PredictionPipeProtocol.WriteFrame(output,ready);
                byte[] frame;long sequence=0;
                while((frame=PredictionWire.ReadFrame(input))!=null)
                {
                    if(ready!=null)frame=PredictionPipeProtocol.OpenEnvelope(frame,checked(++sequence),false);
                    if(ready!=null && PredictionPipeProtocol.IsClearWorld(frame))
                    {
                        sandbox.ClearWorld();
                        PredictionWire.WriteFrame(output,PredictionPipeProtocol.Envelope(PredictionPipeProtocol.ClearWorld(),sequence,true));
                        continue;
                    }
                    byte[] result;
                    try{result=sandbox.Predict(frame,ready!=null);}
                    catch(Exception error)
                    {
                        if(error is OutOfMemoryException)throw;
                        // A native scenario failure invalidates this request;
                        // it is never replaced by extrapolated success points.
                        using(var buffer=new MemoryStream())using(var writer=new BinaryWriter(buffer))
                        {writer.Write(-PredictionWire.Protocol);writer.Write(error.GetType().Name);string message=error.ToString();writer.Write(message.Length>4096?message.Substring(0,4096):message);writer.Write(NativeTileBoundary.MissingX);writer.Write(NativeTileBoundary.MissingY);writer.Write(NativeEntityDirectory.MissingKind);writer.Write(NativeEntityDirectory.MissingSlot);writer.Write(NativeEntityDirectory.MissingField);writer.Flush();result=buffer.ToArray();}
                    }
                    PredictionWire.WriteFrame(output,ready==null?result:PredictionPipeProtocol.Envelope(PredictionPipeProtocol.Result(result,sandbox.Alignment,NativeAssetSnapshot.MissingKey),sequence,true));
                }
            }
            return 0;
        }
    }
}
