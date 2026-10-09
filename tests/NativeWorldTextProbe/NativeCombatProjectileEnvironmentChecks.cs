using System;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatProjectileEnvironmentChecks
    {
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");bool wind=Main.windPhysics;float oldSpeed=Main.windSpeedCurrent,oldStrength=Main.windPhysicsStrength;double oldSurface=Main.worldSurface;Main.windPhysics=false;
            try
            {
                foreach(var row in new[]{new[]{95,97,-1},new[]{95,97,0},new[]{95,97,2},new[]{1254,97,0}})
                {
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,row[0],0,0);p.position=new Vector2(700,646);p.ResetEffects();Main.screenPosition=new Vector2(600,500);p.inventory[54].SetDefaults(row[1]);p.inventory[54].stack=999;
                    if(row[2]>=0)for(int x=40;x<=73;x++)for(int y=30;y<=42;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(row[2]);}
                    var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(920,646);n.life=n.lifeMax=10000;n.target=0;n.netOffset=Vector2.Zero;n.velocity=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);
                    NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));
                    Collision.up=true;Collision.down=true;Collision.honey=true;Collision.shimmer=true;NativeCombatAimChecks.Prepare(host,attack,n,0);Require(Collision.up && Collision.down && Collision.honey && Collision.shimmer,"preparation preserves native collision query flags");
                    var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");Require(plan!=null,"known uniform projectile environment prepares a contact");typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var q=Main.projectile.Single(s=>s.active && s.owner==0);var birth=q.Center;var velocity=q.velocity;int first=0;Vector2 firstDelta=Vector2.Zero;
                    for(int tick=1;tick<=120 && q.active;tick++){NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(2);q.Update(q.whoAmI);if(tick==1){firstDelta=q.Center-birth;Require(Vector2.DistanceSquared(q.velocity,velocity)<.0001f,"liquid translates without multiplying stored bullet velocity");}if(n.life<10000){first=tick;break;}}
                    float scale=q.ignoreWater || row[2]<0?1:row[2]==2?.25f:.5f;Require(Vector2.DistanceSquared(firstDelta,velocity*(q.extraUpdates+1)*scale)<.001f,"native first movement uses the actual uniform liquid displacement");Console.WriteLine("ENV contact: projectile="+q.type+" liquid="+row[2]+" ignoreWater="+q.ignoreWater+" delta="+firstDelta+" velocity="+velocity+" expected="+plan.Tick+" first="+first);Require(first>0 && first==plan.Tick,"known uniform liquid first Damage matches planned time");
                }
                Boundaries(context,combat,host,attack,input);Wind(context,combat,host,attack,input);
            }
            finally{Main.windPhysics=wind;Main.windSpeedCurrent=oldSpeed;Main.windPhysicsStrength=oldStrength;Main.worldSurface=oldSurface;NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
        private static void Boundaries(object context,object combat,object host,object attack,object input)
        {
            foreach(var row in new[]{new[]{95,97,0,1},new[]{1254,97,3,0},new[]{218,0,0,0},new[]{113,0,0,0},new[]{113,0,3,0},new[]{495,0,3,0}})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,row[0],0,0);p.position=new Vector2(700,646);p.ResetEffects();p.channel=p.controlUseItem=true;Main.screenPosition=new Vector2(600,500);Main.leftWorld=Main.topWorld=0;Main.rightWorld=Main.maxTilesX*16;Main.bottomWorld=Main.maxTilesY*16;
                if(row[1]>0){p.inventory[54].SetDefaults(row[1]);p.inventory[54].stack=999;}
                for(int x=row[3]==1?50:40;x<=73;x++)for(int y=30;y<=42;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(row[2]);}
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(920,646);n.life=n.lifeMax=10000;n.target=0;n.netOffset=Vector2.Zero;n.velocity=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);
                NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0,true);
                typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var q=Main.projectile.Single(s=>s.active && s.owner==0);NativeCombatAimChecks.Prepare(host,attack,n,0,true);var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");bool regularWaterIgnore=row[0]==113 && row[2]==0;
                Require(regularWaterIgnore?plan!=null:plan==null,"only the modeled ignore-water controlled continuation publishes a contact in this boundary matrix");int first=0;
                for(int tick=1;tick<=45 && q.active;tick++){NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(2);q.Update(q.whoAmI);if(n.life<10000){first=tick;break;}}
                Console.WriteLine("ENV boundary: weapon="+row[0]+" type="+q.type+" liquid="+row[2]+" transition="+row[3]+" plan="+plan?.Tick+" nativeFirst="+first+" active="+q.active+" ignoreWater="+q.ignoreWater+" shimmer="+q.shimmerWet);
                Require(row[3]==1 || regularWaterIgnore?first>0:!q.active && first==0,"native transition may remain useful manually; water destruction and shimmer are distinct native exits");
                if(regularWaterIgnore)Require(first==plan.Tick+1,"projectile-stage index0 maps to the first actual component tick1");
            }
        }
        private static void Wind(object context,object combat,object host,object attack,object input)
        {
            Main.windPhysics=true;Main.windSpeedCurrent=-1;Main.windPhysicsStrength=.1f;
            foreach(int scene in new[]{0,1,2})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.ResetEffects();Main.screenPosition=new Vector2(600,500);p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;Main.worldSurface=scene==2?20:100;
                if(scene==1)for(int x=40;x<=73;x++)for(int y=30;y<=42;y++)Main.tile[x,y].wall=1;
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(920,646);n.life=n.lifeMax=10000;n.target=0;n.netOffset=Vector2.Zero;n.velocity=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);
                NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0);var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");Require(plan!=null,"uniform native wind gate has a useful continuation");
                typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var q=Main.projectile.Single(s=>s.active && s.owner==0);float vx=q.velocity.X;int first=0;
                for(int tick=1;tick<=120 && q.active;tick++){NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(2);q.Update(q.whoAmI);if(tick==1)Require(Math.Abs(q.velocity.X-vx-(scene==0?-.2f:0))<.0001f,"native wind uses each AI subupdate and only the above-surface wall-free gate");if(n.life<10000){first=tick;break;}}
                Console.WriteLine("ENV wind: scene="+scene+" expected="+plan.Tick+" first="+first+" velocity="+q.velocity);Require(first>0 && first==plan.Tick,"native uniform wind first Damage matches planned time");
                if(scene==0){NativeCombatAimChecks.Prepare(host,attack,n,0);Main.windSpeedCurrent=-.5f;Require(GetOptional(attack,"ExpectedImpact")==null,"a changed wind snapshot immediately retires the old contact");Main.windSpeedCurrent=-1;}
            }
            // Isolate the actual vanilla immunity/speed/direction gate from
            // ammo and candidate choice; this does not call AI for prediction.
            Main.worldSurface=100;foreach(var n in Main.npc)n.active=false;var terrain=Get(attack,"terrain");var dry=attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostProjectileEnvironment").GetMethod("Dry",BindingFlags.Static|BindingFlags.NonPublic);
            foreach(var row in new[]{new[]{14,16,1},new[]{100,10,1},new[]{14,-20,1},new[]{14,10,0}})
            {
                foreach(var q in Main.projectile)q.active=false;var shot=Main.projectile[0];shot.SetDefaults(row[0]);shot.owner=0;shot.whoAmI=0;shot.active=true;shot.Center=new Vector2(710,660);shot.velocity=new Vector2(row[1],0);Main.tile[44,41].wall=0;Call(terrain,"Reset");bool allowed=(bool)dry.Invoke(null,new object[]{shot,terrain,shot.Center.X,shot.Center.Y,shot.velocity.X});float before=shot.velocity.X;shot.Update(0);bool unchanged=Math.Abs(shot.velocity.X-before)<.0001f;Require(allowed==(row[2]==1) && unchanged==allowed,"pure dry gate respects native type immunity, speed and wind direction");Console.WriteLine("ENV wind gate: type="+row[0]+" vx="+row[1]+" noImpulse="+allowed+" native="+shot.velocity.X);
            }
        }
    }
}
