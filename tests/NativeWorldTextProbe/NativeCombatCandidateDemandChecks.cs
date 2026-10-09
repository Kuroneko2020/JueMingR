using System;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatCandidateDemandChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static NpcPredictionCache watchCache;private static int window,count;
        private static void Before(Player __instance){if(__instance.whoAmI==0 && watchCache!=null){window=watchCache.Required;count=watchCache.Read(1)?.Count??0;}}
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");
            NativeCombatCadenceChecks.Save(combat,new CombatOptions());NativeCombatObservationChecks.Save(host,new ObservationOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3278,0,0);p.position=new Vector2(700,646);p.releaseUseItem=true;Main.screenPosition=new Vector2(600,400);
            float range=ProjectileID.Sets.YoyosMaximumRange[p.HeldItem.shoot];var far=Scene(2,p.Center+new Vector2(range+90,0));var near=Scene(3,p.Center+new Vector2(range/2,0));
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,false,25,true,true));NativeToolExecutionChecks.Sample(context,input,far.Center,true);Call(combat,"Sample");Call(host,"SampleMouse");NativeQuickItemChecks.BeginWorldStep();p.Update(0);
            var selected=(JueMingR.Platform.Combat.NpcIdentity)Get(Get(host,"Selection"),"Target");Console.WriteLine("CANDIDATE yoyo range="+range+" target="+selected.Slot+" near="+near.Center+" far="+far.Center);Require(selected.Slot==3,"one shared target rejects clearly out-of-range yoyo receiver instead of letting unreachable mouse-nearest target mask reachable target");
            near.active=false;far.width=80;far.height=40;far.Center=p.Center+new Vector2(range+10,0);NativeToolExecutionChecks.Sample(context,input,far.Center,true);Call(combat,"Sample");Call(host,"SampleMouse");NativeQuickItemChecks.BeginWorldStep();p.Update(0);selected=(JueMingR.Platform.Combat.NpcIdentity)Get(Get(host,"Selection"),"Target");Require(selected.Slot==2,"large receiver edge remains in range even when its centre is outside");
            Console.WriteLine("PASS shared yoyo reach candidate and legal large receiver edge; single identity still supplies all consumers");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
            SameObstacle(context,combat,host,input);ShortDemand(context,combat,host,input);
        }
        private static void SameObstacle(object context,object combat,object host,object input)
        {
            foreach(int weapon in new[]{95,39,3570,3278})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,weapon,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.channel=p.controlUseItem=true;p.yoyoGlove=p.magicString=false;p.counterWeight=0;p.statMana=p.statManaMax2=10000;Main.screenPosition=new Vector2(600,400);
                p.inventory[54].SetDefaults(weapon==95?97:40);p.inventory[54].stack=999;var blocked=Scene(2,new Vector2(825,667));var open=Scene(3,new Vector2(650,667));
                for(int y=40;y<=42;y++){Main.tile[48,y].active(true);Main.tile[48,y].type=1;}
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,false,25,true,true));NativeToolExecutionChecks.Sample(context,input,blocked.Center,true);Call(combat,"Sample");Call(host,"SampleMouse");Call(Get(host,"Selection"),"Update",((ObservationSettings)Get(host,"Settings")).Value,Get(host,"Session"),true,null);
                var identity=(JueMingR.Platform.Combat.NpcIdentity)Get(Get(host,"Selection"),"Target");Console.WriteLine("WALL CANDIDATE weapon="+weapon+" selected="+identity.Slot+" solidColumn=48:y40..42 player="+p.Center+" A="+blocked.Center+" B="+open.Center);
                Require(identity.Slot==(weapon==95?3:2),"same full-body solid column changes current straight-shot passage preference, while arc/sky/navigation unknown or non-straight routes preserve physical nearest intent");
                if(weapon==95)
                {
                    // Preference never deletes the only blocked receiver; its
                    // current zero velocity is not a future immobility proof.
                    open.active=false;blocked.velocity=new Vector2(0,-1);Call(Get(host,"Selection"),"Update",((ObservationSettings)Get(host,"Settings")).Value,Get(host,"Session"),true,null);
                    Require(((JueMingR.Platform.Combat.NpcIdentity)Get(Get(host,"Selection"),"Target")).Slot==2,"moving or sole blocked receiver remains a candidate, not a permanent unreachable exclusion");
                    open.active=true;blocked.velocity=Vector2.Zero;Main.tile[48,42].active(false);Call(Get(host,"Selection"),"Update",((ObservationSettings)Get(host,"Settings")).Value,Get(host,"Session"),true,null);
                    Require(((JueMingR.Platform.Combat.NpcIdentity)Get(Get(host,"Selection"),"Target")).Slot==2,"current receiver edge through incomplete wall cone is unknown, centre blockage alone cannot demote it");
                    Main.tile[48,42].active(true);Call(Get(host,"Selection"),"Update",((ObservationSettings)Get(host,"Settings")).Value,Get(host,"Session"),true,null);
                }
                var target=Main.npc[identity.Slot];var attack=Get(combat,"Attack");NativeCombatAimChecks.Prepare(host,attack,target,0,true);var plan=GetOptional(attack,"ExpectedImpact");
                var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");Call(Get(Get(host,"World"),"Marker"),"Capture");
                Require(cache.Read(1).Identity.Equals(identity) && Get(Get(Get(host,"World"),"Marker"),"identity").Equals(identity),"path/attack future and marker capture share the final selected identity");
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});int first=-1;
                for(int tick=1;tick<=120 && first<0;tick++)
                {NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,blocked.Center,true);Call(combat,"Sample");blocked.UpdateNPC(2);open.UpdateNPC(3);if(weapon==3278)NativeCombatAimChecks.Prepare(host,attack,target,0,true);int life=target.life;for(int slot=0;slot<Main.maxProjectiles;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;Main.projectile[slot].Update(slot);}Main.ProjectileUpdateLoopIndex=-1;if(target.life<life)first=tick;}
                Console.WriteLine("WALL RESULT weapon="+weapon+" selected="+identity.Slot+" openingContact="+(plan!=null)+" firstDamage="+first+" lifeA="+blocked.life+" lifeB="+open.life);
                Require(weapon!=95 || first>0 && blocked.life==100000,"current blocked straight target cannot mask the useful open native shot");
                Require(weapon==39?plan==null && first<0:first>0,"same obstacle preserves finite unknown arc and actual useful sky/navigation outcomes without claiming every mechanism reachable");
            }
            Console.WriteLine("PASS same-obstacle shared identity matrix; current passage preference is conditional, finite no-Contact is not a physical-impossibility claim.");
        }
        private static void ShortDemand(object context,object combat,object host,object input)
        {
            var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");var audit=new Harmony("JueMingR.Tests.AttackDemand");audit.Patch(typeof(Player).GetMethod("ItemCheck",Flags),prefix:new HarmonyMethod(typeof(NativeCombatCandidateDemandChecks).GetMethod("Before",Flags)){priority=Priority.Last});
            try
            {
                foreach(bool path in new[]{false,true})
                {
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.releaseUseItem=true;p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;var n=Scene(2,new Vector2(870,667));
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,path,false,false,false,25,false,true));NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),false);Call(combat,"Sample");Call(host,"SampleMouse");NativeQuickItemChecks.BeginWorldStep();p.Update(0);Call(context,"UpdateRuntime");
                    NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");Call(host,"SampleMouse");watchCache=cache;NativeQuickItemChecks.BeginWorldStep();p.Update(0);watchCache=null;
                    Projectile shot=null;foreach(var q in Main.projectile)if(q.active && q.type==14)shot=q;
                    Console.WriteLine("DEMAND path="+path+" required="+window+" samples="+count+" shot="+(shot==null?"none":shot.velocity.ToString()));Require(shot!=null && shot.velocity.X>0 && (path?window==120:window>=16 && window<40) && count>=window+1,"real first shot uses bounded short attack demand, independent path keeps its own long request");
                }
                NativeCombatObservationChecks.Save(host,new ObservationOptions(marker:true));NativeToolExecutionChecks.Sample(context,input,new Vector2(870,667),false);Call(combat,"Sample");Call(host,"SampleMouse");Call(context,"UpdateRuntime");int steps=cache.Steps;
                for(int i=0;i<20;i++){Call(context,"UpdateRuntime");Call(context,"UpdateShell");}Require(cache.Required==0 && cache.Steps==steps && (bool)Get(Get(host,"Selection"),"HasTarget"),"Aim OFF marker-only selection performs no future work or residual attack demand");
                Console.WriteLine("PASS attack-only short preferred prefix, path+attack long prefix, marker-only OFF zero future steps");
            }
            finally{watchCache=null;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
        private static NPC Scene(int slot,Vector2 center){var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.Center=center;n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=100000;Array.Clear(n.immune,0,n.immune.Length);return n;}
    }
}
