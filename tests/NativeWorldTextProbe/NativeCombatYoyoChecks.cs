using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatYoyoChecks
    {
        private static NPC watchedTarget;private static string watchedChild;private static long childFirst=-1;
        private static void Strike(NPC __instance,int __result){int slot=Main.ProjectileUpdateLoopIndex;if(__result>0 && ReferenceEquals(__instance,watchedTarget) && slot>=0 && Main.projectile[slot].key.ToString()==watchedChild && childFirst<0)childFirst=Main.GameUpdateCount;}
        internal static void Run(object context)
        {
            // Existing isolated achievement outlet: native Shoot/AI/Damage
            // remain intact; this CPU component has no achievement service.
            var audit=new Harmony("JueMingR.Tests.YoyoAimAchievement");var method=typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod("HandleSpecialEvent",BindingFlags.Static|BindingFlags.Public);
            audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            var strike=AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)});audit.Patch(strike,postfix:new HarmonyMethod(typeof(NativeCombatYoyoChecks),nameof(Strike)));
            try{RunCore(context);}finally{watchedTarget=null;watchedChild=null;audit.Unpatch(strike,HarmonyPatchType.All,audit.Id);audit.Unpatch(method,HarmonyPatchType.All,audit.Id);}
        }
        private static void RunCore(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var tools=Get(context,"Tools");var input=Get(context,"Input");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,3278,0,0);p.position=new Vector2(700,500);p.channel=p.controlUseItem=true;Main.screenPosition=new Vector2(600,400);p.itemAnimationMax=p.HeldItem.useAnimation;
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(800,502);n.life=n.lifeMax=10000;n.target=0;Array.Clear(n.immune,0,n.immune.Length);
            NativeToolExecutionChecks.Sample(context,input,new Vector2(650,450),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0,true);
            typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var shot=Main.projectile.Single(q=>q.active && q.type==541);NativeCombatAimChecks.Prepare(host,attack,n,0,true);shot.AI();
            Require(shot.ai[0]>790,"yoyo natural AI consumes its prepared control point, not physical cursor: "+shot.ai[0]);
            Require(GetOptional(attack,"ExpectedImpact")!=null,"yoyo contact requires finite inertial replay");
            var initialContact=(JueMingR.Features.Combat.AttackContact)GetOptional(attack,"ExpectedImpact");var entries=(System.Collections.IDictionary)Get(Get(attack,"Control"),"shots");var navigator=Get(entries[(int)shot.key],"Navigation");
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,false));
            var stepper=navigator.GetType().GetMethod("Step",BindingFlags.Static|BindingFlags.NonPublic);float range=Terraria.ID.ProjectileID.Sets.YoyosMaximumRange[shot.type],speed=Terraria.ID.ProjectileID.Sets.YoyosTopSpeed[shot.type];
            foreach(float distance in new[]{range*.9f,range*1.1f})
            {
                shot.Center=p.Center+new Vector2(distance,0);shot.velocity=new Vector2(4,0);shot.ai[0]=0;shot.ai[1]=0;Main.mouseX=(int)(p.Center.X-Main.screenPosition.X);Main.mouseY=(int)(p.Center.Y-Main.screenPosition.Y);
                Vector2 expected=(Vector2)stepper.Invoke(null,new object[]{shot.Center,shot.velocity,p.Center,p.Center,range,speed,false});shot.AI();Require(Vector2.DistanceSquared(expected,shot.velocity)<.0001f,"pure controlled motor matches native inside/pullback entry: distance="+distance);
                var fresh=Activator.CreateInstance(navigator.GetType(),true);var args=new object[]{p,shot,initialContact.Timeline,NativeCombatAimChecks.PhaseClock(attack,initialContact.Timeline,"Projectiles"),Vector2.Zero,false};var result=fresh.GetType().GetMethod("Prepare",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(fresh,args);Require(result!=null && (bool)args[5],"initial R..1.3R remains eligible for a real pullback contact");
                Console.WriteLine("PASS yoyo motor/initial radius: distance="+distance+" native="+shot.velocity);
            }
            shot.Center=p.Center+new Vector2(range*1.4f,0);shot.ai[0]=0;shot.AI();Require(shot.ai[0]==-1,"greater than 1.3R retains native return transition");
            p.magicString=true;p.channel=false;shot.AI();Require(shot.ai[0]==-3 && shot.damage==0 && Main.projectile.Any(q=>q.active && q.type==541 && q.ai[0]==-2),"magic string preserves native detached birth");
            p.channel=true;NativeCombatAimChecks.Prepare(host,attack,n,0,true);var detached=Main.projectile.Single(q=>q.active && q.type==541 && q.ai[0]==-2);detached.AI();Require(detached.ai[0]==-2,"detached yoyo cannot regain navigation");
            Console.WriteLine("PASS yoyo natural input/contact and magic-string release");
            Route(context,false);Route(context,true);Route(context,false,false);
            Blocked(context,false);Blocked(context,true);
            GlovePhase(context,0);GlovePhase(context,5);
            Lifetime(context,3278);Lifetime(context,3389);LargeEdge(context);NativeCombatYoyoNavigationChecks.Run(context);
        }
        private static void GlovePhase(object context,int parentSlot)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3278,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.channel=p.controlUseItem=p.yoyoGlove=true;p.counterWeight=0;Main.screenPosition=new Vector2(600,400);
            for(int i=0;i<parentSlot;i++)Require(Projectile.NewProjectile(new Terraria.DataStructures.EntitySource_DebugCommand(),new Vector2(300,300),Vector2.Zero,1,0,0,0)==i,"native filler reserves parent slot");
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(800,646);n.velocity=n.netOffset=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=10000;n.target=0;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);n.shimmerTransparency=0;
            NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,new Vector2(650,450),true);Call(combat,"Sample");n.UpdateNPC(2);NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0,true);
            typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var parent=Main.projectile[parentSlot];Require(parent.type==541,"native yoyo parent in reserved slot");for(int i=0;i<parentSlot;i++)Main.projectile[i].Kill();parent.Center=new Vector2(799,666);parent.velocity=new Vector2(10,0);NativeCombatAimChecks.Prepare(host,attack,n,0,true);
            var rng=typeof(Main).GetField("_rngs",BindingFlags.Static|BindingFlags.NonPublic);object old=rng.GetValue(null);rng.SetValue(null,new System.Collections.Generic.Dictionary<string,Terraria.Utilities.UnifiedRandom>{{"UpdateProjectiles",new Terraria.Utilities.UnifiedRandom(702)}});
            try
            {
                int life=n.life;NativeCombatPhaseChecks.ProjectilePhase();Require(n.life<life,"original parent Damage naturally triggers glove birth");var child=Main.projectile.Single(q=>q.active && q.type==541 && !ReferenceEquals(q,parent));var control=Get(attack,"Control");var entries=(System.Collections.IDictionary)Get(control,"shots");var entry=entries[(int)child.key];bool later=child.whoAmI>parentSlot;
                Require(entry!=null && (bool)Get(entry,"BornNext")==!later,"causal newborn phase preserves passed versus later slot");Require((child.localAI[0]>0)==later,"only later-slot second ball runs AI in its birth phase");
                var nav=Get(entry,"Navigation");var secondary=nav.GetType().GetMethod("Secondary",BindingFlags.Static|BindingFlags.NonPublic);Require((bool)secondary.Invoke(null,new object[]{child})==later && (bool)secondary.Invoke(null,new object[]{parent})==!later,"native low-slot order decides both current roles, not ai0 birth tag");
                Vector2 parentEnd=parent.Center;var plan=(AttackContact)GetOptional(entry,"Contact");Console.WriteLine("YOYO natural glove phase: parent="+parentSlot+" child="+child.whoAmI+" bornNext="+Get(entry,"BornNext")+" clock="+child.localAI[0]+" contact="+plan?.Tick+" sample="+plan?.Timeline.SampleTick+" nativeStep="+Main.GameUpdateCount);
                Require(parentEnd.X>799,"newborn preparation did not rewind/re-advance the parent's natural AI/movement");Require(plan!=null,"natural newborn has a legal finite body contact in this open fixture");long expected=plan.Timeline.SampleTick+plan.Tick;watchedTarget=n;watchedChild=child.key.ToString();childFirst=-1;
                for(int tick=1;tick<=35 && childFirst<0;tick++)
                {NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,new Vector2(650,450),true);Call(combat,"Sample");n.UpdateNPC(2);NativeCombatAimChecks.Prepare(host,attack,n,0,true);NativeCombatPhaseChecks.ProjectilePhase();if(tick==1 && !later)Require(child.localAI[0]>0,"passed child begins at next original projectile phase");}
                Console.WriteLine("YOYO natural glove child Damage: key="+watchedChild+" expected="+expected+" actual="+childFirst+" later="+later+" center="+child.Center+" velocity="+child.velocity);Require(childFirst==expected,"early/late newborn first real StrikeNPC uses the same absolute contact tick");watchedTarget=null;watchedChild=null;
                // A different live same-owner ball owns native group recall.
                // Freeze only this new lifetime premise; keep the real births,
                // source references, body sizes and control parameters.
                child.Center=p.Center;child.velocity=new Vector2(4,0);child.ai[0]=1;child.localAI[0]=0;Array.Clear(child.localNPCImmunity,0,child.localNPCImmunity.Length);Array.Clear(n.immune,0,n.immune.Length);
                parent.ai[0]=1;parent.localAI[0]=Terraria.ID.ProjectileID.Sets.YoyosLifeTimeMultiplier[parent.type]*60*((1+p.meleeSpeed)/2)-.5f;
                NativeCombatAimChecks.Prepare(host,attack,n,0,true);var latePlan=(AttackContact)GetOptional(entries[(int)child.key],"Contact");NativeCombatPhaseChecks.ProjectilePhase();
                Console.WriteLine("YOYO associated native recall: parent="+parentSlot+" childContact="+latePlan?.Tick+" ownClock="+child.localAI[0]+" parentState="+parent.ai[0]+" childState="+child.ai[0]);Require(parent.ai[0]<0 && child.ai[0]<0,"other original ball expiry recalls the whole native owner group");Require(latePlan==null,"a healthy ball cannot promise future Damage past the other known native group lifetime");
            }
            finally{rng.SetValue(null,old);}
        }
        private static void Lifetime(object context,int weapon)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),weapon,0,0);p.position=new Vector2(700,646);p.channel=p.controlUseItem=true;p.yoyoString=p.magicString=p.yoyoGlove=false;Main.screenPosition=new Vector2(600,400);
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(800,646);n.life=n.lifeMax=10000;n.target=0;Array.Clear(n.immune,0,n.immune.Length);
            NativeToolExecutionChecks.Sample(context,Get(context,"Input"),new Vector2(650,450),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0,true);
            typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var shot=Main.projectile.Single(q=>q.active && q.type==p.HeldItem.shoot);float native=Terraria.ID.ProjectileID.Sets.YoyosLifeTimeMultiplier[shot.type];
            shot.localAI[0]=native<0?100000:native*60*((1+p.meleeSpeed)/2)-.5f;NativeCombatAimChecks.Prepare(host,attack,n,0,true);
            Require((GetOptional(attack,"ExpectedImpact")!=null)==(native<0),"yoyo contact cannot outlive known native control window: "+weapon);shot.AI();Require((shot.ai[0]>=0)==(native<0),"native finite/infinite lifetime agrees with control retirement");
            var registry=(System.Collections.IDictionary)Get(Get(attack,"Control"),"shots");if(native>0){shot.localAI[0]=native*60*((1+p.meleeSpeed)/2)-4;var nav=Get(registry[(int)shot.key],"Navigation");var remaining=nav.GetType().GetMethod("Remaining",BindingFlags.Static|BindingFlags.NonPublic);Require((int)remaining.Invoke(null,new object[]{p,shot,false})==4 && (int)remaining.Invoke(null,new object[]{p,shot,true})==1,"second-ball random clock uses conservative four increments without RNG");}
            Console.WriteLine("PASS native yoyo lifetime: weapon="+weapon+" infinite="+(native<0));
        }
        private static void LargeEdge(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3278,0,0);p.position=new Vector2(700,646);p.channel=p.controlUseItem=true;Main.screenPosition=new Vector2(600,400);
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(800,646);n.width=200;n.life=n.lifeMax=10000;n.target=0;Array.Clear(n.immune,0,n.immune.Length);
            NativeToolExecutionChecks.Sample(context,Get(context,"Input"),new Vector2(650,450),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0,true);
            typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var shot=Main.projectile.Single(q=>q.active && q.type==541);NativeCombatAimChecks.Prepare(host,attack,n,0,true);Require(GetOptional(attack,"ExpectedImpact")!=null,"large receive edge inside radius stays reachable when centre is outside");
            int before=n.life;for(int i=0;i<40 && n.life==before;i++)shot.Update(shot.whoAmI);Require(n.life<before,"large receive edge plan reaches genuine native Damage");Console.WriteLine("PASS yoyo large receive edge/centre-outside radius");
        }
        private static void Blocked(object context,bool outside)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3278,0,0);p.position=new Vector2(700,646);p.channel=p.controlUseItem=true;Main.screenPosition=new Vector2(600,400);
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(outside?950:800,646);n.life=n.lifeMax=10000;n.target=0;Array.Clear(n.immune,0,n.immune.Length);
            if(!outside)for(int y=5;y<=42;y++){Main.tile[48,y].active(true);Main.tile[48,y].type=1;}
            NativeToolExecutionChecks.Sample(context,Get(context,"Input"),new Vector2(650,450),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));Action prepare=()=>
            {
                if(!outside){NativeCombatAimChecks.Prepare(host,attack,n,0,true);return;}
                var selection=Get(host,"Selection");Call(selection,"Update",((ObservationSettings)Get(host,"Settings")).Value,(long)Get(host,"Session"),true,null);Require(!(bool)Get(selection,"HasTarget"),"current weapon excludes this physically out-of-range receiver");Call(Get(Get(host,"Prediction"),"Cache"),"Clear");Call(attack,"PrepareProjectiles");
            };prepare();
            typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var shot=Main.projectile.Single(q=>q.active && q.type==541);prepare();shot.AI();
            Vector2 manual=new Vector2(650,450)-p.Center;float radius=Terraria.ID.ProjectileID.Sets.YoyosMaximumRange[shot.type]/((1+p.meleeSpeed*3)/4);if(p.yoyoString)radius=(Terraria.ID.ProjectileID.Sets.YoyosMaximumRange[shot.type]*1.25f+30)/((1+p.meleeSpeed*3)/4);if(manual.Length()>radius-1)manual=manual.SafeNormalize(Vector2.Zero)*(radius-1);manual+=p.Center;
            Require(Vector2.DistanceSquared(new Vector2(shot.ai[0],shot.ai[1]),manual)<.01f && GetOptional(attack,"ExpectedImpact")==null,"no path/outside target radius restores real manual cursor and native radial clip: "+shot.ai[0]+","+shot.ai[1]+" expected="+manual);
            Console.WriteLine("PASS yoyo navigation rejection/manual: outside="+outside);
        }
        private static void Route(object context,bool wall,bool glove=true)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3278,0,0);p.position=new Vector2(700,646);p.channel=p.controlUseItem=true;p.yoyoGlove=glove;p.counterWeight=556;Main.screenPosition=new Vector2(600,400);
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(800,646);n.life=n.lifeMax=10000;n.target=0;Array.Clear(n.immune,0,n.immune.Length);
            if(wall)for(int y=39;y<=42;y++){Main.tile[48,y].active(true);Main.tile[48,y].type=1;}
            NativeToolExecutionChecks.Sample(context,Get(context,"Input"),new Vector2(650,450),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0,true);
            typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var shot=Main.projectile.Single(q=>q.active && q.type==541);
            int first=-1;bool sawCorner=false;int life=n.life;
            for(int tick=1;tick<=100 && shot.active;tick++)
            {
                NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,Get(context,"Input"),new Vector2(650,450),true);Call(combat,"Sample");NativeCombatAimChecks.Prepare(host,attack,n,0,true);
                if(wall && tick<=3){var dictionary=(System.Collections.IDictionary)Get(Get(attack,"Control"),"shots");var entry=dictionary[(int)shot.key];var nav=Get(entry,"Navigation");Console.WriteLine("YOYO route difference tick="+tick+" center="+shot.Center+" v="+shot.velocity+" point="+Get(entry,"Point")+" has="+Get(entry,"HasPoint")+" path="+string.Join(";",((System.Collections.IEnumerable)Get(nav,"path")).Cast<object>())+" searches="+Get(nav,"Searches")+" stop="+Get(nav,"ReplayStop")+" step="+Get(nav,"ReplayStep")+" futurePlayer="+Get(nav,"ReplayPlayer"));if(tick==1)Require((bool)Get(entry,"HasPoint"),"supported wall route needs a first genuine navigation capability, not eventual manual Damage");}
                foreach(var q in Main.projectile.Where(q=>q.active).ToArray())q.Update(q.whoAmI);
                sawCorner|=shot.Center.Y<623;for(int i=0;i<n.immune.Length;i++)if(n.immune[i]>0)n.immune[i]--;
                if(n.life<life){first=tick;break;}
            }
            Require(first>0,"finite yoyo route reaches real native Damage: wall="+wall+" center="+shot.Center+" ai="+shot.ai[0]+","+shot.ai[1]);
            Require(!wall || sawCorner,"wall route actually goes around ball-sized upper gap above real player support");
            Console.WriteLine("PASS yoyo real route/Damage: wall="+wall+" first="+first+" corner="+sawCorner);
            if(!glove)
            {
                var natural=Main.projectile.Single(q=>q.active && q.type==556);var registry=(System.Collections.IDictionary)Get(Get(attack,"Control"),"shots");Require(!registry.Contains((int)natural.key),"natural parent Damage birth cannot register counterweight as controllable ball");
                NativeCombatAimChecks.Prepare(host,attack,n,0,true);int mouseX=Main.mouseX,mouseY=Main.mouseY;natural.AI();Require(Main.mouseX==mouseX && Main.mouseY==mouseY,"natural counterweight AI preserves physical mouse delta input");Console.WriteLine("PASS natural Damage counterweight source exclusion");return;
            }
            var second=Main.projectile.Single(q=>q.active && q.type==541 && !ReferenceEquals(q,shot));NativeCombatAimChecks.Prepare(host,attack,n,0,true);var entries=(System.Collections.IDictionary)Get(Get(attack,"Control"),"shots");var secondEntry=entries[(int)second.key];Require(secondEntry!=null && GetOptional(secondEntry,"Navigation")!=null,"glove second ball inherits actual Damage source and its own navigator");bool has=(bool)Get(secondEntry,"HasPoint");Vector2 point=(Vector2)Get(secondEntry,"Point");second.AI();Require(!has || Math.Abs(second.ai[0]-point.X)<1.1f,"second ball consumes its own feasible motor result");var secondNav=Get(secondEntry,"Navigation");Console.WriteLine("YOYO paired replay: wall="+wall+" usable="+has+" stop="+Get(secondNav,"ReplayStop")+" step="+Get(secondNav,"ReplayStep")+" futurePlayer="+Get(secondNav,"ReplayPlayer"));
            p.yoyoGlove=false;p.Counterweight(n.Center,shot.damage,shot.knockBack);var counter=Main.projectile.Single(q=>q.active && q.type==556);int x=Main.mouseX,yMouse=Main.mouseY;counter.AI();Require(Main.mouseX==x && Main.mouseY==yMouse,"counterweight does not borrow navigation cursor");
            Console.WriteLine("PASS yoyo real route/Damage/paired role: wall="+wall+" first="+first+" corner="+sawCorner);
        }
    }
}
