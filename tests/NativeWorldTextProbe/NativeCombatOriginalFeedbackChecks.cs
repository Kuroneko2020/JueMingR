using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Observe only natural native consumers. No supplied Contact, extra
    // Prepare or edited projectile direction may make an input test pass.
    internal static class NativeCombatOriginalFeedbackChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static Player player;private static NPC receiver;private static Vector2 physical;
        private static int parentType,childType,parentBirths,childBirths,aimed,raw,parentHits,childHits,damageType,inputAimed,inputRaw,firstCopper;
        private static readonly List<string> born=new List<string>();
        private static Vector2 firstCopperVelocity;
        private static bool assistEnabled,naturalPress;private static object observedAttack;
        private static int permittedBirths,releasedBirths;
        private static int damageSlot;private static readonly HashSet<int> clockChildren=new HashSet<int>();private static uint? clockChildHit,firstParentHit;
        private static readonly HashSet<int> sideTargets=new HashSet<int>();private static int sideStrikes;private static uint? zeroWhipTick;private static bool bridgeOnly;
        private sealed class SporeStep {internal Vector2 Velocity,Center;internal int Alpha;}
        private static int sporeUpdates,sporeRetired;
        private static int flightReads,familyCalls,familyMaximum,drawTerrainReads;
        private static readonly Dictionary<string,int> familyPasses=new Dictionary<string,int>();
        private static readonly List<string> familyQueries=new List<string>();
        private static object familyWork;private static double familyUsed;
        private static int beforeFamilyQueries,worldFamilyQueries;private static long requestedMemberSteps,totalRequestedMemberSteps;
        private static uint familyPassTick;private static int duplicateFamilyPasses;
        private static bool presenting;
        private static bool tracingFloating,tracingFamily;private static string floatingFirstReject;private static int floatingReports,floatingMotionRefusals;
        private static uint? floatingDetachedHit;
        private static void FlightRead(){flightReads++;}
        private static void FamilyRead(Projectile[] sources,JueMingR.Platform.Combat.NpcTrajectory path,object clock)
        {
            familyCalls++;familyMaximum=Math.Max(familyMaximum,sources.Length);tracingFamily=tracingFloating;floatingFirstReject=null;
            if(familyPassTick!=Main.GameUpdateCount){familyPassTick=Main.GameUpdateCount;familyPasses.Clear();familyQueries.Clear();beforeFamilyQueries=worldFamilyQueries=0;requestedMemberSteps=0;familyUsed=0;}
            var active=typeof(JueMingR.Features.Combat.CombatPredictionWork).GetField("active",Flags).GetValue(null);
            Require(ReferenceEquals(active,familyWork) && Convert.ToUInt64(Get(active,"tick"))==Main.GameUpdateCount,"every real family query is charged to the same current update's Host work owner");
            double used=Convert.ToDouble(Get(active,"UsedMilliseconds"));Require(used>=familyUsed,"family queries never reset the same update's accumulated work");familyUsed=used;
            string phase=Convert.ToString(Get(clock,"Phase"));if(phase=="BeforeNpc")beforeFamilyQueries++;else if(phase=="CompletedWorld")worldFamilyQueries++;else Require(false,"family query has a legal native preparation phase");
            int first=Convert.ToInt32(Get(clock,"FirstTick"));Require(path.Count<=121 && first>=0 && first<path.Count,"family request retains the legal finite NPC horizon and phase clock");
            int possibleChildren=parentType==130?7:childType>0?1:0;
            int updates=sources.Max(p=>p.extraUpdates+1);
            if(childType>0)updates=Math.Max(updates,Terraria.ID.ContentSamples.ProjectilesByType[childType].extraUpdates+1);
            long requested=(long)Math.Min(120,path.Count-first)*(sources.Length+possibleChildren)*updates;
            requestedMemberSteps+=requested;totalRequestedMemberSteps+=requested;
            // Native actions may change a source within one world update. Only
            // the same path, phase and unchanged observed members are repeats.
            string key=System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(path)+"/"+Get(clock,"Phase")+"/"+Get(clock,"FirstTick")+"/"+string.Join(";",sources.Select(p=>p.key+":"+p.type+":"+p.Center+":"+p.velocity+":"+p.ai[0]+":"+p.ai[1]+":"+p.timeLeft));
            int count;familyPasses.TryGetValue(key,out count);familyPasses[key]=count+1;if(count>0)duplicateFamilyPasses++;
            familyQueries.Add("path="+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(path)+" sample="+path.SampleTick+" capture="+path.CaptureTick+" count="+path.Count+" calculation="+(path.CalculationToken==null?"null":System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(path.CalculationToken).ToString())+" paused="+path.BudgetPaused+" clock="+Get(clock,"Phase")+"/"+Get(clock,"FirstTick")+" members="+string.Join(";",sources.Select(p=>p.key+":"+p.type+":"+p.Center+":"+p.velocity+":"+p.ai[0]+":"+p.ai[1]+":"+p.timeLeft)));
        }
        private static void FloatingMotionResult(bool __result,JueMingR.Platform.Combat.PredictionStop stop,JueMingR.Platform.Combat.PredictionPlayerMotion p)
        {
            if(!tracingFamily || __result)return;
            // All bindings are value copies. Harmony's __args array may write
            // boxed ref/out arguments back; an observer must never own that.
            var motion=p;
            if(stop==JueMingR.Platform.Combat.PredictionStop.UnsupportedMechanism && motion.FloatInWater && motion.FloatingNow)floatingMotionRefusals++;
            if(floatingFirstReject==null)floatingFirstReject="motion/"+stop+" floating="+motion.FloatingNow+" floatEquipment="+motion.FloatInWater+" wet="+motion.Wet+" position="+motion.X+","+motion.Y;
        }
        private static void FloatingDryResult(Projectile shot,float x,float y,bool __result)
        {if(tracingFamily && !__result && floatingFirstReject==null)floatingFirstReject="dry/type="+shot.type+" at="+x+","+y;}
        private static void FloatingBeforeNpcResult(int tick,int outer,bool __result)
        {if(tracingFamily && !__result && floatingFirstReject==null)floatingFirstReject="beforeNpc/tick="+tick+" outer="+outer;}
        private static void FamilyResult(Projectile[] sources,JueMingR.Features.Combat.AttackContact __result)
        {
            if(tracingFamily && __result==null && floatingReports++<4)Console.WriteLine("ORIGINAL floating family-null tick="+Main.GameUpdateCount+" firstReject="+(floatingFirstReject??"finite-no-contact")+" sources="+string.Join(",",sources.Select(p=>p.type+"@ai="+p.ai[0])));
            tracingFamily=false;
            if(player==null || player.HeldItem.type!=1228 || !assistEnabled || player.itemAnimation!=1)return;
            Console.WriteLine("ORIGINAL family-result tick="+Main.GameUpdateCount+" contact="+(__result==null?"null":(__result.Timeline.SampleTick+__result.ReceiveTick).ToString())+" sourceCount="+sources.Length);
        }
        private static Exception FamilyException(Exception __exception){if(__exception!=null)Console.WriteLine("ORIGINAL family-exception "+__exception);return __exception;}
        private static void TerrainRead(){if(presenting)drawTerrainReads++;}
        private static bool Present(object host,object attack,object world,Player p,NPC n,ProbeGraphics graphics,string output,string name,bool expected,bool save,bool optionalDisplay=false)
        {
            int reads=0,families=0,captureReads=0;drawTerrainReads=0;presenting=true;
            bool visible;
            try{visible=NativeCombatUsageChecks.PresentNative(host,attack,world,p,n,graphics,output,name,expected,save,optionalDisplay,()=>{captureReads=drawTerrainReads;drawTerrainReads=0;reads=flightReads;families=familyCalls;});}
            finally{presenting=false;}
            // ExpectedImpact/Capture validates its prepared terrain dependency.
            // Project and actual Draw only consume the captured scalar/member
            // lease. Do not charge Update's legitimate validation to rendering.
            if(captureReads>0)Console.WriteLine("ORIGINAL capture terrain validation name="+name+" reads="+captureReads+" projectDrawReads="+drawTerrainReads);
            Require(drawTerrainReads==0 && flightReads==reads && familyCalls==families,"actual Project/Draw uses the captured member lease without terrain, pool discovery or replay: "+name);
            return visible;
        }
        private static void SporeBefore(Projectile __instance,out SporeStep __state)
        {__state=player!=null && __instance.type==228?new SporeStep{Velocity=__instance.velocity,Center=__instance.Center,Alpha=__instance.alpha}:null;}
        private static void SporeAfter(Projectile __instance,SporeStep __state)
        {
            if(__state==null)return;sporeUpdates++;
            var expected=__state.Velocity*.96f;
            Require(Vector2.DistanceSquared(__instance.velocity,expected)<.000001f && __instance.alpha==__state.Alpha+2,"actual SporeCloud AI44 preserves its linear .96 damping and alpha clock");
            if(__instance.active)Require(__instance.alpha<=200 && Vector2.DistanceSquared(__instance.Center,__state.Center+expected)<.0001f,"actual SporeCloud moves along its sealed birth direction");
            else{Require(__instance.alpha>200,"actual SporeCloud exits at its native fade boundary");sporeRetired++;}
        }
        private static void Birth(int __result)
        {
            if(player==null || __result<0 || __result>=Main.maxProjectiles)return;
            var shot=Main.projectile[__result];if(shot.owner!=player.whoAmI || shot.type!=parentType && shot.type!=childType)return;
            if(shot.type==parentType)parentBirths++;else childBirths++;
            Vector2 ray=shot.velocity.SafeNormalize(Vector2.UnitX),toward=(receiver.Center-shot.Center).SafeNormalize(Vector2.UnitX),mouse=(physical-shot.Center).SafeNormalize(-Vector2.UnitX);
            float targetDot=Vector2.Dot(ray,toward),mouseDot=Vector2.Dot(ray,mouse);
            var cursor=new Vector2(Main.mouseX,Main.mouseY)+Main.screenPosition;
            if(shot.type==parentType)
            {
                if(targetDot>.9f && mouseDot<.9f)aimed++;if(mouseDot>.9f)raw++;
                if(cursor.X>player.Center.X && Vector2.DistanceSquared(cursor,physical)>4)inputAimed++;
                if(Vector2.DistanceSquared(cursor,physical)<=2)inputRaw++;
                bool permitted=assistEnabled && naturalPress;
                if(permitted)permittedBirths++;else releasedBirths++;
                Require(permitted?cursor.X>player.Center.X && Vector2.DistanceSquared(cursor,physical)>4:Vector2.DistanceSquared(cursor,physical)<=2,"each original consumer respects this natural press/release input permission");
                if(player.HeldItem.type==5669 && (player.itemAnimationMax-player.itemAnimation)/player.itemTime==0)
                {
                    // TrueCopper uses Main.MouseWorld, native world-border
                    // clamping, then displacement/2. Later two burst members
                    // additionally query a target and consume native RNG.
                    // A raw-unit-ray expectation would reject legal vanilla.
                    var clamped=Main.MouseWorld;player.LimitPointToPlayerReachableArea(ref clamped);
                    var expected=(clamped-player.MountedCenter)/2;
                    var rawPoint=physical;var targetPoint=receiver.Center;player.LimitPointToPlayerReachableArea(ref rawPoint);player.LimitPointToPlayerReachableArea(ref targetPoint);
                    Require(Vector2.DistanceSquared(rawPoint,targetPoint)>4,"native world-border clamp keeps the manual and selected target premises distinct");
                    Require(Vector2.DistanceSquared(shot.velocity,expected)<.0001f,"native first TrueCopper source consumes the observed cursor after its original clamp");
                    if(firstCopper==0)firstCopperVelocity=shot.velocity;firstCopper++;
                }
            }
            born.Add("type="+shot.type+" slot="+shot.whoAmI+" tick="+Main.GameUpdateCount+" pressed="+naturalPress+" permission="+Get(observedAttack,"Permission")+" animation="+player.itemAnimation+" time="+player.itemTime+" burst="+(player.itemTime>0?(player.itemAnimationMax-player.itemAnimation)/player.itemTime:-1)+" cursor="+cursor+" center="+shot.Center+" velocity="+shot.velocity+" targetDot="+targetDot+" rawDot="+mouseDot);
        }
        private static void BeforeDamage(Projectile __instance,out int __state)
        {__state=damageType;damageType=__instance.type;damageSlot=__instance.whoAmI;
            if(player!=null && parentType==841 && __instance.type==841 && !firstParentHit.HasValue && (__instance.ai[0]==1 || __instance.ai[0]==21 || __instance.ai[0]==33 || __instance.ai[0]==34 || __instance.ai[0]==41))
                Console.WriteLine("ORIGINAL whip-native tick="+Main.GameUpdateCount+" ai="+__instance.ai[0]+" animation="+player.itemAnimation+" frame="+player.bodyFrame.Y+" arm="+player.GetArmPosition()+" damage="+__instance.damage+" box="+__instance.Hitbox+" target="+receiver.Hitbox+" collision="+__instance.Colliding(__instance.Hitbox,receiver.Hitbox));
        }
        private static void AfterDamage(Projectile __instance,int __state){if(parentType==841 && __instance.type==841 && __instance.damage==0 && !zeroWhipTick.HasValue)zeroWhipTick=Main.GameUpdateCount;damageType=__state;}
        private static void Hit(NPC __instance,int __result)
        {
            if(damageType==841 && sideTargets.Contains(__instance.whoAmI))sideStrikes++;
            if(!ReferenceEquals(__instance,receiver) || __result<=0)return;
            if(damageType==parentType){parentHits++;if(!firstParentHit.HasValue){firstParentHit=Main.GameUpdateCount;if(parentType==46){var p=Main.projectile[damageSlot];Rectangle ext;bridgeOnly=p.AI_019_Spears_GetExtensionHitbox(player,out ext) && !p.Hitbox.Intersects(receiver.Hitbox) && !ext.Intersects(receiver.Hitbox) && p.Colliding(p.Hitbox,receiver.Hitbox);Console.WriteLine("ORIGINAL native spear bridge first="+firstParentHit+" onlyBridge="+bridgeOnly+" body="+p.Hitbox+" extension="+ext+" target="+receiver.Hitbox);}}}
            if(damageType==childType){childHits++;if(clockChildren.Contains(damageSlot) && !clockChildHit.HasValue)clockChildHit=Main.GameUpdateCount;if(tracingFloating && !floatingDetachedHit.HasValue && !Main.projectile.Any(p=>p.active && p.owner==player.whoAmI && p.type==parentType))floatingDetachedHit=Main.GameUpdateCount;}
        }
        internal static void RunSpears(object context,ProbeGraphics graphics=null,string output=null)
        {Run(context,false,graphics,output);}
        internal static void RunHand(object context,ProbeGraphics graphics=null,string output=null)
        {Run(context,true,graphics,output);}
        internal static void RunSeams(object context,ProbeGraphics graphics=null,string output=null)
        {Run(context,false,graphics,output,true);}
        private static void Run(object context,bool hand,ProbeGraphics graphics,string output,bool seams=false)
        {using(var steps=graphics==null?null:NativeCombatUsageChecks.ObserveDrawSteps())RunCore(context,hand,graphics,output,seams);}
        private static void RunCore(object context,bool hand,ProbeGraphics graphics,string output,bool seams)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var input=Get(context,"Input");var attack=Get(combat,"Attack");
            observedAttack=attack;
            familyWork=Get(host,"PredictionWork");
            var audit=new Harmony("JueMingR.Tests.OriginalFeedbackSpearUsage");
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            var birth=typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13);
            audit.Patch(birth,postfix:new HarmonyMethod(typeof(NativeCombatOriginalFeedbackChecks).GetMethod(nameof(Birth),Flags)));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatOriginalFeedbackChecks).GetMethod(nameof(BeforeDamage),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatOriginalFeedbackChecks).GetMethod(nameof(AfterDamage),Flags)));
            audit.Patch(typeof(Projectile).GetMethod("Update",Flags),prefix:new HarmonyMethod(typeof(NativeCombatOriginalFeedbackChecks).GetMethod(nameof(SporeBefore),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatOriginalFeedbackChecks).GetMethod(nameof(SporeAfter),Flags)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatOriginalFeedbackChecks).GetMethod(nameof(Hit),Flags)));
            var assembly=attack.GetType().Assembly;
            audit.Patch(attack.GetType().GetMethod("ReadFlight",Flags),prefix:new HarmonyMethod(typeof(NativeCombatOriginalFeedbackChecks).GetMethod(nameof(FlightRead),Flags)));
            audit.Patch(assembly.GetType("JueMingR.TerrariaHost.Combat.HostMeleeAttack",true).GetMethod("KnownSpearFamily",Flags),prefix:new HarmonyMethod(typeof(NativeCombatOriginalFeedbackChecks).GetMethod(nameof(FamilyRead),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatOriginalFeedbackChecks).GetMethod(nameof(FamilyResult),Flags)),finalizer:new HarmonyMethod(typeof(NativeCombatOriginalFeedbackChecks).GetMethod(nameof(FamilyException),Flags)));
            audit.Patch(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true).GetMethod("Read",Flags),prefix:new HarmonyMethod(typeof(NativeCombatOriginalFeedbackChecks).GetMethod(nameof(TerrainRead),Flags)));
            var floatingObserver=typeof(NativeCombatOriginalFeedbackChecks).GetMethod(nameof(FloatingMotionResult),Flags);
            Require(floatingObserver.ReturnType==typeof(void) && floatingObserver.GetParameters().All(p=>!p.ParameterType.IsByRef && p.ParameterType!=typeof(object[])),"floating observation has no result/ref/out or __args write-back capability before checking any child-only prediction");
            audit.Patch(typeof(JueMingR.Features.Combat.PlayerMotionContinuation).GetMethod("Advance",Flags),postfix:new HarmonyMethod(floatingObserver));
            audit.Patch(assembly.GetType("JueMingR.TerrariaHost.Combat.HostProjectileEnvironment",true).GetMethod("Dry",Flags),postfix:new HarmonyMethod(typeof(NativeCombatOriginalFeedbackChecks).GetMethod(nameof(FloatingDryResult),Flags)));
            audit.Patch(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true).GetMethod("BeforeNpc",Flags),postfix:new HarmonyMethod(typeof(NativeCombatOriginalFeedbackChecks).GetMethod(nameof(FloatingBeforeNpcResult),Flags)));
            var failures=new List<string>();
            Vector2? copperManual=null;
            try
            {
                if(Environment.GetEnvironmentVariable("JMR_USAGE_ORIGINAL_FEEDBACK")=="whip-next")
                {
                    foreach(int count in new[]{0,1,4})try{RunNextWhipHand(context,host,combat,input,attack,count,graphics,output);}catch(Exception error){failures.Add("next-whip/"+count+": "+error.Message);Console.WriteLine("FAIL ORIGINAL next-whip/"+count+" "+error);}
                    Require(failures.Count==0,string.Join(" | ",failures));return;
                }
                if(seams)
                {
                    if(graphics!=null){RunPlainSpearCurrent(context,host,combat,input,attack,4672,841,graphics,output);RunFloatingDraw(context,host,combat,input,attack,graphics,output);}
                    else {RunPlainSpearCurrent(context,host,combat,input,attack,274,46);RunWhipOtherHits(context,host,combat,input,attack,1);RunWhipOtherHits(context,host,combat,input,attack,4);}
                    return;
                }
                // AI019 has four existing derived routes, each with a real
                // branch: repeated mushroom, first-step thunder, and late
                // chlorophyte/north-pole (the latter also checks a wall).
                foreach(var row in hand?new[]{new[]{55,6,0,53},new[]{5669,1100,0,60}}:graphics!=null?new[]{new[]{1228,222,228,49},new[]{1228,222,228,60}}:new[]{new[]{1228,222,228,49},new[]{1228,222,228,60},new[]{756,130,131,49},new[]{4061,730,732,60},new[]{1947,342,343,60}})
                foreach(bool assist in new[]{false,true})
                {
                    try
                    {
                        NativeCombatObservationChecks.Save(host,new JueMingR.Features.Combat.ObservationOptions());NativeCombatCadenceChecks.Save(combat,new JueMingR.Features.Combat.CombatOptions());
                        player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,row[0],0,0);player.position=new Vector2(700,646);player.ResetEffects();player.ResetFallDamage();player.releaseUseItem=true;
                        Main.screenPosition=new Vector2(480,400);Main.GameViewMatrix.Zoom=Vector2.One;Main.rand=new Terraria.Utilities.UnifiedRandom(7193);
                        receiver=NativeCombatControlPreviewChecks.Dummy(2,row[3],40);physical=new Vector2(550,430);parentType=row[1];childType=row[2];parentBirths=childBirths=aimed=raw=parentHits=childHits=damageType=inputAimed=inputRaw=firstCopper=sporeUpdates=sporeRetired=0;born.Clear();
                        assistEnabled=assist;naturalPress=false;permittedBirths=releasedBirths=0;
                        familyMaximum=duplicateFamilyPasses=0;familyPasses.Clear();totalRequestedMemberSteps=0;int initialFamilies=familyCalls,initialFlights=flightReads;
                        var meleeType=assembly.GetType("JueMingR.TerrariaHost.Combat.HostMeleeAttack",true);var scansField=meleeType.GetField("SpearFlightPoolScans",Flags);var stepsField=meleeType.GetField("SpearFlightMemberSteps",Flags);
                        int initialScans=scansField==null?0:(int)scansField.GetValue(null),initialSteps=stepsField==null?0:(int)stepsField.GetValue(null);
                        clockChildren.Clear();clockChildHit=null;
                        NativeCombatObservationChecks.Save(host,new JueMingR.Features.Combat.ObservationOptions(false,true,true,false,true,25,true,assist));
                        for(int i=0;i<4;i++)NativeCombatSelectionPreviewChecks.Step(context,physical,false,true);
                        Require(familyCalls==initialFamilies,"natural idle source-free preview does not replay a spear family");
                        bool preview=GetOptional(attack,"ExpectedPreview")!=null;
                        if(graphics!=null)Present(host,attack,Get(host,"World"),player,receiver,graphics,output,"original-idle-"+row[0]+"-"+row[3]+"-"+assist,true,true,true);
                        // One complete press/release/repress sequence crosses
                        // first use, late child birth, cooldown and recovery.
                        int previewFrames=0,stableFrames=0,stableShown=0,tailFrames=0,tailRecoveries=0,missingRun=0,longestMissing=0,actualFrames=0,actualGap=0,actualLongest=0,maxBefore=0,maxWorld=0;bool previousTail=false;var previewMisses=new List<string>();
                        for(int i=0;i<75;i++)
                        {
                            int beforeMemberSteps=stepsField==null?0:(int)stepsField.GetValue(null);
                            int beforeFamilyCalls=familyCalls;
                            naturalPress=i<35 || i>=42;NativeCombatSelectionPreviewChecks.Step(context,physical,naturalPress);
                            if(!hand && familyPassTick==Main.GameUpdateCount)
                            {
                                // This fixture has one ClearLine candidate:
                                // one actual-phase admission, then World's
                                // candidate/full-tail/remaining-window requests.
                                // Different immutable requests are not memo hits.
                                Require((bool)Get(Get(host,"Options"),"ClearLine") && (int)Get(Get(host,"Selection"),"CandidateCount")==1 && beforeFamilyQueries<=1 && worldFamilyQueries<=3,"single-receiver natural ClearLine preparation retains its separate bounded phase responsibilities");
                                maxBefore=Math.Max(maxBefore,beforeFamilyQueries);maxWorld=Math.Max(maxWorld,worldFamilyQueries);
                            }
                            if(!hand && stepsField!=null)
                            {
                                int updateSteps=(int)stepsField.GetValue(null)-beforeMemberSteps;
                                Require(updateSteps<=requestedMemberSteps,"actual native member substeps stay within distinct finite requests and their original extraUpdates/bounded births");
                            }
                            var contact=(JueMingR.Features.Combat.AttackContact)GetOptional(attack,"ExpectedPreview");
                            // Native animation=1 is separately observed while
                            // the next-use timing oracle is being checked.
                            // Preserve every supplied update in the denominator,
                            // including cold/optional omissions. Independent
                            // native input and actual receive oracles follow.
                            bool tail=row[0]==1228 && player.itemAnimation==1;
                            if(tail)tailFrames++;else{stableFrames++;if(contact!=null)stableShown++;else if(missingRun==0)TraceOriginalPreviewFailure(host,attack,row[0],row[3],assist,i);}
                            if(previousTail && contact!=null)tailRecoveries++;previousTail=tail;
                            if(contact!=null)missingRun=0;else longestMissing=Math.Max(longestMissing,++missingRun);
                            if(contact!=null)previewFrames++;else if(previewMisses.Count<3)previewMisses.Add("frame="+i+" animation="+player.itemAnimation+" time="+player.itemTime+" live="+string.Join(",",Main.projectile.Where(q=>q.active && q.owner==player.whoAmI).Select(q=>q.type.ToString())));
                            bool actual=Present(host,attack,Get(host,"World"),player,receiver,graphics,output,"original-use-"+row[0]+"-"+row[3]+"-"+assist+"-"+i,true,graphics!=null && (i==0 || i==21 || i==22 || i==74),true);
                            if(actual){actualFrames++;actualGap=0;}else actualLongest=Math.Max(actualLongest,++actualGap);
                        }
                        Require(stableShown>0 && stableFrames>=73 && longestMissing<15 && actualFrames>0 && actualLongest<15,"the full original-use window repeatedly acquires preview and presentation within three nominal display refresh opportunities");
                        if(!hand)Require(familyCalls>initialFamilies && familyMaximum>=2,"natural parent/derived sources share one captured finite family replay");
                        if(!hand && scansField!=null && stepsField!=null)
                        {
                            int scans=(int)scansField.GetValue(null)-initialScans,steps=(int)stepsField.GetValue(null)-initialSteps,families=familyCalls-initialFamilies;
                            Require(scans<=flightReads-initialFlights && steps>0 && steps<=totalRequestedMemberSteps,"DEBUG total native substep work stays within the observed distinct finite requests; memo hits need no scan");
                            Console.WriteLine("PASS ORIGINAL spear work discoveryScans="+scans+" families="+families+" maxSources="+familyMaximum+" memberSteps="+steps+" hits="+(parentHits+childHits));
                        }
                        else if(!hand)Console.WriteLine("ORIGINAL spear DEBUG work counters UNAVAILABLE; native receive/input/presentation assertions remain active");
                        if(!hand)Require(duplicateFamilyPasses==0,"Release observed identical path/phase/member facts are replayed once per synchronous pass");
                        if(!hand)Console.WriteLine("ORIGINAL family phase bounds maxBefore="+maxBefore+" maxCompleted="+maxWorld+" declaredMemberSubstepLimit="+totalRequestedMemberSteps+" hardExceeded="+Get(familyWork,"Exceeded")+" debugSteps="+(stepsField==null?"UNAVAILABLE":((int)stepsField.GetValue(null)-initialSteps).ToString()));
                        Console.WriteLine("PASS ORIGINAL preview stable="+stableShown+"/"+stableFrames+" full="+previewFrames+"/75 longestMissing="+longestMissing+" actual="+actualFrames+"/75 actualLongestMissing="+actualLongest+" tail="+tailFrames+" restored="+tailRecoveries+" actualDraw="+(graphics!=null));
                        Console.WriteLine("ORIGINAL "+(hand?"hand":"spear")+" item="+row[0]+" dummyX="+row[3]+" assist="+assist+" preview="+preview+" previewFrames="+previewFrames+"/75 missing=["+string.Join(";",previewMisses)+"] parents="+parentBirths+" children="+childBirths+" aimed="+aimed+" raw="+raw+" inputAimed="+inputAimed+" inputRaw="+inputRaw+" firstCopper="+firstCopper+" parentDamage="+parentHits+" childDamage="+childHits+" births=["+string.Join(";",born)+"]");
                        Require(parentBirths>0 && (hand || childBirths>0),"natural entire native use reaches parent and actual derived birth: "+row[0]);
                        Require(inputAimed==permittedBirths && inputRaw==releasedBirths,"all permitted consumers use assistance and all manual/released consumers use physical input: "+row[0]+" assist="+assist);
                        Console.WriteLine("PASS ORIGINAL input assisted="+inputAimed+"/"+permittedBirths+" manualOrReleased="+inputRaw+"/"+releasedBirths);
                        if(row[0]==5669)
                        {
                            Require(firstCopper>=2,"TrueCopper crosses first members of separate original use animations");
                            if(!assist)copperManual=firstCopperVelocity;
                            else Require(copperManual.HasValue && Vector2.DistanceSquared(copperManual.Value,firstCopperVelocity)>1,"actual first TrueCopper OFF/ON vectors remain distinct after the original clamp");
                            Console.WriteLine("PASS ORIGINAL TrueCopper first displacement assist="+assist+" actual="+firstCopperVelocity+" manual="+copperManual);
                        }
                        else Require(assist?aimed==parentBirths && raw==0:raw==parentBirths && aimed==0,"every naturally directed source follows the assisted/raw ray: "+row[0]+" assist="+assist);
                        if(assist){Require(parentHits+childHits>0,"directed native attack actually damages placed dummy: "+row[0]);if(!hand && row[3]==60)Require(childHits>0 && parentHits==0,"far dummy receives real child rather than close spear head: "+row[0]);}
                        // TrueCopper's second/third burst members may legally
                        // choose GetZenithTarget even with raw manual input.
                        // Its first native vector and each actual cursor above
                        // provide the independent OFF witness instead.
                        else if(row[0]!=5669)Require(parentHits+childHits==0,"physical mouse away from dummy provides an independent manual control: "+row[0]);
                        if(row[0]==1228)
                        {
                            receiver.dontTakeDamage=true;naturalPress=false;NativeCombatSelectionPreviewChecks.Step(context,physical,false,true);
                            Present(host,attack,Get(host,"World"),player,receiver,graphics,output,"original-revoked-"+row[3]+"-"+assist,false,graphics!=null);
                            receiver.dontTakeDamage=false;NativeCombatSelectionPreviewChecks.Step(context,physical,false,true);
                            Present(host,attack,Get(host,"World"),player,receiver,graphics,output,"original-restored-"+row[3]+"-"+assist,true,graphics!=null);
                            // Let both sealed children naturally finish. A
                            // spatially straight fading spore is distinct
                            // from NPC prediction and a curved ChlorophyteOrb.
                            for(int i=0;i<120;i++)NativeCombatSelectionPreviewChecks.Step(context,physical,false);
                            Require(sporeRetired==childBirths && sporeUpdates==101*childBirths && !Main.projectile.Any(q=>q.active && q.type==228),"all actual SporeCloud sources complete 100 moving AI steps and exit before damage on step101");
                            Console.WriteLine("PASS ORIGINAL SporeCloud sealed motor item=1228 assist="+assist+" retired="+sporeRetired+" updates="+sporeUpdates);
                            // Item1228 is not autoReuse. Native StartActualUse
                            // tests animation==0 BEFORE the old animation is
                            // decremented. A release on its 1/1 tail can only
                            // end that hand; the following fresh press starts
                            // the next complete one. Observe both actions
                            // before judging the independently saved preview.
                            naturalPress=true;int beforeClock=parentBirths;
                            var spearClocks=new List<Tuple<uint,int>>();var previewDelay=attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackWindow",true).GetMethod("PreviewDelay",Flags);
                            for(int i=0;i<30 && (i==0 || player.itemAnimation!=1);i++)
                            {NativeCombatSelectionPreviewChecks.Step(context,physical,true);spearClocks.Add(Tuple.Create(Main.GameUpdateCount,(int)previewDelay.Invoke(null,new object[]{player,player.HeldItem})));}
                            Require(parentBirths==beforeClock+1 && player.itemAnimation==1 && player.itemTime==1,"natural full Chlorophyte hand reaches its original non-autoreuse 1/1 tail");
                            uint tailTick=Main.GameUpdateCount;var tailPreview=(JueMingR.Features.Combat.AttackContact)GetOptional(attack,"ExpectedPreview");int atTail=parentBirths;
                            foreach(var q in Main.projectile)if(q.active && q.type==228 && q.owner==player.whoAmI)clockChildren.Add(q.whoAmI);
                            Present(host,attack,Get(host,"World"),player,receiver,graphics,output,"original-clock-tail-"+row[3]+"-"+assist,tailPreview!=null,graphics!=null);
                            naturalPress=false;NativeCombatSelectionPreviewChecks.Step(context,physical,false);
                            Require(parentBirths==atTail && player.itemAnimation==0,"next native action only retires the old Chlorophyte hand");
                            naturalPress=true;NativeCombatSelectionPreviewChecks.Step(context,physical,true);
                            Require(parentBirths==atTail+1 && Main.GameUpdateCount==tailTick+2,"fresh native press emits the new Chlorophyte hand two world actions after the tail observation");
                            uint nextBirthTick=Main.GameUpdateCount;naturalPress=false;
                            Console.WriteLine("ORIGINAL tail source witness tail="+tailTick+" birth="+nextBirthTick+" sample="+(tailPreview==null?-1:tailPreview.Timeline.SampleTick)+" capture="+(tailPreview==null?-1:tailPreview.Timeline.CaptureTick)+" delay="+(tailPreview==null?-1:tailPreview.Delay)+" receive="+(tailPreview==null?-1:tailPreview.ReceiveTick));
                            for(int i=0;i<45;i++)NativeCombatSelectionPreviewChecks.Step(context,physical,false);
                            Console.WriteLine("ORIGINAL tail-clock item=1228 assist="+assist+" tailTick="+tailTick+" actualBirthTick="+nextBirthTick+" observedOldChildHit="+clockChildHit+" preview="+(tailPreview!=null)+" previewDelay="+(tailPreview==null?-1:tailPreview.Delay)+" previewReceiveTick="+(tailPreview==null?-1:tailPreview.Timeline.SampleTick+tailPreview.ReceiveTick));
                            foreach(var sample in spearClocks)Require(sample.Item2==(int)(nextBirthTick-sample.Item1)-1,"AI019 matched-time full cooldown predicts the same actual next hand: tick="+sample.Item1+" predicted="+sample.Item2+" actual="+((int)(nextBirthTick-sample.Item1)-1));
                            // This clock component seals a healthy tail hypothesis to compare its
                            // absolute native birth/receive; it is separate from optional
                            // per-frame coverage in the original fixed use window above.
                            Require(tailPreview!=null,"natural tail retains a lawful current-flight or complete-new-hand preview");
                            if(clockChildHit.HasValue)
                                Require(tailPreview.Delay==0 && tailPreview.Timeline.SampleTick+tailPreview.ReceiveTick==clockChildHit.Value,"already sealed Chlorophyte child owns the earliest actual receive before the hypothetical new hand");
                            else Require(tailPreview.Timeline.SampleTick+tailPreview.Delay+1==nextBirthTick,"captured tail hypothesis predicts the absolute next native hand birth, including its legitimate display window age");
                            int oldFlights=flightReads,oldFamilies=familyCalls;
                            player.selectedItemState.Select(1);NativeCombatSelectionPreviewChecks.Step(context,physical,false);
                            Require(flightReads==oldFlights && familyCalls==oldFamilies,"switching away retires spear discovery and replay even while its sealed children remain");
                            NativeCombatObservationChecks.Save(host,new JueMingR.Features.Combat.ObservationOptions());NativeCombatSelectionPreviewChecks.Step(context,physical,false);
                            Require(flightReads==oldFlights && familyCalls==oldFamilies,"closed observation performs no spear pool discovery or replay");
                        }
                    }
                    catch(Exception error){failures.Add(row[0]+"/"+row[3]+"/"+assist+": "+error.Message);Console.WriteLine("FAIL ORIGINAL spear "+error);}
                }
                if(!hand && graphics==null)
                {
                    foreach(var plain in new[]{new[]{277,47},new[]{4672,841},new[]{274,46}})
                    try{RunPlainSpearCurrent(context,host,combat,input,attack,plain[0],plain[1]);}catch(Exception error){failures.Add("plain-current/"+plain[0]+": "+error.Message);Console.WriteLine("FAIL ORIGINAL plain-current/"+plain[0]+" "+error);}
                    RunWhipOtherHits(context,host,combat,input,attack,1);RunWhipOtherHits(context,host,combat,input,attack,4);
                    foreach(int count in new[]{0,1,4})RunNextWhipHand(context,host,combat,input,attack,count);
                    try{RunClockCounterexamples(context,host,combat,input,attack);}catch(Exception error){failures.Add("shared-clock: "+error.Message);Console.WriteLine("FAIL ORIGINAL shared-clock "+error);}
                }
                if(!hand && graphics!=null)
                {RunPlainSpearCurrent(context,host,combat,input,attack,4672,841,graphics,output);RunNextWhipHand(context,host,combat,input,attack,0,graphics,output);RunFloatingDraw(context,host,combat,input,attack,graphics,output);}
                Require(failures.Count==0,"original spear feedback failures: "+string.Join(" | ",failures));
            }
            finally{player=null;receiver=null;observedAttack=null;tracingFloating=tracingFamily=false;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new JueMingR.Features.Combat.ObservationOptions());NativeCombatCadenceChecks.Save(combat,new JueMingR.Features.Combat.CombatOptions());}
        }
        private static void TraceOriginalPreviewFailure(object host,object attack,int item,int dummy,bool assist,int frame)
        {
            // This is a snapshot AFTER the existing predicate has failed. It
            // neither enters a preparation scope nor changes the denominator.
            var selected=(JueMingR.Platform.Combat.NpcIdentity)Get(Get(host,"Selection"),"Target");var work=Get(host,"PredictionWork");
            var cache=(JueMingR.Features.Combat.NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");
            Console.WriteLine("ORIGINAL PREVIEW FIRST FAILURE item="+item+" dummyTile="+dummy+" assist="+assist+" frame="+frame+" GUC="+Main.GameUpdateCount+" animation="+player.itemAnimation+" time="+player.itemTime+" target="+selected.Slot+"/"+selected.Generation+" expected="+receiver.whoAmI+"/"+receiver.generation+" budgetExceeded="+Get(work,"Exceeded")+" budgetMs="+Get(work,"UsedMilliseconds")+" attackFailed="+Get(attack,"Failed")+" pathFailed="+Get(host,"pathFailed")+" read0="+(cache.Read(0)==null?"none":cache.Read(0).Identity.Slot+"/"+cache.Read(0).SampleTick)+" read1="+(cache.Read(1)==null?"none":cache.Read(1).Identity.Slot+"/"+cache.Read(1).SampleTick)+" parents="+parentBirths+" children="+childBirths+" parentHits="+parentHits+" childHits="+childHits);
            bool consumed=(bool)Get(attack,"hasConsumedInput");var input=Get(attack,"consumedInput");
            Console.WriteLine("ORIGINAL PREVIEW input hasFact="+consumed+" factGUC="+Get(attack,"consumedInputStep")+" kind="+Get(input,"Kind")+" point="+Get(input,"Point")+" current="+(GetOptional(attack,"current")!=null)+" next="+(GetOptional(attack,"next")!=null)+" compatible="+Call(attack,"PreviewInputCompatible")+" activeSources="+string.Join(";",Main.projectile.Where(p=>p.active && p.owner==player.whoAmI).Select(p=>p.type+"/ai="+p.ai[0]+"/position="+p.Center+"/velocity="+p.velocity)));
            var plans=(Array)Get(attack,"candidatePlans");
            bool validSlot=selected.Slot>=0 && selected.Slot*2+1<plans.Length;
            foreach(var plan in new[]{Get(attack,"preview"),validSlot?plans.GetValue(selected.Slot*2):null,validSlot?plans.GetValue(selected.Slot*2+1):null})
            {
                if(plan==null){Console.WriteLine("ORIGINAL PREVIEW plan none");continue;}
                var timeline=(JueMingR.Platform.Combat.NpcTrajectory)GetOptional(plan,"Timeline");var contact=(JueMingR.Features.Combat.AttackContact)GetOptional(plan,"Contact");
                Console.WriteLine("ORIGINAL PREVIEW plan prepared="+Get(plan,"Prepared")+" preparedGUC="+Get(plan,"PreparedTick")+" phase="+Get(plan,"Phase")+" delay="+Get(plan,"Delay")+" timeline="+(timeline==null?"none":timeline.Identity.Slot+"/"+timeline.SampleTick)+" contact="+(contact==null?"none":contact.Timeline.Identity.Slot+"/"+contact.Tick));
            }
        }
        private static void RunPlainSpearCurrent(object context,object host,object combat,object input,object attack,int weapon,int shotType,ProbeGraphics graphics=null,string output=null)
        {
            NativeCombatObservationChecks.Save(host,new JueMingR.Features.Combat.ObservationOptions());NativeCombatCadenceChecks.Save(combat,new JueMingR.Features.Combat.CombatOptions());
            player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,weapon,0,0);player.position=new Vector2(700,646);player.ResetEffects();player.ResetFallDamage();player.releaseUseItem=true;
            Main.screenPosition=new Vector2(480,400);Main.GameViewMatrix.Zoom=Vector2.One;receiver=NativeCombatControlPreviewChecks.Dummy(2,weapon==274?52:49,40);physical=receiver.Center;
            if(weapon==274)receiver.immune[player.whoAmI]=15;
            parentType=shotType;childType=0;parentBirths=parentHits=0;firstParentHit=null;bridgeOnly=false;assistEnabled=false;naturalPress=false;born.Clear();sideTargets.Clear();
            NativeCombatObservationChecks.Save(host,new JueMingR.Features.Combat.ObservationOptions(false,true,true,false,true,25,true,false));
            for(int i=0;i<4;i++)NativeCombatSelectionPreviewChecks.Step(context,physical,false,true);
            naturalPress=true;NativeCombatSelectionPreviewChecks.Step(context,physical,true);
            Require(parentBirths==1 && parentHits==0,"manual original body creates a sealed source before its first body receive: "+weapon);
            var originalSource=weapon==4672?Main.projectile.Single(q=>q.active && q.owner==player.whoAmI && q.type==shotType):null;
            physical=new Vector2(550,430);naturalPress=false;NativeCombatSelectionPreviewChecks.Step(context,physical,false);
            if(weapon==4672)
            {
                // This member-revocation seam needs one healthy captured
                // native family, not a mandatory red box on its cold frame.
                // Wait only normal updates of this same source, and never
                // pass its actual first receive to manufacture a later oracle.
                var work=Get(host,"PredictionWork");int waits=0;
                for(;;)
                {
                    bool healthy=GetOptional(attack,"ExpectedPreview")!=null && GetOptional(Get(attack,"preview"),"Flight")!=null;
                    Console.WriteLine("ORIGINAL whip-member preparation wait="+waits+" tick="+Main.GameUpdateCount+" missing="+(!healthy)+" budgetMs="+Get(work,"UsedMilliseconds")+" exceeded="+Get(work,"Exceeded")+" firstNativeReceive="+firstParentHit);
                    if(healthy || waits==6 || firstParentHit.HasValue)break;
                    NativeCombatSelectionPreviewChecks.Step(context,physical,false);waits++;
                    Require(parentBirths==1 && originalSource.active && originalSource.type==shotType && originalSource.owner==player.whoAmI,"finite member preparation observes the same single native birth without another press");
                }
                Require(!firstParentHit.HasValue && GetOptional(attack,"ExpectedPreview")!=null && GetOptional(Get(attack,"preview"),"Flight")!=null,"the same native family supplies a healthy preview before its first real receive within six normal updates");
            }
            var saved=(JueMingR.Features.Combat.AttackContact)GetOptional(attack,"ExpectedPreview");uint observed=Main.GameUpdateCount;
            if(weapon==4672)
            {
                var world=Get(host,"World");Present(host,attack,world,player,receiver,graphics,output,"original-whip-current",true,graphics!=null);
                // Check the aged branch on this SAME naturally captured family.
                // Actual Draw below retains its original same-update boundary;
                // no clock manipulation or replacement flight creates age1.
                var savedFlight=Get(Get(attack,"preview"),"Flight");Require(savedFlight!=null && (bool)Call(savedFlight,"PresentationCurrent",1),"the captured ordinary whip family starts with a healthy age1 presentation member gate");
                bool canFloat=player.canFloatInWater,shouldFloat=player.ShouldFloatInWater;player.canFloatInWater=!canFloat;Require(player.ShouldFloatInWater!=shouldFloat,"the native floating capability changes without another world action");Require(!(bool)Call(savedFlight,"PresentationCurrent",1),"age1 presentation does not skip the captured spear/whip family owner gate");RejectPresentation(world,graphics,"ordinary whip floating capability changed");player.canFloatInWater=canFloat;
                Require((bool)Call(savedFlight,"PresentationCurrent",1),"exact native owner restoration restores the same age1 member gate");
                Present(host,attack,world,player,receiver,graphics,output,"original-whip-float-restored",true,false);
                float range=player.whipRangeMultiplier;player.whipRangeMultiplier+=.25f;RejectPresentation(world,graphics,"ordinary whip range changed");player.whipRangeMultiplier=range;
                Present(host,attack,world,player,receiver,graphics,output,"original-whip-range-restored",true,false);
                var frame=player.bodyFrame;var arm=player.GetArmPosition();bool changed=false;
                for(int i=0;i<Main.OffsetsPlayerOnhand.Length;i++){player.bodyFrame.Y=i*56;if(player.GetArmPosition()!=arm){changed=true;break;}}
                Require(changed,"native body frame supplies a distinct arm anchor for this same-update lease counterexample");RejectPresentation(world,graphics,"ordinary whip arm anchor changed");player.bodyFrame=frame;
                Present(host,attack,world,player,receiver,graphics,output,"original-whip-arm-restored",true,false);
                Require(ReferenceEquals(saved,GetOptional(attack,"ExpectedPreview")),"pure member restoration retains the same independently prepared current-flight contact without another action or Prepare");
                Console.WriteLine("PASS ORIGINAL whip same-update range/arm revocation and restoration actualDraw="+(graphics!=null));
            }
            for(int i=0;i<40 && !firstParentHit.HasValue;i++)NativeCombatSelectionPreviewChecks.Step(context,physical,false);
            Console.WriteLine("ORIGINAL plain current item="+weapon+" observed="+observed+" actualFirstHit="+firstParentHit+" previewReceive="+(saved==null?-1:saved.Timeline.SampleTick+saved.ReceiveTick)+" delay="+(saved==null?-1:saved.Delay));
            Require(firstParentHit.HasValue && saved!=null && saved.Delay==0 && saved.Timeline.SampleTick+saved.ReceiveTick==firstParentHit.Value,"ordinary sealed body owns its actual earliest receive while the physical input points away and no new press is permitted: "+weapon);
            if(weapon==274)Require(bridgeOnly,"native first permitted spear receive intersects a bridge rectangle while both endpoint rectangles miss");
        }
        private static void RunWhipOtherHits(object context,object host,object combat,object input,object attack,int count)
        {
            NativeCombatObservationChecks.Save(host,new JueMingR.Features.Combat.ObservationOptions());NativeCombatCadenceChecks.Save(combat,new JueMingR.Features.Combat.CombatOptions());
            player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,4672,0,0);player.position=new Vector2(700,646);player.ResetEffects();player.ResetFallDamage();player.releaseUseItem=true;
            receiver=NativeCombatControlPreviewChecks.Dummy(10,49,40);physical=receiver.Center;sideTargets.Clear();for(int i=0;i<count;i++)sideTargets.Add(NativeCombatControlPreviewChecks.Dummy(i,47,40).whoAmI);
            parentType=841;childType=0;parentBirths=parentHits=sideStrikes=0;firstParentHit=zeroWhipTick=null;assistEnabled=naturalPress=false;born.Clear();
            // A radius-zero physical selection retains the actual chosen
            // target while closer native Damage receivers stay observable.
            NativeCombatObservationChecks.Save(host,new JueMingR.Features.Combat.ObservationOptions(false,true,true,true,true,0,true,false));
            for(int i=0;i<4;i++)NativeCombatSelectionPreviewChecks.Step(context,physical,false,true);
            naturalPress=true;NativeCombatSelectionPreviewChecks.Step(context,physical,true);
            var stepsField=attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostMeleeAttack",true).GetField("SpearFlightMemberSteps",Flags);
            int initialSteps=stepsField==null?0:(int)stepsField.GetValue(null),initialFamilies=familyCalls;
            naturalPress=false;NativeCombatSelectionPreviewChecks.Step(context,physical,false);
            uint observed=Main.GameUpdateCount;int replaySteps=stepsField==null?0:(int)stepsField.GetValue(null)-initialSteps,replayCalls=familyCalls-initialFamilies;
            var source=Main.projectile.First(q=>q.active && q.owner==player.whoAmI && q.type==841);int updates=source.extraUpdates+1;
            var saved=(JueMingR.Features.Combat.AttackContact)GetOptional(attack,"ExpectedPreview");var plan=Get(attack,"preview");var flight=GetOptional(plan,"Flight");
            for(int i=0;i<40;i++)NativeCombatSelectionPreviewChecks.Step(context,physical,false);
            Console.WriteLine("ORIGINAL whip native otherHits="+count+" strikes="+sideStrikes+" zeroTick="+zeroWhipTick+" firstSelected="+firstParentHit+" preview="+(saved==null?-1:saved.Timeline.SampleTick+saved.ReceiveTick)+" liveFlight="+(flight!=null));
            Require(sideStrikes==count,"each native preceding dummy writes one genuine OnHit in the original NPC slot loop");
            if(count==1)Require(firstParentHit.HasValue && saved!=null && saved.Delay==0 && saved.Timeline.SampleTick+saved.ReceiveTick==firstParentHit.Value,"simple whip damage decay retains its genuine later selected receive");
            else
            {
                Require(zeroWhipTick.HasValue && !firstParentHit.HasValue && flight==null,"native preceding whip hits reach zero damage before the later target; that current body supplies no future contact");
                if(stepsField!=null)
                {
                    long remainingActions=(long)zeroWhipTick.Value-observed+1;
                    Require(replayCalls>0 && replaySteps>0 && remainingActions>0 && replaySteps<=replayCalls*updates*remainingActions,"DEBUG finite current-whip replay stops by the genuine zero-damage action rather than continuing the empty receive path to native retirement");
                    Console.WriteLine("PASS ORIGINAL whip zero-damage work memberSteps="+replaySteps+" families="+replayCalls+" updates="+updates+" nativeRemainingActions="+remainingActions);
                }
                else Console.WriteLine("ORIGINAL whip zero-damage DEBUG work counter UNAVAILABLE; native zero damage and no selected receive remain asserted");
            }
            sideTargets.Clear();
        }
        private static void RunNextWhipHand(object context,object host,object combat,object input,object attack,int count,ProbeGraphics graphics=null,string output=null)
        {
            NativeCombatObservationChecks.Save(host,new JueMingR.Features.Combat.ObservationOptions());NativeCombatCadenceChecks.Save(combat,new JueMingR.Features.Combat.CombatOptions());
            player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,4672,0,0);player.position=new Vector2(700,646);player.ResetEffects();player.ResetFallDamage();player.releaseUseItem=true;
            receiver=NativeCombatControlPreviewChecks.Dummy(10,49,40);physical=receiver.Center;sideTargets.Clear();for(int i=0;i<count;i++)sideTargets.Add(NativeCombatControlPreviewChecks.Dummy(i,47,40).whoAmI);
            parentType=841;childType=0;parentBirths=parentHits=sideStrikes=0;firstParentHit=zeroWhipTick=null;assistEnabled=naturalPress=false;born.Clear();
            NativeCombatObservationChecks.Save(host,new JueMingR.Features.Combat.ObservationOptions(false,true,true,true,true,0,true,false));
            for(int i=0;i<4;i++)NativeCombatSelectionPreviewChecks.Step(context,physical,false,true);
            var saved=(JueMingR.Features.Combat.AttackContact)GetOptional(attack,"ExpectedPreview");uint observed=Main.GameUpdateCount;
            if(count==0)
            {
                // A revocation component needs a healthy hypothesis to revoke.
                // Report the original cold interval, then use only a bounded
                // normal idle schedule; this is not a first-click timing test.
                for(int wait=0;saved==null && wait<6;wait++)
                {
                    Console.WriteLine("ORIGINAL idle whip cold capture wait="+wait+" tick="+Main.GameUpdateCount+" budgetMs="+Get(Get(host,"PredictionWork"),"UsedMilliseconds")+" exceeded="+Get(Get(host,"PredictionWork"),"Exceeded"));
                    NativeCombatSelectionPreviewChecks.Step(context,physical,false,true);saved=GetOptional(attack,"ExpectedPreview") as JueMingR.Features.Combat.AttackContact;
                }
                Require(saved!=null && parentBirths==0,"bounded normal idle capture supplies the original source-free whip revocation hypothesis");
                if((uint)Get(attack,"previewStep")==Main.GameUpdateCount)
                {NativeCombatSelectionPreviewChecks.Step(context,physical,false,true);saved=GetOptional(attack,"ExpectedPreview") as JueMingR.Features.Combat.AttackContact;}
                Require(saved!=null && (uint)Get(attack,"previewStep")<Main.GameUpdateCount && GetOptional(Get(attack,"preview"),"Flight")==null,"the idle negative component tests an actually aged healthy source-free hypothesis");
                var world=Get(host,"World");Present(host,attack,world,player,receiver,graphics,output,"original-whip-idle",true,graphics!=null);
                float range=player.whipRangeMultiplier;player.whipRangeMultiplier+=.25f;
                var idlePlan=Get(attack,"preview");int idleAge=(int)(Main.GameUpdateCount-(uint)Get(attack,"previewStep"));var idleOwner=GetOptional(idlePlan,"WhipOwner");
                Console.WriteLine("ORIGINAL idle whip range witness age="+idleAge+" flight="+(GetOptional(idlePlan,"Flight")!=null)+" ownerCurrent="+(idleOwner==null?"unavailable":Convert.ToString(Get(idleOwner,"Current")))+" members="+Call(idlePlan,"MembersCurrent",player)+" presentationMembers="+Call(idlePlan,"PresentationMembersCurrent",player,idleAge));
                RejectPresentation(world,graphics,"idle whip range changed");player.whipRangeMultiplier=range;Present(host,attack,world,player,receiver,graphics,output,"original-whip-idle-range-restored",true,false);
                var frame=player.bodyFrame;var arm=player.GetArmPosition();bool changed=false;
                for(int i=0;i<Main.OffsetsPlayerOnhand.Length;i++){player.bodyFrame.Y=i*56;if(player.GetArmPosition()!=arm){changed=true;break;}}
                Require(changed,"idle whip has a distinct native arm anchor for its same-update negative lease");RejectPresentation(world,graphics,"idle whip arm changed");player.bodyFrame=frame;Present(host,attack,world,player,receiver,graphics,output,"original-whip-idle-arm-restored",true,false);
                bool storm=player.sandStorm;player.sandStorm=!storm;RejectPresentation(world,graphics,"idle whip sandStorm changed");player.sandStorm=storm;Present(host,attack,world,player,receiver,graphics,output,"original-whip-idle-storm-restored",true,false);
                int damage=player.HeldItem.damage,nativeDamage=player.GetWeaponDamage(player.HeldItem);player.HeldItem.damage+=5;Require(player.GetWeaponDamage(player.HeldItem)!=nativeDamage,"native future whip damage premise changed");RejectPresentation(world,graphics,"idle whip damage changed");player.HeldItem.damage=damage;Present(host,attack,world,player,receiver,graphics,output,"original-whip-idle-damage-restored",true,false);
                float summoner=player.summonerWeaponSpeedBonus;player.summonerWeaponSpeedBonus+=.25f;RejectPresentation(world,graphics,"idle whip native summoner speed changed");player.summonerWeaponSpeedBonus=summoner;Present(host,attack,world,player,receiver,graphics,output,"original-whip-idle-summoner-restored",true,false);
                float useTime=player.whipUseTimeMultiplier;player.whipUseTimeMultiplier+=.25f;RejectPresentation(world,graphics,"idle whip native use-time multiplier changed");player.whipUseTimeMultiplier=useTime;Present(host,attack,world,player,receiver,graphics,output,"original-whip-idle-time-restored",true,false);
                Require(ReferenceEquals(saved,GetOptional(attack,"ExpectedPreview")),"same-update ordinary whip owner/damage restoration reuses the exact independently prepared idle hypothesis");
                Console.WriteLine("PASS ORIGINAL idle whip arm/range/sandStorm/damage/duration lease actualDraw="+(graphics!=null));
            }
            // The aged display above has its own revocation checks. The real
            // first-receive oracle needs a hypothesis of this exact action
            // sample, acquired only through a bounded normal idle schedule.
            // Keep the original initial four updates and their cold facts.
            int freshWaits=0;
            while(true)
            {
                var freshPlan=Get(attack,"preview");var freshTimeline=GetOptional(freshPlan,"Timeline") as JueMingR.Platform.Combat.NpcTrajectory;
                var freshContact=GetOptional(attack,"ExpectedPreview") as JueMingR.Features.Combat.AttackContact;
                bool fresh=freshTimeline!=null && freshTimeline.SampleTick==(long)Main.GameUpdateCount && freshTimeline.CaptureTick==(long)Main.GameUpdateCount && (uint)Get(attack,"previewStep")==Main.GameUpdateCount && !freshTimeline.BudgetPaused && !(bool)Get(attack,"Failed") && !(bool)Get(host,"TargetPredictionFailed") && (count<4?freshContact!=null:(bool)Get(freshPlan,"Prepared") && freshContact==null && freshTimeline.Count==JueMingR.Features.Combat.NpcPredictionCache.Horizon+1);
                Console.WriteLine("ORIGINAL idle whip fresh hypothesis sides="+count+" wait="+freshWaits+" tick="+Main.GameUpdateCount+" sample="+(freshTimeline==null?-1:freshTimeline.SampleTick)+" capture="+(freshTimeline==null?-1:freshTimeline.CaptureTick)+" healthy="+fresh+" budgetMs="+Get(Get(host,"PredictionWork"),"UsedMilliseconds")+" exceeded="+Get(Get(host,"PredictionWork"),"Exceeded"));
                if(fresh){saved=freshContact;observed=Main.GameUpdateCount;break;}
                Require(freshWaits<6,"finite normal idle updates must acquire the current-sample complete whip hypothesis before its real first use");
                freshWaits++;NativeCombatSelectionPreviewChecks.Step(context,physical,false,true);
            }
            naturalPress=true;NativeCombatSelectionPreviewChecks.Step(context,physical,true);naturalPress=false;
            for(int i=0;i<40;i++)NativeCombatSelectionPreviewChecks.Step(context,physical,false);
            Console.WriteLine("ORIGINAL idle whip future hand sides="+count+" observed="+observed+" firstSelected="+firstParentHit+" zeroTick="+zeroWhipTick+" preview="+(saved==null?-1:saved.Timeline.SampleTick+saved.ReceiveTick)+" previewDelay="+(saved==null?-1:saved.Delay)+" sideStrikes="+sideStrikes);
            Require(parentBirths==1 && sideStrikes==count,"one natural native whip hand visits the preceding receiver denominator");
            if(count<4)Require(firstParentHit.HasValue && saved!=null && saved.Delay==0 && saved.Timeline.SampleTick+saved.ReceiveTick==firstParentHit.Value,"the untouched idle hypothesis matches the naturally consumed first whip hand's actual earliest selected receive");
            else Require(zeroWhipTick.HasValue && !firstParentHit.HasValue && saved==null,"native simple preceding whip hits exhaust damage before the chosen target; the idle hand must not promise that nonexistent receive");
            sideTargets.Clear();
        }
        private static void RejectPresentation(object world,ProbeGraphics graphics,string name)
        {
            int reads=flightReads,families=familyCalls;drawTerrainReads=0;presenting=true;
            try
            {
                Call(world,"Prepare");var zoom=Main.GameViewMatrix.ZoomMatrix;
                if(graphics==null){var impact=Get(world,"Impact");Call(impact,"Project",zoom,Matrix.Invert(zoom));Require(!(bool)Get(impact,"Visible"),name+" immediately retires the cached projection");}
                else Require(!graphics.Pixels(()=>Call(world,"Draw"),zoom).Any(c=>c.A>100 && c.R>190 && c.G<110 && c.B<110),name+" immediately removes actual red pixels");
            }
            finally{presenting=false;}
            Require(drawTerrainReads==0 && flightReads==reads && familyCalls==families,"same-update rejection performs no terrain/discovery/replay work: "+name);
        }
        private static void RunFloatingDraw(object context,object host,object combat,object input,object attack,ProbeGraphics graphics,string output)
        {
            const int targetY=40;
            NativeCombatObservationChecks.Save(host,new JueMingR.Features.Combat.ObservationOptions());NativeCombatCadenceChecks.Save(combat,new JueMingR.Features.Combat.CombatOptions());
            player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,1228,0,0);player.position=new Vector2(700,646);player.ResetFallDamage();player.releaseUseItem=true;player.armor[4].SetDefaults(4404);
            // Native floating equipment and a local water surface, followed
            // by the same original Player/NPC/projectile/observer schedule.
            // The native player's 700..720 body still touches water, while
            // the naturally extended spear at x>720 has a dry continuation.
            for(int x=43;x<=44;x++)for(int y=41;y<=42;y++){Main.tile[x,y].active(false);Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(0);}
            Main.screenPosition=new Vector2(480,400);Main.GameViewMatrix.Zoom=Vector2.One;receiver=NativeCombatControlPreviewChecks.Dummy(2,targetY==40?60:49,targetY);physical=receiver.Center;
            parentType=222;childType=228;parentBirths=childBirths=parentHits=childHits=sporeUpdates=sporeRetired=0;firstParentHit=null;assistEnabled=naturalPress=false;born.Clear();
            NativeCombatObservationChecks.Save(host,new JueMingR.Features.Combat.ObservationOptions(false,true,true,false,true,25,true,false));
            var terrain=attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);var floating=terrain.GetMethod("FloatingObserved",Flags);int qualified=0;bool gatesLogged=false;
            var dry=attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostProjectileEnvironment",true).GetMethod("Dry",Flags);var observedTerrain=Activator.CreateInstance(terrain,true);
            int initialFlights=flightReads,initialFamilies=familyCalls,published=0,detachedPreviews=0;bool bodyDraw=false,childDraw=false,memberChecked=false;tracingFloating=true;floatingReports=floatingMotionRefusals=0;floatingDetachedHit=null;
            var detachedReceives=new HashSet<long>();
            for(int i=0;i<70;i++)
            {
                naturalPress=i==4;NativeCombatSelectionPreviewChecks.Step(context,physical,naturalPress);if(i==4)physical=new Vector2(550,430);
                bool floats=(bool)floating.Invoke(null,new object[]{player});if(floats)qualified++;
                if(floats && !gatesLogged && Main.projectile.Any(p=>p.active && p.owner==player.whoAmI))
                {gatesLogged=true;Console.WriteLine("ORIGINAL floating current gate targetY="+targetY+" nativeWet="+player.wet+" mounted="+player.MountedCenter+" sources=["+string.Join(";",Main.projectile.Where(p=>p.active && p.owner==player.whoAmI).Select(p=>p.type+" center="+p.Center+" ignoreWater="+p.ignoreWater+" tile="+p.tileCollide+" dry="+dry.Invoke(null,new object[]{p,observedTerrain,p.Center.X,p.Center.Y,p.velocity.X})))+"]");}
                var plan=Get(attack,"preview");var flight=GetOptional(plan,"Flight");var contact=(JueMingR.Features.Combat.AttackContact)GetOptional(attack,"ExpectedPreview");
                if(Call(Get(Get(host,"Prediction"),"Cache"),"Read",1)!=null)published++;
                if(!floats)continue;
                var members=Main.projectile.Where(p=>p.active && p.owner==player.whoAmI && (p.type==222 || p.type==228)).ToArray();
                bool hasBody=members.Any(p=>p.type==222),hasChild=members.Any(p=>p.type==228);
                if(hasBody)Require(contact==null && flight==null,"a live hand still needs the unsupported future floating owner continuation");
                else if(hasChild && contact!=null)
                {Require(flight!=null,"a child-only contact belongs to an observed sealed family, not a new hypothetical hand");detachedPreviews++;detachedReceives.Add(contact.Timeline.SampleTick+contact.ReceiveTick);}
                if(members.Length>0 && !memberChecked)
                {
                    // This validates a pure member lease on real observed
                    // members; it is not a fabricated Contact or wet forecast.
                    var stateType=attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostMeleeAttack+SpearFlightState",true);
                    var state=stateType.GetConstructor(Flags,null,new[]{typeof(Player),typeof(Projectile[])},null).Invoke(new object[]{player,members});
                    int reads=flightReads,families=familyCalls;drawTerrainReads=0;presenting=true;
                    try
                    {
                        Require((bool)Get(state,"Current"),"real constrained floating owner/member scalar lease is current");
                        bool canFloat=player.canFloatInWater;player.canFloatInWater=false;Require(!player.ShouldFloatInWater && !(bool)Get(state,"Current"),"floating capability change immediately retires the member lease");player.canFloatInWater=canFloat;
                        Require((bool)Get(state,"Current"),"restoring native floating capability restores the same pure member lease");
                    }
                    finally{presenting=false;}
                    Require(drawTerrainReads==0 && flightReads==reads && familyCalls==families,"floating member Current performs no terrain, discovery or replay");memberChecked=true;
                    Console.WriteLine("PASS ORIGINAL constrained floating owner/member Current pure zeroTerrain; this is member validation, not wet prediction support");
                }
                if(hasBody && !bodyDraw || hasChild && !hasBody && !childDraw && contact!=null)
                {
                    Require(player.wet && player.ShouldFloatInWater,"actual constrained floating owner remains native wet for withdrawal Draw");
                    Present(host,attack,Get(host,"World"),player,receiver,graphics,output,"original-floating-phase-"+(hasBody?"body":"child"),!hasBody,true);
                    if(hasBody)bodyDraw=true;else childDraw=true;
                }
            }
            Console.WriteLine("ORIGINAL floating route qualified="+qualified+" publishedSelected="+published+" previewTimeline="+(GetOptional(Get(attack,"preview"),"Timeline")!=null)+" flightReads="+(flightReads-initialFlights)+" familyCalls="+(familyCalls-initialFamilies)+" floatingMotionRefusals="+floatingMotionRefusals+" nativeParentDamage="+parentHits+" nativeChildDamage="+childHits+
                " parentBirths="+parentBirths+" childBirths="+childBirths+" bodyDraw="+bodyDraw+" childDraw="+childDraw+" memberChecked="+memberChecked+" detachedPreviews="+detachedPreviews+" detachedReceiveCount="+detachedReceives.Count+" detachedHit="+floatingDetachedHit+" hitMatched="+(floatingDetachedHit.HasValue && detachedReceives.Contains(floatingDetachedHit.Value)));tracingFloating=false;
            Require(qualified==70 && published==70 && floatingMotionRefusals>0 && familyCalls>initialFamilies && parentBirths==1 && childBirths==1 && childHits>0 && bodyDraw && childDraw && memberChecked && detachedPreviews>0 && floatingDetachedHit.HasValue && detachedReceives.Contains(floatingDetachedHit.Value),"unsupported live hand withdraws while the sealed child's future contact independently matches a native receive after hand retirement");
            Console.WriteLine("PASS ORIGINAL floating hand withdrawal/sealed-child receive="+floatingDetachedHit+" previews="+detachedPreviews+" actual World.Draw; zero extra terrain/discovery/replay; no future floating motion support claimed");
        }
        private static void RunClockCounterexamples(object context,object host,object combat,object input,object attack)
        {
            var window=attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackWindow",true);
            var delayMethod=window.GetMethod("PreviewDelay",Flags);
            // Appearance is not the boundary: gun/bow/non-autoreuse attachment
            // uses all retire the tail before starting; an item with native
            // autoReuse clears it earlier. Observe original Update throughout.
            foreach(var row in new[]{new[]{95,0},new[]{95,1},new[]{39,0},new[]{1201,0},new[]{5669,0},new[]{4672,0}})
            {
                NativeCombatObservationChecks.Save(host,new JueMingR.Features.Combat.ObservationOptions());NativeCombatCadenceChecks.Save(combat,new JueMingR.Features.Combat.CombatOptions());
                player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,row[0],0,0);player.position=new Vector2(700,646);player.ResetEffects();player.ResetFallDamage();player.releaseUseItem=true;
                player.autoReuseAllWeapons=row[1]!=0;player.inventory[54].SetDefaults(player.HeldItem.useAmmo==40?1334:3104);player.inventory[54].stack=player.HeldItem.useAmmo==40?999:1;
                Main.screenPosition=new Vector2(480,400);Main.GameViewMatrix.Zoom=Vector2.One;Main.rand=new Terraria.Utilities.UnifiedRandom(7193);
                receiver=NativeCombatControlPreviewChecks.Dummy(2,55,40);physical=new Vector2(550,430);parentType=player.HeldItem.useAmmo==40?278:player.HeldItem.useAmmo>0?14:player.HeldItem.shoot;childType=0;parentBirths=childBirths=firstCopper=0;assistEnabled=naturalPress=false;clockChildren.Clear();clockChildHit=null;born.Clear();
                NativeCombatObservationChecks.Save(host,new JueMingR.Features.Combat.ObservationOptions(false,true,true,false,true,25,true,false));
                for(int i=0;i<4;i++)NativeCombatSelectionPreviewChecks.Step(context,physical,false,true);
                Require(GetOptional(attack,"ExpectedPreview")!=null && parentBirths==0,"unpressed original clock counterexample has a read-only preview and no source");
                naturalPress=true;
                var cooldownClocks=new List<Tuple<uint,int>>();
                for(int i=0;i<60 && (i==0 || player.itemAnimation!=1);i++)
                {NativeCombatSelectionPreviewChecks.Step(context,physical,true);if(player.HeldItem.useTime==player.HeldItem.useAnimation)cooldownClocks.Add(Tuple.Create(Main.GameUpdateCount,(int)delayMethod.Invoke(null,new object[]{player,player.HeldItem})));}
                Require(player.itemAnimation==1 && parentBirths>0,"original clock counterexample reaches a natural last animation step");
                bool nativeAuto=player.HeldItem.autoReuse;uint tail=Main.GameUpdateCount;int prior=parentBirths,tailTime=player.itemTime;int delay=(int)delayMethod.Invoke(null,new object[]{player,player.HeldItem});
                if(nativeAuto)
                {for(int i=0;i<10 && parentBirths==prior;i++)NativeCombatSelectionPreviewChecks.Step(context,physical,true);Require(parentBirths==prior+1,"native item autoReuse clears the tail while its remaining itemTime still owns the first birth gate");}
                else
                {naturalPress=false;NativeCombatSelectionPreviewChecks.Step(context,physical,false);Require(parentBirths==prior && player.itemAnimation==0,"original non-autoreuse tail only ends even with attachment reuse");Require(GetOptional(attack,"ExpectedPreview")!=null,"released zero-timer original weapon keeps its independent hypothetical preview");naturalPress=true;NativeCombatSelectionPreviewChecks.Step(context,physical,true);Require(parentBirths==prior+1,"fresh native hand starts only after the old tail has ended");}
                int actual=(int)(Main.GameUpdateCount-tail);Console.WriteLine("ORIGINAL shared-tail item="+row[0]+" attachment="+row[1]+" nativeAuto="+nativeAuto+" tailTime="+tailTime+" previewDelay="+delay+" actualBirthActions="+actual+" births=["+string.Join(";",born)+"]");
                Require(delay==actual-1 && delay==Math.Max(nativeAuto?0:1,tailTime-1),"shared ordinary preview clock equals each actual native hand opening, keeping item/attachment autoreuse and the actual burst time distinct");
                foreach(var sample in cooldownClocks)Require(sample.Item2==(int)(Main.GameUpdateCount-sample.Item1)-1,"every natural complete-hand cooldown sample predicts the same actual next birth: item="+row[0]+" attachment="+row[1]+" tick="+sample.Item1+" predicted="+sample.Item2+" actual="+((int)(Main.GameUpdateCount-sample.Item1)-1));
                Console.WriteLine("PASS ORIGINAL full cooldown clock item="+row[0]+" attachment="+row[1]+" samples="+cooldownClocks.Count);
            }
            Console.WriteLine("ORIGINAL autoReuse AI019 roles=["+string.Join(",",Terraria.ID.ContentSamples.ItemsByType.Values.Where(item=>item.autoReuse && item.shoot>0 && Terraria.ID.ContentSamples.ProjectilesByType[item.shoot].aiStyle==19).Select(item=>item.type.ToString()))+"]");
        }
    }
}
