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
    internal static class NativeCombatWaspBatChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static bool watching,natural;private static NPC target,front;private static Projectile receiving,source;
        private static long first;private static int role,births,baseDamage,solves,captures,motors,frontHits;private static AttackContact prepared;
        private static Vector2 raw;
        private sealed class NaturalBirth
        {internal Projectile Shot;internal AttackContact Known;internal long SampleTick,First=-1;internal bool Retired;}
        private static readonly List<NaturalBirth> naturalBirths=new List<NaturalBirth>();
        private static Assembly naturalAssembly;private static Type naturalModel;private static bool naturalNear;
        private static void SolveWork(){solves++;}
        private static void CaptureWork(){captures++;}
        private static void MotorWork(){motors++;}
        private static void BeforeDamage(Projectile __instance){receiving=__instance;}
        private static void AfterDamage(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {
            if(!watching || receiving==null || receiving.owner!=Main.myPlayer || receiving.damage<=0 || receiving.type!=role || __result<=0)return;
            if(ReferenceEquals(__instance,front))frontHits++;
            if(ReferenceEquals(__instance,target) && (natural || ReferenceEquals(receiving,source)) && first<0)first=Main.GameUpdateCount;
            if(natural && ReferenceEquals(__instance,target))foreach(var born in naturalBirths)if(ReferenceEquals(receiving,born.Shot) && born.First<0)born.First=Main.GameUpdateCount;
        }
        private static void DamageInput(float dmg)
        {
            if(watching && receiving!=null && receiving.type==189 && receiving.owner==Main.myPlayer)
                Require(Math.Abs(dmg-(receiving.damage+(Main.player[receiving.owner].strongBees?5:0)))<.001f,"native Wasp adds strong-bee5 at DamageVar input, without altering birth damage/penetration");
        }
        private static void Born(int __result)
        {
            if(!watching || !natural || __result<0)return;var q=Main.projectile[__result];if(q.owner!=Main.myPlayer || q.type!=role)return;
            Require(q.magic && q.damage==baseDamage && q.penetrate==(role==189?3:1),"actual hand birth retains native magic/damage/penetration role");births++;
            var aim=new Vector2(prepared.AimX,prepared.AimY)-q.Center;var physical=raw-q.Center;var baseline=aim.SafeNormalize(Vector2.UnitX)*Main.player[q.owner].HeldItem.shootSpeed;
            float jitter=role==189?.7f:1.75f;
            Require(Math.Abs(q.velocity.X-baseline.X)<=jitter+.02f && Math.Abs(q.velocity.Y-baseline.Y)<=jitter+.02f && Vector2.Dot(q.velocity,physical)<0,"actual Wasp/Bat birth follows assisted hand with legal independent jitter and opposite physical mouse");
            if(role==316)
            {
                if(naturalNear)Require(q.Center==new Vector2(660,667) && q.width==16 && q.height==16 && target.Hitbox==new Rectangle(695,648,18,40) && baseline.X>9.99f && Math.Abs(baseline.Y)<.23f,"near Bat actual source/shape bounds guarantee every legal jitter crosses the dummy within four actions before floor contact");
                // Capture before the native projectile loop. Single-hit bats
                // have independent histories against the genuine non-chaseable
                // dummy; each actual jitter is replayed without another RNG draw.
                var timeline=Timeline(naturalAssembly,target);var terrain=Terrain(naturalAssembly);
                var obstacles=Activator.CreateInstance(naturalAssembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{Main.player[q.owner],q,timeline,0,true,false},null);
                var clock=Activator.CreateInstance(naturalAssembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{timeline,Enum.Parse(naturalAssembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc"),0},null);
                var known=(AttackContact)naturalModel.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{Main.player[q.owner],new[]{q},timeline,clock,terrain,obstacles});
                naturalBirths.Add(new NaturalBirth{Shot=q,Known=known,SampleTick=timeline.SampleTick});
                Console.WriteLine("BAT actual birth slot="+q.whoAmI+" center="+q.Center+" V="+q.velocity+" extra="+q.extraUpdates+" alpha="+q.alpha+" damage="+q.damage+" friendly="+q.friendly+" target="+target.Hitbox+" chase="+target.CanBeChasedBy(q)+" known="+known?.Tick+" assisted="+prepared.AimX+","+prepared.AimY+" raw="+raw);
            }
        }
        private static Player Reset(object context,int item=1155)
        {
            watching=natural=false;target=front=null;source=receiving=null;first=-1;births=frontHits=0;
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),item,0,0);Main.windPhysics=false;
            for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();for(int i=0;i<Main.maxNPCs;i++)Projectile.perIDStaticNPCImmunity[189,i]=0;if(Main.GameUpdateCount==0)NativeQuickItemChecks.BeginWorldStep();return p;
        }
        private static NPC Target(int slot,Vector2 position,bool wet=false)
        {
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.position=position;n.width=32;n.height=40;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;n.wet=wet;Array.Clear(n.immune,0,n.immune.Length);return n;
        }
        private static NpcTrajectory Timeline(Assembly a,NPC n)
        {
            var key=(NpcIdentity)a.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true).GetMethod("Identity",Flags).Invoke(null,new object[]{n,1L});
            var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=true,Wet=n.wet};
            var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(key,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static object Terrain(Assembly a)
        {var terrain=Activator.CreateInstance(a.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");return terrain;}
        internal static void Run(object context)
        {
            var a=Get(context,"Combat").GetType().Assembly;var model=a.GetType("JueMingR.TerrariaHost.Combat.HostWaspBatAttack",true);var bee=a.GetType("JueMingR.TerrariaHost.Combat.HostBeeAttack",true);
            bool wind=Main.windPhysics;var random=Main.rand;var audit=new Harmony("JueMingR.Tests.WaspBat");
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(model.GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatWaspBatChecks),nameof(SolveWork)));
            audit.Patch(a.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true).GetMethod("CaptureWetChaseTargets",Flags),prefix:new HarmonyMethod(typeof(NativeCombatWaspBatChecks),nameof(CaptureWork)));
            audit.Patch(bee.GetMethod("StepWaspBat",Flags),prefix:new HarmonyMethod(typeof(NativeCombatWaspBatChecks),nameof(MotorWork)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatWaspBatChecks),nameof(Born)));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatWaspBatChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatWaspBatChecks),nameof(AfterDamage)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatWaspBatChecks),nameof(Strike)));
            audit.Patch(typeof(Main).GetMethod("DamageVar",Flags),prefix:new HarmonyMethod(typeof(NativeCombatWaspBatChecks),nameof(DamageInput)));
            try
            {
                Require((bool)model.GetMethod("Handles",Flags).Invoke(null,new object[]{189}) && (bool)model.GetMethod("Handles",Flags).Invoke(null,new object[]{316}) && !(bool)model.GetMethod("Handles",Flags).Invoke(null,new object[]{181}) && !(bool)bee.GetMethod("Handles",Flags).Invoke(null,new object[]{189}),"Wasp/Bat dispatch is explicit and preserves Bee181/566 identities");
                string only=Environment.GetEnvironmentVariable("JMR_WASP_BAT_ORACLE_ONLY");
                if(string.IsNullOrEmpty(only) || only=="motor")Motors(context,a,bee);
                if(string.IsNullOrEmpty(only) || only=="receive")Receives(context,a,model);
                if(string.IsNullOrEmpty(only) || only=="natural")Natural(context,a,model);
                Require(string.IsNullOrEmpty(only) || only=="motor" || only=="receive" || only=="natural","known Wasp/Bat leaf selector");
            }
            finally{watching=natural=naturalNear=false;target=front=null;source=receiving=null;naturalBirths.Clear();naturalAssembly=null;naturalModel=null;audit.UnpatchAll(audit.Id);Main.windPhysics=wind;Main.rand=random;NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
        private static void Motors(object context,Assembly a,Type bee)
        {
            var stateType=bee.GetNestedType("State",BindingFlags.NonPublic);var targetsType=a.GetType("JueMingR.TerrariaHost.Combat.HostProjectileTargets",true);int cases=0,actions=0,reflections=0,batWallKills=0;
            foreach(int type in new[]{189,316,181,566})foreach(int scene in Enumerable.Range(0,9))
            {
                if((type==181 || type==566) && scene!=0 && scene!=3)continue;
                var p=Reset(context);var n=Target(2,new Vector2(1110,390),scene==2);n.dontTakeDamage=scene!=1 && scene!=2;var timeline=Timeline(a,n);
                if(scene>=4 && scene<=6)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==4?0:scene==5?2:3);}
                if(scene==3)for(int y=10;y<60;y++)NativeToolsChecks.Tile(60,y,1);
                if(scene==8){NativeToolsChecks.Tile(60,25,1);Main.tile[60,25].slope(1);}
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),scene==8?new Vector2(936,390):new Vector2(950,350),scene==3?new Vector2(24,0):scene==8?new Vector2(24,8):new Vector2(6,0),type,0,0,p.whoAmI,scene==1 || scene==2?30:0);
                var q=Main.projectile[slot];if(scene==7)q.timeLeft=2;var state=Activator.CreateInstance(stateType,Flags,null,new object[]{q},null);var targets=targetsType.GetMethod("CaptureWetChase",Flags).Invoke(null,new object[]{q,1L});var terrain=Terrain(a);
                var step=bee.GetMethod(type==189 || type==316?"StepWaspBat":"Step",Flags);
                for(int outer=1;outer<=80 && q.active;outer++)
                {
                    int updates=(int)Get(state,"Extra")+1;
                    for(int sub=0;sub<updates;sub++)
                    {
                        int pen=(int)Get(state,"Penetration");var args=new object[]{q,targets,timeline,outer,terrain,state};Require((bool)step.Invoke(null,args),"Wasp/Bat retains motor scene dependencies");state=args[5];if((int)Get(state,"Penetration")<pen && type!=316)reflections++;
                        var tail=new object[]{state};bee.GetMethod("Tail",Flags).Invoke(null,tail);state=tail[0];actions++;if((bool)Get(state,"Stopped"))break;
                    }
                    q.Update(slot);
                    Require(q.active==(bool)Get(state,"Alive") && q.timeLeft==(int)Get(state,"Left") && q.extraUpdates==(int)Get(state,"Extra") && q.alpha==(int)Get(state,"Alpha") && q.ai[0]==(float)Get(state,"Counter") && q.penetrate==(int)Get(state,"Penetration") && Vector2.DistanceSquared(q.position,(Vector2)Get(state,"Position"))<.002f && Vector2.DistanceSquared(q.velocity,(Vector2)Get(state,"Velocity"))<.0001f,"native Wasp/Bat full motor: "+type+"/"+scene+"/"+outer+" native="+q.position+"/"+q.velocity+" ai="+q.ai[0]+" left="+q.timeLeft+" pen="+q.penetrate+" alpha="+q.alpha+" extra="+q.extraUpdates+" model="+Get(state,"Position")+"/"+Get(state,"Velocity")+" ai="+Get(state,"Counter")+" left="+Get(state,"Left")+" pen="+Get(state,"Penetration")+" alpha="+Get(state,"Alpha")+" extra="+Get(state,"Extra"));
                    if(scene==2)Require((int)Get(state,"TargetSlot")==-1,"wet non370 target cannot cause autonomous steering");
                    if(scene==1 && outer==1)Require((int)Get(state,"TargetSlot")==2 && q.velocity.Y>0,"counter30's first native action acquires the target and exercises the role's acquired speed/acceleration");
                }
                if(scene==3 && type==316){Require(!q.active && q.penetrate==1,"Bat wall collision kills without Wasp penetration reflection");batWallKills++;}
                if(scene==4)Require(!q.active,"ordinary water kills the AI036 role before its next Damage");
                if(scene==2){Require((bool)Get(targets,"Current"),"wet chase lease remains current");n.wet=false;Require(!(bool)Get(targets,"Current"),"target wet-state changes invalidate preparation");}
                cases++;
            }
            Require(cases==22 && reflections>0 && batWallKills==1,"Wasp/Bat branch motor and adjacent unchanged Bee roles execute");Console.WriteLine("PASS WASP BAT motors="+cases+" actions="+actions+" reflections="+reflections+" batWallKills="+batWallKills);
            PartialShimmer(context,a,bee);
        }
        private static void PartialShimmer(object context,Assembly a,Type bee)
        {
            var stateType=bee.GetNestedType("State",BindingFlags.NonPublic);var targetsType=a.GetType("JueMingR.TerrariaHost.Combat.HostProjectileTargets",true);int cases=0;
            foreach(int type in new[]{189,316})foreach(bool descending in new[]{false,true})
            {
                var p=Reset(context);var n=Target(2,new Vector2(1110,390));n.dontTakeDamage=true;var timeline=Timeline(a,n);
                for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=128;Main.tile[x,y].liquidType(3);}
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(950,350),new Vector2(6,descending?2:-2),type,0,0,p.whoAmI);
                var q=Main.projectile[slot];
                // A faded, already-born AI036 member has extraUpdates zero.
                // One complete native outer therefore isolates this Shimmer
                // branch before a later sub can re-enter the partial liquid.
                q.ai[0]=10;q.alpha=0;q.extraUpdates=0;q.timeLeft=590;
                Require(q.wet && q.shimmerWet && Main.tile[(int)q.Center.X/16,(int)q.position.Y/16].liquid==128,"partial Shimmer scene is wet without a full native Kill column");
                var state=Activator.CreateInstance(stateType,Flags,null,new object[]{q},null);var targets=targetsType.GetMethod("CaptureWetChase",Flags).Invoke(null,new object[]{q,1L});var terrain=Terrain(a);
                var args=new object[]{q,targets,timeline,1,terrain,state};Require((bool)bee.GetMethod("StepWaspBat",Flags).Invoke(null,args),"partial Shimmer retains its actual column/movement dependencies");state=args[5];
                var tail=new object[]{state};bee.GetMethod("Tail",Flags).Invoke(null,tail);state=tail[0];q.Update(slot);
                Require(q.active && (bool)Get(state,"Alive") && q.timeLeft==(int)Get(state,"Left") && q.extraUpdates==(int)Get(state,"Extra") && q.alpha==(int)Get(state,"Alpha") && q.ai[0]==(float)Get(state,"Counter") && q.penetrate==(int)Get(state,"Penetration") && q.wet==(bool)Get(state,"Wet") && q.shimmerWet==(bool)Get(state,"Shimmer") && Vector2.DistanceSquared(q.position,(Vector2)Get(state,"Position"))<.002f && Vector2.DistanceSquared(q.velocity,(Vector2)Get(state,"Velocity"))<.0001f,"native partial Shimmer full outer agrees: "+type+"/"+descending+" native="+q.position+"/"+q.velocity+" wet="+q.wet+" shimmer="+q.shimmerWet+" model="+Get(state,"Position")+"/"+Get(state,"Velocity")+" wet="+Get(state,"Wet")+" shimmer="+Get(state,"Shimmer"));
                float expectedY=-(2+(type==189?.2f:.25f));
                Require(Math.Abs(q.velocity.Y-expectedY)<.0001f && (descending?!q.wet && !q.shimmerWet:q.wet && q.shimmerWet),"partial Shimmer reverses only descending AI036; ascending retains its post-AI velocity and wet flags");
                Console.WriteLine("WASP BAT partial Shimmer type="+type+" descending="+descending+" pos="+q.position+" V="+q.velocity+" left="+q.timeLeft+" wet="+q.wet+" shimmer="+q.shimmerWet);cases++;
            }
            Require(cases==4,"both partial Shimmer directions execute for both native AI036 hand roles");Console.WriteLine("PASS WASP BAT partial Shimmer=4 native Kill-column negatives/reversal/upward retention");
        }
        private static void Receives(object context,Assembly a,Type model)
        {
            var obstacleType=a.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true);var clockType=a.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true);var phaseType=a.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true);int cases=0,hitCases=0;
            var foreign=Main.player[254];bool foreignActive=foreign.active;
            try
            {
                foreach(int type in new[]{189,316})foreach(int scene in Enumerable.Range(0,4))
                {
                    var p=Reset(context);role=type;target=Target(2,new Vector2(950,330));Projectile.perIDStaticNPCImmunity[189,2]=0;
                    Projectile ambient=null;
                    if(scene==0){target.immune[p.whoAmI]=200;if(type==189)Projectile.perIDStaticNPCImmunity[189,2]=Main.GameUpdateCount+200;}
                    if(scene==1 || scene==2)
                    {
                        foreign.active=true;int old=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),target.Center,Vector2.Zero,type,40,0,scene==2?254:p.whoAmI);ambient=Main.projectile[old];ambient.tileCollide=false;
                    }
                    if(scene==3)front=Target(1,target.position);
                    int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),target.Center,Vector2.Zero,type,40,0,p.whoAmI);source=Main.projectile[slot];source.tileCollide=false;
                    var timeline=Timeline(a,target);var terrain=Terrain(a);var obstacles=Activator.CreateInstance(obstacleType,Flags,null,new object[]{p,source,timeline,0,true,false},null);var clock=Activator.CreateInstance(clockType,Flags,null,new object[]{timeline,Enum.Parse(phaseType,"BeforeNpc"),0},null);
                    var plan=(AttackContact)model.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{p,new[]{source},timeline,clock,terrain,obstacles});
                    Require((bool)Get(obstacles,"Unchanged"),"actual family/targets/receive policy is current after Prepare");
                    if(type==189){bool saved=p.strongBees;p.strongBees=!saved;Require(!(bool)Get(obstacles,"Unchanged"),"actual owner strong-bee Damage premise invalidates the consume lease");p.strongBees=saved;}
                    if(ambient!=null && type==189){var old=ambient.position;ambient.position.X++;Require(!(bool)Get(obstacles,"Unchanged"),"ambient static writer invalidates the attached family lease");ambient.position=old;}
                    watching=true;natural=false;first=-1;long start=Main.GameUpdateCount;
                    for(int outer=1;outer<=120;outer++)
                    {
                        NativeQuickItemChecks.BeginWorldStep();if(front!=null)front.UpdateNPC(front.whoAmI);target.UpdateNPC(2);
                        for(int index=0;index<Main.projectile.Length;index++){var q=Main.projectile[index];if(q.active && q.type==type)q.Update(index);}
                        if(first>=0 || !source.active)break;
                    }
                    watching=false;int actual=first<0?0:(int)(first-start);
                    Require(actual==(plan?.Tick??0),"actual source-specific first Damage agrees with ambient/immune slot history: "+type+"/"+scene+" native="+actual+" plan="+plan?.Tick+" front="+frontHits);
                    if(scene==0)Require(type==189?actual==0:actual==1,"Wasp static expiry blocks while Bat penetrate1 bypasses owner immunity");
                    if(scene==1)Require(type==189?actual>1:actual==1,"ambient Wasp static history delays only the new Wasp source");
                    if(scene==2)Require(actual==1,"foreign PVE gate cannot manufacture local static/owner immunity");
                    if(scene==3)Require(frontHits>0 && (type==189?actual==1:actual==0),"native lower NPC slot consumes Bat's only hit but Wasp reaches selected with its finite budget");
                    if(actual>0)hitCases++;cases++;Console.WriteLine("WASP BAT receives type="+type+" scene="+scene+" first="+actual+" plan="+plan?.Tick+" front="+frontHits);
                }
            }
            finally{watching=false;foreign.active=foreignActive;}
            Require(cases==8 && hitCases>0,"every role's native receive positive and negative executes");Console.WriteLine("PASS WASP BAT receives="+cases+" usefulSources="+hitCases);
        }
        private static void Natural(object context,Assembly a,Type model)
        {
            var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");int cases=0;string only=Environment.GetEnvironmentVariable("JMR_WASP_BAT_NATURAL_CASE");naturalAssembly=a;naturalModel=model;
            foreach(int item in new[]{1155,1801})foreach(bool pack in new[]{false,true})
            foreach(bool near in new[]{false,true})
            {
                string caseName=item+"/"+pack+(near?"/near":"");
                if(item==1801 && pack || item!=1801 && near || !string.IsNullOrEmpty(only) && only!=caseName)continue;naturalBirths.Clear();var p=Reset(context,item);naturalNear=near;role=item==1155?189:316;p.position=new Vector2(650,646);p.statMana=p.statManaMax2=200;p.releaseUseItem=true;Main.screenPosition=new Vector2(500,400);
                // Natural scenes keep the genuine dummy/placed tile and floor.
                for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);for(int i=3;i<10;i++)p.armor[i].TurnToAir();if(pack)p.armor[3].SetDefaults(3333);
                // Bat's legal ±1.75 spread can miss the distant immortal
                // dummy: it cannot autonomously chase that body. Tile43's
                // closer dummy is a distinct positive: all legal launch
                // components cross its body within four actions, before the
                // worst downward spread reaches the real floor at center680.
                target=NativeCombatControlPreviewChecks.Dummy(2,near?43:52,40);raw=target.Center-new Vector2(390,0);solves=captures=motors=0;
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));
                for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,raw,false,true);
                Require(p.strongBees==pack,"natural Hive Pack establishes the Wasp Damage premise");prepared=(AttackContact)GetOptional(attack,"ExpectedPreview");Require(prepared!=null && prepared.Confidence==AttackConfidence.Representative && !Main.projectile.Any(q=>q.active),"idle Wasp/Bat gives a legal random birth representative without firing");
                int s=solves,c=captures,m=motors;var marker=Get(Get(host,"World"),"Impact");var zoom=Main.GameViewMatrix.ZoomMatrix;
                for(int draw=0;draw<20;draw++){Call(marker,"Capture");Call(marker,"Project",zoom,Matrix.Invert(zoom));Require((bool)Call(attack,"PreviewCurrent",prepared),"same-update Wasp/Bat presentation retains its current prepared lease");}
                Require(s==solves && c==captures && m==motors && captures==solves && captures>0,"same-update Draw performs no solve/capture/motor; each solve captures one shared target scene");
                if(role==189)
                {
                    // The solver owns a private player pose. Its gear lease must
                    // still reject a live owner's changed flag within this update.
                    bool strong=p.strongBees;
                    try
                    {
                        p.strongBees=!strong;
                        Require(!(bool)Call(attack,"PreviewCurrent",prepared),"live strongBees change invalidates the old private-pose Wasp preview");
                    }
                    finally{p.strongBees=strong;}
                    Require((bool)Call(attack,"PreviewCurrent",prepared),"restored live Wasp premise retains the same-update preview");
                    Require(s==solves && c==captures && m==motors,"gear lease presentation checks perform no solve/capture/motor");
                }
                baseDamage=p.GetWeaponDamage(p.HeldItem);long start=Main.GameUpdateCount;watching=natural=true;first=-1;births=0;
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));NativeCombatSelectionPreviewChecks.Step(context,raw,true);
                TraceBats(0);
                Require(births>=2 && births<=(role==189?4:3),"actual hand creates the complete legal native random count without a seeded favorable draw");
                for(int i=1;i<120 && (first<0 || role==316);i++){NativeCombatSelectionPreviewChecks.Step(context,raw,false);TraceBats(i);}
                foreach(var born in naturalBirths)
                {
                    int actual=born.First<0?0:(int)(born.First-born.SampleTick);
                    Console.WriteLine("BAT actual replay slot="+born.Shot.whoAmI+" native="+actual+" known="+born.Known?.Tick);
                    Require(actual==(born.Known?.Tick??0),"each actual Bat birth's native first Damage agrees with its own known replay");
                }
                // Dummy488 is immortal; actual positive Strike is the useful
                // damage witness, not a fabricated life reduction.
                if(role==189 || near)Require(first>=start,"actual one-press/release Wasp or guaranteed-near Bat family delivers useful native Strike");watching=natural=false;
                Console.WriteLine("WASP BAT natural item="+item+" pack="+pack+" near="+near+" births="+births+" first="+(first<0?0:first-start)+" randomMiss="+(first<0?1:0)+" representative="+prepared.Tick+" solves="+solves+" captures="+captures+" motor="+motors);cases++;
            }
            Require(cases==(string.IsNullOrEmpty(only)?4:1),"both real weapons, Wasp's actual gear branch and Bat's guaranteed positive/random stage execute, or the explicit narrow case");Console.WriteLine("PASS WASP BAT natural="+cases+" birth/usefulDamage/readonly presentation");
        }
        private static void TraceBats(int step)
        {
            foreach(var born in naturalBirths)
            {
                var shot=born.Shot;if(step!=0 && step!=9 && (shot.active || born.Retired))continue;
                Console.WriteLine("BAT native flight step="+step+" slot="+shot.whoAmI+" active="+shot.active+" center="+shot.Center+" V="+shot.velocity+" counter="+shot.ai[0]+" extra="+shot.extraUpdates+" left="+shot.timeLeft+" pen="+shot.penetrate+" damage="+shot.damage+" friendly="+shot.friendly+" hostile="+shot.hostile+" ownerImmune="+target.immune[shot.owner]+" target="+target.Hitbox+" chase="+target.CanBeChasedBy(shot));
                if(!shot.active)born.Retired=true;
            }
        }
    }
}
