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
            NativeCombatAmmoChecks.Run(context);
            NativeCombatSkyChecks.Run(context);
            NativeCombatScatterChecks.Run(context);
            NativeCombatEffectChecks.Run(context);
            NativeCombatMeleeChecks.Run(context);
            NativeCombatControlChecks.Run(context);
            foreach(int weaponType in new[]{ItemID.FlintlockPistol,ItemID.WoodenBow})foreach(float targetSpeed in new[]{0f,1f})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());
                var player=NativeToolExecutionChecks.Reset(context,tools,input,weaponType,0,0);player.inventory[54].SetDefaults(weaponType==ItemID.WoodenBow?ItemID.WoodenArrow:ItemID.MusketBall);player.inventory[54].stack=999;
                player.position=new Vector2(400,400);player.velocity=Vector2.Zero;
                Main.screenPosition=new Vector2(300,300);
                var body=Main.npc[2];body.SetDefaults(3);body.whoAmI=2;body.active=true;body.life=body.lifeMax=10000;body.defense=0;body.position=new Vector2(780,410);body.velocity=new Vector2(targetSpeed,0);
                NativeToolExecutionChecks.Sample(context,input,new Vector2(350,150),true);Call(combat,"Sample");
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));
                Prepare(host,attack,body,targetSpeed);
                var contact=(AttackContact)GetOptional(attack,"ExpectedImpact");Require(contact!=null,"prepared real weapon contact: "+weaponType+"/"+targetSpeed);
                var impact=Get(Get(host,"World"),"Impact");Call(impact,"Capture");var zoom=Main.GameViewMatrix.ZoomMatrix;Call(impact,"Project",zoom,Matrix.Invert(zoom));
                Require((bool)Get(impact,"Visible"),"only legal matching prepared contact displays red");
                var projected=(Vector2)Get(impact,"Position");var expected=Vector2.Transform(new Vector2(contact.ImpactX,contact.ImpactY)-Main.screenPosition,zoom);
                Require(Vector2.DistanceSquared(projected,expected)<.001f,"red projects ExpectedImpact rather than elevated AimInput using final camera");
                int oldX=Main.mouseX,oldY=Main.mouseY,stack=player.inventory[54].stack;var rng=Main.rand;
                // Preparation must be genuinely read-only even for native
                // ammo-cycling and conservation policies, not dontConsume.
                var rngFields=typeof(Terraria.Utilities.UnifiedRandom).GetFields(Flags).Where(f=>!f.IsStatic).ToArray();
                var before=rngFields.Select(f=>Clone(f.GetValue(rng))).ToArray();int cycling=player.ammoCyclingOffset;
                Call(attack,"Prepare");
                int immuneAtBirth=body.immune[0];
                Require(stack==player.inventory[54].stack && cycling==player.ammoCyclingOffset && ReferenceEquals(rng,Main.rand) && rngFields.Select((f,i)=>Equal(before[i],f.GetValue(rng))).All(b=>b),"prepare does not consume inventory/RNG/cycling");
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(player,new object[]{0,player.HeldItem,50,false});
                var born=Main.projectile.Where(p=>p.active && p.owner==0).ToArray();Require(born.Length==1,"aim creates exactly the vanilla single shot");
                Require(Main.mouseX==oldX && Main.mouseY==oldY,"real Shoot postfix/finalizer restores physical cursor");
                Call(impact,"Project",zoom,Matrix.Invert(zoom));Require(!(bool)Get(impact,"Visible"),"consumed ordinary capability cannot leave stale red");
                var shot=born[0];Require(shot.type==(weaponType==ItemID.WoodenBow?1:14),"native selected projectile type is preserved");
                var model=weaponType==ItemID.WoodenBow?new AttackMotion(player.HeldItem.shootSpeed+player.inventory[54].shootSpeed,.1f,15,shot.extraUpdates+1,shot.width,shot.height,shot.timeLeft):new AttackMotion(player.HeldItem.shootSpeed+player.inventory[54].shootSpeed,0,0,shot.extraUpdates+1,shot.width,shot.height,shot.timeLeft);
                var native=shot.Center;float x=native.X,y=native.Y,vx=shot.velocity.X,vy=shot.velocity.Y;
                for(int tick=1;tick<=contact.Tick && shot.active;tick++)
                {
                    body.position.X=780+targetSpeed*tick;
                    for(int sub=0;sub<model.Updates;sub++)model.Advance(ref x,ref y,ref vx,ref vy,(tick-1)*model.Updates+sub+1);
                    shot.Update(shot.whoAmI);
                    if(shot.active)Require(Vector2.DistanceSquared(shot.Center,new Vector2(x,y))<.02f,"native AI-before-move matches prepared subupdate motion");
                }
                Require(body.life<10000,"actual native projectile damage reaches fixed/moving target: "+weaponType+"/"+targetSpeed+" immuneAtBirth="+immuneAtBirth+" immuneNow="+body.immune[0]+" position="+shot.position+" target="+body.Hitbox+" active="+shot.active+" tick="+contact.Tick);
                Console.WriteLine("PASS actual ItemCheck_Shoot -> Projectile.Update/Damage: weapon="+weaponType+" targetVx="+targetSpeed+" aim="+contact.AimX+","+contact.AimY+" expected="+contact.ImpactX+","+contact.ImpactY+" tick="+contact.Tick);
                // A changed ammo stack or slot identity rejects the old plan;
                // no virtual input is left installed on a skipped consumer.
                foreach(var p in Main.projectile)p.active=false;
                player.inventory[54].stack++;Require(Call(attack,"BeginShot",player,player.HeldItem)==null,"ammo mutation rejects prepared ownership");
            }
            FirstClick(context,combat,host,input,tools,attack);
            ActionPhase(context,combat,host,input,tools,attack);
            MovingShooter(context,combat,host,input,tools,attack);
            CompletedWorldTiming(context,combat,host,input,tools,attack);
            PreparationBoundaries(context,combat,host,input,tools,attack);
            CursorReceipts(combat);
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
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
                    NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),false);Call(combat,"Sample");Call(host,"SampleMouse");Call(context,"UpdateRuntime");Require(GetOptional(attack,"ExpectedImpact")==null,"idle has no borrowed ordinary attack plan");
                    NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");Call(host,"SampleMouse");NativeQuickItemChecks.BeginWorldStep();p.Update(0);
                    var shot=Main.projectile.Single(q=>q.active && q.owner==0);var direction=Vector2.Normalize(n.Center-shot.Center);
                    Require(Vector2.Dot(direction,Vector2.Normalize(shot.velocity))>.999f,"first natural idle-to-click shot is assisted with Path="+path);
                    Call(context,"UpdateRuntime");NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),false);Call(combat,"Sample");Call(host,"SampleMouse");NativeQuickItemChecks.BeginWorldStep();p.Update(0);Call(context,"UpdateRuntime");Require(Main.projectile.Count(q=>q.active && q.owner==0)==1 && GetOptional(attack,"ExpectedImpact")==null,"release causes no additional projectile or stale contact");
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
                var contact=(AttackContact)GetOptional(attack,"ExpectedImpact");Require(contact==null || ReferenceEquals(contact.Timeline,completed),"post-world red never consumes the early action publication");
                Console.WriteLine("PASS shared action/completed phases: one actual player move, native first NPC step, distinct same-tick path publication and matching red ownership.");
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
                var plan=(AttackContact)GetOptional(attack,"next");
                if(spawnX==850)
                {
                    var timeline=((NpcPredictionCache)Get(Get(host,"Prediction"),"Cache")).Read(1);var spawn=(Vector2)Get(attack,"nextOrigin");Require(timeline!=null && timeline.Count==121 && plan==null,"original tiny-target scene has no strict translated rectangle contact");
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
                Require(plan!=null,"completed-world preparation provides next real player phase");
                uint sampled=Main.GameUpdateCount;Projectile shot=null;int hit=-1;
                for(int frame=1;frame<80 && hit<0;frame++)
                {
                    NativeQuickItemChecks.BeginWorldStep();Call(Get(context,"nativeNpcs"),"BeginTick");
                    if(frame>1){Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft=false;p.controlUseItem=false;}
                    p.Update(0);
                    if(frame==1){shot=Main.projectile.Single(q=>q.active && q.owner==0);Console.WriteLine("TIMING born="+shot.Center+" velocity="+shot.velocity+" planTick="+plan.Tick+" aim="+plan.AimX+","+plan.AimY+" friendly="+shot.friendly+" damage="+shot.damage+" immune="+n.immune[0]);}
                    n.UpdateNPC(2);
                    Require(frame<plan.Timeline.Count && Math.Abs(n.position.X-plan.Timeline[frame].Bounds.X)<.15f && Math.Abs(n.position.Y-plan.Timeline[frame].Bounds.Y)<.15f,"actual NPC update matches the one prepared shared future at frame="+frame);
                    int life=n.life;if(shot.active)shot.Update(shot.whoAmI);if(n.life<life)hit=frame;if(frame==plan.Tick)Console.WriteLine("TIMING predicted frame actualShot="+shot.Hitbox+" npc="+n.Hitbox+" immune="+n.immune[0]+" active="+shot.active+" damage="+shot.damage+" life="+n.life);
                }
                Require(hit>0,"original moving NPC receives natural projectile");
                Require(plan.Timeline.SampleTick+plan.Tick==sampled+hit,"first native contact must use its exact same future index: prepared="+plan.Tick+" actual="+hit);
                var bounds=plan.Timeline[hit].ProjectileReceiveBounds;Require(plan.ImpactX>=bounds.X && plan.ImpactX<=bounds.X+bounds.Width && plan.ImpactY>=bounds.Y && plan.ImpactY<=bounds.Y+bounds.Height,"red belongs to the actual contact tick, not an adjacent future frame");
                Console.WriteLine("PASS completed world T -> Player/Shoot -> moving native NPC -> Projectile/Damage: firstContact="+hit+" timelineIndex="+plan.Tick+" sample="+sampled);
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
            Call(attack,"Prepare");Require((bool)Get(attack,"Failed") && GetOptional(attack,"ExpectedImpact")==null,"persistent failure does not retry work each update");
            Call(attack,"Reset");Prepare(host,attack,n,0);Require(GetOptional(attack,"ExpectedImpact")!=null,"explicit aim recovery restores valid preparation");
            var tile=Main.tile[60,40];Main.tile[60,40]=null;
            try{Call(attack,"Prepare");Require(Main.tile[60,40]==null,"finite geometry acquisition never fills a missing live tile");}
            finally{Main.tile[60,40]=tile;}
            Console.WriteLine("PASS preparation isolation: native up/down, read-only terrain, exception-local path survival, finite latch/recovery.");
        }
        private static bool Equal(object a,object b){if(a is Array && b is Array)return ((Array)a).Cast<object>().SequenceEqual(((Array)b).Cast<object>());return Equals(a,b);}
        internal static void Prepare(object host,object attack,NPC body,float vx,bool projectilePhase=false)
        {
            var selection=Get(host,"Selection");Call(selection,"Update",((ObservationSettings)Get(host,"Settings")).Value,(long)Get(host,"Session"),true,null);
            var id=(NpcIdentity)Get(selection,"Target");Require(id.Slot==body.whoAmI,"single shared final target is selected");
            var points=new NpcTrajectoryPoint[121];for(int i=0;i<points.Length;i++)points[i]=new NpcTrajectoryPoint(i,new NpcMotionState{Identity=id,X=body.position.X+vx*i,Y=body.position.Y,Width=body.width,Height=body.height,Vx=vx,CanReceive=true,Active=true});
            var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");cache.Demand(0,1,120);cache.Demand(1,1,120);cache.Publish(new NpcTrajectory(id,Main.GameUpdateCount,1,PredictionAssumption.None,PredictionStop.None,points,points.Length));Call(attack,projectilePhase?"PrepareProjectiles":"Prepare");
        }
        internal static object PhaseClock(object attack,NpcTrajectory timeline,string phase)
        {var assembly=attack.GetType().Assembly;var stage=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true);var type=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true);return Activator.CreateInstance(type,Flags,null,new object[]{timeline,Enum.Parse(stage,phase)},null);}
    }
}
