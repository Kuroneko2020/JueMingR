using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatShotBirthChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private sealed class Birth {internal Vector2 Aim,Center,Velocity;}
        private static readonly List<Birth> births=new List<Birth>();
        private static bool capturing;private static Vector2 cursor;
        private static int poseDeaths,poseDeathSaves;private static string poseStage;
        private static Type facingModel;private static int facingEntries,facingExits,predictedFacing,nativeFacing;
        private static bool facingPose,facingCrossed;private static Vector2 facingOrigin,facingLogical;private static float facingRotation;
        private static bool RefusePoseDeathSave(){poseDeathSaves++;return false;}
        private static void PoseDeath(Player __instance,Terraria.DataStructures.PlayerDeathReason __0,double __1)
        {if(!ReferenceEquals(__instance,Main.LocalPlayer))return;poseDeaths++;Console.WriteLine("FACING native death stage="+poseStage+" other="+__0.SourceOtherIndex+" npc="+GetOptional(__0,"_sourceNPCIndex")+" damage="+__1+" position="+__instance.position+" fall="+__instance.fallStart+" gravity="+__instance.gravDir+" mount="+__instance.mount.Type+Environment.NewLine+new System.Diagnostics.StackTrace(1,true));}
        private static void FacingShootBefore(Player __instance,Item __1)
        {
            if(!ReferenceEquals(__instance,Main.LocalPlayer) || __1.type!=3870)return;
            facingEntries++;facingOrigin=__instance.RotatedRelativePoint(__instance.MountedCenter);facingLogical=Main.MouseWorld;facingRotation=__instance.fullRotation;
            facingPose=__instance.sleeping.isSleeping && !__instance.mount.Active && __instance.forcedGravity>0 && __instance.gravDir==-1 && Math.Abs(Math.Abs(facingRotation)-MathHelper.PiOver2)<.0001f && __instance.active && !__instance.dead && __instance.statLife>0 && poseDeaths==0 && poseDeathSaves==0;
            // At the native sleeping quarter-turn, opposite Y sides of the
            // same origin cross the facing dot sign. This only qualifies the
            // input domain; the expected direction comes from actual Shoot.
            float physicalY=Main.screenPosition.Y+Main.mouseY;
            facingCrossed=Main.mouseX>=0 && Main.mouseX<Main.screenWidth && Main.mouseY>=0 && Main.mouseY<Main.screenHeight && (facingLogical.Y-facingOrigin.Y)*(physicalY-facingOrigin.Y)<0;
            predictedFacing=(int)Static(facingModel,"Facing",__instance,facingOrigin,facingLogical);
            Console.WriteLine("FACING actual Shoot entry sleeping="+__instance.sleeping.isSleeping+" forcedGravity="+__instance.forcedGravity+" gravity="+__instance.gravDir+" mount="+__instance.mount.Active+" rotation="+facingRotation+" origin="+facingOrigin+" logical="+facingLogical+" physical="+Main.mouseX+","+Main.mouseY+" crossed="+facingCrossed+" alive="+!__instance.dead+" deaths="+poseDeaths+" saves="+poseDeathSaves+" predicted="+predictedFacing);
        }
        private static void FacingShootAfter(Player __instance,Item __1)
        {
            if(!ReferenceEquals(__instance,Main.LocalPlayer) || __1.type!=3870)return;
            facingExits++;nativeFacing=__instance.direction;
            Console.WriteLine("FACING actual Shoot exit native="+nativeFacing+" sleeping="+__instance.sleeping.isSleeping+" rotation="+__instance.fullRotation+" three711="+Main.projectile.Count(q=>q.active && q.type==711));
        }
        private static void Cursor(Vector2 __2){if(capturing)cursor=__2;}
        private static void Replay(Projectile __1){if(capturing)births.Add(new Birth{Aim=cursor,Center=__1.Center,Velocity=__1.velocity});}
        private static void Dart(Projectile[] __0,Vector2 __9){if(capturing && __0.Length>0)births.Add(new Birth{Aim=__9,Center=__0[0].Center,Velocity=__0[0].velocity});}
        private static object Static(Type type,string name,params object[] args){return type.GetMethod(name,Flags).Invoke(null,args);}
        private static Player Scene(object context,int item)
        {
            var p=NativeCombatBallisticCandidateChecks.Scene(context,item,50);p.position=new Vector2(800,500);p.direction=1;p.fullRotation=0;p.gravDir=1;p.velocity=Vector2.Zero;
            for(int x=5;x<115;x++)for(int y=5;y<78;y++)Main.tile[x,y].ClearEverything();foreach(var q in Main.projectile)q.active=false;foreach(var n in Main.npc)n.active=false;
            return p;
        }
        private static NPC Target(Vector2 point)
        {
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.width=64;n.height=64;n.Center=point;n.life=n.lifeMax=100000;n.noGravity=n.noTileCollide=true;n.velocity=Vector2.Zero;n.dontTakeDamage=false;
            return n;
        }
        private static NpcTrajectory Path(NPC n)
        {
            var id=new NpcIdentity(1,n,n.whoAmI,n.generation,n.type,n.netID);var state=new NpcMotionState{Identity=id,Active=true,CanReceive=true,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,NoTileCollide=true};
            var points=Enumerable.Range(0,121).Select(i=>new NpcTrajectoryPoint(i,state)).ToArray();return new NpcTrajectory(id,Main.GameUpdateCount,1,PredictionAssumption.None,PredictionStop.None,points,points.Length);
        }
        internal static void RunFacing(object context)
        {
            NativeCombatAimChecks.Initialize();
            NativeCombatLiveContextChecks.InitializeMount();
            var host=Get(context,"Combat").GetType().Assembly;facingModel=host.GetType("JueMingR.TerrariaHost.Combat.HostBetsyFlameAttack",true);
            var audit=new Harmony("JueMingR.Tests.ReviewedFacingPose");
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            // This pose oracle owns no PlayerFileData/save lifecycle. Count
            // and reject only the unrelated async death-save launch; any
            // native death or attempted save still fails the live-pose gate.
            // Player.KillMe and the real Player.IOLock remain untouched.
            poseDeaths=poseDeathSaves=0;poseStage="scene";audit.Patch(typeof(WorldGen).GetMethod("saveToonWhilePlaying",Flags),prefix:new HarmonyMethod(typeof(NativeCombatShotBirthChecks),nameof(RefusePoseDeathSave)));
            audit.Patch(typeof(Player).GetMethod("KillMe",Flags),prefix:new HarmonyMethod(typeof(NativeCombatShotBirthChecks),nameof(PoseDeath)));
            audit.Patch(typeof(Player).GetMethod("ItemCheck_Shoot",Flags),prefix:new HarmonyMethod(typeof(NativeCombatShotBirthChecks),nameof(FacingShootBefore)),postfix:new HarmonyMethod(typeof(NativeCombatShotBirthChecks),nameof(FacingShootAfter)));
            var sections=Main.sectionManager;int mode=Main.netMode,w=Main.screenWidth,h=Main.screenHeight,brain=NPC.brainOfGravity,crimson=NPC.crimsonBoss;var screen=Main.screenPosition;bool good=Main.getGoodWorld;
            var sleeping=Main.sleepingManager;var sitting=Main.sittingManager;var zoom=Main.GameViewMatrix.Zoom;
            float left=Main.leftWorld,right=Main.rightWorld,top=Main.topWorld,bottom=Main.bottomWorld;
            try
            {
                // Use the normal weapon baseline, not Scene's direct pose
                // fields. Native bed interaction is this witness's only
                // sleeping/quarter-turn producer; the actual Brain supplies
                // gravity later in the original Player.Update order.
                var p=NativeCombatBallisticCandidateChecks.Scene(context,3870,50);p.position=new Vector2(800,52*16-p.height);
                for(int x=5;x<115;x++)for(int y=5;y<78;y++)Main.tile[x,y].ClearEverything();foreach(var q in Main.projectile)q.active=false;foreach(var n in Main.npc)n.active=false;
                Main.leftWorld=Main.topWorld=0;Main.rightWorld=Main.maxTilesX*16;Main.bottomWorld=Main.maxTilesY*16;
                Main.sittingManager=new Terraria.DataStructures.AnchoredEntitiesCollection();Main.sleepingManager=new Terraria.DataStructures.AnchoredEntitiesCollection();
                var data=Terraria.ObjectData.TileObjectData.GetTileData(TileID.Beds,0);
                Require(data!=null && data.Width==4 && data.Height==2,"locked native bed metadata supplies its complete four-by-two object");
                for(int x=48;x<56;x++)NativeToolsChecks.Tile(x,52,TileID.Stone);
                Require(WorldGen.PlaceObject(50+data.Origin.X,50+data.Origin.Y,TileID.Beds,true,direction:-1),"original PlaceObject builds a genuinely anchored bed");
                for(int x=50;x<54;x++)for(int y=50;y<52;y++)Require(Main.tile[x,y].active() && Main.tile[x,y].type==TileID.Beds,"native bed placement retains all eight actual tiles");
                int direction;Vector2 anchor,visual;
                Require(Terraria.GameContent.PlayerSleepingHelper.GetSleepingTargetInfo(50,50,out direction,out anchor,out visual),"original bed metadata resolves the real sleep anchor");
                Require(!p.mount.Active && !p.sleeping.isSleeping,"the normal original holder begins unmounted and awake");
                poseStage="native-bed";p.sleeping.StartSleeping(p,50,50);p.ResetFallDamage();
                Require(p.sleeping.isSleeping && p.Bottom==anchor && p.direction==direction && Math.Abs(Math.Abs(p.fullRotation)-MathHelper.PiOver2)<.0001f,"actual StartSleeping snaps to the bed and produces its original quarter-turn");
                Console.WriteLine("FACING actual bed anchor="+anchor+" direction="+direction+" sleeping="+p.sleeping.isSleeping+" rotation="+p.fullRotation+" gravity="+p.gravDir);
                // Real AI54's completed phase owns brainOfGravity only in a
                // good world; Player then acquires forcedGravity itself. The
                // already-entered phase avoids replaying its one-time visuals.
                Main.getGoodWorld=true;var boss=Main.npc[1];boss.SetDefaults(NPCID.BrainofCthulhu);boss.whoAmI=1;boss.active=true;boss.Center=p.Center+new Vector2(200,0);boss.ai[0]=-1;boss.localAI[0]=boss.localAI[2]=1;boss.target=p.whoAmI;poseStage="brain-source";boss.AI();
                Require(NPC.brainOfGravity==1,"original good-world Brain AI54 establishes the gravity source");
                Main.GameViewMatrix.Zoom=Vector2.One;Main.screenPosition=p.Center-new Vector2(Main.screenWidth/2,Main.screenHeight/2);Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();
                // A genuine physical point inside the viewport stays below
                // the holder. Original inverted MouseWorld restores it above;
                // the Shoot observer verifies that actual integer mapping.
                facingEntries=facingExits=0;facingPose=facingCrossed=false;
                NativeToolExecutionChecks.Sample(context,Get(context,"Input"),p.Center+new Vector2(10,200),true);NativeQuickItemChecks.BeginWorldStep();
                Main.sittingManager.ClearPlayerAnchors();Main.sleepingManager.ClearPlayerAnchors();poseStage="player-update";p.Update(p.whoAmI);
                Require(p.active && !p.dead && p.statLife>0 && poseDeaths==0 && poseDeathSaves==0,"legal original Player.Update reaches the rotating pose alive with no death or save request");
                Require(facingEntries==1 && facingExits==1 && facingPose,"complete original ItemCheck reaches Shoot in its native sleeping inverted pose");
                Require(facingCrossed,"actual legal screen pointer and restored logical MouseWorld cross the sleeping facing axis");
                Require(Main.projectile.Count(q=>q.active && q.type==711)==3,"actual Betsy Shoot reaches all three deterministic births");
                Require(predictedFacing==nativeFacing,"B10-F01: helper facing must equal original Shoot ChangeDir after logical MouseWorld restoration; predicted="+predictedFacing+" native="+nativeFacing+" logical="+facingLogical+" rotation="+facingRotation);
                Console.WriteLine("PASS original Shoot facing: native bed, inverted sleeping pose and complete ItemCheck across dot sign");
            }
            finally{audit.UnpatchAll(audit.Id);facingModel=null;Main.sleepingManager=sleeping;Main.sittingManager=sitting;Main.GameViewMatrix.Zoom=zoom;Main.leftWorld=left;Main.rightWorld=right;Main.topWorld=top;Main.bottomWorld=bottom;Main.getGoodWorld=good;NPC.crimsonBoss=crimson;NPC.brainOfGravity=brain;Main.netMode=mode;Main.sectionManager=sections;Main.screenPosition=screen;Main.screenWidth=w;Main.screenHeight=h;Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();}
        }
        internal static void RunBirthOrder(object context)
        {
            NativeCombatAimChecks.Initialize();
            var host=Get(context,"Combat").GetType().Assembly;var representative=host.GetType("JueMingR.TerrariaHost.Combat.HostRepresentativeAttack",true);var models=host.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels",true);var ammoType=host.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true);
            var patch=new Harmony("JueMingR.Tests.ReviewedShotBirth");
            patch.Patch(host.GetType("JueMingR.TerrariaHost.Combat.HostThornAttack",true).GetMethods(Flags).Single(m=>m.Name=="BirthVelocity" && m.GetParameters().Length==5),postfix:new HarmonyMethod(typeof(NativeCombatShotBirthChecks),nameof(Cursor)));
            patch.Patch(representative.GetMethod("Replay",Flags),prefix:new HarmonyMethod(typeof(NativeCombatShotBirthChecks),nameof(Replay)));
            patch.Patch(host.GetType("JueMingR.TerrariaHost.Combat.HostDartAttack",true).GetMethod("Family",Flags),prefix:new HarmonyMethod(typeof(NativeCombatShotBirthChecks),nameof(Dart)));
            int cases=0;
            try
            {
                foreach(int item in new[]{3859,281,986,3007,3008})foreach(int oldDirection in new[]{1,-1})foreach(bool reverse in new[]{true,false})
                {
                    var p=Scene(context,item);p.direction=oldDirection;p.inventory[54].SetDefaults(item==3859?ItemID.WoodenArrow:3009);p.inventory[54].stack=999;
                    int intended=reverse?-oldDirection:oldDirection;var n=Target(p.MountedCenter+new Vector2(intended*220,-12));var path=Path(n);var ammo=Static(ammoType,"Capture",p,p.HeldItem);Require(ammo!=null,"real weapon and ammo resolve");
                    var terrain=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");var origin=(Vector2)models.GetMethods(Flags).Single(m=>m.Name=="LaunchOrigin" && m.GetParameters().Length==2).Invoke(null,new object[]{p,p.HeldItem});
                    var clock=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc"),0},null);
                    Vector2 aim;births.Clear();capturing=true;
                    using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))
                    {
                        if(item==3859)
                        {
                            var solution=representative.GetMethods(Flags).Single(m=>m.Name=="Prepare" && m.GetParameters().Length==6).Invoke(null,new object[]{p,ammo,path,origin,clock,terrain});
                            Require((bool)Get(solution,"HasPoint"),"Phoenix has a legal representative candidate");aim=(Vector2)Get(solution,"Point");
                        }
                        else
                        {
                            var prototype=ContentSamples.ProjectilesByType[(int)Get(ammo,"Projectile")];var root=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,prototype,path,0,true,false},null);
                            var contact=(AttackContact)Static(host.GetType("JueMingR.TerrariaHost.Combat.HostDartAttack",true),"Solve",p,ammo,path,origin,0,true,terrain,root,0);
                            Require(contact!=null,"dedicated dart has an unobstructed same-input contact");aim=new Vector2(contact.AimX,contact.AimY);
                        }
                    }
                    capturing=false;Require(p.direction==oldDirection,"prediction never changes the live player's facing");var predicted=births.Last(b=>b.Aim==aim);
                    Main.mouseX=(int)(aim.X-Main.screenPosition.X);Main.mouseY=(int)(aim.Y-Main.screenPosition.Y);
                    typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});
                    var shots=Main.projectile.Where(q=>q.active && q.owner==p.whoAmI).OrderBy(q=>q.whoAmI).ToArray();Require(shots.Length==(item==3859?6:1),"native weapon has its real fan/birth count");
                    var actual=shots[item==3859?2:0];
                    Require(Vector2.DistanceSquared(actual.Center,predicted.Center)<.0001f && Vector2.DistanceSquared(actual.velocity,predicted.Velocity)<.0001f,"B10-F02: candidate uses original ChangeDir-before-muzzle/fan order item="+item+" reverse="+reverse+" native="+actual.Center+"/"+actual.velocity+" predicted="+predicted.Center+"/"+predicted.Velocity);
                    cases++;
                }
                Console.WriteLine("PASS original Shoot candidate birth order cases="+cases);
            }
            finally{capturing=false;births.Clear();patch.UnpatchAll(patch.Id);}
        }
    }
}
