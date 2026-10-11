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
    internal static class NativeCombatFlareChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static readonly int[] Types={163,310,1008,1009,1010,1011},Ammos={931,1614,5377,5378,5379,5380};
        private static Assembly assembly;private static Type model;private static NPC target;private static Projectile receiving;
        private static bool natural;private static long first,birthTick;private static int births,solves,steps;private static AttackContact known;private static Vector2 raw;
        private static readonly List<int> hits=new List<int>();
        private static object Static(Type type,string method,params object[] args){return type.GetMethod(method,Flags).Invoke(null,args);}
        private static void SolveWork(){solves++;}private static void StepWork(){steps++;}
        private static void BeforeDamage(Projectile __instance){receiving=__instance;}private static void AfterDamage(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {if(receiving==null || !Types.Contains(receiving.type) || __result<=0)return;hits.Add(__instance.whoAmI);if(ReferenceEquals(__instance,target) && first<0)first=Main.GameUpdateCount;}
        private static Player Reset(object context,int ammo=931)
        {
            natural=false;target=null;receiving=null;first=-1;births=0;known=null;hits.Clear();
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),930,0,0);p.inventory[54].SetDefaults(ammo);p.inventory[54].stack=999;Main.windPhysics=false;
            for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();if(Main.GameUpdateCount==0)NativeQuickItemChecks.BeginWorldStep();return p;
        }
        private static NPC Target(int slot,Vector2 center)
        {
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.width=18;n.height=40;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;
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
            if(!natural || __result<0 || __result>=Main.maxProjectiles)return;var q=Main.projectile[__result];if(!Types.Contains(q.type))return;
            births++;if(births!=1)return;birthTick=Main.GameUpdateCount;var p=Main.LocalPlayer;var origin=p.RotatedRelativePoint(p.MountedCenter);var cursor=new Vector2(Main.mouseX,Main.mouseY)+Main.screenPosition;
            var ammo=p.inventory.First(a=>a!=null && a.stack>0 && a.ammo==931);var expected=(Vector2)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostThornAttack",true),"BirthVelocity",p,origin,cursor,p.HeldItem.shootSpeed+ammo.shootSpeed);
            Require(q.type==ammo.shoot && q.damage==p.GetWeaponDamage(p.HeldItem)+(int)(ammo.damage*p.GetWeaponDamageMultiplier(ammo)) && Vector2.DistanceSquared(q.Center,origin)<.0001f && Vector2.DistanceSquared(q.velocity,expected)<.0001f && Vector2.Dot(q.velocity,raw-origin)<0,"actual flare birth uses selected round, native ammo damage/speed and borrowed cursor opposite physical mouse");
            var path=Timeline(target);known=(AttackContact)Static(model,"Known",p,q,path,Clock(path),Terrain(),Obstacles(p,q,path));
            Console.WriteLine("FLARE birth type="+q.type+" center="+q.Center+" velocity="+q.velocity+" damage="+q.damage+" cursor="+cursor+" raw="+raw+" known="+known?.Tick);
        }
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostFlareAttack",true);var audit=new Harmony("JueMingR.Tests.Flare");bool wind=Main.windPhysics;
            // Direct Projectile.Update needs the same real visual helper that
            // the ordinary projectile phase owns. Restore the host instance.
            var priorSpelunker=Main.instance.SpelunkerProjectileHelper;Main.instance.SpelunkerProjectileHelper=new Terraria.GameContent.SpelunkerProjectileHelper();
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(model.GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatFlareChecks),nameof(SolveWork)));audit.Patch(model.GetMethod("Step",Flags),prefix:new HarmonyMethod(typeof(NativeCombatFlareChecks),nameof(StepWork)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatFlareChecks),nameof(Born)));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatFlareChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatFlareChecks),nameof(AfterDamage)));
            audit.Patch(typeof(NPC).GetMethod("StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatFlareChecks),nameof(Strike)));
            try{string leaf=Environment.GetEnvironmentVariable("JMR_FLARE_ORACLE_ONLY");Require(string.IsNullOrEmpty(leaf) || leaf=="motor" || leaf=="receive" || leaf=="natural","known flare leaf");if(string.IsNullOrEmpty(leaf) || leaf=="motor")Motors(context);if(string.IsNullOrEmpty(leaf) || leaf=="receive")Receives(context);if(string.IsNullOrEmpty(leaf) || leaf=="natural")foreach(int ammo in Ammos)Natural(context,ammo);}
            finally{natural=false;audit.UnpatchAll(audit.Id);Main.windPhysics=wind;Main.instance.SpelunkerProjectileHelper=priorSpelunker;}
        }
        private static void Motors(object context)
        {
            int actions=0,from=0;string selected=Environment.GetEnvironmentVariable("JMR_FLARE_MOTOR_FROM");Require(string.IsNullOrEmpty(selected) || int.TryParse(selected,out from) && from>=0 && from<16,"known flare motor suffix");
            // Six actual round defaults, then the shared dry/wet stick,
            // removal, gravity clock, Shimmer and retirement mechanisms.
            for(int scene=from;scene<16;scene++)
            {
                var p=Reset(context);int type=Types[scene%Types.Length];
                if(scene>=7 && scene<=11)for(int x=40;x<100;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==7?0:scene==8?2:scene==9?1:3);}
                if(scene==6 || scene==7 || scene==8)for(int y=10;y<60;y++)NativeToolsChecks.Tile(60,y,1);
                var center=scene==15?new Vector2(8,350):new Vector2(920,350);var velocity=new Vector2(scene==15?-6:6,scene==11?1:-.3f);
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,velocity,type,12,0,p.whoAmI);var q=Main.projectile[slot];
                if(scene==11){Main.tile[(int)(q.Center.X/16),(int)(q.position.Y/16)-1].liquid=0;}
                if(scene==12){q.localAI[1]=29;q.velocity.Y=15.98f;}if(scene==13){q.localAI[0]=1;q.velocity=Vector2.Zero;NativeToolsChecks.Tile(57,21,1);}if(scene==14)q.timeLeft=1;
                var copy=(Projectile)Static(model,"Copy",q);var terrain=Terrain();
                for(int outer=1;outer<=36 && q.active;outer++)
                {
                    if(scene==13 && outer==3){Main.tile[57,21].ClearEverything();Call(terrain,"Reset");}
                    Require((bool)Static(model,"Step",copy,terrain),"flare terrain lease remains complete");Static(model,"Finish",copy);Main.instance.SpelunkerProjectileHelper.OnPreUpdateAllProjectiles();q.Update(slot);actions++;
                    Console.WriteLine("FLARE motor scene="+scene+" action="+outer+" actual="+q.position+"/"+q.velocity+" damage="+q.damage+" left="+q.timeLeft+" local="+q.localAI[0]+","+q.localAI[1]+" model="+copy.position+"/"+copy.velocity+" damage="+copy.damage+" left="+copy.timeLeft+" local="+copy.localAI[0]+","+copy.localAI[1]);
                    Require(q.active==copy.active && q.timeLeft==copy.timeLeft && q.damage==copy.damage && q.ai.SequenceEqual(copy.ai) && q.localAI.SequenceEqual(copy.localAI) && q.wet==copy.wet && q.shimmerWet==copy.shimmerWet && (!q.active || Vector2.DistanceSquared(q.position,copy.position)<.002f && Vector2.DistanceSquared(q.velocity,copy.velocity)<.00001f),"native flare movement, stick/detach action, damage and retirement agree");
                }
                if(scene==6 || scene==7 || scene==8)Require(q.localAI[0]==1 && q.damage==0,"native tile contact has become a harmless stuck light");
                if(scene==13)Require(q.localAI[0]==0 && q.damage==0 && q.velocity.Y>0,"removing support resumes falling without restoring damage");
                if(scene==15)Require(q.timeLeft>0,"world boundary retires without Kill");
            }
            Console.WriteLine("PASS FLARE motor="+(16-from)+" from="+from+" actions="+actions);
        }
        private static void Receives(object context)
        {
            for(int mode=0;mode<12;mode++)
            {
                var p=Reset(context);target=Target(6,new Vector2(960,350));int type=Types[mode%Types.Length];
                if(mode==6)target.immune[p.whoAmI]=200;if(mode==7)target.reflectsProjectiles=true;if(mode==8)for(int i=0;i<5;i++)Target(i,target.Center);
                if(mode==9 || mode==10){for(int y=10;y<60;y++)NativeToolsChecks.Tile(60,y,1);}
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(mode==9 || mode==10?956:960,350),new Vector2(mode==9 || mode==10?6:0,0),type,40,0,p.whoAmI);var q=Main.projectile[slot];if(mode==10)q.localAI[0]=1;if(mode==11)q.timeLeft=1;
                NativeQuickItemChecks.BeginWorldStep();var path=Timeline(target);var contact=(AttackContact)Static(model,"Known",p,q,path,Clock(path),Terrain(),Obstacles(p,q,path));long sample=path.SampleTick;
                for(int outer=1;outer<=24 && q.active && first<0;outer++){if(outer>1)NativeQuickItemChecks.BeginWorldStep();for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active)Main.npc[i].UpdateNPC(i);q.Update(slot);}
                int actual=first<0?0:(int)(first-sample);bool negative=mode==6 || mode==10;
                Console.WriteLine("FLARE receive mode="+mode+" actual="+actual+" known="+contact?.Tick+" slots="+string.Join(",",hits)+" local="+q.localAI[0]+" damage="+q.damage);
                Require((actual==0)==negative && (contact==null)==negative && (contact==null || contact.Tick==actual),"actual flare owner immunity, unreflectable role, unlimited penetration and last sticking action agree");
                if(mode==7)Require(q.friendly && !q.hostile,"AI33 stays friendly against a reflecting NPC");
                if(mode==8)Require(hits.Take(6).SequenceEqual(new[]{0,1,2,3,4,6}),"unlimited flare visits all qualified receivers in native slot order");
                if(mode==9)Require(q.localAI[0]==1 && q.damage>0,"tile stick action still has its original damage at actual receive");
            }
            Console.WriteLine("PASS FLARE receive=12");
        }
        private static void Natural(object context,int ammo)
        {
            var p=Reset(context,ammo);p.position=new Vector2(650,646);p.releaseUseItem=true;Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,48,40);raw=target.Center-new Vector2(490,0);var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");solves=steps=0;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));for(int i=0;i<3;i++){NativeCombatSelectionPreviewChecks.Step(context,raw,false,true);Require(GetOptional(attack,"ExpectedPreview")!=null && !Main.projectile.Any(q=>q.active),"idle flare preview is independent of attack permission");}
            natural=true;NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));int visible=0;
            for(int i=0;i<42;i++)
            {
                NativeCombatSelectionPreviewChecks.Step(context,raw,i!=22);var plan=(AttackContact)GetOptional(attack,"ExpectedPreview");Require(plan!=null,"feasible flare idle/use/cooldown interval retains a valid preview");visible++;
                int a=solves,b=steps;var marker=Get(Get(host,"World"),"Impact");var zoom=Main.GameViewMatrix.ZoomMatrix;for(int j=0;j<12;j++){Call(marker,"Capture");Call(marker,"Project",zoom,Matrix.Invert(zoom));Require((bool)Call(attack,"PreviewCurrent",plan),"same-update CPU flare presentation remains current");}Require(a==solves && b==steps,"CPU presentation does not solve or replay flares");
            }
            Require(births>=2 && known!=null && first>=birthTick && first-birthTick+1==known.Tick,"actual flare positive Strike matches born flight and a legal release/second click");
            Console.WriteLine("PASS FLARE natural ammo="+ammo+" births="+births+" first="+(first-birthTick+1)+" known="+known.Tick+" preview="+visible+"/42 solves="+solves+" steps="+steps+" CPU=12x0");
            NativeCombatSelectionPreviewChecks.Step(context,raw,false);natural=false;
        }
    }
}
