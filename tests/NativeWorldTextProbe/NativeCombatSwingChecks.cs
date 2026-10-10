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
        private static int idleHits,idleRight,idleShapes;private static long idleFirst;private static bool calibrating;
        private static void IdleDamage(NPC __instance,int __result){if(ReferenceEquals(__instance,target) && __result>0){idleHits++;if(idleFirst<0)idleFirst=Main.GameUpdateCount;}}
        private static void IdleShape(Player __instance,Item __0,Rectangle __1)
        {
            if(!calibrating || __instance.whoAmI!=Main.myPlayer || __0.type!=4)return;
            // Learn this fixture's edge from the real complete animation,
            // independently of the product's private future pose extraction.
            if(__1.Bottom>target.Hitbox.Top && __1.Top<target.Hitbox.Bottom){idleRight=Math.Max(idleRight,__1.Right);idleShapes++;}
        }
        internal static void Run(object context)
        {Idle(context);Check(context,false);Check(context,true);NativeCombatSwingSpecialChecks.Run(context);}
        private static void Idle(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var input=Get(context,"Input");var aim=Get(combat,"Attack");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,4,0,0);p.position=new Vector2(700,646);p.ResetEffects();Main.screenPosition=new Vector2(600,500);
            var n=NativeCombatControlPreviewChecks.Dummy(2,46,40);
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,false));
            var damageAudit=new Harmony("JueMingR.Tests.IdleSwingDamage");damageAudit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatSwingChecks).GetMethod(nameof(IdleDamage),BindingFlags.Static|BindingFlags.NonPublic)));target=n;idleHits=0;idleFirst=-1;
            damageAudit.Patch(typeof(Player).GetMethod("ItemCheck_MeleeHitNPCs",BindingFlags.Instance|BindingFlags.NonPublic),prefix:new HarmonyMethod(typeof(NativeCombatSwingChecks).GetMethod(nameof(IdleShape),BindingFlags.Static|BindingFlags.NonPublic)));
            try
            {
            // The previous hard-coded x=700 was already a real overlap in
            // this locked native frame, not an edge negative. Establish the
            // full native swept extent first, then translate exactly to its
            // boundary. No product method supplies the expected edge.
            p.position.X-=100;idleRight=int.MinValue;idleShapes=0;calibrating=true;
            for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(760,650),false,true);
            for(int i=0;i<25;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(760,650),true);
            calibrating=false;Require(idleShapes>5 && idleHits==0,"native full swing establishes an independent spatial boundary away from receiver");
            p.position.X+=n.Hitbox.Left-idleRight;
            for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(760,650),false,true);
            bool edgePreview=GetOptional(aim,"ExpectedPreview")!=null;
            for(int i=0;i<25;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(760,650),true);Require(idleHits==0,"original complete use agrees that edge touching cannot damage: hits="+idleHits+" first="+idleFirst+" preview="+edgePreview+" player="+p.position+" target="+n.Hitbox);Require(!edgePreview,"ordinary swing actual edge miss has no hypothetical contact");
            NativeCombatSelectionPreviewChecks.Step(context,new Vector2(760,650),false);p.position.X+=1;
            for(int i=0;i<3;i++)
            {
                NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,new Vector2(760,650),false);Call(combat,"Sample");Call(host,"SampleMouse");p.Update(0);n.UpdateNPC(2);Call(context,"UpdateRuntime");Call(context,"UpdateShell");
                var contact=(AttackContact)GetOptional(aim,"ExpectedPreview");
                if(contact==null)ExplainIdle(aim,host,p,n);
                Require(contact!=null && contact.ReceiveTick<contact.Timeline.Count,"idle native held frame supports future ordinary swing without Aim permission");
                Require(p.itemAnimation==0 && p.itemTime==0 && !p.controlUseItem && !p.channel && GetOptional(aim,"ExpectedImpact")==null,"hypothetical swing leaves actual animation and input idle");
            }
            p.direction=-1;NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,650),false,true);
            Require(GetOptional(aim,"ExpectedPreview")==null,"turning in place retires a right-facing ordinary swing contact");
            p.direction=1;NativeCombatSelectionPreviewChecks.Step(context,new Vector2(760,650),false,true);
            Require(GetOptional(aim,"ExpectedPreview")!=null,"turning back recovers the ordinary idle swing without an attack edge");
            var planned=(AttackContact)GetOptional(aim,"ExpectedPreview");long expected=planned.Timeline.SampleTick+planned.Tick;idleFirst=-1;
            for(int i=0;i<25;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(760,650),true);
            Require(idleHits>0 && idleFirst==expected,"idle hypothetical swing's first real original Damage agrees with contact time: expected="+expected+" actual="+idleFirst);
            Console.WriteLine("PASS idle ordinary swing: exact edge miss / one pixel overlap, original first Damage="+idleFirst+", native held frame and no attack permission");
            }
            finally{calibrating=false;target=null;damageAudit.UnpatchAll(damageAudit.Id);}
        }
        private static void ExplainIdle(object aim,object host,Player player,NPC npc)
        {
            var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");var path=cache.Read(1);var model=Get(aim,"swing");var frame=(Rectangle)Get(model,"frame");
            var expected=(AttackContact)GetOptional(aim,"ExpectedPreview");Console.WriteLine("SWING idle: player="+player.position+" direction="+player.direction+" frame="+frame+" visual="+player.lastVisualizedSelectedItem.type+" animation="+player.itemAnimation+" target="+npc.Hitbox+" alive="+npc.active+" canReceive="+Get(aim,"CurrentReceiver")+" demand="+Get(aim,"PreviewDemand")+" future="+path?.Count+" stop="+path?.Stop+" age="+(Main.GameUpdateCount-path?.SampleTick)+" preview="+(expected==null?"null":expected.Tick+"/"+expected.ReceiveTick));
            if(path!=null)
            {
                object result=model.GetType().GetMethod("Preview",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(model,new[]{(object)player,player.HeldItem,path,NativeCombatAimChecks.PhaseClock(aim,path,"CompletedWorld"),Get(aim,"terrain"),120});
                Console.WriteLine("SWING direct finite preview="+result);
            }
        }
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
