using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using JueMingR.Features.Processing;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Items;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class ProcessingChecks
    {
        internal static void Check(List<string> failures)
        {
            try{Storage();Ownership();}catch(Exception error){failures.Add("Processing: "+error);}
        }
        private static void Storage()
        {
            string root=Path.Combine(Path.GetTempPath(),"JueMingR-processing-settings-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            try
            {
                for(int domain=0;domain<3;domain++)
                {
                    string path=Path.Combine(root,domain+".json");string[] names=domain==2?Enumerable.Range(0,256).Select(i=>"完整词缀，含逗号 "+i).ToArray():new string[0];
                    using(var s=new ProcessingSettings(new AtomicFileDocument(path,65536),domain))
                    {
                        Until(()=>{s.Poll();return s.Loaded;});Require(s.Ready && !s.Value.Enabled && s.Value.Names.Count==0,"independent missing domain starts off/empty");
                        Require(s.Set(new ProcessingOptions(true,names)) && !s.Ready && s.Busy,"submission immediately revokes consumption readiness");Until(()=>{s.Poll();return !s.Busy;});Require(s.Ready && s.Value.Enabled,"committed preference publishes");
                        long revision=s.Revision;byte[] bytes=File.ReadAllBytes(path);for(int i=0;i<10000;i++)s.Poll();Require(s.Revision==revision && bytes.SequenceEqual(File.ReadAllBytes(path)),"stable poll does not mutate document or revision");
                    }
                    using(var s=new ProcessingSettings(new AtomicFileDocument(path,65536),domain))
                    {Until(()=>{s.Poll();return s.Loaded;});Require(s.Ready && s.Value.Enabled && s.Value.Names.SequenceEqual(names),"reload preserves full Unicode list identities, not comma splitting");}
                    var original=File.ReadAllBytes(path);string invalid=Encoding.UTF8.GetString(original).Replace("\"version\":1","\"version\":99");File.WriteAllText(path,invalid,new UTF8Encoding(false));
                    using(var s=new ProcessingSettings(new AtomicFileDocument(path,65536),domain))
                    {Until(()=>{s.Poll();return s.Loaded;});Require(!s.Ready && s.Protected && !s.Set(new ProcessingOptions(true)),"future domain stays protected, not silently reset");Require(File.ReadAllText(path)==invalid,"protected original stays unchanged");}
                }
                using(var s=new ProcessingSettings(new UnconfirmedStorage(),2))
                {Until(()=>{s.Poll();return s.Loaded;});Require(s.Set(new ProcessingOptions(true,new[]{"完整目标"})),"isolated storage fault submit");Until(()=>{s.Poll();return !s.Busy;});Require(s.Protected && !s.Ready && !s.Set(new ProcessingOptions()),"unknown replace cannot become safe via another toggle");}
            }
            finally
            {
                string full=Path.GetFullPath(root),temp=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
                if(!full.StartsWith(temp,StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("JueMingR-processing-settings-",StringComparison.Ordinal))throw new InvalidOperationException("unsafe isolated fixture cleanup");
                Directory.Delete(full,true);
            }
        }
        private static void Ownership()
        {
            var o=new ItemOperationOwnership();o.SetSession(1);var slots=new ulong[5];slots[0]=1UL<<12;slots[4]=1UL<<3;
            Require(o.TryBeginProcessing(1,slots,10),"bag and actual void-key resources acquired");
            Require(!o.TryBeginSale(1) && !o.TryBeginUse(1,12,11) && o.TryBeginUse(1,13,12),"sale/exact source conflicts but adjacent quick source can proceed");
            o.EndUse(1,12);o.EndProcessing(1,slots,99,false);Require(o.IsProtected(4,3),"wrong token cannot clear key lease");
            o.EndProcessing(1,slots,10,true);Require(o.IsProtected(0,12) && o.IsProtected(4,3),"unknown holds both actual resource domains");
            o.SetSession(2);Require(o.ProtectedSlots==0 && !o.IsProtected(4,3),"new owner generation ends prior resource domain");
        }
        private static void Until(Func<bool> done){for(int i=0;i<5000;i++){if(done())return;Thread.Sleep(1);}throw new TimeoutException("processing worker fixture");}
        private static void Require(bool condition,string why){if(!condition)throw new InvalidOperationException(why);}
        private sealed class UnconfirmedStorage : IPreferenceStorage
        {
            public PreferenceReadResult Read(){return new PreferenceReadResult(PreferenceReadStatus.Missing,null,null,null);}
            public PreferenceWriteResult Write(string identity,byte[] contents){return new PreferenceWriteResult(PreferenceWriteStatus.IoFailure,null,"isolated-post-replace",true,true);}
            public void Dispose(){}
        }
    }
}
