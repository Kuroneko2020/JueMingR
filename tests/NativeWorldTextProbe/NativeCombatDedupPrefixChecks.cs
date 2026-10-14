using System;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatDedupPrefixChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static int advances;private static bool interrupt;
        private static void Advance(ref PredictionPlayerMotion __0)
        {advances++;if(interrupt && advances==4){__0.X+=900;throw new CombatPredictionBudgetExceededException();}}
        internal static void Run(object aim,Player player,NpcTrajectory timeline)
        {
            var assembly=aim.GetType().Assembly;var terrainType=assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain");var frameType=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPlayerFrame");var prefixType=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPlayerPrefix");
            var frame=frameType.GetMethod("Capture",Flags).Invoke(null,new object[]{player,timeline});
            var reference=(PredictionPlayerMotion)Get(frame,"Motion");var world=(PredictionEnvironment)Get(frame,"Environment");
            var referenceTerrain=(IPredictionTerrain)Activator.CreateInstance(terrainType,true);
            var terrain=Activator.CreateInstance(terrainType,true);
            var prefix=Activator.CreateInstance(prefixType,Flags,null,new object[]{frame,terrain,120},null);
            var audit=new Harmony("JueMingR.Tests.OwnerPrefix");audit.Patch(typeof(PlayerMotionContinuation).GetMethod("Advance",Flags),prefix:new HarmonyMethod(typeof(NativeCombatDedupPrefixChecks),nameof(Advance)));
            try
            {
                using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))
                {
                    advances=0;interrupt=true;bool paused=false;
                    try{Call(prefix,"TryRead",10,null);}catch(TargetInvocationException e){if(!(e.InnerException is CombatPredictionBudgetExceededException))throw;paused=true;}
                    Require(paused && (int)Get(prefix,"completed")==3,"partial fourth owner action cannot be committed as a completed prefix");
                    interrupt=false;advances=0;var resumed=new object[]{10,null};Require((bool)Call(prefix,"TryRead",resumed) && advances==7,"resume replays only the unfinished owner action, preserving the first three complete values");
                    foreach(int request in new[]{0,1,16,16,32,64})
                    {
                        int prior=(int)Get(prefix,"completed");advances=0;var result=new object[]{request,null};Require((bool)Call(prefix,"TryRead",result),"finite owner prefix is available");
                        Require(advances==Math.Max(0,request-prior),"only the requested unfinished owner suffix advances, including repeated reads");
                        var fresh=(PredictionPlayerMotion)Get(frame,"Motion");PredictionStop stop;
                        // Independent one-shot reference, outside the shared
                        // count. All state fields, not only position, compare.
                        for(int i=0;i<request;i++)Require(PlayerMotionContinuation.Advance(ref fresh,world,referenceTerrain,out stop),"reference action completes");
                        Require(fresh.SameSample((PredictionPlayerMotion)result[1]),"shared owner prefix equals one-shot full state at "+request);
                    }
                }
                Console.WriteLine("DEDUP ownerPrefix 0/1/16/16/32/64 equals full-state reference; partial fourth action rejected; resume=7 not10");
            }
            finally{interrupt=false;audit.UnpatchAll(audit.Id);}
        }
    }
}
