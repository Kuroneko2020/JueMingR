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
    internal static class NativeCombatObstacleChecks
    {
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.ResetEffects();Main.screenPosition=new Vector2(600,500);p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;
            foreach(int slot in new[]{2,3}){var npc=Main.npc[slot];npc.SetDefaults(3);npc.whoAmI=slot;npc.active=true;npc.position=new Vector2(slot==2?920:820,646);npc.life=npc.lifeMax=10000;npc.target=0;npc.netOffset=Vector2.Zero;npc.velocity=Vector2.Zero;npc.aiStyle=-1;npc.noGravity=true;npc.knockBackResist=0;Array.Clear(npc.immune,0,npc.immune.Length);}
            var n=Main.npc[2];NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");Call(Get(host,"Selection"),"SampleMouse",input);NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0);var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");
            typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var q=Main.projectile.Single(s=>s.active && s.owner==0);int first=0;
            for(int tick=1;tick<=45 && q.active;tick++){NativeQuickItemChecks.BeginWorldStep();Main.npc[2].UpdateNPC(2);Main.npc[3].UpdateNPC(3);q.Update(q.whoAmI);if(n.life<10000){first=tick;break;}}
            Console.WriteLine("OBSTACLE body-first: contact="+plan?.Tick+" selectedFirst="+first+" blockerLife="+Main.npc[3].life+" active="+q.active);Require(Main.npc[3].life<10000 && first==0 && !q.active,"native single-penetration bullet retires on the observed intervening NPC");Require(plan==null,"a selected future behind a known receiver is not a legal ordinary contact");
            Slots(context,combat,host,attack,input);QualificationChanges(context,combat,host,attack,input);NativeCombatObservationChecks.Save(host,new ObservationOptions());
        }
        private static void QualificationChanges(object context,object combat,object host,object attack,object input)
        {
            foreach(int kind in new[]{0,1,2})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.ResetEffects();Main.screenPosition=new Vector2(600,500);p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;
                foreach(int slot in new[]{2,3}){var npc=Main.npc[slot];npc.SetDefaults(3);npc.whoAmI=slot;npc.active=true;npc.friendly=false;npc.dontTakeDamage=false;npc.position=new Vector2(slot==2?920:820,646);npc.life=npc.lifeMax=10000;npc.target=0;npc.netOffset=Vector2.Zero;npc.velocity=Vector2.Zero;npc.aiStyle=-1;npc.noGravity=true;npc.knockBackResist=0;Array.Clear(npc.immune,0,npc.immune.Length);Array.Clear(npc.buffType,0,npc.buffType.Length);Array.Clear(npc.buffTime,0,npc.buffTime.Length);}
                var target=Main.npc[2];var other=Main.npc[3];if(kind==0)other.active=false;else if(kind==1)other.friendly=true;else other.dontTakeDamage=true;
                NativeToolExecutionChecks.Sample(context,input,target.Center,true);Call(combat,"Sample");Call(Get(host,"Selection"),"SampleMouse",input);NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,target,0);Require(GetOptional(attack,"ExpectedImpact")!=null,"non-receiver does not consume the prepared ordinary bullet");
                other.active=true;other.friendly=false;other.dontTakeDamage=false;Require(GetOptional(attack,"ExpectedImpact")==null,"newly qualified receiver retires the old visible contact before consumption");
                typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var q=Main.projectile.Single(s=>s.active && s.owner==0);
                for(int tick=1;tick<=45 && q.active;tick++){NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(2);other.UpdateNPC(3);q.Update(q.whoAmI);}
                Require(other.life<10000 && target.life==10000 && GetOptional(attack,"ExpectedImpact")==null,"natural shot rejects the stale capability and keeps native first-receiver retirement");Console.WriteLine("PASS changed receiver qualification: kind="+kind+" otherLife="+other.life+" selectedLife="+target.life);
            }
        }
        private static void Slots(object context,object combat,object host,object attack,object input)
        {
            var obstacles=attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles");
            // type, selected slot, other owner immunity, other local immunity,
            // expected selected hit, expected other hit. Natural NPC bodies
            // overlap at the same damage endpoint; array order decides first.
            foreach(var row in new[]{new[]{14,7,0,0,0,1},new[]{14,3,0,0,1,0},new[]{36,7,0,0,1,1},new[]{14,7,10,0,0,1},new[]{36,7,10,0,1,0},new[]{36,7,0,-1,1,0},new[]{36,7,0,1,1,1},new[]{36,7,0,2,1,0}})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.ResetEffects();int other=row[1]==7?3:7;
                foreach(int slot in new[]{3,7}){var npc=Main.npc[slot];npc.SetDefaults(3);npc.whoAmI=slot;npc.active=true;npc.position=new Vector2(slot==row[1]?924:916,646);npc.life=npc.lifeMax=10000;npc.target=0;npc.netOffset=Vector2.Zero;npc.velocity=Vector2.Zero;npc.aiStyle=-1;npc.noGravity=true;npc.knockBackResist=0;Array.Clear(npc.immune,0,npc.immune.Length);}
                var target=Main.npc[row[1]];Main.npc[other].immune[0]=row[2];var shot=Main.projectile[0];shot.SetDefaults(row[0]);shot.owner=0;shot.whoAmI=0;shot.active=true;shot.Center=new Vector2(917,660);shot.velocity=new Vector2(10,0);shot.damage=20;shot.knockBack=0;shot.localNPCImmunity[other]=row[3];
                var id=new NpcIdentity((long)Get(host,"Session"),target,target.whoAmI,target.generation,target.type,target.netID);var points=new NpcTrajectoryPoint[3];for(int i=0;i<points.Length;i++)points[i]=new NpcTrajectoryPoint(i,new NpcMotionState{Identity=id,X=target.position.X,Y=target.position.Y,Width=target.width,Height=target.height,CanReceive=true,Active=true});var timeline=new NpcTrajectory(id,Main.GameUpdateCount,1,PredictionAssumption.FixedTarget,PredictionStop.None,points,points.Length);
                var capture=Activator.CreateInstance(obstacles,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{p,shot,timeline,0,false,false},null);bool allowed=(bool)obstacles.GetMethod("Pass",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(int),typeof(int),typeof(float),typeof(float)},null).Invoke(capture,new object[]{1,0,927f,660f});shot.Update(0);bool selectedHit=target.life<10000,otherHit=Main.npc[other].life<10000;
                Console.WriteLine("OBSTACLE slot: type="+row[0]+" target="+row[1]+" owner="+row[2]+" local="+row[3]+" model="+allowed+" selected="+selectedHit+" other="+otherHit+" remaining="+shot.penetrate);Require(allowed==(row[4]==1) && selectedHit==allowed && otherHit==(row[5]==1),"native slot order, finite penetration and current immunity agree at the same endpoint");
            }
        }
    }
}
