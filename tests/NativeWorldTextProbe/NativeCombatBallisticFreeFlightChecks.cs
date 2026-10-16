using System;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatBallisticFreeFlightChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static int currentType,strikes,births,enhanced;
        private static NPC target;private static object context;private static bool physicalOnly;private static Vector2 expectedPhysical;
        private static void Damage(Projectile __instance){currentType=__instance.type;}
        private static void AfterDamage(){currentType=0;}
        private static void Strike(NPC __instance,int __result){if(ReferenceEquals(__instance,target) && __result>0 && (currentType==206 || currentType==410 || currentType==523 || currentType==876 || currentType==496 || currentType==1082 || currentType==1083 || currentType==409))strikes++;}
        private static void Born(IEntitySource __0,int __result)
        {
            if(__result<0 || __0 is EntitySource_Parent)return;var shot=Main.projectile[__result];
            if(shot.type!=206 && shot.type!=410 && shot.type!=523 && shot.type!=876 && shot.type!=496 && shot.type!=1082 && shot.type!=1083 && shot.type!=409)return;
            births++;var attack=Get(Get(context,"Combat"),"Attack");
            if((bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced")enhanced++;
            if(physicalOnly)Require(Vector2.DistanceSquared(Main.MouseWorld,expectedPhysical)<3,"invalidated release source retains the physical native cursor");
        }
        internal static void Run(object value)
        {
            using(var trace=new NativeCombatWeaponPreparationTrace(value,3772))RunCore(value);
        }
        private static void RunCore(object value)
        {
            context=value;var host=Get(context,"Combat").GetType().Assembly;int cases=0,actions=0;
            var type=host.GetType("JueMingR.TerrariaHost.Combat.HostFreeFlightAttack",true);
            var copy=host.GetType("JueMingR.TerrariaHost.Combat.HostBetsyFlameAttack",true).GetMethod("Copy",Flags);
            var terrainType=host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);
            foreach(int projectile in new[]{206,410,523,876,496,1082,1083})foreach(int liquid in new[]{-1,0,2})
            {
                var player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),1178,0,0);Main.windPhysics=false;
                foreach(var npc in Main.npc)npc.active=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();
                if(liquid>=0)for(int x=20;x<115;x++)for(int y=5;y<78;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(liquid);}
                int slot=Projectile.NewProjectile(player.GetProjectileSource_Item(player.HeldItem),new Vector2(700,350),new Vector2(9,-1),projectile,0,0,player.whoAmI);
                var native=Main.projectile[slot];
                // Freeze the first actual RNG-dependent state, then compare
                // deterministic continuation. No native/random call happens
                // in the product model and no expected random value is forced.
                NativeQuickItemChecks.BeginWorldStep();native.Update(slot);
                var model=(Projectile)copy.Invoke(null,new object[]{native});var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");
                bool representative=false;
                for(int outer=1;outer<=40 && native.active;outer++)
                {
                    for(int sub=0;sub<=model.extraUpdates && model.active;sub++)
                    {
                        var args=new object[]{model,terrain,representative};Require((bool)type.GetMethod("Step",Flags).Invoke(null,args),"free flight acquired concrete terrain");representative=(bool)args[2];
                        if(model.active && --model.timeLeft<=0)model.active=false;actions++;
                    }
                    NativeQuickItemChecks.BeginWorldStep();native.Update(slot);
                    Require(native.active==model.active && (!native.active || Vector2.DistanceSquared(native.position,model.position)<.002f && Vector2.DistanceSquared(native.velocity,model.velocity)<.0001f && native.width==model.width && native.height==model.height && native.timeLeft==model.timeLeft),
                        "native observed free flight/body/clock agrees: "+projectile+"/"+liquid+"/"+outer+" native="+native.position+"/"+native.velocity+" model="+model.position+"/"+model.velocity);
                }
                cases++;
            }
            var audit=new Harmony("JueMingR.Tests.FreeFlightNativeInput");
            // The isolated native world has no Steam achievements manager.
            // Keep the same presentation-only exclusion as other actual-use
            // fixtures; Shoot, motors, receiver policy and Damage remain native.
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBallisticFreeFlightChecks),nameof(Damage)),postfix:new HarmonyMethod(typeof(NativeCombatBallisticFreeFlightChecks),nameof(AfterDamage)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatBallisticFreeFlightChecks),nameof(Strike)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatBallisticFreeFlightChecks),nameof(Born)));
            try
            {
                foreach(int weapon in new[]{1178,2623,3210,4347,4348,3053,3772,3352,2622})
                {
                    string only=Environment.GetEnvironmentVariable("JMR_FREE_FLIGHT_ITEM");if(!string.IsNullOrEmpty(only) && only!=weapon.ToString())continue;
                    var player=NativeCombatBallisticCandidateChecks.Scene(context,weapon,48);target=Main.npc[2];strikes=births=enhanced=0;player.statMana=player.statManaMax=player.statManaMax2=10000;
                    NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new JueMingR.Features.Combat.ObservationOptions(false,true,true,true,true,50,true,true));
                    var raw=target.Center-new Vector2(500,0);
                    // BoneSword/Bladetongue read on native use release. This
                    // is the real delayed source; holding forever never fires.
                    for(int tick=0;tick<180 && strikes==0;tick++)NativeCombatSelectionPreviewChecks.Step(context,raw,tick%40<20);
                    Console.WriteLine("BALLISTIC-FREE-FLIGHT item="+weapon+" births="+births+" enhanced="+enhanced+" positiveStrikes="+strikes);
                    Require(births>0 && enhanced>0 && strikes>0,"actual original cursor/Shoot/Update/Damage uses the finite enhanced hand: "+weapon);
                    if(weapon==3772 || weapon==3352)
                    {
                        player=NativeCombatBallisticCandidateChecks.Scene(context,weapon,48);target=Main.npc[2];strikes=births=enhanced=0;player.statMana=player.statManaMax=player.statManaMax2=10000;
                        NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new JueMingR.Features.Combat.ObservationOptions(false,true,true,true,true,50,true,true));raw=target.Center-new Vector2(500,0);
                        for(int tick=0;tick<180;tick++)NativeCombatSelectionPreviewChecks.Step(context,raw,true);
                        Require(births==0 && enhanced==0,"native release-only item never emits a source while continuously held");
                        ReleaseInvalidation(context,weapon);
                    }
                }
            }
            finally{audit.UnpatchAll(audit.Id);target=null;context=null;physicalOnly=false;}
            Console.WriteLine("BALLISTIC-FREE-FLIGHT native-cases="+cases+" actions="+actions+" true-input-items=9 requested="+Environment.GetEnvironmentVariable("JMR_FREE_FLIGHT_ITEM"));
        }
        private static void ReleaseInvalidation(object value,int weapon)
        {
            var input=Get(value,"Input");var host=Get(value,"CombatObservation");
            foreach(string invalidation in new[]{"aim-off","ui","weapon-instance","receiver","focus"})
            {
                var p=NativeCombatBallisticCandidateChecks.Scene(value,weapon,48);target=Main.npc[2];strikes=births=enhanced=0;p.statMana=p.statManaMax=p.statManaMax2=10000;var raw=target.Center-new Vector2(500,0);
                NativeCombatObservationChecks.Save(host,new JueMingR.Features.Combat.ObservationOptions(false,true,true,true,true,50,true,true));
                for(int tick=0;tick<20;tick++)NativeCombatSelectionPreviewChecks.Step(value,raw,true);
                if(invalidation=="aim-off")NativeCombatObservationChecks.Save(host,new JueMingR.Features.Combat.ObservationOptions(false,true,true,true,true,50,true,false));
                else if(invalidation=="ui")Main.blockInput=true;
                else if(invalidation=="weapon-instance"){var replacement=new Item();replacement.SetDefaults(weapon);p.inventory[p.selectedItem]=replacement;}
                else if(invalidation=="receiver"){target.active=false;target=NativeCombatControlPreviewChecks.Dummy(3,48,40);}
                else if(invalidation=="focus")Set(input,"foregroundWindow",(Func<IntPtr>)(()=>IntPtr.Zero));
                physicalOnly=true;expectedPhysical=raw;
                try{for(int tick=0;tick<20;tick++)NativeCombatSelectionPreviewChecks.Step(value,raw,false);}
                finally{physicalOnly=false;Main.blockInput=false;Set(input,"foregroundWindow",(Func<IntPtr>)(()=>new IntPtr(1)));}
                Require(enhanced==0,"release credential invalidates on "+invalidation+" item="+weapon);
                if(invalidation=="aim-off" || invalidation=="weapon-instance" || invalidation=="receiver")Require(births>0,"invalidated release preserves the native source when gameplay still permits it");
                Console.WriteLine("BALLISTIC-FREE-FLIGHT invalidated="+invalidation+" item="+weapon+" nativeBirths="+births+" enhanced="+enhanced);
            }
        }
    }
}
