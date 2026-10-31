using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class BlueprintMirrorTrimChecks
    {
        private static readonly string Scope = new string('a',64);
        internal static void Check(IList<string> failures)
        {
            Run(failures,"hand-listed axes and inclusive rectangles",Geometry);
            Run(failures,"independent v1 bytes and strict v2 pose",Codec);
            Run(failures,"v1 exact original retained only on a real change",History);
        }
        private static void Run(IList<string> failures,string name,Action check)
        { try { check(); } catch(Exception error) { failures.Add("BlueprintMirrorTrim "+name+": "+error); } }
        private static void Require(bool condition,string message) { if(!condition)throw new InvalidOperationException(message); }
        private static void Geometry()
        {
            var odd=new BlueprintProjectionView(new CaptureSummary{Left=30,Top=40,Width=5,Height=4},true,null);
            Require(Enumerable.Range(0,5).Select(odd.DisplayX).SequenceEqual(new[]{4,3,2,1,0}),"odd middle is stationary and endpoints reflect");
            var even=new BlueprintProjectionView(new CaptureSummary{Width=4,Height=1},true,null);
            Require(Enumerable.Range(0,4).Select(even.DisplayX).SequenceEqual(new[]{3,2,1,0}),"even axis lies between middle columns");
            var one=new BlueprintProjectionView(new CaptureSummary{Width=1,Height=1},true,null);
            Require(one.DisplayX(0)==0 && odd.SourceX(odd.DisplayX(3))==3,"width one and inverse mapping");
            Require(odd.SourceBounds(new BlueprintProjectionBounds(0,1,2,2)).Equals(new BlueprintProjectionBounds(3,1,2,2)),"half-open source interval is hand-listed [3,5), no Summary offset");
            foreach(var endpoints in new[]{new[]{1,0,3,2},new[]{3,0,1,2},new[]{1,2,3,0},new[]{3,2,1,0}})
                Require(BlueprintProjectionView.InclusiveRectangle(endpoints[0],endpoints[1],endpoints[2],endpoints[3],5,4).Equals(new BlueprintProjectionBounds(1,0,3,3)),"four drag directions contain both endpoints");
            Require(BlueprintProjectionView.InclusiveRectangle(2,1,2,1,5,4).Equals(new BlueprintProjectionBounds(2,1,1,1)),"zero movement selects one cell");
            Require(BlueprintProjectionView.InclusiveRectangle(int.MinValue,-100,int.MaxValue,100,5,4).Equals(new BlueprintProjectionBounds(0,0,5,4)),"long intermediate clips extreme inclusive endpoints");
            Require(BlueprintProjectionView.InclusiveRectangle(-4,-4,-1,-1,5,4).Width==0,"outside rectangle is genuinely empty");
        }
        // This writer is a frozen independent v1 fixture. Calling today's
        // encoder here would silently turn a backward-compatibility test v2.
        private static byte[] V1()
        {
            using(var stream=new MemoryStream())
            {
                using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
                {
                    writer.Write(0x3130435342524d4aUL);writer.Write(1);writer.Write(Enumerable.Repeat((byte)0xaa,32).ToArray());
                    writer.Write(1000);writer.Write(500);writer.Write(7L);writer.Write(1);
                    writer.Write(new Guid("11111111-1111-1111-1111-111111111111").ToByteArray());writer.Write(new Guid("22222222-2222-2222-2222-222222222222").ToByteArray());writer.Write(new byte[32]);
                    writer.Write(100);writer.Write(50);writer.Write(2L);writer.Write(1L);writer.Write(1L);
                    writer.Write(1);writer.Write((1L<<32)|3);writer.Write((ushort)2);writer.Write(0);
                }
                return Seal(stream.ToArray());
            }
        }
        private static byte[] Seal(byte[] payload)
        { using(var sha=SHA256.Create()){var result=new byte[payload.Length+32];Buffer.BlockCopy(payload,0,result,0,payload.Length);Buffer.BlockCopy(sha.ComputeHash(payload),0,result,payload.Length,32);return result;} }
        private static byte[] Reseal(byte[] bytes)
        { var payload=new byte[bytes.Length-32];Buffer.BlockCopy(bytes,0,payload,0,payload.Length);return Seal(payload); }
        private static void Codec()
        {
            byte[] old=V1();var loaded=BlueprintSceneCodec.Decode(old,Scope,1000,500);var record=loaded.Records[0];
            Require(loaded.SourceVersion==1 && !record.MirroredHorizontally && record.OriginX==100 && record.Order==2,"frozen v1 defaults normal and retains identity/order");
            var wall=new CaptureCell{X=3,Y=1,Layers=CaptureLayer.Wall,WallType=7};
            Require(!record.Mask.Apply(wall).HasContent,"old mask coordinates remain source local");
            var mirrored=new BlueprintSceneDocument(Scope,1000,500,8,new[]{new BlueprintSceneRecord(record.Template,record.OriginX,record.OriginY,record.Order,record.Mask,true)});
            byte[] bytes=BlueprintSceneCodec.Encode(mirrored);var reopened=BlueprintSceneCodec.Decode(bytes,Scope,1000,500);
            Require(BitConverter.ToInt32(bytes,8)==2 && reopened.SourceVersion==2 && reopened.Records[0].MirroredHorizontally && !reopened.Records[0].Mask.Apply(wall).HasContent,"v2 persists pose with unchanged source mask");
            bytes[144]=2;ExpectProtected(Reseal(bytes),PreferenceStatus.UnsupportedVersion);
            var future=old.Take(12).ToArray();Buffer.BlockCopy(BitConverter.GetBytes(3),0,future,8,4);ExpectProtected(future,PreferenceStatus.UnsupportedVersion);
            var corrupt=(byte[])old.Clone();corrupt[50]^=1;ExpectProtected(corrupt,PreferenceStatus.Invalid);
        }
        private static void ExpectProtected(byte[] bytes,PreferenceStatus status)
        { try{BlueprintSceneCodec.Decode(bytes,Scope,1000,500);throw new InvalidOperationException("invalid bytes accepted");}catch(PreferenceFormatException error){Require(error.Status==status,"precise protected decode category");} }
        private static void History()
        {
            string root=Path.Combine(Path.GetTempPath(),"JMR-mirror-scene-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string path=Path.Combine(root,"scene.bin");
            byte[] original=V1();File.WriteAllBytes(path,original);
            var history=new BlueprintSceneHistory(scope=>new AtomicFileDocument(path,int.MaxValue,true,".schema1-original"),(file,value)=>{if(value.SourceVersion==1)((AtomicFileDocument)file).RetainLoadedSource();});
            try
            {
                history.BeginSession(1,1000,500);history.UseScope(Scope);Until(()=>{history.Poll();return history.Loaded;});
                Require(!history.Protected && File.ReadAllBytes(path).SequenceEqual(original) && !File.Exists(path+".schema1-original"),"loading old document does not rewrite or fabricate an archive");
                var record=history.Restored.Records[0];Require(history.Offer(new[]{new BlueprintSceneRecord(record.Template,record.OriginX,record.OriginY,record.Order,record.Mask,true)}),"real committed pose accepted");
                Until(()=>{history.Poll();return history.Saved;});
                Require(File.ReadAllBytes(path+".schema1-original").SequenceEqual(original) && BlueprintSceneCodec.Decode(File.ReadAllBytes(path),Scope,1000,500).Records[0].MirroredHorizontally,"first actual v2 write retains exact authenticated source bytes");
            }
            finally { Require(history.Stop(3000),"document worker stopped before cleanup"); }
            Directory.Delete(root,true);
        }
        private static void Until(Func<bool> condition)
        { var limit=DateTime.UtcNow.AddSeconds(10);while(!condition()){if(DateTime.UtcNow>limit)throw new TimeoutException("scene worker");Thread.Sleep(1);} }
    }
}
