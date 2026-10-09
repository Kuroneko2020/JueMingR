using System;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatSwingChecks
    {
        private static object attack;private static NPC target;private static int contacts,poses;
        internal static void Run(object context)
        {Check(context,false);Check(context,true);}
        private static void Check(object context,bool future)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var input=Get(context,"Input");attack=Get(combat,"Attack");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,4,0,0);p.position=new Vector2(700,646);p.ResetEffects();Main.screenPosition=new Vector2(600,500);
            // Reset does not erase lastVisualizedSelectedItem. Let the real
            // idle player publish this held sword's frame before its first use,
            // as in normal play; the previous Xeno visual is not a sword pose.
            NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,new Vector2(760,650),false);Call(combat,"Sample");p.Update(0);
            if(future)Main.dayTime=false;
            target=Main.npc[2];target.SetDefaults(3);target.whoAmI=2;target.active=true;target.position=new Vector2(future?742:724,640);target.life=target.lifeMax=10000;target.target=0;if(future){target.direction=-1;target.velocity=new Vector2(-1.25f,0);}Array.Clear(target.immune,0,target.immune.Length);target.UpdateNPC(2);
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));contacts=poses=0;
            var audit=new Harmony("JueMingR.Tests.SwingContact");var method=typeof(Player).GetMethod("ItemCheck_MeleeHitNPCs",BindingFlags.Instance|BindingFlags.NonPublic);audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatSwingChecks).GetMethod(nameof(BeforeDamage),BindingFlags.Static|BindingFlags.NonPublic)));
            try
            {
                long planned=-1,first=-1;int life=target.life;
                for(int step=0;step<18;step++)
                {
                    NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,new Vector2(760,650),true);Call(combat,"Sample");p.Update(0);target.UpdateNPC(2);Call(context,"UpdateRuntime");
                    if(first<0 && target.life<life)first=Main.GameUpdateCount;
                    if(future && first<0 && planned<0){var contact=(AttackContact)GetOptional(attack,"ExpectedImpact");if(contact!=null){planned=contact.Timeline.SampleTick+contact.Tick;Require(planned>Main.GameUpdateCount,"future swing is published before its damage window");}}
                }
                Require(contacts>0 && target.life<10000,"ordinary noShoot sword uses natural pose and produces legal damage contact");
                Require(poses==18,"every natural attack pose is compared through all three animation phases");
                if(future)Require(planned>0 && first==planned,"completed-world ordinary swing predicts actual future first Damage: expected="+planned+" actual="+first);
                Console.WriteLine("PASS ordinary natural swing contact, body bounds and Player-before-NPC damage clock: future="+future+" contacts="+contacts+" poses="+poses+" expected="+planned+" first="+first);
            }
            finally{audit.Unpatch(method,HarmonyPatchType.All,audit.Id);attack=null;target=null;}
        }
        private static void BeforeDamage(Player __instance,Item __0,Rectangle __1)
        {
            if(attack!=null && __0.type==4)
            {
                var swing=Get(attack,"swing");var frame=(Rectangle)Get(swing,"frame");float offset=(float)Get(swing,"mountOffset");
                var pose=swing.GetType().GetMethod("Pose",BindingFlags.Static|BindingFlags.NonPublic);var predicted=(Vector2)pose.Invoke(null,new object[]{__instance,__0,frame,offset});
                Require(Vector2.DistanceSquared(predicted,__instance.itemLocation)<.0001f,"finite pose extraction agrees with naturally generated location through animation phases: predicted="+predicted+" actual="+__instance.itemLocation+" position="+__instance.position+" frame="+frame+" animation="+__instance.itemAnimation+"/"+__instance.itemAnimationMax+" dir="+__instance.direction+" mount="+offset+" visual="+__instance.lastVisualizedSelectedItem.type);
                int x=Main.mouseX,y=Main.mouseY;try{Main.mouseX=13;Main.mouseY=900;bool blocked;Rectangle same;__instance.ItemCheck_GetMeleeHitbox(__0,frame,out blocked,out same);Require(same==__1 && !blocked,"moving mouse does not rotate ordinary swing damage rectangle");}finally{Main.mouseX=x;Main.mouseY=y;}poses++;
            }
            if(attack==null || __instance.whoAmI!=Main.myPlayer || __0.type!=4 || target.immune[0]!=0 || __instance.attackCD>0 || !__instance.CanHitNPCWithMeleeHit(2))return;
            var body=new Rectangle((int)(target.position.X+target.netOffset.X),(int)(target.position.Y+target.netOffset.Y),target.width,target.height);if(!__1.Intersects(body))return;
            var contact=(AttackContact)GetOptional(attack,"ExpectedImpact");Require(contact!=null,"natural ordinary sword damage window has a prepared same-target contact");
            Require(contact.Timeline.SampleTick+contact.Tick==Main.GameUpdateCount,"Player damage uses current GUC while receiving NPC remains at previous completed sample");contacts++;
        }
    }
}
