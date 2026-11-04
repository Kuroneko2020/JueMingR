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
            if(current==null || current.IsAir || !current.CanHavePrefixes())return false;
            Item plain=current.Clone();plain.ResetPrefix();
            foreach(string name in settings.Value.Names)
            {
                bool known=false,usable=false;
                for(int id=1;id<Lang.prefix.Length;id++)if(NameEquals(name,Lang.prefix[id]?.Value))
                {known=true;if(CanRoll(plain,id)){valid.Add(id);usable=true;}}
                if(!known)Message="当前语言下找不到名单中的词缀，请修改名单。";
                else if(!usable && Message==null)Message="名单中的部分词缀不适用于当前物品。";
            }
            if(valid.Count==0 && settings.Value.Names.Count!=0 && Message==null)Message="名单中没有适用于当前物品的词缀。";
            return valid.Count!=0;
        }
        internal bool Matches(Item current){return HasTargets(current) && valid.Contains(current.prefix);}
        // Presentation asks this without publishing settings. Submission repeats
        // the same check: a visible button is never a saved-command permission.
        internal bool TryValidateAddition(string input,out string name,out string reason)
        {
            reason=null;name=(input??"").Trim();bool known=false,usable=false;
            var s=host.Settings[2];
            if(!host.Controls(2)){reason="请稍后再试。";return false;}
            if(name.Length==0 || name.Length>128 || name.Any(char.IsControl) || s.Value.Names.Count>=256){reason="词缀名称或名单长度超出范围。";return false;}
            Item current=Main.reforgeItem,plain=current==null || current.IsAir?null:current.Clone();plain?.ResetPrefix();
            for(int id=1;id<Lang.prefix.Length;id++)if(NameEquals(name,Lang.prefix[id]?.Value)){known=true;if(plain==null || CanRoll(plain,id))usable=true;}
            if(!known){reason="请输入当前语言下的完整词缀名称。";return false;}
            if(!usable){reason="这个词缀不适用于当前重铽物品。";return false;}
            foreach(string existing in s.Value.Names)if(NameEquals(existing,name)){reason="名单中已有这个词缀。";return false;}
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

        // The .8 prefix readers depend on category/accessory/vanity and rounded
        // damage/useAnimation/mana/knockBack, not only Item reference/type. A
        // prefixed clone resets through Refresh, including the world variant.
        // Keep one current draft key, never clones or prefix scans per Draw.
        internal struct AdditionItemKey : IEquatable<AdditionItemKey>
        {
            private Item source;
            private int type,stack,damage,animation,mana;
            private byte prefix;
            private float knockBack;
            private bool accessory,vanity;
            private object variant,selectedVariant;
            internal static AdditionItemKey Current()
            {
                var p=Main.reforgeItem;if(p==null)return default(AdditionItemKey);
                return new AdditionItemKey{source=p,type=p.type,stack=p.stack,prefix=p.prefix,damage=p.damage,animation=p.useAnimation,mana=p.mana,knockBack=p.knockBack,accessory=p.accessory,vanity=p.vanity,variant=p.Variant,selectedVariant=p.IsAir?null:Terraria.GameContent.Items.ItemVariants.SelectVariant(p.type)};
            }
            public bool Equals(AdditionItemKey other)
            {return ReferenceEquals(source,other.source) && type==other.type && stack==other.stack && prefix==other.prefix && damage==other.damage && animation==other.animation && mana==other.mana && knockBack==other.knockBack && accessory==other.accessory && vanity==other.vanity && ReferenceEquals(variant,other.variant) && ReferenceEquals(selectedVariant,other.selectedVariant);}
        }
    }
}
