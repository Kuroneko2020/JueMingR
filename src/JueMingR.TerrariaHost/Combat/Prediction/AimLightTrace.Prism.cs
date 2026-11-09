#if JMR_AIM_LIGHT
using System;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    internal static partial class AimLightTrace
    {
        // Temporary, explicitly hash-armed consumer capture. These gates carry
        // actual short-circuit results; a missing bit means NOT EVALUATED.
        internal enum PrismGate
        {
            Registry,ProjectileReference,ShotActive,ShotOwner,ShotKey,ShotType,
            Source,Player,PlayerActive,PlayerAlive,SessionActive,SourcePlayer,Session,Selection,Slot,WeaponReference,WeaponType,WeaponPrefix,Demand,Aim,
            Available,CombatPlayer,CombatSession,Fresh,Interface,Gameplay,PlayerUsable,InventoryUnlocked,NotFilm,WorldFree,NativeUiFree,ContainersFree,CursorFree,
            SelectionAvailable,SettingsReady,AimWanted,BasicPlayer,Intent,HasTarget,TargetIdentity,TargetReceives,ConsumerPlayer,ConsumerItem,ConsumerWeapon,Eligible,Consumes,Point,Scope,
            ShellTarget,ShellPointer,ShellMouse,ShellFailed,Layers,ShellSession,PresentationRuntime,Presentation,Gesture,Adjustment,NativeWindows,
            NativeWorld,NativePlayer,Focus,InputMode,Map,HideUi,Fancy,Options,Capture,TextOwner,Journey,MouseOwner,
            SampleFrame,InputStart,NotPaused,CanUpdate,Alive,NotGhost,NotCced,NotCursed,ItemsAllowed,ItemWorldIdle,NoManualOperation,NotBlocked,NotChat,NotSign,NotChest,NotWriting,
            ChestClosed,NoNpcTalk,NoSignEdit,NoShop,CursorExists,CursorEmpty,NotMenu,Client,NetworkMode,NotGamepad,NotFastUse,NotFancyDraw,NotFancyUi,CaptureExists,CaptureInactive,
            NoTextTaker,NoNpcChat,NoClothes,NoHair,NoInfoAdjustment,NoPendingAdjustment,KnownTextOwner
        }
        private struct PrismRow
        {
            internal uint Tick;internal long Frame;internal int Key,Owner,Slot,Weapon,Prefix,Target,TargetGeneration,TargetType,TargetNetId,SourceSlot,SourceType,SourcePrefix;
            internal long Session,Selection,SourceSession,SourceSelection;internal ulong Seen,Passed,SeenHigh,PassedHigh;
            internal string Reject,Outcome,Error;internal bool Tool,Native,Scope,PointKnown,ExpectedKnown,Returned;
            internal int PhysicalX,PhysicalY,RawX,RawY,BorrowedX,BorrowedY,NativeX,NativeY,ReturnedX,ReturnedY,Input,Ui,Availability;
            internal Vector2 Screen,Origin,Point,Before,After,Expected;internal float Gravity,Charge,Rotation,AfterRotation,SteeringError;
        }
        private static readonly PrismRow[] prismRows=new PrismRow[192];
        private static PrismRow prismRow,prismPrevious;
        private static bool prismPending,prismTriggered,prismFinished,prismPointActive;private static int prismAncillary;
        private static HostAttackControl.Source prismConsumerSource;
        private static Projectile prismSubject;private static int prismKey,prismCount,prismToolCalls,prismNativeCalls,prismPointCalls,prismBirths;
        private static long prismStarted;private static uint prismLastStored;private static string prismSetupFailure;
        internal static void PrismSetupFailed(Exception error){try{prismSetupFailure=error.GetType().FullName;}catch{prismSetupFailure="diagnostic-hook-failure";}}
        internal static bool PrismOn=>log!=null && log.PrismOnly && log.Accepting && !prismFinished;
        internal static bool PrismConsuming=>PrismOn && prismPending && prismRow.Tool && prismPointActive && prismAncillary==0;
        internal static bool PrismTracked(Projectile shot)=>ReferenceEquals(shot,prismSubject) && (int)shot.key==prismKey;
        internal static bool PrismSourceMatches(HostAttackControl.Source source)=>ReferenceEquals(source,prismConsumerSource);
        internal static void PrismBeginPoint(){if(PrismOn && prismPending){prismPointActive=true;prismConsumerSource=null;}}
        internal static void PrismEndPoint(){prismPointActive=false;prismConsumerSource=null;}
        internal static void PrismSuspend(){prismAncillary++;}
        internal static void PrismResume(){prismAncillary--;}
        private static bool Subject(Projectile shot)
        {
            if(!PrismOn || shot==null || shot.type!=633 || shot.owner!=Main.myPlayer)return false;
            if(!prismTriggered)
            {prismTriggered=true;prismSubject=shot;prismKey=(int)shot.key;prismStarted=Stopwatch.GetTimestamp();}
            return ReferenceEquals(prismSubject,shot) && (int)shot.key==prismKey;
        }
        internal static void PrismBirth(Player player,Projectile shot)
        {try{if(ReferenceEquals(player,Main.LocalPlayer) && Subject(shot))prismBirths++;}catch(Exception error){Broken(error);}}
        private static void NewRow(HostCombat combat,Projectile shot)
        {
            var player=Main.LocalPlayer;var input=combat?.Tools.Input;var observation=combat?.Attack?.PrismObservation;
            var target=observation!=null && observation.Selection.HasTarget?observation.Selection.Target:default(JueMingR.Platform.Combat.NpcIdentity);
            prismRow=new PrismRow{Tick=Main.GameUpdateCount,Key=(int)shot.key,Owner=shot.owner,Frame=input?.Frame??-1,Session=observation?.Session??-1,
                Selection=combat?.Tools.SelectionIntent??-1,Slot=player?.selectedItem??-1,Weapon=player?.HeldItem.type??0,Prefix=player?.HeldItem.prefix??0,
                Target=observation!=null && observation.Selection.HasTarget?target.Slot:-1,TargetGeneration=target.Generation,TargetType=target.Type,TargetNetId=target.NetId,
                PhysicalX=Main.mouseX,PhysicalY=Main.mouseY,RawX=input?.PhysicalMapX??-1,RawY=input?.PhysicalMapY??-1,Screen=Main.screenPosition,Gravity=player?.gravDir??0,Charge=shot.ai[0],Rotation=shot.rotation,Before=shot.velocity,
                Origin=player==null?Vector2.Zero:player.RotatedRelativePoint(player.MountedCenter)};
            prismRow.Input=(input?.IsFocused==true?1:0)|(input?.CanStartActions==true?2:0)|(input?.CanRetainIntent==true?4:0)|(input?.HotkeyCapture==true?8:0)|
                (input?.RawMouseLeft==true?16:0)|(combat?.Left==true?32:0)|(player?.controlUseItem==true?64:0)|(player?.channel==true?128:0)|
                (PlayerInput.Triggers.Current.MouseLeft?256:0)|(input?.ExternalModalBusy==true?512:0)|(input?.MapPointerOwned==true?1024:0);
            prismRow.Ui=(Main.hideUI?1:0)|(Main.mapFullscreen?2:0)|(Main.gamePaused?4:0)|(Main.blockInput?8:0)|(Main.drawingPlayerChat?16:0)|
                (Main.editSign?32:0)|(Main.editChest?64:0)|(PlayerInput.WritingText?128:0)|(player?.mouseInterface==true?256:0)|(Main.onlyDrawFancyUI?512:0)|
                (Main.ingameOptionsWindow?1024:0)|(Main.inFancyUI?2048:0)|(Main.CurrentInputTextTakerOverride!=null?4096:0);
            prismRow.Availability=(combat?.Tools.Available==true?1:0)|(combat?.Tools.Items.Available==true?2:0)|(combat?.Available==true?4:0)|(observation?.Hooks.Ready==true?8:0);
            prismPending=true;
        }
        internal static bool PrismToolBefore(HostCombat combat,Projectile shot)
        {
            try
            {
                if(!Subject(shot))return false;
                if(prismPending)Store("interrupted-before-next-tool");
                NewRow(combat,shot);prismRow.Tool=true;prismToolCalls++;return true;
            }
            catch(Exception error){Broken(error);return false;}
        }
        internal static bool Gate(PrismGate gate,bool accepted)
        {
            if(!PrismConsuming)return accepted;
            try
            {
                int bit=(int)gate;
                if(bit<64){prismRow.Seen|=1UL<<bit;if(accepted)prismRow.Passed|=1UL<<bit;else prismRow.Passed&=~(1UL<<bit);}
                else{prismRow.SeenHigh|=1UL<<(bit-64);if(accepted)prismRow.PassedHigh|=1UL<<(bit-64);else prismRow.PassedHigh&=~(1UL<<(bit-64));}
                if(!accepted && prismRow.Reject==null)prismRow.Reject=gate.ToString();
            }
            catch(Exception error){Broken(error);}
            return accepted;
        }
        internal static void PrismSource(HostAttackControl.Source source)
        {
            if(!PrismConsuming)return;
            prismConsumerSource=source;
            if(source==null)return;
            prismRow.SourceSession=source.Session;prismRow.SourceSelection=source.Selection;prismRow.SourceSlot=source.Slot;prismRow.SourceType=source.Type;prismRow.SourcePrefix=source.Prefix;
        }
        internal static void PrismPointCheck(HostCombat combat)
        {
            if(!PrismConsuming)return;
            try
            {
                prismPointCalls++;var player=combat.Player;var observation=combat.Attack.PrismObservation;var input=combat.Tools.Input;
                var target=observation.Selection.HasTarget?observation.Selection.Target:default(JueMingR.Platform.Combat.NpcIdentity);
                prismRow.Frame=input.Frame;prismRow.Session=observation.Session;prismRow.Selection=combat.Tools.SelectionIntent;
                prismRow.Slot=player?.selectedItem??-1;prismRow.Weapon=player?.HeldItem.type??0;prismRow.Prefix=player?.HeldItem.prefix??0;
                prismRow.Target=observation.Selection.HasTarget?target.Slot:-1;prismRow.TargetGeneration=target.Generation;prismRow.TargetType=target.Type;prismRow.TargetNetId=target.NetId;
                prismRow.Input=(input.IsFocused?1:0)|(input.CanStartActions?2:0)|(input.CanRetainIntent?4:0)|(input.HotkeyCapture?8:0)|
                    (input.RawMouseLeft?16:0)|(combat.Left?32:0)|(player?.controlUseItem==true?64:0)|(player?.channel==true?128:0)|
                    (PlayerInput.Triggers.Current.MouseLeft?256:0)|(input.ExternalModalBusy?512:0)|(input.MapPointerOwned?1024:0);
                prismRow.Ui=(Main.hideUI?1:0)|(Main.mapFullscreen?2:0)|(Main.gamePaused?4:0)|(Main.blockInput?8:0)|(Main.drawingPlayerChat?16:0)|
                    (Main.editSign?32:0)|(Main.editChest?64:0)|(PlayerInput.WritingText?128:0)|(player?.mouseInterface==true?256:0)|(Main.onlyDrawFancyUI?512:0)|
                    (Main.ingameOptionsWindow?1024:0)|(Main.inFancyUI?2048:0)|(Main.CurrentInputTextTakerOverride!=null?4096:0);
            }
            catch(Exception error){Broken(error);}
        }
        internal static bool PrismPoint(bool available,Vector2 point,string kind)
        {if(PrismConsuming){prismRow.PointKnown=available;prismRow.Point=point;prismRow.Outcome=kind;Gate(PrismGate.Point,available);}return available;}
        internal static void PrismBorrowed(CombatCursorScope scope)
        {if(PrismOn && prismPending && prismRow.Tool){prismRow.Scope=scope!=null;prismRow.BorrowedX=Main.mouseX;prismRow.BorrowedY=Main.mouseY;if(scope==null && prismRow.PointKnown)prismRow.Reject="Scope";}}
        internal static void PrismNativeBefore(HostCombat combat,Projectile shot)
        {
            try
            {
                if(!Subject(shot))return;
                if(!prismPending)NewRow(combat,shot);
                prismNativeCalls++;prismRow.Native=true;prismRow.NativeX=Main.mouseX;prismRow.NativeY=Main.mouseY;
                prismRow.Before=shot.velocity;prismRow.Rotation=shot.rotation;
                var player=Main.LocalPlayer;prismRow.Screen=Main.screenPosition;prismRow.Gravity=player.gravDir;
                prismRow.Origin=player.RotatedRelativePoint(player.MountedCenter);
                var delta=Main.screenPosition+new Vector2(Main.mouseX,Main.mouseY)-prismRow.Origin;
                if(player.gravDir==-1)delta.Y=Main.screenHeight-Main.mouseY+Main.screenPosition.Y-prismRow.Origin.Y;
                Vector2 desired=Vector2.Normalize(delta);if(float.IsNaN(desired.X) || float.IsNaN(desired.Y))desired=-Vector2.UnitY;
                prismRow.Expected=Vector2.Normalize(Vector2.Lerp(desired,Vector2.Normalize(shot.velocity),.92f))*player.HeldItem.shootSpeed*shot.scale;
                prismRow.ExpectedKnown=!float.IsNaN(prismRow.Expected.X) && !float.IsNaN(prismRow.Expected.Y);
            }
            catch(Exception error){Broken(error);}
        }
        internal static void PrismNativeAfter(Projectile shot,Exception error)
        {
            try
            {
                if(!PrismOn || !prismPending || !ReferenceEquals(shot,prismSubject))return;
                prismRow.After=shot.velocity;prismRow.AfterRotation=shot.rotation;prismRow.Error=error?.GetType().FullName;
                if(prismRow.ExpectedKnown)prismRow.SteeringError=Vector2.DistanceSquared(prismRow.Expected,shot.velocity);
                if(!prismRow.Tool)Store("native-consumer-without-tool-prefix");
            }
            catch(Exception captureError){Broken(captureError);}
        }
        internal static void PrismToolAfter(Exception error)
        {
            try
            {
                if(!PrismOn || !prismPending || !prismRow.Tool)return;
                prismRow.Returned=true;prismRow.ReturnedX=Main.mouseX;prismRow.ReturnedY=Main.mouseY;
                if(error!=null)prismRow.Error=error.GetType().FullName;
                Store(prismRow.Native?"actual-ai-completed":"tool-prefix-without-native-consumer");
            }
            catch(Exception captureError){Broken(captureError);}
        }
        private static void Store(string arrival)
        {
            if(!prismPending)return;prismPending=false;
            if(prismRow.Outcome==null)prismRow.Outcome=arrival;else prismRow.Outcome+="/"+arrival;
            bool change=prismCount<8 || prismRow.Seen!=prismPrevious.Seen || prismRow.Passed!=prismPrevious.Passed || prismRow.SeenHigh!=prismPrevious.SeenHigh || prismRow.PassedHigh!=prismPrevious.PassedHigh ||
                prismRow.Reject!=prismPrevious.Reject || prismRow.Outcome!=prismPrevious.Outcome || prismRow.Input!=prismPrevious.Input || prismRow.Ui!=prismPrevious.Ui || prismRow.Target!=prismPrevious.Target || prismRow.SteeringError>.001f || prismRow.Error!=null;
            if(change || Main.GameUpdateCount-prismLastStored>=10)
            {
                prismRows[prismCount++]=prismRow;prismPrevious=prismRow;prismLastStored=Main.GameUpdateCount;
                if(prismCount==prismRows.Length)PrismFinish("192-rows");
            }
            if(prismNativeCalls>=1500)PrismFinish("1500-native-ai");
        }
        internal static void PrismPoll(bool session)
        {
            try
            {
                if(!PrismOn || !prismTriggered)return;
                if(!session)PrismFinish("session-ended");
                else if(!prismSubject.active || (int)prismSubject.key!=prismKey)PrismFinish("native-parent-retired");
                else if(Stopwatch.GetTimestamp()-prismStarted>=120*Stopwatch.Frequency)PrismFinish("120-seconds-after-trigger");
            }
            catch(Exception error){Broken(error);}
        }
        private static string V(Vector2 v)=>v.X.ToString("R",CultureInfo.InvariantCulture)+","+v.Y.ToString("R",CultureInfo.InvariantCulture);
        private static void PrismFinish(string reason)
        {
            if(!PrismOn)return;
            if(prismPending)Store("unfinished-at-stop");
            if(prismFinished)return;prismFinished=true;
            try
            {
                log.Offer("prism-summary",prismTriggered?(long)Main.GameUpdateCount:-1,"reason="+reason+";triggered="+prismTriggered+";observedBirths="+prismBirths+";toolPrefixes="+prismToolCalls+";nativeAi075="+prismNativeCalls+";pointChecks="+prismPointCalls+";rows="+prismCount+";subjectKey="+prismKey+";diagnosticHookFailure="+(prismSetupFailure??"none")+";missingGate=not-evaluated;phase=actual-consumer;noTrigger=not-captured");
                log.Offer("prism-gates",-1,string.Join(",",Enum.GetNames(typeof(PrismGate))));
                log.Offer("prism-masks",-1,"input:focused=1,canStart=2,retain=4,hotkeyCapture=8,rawLeft=16,combatLeft=32,useItem=64,channel=128,mappedLeft=256,externalModal=512,mapPointer=1024;ui:hide=1,map=2,paused=4,block=8,chat=16,sign=32,chest=64,writing=128,mouseInterface=256,onlyFancy=512,options=1024,fancy=2048,textTaker=4096");
                for(int i=0;i<prismCount;i++)
                {
                    var r=prismRows[i];log.Offer("prism-consumer",r.Tick,"frame="+r.Frame+";key="+r.Key+";owner="+r.Owner+";sourceSession="+r.SourceSession+";sourceSelection="+r.SourceSelection+";sourceItem="+r.SourceSlot+","+r.SourceType+","+r.SourcePrefix+";session="+r.Session+";selection="+r.Selection+";item="+r.Slot+","+r.Weapon+","+r.Prefix+
                        ";target="+r.Target+","+r.TargetGeneration+","+r.TargetType+","+r.TargetNetId+";seen="+r.Seen.ToString("X16")+","+r.SeenHigh.ToString("X16")+";passed="+r.Passed.ToString("X16")+","+r.PassedHigh.ToString("X16")+";firstReject="+(r.PointKnown && r.Scope?"none":r.Reject??"none")+";observedFirstFalse="+(r.Reject??"none")+";outcome="+r.Outcome+";input="+r.Input+";ui="+r.Ui+";availability="+r.Availability+";tool="+r.Tool+";native="+r.Native+";scope="+r.Scope+
                        ";beforeCursor="+r.PhysicalX+","+r.PhysicalY+";physicalMap="+r.RawX+","+r.RawY+";borrowed="+r.BorrowedX+","+r.BorrowedY+";nativeCursor="+r.NativeX+","+r.NativeY+";returned="+r.Returned+":"+r.ReturnedX+","+r.ReturnedY+";screen="+V(r.Screen)+";gravity="+F(r.Gravity)+";origin="+V(r.Origin)+";point="+r.PointKnown+":"+V(r.Point)+";charge="+F(r.Charge)+";before="+V(r.Before)+";after="+V(r.After)+";expected="+r.ExpectedKnown+":"+V(r.Expected)+";steeringErrorSquared="+F(r.SteeringError)+";rotation="+F(r.Rotation)+","+F(r.AfterRotation)+";error="+(r.Error??"none"));
                }
                log.Stop("prism-"+reason);
            }
            catch(Exception error){Broken(error);}
        }
    }
}
#endif
