using System;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Features.Guidance;
using JueMingR.Features.Tools;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeDisplayResponsibilityChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static int faults;
        private static void Fault(){faults++;throw new InvalidOperationException("Controlled optional display fault.");}
        private static bool SkipAchievement(){return false;}
        private static void RetryState(object host,object world,object marker,string phase)
        {
            var settings=(ObservationSettings)Get(host,"Settings");
            Console.WriteLine("DISPLAY RETRY phase="+phase+" optionsPath="+settings.Value.Path+" optionsMarker="+settings.Value.Marker+" loaded="+settings.Loaded+" busy="+settings.Busy+" ready="+settings.Ready+" canRun="+settings.CanRun+" accepted="+settings.AcceptedCommandId+" completed="+settings.CompletedCommandId+" succeeded="+settings.CompletionSucceeded+" pathAccess="+Get(host,"PathAccess")+" path="+Get(host,"Path")+" worldFailed="+Get(world,"Failed")+" markerFailed="+Get(marker,"Failed")+" selectionFailed="+Get(host,"selectionFailed")+" layer="+Get(host,"LayerStatus")+" canConfigure="+Get(host,"CanConfigure"));
        }
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");var world=Get(host,"World");var marker=Get(world,"Marker");
            var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");var input=Get(context,"Input");
            if(!(bool)Get(host,"PathAccess"))NativeHiddenEntryChecks.RevealForComposition(context);
            foreach(var n in Main.npc)n.active=false;
            var target=Main.npc[2];target.SetDefaults(2);target.whoAmI=2;target.active=true;target.target=0;target.position=new Vector2(800,650);
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,marker:true));NativeCombatObservationChecks.Fresh(context,host);
            Set(host,"LayerStatus",Enum.Parse(Get(host,"LayerStatus").GetType(),"Ready"));
            var harmony=new Harmony("JueMingR.Tests.CloseoutDisplay");var prepare=world.GetType().GetMethod("PrepareCore",Flags);
            harmony.Patch(prepare,prefix:new HarmonyMethod(typeof(NativeDisplayResponsibilityChecks).GetMethod(nameof(Fault),Flags)));faults=0;
            try
            {
                Call(context,"UpdateShell");for(int i=0;i<3;i++){Call(context,"UpdateRuntime");Call(context,"UpdateShell");}
                Require(faults==1 && (bool)Get(world,"Failed") && !(bool)Get(host,"Path"),"Real outer shell latches optional preparation once; saved display intent remains.");
                Require((bool)Get(Get(context,"Tools"),"Available") && (bool)Get(Get(context,"QuickItems"),"Available"),"Optional display failure leaves actual adjacent operation owners available.");
                // 0 is the display and 1 belongs to aim, including its OFF release.
                cache.Demand(2,1,120);NativeCombatObservationChecks.Fresh(context,host);
                // This fixture-owned reader has no registered scheduler.
                // Demand retains its lease; its owner prepares Source just as
                // the independent readers in NativeCombatObservationChecks do.
                var independentSelection=Get(host,"Selection");var identity=(JueMingR.Platform.Combat.NpcIdentity)Get(independentSelection,"Target");
                Require((bool)Get(independentSelection,"HasTarget") && ReferenceEquals(identity.Token,target),"Independent reader enters through the actual selected target after display failure.");
                Call(Get(host,"Prediction"),"Prepare",identity,(long)Main.GameUpdateCount);
                Require(cache.Read(2)!=null && cache.Read(0)==null,"Healthy independent prediction consumer keeps shared Source despite failed display.");
            }
            finally{harmony.Unpatch(prepare,HarmonyPatchType.All,harmony.Id);}
            var pixel=Terraria.GameContent.TextureAssets.MagicPixel;
            try
            {
                Terraria.GameContent.TextureAssets.MagicPixel=(ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>)typeof(ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>).GetConstructor(Flags,null,new[]{typeof(string)},null).Invoke(new object[]{"closeout-replaced-pixel"});
                Call(host,"Poll");Require(!(bool)Get(world,"Failed"),"Replaced borrowed resource lifetime restores display without reading failed Asset.Value.");
            }
            finally{Terraria.GameContent.TextureAssets.MagicPixel=pixel;}
            Call(host,"Set",1,true);NativeCombatObservationChecks.Fresh(context,host);Call(context,"UpdateShell");Require(!(bool)Get(world,"Failed") && cache.Read(0)!=null,"Explicit retry restores preparation without lost intent.");cache.Release(2);
            Set(marker,"Failed",true);NativeCombatObservationChecks.Save(host,new ObservationOptions(marker:true));
            var selection=Get(host,"Selection");var candidatesProperty=selection.GetType().GetProperty("Candidates",Flags);
            // Candidates is DEBUG observation only. Release still exercises the
            // same updates and checks real demand/selection and failure lifetime.
            int candidates=candidatesProperty==null?0:(int)candidatesProperty.GetValue(selection,null);
            for(int i=0;i<3;i++){NativeCombatObservationChecks.Fresh(context,host);Call(context,"UpdateShell");}
            Require((bool)Get(marker,"Failed") && !(bool)Get(host,"Marker") && !(bool)Get(selection,"HasTarget"),"Failed-only marker has no selection demand and clear-frame cannot unlock it.");
            if(candidatesProperty!=null)Require((int)candidatesProperty.GetValue(selection,null)==candidates,"DEBUG failed-only marker performs no candidate search.");
            Call(host,"Set",5,true);NativeCombatObservationChecks.Fresh(context,host);
            Require(!(bool)Get(marker,"Failed") && !(bool)Get(host,"Marker"),"Explicit marker retry clears its own failure while absent parent demand still keeps it inactive.");
            // Set submits the real preference command; Fresh drives native
            // observation but never Polls settings. Wait for this exact owner
            // receipt before claiming that the new parent demand is active.
            var retrySettings=(ObservationSettings)Get(host,"Settings");RetryState(host,world,marker,"before-parent");long priorCommand=retrySettings.AcceptedCommandId;
            Call(host,"Set",1,true);long acceptedCommand=retrySettings.AcceptedCommandId;
            Require(acceptedCommand>priorCommand,"Parent retry is accepted by the real settings owner as a new command.");
            NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !retrySettings.Busy && retrySettings.CompletedCommandId==acceptedCommand;});
            Require(retrySettings.CompletionSucceeded && retrySettings.CompletedCommandId==acceptedCommand && retrySettings.Ready && retrySettings.Value.Path && retrySettings.Value.Marker,"This exact parent retry commits successfully while preserving the marker preference.");
            NativeCombatObservationChecks.Fresh(context,host);RetryState(host,world,marker,"after-parent-receipt");Require((bool)Get(host,"Marker"),"Existing explicit marker retry remains reachable.");
            var guidance=Get(context,"Guidance");var guidanceWorld=Get(guidance,"World");
            Call(guidance,"SetEnabled",GuidanceKind.Rare,true);
            var guidancePrepare=guidanceWorld.GetType().GetMethod("PrepareCore",Flags);harmony.Patch(guidancePrepare,prefix:new HarmonyMethod(typeof(NativeDisplayResponsibilityChecks).GetMethod(nameof(Fault),Flags)));
            try{Call(context,"UpdateShell");Require((int)Get(guidanceWorld,"Failures")==7,"Shared optional guidance preparation fault stays local.");}
            finally{harmony.Unpatch(guidancePrepare,HarmonyPatchType.All,harmony.Id);}
            Call(guidanceWorld,"Clear");Require((int)Get(guidanceWorld,"Failures")==7,"Clearing guidance paint retains failure lifetime.");
            Call(guidance,"Update",Main.GameUpdateCount);Require(!(bool)Get(Get(guidance,"Rare"),"Visible"),"Failed guidance has no discovery consumer.");
            Call(guidance,"SetEnabled",GuidanceKind.Rare,true);Require(((int)Get(guidanceWorld,"Failures")&1)==0,"Guidance explicit retry clears only its content failure.");
            Call(guidanceWorld,"ResourcesChanged",Terraria.Localization.LanguageManager.Instance);Call(guidanceWorld,"Prepare");
            Require((int)Get(guidanceWorld,"Failures")==0,"Existing language-resource event restores failed guidance lifetime without discovery polling.");
            Mining(context,harmony,input);
            Call(host,"OnSessionEnded");Require(!(bool)Get(world,"Failed") && !(bool)Get(marker,"Failed"),"New session owns fresh display lifetime.");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
            Console.WriteLine("PASS DISPLAY real outer preparation, failure latch/retry, independent reader, guidance demand and native mining responsibility; no GPU claim.");
        }
        private static void Mining(object context,Harmony harmony,object input)
        {
            var tools=Get(context,"Tools");var mining=Get(tools,"Mining");var p=Main.LocalPlayer;
            // Reuse the original full tool consumer's complete initialization
            // and Reset, rather than growing a second approximate environment.
            NativeToolExecutionChecks.Initialize(context,null);
            p=NativeToolExecutionChecks.Reset(context,tools,input,ItemID.CopperPickaxe,0,0);
            NativeToolsChecks.SetMode(tools,2,1);NativeToolsChecks.Tile(42,40,6);
            Require((bool)Call(mining,"Select",p,42,40,6,false),"Real business vein selected before drawing fault.");Call(mining,"Update");
            Call(mining,"WaitForFalling",43,40,123,0UL);int falls=(int)Get(mining,"falls");
            var worker=tools.GetType().Assembly.GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker");var draw=worker.GetMethod("DrawEntityLabels",Flags);var contextField=worker.GetField("postfixContext",Flags);var layer=worker.GetField("entityLayerStatus",Flags);
            object prior=contextField.GetValue(null),priorLayer=layer.GetValue(null);var miningDraw=mining.GetType().GetMethod("Draw",Flags);
            harmony.Patch(miningDraw,prefix:new HarmonyMethod(typeof(NativeDisplayResponsibilityChecks).GetMethod(nameof(Fault),Flags)));
            try{contextField.SetValue(null,context);layer.SetValue(null,Enum.Parse(layer.FieldType,"Ready"));draw.Invoke(null,null);}
            finally{harmony.Unpatch(miningDraw,HarmonyPatchType.All,harmony.Id);contextField.SetValue(null,prior);layer.SetValue(null,priorLayer);}
            Require(((MiningRegion)Get(mining,"Region")).Count==1 && (int)Get(mining,"falls")==falls && (bool)Get(mining,"displayFailed"),"Real outer drawing catch preserves vein and falling witness.");
            var achievements=new[]{"HandleSpecialEvent","HandleMining","HandleRunning"};
            foreach(string name in achievements)harmony.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeDisplayResponsibilityChecks).GetMethod(nameof(SkipAchievement),Flags)));
            // Reuse the complete native execution seam; direct ItemCheck
            // omits the player's own per-update preparation for mining.
            try{for(int f=0;f<180 && Main.tile[42,40].active();f++)
            {NativeToolExecutionChecks.Sample(context,input,new Vector2(480,540),false);NativeQuickItemChecks.BeginWorldStep();p.Update(0);Call(context,"UpdateRuntime");}}
            finally{foreach(string name in achievements)harmony.Unpatch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),HarmonyPatchType.All,harmony.Id);}
            Require(!Main.tile[42,40].active(),"Actual native ItemCheck/PickTile progresses with failed colours and real eligibility.");
            NativeToolsChecks.SetMode(tools,2,0);Call(mining,"Update");Require(((MiningRegion)Get(mining,"Region")).Count==0 && (int)Get(mining,"falls")==0,"Normal disable still retires business and finite falling responsibility.");
        }
    }
}
