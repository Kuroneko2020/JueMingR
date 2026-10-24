using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatPlacedPulseChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static NPC target;private static Projectile receiving;private static long first;private static int expectedType;
        private static void Damage(Projectile __instance){receiving=__instance;}
        private static void DamageDone(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {if(ReferenceEquals(target,__instance) && receiving!=null && receiving.type==expectedType && __result>0 && first<0)first=Main.GameUpdateCount;}
        private static Player Scene(object context,int item,float gravity)
        {
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),item,0,0);
            p.position=new Vector2(650,646);p.ResetEffects();p.gravDir=gravity;p.statMana=p.statManaMax=p.statManaMax2=1000;p.releaseUseItem=true;
            Main.screenPosition=new Vector2(500,400);Main.windPhysics=false;Main.tileSolid[1]=true;
            foreach(var n in Main.npc)n.active=false;
            for(int x=5;x<115;x++)for(int y=5;y<78;y++)Main.tile[x,y].ClearEverything();return p;
        }
        internal static void Run(object context)
        {RunCore(context,false);}
        internal static void RunReflection(object context)
        {RunCore(context,true);}
        private static void RunCore(object context,bool reflection)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var assembly=combat.GetType().Assembly;
            var model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostPlacedPulseAttack",true);var terrainType=assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);
            int rays=0,motors=0,actions=0,hands=0;
            foreach(float gravity in new[]{1f,-1f})foreach(bool wall in new[]{false,true})
            {
                var p=Scene(context,3006,gravity);if(wall)for(int y=5;y<78;y++)NativeToolsChecks.Tile(55,y,1);
                foreach(var point in new[]{new Vector2(1060.7f,660.3f),new Vector2(420.2f,515.8f),new Vector2(670.5f,880.3f),new Vector2(860.8f,330.4f)})
                {
                    var screen=point-Main.screenPosition;if(gravity<0)screen.Y=Main.screenHeight-screen.Y;Main.mouseX=(int)screen.X;Main.mouseY=(int)screen.Y;
                    var origin=p.RotatedRelativePoint(p.MountedCenter);p.direction=point.X<origin.X?-1:1;
                    var velocity=(Vector2)assembly.GetType("JueMingR.TerrariaHost.Combat.HostThornAttack",true).GetMethod("BirthVelocity",Flags,null,new[]{typeof(Player),typeof(Vector2),typeof(Vector2),typeof(float)},null).Invoke(null,new object[]{p,origin,point,10f});
                    var native=p.GetFarthestSpawnPositionOnLine(origin,velocity.X,velocity.Y);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");
                    var args=new object[]{p,origin,point,10f,terrain,Vector2.Zero};Require((bool)model.GetMethod("Birth",Flags).Invoke(null,args),"finite placement ray is modeled");
                    if(Vector2.DistanceSquared(native,(Vector2)args[5])>=.001f)
                    {
                        var at=origin;var owner=p.Center+new Vector2(p.direction*16,0);
                        for(int i=0;i<50;i++)
                        {
                            bool original=Collision.CanHitLine(owner,0,0,at,0,0);var query=new object[]{new MotionRect(owner.X,owner.Y,0,0),new MotionRect(at.X,at.Y,0,0),false,PredictionStop.None};
                            bool valid=(bool)terrainType.GetMethod("CanHitLine",Flags).Invoke(terrain,query);bool modeled=(bool)query[2];
                            if(!valid || original!=modeled){Console.WriteLine("PULSE LINE MISMATCH i="+i+" at="+at+" owner="+owner+" original="+original+" modeled="+modeled+" valid="+valid);break;}at+=velocity;
                        }
                    }
                    Require(Vector2.DistanceSquared(native,(Vector2)args[5])<.001f,"original integer cursor/normal and reverse gravity/blocked placement ray gravity="+gravity+" wall="+wall+" point="+point+" native="+native+" model="+args[5]+" origin="+origin);rays++;
                }
            }
            if(reflection){Console.WriteLine("PASS REFLECTION placed-pulse BirthVelocity original native placement rays="+rays);return;}
            foreach(int type in new[]{476,950})
            {
                var p=Scene(context,type==476?3006:5065,1);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(900,450),Vector2.Zero,type,0,0,p.whoAmI);var native=Main.projectile[slot];
                var copy=(Projectile)assembly.GetType("JueMingR.TerrariaHost.Combat.HostRaySpellAttack",true).GetMethod("Copy",Flags).Invoke(null,new object[]{native});
                for(int t=0;t<70 && native.active;t++)
                {
                    model.GetMethod("Ai",Flags).Invoke(null,new object[]{copy});if(copy.active && --copy.timeLeft<=0)copy.active=false;
                    NativeQuickItemChecks.BeginWorldStep();native.Update(slot);actions++;
                    Require(native.active==copy.active && (!native.active || native.position==copy.position && native.velocity==copy.velocity && native.timeLeft==copy.timeLeft && native.ai[0]==copy.ai[0] && Math.Abs(native.scale-copy.scale)<.0001f && Math.Abs(native.localAI[0]-copy.localAI[0])<.0001f),"original finite stationary AI/lifetime/body scale remains exact");
                }
                motors++;
            }
            var audit=new Harmony("JueMingR.Tests.PlacedPulse");
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPlacedPulseChecks),nameof(Damage)),postfix:new HarmonyMethod(typeof(NativeCombatPlacedPulseChecks),nameof(DamageDone)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatPlacedPulseChecks),nameof(Strike)));
            try
            {
                foreach(int item in new[]{3006,5065})foreach(float gravity in new[]{1f,-1f})foreach(bool wall in new[]{false,true})
                {
                    var p=Scene(context,item,gravity);target=Main.npc[2];target.SetDefaults(3);target.whoAmI=2;target.active=true;target.Center=new Vector2(1060,660);target.aiStyle=-1;target.noGravity=target.noTileCollide=true;target.life=target.lifeMax=10000;target.defense=0;target.friendly=target.immortal=target.dontTakeDamage=false;Array.Clear(target.immune,0,target.immune.Length);
                    if(wall)for(int y=0;y<Main.maxTilesY;y++)NativeToolsChecks.Tile(55,y,1);
                    expectedType=p.HeldItem.shoot;first=-1;
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,50,false,true));
                    NativeToolExecutionChecks.Sample(context,Get(context,"Input"),target.Center-new Vector2(600,0),true);Call(combat,"Sample");
                    NativeCombatAimChecks.PrepareMechanismFixture(host,Get(combat,"Attack"),target,0);var contact=(AttackContact)GetOptional(Get(combat,"Attack"),"ExpectedImpact");
                    Require(wall?contact==null:contact!=null,"finite placed-body contact uses the native clipped birth position");
                    long start=Main.GameUpdateCount;int mx=Main.mouseX,my=Main.mouseY;
                    typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});
                    Require(Main.mouseX==mx && Main.mouseY==my,"real placement restores physical cursor");var shots=Main.projectile.Where(q=>q.active && q.owner==p.whoAmI && q.type==expectedType).ToArray();Require(shots.Length==1,"one actual stationary native body");
                    if(!wall){var attack=Get(combat,"Attack");Require((bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced","actual native placement consumes the prepared point");}
                    for(int step=1;step<=70 && first<0;step++){NativeQuickItemChecks.BeginWorldStep();foreach(var q in Main.projectile)if(q.active)q.Update(q.whoAmI);}
                    Console.WriteLine("PLACED-PULSE item="+item+" gravity="+gravity+" wall="+wall+" native="+(first<0?-1:first-start)+" contact="+contact?.Tick);
                    Require(wall?first<0:first-start==contact.Tick,"actual first Damage agrees with finite placement receive");hands++;
                }
            }
            finally{audit.UnpatchAll(audit.Id);target=null;receiving=null;}
            Console.WriteLine("PASS PLACED-PULSE original rays="+rays+" motors="+motors+" actions="+actions+" actual hands="+hands);
        }
    }
}
