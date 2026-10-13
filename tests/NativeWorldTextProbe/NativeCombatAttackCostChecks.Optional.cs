using System;
using System.Reflection;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCombatAttackCostChecks
    {
        private static int optionalPlans;
        private static int optionalSourceClears;
        private static int optionalCorePrefixes;
        private struct OptionalPrefixCall {internal ulong Tick;internal int Required;}
        private static readonly List<OptionalPrefixCall> optionalPrefixCalls=new List<OptionalPrefixCall>(64);
        private static bool optionalForceBudget;
        private static CombatPredictionWork captureOwner;
        private static int captureGets,captureReads;
        private static bool captureObserved,captureCharged,captureThrow;
        private static void CaptureGetter()
        {
            if(!captureObserved)return;captureGets++;CaptureCharge();
            if(captureThrow){captureThrow=false;throw new CombatPredictionBudgetExceededException();}
        }
        private static void CaptureTerrainRead(){if(captureObserved){captureReads++;CaptureCharge();}}
        private static void CaptureCharge()
        {captureCharged&=ReferenceEquals(typeof(CombatPredictionWork).GetField("active",Flags).GetValue(null),captureOwner) && (int)Get(captureOwner,"optionalDepth")>0;}
        private static void CountOptionalPlan(){optionalPlans++;}
        private static void CountOptionalSourceClear(){optionalSourceClears++;}
        private static void CountOptionalCorePrefix(){optionalCorePrefixes++;}
        private static void CountOptionalShortPrefix(int __2){optionalPrefixCalls.Add(new OptionalPrefixCall{Tick=Main.GameUpdateCount,Required=__2});}
        private static void OptionalEarlyBudget(){if(optionalForceBudget){optionalForceBudget=false;throw new CombatPredictionBudgetExceededException();}}
        private static void OptionalConsumption(object context,object combat,object host,object attack,object input)
        {
            var audit=new Harmony("JueMingR.Tests.OptionalConsumption");
            try
            {
                NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                NativeCombatObservationChecks.Save(host,new ObservationOptions());
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);
                p.position=new Vector2(700,646);p.releaseUseItem=true;p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;
                Main.screenPosition=new Vector2(600,400);
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.Center=new Vector2(910,667);n.velocity=n.netOffset=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=100000;
                for(int y=0;y<Main.maxTilesY;y++)NativeToolsChecks.Tile(48,y,1);
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,true,false));
                for(int i=0;i<8;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(650,550),false,true);
                Console.WriteLine("OPTIONAL PREMISE path="+(GetOptional(host,"DisplayPath")!=null)+" preview="+(GetOptional(attack,"ExpectedPreview")!=null)+" cache="+((NpcPredictionCache)Get(Get(host,"Prediction"),"Cache")).Required+" workMs="+Get(Get(host,"PredictionWork"),"UsedMilliseconds"));
                Require(GetOptional(host,"DisplayPath")!=null && GetOptional(attack,"ExpectedPreview")==null,"counterexample owns a real valid path while retired preview has no Contact");
                audit.Patch(attack.GetType().GetMethod("PreparePlan",Flags),prefix:new HarmonyMethod(typeof(NativeCombatAttackCostChecks).GetMethod(nameof(CountOptionalPlan),Flags)));
                optionalPlans=0;
                using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))
                    for(int i=0;i<8;i++)Call(attack,"ValidatePreviewPresentation");
                Console.WriteLine("OPTIONAL RETIREMENT sameGucValidationCalls=8 actualPreparePlan="+optionalPlans+" previewRetired=true");
                Require(optionalPlans==0,"retired preview validation cannot produce a plan");
                int before=optionalPlans;
                for(int i=0;i<4;i++)Call(context,"UpdateRuntime");
                Require(optionalPlans==before,"empty outer callbacks at the same real update do not admit new optional preview work");
                n.life--;Call(host,"ValidateDisplay",(long)Main.GameUpdateCount);
                Require(GetOptional(host,"DisplayPath")==null,"same GUC real life change revokes visible path rather than reusing a clock-only lease");
                for(int i=0;i<6;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(650,550),false,true);
                Require(GetOptional(host,"DisplayPath")!=null,"budget-exit control starts with an actually published display lease");
                audit.Patch(attack.GetType().GetMethod("ResolveSelection",Flags),prefix:new HarmonyMethod(typeof(NativeCombatAttackCostChecks).GetMethod(nameof(OptionalEarlyBudget),Flags)));
                n.life--;optionalForceBudget=true;Call(context,"UpdateRuntime");
                Require(!optionalForceBudget && GetOptional(host,"DisplayPath")==null && GetOptional(attack,"ExpectedPreview")==null,"same GUC real change followed by an early producer budget exit cannot retain an unchecked old display lease");
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,false,false,false,25,true,false));
                NativeCombatSelectionPreviewChecks.Step(context,new Vector2(650,550),false,true);
                Require(!(bool)Get(host,"Marker") && !(bool)Get(Get(host,"Selection"),"HasTarget") && ((NpcPredictionCache)Get(Get(host,"Prediction"),"Cache")).Required==0,"saved Marker preference cannot keep selection/prediction alive after the final parent exits");
                Require(((ObservationOptions)Get(host,"Options")).Marker,"effective shutdown preserves the saved marker preference");
                audit.Patch(Get(host,"Prediction").GetType().GetMethod("Clear",Flags),prefix:new HarmonyMethod(typeof(NativeCombatAttackCostChecks).GetMethod(nameof(CountOptionalSourceClear),Flags)));
                NativeCombatObservationChecks.Save(host,new ObservationOptions(collision:true,marker:true));
                NativeCombatSelectionPreviewChecks.Step(context,new Vector2(650,550),false,true);optionalSourceClears=0;
                for(int i=0;i<12;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(650,550),false,true);
                Require((bool)Get(host,"Collision") && !(bool)Get(Get(host,"Selection"),"HasTarget") && optionalSourceClears==0,"independent collision observation remains active without repeatedly clearing an inactive prediction producer");
                Console.WriteLine("OPTIONAL SHUTDOWN collisionUpdates=12 actualSourceClear="+optionalSourceClears+" savedMarker=true");
                OptionalCoreDisplay(context,combat,host,input);
                OptionalShortCadence(context,combat,host,attack,input);
                OptionalActualContact(context,combat,host,attack,input);
                OptionalAccounting();
                Console.WriteLine("PASS optional consumption, repeated outer callback, same-update dependency and final-consumer marker shutdown");
            }
            finally{optionalForceBudget=false;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());}
        }
        private static void OptionalShortCadence(object context,object combat,object host,object attack,object input)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.releaseUseItem=true;p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.Center=new Vector2(910,667);n.velocity=n.netOffset=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=100000;
            for(int y=0;y<Main.maxTilesY;y++)NativeToolsChecks.Tile(48,y,1);
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,true,false));
            for(int i=0;i<8;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(650,550),false,true);
            Require(GetOptional(host,"DisplayPath")!=null && GetOptional(attack,"ExpectedPreview")==null,"short cadence starts with a valid NPC path and no retired preview demand");
            var audit=new Harmony("JueMingR.Tests.OptionalShortCadence");var method=Get(host,"Prediction").GetType().GetMethod("PreparePrefix",Flags);
            audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatAttackCostChecks).GetMethod(nameof(CountOptionalShortPrefix),Flags)));optionalPrefixCalls.Clear();
            int fullAttempts=0,shortAttempts=0;
            try
            {
                for(int i=0;i<16;i++)
                {
                    n.life--;NativeCombatSelectionPreviewChecks.Step(context,new Vector2(650,550),false,true);
                    ulong tick=Main.GameUpdateCount;var entries=optionalPrefixCalls.Where(entry=>entry.Tick==tick).ToArray();
                    bool full=(long)Get(host,"optionalTick")== (long)tick,shortOnly=!full && (long)Get(host,"displayShortTick")== (long)tick;
                    Require(full?entries.Length>=1 && entries.Length<=2:shortOnly?entries.Length==1 && entries[0].Required<=32:entries.Length==0,"actual full/short admission exactly accounts for this update's Source entries; short never grows to a120 tail");
                    if(full){fullAttempts++;Require((long)Get(host,"displayShortTick")== (long)tick,"a full actual attempt also owns this update's short clock");}
                    if(shortOnly)shortAttempts++;
                    int count=optionalPrefixCalls.Count;for(int repeat=0;repeat<4;repeat++)Call(context,"UpdateRuntime");
                    Require(optionalPrefixCalls.Count==count,"same GUC outer repeats cannot issue another full or short Source preparation");
                }
                Require(shortAttempts>0 && fullAttempts>0,"life changes with no Aim/core exercise both finite fresh-prefix and independent full-tail opportunities");
                Console.WriteLine("OPTIONAL SHORT CADENCE worldUpdates=16 fullActualAttempts="+fullAttempts+" shortActualAttempts="+shortAttempts+" sourceEntries="+optionalPrefixCalls.Count+" coreDemand=false repeatSourceEntries=0");
                // Stop changing this context. Complete blocked misses are still
                // pure consumption, even after fresh short publications exist.
                optionalPlans=0;for(int i=0;i<8;i++)Call(attack,"ValidatePreviewPresentation");Require(optionalPlans==0,"retired preview validation performs no plan production while bounded NPC path recovery remains active");
            }
            finally{audit.UnpatchAll(audit.Id);}
        }
        private static void CaptureDisplayFixture(object host)
        {
            // The producer overload accepts a result-owned lease. This existing
            // fixture explicitly exercises the selected-publication overload.
            host.GetType().GetMethod("CaptureDisplay",Flags,null,new[]{typeof(long)},null).Invoke(host,new object[]{(long)Main.GameUpdateCount});
        }
        private static void OptionalCoreDisplay(object context,object combat,object host,object input)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.releaseUseItem=true;p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.Center=new Vector2(910,667);n.velocity=n.netOffset=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=100000;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,true,true));
            for(int i=0;i<8;i++)CoreDisplayStep(context,combat,host,input,n);
            Console.WriteLine("CORE WARM target="+Get(Get(host,"Selection"),"HasTarget")+" path="+Get(host,"Path")+" failed="+Get(host,"TargetPredictionFailed")+" cache="+(((NpcPredictionCache)Get(Get(host,"Prediction"),"Cache")).Read(0)?.Count??0)+" rawDisplay="+(((NpcTrajectory)GetOptional(host,"displayPath"))?.Count??0)+" lease="+Get(host,"displayLeaseTick")+" tick="+Main.GameUpdateCount+" optionalTick="+Get(host,"optionalTick")+" corePermission="+Get(Get(combat,"Attack"),"Permission")+" workMs="+Get(Get(host,"PredictionWork"),"UsedMilliseconds"));
            Require(GetOptional(host,"DisplayPath")!=null,"core harvest starts with a genuinely prepared display publication");
            // The retention countercontrol belongs before the next full-tail
            // admission. Align with a real cadence admission; publishing the
            // static fixture itself does not advance the producer's clock.
            for(int i=0;i<5 && (long)Get(host,"optionalTick")!=(long)Main.GameUpdateCount;i++)CoreDisplayStep(context,combat,host,input,n);
            Require((long)Get(host,"optionalTick")==(long)Main.GameUpdateCount,"retention control starts after a real full-tail admission, not immediately before the next due tick");
            // Retention is a component countercontrol, not a claim that a
            // timed optional tail always reaches121. Publish the existing
            // independent static-NPC121 fixture before the real new core pass.
            NativeCombatAimChecks.Prepare(host,Get(combat,"Attack"),n,0,true);CaptureDisplayFixture(host);Call(host,"ValidateDisplay",(long)Main.GameUpdateCount);
            Require(((NpcTrajectory)GetOptional(host,"DisplayPath"))?.Count==121,"long retention control owns the explicit valid121 component publication");
            var retained=(NpcTrajectory)GetOptional(host,"DisplayPath");n.position.X+=.5f;CoreDisplayStep(context,combat,host,input,n);
            var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");
            Require(ReferenceEquals(retained,GetOptional(host,"DisplayPath")) && cache.Read(0)!=null && retained.Count>cache.Read(0).Count,"a valid long display is preserved while a distinct current core pass publishes its shorter prefix");
            var audit=new Harmony("JueMingR.Tests.CoreDisplayHarvest");var source=Get(host,"Prediction");var method=source.GetType().GetMethod("PreparePrefix",Flags);
            audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatAttackCostChecks).GetMethod(nameof(CountOptionalCorePrefix),Flags)));
            try
            {
                long due=(long)Get(host,"optionalTick");
                Require((long)Main.GameUpdateCount+1-due<5,"life-change control enters before another optional tail is due");
                n.life--;optionalCorePrefixes=0;CoreDisplayStep(context,combat,host,input,n);
                var display=(NpcTrajectory)GetOptional(host,"DisplayPath");
                Console.WriteLine("CORE HARVEST OBSERVED calls="+optionalCorePrefixes+" displayCount="+(display?.Count??0)+" displaySample="+(display?.SampleTick??-1)+" currentTick="+Main.GameUpdateCount+" tailBefore="+due+" tailAfter="+Get(host,"optionalTick")+" cacheCount="+(cache.Read(0)?.Count??0)+" cacheSample="+(cache.Read(0)?.SampleTick??-1)+" workMs="+Get(Get(host,"PredictionWork"),"UsedMilliseconds")+" exceeded="+Get(Get(host,"PredictionWork"),"Exceeded"));
                Require(optionalCorePrefixes==1 && display!=null && display.SampleTick==(long)Main.GameUpdateCount && (long)Get(host,"optionalTick")==due,"invalid old life display consumes this pass's fresh CompletedWorld core prefix without a second Source preparation or tail admission");
                Console.WriteLine("OPTIONAL CORE HARVEST sourcePreparePrefix="+optionalCorePrefixes+" oldTailTick="+due+" captureTick="+display.CaptureTick+" sampleTick="+display.SampleTick+" count="+display.Count);
                optionalCorePrefixes=0;
                for(int i=0;i<4;i++){Call(input,"BeginUpdate");Call(context,"UpdateRuntime");}
                Require(optionalCorePrefixes==0,"empty outer epochs do not re-sample Source to refresh a harvested display");
                int capturedLife=(int)Get(host,"displayLife");n.life--;optionalForceBudget=true;Call(context,"UpdateRuntime");
                Require(!optionalForceBudget && GetOptional(host,"DisplayPath")==null && (int)Get(host,"displayLife")==capturedLife,"same-GUC life change plus early budget cannot retag the retained publication with current life");
                NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");Call(host,"SampleMouse");NativeQuickItemChecks.BeginWorldStep();var beforeDisplay=GetOptional(host,"displayPath");Call(Get(combat,"Attack"),"PrepareNatural");
                Require(cache.Read(1)!=null && cache.Read(1).SampleTick==(long)Main.GameUpdateCount-1 && ReferenceEquals(beforeDisplay,GetOptional(host,"displayPath")) && (int)Get(host,"displayLife")==capturedLife,"a real BeforeNpc prefix cannot replace or relabel the CompletedWorld display");
            }
            finally{audit.UnpatchAll(audit.Id);}
        }
        private static void CoreDisplayStep(object context,object combat,object host,object input,NPC npc)
        {
            NativeToolExecutionChecks.Sample(context,input,npc.Center,true);Call(combat,"Sample");Call(host,"SampleMouse");NativeQuickItemChecks.BeginWorldStep();Call(Get(context,"nativeNpcs"),"BeginTick");Call(context,"UpdateRuntime");
        }
        private static void OptionalActualContact(object context,object combat,object host,object attack,object input)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3278,0,0);p.position=new Vector2(700,646);p.releaseUseItem=true;
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.Center=new Vector2(870,667);n.velocity=n.netOffset=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=100000;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,true,true));
            // Diagnose the natural source at its real consumption boundaries.
            // A projectile-phase receipt is not automatically a still-qualified
            // completed-world receipt after native AI/input/position changed.
            for(int i=0;i<8;i++)
            {
                NativeToolExecutionChecks.Sample(context,input,new Vector2(600,500),true);Call(combat,"Sample");Call(host,"SampleMouse");NativeQuickItemChecks.BeginWorldStep();
                p.Update(0);OptionalContactState(host,attack,p,"after-player");
                foreach(var body in Main.npc)if(body.active)body.UpdateNPC(body.whoAmI);
                for(int slot=0;slot<Main.maxProjectiles;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;Main.projectile[slot].Update(slot);}Main.ProjectileUpdateLoopIndex=-1;
                OptionalContactState(host,attack,p,"after-native-ai");Call(context,"UpdateRuntime");Call(context,"UpdateShell");OptionalContactState(host,attack,p,"completed-world");
            }
            // Independently arrange the already-covered native controller
            // component. Only its trajectory is synthetic; birth and the
            // qualified Contact are production outlets. This assertion does
            // not claim ordinary post-shot guns always retain an actual source.
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
            p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3278,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.channel=p.controlUseItem=true;p.yoyoGlove=p.magicString=false;p.counterWeight=0;
            n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.target=0;n.Center=new Vector2(810,667);n.velocity=n.netOffset=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=10000;Array.Clear(n.immune,0,n.immune.Length);
            NativeCombatObservationChecks.Save(host,new ObservationOptions().Toggle(1).Toggle(6));ComponentStep(context,combat,input,n);NativeCombatAimChecks.Prepare(host,attack,n,0,true);
            typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});
            Require(Main.projectile.Any(q=>q.active && q.type==541),"reuse component owns a real native yoyo birth");
            ComponentStep(context,combat,input,n);NativeCombatAimChecks.Prepare(host,attack,n,0,true);Call(attack,"Prepare");
            CaptureDisplayFixture(host);Call(host,"ValidateDisplay",(long)Main.GameUpdateCount);
            var actual=(AttackContact)GetOptional(attack,"ExpectedImpact");
            Require(actual!=null,"same completed-world component owns a qualified actual controller Contact before optional preparation");
            optionalPlans=0;
            for(int i=0;i<8;i++)Call(attack,"PreparePreviewOnly");
            Require(optionalPlans==0 && ReferenceEquals(actual,GetOptional(attack,"ExpectedImpact")),"optional preview entries preserve the actual qualified contact and do not solve an unused hypothetical attack");
            Require(GetOptional(attack,"ExpectedPreview")==null && !(bool)Get(attack,"PreviewDemand"),"retired preview leaves the qualified actual controller Contact intact");
            Console.WriteLine("OPTIONAL ACTUAL CONTACT retiredPreviewEntries=8 actualPreparePlan="+optionalPlans+" actualReceiveTick="+actual.ReceiveTick);

        }
        private static void OptionalCapture(object context,object combat,object host,object attack,object input,NPC npc,AttackContact actual)
        {
            var impact=Get(Get(host,"World"),"Impact");captureOwner=(CombatPredictionWork)Get(host,"PredictionWork");
            var audit=new Harmony("JueMingR.Tests.OptionalCapture");
            audit.Patch(attack.GetType().GetProperty("ExpectedImpact",Flags).GetGetMethod(true),prefix:new HarmonyMethod(typeof(NativeCombatAttackCostChecks),nameof(CaptureGetter)));
            audit.Patch(attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true).GetMethod("Read",Flags),prefix:new HarmonyMethod(typeof(NativeCombatAttackCostChecks),nameof(CaptureTerrainRead)));
            try
            {
                captureGets=captureReads=0;captureCharged=captureObserved=true;
                Call(impact,"Capture");captureObserved=false;
                Require(ReferenceEquals(actual,GetOptional(impact,"contact")) && captureGets==1 && captureReads>0 && captureCharged,"shell Capture consumes qualified actual terrain under the same host optional owner");
                long lease=(long)Get(host,"displayLeaseTick");Set(host,"displayLeaseTick",-1L);captureGets=captureReads=0;captureObserved=true;Call(impact,"Capture");captureObserved=false;Set(host,"displayLeaseTick",lease);
                Require(captureGets==0 && captureReads==0 && GetOptional(impact,"contact")==null,"no DisplayPath stops before actual/control terrain qualification");
                captureThrow=captureObserved=true;Call(impact,"Capture");captureObserved=false;
                Require(!captureThrow && !(bool)Get(impact,"Failed") && GetOptional(impact,"contact")==null && ReferenceEquals(actual,GetOptional(attack,"ExpectedImpact")),"a qualification budget exception clears only this presentation without latching or retiring actual input");
                using(captureOwner.Enter(Main.GameUpdateCount))using(CombatPredictionWork.Optional(3))
                {var watch=System.Diagnostics.Stopwatch.StartNew();while(watch.Elapsed.TotalMilliseconds<3.2)System.Threading.Thread.SpinWait(16);}
                captureGets=0;captureObserved=true;Call(impact,"Capture");captureObserved=false;
                Require(captureGets==0 && GetOptional(impact,"contact")==null && !(bool)Get(impact,"Failed") && ReferenceEquals(actual,GetOptional(attack,"ExpectedImpact")),"same-update exhausted optional allowance denies Capture without resetting or clearing actual input");
                ComponentStep(context,combat,input,npc);NativeCombatAimChecks.Prepare(host,attack,npc,0,true);Call(attack,"Prepare");CaptureDisplayFixture(host);Call(host,"ValidateDisplay",(long)Main.GameUpdateCount);
                var recovered=GetOptional(attack,"ExpectedImpact");Require(recovered!=null,"next-update Capture recovery has a fresh qualified actual controller Contact");
                captureCharged=captureObserved=true;Call(impact,"Capture");captureObserved=false;Require(ReferenceEquals(recovered,GetOptional(impact,"contact")) && !(bool)Get(impact,"Failed") && captureCharged,"next update restores presentation through the normal owner allowance");
                // Yoyo uses its own navigation terrain. This separate empty
                // registry component acquires an existing real world tile in
                // the control owner's shared reader; it does not claim that
                // the preceding yoyo naturally populated this other lease.
                var control=Get(attack,"Control");var sharedTerrain=(IPredictionTerrain)Get(control,"terrain");PredictionTile tile;PredictionStop tileStop;
                Require(sharedTerrain.Tile(43,43,out tile,out tileStop),"empty-registry component acquires a real existing floor cell");
                captureReads=0;captureObserved=true;using(captureOwner.Enter(Main.GameUpdateCount))using(CombatPredictionWork.Optional(3))Require((bool)Get(sharedTerrain,"Unchanged"),"the explicit old read set is currently valid");captureObserved=false;
                Require(captureReads>0 && captureCharged,"positive read counter sees validation of the explicit old terrain lease");
                int oldCells=((IDictionary)Get(Get(control,"terrain"),"cells")).Count;Require(oldCells>0,"zero-shot countercontrol owns an explicit nonempty old control terrain read set");Call(control,"Reset");
                Require(((IDictionary)Get(control,"shots")).Count==0 && ((IDictionary)Get(Get(control,"terrain"),"cells")).Count==oldCells,"normal control Reset removes registry entries while retaining exactly the old terrain read set");
                Require((bool)Get(attack,"CurrentReceiver"),"zero-registry getter still has a valid current receiver so its registry fastpath is the denying gate");
                captureReads=0;captureObserved=true;var empty=GetOptional(control,"ExpectedImpact");captureObserved=false;
                Require(empty==null && captureReads==0,"no observed control shots do not validate retained old terrain");
                Console.WriteLine("OPTIONAL CAPTURE EMPTY controlShots=0 retainedTerrainCells="+oldCells+" actualTerrainReads="+captureReads);
                Console.WriteLine("OPTIONAL CAPTURE qualifiedReadsCharged=true noDisplayReads=0 noControlReads=0 budgetPresentationOnly=true nextUpdateRecovery=true");
            }
            finally{captureObserved=captureThrow=false;captureOwner=null;audit.UnpatchAll(audit.Id);}
        }
        private static void OptionalContactState(object host,object attack,Player player,string phase)
        {
            var control=Get(attack,"Control");var entries=(IDictionary)Get(control,"shots");
            Console.WriteLine("CONTACT PHASE="+phase+" guc="+Main.GameUpdateCount+" receiver="+Get(attack,"CurrentReceiver")+" permission="+Get(attack,"Permission")+" current="+(GetOptional(attack,"current")!=null)+" next="+(GetOptional(attack,"next")!=null)+" channel="+player.channel+" entries="+entries.Count+" published="+(((NpcPredictionCache)Get(Get(host,"Prediction"),"Cache")).Read(1)?.Count??0));
            foreach(DictionaryEntry pair in entries)
            {
                var entry=pair.Value;var contact=(AttackContact)GetOptional(entry,"Contact");var shot=(Projectile)Get(entry,"Shot");var navigation=GetOptional(entry,"Navigation");
                Console.WriteLine("CONTACT SOURCE type="+shot.type+" prepared="+Get(entry,"Prepared")+" contact="+(contact!=null)+" hasPoint="+Get(entry,"HasPoint")+" valid="+Call(control,"Valid",entry)+" timelineCurrent="+(contact!=null && (bool)Call(attack,"TimelineCurrent",contact.Timeline))+" environment="+Get(Get(control,"environmentState"),"Current")+" terrain="+Get(Get(control,"terrain"),"Unchanged")+" navCurrent="+(navigation==null || (bool)Call(navigation,"Current",player))+" ai0="+shot.ai[0]);
            }
            Console.WriteLine("CONTACT RESULT actual="+(GetOptional(attack,"ExpectedImpact")!=null)+" preview="+(GetOptional(attack,"ExpectedPreview")!=null));
        }
        private static void OptionalAccounting()
        {
            var work=new CombatPredictionWork(1000);
            using(work.Enter(1))
            {
                using(CombatPredictionWork.Optional(50))
                {
                    while(work.UsedMilliseconds<10){ }
                    using(CombatPredictionWork.Optional(50))while(work.UsedMilliseconds<25){ }
                }
                // Core work between optional entries consumes only the total.
                while(work.UsedMilliseconds<35){ }
                using(CombatPredictionWork.Optional(50))
                {
                    while(work.UsedMilliseconds<50){ }
                    Require(CombatPredictionWork.TryContinue(),"nested optional time is charged once and intervening core time does not consume its remainder");
                    while(work.UsedMilliseconds<65){ }
                    Require(!CombatPredictionWork.TryContinue() && !work.Exceeded,"all optional entries share their actual cumulative allowance without exhausting the core owner");
                }
                using(CombatPredictionWork.Optional(50))Require(!CombatPredictionWork.TryContinue(),"another optional entry in the same native update cannot mint a fresh allowance");
                Require(CombatPredictionWork.TryContinue(),"leaving exhausted optional work restores the still available core allowance");
            }
            using(work.Enter(2))using(CombatPredictionWork.Optional(50))Require(CombatPredictionWork.TryContinue(),"the next real update receives a new optional opportunity");
            using(work.Enter(3))using(CombatPredictionWork.Optional(50))
            {
                while(work.UsedMilliseconds<5){ }
                using(CombatPredictionWork.Optional(1))Require(!CombatPredictionWork.TryContinue(),"a tighter nested optional ceiling includes the current outer interval already spent");
                Require(CombatPredictionWork.TryContinue(),"leaving the tighter nested ceiling restores only the existing outer remainder");
            }
            using(work.Enter(4))
            {
                using(CombatPredictionWork.Optional(20)){while(work.UsedMilliseconds<21){ }Require(!CombatPredictionWork.TryContinue(),"optional production cannot consume its reserved validation remainder");}
                using(CombatPredictionWork.Optional(30))Require(CombatPredictionWork.TryContinue(),"a real later consumer validation borrows the same owner's reserved remainder");
                using(CombatPredictionWork.Optional(20))Require(!CombatPredictionWork.TryContinue(),"another producer cannot borrow the validation reserve as a fresh production allowance");
            }
            Console.WriteLine("PASS shared actual optional accounting across nesting, core interleave, repeated entries and next-update recovery");
        }
    }
}
