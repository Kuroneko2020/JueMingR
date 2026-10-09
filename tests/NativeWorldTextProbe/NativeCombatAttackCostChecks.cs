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
        private static void Count(MethodBase __originalMethod)
        {string key=__originalMethod.DeclaringType.Name+"."+__originalMethod.Name;int value;calls.TryGetValue(key,out value);calls[key]=value+1;}
        private static int Total(string key){int value;return calls.TryGetValue(key,out value)?value:0;}
        private static Harmony Audit(object attack)
        {
            var audit=new Harmony("JueMingR.Tests.AttackCosts");var assembly=attack.GetType().Assembly;
            foreach(string entry in new[]{"HostAttackAim.PrepareCore","HostAttackAim.Valid","HostAttackAim.PresentationCurrent","HostAttackControl.PresentationCurrent","HostBeamAttack.Solve","HostBeamAttack.Length","PredictionTerrain.Read","PredictionTerrain.ProjectilePassage","PredictionTerrain.get_Unchanged","HostAttackObstacles.Eligible","HostAttackObstacles.get_Unchanged","AttackAmmoSnapshot.CaptureCore","AttackAmmoSnapshot.MembersMatch","HostAttackCandidates.Priority","HostYoyoNavigation.Remaining","HostYoyoNavigation.Current","HostYoyoNavigation.Prepare"})
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
                NativeToolsChecks.Tile(48,41,1);Require(Call(Get(attack,"Control"),"BeginAI",shot)==null,"actual controller consumer revalidates changed navigation terrain and retires the same capability");calls.Clear();pixels=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);Require(!pixels.Any(c=>c.A>100 && c.R>190 && c.G<110 && c.B<110) && Total("PredictionTerrain.Read")==0 && Total("HostYoyoNavigation.Current")==0,"consumer rejection immediately retires the red lease; Draw does not rediscover or revalidate the world");
                Console.WriteLine("PASS controller Draw: real source/contact pixels, constant bound lease, consumer changed-terrain rejection immediately removes red with zero Draw scans.");
            }
            finally{outlet.Unpatch(method,HarmonyPatchType.All,outlet.Id);}
        }
    }
}
