using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace JueMingR.Features.Fishing
{
    public sealed class FishList
    {
        private readonly HashSet<FishKey> keys;
        public IReadOnlyList<FishKey> Exact {get;}
        public IReadOnlyList<string> Keywords {get;}
        public FishList(IEnumerable<FishKey> exact=null,IEnumerable<string> keywords=null)
        {
            FishKey[] values=(exact??Enumerable.Empty<FishKey>()).ToArray();
            keys=new HashSet<FishKey>(values);
            if(keys.Count!=values.Length || values.Any(x=>x.Id<=0 || x.Kind!=FishKind.Item && x.Kind!=FishKind.Npc))throw new ArgumentException("Invalid fish list.");
            string[] words=(keywords??Enumerable.Empty<string>()).ToArray();
            if(words.Any(x=>string.IsNullOrWhiteSpace(x) || x!=x.Trim()) || new HashSet<string>(words,StringComparer.Ordinal).Count!=words.Length)throw new ArgumentException("Invalid keywords.");
            Exact=Array.AsReadOnly(values);Keywords=Array.AsReadOnly(words);
        }
        public bool Contains(FishKey key){return keys.Contains(key);}
        public bool Matches(string name)
        {if(string.IsNullOrEmpty(name))return false;foreach(string word in Keywords)if(CultureInfo.CurrentCulture.CompareInfo.IndexOf(name,word,CompareOptions.IgnoreCase)>=0)return true;return false;}
        public FishList Add(IEnumerable<FishKey> items){return new FishList(Exact.Concat(items).Distinct(),Keywords);}
        public FishList Remove(FishKey key){return new FishList(Exact.Where(x=>!x.Equals(key)),Keywords);}
        public FishList Add(string keyword){return new FishList(Exact,Keywords.Concat(new[]{keyword.Trim()}).Distinct(StringComparer.Ordinal));}
        public FishList Remove(string keyword){return new FishList(Exact,Keywords.Where(x=>x!=keyword));}
    }
    public sealed class FishPreset
    {
        public int Mode {get;}
        public int Match {get;}
        public string Name {get;}
        public FishList Content {get;}
        public FishPreset(int mode,int match,FishList content,string name=null)
        {
            FishingOptions.Scope(mode,match);if(content==null)throw new ArgumentNullException(nameof(content));
            if(match==0 && content.Keywords.Count!=0 || match==1 && content.Exact.Count!=0)throw new ArgumentException("Mixed preset.");
            Mode=mode;Match=match;Content=content;
            Name=name??AutoName(mode,match,content);
            if(string.IsNullOrWhiteSpace(Name))throw new ArgumentException("Invalid preset name.");
        }
        private static string AutoName(int mode,int match,FishList list)
        {
            // Length prefixes preserve unambiguous ordered content, including
            // separators in keywords. Names are stable without a naming dialog.
            var text=new StringBuilder();
            if(match==0)foreach(var key in list.Exact)text.Append(key.ToString()).Append(';');
            else foreach(string word in list.Keywords)text.Append(word.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(word);
            ulong hash=14695981039346656037UL;
            foreach(byte b in Encoding.UTF8.GetBytes(text.ToString()))hash=unchecked((hash^b)*1099511628211UL);
            return "auto-"+mode+"-"+match+"-"+hash.ToString("x16",CultureInfo.InvariantCulture);
        }
    }
    public sealed class FishingOptions
    {
        public bool Auto {get;}
        public bool Loadout {get;}
        public bool Equipment {get;}
        public bool Cut {get;}
        public int StoreMode {get;}
        public int LastStoreMode {get;}
        public int FilterMode {get;}
        public int LastFilterMode {get;}
        public int Match {get;}
        public int Crates {get;}
        public int Quests {get;}
        public int Npcs {get;}
        public int EditingMode {get{return FilterMode==0?LastFilterMode:FilterMode;}}
        private readonly FishList[] lists;
        public IReadOnlyList<FishPreset> Presets {get;}
        public FishingOptions(bool auto=false,bool loadout=false,bool equipment=false,bool cut=false,int storeMode=0,int lastStoreMode=1,
            int filterMode=0,int lastFilterMode=1,int match=0,int crates=1,int quests=1,int npcs=2,IEnumerable<FishList> lists=null,IEnumerable<FishPreset> presets=null)
        {
            if(loadout && equipment || storeMode<0 || storeMode>2 || lastStoreMode<1 || lastStoreMode>2 || filterMode<0 || filterMode>2 || lastFilterMode<1 || lastFilterMode>2 ||
                match<0 || match>1 || crates<0 || crates>2 || quests<0 || quests>2 || npcs<0 || npcs>2)throw new ArgumentException("Invalid fishing settings.");
            Auto=auto;Loadout=loadout;Equipment=equipment;Cut=cut;StoreMode=storeMode;LastStoreMode=storeMode==0?lastStoreMode:storeMode;
            FilterMode=filterMode;LastFilterMode=filterMode==0?lastFilterMode:filterMode;Match=match;Crates=crates;Quests=quests;Npcs=npcs;
            this.lists=lists==null?new[]{new FishList(),new FishList(),new FishList(),new FishList()}:lists.ToArray();
            if(this.lists.Length!=4 || this.lists.Any(x=>x==null))throw new ArgumentException("Four lists required.");
            for(int i=0;i<4;i++)if(i%2==0?this.lists[i].Keywords.Count!=0:this.lists[i].Exact.Count!=0)throw new ArgumentException("Mixed scoped list.");
            FishPreset[] saved=(presets??Enumerable.Empty<FishPreset>()).ToArray();
            var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(var p in saved)if(p==null || !names.Add(p.Mode+":"+p.Match+":"+p.Name))throw new ArgumentException("Duplicate preset.");
            Presets=Array.AsReadOnly(saved);
        }
        internal static int Scope(int mode,int match)
        {if(mode<1 || mode>2 || match<0 || match>1)throw new ArgumentOutOfRangeException();return (mode-1)*2+match;}
        public FishList List(int mode,int match){return lists[Scope(mode,match)];}
        public FishingOptions WithList(int mode,int match,FishList list)
        {var next=(FishList[])lists.Clone();next[Scope(mode,match)]=list;return Copy(next,Presets);}
        public FishingOptions SavePreset(int mode,int match)
        {var preset=new FishPreset(mode,match,List(mode,match));return Copy(lists,Presets.Where(x=>x.Mode!=mode || x.Match!=match || x.Name!=preset.Name).Concat(new[]{preset}));}
        public FishingOptions DeletePreset(int mode,int match,string name)
        {Scope(mode,match);return Copy(lists,Presets.Where(x=>x.Mode!=mode || x.Match!=match || x.Name!=name));}
        public FishingOptions ApplyPreset(FishPreset preset,int mode,int match)
        {if(preset==null || preset.Mode!=mode || preset.Match!=match)throw new ArgumentException("Preset belongs to another list.");return WithList(mode,match,preset.Content);}
        private FishingOptions Copy(IEnumerable<FishList> next,IEnumerable<FishPreset> saved)
        {return new FishingOptions(Auto,Loadout,Equipment,Cut,StoreMode,LastStoreMode,FilterMode,LastFilterMode,Match,Crates,Quests,Npcs,next,saved);}
        // Feature ids are private to this domain's commands, never persistent
        // hotkey identities: auto, loadout, equipment, store, cut, filter, match,
        // crates, quests, NPCs. Enabling a strategy closes its peer atomically.
        public FishingOptions Change(int feature,int value)
        {
            if(feature<0 || feature>9 || value<0 || value>(feature==3 || feature==5 || feature>=7?2:1))throw new ArgumentOutOfRangeException();
            return new FishingOptions(feature==0?value!=0:Auto,feature==1?value!=0:feature==2 && value!=0?false:Loadout,
                feature==2?value!=0:feature==1 && value!=0?false:Equipment,feature==4?value!=0:Cut,
                feature==3?value:StoreMode,LastStoreMode,feature==5?value:FilterMode,LastFilterMode,feature==6?value:Match,
                feature==7?value:Crates,feature==8?value:Quests,feature==9?value:Npcs,lists,Presets);
        }
        public int State(int feature)
        {switch(feature){case 0:return Auto?1:0;case 1:return Loadout?1:0;case 2:return Equipment?1:0;case 3:return StoreMode;case 4:return Cut?1:0;case 5:return FilterMode;case 6:return Match;case 7:return Crates;case 8:return Quests;case 9:return Npcs;default:throw new ArgumentOutOfRangeException();}}
        public FishingOptions Toggle(int feature)
        {if(feature<0 || feature>5)throw new ArgumentOutOfRangeException();return Change(feature,State(feature)!=0?0:feature==3?LastStoreMode:feature==5?LastFilterMode:1);}
    }
}
