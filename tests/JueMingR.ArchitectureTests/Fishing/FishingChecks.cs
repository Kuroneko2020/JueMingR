using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using JueMingR.Features.Fishing;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class FishingChecks
    {
        internal static void Check(List<string> failures)
        {
            try
            {
                Names();Filters();Scopes();Persistence();
                Equipment();
            }
            catch(Exception error){failures.Add("Fishing: "+error);}
        }
        private static void Names()
        {
            Require(PlayerNameRules.Increment("Test009")=="Test010","ASCII carry preserves width");
            Require(PlayerNameRules.Increment("名字")=="名字1","names without suffix append one");
            Require(PlayerNameRules.Increment("9999999999999999999")=="10000000000000000000","string carry has no integer overflow");
            Require(PlayerNameRules.Normalize(" \t 名\r字\n\t ")=="名字","normalize prior to native save");
            Require(PlayerNameRules.Increment("名９")=="名９1","non ASCII digits are name content");
            Reject(()=>PlayerNameRules.Normalize(" \r\n\t"));Reject(()=>PlayerNameRules.Increment(new string('9',20)));
            Reject(()=>PlayerNameRules.Increment(new string('a',20)));Reject(()=>PlayerNameRules.Normalize("bad\ud800"));
            Require(PlayerNameRules.Normalize(new string('鱼',20)).Length==20,"20 UTF16 units accepted unchanged");
        }
        private static void Equipment()
        {
            int[] ids={2367,2368,2369,5591,5592,5593,5064,3721,2374,5139,5140,5141,5142,5143,5144,5145,5146,2373,2375,4881,3035,3034,855,5331,3250,3251,3252,1250,1251,1252,396,158};
            foreach(int id in ids){FishingGear gear;Require(FishingEquipmentCatalog.TryGet(id,true,out gear),"complete fixed fishing equipment directory: "+id);}
            FishingGear unused;Require(!FishingEquipmentCatalog.TryGet(1,true,out unused) && !FishingEquipmentCatalog.TryGet(4881,false,out unused),"unknown items and dry lava hook excluded");
            Require(FishingEquipmentCatalog.LoadoutScore(new[]{5591,5592,5593,5064,3721,2374},false)==45,"loadout uses 5/10 heuristic, not item ranking weights");
            var candidates=ids.Select(id=>new FishingGearCandidate(id,2,0)).ToArray();
            Require(FishingEquipmentCatalog.Accessories(candidates,true).Select(x=>x.Type).SequenceEqual(new[]{5064,3721,2374,5146,3035,5331}),"distinct bags and earring kept; one bobber/coin/horseshoe; redundant line/box/hook removed");
            Require(!FishingEquipmentCatalog.BestArmor(candidates,0,5591).HasValue && FishingEquipmentCatalog.BestArmor(candidates,0,2367).Value.Type==5591,"strict upgrade; same score does not churn");
            var tie=new[]{new FishingGearCandidate(2374,2,1),new FishingGearCandidate(2374,0,7),new FishingGearCandidate(2374,1,12)};
            Require(FishingEquipmentCatalog.Accessories(tie,false)[0].Source==0,"equal score retains currently worn item");
            Require(FishingEquipmentCatalog.Accessories(new[]{new FishingGearCandidate(3721,2,1),new FishingGearCandidate(4881,2,2)},true).Count==2,"ordinary bag does not cover lava hook");
        }
        private static void Filters()
        {
            var item=new FishKey(FishKind.Item,100);var npc=new FishKey(FishKind.Npc,100);
            var options=new FishingOptions(filterMode:1,crates:0,quests:0,npcs:0).WithList(1,0,new FishList(new[]{item}));
            Require(FishFilter.Keep(options,item,"fish",true,false,false),"explicit whitelist item kept");
            Require(!FishFilter.Keep(options,npc,"fish",true,false,false),"NPC and item same ID cannot collide");
            Require(FishFilter.Keep(options,npc,"fish",false,false,false) && FishFilter.Keep(options,null,null,true,false,false),"missing sonar and unknown always keep");
            Require(!FishFilter.Keep(options.Change(5,2).WithList(2,0,new FishList(new[]{item})).Change(7,1),item,"crate",true,true,false),"explicit blacklist beats special allow");
            Require(FishFilter.Keep(options.Change(7,2),item,"crate",true,true,false),"explicit whitelist beats special deny");
            Require(FishFilter.Keep(options.Change(7,1).Change(8,2),npc,"both",true,true,true),"crate special precedes quest and NPC");
            Require(!FishFilter.Keep(options.Change(8,2).Change(9,1),npc,"quest NPC",true,false,true),"quest special precedes NPC allow");
            var old=CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("tr-TR");
                var words=options.Change(6,1).WithList(1,1,new FishList(keywords:new[]{"ışık"}));
                Require(FishFilter.Keep(words,item,"IŞIK balık",true,false,false),"localized current-culture ignore-case matching");
                Require(!FishFilter.Keep(words,item,"InternalNameOnly",true,false,false),"unmatched display name cannot match other metadata");
            }
            finally{CultureInfo.CurrentCulture=old;}
            var defaults=new FishingOptions(filterMode:1);
            Require(FishFilter.Keep(defaults,item,"crate",true,true,false) && FishFilter.Keep(defaults,item,"quest",true,false,true) && !FishFilter.Keep(defaults,npc,"npc",true,false,false),"default special behavior");
        }
        private static void Scopes()
        {
            var items=Enumerable.Range(1,300).Select(x=>new FishKey(FishKind.Item,x)).ToArray();
            var options=new FishingOptions().WithList(1,0,new FishList(items)).WithList(2,1,new FishList(keywords:new[]{"a\"b","空 格"})).SavePreset(1,0).SavePreset(2,1);
            Require(options.List(1,0).Exact.Count==300 && options.List(2,0).Exact.Count==0 && options.List(1,1).Keywords.Count==0,"four scopes independent beyond display cap");
            var cleared=options.WithList(1,0,new FishList());
            Require(options.List(1,0).Exact.Count==300 && cleared.List(2,1).Keywords.Count==2,"immutable clear affects current only");
            var applied=cleared.ApplyPreset(options.Presets[0],1,0);
            Require(applied.List(1,0).Exact.Count==300 && !applied.Auto && applied.FilterMode==0 && applied.Crates==1,"preset only replaces list");
            Reject(()=>options.ApplyPreset(options.Presets[0],2,0));
            Require(options.SavePreset(1,0).Presets.Count==2,"same scoped content replaces stable auto preset");
            Require(options.DeletePreset(1,0,options.Presets[0].Name).Presets.Count==1,"delete exact scoped preset");
            var exclusive=options.Change(1,1).Change(2,1);Require(exclusive.Equipment && !exclusive.Loadout,"equip modes exclusive in committed candidate");
            Require(exclusive.Change(1,1).Loadout && !exclusive.Change(1,1).Equipment,"reverse exclusivity");
            Require(options.Change(3,2).Toggle(3).WithList(2,0,new FishList()).Toggle(3).StoreMode==2,"store last reliable mode survives unrelated edits");
            Require(options.Change(5,2).Toggle(5).Change(6,1).Toggle(5).FilterMode==2,"filter toggle restores remembered non-off mode");
        }
        private static void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new InvalidOperationException("Invalid command accepted.");}
        private static void Persistence()
        {
            var codec=new FishingCodec();
            var options=new FishingOptions(storeMode:2,filterMode:2).WithList(1,0,new FishList(Enumerable.Range(1,6200).Select(i=>new FishKey(FishKind.Item,i))))
                .WithList(2,1,new FishList(keywords:new[]{"鱼\\\"\t名","换\n行","😀"})).SavePreset(2,1).Toggle(3).Toggle(5);
            byte[] bytes=codec.Encode(options);var read=codec.Decode(bytes);
            Require(read.List(1,0).Exact.Count==6200 && read.List(2,1).Keywords.SequenceEqual(new[]{"鱼\\\"\t名","换\n行","😀"}) && read.Presets.Count==1,"full catalog and escaped localized keywords persist beyond 96");
            Require(read.Toggle(3).StoreMode==2 && read.Toggle(5).FilterMode==2,"restart retains both non-off modes");
            string json=Encoding.UTF8.GetString(codec.Encode(new FishingOptions()));
            foreach(string invalid in new[]{json.Replace("\"version\":1","\"version\":77"),json.Replace("\"auto\":false","\"auto\":false,\"future\":true"),
                json.Replace("\"auto\":false","\"auto\":false,\"auto\":true"),json.Replace("\"storeMode\":0","\"storeMode\":2"),json.Replace("\"keywords\":[]","\"keywords\":[],\"extra\":0")})
            {try{codec.Decode(Encoding.UTF8.GetBytes(invalid));throw new InvalidOperationException("Unknown/ambiguous fishing file accepted.");}catch(PreferenceFormatException){}}
            string directory=Path.Combine(Path.GetTempPath(),"JueMingR-fishing-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try
            {
                string path=Path.Combine(directory,"fishing.json");
                using(var settings=new FishingSettings(new AtomicFileDocument(path,FishingCodec.MaximumBytes)))
                {
                    Until(()=>{settings.Poll();return settings.Loaded;});Require(settings.Ready && !settings.Value.Auto,"missing file defaults off");
                    Require(settings.Set(options) && settings.Busy && settings.Value.List(1,0).Exact.Count==0,"accepted save does not publish before receipt");
                    Require(!settings.Set(new FishingOptions()),"one consistency command at a time");
                    Until(()=>{settings.Poll();return !settings.Busy;});Require(settings.CompletionSucceeded && settings.CompletedCommandId==settings.AcceptedCommandId && settings.Value.List(1,0).Exact.Count==6200,"matching reliable save publishes whole list");
                }
                using(var settings=new FishingSettings(new AtomicFileDocument(path,FishingCodec.MaximumBytes)))
                {Until(()=>{settings.Poll();return settings.Loaded;});Require(settings.Value.List(1,0).Exact.Count==6200,"actual file restart roundtrip");}
                string future=File.ReadAllText(path).Replace("\"version\":1","\"version\":77");File.WriteAllText(path,future);
                using(var settings=new FishingSettings(new AtomicFileDocument(path,FishingCodec.MaximumBytes)))
                {Until(()=>{settings.Poll();return settings.Loaded;});Require(settings.Protected && !settings.Set(options) && File.ReadAllText(path)==future,"future file protected without rewriting");}
                foreach(bool unknown in new[]{false,true})using(var settings=new FishingSettings(new FailingStorage(unknown)))
                {
                    Until(()=>{settings.Poll();return settings.Loaded;});Require(settings.Set(options),"failure candidate accepted");
                    Until(()=>{settings.Poll();return !settings.Busy;});
                    Require(!settings.CompletionSucceeded && settings.Value.List(1,0).Exact.Count==0 && settings.Protected==unknown && settings.Message!=null,"failed or unknown receipt never publishes candidate");
                    Require(settings.Set(options)!=unknown,"only known not-committed failure permits explicit retry");
                }
            }
            finally
            {
                string full=Path.GetFullPath(directory),temp=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
                if(!full.StartsWith(temp,StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("JueMingR-fishing-",StringComparison.Ordinal))throw new InvalidOperationException("Unsafe cleanup");
                Directory.Delete(full,true);
            }
        }
        private sealed class FailingStorage : IPreferenceStorage
        {
            private readonly bool unknown;
            internal FailingStorage(bool unknown){this.unknown=unknown;}
            public PreferenceReadResult Read(){return new PreferenceReadResult(PreferenceReadStatus.Missing,null,"missing",null);}
            public PreferenceWriteResult Write(string expectedIdentity,byte[] bytes)
            {return new PreferenceWriteResult(PreferenceWriteStatus.IoFailure,null,"injected before commit / unconfirmed",unknown,unknown);}
            public void Dispose(){}
        }
        private static void Until(Func<bool> done){for(int i=0;i<5000;i++){if(done())return;Thread.Sleep(1);}throw new TimeoutException("Fishing settings fixture");}
        private static void Require(bool value,string why){if(!value)throw new InvalidOperationException(why);}
    }
}
