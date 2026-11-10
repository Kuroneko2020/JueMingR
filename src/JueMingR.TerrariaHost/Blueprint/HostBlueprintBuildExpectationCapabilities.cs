using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        private enum NearCapabilityFamily { Types, Hammer, Brush, Roller, Scraper, Pick }
        // Shared by live facts/ranges, with no Item, source window or lease.
        // A recovery serial changes only when a matching qualified capability
        // returns/expands. Loss and unrelated types do not reopen negatives.
        private sealed class NearCapabilityDependency
        {
            internal string Identity;
            internal NearCapabilityFamily Family;
            internal int[] Types;
            internal int References;
            internal long Serial;
            internal bool Disposed;
        }
        private struct NearCapabilityType
        {
            internal int Families, Reach;
            internal long Serial;
        }
        private readonly Dictionary<string,NearCapabilityDependency> nearCapabilityKeys=new Dictionary<string,NearCapabilityDependency>();
        private Dictionary<int,NearCapabilityType> nearCapabilityTypes=new Dictionary<int,NearCapabilityType>();
        private Dictionary<int,NearCapabilityType> nearCapabilityNext=new Dictionary<int,NearCapabilityType>();
        private readonly Dictionary<int,NearCapabilityType> nearCapabilityLive=new Dictionary<int,NearCapabilityType>();
        private readonly long[] nearCapabilityFamilies=new long[6];
        private readonly int[] nearCapabilityReach=new int[6];
        private readonly long[] nearCapabilityLiveFamilies=new long[6];
        private object nearCapabilityCatalog;
        private long nearCapabilityCatalogIdentity,nearCapabilityClock;
        private bool nearCapabilityObserving,nearCapabilityLiveValid;
#if DEBUG
        // Conservative retained-size estimate, not measured GC allocation.
        // These dictionaries start at capacity zero, grow only by ordinary
        // insertion and are never pre-sized/trimmed. Four times the lifetime
        // maximum live count bounds their prime-sized bucket/entry capacity;
        // removals reuse free entries, and Clear retains those backing arrays.
        // Use the largest of the three <=50-type snapshots for each table: this
        // overcounts unequal capacities instead of hiding a cleared large one.
        private int nearCapabilityTypeHighWater,nearCapabilityKeyHighWater;
        private long nearCapabilityKeyPayloadBytes;
        internal long NearCapabilityPlanBytes
        {
            get
            {
                long typeSlots=4L*nearCapabilityTypeHighWater,keySlots=4L*nearCapabilityKeyHighWater;
                // Four dictionary objects/owner fields, two six-long arrays;
                // per-slot bounds include bucket ints, entry padding and refs.
                return 4L*128+2L*(24+6*8)+(24+6*4)+64+3L*typeSlots*48+keySlots*40+nearCapabilityKeyPayloadBytes;
            }
        }
        private static long NearCapabilityPayloadEstimate(NearCapabilityDependency dependency)
        {
            // Dependency object includes the refcount, family, serial and
            // disposed flag. Charge strings/arrays even when CLR interns an
            // empty/shared value; these are upper estimates of retained roots.
            long text=24L+2L*dependency.Identity.Length;
            long types=24L+4L*dependency.Types.Length;
            return 64+((text+7)&~7L)+((types+7)&~7L);
        }
        // Partition the common counter: slots are visited once by the shared
        // publisher/live fence; dependency reads never hide another bag walk.
        internal long NearBuildCapabilitySlotChecks { get; private set; }
        internal long NearBuildCapabilityDependencyChecks { get; private set; }
