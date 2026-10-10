using System;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatSpecialArrowChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var assembly=combat.GetType().Assembly;var model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostSpecialArrowAttack",true);var stateType=model.GetNestedType("State",BindingFlags.NonPublic);var terrainType=assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);bool wind=Main.windPhysics;var random=Main.rand;int motors=0,steps=0,births=0,hits=0;
            try
            {
                foreach(int type in new[]{469,485,710})foreach(int scene in Enumerable.Range(0,8))
                {
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),39,0,0);p.ResetEffects();Main.windPhysics=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();
                    if(scene>=1 && scene<=3)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:1);}
                    if(scene>=4){for(int x=40;x<=80;x++)foreach(int y in new[]{15,45}){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}for(int y=15;y<=45;y++)foreach(int x in new[]{40,80}){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}Main.tile[60,25].active(true);Main.tile[60,25].type=1;Main.tile[60,25].slope((byte)(scene-3));}
                    var origin=scene>=4?new Vector2(936,390):new Vector2(950,350);var velocity=scene>=4?new Vector2(15,8):new Vector2(6,-.3f);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),origin,velocity,type,0,0,p.whoAmI,4,1);var q=Main.projectile[slot];var state=Activator.CreateInstance(stateType,Flags,null,new object[]{q},null);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");
                    for(int outer=1;outer<=35 && q.active;outer++)
                    {int updates=q.extraUpdates+1;for(int sub=0;sub<updates && (bool)Get(state,"Alive");sub++){var args=new object[]{q,terrain,state};Require((bool)model.GetMethod("Step",Flags).Invoke(null,args),"special arrow full native terrain/liquid stage is known: "+type+"/"+scene);state=args[2];}q.Update(slot);Require(q.active==((bool)Get(state,"Alive") && (int)Get(state,"Left")>0) && (!q.active || Vector2.DistanceSquared(q.Center,(Vector2)Get(state,"Center"))<.002f && Vector2.DistanceSquared(q.velocity,(Vector2)Get(state,"Velocity"))<.0001f && q.ai[0]==(float)Get(state,"Counter") && q.ai[1]==(float)Get(state,"Second")),"special arrow native motor/liquid/slope agrees: "+type+"/"+scene+"/"+outer+" native="+q.Center+"/"+q.velocity+" ai="+q.ai[0]+","+q.ai[1]+" model="+Get(state,"Center")+"/"+Get(state,"Velocity")+" ai="+Get(state,"Counter")+","+Get(state,"Second"));steps+=updates;}
                    if(type==469 && (scene==1 || scene==3))Require(!q.active && Main.projectile.Any(a=>a.active && a.owner==p.whoAmI && a.type!=469),"native BeeArrow water/lava AI exit really emits its separate bee role");motors++;
                }
                foreach(float counter in new[]{-1f,0f,19f,20f})foreach(float ySpeed in new[]{-.3f,.1f})
                {
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),39,0,0);p.ResetEffects();Main.windPhysics=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,6,ySpeed,710,0,0,p.whoAmI,counter);var q=Main.projectile[slot];var state=Activator.CreateInstance(stateType,Flags,null,new object[]{q},null);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");
                    for(int outer=1;outer<=20;outer++){for(int sub=0;sub<q.extraUpdates+1;sub++){var args=new object[]{q,terrain,state};Require((bool)model.GetMethod("Step",Flags).Invoke(null,args),"PhantomPhoenix piecewise motor has known finite terrain");state=args[2];}q.Update(slot);Require(q.active && Vector2.DistanceSquared(q.Center,(Vector2)Get(state,"Center"))<.002f && Vector2.DistanceSquared(q.velocity,(Vector2)Get(state,"Velocity"))<.0001f && q.ai[0]==(float)Get(state,"Counter"),"710 actual negative clock/positive-Y-only drag agrees: "+counter+"/"+ySpeed+"/"+outer);steps+=q.extraUpdates+1;}motors++;
                }
                foreach(int type in new[]{469,485})foreach(float counter in new[]{14.9f,15f,30f})
                {
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),39,0,0);p.ResetEffects();Main.windPhysics=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,6,-.3f,type,0,0,p.whoAmI,counter,1);var q=Main.projectile[slot];var state=Activator.CreateInstance(stateType,Flags,null,new object[]{q},null);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");
                    for(int outer=1;outer<=20;outer++){var args=new object[]{q,terrain,state};Require((bool)model.GetMethod("Step",Flags).Invoke(null,args),"captured launch-clock threshold has known terrain");state=args[2];q.Update(slot);Require(q.active && Vector2.DistanceSquared(q.Center,(Vector2)Get(state,"Center"))<.002f && Vector2.DistanceSquared(q.velocity,(Vector2)Get(state,"Velocity"))<.0001f && q.ai[0]==(float)Get(state,"Counter") && q.ai[1]==(float)Get(state,"Second"),"469/485 nonadvancing clock still has native >=15 clamp/gravity before pull: "+type+"/"+counter+"/"+outer);steps++;}motors++;
                }
                // A captured wet=true 710 has a random Kill branch before its
                // motor. Its ignoreWater default does not erase that fact.
                {var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),39,0,0);p.ResetEffects();var q=new Projectile();q.SetDefaults(710);q.Center=new Vector2(950,350);q.velocity=Vector2.UnitX*6;q.wet=true;var state=Activator.CreateInstance(stateType,Flags,null,new object[]{q},null);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var args=new object[]{q,terrain,state};Require(!(bool)model.GetMethod("Step",Flags).Invoke(null,args),"captured wet 710 retires unresolved random survival instead of hiding it with ignoreWater");}
                foreach(int weapon in new[]{2888,3019,3859})for(int seed=0;seed<(weapon==3019?16:1);seed++)
                {
                    var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,weapon,0,0);p.ResetEffects();p.position=new Vector2(700,646);p.inventory[54].SetDefaults(40);p.inventory[54].stack=999;Main.screenPosition=new Vector2(600,500);Main.windPhysics=false;Main.rand=new Terraria.Utilities.UnifiedRandom(7123+seed);
                    var n=Target();var timeline=Timeline(assembly,n);NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0);var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");Require(plan!=null && plan.Confidence==(weapon==2888?AttackConfidence.Conditional:AttackConfidence.Representative),"actual special arrow input has its primary-stage confidence: "+weapon+" contact="+plan?.Tick+" confidence="+plan?.Confidence+" failed="+Get(attack,"Failed"));
                    typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var shots=Main.projectile.Where(a=>a.active && a.owner==0).ToArray();Require(shots.Length==(weapon==3859?6:1),"actual special hand stage preserves its native birth count");var q=shots[0];int expected=weapon==2888?469:weapon==3019?485:710;Require(q.type==expected,"actual weapon/ammo produces its native special arrow");if(weapon==3019)Require(new Vector2(q.ai[0],q.ai[1]).Length()>0,"real Hellwing birth captures its independent AI launch vector");
                    var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");
                    if(weapon==3859)
                    {
                        var keys=shots.Select(s=>s.key).ToArray();var family=(AttackContact)model.GetMethod("ReplayFamily",Flags).Invoke(null,new object[]{p,shots,terrain,timeline,new Vector2(plan.AimX,plan.AimY),0,true,false});int firstFamily=0;bool primaryDamage=false;
                        for(int outer=1;outer<=120 && firstFamily==0;outer++)
                        {NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(n.whoAmI);for(int slot=0;slot<1000;slot++){var member=Main.projectile[slot];if(!member.active || member.owner!=p.whoAmI)continue;var key=member.key;int life=n.life;member.Update(slot);if(n.life<life){firstFamily=outer;primaryDamage=keys.Contains(key);break;}}}
                        Require(firstFamily>0 && family!=null && firstFamily==family.Tick && firstFamily==plan.Tick,"actual six-primary Phoenix hand stage has useful native Damage matching born and unborn family: native="+firstFamily+" born="+family?.Tick+" unborn="+plan.Tick+" primary="+primaryDamage);
                        hits++;births++;Console.WriteLine("SPECIAL ARROW six-primary Phoenix native="+firstFamily+" born="+family.Tick+" unborn="+plan.Tick+" firstPrimary="+primaryDamage+" initialKeys="+string.Join(",",keys.Select(k=>k.ToString())));continue;
                    }
                    var exact=(AttackContact)model.GetMethod("Replay",Flags).Invoke(null,new object[]{q,terrain,timeline,new Vector2(plan.AimX,plan.AimY),0,false});var state=Activator.CreateInstance(stateType,Flags,null,new object[]{q},null);int first=0;
                    for(int outer=1;outer<=120 && q.active;outer++)
                    {for(int sub=0;sub<q.extraUpdates+1 && (bool)Get(state,"Alive");sub++){var args=new object[]{q,terrain,state};Require((bool)model.GetMethod("Step",Flags).Invoke(null,args),"actual born arrow continuation has complete finite dependencies");state=args[2];}NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(n.whoAmI);q.Update(q.whoAmI);if(n.life<10000){first=outer;break;}Require(q.active==((bool)Get(state,"Alive") && (int)Get(state,"Left")>0) && (!q.active || Vector2.DistanceSquared(q.Center,(Vector2)Get(state,"Center"))<.002f && Vector2.DistanceSquared(q.velocity,(Vector2)Get(state,"Velocity"))<.0001f && q.ai[0]==(float)Get(state,"Counter") && q.ai[1]==(float)Get(state,"Second")),"real special birth motor and preserved ai1 agree: "+weapon+"/"+seed+"/"+outer);steps+=q.extraUpdates+1;}
                    Require(first==(exact==null?0:exact.Tick),"actual known special birth first Damage agrees exactly: "+weapon+"/"+seed+" native="+first+" known="+exact?.Tick);if(weapon==2888)Require(first==plan.Tick,"deterministic special arrow primary Damage matches unborn plan");if(first>0)hits++;births++;Console.WriteLine("SPECIAL ARROW weapon="+weapon+" seed="+seed+" native="+first+" bornContact="+exact?.Tick+" unborn="+plan.Tick+" ai="+q.ai[0]+","+q.ai[1]);
                }
                PhoenixDerived(context,assembly,model,terrainType);
            }
            finally{Main.windPhysics=wind;Main.rand=random;NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());}
            Require(hits>2,"native Hellwing random distribution retains useful births and misses in its denominator");Console.WriteLine("PASS special AI001 arrows native motor="+motors+" cases/"+steps+" steps; actual born contacts="+births+"; useful births="+hits+"/"+births);
        }
        private static void PhoenixDerived(object context,Assembly assembly,Type model,Type terrainType)
        {
            int childHits=0,burstHits=0;
            foreach(int scene in Enumerable.Range(0,5))
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3859,0,0);p.ResetEffects();Main.windPhysics=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();foreach(var npc in Main.npc)npc.active=false;
                bool wall=scene==2 || scene==4,immune=scene==1;var selected=Target();selected.position=wall?new Vector2(1042,390):new Vector2(970,410);
                NPC front=null;if(!wall){front=Main.npc[1];front.SetDefaults(3);front.whoAmI=1;front.active=true;front.position=new Vector2(982,340);front.life=front.lifeMax=10000;front.defense=0;front.aiStyle=-1;front.noGravity=front.noTileCollide=true;front.velocity=front.netOffset=Vector2.Zero;front.knockBackResist=0;Array.Clear(front.immune,0,front.immune.Length);if(immune)front.immune[p.whoAmI]=100;}
                if(wall)for(int y=15;y<=45;y++){Main.tile[65,y].active(true);Main.tile[65,y].type=1;}
                // Birth the parent above seven occupied slots, then free the
                // first five before capture. Its actual children reuse those
                // lower slots and wait until the next native outer loop.
                if(scene>=3)for(int i=0;i<7;i++){var blocker=Main.projectile[i];blocker.SetDefaults(1);blocker.whoAmI=i;blocker.active=true;blocker.owner=254;blocker.tileCollide=false;blocker.damage=0;blocker.Center=new Vector2(800,200);}
                Vector2 origin=wall?new Vector2(950,410):new Vector2(950,350),velocity=wall?new Vector2(10,0):new Vector2(6,0);
                int parentSlot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),origin,velocity,710,20,0,p.whoAmI);var parent=Main.projectile[parentSlot];var parentKey=parent.key;if(scene>=3)for(int i=0;i<5;i++)Main.projectile[i].active=false;var timeline=Timeline(assembly,selected);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");
                var expected=(AttackContact)model.GetMethod("ReplayFamily",Flags).Invoke(null,new object[]{p,new[]{parent},terrain,timeline,new Vector2(1100,350),0,true,false});int first=0,frontReceives=0,children=0;bool damageFromChild=false;
                for(int outer=1;outer<=120;outer++)
                {
                    NativeQuickItemChecks.BeginWorldStep();if(front!=null)front.UpdateNPC(front.whoAmI);selected.UpdateNPC(selected.whoAmI);
                    for(int slot=0;slot<1000;slot++)
                    {
                        var shot=Main.projectile[slot];if(!shot.active || shot.owner!=p.whoAmI)continue;var key=shot.key;int life=selected.life,frontLife=front?.life??0;shot.Update(slot);
                        if(front!=null && front.life<frontLife){frontReceives++;foreach(var child in Main.projectile.Where(a=>a.active && a.owner==p.whoAmI && a.type==710 && !a.key.Equals(parentKey))){Require(child.ai[1]==-1 && child.localNPCImmunity[front.whoAmI]==-1,"native Betsy child copies the actual front receiver's -1 local immunity before parent penetration/Kill");children++;}}
                        if(selected.life<life && first==0){first=outer;damageFromChild=!key.Equals(parentKey);}
                    }
                    if(first>0 || outer>=90 && !Main.projectile.Any(a=>a.active && a.owner==p.whoAmI))break;
                }
                Require(first==(expected==null?0:expected.Tick),"Phoenix real parent/child/Kill first Damage agrees: scene="+scene+" native="+first+" model="+expected?.Tick+" child="+damageFromChild+" front="+frontReceives);
                if(immune)Require(first==0 && frontReceives==0 && children==0,"owner-immune front is no OnHit event and cannot create useful child contact");
                else if(wall){Require(first>0 && !damageFromChild && frontReceives==0,"native wall Kill 40x40 Damage is useful without an invented body-through-wall contact");burstHits++;}
                else{Require(first>0 && damageFromChild && frontReceives==1 && children==5,"native front OnHit emits exactly five useful copied-local children, suppressing repeat parent/burst/sibling receiver hits");childHits++;}
                Console.WriteLine("PHOENIX DERIVED scene="+scene+" parentSlot="+parentSlot+" native="+first+" model="+expected?.Tick+" firstChild="+damageFromChild+" frontReceives="+frontReceives+" children="+children);
            }
            Require(childHits==2 && burstHits==2,"derived oracle retains both native child slot orders, useful Kill role and immune negative");
        }
        private static NPC Target()
        {var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(1100,646);n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);return n;}
        private static NpcTrajectory Timeline(Assembly assembly,NPC n)
        {var selection=assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true);var key=(NpcIdentity)selection.GetMethod("Identity",Flags).Invoke(null,new object[]{n,1L});var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true,NoTileCollide=true};var points=Enumerable.Range(0,181).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(key,0,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);}
    }
}
