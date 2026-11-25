using System;
using System.Collections;
using System.Collections.Generic;
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
                first.Hurt(PlayerDeathReason.ByCustomReason("fixture native healing injury"),Math.Max(1,first.statLife-first.statLifeMax2/4),0);
                Step(host,first,second);
                Require(first.inventory[10].stack==1 && first.potionDelay>0 && (first.FindBuffIndex(BuffID.PotionSickness)>=0),"first native low-life action drinks one real potion");
                int cooldown=first.potionDelay;for(int i=0;i<cooldown+2;i++)Step(host,first,second);
                Require(first.potionDelay==0 && !(first.FindBuffIndex(BuffID.PotionSickness)>=0),"original sickness/cooldown naturally expires without clearing it");
                first.Hurt(PlayerDeathReason.ByCustomReason("fixture second native healing injury"),Math.Max(1,first.statLife-first.statLifeMax2/4),0);Step(host,first,second);
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
        internal static void Planning(object host,Player first,Player second)
        {
            var a=Call(host,"Owned",first);var b=Call(host,"Owned",second);var human=Main.LocalPlayer;
            var originalA=((CompanionProfile)Get(a,"Profile")).Behavior;var originalB=((CompanionProfile)Get(b,"Profile")).Behavior;
            var brainA=Get(a,"Behavior");var brainB=Get(b,"Behavior");
            var positions=new[]{human.position,first.position,second.position};var velocities=new[]{human.velocity,first.velocity,second.velocity};
            var originalItems=new[]{first.inventory[0],first.inventory[54]};int selected=first.selectedItem;
            double surface=Main.worldSurface;
            var oldNpc=Main.npc[11];var tiles=new List<Tuple<int,int,Tile>>();var existing=new HashSet<int>(Main.projectile.Where(p=>p.active).Select(p=>(int)p.key));
            try
            {
                // Reuse the established real jump scene's complete vertical
                // premise: clear Y50..94, bank Y72, lower floor Y80, surface40.
                // Y60 is space gravity even after merely changing surface40.
                Main.worldSurface=40;
                for(int x=48;x<90;x++)for(int y=50;y<95;y++)
                {tiles.Add(Tuple.Create(x,y,Main.tile[x,y]));Main.tile[x,y]=new Tile();if(y>=80 || y>=72 && (x<66 || x>=70)){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}}
                foreach(bool firing in new[]{false,true})
                {
                    Call(host,"ClearWorldSources");foreach(var actor in new[]{a,b}){Set(actor,"Target",null);Set(actor,"AimTimeline",null);Set(actor,"AimTerrain",null);Set(actor,"AimPlayer",null);Set(actor,"HasAim",false);}
                    human.position=new Vector2(1220,1110);human.velocity=Vector2.Zero;
                    first.position=new Vector2(800,1110);second.position=new Vector2(1044,1110);first.velocity=second.velocity=Vector2.Zero;
                    first.inventory[0]=new Item(ItemID.VenusMagnum);first.inventory[54]=new Item(ItemID.MusketBall){stack=100};first.selectedItemState.Select(0);first.itemAnimation=first.itemTime=first.reuseDelay=0;first.releaseUseItem=true;
                    Require(first.HeldItem.autoReuse,"ordinary automatic fixture weapon supplies held native fire without random ammo conservation");
                    Set(a,"Behavior",Activator.CreateInstance(brainA.GetType(),true));Set(b,"Behavior",Activator.CreateInstance(brainB.GetType(),true));
                    Set(host,"planningTurn",0);Set(host,"planningStep",UInt32.MaxValue);
                    Call(host,"SetBehavior",((CompanionProfile)Get(a,"Profile")).Id,new CompanionBehavior{Follow=true,AttackEnabled=firing});
                    Call(host,"SetBehavior",((CompanionProfile)Get(b,"Profile")).Id,new CompanionBehavior{Follow=true,AttackEnabled=false});
                    // This native armor survives the automatic weapon's first
                    // real hit. A dead ordinary zombie correctly stops fire.
                    var target=new NPC{whoAmI=11};target.SetDefaults(NPCID.PossessedArmor);target.active=true;target.position=new Vector2(900,1152-target.height);Main.npc[11]=target;
                    var observed=(NPC[])GetOptional(host,"observedNpcs");if(observed!=null)observed[11].active=false;
                    NetMessage.SendData(23,-1,-1,null,11);
                    // Consume the real primary frame through the ordinary
                    // serializer/decoder callback already installed by Run.
                    NativeCompanionCoreNetworkChecks.ApplyLatestNpc(11);
                    object firstNav = Get(Get(a, "Behavior"), "Navigator"), secondNav = Get(Get(b, "Behavior"), "Navigator");
                    bool tookOff = false;
                    int fighting = 0;
                    int teamBefore = (int)Get(firstNav, "PlannerSlices") + (int)Get(secondNav, "PlannerSlices");
                    Require((long)Get(secondNav, "ActionId") == 0, "second role starts without a retained navigation action");
                    for (int step = 0; step < 120; step++)
                    {
                        NativeQuickItemChecks.BeginWorldStep();
                        human.Update(human.whoAmI); first.Update(first.whoAmI); second.Update(second.whoAmI);
                        StepProjectiles(); Call(host, "AdvanceNpcObservation"); Call(host, "Update", 0UL);
                        int team = (int)Get(firstNav, "PlannerSlices") + (int)Get(secondNav, "PlannerSlices");
                        Require(team - teamBefore <= 1, "the team receives at most one heavy navigation grant per native world step");
                        Call(host, "Update", 0UL);
                        Require((int)Get(firstNav, "PlannerSlices") + (int)Get(secondNav, "PlannerSlices") == team,
                            "a repeated outer update cannot issue another planning grant for the same step");
                        teamBefore = team;
                        tookOff |= second.velocity.Y < 0 && second.position.Y < 1110;
                        if (first.controlUseItem) fighting++;
                        Require(!(bool)Get(a,"Faulted") && !(bool)Get(b,"Faulted"),"both real native bodies run without swallowed planning/weapon failure");
                        if (tookOff && (fighting >= 4 || !firing)) break;
                    }
                    int secondSlices = (int)Get(secondNav, "PlannerSlices");
                    Console.WriteLine($"PLAN FAIR firstAttack={firing} secondSlices={secondSlices} teamSlices={teamBefore} takeoff={tookOff} firingUpdates={fighting}");
                    Require(secondSlices > 0 && tookOff,"uncached second role receives bounded planning and real native takeoff while first attack="+firing);
                    Require(!firing || fighting >= 4 && first.inventory[54].stack < 100 &&
                        Main.projectile.Any(p => p.active && p.owner == first.whoAmI),
                        "the automatic native attack remains held and pays actual ammunition during peer planning");
                }
            }
            finally
            {
                foreach(var tile in tiles)Main.tile[tile.Item1,tile.Item2]=tile.Item3;
                foreach(var p in Main.projectile)if(p.active && !existing.Contains((int)p.key) && (p.owner==first.whoAmI || p.owner==second.whoAmI))p.Kill();
                Main.npc[11]=oldNpc;Call(host,"ClearWorldSources");
                human.position=positions[0];first.position=positions[1];second.position=positions[2];human.velocity=velocities[0];first.velocity=velocities[1];second.velocity=velocities[2];
                first.inventory[0]=originalItems[0];first.inventory[54]=originalItems[1];first.selectedItemState.Select(selected);first.itemAnimation=first.itemTime=first.reuseDelay=0;first.channel=false;
                Main.worldSurface=surface;
                Call(host,"SetBehavior",((CompanionProfile)Get(a,"Profile")).Id,originalA);Call(host,"SetBehavior",((CompanionProfile)Get(b,"Profile")).Id,originalB);Set(a,"Behavior",brainA);Set(b,"Behavior",brainB);
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
