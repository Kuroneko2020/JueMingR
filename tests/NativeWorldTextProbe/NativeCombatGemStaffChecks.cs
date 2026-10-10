using System;
using System.Collections.Generic;
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
    internal static class NativeCombatGemStaffChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        // Raw SetWeaponDefaults still contains 6171, but locked .8 marks it
        // Deprecated and SetDefaults turns it to air. It is not a selectable
        // eighth staff; never enable it just to manufacture a birth oracle.
        private static readonly int[] weapons={739,740,741,742,743,744,3377};
        private static readonly int[] robes={0,1282,1283,1284,1285,1286,1287,4256};
        private static NPC target;private static long firstHit;private static int damageDepth;
        private static Projectile receiving;private static bool recordBirth;private static string actualBirth,actualReceive;
        private static bool Gem(int t){return t>=121 && t<=126 || t==597;}
        private static void Born(int __result)
        {if(recordBirth && __result>=0 && __result<Main.maxProjectiles){var q=Main.projectile[__result];if(Gem(q.type))actualBirth+=(actualBirth==null?"":";")+q.type+"/"+q.position+"/"+q.velocity+"/"+q.ai[0]+"/"+q.ai[1];}}
        private static void BeforeDamage(Projectile __instance){if(Gem(__instance.type)){damageDepth++;receiving=__instance;}}
        private static void AfterDamage(Projectile __instance){if(Gem(__instance.type))damageDepth--;}
        private static void Strike(NPC __instance,int __result){if(damageDepth>0 && ReferenceEquals(__instance,target) && __result>0 && firstHit<0){firstHit=Main.GameUpdateCount;actualReceive=receiving.type+"/"+receiving.position+"/"+receiving.velocity+"/"+receiving.width+"x"+receiving.height;}}
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var audit=new Harmony("JueMingR.Tests.GemStaff");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatGemStaffChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatGemStaffChecks),nameof(AfterDamage)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatGemStaffChecks),nameof(Strike)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatGemStaffChecks),nameof(Born)));
            try
            {
                string leaf=Environment.GetEnvironmentVariable("JMR_GEM_ORACLE_ONLY");var assembly=combat.GetType().Assembly;
                if(leaf==null || leaf=="birth")Births(context,assembly);
                if(leaf==null || leaf=="motor")Motor(context,assembly);
                if(leaf==null || leaf=="collision")Collisions(context,assembly);
                if(leaf==null || leaf=="natural")Natural(context,host,combat);
            }
            finally{target=null;recordBirth=false;receiving=null;damageDepth=0;Main.windPhysics=wind;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());}
        }
        private static Player Reset(object context,int weapon,int robe)
        {
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),weapon,0,0);p.position=new Vector2(650,646);p.ResetEffects();p.ResetFallDamage();p.statMana=p.statManaMax=p.statManaMax2=300;p.releaseUseItem=true;p.armor[1].SetDefaults(robe);Main.windPhysics=false;Main.screenPosition=new Vector2(500,400);Main.rand=new Terraria.Utilities.UnifiedRandom(8713);return p;
        }
        private static Projectile[] NativeBirth(object context,Player p,Vector2 aim)
        {
            NativeToolExecutionChecks.Sample(context,Get(context,"Input"),aim,true);Call(Get(context,"Combat"),"Sample");
            typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});
            return Main.projectile.Where(q=>q.active && q.owner==p.whoAmI && Gem(q.type)).OrderBy(q=>q.whoAmI).ToArray();
        }
        private static object Ammo(Assembly a,Player p){return a.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true).GetMethod("Capture",Flags).Invoke(null,new object[]{p,p.HeldItem});}
        private static void Births(object context,Assembly a)
        {
            var deprecated=new Item();deprecated.SetDefaults(6171);Require(Terraria.ID.ItemID.Sets.Deprecated[6171] && deprecated.IsAir,"native 6171 deprecated definition is not a usable staff");
            int cases=0,members=0;
            foreach(int weapon in weapons)foreach(int robe in robes)
            {
                var p=Reset(context,weapon,robe);var ammo=Ammo(a,p);var qs=NativeBirth(context,p,p.Center+new Vector2(400,0));
                Require(qs.Length>0,"actual staff ItemCheck_Shoot creates its native family "+weapon+"/"+robe+" actualItem="+p.HeldItem.type+" shoot="+p.HeldItem.shoot+" stack="+p.HeldItem.stack+" slot="+p.selectedItem+" born="+string.Join(",",Main.projectile.Where(q=>q.active).Select(q=>q.type+"/"+q.owner)));var flags=new Projectile.GemStaffFeatures(qs[0].ai[0]);
                Require(qs.Length==(flags.AllGems?3:flags.SwirlTwins?2:1),"original native family count");
                for(int i=0;i<qs.Length;i++)
                {
                    var q=qs[i];Require(q.ai[0]==(float)Get(ammo,"GemFeatures") && q.damage==(int)Get(ammo,"Damage") && Math.Abs(q.velocity.Length()-(float)Get(ammo,"Speed")*(flags.AllGems && i==1?1.1f:1))<.0001f,"native packed robe/weapon features, damage and speed match read-only ammo "+weapon+"/"+robe);
                    Require(q.ai[1]==(flags.AllGems?i+1:i),"original twin/fan ordinal is not a generic single projectile");members++;
                }
                p.armor[1].SetDefaults(robe==1282?1287:1282);
                bool same=(bool)ammo.GetType().GetMethod("MembersMatch",Flags).Invoke(ammo,new object[]{p,p.HeldItem});var now=Ammo(a,p);
                Require(same==((float)Get(ammo,"GemFeatures")== (float)Get(now,"GemFeatures") && (int)Get(ammo,"Damage")== (int)Get(now,"Damage")),"same held item invalidates changed gem-robe feature lease");cases++;
            }
            Require(cases==56 && members>56,"complete seven hand staves by seven actual robe combinations plus bare control");Console.WriteLine("PASS native gem birth cases="+cases+" members="+members+" deprecated6171=air");
        }
        private static NpcTrajectory Path(Assembly a,NPC n,int count=121)
        {
            var id=(NpcIdentity)a.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true).GetMethod("Identity",Flags).Invoke(null,new object[]{n,1L});
            var state=new NpcMotionState{Identity=id,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=true};
            var points=Enumerable.Range(0,count).Select(i=>new NpcTrajectoryPoint(i,state)).ToArray();return new NpcTrajectory(id,(long)Main.GameUpdateCount,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,count,PredictionStrategy.RollingConditional);
        }
        private static object Receivers(Assembly a,Player p,Projectile q,NpcTrajectory path)
        {var value=Activator.CreateInstance(a.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,false,true},null);Call(value,"BeginCandidate");return value;}
        private static void Motor(object context,Assembly a)
        {
            var model=a.GetType("JueMingR.TerrariaHost.Combat.HostGemStaffAttack",true);var st=model.GetNestedType("State",BindingFlags.NonPublic);var terrainType=a.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);int cases=0,actions=0,retired=0,bounces=0,settled=0;
            foreach(int weapon in weapons)foreach(int robe in robes)foreach(int scene in Enumerable.Range(0,8))
            {
                var p=Reset(context,weapon,robe);foreach(var other in Main.npc)other.active=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();
                var n=Main.npc[2];n.SetDefaults(3);n.active=true;n.whoAmI=2;n.Center=new Vector2(1530.25f,410.5f);n.life=n.lifeMax=100000;n.netOffset=Vector2.Zero;
                var shot=NativeBirth(context,p,p.Center+Vector2.UnitX*400)[0];shot.Center=new Vector2(950,350);shot.velocity=new Vector2(12,.2f);shot.damage=0;
                if(scene>=1 && scene<=3)for(int x=30;x<115;x++)for(int y=10;y<80;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:3);}
                if(scene==4 || scene==5)for(int y=10;y<80;y++){NativeToolsChecks.Tile(65,y,1);NativeToolsChecks.Tile(45,y,1);}
                if(scene==5){shot.velocity=new Vector2(.001f,0);shot.scale=.105f;}
                if(scene==6)shot.timeLeft=2;
                if(scene==7)shot.position.X=Main.leftWorld;
                shot.wet=Collision.WetCollision(shot.position,shot.width,shot.height);shot.honeyWet=shot.wet && Collision.honey;shot.shimmerWet=shot.wet && Collision.shimmer;
                var state=Activator.CreateInstance(st,Flags,null,new object[]{shot},null);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var path=Path(a,n);var receivers=Receivers(a,p,shot,path);var targets=Call(receivers,"CaptureChaseTargets",shot);
                for(int outer=1;outer<=120 && shot.active;outer++)
                {
                    Require((bool)model.GetMethod("Step",Flags).Invoke(null,new object[]{shot,state,targets,receivers,path,outer,outer,terrain}),"gem motor geometry available");model.GetMethod("Tail",Flags).Invoke(null,new[]{(object)shot,state});shot.Update(shot.whoAmI);bool alive=(bool)Get(state,"Alive");
                    Require(alive==shot.active && (!alive || Vector2.DistanceSquared(shot.position,(Vector2)Get(state,"Position"))<.002f && Vector2.DistanceSquared(shot.velocity,(Vector2)Get(state,"Velocity"))<.0001f && shot.timeLeft==(int)Get(state,"Left") && shot.penetrate==(int)Get(state,"Penetration") && shot.ai[2]==(float)Get(state,"Target") && shot.localAI[0]==(float)Get(state,"SwirlClock") && shot.scale==(float)Get(state,"Scale") && shot.wet==(bool)Get(state,"Wet") && shot.shimmerWet==(bool)Get(state,"Shimmer")),"original complete gem Update: "+weapon+"/"+robe+"/"+scene+"/"+outer+" native="+shot.active+"/"+shot.position+"/"+shot.velocity+"/"+shot.timeLeft+"/"+shot.penetrate+"/"+shot.ai[2]+"/"+shot.scale+" model="+alive+"/"+Get(state,"Position")+"/"+Get(state,"Velocity")+"/"+Get(state,"Left")+"/"+Get(state,"Penetration")+"/"+Get(state,"Target")+"/"+Get(state,"Scale"));
                    if(alive)
                    {
                        var old=(Vector2[])Get(state,"Old");
                        // Trail entries retain the already checked motor's
                        // precision, not a stronger bitwise physics contract.
                        // Keep its integer collision coordinates exact too.
                        Require(Enumerable.Range(0,old.Length).All(i=>Vector2.DistanceSquared(shot.oldPos[i],old[i])<.002f && (int)shot.oldPos[i].X==(int)old[i].X && (int)shot.oldPos[i].Y==(int)old[i].Y),"gem trail shifts at original post-Damage boundary "+weapon+"/"+robe+"/"+scene+"/"+outer+" differences="+string.Join(";",Enumerable.Range(0,old.Length).Where(i=>old[i]!=shot.oldPos[i]).Select(i=>i+":"+shot.oldPos[i].X.ToString("R")+","+shot.oldPos[i].Y.ToString("R")+"/"+old[i].X.ToString("R")+","+old[i].Y.ToString("R"))));
                    }
                    if(scene==4 && shot.velocity.X<0)bounces++;if(scene==4 && shot.active && shot.velocity.Length()<.001f)settled++;actions++;
                }
                if(!shot.active)retired++;cases++;
            }
            Require(cases==448 && retired>0 && bounces>0 && settled>0,"all real gem/robe motors retain environmental, fade, lifetime, bounce and world-boundary controls");Console.WriteLine("PASS original gem motors="+cases+" actions="+actions+" retired="+retired+" bounces="+bounces+" settled="+settled);
        }
        private static void Collisions(object context,Assembly a)
        {
            var model=a.GetType("JueMingR.TerrariaHost.Combat.HostGemStaffAttack",true);var st=model.GetNestedType("State",BindingFlags.NonPublic);var terrainType=a.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);int cases=0,trailOnly=0,bursts=0,damageMargins=0;
            foreach(int weapon in weapons)foreach(int robe in robes)foreach(bool burst in new[]{false,true})foreach(int offset in new[]{-95,-35,-26,0,8,35,95})
            {
                var p=Reset(context,weapon,robe);var shot=NativeBirth(context,p,p.Center+Vector2.UnitX*400)[0];shot.Center=new Vector2(950.25f,350.5f);for(int i=0;i<shot.oldPos.Length;i++)shot.oldPos[i]=shot.position-new Vector2(i*35,0);
                var state=Activator.CreateInstance(st,Flags,null,new object[]{shot},null);Set(state,"DeathCenter",shot.Center);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var box=new Rectangle(950+offset,345,20,20);var original=shot.Hitbox;if(burst)shot.Resize(160,160);
                bool native=shot.Colliding(shot.Damage_GetHitbox(),box);object[] args={shot,state,terrain,new MotionRect(box.X,box.Y,box.Width,box.Height),burst,Rectangle.Empty,false};Require((bool)model.GetMethod("CollisionShape",Flags).Invoke(null,args),"gem collider geometry is captured");bool predicted=(bool)args[6];Require(predicted==native,"original gem custom current/trail/bigger/burst shape "+weapon+"/"+robe+"/"+burst+"/"+offset);if(native && !shot.Colliding(shot.Hitbox,box))damageMargins++;if(native && !original.Intersects(box) && offset<0)trailOnly++;if(burst && native)bursts++;cases++;
            }
            Require(cases==784 && trailOnly>0 && bursts>0 && damageMargins>0,"native custom collision includes non-body trails, native enlarged burst boxes and counterexamples to omitting Damage_GetHitbox");Console.WriteLine("PASS original gem colliders="+cases+" outsideBody="+trailOnly+" bursts="+bursts+" damageMarginControls="+damageMargins);
        }
        private static void Natural(object context,object host,object combat)
        {
            var attack=Get(combat,"Attack");int cases=0,hits=0,negative=0,wallHits=0;var failures=new List<string>();
            foreach(int weapon in weapons)foreach(int robe in robes)foreach(bool blocked in new[]{false,true})
            {
                try
                {
                    var p=Reset(context,weapon,robe);target=NativeCombatControlPreviewChecks.Dummy(2,62,40);firstHit=-1;damageDepth=0;if(blocked)for(int y=0;y<Main.maxTilesY;y++)NativeToolsChecks.Tile(46,y,1);
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,true,25,true,false));AttackContact preview=null;
                    for(int frame=0;frame<3;frame++){NativeCombatSelectionPreviewChecks.Step(context,target.Center,false,true);preview=(AttackContact)GetOptional(attack,"ExpectedPreview");Require(!Main.projectile.Any(q=>q.active),"idle gem observation creates no projectile");}
                    Require(blocked || preview!=null,"full gem idle preview with Aim OFF "+weapon+"/"+robe+"/"+blocked);
                    if(preview!=null){var impact=Get(Get(host,"World"),"Impact");Call(impact,"Capture");var zoom=Main.GameViewMatrix.ZoomMatrix;Call(impact,"Project",zoom,Matrix.Invert(zoom));var b=preview.Timeline[preview.ReceiveTick].Bounds;Require((bool)Get(impact,"Visible") && Vector2.DistanceSquared((Vector2)Get(impact,"Position"),Vector2.Transform(new Vector2(b.CenterX,b.CenterY)-Main.screenPosition,zoom))<.001f,"gem preview stays on the same receive-time NPC path");}
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,true,25,true,true));long start=Main.GameUpdateCount;bool born=false;actualBirth=actualReceive=null;recordBirth=true;
                    for(int frame=1;frame<=120 && firstHit<0;frame++){NativeCombatSelectionPreviewChecks.Step(context,target.Center,frame==1);born|=Main.projectile.Any(q=>q.active && q.owner==p.whoAmI && Gem(q.type));}
                    recordBirth=false;
                    // Contact.Tick is relative to its NPC sample, which can
                    // precede this completed frame by one. Compare absolute
                    // native world epochs, never subtract a guessed one tick.
                    // The thin-wall scene is not intrinsically a negative:
                    // native AI can move a twin's position before collision.
                    // Keep that original positive as a counterexample, and
                    // require both contact presence and the exact first hit.
                    Require(born,"original gem manual action births even without an input solution");Require(preview==null?firstHit<0:firstHit>=start && (preview.Confidence==AttackConfidence.Representative || firstHit==preview.Timeline.SampleTick+preview.Tick),"native first gem Damage agrees with unpressed preview "+weapon+"/"+robe+"/"+blocked+" actual="+(firstHit<0?-1:firstHit-start)+" preview="+preview?.Tick+" sampleAge="+(preview==null?-1:start-preview.Timeline.SampleTick)+" aim="+preview?.AimX+","+preview?.AimY+" birth="+actualBirth+" received="+actualReceive+" player="+p.position);
                    if(firstHit<0)negative++;else{hits++;if(blocked)wallHits++;}cases++;
                }
                catch(Exception e){failures.Add(weapon+"/"+robe+"/"+blocked+": "+e.Message);Console.WriteLine("GEM NATURAL FAIL "+e);}
            }
            Require(failures.Count==0,"all natural gem/robe members: "+string.Join(" | ",failures));Require(cases==112 && hits>=56 && negative>0 && wallHits>0,"every real gem/robe natural first-use and wall case, including native twins that remain able to hit");Console.WriteLine("PASS natural gem/robe cases="+cases+" actualHits="+hits+" blocked="+negative+" nativeWallHits="+wallHits);
        }
    }
}
