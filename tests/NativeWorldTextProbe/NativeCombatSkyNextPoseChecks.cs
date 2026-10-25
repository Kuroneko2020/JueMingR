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
    // A real Source publication and CompletedWorld hand are retained across a
    // deterministic exhaustion at PrepareSample's first production Check.
    // Native Shoot/AI/Update remain original; the fixture owns only isolated
    // actors, terrain, RNG and the budget's test-controlled consumed amount.
    internal static class NativeCombatSkyNextPoseChecks
    {
        const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        static object host,combat,attack,prediction;static Assembly assembly;static Type sky;
        static Player owner,pose,poseForReplay;static object root;static Vector2 suppliedOrigin;
        static int skyCalls,arrowCalls,sourceCalls,coreCalls,beginCalls,poseApplications;
        static bool exhaustAtSource,shooting,trackNative,legacy;static CombatPredictionWork work;
        static readonly List<Projectile> births=new List<Projectile>();
        static Projectile tracked;static int substep,firstEnabled,firstKilled;static Vector2 enabledAt,killedAt;
        static bool modelReplay;static int modelStep,modelFirstBlocked;static Vector2 modelBlockedAt;
        static bool nativeWitness,sourcePreserved;static int sourceBudgetStops,nativeShootEntries,livePoseApplications;
        static object retainedSolution;static NpcTrajectory retainedPublication;static PredictionPlayerMotion retainedMotion,shootMotion;
        static uint retainedPrepared;static Vector2 retainedOrigin;static int retainedSky,retainedCore,retainedSource,retainedBegin;
        static void Sky(Player __0,Vector2 __4,object __8){skyCalls++;pose=__0;poseForReplay=(Player)typeof(object).GetMethod("MemberwiseClone",F).Invoke(__0,null);suppliedOrigin=__4;root=__8;}
        static void MotionStep(){if(modelReplay)modelStep++;}
        static void Passage(bool __result,float __0,float __1)
        {if(modelReplay && !__result && modelFirstBlocked==0){modelFirstBlocked=modelStep;modelBlockedAt=new Vector2(__0,__1);}}
        static void Arrow(){arrowCalls++;}
        static void Core(){coreCalls++;}
        static void Begin(){beginCalls++;}
        static void Applied(Player __0){if(!ReferenceEquals(__0,owner))poseApplications++;else if(nativeWitness)livePoseApplications++;}
        static void Source()
        {
            sourceCalls++;
            if(!exhaustAtSource)return;
            // Enter already established this real update. Charge its original
            // hard allowance, then let Check/TryContinue execute unchanged.
            typeof(CombatPredictionWork).GetField("spent",F).SetValue(work,typeof(CombatPredictionWork).GetField("allowance",F).GetValue(work));
            if(nativeWitness)sourcePreserved=RetainedFactsCurrent();
        }
        static void SourceFinished(Exception __exception)
        {if(nativeWitness && __exception is CombatPredictionBudgetExceededException)sourceBudgetStops++;}
        static bool SameSolution(object a,object b)
        {
            // HostAttackSolution is a value. Reflection boxes each read; only
            // its actual reference members have reference-identity meaning.
            return a.GetType().GetFields(F).Where(f=>!f.IsStatic).All(f=>f.Name=="Timeline" || f.Name=="Contact"?
                ReferenceEquals(f.GetValue(a),f.GetValue(b)):Equals(f.GetValue(a),f.GetValue(b)));
        }
        static bool RetainedFactsCurrent()
        {return ReferenceEquals(retainedPublication,((NpcPredictionCache)Get(prediction,"Cache")).Read(1)) &&
            SameSolution(retainedSolution,Get(attack,"nextSolution")) && (uint)Get(attack,"prepared")==retainedPrepared &&
            retainedOrigin==(Vector2)Get(attack,"nextOrigin") && retainedMotion.SameSample((PredictionPlayerMotion)Get(attack,"nextBalancedMotion"));}
        [HarmonyPriority(Priority.First)]
        static void NativeShootStart(Player __instance)
        {
            if(!nativeWitness || !ReferenceEquals(__instance,owner))return;
            nativeShootEntries++;shootMotion=(PredictionPlayerMotion)prediction.GetType().GetMethod("ReadPlayer",F).Invoke(null,new object[]{owner});
            Console.WriteLine("RETAINED-NATIVE shoot-entry item="+owner.HeldItem.type+" sourceStops="+sourceBudgetStops+" sourcePreserved="+sourcePreserved+" retained="+RetainedFactsCurrent()+" sourceDelta="+(sourceCalls-retainedSource)+" coreDelta="+(coreCalls-retainedCore)+" skyDelta="+(skyCalls-retainedSky)+" nextTimelineSame="+ReferenceEquals(Get(retainedSolution,"Timeline"),Get(Get(attack,"nextSolution"),"Timeline")));
            foreach(var f in typeof(PredictionPlayerMotion).GetFields(F).Where(f=>!f.IsStatic))if(!Equals(f.GetValue(retainedMotion),f.GetValue(shootMotion)))Console.WriteLine("RETAINED-NATIVE motion-diff "+f.Name+" expected="+f.GetValue(retainedMotion)+" actual="+f.GetValue(shootMotion));
            Require(sourcePreserved && RetainedFactsCurrent() && sourceBudgetStops==1 && sourceCalls==retainedSource+1 && coreCalls==retainedCore && skyCalls==retainedSky,"original first Source Check stops before any replacement publication or Core/Sky solve");
            // Update may consume RNG before Shoot. Seed only at the actual
            // native emission entry, before production BeginShot, never before
            // the whole Update and never by rewriting a projectile birth.
            Main.rand=new Terraria.Utilities.UnifiedRandom(MeanSeed());births.Clear();shooting=true;
        }
        static void NativeShootEnd()
        {
            if(!nativeWitness)return;shooting=false;
            foreach(var q in births)Console.WriteLine("RETAINED-NATIVE birth ordinal="+births.IndexOf(q)+" type="+q.type+" key="+q.key+" center="+q.Center+" v="+q.velocity+" ai1="+q.ai[1]+" tile="+q.tileCollide+" seedAtShoot="+MeanSeed());
        }
        static void Born(int __result)
        {if(shooting && __result>=0 && __result<Main.maxProjectiles){var q=Main.projectile[__result];if(q.owner==owner.whoAmI)births.Add(q);}}
        static void AiStart(Projectile __instance){if(trackNative && ReferenceEquals(__instance,tracked))substep++;}
        static void AiEnd(Projectile __instance)
        {if(trackNative && ReferenceEquals(__instance,tracked) && firstEnabled==0 && __instance.tileCollide){firstEnabled=substep;enabledAt=__instance.Center;}}
        static void Kill(Projectile __instance)
        {if(trackNative && ReferenceEquals(__instance,tracked) && firstKilled==0){firstKilled=substep;killedAt=__instance.Center;}}
        static Vector2 Origin(Player p)
        {return (Vector2)assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels",true).GetMethod("LaunchOrigin",F,null,new[]{typeof(Player),typeof(Item)},null).Invoke(null,new object[]{p,p.HeldItem});}
        static void SetTick(uint tick){typeof(Main).GetField("_gameUpdateCount",F).SetValue(null,tick);}
        internal static void Run(object context)
        {
            host=Get(context,"CombatObservation");combat=Get(context,"Combat");attack=Get(combat,"Attack");prediction=Get(host,"Prediction");assembly=attack.GetType().Assembly;sky=assembly.GetType("JueMingR.TerrariaHost.Combat.HostSkyAttack",true);work=(CombatPredictionWork)Get(host,"PredictionWork");
            legacy=Environment.GetEnvironmentVariable("JUEMINGR_SKY_CANDIDATE")!=null;
            var audit=new Harmony("JueMingR.Tests.SkyNextPose20261025");
            audit.Patch(sky.GetMethod("Prepare",F),prefix:new HarmonyMethod(typeof(NativeCombatSkyNextPoseChecks),nameof(Sky)));
            audit.Patch(assembly.GetType("JueMingR.TerrariaHost.Combat.HostNativeArrowBirthAttack",true).GetMethod("Prepare",F),prefix:new HarmonyMethod(typeof(NativeCombatSkyNextPoseChecks),nameof(Arrow)));
            audit.Patch(attack.GetType().GetMethod("PrepareCore",F),prefix:new HarmonyMethod(typeof(NativeCombatSkyNextPoseChecks),nameof(Core)));
            audit.Patch(attack.GetType().GetMethod("BeginShot",F),prefix:new HarmonyMethod(typeof(NativeCombatSkyNextPoseChecks),nameof(Begin)));
            audit.Patch(prediction.GetType().GetMethod("PrepareSample",F),prefix:new HarmonyMethod(typeof(NativeCombatSkyNextPoseChecks),nameof(Source)),finalizer:new HarmonyMethod(typeof(NativeCombatSkyNextPoseChecks),nameof(SourceFinished)));
            audit.Patch(typeof(Player).GetMethod("ItemCheck_Shoot",F),prefix:new HarmonyMethod(typeof(NativeCombatSkyNextPoseChecks),nameof(NativeShootStart)),postfix:new HarmonyMethod(typeof(NativeCombatSkyNextPoseChecks),nameof(NativeShootEnd)));
            audit.Patch(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPlayerFrame",true).GetMethod("ApplyPose",F),prefix:new HarmonyMethod(typeof(NativeCombatSkyNextPoseChecks),nameof(Applied)));
            audit.Patch(typeof(Projectile).GetMethods(F).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatSkyNextPoseChecks),nameof(Born)));
            audit.Patch(typeof(Projectile).GetMethod("AI",F),prefix:new HarmonyMethod(typeof(NativeCombatSkyNextPoseChecks),nameof(AiStart)),postfix:new HarmonyMethod(typeof(NativeCombatSkyNextPoseChecks),nameof(AiEnd)));
            audit.Patch(typeof(Projectile).GetMethod("Kill",F),prefix:new HarmonyMethod(typeof(NativeCombatSkyNextPoseChecks),nameof(Kill)));
            audit.Patch(typeof(AttackMotion).GetMethod("Advance",F),prefix:new HarmonyMethod(typeof(NativeCombatSkyNextPoseChecks),nameof(MotionStep)));
            audit.Patch(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true).GetMethod("ProjectilePassage",F),postfix:new HarmonyMethod(typeof(NativeCombatSkyNextPoseChecks),nameof(Passage)));
            try
            {
                NativeRetainedConsume(context,3065);
                NativeRetainedConsume(context,ItemID.WoodenBow);
                Roof(context);
                Meteor(context);
                Prepare(context,3065,-10,false);BudgetConsume(context,true);
                Prepare(context,ItemID.WoodenBow,-5,false);BudgetConsume(context,true);
                PayloadClock(context);
                foreach(int item in new[]{3570,2750,4381,65,3065,3029})Dispatch(context,item);
                foreach(float vy in new[]{0f,5f}){Prepare(context,3065,vy,false);Require(vy==0 || pose.position.Y>owner.position.Y,"falling next Sky keeps the vertical sign");}
                CloneCosts();
                Console.WriteLine("SKY-NEXT PASS source=real production-budget-stop=real consumed=BeginShot native=Shoot/AI/Update damage=NOT_EVALUATED");
            }
            finally{nativeWitness=exhaustAtSource=shooting=trackNative=modelReplay=false;owner=pose=poseForReplay=null;root=null;tracked=null;births.Clear();audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());Main.ProjectileUpdateLoopIndex=-1;}
        }
        static void NativeJumpStep(object context,bool fire)
        {
            NativeToolExecutionChecks.Sample(context,Get(context,"Input"),Main.npc[2].Center,fire);Call(combat,"Sample");
            Terraria.GameInput.PlayerInput.Triggers.Current.Jump=true;owner.controlJump=true;
            NativeQuickItemChecks.BeginWorldStep();owner.Update(owner.whoAmI);
        }
        static void NativeRetainedConsume(object context,int item)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            owner=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),item,0,0);
            foreach(var a in owner.armor)a.TurnToAir();owner.ResetEffects();owner.mount.Dismount(owner);
            foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;
            for(int x=5;x<115;x++)for(int y=5;y<115;y++)Main.tile[x,y].ClearEverything();
            for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,65,TileID.Stone);
            owner.position=new Vector2(700,65*16-owner.height);owner.velocity=Vector2.Zero;owner.jump=0;owner.releaseJump=true;owner.fallStart=(int)(owner.position.Y/16);
            owner.immune=false;owner.immuneTime=0;owner.statLife=owner.statLifeMax=owner.statLifeMax2=400;owner.lifeRegenTime=0;owner.lifeRegenCount=0;
            owner.inventory[54].SetDefaults(ItemID.WoodenArrow);owner.inventory[54].stack=999;owner.statMana=owner.statManaMax=owner.statManaMax2=10000;
            // Keep both completed observations in native's ordinary .25
            // altitude clamp. Its changing altitude gravity is a real exact
            // dependency, unlike the two naturally changing clocks below.
            Main.windPhysics=false;Main.worldSurface=60;Main.screenPosition=new Vector2(600.25f,700.75f);Main.GameViewMatrix.Zoom=Vector2.One;Main.rand=new Terraria.Utilities.UnifiedRandom(7193);
            var target=Main.npc[2];target.SetDefaults(NPCID.Zombie);target.whoAmI=2;target.active=true;target.width=target.height=34;target.Center=new Vector2(1100,1000);target.velocity=Vector2.Zero;target.noGravity=target.noTileCollide=true;target.aiStyle=-1;target.life=target.lifeMax=100000;target.target=0;target.knockBackResist=0;Array.Clear(target.immune,0,target.immune.Length);
            var native=new Harmony("JueMingR.Tests.RetainedNativeUpdate");
            foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})native.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,F),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks).GetMethod("SkipAchievement",F)));
            try
            {
                // Start on actual solid ground, then let two complete native
                // held-jump updates establish the airborne prefix and old row.
                // No forecast pose/timer is ever written to this live player.
                NativeJumpStep(context,false);NativeJumpStep(context,false);
                Console.WriteLine("RETAINED-NATIVE premise item="+item+" p="+owner.position+" v="+owner.velocity+" jump="+owner.jump+" fall="+owner.fallStart+" row="+(int)(owner.position.Y/16)+" regen="+owner.lifeRegenTime+" count="+owner.lifeRegenCount);
                Require(owner.jump>1 && owner.controlJump && owner.velocity.Y<0 && owner.fallStart!=(int)(owner.position.Y/16),"original ground jump establishes a legal held-jump row crossing");
                var cache=(NpcPredictionCache)Get(prediction,"Cache");var selection=Get(host,"Selection");
                bool preparedNext=false;int attempts=0;
                for(;attempts<24;attempts++)
                {
                    NativeToolExecutionChecks.Sample(context,Get(context,"Input"),target.Center,true);Call(combat,"Sample");Terraria.GameInput.PlayerInput.Triggers.Current.Jump=true;owner.controlJump=true;
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,50,true,true));
                    Call(selection,"Update",((ObservationSettings)Get(host,"Settings")).Value,(long)Get(host,"Session"),true,null);cache.Demand(1,1,120);
                    using(work.Enter(Main.GameUpdateCount))
                    {
                        Call(host,"PrepareSelectedPrediction",(long)Main.GameUpdateCount,false,(ulong)Main.GameUpdateCount,120);
                        Call(attack,"Prepare");
                    }
                    if((bool)Get(Get(attack,"nextSolution"),"HasPoint") && (uint)Get(attack,"prepared")==Main.GameUpdateCount &&
                        owner.jump>1 && owner.fallStart!=(int)(owner.position.Y/16)){preparedNext=true;break;}
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeJumpStep(context,false);
                }
                Console.WriteLine("RETAINED-NATIVE preparation item="+item+" budgetMs=6 attempts="+(attempts+1)+" usable="+preparedNext+" usedMs="+work.UsedMilliseconds);
                Require(preparedNext,"finite normal shared 6ms Source/Host window supplies a legal next hand");
                retainedSolution=Get(attack,"nextSolution");retainedPublication=cache.Read(1);retainedPrepared=(uint)Get(attack,"prepared");retainedOrigin=(Vector2)Get(attack,"nextOrigin");retainedMotion=(PredictionPlayerMotion)Get(attack,"nextBalancedMotion");
                retainedSky=skyCalls;retainedCore=coreCalls;retainedSource=sourceCalls;retainedBegin=beginCalls;sourceBudgetStops=nativeShootEntries=livePoseApplications=0;sourcePreserved=false;
                float regen=owner.lifeRegenTime;int count=owner.lifeRegenCount,fall=owner.fallStart,hp=owner.statLife,immuneTime=owner.immuneTime;bool immune=owner.immune;
                Require(retainedPublication!=null && ReferenceEquals(Get(retainedSolution,"Timeline"),retainedPublication) && !retainedMotion.WallSourceExpected && retainedPublication.WallSources==null,"retained hand belongs to the real Source publication without a Wall payload");
                nativeWitness=exhaustAtSource=true;
                try{NativeJumpStep(context,true);}finally{nativeWitness=exhaustAtSource=shooting=false;}
                var consumed=Get(attack,"consumedInput");
                Console.WriteLine("RETAINED-NATIVE result item="+item+" update="+Main.GameUpdateCount+" prepared="+retainedPrepared+" sample="+retainedPublication.SampleTick+" capture="+retainedPublication.CaptureTick+" regen="+regen+"->"+owner.lifeRegenTime+" count="+count+"->"+owner.lifeRegenCount+" fall="+fall+"->"+owner.fallStart+" hp="+hp+"->"+owner.statLife+" immune="+immune+"/"+immuneTime+"->"+owner.immune+"/"+owner.immuneTime+" pos="+owner.position+" v="+owner.velocity+" expected="+retainedMotion.X+","+retainedMotion.Y+"/"+retainedMotion.Vx+","+retainedMotion.Vy+" kind="+Get(consumed,"Kind")+" births="+births.Count+" seedAtShoot="+MeanSeed());
                Require(owner.lifeRegenTime==regen+1 && owner.fallStart!=fall && owner.statLife==hp && owner.immune==immune && owner.immuneTime==immuneTime,"one full original Update naturally changes regen and fall row while exact health/immunity stay valid");
                Require(nativeShootEntries==1 && beginCalls==retainedBegin+1 && births.Count==(item==3065?3:1) && livePoseApplications==0,"one native ItemCheck/Shoot/BeginShot emits original members without a live ApplyPose");
                Require((bool)Get(attack,"hasConsumedInput") && Get(consumed,"Kind").ToString()=="Enhanced" && (Vector2)Get(consumed,"Point")== (Vector2)Get(retainedSolution,"Point"),"full original Player.Update consumes the retained next as Enhanced");
                Require((Vector2)Get(attack,"consumedOrigin")==retainedOrigin && (uint)Get(attack,"consumedInputStep")==Main.GameUpdateCount &&
                    ((NpcIdentity)Get(consumed,"Target")).Equals(retainedPublication.Identity) && ReferenceEquals(Get(consumed,"Weapon"),owner.HeldItem) && Equals(Get(consumed,"PointSemantics"),Get(retainedSolution,"PointSemantics")),"actual consumed origin/epoch/target/weapon/point meaning belong to the retained hand");
                var exact=shootMotion;exact.GravityAction=1;exact.WallRegenTime=retainedMotion.WallRegenTime;exact.WallRegenCount=retainedMotion.WallRegenCount;exact.WallFallStart=retainedMotion.WallFallStart;
                Require(retainedMotion.SameSample(exact),"all sampled physical/health/control facts match after only the independently justified clocks and ordinal are aligned in a local oracle copy");
                Require(!(bool)Get(Get(attack,"nextSolution"),"HasPoint") && !(bool)Get(Get(attack,"currentSolution"),"HasPoint") && retainedMotion.SameSample((PredictionPlayerMotion)Get(attack,"nextBalancedMotion")),"one native consumption retires both solutions without modifying saved motion");
                RetainedDependencies();
            }
            finally{nativeWitness=exhaustAtSource=shooting=false;native.UnpatchAll(native.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());}
        }
        static PredictionPlayerMotion Changed(PredictionPlayerMotion value,string name,object replacement=null)
        {
            object box=value;var field=typeof(PredictionPlayerMotion).GetField(name,F);object old=field.GetValue(box);
            field.SetValue(box,replacement??(old is bool?(object)!(bool)old:old is int?(object)((int)old+1):old is float?(object)((float)old+1):new object()));
            return (PredictionPlayerMotion)box;
        }
        static void RetainedDependencies()
        {
            var compare=attack.GetType().GetMethod("RetainedNextMotionCurrent",F);
            var a=retainedMotion;var b=a;b.WallRegenTime++;b.WallRegenCount++;b.WallFallStart++;
            var world=default(PredictionEnvironment);var path=retainedPublication;
            Func<PredictionPlayerMotion,PredictionPlayerMotion,NpcTrajectory,PredictionEnvironment,bool> same=(x,y,t,e)=>(bool)compare.Invoke(null,new object[]{x,y,t,e});
            Require(!a.SameSample(b) && !PlayerMotionContinuation.SameFixedInput(a,b,false) && same(a,b,path,world),"global sample and existing fixed-input fall semantics remain strict while only retained-next admits its three non-dependencies");
            foreach(string name in new[]{"WallSourceExpected","WallGross","WallTongued","WallHorrifiedTicks","WallTonguedTicks"})
            {
                object value=name.EndsWith("Ticks",StringComparison.Ordinal)?(object)5:true;
                Require(!same(Changed(a,name,value),Changed(b,name,value),path,world),"either retained/observed Wall qualification forbids the exemption: "+name);
                Require(!same(Changed(a,name,value),b,path,world) && !same(a,Changed(b,name,value),path,world),"changed Wall qualification remains exact on both sides: "+name);
            }
            var wallValues=Enumerable.Range(0,path.Count).Select(i=>new PredictionWallSource(true,false,default(NpcIdentity),default(MotionRect),0,0,0)).ToArray();
            var sources=new PredictionWallSources(wallValues,wallValues.Length,owner,owner.whoAmI,path.Identity,path.CaptureTick);
            var points=Enumerable.Range(0,path.Count).Select(i=>path[i]).ToArray();
            var wallPath=new NpcTrajectory(path.Identity,path.SampleTick,path.Version,path.Assumptions,path.Stop,points,points.Length,path.Strategy,path.RelationVersion,path.Quality,path.GravitySources,sources);
            Require(!same(a,b,wallPath,world),"real timeline Wall payload, including captured absence, forbids exemption");
            world.WallSources=sources;Require(!same(a,b,path,world),"bound environment Wall sequence forbids exemption");world.WallSources=null;
            world.WallSource=new PredictionWallSource(true,false,default(NpcIdentity),default(MotionRect),0,0,0);Require(!same(a,b,path,world),"captured individual Wall source forbids exemption even when not valid");world=default(PredictionEnvironment);
            string[] exact={"X","Y","Vx","Vy","Width","Height","Gravity","DefaultGravity","GravityDirection","ForcedGravityTicks","GravityReleaseUp","GravityControl","GravityControl2","GravitySourceExpected","GravityAction","WallLife","WallLifeMax","WallHealthKnown","WallImmune","WallImmuneTime","BordersDead","WallExtraFall","WallNoFallDamage","WallHasWings","Left","Right","Up","Down","HoldJump","ReleaseJump","AutoJump","Jump","JumpSpeed","JumpHeight","PlayerToken","PlayerIndex","WallAction"};
            foreach(string name in exact)Require(!same(a,Changed(b,name),path,world),"retained-next keeps the real dependency exact: "+name);
            Require(a.SameSample(retainedMotion) && retainedMotion.SameSample((PredictionPlayerMotion)Get(attack,"nextBalancedMotion")),"all dependency comparisons operate only on value copies");
            Console.WriteLine("RETAINED-NATIVE negatives Wall=timeline/environment/captured/expected/gross/tongued/buff-ticks physical-health-controls="+string.Join(",",exact)+" global-SameSample=STRICT existing-SameFixedInput-fall=STRICT savedMotion=UNCHANGED");
        }
        static NpcTrajectory Prepare(object context,int item,float vy,bool roof,bool gravityPayload=false)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            owner=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),item,0,0);
            foreach(var a in owner.armor)a.TurnToAir();owner.ResetEffects();owner.mount.Dismount(owner);
            if(vy==-10)LegalRisingPrefix();
            owner.position=new Vector2(700,914);owner.velocity=new Vector2(0,vy);owner.releaseUseItem=true;owner.controlJump=false;owner.jump=0;owner.releaseJump=true;
            owner.inventory[54].SetDefaults(ItemID.WoodenArrow);owner.inventory[54].stack=999;owner.statMana=owner.statManaMax=owner.statManaMax2=10000;
            foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;
            for(int x=5;x<115;x++)for(int y=5;y<115;y++)Main.tile[x,y].ClearEverything();
            if(roof)for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,47,TileID.Stone);
            var target=Main.npc[2];target.SetDefaults(NPCID.Zombie);target.whoAmI=2;target.active=true;target.width=target.height=34;target.Center=new Vector2(1100,1000);target.velocity=Vector2.Zero;target.noGravity=target.noTileCollide=true;target.aiStyle=-1;target.life=target.lifeMax=100000;target.target=0;target.knockBackResist=0;Array.Clear(target.immune,0,target.immune.Length);
            Main.windPhysics=false;Main.screenPosition=new Vector2(600.25f,700.75f);Main.GameViewMatrix.Zoom=Vector2.One;Main.worldSurface=10;Main.rand=new Terraria.Utilities.UnifiedRandom(7193);
            NativeToolExecutionChecks.Sample(context,Get(context,"Input"),target.Center,true);Call(combat,"Sample");
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,50,true,true));
            var selection=Get(host,"Selection");Call(selection,"Update",((ObservationSettings)Get(host,"Settings")).Value,(long)Get(host,"Session"),true,null);var id=(NpcIdentity)Get(selection,"Target");Require(id.Slot==2,"next Sky fixture selects its actual receiver");
            var cache=(NpcPredictionCache)Get(prediction,"Cache");cache.Demand(1,1,120);pose=null;root=null;skyCalls=arrowCalls=poseApplications=0;var original=owner.position;var originalVelocity=owner.velocity;
            using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))
            {
                // This is Source's real motor/publication, not PublishFixture.
                Call(prediction,"PreparePrefix",id,(long)Main.GameUpdateCount,120,120);
                if(gravityPayload)cache.Publish(WithGravity(cache.Read(1),owner,owner.whoAmI));
                Call(attack,"PrepareCore",Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld"));
            }
            var path=cache.Read(1);var next=Get(attack,"nextSolution");var motion=(PredictionPlayerMotion)Get(attack,"nextBalancedMotion");
            Console.WriteLine("SKY-NEXT prepare item="+item+" tick="+Main.GameUpdateCount+" sample="+(path?.SampleTick??-1)+" capture="+(path?.CaptureTick??-1)+" count="+(path?.Count??0)+" gravitySources="+(path?.GravitySources!=null)+" wallSources="+(path?.WallSources!=null)+" sourceCalls="+sourceCalls+" sky="+skyCalls+" arrow="+arrowCalls+" poseApplications="+poseApplications+" current="+original+" nextMotion="+motion.X+","+motion.Y+"/"+motion.Vx+","+motion.Vy+" pose="+(pose==null?"none":pose.position.ToString())+" origin="+Get(attack,"nextOrigin")+" has="+Get(next,"HasPoint")+" reach="+Get(next,"Reachability")+" point="+Get(next,"Point")+" roof="+roof);
            Require(path!=null && path.SampleTick==Main.GameUpdateCount && path.Count>40,"real Source supplies a fresh finite future for CompletedWorld");
            Require(owner.position==original && owner.velocity==originalVelocity,"preparation never mutates the live holder");
            Require(ReferenceEquals(motion.PlayerToken,owner),"advanced next motion retains the real player token");
            if(new[]{3570,2750,4381,65,3065}.Contains(item) && !legacy)
            {
                Require(skyCalls==1 && pose!=null && !ReferenceEquals(pose,owner) && pose.position==new Vector2(motion.X,motion.Y),"one actual next Sky reads a private future pose");
                Require(suppliedOrigin==Origin(pose) && suppliedOrigin==(Vector2)Get(attack,"nextOrigin"),"saved nextOrigin is computed directly from that future pose");
                Require(root!=null && ReferenceEquals(Get(root,"player"),owner),"Sky receiver root retains the real live owner");
                Require(poseApplications==1,"each affected next preparation applies exactly one private pose outside candidate/replay loops");
            }
            return path;
        }
        static void BudgetConsume(object context,bool native)
        {
            // Kept as a model-pose/budget-consumption mechanism check. Only
            // NativeRetainedConsume above proves a complete Player.Update.
            var cache=(NpcPredictionCache)Get(prediction,"Cache");var path=cache.Read(1);var next=Get(attack,"nextSolution");var point=(Vector2)Get(next,"Point");var motion=(PredictionPlayerMotion)Get(attack,"nextBalancedMotion");uint prepared=Main.GameUpdateCount;
            Require((bool)Get(next,"HasPoint"),"open-sky next hand has a usable point");
            assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPlayerFrame",true).GetMethod("ApplyPose",F).Invoke(null,new object[]{owner,motion});SetTick(prepared+1);
            NativeToolExecutionChecks.Sample(context,Get(context,"Input"),Main.npc[2].Center,true);Call(combat,"Sample");
            int solves=skyCalls,cores=coreCalls,sources=sourceCalls;exhaustAtSource=true;
            try
            {
                using(work.Enter(Main.GameUpdateCount))
                {
                    Call(host,"PrepareSelectedPrediction",path.SampleTick,true,(ulong)Main.GameUpdateCount,40);
                    Call(attack,"PrepareAction");
                }
            }
            finally{exhaustAtSource=false;}
            Require(work.Exceeded && sourceCalls==sources+1 && skyCalls==solves && coreCalls==cores,"production first Source Check exhausts then PreparePhase skips core without a replacement solve");
            Require(ReferenceEquals(path,cache.Read(1)) && (Vector2)Get(Get(attack,"nextSolution"),"Point")==point && (uint)Get(attack,"prepared")==prepared,"early budget stop retains the original publication and prepared next hand");
            var actualMotion=(PredictionPlayerMotion)prediction.GetType().GetMethod("ReadPlayer",F).Invoke(null,new object[]{owner});
            foreach(var f in typeof(PredictionPlayerMotion).GetFields(F).Where(f=>!f.IsStatic))if(!Equals(f.GetValue(motion),f.GetValue(actualMotion)))Console.WriteLine("SKY-NEXT motion-diff "+f.Name+" expected="+f.GetValue(motion)+" actual="+f.GetValue(actualMotion));
            Console.WriteLine("SKY-NEXT gates motion="+motion.SameSample(actualMotion)+" balancedTrue="+Call(attack,"BalancedInputCurrent",true,true)+" balancedFalse="+Call(attack,"BalancedInputCurrent",false,true)+" permission="+Get(attack,"Permission")+" receiver="+Get(attack,"CurrentReceiver")+" timeline="+Call(attack,"TimelineCurrent",path)+" environment="+Get(Get(attack,"environmentState"),"Current")+" terrain="+Get(Get(attack,"terrain"),"Unchanged")+" root="+(root==null?"none":Get(root,"Unchanged").ToString())+" origin="+Origin(owner));
            if(!legacy)
            {
                Require((bool)Call(attack,"BalancedInputCurrent",false,true),"unchanged real next hand passes its full consumption gate before negative controls");
                owner.position.X+=16;Require(!(bool)Call(attack,"BalancedInputCurrent",false,true),"physical displacement still rejects retained next");owner.position.X-=16;
                byte prefix=owner.HeldItem.prefix;owner.HeldItem.prefix++;Require(!(bool)Call(attack,"BalancedInputCurrent",false,true),"changed weapon/ammo identity still rejects retained next");owner.HeldItem.prefix=prefix;
                var target=Main.npc[2];Main.npc[2]=new NPC{whoAmI=2};Require(!(bool)Call(attack,"BalancedInputCurrent",false,true),"replacement receiver token still rejects retained next");Main.npc[2]=target;
                SetTick(prepared+2);Require(!(bool)Call(attack,"BalancedInputCurrent",false,true),"expired next remains rejected");SetTick(prepared+1);
                Require((bool)Call(attack,"BalancedInputCurrent",false,true),"negative controls restore the original legal next without a new solve");
            }
            int beginBefore=beginCalls;
            if(native)Shoot(MeanSeed());else {var scope=Call(attack,"BeginShot",owner,owner.HeldItem,true);Require(scope!=null,"retained next reaches a real cursor scope");Call(scope,"End");}
            var consumed=Get(attack,"consumedInput");
            Console.WriteLine("SKY-NEXT model-pose-budget-mechanism prepared="+prepared+" now="+Main.GameUpdateCount+" sample="+path.SampleTick+" sourceCheck="+sourceCalls+" noNewSolve="+(solves==skyCalls)+" noNewCore="+(cores==coreCalls)+" exceeded="+work.Exceeded+" begin="+(beginCalls-beginBefore)+" consumed="+Get(consumed,"Kind")+" point="+Get(consumed,"Point")+" expected="+point+" nativeBirths="+births.Count);
            Require(beginCalls>beginBefore && (bool)Get(attack,"hasConsumedInput") && Get(consumed,"Kind").ToString()=="Enhanced" && (Vector2)Get(consumed,"Point")==point,"BeginShot actually consumes that retained next as Enhanced");
            Require(!(bool)Get(Get(attack,"nextSolution"),"HasPoint") && !(bool)Get(Get(attack,"currentSolution"),"HasPoint"),"consumption retires both one-shot solutions");
            Require(motion.SameSample((PredictionPlayerMotion)Get(attack,"nextBalancedMotion")),"clock adaptation never rewrites the saved advanced motion");
        }
        static NpcTrajectory WithGravity(NpcTrajectory path,object playerToken,int playerIndex)
        {
            var points=Enumerable.Range(0,path.Count).Select(i=>path[i]).ToArray();
            var sources=Enumerable.Range(0,path.Count).Select(i=>new PredictionGravitySource(true,false,default(NpcIdentity),0,0)).ToArray();
            return new NpcTrajectory(path.Identity,path.SampleTick,path.Version,path.Assumptions,path.Stop,points,points.Length,PredictionStrategy.RollingConditional,path.RelationVersion,path.Quality,
                new PredictionGravitySources(sources,sources.Length,playerToken,playerIndex,false,path.Identity,path.SampleTick));
        }
        static void PayloadClock(object context)
        {
            // Only this clock mechanism supplies an explicit captured-absence
            // payload over Source's real points. The budget witness above has
            // no injected timeline/payload and remains a separate receipt.
            var path=Prepare(context,3065,-5,false,true);var motion=(PredictionPlayerMotion)Get(attack,"nextBalancedMotion");uint prepared=Main.GameUpdateCount;
            assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPlayerFrame",true).GetMethod("ApplyPose",F).Invoke(null,new object[]{owner,motion});SetTick(prepared+1);
            Require((bool)Call(attack,"BalancedMotionCurrent",owner,path),"real player observation binds the legitimate gravity payload at current trajectory ordinal");
            Require(!(bool)Call(attack,"BalancedMotionCurrent",owner,WithGravity(path,new object(),owner.whoAmI)),"wrong gravity player token rejects rather than borrowing same slot");
            Require(!(bool)Call(attack,"BalancedMotionCurrent",owner,WithGravity(path,owner,owner.whoAmI+1)),"wrong gravity player slot rejects");
            int forced=owner.forcedGravity;owner.forcedGravity=1;Require(!(bool)Call(attack,"BalancedMotionCurrent",owner,path),"real forced gravity remains an exact physical dependency");owner.forcedGravity=forced;
            var clone=(Player)typeof(object).GetMethod("MemberwiseClone",F).Invoke(owner,null);Require(!(bool)Call(attack,"BalancedMotionCurrent",clone,path),"a private same-whoAmI body is not the payload's real observed player");
            NpcTrajectory window;Require(path.TryWindow(path.SampleTick+2,path.Count-3,path.Version,out window),"clock negative has a legitimate later window");Require(!(bool)Call(attack,"BalancedMotionCurrent",owner,window),"a later trajectory window cannot authorize this prepared hand");
            Console.WriteLine("SKY-NEXT payload-clock source=explicit-captured-absence over-real-Source-points sample="+path.SampleTick+" capture="+path.CaptureTick+" now="+Main.GameUpdateCount+" count="+path.Count+" savedOrdinal="+motion.GravityAction+" token/slot/forced/clone/window=REJECTED valid=ACCEPTED");
        }
        static void CloneCosts()
        {
            if(!AppDomain.MonitoringIsEnabled)AppDomain.MonitoringIsEnabled=true;
            const int count=1000;var copy=typeof(object).GetMethod("MemberwiseClone",F);copy.Invoke(owner,null);
            var wall=System.Diagnostics.Stopwatch.StartNew();long before=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;object last=null;
            for(int i=0;i<count;i++)last=copy.Invoke(owner,null);
            long allocated=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-before;GC.KeepAlive(last);
            Console.WriteLine("SKY-NEXT clone-cost calls="+count+" allocated="+allocated+" perCall="+(allocated/(double)count)+" wallMs="+wall.Elapsed.TotalMilliseconds+" production=one-per-affected-CompletedWorld-preparation counter=ApplyPose not-FPS");
            Require(allocated>0 && poseApplications==1,"extra allocation is measured honestly and the real affected preparation remains a single copy");
        }
        static bool risingChecked;
        static void LegalRisingPrefix()
        {
            if(risingChecked)return;
            NativeCombatLiveContextChecks.InitializeMount();int mode=Main.netMode,me=Main.myPlayer;bool dedicated=Main.dedServ;
            var bounce=Main.npc[3];
            try
            {
                Main.netMode=2;Main.myPlayer=1;Main.dedServ=true;owner.mount.SetMount(MountID.Slime,owner);
                bounce.SetDefaults(NPCID.Zombie);bounce.whoAmI=3;bounce.active=true;bounce.position=owner.Bottom-new Vector2(10,3);bounce.width=20;bounce.height=20;bounce.noTileCollide=true;bounce.immune[owner.whoAmI]=0;
                owner.velocity=new Vector2(0,1);owner.JumpMovement();Require(owner.velocity.Y==-10,"original slime JumpMovement supplies the tested upward velocity");owner.mount.Dismount(owner);
                Require(owner.velocity.Y==-10 && !owner.mount.Active,"original dismount preserves that legal ordinary ballistic prefix");risingChecked=true;
                Console.WriteLine("SKY-NEXT upward-premise native=JumpMovement slime-bounce/dismount vy="+owner.velocity.Y+" mountActive="+owner.mount.Active);
            }
            finally{bounce.active=false;owner.mount.Dismount(owner);owner.immune=false;owner.immuneTime=0;Main.netMode=mode;Main.myPlayer=me;Main.dedServ=dedicated;owner.ResetEffects();}
        }
        static int meanSeed=-1;
        static int MeanSeed()
        {
            if(meanSeed>=0)return meanSeed;
            for(int seed=0;seed<300000;seed++){var r=new Terraria.Utilities.UnifiedRandom(seed);if(r.Next(0,401)==200 && r.Next(-40,41)==0){meanSeed=seed;return seed;}}
            throw new InvalidOperationException("No finite representative seed");
        }
        static void Shoot(int seed)
        {
            births.Clear();Main.rand=new Terraria.Utilities.UnifiedRandom(seed);shooting=true;
            try{typeof(Player).GetMethod("ItemCheck_Shoot",F).Invoke(owner,new object[]{owner.whoAmI,owner.HeldItem,owner.GetWeaponDamage(owner.HeldItem),false});}
            finally{shooting=false;}
            foreach(var q in births)Console.WriteLine("SKY-NEXT native birth seed="+seed+" ordinal="+births.IndexOf(q)+" type="+q.type+" key="+q.key+" center="+q.Center+" v="+q.velocity+" ai1="+q.ai[1]+" tile="+q.tileCollide+" raw="+Main.mouseX+","+Main.mouseY+" screen="+Main.screenPosition);
        }
        static void Roof(object context)
        {
            Prepare(context,3065,-10,true);var next=Get(attack,"nextSolution");var point=(Vector2)Get(next,"Point");var motion=(PredictionPlayerMotion)Get(attack,"nextBalancedMotion");float oldGate=owner.Center.Y-200;
            assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPlayerFrame",true).GetMethod("ApplyPose",F).Invoke(null,new object[]{owner,motion});SetTick(Main.GameUpdateCount+1);
            // This oracle uses the original Shoot cursor consumer. No ai[1]
            // value is copied from the model into the native projectile.
            var scope=assembly.GetType("JueMingR.TerrariaHost.Combat.CombatCursorScope",true).GetMethod("Begin",F).Invoke(null,new object[]{point,false});
            try{Shoot(MeanSeed());}finally{Call(scope,"End");}
            Require(births.Count==3 && births[0].type==503,"native Star Wrath retains its three original members");tracked=births[0];
            Require(Math.Abs(tracked.Center.X-(owner.Center.X-200*owner.direction))<.001f && Math.Abs(tracked.Center.Y-(owner.Center.Y-600))<.001f,"seeded native ordinal0 is the exact representative birth from the future Center");
            Require(Math.Abs(tracked.ai[1]-Math.Min(point.Y,owner.Center.Y-200))<1.01f,"native ai1 independently uses future Center and raw integral cursor");
            var modelBirth=new object[]{poseForReplay,3065,suppliedOrigin,point,owner.HeldItem.shootSpeed/owner.meleeSpeed,null,null};sky.GetMethod("Launch",F).Invoke(null,modelBirth);
            var modelOrigin=(Vector2)modelBirth[5];var modelVelocity=(Vector2)modelBirth[6];
            Require(Vector2.DistanceSquared(modelOrigin,tracked.Center)<.001f && Vector2.DistanceSquared(modelVelocity,tracked.velocity)<.001f,"native seeded ordinal0 exactly matches the independent model replay's birth and velocity");
            var path=(NpcTrajectory)Get(next,"Timeline");var sample=ContentSamples.ProjectilesByType[503];var modelTerrain=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(modelTerrain,"Reset");
            var receive=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackReceive",true).GetMethod("Capture",F).Invoke(null,new object[]{owner,sample,2,false,true});
            var modelRoot=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),F,null,new object[]{owner,sample,path,0,false,true},null);
            var flight=new AttackMotion(owner.HeldItem.shootSpeed/owner.meleeSpeed,0,0,sample.extraUpdates+1,sample.width,sample.height,sample.timeLeft,AttackConfidence.Representative);
            modelStep=modelFirstBlocked=0;modelReplay=true;
            try{using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))sky.GetMethod("Replay",F).Invoke(null,new object[]{poseForReplay,owner.HeldItem,point,modelOrigin,modelVelocity,sample,flight,path,0,modelTerrain,receive,modelRoot,0});}
            finally{modelReplay=false;}
            foreach(var q in births.Skip(1))q.active=false;substep=firstEnabled=firstKilled=0;trackNative=true;
            try{for(int i=0;i<60 && tracked.active;i++){NativeQuickItemChecks.BeginWorldStep();tracked.Update(tracked.whoAmI);}}
            finally{trackNative=false;}
            Console.WriteLine("SKY-NEXT roof row=47 top=752 oldGate="+oldGate+" newGate="+tracked.ai[1]+" enabledSub="+firstEnabled+" enabledAt="+enabledAt+" killSub="+firstKilled+" killAt="+killedAt+" active="+tracked.active+" modelFirstBlocked="+modelFirstBlocked+" modelBlockedAt="+modelBlockedAt+" modelReach="+Get(next,"Reachability")+" contact="+(GetOptional(next,"Contact")!=null));
            Require(firstEnabled>0 && firstKilled>0 && !tracked.active,"real native AI enables collision then terminates on the solid roof");
            Require(Get(next,"Reachability").ToString()=="Blocked" && GetOptional(next,"Contact")==null,"future-pose model rejects that same critical solid roof");
            Require(modelFirstBlocked==firstKilled,"model first roof collision agrees with original native subupdate, not merely the final blocked result");
        }
        static void Meteor(object context)
        {
            Prepare(context,2750,-10,false);var motion=(PredictionPlayerMotion)Get(attack,"nextBalancedMotion");float oldGate=owner.position.Y-300;var future=poseForReplay;float testY=(owner.position.Y+motion.Y)*.5f-300;
            var q=new Projectile();q.SetDefaults(337);q.owner=owner.whoAmI;q.position=new Vector2(900,testY);q.velocity=new Vector2(0,10);q.tileCollide=false;
            owner.position=new Vector2(motion.X,motion.Y);owner.velocity=new Vector2(motion.Vx,motion.Vy);
            Require(q.position.Y>=Main.worldSurface*16 && q.position.Y<=oldGate && q.position.Y>owner.position.Y-300,"underground meteor band keeps worldSurface OR false and distinguishes old owner height");q.AI();
            Console.WriteLine("SKY-NEXT meteor posY="+testY+" oldGate="+oldGate+" newGate="+(owner.position.Y-300)+" surface="+(Main.worldSurface*16)+" nativeTile="+q.tileCollide+" modelPose="+future.position);
            Require(q.tileCollide && future.position.Y==owner.position.Y,"native 337 owner gate and model pose use the same future hand");
        }
        static void Dispatch(object context,int item)
        {
            Prepare(context,item,-5,false);
            if(item==3029)Require(skyCalls==0 && arrowCalls>0,"Daedalus preserves its NativeArrowBirth early dispatch");
            if(item==3065)
            {
                var guide=Main.npc[3];guide.SetDefaults(NPCID.Guide);guide.whoAmI=3;guide.active=true;guide.position=new Vector2(1500,1200);owner.killGuide=false;
                // The Guide begins ineligible. Changing only the real owner's
                // qualification must invalidate the captured receiver set.
                Require((bool)Get(root,"Unchanged"),"live-owner receiver lease begins current");owner.killGuide=true;
                Require(!(bool)Get(root,"Unchanged") && !pose.killGuide,"real killGuide changes actual receiver qualification while the private same-whoAmI body remains frozen");owner.killGuide=false;guide.active=false;
            }
        }
    }
}
