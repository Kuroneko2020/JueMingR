using System;
using JueMingR.Features.Fishing;
using JueMingR.TerrariaHost.Tools;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Fishing
{
    internal enum FishingPhase { Idle, Waiting, PullRequested, Returning, RecastReady, CastRequested, Cutting }
    internal sealed class FishingSession
    {
        private readonly HostFishing host;
        private Player player,lastPlayer;
        private Item rod,lastRod;
        private int rodSlot,rodType,life,lastSlot;
        private long generation,selection,lastGeneration,nextSession,operation,borrow;
        private Vector2 target,lastTarget;
        private bool manualCast,used,pullConsumed,newCast,truffle,lastTruffle;
        private ulong entered;
        private readonly FishingBobber[] owned=new FishingBobber[Main.maxProjectiles];
        private readonly bool[] pullable=new bool[Main.maxProjectiles];
        private int ownedCount,cutKey;
        private long cutBite=-1;
        internal FishingPhase Phase {get;private set;}
        internal bool Active {get{return Phase!=FishingPhase.Idle;}}
        internal long Token {get;private set;}
        internal long PullToken {get{return pullConsumed?operation:0;}}
        internal Vector2 Target {get{return target;}}
        internal Item Rod {get{return rod;}}
        internal bool InLiquid {get;private set;}
        internal int Liquid {get;private set;}
        internal bool StorageSession {get{return Active && Identity() && player.statLife>=life && host.KeepsSession;}}
        internal FishingSession(HostFishing host){this.host=host;}
        private HostTools Tools {get{return host.Tools;}}
        private bool Identity()
        {return player!=null && ReferenceEquals(player,host.Player) && generation==Tools.Runtime.Generation && selection==Tools.SelectionIntent && rodSlot>=0 && rodSlot<50 && ReferenceEquals(player.inventory[rodSlot],rod) && rod.type==rodType && rod.stack>0 && rod.fishingPole>0 && !player.dead;}
        internal bool Protect(Item item){return Active && Identity() && ReferenceEquals(item,rod);}
        internal void ObserveUse(Player p,Item item)
        {
            if(!ReferenceEquals(p,host.Player) || item==null || item.fishingPole<=0 || Tools.Use.InNativeUse || !PlayerInput.Triggers.Current.MouseLeft)return;
            // A native new use arms only a manual first cast. Pull has no
            // StartActualUse callback and takes control in BeforeNativePull.
            Stop();host.Observation.Invalidate();
            if(p.selectedItem<0 || p.selectedItem>=50 || FishingBorrow.HasBobber(p))return;
            lastPlayer=p;lastRod=item;lastSlot=p.selectedItem;lastTarget=Main.MouseWorld;lastGeneration=Tools.Runtime.Generation;manualCast=true;
            lastTruffle=p.GetFishingConditions().BaitItemType==Terraria.ID.ItemID.TruffleWorm;
        }
        internal void ObserveCreated(Player p,Projectile projectile)
        {
            if(!ReferenceEquals(p,player) || !Active || Phase!=FishingPhase.CastRequested || !Tools.Use.Is(ToolKind.FishingCast) || !projectile.bobber || projectile.owner!=p.whoAmI || (int)projectile.key==-1)return;
            newCast=true;host.Observation.Invalidate();
        }
        internal void Update()
        {
            if(!Active)
            {
                if(!host.NeedsSession || !manualCast || !ReferenceEquals(lastPlayer,host.Player) || lastGeneration!=Tools.Runtime.Generation ||
                    lastSlot<0 || lastSlot>=50 || !ReferenceEquals(lastPlayer.inventory[lastSlot],lastRod) || lastPlayer.selectedItem!=lastSlot || lastPlayer.dead)return;
                for(int i=0;i<host.Observation.Count;i++)if(host.Observation.Bobbers[i].InLiquid)
                {
                    player=lastPlayer;rod=lastRod;rodSlot=lastSlot;rodType=rod.type;generation=lastGeneration;selection=Tools.SelectionIntent;
                    target=lastTarget;life=player.statLife;Token=++nextSession;Phase=FishingPhase.Waiting;manualCast=false;truffle=lastTruffle;Snapshot();Prompt(true,false);break;
                }
                if(!Active)return;
            }
            if(!Identity()){End(player!=null && !player.dead && ReferenceEquals(player,host.Player) && generation==Tools.Runtime.Generation);return;}
            if(player.statLife<life){Stop();return;}life=player.statLife;
            if(!host.KeepsSession && Phase==FishingPhase.Waiting){Stop();return;}
            bool borrowed=Tools.Fishing.Active;
            if(borrowed)
            {borrow=Tools.Fishing.Token;return;}
            if(borrow!=0)
            {
                long previous=borrow;borrow=0;
                if(Tools.Fishing.Token!=previous || Tools.Fishing.Phase!=FishingBorrowPhase.Completed || host.Observation.Count==0){Stop();return;}
                Snapshot();Phase=FishingPhase.Waiting;
            }
            if(player.selectedItem!=rodSlot && !(Tools.Use.Active && (Tools.Use.Intent.SelectionOnly || Tools.Use.Intent.Kind< ToolKind.FishingPull)) && !Tools.Items.ReturningSelection){End(true);return;}
            InLiquid=false;
            for(int i=0;i<host.Observation.Count;i++)if(host.Observation.Bobbers[i].InLiquid)
            {InLiquid=true;var b=host.Observation.Bobbers[i].Projectile;Liquid=b.lavaWet?3:b.honeyWet?2:1;break;}
            if(Phase==FishingPhase.Waiting)
            {
                if(host.Observation.Count==0){End(true);return;}
                Snapshot();return;
            }
            // Pauses do not erase observed consumption or authorize a new use.
            if(Phase==FishingPhase.Returning || Phase==FishingPhase.Cutting)
            {
                if(!AnyOwned())
                {
                    if(!host.Settings.Value.Auto || host.Settings.Busy && !host.Settings.Requested.Auto){Stop();return;}
                    if(Phase==FishingPhase.Cutting && Tools.Use.Active && Tools.Use.Intent.Kind==ToolKind.FishingCut)Tools.Use.Cancel();
                    Phase=FishingPhase.RecastReady;entered=Tools.Tick;
                }
                else if(Tools.Tick-entered>=600){Stop();host.Report("本次鱼竿操作未能确认结束，已停止自动重抛。");}
            }
        }
        private void Snapshot()
        {ownedCount=host.Observation.Count;Array.Copy(host.Observation.Bobbers,owned,ownedCount);}
        private bool AnyOwned(){for(int i=0;i<ownedCount;i++)if(FishingObservation.Live(owned[i],player))return true;return false;}
        private bool Ready()
        {return Active && Identity() && host.Ready && host.Settings.Value.Auto && !Tools.Fishing.Active && Tools.Admit(player,true) && !PlayerInput.Triggers.Current.MouseLeft && !player.selectedItemState.HasBufferedChange;}
        private bool AnyCurrent(){return FishingBorrow.HasBobber(player);}
        internal ToolIntent Choose(Player p)
        {
            if(!ReferenceEquals(p,player) || !Ready() || p.selectedItem!=rodSlot)return null;
            if(Phase==FishingPhase.RecastReady)
            {
                if(AnyCurrent()){Stop();return null;}
                long token=++operation;return new ToolIntent{Kind=ToolKind.FishingCast,Slot=rodSlot,Target=target,HeldInventory=true,
                    Valid=()=>operation==token && Ready() && (Phase==FishingPhase.RecastReady || Phase==FishingPhase.CastRequested) && !AnyCurrent(),
                    Admitted=()=>{Phase=FishingPhase.CastRequested;used=newCast=false;entered=Tools.Tick;},
                    Used=()=>{used=true;Tools.Fishing.RecordOwnedCast(player,rod,target);},
                    Completed=(started,unknown)=>
                    {
                        if(operation!=token || !Active)return;
                        if(unknown || !started || !used || !newCast){Stop();if(unknown || started)host.Report("本次重抛结果未确认，已停止自动钓鱼。");return;}
                        host.Observation.Invalidate();host.Observation.Read(player);Snapshot();Phase=FishingPhase.Waiting;pullConsumed=false;
                    }};
            }
            if(Phase!=FishingPhase.Waiting)return null;
            bool bite=false;FishingBobber refused=default(FishingBobber);bool reject=false;
            bool sonar=false;for(int i=0;i<player.buffType.Length;i++)if(player.buffType[i]==122 && player.buffTime[i]>0){sonar=true;break;}
            for(int i=0;i<host.Observation.Count;i++)
            {
                var b=host.Observation.Bobbers[i];if(!FishingObservation.Live(b,p) || !b.InLiquid || b.Projectile.ai[1]>=0)continue;
                bite=true;FishKey? key=b.Candidate;
                string name=key.HasValue?(key.Value.Kind==FishKind.Item?Lang.GetItemNameValue(key.Value.Id):Lang.GetNPCNameValue(key.Value.Id)):null;
                bool crate=key.HasValue && key.Value.Kind==FishKind.Item && Terraria.ID.ItemID.Sets.IsFishingCrate[key.Value.Id];
                bool quest=key.HasValue && key.Value.Kind==FishKind.Item && !Main.anglerQuestFinished && Main.anglerQuest>=0 && Main.anglerQuest<Main.anglerQuestItemNetIDs.Length && Main.anglerQuestItemNetIDs[Main.anglerQuest]==key.Value.Id;
                if(!FishFilter.Keep(host.Settings.Value,key,name,sonar,crate,quest)){reject=true;refused=b;}
            }
            if(!bite)return null;
            if(reject)
            {
                if(!host.Settings.Value.Cut || cutKey==refused.Key && cutBite==refused.Bite)return null;
                for(int i=0;i<50;i++)if(i!=rodSlot && player.inventory[i]!=null && player.inventory[i].fishingPole<=0 && !Tools.Items.Ownership.IsProtected(i) && !player.inventoryChestStack[i])
                {
                    int slot=i;long token=++operation;int key=refused.Key;long serial=refused.Bite;
                    return new ToolIntent{Kind=ToolKind.FishingCut,Slot=slot,SelectionOnly=true,Target=target,HeldInventory=true,
                        Valid=()=>operation==token && Ready() && (Phase==FishingPhase.Waiting || Phase==FishingPhase.Cutting) && AnyOwned(),
                        Refresh=()=>Active && Phase==FishingPhase.Cutting && AnyOwned(),
                        Admitted=()=>{cutKey=key;cutBite=serial;Phase=FishingPhase.Cutting;entered=Tools.Tick;pullConsumed=false;},
                        Completed=(started,unknown)=>{if(operation!=token || !Active)return;if(unknown)Stop();else if(Phase==FishingPhase.Cutting){Phase=AnyOwned()?FishingPhase.Waiting:FishingPhase.RecastReady;}}};
                }
                return null;
            }
            long pull=++operation;
            return new ToolIntent{Kind=ToolKind.FishingPull,Slot=rodSlot,Target=target,HeldInventory=true,
                Valid=()=>operation==pull && Ready() && (Phase==FishingPhase.Waiting || Phase==FishingPhase.PullRequested) && AnyOwned(),
                Admitted=()=>{Snapshot();Phase=FishingPhase.PullRequested;used=pullConsumed=false;entered=Tools.Tick;},Used=()=>used=true,
                Completed=(started,unknown)=>{if(operation==pull && Active && (unknown || !pullConsumed)){Stop();if(unknown)host.Report("本次收竿结果未确认，已停止自动钓鱼。");}}};
        }
        // This is called only after the real original pull consumer returned.
        // A use receipt alone cannot create storage qualification.
        internal long BeforeNativePull(Player p,Item item)
        {
            if(ReferenceEquals(p,player) && item!=null && item.fishingPole>0 && !Tools.Use.InNativeUse && PlayerInput.Triggers.Current.MouseLeft && AnyCurrent())
            {End(true);return 0;}
            if(!ReferenceEquals(p,player) || Phase!=FishingPhase.PullRequested || !Tools.Use.Is(ToolKind.FishingPull) || !Tools.Use.ActionValid)return 0;
            for(int i=0;i<ownedCount;i++)pullable[i]=FishingObservation.Live(owned[i],p) && owned[i].Projectile.ai[0]<1;
            return operation;
        }
        internal void NativePulled(Player p,long token)
        {
            // Native TryStartUse pulls first and returns false when bobbers
            // exist, so an ordinary successful pull never calls StartActualUse.
            // Confirm its own original consumer transition, not animation/use.
            if(token==0 || token!=operation || !ReferenceEquals(p,player) || Phase!=FishingPhase.PullRequested || !Tools.Use.Is(ToolKind.FishingPull))return;
            for(int i=0;i<ownedCount;i++)if(pullable[i] && FishingObservation.Live(owned[i],p) && owned[i].Projectile.ai[0]>=1)
            {pullConsumed=true;Phase=FishingPhase.Returning;entered=Tools.Tick;return;}
        }
        internal bool OwnsProduct(Player p,Projectile b)
        {
            if(!ReferenceEquals(p,player) || !Identity() || !pullConsumed || Phase!=FishingPhase.Returning)return false;
            for(int i=0;i<ownedCount;i++)if(pullable[i] && ReferenceEquals(owned[i].Projectile,b) && owned[i].Key==(int)b.key)return true;return false;
        }
        internal void Stop()
        {End(false);}
        internal void ManualSelection(){End(true);}
        private void Prompt(bool start,bool naturalEnd)
        {
            // Preserve the four Legacy session-edge messages. "鲨猪啦！" is
            // a playful end notice, never evidence that a boss was spawned.
            // Our owned cuts, rethrows and tool loans keep the same token.
            var feedback=Tools.Feedback;if(feedback==null)return;long token=Token;
            string text=start?(truffle?"开始鲨猪":"开始钓鱼"):(truffle && naturalEnd?"鲨猪啦！":"停止钓鱼");
            feedback.Show("fishing.session",text,start,()=>Token==token && Active==start,feedback.Capture());
        }
        private void End(bool natural)
        {
            bool wasActive=Active;
            Phase=FishingPhase.Idle;manualCast=false;InLiquid=false;Liquid=0;borrow=0;pullConsumed=false;
            if(wasActive)Prompt(false,natural);
            if(Tools.Use.Active && Tools.Use.Intent.Kind>=ToolKind.FishingPull)Tools.Use.Cancel();
            Array.Clear(owned,0,ownedCount);ownedCount=0;player=null;rod=null;
        }
        internal void Reset(){Stop();lastPlayer=null;lastRod=null;cutBite=-1;operation++;}
    }
}
