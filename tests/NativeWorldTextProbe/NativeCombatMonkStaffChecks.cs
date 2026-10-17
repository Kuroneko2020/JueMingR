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
    internal static class NativeCombatMonkStaffChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly host;private static Type model;private static NPC target;private static int hits;
        private static void Strike(NPC __instance,int __result){if(ReferenceEquals(__instance,target) && __result>0)hits++;}
        private static Player Scene(object context,int item=3836)
        {var p=NativeCombatBallisticCandidateChecks.Scene(context,item,50);foreach(var n in Main.npc)n.active=false;for(int x=5;x<115;x++)for(int y=5;y<78;y++)Main.tile[x,y].ClearEverything();for(int x=39;x<45;x++)NativeToolsChecks.Tile(x,43,1);p.itemAnimationMax=p.itemAnimation=p.HeldItem.useAnimation;p.velocity=Vector2.Zero;p.frozen=false;Main.windPhysics=false;for(int i=0;i<Main.maxNPCs;i++)Projectile.perIDStaticNPCImmunity[699,i]=0;return p;}
        private static NPC Target(Vector2 center)
        {var n=Main.npc[2];n.SetDefaults(3);n.active=true;n.whoAmI=2;n.width=n.height=40;n.Center=center;n.life=n.lifeMax=100000;n.defense=0;n.knockBackResist=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static object Terrain(){var t=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static Projectile Copy(Projectile p){return (Projectile)host.GetType("JueMingR.TerrariaHost.Combat.HostBetsyFlameAttack").GetMethod("Copy",Flags).Invoke(null,new object[]{p});}
        private static AttackContact Known(Player p,Projectile q,NPC n,out object root)
        {var key=new NpcIdentity(1,n,n.whoAmI,n.generation,n.type,n.netID);var s=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,Active=true,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,s)).ToArray();var path=new NpcTrajectory(key,Main.GameUpdateCount,1,PredictionAssumption.None,PredictionStop.None,points,points.Length);var phase=Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");var clock=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,phase},null);var terrain=Terrain();root=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,false,true},null);using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))return (AttackContact)model.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{p,new[]{q},path,clock,terrain,root});}
        private static void UpdateAll(){for(int slot=0;slot<Main.maxProjectiles;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;try{Main.projectile[slot].Update(slot);}finally{Main.ProjectileUpdateLoopIndex=-1;}}}
        internal static void Run(object context)
        {
            host=Get(context,"Combat").GetType().Assembly;model=host.GetType("JueMingR.TerrariaHost.Combat.HostMonkStaffAttack",true);int poses=0,motors=0;
            foreach(int type in new[]{699,708})foreach(int facing in new[]{-1,1})foreach(bool frozen in new[]{false,true})
            {
                var p=Scene(context);p.direction=facing;p.frozen=frozen;int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),p.MountedCenter,new Vector2(42*facing,2),type,0,0,p.whoAmI,12*facing);var actual=Main.projectile[slot];var shadow=Copy(actual);int emit=0;
                for(int animation=p.itemAnimationMax;animation>=2;animation--)
                {
                    p.itemAnimation=animation;int before=Main.projectile.Count(q=>q.active && q.type==709);var args=new object[]{shadow,p,false};model.GetMethod("ParentAi",Flags).Invoke(null,args);actual.AI();bool born=Main.projectile.Count(q=>q.active && q.type==709)>before;if(born)emit++;Require((bool)args[2]==born && actual.active==shadow.active && (!actual.active || Vector2.DistanceSquared(actual.position,shadow.position)<.0001f && Math.Abs(actual.rotation-shadow.rotation)<.00001f),"AI142 complete native pose/frozen/three-birth clock type="+type+" facing="+facing+" anim="+animation);
                    if(actual.active)for(int dx=-120;dx<=120;dx+=30)for(int dy=-50;dy<=50;dy+=25){var box=new Rectangle((int)actual.Center.X+dx,(int)actual.Center.Y+dy,18,20);Require(actual.Colliding(actual.Hitbox,box)==(bool)model.GetMethod("Collides",Flags).Invoke(null,new object[]{shadow,new MotionRect(box.X,box.Y,box.Width,box.Height)}),"AI142 native95px shaft and24px body remain distinct colliders type="+type+" facing="+facing+" frozen="+frozen+" anim="+animation+" delta="+dx+","+dy+" actual="+actual.rotation+" model="+shadow.rotation+" position="+actual.position+"/"+shadow.position);}poses++;
                }
                Require(emit==(type==708 && !frozen?3:0),"Fury emits actual2/6/10 child bolts, frozen and other-family negatives");
            }
            foreach(int type in new[]{700,709,1110})foreach(bool retire in new[]{false,true})
            {
                var p=Scene(context);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(800,450),new Vector2(7,-1),type,0,0,p.whoAmI);var actual=Main.projectile[slot];if(retire){if(type==700)actual.ai[0]=58;else if(type==709)actual.ai[1]=28;else{actual.ai[1]=1;actual.alpha=0;}}var shadow=Copy(actual);var terrain=Terrain();
                for(int outer=0;outer<65 && actual.active;outer++)
                {
                    for(int sub=0;sub<=shadow.extraUpdates && shadow.active;sub++){bool alive=(bool)model.GetMethod("ChildAi",Flags).Invoke(null,new object[]{shadow});if(!alive){shadow.active=false;shadow.timeLeft=0;break;}if(shadow.active){var args=new object[]{shadow,terrain,false,Vector2.Zero};Require((bool)model.GetMethod("Move",Flags).Invoke(null,args),"Monk finite child environment");if((bool)args[2] || --shadow.timeLeft<=0)shadow.active=false;}motors++;}
                    NativeQuickItemChecks.BeginWorldStep();actual.Update(slot);Require(actual.active==shadow.active && (!actual.active || Vector2.DistanceSquared(actual.position,shadow.position)<.001f && Vector2.DistanceSquared(actual.velocity,shadow.velocity)<.0001f && actual.width==shadow.width && actual.height==shadow.height && actual.friendly==shadow.friendly && actual.ai[0]==shadow.ai[0] && actual.ai[1]==shadow.ai[1]),"nativeAI143/AI00130/AI077 circle fade/resize/lifetime type="+type+" outer="+outer+" actual="+actual.position+"/"+actual.velocity+" model="+shadow.position+"/"+shadow.velocity);
                }
            }
            foreach(bool lower in new[]{false,true})
            {
                var p=Scene(context,3858);var n=Target(new Vector2(850,450));if(lower)for(int i=0;i<7;i++){var q=Main.projectile[i];q.SetDefaults(1);q.owner=254;q.active=true;q.whoAmI=i;q.damage=0;q.tileCollide=false;q.Center=new Vector2(100,100);}int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(820,450),new Vector2(10,0),709,50,0,p.whoAmI);var bolt=Main.projectile[slot];bolt.ai[1]=29;if(lower)for(int i=0;i<5;i++)Main.projectile[i].active=false;object root;var contact=Known(p,bolt,n,out root);int first=0;
                for(int tick=1;tick<=10;tick++){NativeQuickItemChecks.BeginWorldStep();if(n.immune[p.whoAmI]>0)n.immune[p.whoAmI]--;UpdateAll();if(n.life<100000){first=tick;break;}}
                Require(contact!=null && contact.Tick==first && first==(lower?2:1),"Fury timed Kill circle keeps actual higher/lower slot first Damage");Require(Main.projectile.Any(q=>q.active && q.type==1110),"real709 Kill creates1110 damaging body");
            }
            foreach(int type in new[]{700,1110})
            {
                var p=Scene(context);var n=Target(new Vector2(850,450));int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(820,450),new Vector2(6,0),type,50,0,p.whoAmI);var q=Main.projectile[slot];object root;var contact=Known(p,q,n,out root);NativeQuickItemChecks.BeginWorldStep();q.Update(slot);Require(contact!=null && contact.Tick==1 && n.life<100000,"observed ghost and circle retain real Receive type="+type);
            }
            int ghostUseful=0,ghostMiss=0;var oldRandom=Main.rand;
            try
            {
                for(int seed=0;seed<16;seed++)
                {
                    var p=Scene(context);var n=Target(new Vector2(1000,666));var front=Main.npc[1];front.SetDefaults(3);front.active=true;front.whoAmI=1;front.width=front.height=40;front.Center=new Vector2(700,666);front.life=front.lifeMax=100000;front.defense=0;front.noGravity=front.noTileCollide=true;front.aiStyle=-1;front.knockBackResist=0;Array.Clear(front.immune,0,front.immune.Length);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),p.MountedCenter,new Vector2(42,0),699,60,0,p.whoAmI,15.75f);var parent=Main.projectile[slot];object root;var contact=Known(p,parent,n,out root);Require(contact!=null && contact.Confidence==AttackConfidence.Representative,"prior receiver's actual OnHit yields a lawful ghost contact beyond the finite parent reach");Require((bool)Get(root,"Unchanged"),"observed Monk family/owner lease starts current");p.frozen=true;Require(!(bool)Get(root,"Unchanged"),"same-GUC frozen changes the anchored native pose/birth stage");p.frozen=false;Main.rand=new Terraria.Utilities.UnifiedRandom(seed);bool born=false;
                    for(int tick=1;tick<=90;tick++){NativeQuickItemChecks.BeginWorldStep();p.itemAnimation=Math.Max(0,p.itemAnimation-1);if(front.immune[p.whoAmI]>0)front.immune[p.whoAmI]--;if(n.immune[p.whoAmI]>0)n.immune[p.whoAmI]--;UpdateAll();born|=Main.projectile.Any(q=>q.active && q.type==700);if(n.life<100000)break;}
                    Require(front.life<100000 && born && parent.localAI[0]>900,"actual earlier receiver, one-shot1000clock and native ghost birth are retained for every seed");if(n.life<100000)ghostUseful++;else ghostMiss++;
                }
                Require(ghostUseful>0,"actual original Ghastly OnHit ghost, independently of parent reach, receives the selected NPC in recorded lawful random outcomes");
            }
            finally{Main.rand=oldRandom;}
            var audit=new Harmony("JueMingR.Tests.MonkStaffInput");audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatMonkStaffChecks),nameof(Strike)));
            try
            {
                foreach(int weapon in new[]{3836,3858})
                {
                    var p=NativeCombatBallisticCandidateChecks.Scene(context,weapon,50);if(weapon==3858)p.altFunctionUse=2;p.itemAnimation=p.itemAnimationMax=p.HeldItem.useAnimation;target=Main.npc[2];hits=0;var observation=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,false,true,50,true,true));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),target.Center-new Vector2(500,0),true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,target,0);Require(GetOptional(attack,"ExpectedImpact")!=null,"Monk full hand family has a useful actual opportunity item="+weapon);var captured=GetOptional(attack,"obstacles");Require(captured!=null && (bool)Get(captured,"Unchanged"),"publicMonk Source returns the current complete family root");p.frozen=true;Require(!(bool)Get(captured,"Unchanged") && !(bool)Get(captured,"BirthInputsCurrent"),"same-GUC frozen revokes the actual publicMonk owner/birth dependency");p.frozen=false;
                    typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});Require((bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced","native Monk item consumes prepared source");var parent=Main.projectile.Single(q=>q.active && (q.type==699 || q.type==708));Require(weapon==3836?parent.type==699:parent.type==708 && parent.damage==(int)(p.GetWeaponDamage(p.HeldItem)*.5f),"Fury alternate has its real708 and half weapon damage, distinct from707 control source");
                    for(int tick=1;tick<=120 && hits==0;tick++){NativeQuickItemChecks.BeginWorldStep();p.itemAnimation=Math.Max(0,p.itemAnimation-1);UpdateAll();}Require(hits>0,"native actual finite Monk pose and derived family receive useful Damage item="+weapon);
                }
            }
            finally{audit.UnpatchAll(audit.Id);target=null;}Console.WriteLine("MONK-STAFF poseActions="+poses+" childActions="+motors+" circleSlotDamage=2 observedReceive=2 trueHands=2 ghost16="+ghostUseful+"/"+ghostMiss);
        }
    }
}