#endif
        private void NearCapabilitySlotWork()
        {
#if DEBUG
            NearBuildCapabilityChecks++;NearBuildCapabilitySlotChecks++;
#endif
        }
        private void NearCapabilityDependencyWork()
        {
#if DEBUG
            NearBuildCapabilityChecks++;NearBuildCapabilityDependencyChecks++;
#endif
        }
        // Call only in the paid automatic-planning input epoch. A material UI,
        // an empty outer callback or a retained native tail is not permission.
        // false does no collection walk and records no new capability work.
        private void BeginNearCapabilityObservation(bool enabled)
        {
            nearCapabilityObserving=enabled;nearCapabilityLiveValid=false;
            if(enabled){nearCapabilityNext.Clear();nearCapabilityLive.Clear();}
        }
        // The caller has already checked the same native item/slot against
        // BuildInventoryEligible in ObserveBuildSignals' existing 50-slot walk.
        private void ObserveNearCapabilityItem(int slot,Item item)
        {
            if(!nearCapabilityObserving)return;
            NearCapabilitySlotWork();
            AddNearCapabilityItem(nearCapabilityNext,slot,item);
        }
        private void AddNearCapabilityItem(Dictionary<int,NearCapabilityType> destination,int slot,Item item)
        {
            if(slot<0 || slot>=50 || item==null || item.IsAir)return;
            NearCapabilityType value;bool exists=destination.TryGetValue(item.type,out value);
            value.Reach=exists?Math.Max(value.Reach,item.tileBoost):item.tileBoost;
            NearCapabilityDependencyWork();
            if(item.hammer>0 && item.pick==0 && !item.channel && item.shoot<=0)value.Families|=1<<(int)NearCapabilityFamily.Hammer;
            NearCapabilityDependencyWork();
            if(item.type==ItemID.Paintbrush || item.type==ItemID.SpectrePaintbrush)value.Families|=1<<(int)NearCapabilityFamily.Brush;
            NearCapabilityDependencyWork();
            if(item.type==ItemID.PaintRoller || item.type==ItemID.SpectrePaintRoller)value.Families|=1<<(int)NearCapabilityFamily.Roller;
            NearCapabilityDependencyWork();
            if(ItemID.Sets.IsPaintScraper[item.type])value.Families|=1<<(int)NearCapabilityFamily.Scraper;
            if(item.pick>0)value.Families|=1<<(int)NearCapabilityFamily.Pick;
            destination[item.type]=value;
#if DEBUG
            nearCapabilityTypeHighWater=Math.Max(nearCapabilityTypeHighWater,destination.Count);
#endif
        }
        private void PublishNearCapabilityObservation()
        {
            if(!nearCapabilityObserving)return;
            Array.Clear(nearCapabilityReach,0,nearCapabilityReach.Length);
            int presentFamilies=0;
            // Arrays retain the greatest recovery even during absence. This
            // preserves a negative cutoff through ordinary positive-stack noise.
            foreach(var pair in nearCapabilityNext)
            {
                NearCapabilityDependencyWork();
                NearCapabilityType previous;var next=pair.Value;
                if(!nearCapabilityTypes.TryGetValue(pair.Key,out previous) ||
                    (next.Families&~previous.Families)!=0 || next.Reach>previous.Reach)next.Serial=++nearCapabilityClock;
                else next.Serial=previous.Serial;
                nearCapabilityLive[pair.Key]=next;
            }
            foreach(var pair in nearCapabilityLive)
            {
                NearCapabilityDependencyWork();nearCapabilityNext[pair.Key]=pair.Value;
                for(int family=1;family<6;family++)
                {
                    NearCapabilityDependencyWork();
                    if((pair.Value.Families&(1<<family))!=0)
                    {
                        nearCapabilityFamilies[family]=Math.Max(nearCapabilityFamilies[family],pair.Value.Serial);
                        // Wooden tools can have negative tileBoost. Absence's
                        // zero fallback must never widen a present -1 tool and
                        // sign a false in-reach negative across its real edge.
                        nearCapabilityReach[family]=(presentFamilies&(1<<family))==0?pair.Value.Reach:Math.Max(nearCapabilityReach[family],pair.Value.Reach);
                        presentFamilies|=1<<family;
                    }
                }
            }
            nearCapabilityLive.Clear();
            var old=nearCapabilityTypes;nearCapabilityTypes=nearCapabilityNext;nearCapabilityNext=old;
            foreach(var dependency in nearCapabilityKeys.Values)RefreshNearCapabilitySerial(dependency);
        }
        // The shared qualified inventory publication already computes maxima;
        // no candidate performs another inventory walk. A business family can
        // keep its legal long tool even when its current source is shorter.
        private int NearOperationBoost(BlueprintBuildOperation operation,int fallbackType)
        {
            if(operation?.Kind==BlueprintBuildOperationKind.Hammer || HoikOperation(operation) && operation.HoikWaitTool==1)return nearCapabilityReach[(int)NearCapabilityFamily.Hammer];
            if(operation?.Kind==BlueprintBuildOperationKind.PaintTile)return nearCapabilityReach[(int)NearCapabilityFamily.Brush];
            if(operation?.Kind==BlueprintBuildOperationKind.PaintWall)return nearCapabilityReach[(int)NearCapabilityFamily.Roller];
            if(operation?.Kind==BlueprintBuildOperationKind.Scrape)return nearCapabilityReach[(int)NearCapabilityFamily.Scraper];
            if(WiringOperation(operation))
            {
                int best=0;
                for(int option=0;option<3;option++){NearCapabilityType capability;int type=WiringToolType(operation,option);if(type>0 && WiringToolMatches(operation,type) && nearCapabilityTypes.TryGetValue(type,out capability))best=Math.Max(best,capability.Reach);}
                return best;
            }
            // Producer matching may admit a different actual Item. Its shared
            // discovery envelope is conservative until the exact plan rebinds.
            if(operation?.NativePlacement?.Producer!=null)return buildEnvelopeBoost;
            NearCapabilityType value;
            return nearCapabilityTypes.TryGetValue(fallbackType,out value)?value.Reach:0;
        }
        private int NearLiveOperationBoost(BlueprintBuildOperation operation,int fallbackType)
        {
            var family=NearCapabilityFamily.Types;
            if(operation?.Kind==BlueprintBuildOperationKind.Hammer || HoikOperation(operation) && operation.HoikWaitTool==1)family=NearCapabilityFamily.Hammer;
            else if(operation?.Kind==BlueprintBuildOperationKind.PaintTile)family=NearCapabilityFamily.Brush;
            else if(operation?.Kind==BlueprintBuildOperationKind.PaintWall)family=NearCapabilityFamily.Roller;
            else if(operation?.Kind==BlueprintBuildOperationKind.Scrape)family=NearCapabilityFamily.Scraper;
            int best=0;
            if(family!=NearCapabilityFamily.Types || operation?.NativePlacement?.Producer!=null)
            {
                bool found=false;
                foreach(var pair in nearCapabilityLive)
                {NearCapabilityDependencyWork();if(family==NearCapabilityFamily.Types || (pair.Value.Families&(1<<(int)family))!=0){best=found?Math.Max(best,pair.Value.Reach):pair.Value.Reach;found=true;}}
                return best;
            }
            if(WiringOperation(operation))
            {
                for(int option=0;option<3;option++)
                {NearCapabilityDependencyWork();NearCapabilityType capability;int type=WiringToolType(operation,option);if(type>0 && WiringToolMatches(operation,type) && nearCapabilityLive.TryGetValue(type,out capability))best=Math.Max(best,capability.Reach);}
                return best;
            }
            NearCapabilityType value;NearCapabilityDependencyWork();
            return nearCapabilityLive.TryGetValue(fallbackType,out value)?value.Reach:0;
        }
        private NearCapabilityDependency GetNearCapabilityDependency(BlueprintBuildOperation operation,CaptureCell cell,CaptureObject? obj,int fallbackType)
        {
            if(!nearCapabilityObserving)return null;
            NearCapabilityDependencyWork();
            var family=NearCapabilityFamily.Types;string identity;int[] types=null;
            if(HoikOperation(operation) && operation.HoikWaitTool==3)
            {
                // Starting/swap waits subscribe to this policy's actual final
                // platform set, not to the temporary stone or original-only
                // type. The existing shared type serials wake allowed restock
                // without polling every candidate or widening its reach.
                var placement=MaterialCatalog?.Tile(cell,obj);var accepted=new List<int>();
                if(placement!=null)
                {
                    accepted.Add(placement.UseItemType);
                    var candidates=MaterialCatalog.Substitution?.Candidates(placement,substitutionPolicy);
                    if(candidates!=null)foreach(var candidate in candidates)
                        if(BasicPlacement(candidate) && !accepted.Contains(candidate.UseItemType))accepted.Add(candidate.UseItemType);
                }
                types=accepted.ToArray();identity="hoik-platform:"+(placement?.UseItemType??0)+":"+substitutionRevision;
            }
            else if(HoikOperation(operation) && operation.HoikWaitTool==2){family=NearCapabilityFamily.Pick;identity="hoik-pick";}
            else if(operation?.Kind==BlueprintBuildOperationKind.Hammer || HoikOperation(operation) && operation.HoikWaitTool==1)
            {
                family=NearCapabilityFamily.Hammer;
                // The original hammer matcher also rejects axe/hammer tiles.
                if(Main.tileHammer[cell.TileType] || Main.tileAxe[cell.TileType]){family=NearCapabilityFamily.Types;types=Array.Empty<int>();identity="hammer-forbidden";}
                else identity="hammer";
            }
            else if(operation?.Kind==BlueprintBuildOperationKind.PaintTile){family=NearCapabilityFamily.Brush;identity="brush";}
            else if(operation?.Kind==BlueprintBuildOperationKind.PaintWall){family=NearCapabilityFamily.Roller;identity="roller";}
            else if(operation?.Kind==BlueprintBuildOperationKind.Scrape){family=NearCapabilityFamily.Scraper;identity="scraper";}
            else if(WiringOperation(operation))identity="wire:"+(int)operation.Kind+":"+operation.Mode;
            else if(operation?.NativePlacement?.Producer!=null)
            {
                if(!ReferenceEquals(nearCapabilityCatalog,MaterialCatalog)){nearCapabilityCatalog=MaterialCatalog;nearCapabilityCatalogIdentity++;}
                // Exactly the fields read by this catalog's Matches/Producers
                // query. No world position, Item, object payload or whole catalog.
                identity="producer:"+nearCapabilityCatalogIdentity+":"+(int)cell.Layers+":"+cell.TileType+":"+cell.FrameX+":"+cell.FrameY+":"+cell.Slope+":"+cell.HalfBrick+":"+(obj?.Width??0)+":"+(obj?.Height??0);
            }
            else identity="type:"+fallbackType;
            NearCapabilityDependency dependency;
            if(nearCapabilityKeys.TryGetValue(identity,out dependency)){dependency.References++;return dependency;}
            if(family==NearCapabilityFamily.Types && types==null)
            {
                var accepted=new List<int>();
                if(WiringOperation(operation))for(int option=0;option<3;option++)
                {NearCapabilityDependencyWork();int type=WiringToolType(operation,option);if(type>0 && WiringToolMatches(operation,type) && !accepted.Contains(type))accepted.Add(type);}
                else if(operation?.NativePlacement?.Producer!=null && MaterialCatalog!=null)
                    foreach(var producer in MaterialCatalog.Producers(cell,obj))
                    {NearCapabilityDependencyWork();if(producer.Producer!=null && producer.UseItemType>0 && !accepted.Contains(producer.UseItemType))accepted.Add(producer.UseItemType);}
                else if(fallbackType>0)accepted.Add(fallbackType);
                types=accepted.ToArray();
            }
            dependency=new NearCapabilityDependency{Identity=identity,Family=family,Types=types??Array.Empty<int>(),References=1};
            nearCapabilityKeys.Add(identity,dependency);
#if DEBUG
            nearCapabilityKeyHighWater=Math.Max(nearCapabilityKeyHighWater,nearCapabilityKeys.Count);
            nearCapabilityKeyPayloadBytes+=NearCapabilityPayloadEstimate(dependency);
#endif
            RefreshNearCapabilitySerial(dependency);return dependency;
        }
        private void RefreshNearCapabilitySerial(NearCapabilityDependency dependency)
        {
            NearCapabilityDependencyWork();
            if(dependency.Family!=NearCapabilityFamily.Types)dependency.Serial=Math.Max(dependency.Serial,nearCapabilityFamilies[(int)dependency.Family]);
            else foreach(int type in dependency.Types)
            {NearCapabilityDependencyWork();NearCapabilityType value;if(nearCapabilityTypes.TryGetValue(type,out value))dependency.Serial=Math.Max(dependency.Serial,value.Serial);}
        }
        private long CurrentNearCapabilitySerial(NearCapabilityDependency dependency)
        {NearCapabilityDependencyWork();return dependency==null || dependency.Disposed?-1:dependency.Serial;}
        private void ReleaseNearCapabilityDependency(NearCapabilityDependency dependency)
        {
            if(dependency==null || dependency.Disposed)return;
            if(--dependency.References==0)
            {
                nearCapabilityKeys.Remove(dependency.Identity);dependency.Disposed=true;
#if DEBUG
                nearCapabilityKeyPayloadBytes-=NearCapabilityPayloadEstimate(dependency);
#endif
            }
        }
        // Begin ONCE around the unique final B-admission dependency set, not
        // once per fact/key. Its one qualified bag snapshot sees an in-callback
        // new source; LiveRecovery below never scans inventory or changes Items.
        private void BeginNearCapabilityLiveValidation(Player p,bool enabled)
        {
            nearCapabilityLiveValid=enabled && p!=null;
            if(!nearCapabilityLiveValid)return;
            nearCapabilityLive.Clear();Array.Copy(nearCapabilityFamilies,nearCapabilityLiveFamilies,nearCapabilityFamilies.Length);
            for(int slot=0;slot<50;slot++)
            {
                NearCapabilitySlotWork();var item=p.inventory[slot];
                if(item!=null && !item.IsAir && buildTools.BuildInventoryEligible(slot,item))AddNearCapabilityItem(nearCapabilityLive,slot,item);
            }
            // A speculative live fence never signs/updates the published clock.
            foreach(var pair in nearCapabilityLive)
            {
                NearCapabilityDependencyWork();NearCapabilityType old;
                long serial=!nearCapabilityTypes.TryGetValue(pair.Key,out old) ||
                    (pair.Value.Families&~old.Families)!=0 || pair.Value.Reach>old.Reach?nearCapabilityClock+1:old.Serial;
                for(int family=1;family<6;family++)
                {NearCapabilityDependencyWork();if((pair.Value.Families&(1<<family))!=0)nearCapabilityLiveFamilies[family]=Math.Max(nearCapabilityLiveFamilies[family],serial);}
            }
        }
        private bool LiveNearCapabilityRecovery(NearCapabilityDependency dependency,long cutoff)
        {
            NearCapabilityDependencyWork();
            if(!nearCapabilityLiveValid || dependency==null || dependency.Disposed)return true; // unknown cannot authorize B
            if(dependency.Family!=NearCapabilityFamily.Types)return nearCapabilityLiveFamilies[(int)dependency.Family]>cutoff;
            foreach(int type in dependency.Types)
            {
                NearCapabilityDependencyWork();NearCapabilityType live,old;
                if(!nearCapabilityLive.TryGetValue(type,out live))continue;
                long serial=!nearCapabilityTypes.TryGetValue(type,out old) || (live.Families&~old.Families)!=0 || live.Reach>old.Reach?nearCapabilityClock+1:old.Serial;
                if(serial>cutoff)return true;
            }
            return false;
        }
        private void ClearNearCapabilityDependencies()
        {
            foreach(var dependency in nearCapabilityKeys.Values)dependency.Disposed=true;
            nearCapabilityKeys.Clear();nearCapabilityTypes.Clear();nearCapabilityNext.Clear();nearCapabilityLive.Clear();
#if DEBUG
            // Only payload roots are released. All four Dictionary instances
            // retain their high-water backing arrays, including while OFF.
            nearCapabilityKeyPayloadBytes=0;
#endif
            Array.Clear(nearCapabilityFamilies,0,nearCapabilityFamilies.Length);Array.Clear(nearCapabilityLiveFamilies,0,nearCapabilityLiveFamilies.Length);
            nearCapabilityCatalog=null;nearCapabilityObserving=nearCapabilityLiveValid=false;
        }
    }
}
