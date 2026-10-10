using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Guidance;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // Optional future aim providers return a stamped target, not a direction
    // lease. The consumer still validates every identity at actual ChangeDir.
    internal sealed class FacingTarget
    {
        internal readonly NPC Npc;
        internal readonly int Slot,Type,NetId,Generation;
        internal readonly long Session;
        internal readonly uint Step;
        private readonly bool shared;
        internal FacingTarget(NPC npc,long session):this(npc,session,false){}
        internal FacingTarget(NPC npc,long session,bool shared)
        {Npc=npc;Slot=npc.whoAmI;Type=npc.type;NetId=npc.netID;Generation=npc.generation;Session=session;Step=Main.GameUpdateCount;this.shared=shared;}
        internal bool Valid(long session)
        {
            return Session==session && Slot>=0 && Slot<Main.maxNPCs && ReferenceEquals(Main.npc[Slot],Npc) && Npc.whoAmI==Slot &&
                Npc.type==Type && Npc.netID==NetId && Npc.generation==Generation && (shared?CombatSelection.Receives(Npc,true):CombatFacing.Targetable(Npc));
        }
    }
    internal sealed class CombatFacing
    {
        private readonly HostCombat host;
        private readonly FacingSelector selector=new FacingSelector();
        private FacingTarget target;
        private Item weapon;
        private int weaponType,direction,slot;
        private uint nextDecision,lastSearch;
        private bool searched,hasDecision,fromProvider;
        internal Func<Player,FacingTarget> TargetProvider {get;set;}
        internal Func<bool> SharedTargetRequired {get;set;}
        internal bool LastChangeObserved {get;private set;}
#if DEBUG
        internal int Searches {get;private set;}
        internal int CandidateReads {get;private set;}
#endif
        internal CombatFacing(HostCombat host){this.host=host;}
        internal static bool Targetable(NPC n)
        {return n!=null && n.active && n.type!=NPCID.TargetDummy && !n.hide && n.life>0 && !n.townNPC && !n.friendly && n.chaseable && n.lifeMax>5 && !n.dontTakeDamage && !n.immortal;}
        private static bool Weapon(Player p)
        {
            if(p.selectedItem<0 || p.selectedItem>=10)return false;Item i=p.HeldItem;
            if(i==null || i.IsAir || i.createTile>=0 || i.createWall>=0 || i.pick>0 || i.axe>0 || i.hammer>0 || i.fishingPole>0 || i.ammo>0 || i.sentry || i.summon && i.buffType>0)return false;
            if(i.damage>0)return true;
            if(i.type!=ItemID.CoinGun)return false;
            for(int n=0;n<58;n++)if(p.inventory[n]!=null && !p.inventory[n].IsAir && p.inventory[n].IsACoin)return true;
            return false;
        }
        internal void Apply(Player p)
        {
            LastChangeObserved=false;
            if(!host.IsEnabled(5)){Reset();return;}
            if(!host.Left && !host.Right && p.itemAnimation<=0 && p.itemTime<=0 || !Weapon(p) || !host.Admitted(p) ||
                host.Tools.Items.Ownership.HasUse && !host.Use.Active){Reset();return;}
            // A legal managed yoyo attack keeps its trusted attack intent.
            // Movement remains the direction owner outside that admitted use.
            if(p.controlLeft!=p.controlRight && !host.Use.ManagedAttackIntent(p))
            {Reset();Change(p,p.controlLeft?-1:1);return;}
            uint now=Main.GameUpdateCount;
            bool identity=ReferenceEquals(weapon,p.HeldItem) && weaponType==p.HeldItem.type && slot==p.selectedItem;
            if(!identity || target!=null && (!target.Valid(host.Runtime.Generation) || !fromProvider && !InRange(p,target.Npc)))ClearDecision();
            // Revocation is checked even inside the automatic cooldown. Calling
            // this optional provider reads a prepared result, never starts aim.
            FacingTarget provided=null;
            try{provided=TargetProvider?.Invoke(p);}catch{ /* An optional failed result grants no direction authority. */ }
            bool validProvider=provided!=null && provided.Valid(host.Runtime.Generation) && unchecked(now-provided.Step)<=1;
            // When aim owns a final selection, do not silently substitute the
            // independent Facing selector's different NPC or old eligibility.
            if(SharedTargetRequired?.Invoke()==true && !validProvider){ClearDecision();return;}
            if(!fromProvider && validProvider)ClearDecision();
            if(fromProvider && (!validProvider || !SameTarget(target,provided)))ClearDecision();
            if(!hasDecision || unchecked((int)(now-nextDecision))>=0)
            {
                if(searched && lastSearch==now)return;searched=true;lastSearch=now;
                target=validProvider?provided:Find(p);fromProvider=validProvider;
                Vector2 point=target!=null?target.Npc.Center:Main.MouseWorld;
                direction=float.IsNaN(point.X) || float.IsInfinity(point.X) || float.IsNaN(point.Y) || float.IsInfinity(point.Y)?0:point.X-p.Center.X>2?1:point.X-p.Center.X< -2?-1:0;
                weapon=p.HeldItem;weaponType=weapon.type;slot=p.selectedItem;hasDecision=true;nextDecision=unchecked(now+10);
            }
            if(direction!=0 && host.IsEnabled(5) && (!fromProvider || validProvider) && (target==null || target.Valid(host.Runtime.Generation)))Change(p,direction);
        }
        private FacingTarget Find(Player p)
        {
#if DEBUG
            Searches++;
#endif
            host.Tools.Npcs.BeginActions(host.Tools.Input.Frame);selector.Begin(p.Center.X,p.Center.Y);
            for(int i=0;i<host.Tools.Npcs.Count;i++)
            {
#if DEBUG
                CandidateReads++;
#endif
                GuidanceNpc facts;if(!host.Tools.Npcs.TryRead(i,NpcDemand.Basic,out facts) || !facts.Active)continue;
                NPC n=facts.Identity as NPC;
                if(!Targetable(n))continue;
                selector.Consider(new FacingCandidate{Slot=i,LifeMax=n.lifeMax,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height});
            }
            int chosen=selector.Finish();return chosen<0?null:new FacingTarget(Main.npc[chosen],host.Runtime.Generation);
        }
        private static bool InRange(Player p,NPC n)
        {var v=Vector2.Clamp(p.Center,n.position,n.position+new Vector2(n.width,n.height));return Vector2.DistanceSquared(v,p.Center)<=FacingSelector.Range*FacingSelector.Range;}
        private static bool SameTarget(FacingTarget a,FacingTarget b)
        {return a!=null && b!=null && ReferenceEquals(a.Npc,b.Npc) && a.Type==b.Type && a.Generation==b.Generation && a.Session==b.Session;}
        private void Change(Player p,int value)
        {if(p.direction!=value)p.ChangeDir(value);LastChangeObserved=p.direction==value;}
        private void ClearDecision(){target=null;weapon=null;direction=0;hasDecision=fromProvider=false;}
        internal void Reset(){ClearDecision();searched=false;LastChangeObserved=false;}
    }
}
