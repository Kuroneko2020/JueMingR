using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // Owns a held-input intention and its real source, never an inventory copy.
    // Decisions advance on GameUpdateCount; repeated ItemCheck/AI entry in the
    // same simulation step reapplies the decision without advancing cadence.
    internal sealed class CombatUse
    {
        private readonly HostCombat host;
        private Player player;
        private Item source;
        private int slot,type,prefix,kind=-1,pendingSlot=-1;
        private long session,selection,token;
        private uint decisionTick,cycleTick,pressTick;
        private bool decided,press,used,released,releaseObserved,blockedGesture,waitingInterval,yielding;
        private int inCheck;
        private readonly List<CombatInputScope> scopes=new List<CombatInputScope>();
        private CombatInputScope syncScope;
        private Projectile primary,paired;
        private int primaryKey,pairedKey,primaryType,pairedType;
        private bool flailReceipt;
        private uint stagnantTick;
        private int stagnant;
        private Vector2 lastPosition;
        internal bool Selecting {get;private set;}
#if DEBUG
        internal int ProjectileDiscoveryReads {get;private set;}
        internal int SwitchCandidateReads {get;private set;}
#endif
        internal bool Active {get{return player!=null;}}
        internal bool FeatureEnabled {get{return kind>=0 && host.IsEnabled(kind);}}
        internal bool DeferSync
        {
            get
            {
                Player p=host.Player;if(host.Left || !host.Right || p==null || p.dead || p.ghost)return false;
                if(host.Handoff.RightSync)return true;
                if(!host.Admitted(p) || p.selectedItem<0 || p.selectedItem>=10)return false;
                Item item=p.inventory[p.selectedItem];
                return host.IsEnabled(1) && WeaponCatalog.Flail(item) || host.IsEnabled(2) && WeaponCatalog.Switchable(item);
            }
        }
        internal bool CanYield {get{return Active && Identity() && slot<10 && pendingSlot<0 && used && WeaponCatalog.Ready(player) && player.selectedItemState.CanChangeSelectedItemImmediately && (kind!=2 || releaseObserved) && CanLeaveProjectile();}}
        internal bool CanOffer {get{return CanYield || Active && Identity() && slot<10 && used && (kind==0 || kind==3) && player.itemAnimation<=2 && player.itemTime<=2 && player.reuseDelay<=1;}}
        internal void RequestYield(){yielding=true;decided=false;}
        internal void CancelYield(){yielding=false;decided=false;}
        internal long Operation {get{return token;}}
        internal CombatUse(HostCombat host){this.host=host;}
        private bool Identity()
        {
            if(player==null || session!=host.Runtime.Generation || !ReferenceEquals(player,host.Player) || selection!=host.Tools.SelectionIntent)return false;
            if(pendingSlot>=0)return true;
            if(player.selectedItem!=slot)return false;
            Item item=player.inventory[slot];
            // Vanilla intentionally clones mouseItem <-> inventory[58]. Cursor
            // identity is recaptured at each native call, not inferred across
            // arbitrary user inventory moves from equal type/stack values.
            return item!=null && item.type==type && item.prefix==prefix && item.stack>0 && (slot==58 || ReferenceEquals(item,source));
        }
        private int Qualify(Player p)
        {
            int selected=p.selectedItem;if(selected<0 || selected>=10 && selected!=58)return -1;
            Item item=p.inventory[selected];if(item==null || item.IsAir)return -1;
            if(host.Left)
            {
                if(selected<10 && host.IsEnabled(3) && item.type==2269)return 3;
                if(selected<10 && host.IsEnabled(4) && p.magicString && WeaponCatalog.Yoyo(item))return 4;
                if(host.IsEnabled(0) && WeaponCatalog.AutoClick(item))return 0;
                return -1;
            }
            if(!host.Right || selected>=10 || !WeaponCatalog.RightAvailable(p,item))return -1;
            if(host.IsEnabled(1) && WeaponCatalog.Flail(item))return 1;
            if(host.IsEnabled(2) && WeaponCatalog.Switchable(item) && NextSlot(p,selected)>=0)return 2;
            return -1;
        }
        private int NextSlot(Player p,int current)
        {
            for(int n=1;n<10;n++)
            {
#if DEBUG
                SwitchCandidateReads++;
#endif
                int next=(current+n)%10;
                if(WeaponCatalog.Switchable(p.inventory[next]) && !host.Tools.Items.Ownership.IsProtected(next) && !p.inventoryChestStack[next] &&
                    !host.Tools.Items.World.ManualMaterials.Contains(p.inventory[next]))return next;
            }
            return -1;
        }
        internal void BeforeSync(Player p)
        {
            // Right-click interactions are decided later in Player.Update.
            // Keep the physical tile input intact until vanilla has had that
            // opportunity; even a confirmed quick-switch must wait for it.
            if(!host.Left && host.Right){FinishFrame();host.Handoff.BeforeSync(p);return;}
            Sync(p);
        }
        internal void AfterInteractions(Player p)
        {if(!host.Left && host.Right)Sync(p);}
        internal void Sync(Player p)
        {
            FinishFrame();
            if(!host.Left && !host.Right)blockedGesture=false;
            host.Handoff.BeforeSync(p);
            if(host.Handoff.Waiting && !host.Handoff.Releasing)return;
            if(!Active && (!host.Settings.CanRun || (host.Settings.Value.EnabledMask & 31)==0 || !host.Left && !host.Right))return;
            if(blockedGesture || !host.Admitted(p,p.selectedItem==58)){Stop();return;}
            if(!host.Left && host.Right && p.tileInteractionHappened){Stop();return;}
            if(Active && !Identity()){Stop();return;}
            if(pendingSlot>=0)
            {
                if(p.selectedItem!=pendingSlot || p.selectedItemState.HasBufferedChange)return;
                slot=pendingSlot;pendingSlot=-1;source=p.inventory[slot];type=source.type;prefix=source.prefix;
                primary=paired=null;used=released=releaseObserved=false;decided=false;cycleTick=Main.GameUpdateCount;waitingInterval=true;
            }
            int nextKind=Qualify(p);
            if(nextKind<0 || Active && nextKind!=kind){Stop();return;}
            if(!Active)
            {
                bool cursor=p.selectedItem==58 && Main.mouseItem!=null && !Main.mouseItem.IsAir && Main.mouseItem.type==p.inventory[58].type && Main.mouseItem.prefix==p.inventory[58].prefix;
                if(p.selectedItemState.HasActiveOverride && !cursor || p.selectedItemState.HasBufferedChange || host.Tools.Use.Active)return;
                long next=host.Tools.Items.Ownership.NewUseToken();
                if(!(p.selectedItem==58?host.Tools.Items.Ownership.TryBeginCursorUse(host.Runtime.Generation,next):host.Tools.Items.Ownership.TryBeginUse(host.Runtime.Generation,p.selectedItem,next)))return;
                player=p;slot=p.selectedItem;source=p.inventory[slot];type=source.type;prefix=source.prefix;token=next;
                session=host.Runtime.Generation;selection=host.Tools.SelectionIntent;kind=nextKind;cycleTick=Main.GameUpdateCount;
                Discover();
            }
            if(kind==2 && released && releaseObserved && WeaponCatalog.Ready(p) && CanLeaveProjectile())
            {Switch(p);return;}
            Decide();
            syncScope=new CombatInputScope(p,press,kind==1 || kind==2,false);syncScope.Apply();
        }
        internal CombatInputScope Begin(Player p)
        {
            // Select only buffers the next real slot. Its protection already
            // belongs to us, so a fresh candidate search here would reject our
            // own pending destination and erase the confirmation/interval.
            if(ReferenceEquals(p,player) && pendingSlot>=0)return null;
            if(!ReferenceEquals(p,player) || !Identity() || !host.Admitted(p,slot==58) || Qualify(p)!=kind){if(ReferenceEquals(p,player))Stop();return null;}
            if(slot==58)source=p.inventory[58];
            Decide();
            var scope=new CombatInputScope(p,press,kind==1 || kind==2,true);scopes.Add(scope);inCheck++;scope.Apply();
            if(kind==2 && released && WeaponCatalog.Release(source)==ReleaseMechanism.ItemRelease)BorrowAim(scope,CombatAimStage.ItemRelease,null);
            return scope;
        }
        internal void End(CombatInputScope scope,Exception error)
        {
            if(scope==null)return;scope.End(error==null);if(!scopes.Remove(scope))return;if(inCheck>0)inCheck--;
            if(error!=null){blockedGesture=true;Stop();}
        }
        internal void AfterReuse(Player p)
        {
            if(!ReferenceEquals(p,player) || inCheck==0 || !Identity() || !press)return;
            // Let the complete native AutoReuseLogic/TryAllowingItemReuse run
            // first. Only fill a still-missing release at a real ready boundary.
            if(WeaponCatalog.Ready(p) && !p.releaseUseItem && scopes.Count>0)scopes[scopes.Count-1].FillRelease();
        }
        private void Decide()
        {
            uint now=Main.GameUpdateCount;if(decided && decisionTick==now)return;
            decisionTick=now;decided=true;press=false;
            if(yielding)return;
            switch(kind)
            {
                case 0:press=true;break;
                case 3:
                    press=WeaponCatalog.Ready(player);
                    if(!press && player.itemAnimation<=2 && player.itemTime<=2 && player.reuseDelay<=0 && !player.delayUseItem)
                    {if(pressTick==now)press=true;else pressTick=unchecked(now+1);}
                    break;
                case 4:
                    // A release is retained for the entire simulation step so
                    // the native yoyo AI can create its magic-string split.
                    press=Valid(primary,primaryKey) && primary.ai[0]>=0 ? unchecked(now-cycleTick)<2 : WeaponCatalog.Ready(player);
                    if(press && !Valid(primary,primaryKey))cycleTick=now;
                    break;
                case 1:DecideFlail(now);break;
                case 2:
                    // The slider is extra time AFTER the real selection was
                    // observed. It must never postpone a valid release window
                    // or disappear for the two projectile-charge families.
                    if(waitingInterval)
                    {
                        if(unchecked(now-cycleTick)<(uint)host.Settings.Value.SwitchInterval || !WeaponCatalog.Ready(player))break;
                        waitingInterval=false;
                    }
                    if(released){press=false;if(!releaseObserved && WeaponCatalog.Ready(player)){released=false;used=false;}break;}
                    press=true;
                    ReleaseMechanism mechanism=WeaponCatalog.Release(source);
                    // Throw after a real start, leaving the native remaining
                    // animation as itemTime while the projectile travels. A
                    // tail-only release makes the next press recall it almost
                    // immediately. Entry animation 1 is too late: native
                    // AutoReuseLogic can consume it before the shoot check.
                    if(mechanism==ReleaseMechanism.ItemRelease && used && player.itemAnimation>1 && player.itemTime==0 ||
                        mechanism==ReleaseMechanism.Flint && Valid(primary,primaryKey) && primary.ai[1]>=30 ||
                        mechanism==ReleaseMechanism.Glacier && Valid(primary,primaryKey) && (primary.ai[0]>=60 || primary.ai[1]==1))
                    {press=false;released=true;if(mechanism==ReleaseMechanism.Glacier && primary.ai[1]==1)releaseObserved=true;}
                    break;
            }
        }
        private void DecideFlail(uint now)
        {
            if(!Valid(primary,primaryKey)){primary=null;press=WeaponCatalog.Ready(player) && unchecked(now-pressTick)>=5;return;}
            int phase=(int)primary.ai[0];
            if(phase==0){press=false;return;}
            if(phase==3 || phase==6){press=false;return;}
            if(phase==4 || phase==5){press=false;return;}
            if(stagnantTick!=now)
            {stagnantTick=now;stagnant=Vector2.DistanceSquared(primary.position,lastPosition)<0.01f?stagnant+1:0;lastPosition=primary.position;}
            // Damage/movement run after AI. Their real receipts are consumed on
            // the next simulation decision; phase 5 keeps vanilla's wall pause.
            press=(phase==1 || phase==2) && (flailReceipt || stagnant>=18);
            flailReceipt=false;
            if(press)pressTick=now;
        }
        internal void Started(Player p,Item item)
        {if(ReferenceEquals(p,player) && inCheck>0 && ReferenceEquals(item,source)){foreach(var scope in scopes)scope.ReleaseConsumed();used=true;if(kind==4)cycleTick=Main.GameUpdateCount;}}
        internal void Created(Player p,Projectile shot)
        {
            if(!ReferenceEquals(p,player) || !Identity() || shot.owner!=p.whoAmI || !shot.active)return;
            if(inCheck==0)return;
            if(shot.type==source.shoot)
            {primary=shot;primaryKey=(int)shot.key;primaryType=shot.type;flailReceipt=false;if(kind==2 && released)releaseObserved=true;}
            else if(kind==1 && (source.shoot==26 && shot.type==35 || source.shoot==35 && shot.type==26))
            {paired=shot;pairedKey=(int)shot.key;pairedType=shot.type;}
        }
        private void Discover()
        {
            if(kind==0 || kind==3 || source.shoot<=0 || player.ownedProjectileCounts[source.shoot]<=0)return;
            for(int i=0;i<Main.maxProjectiles;i++)
            {
#if DEBUG
                ProjectileDiscoveryReads++;
#endif
                var p=Main.projectile[i];
                if(p!=null && p.active && p.owner==player.whoAmI && p.type==source.shoot && (kind!=4 || p.ai[0]>=0))
                {primary=p;primaryKey=(int)p.key;primaryType=p.type;break;}
            }
        }
        private bool Valid(Projectile p,int key)
        {
            int expected=ReferenceEquals(p,primary)?primaryType:pairedType;
            return p!=null && player!=null && p.active && (p.type==expected || expected==948 && p.type==947) && (int)p.key==key && p.owner==player.whoAmI &&
                p.whoAmI>=0 && p.whoAmI<Main.maxProjectiles && ReferenceEquals(Main.projectile[p.whoAmI],p);
        }
        internal bool TracksFlail(Projectile shot)
        {return kind==1 && Identity() && (ReferenceEquals(shot,primary) && Valid(primary,primaryKey) || ReferenceEquals(shot,paired) && Valid(paired,pairedKey));}
        internal void FlailReceipt(Projectile shot){if(TracksFlail(shot))flailReceipt=true;}
        internal void BeginProjectile(Projectile shot,out CombatInputScope scope)
        {
            scope=null;
            if(!Active || !(ReferenceEquals(shot,primary) && Valid(primary,primaryKey) || ReferenceEquals(shot,paired) && Valid(paired,pairedKey)) ||
                !Identity() || !FeatureEnabled || !host.Admitted(player,slot==58))return;
            // Give the shared finalizer cleanup responsibility before Apply or
            // aim borrowing can fail; a return-value handoff would lose it.
            scope=new CombatInputScope(player,press,kind==1 || kind==2,false);scopes.Add(scope);scope.Apply();
            // These are the .8 stages that actually read the point. Flint and
            // Glacier retain native direction/velocity after charge; no aim
            // override is lent to a returning projectile or a future Kill.
            if(kind==1 && shot.ai[0]==0 && !player.channel)BorrowAim(scope,CombatAimStage.FlailRelease,shot);
            else if(kind==2 && type==5462 && shot.ai[0]==0)BorrowAim(scope,CombatAimStage.FlintCharge,shot);
            else if(kind==2 && type==6153 && player.channel && shot.ai[1]==0 && (int)shot.ai[0]%3==0)BorrowAim(scope,CombatAimStage.GlacierCharge,shot);
        }
        private void BorrowAim(CombatInputScope scope,CombatAimStage stage,Projectile projectile)
        {
            if(host.Aim.Provider==null)return;
            var request=new CombatAimRequest(player,source,projectile,session,selection,token,slot,stage);Vector2 point;
            if(host.Aim.TryPoint(request,out point) && Identity() && host.Admitted(player) && request.Operation==token && host.IsEnabled(kind) &&
                (projectile==null || Valid(projectile,request.ProjectileKey)))scope.BorrowAim(point);
        }
        internal void EndProjectile(Projectile shot,CombatInputScope scope,Exception error)
        {
            if(scope==null)return;scope.End();if(!scopes.Remove(scope))return;
            if(error!=null){blockedGesture=true;Stop();return;}
            if(kind==2 && released && ReferenceEquals(shot,primary) &&
                (type==5462 && shot.ai[0]==1 || type==6153 && shot.ai[1]==1))releaseObserved=true;
        }
        private bool CanLeaveProjectile(){return type!=5462 || !Valid(primary,primaryKey);}
        private void Switch(Player p)
        {
            if(!p.selectedItemState.CanChangeSelectedItemImmediately || p.selectedItemState.HasBufferedChange || p.selectedItemState.HasActiveOverride)return;
            int next=NextSlot(p,slot);if(next<0){Stop();return;}
            host.Tools.Items.Ownership.EndUse(session,token);
            token=host.Tools.Items.Ownership.NewUseToken();
            if(!host.Tools.Items.Ownership.TryBeginUse(session,next,token)){Stop();return;}
            pendingSlot=next;
            try{Selecting=true;p.selectedItemState.Select(next);}finally{Selecting=false;}
        }
        internal bool Owns(Item[] items,int index)
        {return inCheck>0 && Active && Identity() && index==slot && ReferenceEquals(items,player.inventory) && ReferenceEquals(items[index],source);}
        internal void FinishFrame()
        {for(int i=scopes.Count-1;i>=0;i--)scopes[i].End();scopes.Clear();inCheck=0;syncScope?.End();syncScope=null;}
        internal void Stop()
        {
            FinishFrame();if(player!=null)host.Tools.Items.Ownership.EndUse(session,token);
            player=null;source=null;primary=paired=null;token=0;kind=-1;pendingSlot=-1;decided=press=used=released=releaseObserved=waitingInterval=flailReceipt=yielding=false;stagnant=0;
        }
        internal void ManualSelection(){if(Active || host.Handoff.Waiting)blockedGesture=true;Stop();}
    }
}
