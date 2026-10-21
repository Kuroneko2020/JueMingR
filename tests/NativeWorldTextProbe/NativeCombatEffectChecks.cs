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
    internal static class NativeCombatEffectChecks
    {
        private struct BirthCause {internal string Key;internal int Type;}
        private static readonly Dictionary<string,BirthCause> parents=new Dictionary<string,BirthCause>();
        private static int dummyHits;
        internal static void Run(object context)
        {
            var audit=new Harmony("JueMingR.Tests.AttackEffects");var birth=typeof(Projectile).GetMethods(BindingFlags.Public|BindingFlags.Static).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float));var strike=AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)});
            audit.Patch(birth,prefix:new HarmonyMethod(typeof(NativeCombatEffectChecks),nameof(BeforeBirth)),postfix:new HarmonyMethod(typeof(NativeCombatEffectChecks),nameof(AfterBirth)));audit.Patch(strike,postfix:new HarmonyMethod(typeof(NativeCombatEffectChecks),nameof(Strike)));
            try{RunCore(context);}finally{audit.Unpatch(birth,HarmonyPatchType.All,audit.Id);audit.Unpatch(strike,HarmonyPatchType.All,audit.Id);parents.Clear();}
        }
        private static void BeforeBirth(IEntitySource __0,out BirthCause __state){__state=default(BirthCause);var parent=(__0 as EntitySource_Parent)?.Entity as Projectile;if(parent!=null)__state=new BirthCause{Key=parent.key.ToString(),Type=parent.type};}
        private static void AfterBirth(int __result,BirthCause __state){if(__state.Key!=null && __result>=0 && __result<Main.maxProjectiles)parents[Main.projectile[__result].key.ToString()]=__state;}
        private static void Strike(NPC __instance,int __result){if(__instance.type==488 && __result>0)dummyHits++;}
        private static void RunCore(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");
            string finiteOracle=Environment.GetEnvironmentVariable("JMR_WEAPON_EFFECT_ORACLE_ONLY");
            if(finiteOracle=="meteor-homing-boundaries")
            {Console.WriteLine("LOCAL FEEDBACK ONLY: Meteor open/closed and autonomous homing boundaries; complete Effects not evaluated.");Bounce(context,combat,host,attack,input);Bounce(context,combat,host,attack,input,true);HomingCases(context,combat,host,attack,input);return;}
            if(finiteOracle=="cluster"){NativeCombatClusterChecks.Run(context);return;}
            if(finiteOracle=="cold-bolt"){NativeCombatColdBoltChecks.Run(context);return;}
            if(finiteOracle=="scythe"){NativeCombatScytheChecks.Run(context);return;}
            if(finiteOracle=="finite-shot"){NativeCombatFiniteShotChecks.Run(context);return;}
            if(finiteOracle=="mixed-swing"){NativeCombatMixedSwingChecks.Run(context);return;}
            if(finiteOracle=="gem-staff"){NativeCombatGemStaffChecks.Run(context);return;}
            if(finiteOracle=="poison-cloud"){NativeCombatPoisonCloudChecks.Run(context);return;}
            if(finiteOracle=="flame"){NativeCombatFlameChecks.Run(context);return;}
            if(finiteOracle=="bee"){NativeCombatBeeChecks.Run(context);return;}
            if(finiteOracle=="bee-gun"){NativeCombatBeeGunChecks.Run(context);return;}
            if(finiteOracle=="phoenix"){NativeCombatPhoenixChecks.Run(context);return;}
            if(finiteOracle=="thorn"){NativeCombatThornChecks.Run(context);return;}
            if(finiteOracle=="stream"){NativeCombatStreamChecks.Run(context);return;}
            if(finiteOracle=="bottle"){NativeCombatBottleChecks.Run(context);return;}
            if(finiteOracle=="note"){NativeCombatNoteChecks.Run(context);return;}
            if(finiteOracle=="nebula-blaze"){NativeCombatNebulaBlazeChecks.Run(context);return;}
            if(finiteOracle=="native-arrow-birth"){NativeCombatNativeArrowBirthChecks.Run(context);return;}
            if(finiteOracle=="sky-fracture"){NativeCombatSkyFractureChecks.Run(context);return;}
            if(finiteOracle=="ice-rod"){NativeCombatIceRodChecks.Run(context);return;}
            if(finiteOracle=="ai29-spells"){NativeCombatAi29SpellChecks.Run(context);return;}
            if(finiteOracle=="flare"){NativeCombatFlareChecks.Run(context);return;}
            if(finiteOracle=="blizzard"){NativeCombatBlizzardChecks.Run(context);return;}
            if(finiteOracle=="ray-spells"){NativeCombatRaySpellChecks.Run(context);return;}
            if(finiteOracle=="sonic-boom"){NativeCombatSonicBoomChecks.Run(context);return;}
            if(finiteOracle=="book-staff"){NativeCombatBookStaffChecks.Run(context);return;}
            if(finiteOracle=="skull"){NativeCombatSkullChecks.Run(context);return;}
            if(finiteOracle=="ornament"){NativeCombatOrnamentChecks.Run(context);return;}
            if(finiteOracle=="inferno"){NativeCombatInfernoChecks.Run(context);return;}
            if(finiteOracle=="paper-airplane"){NativeCombatPaperAirplaneChecks.Run(context);return;}
            if(finiteOracle=="arc-swing"){NativeCombatArcSwingChecks.Run(context);return;}
            if(finiteOracle=="attached-shot"){NativeCombatAttachedShotChecks.Run(context);return;}
            if(finiteOracle=="ballistic-candidates"){NativeCombatBallisticCandidateChecks.Run(context);return;}
            if(Environment.GetEnvironmentVariable("JMR_WEAPON_EFFECT_ORACLE_ONLY")=="rapier"){NativeCombatBallisticRapierChecks.Run(context);return;}
            if(finiteOracle=="homing-effect"){NativeCombatHomingEffectChecks.Run(context);return;}
            if(finiteOracle=="placed-pulse"){NativeCombatPlacedPulseChecks.Run(context);return;}
            if(finiteOracle=="terrain-line"){NativeCombatTerrainLineChecks.Run(context);return;}
            if(finiteOracle=="fixed-sky"){NativeCombatFixedSkyChecks.Run(context);return;}
            if(finiteOracle=="sky-input"){NativeCombatSkyChecks.Run(context);return;}
            if(finiteOracle=="seedler"){NativeCombatSeedlerChecks.Run(context);return;}
            if(finiteOracle=="rainbow"){NativeCombatRainbowChecks.Run(context);return;}
            if(finiteOracle=="arrow-family"){NativeCombatArrowFamilyChecks.Run(context);return;}
            if(finiteOracle=="impact-burst"){NativeCombatImpactBurstChecks.Run(context);return;}
            if(finiteOracle=="clinger-wall"){NativeCombatClingerWallChecks.Run(context);return;}
            if(finiteOracle=="jousting-lance"){NativeCombatJoustingLanceChecks.Run(context);return;}
            if(finiteOracle=="whip-star"){NativeCombatWhipStarChecks.Run(context);return;}
            if(finiteOracle=="harpy-arrow-family"){NativeCombatHarpyArrowFamilyChecks.Run(context);return;}
            if(finiteOracle=="bubble-flail"){NativeCombatBubbleFlailFamilyChecks.Run(context);return;}
            if(finiteOracle=="finite-tail"){FiniteTail(context);return;}
            if(finiteOracle=="held-then-steering"){NativeCombatHeldAmmoFamilyChecks.Run(context);NativeCombatAmmoSteeringChecks.Run(context);return;}
            if(finiteOracle=="piranha"){NativeCombatPiranhaChecks.Run(context);return;}
            if(finiteOracle=="lights-bane"){NativeCombatLightsBaneChecks.Run(context);return;}
            if(finiteOracle=="sharp-tears"){NativeCombatSharpTearsChecks.Run(context);return;}
            if(finiteOracle=="north-pole"){NativeCombatNorthPoleChecks.Run(context);return;}
            if(finiteOracle=="onyx-blast"){NativeCombatOnyxBlastChecks.Run(context);return;}
            if(finiteOracle=="held-ammo-family"){NativeCombatHeldAmmoFamilyChecks.Run(context);return;}
            if(finiteOracle=="ammo-steering"){NativeCombatAmmoSteeringChecks.Run(context);return;}
            if(finiteOracle=="bee-hand"){NativeCombatBeeHandChecks.Run(context);return;}
            if(finiteOracle=="melee-hit-child"){NativeCombatMeleeHitChildChecks.Run(context);return;}
            if(finiteOracle=="melee-hit-family"){NativeCombatMeleeHitFamilyChecks.Run(context);return;}
            if(finiteOracle=="ammo-impact"){NativeCombatAmmoImpactChecks.Run(context);return;}
            if(finiteOracle=="arc-owner-pose"){NativeCombatArcOwnerPoseChecks.Run(context);return;}
            if(finiteOracle=="grass-curve"){NativeCombatGrassCurveChecks.Run(context);return;}
            if(finiteOracle=="monk-staff"){NativeCombatMonkStaffChecks.Run(context);return;}
            if(finiteOracle=="super-star"){NativeCombatSuperStarChecks.Run(context);return;}
            if(finiteOracle=="nebula-arcanum"){NativeCombatNebulaArcanumChecks.Run(context);return;}
            if(finiteOracle=="medusa-scan"){NativeCombatMedusaScanChecks.Run(context);return;}
            if(finiteOracle=="solar-eruption"){NativeCombatSolarEruptionChecks.Run(context);return;}
            if(finiteOracle=="influx"){NativeCombatInfluxChecks.Run(context);return;}
            if(finiteOracle=="nail"){NativeCombatNailChecks.Run(context);return;}
            if(finiteOracle=="spear-shards"){NativeCombatSpearShardChecks.Run(context);return;}
            if(finiteOracle=="guitar-curve"){NativeCombatGuitarCurveChecks.Run(context);return;}
            if(finiteOracle=="yoyo-family"){NativeCombatYoyoFamilyChecks.Run(context);return;}
            if(finiteOracle=="weather-pain"){NativeCombatWeatherPainChecks.Run(context);return;}
            if(finiteOracle=="phantasm-echo"){NativeCombatPhantasmEchoChecks.Run(context);return;}
            if(finiteOracle=="magnet-sphere"){NativeCombatMagnetSphereChecks.Run(context);return;}
            if(finiteOracle=="spirit-flame"){NativeCombatBallisticSpiritChecks.Run(context);return;}
            if(finiteOracle=="typhoon"){NativeCombatBallisticTyphoonChecks.Run(context);return;}
            if(finiteOracle=="cloud"){NativeCombatBallisticCloudChecks.Run(context);return;}
            if(finiteOracle=="ballistic-seeds"){NativeCombatBallisticSeedChecks.Run(context);return;}
            if(finiteOracle=="free-flight"){NativeCombatBallisticFreeFlightChecks.Run(context);return;}
            if(finiteOracle=="betsy-flame"){NativeCombatBetsyFlameChecks.Run(context);return;}
            if(finiteOracle=="electrosphere"){NativeCombatElectrosphereChecks.Run(context);return;}
            if(finiteOracle=="barrel"){NativeCombatBarrelChecks.Run(context);return;}
            if(finiteOracle=="javelin"){NativeCombatJavelinChecks.Run(context);return;}
            if(finiteOracle=="laser-paint"){NativeCombatLaserPaintChecks.Run(context);return;}
            if(finiteOracle=="dart"){NativeCombatDartChecks.Run(context);return;}
            if(finiteOracle=="line"){NativeCombatLineChecks.Run(context);return;}
            if(finiteOracle=="wasp-bat"){NativeCombatWaspBatChecks.Run(context);return;}
            if(finiteOracle=="reflection"){NativeCombatReflectionChecks.Run(context);return;}
            if(finiteOracle=="scourge"){NativeCombatScourgeChecks.Run(context);return;}
            if(finiteOracle=="phaseblade"){NativeCombatPhasebladeChecks.Run(context);return;}
            string oracle=Environment.GetEnvironmentVariable("JMR_WEAPON_EFFECT_ORACLE_ONLY");if(oracle=="ai008"){MagicBounceMotor(context,combat);MagicBurst(context,combat);return;}if(oracle=="crystal"){CrystalBirths(context,combat,host,attack,input);CrystalMotor(context,combat,input);return;}if(oracle=="harpy"){NativeCombatHarpyChecks.Run(context);return;}if(oracle=="finite-throw"){NativeCombatFiniteThrowChecks.Run(context);return;}if(oracle=="special-arrow"){NativeCombatSpecialArrowChecks.Run(context);return;}if(oracle=="explosive"){NativeCombatExplosiveChecks.Run(context);return;}if(oracle=="harpoon"){NativeCombatHarpoonChecks.Run(context);return;}
            foreach(var row in new[]{new[]{39,516,91,92},new[]{95,515,89,90},new[]{39,3568,639,640},new[]{95,1179,207,0},new[]{127,0,20,0},new[]{518,0,94,0},new[]{197,75,955,0},new[]{1446,0,297,0},new[]{112,0,15,0},new[]{165,0,27,0},new[]{519,0,95,0},new[]{1264,0,253,0},new[]{3063,0,502,0}})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,row[0],0,0);p.position=new Vector2(700,646);p.ResetEffects();Main.screenPosition=new Vector2(600,500);p.inventory[54].SetDefaults(row[1]);p.inventory[54].stack=999;Main.rand=new Terraria.Utilities.UnifiedRandom(7123);
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(1100,646);n.life=n.lifeMax=10000;n.target=0;n.defense=0;n.netOffset=Vector2.Zero;n.velocity=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);
                // This thirteen-row oracle publishes a synthetic121 future
                // to compare native birth/derived roles and first Damage.
                // Its explicit mechanism scope is not a cold Main opportunity;
                // Shoot still consumes the real permission/input outlet.
                NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,0,phaseName:"BeforeNpc");
                if(row[2]==91)NativeCombatAimChecks.SolutionCheckpoint(context,attack,"effect91-before-consumer");
                var solution=NativeCombatAimChecks.RequireSolution(attack,p,"effect solution admits its actual enhanced hand input: "+row[2]);var plan=solution.Contact;
                bool representative=row[2]==91 || row[2]==89 || row[2]==639 || row[2]==207 || row[2]==20 || row[2]==955;
                Require(solution.Route==(representative?"representative":"dedicated-contact") && (representative || plan!=null),"six representative / seven dedicated effect routes retain their actual contact contract: "+row[2]);
                if(row[0]==518)Require(plan.Confidence==AttackConfidence.Representative,"CrystalStorm native random component offsets cannot promise an exact unborn birth");
                Require(!(bool)Get(attack,"hasConsumedInput"),"independent effect fixture has no prior hand consumption");int physicalX=Main.mouseX,physicalY=Main.mouseY,ammoBefore=p.inventory[54].stack;
                typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});
                NativeCombatAimChecks.RequireConsumedInput(attack,p,solution,"real effect Shoot consumes this row's Enhanced solution: "+row[2]);
                Require(Main.mouseX==physicalX && Main.mouseY==physicalY,"native effect hand returns the physical cursor");
                var parent=Main.projectile.Single(q=>q.active && q.owner==0);Require(parent.type==row[2],"native effect ammo identity");
                // Representative receive geometry is not a general first-Damage
                // promise. Seal the actual birth and ask the existing independent
                // known-family/scalar oracle before any native Update. These six
                // stationary cases have an exact primary outcome to retain.
                var known=representative?KnownPrimaryContact(attack,p,parent,solution):null;
                if(representative)
                {
                    Require(known!=null && known.ReceiveTick==solution.FirstTick+known.Tick-1,"born effect oracle retains its own receive/native phase equation: "+row[2]);
                    Console.WriteLine("EFFECT known-birth: primary="+row[2]+" native="+known.Tick+" receive="+known.ReceiveTick+" first="+solution.FirstTick+" confidence="+known.Confidence+" naturalUpdates=not-started");
                }
                Console.WriteLine("EFFECT prepared: primary="+row[2]+" phase="+solution.Phase+" route="+solution.Route+" point="+solution.Point+" reach="+solution.Reachability+" receive="+solution.ReceiveTick+" first="+solution.FirstTick+" contact="+(plan!=null)+" sample="+solution.Timeline.SampleTick+" target="+solution.Timeline.Identity.Slot+" ammo="+ammoBefore+"->"+p.inventory[54].stack);
                string parentKey=parent.key.ToString();var keys=new HashSet<string>();keys.Add(parentKey);int primaryFirst=0,derivedFirst=0,children=0;Vector2 parentBirth=parent.Center,birthVelocity=parent.velocity;
                for(int tick=1;tick<=180;tick++)
                {
                    NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(n.whoAmI);
                    for(int slot=0;slot<Main.maxProjectiles;slot++)
                    {
                        var q=Main.projectile[slot];if(!q.active)continue;
                        if(keys.Add(q.key.ToString())){children++;BirthCause cause;Require(parents.TryGetValue(q.key.ToString(),out cause) && (cause.Key==parentKey || row[2]==91 && cause.Type==92),"actual EntitySource_Parent identifies the natural parent/second-generation star");Console.WriteLine("EFFECT birth: primary="+parentKey+" sourceParent="+cause.Key+" sourceType="+cause.Type+" child="+q.key+" type="+q.type+" tick="+tick+" center="+q.Center+" velocity="+q.velocity+" delay="+q.ai[1]);Require(q.type==row[3],"native derived role remains separate from the primary");if(q.type==640)Require(q.Center==parentBirth && q.velocity==birthVelocity,"night arrow trail returns to sealed birth origin/velocity");}
                        int oldLife=n.life;Main.ProjectileUpdateLoopIndex=slot;try{q.Update(slot);}finally{Main.ProjectileUpdateLoopIndex=-1;}
                        if(n.life<oldLife){if(q.type==row[2] && primaryFirst==0)primaryFirst=tick;else if(q.type==row[3] && derivedFirst==0)derivedFirst=tick;}
                    }
                }
                Console.WriteLine("EFFECT result: primary="+row[2]+" expected="+solution.NativeTick+" first="+primaryFirst+" children="+children+" derivedDamage="+derivedFirst+" life="+n.life);
                Require(primaryFirst>0,"natural primary has a useful result");Require(plan!=null && plan.Confidence==AttackConfidence.Representative || primaryFirst==solution.NativeTick,"deterministic primary first Damage matches its actual solution clock");
                if(representative)Require(primaryFirst==known.Tick && known.ReceiveTick==solution.ReceiveTick,"actual stationary primary Damage agrees with independently known birth and representative receive geometry: "+row[2]);
                if(row[3]!=0)Require(children>0,"natural death emits its real derived stage");
            }
            NativeCombatProjectileEnvironmentChecks.Run(context);
            NativeCombatHarpyChecks.Run(context);
            NativeCombatFiniteThrowChecks.Run(context);
            NativeCombatSpecialArrowChecks.Run(context);
            NativeCombatExplosiveChecks.Run(context);
            NativeCombatHarpoonChecks.Run(context);
            NativeCombatClusterChecks.Run(context);
            NativeCombatColdBoltChecks.Run(context);
            NativeCombatScytheChecks.Run(context);
            NativeCombatFiniteShotChecks.Run(context);
            NativeCombatGemStaffChecks.Run(context);
            NativeCombatPoisonCloudChecks.Run(context);
            NativeCombatFlameChecks.Run(context);
            NativeCombatBeeChecks.Run(context);
            NativeCombatBeeGunChecks.Run(context);
            NativeCombatWaspBatChecks.Run(context);
            NativeCombatLineChecks.Run(context);
            NativeCombatLaserPaintChecks.Run(context);
            NativeCombatJavelinChecks.Run(context);
            NativeCombatPhoenixChecks.Run(context);
            NativeCombatThornChecks.Run(context);
            NativeCombatStreamChecks.Run(context);
            NativeCombatBottleChecks.Run(context);
            NativeCombatNoteChecks.Run(context);
            NativeCombatNebulaBlazeChecks.Run(context);
            NativeCombatSkyFractureChecks.Run(context);
            NativeCombatIceRodChecks.Run(context);
            NativeCombatAi29SpellChecks.Run(context);
            NativeCombatFlareChecks.Run(context);
            NativeCombatBlizzardChecks.Run(context);
            NativeCombatRaySpellChecks.Run(context);
            NativeCombatSonicBoomChecks.Run(context);
            NativeCombatBookStaffChecks.Run(context);
            NativeCombatSkullChecks.Run(context);
            NativeCombatOrnamentChecks.Run(context);
            NativeCombatInfernoChecks.Run(context);
            NativeCombatPaperAirplaneChecks.Run(context);
            NativeCombatArcSwingChecks.Run(context);
            NativeCombatAttachedShotChecks.Run(context);
            NativeCombatBetsyFlameChecks.Run(context);
            NativeCombatElectrosphereChecks.Run(context);
            NativeCombatBarrelChecks.Run(context);
            NativeCombatDartChecks.Run(context);
            NativeCombatMixedSwingChecks.Run(context);
            NativeCombatReflectionChecks.Run(context);
            NativeCombatScourgeChecks.Run(context);
            NativeCombatPhasebladeChecks.Run(context);
            CrystalBirths(context,combat,host,attack,input);CrystalMotor(context,combat,input);
            MagicBounceMotor(context,combat);
            MagicBurst(context,combat);
            Explosion(context,combat,host,attack,input);Explosion(context,combat,host,attack,input,true);
            Bounce(context,combat,host,attack,input);Bounce(context,combat,host,attack,input,true);
            HomingCases(context,combat,host,attack,input);
            ChaseQualification(context,combat,host,input);
            NativeCombatObstacleChecks.Run(context);
            NativeCombatBallisticCandidateChecks.Run(context);
            NativeCombatBallisticSeedChecks.Run(context);
            NativeCombatBallisticFreeFlightChecks.Run(context);
            NativeCombatBallisticRapierChecks.Run(context);
            NativeCombatBallisticCloudChecks.Run(context);
            NativeCombatBallisticTyphoonChecks.Run(context);
            NativeCombatBallisticSpiritChecks.Run(context);
            NativeCombatHomingEffectChecks.Run(context);
            NativeCombatNativeArrowBirthChecks.Run(context);
            NativeCombatMagnetSphereChecks.Run(context);
            NativeCombatPlacedPulseChecks.Run(context);
            NativeCombatTerrainLineChecks.Run(context);
            NativeCombatFixedSkyChecks.Run(context);
            NativeCombatSeedlerChecks.Run(context);
            NativeCombatRainbowChecks.Run(context);
            NativeCombatArrowFamilyChecks.Run(context);
            NativeCombatImpactBurstChecks.Run(context);
            NativeCombatClingerWallChecks.Run(context);
            NativeCombatJoustingLanceChecks.Run(context);
            NativeCombatWhipStarChecks.Run(context);
            NativeCombatHarpyArrowFamilyChecks.Run(context);
            NativeCombatBubbleFlailFamilyChecks.Run(context);
            NativeCombatPiranhaChecks.Run(context);
            NativeCombatLightsBaneChecks.Run(context);
            NativeCombatSharpTearsChecks.Run(context);
            NativeCombatNorthPoleChecks.Run(context);
            NativeCombatOnyxBlastChecks.Run(context);
            NativeCombatHeldAmmoFamilyChecks.Run(context);
            NativeCombatAmmoSteeringChecks.Run(context);
            NativeCombatBeeHandChecks.Run(context);
            NativeCombatMeleeHitChildChecks.Run(context);
            NativeCombatMeleeHitFamilyChecks.Run(context);
            NativeCombatAmmoImpactChecks.Run(context);
            NativeCombatArcOwnerPoseChecks.Run(context);
            NativeCombatGrassCurveChecks.Run(context);
            NativeCombatMonkStaffChecks.Run(context);
            FiniteTail(context);
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
        }
        private static AttackContact KnownPrimaryContact(object attack,Player player,Projectile shot,NativeCombatAimChecks.SolutionFacts solution)
        {
            const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
            var random=Main.rand;var randomFields=random.GetType().GetFields(flags).Where(f=>!f.IsStatic).ToArray();
            var randomValues=randomFields.Select(f=>f.GetValue(random) is Array a?(object)a.Clone():f.GetValue(random)).ToArray();
            var players=Main.player;var npcs=Main.npc;var projectiles=Main.projectile;uint tick=Main.GameUpdateCount;int loop=Main.ProjectileUpdateLoopIndex,mx=Main.mouseX,my=Main.mouseY;
            var dust=Main.dust;float dustCount=Dust.dCount;var camera=Main.screenPosition;var scratch=new[]{Collision.up,Collision.down,Collision.honey,Collision.shimmer,Collision.stair,Collision.stairFall,Collision.sloping};
            var live=Main.projectile.Where(q=>q.active).ToArray();var keys=live.Select(q=>q.key).ToArray();var target=Main.npc[solution.Timeline.Identity.Slot];
            var actors=Main.npc.Where(n=>n.active).ToArray();var actorPositions=actors.Select(n=>n.position).ToArray();var actorVelocities=actors.Select(n=>n.velocity).ToArray();var actorLife=actors.Select(n=>n.life).ToArray();var actorImmunity=actors.Select(n=>(int[])n.immune.Clone()).ToArray();
            var position=target.position;var velocity=target.velocity;int life=target.life;var immunity=(int[])target.immune.Clone();var ownerCounts=(int[])player.ownedProjectileCounts.Clone();
            var ownerPosition=player.position;var ownerVelocity=player.velocity;int direction=player.direction,animation=player.itemAnimation,time=player.itemTime,stack=player.inventory[54].stack;
            var birth=shot.Center;var speed=shot.velocity;var ai=(float[])shot.ai.Clone();var localAi=(float[])shot.localAI.Clone();int left=shot.timeLeft,penetrate=shot.penetrate;bool active=shot.active;
            try{return KnownPrimaryCore(attack,player,shot,solution);}
            finally
            {
                Require(ReferenceEquals(Main.rand,random) && randomFields.Select((f,i)=>{var value=f.GetValue(random);return value is Array a && randomValues[i] is Array b?a.Cast<object>().SequenceEqual(b.Cast<object>()):Equals(value,randomValues[i]);}).All(s=>s) &&
                    ReferenceEquals(Main.player,players) && ReferenceEquals(Main.npc,npcs) && ReferenceEquals(Main.projectile,projectiles) && Main.GameUpdateCount==tick && Main.ProjectileUpdateLoopIndex==loop && Main.mouseX==mx && Main.mouseY==my &&
                    ReferenceEquals(Main.dust,dust) && Dust.dCount==dustCount && Main.screenPosition==camera && scratch.SequenceEqual(new[]{Collision.up,Collision.down,Collision.honey,Collision.shimmer,Collision.stair,Collision.stairFall,Collision.sloping}) &&
                    live.SequenceEqual(Main.projectile.Where(q=>q.active)) && keys.SequenceEqual(live.Select(q=>q.key)) && actors.SequenceEqual(Main.npc.Where(n=>n.active)) && actors.Select((n,i)=>n.position==actorPositions[i] && n.velocity==actorVelocities[i] && n.life==actorLife[i] && actorImmunity[i].SequenceEqual(n.immune)).All(v=>v) && target.position==position && target.velocity==velocity && target.life==life && immunity.SequenceEqual(target.immune) &&
                    player.position==ownerPosition && player.velocity==ownerVelocity && player.direction==direction && player.itemAnimation==animation && player.itemTime==time && player.inventory[54].stack==stack && ownerCounts.SequenceEqual(player.ownedProjectileCounts) &&
                    shot.Center==birth && shot.velocity==speed && shot.timeLeft==left && shot.penetrate==penetrate && shot.active==active && ai.SequenceEqual(shot.ai) && localAi.SequenceEqual(shot.localAI),"independent born oracle preserves live RNG state, actor pools, owner/resources, receiver/immunity, born source, clock and cursor");
                Console.WriteLine("EFFECT known-birth isolation: type="+shot.type+" pools/RNG/actors/clock/cursor=unchanged");
            }
        }
        private static AttackContact KnownPrimaryCore(object attack,Player player,Projectile shot,NativeCombatAimChecks.SolutionFacts solution)
        {
            const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
            var assembly=attack.GetType().Assembly;var terrain=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");
            string family=shot.type==91 || shot.type==639?"HostArrowFamilyAttack":shot.type==89?"HostAmmoImpactAttack":shot.type==207 || shot.type==36?"HostAmmoSteeringAttack":null;
            if(family!=null)
            {
                var root=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),flags,null,new object[]{player,shot,solution.Timeline,0,true,false},null);
                return (AttackContact)assembly.GetType("JueMingR.TerrariaHost.Combat."+family,true).GetMethod("KnownFamily",flags).Invoke(null,new object[]{player,new[]{shot},solution.Timeline,NativeCombatAimChecks.PhaseClock(attack,solution.Timeline,"BeforeNpc"),terrain,root});
            }
            var args=new object[]{Get(attack,"ammo"),default(AttackMotion)};
            Require((bool)assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels",true).GetMethod("TryRead",flags).Invoke(null,args),"born ordinary effect has an explicit scalar motion");
            return AttackIntercept.Replay(solution.Point.X,solution.Point.Y,shot.Center.X,shot.Center.Y,shot.velocity.X,shot.velocity.Y,(AttackMotion)args[1],solution.Timeline,solution.FirstTick-1);
        }
        private static void FiniteTail(object context)
        {
            NativeCombatSuperStarChecks.Run(context);
            NativeCombatNebulaArcanumChecks.Run(context);
            NativeCombatMedusaScanChecks.Run(context);
            NativeCombatSolarEruptionChecks.Run(context);
            NativeCombatInfluxChecks.Run(context);
            NativeCombatNailChecks.Run(context);
            NativeCombatSpearShardChecks.Run(context);
            NativeCombatGuitarCurveChecks.Run(context);
            NativeCombatYoyoFamilyChecks.Run(context);
            NativeCombatWeatherPainChecks.Run(context);
            NativeCombatPhantasmEchoChecks.Run(context);
        }
        private static void ChaseQualification(object context,object combat,object host,object input)
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var obstacles=combat.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true);
            var receive=combat.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackReceive",true);
            foreach(int changedSlot in new[]{2,3})
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,1446,0,0);p.ResetEffects();
                foreach(int slot in new[]{2,3}){var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.chaseable=true;n.friendly=n.dontTakeDamage=n.immortal=false;n.life=n.lifeMax=10000;n.position=new Vector2(900+slot*30,646);n.netOffset=Vector2.Zero;Array.Clear(n.immune,0,n.immune.Length);}
                var target=Main.npc[2];var shot=new Projectile();shot.SetDefaults(297);shot.owner=p.whoAmI;
                var key=new NpcIdentity((long)Get(host,"Session"),target,2,target.generation,target.type,target.netID);var state=new NpcMotionState{Identity=key,X=target.position.X,Y=target.position.Y,Width=target.width,Height=target.height,CanReceive=true,Active=true};var points=Enumerable.Range(0,3).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();var timeline=new NpcTrajectory(key,Main.GameUpdateCount,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length);
                var capture=Activator.CreateInstance(obstacles,flags,null,new object[]{p,shot,timeline,0,false,false},null);Require((bool)Get(capture,"Unchanged"),"native selected/other chase lease starts current");
                var before=receive.GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{p,shot,changedSlot,false,false});Require(Main.npc[changedSlot].CanBeChasedBy(shot),"native scene begins with chaseable target");Main.npc[changedSlot].chaseable=false;
                var after=receive.GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{p,shot,changedSlot,false,false});Require((bool)receive.GetMethod("Same",flags).Invoke(before,new[]{after}),"chase permission changes without changing native Damage permission");Require(!Main.npc[changedSlot].CanBeChasedBy(shot) && !(bool)Get(capture,"Unchanged"),"selected and other autonomous chase changes invalidate the prepared scene: "+changedSlot);
            }
            Console.WriteLine("PASS independent native chase qualification invalidates selected/other scene without Damage permission change: 2 cases");
        }
        private static void MagicBurst(object context,object combat)
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;var assembly=combat.GetType().Assembly;var motor=assembly.GetType("JueMingR.TerrariaHost.Combat.HostMagicBounceAttack",true);var stateType=motor.GetNestedType("State",BindingFlags.NonPublic);var terrainType=assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);bool wind=Main.windPhysics;
            try{foreach(int previous in new[]{0,4})foreach(bool far in new[]{false,true})
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3063,0,0);Main.windPhysics=false;Main.tile[60,21].active(true);Main.tile[60,21].type=1;
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(940,far?420:370);n.life=n.lifeMax=10000;n.netOffset=Vector2.Zero;n.defense=0;Array.Clear(n.immune,0,n.immune.Length);
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(950,350),new Vector2(6,0),502,50,0,p.whoAmI);var shot=Main.projectile[slot];shot.ai[0]=previous;
                var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var state=Activator.CreateInstance(stateType,flags,null,new object[]{shot,shot.Center,shot.velocity},null);var args=new object[]{Terraria.ID.ContentSamples.ProjectilesByType[502],terrain,state};Require((bool)motor.GetMethod("Step",flags).Invoke(null,args),"Meowmere known bounce retains terrain dependencies");state=args[2];
                var key=new NpcIdentity(1,new object(),2,1,n.type,n.netID);var npcState=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true};var points=Enumerable.Range(0,3).Select(t=>new NpcTrajectoryPoint(t,npcState)).ToArray();var timeline=new NpcTrajectory(key,0,1,PredictionAssumption.None,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);int side=(int)Get(state,"BurstSide");Require(side==40+8*(previous+1),"Meowmere collision stage has its native expanding burst bounds");var exact=AttackIntercept.BodyContact(timeline,1,0,10000,350,(float)Get(state,"BurstX"),(float)Get(state,"BurstY"),side,side,AttackConfidence.Conditional);
                shot.Update(slot);Require(far?n.life==10000 && exact==null:n.life<10000 && exact!=null && exact.Tick==1,"Meowmere natural pre-position collision burst matches selected native receiver including final Kill: "+previous+"/"+far);Require(shot.active==(previous==0),"fifth Meowmere bounce retires primary while retaining its natural burst");
                Console.WriteLine("EFFECT Meowmere burst: previous="+previous+" far="+far+" side="+side+" life="+n.life+" active="+shot.active);
            }}finally{Main.windPhysics=wind;}
            Console.WriteLine("PASS Meowmere finite collision burst: first/final bounce x inside/outside actual Damage=4 cases");
        }
        private static void MagicBounceMotor(object context,object combat)
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;var assembly=combat.GetType().Assembly;var motor=assembly.GetType("JueMingR.TerrariaHost.Combat.HostMagicBounceAttack",true);var stateType=motor.GetNestedType("State",BindingFlags.NonPublic);var step=motor.GetMethod("Step",flags);var terrainType=assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);int cases=0,outer=0,powered=0,bounces=0;bool wind=Main.windPhysics;float speed=Main.windSpeedCurrent,strength=Main.windPhysicsStrength;
            try{foreach(int type in new[]{15,27,95,253,502})foreach(int scene in Enumerable.Range(0,12))
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),112,0,0);Main.windPhysics=scene==7;Main.windSpeedCurrent=-.1f;Main.windPhysicsStrength=1;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();
                bool platformBounce=scene==1 && (type==15 || type==253);int platformY=platformBounce?60:25;
                if(scene==1)for(int x=40;x<=80;x++){Main.tile[x,platformY].active(true);Main.tile[x,platformY].type=19;Main.tile[x,platformY].frameY=0;}
                if(scene>=2 && scene<=5 || scene>=8)
                {
                    for(int x=40;x<=80;x++)foreach(int y in new[]{15,45}){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}
                    for(int y=15;y<=45;y++)foreach(int x in new[]{40,80}){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}
                    if(scene>=3 && scene<=5)for(int x=41;x<80;x++)for(int y=16;y<45;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==3?0:scene==4?2:1);}
                }
                if(scene==6){Main.tile[60,25].active(true);Main.tile[60,25].type=1;Main.tile[60,25].slope(1);}
                if(scene>=8){Main.tile[60,25].active(true);Main.tile[60,25].type=1;Main.tile[60,25].slope((byte)(scene-7));}
                var origin=scene>=8?new Vector2(936,390):scene==6?new Vector2(968,402):platformBounce?new Vector2(950,900):new Vector2(950,350);var velocity=scene>=8?new Vector2(20,8):scene==1?new Vector2(1,platformBounce?12:3):new Vector2(6,3);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),origin,velocity,type,50,0,p.whoAmI);var shot=Main.projectile[slot];
                var state=Activator.CreateInstance(stateType,flags,null,new object[]{shot,shot.Center,shot.velocity},null);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");
                for(int tick=1;tick<=80 && shot.active;tick++)
                {
                    int updates=(int)Get(state,"ExtraUpdates")+1;for(int sub=0;sub<updates && (bool)Get(state,"Alive") && (int)Get(state,"Left")>0;sub++){var args=new object[]{Terraria.ID.ContentSamples.ProjectilesByType[type],terrain,state};Require((bool)step.Invoke(null,args),"AI008 finite motor retains all scene dependencies: "+type+"/"+scene+"/"+tick+" model="+Get(state,"X")+","+Get(state,"Y")+" velocity="+Get(state,"Vx")+","+Get(state,"Vy")+" native="+shot.Center+"/"+shot.velocity);state=args[2];}
                    shot.Update(slot);outer++;Require(shot.active==((bool)Get(state,"Alive") && (int)Get(state,"Left")>0),"AI008 original water/finite-bounce retirement agrees: "+type+"/"+scene+"/"+tick);
                    if(shot.active)Require(Vector2.DistanceSquared(shot.Center,new Vector2((float)Get(state,"X"),(float)Get(state,"Y")))<.02f && Vector2.DistanceSquared(shot.velocity,new Vector2((float)Get(state,"Vx"),(float)Get(state,"Vy")))<.0001f && shot.ai[0]==(float)Get(state,"Ai0") && shot.ai[1]==(float)Get(state,"Ai1") && shot.extraUpdates==(int)Get(state,"ExtraUpdates"),"AI008 native movement/AI/platform/liquid/slope facts agree: "+type+"/"+scene+"/"+tick+" native="+shot.Center+" model="+Get(state,"X")+","+Get(state,"Y"));
                }
                if(shot.ai[0]>0)bounces++;if(type==27 && scene==5){Require(shot.ai[1]==1 && shot.extraUpdates==5,"WaterBolt real lava power switches only future outer update counts");powered++;}if(type==15 && (scene==3 || scene==4))Require(!shot.active,"fire's native preceding-wet destruction remains distinct from lava");
                Console.WriteLine("EFFECT AI008 motor: type="+type+" scene="+scene+" bounces="+shot.ai[0]+" ai1="+shot.ai[1]+" extra="+shot.extraUpdates+" active="+shot.active);cases++;
            }}finally{Main.windPhysics=wind;Main.windSpeedCurrent=speed;Main.windPhysicsStrength=strength;}
            // The former near-top platform scene intentionally leaves the
            // acquired terrain. This is an unknown-query retirement, never
            // evidence that its physical trajectory is unable to hit.
            var boundaryPlayer=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),112,0,0);Main.windPhysics=false;
            try
            {
                for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();for(int x=40;x<=80;x++){Main.tile[x,25].active(true);Main.tile[x,25].type=19;Main.tile[x,25].frameY=0;}
                int boundarySlot=Projectile.NewProjectile(boundaryPlayer.GetProjectileSource_Item(boundaryPlayer.HeldItem),950,350,1,12,15,50,0,boundaryPlayer.whoAmI);var boundaryShot=Main.projectile[boundarySlot];var boundary=Activator.CreateInstance(stateType,flags,null,new object[]{boundaryShot,boundaryShot.Center,boundaryShot.velocity},null);var boundaryTerrain=Activator.CreateInstance(terrainType,true);Call(boundaryTerrain,"Reset");int retired=0;
                for(int tick=1;tick<=80 && boundaryShot.active;tick++){var args=new object[]{Terraria.ID.ContentSamples.ProjectilesByType[15],boundaryTerrain,boundary};if(!(bool)step.Invoke(null,args)){retired=tick;break;}boundary=args[2];boundaryShot.Update(boundarySlot);}
                Require(retired>0 && (float)Get(boundary,"Y")<40,"outer QueryArea boundary explicitly retires an unknown AI008 continuation");Console.WriteLine("EFFECT AI008 unknown-boundary: predictionRetired="+retired+" nativeActive="+boundaryShot.active+" center="+boundaryShot.Center+" boundary=unknown-terrain; not-physical-unreachable");
            }finally{Main.windPhysics=wind;}
            Require(powered==1 && bounces>5,"AI008 matrix reaches power and actual reflection stages");Console.WriteLine("PASS full native AI008 scalar motor: cases="+cases+" outerUpdates="+outer+" realBounceCases="+bounces+" lavaPowerCases="+powered);
        }
        private static void CrystalMotor(object context,object combat,object input)
        {
            var assembly=combat.GetType().Assembly;var model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostCrystalAttack",true);var stateType=model.GetNestedType("State",BindingFlags.NonPublic);var step=model.GetMethod("Step",BindingFlags.Static|BindingFlags.NonPublic);var terrainType=assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);bool wind=Main.windPhysics;int cases=0,reflections=0;
            try{foreach(int type in new[]{94,1114})foreach(int scene in Enumerable.Range(0,9))
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,518,0,0);Main.windPhysics=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();
                if(scene==1)for(int y=15;y<45;y++){Main.tile[65,y].active(true);Main.tile[65,y].type=1;}
                if(scene>=2 && scene<=4)for(int x=40;x<80;x++)for(int y=15;y<45;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==2?0:scene==3?2:1);}
                if(scene>=5)
                {
                    for(int x=40;x<=80;x++)foreach(int y in new[]{15,45}){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}
                    for(int y=15;y<=45;y++)foreach(int x in new[]{40,80}){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}
                    Main.tile[60,25].active(true);Main.tile[60,25].type=1;Main.tile[60,25].slope((byte)(scene-4));
                }
                int slot=scene>=5?Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),936,390,24,8,type,50,0,p.whoAmI):Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,6,0,type,50,0,p.whoAmI);var shot=Main.projectile[slot];var shadow=Activator.CreateInstance(stateType,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{shot,shot.Center,shot.velocity},null);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");int ticks=0;bool reflected=false;
                for(int tick=1;tick<=180 && shot.active;tick++)
                {
                    var args=new object[]{shot,terrain,shadow};Require((bool)step.Invoke(null,args),"AI024 finite motor keeps all scene dependencies");shadow=args[2];float previous=shot.velocity.X;shot.Update(slot);ticks++;reflected|=previous>0 && shot.velocity.X<0;
                    Require(shot.active==(bool)Get(shadow,"Alive"),"AI024 scale/water/wall exit agrees: "+type+"/"+scene+"/"+tick);
                    if(shot.active)Require(Vector2.DistanceSquared(shot.Center,new Vector2((float)Get(shadow,"X"),(float)Get(shadow,"Y")))<.02f && Vector2.DistanceSquared(shot.velocity,new Vector2((float)Get(shadow,"Vx"),(float)Get(shadow,"Vy")))<.0001f && shot.ai[1]==(float)Get(shadow,"Age") && shot.scale==(float)Get(shadow,"Scale"),"AI024 complete native motor agrees: "+type+"/"+scene+"/"+tick+" native="+shot.Center+"/"+shot.velocity+" model="+Get(shadow,"X")+","+Get(shadow,"Y"));
                }
                Require(!shot.active,"AI024 reaches its actual finite exit");if(type==94 && scene==1){Require(reflected,"CrystalStorm actually reaches wall reflection");reflections++;}if(type==1114 && scene>=2 && scene<=4)Require(ticks==1,"shimmer crystal dies from the actual born wet state before body Damage");cases++;
            }}finally{Main.windPhysics=wind;}Console.WriteLine("PASS native AI024 scale/reflect/water motor: cases="+cases+" reflectionCases="+reflections);
        }
        private static void CrystalBirths(object context,object combat,object host,object attack,object input)
        {
            var crystal=combat.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostCrystalAttack",true);var stateType=crystal.GetNestedType("State",BindingFlags.NonPublic);var step=crystal.GetMethod("Step",BindingFlags.Static|BindingFlags.NonPublic);var replay=crystal.GetMethod("Replay",BindingFlags.Static|BindingFlags.NonPublic);var terrainType=combat.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);int hits=0;bool wind=Main.windPhysics;
            try{for(int seed=0;seed<16;seed++)
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,518,0,0);p.position=new Vector2(700,646);p.ResetEffects();Main.screenPosition=new Vector2(600,500);Main.windPhysics=false;Main.rand=new Terraria.Utilities.UnifiedRandom(7123+seed);
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(1100,646);n.life=n.lifeMax=10000;n.target=0;n.defense=0;n.netOffset=Vector2.Zero;n.velocity=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);
                NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,0);var representative=(AttackContact)GetOptional(attack,"ExpectedImpact");Require(representative!=null && representative.Confidence==AttackConfidence.Representative,"unborn CrystalStorm random velocity remains a representative");
                typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var shot=Main.projectile.Single(q=>q.active && q.owner==0);Require(shot.type==94,"CrystalStorm creates its actual AI024 member");
                var key=new NpcIdentity(1,new object(),2,1,n.type,n.netID);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true};var points=Enumerable.Range(0,181).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();var timeline=new NpcTrajectory(key,0,1,PredictionAssumption.None,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
                var birth=shot.Center;var velocity=shot.velocity;var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var exact=(AttackContact)replay.Invoke(null,new object[]{shot,terrain,timeline,new Vector2(representative.AimX,representative.AimY),birth,velocity,0});var shadow=Activator.CreateInstance(stateType,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{shot,birth,velocity},null);int first=0;
                for(int tick=1;tick<=180 && shot.active;tick++)
                {
                    for(int sub=0;sub<shot.extraUpdates+1;sub++){var stepArgs=new object[]{shot,terrain,shadow};Require((bool)step.Invoke(null,stepArgs),"known CrystalStorm birth has all finite terrain dependencies: "+seed+"/"+tick);shadow=stepArgs[2];}
                    shot.Update(shot.whoAmI);if(n.life<10000){first=tick;break;}Require(shot.active==(bool)Get(shadow,"Alive"),"CrystalStorm native scale/retirement matches known finite state");
                    if(shot.active)Require(Vector2.DistanceSquared(shot.Center,new Vector2((float)Get(shadow,"X"),(float)Get(shadow,"Y")))<.02f && Vector2.DistanceSquared(shot.velocity,new Vector2((float)Get(shadow,"Vx"),(float)Get(shadow,"Vy")))<.0001f,"actual CrystalStorm damping/reflection before Damage: seed="+seed+" tick="+tick+" native="+shot.Center+"/"+shot.velocity+" model="+Get(shadow,"X")+","+Get(shadow,"Y")+"/"+Get(shadow,"Vx")+","+Get(shadow,"Vy"));
                }
                Require(first==(exact==null?0:exact.Tick),"known CrystalStorm birth first Damage matches exact replay without tolerance: seed="+seed+" actual="+first+" contact="+exact?.Tick);if(first>0)hits++;
                Console.WriteLine("EFFECT crystal birth: seed="+seed+" center="+birth+" velocity="+velocity+" representative="+representative.Tick+" actual="+first+" exact="+exact?.Tick);
            }}finally{Main.windPhysics=wind;}
            Require(hits>0,"representative CrystalStorm input produces actual useful shots");Console.WriteLine("PASS CrystalStorm actual random births: exact motor/contact=16/16; useful natural hits="+hits+"/16; unborn confidence=Representative");
        }
        private static void HomingCases(object context,object combat,object host,object attack,object input)
        {
            // These direct Shoot/finite native Update oracles provide their
            // receiver path explicitly. Use the component allowance, keeping
            // all original outcomes separate from ordinary Main opportunity.
            foreach(int scene in new[]{0,1,2})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.ResetEffects();Main.screenPosition=new Vector2(600,500);p.inventory[54].SetDefaults(1179);p.inventory[54].stack=999;
                var n=Main.npc[2];n.SetDefaults(scene==2?488:3);n.whoAmI=2;n.active=true;n.position=new Vector2(1100,646);n.target=0;n.netOffset=Vector2.Zero;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);if(scene!=2)n.life=n.lifeMax=10000;
                if(scene==0)for(int y=20;y<=43;y++){Main.tile[56,y].active(true);Main.tile[56,y].type=1;}
                if(scene==1){var other=Main.npc[3];other.SetDefaults(3);other.whoAmI=3;other.active=true;other.position=new Vector2(1000,646);other.life=other.lifeMax=10000;other.target=0;other.netOffset=Vector2.Zero;Array.Clear(other.immune,0,other.immune.Length);}
                NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");Call(Get(host,"Selection"),"SampleMouse",input);NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,scene==2,25,false,true));NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,0,phaseName:"BeforeNpc");var solution=NativeCombatAimChecks.ReadSolution(attack);var available=NativeCombatAimChecks.QueryInput(attack,p);dummyHits=0;
                Require(solution.HasPoint && available.Allowed && available.Kind=="Enhanced" && available.Target.Equals(solution.Timeline.Identity) && available.Point==solution.Point,"homing boundary consumes current representative direction without claiming an ambient Damage root");
                Require(!(bool)Get(attack,"hasConsumedInput"),"homing boundary has no prior actual hand consumption");
                typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});NativeCombatAimChecks.RequireConsumedInput(attack,p,solution,"homing boundary actual Enhanced hand consumption");var q=Main.projectile.Single(s=>s.active && s.type==207);var plan=KnownPrimaryContact(attack,p,q,solution);int first=0;bool untracked=n.CanBeChasedBy(null);
                for(int tick=1;tick<=45 && q.active;tick++){q.Update(q.whoAmI);if(scene==2?dummyHits>0:n.life<10000){first=tick;break;}}
                Console.WriteLine("EFFECT homing boundary: scene="+scene+" contact="+plan?.Tick+" first="+first+" nativeTarget="+q.ai[1]+" chaseable="+untracked+" dummyHits="+dummyHits+" blockerLife="+Main.npc[3].life);
                Require(scene==2?plan!=null && first>0 && !untracked && n.immortal && q.ai[1]==0:plan==null && first==0,"native autonomous wall/nearer target differs from valid direct immortal-dummy hit");if(scene==1)Require(Main.npc[3].life<10000,"native homing chooses and hits the nearer NPC without product steering");
            }
        }
        private static void Bounce(object context,object combat,object host,object attack,object input,bool closed=false)
        {
            var original=Main.rand;var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            var copy=(Terraria.Utilities.UnifiedRandom)typeof(object).GetMethod("MemberwiseClone",flags).Invoke(original,null);
            foreach(var field in original.GetType().GetFields(flags).Where(f=>!f.IsStatic))if(field.GetValue(original) is Array state)field.SetValue(copy,state.Clone());
            BounceCase(context,combat,host,attack,input,closed,false);
            try{Main.rand=copy;BounceCase(context,combat,host,attack,input,closed,true);}finally{Main.rand=original;}
        }
        private static void BounceCase(object context,object combat,object host,object attack,object input,bool closed,bool physical)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.ResetEffects();Main.screenPosition=new Vector2(600,500);p.inventory[54].SetDefaults(234);p.inventory[54].stack=999;
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(850,620);n.life=n.lifeMax=10000;n.target=0;n.netOffset=Vector2.Zero;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);Main.tile[49,40].active(true);Main.tile[49,40].type=1;
            if(closed)for(int y=39;y<=43;y++){Main.tile[51,y].active(true);Main.tile[51,y].type=1;}
            NativeToolExecutionChecks.Sample(context,input,new Vector2(810,708),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,0,phaseName:"BeforeNpc");var solution=NativeCombatAimChecks.ReadSolution(attack);var available=NativeCombatAimChecks.QueryInput(attack,p);
            Require(solution.HasPoint && available.Allowed && available.Kind=="Enhanced" && available.Target.Equals(solution.Timeline.Identity) && available.Point==solution.Point,"bounce public direction retains actual input identity without a full family root requirement");
            Require(!(bool)Get(attack,"hasConsumedInput"),"bounce boundary has no prior actual hand consumption");
            if(physical){NativeCombatObservationChecks.Save(host,new ObservationOptions());Main.mouseX=(int)(810-Main.screenPosition.X);Main.mouseY=(int)(708-Main.screenPosition.Y);}
            typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});if(!physical)NativeCombatAimChecks.RequireConsumedInput(attack,p,solution,"bounce actual Enhanced hand consumption");var q=Main.projectile.Single(s=>s.active && s.type==36);var plan=KnownPrimaryContact(attack,p,q,solution);int first=0;bool bounced=false;
            for(int tick=1;tick<=60 && q.active;tick++){var old=q.velocity;q.Update(q.whoAmI);if(old.Y>0 && q.velocity.Y<0)bounced=true;if(n.life<10000){first=tick;break;}}
            Console.WriteLine("EFFECT bounce: closed="+closed+" physical="+physical+" expected="+plan?.Tick+" first="+first+" bounce="+bounced+" remaining="+q.penetrate+" center="+q.Center);Require(closed?first==0:first>0 && (!physical || bounced),"native MeteorShot public usefulness and independent fixed-input reflection respect the open/closed passage");Require(closed?plan==null:plan!=null && plan.Tick==first,"finite reflection contact matches first natural Damage without changing native bounce");
        }
        private static void Explosion(object context,object combat,object host,object attack,object input,bool far=false)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,759,0,0);p.position=new Vector2(700,646);p.ResetEffects();Main.screenPosition=new Vector2(600,500);p.inventory[54].SetDefaults(771);p.inventory[54].stack=999;
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(far?990:920,610);n.life=n.lifeMax=10000;n.target=0;n.netOffset=Vector2.Zero;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);
            for(int y=20;y<=43;y++){Main.tile[56,y].active(true);Main.tile[56,y].type=1;}
            NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(true,true,false,false,false,25,false,true));NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,0);var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");
            typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var q=Main.projectile.Single(s=>s.active && s.type==134);int first=0;
            Console.WriteLine("EFFECT rocket birth: position="+q.position+" size="+q.width+"x"+q.height+" velocity="+q.velocity+" speed="+p.HeldItem.shootSpeed+" plan="+plan?.ImpactX+","+plan?.ImpactY);
            for(int tick=1;tick<=60 && q.active;tick++){q.Update(q.whoAmI);if(tick>=16)Console.WriteLine("EFFECT rocket phase: tick="+tick+" center="+q.Center+" velocity="+q.velocity+" active="+q.active);if(n.life<10000){first=tick;break;}}
            Console.WriteLine("EFFECT wall explosion: far="+far+" expected="+plan?.Tick+" first="+first+" active="+q.active+" body="+q.width+"x"+q.height+" center="+q.Center);
            bool expanded=false;foreach(var sample in (Array)Get(Get(host,"Geometry"),"Events"))if(sample!=null && (int)Get(sample,"Type")==134 && (int)Get(sample,"Count")>0){var shape=((Array)Get(sample,"Shapes")).GetValue(0);var a=(Vector2)Get(shape,"A");var b=(Vector2)Get(shape,"B");expanded|=b.X-a.X==128 && b.Y-a.Y==128;}
            // Kill restores/resizes the visual body after its Damage call.
            // Observe the existing natural event lane at the actual window,
            // rather than querying Damage or reading the final visual size.
            Require(!q.active && expanded,"native wall Kill emits its observed 128 damage rectangle beyond the untraversable wall");Require(far?first==0 && plan==null:first>0 && plan!=null && plan.Tick==first,"known explosion trigger/area distinguishes target inside/outside the native area");
        }
    }
}
