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
    internal static class NativeCombatGrassCurveChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly host;private static Type model;private static int depth,rng,bodyPrepared,bodyStrikes,damageType,bodyItem;private static bool calibrating;private static Rectangle firstBox;private static NPC bodyTarget;
        private static void Shape(Player __instance,Item __0,Rectangle __1){if(calibrating && __0.type==bodyItem && firstBox==Rectangle.Empty)firstBox=__1;}
        private static void Observed(object __instance,Player player,Item item)
        {
            if(bodyTarget==null || item.type!=bodyItem || player.itemAnimation<=0)return;
            // The real ObserveSwing just sealed this native frame. Its normal
            // cold budget failure remains recorded in r04. Replay preparation
            // at that SAME real pose in an explicitly finite component scope;
            // this proves the hand consumer, not ordinary preparation latency.
            var observation=Get(__instance,"observation");var cache=Get(Get(observation,"Prediction"),"Cache");var path=Call(cache,"Read",1);
            Require((bool)Get(__instance,"Permission") && path!=null,"actual body hand has real input permission and a selected prediction path");
            using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))Call(__instance,"PrepareSwing",player,item,path,Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc"),true);
            if(GetOptional(__instance,"current") is AttackContact contact && contact.Timeline.Identity.Slot==bodyTarget.whoAmI && GetOptional(__instance,"ammo")==null)bodyPrepared++;
        }        private static void Damage(Projectile __instance){damageType=__instance.type;}private static void DamageDone(){damageType=0;}
        private static void Strike(NPC __instance,int __result){if(ReferenceEquals(bodyTarget,__instance) && __result>0 && damageType==0)bodyStrikes++;}
        private static void Random(){if(depth>0)rng++;}
        private static Player Scene(object context)
        {var p=NativeCombatBallisticCandidateChecks.Scene(context,190,50);NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());foreach(var n in Main.npc)n.active=false;Main.windPhysics=false;for(int x=5;x<115;x++)for(int y=5;y<78;y++)Main.tile[x,y].ClearEverything();p.gravDir=1;p.direction=1;return p;}
        private static NPC Target(int slot,Vector2 center,bool blocked=false)
        {var n=Main.npc[slot];n.SetDefaults(3);n.active=true;n.whoAmI=slot;n.width=n.height=40;n.Center=center;n.life=n.lifeMax=100000;n.defense=0;n.knockBackResist=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.dontTakeDamage=blocked;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static object Terrain(){var t=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static Projectile Copy(Projectile p){depth++;try{return (Projectile)host.GetType("JueMingR.TerrariaHost.Combat.HostNoteAttack").GetMethod("Copy",Flags).Invoke(null,new object[]{p});}finally{depth--;}}
        private static AttackContact Known(Player p,Projectile q,NPC n,out object root)
        {var key=new NpcIdentity(1,n,n.whoAmI,n.generation,n.type,n.netID);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,Active=true,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();var path=new NpcTrajectory(key,Main.GameUpdateCount,1,PredictionAssumption.None,PredictionStop.None,points,points.Length);var phase=Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");var clock=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,phase},null);root=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,false,true},null);depth++;try{using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))return (AttackContact)model.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{p,new[]{q},path,clock,Terrain(),root});}finally{depth--;}}
        internal static void Run(object context)
        {
            host=Get(context,"Combat").GetType().Assembly;model=host.GetType("JueMingR.TerrariaHost.Combat.HostGrassCurveAttack",true);var audit=new Harmony("JueMingR.Tests.GrassCurve");
            foreach(var method in typeof(Terraria.Utilities.UnifiedRandom).GetMethods(Flags).Where(m=>m.DeclaringType==typeof(Terraria.Utilities.UnifiedRandom) && (m.Name=="Next" || m.Name=="NextDouble")))audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatGrassCurveChecks),nameof(Random)));
            int motors=0,actions=0,births=0,useful=0;var originalRandom=Main.rand;
            try
            {
                foreach(float curve in new[]{-.14f,.06f,.16f})foreach(int scene in new[]{0,1,2,3,4}.Where(v=>string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JMR_GRASS_MOTOR_SCENE")) || v.ToString()==Environment.GetEnvironmentVariable("JMR_GRASS_MOTOR_SCENE")))
                {
                    var p=Scene(context);if(scene>=1 && scene<=3)for(int x=25;x<105;x++)for(int y=10;y<75;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:1);}
                    int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(800,450),Vector2.UnitX*16,976,0,0,p.whoAmI,curve,12);var actual=Main.projectile[slot];if(scene==4)actual.shimmerWet=true;var shadow=Copy(actual);var terrain=Terrain();
                    for(int tick=1;tick<=60 && actual.active;tick++)
                    {
                        depth++;try{Require((bool)model.GetMethod("Step",Flags).Invoke(null,new object[]{shadow,terrain}),"Grass actual liquid/world motor is captured");}finally{depth--;}if(shadow.active && --shadow.timeLeft<=0){shadow.active=false;shadow.timeLeft=0;}NativeQuickItemChecks.BeginWorldStep();actual.Update(slot);
                        Require(actual.active==shadow.active && (!actual.active || Vector2.DistanceSquared(actual.position,shadow.position)<.002f && Vector2.DistanceSquared(actual.velocity,shadow.velocity)<.0001f && actual.timeLeft==shadow.timeLeft),"native976 full60-update curve/envelope/lava/shimmer agrees curve="+curve+" scene="+scene+" tick="+tick+" actual="+actual.position+"/"+actual.velocity+" model="+shadow.position+"/"+shadow.velocity);actions++;
                    }
                    motors++;
                }
                foreach(bool blocked in new[]{false,true})
                {
                    var p=Scene(context);var front=Target(1,new Vector2(800,450));var n=Target(2,new Vector2(920,450),blocked);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(700,450),Vector2.UnitX*16,976,40,0,p.whoAmI,0,60);var q=Main.projectile[slot];object root;var contact=Known(p,q,n,out root);Require((bool)Get(root,"Unchanged"),"Grass captured family starts current");q.ai[1]--;Require(!(bool)Get(root,"Unchanged"),"same-GUC native bend clock revokes the actual curve family");q.ai[1]++;int first=0;
                    for(int tick=1;tick<=60 && q.active;tick++){NativeQuickItemChecks.BeginWorldStep();q.Update(slot);if(n.life<100000 && first==0)first=tick;}
                    Require(front.life<100000 && (blocked?contact==null && first==0:contact!=null && contact.Tick==first && first>0),"native penetration2 crosses a prior receiver with permanent per-body local immunity and invulnerable negative");
                }
                var chooser=Scene(context);var searchPoint=new Vector2(800,450);var a=Target(1,searchPoint+Vector2.UnitX*100);var b=Target(2,searchPoint-Vector2.UnitX*100);for(int y=10;y<65;y++)NativeToolsChecks.Tile(53,y,1);var query=typeof(Player).GetMethod("GetZenithTarget",Flags);var queryArgs=new object[]{searchPoint,150f,0};Require((bool)query.Invoke(chooser,queryArgs) && (int)queryArgs[2]==1,"native near-neighbor query keeps lowest slot on a distance tie even behind a wall");a.active=b.active=false;var edge=Target(3,searchPoint+Vector2.UnitX*150);queryArgs=new object[]{searchPoint,150f,0};Require(!(bool)query.Invoke(chooser,queryArgs),"native150px search is strict, not an arbitrary distant selected receiver");edge.Center=searchPoint+Vector2.UnitX*149;queryArgs=new object[]{searchPoint,150f,0};Require((bool)query.Invoke(chooser,queryArgs) && (int)queryArgs[2]==3,"native just-inside nearest body remains a legal autonomous birth target");
                foreach(int facing in new[]{-1,1})foreach(int gravity in new[]{-1,1})foreach(bool nearby in new[]{false,true})foreach(int seed in new[]{1,17})
                {
                    var p=Scene(context);p.gravDir=gravity;p.direction=-facing;Vector2 search=p.MountedCenter+new Vector2(70*facing,-40*gravity);NPC n=nearby?Target(2,search):null;var raw=p.Center+new Vector2(300*facing,0);NativeToolExecutionChecks.Sample(context,Get(context,"Input"),raw,true);Call(Get(context,"Combat"),"Sample");int mouseX=Main.mouseX,mouseY=Main.mouseY;Main.rand=new Terraria.Utilities.UnifiedRandom(seed);
                    typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var q=Main.projectile.Single(x=>x.active && x.type==976);float magnitude=Math.Abs(q.ai[0]);Require(p.direction==facing && Math.Sign(q.ai[0])==-facing*gravity && magnitude>=MathHelper.Pi/60-.00001f && magnitude<.17f && Math.Abs(q.velocity.Length()-16)<.001f && q.damage==(int)(p.GetWeaponDamage(p.HeldItem)*.25),"native Grass uses common cursor facing plus gravity/quarterdamage/legal random curve, not its Shoot mouse velocity");
                    float originY=p.Center.Y+p.height/2*gravity-(nearby?0:24*gravity);Require(Math.Abs(q.Center.X-p.Center.X)<=p.width/2+.001f && Math.Abs(q.Center.Y-originY)<.001f && q.ai[1]>=0 && q.ai[1]<=60 && Main.mouseX==mouseX && Main.mouseY==mouseY,"actual nearest-target/no-target branch preserves real native birth support and physical cursor");
                    var attack=Get(Get(context,"Combat"),"Attack");Require(!(bool)Get(attack,"hasConsumedInput") || Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))!="Enhanced","a native nearby random birth does not manufacture distant enhanced launch control");
                    if(nearby){object root;var expected=Known(p,q,n,out root);int first=0;for(int tick=1;tick<=60 && q.active;tick++){NativeQuickItemChecks.BeginWorldStep();q.Update(q.whoAmI);if(n.life<100000 && first==0)first=tick;}Require(expected==null?first==0:first==expected.Tick,"actual random born curve has exact sealed first Receive, including genuine random misses");if(first>0)useful++;}births++;
                }
                var owner=Scene(context);Require((bool)host.GetType("JueMingR.TerrariaHost.Combat.HostSwingAttack").GetMethod("Handles",Flags).Invoke(null,new object[]{owner.HeldItem}),"Blade of Grass retains its ordinary native melee hand alongside the autonomous curve");Require(!(bool)host.GetType("JueMingR.TerrariaHost.Combat.HostSwingAttack").GetMethod("Mixed",Flags).Invoke(null,new object[]{owner.HeldItem}),"native autonomous Grass birth does not suppress ordinary body observation as a fictitious mixed-speed shot");Require(useful>0 && rng==0,"native nearby Grass births have useful real Damage and all private sealed copies/replays consume no live RNG");Body(context,audit);
            }
            finally{depth=0;Main.rand=originalRandom;audit.UnpatchAll(audit.Id);}Console.WriteLine("GRASS-CURVE motors="+motors+" actions="+actions+" trueBirths="+births+" usefulNative="+useful+" receiveEdges=2 rng=0");
        }
        private static void Body(object context,Harmony audit)
        {
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(Player).GetMethod("ItemCheck_MeleeHitNPCs",Flags),prefix:new HarmonyMethod(typeof(NativeCombatGrassCurveChecks),nameof(Shape)));
            audit.Patch(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackAim").GetMethod("ObserveSwing",Flags),postfix:new HarmonyMethod(typeof(NativeCombatGrassCurveChecks),nameof(Observed)));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatGrassCurveChecks),nameof(Damage)),postfix:new HarmonyMethod(typeof(NativeCombatGrassCurveChecks),nameof(DamageDone)));
            audit.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatGrassCurveChecks),nameof(Strike)));
            foreach(int item in new[]{190,46}){bodyItem=item;var p=NativeCombatBallisticCandidateChecks.Scene(context,item,52);var n=Main.npc[2];for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,n.Center,false,true);firstBox=Rectangle.Empty;calibrating=true;try{NativeCombatSelectionPreviewChecks.Step(context,n.Center,true);}finally{calibrating=false;}
            Require(firstBox.Width>0,"native Grass actual hand supplies its real melee shape");float closeX=p.position.X+n.Hitbox.Left-firstBox.Right+1;
            p=NativeCombatBallisticCandidateChecks.Scene(context,item,52);p.position.X=closeX;bodyTarget=Main.npc[2];bodyPrepared=bodyStrikes=damageType=0;NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions(false,true,false,false,true,50,true,true));
            try{for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,bodyTarget.Center,false);for(int tick=0;tick<70 && (bodyPrepared==0 || bodyStrikes==0);tick++)NativeCombatSelectionPreviewChecks.Step(context,bodyTarget.Center-new Vector2(430,0),tick<12);Require(bodyPrepared>0 && bodyStrikes>0,"real registered Grass ObserveSwing prepares the actual body-hand contact (ammo null) and original melee, independently of976, strikes the receiver prepared="+bodyPrepared+" strikes="+bodyStrikes+" closeX="+closeX+" calibrated="+firstBox+" item="+item);Console.WriteLine("AUTONOMOUS-BIRTH actual body item="+item+" scopeMs=1000 preparation="+bodyPrepared+" nativeMeleeStrikes="+bodyStrikes);}
            finally{bodyTarget=null;damageType=0;}}
        }
    }
}
