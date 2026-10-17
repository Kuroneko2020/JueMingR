using System;
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
    internal static class NativeCombatHeldAmmoFamilyChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Assembly host;private static Type model;private static Projectile receiving;private static int first,currentTick,hitType,depth,rng;
        private static void Damage(Projectile __instance){receiving=__instance;}
        private static void DamageDone(){receiving=null;}
        private static void Strike(NPC __instance,int __result){if(__instance.whoAmI==2 && __result>0 && receiving!=null){if(first==0){first=currentTick;hitType=receiving.type;}if(runtimeWatch){string key=receiving.key.ToString();if(runtimeFirstBodies.Contains(key))runtimeFirstStrikes++;if(runtimeRestoredBodies.Contains(key))runtimeRestoredStrikes++;if(runtimeSwapBodies.Contains(key))runtimeSwapStrikes++;}}}
        private static object Static(Type t,string name,params object[] args){depth++;try{return t.GetMethod(name,Flags).Invoke(null,args);}finally{depth--;}}
        private static void Random(){if(depth>0)rng++;}
        internal static void Run(object context)
        {
            host=Get(context,"Combat").GetType().Assembly;
            var items=ContentSamples.ItemsByType.Values.Where(i=>(i.ammo==AmmoID.Arrow || i.ammo==AmmoID.Bullet) && i.shoot>0).OrderBy(i=>i.shoot).ThenBy(i=>i.type).ToArray();
            foreach(var item in items){var p=ContentSamples.ProjectilesByType[item.shoot];Console.WriteLine("HELD-AMMO inventory item="+item.type+" ammo="+item.ammo+" shoot="+item.shoot+" ai="+p.aiStyle+" updates="+p.extraUpdates+" pen="+p.penetrate+" size="+p.width+"x"+p.height);}
            Console.WriteLine("HELD-AMMO inventory distinct="+items.Select(i=>i.shoot).Distinct().Count()+" items="+items.Length);
            Console.WriteLine("HELD-AMMO actual local-immunity members="+string.Join(",",items.Select(i=>i.shoot).Concat(new[]{90,92,616,631,640,661,706,1124}).Distinct().Where(t=>ContentSamples.ProjectilesByType[t].usesLocalNPCImmunity).Select(t=>t+":"+ContentSamples.ProjectilesByType[t].localNPCHitCooldown)));
            if(Environment.GetEnvironmentVariable("JMR_HELD_AMMO_INVENTORY_ONLY")=="1")return;
            model=host.GetType("JueMingR.TerrariaHost.Combat.HostHeldAmmoFamilyAttack",true);
            var audit=new Harmony("JueMingR.Tests.HeldAmmoFamily");
            depth=rng=0;foreach(var method in typeof(Terraria.Utilities.UnifiedRandom).GetMethods(Flags).Where(m=>m.DeclaringType==typeof(Terraria.Utilities.UnifiedRandom) && (m.Name=="Next" || m.Name=="NextDouble")))audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(Random)));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(Damage)),finalizer:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(DamageDone)));
            audit.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(Strike)));
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            try{var section=Environment.GetEnvironmentVariable("JMR_HELD_AMMO_SECTION");if(section=="copy")CopyIsolation(context);else if(section=="slot-prefix")SlotPrefix(context);else if(section=="terminal")TerminalStages(context);else if(section=="candidate-cost")CandidateCost(context);else if(section=="world-gates")WorldGates(context);else if(section=="range-lifecycle")RangeLifecycle(context);else if(section=="source-gates")SourceGates(context);else if(section=="source-tail")SourceGateTail(context);else if(section=="source-aim-runtime")MovingRuntime(context,false,1);else if(section=="source-both-runtime")MovingRuntime(context,false,2);else if(section=="moving-runtime")MovingRuntime(context);else if(section=="range-runtime")MovingRuntime(context,true);else if(section=="moving-consumer")MovingConsumer(context);else if(section=="consumer")Consumers(context);else if(section=="burst")VortexBurst(context);else if(section=="harpy")Harpy(context);else{bool birthsOnly=section=="births";if(!birthsOnly){CopyIsolation(context);Ai(context,items.Select(i=>i.shoot).Distinct().Concat(new[]{616,661}).ToArray());Vortex(context);VortexBurst(context);Known(context,items.Select(i=>i.shoot).Distinct().Concat(new[]{616,661}).ToArray());}Births(context,items);if(!birthsOnly){Consumers(context);if(section!="no-harpy")Harpy(context);if(section==null){MovingConsumer(context);MovingRuntime(context);MovingRuntime(context,true);WorldGates(context);RangeLifecycle(context);SlotPrefix(context);TerminalStages(context);}}}Require(rng==0,"complete private held batch consumes zero original RNG");Console.WriteLine(section=="moving-runtime" || section=="range-runtime"?"PASS HELD-AMMO normal real Source/Main/input/birth/Strike chain; native RNG is outside private-call audit":section==null?"PASS HELD-AMMO complete legal inventory, native held births, ordered family receives and616; explicitly audited private calls RNG0":"PASS HELD-AMMO component "+section+" RNG0");}
            finally{receiving=null;Main.ProjectileUpdateLoopIndex=-1;audit.UnpatchAll(audit.Id);}
        }
        private static Player Scene(object context)
        {
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3475,0,0);p.position=new Vector2(850,646);p.velocity=Vector2.Zero;p.ResetEffects();p.ResetMeleeHitCooldowns();p.channel=p.controlUseItem=true;Main.windPhysics=false;Main.ProjectileUpdateLoopIndex=-1;
            foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;first=currentTick=hitType=0;
            for(int x=5;x<Main.maxTilesX-5;x++)for(int y=5;y<Main.maxTilesY-5;y++)Main.tile[x,y].ClearEverything();return p;
        }
        private static NPC Target(Vector2 center,int slot=2,bool blocked=false)
        {
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.active=true;n.whoAmI=slot;n.width=n.height=31;n.Center=center;n.life=n.lifeMax=100000;n.defense=0;n.knockBackResist=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.dontTakeDamage=blocked;Array.Clear(n.immune,0,n.immune.Length);return n;
        }
        private static NpcTrajectory Path(NPC n)
        {
            var key=(NpcIdentity)Static(host.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,Active=true,CanReceive=!n.dontTakeDamage,NoTileCollide=true};return new NpcTrajectory(key,Main.GameUpdateCount,1,PredictionAssumption.NoNewHits,PredictionStop.None,Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray(),121);
        }
        private static object Terrain(){var t=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static object Root(Player p,Projectile q,NpcTrajectory path){return Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,false,true},null);}
        private static Projectile Copy(Projectile p){return (Projectile)Static(host.GetType("JueMingR.TerrariaHost.Combat.HostNebulaBlazeAttack",true),"Copy",p);}
        private static void CopyIsolation(object context)
        {
            foreach(int type in new[]{1,91,117,639,14,89,283,286,225,285,207,616,661})
            {
                var p=Scene(context);var q=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),p.Center,new Vector2(8,-1),type,60,0,p.whoAmI)];q.ai[0]=3;q.localAI[0]=7;q.localNPCImmunity[2]=11;
                var a=(Projectile)Static(model,"Copy",q);var b=(Projectile)Static(model,"Copy",q);
                Require(a.key.Equals(q.key) && b.key.Equals(q.key) && a.position==q.position && a.velocity==q.velocity && a.oldVelocity==q.oldVelocity && a.aiStyle==q.aiStyle && a.arrow==q.arrow,"captured projectile copy preserves native identity and motor dispatch");
                foreach(string field in new[]{"ai","localAI","localNPCImmunity","oldPos","oldRot","oldSpriteDirection"})
                {
                    var original=typeof(Projectile).GetField(field,Flags).GetValue(q) as Array;var left=typeof(Projectile).GetField(field,Flags).GetValue(a) as Array;var right=typeof(Projectile).GetField(field,Flags).GetValue(b) as Array;
                    Require(original==null?left==null && right==null:!ReferenceEquals(original,left) && !ReferenceEquals(left,right) && original.Cast<object>().SequenceEqual(left.Cast<object>()),"every replay array is detached from live state and other candidates: "+field);
                }
                a.ai[0]++;a.localAI[0]++;a.localNPCImmunity[2]++;a.position.X++;
                Require(q.ai[0]==3 && b.ai[0]==3 && q.localAI[0]==7 && b.localAI[0]==7 && q.localNPCImmunity[2]==11 && b.localNPCImmunity[2]==11 && b.position==q.position,"one candidate's writes never change another candidate or native projectile");
            }
            Console.WriteLine("PASS HELD copied native identities and six mutable arrays isolated across13 finite mechanisms");
        }
        private static void Ai(object context,int[] types)
        {
            int cases=0,actions=0;
            foreach(int type in types)foreach(int age in new[]{0,14,15})
            {
                var p=Scene(context);var n=Target(new Vector2(1250,360));var q=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(1000,350),new Vector2(6,-1),type,60,0,p.whoAmI)];q.ai[0]=age;var shadow=Copy(q);var path=Path(n);var root=Root(p,q,path);var capture=Static(model,"CaptureForSolve",p,root,new[]{type});var terrain=Terrain();
                for(int action=0;action<45;action++)
                {
                    int result=(int)Static(model,"Ai",shadow,capture,path,1,terrain);Require(result==0,"every actual selected ammo motor has an explicit supported finite kernel type="+type);q.AI();
                    Require(Vector2.DistanceSquared(q.velocity,shadow.velocity)<.00001f && q.ai.SequenceEqual(shadow.ai) && q.alpha==shadow.alpha,"original selected ammo AI clock/velocity/alpha type="+type+" age="+age+" action="+action+" native="+q.velocity+"/"+string.Join(",",q.ai)+" alpha="+q.alpha+" model="+shadow.velocity+"/"+string.Join(",",shadow.ai)+" alpha="+shadow.alpha);
                    q.position+=q.velocity;shadow.position+=shadow.velocity;actions++;
                }
                cases++;
            }
            Console.WriteLine("PASS HELD-AMMO legal inventory motor cases="+cases+" actions="+actions);
        }
        private static void Vortex(object context)
        {
            foreach(int edge in new[]{0,1,2,3,4,5})
            {
                var p=Scene(context);var n=Target(new Vector2(edge==3?2050:1250,365),blocked:edge==1);var q=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(1000,350),new Vector2(8,0),616,60,0,p.whoAmI)];q.ai[0]=edge==5?19:20;q.ai[1]=edge==0 || edge==4?0:3;
                if(edge==2)n.active=false;if(edge==4)for(int y=5;y<75;y++)NativeToolsChecks.Tile(70,y,1);var path=Path(n);var capture=Static(model,"CaptureForSolve",p,Root(p,q,path),new[]{616});var shadow=Copy(q);
                for(int action=0;action<3;action++){Require((int)Static(model,"Ai",shadow,capture,path,1,Terrain())==0,"Vortex exact observed target premise is supported");q.AI();Require(q.ai.SequenceEqual(shadow.ai) && q.localAI.SequenceEqual(shadow.localAI) && Vector2.DistanceSquared(q.velocity,shadow.velocity)<.00001f,"61620/21clock,800LOS acquisition,1000locked/invulnerable/lost semantics edge="+edge+" action="+action+" native="+q.velocity+"/"+string.Join(",",q.ai)+" local="+string.Join(",",q.localAI)+" model="+shadow.velocity+"/"+string.Join(",",shadow.ai)+" local="+string.Join(",",shadow.localAI));}
            }
            foreach(int phase in new[]{0,4,5,29,33,34,35,39,68,69})foreach(int delay in new[]{0,1,5})
            {var q=new Projectile();q.SetDefaults(615);q.ai[0]=phase;Require((bool)Static(host.GetType("JueMingR.TerrariaHost.Combat.HostVortexRocketAttack",true),"Due",q,delay)==(((phase+delay+1)/5)%7==0),"616 exact native controller quotient/modulo firing phase");}
            Console.WriteLine("PASS HELD-AMMO616 six lock edges and30 exact phase boundaries");
        }
        private static void Advance(int limit=120,Player harpy=null)
        {
            for(currentTick=1;currentTick<=limit && first==0;currentTick++)
            {NativeQuickItemChecks.BeginWorldStep();if(harpy!=null)typeof(Player).GetMethod("Update_HarpyCharm",Flags).Invoke(harpy,null);foreach(var n in Main.npc)if(n.active)n.UpdateNPC(n.whoAmI);for(int slot=0;slot<1000;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;try{Main.projectile[slot].Update(slot);}finally{Main.ProjectileUpdateLoopIndex=-1;}}}
        }
        private static object Clock(NpcTrajectory path,string phase="CompletedWorld")
        {return Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),phase)},null);}
        private static void VortexBurst(object context)
        {
            foreach(int penetration in new[]{0,1})foreach(bool preceding in new[]{false,true})foreach(bool blocked in new[]{false,true})
            {
                var p=Scene(context);var n=Target(new Vector2(1008,380),blocked:blocked);if(preceding)Target(n.Center,1);var q=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(1000,350),new Vector2(8,0),616,60,0,p.whoAmI)];q.timeLeft=1;q.penetrate=penetration;var path=Path(n);var root=Root(p,q,path);AttackContact plan;using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))plan=(AttackContact)Static(model,"KnownFamily",p,new[]{q},path,Clock(path),Terrain(),root);Advance(3);
                bool hit=!blocked && (!preceding || penetration==0);Require(hit?plan!=null && plan.Tick==first && first==1:plan==null && first==0,"616 samebody80 native Kill preserves exhausted/unlimited versus livepen1 and prior receiver slot/owner history pen="+penetration+" preceding="+preceding+" blocked="+blocked+" native="+first+" predicted="+(plan?.Tick??-1));
            }
            Console.WriteLine("PASS HELD-AMMO616 actual burst pen/slot/invulnerable cases=8");
        }
        private static void Known(object context,int[] types)
        {
            foreach(int type in types)
            {
                var p=Scene(context);var n=Target(new Vector2(1250,350));var q=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(1000,350),new Vector2(8,0),type,60,0,p.whoAmI)];var path=Path(n);var root=Root(p,q,path);AttackContact plan;
                using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))plan=(AttackContact)Static(model,"KnownFamily",p,new[]{q},path,Clock(path),Terrain(),root);
                Require(plan!=null,"every actual legal selected ammo has useful finite known receive type="+type);int left=q.timeLeft;q.timeLeft--;Require(!(bool)Get(root,"Unchanged"),"sameGUC family lifetime edit revokes actual batch root");q.timeLeft=left;int style=q.aiStyle;q.aiStyle=style==0?1:0;Require(!(bool)Get(root,"Unchanged"),"actual motor dispatch edit revokes the captured batch");q.aiStyle=style;bool arrow=q.arrow;q.arrow=!arrow;Require(!(bool)Get(root,"Unchanged"),"actual arrow charm/quiver/proc qualification edit revokes the captured batch");q.arrow=arrow;Advance();Require(first==plan.Tick,"ordered finite batch predicts original first Strike type="+type+" predicted="+plan.Tick+" native="+first+" receive="+hitType);
            }
            Console.WriteLine("PASS HELD-AMMO sealed native receives="+types.Length+" with sameGUC family edits");
        }
        private static void Births(object context,Item[] items)
        {
            int cases=0,damageMismatches=0;
            foreach(int weapon in new[]{3475,3540,3854})foreach(var item in items.GroupBy(i=>i.shoot).Select(g=>g.First()).Where(i=>i.ammo==ContentSamples.ItemsByType[weapon].useAmmo))
            {
                var p=Scene(context);p.inventory[p.selectedItem].SetDefaults(weapon);p.inventory[54].SetDefaults(item.type);p.inventory[54].stack=999;p.phantomPhoneixCounter=0;p.itemAnimation=p.itemAnimationMax=p.HeldItem.useAnimation;
                var parent=new Projectile();parent.SetDefaults(p.HeldItem.shoot);parent.owner=p.whoAmI;parent.active=true;parent.whoAmI=0;parent.damage=p.GetWeaponDamage(p.HeldItem);parent.ai[0]=10;parent.ai[1]=1;Main.projectile[0]=parent;
                var n=Target(p.Center+new Vector2(390,0));n.width=n.height=180;n.Center=p.Center+new Vector2(390,0);var path=Path(n);object[] args={p,parent,path,Clock(path),Terrain(),null,null,null};AttackContact plan;using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))plan=(AttackContact)Static(host.GetType("JueMingR.TerrariaHost.Combat.HostHeldAttack",true),"Solve",args);
                Require(plan!=null && args[7]!=null,"public held dispatcher admits complete selected ammo family weapon="+weapon+" ammo="+item.type);var aim=(Vector2)args[5];Main.mouseX=(int)(aim.X-Main.screenPosition.X);Main.mouseY=(int)(aim.Y-Main.screenPosition.Y);parent.AI();var born=Main.projectile.Where(q=>q.active && q.type!=parent.type).ToArray();int expectedType=parent.type==705 && item.shoot==1?2:item.shoot;Require(born.Length==(parent.type==630?4:parent.type==705?2:1) && born.All(q=>q.type==expectedType),"native controller firing retains exact legal selected type and volley count weapon="+weapon+" ammo="+item.type);
                int damage=(int)Get(args[6],"Damage");if(born.Any(q=>q.damage!=damage)){damageMismatches++;Console.WriteLine("FAIL HELD selected damage weapon="+weapon+" ammo="+item.type+" captured="+damage+" native="+born[0].damage);}
                foreach(var q in born)Require(Vector2.DistanceSquared(q.Center,p.RotatedRelativePoint(p.MountedCenter))<=450 && q.damage>0,"native selected-ammo births use mounted origin plus only630 legal jitter");parent.active=false;var actualRoot=Root(p,born[0],path);AttackContact sealedPlan;using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))sealedPlan=(AttackContact)Static(model,"KnownFamily",p,born,path,Clock(path),Terrain(),actualRoot);Advance();Require(first>0 && sealedPlan!=null && first==sealedPlan.Tick,"actual native held volley and sealed exact birth batch first receive weapon="+weapon+" ammo="+item.type+" predicted="+(sealedPlan?.Tick??-1)+" native="+first);Console.WriteLine("HELD native volley weapon="+weapon+" ammo="+item.type+" bodies="+born.Length+" first="+first);cases++;
            }
            Console.WriteLine("HELD-AMMO actual controller AI075 legal ammo births/receives="+cases+" damageMismatches="+damageMismatches);Require(damageMismatches==0,"every native held selected-ammo damage equals the captured PickAmmo result");
        }
        private static void Consumers(object context)
        {
            var observation=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");
            foreach(var row in new[]{new[]{3475,1351,615},new[]{3540,516,630},new[]{3854,3568,705}})foreach(int edit in new[]{0,1,2,3,4,5,6})
            {
                var p=Scene(context);p.inventory[p.selectedItem].SetDefaults(row[0]);p.inventory[54].SetDefaults(row[1]);p.inventory[54].stack=999;p.itemAnimation=p.itemTime=0;p.releaseUseItem=true;p.phantomPhoneixCounter=0;Main.screenPosition=new Vector2(500,400);for(int x=5;x<Main.maxTilesX-5;x++)NativeToolsChecks.Tile(x,43,1);var n=Target(p.Center+new Vector2(210,0));n.width=n.height=100;n.Center=p.Center+new Vector2(210,0);
                NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,false,true,25,true,true));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),n.Center-new Vector2(450,0),true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,n,0);typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var parent=Main.projectile.Single(q=>q.active && q.type==row[2]);parent.ai[0]=10;parent.ai[1]=1;
                var ambient=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(1100,350),Vector2.Zero,row[2]==615?286:639,0,0,p.whoAmI)];NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,n,0);
                // CompletedWorld no longer grants finite controller input.
                // This registered component uses the same post-NPC core as Main.
                using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))Call(Get(attack,"Control"),"PrepareProjectileWorld",Call(Get(Get(observation,"Prediction"),"Cache"),"Read",1));var entries=(System.Collections.IDictionary)Get(Get(attack,"Control"),"shots");Require(entries.Contains((int)parent.key),"real Shoot registered its native held controller");var entry=entries[(int)parent.key];var root=GetOptional(entry,"Obstacles");Require(root!=null && GetOptional(entry,"Contact")!=null,"real registered held preparation returns complete family Contact/root type="+parent.type);
                if(edit==1)parent.scale=0;if(edit==2)p.channel=false;if(edit==3)ambient.ai[0]++;if(edit==4)p.position.X+=.125f;if(edit==5)p.gravDir=-1;if(edit==6){Main.tile[(int)p.position.X/16,(int)(p.position.Y+p.height)/16].active(false);Require(!(bool)Get(Get(Get(attack,"Control"),"terrain"),"Unchanged"),"read owner-ground terrain edit revokes the actual preparation terrain lease");}if(edit!=0 && edit!=6)Require(!(bool)Get(root,"Unchanged"),"sameGUC parent scale/channel/family edit revokes the actual registered root edit="+edit);
                int mouse=Main.mouseX;parent.AI();bool enhanced=Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced" && ReferenceEquals(GetOptional(Get(attack,"consumedInput"),"Source"),parent);Require(Main.mouseX==mouse,"real held AI returns physical cursor");Require(enhanced==(edit==0),"actual native held cursor consumer accepts only a valid complete root edit="+edit+" type="+parent.type);if(edit==0){parent.active=false;ambient.active=false;Advance();Require(first>0,"registered enhanced held source reaches native positive Damage");}
            }
            Console.WriteLine("PASS HELD-AMMO real Shoot registered three controllers→familyRoot→AI Enhanced/Strike original9sameGUC scale/channel/family plus9 owner-position/gravity/read-ground counterexamples (1000ms component scope)");
        }
        private static object movingEntry;private static Projectile movingParent;private static NPC movingSelected,movingOther;
        private static object movingRoot;private static AttackContact movingPlan;private static bool movingWatch,movingBeforeAi,movingActualSource;private static string movingKind;private static int movingEdit,movingPreparations,movingBorn;
        private static void AfterProjectileWorldPreparation()
        {
            if(!movingWatch || movingEntry==null)return;movingPreparations++;movingRoot=GetOptional(movingEntry,"Obstacles");movingPlan=(AttackContact)GetOptional(movingEntry,"Contact");
            // Later than the production Main prefix, but before the first slot.
            if(movingEdit==1)movingSelected.position.X+=7;
            if(movingEdit==2)movingSelected.dontTakeDamage=true;
            if(movingEdit==3)movingOther.position.X+=7;
            if(movingEdit==5)movingSelected.life--;
            if(movingEdit==8){var body=movingSelected.position+movingSelected.netOffset;movingSelected.netOffset.X+=.125f;Require(movingSelected.position+movingSelected.netOffset!=body,"negative changes the actual owner body after preparation");Console.WriteLine("HELD OFFSET postprepare actual owner="+(movingSelected.position+movingSelected.netOffset)+" prior="+body);}
            if(movingEdit==6){var before=movingSelected.position;var width=movingSelected.width;var height=movingSelected.height;movingSelected=Target(Vector2.Zero);movingSelected.position=before;movingSelected.width=width;movingSelected.height=height;}
        }
        private static void BeforeMovingAi(Projectile __instance)
        {if(movingWatch && ReferenceEquals(__instance,movingParent))movingBeforeAi=movingRoot!=null && (bool)Get(movingRoot,"Unchanged");}
        private static int movingRecordInputs;
        private static void MovingReceipt(object __0)
        {if(movingWatch && ReferenceEquals(GetOptional(__0,"Source"),movingParent)){movingRecordInputs++;movingKind=Convert.ToString(Get(__0,"Kind"));movingActualSource=true;}}
        private static void MovingBorn(int __result,Terraria.DataStructures.IEntitySource __0)
        {if(movingWatch && __result>=0 && __result<Main.maxProjectiles && Main.projectile[__result].owner==Main.myPlayer && !ReferenceEquals(Main.projectile[__result],movingParent)){movingBorn++;if(runtimeWatch && movingActualSource && movingKind=="Enhanced"){string key=Main.projectile[__result].key.ToString();if(runtimeStage==1)runtimeFirstBodies.Add(key);if(runtimeStage==2)runtimeRestoredBodies.Add(key);if(runtimeSwap && Main.projectile[__result].type==89 && Main.ProjectileUpdateLoopIndex==movingParent.whoAmI && __0 is Terraria.DataStructures.EntitySource_ItemUse_WithAmmo)runtimeSwapBodies.Add(key);}}}
        private static Projectile slotLow;private static int slotEdit;private static bool slotWatch;
        private static void TraceSlotTerminal(Projectile __instance)
        {
            if(!slotWatch || !ReferenceEquals(__instance,movingParent) || movingRoot==null)return;
            // Edit AFTER the earlier native body updated, immediately before
            // actual parent consumption. Audio is not a collision premise;
            // the deliberately retained initialization flag still is sealed.
            if(slotLow.type==706 && slotEdit==5)slotLow.localAI[0]=0;
            if(slotLow.type==706 && slotEdit==6)slotLow.localAI[1]++;
            var families=GetOptional(movingRoot,"heldPrefixFamilies") as System.Collections.IDictionary;if(families==null)return;
            foreach(var family in families.Values)foreach(var member in (System.Collections.IEnumerable)Get(family,"members"))if((int)Get(member,"Slot")==slotLow.whoAmI)
            {
                Console.WriteLine("HELD SLOT TERMINAL edit="+slotEdit+" expected="+Get(member,"Position")+"/"+Get(member,"Velocity")+"/"+Get(member,"Type")+"/"+Get(member,"Owner")+" native="+slotLow.position+"/"+slotLow.velocity+"/"+slotLow.type+"/"+slotLow.owner+" active="+slotLow.active);
                if(slotLow.type==706)
                {
                    var local=GetOptional(member,"retargetLocal") as int[];Console.WriteLine("HELD706 LEASE root="+Get(movingRoot,"HeldControllerUnchanged")+" family="+Get(family,"Current")+" initialReceive="+Call(movingRoot,"ReceiveUnchanged")+" birthInputs="+Call(movingRoot,"BirthInputsMatch",false,true)+" nativeLocal="+slotLow.localNPCImmunity[2]+" expectedLocal="+(local==null?-999:local[2]));
                    Console.WriteLine("HELD706 SCALARS expected localAI="+string.Join(",",(float[])Get(member,"Local"))+" ai="+string.Join(",",(float[])Get(member,"Ai"))+" alpha="+Get(member,"Alpha")+" left="+Get(member,"Left")+" pen="+Get(member,"Penetration")+" native localAI="+string.Join(",",slotLow.localAI)+" ai="+string.Join(",",slotLow.ai)+" alpha="+slotLow.alpha+" left="+slotLow.timeLeft+" pen="+slotLow.penetrate);
                    int i=0;foreach(var lease in (System.Collections.IEnumerable)Get(movingRoot,"receiveLeases"))Console.WriteLine("HELD706 RECEIVE lease="+(i++)+" type="+((Projectile)Get(lease,"shot")).type+" current="+Call(lease,"ReceiveUnchanged"));
                }
                return;
            }
        }
        private static void EditSlotPrefix()
        {
            if(!slotWatch || slotEdit==0 || slotEdit==4 || slotEdit==5 || slotEdit==6)return;
            if(slotEdit==1)slotLow.velocity.X+=.125f;
            else if(slotEdit==3)slotLow.localNPCImmunity[2]++;
            else
            {
                var p=Main.LocalPlayer;var center=slotLow.Center;var velocity=slotLow.velocity;int slot=slotLow.whoAmI;var key=slotLow.key;slotLow.active=false;
                int next=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,velocity,14,1,0,p.whoAmI);slotLow=Main.projectile[next];slotLow.timeLeft=1;Require(next==slot && !slotLow.key.Equals(key),"counterexample uses an actual native new identity in the same earlier slot");
            }
        }
        private static void SlotPrefix(object context)
        {
            foreach(int layout in new[]{0,1,2,3}.Where(i=>Environment.GetEnvironmentVariable("JMR_HELD_SLOT_LAYOUT")==null || i.ToString()==Environment.GetEnvironmentVariable("JMR_HELD_SLOT_LAYOUT")))
            foreach(int edit in layout==2?new[]{0,3,5,6}:layout==3?new[]{0,3}:layout==0?new[]{0,1,2,4}:new[]{0,1,2})
            {
            bool phoenixThird=layout==1;
            var p=Scene(context);p.position=new Vector2(650,646);p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;p.itemAnimation=p.itemTime=0;p.releaseUseItem=true;Main.screenPosition=new Vector2(500,400);
            if(phoenixThird){p.HeldItem.SetDefaults(3854);p.inventory[54].SetDefaults(40);p.inventory[54].stack=999;}
            for(int x=5;x<Main.maxTilesX-5;x++)NativeToolsChecks.Tile(x,43,1);var n=Target(p.Center+new Vector2(200,0));n.width=n.height=60;n.Center=p.Center+new Vector2(200,0);
            var low=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(500,350),new Vector2(8,0),layout==2?706:layout==3?639:14,1,0,p.whoAmI)];low.timeLeft=layout>=2?30:1;if(layout>=2)low.localNPCImmunity[2]=layout==2?4:-1;if(layout==3){low.localAI[0]=500;low.localAI[1]=350;low.ai[0]=8;low.ai[1]=0;}var lowKey=low.key;int lowSlot=low.whoAmI;
            NPC priorReceiver=null;if(edit==4){low.timeLeft=100;priorReceiver=Target(low.Center+new Vector2(8,0),1);}
            var observation=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");var input=Get(context,"Input");NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,false,true,25,true,true));
            NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,n,0);
            typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var parent=Main.projectile.Single(q=>q.active && q.type==(phoenixThird?705:615));Require(lowSlot<parent.whoAmI,"original live ammo occupies the lower slot before actual held Shoot");parent.ai[0]=10;parent.ai[1]=1;if(phoenixThird)p.phantomPhoneixCounter=2;
            NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,n,0,phaseName:"BeforeNpc");n.UpdateNPC(n.whoAmI);
            var control=Get(attack,"Control");using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))Call(control,"PrepareProjectileWorld",Call(Get(Get(observation,"Prediction"),"Cache"),"Read",1));var entries=(System.Collections.IDictionary)Get(control,"shots");movingEntry=entries[(int)parent.key];movingRoot=GetOptional(movingEntry,"Obstacles");movingPlan=GetOptional(movingEntry,"Contact") as AttackContact;movingParent=parent;movingActualSource=false;movingKind=null;movingRecordInputs=0;
            var audit=new Harmony("JueMingR.Tests.HeldSlotPrefix");audit.Patch(attack.GetType().GetMethod("RecordInput",Flags),postfix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(MovingReceipt)));
            audit.Patch(typeof(Projectile).GetMethod("AI",Flags),prefix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(TraceSlotTerminal)){priority=Priority.First+1000});
            if(Main.instance.SpelunkerProjectileHelper==null)Main.instance.SpelunkerProjectileHelper=new Terraria.GameContent.SpelunkerProjectileHelper();if(Main.instance.ChumBucketProjectileHelper==null)Main.instance.ChumBucketProjectileHelper=new Terraria.GameContent.ChumBucketProjectileHelper();var world=typeof(Main).GetMethod("UpdateWorld_Projectiles",Flags);
            audit.Patch(world,prefix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(EditSlotPrefix)){priority=Priority.Last});slotLow=low;slotEdit=edit;
            try
            {
                currentTick=1;movingWatch=slotWatch=true;world.Invoke(Main.instance,null);movingWatch=slotWatch=false;bool enhanced=movingActualSource && movingKind=="Enhanced";var child=Main.projectile.First(q=>q.active && q.type==(phoenixThird?706:14) && !q.key.Equals(lowKey));int childSlot=child.whoAmI;parent.active=false;
                for(currentTick=2;currentTick<=80 && first==0;currentTick++){NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(n.whoAmI);world.Invoke(Main.instance,null);}
                Console.WriteLine("HELD SLOT PREFIX layout="+layout+" controller="+parent.type+" child="+child.type+" edit="+edit+" low="+lowSlot+" parent="+parent.whoAmI+" nativeChild="+childSlot+" oldRetired="+!low.key.Equals(lowKey)+" predicted="+movingPlan?.Tick+" nativeFirst="+first+" input="+movingKind+" source="+movingActualSource+" ambientLocal="+low.localNPCImmunity[2]);
                Require(movingPlan!=null && (edit==4 || first==movingPlan.Tick) && enhanced==(edit==0 || edit==5) && (layout>=2?childSlot>parent.whoAmI:childSlot<parent.whoAmI),"real lower-slot update/retirement has exact first Strike and Enhanced; nonpredicted motion/identity/local edits revoke input");
                if(edit==4)Require(priorReceiver.life<priorReceiver.lifeMax,"the earlier slot really damages another receiver; complete NPC/history premises revoke Enhanced");
            }
            finally{movingWatch=slotWatch=false;audit.UnpatchAll(audit.Id);}
            }
        }
        private static void TerminalStages(object context)
        {
            // Compare ONE original projectile Update, stopping the private
            // outer at that exact slot. Children are born while the parent
            // remains occupied, but no higher child AI has run in either side.
            int[] types={14,14,14,14,90,14,41,286,89,639,225,285};
            string[] stages={"left/no-extra","left/extra2","tile/extra2","Damage-pen0/extra2","AI/extra2","full-shimmer/extra2","41-odd-burst/no-extra","286-odd-burst/extra2","89-native-children","639-native-child","225-terminal-tile","285-terminal-tile"};
            for(int edge=0;edge<types.Length;edge++)
            {
                var owner=Scene(context);var selected=Target(new Vector2(1250,800));
                var shot=Main.projectile[Projectile.NewProjectile(owner.GetProjectileSource_Item(owner.HeldItem),new Vector2(504,350),new Vector2(8,0),types[edge],60,0,owner.whoAmI)];
                shot.extraUpdates=edge==0 || edge==6?0:edge==8 || edge==9 || edge==11?1:2;shot.timeLeft=1;shot.tileCollide=true;
                if(edge==2 || edge==10 || edge==11){shot.timeLeft=100;for(int y=16;y<30;y++)NativeToolsChecks.Tile(32,y,1);}
                if(edge==3){shot.timeLeft=100;shot.penetrate=1;Target(shot.Center+new Vector2(8,0),1);}
                if(edge==4){shot.timeLeft=100;shot.ai[1]=15;shot.scale=.21f;}
                if(edge==5){shot.timeLeft=100;for(int x=28;x<37;x++)for(int y=18;y<27;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(3);}}
                if(edge==6 || edge==7){shot.width=9;shot.height=11;shot.position=new Vector2(499.25f,344.75f);}
                if(edge==9){shot.localAI[0]=shot.Center.X;shot.localAI[1]=shot.Center.Y;shot.ai[0]=8;shot.ai[1]=0;}
                if(edge==10)shot.ai[1]=3;if(edge==11)shot.ai[1]=1;
                var path=Path(selected);var root=Root(owner,shot,path);var captured=Static(model,"CaptureForSolve",owner,root,new[]{shot.type});Call(root,"BeginCandidate");Call(root,"ResetAmmoImpactCandidate");
                var memberType=model.GetNestedType("Member",Flags);var rows=(System.Collections.IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(memberType));
                var predicted=(Projectile)Static(model,"Copy",shot);rows.Add(Static(model,"Make",predicted,root,false,false,0));var pool=Call(Get(captured,"Slots"),"Copy");
                object[] args={rows,pool,captured,path,1,1,selected.Center,Terrain(),root,owner.Center,false,null,-1,shot.whoAmI,true};
                Require((bool)Static(model,"Outer",args),"terminal stage private replay is fully known: "+stages[edge]);
                var key=shot.key;int slot=shot.whoAmI;Main.ProjectileUpdateLoopIndex=slot;try{shot.Update(slot);}finally{Main.ProjectileUpdateLoopIndex=-1;}
                var expectedChildren=rows.Cast<object>().Select(m=>(Projectile)Get(m,"Shot")).Where(q=>!ReferenceEquals(q,predicted)).OrderBy(q=>q.whoAmI).ToArray();
                var actualChildren=Main.projectile.Where(q=>q.active && !q.key.Equals(key)).OrderBy(q=>q.whoAmI).ToArray();
                Console.WriteLine("HELD TERMINAL edge="+edge+" stage="+stages[edge]+" updates="+(shot.extraUpdates+1)+" native="+shot.position+"/"+shot.velocity+"/"+shot.width+"x"+shot.height+" active="+shot.active+" model="+predicted.position+"/"+predicted.velocity+"/"+predicted.width+"x"+predicted.height+" active="+predicted.active+" nativeChildren="+string.Join(",",actualChildren.Select(q=>q.whoAmI+":"+q.type))+" modelChildren="+string.Join(",",expectedChildren.Select(q=>q.whoAmI+":"+q.type)));
                Require(!shot.active && !predicted.active && shot.position==predicted.position && shot.velocity==predicted.velocity && shot.width==predicted.width && shot.height==predicted.height && shot.type==predicted.type && shot.owner==predicted.owner,"strict retired terminal movement/geometry matches original Update: "+stages[edge]);
                Require(actualChildren.Select(q=>q.type).SequenceEqual(expectedChildren.Select(q=>q.type)) && actualChildren.Select(q=>q.whoAmI).SequenceEqual(expectedChildren.Select(q=>q.whoAmI)),"actual native Kill child types and occupied-parent allocation preserved: "+stages[edge]);
                if(edge==8)Require(actualChildren.Length==2 && actualChildren.All(q=>q.type==90),"89 really emits two original crystal children");if(edge==9)Require(actualChildren.Length==1 && actualChildren[0].type==640,"639 really emits its original delayed child");
            }
            Console.WriteLine("PASS HELD twelve exact terminal stages: no/extra updates, AI/liquid/tile/Damage/lifetime, odd integer bursts and native derived slots");
        }
        private static bool runtimeWatch,runtimeSwap;private static int runtimeStage,runtimeFirstStrikes,runtimeRestoredStrikes,runtimeSwapStrikes;
        private static readonly System.Collections.Generic.HashSet<string> runtimeFirstBodies=new System.Collections.Generic.HashSet<string>(),runtimeRestoredBodies=new System.Collections.Generic.HashSet<string>();
        private static readonly System.Collections.Generic.HashSet<string> runtimeSwapBodies=new System.Collections.Generic.HashSet<string>();
        private static NPC RuntimeNpc(int slot,Vector2 position)
        {
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.position=position;n.target=Main.myPlayer;n.direction=-1;n.velocity=Vector2.Zero;n.life=n.lifeMax=100000;n.defense=0;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);return n;
        }
        private static void MovingRuntime(object context,bool rangeCase=false,int sourceMode=0)
        {
            // A finite normal-cadence native driver, following the existing
            // SelectionPreview.Step ordering but using Main's real whole pass.
            // No PublishFixture, synthetic path,1000ms scope or direct solver.
            bool oldSourceGate=Environment.GetEnvironmentVariable("JMR_HELD_SOURCE_OLD")=="1",sourceTrace=Environment.GetEnvironmentVariable("JMR_HELD_SOURCE_TRACE")=="1";var observation=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");var input=Get(context,"Input");var work=(CombatPredictionWork)Get(observation,"PredictionWork");
            var p=Scene(context);p.inventory[p.selectedItem].SetDefaults(3475);p.inventory[54].SetDefaults(1179);p.inventory[54].stack=999;p.itemAnimation=p.itemTime=0;p.releaseUseItem=true;Main.screenPosition=new Vector2(rangeCase?800:500,400);
            // 207 has three updates and each native birth component is <16.
            // At900px this tiny moving receiver cannot be reached in the old
            // sixteen-outers window; the farther second receiver stays behind
            // it and inside the same original120-tile arena.
            for(int x=5;x<Main.maxTilesX-5;x++)NativeToolsChecks.Tile(x,43,1);var selected=RuntimeNpc(2,new Vector2(p.Center.X+(rangeCase?900:210),648));var other=RuntimeNpc(1,new Vector2(p.Center.X+(rangeCase?950:620),648));var start=selected.position;var otherStart=other.position;
            var world=typeof(Main).GetMethod("UpdateWorld_Projectiles",Flags);if(Main.instance.SpelunkerProjectileHelper==null)Main.instance.SpelunkerProjectileHelper=new Terraria.GameContent.SpelunkerProjectileHelper();if(Main.instance.ChumBucketProjectileHelper==null)Main.instance.ChumBucketProjectileHelper=new Terraria.GameContent.ChumBucketProjectileHelper();
            var control=Get(attack,"Control");var audit=new Harmony("JueMingR.Tests.HeldRuntimeWorld");audit.Patch(control.GetType().GetMethod("Prepare",Flags),prefix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(GateCompleted)));audit.Patch(control.GetType().GetMethod("SolveEntry",Flags),prefix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(GateSolve)));audit.Patch(model.GetMethod("CaptureForSolve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(GateCapture)));audit.Patch(world,prefix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(AfterProjectileWorldPreparation)){priority=Priority.Last});
            if(sourceMode>0)audit.Patch(host.GetType("JueMingR.TerrariaHost.Combat.NpcPredictionSource",true).GetMethod("PrepareSample",Flags),prefix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(GateSource)));
            audit.Patch(typeof(Projectile).GetMethod("AI",Flags),prefix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(BeforeMovingAi)){priority=Priority.First+1000});audit.Patch(attack.GetType().GetMethod("RecordInput",Flags),postfix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(MovingReceipt)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(MovingBorn)));
            int firstEnhanced=0,restoredEnhanced=0,e1=0,e2=0,releaseInputs=0,born=0,completedCalls=0,maxRequested=0,maxPublished=0,maxRange=16,contactRangeDrops=0,nextContactRange=0,nextContactFrame=0,cooldownEpochs=0,readyRebuilds=0;bool publicationEmpty=false;string firstParentKey=null,restoredParentKey=null;runtimeFirstBodies.Clear();runtimeRestoredBodies.Clear();runtimeSwapBodies.Clear();runtimeFirstStrikes=runtimeRestoredStrikes=runtimeSwapStrikes=0;runtimeWatch=true;runtimeSwap=false;
            NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,sourceMode!=1,false,false,true,25,true,true));
            try
            {
                for(int frame=1;frame<=70;frame++)
                {
                    bool held=frame<=32 || frame>=39;runtimeStage=frame<=32?1:frame>=39?2:0;currentTick=frame;var raw=selected.Center-new Vector2(450,0);
                    if(sourceMode>0 && frame==52){p.inventory[54].SetDefaults(ContentSamples.ItemsByType.Values.First(i=>i.ammo==AmmoID.Bullet && i.shoot==89).type);p.inventory[54].stack=999;runtimeSwap=true;Console.WriteLine("HELD SOURCE actual ammo change at52 shoot=89; existing controller key retained");}
                    NativeToolExecutionChecks.Sample(context,input,raw,held);Call(combat,"Sample");Call(observation,"SampleMouse");int mouse=Main.mouseX;NativeQuickItemChecks.BeginWorldStep();
                    var beforeParent=Main.projectile.FirstOrDefault(q=>q.active && q.owner==p.whoAmI && q.type==615);bool cooling=sourceMode>0 && held && beforeParent!=null && beforeParent.extraUpdates==0 && beforeParent.ai[1]>1 && p.itemAnimation==2 && p.itemTime==2;bool actionBefore=sourceMode==0 || (bool)Get(attack,"ActionPreparationNeeded");int priorRange=16;var priorEntries=(System.Collections.IDictionary)Get(control,"shots");if(beforeParent!=null && priorEntries.Contains((int)beforeParent.key))priorRange=(int)Get(priorEntries[(int)beforeParent.key],"RequiredRange");if(sourceMode>0){gateSource=gateSolve=gateCapture=0;gateWatch=true;}
                    if(rangeCase && frame==1)
                    {var selectionType=host.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true);float radius=(float)Static(selectionType,"PlayerRadius",p.Center);var box=(Rectangle)Static(selectionType,"ReceiveBounds",selected);var nearest=new Vector2(MathHelper.Clamp(p.Center.X,box.Left,box.Right),MathHelper.Clamp(p.Center.Y,box.Top,box.Bottom));Console.WriteLine("HELD RANGE ADMISSION beforeOpening=True player="+p.Center+" selected="+selected.Center+" nearestDistance="+Vector2.Distance(p.Center,nearest)+" radius="+radius+" camera="+Main.screenPosition+" screen="+Main.screenWidth+"x"+Main.screenHeight+" zoom="+Main.GameViewMatrix.ZoomMatrix.M11);Require(Vector2.DistanceSquared(p.Center,nearest)<=radius*radius,"900px normal driver starts inside the actual unchanged screen-derived selection radius before native opening");}
                    p.Update(p.whoAmI);
                    var parent=Main.projectile.FirstOrDefault(q=>q.active && q.owner==p.whoAmI && q.type==615);var entries=(System.Collections.IDictionary)Get(Get(attack,"Control"),"shots");movingParent=parent;movingEntry=parent!=null && entries.Contains((int)parent.key)?entries[(int)parent.key]:null;movingSelected=selected;movingOther=other;movingEdit=0;movingRoot=null;movingPlan=null;movingBeforeAi=movingActualSource=false;movingKind=null;movingPreparations=movingBorn=movingRecordInputs=0;
                    int rangeBefore=movingEntry==null?16:(int)Get(movingEntry,"RequiredRange");if(parent!=null && runtimeStage==1 && firstParentKey==null)firstParentKey=parent.key.ToString();if(parent!=null && runtimeStage==2 && restoredParentKey==null){restoredParentKey=parent.key.ToString();Require(restoredParentKey!=firstParentKey && rangeBefore==16,"actual restored controller has a new native key and no inherited enlarged range");}
                    float readyAi1=parent?.ai[1]??-1;foreach(var n in Main.npc)if(n.active)n.UpdateNPC(n.whoAmI);movingWatch=parent!=null;world.Invoke(Main.instance,null);movingWatch=false;born+=movingBorn;double preparedUsed=work.UsedMilliseconds;bool preparedExceeded=work.Exceeded;
                    int producerCalls=gateSource,sourceSolves=gateSolve,sourceCaptures=gateCapture;if(sourceMode>0)gateWatch=false;
                    var actualCache=(NpcPredictionCache)Get(Get(observation,"Prediction"),"Cache");int requested=actualCache.Required,published=actualCache.Read(1)?.Count??0,rangeAfter=movingEntry==null?16:(int)Get(movingEntry,"RequiredRange");maxRequested=Math.Max(maxRequested,requested);maxPublished=Math.Max(maxPublished,published);maxRange=Math.Max(maxRange,rangeAfter);
                    if(cooling && !oldSourceGate){cooldownEpochs++;Require(!actionBefore && published==0 && sourceSolves==0 && sourceCaptures==0 && rangeAfter==priorRange,"actual native2/2 cooldown releases action publication without solving or losing finite range");if(sourceMode==1)Require(producerCalls==0 && requested==0,"Aim-only cooldown adds no Source future work when no independent reader owns a demand");publicationEmpty=true;}
                    if(nextContactFrame>0){Console.WriteLine("HELD SOURCE AFTER CONTACT priorFrame="+nextContactFrame+" frame="+frame+" desiredRange="+nextContactRange+" actualRequired="+requested+" actualPublished="+published+" sameKey="+(parent!=null && parent.key.ToString()==firstParentKey));nextContactFrame=0;}
                    if(movingPlan!=null){Require(rangeAfter==Math.Max(16,Math.Min(120,movingPlan.Tick+8)),"actual successful post-NPC Contact reduces future demand to its receive plus8");contactRangeDrops++;}
                    if(movingPlan!=null){nextContactRange=rangeAfter;nextContactFrame=frame;}
                    bool enhanced=movingActualSource && movingKind=="Enhanced";
                    if(sourceMode>0 && !oldSourceGate && enhanced && publicationEmpty){Require(published>0 && producerCalls>0,"the actual ready pass rebuilds a real action publication after cooldown release");readyRebuilds++;publicationEmpty=false;}
                    if(enhanced && runtimeStage==1){e1++;if(firstEnhanced==0)firstEnhanced=frame;}if(enhanced && runtimeStage==2){e2++;if(restoredEnhanced==0)restoredEnhanced=frame;}if(enhanced && runtimeStage==0)releaseInputs++;
                    Require(Main.mouseX==mouse,"full ordinary held Main pass returns physical cursor");gateCompleted=gateSolve=gateCapture=0;gateWatch=true;try{Call(context,"UpdateRuntime");Call(context,"UpdateShell");}finally{gateWatch=false;}Require(gateSolve==0 && gateCapture==0,"real normal CompletedWorld runtime/shell tail does not repeat finite input solve/capture");completedCalls+=gateCompleted;
                    if(runtimeStage==0)Require(movingEntry==null || (int)Get(movingEntry,"RequiredRange")==16,"real physical release clears enlarged finite-held demand without a synthetic reset");
                    if(sourceTrace || frame<=6 || frame==firstEnhanced || frame==restoredEnhanced || frame==32 || frame==33 || frame==38 || frame==39 || frame==70)
                        Console.WriteLine("HELD RUNTIME frame="+frame+" stage="+runtimeStage+" input="+movingKind+" recordInputs="+movingRecordInputs+" source="+movingActualSource+" births="+movingBorn+" root="+movingBeforeAi+" path="+published+" requested="+requested+" requiredRange="+rangeBefore+"->"+rangeAfter+" parentKey="+parent?.key+" used6ms="+work.UsedMilliseconds+" exceeded="+work.Exceeded+" selected="+selected.position+" other="+other.position+" sourceMode="+sourceMode+" cooldown="+cooling+" actionNeeded="+actionBefore+" producerCalls="+producerCalls+" readyAi1="+readyAi1+" solveCalls="+sourceSolves+" captures="+sourceCaptures+" contact="+movingPlan?.Tick+" preparedUsed="+preparedUsed+" preparedExceeded="+preparedExceeded+" firstBodyStrikes="+runtimeFirstStrikes+" restoredBodyStrikes="+runtimeRestoredStrikes+" swapBodyStrikes="+runtimeSwapStrikes+" oldSourceGate="+oldSourceGate);
                }
                if(sourceMode==0)Require(completedCalls>0,"the real runtime tail actually visits CompletedWorld control preparation");Console.WriteLine("HELD REAL TAIL CompletedWorldPrepare="+completedCalls+" finiteSolve=0 familyCapture=0");
                Require(selected.position!=start && other.position!=otherStart,"both receivers retain their actual original ground AI/motion");
                Require(firstEnhanced>0 && firstEnhanced<=32 && restoredEnhanced>=39 && e1>0 && e2>0 && releaseInputs==0 && runtimeFirstBodies.Count>0 && runtimeRestoredBodies.Count>0 && runtimeFirstStrikes>0 && runtimeRestoredStrikes>0,"ordinary real Source/player/twoNPC/Main chain recovers Enhanced, actual born and same-body Strike before and after release without synthetic preparation");
                Require(contactRangeDrops>0 && restoredParentKey!=null,"real normal chain contains successful range fallback and native source identity recovery");if(rangeCase)Require(maxPublished>17 && maxRange>16,"nonseparable207 obtains actually published larger Source coverage instead of declaring a short-prefix miss");Console.WriteLine("HELD SOURCE RANGE case="+rangeCase+" requestedMax="+maxRequested+" publishedMax="+maxPublished+" rangeMax="+maxRange+" actualContacts="+contactRangeDrops+" firstKey="+firstParentKey+" restoredKey="+restoredParentKey);
                if(sourceMode>0){Console.WriteLine("HELD SOURCE GATE runtimeMode="+sourceMode+" cooldownEpochs="+cooldownEpochs+" readyRebuilds="+readyRebuilds+" actual89Born="+runtimeSwapBodies.Count+" sameBody89Strikes="+runtimeSwapStrikes+" original70GUC=True noSyntheticSource=True");Require((oldSourceGate || cooldownEpochs>0 && readyRebuilds>0) && runtimeSwapBodies.Count>0 && runtimeSwapStrikes>0,"actual Source gating retains cold/continued/released/new-key and changed-ammo Enhanced native receipts and same-body Strikes");}
                Console.WriteLine("PASS HELD RUNTIME70 real source six-ms coldFirstEnhanced="+firstEnhanced+" restored="+restoredEnhanced+" receipts="+e1+"/"+e2+" enhancedBorn="+runtimeFirstBodies.Count+"/"+runtimeRestoredBodies.Count+" enhancedStrikes="+runtimeFirstStrikes+"/"+runtimeRestoredStrikes+" totalNativeBorn="+born+" releaseEnhanced="+releaseInputs);
            }
            finally{gateWatch=runtimeWatch=movingWatch=runtimeSwap=false;movingEntry=movingRoot=null;movingParent=null;audit.UnpatchAll(audit.Id);}
        }
        private static void PublishOffsetComponent(object observation,NPC n)
        {
            // Post-NPC component publication: preserve the real native smoothed
            // current owner body. Future rows use the original AI0 friction and
            // native smoothing; this is not Source-cost proof.
            var cache=(NpcPredictionCache)Get(Get(observation,"Prediction"),"Cache");var prior=cache.Read(1);var offset=n.netOffset;var position=n.position;var velocity=n.velocity;var points=new NpcTrajectoryPoint[121];
            for(int i=0;i<points.Length;i++)
            {
                points[i]=new NpcTrajectoryPoint(i,new NpcMotionState{Identity=prior.Identity,X=position.X,Y=position.Y,Width=n.width,Height=n.height,Vx=velocity.X,Vy=velocity.Y,A0=n.ai[0],Wet=n.wet,NoTileCollide=n.noTileCollide,CanReceive=true,Active=true,NetOffsetX=offset.X,NetOffsetY=offset.Y});
                velocity.X*=.93f;if(velocity.X>-.1f && velocity.X<.1f)velocity.X=0;position+=velocity;
                if(offset!=Vector2.Zero){float length=offset.Length(),step=2+length/Main.multiplayerNPCSmoothingRange*2;var movement=offset;movement.Normalize();movement*=step;offset-=movement;if(offset.Length()<step)offset=Vector2.Zero;}
            }
            cache.Publish(new NpcTrajectory(prior.Identity,Main.GameUpdateCount,prior.Version+1,PredictionAssumption.NetworkObservation,PredictionStop.None,points,points.Length));
        }
        private static bool costWatch;private static int costCandidates,costCopies,costPrefixes,costFrames,costSteps,costDefaults,costKills;private static Vector2 costOrigin;
        private static readonly System.Collections.Generic.List<Vector2> costDirections=new System.Collections.Generic.List<Vector2>();
        private static void CostDefault(){if(costWatch)costDefaults++;}
        private static void CostKill(){if(costWatch)costKills++;}
        private static void CostCandidate(Vector2 __6){if(costWatch){costCandidates++;costDirections.Add((__6-costOrigin).SafeNormalize(Vector2.UnitX));}}
        private static void CostCopy(){if(costWatch)costCopies++;}
        private static void CostPrefix(){if(costWatch)costPrefixes++;}
        private static void CostFrame(){if(costWatch)costFrames++;}
        private static void CostStep(){if(costWatch)costSteps++;}
        private static void CandidateCost(object context)
        {
            // Isolated mechanism workload only; no ordinary Main cost claim.
            // Keep the complete receiver scene and a real full-height wall,
            // so every proposed direction must prove its own terrain miss.
            var audit=new Harmony("JueMingR.Tests.HeldCandidateCost");audit.Patch(model.GetMethod("ExplicitBirthBatchCore",Flags)??model.GetMethod("ExplicitBirthBatch",Flags),prefix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(CostCandidate)));audit.Patch(model.GetMethod("Copy",Flags),prefix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(CostCopy)));audit.Patch(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPlayerPrefix",true).GetConstructors(Flags).Single(),prefix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(CostPrefix)));audit.Patch(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPlayerFrame",true).GetMethod("Capture",Flags),prefix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(CostFrame)));audit.Patch(typeof(PlayerMotionContinuation).GetMethod("Advance",Flags),prefix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(CostStep)));
            audit.Patch(typeof(Projectile).GetMethod("SetDefaults",Flags,null,new[]{typeof(int)},null),prefix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(CostDefault)));audit.Patch(model.GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(CostKill)));
            try
            {
                foreach(bool hardMiss in new[]{false,true})
                {
                    var p=Scene(context);p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;for(int x=5;x<Main.maxTilesX-5;x++)NativeToolsChecks.Tile(x,43,1);var n=Target(p.Center+new Vector2(210,0));n.width=n.height=100;n.Center=p.Center+new Vector2(210,0);if(hardMiss)for(int y=5;y<Main.maxTilesY-5;y++)NativeToolsChecks.Tile(60,y,1);
                    int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),p.MountedCenter,Vector2.UnitX,615,p.GetWeaponDamage(p.HeldItem),0,p.whoAmI);var parent=Main.projectile[slot];parent.ai[0]=10;parent.ai[1]=1;var path=Path(n);object[] args={p,parent,path,Clock(path),Terrain(),null,null,null};costCandidates=costCopies=costPrefixes=costFrames=costSteps=costDefaults=costKills=0;costOrigin=p.RotatedRelativePoint(p.MountedCenter);costDirections.Clear();AttackContact plan;costWatch=true;var timer=System.Diagnostics.Stopwatch.StartNew();try{using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))plan=(AttackContact)Static(host.GetType("JueMingR.TerrariaHost.Combat.HostHeldAttack",true),"Solve",args);}finally{costWatch=false;timer.Stop();}
                    var aim=(Vector2)args[5];Main.mouseX=(int)(aim.X-Main.screenPosition.X);Main.mouseY=(int)(aim.Y-Main.screenPosition.Y);parent.AI();var actual=Main.projectile.Where(q=>q.active && q.type==14).ToArray();Require(actual.Length==1,"workload retains the real single ordinary14 controller birth");parent.active=false;Advance();Require(hardMiss?first==0:first>0,"same native controller/world keeps the positive Strike and full-height wall negative");
                    Console.WriteLine("HELD CANDIDATE native hardMiss="+hardMiss+" actualBirth="+actual.Length+" firstStrike="+first);
                    Require((plan==null)==hardMiss,"ordinary14 finite receiver keeps useful positive and actual full-height wall miss");if(model.GetMethod("Proposals",Flags)!=null){Require(costCandidates>0 && costCopies==2*costCandidates && costDefaults==2,"each candidate still has its private born/prototype copies; repeated SetDefaults is absent");if(hardMiss){Require(costSteps<121,"complete native source retirement ends replay before the unused owner horizon");for(int angle=0;angle<32;angle++)Require(costDirections.Any(d=>Vector2.Dot(d,(angle*MathHelper.TwoPi/32).ToRotationVector2())>.999999f),"all original32 angular fallbacks remain after proposals");}}else Require(costCandidates==(hardMiss?32:1) && costCopies==costCandidates,"all original directions retain their independent private projectile replay");if(model.GetMethod("ExplicitBirthBatchCore",Flags)!=null)Require(costPrefixes==1 && costFrames==2 && costSteps<=121,"one solve shares only one owner prefix and at most one complete120-action continuation");else Require(costPrefixes==costCandidates,"baseline really constructs one owner prefix per candidate");
                    Console.WriteLine("HELD CANDIDATE COST hardMiss="+hardMiss+" plan="+plan?.Tick+" candidates="+costCandidates+" projectileCopies="+costCopies+" ownerPrefixes="+costPrefixes+" ownerCaptures="+costFrames+" playerAdvance="+costSteps+" projectileDefaults="+costDefaults+" memberKills="+costKills+" diagnosticMs="+timer.Elapsed.TotalMilliseconds+" allowance=1000_component_only");
                }
            }
            finally{costWatch=false;audit.UnpatchAll(audit.Id);}
        }
        private static bool gateWatch;private static int gateCore,gateSolve,gateCapture,gateSource,gateCompleted;
        private static void GateCompleted(object __1){if(gateWatch && Convert.ToString(__1)=="CompletedWorld")gateCompleted++;}
        private static void GateCore(){if(gateWatch)gateCore++;}
        private static void GateSolve(){if(gateWatch)gateSolve++;}
        private static void GateCapture(){if(gateWatch)gateCapture++;}
        private static void GateSource(){if(gateWatch)gateSource++;}
        private static void SourceGates(object context)
        {
            var observation=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");var control=Get(attack,"Control");var input=Get(context,"Input");
            string[] names={"native-cooldown","ready","unusual-extra","no-registration","invalid-registration","new-opening","one-ready-of-two","nonfinite-neighbor"};
            for(int mode=0;mode<names.Length;mode++)
            {
                var p=Scene(context);p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;p.itemAnimation=p.itemTime=0;p.releaseUseItem=true;var n=Target(p.Center+new Vector2(210,0));n.width=n.height=100;n.Center=p.Center+new Vector2(210,0);
                NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,false,false,false,true,25,true,true));NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,n,0);
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var parent=Main.projectile.Single(q=>q.active && q.type==615);parent.ai[1]=3;parent.AI();
                Require(p.itemAnimation==2 && p.itemTime==2,"original AI075 supplies both real dummy timers for the Source cooldown gate");
                var cacheOwner=(NpcPredictionCache)Get(Get(observation,"Prediction"),"Cache");var path=cacheOwner.Read(1);cacheOwner.Release(0); // PublishFixture owned this synthetic display request; restore actual Aim-only ownership.
                var entries=(System.Collections.IDictionary)Get(control,"shots");var entry=entries[(int)parent.key];entry.GetType().GetField("RequiredRange",Flags).SetValue(entry,64);entry.GetType().GetField("RangeTarget",Flags).SetValue(entry,path.Identity);control.GetType().GetField("finiteHeldDemandRaised",Flags).SetValue(control,true);
                if(mode==1)parent.ai[1]=1;if(mode==2)parent.extraUpdates=1;if(mode==3)Call(control,"Reset");if(mode==4)parent.active=false;
                if(mode==5){p.itemAnimation=p.itemTime=0;p.releaseUseItem=true;}
                if(mode==6){typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var sibling=Main.projectile.Single(q=>q.active && q.type==615 && !ReferenceEquals(q,parent));sibling.ai[1]=1;Require(entries.Contains((int)sibling.key),"second ready controller has a real independent registered native birth");}
                if(mode==7)p.HeldItem.SetDefaults(3278);
                bool needed=(bool)Get(control,"FiniteHeldActionNeeded"),action=(bool)Get(attack,"ActionPreparationNeeded");Call(attack,"Demand",true);var cache=(NpcPredictionCache)Get(Get(observation,"Prediction"),"Cache");int required=cache.Required,demand1=((int[])Get(cache,"demands"))[1];
                Console.WriteLine("HELD SOURCE PREDICATE mode="+names[mode]+" needed="+needed+" action="+action+" actualTimers="+p.itemAnimation+"/"+p.itemTime+" ai1="+parent.ai[1]+" extra="+parent.extraUpdates+" requiredGlobal="+required+" actionDemand1="+demand1+" retainedRange="+Get(entry,"RequiredRange")+" componentOnly=True");
                Require(needed==(mode!=0),"only a fully known native cooldown can suppress the shared action predicate: "+names[mode]);
                if(mode==0)
                {
                    Require(!action && demand1==0 && required==0 && (int)Get(entry,"RequiredRange")==64 && ((NpcIdentity)Get(entry,"RangeTarget")).Equals(path.Identity),"Aim-only cooldown releases action Demand1 and its publication without erasing finite range ownership");
                    AttackContact preview;using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))preview=(AttackContact)Call(control,"Preview",path,Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld"),0,false);
                    Require(preview!=null,"independent read-only future controller preview remains complete during a gated action cooldown");
                }
            }
            Console.WriteLine("PASS HELD Source predicate8 native/timer/registration conservatism and independent readonly preview; component only");
        }
        private static int sourceResolveCalls;
        private static void SourceResolve(){sourceResolveCalls++;}
        private static void SourceGateTail(object context)
        {
            var observation=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");var control=Get(attack,"Control");var input=Get(context,"Input");
            foreach(int weapon in new[]{3540,3854})foreach(bool ready in new[]{false,true})
            {
                var p=Scene(context);p.HeldItem.SetDefaults(weapon);p.inventory[54].SetDefaults(40);p.inventory[54].stack=999;p.itemAnimation=p.itemTime=0;p.releaseUseItem=true;
                var n=Target(p.Center+new Vector2(210,0));NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,false,false,false,true,25,true,true));NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var parent=Main.projectile.Single(q=>q.active && q.type==p.HeldItem.shoot);parent.ai[1]=ready?2:3;parent.AI();
                bool needed=(bool)Get(control,"FiniteHeldActionNeeded"),action=(bool)Get(attack,"ActionPreparationNeeded");
                Console.WriteLine("HELD SOURCE TAIL controller="+parent.type+" nativeAi1="+parent.ai[1]+" timers="+p.itemAnimation+"/"+p.itemTime+" ready="+ready+" needed="+needed+" action="+action);
                Require(p.itemAnimation==2 && p.itemTime==2 && parent.ai[1]==(ready?1:2) && needed==ready && action==ready,"actual630/705 native cooldown and upcoming mouse-read use the same Source gate");
            }
            {
                var p=Scene(context);p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;p.itemAnimation=p.itemTime=0;p.releaseUseItem=true;Main.screenPosition=new Vector2(500,400);
                for(int x=5;x<Main.maxTilesX-5;x++)NativeToolsChecks.Tile(x,43,1);var n=Target(p.Center+new Vector2(210,0));var other=Target(p.Center+new Vector2(360,0),1);
                NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,false,true,false,true,25,true,true));NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var parent=Main.projectile.Single(q=>q.active && q.type==615);
                var audit=new Harmony("JueMingR.Tests.HeldSourceClearLine");audit.Patch(attack.GetType().GetMethod("ResolveSelection",Flags),prefix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(SourceResolve)));sourceResolveCalls=0;int warm=0;
                try
                {
                    for(int epoch=0;epoch<6;epoch++)
                    {
                        NativeQuickItemChecks.BeginWorldStep();parent.ai[1]=30;parent.AI();NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");
                        Require(!(bool)Get(attack,"ActionPreparationNeeded"),"native cooldown gate remains false while ClearLine owns its independent candidate work");
                        long before=(long)Get(attack,"candidateWarmTick");Call(observation,"Update",Main.GameUpdateCount);long after=(long)Get(attack,"candidateWarmTick");int count=(int)Get(Get(observation,"Selection"),"CandidateCount");if(after>before)warm++;
                        Console.WriteLine("HELD SOURCE CLEARLINE epoch="+epoch+" candidates="+count+" resolveCalls="+sourceResolveCalls+" warmTick="+before+"->"+after+" actionNeeded="+Get(attack,"ActionPreparationNeeded"));
                        Require(count==2,"ClearLine sample preserves both real eligible receivers");
                    }
                    Require(sourceResolveCalls>=6 && warm>0,"actual UpdateSample enters ClearLine ResolveSelection and its independent warm cadence despite the finite action cooldown gate");
                }
                finally{audit.UnpatchAll(audit.Id);}
            }
            Console.WriteLine("PASS HELD Source tail four630/705 predicates and real ClearLine UpdateSample two-candidate warm; bounded component, no ordinary cost claim");
        }
        private static void RangeLifecycle(object context)
        {
            // Reachable enlarged-demand tuples, explicitly component-only.
            // The separate70GUC/900px driver never assigns these fields.
            var observation=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");var control=Get(attack,"Control");var input=Get(context,"Input");
            foreach(string boundary in new[]{"off","ui","cancel","new-generation","closer-wall-open"})
            {
                var p=Scene(context);p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;p.itemAnimation=p.itemTime=0;p.releaseUseItem=true;Main.screenPosition=new Vector2(500,400);
                for(int x=5;x<Main.maxTilesX-5;x++)NativeToolsChecks.Tile(x,43,1);var n=Target(p.Center+new Vector2(600,0));n.width=n.height=80;n.Center=p.Center+new Vector2(600,0);
                NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,false,true,25,true,true));NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,n,0);
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var parent=Main.projectile.Single(q=>q.active && q.type==615);parent.ai[0]=10;parent.ai[1]=1;
                var path=(NpcTrajectory)Call(Get(Get(observation,"Prediction"),"Cache"),"Read",1);var entries=(System.Collections.IDictionary)Get(control,"shots");var entry=entries[(int)parent.key];entry.GetType().GetField("RequiredRange",Flags).SetValue(entry,120);entry.GetType().GetField("RangeTarget",Flags).SetValue(entry,path.Identity);control.GetType().GetField("finiteHeldDemandRaised",Flags).SetValue(control,true);
                Require((int)Call(control,"RequiredFiniteHeldRange",path.Identity)==120,"correct source/target/edge tuple owns the heavy component range");bool oldChat=Main.drawingPlayerChat;
                try
                {
                    if(boundary=="off")NativeCombatObservationChecks.Save(observation,new ObservationOptions());
                    if(boundary=="ui")Main.drawingPlayerChat=true;
                    if(boundary=="cancel"){p.channel=p.controlUseItem=false;p.itemAnimation=p.itemTime=0;NativeToolExecutionChecks.Sample(context,input,n.Center,false);Call(combat,"Sample");}
                    if(boundary=="off" || boundary=="ui" || boundary=="cancel")
                    {
                        Call(attack,"Demand",false);Require(!(bool)Get(control,"finiteHeldDemandRaised") && (int)Call(control,"RequiredFiniteHeldRange",path.Identity)==16,"real permission/channel withdrawal clears heavy demand: "+boundary);
                        Call(attack,"Demand",false);Require(!(bool)Get(control,"finiteHeldDemandRaised") && (int)Call(control,"RequiredFiniteHeldRange",path.Identity)==16,"repeated withdrawn demand remains retired without a new heavy edge: "+boundary);
                        Console.WriteLine("HELD RANGE LIFECYCLE boundary="+boundary+" range120->16 edge=False repeatedStable=True componentOnly=True");continue;
                    }
                    if(boundary=="new-generation")
                    {var prior=path.Identity;n=Target(p.Center+new Vector2(210,0));var next=(NpcIdentity)Static(host.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,prior.Session);Require(!ReferenceEquals(next.Token,prior.Token) && !next.Equals(prior) && (int)Call(control,"RequiredFiniteHeldRange",next)==16,"actual original NPC replacement identity cannot inherit the old target's enlarged demand");}
                    else
                    {
                        for(int y=5;y<43;y++)NativeToolsChecks.Tile(70,y,1);
                        for(int y=5;y<43;y++)Main.tile[70,y].ClearEverything();n.Center=p.Center+new Vector2(210,0);
                    }
                    NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,n,0,phaseName:"BeforeNpc");n.UpdateNPC(n.whoAmI);path=(NpcTrajectory)Call(Get(Get(observation,"Prediction"),"Cache"),"Read",1);
                    using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))Call(control,"PrepareProjectileWorld",path);
                    var contact=GetOptional(entry,"Contact") as AttackContact;int required=(int)Get(entry,"RequiredRange");Console.WriteLine("HELD RANGE LIFECYCLE boundary="+boundary+" contact="+contact?.Tick+" range120->"+required+" generation="+path.Identity.Generation+" componentOnly=True");
                    Require(contact!=null && required==Math.Max(16,Math.Min(120,contact.Tick+8)) && required<120 && ((NpcIdentity)Get(entry,"RangeTarget")).Equals(path.Identity),"same production core replaces stale heavy demand with the actual near/new-target Contact plus8: "+boundary);
                }
                finally{Main.drawingPlayerChat=oldChat;}
            }
        }
        private static void WorldGates(object context)
        {
            var observation=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");var control=Get(attack,"Control");var input=Get(context,"Input");var world=typeof(Main).GetMethod("UpdateWorld_Projectiles",Flags);
            if(Main.instance.SpelunkerProjectileHelper==null)Main.instance.SpelunkerProjectileHelper=new Terraria.GameContent.SpelunkerProjectileHelper();if(Main.instance.ChumBucketProjectileHelper==null)Main.instance.ChumBucketProjectileHelper=new Terraria.GameContent.ChumBucketProjectileHelper();
            var audit=new Harmony("JueMingR.Tests.HeldWorldGates");audit.Patch(control.GetType().GetMethod("Prepare",Flags),prefix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(GateCompleted)));audit.Patch(control.GetType().GetMethod("PrepareProjectileWorld",Flags),prefix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(GateCore)));audit.Patch(control.GetType().GetMethod("SolveEntry",Flags),prefix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(GateSolve)));audit.Patch(model.GetMethod("CaptureForSolve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(GateCapture)));audit.Patch(host.GetType("JueMingR.TerrariaHost.Combat.NpcPredictionSource",true).GetMethod("PrepareSample",Flags),prefix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(GateSource)));audit.Patch(attack.GetType().GetMethod("RecordInput",Flags),postfix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(MovingReceipt)));
            try
            {
                for(int mode=0;mode<9;mode++)
                {
                    var p=Scene(context);if(mode>=3)p.inventory[p.selectedItem].SetDefaults(new[]{3475,3540,3854}[(mode-3)/2]);p.phantomPhoneixCounter=mode==8?2:0;p.inventory[54].SetDefaults(mode>=5?40:97);p.inventory[54].stack=999;p.itemAnimation=p.itemTime=0;p.releaseUseItem=true;Main.screenPosition=new Vector2(500,400);for(int x=5;x<Main.maxTilesX-5;x++)NativeToolsChecks.Tile(x,43,1);var n=Target(p.Center+new Vector2(210,0));n.width=n.height=100;n.Center=p.Center+new Vector2(210,0);
                    NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,false,true,25,true,true));NativeToolExecutionChecks.Sample(context,input,n.Center-new Vector2(450,0),true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,n,0);typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var parent=Main.projectile.Single(q=>q.active && q.type==p.HeldItem.shoot);parent.ai[0]=10;parent.ai[1]=mode>=3?2+(mode-3)%2:1;
                    NativeQuickItemChecks.BeginWorldStep();n.velocity=Vector2.UnitX;NativeToolExecutionChecks.Sample(context,input,n.Center-new Vector2(450,0),true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,n,1,phaseName:"BeforeNpc");n.UpdateNPC(n.whoAmI);
                    if(mode>=3)
                    {
                        var timeline=(NpcTrajectory)Call(Get(Get(observation,"Prediction"),"Cache"),"Read",1);var phase=Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");gateCore=gateSolve=gateCapture=gateSource=gateCompleted=0;gateWatch=true;try{Call(control,"Prepare",timeline,phase);}finally{gateWatch=false;}Require(gateCompleted==1 && gateSolve==0 && gateCapture==0,"actual CompletedWorld control preparation retires finite input without replaying it");
                        gateSolve=gateCapture=0;AttackContact view;gateWatch=true;try{using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))view=(AttackContact)Call(control,"Preview",timeline,phase,0,false);}finally{gateWatch=false;}Require(view!=null && gateSolve>0 && gateCapture>0,"same-phase readonly registered preview keeps its complete pending family/Phoenix Contact");Console.WriteLine("HELD COMPLETED/VIEW controller="+p.HeldItem.shoot+" counter="+p.phantomPhoneixCounter+" actualPrepareSolve=0 view="+view.Tick+" viewSolve="+gateSolve+" viewCapture="+gateCapture);
                    }
                    if(mode==0)NativeCombatObservationChecks.Save(observation,new ObservationOptions());if(mode==1)parent.active=false;
                    movingParent=parent;movingActualSource=false;movingKind=null;movingRecordInputs=0;movingWatch=true;gateCore=gateSolve=gateCapture=gateSource=0;gateWatch=true;int mouse=Main.mouseX;
                    // A positive mechanism scope calls the exact core used by
                    // the production Main prefix. Both subsequent passes are
                    // real Main entry calls in this same GUC; no Source refresh.
                    if(mode==2)
                    {
                        // Reachable heavy-demand state, explicitly COMPONENT
                        // only. The original real70GUC driver never injects it.
                        var path=(NpcTrajectory)Call(Get(Get(observation,"Prediction"),"Cache"),"Read",1);var entry=((System.Collections.IDictionary)Get(control,"shots"))[(int)parent.key];entry.GetType().GetField("RequiredRange",Flags).SetValue(entry,120);entry.GetType().GetField("RangeTarget",Flags).SetValue(entry,path.Identity);control.GetType().GetField("finiteHeldDemandRaised",Flags).SetValue(control,true);
                        Require((int)Call(control,"RequiredFiniteHeldRange",path.Identity)==120,"component begins at a correctly owned enlarged range including its edge flag");
                        var id=path.Identity;var nextGeneration=new NpcIdentity(id.Session,id.Token,id.Slot,id.Generation+1,id.Type,id.NetId);Require((int)Call(control,"RequiredFiniteHeldRange",nextGeneration)==16,"a different target generation cannot inherit the observed controller's heavy demand");
                        using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))Call(control,"PrepareProjectileWorld",path);
                        var plan=GetOptional(entry,"Contact") as AttackContact;Require(plan!=null && (int)Get(entry,"RequiredRange")==Math.Max(16,Math.Min(120,plan.Tick+8)) && (int)Get(entry,"RequiredRange")<120,"actual same-core Contact reduces the finite demand from120 to receive plus8");Console.WriteLine("HELD RANGE COMPONENT120->"+Get(entry,"RequiredRange")+" actualContact="+plan.Tick+"; not an ordinary Source opportunity");
                    }
                    world.Invoke(Main.instance,null);
                    if(mode>=3)
                    {
                        Require(gateCore==1 && gateSolve==0 && gateCapture==0 && gateSource==0 && !Main.projectile.Any(q=>q.active && !ReferenceEquals(q,parent)),"actual native cooldown AI has no input-bearing birth and adds zero full-family future work");gateWatch=movingWatch=false;
                        Console.WriteLine("HELD COOLDOWN controller="+parent.type+" initialAi1="+(2+(mode-3)%2)+" nextAi1="+parent.ai[1]+" core="+gateCore+" solve="+gateSolve+" capture="+gateCapture+" source="+gateSource);
                        while(parent.ai[1]>1)
                        {
                            NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,n.Center-new Vector2(450,0),true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,n,1,phaseName:"BeforeNpc");n.UpdateNPC(n.whoAmI);gateCore=gateSolve=gateCapture=gateSource=0;gateWatch=true;world.Invoke(Main.instance,null);gateWatch=false;Require(gateCore==1 && gateSolve==0 && gateCapture==0 && gateSource==0,"each remaining native cooldown phase keeps zero family solve/capture");
                        }
                        Require(parent.ai[1]==1,"the original cooldown reaches the real cursor-bearing phase");NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,n.Center-new Vector2(450,0),true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,n,1,phaseName:"BeforeNpc");n.UpdateNPC(n.whoAmI);gateCore=gateSolve=gateCapture=gateSource=0;movingActualSource=false;movingKind=null;int readyCounter=p.phantomPhoneixCounter;movingWatch=gateWatch=true;world.Invoke(Main.instance,null);gateWatch=movingWatch=false;
                        bool fired=movingActualSource && movingKind=="Enhanced";Console.WriteLine("HELD READY DETAIL controller="+p.HeldItem.shoot+" counter="+readyCounter+"->"+p.phantomPhoneixCounter+" core="+gateCore+" solve="+gateSolve+" capture="+gateCapture+" source="+gateSource+" kind="+movingKind+" actualSource="+movingActualSource+" bornTypes="+string.Join(",",Main.projectile.Where(q=>q.active && !ReferenceEquals(q,parent)).Select(q=>q.type))+" used6ms="+((CombatPredictionWork)Get(observation,"PredictionWork")).UsedMilliseconds+" exceeded="+((CombatPredictionWork)Get(observation,"PredictionWork")).Exceeded);Require(gateCore==1 && gateSolve==1 && (gateCapture>0 && (mode!=8 || readyCounter==2 && Main.projectile.Any(q=>q.active && q.type==706))) && gateSource==0 && fired && Main.projectile.Any(q=>q.active && !ReferenceEquals(q,parent)),"real ai1=1 production Main six-ms pass prepares once and consumes Enhanced with native birth");parent.active=false;
                        for(int action=1;action<=60 && first==0;action++){currentTick=action;NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(n.whoAmI);world.Invoke(Main.instance,null);}Require(first>0,"actual ready-phase Enhanced birth reaches native selected Strike");Console.WriteLine("HELD COOLDOWN READY controller="+p.HeldItem.shoot+" initialAi1="+(2+(mode-3)%2)+" actualEnhanced="+fired+" firstNativeStrike="+first+" ordinaryAllowance6ms=True syntheticSourceComponent=True");continue;
                    }
                    int solves=gateSolve,captures=gateCapture,sources=gateSource;bool enhanced=movingActualSource && movingKind=="Enhanced";
                    world.Invoke(Main.instance,null);gateWatch=movingWatch=false;
                    Require(Main.mouseX==mouse && gateSource==0 && gateSolve==solves && gateCapture==captures && gateSource==sources,"sameGUC real Main repeated pass never adds solve/capture/Source preparation and returns cursor");
                    if(mode<2)Require(gateCore==0 && gateSolve==0 && gateCapture==0 && !enhanced,"AimOFF with native parent / AimON without registered finite source adds zero future work");
                    else Require(gateSolve==1 && gateCapture>0 && enhanced,"actual finite component prepares once and reaches native Enhanced before sameGUC repeat");
                    Console.WriteLine("HELD WORLD GATE mode="+mode+" parentActive="+parent.active+" coreCalls="+gateCore+" solves="+gateSolve+" captures="+gateCapture+" sourcePrepareSample="+gateSource+" actualEnhanced="+enhanced+" sameGUCRepeatNoNewFuture=True");
                }
                Console.WriteLine("PASS HELD real Main OFF/no-finite/finite-repeat gates=3 plus6 real-controller cooldown→ready cases; synthetic Source mechanisms are not ordinary Source cost");
            }
            finally{gateWatch=movingWatch=false;movingParent=null;audit.UnpatchAll(audit.Id);}
        }
        private static void MovingConsumer(object context)
        {
            var observation=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");var input=Get(context,"Input");
            var world=typeof(Main).GetMethod("UpdateWorld_Projectiles",Flags);Require(world!=null && Main.instance!=null,"real Main projectile pass and native Main instance exist");
            // AimInitialize creates Main without a window/constructor. This
            // real world pass needs the two original field initializers used
            // by PreUpdateAllProjectiles; neither original helper is replaced.
            if(Main.instance.SpelunkerProjectileHelper==null)Main.instance.SpelunkerProjectileHelper=new Terraria.GameContent.SpelunkerProjectileHelper();
            if(Main.instance.ChumBucketProjectileHelper==null)Main.instance.ChumBucketProjectileHelper=new Terraria.GameContent.ChumBucketProjectileHelper();
            var audit=new Harmony("JueMingR.Tests.HeldWorldPhase");
            audit.Patch(world,prefix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(AfterProjectileWorldPreparation)){priority=Priority.Last});
            audit.Patch(typeof(Projectile).GetMethod("AI",Flags),prefix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(BeforeMovingAi)){priority=Priority.First+1000});
            audit.Patch(attack.GetType().GetMethod("RecordInput",Flags),postfix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(MovingReceipt)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatHeldAmmoFamilyChecks),nameof(MovingBorn)));
            var originalWork=(CombatPredictionWork)Get(observation,"PredictionWork");int cases=0,ordinaryEnhanced=0,ordinaryDeferred=0,oldOffsetDelay=NPC.offSetDelayTime,oldSmoothing=Main.multiplayerNPCSmoothingRange;bool oldDedicated=Main.dedServ;
            try
            {
                foreach(bool component in new[]{false,true})foreach(var row in new[]{new[]{3475,1179,615,207},new[]{3475,97,615,14},new[]{3540,1235,630,225},new[]{3540,40,630,1},new[]{3854,1235,705,225},new[]{3854,40,705,1}})
                foreach(int edit in row[2]==615?(row[3]==207?new[]{0,1,2,3,4}:(component?new[]{0,1,5,6,7,8}:new[]{0,1,5,6})):new[]{0})
                {
                    movingWatch=false;var p=Scene(context);p.inventory[p.selectedItem].SetDefaults(row[0]);p.inventory[54].SetDefaults(row[1]);p.inventory[54].stack=999;p.itemAnimation=p.itemTime=0;p.releaseUseItem=true;p.phantomPhoneixCounter=0;Main.screenPosition=new Vector2(500,400);
                    Require(p.inventory[54].shoot==row[3],"phase test uses the actual legal selected ammunition");for(int x=5;x<Main.maxTilesX-5;x++)NativeToolsChecks.Tile(x,43,1);
                    var n=Target(p.Center+new Vector2(210,0));n.width=n.height=100;n.Center=p.Center+new Vector2(210,0);var other=Target(p.Center+new Vector2(620,160),1);
                    NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,false,true,25,true,true));NativeToolExecutionChecks.Sample(context,input,n.Center-new Vector2(450,0),true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,n,0);
                    typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});
                    var parent=Main.projectile.Single(q=>q.active && q.type==row[2]);parent.ai[0]=10;parent.ai[1]=1;var entries=(System.Collections.IDictionary)Get(Get(attack,"Control"),"shots");Require(entries.Contains((int)parent.key),"phase test retains its real native Shoot registration");
                    NativeQuickItemChecks.BeginWorldStep();n.velocity=Vector2.UnitX;other.velocity=edit==4?Vector2.UnitX:Vector2.Zero;NativeToolExecutionChecks.Sample(context,input,n.Center-new Vector2(450,0),true);Call(combat,"Sample");
                    NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,n,1,phaseName:"BeforeNpc");movingEntry=entries[(int)parent.key];Require(GetOptional(movingEntry,"Contact")==null,"BeforeNpc does not solve a finite held family before native NPC motion");
                    // Like ReceiveShapes, suppress VFX only around native NPC
                    // smoothing. Restore the client before every real Main/AI
                    // consumer; dedServ must never grant or remove input here.
                    // AI0 is a legal smoothing index; synthetic -1 is not.
                    if(edit>=7){Main.dedServ=true;NPC.offSetDelayTime=0;Main.multiplayerNPCSmoothingRange=200;n.aiStyle=other.aiStyle=0;n.netOffset=new Vector2(16.1f,5.3f);}
                    var before=n.position;n.UpdateNPC(n.whoAmI);other.UpdateNPC(other.whoAmI);Require(edit>=7?n.velocity==new Vector2(.93f,0) && n.netOffset!=Vector2.Zero:n.position-before==Vector2.UnitX,"original26 follow exact one-pixel motion; offset cases retain original AI0 friction and actual nonzero smoothing");
                    if(edit>=7){Main.dedServ=oldDedicated;PublishOffsetComponent(observation,n);var owner=n.position+n.netOffset;var recovered=owner-n.position;Require(n.netOffset!=Vector2.Zero && recovered!=n.netOffset,"nonbinary native smoothed offset exposes stored-body roundtrip loss");Console.WriteLine("HELD OFFSET native="+n.netOffset+" owner="+owner+" subtraction="+recovered+" edit="+edit);}
                    // Ordinary runs first through Main's real six-ms owner.
                    // The separate component calls that hook's same core at
                    // postNPC under1000ms, then Main consumes it without a
                    // duplicate solve. This is not ordinary opportunity proof.
                    if(component)using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))Call(Get(attack,"Control"),"PrepareProjectileWorld",Call(Get(Get(observation,"Prediction"),"Cache"),"Read",1));
                    movingParent=parent;movingSelected=n;movingOther=other;movingEdit=edit;movingRoot=null;movingPlan=null;movingBeforeAi=movingActualSource=false;movingKind=null;movingPreparations=movingBorn=movingRecordInputs=0;currentTick=1;movingWatch=true;int mouse=Main.mouseX;
                    world.Invoke(Main.instance,null);movingWatch=false;
                    bool enhanced=movingKind=="Enhanced" && movingActualSource;bool negative=edit>=1 && edit<=3 || edit==5 || edit==6 || edit==8;
                    Console.WriteLine("HELD WORLD controller="+parent.type+" ammo="+row[3]+" component1000="+component+" edit="+edit+" otherMoving="+(edit==4)+" preparations="+movingPreparations+" contact="+movingPlan?.Tick+" rootBeforeAi="+movingBeforeAi+" actualInput="+movingKind+" actualSource="+movingActualSource+" recordInputs="+movingRecordInputs+" births="+movingBorn+" ordinaryWorkMs="+(component?(double?)null:originalWork.UsedMilliseconds));
                    Require(movingPreparations==1 && (negative || movingActualSource) && Main.mouseX==mouse,"one real Main prefix precedes native held AI input and cursor restoration");
                    if(component && edit!=4)Require(movingPlan!=null && enhanced==!negative,"complete component uses valid postNPC root and rejects each actual postprepare dependency edit");
                    if(negative)Require(!enhanced,"real phase mutation cannot consume Enhanced input");
                    if(!component && !negative){if(enhanced)ordinaryEnhanced++;else ordinaryDeferred++;}
                    Require(movingBorn>0,"native controller phase still produces its original selected-ammo birth");parent.active=false;
                    for(int tick=2;tick<=80 && first==0;tick++){currentTick=tick;NativeQuickItemChecks.BeginWorldStep();if(edit>=7)Main.dedServ=true;try{n.UpdateNPC(n.whoAmI);other.UpdateNPC(other.whoAmI);}finally{Main.dedServ=oldDedicated;}world.Invoke(Main.instance,null);}
                    if(component && !negative && edit!=4)Require(first>0,"actual enhanced postNPC controller birth reaches native selected Strike");if(edit>=7){Main.dedServ=oldDedicated;NPC.offSetDelayTime=oldOffsetDelay;Main.multiplayerNPCSmoothingRange=oldSmoothing;}
                    Console.WriteLine("HELD WORLD native first="+first+" hitType="+hitType+" doubleMovingBoundary="+(edit==4?"observed current postNPC rows; no other-NPC future promise":"none"));cases++;
                }
                Require(cases==28 && ordinaryEnhanced>0,"three controllers/chase/plain ammo, exact phase mutations and one double-moving boundary execute separately for component and ordinary");
                Console.WriteLine("PASS HELD WORLD actual Main.UpdateWorld_Projectiles cases="+cases+" ordinaryEnhanced="+ordinaryEnhanced+" ordinaryDeferred="+ordinaryDeferred+" ordinary opportunity is separate from1000ms components");
            }
            finally{Main.dedServ=oldDedicated;NPC.offSetDelayTime=oldOffsetDelay;Main.multiplayerNPCSmoothingRange=oldSmoothing;movingWatch=false;movingEntry=movingRoot=null;movingParent=null;movingSelected=movingOther=null;audit.UnpatchAll(audit.Id);}
        }
        private static void Harpy(object context)
        {
            {
                var p=Scene(context);p.accHarpyCharm=true;p.accHarpyCharmCooldown=3;var n=Target(new Vector2(1250,650));var q=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(1000,650),new Vector2(8,0),1,60,0,p.whoAmI)];var path=Path(n);var root=Root(p,q,path);var future=(Player)typeof(object).GetMethod("MemberwiseClone",Flags).Invoke(p,null);future.position+=new Vector2(30,0);var captured=Static(model,"CaptureForSolve",future,root,new[]{1});var state=Get(captured,"HarpyState");Require(ReferenceEquals(Get(state,"owner"),Main.player[p.whoAmI]),"future pose capture binds real Harpy owner rather than immutable clone");p.accHarpyCharmCooldown++;Require(!(bool)Get(root,"Unchanged"),"real sameGUC cooldown mutation revokes a future-pose capture");p.accHarpyCharmCooldown--;
            }
            int cases=0,positive=0;
            foreach(int type in new[]{1,639})foreach(int range in new[]{399,400,1200,1201})foreach(int cooldown in new[]{0,1})foreach(bool already in new[]{false,true})
            {
                var p=Scene(context);p.position=new Vector2(650,646);for(int x=5;x<Main.maxTilesX-5;x++)NativeToolsChecks.Tile(x,43,1);p.accHarpyCharm=!already;p.accHarpyCharmCooldown=cooldown;p.accSeraphNecklace=p.hasPhoenixQuiver=false;var n=Target(new Vector2(1300,650));var q=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),p.Center+new Vector2(range,0),new Vector2(0,-6),type,60,0,p.whoAmI)];if(already){var features=new Projectile.AI_001_Features(q.ai[2]);features.HarpyCharmed=true;q.ai[2]=features.Bits;}
                var path=Path(n);var root=Root(p,q,path);AttackContact plan;using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))plan=(AttackContact)Static(model,"KnownFamily",p,new[]{q},path,Clock(path),Terrain(),root);Advance(100,p);Require(first==0?plan==null:plan!=null && plan.Tick==first,"shared batch native Player charm→AI tail→OnHit bits/receive ranges and equipment-off sealed charm type="+type+" range="+range+" cooldown="+cooldown+" already="+already+" native="+first+" predicted="+(plan?.Tick??-1));if(first>0)positive++;cases++;
            }
            Require(positive>0,"batch charm comparison includes useful native first Strike");Console.WriteLine("PASS HELD-AMMO shared Harpy known Player/AI/receive cases="+cases+" positive="+positive);
            int batches=0,positiveBatches=0;
            foreach(int weapon in new[]{3540,3854})foreach(int parentSlot in new[]{0,20})foreach(int cooldown in new[]{0,1})foreach(bool quiver in new[]{false,true})
            {
                var p=Scene(context);p.position=new Vector2(650,646);p.inventory[p.selectedItem].SetDefaults(weapon);p.inventory[54].SetDefaults(40);p.inventory[54].stack=999;p.itemAnimation=p.itemAnimationMax=p.HeldItem.useAnimation;p.phantomPhoneixCounter=0;p.accHarpyCharm=true;p.accHarpyCharmCooldown=cooldown;p.accSeraphNecklace=p.hasPhoenixQuiver=false;p.magicQuiver=quiver;for(int x=5;x<Main.maxTilesX-5;x++)NativeToolsChecks.Tile(x,43,1);
                var n=Target(new Vector2(1300,667));n.width=n.height=200;n.Center=new Vector2(1300,667);int oldSlot=parentSlot==0?20:0;var old=new Projectile();old.SetDefaults(1);old.active=true;old.owner=p.whoAmI;old.whoAmI=oldSlot;old.Center=p.Center+new Vector2(600,0);old.velocity=new Vector2(0,-6);old.damage=0;Main.projectile[oldSlot]=old;var parent=new Projectile();parent.SetDefaults(p.HeldItem.shoot);parent.active=true;parent.owner=p.whoAmI;parent.whoAmI=parentSlot;parent.ai[0]=10;parent.ai[1]=1;Main.projectile[parentSlot]=parent;
                // This real partial projectile pass has ALREADY run Player.
                // Charm sees only the old arrow, never the not-yet-born batch.
                NativeQuickItemChecks.BeginWorldStep();typeof(Player).GetMethod("Update_HarpyCharm",Flags).Invoke(p,null);n.UpdateNPC(n.whoAmI);if(oldSlot<parentSlot){Main.ProjectileUpdateLoopIndex=oldSlot;old.Update(oldSlot);}Main.mouseX=(int)(n.Center.X-Main.screenPosition.X);Main.mouseY=(int)(n.Center.Y-Main.screenPosition.Y);Main.ProjectileUpdateLoopIndex=parentSlot;parent.AI();Main.ProjectileUpdateLoopIndex=-1;
                var born=Main.projectile.Where(q=>q.active && !ReferenceEquals(q,parent) && !ReferenceEquals(q,old)).ToArray();Require(born.Length==(weapon==3540?4:2),"actual held Harpy phase has complete native selected-ammo births");var prototypes=born.Select(Copy).ToArray();foreach(var q in born)q.active=false;var path=Path(n);var clock=Clock(path,"Projectiles");var root=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,prototypes[0],path,0,false,false},null);AttackContact plan;using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))plan=(AttackContact)Static(model,"ExplicitBirthBatch",p,prototypes,path,clock,Terrain(),root,n.Center,0,parent,null,false);foreach(var q in born)q.active=true;
                for(int slot=parentSlot+1;slot<1000;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;Main.projectile[slot].Update(slot);}Main.ProjectileUpdateLoopIndex=-1;parent.active=false;Advance(100,p);Require(first==0?plan==null:plan!=null && plan.Tick==first,"same batch native partial phase/newborn lower-or-higher slots/old-arrow charm/cooldown/quiver first receive weapon="+weapon+" parent="+parentSlot+" cooldown="+cooldown+" quiver="+quiver+" native="+first+" predicted="+(plan?.Tick??-1));if(first>0)positiveBatches++;Console.WriteLine("HELD Harpy partial weapon="+weapon+" parent="+parentSlot+" cooldown="+cooldown+" quiver="+quiver+" native="+first+" predicted="+(plan?.Tick??-1));batches++;
            }
            Require(positiveBatches>0,"real partial native batch group contains useful original first Strike");Console.WriteLine("PASS HELD-AMMO real partial native held births with old-arrow charm/slot/quiver cases="+batches+" positive="+positiveBatches);
        }
    }
}
