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
    internal static class NativeCombatNebulaBlazeChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static NPC target;private static Projectile receiving,watchedSource;
        private static bool recordBirth;private static int solves,steps,poolScans,kills,burstHits,modelDepth,knownCalls,rngCalls,targetCaptures;private static long first;private static object ammo;private static Vector2 raw;
        private sealed class Birth
        {internal Projectile Shot;internal long Tick,First=-1;internal AttackContact Known;internal int Type,Damage;internal Vector2 Center,Velocity,Cursor;}
        private static readonly List<Birth> born=new List<Birth>();private static readonly List<int> strikeSlots=new List<int>();
        private static object Static(Type t,string name,params object[] args){return t.GetMethod(name,Flags).Invoke(null,args);}
        private static void SolveWork(){solves++;modelDepth++;}private static void KnownWork(){knownCalls++;modelDepth++;}private static void ModelDone(){modelDepth--;}
        private static void RngWork(){if(modelDepth>0)rngCalls++;}private static void TargetWork(){if(modelDepth>0)targetCaptures++;}
        private static void MotorWork(){steps++;}private static void PoolWork(){poolScans++;}
        private static bool Role(Projectile p){return p!=null && (p.type==634 || p.type==635);}
        private static void BeforeKill(Projectile __instance){if(Role(__instance))kills++;}
        private static void BeforeDamage(Projectile __instance){receiving=__instance;}private static void AfterDamage(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {
            if(!Role(receiving) || receiving.owner!=Main.myPlayer || receiving.damage<=0 || __result<=0)return;strikeSlots.Add(__instance.whoAmI);if(receiving.width==50)burstHits++;
            if(!ReferenceEquals(__instance,target) || watchedSource!=null && !ReferenceEquals(watchedSource,receiving))return;if(first<0)first=Main.GameUpdateCount;
            foreach(var b in born)if(ReferenceEquals(b.Shot,receiving) && b.First<0)b.First=Main.GameUpdateCount;
        }
        private static Player Reset(object context)
        {
            recordBirth=false;target=null;receiving=watchedSource=null;first=-1;kills=burstHits=0;born.Clear();strikeSlots.Clear();
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            // Native ResetEffects restores Max2 from permanent Max before
            // Update clamps mana. Both continuous birth/use cases need a
            // real200-mana player, rather than one temporary Max2 assignment.
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3542,0,0);p.statMana=p.statManaMax=p.statManaMax2=200;Main.windPhysics=false;
            for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();if(Main.GameUpdateCount==0)NativeQuickItemChecks.BeginWorldStep();return p;
        }
        private static NPC Target(int slot,Vector2 center,int width=18,int height=40)
        {
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.width=width;n.height=height;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;n.realLife=-1;
            Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);n.lifeRegen=n.lifeRegenCount=0;return n;
        }
        private static NpcTrajectory Timeline(NPC n)
        {
            var key=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=n.noTileCollide,ReflectsProjectiles=n.reflectsProjectiles};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();
            return new NpcTrajectory(key,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static object Terrain(){var t=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static object Obstacles(Player p,Projectile q,NpcTrajectory path){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,true,false},null);}
        private static object Clock(NpcTrajectory path,string phase="BeforeNpc"){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),phase),0},null);}
        private static Projectile Shot(Player p,int type,Vector2 center,Vector2 v,int damage=50)
        {return Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,v,type,damage,0,p.whoAmI)];}
        private static AttackContact Known(Player p,Projectile q,NpcTrajectory path)
        {return (AttackContact)Static(model,"KnownFamily",p,new[]{q},path,Clock(path),Terrain(),Obstacles(p,q,path));}
        private static void Born(IEntitySource __0,int __result)
        {
            if(!recordBirth || __result<0)return;var q=Main.projectile[__result];if(!Role(q))return;var p=Main.LocalPlayer;var cursor=new Vector2(Main.mouseX,Main.mouseY)+Main.screenPosition;var origin=p.RotatedRelativePoint(p.MountedCenter);var v=(cursor-origin).SafeNormalize(Vector2.UnitX)*(float)Get(ammo,"Speed");if(q.type==634)v=v.SafeNormalize(Vector2.UnitX)*(v.Length()-1);
            float angle=MathHelper.WrapAngle(q.velocity.ToRotation()-v.ToRotation()),scale=q.velocity.Length()/v.Length();int expected=(int)Get(ammo,"Damage")*(q.type==635?3:1);var path=Timeline(target);
            var b=new Birth{Shot=q,Tick=Main.GameUpdateCount,Type=q.type,Damage=q.damage,Center=q.Center,Velocity=q.velocity,Cursor=cursor,Known=Known(p,q,path)};born.Add(b);
            Console.WriteLine("NEBULA born slot="+q.whoAmI+" type="+q.type+" center="+q.Center+" V="+q.velocity+" damage="+q.damage+" expectedDamage="+expected+" angle="+angle+" scale="+scale+" extra="+q.extraUpdates+" cursor="+cursor+" raw="+raw+" known="+b.Known?.Tick);
            Require(!(__0 is EntitySource_Parent source && source.Entity is Projectile) && q.magic && q.width==40 && q.height==40 && q.extraUpdates==(q.type==635?3:2) && q.ignoreWater==(q.type==634) && q.damage==expected && Vector2.DistanceSquared(q.Center,origin)<.0001f && angle>=-(float)Math.PI/8*.7f && angle<(float)Math.PI/8*.7f && scale>=.95f && scale<1.25f,"actual ItemCheck births one634 or635 with native random-angle/speed and triple-damage branch; no parent child spawn");
        }
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostNebulaBlazeAttack",true);var audit=new Harmony("JueMingR.Tests.NebulaBlaze");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(model.GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatNebulaBlazeChecks),nameof(SolveWork)),postfix:new HarmonyMethod(typeof(NativeCombatNebulaBlazeChecks),nameof(ModelDone)));audit.Patch(model.GetMethod("KnownFamily",Flags),prefix:new HarmonyMethod(typeof(NativeCombatNebulaBlazeChecks),nameof(KnownWork)),postfix:new HarmonyMethod(typeof(NativeCombatNebulaBlazeChecks),nameof(ModelDone)));
            audit.Patch(model.GetMethod("Motor",Flags),prefix:new HarmonyMethod(typeof(NativeCombatNebulaBlazeChecks),nameof(MotorWork)));audit.Patch(model.GetMethod("Capture",Flags),prefix:new HarmonyMethod(typeof(NativeCombatNebulaBlazeChecks),nameof(PoolWork)));
            audit.Patch(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true).GetMethod("CaptureChaseTargets",Flags),prefix:new HarmonyMethod(typeof(NativeCombatNebulaBlazeChecks),nameof(TargetWork)));
            foreach(var method in Main.rand.GetType().GetMethods(Flags).Where(m=>m.Name=="Next" || m.Name=="NextDouble").Distinct())audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatNebulaBlazeChecks),nameof(RngWork)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatNebulaBlazeChecks),nameof(Born)));
            audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatNebulaBlazeChecks),nameof(BeforeKill)));audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatNebulaBlazeChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatNebulaBlazeChecks),nameof(AfterDamage)));audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatNebulaBlazeChecks),nameof(Strike)));
            try
            {
                string only=Environment.GetEnvironmentVariable("JMR_NEBULA_BLAZE_ORACLE_ONLY");if(string.IsNullOrEmpty(only) || only=="motor")Motors(context);if(string.IsNullOrEmpty(only) || only=="birth")Births(context);if(string.IsNullOrEmpty(only) || only=="receive")Receives(context);if(only=="owner")OwnerRewrite(context);if(string.IsNullOrEmpty(only) || only=="natural")Natural(context);
                Require(string.IsNullOrEmpty(only) || only=="motor" || only=="birth" || only=="receive" || only=="owner" || only=="natural","nebula narrow selector");
            }
            finally{recordBirth=false;receiving=null;target=null;audit.UnpatchAll(audit.Id);Main.windPhysics=wind;NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
        private static void Compare(Projectile q,Projectile copy,int scene,int outer)
        {
            // First-AI localAI0 selects only a dust phase. Native draws that
            // value; the representative never consumes RNG to match visuals.
            Require(q.active==copy.active && q.width==copy.width && q.height==copy.height && q.penetrate==copy.penetrate && q.maxPenetrate==copy.maxPenetrate && q.timeLeft==copy.timeLeft && q.alpha==copy.alpha && q.ai.SequenceEqual(copy.ai) && q.direction==copy.direction && q.spriteDirection==copy.spriteDirection && q.frameCounter==copy.frameCounter && q.frame==copy.frame && q.rotation==copy.rotation && q.wet==copy.wet && q.honeyWet==copy.honeyWet && q.lavaWet==copy.lavaWet && q.shimmerWet==copy.shimmerWet && Vector2.DistanceSquared(q.position,copy.position)<.002f && Vector2.DistanceSquared(q.velocity,copy.velocity)<.0001f,"nebula native motion/state "+q.type+"/"+scene+"/"+outer+" native="+q.position+"/"+q.velocity+"/AI="+string.Join(",",q.ai)+"/alive="+q.active+"/left="+q.timeLeft+"/body="+q.width+"/pen="+q.penetrate+","+q.maxPenetrate+"/alpha="+q.alpha+"/rotation="+q.rotation+"/frame="+q.frame+","+q.frameCounter+"/wet="+q.wet+","+q.honeyWet+","+q.lavaWet+","+q.shimmerWet+" model="+copy.position+"/"+copy.velocity+"/AI="+string.Join(",",copy.ai)+"/alive="+copy.active+"/left="+copy.timeLeft+"/body="+copy.width+"/pen="+copy.penetrate+","+copy.maxPenetrate+"/alpha="+copy.alpha+"/rotation="+copy.rotation+"/frame="+copy.frame+","+copy.frameCounter+"/wet="+copy.wet+","+copy.honeyWet+","+copy.lavaWet+","+copy.shimmerWet);
        }
        private static void Motors(object context)
        {
            int cases=0,actions=0;foreach(int type in new[]{634,635})foreach(int scene in Enumerable.Range(0,10))
            {
                var p=Reset(context);target=Target(2,new Vector2(1700,350));if(scene>=1 && scene<=3)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:3);}if(scene==4)for(int y=10;y<60;y++)NativeToolsChecks.Tile(60,y,1);
                if(scene==5){Target(1,new Vector2(1150,350));target.Center=new Vector2(1020,350);}if(scene==6)target.Center=new Vector2(1000,350);if(scene==7)target.Center=new Vector2(965,350);
                var q=Shot(p,type,scene==8?new Vector2(10,350):new Vector2(950,350),new Vector2(scene==4?24:6,0),0);if(scene==9)q.timeLeft=1;if(scene!=0)q.ai[1]=1;
                var path=Timeline(target);var terrain=Terrain();var obstacles=Obstacles(p,q,path);var targets=Call(obstacles,"CaptureChaseTargets",q);var copy=(Projectile)Static(model,"Copy",q);Vector2 anchor;int width,height;q.GetCollisionParams(out anchor,out width,out height);Require(q.width==40 && q.height==40 && width==8 && height==8,"native40 damage body and8 tile collider differ");
                for(int outer=1;outer<=30 && q.active;outer++)
                {
                    int updates=q.extraUpdates+1;for(int sub=0;sub<updates;sub++){Require((bool)Static(model,"Step",copy,targets,path,1,terrain),"nebula finite motor dependencies");actions++;if(!copy.active)break;Static(model,"Finish",copy);if(!copy.active){if(sub+1<updates)Require((bool)Static(model,"RetiredStep",copy,targets,path,1,terrain),"nebula inactive tail movement without another AI or Damage");break;}}
                    q.Update(q.whoAmI);Compare(q,copy,scene,outer);if(scene==5 && outer==1)Require(q.ai[0]==1,"first lower NPC slot is chased despite a nearer higher slot");
                    if(scene==6 && outer==1){target.dontTakeDamage=true;targets=Call(obstacles,"CaptureChaseTargets",q);} // Next real query must reset ai1 andai0.
                }
                if(scene==8)Require(kills==0 && q.timeLeft>0,"native world boundary retires without Kill burst");cases++;
            }
            Console.WriteLine("PASS NEBULA motor="+cases+" actions="+actions);
        }
        private static void Births(object context)
        {
            var p=Reset(context);p.position=new Vector2(650,646);p.releaseUseItem=true;Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,45,40);target.position.X=p.Center.X+60;raw=target.Center;
            ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);recordBirth=true;for(int i=0;i<37;i++)NativeCombatSelectionPreviewChecks.Step(context,raw,true);NativeCombatSelectionPreviewChecks.Step(context,raw,false);
            Require(born.Count>=3 && born.Count<=4,"continuous real ItemCheck creates one shot per native12-update use without simultaneous634/635");Console.WriteLine("PASS NEBULA actual hand birth="+born.Count+" roles="+string.Join(",",born.Select(b=>b.Type)));recordBirth=false;
            foreach(int type in new[]{634,635}){var expected=(Vector2)Static(model,"BirthVelocity",Vector2.UnitX,6f,type,0f,1.1f);Require(expected==new Vector2((type==634?5f:6f)*1.1f,0),"each future birth representative is a legal native branch with its own speed");}
        }
        private static void Receives(object context)
        {
            int cases=0;foreach(int type in new[]{634,635})foreach(int mode in Enumerable.Range(0,7))
            {
                var p=Reset(context);target=Target(2,new Vector2(mode==1 || mode==3?983:mode==4?984:960,350),18,40);if(mode==1 || mode==6)target.immune[p.whoAmI]=200;if(mode==5)target.reflectsProjectiles=true;NPC front=mode==2?Target(1,new Vector2(950,350),22,40):null;
                var q=Shot(p,type,new Vector2(950,350),Vector2.Zero);if(mode==1 || mode==3 || mode==4)q.timeLeft=1;
                if(mode==1)Require(target.immune[p.whoAmI]==200 && !q.getRect().Intersects(target.Hitbox) && new Rectangle(925,325,50,50).Intersects(target.Hitbox),"old owner200 negative is strictly outside the owner-bypassing40 flight body and one pixel inside the50 Kill burst");
                NativeQuickItemChecks.BeginWorldStep();var path=Timeline(target);var contact=Known(p,q,path);long sample=path.SampleTick;
                Require(Static(model,"KnownFamily",p,new[]{q},path,Clock(path,"Projectiles"),Terrain(),Obstacles(p,q,path))==null,"partial projectile-array epoch cannot be replayed as a full world");
                for(int outer=1;outer<=12 && q.active && first<0;outer++){if(outer>1)NativeQuickItemChecks.BeginWorldStep();if(front!=null)front.UpdateNPC(1);target.UpdateNPC(2);q.Update(q.whoAmI);}
                int actual=first<0?0:(int)(first-sample);Console.WriteLine("NEBULA receive type="+type+" mode="+mode+" native="+actual+" known="+contact?.Tick+" slots="+string.Join(",",strikeSlots)+" burst="+burstHits+" body="+q.width+" pen="+q.penetrate+" owner="+target.immune[p.whoAmI]);
                bool negative=mode==1 || mode==4 || mode==5;Require((actual==0)==negative && (contact==null)==negative && (contact==null || contact.Tick==actual),"nebula real first Strike/50burst/owner/reflection equals known replay");
                if(mode==0)Require(strikeSlots.Count(s=>s==2)==1 && target.immune[p.whoAmI]>0,"native explicit owner5 prevents the same target being hit again by its synchronous Kill");if(mode==2)Require(strikeSlots.Take(2).SequenceEqual(new[]{1,2}) && burstHits==1,"lower body hit spends pen1 then50 Kill burst legitimately hits the higher slot with shared owner history");cases++;
            }
            Console.WriteLine("PASS NEBULA receive="+cases);
            OwnerRewrite(context);
        }
        private static void OwnerRewrite(object context)
        {
            foreach(bool ambient in new[]{false,true})
            {
                var p=Reset(context);target=Target(2,new Vector2(983,350));target.immortal=true;target.immune[p.whoAmI]=200;
                Projectile prior=ambient?Shot(p,634,target.Center,Vector2.Zero):null;watchedSource=Shot(p,635,new Vector2(950,350),Vector2.Zero);watchedSource.timeLeft=21;
                // The actual source body40 is strictly outside; only Kill50
                // overlaps. Immortal is a native non-chaseable damage body,
                // so both projectiles stay at their observed positions.
                Require(!watchedSource.getRect().Intersects(target.Hitbox) && new Rectangle(925,325,50,50).Intersects(target.Hitbox) && !target.CanBeChasedBy(watchedSource),"owner-write counterexample has only a50burst source, with no autonomous steering");
                NativeQuickItemChecks.BeginWorldStep();var path=Timeline(target);var contact=Known(p,watchedSource,path);long sample=path.SampleTick;int afterAmbient=-1;
                for(int outer=1;outer<=8 && watchedSource.active;outer++)
                {
                    if(outer>1)NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(2);if(prior!=null && prior.active)prior.Update(prior.whoAmI);if(outer==1)afterAmbient=target.immune[p.whoAmI];watchedSource.Update(watchedSource.whoAmI);
                }
                int actual=first<0?0:(int)(first-sample);Console.WriteLine("NEBULA owner rewrite ambient="+ambient+" sourceSlot="+watchedSource.whoAmI+" afterAmbient="+afterAmbient+" nativeSource="+actual+" known="+contact?.Tick+" finalOwner="+target.immune[p.whoAmI]);
                Require(ambient?afterAmbient==5 && actual==6:afterAmbient>=190 && actual==0,"a native lower pen1 shot overwrites old200 owner immunity with5; only its actual later source burst may contribute");
                Require((contact==null)==!ambient && (contact==null || contact.Tick==actual),"known family must preserve native owner writes that shorten an old captured immunity wait; ambient damage cannot itself count as source contact");
            }
        }
        private static void Draws(object host,object attack,AttackContact plan)
        {
            int a=solves,b=steps,c=poolScans;var marker=Get(Get(host,"World"),"Impact");var zoom=Main.GameViewMatrix.ZoomMatrix;
            for(int i=0;i<20;i++){Call(marker,"Capture");Call(marker,"Project",zoom,Matrix.Invert(zoom));Require((bool)Call(attack,"PreviewCurrent",plan),"nebula same-update observed preview remains leased");}
            Require(a==solves && b==steps && c==poolScans,"twenty presentations add zero Nebula Solve/motor/pool scans");
        }
        private static void Natural(object context)
        {
            var p=Reset(context);p.position=new Vector2(650,646);p.releaseUseItem=true;Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,45,40);target.position.X=p.Center.X+60;raw=target.Center-new Vector2(390,0);solves=steps=poolScans=knownCalls=rngCalls=targetCaptures=modelDepth=0;var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,raw,false,true);var plan=(AttackContact)GetOptional(attack,"ExpectedPreview");Require(plan!=null && plan.Confidence==AttackConfidence.Representative && !Main.projectile.Any(q=>q.active),"idle Nebula random hand is represented without firing");Draws(host,attack,plan);Require(poolScans==solves && targetCaptures==solves && rngCalls==0,"each idle solve captures one pool and chase scene without native RNG");
            ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);recordBirth=true;NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));int previewFrames=0;
            for(int frame=0;frame<37;frame++)
            {
                NativeCombatSelectionPreviewChecks.Step(context,raw,true);var next=(AttackContact)GetOptional(attack,"ExpectedPreview");if(frame>=2){Require(next!=null,"continuous12-update weapon preserves a true next-hand preview during cooldown");previewFrames++;}if(frame==8 && next!=null)Draws(host,attack,next);
            }
            NativeCombatSelectionPreviewChecks.Step(context,raw,false);for(int frame=0;frame<12 && born.Any(b=>b.First<0);frame++)NativeCombatSelectionPreviewChecks.Step(context,raw,false);
            foreach(var b in born){Console.WriteLine("NEBULA natural actual type="+b.Type+" first="+(b.First<0?0:b.First-b.Tick+1)+" known="+b.Known?.Tick+" V="+b.Velocity+" cursor="+b.Cursor);Require(b.First>=b.Tick && b.Known!=null && b.Known.Confidence==AttackConfidence.Conditional && b.First-b.Tick+1==b.Known.Tick && Vector2.Dot(b.Velocity,raw-b.Center)<0,"each captured near natural random birth has real positive Strike on immortal488 equal to its actual-born replay, using borrowed cursor opposite raw input");}
            Require(rngCalls==0 && poolScans==solves+knownCalls && targetCaptures==poolScans && born.Count>=3 && previewFrames>0 && solves>0 && poolScans>0 && steps>0,"every Solve/Known captures one scene, consumes no RNG, and has useful natural source evidence");Console.WriteLine("PASS NEBULA natural actual="+born.Count+" previewFrames="+previewFrames+" solves="+solves+" knownCalls="+knownCalls+" poolScans="+poolScans+" motor="+steps+" RNG="+rngCalls);recordBirth=false;
        }
    }
}
