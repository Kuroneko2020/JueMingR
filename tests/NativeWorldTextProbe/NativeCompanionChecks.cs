using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionChecks
    {
        internal static void Run(object context)
        {
            // Normal native world entry registers the creative power IDs.
            // The headless fixture bypasses that entry; initialize its real
            // registry before writing the complete native character stream.
            Terraria.GameContent.Creative.CreativePowerManager.Initialize();
            var host=Get(context,"Companions");Require(host!=null,"normal full composition owns companion feature");
            Require((bool)Get(host,"Enabled"),"native companion hooks installed: "+Get(host,"Feedback"));
            var profiles=Get(host,"profiles");
            NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");Call(host,"Update",0UL);return (bool)Get(profiles,"Ready");});
            Require((bool)Call(host,"Create","同行甲"),"create first real native profile");
            Require((bool)Call(host,"Create","同行乙"),"create second real native profile");
            NativeQuickItemChecks.Until(()=>{Call(host,"Update",0UL);return ((IEnumerable)Get(host,"Profiles")).Cast<CompanionProfile>().Count()==2 && ((IEnumerable)Get(host,"Profiles")).Cast<CompanionProfile>().All(p=>(bool)Call(profiles,"IsSaved",p.Id));});
            var list=((IEnumerable)Get(host,"Profiles")).Cast<CompanionProfile>().ToArray();
            var first=(Player)Call(host,"GetBody",list[0].Id);var second=(Player)Call(host,"GetBody",list[1].Id);
            if(first==null || second==null){Console.WriteLine("COMPANION LOAD ERROR "+Get(host,"Feedback"));Call(profiles,"LoadBody",list[0]);throw new InvalidDataException("Native companion load returned no body");}
            Require(first.inventory[0].type==3507 && second.inventory[0].type==3507,"first native starter exists independently");
            Require(!ReferenceEquals(first.inventory[0],second.inventory[0]),"profiles own distinct native item objects");
            first.inventory[12].SetDefaults(ItemID.Wood);first.inventory[12].stack=200;
            long generation;var reserve=profiles.GetType().GetMethod("TryReserve",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            object[] args={list[0],0L};Require((bool)reserve.Invoke(profiles,args),"activate unique character");generation=(long)args[1];
            args=new object[]{list[0],0L};Require(!(bool)reserve.Invoke(profiles,args),"same character cannot duplicate live resources");
            first.inventory[12].stack-=50;Call(profiles,"CaptureAndSave",list[0].Id,first,generation);
            NativeQuickItemChecks.Until(()=>{Call(profiles,"Pump");return (bool)Call(profiles,"IsSaved",list[0].Id);});
            Call(profiles,"Release",list[0],generation);
            var revised=((IEnumerable)Get(host,"Profiles")).Cast<CompanionProfile>().First(p=>p.Id==list[0].Id);
            var restored=(Player)Call(profiles,"LoadBody",revised);Require(restored.inventory[12].stack==150,"native complete archive retains actual 200 to 150 consumption");
            Require(restored.inventory[0].type==3507 && restored.bank.item.Length==40,"full native character load preserves starter and bank shape");
            var shell=Get(context,"Shell");var renderer=Get(shell,"renderer");Call(renderer,"RefreshResources");Call(renderer,"Prepare",Get(shell,"State"),960f,760f,1f);
            NativeCompanionUiChecks.Run(shell);
            var tiles=new Tile[160,120];for(int x=0;x<160;x++)for(int y=0;y<120;y++)tiles[x,y]=x<Main.maxTilesX?Main.tile[x,y]:new Tile();Main.maxTilesX=160;Main.tile=tiles;Main.Map=new Terraria.Map.WorldMap(160,120);
            NativeCompanionBehaviorChecks.Run(host);
            NativeCompanionLifecycleChecks.FailedLoad(host,list[0].Id);
            var independentFailures=new List<Exception>();
            NativeBody(context,host,list[1].Id,list[0].Id,independentFailures);
            NativeCompanionTransportChecks.Run();
            NativeCompanionPersistenceChecks.RunWriter(host,list[0].Id,Environment.GetCommandLineArgs()[2],Program.OutputDirectory);
            if(independentFailures.Count!=0)throw independentFailures[0];
            Console.WriteLine("PASS companions: production composition/serialization/full native archive/activation isolation/actual resource continuity/UI layout/transport framing. No game, Steam or FPS acceptance.");
        }
        private static void NativeBody(object context,object host,Guid id,Guid other,List<Exception> independentFailures)
        {
            NativeCombatAimChecks.Initialize();
            for(int i=0;i<Main.item.Length;i++)Main.item[i].whoAmI=i;
            Main.spawnTileX=60;Main.spawnTileY=58;Main.leftWorld=Main.topWorld=0;Main.rightWorld=Main.maxTilesX*16;Main.bottomWorld=Main.maxTilesY*16;
            for(int x=2;x<118;x++){Main.tile[x,60].active(true);Main.tile[x,60].type=TileID.Stone;}
            var isolation=new Harmony("JueMingR.Tests.CompanionBodyOutlets");
            new Harmony("JueMingR.Tests.QuickItemOutlets").Unpatch(AccessTools.Method(typeof(Player),"Spawn"),HarmonyPatchType.Prefix,"JueMingR.Tests.QuickItemOutlets");
            foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})isolation.Patch(AccessTools.Method(typeof(Terraria.GameContent.Achievements.AchievementsHelper),name),prefix:new HarmonyMethod(typeof(NativeCompanionChecks),nameof(SkipCosmetic)));
            try
            {
                Require((bool)Call(host,"Join",id),"actual native Spawn joins saved character: "+Get(host,"Feedback"));
                var p=(Player)Call(host,"GetBody",id);Require(p.active && p.whoAmI!=Main.myPlayer,"independent active native body");
                NativeCompanionTileContextChecks.Run(host,p);
                try{NativeCompanionBehaviorChecks.RunJumpReachability(host);}
                catch(Exception failure){Console.Error.WriteLine("FAIL companion jump (other restored native checks continue): "+failure);independentFailures.Add(failure);}
                var human=Main.LocalPlayer;human.position=new Vector2(1300,900);var start=p.position;int realSlot=Main.myPlayer;var mouse=Main.mouseItem;int x=Main.mouseX,y=Main.mouseY;
                for(int frame=0;frame<35;frame++)
                {NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",(ulong)frame);p.Update(p.whoAmI);}
                Require(p.position.X>start.X && Main.myPlayer==realSlot && ReferenceEquals(mouse,Main.mouseItem) && Main.mouseX==x && Main.mouseY==y,"native companion follows without borrowing human identity/mouse beyond consumers: "+Call(host,"Status",id));
                Main.editSign=true;Main.editChest=true;NativeQuickItemChecks.BeginWorldStep();p.Update(p.whoAmI);Require(Main.editSign && Main.editChest,"companion native body cannot close human sign/chest editing");Main.editSign=Main.editChest=false;
                var before=p.position;p.Update(p.whoAmI);Require(p.position==before,"same-step duplicate native update cannot advance body twice");
                p.inventory[0].SetDefaults(39);p.inventory[54].SetDefaults(40);p.inventory[54].stack=20;p.selectedItemState.Select(0);
                var target=Main.npc[0];target.SetDefaults(NPCID.Zombie);target.whoAmI=0;target.position=p.position+new Vector2(180,0);target.active=true;target.life=target.lifeMax=1000;
                for(int frame=0;frame<65;frame++){NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",(ulong)frame);p.Update(p.whoAmI);}
                Require(p.inventory[54].stack<20 && Main.projectile.Any(q=>q.active && q.owner==p.whoAmI && q.type==1),"real native bow consumes its own ammo and creates its own projectile");
                try{NativeCompanionPredictionChecks.Run(host,p,other);}
                catch(Exception failure){Console.Error.WriteLine("FAIL companion network prediction (restored independent lifecycle/resource checks continue): "+failure);independentFailures.Add(failure);}
                target.active=false;p.immune=false;p.immuneTime=0;Array.Clear(p.hurtCooldowns,0,p.hurtCooldowns.Length);
                var enemy=Main.projectile[999];enemy.SetDefaults(1);enemy.active=true;enemy.friendly=false;enemy.hostile=true;enemy.damage=20;enemy.owner=255;enemy.position=p.position;enemy.velocity=Vector2.Zero;
                int life=p.statLife;enemy.Damage();Require(p.statLife<life && p.immuneTime>0,"real native enemy projectile Damage_EVP harms companion with native immunity");enemy.active=false;
                var actor=Call(host,"Owned",p);
                p.dead=true;p.deadTime=100;p.respawnTimer=300;p.pvpDeath=true;
                Call(host,"AfterBody",actor);Require(p.dead && p.respawnTimer==300,"native prohibited respawn skip retains timer");
                p.pvpDeath=false;Require(p.AllowsRespawnTimerSkip(),"fixture reaches native legal skip conditions");
                Call(host,"AfterBody",actor);Require(p.dead && p.respawnTimer==1,"legal skip requests native one-step timer");
                Call(p,"UpdateDead");Call(host,"AfterBody",actor);
                Require(!p.dead && p.statLife>0,"next real native UpdateDead reaches zero and revives instead of resetting one forever");
                try{NativeCompanionContinuousChecks.Run(host,p,id);}
                catch(Exception failure){Console.Error.WriteLine("FAIL companion continuous weapon (restored resource/lifecycle checks continue): "+failure);independentFailures.Add(failure);}
                NativeCompanionUiChecks.Equipment(Get(context,"Shell"),id);
                NativeCompanionLifecycleChecks.FailedSaveExit(host,p,id,other);
                Require(human.active && Main.myPlayer==realSlot,"recall closes only companion");
            }
            finally{foreach(var m in isolation.GetPatchedMethods().ToArray())isolation.Unpatch(m,HarmonyPatchType.All,isolation.Id);}
        }
        private static bool SkipCosmetic(){return false;}
    }
}
