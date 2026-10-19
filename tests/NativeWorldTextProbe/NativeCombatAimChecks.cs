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
    internal static class NativeCombatAimChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static void Initialize()
        {
            typeof(Main).GetMethod("Initialize_TileAndNPCData1",Flags).Invoke(null,null);typeof(Main).GetMethod("Initialize_TileAndNPCData2",Flags).Invoke(null,null);
            Terraria.ObjectData.TileObjectData.Initialize();Lighting.Mode=Terraria.Graphics.Light.LightMode.Color;
            NativeCombatWorkerAssetChecks.Initialize();
            // Main's real entity initialization also fills its static gore
            // array. These components execute original Kill/explosion paths,
            // so supply normal original Gore instances, not a fake ABI type.
            for(int i=0;i<Main.gore.Length;i++)Main.gore[i]=new Gore();
            Terraria.GameContent.Creative.CreativePowerManager.Initialize();Terraria.DataStructures.ArmorSetBonuses.Initialize();Terraria.DataStructures.ArmorSetBonuses.BuildLookup();
            if(Main.instance==null){Main.instance=(Main)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Main));GC.SuppressFinalize(Main.instance);}
            PopupText.popupText=new PopupText[20];for(int i=0;i<20;i++)PopupText.popupText[i]=new PopupText();
            foreach(var item in Main.item)item.whoAmI=Array.IndexOf(Main.item,item);
            for(int i=1;i<Main.player.Length;i++)if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};
        }
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var input=Get(context,"Input");var tools=Get(context,"Tools");
            var attack=GetOptional(combat,"Attack");Require(attack!=null,"ordinary aim owner must be composed");Initialize();
            bool ordinaryOnly=Environment.GetEnvironmentVariable("JMR_AIM_ORACLE_ONLY")=="ordinary";
            // Basic consumers and their actual input/safety contract
            // belong to the normal Aim scope, not only a private runner.
            if(!ordinaryOnly)
            {NativeCombatBasicAimChecks.Run(context);NativeCombatAmmoChecks.Run(context);NativeCombatSkyChecks.Run(context);NativeCombatScatterChecks.Run(context);}
            OriginalBorderBeforeItem(context,host,input,tools);
            // Cumulative routing owns Effects, Mechanics, Control and
            // Integration independently. Aim covers its ordinary/ammo/sky/
            // scatter chain once; a specialist Aim PASS is not those scopes.
            foreach(int weaponType in new[]{ItemID.FlintlockPistol,ItemID.WoodenBow})foreach(float targetSpeed in new[]{0f,1f})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());
                var player=NativeToolExecutionChecks.Reset(context,tools,input,weaponType,0,0);player.inventory[54].SetDefaults(weaponType==ItemID.WoodenBow?ItemID.WoodenArrow:ItemID.MusketBall);player.inventory[54].stack=999;
                // The direct Shoot component needs a legal stationary holder:
                // tile43 is the Reset fixture's real floor, so Y=688-height.
                // Translate the old scene by (300,246), retaining its target
                // and physical-cursor offsets instead of changing ballistics.
                player.position=new Vector2(700,43*16-player.height);player.velocity=Vector2.Zero;
                Main.screenPosition=new Vector2(600,546);
                var body=Main.npc[2];body.SetDefaults(3);body.whoAmI=2;body.active=true;body.life=body.lifeMax=10000;body.defense=0;body.position=new Vector2(1080,656);body.velocity=new Vector2(targetSpeed,0);
                NativeToolExecutionChecks.Sample(context,input,new Vector2(650,396),true);Call(combat,"Sample");
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));
                PrepareMechanismFixture(host,attack,body,targetSpeed);
                var solution=RequireSolution(attack,player,"prepared real weapon solution and enhanced input: "+weaponType+"/"+targetSpeed);
                Require(GetOptional(attack,"ExpectedPreview")==null,"retired impact preview contributes no ordinary input demand");
                int oldX=Main.mouseX,oldY=Main.mouseY,stack=player.inventory[54].stack;var rng=Main.rand;
                // Preparation must be genuinely read-only even for native
                // ammo-cycling and conservation policies, not dontConsume.
                var rngFields=typeof(Terraria.Utilities.UnifiedRandom).GetFields(Flags).Where(f=>!f.IsStatic).ToArray();
                var before=rngFields.Select(f=>Clone(f.GetValue(rng))).ToArray();int cycling=player.ammoCyclingOffset;
                var componentPhase=Enum.Parse(attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");
                using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))Call(attack,"PrepareCore",componentPhase);
                solution=RequireSolution(attack,player,"repeated read-only preparation retains its actual solution/input");
                Require(Vector2.DistanceSquared((Vector2)Get(attack,"origin"),(Vector2)Get(attack,"nextOrigin"))<.0001f,"legal grounded direct-shoot holder has the same actual and next native origin");
                Require(GetOptional(attack,"ExpectedPreview")==null,"repeated preparation cannot restore the withdrawn independent preview");
                int immuneAtBirth=body.immune[0];
                Require(stack==player.inventory[54].stack && cycling==player.ammoCyclingOffset && ReferenceEquals(rng,Main.rand) && rngFields.Select((f,i)=>Equal(before[i],f.GetValue(rng))).All(b=>b),"prepare does not consume inventory/RNG/cycling");
                if(weaponType==95 && targetSpeed==0)SolutionCheckpoint(context,attack,"parent95/0-prepared");
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(player,new object[]{0,player.HeldItem,50,false});
                if(weaponType==95 && targetSpeed==0)SolutionCheckpoint(context,attack,"parent95/0-shot");
                var born=Main.projectile.Where(p=>p.active && p.owner==0).ToArray();Require(born.Length==1,"aim creates exactly the vanilla single shot");
                Require(Main.mouseX==oldX && Main.mouseY==oldY,"real Shoot postfix/finalizer restores physical cursor");
                Require(GetOptional(attack,"ExpectedPreview")==null && QueryInput(attack,player).Kind!="Enhanced","native consumption retires enhanced execution and cannot restore the withdrawn independent preview");
                var shot=born[0];Require(shot.type==(weaponType==ItemID.WoodenBow?1:14),"native selected projectile type is preserved");
                if(weaponType==95 && targetSpeed==0)Console.WriteLine("PARENT AIM BIRTH center="+shot.Center+" velocity="+shot.velocity+" updates="+(shot.extraUpdates+1)+" player="+player.position+" gravity="+player.gravDir+" target="+body.position+" targetOffset="+body.netOffset+" solution="+solution.Point+" nativeTick="+solution.NativeTick);
                var model=weaponType==ItemID.WoodenBow?new AttackMotion(player.HeldItem.shootSpeed+player.inventory[54].shootSpeed,.1f,15,shot.extraUpdates+1,shot.width,shot.height,shot.timeLeft):new AttackMotion(player.HeldItem.shootSpeed+player.inventory[54].shootSpeed,0,0,shot.extraUpdates+1,shot.width,shot.height,shot.timeLeft);
                var native=shot.Center;float x=native.X,y=native.Y,vx=shot.velocity.X,vy=shot.velocity.Y;
                for(int tick=1;tick<=solution.NativeTick && shot.active;tick++)
                {
                    body.position.X=1080+targetSpeed*tick;
                    for(int sub=0;sub<model.Updates;sub++)model.Advance(ref x,ref y,ref vx,ref vy,(tick-1)*model.Updates+sub+1);
                    shot.Update(shot.whoAmI);
                    if(shot.active)Require(Vector2.DistanceSquared(shot.Center,new Vector2(x,y))<.02f,"native AI-before-move matches prepared subupdate motion");
                }
                Require(body.life<10000,"actual native projectile damage reaches fixed/moving target: "+weaponType+"/"+targetSpeed+" immuneAtBirth="+immuneAtBirth+" immuneNow="+body.immune[0]+" position="+shot.position+" target="+body.Hitbox+" active="+shot.active+" tick="+solution.NativeTick);
                Console.WriteLine("PASS actual ItemCheck_Shoot -> Projectile.Update/Damage: weapon="+weaponType+" targetVx="+targetSpeed+" aim="+solution.Point+" receiveIndex="+solution.ReceiveTick+" firstIndex="+solution.FirstTick+" nativeTick="+solution.NativeTick+" route="+solution.Route+" optionalContact="+(solution.Contact!=null));
                // The consumed enhanced capability stays retired across an
                // ammo change; a legal consumer retains current basic input.
                foreach(var p in Main.projectile)p.active=false;
                player.inventory[54].stack++;
                var retired=QueryInput(attack,player);Require(retired.Allowed && retired.Kind=="Basic","ammo mutation cannot retain enhanced input while current basic remains legal");
                int manualX=Main.mouseX,manualY=Main.mouseY;
                var basic=Call(attack,"BeginShot",player,player.HeldItem,true);
                Require(basic!=null && Main.mouseX+Main.screenPosition.X>player.Center.X,"ammo mutation preserves legal current-target basic input");
                Call(basic,"End");Require(Main.mouseX==manualX && Main.mouseY==manualY,"basic input returns the original physical coordinates");
            }
            FirstClick(context,combat,host,input,tools,attack);
            ActionPhase(context,combat,host,input,tools,attack);
            MovingShooter(context,combat,host,input,tools,attack);
            CompletedWorldTiming(context,combat,host,input,tools,attack);
            PreparationBoundaries(context,combat,host,input,tools,attack);
            CursorReceipts(combat);
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
        }
        private static void OriginalBorderBeforeItem(object context,object host,object input,object tools)
        {
            // f50646b added the real next-player border premise. The old
            // component placed its holder at (400,400), then called Shoot
            // directly, skipping the original BordersMovement before ItemCheck.
            // Keep that illegal starting state as a native counterexample.
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
            var p=NativeToolExecutionChecks.Reset(context,tools,input,ItemID.FlintlockPistol,0,0);
            p.position=new Vector2(400,400);p.velocity=Vector2.Zero;
            NativeToolExecutionChecks.Sample(context,input,new Vector2(350,150),false);
            var prior=p.position;float cameraX=Main.cameraX,cameraY=Main.cameraY;
            var audit=new Harmony("JueMingR.Tests.AimBorderBeforeItem");
            foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks).GetMethod("SkipAchievement",Flags)));
            try
            {
                NativeQuickItemChecks.BeginWorldStep();p.Update(0);
                Require(p.position.X==Main.leftWorld+640 && p.position.Y==Main.topWorld+640 && p.velocity.X==0 && p.velocity.Y>=.11f && !Main.projectile.Any(q=>q.active && q.owner==0),"Original Player.Update normalizes the old illegal holder before item use without firing: prior="+prior+" now="+p.position+" velocity="+p.velocity);
                Console.WriteLine("PASS original Player.Update old illegal aim-holder boundary prior="+prior+" now="+p.position+" velocity="+p.velocity+" launchOrigin="+p.RotatedRelativePoint(p.MountedCenter)+" noShot=true");
            }
            finally{Main.cameraX=cameraX;Main.cameraY=cameraY;foreach(var method in audit.GetPatchedMethods().ToArray())audit.Unpatch(method,HarmonyPatchType.All,audit.Id);}
        }
        private static void MovingShooter(object context,object combat,object host,object input,object tools,object attack)
        {
            var audit=new Harmony("JueMingR.Tests.AimMovingPlayer");
            // Reuse the established cadence fixture's achievement outlet;
            // Player.Update, movement, ItemCheck and shoot stay original.
            foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks).GetMethod("SkipAchievement",Flags)));
            try
            {
            foreach(bool jumping in new[]{false,true})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());
                var p=NativeToolExecutionChecks.Reset(context,tools,input,ItemID.FlintlockPistol,0,0);p.inventory[54].SetDefaults(ItemID.MusketBall);p.inventory[54].stack=999;
                p.position=new Vector2(660,646);p.velocity=new Vector2(4,jumping?-5:0);Main.screenPosition=new Vector2(600,500);
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(1080,656);n.life=n.lifeMax=10000;n.defense=0;n.target=0;
                NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");p.controlRight=true;p.controlLeft=false;p.controlJump=jumping;Terraria.GameInput.PlayerInput.Triggers.Current.Right=true;Terraria.GameInput.PlayerInput.Triggers.Current.Jump=jumping;
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));Prepare(host,attack,n,0);
                var previous=p.RotatedRelativePoint(p.MountedCenter);NativeQuickItemChecks.BeginWorldStep();
                p.Update(0);
                var born=Main.projectile.Where(q=>q.active && q.owner==0).ToArray();Require(born.Length==1,"natural moving Player.Update produces one original shot");
                Require(Vector2.DistanceSquared(previous,p.RotatedRelativePoint(p.MountedCenter))>4,"moving-shooter positive exceeds old two-pixel refusal");
                var delta=n.Center-born[0].Center;delta.Normalize();
                Require(Vector2.Dot(delta,Vector2.Normalize(born[0].velocity))>.999f,"next-origin plan is consumed by real running/jumping Player.Update: "+jumping+" old="+previous+" now="+p.MountedCenter+" velocity="+born[0].velocity);
                Console.WriteLine("PASS natural Player.Update moving shooter: jumping="+jumping+" originDelta="+(p.RotatedRelativePoint(p.MountedCenter)-previous)+" shot="+born[0].velocity);
            }
            }
            finally{foreach(var method in audit.GetPatchedMethods().ToArray())audit.Unpatch(method,HarmonyPatchType.All,audit.Id);}
        }
        private static object Clone(object value){return value is Array?((Array)value).Clone():value;}
        private static void FirstClick(object context,object combat,object host,object input,object tools,object attack)
        {
            var audit=new Harmony("JueMingR.Tests.AimFirstClick");foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks).GetMethod("SkipAchievement",Flags)));
            try
            {
                foreach(bool path in new[]{false,true})
                {
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,ItemID.FlintlockPistol,0,0);p.position=new Vector2(700,646);p.velocity=Vector2.Zero;Main.screenPosition=new Vector2(600,500);
                    p.inventory[54].SetDefaults(ItemID.MusketBall);p.inventory[54].stack=999;var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(1050,652);n.life=n.lifeMax=10000;n.defense=0;n.target=0;
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,path,false,false,false,25,false,true));
                    NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),false);Call(combat,"Sample");Call(host,"SampleMouse");Call(context,"UpdateRuntime");Require(QueryInput(attack,p).Kind!="Enhanced","idle has no borrowed enhanced input");
                    NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");Call(host,"SampleMouse");NativeQuickItemChecks.BeginWorldStep();p.Update(0);
                    var shot=Main.projectile.Single(q=>q.active && q.owner==0);var direction=Vector2.Normalize(n.Center-shot.Center);
                    Require(Vector2.Dot(direction,Vector2.Normalize(shot.velocity))>.999f,"first natural idle-to-click shot is assisted with Path="+path);
                    Call(context,"UpdateRuntime");NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),false);Call(combat,"Sample");Call(host,"SampleMouse");NativeQuickItemChecks.BeginWorldStep();p.Update(0);Call(context,"UpdateRuntime");Require(Main.projectile.Count(q=>q.active && q.owner==0)==1 && QueryInput(attack,p).Kind!="Enhanced","release causes no additional projectile or stale enhanced input");
                    Console.WriteLine("PASS first natural click/release: Path="+path+" Marker=false, one native shot, no idle virtual plan.");
                }
            }
            finally{foreach(var method in audit.GetPatchedMethods().ToArray())audit.Unpatch(method,HarmonyPatchType.All,audit.Id);}
        }
        private static void ActionPhase(object context,object combat,object host,object input,object tools,object attack)
        {
            var audit=new Harmony("JueMingR.Tests.AimActionPhase");foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks).GetMethod("SkipAchievement",Flags)));
            try
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,ItemID.FlintlockPistol,0,0);p.position=new Vector2(700,646);p.velocity=new Vector2(-4,0);p.inventory[54].SetDefaults(ItemID.MusketBall);p.inventory[54].stack=999;Main.screenPosition=new Vector2(600,300);Main.dayTime=false;
                var n=Main.npc[2];n.SetDefaults(2);n.whoAmI=2;n.active=true;n.position=new Vector2(704-n.width/2f,450-n.height/2f);n.velocity=new Vector2(4,0);n.life=n.lifeMax=10000;n.target=0;
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");Call(host,"SampleMouse");Terraria.GameInput.PlayerInput.Triggers.Current.Left=true;
                NativeQuickItemChecks.BeginWorldStep();p.Update(0);var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");var early=cache.Read(1);Require(early!=null && early.SampleTick==Main.GameUpdateCount-1,"pre-NPC action publication retains the previous completed NPC sample tick");
                n.UpdateNPC(2);Require(Math.Abs(n.velocity.X-early[1].Vx)<.0001f && Math.Abs(n.position.X-early[1].Bounds.X)<.0001f,"first NPC action uses already moved player once: native="+n.velocity.X+" predicted="+early[1].Vx);
                foreach(var q in Main.projectile.Where(q=>q.active && q.owner==0).ToArray())q.Update(q.whoAmI);Call(context,"UpdateRuntime");var completed=cache.Read(0);Require(completed!=null && !ReferenceEquals(completed,early) && completed.SampleTick==Main.GameUpdateCount && completed[0].Bounds.X==(int)n.position.X,"same update Postfix publishes a distinct completed-world sample for the path reader");
                var solution=ReadSolution(attack);var capability=QueryInput(attack,p);
                // A consumed hand may still be cooling down after this world
                // step. Validate a real solution when one exists; otherwise
                // the actual input outlet must not retain enhanced execution.
                // The two distinct native publications above remain mandatory.
                if(solution.Timeline!=null)Require(ReferenceEquals(solution.Timeline,completed),"post-world solution never consumes the early action publication");
                else Require(!solution.HasPoint && capability.Kind!="Enhanced","retired/cooling hand has neither a synthetic solution nor stale enhanced input");
                if(capability.Kind=="Enhanced")Require(solution.HasPoint && ReferenceEquals(solution.Timeline,completed) && capability.Target.Equals(completed.Identity),"an actual enhanced post-world capability belongs to the completed publication");
                Console.WriteLine("PASS shared action/completed phases: one actual player move, native first NPC step, distinct same-tick path publication; actual solution="+(solution.Timeline!=null)+" inputAllowed="+capability.Allowed+" inputKind="+capability.Kind+" permission="+Get(attack,"Permission")+" consumed="+Get(attack,"hasConsumedInput")+" itemTime="+p.itemTime+" animation="+p.itemAnimation);
            }
            finally{foreach(var method in audit.GetPatchedMethods().ToArray())audit.Unpatch(method,HarmonyPatchType.All,audit.Id);}
        }
        private static void CursorReceipts(object combat)
        {
            var scope=combat.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.CombatCursorScope");var begin=scope.GetMethod("Begin",Flags);
            Main.mouseX=21;Main.mouseY=22;var point=Main.screenPosition+new Vector2(100,110);
            var outer=begin.Invoke(null,new object[]{point,true});var inner=begin.Invoke(null,new object[]{point,true});int ownedX=Main.mouseX,ownedY=Main.mouseY;
            Call(outer,"End");Require(Main.mouseX==ownedX && Main.mouseY==ownedY,"late outer receipt cannot revoke same-coordinate successor");
            Call(inner,"End");Call(outer,"End");Require(Main.mouseX==21 && Main.mouseY==22,"innermost completion retires deferred outer cursor once");
            outer=begin.Invoke(null,new object[]{point,true});Main.mouseX=333;Call(outer,"End");Require(Main.mouseX==333 && Main.mouseY==22,"unrelated cursor coordinate ownership is preserved");
            var beginTile=scope.GetMethod("BeginTileTarget",Flags);int priorTileX=Player.tileTargetX,priorTileY=Player.tileTargetY;float priorGravity=Main.LocalPlayer.gravDir;
            try
            {
                foreach(float gravity in new[]{1f,-1f})
                {
                    Main.LocalPlayer.gravDir=gravity;Player.tileTargetX=31;Player.tileTargetY=32;
                    point=new Vector2(813.75f,957.25f);outer=beginTile.Invoke(null,new object[]{point,true});
                    float worldY=gravity==-1?Main.screenPosition.Y+Main.screenHeight-Main.mouseY:Main.screenPosition.Y+Main.mouseY;
                    int expectedX=Math.Max(5,Math.Min(Main.maxTilesX-5,(int)((Main.mouseX+Main.screenPosition.X)/16f))),expectedY=Math.Max(5,Math.Min(Main.maxTilesY-5,(int)(worldY/16f)));
                    Require(Player.tileTargetX==expectedX && Player.tileTargetY==expectedY,"tile birth consumes paired integer mouse mapping in either gravity");
                    inner=beginTile.Invoke(null,new object[]{point,true});Call(outer,"End");Require(Player.tileTargetX==expectedX && Player.tileTargetY==expectedY,"late tile owner preserves nested successor");
                    Call(inner,"End");Call(outer,"End");Require(Player.tileTargetX==31 && Player.tileTargetY==32,"nested tile pair restores once");
                    outer=beginTile.Invoke(null,new object[]{new Vector2(-5000,-5000),true});Require(Player.tileTargetX==5 && Player.tileTargetY==5,"native low-world tile clamp");
                    Player.tileTargetX=77;Call(outer,"End");Require(Player.tileTargetX==77 && Player.tileTargetY==32,"foreign tile coordinate ownership survives finalizer");
                }
            }
            finally{Player.tileTargetX=priorTileX;Player.tileTargetY=priorTileY;Main.LocalPlayer.gravDir=priorGravity;}
            Console.WriteLine("PASS cursor receipts: successor identity, repeated finalizer, independent coordinate ownership.");
        }
        private static void CompletedWorldTiming(object context,object combat,object host,object input,object tools,object attack)
        {
            var audit=new Harmony("JueMingR.Tests.AimTiming");foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks).GetMethod("SkipAchievement",Flags)));
            try
            {
                foreach(int spawnX in new[]{850,847})
                {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,ItemID.FlintlockPistol,0,0);
                p.position=new Vector2(700,646);p.velocity=Vector2.Zero;Main.screenPosition=new Vector2(600,500);Main.dayTime=false;
                p.inventory[54].SetDefaults(ItemID.MusketBall);p.inventory[54].stack=999;
                var n=Main.npc[2];n.SetDefaults(2);n.whoAmI=2;n.active=true;n.position=new Vector2(spawnX,662);n.velocity=new Vector2(4,0);n.width=n.height=2;n.life=n.lifeMax=10000;n.defense=0;n.target=0;
                NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));Call(Get(context,"nativeNpcs"),"BeginTick");Call(host,"Update",(ulong)Main.GameUpdateCount);
                var plan=ReadSolution(attack,true);
                if(spawnX==850)
                {
                    var timeline=((NpcPredictionCache)Get(Get(host,"Prediction"),"Cache")).Read(1);var spawn=(Vector2)Get(attack,"nextOrigin");Require(timeline!=null && timeline.Count==121 && (!plan.HasPoint || plan.Reachability!="Reachable"),"original tiny-target geometry has no reachable actual solution");
                    var bullet=ContentSamples.ProjectilesByType[14];float gap=float.MaxValue;
                    for(int k=1;k<(timeline.Count-1)*(bullet.extraUpdates+1)+1;k++)
                    {
                        var box=timeline[(k-1)/(bullet.extraUpdates+1)+1].ProjectileReceiveBounds;float left=box.X-bullet.width/2f+1-spawn.X,right=box.X+box.Width+bullet.width/2f-spawn.X,top=box.Y-bullet.height/2f+1-spawn.Y,bottom=box.Y+box.Height+bullet.height/2f-spawn.Y;
                        float cx=Math.Max(left,Math.Min(right,0)),cy=Math.Max(top,Math.Min(bottom,0));float min=(float)Math.Sqrt(cx*cx+cy*cy),max=(float)Math.Sqrt(Math.Max(left*left,right*right)+Math.Max(top*top,bottom*bottom));float radius=10*k;
                        Require(radius<=min || radius>=max,"no strict launch-circle intersection at step="+k+" min="+min+" radius="+radius+" max="+max);
                        gap=Math.Min(gap,Math.Max(min-radius,radius-max));
                    }
                    Console.WriteLine("PASS retained tiny-target negative: all discrete subupdate radii are outside integer damage contact region; minimum gap="+gap+" updates="+(bullet.extraUpdates+1)+"; positive shifts birth X by three pixels.");continue;
                }
                Require(plan.HasPoint && plan.Timeline!=null && plan.Reachability=="Reachable" && plan.NativeTick>0,"completed-world preparation provides next real player solution phase");
                uint sampled=Main.GameUpdateCount;Projectile shot=null;int hit=-1;
                for(int frame=1;frame<80 && hit<0;frame++)
                {
                    NativeQuickItemChecks.BeginWorldStep();Call(Get(context,"nativeNpcs"),"BeginTick");
                    if(frame>1){Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft=false;p.controlUseItem=false;}
                    p.Update(0);
                    if(frame==1){shot=Main.projectile.Single(q=>q.active && q.owner==0);Console.WriteLine("TIMING born="+shot.Center+" velocity="+shot.velocity+" receiveIndex="+plan.ReceiveTick+" firstIndex="+plan.FirstTick+" nativeTick="+plan.NativeTick+" aim="+plan.Point+" friendly="+shot.friendly+" damage="+shot.damage+" immune="+n.immune[0]);}
                    n.UpdateNPC(2);
                    Require(frame<plan.Timeline.Count && Math.Abs(n.position.X-plan.Timeline[frame].Bounds.X)<.15f && Math.Abs(n.position.Y-plan.Timeline[frame].Bounds.Y)<.15f,"actual NPC update matches the one prepared shared future at frame="+frame);
                    int life=n.life;if(shot.active)shot.Update(shot.whoAmI);if(n.life<life)hit=frame;if(frame==plan.NativeTick)Console.WriteLine("TIMING predicted frame actualShot="+shot.Hitbox+" npc="+n.Hitbox+" immune="+n.immune[0]+" active="+shot.active+" damage="+shot.damage+" life="+n.life);
                }
                Require(hit>0,"original moving NPC receives natural projectile");
                Require(plan.Timeline.SampleTick+plan.ReceiveTick==sampled+hit,"first native contact uses the solution's exact absolute receive time: receive="+plan.ReceiveTick+" actual="+hit);
                var bounds=plan.Timeline[plan.ReceiveTick].ProjectileReceiveBounds;
                if(plan.Contact!=null)Require(plan.Contact.ImpactX>=bounds.X && plan.Contact.ImpactX<=bounds.X+bounds.Width && plan.Contact.ImpactY>=bounds.Y && plan.Contact.ImpactY<=bounds.Y+bounds.Height,"dedicated contact belongs to the actual receive tick, not an adjacent future frame");
                else
                {
                    // Original Damage temporarily adds netOffset, then reads
                    // the integer NPC hitbox. Compare that receiving geometry,
                    // not its continuous position to a truncated rectangle.
                    var actualReceive=new Rectangle((int)(n.position.X+n.netOffset.X),(int)(n.position.Y+n.netOffset.Y),n.width,n.height);
                    Require(n.type==2 && actualReceive.X==bounds.X && actualReceive.Y==bounds.Y && actualReceive.Width==bounds.Width && actualReceive.Height==bounds.Height,"native damage is independently observed at the representative solution's exact integer receive rectangle: native="+actualReceive+" predicted="+bounds.X+","+bounds.Y+","+bounds.Width+","+bounds.Height+"; no Impact is fabricated");
                }
                Console.WriteLine("PASS completed world T -> Player/Shoot -> moving native NPC -> Projectile/Damage: firstContact="+hit+" receiveIndex="+plan.ReceiveTick+" sample="+sampled+" optionalContact="+(plan.Contact!=null));
                }
            }
            finally{foreach(var method in audit.GetPatchedMethods().ToArray())audit.Unpatch(method,HarmonyPatchType.All,audit.Id);}
        }
        private static void PreparationBoundaries(object context,object combat,object host,object input,object tools,object attack)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,ItemID.FlintlockPistol,0,0);
            p.position=new Vector2(660,646);p.inventory[54].SetDefaults(ItemID.MusketBall);p.inventory[54].stack=999;
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(1080,656);
            NativeToolExecutionChecks.Sample(context,input,new Vector2(600,500),true);Call(combat,"Sample");
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));Collision.up=Collision.down=true;Prepare(host,attack,n,0);
            Require(Collision.up && Collision.down,"preparation preserves nondefault native contact flags");
            var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");var healthy=cache.Read(0);var solid=Main.tileSolid;
            try{Main.tileSolid=null;Call(attack,"Prepare");}
            finally{Main.tileSolid=solid;}
            Require(Collision.up && Collision.down && (bool)Get(attack,"Failed") && ReferenceEquals(cache.Read(0),healthy),"preparation exception preserves native flags and healthy shared path, latching only aim");
            Call(attack,"Prepare");Require((bool)Get(attack,"Failed") && QueryInput(attack,p).Kind!="Enhanced","persistent failure does not retry work or retain enhanced input each update");
            p.inventory[0].SetDefaults(ItemID.Musket);Prepare(host,attack,n,0);Require(!(bool)Get(attack,"Failed"),"a healthy weapon type in the same slot recovers without explicit reset");RequireSolution(attack,p,"recovered healthy weapon provides actual enhanced solution");
            p.inventory[0].SetDefaults(ItemID.FlintlockPistol);Prepare(host,attack,n,0);Require((bool)Get(attack,"Failed"),"returning to the retained bad weapon/target context does not retry it");
            p.inventory[0]=new Item();p.inventory[0].SetDefaults(ItemID.FlintlockPistol);Prepare(host,attack,n,0);Require(!(bool)Get(attack,"Failed"),"a new weapon instance does not inherit the previous instance fault");RequireSolution(attack,p,"new healthy weapon instance provides actual enhanced solution");
            Call(attack,"FailLocal",new InvalidOperationException("isolated target fault"));Require((bool)Get(attack,"Failed"),"injected execution error records the current full identity");
            var oldPosition=n.position;n=(NPC)typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{2,(byte)0});n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=oldPosition;
            Prepare(host,attack,n,0);Require(!(bool)Get(attack,"Failed"),"native slot reuse with a new NPC generation restores healthy execution");RequireSolution(attack,p,"new target generation provides actual enhanced solution");
            Call(attack,"Reset");Prepare(host,attack,n,0);RequireSolution(attack,p,"explicit aim recovery restores actual solution and enhanced input");
            bool fatal=false;try{Call(attack,"FailLocal",new InvalidOperationException("reflection wrapper",new OutOfMemoryException("fatal oracle")));}catch(TargetInvocationException error){fatal=error.InnerException is OutOfMemoryException;}
            Require(fatal && !(bool)Get(attack,"Failed"),"wrapped fatal exceptions propagate without becoming a recoverable aim latch");
            var tile=Main.tile[60,40];Main.tile[60,40]=null;
            try{Call(attack,"Prepare");Require(Main.tile[60,40]==null,"finite geometry acquisition never fills a missing live tile");}
            finally{Main.tile[60,40]=tile;}
            Console.WriteLine("PASS preparation isolation: native up/down, read-only terrain, exception-local path survival, finite latch/recovery.");
        }
        private static bool Equal(object a,object b){if(a is Array && b is Array)return ((Array)a).Cast<object>().SequenceEqual(((Array)b).Cast<object>());return Equals(a,b);}
        internal static void Prepare(object host,object attack,NPC body,float vx,bool projectilePhase=false)
        {PublishFixture(host,body,vx);Call(attack,projectilePhase?"PrepareProjectiles":"Prepare");}
        internal static void PrepareBeforeNpc(object host,object attack,NPC body,float vx)
        {
            // Direct ItemCheck_Shoot observes the pre-NPC epoch. Keep the
            // ordinary allowance while providing that phase's T-1 sample.
            PublishFixtureAt(host,body,vx,(long)Main.GameUpdateCount-1);
            Call(attack,"PrepareAction");
        }
        // Complete-model/native-outcome components have their own finite work
        // scope. They do not assert that this synthetic121 future can be built
        // during a cold real input allowance. Native Shoot still uses the normal
        // operation/permission outlet and retains its real owner protection.
        internal static void PrepareMechanismFixture(object host,object attack,NPC body,float vx,bool projectilePhase=false,string phaseName=null)
        {
            // A fixture inside the actual pre-NPC Shoot entry still observes
            // T-1 NPCs. Match that real phase instead of moving native clocks.
            PublishFixtureAt(host,body,vx,phaseName=="BeforeNpc"?(long)Main.GameUpdateCount-1:(long)Main.GameUpdateCount);
            var phase=attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true);
            using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))Call(attack,"PrepareCore",Enum.Parse(phase,phaseName??(projectilePhase?"Projectiles":"CompletedWorld")));
        }
        // Retain the unique three-argument reflection boundary used by the
        // existing navigation oracle; MethodInfo.Invoke does not fill C#
        // optional parameters. The explicit phase caller uses a named core.
        private static void PublishFixture(object host,NPC body,float vx)
        {PublishFixtureAt(host,body,vx,null);}
        private static void PublishFixtureAt(object host,NPC body,float vx,long? sampleTick)
        {
            var selection=Get(host,"Selection");Call(selection,"Update",((ObservationSettings)Get(host,"Settings")).Value,(long)Get(host,"Session"),true,null);
            var id=(NpcIdentity)Get(selection,"Target");Require(id.Slot==body.whoAmI,"single shared final target is selected");
            var points=new NpcTrajectoryPoint[121];for(int i=0;i<points.Length;i++)points[i]=new NpcTrajectoryPoint(i,new NpcMotionState{Identity=id,X=body.position.X+vx*i,Y=body.position.Y,Width=body.width,Height=body.height,Vx=vx,A0=body.ai[0],Wet=body.wet,NoTileCollide=body.noTileCollide,CanReceive=true,Active=true});
            // The synthetic component path reads no NPC terrain. Initialize its empty
            // terrain lease against real world dimensions before publication;
            // an uninitialized lease cannot authorize the display component.
            var prediction=Get(host,"Prediction");Call(Get(prediction,"Terrain"),"Reset");
            var cache=(NpcPredictionCache)Get(prediction,"Cache");cache.Demand(0,1,120);cache.Demand(1,1,120);cache.Publish(new NpcTrajectory(id,sampleTick??Main.GameUpdateCount,1,PredictionAssumption.None,PredictionStop.None,points,points.Length));
        }
        internal static void PrepareDisplay(object host,object attack)
        {
            // This synthetic trajectory helper belongs only to components.
            // Explicitly perform Host's distinct display capture/validation
            // and preview duties. Its pre-published input is not evidence of
            // natural first-click preparation or display scheduling.
            if(!(bool)Get(host,"Path"))return;
            host.GetType().GetMethod("CaptureDisplay",Flags,null,new[]{typeof(long)},null).Invoke(host,new object[]{(long)Main.GameUpdateCount});
            Call(host,"ValidateDisplay",(long)Main.GameUpdateCount);
            Call(attack,"PreparePreviewOnly");Call(attack,"ValidatePreviewPresentation");
        }
        internal static object PhaseClock(object attack,NpcTrajectory timeline,string phase)
        {var assembly=attack.GetType().Assembly;var stage=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true);var type=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true);return Activator.CreateInstance(type,Flags,null,new object[]{timeline,Enum.Parse(stage,phase)},null);}
        internal struct SolutionFacts
        {
            internal NpcTrajectory Timeline;internal Vector2 Point;internal int ReceiveTick,NativeTick,FirstTick,Delay;
            internal AttackContact Contact;internal bool HasPoint;internal string Reachability,Route,Phase;
        }
        internal struct InputFacts
        {
            internal bool Allowed;internal string Kind;internal Vector2 Point;internal NpcIdentity Target;
        }
        // Facts-only component adapters. Reading a private candidate/controller
        // is not an Aim permission query and never lends/records input. A caller
        // separately proves plan freshness and the real consumer's permission.
        internal static SolutionFacts ReadSolutionValue(object attack,object solution,string phase,int launchDelay=0)
        {
            var facts=new SolutionFacts{Route="representative",Phase=phase,Delay=launchDelay,ReceiveTick=-1,NativeTick=-1,FirstTick=-1,Reachability="Unknown"};
            if(solution==null)return facts;
            facts.Timeline=(NpcTrajectory)GetOptional(solution,"Timeline");facts.Point=(Vector2)Get(solution,"Point");facts.HasPoint=(bool)Get(solution,"HasPoint");
            facts.ReceiveTick=(int)Get(solution,"ReceiveTick");facts.Reachability=Get(solution,"Reachability").ToString();facts.Contact=(AttackContact)GetOptional(solution,"Contact");
            return FinishFacts(attack,facts);
        }
        internal static SolutionFacts ReadPlanSolution(object attack,object plan)
        {
            if(plan==null)return new SolutionFacts{Route="unprepared-plan",Phase="None",Reachability="Unknown",ReceiveTick=-1,NativeTick=-1,FirstTick=-1};
            string phase=Get(plan,"Phase").ToString();int delay=(int)Get(plan,"Delay");
            if((bool)Get(plan,"Balanced"))
            {
                // An observed held body supplies its own cooldown to the birth
                // adapter. PrepareBalancedPreview uses clock delay zero there;
                // an opening instead uses the plan's future-hand delay.
                var held=Main.LocalPlayer.HeldItem.shoot==615 || Main.LocalPlayer.HeldItem.shoot==630 || Main.LocalPlayer.HeldItem.shoot==705?ReadObservedHeldSource(attack):null;
                var value=ReadSolutionValue(attack,Get(plan,"Solution"),phase,held==null?delay:0);
                value.Delay=held==null?delay:Math.Max(0,(int)Math.Ceiling(((Projectile)Get(held,"Shot")).ai[1])-1);
                return value;
            }
            var contact=(AttackContact)GetOptional(plan,"Contact");
            var facts=new SolutionFacts{Route="dedicated-contact",Phase=phase,Delay=delay,ReceiveTick=-1,NativeTick=-1,FirstTick=-1,Reachability="Unknown",Contact=contact};
            if(contact!=null){facts.Delay=contact.Delay;facts.Timeline=contact.Timeline;facts.Point=new Vector2(contact.AimX,contact.AimY);facts.HasPoint=true;facts.ReceiveTick=contact.ReceiveTick;facts.Reachability="Reachable";}
            return FinishFacts(attack,facts);
        }
        internal static object ReadObservedHeldSource(object attack)
        {
            var control=Get(attack,"Control");
            foreach(System.Collections.DictionaryEntry pair in (System.Collections.IDictionary)Get(control,"shots"))
            {
                var entry=pair.Value;int kind=(int)Get(entry,"Type");if(kind!=615 && kind!=630 && kind!=705)continue;
                var observed=control.GetType().GetMethod("Observed",Flags,null,new[]{entry.GetType()},null);
                if((bool)observed.Invoke(control,new[]{entry}))return entry;
            }
            return null;
        }
        private static SolutionFacts FinishFacts(object attack,SolutionFacts facts)
        {
            if(facts.Timeline==null)return facts;
            var assembly=attack.GetType().Assembly;var phase=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true);var type=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true);
            var clock=Activator.CreateInstance(type,Flags,null,new object[]{facts.Timeline,Enum.Parse(phase,facts.Phase),facts.Delay},null);
            facts.FirstTick=(int)Get(clock,"FirstTick");facts.NativeTick=facts.Contact!=null?facts.Contact.Tick:facts.ReceiveTick<facts.FirstTick?-1:facts.ReceiveTick-facts.FirstTick+1;
            return facts;
        }
        // Read the actual post-7a8f18a ordinary solution. A representative point
        // is not a fabricated full Contact; dedicated solvers retain theirs.
        // These are finite component observations, never a second input owner.
        internal static SolutionFacts ReadSolution(object attack,bool next=false)
        {
            bool balanced=(bool)Get(attack,"balancedOrdinary");bool future=next || Main.GameUpdateCount>(uint)Get(attack,"prepared");
            string phase=Get(attack,"preparedOrdinaryPhase").ToString();
            var result=new SolutionFacts{Route=balanced?"representative":"dedicated-contact",Phase=phase,ReceiveTick=-1,NativeTick=-1,FirstTick=-1};
            if(balanced)
            {
                var value=Get(attack,future?"nextSolution":"currentSolution");
                result.Timeline=(NpcTrajectory)GetOptional(value,"Timeline");result.Point=(Vector2)Get(value,"Point");result.HasPoint=(bool)Get(value,"HasPoint");
                result.ReceiveTick=(int)Get(value,"ReceiveTick");result.Reachability=Get(value,"Reachability").ToString();result.Contact=(AttackContact)GetOptional(value,"Contact");
            }
            else
            {
                // Snapshot the prepared field before any validity/consumer query.
                // ExpectedImpact may ClearExecution when its live receiver gate
                // fails; an observation must not erase the cause being tested.
                result.Contact=(AttackContact)GetOptional(attack,future?"next":"current");
                if(result.Contact!=null){result.Timeline=result.Contact.Timeline;result.Point=new Vector2(result.Contact.AimX,result.Contact.AimY);result.HasPoint=true;result.ReceiveTick=result.Contact.ReceiveTick;result.Reachability="Reachable";}
            }
            if(result.Timeline!=null)
            {
                result.FirstTick=(int)Get(PhaseClock(attack,result.Timeline,phase),"FirstTick");
                result.NativeTick=result.Contact!=null?result.Contact.Tick:result.ReceiveTick<result.FirstTick?-1:result.ReceiveTick-result.FirstTick+1;
            }
            return result;
        }
        internal static InputFacts QueryInput(object attack,Player player,string phase="BeforeNpc")
        {
            var stage=attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true);
            var args=new object[]{player,player.HeldItem,Enum.Parse(stage,phase),null};
            bool allowed=(bool)attack.GetType().GetMethod("TryOrdinaryInput",Flags).Invoke(attack,args);
            return new InputFacts{Allowed=allowed,Kind=allowed?Get(args[3],"Kind").ToString():"None",Point=allowed?(Vector2)Get(args[3],"Point"):Vector2.Zero,Target=allowed?(NpcIdentity)Get(args[3],"Target"):default(NpcIdentity)};
        }
        internal static SolutionFacts RequireSolution(object attack,Player player,string reason)
        {
            var value=ReadSolution(attack);var input=QueryInput(attack,player);
            Require(value.HasPoint && value.Timeline!=null && value.Reachability=="Reachable" && value.ReceiveTick>=0 && value.ReceiveTick<value.Timeline.Count && value.NativeTick>0 &&
                !float.IsNaN(value.Point.X) && !float.IsNaN(value.Point.Y) && !float.IsInfinity(value.Point.X) && !float.IsInfinity(value.Point.Y) && input.Allowed && input.Kind=="Enhanced" && input.Target.Equals(value.Timeline.Identity) && Vector2.DistanceSquared(input.Point,value.Point)<.0001f,reason);
            if(value.Contact!=null)Require(ReferenceEquals(value.Contact.Timeline,value.Timeline) && value.Contact.ReceiveTick==value.ReceiveTick && Vector2.DistanceSquared(new Vector2(value.Contact.AimX,value.Contact.AimY),value.Point)<.0001f,"Optional dedicated Contact agrees with its actual solution clock/target/point");
            return value;
        }
        internal static void RequireConsumedInput(object attack,Player player,SolutionFacts solution,string reason,string phase="BeforeNpc")
        {
            // RecordInput is published only by the native cursor scope's Begin,
            // never by QueryInput. Call after the real hand Shoot, with the
            // solution sealed before Shoot; the hand has no source projectile.
            var consumed=Get(attack,"consumedInput");
            Require((bool)Get(attack,"hasConsumedInput") && (uint)Get(attack,"consumedInputStep")==Main.GameUpdateCount &&
                Get(consumed,"Kind").ToString()=="Enhanced" && Get(consumed,"Phase").ToString()==phase &&
                ReferenceEquals(Get(consumed,"Weapon"),player.HeldItem) && GetOptional(consumed,"Source")==null &&
                ((NpcIdentity)Get(consumed,"Target")).Equals(solution.Timeline.Identity) &&
                Vector2.DistanceSquared((Vector2)Get(consumed,"Point"),solution.Point)<.0001f,reason);
            Console.WriteLine("NATIVE HAND CONSUMED tick="+Main.GameUpdateCount+" phase="+phase+" target="+solution.Timeline.Identity.Slot+" kind="+Get(consumed,"Kind")+" point="+Get(consumed,"Point")+" source=hand route="+solution.Route+" receive="+solution.ReceiveTick+" first="+solution.FirstTick);
        }
        internal static void SolutionCheckpoint(object context,object attack,string stage)
        {
            var host=Get(context,"CombatObservation");var movement=Get(context,"Movement");var assembly=attack.GetType().Assembly;var player=Main.LocalPlayer;
            var motion=(PredictionPlayerMotion)Get(host,"Prediction").GetType().GetMethod("ReadPlayer",Flags).Invoke(null,new object[]{player});
            string hash;using(var sha=System.Security.Cryptography.SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(System.IO.File.ReadAllBytes(assembly.Location))).Replace("-","");
            Console.WriteLine("AMMO CURRENT CHECKPOINT stage="+stage+" hostMvid="+assembly.ManifestModule.ModuleVersionId+" sha256="+hash+" tick="+Main.GameUpdateCount+" prepared="+Get(attack,"prepared")+" phase="+Get(attack,"preparedOrdinaryPhase")+" balanced="+Get(attack,"balancedOrdinary")+" failed="+Get(attack,"Failed")+" permission="+Get(attack,"Permission")+" receiver="+Get(attack,"CurrentReceiver")+" movement="+Call(movement,"IsEnabled",0)+","+Call(movement,"IsEnabled",1)+","+Call(movement,"IsEnabled",2)+" assisted="+motion.AssistedJump+" intentVersion="+motion.AssistedJumpVersion);
            var nextMotion=(PredictionPlayerMotion)Get(attack,"nextBalancedMotion");var preparedMotion=(PredictionPlayerMotion)Get(attack,"preparedMotion");
            Console.WriteLine("AMMO ORIGIN stage="+stage+" origin="+Get(attack,"origin")+" nextOrigin="+Get(attack,"nextOrigin")+" currentPlayer="+player.position+" velocity="+player.velocity+" mountedCenter="+player.MountedCenter+" observed="+motion.X+","+motion.Y+"/"+motion.Vx+","+motion.Vy+" observedGravity="+motion.Gravity+"/"+motion.GravityDirection+" preparedMotion="+preparedMotion.X+","+preparedMotion.Y+"/"+preparedMotion.Vx+","+preparedMotion.Vy+" nextMotion="+nextMotion.X+","+nextMotion.Y+"/"+nextMotion.Vx+","+nextMotion.Vy+" nextComplex="+nextMotion.Complex+" nextWall="+nextMotion.WallGross+","+nextMotion.WallTongued+" nextMount="+nextMotion.Mounted+" originalFilm="+player.isControlledByFilm);
            foreach(string field in new[]{"currentSolution","nextSolution"})
            {
                var value=Get(attack,field);var timeline=(NpcTrajectory)GetOptional(value,"Timeline");
                Console.WriteLine("AMMO SOLUTION field="+field+" point="+Get(value,"Point")+" has="+Get(value,"HasPoint")+" reach="+Get(value,"Reachability")+" receive="+Get(value,"ReceiveTick")+" contact="+(GetOptional(value,"Contact")!=null)+" sample="+(timeline?.SampleTick??-1)+" count="+(timeline?.Count??0)+" target="+(timeline==null?"none":timeline.Identity.Slot+"/"+timeline.Identity.Generation+"/"+timeline.Identity.Type)+" rawCurrent="+(GetOptional(attack,"current")!=null)+" rawNext="+(GetOptional(attack,"next")!=null));
            }
            var input=QueryInput(attack,player);var consumed=Get(attack,"consumedInput");
            Console.WriteLine("AMMO INPUT query="+input.Allowed+" kind="+input.Kind+" point="+input.Point+" target="+input.Target.Slot+" consumed="+Get(attack,"hasConsumedInput")+" consumedTick="+Get(attack,"consumedInputStep")+" consumedKind="+Get(consumed,"Kind")+" consumedPoint="+Get(consumed,"Point")+" consumedSource="+(GetOptional(consumed,"Source")!=null));
        }
    }
}
