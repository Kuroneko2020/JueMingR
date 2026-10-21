using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatSkyChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static SkyWitness observing;
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");var tools=Get(context,"Tools");
            foreach(int type in new[]{3029,4381,2750,65,3065,3570})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,type,0,0);p.position=new Vector2(700,900);p.velocity=Vector2.Zero;p.inventory[54].SetDefaults(40);p.inventory[54].stack=999;
                Main.screenPosition=new Vector2(600,800);var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(1100,1000);n.life=n.lifeMax=10000;n.target=0;Array.Clear(n.immune,0,n.immune.Length);
                // Tool Reset installs a full solid floor at tile Y43. This
                // positive scene is open sky: an arrow cannot reach the target
                // below that ceiling. Terrain negatives retain their own wall.
                for(int tx=5;tx<115;tx++)Main.tile[tx,43].ClearEverything();
                // This direct native Shoot oracle supplies a synthetic moving
                // receiver. Its finite mechanism allowance is separate from
                // cold Main opportunity; sample the actual pre-NPC phase.
                NativeToolExecutionChecks.Sample(context,input,new Vector2(650,850),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,1,phaseName:"BeforeNpc");
                // The native-arrow birth family uses the representative
                // solution. The remaining dedicated sky solvers still owe
                // their full Contact; nullable ordinary Contact cannot weaken
                // those independent lifecycle oracles.
                var plan=NativeCombatAimChecks.RequireSolution(attack,p,"sky consumer prepares its actual enhanced birth/flight input: "+type);
                if(type==3029)NativeCombatAimChecks.SolutionCheckpoint(context,attack,"sky3029-prepared");
                if(type!=3029)Require(plan.Contact!=null && plan.Contact.Confidence==AttackConfidence.Representative,"dedicated sky consumer retains its full contact: "+type);
                Require(plan.Point.X>=1100 && plan.Point.X<1300 && Math.Abs(plan.Point.Y-1020)<25,"sky AimInput is future cursor point, not a long muzzle direction");
                Main.rand=new Terraria.Utilities.UnifiedRandom(7123);int x=Main.mouseX,y=Main.mouseY;
                using(var witness=type==3029?new SkyWitness(attack,p,n,plan):null)
                {
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});
                if(type==3029)NativeCombatAimChecks.SolutionCheckpoint(context,attack,"sky3029-shot");
                var shots=Main.projectile.Where(q=>q.active && q.owner==0).ToArray();witness?.Births(shots,"natural");Require(shots.Length>=1 && shots.Length<=4,"native sky quantity preserved");
                Require(shots.All(q=>q.Center.Y<=p.MountedCenter.Y-590 && q.velocity.Y>0),"native sky birth and downward flight use real input branch");
                if(type==4381)Require(shots.All(q=>q.type==819 && q.tileCollide),"blood arrow conversion retains immediate terrain collision");
                if(type==3570)Require(shots.All(q=>Math.Abs(q.ai[1]-plan.Point.Y)<1.01f),"lunar cursorY is preserved as native terrain threshold");
                if(type==3065)Require(shots.All(q=>Math.Abs(q.ai[1]-(p.Center.Y-200))<.01f),"star wrath clamps native terrain threshold to player height");
                Require(Main.mouseX==x && Main.mouseY==y,"sky cursor returns to physical intent");Console.WriteLine("PASS native sky input/birth: weapon="+type+" representativeTick="+plan.NativeTick+" receive="+plan.ReceiveTick+" first="+plan.FirstTick+" phase="+plan.Phase+" route="+plan.Route+" contact="+(plan.Contact!=null)+" cursor="+plan.Point+" births="+shots.Length);
                int firstDamage=0;
                for(int tick=1;tick<=120;tick++)
                {NativeQuickItemChecks.BeginWorldStep();n.position.X=1100+tick;if(n.immune[0]>0)n.immune[0]--;witness?.At(tick);foreach(var q in Main.projectile.Where(q=>q.active && q.owner==0).ToArray()){q.Update(q.whoAmI);witness?.After(q);}if(n.life<10000){firstDamage=tick;break;}}
                Console.WriteLine("NATIVE-SKY-RESULT weapon="+type+" seeded firstDamage="+firstDamage+" meanPlan="+plan.NativeTick);
                witness?.Complete(firstDamage,context);
                // Random birth/spread is intentionally not equated to the
                // representative point. This fixed seed tests useful actual
                // native damage, with the prescribed shared moving timeline.
                if(type==3570)Require(firstDamage>0,"representative sky input reaches useful native damage in seeded scene: "+type);
                }
                Array.Clear(n.immune,0,n.immune.Length);
                if(type==4381 || type==3570)
                {
                    foreach(var q in Main.projectile)q.active=false;n.position=new Vector2(1100,1000);n.life=10000;
                    for(int tx=5;tx<115;tx++)NativeToolsChecks.Tile(tx,43,1);
                    NativeToolExecutionChecks.Sample(context,input,new Vector2(1100,1020),true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,1,phaseName:"BeforeNpc");
                    var roofPlan=(AttackContact)GetOptional(attack,"ExpectedImpact");Require(type==4381?roofPlan==null:roofPlan!=null,"same roof blocks blood arrow but lunar native pre-threshold phase passes");
                    Main.rand=new Terraria.Utilities.UnifiedRandom(7123);typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});int damage=0;
                    for(int tick=1;tick<=120;tick++){NativeQuickItemChecks.BeginWorldStep();n.position.X=1100+tick;if(n.immune[0]>0)n.immune[0]--;foreach(var q in Main.projectile.Where(q=>q.active && q.owner==0).ToArray())q.Update(q.whoAmI);if(n.life<10000){damage=tick;break;}}
                    Require(type==4381?damage==0:damage>0,"real same-roof Damage differs according to native terrain phase: "+type);
                    Array.Clear(n.immune,0,n.immune.Length);Console.WriteLine("PASS sky same roof -> real Damage: weapon="+type+" firstDamage="+damage+" plan="+(roofPlan!=null));
                }
            }
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
        }
        // This finite observer keeps the original seeded miss and its separate
        // natural-outcome window. Its ordinal-0 run uses the existing legal birth
        // adapter, not a second aim owner or a fabricated full Contact. Private
        // actor pools, RNG and native scratch return even when that run fails.
        private sealed class SkyWitness:IDisposable
        {
            private sealed class Arrow
            {
                internal Projectile Body;internal Terraria.DataStructures.ProjectileKey Key;internal string Birth;internal float Gap=float.MaxValue,FutureGap=float.MaxValue;internal int NearTick,Exit,DamageCalls,Damage,KillTick;
                internal int Hand,BornAt,Updates,DamageEvents,FirstDamageWorld,FirstDamageAge,FirstTick=1;internal JueMingR.Platform.Combat.NpcTrajectory Timeline;
                internal string Near,Last,Kill;
            }
            private readonly object attack;private readonly Player pose;private readonly NPC receiver;private readonly NativeCombatAimChecks.SolutionFacts plan;
            private readonly Projectile representative;private readonly Terraria.Utilities.UnifiedRandom random;private readonly Harmony audit;private readonly uint preparedTick;
            private List<Arrow> arrows=new List<Arrow>();private NPC target;private int tick;private string stage;private bool consumedMatches;
            private static readonly MethodInfo clone=typeof(object).GetMethod("MemberwiseClone",Flags);
            internal SkyWitness(object attack,Player player,NPC npc,NativeCombatAimChecks.SolutionFacts plan)
            {
                Require(observing==null,"one finite sky witness owns native observation");this.attack=attack;this.plan=plan;pose=CopyPlayer(player);receiver=CopyNpc(npc);target=npc;preparedTick=Main.GameUpdateCount;random=CopyRandom(Main.rand);
                var scratch=Scratch();
                try
                {
                    var assembly=attack.GetType().Assembly;var terrain=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");
                    var origin=(Vector2)assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels",true).GetMethod("LaunchOrigin",Flags).Invoke(null,new object[]{pose,pose.HeldItem});
                    var born=(Projectile[])assembly.GetType("JueMingR.TerrariaHost.Combat.HostNativeArrowBirthAttack",true).GetMethod("Births",Flags).Invoke(null,new object[]{pose,Get(attack,"ammo"),origin,plan.Point,0,terrain});
                    Require(born!=null && born.Length==3,"existing legal midpoint birth adapter supplies all sky ordinals");representative=born[0];
                    Require(SameRandom(random,Main.rand),"legal sky birth capture never consumes the natural seeded stream");
                }
                finally{RestoreScratch(scratch);}
                Console.WriteLine("SKY-WITNESS state seed=7123 prepared="+preparedTick+" phase="+plan.Phase+" sample="+plan.Timeline.SampleTick+" count="+plan.Timeline.Count+" point="+plan.Point+" receive="+plan.ReceiveTick+" first="+plan.FirstTick+" nativeTick="+plan.NativeTick+" player="+pose.position+" velocity="+pose.velocity+" dir="+pose.direction+" grav="+pose.gravDir+" quiver="+pose.magicQuiver+" harpy="+pose.accHarpyCharm+" archery="+pose.archery+" wind="+Main.windPhysics+"/"+Main.windPhysicsStrength+" receiver="+Receiver(npc)+" legalOrdinal0=["+Scalar(representative)+"]");
                audit=new Harmony("JueMingR.Tests.SkySingleStateWitness");observing=this;
                try{audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSkyChecks),nameof(Killing)));audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSkyChecks),nameof(Damaging)),finalizer:new HarmonyMethod(typeof(NativeCombatSkyChecks),nameof(Damaged)));}
                catch{Dispose();throw;}
            }
            internal void Births(Projectile[] shots,string label)
            {
                stage=label;arrows=shots.Select(q=>new Arrow{Body=q,Key=q.key,Birth=Scalar(q),Timeline=plan.Timeline,FirstTick=plan.FirstTick}).ToList();
                foreach(var entry in arrows)Console.WriteLine("SKY-WITNESS birth stage="+stage+" "+entry.Birth);
                if(label!="natural")return;
                bool receipt=(bool)Get(attack,"hasConsumedInput");var input=Get(attack,"consumedInput");var kind=Get(input,"Kind").ToString();var point=(Vector2)Get(input,"Point");
                consumedMatches=receipt && kind=="Enhanced" && Vector2.DistanceSquared(point,plan.Point)<.0001f && Equals(Get(input,"Target"),plan.Timeline.Identity) && (uint)Get(attack,"consumedInputStep")==Main.GameUpdateCount;
                Console.WriteLine("SKY-WITNESS consumed receipt="+receipt+" kind="+kind+" point="+point+" target="+Get(input,"Target")+" phase="+Get(input,"Phase")+" step="+Get(attack,"consumedInputStep")+" samePreparedPoint="+consumedMatches);
            }
            internal void At(int value){tick=value;}
            internal void After(Projectile q)
            {
                var entry=Find(q);if(entry==null)return;entry.Updates++;float gap=Gap(q.Hitbox,target.Hitbox);var path=entry.Timeline;var b=path[Math.Min(entry.FirstTick+entry.Updates-1,path.Count-1)].ProjectileReceiveBounds;
                float future=Gap(q.Hitbox,new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height));
                if(gap<entry.Gap){entry.Gap=gap;entry.NearTick=tick;entry.Near="shot="+q.Hitbox+" target="+target.Hitbox+" future="+b.X+","+b.Y+","+b.Width+","+b.Height+" "+Receiver(target);}
                entry.FutureGap=Math.Min(entry.FutureGap,future);entry.Last=Scalar(q);if(!q.active && entry.Exit==0)entry.Exit=tick;
            }
            private Arrow Find(Projectile p){return arrows.FirstOrDefault(a=>ReferenceEquals(a.Body,p) && a.Key.Equals(p.key));}
            private void Summary(int first)
            {foreach(var a in arrows)Console.WriteLine("SKY-WITNESS result stage="+stage+" hand="+a.Hand+" bornWorld="+a.BornAt+" updates="+a.Updates+" birth=["+a.Birth+"] firstDamage="+first+" firstDamageWorld="+a.FirstDamageWorld+" firstDamageAge="+a.FirstDamageAge+" damageCalls="+a.DamageCalls+" damageEvents="+a.DamageEvents+" actualLifeLoss="+a.Damage+" nearestGapSquared="+a.Gap+" futureGapSquared="+a.FutureGap+" nearTick="+a.NearTick+" near=["+a.Near+"] exit="+a.Exit+" killTick="+a.KillTick+" kill=["+a.Kill+"] last=["+a.Last+"]");}
            internal void Complete(int naturalDamage,object context)
            {
                Summary(naturalDamage);var natural=arrows;var naturalTarget=target;
                var players=Main.player;var npcs=Main.npc;var projectiles=Main.projectile;var savedRandom=Main.rand;uint savedTick=Main.GameUpdateCount;int loop=Main.ProjectileUpdateLoopIndex;
                var owner=players[pose.whoAmI];string ownerState=OwnerState(owner),targetState=Receiver(target)+" position="+target.position;var ownerCounts=(int[])owner.ownedProjectileCounts.Clone();var immunity=(int[])target.immune.Clone();
                var scratch=Scratch();var dust=Main.dust;float dustCount=Dust.dCount;int legalDamage=0;
                try
                {
                    Main.player=(Player[])players.Clone();Main.player[pose.whoAmI]=CopyPlayer(pose);
                    Main.npc=Enumerable.Range(0,npcs.Length).Select(i=>new NPC{whoAmI=i}).ToArray();target=CopyNpc(receiver);Main.npc[receiver.whoAmI]=target;
                    Main.projectile=Enumerable.Range(0,projectiles.Length).Select(i=>new Projectile{whoAmI=i}).ToArray();var q=representative;q.whoAmI=natural[0].Body.whoAmI;q.key=natural[0].Body.key;Main.projectile[q.whoAmI]=q;
                    Main.dust=Enumerable.Range(0,dust.Length).Select(i=>new Dust()).ToArray();Main.rand=CopyRandom(random);SetTick(preparedTick);Births(new[]{q},"legal-ordinal0");int first=0;
                    for(int i=1;i<=120;i++){NativeQuickItemChecks.BeginWorldStep();target.position.X=1100+i;if(target.immune[0]>0)target.immune[0]--;At(i);q.Update(q.whoAmI);After(q);if(target.life<10000){first=i;break;}if(!q.active)break;}
                    legalDamage=first;Summary(first);Console.WriteLine("SKY-WITNESS independent legalOrdinal0 nativeFirstDamage="+first+" expected="+plan.NativeTick+" matched="+(first==plan.NativeTick)+" naturalFirstDamage="+naturalDamage+" consumedMatches="+consumedMatches+" scope=private-native-motor-and-Damage; natural-seed-unchanged");
                }
                finally
                {
                    Main.player=players;Main.npc=npcs;Main.projectile=projectiles;Main.dust=dust;Dust.dCount=dustCount;Main.rand=savedRandom;SetTick(savedTick);Main.ProjectileUpdateLoopIndex=loop;RestoreScratch(scratch);arrows=natural;target=naturalTarget;stage="natural";
                    Require(ReferenceEquals(Main.player,players) && ReferenceEquals(Main.npc,npcs) && ReferenceEquals(Main.projectile,projectiles) && ReferenceEquals(Main.dust,dust) && Dust.dCount==dustCount && ReferenceEquals(Main.rand,savedRandom) && Main.GameUpdateCount==savedTick && OwnerState(owner)==ownerState && ownerCounts.SequenceEqual(owner.ownedProjectileCounts) && Receiver(target)+" position="+target.position==targetState && immunity.SequenceEqual(target.immune),"independent sky witness restores native pools/dust/RNG/clock and preserves original actor life/immune/input/ammo/counts");
                }
                Require(consumedMatches,"actual sky enhanced consumer uses the same prepared Point/target/step");
                Require(naturalDamage==0 && natural.Count==3 && natural.All(a=>a.Updates==120 && a.Damage==0 && a.Gap>0 && a.FutureGap>0),"original seed7123 single group remains its observed 0/3 geometric miss within 120 updates");
                Require(plan.NativeTick==43 && legalDamage==43,"same-point legal ordinal0 retains its independent real native first Damage43");
                FiniteWindow(context);
            }
            // Predeclared nine native Shoot opportunities, not nine retries
            // until one hits. Each arrow receives at most120 updates; frozen
            // tails cannot contribute late Damage. This direct-Shoot component
            // does not claim Player.Update admitted an automatic attack cadence.
            private void FiniteWindow(object context)
            {
                const int hands=9,interval=19,perArrow=120,worlds=(hands-1)*interval+perArrow;
                var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var input=Get(context,"Input");var owner=Main.player[pose.whoAmI];var ownerBefore=(Player)clone.Invoke(owner,null);
                var npcs=Main.npc;var projectiles=Main.projectile;var dust=Main.dust;float dustCount=Dust.dCount;var rng=Main.rand;uint clock=Main.GameUpdateCount;int loop=Main.ProjectileUpdateLoopIndex,mx=Main.mouseX,my=Main.mouseY;var scratch=Scratch();var priorArrows=arrows;var priorTarget=target;
                var keyField=typeof(Projectile).GetField("keyToIndex",Flags);var keyMap=(Array)keyField.GetValue(null);var savedKeys=(Array)keyMap.Clone();
                int attempted=0,admitted=0,shoots=0,consumed=0,first=0,totalDamage=0,events=0;
                try
                {
                    InstallPlayer(CopyPlayer(pose),owner);Main.npc=Enumerable.Range(0,npcs.Length).Select(i=>new NPC{whoAmI=i}).ToArray();target=CopyNpc(receiver);Main.npc[target.whoAmI]=target;
                    Main.projectile=Enumerable.Range(0,projectiles.Length).Select(i=>new Projectile{whoAmI=i}).ToArray();Main.dust=Enumerable.Range(0,dust.Length).Select(i=>new Dust()).ToArray();Main.rand=CopyRandom(random);SetTick(preparedTick);arrows=new List<Arrow>();stage="finite-natural-window";
                    for(int world=0;world<worlds;world++)
                    {
                        // At launch world19 the receiver is still the previous
                        // completed X1119. Point1 is X1120 after the next native
                        // displacement, exactly as the original single hand.
                        if(world%interval==0 && attempted<hands)
                        {
                            attempted++;NativeToolExecutionChecks.Sample(context,input,new Vector2(650,850),true);Call(combat,"Sample");
                            int stack=owner.inventory[54].stack,cycling=owner.ammoCyclingOffset;var prepareRandom=CopyRandom(Main.rand);
                            NativeCombatAimChecks.PrepareMechanismFixture(host,attack,target,1,phaseName:"BeforeNpc");var solution=NativeCombatAimChecks.RequireSolution(attack,owner,"fixed sky window opportunity has its actual enhanced solution");admitted++;
                            Require(stack==owner.inventory[54].stack && cycling==owner.ammoCyclingOffset && SameRandom(prepareRandom,Main.rand),"finite sky preparation does not consume ammo/cycling/RNG");
                            int physicalX=Main.mouseX,physicalY=Main.mouseY;var known=new HashSet<Terraria.DataStructures.ProjectileKey>(arrows.Select(a=>a.Key));
                            typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(owner,new object[]{owner.whoAmI,owner.HeldItem,owner.GetWeaponDamage(owner.HeldItem),false});shoots++;
                            var packet=Get(attack,"consumedInput");Require((bool)Get(attack,"hasConsumedInput") && Get(packet,"Kind").ToString()=="Enhanced" && Vector2.DistanceSquared((Vector2)Get(packet,"Point"),solution.Point)<.0001f && Equals(Get(packet,"Target"),solution.Timeline.Identity) && (uint)Get(attack,"consumedInputStep")==Main.GameUpdateCount,"each fixed sky hand actually consumes its own Point/identity/step");
                            var born=Main.projectile.Where(q=>q.active && q.owner==owner.whoAmI && !known.Contains(q.key)).ToArray();
                            Require(born.Length>=3 && born.Length<=4 && born.All(q=>q.type==1 && q.damage>0 && q.noDropItem && q.Center.Y<=owner.MountedCenter.Y-590 && q.velocity.Y>0) && Main.mouseX==physicalX && Main.mouseY==physicalY,"fixed sky hand retains native count/type/damage/sky birth/downward motion/noDrop and physical cursor");
                            int spent=stack-owner.inventory[54].stack;Require(spent>=0 && spent<=1,"each actual sky Shoot preserves native ammo conservation range");consumed+=spent;
                            foreach(var q in born)arrows.Add(new Arrow{Body=q,Key=q.key,Birth=Scalar(q),Hand=attempted,BornAt=world,Timeline=solution.Timeline,FirstTick=solution.FirstTick});
                            Console.WriteLine("SKY-WINDOW hand="+attempted+" world="+world+" guc="+Main.GameUpdateCount+" sample="+solution.Timeline.SampleTick+" point="+solution.Point+" first="+solution.FirstTick+" receive="+solution.ReceiveTick+" nativeTick="+solution.NativeTick+" births="+born.Length+" ammoSpent="+spent+" scope=finite-direct-Shoot");
                        }
                        NativeQuickItemChecks.BeginWorldStep();target.position.X=1100+world+1;if(target.immune[0]>0)target.immune[0]--;At(world+1);
                        foreach(var a in arrows.Where(a=>a.Body.active && a.Key.Equals(a.Body.key) && a.Updates<perArrow).ToArray()){Main.ProjectileUpdateLoopIndex=a.Body.whoAmI;a.Body.Update(a.Body.whoAmI);After(a.Body);}
                    }
                    totalDamage=arrows.Sum(a=>a.Damage);events=arrows.Sum(a=>a.DamageEvents);first=arrows.Where(a=>a.FirstDamageWorld>0).Select(a=>a.FirstDamageWorld).DefaultIfEmpty(0).Min();Summary(first);
                    Console.WriteLine("SKY-WINDOW result seed=7123 initializations=1 plannedHands="+hands+" interval="+interval+" worlds="+worlds+" perArrowLimit="+perArrow+" attempted="+attempted+" admitted="+admitted+" actualShoot="+shoots+" births="+arrows.Count+" damagingArrows="+arrows.Count(a=>a.Damage>0)+" damageEvents="+events+" actualLifeLoss="+totalDamage+" receiverLifeLoss="+(10000-target.life)+" firstDamageWorld="+first+" ammoSpent="+consumed+" scope=finite-direct-Shoot; legal-representative-excluded");
                    Require(attempted==hands && admitted==hands && shoots==hands && arrows.All(a=>a.Updates<=perArrow) && totalDamage==10000-target.life && owner.inventory[54].stack==999-consumed,"fixed finite sky denominators and resource/damage accounting remain exact");
                    Require(first>0 && events>0 && totalDamage>0,"predeclared nine-hand natural primary-arrow window has at least one real native Damage without representative substitution");
                }
                finally
                {
                    InstallPlayer(ownerBefore,owner);Main.npc=npcs;Main.projectile=projectiles;Main.dust=dust;Dust.dCount=dustCount;Main.rand=rng;SetTick(clock);Main.ProjectileUpdateLoopIndex=loop;Main.mouseX=mx;Main.mouseY=my;RestoreScratch(scratch);Array.Copy(savedKeys,keyMap,keyMap.Length);keyField.SetValue(null,keyMap);arrows=priorArrows;target=priorTarget;stage="natural";
                    // Return lookup mappings for the restored real pool; keep
                    // native slot generations monotonic after actual births.
                    // Reusing consumed generation numbers could alias history.
                    // Do not resurrect an old input sample or a private-target
                    // capability after returning its actor pools. New cases
                    // must sample and prepare through their normal fixture.
                    Call(attack,"ClearExecution");Call(input,"BeginUpdate");
                    Require(ReferenceEquals(Main.npc,npcs) && ReferenceEquals(Main.projectile,projectiles) && ReferenceEquals(Main.dust,dust) && Dust.dCount==dustCount && ReferenceEquals(Main.rand,rng) && Main.GameUpdateCount==clock && ReferenceEquals(owner.inventory,ownerBefore.inventory),"finite sky window returns pools/dust/RNG/clock and original owner inventory reference");
                }
            }
            private static void InstallPlayer(Player source,Player destination)
            {for(Type type=typeof(Player);type!=null;type=type.BaseType)foreach(var field in type.GetFields(Flags|BindingFlags.DeclaredOnly))if(!field.IsStatic && !field.IsInitOnly && !field.IsLiteral)field.SetValue(destination,field.GetValue(source));}
            private static void SetTick(uint value){typeof(Main).GetField("_gameUpdateCount",Flags).SetValue(null,value);}
            private static float Gap(Rectangle a,Rectangle b){float x=Math.Max(0,Math.Max(a.Left-b.Right,b.Left-a.Right)),y=Math.Max(0,Math.Max(a.Top-b.Bottom,b.Top-a.Bottom));return x*x+y*y;}
            private static string Receiver(NPC n){return "type="+n.type+" active="+n.active+" friendly="+n.friendly+" immortal="+n.immortal+" dontTakeDamage="+n.dontTakeDamage+" life="+n.life+" defense="+n.defense+" immune0="+n.immune[0]+" hitbox="+n.Hitbox;}
            private static string Scalar(Projectile q){return "slot="+q.whoAmI+" key="+q.key+" type="+q.type+" owner="+q.owner+" center="+q.Center+" velocity="+q.velocity+" damage="+q.damage+" friendly="+q.friendly+" active="+q.active+" updates="+(q.extraUpdates+1)+" left="+q.timeLeft+" tile="+q.tileCollide+" penetrate="+q.penetrate+" ai="+string.Join(",",q.ai);}
            // Native Damage writes owner counts and NPC playerInteraction in
            // addition to life/immune. Detach actor arrays before the private
            // run; this is a bounded wooden-arrow oracle, not a world serializer.
            private static T ActorCopy<T>(T value) where T:class
            {var copy=(T)clone.Invoke(value,null);for(Type type=value.GetType();type!=null;type=type.BaseType)foreach(var f in type.GetFields(Flags|BindingFlags.DeclaredOnly))if(!f.IsStatic && f.FieldType.IsArray){var a=f.GetValue(value) as Array;if(a!=null)f.SetValue(copy,a.Clone());}return copy;}
            private static Player CopyPlayer(Player p){var q=ActorCopy(p);q.inventory=p.inventory.Select(i=>i.Clone()).ToArray();q.armor=p.armor.Select(i=>i.Clone()).ToArray();return q;}
            private static NPC CopyNpc(NPC n){return ActorCopy(n);}
            private static Terraria.Utilities.UnifiedRandom CopyRandom(Terraria.Utilities.UnifiedRandom r){var q=(Terraria.Utilities.UnifiedRandom)clone.Invoke(r,null);foreach(var f in r.GetType().GetFields(Flags))if(!f.IsStatic && f.FieldType.IsArray){var a=f.GetValue(r) as Array;if(a!=null)f.SetValue(q,a.Clone());}return q;}
            private static bool SameRandom(object a,object b){return a.GetType().GetFields(Flags).Where(f=>!f.IsStatic).All(f=>{var x=f.GetValue(a);var y=f.GetValue(b);return x is Array && y is Array?((Array)x).Cast<object>().SequenceEqual(((Array)y).Cast<object>()):Equals(x,y);});}
            private static string OwnerState(Player p){return p.position+"/"+p.velocity+"/"+p.statLife+"/"+p.statMana+"/"+p.direction+"/"+p.itemTime+"/"+p.itemAnimation+"/"+p.itemRotation+"/"+p.controlUseItem+"/"+p.channel+"/"+p.inventory[54].type+"/"+p.inventory[54].stack;}
            private static Dictionary<FieldInfo,object> Scratch()
            {return typeof(Collision).GetFields(Flags).Where(f=>f.IsStatic && !f.IsLiteral && !f.IsInitOnly && (f.FieldType.IsPrimitive || typeof(System.Collections.IList).IsAssignableFrom(f.FieldType))).ToDictionary(f=>f,f=>{var value=f.GetValue(null);var list=value as System.Collections.IList;return list==null?value:(object)Tuple.Create(value,list.Cast<object>().ToArray());});}
            private static void RestoreScratch(Dictionary<FieldInfo,object> values)
            {foreach(var value in values){var list=value.Value as Tuple<object,object[]>;if(list==null)value.Key.SetValue(null,value.Value);else{var target=(System.Collections.IList)list.Item1;target.Clear();foreach(var item in list.Item2)target.Add(item);value.Key.SetValue(null,target);}}}
            public void Dispose(){try{audit?.UnpatchAll(audit.Id);}finally{if(ReferenceEquals(observing,this))observing=null;}if(audit!=null)Require(!Harmony.GetAllPatchedMethods().Any(m=>Harmony.GetPatchInfo(m).Owners.Contains(audit.Id)),"temporary sky observer Harmony owner is removed");}
            internal void Kill(Projectile q){var a=Find(q);if(a==null)return;a.KillTick=tick;a.Kill=Scalar(q)+" caller="+new System.Diagnostics.StackTrace(false).ToString().Replace(Environment.NewLine," | ");}
            internal void DamageBefore(Projectile q,out int life){var a=Find(q);life=target.life;if(a!=null)a.DamageCalls++;}
            internal void DamageAfter(Projectile q,int life){var a=Find(q);if(a==null)return;int loss=Math.Max(0,life-target.life);a.Damage+=loss;if(loss>0){a.DamageEvents++;if(a.FirstDamageWorld==0){a.FirstDamageWorld=tick;a.FirstDamageAge=a.Updates+1;}}}
        }
        private static void Killing(Projectile __instance){observing?.Kill(__instance);}
        private static void Damaging(Projectile __instance,out int __state){__state=0;observing?.DamageBefore(__instance,out __state);}
        private static void Damaged(Projectile __instance,int __state){observing?.DamageAfter(__instance,__state);}
    }
}
