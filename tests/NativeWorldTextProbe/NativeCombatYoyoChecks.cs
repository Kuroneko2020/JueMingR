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
        internal static void Run(object context)
        {
            // Existing isolated achievement outlet: native Shoot/AI/Damage
            // remain intact; this CPU component has no achievement service.
            var audit=new Harmony("JueMingR.Tests.YoyoAimAchievement");var method=typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod("HandleSpecialEvent",BindingFlags.Static|BindingFlags.Public);
            audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            try{RunCore(context);}finally{audit.Unpatch(method,HarmonyPatchType.All,audit.Id);}
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
            Lifetime(context,3278);Lifetime(context,3389);LargeEdge(context);
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
            NativeToolExecutionChecks.Sample(context,Get(context,"Input"),new Vector2(650,450),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0,true);
            typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var shot=Main.projectile.Single(q=>q.active && q.type==541);NativeCombatAimChecks.Prepare(host,attack,n,0,true);shot.AI();
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
