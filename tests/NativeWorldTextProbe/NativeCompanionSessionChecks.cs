using System;
using System.Collections;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.IO;
using JueMingR.Platform.Companions;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionSessionChecks
    {
        private static object borrowedProbe,borrowedToken;
        private static Player dying;
        private static bool insideBorrow;
        private static int reads;
        private static void ReadDuringDeath(Player __instance)
        {
            if(!ReferenceEquals(__instance,dying))return;
            insideBorrow=ReferenceEquals(Main.LocalPlayer,__instance);reads++;
            borrowedToken=Get(borrowedProbe,"SessionIdentity");
        }
        private static bool SkipCosmetic(){return false;}
        private static CompanionProfile Profile(object host,Guid id)
        {return ((IEnumerable)Get(host,"Profiles")).Cast<CompanionProfile>().Single(p=>p.Id==id);}
        private static void Saved(object host,Guid id)
        {NativeQuickItemChecks.Until(()=>{Call(host,"Update",0UL);return (bool)Call(Get(host,"profiles"),"IsSaved",id);});}
        private static void Recall(object host,Guid id)
        {Require((bool)Call(host,"Recall",id),"session fixture recall accepted");NativeQuickItemChecks.Until(()=>{Call(host,"Update",0UL);return !(bool)Call(host,"IsJoined",id);});}
        private static void Step(object context,Player a,Player b)
        {
            NativeQuickItemChecks.BeginWorldStep();Call(context,"UpdateRuntime");a.Update(a.whoAmI);b.Update(b.whoAmI);Call(context,"UpdateRuntime");
        }
        internal static void Run(object context)
        {Scenario(context,null);}
        internal static void Journey(object context,ProbeGraphics graphics)
        {
            // Reuse the original Journey UI startup, not a forged Enabled flag
            // or a skipped Open callback. This entry owns real GPU resources.
            NativeJourneyUiChecks.PrepareNativeMenu(graphics);
            var access=Get(context,"hiddenAccess");
            for(int i=0;i<5 && !(bool)Get(access,"IsUnlocked");i++)Call(access,"TryCompleteTitleClick",(long)(100*i),(long)(1000+i));
            var journey=Get(context,"Journey");
            // The production activation deliberately reports a bounded player
            // message after catching native errors. Observe original exceptions
            // only during this one game-thread fixture call, without replacing
            // its catch, admission or result; always detach the temporary sink.
            int thread=System.Threading.Thread.CurrentThread.ManagedThreadId;
            EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> trace=(sender,args)=>
            {if(System.Threading.Thread.CurrentThread.ManagedThreadId==thread)Console.Error.WriteLine("COMPANION-JOURNEY activation native exception: "+args.Exception);};
            bool enabled;
            AppDomain.CurrentDomain.FirstChanceException+=trace;
            try {enabled=(bool)Call(journey,"SetEnabled",true);}
            finally {AppDomain.CurrentDomain.FirstChanceException-=trace;}
            Require(enabled,"real Journey activates before AI death: "+Get(journey,"LastFailure"));
            try {Scenario(context,journey);}finally{Call(journey,"SetEnabled",false);}
        }
        private static void Scenario(object context,object journey)
        {
            NativeCombatAimChecks.Initialize();
            new Harmony("JueMingR.Tests.QuickItemOutlets").Unpatch(AccessTools.Method(typeof(Player),"Spawn"),HarmonyPatchType.Prefix,"JueMingR.Tests.QuickItemOutlets");
            Main.spawnTileX=60;Main.spawnTileY=58;Main.leftWorld=Main.topWorld=0;Main.rightWorld=Main.maxTilesX*16;Main.bottomWorld=Main.maxTilesY*16;
            for(int x=2;x<118;x++){Main.tile[x,60].active(true);Main.tile[x,60].type=TileID.Stone;}
            var host=Get(context,"Companions");var profiles=Get(host,"profiles");
            var human=Main.ActivePlayerFileData.Player;var world=Main.ActiveWorldFileData;int humanTeam=human.team;
            var isolation=new Harmony("JueMingR.Tests.CompanionPublicSession");
            foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})isolation.Patch(AccessTools.Method(typeof(Terraria.GameContent.Achievements.AchievementsHelper),name),prefix:new HarmonyMethod(typeof(NativeCompanionSessionChecks),nameof(SkipCosmetic)));
            // This proof exercises real human Hurt/death and session edges. It
            // does not initialize or claim the unrelated human map/file writer.
            isolation.Patch(AccessTools.Method(typeof(WorldGen),"saveToonWhilePlaying"),prefix:new HarmonyMethod(typeof(NativeCompanionSessionChecks),nameof(SkipCosmetic)));
            isolation.Patch(AccessTools.Method(typeof(Player),"KillMe"),prefix:new HarmonyMethod(typeof(NativeCompanionSessionChecks),nameof(ReadDuringDeath)){priority=Priority.Last});
            Guid first=Guid.Empty,second=Guid.Empty;
            try
            {
                NativeCompanionCoreLifecycleChecks.PrepareHuman();
                NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return (bool)Get(profiles,"Ready");});
                string prefix=journey==null?"公共时序":"旅行时序";
                Require((bool)Call(host,"Create",prefix+"甲") && (bool)Call(host,"Create",prefix+"乙"),"two independent real profiles created");
                NativeQuickItemChecks.Until(()=>{Call(host,"Update",0UL);var published=((IEnumerable)Get(host,"Profiles")).Cast<CompanionProfile>().ToArray();return published.Any(p=>p.Name==prefix+"甲") && published.Any(p=>p.Name==prefix+"乙");});
                first=((IEnumerable)Get(host,"Profiles")).Cast<CompanionProfile>().Single(p=>p.Name==prefix+"甲").Id;
                second=((IEnumerable)Get(host,"Profiles")).Cast<CompanionProfile>().Single(p=>p.Name==prefix+"乙").Id;Saved(host,first);Saved(host,second);
                Require((bool)Call(host,"SetTeam",first,2),"offline team modifies latest native archive without joining");Saved(host,first);
                Require((bool)Call(host,"Join",first) && (bool)Call(host,"Join",second),"both real actors join");
                var a=(Player)Call(host,"GetBody",first);var b=(Player)Call(host,"GetBody",second);
                Require(a.team==2 && human.team==humanTeam,"saved native team survives offline edit and join without changing human");
                foreach(Guid id in new[]{first,second}){var behavior=Profile(host,id).Behavior;behavior.Follow=false;behavior.AttackEnabled=false;Call(host,"SetBehavior",id,behavior);}
                a.inventory[12].SetDefaults(ItemID.WoodenBow);a.inventory[12].Prefix(PrefixID.Demonic);
                Require((bool)Call(host,"SelectWeapon",first,12),"explicit manual native weapon selection accepted");Saved(host,first);
                var preference=Profile(host,first).Behavior;preference.SelfDefense=true;Call(host,"SetBehavior",first,preference);
                Require(Profile(host,first).Behavior.ManualWeaponSlot==12 && Profile(host,first).Behavior.AutoRespawn,"cloned ordinary preference edit preserves manual weapon and respawn default");
                borrowedProbe=Get(Get(context,"DeathRecords"),"probe");var token=Get(borrowedProbe,"SessionIdentity");
                var runtime=Get(Get(context,"runtime"),"SharedRuntime");var generation=Get(runtime,"Generation");
                dying=a;reads=0;borrowedToken=null;insideBorrow=false;
                a.immune=false;a.immuneTime=0;Array.Clear(a.hurtCooldowns,0,a.hurtCooldowns.Length);
                a.Hurt(PlayerDeathReason.ByCustomReason("isolated AI Hurt public runtime"),99999,0);
                Require(a.dead && reads==1 && insideBorrow && ReferenceEquals(token,borrowedToken),"actual Hurt to KillMe reads stable human token inside AI identity scope");
                Call(context,"UpdateRuntime");
                Require(ReferenceEquals(token,Get(borrowedProbe,"SessionIdentity")) && Equals(generation,Get(runtime,"Generation")) && Call(host,"Owned",b)!=null && ReferenceEquals(Main.ActivePlayerFileData.Player,human),"public runtime after AI death preserves human generation and other actor");
                if(journey!=null)Require((bool)Get(journey,"IsActive"),"AI death/public update does not stop the already active real Journey session");
                int frames=0;for(;frames<2400 && a.dead;frames++)Step(context,a,b);
                Require(!a.dead && a.selectedItem==12 && a.inventory[12].type==ItemID.WoodenBow,"original dead timer and automatic spawn restore valid manual choice without new items");
                Recall(host,first);Require((bool)Call(host,"Join",first),"manual choice saved and body rejoined");a=(Player)Call(host,"GetBody",first);
                Require(a.selectedItem==12 && Profile(host,first).Behavior.ManualWeaponType==ItemID.WoodenBow && a.team==2,"new native body restores manual preference and saved native team");
                var off=Profile(host,first).Behavior;off.AutoRespawn=false;Call(host,"SetBehavior",first,off);
                dying=a;a.immune=false;a.immuneTime=0;Array.Clear(a.hurtCooldowns,0,a.hurtCooldowns.Length);a.Hurt(PlayerDeathReason.ByCustomReason("isolated disabled auto respawn"),99999,0);
                int timer=a.respawnTimer;for(int i=0;i<timer+2;i++)Step(context,a,b);
                Require(a.dead && a.respawnTimer==0 && (string)Call(host,"RespawnStatus",first)=="自动复活已关闭","disabled auto respawn advances native dead state to zero without artificial spawn");
                Saved(host,first);Recall(host,first);Require((bool)Call(host,"Join",first),"dead auto-off body can rejoin its actual archive");a=(Player)Call(host,"GetBody",first);
                Require(a.dead && !Profile(host,first).Behavior.AutoRespawn && a.inventory[12].type==ItemID.WoodenBow,"recall/rejoin preserves real death, preference and resource ownership");
                var on=Profile(host,first).Behavior;on.AutoRespawn=true;Call(host,"SetBehavior",first,on);Step(context,a,b);
                Require(!a.dead && a.selectedItem==12,"re-enabling continues the existing zero timer and valid manual selection");
                a.inventory[12].SetDefaults(ItemID.DirtBlock);Call(host,"ValidateManualWeapon",first);
                Require(Profile(host,first).Behavior.ManualWeaponSlot==-1,"same-slot different item retires manual preference rather than impersonating selected weapon");
                var beforeHuman=Get(borrowedProbe,"SessionIdentity");human.immune=false;human.immuneTime=0;Array.Clear(human.hurtCooldowns,0,human.hurtCooldowns.Length);
                human.Hurt(PlayerDeathReason.ByCustomReason("isolated actual human death"),99999,0);Call(context,"UpdateRuntime");
                Require(human.dead && ReferenceEquals(beforeHuman,Get(borrowedProbe,"SessionIdentity")) && Equals(generation,Get(runtime,"Generation")),"actual classic human death alone is not a new character session");
                Main.ActiveWorldFileData=new WorldFileData(System.IO.Path.Combine(Terraria.Program.SavePath,"real-world-change.wld"),false){UniqueId=Guid.NewGuid(),WorldId=8221,CreationTime=DateTime.UtcNow};
                Call(context,"UpdateRuntime");
                Require(!ReferenceEquals(beforeHuman,Get(borrowedProbe,"SessionIdentity")) && !Equals(generation,Get(runtime,"Generation")) && Call(host,"Owned",b)==null,"actual world identity edge still ends old actors and creates a new generation");
                if(journey!=null)Require(!(bool)Get(journey,"IsActive"),"true world change still ends old Journey ownership");
                Console.WriteLine("PASS native Hurt/KillMe -> public UpdateRuntime -> respawn, human identity/world counterexamples, auto-off/re-enable and saved manual/native team; JourneyActive="+(journey!=null)+" nativeFrames="+frames+"; human disk writer intentionally excluded, no installed-game/Steam/FPS claim.");
            }
            finally
            {
                borrowedProbe=null;dying=null;Main.ActiveWorldFileData=world;
                Call(host,"OnSessionEnded");isolation.UnpatchAll(isolation.Id);
            }
        }
    }
}
