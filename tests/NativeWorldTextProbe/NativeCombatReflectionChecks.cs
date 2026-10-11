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
    // This leaf isolates original Damage's receive/reflect seam. Continuous
    // NPC tests own the actual phase transition producing the sampled flag.
    internal static class NativeCombatReflectionChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static void Run(object context)
        {
            var assembly=Get(context,"Combat").GetType().Assembly;var obstacleType=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true);int cases=0,reflected=0,received=0;
            foreach(int type in new[]{14,1,3,15,94,118,121,955,114,24})foreach(bool reflecting in new[]{false,true})foreach(bool offset in new[]{false,true})
            {
                NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),95,0,0);
                foreach(var other in Main.npc)other.active=false;
                var n=Main.npc[2];n.SetDefaults(618);n.whoAmI=2;n.active=true;n.position=new Vector2(900,580);n.life=n.lifeMax=100000;n.defense=0;n.reflectsProjectiles=reflecting;n.dontTakeDamage=false;Array.Clear(n.immune,0,n.immune.Length);
                n.netOffset=offset?new Vector2(180,0):Vector2.Zero;
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),n.Center+n.netOffset,new Vector2(4,0),type,80,0,p.whoAmI);var shot=Main.projectile[slot];shot.oldVelocity=shot.velocity;
                var identity=(NpcIdentity)assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true).GetMethod("Identity",Flags).Invoke(null,new object[]{n,1L});
                var state=new NpcMotionState{Identity=identity,X=n.position.X,Y=n.position.Y,NetOffsetX=n.netOffset.X,NetOffsetY=n.netOffset.Y,Width=n.width,Height=n.height,CanReceive=true,ReflectsProjectiles=reflecting};
                var timeline=Path(identity,state);var obstacles=Activator.CreateInstance(obstacleType,Flags,null,new object[]{p,shot,timeline,0,false,false},null);
                // The first native action starts the private penetration and
                // immunity history, just as every production replay does.
                bool allowed=(bool)obstacleType.GetMethod("PassOuter",Flags).Invoke(obstacles,new object[]{1,1,1,shot.Center.X,shot.Center.Y,0});
                int life=n.life;shot.Damage();bool hit=n.life<life;
                Require(allowed==hit && (hit!=shot.reflected),"original Damage distinguishes reflection from actual receive: "+type+"/"+reflecting+" allowed="+allowed+" nativeHit="+hit+" reflected="+shot.reflected);
                if(shot.reflected)reflected++;if(hit)received++;cases++;
                n.reflectsProjectiles=!reflecting;Require(!(bool)Get(obstacles,"Unchanged"),"same-identity reflection phase change retires a prepared receiver lease");
                // Unknown future reflection is not a fake invulnerable NPC.
                // The motion/hand contact survives; only reflectable shots
                // lack proof at this possible receive.
                state.ReflectsProjectiles=false;state.ProjectileReflectionUnknown=true;var uncertain=Path(identity,state);
                Require(!AttackIntercept.Equivalent(timeline,uncertain),"future reflection uncertainty participates in preview identity");
                var box=uncertain[1].ReceiveBounds;
                Require(AttackIntercept.MeleeContact(uncertain,1,box.X,box.Y,box.Width,box.Height,box.CenterX,box.CenterY)!=null,"reflection uncertainty does not remove ordinary hand melee receive");
            }
            Require(cases==40 && reflected==16 && received==24,"native receive oracle keeps all reflectable and nonreflectable role controls, including temporary NPC network offsets");
            Console.WriteLine("PASS original projectile reflection receive cases="+cases+" reflected="+reflected+" actualDamage="+received);
            DerivedReceiveLeases(context,assembly,obstacleType);
            BeforeNpcReflection(context,assembly,obstacleType);
        }
        private static void BeforeNpcReflection(object context,Assembly assembly,Type obstacleType)
        {
            int cases=0,reflections=0,lateBirths=0;int mode=Main.GameMode,net=Main.netMode;
            try
            {
                foreach(int member in new[]{473,474,475,476})foreach(int type in new[]{14,15,121,24})foreach(bool selected in new[]{false,true})foreach(bool late in new[]{false,true})foreach(bool touches in new[]{false,true})
                {
                    NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),95,0,0);Main.GameMode=1;Main.netMode=0;
                    foreach(var other in Main.npc)other.active=false;
                    var n=Main.npc[selected?2:1]=new NPC{whoAmI=selected?2:1};n.SetDefaults(member);n.active=true;n.target=p.whoAmI;n.position=new Vector2(900,580);n.velocity=new Vector2(14,0);n.ai[0]=3;n.ai[1]=20;n.dontTakeDamage=true;n.immune[p.whoAmI]=200;n.netOffset=new Vector2(180,0);
                    NPC receiver=n;
                    if(!selected){receiver=Main.npc[2]=new NPC{whoAmI=2};receiver.SetDefaults(3);receiver.active=true;receiver.position=new Vector2(1400,580);}
                    // The late member is born after this outer's real NPC AI.
                    // Damage cannot reflect it through an invulnerable chest.
                    if(late)n.UpdateNPC(n.whoAmI);
                    var identity=(NpcIdentity)assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true).GetMethod("Identity",Flags).Invoke(null,new object[]{receiver,1L});
                    var state=new NpcMotionState{Identity=identity,X=receiver.position.X,Y=receiver.position.Y,Width=receiver.width,Height=receiver.height,CanReceive=!selected,ReflectsProjectiles=selected};
                    var path=Path(identity,state);Vector2 center=touches?n.Center:n.Center+new Vector2(0,400);
                    int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,new Vector2(4,0),type,80,0,p.whoAmI);var q=Main.projectile[slot];q.oldVelocity=q.velocity;
                    var obstacles=Activator.CreateInstance(obstacleType,Flags,null,new object[]{p,q,path,0,!late,false},null);
                    bool allowed=(bool)obstacleType.GetMethod("BeforeNpc",Flags).Invoke(obstacles,new object[]{1,1,q.Center.X,q.Center.Y,0,0});
                    if(!late)n.UpdateNPC(n.whoAmI);else q.Damage();
                    Require(allowed==!q.reflected,"original AI87 pre-NPC reflection ignores immunity, uses raw body and respects post-NPC births: "+member+"/"+type+" selected="+selected+" late="+late+" touch="+touches);
                    if(q.reflected)reflections++;
                    if(late)
                    {
                        lateBirths++;allowed=(bool)obstacleType.GetMethod("BeforeNpc",Flags).Invoke(obstacles,new object[]{2,2,q.Center.X,q.Center.Y,0,0});n.UpdateNPC(n.whoAmI);
                        Require(allowed==!q.reflected,"late birth participates in the next actual NPC scan");
                    }
                    if(!selected){n.ai[1]++;Require(!(bool)Get(obstacles,"Unchanged"),"invulnerable other reflector's clock is a consume dependency");}
                    cases++;
                }
            }
            finally{Main.GameMode=mode;Main.netMode=net;}
            Require(cases==128 && reflections>0 && lateBirths==64,"all four AI87 members keep selected/other, reflectable/nonreflectable and real birth phase controls");
            Console.WriteLine("PASS original pre-NPC reflection cases="+cases+" reflectedBeforeDamage="+reflections+" postNpcBirths="+lateBirths);
            ReflectionBeforeFutureReceive(context,assembly,obstacleType);
        }
        private static void ReflectionBeforeFutureReceive(object context,Assembly assembly,Type obstacleType)
        {
            int mode=Main.GameMode,net=Main.netMode;
            try
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),95,0,0);Main.GameMode=1;Main.netMode=0;
                foreach(var other in Main.npc)other.active=false;
                var n=Main.npc[2]=new NPC{whoAmI=2};n.SetDefaults(473);n.active=true;n.target=p.whoAmI;n.position=new Vector2(900,580);n.ai[0]=3;n.ai[1]=178;n.dontTakeDamage=true;
                var id=(NpcIdentity)assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true).GetMethod("Identity",Flags).Invoke(null,new object[]{n,1L});
                var points=new NpcTrajectoryPoint[5];
                for(int i=0;i<points.Length;i++)points[i]=new NpcTrajectoryPoint(i,new NpcMotionState{Identity=id,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=i>=3,ReflectsProjectiles=i>0 && i<3});
                var path=new NpcTrajectory(id,(long)Main.GameUpdateCount,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
                // The guard is only 28px wide: Center-20 leaves the 4px
                // bullet outside even after this four-step horizon. Use an
                // actual overlap but retain a nonzero inverse-solver ray.
                var origin=n.Center-new Vector2(5,0);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),origin,Vector2.UnitX,14,80,0,p.whoAmI);var q=Main.projectile[slot];q.oldVelocity=q.velocity;
                Require(q.Hitbox.Intersects(n.Hitbox),"guard-to-receive counterexample starts with a real original pre-NPC body overlap");
                var obstacles=Activator.CreateInstance(obstacleType,Flags,null,new object[]{p,q,path,0,true,false},null);var pass=obstacleType.GetMethod("PassOuter",Flags);var before=obstacleType.GetMethod("BeforeNpc",Flags);
                Func<int,int,float,float,bool> damage=(k,t,x,y)=>(bool)pass.Invoke(obstacles,new object[]{k,t,k,x,y,0});
                Func<int,int,float,float,bool> first=(k,t,x,y)=>(bool)before.Invoke(obstacles,new object[]{t,k,x,y,0,0});
                var motion=new AttackMotion(1,0,0,1,q.width,q.height,4);
                var old=AttackIntercept.Solve(origin.X,origin.Y,motion,path,0,0,damagePassage:damage);
                var corrected=AttackIntercept.Solve(origin.X,origin.Y,motion,path,0,0,damagePassage:damage,beforeMotion:first);
                Require(old!=null && old.Tick==3,"omitting NPC reflection reaches the unchanged later receive window at contact3");
                Require(corrected==null,"pre-NPC reflection retires this shot before the later receive window");
                n.UpdateNPC(2);Require(q.reflected && !q.friendly && n.dontTakeDamage,"the original NPC scan has already reflected this shot before any possible selected Damage");
                Console.WriteLine("PASS native guard-to-receive counterexample: omittedNpcPhase=contact3 actualReflectedBeforeDamage corrected=noContact");
            }
            finally{Main.GameMode=mode;Main.netMode=net;}
        }
        private static void DerivedReceiveLeases(object context,Assembly assembly,Type obstacleType)
        {
            int cases=0;
            foreach(var pair in new[]{new[]{510,511},new[]{183,181}})foreach(bool other in new[]{false,true})
            {
                NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),95,0,0);
                foreach(var n in Main.npc)n.active=false;
                var target=Main.npc[2];target.SetDefaults(3);target.whoAmI=2;target.active=true;target.position=new Vector2(900,580);Array.Clear(target.immune,0,target.immune.Length);
                var front=Main.npc[1];front.SetDefaults(3);front.whoAmI=1;front.active=other;front.position=new Vector2(860,580);Array.Clear(front.immune,0,front.immune.Length);
                var id=(NpcIdentity)assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true).GetMethod("Identity",Flags).Invoke(null,new object[]{target,1L});
                var path=Path(id,new NpcMotionState{Identity=id,X=target.position.X,Y=target.position.Y,Width=target.width,Height=target.height,CanReceive=true});
                var parent=new Projectile();parent.SetDefaults(pair[0]);parent.owner=p.whoAmI;
                var child=new Projectile();child.SetDefaults(pair[1]);child.owner=p.whoAmI;int slot=other?1:2;
                Require(child.usesIDStaticNPCImmunity,"real derivative owns a different native static immunity policy");
                uint previous=Projectile.perIDStaticNPCImmunity[child.immunityIdentity,slot];
                try
                {
                    Projectile.perIDStaticNPCImmunity[child.immunityIdentity,slot]=0;
                    var root=Activator.CreateInstance(obstacleType,Flags,null,new object[]{p,parent,path,0,false,false},null);
                    for(int i=0;i<16;i++)obstacleType.GetMethod("ForkForSpawn",Flags).Invoke(root,new object[]{child,false});
                    Require((bool)Get(root,"Unchanged") && ((System.Collections.ICollection)Get(root,"receiveLeases")).Count==1,"repeated candidate members retain one distinct child receive lease");
                    Projectile.perIDStaticNPCImmunity[child.immunityIdentity,slot]=Main.GameUpdateCount+30;
                    Require(!(bool)Get(root,"Unchanged") && Main.npc[slot].immune[p.whoAmI]==0,"derived-only static immunity invalidates parent consume even when owner immunity is unchanged");cases++;
                }
                finally{Projectile.perIDStaticNPCImmunity[child.immunityIdentity,slot]=previous;}
            }
            Console.WriteLine("PASS derived receive leases="+cases+" repeatedCandidates=16 perPolicy=1");
        }
        private static NpcTrajectory Path(NpcIdentity id,NpcMotionState state)
        {var points=Enumerable.Range(0,3).Select(i=>new NpcTrajectoryPoint(i,state)).ToArray();return new NpcTrajectory(id,(long)Main.GameUpdateCount,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);}
    }
}
