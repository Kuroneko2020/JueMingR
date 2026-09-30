using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace JueMingR.PredictionWorker
{
    // Code cache, not world state. The expected digest must come from the
    // authenticated Host; neither an adjacent manifest nor first-use data is
    // an authority. Hash and load the same immutable byte array.
    internal static class PredictionMaterialCache
    {
        private const int Maximum=64*1024*1024,Magic=0x504D4331;
        internal static byte[] Load(string directory,string key,string expected,Func<byte[]> build)
        {
            if(!Identity(key) || expected!=null && !Identity(expected))throw new InvalidDataException("Invalid material identity.");
            if(expected==null){Console.Error.WriteLine("MATERIAL uncertified-rules; rebuild required");return Build(build,null);}
            directory=Path.GetFullPath(directory);Directory.CreateDirectory(directory);
            string target=Path.Combine(directory,key+".image");var elapsed=Stopwatch.StartNew();
            string lockPath=Path.Combine(directory,key+".lock");
            try
            {
            using(var lease=Acquire(lockPath))
            {
                ReclaimInterrupted(directory,key);
                byte[] image=Read(target,key,expected);
                if(image!=null){Console.Error.WriteLine("MATERIAL hit-ms="+elapsed.Elapsed.TotalMilliseconds.ToString("F3",CultureInfo.InvariantCulture));return image;}
                image=Build(build,expected);
                string temporary=Path.Combine(directory,key+"."+Guid.NewGuid().ToString("N")+".tmp");
                try
                {
                    using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                    using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
                    {writer.Write(Magic);writer.Write(Encoding.ASCII.GetBytes(key));writer.Write(image.Length);writer.Write(image);writer.Flush();stream.Flush(true);}
                    if(File.Exists(target))File.Replace(temporary,target,null);else File.Move(temporary,target);
                    Trim(directory,target);
                }
                finally{if(File.Exists(temporary))File.Delete(temporary);}
                Console.Error.WriteLine("MATERIAL generated-published-ms="+elapsed.Elapsed.TotalMilliseconds.ToString("F3",CultureInfo.InvariantCulture));return image;
            }
            }
            finally{DeleteReleasedLock(lockPath);}
        }
        private static byte[] Build(Func<byte[]> build,string expected)
        {byte[] bytes=build();if(bytes==null || bytes.Length<1 || bytes.Length>Maximum || expected!=null && Hash(bytes)!=expected)throw new InvalidDataException("Generated material differs from trusted approval.");return bytes;}
        private static byte[] Read(string path,string key,string expected)
        {
            try
            {
                using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var reader=new BinaryReader(stream))
                {
                    if(stream.Length<73 || stream.Length>Maximum+72 || reader.ReadInt32()!=Magic || Encoding.ASCII.GetString(reader.ReadBytes(64))!=key)return null;
                    int length=reader.ReadInt32();if(length<1 || length>Maximum || length!=stream.Length-stream.Position)return null;
                    byte[] bytes=reader.ReadBytes(length);return bytes.Length==length && Hash(bytes)==expected?bytes:null;
                }
            }
            catch(IOException){return null;}
        }
        private static FileStream Acquire(string path)
        {
            var watch=Stopwatch.StartNew();
            while(true)
            {try{return new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException){if(watch.ElapsedMilliseconds>=45000)throw;Thread.Sleep(50);}}
        }
        private static void Trim(string directory,string current)
        {
            // Only exact owned cache filenames; never recursive and never
            // touch preferences, saves or another currently leased material.
            foreach(var file in new DirectoryInfo(directory).EnumerateFiles("*.image").Where(f=>Identity(Path.GetFileNameWithoutExtension(f.Name))).OrderByDescending(f=>f.LastWriteTimeUtc).Skip(4))
            {
                if(string.Equals(file.FullName,current,StringComparison.OrdinalIgnoreCase))continue;
                try{using(var lease=new FileStream(Path.ChangeExtension(file.FullName,".lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))file.Delete();}catch(IOException){}catch(UnauthorizedAccessException){}
            }
        }
        private static void ReclaimInterrupted(string directory,string ownedKey)
        {
            // Process termination bypasses finally. A valid temporary belongs
            // to its key's exclusive lease; only reclaim it while that lease
            // is ours. Other active generators are skipped without waiting.
            int examined=0;
            foreach(string path in Directory.EnumerateFiles(directory,"*.tmp"))
            {
                if(++examined>64)break;
                string name=Path.GetFileName(path);Guid id;
                if(name.Length!=101 || name[64]!='.' || !Identity(name.Substring(0,64)) || !Guid.TryParseExact(name.Substring(65,32),"N",out id))continue;
                string key=name.Substring(0,64);
                try
                {
                    if(key==ownedKey)File.Delete(path);
                    else
                    {
                        string lockPath=Path.Combine(directory,key+".lock");
                        try{using(var lease=new FileStream(lockPath,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))File.Delete(path);}
                        finally{DeleteReleasedLock(lockPath);}
                    }
                }
                catch(IOException){}catch(UnauthorizedAccessException){}
            }
            // A process can also die before creating its temporary. Lock files
            // contain no state. Windows denies deletion while any generator
            // holds FileShare.None, including one that wins a release race.
            foreach(string path in Directory.EnumerateFiles(directory,"*.lock").Take(64))
                if(Identity(Path.GetFileNameWithoutExtension(path)) && Path.GetFileNameWithoutExtension(path)!=ownedKey)DeleteReleasedLock(path);
        }
        private static void DeleteReleasedLock(string path)
        {try{File.Delete(path);}catch(IOException){}catch(UnauthorizedAccessException){}}
        internal static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
        private static bool Identity(string value){return value!=null && value.Length==64 && value.All(c=>c>='0' && c<='9' || c>='A' && c<='F');}
    }
}
