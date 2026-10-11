using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatSwingSpecialChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static NPC watched;private static long firstDamage;
        private static readonly List<string> nativeShapes=new List<string>();
        private static int nativeRight;
        private static void Damage(NPC __instance,int __result){if(ReferenceEquals(__instance,watched) && __result>0 && firstDamage<0)firstDamage=Main.GameUpdateCount;}
        private static void Shape(Player __instance,Item __0,Rectangle __1)
        {
            if(watched==null || __instance.whoAmI!=Main.myPlayer || nativeShapes.Count>=32)return;
            if(__1.Bottom>watched.Hitbox.Top && __1.Top<watched.Hitbox.Bottom)nativeRight=Math.Max(nativeRight,__1.Right);
            nativeShapes.Add(__instance.itemAnimation+"/"+__instance.itemAnimationMax+":"+__1+"@"+__instance.position+" dir="+__instance.direction+" overlap="+__1.Intersects(watched.Hitbox));
        }
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var input=Get(context,"Input");var attack=Get(combat,"Attack");var model=attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostSwingAttack",true);
            var pose=model.GetMethod("Pose",Flags);var rotation=model.GetMethod("Rotation",Flags);int poses=0;
            foreach(int type in new[]{121,946,4707})foreach(int direction in new[]{-1,1})foreach(float gravity in new[]{-1f,1f})
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,type,0,0);p.ResetEffects();p.position=new Vector2(700,500);p.direction=direction;p.gravDir=gravity;
                var frame=Item.GetDrawHitbox(type,p);p.itemAnimationMax=p.HeldItem.useAnimation;
                for(int a=1;a<=p.itemAnimationMax;a++)
                {
                    p.itemAnimation=a;var point=(Vector2)pose.Invoke(null,new object[]{p,p.HeldItem,frame,0f});float angle=(float)rotation.Invoke(null,new object[]{p,p.HeldItem});
                    p.ItemCheck_ApplyUseStyle(0,p.HeldItem,frame);
                    Require(Vector2.DistanceSquared(point,p.itemLocation)<.00001f && Math.Abs(angle-p.itemRotation)<.00001f,"Volcano/umbrella finite pose agrees with native every phase/direction/gravity: "+type+"/"+direction+"/"+gravity+"/"+a);
                    bool blocked;Rectangle box;p.ItemCheck_GetMeleeHitbox(p.HeldItem,frame,out blocked,out box);
                    if(type!=121)Require(blocked==(a>p.itemAnimationMax*.666),"native umbrella opening third cannot damage");
                    poses++;
                }
            }
            var audit=new Harmony("JueMingR.Tests.SwingSpecial");audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatSwingSpecialChecks).GetMethod(nameof(Damage),Flags)));
            audit.Patch(typeof(Player).GetMethod("ItemCheck_MeleeHitNPCs",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSwingSpecialChecks),nameof(Shape)));
            string only=Environment.GetEnvironmentVariable("JMR_SWING_SPECIAL_CASE");int scenes=0;
            try
            {
                foreach(int type in new[]{121,946,4707})foreach(int direction in new[]{-1,1})
                {
                    if(!string.IsNullOrEmpty(only) && only!=type+"/"+direction)continue;
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,type,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.direction=direction;p.releaseUseItem=true;Main.screenPosition=new Vector2(600,400);
                    watched=NativeCombatControlPreviewChecks.Dummy(2,direction==1?46:42,40);firstDamage=-1;nativeShapes.Clear();nativeRight=int.MinValue;
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,true));
                    for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(710+direction*100,650),false,true);
                    var predicted=(AttackContact)GetOptional(attack,"ExpectedPreview");
                    long expected=predicted==null?-1:predicted.Timeline.SampleTick+predicted.Tick;
                    var idlePosition=p.position;int idleDirection=p.direction;var nativeFrame=Item.GetDrawHitbox(type,p);var targetBox=watched.Hitbox;
                    for(int i=0;i<p.HeldItem.useAnimation+2;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(710+direction*100,650),true);
                    // A null model must not prevent the original action from
                    // deciding whether this fixed spatial positive is real.
                    // Keep both original positive and first-time requirements.
                    if(predicted==null || firstDamage!=expected)Console.WriteLine("SWING SPECIAL first difference item="+type+"/"+direction+" idle="+idlePosition+" facing="+idleDirection+" frame="+nativeFrame+" targetBefore="+targetBox+" targetAfter="+watched.Hitbox+" life="+watched.life+" immune="+watched.immune[p.whoAmI]+" expected="+expected+" native="+firstDamage+" boxes="+string.Join(";",nativeShapes));
                    if(type!=121 && direction==1)
                    {
                        // Tile42 and tile46 are not equally distant from this
                        // player. The old right-hand "positive" has a real
                        // thirteen-pixel gap with the native delivered 24x24
                        // item frame. Retain it as a negative, independently
                        // learn the full native extent, then test touching and
                        // a one-pixel overlap. No product pose sets the edge.
                        Require(predicted==null && firstDamage<0 && nativeRight<targetBox.Left,"original right umbrella fixture is a real native/model miss, not a missing positive solution");
                        int gap=targetBox.Left-nativeRight;Require(gap==13,"locked original right umbrella swept extent establishes the fixture's thirteen-pixel gap");
                        NativeCombatSelectionPreviewChecks.Step(context,new Vector2(810,650),false,true);p.position.X+=gap;
                        for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(810,650),false,true);
                        Require(GetOptional(attack,"ExpectedPreview")==null,"umbrella exact native edge touching has no predicted receive");
                        nativeRight=int.MinValue;nativeShapes.Clear();
                        for(int i=0;i<p.HeldItem.useAnimation+2;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(810,650),true);
                        Require(firstDamage<0 && nativeRight==targetBox.Left,"full original umbrella edge action cannot damage at rectangle touching");
                        NativeCombatSelectionPreviewChecks.Step(context,new Vector2(810,650),false,true);p.position.X+=1;
                        for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(810,650),false,true);
                        predicted=(AttackContact)GetOptional(attack,"ExpectedPreview");expected=predicted==null?-1:predicted.Timeline.SampleTick+predicted.Tick;
                        nativeRight=int.MinValue;nativeShapes.Clear();
                        for(int i=0;i<p.HeldItem.useAnimation+2;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(810,650),true);
                        Console.WriteLine("SWING SPECIAL calibrated item="+type+" originalGap="+gap+" player="+p.position+" target="+watched.Hitbox+" nativeRight="+nativeRight+" expected="+expected+" native="+firstDamage);
                        Require(nativeRight==targetBox.Left+1,"right umbrella positive has precisely one pixel of native swept overlap");
                    }
                    Require(predicted!=null,"special no-projectile melee has an idle read-only first-contact preview: "+type+"/"+direction+" nativeFirst="+firstDamage);
                    Require(firstDamage==expected,"native first melee Damage matches idle future pose/capsule: "+type+"/"+direction+" expected="+expected+" actual="+firstDamage);
                    scenes++;
                    Console.WriteLine("PASS native special melee first damage item="+type+" direction="+direction+" expected="+expected+" first="+firstDamage);
                }
                Require(scenes==(string.IsNullOrEmpty(only)?6:1),"requested native special melee natural scenes execute");Console.WriteLine("PASS native Volcano/umbrella all animation geometry poses="+poses+" natural idle-to-first-Damage scenes="+scenes+" filter="+(only??"all"));
            }
            finally{watched=null;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
    }
}
