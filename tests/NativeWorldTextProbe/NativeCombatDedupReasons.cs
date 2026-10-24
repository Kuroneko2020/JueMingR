using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;
using Microsoft.Xna.Framework;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Finite reason window only. Instrumented elapsed time is explanatory,
    // never a replacement for the unobserved optimized Release time window.
    internal static class NativeCombatDedupReasons
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private sealed class Span {internal long Start,Children;internal string Name;}
        private static readonly Stack<Span> stack=new Stack<Span>();
        private static readonly Dictionary<string,double[]> times=new Dictionary<string,double[]>();
        private static object host,attack;
        private static readonly Dictionary<string,int> ranges=new Dictionary<string,int>();
        private static readonly Dictionary<string,int> actionSolves=new Dictionary<string,int>();
        private static int npcSteps,ownerArrays,ownerValues;
        private static void NpcStep(){npcSteps++;}
        private static void OwnerArray(object[] __args){ownerArrays++;ownerValues+=Math.Min(120,(int)__args[2])+1;}
        private static void ActionSolve(object[] __args)
        {if(!(bool)__args[5])return;string key=Main.GameUpdateCount+"/"+((NpcTrajectory)__args[3]).SampleTick;int n;actionSolves.TryGetValue(key,out n);actionSolves[key]=n+1;}
        private static void Range(object __instance,object[] __args)
        {
            bool action=(bool)__args[2];int required=(int)__args[3];
            string key=(ReferenceEquals(__instance,Get(host,"Prediction"))?"selected":"candidate")+"/"+(action?"action":"world")+"/"+required;
            int count;ranges.TryGetValue(key,out count);ranges[key]=count+1;
        }
        private static void Enter(MethodBase __originalMethod,out object __state)
        {var s=new Span{Start=Stopwatch.GetTimestamp(),Name=__originalMethod.DeclaringType.Name+"."+__originalMethod.Name};stack.Push(s);__state=s;}
        private static Exception Exit(Exception __exception,object __state)
        {var s=(Span)__state;long elapsed=Stopwatch.GetTimestamp()-s.Start;stack.Pop();if(stack.Count>0)stack.Peek().Children+=elapsed;double[] total;if(!times.TryGetValue(s.Name,out total))times.Add(s.Name,total=new double[3]);total[0]+=elapsed*1000.0/Stopwatch.Frequency;total[1]+=(elapsed-s.Children)*1000.0/Stopwatch.Frequency;total[2]++;return __exception;}
        private static void Supply(object __instance,Player __0,Item __1,bool __result,object __3)
        {
            if(!ReferenceEquals(__instance,attack) || !__result || Get(__3,"Kind").ToString()!="Basic")return;
            var prediction=Get(host,"Prediction");var timeline=(NpcTrajectory)Call(Get(prediction,"Cache"),"Read",1);var work=(CombatPredictionWork)Get(host,"PredictionWork");
            Console.WriteLine("REASON Basic GUC="+Main.GameUpdateCount+" current="+(GetOptional(attack,"current")!=null)+" next="+(GetOptional(attack,"next")!=null)+" prepared="+Get(attack,"prepared")+" phase="+Get(attack,"preparedOrdinaryPhase")+" failed="+Get(attack,"Failed")+" workExceeded="+work.Exceeded+" used="+work.UsedMilliseconds+" target="+Main.npc[((NpcIdentity)Get(Get(host,"Selection"),"Target")).Slot].Center+" targetTile="+Main.tile[(int)Main.npc[((NpcIdentity)Get(Get(host,"Selection"),"Target")).Slot].Center.X/16,(int)Main.npc[((NpcIdentity)Get(Get(host,"Selection"),"Target")).Slot].Center.Y/16].active()+" source="+(timeline==null?"none":timeline.Count+"/"+timeline.Stop+" paused="+timeline.BudgetPaused+" sample="+timeline.SampleTick));
            if(Environment.GetEnvironmentVariable("JMR_DEDUP_ORACLE")=="1")
            {
                // Already-selected Basic remains the actual input. This
                // separate frozen opportunity oracle is not a production
                // allowance and is excluded from all normal timing windows.
                using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))
                {
                    var id=(NpcIdentity)Get(Get(host,"Selection"),"Target");Call(prediction,"PrepareActionPrefix",id,(long)Main.GameUpdateCount-1,120,120);
                    var full=(NpcTrajectory)Call(Get(prediction,"Cache"),"Read",1);var assembly=attack.GetType().Assembly;
                    var ammo=assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot").GetMethod("Capture",Flags).Invoke(null,new object[]{__0,__1});
                    var reference=full==null || ammo==null?null:FrozenOracle(attack,__0,__1,ammo,full);
                    var receiver=Main.npc[id.Slot];bool los=Collision.CanHitLine(__0.position,__0.width,__0.height,receiver.position,receiver.width,receiver.height);
                    Console.WriteLine("REASON separateOracle GUC="+Main.GameUpdateCount+" originalKind=Basic full="+(full?.Count??0)+" stop="+full?.Stop+" referenceContact="+(reference!=null)+" receive="+(reference?.ReceiveTick??-1)+" nativeLOS="+los+" floor43="+Main.tile[50,43].active());
                }
            }
        }
        private static AttackContact FrozenOracle(object aim,Player player,Item item,object ammo,NpcTrajectory timeline)
        {
            var assembly=aim.GetType().Assembly;var terrain=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain"),true);
            var start=(Vector2)assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels").GetMethod("LaunchOrigin",Flags,null,new[]{typeof(Player),typeof(Item)},null).Invoke(null,new object[]{player,item});
            // Reflection does not fill optional arguments. This diagnostic
            // starts at the live origin, so the existing final flag is false.
            return (AttackContact)aim.GetType().GetMethod("SolveOnTerrain",Flags).Invoke(null,new object[]{player,item,ammo,timeline,start,0,true,terrain,0,null,null,null,false});
        }
        internal static void RunReflection(object context)
        {
            var combat=Get(context,"Combat");var observation=Get(context,"CombatObservation");var aim=Get(combat,"Attack");
            NativeCombatObservationChecks.Save(observation,new ObservationOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),95,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;
            foreach(var npc in Main.npc)npc.active=false;
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.Center=new Vector2(970,667);n.life=n.lifeMax=10000;Array.Clear(n.immune,0,n.immune.Length);
            var id=new NpcIdentity((long)Get(observation,"Session"),n,2,n.generation,n.type,n.netID);
            var state=new NpcMotionState{Identity=id,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,Active=true,CanReceive=true};
            var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();
            NativeQuickItemChecks.BeginWorldStep();var path=new NpcTrajectory(id,(long)Main.GameUpdateCount-1,1,PredictionAssumption.HeldPlayerControls,PredictionStop.None,points,points.Length);
            var ammo=aim.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot").GetMethod("Capture",Flags).Invoke(null,new object[]{p,p.HeldItem});
            AttackContact result;using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))result=FrozenOracle(aim,p,p.HeldItem,ammo,path);
            Require(result!=null && ReferenceEquals(result.Timeline,path) && result.ReceiveTick>0 && p.inventory[54].stack==999,"separate frozen Basic-opportunity oracle actually receives its known static bullet target without consuming ammo");
            Console.WriteLine("PASS REFLECTION reasons frozen original oracle receive="+result.ReceiveTick+" optionalLaunchOriginAdvanced=False; no production allowance or Source cache mutation");
        }
        internal static void Attach(Harmony audit,object value,object aim)
        {
            host=value;attack=aim;times.Clear();stack.Clear();ranges.Clear();actionSolves.Clear();npcSteps=ownerArrays=ownerValues=0;
            foreach(var method in new[]{aim.GetType().GetMethod("PrepareCore",Flags),aim.GetType().GetMethod("PreparePlanCore",Flags),aim.GetType().GetMethod("SolveOnTerrain",Flags),Get(value,"Prediction").GetType().GetMethod("PrepareSample",Flags),aim.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostMeleeAttack").GetMethod("SolvePrepared",Flags),typeof(PlayerMotionContinuation).GetMethod("Advance",Flags)})
                audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatDedupReasons),nameof(Enter)),finalizer:new HarmonyMethod(typeof(NativeCombatDedupReasons),nameof(Exit)));
            audit.Patch(Get(value,"Prediction").GetType().GetMethod("PrepareSample",Flags),prefix:new HarmonyMethod(typeof(NativeCombatDedupReasons),nameof(Range)));
            audit.Patch(typeof(NpcMotion).GetMethods(Flags).Single(m=>m.Name=="Step" && m.GetParameters().Length==8),prefix:new HarmonyMethod(typeof(NativeCombatDedupReasons),nameof(NpcStep)));
            audit.Patch(aim.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostMeleeAttack").GetMethod("SolvePrepared",Flags),prefix:new HarmonyMethod(typeof(NativeCombatDedupReasons),nameof(ActionSolve)));
            var prefix=aim.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPlayerPrefix");
            if(prefix!=null)audit.Patch(prefix.GetConstructors(Flags)[0],prefix:new HarmonyMethod(typeof(NativeCombatDedupReasons),nameof(OwnerArray)));
            audit.Patch(aim.GetType().GetMethod("TryOrdinaryInput",Flags),postfix:new HarmonyMethod(typeof(NativeCombatDedupReasons),nameof(Supply)));
        }
        internal static void Report()
        {foreach(var pair in times)Console.WriteLine("REASON scope="+pair.Key+" inclusiveMs="+pair.Value[0]+" selfMs="+pair.Value[1]+" calls="+pair.Value[2]);foreach(var pair in ranges)Console.WriteLine("REASON range="+pair.Key+" calls="+pair.Value);foreach(var pair in actionSolves){Console.WriteLine("REASON actualHandSample="+pair.Key+" weaponStarts="+pair.Value);if(Environment.GetEnvironmentVariable("JMR_DEDUP_SINGLE_START")=="1")Require(pair.Value==1,"actual same-sample melee hand starts once with mechanism range, without 16/32/64 prefix replay");}Console.WriteLine("REASON work npcSteps="+npcSteps+" ownerArrays="+ownerArrays+" valueCapacity="+ownerValues);times.Clear();ranges.Clear();actionSolves.Clear();npcSteps=ownerArrays=ownerValues=0;}
    }
}
