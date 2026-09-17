namespace JueMingR.Features.KeepFavorited
{
    // Neutral identity owned by one host member in one session. Claims may
    // cross only a proved native operation; an explicit cancellation invalidates
    // earlier claims even if their native completion arrives later.
    public sealed class FavoriteIntent
    {
        public bool Value { get; private set; }
        public long Revision { get; private set; }
        public bool Retired { get; private set; }
        public FavoriteIntent(bool value) { Value=value; }
        public void Explicit(bool value) { if(Retired)return;Value=value;Revision++; }
        public void Inherit(FavoriteClaim claim) { if(!Retired && claim.Valid)Value=true; }
        public FavoriteClaim Claim() {return new FavoriteClaim(this,Revision,Value);}
        public void Retire() {Retired=true;Value=false;Revision++;}
    }
    public struct FavoriteClaim
    {
        private readonly FavoriteIntent source;
        private readonly long revision;
        private readonly bool value;
        internal FavoriteClaim(FavoriteIntent source,long revision,bool value) {this.source=source;this.revision=revision;this.value=value;}
        public bool Valid {get{return value && source!=null && !source.Retired && source.Revision==revision && source.Value;}}
    }
}
