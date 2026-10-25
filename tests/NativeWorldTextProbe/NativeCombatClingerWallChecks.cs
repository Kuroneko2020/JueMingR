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
    internal static class NativeCombatClingerWallChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static NPC target;private static Projectile receiving;private static long first;
        private static void Damage(Projectile __instance){receiving=__instance;}
        private static void DamageDone(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {if(ReferenceEquals(target,__instance) && receiving?.type==482 && __result>0 && first<0)first=Main.GameUpdateCount;}
        private static Player Scene(object context,float gravity,int floor)
        {
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3014,0,0);
            p.position=new Vector2(650,646);p.ResetEffects();p.gravDir=gravity;p.statMana=p.statManaMax=p.statManaMax2=1000;p.releaseUseItem=true;
            Main.screenPosition=new Vector2(500,400);Main.windPhysics=false;Main.tileSolid[1]=true;Main.tileSolid[19]=Main.tileSolidTop[19]=true;
            foreach(var n in Main.npc)n.active=false;
            for(int x=5;x<115;x++)for(int y=5;y<78;y++)Main.tile[x,y].ClearEverything();
            for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,10,1);
            if(floor!=0)for(int x=5;x<115;x++)
            {
                NativeToolsChecks.Tile(x,48,floor==1?1:19);
                if(floor==3)Main.tile[x,48].active(false); // Native checks the raw platform type here.
            }
            return p;
        }
        internal static void Run(object context)
        {RunCore(context,false);}
        internal static void RunReflection(object context)
        {RunCore(context,true);}
        private static void RunCore(object context,bool reflection)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var assembly=combat.GetType().Assembly;
            var model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostClingerWallAttack",true);
            var terrainType=assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);
            var shoot=typeof(Player).GetMethod("ItemCheck_Shoot",Flags);int rays=0,actions=0,hands=0;
            foreach(float gravity in new[]{1f,-1f})foreach(int floor in new[]{0,1,2,3})foreach(bool wall in new[]{false,true})
            {
                if(reflection)continue;
                var p=Scene(context,gravity,floor);if(wall)for(int y=10;y<77;y++)NativeToolsChecks.Tile(55,y,1);
                var raw=new Vector2(1060,660);Main.screenPosition.Y=660-Main.screenHeight/2;Main.mouseX=(int)(raw.X-Main.screenPosition.X);Main.mouseY=(int)(raw.Y-Main.screenPosition.Y);
                var origin=p.RotatedRelativePoint(p.MountedCenter);p.direction=1;
                var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");
                var args=new object[]{p,origin,raw,p.HeldItem.shootSpeed,terrain,null};Require((bool)model.GetMethod("Birth",Flags).Invoke(null,args),"Clinger finite placement acquired its terrain gravity="+gravity+" floor="+floor+" wall="+wall);
                shoot.Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});
                var native=Main.projectile.SingleOrDefault(q=>q.active && q.owner==p.whoAmI && q.type==482);var expected=(Projectile)args[5];
                Require((native==null)==(expected==null),"Clinger native floor requirement agrees gravity="+gravity+" floor="+floor+" wall="+wall+" native="+native?.position+"/"+native?.ai[0]+"/"+native?.ai[1]+" model="+expected?.position+"/"+expected?.ai[0]+"/"+expected?.ai[1]);
                if(native!=null)Require(native.position==expected.position && native.ai[0]==expected.ai[0] && native.ai[1]==expected.ai[1],"Clinger original raw cursor/height/X snap agrees native="+native.position+"/"+native.ai[0]+"/"+native.ai[1]+" model="+expected.position+"/"+expected.ai[0]+"/"+expected.ai[1]);rays++;
            }
            // This isolated 120-tile world has vanilla's 640px playable
            // border. Keep these rays inside that actual domain: y515 was
            // limited to y640 while its original initial velocity still went
            // upward, eventually leaving the tile array before a floor scan.
            // The failed raw515 observation is retained in m2h-Clinger.log.
            foreach(float gravity in new[]{1f,-1f})foreach(float y in new[]{645f,735f})
            {
                var p=Scene(context,gravity,1);var raw=new Vector2(1060,y);Main.mouseX=(int)(raw.X-Main.screenPosition.X);Main.mouseY=(int)(raw.Y-Main.screenPosition.Y);p.direction=1;
                var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var args=new object[]{p,p.RotatedRelativePoint(p.MountedCenter),Main.MouseWorld,p.HeldItem.shootSpeed,terrain,null};
                // The old reflection seam lived only in failed-ray diagnostics.
                // Observe it on the same normal rays too, using the native
                // integer MouseWorld direction before the original placement
                // oracle below. A passing placement cannot skip this Invoke.
                var origin=(Vector2)args[1];var logical=raw;if(gravity<0)logical.Y=Main.screenPosition.Y+Main.screenHeight-(raw.Y-Main.screenPosition.Y);
                var velocity=(Vector2)assembly.GetType("JueMingR.TerrariaHost.Combat.HostThornAttack",true).GetMethod("BirthVelocity",Flags,null,new[]{typeof(Player),typeof(Vector2),typeof(Vector2),typeof(float)},null).Invoke(null,new object[]{p,origin,logical,p.HeldItem.shootSpeed});
                var expectedDirection=Main.MouseWorld-origin;expectedDirection.Normalize();
                Require(Vector2.DistanceSquared(velocity,expectedDirection*p.HeldItem.shootSpeed)<.0001f,"Clinger reflected initial ray uses the native integer MouseWorld and original speed before placement.");
                bool modeled=(bool)model.GetMethod("Birth",Flags).Invoke(null,args);
                if(!modeled)
                {
                    var at=origin;var limited=raw;p.LimitPointToPlayerReachableArea(ref limited);
                    Console.WriteLine("CLINGER failed ray origin="+origin+" velocity="+velocity+" speed="+p.HeldItem.shootSpeed+" raw="+raw+" limited="+limited);
                    for(int i=0;i<300;i++)
                    {
                        var query=new object[]{new MotionRect(p.position.X,p.position.Y,p.width,p.height),new MotionRect(at.X,at.Y,1,1),false,PredictionStop.None};bool valid=(bool)terrainType.GetMethod("CanHitLine",Flags).Invoke(terrain,query);bool actual=Collision.CanHitLine(p.position,p.width,p.height,at,1,1);
                        if(!valid || !(bool)query[2]){Console.WriteLine("CLINGER ray stop i="+i+" at="+at+" valid="+valid+" predicted="+query[2]+" native="+actual+" stop="+query[3]);break;}at+=velocity;if(Vector2.Distance(at,limited)<20+Math.Abs(velocity.X)+Math.Abs(velocity.Y)){Console.WriteLine("CLINGER ray reached="+at);break;}
                    }
                }
                Require(modeled,"nonhorizontal Clinger raw/logical gravity ray has finite captured terrain gravity="+gravity+" y="+y+" screen="+Main.screenPosition+"/"+Main.screenHeight);shoot.Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});
                var native=Main.projectile.SingleOrDefault(q=>q.active && q.owner==p.whoAmI && q.type==482);var expected=(Projectile)args[5];
                Require((native==null)==(expected==null) && (native==null || native.position==expected.position && native.ai[0]==expected.ai[0] && native.ai[1]==expected.ai[1]),"Clinger raw placement differs from the separately reversed initial ray exactly as vanilla gravity="+gravity+" y="+y+" native="+native?.position+"/"+native?.ai[0]+"/"+native?.ai[1]+" model="+expected?.position+"/"+expected?.ai[0]+"/"+expected?.ai[1]);rays++;
            }
            if(reflection){Console.WriteLine("PASS REFLECTION clinger BirthVelocity actual native nonhorizontal placement rays="+rays);return;}
            foreach(bool olderFirst in new[]{false,true})
            {
                var p=Scene(context,1,1);var native=new Projectile[2];var copies=new Projectile[2];
                for(int i=0;i<2;i++)
                {
                    int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(900+i*24,660),Vector2.Zero,482,0,0,p.whoAmI,528,240);
                    native[i]=Main.projectile[slot];native[i].timeLeft=100+(olderFirst?i:1-i);
                    copies[i]=(Projectile)assembly.GetType("JueMingR.TerrariaHost.Combat.HostRaySpellAttack",true).GetMethod("Copy",Flags).Invoke(null,new object[]{native[i]});
                }
                for(int step=0;step<4;step++)
                {
                    NativeQuickItemChecks.BeginWorldStep();
                    for(int i=0;i<2;i++)
                    {
                        var copy=copies[i];if(copy.active){model.GetMethod("Ai",Flags).Invoke(null,new object[]{copy,copies,p.Center});if(copy.active){copy.position+=copy.velocity;if(--copy.timeLeft<=0)copy.active=false;}}
                        if(native[i].active)native[i].Update(native[i].whoAmI);actions++;
                        Require(native[i].active==copy.active && (!copy.active || native[i].position==copy.position && native[i].height==copy.height && native[i].timeLeft==copy.timeLeft),"Clinger old/new slot-order and native outer clock remain exact");
                    }
                }
            }
            var audit=new Harmony("JueMingR.Tests.ClingerWall");
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatClingerWallChecks),nameof(Damage)),postfix:new HarmonyMethod(typeof(NativeCombatClingerWallChecks),nameof(DamageDone)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatClingerWallChecks),nameof(Strike)));
            try
            {
                foreach(float gravity in new[]{1f,-1f})foreach(bool floor in new[]{false,true})foreach(bool wall in new[]{false,true})
                {
                    var p=Scene(context,gravity,floor?1:0);Main.screenPosition.Y=660-Main.screenHeight/2;
                    target=Main.npc[2];target.SetDefaults(3);target.whoAmI=2;target.active=true;target.Center=new Vector2(1060,660);target.aiStyle=-1;target.noGravity=target.noTileCollide=true;target.life=target.lifeMax=10000;target.defense=0;target.friendly=target.immortal=target.dontTakeDamage=false;Array.Clear(target.immune,0,target.immune.Length);
                    if(wall)for(int y=10;y<77;y++)NativeToolsChecks.Tile(55,y,1);first=-1;
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,50,false,true));
                    NativeToolExecutionChecks.Sample(context,Get(context,"Input"),target.Center-new Vector2(500,0),true);Call(combat,"Sample");
                    NativeCombatAimChecks.PrepareMechanismFixture(host,Get(combat,"Attack"),target,0);var contact=(AttackContact)GetOptional(Get(combat,"Attack"),"ExpectedImpact");
                    Require(floor && !wall?contact!=null:contact==null,"Clinger full variable body requires a reachable floor-backed placement");
                    long start=Main.GameUpdateCount;int mx=Main.mouseX,my=Main.mouseY;shoot.Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});
                    Require(Main.mouseX==mx && Main.mouseY==my,"Clinger native consumer restores physical cursor");
                    for(int step=1;step<=8 && first<0;step++){NativeQuickItemChecks.BeginWorldStep();foreach(var q in Main.projectile)if(q.active)q.Update(q.whoAmI);}
                    Require(floor && !wall?first-start==contact.Tick:first<0,"Clinger real first Damage preserves floor and wall negatives");hands++;
                }
            }
            finally{audit.UnpatchAll(audit.Id);target=null;receiving=null;}
            Console.WriteLine("PASS CLINGER-WALL original placement="+rays+" slot-actions="+actions+" actual hands="+hands);
        }
    }
}
