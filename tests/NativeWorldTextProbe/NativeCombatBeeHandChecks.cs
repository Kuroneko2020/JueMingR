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
    internal static class NativeCombatBeeHandChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static NPC victim,selected;private static Projectile damage;private static int primary,first,depth,rng;
        private static void Damage(Projectile __instance){damage=__instance;}
        private static void DamageDone(){damage=null;}
        private static void Strike(NPC __instance,int __result){if(__result<=0)return;if(ReferenceEquals(__instance,victim) && damage==null)primary++;if(ReferenceEquals(__instance,selected) && damage!=null && (damage.type==181 || damage.type==566) && first==0)first=(int)Main.GameUpdateCount;}
        private static void Random(){if(depth>0)rng++;}
        private static NPC Target(int slot,Vector2 center,int life)
        {var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.width=n.height=34;n.Center=center;n.life=n.lifeMax=life;n.defense=0;n.knockBackResist=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.friendly=n.immortal=n.dontTakeDamage=false;n.velocity=n.netOffset=Vector2.Zero;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static NpcTrajectory Path(NPC n)
        {var id=new NpcIdentity(1,n,n.whoAmI,n.generation,n.type,n.netID);var s=new NpcMotionState{Identity=id,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=true,Active=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,s)).ToArray();return new NpcTrajectory(id,Main.GameUpdateCount,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length);}
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var observation=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var assembly=combat.GetType().Assembly;var audit=new Harmony("JueMingR.Tests.BeeHand");
            // This source really kills its primary victim. Keep the original
            // Strike/checkDead/loot path with Main's genuine databases, as in
            // the existing attached-weapon oracle; never bypass native death.
            if(Main.BestiaryTracker==null)Main.BestiaryTracker=new Terraria.GameContent.Bestiary.BestiaryUnlocksTracker();
            // First real kill also queries bestiary completion. A tracker alone
            // is insufficient: Main.Initialize populates this database first.
            if(Main.BestiaryDB==null){Main.BestiaryDB=new Terraria.GameContent.Bestiary.BestiaryDatabase();new Terraria.GameContent.Bestiary.BestiaryDatabaseNPCsPopulator().Populate(Main.BestiaryDB);}
            if(Main.ItemDropSolver==null){Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);}
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBeeHandChecks),nameof(Damage)),finalizer:new HarmonyMethod(typeof(NativeCombatBeeHandChecks),nameof(DamageDone)));
            audit.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatBeeHandChecks),nameof(Strike)));
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            foreach(var m in typeof(Terraria.Utilities.UnifiedRandom).GetMethods(Flags).Where(m=>m.DeclaringType==typeof(Terraria.Utilities.UnifiedRandom) && (m.Name=="Next" || m.Name=="NextDouble")))audit.Patch(m,prefix:new HarmonyMethod(typeof(NativeCombatBeeHandChecks),nameof(Random)));
            int cases=0;try
            {
                foreach(bool strong in new[]{false,true})foreach(bool blocked in new[]{false,true})
                {
                    NativeCombatObservationChecks.Save(observation,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),1123,0,0);p.position=new Vector2(700,646);p.velocity=Vector2.Zero;p.strongBees=strong;p.armor[3].SetDefaults(strong?3333:0);p.releaseJump=p.releaseUp=p.releaseUseItem=true;p.direction=1;Main.screenPosition=new Vector2(600,400);Main.windPhysics=false;
                    foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;Main.tileSolid[1]=true;for(int x=5;x<Main.maxTilesX-5;x++)for(int y=5;y<Main.maxTilesY-5;y++)Main.tile[x,y].ClearEverything();for(int x=5;x<Main.maxTilesX-5;x++)NativeToolsChecks.Tile(x,43,1);
                    primary=first=rng=0;victim=Target(3,p.Center+new Vector2(36,-4),1);selected=Target(2,p.Center+new Vector2(185,-4),100000);
                    NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,true,false,0,false,true));
                    for(int step=0;step<3;step++)NativeCombatSelectionPreviewChecks.Step(context,selected.Center,false);
                    for(int step=0;step<35 && !Main.projectile.Any(q=>q.active && (q.type==181 || q.type==566));step++)NativeCombatSelectionPreviewChecks.Step(context,selected.Center,true);
                    var children=Main.projectile.Where(q=>q.active && q.owner==p.whoAmI && (q.type==181 || q.type==566)).ToArray();Require(primary>0 && !victim.active && children.Length>=1 && children.Length<=(strong?4:3) && children.All(q=>q.melee && q.damage>0),"real BeeKeeper melee hits another receiver and births original melee bees");
                    selected.dontTakeDamage=blocked;var path=Path(selected);var phase=Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");object[] args={p,path,phase,false};object flight;
                    depth++;try{using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))flight=attack.GetType().GetMethod("ReadFlight",Flags).Invoke(attack,args);}finally{depth--;}
                    var contact=flight==null?null:(AttackContact)Get(flight,"Contact");Require((bool)args[3] && (blocked?contact==null:contact!=null),"actual BeeKeeper weapon Flight observes born bees and respects receiver qualification");
                    var registered=(System.Collections.IDictionary)Get(Get(attack,"Control"),"shots");foreach(var q in children)Require(!registered.Contains((int)q.key),"actual hand-born free bee has no cursor source");
                    long start=Main.GameUpdateCount;for(int step=0;step<120 && first==0 && Main.projectile.Any(q=>q.active);step++){NativeQuickItemChecks.BeginWorldStep();selected.UpdateNPC(2);for(int slot=0;slot<1000;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;try{Main.projectile[slot].Update(slot);}finally{Main.ProjectileUpdateLoopIndex=-1;}}}
                    Require(blocked?first==0:first>start && first-start==contact.Tick,"actual real hand -> another victim -> free bee selected first Strike agrees with sealed Flight");Require(rng==0,"private observed hand-bee replay consumes no live RNG");Console.WriteLine("BEE HAND strong="+strong+" blocked="+blocked+" born="+children.Length+" expected="+contact?.Tick+" native="+(first==0?0:first-start));cases++;
                }
                Console.WriteLine("BEE HAND actual native primary/offspring consumers="+cases+"; no future unobserved hand proc or extra input authority claimed");
            }
            catch{Console.WriteLine("BEE HAND FAILURE primary="+primary+" first="+first+" victim="+victim?.life+"/"+victim?.active+" bees="+Main.projectile.Count(q=>q.active && (q.type==181 || q.type==566)));throw;}
            finally{audit.UnpatchAll(audit.Id);damage=null;victim=selected=null;}
        }
    }
}
