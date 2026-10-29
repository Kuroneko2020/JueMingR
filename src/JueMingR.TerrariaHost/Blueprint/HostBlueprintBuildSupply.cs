using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.Items;
using JueMingR.TerrariaHost.Tools;
using Terraria;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        private ItemMaterialPreparation buildMaterialPreparation;
        private BlueprintBuildTarget buildSupplyTarget;
        private BuildOperationPlan buildSupplyPlan;
        private long buildSupplyReadyFlight;
        private bool buildPendingProtectionInstalled;

        private void ClearBuildSupplyHint()
        { buildSupplyTarget=null;buildSupplyPlan=null;buildSupplyReadyFlight=0; }

        private void InitializeBuildSupply()
        {
            if(buildMaterialPreparation==null)buildMaterialPreparation=new ItemMaterialPreparation(buildTools);
            buildTools.BuildPreparationOwns=(array,slot)=>buildMaterialPreparation.OwnsEndpoint(array,slot);
            buildTools.BuildCleanupMaterial=item=>buildMaterialPreparation.IsDisplaced(item);
            if(!buildPendingProtectionInstalled)
            {
                buildPendingProtectionInstalled=true;var previous=buildTools.Items.World.AdditionalProtection;
                buildTools.Items.World.AdditionalProtection=item=>(previous?.Invoke(item)??false)||WiringPendingMaterial(item);
            }
        }
        private bool TryPrepareBuildSupply(BlueprintBuildTarget target,Player p,int desiredType,int destination,out int currentSlot,BuildOperationPlan plan=null)
        {
            currentSlot=-1;
            var preparation=buildMaterialPreparation;
            if(preparation==null || desiredType<=0)return false;
            if(preparation.HasWork)
            {
                if(preparation.Unknown)
                {
                    int independent=ConstructionItemSlot(p,desiredType);
                    if(independent>=0 && (destination==54?ReferenceEquals(FirstBuildPaint(p),p.inventory[independent]):
                        destination==0?independent==0:FirstBuildMaterialSlot(p,desiredType)==independent))
                    {currentSlot=independent;return true;}
                    WaitForBuild(target,BlueprintBuildWait.Dependencies,preparation.Slots);return false;
                }
                if(preparation.Material!=null && preparation.Material.type==desiredType && !preparation.Material.IsAir &&
                    preparation.TryPrepare(p,-1,destination,target.Identity,out currentSlot))
                {buildSupplyTarget=target;buildSupplyPlan=plan;buildSupplyReadyFlight=0;return true;}
                if(!preparation.Using && buildSupplyTarget!=null && !SameBuildTarget(buildSupplyTarget,target))
                {WaitForDynamicBuild(target);return false;}
                if(preparation.Material==null || preparation.Material.IsAir || preparation.Material.type!=desiredType)preparation.RequestReturn();
                WaitForDynamicBuild(target);return false;
            }
            int source=ConstructionItemSlot(p,desiredType);
            if(source<0){WaitForBuild(target,BlueprintBuildWait.Material);return false;}
            if(destination==54)
            {
                Item first=FirstBuildPaint(p);
                if(ReferenceEquals(first,p.inventory[source])){currentSlot=source;return true;}
            }
            else if(destination<0)
            {
                // Ordinary wire/wand consumers read the first real positive
                // stack. Never hide a protected earlier item to select ours.
                int first=FirstBuildMaterialSlot(p,desiredType);
                if(first==source){currentSlot=source;return true;}
                WaitForBuild(target,BlueprintBuildWait.Dependencies,first>=0?1UL<<first:0);return false;
            }
            else if(source==destination){currentSlot=source;return true;}
            buildSupplyTarget=target;buildSupplyPlan=plan;buildSupplyReadyFlight=0;
            bool ready=preparation.TryPrepare(p,source,destination,target.Identity,out currentSlot);
            if(!ready)WaitForDynamicBuild(target);
            return ready;
        }
        private bool TryPlanBuildSupply(BlueprintBuildTarget target, Player p, int type, int destination,
            out int slot, out BlueprintBuildWait wait, out ulong dependencySlots)
        {
            slot = -1; wait = BlueprintBuildWait.None; dependencySlots = 0;
            if (p == null || type <= 0 || buildMaterialPreparation == null) { wait = BlueprintBuildWait.Material; return false; }
            var preparation = buildMaterialPreparation;
            int ready;
            if (preparation.TryReadSupply(p, type, destination, out ready))
            {
                bool selected = destination == 54 ? ReferenceEquals(FirstBuildPaint(p), p.inventory[ready]) :
                    destination == 0 ? ready == 0 : FirstBuildMaterialSlot(p, type) == ready;
                if (selected && !preparation.IsDisplaced(p.inventory[ready])) { slot = ready; return true; }
            }
            int source = ConstructionItemSlot(p, type);
            if (source < 0) { wait = BlueprintBuildWait.Material; return false; }
            int first = destination == 54 ? Array.IndexOf(p.inventory, FirstBuildPaint(p)) : destination == 0 ? 0 : FirstBuildMaterialSlot(p, type);
            if (first == source) { slot = source; return true; }
            if (preparation.HasWork)
            { wait = preparation.Unknown ? BlueprintBuildWait.Dependencies : BlueprintBuildWait.Dynamic; dependencySlots = preparation.Slots; return false; }
            if (destination < 0) { wait = BlueprintBuildWait.Dependencies; dependencySlots = first >= 0 ? 1UL << first : 0; return false; }
            // A legal main-bag source exists, but the selector needs a real
            // native preparation. This is a selectable fallback, never proof
            // that the current continuous source can already serve it.
            slot = -1; return true;
        }
        private Item ReadBuildPaintSlot(Player p, int slot)
        {
            var item = p.inventory[slot];
            if (item != null && !item.PaintOrCoating) return item;
            if (item != null && !item.IsAir && !p.inventoryChestStack[slot] && !buildTools.Items.World.ManualMaterials.Contains(item) &&
                !buildTools.ProtectedAfterYield(item) && !WiringPendingMaterial(item) && buildMaterialPreparation?.IsDisplaced(item) != true)
            {
                // This is only the builder's read-only selector. The global
                // native guard still requires its real consumer scope. A
                // current exact build owner or Ready receipt can be observed
                // before ItemCheck without hiding its own protected primary.
                var flight = activeBuild; var use = buildTools.Use;
                if (flight != null && flight.Token > 0 && flight.Token == use.Operation && ReferenceEquals(flight.Player, p) &&
                    flight.Target.Identity.SessionGeneration == generation && !IsUnconfirmedTarget(flight.Target) && use.OwnsBuildMaterial(item)) return item;
                int ready = -1;
                if (buildMaterialPreparation?.TryReadSupply(p, item.type, 54, out ready) == true && ready == slot) return item;
            }
            return ItemPendingGuards.ReadSelectionSlot(p.inventory, slot);
        }
        private Item FirstBuildPaint(Player p)
        {
            for(int i=54;i<58;i++){var item=ReadBuildPaintSlot(p,i);if(item.PaintOrCoating && item.stack>0)return item;}
            for(int i=0;i<58;i++){var item=ReadBuildPaintSlot(p,i);if(item.PaintOrCoating && item.stack>0)return item;}
            return null;
        }
        private static int FirstBuildMaterialSlot(Player p,int type)
        {for(int slot=0;slot<58;slot++)if(p.inventory[slot].type==type && p.inventory[slot].stack>0)return slot;return -1;}
        private bool ContinueBuildSupply(Player p)
        {
            var preparation=buildMaterialPreparation;
            if(preparation?.HasWork!=true || preparation.Using || preparation.Unknown)return false;
            if(preparation.Abandoning || preparation.Returning){ClearBuildSupplyHint();return true;}
            // A naturally ended use may leave only a checked, ready successor
            // and its finite inventory receipt. No old completed target is
            // replayed, and a revoked successor immediately starts return.
            if(buildSupplyReadyFlight>0)
            {
                if(preparation.Ready && preparedBuild!=null && preparedBuild.Generation==buildSupplyReadyFlight &&
                    ReferenceEquals(preparedBuild.Target,buildSupplyTarget) && CanBuildAutomatically(p) && BuildFlightCurrent(preparedBuild,false))return false;
                // This receipt was retained for exactly one proven flight.
                // Losing that successor cannot turn its absent plan into an
                // invitation to choose another tool for the same target.
                preparation.RequestReturn();ClearBuildSupplyHint();return true;
            }
            var target=buildSupplyTarget;
            if(target==null || !IsBuildTargetCurrent(target) || !CanBuildAutomatically(p))
            {preparation.RequestReturn();return true;}
            BuildFlight flight;
            if(TryPrepareSelectedBuild(target,p,out flight,buildSupplyPlan))preparedBuild=flight;
            else if(preparation.Ready)preparation.RequestReturn();
            return true;
        }
        private void AddPreparedBuildEndpoints(Player p,List<BuildSource> dependencies,int toolSlot,ref ulong slots)
        {
            var preparation=buildMaterialPreparation;if(preparation?.HasWork!=true)return;
            ulong endpoints=preparation.Slots;
            for(int slot=0;slot<58;slot++)
            {
                ulong bit=1UL<<slot;if((endpoints&bit)==0 || slot==toolSlot || (slots&bit)!=0)continue;
                var item=p.inventory[slot];if(!preparation.OwnsEndpoint(p.inventory,slot))continue;
                var endpoint=SnapshotSource(slot,item);endpoint.Cleanup=true;endpoint.Maximum=0;
                dependencies.Add(endpoint);slots|=bit;
            }
        }
        private bool AcquirePreparedBuildLease(BuildFlight flight,long token)
        {
            var preparation=buildMaterialPreparation;
            if(buildSupplyReadyFlight>0 && (flight.Generation!=buildSupplyReadyFlight || !ReferenceEquals(flight.Target,buildSupplyTarget)))return false;
            ConfigurePreparedBuildBudgets(flight);
            bool acquired=preparation?.Ready==true && preparation.Blocks(flight.Target.Identity) ? preparation.TryAdoptUse(token,flight.Source.Slot,flight.DependencySlots) :
                buildTools.Items.Ownership.TryBeginUse(buildTools.Runtime.Generation,flight.Source.Slot,token,flight.DependencySlots);
            if(acquired)buildSupplyReadyFlight=0;
            return acquired;
        }
        private void ConfigurePreparedBuildBudgets(BuildFlight flight)
        {
            var preparation=buildMaterialPreparation;
            if(preparation?.HasWork==true)
            {
                // The adapter can outlive this projection on cancellation or
                // unknown identity. Keep only its finite budget vector; a
                // closure over flight would retain Range/Mask/source payloads.
                int heldSlot=flight.Source.Slot,heldMaximum=BuildHeldMaximum(flight);
                var dependencies=flight.Dependencies;
                preparation.UseConsumptionMaximum=slot=>
                {
                    if(slot==heldSlot)return heldMaximum;
                    foreach(var source in dependencies)if(source.Slot==slot)return source.Maximum;
                    return 0;
                };
            }
        }
        private void ReturnPreparedBuildLease(BuildFlight flight,bool unknown)
        {
            var preparation=buildMaterialPreparation;
            if(preparation?.Using!=true)return;
            var next=preparedBuild;int supply;
            bool other;
            if(!unknown && flight.Checked && !flight.Rejected && CanBuildAutomatically(flight.Player) &&
                !IsUnconfirmedTarget(flight.Target) && BuildContinuationCurrent(flight.Player) && !buildContinuation.DebtDue &&
                next!=null && next.Operation?.SupplyType>0 && SameBuildSource(flight,next,flight.DependencySlots) && BuildFlightCurrent(next,false) &&
                preparation.TryReadSupply(flight.Player,next.Operation.SupplyType,next.Operation.SupplyDestination,out supply) &&
                !buildTools.Contended(ToolKind.Build,out other) && preparation.TryReturnUseReady(flight.Token,next.Target.Identity))
            {
                // This owns no ToolUse or automatic input. Only the already
                // current exact successor can adopt the finite receipt; a
                // stale target or competing tool sends it through real return.
                buildSupplyTarget=next.Target;buildSupplyPlan=null;buildSupplyReadyFlight=next.Generation;return;
            }
            preparation.ReturnUse(flight.Token);
        }
    }
}
