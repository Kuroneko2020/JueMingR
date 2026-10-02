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
                    try
                    {
                        byte[] core=sandbox.Predict(frame,ready!=null);
                        result=ready==null?core:PredictionPipeProtocol.Envelope(PredictionPipeProtocol.Result(core,sandbox.Alignment,NativeAssetSnapshot.MissingKey),sequence,true);
                    }
                    catch(Exception error)
                    {
                        if(error is OutOfMemoryException)throw;
                        // A native scenario failure invalidates this request;
                        // it is never replaced by extrapolated success points.
                        bool capacity=error is PredictionCapacityException;
                        using(var buffer=new MemoryStream())using(var writer=new BinaryWriter(buffer))
                        {writer.Write(-PredictionWire.Protocol);writer.Write(error.GetType().Name);string message=(PredictionPipeProtocol.Measure?"completed-steps="+sandbox.MeasuredCompletedSteps+"; ":"")+error;writer.Write(message.Length>4096?message.Substring(0,4096):message);writer.Write(capacity?-1:NativeTileBoundary.MissingX);writer.Write(capacity?-1:NativeTileBoundary.MissingY);writer.Write(capacity?-1:NativeEntityDirectory.MissingKind);writer.Write(capacity?-1:NativeEntityDirectory.MissingSlot);writer.Write(capacity?-1:NativeEntityDirectory.MissingField);writer.Flush();result=buffer.ToArray();}
                        // Failed predictions have no usable alignment. A real
                        // missing texture is a bounded page request, not an
                        // acknowledgement; preserve it for Session discovery.
                        // Capacity refusals carry neither proof nor stale key.
                        if(ready!=null)result=PredictionPipeProtocol.Envelope(PredictionPipeProtocol.Result(result,null,capacity?-1:NativeAssetSnapshot.MissingKey),sequence,true);
                    }
                    // I/O is outside request recovery: after a partial write
                    // the only safe action is closing this owned connection.
                    PredictionWire.WriteFrame(output,result);
                }
            }
            return 0;
        }
    }
}
