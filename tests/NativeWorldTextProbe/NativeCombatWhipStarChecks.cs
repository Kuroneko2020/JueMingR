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
    internal static class NativeCombatWhipStarChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static Assembly assembly;private static Type model;private static Projectile parent,receiving;private static int nativeTick,starFirst;private static readonly List<Projectile> born=new List<Projectile>();
        private static object Static(Type type,string name,params object[] args){return type.GetMethod(name,Flags).Invoke(null,args);}
        private static void Birth(IEntitySource __0,int __result)
        {if((__0 as EntitySource_Parent)?.Entity==parent && __result>=0 && __result<1000 && Main.projectile[__result].type==1039)born.Add(Main.projectile[__result]);}
        private static void Damage(Projectile __instance){receiving=__instance;}private static void DamageDone(){receiving=null;}
        private static void Strike(NPC __instance,int __result){if(__result>0 && __instance.whoAmI==1 && receiving?.type==1039 && starFirst==0)starFirst=nativeTick;}
        private static Player Reset(object context)
        {
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),5479,0,0);p.position=new Vector2(850,646);p.velocity=Vector2.Zero;p.ResetEffects();p.itemAnimationMax=p.itemAnimation=p.HeldItem.useAnimation;p.direction=1;Main.windPhysics=false;
            // This manual finite driver advances itemAnimation itself. Run
            // the original pose stage too: GetArmPosition reads bodyFrame,
            // which otherwise leaks an earlier weapon's frame across leaves.
            var inherited=p.bodyFrame;p.PlayerFrame();Console.WriteLine("WHIP STAR native pose inherited="+inherited+" current="+p.bodyFrame+" animation="+p.itemAnimation+"/"+p.itemAnimationMax+" arm="+p.GetArmPosition());
            foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;for(int x=35;x<115;x++)for(int y=10;y<65;y++)Main.tile[x,y].ClearEverything();for(int x=35;x<115;x++)NativeToolsChecks.Tile(x,43,1);born.Clear();nativeTick=starFirst=0;return p;
        }
        private static NPC Target(int slot,Vector2 center)
        {var previous=Main.npc[slot];var retained=Enumerable.Range(0,previous.buffType.Length).Where(i=>previous.buffType[i]>0 && previous.buffTime[i]>0).Select(i=>previous.buffType[i]+":"+previous.buffTime[i]);Console.WriteLine("WHIP STAR fresh native receiver slot="+slot+" inheritedBuffs="+string.Join(",",retained));typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;n.realLife=-1;n.friendly=n.immortal=n.dontTakeDamage=false;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static NpcTrajectory Timeline(NPC n,bool completed=false)
        {
            var identity=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=identity,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(identity,(long)Main.GameUpdateCount-(completed?0:1),1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static object Terrain(){var value=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(value,"Reset");return value;}
        private static AttackContact Known(Player p,Projectile[] sources,NPC n,out object root,bool completed=false)
        {
            var path=Timeline(n,completed);var phase=Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),completed?"CompletedWorld":"BeforeNpc");var clock=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new[]{(object)path,phase},null);var terrain=Terrain();root=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,sources[0],path,0,!completed,completed},null);return (AttackContact)Static(model,"KnownFamily",p,sources,path,clock,terrain,root);
        }
        private static Projectile Shoot(Player p)
        {Main.mouseX=1250-(int)Main.screenPosition.X;Main.mouseY=667-(int)Main.screenPosition.Y;typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});return Main.projectile.Single(q=>q.active && q.type==1034);}
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostWhipStarAttack",true);var audit=new Harmony("JueMingR.Tests.WhipStar");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            var birth=typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float));audit.Patch(birth,postfix:new HarmonyMethod(typeof(NativeCombatWhipStarChecks),nameof(Birth)));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatWhipStarChecks),nameof(Damage)),postfix:new HarmonyMethod(typeof(NativeCombatWhipStarChecks),nameof(DamageDone)));audit.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatWhipStarChecks),nameof(Strike)));
            try{BirthGeometry(context);Trigger(context);Motor(context);FullPool(context);Console.WriteLine("PASS WHIP STAR observed1034 proc/timers, native source births and autonomous1039 Damage.");}finally{Main.windPhysics=wind;Main.ProjectileUpdateLoopIndex=-1;audit.UnpatchAll(audit.Id);}
        }
        private static void BirthGeometry(object context)
        {
            foreach(bool solid in new[]{false,true})
            {
                var p=Reset(context);var n=Target(2,new Vector2(1100,667));parent=Shoot(p);parent.ai[0]=12;parent.AI();var points=new List<Vector2>();Projectile.FillWhipControlPoints(parent,points,p);
                if(solid)foreach(var point in points){var cell=point.ToTileCoordinates();if(WorldGen.InWorld(cell,5))NativeToolsChecks.Tile(cell.X,cell.Y,1);}
                object[] args={parent,p,Terrain(),n.whoAmI,null};Require((bool)Static(model,"Births",args),"all actual whip-tip birth tiles are leased");var expected=(Projectile[])args[4];parent.ConstellationWhip_SpawnConstellationStarsTowards(n,parent.damage*2);
                Require(born.Count==expected.Length && born.Count>0,"actual original constellation count uses every third native control point");
                for(int i=0;i<born.Count;i++)Require(Vector2.Distance(born[i].Center,expected[i].Center)<.001f && born[i].damage==expected[i].damage && born[i].tagEffectType==parent.tagEffectType && born[i].ai[0]==n.whoAmI && born[i].ai[1]>=.02f && born[i].ai[1]<.11f && born[i].velocity.Length()<4.001f,"original source/position/solid fallback/44%current damage/target and native random velocity/color support agree");
            }
            Console.WriteLine("PASS WHIP STAR original point births and solid3 owner fallback");
        }
        private static void Trigger(object context)
        {
            foreach(bool enabled in new[]{false,true})
            {
                var p=Reset(context);var near=Target(1,new Vector2(1000,667));var far=Target(2,new Vector2(1750,667));parent=Shoot(p);
                p.TagEffectStack.TryApplyTagToNPC(parent.tagEffectType,near);if(enabled)p.TagEffectStack.TryEnableProcOnNPC(parent.tagEffectType,near);
                object root;var expected=Known(p,new[]{parent},enabled?near:far,out root);Console.WriteLine("WHIP STAR PREP proc="+enabled+" predicted="+expected?.Tick+" confidence="+expected?.Confidence+" tag="+parent.tagEffectType);
                Console.WriteLine("WHIP STAR LEASE nativeProc="+p.TagEffectStack.CanProcOnNPC(parent.tagEffectType,near.whoAmI)+" capturedTimer="+((int[])Get(Get(root,"whipStarOwner"),"Remaining"))[near.whoAmI]);
                if(!enabled)Require(expected==null,"without an observed eligible proc the beyond-whip star stage does not exist");
                if(enabled){p.TagEffectStack.RemoveAllProcs(parent.tagEffectType);Require(!(bool)Get(root,"Unchanged"),"same-GUC proc removal revokes the exact finite tag lease");p.TagEffectStack.TryEnableProcOnNPC(parent.tagEffectType,near);}
                int first=0,starPreparedAt=0;AttackContact starPlan=null;for(int tick=1;tick<=120;tick++)
                {
                    nativeTick=tick;
                    NativeQuickItemChecks.BeginWorldStep();if(tick>1){p.itemAnimation=Math.Max(0,p.itemAnimation-1);p.TagEffectStack.Update();}p.PlayerFrame();near.UpdateNPC(near.whoAmI);far.UpdateNPC(far.whoAmI);
                    for(int slot=0;slot<1000;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;try{Main.projectile[slot].Update(slot);}finally{Main.ProjectileUpdateLoopIndex=-1;}}
                    if(first==0 && (enabled?near:far).life<10000)first=tick;
                    if(enabled && born.Count>0 && starPlan==null){object starRoot;starPlan=Known(p,born.Where(q=>q.active).ToArray(),near,out starRoot,true);starPreparedAt=tick;if(starPlan!=null)starFirst=0;}
                }
                Console.WriteLine("WHIP STAR TRIGGER proc="+enabled+" born="+born.Count+" primaryFirst="+first+" starPreparedAt="+starPreparedAt+" starPlan="+starPlan?.Tick+" actualStar="+starFirst+" nearLife="+near.life+" farLife="+far.life);
                Require(enabled?expected!=null && first==expected.Tick && born.Count>0 && starPlan!=null && starFirst==starPreparedAt+starPlan.Tick && parent.localAI[0]==1 && !p.TagEffectStack.CanProcOnNPC(parent.tagEffectType,near.whoAmI):born.Count==0 && first==0,"real primary receive, once-only observed proc, own1039 source births and actual later first star Damage preserve causal boundaries");
            }
        }
        private static void Motor(object context)
        {
            var p=Reset(context);var n=Target(2,new Vector2(1700,350));var path=Timeline(n);var q=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(500,350),new Vector2(2,-1),1039,50,0,p.whoAmI,-1,.065f)];var shadow=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostThornAttack",true),"Copy",q);var homing=assembly.GetType("JueMingR.TerrariaHost.Combat.HostHomingEffectAttack",true);var terrain=Terrain();var obstacles=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,true,false},null);var targets=Call(obstacles,"CaptureChaseTargets",q);
            for(int step=0;step<100;step++){Require((bool)Static(homing,"Ai",shadow,targets,path,1,terrain),"1039 complete AI171 query is available");q.AI();Require(Vector2.Distance(q.velocity,shadow.velocity)<.0001f && q.ai[0]==shadow.ai[0] && q.timeLeft==shadow.timeLeft,"original1039 uses its1600 target radius and exact171 warmup/turn, not931 birth assumptions");q.position+=q.velocity;shadow.position+=shadow.velocity;q.timeLeft--;shadow.timeLeft--;}
            Console.WriteLine("PASS WHIP STAR1039 original AI171100 actions with beyond800 target");
        }
        private static void FullPool(object context)
        {
            var p=Reset(context);var n=Target(1,new Vector2(1000,667));parent=Shoot(p);p.TagEffectStack.TryApplyTagToNPC(parent.tagEffectType,n);p.TagEffectStack.TryEnableProcOnNPC(parent.tagEffectType,n);
            for(int slot=0;slot<1000;slot++)if(!Main.projectile[slot].active){var q=Main.projectile[slot];q.SetDefaults(1);q.whoAmI=slot;q.owner=p.whoAmI;q.active=true;q.damage=0;q.position=new Vector2(600,350);q.velocity=Vector2.Zero;q.timeLeft=3600;}
            object root;var expected=Known(p,new[]{parent},n,out root);Require(expected!=null,"pool exhaustion in later OnHit descendants cannot erase the already qualified primary whip Strike");Console.WriteLine("PASS WHIP STAR full pool preserves actual primary receive before optional star births");
        }
    }
}
