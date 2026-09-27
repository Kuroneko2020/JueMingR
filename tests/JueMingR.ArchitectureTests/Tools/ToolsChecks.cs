using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using JueMingR.Features.Tools;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Items;

namespace JueMingR.ArchitectureTests
{
    internal static class ToolsChecks
    {
        internal static void Check(List<string> failures)
        {
            try
            {
                // Multiple native-use owners must not start their own token
                // counters at one: a late finalizer could release its successor.
                var allocator=typeof(ItemOperationOwnership).GetMethod("NewUseToken");
                Require(allocator!=null,"shared use-token allocator is missing");
                var owner=new ItemOperationOwnership();owner.SetSession(1);
                long first=(long)allocator.Invoke(owner,null),second=(long)allocator.Invoke(owner,null);
                Require(first>0 && second>first,"separate owners receive unique tokens");
                Require(owner.TryBeginUse(1,12,first),"first tool admitted");owner.EndUse(1,first);
                Require(owner.TryBeginUse(1,13,second),"quick item admitted after tool yields");
                owner.EndUse(1,first);Require(owner.IsUseSlot(13),"late tool finalizer cannot release quick item");
                owner.EndUse(1,second);Require(!owner.AnyProtected,"matching lease released");
                Require(typeof(JueMingR.Features.Processing.ProcessingOptions).Assembly.GetType("JueMingR.Features.Tools.MiningRegion")!=null,"bounded mining region is missing");
                Policies();Storage();
            }
            catch(Exception error){failures.Add("Tools: "+error);}
        }
        private static void Policies()
        {
            int[] expected={6,7,8,9,166,167,168,169,22,37,56,58,204,107,108,111,211,221,222,223,63,64,65,66,67,68,178,566,123,224,404,407,408,48,232,745,750};
            Require(Enumerable.Range(0,1000).Where(MiningRegion.Supported).OrderBy(v=>v).SequenceEqual(expected.OrderBy(v=>v)),"all original 33 plus four current spikes; no backing stone");
            var tiles=new Dictionary<string,int>{{"0,0",63},{"3,3",566},{"6,6",178},{"10,10",64},{"1,1",1}};
            Func<int,int,int> read=(x,y)=>{int value;return tiles.TryGetValue(x+","+y,out value)?value:0;};
            var region=new MiningRegion();Require(region.Select(0,0,63,read) && region.Count==3,"three-cell diagonal mixed gems; four-cell gap excluded");
            Require(!region.TooFar(36,36) && region.TooFar(37,36),"distance is to remaining bbox on each axis, not seed radius");
            tiles.Remove("0,0");Require(region.Select(0,0,63,read,true) && region.Count==2,"real removed seed reaches nearby surviving gems");
            Require(!region.AddFallen(new MiningPoint(81,0,63)) && !region.AddFallen(new MiningPoint(5,5,1)),"falling cannot escape original scan domain or admit backing stone");
            Require(region.Select(0,0,6,(x,y)=>6) && region.Count==512 && region.Truncated,"large dense vein retains 512 and reports truncation");
            var queue=new ReplantQueue();for(int i=0;i<48;i++)Require(queue.Add(i,1,i%7,10),"48 bounded fallback slots");
            Require(!queue.Add(48,1,1,10) && !queue.Add(0,1,0,500),"capacity and repeated aims do not renew");
            for(int i=0;i<48;i++)Require(queue.Next()==i,"round robin cannot strand later seeded plots");
            queue.Expire(609);Require(queue.Count==48,"retained until 600 actual updates");queue.Expire(610);Require(queue.Count==0,"all original deadlines expire without aiming renewal");
            foreach(int mode in new[]{1,2}){var option=new ToolOptions().WithMode(mode).Toggle();Require(option.Mode==0 && option.LastMode==mode && option.WithCategories(0).Toggle().Mode==mode,"off/categories preserve last real mode");}
        }
        private static void Storage()
        {
            string directory=Path.Combine(Path.GetTempPath(),"JueMingR-tools-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try
            {
                for(int domain=0;domain<3;domain++)
                {
                    string path=Path.Combine(directory,domain+".json");
                    using(var settings=new ToolSettings(new AtomicFileDocument(path,65536),domain))
                    {
                        Until(()=>{settings.Poll();return settings.Loaded;});Require(settings.Ready && settings.Value.Mode==0 && settings.Value.LastMode==1 && settings.Value.Categories==255,"independent default off/all/unbound mode choice");
                        Require(settings.Set(new ToolOptions(domain==1?1:2)) && !settings.Ready,"pending commit revokes operation permission");
                        Require(!settings.Set(new ToolOptions()),"busy cannot replace in-flight command");Until(()=>{settings.Poll();return !settings.Busy;});
                        Require(settings.CompletionSucceeded && settings.CompletedCommandId==settings.AcceptedCommandId && settings.Value.Mode==(domain==1?1:2),"only exact commit publishes");
                        Require(settings.Set(settings.Value.WithMode(0)),"disable commit accepted");Until(()=>{settings.Poll();return !settings.Busy;});
                    }
                    using(var settings=new ToolSettings(new AtomicFileDocument(path,65536),domain))
                    {Until(()=>{settings.Poll();return settings.Loaded;});Require(settings.Ready && settings.Value.Mode==0 && settings.Value.LastMode==(domain==1?1:2),"restart preserves last non-off mode");}
                    string future=File.ReadAllText(path).Replace("\"version\":1","\"version\":99");File.WriteAllText(path,future);
                    using(var settings=new ToolSettings(new AtomicFileDocument(path,65536),domain))
                    {Until(()=>{settings.Poll();return settings.Loaded;});Require(settings.Protected && !settings.Ready && !settings.Set(new ToolOptions(1)) && File.ReadAllText(path)==future,"future file protected unchanged");}
                }
            }
            finally
            {
                string full=Path.GetFullPath(directory),temp=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
                if(!full.StartsWith(temp,StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("JueMingR-tools-",StringComparison.Ordinal))throw new InvalidOperationException("Unsafe test cleanup");Directory.Delete(full,true);
            }
        }
        private static void Until(Func<bool> done){for(int i=0;i<5000;i++){if(done())return;Thread.Sleep(1);}throw new TimeoutException("tools settings fixture");}
        private static void Require(bool value,string why){if(!value)throw new InvalidOperationException(why);}
    }
}
