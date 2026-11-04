using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.Processing;
using Terraria;
using Terraria.Localization;

namespace JueMingR.TerrariaHost.Processing
{
    internal sealed class ReforgeTargets
    {
        internal const string InvalidNameHint="需输入当前语言下存在的词缀";
        private readonly HostProcessing host;
        private readonly HashSet<int> valid=new HashSet<int>();
        private Item item;
        private int type;
        private long revision=-1;
        private GameCulture culture;
        internal string Message {get;private set;}
#if DEBUG
        internal long Resolutions;
#endif
        internal ReforgeTargets(HostProcessing host){this.host=host;}
        internal bool HasTargets(Item current)
        {
            var settings=host.Settings[2];
            if(ReferenceEquals(item,current) && type==(current?.type??0) && revision==settings.Revision && ReferenceEquals(culture,Language.ActiveCulture))return valid.Count!=0;
#if DEBUG
            Resolutions++;
#endif
            item=current;type=current?.type??0;revision=settings.Revision;culture=Language.ActiveCulture;valid.Clear();Message=null;
            if(current==null || current.IsAir)return false;
            if(!current.CanHavePrefixes()){if(settings.Value.Names.Count!=0)Message="当前物品无法重铸。";return false;}
            Item plain=current.Clone();plain.ResetPrefix();
            bool anyKnown=false;
            foreach(string name in settings.Value.Names)
            {
                for(int id=1;id<Lang.prefix.Length;id++)if(NameEquals(name,Lang.prefix[id]?.Value))
                {anyKnown=true;if(CanRoll(plain,id))valid.Add(id);}
            }
            // The saved list is shared across items. Inapplicable members do
            // not make a usable mixed list erroneous; only an empty reachable
            // set blocks spending. Item/list/language identity owns this cache.
            if(valid.Count==0 && settings.Value.Names.Count!=0)Message=anyKnown?"名单中没有适用于当前物品的词缀。":"当前语言下找不到名单中的词缀，请修改名单。";
            return valid.Count!=0;
        }
        internal bool Matches(Item current){return HasTargets(current) && valid.Contains(current.prefix);}
        // Presentation asks this without publishing settings. Submission repeats
        // the same check: a visible button is never a saved-command permission.
        internal bool TryValidateAddition(string input,out string name,out string reason)
        {
            reason=null;name=(input??"").Trim();bool known=false;
            var s=host.Settings[2];
            if(!host.Controls(2)){reason="请稍后再试。";return false;}
            if(name.Length==0 || name.Length>128 || name.Any(char.IsControl)){reason=InvalidNameHint;return false;}
            if(s.Value.Names.Count>=256){reason="词缀名单已满。";return false;}
            // Adding configures a reusable target, not the current reforge slot.
            // Reachability belongs exclusively to HasTargets at execution time.
            for(int id=1;id<Lang.prefix.Length;id++)if(NameEquals(name,Lang.prefix[id]?.Value)){known=true;break;}
            if(!known){reason=InvalidNameHint;return false;}
            foreach(string existing in s.Value.Names)if(NameEquals(existing,name)){reason=InvalidNameHint;return false;}
            return true;
        }
        internal bool Add(string input,out string reason)
        {
            string name;if(!TryValidateAddition(input,out name,out reason))return false;
            var s=host.Settings[2];
            try{if(!host.Controls(2) || !s.Set(new ProcessingOptions(s.Value.Enabled,s.Value.Names.Concat(new[]{name})))){reason="请稍后再试。";return false;}}
            catch(ArgumentException){reason="词缀名称或名单长度超出范围。";return false;}return true;
        }
        internal bool Remove(string name){var s=host.Settings[2];return host.Controls(2) && s.Value.Names.Contains(name) && s.Set(new ProcessingOptions(s.Value.Enabled,s.Value.Names.Where(v=>v!=name)));}
        private static bool NameEquals(string a,string b){return !string.IsNullOrEmpty(b) && string.Equals(a,b,StringComparison.OrdinalIgnoreCase);}
        private static bool CanRoll(Item plain,int id)
        {float a,b,c,d,e,f,value;int crit,tag,armor;return plain.CanRollPrefix(id) && plain.TryGetPrefixStatMultipliersForItem(id,out a,out b,out c,out d,out e,out f,out crit,out tag,out armor,out value);}

    }
}
