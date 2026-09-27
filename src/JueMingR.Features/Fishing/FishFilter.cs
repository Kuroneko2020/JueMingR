using System;
using System.Globalization;

namespace JueMingR.Features.Fishing
{
    public enum FishKind { Item, Npc }
    public struct FishKey : IEquatable<FishKey>
    {
        public FishKind Kind {get;}
        public int Id {get;}
        public FishKey(FishKind kind,int id)
        {if((kind!=FishKind.Item && kind!=FishKind.Npc) || id<=0)throw new ArgumentOutOfRangeException();Kind=kind;Id=id;}
        public bool Equals(FishKey other){return Kind==other.Kind && Id==other.Id;}
        public override bool Equals(object other){return other is FishKey && Equals((FishKey)other);}
        public override int GetHashCode(){return unchecked(Id*397^(int)Kind);}
        public override string ToString(){return (Kind==FishKind.Item?"Item:":"NPC:")+Id.ToString(CultureInfo.InvariantCulture);}
    }
    public static class FishFilter
    {
        // Unknown observation and absent live sonar always keep. Candidate
        // previews may explicitly supply known facts; they never authorize use.
        public static bool Keep(FishingOptions options,FishKey? candidate,string localizedName,bool sonar,bool crate,bool quest)
        {
            if(options==null || options.FilterMode==0 || !sonar || !candidate.HasValue)return true;
            FishKey key=candidate.Value;FishList list=options.List(options.FilterMode,options.Match);
            bool match=options.Match==0?list.Contains(key):list.Matches(localizedName);
            int special=crate?options.Crates:quest?options.Quests:key.Kind==FishKind.Npc?options.Npcs:0;
            // Explicit special rules override either list, including an empty
            // active list. Follow (0) alone delegates to ordinary matching.
            if(special==1)return true;
            if(special==2)return false;
            return options.FilterMode==1?match:!match;
        }
    }
}
