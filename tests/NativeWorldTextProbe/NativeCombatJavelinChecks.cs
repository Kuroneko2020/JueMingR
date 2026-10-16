using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatJavelinChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static NPC target;private static Projectile receiving;private static bool natural;private static int births;private static long first;private static object ammo;private static Vector2 raw;
        private static object Static(Type type,string name,params object[] args){return type.GetMethod(name,Flags).Invoke(null,args);}
        private static Player Reset(object context)
        {
            natural=false;target=null;receiving=null;first=-1;births=0;NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3094,0,0);p.inventory[0].stack=999;Main.windPhysics=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();foreach(var n in Main.npc)n.active=false;return p;
        }
        private static object Terrain(){var t=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static void BeforeDamage(Projectile __instance){receiving=__instance;}private static void AfterDamage(){receiving=null;}
        private static void Strike(NPC __instance,int __result){if(natural && ReferenceEquals(__instance,target) && receiving!=null && receiving.type==507 && receiving.damage>0 && receiving.owner==Main.myPlayer && __result>0 && first<0)first=Main.GameUpdateCount;}
        private static void Born(IEntitySource __0,int __result)
        {
            if(!natural || __result<0)return;var q=Main.projectile[__result];if(q.type!=507)return;births++;var p=Main.LocalPlayer;var source=__0 as EntitySource_Parent;Require(!(source?.Entity is Projectile),"plain Javelin birth is the held consumable's actual hand stage");
            var origin=p.RotatedRelativePoint(p.MountedCenter);origin.Y=p.position.Y+p.height/3;var cursor=new Vector2(Main.mouseX,Main.mouseY)+Main.screenPosition;var expected=(cursor-origin).SafeNormalize(Vector2.UnitX)*(float)Get(ammo,"Speed");
            Console.WriteLine("JAVELIN born slot="+q.whoAmI+" center="+q.Center+" V="+q.velocity+" damage="+q.damage+" cursor="+cursor+" raw="+raw+" playerY="+p.position.Y+" height="+p.height);
            Require(q.owner==p.whoAmI && q.ranged && q.damage==(int)Get(ammo,"Damage") && q.penetrate==3 && q.maxPenetrate==3 && Vector2.DistanceSquared(q.Center,origin)<.0001f && Vector2.DistanceSquared(q.velocity,expected)<.0001f && Vector2.Dot(q.velocity,raw-origin)<0,"actual3094 birth consumes assisted cursor with native height/3 source and positive item damage");
        }
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostFiniteShotAttack",true);var audit=new Harmony("JueMingR.Tests.PlainJavelin");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatJavelinChecks),nameof(Born)));audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatJavelinChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatJavelinChecks),nameof(AfterDamage)));audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatJavelinChecks),nameof(Strike)));
            var componentHand=new NativeCombatHandFixture(context,()=>natural,()=>target);
            try{string only=Environment.GetEnvironmentVariable("JMR_JAVELIN_ORACLE_ONLY");if(string.IsNullOrEmpty(only) || only=="motor")Motors(context);if(string.IsNullOrEmpty(only) || only=="receive")Receives(context);if(string.IsNullOrEmpty(only) || only=="natural")Natural(context);Require(string.IsNullOrEmpty(only) || only=="motor" || only=="receive" || only=="natural","plain Javelin narrow leaf");}
            finally{componentHand.Dispose();natural=false;target=null;receiving=null;Main.windPhysics=wind;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
        private static void Motors(object context)
        {
            int cases=0,actions=0;var stateType=model.GetNestedType("State",BindingFlags.NonPublic);
            foreach(int scene in Enumerable.Range(0,10))
            {
                var p=Reset(context);if(scene>=1 && scene<=3)for(int x=40;x<115;x++)for(int y=10;y<70;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:3);}
                if(scene==4)for(int y=10;y<70;y++)NativeToolsChecks.Tile(60,y,1);if(scene==5){NativeToolsChecks.Tile(60,25,1);Main.tile[60,25].slope(1);}if(scene>=8)for(int x=40;x<115;x++)for(int y=10;y<70;y++){Main.tile[x,y].liquid=128;Main.tile[x,y].liquidType(3);}
                Vector2 center=scene==5?new Vector2(936,390):scene==6?new Vector2(10,350):new Vector2(950,350),v=scene==4?new Vector2(24,0):scene==5?new Vector2(24,8):scene==6?new Vector2(-7,0):scene>=8?new Vector2(6,scene==8?2:-2):new Vector2(6,-.3f);
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,v,507,0,0,p.whoAmI);var q=Main.projectile[slot];if(scene==7)q.timeLeft=1;var s=Activator.CreateInstance(stateType,Flags,null,new object[]{q,q.Center,q.velocity},null);var terrain=Terrain();Vector2 anchor;int cw,ch;q.GetCollisionParams(out anchor,out cw,out ch);Require(cw==4 && ch==4 && q.Damage_GetHitbox().Width==16 && q.Damage_GetHitbox().Height==16,"native plainJavelin separates its4px tilebody from16px damagebody");
                for(int outer=1;outer<=80 && q.active;outer++)
                {
                    var args=new object[]{q,terrain,s};Require((bool)Static(model,"Step",args),"plainJavelin finite scene remains readable");s=args[2];if((bool)Get(s,"Alive")){var tail=new object[]{s};Static(model,"Finish",tail);s=tail[0];}q.Update(slot);actions++;
                    Require(q.active==(bool)Get(s,"Alive") && q.timeLeft==(int)Get(s,"Left") && q.ai[0]==(float)Get(s,"Counter") && Vector2.DistanceSquared(q.position,(Vector2)Get(s,"Position"))<.002f && Vector2.DistanceSquared(q.velocity,(Vector2)Get(s,"Velocity"))<.0001f && q.wet==(bool)Get(s,"Wet") && q.honeyWet==(bool)Get(s,"Honey") && q.lavaWet==(bool)Get(s,"Lava") && q.shimmerWet==(bool)Get(s,"Shimmer"),"native Javelin full lifetime/terrain/liquid/clock agrees: "+scene+"/"+outer+" native="+q.position+"/"+q.velocity+"/"+q.timeLeft+"/"+q.ai[0]+" model="+Get(s,"Position")+"/"+Get(s,"Velocity")+"/"+Get(s,"Left")+"/"+Get(s,"Counter"));
                    if(scene==0 && outer==60)Require(q.velocity==new Vector2(6,-.3f),"plainJavelin does not start gravity at sixty");if(scene==0 && outer==61)Require(q.velocity.X==6*.98f && q.velocity.Y==0,"plainJavelin first damping and .3 gravity occur at action61");
                }
                if(scene==6)Require(!q.active && q.timeLeft>0,"worldretire doesnotcallKill");cases++;
            }
            Console.WriteLine("PASS plainJavelin motor="+cases+" actions="+actions);
        }
        private static NPC Target(int slot,Vector2 center,int width=50,int height=60)
        {
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.width=width;n.height=height;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);n.lifeRegen=n.lifeRegenCount=0;return n;
        }
        private static void Receives(object context)
        {
            int cases=0;foreach(int mode in new[]{0,1,2,3})
            {
                var p=Reset(context);target=Target(4,new Vector2(950,350));if(mode==1)target.immune[p.whoAmI]=100;
                int count=mode>=2?mode:0;for(int i=1;i<=count;i++)Target(i,new Vector2(950,350),100,100);
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,11.5f,0,507,50,0,p.whoAmI);var q=Main.projectile[slot];NativeQuickItemChecks.BeginWorldStep();var key=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",target,1L);var state=new NpcMotionState{Identity=key,X=target.position.X,Y=target.position.Y,Width=target.width,Height=target.height,CanReceive=true,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();var path=new NpcTrajectory(key,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
                var obstacle=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,true,false},null);var captured=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);var contact=(AttackContact)Static(model,"Solve",p,captured,path,q.Center,0,true,Terrain(),obstacle,0);natural=true;long sample=path.SampleTick;
                if(contact!=null)q.velocity=(new Vector2(contact.AimX,contact.AimY)-q.Center).SafeNormalize(Vector2.UnitX)*11.5f;
                for(int outer=1;outer<=120 && q.active && first<0;outer++){if(outer>1)NativeQuickItemChecks.BeginWorldStep();for(int i=1;i<=count;i++)Main.npc[i].UpdateNPC(i);target.UpdateNPC(4);q.Update(slot);}
                int actual=first<0?0:(int)(first-sample);Console.WriteLine("JAVELIN actual receiver mode="+mode+" first="+actual+" modeled="+contact?.Tick+" pen="+q.penetrate+" ownerImmune="+target.immune[p.whoAmI]);
                Require((mode==1 || mode==3?actual==0 && contact==null:actual==1 && contact?.Tick==1) && Enumerable.Range(1,count).All(i=>Main.npc[i].life<10000),"ordinary Javelin selected owner gate and actual lower-slot 3-hit exhaustion retain strict positive and negative contacts");natural=false;cases++;
            }
            Console.WriteLine("PASS plainJavelin receivers="+cases);
        }
        private static void Natural(object context)
        {
            var p=Reset(context);p.position=new Vector2(650,646);p.releaseUseItem=true;Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,43,40);raw=target.Center-new Vector2(390,0);var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,raw,false,true);Require(GetOptional(attack,"ExpectedPreview")==null && !Main.projectile.Any(q=>q.active) && p.HeldItem.noMelee,"withdrawn plainJavelin idle preview cannot invent useStyle1 melee or a throw");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),raw,true);Call(Get(context,"Combat"),"Sample");NativeCombatAimChecks.PrepareMechanismFixture(host,attack,target,0);var contact=(AttackContact)GetOptional(attack,"ExpectedImpact");Require(contact!=null,"actual pressed consumable Javelin prepares its real throw");ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);natural=true;long start=Main.GameUpdateCount;NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));NativeCombatSelectionPreviewChecks.Step(context,raw,true);Require((bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced","actual native Javelin consumes the prepared direction");for(int i=0;i<8 && first<0;i++)NativeCombatSelectionPreviewChecks.Step(context,raw,false);Console.WriteLine("JAVELIN natural births="+births+" first="+(first<0?0:first-start)+" preview="+contact.Tick);Require(births==1 && first>=start && first-start==contact.Tick,"actual consumable lightpress/release has a positive native Strike at the predicted throw contact on the genuine immortal dummy");natural=false;
        }
    }
}
