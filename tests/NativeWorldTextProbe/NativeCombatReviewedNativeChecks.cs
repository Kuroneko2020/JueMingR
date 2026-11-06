using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.Graphics.Renderers;
using Terraria.Utilities;

namespace NativeWorldTextProbe
{
    // R07's explicit Native oracle only. The answers come from the locked
    // original stage and a real, already observed actor. No product callback,
    // capture field, or expected value is repaired by these diagnostics.
    internal static class NativeCombatReviewedNativeChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static readonly FieldInfo Streams=typeof(Main).GetField("_rngs",Flags);
        private static readonly List<string> ParticleStages=new List<string>();
        private static int sporeCalls;

        internal static void Stool(Assembly host,string layout,string output)
        {
            Directory.CreateDirectory(output);
            // These are the existing headless initializers, not Main.Run or
            // Player.Update and not the product composition/input patches.
            typeof(NativeQuickItemChecks).GetMethod("Initialize",Flags).Invoke(null,null);
            Terraria.ObjectData.TileObjectData.Initialize();
            Terraria.DataStructures.ArmorSetBonuses.Initialize();
            Terraria.DataStructures.ArmorSetBonuses.BuildLookup();
            NativeCombatWorkerChecks.Scene(false);NPC.ClearAll();
            bool previousDedicated=Main.dedServ;object previousStreams=Streams.GetValue(null);
            try
            {
                Main.dedServ=true;
                var player=Main.LocalPlayer;
                player.position=new Vector2(800,65*16-player.height);player.velocity=Vector2.Zero;
                player.controlUp=true;player.controlUseItem=false;
                player.armor[3].SetDefaults(4341);
                player.ResetEffects();player.UpdateEquips(0);
                Require(player.portableStoolInfo.HasAStool && player.portableStoolInfo.HeightBoost==26 && player.portableStoolInfo.VisualYOffset==26 && player.portableStoolInfo.MapYOffset==26,"Original equipment stage must establish the actual 26/26/26 stool page.");
                Require(player.CanFitSpace(26) && !player.mount.Active && !player.pulley && player.grappling[0]==-1 && !player.gravControl,"Original stool input/space/posture prerequisites must hold.");
                typeof(Player).GetMethod("UpdatePortableStoolUsage",Flags).Invoke(player,null);
                Require(player.portableStoolInfo.IsInUse && player.height==68 && player.Bottom.Y==65*16 && player.MountedCenter.Y==player.position.Y+21,"Original stool stage must resize about the retained bottom and establish the genuine anchor.");
                Vector2 anchor=player.MountedCenter;
                StoolPageChecks(host,player,output);

                // Observe the leaf round trip as well as the actual fullpage
                // Worker below. A codec mismatch is evidence, not a new value
                // supplied to the Worker or an early substitute for that run.
                var copy=new Player();
                var values=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true).GetField("Players",Flags).GetValue(null);
                Type context=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeActorContext",true);
                using(var bytes=new MemoryStream())
                {
                    using(var writer=new BinaryWriter(bytes,System.Text.Encoding.UTF8,true))
                    {values.GetType().GetMethod("Write",Flags).Invoke(values,new object[]{writer,player});context.GetMethod("WritePlayer",Flags).Invoke(null,new object[]{writer,player});}
                    bytes.Position=0;
                    using(var reader=new BinaryReader(bytes))
                    {values.GetType().GetMethod("Read",Flags).Invoke(values,new object[]{reader,copy});context.GetMethod("ReadPlayer",Flags).Invoke(null,new object[]{reader,copy});Require(bytes.Position==bytes.Length,"Player context round trip consumes one complete current page.");}
                }
                File.WriteAllLines(Path.Combine(output,"stool-anchor.txt"),new[]{"source="+anchor,"decoded="+copy.MountedCenter,"height="+player.height,"actual-stats="+StoolFacts(player),"decoded-stats="+StoolFacts(copy)});

                var npc=Main.npc[0];npc.SetDefaults(2);npc.whoAmI=0;npc.active=true;npc.target=0;npc.position=new Vector2(600,700);npc.timeLeft=750;
                int slot=Projectile.NewProjectile(new EntitySource_DebugCommand(),anchor+new Vector2(100,0),Vector2.Zero,25,0,0,player.whoAmI);
                Require(slot>=0 && slot<Main.maxProjectiles,"The original flail must own an actual available slot.");
                var flail=Main.projectile[slot];flail.ai[0]=2;flail.ai[1]=0;
                Require(flail.active && flail.type==25 && flail.owner==player.whoAmI && !player.dead && !player.noItems && !player.CCed && flail.Distance(anchor)>10 && flail.Distance(player.Center)<900/player.meleeSpeed,"The observed original phase-2 flail must satisfy the actual recall branch.");
                SetStreams(403);
                using(var child=NativeCombatWorkerChecks.Start(layout))
                {
                    var errors=child.StandardError.ReadToEndAsync();
                    try
                    {
                        var scene=NativeCombatWorkerChecks.AcquireFrozen(host,child,new[]{0},new[]{slot},0,horizon:1);
                        File.WriteAllBytes(Path.Combine(output,"stool-fullpage.bin"),scene.Snapshot);
                        File.WriteAllBytes(Path.Combine(output,"stool-worker.bin"),scene.Future);
                        NativeCombatWorkerChecks.Compare(scene.Future,0,output,"stool-flail-one-step",nativeStreams:true,expectedHorizon:1,
                            dependencyComparison:(actual,predicted)=>CompareShot(actual,predicted,slot,25,Path.Combine(output,"stool-flail-one-step.csv")));
                        AssertBusiness(copy.MountedCenter==anchor && StoolFacts(copy)==StoolFacts(player),"The complete original current stool page and MountedCenter must survive the value codec.");
                    }
                    finally{NativeCombatWorkerChecks.Exit(child,"R07 stool helper exits");Require(errors.Wait(5000),"Stool helper stderr closes.");File.WriteAllText(Path.Combine(output,"stool-worker.log"),errors.Result);}
                }
                Console.WriteLine("PASS R07 legal original stool 26/26/26, fullpage Worker and original flail single-step anchor/velocity.");
            }
            finally{Streams.SetValue(null,previousStreams);Main.dedServ=previousDedicated;}
        }

        internal static void Particles(Assembly host,string layout,string output,string content)
        {
            Directory.CreateDirectory(output);
            Require(content!="--cpu" && Directory.Exists(content),"R07 particle oracle requires the actual matching Content, not fabricated loaded metadata.");
            object previousStreams=Streams.GetValue(null);bool previousDedicated=Main.dedServ,previousIgnore=Main.ignoreErrors;
            Main previousMain=Main.instance;
            var poolField=typeof(ParticleOrchestrator).GetField("_poolRandomizedFrame",Flags);
            object previousPool=poolField.GetValue(null);
            var flyPool=typeof(ParticleOrchestrator).GetField("_poolFlies",Flags);
            var sparklePool=typeof(ParticleOrchestrator).GetField("_poolPrettySparkle",Flags);
            object previousFlies=flyPool.GetValue(null),previousSparkles=sparklePool.GetValue(null);
            var previousRenderer=Main.ParticleSystem_World_OverPlayers;
            var observation=new Harmony("JueMingR.Tests.R07.OriginalParticleObservation");
            try
            {
                using(var graphics=new ProbeGraphics(content))
                {
                    graphics.LoadTexture("Projectile","Images/Projectile_228",228);
                    graphics.LoadTexture("Npc","Images/NPC_48",48);
                    Require(TextureAssets.Projectile[228].IsLoaded && TextureAssets.Projectile[228].Value.GraphicsDevice!=null && Main.projFrames[228]>0,"The original particle initializer must receive an actual loaded resource and its current frame count.");
                    NativeCombatWorkerChecks.Scene(false);Main.dedServ=false;Main.ignoreErrors=false;
                    Main.ParticleSystem_World_OverPlayers=new ParticleRenderer();
                    poolField.SetValue(null,new ParticlePool<RandomizedFrameParticle>(2,()=>new RandomizedFrameParticle()));
                    flyPool.SetValue(null,new ParticlePool<LittleFlyingCritterParticle>(2,()=>new LittleFlyingCritterParticle()));
                    sparklePool.SetValue(null,new ParticlePool<PrettySparkleParticle>(2,()=>new PrettySparkleParticle()));
                    OriginalEntrypoints(output);
                    Main.ParticleSystem_World_OverPlayers.Clear();
                    int seed=Enumerable.Range(0,10000).First(value=>new UnifiedRandom(value).Next(60)==0);
                    observation.Patch(typeof(ParticleOrchestrator).GetMethod("SpawnParticlesDirect",Flags),prefix:new HarmonyMethod(typeof(NativeCombatReviewedNativeChecks).GetMethod("BeforeParticle",Flags)),postfix:new HarmonyMethod(typeof(NativeCombatReviewedNativeChecks).GetMethod("AfterParticle",Flags)));
                    using(var child=NativeCombatWorkerChecks.Start(layout))
                    {
                        var errors=child.StandardError.ReadToEndAsync();
                        try
                        {
                            foreach(bool spore in new[]{false,true})
                            {
                            NativeCombatWorkerChecks.Scene(false);Main.dedServ=false;
                            var npc=Main.npc[0];npc.SetDefaults(48);npc.whoAmI=0;npc.active=true;npc.target=0;npc.position=new Vector2(480,480);npc.velocity=new Vector2(2,-1);npc.ai[0]=29;npc.timeLeft=750;
                            if(spore)npc.AddBuff(397,120);
                            Require((npc.buffType.Contains(397)==spore) && npc.aiStyle==14 && Collision.CanHit(npc,Main.LocalPlayer),"The current Harpy, buff branch and original same-stage firing/LOS prerequisites must be real.");
                            SetStreams(seed);string before;using(Main.SwapRandom("UpdateNPCs"))before=NativeCombatWorkerChecks.RandomStamp();
                            sporeCalls=0;ParticleStages.Clear();string name=spore?"spore-harpy-one-step":"harpy-no-particle-one-step";
                            // The three inactive pages are actual original
                            // allocation slots; the Worker cannot invent them.
                            var scene=NativeCombatWorkerChecks.AcquireFrozen(host,child,new[]{0},new[]{0,1,2},0,horizon:1);
                            using(Main.SwapRandom("UpdateNPCs"))Require(before==NativeCombatWorkerChecks.RandomStamp(),"Fullpage capture and Worker leave the original NPC RNG untouched.");
                            File.WriteAllBytes(Path.Combine(output,name+"-fullpage.bin"),scene.Snapshot);
                            File.WriteAllBytes(Path.Combine(output,name+"-worker.bin"),scene.Future);
                            NativeCombatWorkerChecks.Compare(scene.Future,0,output,name,nativeStreams:true,expectedHorizon:1,
                                dependencyComparison:(actual,predicted)=>
                                {
                                    Require(sporeCalls==(spore?1:0),"The original current Harpy step must actually take the declared SporeCloud branch.");
                                    File.WriteAllLines(Path.Combine(output,name+"-original-stages.log"),new[]{"npcSeed="+seed,"NPC-before="+before}.Concat(ParticleStages));
                                    CompareShot(actual,predicted,0,38,Path.Combine(output,name+".csv"));
                                });
                            }
                        }
                        finally{NativeCombatWorkerChecks.Exit(child,"R07 particle helper exits");Require(errors.Wait(5000),"Particle helper stderr closes.");File.WriteAllText(Path.Combine(output,"particle-worker.log"),errors.Result);}
                    }
                    observation.UnpatchAll(observation.Id);
                    ParticleInitializationChecks(host,output);
                }
                Console.WriteLine("PASS R07 actual current SporeCloud initialization and same-actor original Harpy feather single-step.");
            }
            finally
            {
                observation.UnpatchAll(observation.Id);poolField.SetValue(null,previousPool);flyPool.SetValue(null,previousFlies);sparklePool.SetValue(null,previousSparkles);Main.ParticleSystem_World_OverPlayers=previousRenderer;
                Streams.SetValue(null,previousStreams);Main.dedServ=previousDedicated;Main.ignoreErrors=previousIgnore;Main.instance=previousMain;
            }
        }

        private static void StoolPageChecks(Assembly host,Player used,string output)
        {
            Type codec=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePortableStoolSnapshot",true);
            var ordinary=new Player{active=true,whoAmI=1};ordinary.ResetEffects();
            var equipped=new Player{active=true,whoAmI=1};equipped.armor[3].SetDefaults(4341);equipped.ResetEffects();equipped.UpdateEquips(1);
            Require(equipped.portableStoolInfo.HasAStool && !equipped.portableStoolInfo.IsInUse,"Original equipment can be present without current stool use.");
            var jumping=new Player{active=true,whoAmI=1,position=used.position,height=used.height,width=used.width,controlJump=true,releaseJump=true};
            jumping.portableStoolInfo=used.portableStoolInfo;
            jumping.JumpMovement();
            Require(!jumping.portableStoolInfo.IsInUse && jumping.portableStoolInfo.HasAStool && jumping.height==68,"Original JumpMovement really clears use while the retained expanded height has not yet resized.");
            var rows=new List<string>();
            foreach(var source in new[]{ordinary,equipped,used,jumping})
            {
                byte[] page=StoolPage(codec,source);var restored=new Player{position=source.position,width=source.width,height=source.height};
                using(var reader=new BinaryReader(new MemoryStream(page)))codec.GetMethod("Read",Flags).Invoke(null,new object[]{reader,restored});
                AssertBusiness(page.Length==18 && StoolFacts(restored)==StoolFacts(source) && restored.MountedCenter==source.MountedCenter,"All five current stool values and natural anchor survive Reset/equipped/used/Jump states without resizing.");
                rows.Add(StoolFacts(source)+",height="+source.height+",center="+source.MountedCenter);
            }
            byte[] valid=StoolPage(codec,used);
            var premise=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true).GetMethod("PlayerPremise",Flags);
            byte[] equippedProof=PlayerPremise(premise,equipped);var equippedValue=equipped.portableStoolInfo;
            equipped.portableStoolInfo.Reset();byte[] resetProof=PlayerPremise(premise,equipped);equipped.portableStoolInfo=equippedValue;
            AssertBusiness(!equippedProof.SequenceEqual(resetProof),"Stool premise changes retire old history even while primitive pose, controls and equipment stay identical.");
            foreach(int offset in new[]{4,5}){byte[] bad=(byte[])valid.Clone();bad[offset]=2;RefuseStool(codec,bad);}
            foreach(var change in new[]{new[]{0,2},new[]{6,-1},new[]{10,13},new[]{14,int.MaxValue}})
            {byte[] bad=(byte[])valid.Clone();Buffer.BlockCopy(BitConverter.GetBytes(change[1]),0,bad,change[0],4);RefuseStool(codec,bad);}
            var impossible=(byte[])valid.Clone();impossible[4]=0;RefuseStool(codec,impossible);
            foreach(int length in new[]{0,3,4,5,17})RefuseStool(codec,valid.Take(length).ToArray());
            File.WriteAllLines(Path.Combine(output,"stool-real-combinations.txt"),rows);
            Console.WriteLine("PASS stool Reset/equipment/use/original-Jump combinations, future/corrupt/truncated pages and atomic failed apply");
        }
        private static byte[] StoolPage(Type codec,Player player)
        {using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){codec.GetMethod("Write",Flags).Invoke(null,new object[]{writer,player});writer.Flush();return stream.ToArray();}}
        private static byte[] PlayerPremise(MethodInfo method,Player player)
        {using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){method.Invoke(null,new object[]{writer,player});writer.Flush();return stream.ToArray();}}
        private static void RefuseStool(Type codec,byte[] bytes)
        {
            var player=new Player();string before=StoolFacts(player);bool refused=false;
            try{using(var reader=new BinaryReader(new MemoryStream(bytes)))codec.GetMethod("Read",Flags).Invoke(null,new object[]{reader,player});}
            catch(TargetInvocationException error){refused=error.InnerException is InvalidDataException || error.InnerException is EndOfStreamException;}
            AssertBusiness(refused && StoolFacts(player)==before,"Future/corrupt/incomplete stool pages refuse without partially applying the private state.");
        }
        private static void ParticleInitializationChecks(Assembly host,string output)
        {
            Type helper=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeParticleInitialization",true);
            Type boundary=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeEffectBoundary",true);
            var dispatcher=typeof(ParticleOrchestrator).GetMethod("SpawnParticlesDirect",Flags);
            var prefix=new HarmonyMethod(boundary.GetMethod("ParticleInitialization",Flags));
            var patch=new Harmony("JueMingR.Tests.R07.PrivateInitialization");
            var randomPool=typeof(ParticleOrchestrator).GetField("_poolRandomizedFrame",Flags);
            var flyPool=typeof(ParticleOrchestrator).GetField("_poolFlies",Flags);
            var sparklePool=typeof(ParticleOrchestrator).GetField("_poolPrettySparkle",Flags);
            object oldRandom=randomPool.GetValue(null),oldFlies=flyPool.GetValue(null),oldSparkles=sparklePool.GetValue(null);
            var oldRenderer=Main.ParticleSystem_World_OverPlayers;var oldRng=Main.rand;bool oldPooling=Main.NoPooling;int oldMode=Main.netMode;
            var rows=new List<string>{"type,poolState,noPooling,seed,entry,originalState,privateState"};
            var settings=new ParticleOrchestraSettings{PositionInWorld=new Vector2(500,500),MovementVector=new Vector2(1,-1)};
            try
            {
                Main.netMode=0;
                foreach(var type in new[]{ParticleOrchestraType.SporeCloud,ParticleOrchestraType.PooFly,ParticleOrchestraType.SilverBulletSparkle})
                foreach(int poolState in new[]{0,1,2})foreach(bool noPooling in new[]{false,true})
                {
                    Main.NoPooling=noPooling;Main.ParticleSystem_World_OverPlayers=new ParticleRenderer();
                    randomPool.SetValue(null,new ParticlePool<RandomizedFrameParticle>(64,()=>new RandomizedFrameParticle()));
                    flyPool.SetValue(null,new ParticlePool<LittleFlyingCritterParticle>(64,()=>new LittleFlyingCritterParticle()));
                    sparklePool.SetValue(null,new ParticlePool<PrettySparkleParticle>(64,()=>new PrettySparkleParticle()));
                    // Fill via the actual native initializer and renderer, not
                    // forged occupancy bits. Clear is the original pool-return
                    // path; NoPooling must also handle these resting entries.
                    if(poolState!=0)for(int i=0;i<60;i++)ParticleOrchestrator.SpawnParticlesDirect(type,settings);
                    if(poolState==2)Main.ParticleSystem_World_OverPlayers.Clear();
                    Require(Main.ParticleSystem_World_OverPlayers.Particles.Count==(poolState==1?60:0),"Original pool fixture must have the declared active particle occupancy.");
                    var expected=new Dictionary<string,string>();
                    foreach(int seed in new[]{0,35,1907})foreach(int entry in new[]{0,1,2})
                    {
                        Main.rand=new UnifiedRandom(seed);CallParticle(type,entry,settings);
                        expected.Add(seed+":"+entry,NativeCombatWorkerChecks.RandomStamp());
                    }
                    if(type==ParticleOrchestraType.PooFly)
                        Require(((ParticlePool<LittleFlyingCritterParticle>)flyPool.GetValue(null)).CountParticlesInUse()==0,"The actual authenticated .8 pool-count invariant must hold for the populated original pool.");
                    string rngBefore=NativeCombatWorkerChecks.RandomStamp();var rngObject=Main.rand;
                    var renderer=Main.ParticleSystem_World_OverPlayers;int rendererCount=renderer.Particles.Count;
                    object capturedRandom=randomPool.GetValue(null),capturedFlies=flyPool.GetValue(null),capturedSparkles=sparklePool.GetValue(null);
                    byte[] page=ParticlePage(helper);
                    AssertBusiness(page.Length==18 && ReferenceEquals(rngObject,Main.rand) && rngBefore==NativeCombatWorkerChecks.RandomStamp() && Main.ParticleSystem_World_OverPlayers.Particles.Count==rendererCount,"The bounded particle premise capture is read-only and consumes no live RNG.");
                    patch.Patch(dispatcher,prefix:prefix);
                    try
                    {
                        foreach(int seed in new[]{0,35,1907})foreach(int entry in new[]{0,1,2})
                        {
                            BeginParticle(boundary,helper,page);Main.rand=new UnifiedRandom(seed);CallParticle(type,entry,settings);
                            string actual=NativeCombatWorkerChecks.RandomStamp(),answer=expected[seed+":"+entry];
                            rows.Add(type+","+poolState+","+noPooling+","+seed+","+entry+",\""+answer+"\",\""+actual+"\"");
                            AssertBusiness(answer==actual && ReferenceEquals(renderer,Main.ParticleSystem_World_OverPlayers) && renderer.Particles.Count==rendererCount
                                && ReferenceEquals(capturedRandom,randomPool.GetValue(null)) && ReferenceEquals(capturedFlies,flyPool.GetValue(null)) && ReferenceEquals(capturedSparkles,sparklePool.GetValue(null))
                                && (string)boundary.GetProperty("Failure",Flags).GetValue(null,null)==null,"Audited current initialization must preserve the entire original RNG state across all three entries without publishing a particle or replacing live pools.");
                        }
                    }
                    finally{patch.UnpatchAll(patch.Id);}
                }
                ParticlePageRefusals(helper,boundary,patch,dispatcher,prefix,settings);
            }
            finally
            {
                patch.UnpatchAll(patch.Id);randomPool.SetValue(null,oldRandom);flyPool.SetValue(null,oldFlies);sparklePool.SetValue(null,oldSparkles);
                Main.ParticleSystem_World_OverPlayers=oldRenderer;Main.rand=oldRng;Main.NoPooling=oldPooling;Main.netMode=oldMode;
                helper.GetMethod("Clear",Flags).Invoke(null,null);File.WriteAllLines(Path.Combine(output,"particle-initialization-rng.csv"),rows);
            }
            Console.WriteLine("PASS 162 actual native initializer RNG comparisons: three types/entries/seeds, empty/active>50/resting pools and NoPooling; no renderer publication");
        }
        private static void ParticlePageRefusals(Type helper,Type boundary,Harmony patch,MethodInfo dispatcher,HarmonyMethod prefix,ParticleOrchestraSettings settings)
        {
            byte[] valid=ParticlePage(helper);
            foreach(var change in new[]{new[]{0,2},new[]{6,0},new[]{10,32769},new[]{14,-1}})
            {byte[] bad=(byte[])valid.Clone();Buffer.BlockCopy(BitConverter.GetBytes(change[1]),0,bad,change[0],4);RefuseParticlePage(helper,bad);}
            foreach(int offset in new[]{4,5}){byte[] bad=(byte[])valid.Clone();bad[offset]=2;RefuseParticlePage(helper,bad);}
            var inconsistent=(byte[])valid.Clone();inconsistent[4]=0;RefuseParticlePage(helper,inconsistent);
            foreach(int length in new[]{0,3,4,5,17})RefuseParticlePage(helper,valid.Take(length).ToArray());
            var asset=TextureAssets.Projectile[228];byte[] missing,unloaded;
            try
            {
                TextureAssets.Projectile[228]=null;missing=ParticlePage(helper);
                var pending=(ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>)typeof(ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>).GetConstructor(Flags,null,new[]{typeof(string)},null).Invoke(new object[]{asset.Name});
                TextureAssets.Projectile[228]=pending;unloaded=ParticlePage(helper);
                AssertBusiness(!pending.IsLoaded,"Capturing an unloaded real Asset object must not request or load it.");
            }
            finally{TextureAssets.Projectile[228]=asset;}
            var zeroFrames=(byte[])valid.Clone();Buffer.BlockCopy(BitConverter.GetBytes(0),0,zeroFrames,14,4);
            patch.Patch(dispatcher,prefix:prefix);
            try
            {
                string before;
                foreach(byte[] unknown in new[]{missing,unloaded,zeroFrames})
                {
                    BeginParticle(boundary,helper,unknown);before=NativeCombatWorkerChecks.RandomStamp();bool refused=false;
                    try{ParticleOrchestrator.RequestParticleSpawn(true,ParticleOrchestraType.SporeCloud,settings);}catch(InvalidDataException){refused=true;}
                    AssertBusiness(refused && before==NativeCombatWorkerChecks.RandomStamp() && (string)boundary.GetProperty("Failure",Flags).GetValue(null,null)!=null,"An actual Spore invocation rejects unknown loaded-resource/frame premises with a sticky failure even if the caller catches the exception.");
                }
                BeginParticle(boundary,helper,missing);ParticleOrchestrator.RequestParticleSpawn(true,ParticleOrchestraType.SilverBulletSparkle,settings);
                AssertBusiness((string)boundary.GetProperty("Failure",Flags).GetValue(null,null)==null,"An unused missing Spore resource must not block another audited initializer.");
                BeginParticle(boundary,helper,valid);before=NativeCombatWorkerChecks.RandomStamp();Main.netMode=2;
                ParticleOrchestrator.RequestParticleSpawn(true,ParticleOrchestraType.SporeCloud,settings);
                AssertBusiness(before==NativeCombatWorkerChecks.RandomStamp(),"The original server dispatcher guard consumes no initialization RNG.");
                Main.netMode=0;
                BeginParticle(boundary,helper,valid);before=NativeCombatWorkerChecks.RandomStamp();ParticleOrchestrator.RequestParticleSpawn(true,ParticleOrchestraType.Keybrand,settings);
                AssertBusiness(before==NativeCombatWorkerChecks.RandomStamp() && (string)boundary.GetProperty("Failure",Flags).GetValue(null,null)==null,"The limited repair does not change remaining existing Skip types into a global Block.");
            }
            finally{Main.netMode=0;patch.UnpatchAll(patch.Id);}
            Console.WriteLine("PASS particle resource unknown/sticky isolation, server guard, future/corrupt/truncated pages and unchanged unmodeled Skip");
        }
        private static void BeginParticle(Type boundary,Type helper,byte[] bytes)
        {boundary.GetMethod("Begin",Flags).Invoke(null,null);using(var reader=new BinaryReader(new MemoryStream(bytes)))helper.GetMethod("Read",Flags).Invoke(null,new object[]{reader});}
        private static byte[] ParticlePage(Type helper)
        {using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){helper.GetMethod("Write",Flags).Invoke(null,new object[]{writer});writer.Flush();return stream.ToArray();}}
        private static void RefuseParticlePage(Type helper,byte[] bytes)
        {
            bool refused=false;try{using(var reader=new BinaryReader(new MemoryStream(bytes)))helper.GetMethod("Read",Flags).Invoke(null,new object[]{reader});}
            catch(TargetInvocationException error){refused=error.InnerException is InvalidDataException || error.InnerException is EndOfStreamException;}
            AssertBusiness(refused,"Malformed/future/incomplete particle initialization pages must refuse, never borrow old metadata.");
        }
        private static void CallParticle(ParticleOrchestraType type,int entry,ParticleOrchestraSettings settings)
        {
            if(entry==0)ParticleOrchestrator.RequestParticleSpawn(true,type,settings);
            else if(entry==1)ParticleOrchestrator.BroadcastParticleSpawn(type,settings);
            else ParticleOrchestrator.BroadcastOrRequestParticleSpawn(type,settings);
        }
        private static void OriginalEntrypoints(string output)
        {
            var rows=new List<string>{"type,entry,originalRngBefore,originalRngAfter,particleCount"};
            foreach(ParticleOrchestraType type in new[]{ParticleOrchestraType.SporeCloud,ParticleOrchestraType.PooFly,ParticleOrchestraType.SilverBulletSparkle})
                foreach(int entry in new[]{0,1,2})
                {
                    Main.rand=new UnifiedRandom(901);string before=NativeCombatWorkerChecks.RandomStamp();
                    var settings=new ParticleOrchestraSettings{PositionInWorld=new Vector2(500,500),MovementVector=new Vector2(1,-1)};
                    if(entry==0)ParticleOrchestrator.RequestParticleSpawn(true,type,settings);
                    else if(entry==1)ParticleOrchestrator.BroadcastParticleSpawn(type,settings);
                    else ParticleOrchestrator.BroadcastOrRequestParticleSpawn(type,settings);
                    rows.Add(type+","+entry+",\""+before+"\",\""+NativeCombatWorkerChecks.RandomStamp()+"\","+Main.ParticleSystem_World_OverPlayers.Particles.Count);
                }
            var flies=typeof(ParticleOrchestrator).GetField("_poolFlies",Flags).GetValue(null);
            rows.Add("actual-PooFly-pool-count="+flies.GetType().GetMethod("CountParticlesInUse").Invoke(flies,null));
            File.WriteAllLines(Path.Combine(output,"particle-original-entrypoints.csv"),rows);
        }
        private static void BeforeParticle(ParticleOrchestraType type)
        {if(type==ParticleOrchestraType.SporeCloud){sporeCalls++;ParticleStages.Add("SporeCloud-before="+NativeCombatWorkerChecks.RandomStamp());}}
        private static void AfterParticle(ParticleOrchestraType type)
        {if(type==ParticleOrchestraType.SporeCloud)ParticleStages.Add("SporeCloud-after="+NativeCombatWorkerChecks.RandomStamp());}
        private static void SetStreams(int npcSeed)
        {Streams.SetValue(null,new Dictionary<string,UnifiedRandom>{{"UpdatePlayers",new UnifiedRandom(531)},{"UpdateNPCs",new UnifiedRandom(npcSeed)},{"UpdateProjectiles",new UnifiedRandom(171)}});}
        private static string StoolFacts(Player player)
        {var value=player.portableStoolInfo;return value.HasAStool+","+value.IsInUse+","+value.HeightBoost+","+value.VisualYOffset+","+value.MapYOffset;}
        private sealed class Shot
        {internal int Step,Slot,Type;internal uint Key;internal Vector2 Position,Velocity;}
        private static List<Shot> Shots(byte[] bytes)
        {
            var shots=new List<Shot>();using(var reader=new BinaryReader(new MemoryStream(bytes)))
            while(reader.BaseStream.Position<reader.BaseStream.Length)
            {
                int step=reader.ReadInt32(),npcs=reader.ReadInt32();Require(npcs>=0 && npcs<=Main.maxNPCs,"Bounded original NPC dependency count.");
                for(int i=0;i<npcs;i++){reader.ReadInt32();reader.ReadByte();reader.ReadInt32();reader.ReadInt32();for(int n=0;n<4;n++)reader.ReadSingle();reader.ReadInt32();for(int n=0;n<4;n++)reader.ReadSingle();}
                int count=reader.ReadInt32();Require(count>=0 && count<=Main.maxProjectiles,"Bounded original projectile dependency count.");
                for(int i=0;i<count;i++)
                {var shot=new Shot{Step=step,Slot=reader.ReadInt32(),Key=reader.ReadUInt32(),Type=reader.ReadInt32(),Position=new Vector2(reader.ReadSingle(),reader.ReadSingle()),Velocity=new Vector2(reader.ReadSingle(),reader.ReadSingle())};reader.ReadInt32();for(int n=0;n<3;n++)reader.ReadSingle();shots.Add(shot);}
            }
            return shots;
        }
        private static void CompareShot(byte[] actual,byte[] predicted,int slot,int type,string output)
        {
            var expected=Shots(actual).Single(shot=>shot.Step==1 && shot.Slot==slot && shot.Type==type);
            var matching=Shots(predicted).Where(shot=>shot.Step==1 && shot.Slot==slot && shot.Type==type).ToArray();
            AssertBusiness(matching.Length==1,"The Worker must preserve this original single-step projectile birth/identity.");
            var observed=matching[0];
            File.WriteAllLines(output,new[]{"source,slot,type,key,x,y,vx,vy","original,"+ShotRow(expected),"worker,"+ShotRow(observed)});
            AssertBusiness(expected.Key==observed.Key && Vector2.Distance(expected.Position,observed.Position)<=.002f && Vector2.Distance(expected.Velocity,observed.Velocity)<=.002f,"Original single-step projectile "+type+" differs after current actor snapshot/initialization: expected="+expected.Velocity+" Worker="+observed.Velocity);
        }
        private static string ShotRow(Shot shot)
        {return shot.Slot+","+shot.Type+","+shot.Key+","+shot.Position.X.ToString("R")+","+shot.Position.Y.ToString("R")+","+shot.Velocity.X.ToString("R")+","+shot.Velocity.Y.ToString("R");}
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private static void AssertBusiness(bool condition,string message){if(!condition)throw new JueMingR.Testing.BusinessAssertionException(message);}
    }
}
