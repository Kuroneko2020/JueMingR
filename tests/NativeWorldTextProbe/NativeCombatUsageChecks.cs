using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // One native outer schedule supplies both the CPU and actual World.Draw
    // checks. The probe never patches in a contact after a shot is consumed.
    internal static class NativeCombatUsageChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static Harmony drawStepAudit;private static int drawStepScopes,drawNpcSteps;
        private static void CountDrawNpcStep(){drawNpcSteps++;}
        internal static int ObservedNpcSteps
        {
            get{Require(drawStepScopes>0 && drawStepAudit!=null,"actual future Step read requires an installed observer scope");return drawNpcSteps;}
        }
        private sealed class DrawStepScope : IDisposable
        {
            private bool disposed;
            public void Dispose()
            {
                if(disposed)return;disposed=true;
                if(--drawStepScopes==0){drawStepAudit.UnpatchAll(drawStepAudit.Id);drawStepAudit=null;}
            }
        }
        internal static IDisposable ObserveDrawSteps()
        {
            if(drawStepScopes==0)
            {
                var methods=typeof(NpcMotion).GetMethods(Flags).Where(m=>m.Name=="Step" && m.GetParameters().Length==8).ToArray();
                Require(methods.Length>0,"actual future producer Step observer is present in the loaded candidate");
                var audit=new Harmony("JueMingR.Tests.UsageDrawFutureSteps");
                try{foreach(var method in methods)audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatUsageChecks),nameof(CountDrawNpcStep)));}
                catch{audit.UnpatchAll(audit.Id);throw;}
                drawStepAudit=audit;drawNpcSteps=0;
            }
            drawStepScopes++;return new DrawStepScope();
        }
        private static Player player;private static NPC receiver;private static int births,hits,aimedBirths,ordinaryChildren,useFrame,bubbleOrderChanges,bubbleAssistedChanges;private static Vector2 physical;private static uint? solarDue;private static bool solarWaitVerified,observeUse;
        private sealed class KillWitness {internal bool Changed;internal string Details;}
        private static void Born(Player __instance,Projectile __0)
        {
            if(!observeUse || !ReferenceEquals(__instance,player) || __0.owner!=player.whoAmI)return;
            births++;
            if(__0.type==611 && solarDue.HasValue)
            {Require(Main.GameUpdateCount==solarDue.Value,"late Solar preview waits until the original next source actually appears: expected="+solarDue+" actual="+Main.GameUpdateCount);solarDue=null;solarWaitVerified=true;}
            if(__0.type==14 || __0.type==278)ordinaryChildren++;
            if(__0.type!=player.HeldItem.shoot && __0.type!=14 && __0.type!=278 && __0.type!=444 && __0.type!=615 && __0.type!=611)return;
            if(__0.velocity.LengthSquared()<.001f)return;
            // Bubble velocity contains native +/-60 degree random spread.
            // Its stored AI angle is the consumed initial aim; the later
            // ordinary bullet consumes Kill's exact cursor without spread.
            var ray=__0.type==444?__0.ai[0].ToRotationVector2():Vector2.Normalize(__0.velocity);var raw=(physical-__0.Center).SafeNormalize(-Vector2.UnitX);var aim=(receiver.Center-__0.Center).SafeNormalize(Vector2.UnitX);
            Require(Vector2.Dot(ray,aim)>.9f && Vector2.Dot(ray,raw)<.9f,"native consumer gets the assisted direction, distinct from the physical mouse: item="+player.HeldItem.type+" frame="+useFrame+" projectile="+__0.type+" origin="+__0.Center+" target="+receiver.Center+" velocity="+__0.velocity+" aimDot="+Vector2.Dot(ray,aim)+" rawDot="+Vector2.Dot(ray,raw));
            aimedBirths++;
        }
        private static void Damage(NPC __instance,int __result){if(ReferenceEquals(__instance,receiver) && __result>0)hits++;}
        private static void KillBefore(object __instance,Projectile __0,out KillWitness __state)
        {
            __state=null;if(player==null || __0.type!=444)return;
            var shots=(System.Collections.IDictionary)Get(__instance,"shots");object entry=shots[(int)__0.key];
            if(entry==null){__state=new KillWitness{Details="missing receipt"};return;}
            var obstacles=GetOptional(entry,"Obstacles");
            int free=Array.FindIndex(Main.projectile,q=>!q.active);bool changed=free>=0 && (free>__0.whoAmI)!=(bool)Get(entry,"ChildLaterSlot");
            __state=new KillWitness{Changed=changed,Details="frame="+useFrame+" slot="+__0.whoAmI+" center="+__0.Center+" expiry="+Get(entry,"NextKillOrigin")+" origin="+Get(entry,"KillOrigin")+" contact="+(GetOptional(entry,"Contact")!=null)+" next="+(GetOptional(entry,"NextKillContact")!=null)+" prepared="+Get(entry,"Prepared")+" tick="+Main.GameUpdateCount+" later="+Get(entry,"ChildLaterSlot")+" firstFree="+free+" environment="+Get(Get(__instance,"environmentState"),"Current")+" terrain="+Get(Get(__instance,"terrain"),"Unchanged")+" obstacles="+(obstacles==null || (bool)Get(obstacles,"Unchanged"))};
        }
        private static void KillAfter(object __result,KillWitness __state)
        {if(__state==null)return;if(__state.Changed){bubbleOrderChanges++;if(__result!=null)bubbleAssistedChanges++;}if(__result==null)Console.WriteLine("USAGE bubble unassisted Kill "+__state.Details);}
        internal static void Run(object context,ProbeGraphics graphics=null,string output=null)
        {
            // One observer lifetime for the real graphics group, including
            // nested original-feedback leaves; no patching in each Draw.
            using(var steps=graphics==null?null:ObserveDrawSteps())RunCore(context,graphics,output);
        }
        private static void RunCore(object context,ProbeGraphics graphics,string output)
        {
            if(output!=null)Directory.CreateDirectory(output);
            var usageHost=Get(context,"CombatObservation");
            Console.WriteLine("USAGE fixture initialLayer="+Get(usageHost,"LayerStatus")+" actualDraw="+(graphics!=null));
            Set(usageHost,"LayerStatus",Enum.Parse(Get(usageHost,"LayerStatus").GetType(),"Ready"));Main.screenWidth=960;Main.screenHeight=640;Main.UIScale=1;
            if(Environment.GetEnvironmentVariable("JMR_USAGE_ORIGINAL_FEEDBACK")=="basic-aim"){NativeCombatBasicAimChecks.Run(context);return;}
            if(Environment.GetEnvironmentVariable("JMR_USAGE_ORIGINAL_FEEDBACK")=="spears"){NativeCombatOriginalFeedbackChecks.RunSpears(context,graphics,output);return;}
            if(Environment.GetEnvironmentVariable("JMR_USAGE_ORIGINAL_FEEDBACK")=="hand"){NativeCombatOriginalFeedbackChecks.RunHand(context,graphics,output);return;}
            if(Environment.GetEnvironmentVariable("JMR_USAGE_ORIGINAL_FEEDBACK")=="ranged"){NativeCombatOriginalRangedChecks.Run(context,graphics,output);return;}
            if(Environment.GetEnvironmentVariable("JMR_USAGE_ORIGINAL_FEEDBACK")=="seams"){NativeCombatOriginalFeedbackChecks.RunSeams(context,graphics,output);return;}
            if(Environment.GetEnvironmentVariable("JMR_USAGE_ORIGINAL_FEEDBACK")=="whip-next"){NativeCombatOriginalFeedbackChecks.RunSpears(context,graphics,output);return;}
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");var input=Get(context,"Input");var world=Get(host,"World");
            var audit=new Harmony("JueMingR.Tests.ActualAttackUsage");
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(Player).GetMethod("TryUpdateChannel",Flags),postfix:new HarmonyMethod(typeof(NativeCombatUsageChecks).GetMethod(nameof(Born),Flags)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatUsageChecks).GetMethod(nameof(Damage),Flags)));
            audit.Patch(Get(attack,"Control").GetType().GetMethod("BeginKill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatUsageChecks).GetMethod(nameof(KillBefore),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatUsageChecks).GetMethod(nameof(KillAfter),Flags)));
            var failures=new System.Collections.Generic.List<string>();
            try
            {
                var types=new[]{95,1201,3475,2797,3473};string selected=Environment.GetEnvironmentVariable("JMR_USAGE_ITEM");
                if(!string.IsNullOrEmpty(selected))
                {int type;if(!int.TryParse(selected,out type) || !types.Contains(type))throw new ArgumentException("Unknown actual usage item: "+selected);types=new[]{type};}
                Console.WriteLine("USAGE native requested items="+string.Join(",",types)+" actualDraw="+(graphics!=null));
                foreach(int type in types)
                {
                    try
                    {
                        NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                        player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,type,0,0);player.position=new Vector2(700,646);player.ResetEffects();player.ResetFallDamage();player.releaseUseItem=true;player.statMana=player.statManaMax=player.statManaMax2=200;
                        player.inventory[54].SetDefaults(player.HeldItem.useAmmo==40?1334:3104);player.inventory[54].stack=player.HeldItem.useAmmo==40?999:1;
                        Main.screenPosition=new Vector2(480,400);Main.GameViewMatrix.Zoom=Vector2.One;Main.rand=new Terraria.Utilities.UnifiedRandom(7193);
                        receiver=NativeCombatControlPreviewChecks.Dummy(2,55,40);physical=new Vector2(650,430);births=hits=aimedBirths=ordinaryChildren=bubbleOrderChanges=bubbleAssistedChanges=0;solarDue=null;solarWaitVerified=false;observeUse=true;
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));
                        var idleDisplay=new DisplayCoverage();var useDisplay=new DisplayCoverage();
                        for(int i=0;i<4;i++)
                        {
                            NativeCombatSelectionPreviewChecks.Step(context,physical,false,true);
                            idleDisplay.Record(Present(host,attack,world,graphics,output,"idle-"+type+"-"+i,true,i==3));
                            Require(births==0 && !Main.projectile.Any(q=>q.active) && !player.controlUseItem && !player.channel,"unpressed path preview creates neither native attack nor controller: "+type);
                        }
                        receiver.dontTakeDamage=true;NativeCombatSelectionPreviewChecks.Step(context,physical,false,true);Present(host,attack,world,graphics,output,"receiver-unavailable-"+type,false,false);
                        receiver.dontTakeDamage=false;NativeCombatSelectionPreviewChecks.Step(context,physical,false,true);Present(host,attack,world,graphics,output,"receiver-restored-"+type,true,false);
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));
                        int duration=type==2797 || type==3475?75:45;int shown=0,delayed=0,live=0;
                        for(int frame=0;frame<duration;frame++)
                        {
                            useFrame=frame;
                            NativeCombatSelectionPreviewChecks.Step(context,physical,true);
                            var contact=(AttackContact)GetOptional(attack,"ExpectedPreview");
                            if(contact!=null){shown++;if(contact.Delay>0)delayed++;}
                            if(type==3473 && contact!=null && contact.Delay>0 && !solarWaitVerified && !solarDue.HasValue)solarDue=Main.GameUpdateCount+(uint)contact.Delay+1;
                            if(Main.projectile.Any(q=>q.active && q.owner==0 && (q.type==611 || q.type==615 || q.type==444)))live++;
                            // The original finite interval is the denominator.
                            // Optional display may miss a budgeted update; actual
                            // input, damage and every rendered source stay strict.
                            useDisplay.Record(Present(host,attack,world,graphics,output,"use-"+type+"-"+frame,true,frame==0 || frame==duration-1));
                            if(frame==0)Require(births>0 && aimedBirths>0,"first real click reaches its original directed source: "+type);
                        }
                        // Fifteen ordinary updates span three display refresh
                        // opportunities (about a quarter second at 60 Hz).
                        // This supplied scene must not remain blank that long;
                        // the independent four cold idle updates stay reported.
                        Require(useDisplay.Present>0 && shown>0 && useDisplay.LongestMissing<15,"the unchanged finite use interval repeatedly acquires presentation without sustained blank periods: "+type);
                        Require(hits>0 && aimedBirths>0,"finite complete use reaches real StrikeNPC on the placed training dummy: "+type);
                        if(type==2797 || type==3475)Require(ordinaryChildren>0 && live>0,"native ongoing parent reaches its real ammunition/second stage: "+type);
                        if(type==2797)Require(bubbleOrderChanges>0 && bubbleAssistedChanges==bubbleOrderChanges,"earlier sibling release changes real child update order and every changed Kill consumes its prepared alternative");
                        if(type==95 || type==1201)Require(delayed>0,"normal cooldown retains a future launch preview: "+type);
                        if(type==3473)Require(solarWaitVerified && delayed>0,"late sealed Solar arc previews its actual delayed next use and verifies the next native birth");
                        for(int i=0;i<4;i++)NativeCombatSelectionPreviewChecks.Step(context,physical,false);
                        if(type==2797 || type==3475)
                        {observeUse=false;player.inventory[1].SetDefaults(3509);player.selectedItemState.Select(1);}
                        else player.inventory[0].SetDefaults(3509);
                        NativeCombatSelectionPreviewChecks.Step(context,physical,false);
                        Present(host,attack,world,graphics,output,"switch-away-"+type,false,true);
                        Require(!player.controlUseItem && !player.channel,"release and item switch preserve native input state: "+type);
                        if(type==2797 || type==3475)
                        {
                            // Keep the actual original item and use vanilla's
                            // buffered slot-selection boundary. Returning to
                            // it must acquire new natural consumer receipts.
                            for(int i=0;i<35;i++)NativeCombatSelectionPreviewChecks.Step(context,physical,false);
                            player.selectedItemState.Select(0);
                            for(int i=0;i<4;i++)NativeCombatSelectionPreviewChecks.Step(context,physical,false);
                            Require(ReferenceEquals(player.HeldItem,player.inventory[0]) && player.HeldItem.type==type,"native switch returns to the original controller weapon");
                            int returned=aimedBirths,returnedAmmo=ordinaryChildren;observeUse=true;
                            for(int i=0;i<45;i++){useFrame=duration+40+i;NativeCombatSelectionPreviewChecks.Step(context,physical,true);}
                            Require(aimedBirths>returned && ordinaryChildren>returnedAmmo,"switch-back fresh press reaches real directed controller and ammunition consumers: "+type);
                            int repeated=aimedBirths;
                            for(int i=0;i<4;i++)NativeCombatSelectionPreviewChecks.Step(context,physical,false);
                            for(int i=0;i<45;i++){useFrame=duration+89+i;NativeCombatSelectionPreviewChecks.Step(context,physical,true);}
                            Require(aimedBirths>repeated,"natural release/repress after normal cooldown reacquires assisted input: "+type);
                            Present(host,attack,world,graphics,output,"restored-use-"+type,true,true);
                            Console.WriteLine("PASS actual controller recovery item="+type+" switchBackDirected="+(aimedBirths-returned)+" repressedDirected="+(aimedBirths-repeated)+" actualAmmo="+(ordinaryChildren-returnedAmmo));
                        }
                        Console.WriteLine("PASS actual use item="+type+" idleDisplay="+idleDisplay+" useDisplay="+useDisplay+" preview="+shown+"/"+duration+" delayed="+delayed+" live="+live+" directedBirths="+aimedBirths+" childAmmo="+ordinaryChildren+" actualDamage="+hits+" bubbleOrderChanges="+bubbleAssistedChanges+"/"+bubbleOrderChanges+" draw="+(graphics!=null));
                    }
                    catch(Exception error){failures.Add(type+": "+error.Message);Console.WriteLine("FAIL actual use item="+type+": "+error);}
                }
                Require(failures.Count==0,"actual usage failures: "+string.Join(" | ",failures));
            }
            finally{player=null;receiver=null;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());}
            // Default CPU integration consumes every original-feedback leaf.
            // Explicit item/subset entries remain the narrow diagnosis path;
            // actual GPU leaves are dispatched separately to limit repetition.
            if(graphics==null && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JMR_USAGE_ITEM")))
            {NativeCombatOriginalFeedbackChecks.RunHand(context);NativeCombatOriginalFeedbackChecks.RunSpears(context);NativeCombatOriginalRangedChecks.Run(context);}
            else if(graphics!=null && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JMR_USAGE_ITEM")))NativeCombatOriginalFeedbackChecks.RunSpears(context,graphics,output);
        }
        private static bool Red(Color value){return value.A>100 && value.R>190 && value.G<110 && value.B<110;}
        private sealed class DisplayCoverage
        {
            internal int Present;private int total,gap,longest,runs;
            internal int LongestMissing {get{return longest;}}
            internal void Record(bool visible){total++;if(visible){Present++;gap=0;}else{if(gap==0)runs++;longest=Math.Max(longest,++gap);}}
            public override string ToString(){return Present+"/"+total+" longestMissing="+longest+" gapRuns="+runs;}
        }
        private static bool Present(object host,object attack,object world,ProbeGraphics graphics,string output,string name,bool expected,bool save)
        {return PresentNative(host,attack,world,player,receiver,graphics,output,name,expected,save,true);}
        internal static bool PresentNative(object host,object attack,object world,Player observedPlayer,NPC observedReceiver,ProbeGraphics graphics,string output,string name,bool expected,bool save,bool optionalDisplay=false,Action captureCompleted=null)
        {
            var contact=(AttackContact)GetOptional(attack,"ExpectedPreview");
            Require(expected?(optionalDisplay || contact!=null):contact==null,"finite stable preview/revocation at "+name+" timers="+observedPlayer.itemAnimation+"/"+observedPlayer.itemTime+" direction="+observedPlayer.direction+" sources="+Main.projectile.Count(q=>q.active)+" receiver="+observedReceiver.active+"/"+observedReceiver.dontTakeDamage);
            var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");
            if(contact!=null)
            {
                var displayed=GetOptional(host,"DisplayPath") as NpcTrajectory;
                long now=Main.GameUpdateCount;int maximumAge=(int)host.GetType().GetField("DisplayMaximumAge",Flags).GetValue(null);
                Require(displayed!=null && contact.Timeline.Identity.Equals(displayed.Identity) && ReferenceEquals(contact.Timeline.Identity.Token,observedReceiver) && contact.Timeline.Identity.Slot==observedReceiver.whoAmI && contact.Timeline.CaptureTick==displayed.CaptureTick && ReferenceEquals(contact.Timeline.CalculationToken,displayed.CalculationToken),"preview has the complete selected identity/capture/calculation of the validated display source: "+name);
                long offset=contact.Timeline.SampleTick-contact.Timeline.CaptureTick,receive=contact.Timeline.SampleTick+contact.ReceiveTick;
                Require(now-contact.Timeline.CaptureTick>=0 && now-contact.Timeline.CaptureTick<=maximumAge && offset>=0 && contact.Timeline.SampleTick<=now && contact.ReceiveTick>=0 && contact.ReceiveTick<contact.Timeline.Count && receive>=displayed.SampleTick && receive<displayed.SampleTick+displayed.Count && receive>=now,"preview's absolute receive and window offset stay within the captured display future: "+name);
                Require(contact.Timeline[contact.ReceiveTick].Bounds.Equals(displayed[(int)(receive-displayed.SampleTick)].Bounds),"preview receive geometry equals the independent display source point at its absolute window offset: "+name);
            }
            var execution=GetOptional(attack,"ExpectedImpact") as AttackContact;
            if(execution!=null)
            {
                var published=cache.Read(1);var timeline=execution.Timeline;
                Require(published!=null && timeline.Identity.Equals(published.Identity) && timeline.SampleTick==published.SampleTick && timeline.CaptureTick==published.CaptureTick && (ReferenceEquals(timeline,published) || timeline.CalculationToken!=null && ReferenceEquals(timeline.CalculationToken,published.CalculationToken)) && execution.ReceiveTick>=0 && execution.ReceiveTick<timeline.Count && execution.ReceiveTick<published.Count && timeline[execution.ReceiveTick].Bounds.Equals(published[execution.ReceiveTick].Bounds),"execution marker has the independent published action identity/sample/calculation and receive geometry: "+name);
            }
            Call(world,"Prepare");var zoom=Main.GameViewMatrix.ZoomMatrix;var impact=Get(world,"Impact");
            var actual=GetOptional(impact,"contact") as AttackContact;
            // Capture additionally needs the validated path's visual lease.
            // A legitimate execution contact alone does not force optional
            // display on a frame whose path has expired or was deferred.
            var admittedDisplay=GetOptional(host,"DisplayPath") as NpcTrajectory;
            if(name=="use-3475-0")Console.WriteLine("USAGE capture boundary tick="+Main.GameUpdateCount+" display="+(admittedDisplay==null?"null":admittedDisplay.Identity.Slot+"/"+admittedDisplay.SampleTick+"/"+admittedDisplay.CaptureTick)+" preview="+ContactFact(contact)+" execution="+ContactFact(execution)+" actual="+ContactFact(actual));
            Require(ReferenceEquals(actual,admittedDisplay==null?null:execution??contact),"actual marker captures the independently admitted execution before preview under a valid display lease: "+name);
            if(!expected)Require(actual==null,"negative presentation revokes both execution and preview: "+name);
            else if(!optionalDisplay)Require(actual!=null,"required component presentation remains present: "+name);
            if(actual!=null)Require(ReferenceEquals(actual.Timeline.Identity.Token,observedReceiver) && actual.Timeline.Identity.Slot==observedReceiver.whoAmI && actual.Timeline.Identity.Equals(Get(Get(host,"Selection"),"Target")) && actual.ReceiveTick>=0 && actual.ReceiveTick<actual.Timeline.Count,"actual marker retains the complete selected receiver and legal receive: "+name);
            captureCompleted?.Invoke();
            bool visible=actual!=null;
            if(graphics==null){Call(impact,"Project",zoom,Matrix.Invert(zoom));Require((bool)Get(impact,"Visible")==visible,"natural capture/project at "+name);return visible;}
            Require(drawStepScopes>0 && drawStepAudit!=null,"actual Draw future-work observer is installed for this group");
            int steps=ObservedNpcSteps;var pixels=graphics.Pixels(()=>Call(world,"Draw"),zoom);var red=pixels.Select((v,i)=>new{v,i}).Where(p=>Red(p.v)).Select(p=>new Vector2(p.i%960,p.i/960)).ToArray();
            Require((red.Length>0)==visible && ObservedNpcSteps==steps,"natural actual World.Draw red presence without real future Step work: "+name);
            if(visible)
            {
                var b=actual.Timeline[actual.ReceiveTick].Bounds;var center=Vector2.Transform(new Vector2(b.CenterX,b.CenterY)-Main.screenPosition,zoom);
                Require(red.Length>=16 && red.Length<=100 && red.All(p=>Math.Abs(p.X-center.X)<=6 && Math.Abs(p.Y-center.Y)<=6) && !Red(pixels[(int)center.Y*960+(int)center.X]),"actual hollow red pixels surround the same receive-time NPC path position: "+name);
            }
            if(save && output!=null)graphics.Image(Path.Combine(output,name+".png"),()=>Call(world,"Draw"),zoom);
            Require(ObservedNpcSteps==steps,"exported actual World.Draw also performs no real future Step work: "+name);
            return visible;
        }
        private static string ContactFact(AttackContact contact)
        {return contact==null?"null":contact.Timeline.Identity.Slot+"/sample"+contact.Timeline.SampleTick+"/capture"+contact.Timeline.CaptureTick+"/receive"+contact.ReceiveTick+"/absolute"+(contact.Timeline.SampleTick+contact.ReceiveTick)+"/object"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(contact);}
    }
}
