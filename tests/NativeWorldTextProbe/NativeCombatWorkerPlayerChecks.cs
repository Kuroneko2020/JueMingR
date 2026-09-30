using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Utilities;
using HarmonyLib;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatWorkerPlayerChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static string externalAttempt;
        internal static void Run(Assembly host,string layout,string output)
        {
            // Reuse only the existing headless native initialization, never its
            // product Composition Root or input/action patches.
            typeof(NativeQuickItemChecks).GetMethod("Initialize",Flags).Invoke(null,null);
            Terraria.ObjectData.TileObjectData.Initialize();Terraria.GameContent.Creative.CreativePowerManager.Initialize();
            Terraria.DataStructures.ArmorSetBonuses.Initialize();Terraria.DataStructures.ArmorSetBonuses.BuildLookup();
            Lighting.Mode=Terraria.Graphics.Light.LightMode.Color;
            if(Main.instance==null){Main.instance=(Main)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Main));GC.SuppressFinalize(Main.instance);}
            for(int i=1;i<Main.player.Length;i++)if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};
            for(int i=0;i<Main.item.Length;i++)Main.item[i].whoAmI=i;
            PopupText.popupText=new PopupText[20];for(int i=0;i<20;i++)PopupText.popupText[i]=new PopupText();
            NativeCombatWorkerAssetChecks.Initialize();
            Main.dedServ=false;Main.dayTime=false;Main.worldSurface=60;Main.rockLayer=90;
            Main.leftWorld=Main.topWorld=0;Main.rightWorld=Main.bottomWorld=1920;
            Main.tileSolid[1]=true;
            for(int x=0;x<120;x++)for(int y=70;y<120;y++){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}
            for(int x=50;x<53;x++)for(int y=55;y<70;y++){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}
            var field=typeof(Main).GetField("_rngs",Flags);object prior=field.GetValue(null);
            field.SetValue(null,new Dictionary<string,UnifiedRandom>{{"UpdatePlayers",new UnifiedRandom(531)},{"UpdateNPCs",new UnifiedRandom(879)}});
            var display=new Harmony("JueMingR.Tests.NativePlayerAchievementSink");
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning"})
                display.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatWorkerPlayerChecks).GetMethod("SkipAchievement",Flags)));
            externalAttempt=null;
            // Fence before the original death path can queue a background save.
            // A bad oracle scene must fail without touching a file or socket.
            foreach(var method in new[]{typeof(WorldGen).GetMethod("saveToonWhilePlaying",Flags),typeof(Player).GetMethod("SavePlayer",Flags),typeof(NetMessage).GetMethod("SendData",Flags)})
                display.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatWorkerPlayerChecks).GetMethod("RefuseExternal",Flags)));
            display.Patch(typeof(Item).GetMethod("GetDrawHitbox",Flags),prefix:new HarmonyMethod(typeof(NativeCombatWorkerPlayerChecks).GetMethod("EmptyItemMetrics",Flags)));
            try
            {
                string[] scenes=Environment.GetEnvironmentVariable("JUEMINGR_NPC_PLAYER_SCENES")?.Split(',')??new[]{"wall","jump","fall","reverse","boots","frog","frog-cycle","two-jumps","left-border","forced-expiry","forced-source-retired","swift-long","swift-expiry","swift-expired","swift-boots-long","swift-boots-expiry","swift-boots-expired","swift-frog-long","swift-frog-expiry","swift-frog-expired","swift-boots-prefix-expiry","food26-long","food26-expiry","food26-expired","food206-expiry","food207-expiry","mixed-expiry","mixed-reverse-expiry"};
                using(var child=NativeCombatWorkerChecks.Start(layout))
                {
                var errors=child.StandardError.ReadToEndAsync();
                try
                {
                foreach(string scene in scenes)
                {
                for(int slot=0;slot<Main.npc.Length;slot++)Main.npc[slot]=new NPC();
                // A low ceiling makes the held-jump scenario complete a real
                // landing/rejump cycle within the required 120-update window.
                for(int x=0;x<120;x++){Main.tile[x,63].type=1;Main.tile[x,63].active(scene=="frog-cycle" || x>=50 && x<53);}
                Main.player[1]=new Player{whoAmI=1};Main.worldSurface=scene=="frog-cycle"?20:60;
                Main.player[0]=new Player{whoAmI=0,active=true};var player=Main.player[0];
                player.position=new Vector2(scene=="reverse"?750:640,70*16-player.height-(scene=="fall"?180:0));
                player.velocity=scene=="reverse"?new Vector2(-3,0):Vector2.Zero;
                player.fallStart=player.fallStart2=(int)(player.position.Y/16);
                player.statLife=player.statLifeMax=player.statLifeMax2=400;player.immune=true;player.immuneTime=10000;player.isControlledByFilm=true;player.releaseJump=true;
                NPC.brainOfGravity=-1;
                if(scene=="forced-expiry"){player.position=new Vector2(700,630);player.forcedGravity=2;player.gravDir=-1;player.fallStart=player.fallStart2=39;}
                if(scene=="boots")player.armor[3].SetDefaults(Terraria.ID.ItemID.HermesBoots);
                if(scene.StartsWith("swift",StringComparison.Ordinal))
                {
                    player.AddBuff(3,scene.EndsWith("long",StringComparison.Ordinal)?600:scene.EndsWith("expired",StringComparison.Ordinal)?1:3);
                    if(scene.Contains("boots"))player.armor[3].SetDefaults(54);
                    if(scene.Contains("frog"))player.armor[3].SetDefaults(2423);
                    if(scene.Contains("prefix"))player.armor[3].prefix=76;
                }
                if(scene.StartsWith("food",StringComparison.Ordinal))player.AddBuff(int.Parse(scene.Substring(4,scene.IndexOf('-')-4)),scene.EndsWith("long",StringComparison.Ordinal)?600:scene.EndsWith("expired",StringComparison.Ordinal)?1:3);
                if(scene.StartsWith("mixed",StringComparison.Ordinal))
                {
                    int[] types=scene.Contains("reverse")?new[]{26,3,2}:new[]{2,3,26};
                    for(int b=0;b<types.Length;b++){player.buffType[b]=types[b];player.buffTime[b]=3;}
                }
                if(scene.StartsWith("frog",StringComparison.Ordinal))player.armor[3].SetDefaults(Terraria.ID.ItemID.FrogLeg);
                if(scene=="two-jumps")
                {var second=Main.player[1];second.active=true;second.position=new Vector2(850,1078);second.fallStart=second.fallStart2=67;second.statLife=second.statLifeMax=second.statLifeMax2=400;second.immune=true;second.immuneTime=10000;second.isControlledByFilm=true;second.releaseJump=true;second.armor[3].SetDefaults(Terraria.ID.ItemID.FrogLeg);}
                bool jumping=scene=="jump" || scene=="two-jumps" || scene.Contains("frog");
                Action advance=()=>AdvancePlayer(jumping,scene=="left-border");advance();
                if(scene=="forced-expiry")RequireGravity(player,1,-1,"Original pre-capture timer");
                if(jumping && (player.velocity.Y>=0 || player.jump<=0))throw new InvalidOperationException("Jump oracle must have actually started jumping before capture.");
                if(scene.StartsWith("frog",StringComparison.Ordinal) && (player.jumpSpeedBoost<=0 || !player.autoJump))throw new InvalidOperationException("Frog accessory premise missing.");
                if(scene=="boots" && player.accRunSpeed<=player.maxRunSpeed)throw new InvalidOperationException("Running accessory premise missing.");
                if(scene=="two-jumps" && Main.player[1].jumpSpeedBoost<=player.jumpSpeedBoost)throw new InvalidOperationException("Two-player jump premises must differ.");
                var npc=Main.npc[0];npc.SetDefaults(2);npc.whoAmI=0;npc.active=true;npc.target=0;npc.position=new Vector2(650,600);npc.timeLeft=750;
                if(scene=="forced-source-retired")
                {
                    // A brain can become inactive after the world's selector
                    // check. The next player phase still refreshes once, then
                    // the original NPC phase retires the stale selector.
                    Main.npc[1].SetDefaults(266);Main.npc[1].whoAmI=1;Main.npc[1].active=false;Main.npc[1].position=new Vector2(700,650);
                    NPC.brainOfGravity=1;player.position=new Vector2(700,630);player.forcedGravity=1;player.gravDir=-1;player.fallStart=player.fallStart2=39;
                }
                typeof(Main).GetField("_gameUpdateCount",Flags).SetValue(null,1000U);
                var capture=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true).GetMethod("Capture",Flags);
                byte[] snapshot=(byte[])capture.Invoke(null,new object[]{scene=="forced-source-retired"?new[]{0,1}:new[]{0},0,1000L,120}),future;
                future=NativeCombatWorkerChecks.Exchange(child,snapshot);
                float start=player.position.X;int repeatedJumps=0;
                using(var log=new StreamWriter(Path.Combine(output,"moving-player-"+scene+"-oracle.csv")))
                {
                    log.WriteLine("tick,x,y,vx,vy");int step=0;
                    NativeCombatWorkerChecks.Compare(future,0,output,"eye-moving-player-"+scene,nativeStreams:true,playerUpdate:()=>
                    {float oldVelocity=player.velocity.Y;advance();if(scene=="forced-expiry" && step==0){RequireGravity(player,0,1,"Original timer expires");if(player.position.Y!=640)throw new InvalidOperationException("Ordinary top border must clamp after forced gravity expires.");}if(oldVelocity==0 && player.velocity.Y<0)repeatedJumps++;log.WriteLine((++step)+","+player.position.X+","+player.position.Y+","+player.velocity.X+","+player.velocity.Y);});
                }
                if(scene=="left-border"){if(player.position.X!=640 || player.velocity.X!=0)throw new InvalidOperationException("Original world edge must remain stationary.");}
                else if(!jumping && !scene.StartsWith("forced-",StringComparison.Ordinal) && (player.position.X<=start+(scene=="reverse"?10:100) || player.position.X+player.width>800.01f || player.velocity.X!=0))throw new InvalidOperationException("Player oracle must walk and stop against the wall.");
                if(scene=="forced-source-retired"){RequireGravity(player,0,1,"Inactive gravity source must eventually expire");if(NPC.brainOfGravity!=-1)throw new InvalidOperationException("Original NPC phase must retire its selector.");}
                if(scene=="frog-cycle" && repeatedJumps==0)throw new InvalidOperationException("Held autojump oracle must land and jump again without a new input edge.");
                }
                }
                finally{NativeCombatWorkerChecks.Exit(child,"moving-player helper exits");File.WriteAllText(Path.Combine(output,"moving-player-worker.log"),errors.Result);}
                }
            }
            finally{display.UnpatchAll(display.Id);field.SetValue(null,prior);}
            Console.WriteLine("PASS selected fixed-input player scenarios; native NPC states exact and player components within 0.002px.");
        }
        private static void RequireGravity(Player player,int timer,float direction,string label){if(player.forcedGravity!=timer || player.gravDir!=direction)throw new InvalidOperationException(label);}
        private static void AdvancePlayer(bool jump,bool left=false)
        {
            using(Main.SwapRandom("UpdatePlayers"))
                for(int i=0;i<2;i++)if(Main.player[i].active){Main.player[i].controlRight=!left;Main.player[i].controlLeft=left;Main.player[i].controlJump=jump;Main.player[i].Update(i);}
            if(externalAttempt!=null)throw new InvalidOperationException("Invalid player oracle attempted "+externalAttempt);
        }
        // No motion, state returned to Player.Update, or RNG in these terminal
        // achievement notifications; the independent physics stays unpatched.
        private static bool SkipAchievement(){return false;}
        private static bool EmptyItemMetrics(int type,ref Rectangle __result)
        {if(type!=0)throw new InvalidOperationException("Player movement oracle must have empty hands.");__result=new Rectangle(0,0,1,1);return false;}
        private static bool RefuseExternal(MethodBase __originalMethod)
        {externalAttempt=__originalMethod.DeclaringType.Name+"."+__originalMethod.Name;throw new InvalidOperationException("Forbidden player oracle external operation: "+externalAttempt);}
    }
}
