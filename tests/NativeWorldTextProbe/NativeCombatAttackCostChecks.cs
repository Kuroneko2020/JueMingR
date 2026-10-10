using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Probe-only counters count real entry calls. No production counters/logs,
    // AI skips, timing manipulation, synthetic benchmark solver or FPS claim.
    internal static class NativeCombatAttackCostChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static readonly Dictionary<string,int> calls=new Dictionary<string,int>();
        private static Player watchedYoyoPlayer;private static int primaryBirths,detachedBirths,overlappingBirths;private static bool forwardBirths;
        private static void YoyoBorn(Player __instance,Projectile __0)
        {
            if(!ReferenceEquals(__instance,watchedYoyoPlayer) || __0.owner!=__instance.whoAmI || __0.type!=__instance.HeldItem.shoot || __0.aiStyle!=99)return;
            if(__0.ai[0]==-2){detachedBirths++;return;}
            primaryBirths++;forwardBirths&=__0.velocity.X>0;
            if(Main.projectile.Any(q=>q.active && q.owner==__instance.whoAmI && q.aiStyle==99 && q.ai[0]==-2))overlappingBirths++;
        }
        private static void Count(MethodBase __originalMethod)
        {string key=__originalMethod.DeclaringType.Name+"."+__originalMethod.Name;int value;calls.TryGetValue(key,out value);calls[key]=value+1;}
        private static int Total(string key){int value;return calls.TryGetValue(key,out value)?value:0;}
        private static Harmony Audit(object attack)
        {
            var audit=new Harmony("JueMingR.Tests.AttackCosts");var assembly=attack.GetType().Assembly;
            foreach(string entry in new[]{"HostAttackAim.Clear","HostAttackControl.Clear","HostAttackAim.PrepareCore","HostAttackAim.Valid","HostAttackAim.PresentationCurrent","HostAttackControl.PresentationCurrent","HostBeamAttack.Solve","HostBeamAttack.Length","PredictionTerrain.Read","PredictionTerrain.ProjectilePassage","PredictionTerrain.get_Unchanged","HostAttackObstacles.Eligible","HostAttackObstacles.get_Unchanged","AttackAmmoSnapshot.CaptureCore","AttackAmmoSnapshot.MembersMatch","HostAttackCandidates.Priority","HostYoyoNavigation.Remaining","HostYoyoNavigation.Current","HostYoyoNavigation.Prepare","HostYoyoNavigation.OpeningPoint","HostYoyoNavigation.Find"})
            {var split=entry.Split('.');var type=assembly.GetType("JueMingR.TerrariaHost.Combat."+split[0],true);audit.Patch(type.GetMethod(split[1],Flags),prefix:new HarmonyMethod(typeof(NativeCombatAttackCostChecks).GetMethod("Count",Flags)));}
            audit.Patch(typeof(AttackIntercept).GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatAttackCostChecks).GetMethod("Count",Flags)));
            return audit;
        }
        private static void Report(string scenario)
        {Console.WriteLine("ATTACK COST "+scenario+" "+string.Join(";",calls));}
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");var audit=Audit(attack);
            try
            {
                YoyoOpening(context,combat,host,attack,input);YoyoMagicOpening(context,combat,host,attack,input);
                foreach(string scenario in new[]{"off","idle","no-target","stable","changing","dense","blocked","inapplicable","same-sample","switching"})
                {
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,scenario=="inapplicable"?3509:95,0,0);p.position=new Vector2(700,646);p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;Main.screenPosition=new Vector2(600,400);
                    bool active=scenario!="off" && scenario!="idle",target=scenario!="no-target";int count=scenario=="dense"?Main.maxNPCs:scenario=="switching"?2:target?1:0;
                    for(int i=0;i<count;i++){var n=Main.npc[i];n.SetDefaults(3);n.whoAmI=i;n.active=true;n.target=0;n.aiStyle=-1;n.noGravity=true;n.position=new Vector2(1020+i%20*4,646+i/20*4);n.velocity=n.netOffset=Vector2.Zero;n.life=n.lifeMax=100000;Array.Clear(n.immune,0,n.immune.Length);}
                    if(scenario=="blocked")for(int y=0;y<Main.maxTilesY;y++){Main.tile[60,y].active(true);Main.tile[60,y].type=1;}
                    NativeCombatObservationChecks.Save(host,scenario=="off"?new ObservationOptions():new ObservationOptions().Toggle(6));
                    calls.Clear();var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");int steps=cache.Steps,afterFirst=steps;
                    for(int i=0;i<8;i++)
                    {if(scenario=="switching"){p.inventory[0].SetDefaults(i%2==0?95:39);p.inventory[54].SetDefaults(i%2==0?97:40);p.inventory[54].stack=999;Main.npc[1].Center=new Vector2(1100,620);}NativeToolExecutionChecks.Sample(context,input,scenario=="switching"?Main.npc[i%2].Center:new Vector2(850,667),active);Call(combat,"Sample");Call(host,"SampleMouse");if(scenario!="same-sample" || i==0)NativeQuickItemChecks.BeginWorldStep();if(scenario=="changing")Main.npc[0].position.X+=1;Call(host,"Update",(ulong)Main.GameUpdateCount);var contact=GetOptional(attack,"ExpectedImpact");if(i==0)afterFirst=cache.Steps;if(scenario=="switching")Require(contact!=null,"rapid observed weapon/ammo/physical target replacement keeps a useful prepared contact");}
                    Report(scenario);
                    if(!active || count==0 || scenario=="inapplicable")Require(Total("AttackIntercept.Solve")==0 && cache.Steps==steps,"off/idle/no-target/inapplicable produce no attack solver or future work");
                    Require(Total("HostAttackCandidates.Priority")<=8*Main.maxNPCs && Total("AttackAmmoSnapshot.CaptureCore")<=48,"candidate qualification captures once per selection, not once per dense receiver");
                    if(scenario=="stable" || scenario=="changing")Require(GetOptional(attack,"ExpectedImpact")!=null,"measured ordinary solve keeps useful contact");
                    if(scenario=="blocked")Require(GetOptional(attack,"ExpectedImpact")==null,"measured stable wall does not invent contact");
                    if(scenario=="same-sample")Require(cache.Steps==afterFirst && GetOptional(attack,"ExpectedImpact")!=null && Total("AttackIntercept.Solve")<=16,"same sample repeats reuse the one future while bounded preparation remains useful");
                    if(scenario=="switching"){var final=(AttackContact)GetOptional(attack,"ExpectedImpact");var victim=Main.npc[final.Timeline.Identity.Slot];int life=victim.life;typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});for(int tick=0;tick<60 && victim.life==life;tick++){NativeQuickItemChecks.BeginWorldStep();victim.UpdateNPC(victim.whoAmI);for(int slot=0;slot<Main.maxProjectiles;slot++)if(Main.projectile[slot].active)Main.projectile[slot].Update(slot);}Require(victim.life<life,"counted rapid observed selection replacements still yield natural Shoot/Update/Damage progress");Report("switching-native-progress");}
                }
                SameEpoch(context,combat,host,attack,input);Navigation(context,combat,host,attack,input);Beam(context,combat,host,attack,input);
                Console.WriteLine("PASS attack costs: real finite preparation/qualification/retirement/navigation and beam scans; component calls, not game FPS.");
            }
            finally{audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
        // The full natural preparation entry and original ItemCheck/AI run on
        // distinct real update/input epochs. Opening Find was absent from the
        // old costs hook and from the flying navigator's Searches property.
        // These test-side method hooks also work against Release consumers.
        internal static void RunYoyoOpening(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");var audit=Audit(attack);
            try{YoyoOpening(context,combat,host,attack,input);YoyoMagicOpening(context,combat,host,attack,input);}finally{audit.UnpatchAll(audit.Id);}
        }
        internal static void YoyoOpening(object context,object combat,object host,object attack,object input)
        {
            var outlet=new Harmony("JueMingR.Tests.YoyoOpeningAchievement");foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})outlet.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            try
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3278,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.releaseUseItem=true;p.yoyoGlove=p.magicString=false;p.counterWeight=0;Main.screenPosition=new Vector2(600,400);
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.target=0;n.aiStyle=-1;n.noGravity=true;n.Center=new Vector2(830,667);n.velocity=n.netOffset=Vector2.Zero;n.life=n.lifeMax=10000;n.knockBackResist=0;n.shimmerTransparency=0;Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);
                NativeCombatObservationChecks.Save(host,new ObservationOptions().Toggle(6));NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,new Vector2(600,667),true);Call(combat,"Sample");Call(host,"SampleMouse");calls.Clear();
                // The native ItemCheck prefix prepares AFTER Player.Update's
                // movement/equipment reset. Do not pre-prepare in that epoch
                // and thereby suppress the real post-movement first-click seam.
                var control=Get(attack,"Control");int mx=Main.mouseX,my=Main.mouseY;p.Update(0);
                var shot=Main.projectile.Single(q=>q.active && q.type==541);Report("yoyo-first-natural-opening");Console.WriteLine("YOYO first state: opening="+Get(control,"openingUsable")+" permission="+Get(attack,"Permission")+" failed="+Get(attack,"Failed")+" target="+Get(Get(host,"Selection"),"HasTarget")+" timeline="+(((NpcPredictionCache)Get(Get(host,"Prediction"),"Cache")).Read(1)?.Count)+" timers="+p.itemAnimation+"/"+p.itemTime+" velocity="+shot.velocity+" mouseBefore="+mx+","+my+" mouseAfter="+Main.mouseX+","+Main.mouseY);
                Require((bool)Get(control,"openingUsable") && Total("HostYoyoNavigation.OpeningPoint")==1 && Total("HostYoyoNavigation.Find")<=2,"real first click prepares one timely opening; same-pass newborn may prepare its own flight route");Require(shot.velocity.X>0,"first natural Player.Update Shoot faces the prepared target without waiting another epoch");Require(Main.mouseX==mx && Main.mouseY==my,"first natural Shoot returns the physical cursor");
                n.immune[0]=200;
                for(int tick=0;tick<20 && shot.Center.X<=790;tick++)
                {NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");Call(attack,"PrepareNatural");p.ItemCheck();n.UpdateNPC(2);shot.Update(shot.whoAmI);}
                Require(shot.active && shot.Center.X>790,"original ball naturally clears the future hand-side wall before the measured continuous window");
                for(int y=5;y<=42;y++)NativeToolsChecks.Tile(48,y,1);
                float before=Vector2.Distance(shot.Center,n.Center);int key=(int)shot.key;calls.Clear();int points=0;
                for(int tick=0;tick<8;tick++)
                {
                    NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");Call(host,"SampleMouse");Call(attack,"PrepareNatural");
                    var entries=(IDictionary)Get(control,"shots");Require(entries.Contains(key),"continuous control retains the real causal ball source");if((bool)Get(entries[key],"HasPoint"))points++;
                    Require(p.itemAnimation==2 && p.itemTime==2,"original AI99 dummy timers prove no new ItemCheck firing window before this action");
                    mx=Main.mouseX;my=Main.mouseY;p.ItemCheck();n.UpdateNPC(2);shot.Update(shot.whoAmI);Require(shot.active && (int)shot.key==key && Main.mouseX==mx && Main.mouseY==my,"each original continuous action preserves ball identity and returns borrowed cursor");
                }
                float after=Vector2.Distance(shot.Center,n.Center);Report("yoyo-continuous-hand-wall8");Console.WriteLine("YOYO opening progress: before="+before+" after="+after+" points="+points+" sameKey="+key);
                Require(points==8 && after<before,"all eight existing-ball preparations remain useful and the original motor approaches the target beyond the blocked hand");
                Require(Total("HostYoyoNavigation.OpeningPoint")==0 && Total("HostYoyoNavigation.Find")<=2 && Total("HostYoyoNavigation.Prepare")==8,"no new native firing opportunity performs no opening search while each real flying ball still prepares");
                for(int y=5;y<=42;y++)Main.tile[48,y].active(false);
                // Let native release/return end the primary; never kill it to
                // manufacture a fresh window or alter its lifetime/recall.
                for(int tick=0;tick<90 && shot.active;tick++)
                {NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,n.Center,false);Call(combat,"Sample");p.controlUseItem=false;p.ItemCheck();n.UpdateNPC(2);shot.Update(shot.whoAmI);}
                Require(!shot.active,"original released primary completes its own native recall");
                for(int tick=0;tick<4 && (p.itemAnimation>0 || p.itemTime>0);tick++){NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,n.Center,false);Call(combat,"Sample");p.controlUseItem=false;p.ItemCheck();}
                NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,new Vector2(600,667),true);Call(combat,"Sample");Call(host,"SampleMouse");calls.Clear();Call(attack,"PrepareNatural");Require((bool)Get(control,"openingUsable") && Total("HostYoyoNavigation.OpeningPoint")==1,"fresh click after native recall prepares a new useful opening");p.controlUseItem=true;p.ItemCheck();var again=Main.projectile.Single(q=>q.active && q.type==541);Require((int)again.key!=key && again.velocity.X>0,"next legal original ItemCheck emits immediately with the new opening direction");Report("yoyo-native-recall-reopen");
                mx=Main.mouseX;my=Main.mouseY;calls.Clear();NativeCombatObservationChecks.Save(host,new ObservationOptions());Require(Call(control,"BeginOpening",p,p.HeldItem)==null && Call(control,"BeginAI",again)==null && again.active && Main.mouseX==mx && Main.mouseY==my,"Aim OFF returns temporary inputs and preserves the real surviving ball");
                NativeCombatObservationChecks.Save(host,new ObservationOptions().Toggle(6));NativeCombatAimChecks.Prepare(host,attack,n,0,true);p.inventory[0]=p.HeldItem.Clone();Require(Call(control,"BeginOpening",p,p.HeldItem)==null && Call(control,"BeginAI",again)==null && again.active,"same-type source replacement cannot borrow old opening or flying input");Call(attack,"Reset");Require(((IDictionary)Get(control,"shots")).Count==0 && !(bool)Get(control,"openingUsable") && again.active && Main.mouseX==mx && Main.mouseY==my,"session reset removes all routes/leases while preserving native ball lifetime");
            }
            finally{outlet.UnpatchAll(outlet.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
        private static void YoyoMagicOpening(object context,object combat,object host,object attack,object input)
        {
            NativeCombatCadenceChecks.Save(combat,new CombatOptions());NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3278,0,0);p.position=new Vector2(700,646);p.releaseUseItem=true;foreach(var item in p.armor)item.TurnToAir();p.armor[3].SetDefaults(Terraria.ID.ItemID.MagicString);
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.target=0;n.Center=new Vector2(810,667);n.velocity=n.netOffset=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=10000;n.shimmerTransparency=0;Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);
            NativeCombatObservationChecks.Save(host,new ObservationOptions().Toggle(6));NativeCombatCadenceChecks.Save(combat,new CombatOptions(16));
            var audit=new Harmony("JueMingR.Tests.YoyoOpeningMagic");foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));audit.Patch(typeof(Player).GetMethod("TryUpdateChannel",Flags),postfix:new HarmonyMethod(typeof(NativeCombatAttackCostChecks),nameof(YoyoBorn)));
            try
            {
                watchedYoyoPlayer=p;primaryBirths=detachedBirths=overlappingBirths=0;forwardBirths=true;calls.Clear();
                for(int tick=0;tick<32;tick++){NativeCombatCadenceChecks.Step(context,true,false,0,point:new Vector2(600,667));Require(Main.mouseX==NativeCombatCadenceChecks.ManualMouseX && Main.mouseY==NativeCombatCadenceChecks.ManualMouseY,"managed magic-string native windows return every borrowed cursor");}
                Report("yoyo-magic-string-natural32");Console.WriteLine("YOYO magic openings: primary="+primaryBirths+" detached="+detachedBirths+" withLiveDetached="+overlappingBirths+" forward="+forwardBirths);
                Require(p.magicString && primaryBirths>=3 && detachedBirths>=2 && overlappingBirths>0 && forwardBirths,"legitimate native magic-string re-emission remains aimed even with an existing detached ball");
                Require(Total("HostYoyoNavigation.OpeningPoint")>=primaryBirths && Total("HostYoyoNavigation.OpeningPoint")<32,"real re-emissions prepare openings, while continuous/recall actions do not rebuild one each epoch");
                var entries=(IDictionary)Get(Get(attack,"Control"),"shots");foreach(var q in Main.projectile.Where(q=>q.active && q.aiStyle==99 && q.ai[0]==-2))Require(!entries.Contains((int)q.key),"native detached balls do not become registered aim control sources");
            }
            finally{watchedYoyoPlayer=null;audit.UnpatchAll(audit.Id);NativeCombatCadenceChecks.Save(combat,new CombatOptions());NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
        private static void SameEpoch(object context,object combat,object host,object attack,object input)
        {
            var outlet=new Harmony("JueMingR.Tests.AttackCostActionAchievement");foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})outlet.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            try
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.releaseUseItem=true;p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;Main.screenPosition=new Vector2(600,400);
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.target=0;n.aiStyle=-1;n.noGravity=true;n.Center=new Vector2(1020,667);n.velocity=n.netOffset=Vector2.Zero;n.life=n.lifeMax=10000;Array.Clear(n.immune,0,n.immune.Length);
                NativeCombatObservationChecks.Save(host,new ObservationOptions().Toggle(6));NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");Call(host,"SampleMouse");NativeQuickItemChecks.BeginWorldStep();calls.Clear();Call(attack,"PrepareNatural");Require(GetOptional(attack,"ExpectedImpact")!=null,"one real input epoch prepares useful natural first-shot contact");Report("natural-action-first");calls.Clear();int steps=((NpcPredictionCache)Get(Get(host,"Prediction"),"Cache")).Steps;
                for(int i=0;i<20;i++){Call(attack,"PrepareNatural");Require(GetOptional(attack,"ExpectedImpact")!=null,"same-epoch natural reads preserve current contact");}
                Report("natural-action-same-epoch20");Require(Total("HostAttackAim.PrepareCore")==0 && Total("AttackIntercept.Solve")==0 && ((NpcPredictionCache)Get(Get(host,"Prediction"),"Cache")).Steps==steps,"natural repeated callbacks in one input.Frame do not prepare/solve/advance a future again");
                p.Update(0);var born=Main.projectile.Single(q=>q.active && q.type==14);int life=n.life;for(int tick=0;tick<40 && n.life==life;tick++){NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(2);born.Update(born.whoAmI);}Require(n.life<life,"same-epoch preparation still permits original natural Player.Update Shoot and Damage progress");
                NativeCombatObservationChecks.Save(host,new ObservationOptions());p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3278,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.channel=p.controlUseItem=true;p.yoyoGlove=p.magicString=false;p.counterWeight=0;n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.target=0;n.aiStyle=-1;n.noGravity=true;n.Center=new Vector2(810,667);n.life=n.lifeMax=10000;Array.Clear(n.immune,0,n.immune.Length);
                NativeCombatObservationChecks.Save(host,new ObservationOptions().Toggle(6));NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatAimChecks.Prepare(host,attack,n,0,true);typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var yoyo=Main.projectile.Single(q=>q.active && q.type==541);NativeCombatAimChecks.Prepare(host,attack,n,0,true);var control=Get(attack,"Control");calls.Clear();int mx=Main.mouseX,my=Main.mouseY;
                for(int i=0;i<20;i++){var scope=Call(control,"BeginAI",yoyo);Require(scope!=null,"same-source controlled consumer retains useful point");Call(scope,"End");var contact=GetOptional(attack,"ExpectedImpact");}
                Report("controller-same-source20");Require(Total("AttackIntercept.Solve")==0 && Total("HostYoyoNavigation.Prepare")==0 && Total("HostYoyoNavigation.Remaining")==0 && Main.mouseX==mx && Main.mouseY==my,"controlled repeated BeginAI/read only validate and restore, never navigate/replay");
                life=n.life;for(int tick=0;tick<40 && n.life==life;tick++){NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(2);NativeCombatAimChecks.Prepare(host,attack,n,0,true);yoyo.Update(yoyo.whoAmI);}Require(n.life<life,"repeated controlled reads retain actual native motor/Damage progress");
            }
            finally{foreach(var method in outlet.GetPatchedMethods().ToArray())outlet.Unpatch(method,HarmonyPatchType.All,outlet.Id);}
        }
        private static void Navigation(object context,object combat,object host,object attack,object input)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3278,0,0);p.position=new Vector2(700,646);p.channel=p.controlUseItem=true;Main.screenPosition=new Vector2(600,400);var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.target=0;n.aiStyle=-1;n.noGravity=true;n.Center=new Vector2(790,667);n.life=n.lifeMax=10000;
            NativeCombatObservationChecks.Save(host,new ObservationOptions().Toggle(6));NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatAimChecks.Prepare(host,attack,n,0,true);
            var shot=Main.projectile[Projectile.NewProjectile(new EntitySource_ItemUse(p,p.HeldItem),p.Center,Vector2.UnitX*16,541,30,0,0)];
            var navigation=Activator.CreateInstance(attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostYoyoNavigation",true),true);var timeline=((NpcPredictionCache)Get(Get(host,"Prediction"),"Cache")).Read(1);var clock=Clock(attack,timeline,"Projectiles");calls.Clear();
            var args=new object[]{p,shot,timeline,clock,Vector2.Zero,false};navigation.GetType().GetMethod("Prepare",Flags).Invoke(navigation,args);Require((bool)args[5],"navigation workload keeps a useful controlled point");
            Require(Total("HostYoyoNavigation.Remaining")==1,"associated lifetime is captured once per preparation, not per motor replay step");Report("navigation-prepare");calls.Clear();
            for(int i=0;i<20;i++)Require((bool)Call(navigation,"Current",p),"stable navigation consumer retains its dependencies");
            Require(Total("HostYoyoNavigation.Prepare")==0 && Total("HostYoyoNavigation.Remaining")==0,"navigation consumer only validates, never searches/replays lifetime");Report("navigation-current20");
        }
        private static object Clock(object attack,object timeline,string phase)
        {var assembly=attack.GetType().Assembly;var type=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true);return Activator.CreateInstance(type,Flags,null,new[]{timeline,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),phase)},null);}
        private static void Beam(object context,object combat,object host,object attack,object input)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3541,0,0);p.position=new Vector2(700,646);p.channel=p.controlUseItem=true;p.statMana=p.statManaMax2=10000;Main.screenPosition=new Vector2(600,400);
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.target=0;n.aiStyle=-1;n.noGravity=true;n.Center=new Vector2(1100,p.MountedCenter.Y);n.life=n.lifeMax=10000;Array.Clear(n.immune,0,n.immune.Length);
            NativeCombatObservationChecks.Save(host,new ObservationOptions().Toggle(6));NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatAimChecks.Prepare(host,attack,n,0,true);
            var timeline=((NpcPredictionCache)Get(Get(host,"Prediction"),"Cache")).Read(1);var assembly=attack.GetType().Assembly;var parent=Main.projectile[Projectile.NewProjectile(new EntitySource_ItemUse(p,p.HeldItem),p.MountedCenter,Vector2.UnitX,633,20,0,0)];parent.ai[0]=180;
            var childType=assembly.GetType("JueMingR.TerrariaHost.Combat.HostBeamAttack+Child",true);var children=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(childType));
            for(int i=0;i<6;i++){var child=Main.projectile[Projectile.NewProjectile(new EntitySource_Parent(parent),parent.Center,Vector2.UnitX,632,60,0,0,i,parent.key)];children.Add(Activator.CreateInstance(childType,Flags,null,new object[]{child},null));}
            for(int y=0;y<Main.maxTilesY;y++){Main.tile[60,y].active(true);Main.tile[60,y].type=1;}
            var terrain=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");calls.Clear();var args=new object[]{p,parent,children,n.Center,timeline,Clock(attack,timeline,"Projectiles"),terrain,null};
            var contact=assembly.GetType("JueMingR.TerrariaHost.Combat.HostBeamAttack",true).GetMethod("Solve",Flags).Invoke(null,args);Report("charged-beam-wall24x6");Require(contact==null,"full-charge finite wall scan still refuses blocked contact");
            Require(Total("HostBeamAttack.Length")<=24,"identical six full-charge ray scans must share within the real solver");
            calls.Clear();for(int y=0;y<Main.maxTilesY;y++)Main.tile[60,y].active(false);Call(terrain,"Reset");contact=assembly.GetType("JueMingR.TerrariaHost.Combat.HostBeamAttack",true).GetMethod("Solve",Flags).Invoke(null,args);Report("charged-beam-open-change");Require(contact!=null && Total("HostBeamAttack.Length")>0,"next preparation scans changed terrain afresh and recovers useful finite beam contact");
            calls.Clear();parent.ai[0]=120;for(int y=0;y<Main.maxTilesY;y++)Main.tile[60,y].active(true);Call(terrain,"Reset");contact=assembly.GetType("JueMingR.TerrariaHost.Combat.HostBeamAttack",true).GetMethod("Solve",Flags).Invoke(null,args);Report("partial-beam-distinct-rays");Require(contact==null && Total("HostBeamAttack.Length")>24,"partial bundle retains distinct rays and wall refusal rather than sharing full-charge result");
        }
        internal static void Draw(object context,ProbeGraphics graphics)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");var world=Get(host,"World");var audit=Audit(attack);
            try
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;Main.screenPosition=new Vector2(600,400);var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(810,646);n.velocity=n.netOffset=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=10000;
                NativeCombatObservationChecks.Save(host,new ObservationOptions().Toggle(1).Toggle(6));NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatAimChecks.Prepare(host,attack,n,0);Call(world,"Prepare");calls.Clear();
                for(int i=0;i<12;i++)graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);Report("actual-draw12");
                Require(Total("HostAttackAim.Valid")==0 && Total("PredictionTerrain.Read")==0 && Total("HostAttackObstacles.get_Unchanged")==0 && Total("HostAttackObstacles.Eligible")==0,"actual Draw reads a prepared presentation lease, never scans terrain or the NPC qualification pool");
                Require(Total("HostAttackAim.PresentationCurrent")==12 && Total("AttackAmmoSnapshot.MembersMatch")==12,"actual ordinary Draw performs exactly one prepared-lease and known ammo-member check");
                Require(Total("AttackIntercept.Solve")==0 && Total("HostBeamAttack.Solve")==0 && Total("HostYoyoNavigation.Prepare")==0,"actual Draw performs freshness reads, never expensive solving/navigation");
                Require((bool)Get(Get(world,"Impact"),"Visible"),"counted actual Draw retains healthy red output");
                n.dontTakeDamage=true;calls.Clear();graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);Require(!(bool)Get(Get(world,"Impact"),"Visible") && Total("HostAttackAim.Clear")==0 && Total("HostAttackControl.Clear")==0 && Total("PredictionTerrain.Read")==0 && Total("HostAttackObstacles.Eligible")==0,"current receiver refusal removes actual red with constant reads, never retires/scans control sources inside Draw");Report("ordinary-draw-receiver-refusal");n.dontTakeDamage=false;NativeCombatAimChecks.Prepare(host,attack,n,0);Call(world,"Prepare");
                p.inventory[54].SetDefaults(278);p.inventory[54].stack=999;graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);Require(!(bool)Get(Get(world,"Impact"),"Visible"),"same-frame known ammo mutation retires the presentation without reselection/scanning");
                NativeCombatAimChecks.Prepare(host,attack,n,0);Call(world,"Prepare");graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);Require((bool)Get(Get(world,"Impact"),"Visible"),"new preparation restores the changed-ammo display normally");
                float wind=Main.windSpeedCurrent;try{Main.windSpeedCurrent=wind+.01f;graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);Require(!(bool)Get(Get(world,"Impact"),"Visible"),"known environment-field mismatch retires prepared red without terrain reads");}finally{Main.windSpeedCurrent=wind;}
                ControllerDraw(context,graphics,combat,host,attack,input,world);
            }
            finally{audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
        private static void ControllerDraw(object context,ProbeGraphics graphics,object combat,object host,object attack,object input,object world)
        {
            var outlet=new Harmony("JueMingR.Tests.AttackCostYoyoAchievement");var method=typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod("HandleSpecialEvent",Flags);outlet.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            try
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3278,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.channel=p.controlUseItem=true;p.yoyoGlove=p.magicString=false;p.counterWeight=0;Main.screenPosition=new Vector2(600,400);
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.target=0;n.Center=new Vector2(810,667);n.velocity=n.netOffset=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=10000;Array.Clear(n.immune,0,n.immune.Length);
                NativeCombatObservationChecks.Save(host,new ObservationOptions().Toggle(1).Toggle(6));NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatAimChecks.Prepare(host,attack,n,0,true);
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var shot=Main.projectile.Single(q=>q.active && q.type==541);NativeCombatAimChecks.Prepare(host,attack,n,0,true);Require(GetOptional(attack,"ExpectedImpact")!=null,"real native yoyo birth has a usable prepared controller contact");Call(world,"Prepare");calls.Clear();
                var pixels=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);Report("controller-draw");Require(pixels.Any(c=>c.A>100 && c.R>190 && c.G<110 && c.B<110),"real controller prepared lease draws red pixels");
                Require(Total("HostAttackControl.PresentationCurrent")==1 && Total("HostYoyoNavigation.Current")==0 && Total("HostYoyoNavigation.Prepare")==0 && Total("PredictionTerrain.Read")==0 && Total("HostAttackObstacles.Eligible")==0,"controller Draw reads its bound source directly, without lookup/replay/terrain or qualification scans");
                n.dontTakeDamage=true;calls.Clear();pixels=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);Require(!pixels.Any(c=>c.A>100 && c.R>190 && c.G<110 && c.B<110) && Total("HostAttackAim.Clear")==0 && Total("HostAttackControl.Clear")==0 && Total("PredictionTerrain.Read")==0 && Total("HostAttackObstacles.Eligible")==0,"controller receiver refusal is pure constant Draw validation, with no source cleanup loop");Report("controller-draw-receiver-refusal");n.dontTakeDamage=false;NativeCombatAimChecks.Prepare(host,attack,n,0,true);Call(world,"Prepare");
                NativeToolsChecks.Tile(48,41,1);Require(Call(Get(attack,"Control"),"BeginAI",shot)==null,"actual controller consumer revalidates changed navigation terrain and retires the same capability");calls.Clear();pixels=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);Require(!pixels.Any(c=>c.A>100 && c.R>190 && c.G<110 && c.B<110) && Total("PredictionTerrain.Read")==0 && Total("HostYoyoNavigation.Current")==0,"consumer rejection immediately retires the red lease; Draw does not rediscover or revalidate the world");
                Console.WriteLine("PASS controller Draw: real source/contact pixels, constant bound lease, consumer changed-terrain rejection immediately removes red with zero Draw scans.");
            }
            finally{outlet.Unpatch(method,HarmonyPatchType.All,outlet.Id);}
        }
    }
}
