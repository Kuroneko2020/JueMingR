using System;
using JueMingR.Features.Movement;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Movement
{
    internal struct SmartFallCobwebPlan
    {
        internal bool Found;
        internal int X,Y,TouchTick;
        internal float PlayerX,PlayerY;
    }

    // One search rides the existing finite body corridor. It never calls a
    // world-mutating placement API and does not replay a Player.Update.
    internal sealed class SmartFallCobwebSearch
    {
        private Player player;
        private Item material;
        private int earliest,count;
        private readonly SmartFallCobwebPlan[] plans=new SmartFallCobwebPlan[8];
        internal SmartFallCobwebPlan Plan;
        internal void Begin(Player p,Item item,int acquire)
        {player=p;material=item;earliest=acquire+1;count=0;Plan=default(SmartFallCobwebPlan);}
        internal void Sample(Vector2 position,int tick,ref long reads,SmartFallContactBand band)
        {
            if(material==null || tick<earliest)return;
            float y=position.Y-player.gravDir*.01f;
            int left=(int)Math.Floor(position.X/16),right=(int)Math.Floor((position.X+player.width-.001f)/16);
            int top=(int)Math.Floor(y/16),bottom=(int)Math.Floor((y+player.height-.001f)/16);
            // A player has only a few overlapping cells at one native sample.
            // Read support into the SAME bounded dependency set as collision.
            for(int ty=bottom;ty>=top;ty--)for(int tx=left;tx<=right;tx++)
                if(CanPlace(player,material,tx,ty,ref reads,band,tick,position))
                {
                    // Keep the first legal post-preparation opportunity as
                    // well as seven recent alternatives. Overwriting all8
                    // with late points makes Touch-Acquire stay constant while
                    // a continuous owner starts its next action: Yield is
                    // never requested despite a reachable earlier boundary.
                    // Finish still rejects any point whose remaining fall is
                    // unsafe; neither the corridor nor this8-point cap grows.
                    int slot=count==0?0:1+(count-1)%(plans.Length-1);
                    plans[slot]=new SmartFallCobwebPlan{Found=true,X=tx,Y=ty,TouchTick=tick,PlayerX=position.X,PlayerY=position.Y};count++;return;
                }
        }
        internal SmartFallCobwebPlan Finish(SmartFallContact landing)
        {
            Plan=default(SmartFallCobwebPlan);
            int allowance=player.stoned?2:25+player.extraFall+(player.mount.Active?player.mount.ExtraFall:0);
            for(int i=0;i<Math.Min(count,plans.Length);i++)
            {
                var candidate=plans[i];
                if(!landing.Found || candidate.TouchTick>=landing.Ticks || ((int)(landing.Y/16)-(int)(candidate.PlayerY/16))*player.gravDir>allowance)continue;
                if(!Plan.Found || candidate.TouchTick<Plan.TouchTick)Plan=candidate;
            }
            return Plan;
        }
        internal static bool CanPlace(Player p,Item item,int x,int y,ref long reads,SmartFallContactBand band=null,int tick=0,Vector2? futureBody=null)
        {
            if(item==null || item.IsAir || !item.consumable || item.createTile!=51 || p.noItems || p.CCed ||
                !InReach(p,item,x,y,futureBody))return false;
            Tile tile=SmartFallGeometry.Read(x,y,ref reads);band?.Observe(x,y,tile,tick);
            if(tile==null || tile.active() || tile.liquid>0 && tile.lava())return false;
            bool support=false;
            for(int i=0;i<4;i++)
            {
                int tx=x+(i==0?-1:i==1?1:0),ty=y+(i==2?-1:i==3?1:0);
                Tile near=SmartFallGeometry.Read(tx,ty,ref reads);band?.Observe(tx,ty,near,tick);
                if(near==null)return false;
                if(near.active() || near.wall>0)support=true;
            }
            // Player.PlaceThing_Tiles_BlockPlacementForAssortedThings (.8):
            // 51 uses active OR wall in the four orthogonal neighbours. A
            // target wall alone is not the cobweb branch's support condition.
            return support;
        }
        private static bool InReach(Player p,Item item,int x,int y,Vector2? futureBody)
        {
            if(!futureBody.HasValue)return p.IsInTileInteractionRange(x,y,TileReachCheckSettings.Simple,item.tileBoost+p.blockRange);
            // This candidate will be used AFTER the current owner's natural
            // timers. Native ItemCheck then shares the next Sticky sample's
            // body position. Apply .8 GetTileRegion's exact bounds at that
            // already predicted body; current-position reach would reject all
            // long preparations and never let a continuous owner yield.
            // No Player field is moved. Borrow/use still call native CURRENT
            // reach again, so prediction cannot authorize a remote placement.
            TileReachCheckSettings.Simple.GetRanges(out int rx,out int ry);rx+=item.tileBoost+p.blockRange;ry+=item.tileBoost+p.blockRange;
            Vector2 position=futureBody.Value;
            return Collision.InTileBounds(x,y,(int)(position.X/16f)-rx,(int)(position.Y/16f)-ry,
                (int)Math.Ceiling((position.X+p.width)/16f)-1+rx,(int)Math.Ceiling((position.Y+p.height)/16f)-1+ry);
        }
    }

    internal sealed partial class SmartFallHost
    {
        private readonly SmartFallCobwebSearch webSearch=new SmartFallCobwebSearch();
        private Tools.HostTools rescueTools;
        private SmartFallCobwebPlan webPlan;
        private bool webRequest,webSelected,webIssued,webObserving,webStickySample;
        private Item webSource;
        private long webOwner,webSelection,webLoan,webSampleTick;
        private uint webDeadline,webObserveUntil;
        private int webAcquire,webPlacedX,webPlacedY,webSlot=-1;
        private float webGravity;
        private SmartFallCobwebAimScope webAim;
        internal long CobwebRequests,CobwebPlacements,CobwebDebits,CobwebTouches;
        internal bool CobwebPlaced {get;private set;}
        internal bool CobwebProtected {get;private set;}
        internal bool CobwebNativeReady {get;private set;}
        internal void EnableCobwebObservation(bool ready)
        {
            CobwebNativeReady=ready;
            if(!ready){CancelWebRequest();if(webSelected && retained!=null)borrow.Restore(retained,retainedGeneration);}
        }
        internal void AttachTools(Tools.HostTools tools)
        {rescueTools=tools;if(tools!=null)tools.FallRescue=this;borrow.HeldRescuePermit=()=>tools?.Combat?.Handoff.AllowsSmartFall??false;}
        private static bool BeforeDeadline(uint deadline){return unchecked((int)(deadline-Main.GameUpdateCount))>=0;}
        private bool WebRequestIdentity(Player p)
        {
            return webRequest && ReferenceEquals(p,retained) && generation==retainedGeneration &&
                rescueTools!=null && rescueTools.Runtime.Generation==generation && webSelection==rescueTools.SelectionIntent &&
                session.Value.SmartFallProtection.Enabled && session.Value.SmartFallProtection.Cobweb && BeforeDeadline(webDeadline) &&
                webSlot>=0 && webSlot<50 && ReferenceEquals(p.inventory[webSlot],webSource) && webSource.stack>0 && webSource.createTile==51 &&
                !p.dead && !p.CCed && p.gravDir==webGravity && (permitted && scope && session.CanAct(p)) &&
                (!PlayerInput.Triggers.Current.MouseLeft && !PlayerInput.Triggers.Current.MouseRight || (rescueTools.Combat?.Handoff.AllowsSmartFall??false));
        }
        // Ready callbacks read this exact already prepared request. They never
        // scan inventory/terrain and may not end another owner's token.
        internal bool CobwebYieldRequested(Player p,long owner)
        {return WebRequestIdentity(p) && webOwner==owner && owner>0;}
        internal bool CobwebOpportunityReserved(Player p)
        {return WebRequestIdentity(p) || webSelected && ReferenceEquals(p,retained) && borrow.IsCobwebSelection(p);}
        internal bool CobwebSelection(Player p)
        {return webSelected && ReferenceEquals(p,retained) && borrow.IsCobwebSelection(p);}
        internal long CobwebOperation {get{return webSelected?borrow.Operation:0;}}
        private bool WebOwnedSource(int slot,Item item)
        {
            return rescueTools!=null && rescueTools.Use.PlanningSource(slot,item) &&
                items.Ownership.OnlyUseProtects(generation,rescueTools.Use.Operation,slot);
        }
        private Item WebOpportunity(Player p)
        {return borrow.CobwebOpportunity(p,WebOwnedSource);}
        private int WebAcquireDelay(Player p)
        {
            int remaining=Math.Max(p.itemTime,Math.Max(p.itemAnimation,p.reuseDelay));
            // ToolUse retires in SelectedItemState.Update, AFTER this Capture.
            // Even when its natural timers reach zero, this Capture still
            // sees the old use token; only the following Capture may borrow.
            // Include that phase before choosing a touch/deadline. Otherwise
            // the first candidate expires exactly as the legal loan opens.
            int toolRetirement=rescueTools?.Use.Active==true?1:0;
            // Selection precedes motion/ItemCheck; a placed web first reaches
            // Sticky in the NEXT Update. Override/neutral preparation remains
            // separate, and no native timer or release field is shortened.
            return Math.Max(0,remaining)+toolRetirement+(p.selectedItemState.HasActiveOverride?1:0)+(p.releaseUseItem?0:1);
        }
        private bool WebProtectsRemainder(Player p,float sampleY,SmartFallContact landing)
        {
            int allowance=p.stoned?2:25+p.extraFall+(p.mount.Active?p.mount.ExtraFall:0);
            return landing.Found && ((int)(landing.Y/16)-(int)(sampleY/16))*p.gravDir<=allowance;
        }
        private void PrepareWebSearch(Player p)
        {
            contactBand.ObservePlacement(session.Value.SmartFallProtection.Cobweb);
            webAcquire=WebAcquireDelay(p);
            webSearch.Begin(p,CobwebNativeReady && session.Value.SmartFallProtection.Cobweb?sources[12]:null,webAcquire);
        }
        private bool NaturalWebOpportunity(Player p)
        {return CobwebNativeReady && contact.WebFound && contact.WebTick>=0 && !p.mount.Active && !p.shimmering && WebProtectsRemainder(p,contact.WebPlayerY,contact);}
        private bool WebCandidate(Player p,out int delay)
        {
            delay=webAcquire;
            if(!CobwebNativeReady || webSelected || webObserving || sources[12]==null || !webPlan.Found || p.mount.Active || p.shimmering ||
                webPlan.TouchTick<=webAcquire || !WebProtectsRemainder(p,webPlan.PlayerY,contact))return false;
            return SmartFallCobwebSearch.CanPlace(p,sources[12],webPlan.X,webPlan.Y,ref TileReads,null,0,new Vector2(webPlan.PlayerX,webPlan.PlayerY));
        }
        private bool WebDue(){return webPlan.TouchTick<=webAcquire+2;}
        private void StartWeb(Player p)
        {
            if(!webPlan.Found || rescueTools==null || !session.CanAct(p))return;
            Item source=sources[12];
            long owner=rescueTools.Use.Active?rescueTools.Use.Operation:rescueTools.Combat?.Use.Operation??0;
            if(!webRequest)
            {
                webRequest=true;webSource=source;webOwner=owner;webSelection=rescueTools.SelectionIntent;webGravity=p.gravDir;
                webSlot=Array.IndexOf(p.inventory,source);
                webDeadline=Main.GameUpdateCount+(uint)Math.Max(0,webPlan.TouchTick-1);Count(ref CobwebRequests);
            }
            else if(!ReferenceEquals(webSource,source) || webOwner!=0 && owner!=0 && webOwner!=owner)
            {CancelWebRequest();return;}
            if(!WebRequestIdentity(p))
            {
                // A combat-owned physical hold gets its exact permit only after
                // Handoff offers this request at the next native selection seam.
                bool ownCombat=owner!=0 && rescueTools.Combat?.Use.Operation==owner;
                if(!ownCombat)CancelWebRequest();return;
            }
            if(owner!=0 || items.Ownership.HasUse)return;
            if(!SmartFallCobwebSearch.CanPlace(p,source,webPlan.X,webPlan.Y,ref TileReads) || !borrow.TryBorrow(p,generation,source,SmartFallFamily.Cobweb))return;
            webSelected=true;webIssued=false;webLoan=borrow.Operation;life=p.statLife;
            webPlacedX=webPlan.X;webPlacedY=webPlan.Y;webObserveUntil=webDeadline+2;
            webRequest=false;CobwebPlaced=CobwebProtected=false;Result=SmartFallResult.Prepared;
        }
        // Combat must see the request while it still owns its physical hold;
        // Valid/AllowsSmartFall subsequently proves the captured buttons only.
        internal bool CobwebCombatRequest(Player p,long owner)
        {
            return webRequest && webOwner>0 && (owner==0 || webOwner==owner) && ReferenceEquals(p,retained) && rescueTools!=null &&
                webSelection==rescueTools.SelectionIntent && rescueTools.Runtime.Generation==generation && BeforeDeadline(webDeadline) &&
                webSlot>=0 && webSlot<50 && ReferenceEquals(p.inventory[webSlot],webSource) && webSource.stack>0 && webSource.createTile==51 &&
                session.Value.SmartFallProtection.Enabled && session.Value.SmartFallProtection.Cobweb && permitted && scope && session.CanAct(p) && p.gravDir==webGravity;
        }
        private void CancelWebRequest(){webRequest=false;webOwner=0;webSource=null;webSlot=-1;}
        internal void CancelCobwebBorrow(Player p,long operation)
        {if(webSelected && operation==webLoan && ReferenceEquals(p,retained)){borrow.Restore(p,generation);if(!webIssued)Result=SmartFallResult.TakenOver;}}
        private void AdvanceWeb(Player p,bool canAct)
        {
            if(webRequest && (!BeforeDeadline(webDeadline) || !canAct || !session.Value.SmartFallProtection.Cobweb ||
                rescueTools==null || webSelection!=rescueTools.SelectionIntent || p.gravDir!=webGravity))CancelWebRequest();
            if(webSelected)
            {
                if(!borrow.HasPending){webSelected=false;webLoan=0;}
                else if(!canAct || !session.Value.SmartFallProtection.Cobweb || webIssued || !BeforeDeadline(webDeadline))borrow.Restore(p,generation);
            }
            if(webObserving && (!BeforeDeadline(webObserveUntil) || p.gravDir!=webGravity || p.dead || p.statLife<life))
            {webObserving=false;if(!CobwebProtected)Result=SmartFallResult.Failed;}
        }
        private bool BeginWebItemCheck(Player p)
        {
            if(webIssued)return false;
            if(!permitted || !session.CanAct(p) || !BeforeDeadline(webDeadline) || !borrow.SelectionReady ||
                !SmartFallCobwebSearch.CanPlace(p,webSource,webPlacedX,webPlacedY,ref TileReads))
            {borrow.Restore(p,generation);return false;}
            bool entered=borrow.BeginItemCheck(p);
            if(!entered)return false;
            webIssued=true;Count(ref NativeActions);
            webAim=new SmartFallCobwebAimScope(webPlacedX,webPlacedY);webAim.Apply();
            return true;
        }
        private void EndWebItemCheck(Player p,long operation,bool entered,Exception error)
        {
            try
            {
                if(!entered)return;
                bool settled=borrow.SettleCobweb(p,operation,entered,error,out int debit);
                borrow.EndItemCheck(p,operation,entered,error);
                Tile tile=SmartFallGeometry.Read(webPlacedX,webPlacedY,ref TileReads);
                bool web=tile!=null && tile.nactive() && tile.type==51;
                if(settled && debit==1){Count(ref CobwebDebits);if(web){CobwebPlaced=true;Count(ref CobwebPlacements);}}
                // Appearance alone is neither our debit nor protection. A
                // natural concurrent web may still protect at the real sample.
                webObserving=error==null && web;Result=settled?debit==1?SmartFallResult.Consumed:SmartFallResult.Failed:SmartFallResult.Unconfirmed;
                borrow.Restore(p,generation);
                if(!web)bandValid=false;
            }
            finally{webAim?.End();webAim=null;}
        }
        internal void BeforeSticky(Player p)
        {
            webStickySample=false;
            if(!Current(p) || p.mount.Active || p.shimmering || p.velocity.Y*p.gravDir<=0 || !(webObserving || NaturalWebOpportunity(p)))return;
            webStickySample=SmartFallGeometry.NativeWeb(p.position,p.width,p.height,p.gravDir,ref TileReads,null,0,out _,out _);
        }
        internal void AfterSticky(Player p)
        {
            if(!Current(p) || !webStickySample)return;webStickySample=false;
            if(p.fallStart!=(int)(p.position.Y/16) || p.velocity.Y*p.gravDir>.301f || webObserving && p.statLife<life)return;
            Count(ref CobwebTouches);
            var remaining=SmartFallGeometry.Contact(p,ref TileReads);
            if(!remaining.Found || !WebProtectsRemainder(p,p.position.Y,remaining))return;
            CobwebProtected=true;webObserving=false;Result=SmartFallResult.Protected;Count(ref Protections);CancelWebRequest();
        }
        private void ClearWeb()
        {CancelWebRequest();webSelected=webIssued=webObserving=webStickySample=false;webPlan=default(SmartFallCobwebPlan);webLoan=0;CobwebPlaced=CobwebProtected=false;}
    }

    internal sealed class SmartFallCobwebAimScope
    {
        private readonly int x,y,oldX,oldY,oldTileX,oldTileY;
        private readonly bool oldSmartMouse,oldSmartPad;
        private int ownX,ownY;
        private bool active;
        internal SmartFallCobwebAimScope(int x,int y)
        {this.x=x;this.y=y;oldX=Main.mouseX;oldY=Main.mouseY;oldTileX=Player.tileTargetX;oldTileY=Player.tileTargetY;oldSmartMouse=Main.SmartCursorWanted_Mouse;oldSmartPad=Main.SmartCursorWanted_GamePad;}
        internal void Apply()
        {
            Vector2 screen=Main.ReverseGravitySupport(new Vector2(x*16+8,y*16+8)-Main.screenPosition);
            ownX=(int)screen.X;ownY=(int)screen.Y;active=true;
            Main.SmartCursorWanted_Mouse=Main.SmartCursorWanted_GamePad=false;
            Main.mouseX=ownX;Main.mouseY=ownY;Player.tileTargetX=x;Player.tileTargetY=y;
        }
        internal void End()
        {
            if(!active)return;active=false;
            if(Main.mouseX==ownX)Main.mouseX=oldX;if(Main.mouseY==ownY)Main.mouseY=oldY;
            if(Player.tileTargetX==x)Player.tileTargetX=oldTileX;if(Player.tileTargetY==y)Player.tileTargetY=oldTileY;
            if(!Main.SmartCursorWanted_Mouse)Main.SmartCursorWanted_Mouse=oldSmartMouse;
            if(!Main.SmartCursorWanted_GamePad)Main.SmartCursorWanted_GamePad=oldSmartPad;
        }
    }
}
