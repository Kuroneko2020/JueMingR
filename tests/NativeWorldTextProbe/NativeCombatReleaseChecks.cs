using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Observes native starts, throws and trajectories; never changes an item
    // timer, selection rule, projectile AI or damage gate. The common CPU
    // fixture's 24x24 held-item draw box is not a full melee/DPS measurement.
    internal static class NativeCombatReleaseChecks
    {
        private sealed class Use {internal uint Tick;internal int Animation;}
        private sealed class Flight
        {internal Projectile Shot;internal int Key;internal Vector2 Origin;internal float Distance;internal bool Returned;}
        private static readonly List<Use> uses=new List<Use>();
        private static readonly List<Flight> flights=new List<Flight>();
        private static readonly List<uint> releaseDelays=new List<uint>();
        private static bool watching;
        internal static void Run(object context)
        {
            object combat=Get(context,"Combat"),tools=Get(context,"Tools"),input=Get(context,"Input");
            const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
            var audit=new Harmony("JueMingR.Tests.CombatRelease");
            audit.Patch(typeof(Player).GetMethod("ItemCheck_StartActualUse",flags),postfix:new HarmonyMethod(typeof(NativeCombatReleaseChecks),nameof(Started)));
            audit.Patch(typeof(Player).GetMethod("TryUpdateChannel",flags),postfix:new HarmonyMethod(typeof(NativeCombatReleaseChecks),nameof(Created)));
            try
            {
                foreach(int interval in new[]{0,12,30})foreach(int first in new[]{198,3764})
                {
                    NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                    var p=NativeToolExecutionChecks.Reset(context,tools,input,first,0,0);
                    foreach(var item in p.armor)item.TurnToAir();Array.Clear(p.buffType,0,p.buffType.Length);Array.Clear(p.buffTime,0,p.buffTime.Length);
                    int[] types=first==198?new[]{198,199,200,201,202,203,4258,5535}:new[]{3764,3765,3766,3767,3768,3769,4259,5536};
                    for(int i=0;i<types.Length;i++){p.inventory[i].SetDefaults(types[i]);if(first==3764)p.inventory[i].Prefix(81);}
                    p.releaseUseItem=true;p.reuseDelay=0;p.delayUseItem=false;
                    uses.Clear();flights.Clear();releaseDelays.Clear();NativeCombatCadenceChecks.Save(combat,new CombatOptions(4,interval));watching=true;
                    for(int t=0;t<240;t++)
                    {
                        NativeCombatCadenceChecks.Step(context,false,true,t%3,null,new Vector2(1650,p.Center.Y));
                        foreach(var flight in flights)
                        {
                            if(!flight.Shot.active || (int)flight.Shot.key!=flight.Key)continue;
                            flight.Distance=Math.Max(flight.Distance,Vector2.Distance(flight.Origin,flight.Shot.Center));
                            if(flight.Shot.ai[0]==1)flight.Returned=true;
                        }
                    }
                    watching=false;
                    Require(uses.Count>=4 && flights.Count>=4 && releaseDelays.Count==flights.Count,"native quick-switch continues throwing, item="+first+" interval="+interval);
                    var complete=flights.Where(f=>f.Returned).ToArray();
                    Console.WriteLine("Release flight item="+first+" interval="+interval+" shots="+flights.Count+" releaseTicks="+string.Join("/",releaseDelays.Distinct())+" maxDistance="+(complete.Length==0?0:complete.Average(f=>f.Distance)).ToString("F2"));
                    Require(complete.Length>=3 && complete.All(f=>f.Distance>100),"flight must reach beyond 100px before native return completes; late release collapses this range");
                    Require(releaseDelays.All(t=>t<uses[0].Animation/2),"throw while native animation cooldown still has useful flight time remaining");
                    Require(uses.Skip(1).Select((u,i)=>unchecked(u.Tick-uses[i].Tick)>=uses[i].Animation+interval).All(v=>v),"early throw must retain native cooldown plus configured extra interval");
                    watching=true;int before=uses.Count;for(int t=0;t<40;t++)NativeCombatCadenceChecks.Step(context,false,false,1);watching=false;
                    Require(!(bool)Get(Get(combat,"Use"),"Active") && !Main.mouseLeft && uses.Count==before,"release retires input without another automatic use");
                }
                FastWeapons(context,combat,tools,input);
            }
            finally{watching=false;NativeCombatCadenceChecks.Save(combat,new CombatOptions());foreach(var method in audit.GetPatchedMethods().ToArray())audit.Unpatch(method,HarmonyPatchType.All,audit.Id);}
            Console.WriteLine("PASS early native phaseblade throw retains outward flight, cooldown, extra interval and neutral-input retirement.");
        }
        private static void FastWeapons(object context,object combat,object tools,object input)
        {
            NativeCombatCadenceChecks.Save(combat,new CombatOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,3352,0,0);
            var world=Main.ActiveWorldFileData;int mode=world.GameMode;bool good=Main.getGoodWorld,extra=p.extraAccessory;
            var field=typeof(Main).GetField("_gameModeDifficultyOverride",BindingFlags.NonPublic|BindingFlags.Static);object difficulty=field.GetValue(null);
            try
            {
                field.SetValue(null,null);world.GameMode=2;Main.getGoodWorld=false;p.extraAccessory=true;
                p.inventory[0].Prefix(Terraria.ID.PrefixID.Light);p.inventory[1].SetDefaults(3772);p.inventory[1].Prefix(Terraria.ID.PrefixID.Light);
                int[] equipment={3806,3881,2765,211,897,936,1343,3992,6182,1865};
                for(int i=0;i<equipment.Length;i++){p.armor[i].SetDefaults(equipment[i]);if(i>=3)p.armor[i].Prefix(Terraria.ID.PrefixID.Violent);}
                p.AddBuff(Terraria.ID.BuffID.Tipsy,600);p.AddBuff(Terraria.ID.BuffID.WellFed3,600);
                p.releaseUseItem=true;p.reuseDelay=0;p.delayUseItem=false;uses.Clear();flights.Clear();releaseDelays.Clear();
                NativeCombatCadenceChecks.Save(combat,new CombatOptions(4,0));watching=true;
                for(int t=0;t<180;t++)NativeCombatCadenceChecks.Step(context,false,true,1,null,new Vector2(1650,p.Center.Y));
                watching=false;
                Require(uses.Count>=12 && uses.Any(u=>u.Animation==3) && flights.Count>=12,"real equipment reaches animation 3 and still repeatedly throws");
                Require(releaseDelays.All(t=>t==1),"short native animation retains its first legal release before auto-reuse swallows the tail");
                Require(uses.Skip(1).Select((u,i)=>unchecked(u.Tick-uses[i].Tick)>=uses[i].Animation).All(v=>v),"high attack speed still retains native cooldown");
                Console.WriteLine("Release high speed: animations="+string.Join("/",uses.Select(u=>u.Animation).Distinct())+" shots="+flights.Count);
            }
            finally
            {
                watching=false;NativeCombatCadenceChecks.Save(combat,new CombatOptions());world.GameMode=mode;Main.getGoodWorld=good;field.SetValue(null,difficulty);p.extraAccessory=extra;
                foreach(var item in p.armor)item.TurnToAir();Array.Clear(p.buffType,0,p.buffType.Length);Array.Clear(p.buffTime,0,p.buffTime.Length);
            }
        }
        private static void Started(Player __instance)
        {if(watching && __instance.whoAmI==Main.myPlayer)uses.Add(new Use{Tick=Main.GameUpdateCount,Animation=__instance.itemAnimationMax});}
        private static void Created(Player __instance,Projectile __0)
        {
            if(!watching || __instance.whoAmI!=Main.myPlayer || __0.type!=__instance.HeldItem.shoot)return;
            Require(uses.Count>0,"a native throw needs an actual use");releaseDelays.Add(unchecked(Main.GameUpdateCount-uses[uses.Count-1].Tick));
            flights.Add(new Flight{Shot=__0,Key=(int)__0.key,Origin=__0.Center});
        }
    }
}
