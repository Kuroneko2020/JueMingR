using System;
using System.Collections.Generic;
using JueMingR.Features.Fishing;
using JueMingR.Platform.Information;
using JueMingR.TerrariaHost.Information;
using Terraria;
using Terraria.Localization;

namespace JueMingR.TerrariaHost.Fishing
{
    // Prepared strings only flow into the existing information-window owner.
    // Display demand does not enable automatic fishing or equipment changes.
    internal sealed class FishingDisplay : IDisposable
    {
        private readonly HostFishing host;
        private readonly HostInformation information;
        private FishingCandidates candidates;
        private FishingOptions options;
        private object culture;
        private bool sonar,fullEnabled,filteredEnabled;
        internal string Full {get;private set;}
        internal string Filtered {get;private set;}
        internal bool Enabled {get{return information.Enabled(InformationKind.FullFish) || information.Enabled(InformationKind.FilteredFish);}}
        internal long Builds {get;private set;}
        internal FishingDisplay(HostFishing host,HostInformation information)
        {
            this.host=host;this.information=information;
            information.FishingText=kind=>kind==InformationKind.FullFish?Full:kind==InformationKind.FilteredFish?Filtered:null;
            LanguageManager.Instance.OnLanguageChanged+=LanguageChanged;
        }
        // Resource-pack reload may retain the same GameCulture object while
        // changing item names and therefore keyword-preview decisions.
        private void LanguageChanged(LanguageManager manager){culture=null;}
        public void Dispose(){LanguageManager.Instance.OnLanguageChanged-=LanguageChanged;Clear();}
        internal void Clear(){Full=Filtered=null;candidates=null;options=null;culture=null;}
        internal void Update(Player p)
        {
            if(!Enabled || p==null){Clear();return;}
            Projectile b=null;
            for(int i=0;i<host.Observation.Count;i++)if(host.Observation.Bobbers[i].InLiquid){b=host.Observation.Bobbers[i].Projectile;break;}
            if(b==null || p.HeldItem.fishingPole<=0){Clear();return;}
            var next=host.Catalog.Current(p,b,false);var value=host.Settings.Value;
            bool hasSonar=FishingCatalog.Sonar(p),all=information.Enabled(InformationKind.FullFish),filtered=information.Enabled(InformationKind.FilteredFish);
            if(ReferenceEquals(candidates,next) && ReferenceEquals(options,value) && ReferenceEquals(culture,Language.ActiveCulture) && sonar==hasSonar && fullEnabled==all && filteredEnabled==filtered)return;
            candidates=next;options=value;culture=Language.ActiveCulture;sonar=hasSonar;fullEnabled=all;filteredEnabled=filtered;Builds++;
            Full=all?"完整鱼获："+Describe(next,null):null;
            string condition=value.FilterMode==0?"过滤关闭；":!hasSonar?"未获得声呐；":null;
            Filtered=filtered?"过滤鱼获："+condition+Describe(next,value.FilterMode==0 || !hasSonar?null:value):null;
        }
        private static string Describe(FishingCandidates snapshot,FishingOptions options)
        {
            if(!snapshot.Known || snapshot.Message!=null)return snapshot.Message??"暂不可读取。";
            var names=new List<string>();int total=0;
            foreach(var fish in snapshot.Keys)
            {
                if(fish.Kind!=FishKind.Item)continue;
                string name=FishingCatalog.Name(fish);
                if(options!=null && !FishFilter.Keep(options,fish,name,true,FishingCatalog.Crate(fish),FishingCatalog.Quest(fish)))continue;
                total++;if(names.Count<96)names.Add(name);
            }
            if(total==0)return "暂无符合条件的物品。";
            string result=string.Join("、",names);
            return total>names.Count?result+"……（共 "+total+" 项，显示前 96 项）":result;
        }
    }
}
