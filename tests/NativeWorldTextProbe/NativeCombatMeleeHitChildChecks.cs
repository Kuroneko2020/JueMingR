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
    internal static class NativeCombatMeleeHitChildChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly host;private static Type model;private static object handContext;
        private static NPC victim,selected;private static Projectile receiving;private static int depth,rng,primary,first,caseItem,bornSource,expectedDamage;
        private static long epoch;private static AttackContact handPlan;private static bool captured,blocked;
        private static object Static(Type t,string name,params object[] args){return t.GetMethod(name,Flags).Invoke(null,args);}
        private static void Random(){if(depth>0)rng++;}
        private static void Damage(Projectile __instance){receiving=__instance;}
        private static void DamageDone(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {if(__result<=0)return;if(ReferenceEquals(__instance,victim) && receiving==null)primary++;if(ReferenceEquals(__instance,selected) && receiving!=null && Family(receiving.type) && first==0)first=(int)(Main.GameUpdateCount-epoch);}
        private static bool Family(int type){return type==1111 || type==524 || type==977 || type==978;}
        private static int Child(int item){return item==1166?1111:item==3211?524:item==155?977:978;}
        private static void BeforeHit(int originalDamage){expectedDamage=(int)(originalDamage*.75);}
        private static void OnHit(int damage,int dmgRandomized)
        {if(caseItem==3211)expectedDamage=(int)(damage*.5);if(caseItem==155)expectedDamage=(int)(dmgRandomized*.5f);if(caseItem==121)expectedDamage=(int)(damage*.75f);}
        private static void Birth(IEntitySource __0,int __result)
        {if(handContext!=null && __result>=0 && __result<Main.maxProjectiles && Main.projectile[__result].type==Child(caseItem)){var source=__0 as EntitySource_ItemUse;Require(source!=null && source.Item.type==caseItem,"native hand offspring keeps the real ItemUse source");bornSource++;}}
        private static void AfterHit(int npcIndex)
        {
            if(handContext==null || captured || !ReferenceEquals(Main.npc[npcIndex],victim) || primary==0)return;
            var p=Main.LocalPlayer;var children=Main.projectile.Where(child=>child.active && child.owner==p.whoAmI && child.type==Child(caseItem)).ToArray();if(children.Length==0)return;
            Require(children.Length==1 && children[0].damage==expectedDamage && bornSource==1,"actual native hand births the one finite child with its original pre/post-randomization damage");
            // This is an observation of an ALREADY born random body. Place a
            // receiver in that sealed body's lane at the post-hand boundary;
            // no future hand aim or prediction of its random birth is claimed.
            var q=children[0];selected.Center=q.Center+(q.type==978?Vector2.UnitX*20:q.velocity.SafeNormalize(Vector2.UnitX)*25);selected.dontTakeDamage=blocked;
            epoch=Main.GameUpdateCount-1;var path=Path(selected,epoch);var attack=Get(Get(handContext,"Combat"),"Attack");var phase=Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc");var args=new object[]{p,path,phase,false};object flight;
            depth++;try{using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))flight=Call(attack,"ReadFlight",args);}finally{depth--;}
            handPlan=flight==null?null:Get(flight,"Contact") as AttackContact;Require((bool)args[3] && (blocked?handPlan==null:handPlan!=null),"real public Flight observes native hand-born child with exact receiver qualification item="+caseItem);
            if(handPlan!=null)Require(flight!=null && (bool)Get(flight,"Current") && (bool)Get(flight,"DependenciesCurrent"),"actual hand child Flight returns both captured scalar and full dependencies");
            var registered=(System.Collections.IDictionary)Get(Get(attack,"Control"),"shots");Require(!registered.Contains((int)q.key),"native hand child never becomes a new continuous cursor source");captured=true;
        }
        private static Player Scene(object context,int item=1166)
        {
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),item,0,0);
            p.position=new Vector2(700,646);p.velocity=Vector2.Zero;p.direction=1;p.gravDir=1;p.attackCD=0;p.ResetMeleeHitCooldowns();p.releaseUseItem=p.releaseUp=p.releaseJump=true;p.itemAnimation=p.itemTime=0;Main.windPhysics=false;Main.ProjectileUpdateLoopIndex=-1;
            foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;for(int x=5;x<Main.maxTilesX-5;x++)for(int y=5;y<Main.maxTilesY-5;y++)Main.tile[x,y].ClearEverything();Main.tileSolid[1]=true;return p;
        }
        private static NPC Target(int slot,Vector2 center,int side=30,int life=100000)
        {
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.active=true;n.whoAmI=slot;n.width=n.height=side;n.Center=center;n.life=n.lifeMax=life;n.defense=0;n.knockBackResist=0;n.velocity=n.netOffset=Vector2.Zero;n.noGravity=n.noTileCollide=true;n.aiStyle=-1;n.immortal=n.dontTakeDamage=n.friendly=false;Array.Clear(n.immune,0,n.immune.Length);return n;
        }
        private static object Terrain(){var t=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static Projectile Copy(Projectile p){return (Projectile)Static(host.GetType("JueMingR.TerrariaHost.Combat.HostNoteAttack",true),"Copy",p);}
        private static NpcTrajectory Path(NPC n,long? sample=null)
        {var id=new NpcIdentity(1,n,n.whoAmI,n.generation,n.type,n.netID);var s=new NpcMotionState{Identity=id,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,Active=true,CanReceive=!n.dontTakeDamage,NoTileCollide=n.noTileCollide};var points=Enumerable.Range(0,121).Select(i=>new NpcTrajectoryPoint(i,s)).ToArray();return new NpcTrajectory(id,sample??Main.GameUpdateCount,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length);}
        private static AttackContact Known(Player p,Projectile q,NPC n,out object root)
        {
            var path=Path(n);root=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,false,true},null);var phase=Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");var clock=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,phase},null);
            depth++;try{using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))return (AttackContact)Static(model,"KnownFamily",p,new[]{q},path,clock,Terrain(),root);}finally{depth--;}
        }
        private static void Advance(int limit=120)
        {for(int t=0;t<limit && first==0 && Main.projectile.Any(q=>q.active);t++){NativeQuickItemChecks.BeginWorldStep();foreach(var n in Main.npc)if(n.active)n.UpdateNPC(n.whoAmI);for(int slot=0;slot<Main.maxProjectiles;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;try{Main.projectile[slot].Update(slot);}finally{Main.ProjectileUpdateLoopIndex=-1;}}}}
        private static void Motors(object context)
        {
            int cases=0,actions=0;
            foreach(int type in new[]{1111,524,977,978})foreach(int kind in Enumerable.Range(0,8))
            {
                var p=Scene(context);
                // The harness has only120x120 real tiles, while Terraria's
                // default world bounds can still describe a larger world.
                // Keep the native motor oracle inside that actual tile arena.
                for(int x=10;x<=110;x++){NativeToolsChecks.Tile(x,6,1);NativeToolsChecks.Tile(x,70,1);}for(int y=6;y<=70;y++){NativeToolsChecks.Tile(10,y,1);NativeToolsChecks.Tile(110,y,1);}
                if(kind>=1 && kind<=3)for(int x=35;x<100;x++)for(int y=15;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(kind==1?0:kind==2?2:3);}if(kind==4)for(int y=5;y<75;y++)NativeToolsChecks.Tile(52,y,1);if(kind==5)for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,25,1);
                Main.windPhysics=kind==7;Main.windSpeedCurrent=.4f;Main.windPhysicsStrength=.2f;int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(800,370),new Vector2(4,2),type,0,0,p.whoAmI,type==977?.9f:0);var q=Main.projectile[slot];if(kind==6){if(type==1111)q.ai[0]=19;if(type==524)q.ai[0]=499.5f;if(type==977)q.timeLeft=1;if(type==978){q.frame=4;q.frameCounter=1;}}
                var shadow=Copy(q);var terrain=Terrain();for(int outer=0;outer<120 && q.active;outer++)
                {
                    int updates=shadow.extraUpdates+1;for(int sub=0;sub<updates && shadow.active;sub++){depth++;try{Require((bool)Static(model,"Step",shadow,terrain),"finite hit-child full environment is resolved type="+type+" scene="+kind+" outer="+outer+" sub="+sub+" style="+shadow.aiStyle+" slope="+shadow.correctSlopeCollision+" position="+shadow.position);}finally{depth--;}if(shadow.active && (--shadow.timeLeft<=0 || shadow.penetrate==0)){shadow.active=false;shadow.timeLeft=0;}actions++;}
                    NativeQuickItemChecks.BeginWorldStep();q.Update(slot);Require(q.active==shadow.active,"original hit-child exact retirement type="+type+" scene="+kind+" outer="+outer);if(!q.active)break;
                    Require(Vector2.DistanceSquared(q.position,shadow.position)<.00001f && Vector2.DistanceSquared(q.velocity,shadow.velocity)<.00001f && q.ai.SequenceEqual(shadow.ai) && q.timeLeft==shadow.timeLeft && q.frame==shadow.frame && q.frameCounter==shadow.frameCounter && q.alpha==shadow.alpha && Math.Abs(q.rotation-shadow.rotation)<.00001f && Math.Abs(q.scale-shadow.scale)<.00001f,"native hit-child motor/clock/bounce exact type="+type+" scene="+kind+" outer="+outer+" actual="+q.position+" model="+shadow.position); 
                }
                cases++;
            }
            Console.WriteLine("MELEE CHILD motors="+cases+" actions="+actions);
        }
        private static void Receives(object context)
        {
            int cases=0;
            foreach(int type in new[]{1111,524,977,978})foreach(bool deny in new[]{false,true})
            {
                var p=Scene(context);p.position=new Vector2(780,328);selected=Target(2,new Vector2(900,350));selected.dontTakeDamage=deny;if(type==978)selected.noTileCollide=false;int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(830,350),new Vector2(4,0),type,40,0,p.whoAmI);var q=Main.projectile[slot];object root;var plan=Known(p,q,selected,out root);Require((bool)Get(root,"Unchanged"),"actual finite hit-child lease is initially current");int style=q.aiStyle;q.aiStyle=0;Require(!(bool)Get(root,"Unchanged"),"sameGUC child motor dispatch revokes plan");q.aiStyle=style;
                if(type==978){q.frameCounter++;Require(!(bool)Get(root,"Unchanged"),"sameGUC real frame retirement clock revokes Volcano plan");q.frameCounter--;q.ownerHitCheck=false;Require(!(bool)Get(root,"Unchanged"),"sameGUC owner-qualification policy revokes Volcano plan");q.ownerHitCheck=true;p.position.X++;Require(!(bool)Get(root,"Unchanged"),"sameGUC actual future owner anchor revokes Volcano plan");p.position.X--;}
                first=0;epoch=Main.GameUpdateCount;Advance();Require(deny?plan==null && first==0:plan!=null && first==plan.Tick,"sealed hit-child exact native receive and negative type="+type+" actual="+first+" plan="+plan?.Tick);cases++;
            }
            // A native AI096 body's preceding hit advances its separate AI clock
            // kill clock, without changing the shared static/local history.
            var owner=Scene(context);owner.position=new Vector2(780,328);victim=Target(1,new Vector2(850,350));selected=Target(2,new Vector2(940,350));int index=Projectile.NewProjectile(owner.GetProjectileSource_Item(owner.HeldItem),new Vector2(830,350),new Vector2(4,0),524,40,0,owner.whoAmI);var shot=Main.projectile[index];object lease;var predicted=Known(owner,shot,selected,out lease);first=0;epoch=Main.GameUpdateCount;Advance();Require(predicted!=null && first==predicted.Tick && shot.ai[0]>=50,"524 actually passes an earlier receiver with OnHit AI clock +50 before selected Strike");Console.WriteLine("MELEE CHILD sealed receives="+cases+" earlier524=true");
            var wallOwner=Scene(context);wallOwner.position=new Vector2(780,328);selected=Target(2,new Vector2(900,350));selected.noTileCollide=false;for(int y=5;y<75;y++)NativeToolsChecks.Tile(53,y,1);int wallSlot=Projectile.NewProjectile(wallOwner.GetProjectileSource_Item(wallOwner.HeldItem),new Vector2(830,350),Vector2.Zero,978,40,0,wallOwner.whoAmI);object wallRoot;var wallPlan=Known(wallOwner,Main.projectile[wallSlot],selected,out wallRoot);first=0;epoch=Main.GameUpdateCount;Advance();Require(wallPlan==null && first==0,"Volcano's real body overlap never bypasses original owner-hit wall qualification");Console.WriteLine("MELEE CHILD Volcano real owner-wall negative=1");
        }
        private static void Hands(object context)
        {
            int cases=0;foreach(int item in new[]{1166,3211,155,121})foreach(bool deny in new[]{false,true})foreach(int seed in new[]{17,29})
            {
                var p=Scene(context,item);for(int x=5;x<Main.maxTilesX-5;x++)NativeToolsChecks.Tile(x,43,1);victim=Target(3,p.Center+new Vector2(36,-4),34,1);selected=Target(2,p.Center+new Vector2(300,0));caseItem=item;blocked=deny;first=primary=bornSource=0;handPlan=null;captured=false;handContext=context;Main.rand=new Terraria.Utilities.UnifiedRandom(seed);
                try{for(int action=0;action<40 && !captured;action++)NativeCombatSelectionPreviewChecks.Step(context,p.Center+new Vector2(300,0),true);}finally{handContext=null;}
                Require(primary>0 && captured && bornSource==1,"actual Player.Update hand reaches native OnHit child birth item="+item);Advance(90);Require(deny?first==0:first>0 && handPlan!=null && first==handPlan.Tick,"actual native hand -> sealed offspring -> selected first Strike matches real Flight item="+item+" blocked="+deny+" seed="+seed+" actual="+first+" plan="+handPlan?.Tick);Console.WriteLine("MELEE CHILD native hand item="+item+" seed="+seed+" blocked="+deny+" first="+first+" plan="+handPlan?.Tick);cases++;
            }
            Console.WriteLine("MELEE CHILD actual native hand consumers="+cases+"; receiver lane selected only after sealed random birth; no future proc/input claim");
        }
        internal static void Run(object context)
        {
            // Native primary kills must keep their real bestiary/loot path.
            // The isolated content harness has not run Terraria's world loader.
            if(Main.BestiaryTracker==null)Main.BestiaryTracker=new Terraria.GameContent.Bestiary.BestiaryUnlocksTracker();
            if(Main.BestiaryDB==null){Main.BestiaryDB=new Terraria.GameContent.Bestiary.BestiaryDatabase();new Terraria.GameContent.Bestiary.BestiaryDatabaseNPCsPopulator().Populate(Main.BestiaryDB);}
            if(Main.ItemDropSolver==null){Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);}
            host=Get(context,"Combat").GetType().Assembly;model=host.GetType("JueMingR.TerrariaHost.Combat.HostMeleeHitChildAttack",true);var audit=new Harmony("JueMingR.Tests.MeleeHitChild");depth=rng=0;
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatMeleeHitChildChecks),nameof(Damage)),finalizer:new HarmonyMethod(typeof(NativeCombatMeleeHitChildChecks),nameof(DamageDone)));
            audit.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatMeleeHitChildChecks),nameof(Strike)));
            audit.Patch(typeof(Player).GetMethod("ProcessHitAgainstNPC",Flags),prefix:new HarmonyMethod(typeof(NativeCombatMeleeHitChildChecks),nameof(BeforeHit)),postfix:new HarmonyMethod(typeof(NativeCombatMeleeHitChildChecks),nameof(AfterHit)));
            audit.Patch(typeof(Player).GetMethod("ApplyNPCOnHitEffects",Flags),prefix:new HarmonyMethod(typeof(NativeCombatMeleeHitChildChecks),nameof(OnHit)));
            var birth=typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float));audit.Patch(birth,postfix:new HarmonyMethod(typeof(NativeCombatMeleeHitChildChecks),nameof(Birth)));
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            foreach(var method in typeof(Terraria.Utilities.UnifiedRandom).GetMethods(Flags).Where(m=>m.DeclaringType==typeof(Terraria.Utilities.UnifiedRandom) && (m.Name=="Next" || m.Name=="NextDouble")))audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatMeleeHitChildChecks),nameof(Random)));
            try{Motors(context);Receives(context);Hands(context);Require(rng==0,"all private native hit-child replays consume zero live RNG");Require(!(bool)Static(model,"Weapon",1320),"shared tool's actual 1111 birth never grants a new Combat hand");Console.WriteLine("PASS MELEE-HIT-CHILD RNG0");}
            finally{depth=0;handContext=null;receiving=null;victim=selected=null;Main.ProjectileUpdateLoopIndex=-1;audit.UnpatchAll(audit.Id);}
        }
    }
}
