using System;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeTargetPlacementBudgetChecks
    {
        internal static void Run(Type type)
        {
            foreach(bool directoryFirst in new[]{false,true})
            {
                var catalog=Activator.CreateInstance(type,true);long token=0;
                Func<long> reads=()=>(long)Get(catalog,"ItemReads")+(long)Get(catalog,"PlacementReads");
                try
                {
                    Call(catalog,"BindPlacementClock",(Func<long>)(()=>token));
                    Call(catalog,"StepPlacement");Call(catalog,"Step",true);
                    Require(reads()==0,"mapping zero is not a metadata work opportunity");
                    token=1;
                    if(directoryFirst){Call(catalog,"Step",true);Call(catalog,"StepPlacement");}
                    else{Call(catalog,"StepPlacement");Call(catalog,"Step",true);}
                    long first=reads();Require(first>0&&first<=64,"directory and placement producers share one allowance in either order");
                    Call(catalog,"Reset");Call(catalog,"StepPlacement");Call(catalog,"Step",true);
                    Require(reads()==first,"Reset does not refund an already consumed mapping allowance");
                    token=1000;Call(catalog,"StepPlacement");Require(reads()-first<=64,"clock jump grants one batch, not the numerical delta");
                    long next=reads();token=2;Call(catalog,"StepPlacement");Require(reads()-next<=64,"clock rollback grants at most one new batch");
                    uint game=Main.GameUpdateCount;
                    for(int i=0;i<110&&!(bool)Get(catalog,"PlacementReady");i++){token++;Call(catalog,"StepPlacement");}
                    Require(Main.GameUpdateCount==game&&(bool)Get(catalog,"PlacementReady"),"mapping opportunities finish placement while native world counter is paused");
                    Require(GetOptional(catalog,"MaterialPlacement")!=null&&GetOptional(catalog,"Catalog")==null,
                        "placement-only readiness publishes shared blueprint metadata without falsely publishing a full item directory");
                    object published=Get(catalog,"MaterialPlacement");long revision=(long)Get(catalog,"PlacementRevision");
                    Call(catalog,"StepPlacement");Require(ReferenceEquals(Get(catalog,"MaterialPlacement"),published)&&(long)Get(catalog,"PlacementRevision")==revision,
                        "finished metadata is reused with the same publication identity and version");
                    Call(catalog,"Reset");Require(GetOptional(catalog,"MaterialPlacement")==null&&(long)Get(catalog,"PlacementRevision")>revision,
                        "Reset invalidates the shared publication and advances its revision");
                }
                finally{((IDisposable)catalog).Dispose();}
            }
            Console.WriteLine("PASS shared placement producers: order-independent mapping budget, reset/jump/rollback, paused-world completion and shared publication identity/version.");
        }
    }
}
