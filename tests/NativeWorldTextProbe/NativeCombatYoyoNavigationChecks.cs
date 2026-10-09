using System;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatYoyoNavigationChecks
    {
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3278,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.channel=p.controlUseItem=true;p.yoyoGlove=p.magicString=false;p.counterWeight=0;Main.screenPosition=new Vector2(600,400);
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(800,646);n.velocity=n.netOffset=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=10000;n.target=0;n.knockBackResist=0;n.shimmerTransparency=0;Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);
            NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0,true);
            var control=Get(attack,"Control");Main.tile[48,41].active(true);Main.tile[48,41].type=1;var staleOpening=Call(control,"BeginOpening",p,p.HeldItem);if(staleOpening!=null)Call(staleOpening,"End");Console.WriteLine("YOYO stale opening: borrowed="+(staleOpening!=null));Require(staleOpening==null,"birth navigation owns its terrain dependency until consumption");Main.tile[48,41].active(false);NativeCombatAimChecks.Prepare(host,attack,n,0,true);
            p.velocity.X=2;var movedOpening=Call(control,"BeginOpening",p,p.HeldItem);if(movedOpening!=null)Call(movedOpening,"End");Require(movedOpening==null,"birth path also retires after player movement premise changes without preparing again");p.velocity=Vector2.Zero;NativeCombatAimChecks.Prepare(host,attack,n,0,true);
            typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var shot=Main.projectile.Single(q=>q.active && q.type==541);NativeCombatAimChecks.Prepare(host,attack,n,0,true);var entries=(System.Collections.IDictionary)Get(Get(attack,"Control"),"shots");var entry=entries[(int)shot.key];var nav=Get(entry,"Navigation");int searches=(int)Get(nav,"Searches");Require((bool)Get(entry,"HasPoint"),"open scene starts with a real replayed navigation capability");
            for(int i=0;i<4;i++)NativeCombatAimChecks.Prepare(host,attack,n,0,true);Require((int)Get(nav,"Searches")==searches,"same sample/terrain/player does not search again");
            Main.tile[48,41].active(true);Main.tile[48,41].type=1;
            Console.WriteLine("YOYO stale terrain: impact="+(GetOptional(attack,"ExpectedImpact")!=null)+" searches="+Get(nav,"Searches"));
            Require(GetOptional(attack,"ExpectedImpact")==null && Call(Get(attack,"Control"),"BeginAI",shot)==null && (int)Get(nav,"Searches")==searches,"navigation's own changed terrain retires Draw/AI without a new search");
            Main.tile[48,41].active(false);NativeCombatAimChecks.Prepare(host,attack,n,0,true);int freshSearches=(int)Get(nav,"Searches");p.velocity.X=2;
            Require(GetOptional(attack,"ExpectedImpact")==null && Call(Get(attack,"Control"),"BeginAI",shot)==null && (int)Get(nav,"Searches")==freshSearches,"changed player movement retires navigation before Draw/AI without solving");p.velocity=Vector2.Zero;NativeCombatAimChecks.Prepare(host,attack,n,0,true);searches=(int)Get(nav,"Searches");
            // An observed authoritative player correction is a new premise;
            // never pretend the old player's control-radius route is current.
            p.position.X+=12;NativeCombatAimChecks.Prepare(host,attack,n,0,true);Require((int)Get(nav,"Searches")==searches+1 && (bool)Get(entry,"HasPoint"),"player position change revises the route while preserving useful control");
            for(int y=5;y<=42;y++){Main.tile[48,y].active(true);Main.tile[48,y].type=1;}NativeCombatAimChecks.Prepare(host,attack,n,0,true);Require(!(bool)Get(entry,"HasPoint") && GetOptional(attack,"ExpectedImpact")==null,"related closed terrain immediately removes navigation/red capability");int blockedSearches=(int)Get(nav,"Searches");
            for(int tick=1;tick<=12;tick++)
            {NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");n.UpdateNPC(2);NativeCombatAimChecks.Prepare(host,attack,n,0,true);Require(!(bool)Get(entry,"HasPoint"),"stable sealed route cannot become a geometric-only input lease");shot.Update(shot.whoAmI);}
            int retries=(int)Get(nav,"Searches")-blockedSearches;Require(retries<=3 && shot.active && n.life==10000,"stable no-progress route retries finitely and retains native manual ball without phantom Damage");
            for(int y=5;y<=42;y++)Main.tile[48,y].active(false);long first=-1,expected=-1;
            for(int tick=1;tick<=40 && first<0;tick++)
            {NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");n.UpdateNPC(2);NativeCombatAimChecks.Prepare(host,attack,n,0,true);var plan=(AttackContact)GetOptional(entry,"Contact");if(plan!=null)expected=plan.Timeline.SampleTick+plan.Tick;int life=n.life;shot.Update(shot.whoAmI);if(n.life<life)first=Main.GameUpdateCount;}
            Console.WriteLine("YOYO navigation changes: stableSearches="+searches+" closedRetries="+retries+" first="+first+" expected="+expected+" center="+shot.Center);Require(first>0 && first==expected,"reopened local route resumes useful input and the real native first Damage clock");
            Gap(context,false);Gap(context,true);Progress(context);
        }
        internal static void RunIsolated(object context)
        {
            // Same narrowly scoped original achievement outlet as YoyoChecks;
            // this CPU entry has no achievement host. AI/movement/Damage and
            // progress remain original; remove only this probe's patch finally.
            var audit=new HarmonyLib.Harmony("JueMingR.Tests.YoyoNavigationAchievement");var method=typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod("HandleSpecialEvent",BindingFlags.Static|BindingFlags.Public);
            audit.Patch(method,prefix:new HarmonyLib.HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            try{Run(context);}finally{audit.Unpatch(method,HarmonyLib.HarmonyPatchType.All,audit.Id);}
        }
        private static void Progress(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3278,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.channel=p.controlUseItem=true;p.yoyoGlove=p.magicString=false;p.counterWeight=0;Main.screenPosition=new Vector2(600,400);
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(780,646);n.velocity=n.netOffset=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=10000;n.target=0;n.knockBackResist=0;n.shimmerTransparency=0;Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);
            NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0,true);
            typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var shot=Main.projectile.Single(q=>q.active && q.type==541);int key=(int)shot.key;n.immune[0]=200;
            // Observed finite owner immunity (naturally decremented below)
            // prevents hit rebound; 541 does not use its local immunity array.
            // the native motor itself still approaches and brakes near the goal.
            // Do not freeze/move the ball or write the private progress clock.
            var control=Get(attack,"Control");var entries=(System.Collections.IDictionary)Get(control,"shots");var entry=entries[key];var nav=Get(entry,"Navigation");bool expired=false;uint retired=0;int searches=0;
            for(int i=0;i<120 && shot.active;i++)
            {
                NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");n.UpdateNPC(2);NativeCombatAimChecks.Prepare(host,attack,n,0,true);
                int count=((System.Collections.IList)Get(nav,"path")).Count;uint age=Main.GameUpdateCount-(uint)Get(nav,"progressStep");bool point=(bool)Get(entry,"HasPoint");int work=(int)Get(nav,"Searches");
                if(i%10==0 || age>=13 || expired)Console.WriteLine("YOYO progress tick="+Main.GameUpdateCount+" age="+age+" path="+count+" point="+point+" searches="+work+" immune="+n.immune[0]+" center="+shot.Center+" velocity="+shot.velocity);
                if(!expired && count==0 && (uint)Get(nav,"retryStep")==Main.GameUpdateCount+5 && age==15)
                {expired=true;retired=Main.GameUpdateCount;searches=work;Require(!point && GetOptional(attack,"ExpectedImpact")==null && Call(control,"BeginAI",shot)==null,"natural fifteen-frame low movement retires only the navigation capability and red marker");}
                else if(expired && Main.GameUpdateCount<retired+5)Require(!point && work==searches,"retired route waits its bounded five-frame retry without searching");
                else if(expired && Main.GameUpdateCount==retired+5)
                {Require(point && work==searches+1 && shot.active && (int)shot.key==key,"same real live ball recovers the valid route once at the retry boundary");Console.WriteLine("PASS natural valid-route progress>14 retirement and +5 recovery; native low-movement braking, not an obstacle deadlock claim");return;}
                shot.Update(shot.whoAmI);
            }
            Require(false,"native live ball must naturally reach the valid-route low-progress branch before recall");
        }
        private static void Gap(object context,bool thin)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3278,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.channel=p.controlUseItem=true;p.yoyoGlove=p.magicString=false;p.counterWeight=0;Main.screenPosition=new Vector2(600,400);
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(800,646);n.velocity=n.netOffset=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=10000;n.target=0;n.knockBackResist=0;n.shimmerTransparency=0;Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);
            for(int y=5;y<=42;y++){Main.tile[48,y].active(true);Main.tile[48,y].type=1;}
            if(thin)Main.tile[48,41].halfBrick(true);else{Main.tile[48,40].active(false);Main.tile[48,41].active(false);}
            NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0,true);
            Console.WriteLine("YOYO gap birth input: usable="+Get(Get(attack,"Control"),"openingUsable")+" point="+Get(Get(attack,"Control"),"openingPoint")+" mouse="+Main.MouseWorld+" player="+p.Center+" grav="+p.gravDir);
            var openingNav=GetOptional(Get(attack,"Control"),"openingNavigation");if(openingNav!=null)Console.WriteLine("YOYO gap birth path: "+string.Join(";",((System.Collections.IEnumerable)Get(openingNav,"path")).Cast<object>()));
            if(!thin)Require(((Vector2)Get(Get(attack,"Control"),"openingPoint")).X>p.Center.X,"first visible forward entrance is preferred over a backward BFS tie for the same ball-sized gap");
            typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var shot=Main.projectile.Single(q=>q.active && q.type==541);var entries=(System.Collections.IDictionary)Get(Get(attack,"Control"),"shots");var entry=entries[(int)shot.key];NativeCombatAimChecks.Prepare(host,attack,n,0,true);bool initial=(bool)Get(entry,"HasPoint");long first=-1,expected=-1;
            var nav=Get(entry,"Navigation");Console.WriteLine("YOYO gap opening: thin="+thin+" center="+shot.Center+" velocity="+shot.velocity+" point="+Get(entry,"Point")+" replay="+GetOptional(nav,"ReplayStop")+" step="+GetOptional(nav,"ReplayStep"));
            if(!thin)Require(initial && GetOptional(entry,"Contact")==null && GetOptional(attack,"ExpectedImpact")==null && (string)Get(nav,"ReplayStop")=="BallTerrain" && (int)Get(nav,"ReplayStep")>=3,"geometric route with a later inertia/corner rejection grants only a verified short input prefix, no red impact");
            for(int tick=1;tick<=45 && first<0;tick++)
            {NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");n.UpdateNPC(2);NativeCombatAimChecks.Prepare(host,attack,n,0,true);var plan=(AttackContact)GetOptional(entry,"Contact");expected=plan==null?-1:plan.Timeline.SampleTick+plan.Tick;if(thin)Require(!(bool)Get(entry,"HasPoint") && GetOptional(attack,"ExpectedImpact")==null,"eight-pixel gap cannot accept a sixteen-pixel native ball");if(tick<=3 || plan!=null)Console.WriteLine("YOYO gap before: tick="+Main.GameUpdateCount+" center="+shot.Center+" v="+shot.velocity+" plan="+(plan==null?"null":expected.ToString())+" point="+Get(entry,"Point")+" replay="+GetOptional(nav,"ReplayStop")+" step="+GetOptional(nav,"ReplayStep"));int life=n.life;shot.Update(shot.whoAmI);if(n.life<life)first=Main.GameUpdateCount;}
            Console.WriteLine("YOYO gap result: pixels="+(thin?8:32)+" initial="+initial+" first="+first+" expected="+expected+" ball="+shot.width+"x"+shot.height+" center="+shot.Center+" velocity="+shot.velocity);
            Require(thin?!initial && first<0:initial && first>0 && first==expected,"ball-sized turning gap reaches real Damage at the current Contact tick; thinner gap preserves native rejection");
        }
    }
}
