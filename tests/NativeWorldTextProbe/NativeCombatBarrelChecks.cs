using System;
using System.Collections.Generic;
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
    internal static class NativeCombatBarrelChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static NPC target;private static Projectile receiving,motor;
        private static bool natural;private static int births,solves,steps;private static long first,birthTick;private static AttackContact known;private static object ammo;private static Vector2 raw;
        private static readonly List<int> hitSlots=new List<int>();
        private static Vector2 killCenter;private static int killCalls;
        private static object Static(Type t,string name,params object[] args){return t.GetMethod(name,Flags).Invoke(null,args);}
        private static void SolveWork(){solves++;}private static void StepWork(){steps++;}
        private static void BeforeKill(Projectile __instance){if(ReferenceEquals(__instance,motor)){killCalls++;killCenter=__instance.Center;}}
        private static void BeforeDamage(Projectile __instance){receiving=__instance;}private static void AfterDamage(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {
            if(receiving==null || receiving.type!=1023 || receiving.owner!=Main.myPlayer || receiving.damage<=0 || __result<=0)return;
            hitSlots.Add(__instance.whoAmI);if(ReferenceEquals(__instance,target) && first<0)first=Main.GameUpdateCount;
        }
        private static Player Reset(object context)
        {
            natural=false;target=null;receiving=motor=null;first=-1;births=killCalls=0;known=null;hitSlots.Clear();
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),5460,0,0);Main.windPhysics=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();if(Main.GameUpdateCount==0)NativeQuickItemChecks.BeginWorldStep();return p;
        }
        private static NPC Target(int slot,Vector2 center,int width=18,int height=40)
        {
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.width=width;n.height=height;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;
            Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);n.lifeRegen=n.lifeRegenCount=0;return n;
        }
        private static NpcTrajectory Timeline(NPC n)
        {
            var key=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=n.noTileCollide,ReflectsProjectiles=n.reflectsProjectiles};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();
            return new NpcTrajectory(key,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static object Terrain(){var t=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static object Obstacles(Player p,Projectile q,NpcTrajectory path){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,true,false},null);}
        private static object Clock(NpcTrajectory path){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc"),0},null);}
        private static void Born(IEntitySource __0,int __result)
        {
            if(!natural || __result<0)return;var q=Main.projectile[__result];if(q.type!=1023)return;births++;birthTick=Main.GameUpdateCount;
            var p=Main.LocalPlayer;var parent=__0 as EntitySource_Parent;Require(!(parent?.Entity is Projectile),"native barrel birth is the held5460 hand stage");var origin=p.RotatedRelativePoint(p.MountedCenter);var cursor=new Vector2(Main.mouseX,Main.mouseY)+Main.screenPosition;var expected=(cursor-origin).SafeNormalize(Vector2.UnitX)*(float)Get(ammo,"Speed");while(expected.X>=16 || expected.X<=-16 || expected.Y>=16 || expected.Y< -16)expected*=.97f;
            var path=Timeline(target);known=(AttackContact)Static(model,"Known",p,q,path,Clock(path),Terrain(),Obstacles(p,q,path));Console.WriteLine("BARREL born slot="+q.whoAmI+" center="+q.Center+" V="+q.velocity+" dmg="+q.damage+" pen/max="+q.penetrate+"/"+q.maxPenetrate+" local="+q.localNPCHitCooldown+" cursor="+cursor+" raw="+raw+" known="+known?.Tick);
            Require(q.owner==p.whoAmI && q.damage==(int)Get(ammo,"Damage") && q.penetrate==1 && q.maxPenetrate==1 && q.usesLocalNPCImmunity && q.localNPCHitCooldown==-1 && Vector2.DistanceSquared(q.Center,origin)<.0001f && Vector2.DistanceSquared(q.velocity,expected)<.0001f && Vector2.Dot(q.velocity,raw-origin)<0,"actual barrel consumes borrowed cursor and native mounted birth without RNG or new control");
        }
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostBarrelAttack",true);var audit=new Harmony("JueMingR.Tests.Barrel");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(model.GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBarrelChecks),nameof(SolveWork)));audit.Patch(model.GetMethod("Step",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBarrelChecks),nameof(StepWork)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatBarrelChecks),nameof(Born)));
            audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBarrelChecks),nameof(BeforeKill)));audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBarrelChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatBarrelChecks),nameof(AfterDamage)));audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatBarrelChecks),nameof(Strike)));
            try{string only=Environment.GetEnvironmentVariable("JMR_BARREL_ORACLE_ONLY");if(string.IsNullOrEmpty(only) || only=="motor")Motors(context);if(string.IsNullOrEmpty(only) || only=="receive")Receives(context);if(string.IsNullOrEmpty(only) || only=="natural")Natural(context);Require(string.IsNullOrEmpty(only) || only=="motor" || only=="receive" || only=="natural","native barrel narrow leaf");}
            finally{natural=false;target=null;receiving=motor=null;audit.UnpatchAll(audit.Id);Main.windPhysics=wind;NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
        private static void Compare(Projectile q,object state,int scene,int outer)
        {
            Require(q.active==(bool)Get(state,"Alive") && q.width==(int)Get(state,"Width") && q.height==(int)Get(state,"Height") && q.timeLeft==(int)Get(state,"Left") && q.alpha==(int)Get(state,"Alpha") && q.ai[0]==(float)Get(state,"Counter") && q.ai[1]==(float)Get(state,"Started") && q.localAI[1]==(float)Get(state,"Bounced") && q.rotation==(float)Get(state,"Rotation") && q.wet==(bool)Get(state,"Wet") && q.honeyWet==(bool)Get(state,"Honey") && q.lavaWet==(bool)Get(state,"Lava") && q.shimmerWet==(bool)Get(state,"Shimmer") && Vector2.DistanceSquared(q.position,(Vector2)Get(state,"Position"))<.002f && Vector2.DistanceSquared(q.velocity,(Vector2)Get(state,"Velocity"))<.0001f,"native barrel motor/lifetime/raw body agrees "+scene+"/"+outer+" actual="+q.position+"/"+q.velocity+"/"+q.timeLeft+"/"+q.width+"/"+q.rotation+" model="+Get(state,"Position")+"/"+Get(state,"Velocity")+"/"+Get(state,"Left")+"/"+Get(state,"Width")+"/"+Get(state,"Rotation"));
        }
        private static void Motors(object context)
        {
            int cases=0,actions=0,bounces=0;foreach(int scene in Enumerable.Range(0,12))
            {
                var p=Reset(context);if(scene>=1 && scene<=3)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:3);}if(scene==4 || scene==10)for(int y=10;y<60;y++)NativeToolsChecks.Tile(60,y,1);if(scene==5){NativeToolsChecks.Tile(60,25,1);Main.tile[60,25].slope(1);}if(scene==11)for(int x=40;x<115;x++)NativeToolsChecks.Tile(x,24,1);if(scene==8 || scene==9)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=128;Main.tile[x,y].liquidType(3);}
                var center=scene==5?new Vector2(936,390):scene==6?new Vector2(10,350):scene==11?new Vector2(950,373):new Vector2(950,350);var velocity=scene==4?new Vector2(24,0):scene==10?new Vector2(3,0):scene==11?new Vector2(0,3):scene==5?new Vector2(24,8):scene==6?new Vector2(-7,0):scene==8 || scene==9?new Vector2(6,scene==8?2:-2):new Vector2(6,-.3f);
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,velocity,1023,0,0,p.whoAmI);var q=Main.projectile[slot];if(scene==7)q.timeLeft=1;var state=Activator.CreateInstance(model.GetNestedType("State",BindingFlags.NonPublic),Flags,null,new object[]{q},null);var terrain=Terrain();motor=q;Vector2 anchor;int width,height;q.GetCollisionParams(out anchor,out width,out height);Require(width==14 && height==14 && q.width==26 && q.Damage_GetHitbox().Width==42,"native barrel separates14px tile/26px reflection/42px damage bodies");
                for(int outer=1;outer<=181 && q.active;outer++)
                {
                    Require((bool)Static(model,"Step",state,terrain),"barrel finite native terrain remains readable");if((bool)Get(state,"Alive"))Static(model,"Tail",state,q.penetrate);q.Update(slot);actions++;Compare(q,state,scene,outer);
                    if(q.localAI[1]>0)bounces++;if((bool)Get(state,"Burst"))Require(killCalls==1 && Vector2.DistanceSquared(killCenter,(Vector2)Get(state,"BurstCenter"))<.0001f && q.width==192 && q.Damage_GetHitbox().Width==208,"actual BeforeKill center is the208px explosion source, independent of any later dead displacement");
                    if(scene==0 && outer==19)Require(q.velocity.Y==-.3f && q.alpha==0,"barrel gravity remains zero before action20 despite visible alpha");if(scene==0 && outer==20)Require(q.ai[0]==30 && q.velocity.Y==-.3f+.25f,"barrel gravity starts at20 with native clock saturation30");
                }
                if(scene==6)Require(killCalls==0 && q.timeLeft>0,"barrel worldretire does not run Kill or explosion");if(scene==10)Require(q.localAI[1]>0,"low-speed axis collision actually runs the minimum-five reflection branch");cases++;
            }
            Require(bounces>0,"actual barrel reflection stages execute");Console.WriteLine("PASS BARREL motor="+cases+" actions="+actions+" bounceActions="+bounces);
        }
        private static void Receives(object context)
        {
            int cases=0;foreach(int mode in Enumerable.Range(0,10))
            {
                var p=Reset(context);int selected=mode==1?2:4;target=Target(selected,new Vector2(mode==6?70:mode==7?975:mode==8?966:mode==9?918:mode==5?1040:1060,350),mode==7 || mode==8?4:18,40);
                NPC front=null;if(mode<=3)front=Target(mode==1?5:1,new Vector2(950,350),20,20);if(mode==4)front=Target(1,new Vector2(920,350),18,20);if(mode==2)target.immune[p.whoAmI]=100;if(mode==7 || mode==8)target.reflectsProjectiles=true;
                var center=new Vector2(mode==6?10:950,350);var velocity=mode==4 || mode==5?Vector2.Zero:new Vector2(mode==6?-6:6,0);if(mode==9)for(int y=10;y<60;y++)NativeToolsChecks.Tile(60,y,1);
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,velocity,1023,50,0,p.whoAmI);var q=Main.projectile[slot];if(mode==3)q.localNPCImmunity[selected]=-1;if(mode==4 || mode==5)q.timeLeft=1;NativeQuickItemChecks.BeginWorldStep();var path=Timeline(target);var contact=(AttackContact)Static(model,"Known",p,q,path,Clock(path),Terrain(),Obstacles(p,q,path));long sample=path.SampleTick;motor=q;
                for(int outer=1;outer<=12 && q.active && first<0;outer++){if(outer>1)NativeQuickItemChecks.BeginWorldStep();if(front!=null)front.UpdateNPC(front.whoAmI);target.UpdateNPC(selected);q.Update(slot);}
                int actual=first<0?0:(int)(first-sample);Console.WriteLine("BARREL receive mode="+mode+" actual="+actual+" known="+contact?.Tick+" ordered="+string.Join(",",hitSlots)+" pen="+q.penetrate+" active="+q.active+" friendly="+q.friendly+" width="+q.width+" bounced="+q.localAI[1]);bool negative=mode==2 || mode==3 || mode==4 || mode==6 || mode==8;
                Require((actual==0)==negative && (contact==null)==negative && (contact==null || contact.Tick==actual),"barrel actual firstStrike equalsKnown through raw reflection, local/owner gates and real positive or exhausted explosion budget");
                if(mode<=3)Require(hitSlots.Count(i=>i==front.whoAmI)==1 && q.localNPCImmunity[front.whoAmI]==-1 && front.immune[p.whoAmI]==0,"same projectile body victim is excluded from its Kill explosion by native local-1, withowner immunitycleared");
                if(mode==0 || mode==1)Require(hitSlots.SequenceEqual(new[]{front.whoAmI,selected}) && actual==1 && q.penetrate==0,"zero-budget Kill restarts native slot iteration and actually hits another208px receiver in the sameouter");
                if(mode==4)Require(hitSlots.SequenceEqual(new[]{1}) && q.penetrate==0,"expiry positivepen1 explosion consumes its first receiver and cannot invent unlimited selected contact");if(mode==8)Require(killCalls==0 && q.active && !q.friendly,"actual raw-body reflection is not Kill/burst");
                if(mode==9)Require(q.localAI[1]==1 && (float)typeof(Projectile).GetMethod("Damage_GetProjectileSpecificDamageMultiplier",Flags).Invoke(q,null)==1.33f,"native bounce marker has the actual1.33 damage multiplier");cases++;
            }
            Console.WriteLine("PASS BARREL receive="+cases);
        }
        private static void Natural(object context)
        {
            var p=Reset(context);p.position=new Vector2(650,646);p.releaseUseItem=true;Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,43,40);raw=target.Center-new Vector2(390,0);var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");solves=steps=0;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,raw,false,true);var prepared=(AttackContact)GetOptional(attack,"ExpectedPreview");Require(prepared!=null && !Main.projectile.Any(q=>q.active),"real barrel idle next-hand preview without firing");ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);
            int priorSolves=solves,priorSteps=steps;var marker=Get(Get(host,"World"),"Impact");var zoom=Main.GameViewMatrix.ZoomMatrix;for(int i=0;i<20;i++){Call(marker,"Capture");Call(marker,"Project",zoom,Matrix.Invert(zoom));Require((bool)Call(attack,"PreviewCurrent",prepared),"barrel same-update presentation retains its lease");}Require(solves==priorSolves && steps==priorSteps && solves>0,"twenty Draws perform zero barrel Solve/motor work");
            natural=true;first=-1;births=0;NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));NativeCombatSelectionPreviewChecks.Step(context,raw,true);for(int i=0;i<8 && first<0;i++)NativeCombatSelectionPreviewChecks.Step(context,raw,false);Console.WriteLine("BARREL natural births="+births+" first="+(first<0?0:first-birthTick+1)+" known="+known?.Tick+" solves="+solves+" steps="+steps+" actualHitSlots="+string.Join(",",hitSlots));
            Require(births==1 && first>=birthTick && known!=null && first-birthTick+1==known.Tick,"actual barrel one-press/release has positive nativeStrike on genuine immortal488, equal to each actualborn Known");natural=false;
        }
    }
}
