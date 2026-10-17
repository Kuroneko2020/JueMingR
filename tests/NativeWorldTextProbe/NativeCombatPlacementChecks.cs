using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatPlacementChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static NPC target;private static Item item;private static bool assisted,shooting;private static int births,checks,shots;private static Vector2 expected,expectedVelocity;
        private static readonly int[] items={1157,1309,1802,2364,2365,2535,2551,2584,2621,2749,3249,3474,3531,4269,4273,4281,4607,4758,5005,5069,5114,5456,5663,5664,6148,6149,6161,6164,1572,2366,3569,3571,5119,5463,3818,3819,3820,3824,3825,3826,3829,3830,3831,3832,3833,3834};
        private static Vector2 physical=new Vector2(600,500);
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var input=Get(context,"Input");var attack=Get(combat,"Attack");var audit=new Harmony("JueMingR.Tests.Placement");
            audit.Patch(AccessTools.Method(typeof(Player),"ItemCheck_CheckCanUse_Inner"),prefix:new HarmonyMethod(typeof(NativeCombatPlacementChecks).GetMethod(nameof(Check),Flags)){priority=Priority.Last});
            audit.Patch(AccessTools.Method(typeof(Player),"ItemCheck_Shoot"),prefix:new HarmonyMethod(typeof(NativeCombatPlacementChecks).GetMethod(nameof(Shoot),Flags)){priority=Priority.Last},postfix:new HarmonyMethod(typeof(NativeCombatPlacementChecks).GetMethod(nameof(ShotEnd),Flags)),finalizer:new HarmonyMethod(typeof(NativeCombatPlacementChecks).GetMethod(nameof(ShotFinal),Flags)));
            audit.Patch(AccessTools.Method(typeof(Player),"TryUpdateChannel"),postfix:new HarmonyMethod(typeof(NativeCombatPlacementChecks).GetMethod(nameof(Born),Flags)));
            var failures=new List<string>();int totalBirths=0;
            try
            {
                foreach(int type in items)
                {
                    try
                    {
                        var p=Setup(context,combat,host,input,type);item=p.HeldItem;assisted=true;births=checks=shots=0;
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));
                        for(int i=0;i<2;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),false,true);
                        Require(births==0 && GetOptional(attack,"ExpectedPreview")==null,"idle placement cannot invent a autonomous damage contact or source: "+type);
                        NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),true);
                        Require(checks>0 && shots==1 && births>0,"natural placement passes actual native CanUse/Shoot and creates its native source: "+type+" checks="+checks+" shots="+shots+" births="+births);
                        Require(GetOptional(attack,"placementUse")==null && GetOptional(attack,"ExpectedPreview")==null,"placement use receipt retires and no spawn point becomes a damage marker: "+type);
                        totalBirths+=births;
                    }
                    catch(Exception error){failures.Add(type+": "+error.Message);Console.WriteLine("FAIL natural placement item="+type+": "+error);}
                    finally{item=null;target=null;shooting=false;}
                }
                // Aim-off still permits the player's ordinary placement at the
                // physical cursor. No earlier hypothetical preview grants it.
                var off=Setup(context,combat,host,input,1309);item=off.HeldItem;assisted=false;births=checks=shots=0;
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,false));
                NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),false,true);
                NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),true);
                Require(births==1 && GetOptional(attack,"placementUse")==null,"Aim-off minion birth stays on original manual cursor");
                foreach(int type in new[]{3824,1572})foreach(bool aim in new[]{false,true})
                {
                    var mirror=Setup(context,combat,host,input,type);item=mirror.HeldItem;assisted=aim;births=checks=shots=0;
                    target.position=new Vector2(580,500);physical=new Vector2(880,500);NativeCombatCadenceChecks.Save(combat,new CombatOptions(32));
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,aim));
                    NativeCombatSelectionPreviewChecks.Step(context,physical,false,true);
                    NativeCombatSelectionPreviewChecks.Step(context,physical,true);
                    Require(checks>0 && shots==1 && births==1,"mirrored Ballista/other sentry physical native placement survives Aim ON/OFF: "+type);
                }
                physical=new Vector2(600,500);
                // Physical DD2 site is blocked while the selected NPC site is
                // usable. Native CanUse must reject; no selected-point rescue.
                var blocked=Setup(context,combat,host,input,3818);item=blocked.HeldItem;assisted=true;births=checks=shots=0;
                for(int y=28;y<43;y++)NativeToolsChecks.Tile(49,y,1);
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));
                NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),false,true);
                NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),true);
                Require(checks>0 && births==0 && shots==0,"native DD2 CanUse rejects blocked physical site rather than using legal NPC point");
                Require(failures.Count==0,"placement family failures: "+string.Join(" | ",failures));
                Console.WriteLine("PASS native placement input families="+items.Length+" births="+totalBirths+"; CanUse and Shoot preserve physical sentry placement and selected non-sentry placement; autonomous targeting is untouched");
            }
            finally{item=null;target=null;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
        private static Player Setup(object context,object combat,object host,object input,int type)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,type,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.releaseUseItem=true;p.statMana=p.statManaMax=p.statManaMax2=200;p.downedDD2EventAnyDifficulty=true;Main.screenPosition=new Vector2(600,400);
            target=NativeCombatControlPreviewChecks.Dummy(2,55,40);
            if(!p.HeldItem.sentry)for(int y=28;y<43;y++)NativeToolsChecks.Tile(37,y,1);
            return p;
        }
        private static Vector2 Cursor(Player p)
        {
            bool reverse=item.type==6161 || item.type==1572 || item.type==2366 || item.type==3569 || item.type==3571 || item.type==5119;
            return (reverse?Main.ReverseGravitySupport(Main.MouseScreen):Main.MouseScreen)+Main.screenPosition;
        }
        private static void Check(Player __instance,Item __0)
        {
            if(!ReferenceEquals(item,__0) || __instance.whoAmI!=0 || !__instance.controlUseItem)return;
            if(assisted)Require(Vector2.DistanceSquared(Cursor(__instance),item.sentry?physical:target.Center+target.netOffset)<2,"native CanUse preserves its physical sentry or selected non-sentry point before terrain/payment gates: "+item.type);
            checks++;
        }
        private static void Shoot(Player __instance,Item __1)
        {
            if(!ReferenceEquals(item,__1) || __instance.whoAmI!=0)return;
            if(assisted)Require(Vector2.DistanceSquared(Cursor(__instance),item.sentry?physical:target.Center+target.netOffset)<2,"native Shoot consumes the same physical sentry or selected non-sentry point: "+item.type);
            expected=Main.MouseWorld;
            if(item.type==6161){expected=__instance.RotatedRelativePoint(__instance.MountedCenter);expectedVelocity=Vector2.Normalize(Cursor(__instance)-expected)*item.shootSpeed;}
            else if(item.type!=3531 && !item.sentry)__instance.LimitPointToPlayerReachableArea(ref expected);
            if(item.sentry)
            {
                if(ProjectileID.Sets.IsADD2Turret[item.shoot])
                {int x,y,up;__instance.FindSentryRestingSpot(item.shoot,out x,out y,out up);expected=new Vector2(x,y-up);}
                else
                {object[] args={item,0f,0f};AccessTools.Method(typeof(Player),"PickSentryLandingSpot").Invoke(__instance,args);expected=new Vector2((float)args[1],item.type==5463?Main.MouseWorld.Y:(float)args[2]);}
            }
            shots++;
            shooting=true;
        }
        private static void ShotEnd(){shooting=false;}
        private static Exception ShotFinal(Exception __exception){shooting=false;return __exception;}
        private static void Born(Player __instance,Projectile __0)
        {
            if(item==null || __instance.whoAmI!=0 || !shooting)return;
            // Shoot naturally faces the physical cursor after its entry. DD2
            // FindSentryRestingSpot adds that final direction to some X values;
            // an entry-facing oracle would be wrong by two pixels when mirrored.
            if(item.sentry && ProjectileID.Sets.IsADD2Turret[item.shoot])
            {int x,y,up;__instance.FindSentryRestingSpot(item.shoot,out x,out y,out up);expected=new Vector2(x,y-up);}
            // This hook is inside the original NewProjectile, before any AI.
            Require(Vector2.DistanceSquared(__0.Center,expected)<2,"original hand birth matches its real clamping/ground placement query: "+item.type+" actual="+__0.Center+" expected="+expected);
            if(item.type==6161)Require(__0.type==1118 && Vector2.DistanceSquared(__0.velocity,expectedVelocity)<.0001f,"Clay Pot's original hand stage is a directed birth, before autonomous AI067");
            births++;
        }
    }
}
