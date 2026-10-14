using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // These isolated stationary receivers test selection and the actual outer
    // preparation/display boundary. NPC family motion has separate original
    // oracles; no synthetic contact or private Solve is inserted here.
    internal static class NativeCombatSelectionPreviewChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static bool injectPreviewFault;private static int previewFaults;
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var input=Get(context,"Input");var attack=Get(combat,"Attack");
            try
            {
                NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                RankedChanges(context,host,input);
                foreach(bool mouse in new[]{false,true})foreach(bool clear in new[]{false,true})
                {
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.releaseUseItem=true;p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;Main.screenPosition=new Vector2(550,400);
                    var a=Scene(2,new Vector2(800,667));var b=Scene(3,new Vector2(600,667));
                    for(int y=0;y<Main.maxTilesY;y++){Main.tile[47,y].active(true);Main.tile[47,y].type=1;}
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,clear,mouse,false,50,true,true));
                    for(int i=0;i<3;i++)Step(context,a.Center,false);
                    Require(Selected(host)==(clear?3:2),"near blocked A/far actual reachable B respects centre and mode before attacking: mouse="+mouse+" clear="+clear);
                    int selected=Selected(host);Step(context,a.Center,true);Require(Selected(host)==selected,"attack input does not change the selection contract");
                    // BeforeNpc releases reader0; the real display owns its
                    // validated immutable window independently of that reader.
                    var visible=GetOptional(host,"DisplayPath") as NpcTrajectory;
                    Console.WriteLine("SELECTION actual path mouse="+mouse+" clear="+clear+" selected="+selected+" display="+(visible==null?"none":visible.Identity.Slot+"/"+visible.SampleTick+"/"+visible.CaptureTick));
                    Require(visible!=null && visible.Identity.Slot==selected,"actual visible path uses the final shared selection");
                    Console.WriteLine("PASS finite actual candidate selection mouse="+mouse+" clear="+clear+" selected="+selected);
                }
                foreach(int weapon in new[]{3278,5669})
                {
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,weapon,0,0);p.position=new Vector2(700,646);Main.screenPosition=new Vector2(550,400);
                    var a=Scene(2,new Vector2(1250,667));Scene(3,new Vector2(780,667));
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,false,50,true,true));Step(context,a.Center,false);
                    Require(Selected(host)==2,"nearest mouse target remains selected outside current yoyo/melee reach: "+weapon);
                }
                if((bool)Get(attack,"PreviewDemand"))
                {Idle(context,combat,host,input,attack);StaticImmunity(context,combat,host,input,attack);PreviewFaultIsolation(context,combat,host,input,attack);}
                else Require(GetOptional(attack,"ExpectedPreview")==null,"retired hypothetical display remains absent through selection changes");
            }
            finally{NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());}
        }
        private static void RankedChanges(object context,object host,object input)
        {
            foreach(bool mouse in new[]{false,true})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);
                p.position=new Vector2(700,646);p.releaseUseItem=true;p.inventory[54].SetDefaults(3104);p.inventory[54].stack=1;Main.screenPosition=new Vector2(350,400);
                var near=Scene(2,new Vector2(810,667));var middle=Scene(3,new Vector2(870,667));var far=Scene(4,new Vector2(460,667));var cursor=new Vector2(725,667);
                for(int y=0;y<Main.maxTilesY;y++)NativeToolsChecks.Tile(47,y,1);
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,mouse,false,50,true,false));
                var attack=Get(Get(context,"Combat"),"Attack");int cadence=(int)host.GetType().GetField("DisplayRefreshUpdates",Flags).GetRawConstantValue();
                // The owner requires cold work to remain visible in evidence,
                // not a cold eleven-update guarantee derived from cadence.
                // Preserve that exact diagnostic window, then establish a
                // separate fixed warm fixture before checking changed facts.
                // Neither interval grants BeforeNpc input or restarts a budget.
                bool coldWindowMet=false;
                for(int i=0;i<cadence*2+1;i++)
                {
                    Step(context,cursor,false,true);
                    coldWindowMet|=Selected(host)==4 && RankedContact(host)!=null;
                    var farPlan=((Array)Get(attack,"candidatePlans")).GetValue(8);var farContact=farPlan==null?null:GetOptional(farPlan,"Contact") as AttackContact;
                    Console.WriteLine("RANKED POSTSTEP index="+i+" GUC="+Main.GameUpdateCount+" selected="+Selected(host)+" reachable="+Get(Get(host,"Selection"),"ClearLine")+" display="+(((NpcTrajectory)GetOptional(host,"DisplayPath"))?.Count??0)+" optionalTick="+Get(host,"optionalTick")+" farPrepared="+(farPlan!=null && (bool)Get(farPlan,"Prepared"))+" preparedTick="+(farPlan==null?0:Get(farPlan,"PreparedTick"))+" phase="+(farPlan==null?null:Get(farPlan,"Phase"))+" sample="+(farContact==null?-1:farContact.Timeline.SampleTick)+" receive="+(farContact==null?-1:farContact.ReceiveTick)+" work="+Get(Get(host,"PredictionWork"),"UsedMilliseconds")+" exceeded="+Get(Get(host,"PredictionWork"),"Exceeded"));
                }
                Console.WriteLine("RANKED COLD DIAGNOSTIC updates="+(cadence*2+1)+" originalElevenUpdateSignal="+coldWindowMet+"; observation-only, not a product deadline or warm PASS");
                int warmProofs=0,recentWarmProofs=0,firstWarmProof=-1,hardWarmExits=0,warmDisplaySteps=0,warmDisplayGap=0,maxWarmDisplayGap=0,minimumDisplayCount=int.MaxValue,maximumDisplayCount=0;double maximumWarmWork=0;
                for(int i=0;i<60;i++)
                {
                    Step(context,cursor,false,true);
                    bool qualified=Selected(host)==4 && RankedContact(host)!=null;
                    if(qualified){warmProofs++;if(firstWarmProof<0)firstWarmProof=i;if(i>=60-cadence)recentWarmProofs++;}
                    var display=GetOptional(host,"DisplayPath") as NpcTrajectory;
                    bool visible=Selected(host)==4 && (bool)Get(Get(host,"Selection"),"ClearLine") && display!=null && display.Identity.Slot==4;
                    if(visible){warmDisplaySteps++;warmDisplayGap=0;minimumDisplayCount=Math.Min(minimumDisplayCount,display.Count);maximumDisplayCount=Math.Max(maximumDisplayCount,display.Count);}else maxWarmDisplayGap=Math.Max(maxWarmDisplayGap,++warmDisplayGap);
                    var work=Get(host,"PredictionWork");if((bool)Get(work,"Exceeded"))hardWarmExits++;maximumWarmWork=Math.Max(maximumWarmWork,(double)Get(work,"UsedMilliseconds"));
                }
                var warmDisplay=GetOptional(host,"DisplayPath") as NpcTrajectory;
                Console.WriteLine("RANKED FIXED WARM SETUP updates=60 mouse="+mouse+" freshProofs="+warmProofs+" recentFiveUpdateProofs="+recentWarmProofs+" firstProofIndex="+firstWarmProof+" hardExits="+hardWarmExits+" maximumChargedMs="+maximumWarmWork+" selected="+Selected(host)+" reachable="+Get(Get(host,"Selection"),"ClearLine")+" displayIdentity="+(warmDisplay==null?"none":warmDisplay.Identity.ToString())+" displayCount="+(warmDisplay?.Count??0)+" actualFarPathSteps="+warmDisplaySteps+" longestPathGap="+maxWarmDisplayGap+" pathCountRange="+(warmDisplaySteps==0?0:minimumDisplayCount)+".."+maximumDisplayCount);
                Require(Selected(host)==4 && (bool)Get(Get(host,"Selection"),"ClearLine") && warmDisplay!=null && warmDisplay.Identity.Slot==4 && warmProofs>0,"fixed warm fixture ends with the selected reachable farther NPC and its actually validated path; solve frequency is not display continuity: mouse="+mouse);
                for(int y=0;y<Main.maxTilesY;y++)Main.tile[47,y].ClearEverything();
                RankedAdvance(context,host,cursor,2,true,"near target wins at the bounded optional refresh when its wall opens even while old far stays reachable: mouse="+mouse,false);
                for(int y=0;y<Main.maxTilesY;y++)NativeToolsChecks.Tile(47,y,1);
                RankedAdvance(context,host,cursor,4,true,"closing the same wall retires near proof and restores the farther solution within optional cadence: mouse="+mouse);
                var ordinary=(AttackContact)RankedContact(host);
                p.inventory[54].SetDefaults(1302);p.inventory[54].stack=999;
                RankedAdvance(context,host,cursor,4,true,"high-velocity ammo refreshes the farther solution within optional cadence: mouse="+mouse);var fast=(AttackContact)RankedContact(host);
                Require(Selected(host)==4 && fast!=null && ordinary!=null && fast.Tick<ordinary.Tick,"actual high-velocity ammunition preserves wall tiers but replaces the receive time: mouse="+mouse);
                p.inventory[54].TurnToAir();RankedAdvance(context,host,cursor,2,false,"no usable ammunition leaves nearest fallback without retaining old proof: mouse="+mouse);
                p.inventory[54].SetDefaults(3104);p.inventory[54].stack=1;RankedAdvance(context,host,cursor,4,true,"ordinary Endless Pouch restores the actual far solution: mouse="+mouse);
                middle.Center=new Vector2(600,667);RankedAdvance(context,host,cursor,3,true,"a moved formerly blocked middle candidate outranks old reachable far: mouse="+mouse);
                near.Center=new Vector2(660,667);RankedAdvance(context,host,cursor,2,true,"nearer movement is re-ranked within the reachable tier: mouse="+mouse);
                near.Center=new Vector2(810,667);NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,mouse,false,50,true,false));
                Step(context,cursor,false,true);Require(Selected(host)==2,"nearest mode ignores the existing wall and usable farther attack: mouse="+mouse);
                Console.WriteLine("PASS three-candidate ranking/open-close wall/real ammo/movement/nearest: mouse="+mouse+" farStillActive="+far.active);
            }
        }
        private static void RankedAdvance(object context,object host,Vector2 cursor,int expected,bool contactExpected,string reason,bool retirePrior=true)
        {
            var attack=Get(Get(context,"Combat"),"Attack");var prior=RankedContact(host);
            Step(context,cursor,false,true);
            // Opening the near wall changes distance competition, not the
            // still-reachable far contact's own dependencies. Closing a wall,
            // changing ammo or an intervening receiver invalidates old proof.
            Require(!retirePrior || prior==null || !ReferenceEquals(prior,RankedContact(host)),"invalidated hand/receiver facts immediately revoke the old candidate proof before optional re-solving");
            // One five-update due interval plus one bounded fair cycle of the
            // actual candidate set. A denied final candidate remains unresolved
            // and gets its reserved next-pass turn; an optional refresh is not
            // a guarantee that every candidate solve fits that one slice.
            // Never force a solver from a validation read.
            int cadence=(int)host.GetType().GetField("DisplayRefreshUpdates",Flags).GetRawConstantValue();
            int candidates=(int)Get(Get(host,"Selection"),"CandidateCount");
            for(int remaining=cadence+Math.Max(0,candidates-1);remaining>0;remaining--)
            {
                var contact=(AttackContact)RankedContact(host);
                if(Selected(host)==expected && (contactExpected?contact!=null && contact.Timeline.Identity.Slot==expected:contact==null))return;
                Step(context,cursor,false,true);
            }
            var result=(AttackContact)RankedContact(host);
            Require(Selected(host)==expected && (contactExpected?result!=null && result.Timeline.Identity.Slot==expected:result==null),reason);
        }
        private static AttackContact RankedContact(object host)
        {
            var attack=Get(host,"Attack");var identity=(NpcIdentity)Get(Get(host,"Selection"),"Target");
            // A completed result actually admitted by THIS selection call may
            // reuse an older immutable plan after its real dependencies pass.
            // This pointer is cleared at every ResolveSelection entry, so it
            // cannot grant a persistent raw-plan qualification on later calls.
            var admitted=GetOptional(attack,"candidatePresentationPlan");
            if(admitted!=null && (bool)Get(admitted,"Prepared") && Get(admitted,"Phase").ToString()=="CompletedWorld")
            {
                var admittedContact=GetOptional(admitted,"Contact") as AttackContact;var admittedDisplay=GetOptional(host,"DisplayPath") as NpcTrajectory;
                if(admittedContact!=null && admittedContact.Timeline.Identity.Equals(identity) && admittedDisplay!=null && admittedDisplay.Identity.Equals(identity))return admittedContact;
            }
            var plans=(Array)Get(attack,"candidatePlans");var plan=plans.GetValue(identity.Slot*2);
            // The temporary publication pointer is cleared by a repeated
            // outer entry. Inspect only this GUC's actually completed World
            // plan, never an older raw Contact as a persistent qualification.
            if(plan==null || !(bool)Get(plan,"Prepared") || (uint)Get(plan,"PreparedTick")!=Main.GameUpdateCount || Get(plan,"Phase").ToString()!="CompletedWorld")return null;
            var contact=GetOptional(plan,"Contact") as AttackContact;
            return contact!=null && contact.Timeline.SampleTick==(long)Main.GameUpdateCount && contact.Timeline.Identity.Equals(identity) && GetOptional(host,"DisplayPath")!=null?contact:null;
        }
        private static void Idle(object context,object combat,object host,object input,object attack)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.releaseUseItem=true;p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;Main.screenPosition=new Vector2(600,400);var n=Scene(2,new Vector2(910,667));
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,true,false));
            int stack=p.inventory[54].stack,cycling=p.ammoCyclingOffset;
            for(int i=0;i<4;i++)
            {
                Step(context,new Vector2(650,550),false,true);var result=(AttackContact)GetOptional(attack,"ExpectedPreview");
                Require(result!=null && result.ReceiveTick>=0 && result.ReceiveTick<result.Timeline.Count,"idle held weapon creates a real read-only preview with Aim OFF");
                var impact=Get(Get(host,"World"),"Impact");Call(impact,"Capture");var zoom=Main.GameViewMatrix.ZoomMatrix;Call(impact,"Project",zoom,Matrix.Invert(zoom));
                var point=result.ReceiveBounds;var expected=Vector2.Transform(new Vector2(point.CenterX,point.CenterY)-Main.screenPosition,zoom);
                Require((bool)Get(impact,"Visible") && Vector2.DistanceSquared((Vector2)Get(impact,"Position"),expected)<.001f,"red marks the receive-time NPC path centre");
                Require(GetOptional(attack,"ExpectedImpact")==null && !p.controlUseItem && !p.channel && !Main.projectile.Any(q=>q.active),"preview has no native input capability or projectile birth");
            }
            Require(stack==p.inventory[54].stack && cycling==p.ammoCyclingOffset,"idle preview preserves resources and ammunition cycling across natural updates");
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,true,true));Step(context,new Vector2(650,550),false);var preview=(AttackContact)Get(attack,"ExpectedPreview");Call(Get(Get(host,"World"),"Impact"),"Capture");
            Step(context,new Vector2(650,550),true);Require(GetOptional(attack,"ExpectedPreview")!=null,"ordinary cooldown retains a legal delayed preview after a natural shot");
            Require(((AttackContact)Get(attack,"ExpectedPreview")).Delay>0,"ordinary cooldown is represented as an actual future launch delay");
            Console.WriteLine("PASS natural idle path-only preview, centre/time mapping, pure observation and post-shot delayed preview");
        }
        private static void StaticImmunity(object context,object combat,object host,object input,object attack)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,1446,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.statMana=p.statManaMax=p.statManaMax2=200;Main.screenPosition=new Vector2(600,400);var n=NativeCombatControlPreviewChecks.Dummy(2,52,40);
            var shot=ContentSamples.ProjectilesByType[297];uint old=Projectile.perIDStaticNPCImmunity[shot.immunityIdentity,2];
            try
            {
                Projectile.perIDStaticNPCImmunity[shot.immunityIdentity,2]=0;
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,false));
                for(int i=0;i<3;i++)Step(context,n.Center,false,true);
                Require(GetOptional(attack,"ExpectedPreview")!=null,"idle Spectre preview reaches the native placed dummy before a foreign static-immunity wait");
                // Arrange an already-observed foreign hit wait, then let the
                // actual outer clock expire it without rewriting its timestamp.
                uint expiry=Main.GameUpdateCount+130;Projectile.perIDStaticNPCImmunity[shot.immunityIdentity,2]=expiry;
                Step(context,n.Center,false,true);Require(GetOptional(attack,"ExpectedPreview")==null,"a changed selected static immunity invalidates the old contact");
                int recovered=-1;
                for(int i=0;i<132;i++)
                {Step(context,n.Center,false,true);if(recovered<0 && GetOptional(attack,"ExpectedPreview")!=null)recovered=i;Require(Projectile.perIDStaticNPCImmunity[shot.immunityIdentity,2]==expiry,"natural time advances independently of the stored static expiry");}
                Require(recovered>=0 && GetOptional(attack,"ExpectedPreview")!=null,"the same static expiry becomes a usable receive window again without stale null reuse");
                Console.WriteLine("PASS natural preview static-immunity dependency/expiry recovery: item=1446 projectile=297 recovered="+recovered);
            }
            finally{Projectile.perIDStaticNPCImmunity[shot.immunityIdentity,2]=old;}
        }
        private static void PreviewFaultIsolation(object context,object combat,object host,object input,object attack)
        {
            var audit=new Harmony("JueMingR.Tests.SelectionPreviewFault");
            audit.Patch(attack.GetType().GetMethod("PreparePlan",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSelectionPreviewChecks).GetMethod("PreviewFault",Flags)));
            try
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;Main.screenPosition=new Vector2(600,400);var n=Scene(2,new Vector2(910,667));
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,true,false));injectPreviewFault=true;previewFaults=0;
                for(int i=0;i<4;i++)Step(context,n.Center,false,true);
                Require(previewFaults==1 && !(bool)Get(attack,"Failed") && GetOptional(attack,"ExpectedPreview")==null && ((NpcPredictionCache)Get(Get(host,"Prediction"),"Cache")).Read(0)!=null,"same preview fault is tried once, leaving execution and the shared path healthy");
                injectPreviewFault=false;p.inventory[0]=new Item();p.inventory[0].SetDefaults(95);Step(context,n.Center,false,true);
                Require(GetOptional(attack,"ExpectedPreview")!=null && !(bool)Get(attack,"Failed"),"new healthy held instance recovers preview automatically");
                injectPreviewFault=true;Step(context,n.Center,false);Require(previewFaults==2,"new concrete context gets one bounded preview attempt");injectPreviewFault=false;
                var oldCenter=n.Center;typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{2,(byte)0});n=Scene(2,oldCenter);
                Step(context,n.Center,false,true);Require(GetOptional(attack,"ExpectedPreview")!=null,"new full target identity recovers preview without resetting the path");
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,true,true));Step(context,n.Center,true);Require(Main.projectile.Any(q=>q.active && q.owner==0),"a recovered preview fault does not revoke the later real shot capability");
                Console.WriteLine("PASS natural preview fault isolation: bounded retry, healthy path/execution, new weapon and NPC identity recovery");
            }
            finally{injectPreviewFault=false;foreach(var method in audit.GetPatchedMethods().ToArray())audit.Unpatch(method,HarmonyPatchType.All,audit.Id);}
        }
        private static void PreviewFault(){if(injectPreviewFault){previewFaults++;throw new InvalidOperationException("isolated preview preparation fault");}}
        internal static void Step(object context,Vector2 mouse,bool left,bool pureObservation=false)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var input=Get(context,"Input");NativeToolExecutionChecks.Sample(context,input,mouse,left);Call(combat,"Sample");Call(host,"SampleMouse");int x=Main.mouseX,y=Main.mouseY;
            NativeQuickItemChecks.BeginWorldStep();Main.LocalPlayer.Update(0);
            foreach(var n in Main.npc)if(n.active)n.UpdateNPC(n.whoAmI);
            for(int i=0;i<Main.maxProjectiles;i++)if(Main.projectile[i].active){Main.ProjectileUpdateLoopIndex=i;Main.projectile[i].Update(i);}Main.ProjectileUpdateLoopIndex=-1;
            var p=Main.LocalPlayer;int direction=p.direction,animation=p.itemAnimation,time=p.itemTime,mana=p.statMana,stack=p.inventory[54].stack,cycling=p.ammoCyclingOffset;bool use=p.controlUseItem,channel=p.channel;var random=Main.rand;
            var fields=typeof(Terraria.Utilities.UnifiedRandom).GetFields(Flags).Where(f=>!f.IsStatic).ToArray();var before=fields.Select(f=>Copy(f.GetValue(random))).ToArray();
            Call(context,"UpdateRuntime");Call(context,"UpdateShell");Require(Main.mouseX==x && Main.mouseY==y,"physical cursor survives Update/ItemCheck/NPC/Projectile/presentation");
            if(pureObservation)Require(p.direction==direction && p.itemAnimation==animation && p.itemTime==time && p.statMana==mana && p.inventory[54].stack==stack && p.ammoCyclingOffset==cycling && p.controlUseItem==use && p.channel==channel && ReferenceEquals(random,Main.rand) && fields.Select((f,i)=>Equal(before[i],f.GetValue(random))).All(v=>v),"natural completed preview stage is read-only for input, facing, timers, resources and RNG");
        }
        private static NPC Scene(int slot,Vector2 centre)
        {var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.Center=centre;n.velocity=n.netOffset=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=100000;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static int Selected(object host){return ((NpcIdentity)Get(Get(host,"Selection"),"Target")).Slot;}
        private static object Copy(object value){var a=value as Array;return a==null?value:a.Clone();}
        private static bool Equal(object a,object b){var x=a as Array;var y=b as Array;return x==null?Equals(a,b):y!=null && x.Cast<object>().SequenceEqual(y.Cast<object>());}
    }
}
