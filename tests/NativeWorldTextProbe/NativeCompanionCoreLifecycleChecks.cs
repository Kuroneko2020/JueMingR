using System;
using System.Collections;
using System.Linq;
using HarmonyLib;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionCoreLifecycleChecks
    {
        private static object lifecycleHost;
        private static Player preparedHuman;
        private static bool HumanDiskSink(){return Call(lifecycleHost,"Owned",Main.LocalPlayer)!=null;}
        internal static void PrepareHuman()
        {
            var human=Main.LocalPlayer;if(ReferenceEquals(preparedHuman,human))return;
            // Reuse the established Browser/Journey native startup sequence,
            // then the exact Deserialize-owned payload and real Spawn stage.
            if(Recipe.numRecipes==0)
            {
                Recipe.SetupRecipeGroups();
                for(int i=0;i<Main.recipe.Length;i++)Main.recipe[i]=new Recipe();
                Recipe.SetupRecipes();ContentSamples.FixItemsAfterRecipesAreAdded();
            }
            Terraria.Map.MapHelper.Initialize();
            if(human.savedPerPlayerFieldsThatArentInThePlayerClass==null)human.savedPerPlayerFieldsThatArentInThePlayerClass=new Player.SavedPlayerDataWithAnnoyingRules();
            human.Spawn(PlayerSpawnContext.SpawningIntoWorld);preparedHuman=human;
        }
        internal static void Run(object host,Player first,Guid other)
        {
            Require((bool)Call(host,"Join",other),"second real profile joins lifecycle fixture: "+Get(host,"Feedback"));
            var second=(Player)Call(host,"GetBody",other);var a=Call(host,"Owned",first);var b=Call(host,"Owned",second);
            var firstProfile=(CompanionProfile)Get(a,"Profile");var otherProfile=(CompanionProfile)Get(b,"Profile");
            var human=Main.LocalPlayer;bool map=Main.mapFullscreen;int slot=Main.myPlayer;
            var files=new Harmony("JueMingR.Tests.CompanionLifecycleHumanFiles");lifecycleHost=host;
            // The native KillMe request stays real. This headless test does
            // not initialize the human map/file writer and does not claim its
            // disk persistence; AI requests retain their production owner.
            files.Patch(AccessTools.Method(typeof(WorldGen),"saveToonWhilePlaying"),prefix:new HarmonyMethod(typeof(NativeCompanionCoreLifecycleChecks),nameof(HumanDiskSink)));
            try
            {
                // The parent quick-item fixture has not entered a real world
                // with its human body. Initialize its native spawn/fall state
                // before asking full Player.Update to run (the AI Join already
                // does this). Never manufacture a death/respawn with dead=true.
                PrepareHuman();
                Console.WriteLine("LIFECYCLE begin native buffs humanLife="+human.statLife+" first="+first.statLife+" second="+second.statLife);
                Call(host,"SetBehavior",firstProfile.Id,new CompanionBehavior{Follow=false,AttackEnabled=false});
                Call(host,"SetBehavior",other,new CompanionBehavior{Follow=true,AttackEnabled=false});
                foreach(var npc in Main.npc)npc.active=false;
                foreach(var p in new[]{human,first,second}){p.AddBuff(BuffID.Poisoned,p==human?17:p==first?11:7);}
                int h=human.buffTime[human.FindBuffIndex(BuffID.Poisoned)],f=first.buffTime[first.FindBuffIndex(BuffID.Poisoned)],s=second.buffTime[second.FindBuffIndex(BuffID.Poisoned)];
                NativeQuickItemChecks.BeginWorldStep();human.Update(human.whoAmI);first.Update(first.whoAmI);second.Update(second.whoAmI);
                Console.WriteLine("LIFECYCLE one world step humanDead="+human.dead+" firstDead="+first.dead+" secondDead="+second.dead);
                Require(human.buffTime[human.FindBuffIndex(BuffID.Poisoned)]==h-1 && first.buffTime[first.FindBuffIndex(BuffID.Poisoned)]==f-1 && second.buffTime[second.FindBuffIndex(BuffID.Poisoned)]==s-1,"human and two actors decrement different native buffs exactly once");
                first.Update(first.whoAmI);Require(first.buffTime[first.FindBuffIndex(BuffID.Poisoned)]==f-1,"duplicate same-world-step body cannot double tick buff");
                for(int i=0;i<20;i++)Step(host,first,second);
                Require(first.FindBuffIndex(BuffID.Poisoned)<0 && second.FindBuffIndex(BuffID.Poisoned)<0 && !first.buffType.Contains(BuffID.Poisoned),"native expired debuff actually deleted while attack disabled");
                first.inventory[10].SetDefaults(ItemID.HealingPotion);first.inventory[10].stack=2;
                first.immune=false;first.immuneTime=0;Array.Clear(first.hurtCooldowns,0,first.hurtCooldowns.Length);
                first.Hurt(PlayerDeathReason.ByCustomReason("fixture native healing injury"),Math.Max(1,first.statLife-first.statLifeMax2/2),0);
                Step(host,first,second);
                Require(first.inventory[10].stack==1 && first.potionDelay>0 && (first.FindBuffIndex(BuffID.PotionSickness)>=0),"first native low-life action drinks one real potion");
                int cooldown=first.potionDelay;for(int i=0;i<cooldown+2;i++)Step(host,first,second);
                Require(first.potionDelay==0 && !(first.FindBuffIndex(BuffID.PotionSickness)>=0),"original sickness/cooldown naturally expires without clearing it");
                first.Hurt(PlayerDeathReason.ByCustomReason("fixture second native healing injury"),Math.Max(1,first.statLife-first.statLifeMax2/2),0);Step(host,first,second);
                Require(first.inventory[10].IsAir && first.potionDelay>0,"second low-life action consumes second bottle after real native timer");
                Console.WriteLine("PASS companion native buff timing/deletion and first potion -> full native cooldown -> second potion.");
                Minions(host,first,second,a,b);
                Require(Main.myPlayer==slot && Main.mapFullscreen==map,"lifecycle consumers restore human identity/map; human KillMe retains its own native mouse changes");
            }
            finally
            {
                Main.mapFullscreen=map;Call(host,"SetBehavior",firstProfile.Id,firstProfile.Behavior);
                Call(host,"SetBehavior",other,otherProfile.Behavior);Call(host,"Recall",other);
                NativeQuickItemChecks.Until(()=>{Call(host,"Update",0UL);return !((IDictionary)Get(host,"actors")).Contains(other);});
                files.UnpatchAll(files.Id);lifecycleHost=null;
            }
        }
        private static void Step(object host,params Player[] bodies)
        {
            NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",0UL);
            foreach(var p in bodies)p.Update(p.whoAmI);
            foreach(var p in bodies){var a=Call(host,"Owned",p);Require(a==null || !(bool)Get(a,"Faulted"),"real lifecycle update has no swallowed failure: "+(a==null?"retired":Get(a,"Status")));}
        }
        private static int Records(object respawner){return ((IList)Get(respawner,"_minions")).Count;}
        private static void Summon(object host,Player p,object actor)
        {
            p.inventory[1].SetDefaults(ItemID.BabyBirdStaff);
            if(actor==null)p.SilentlyShootItem(p.inventory[1]);
            else
            {
                var hooks=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionHooks");
                using((IDisposable)hooks.GetMethod("Enter",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{actor,p.Center,false}))p.SilentlyShootItem(p.inventory[1]);
            }
            Require(Main.projectile.Any(q=>q.active && q.owner==p.whoAmI && q.MinionSpawnInfo!=null),"real native item factory creates tracked summon record");
        }
        private static void Minions(object host,Player first,Player second,object a,object b)
        {
            var human=Main.LocalPlayer;var humanRespawner=AccessTools.Field(typeof(Player),"_localMinionRespawner").GetValue(null);
            Summon(host,human,null);human.KillMe(PlayerDeathReason.ByCustomReason("fixture human death"),999,0);
            Require(human.dead && Records(humanRespawner)>0,"real human KillMe collects native summon record");int humanCount=Records(humanRespawner);
            // Human KillMe intentionally assigns its own cursor slot. Compare
            // AI restoration against this legitimate entering value, not the
            // pre-human-death cursor reference.
            var mouse=Main.mouseItem;int mx=Main.mouseX,my=Main.mouseY,who=Main.myPlayer;
            first.KillMe(PlayerDeathReason.ByCustomReason("fixture actor without minions"),999,0);
            Require(first.dead && Records(Get(a,"Minions"))==0 && Records(humanRespawner)==humanCount,"AI true death with no summon cannot clear human pending recovery");
            Summon(host,second,b);second.KillMe(PlayerDeathReason.ByCustomReason("fixture second actor death"),999,0);
            Require(second.dead && Records(Get(b,"Minions"))>0 && Records(humanRespawner)==humanCount,"interleaved second actor true death owns separate recovery record");
            for(int i=0;i<200 && (first.dead || second.dead);i++){Step(host,first,second);StepProjectiles();}
            Require(!first.dead && !second.dead && Records(Get(b,"Minions"))==0 && Main.projectile.Any(q=>q.active && q.owner==second.whoAmI && q.MinionSpawnInfo!=null),"real UpdateDead/allowed spawn restores actor summon from native matching inventory");
            Summon(host,first,a);first.KillMe(PlayerDeathReason.ByCustomReason("fixture interleaved recall first"),999,0);
            second.KillMe(PlayerDeathReason.ByCustomReason("fixture interleaved recall second"),999,0);int remaining=Records(Get(a,"Minions"));
            var otherId=((CompanionProfile)Get(b,"Profile")).Id;Call(host,"Recall",otherId);
            Require(remaining>0 && Records(Get(a,"Minions"))==remaining && Records(Get(b,"Minions"))==0 && Records(humanRespawner)==humanCount,"recall during death retires only its own temporary record");
            for(int i=0;i<200 && first.dead;i++){Step(host,first);StepProjectiles();}
            Require(!first.dead && Records(Get(a,"Minions"))==0 && ReferenceEquals(Main.mouseItem,mouse) && Main.mouseX==mx && Main.mouseY==my && Main.myPlayer==who,"other actor native revival restores summon and exact human context after peer recall");
            while(human.dead){NativeQuickItemChecks.BeginWorldStep();human.Update(human.whoAmI);if(human.dead && human.respawnTimer>1 && human.AllowsRespawnTimerSkip())human.SkipRespawnTime();StepProjectiles();}
            Require(Records(humanRespawner)==0 && Main.projectile.Any(q=>q.active && q.owner==human.whoAmI && q.MinionSpawnInfo!=null),"human native respawn retains/restores original summon despite AI deaths");
            Console.WriteLine("PASS companion actual KillMe -> per-role minion collect -> native legal respawn/restore; human record retained.");
        }
        private static void StepProjectiles()
        {
            int previous=Main.ProjectileUpdateLoopIndex;
            try{for(int i=0;i<Main.projectile.Length;i++)if(Main.projectile[i].active){Main.ProjectileUpdateLoopIndex=i;Main.projectile[i].Update(i);}}
            finally{Main.ProjectileUpdateLoopIndex=previous;}
        }
    }
}
