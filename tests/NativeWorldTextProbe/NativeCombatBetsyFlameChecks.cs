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
    internal static class NativeCombatBetsyFlameChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static NPC target;private static Projectile receiving;
        private static bool natural;private static int solves,steps,kills;private static long first,birthTick;private static object ammo;private static AttackContact known;private static Vector2 raw;
        private static bool firstBodyOverlap;private static int firstWidth,firstPenetration,burstStrikes;
        private static readonly List<Projectile> born=new List<Projectile>();private static readonly List<int> strikes=new List<int>();
        private static object Static(Type t,string name,params object[] args){return t.GetMethod(name,Flags).Invoke(null,args);}
        private static void SolveWork(){solves++;}private static void StepWork(){steps++;}
        private static void BeforeKill(Projectile __instance){if(__instance.type==711)kills++;}
        private static void BeforeDamage(Projectile __instance){receiving=__instance;}private static void AfterDamage(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {if(receiving==null || receiving.type!=711 || receiving.owner!=Main.myPlayer || receiving.damage<=0 || __result<=0)return;strikes.Add(__instance.whoAmI);if(receiving.width==140)burstStrikes++;if(ReferenceEquals(__instance,target) && first<0){first=Main.GameUpdateCount;firstBodyOverlap=receiving.getRect().Intersects(__instance.Hitbox);firstWidth=receiving.width;firstPenetration=receiving.penetrate;}}
        private static Player Reset(object context)
        {
            natural=false;target=null;receiving=null;born.Clear();strikes.Clear();first=-1;kills=0;known=null;firstBodyOverlap=false;firstWidth=firstPenetration=burstStrikes=0;
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3870,0,0);Main.windPhysics=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();if(Main.GameUpdateCount==0)NativeQuickItemChecks.BeginWorldStep();return p;
        }
        private static NPC Target(int slot,Vector2 center,int width=18,int height=40)
        {
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.width=width;n.height=height;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;
            Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);n.lifeRegen=n.lifeRegenCount=0;return n;
        }
        private static NpcTrajectory Timeline(NPC n)
        {
            var key=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=n.noTileCollide,ReflectsProjectiles=n.reflectsProjectiles};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();
            return new NpcTrajectory(key,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static object Terrain(){var t=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static object Obstacles(Player p,Projectile q,NpcTrajectory path){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,true,false},null);}
        private static object Clock(NpcTrajectory path){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc"),0},null);}
        private static void Born(IEntitySource __0,int __result)
        {
            if(!natural || __result<0 || Main.projectile[__result].type!=711)return;var q=Main.projectile[__result];var p=Main.LocalPlayer;int index=born.Count;born.Add(q);birthTick=Main.GameUpdateCount;
            var cursor=new Vector2(Main.mouseX,Main.mouseY)+Main.screenPosition;var origin=p.RotatedRelativePoint(p.MountedCenter);var direction=(cursor-origin).SafeNormalize(Vector2.UnitX);var args=new object[]{Terrain(),origin,direction*(float)Get(ammo,"Speed"),p.HeldItem.scale,null};Require((bool)Static(model,"BirthOrigin",args),"actual Betsy flame birth query is available");
            var velocity=(Vector2)Static(model,"BirthVelocity",direction*(float)Get(ammo,"Speed"),p.direction,index);
            Console.WriteLine("BETSY born index="+index+" slot="+q.whoAmI+" center="+q.Center+" V="+q.velocity+" damage="+q.damage+" size="+q.width+"x"+q.height+" scale="+q.scale+" trail="+q.oldPos.Length+" cursor="+cursor+" raw="+raw+" direction="+p.direction);
            Require(!(__0 is EntitySource_Parent s && s.Entity is Projectile) && index<3 && q.width==20 && q.height==20 && q.extraUpdates==1 && q.usesLocalNPCImmunity && q.localNPCHitCooldown==-1 && q.damage==(int)Get(ammo,"Damage") && Vector2.DistanceSquared(q.Center,(Vector2)args[4])<.0001f && Vector2.DistanceSquared(q.velocity,velocity)<.0001f && Vector2.Dot(q.velocity,raw-origin)<0,"all three actual deterministic native hand births consume the borrowed cursor and real40px CanHit origin");
            if(index==2){var path=Timeline(target);known=(AttackContact)Static(model,"KnownFamily",p,born.ToArray(),path,Clock(path),Terrain(),Obstacles(p,born[0],path));}
        }
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostBetsyFlameAttack",true);var audit=new Harmony("JueMingR.Tests.BetsyFlame");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(model.GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBetsyFlameChecks),nameof(SolveWork)));audit.Patch(model.GetMethod("Motor",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBetsyFlameChecks),nameof(StepWork)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatBetsyFlameChecks),nameof(Born)));
            audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBetsyFlameChecks),nameof(BeforeKill)));audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBetsyFlameChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatBetsyFlameChecks),nameof(AfterDamage)));audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatBetsyFlameChecks),nameof(Strike)));
            var componentHand=new NativeCombatHandFixture(context,()=>natural,()=>target);
            try{string only=Environment.GetEnvironmentVariable("JMR_BETSY_FLAME_ORACLE_ONLY");if(string.IsNullOrEmpty(only) || only=="motor")Motors(context);if(string.IsNullOrEmpty(only) || only=="birth")Births(context);if(string.IsNullOrEmpty(only) || only=="receive")Receives(context);if(string.IsNullOrEmpty(only) || only=="natural")Natural(context);Require(string.IsNullOrEmpty(only) || only=="motor" || only=="birth" || only=="receive" || only=="natural","native Betsy flame narrow leaf");}
            finally{componentHand.Dispose();natural=false;target=null;receiving=null;audit.UnpatchAll(audit.Id);Main.windPhysics=wind;NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
        private static void Compare(Projectile q,Projectile copy,int scene,int outer)
        {
            if(scene==0 && outer==1)Require(q.ai[0]==2 && q.alpha==230,"native second subupdate increments its clock before the first25alpha fade");
            Require(q.active==copy.active && q.width==copy.width && q.height==copy.height && q.penetrate==copy.penetrate && q.timeLeft==copy.timeLeft && q.alpha==copy.alpha && q.ai[0]==copy.ai[0] && q.localAI[0]==copy.localAI[0] && q.direction==copy.direction && q.rotation==copy.rotation && q.wet==copy.wet && q.honeyWet==copy.honeyWet && q.lavaWet==copy.lavaWet && q.shimmerWet==copy.shimmerWet && Vector2.DistanceSquared(q.position,copy.position)<.002f && Vector2.DistanceSquared(q.velocity,copy.velocity)<.0001f,"Betsy native motor/lifetime/alpha agrees "+scene+"/"+outer+" native="+q.position+"/"+q.velocity+"/"+q.ai[0]+"/"+q.timeLeft+"/"+q.active+"/size="+q.width+","+q.height+"/pen="+q.penetrate+"/alpha="+q.alpha+"/local="+q.localAI[0]+"/dir="+q.direction+"/rot="+q.rotation+"/wet="+q.wet+","+q.honeyWet+","+q.lavaWet+","+q.shimmerWet+" model="+copy.position+"/"+copy.velocity+"/"+copy.ai[0]+"/"+copy.timeLeft+"/"+copy.active+"/size="+copy.width+","+copy.height+"/pen="+copy.penetrate+"/alpha="+copy.alpha+"/local="+copy.localAI[0]+"/dir="+copy.direction+"/rot="+copy.rotation+"/wet="+copy.wet+","+copy.honeyWet+","+copy.lavaWet+","+copy.shimmerWet);
            Require(q.oldPos.SequenceEqual(copy.oldPos) && q.oldRot.SequenceEqual(copy.oldRot) && q.oldSpriteDirection.SequenceEqual(copy.oldSpriteDirection),"Betsy pre-Damage trail shifts exactly once after each real subupdate");
        }
        private static void Motors(object context)
        {
            int actions=0,cases=0;foreach(int scene in Enumerable.Range(0,10))
            {
                var p=Reset(context);if(scene>=1 && scene<=3)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:3);}if(scene==4)for(int y=10;y<60;y++)NativeToolsChecks.Tile(60,y,1);if(scene==5){NativeToolsChecks.Tile(60,25,1);Main.tile[60,25].slope(1);}if(scene==8 || scene==9)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=128;Main.tile[x,y].liquidType(3);}
                var center=scene==5?new Vector2(936,390):scene==6?new Vector2(10,350):new Vector2(950,350);var velocity=scene==4?new Vector2(24,0):scene==5?new Vector2(24,8):scene==6?new Vector2(-7,0):scene==8 || scene==9?new Vector2(6,scene==8?2:-2):new Vector2(6,-.3f);
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,velocity,711,0,0,p.whoAmI);var q=Main.projectile[slot];if(scene==7)q.timeLeft=1;var copy=(Projectile)Static(model,"Copy",q);var terrain=Terrain();Vector2 anchor;int width,height;q.GetCollisionParams(out anchor,out width,out height);
                Console.WriteLine("BETSY metadata scene="+scene+" type="+q.type+" size="+q.width+"x"+q.height+" scale="+q.scale+" trail="+q.oldPos.Length+" tile="+width+"x"+height+" anchor="+anchor+" copy="+copy.width+"x"+copy.height);
                // Locked x86 SetDefaults yields 20 after its runtime float scale/truncation; paper 30*.7=21 is not the native body.
                Require(q.width==20 && q.height==20 && copy.width==q.width && copy.height==q.height && q.scale==.7f && q.oldPos.Length==20 && width==6 && height==6,"native20px body/20trail/6px tile are distinct");
                for(int outer=1;outer<=40 && q.active;outer++){int updates=q.extraUpdates+1;for(int sub=0;sub<updates;sub++){Require((bool)Static(model,"Step",copy,terrain),"finite Betsy dependencies remain available");actions++;if(!copy.active)break;Static(model,"Finish",copy);if(!copy.active){if(sub+1<updates){Require((bool)Static(model,"RetiredStep",copy,terrain),"tail Kill retains the one inactive movement interval without another AI or Damage");actions++;}break;}}q.Update(slot);Compare(q,copy,scene,outer);}if(scene==6)Require(kills==0 && q.timeLeft>0,"world retirement has no Kill");cases++;
            }
            Console.WriteLine("PASS BETSY motor="+cases+" actions="+actions);
        }
        private static void Births(object context)
        {
            int cases=0;foreach(bool blocked in new[]{false,true})foreach(int direction in new[]{-1,1})
            {
                var p=Reset(context);Vector2 origin=new Vector2(950,350),v=new Vector2(direction*14,0),offset=Vector2.Normalize(v)*40*p.HeldItem.scale;if(blocked)for(int y=10;y<60;y++)NativeToolsChecks.Tile(direction==1?60:58,y,1);
                bool clear=Collision.CanHit(origin,0,0,origin+offset,0,0);Require(clear!=blocked,"Betsy birth fixture truly selects CanHit clear and blocked");var args=new object[]{Terrain(),origin,v,p.HeldItem.scale,null};Require((bool)Static(model,"BirthOrigin",args) && (Vector2)args[4]==(clear?origin+offset:origin),"40px birth offset follows native CanHit");
                for(int i=0;i<3;i++){var expected=(v*.8f+Vector2.Normalize(v)*i).RotatedBy(i*((float)Math.PI/180f*(-direction)));Require((Vector2)Static(model,"BirthVelocity",v,direction,i)==expected,"all three fan velocities use exact native float operation order");}cases++;
            }
            var owner=Main.LocalPlayer;float gravity=owner.gravDir,rotation=owner.fullRotation;
            try{foreach(float g in new[]{1f,-1f})foreach(float r in new[]{-.4f,0f,.4f}){owner.gravDir=g;owner.fullRotation=r;var origin=new Vector2(950,350);var aim=new Vector2(970,410);var rawPoint=aim;if(g==-1)rawPoint.Y=Main.screenPosition.Y+Main.screenHeight-(aim.Y-Main.screenPosition.Y);var ray=Vector2.Normalize(rawPoint-origin);int expected=Vector2.Dot(Vector2.UnitX.RotatedBy(r),ray)>0?1:-1;Require((int)Static(model,"Facing",owner,origin,aim)==expected,"native pre-reverse-gravity facing predicate is retained by the fan");}}
            finally{owner.gravDir=gravity;owner.fullRotation=rotation;}
            Console.WriteLine("PASS BETSY birth roles="+cases);
        }
        private static void Receives(object context)
        {
            int cases=0;foreach(int mode in Enumerable.Range(0,11))
            {
                var p=Reset(context);bool trail=mode==3 || mode==4;target=Target(2,new Vector2(trail?900:mode==7?1040:mode>=8?1019:960,350),18,40);if(mode==1)target.immune[p.whoAmI]=200;if(mode==5 || mode==9)target.reflectsProjectiles=true;NPC front=mode==2 || mode==7?Target(1,new Vector2(950,350),22,40):null;
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,6,0,711,50,0,p.whoAmI);var q=Main.projectile[slot];if(trail)q.oldPos[0]=target.position;if(mode==4)q.penetrate=q.maxPenetrate=-1;if(mode==6 || mode==10)q.localNPCImmunity[2]=-1;if(mode>=8)q.timeLeft=1;
                NativeQuickItemChecks.BeginWorldStep();var path=Timeline(target);var terrain=Terrain();var obstacles=Obstacles(p,q,path);known=(AttackContact)Static(model,"KnownFamily",p,new[]{q},path,Clock(path),terrain,obstacles);long sample=path.SampleTick;
                if(mode==3)
                {
                    var old=q.oldPos[0];q.oldPos[0]+=Vector2.UnitX;Require(!(bool)Get(obstacles,"Unchanged"),"changing711 oldPos alone invalidates the captured Damage trail lease");q.oldPos[0]=old;Require((bool)Get(obstacles,"Unchanged"),"restored trail preserves original unchanged body/AI lease");
                    var privatePose=(Player)typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null);Call(obstacles,"CaptureShotFacing",privatePose);Require((bool)Get(obstacles,"Unchanged") && (bool)Get(obstacles,"BirthInputsCurrent"),"private shape binds the actual live owner's facing lease");
                    float rotation=p.fullRotation;
                    try{p.fullRotation=rotation+.4f;Require(!(bool)Get(obstacles,"Unchanged") && !(bool)Get(obstacles,"BirthInputsCurrent"),"fullRotation alone revokes consume and scalar presentation lease even when body/AI do not change");p.fullRotation=rotation;Require((bool)Get(obstacles,"Unchanged"),"restoring the real rotation restores the lease");Main.player[p.whoAmI]=privatePose;Require(!(bool)Get(obstacles,"BirthInputsCurrent"),"a same-slot private player cannot replace the captured live owner identity");}
                    finally{p.fullRotation=rotation;Main.player[p.whoAmI]=p;}
                    Require((bool)Get(obstacles,"Unchanged") && (bool)Get(obstacles,"BirthInputsCurrent"),"live owner identity and rotation restored without running native update");
                }
                for(int outer=1;outer<=12 && q.active && first<0;outer++){if(outer>1)NativeQuickItemChecks.BeginWorldStep();if(front!=null)front.UpdateNPC(1);target.UpdateNPC(2);q.Update(slot);}
                int actual=first<0?0:(int)(first-sample);Console.WriteLine("BETSY receive mode="+mode+" native="+actual+" known="+known?.Tick+" slots="+string.Join(",",strikes)+" local="+q.localNPCImmunity[2]+" owner="+target.immune[p.whoAmI]+" firstBodyOverlap="+firstBodyOverlap+" firstWidth="+firstWidth+" firstPen="+firstPenetration+" burstStrikes="+burstStrikes);bool negative=mode==1 || mode==4 || mode==5 || mode==6 || mode==7 || mode==9 || mode==10;
                Require((actual==0)==negative && (known==null)==negative && (known==null || known.Tick==actual),"Betsy actual local/owner/preceding receiver/raw reflection/trail penetration and same-body Kill stages agree");if(mode==3)Require(actual==1 && !firstBodyOverlap && firstWidth==20 && strikes.Count(slotHit=>slotHit==2)==1,"native oldPos alone hits before the140Kill, whose retained local immunity prevents a repeated selected hit");
                if(mode==2)Require(strikes.SequenceEqual(new[]{1,2}) && firstWidth==140 && firstPenetration==-1 && q.localNPCImmunity[1]==-1 && q.localNPCImmunity[2]==-1,"earlier actual receiver spends pen1 then same-body140Kill reaches selected once, without re-hitting the earlier receiver");
                if(mode==7)Require(strikes.SequenceEqual(new[]{1}) && q.width==140 && q.penetrate==-1 && !q.getRect().Intersects(target.Hitbox),"outside140Kill is the true preceding-receiver negative control");
                if(mode==8)Require(firstWidth==140 && firstPenetration==-1 && burstStrikes==1 && q.width==140 && q.localNPCImmunity[2]==-1,"expiry-only140body gives actual positiveDamage on the same source without a child");cases++;
            }
            Console.WriteLine("PASS BETSY receive="+cases);
        }
        private static void Natural(object context)
        {
            var p=Reset(context);p.position=new Vector2(650,646);p.releaseUseItem=true;p.statMana=p.statManaMax2=200;Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,45,40);raw=target.Center-new Vector2(390,0);solves=steps=0;
            var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,raw,false,true);
            Require(GetOptional(attack,"ExpectedPreview")==null && solves==0 && steps==0 && !Main.projectile.Any(q=>q.active),"withdrawn idle Betsy preview never solves or fires");
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),raw,true);Call(Get(context,"Combat"),"Sample");NativeCombatAimChecks.PrepareMechanismFixture(host,attack,target,0);var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");Require(plan!=null,"actual pressed Betsy has its finite three-shot preparation");Call(attack,"BindPresentation",plan);
            ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);
            int draws=steps,drawSolves=solves;var marker=Get(Get(host,"World"),"Impact");var zoom=Main.GameViewMatrix.ZoomMatrix;for(int d=0;d<20;d++){Call(marker,"Capture");Call(marker,"Project",zoom,Matrix.Invert(zoom));Require((bool)Call(attack,"PresentationCurrent",plan),"Betsy Draw respects the current scalar presentation lease");}Require(draws==steps && drawSolves==solves,"twenty Draws perform no Betsy solve or motor");
            {float rotation=p.fullRotation;try{p.fullRotation=rotation+.4f;Require(!(bool)Call(attack,"PresentationCurrent",plan),"actual fullRotation change revokes the old ordinary idle preview through its scalar birth lease");}finally{p.fullRotation=rotation;}Require((bool)Call(attack,"PresentationCurrent",plan) && draws==steps && drawSolves==solves,"restoring facing restores same-update preview without a new solve");}
            natural=true;long start=Main.GameUpdateCount;NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));NativeCombatSelectionPreviewChecks.Step(context,raw,true);Require((bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced","actual Betsy hand consumes its prepared cursor");for(int i=0;i<20 && first<0;i++)NativeCombatSelectionPreviewChecks.Step(context,raw,false);
            Console.WriteLine("BETSY natural births="+born.Count+" first="+(first<0?0:first-start)+" actualKnown="+known?.Tick+" representative="+plan?.Tick+" solves="+solves+" steps="+steps);
            Require(born.Count==3 && born.Select(q=>q.whoAmI).Distinct().Count()==3 && first>=birthTick && known!=null && first-birthTick+1==known.Tick,"one real press/release gives three deterministic source shots and useful positive Strike on the genuine immortal dummy, matching actual-born family");natural=false;
        }
    }
}
