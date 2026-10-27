using JueMingR.Features.Blueprint;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        private BlueprintConstructionSelection SelectProjectionTarget(BlueprintProjectionPlacement item,CaptureCell cell,CaptureObject? obj=null)
        {
            var catalog=MaterialCatalog;
            if(catalog==null || item.Summary.TargetVersion!=catalog.TargetVersion)
            {
                ushort bits=BlueprintConstructionTargets.Layers(cell);
                return new BlueprintConstructionSelection(bits,bits,catalog?.Revision??0);
            }
            return catalog.Select(cell,obj);
        }
        private bool ProjectionQualificationCurrent(BlueprintProjectionPlacement item)
        {
            var value=item?.Qualification;var catalog=MaterialCatalog;
            return value!=null && value.Error==null && catalog!=null && value.Identity.Equals(item.Identity) &&
                ReferenceEquals(value.Mask,item.Mask) && ReferenceEquals(value.Catalog,catalog) && value.CapabilityRevision==catalog.Revision &&
                value.ClassifierVersion==BlueprintPlacementCatalog.ClassifierVersion;
        }
        private void EnsureProjectionQualification(BlueprintProjectionPlacement item)
        {
            if(!item.Opened || item.Summary==null || ProjectionQualificationCurrent(item))return;
            if(item.Match!=null)ReleaseProjectionMatch(item);
            var catalog=MaterialCatalog;if(catalog==null || Library==null)return;
            if(ReferenceEquals(item.QualificationRequestedMask,item.Mask) && item.QualificationRequestedRevision==catalog.Revision)return;
            // Display Open and its immutable raw geometry do not wait for this
            // bounded derived pass. Unknown qualification is never final zero.
            if(Library.RequestProjectionQualification(item.Identity,catalog,item.Mask))
            {item.QualificationRequestedMask=item.Mask;item.QualificationRequestedRevision=catalog.Revision;}
        }
        private void TakeProjectionQualifications()
        {
            BlueprintProjectionQualificationReceipt receipt;
            while(Library.TryTakeProjectionQualification(out receipt))
            {
                if(receipt.Identity.SessionGeneration!=generation || receipt.CapabilityRevision!=MaterialCatalog?.Revision ||
                    !ReferenceEquals(receipt.Catalog,MaterialCatalog))continue;
                foreach(var item in projectionScene)AcceptProjectionQualification(item,receipt);
                if(restoreProjection!=null && !projectionScene.Contains(restoreProjection))AcceptProjectionQualification(restoreProjection,receipt);
                if(candidateProjection!=null)AcceptProjectionQualification(candidateProjection,receipt);
            }
        }
        private void AcceptProjectionQualification(BlueprintProjectionPlacement item,BlueprintProjectionQualificationReceipt receipt)
        {
            if(!item.Identity.Equals(receipt.Identity) || !ReferenceEquals(item.Mask,receipt.Mask) ||
                !ReferenceEquals(item.QualificationRequestedMask,receipt.Mask) || item.QualificationRequestedRevision!=receipt.CapabilityRevision)return;
            if(receipt.Error!=null)
            {
                // Preserve display and raw source on a real qualification read
                // failure. No target can be built, moved or retired from it.
                item.Qualification=receipt;projectionFeedback="这份蓝图的施工内容暂未能确认，原图仍保留。";return;
            }
            // A first qualification publishes no old Match to retire. Keep a
            // real Checking click's scheduling intent across this cold receipt;
            // actual identity/mask/catalog revocation still uses Release below.
            if(item.Match!=null)ReleaseProjectionMatch(item);
            item.Qualification=receipt;item.MatchRanges=receipt.MatchRanges;
            item.TotalConstructionLayerCount=receipt.TotalRequiredLayerCount;
        }
        private void RevokeProjectionQualifications(bool firstCatalog)
        {
            CancelAutomaticBuild();
            foreach(var item in projectionScene)
            {
                // Initial producer publication has no old qualification/proof
                // to revoke. Preserve that same owner's pending Checking intent.
                // Every real replacement and every existing proof still takes
                // the ordinary release path, including priority cancellation.
                if(!firstCatalog || item.Match!=null || item.Qualification!=null)ReleaseProjectionMatch(item);
                item.Qualification=null;item.QualificationRequestedMask=null;item.QualificationRequestedRevision=-1;
            }
        }
    }
}
