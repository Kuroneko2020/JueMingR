using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JueMingR.Features.KeepFavorited;
using JueMingR.Features.QuickItems;
using JueMingR.Platform.Runtime;
using JueMingR.TerrariaHost.QuickItems;
using Terraria;

namespace JueMingR.TerrariaHost.KeepFavorited
{
    internal sealed class HostKeepFavorited : IRuntimeFeature
    {
        internal sealed class Member
        {
            internal readonly Item Item;
            internal readonly FavoriteIntent Intent;
            internal int Type,Prefix,Seen;
            internal Item[] Inactive; internal int InactiveSlot;
            internal Item MirrorSource;
            internal Member(Item item,bool favorite) {Item=item;Type=item.type;Prefix=item.prefix;Intent=new FavoriteIntent(favorite);}
        }
        private sealed class Identity : IEqualityComparer<Item>
        {public bool Equals(Item a,Item b){return ReferenceEquals(a,b);}public int GetHashCode(Item value){return RuntimeHelpers.GetHashCode(value);}}
        private readonly Dictionary<Item,Member> members=new Dictionary<Item,Member>(new Identity());
        private readonly List<Item> retired=new List<Item>();
        private readonly HostQuickItems host;
        private bool observing,failed,wasEnabled;
        private int scan,depth;
        // Session identity survives softcore death and temporary action denial.
        // A missing observation is not evidence that an item left this session.
        internal Player Player {get{return host.Runtime.IsSessionActive && System.Threading.Thread.CurrentThread.ManagedThreadId==host.ThreadId ? host.Items.World.SessionPlayer : null;}}
#if DEBUG
        internal long Reads {get;private set;}
        internal long Writes {get;private set;}
#endif
        [System.Diagnostics.Conditional("DEBUG")] private void ReadCount(){
#if DEBUG
            Reads++;
#endif
        }
        [System.Diagnostics.Conditional("DEBUG")] private void WriteCount(){
#if DEBUG
            Writes++;
#endif
        }
        internal bool Available {get{return !failed;}}
        internal int Count {get{return members.Count;}}
        internal Exception SetupError {get;private set;}
        public bool Enabled {get{return !failed && host.Settings.KeepFavorited;}}
        internal bool Active {get{return Enabled && Player!=null;}}
        internal HostKeepFavorited(HostQuickItems host)
        {
            this.host=host;host.Favorite=this;
            try{FavoriteHooks.Install(this);}catch(Exception error){SetupError=error;failed=true;FavoriteHooks.Uninstall();}
        }
        internal static bool Present(Item item) {return item!=null && item.type>0 && item.stack>0;}
        internal Member Find(Item item) {Member member;return item!=null && members.TryGetValue(item,out member)?member:null;}
        internal FavoriteClaim Claim(Item item) {var member=Find(item);return member==null?default(FavoriteClaim):member.Intent.Claim();}
        internal FavoriteClaim TransferClaim(Item item,bool nativeFavorite)
        {var member=Find(item);return member!=null?member.Intent.Claim():nativeFavorite && Present(item) && item.favorited?new FavoriteIntent(true).Claim():default(FavoriteClaim);}
        internal bool IsMainArray(Item[] inv) {return Player!=null && ReferenceEquals(inv,Player.inventory);}
        internal bool Contains(Item item) {return Find(item)!=null;}
        internal void Begin() {if(depth==0)Observe();depth++;}
        internal void End() {depth=Math.Max(0,depth-1);if(depth==0)Observe();}
        internal void Link(FavoriteClaim source,Item result)
        {
            if(!Active || !Present(result) || !source.Valid)return;
            var member=Find(result);
            if(member==null){member=new Member(result,false);members.Add(result,member);}
            member.Intent.Inherit(source);member.Type=result.type;member.Prefix=result.prefix;
        }
        internal void Mirror(FavoriteClaim claim,Item source,Item target,bool toMouse)
        {
            Link(claim,target);
            var mirror=Find(toMouse?source:target);
            if(mirror!=null)mirror.MirrorSource=toMouse?target:source;
        }
        internal void Explicit(Item item)
        {
            if(!Active)return;
            var member=Find(item);if(member!=null)member.Intent.Explicit(item.favorited);
        }
        internal void Mutated(Item item,int oldType)
        {
            var member=Find(item);if(member==null)return;
            if(QuickItemRules.NextState(oldType)==item.type){member.Type=item.type;member.Prefix=item.prefix;}
        }
        internal void CorrectParticipant(Item item,bool originalFavorite)
        {
            // Only called for the proved same-type LeftClick favorite swap.
            // Equipment favorite is loadout sharing and is never rewritten.
            var member=Find(item);if(!LegalFavorite(item))return;
            bool intended=member==null?originalFavorite:member.Intent.Value;
            if(item.favorited!=intended){item.favorited=intended;WriteCount();}
        }
        private bool LegalFavorite(Item item)
        {
            if(Player==null)return false;
            if(ReferenceEquals(item,Main.mouseItem))return true;
            for(int i=0;i<50;i++)if(ReferenceEquals(item,Player.inventory[i]))return true;
            return false;
        }
        internal void Observe()
        {
            if(observing || depth!=0)return;
            if(!Enabled){Clear();return;}
            if(Player==null)return;
            observing=true;
            try
            {
                scan++;
                Player p=Player;
                for(int i=0;i<50;i++)Touch(p.inventory[i],true,true);
                for(int i=0;i<p.armor.Length;i++)Touch(p.armor[i],false,false);
                for(int i=0;i<p.miscEquips.Length;i++)Touch(p.miscEquips[i],false,false);
                Touch(Main.mouseItem,true,true);Touch(p.trashItem,false,false);
                Member mirror=Find(p.inventory[58]);
                if(mirror!=null && ReferenceEquals(mirror.MirrorSource,Main.mouseItem) && Present(Main.mouseItem) &&
                    mirror.Type==Main.mouseItem.type && mirror.Prefix==Main.mouseItem.prefix && mirror.Item.stack==Main.mouseItem.stack)
                {mirror.Seen=scan;ReadCount();}
                // Only locations positively handed off by TrySwitchingLoadout.
                // No enumeration/seeding of unknown inactive equipment groups.
                foreach(var member in members.Values)
                    if(member.Seen!=scan && member.Inactive!=null && member.InactiveSlot<member.Inactive.Length &&
                        ReferenceEquals(member.Inactive[member.InactiveSlot],member.Item) && Present(member.Item) && member.Item.type==member.Type && member.Item.prefix==member.Prefix)
                    {member.Seen=scan;ReadCount();}
                retired.Clear();foreach(var pair in members)if(pair.Value.Seen!=scan)retired.Add(pair.Key);
                foreach(Item item in retired){members[item].Intent.Retire();members.Remove(item);}
                wasEnabled=true;
            }
            finally{observing=false;}
        }
        private void Touch(Item item,bool seed,bool restore)
        {
            ReadCount();
            if(!Present(item))return;
            var member=Find(item);
            if(member!=null && (member.Type!=item.type || member.Prefix!=item.prefix))
            {member.Intent.Retire();members.Remove(item);member=null;}
            if(member==null){member=new Member(item,seed && item.favorited);members.Add(item,member);}
            member.Seen=scan;member.Inactive=null;
            if(restore && member.Intent.Value && !item.favorited)
            {item.favorited=true;WriteCount();if(!item.favorited)throw new InvalidOperationException("Favorite write did not persist.");}
        }
        internal void KeepInactive(Item item,Item[] array,int slot)
        {var member=Find(item);if(member!=null && array!=null && slot>=0 && slot<array.Length && ReferenceEquals(array[slot],item)){member.Inactive=array;member.InactiveSlot=slot;}}
        internal void Clear()
        {if(members.Count!=0){foreach(var member in members.Values)member.Intent.Retire();members.Clear();}retired.Clear();wasEnabled=false;}
        public void OnSessionStarted(){Clear();depth=0;}
        public void OnSessionEnded(){Clear();depth=0;}
        public void Update(ulong tick){if(Enabled)Observe();else if(wasEnabled || members.Count!=0)Clear();}
        public void FailClosed(){failed=true;Clear();depth=0;}
    }
}
