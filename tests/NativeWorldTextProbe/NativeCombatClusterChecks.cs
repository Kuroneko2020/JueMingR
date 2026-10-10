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
    internal static class NativeCombatClusterChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(object context)
        {
            var assembly=Get(context,"Combat").GetType().Assembly;
            var model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostClusterAttack",true);
            var terrainType=assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);
            bool wind=Main.windPhysics;var random=Main.rand;int cases=0,hits=0,misses=0,lower=0;
            try
            {
                foreach(int parentType in new[]{776,777,778,803})foreach(int scene in Enumerable.Range(0,6))
                {
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),759,0,0);
                    p.ResetEffects();Main.windPhysics=false;Main.rand=new Terraria.Utilities.UnifiedRandom(61826+scene);
                    for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();foreach(var n in Main.npc)n.active=false;
                    // The original Kill owns the six random births. Testing
                    // their captured continuation neither replaces that RNG
                    // nor claims an unborn representative is the random draw.
                    var target=Target(scene==2?new Vector2(1120,600):new Vector2(1032,632));
                    if(scene==1)target.immune[p.whoAmI]=100;
                    if(scene==2)for(int y=20;y<70;y++){Main.tile[65,y].active(true);Main.tile[65,y].type=1;}
                    if(scene>=3)for(int i=0;i<7;i++){var blocker=Main.projectile[i];blocker.SetDefaults(1);blocker.active=true;blocker.whoAmI=i;blocker.owner=254;blocker.damage=0;blocker.tileCollide=false;blocker.Center=new Vector2(700,250);}
                    int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,550,0,0,parentType,80,0,p.whoAmI);
                    var parent=Main.projectile[slot];if(scene>=3)for(int i=0;i<5;i++)Main.projectile[i].active=false;
                    parent.Kill();var children=Main.projectile.Where(q=>q.active && q.owner==p.whoAmI).ToArray();
                    Require(!parent.active && children.Length==6 && children.All(q=>q.type==(parentType==803?862:779) && q.damage==40 && q.timeLeft>=31 && q.timeLeft<=60),"actual cluster Kill births six proper finite fragment roles: "+parentType+"/"+scene);
                    Require(target.life==10000,"target outside original parent explosion; child test does not inherit an uncounted parent hit");
                    if(scene>=3){Require(children.Count(q=>q.whoAmI<slot)==5,"native children allocate before dying parent slot is released");lower++;}
                    foreach(var child in children)Require((child.velocity+Vector2.UnitY).Length()>=3.9999f && (child.velocity+Vector2.UnitY).Length()<=6.0001f,"native fragment speed retains its original random interval");
                    var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var path=Timeline(assembly,target);
                    var expected=(AttackContact)model.GetMethod("ReplayFamily",Flags).Invoke(null,new object[]{p,children,path,terrain,new Vector2(1100,550),0,true});int first=0;
                    for(int outer=1;outer<=100;outer++)
                    {
                        NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(target.whoAmI);
                        for(int i=0;i<1000;i++){var shot=Main.projectile[i];if(!shot.active || shot.owner!=p.whoAmI)continue;int life=target.life;shot.Update(i);if(target.life<life && first==0)first=outer;}
                        if(first>0 || !Main.projectile.Any(q=>q.active && q.owner==p.whoAmI))break;
                    }
                    Require(first==(expected==null?0:expected.Tick),"captured complete cluster family agrees with actual first Damage: parent="+parentType+" scene="+scene+" native="+first+" model="+expected?.Tick);
                    if(scene==1 || scene==2)Require(first==0 && expected==null,"native owner immunity and blocking terrain cannot manufacture a child contact");
                    if(first>0)hits++;else misses++;cases++;
                    Console.WriteLine("CLUSTER captured parent="+parentType+" scene="+scene+" parentSlot="+slot+" childSlots="+string.Join(",",children.Select(q=>q.whoAmI))+" native="+first+" model="+expected?.Tick);
                }
                Require(hits>=4 && misses>=8 && lower==12,"cluster oracle retains useful actual children, negative scenes, and native allocation order");
                Console.WriteLine("PASS cluster captured families="+cases+" actual useful="+hits+" misses="+misses+" lower-slot="+lower);
            }
            finally{Main.windPhysics=wind;Main.rand=random;NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());}
        }
        private static NPC Target(Vector2 position)
        {
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=position;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;
            Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);return n;
        }
        private static NpcTrajectory Timeline(Assembly assembly,NPC target)
        {
            var type=assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true);var identity=(NpcIdentity)type.GetMethod("Identity",Flags).Invoke(null,new object[]{target,1L});
            var state=new NpcMotionState{Identity=identity,X=target.position.X,Y=target.position.Y,Width=target.width,Height=target.height,CanReceive=true,NoTileCollide=true};
            var points=Enumerable.Range(0,181).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(identity,0,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
    }
}
