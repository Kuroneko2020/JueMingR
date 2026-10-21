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
    // The first native Damage decides whether the hand or its launched body
    // wins. An independently observed real first-use rectangle supplies the
    // close positive; the product does not supply this fixture's geometry.
    internal static class NativeCombatMixedSwingChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static NPC target;private static int projectile,firstType,weapon;private static long first;
        private static bool calibrating,recordBirth;private static int births,birthType;private static Rectangle firstBox;
        private static int handPreviews;
        private static readonly List<string> birthFacts=new List<string>();
        private sealed class StarTrace
        {internal Projectile Shot;internal string Key,Last;internal AttackContact Plan;internal long Born,Hit=-1;internal bool Missing;internal float Gap=float.MaxValue;}
        private static readonly List<StarTrace> stars=new List<StarTrace>();
        private static object starAttack,componentContext;private static bool componentStar;private static AttackContact starTimeline,actualStarPlan;private static Assembly starAssembly;private static Projectile damageShot;
        private static NativeCombatAimChecks.SolutionFacts expectedRepresentative,actualRepresentative;
        private static int representativeInputs;private static AttackContact bornRepresentative;private static long representativeHit=-1;
        private static void PreviewWork(){handPreviews++;}
        private static void Born(int __result)
        {
            if(!recordBirth || __result<0 || __result>=Main.maxProjectiles)return;var shot=Main.projectile[__result];if(shot.owner!=Main.myPlayer || shot.type!=birthType)return;
            births++;birthFacts.Add(shot.whoAmI+":"+shot.Center+"/"+shot.velocity+" ai="+string.Join(",",shot.ai)+" life="+shot.timeLeft);
            if(shot.type==1012)
            {
                // BeginShot retires the public solution before NewProjectile.
                // Seal it at the real Shoot prefix; the birth must carry that
                // invocation's receipt, not a previous-world preparation.
                var owner=Main.LocalPlayer;Require(representativeInputs==1,"one actual Waffle hand reaches this native birth");NativeCombatAimChecks.RequireConsumedInput(starAttack,owner,actualRepresentative,"Waffle actual birth consumes this hand's current Enhanced point");
                var assistedCursor=Main.MouseWorld;Require(Math.Abs(assistedCursor.X-actualRepresentative.Point.X)<1.01f && Math.Abs(assistedCursor.Y-actualRepresentative.Point.Y)<1.01f,"Waffle native birth reads the assisted cursor with original integer pixel encoding");
                var args=new object[]{Get(starAttack,"ammo"),default(AttackMotion)};Require((bool)starAssembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels",true).GetMethod("TryRead",Flags).Invoke(null,args),"born1012 retains its audited AI002 scalar motor");
                bornRepresentative=AttackIntercept.Replay(actualRepresentative.Point.X,actualRepresentative.Point.Y,shot.Center.X,shot.Center.Y,shot.velocity.X,shot.velocity.Y,(AttackMotion)args[1],actualRepresentative.Timeline,actualRepresentative.FirstTick-1);
                return;
            }
            if(shot.type!=503 || starTimeline==null)return;
            var p=Main.LocalPlayer;var cursor=Main.screenPosition+new Vector2(Main.mouseX,Main.mouseY);var receipt=Get(starAttack,"consumedInput");var planned=(Vector2)Get(receipt,"Point");
            Require((bool)Get(starAttack,"hasConsumedInput") && Convert.ToString(Get(receipt,"Kind"))=="Enhanced","Star Wrath actual birth carries a real enhanced input receipt");
            Require(p.HeldItem.type==3065 && Math.Abs(cursor.X-planned.X)<1.01f && Math.Abs(cursor.Y-planned.Y)<1.01f,"actual Star Wrath consumer receives its prepared assisted cursor, retaining native integer pixel encoding");
            Require(Math.Abs(shot.ai[1]-Math.Min(cursor.Y,p.Center.Y-200))<.01f,"actual Star Wrath gate comes from the assisted cursor and original player center");
            Console.WriteLine("MIXED STAR consumer cursor="+cursor+" plan="+planned+" owner="+p.whoAmI+" facing="+p.direction+" player="+p.Center);
            var trace=new StarTrace{Shot=shot,Key=shot.key.ToString(),Born=Main.GameUpdateCount};trace.Plan=StarReplay(shot,starTimeline.Timeline,(int)(trace.Born-starTimeline.Timeline.SampleTick)-1,true,ref trace.Missing);stars.Add(trace);
        }
        private static void PrepareActualStar(Player __instance)
        {
            if(recordBirth && __instance.whoAmI==Main.myPlayer && __instance.HeldItem.type==5382)
            {
                // Observe existing natural preparation only. This branch neither
                // prepares again nor fabricates a Contact from a representative point.
                Require(!(bool)Get(starAttack,"hasConsumedInput"),"Waffle actual Shoot starts before any native input consumption");actualRepresentative=NativeCombatAimChecks.RequireSolution(starAttack,__instance,"Waffle actual Shoot owns a reachable current representative point");representativeInputs++;
                Require(actualRepresentative.Timeline.Identity.Equals(expectedRepresentative.Timeline.Identity) && Vector2.DistanceSquared(actualRepresentative.Point,expectedRepresentative.Point)<.0001f,"the actual Waffle consumer retains the component's selected identity and representative point");return;
            }
            if(!recordBirth || !componentStar || __instance.whoAmI!=Main.myPlayer || __instance.HeldItem.type!=3065)return;
            // This is an explicit native Shoot component. The previous-world
            // preparation can be replaced by the real Player entry's ordinary
            // cold allowance; prepare at the actual consumer phase instead.
            // Main cold/continuous opportunity is measured separately by A.
            NativeCombatAimChecks.PrepareMechanismFixture(Get(componentContext,"CombatObservation"),starAttack,target,0,phaseName:"BeforeNpc");
            actualStarPlan=(AttackContact)GetOptional(starAttack,"ExpectedImpact");Require(actualStarPlan!=null,"actual Star Wrath source has a complete component preparation");starTimeline=actualStarPlan;
        }
        private static void BeforeDamage(Projectile __instance){projectile=__instance.type;damageShot=__instance;}
        private static void AfterDamage(){projectile=0;damageShot=null;}
        private static void Strike(NPC __instance,int __result)
        {if(ReferenceEquals(target,__instance) && __result>0){if(first<0){first=Main.GameUpdateCount;firstType=projectile;}if(projectile==1012 && representativeHit<0)representativeHit=Main.GameUpdateCount;foreach(var trace in stars)if(ReferenceEquals(trace.Shot,damageShot) && trace.Key==damageShot.key.ToString() && trace.Hit<0)trace.Hit=Main.GameUpdateCount;}}
        private static void Shape(Player __instance,Item __0,Rectangle __1)
        {if(calibrating && __instance.whoAmI==Main.myPlayer && __0.type==weapon && firstBox==Rectangle.Empty)firstBox=__1;}
        internal static void Run(object context)
        {
            using(var trace=new NativeCombatWeaponPreparationTrace(context,3065))RunCore(context);
        }
        private static void RunCore(object context)
        {
            var audit=new Harmony("JueMingR.Tests.MixedHandShot");
            // This leaf also runs directly, without FiniteShot's parent
            // setup. Achievement UI is outside the isolated native receiver.
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatMixedSwingChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatMixedSwingChecks),nameof(AfterDamage)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatMixedSwingChecks),nameof(Strike)));
            audit.Patch(typeof(Player).GetMethod("ItemCheck_MeleeHitNPCs",Flags),prefix:new HarmonyMethod(typeof(NativeCombatMixedSwingChecks),nameof(Shape)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatMixedSwingChecks),nameof(Born)));
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");int cases=0,handHits=0,shotStages=0,randomMisses=0;
            var swing=attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostSwingAttack",true);
            starAssembly=attack.GetType().Assembly;starAttack=attack;componentContext=context;
            audit.Patch(typeof(Player).GetMethod("ItemCheck_Shoot",Flags),prefix:new HarmonyMethod(typeof(NativeCombatMixedSwingChecks),nameof(PrepareActualStar)){priority=Priority.First});
            audit.Patch(swing.GetMethod("PreviewPrepared",Flags),prefix:new HarmonyMethod(typeof(NativeCombatMixedSwingChecks),nameof(PreviewWork)));
            string only=Environment.GetEnvironmentVariable("JMR_MIXED_SWING_CASE");
            try
            {
                if(Environment.GetEnvironmentVariable("JMR_MIXED_SWING_STAR_ORACLE")=="birth"){StarLegalBirths(context,attack,host);return;}
                foreach(int item in new[]{723,989,65,3063,3065,5382,1928,3827})
                {
                    if(!string.IsNullOrEmpty(only) && !only.StartsWith(item+"/",StringComparison.Ordinal))continue;
                    weapon=item;var p=Reset(context,item);target=NativeCombatControlPreviewChecks.Dummy(2,52,40);
                    for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,target.Center,false,true);
                    firstBox=Rectangle.Empty;calibrating=true;NativeCombatSelectionPreviewChecks.Step(context,target.Center,true);calibrating=false;
                    Require(firstBox.Width>0 && first<0,"real mixed first-use shape is observed away from the receiver");
                    float closeX=p.position.X+target.Hitbox.Left-firstBox.Right+1;
                    foreach(bool near in new[]{false,true})
                    {
                        if(!string.IsNullOrEmpty(only) && !string.Equals(only,item+"/"+near,StringComparison.OrdinalIgnoreCase))continue;
                        p=Reset(context,item);if(near)p.position.X=closeX;target=NativeCombatControlPreviewChecks.Dummy(2,52,40);
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,true,25,true,false));AttackContact preview=null;
                        for(int i=0;i<3;i++)
                        {NativeCombatSelectionPreviewChecks.Step(context,target.Center,false,true);Require(GetOptional(attack,"ExpectedPreview")==null,"withdrawn idle mixed impact remains absent");Require(!Main.projectile.Any(q=>q.active) && p.itemAnimation==0,"idle mixed preview neither swings nor fires");}
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,true,25,true,true));
                        NativeToolExecutionChecks.Sample(context,Get(context,"Input"),target.Center,true);Call(combat,"Sample");
                        NativeCombatAimChecks.PrepareMechanismFixture(host,attack,target,0);
                        bool representative=item==5382;var shotSolution=representative?NativeCombatAimChecks.RequireSolution(attack,p,"Waffle public preparation supplies its real representative projectile point"):default(NativeCombatAimChecks.SolutionFacts);var shotPlan=representative?shotSolution.Contact:(AttackContact)GetOptional(attack,"ExpectedImpact");
                        if(!representative && shotPlan==null)Console.WriteLine("MIXED missing actual component item="+item+" near="+near+" guc="+Main.GameUpdateCount+" current="+((AttackContact)GetOptional(attack,"current"))?.Tick+" next="+((AttackContact)GetOptional(attack,"next"))?.Tick+" failed="+Get(attack,"Failed")+" permission="+Get(attack,"Permission")+" animation/time="+p.itemAnimation+"/"+p.itemTime+" velocity="+p.velocity+" receiver="+target.Hitbox+" immune="+target.immune[p.whoAmI]);
                        bool randomStar=!near && item==3065;
                        Require(representative || shotPlan!=null || randomStar,"the real mixed hand keeps its native shot preparation: "+item+"/"+near);
                        var componentPath=(representative?shotSolution.Timeline:shotPlan?.Timeline)??(NpcTrajectory)Call(Get(Get(host,"Prediction"),"Cache"),"Read",1);Require(componentPath!=null,"mixed source component owns its full immutable receiver path");
                        // The product's current input belongs to the projectile.
                        // Compare the adjacent native sword's finite geometry
                        // separately, using its observed Animate frame. This
                        // component calculation neither publishes a retired
                        // impact preview nor grants a cursor receipt.
                        var handTerrain=Activator.CreateInstance(starAssembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(handTerrain,"Reset");
                        var frameType=starAssembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPlayerFrame",true);
                        var frame=frameType.GetMethod("Capture",Flags).Invoke(null,new object[]{p,componentPath});
                        var clock=NativeCombatAimChecks.PhaseClock(attack,componentPath,"CompletedWorld");AttackContact hand;
                        using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))hand=(AttackContact)swing.GetMethod("PreviewPrepared",Flags).Invoke(Get(attack,"swing"),new object[]{p,p.HeldItem,componentPath,clock,handTerrain,frame,120});
                        preview=hand!=null && (shotPlan==null || hand.Tick<shotPlan.Tick)?hand:shotPlan;
                        // A public representative point is not a full family Contact.
                        // This deterministic single-body fixture still compares its
                        // phase clock against the independently modeled native hand.
                        int expectedTick=representative?(hand!=null?Math.Min(hand.Tick,shotSolution.NativeTick):shotSolution.NativeTick):preview?.Tick??-1;
                        int prepared=handPreviews;var marker=Get(Get(host,"World"),"Impact");var zoom=Main.GameViewMatrix.ZoomMatrix;
                        for(int draw=0;draw<20;draw++){Call(marker,"Capture");Call(marker,"Project",zoom,Matrix.Invert(zoom));Require(GetOptional(attack,"ExpectedPreview")==null,"mixed presentation cannot revive the withdrawn idle impact");}
                        Require(handPreviews==prepared,"twenty same-update mixed presentations do not solve a hand");
                        long start=Main.GameUpdateCount;int shotType=p.HeldItem.shoot;birthType=shotType;births=0;birthFacts.Clear();stars.Clear();starTimeline=null;actualStarPlan=null;componentStar=randomStar;representativeInputs=0;bornRepresentative=null;representativeHit=-1;expectedRepresentative=shotSolution;actualRepresentative=default(NativeCombatAimChecks.SolutionFacts);recordBirth=true;
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,true,25,true,true));
                        int actionLimit=randomStar?Math.Max(90,componentPath.Count):90;
                        for(int i=1;i<=actionLimit && first<0;i++)
                        {NativeCombatSelectionPreviewChecks.Step(context,target.Center,i==1);StarPositions();}
                        recordBirth=false;componentStar=false;
                        if(randomStar){Require(actualStarPlan!=null,"Star Wrath cannot pass on an absent actual-source plan");preview=actualStarPlan;expectedTick=preview.Tick;}
                        if(representative)
                        {
                            Require(representativeInputs==1 && births==1,"Waffle has one real hand consumption and one original1012 birth");
                            if(!near)Require(bornRepresentative!=null && representativeHit==bornRepresentative.Timeline.SampleTick+bornRepresentative.Tick,"distant real1012 first Damage exactly matches its sealed born scalar replay");
                        }
                        if(starTimeline!=null){Console.WriteLine("MIXED STAR actual births="+string.Join(";",birthFacts));StarResults(first<0);Require(stars.Count==3,"native Star Wrath consumer emits all three observed legal births");starTimeline=null;}
                        if(first<0)Console.WriteLine("MIXED first missing item="+item+" near="+near+" target="+target.Hitbox+" life="+target.life+" ownerImmune="+target.immune[p.whoAmI]+" births="+string.Join(";",birthFacts)+" remaining="+string.Join(";",Main.projectile.Where(q=>q.active && q.owner==p.whoAmI && q.type==shotType).Select(q=>q.whoAmI+":"+q.Center+"/"+q.velocity+" ai="+string.Join(",",q.ai)+" pen="+q.penetrate+" tile="+q.tileCollide)));
                        // Only the two future sky births have random source
                        // positions here. Existing finite shots, native hands,
                        // Meowmere and Waffle retain exact first-hit clocks.
                        bool randomSky=!near && (item==65 || item==3065);
                        // The native -400 birth endpoint with legal +0.8 Y
                        // jitter misses this same cursor, whereas zero jitter
                        // hits. A Representative plan cannot promise every
                        // random use; its actual births above must still agree
                        // with their own deterministic replay over the horizon.
                        Require(randomStar?preview.Confidence==AttackConfidence.Representative:first>=0,"deterministic mixed stages require useful native Damage; Star Wrath retains its explicit representative source boundary");
                        Require(first<0 && randomStar || first>=0 && (randomSky && preview.Confidence==AttackConfidence.Representative || first-start==expectedTick),"mixed earliest hand/shot clock matches actual native first Damage: "+item+"/"+near+" first="+(first-start)+" type="+firstType+" plan="+expectedTick+" route="+(representative?"representative-point":preview.Confidence.ToString())+" player="+p.position+" nativeFirstBox="+firstBox);
                        Require(first<0 && randomStar || (near?firstType==0:firstType==shotType),"real first receiver distinguishes close hand and distant projectile: "+item+"/"+near+" type="+firstType);
                        // A near one-penetration shot can be born and killed
                        // by native Damage before the outer update returns.
                        // Observe its actual birth instead of requiring it to
                        // remain active after that valid receive.
                        Require(births>0,"native mixed use preserves its independently launched projectile");
                        if(near)handHits++;else shotStages++;if(first<0)randomMisses++;cases++;
                        Console.WriteLine("MIXED HAND item="+item+" near="+near+" first="+(first<0?-1:first-start)+" damageType="+firstType+" predicted="+expectedTick+" actualBirths="+births);
                    }
                }
                Require(string.IsNullOrEmpty(only)?cases==16 && handHits==8 && shotStages==8:cases==1,"mixed stage oracle executes every requested real hand/shot stage");
                Console.WriteLine("PASS native mixed sword actual-use component: deterministic first Damage and observed representative Star Wrath outcomes, withdrawn idle impact remains absent; randomMisses="+randomMisses);
            }
            finally{calibrating=recordBirth=componentStar=false;componentContext=null;actualStarPlan=bornRepresentative=null;representativeInputs=0;representativeHit=-1;expectedRepresentative=actualRepresentative=default(NativeCombatAimChecks.SolutionFacts);target=null;projectile=0;damageShot=null;starTimeline=null;starAttack=null;stars.Clear();audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
        private static void StarPositions()
        {
            foreach(var trace in stars)
            {
                var q=trace.Shot;if(q.key.ToString()!=trace.Key)continue;
                if(q.Hitbox.Bottom>=target.Hitbox.Top && q.Hitbox.Top<=target.Hitbox.Bottom)trace.Gap=Math.Min(trace.Gap,Math.Max(0,Math.Max(target.Hitbox.Left-q.Hitbox.Right,q.Hitbox.Left-target.Hitbox.Right)));
                string fact="tick="+(Main.GameUpdateCount-trace.Born)+" active="+q.active+" center="+q.Center+" V="+q.velocity+" tile="+q.tileCollide+" alpha="+q.alpha+" left="+q.timeLeft+" local="+q.localNPCImmunity[target.whoAmI];
                if(trace.Last==null || q.active && q.Hitbox.Bottom>=target.Hitbox.Top && q.Hitbox.Top<=target.Hitbox.Bottom || !q.active && !trace.Last.Contains("active=False"))Console.WriteLine("MIXED STAR slot="+q.whoAmI+" "+fact);
                trace.Last=fact;
            }
        }
        private static void StarResults(bool complete)
        {
            foreach(var trace in stars)
            {
                long plan=trace.Plan==null?-1:trace.Plan.Timeline.SampleTick+trace.Plan.Tick;
                Console.WriteLine("MIXED STAR replay slot="+trace.Shot.whoAmI+" native="+(trace.Hit<0?-1:trace.Hit-trace.Born)+" known="+(plan<0?-1:plan-trace.Born)+" gap="+trace.Gap+" missing="+trace.Missing+" last="+trace.Last);
                Require(!trace.Missing,"observed Star Wrath finite birth replay retains required terrain");
                // On an early first hit other members may still be on their
                // way. Only a complete action may prove their negative.
                if(complete || trace.Hit>=0 || trace.Plan==null)Require(trace.Hit==plan,"each actual Star Wrath birth, including world retirement, agrees with its own deterministic first Damage");
            }
        }
        private static AttackContact StarReplay(Projectile shot,JueMingR.Platform.Combat.NpcTrajectory timeline,int age,bool beforeNpc,ref bool missing)
        {
            var p=Main.LocalPlayer;var copy=new Projectile();copy.SetDefaults(503);copy.owner=shot.owner;copy.Center=shot.Center;copy.velocity=shot.velocity;copy.damage=shot.damage;copy.ai[1]=shot.ai[1];
            var terrain=Activator.CreateInstance(starAssembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");
            var tile=terrain.GetType().GetMethod("ProjectileTileMotion",Flags);var receiver=starAssembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackReceive",true).GetMethod("Capture",Flags).Invoke(null,new object[]{p,shot,timeline.Identity.Slot,beforeNpc,!beforeNpc});
            bool unavailable=false,collides=shot.tileCollide;
            AttackPassage passage=(x,y,nx,ny,w,h)=>
            {
                collides|=y>shot.ai[1];if(!collides)return true;copy.position=new Vector2(x-w*.5f,y-h*.5f);copy.velocity=new Vector2(nx-x,ny-y);
                var args=new object[]{copy,copy.position,copy.velocity,false,false,Vector2.Zero,false,true,false,true};bool available=(bool)tile.Invoke(terrain,args);if(!available)unavailable=true;return available && !(bool)args[6];
            };
            var motion=new AttackMotion(shot.velocity.Length(),0,0,shot.extraUpdates+1,shot.width,shot.height,shot.timeLeft);
            var result=AttackIntercept.Replay(shot.Center.X+shot.velocity.X*1000,shot.Center.Y+shot.velocity.Y*1000,shot.Center.X,shot.Center.Y,shot.velocity.X,shot.velocity.Y,motion,timeline,age,passage,t=>(bool)Call(receiver,"Allows",t),null,(k,t,x,y)=>x-shot.width*.5f>Main.leftWorld && x+shot.width*.5f<Main.rightWorld && y-shot.height*.5f>Main.topWorld && y+shot.height*.5f<Main.bottomWorld);
            missing=unavailable;return result;
        }
        private static void StarLegalBirths(object context,object attack,object host)
        {
            foreach(float jitter in new[]{0f,.8f})
            {
                var p=Reset(context,3065);target=NativeCombatControlPreviewChecks.Dummy(2,52,40);NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,true,25,true,false));
                for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,target.Center,false,true);
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,true,25,true,true));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),target.Center,true);Call(Get(context,"Combat"),"Sample");NativeCombatAimChecks.PrepareMechanismFixture(host,attack,target,0);var preview=(AttackContact)GetOptional(attack,"ExpectedImpact");Require(preview!=null && preview.Confidence==AttackConfidence.Representative,"the same cursor has a legal representative sky contact before any birth");
                NativeCombatObservationChecks.Save(host,new ObservationOptions());stars.Clear();birthFacts.Clear();births=0;birthType=503;first=-1;starTimeline=null;
                long born=Main.GameUpdateCount;
                // This is an independent native motor oracle for legal
                // Player48184 birth values, not a forged ItemCheck receipt or
                // reconstruction of R32's unavailable random state. All three
                // heights/slots/gates are retained; no RNG method is patched.
                for(int i=0;i<3;i++)
                {
                    // Next(0,401) permits this endpoint. With +0.8 Y the
                    // last full substep before the reset floor has a 21px
                    // horizontal gap; -200 still overlaps and is not a negative.
                    var origin=p.Center+new Vector2(-400*p.direction,-600-100*i);var aim=new Vector2(preview.AimX,preview.AimY);var v=aim-origin;v.Y=Math.Max(20,Math.Abs(v.Y));v.Normalize();v*=p.HeldItem.shootSpeed;v.Y+=jitter;
                    int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),origin,v,503,p.GetWeaponDamage(p.HeldItem),0,p.whoAmI,0,Math.Min(aim.Y,p.Center.Y-200));var q=Main.projectile[slot];
                    var trace=new StarTrace{Shot=q,Key=q.key.ToString(),Born=born};trace.Plan=StarReplay(q,preview.Timeline,(int)(born-preview.Timeline.SampleTick),false,ref trace.Missing);stars.Add(trace);
                    if(i==0)
                    {
                        float gap;bool overlaps=StarLegalGeometry(q,43*16,out gap);
                        Require(jitter==0?overlaps && trace.Plan!=null:!overlaps && gap>=20 && trace.Plan==null,"legal birth geometry and pure replay establish a positive or strict pre-floor negative before native actions");
                        Console.WriteLine("MIXED STAR legal geometry jitter="+jitter+" overlap="+overlaps+" gap="+gap+" origin="+origin+" V="+v+" target="+target.Hitbox);
                    }
                }
                for(int i=0;i<90;i++){NativeCombatSelectionPreviewChecks.Step(context,target.Center,false,true);StarPositions();}
                StarResults(true);Require(stars.Count==3 && stars.Skip(1).All(t=>!t.Shot.active && t.Hit<0 && t.Plan==null),"two upper legal births retire at the actual world boundary before AI/Damage");
                Require(jitter==0?stars[0].Hit>=born:stars.All(t=>t.Hit<0),"same representative cursor has native zero-jitter positive and legal +0.8-jitter negative births");
                Console.WriteLine("PASS MIXED STAR legalBirth jitter="+jitter+" first="+(stars[0].Hit<0?-1:stars[0].Hit-born)+" representative="+preview.Tick+" cursor="+preview.AimX+","+preview.AimY);
            }
        }
        private static bool StarLegalGeometry(Projectile shot,float floorTop,out float gap)
        {
            Vector2 position=shot.position;gap=float.MaxValue;bool overlaps=false;
            // Dry AI005 keeps this velocity until tile collision. The reset
            // floor kills before Damage in the colliding substep; only prior
            // full movements can receive. This is a bounded geometry premise,
            // independently checked by Replay and the complete native action.
            for(int step=0;step<180;step++)
            {
                position+=shot.velocity;if(position.Y+shot.height>=floorTop)break;
                var box=new Rectangle((int)position.X,(int)position.Y,shot.width,shot.height);var npc=target.Hitbox;
                float dx=Math.Max(0,Math.Max(npc.Left-box.Right,box.Left-npc.Right)),dy=Math.Max(0,Math.Max(npc.Top-box.Bottom,box.Top-npc.Bottom));
                gap=Math.Min(gap,(float)Math.Sqrt(dx*dx+dy*dy));overlaps|=box.Intersects(npc);
            }
            return overlaps;
        }
        private static Player Reset(object context,int item)
        {
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),item,0,0);p.position=new Vector2(650,646);p.ResetEffects();p.ResetFallDamage();p.ResetMeleeHitCooldowns();p.attackCD=0;p.releaseUseItem=true;
            Main.windPhysics=false;Main.screenPosition=new Vector2(500,400);first=-1;firstType=-1;return p;
        }
    }
}
