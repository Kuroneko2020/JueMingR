using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Actual ItemCheck birth/AI/TileCollision/Damage are the result oracle.
    // A geometric polyline, predicted Contact or repeated replay is not a hit.
    internal static class NativeCombatGuidedNavigationChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static NPC receiver;
        private static int damage;
        private static uint firstDamage,damageBorn;
        private static bool progressWatch,shortComplete,longContact;
        private static int grownRequest;
        private static object watchedEntry;
        private static long contactAtDamage;
        private static object naturalControl,naturalAttack;
        private static bool natural;
        private static int nativeActions,preparedActions,basicActions,births,preparedBirthActions,observedSearches;
        private static readonly HashSet<uint> seen=new HashSet<uint>();
        private static void NativeInput(Projectile __instance)
        {
            if(!natural || __instance.owner!=Main.myPlayer || __instance.type!=16 && __instance.type!=34 && __instance.type!=79 && __instance.type!=491)return;nativeActions++;
            var p=Main.LocalPlayer;var entry=((IDictionary)Get(naturalControl,"shots"))[(int)__instance.key];
            bool first=seen.Add((uint)__instance.key);if(first){births++;Require(entry!=null,"first original AI has its same-pass registered causal source even when the budget provides only Basic");}
            if(entry==null)return;watchedEntry=entry;
            var navigation=GetOptional(entry,"Guided");if(navigation!=null)observedSearches=Math.Max(observedSearches,(int)Get(navigation,"Searches"));
            bool ready=(bool)Get(naturalAttack,"Permission") && (bool)Get(naturalAttack,"CurrentReceiver") && (uint)Get(entry,"Prepared")==Main.GameUpdateCount && (bool)Get(entry,"HasPoint") && p.channel && p.HeldItem.shoot==__instance.type && (__instance.type==491?__instance.ai[0]<=0:__instance.ai[0]>=0);
            if(!ready){basicActions++;return;}preparedActions++;if(first)preparedBirthActions++;
            var receipt=Get(naturalAttack,"consumedInput");var point=(Vector2)Get(entry,"Point");
            Require(Convert.ToString(Get(receipt,"Kind"))=="Enhanced" && Convert.ToString(Get(receipt,"PointSemantics"))=="WorldPoint" && Vector2.DistanceSquared(Main.MouseWorld,point)<2.01f,"a currently prepared guided first/continuing native AI actually consumes its world point, not physical input");
        }
        private static void Struck(NPC __instance,int __result)
        {
            if(ReferenceEquals(receiver,__instance) && __result>0 && damage++==0)
            {
                firstDamage=Main.GameUpdateCount;damageBorn=watchedEntry==null?0:(uint)Get(watchedEntry,"Born");var contact=watchedEntry==null?null:(AttackContact)GetOptional(watchedEntry,"Contact");
                contactAtDamage=contact==null?-1:contact.Timeline.SampleTick+contact.Tick;
            }
        }
        internal static void Run(object context)
        {
            var audit=new Harmony("JueMingR.Tests.GuidedNavigation");
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})
                audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks).GetMethod("SkipAchievement",Flags)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatGuidedNavigationChecks).GetMethod(nameof(Struck),Flags)));
            audit.Patch(AccessTools.Method(typeof(Projectile),"AI_009_MagicMissiles"),prefix:new HarmonyMethod(typeof(NativeCombatGuidedNavigationChecks).GetMethod(nameof(NativeInput),Flags)));
            try
            {
                NativeCombatGuidedLiquidChecks.Run(context);NativeCombatGuidedBudgetChecks.Run(context);
                foreach(int weapon in new[]{113,218,495,3030})AroundWall(context,weapon,weapon==495,weapon==218);
                ClearLineProgress(context);PrefixAndDependencies(context);Geometry(context);Lifecycle(context);
            }
            finally
            {natural=false;receiver=null;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());}
        }
        private static Player Scene(object context,int weapon,bool interiorWall=false)
        {
            var host=Get(context,"CombatObservation");NativeCombatObservationChecks.Save(host,new ObservationOptions());
            var combat=Get(context,"Combat");NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),weapon,0,0);
            int ground=interiorWall?53:43;
            if(interiorWall)for(int x=5;x<115;x++){Main.tile[x,43].ClearEverything();NativeToolsChecks.Tile(x,ground,1);}
            p.position=new Vector2(700,ground*16-p.height);p.ResetEffects();p.releaseUseItem=true;p.statMana=p.statManaMax=p.statManaMax2=1000;Main.screenPosition=new Vector2(600,interiorWall?560:400);
            Main.leftWorld=Main.topWorld=0;Main.rightWorld=Main.maxTilesX*16;Main.bottomWorld=Main.maxTilesY*16;
            receiver=NativeCombatControlPreviewChecks.Dummy(2,56,ground-3);damage=0;firstDamage=0;watchedEntry=null;contactAtDamage=-1;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,false,false,true,50,false,true));return p;
        }
        private static void AroundWall(object context,int weapon,bool moving,bool clear)
        {
            var p=Scene(context,weapon,true);var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");
            if(clear)NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,true,false,true,50,false,true));
            // Ground closes the bottom; the only legal route is over this
            // finite wall. Keep the entire turn inside vanilla's 640px world
            // play-area margin: the old 624px top was above legal mouse reach.
            // The wall/player/ground/dummy move together; no body is shrunk.
            for(int y=49;y<=52;y++)NativeToolsChecks.Tile(48,y,1);
            if(moving)for(int x=48;x<=51;x++)NativeToolsChecks.Tile(x,49,1);
            var turn=new Vector2(760,776);var limited=turn;p.LimitPointToPlayerReachableArea(ref limited);
            Require(Vector2.DistanceSquared(turn,limited)<.01f,"wall fixture has a real bypass turn inside vanilla's current legal mouse region");
            Projectile live=null;object entry=null,navigation=null;int maximumSearches=0,prefixes=0;long expected=-1;
            nativeActions=preparedActions=basicActions=births=preparedBirthActions=observedSearches=0;seen.Clear();naturalControl=Get(attack,"Control");naturalAttack=attack;natural=true;bool lastHeld=false;
            try{for(int tick=0;tick<100 && damage==0;tick++)
            {
                if(moving)Main.screenPosition+=new Vector2(2,1);
                bool active=Main.projectile.Any(q=>q.active && q.owner==p.whoAmI && q.type==p.HeldItem.shoot);
                // A cold Basic missile may naturally hit the wall and retire.
                // Use a real release/new click when no controller survives;
                // do not resurrect it or fake native auto-reuse for the fixture.
                bool held=tick==0 || active || !lastHeld;lastHeld=held;
                NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,660),held,false,moving && tick<12);
                var shot=Main.projectile.FirstOrDefault(q=>q.active && q.owner==p.whoAmI && q.type==p.HeldItem.shoot);
                if(live==null && shot!=null)
                {
                    live=shot;entry=((IDictionary)Get(Get(attack,"Control"),"shots"))[(int)shot.key];
                    Require(entry!=null,"first original birth registers its causal source");
                    watchedEntry=entry;
                    navigation=Get(entry,"Guided");Require(navigation!=null,"newborn has same-pass guided preparation");
                }
                if(shot!=null)
                {entry=((IDictionary)Get(Get(attack,"Control"),"shots"))[(int)shot.key];navigation=entry==null?null:GetOptional(entry,"Guided");}
                if(entry!=null && navigation!=null)
                {
                    maximumSearches=Math.Max(maximumSearches,(int)Get(navigation,"Searches"));
                    if((bool)Get(entry,"HasPoint") && GetOptional(entry,"Contact")==null)prefixes++;
                    var contact=(AttackContact)GetOptional(entry,"Contact");if(contact!=null)expected=contact.Timeline.SampleTick+contact.Tick;
                }
            }
            }finally{natural=false;}
            maximumSearches=Math.Max(maximumSearches,observedSearches);
            Console.WriteLine("GUIDED native bypass weapon="+weapon+" firstDamage="+firstDamage+" expected="+expected+" searches="+maximumSearches+" prefixes="+prefixes+" moving="+moving+" clear="+clear+" births="+births+" nativeAI="+nativeActions+" preparedAI="+preparedActions+" preparedFirstAI="+preparedBirthActions+" basicAI="+basicActions);
            Require(live!=null && damage>0,"four guided original consumers route around the finite wall and actually damage the original dummy: "+weapon);
            Require(contactAtDamage==firstDamage,"current guided Contact clock matches first original Damage: "+weapon+" actual="+firstDamage+" plan="+contactAtDamage);
            Require(maximumSearches>0,"wall scene exercises geometry rather than a direct control hit");
            Require(preparedActions>0,"normal six-ms repeated updates obtain actual guided input, not persistent Basic or a component-only proof");
            Require(!moving || p.position.X>700,"moving scene advances the actual player while the camera moves");
        }
        private static void ProgressPlan(object __0,NpcTrajectory __1,AttackContact __result)
        {
            if(!progressWatch || __1.Identity.Slot!=4)return;
            var window=Get(__0,"GuidedWindow");
            if(__result==null && (bool)Get(window,"Complete") && (int)Get(window,"Through")==16)shortComplete=true;
            if(__result!=null && __result.Tick>16 && ReferenceEquals(__result.Timeline,__1) && (uint)Get(__0,"PreparedTick")==Main.GameUpdateCount)longContact=true;
        }
        private static void ProgressRequest(NpcIdentity identity,int __result)
        {if(progressWatch && identity.Slot==4)grownRequest=Math.Max(grownRequest,__result);}
        private static void ClearLineProgress(object context)
        {
            var p=Scene(context,3030,true);p.position.X=1000;
            var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");
            // Near dummy is sealed on every side. The other direction stays
            // open, inside the real 640px control border. Native knife travel
            // and Strike, rather than a distance estimate, prove >16 ticks.
            receiver.active=false;NativeCombatControlPreviewChecks.Dummy(2,75,50);
            receiver=NativeCombatControlPreviewChecks.Dummy(4,40,50);
            for(int x=74;x<=78;x++)NativeToolsChecks.Tile(x,49,1);
            for(int y=49;y<=52;y++){NativeToolsChecks.Tile(74,y,1);NativeToolsChecks.Tile(78,y,1);}
            var limited=receiver.Center;p.LimitPointToPlayerReachableArea(ref limited);
            Require(Vector2.DistanceSquared(limited,receiver.Center)<.01f,"far clear-line receiver is inside vanilla's current control area");
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,true,false,true,50,false,true));
            naturalControl=Get(attack,"Control");naturalAttack=attack;natural=true;progressWatch=true;shortComplete=longContact=false;grownRequest=0;
            nativeActions=preparedActions=basicActions=births=preparedBirthActions=observedSearches=0;seen.Clear();
            var audit=new Harmony("JueMingR.Tests.GuidedCandidateProgress");
            audit.Patch(attack.GetType().GetMethod("PreparePlan",Flags),postfix:new HarmonyMethod(typeof(NativeCombatGuidedNavigationChecks).GetMethod(nameof(ProgressPlan),Flags)));
            audit.Patch(attack.GetType().GetMethod("CandidateProgressRequired",Flags),postfix:new HarmonyMethod(typeof(NativeCombatGuidedNavigationChecks).GetMethod(nameof(ProgressRequest),Flags)));
            bool chosen=false,multiple=false,lastHeld=false;
            try
            {
                for(int tick=0;tick<160 && damage==0;tick++)
                {
                    bool active=Main.projectile.Any(q=>q.active && q.owner==0 && q.type==491);
                    bool held=tick==0 || active || !lastHeld;lastHeld=held;
                    NativeCombatSelectionPreviewChecks.Step(context,new Vector2(1200,820),held);
                    var selection=Get(host,"Selection");multiple|=(int)Get(selection,"CandidateCount")>=2;
                    chosen|=((NpcIdentity)Get(selection,"Target")).Slot==4 && (bool)Get(selection,"ClearLine");
                }
            }
            finally{natural=progressWatch=false;audit.UnpatchAll(audit.Id);}
            Console.WriteLine("GUIDED candidate progression short16="+shortComplete+" request="+grownRequest+" longContact="+longContact+" selected="+chosen+" damage="+firstDamage+" born="+damageBorn+" contact="+contactAtDamage+" nativeAI="+nativeActions+" preparedAI="+preparedActions);
            Require(multiple && shortComplete && grownRequest>=32 && longContact,"multiple guided candidates complete 16, grow their own requested window and produce a fresh contact beyond 16");
            Require(chosen && damage>0 && damageBorn>0 && (long)firstDamage-damageBorn>16 && contactAtDamage==firstDamage,"clear-line selects the farther real positive and native knife damages it after more than 16 actual world steps at the Contact clock");
            Require(preparedActions>0,"actual guided input consumes the naturally selected positive under the normal cumulative allowance");
        }
        private static void PrefixAndDependencies(object context)
        {
            // Native Damage_PVE_Inner exempts single-penetration/no-local-
            // immunity missiles (16) from owner immunity. Flame lash (34)
            // actually consumes this gate, so this is a genuine no-Contact
            // premise rather than an instruction to hide a legal hit.
            var p=Scene(context,218,true);var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");var input=Get(context,"Input");
            p.channel=p.controlUseItem=true;receiver.immune[0]=200;for(int y=49;y<=52;y++)NativeToolsChecks.Tile(48,y,1);
            NativeToolExecutionChecks.Sample(context,input,new Vector2(600,660),true);Call(combat,"Sample");
            NativeCombatAimChecks.PrepareMechanismFixture(host,attack,receiver,0,true);
            typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});
            var shot=Main.projectile.Single(q=>q.active && q.owner==0 && q.type==34);
            Require(shot.maxPenetrate==2 && shot.usesLocalNPCImmunity && receiver.immune[shot.owner]==200,"prefix fixture uses the native owner-immunity gate instead of the single-penetration exception");
            NativeCombatAimChecks.PrepareMechanismFixture(host,attack,receiver,0,true);
            var control=Get(attack,"Control");var entry=((IDictionary)Get(control,"shots"))[(int)shot.key];var nav=Get(entry,"Guided");
            Require((bool)Get(entry,"HasPoint") && GetOptional(entry,"Contact")==null,"safe navigation prefix does not manufacture Contact through known owner immunity");
            int searches=(int)Get(nav,"Searches"),replays=(int)Get(nav,"Replays");
            for(int i=0;i<4;i++){var scope=Call(control,"BeginAI",shot);Require(scope!=null,"safe prefix without Contact is a real cursor capability");Call(scope,"End");}
            Require((int)Get(nav,"Searches")==searches && (int)Get(nav,"Replays")==replays,"repeated AI consumers do not search or replay futures");
            var point=(Vector2)Get(entry,"Point");NativeToolsChecks.Tile((int)point.X/16,(int)point.Y/16,1);
            var stale=Call(control,"BeginAI",shot);if(stale!=null)Call(stale,"End");
            Require(!(bool)Get(entry,"HasPoint") && GetOptional(entry,"Contact")==null && (int)Get(nav,"Searches")==searches,"changed route terrain revokes the capability without searching in AI");
        }
        private static void Geometry(object context)
        {
            var p=Scene(context,3030);var attack=Get(Get(context,"Combat"),"Attack");
            var routeType=attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostGuidedRoute",true);
            var sample=new Projectile();sample.SetDefaults(491);sample.owner=0;sample.Center=new Vector2(720,660);
            Vector2 anchor;int width,height;sample.GetCollisionParams(out anchor,out width,out height);Require(width==10 && height==10,"narrow-gap negative uses the real ten-pixel knife collision body, not a magic missile's larger damage rectangle");
            var id=new NpcIdentity((long)Get(Get(context,"CombatObservation"),"Session"),receiver,receiver.whoAmI,receiver.generation,receiver.type,receiver.netID);
            var goal=new MotionRect(840,640,32,48);
            for(int y=5;y<43;y++)NativeToolsChecks.Tile(48,y,1);
            var route=Activator.CreateInstance(routeType,true);
            Require(!(bool)Call(route,"Prepare",p,sample,id,goal),"sealed wall has no whole-body route inside the native control area");
            int rejects=(int)Get(route,"BlockRejections");
            Require(!(bool)Call(route,"Clear",sample.Center,new Vector2(goal.CenterX,goal.CenterY)) && (int)Get(route,"BlockRejections")>rejects,"known native full-tile blocker rejects a repeated long goal ray without granting passage");
            Main.tile[48,41].halfBrick(true);
            Require(!(bool)Call(route,"Prepare",p,sample,id,goal),"a half-block slit smaller than the native body cannot become a route");
            Main.tile[48,40].ClearEverything();Main.tile[48,41].ClearEverything();
            var opening=new Vector2(776,648);var limited=opening;p.LimitPointToPlayerReachableArea(ref limited);
            Require(Vector2.DistanceSquared(opening,limited)<.01f,"ten-pixel positive gap has a legal native control point inside the 640px play-area border");
            Require((bool)Call(route,"Prepare",p,sample,id,goal),"body-sized opening has a strict route");
            int searches=(int)Get(route,"Searches");p.position.X+=2;goal=new MotionRect(842,640,32,48);
            Require((bool)Call(route,"Prepare",p,sample,id,goal) && (int)Get(route,"Searches")==searches,"small player/target movement reuses geometry and updates the terminal");
        }
        private static void Lifecycle(object context)
        {
            foreach(int weapon in new[]{113,3030})
            {
                var p=Scene(context,weapon);receiver.position.X=1450;
                NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),true);
                var shot=Main.projectile.Single(q=>q.active && q.owner==0 && q.type==p.HeldItem.shoot);
                NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),false);
                Require(weapon==3030?!shot.active || shot.ai[0]>0:!shot.active || shot.ai[0]<0,"native release retains autonomous missile/knife-return phase");
                p.channel=true;shot.AI();Require(weapon==3030?!shot.active || shot.ai[0]>0:!shot.active || shot.ai[0]<0,"released old source cannot regain guided control");
            }
        }
    }
}
