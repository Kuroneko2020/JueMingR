using System;
using System.Collections.Generic;
using JueMingR.Features.QuickItems;
using System.Threading;
using System.Diagnostics;

namespace JueMingR.ArchitectureTests
{
    internal static class QuickItemChecks
    {
        internal static void Check(IList<string> failures)
        {
            var id = "0123456789abcdef0123456789abcdef";
            Func<int, bool, QuickItemEntry> use = (type, compatible) => new QuickItemEntry(id, type, QuickItemMode.Use, compatible, true);
            Func<int,int,QuickItemCandidate> item = (slot,type) => new QuickItemCandidate(slot,type,1,true);
            var choice = QuickItemRules.Choose(use(50,true), new[] {item(0,3199), item(25,50)}, 0);
            if (choice.Slot != 25 || choice.TargetType != 50) failures.Add("Quick rules: preferred provider lost to selected substitute.");
            choice = QuickItemRules.Choose(use(5358,true), new[] {item(2,50), item(33,5361)}, 2);
            if (choice.Slot != 33 || choice.TargetType != 5358) failures.Add("Quick rules: preferred physical phone family was not converted.");
            choice = QuickItemRules.Choose(use(50,true), new[] {item(2,5360), item(28,3199)}, 2);
            if (choice.Slot != 28) failures.Add("Quick rules: ready substitute should precede conversion.");
            choice = QuickItemRules.Choose(use(50,false), new[] {item(2,3199)}, 2);
            if (choice.Found) failures.Add("Quick rules: strict provider substituted.");
            foreach (int target in new[] {50,3199,3124,5358,4263,5360,4819,5361,5359})
            {
                if (QuickItemRules.Choose(use(target,true), new[] {item(0,2350), item(1,4870)},0).Found)
                    failures.Add("Quick rules: potion accidentally became compatible provider.");
            }
            foreach(int target in new[]{5358,5360,5361,5359})
                if(!QuickItemRules.Choose(use(target,false),new[]{new QuickItemCandidate(17,5437,1,false)},0).Found)
                    failures.Add("Quick rules: native dummy recovery cannot reach a legal phone target.");
            if(QuickItemRules.Choose(use(5358,false),new[]{new QuickItemCandidate(17,5437,2,false)},0).Found)
                failures.Add("Quick rules: dummy recovery bypasses singleton constraint.");
            try{use(5437,false);failures.Add("Quick rules: internal dummy is bindable.");}catch(ArgumentException){}
            if (QuickItemRules.Choose(use(5359,true), new[] {item(0,50)},0).Found) failures.Add("Quick rules: world spawn collapsed into personal spawn.");
            choice = QuickItemRules.Choose(use(8,false), new[] {item(49,8), item(1,8), item(28,8), item(58,8)},28);
            if (choice.Slot != 28) failures.Add("Quick rules: current same-type provider did not win.");
            choice = QuickItemRules.Choose(use(8,false), new[] {item(49,8), item(1,8), item(28,8), item(58,8)},0);
            if (choice.Slot != 1) failures.Add("Quick rules: hotbar/main/mouse ordering incorrect.");
            if (QuickItemRules.Choose(use(8,false), new[] {item(50,8), item(58,8), new QuickItemCandidate(2,8,1,true,true)},0).Found)
                failures.Add("Quick rules: excluded or already-owned slot admitted.");
            int[][] families = {new[]{2611,5526},new[]{4131,5325},new[]{4346,5391},new[]{4767,5453},new[]{5059,5060},new[]{5309,5454},
                new[]{5323,5455},new[]{5324,5329,5330},new[]{5358,5360,5361,5359},new[]{6168,6169,6193,6194},new[]{6190,6195}};
            foreach (var family in families) for (int i=0;i<family.Length;i++)
            {
                int target=family[(i+1)%family.Length];
                if (QuickItemRules.NextState(family[i])!=target) failures.Add("Quick rules: missing legal native directed edge.");
                choice=QuickItemRules.Choose(new QuickItemEntry(id,target,QuickItemMode.SetState,false,true),new[]{new QuickItemCandidate(17,family[i],1,false)},0);
                if (!choice.Found || choice.Use || choice.TargetType!=target) failures.Add("Quick rules: state-only operation requires fictitious left-click use.");
            }
            var round = QuickItemDocument.Decode(QuickItemDocument.Encode(new QuickItemDocument(true,true,new[]{use(5359,true)})));
            if (!round.KeepFavorited || !round.Enabled || round.Entries.Count!=1 || round.Entries[0].Id!=id || round.Entries[0].Target!=5359)
                failures.Add("Quick config: value roundtrip changed stable identity or world-spawn target.");
            ExerciseStorage(failures, use(50,true));
        }
        private static void Wait(QuickItemSettings settings, Func<bool> done)
        { var time=Stopwatch.StartNew(); while(!done() && time.ElapsedMilliseconds<5000) {settings.Poll();Thread.Sleep(1);} if(!done())throw new Exception("Quick settings worker timeout."); }
        private static void ExerciseStorage(IList<string> failures, QuickItemEntry entry)
        {
            var storage=new HotkeyCoreChecks.MemoryStorage();
            using(var settings=new QuickItemSettings(storage))
            {
                Wait(settings,()=>settings.Loaded);
                if(settings.Enabled || settings.KeepFavorited || settings.Current.Entries.Count!=0)failures.Add("Quick config: defaults enabled or seeded entries.");
                string reason; storage.Block.Reset();
                if(!settings.TryChange(new QuickItemDocument(true,true,new[]{entry}),entry.Id,out reason))throw new Exception(reason);
                if(settings.Registered(entry.Id))failures.Add("Quick config: uncommitted entry registered.");
                storage.Block.Set();Wait(settings,()=>!settings.Busy);
                if(!settings.CanExecute(entry.Id) || !settings.KeepFavorited)failures.Add("Quick config: reliable commit did not activate.");
                storage.Block.Reset();
                settings.TryChange(settings.Current.Toggles(true,false),null,out reason);
                if(settings.Enabled || !settings.KeepFavorited)failures.Add("Quick config: pending quick disable either executes or clears independent favorite.");
                storage.Block.Set();Wait(settings,()=>!settings.Busy);
                settings.TryChange(settings.Current.Toggles(true,true),null,out reason);Wait(settings,()=>!settings.Busy);
                storage.Fail=true;settings.TryChange(settings.Current.Change(null,entry.Id),entry.Id,out reason);Wait(settings,()=>!settings.Busy);
                if(settings.CanExecute(entry.Id) || settings.Registered(entry.Id) || settings.Current.Find(entry.Id)==null || settings.Message.Contains("已保存"))
                    failures.Add("Quick config: failed deletion revived execution or claimed deletion.");
                storage.Fail=false;settings.TryChange(settings.Current.Change(entry),entry.Id,out reason);Wait(settings,()=>!settings.Busy);
                if(!settings.CanExecute(entry.Id))failures.Add("Quick config: explicit retry failed to restore known saved entry.");
                storage.Fail=storage.Unknown=true;settings.TryChange(settings.Current.Change(null,entry.Id),entry.Id,out reason);Wait(settings,()=>!settings.Busy);
                if(!settings.Protected || !settings.CommitUnconfirmed || settings.Registered(entry.Id))failures.Add("Quick config: unknown delete commit remained executable.");
            }
            using(var restart=new QuickItemSettings(new HotkeyCoreChecks.MemoryStorage{Bytes=storage.Bytes}))
            {Wait(restart,()=>restart.Loaded);if(restart.Current.Find(entry.Id)!=null)failures.Add("Quick config: restart resurrected actually committed deletion.");}
        }
    }
}
