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
    internal static class NativeCombatPhasebladeChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static readonly int[] weapons={198,199,200,201,202,203,3764,3765,3766,3767,3768,3769,4258,4259,671,5535,5536,5670,5671};
        private static bool watchPress;private static Rectangle pressBox;private static int pressAnimation,pressMaximum,pressVisualType;private static Vector2 pressLocation;
        private static bool measureReads;private static int playerReads;
        private static bool watchRelease;
        private static NPC releaseTarget;private static Projectile releaseShot;private static int releaseDamageDepth,releaseHits,releaseDamage,releasePlanTick;private static ulong firstReleaseDamage;
        private static void ReleasePlan(Player __0,object __4,AttackContact __result)
        {if(watchRelease){if((bool)Get(__4,"BeforeNpc"))releasePlanTick=__result?.Tick??0;Console.WriteLine("PHASEBLADE RELEASEPLAN item="+__0.HeldItem.type+" animation="+__0.itemAnimation+" time="+__0.itemTime+" use="+__0.controlUseItem+" owner="+__0.position+" phaseBefore="+Get(__4,"BeforeNpc")+" first="+Get(__4,"FirstTick")+" delay="+Get(__4,"LaunchDelay")+" contact="+__result?.Tick+" aim="+__result?.AimX+","+__result?.AimY);}}
        private static void BeforeShot(object __instance,Player __0)
        {if(watchRelease){var contact=GetOptional(__instance,"current") as AttackContact;Console.WriteLine("PHASEBLADE SHOTENTRY animation="+__0.itemAnimation+" time="+__0.itemTime+" use="+__0.controlUseItem+" selected="+GetOptional(__instance,"Target")+" current="+contact?.Tick+" aim="+contact?.AimX+","+contact?.AimY);}}
        private static void AfterShot(object __result)
        {if(watchRelease)Console.WriteLine("PHASEBLADE SHOTCURSOR borrowed="+(__result!=null));}
        private static void Born(Player __instance,Projectile __0)
        {if(watchRelease && __instance.whoAmI==Main.myPlayer){releaseShot=__0;Console.WriteLine("PHASEBLADE BORN type="+__0.type+" slot="+__0.whoAmI+" center="+__0.Center+" velocity="+__0.velocity+" ownerHitCheck="+__0.ownerHitCheck+" animation="+__instance.itemAnimation+" mouse="+Main.MouseWorld);}}
        private static void BeforeReleaseDamage(Projectile __instance,out bool __state)
        {__state=releaseTarget!=null && ReferenceEquals(__instance,releaseShot);if(__state)releaseDamageDepth++;}
        private static void AfterReleaseDamage(bool __state){if(__state)releaseDamageDepth--;}
        private static void ReleaseStrike(NPC __instance,int __result)
        {if(releaseDamageDepth>0 && ReferenceEquals(__instance,releaseTarget) && __result>0){if(releaseHits==0)firstReleaseDamage=Main.GameUpdateCount;releaseHits++;releaseDamage+=__result;}}
        private static void PlayerRead(){if(measureReads)playerReads++;}
        private static void PressShape(Player __instance,Rectangle __1)
        {if(watchPress && __instance.whoAmI==Main.myPlayer){pressBox=__1;pressAnimation=__instance.itemAnimation;pressMaximum=__instance.itemAnimationMax;pressVisualType=__instance.lastVisualizedSelectedItem.type;pressLocation=__instance.itemLocation;}}
        internal static void Run(object context)
        {
            var assembly=Get(context,"Combat").GetType().Assembly;var model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostPhasebladeAttack",true);
            bool wind=Main.windPhysics;var audit=new Harmony("JueMingR.Tests.Phaseblade");
            audit.Patch(typeof(Player).GetMethod("ItemCheck_MeleeHitNPCs",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPhasebladeChecks).GetMethod("PressShape",Flags)));
            audit.Patch(assembly.GetType("JueMingR.TerrariaHost.Combat.NpcPredictionSource",true).GetMethod("ReadPlayer",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPhasebladeChecks).GetMethod("PlayerRead",Flags)));
            audit.Patch(model.GetMethod("ReleaseSolve",Flags),postfix:new HarmonyMethod(typeof(NativeCombatPhasebladeChecks).GetMethod("ReleasePlan",Flags)));
            audit.Patch(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackAim",true).GetMethod("BeginShot",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPhasebladeChecks).GetMethod("BeforeShot",Flags)),postfix:new HarmonyMethod(typeof(NativeCombatPhasebladeChecks).GetMethod("AfterShot",Flags)));
            audit.Patch(typeof(Player).GetMethod("TryUpdateChannel",Flags,null,new[]{typeof(Projectile)},null),postfix:new HarmonyMethod(typeof(NativeCombatPhasebladeChecks).GetMethod("Born",Flags)));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPhasebladeChecks).GetMethod("BeforeReleaseDamage",Flags)),postfix:new HarmonyMethod(typeof(NativeCombatPhasebladeChecks).GetMethod("AfterReleaseDamage",Flags)));
            audit.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatPhasebladeChecks).GetMethod("ReleaseStrike",Flags)));
            foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks).GetMethod("SkipAchievement",Flags)));
            try{Main.windPhysics=false;if(Environment.GetEnvironmentVariable("JMR_PHASEBLADE_ORACLE_ONLY")=="integration"){Integration(context);return;}Natural(context,assembly,model);TapEdges(context,assembly);Motors(context,assembly,model);Shapes(context,model);Integration(context);Workload(context,assembly,model);}
            finally{watchPress=measureReads=watchRelease=false;releaseTarget=null;releaseShot=null;releaseDamageDepth=0;Main.windPhysics=wind;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());}
        }
        private static Player Reset(object context,int weapon)
        {NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),weapon,0,0);p.ResetEffects();p.ResetMeleeHitCooldowns();p.attackCD=0;p.releaseUseItem=true;p.controlUseItem=false;p.itemAnimation=p.itemTime=0;return p;}
        private static void Input(object context,Player p,bool press,Vector2 aim)
        {NativeToolExecutionChecks.Sample(context,Get(context,"Input"),aim,press);p.controlUseItem=press;NativeQuickItemChecks.BeginWorldStep();p.Update(p.whoAmI);}
        private static NPC Target(int slot,Vector2 position,int width,int height)
        {var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.position=position;n.width=width;n.height=height;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static NpcTrajectory Timeline(Assembly assembly,NPC n,bool completed=false,int count=121)
        {
            var key=(NpcIdentity)assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true).GetMethod("Identity",Flags).Invoke(null,new object[]{n,1L});
            var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=true};var points=Enumerable.Range(0,count).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();
            return new NpcTrajectory(key,(long)Main.GameUpdateCount-(completed?0:1),1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static object Terrain(Assembly assembly)
        {var terrain=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");return terrain;}
        private static object Clock(Assembly assembly,NpcTrajectory timeline,string phase,int delay=0)
        {return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{timeline,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),phase),delay},null);}
        private static object Receivers(Assembly assembly,Player p,Projectile q,NpcTrajectory timeline,bool before)
        {return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,timeline,0,before,!before},null);}
        private static Projectile Copy(Projectile p)
        {var q=(Projectile)typeof(object).GetMethod("MemberwiseClone",Flags).Invoke(p,null);q.ai=(float[])p.ai.Clone();q.localAI=(float[])p.localAI.Clone();q.localNPCImmunity=(int[])p.localNPCImmunity.Clone();return q;}
        private static void Natural(object context,Assembly assembly,Type model)
        {
            int cases=0,contacts=0;
            foreach(int weapon in weapons)
            {
                var p=Reset(context,weapon);var aim=p.Center+new Vector2(260,0);int type=p.HeldItem.shoot;
                Require((bool)model.GetMethod("Handles",Flags).Invoke(null,new object[]{type}),"all nineteen actual phaseblade held mappings have their own motor");
                watchPress=true;pressBox=Rectangle.Empty;Input(context,p,true,aim);watchPress=false;Require(!Main.projectile.Any(member=>member.active && member.owner==p.whoAmI && member.type==type) && p.itemAnimation>0 && p.itemTime==0,"one native press begins only melee, without release birth: "+weapon);
                Require(pressBox.Width>0 && pressAnimation==pressMaximum-1,"native first press Damage pose already decremented animation: "+weapon);
                int animation=p.itemAnimation;
                Input(context,p,false,aim);var born=Main.projectile.Where(member=>member.active && member.owner==p.whoAmI && member.type==type).ToArray();
                Require(born.Length==1 && p.itemAnimation==0 && p.itemTime==animation-1,"earliest legal natural release is the next complete update: "+weapon+" born="+born.Length+" time="+p.itemTime+" priorAnimation="+animation);
                var q=born[0];Require(Math.Abs(q.velocity.Length()-p.HeldItem.shootSpeed/p.meleeSpeed)<.001f,"real phaseblade birth retains native melee-speed launch scaling");
                var target=Target(2,new Vector2(q.Center.X+130,q.Center.Y-20),40,40);var timeline=Timeline(assembly,target);var terrain=Terrain(assembly);
                var contact=(AttackContact)model.GetMethod("Known",Flags).Invoke(null,new object[]{p,q,timeline,Clock(assembly,timeline,"BeforeNpc"),terrain,Receivers(assembly,p,q,timeline,true)});int first=0;
                for(int outer=1;outer<=90 && q.active;outer++)
                {NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(2);q.Update(q.whoAmI);if(target.life<10000){first=outer;break;}}
                Require(first>0 && contact!=null && first==contact.Tick,"actual naturally born phaseblade first useful Damage agrees: "+weapon+" native="+first+" plan="+contact?.Tick);contacts++;
                // Recall uses controlUseItem even while cooldown disallows a
                // new StartActualUse. It must not manufacture another shot.
                Input(context,p,true,aim);q.Update(q.whoAmI);Require(q.ai[0]==1 || !q.active,"a real second press recalls the existing native blade");Require(Main.projectile.Count(s=>s.active && s.owner==p.whoAmI && s.type==type)<=1,"original owned-projectile gate blocks duplicate release births");cases++;
                Console.WriteLine("PHASEBLADE NATURAL weapon="+weapon+" type="+type+" earliestRelease=2 nativeFirst="+first+" model="+contact.Tick);
            }
            Require(cases==19 && contacts==19,"phaseblade natural input and real Damage cover the complete held family");
        }
        private static void Motors(object context,Assembly assembly,Type model)
        {
            int cases=0,actions=0,landings=0,catches=0,dynamic=0;
            foreach(int weapon in weapons)foreach(int scene in Enumerable.Range(0,12))
            {
                var p=Reset(context,weapon);p.position=new Vector2(790,350);p.controlUseItem=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();
                var target=Target(2,new Vector2(1400,450),32,40);target.dontTakeDamage=true;
                if(scene>=1 && scene<=4)for(int x=35;x<110;x++)for(int y=10;y<80;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:scene==3?1:3);}
                if(scene>=5 && scene<=8){Main.tile[60,25].active(true);Main.tile[60,25].type=1;Main.tile[60,25].slope((byte)(scene-4));}
                for(int x=35;x<110;x++){Main.tile[x,45].active(true);Main.tile[x,45].type=1;}
                Vector2 origin=scene>=5 && scene<=8?new Vector2(936,390):scene==11?p.Center+new Vector2(6400,0):new Vector2(920,350);
                Vector2 velocity=scene>=5 && scene<=8?new Vector2(24,8):new Vector2(8,-.5f);
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),origin,velocity,p.HeldItem.shoot,0,0,p.whoAmI);var q=Main.projectile[slot];
                if(scene==9)q.ai[0]=2;if(scene==10 || scene==11){q.ai[0]=1;q.timeLeft=4;}
                var shadow=Copy(q);var terrain=Terrain(assembly);
                for(int outer=1;outer<=260 && q.active;outer++)
                {
                    if(outer==90)p.controlUseItem=true;
                    int updates=shadow.extraUpdates+1;
                    for(int sub=0;sub<updates && shadow.active;sub++)
                    {var args=new object[]{shadow,p,terrain,true};Require((bool)model.GetMethod("Advance",Flags).Invoke(null,args),"phaseblade full native movement retains queried dependencies: "+weapon+"/"+scene+"/"+outer);if(!(bool)args[3])break;model.GetMethod("Tail",Flags).Invoke(null,new object[]{shadow});actions++;}
                    q.Update(slot);
                    Require(q.active==shadow.active && Vector2.DistanceSquared(q.position,shadow.position)<.002f && Vector2.DistanceSquared(q.velocity,shadow.velocity)<.0001f && q.ai[0]==shadow.ai[0] && q.ai[1]==shadow.ai[1] && q.tileCollide==shadow.tileCollide && q.extraUpdates==shadow.extraUpdates && q.timeLeft==shadow.timeLeft && q.wet==shadow.wet && q.shimmerWet==shadow.shimmerWet,
                        "native phaseblade outer/motor/landing/return/lifetime agrees: "+weapon+"/"+scene+"/"+outer+" native="+q.position+"/"+q.velocity+"/"+q.ai[0]+","+q.ai[1]+"/"+q.extraUpdates+"/"+q.timeLeft+" model="+shadow.position+"/"+shadow.velocity+"/"+shadow.ai[0]+","+shadow.ai[1]+"/"+shadow.extraUpdates+"/"+shadow.timeLeft);
                    if(q.ai[0]==2)landings++;if(q.extraUpdates>0)dynamic++;
                }
                Require(!q.active,"native blade eventually catches its actual owner after recall");catches++;cases++;
            }
            Require(cases==228 && landings>0 && dynamic>0 && catches==cases,"all phaseblade bodies cover landing, liquid, slopes, dynamic extra and catch");Console.WriteLine("PASS PHASEBLADE motors="+cases+" actions="+actions+" landing="+landings+" dynamic="+dynamic+" catches="+catches);
        }
        private static void TapEdges(object context,Assembly assembly)
        {
            int cases=0;
            foreach(int weapon in weapons)
            {
                var p=Reset(context,weapon);watchPress=true;pressBox=Rectangle.Empty;Input(context,p,true,p.Center+new Vector2(200,0));watchPress=false;var immediate=pressBox;int immediateVisual=pressVisualType;var immediatePosition=p.position;
                // ItemCheck animates with lastVisualizedSelectedItem before
                // its tail copies the new inventory item. Reset's direct
                // inventory replacement is not a native idle update. Match
                // the validation's idle->press sequence for calibration.
                p=Reset(context,weapon);Input(context,p,false,p.Center+new Vector2(200,0));watchPress=true;pressBox=Rectangle.Empty;Input(context,p,true,p.Center+new Vector2(200,0));watchPress=false;var actual=pressBox;var calibrationPosition=p.position;int calibrationAnimation=pressAnimation,calibrationMaximum=pressMaximum;
                Require(pressVisualType==weapon,"native idle updates the visual item before calibrated first press");
                if(weapon==671)
                {
                    Require(immediateVisual!=671 && immediatePosition==calibrationPosition && immediate.Width==actual.Width && immediate.Height==actual.Height && actual.X==immediate.X+8 && actual.Y==immediate.Y+8,"original671 calibration counterexample is the prior visual item's missing native8/8 offsets, with unchanged owner/frame dimensions");
                    Console.WriteLine("PHASEBLADE STALE671 immediateVisual="+immediateVisual+" immediate="+immediate+" calibratedVisual="+pressVisualType+" calibrated="+actual+" itemLocation="+pressLocation);
                }
                Require(actual.Width>0 && actual.Height>4,"native calibration captures a real first-press Damage rectangle");
                foreach(bool outside in new[]{false,true})
                {
                    p=Reset(context,weapon);NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions(false,true,false,false,true,25,true,false));Input(context,p,false,p.Center+new Vector2(200,0));
                    var n=Target(2,new Vector2(outside?actual.Right:actual.Right-2,actual.Y+3),2,2);var timeline=Timeline(assembly,n);var swing=Get(Get(Get(context,"Combat"),"Attack"),"swing");
                    var before=(AttackContact)swing.GetType().GetMethod("Preview",Flags).Invoke(swing,new object[]{p,p.HeldItem,timeline,Clock(assembly,timeline,"BeforeNpc"),Terrain(assembly),1});
                    var completedTimeline=Timeline(assembly,n,true);
                    var completed=(AttackContact)swing.GetType().GetMethod("Preview",Flags).Invoke(swing,new object[]{p,p.HeldItem,completedTimeline,Clock(assembly,completedTimeline,"CompletedWorld"),Terrain(assembly),1});
                    var beforePosition=p.position;int beforeAnimation=p.itemAnimation;watchPress=true;pressBox=Rectangle.Empty;Input(context,p,true,p.Center+new Vector2(200,0));watchPress=false;bool received=n.life<10000;
                    Require(pressVisualType==weapon && pressBox==actual && p.position==calibrationPosition && pressAnimation==calibrationAnimation && pressMaximum==calibrationMaximum,"calibration and validation retain the same native first-press item, owner, pose and animation");
                    Console.WriteLine("PHASEBLADE TAP weapon="+weapon+" outside="+outside+" calibration="+actual+"/"+calibrationPosition+"/"+calibrationAnimation+","+calibrationMaximum+" before="+beforePosition+"/"+beforeAnimation+" actual="+pressBox+"/"+p.position+"/"+pressAnimation+","+pressMaximum+" target="+n.Hitbox+" received="+received+" cooldown="+p.meleeNPCHitCooldown[2]+" ownerImmune="+n.immune[p.whoAmI]);
                    Require(received==!outside && (before!=null)==received && (completed!=null)==received,"idle first tap BeforeNpc/CompletedWorld is the actual native selected receive edge: "+weapon+"/"+outside+" native="+received+" before="+before?.ReceiveTick+" complete="+completed?.ReceiveTick);
                    cases++;
                }
            }
            Console.WriteLine("PASS PHASEBLADE first native press pose/phase receive edges="+cases);
        }
        private static void Shapes(object context,Type model)
        {
            int positives=0,negatives=0;
            foreach(int weapon in weapons)foreach(bool outside in new[]{false,true})
            {
                var p=Reset(context,weapon);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(950,350),Vector2.Zero,p.HeldItem.shoot,100,0,p.whoAmI);var q=Main.projectile[slot];q.ai[0]=2;
                var box=(Rectangle)model.GetMethod("DamageBox",Flags).Invoke(null,new object[]{q});Require(box==q.Damage_GetHitbox() && box.Width==q.width-28,"landing body uses the actual horizontal14 inflation");
                var n=Target(2,new Vector2(outside?box.Right:box.Right-2,box.Y+3),2,8);q.Damage();Require((n.life<10000)==!outside,"actual landing damage respects narrowed horizontal shape: "+weapon+"/"+outside);if(outside)negatives++;else positives++;
            }
            Require(positives==19 && negatives==19,"every actual phaseblade landing body has native positive and negative receive edges");
        }
        private static void Integration(object context)
        {
            int cases=0,positive=0;
            var combat=Get(context,"Combat");var attack=Get(combat,"Attack");var observation=Get(context,"CombatObservation");
            // These representatives cover ordinary blade, the saber damage
            // branch, Keybrand's dynamic extra and the 1102 saber exception.
            foreach(int weapon in new[]{198,3764,671,5671})
            {
                NativeCombatCadenceChecks.Save(combat,new CombatOptions());var p=Reset(context,weapon);p.position=new Vector2(700,646);Main.screenPosition=new Vector2(600,400);
                var target=NativeCombatControlPreviewChecks.Dummy(2,54,40);var aim=p.Center+new Vector2(220,-80);
                NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,false,true,25,true,false));
                for(int i=0;i<3;i++)
                {
                    NativeCombatSelectionPreviewChecks.Step(context,aim,false,true);
                    Require(GetOptional(attack,"ExpectedPreview")!=null,"phaseblade idle AimOFF displays the explicit tap-release observation: "+weapon);
                    Require(!Main.projectile.Any(q=>q.active && q.owner==p.whoAmI) && p.itemAnimation==0 && !p.controlUseItem,"phaseblade idle preview cannot manufacture a press or release");
                }
                NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,false,true,25,true,true));
                NativeCombatSelectionPreviewChecks.Step(context,aim,true);
                Require(!Main.projectile.Any(q=>q.active && q.owner==p.whoAmI && q.type==p.HeldItem.shoot) && p.itemAnimation>0,"actual auxiliary press remains the original melee stage");
                releaseTarget=target;releaseShot=null;releaseDamageDepth=releaseHits=releaseDamage=releasePlanTick=0;firstReleaseDamage=0;
                watchRelease=true;try{NativeCombatSelectionPreviewChecks.Step(context,aim,false);}finally{watchRelease=false;}ulong releaseTick=Main.GameUpdateCount;
                var born=Main.projectile.Where(q=>q.active && q.owner==p.whoAmI && q.type==p.HeldItem.shoot).ToArray();Require(born.Length==1,"auxiliary release consumes the original native launch opportunity");
                Console.WriteLine("PHASEBLADE RELEASEAFTER owner="+p.position+" target="+target.Hitbox+" body="+born[0].Hitbox+" velocity="+born[0].velocity+" ai="+born[0].ai[0]+","+born[0].ai[1]+" life="+target.life+" cooldown="+p.meleeNPCHitCooldown[target.whoAmI]+" local="+born[0].localNPCImmunity[target.whoAmI]+" ownerImmune="+target.immune[p.whoAmI]);
                // Native488 is immortal: StrikeNPC reports useful positive
                // damage and writes immunity but deliberately never subtracts
                // life. Count only the exact released shot's native Damage ->
                // StrikeNPC chain; melee, other shots and overlap cannot pass.
                int life=target.life,first=releaseHits>0?1:-1;Require(target.immortal && life==target.lifeMax,"placed native dummy preserves its immortal life contract");
                for(int i=1;i<=80 && born[0].active;i++)
                {NativeCombatSelectionPreviewChecks.Step(context,aim,false);Require(Main.projectile.Count(q=>q.active && q.owner==p.whoAmI && q.type==p.HeldItem.shoot)<=1,"an actual same-type flight cannot open a hypothetical new hand use");if(i<=12 || !born[0].active)Console.WriteLine("PHASEBLADE RELEASEFLIGHT tick="+i+" center="+born[0].Center+" velocity="+born[0].velocity+" ai="+born[0].ai[0]+","+born[0].ai[1]+" active="+born[0].active+" target="+target.Hitbox+" life="+target.life+" local="+born[0].localNPCImmunity[target.whoAmI]);if(releaseHits>0){first=(int)(firstReleaseDamage-releaseTick)+1;break;}}
                Require(first>=1 && releaseHits>0 && releaseDamage>0,"actual assisted native release produces useful Damage: "+weapon);Require(first==releasePlanTick,"native released projectile's first positive Strike agrees with prepared Contact, including its birth-frame Damage: "+weapon+" native="+first+" plan="+releasePlanTick);Require(target.life==life,"native immortal dummy reports damage without a fabricated life decrement");positive++;cases++;
                Console.WriteLine("PHASEBLADE INTEGRATION weapon="+weapon+" releaseDamageTick="+first+" plan="+releasePlanTick+" actualPositiveDamage="+releaseDamage+" strikes="+releaseHits);releaseTarget=null;releaseShot=null;
            }
            Require(cases==4 && positive==4,"phaseblade idle/press/release/live-flight consumers retain real inputs and useful damage");
        }
        private static void Workload(object context,Assembly assembly,Type model)
        {
            var p=Reset(context,198);var origin=p.RotatedRelativePoint(p.MountedCenter);var n=Target(2,new Vector2(origin.X+180,origin.Y-20),40,40);n.dontTakeDamage=true;
            var timeline=Timeline(assembly,n);var terrain=Terrain(assembly);var policy=new Projectile();policy.SetDefaults(p.HeldItem.shoot);policy.owner=p.whoAmI;
            var ammo=assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true).GetMethod("Capture",Flags).Invoke(null,new object[]{p,p.HeldItem});
            model.GetMethod("ResetCounters",Flags).Invoke(null,null);playerReads=0;measureReads=true;
            AttackContact contact;
            try{contact=(AttackContact)model.GetMethod("ReleaseSolve",Flags).Invoke(null,new object[]{p,ammo,timeline,origin,Clock(assembly,timeline,"BeforeNpc"),terrain,Receivers(assembly,p,policy,timeline,true)});}
            finally{measureReads=false;}
            int candidates=(int)model.GetField("Candidates",Flags).GetValue(null),steps=(int)model.GetField("MotorSteps",Flags).GetValue(null);
            Require(contact==null && playerReads==1 && candidates<=33 && candidates>0 && steps>0 && steps<=41*120*21,"actual unreceivable scalar path shares one owner capture, deduplicates static seeds and preserves finite candidate/outer work");
            Console.WriteLine("PHASEBLADE WORKLOAD nativeReadPlayer="+playerReads+" candidates="+candidates+" motor="+steps);
        }
    }
}
