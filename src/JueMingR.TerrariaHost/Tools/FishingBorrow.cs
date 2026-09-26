using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Tools
{
    internal enum FishingBorrowPhase { Idle, Borrowed, Returning, RecastOwned, Completed, Cancelled, Expired, Unknown }
    // Narrow collaboration contract for a future fishing participant. While
    // Active it preserves its session and must not submit its own recast. The
    // current capture consumer is the sole owner of this token's one attempt.
    internal sealed class FishingBorrow
    {
        private readonly HostTools host;
        private Item rod,lastCastRod;
        private Player player;
        private int slot,type;
        private long session,selection,deadline,checkAfter,lastCastSession,nextToken;
        private Vector2 target,lastCast;
        private readonly int[] keys=new int[Main.maxProjectiles];
        private readonly Projectile[] references=new Projectile[Main.maxProjectiles];
        private int count;
        internal long Token {get;private set;}
        internal FishingBorrowPhase Phase {get;private set;}
        internal bool Active {get{return Phase==FishingBorrowPhase.Borrowed || Phase==FishingBorrowPhase.Returning || Phase==FishingBorrowPhase.RecastOwned;}}
        internal bool OwnsRecast {get{return Active;}}
        internal long Session {get{return session;}}
        internal bool RecastAttempted {get;private set;}
        internal bool RecastStarted {get;private set;}
        internal bool RecastObserved {get;private set;}
        internal Vector2 OriginalTarget {get{return target;}}
        internal Item Rod {get{return rod;}}
        internal FishingBorrow(HostTools host){this.host=host;}
        // This interval extends beyond the net animation, until the single
        // recast decision ends. Protect the exact rod, never every fishing rod.
        internal bool ProtectRod(Item source){return Active && ReferenceEquals(source,rod) && IntentValid();}
        internal void ObserveCast(Player p,Item item)
        {
            if(!ReferenceEquals(p,host.Player) || item==null || item.fishingPole<=0 || host.Use.InNativeUse)return;
            lastCastRod=item;lastCast=Main.MouseWorld;lastCastSession=host.Runtime.Generation;
        }
        internal long Prepare(Player p)
        {
            if(Active || p.selectedItem<0 || p.selectedItem>=50 || p.HeldItem.fishingPole<=0)return 0;
            int found=0;Vector2 point=Vector2.Zero;
            for(int i=0;i<Main.maxProjectiles;i++)
            {
                var b=Main.projectile[i];if(!Bobber(b,p))continue;if(b.ai[0]>=1)return 0;
                references[found]=b;keys[found]=(int)b.key;found++;point=b.Center;
            }
            if(found==0)return 0;
            player=p;rod=p.HeldItem;slot=p.selectedItem;type=rod.type;session=host.Runtime.Generation;selection=host.SelectionIntent;
            target=ReferenceEquals(lastCastRod,rod) && lastCastSession==session?lastCast:point;
            count=found;Token=++nextToken;RecastAttempted=RecastStarted=RecastObserved=false;deadline=host.Input.Frame+300;Phase=FishingBorrowPhase.Borrowed;return Token;
        }
        internal void NetFinished(long lease,bool started,bool unknown)
        {
            if(lease==0 || lease!=Token || Phase!=FishingBorrowPhase.Borrowed)return;
            if(unknown){End(FishingBorrowPhase.Unknown);return;}
            // Merely selecting the net ends native bobber AI. An animation
            // need not have started for this admitted borrow to owe a return.
            Phase=FishingBorrowPhase.Returning;checkAfter=host.Input.Frame+1;
        }
        internal void Update()
        {
            if(!Active)return;
            if(host.Input.Frame>=deadline){End(FishingBorrowPhase.Expired);return;}
            if(!IntentValid()){End(FishingBorrowPhase.Cancelled);return;}
        }
        private bool IntentValid()
        {
            return ReferenceEquals(player,host.Player) && session==host.Runtime.Generation && host.Mode(0)==1 && host.SelectionIntent==selection &&
                rod!=null && ReferenceEquals(player.inventory[slot],rod) && rod.type==type && rod.stack>0 && rod.fishingPole>0 && !player.dead &&
                !PlayerInput.Triggers.Current.MouseLeft && !PlayerInput.Triggers.Current.MouseRight && !PlayerInput.Triggers.Current.SmartSelect && !host.Capture.Boss();
        }
        internal ToolIntent Choose(Player p)
        {
            if(Phase!=FishingBorrowPhase.Returning || host.Input.Frame<checkAfter || !IntentValid() || !host.Admit(p,false) || p.selectedItem!=slot || p.selectedItemState.HasBufferedChange)return null;
            // Any local bobber (including a newer one not in our snapshot)
            // prevents use: vanilla would pull it instead of making a new cast.
            for(int i=0;i<Main.maxProjectiles;i++)if(Bobber(Main.projectile[i],p)){End(FishingBorrowPhase.Completed);return null;}
            bool disappeared=count>0;
            for(int i=0;i<count;i++)if(references[i]!=null && references[i].active && (int)references[i].key==keys[i])disappeared=false;
            if(!disappeared){End(FishingBorrowPhase.Completed);return null;}
            long lease=Token;
            return new ToolIntent{Kind=ToolKind.Recast,Slot=slot,Target=target,
                Valid=()=>Token==lease && (Phase==FishingBorrowPhase.Returning || Phase==FishingBorrowPhase.RecastOwned) && IntentValid() && !AnyBobber(p),
                Admitted=()=>{if(Token==lease && Phase==FishingBorrowPhase.Returning){RecastAttempted=true;Phase=FishingBorrowPhase.RecastOwned;}},
                Completed=(started,unknown)=>{if(Token==lease && Phase==FishingBorrowPhase.RecastOwned){RecastStarted=started;RecastObserved=AnyBobber(p);End(unknown || started && !RecastObserved?FishingBorrowPhase.Unknown:started?FishingBorrowPhase.Completed:FishingBorrowPhase.Cancelled);}}};
        }
        private static bool Bobber(Projectile b,Player p){return b!=null && b.active && b.bobber && b.owner==p.whoAmI;}
        internal static bool HasBobber(Player p){for(int i=0;i<Main.maxProjectiles;i++)if(Bobber(Main.projectile[i],p))return true;return false;}
        private static bool AnyBobber(Player p){return HasBobber(p);}
        internal void Cancel(){if(Active)End(FishingBorrowPhase.Cancelled);}
        internal void Reset(){Cancel();lastCastRod=null;lastCastSession=0;}
        // Terminal facts remain readable for the participant that lent this
        // session. No terminal state authorizes replay of the same token.
        private void End(FishingBorrowPhase phase){Phase=phase;Array.Clear(references,0,count);count=0;player=null;}
    }
}
