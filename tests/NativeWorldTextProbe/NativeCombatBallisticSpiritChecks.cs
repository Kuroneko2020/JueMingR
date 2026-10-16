using System;
using System.Linq;
using System.Reflection;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatBallisticSpiritChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static NpcTrajectory Path(NPC target)
        {
            var identity=new NpcIdentity(1,target,target.whoAmI,target.generation,target.type,target.netID);var state=new NpcMotionState{Identity=identity,X=target.position.X,Y=target.position.Y,Width=target.width,Height=target.height,CanReceive=true,Active=true};
            var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(identity,Main.GameUpdateCount,1,PredictionAssumption.None,PredictionStop.None,points,points.Length);
        }
        internal static void Run(object context)
        {
            var host=Get(context,"Combat").GetType().Assembly;var model=host.GetType("JueMingR.TerrariaHost.Combat.HostSpiritFlameAttack",true);int cases=0,actions=0;
            foreach(int phase in new[]{-2,2})foreach(bool chase in new[]{true,false})foreach(bool solid in new[]{true,false})
            {
                var player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3779,0,0);Main.windPhysics=false;foreach(var npc in Main.npc)npc.active=false;
                var target=Main.npc[2];target.SetDefaults(1);target.active=chase;target.whoAmI=2;target.position=new Vector2(850,350);var path=Path(target);
                if(solid)NativeToolsChecks.Tile(43,21,1);
                int slot=Projectile.NewProjectile(player.GetProjectileSource_Item(player.HeldItem),new Vector2(700,350),Vector2.Zero,659,0,0,player.whoAmI,phase);var native=Main.projectile[slot];
                var shadow=(Projectile)host.GetType("JueMingR.TerrariaHost.Combat.HostBetsyFlameAttack",true).GetMethod("Copy",Flags).Invoke(null,new object[]{native});
                var targets=host.GetType("JueMingR.TerrariaHost.Combat.HostProjectileTargets",true).GetMethod("Capture",Flags).Invoke(null,new object[]{shadow,1L});
                var terrain=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");
                for(int action=1;action<=90;action++)
                {
                    Require((bool)model.GetMethod("Ai",Flags).Invoke(null,new object[]{shadow,terrain,targets,path,action}),"AI129 concrete captured pursuit/solid");typeof(Projectile).GetMethod("AI",Flags).Invoke(native,null);
                    Require(Vector2.DistanceSquared(native.velocity,shadow.velocity)<.000001f && native.ai[0]==shadow.ai[0] && native.localAI[0]==shadow.localAI[0] && native.timeLeft==shadow.timeLeft,"AI129 native pursuit/retirement phase="+phase+" chase="+chase+" solid="+solid+" action="+action);
                    native.position+=native.velocity;shadow.position+=shadow.velocity;native.timeLeft--;shadow.timeLeft--;actions++;
                }
                cases++;
            }
            foreach(int separation in new[]{44,80})
            {
                var player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3779,0,0);foreach(var npc in Main.npc)npc.active=false;
                // A new burst case owns a new receiver identity. SetDefaults
                // on the old object keeps its preceding family's immunity.
                typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{2,(byte)0});
                var target=Main.npc[2];target.SetDefaults(1);target.active=true;target.whoAmI=2;target.Center=new Vector2(850,350);target.life=target.lifeMax=10000;target.knockBackResist=0;var path=Path(target);
                Require(target.immune[player.whoAmI]==0,"AI129 new burst receiver starts without an earlier family's owner immunity");
                int slot=Projectile.NewProjectile(player.GetProjectileSource_Item(player.HeldItem),target.Center+new Vector2(separation,0),Vector2.Zero,659,50,0,player.whoAmI,2);var shot=Main.projectile[slot];shot.timeLeft=1;
                var terrain=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");
                var root=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{player,shot,path,0,false,true},null);
                var phase=Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");
                var clock=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,phase},null);
                var contact=(JueMingR.Features.Combat.AttackContact)model.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{player,new[]{shot},path,clock,terrain,root});
                int burstLife=target.life;NativeQuickItemChecks.BeginWorldStep();shot.Update(slot);
                Console.WriteLine("BALLISTIC-SPIRIT burst separation="+separation+" contact="+(contact==null?"null":contact.Tick.ToString())+" active="+shot.active+" width="+shot.width+" life="+burstLife+"->"+target.life+" ownerImmune="+target.immune[player.whoAmI]+" dontTakeDamage="+target.dontTakeDamage+" immortal="+target.immortal+" friendly="+target.friendly);
                Require(separation==44?contact!=null && contact.Tick==1 && !shot.active && shot.width==80 && target.life<burstLife:contact==null && target.life==burstLife,"AI129 exact same-body Kill80 burst with outside counterexample separation="+separation);
            }
            // Component hand: a real chaseable receiver, actual Shoot random
            // birth and original Damage. Its published stationary trajectory
            // is a supplied component premise, separate from the full Main.
            var p=NativeCombatBallisticCandidateChecks.Scene(context,3779,50);var n=Main.npc[2];var center=n.Center;n.SetDefaults(1);n.whoAmI=2;n.active=true;n.Center=center;n.life=n.lifeMax=10000;n.knockBackResist=0;
            var observation=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");
            NativeCombatObservationChecks.Save(observation,new JueMingR.Features.Combat.ObservationOptions(false,true,false,false,false,50,true,true));
            NativeToolExecutionChecks.Sample(context,Get(context,"Input"),n.Center-new Vector2(500,0),true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,n,0);Require(GetOptional(attack,"ExpectedImpact")!=null,"AI129 true autonomous hand contact");
            typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});Require((bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced","AI129 actual hand consumer");
            int life=n.life;for(int tick=0;tick<120 && n.life==life;tick++)
            {NativeQuickItemChecks.BeginWorldStep();for(int slot=0;slot<Main.maxProjectiles;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;try{Main.projectile[slot].Update(slot);}finally{Main.ProjectileUpdateLoopIndex=-1;}}}
            Require(n.life<life,"AI129 legal random birth obtains actual native Damage");
            Console.WriteLine("BALLISTIC-SPIRIT nativeAICases="+cases+" aiActions="+actions+" actualHand=1");
        }
    }
}

