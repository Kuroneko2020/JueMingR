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
        private static Player player;private static NPC receiver;private static int births,hits,aimedBirths,ordinaryChildren,useFrame,bubbleOrderChanges,bubbleAssistedChanges;private static Vector2 physical;private static uint? solarDue;private static bool solarWaitVerified;
        private sealed class KillWitness {internal bool Changed;internal string Details;}
        private static void Born(Player __instance,Projectile __0)
        {
            if(!ReferenceEquals(__instance,player) || __0.owner!=player.whoAmI)return;
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
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");var input=Get(context,"Input");var world=Get(host,"World");
            var audit=new Harmony("JueMingR.Tests.ActualAttackUsage");
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(Player).GetMethod("TryUpdateChannel",Flags),postfix:new HarmonyMethod(typeof(NativeCombatUsageChecks).GetMethod(nameof(Born),Flags)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatUsageChecks).GetMethod(nameof(Damage),Flags)));
            audit.Patch(Get(attack,"Control").GetType().GetMethod("BeginKill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatUsageChecks).GetMethod(nameof(KillBefore),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatUsageChecks).GetMethod(nameof(KillAfter),Flags)));
            var failures=new System.Collections.Generic.List<string>();
            try
            {
                Set(host,"LayerStatus",Enum.Parse(Get(host,"LayerStatus").GetType(),"Ready"));Main.screenWidth=960;Main.screenHeight=640;Main.UIScale=1;
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
                        receiver=NativeCombatControlPreviewChecks.Dummy(2,55,40);physical=new Vector2(650,430);births=hits=aimedBirths=ordinaryChildren=bubbleOrderChanges=bubbleAssistedChanges=0;solarDue=null;solarWaitVerified=false;
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));
                        for(int i=0;i<4;i++)
                        {
                            NativeCombatSelectionPreviewChecks.Step(context,physical,false,true);
                            Present(host,attack,world,graphics,output,"idle-"+type+"-"+i,true,i==3);
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
                            // These receivers are fixed, in range, unobstructed,
                            // and supplied for the entire finite interval. A
                            // current hit or the next legal use fits 120 ticks.
                            Present(host,attack,world,graphics,output,"use-"+type+"-"+frame,true,frame==0 || frame==duration-1);
                            if(frame==0)Require(births>0 && aimedBirths>0,"first real click reaches its original directed source: "+type);
                        }
                        Require(hits>0 && aimedBirths>0,"finite complete use reaches real StrikeNPC on the placed training dummy: "+type);
                        if(type==2797 || type==3475)Require(ordinaryChildren>0 && live>0,"native ongoing parent reaches its real ammunition/second stage: "+type);
                        if(type==2797)Require(bubbleOrderChanges>0 && bubbleAssistedChanges==bubbleOrderChanges,"earlier sibling release changes real child update order and every changed Kill consumes its prepared alternative");
                        if(type==95 || type==1201)Require(delayed>0,"normal cooldown retains a future launch preview: "+type);
                        if(type==3473)Require(solarWaitVerified && delayed>0,"late sealed Solar arc previews its actual delayed next use and verifies the next native birth");
                        for(int i=0;i<4;i++)NativeCombatSelectionPreviewChecks.Step(context,physical,false);
                        player.inventory[0].SetDefaults(3509);NativeCombatSelectionPreviewChecks.Step(context,physical,false);
                        Present(host,attack,world,graphics,output,"switch-away-"+type,false,true);
                        Require(!player.controlUseItem && !player.channel,"release and item switch preserve native input state: "+type);
                        Console.WriteLine("PASS actual use item="+type+" idle=4/4 use="+shown+"/"+duration+" delayed="+delayed+" live="+live+" directedBirths="+aimedBirths+" childAmmo="+ordinaryChildren+" actualDamage="+hits+" bubbleOrderChanges="+bubbleAssistedChanges+"/"+bubbleOrderChanges+" draw="+(graphics!=null));
                    }
                    catch(Exception error){failures.Add(type+": "+error.Message);Console.WriteLine("FAIL actual use item="+type+": "+error);}
                }
                Require(failures.Count==0,"actual usage failures: "+string.Join(" | ",failures));
            }
            finally{player=null;receiver=null;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());}
        }
        private static bool Red(Color value){return value.A>100 && value.R>190 && value.G<110 && value.B<110;}
        private static void Present(object host,object attack,object world,ProbeGraphics graphics,string output,string name,bool expected,bool save)
        {
            var contact=(AttackContact)GetOptional(attack,"ExpectedPreview");
            Require((contact!=null)==expected,"finite stable preview/revocation at "+name+" timers="+player.itemAnimation+"/"+player.itemTime+" direction="+player.direction+" sources="+Main.projectile.Count(q=>q.active)+" receiver="+receiver.active+"/"+receiver.dontTakeDamage);
            var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");
            if(expected)Require(ReferenceEquals(contact.Timeline,cache.Read(0)) && contact.Timeline.Identity.Slot==receiver.whoAmI,"preview and selected path share the actual published timeline: "+name);
            Call(world,"Prepare");var zoom=Main.GameViewMatrix.ZoomMatrix;var impact=Get(world,"Impact");
            if(graphics==null){Call(impact,"Project",zoom,Matrix.Invert(zoom));Require((bool)Get(impact,"Visible")==expected,"natural capture/project at "+name);return;}
            int steps=cache.Steps;var pixels=graphics.Pixels(()=>Call(world,"Draw"),zoom);var red=pixels.Select((v,i)=>new{v,i}).Where(p=>Red(p.v)).Select(p=>new Vector2(p.i%960,p.i/960)).ToArray();
            Require((red.Length>0)==expected && cache.Steps==steps,"natural actual World.Draw red presence without advancing prediction: "+name);
            if(expected)
            {
                var b=contact.Timeline[contact.ReceiveTick].Bounds;var center=Vector2.Transform(new Vector2(b.CenterX,b.CenterY)-Main.screenPosition,zoom);
                Require(red.Length>=16 && red.Length<=100 && red.All(p=>Math.Abs(p.X-center.X)<=6 && Math.Abs(p.Y-center.Y)<=6) && !Red(pixels[(int)center.Y*960+(int)center.X]),"actual hollow red pixels surround the same receive-time NPC path position: "+name);
            }
            if(save && output!=null)graphics.Image(Path.Combine(output,name+".png"),()=>Call(world,"Draw"),zoom);
        }
    }
}
