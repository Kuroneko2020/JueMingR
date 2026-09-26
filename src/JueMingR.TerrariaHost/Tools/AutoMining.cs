using System;
using System.Collections.Generic;
using JueMingR.Features.Tools;
using JueMingR.TerrariaHost.World;
using JueMingR.TerrariaHost.Rendering;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Tools
{
    internal sealed class AutoMining
    {
        internal struct ManualHit {internal bool Valid,Automatic;internal int X,Y,Type,Slot;internal Item Tool;internal long Session;internal ulong Below;internal MiningRegion BeforeRegion;internal Dictionary<long,ulong> BeforeGravity;}
        private struct Falling {internal int X,Y,Type;internal ulong Occupied,Created;}
        private readonly HostTools host;
        internal MiningRegion Region {get;private set;}=new MiningRegion();
        private Item tool;
        private int slot,type,cursor;
        private long session,selection;
        private readonly Falling[] falling=new Falling[MiningRegion.Capacity];
        private readonly Dictionary<long,ulong> gravityBaseline=new Dictionary<long,ulong>();
        private readonly System.Collections.Generic.HashSet<long> vacated=new System.Collections.Generic.HashSet<long>();
        private int falls,fallCursor;
        private bool manualHeld;
        private long gesture,consumedGesture=-1;
        internal void ObserveManual()
        {
            bool held=PlayerInput.Triggers.Current.MouseLeft;
            if(!held){manualHeld=false;return;}
            if(host.Mode(2)!=2 || !host.Input.CanStartActions)return;
            if(held && !manualHeld)gesture++;manualHeld=held;
        }
        private readonly Vector2[] overlay=new Vector2[MiningRegion.Capacity];
        private readonly bool[] green=new bool[MiningRegion.Capacity];
        private int drawn;
#if DEBUG
        internal long OverlayChecks {get;private set;}
#endif
        internal AutoMining(HostTools host){this.host=host;}
        // Dirt is an active type-zero tile. Keep it distinct from confirmed
        // air (zero) and unreadable (-1) in gravity occupancy observations.
        private static int Read(int x,int y){var t=WorldTileObservation.ReadCurrent(x,y);return !t.Readable?-1:t.Active?(t.Type==0?int.MaxValue:t.Type):0;}
        internal bool Select(Player p,int x,int y,int seedType,bool removed)
        {
            if(p==null || p.selectedItem<0 || p.selectedItem>=50 || p.HeldItem.pick<=0)return false;
            var candidate=new MiningRegion();if(!candidate.Select(x,y,seedType,Read,removed))return false;
            Adopt(p,candidate,CaptureGravity(candidate));return true;
        }
        private static Dictionary<long,ulong> CaptureGravity(MiningRegion region)
        {var values=new Dictionary<long,ulong>();for(int i=0;i<region.Count;i++){var point=region[i];if(point.Type==123 || point.Type==224)values[Key(point.X,point.Y)]=Below(point.X,point.Y);}return values;}
        private void Adopt(Player p,MiningRegion candidate,Dictionary<long,ulong> baseline)
        {
            Region=candidate;tool=p.HeldItem;slot=p.selectedItem;type=tool.type;session=host.Runtime.Generation;selection=host.SelectionIntent;cursor=falls=drawn=fallCursor=0;
            gravityBaseline.Clear();foreach(var pair in baseline)gravityBaseline.Add(pair.Key,pair.Value);
        }
        private static long Key(int x,int y){return ((long)x<<32)|(uint)y;}
        private static ulong Below(int x,int y)
        {ulong occupied=0;for(int dx=-1;dx<=1;dx++)for(int dy=1;dy<=18;dy++)if(Read(x+dx,y+dy)!=0)occupied|=1UL<<((dx+1)*18+dy-1);return occupied;}
        private void WaitForFalling(int x,int y,int tile,ulong occupied)
        {
            for(int i=0;i<falls;i++)if(falling[i].X==x && falling[i].Y==y)return;
            if(falls<falling.Length)falling[falls++]=new Falling{X=x,Y=y,Type=tile,Occupied=occupied,Created=host.Tick};
        }
        internal ManualHit BeforePick(Player p,int x,int y)
        {
            if(host.Mode(2)==0 || !ReferenceEquals(p,host.Player))return default(ManualHit);
            bool owned=host.Use.Is(ToolKind.Mining);
            if(!owned && (host.Mode(2)!=2 || consumedGesture==gesture || !host.Input.CanStartActions || !PlayerInput.Triggers.Current.MouseLeft || host.Items.Ownership.IsUseSlot(p.selectedItem)))return default(ManualHit);
            if(p.selectedItem<0 || p.selectedItem>=50 || p.HeldItem.pick<=0)return default(ManualHit);
            var before=WorldTileObservation.ReadCurrent(x,y);if(!before.Readable || !before.Active || !MiningRegion.Supported(before.Type))return default(ManualHit);
            ulong occupied=0;
            if(before.Type==123 || before.Type==224)occupied=Below(x,y);
            var hit=new ManualHit{Valid=true,Automatic=owned,X=x,Y=y,Type=before.Type,Slot=p.selectedItem,Tool=p.HeldItem,Session=host.Runtime.Generation,Below=occupied};
            // Vanilla recursively drops upper silt inside this very PickTile.
            // Snapshot before the real hit, but publish nothing unless its
            // successful return proves the seed changed. No trial digging.
            if(!owned && (before.Type==123 || before.Type==224) && MiningEligibility.CanProgress(p,p.HeldItem,x,y,before.Type))
            {var region=new MiningRegion();if(region.Select(x,y,before.Type,Read)){hit.BeforeRegion=region;hit.BeforeGravity=CaptureGravity(region);}}
            return hit;
        }
        internal void AfterPick(Player p,ManualHit hit)
        {
            if(!hit.Valid || !ReferenceEquals(p,host.Player) || hit.Session!=host.Runtime.Generation || p.selectedItem!=hit.Slot || !ReferenceEquals(p.HeldItem,hit.Tool))return;
            var after=WorldTileObservation.ReadCurrent(hit.X,hit.Y);if(!after.Readable || after.Active && after.Type==hit.Type)return;
            if(!hit.Automatic && host.Mode(2)==2){consumedGesture=gesture;if(hit.BeforeRegion!=null)Adopt(p,hit.BeforeRegion,hit.BeforeGravity);else Select(p,hit.X,hit.Y,hit.Type,true);}
            if(hit.Type==123 || hit.Type==224)WaitForFalling(hit.X,hit.Y,hit.Type,hit.Below);
        }
        internal void Update()
        {
            Player p=host.Player;
            // A native override temporarily borrows another slot. The original
            // pick remains the owner; only new explicit intent or replacement
            // of that exact source cancels the retained mining region.
            if(!host.KeepsIntent(2) || p==null || p.dead || tool!=null && (session!=host.Runtime.Generation || selection!=host.SelectionIntent || !ReferenceEquals(p.inventory[slot],tool) || tool.type!=type || tool.stack<=0 || tool.pick<=0)) {Clear();return;}
            if(tool==null)return;
            if(Region.TooFar((int)(p.Center.X/16),(int)(p.Center.Y/16))){Clear();return;}
            vacated.Clear();
            for(int i=Region.Count-1;i>=0;i--)
            {
                var point=Region[i];int value=Read(point.X,point.Y);if(value<0 || value==point.Type)continue;
                if(value==0)vacated.Add(Key(point.X,point.Y));
                // Removing one support can drop several selected cells. Their
                // prior bounded observations survive the disappearance; the
                // PickTile target is not the only possible falling member.
                ulong before;if(gravityBaseline.TryGetValue(Key(point.X,point.Y),out before)){WaitForFalling(point.X,point.Y,point.Type,before);gravityBaseline.Remove(Key(point.X,point.Y));}
                Region.RemoveAt(i);
            }
            // A previously occupied cell may receive an upper selected block
            // only after we observed that selected cell become empty. Clear
            // that bit in existing finite witnesses; unrelated old blocks
            // remain excluded, without an ever-growing history set.
            if(vacated.Count>0)for(int i=0;i<falls;i++)
            {var f=falling[i];for(int dx=-1;dx<=1;dx++)for(int dy=1;dy<=18;dy++)if(vacated.Contains(Key(f.X+dx,f.Y+dy)))f.Occupied&=~(1UL<<((dx+1)*18+dy-1));falling[i]=f;}
            UpdateFalling();drawn=Region.Count;
            for(int i=0;i<drawn;i++)
            {
#if DEBUG
                OverlayChecks++;
#endif
                var point=Region[i];overlay[i]=new Vector2(point.X*16+8,point.Y*16+8);green[i]=p.selectedItem==slot && ReferenceEquals(p.HeldItem,tool) && MiningEligibility.CanProgress(p,tool,point.X,point.Y,point.Type);
            }
        }
        private void UpdateFalling()
        {
            int budget=8;
            for(;falls>0 && budget>0;budget--)
            {
                int i=fallCursor%falls;
                // 135 real updates allow two full 512/8 observation rounds,
                // including a column that lands after its first inspection.
                var f=falling[i];bool retire=host.Tick-f.Created>=135;
                for(int dy=1;dy<=18 && !retire;dy++)for(int dx=-1;dx<=1 && !retire;dx++)
                {
                    int bit=(dx+1)*18+dy-1;if((f.Occupied&(1UL<<bit))!=0 || Read(f.X+dx,f.Y+dy)!=f.Type)continue;
                    retire=Region.AddFallen(new MiningPoint(f.X+dx,f.Y+dy,f.Type));
                    if(retire)gravityBaseline[Key(f.X+dx,f.Y+dy)]=Below(f.X+dx,f.Y+dy);
                }
                if(retire){falling[i]=falling[--falls];falling[falls]=default(Falling);}
                else fallCursor=(i+1)%falls;
            }
        }
        internal ToolIntent Choose(Player p)
        {
            if(host.Mode(2)==0 || tool==null || !host.Admit(p,false) || p.selectedItem!=slot || !ReferenceEquals(p.HeldItem,tool))return null;
            for(int n=0;n<Region.Count;n++)
            {
                int i=(cursor+n)%Region.Count;var point=Region[i];if(!MiningEligibility.CanProgress(p,tool,point.X,point.Y,point.Type))continue;
                // Native HitTile has a finite damage cache. Finish this valid
                // tile before advancing, otherwise a broad low-power vein can
                // evict every partial hit without ever removing anything.
                cursor=i;
                return new ToolIntent{Kind=ToolKind.Mining,Slot=slot,Target=new Vector2(point.X*16+8,point.Y*16+8),
                    Valid=()=>host.Mode(2)!=0 && session==host.Runtime.Generation && ReferenceEquals(p.HeldItem,tool) && MiningEligibility.CanProgress(p,tool,point.X,point.Y,point.Type)};
            }
            return null;
        }
        internal void Draw()
        {
            if(drawn==0 || host.Mode(2)==0 || !WorldPresentation.CanDraw || Main.spriteBatch==null)return;
            Matrix zoom=Main.GameViewMatrix.ZoomMatrix;
            for(int i=0;i<drawn;i++)
            {
                Vector2 screen=Main.ReverseGravitySupport(overlay[i]-Main.screenPosition);Vector2 a=Vector2.Transform(screen-new Vector2(8),zoom),b=Vector2.Transform(screen+new Vector2(8),zoom);
                if(b.X<0 || b.Y<0 || a.X>Main.screenWidth || a.Y>Main.screenHeight)continue;
                var rect=new Rectangle((int)screen.X-8,(int)screen.Y-8,16,16);var color=green[i]?Color.FromNonPremultiplied(65,230,95,90):Color.FromNonPremultiplied(240,65,65,90);
                Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,rect,color);
            }
        }
        internal void Clear(){Region.Clear();gravityBaseline.Clear();vacated.Clear();tool=null;falls=drawn=cursor=fallCursor=0;manualHeld=false;}
    }
}
