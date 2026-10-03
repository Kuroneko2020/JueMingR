using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using HarmonyLib;
using Microsoft.Xna.Framework;

namespace NativeWorldTextProbe
{
    // This directory separates identity leads from executable scene evidence.
    // NewNPC proves birth, not every phase or environmental prerequisite. A
    // candidate is promoted only after an actual original-update comparison;
    // missing context stays visible and makes the complete run fail.
    internal static class NativeCombatLegalCoverageChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static readonly HashSet<int> Town=Set("17,18,19,20,22,37,38,54,107,108,124,142,160,178,207,208,209,227,228,229,353,368,369,441,453,550,588,633,637,638,656,663,670,678,679,680,681,682,683,684");
        private static readonly HashSet<int> Rescue=Set("105,106,123,354,376,579,589,685,695,696");
        private static readonly HashSet<int> Placeholder=Set("76,146,403,404,408,547,664");
        private static readonly HashSet<int> Worm=Set("7,8,9,10,11,12,13,14,15,39,40,41,87,88,89,90,91,92,95,96,97,98,99,100,117,118,119,134,135,136,375,402,412,413,414,454,455,456,457,458,459,510,511,512,513,514,515,621,622,623");
        private static readonly HashSet<int> Linked=Set("35,36,113,114,115,116,127,128,129,130,131,245,246,247,248,249,262,263,264,266,267,327,328,370,371,372,373,379,384,390,391,392,393,394,395,396,397,398,400,401,415,416,437,438,439,440,472,478,479,491,492,522,523,594");
        private static readonly HashSet<int> ProjectileNpc=Set("25,30,33,112,261,265,371,516,519,665,666");
        private static readonly HashSet<int> Rooted=Set("43,56,101,175,259,260");
        private static readonly HashSet<int> Boss=Set("4,35,50,113,125,126,127,134,222,245,262,266,370,395,396,397,398,439,636,657,664,668");
        private static readonly HashSet<int> Special=Set("70,72,422,493,507,517,488,686,687,690,547,548,549,551,552,553,554,555,556,557,558,559,560,561,562,563,564,565,566,567,568,569,570,571,572,573,574,575,576,577,578");
        internal static void Run(Assembly host,string layout,string output)
        {
            Lighting.Mode=Terraria.Graphics.Light.LightMode.Color;
            NativeCombatWorkerChecks.Scene(false);
            Terraria.Localization.LanguageManager.Instance.SetLanguage("en-US");Lang.InitializeLegacyLocalization();ContentSamples.Initialize();
            Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);
            TorchID.Initialize();
            if(Main.instance==null){Main.instance=(Main)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Main));GC.SuppressFinalize(Main.instance);}
            Main.instance.CameraModifiers=new Terraria.Graphics.CameraModifiers.CameraModifierStack();
            for(int i=0;i<Main.combatText.Length;i++)Main.combatText[i]=new CombatText();
            var outlets=new Harmony("JueMingR.Tests.LegalSceneOutlets");
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning"})outlets.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatLegalCoverageChecks).GetMethod(nameof(SkipPresentation),Flags)));
            foreach(var method in typeof(CombatText).GetMethods().Where(m=>m.Name=="NewText" && m.GetParameters()[2].ParameterType==typeof(string)))
                outlets.Patch(method,transpiler:new HarmonyMethod(host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeEffectBoundary",true).GetMethod("TextMetrics",Flags)));
            foreach(var method in typeof(WorldGen).GetMethods(Flags).Where(m=>m.Name=="BroadcastText"))outlets.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatLegalCoverageChecks).GetMethod(nameof(SkipPresentation),Flags)));
            outlets.Patch(typeof(Main).GetMethod("NotifyOfEvent",Flags),prefix:new HarmonyMethod(typeof(NativeCombatLegalCoverageChecks).GetMethod(nameof(SkipPresentation),Flags)));
            foreach(var method in typeof(Terraria.Net.NetManager).GetMethods(Flags).Where(m=>m.Name=="Broadcast" || m.Name=="SendToServer" || m.Name=="SendToClient"))
                outlets.Patch(method,prefix:new HarmonyMethod(host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeEffectBoundary",true).GetMethod("RecyclePacket",Flags)));
            string filter=Environment.GetEnvironmentVariable("JUEMINGR_NPC_TYPES");var subset=string.IsNullOrEmpty(filter)?null:Set(filter);
            bool variantsOnly=Environment.GetEnvironmentVariable("JUEMINGR_NPC_VARIANTS_ONLY")=="1";if(variantsOnly)subset=new HashSet<int>();
            int leads=0,passed=0,unresolved=0;
            using(var child=NativeCombatWorkerChecks.Start(layout))
            using(var csv=new StreamWriter(Path.Combine(output,"legal-npc-coverage.csv")))
            {
                var errors=child.StandardError.ReadToEndAsync();
                csv.WriteLine("requestedNetId,type,netId,scene,selectable,strategy,outcome,evidence");
                try
                {
                    for(int id=-65;id<NPCID.Count;id++)
                    {
                        if(id==0 || subset!=null && !subset.Contains(id))continue;leads++;
                        int type=NPCID.FromNetId(id);string plan=Plan(type),outcome="NeedsContext",evidence="",strategy="unexecuted";bool selectable=false;NPC n=null;
                        try
                        {
                            if(plan=="ReachabilityRequired")
                            {
                                outcome=type==664?"NonWorldBestiaryIdentity":type==547?"ReservedDebugIdentity":"ReservedNativeIdentity";passed++;
                                evidence=type==664?"Player.cs:37823/45617 registers a local bestiary NPC; TorchGod attacks are Projectile949":type==547?"NPCID.DD2AttackerTest; hidden bestiary NPCID.cs9464; actual DD2 event births use 551..578":"NPCID None2/None3 or unused Stardust parts; no native defaults; hidden bestiary; 407 actually creates Projectile539";
                            }
                            else if(plan=="IndependentCandidate" || plan=="Catchable" || plan=="FriendlyTown" || plan=="FriendlyRescue" || HasContext(type,host))
                            {
                                bool context=HasContext(type,host);int slot=context?SpawnContext(type,host,out plan):Spawn(id,out plan);n=Main.npc[slot];
                                Require(n.active && n.netID==id && n.type==type,"Original birth preserves requested netID and resolved type.");
                                // These friendly identities are selection
                                // counterexamples, not motion coverage. Do not
                                // pretend a tiny arena is a valid town layout.
                                int warm=context?0:Town.Contains(type) || Rescue.Contains(type)?0:Main.npcCatchable[type]?100:3;
                                for(int i=0;i<warm;i++)NativeCombatSegmentedPredictionChecks.Advance();
                                selectable=(bool)host.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true).GetMethod("Receives",Flags).Invoke(null,new object[]{n,true});
                                if(Town.Contains(type) || Rescue.Contains(type) || new[]{70,72,249,263,328,392,400,412,413,437,491,548,549}.Contains(type))
                                {Require(n.active && !selectable,"Live friendly actor remains outside combat selection.");outcome="ExcludedBySelectionContract";evidence="native birth + current Receives, no motion claim";passed++;}
                                else
                                {
                                    Require(n.active && selectable,"Scene must reach a live eligible target, not default invulnerability or premature loss.");
                                    typeof(Main).GetField("_gameUpdateCount",Flags).SetValue(null,1000U);
                                    if(NativeCombatSegmentedPredictionChecks.Family(host,type)!=0)
                                    {Trend(host,output,slot,id);strategy="SegmentedTrend";outcome="Approximate120WithOriginalErrors";evidence="legal-"+id+"-trend.csv";}
                                    else
                                    {
                                        if(type>=551 && type<=578)Console.WriteLine("EFFECT input type="+type+" dust-active="+Main.dust.Count(d=>d.active)+" first-free="+Array.FindIndex(Main.dust,d=>!d.active)+" dCount="+Dust.dCount+" gore-active="+Main.gore.Count(g=>g.active)+" text-active="+Main.combatText.Count(c=>c.active));
                                        if(Environment.GetEnvironmentVariable("JUEMINGR_NPC_CLEAR_EFFECT_DIAGNOSTIC")=="1")
                                        {
                                            Console.WriteLine("DIAGNOSTIC controlled empty effect pools; not original-scene coverage");
                                            foreach(var dust in Main.dust)dust.active=false;foreach(var gore in Main.gore)gore.active=false;foreach(var text in Main.combatText)text.active=false;Dust.dCount=0;
                                        }
                                        Rectangle? region=Main.maxTilesX>512?(Rectangle?)new Rectangle(245,0,510,120):Main.maxTilesX==384 && Main.maxTilesY==400?new Rectangle(0,96,384,304):(Rectangle?)null;
                                        var frozen=NativeCombatWorkerChecks.AcquireFrozen(host,child,Enumerable.Range(0,Main.maxNPCs).Where(i=>Main.npc[i].active).ToArray(),Enumerable.Range(0,Main.maxProjectiles).Where(i=>Main.projectile[i].active).ToArray(),slot,region,NativeCombatEventBirthChecks.Handles(type));
                                        File.WriteAllBytes(Path.Combine(output,"legal-"+id+"-snapshot.bin"),frozen.Snapshot);File.WriteAllBytes(Path.Combine(output,"legal-"+id+"-future.bin"),frozen.Future);
                                        bool numeric=Environment.GetEnvironmentVariable("JUEMINGR_NPC_NUMERIC_TRACE")=="1";
                                        string trace=Path.Combine(output,"numeric-"+id);if(numeric){Directory.CreateDirectory(trace);byte[] snapshot=(byte[])frozen.Snapshot.Clone();Buffer.BlockCopy(BitConverter.GetBytes(120),0,snapshot,12,4);File.WriteAllBytes(Path.Combine(trace,"difference-one-step.bin"),snapshot);}
                                        NativeCombatWorkerChecks.Compare(frozen.Future,slot,output,"legal-"+id,dependencyComparison:CompareMotion,motionTolerance:.002f,nativeStep:(step,actor)=>{if(numeric && step==120)File.WriteAllLines(Path.Combine(trace,"difference-original.txt"),NativeCombatPrivateImageChecks.Dump(actor,Main.LocalPlayer));});
                                        strategy="NativeIsolated";outcome=n.active?"Frozen120NativeMatch":"OriginalNaturalEndMatch";evidence="legal-"+id+"-oracle.csv; motion <=0.002px, identities/life/AI/stages exact";
                                    }
                                    passed++;
                                }
                            }
                            else{unresolved++;evidence="explicit scene/phase or reachability evidence required";}
                        }
                        catch(Exception error)
                        {
                            if(error is OutOfMemoryException || child.HasExited)throw;
                            unresolved++;outcome="Unresolved";evidence=(error is TargetInvocationException?error.InnerException:error).ToString().Replace("\r"," ").Replace("\n"," ");
                        }
                        csv.WriteLine(string.Join(",",id,type,n==null?id:n.netID,plan,selectable,strategy,outcome,"\""+evidence.Replace("\"","\"\"")+"\""));csv.Flush();
                        if(leads%25==0)Console.WriteLine("LEGAL leads="+leads+" passed="+passed+" unresolved="+unresolved);
                    }
                    if(variantsOnly || subset==null)NativeCombatEventBirthChecks.VerifyVariants(host,child,output);
                }
                finally{outlets.UnpatchAll(outlets.Id);NativeCombatWorkerChecks.Exit(child,"legal scene helper EOF");File.WriteAllText(Path.Combine(output,"legal-worker.log"),errors.Result);}
            }
            Require(subset!=null || leads==761,"All positive and negative identity leads are accounted for.");
            Console.WriteLine("LEGAL classified="+leads+" passed="+passed+" unresolved="+unresolved+"; classification is not all-phase coverage");
            Require(unresolved==0,"Legal scene coverage has unresolved identities; see CSV, never convert these to a default-state PASS.");
        }
        private static string Plan(int type)
        {
            if(Placeholder.Contains(type))return "ReachabilityRequired";
            if(Town.Contains(type))return "FriendlyTown";if(Rescue.Contains(type))return "FriendlyRescue";
            if(Main.npcCatchable[type])return "Catchable";
            if(Worm.Contains(type))return "WormBirth";if(Rooted.Contains(type))return "RootedPlant";
            if(Linked.Contains(type))return "LinkedBirthOrStage";if(ProjectileNpc.Contains(type))return "ProjectileBirth";
            if(Special.Contains(type))return "EventOrTileContext";if(Boss.Contains(type))return "BossStages";
            return "IndependentCandidate";
        }
        private static int Spawn(int id,out string scene)
        {
            NativeCombatWorkerChecks.Scene(false);NPC.ClearAll();Projectile.ClearAll();Main.getGoodWorld=false;Main.remixWorld=false;NPC.brainOfGravity=Main.wofNPCIndex=-1;
            Main.LocalPlayer.immune=true;Main.LocalPlayer.immuneTime=100000;
            Main.pumpkinMoon=Main.snowMoon=false;Main.invasionType=0;
            Terraria.GameContent.Events.DD2Event.ResetProgressEntirely();Terraria.GameContent.Events.Sandstorm.Happening=false;Main.PlayerSceneMetrics.Reset();
            for(int i=0;i<Main.LocalPlayer.hurtCooldowns.Length;i++)Main.LocalPlayer.hurtCooldowns[i]=100000;
            // A finite actual tile environment, not a fake collision provider.
            // Aquatic movement needs water at birth; all other independent
            // candidates begin above a real floor with native target setup.
            var defaults=new NPC();defaults.SetDefaults(id);bool water=defaults.aiStyle==16 || defaults.aiStyle==18 || defaults.aiStyle==97 || defaults.aiStyle==103;
            if(water)
            {
                for(int x=20;x<70;x++)for(int y=35;y<65;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(0);}
                // The independent oracle holds this player on dry ground.
                // Placing it inside the pool with stale wet=false would make
                // the first private player step a different premise.
                Main.LocalPlayer.position.X=1200;
            }
            scene=water?"NativeBirthWater":"NativeBirthGroundAir";
            return NPC.NewNPC(new EntitySource_DebugCommand(),550,water || defaults.noGravity?800:65*16,id,Start:1);
        }
        private static bool HasContext(int type,Assembly host)
        {return NativeCombatEventBirthChecks.Handles(type) || NativeCombatSegmentedPredictionChecks.Family(host,type)!=0 || Rooted.Contains(type) || type>=548 && type<=578 && type!=550 || new[]{4,25,30,33,35,36,50,70,72,112,113,114,115,116,125,126,127,128,129,130,131,222,245,246,247,248,249,261,262,263,264,265,266,267,327,328,375,383,384,387,390,391,392,393,394,395,396,397,398,400,401,402,412,413,414,415,416,472,477,478,479,488,491,492,516,546,594,636,657,661,665,668,686,687,690}.Contains(type);}
        internal static int SpawnContext(int type,Assembly host,out string scene)
        {
            if(NativeCombatEventBirthChecks.Handles(type))return NativeCombatEventBirthChecks.Birth(type,host,out scene);
            if(type>=548 && type<=578 && type!=550)return NativeCombatDd2CoverageChecks.Birth(type,host,out scene);
            if(NativeCombatSegmentedPredictionChecks.Family(host,type)!=0)
            {
                int family=NativeCombatSegmentedPredictionChecks.Family(host,type),head=new[]{7,10,13,39,87,95,98,117,134,454,510,513,621}.First(t=>NativeCombatSegmentedPredictionChecks.Family(host,t)==family);
                int[] chain=NativeCombatSegmentedPredictionChecks.Birth(host,head);int selected=chain.First(i=>Main.npc[i].type==type);
                for(int i=0;i<5 || Main.npc[selected].dontTakeDamage && i<400;i++)NativeCombatSegmentedPredictionChecks.Advance();
                scene="OriginalLinkedBirth-"+head;return selected;
            }
            string unused;Spawn(1,out unused);NPC.ClearAll();Projectile.ClearAll();
            if(type>=113 && type<=116)
            {
                Main.maxTilesX=384;Main.maxTilesY=400;Main.rightWorld=6144;Main.bottomWorld=6400;Main.tile=new Tile[384,400];
                for(int x=0;x<384;x++)for(int y=0;y<400;y++){var tile=new Tile();Main.tile[x,y]=tile;if(y>=300){tile.active(true);tile.type=57;}}
                Main.LocalPlayer.position=new Vector2(3500,4800-Main.LocalPlayer.height);Main.wofNPCIndex=-1;
                NPC.SpawnWOF(new Vector2(3300,4650));AdvanceScene(3);
                Require(Slots(113).Any() && Slots(114).Count()==2 && Slots(115).Any(),"Native hell summon creates wall, eyes and attached Hungry.");
                if(type==116){Kill(host,Find(115));AdvanceScene(1);}
                scene="NativeSpawnWOF-hell"+(type==116?"-hungry-released":"");return Find(type);
            }
            if(type==546 || type==661)
            {
                Main.maxTilesX=1000;Main.rightWorld=16000;Main.tile=new Tile[1000,120];
                for(int x=0;x<1000;x++)for(int y=0;y<120;y++){Main.tile[x,y]=new Tile();if(y>=65){Main.tile[x,y].active(true);Main.tile[x,y].type=(ushort)(type==546?53:117);}}
                Main.LocalPlayer.position=new Vector2(8000,1040-Main.LocalPlayer.height);
                if(type==546){Main.worldSurface=80;Main.rockLayer=100;Main.hardMode=true;Terraria.GameContent.Events.Sandstorm.Happening=true;Terraria.GameContent.Events.Sandstorm.TimeLeft=50000;Main.windSpeedCurrent=.8f;}
                else NPC.downedPlantBoss=true;
                Main.LocalPlayer.UpdateSceneMetrics();Main.LocalPlayer.UpdateBiomes();
                Require(type==546?Main.LocalPlayer.ZoneDesert && Main.LocalPlayer.ZoneSandstorm:Main.LocalPlayer.ZoneHallow,"Native scene scan establishes the required biome.");
                int special=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),8050,type==661?1000:850,type,Start:1);
                AdvanceScene(type==661?100:3);scene=type==546?"NativeInlandSandstorm":"NativeHallowNight";return special;
            }
            Main.maxTilesY=400;Main.bottomWorld=6400;Main.tile=new Tile[Main.maxTilesX,Main.maxTilesY];
            for(int x=0;x<Main.maxTilesX;x++)for(int y=0;y<Main.maxTilesY;y++){Main.tile[x,y]=new Tile();if(y>=65){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}}
            Main.hardMode=false;
            int rootType=type;
            if(type==25)rootType=24;if(type==30)rootType=29;if(type==33)rootType=32;if(type==665)rootType=45;if(type==112)rootType=94;
            if(type==261)rootType=260;
            if(type==265)rootType=262;
            if(type==472){rootType=471;Main.hardMode=true;Main.invasionType=1;}
            if(type==413 || type==414)rootType=412;
            if(type==478 || type==479)rootType=477;
            if(type==36)rootType=35;
            if(type>=127 && type<=131)rootType=127;
            if(type>=245 && type<=249)rootType=245;
            if(type>=262 && type<=264)rootType=262;
            if(type==266 || type==267)rootType=266;
            if(type==384)rootType=383;
            if(type==328)rootType=327;
            if(type==391)rootType=390;
            if(type==415)rootType=416;
            if(type==516)rootType=416;
            if(type==375)rootType=374;
            if(type>=392 && type<=395){rootType=395;Main.GameMode=1;}
            if(type>=396 && type<=401)rootType=398;
            if(type==492)rootType=491;
            if(rootType==477){Main.dayTime=true;Main.eclipse=true;NPC.downedPlantBoss=true;}
            if(rootType==327)Main.pumpkinMoon=true;
            if(rootType==262 || rootType==266 || rootType==657 || rootType==668 || rootType==245)
            {
                ushort biome=(ushort)(rootType==266?203:rootType==657?117:rootType==668?147:60);
                for(int x=0;x<Main.maxTilesX;x++)for(int y=65;y<130;y++)Main.tile[x,y].type=biome;
                if(rootType==245){Main.tile[(int)Main.LocalPlayer.Center.X/16,(int)Main.LocalPlayer.Center.Y/16].wall=87;Main.hardMode=true;}
                Main.PlayerSceneMetrics.Reset();Main.LocalPlayer.UpdateSceneMetrics();Main.LocalPlayer.UpdateBiomes();
                Require(rootType==266?Main.LocalPlayer.ZoneCrimson:rootType==657?Main.LocalPlayer.ZoneHallow:rootType==668?Main.LocalPlayer.ZoneSnow:Main.LocalPlayer.ZoneJungle,"Original biome scan for boss arena.");
            }
            if(Rooted.Contains(rootType))
            {
                int x=34,y=rootType==259?50:90;ushort tile=(ushort)(rootType==101?23:rootType==259 || rootType==260?70:60);
                Main.tile[x,y].active(true);Main.tile[x,y].type=tile;Main.hardMode=rootType==101 || rootType==175 || rootType==260;
                if(type==261){for(int tx=20;tx<80;tx++)for(int ty=66;ty<y;ty++)Main.tile[tx,ty].active(false);Main.LocalPlayer.position=new Vector2(800,y*16-Main.LocalPlayer.height);}
                int plant=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),x*16+8,y*16,rootType,ai0:x,ai1:y,Target:0);
                AdvanceScene(3);scene="NativeRootTile-"+tile;
                if(type==261){for(int i=0;i<1500 && !Slots(261).Any();i++)AdvanceScene(1);return Find(261);}return plant;
            }
            if(type==488)
            {
                // Actual placement/TE activation owns the high-slot birth.
                TileEntity.Clear();TileEntity.InitializeAll();
                const int x=34,y=62;for(int dx=0;dx<2;dx++)for(int dy=0;dy<3;dy++)
                {var tile=Main.tile[x+dx,y+dy];tile.active(true);tile.type=378;tile.frameX=(short)(dx*18);tile.frameY=(short)(dy*18);}
                int key=Terraria.GameContent.Tile_Entities.TETrainingDummy.Hook_AfterPlacement(x+1,y+2);
                var dummy=(Terraria.GameContent.Tile_Entities.TETrainingDummy)TileEntity.ByID[key];Terraria.GameContent.Tile_Entities.TETrainingDummy.ClearBoxes();dummy.Update();AdvanceScene(3);
                scene="OriginalTrainingDummyPlacement";Require(dummy.npc>=100,"Original dummy reserves high NPC slots.");return dummy.npc;
            }
            int root=NPC.NewNPC(new EntitySource_DebugCommand(),rootType==398?960:rootType==690?700:550,rootType==398?900:1040,rootType,Start:1,Target:rootType==245?0:255);
            scene="NativeParentBirth-"+rootType;
            if(type==690)Main.LocalPlayer.position=new Vector2(740,65*16-Main.LocalPlayer.height);
            if(type==375)Main.LocalPlayer.position=new Vector2(650,65*16-Main.LocalPlayer.height);
            AdvanceScene(rootType==398?65:type==636?181:type==387?121:3);
            if(type==375){for(int i=0;i<300 && Main.npc[root].type!=375;i++)AdvanceScene(1);Require(Main.npc[root].type==375,"Natural truffle emergence.");}
            if(type==245 || type==249){Kill(host,Find(246));AdvanceScene(3);scene+="-head-defeated";}
            if(type==264 || type==265){while(Main.npc[root].life>Main.npc[root].lifeMax/2)Hit(host,root,Main.npc[root].lifeMax/10);AdvanceScene(3);scene+="-half-life";}
            if(type==266){foreach(int slot in Slots(267))Kill(host,slot);AdvanceScene(3);scene+="-creepers-defeated";}
            if(type==383){Kill(host,Find(384));AdvanceScene(2);scene+="-forcefield-defeated";}
            if(type==395){foreach(int slot in Slots(393).Concat(Slots(394)))Kill(host,slot);AdvanceScene(3);scene+="-weapons-defeated-expert";}
            if(type==398 || type==400)
            {
                for(int i=0;i<2200 && Main.npc[root].dontTakeDamage;i++)
                {foreach(int slot in Slots(396).Concat(Slots(397)))if(Receives(host,Main.npc[slot]))Kill(host,slot);AdvanceScene(1);}
                Require(!Main.npc[root].dontTakeDamage,"Original eye breaks open Moon Lord core.");scene+="-eyes-defeated";
            }
            if(type==401)
            {for(int i=0;i<1500 && !Slots(401).Any();i++)AdvanceScene(1);Require(Slots(401).Any(),"Original tongue projectile and debuff create leech blob.");scene+="-tongue-leech";}
            if(new[]{25,30,33,112,265,472,478,479,516,665}.Contains(type))
            {for(int i=0;i<6000 && !Slots(type).Any();i++)AdvanceScene(1);Require(Slots(type).Any(),"Original parent AI produces requested role "+type);scene+="-actual-child";}
            int target=Find(type);
            if(type==396 || type==397)for(int i=0;i<1500 && !Receives(host,Main.npc[target]);i++)AdvanceScene(1);
            return target;
        }
        private static IEnumerable<int> Slots(int type){return Enumerable.Range(0,Main.maxNPCs).Where(i=>Main.npc[i].active && Main.npc[i].type==type).ToArray();}
        private static int Find(int type){return Slots(type).First();}
        internal static bool Receives(Assembly host,NPC n){return (bool)host.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true).GetMethod("Receives",Flags).Invoke(null,new object[]{n,true});}
        internal static void Hit(Assembly host,int slot,int damage){Require(Receives(host,Main.npc[slot]),"Only a currently hittable actor may receive fixture damage.");Main.npc[slot].StrikeNPCNoInteraction(damage,0,0);}
        private static void Kill(Assembly host,int slot){Hit(host,slot,1000000);}
        internal static void AdvanceScene(int ticks)
        {
            for(int t=0;t<ticks;t++)
            {
                NativeCombatSegmentedPredictionChecks.Advance();
                try{for(int i=0;i<Main.maxProjectiles;i++){Main.ProjectileUpdateLoopIndex=i;if(Main.projectile[i].active)Main.projectile[i].Update(i);}}finally{Main.ProjectileUpdateLoopIndex=-1;}
            }
        }
        private static void Trend(Assembly host,string output,int slot,int requested)
        {
            object source=NativeCombatSegmentedPredictionChecks.Create(host,output);var cache=(JueMingR.Features.Combat.NpcPredictionCache)source.GetType().GetField("Cache",Flags).GetValue(source);cache.Demand(0,120);
            try
            {
                for(int i=0;i<3;i++){NativeCombatSegmentedPredictionChecks.Advance();source.GetType().GetMethod("Prepare",Flags).Invoke(source,new object[]{NativeCombatSegmentedPredictionChecks.Identity(Main.npc[slot]),(long)Main.GameUpdateCount});}
                var path=cache.Read(0);Require(path!=null && path.Count==121,"Every eligible linked role has an actual current +120 path.");
                using(var csv=new StreamWriter(Path.Combine(output,"legal-"+requested+"-trend.csv")))
                {
                    csv.WriteLine("tick,errorPx,alive");for(int i=0;i<=120;i++)
                    {if(i>0)NativeCombatSegmentedPredictionChecks.Advance();var actual=Main.npc[slot];float error=Vector2.Distance(new Vector2(path[i].Bounds.CenterX,path[i].Bounds.CenterY),actual.Center);Require(!float.IsNaN(error) && !float.IsInfinity(error),"Finite approximate errors.");csv.WriteLine(i+","+error.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+actual.active);}
                }
            }
            finally{source.GetType().GetMethod("Stop",Flags).Invoke(source,null);}
        }
        private static bool SkipPresentation(){return false;}
        // The instrumented x86 image can round a Vector2 intermediate slightly
        // differently (observed Golem fist / Moon Lord projectile / ghost).
        // Bound the ENTIRE frozen future, including every dependency. This is
        // not a phase/life/RNG exception, and never compares only the next tick.
        internal static void CompareMotion(byte[] actual,byte[] predicted)
        {
            float maximum=0;
            using(var a=new BinaryReader(new MemoryStream(actual)))using(var b=new BinaryReader(new MemoryStream(predicted)))
            {
                for(int tick=0;tick<=120;tick++)
                {
                    Require(a.ReadInt32()==b.ReadInt32(),"Dependency tick identity.");
                    for(int kind=0;kind<2;kind++)
                    {
                        int count=a.ReadInt32();Require(count==b.ReadInt32(),"Dependency count.");
                        for(int i=0;i<count;i++)
                        {
                            Require(a.ReadInt32()==b.ReadInt32(),"Dependency slot.");
                            if(kind==0){Require(a.ReadByte()==b.ReadByte(),"Dependency generation.");Require(a.ReadInt32()==b.ReadInt32(),"Dependency type.");Require(a.ReadInt32()==b.ReadInt32(),"Dependency netID.");}
                            else{Require(a.ReadUInt32()==b.ReadUInt32(),"Dependency projectile key.");Require(a.ReadInt32()==b.ReadInt32(),"Dependency projectile type.");}
                            for(int component=0;component<4;component++){float error=Math.Abs(a.ReadSingle()-b.ReadSingle());Require(error<=.002f,"Dependency motion bound at "+tick);maximum=Math.Max(maximum,error);}
                            Require(a.ReadInt32()==b.ReadInt32(),"Dependency life/expiry.");
                            for(int ai=0;ai<(kind==0?4:3);ai++)Require(a.ReadSingle()==b.ReadSingle(),"Dependency AI phase at "+tick);
                        }
                    }
                }
                Require(a.BaseStream.Position==actual.Length && b.BaseStream.Position==predicted.Length,"Complete dependency timelines.");
            }
            Console.WriteLine("DEPENDENCY full-future motion-component-max="+maximum.ToString("R"));
        }
        private static HashSet<int> Set(string value){return new HashSet<int>(value.Split(',').Select(int.Parse));}
        private static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    }
}
