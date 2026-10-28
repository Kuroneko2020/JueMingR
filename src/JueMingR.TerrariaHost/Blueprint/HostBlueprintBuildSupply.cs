using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.Items;
using Terraria;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        private ItemMaterialPreparation buildMaterialPreparation;
        private BlueprintBuildTarget buildSupplyTarget;
        private bool buildPendingProtectionInstalled;

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
        private bool TryPrepareBuildSupply(BlueprintBuildTarget target,Player p,int desiredType,int destination,out int currentSlot)
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
                    preparation.TryPrepare(p,-1,destination,target.Identity,out currentSlot))return true;
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
            buildSupplyTarget=target;
            bool ready=preparation.TryPrepare(p,source,destination,target.Identity,out currentSlot);
            if(!ready)WaitForDynamicBuild(target);
            return ready;
        }
        private static Item FirstBuildPaint(Player p)
        {
            for(int i=54;i<58;i++){var item=ItemPendingGuards.ReadSelectionSlot(p.inventory,i);if(item.PaintOrCoating && item.stack>0)return item;}
            for(int i=0;i<58;i++){var item=ItemPendingGuards.ReadSelectionSlot(p.inventory,i);if(item.PaintOrCoating && item.stack>0)return item;}
            return null;
        }
        private static int FirstBuildMaterialSlot(Player p,int type)
        {for(int slot=0;slot<58;slot++)if(p.inventory[slot].type==type && p.inventory[slot].stack>0)return slot;return -1;}
        private bool ContinueBuildSupply(Player p)
        {
            var preparation=buildMaterialPreparation;
            if(preparation?.HasWork!=true || preparation.Using || preparation.Unknown)return false;
            if(preparation.Returning)return true;
            var target=buildSupplyTarget;
            if(target==null || !IsBuildTargetCurrent(target) || !CanBuildAutomatically(p))
            {preparation.RequestReturn();return true;}
            BuildFlight flight;
            if(TryPrepareBuild(target,p,out flight))preparedBuild=flight;
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
            ConfigurePreparedBuildBudgets(flight);
            return preparation?.Ready==true && preparation.Blocks(flight.Target.Identity) ? preparation.TryAdoptUse(token,flight.Source.Slot,flight.DependencySlots) :
                buildTools.Items.Ownership.TryBeginUse(buildTools.Runtime.Generation,flight.Source.Slot,token,flight.DependencySlots);
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
        private void ReturnPreparedBuildLease(BuildFlight flight)
        {
            if(buildMaterialPreparation?.Using==true)buildMaterialPreparation.ReturnUse(flight.Token);
        }
    }
}
