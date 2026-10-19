using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Utilities;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatSpearShardChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly host;private static Type model;private static NPC target;private static int hits;private static long firstGuc;
        private static Projectile representativeParent;private static bool representativeDraws;
        private static int representativeInts,representativeFloats,pureDepth,privateRng;
        private static void PureBegin(){pureDepth++;}
        private static Exception PureEnd(Exception __exception){pureDepth--;return __exception;}
        private static void RandomRead(){if(pureDepth>0)privateRng++;}
        private static void NativeBegin(Projectile __instance)
        {representativeDraws=ReferenceEquals(__instance,representativeParent) && __instance.type==367 && __instance.localAI[0]==0;}
        private static Exception NativeEnd(Exception __exception){representativeDraws=false;return __exception;}
        // Feed only legal inputs at the real AI019 caller. All original spawn,
        // vector, lifetime and Damage code still executes; no child is replaced.
        private static int DrawInt(UnifiedRandom random,int max)
        {if(representativeDraws && max==3){representativeInts++;return 0;}return random.Next(max);}
        private static int DrawRange(UnifiedRandom random,int min,int max)
        {if(representativeDraws && min==1 && max==4){representativeInts++;return 1;}if(representativeDraws && min==30 && max==150){representativeInts++;return 90;}return random.Next(min,max);}
        private static float DrawFloat(UnifiedRandom random)
        {if(representativeDraws){representativeFloats++;return .5f;}return random.NextFloat();}
        private static IEnumerable<CodeInstruction> NativeScalars(IEnumerable<CodeInstruction> source)
        {
            int count=0;
            foreach(var instruction in source)
            {
                var method=instruction.operand as MethodInfo;string replacement=null;
                if(method!=null && method.DeclaringType==typeof(UnifiedRandom) && method.Name=="Next")
                {var args=method.GetParameters();if(args.Length==1)replacement=nameof(DrawInt);else if(args.Length==2)replacement=nameof(DrawRange);}
                if(method!=null && method.DeclaringType==typeof(Utils) && method.Name=="NextFloat" && method.GetParameters().Length==1)replacement=nameof(DrawFloat);
                if(replacement!=null){instruction.opcode=OpCodes.Call;instruction.operand=typeof(NativeCombatSpearShardChecks).GetMethod(replacement,Flags);count++;}
                yield return instruction;
            }
            Require(count>0,"original AI019 scalar call sites are present, including previously compiled wrappers");
        }
        private static void OutsideRandom()
        {
            Require(!representativeDraws,"representative input scope is retired outside the actual first AI");var a=new UnifiedRandom(93417);var b=new UnifiedRandom(93417);
            for(int i=0;i<8;i++)Require(DrawInt(a,3)==b.Next(3) && DrawRange(a,1,4)==b.Next(1,4) && DrawRange(a,30,150)==b.Next(30,150) && DrawFloat(a)==b.NextFloat() && a.NextDouble()==b.NextDouble(),"uncontrolled scalar results and subsequent RNG state remain the original sequence");
        }
        private static void Strike(NPC __instance,int __result){if(ReferenceEquals(__instance,target) && __result>0){if(hits==0)firstGuc=Main.GameUpdateCount;hits++;}}
        private static Player Scene(object context)
        {
            var p=NativeCombatBallisticCandidateChecks.Scene(context,2331,50);foreach(var npc in Main.npc)npc.active=false;Main.windPhysics=false;
            for(int x=5;x<115;x++)for(int y=5;y<78;y++)Main.tile[x,y].ClearEverything();for(int x=39;x<45;x++)NativeToolsChecks.Tile(x,43,1);
            p.itemAnimationMax=p.itemAnimation=p.HeldItem.useAnimation;p.direction=1;p.velocity=Vector2.Zero;return p;
        }
        private static NpcTrajectory Path(NPC n,bool beforeNpc=false)
        {
            var identity=new NpcIdentity(1,n,n.whoAmI,n.generation,n.type,n.netID);var state=new NpcMotionState{Identity=identity,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,Active=true,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(identity,(long)Main.GameUpdateCount-(beforeNpc?1:0),1,PredictionAssumption.None,PredictionStop.None,points,points.Length);
        }
        private static NPC Target(Vector2 center,bool blocked=false)
        {var n=Main.npc[2];n.SetDefaults(3);n.active=true;n.whoAmI=2;n.width=80;n.height=80;n.Center=center;n.life=n.lifeMax=100000;n.defense=0;n.knockBackResist=0;n.noTileCollide=n.noGravity=true;n.aiStyle=-1;n.dontTakeDamage=blocked;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static object Terrain(){var t=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static AttackContact Known(Player p,Projectile shot,NPC n)
        {object root;return Known(p,shot,n,out root);}
        private static AttackContact Known(Player p,Projectile shot,NPC n,out object root,bool beforeNpc=false)
        {
            var path=Path(n,beforeNpc);var terrain=Terrain();var phase=Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),beforeNpc?"BeforeNpc":"CompletedWorld");var clock=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,phase},null);root=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,shot,path,0,beforeNpc,!beforeNpc},null);
            using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))return (AttackContact)model.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{p,new[]{shot},path,clock,terrain,root});
        }
        internal static void Run(object context)
        {
            host=Get(context,"Combat").GetType().Assembly;model=host.GetType("JueMingR.TerrariaHost.Combat.HostSpearShardAttack",true);int cases=0,actions=0;
            foreach(int type in new[]{1107,1108,1109})foreach(int scene in new[]{0,1,2,3,4})
            {
                var p=Scene(context);if(scene==1 || scene==2)for(int x=25;x<105;x++)for(int y=10;y<75;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:2);}
                if(scene==3)for(int y=15;y<45;y++)NativeToolsChecks.Tile(50,y,1);if(scene==4)for(int x=20;x<110;x++)NativeToolsChecks.Tile(x,35,1);
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(700,500),new Vector2(7,scene==4?2:-1),type,0,0,p.whoAmI);var native=Main.projectile[slot];var shadow=(Projectile)host.GetType("JueMingR.TerrariaHost.Combat.HostBetsyFlameAttack").GetMethod("Copy",Flags).Invoke(null,new object[]{native});var terrain=Terrain();
                for(int outer=0;outer<40 && native.active;outer++)
                {
                    for(int sub=0;sub<=shadow.extraUpdates && shadow.active;sub++){Require((bool)model.GetMethod("ChildStep",Flags).Invoke(null,new object[]{shadow,terrain}),"Obsidian child concrete terrain step");if(shadow.active && --shadow.timeLeft<=0)shadow.active=false;actions++;}
                    NativeQuickItemChecks.BeginWorldStep();native.Update(slot);Require(native.active==shadow.active && (!native.active || Vector2.DistanceSquared(native.position,shadow.position)<.001f && Vector2.DistanceSquared(native.velocity,shadow.velocity)<.0001f && native.ai[0]==shadow.ai[0] && native.timeLeft==shadow.timeLeft),"AI014 exact child water retirement/floor/X-only reflection type="+type+" scene="+scene+" outer="+outer+" native="+native.position+"/"+native.velocity+" model="+shadow.position+"/"+shadow.velocity);
                }
                cases++;
            }
            foreach(int type in new[]{1107,1108,1109})foreach(bool blocked in new[]{false,true})
            {
                var p=Scene(context);var n=Target(new Vector2(900,400),blocked);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(700,400),new Vector2(8,0),type,40,0,p.whoAmI);var q=Main.projectile[slot];var contact=Known(p,q,n);int first=0;
                for(int tick=1;tick<=120 && q.active;tick++){NativeQuickItemChecks.BeginWorldStep();if(n.immune[p.whoAmI]>0)n.immune[p.whoAmI]--;q.Update(slot);if(n.life<100000){first=tick;break;}}
                Require(blocked?contact==null && first==0:contact!=null && contact.Tick==first && first>0,"all three observed shard types keep exact native first Damage and invulnerable negative");
            }
            // The captured center belongs to the last frame. AI019 resets it
            // to the owner before extension; accumulating the old center is wrong.
            foreach(bool blocked in new[]{false,true})
            {
                var p=Scene(context);var n=Target(p.MountedCenter+new Vector2(25,0),blocked);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(1100,350),new Vector2(4,0),367,50,0,p.whoAmI);var q=Main.projectile[slot];q.localAI[0]=1;q.ai[0]=1;var contact=Known(p,q,n);NativeQuickItemChecks.BeginWorldStep();q.Update(slot);
                Require(blocked?contact==null && n.life==100000:contact!=null && contact.Tick==1 && n.life<100000,"captured prior spear center is reset to the true owner anchor before next Damage");
            }
            var audit=new Harmony("JueMingR.Tests.SpearShards");audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatSpearShardChecks),nameof(Strike)));
            representativeParent=null;representativeDraws=false;representativeInts=representativeFloats=pureDepth=privateRng=0;OutsideRandom();
            audit.Patch(typeof(Projectile).GetMethod("AI_019_Spears",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSpearShardChecks),nameof(NativeBegin)),transpiler:new HarmonyMethod(typeof(NativeCombatSpearShardChecks),nameof(NativeScalars)),finalizer:new HarmonyMethod(typeof(NativeCombatSpearShardChecks),nameof(NativeEnd)));
            foreach(string method in new[]{"SolvePrepared","KnownFamily","ChildStep"})audit.Patch(model.GetMethod(method,Flags),prefix:new HarmonyMethod(typeof(NativeCombatSpearShardChecks),nameof(PureBegin)),finalizer:new HarmonyMethod(typeof(NativeCombatSpearShardChecks),nameof(PureEnd)));
            foreach(var method in typeof(UnifiedRandom).GetMethods(Flags).Where(m=>m.DeclaringType==typeof(UnifiedRandom) && (m.Name=="Next" || m.Name=="NextDouble")))audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatSpearShardChecks),nameof(RandomRead)));
            try
            {
                var p=Scene(context);target=Target(new Vector2(1050,660));hits=0;firstGuc=-1;var ambient=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(1100,350),Vector2.Zero,1107,0,0,p.whoAmI)];var observation=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,false,true,50,true,true));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),target.Center-new Vector2(500,0),true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,target,0,phaseName:"BeforeNpc");var solution=NativeCombatAimChecks.RequireSolution(attack,p,"Obsidian hand has a reachable representative body/shard point");ambient.ai[0]++;try{NativeCombatAimChecks.RequireSolution(attack,p,"ambient shard clock does not revoke the public representative hand");}finally{ambient.ai[0]--;}
                var velocity=p.velocity;p.velocity+=Vector2.UnitX;try{Require(NativeCombatAimChecks.QueryInput(attack,p).Kind!="Enhanced","changed owner motion revokes the prepared spear point");}finally{p.velocity=velocity;}NativeCombatAimChecks.RequireSolution(attack,p,"restored owner motion admits the current spear point");Require(!(bool)Get(attack,"hasConsumedInput"),"spear queries do not fabricate a real hand consumption");typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});NativeCombatAimChecks.RequireConsumedInput(attack,p,solution,"Obsidian original spear Shoot consumes its actual assisted point");var parent=Main.projectile.Single(q=>q.active && q.type==367);
                // Once the parent is born, this dedicated oracle still owns the
                // complete family and its exact legal primitive representative.
                object captured;var sourcePlan=Known(p,parent,target,out captured,true);Require(sourcePlan!=null && sourcePlan.Confidence==AttackConfidence.Representative && (bool)Get(captured,"Unchanged"),"observed spear family has a complete useful replay root with its unresolved legal child representative");ambient.ai[0]++;try{Require(!(bool)Get(captured,"Unchanged"),"ambient shard clock remains a dependency of the dedicated full-family oracle");}finally{ambient.ai[0]--;}
                Console.WriteLine("SPEAR HAND PREMISE knownTick="+sourcePlan.Tick+" confidence="+sourcePlan.Confidence+" representative="+solution.NativeTick+" aim="+solution.Point+" origin="+parent.Center+" velocity="+parent.velocity+" rotation="+p.fullRotation+" animation="+p.itemAnimation+"/"+p.itemAnimationMax+" target="+target.Hitbox+" player="+p.position);int damage=parent.damage;bool born=false;
                representativeParent=parent;int nativeFirst=0;
                for(int tick=1;tick<=120 && hits==0;tick++)
                {
                    // The observed BeforeNpc family owns this GUC before its first AI.
                    if(tick>1)NativeQuickItemChecks.BeginWorldStep();if(tick>1)p.itemAnimation=Math.Max(0,p.itemAnimation-1);for(int index=0;index<Main.maxProjectiles;index++)if(Main.projectile[index].active){Main.ProjectileUpdateLoopIndex=index;try{Main.projectile[index].Update(index);}finally{Main.ProjectileUpdateLoopIndex=-1;}}
                    var children=Main.projectile.Where(q=>q.active && !ReferenceEquals(q,ambient) && q.type>=1107 && q.type<=1109).ToArray();if(tick==1){Console.WriteLine("SPEAR FIRST CHILDREN "+string.Join(";",children.Select(q=>q.type+"@"+q.Center+"/"+q.velocity+"/left"+q.timeLeft+"/dmg"+q.damage+"/slot"+q.whoAmI))+" parent="+parent.Center+"/"+parent.velocity);Require(children.Length>=1 && children.Length<=3 && parent.localAI[0]==1 && children.All(q=>q.damage==(int)(damage*.5) && q.timeLeft>=28 && q.timeLeft<=149),"first native spear AI creates its real random count/type/damage/lifetime family once");Require(children.Length==1 && children[0].type==1107 && representativeInts==3 && representativeFloats==3,"actual original scalar draws select the same legal count/type/speed/lifetime representative, not a random guaranteed hit");born=true;}if(hits>0)nativeFirst=tick;
                }
                representativeParent=null;OutsideRandom();Console.WriteLine("SPEAR HAND RESULT born="+born+" hits="+hits+" targetLife="+target.life+" target="+target.Hitbox+" nativeFirst="+nativeFirst+" representativeTick="+sourcePlan.Tick+" privateRng="+privateRng);Require(born && hits>0,"actual random native child family, after the short spear body, receives useful Damage");Require(firstGuc>=0 && firstGuc-sourcePlan.Timeline.SampleTick==sourcePlan.Tick && firstGuc-sourcePlan.Timeline.SampleTick==sourcePlan.ReceiveTick,"actual spear Strike GUC agrees with its dedicated BeforeNpc timeline");Console.WriteLine("SPEAR absolute sample="+sourcePlan.Timeline.SampleTick+" strike="+firstGuc+" receive="+sourcePlan.ReceiveTick);Require(nativeFirst==sourcePlan.Tick && pureDepth==0 && privateRng==0,"same legal representative retains exact first native receive and zero private RNG");Console.WriteLine("SPEAR-SHARDS actual hand native strikes="+hits);
            }
            finally{representativeParent=null;representativeDraws=false;audit.UnpatchAll(audit.Id);target=null;Require(!audit.GetPatchedMethods().Any(m=>Harmony.GetPatchInfo(m).Owners.Contains(audit.Id)),"all native scalar audit hooks are removed after this component");}
            Console.WriteLine("SPEAR-SHARDS motorCases="+cases+" actions="+actions+" observedReceive=6 actualHand=1");
        }
    }
}
