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
    internal static class NativeCombatOnyxBlastChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static Projectile receiving;private static NPC target;
        private static int first,tick,strikes;private static Rectangle burst;private static bool burstSeen;
        private static object Static(Type type,string name,params object[] args){return type.GetMethod(name,Flags).Invoke(null,args);}
        private static void Damage(Projectile __instance){receiving=__instance;if(__instance.type==661 && __instance.width==160){burst=__instance.Hitbox;burstSeen=true;}}
        private static void DamageDone(){receiving=null;}
        private static void Strike(NPC __instance,int __result){if(__result>0 && ReferenceEquals(__instance,target) && receiving!=null){strikes++;if(first==0)first=tick;}}
        private static Player Reset(object context,int ammo=97)
        {
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3788,0,0);p.inventory[54].SetDefaults(ammo);p.inventory[54].stack=999;p.position=new Vector2(650,646);p.velocity=Vector2.Zero;p.itemAnimation=p.itemTime=0;p.releaseUseItem=true;p.direction=1;p.gravDir=1;Main.windPhysics=false;Main.ProjectileUpdateLoopIndex=-1;
            foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;for(int x=35;x<Main.maxTilesX-5;x++)for(int y=5;y<Main.maxTilesY-5;y++)Main.tile[x,y].ClearEverything();Main.tileSolid[1]=true;for(int x=5;x<Main.maxTilesX-5;x++)NativeToolsChecks.Tile(x,43,1);
            Array.Clear(Projectile.perIDStaticNPCImmunity,0,Projectile.perIDStaticNPCImmunity.Length);first=tick=strikes=0;target=null;burstSeen=false;return p;
        }
        private static NPC Target(Vector2 center,int side=30)
        {var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.width=n.height=side;n.Center=center;n.life=n.lifeMax=100000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;n.friendly=n.immortal=n.dontTakeDamage=false;Array.Clear(n.immune,0,n.immune.Length);return target=n;}
        private static object Terrain(){var t=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static NpcTrajectory Path(NPC n)
        {var id=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=id,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage && !n.friendly,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(id,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);}
        private static AttackContact Known(Player p,Projectile shot,NPC n,out object root)
        {var path=Path(n);var phase=Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc");var clock=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new[]{(object)path,phase},null);root=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,shot,path,0,true,false},null);using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))return (AttackContact)Static(model,"KnownFamily",p,new[]{shot},path,clock,Terrain(),root);}
        private static void Advance(int max=90)
        {for(tick=1;tick<=max && first==0;tick++){NativeQuickItemChecks.BeginWorldStep();if(target!=null && target.immune[0]>0)target.immune[0]--;for(int slot=0;slot<1000;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;try{Main.projectile[slot].Update(slot);}finally{Main.ProjectileUpdateLoopIndex=-1;}}}}
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostOnyxBlastAttack",true);var audit=new Harmony("JueMingR.Tests.OnyxBlast");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatOnyxBlastChecks),nameof(Damage)),finalizer:new HarmonyMethod(typeof(NativeCombatOnyxBlastChecks),nameof(DamageDone)));
            audit.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatOnyxBlastChecks),nameof(Strike)));
            try
            {
                int actions=0;foreach(int clock in new[]{0,14,15})foreach(var velocity in new[]{new Vector2(6,-1),new Vector2(-2,20),Vector2.Zero})
                {
                    var p=Reset(context);var q=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(850,400),velocity,661,0,0,p.whoAmI)];q.ai[0]=clock;
                    var copy=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostBetsyFlameAttack",true),"Copy",q);
                    for(int step=0;step<40;step++){q.AI();Require((int)Static(model,"Ai",copy)==0,"concrete661 motor admitted");Require(q.velocity==copy.velocity && q.ai.SequenceEqual(copy.ai) && q.alpha==copy.alpha && q.scale==copy.scale && q.rotation==copy.rotation,"native661 full AI state: "+clock+"/"+step);actions++;}
                }
                foreach(float scale in new[]{1f,1.3f})
                {
                    var p=Reset(context);var q=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(850.25f,400.75f),Vector2.Zero,661,60,0,0)];q.width=q.height=(int)(q.width*scale);var copy=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostBetsyFlameAttack",true),"Copy",q);q.Kill();Require(burstSeen && (bool)Static(model,"PrepareKillBurst",copy) && copy.Hitbox==burst,"native661 float Center recenter160 burst, including odd body");
                }
                int colliders=0;foreach(bool wall in new[]{false,true})
                {
                    var p=Reset(context);if(wall)for(int y=15;y<36;y++)NativeToolsChecks.Tile(56,y,1);
                    var q=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(850.25f,400.75f),Vector2.Zero,661,60,0,0)];Static(model,"PrepareKillBurst",q);var terrain=Terrain();
                    for(int x=-100;x<=100;x+=10)for(int y=-100;y<=100;y+=10)
                    {var box=new Rectangle(850+x,400+y,13,17);object[] args={q,new MotionRect(box.X,box.Y,box.Width,box.Height),terrain,false};bool hits=(bool)model.GetMethod("Collides",Flags).Invoke(null,args);Require((bool)args[3] && hits==q.Colliding(q.Hitbox,box),"native661 integer collider center/LOS/layout boundary "+wall+"/"+x+"/"+y);colliders++;}
                }
                foreach(bool blocked in new[]{false,true})
                {
                    var p=Reset(context);var n=Target(new Vector2(930,400));n.dontTakeDamage=blocked;var q=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(850,400),new Vector2(6,0),661,60,0,0)];q.timeLeft=1;object root;var contact=Known(p,q,n,out root);Advance();Console.WriteLine("ONYX sealed blocked="+blocked+" expected="+contact?.Tick+" native="+first+" burst="+burstSeen+" center="+q.Center+" box="+n.Hitbox+" ownerImmune="+n.immune[0]);Require(blocked?contact==null && first==0:contact!=null && contact.Tick==first && first==1,"sealed661 original expiration burst first receive and invulnerable rejection");
                }
                foreach(int ammo in new[]{97,515,1351})
                {
                    var p=Reset(context,ammo);var n=Target(p.Center+new Vector2(210,0),100);var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");Main.screenPosition=new Vector2(500,400);
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,true));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),n.Center-new Vector2(500,0),true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,0);
                    Require(GetOptional(attack,"ExpectedImpact")!=null && GetOptional(attack,"obstacles")!=null,"Onyx actual public full batch/root");var snapshot=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);int damage=(int)Get(snapshot,"Damage"),type=(int)Get(snapshot,"Projectile");
                    typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var born=Main.projectile.Where(q=>q.active).ToArray();Require(born.Length==5 && born.Count(q=>q.type==type && q.damage==damage)==4 && born.Single(q=>q.type==661).damage==damage*2,"native Onyx four selected bullets plus true doubled661");
                    Require((bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced","actual Onyx native Shoot consumes complete batch plan");Advance();Require(first>0 && strikes>0,"actual Onyx volley has native positive Strike");
                }
                Console.WriteLine("ONYX motor9/actions="+actions+" originalKill2 colliderGrid="+colliders+" observedReceive2 realSelectedAmmoHands3; future jitter remains Representative");
            }
            finally{audit.UnpatchAll(audit.Id);Main.windPhysics=wind;receiving=null;target=null;}
        }
    }
}
