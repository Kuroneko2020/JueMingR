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
    internal static class NativeCombatSharpTearsChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static NPC selected;private static Projectile receiving;private static int step,first,depth,rng;
        private static object Static(Type t,string name,params object[] args){return t.GetMethod(name,Flags).Invoke(null,args);}
        private static void Random(){if(depth>0)rng++;}private static void Damage(Projectile __instance){receiving=__instance;}private static void DamageDone(){receiving=null;}
        private static void Strike(NPC __instance,int __result){if(__result>0 && ReferenceEquals(__instance,selected) && receiving?.type==756 && first==0)first=step;}
        private static Player Reset(object context)
        {NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),4270,0,0);p.position=new Vector2(850,646);p.velocity=Vector2.Zero;p.ResetEffects();p.statMana=p.statManaMax=p.statManaMax2=200;Main.windPhysics=false;foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;for(int x=35;x<115;x++)for(int y=10;y<65;y++)Main.tile[x,y].ClearEverything();for(int x=35;x<115;x++)NativeToolsChecks.Tile(x,43,1);first=step=depth=rng=0;selected=null;return p;}
        private static NPC Target(int slot,Vector2 center)
        {var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.width=n.height=20;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;n.realLife=-1;n.friendly=n.immortal=n.dontTakeDamage=false;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static NpcTrajectory Timeline(NPC n)
        {var id=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=id,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(id,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);}
        private static object Terrain(){var t=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static AttackContact Known(Player p,Projectile q,NPC n,out object root)
        {var path=Timeline(n);var phase=Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc");var clock=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new[]{(object)path,phase},null);root=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,true,false},null);depth++;try{return (AttackContact)Static(model,"KnownFamily",p,new[]{q},path,clock,Terrain(),root);}finally{depth--;}}
        private static void Advance(Projectile q,int limit)
        {for(step=1;step<=limit && q.active && first==0;step++){NativeQuickItemChecks.BeginWorldStep();foreach(var n in Main.npc)if(n.active)n.UpdateNPC(n.whoAmI);q.Update(q.whoAmI);}}
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostSharpTearsAttack",true);var audit=new Harmony("JueMingR.Tests.SharpTears");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSharpTearsChecks),nameof(Damage)),postfix:new HarmonyMethod(typeof(NativeCombatSharpTearsChecks),nameof(DamageDone)));audit.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatSharpTearsChecks),nameof(Strike)));foreach(var m in typeof(Terraria.Utilities.UnifiedRandom).GetMethods(Flags).Where(m=>m.DeclaringType==typeof(Terraria.Utilities.UnifiedRandom) && (m.Name=="Next" || m.Name=="NextDouble")))audit.Patch(m,prefix:new HarmonyMethod(typeof(NativeCombatSharpTearsChecks),nameof(Random)));
            try{Motors(context);Spots(context);Receives(context);Hands(context);Consumer(context);Console.WriteLine("PASS SHARP TEARS actual4270 terrain-selected native births, AI157 fixed line, fourth-hit retirement and receive; prediction RNG0.");}finally{depth=0;Main.windPhysics=wind;audit.UnpatchAll(audit.Id);}
        }
        private static void Motors(object context)
        {
            int count=0;foreach(int clock in new[]{-4,0,19,29,35})foreach(float scale in new[]{.8f,1.05f,1.299f})foreach(Vector2 velocity in new[]{Vector2.UnitX*16,-Vector2.UnitY*16})
            {
                var p=Reset(context);var q=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(1100,350),velocity,756,0,0,p.whoAmI,clock,scale)];var copy=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostSuperStarAttack",true),"Copy",q);
                for(int action=0;action<45 && q.active;action++){depth++;try{Static(model,"Ai",copy);}finally{depth--;}if(copy.active)--copy.timeLeft;q.Update(q.whoAmI);Require(q.active==copy.active && q.position==copy.position && q.velocity==copy.velocity && q.ai.SequenceEqual(copy.ai) && q.localAI[0]==copy.localAI[0] && q.alpha==copy.alpha && q.scale==copy.scale && q.timeLeft==copy.timeLeft,"native AI157 negative/start/grow/fade/36th kill is exact, body never moves: "+clock+"/"+scale+"/"+action);for(int x=-20;x<=220;x+=20){var box=new Rectangle((int)q.Center.X+x,(int)q.Center.Y-8,16,16);Require(q.Colliding(q.Hitbox,box)==(bool)Static(model,"Collides",copy,new MotionRect(box.X,box.Y,box.Width,box.Height)),"native SharpTears collision uses its200scale line and negative-clock gate");}}
                Require(rng==0,"private AI157 cannot consume native visual frame/dust randomness");count++;
            }
            Console.WriteLine("PASS SHARP TEARS original motor branches="+count+" and per-action native collider grid");
        }
        private static void Spots(object context)
        {
            foreach(int layout in new[]{0,1,2,3})
            {
                var p=Reset(context);var target=new Vector2(1100,650);if(layout==0)for(int x=35;x<115;x++)Main.tile[x,43].ClearEverything();if(layout>0)for(int x=55;x<70;x++)for(int y=39;y<43;y++)NativeToolsChecks.Tile(x,y,1);if(layout==2)for(int x=55;x<70;x++)Main.tile[x,39].slope(1);if(layout==3)for(int x=55;x<70;x++)Main.tile[x,39].halfBrick(true);object[] args={p,target,Terrain(),null};depth++;try{Require((bool)Static(model,"Spots",args),"SharpTears finite native scan read set is available");}finally{depth--;}var candidates=(Point[])args[3];Require(candidates.Length>0,"native fallback or solid set always has a legal representative");for(int draw=0;draw<24;draw++){var actual=(Point)typeof(Player).GetMethod("FindSharpTearsSpot",Flags).Invoke(p,new object[]{target});if(layout>0)Require(candidates.Contains(actual),"actual unseeded native spot belongs to the complete preferred opening/closed solid candidate set: "+layout);else Require(Math.Abs(actual.X-p.Center.ToTileCoordinates().X)<=2 && Math.Abs(actual.Y-p.Center.ToTileCoordinates().Y)<=2 && candidates.Contains(p.Center.ToTileCoordinates()),"native no-solid fallback includes the zero-offset representative without predicting RNG");}Require(rng==0,"private terrain-selected birth enumeration does not call live RNG");
            }
            Console.WriteLine("PASS SHARP TEARS4 actual solid/slope/half/fallback layouts x24 native choices");
        }
        private static void Receives(object context)
        {
            foreach(int mode in new[]{0,1,2,3,4,5})
            {
                var p=Reset(context);int slot=mode==1?5:mode==2?4:2;selected=Target(slot,new Vector2(1100,350));if(mode==1 || mode==2)for(int i=1;i<slot;i++)Target(i,selected.Center);var q=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(1000,350),Vector2.UnitX*16,756,60,0,p.whoAmI,mode==4?-30:20,1)];if(mode==3 || mode==4)q.localNPCImmunity[slot]=25;selected.immune[p.whoAmI]=mode==5?200:0;object root;var expected=Known(p,q,selected,out root);Advance(q,mode==1?1:70);Require(mode==1 || mode==3 || mode==5?first==0 && expected==null:first>0 && expected!=null && first==expected.Tick,"original local25/separate owner200/negative-clock/four-target slot sequence agrees with complete known family: "+mode+" native="+first+" known="+expected?.Tick);if(mode==1 || mode==2)Require(q.damage==0 && q.penetrate==-1 && q.active,"native fourth received hit silences damage without killing its finite visual body");Console.WriteLine("SHARP receive mode="+mode+" predicted="+expected?.Tick+" native="+first);
            }
        }
        private static void Hands(object context)
        {
            int positive=0;for(int draw=0;draw<8;draw++)
            {var p=Reset(context);selected=Target(2,p.Center+new Vector2(140,0));selected.width=selected.height=40;Main.mouseX=(int)(selected.Center.X-Main.screenPosition.X);Main.mouseY=(int)(selected.Center.Y-Main.screenPosition.Y);typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var q=Main.projectile.Single(s=>s.active && s.type==756);Require(q.damage>0 && q.ai[0]==0 && q.ai[1]>=.8f && q.ai[1]<1.3f && Math.Abs(q.velocity.Length()-16)<.001f && q.penetrate==4 && q.localNPCHitCooldown==25,"actual4270 Shoot keeps native terrain/random birth, scale and four-hit local25 policy");object root;var expected=Known(p,q,selected,out root);Advance(q,40);Require((first>0)==(expected!=null) && (expected==null || expected.Tick==first),"each actual random SharpTears birth has independently sealed native first Damage equal to Known");if(first>0)positive++;}
            Require(positive>0,"real native Blood Thorn hand has useful selected receive");var owner=Reset(context);selected=Target(2,owner.Center+new Vector2(140,0));var path=Timeline(selected);var ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",owner,owner.HeldItem);var rootType=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true);var lease=Activator.CreateInstance(rootType,Flags,null,new object[]{owner,Terraria.ID.ContentSamples.ProjectilesByType[756],path,0,true,false},null);AttackContact contact;depth++;try{using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))contact=(AttackContact)Static(model,"Solve",owner,ammo,path,owner.MountedCenter,0,true,Terrain(),lease,0);}finally{depth--;}Require(contact!=null && contact.Confidence==AttackConfidence.Representative && rng==0,"whole terrain-selected SharpTears hand has a legal useful source solution without native RNG");Console.WriteLine("PASS SHARP TEARS original8 hands, native positive="+positive+", legal whole-source representative="+contact.Tick+" RNG="+rng);
        }
        private static void Consumer(object context)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");
            for(int mode=0;mode<3;mode++)
            {
                var p=Reset(context);p.itemAnimation=p.itemTime=0;p.releaseUseItem=true;selected=Target(2,p.Center+new Vector2(100,0));selected.width=200;selected.height=80;selected.Center=p.Center+new Vector2(100,0);Projectile ambient=null;if(mode==2)ambient=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(1250,350),Vector2.UnitX*16,756,0,0,p.whoAmI,20,1)];var raw=selected.Center-new Vector2(500,0);
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,true));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),raw,true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(host,attack,selected,0);var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");var root=GetOptional(attack,"obstacles");Require(plan!=null && root!=null,"public Blood Thorn preparation owns the complete terrain-selected birth/scene lease");if(mode==1)p.fullRotation+=.1f;if(mode==2)ambient.scale+=.1f;if(mode!=0)Require(!(bool)Get(root,"Unchanged"),"same-GUC SharpTears owner or ambient scale edit revokes actual source preparation");
                int mouse=Main.mouseX;typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});Require(Main.mouseX==mouse,"original Blood Thorn hand restores the borrowed physical cursor");var born=Main.projectile.Single(q=>q.active && q.type==756 && q!=ambient);
                if(mode==0){Require((bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced","real native4270 hand consumes the public source preparation");object lease;var known=Known(p,born,selected,out lease);Advance(born,40);Require(known!=null && first==known.Tick,"the actual random enhanced Blood Thorn birth delivers real native Strike equal to its sealed Known");}
                else Require(!(bool)Get(attack,"hasConsumedInput") || Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))!="Enhanced","revoked SharpTears source lease cannot be consumed as Enhanced");
            }
            Console.WriteLine("PASS SHARP TEARS public prepared source/native Enhanced+Damage/owner and family-scale guards (1000ms component allowance)");
        }
    }
}
