using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // A bounded value tape, not a deferred callback into Main. Existing codecs
    // read/validate on the game thread; only owned primitive bits, cloned byte
    // buffers and immutable schema strings survive sealing. The transport
    // thread performs the binary encoding after ownership has transferred.
    internal sealed class NativeCapturedValues : BinaryWriter
    {
        [StructLayout(LayoutKind.Explicit)]
        internal struct Bits
        {
            [FieldOffset(0)]internal ulong Raw;
            [FieldOffset(0)]internal float Single;
            [FieldOffset(0)]internal double Double;
        }
        internal struct Entry {internal byte Kind;internal Bits Value;}
        internal sealed class Storage
        {
            internal readonly List<Entry> Entries=new List<Entry>(4096);
            internal readonly List<object> Buffers=new List<object>();
        }
#if JMR_AIM_DIAGNOSTICS
        internal byte[] DiagnosticTerrainPages;
#endif
        private Storage storage;
        private List<Entry> entries=>storage.Entries;
        private List<object> buffers=>storage.Buffers;
        private int ownedBytes;
        private bool sealedValues,disposed;
        internal NativeCapturedValues():this(null){}
        internal NativeCapturedValues(Storage reuse):base(Stream.Null,Encoding.UTF8,true){storage=reuse??new Storage();}
        internal bool IsSealed=>sealedValues && storage!=null;
        internal Storage ReleaseStorage()
        {
            if(!IsSealed)throw new InvalidOperationException("Only encoded sealed storage may retire.");
            // A single transport-owned spare, not a general object pool. The
            // old capture becomes unusable before all references AND primitive
            // world values are cleared. Only its empty capacity is reusable.
            var result=storage;storage=null;disposed=true;result.Entries.Clear();result.Buffers.Clear();return result;
        }
        private void Add(byte kind,ulong bits)
        {
            if(sealedValues || disposed)throw new InvalidOperationException("Prediction values are immutable after sealing.");
            // A legal 102-segment Destroyer needs more than 262144 scalar
            // writes (including per-type immunity). Bound by the actual wire
            // budget, not an unrelated entity-count limit. Each entry costs at
            // least one encoded byte; zero-byte buffers never add an entry.
            // The one retained tape is therefore bounded even for byte writes.
            if(kind<11)Reserve(kind<3?1:kind<5?2:kind==5 || kind==6 || kind==9?4:8);
            entries.Add(new Entry{Kind=kind,Value=new Bits{Raw=bits}});
        }
        internal void Seal(){if(disposed || sealedValues)throw new InvalidOperationException("Invalid prediction value transfer.");sealedValues=true;}
        internal byte[] Encode()
        {
            if(!IsSealed)throw new InvalidOperationException("Prediction values must be sealed and owned before encoding.");
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
            {Replay(writer);writer.Flush();if(stream.Length>PredictionPipeProtocol.MaximumPayload)throw new InvalidDataException("Snapshot exceeds frame limit.");return stream.ToArray();}
        }
        #if JMR_AIM_DIAGNOSTICS
        internal byte[] DiagnosticPartial()
        {using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){Replay(writer);writer.Flush();return stream.ToArray();}}
#endif
        private void Replay(BinaryWriter writer)
        {
            foreach(var entry in entries)
            {
                ulong bits=entry.Value.Raw;
                switch(entry.Kind)
                {
                    case 0:writer.Write(bits!=0);break;case 1:writer.Write((byte)bits);break;case 2:writer.Write(unchecked((sbyte)bits));break;
                    case 3:writer.Write(unchecked((short)bits));break;case 4:writer.Write((ushort)bits);break;
                    case 5:writer.Write(unchecked((int)bits));break;case 6:writer.Write((uint)bits);break;
                    case 7:writer.Write(unchecked((long)bits));break;case 8:writer.Write(bits);break;
                    case 9:writer.Write(entry.Value.Single);break;case 10:writer.Write(entry.Value.Double);break;
                    case 11:writer.Write((string)buffers[(int)bits]);break;case 12:writer.Write((byte[])buffers[(int)bits]);break;
                    default:throw new InvalidDataException("Unknown prediction value kind.");
                }
            }
        }
        public override void Write(bool value){Add(0,value?1UL:0UL);}
        public override void Write(byte value){Add(1,value);}
        public override void Write(sbyte value){Add(2,unchecked((ulong)value));}
        public override void Write(short value){Add(3,unchecked((ulong)value));}
        public override void Write(ushort value){Add(4,value);}
        public override void Write(int value){Add(5,unchecked((ulong)value));}
        public override void Write(uint value){Add(6,value);}
        public override void Write(long value){Add(7,unchecked((ulong)value));}
        public override void Write(ulong value){Add(8,value);}
        public override void Write(float value){Add(9,new Bits{Single=value}.Raw);}
        public override void Write(double value){Add(10,new Bits{Double=value}.Raw);}
        public override void Write(string value)
        {
            if(value==null)throw new ArgumentNullException(nameof(value));
            int size=Encoding.UTF8.GetByteCount(value),prefix=1;for(int remaining=size;remaining>=128;remaining>>=7)prefix++;
            Reserve(checked(size+prefix));Add(11,(ulong)buffers.Count);buffers.Add(value);
        }
        public override void Write(byte[] value)
        {
            if(value==null)throw new ArgumentNullException(nameof(value));
            if(value.Length==0){Reserve(0);return;}
            Reserve(value.Length);Add(12,(ulong)buffers.Count);buffers.Add(value.Clone());
        }
        private void Reserve(int bytes)
        {if(sealedValues || disposed)throw new InvalidOperationException("Prediction values are immutable after sealing.");if(bytes>PredictionPipeProtocol.MaximumPayload-ownedBytes)throw new InvalidDataException("Prediction owned buffer capacity.");ownedBytes+=bytes;}
        // No fallback to BinaryWriter's null backing stream is permitted.
        public override Stream BaseStream=>throw new NotSupportedException();
        public override void Write(decimal value){throw new NotSupportedException();}
        public override void Write(char value){throw new NotSupportedException();}
        public override void Write(char[] value){throw new NotSupportedException();}
        public override void Write(char[] value,int index,int count){throw new NotSupportedException();}
        public override void Write(byte[] value,int index,int count){throw new NotSupportedException();}
        public override long Seek(int offset,SeekOrigin origin){throw new NotSupportedException();}
        public override void Flush(){}
        protected override void Dispose(bool disposing){disposed=true;}
    }
}
