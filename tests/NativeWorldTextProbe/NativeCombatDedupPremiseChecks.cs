using System;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatDedupPremiseChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        internal static void SameOwnerRetirement(object aim,object source,Player player)
        {
            var cache=(NpcPredictionCache)Get(source,"Cache");var old=cache.Read(1);
            Console.WriteLine("DEDUP same owner fixture required="+cache.Required+" minimum="+cache.MinimumRequired+" sourceStop="+Get(source,"outcomeStop")+" sourceFuture="+Get(source,"outcomeFuture"));
            Require(old!=null,"dedicated fully modeled same-owner fixture has an actual Source publication");
            var phase=Enum.Parse(aim.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase"),"BeforeNpc");
            var plan=Activator.CreateInstance(aim.GetType().GetNestedType("PreviewPlan",Flags),true);
            Call(aim,"PreparePlan",plan,old,phase);var solution=NativeCombatDedupChecks.PlanSolution(plan);Require(solution.HasPoint && solution.Reachability=="Reachable","same-owner retirement starts with a real reachable representative hand solve");
            plan.GetType().GetField("MotionTerrain",Flags).SetValue(plan,Call(source,"FreezeCandidateTerrain",old));
            plan.GetType().GetField("MotionSource",Flags).SetValue(plan,source);
            var borrow=aim.GetType().GetField("candidateHandPlan",Flags);borrow.SetValue(aim,plan);
            Require(NativeCombatDedupChecks.TryDonation(aim,plan,player,Get(plan,"Ammo"),old,phase),"the completed same-owner hand donates before its actual Source retirement");
            // Retire and recapture this producer at the same real native sample.
            // No live input/geometry changes and no epoch field is rewritten.
            Call(source,"Clear");cache.Demand(1,1,old.Count-1);
            Call(source,"PrepareActionPrefix",old.Identity,old.SampleTick,old.Count-1,NpcPredictionCache.Horizon);
            var rebuilt=cache.Read(1);
            Console.WriteLine("DEDUP same owner recapture old="+old.Count+"/"+old.RelationVersion+"/"+old.Stop+"/"+old.Quality+" new="+(rebuilt==null?"none":rebuilt.Count+"/"+rebuilt.RelationVersion+"/"+rebuilt.Stop+"/"+rebuilt.Quality));
            Require(rebuilt!=null && rebuilt.RelationVersion!=old.RelationVersion && AttackIntercept.Equivalent(old,rebuilt),"actual same-owner Clear/recapture changes its epoch while this fixed future stays geometrically equal");
            borrow.SetValue(aim,plan);
            Require(!NativeCombatDedupChecks.TryDonation(aim,plan,player,Get(plan,"Ammo"),rebuilt,phase) && borrow.GetValue(aim)==null,"the same Source's changed epoch still rejects and retires its old hand donor");
            Console.WriteLine("DEDUP same owner retired epoch="+old.RelationVersion+"/"+rebuilt.RelationVersion+" geometryEquivalent=True donation=False");
            Call(plan,"Clear");Call(aim,"ClearExecution");
        }
        internal static void Run(object aim,object donor,Player player,object phase,Func<int> solves)
        {
            var old=(NpcTrajectory)Get(donor,"Timeline");var points=Enumerable.Range(0,old.Count).Select(i=>old[i]).ToArray();
            NativeCombatDedupPrefixChecks.Run(aim,player,old);
            var borrow=aim.GetType().GetField("candidateHandPlan",Flags);
            foreach(bool phaseChange in new[]{false,true})
            {
                var changedSample=phaseChange?old:new NpcTrajectory(old.Identity,old.SampleTick+1,old.Version,old.Assumptions,old.Stop,points,points.Length,old.Strategy,old.RelationVersion,old.Quality);
                var changedPhase=phaseChange?Enum.Parse(phase.GetType(),"CompletedWorld"):phase;
                borrow.SetValue(aim,donor);
                Require(!NativeCombatDedupChecks.TryDonation(aim,donor,player,Get(donor,"Ammo"),changedSample,changedPhase) && borrow.GetValue(aim)==null,"actual hand transfer refuses changed "+(phaseChange?"native phase":"sample/capture")+" and owner exit retires borrow");
            }
            var far=new PredictionGravitySource[old.Count];var near=new PredictionGravitySource[old.Count];
            for(int i=0;i<old.Count;i++){far[i]=new PredictionGravitySource(true,false,old.Identity,0,0);near[i]=far[i];}
            // Initial geometry and player inputs stay identical. Only a future
            // real gravity action differs, with a physical owner consequence.
            near[2]=new PredictionGravitySource(true,true,old.Identity,player.Center.X,player.Center.Y);
            var a=new PredictionGravitySources(far,old.Count,player,player.whoAmI,true,old.Identity,old.SampleTick);
            var b=new PredictionGravitySources(near,old.Count,player,player.whoAmI,true,old.Identity,old.SampleTick);
            var motion=(PredictionPlayerMotion)Get(donor,"PlayerMotion");motion.GravityControlCaptured=true;motion.GravitySourceExpected=true;motion.GravityAction=2;motion.ForcedGravityTicks=0;
            var other=motion;PredictionStop stop;
            Require(PlayerMotionContinuation.BeginGravity(ref motion,new PredictionEnvironment{GravitySources=a},out stop) && PlayerMotionContinuation.BeginGravity(ref other,new PredictionEnvironment{GravitySources=b},out stop) && motion.GravityDirection!=other.GravityDirection,"same initial player has a different actual future gravity action, not merely a changed cache key");
            var changed=new NpcTrajectory(old.Identity,old.SampleTick,old.Version,old.Assumptions,old.Stop,points,points.Length,PredictionStrategy.RollingConditional,old.RelationVersion,old.Quality,b,old.WallSources,old.BudgetPaused,old.CalculationToken);
            Require(AttackIntercept.Equivalent(old,changed),"old geometry-only equality would accept the changed future source");
            var before=NativeCombatDedupChecks.PlanSolution(donor);int started=solves();Call(aim,"PreparePlan",donor,changed,phase);var result=NativeCombatDedupChecks.PlanSolution(donor);
            Require(solves()>started,"changed future premises actually force a new weapon solve before hand donation, rather than rebinding old computation");
            Require(result.Timeline!=null && ReferenceEquals(result.Timeline,changed) && !ReferenceEquals(before.Timeline,result.Timeline) && Equals(result.Timeline.GravitySources,b),"fresh representative result owns the changed future premises instead of rebinding the old computation");
            // Rebind itself creates a different object; compare the solve's
            // frozen future source before and after, plus the dedicated gate.
            Require(!(bool)aim.GetType().GetMethod("SameSolvePremises",Flags).Invoke(null,new object[]{old,changed}),"future gravity read set rejects old pure computation reuse");
            Console.WriteLine("DEDUP premise futureGravityDirection="+motion.GravityDirection+"/"+other.GravityDirection+" geometryEquivalent=True oldComputationRetired=True freshPoint="+result.HasPoint);
            Call(donor,"Clear");
        }
    }
}
