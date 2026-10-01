using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Utilities;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Development native scene. General production snapshot completeness and
    // lifecycle publication are still separate integration requirements.
    internal sealed class PredictionSandbox : IDisposable
    {
        private static readonly FieldInfo UpdateCount=typeof(Main).GetField("_gameUpdateCount",BindingFlags.Static|BindingFlags.NonPublic);
        private static readonly FieldInfo CollisionContacts=typeof(Collision).GetField("contacts",BindingFlags.Static|BindingFlags.NonPublic),CollisionConveyors=typeof(Collision).GetField("_cacheForConveyorBelts",BindingFlags.Static|BindingFlags.NonPublic);
        private static readonly float CollisionEpsilon=Collision.Epsilon;
        private readonly NativeTileBoundary terrain;
        private readonly NativeEffectBoundary effects;
        private readonly NativeTerrainStore terrainValues=new NativeTerrainStore();
        private readonly bool[] restoredPlayers=new bool[Main.maxPlayers];
        private readonly NativePlayerMotion playerMotion=new NativePlayerMotion();
        private readonly byte[] emptyWorld;
        internal byte[] Alignment {get;private set;}
        internal int MeasuredCompletedSteps {get;private set;}
        internal PredictionSandbox()
        {
            var startup=PredictionPipeProtocol.Measure?Stopwatch.StartNew():null;
            if(typeof(Main).Assembly.ManifestModule.ModuleVersionId!=new Guid("2c29f6c3-4bd9-4add-9c58-da159804e083"))throw new InvalidOperationException("Native version mismatch.");
            Main.dedServ=true;Main.netMode=0;Main.myPlayer=0;Main.gameMenu=false;
            Main.ActiveWorldFileData=new Terraria.IO.WorldFileData();
            Main.rand=new UnifiedRandom(123);
            Terraria.Localization.LanguageManager.Instance.SetLanguage("en-US");Lang.InitializeLegacyLocalization();
            // These two original initializers only establish native type data;
            // Initialize_AlmostEverything would read preferences and network.
            foreach(string name in new[]{"Initialize_TileAndNPCData1","Initialize_TileAndNPCData2"})
                typeof(Main).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            Reset(120,120);
            NativeMountSnapshot.InitializePrivateDefinitions();
            StartupPart(startup,"type-data-reset");
            Terraria.ID.ContentSamples.Initialize();
            StartupPart(startup,"content-samples");
            Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();
            Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);
            StartupPart(startup,"drop-database");
            if(typeof(Terraria.GameContent.TextureAssets).GetField("Gore").FieldType!=typeof(ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>[]))
                throw new InvalidOperationException("Duplicate embedded assembly identity.");
            effects=new NativeEffectBoundary();
            StartupPart(startup,"effect-patches");
            terrain=new NativeTileBoundary();
            StartupPart(startup,"tile-intrinsics");
            NativeEntityDirectory.VerifyIntrinsics();
            StartupPart(startup,"entity-intrinsics");
            NativeImmunitySnapshot.VerifyIntrinsics();
            StartupPart(startup,"immunity-intrinsics");
            NativeExecutionPreparation.Run();
            using(var baseline=new MemoryStream())using(var writer=new BinaryWriter(baseline))
            {NativeWorldSnapshot.Write(writer);NativeEntityContext.WriteShared(writer);NativeRandomSnapshot.Write(writer);writer.Flush();emptyWorld=baseline.ToArray();}
            Console.Error.WriteLine("TERRAIN methods="+terrain.Methods+" accesses="+terrain.Accesses);
        }
        private static void StartupPart(Stopwatch watch,string name)
        {if(watch!=null){Console.Error.WriteLine("STARTUP "+name+"-ms="+watch.Elapsed.TotalMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));watch.Restart();}}
        internal void ClearWorld()
        {
            // Execution code and intrinsic hooks survive; private world values
            // and cache references do not. Only the serial worker invokes this,
            // and the parent cannot send a new world until its acknowledgement.
            NativeEntityDirectory.ClearWorld();NativeTileBoundary.Begin();NativeTileBoundary.End();Alignment=null;
            Reset(120,120);
            using(var reader=new BinaryReader(new MemoryStream(emptyWorld,false)))
            {NativeWorldSnapshot.Read(reader);NativeEntityContext.ReadShared(reader);NativeRandomSnapshot.Read(reader);}
            terrainValues.Clear();NativeAssetSnapshot.ClearWorld();NativeImmunitySnapshot.ClearWorld();playerMotion.Clear();NativeEffectBoundary.Begin();
            NativeLightingSnapshot.Clear();
            Projectile.ClearAll();Main.ActiveWorldFileData=new Terraria.IO.WorldFileData();ClearCollisionWorld();
        }
        private static void ClearCollisionWorld()
        {
            // Native consumers usually clear these at entry, but an early
            // return can retain old-world coordinates. Clear before reset ACK,
            // independently of whether the next NPC uses that entry point.
            ((System.Collections.IList)CollisionContacts.GetValue(null)).Clear();
            ((System.Collections.IList)CollisionConveyors.GetValue(null)).Clear();
            Collision.stair=Collision.stairFall=Collision.honey=Collision.shimmer=Collision.sloping=Collision.landMine=Collision.up=Collision.down=false;
            Collision.Epsilon=CollisionEpsilon;
        }
        internal byte[] Predict(byte[] bytes,bool withAlignment=false)
        {
            Alignment=null;if(PredictionPipeProtocol.Measure)MeasuredCompletedSteps=0;
            long started=PredictionPipeProtocol.Measure?Stopwatch.GetTimestamp():0;
            NativeEffectBoundary.Begin();
            NativeEntityDirectory.Reset();
            NativeImmunitySnapshot.Reset();
            using(var input=new MemoryStream(bytes,false))using(var reader=new BinaryReader(input,Encoding.UTF8,true))
            {
                if(reader.ReadInt32()!=PredictionWire.Protocol)throw new InvalidDataException("Protocol mismatch.");
                long tick=reader.ReadInt64();int horizon=reader.ReadInt32(),selected=reader.ReadInt32();
                if(tick<0 || horizon<1 || horizon>PredictionWire.MaximumHorizon || selected<0 || selected>=Main.maxNPCs)throw new InvalidDataException("Invalid request identity.");
                int width=reader.ReadInt32(),height=reader.ReadInt32();
                NativeTerrainSnapshot.ValidateExtent(1,width,height);
                Reset(width,height);
                long reset=PredictionPipeProtocol.Measure?Stopwatch.GetTimestamp():0;
                Main.worldSurface=reader.ReadDouble();Main.rockLayer=reader.ReadDouble();Main.dayTime=reader.ReadBoolean();Main.time=reader.ReadDouble();
                Main.GameMode=reader.ReadInt32();Main.myPlayer=reader.ReadInt32();Main.bloodMoon=reader.ReadBoolean();Main.eclipse=reader.ReadBoolean();Main.windSpeedCurrent=reader.ReadSingle();
                if(Main.GameMode<0 || Main.GameMode>3 || Main.myPlayer<0 || Main.myPlayer>=Main.maxPlayers || !Finite(Main.worldSurface) || !Finite(Main.rockLayer) || !Finite(Main.time) || !Finite(Main.windSpeedCurrent))throw new InvalidDataException("Invalid world values.");
                NativeWorldSnapshot.Read(reader);
                NativeEntityContext.ReadShared(reader);
                NativeAssetSnapshot.Read(reader);
                NativeLightingSnapshot.Read(reader);
                NativeEffectBoundary.ReadAllocation(reader);
                NativeRandomSnapshot.Read(reader);
                NativeEntityDirectory.Read(reader);
#if JMR_CONDITIONAL_RESEARCH
                ConditionalNpcQuery.ReadRoles(reader);
#endif
                int playerCount=Count(reader,1,Main.maxPlayers);var players=new bool[Main.maxPlayers];var playerSlots=new int[playerCount];
                for(int i=0;i<playerCount;i++)
                {int slot=Count(reader,0,Main.maxPlayers-1);if(players[slot])throw new InvalidDataException("Duplicate player slot.");playerSlots[i]=slot;players[slot]=restoredPlayers[slot]=true;PredictionWire.Players.Read(reader,Main.player[slot]);NativeActorContext.ReadPlayer(reader,Main.player[slot]);playerMotion.Read(reader,slot);if(!Main.player[slot].active || Main.player[slot].whoAmI!=slot)throw new InvalidDataException("Player identity mismatch.");}
                int count=Count(reader,1,Main.maxNPCs+1);var slots=new int[count];int prior=-1;bool found=false;
                for(int i=0;i<count;i++)
                {
                    int slot=Count(reader,0,Main.maxNPCs);if(slot<=prior)throw new InvalidDataException("Invalid native order.");prior=slot;slots[i]=slot;found|=slot==selected;
                    NPC npc=Main.npc[slot];int directoryType=npc.type;byte generation=npc.generation;bool active=npc.active;
                    PredictionWire.Npcs.Read(reader,npc);ValidateSnapshot(npc,slot);
                    NativeEntityContext.ReadNpc(reader,npc);
                    if(npc.type!=directoryType || npc.generation!=generation || npc.active!=active)throw new InvalidDataException("NPC page conflicts with directory identity.");
                    NativeEntityDirectory.KnowNpc(slot);
                }
                if(!found || !Main.npc[selected].active)throw new InvalidDataException("Selected NPC absent.");
                NativeLightingSnapshot.BindRestoredActors();
                NPC selectedNpc=Main.npc[selected];byte selectedGeneration=selectedNpc.generation,ended=0;
                int[] projectileSlots=NativeEntitySnapshot.Read(reader);
                NativeImmunitySnapshot.Read(reader);
                if(Main.maxTilesX!=width || Main.maxTilesY!=height)throw new InvalidDataException("Conflicting terrain dimensions.");
                terrainValues.Read(reader,width,height);
                if(input.Position!=input.Length)throw new InvalidDataException("Trailing snapshot data.");
#if JMR_CONDITIONAL_RESEARCH
                int[] alignmentSlots=ConditionalNpcQuery.Begin(slots,selected);
#else
                int[] alignmentSlots=slots;
#endif
                foreach(int slot in slots)if(Main.npc[slot].type==36)
                {int parent=(int)Main.npc[slot].ai[1];if(parent<0 || parent>=Main.maxNPCs || !Main.npc[parent].active || Main.npc[parent].type!=35)throw new InvalidDataException("Missing Skeletron parent.");}
                long restored=PredictionPipeProtocol.Measure?Stopwatch.GetTimestamp():0;
                var playerFuture=new float[(horizon+1)*playerCount*4];RecordPlayers(playerSlots,playerFuture,0);
                using(var result=new MemoryStream())using(var writer=new BinaryWriter(result,Encoding.UTF8,true))using(var dependencies=new NativeDependencyTimeline())
                using(var proofBytes=new MemoryStream())using(var proof=new BinaryWriter(proofBytes))
                {
                    if(withAlignment){proof.Write(horizon+1);NativePredictionAlignment.Write(proof,NativePredictionAlignment.Observe(tick,alignmentSlots,projectileSlots,selected));}
                    writer.Write(PredictionWire.Protocol);writer.Write(tick);writer.Write(selected);writer.Write(horizon+1);
                    playerMotion.Begin();WritePoint(writer,selectedNpc,selectedGeneration,ended,0);dependencies.Record(0);
                    bool oldSolid379=Main.tileSolid[379];Main.tileSolid[379]=false;
                    NativeTileBoundary.Begin();
                    NativeEntityDirectory.Begin();
                    try
                    {
                    long advanceTicks=0;
#if JMR_CONDITIONAL_RESEARCH
                    long actualNpcCalls=0,actualProjectileCalls=0;
#endif
                    for(int step=1;step<=horizon;step++)
                    {
                        long advanceStart=PredictionPipeProtocol.Measure?Stopwatch.GetTimestamp():0;
                        NativeNpcMotionTrace.Begin(selectedNpc);
                        UpdateCount.SetValue(null,unchecked((uint)(tick+step)));
                        NativeWorldSnapshot.AdvanceObservedWind();
#if JMR_CONDITIONAL_RESEARCH
                        ConditionalNpcQuery.Advance(step);
#endif
                        NPC.UpdateProtectedSpawnSlots();
                        NPC.ClearFoundActiveNPCs();NPC.UpdateFoundActiveNPCs();
                        playerMotion.Advance();
                        // Match Main.UpdateWorld_NPCs: players consume this
                        // tick's selector before the NPC phase retires a dead
                        // or replaced brain. Clearing earlier loses one native
                        // refresh; never clearing it leaves gravity permanent.
                        if(NPC.brainOfGravity>=0 && NPC.brainOfGravity<Main.maxNPCs && (!Main.npc[NPC.brainOfGravity].active || Main.npc[NPC.brainOfGravity].type!=266))NPC.brainOfGravity=-1;
                        // A selected hand sees the already updated lower head
                        // slot. Spawned private entities retain native indices.
                        // Native town AI registers seats during this phase.
                        // Anchors are per tick, not persistent scene state.
                        Main.sittingManager.ClearNPCAnchors();
                        using(NativeRandomSnapshot.Use("UpdateNPCs"))
                            for(int slot=0;slot<Main.maxNPCs;slot++)if(Main.npc[slot].active && NativeEntityDirectory.CanAdvance(Main.npc[slot]))
                            {
#if JMR_CONDITIONAL_RESEARCH
                                actualNpcCalls++;
#endif
                                NativeNpcMotionTrace.Enter(Main.npc[slot]);try{Main.npc[slot].UpdateNPC(slot);}finally{NativeNpcMotionTrace.Leave();}}
                        // Enumerate native slots at the phase itself: a new
                        // higher slot participates this tick; a reused lower
                        // slot waits until the next tick. This also includes
                        // known inactive pool pages made active by native AI.
                        using(NativeRandomSnapshot.Use("UpdateProjectiles"))
                        try
                        {
                            for(int slot=0;slot<Main.maxProjectiles;slot++)
                            {Main.ProjectileUpdateLoopIndex=slot;if(Main.projectile[slot].active && NativeEntityDirectory.CanAdvance(Main.projectile[slot])){
#if JMR_CONDITIONAL_RESEARCH
                                actualProjectileCalls++;
#endif
                                Main.projectile[slot].Update(slot);}}
                        }
                        finally{Main.ProjectileUpdateLoopIndex=-1;}
                        // Time follows both entity phases. Frozen or aligned
                        // requests must advance the same event state before
                        // recording points and end identities.
                        Main.time+=Main.dayRate;
                        if(Main.time>(Main.dayTime?54000:32400))throw new InvalidDataException("World time transition outside this conditional forecast.");
                        if(Terraria.GameContent.Events.DD2Event.Ongoing)
                        {
                            int wait=Terraria.GameContent.Events.DD2Event.TimeLeftBetweenWaves;
                            // Betsy's time-phase birth depends on preceding
                            // weather RNG which this conditional entity future
                            // does not simulate. Never invent its spawn values.
                            if(wait==1 && NPC.waveNumber==7 && Terraria.GameContent.Events.DD2Event.OngoingDifficulty==3 && !Terraria.GameContent.Events.DD2Event.LostThisRun)throw new InvalidDataException("DD2 Betsy time-phase birth needs a new observed scene.");
                            using(NativeRandomSnapshot.Use("UpdateTime"))Terraria.GameContent.Events.DD2Event.UpdateTime();
                        }
                        if(NativeTileBoundary.Missing)throw new InvalidDataException("Native code swallowed an unknown terrain access.");
                        if(NativeEffectBoundary.Failure!=null)throw new InvalidDataException("Native code swallowed a forbidden external operation.");
                        if(NativeAssetSnapshot.Failure!=null)throw new InvalidDataException(NativeAssetSnapshot.Failure);
                        if(NativeEntityDirectory.MissingKind!=0)throw new InvalidDataException("Native code swallowed an unknown entity access.");
                        if(NativeImmunitySnapshot.Failure!=null)throw new InvalidDataException(NativeImmunitySnapshot.Failure);
                        // A slot is not an instance. Native NewNPC may replace
                        // even an active object; type/netID may instead change
                        // on this same object through Transform. End once and
                        // never reconnect a selected path to a later birth.
                        ended=TargetEnd(Main.npc[selected],selectedNpc,selectedGeneration,ended);
                        if(PredictionPipeProtocol.Measure)advanceTicks+=Stopwatch.GetTimestamp()-advanceStart;
                        WritePoint(writer,selectedNpc,selectedGeneration,ended,step);
                        if(withAlignment)
                        {
                            // Session freshness is capped at age 60, including
                            // natural ends and shorter demands. Later points retain
                            // full native movement and presentation semantics,
                            // but cannot ever serve as an acceptance proof.
                            var observed=step<=PredictionWire.MaximumAlignmentAge?NativePredictionAlignment.Observe(tick+step,alignmentSlots,projectileSlots,selected):NativePredictionAlignment.Presentation(tick+step,selected);
                            observed.NewSegment=NativeNpcMotionTrace.Complete();
                            NativePredictionAlignment.Write(proof,observed);
                        }
                        RecordPlayers(playerSlots,playerFuture,step);
                        dependencies.Record(step);
                        if(PredictionPipeProtocol.Measure)MeasuredCompletedSteps=step;
                    }
                    if(withAlignment)proof.Write(advanceTicks);
#if JMR_CONDITIONAL_RESEARCH
                    Console.Error.WriteLine("RESEARCH calls npc="+actualNpcCalls+" projectile="+actualProjectileCalls+" query-reads="+ConditionalNpcQuery.QueryReads+" geometry-stores="+ConditionalNpcQuery.GeometryStores+" exact-roles="+alignmentSlots.Length+" query-pages="+(slots.Length-alignmentSlots.Length));
#endif
                    }
                    finally{NativeNpcMotionTrace.Clear();NativeTileBoundary.End();NativeEntityDirectory.End();Main.tileSolid[379]=oldSolid379;}
                    // Player continuation is a dependency timeline, not another
                    // combat target. It permits alignment to observed movement
                    // and direct comparison with an independent native oracle.
                    writer.Write(playerCount);foreach(int slot in playerSlots)writer.Write(slot);foreach(float value in playerFuture)writer.Write(value);
                    dependencies.WriteTo(writer);
                    if(withAlignment){proof.Flush();Alignment=proofBytes.ToArray();}
                    writer.Write(PredictionPipeProtocol.Measure?Stopwatch.GetTimestamp()-started:0);writer.Write(Stopwatch.Frequency);
                    writer.Write(reset-started);writer.Write(restored-reset);
                    writer.Flush();return result.ToArray();
                }
            }
        }
        private void Reset(int width,int height)
        {
            Main.rand=new UnifiedRandom(123);Main.dedServ=true;Main.netMode=0;Main.gameMenu=false;Main.gamePaused=false;
            Main.maxTilesX=width;Main.maxTilesY=height;Main.leftWorld=Main.topWorld=0;Main.rightWorld=width*16;Main.bottomWorld=height*16;
            Main.screenWidth=960;Main.screenHeight=640;
            Main.sittingManager=new Terraria.DataStructures.AnchoredEntitiesCollection();
            for(int i=0;i<Main.npc.Length;i++)Main.npc[i]=new NPC();
            // Player construction creates inventories/equipment/banks. Only
            // previously restored players can carry request-specific state;
            // unused inactive slots stay initialized without rebuilding 256
            // complete inventories for every single-target forecast.
            for(int i=0;i<Main.player.Length;i++)
                if(Main.player[i]==null || i<restoredPlayers.Length && restoredPlayers[i]){Main.player[i]=new Player();if(i<restoredPlayers.Length)restoredPlayers[i]=false;}
            for(int i=0;i<Main.projectile.Length;i++)Main.projectile[i]=new Projectile();
            for(int i=0;i<Main.dust.Length;i++)Main.dust[i]=new Dust();
            for(int i=0;i<Main.gore.Length;i++)Main.gore[i]=new Gore();
            for(int i=0;i<Main.item.Length;i++)Main.item[i]=new WorldItem();
            for(int i=0;i<Main.combatText.Length;i++)Main.combatText[i]=new CombatText();
            // ClearWorld acknowledges an empty allocation premise even when
            // no next request arrives to overwrite the previous scalar.
            Dust.dCount=0;
            NPC.ClearFoundActiveNPCs();NPC.mechQueen=NPC.golemBoss=-1;
            Main.remixWorld=Main.getGoodWorld=Main.zenithWorld=false;
        }
        private static void ValidateSnapshot(NPC npc,int slot)
        {
            // Original NPC targets also encode NPC slots as 300+slot. These
            // are used by crystal defence and NPC-versus-NPC targeting.
            if(npc.type<0 || npc.type>=Terraria.ID.NPCID.Count || npc.active && (npc.whoAmI!=slot || npc.life<=0 || npc.type==0))
                throw new InvalidDataException("Invalid native snapshot identity.");
            // Inactive pool pages are observed history, not living targets.
            // Native reuse decides which fields to reset; do not normalize
            // their old values or invent a new constructor state here.
            if(!npc.active)return;
            bool targetValid=npc.target>=0 && npc.target<=Main.maxPlayers || npc.SupportsNPCTargets && npc.target>=300 && npc.target<300+Main.maxNPCs;
            if(!targetValid)throw new InvalidDataException("Invalid native target.");
            if(npc.ai==null || npc.ai.Length!=4 || npc.localAI==null || npc.localAI.Length!=4 || npc.buffType==null || npc.buffType.Length!=NPC.maxBuffs || npc.buffTime==null || npc.buffTime.Length!=NPC.maxBuffs || npc.width<1 || npc.height<1)
                throw new InvalidDataException("Invalid native snapshot shape.");
        }
        private static int Count(BinaryReader reader,int minimum,int maximum)
        {int value=reader.ReadInt32();if(value<minimum || value>maximum)throw new InvalidDataException("Invalid bounded count.");return value;}
        private static bool Finite(double value){return !double.IsNaN(value)&&!double.IsInfinity(value);}
        internal static byte TargetEnd(NPC current,NPC selected,byte generation,byte prior)
        {return prior!=0?prior:!ReferenceEquals(current,selected)?(byte)2:selected.generation!=generation?(byte)3:!selected.active?(byte)1:(byte)0;}
        private static void RecordPlayers(int[] slots,float[] values,int step)
        {
            int index=step*slots.Length*4;
            foreach(int slot in slots){var p=Main.player[slot];values[index++]=p.position.X;values[index++]=p.position.Y;values[index++]=p.velocity.X;values[index++]=p.velocity.Y;}
        }
        private void WritePoint(BinaryWriter writer,NPC npc,byte generation,byte ended,int step)
        {
            if(!Finite(npc.position.X)||!Finite(npc.position.Y)||!Finite(npc.velocity.X)||!Finite(npc.velocity.Y))throw new InvalidDataException("Nonfinite native future.");
            writer.Write(step);writer.Write(npc.type);writer.Write(ended==0);writer.Write(npc.position.X);writer.Write(npc.position.Y);
            writer.Write(npc.velocity.X);writer.Write(npc.velocity.Y);writer.Write(npc.width);writer.Write(npc.height);writer.Write(npc.ai[0]);writer.Write(npc.life);
            writer.Write(npc.frame.X);writer.Write(npc.frame.Y);writer.Write(npc.frame.Width);writer.Write(npc.frame.Height);writer.Write(npc.frameCounter);
            writer.Write(playerMotion.Quality|(NativeLightingSnapshot.HasValues?8:0));
            // 0 alive, 1 inactive, 2 slot replaced, 3 generation changed.
            // netID is future form; generation always identifies this birth.
            writer.Write(npc.netID);writer.Write(generation);writer.Write(ended);
        }
        public void Dispose(){terrain.Dispose();effects.Dispose();}
    }
}
