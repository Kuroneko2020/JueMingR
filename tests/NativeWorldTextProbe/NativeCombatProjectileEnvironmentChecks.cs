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
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");bool wind=Main.windPhysics,oldUp=Collision.up,oldDown=Collision.down,oldHoney=Collision.honey,oldShimmer=Collision.shimmer;float oldSpeed=Main.windSpeedCurrent,oldStrength=Main.windPhysicsStrength;double oldSurface=Main.worldSurface;Main.windPhysics=false;
            try
            {
                // Rows update the NPC as well as the projectile; buff arrays belong
                // to the reused NPC slot and are explicitly isolated below.
                foreach(var row in new[]{new[]{95,97,-1},new[]{95,97,0},new[]{95,97,2},new[]{1254,97,0}})
                {
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,row[0],0,0);p.position=new Vector2(700,646);p.ResetEffects();Main.screenPosition=new Vector2(600,500);p.inventory[54].SetDefaults(row[1]);p.inventory[54].stack=999;
                    if(row[2]>=0)for(int x=40;x<=73;x++)for(int y=30;y<=42;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(row[2]);}
                    // SetDefaults retains buff slots. Previous fire/poison weapons must not
                    // turn this liquid oracle into a DoT timer instead of first Damage.
                    var n=Main.npc[2];n.SetDefaults(3);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);n.whoAmI=2;n.active=true;n.position=new Vector2(920,646);n.life=n.lifeMax=10000;n.target=0;n.netOffset=Vector2.Zero;n.velocity=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);
                    NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));
                    Collision.up=true;Collision.down=true;Collision.honey=true;Collision.shimmer=true;NativeCombatAimChecks.PrepareBeforeNpc(host,attack,n,0);Require(Collision.up && Collision.down && Collision.honey && Collision.shimmer,"preparation preserves native collision query flags");
                    // This uniform deterministic motor has no intervening receiver or
                    // derived damage stage. Its geometric receive clock is first Damage.
                    var plan=NativeCombatAimChecks.RequireSolution(attack,p,"known uniform projectile environment prepares a reachable actual input");Require(!(bool)Get(attack,"hasConsumedInput"),"uniform-environment queries do not consume a native hand");typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});NativeCombatAimChecks.RequireConsumedInput(attack,p,plan,"uniform environment native Shoot consumes this Enhanced point");var q=Main.projectile.Single(s=>s.active && s.owner==0);var birth=q.Center;var velocity=q.velocity;int first=0;Vector2 firstDelta=Vector2.Zero;
                    for(int tick=1;tick<=120 && q.active;tick++){NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(2);Require(n.life==10000,"isolated receiver has no damage before projectile update");q.Update(q.whoAmI);if(tick==1){firstDelta=q.Center-birth;Require(Vector2.DistanceSquared(q.velocity,velocity)<.0001f,"liquid translates without multiplying stored bullet velocity");}if(n.life<10000){first=tick;break;}}
                    float scale=q.ignoreWater || row[2]<0?1:row[2]==2?.25f:.5f;Require(Vector2.DistanceSquared(firstDelta,velocity*(q.extraUpdates+1)*scale)<.001f,"native first movement uses the actual uniform liquid displacement");Console.WriteLine("ENV receive: projectile="+q.type+" liquid="+row[2]+" ignoreWater="+q.ignoreWater+" delta="+firstDelta+" velocity="+velocity+" expected="+plan.NativeTick+" first="+first);Require(first>0 && first==plan.NativeTick,"known uniform liquid first Damage matches the actual phase clock");
                }
                FireArrowWater(context,combat,host,attack,input);Boundaries(context,combat,host,attack,input);Wind(context,combat,host,attack,input);
            }
            finally{Main.windPhysics=wind;Main.windSpeedCurrent=oldSpeed;Main.windPhysicsStrength=oldStrength;Main.worldSurface=oldSurface;Collision.up=oldUp;Collision.down=oldDown;Collision.honey=oldHoney;Collision.shimmer=oldShimmer;NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
        private static void FireArrowWater(object context,object combat,object host,object attack,object input)
        {
            var assembly=attack.GetType().Assembly;var snapshots=assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true);var models=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels",true);const BindingFlags flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            foreach(int liquid in new[]{0,2})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,120,0,0);p.position=new Vector2(700,646);p.ResetEffects();Main.screenPosition=new Vector2(600,500);p.inventory[54].SetDefaults(40);p.inventory[54].stack=999;
                for(int x=40;x<=73;x++)for(int y=30;y<=42;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(liquid);}
                var n=Main.npc[2];n.SetDefaults(3);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);Array.Clear(n.immune,0,n.immune.Length);n.whoAmI=2;n.active=true;n.position=new Vector2(920,646);n.life=n.lifeMax=10000;n.defense=0;n.velocity=n.netOffset=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.knockBackResist=0;
                var ammo=snapshots.GetMethod("Capture",flags).Invoke(null,new object[]{p,p.HeldItem});var args=new object[]{ammo,default(AttackMotion)};Require((int)Get(ammo,"Projectile")==2 && (bool)models.GetMethod("TryRead",flags).Invoke(null,args),"real Molten Fury wooden ammo becomes a modeled FireArrow");var motion=((AttackMotion)args[1]).WithEnvironment(liquid==2?.25f:.5f,0);
                NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.PrepareBeforeNpc(host,attack,n,0);if(liquid==2)NativeCombatAimChecks.SolutionCheckpoint(context,attack,"environment-honey-fire-arrow");var plan=NativeCombatAimChecks.ReadSolution(attack);var available=NativeCombatAimChecks.QueryInput(attack,p);Require(plan.HasPoint && available.Allowed && available.Kind=="Enhanced" && available.Point==plan.Point && available.Target.Equals(plan.Timeline.Identity),"water/honey native witness consumes the actual public direction before its result gate");
                Require(!(bool)Get(attack,"hasConsumedInput"),"converted-arrow query does not consume a native hand");typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});NativeCombatAimChecks.RequireConsumedInput(attack,p,plan,"converted fire arrow consumes its prepared Enhanced point");var q=Main.projectile.Single(s=>s.active && s.owner==0);Require(q.type==2 && q.wet,"actual FireArrow birth captures native wet state");float xPos=q.Center.X,yPos=q.Center.Y,vx=q.velocity.X,vy=q.velocity.Y;int first=0;
                for(int outer=1;outer<=120 && q.active;outer++)
                {NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(n.whoAmI);Require(n.life==10000,"water-converted arrow oracle has no previous buff damage");for(int sub=0;sub<q.extraUpdates+1;sub++)motion.Advance(ref xPos,ref yPos,ref vx,ref vy,(outer-1)*motion.Updates+sub+1);q.Update(q.whoAmI);Require(q.type==1 && Vector2.DistanceSquared(q.Center,new Vector2(xPos,yPos))<.002f && Vector2.DistanceSquared(q.velocity,new Vector2(vx,vy))<.0001f,"native TransformType keeps FireArrow motor/body and changes only type: liquid="+liquid+" outer="+outer);if(n.life<10000){first=outer;break;}}
                Console.WriteLine("ENV FireArrow water conversion: liquid="+liquid+" born=2 converted="+q.type+" firstDamage="+first+" expected="+plan.NativeTick+" reach="+plan.Reachability+" final="+q.Center+" active="+q.active);
                Require(plan.Reachability=="Reachable" && first>0 && first==plan.NativeTick,"converted fire arrow native first Damage agrees: liquid="+liquid+" expected="+plan.NativeTick+" actual="+first);
            }
        }
        private static void Boundaries(object context,object combat,object host,object attack,object input)
        {
            foreach(var row in new[]{new[]{95,97,0,1},new[]{1254,97,3,0},new[]{218,0,0,0},new[]{113,0,0,0},new[]{113,0,3,0},new[]{495,0,3,0}})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,row[0],0,0);p.position=new Vector2(700,646);p.ResetEffects();p.channel=p.controlUseItem=true;Main.screenPosition=new Vector2(600,500);Main.leftWorld=Main.topWorld=0;Main.rightWorld=Main.maxTilesX*16;Main.bottomWorld=Main.maxTilesY*16;
                if(row[1]>0){p.inventory[54].SetDefaults(row[1]);p.inventory[54].stack=999;}
                for(int x=row[3]==1?50:40;x<=73;x++)for(int y=30;y<=42;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(row[2]);}
                // SetDefaults retains buff slots. Previous fire/poison weapons must not
                    // turn this liquid oracle into a DoT timer instead of first Damage.
                    var n=Main.npc[2];n.SetDefaults(3);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);n.whoAmI=2;n.active=true;n.position=new Vector2(920,646);n.life=n.lifeMax=10000;n.target=0;n.netOffset=Vector2.Zero;n.velocity=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);
                NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0,true);
                typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var q=Main.projectile.Single(s=>s.active && s.owner==0);NativeCombatAimChecks.Prepare(host,attack,n,0,true);
                // This is a born controlled source, whose dedicated Contact can
                // live in Control. Its selected receiver is still eligible, so
                // the retained getter cannot ClearExecution while observing it.
                Require((bool)Get(attack,"CurrentReceiver"),"born liquid boundary observes a current eligible receiver before reading its dedicated Contact");var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");bool regularWaterIgnore=row[0]==113 && row[2]==0;
                Require(regularWaterIgnore?plan!=null:plan==null,"only the modeled ignore-water controlled continuation publishes a contact in this boundary matrix");int first=0;
                for(int tick=1;tick<=45 && q.active;tick++){NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(2);Require(n.life==10000,"isolated receiver has no damage before projectile update");q.Update(q.whoAmI);if(n.life<10000){first=tick;break;}}
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
                // SetDefaults retains buff slots. Previous fire/poison weapons must not
                    // turn this liquid oracle into a DoT timer instead of first Damage.
                    var n=Main.npc[2];n.SetDefaults(3);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);n.whoAmI=2;n.active=true;n.position=new Vector2(920,646);n.life=n.lifeMax=10000;n.target=0;n.netOffset=Vector2.Zero;n.velocity=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);
                NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.PrepareBeforeNpc(host,attack,n,0);var plan=NativeCombatAimChecks.RequireSolution(attack,p,"uniform native wind gate has a useful continuation");
                if(scene==0){Main.windSpeedCurrent=-.5f;try{Require(NativeCombatAimChecks.QueryInput(attack,p).Kind!="Enhanced","changed wind withdraws the old representative input");}finally{Main.windSpeedCurrent=-1;}NativeCombatAimChecks.RequireSolution(attack,p,"restored wind admits only the original prepared point");}
                Require(!(bool)Get(attack,"hasConsumedInput"),"wind query does not consume a native hand");typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});NativeCombatAimChecks.RequireConsumedInput(attack,p,plan,"wind component consumes its current Enhanced point");var q=Main.projectile.Single(s=>s.active && s.owner==0);float vx=q.velocity.X;int first=0;
                for(int tick=1;tick<=120 && q.active;tick++){NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(2);Require(n.life==10000,"isolated receiver has no damage before projectile update");q.Update(q.whoAmI);if(tick==1)Require(Math.Abs(q.velocity.X-vx-(scene==0?-.2f:0))<.0001f,"native wind uses each AI subupdate and only the above-surface wall-free gate");if(n.life<10000){first=tick;break;}}
                Console.WriteLine("ENV wind: scene="+scene+" expected="+plan.NativeTick+" first="+first+" velocity="+q.velocity);Require(first>0 && first==plan.NativeTick,"native uniform wind first Damage matches the actual phase clock");
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
