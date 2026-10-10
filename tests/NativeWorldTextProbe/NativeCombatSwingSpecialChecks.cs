using System;
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
        private static void Damage(NPC __instance,int __result){if(ReferenceEquals(__instance,watched) && __result>0 && firstDamage<0)firstDamage=Main.GameUpdateCount;}
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
            try
            {
                foreach(int type in new[]{121,946,4707})foreach(int direction in new[]{-1,1})
                {
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,type,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.direction=direction;p.releaseUseItem=true;Main.screenPosition=new Vector2(600,400);
                    watched=NativeCombatControlPreviewChecks.Dummy(2,direction==1?46:42,40);firstDamage=-1;
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,true));
                    for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(710+direction*100,650),false,true);
                    var predicted=(AttackContact)GetOptional(attack,"ExpectedPreview");
                    Require(predicted!=null,"special no-projectile melee has an idle read-only first-contact preview: "+type+"/"+direction);
                    long expected=predicted.Timeline.SampleTick+predicted.Tick;
                    for(int i=0;i<p.HeldItem.useAnimation+2 && firstDamage<0;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(710+direction*100,650),true);
                    Require(firstDamage==expected,"native first melee Damage matches idle future pose/capsule: "+type+"/"+direction+" expected="+expected+" actual="+firstDamage);
                    Console.WriteLine("PASS native special melee first damage item="+type+" direction="+direction+" expected="+expected+" first="+firstDamage);
                }
                Console.WriteLine("PASS native Volcano/umbrella all animation geometry poses="+poses+" and six natural idle-to-first-Damage scenes");
            }
            finally{watched=null;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
    }
}
