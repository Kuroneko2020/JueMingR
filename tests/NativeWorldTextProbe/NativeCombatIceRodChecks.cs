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
    internal static class NativeCombatIceRodChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static object activeContext;
        private static NPC target;private static Projectile receiving;private static bool observe,natural,auditMotor;
        private static int births,kills,motors,positive,solves,steps,restored;private static long first,birthTick,firstBirth;
        private static AttackContact bornPrediction;private static Vector2 raw;
        private static int naturalAction=-1,assistedBirths,manualBirths;private static bool physicalLeft;private static AttackContact shootExpected;
        private static readonly HashSet<ProjectileKey> keys=new HashSet<ProjectileKey>();
        private sealed class PlacementSnapshot
        {
            internal Rectangle[] Bodies;internal int Queries;
            internal bool Query(int x,int y,out bool placed)
            {Queries++;var box=new Rectangle(x*16,y*16,16,16);placed=!Bodies.Any(b=>b.Intersects(box));return true;}
            internal static PlacementSnapshot Capture()
            {
                var rows=new List<Rectangle>();foreach(var p in Main.player)if(p!=null && p.active && !p.dead && !p.ghost)rows.Add(p.Hitbox);
                foreach(var n in Main.npc)if(n!=null && n.active)rows.Add(n.Hitbox);return new PlacementSnapshot{Bodies=rows.ToArray()};
            }
        }
        private sealed class MotorInput {internal Projectile Expected;internal object Terrain;internal bool Compared;}
        private static object Static(Type t,string name,params object[] args){return t.GetMethod(name,Flags).Invoke(null,args);}
        private static object Terrain(){var t=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static Projectile Copy(Projectile p){return (Projectile)Static(model,"Copy",p);}
        private static object Placement(PlacementSnapshot values)
        {return Delegate.CreateDelegate(model.GetNestedType("Placement",Flags),values,typeof(PlacementSnapshot).GetMethod("Query",Flags));}
        private static bool Step(Projectile p,object terrain,PlacementSnapshot values,out bool changed)
        {var args=new object[]{p,terrain,Placement(values),false};bool valid=(bool)Static(model,"Step",args);changed=(bool)args[3];return valid;}
        private static void Finish(Projectile p,object terrain)
        {var args=new object[]{p,terrain,false};Require((bool)Static(model,"Finish",args),"known ice lifetime/Kill cells are available");}
        private static bool Same(Projectile a,Projectile b)
        {return a.active==b.active && a.position==b.position && a.velocity==b.velocity && a.ai.SequenceEqual(b.ai) && a.rotation==b.rotation && a.alpha==b.alpha && a.damage==b.damage && a.direction==b.direction && a.wet==b.wet && a.lavaWet==b.lavaWet && a.honeyWet==b.honeyWet && a.shimmerWet==b.shimmerWet && a.wetCount==b.wetCount;}
        private static bool placementMoment;
        private static MotionRect placementOwner;
        private static int placementMomentQueries;
        private static void PlacementMoment(int x,int y,int outer,MotionRect ownerRawBox)
        {
            if(!placementMoment || placementMomentQueries!=0 || outer!=1 || x!=50 || y!=41)return;
            Require(ownerRawBox.Equals(placementOwner),"first original projectile placement uses the once-completed Player action, not another continuation: expected="+placementOwner.X.ToString("R")+","+placementOwner.Y.ToString("R")+" actual="+ownerRawBox.X.ToString("R")+","+ownerRawBox.Y.ToString("R"));
            Require(!Collision.EmptyTile(x,y),"first native placement consumer is still occupied by the completed player pose");placementMomentQueries++;
        }
        private static void SolveWork(){solves++;}private static void StepWork(){steps++;}
        private static void BeforeKill(Projectile __instance){if(observe && __instance.type==80)kills++;}
        private static void BeforeDamage(Projectile __instance)
        {receiving=__instance;var input=current;if(input!=null && input.Expected.whoAmI==__instance.whoAmI){Compare(__instance,input,false);input.Compared=true;}}
        private static void AfterDamage(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {if(!observe || receiving==null || receiving.type!=80 || !keys.Contains(receiving.key) || __result<=0)return;if(ReferenceEquals(__instance,target)){positive++;if(first<0)first=Main.GameUpdateCount;}}
        private static MotorInput current;
        private static void BeforeUpdate(Projectile __instance,out MotorInput __state)
        {
            __state=null;if(!auditMotor || __instance.type!=80)return;var q=Copy(__instance);var terrain=Terrain();bool changed;
            Require(Step(q,terrain,PlacementSnapshot.Capture(),out changed),"current original ice action has complete pure placement/collision input");
            __state=new MotorInput{Expected=q,Terrain=terrain};current=__state;
        }
        private static void AfterUpdate(Projectile __instance,MotorInput __state)
        {if(__state!=null && !__state.Compared)Compare(__instance,__state,true);current=null;}
        private static void Compare(Projectile actual,MotorInput input,bool finish)
        {
            if(finish)Finish(input.Expected,input.Terrain);var q=input.Expected;
            Require(Same(actual,q),"native Ice22 current input matches pure action at real Damage/retirement slot="+actual.whoAmI+" P="+actual.position+"/"+q.position+" V="+actual.velocity+"/"+q.velocity+" AI="+string.Join(",",actual.ai)+"/"+string.Join(",",q.ai)+" alpha="+actual.alpha+"/"+q.alpha+" damage="+actual.damage+"/"+q.damage+" active="+actual.active+"/"+q.active+" rotation="+actual.rotation.ToString("R")+"/"+q.rotation.ToString("R"));
            if(finish)Require(actual.timeLeft==q.timeLeft,"native ice retirement lifetime agrees");motors++;
        }
        private static void ToolsPredicate(object context,string where)
        {
            try
            {
                var tools=Get(context,"Tools");var world=Get(Get(tools,"Items"),"World");var p=Main.LocalPlayer;
                Console.WriteLine("ICE tools-predicate "+where+" available="+Get(tools,"Available")+" runtime="+Get(Get(tools,"Runtime"),"IsSessionActive")+" capturedPlayerSame="+ReferenceEquals(p,Get(world,"sessionPlayer"))+" sessionPlayerSame="+ReferenceEquals(p,GetOptional(world,"SessionPlayer"))+" toolsPlayerSame="+ReferenceEquals(p,GetOptional(tools,"Player"))+" local="+p.whoAmI+" active/dead/ghost="+p.active+"/"+p.dead+"/"+p.ghost+" menu/server/net="+Main.gameMenu+"/"+Main.dedServ+"/"+Main.netMode+" HP="+p.statLife+" P="+p.position+" V="+p.velocity);
                var settings=(Array)Get(tools,"Settings");for(int i=0;i<settings.Length;i++){var value=settings.GetValue(i);Console.WriteLine("ICE tools-setting index="+i+" loaded/busy/protected="+Get(value,"Loaded")+"/"+Get(value,"Busy")+"/"+Get(value,"Protected"));}
            }
            catch(Exception error){Console.WriteLine("ICE predicate diagnostic unavailable: "+error);}
        }
        private static Player Reset(object context)
        {
            observe=natural=auditMotor=false;keys.Clear();target=null;receiving=null;current=null;first=firstBirth=-1;births=kills=motors=positive=restored=0;bornPrediction=null;assistedBirths=manualBirths=0;naturalAction=-1;physicalLeft=false;shootExpected=null;
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            Player p;try{p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),496,0,0);}catch{ToolsPredicate(context,"Reset catch");throw;}p.position=new Vector2(650,646);p.velocity=Vector2.Zero;p.ResetFallDamage();p.fullRotation=0;p.gfxOffY=0;p.releaseUseItem=true;p.statMana=p.statManaMax=p.statManaMax2=400;Main.windPhysics=false;
            Main.screenPosition=new Vector2(500,400);if(Main.GameUpdateCount==0)NativeQuickItemChecks.BeginWorldStep();return p;
        }
        private static NPC Target(int slot,Vector2 center,bool friendly=false)
        {typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.knockBackResist=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.friendly=friendly;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static NpcTrajectory Timeline(NPC n)
        {var id=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var s=new NpcMotionState{Identity=id,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=n.noTileCollide,Wet=n.wet,ReflectsProjectiles=n.reflectsProjectiles};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,s)).ToArray();return new NpcTrajectory(id,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);}
        private static object Clock(NpcTrajectory path)
        {return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc"),0},null);}
        private static object Obstacles(Player p,Projectile q,NpcTrajectory path)
        {return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,true,false},null);}
        private static object Frame(Player p,NpcTrajectory path)
        {return Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPlayerFrame",true),"Capture",p,path);}
        private static AttackContact Known(Player p,Projectile q,NpcTrajectory path)
        {return (AttackContact)Static(model,"Known",p,new[]{q},path,Clock(path),Terrain(),Obstacles(p,q,path),Frame(p,path));}
        private static int oldMouseX,oldMouseY,oldTileX,oldTileY;private static long shootTick;
        private static void BeforeShoot(Player __instance)
        {if(!natural || !ReferenceEquals(__instance,Main.LocalPlayer))return;oldMouseX=Main.mouseX;oldMouseY=Main.mouseY;oldTileX=Player.tileTargetX;oldTileY=Player.tileTargetY;shootTick=Main.GameUpdateCount;shootExpected=(AttackContact)GetOptional(Get(Get(activeContext,"Combat"),"Attack"),"ExpectedPreview");Console.WriteLine("ICE shoot-entry "+NaturalFacts(__instance)+" physicalMouse="+oldMouseX+","+oldMouseY+" physicalTile="+oldTileX+","+oldTileY+" expected="+shootExpected?.Tick);}
        private static void AfterShoot(Player __instance)
        {if(!natural || !ReferenceEquals(__instance,Main.LocalPlayer))return;Require(Main.mouseX==oldMouseX && Main.mouseY==oldMouseY && Player.tileTargetX==oldTileX && Player.tileTargetY==oldTileY,"real consumer returns both physical cursor and paired tileTarget coordinates");restored++;}
        private static string NaturalFacts(Player p)
        {
            var combat=Get(activeContext,"Combat");var attack=Get(combat,"Attack");var preview=GetOptional(attack,"preview");var path=preview==null?null:(NpcTrajectory)GetOptional(preview,"Timeline");
            return "action="+naturalAction+" tick="+Main.GameUpdateCount+" physicalLeft/combatLeft/use="+physicalLeft+"/"+Get(combat,"Left")+"/"+p.controlUseItem+" anim/time/reuse="+p.itemAnimation+"/"+p.itemTime+"/"+p.reuseDelay+" justStarted="+p.ItemAnimationJustStarted+" mana="+p.statMana+" expected="+((AttackContact)GetOptional(attack,"ExpectedPreview"))?.Tick+" current/next="+((AttackContact)GetOptional(attack,"current"))?.Tick+"/"+((AttackContact)GetOptional(attack,"next"))?.Tick+" prepared="+GetOptional(attack,"prepared")+" path="+(path==null?"null":path.Identity.Type+"/"+path.Identity.Slot+":"+path.Count+"/"+path.Stop+"@"+path.SampleTick);
        }
        private static void Born(IEntitySource __0,int __result)
        {
            if(!observe || __result<0 || __result>=Main.maxProjectiles)return;var q=Main.projectile[__result];if(q.type!=80)return;keys.Add(q.key);births++;birthTick=Main.GameUpdateCount;
            if(!natural)return;var p=Main.LocalPlayer;var cursor=new Vector2(Main.mouseX+Main.screenPosition.X,p.gravDir==-1?Main.screenPosition.Y+Main.screenHeight-Main.mouseY:Main.mouseY+Main.screenPosition.Y);
            var scopeType=assembly.GetType("JueMingR.TerrariaHost.Combat.CombatCursorScope",true);var scope=scopeType.GetField("owner",Flags).GetValue(null);
            bool borrowed=scope!=null && !(bool)Get(scope,"ended") && (bool)Get(scope,"tileTarget");
            Console.WriteLine("ICE actual birth "+NaturalFacts(p)+" slot="+q.whoAmI+" P="+q.position+" V="+q.velocity+" tile="+q.ai[0]+","+q.ai[1]+" cursor="+cursor+" Borrow="+borrowed+" scopeMouse="+(scope==null?"null":Get(scope,"ownX")+","+Get(scope,"ownY"))+" scopeTile="+(scope==null?"null":Get(scope,"ownTileX")+","+Get(scope,"ownTileY"))+" entryExpected="+shootExpected?.Tick+" old/currentReceiptMouse="+oldMouseX+","+oldMouseY+"/"+Main.mouseX+","+Main.mouseY+" old/currentReceiptTile="+oldTileX+","+oldTileY+"/"+Player.tileTargetX+","+Player.tileTargetY);
            Require(shootTick==Main.GameUpdateCount && q.ai[0]==Player.tileTargetX && q.ai[1]==Player.tileTargetY,"actual Ice Rod birth consumes this original Shoot cursor and tileTarget pair");
            var ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);
            var expected=(Projectile)Static(model,"Birth",p,ammo,p.RotatedRelativePoint(p.MountedCenter),cursor,Terrain());
            Require(expected!=null && expected.Center==q.Center && Vector2.DistanceSquared(expected.velocity,q.velocity)<.0001f && expected.ai.SequenceEqual(q.ai),"actual birth origin/velocity/tile pair matches the exact cursor at its native Shoot consumer, not only a directional dot");
            if(borrowed)
            {
                Require(Main.mouseX==(int)Get(scope,"ownX") && Main.mouseY==(int)Get(scope,"ownY") && Player.tileTargetX==(int)Get(scope,"ownTileX") && Player.tileTargetY==(int)Get(scope,"ownTileY"),"active borrowed receipt owns both native input pairs");
                Require(Vector2.Dot(q.velocity,target.Center-p.MountedCenter)>0 && Vector2.Dot(q.velocity,raw-p.MountedCenter)<0,"native assisted ice birth is toward selected receiver, opposite physical cursor");assistedBirths++;
            }
            else
            {
                // A release can leave an original animation's pending Shoot.
                // It consumes physical input without an admitted new receipt.
                Require(Main.mouseX==oldMouseX && Main.mouseY==oldMouseY && Player.tileTargetX==oldTileX && Player.tileTargetY==oldTileY,"unborrowed original pending use retains the actual physical consumer pair");
                Require(!physicalLeft && !(bool)Get(Get(activeContext,"Combat"),"Left"),"a feasible held use cannot silently become an unassisted physical birth");manualBirths++;
            }
            var path=Timeline(target);var known=Known(p,q,path);if(births==1){Require(borrowed,"first authorized native use actually borrows the paired input receipt");bornPrediction=known;firstBirth=Main.GameUpdateCount;}
            Console.WriteLine("ICE birth outcome borrowed="+borrowed+" known="+known?.Tick);
        }
        internal static void Run(object context)
        {
            activeContext=context;assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostIceRodAttack",true);var audit=new Harmony("JueMingR.Tests.IceRod");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true).GetMethod("TryIcePlacement",Flags),prefix:new HarmonyMethod(typeof(NativeCombatIceRodChecks),nameof(PlacementMoment)));
            audit.Patch(model.GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatIceRodChecks),nameof(SolveWork)));audit.Patch(model.GetMethod("Motor",Flags),prefix:new HarmonyMethod(typeof(NativeCombatIceRodChecks),nameof(StepWork)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatIceRodChecks),nameof(Born)));
            audit.Patch(typeof(Projectile).GetMethod("Update",Flags),prefix:new HarmonyMethod(typeof(NativeCombatIceRodChecks),nameof(BeforeUpdate)),postfix:new HarmonyMethod(typeof(NativeCombatIceRodChecks),nameof(AfterUpdate)));
            audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatIceRodChecks),nameof(BeforeKill)));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatIceRodChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatIceRodChecks),nameof(AfterDamage)));
            audit.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatIceRodChecks),nameof(Strike)));
            audit.Patch(typeof(Player).GetMethod("ItemCheck_Shoot",Flags),prefix:new HarmonyMethod(typeof(NativeCombatIceRodChecks),nameof(BeforeShoot)){priority=Priority.First},postfix:new HarmonyMethod(typeof(NativeCombatIceRodChecks),nameof(AfterShoot)){priority=Priority.Last});
            try
            {
                string leaf=Environment.GetEnvironmentVariable("JMR_ICE_ROD_LEAF")??"";Require(leaf=="" || leaf=="motor" || leaf=="receive" || leaf=="natural","valid Ice Rod leaf selector");
                string motorPart=Environment.GetEnvironmentVariable("JMR_ICE_ROD_MOTOR_PART")??"";Require(motorPart=="" || motorPart=="remaining" || motorPart=="time-reset" || motorPart=="foreign" || motorPart=="shared-reads","valid Ice Rod motor part");Require(motorPart=="" || leaf=="motor","motor part selector applies only to motor leaf");
                if(leaf=="" || leaf=="motor")
                {
                    Console.WriteLine("ICE motor actual part="+(motorPart==""?"birth,timing,remaining":motorPart=="remaining"?"remaining (birth/timing reused from R74)":motorPart=="foreign"?"foreign only (R76 valid remaining prefix reused)":motorPart=="shared-reads"?"shared-reads only (R76 valid remaining prefix reused)":"time-reset (timing2 then original Reset only; birth/actions excluded)")+"; prior R74 whole-leaf exit1 remains unchanged");
                    if(motorPart=="foreign"){Console.WriteLine("ICE motor actual narrow subset=foreign current action only; prior20/occupancy4/lease6/order3 reused from R76");ForeignFamilyPlacement(context);}
                    else if(motorPart=="shared-reads"){Console.WriteLine("ICE motor actual narrow subset=source proof/hot guard/far placement to Strike only; other R76 green classes reused");SharedReads(context);}
                    else if(motorPart=="time-reset")
                    {
                        LaunchPlacementMoment(context);ToolsPredicate(context,"before immediate Reset");Reset(context);ToolsPredicate(context,"after immediate Reset");Console.WriteLine("PASS ICE time-reset original cross-scenario reset after actual timing2");
                    }
                    else{if(motorPart==""){Births(context);LaunchPlacementMoment(context);}Motors(context);}
                }if(leaf=="" || leaf=="receive")Receive(context);if(leaf=="" || leaf=="natural")Natural(context);
            }
            finally{audit.UnpatchAll("JueMingR.Tests.IceRod");Main.windPhysics=wind;observe=natural=auditMotor=false;current=null;activeContext=null;placementMoment=false;NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
        private static void Births(object context)
        {
            int cases=0;foreach(float gravity in new[]{1f,-1f})
            {
                var p=Reset(context);p.position=new Vector2(650.25f,600.5f);p.gravDir=gravity;p.itemAnimationMax=9;p.itemAnimation=p.itemTime=8;Main.screenPosition=new Vector2(500.25f,400.5f);Main.screenHeight=600;Main.mouseX=300;Main.mouseY=200;Player.tileTargetX=37;Player.tileTargetY=38;
                var aim=new Vector2(910.25f,620.75f);var cursorType=assembly.GetType("JueMingR.TerrariaHost.Combat.CombatCursorScope",true);var scope=Static(cursorType,"BeginTileTarget",aim,true);var ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);var origin=p.RotatedRelativePoint(p.MountedCenter);var expected=(Projectile)Static(model,"Birth",p,ammo,origin,aim,Terrain());
                try{typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});}finally{Call(scope,"End");}
                var q=Main.projectile.Single(s=>s.active && s.type==80);Require(q.ai.SequenceEqual(expected.ai) && q.Center==expected.Center && Vector2.DistanceSquared(q.velocity,expected.velocity)<.0001f && q.damage==expected.damage,"actual scoped normal/reverse Ice Rod birth has matching cursor, tile pair, origin and velocity");Require(Main.mouseX==300 && Main.mouseY==200 && Player.tileTargetX==37 && Player.tileTargetY==38,"paired tile/cursor receipt restores exact original integers");cases++;
            }
            Console.WriteLine("PASS ICE original births="+cases+" pairedTileRestore=True");
        }
        private static void LaunchPlacementMoment(object context)
        {
            // Observe the real Player.Update first, then the original AI22
            // placement. The second real player action crosses the tile edge,
            // making an accidental repeated continuation observably different.
            // Y646 is above the real floor688 and inside native BordersMovement;
            // tile41 lies inside the owner body. R73 y600 was clamped to640.
            int cases=0;
            foreach(bool beforeNpc in new[]{true,false})
            {
                var p=Reset(context);p.position=new Vector2(787,646);p.velocity=new Vector2(-4.5f,0);p.controlLeft=true;p.controlRight=p.controlUseItem=false;p.ResetFallDamage();
                target=Target(2,new Vector2(1000,664));NativeQuickItemChecks.BeginWorldStep();p.Update(0);
                Require(!Collision.EmptyTile(50,41),"native first moving Player action still occupies placement tile: P="+p.position.X.ToString("R")+","+p.position.Y.ToString("R")+" V="+p.velocity.X.ToString("R")+","+p.velocity.Y.ToString("R")+" box="+p.Hitbox+" active/dead/ghost="+p.active+"/"+p.dead+"/"+p.ghost+" grav="+p.gravDir+" controls="+p.controlLeft+"/"+p.controlRight+" rawTile="+Main.tile[50,41].active()+"/"+Main.tile[50,41].type);
                var path=Timeline(target);var frame=Frame(p,path);placementOwner=new MotionRect(p.position.X,p.position.Y,p.width,p.height);placementMomentQueries=0;
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(808,664),Vector2.UnitX,80,28,0,p.whoAmI,50,41);var ambient=Main.projectile[slot];
                var ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);var terrain=Terrain();var obstacles=Obstacles(p,ambient,path);
                placementMoment=true;
                try
                {
                    // The actual EffectSolve caller owns the distinction:
                    // BeforeNpc already completed Player; CompletedWorld is
                    // explicitly supplied that same advanced launch frame.
                    Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostEffectAttack",true),"Solve",p,ammo,path,p.RotatedRelativePoint(p.MountedCenter),0,beforeNpc,terrain,obstacles,0,frame);
                }
                finally{placementMoment=false;}
                Require(placementMomentQueries==1,"actual EffectSolve reaches first placement in "+(beforeNpc?"BeforeNpc":"CompletedWorld prepared")+" phase");
                ambient.Update(slot);Require(ambient.ai[1]==-1 && ambient.damage==28 && !Main.tile[50,41].active(),"original first AI22 sees occupied owner and keeps damaging failure tail");
                NativeQuickItemChecks.BeginWorldStep();p.Update(0);Require(Collision.EmptyTile(50,41),"second real Player action leaves the tile: repeating advancement would falsely place ice");
                ToolsPredicate(context,"after timing "+(beforeNpc?"BeforeNpc":"CompletedWorld prepared"));
                Console.WriteLine("ICE first-placement phase="+(beforeNpc?"BeforeNpc":"CompletedWorld prepared")+" owner="+placementOwner.X.ToString("R")+","+placementOwner.Y.ToString("R")+" nextOwner="+p.position.X.ToString("R")+","+p.position.Y.ToString("R")+" nativeFailedTail=True");cases++;
            }
            Console.WriteLine("PASS ICE actual first placement Player.Update timing cases="+cases);
        }
        private static void Motors(object context)
        {
            int cases=0;for(int mode=0;mode<20;mode++)
            {
                var p=Reset(context);p.position=new Vector2(650,646);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(808,616),new Vector2(12,0),80,28,0,p.whoAmI,60,38);var q=Main.projectile[slot];
                if(mode==1)q.ai[0]=50;if(mode==2){q.ai[0]=50;Target(3,new Vector2(808,616),true);}if(mode==3){q.ai[0]=50;NativeToolsChecks.Tile(50,38,19);}if(mode==4){q.ai[1]=-1;q.timeLeft=100;}
                if(mode>=5 && mode<=8){NativeToolsChecks.Tile(50,38,mode==8?127:1);Main.tile[50,38].inActive(mode==6 || mode==7);if(mode==7)q.ai[1]=-1;}
                if(mode==9){q.velocity=Vector2.Zero;}if(mode==10)q.lavaWet=true;
                if(mode==11 || mode==12){Main.tile[50,38].liquid=255;Main.tile[50,38].liquidType(mode==11?1:3);}if(mode==13){NativeToolsChecks.Tile(50,38,127);q.ai[0]=-1;q.damage=0;q.velocity=Vector2.Zero;}
                if(mode==14){q.ai[0]=-2;q.damage=0;q.velocity=Vector2.Zero;}if(mode==15){q.ai[0]=50;q.ai[1]=38;q.Center=new Vector2(792,616);q.velocity=new Vector2(12,1);NativeToolsChecks.Tile(50,38,1);}
                if(mode==16){q.ai[0]=50;q.ai[1]=38;q.Center=new Vector2(808,600);q.velocity=new Vector2(1,12);NativeToolsChecks.Tile(50,38,1);}if(mode==17){NativeToolsChecks.Tile(50,38,1);Main.tile[50,38].halfBrick(true);}if(mode==18){q.ai[1]=-1;NativeToolsChecks.Tile(50,38,19);Main.tile[50,38].inActive(true);}if(mode==19){q.ai[0]=50;Main.tile[50,38].liquid=255;Main.tile[50,38].liquidType(2);}
                var expected=Copy(q);var terrain=Terrain();bool changed;Require(Step(expected,terrain,PlacementSnapshot.Capture(),out changed),"native Ice Rod equivalent class is modeled mode="+mode);Finish(expected,terrain);q.Update(slot);
                Require(Same(q,expected) && q.timeLeft==expected.timeLeft,"original Ice22 action/placement/failure/actuation/liquid agrees mode="+mode+" native="+q.position+"/"+q.velocity+" ai="+string.Join(",",q.ai)+" expected="+expected.position+"/"+expected.velocity+" ai="+string.Join(",",expected.ai));cases++;
            }
            // Body occupancy is not damage eligibility. The original predicate
            // is a read-only oracle; the private query consumes copied boxes.
            foreach(int mode in new[]{0,1,2,3})
            {
                var p=Reset(context);bool priorDead=p.dead,priorGhost=p.ghost;var priorPosition=p.position;
                try
                {
                    p.Center=new Vector2(808,616);p.dead=mode==1;p.ghost=mode==2;if(mode==3){p.position=new Vector2(650,646);Target(3,new Vector2(808,616),true).dontTakeDamage=true;}
                    var snapshot=PlacementSnapshot.Capture();bool placed;Require(snapshot.Query(50,38,out placed) && placed==Collision.EmptyTile(50,38),"actual EmptyTile player/dead/ghost/friendly invulnerable occupancy mode="+mode);cases++;
                    Console.WriteLine("ICE occupancy mode="+mode+" scopedDeadGhost="+p.dead+"/"+p.ghost+" originalEmpty="+placed);
                }
                finally
                {
                    // This predicate-only dead/ghost counterexample is not a
                    // native death. Restore its exact entry facts before the
                    // following scene's strict Tools.Player admission check.
                    p.dead=priorDead;p.ghost=priorGhost;p.position=priorPosition;
                }
                Require(p.dead==priorDead && p.ghost==priorGhost && p.position==priorPosition,"occupancy test returns original player facts without weakening session admission");
                Console.WriteLine("ICE occupancy restored mode="+mode+" deadGhost="+p.dead+"/"+p.ghost);
            }
            Console.WriteLine("PASS ICE native action/occupancy classes="+cases);PlacementLeases(context);FamilyPlacement(context);ForeignFamilyPlacement(context);SharedReads(context);
        }
        private static void FamilyPlacement(object context)
        {
            var p=Reset(context);foreach(var cell in new[]{new Point(49,38),new Point(51,38),new Point(50,37),new Point(50,39)})NativeToolsChecks.Tile(cell.X,cell.Y,1);
            int a=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(808,616),Vector2.UnitX,80,28,0,p.whoAmI,50,38);
            int b=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(808,616),Vector2.UnitX,80,28,0,p.whoAmI,50,38);
            var first=Copy(Main.projectile[a]);var later=Copy(Main.projectile[b]);var terrain=Terrain();var world=Activator.CreateInstance(model.GetNestedType("World",Flags),true);var placement=Placement(PlacementSnapshot.Capture());
            Require((bool)Static(model,"Motor",first,terrain,world,placement),"private first slot models successful ice placement");Finish(first,terrain);Main.projectile[a].Update(a);
            Require(Same(Main.projectile[a],first) && first.damage==0 && Main.tile[50,38].active() && Main.tile[50,38].type==127,"actual earlier ice slot places harmless127");
            Require((bool)Static(model,"Motor",later,terrain,world,placement),"later same-family slot consumes newly placed127 overlay");Finish(later,terrain);Main.projectile[b].Update(b);
            Require(Same(Main.projectile[b],later) && later.ai[1]==-1 && later.damage==28,"original occupied ice with blocked neighbors preserves later damaging failure tail");
            Require((bool)Static(model,"Motor",later,terrain,world,placement),"known failed suffix ignores127 core on next action");Finish(later,terrain);Main.projectile[b].Update(b);
            Require(Same(Main.projectile[b],later) && later.timeLeft==Main.projectile[b].timeLeft && later.timeLeft==59,"actual later failed suffix remains modeled after the world-change action");
            Console.WriteLine("PASS ICE ordered same-family placement slots=2 actions=3 actualFailureTail=59");
        }
        private static string IceFacts(Projectile q)
        {return "owner="+q.owner+" active="+q.active+" P="+q.position.X.ToString("R")+","+q.position.Y.ToString("R")+" V="+q.velocity.X.ToString("R")+","+q.velocity.Y.ToString("R")+" ai="+string.Join(",",q.ai.Select(v=>v.ToString("R")))+" alpha="+q.alpha+" rotation="+q.rotation.ToString("R")+" damage="+q.damage+" dir="+q.direction+" wet/lava/honey/shimmer/count="+q.wet+"/"+q.lavaWet+"/"+q.honeyWet+"/"+q.shimmerWet+"/"+q.wetCount+" TL="+q.timeLeft+" size="+q.width+","+q.height+" extra="+q.extraUpdates;}
        private static void ForeignFamilyPlacement(object context)
        {
            var p=Reset(context);var foreignOwner=Main.player[1];bool wasActive=foreignOwner.active,wasDead=foreignOwner.dead,wasGhost=foreignOwner.ghost;var wasPosition=foreignOwner.position;
            try
            {
                foreignOwner.active=true;foreignOwner.dead=foreignOwner.ghost=false;foreignOwner.position=new Vector2(1300,646);
                // Locked NewProjectile10905 assigns caller ai only for the
                // local owner. This actual foreign birth remains at its native
                // default ai, not an invented network-initialized tile pair.
                int foreignSlot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(808,616),Vector2.UnitX,80,28,0,1,50,38);var foreign=Main.projectile[foreignSlot];var foreignInitialAi=(float[])foreign.ai.Clone();var pureForeign=Copy(foreign);var terrain=Terrain();var world=Activator.CreateInstance(model.GetNestedType("World",Flags),true);var bodies=PlacementSnapshot.Capture();var placement=Placement(bodies);
                Console.WriteLine("ICE foreign before myPlayer/net="+Main.myPlayer+"/"+Main.netMode+" body="+IceFacts(foreign)+" tileActive/type="+Main.tile[50,38].active()+"/"+Main.tile[50,38].type+" ownerActive="+foreignOwner.active);
                Require((bool)Static(model,"Motor",pureForeign,terrain,world,placement),"observed foreign AI22 has complete motor facts");Finish(pureForeign,terrain);foreign.Update(foreignSlot);
                Console.WriteLine("ICE foreign after same="+Same(foreign,pureForeign)+" query="+bodies.Queries+" tileActive/type="+Main.tile[50,38].active()+"/"+Main.tile[50,38].type+" native="+IceFacts(foreign)+" pure="+IceFacts(pureForeign));
                Require(Same(foreign,pureForeign) && !Main.tile[50,38].active() && bodies.Queries==0 && foreign.ai.SequenceEqual(foreignInitialAi),"native nonlocal owner gate preserves flight and never calls local placement");
                target=Target(2,new Vector2(1000,616));Require(Known(p,foreign,Timeline(target))==null,"a foreign flight is not a local positive damage source");
                // An already placed foreign member can still remove its ice
                // at original Kill. Capture occupied terrain before that slot,
                // then preserve the shared overlay for the later local slot.
                NativeToolsChecks.Tile(50,38,127);foreign.position=new Vector2(800,608);foreign.velocity=Vector2.Zero;foreign.ai[0]=-1;foreign.ai[1]=38;foreign.damage=0;foreign.timeLeft=1;
                int localSlot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(808,616),Vector2.UnitX,80,28,0,p.whoAmI,50,38);var local=Main.projectile[localSlot];var pureLocal=Copy(local);pureForeign=Copy(foreign);terrain=Terrain();world=Activator.CreateInstance(model.GetNestedType("World",Flags),true);bodies=PlacementSnapshot.Capture();placement=Placement(bodies);
                Require(foreignSlot<localSlot && (bool)Static(model,"Motor",pureForeign,terrain,world,placement),"earlier foreign placed member is advanced in native slot order");pureForeign.timeLeft--;Require(pureForeign.timeLeft==0 && (bool)Static(model,"Kill",pureForeign,terrain,world),"known foreign lifetime consumes its actual Kill cell in the same private family");foreign.Update(foreignSlot);
                Require(Same(foreign,pureForeign) && !foreign.active && !Main.tile[50,38].active() && bodies.Queries==0,"original foreign retirement removes127 without local placement or local damage admission");
                Require((bool)Static(model,"Motor",pureLocal,terrain,world,placement),"later local member consumes the earlier foreign removal overlay");Finish(pureLocal,terrain);local.Update(localSlot);
                Require(Same(local,pureLocal) && local.damage==0 && local.ai[0]<0 && Main.tile[50,38].active() && Main.tile[50,38].type==127 && bodies.Queries==1,"native later local placement succeeds after foreign127 removal, with no frozen occupied-cell error");
                Console.WriteLine("PASS ICE foreign actual owner gate/no local Source and placed127 retirement -> later local placement slots="+foreignSlot+","+localSlot);
            }
            finally{foreignOwner.active=wasActive;foreignOwner.dead=wasDead;foreignOwner.ghost=wasGhost;foreignOwner.position=wasPosition;}
        }
        private static void SharedReads(object context)
        {
            var p=Reset(context);target=Target(2,new Vector2(1000,616));target.SetDefaults(2);target.active=true;target.Center=new Vector2(1000,616);target.velocity=Vector2.Zero;target.target=0;
            var id=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",target,1L);var source=Get(Get(context,"CombatObservation"),"Prediction");
            var path=(NpcTrajectory)Call(source,"TryCandidate",id,(long)Main.GameUpdateCount-1,true,120);Require(path!=null && path.Count>1,"actual source builds an exact opt-in Ice path read proof");
            var sourceTerrain=Get(Get(source,"candidateSource"),"Terrain");var cells=(System.Collections.IDictionary)Get(sourceTerrain,"cells");Require(cells.Count>0,"real source necessary consumer reads cells");int key=(int)cells.Keys.Cast<object>().First();var near=new Point(key%Main.maxTilesX,key/Main.maxTilesX);var far=new Point(90,20);var sourceType=source.GetType();
            Require(!(bool)Static(sourceType,"IceTrajectoryUnaffected",path,new[]{near}) && (bool)Static(sourceType,"IceTrajectoryUnaffected",path,new[]{far}),"actual whole source read proof distinguishes consumed and distant changed cells");
            var cached=(NpcTrajectory)Call(source,"TryCandidate",id,(long)Main.GameUpdateCount-1,true,120);Require(cached!=null && !(bool)Static(sourceType,"IceTrajectoryUnaffected",cached,new[]{near}) && (bool)Static(sourceType,"IceTrajectoryUnaffected",cached,new[]{far}),"same-sample cache publication retains the complete prior source terrain read proof");
            Require(!(bool)Static(sourceType,"IceTrajectoryUnaffected",Timeline(target),new[]{far}),"an unbound matching-looking path never borrows another publication's proof");
            var terrain=Terrain();var tileArgs=new object[]{90,20,default(PredictionTile),PredictionStop.None};Require((bool)Call(terrain,"Tile",tileArgs),"hot-cache fixture reads known actual empty cell");
            Require((bool)Call(terrain,"BeginIcePlayerReadGuard",(object)new[]{far}),"bounded changed-cell player read guard begins");
            try
            {
                Require(!(bool)Call(terrain,"BeginIcePlayerReadGuard",(object)new[]{near}),"read guard refuses reentry without replacing active protection");
                tileArgs=new object[]{90,20,default(PredictionTile),PredictionStop.None};Require(!(bool)Call(terrain,"Tile",tileArgs) && (PredictionStop)tileArgs[3]==PredictionStop.TerrainUnavailable,"actual hot-cache lookup rejects old cell after predicted ice change");
                tileArgs=new object[]{89,20,default(PredictionTile),PredictionStop.None};Require((bool)Call(terrain,"Tile",tileArgs),"unaffected terrain consumer remains available under guard");
            }
            finally{Call(terrain,"EndIcePlayerReadGuard");}
            tileArgs=new object[]{90,20,default(PredictionTile),PredictionStop.None};Require((bool)Call(terrain,"Tile",tileArgs),"guard exit restores original cached cell access");
            // A real far member places ice in action one; selected future
            // contact occurs later. It cannot blanket-cancel the whole family.
            p=Reset(context);target=NativeCombatControlPreviewChecks.Dummy(2,49,40);source=Get(Get(context,"CombatObservation"),"Prediction");id=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",target,1L);
            path=(NpcTrajectory)Call(source,"TryCandidate",id,(long)Main.GameUpdateCount-1,true,120);Require(path!=null && path.Count>1,"default source captures actual training dummy before far ice placement");
            int ambient=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(1448,328),Vector2.UnitX,80,28,0,p.whoAmI,90,20);
            int shot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),p.MountedCenter,new Vector2(12,0),80,28,0,p.whoAmI,(int)(target.Center.X/16),(int)(target.Center.Y/16));var q=Main.projectile[shot];var known=Known(p,q,path);Require(known!=null && known.Tick>1,"far ambient actual placement preserves later selected source's predicted contact");
            observe=true;keys.Add(q.key);long sample=path.SampleTick;for(int action=0;action<30 && first<0;action++){if(action>0){NativeQuickItemChecks.BeginWorldStep();p.Update(0);}target.UpdateNPC(target.whoAmI);if(Main.projectile[ambient].active)Main.projectile[ambient].Update(ambient);if(q.active)q.Update(shot);}
            Require(Main.tile[90,20].active() && Main.tile[90,20].type==127 && first-sample==known.Tick,"original far ambient world change and later real Strike retain exact future tick");
            observe=false;Console.WriteLine("PASS ICE source fresh/cached far/near/unbound proof, hot guard/reentry/exit and far native placement-to-later-Strike");
        }
        private static void PlacementLeases(object context)
        {
            var extra=Main.player[1];bool extraActive=extra.active,extraDead=extra.dead,extraGhost=extra.ghost;var extraPosition=extra.position;
            try
            {
            for(int mode=0;mode<6;mode++)
            {
                var p=Reset(context);target=Target(2,new Vector2(1000,616));int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),p.MountedCenter,new Vector2(12,0),80,28,0,p.whoAmI,50,38);var q=Main.projectile[slot];
                if(mode==0)Target(3,new Vector2(808,616),true).dontTakeDamage=true;
                if(mode==3){Main.player[1].active=true;Main.player[1].dead=Main.player[1].ghost=false;Main.player[1].Center=new Vector2(808,616);}
                var path=Timeline(target);
                if(mode==4)
                {
                    var state=new NpcMotionState{Identity=path.Identity,X=800,Y=608,Width=target.width,Height=target.height,CanReceive=true,NoTileCollide=true};
                    var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();path=new NpcTrajectory(path.Identity,path.SampleTick,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
                }
                var obstacles=Obstacles(p,q,path);var owner=mode==5?new MotionRect(800,608,p.width,p.height):new MotionRect(p.position.X,p.position.Y,p.width,p.height);
                var args=new object[]{Terrain(),50,38,1,1,owner,false};Require((bool)Call(obstacles,"TryIcePlacement",args),"raw placement lease consumer has complete values mode="+mode);
                Require((bool)args[6]==(mode==1 || mode==2),"placement consumes future selected raw Bounds, future owner raw box and every non-damageable occupant mode="+mode);
                Require((bool)obstacles.GetType().GetProperty("Unchanged",Flags).GetValue(obstacles,null),"initial complete raw occupant lease is current mode="+mode);
                if(mode==0)Main.npc[3].position.X+=16;
                if(mode==1)Target(3,new Vector2(808,616),true).dontTakeDamage=true;
                if(mode==2){Main.player[1].active=true;Main.player[1].dead=Main.player[1].ghost=false;Main.player[1].Center=new Vector2(808,616);}
                if(mode==3)Main.player[1].ghost=true;
                if(mode<4)Require(!(bool)obstacles.GetType().GetProperty("Unchanged",Flags).GetValue(obstacles,null),"raw occupant movement/new NPC/new player/ghost withdrawal revokes real consume lease mode="+mode);
            }
            }
            finally{extra.active=extraActive;extra.dead=extraDead;extra.ghost=extraGhost;extra.position=extraPosition;}
            Console.WriteLine("PASS ICE placement lease cases=6 exactFutureRawBoxes=2 changedMembership=4");
        }
        private static void Receive(object context)
        {
            int cases=0;foreach(int mode in new[]{0,1,2,3,4,5})
            {
                var p=Reset(context);NativeQuickItemChecks.BeginWorldStep();p.Update(0);target=Target(2,p.MountedCenter+new Vector2(70,0));int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),p.MountedCenter,new Vector2(12,0),80,28,0,p.whoAmI,90,40);var q=Main.projectile[slot];
                if(mode==1){q.Center=p.MountedCenter+new Vector2(35,0);q.ai[0]=(int)(q.Center.X/16);q.ai[1]=(int)(q.Center.Y/16);}if(mode==2 || mode==5)target.immune[0]=500;if(mode==5)q.penetrate=q.maxPenetrate=2;if(mode==3){for(int y=30;y<=43;y++)NativeToolsChecks.Tile(44,y,1);}if(mode==4){q.Center=target.Center;q.ai[0]=(int)(target.Center.X/16);q.ai[1]=(int)(target.Center.Y/16);}
                keys.Add(q.key);observe=true;NativeQuickItemChecks.BeginWorldStep();var path=Timeline(target);var expected=Known(p,q,path);long sample=path.SampleTick;bool failed=false;
                for(int action=0;action<20 && q.active && first<0;action++){if(action>0){NativeQuickItemChecks.BeginWorldStep();p.Update(0);}target.UpdateNPC(2);q.Update(slot);if(q.ai[1]<0)failed=true;}
                bool negative=mode==1 || mode==3 || mode==5;Require((first<0)==negative && (expected==null)==negative && (expected==null || first-sample==expected.Tick),"actual first ice Strike/null matches Known with placement success, occupied failure tail, wall and immunity mode="+mode+" first="+(first<0?0:first-sample)+" expected="+expected?.Tick);if(mode==4)Require(failed && first>=0,"selected native raw body makes placement fail, preserving real damaging suffix");cases++;
            }
            Console.WriteLine("PASS ICE native receive classes="+cases+" native maxPen1 immune exemption positive / maxPen2 immune gate negative");
        }
        private static void Natural(object context)
        {
            var p=Reset(context);target=NativeCombatControlPreviewChecks.Dummy(2,49,40);raw=target.Center-new Vector2(430,0);var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");solves=steps=0;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,raw,false,true);var plan=(AttackContact)GetOptional(attack,"ExpectedPreview");Require(plan!=null && !Main.projectile.Any(q=>q.active) && !p.controlUseItem,"idle Ice Rod has a useful actual source preview without live spawn");
            int oldSteps=steps,oldSolves=solves;var impact=Get(Get(host,"World"),"Impact");var zoom=Main.GameViewMatrix.ZoomMatrix;for(int i=0;i<12;i++){Call(impact,"Capture");Call(impact,"Project",zoom,Matrix.Invert(zoom));Require((bool)Call(attack,"PreviewCurrent",plan),"current ice preview lease");}Require(steps==oldSteps && solves==oldSolves && solves>0,"CPU Capture/Project does not replay Ice Rod; World.Draw/pixels remain unverified");
            natural=observe=auditMotor=true;NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));long start=Main.GameUpdateCount;int visible=0,withdrawn=0;
            for(int action=0;action<130;action++)
            {
                naturalAction=action;physicalLeft=action<27;NativeCombatSelectionPreviewChecks.Step(context,raw,physicalLeft);var shown=(AttackContact)GetOptional(attack,"ExpectedPreview");
                Console.WriteLine("ICE preview-frame "+NaturalFacts(p)+" shown="+(shown!=null)+" receiverLife="+target.life+" sourceDamageFlights="+Main.projectile.Count(q=>q.active && q.type==80 && q.owner==p.whoAmI && q.damage>0));
                if(physicalLeft)
                {
                    // The demonstrated viable interval is this stationary,
                    // living Dummy with mana for every next original use.
                    // Cooldown is not a reason to erase its next-use preview.
                    Require(target.active && target.life>0 && !target.dontTakeDamage && p.active && !p.dead && !p.ghost && p.statMana>=p.HeldItem.mana,"declared held feasible interval retains native receiver/player/resource facts");
                    Require(shown!=null,"every frame of the actual held feasible interval retains a useful preview; full withdrawal facts were printed first");visible++;
                }
                else if(shown==null){withdrawn++;Console.WriteLine("ICE post-release withdrawal actual handPending="+(p.itemAnimation>0)+" originalSourceAlive="+Main.projectile.Any(q=>q.active && q.type==80 && q.owner==p.whoAmI)+"; not counted in held feasible denominator");}
            }
            Console.WriteLine("ICE natural actions=130 held=27 hand="+births+" positive="+positive+" first="+(first-start)+" known="+bornPrediction?.Tick+" preview="+plan.Tick+" restored="+restored+" currentMotor="+motors+" retire="+kills+" sustainedPreview="+visible+"/27 releasedWithdrawals="+withdrawn+" assisted/manualBirths="+assistedBirths+"/"+manualBirths);
            Require(births>=3 && positive>0 && first>=start && bornPrediction!=null && first-start==plan.Tick && first-firstBirth+1==bornPrediction.Tick,"real first Ice Rod use against native Dummy488 has positive first Strike at its actual idle and first-birth marks");Require(restored==births && assistedBirths>=3 && visible==27 && !p.controlUseItem && !Main.projectile.Any(q=>q.active && q.type==80 && q.damage>0),"held/released original uses preserve paired input restoration, useful preview, and retire every damaging source");natural=observe=auditMotor=false;
            // Occupied stone obstructs all admitted target aim seeds. A legal
            // physical use remains original input and may place harmless ice.
            p=Reset(context);target=NativeCombatControlPreviewChecks.Dummy(2,49,40);for(int y=15;y<=43;y++)NativeToolsChecks.Tile(46,y,1);raw=target.Center-new Vector2(430,0);NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,raw,false,true);Require(GetOptional(attack,"ExpectedPreview")==null,"native solid core makes Ice Rod straight aim genuinely infeasible, not a placed endpoint Contact");
            observe=true;NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));for(int i=0;i<80;i++)NativeCombatSelectionPreviewChecks.Step(context,raw,i==0);Require(births==1 && positive==0 && !p.controlUseItem && Main.projectile.Where(q=>q.active && q.type==80).All(q=>q.damage==0),"infeasible original use is not steered to a fake hit and release leaves no damaging flight");Console.WriteLine("PASS ICE natural positive130 negative80 originalBirths="+births+" CPUCaptureProject=12 DrawUnverified=True");
        }
    }
}
