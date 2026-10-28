#if JMR_BLUEPRINT_DIAGNOSTIC
using System;
using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.DataStructures;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal static class BlueprintBuildTraceHost
    {
        // This first-world gate intentionally bypasses all business and input
        // permissions. Their closed states are evidence, not recording gates.
        internal static void Outer(HostBlueprint blueprint,HostInputState input,ulong tick)
        {
            var trace=BlueprintBuildTrace.Current;if(trace==null || trace.Finished)return;
            try
            {
                int index=Main.myPlayer;
                var p=Main.player!=null && index>=0 && index<Main.player.Length?Main.player[index]:null;
                var value=new BuildTraceOuter{Frame=input?.Frame??-1,InputState=input?.BlueprintDiagnosticState??0,
                    InputMapping=input?.Mapping??-1,WorldAtInputBegin=input?.BlueprintDiagnosticWorldAtBegin??0,
                    GameUpdateCount=Main.GameUpdateCount,UpdateTick=unchecked((long)tick),WorldId=Main.worldID,NetMode=Main.netMode,PlayerIndex=index,
                    WorldPresent=!Main.gameMenu && p!=null && p.active && Main.tile!=null && Main.maxTilesX>0 && Main.maxTilesY>0,
                    Selected=p?.selectedItem??-1,Direction=p?.direction??0,X=p?.position.X??0,Y=p?.position.Y??0,
                    VelocityX=p?.velocity.X??0,VelocityY=p?.velocity.Y??0,TileX=Player.tileTargetX,TileY=Player.tileTargetY,
                    Animation=p?.itemAnimation??-1,ItemTime=p?.itemTime??-1,Reuse=p?.reuseDelay??-1,Chest=p?.chest??-2,TalkNpc=p?.talkNPC??-2};
                value.PlayerState=(p?.active==true?1:0)|(p?.dead==true?2:0)|(p?.ghost==true?4:0)|(p?.noBuilding==true?8:0)|
                    (p?.mouseInterface==true?16:0)|(p?.controlLeft==true?32:0)|(p?.controlRight==true?64:0)|(p?.controlUseItem==true?128:0)|
                    (p?.releaseUseItem==true?256:0)|(p?.selectedItemState.HasActiveOverride==true?512:0)|(p?.selectedItemState.HasBufferedChange==true?1024:0)|
                    (Main.playerInventory?2048:0)|(Main.gamePaused?4096:0)|(Main.gameMenu?8192:0)|(blueprint?.Shell?.State.Visible==true?16384:0);
                value.UiState=(Main.mapFullscreen?1:0)|(Main.inFancyUI?2:0)|(Main.onlyDrawFancyUI?4:0)|(Main.ingameOptionsWindow?8:0)|
                    (Main.blockInput?16:0)|(Main.drawingPlayerChat?32:0)|(Main.editSign?64:0)|(Main.editChest?128:0)|(PlayerInput.WritingText?256:0)|
                    (Main.CurrentInputTextTakerOverride!=null?512:0)|(Main.mouseItem?.IsAir==true?1024:0)|(Main.npcShop!=0?2048:0);
                trace.Outer(value);
                if(BlueprintBuildTrace.Active)blueprint?.DiagnosticSnapshot();
            }
            catch(Exception error){BlueprintBuildTrace.Fault(error);}
        }
    }
    internal sealed partial class HostBlueprint
    {
        internal void DiagnosticSnapshot()
        {
            if(!BlueprintBuildTrace.Active)return;
            try
            {
                var trace=BlueprintBuildTrace.Current;var pref=preference.Snapshot;
                trace.Blueprint(new BuildTraceBlueprint{Session=generation,State=(pref.Value.AutoBuild?1:0)|(ready?2:0)|(failed?4:0)|(runtime.IsSessionActive?8:0)|
                    (pref.IsLoaded?16:0)|(buildTools?.Available==true?32:0)|(State.Active?64:0)|(HasActiveCaptureWork?128:0)|(ProjectionEditing?256:0)|
                    (pendingProjection!=null?512:0)|(deletePending?1024:0)|(preparedBuild!=null?2048:0)|(activeBuild!=null?4096:0)|(buildContainer!=null?8192:0)|
                    (buildTools?.Use.Active==true?16384:0)|(buildTools?.Use.InNativeUse==true?32768:0)|(buildTools?.Use.Returning==true?65536:0)|
                    ((buildTools?.Use.Intent==null?0:(int)buildTools.Use.Intent.Kind+1)<<24),
                    SceneCount=projectionScene.Count,SceneRevision=projectionSceneRevision,ProjectionMode=(int)ProjectionMode,CaptureStatus=(int)Flow.Status,
                    CatalogRevision=materialPlacementIdentity,InventoryRevision=materialInventoryRevision,EligibilityRevision=MaterialBuildEligibilityRevision,SupplyRevision=buildSupplyRevision,
                    MaterialTick=materialTick,ProjectionMatchTick=projectionMatchTick,SourceCount=buildSources.Count,SourceBudget=materialBuildSourceBudget,SearchBudget=materialBuildSearchBudget,
                    MaterialState=(materialInventory?.Ready==true?1:0)|(materialCatalogTransition?2:0)|(materialContextTransition?4:0)|(materialObservedDynamicDemand?8:0)|
                        (materialAll.Failed?16:0)|(materialAll.Unresolved>0?32:0),
                    SceneRestoreState=(projectionScopeBound?1:0)|(projectionRestoreComplete?2:0)|(projectionHistory.Loaded?4:0)|(projectionHistory.Protected?8:0)|(restoreProjection!=null?16:0)});
                if(!trace.NeedSources)return;
                int slot=0;
                // This is the existing at-most-four retained transport list,
                // not a traversal of scenes, source ranges or world targets.
                for(var node=buildSources.First;node!=null && slot<4;node=node.Next,slot++)
                {
                    var s=node.Value;var r=s.Range;var m=r.Materials;
                    trace.Source(slot,new BuildTraceSource{Instance=s.Owner.Identity.ProjectionGeneration,Range=r.Index,Version=r.Version,Wake=m?.BuildWakeRevision??-1,
                        NearVersion=s.NearVersion,NearWake=s.NearWake,NearPlayer=s.NearPlayer,NearSupply=s.NearSupply,NearCursor=s.NearCursor,ForwardCursor=m?.CandidateCursor??-1,
                        FrontierCursor=s.FrontierCursor,Left=s.NearBounds.Left,Top=s.NearBounds.Top,Width=s.NearBounds.Width,Height=s.NearBounds.Height,
                        Flags=(m?.BuildExhausted==true?1:0)|(m?.Candidate!=null?2:0)|(r.Demand?4:0)|(r.Reading?8:0)|(m?.Progress!=null?16:0)|
                            (m?.Current==true?32:0)|(r.Current?64:0)|(m?.BuildRetryPending==true?128:0)|(m?.EligibilityRetry==true?256:0)|(s.Frontier?512:0),
                        RetryOrdinal=m?.EligibilityRetryOrdinal??-1,RetryAt=m?.EligibilityRetryAt??0,MaterialTick=materialTick,
                        ResetBefore=s.DiagnosticResetBefore,ResetAfter=s.DiagnosticResetAfter,ResetReasons=s.DiagnosticResetReasons},buildSources.Count);
                }
                if(slot==0)trace.EmptySources();
            }
            catch(Exception error){BlueprintBuildTrace.Fault(error);}
        }
        private int DiagnosticTarget(BlueprintBuildTarget target,BuildTraceStage stage,BuildTraceReason reason,BuildFlight flight=null,TileObject? choice=null,int? preflightDirection=null,int canPlaceResult=-1)
        {
            if(!BlueprintBuildTrace.Active || target==null)return -1;
            try
            {
                var p=flight?.Player??player;var source=flight?.Source;var placement=target.Placement;
                return BlueprintBuildTrace.Current.Target(new BuildTraceTarget{Session=target.Identity.SessionGeneration,Instance=target.Identity.ProjectionGeneration,
                    Range=target.Range.Index,Ordinal=target.Ordinal,RangeVersion=target.RangeStamp,SupportStamp=target.SupportStamp,Catalog=target.PlacementRevision,
                    OriginX=target.OriginX,OriginY=target.OriginY,X=target.WorldX,Y=target.WorldY,Left=target.Bounds.Left,Top=target.Bounds.Top,Width=target.Bounds.Width,Height=target.Bounds.Height,
                    Kind=placement==null?-1:(int)placement.Kind,TileType=placement?.TileType??-1,WallType=placement?.WallType??-1,ItemType=placement?.UseItemType??-1,
                    Style=placement?.Style??-1,Alternate=placement?.Alternate??-1,ExpectedDirection=preflightDirection??flight?.Direction??placement?.Direction??0,
                    SourceSlot=source?.Slot??-1,SourceType=source?.Type??-1,StackBefore=source?.Stack??-1,StackAfter=source?.Item?.stack??-1,TileBoost=source?.Item?.tileBoost??-1,
                    ActualDirection=p?.direction??0,ActualX=Player.tileTargetX,ActualY=Player.tileTargetY,Animation=p?.itemAnimation??-1,ItemTime=p?.itemTime??-1,Reuse=p?.reuseDelay??-1,
                    Flags=(target.LocalProof?1:0)|(target.LocalPriority?2:0)|(flight?.Started==true?4:0)|(flight?.Entered==true?8:0)|(flight?.Chosen==true?16:0)|(flight?.Checked==true?32:0)|(flight?.Rejected==true?64:0),
                    Flight=flight?.Generation??0,Token=flight?.Token??0,NativeGeneration=buildTools?.Use.NativeGeneration??0,Site=(int)stage,Reason=(int)reason,
                    RangeX=Player.tileRangeX,RangeY=Player.tileRangeY,BlockRange=p?.blockRange??-1,Pulley=p?.pulley==true?1:0,PulleyDirection=p?.pulleyDir??-1,
                    ChoiceAt=choice.HasValue?0:-1,ChoiceType=choice?.type??-1,ChoiceStyle=choice?.style??-1,ChoiceAlternate=choice?.alternate??-1,ChoiceRandom=choice?.random??-1,ChoiceX=choice?.xCoord??-1,ChoiceY=choice?.yCoord??-1,
                    CanPlaceResult=canPlaceResult,CanPlaceAt=-1});
            }
            catch(Exception error){BlueprintBuildTrace.Fault(error);return -1;}
        }
        private bool DiagnosticReject(BlueprintBuildTarget target,BuildTraceStage stage,BuildTraceReason reason)
        {BlueprintBuildTrace.Count(reason);DiagnosticTarget(target,stage,reason);return false;}
        private bool DiagnosticMaterialEligible(int type,bool eligible)
        {
            if(BlueprintBuildTrace.Active){BlueprintBuildTrace.Count(eligible?BuildTraceReason.SourceEligible:BuildTraceReason.SourceFiltered);
                if(!eligible)BlueprintBuildTrace.Current.Supply(type,false,buildSupplyRevision,false);
                BlueprintBuildTrace.Current.PrepareMaterial(type,eligible);}
            return eligible;
        }
        private void DiagnosticFlight(BuildFlight flight,BuildTraceStage stage,BuildTraceReason reason)
        {
            if(!BlueprintBuildTrace.Active || flight==null)return;
            int detail=DiagnosticTarget(flight.Target,stage,reason,flight);
            BlueprintBuildTrace.Semantic(stage,reason,flight.Source.Stack,flight.Source.Item.stack,flight.Direction,flight.Player.direction,
                flight.Generation,flight.Token,buildTools.Use.NativeGeneration,detail);
        }
        private void DiagnosticSupply(int type,bool supplied)
        {
            if(!BlueprintBuildTrace.Active)return;
            BlueprintBuildTrace.Count(supplied?BuildTraceReason.SupplyPresent:BuildTraceReason.SupplyMissing);
            BlueprintBuildTrace.Current.Supply(type,supplied,buildSupplyRevision);
        }
        private void DiagnosticObjectChoice(BlueprintBuildTarget target,int direction,bool canPlace,TileObject choice)
        {
            if(!BlueprintBuildTrace.Active)return;
            int detail=DiagnosticTarget(target,BuildTraceStage.ObjectPreflight,canPlace?BuildTraceReason.ObjectCanPlace:BuildTraceReason.GeometryRejected,
                choice:canPlace?(TileObject?)choice:null,preflightDirection:direction,canPlaceResult:canPlace?1:0);
            if(canPlace)BlueprintBuildTrace.Semantic(BuildTraceStage.ObjectPreflight,BuildTraceReason.ObjectCanPlace,1,direction,choice.xCoord,choice.yCoord,target:detail);
            else BlueprintBuildTrace.Count(BuildTraceReason.ObjectCanPlace);
        }
    }
}
#endif
