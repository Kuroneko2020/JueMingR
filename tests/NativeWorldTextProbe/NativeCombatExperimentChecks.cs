using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Real fixed-native consumers in an isolated component host. No desktop
    // gameplay, server authority, FPS or owner acceptance is inferred.
    internal static class NativeCombatExperimentChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static void Run(object context)
        {
            NativeCombatAimChecks.Initialize();NativeCombatExperimentHotkeyChecks.Reveal(context);
            var owner=Get(context,"CombatExperiments");var host=Get(context,"CombatObservation");
            var combat=Get(context,"Combat");var tools=Get(context,"Tools");var input=Get(context,"Input");
            var ready=(bool[])Get(owner,"ready");var errors=(Exception[])Get(owner,"errors");
            for(int i=0;i<3;i++)Require(ready[i],"independent native experiment hook "+i+" installed: "+errors[i]);
            Save(owner,0);NativeCombatObservationChecks.Save(host,new ObservationOptions());
            var nativeOutlets=new Harmony("JueMingR.Tests.ExperimentNativeOutlets");
            try
            {
                // Reuse the verified Usage/Yoyo CPU fixture's achievement-only
                // outlets. Main.Achievements has no desktop host here; retain
                // all original Player.Update movement/input/ItemCheck stages.
                foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})
                    nativeOutlets.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
                var failures=new List<Exception>();
                Action<string,Action> run=(name,check)=>
                {
                    // Each group below starts with its existing fresh native
                    // Reset. Retire preferences between groups through their
                    // actual owners; never repair a failing group mid-chain.
                    Save(owner,0);NativeCombatObservationChecks.Save(host,new ObservationOptions());
                    NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                    try{check();Console.WriteLine("PASS EXPERIMENT GROUP "+name);}
                    catch(Exception error) when(JueMingR.Testing.BusinessAssertionProtocol.IsAssertion(error))
                    {
                        failures.Add(error);Console.WriteLine("FAIL EXPERIMENT GROUP "+name+": "+error);
                        // Only an ordinary assertion with fully returned native
                        // scopes can expose an independent later obligation.
                        // An unknown exception or a leaked scope stops this leaf.
                        var cursor=owner.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.CombatCursorScope",true);
                        if(GetOptional(owner,"worldOwner")!=null || (int)Get(owner,"worldDepth")!=0 ||
                            cursor.GetField("owner",Flags).GetValue(null)!=null || Main.ProjectileUpdateLoopIndex!=-1 ||
                            Main.LocalPlayer.dead || Main.LocalPlayer.ghost || !(bool)Get(Get(owner,"Settings"),"Ready"))
                            throw new InvalidOperationException("Experiment group did not return a clean independent native fixture: "+name,error);
                    }
                };
                run("hard-lock",()=>HardLock(context,owner,host,tools,input));
                run("movement",()=>Movement(context,owner,tools,input));
                run("summon-attack",()=>SummonAttack(context,owner,tools,input));
                run("nested-attack-gates",()=>NestedAttackGates(context,owner,tools,input));
                run("beams",()=>Beams(context,owner,tools,input));
                run("timings",()=>Timings(context,owner,tools,input));
                run("revolver",()=>Revolver(context,owner,tools,input));
                run("melee",()=>MeleePeriod(context,owner,tools,input));
                run("path-consumer",()=>PathConsumer(context,owner,host,tools,input));
                run("yoyo",()=>YoyoWallInput(context,owner,host,combat,tools,input));
                run("ground-summon",()=>GroundSummonAttack(context,owner,tools,input));
                run("prediction-boundary",()=>PredictionBoundary(context,owner,host,combat,tools,input));
                run("prism-source",()=>PrismSource(context,owner,host,combat,tools,input));
                if(failures.Count>0)
                {
                    Console.WriteLine("EXPERIMENT GROUP FAILURES="+failures.Count+"; original assertions and stacks retained above.");
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
                }
                Console.WriteLine("PASS hidden experiments: actual native steering, terrain, beam, timing and basic prediction boundary; isolated components.");
            }
            finally{nativeOutlets.UnpatchAll(nativeOutlets.Id);Save(owner,0);NativeCombatObservationChecks.Save(host,new ObservationOptions());foreach(var p in Main.projectile)p.active=false;}
        }
        private static void Save(object owner,int mask)
        {
            var settings=(CombatExperimentSettings)Get(owner,"Settings");
            NativeQuickItemChecks.Until(()=>{Call(owner,"Poll");return settings.Ready;});
            Require(settings.Set(new CombatExperimentOptions(mask)),"experiment preference command accepted");
            NativeQuickItemChecks.Until(()=>{Call(owner,"Poll");return !settings.Busy;});
            Require(settings.CompletionSucceeded,"experiment preference committed: "+settings.Message);
        }
        private static Projectile Shot(int type,Vector2 position,Vector2 velocity)
        {
            // Use the real factory: defaults alone have zero damage and no
            // registered ProjectileKey, so cannot represent a live attack.
            int slot=Projectile.NewProjectile(Main.LocalPlayer.GetProjectileSource_Item(Main.LocalPlayer.HeldItem),position,velocity,type,25,0,Main.myPlayer);
            var p=Main.projectile[slot];p.position=position;return p;
        }
        private static void World(object owner,Action run)
        {
            var receipt=Call(owner,"BeginWorld");
            try{run();}finally{owner.GetType().GetMethod("EndWorld",Flags).Invoke(null,new[]{receipt});}
        }
        private static void HardLock(object context,object owner,object host,object tools,object input)
        {
            Save(owner,0);var player=NativeToolExecutionChecks.Reset(context,tools,input,95,0,0);
            player.position=new Vector2(580,646);var target=Main.npc[2];target.SetDefaults(3);target.whoAmI=2;target.active=true;target.Center=new Vector2(850,540);target.life=10000;
            Save(owner,1);Call(host,"Update",Main.GameUpdateCount);
            Require(!((ObservationOptions)Get(host,"Options")).Aim,"hard lock does not enable saved Aim");
            var p=Shot(14,new Vector2(600,600),new Vector2(12,0));float speed=p.velocity.Length();
            p.AI();Require(p.velocity.Y==0,"out-of-live-phase native AI retains ordinary velocity");
            World(owner,()=>p.AI());Require(p.velocity.Y<0 && Math.Abs(p.velocity.Length()-speed)<.001f,"existing real bullet turns after native AI without speed loss; mask="+Get(owner,"EffectiveMask")+" damage="+p.damage+" selected="+Get(Get(host,"Selection"),"HasTarget")+" velocity="+p.velocity);
            var clone=(Projectile)typeof(object).GetMethod("MemberwiseClone",Flags).Invoke(p,null);clone.velocity=new Vector2(12,0);
            World(owner,()=>clone.AI());Require(clone.velocity.Y==0,"same owner/slot/key clone cannot obtain a live write");
            p.owner=1;p.velocity=new Vector2(12,0);World(owner,()=>p.AI());Require(p.velocity.Y==0,"remote native object cannot obtain steering");
            p.owner=0;
            // AI208 charge belongs to native held input even with Aim disabled.
            player.channel=true;player.HeldItem.SetDefaults(6153);Main.mouseX=0;Main.mouseY=100;
            var charge=Shot(1115,player.MountedCenter,new Vector2(12,0));charge.ai[0]=3;charge.ai[1]=0;
            World(owner,()=>charge.AI());var charging=charge.velocity;
            Require(Vector2.Dot(charging.SafeNormalize(Vector2.UnitX),(target.Center-charge.Center).SafeNormalize(Vector2.UnitX))<.5f,"hard lock does not retarget native glacier held charge with Aim off");
            NativeToolExecutionChecks.Sample(context,input,new Vector2(300,500),false);player.controlUseItem=false;player.ItemCheck();
            Require(!player.channel,"actual ItemCheck observes physical release of glacier charge");World(owner,()=>charge.AI());
            Require(charge.active && charge.ai[1]==1 && Vector2.Dot(charge.velocity.SafeNormalize(Vector2.UnitX),(target.Center-charge.Center).SafeNormalize(Vector2.UnitX))>.99f,"released glacier free flight joins hard lock");
            Save(owner,0);p.velocity=new Vector2(12,0);World(owner,()=>p.AI());Require(p.velocity.Y==0,"OFF native consumer does not steer");
        }
        private static void Movement(object context,object owner,object tools,object input)
        {
            Save(owner,0);var player=NativeToolExecutionChecks.Reset(context,tools,input,95,0,0);player.position=new Vector2(580,646);
            for(int y=30;y<43;y++)NativeToolsChecks.Tile(40,y,TileID.Stone);
            var ordinary=Shot(14,new Vector2(600,646),new Vector2(80,0));for(int i=0;i<8 && ordinary.active;i++)ordinary.HandleMovement(ordinary.velocity);
            Require(!ordinary.active || ordinary.position.X<640,"vanilla real bullet cannot pass solid wall");
            Save(owner,4);var p=Shot(14,new Vector2(600,646),new Vector2(80,0));
            float nativeBirthSpeed=p.velocity.Length();World(owner,()=>{for(int i=0;i<8 && p.active;i++)p.HandleMovement(p.velocity);});
            Console.WriteLine("EXPERIMENT MOVEMENT nativeBirthSpeed="+nativeBirthSpeed+" eightMovementConsumers position="+p.position+" active="+p.active);
            Require(p.active && p.position.X>=680 && p.tileCollide,"real movement passes wall with native tileCollide field intact");
            var target=Main.npc[2];target.SetDefaults(3);target.whoAmI=2;target.active=true;target.Center=new Vector2(800,650);target.life=10000;
            p.Center=new Vector2(800,650);p.ownerHitCheck=true;p.ownerHitCheckDistance=1000;
            Require(!p.CanHitWithMeleeWeapon(target),"unscoped owner LOS remains blocked");
            World(owner,()=>Require(p.CanHitWithMeleeWeapon(target),"actual owner-hit consumer bypasses terrain"));
            p.ownerHitCheckDistance=10;target.position.X+=100;
            World(owner,()=>Require(!p.CanHitWithMeleeWeapon(target),"owner-hit distance remains authoritative"));
            Require(!Collision.CanHitLine(player.Center,1,1,target.Center,1,1),"global terrain geometry remains blocked");
            var slime=Shot(266,new Vector2(610,650),new Vector2(64,8));Require(slime.minion,"native baby slime is a minion body");slime.position.Y=43*16-slime.height-2;float floor=43*16-slime.height;
            World(owner,()=>slime.HandleMovement(slime.velocity));Require(slime.active && slime.position.X>=670 && slime.position.Y<=floor+.01f,"walking summon passes wall while retaining floor support");
            var hook=Shot(13,new Vector2(600,600),new Vector2(80,0));World(owner,()=>Require(!(bool)Call(owner,"Wall",hook),"noncombat hook is not granted terrain bypass"));
            Save(owner,0);p=Shot(14,new Vector2(600,646),new Vector2(80,0));World(owner,()=>{for(int i=0;i<8 && p.active;i++)p.HandleMovement(p.velocity);});Require(!p.active || p.position.X<640,"OFF next native movement restores wall behavior");
        }
        private static void Beams(object context,object owner,object tools,object input)
        {
            Save(owner,0);NativeToolExecutionChecks.Reset(context,tools,input,3541,0,0);
            for(int y=0;y<Main.maxTilesY;y++)NativeToolsChecks.Tile(50,y,TileID.Stone);
            var cosmetic=new Harmony("JueMingR.Tests.ExperimentBeamRipple");
            cosmetic.Patch(typeof(Projectile).GetMethod("AI"),transpiler:new HarmonyMethod(typeof(NativeCombatBeamChecks).GetMethod("RippleOutlet",Flags)));
            try
            {
                foreach(int parentType in new[]{633,460})
                {
                    var parent=Shot(parentType,new Vector2(600,600),Vector2.UnitX);parent.ai[0]=200;
                    var beam=Shot(parentType==633?632:461,new Vector2(600,600),Vector2.UnitX);beam.ai[0]=2;beam.ai[1]=(float)parent.key;
                    for(int i=0;i<12;i++)beam.AI();float blocked=beam.localAI[1];Require(beam.active && blocked<600,"native beam scan is clipped by terrain: "+beam.type);
                    Save(owner,4);World(owner,()=>{for(int i=0;i<12;i++)beam.AI();});Require(beam.active && beam.localAI[1]>2300,"actual native beam consumes original maximum length: "+beam.type);
                    Save(owner,0);for(int i=0;i<12;i++)beam.AI();Require(beam.localAI[1]<600,"native beam restores terrain after OFF");
                }
            }
            finally{cosmetic.UnpatchAll(cosmetic.Id);}
        }
        private static Projectile CastSummon(object context,object owner,object tools,object input,int itemType,Vector2 cursor)
        {
            var player=NativeToolExecutionChecks.Reset(context,tools,input,itemType,0,0);player.position=new Vector2(640,688-player.height);player.releaseUseItem=true;player.reuseDelay=0;
            player.statMana=player.statManaMax=player.statManaMax2=200;int projectileType=player.HeldItem.shoot;
            NativeToolExecutionChecks.Sample(context,input,cursor,true);NativeQuickItemChecks.NativeFrame(player);
            var body=Main.projectile.SingleOrDefault(p=>p.active && p.owner==0 && p.type==projectileType);
            Require(body!=null && body.originalDamage>0,"actual summon ItemCheck supplies its native birth and unscaled originalDamage: "+itemType);
            player.inventory[1].SetDefaults(ItemID.CopperPickaxe);player.selectedItemState.Select(1);
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");
            // Actual buffered selection finishes this native use; no timer,
            // minion bookkeeping, buff or source field is manufactured.
            for(int i=0;i<60 && player.selectedItem!=1;i++)Step(context,owner,host,combat,input,cursor,false);
            Require(player.selectedItem==1 && player.HeldItem.type==ItemID.CopperPickaxe,"summon owner naturally switches to a tool after the cast");
            return body;
        }
        private static void SummonAttack(object context,object owner,object tools,object input)
        {
            Save(owner,0);var sentry=CastSummon(context,owner,tools,input,ItemID.StaffoftheFrostHydra,new Vector2(650,600));var player=Main.LocalPlayer;
            Require(sentry.Bottom.Y<=688.01f,"native sentry landing-spot birth has legal floor support");
            for(int y=30;y<43;y++)NativeToolsChecks.Tile(45,y,TileID.Stone);
            var target=Main.npc[2];target.SetDefaults(3);target.whoAmI=2;target.active=true;target.Center=new Vector2(820,620);target.life=10000;target.noGravity=true;target.aiStyle=-1;
            int slot;float x,yAim;
            Require(!sentry.AI_053_HandleSentryNPCTargeting(Vector2.Zero,false,out slot,out x,out yAim),"native sentry targeting is blocked by wall");
            Save(owner,4);World(owner,()=>Require(sentry.AI_053_HandleSentryNPCTargeting(Vector2.Zero,false,out slot,out x,out yAim) && slot==2,"native sentry targeting actually sees wall-hidden enemy"));
            // Retain the bounded 121-update observation; the real cast and
            // native appearance cooldown have already advanced during selection.
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");
            for(int tick=0;tick<=120 && !Main.projectile.Any(p=>p.active && p.owner==0 && p.type==309);tick++)
                Step(context,owner,host,combat,input,new Vector2(650,600),false);
            World(owner,()=>HydraFacts("native-cast-complete",owner,sentry,target));
            Require(Main.projectile.Any(p=>p.active && p.owner==0 && p.type==309),"actual ground sentry AI fires its native attack toward the wall-hidden receiver");
            var imp=Shot(ProjectileID.FlyingImp,new Vector2(650,600),Vector2.Zero);player.impMinion=true;
            World(owner,()=>{for(int tick=0;tick<180;tick++)imp.AI();});
            Require(Main.projectile.Any(p=>p.active && p.owner==0 && p.type==ProjectileID.ImpFireball),"actual flying summon AI emits attack behind wall while owner holds a pick");
        }
        private static void HydraFacts(string stage,object owner,Projectile sentry,NPC target)
        {
            int selected;float x,y;bool acquired=sentry.AI_053_HandleSentryNPCTargeting(Vector2.Zero,false,out selected,out x,out y);
            Console.WriteLine("HYDRA FACTS "+stage+" active="+sentry.active+" type="+sentry.type+" style="+sentry.aiStyle+" key="+sentry.key+" slot="+sentry.whoAmI+
                " numUpdates="+sentry.numUpdates+" extraUpdates="+sentry.extraUpdates+" ai="+string.Join(",",sentry.ai)+" local="+string.Join(",",sentry.localAI)+
                " pos="+sentry.position+" size="+sentry.Size+" velocity="+sentry.velocity+" tileCollide="+sentry.tileCollide+" sentry="+sentry.sentry+
                " damage="+sentry.damage+" originalDamage="+sentry.originalDamage+" lifetime="+sentry.timeLeft+" mode="+Get(owner,"EffectiveMask")+
                " target="+acquired+"/"+selected+"/"+new Vector2(x,y)+" receiver="+target.active+"/"+target.CanBeChasedBy(sentry)+"/"+target.life+
                " bounds="+Main.leftWorld+","+Main.topWorld+","+Main.rightWorld+","+Main.bottomWorld);
        }
        private static void NestedAttackGates(object context,object owner,object tools,object input)
        {
            foreach(int mask in new[]{0,4})
            {
                bool wall=mask!=0;Save(owner,0);var player=NativeToolExecutionChecks.Reset(context,tools,input,95,0,0);player.position=new Vector2(580,646);
                for(int y=20;y<60;y++)NativeToolsChecks.Tile(40,y,TileID.Stone);
                player.itemAnimation=4;player.itemAnimationMax=30;
                var spear=Shot(342,player.MountedCenter,new Vector2(12,0));Save(owner,mask);World(owner,()=>spear.AI());
                Require(Main.projectile.Any(p=>p.active && p.type==343)==wall,"actual North Pole spear terrain gate controls derived 343 birth: "+mask);

                Save(owner,0);player=NativeToolExecutionChecks.Reset(context,tools,input,95,0,0);player.position=new Vector2(580,646);
                for(int x=39;x<57;x++)for(int y=30;y<46;y++)NativeToolsChecks.Tile(x,y,TileID.Stone);
                var edge=Shot(973,new Vector2(648,620),Vector2.UnitX);edge.ai[0]=1;edge.ai[1]=32;edge.ai[2]=1;Save(owner,mask);World(owner,()=>edge.AI());
                Require((edge.damage>0)==wall,"actual True Night's Edge keeps its attack only when terrain gate is bypassed: "+mask);
                var terra=Shot(985,new Vector2(648,620),new Vector2(12,0));terra.ai[0]=1;terra.ai[1]=32;terra.ai[2]=1;World(owner,()=>terra.AI());
                Require((terra.localAI[1]==0)==wall,"actual Terra attack does not enter terrain-stop phase when enabled: "+mask);

                Save(owner,0);player=NativeToolExecutionChecks.Reset(context,tools,input,6153,0,0);player.position=new Vector2(580,646);player.channel=false;
                for(int y=20;y<60;y++)NativeToolsChecks.Tile(40,y,TileID.Stone);
                var glacier=Shot(1115,new Vector2(700,630),new Vector2(12,0));glacier.ai[0]=20;glacier.ai[1]=0;Save(owner,mask);World(owner,()=>glacier.AI());
                Require(glacier.active==wall,"actual released glacier owner LOS kill is bypassed only by through-wall: "+mask);

                Save(owner,0);player=NativeToolExecutionChecks.Reset(context,tools,input,95,0,0);
                var aura=Shot(688,new Vector2(650,640),Vector2.Zero);aura.AI();
                for(int y=20;y<44;y++)NativeToolsChecks.Tile(43,y,TileID.Stone);
                var receiver=new Rectangle(730,620,20,25);NativeToolsChecks.Tile(receiver.Center.X/16,receiver.Center.Y/16,TileID.Stone);
                Save(owner,mask);World(owner,()=>Require(aura.Colliding(aura.Hitbox,receiver)==wall,"real Lightning Aura collision consumes nested terrain/solid-point gate: "+mask));
                var outside=new Rectangle((int)aura.Center.X+400,(int)aura.Center.Y,20,25);World(owner,()=>Require(!aura.Colliding(aura.Hitbox,outside),"Lightning Aura original geometry and range remain authoritative"));

                Save(owner,0);player=NativeToolExecutionChecks.Reset(context,tools,input,95,0,0);
                for(int y=20;y<60;y++)NativeToolsChecks.Tile(43,y,TileID.Stone);
                var nut=Shot(483,new Vector2(600,650),new Vector2(2,0));var target=Main.npc[2];target.SetDefaults(3);target.whoAmI=2;target.active=true;target.life=10000;target.Center=nut.Center+new Vector2(205,-37);
                Save(owner,mask);World(owner,()=>nut.Kill());
                var children=Main.projectile.Where(p=>p.active && p.type==484).ToArray();Require(children.Length>=4 && children.Length<=7,"Seedler preserves its native burst count");
                int directed=children.Count(p=>Math.Abs(p.velocity.X*37+p.velocity.Y*205)<.0001f);
                Require(wall?directed==1:directed==0,"actual Seedler Kill admits the wall-hidden target without changing native child count: "+mask);
            }
        }
        private static void Revolver(object context,object owner,object tools,object input)
        {
            Save(owner,0);var player=NativeToolExecutionChecks.Reset(context,tools,input,2269,0,0);player.inventory[54].SetDefaults(ItemID.MusketBall);player.inventory[54].stack=999;player.releaseUseItem=true;
            Save(owner,2);var seen=new HashSet<int>();var events=new List<int>();int largestCritBonus=0;
            for(int tick=0;tick<21;tick++)
            {
                bool held=tick%5==0;NativeToolExecutionChecks.Sample(context,input,new Vector2(1000,600),held);NativeQuickItemChecks.BeginWorldStep();player.controlUseItem=held;player.ItemCheck();
                if(Main.projectile.Any(p=>p.active && p.owner==0 && seen.Add((int)p.key)))events.Add(tick);
                largestCritBonus=Math.Max(largestCritBonus,player.revolverCritChanceBonus);
            }
            Require(events.SequenceEqual(new[]{0,5,10,15,20}),"Revolver actual press/release ItemCheck uses the five-tick period: "+string.Join(",",events));
            Require(largestCritBonus>0 && !player.HeldItem.autoReuse && 999-player.inventory[54].stack==events.Count,"Revolver retains native release crit accumulation, explicit presses and resource consumption");
            Save(owner,0);
        }
        private static Player meleePlayer;
        private static int meleeTick;
        private static List<int> meleeStarts;
        private static void MeleeStarted(Player __instance)
        {if(ReferenceEquals(__instance,meleePlayer))meleeStarts.Add(meleeTick);}
        private static void MeleePeriod(object context,object owner,object tools,object input)
        {
            Save(owner,0);var player=NativeToolExecutionChecks.Reset(context,tools,input,ItemID.CopperBroadsword,0,0);player.releaseUseItem=true;Save(owner,2);
            var audit=new Harmony("JueMingR.Tests.ExperimentMeleePeriod");var start=typeof(Player).GetMethod("ItemCheck_StartActualUse",Flags);
            meleePlayer=player;meleeStarts=new List<int>();audit.Patch(start,postfix:new HarmonyMethod(typeof(NativeCombatExperimentChecks),nameof(MeleeStarted)));
            try
            {
                for(int tick=0;tick<21;tick++)
                {
                    meleeTick=tick;bool held=tick%5==0;NativeToolExecutionChecks.Sample(context,input,new Vector2(1000,600),held);NativeQuickItemChecks.BeginWorldStep();player.controlUseItem=held;player.ItemCheck();
                }
                Require(meleeStarts.SequenceEqual(new[]{0,5,10,15,20}),"actual complete melee uses repeat every five native ticks: "+string.Join(",",meleeStarts));
            }
            finally{audit.UnpatchAll(audit.Id);meleePlayer=null;meleeStarts=null;Save(owner,0);}
        }
        private static void PathConsumer(object context,object owner,object host,object tools,object input)
        {
            Save(owner,0);NativeCombatObservationChecks.Save(host,new ObservationOptions());var player=NativeToolExecutionChecks.Reset(context,tools,input,ItemID.CopperPickaxe,0,0);
            var target=Main.npc[2];target.SetDefaults(3);target.whoAmI=2;target.active=true;target.Bottom=new Vector2(850,688);target.life=10000;target.target=0;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true));var cache=Get(Get(host,"Prediction"),"Cache");object publication=null;
            for(int i=0;i<16;i++){NativeCombatObservationChecks.Fresh(context,host);publication=Call(cache,"Read",0);if(publication!=null && (int)Get(publication,"Count")>1)break;}
            Require(publication!=null && (int)Get(publication,"Count")>1,"independent Path consumer actually publishes native NPC future");
            Save(owner,4);Require(ReferenceEquals(publication,Call(cache,"Read",0)),"experiment mode edge retains the actual consumer-0 publication");long before=(long)Get(publication,"SampleTick");
            bool advanced=false;for(int i=0;i<16;i++){NativeCombatObservationChecks.Fresh(context,host);publication=Call(cache,"Read",0);if(publication!=null && (long)Get(publication,"SampleTick")>before && (int)Get(publication,"Count")>1){advanced=true;break;}}
            Require(advanced,"independent Path continues actual prediction while original attack future is suspended");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());Save(owner,0);
        }
        private static void YoyoWallInput(object context,object owner,object host,object combat,object tools,object input)
        {
            foreach(int mask in new[]{0,4})
            {
                Save(owner,0);NativeCombatObservationChecks.Save(host,new ObservationOptions());var player=NativeToolExecutionChecks.Reset(context,tools,input,3278,0,0);player.position=new Vector2(700,688-player.height);player.releaseUseItem=true;
                var target=Main.npc[2];target.SetDefaults(3);target.whoAmI=2;target.active=true;target.Center=new Vector2(830,640);target.aiStyle=-1;target.noGravity=true;target.life=target.lifeMax=100000;
                for(int y=20;y<44;y++)NativeToolsChecks.Tile(48,y,TileID.Stone);
                // An enhanced experimental use is current-target Basic. The
                // ordinary side may navigate, but its actual body still cannot
                // cross this tall closed wall in the retained native range.
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,false,false,false,25,false,true));Save(owner,mask);
                float farthestX=float.NegativeInfinity;
                for(int i=0;i<50;i++)
                {
                    Step(context,owner,host,combat,input,new Vector2(500,400));
                    var current=Main.projectile.FirstOrDefault(p=>p.active && p.owner==0 && p.aiStyle==99);
                    if(current!=null)farthestX=Math.Max(farthestX,current.Center.X);
                }
                var ball=Main.projectile.FirstOrDefault(p=>p.active && p.owner==0 && p.aiStyle==99);
                Require(ball!=null,"natural held yoyo remains alive");
                // Native Damage_PVE_Inner gives a struck yoyo a 16px outward
                // recoil. Test actual wall crossing over the same 50 updates,
                // rather than requiring the recoiling last frame at its target.
                Console.WriteLine("YOYO WALL mask="+mask+" farthestX="+farthestX+" final="+ball.Center+" receiverLife="+target.life+" nativeRange="+ProjectileID.Sets.YoyosMaximumRange[ball.type]);
                Require(mask==0?farthestX<768:farthestX>800,"real held yoyo consumes the wall-side or wall-hidden current point: "+mask+" farthestX="+farthestX);
                if(mask!=0)Require(target.life<100000,"through-wall yoyo actually attacks the selected receiver before native recoil");
                if(mask!=0)Require(Vector2.Distance(ball.Center,player.Center)<180,"through-wall yoyo retains native owner reach");
                for(int i=0;i<40 && ball.active;i++)Step(context,owner,host,combat,input,new Vector2(500,400),false);
                Require(!ball.active,"physical yoyo release still completes native recall");
                NativeCombatObservationChecks.Save(host,new ObservationOptions());Save(owner,0);
            }
        }
        private static void GroundSummonAttack(object context,object owner,object tools,object input)
        {
            foreach(int mask in new[]{0,4})
            {
                Save(owner,0);var slime=CastSummon(context,owner,tools,input,ItemID.SlimeStaff,new Vector2(650,672));
                for(int y=20;y<43;y++)NativeToolsChecks.Tile(45,y,TileID.Stone);
                var target=Main.npc[2];target.SetDefaults(3);target.whoAmI=2;target.active=true;target.Bottom=new Vector2(800,688);target.life=target.lifeMax=10000;target.defense=0;target.aiStyle=-1;target.noGravity=true;target.knockBackResist=0;
                Save(owner,mask);var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");
                // Full Player.Update owns per-world minion counts and buff
                // renewal before each actual projectile AI/movement/Damage.
                for(int i=0;i<200 && target.life==10000;i++)Step(context,owner,host,combat,input,new Vector2(650,600),false);
                Console.WriteLine("GROUND SUMMON mask="+mask+" active="+slime.active+" position="+slime.position+" originalDamage="+slime.originalDamage+" damage="+slime.damage+" life="+target.life);
                Require(mask==0?target.life==10000:target.life<10000,"native walking minion AI/movement/Damage attacks the wall-hidden receiver: "+mask+" position="+slime.position);
                Save(owner,0);
            }
        }
        private static void Timings(object context,object owner,object tools,object input)
        {
            var baseline=Events(context,owner,tools,input,1553,0,32);Require(baseline.Count>=4 && baseline.Zip(baseline.Skip(1),(a,b)=>b-a).All(x=>x==5),"fixed SDMG actual ItemCheck cadence is five ticks: "+string.Join(",",baseline));
            foreach(int weapon in new[]{95,1929,434})
            {
                var events=Events(context,owner,tools,input,weapon,2,40);
                Require(events.Count>=5 && events.Zip(events.Skip(1),(a,b)=>b-a).All(x=>x==5),"real single/burst uses retain five-tick emissions: "+weapon+" / "+string.Join(",",events));
            }
            Save(owner,2);var player=NativeToolExecutionChecks.Reset(context,tools,input,ItemID.CopperPickaxe,0,0);var item=player.HeldItem;
            player.ApplyItemTime(item);Require(player.itemTime==item.useTime,"damage-bearing pick retains native tool time");
            player.inventory[0].SetDefaults(95);item=player.HeldItem;Call(player,"ApplyItemAnimation",item);int duration=player.itemAnimationMax;
            Save(owner,0);player.ApplyItemTime(item);Require(player.itemTime==5 && player.itemAnimationMax==duration,"OFF during use leaves frozen current cycle intact");
            Call(player,"ApplyItemAnimation",item);player.ApplyItemTime(item);Require(player.itemTime==item.useTime,"next natural use restores original timing after OFF");
        }
        private static List<int> Events(object context,object owner,object tools,object input,int weapon,int mask,int ticks)
        {
            Save(owner,0);var p=NativeToolExecutionChecks.Reset(context,tools,input,weapon,0,0);p.inventory[54].SetDefaults(ItemID.MusketBall);p.inventory[54].stack=999;
            p.position=new Vector2(700,646);p.statMana=p.statManaMax2=10000;p.reuseDelay=0;p.releaseUseItem=true;
            Save(owner,mask);var seen=new HashSet<int>();var times=new List<int>();
            for(int tick=0;tick<ticks;tick++)
            {
                NativeToolExecutionChecks.Sample(context,input,new Vector2(1000,600),true);NativeQuickItemChecks.BeginWorldStep();p.controlUseItem=true;p.releaseUseItem=true;p.ItemCheck();
                bool born=false;foreach(var q in Main.projectile)if(q.active && q.owner==0 && seen.Add((int)q.key))born=true;
                if(born)times.Add(tick);
            }
            int spent=999-p.inventory[54].stack;
            // SDMG/Chain Gun have native 2/3 ammo conservation; zero spent in
            // this short sample is valid. Pistol spends once per use, whereas
            // Clockwork spends only on the actual first shot of each burst.
            Require(spent>=0 && spent<=times.Count,"native conservation cannot consume more ammo than legal uses: "+weapon);
            if(weapon==95)Require(spent==times.Count,"native pistol consumes exactly one ammo per actual use");
            if(weapon==434)Require(spent==(times.Count+2)/3,"native Clockwork still consumes once per original three-shot burst: uses="+times.Count+" spent="+spent);
            return times;
        }
        private static void PredictionBoundary(object context,object owner,object host,object combat,object tools,object input)
        {
            Save(owner,0);var player=NativeToolExecutionChecks.Reset(context,tools,input,95,0,0);player.inventory[54].SetDefaults(97);player.inventory[54].stack=999;
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.Center=new Vector2(950,646);n.life=10000;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,false,false,false,25,false,true));NativeToolExecutionChecks.Sample(context,input,new Vector2(500,500),true);Call(combat,"Sample");Call(host,"SampleMouse");Call(host,"Update",Main.GameUpdateCount);
            var attack=Get(combat,"Attack");
            foreach(int mask in new[]{1,2,4,7,0})
            {
                Save(owner,mask);Require((bool)Get(attack,"OriginalAttackPredictionAllowed")==(mask==0),"prediction follows effective mode mask");
                Call(host,"PrepareAction");var facts=NativeCombatAimChecks.QueryInput(attack,player);
                Require(facts.Allowed && (mask==0 || facts.Kind=="Basic"),"enhanced mode preserves selected-target Basic input");
                if(mask!=0)Require(GetOptional(attack,"ExpectedImpact")==null,"enhanced mode publishes no vanilla Contact");
            }
        }
        private static void PrismSource(object context,object owner,object host,object combat,object tools,object input)
        {
            Save(owner,0);NativeCombatObservationChecks.Save(host,new ObservationOptions());
            var player=NativeToolExecutionChecks.Reset(context,tools,input,3541,0,0);player.position=new Vector2(700,688-player.height);player.releaseUseItem=true;
            player.statMana=player.statManaMax=player.statManaMax2=10000;Main.screenPosition=new Vector2(550,350);
            var target=Main.npc[2];target.SetDefaults(3);target.whoAmI=2;target.active=true;target.Center=new Vector2(1100,650);target.aiStyle=-1;target.noGravity=true;target.velocity=Vector2.Zero;target.life=target.lifeMax=100000;
            var mouse=new Vector2(520,420);var attack=Get(combat,"Attack");var registry=(IDictionary)Get(Get(attack,"Control"),"shots");
            var cosmetic=new Harmony("JueMingR.Tests.ExperimentPrismRipple");cosmetic.Patch(typeof(Projectile).GetMethod("AI"),transpiler:new HarmonyMethod(typeof(NativeCombatBeamChecks).GetMethod("RippleOutlet",Flags)));
            try
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,true,false,false,25,false,true));
                for(int i=0;i<40;i++)Step(context,owner,host,combat,input,mouse);
                var parent=Main.projectile.Single(p=>p.active && p.type==633 && p.owner==0);int key=(int)parent.key;
                Require(registry.Contains(key),"natural held prism birth is registered");var source=Get(registry[key],"Source");
                foreach(int mask in new[]{1,2,4,7,0})
                {
                    Save(owner,mask);var beforePosition=player.position;float farthestMovement=0;
                    using(NativeCombatUsageChecks.ObserveDrawSteps())
                    {
                        int beforeSteps=NativeCombatUsageChecks.ObservedNpcSteps;
                        for(int i=0;i<16;i++)
                        {
                            Step(context,owner,host,combat,input,mouse,true,i<8?1:-1);
                            farthestMovement=Math.Max(farthestMovement,Vector2.DistanceSquared(beforePosition,player.position));
                        }
                        if(mask!=0)Require(NativeCombatUsageChecks.ObservedNpcSteps==beforeSteps,"enhanced real input/projectile chain performs no original NPC attack future steps");
                    }
                    Console.WriteLine("PRISM MOVEMENT mask="+mask+" peakSquared="+farthestMovement+" endSquared="+Vector2.DistanceSquared(beforePosition,player.position));
                    Require(farthestMovement>1,"native player movement actually advances while switching prism mode: "+mask);
                    Require(parent.active && (int)parent.key==key && registry.Contains(key) && ReferenceEquals(source,Get(registry[key],"Source")),"mode edge keeps same live prism and Source: "+mask);
                    float targetDot=Vector2.Dot(parent.velocity.SafeNormalize(Vector2.UnitY),(target.Center-player.MountedCenter).SafeNormalize(Vector2.UnitX));
                    float rawDot=Vector2.Dot(parent.velocity.SafeNormalize(Vector2.UnitY),(mouse-player.MountedCenter).SafeNormalize(Vector2.UnitY));
                    Require(targetDot>.95f && targetDot>rawDot+.5f,"prism keeps current target and realtime inertia through mode edge: "+mask);
                    if(mask!=0)Require(GetOptional(attack,"ExpectedImpact")==null,"held prism cannot publish original future during experiment");
                }
                for(int i=0;i<3;i++)Step(context,owner,host,combat,input,mouse,false);
                Require(!parent.active && !registry.Contains(key),"physical mouse release retires the natural continuous prism and source");
            }
            finally{cosmetic.UnpatchAll(cosmetic.Id);Save(owner,0);}
        }
        private static void Step(object context,object owner,object host,object combat,object input,Vector2 mouse,bool held=true,int movement=0)
        {
            NativeToolExecutionChecks.Sample(context,input,mouse,held,movement);Call(combat,"Sample");Call(host,"SampleMouse");NativeQuickItemChecks.BeginWorldStep();Main.LocalPlayer.Update(0);
            foreach(var n in Main.npc)if(n.active)n.UpdateNPC(n.whoAmI);
            Call(Get(combat,"Attack"),"PrepareControlledProjectileWorld");
            World(owner,()=>{for(int i=0;i<Main.maxProjectiles;i++)if(Main.projectile[i].active){Main.ProjectileUpdateLoopIndex=i;Main.projectile[i].Update(i);}});Main.ProjectileUpdateLoopIndex=-1;
            Call(context,"UpdateRuntime");
        }
    }
}
