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
                Require(!(bool)Call(aim,"TryCandidateHand",player,player.HeldItem,Get(donor,"Ammo"),changedSample,changedPhase,0) && borrow.GetValue(aim)==null,"actual hand transfer refuses changed "+(phaseChange?"native phase":"sample/capture")+" and retires borrow");
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
            var before=(AttackContact)Get(donor,"Contact");int started=solves();var result=(AttackContact)Call(aim,"PreparePlan",donor,changed,phase);
            Require(solves()>started,"changed future premises actually force a new weapon solve before hand donation, rather than just rebinding old Contact");
            Require(!ReferenceEquals(before,GetOptional(donor,"Contact")),"old Contact is retired before changed future premises are bound");
            // Rebind itself creates a different object; compare the solve's
            // frozen future source before and after, plus the dedicated gate.
            Require(!(bool)aim.GetType().GetMethod("SameSolvePremises",Flags).Invoke(null,new object[]{old,changed}),"future gravity read set rejects old pure computation reuse");
            Console.WriteLine("DEDUP premise futureGravityDirection="+motion.GravityDirection+"/"+other.GravityDirection+" geometryEquivalent=True oldContactRetired=True freshContact="+(result!=null));
            Call(donor,"Clear");
        }
    }
}
