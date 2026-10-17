using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatHarpyArrowFamilyChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static NpcTrajectory Path(NPC n)
        {
            var key=new NpcIdentity(1,n,n.whoAmI,n.generation,n.type,n.netID);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,Active=true,NoTileCollide=n.noTileCollide};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(key,Main.GameUpdateCount,1,PredictionAssumption.None,PredictionStop.None,points,points.Length);
        }
        internal static void Run(object context)
        {
            var host=Get(context,"Combat").GetType().Assembly;var model=host.GetType("JueMingR.TerrariaHost.Combat.HostHarpyAttack",true);int positive=0,negative=0;
            var audit=new Harmony("JueMingR.Tests.HarpyArrowFamily");foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            try
            {
            foreach(int type in new[]{91,117,639})foreach(bool lower in new[]{false,true})foreach(bool blocked in new[]{false,true})
            {
                var p=NativeCombatBallisticCandidateChecks.Scene(context,type==117?682:39,50);p.inventory[54].SetDefaults(type==91?516:type==117?40:3568);p.inventory[54].stack=999;
                typeof(NativeCombatHarpyChecks).GetMethod("Equip",Flags).Invoke(null,new object[]{p,6166});p.accHarpyCharmCooldown=0;Main.windPhysics=false;foreach(var npc in Main.npc)npc.active=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();for(int x=40;x<45;x++)NativeToolsChecks.Tile(x,43,1);
                var n=Main.npc[2];n.SetDefaults(3);n.active=true;n.whoAmI=2;n.width=1000;n.height=40;n.Center=new Vector2(1000,1120);n.life=n.lifeMax=100000;n.defense=0;n.knockBackResist=0;n.noTileCollide=true;n.noGravity=true;n.aiStyle=-1;n.dontTakeDamage=blocked;var path=Path(n);
                if(lower)for(int index=0;index<7;index++){var q=Main.projectile[index];q.SetDefaults(1);q.owner=254;q.whoAmI=index;q.active=true;q.damage=0;q.Center=new Vector2(100,100);q.tileCollide=false;}
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(1200,1000),new Vector2(8,6),type,60,0,p.whoAmI);var parent=Main.projectile[slot];parent.timeLeft=1;if(lower)for(int index=0;index<5;index++)Main.projectile[index].active=false;
                var terrain=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");var phase=Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");var clock=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,phase},null);AttackContact contact;
                using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))contact=(AttackContact)model.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{p,new[]{parent},path,clock,terrain});
                int first=0;bool child=false,charmed=false;for(int tick=1;tick<=120;tick++)
                {
                    NativeQuickItemChecks.BeginWorldStep();typeof(Player).GetMethod("Update_HarpyCharm",Flags).Invoke(p,null);if(parent.active)charmed|=new Projectile.AI_001_Features(parent.ai[2]).HarpyCharmed;if(n.immune[p.whoAmI]>0)n.immune[p.whoAmI]--;
                    for(int index=0;index<Main.maxProjectiles;index++)if(Main.projectile[index].active){Main.ProjectileUpdateLoopIndex=index;try{Main.projectile[index].Update(index);}finally{Main.ProjectileUpdateLoopIndex=-1;}}child|=Main.projectile.Any(q=>q.active && q.owner==p.whoAmI && (q.type==92 || q.type==640 || q.type==1124));if(n.life<100000){first=tick;break;}
                }
                Require(child,"Harpy original expiring parent creates its full natural arrow family type="+type);
                Require(blocked?contact==null && first==0:contact!=null && first>0 && charmed,"Harpy full derived family uses real charm/receive with an invulnerable negative type="+type+" lower="+lower+" actual="+first+" plan="+(contact?.Tick??-1));
                if(!blocked && type==639)Require(first==contact.Tick,"Harpy deterministic Night birth delay/old origin and exact native first Damage agree");
                if(blocked)negative++;else positive++;Console.WriteLine("HARPY-ARROW type="+type+" lower="+lower+" blocked="+blocked+" actual="+first+" plan="+(contact?.Tick??-1)+" charmed="+charmed);
            }
            Console.WriteLine("HARPY-ARROW true-families="+positive+" invulnerable-negatives="+negative);Hellfire(context,host,model);
            }
            finally{audit.UnpatchAll(audit.Id);}
        }
        private static void Hellfire(object context,Assembly host,Type model)
        {
            int cases=0;
            foreach(bool direct in new[]{false,true})foreach(bool blocked in new[]{false,true})
            {
                var p=NativeCombatBallisticCandidateChecks.Scene(context,39,50);p.inventory[54].SetDefaults(265);p.inventory[54].stack=999;
                typeof(NativeCombatHarpyChecks).GetMethod("Equip",Flags).Invoke(null,new object[]{p,6166});p.accHarpyCharmCooldown=0;Main.windPhysics=false;foreach(var npc in Main.npc)npc.active=false;
                for(int x=5;x<115;x++)for(int y=5;y<78;y++)Main.tile[x,y].ClearEverything();
                typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{2,(byte)0});var n=Main.npc[2];n.SetDefaults(3);n.active=true;n.whoAmI=2;n.width=n.height=20;n.Center=new Vector2(1200,1030);n.life=n.lifeMax=100000;n.defense=0;n.knockBackResist=0;n.noGravity=n.noTileCollide=true;n.aiStyle=-1;n.dontTakeDamage=blocked;
                if(direct){typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{1,(byte)0});var first=Main.npc[1];first.SetDefaults(3);first.active=true;first.whoAmI=1;first.width=first.height=8;first.Center=new Vector2(1200,1010);first.life=first.lifeMax=100000;first.defense=0;first.noGravity=first.noTileCollide=true;first.aiStyle=-1;}
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(1200,1000),new Vector2(8,6),41,60,0,p.whoAmI);var parent=Main.projectile[slot];parent.timeLeft=direct?60:1;
                var terrain=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");var path=Path(n);var phase=Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");var clock=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,phase},null);AttackContact contact;
                using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))contact=(AttackContact)model.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{p,new[]{parent},path,clock,terrain});
                NativeQuickItemChecks.BeginWorldStep();typeof(Player).GetMethod("Update_HarpyCharm",Flags).Invoke(p,null);bool charmed=new Projectile.AI_001_Features(parent.ai[2]).HarpyCharmed;parent.Update(slot);
                Require(blocked?contact==null && n.life==100000:contact!=null && contact.Tick==1 && n.life<100000 && charmed,"Harpy Hellfire finite64 Kill burst reaches a receiver beyond its direct body with the original shared history; direct="+direct+" blocked="+blocked+" contact="+(contact?.Tick??-1)+" native="+(100000-n.life));
                if(direct)Require(Main.npc[1].life<100000,"the earlier receiver really consumed the direct Hellfire strike before its finite burst; immune="+Main.npc[1].immune[p.whoAmI]+" flags="+Main.npc[1].active+"/"+Main.npc[1].friendly+"/"+Main.npc[1].dontTakeDamage+"/"+Main.npc[1].immortal+" center="+Main.npc[1].Center+" parent="+parent.Center+"/"+parent.velocity);
                Console.WriteLine("HARPY-AMMO41 direct="+direct+" blocked="+blocked+" plan="+(contact?.Tick??-1)+" native="+(100000-n.life)+" charmed="+charmed);cases++;
            }
            Console.WriteLine("HARPY-AMMO41 cases="+cases);
        }    }
}
