using System;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatDedupSourceChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var aim=Get(combat,"Attack");var input=Get(context,"Input");var assembly=aim.GetType().Assembly;
            foreach(string kind in new[]{"delayed","controlled","flight"})
            {
                int item=kind=="controlled"?3475:kind=="flight"?1228:95;
                NativeCombatObservationChecks.Save(host,new ObservationOptions());
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,item,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;p.statMana=p.statManaMax=p.statManaMax2=200;
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.Center=new Vector2(850,667);n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=100000;
                NativeToolExecutionChecks.Sample(context,input,new Vector2(600,500),true);Call(combat,"Sample");NativeQuickItemChecks.BeginWorldStep();
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,false,50,true,true));
                if(kind=="flight")
                {p.itemAnimation=p.itemAnimationMax=25;p.itemTime=0;typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});Require(Main.projectile.Any(q=>q.active && q.type==222),"flight refusal starts with an actual original spear birth");p.itemTime=30;}
                if(kind=="delayed"){p.itemTime=6;p.itemAnimation=p.itemAnimationMax=25;}
                var id=new NpcIdentity((long)Get(host,"Session"),n,2,0,n.type,n.netID);var state=new NpcMotionState{Identity=id,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,Active=true,CanReceive=true,NoTileCollide=true};
                var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();var path=new NpcTrajectory(id,(long)Main.GameUpdateCount-1,1,PredictionAssumption.HeldPlayerControls,PredictionStop.None,points,121);
                var phase=Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase"),"BeforeNpc");var plan=Activator.CreateInstance(aim.GetType().GetNestedType("PreviewPlan",Flags),true);
                using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))Call(aim,"PreparePlan",plan,path,phase);
                var solution=NativeCombatDedupChecks.PlanSolution(plan);Require(solution.HasPoint && solution.Reachability=="Reachable" && solution.Timeline!=null,"refusal starts with a useful actual model result: "+kind);
                bool ordinary=(bool)assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackWindow",true).GetMethod("Ordinary",Flags).Invoke(null,new object[]{p.HeldItem});
                Require(kind=="delayed"?(int)Get(plan,"Delay")>0:kind=="controlled"?(bool)Get(plan,"Balanced") && !ordinary:GetOptional(plan,"Contact")!=null && (GetOptional(plan,"Flight")!=null || (bool)Get(plan,"HadFlight")),"production plan preserves its actual delayed/held/flight role: "+kind);
                Require(!(bool)Call(aim,"CandidateHandPrepared",plan),"the actual candidate admission gate rejects this non-current ordinary hand role: "+kind);
                p.itemTime=0;
                var ammo=assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot").GetMethod("Capture",Flags).Invoke(null,new object[]{p,p.HeldItem});
                aim.GetType().GetField("candidateHandPlan",Flags).SetValue(aim,plan);
                bool shared;using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))shared=NativeCombatDedupChecks.TryDonation(aim,plan,p,ammo,path,phase);
                Require(!shared && GetOptional(aim,"candidateHandPlan")==null,"real "+kind+" calculation is not adopted as current ordinary hand and its owner-exit borrow retires");
                Console.WriteLine("DEDUP real source="+kind+" positivePreview=True handDonation=False");
            }
            NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());Call(aim,"Reset");
        }
    }
}
