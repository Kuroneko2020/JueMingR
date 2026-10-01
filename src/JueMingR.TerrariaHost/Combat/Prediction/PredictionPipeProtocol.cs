using System;
using System.IO;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // BCL-only source shared with the thin executable. Background transport
    // must not initialize PredictionWire's Terraria snapshot schemas.
    internal static class PredictionPipeProtocol
    {
#if JMR_CONDITIONAL_RESEARCH
        internal const int Protocol=131,MaximumBytes=4*1024*1024,MaximumPayload=MaximumBytes-17;
#else
        internal const int Protocol=31,MaximumBytes=4*1024*1024,MaximumPayload=MaximumBytes-17;
#endif
        internal const string GameHash="960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3";
        // Explicit isolated development probes only; the installed path does
        // no per-request/step cost sampling unless this process opted in.
        internal static readonly bool Measure=Environment.GetEnvironmentVariable("JUEMINGR_PREDICTION_DIAGNOSTICS")=="1";
        private const int ReadyMagic=0x4A525052,TransportVersion=1;
        private const int ExchangeMagic=0x4A525058;
        internal static byte[] ClearWorld(){return new byte[]{0x43,0x57,0x52,0x44,(byte)Protocol};}
        internal static bool IsClearWorld(byte[] value)
        {return value!=null && value.Length==5 && value[0]==0x43 && value[1]==0x57 && value[2]==0x52 && value[3]==0x44 && value[4]==Protocol;}
        internal static byte[] Result(byte[] core,byte[] alignment,int missingAsset)
        {
            // Check the complete result before allocating/copying its frame.
            // Capacity is a request refusal, not a broken authenticated pipe.
            if(core==null || core.Length<1)throw new InvalidDataException("Prediction core absent.");
            long size=16L+core.Length+(alignment==null?0:alignment.Length);
            if(size>MaximumPayload)throw new PredictionCapacityException("Prediction result capacity: core="+core.Length+" alignment="+(alignment==null?0:alignment.Length)+" payload="+size+" limit="+MaximumPayload);
            using(var bytes=new MemoryStream())using(var writer=new BinaryWriter(bytes))
            {writer.Write(1);writer.Write(missingAsset);writer.Write(core.Length);writer.Write(core);writer.Write(alignment==null?0:alignment.Length);if(alignment!=null)writer.Write(alignment);writer.Flush();return bytes.ToArray();}
        }
        internal static byte[] OpenResult(byte[] payload,out byte[] alignment,out int missingAsset)
        {
            using(var reader=new BinaryReader(new MemoryStream(payload,false)))
            {
                if(reader.ReadInt32()!=1)throw new InvalidDataException("Prediction result format.");missingAsset=reader.ReadInt32();
                int size=reader.ReadInt32();if(size<1 || size>payload.Length-16)throw new InvalidDataException("Prediction core length.");byte[] core=reader.ReadBytes(size);
                int extra=reader.ReadInt32();if(extra<0 || extra!=reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException("Prediction alignment length.");alignment=extra==0?null:reader.ReadBytes(extra);return core;
            }
        }
        internal static byte[] Envelope(byte[] payload,long sequence,bool reply)
        {
            if(payload==null || payload.Length<1 || payload.Length>MaximumPayload || sequence<1)throw new InvalidDataException("Invalid prediction exchange.");
            using(var stream=new MemoryStream(payload.Length+17))using(var writer=new BinaryWriter(stream))
            {writer.Write(ExchangeMagic);writer.Write(TransportVersion);writer.Write(reply);writer.Write(sequence);writer.Write(payload);writer.Flush();return stream.ToArray();}
        }
        internal static byte[] OpenEnvelope(byte[] frame,long sequence,bool reply)
        {
            if(frame==null || frame.Length<18 || frame.Length>MaximumBytes || sequence<1)throw new InvalidDataException("Invalid prediction exchange frame.");
            using(var reader=new BinaryReader(new MemoryStream(frame,false)))
            {
                if(reader.ReadInt32()!=ExchangeMagic || reader.ReadInt32()!=TransportVersion || reader.ReadByte()!=(reply?1:0) || reader.ReadInt64()!=sequence)throw new InvalidDataException("Prediction exchange identity mismatch.");
                return reader.ReadBytes(frame.Length-17);
            }
        }
        internal static byte[] Ready(Guid nonce,int parent,long parentStarted,int child,long childStarted,string hostHash)
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                writer.Write(ReadyMagic);writer.Write(TransportVersion);writer.Write(Protocol);writer.Write(nonce.ToByteArray());
                writer.Write(parent);writer.Write(parentStarted);writer.Write(child);writer.Write(childStarted);
                writer.Write(HashBytes(GameHash));writer.Write(HashBytes(hostHash));writer.Flush();return stream.ToArray();
            }
        }
        internal static void VerifyReady(byte[] bytes,Guid nonce,int parent,long parentStarted,int child,long childStarted,string hostHash)
        {
            byte[] expected=Ready(nonce,parent,parentStarted,child,childStarted,hostHash);
            if(bytes==null || bytes.Length!=expected.Length)throw new InvalidDataException("Prediction Ready length mismatch.");
            for(int i=0;i<bytes.Length;i++)if(bytes[i]!=expected[i])throw new InvalidDataException("Prediction Ready identity mismatch.");
        }
        private static byte[] HashBytes(string hash)
        {if(hash==null || hash.Length!=64)throw new InvalidDataException("Invalid payload hash.");var bytes=new byte[32];for(int i=0;i<bytes.Length;i++)bytes[i]=Convert.ToByte(hash.Substring(i*2,2),16);return bytes;}
        internal static byte[] ReadFrame(Stream input)
        {
            int first=input.ReadByte();if(first<0)return null;
            var header=new byte[4];header[0]=(byte)first;Exact(input,header,1,3);
            int count=header[0]|header[1]<<8|header[2]<<16|header[3]<<24;
            if(count<1 || count>MaximumBytes)throw new InvalidDataException("Invalid prediction frame length: "+count);
            var payload=new byte[count];Exact(input,payload,0,count);return payload;
        }
        internal static void WriteFrame(Stream output,byte[] payload)
        {
            if(payload==null || payload.Length<1 || payload.Length>MaximumBytes)throw new InvalidDataException("Invalid prediction frame.");
            int size=payload.Length;output.WriteByte((byte)size);output.WriteByte((byte)(size>>8));output.WriteByte((byte)(size>>16));output.WriteByte((byte)(size>>24));
            output.Write(payload,0,size);output.Flush();
        }
        private static void Exact(Stream input,byte[] bytes,int offset,int count)
        {while(count>0){int got=input.Read(bytes,offset,count);if(got==0)throw new EndOfStreamException("Truncated prediction frame.");offset+=got;count-=got;}}
    }
    // Deliberately narrow: only known bounded result-generation limits use
    // this refusal. Protocol/identity corruption and OOM are never relabeled.
    internal sealed class PredictionCapacityException : Exception
    {internal PredictionCapacityException(string message):base(message){}}
}
