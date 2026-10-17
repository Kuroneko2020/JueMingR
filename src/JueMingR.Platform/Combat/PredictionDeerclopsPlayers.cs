using System;
namespace JueMingR.Platform.Combat
{
    // Only the two native numbered choices and local Slow receiver are copied.
    // Areas are observations; only the matching rolling player may advance one.
    public struct PredictionDeerclopsPlayer : IEquatable<PredictionDeerclopsPlayer>
    {
        public readonly object Token;
        public readonly int Slot,SlowTicks;
        public readonly MotionRect Area;
        public readonly bool Active,Dead,Ghost,Snow,God,SlowImmune,HasSlow,Slow;
        public PredictionDeerclopsPlayer(object token,int slot,MotionRect area,bool active,bool dead,bool ghost,bool snow,bool god,bool slowImmune,bool hasSlow,int slowTicks,bool slow)
        {Token=token;Slot=slot;Area=area;Active=active;Dead=dead;Ghost=ghost;Snow=snow;God=god;SlowImmune=slowImmune;HasSlow=hasSlow;SlowTicks=slowTicks;Slow=slow;}
        public bool Equals(PredictionDeerclopsPlayer b)
        {return ReferenceEquals(Token,b.Token) && Slot==b.Slot && PredictionPlayers.Same(Area,b.Area) && Active==b.Active && Dead==b.Dead && Ghost==b.Ghost && Snow==b.Snow && God==b.God && SlowImmune==b.SlowImmune && HasSlow==b.HasSlow && SlowTicks==b.SlowTicks && Slow==b.Slow;}
    }
    public sealed class PredictionDeerclopsPlayers : IEquatable<PredictionDeerclopsPlayers>
    {
        public readonly PredictionDeerclopsPlayer Direct,Closest,Local;
        public PredictionDeerclopsPlayers(PredictionDeerclopsPlayer direct,PredictionDeerclopsPlayer closest,PredictionDeerclopsPlayer local)
        {Direct=direct;Closest=closest;Local=local;}
        public bool TryGet(int slot,out PredictionDeerclopsPlayer value)
        {if(Direct.Token!=null && Direct.Slot==slot){value=Direct;return true;}if(Closest.Token!=null && Closest.Slot==slot){value=Closest;return true;}if(Local.Token!=null && Local.Slot==slot){value=Local;return true;}value=default(PredictionDeerclopsPlayer);return false;}
        public bool Equals(PredictionDeerclopsPlayers b){return b!=null && Direct.Equals(b.Direct) && Closest.Equals(b.Closest) && Local.Equals(b.Local);}
    }
}
