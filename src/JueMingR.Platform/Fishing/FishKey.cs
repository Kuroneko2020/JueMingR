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
}
