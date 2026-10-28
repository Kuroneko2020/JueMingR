using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Localization;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Hold only Host acceptance of a real worker qualification. All identities,
    // receipts, Match/source work and move eligibility remain production values.
    internal static class NativeBlueprintQualificationPriorityChecks
    {
        private const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static object heldHost;
        private static object watchedTarget;
        private static bool held;
        private static bool TakeQualification(object __instance){return !held || !ReferenceEquals(__instance,heldHost);}
        private static void ObserveRelease(object __instance,object item)
        {
            if(!ReferenceEquals(__instance,heldHost) || !ReferenceEquals(item,watchedTarget) || !ReferenceEquals(GetOptional(__instance,"projectionMatchPriority"),item))return;
            var catalog=GetOptional(__instance,"MaterialCatalog") as BlueprintPlacementCatalog;
            Console.WriteLine("COLD POINT actual priority release hasMatch="+(GetOptional(item,"Match")!=null)+" hasQualification="+(GetOptional(item,"Qualification")!=null)+
                " materialCatalogRevision="+catalog?.Revision+" appliedRevision="+Get(__instance,"materialPlacementIdentity")+" requestedRevision="+Get(item,"QualificationRequestedRevision")+" stack="+new System.Diagnostics.StackTrace(false));
        }
        private static bool ReceiptReady(LibraryState library,object item)
        {
            var worker=Get(library,"worker");
            lock(Get(worker,"gate"))return ((IEnumerable)Get(worker,"projectionQualificationReceipts")).Cast<BlueprintProjectionQualificationReceipt>()
                .Any(receipt=>receipt.Identity.Equals(Get(item,"Identity")));
        }
        internal static void Run(object context,object host,object input,LibraryState library)
        {
            var timing=new Harmony("JueMingR.Tests.Blueprint.ColdPointPriority");
            heldHost=host;held=true;
            string language=Language.ActiveCulture.Name;
            Action step=()=>{NativeQuickItemChecks.BeginWorldStep();Call(context,"UpdateRuntime");};
            using(NativeBlueprintWorldMatchChecks.EmptyWorld(host,200,160))
            try
            {
                Call(host,"ClearProjection");NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());
                timing.Patch(host.GetType().GetMethod("TakeProjectionQualifications",F),prefix:new HarmonyMethod(typeof(NativeBlueprintQualificationPriorityChecks),nameof(TakeQualification)));
                timing.Patch(host.GetType().GetMethod("ReleaseProjectionMatch",F),prefix:new HarmonyMethod(typeof(NativeBlueprintQualificationPriorityChecks),nameof(ObserveRelease)));
                var entry=NativeBlueprintBudgetChecks.Save(library,new[]{new CaptureCell{Layers=CaptureLayer.Tile,TileType=1}},1,1);
                var item=NativeBlueprintWorldMatchChecks.Place(host,input,entry,20,50,step);
                watchedTarget=item;
                bool boundAtCommit=ReferenceEquals(GetOptional(host,"projectionMatchTiles"),Main.tile);
                // Place may become Fixed after this Update's match phase. Let
                // the real world replacement bind before the one valid click;
                // its initial invalidation must retain its original clear rule.
                step();
                Require(ReferenceEquals(GetOptional(host,"projectionMatchTiles"),Main.tile) && ReferenceEquals(GetOptional(host,"projectionMatchSections"),Main.sectionManager),
                    "the one-shot Checking request starts only after the actual world array/section binding is current");
                Console.WriteLine("COLD POINT worldBoundAtCommit="+boundAtCommit+" worldBoundBeforeOnlyClick=true");
                var initialCatalog=GetOptional(host,"MaterialCatalog") as BlueprintPlacementCatalog;
                Console.WriteLine("COLD POINT before only Checking catalogRevision="+(initialCatalog==null?"null":initialCatalog.Revision.ToString())+" appliedRevision="+Get(host,"materialPlacementIdentity"));
                Require(!(bool)Call(host,"ProjectionQualificationCurrent",item) && GetOptional(item,"Match")==null,"real first click begins before qualification and Match publication");
                Require(!(bool)Call(host,"CheckProjectionMove",item) && ReferenceEquals(GetOptional(host,"projectionMatchPriority"),item),"one actual Checking query owns explicit priority while qualification is pending");
                NativeQuickItemChecks.Until(()=>{step();return ReceiptReady(library,item);});
                Console.WriteLine("COLD POINT before accepting queued real receipt samePriority="+ReferenceEquals(GetOptional(host,"projectionMatchPriority"),item)+" requestedRevision="+Get(item,"QualificationRequestedRevision"));
                held=false;step();
                Console.WriteLine("COLD POINT acceptedQualification="+Call(host,"ProjectionQualificationCurrent",item)+" hasMatch="+(GetOptional(item,"Match")!=null)+" samePriority="+ReferenceEquals(GetOptional(host,"projectionMatchPriority"),item));
                Require((bool)Call(host,"ProjectionQualificationCurrent",item) && GetOptional(item,"Match")!=null && ReferenceEquals(GetOptional(host,"projectionMatchPriority"),item),
                    "same identity/origin/mask keeps the original Checking priority across real qualification receipt and first Match creation");
                int updates=1;
                NativeQuickItemChecks.Until(()=>{if((long)Get(Get(item,"Match"),"SourceRequests")>0)return true;step();updates++;return false;});
                Require(updates<=4,"a cold point obtains a real source within one finite free-slot admission round after qualification");
                NativeQuickItemChecks.Until(()=>{step();return Call(host,"ProjectionMoveEligibility",item).ToString()=="Movable";});
                var old=(BlueprintProjectionQualificationReceipt)Get(item,"Qualification");
                LanguageManager.Instance.SetLanguage(language=="zh-Hans"?"en-US":"zh-Hans");Lang.InitializeLegacyLocalization();step();
                Require(GetOptional(host,"MaterialCatalog")==null && GetOptional(item,"Match")==null && GetOptional(host,"projectionMatchPriority")==null,
                    "a real replacement of a published native catalog still withdraws the old proof and explicit priority");
                NativeQuickItemChecks.Until(()=>{step();return (bool)Call(host,"ProjectionQualificationCurrent",item) && GetOptional(item,"Match")!=null;});
                var replacement=(BlueprintProjectionQualificationReceipt)Get(item,"Qualification");var replacementMatch=Get(item,"Match");
                Require(replacement.CapabilityRevision>old.CapabilityRevision && GetOptional(host,"projectionMatchPriority")==null,"new directory publication cannot revive an intent revoked with the old directory");
                Call(host,"AcceptProjectionQualification",item,old);
                Require(ReferenceEquals(Get(item,"Qualification"),replacement) && ReferenceEquals(Get(item,"Match"),replacementMatch) && GetOptional(host,"projectionMatchPriority")==null,
                    "the actual late old-directory receipt cannot restore its proof or priority");
                Call(host,"ClearProjection");
                Require(GetOptional(host,"projectionMatchPriority")==null && GetOptional(item,"Match")==null,"qualified scene cancellation releases its new proof and leaves no priority after the earlier catalog replacement");

                held=true;
                var cancelled=NativeBlueprintWorldMatchChecks.Place(host,input,entry,30,50,step);
                Require(!(bool)Call(host,"CheckProjectionMove",cancelled) && ReferenceEquals(GetOptional(host,"projectionMatchPriority"),cancelled),"second real pending owner has its own one-shot Checking request");
                NativeQuickItemChecks.Until(()=>{step();return ReceiptReady(library,cancelled);});
                Call(host,"ClearProjection");held=false;for(int i=0;i<4;i++)step();
                Require(GetOptional(host,"projectionMatchPriority")==null && GetOptional(cancelled,"Match")==null &&
                    ((IEnumerable)Get(host,"ProjectionScene")).Cast<object>().Count()==0 && (int)Get(host,"ProjectionMatchWorkCount")==0,
                    "true pending identity exit releases priority; its actual late qualification cannot recreate Match or work");
                Console.WriteLine("PASS Blueprint qualification-wait point: directoryInitiallyReady="+(initialCatalog!=null)+" actual Checking calls=1 per identity, real receipt/Match preserved priority, first accepted source Update="+updates+"; real catalog replacement clears live priority, qualified Clear releases proof, pending cancellation clears live priority; actual late receipts cannot revive; no cursor/proof injection");
            }
            finally{held=false;timing.UnpatchAll(timing.Id);Call(host,"ClearProjection");heldHost=watchedTarget=null;
                if(Language.ActiveCulture.Name!=language){LanguageManager.Instance.SetLanguage(language);Lang.InitializeLegacyLocalization();}}
        }
    }
}
