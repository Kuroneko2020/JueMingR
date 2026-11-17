using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    internal static class NativeCompanionCreationChecks
    {
        internal static void StoredMaxima(object host,string output)
        {
            Terraria.GameContent.Creative.CreativePowerManager.Initialize();
            var source=new Player {name="存档基础上限",whoAmI=255,statLife=451,statLifeMax=500,statLifeMax2=600,statMana=199,statManaMax=200,statManaMax2=400};
            source.savedPerPlayerFieldsThatArentInThePlayerClass=new Player.SavedPlayerDataWithAnnoyingRules();
            var profiles=Get(host,"profiles");var bytes=(byte[])Call(profiles,"Capture",source);
            string root=Path.Combine(Path.GetFullPath(output),"stored-maxima");
            using(var archive=new JueMingR.Infrastructure.Companions.FileCompanionArchive(root))
            {var owner=archive.LoadOwner("isolated-maxima").OwnerId;archive.Save(new CompanionProfile(Guid.NewGuid(),owner,source.name,1,1,new CompanionBehavior(),bytes));}
            using(var archive=new JueMingR.Infrastructure.Companions.FileCompanionArchive(root))
            {
                var saved=archive.LoadOwner("isolated-maxima").Profiles.Single();var loaded=(Player)Call(profiles,"LoadBody",saved);
                Require(loaded.statLife==451 && loaded.statMana==199 && loaded.statLifeMax2==500 && loaded.statManaMax2==200,"offline archive readback preserves current resources and uses saved 500/200 base limits without simulating dynamic gains");
            }
        }
        private static int voiceRequests;
        private static bool CountVoice(){voiceRequests++;return false;}
        internal static void VoicePreview(object host)
        {
            var audio=new Harmony("JueMingR.Tests.CompanionVoiceRequests");
            audio.Patch(AccessTools.Method(typeof(Player),"PlayHurtSound"),prefix:new HarmonyMethod(typeof(NativeCompanionCreationChecks),nameof(CountVoice)));
            try
            {
                uint tick=Main.GameUpdateCount;var draft=(Player)Call(host,"NewCreationDraft");voiceRequests=0;
                draft.voiceVariant=1;Call(host,"PreviewCreationVoice",draft);
                draft.voiceVariant=2;Call(host,"PreviewCreationVoice",draft);
                Require(voiceRequests==2 && Main.GameUpdateCount==tick,"two later explicit voice requests are accepted even while the world tick does not advance");
            }
            finally {audio.UnpatchAll(audio.Id);}
        }
        private static bool failTerminalCapture;
        private static void CaptureFailure(Player __0)
        {if(failTerminalCapture && __0.difficulty==2 && __0.dead){failTerminalCapture=false;throw new IOException("isolated final native capture failure");}}
        private static CompanionProfile Profile(object host,Guid id) { return ((IEnumerable)Get(host,"Profiles")).Cast<CompanionProfile>().Single(p=>p.Id==id); }
        private static void Saved(object host,Guid id)
        { var profiles=Get(host,"profiles");NativeQuickItemChecks.Until(()=>{Call(host,"Update",0UL);return (bool)Call(profiles,"IsSaved",id);}); }
        private static void Recalled(object host,Guid id)
        { Require((bool)Call(host,"Recall",id),"recall accepted");NativeQuickItemChecks.Until(()=>{Call(host,"Update",0UL);return !((IDictionary)Get(host,"actors")).Contains(id);}); }
        internal static void Run(object context,string output)
        {
            var outlets=new Harmony("JueMingR.Tests.CompanionCreationCosmeticOutlets");
            foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})outlets.Patch(AccessTools.Method(typeof(Terraria.GameContent.Achievements.AchievementsHelper),name),prefix:new HarmonyMethod(typeof(NativeCompanionCreationChecks),nameof(SkipCosmetic)));
            try { RunCore(context,output); } finally { outlets.UnpatchAll(outlets.Id); }
        }
        private static bool SkipCosmetic(){return false;}
        private static void RunCore(object context,string output)
        {
            NativeCombatAimChecks.Initialize();
            Main.spawnTileX=60;Main.spawnTileY=58;Main.leftWorld=Main.topWorld=0;Main.rightWorld=Main.maxTilesX*16;Main.bottomWorld=Main.maxTilesY*16;
            for(int x=2;x<118;x++){Main.tile[x,60].active(true);Main.tile[x,60].type=TileID.Stone;}
            new Harmony("JueMingR.Tests.QuickItemOutlets").Unpatch(AccessTools.Method(typeof(Player),"Spawn"),HarmonyPatchType.Prefix,"JueMingR.Tests.QuickItemOutlets");
            NativeCompanionCoreLifecycleChecks.PrepareHuman();
            var host=Get(context,"Companions");var profiles=Get(host,"profiles");
            NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");Call(host,"Update",0UL);return (bool)Get(profiles,"Ready");});
            var hairs=(IReadOnlyList<int>)Call(host,"GetCreationHairstyles");
            Require(hairs.Count>200 && hairs.Contains(227) && !hairs.Contains(133) && !hairs.Contains(145),"native creation hair list includes all creation styles and excludes stylist-only unlocks");
            int originalCount=((IEnumerable)Get(host,"Profiles")).Cast<object>().Count();
            var discarded=(Player)Call(host,"NewCreationDraft");discarded.name="未确认草稿";Call(host,"RandomizeCreation",discarded);
            Require(((IEnumerable)Get(host,"Profiles")).Cast<object>().Count()==originalCount && discarded.inventory.All(i=>i.IsAir),"draft edits/cancel do not publish a profile or issue resources");
            bool oldJourney=Main.GameMode==3;
            var ids=new Guid[4];
            for(byte difficulty=0;difficulty<4;difficulty++)
            {
                var draft=(Player)Call(host,"NewCreationDraft");draft.name="创建难度"+difficulty;draft.difficulty=difficulty;
                Call(host,"RandomizeCreation",draft);Require(draft.name=="创建难度"+difficulty && draft.difficulty==difficulty,"random appearance preserves name and chosen difficulty");
                draft.hair=227;draft.skinVariant=9;draft.voiceVariant=3;draft.voicePitchOffset=-.75f;
                draft.hairColor=new Color(10,20,30);draft.eyeColor=new Color(40,50,60);draft.skinColor=new Color(70,80,90);
                draft.shirtColor=new Color(100,110,120);draft.underShirtColor=new Color(130,140,150);draft.pantsColor=new Color(160,170,180);draft.shoeColor=new Color(190,200,210);
                Require((bool)Call(host,"CreateDraft",draft),"full native draft submits once: "+Get(host,"Feedback"));
                Require(!(bool)Call(host,"CreateDraft",draft),"duplicate confirmation cannot create a second character");
                NativeQuickItemChecks.Until(()=>{Call(host,"Update",0UL);return ((IEnumerable)Get(host,"Profiles")).Cast<CompanionProfile>().Any(p=>p.Name==draft.name);});
                ids[difficulty]=((IEnumerable)Get(host,"Profiles")).Cast<CompanionProfile>().Single(p=>p.Name==draft.name).Id;Saved(host,ids[difficulty]);
                Require((string)Get(host,"Feedback")!="正在保存新人物。","completed creation retires only its pending progress message");
                var body=(Player)Call(profiles,"LoadBody",Profile(host,ids[difficulty]));AssertAppearance(body,draft);
                Require(body.difficulty==difficulty && body.statLifeMax==100 && body.statManaMax==20 && body.inventory[0].type==(difficulty==3?6:3507),"native start resources and difficulty survive serialization");
                bool joined=(bool)Call(host,"Join",ids[difficulty]);Require(joined==((difficulty==3)==oldJourney),"Journey world compatibility honors chosen difficulty");
                if(joined)Recalled(host,ids[difficulty]);
                Require((bool)Call(host,"Reset",ids[difficulty]),"explicit destructive reset accepted");Saved(host,ids[difficulty]);
                body=(Player)Call(host,"GetBody",ids[difficulty]);AssertAppearance(body,draft);Require(body.difficulty==difficulty,"explicit reset preserves all four native difficulty values");
            }
            Require(!oldJourney,"terminal fixture uses a non-Journey world");
            string sentinel=Path.GetFullPath(Main.playerPathName);
            Require(sentinel.StartsWith(Path.GetFullPath(output).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"human sentinel remains under this invocation root");
            string backup=sentinel+".bak";
            File.WriteAllText(sentinel,"human-sentinel");File.WriteAllText(backup,"human-backup");
            var priorFile=Main.ActivePlayerFileData;bool oldStarve=Main.dontStarveWorld,oldAnniversary=Main.tenthAnniversaryWorld,oldGood=Main.getGoodWorld;
            var humanFile=new PlayerFileData(sentinel,false){Player=Main.LocalPlayer};
            try
            {
                Main.ActivePlayerFileData=humanFile;
                Require((bool)Call(host,"Join",ids[1]),"mediumcore joins");var medium=(Player)Call(host,"GetBody",ids[1]);
                medium.inventory[12].SetDefaults(ItemID.Wood);medium.inventory[12].stack=37;
                medium.KillMe(PlayerDeathReason.ByCustomReason("isolated mediumcore death"),99999,0);
                Require(medium.dead && medium.inventory.All(i=>i.type!=ItemID.Wood) && Main.item.Any(i=>i!=null && i.active && i.type==ItemID.Wood && i.stack==37),"real native mediumcore death drops owned inventory into world");
                for(int i=0;i<2400 && medium.dead;i++){NativeQuickItemChecks.BeginWorldStep();medium.Update(medium.whoAmI);}
                Require(!medium.dead && medium.difficulty==1 && medium.inventory.All(i=>i.type!=ItemID.Wood),"mediumcore legally respawns without reissuing lost inventory");Recalled(host,ids[1]);
                Main.dontStarveWorld=true;Main.tenthAnniversaryWorld=true;Main.getGoodWorld=false;Require(Main.surviveHardcoreDeath,"real special-world rule enables hardcore life-loss exception");
                Require((bool)Call(host,"Join",ids[2]),"hardcore joins before death: "+Get(host,"Feedback"));var hardcore=(Player)Call(host,"GetBody",ids[2]);
                hardcore.statLifeMax=100;hardcore.KillMe(PlayerDeathReason.ByCustomReason("isolated survivable hardcore death"),99999,0);
                Require(hardcore.dead && hardcore.statLifeMax==40 && !Profile(host,ids[2]).PermanentlyDead,"native surviveHardcoreDeath penalty retains a nonterminal lifecycle");
                for(int i=0;i<2400 && hardcore.dead;i++){NativeQuickItemChecks.BeginWorldStep();hardcore.Update(hardcore.whoAmI);}
                Require(!hardcore.dead && hardcore.statLifeMax==40,"native hardcore special-world exception legally revives");
                var captureFailure=new Harmony("JueMingR.Tests.CompanionTerminalCapture");
                captureFailure.Patch(AccessTools.Method(profiles.GetType(),"Capture"),prefix:new HarmonyMethod(typeof(NativeCompanionCreationChecks),nameof(CaptureFailure)));
                failTerminalCapture=true;
                try { hardcore.KillMe(PlayerDeathReason.ByCustomReason("isolated terminal hardcore death"),99999,0); }
                finally { failTerminalCapture=false;captureFailure.UnpatchAll(captureFailure.Id); }
                var closing=((IDictionary)Get(host,"actors"))[ids[2]];
                Require(closing!=null && (bool)Get(closing,"Closing") && !(bool)Get(closing,"CloseCaptured") && !hardcore.active && Profile(host,ids[2]).PermanentlyDead,"terminal capture failure detaches final body and keeps a retryable permanent archive identity");
                Require(!(bool)Call(host,"Join",ids[2]) && (bool)Call(host,"RetrySave",ids[2]),"terminal failed capture cannot rejoin and explicit retry captures the same final body");Saved(host,ids[2]);
                NativeQuickItemChecks.Until(()=>{Call(host,"Update",0UL);return !((IDictionary)Get(host,"actors")).Contains(ids[2]);});
                Require(Profile(host,ids[2]).PermanentlyDead && !(bool)Call(host,"Join",ids[2]),"actual terminal exit persists and blocks ordinary Join");
                Require(File.ReadAllText(sentinel)=="human-sentinel" && File.ReadAllText(backup)=="human-backup" && ReferenceEquals(Main.ActivePlayerFileData,humanFile),"AI terminal death leaves isolated human file, backup and active file identity untouched");
                Require((bool)Call(host,"RestoreBehavior",ids[2]),"behavior restoration remains available");Saved(host,ids[2]);Require(Profile(host,ids[2]).PermanentlyDead,"behavior restoration cannot revive a dead profile");
                var archive=Get(Get(profiles,"manager"),"archive");
                string game=Directory.GetParent(Directory.GetParent((string)Get(archive,"root")).FullName).FullName;
                string isolated=Path.Combine(Path.GetFullPath(output),"terminal-reader");
                string relative=Path.Combine("JueMingRData","companions","characters",Profile(host,ids[2]).OwnerId.ToString("N"),ids[2].ToString("N")+".bin");
                string ownerRelative=Path.Combine("JueMingRData","companions","owners");Directory.CreateDirectory(Path.Combine(isolated,Path.GetDirectoryName(relative)));
                File.Copy(Path.Combine(game,relative),Path.Combine(isolated,relative));Directory.CreateDirectory(Path.Combine(isolated,ownerRelative));
                foreach(string ownerFile in Directory.GetFiles(Path.Combine(game,ownerRelative),"*.bin"))File.Copy(ownerFile,Path.Combine(isolated,ownerRelative,Path.GetFileName(ownerFile)));
                using(var reader=new JueMingR.Infrastructure.Companions.FileCompanionArchive(isolated))
                {var read=reader.LoadOwner((string)Get(Get(profiles,"manager"),"ownerKey"));Require(read.Profiles.Single().PermanentlyDead,"fresh archive reader retains terminal state across re-entry");}
                Require((bool)Call(host,"Reset",ids[2]),"explicit reset creates a new body epoch");Saved(host,ids[2]);
                Require(!Profile(host,ids[2]).PermanentlyDead && ((Player)Call(host,"GetBody",ids[2])).difficulty==2,"explicit reset clears terminal state while preserving hardcore difficulty");
                // A non-owned player's original exit must still reach native file
                // deletion. Both sentinels are disposable invocation-owned paths.
                new Player().KillMeForGood();Require(!File.Exists(sentinel) && !File.Exists(backup) && !ReferenceEquals(Main.ActivePlayerFileData,humanFile),"non-owned native terminal path still deletes its own isolated files");
            }
            finally{Main.ActivePlayerFileData=priorFile;Main.dontStarveWorld=oldStarve;Main.tenthAnniversaryWorld=oldAnniversary;Main.getGoodWorld=oldGood;}
            Console.WriteLine("PASS creation: seven colors, complete hair/voice, four difficulty roundtrip/reset, mediumcore native drops/respawn, hardcore exception/terminal/archive/sentinel safety");
        }
        private static void AssertAppearance(Player actual,Player expected)
        { Require(actual.hair==expected.hair && actual.skinVariant==expected.skinVariant && actual.voiceVariant==expected.voiceVariant && actual.voicePitchOffset==expected.voicePitchOffset && actual.hairColor==expected.hairColor && actual.eyeColor==expected.eyeColor && actual.skinColor==expected.skinColor && actual.shirtColor==expected.shirtColor && actual.underShirtColor==expected.underShirtColor && actual.pantsColor==expected.pantsColor && actual.shoeColor==expected.shoeColor,"native stream preserves seven colors, hairstyle, clothing and voice"); }
    }
}
