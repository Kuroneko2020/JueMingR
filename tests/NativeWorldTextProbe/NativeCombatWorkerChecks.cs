using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Utilities;

namespace NativeWorldTextProbe
{
    // Independent original-update oracle: no product Composition Root or
    // Harmony patches, and the whole future is received before time advances.
    internal static class NativeCombatWorkerChecks
    {
        internal const int ExpectedProtocol=25, PointBytes=75;
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Run(string output,bool catalogue=false,bool linkedOnly=false,bool lifetimeOnly=false,bool assetsOnly=false,bool randomOnly=false,bool playerOnly=false,bool entityOnly=false,bool birthOnly=false,bool contextOnly=false,bool immunityOnly=false,bool lifecycleOnly=false,bool fieldsOnly=false,bool transportOnly=false,bool productionOnly=false,string content=null,bool preparationOnly=false,bool menuOnly=false,bool snapshotOnly=false,bool longCoverageOnly=false,bool legalOnly=false)
        {
            output=Path.GetFullPath(output);
            Directory.CreateDirectory(output);
            string build=Environment.GetEnvironmentVariable("JUEMINGR_NPC_WORKER_BUILD")??Path.Combine(Program.Repository,"artifacts/build/Debug/work/bin");
            string configuration=Environment.GetEnvironmentVariable("JUEMINGR_NPC_CONFIGURATION")??"Debug";
            if(configuration!="Debug" && configuration!="Release")throw new InvalidOperationException("Unknown prediction build configuration.");
            Console.WriteLine("BUILD prediction-configuration="+configuration);
            string reuse=Environment.GetEnvironmentVariable("JUEMINGR_NPC_WORKER_LAYOUT");
            // Formal checks in one checks root share only an exact verified
            // payload layout and authenticated disk materials. Each scope
            // still owns a fresh process and isolated user/world data.
            string shared=content=="--cpu"?Path.Combine(Path.GetDirectoryName(output),"prediction-worker-layout"):null;
            if(reuse==null && shared!=null && Directory.Exists(shared))reuse=shared;
            string layout=reuse??shared??Path.Combine(output,"worker-layout-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(layout);
            foreach(string assembly in new[]{"JueMingR.PredictionWorker","JueMingR.TerrariaHost","JueMingR.Platform","JueMingR.Features","JueMingR.Infrastructure"})
            {
                string suffix=assembly.EndsWith("PredictionWorker",StringComparison.Ordinal)?".exe":".dll";
                string source=Path.Combine(build,assembly,"x86",configuration,"net472",assembly+suffix);
                Stage(source,Path.Combine(layout,assembly+suffix),reuse!=null);
                if(suffix==".exe")Stage(source+".config",Path.Combine(layout,assembly+suffix+".config"),reuse!=null);
            }
            Stage(Path.Combine(Program.Repository,"external/Harmony/0Harmony.dll"),Path.Combine(layout,"0Harmony.dll"),reuse!=null);
            var host=Assembly.LoadFrom(Path.Combine(layout,"JueMingR.TerrariaHost.dll"));
            foreach(var component in new[]{host,typeof(JueMingR.Platform.Combat.NpcTrajectory).Assembly,typeof(JueMingR.Features.Combat.NpcPredictionCache).Assembly,Assembly.Load("JueMingR.Infrastructure")})
            {
                var debug=component.GetCustomAttribute<DebuggableAttribute>();
                if(configuration=="Release")Require(debug==null || !debug.IsJITOptimizerDisabled,"actual product component is optimized Release: "+component.FullName);
                Console.WriteLine("RUNTIME "+component.GetName().Name+" mvid="+component.ManifestModule.ModuleVersionId+" optimized="+(debug==null || !debug.IsJITOptimizerDisabled));
            }
            if(productionOnly || menuOnly)
            {
                Terraria.Program.SavePath=Path.Combine(output,"isolated-user");
                string cached=Path.Combine(Terraria.Program.SavePath,"composition/JueMingRData/cache/npc-prediction");Directory.CreateDirectory(cached);
                // Repeat measurements may reuse exact authenticated prepared
                // materials. Product still selects its normal gameDirectory
                // cache path; no alternate sampling or publication seam.
                if(reuse!=null)
                {
                    string prepared=Path.Combine(layout,"prediction-materials");
                    if(Directory.Exists(prepared))foreach(string file in Directory.EnumerateFiles(prepared,"*.image"))File.Copy(file,Path.Combine(cached,Path.GetFileName(file)),true);
                }
                string hash;using(var input=File.OpenRead(host.Location))using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(input)).Replace("-","");
                try{NativeQuickItemChecks.Run(context=>{if(menuOnly)NativeCombatMenuPreparationChecks.Run(context,output);else NativeCombatProductionPredictionChecks.Run(context,output,content);},processing:true,shortFeedback:true,candidateAssembly:host.Location,predictionHostHash:hash);}
                finally
                {
                    string prepared=Path.Combine(layout,"prediction-materials");Directory.CreateDirectory(prepared);
                    foreach(string file in Directory.EnumerateFiles(cached,"*.image"))File.Copy(file,Path.Combine(prepared,Path.GetFileName(file)),true);
                }
                return;
            }
            var wire=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true);
            var capture=wire.GetMethod("Capture",Flags);
            if(lifetimeOnly){NativeCombatWorkerBoundaryChecks.ParentExit(layout);return;}
            Initialize();
            if(legalOnly || Environment.GetEnvironmentVariable("JUEMINGR_NPC_LEGAL_COVERAGE")=="1"){NativeCombatLegalCoverageChecks.Run(host,layout,output);return;}
            if(snapshotOnly){NativeCombatSnapshotChecks.Run(host);return;}
            if(longCoverageOnly){NativeCombatLongCoverageChecks.Run(host,layout,output);return;}
            if(preparationOnly){NativeCombatWorkerPreparationChecks.Run(host,layout,output);return;}
            if(transportOnly){NativeCombatWorkerTransportChecks.Run(host,layout,output);return;}
            if(fieldsOnly){NativeCombatWorkerFieldChecks.Run(host);return;}
            if(lifecycleOnly){NativeCombatWorkerLifecycleChecks.Run(host,layout,output);return;}
            if(contextOnly){NativeCombatWorkerContextChecks.Run(host);NativeCombatWorkerTagChecks.RemoteTerrain(host,layout,output);return;}
            if(immunityOnly){NativeCombatWorkerImmunityChecks.Run(host,layout,output);return;}
            if(birthOnly){NativeCombatWorkerBirthChecks.Run(host,layout,output);return;}
            if(entityOnly){NativeCombatWorkerEntityChecks.Run(host,layout,output);return;}
            if(playerOnly){NativeCombatWorkerPlayerChecks.Run(host,layout,output);return;}
            if(randomOnly){RandomScene(host,layout,output);return;}
            if(assetsOnly){NativeCombatWorkerAssetChecks.Run(host,layout,output);NativeCombatWorkerAssetChecks.GoreMetadata(host);return;}
            if(linkedOnly){NativeCombatWorkerLinkedChecks.Run(host,layout,output);return;}
            if(!catalogue)NativeCombatTileManifestChecks.Run(output,false);
            if(catalogue){Catalogue(layout,capture,output);return;}
            foreach(bool linked in new[]{false,true})
            {
                int[] slots=Scene(linked);int selected=linked?1:0;
                byte[] snapshot;
                string randomBefore=RandomStamp();
                var watch=Stopwatch.StartNew();
                byte[] future;
                using(var child=Start(layout))
                {
                    Task<string> errors=child.StandardError.ReadToEndAsync();
                    FrozenScene acquired;
                    try{acquired=AcquireFrozen(host,child,slots,new int[0],selected);}
                    catch{if(!child.HasExited)child.Kill();child.WaitForExit(5000);if(errors.Wait(5000))Console.Error.WriteLine(errors.Result);throw;}
                    snapshot=acquired.Snapshot;future=acquired.Future;
                    Console.WriteLine("FIRST result-ms="+watch.Elapsed.TotalMilliseconds.ToString("F2"));
                    byte[] repeat=Exchange(child,snapshot);
                    Require(future.Take(future.Length-32).SequenceEqual(repeat.Take(repeat.Length-32)),"same worker resets independent requests");
                    Timing(future,"cold");Timing(repeat,"warm");child.Refresh();Console.WriteLine("WORKER private-bytes="+child.PrivateMemorySize64+" cpu-ms="+child.TotalProcessorTime.TotalMilliseconds);
                    Exit(child,"EOF exits owned helper");
                    Require(child.ExitCode==0,"worker success: "+errors.Result);
                    Console.WriteLine(errors.Result.Trim());
                    Console.WriteLine("WORKER scene="+(linked?"Skeletron head+hands":"Harpy")+" frame="+snapshot.Length+" cold-two-requests-ms="+watch.Elapsed.TotalMilliseconds.ToString("F2"));
                }
                Require(randomBefore==RandomStamp(),"child leaves parent native RNG unchanged");
                File.WriteAllBytes(Path.Combine(output,linked?"skeletron-frozen.bin":"harpy-frozen.bin"),future);
                Compare(future,selected,output,linked?"skeletron":"harpy");
            }
            NativeCombatWorkerBoundaryChecks.Terrain(host,layout,output);
            NativeCombatWorkerLinkedChecks.Run(host,layout,output);
            NativeCombatWorkerAssetChecks.Run(host,layout,output);
            RandomScene(host,layout,output);
            NativeCombatWorkerEntityChecks.Run(host,layout,output);
            NativeCombatWorkerContextChecks.Run(host);
            NativeCombatWorkerFieldChecks.Run(host);
            NativeCombatWorkerTagChecks.RemoteTerrain(host,layout,output);
            NativeCombatWorkerBirthChecks.Run(host,layout,output);
            NativeCombatWorkerImmunityChecks.Run(host,layout,output);
            NativeCombatWorkerLifecycleChecks.Run(host,layout,output);
            NativeCombatWorkerBoundaryChecks.ParentExit(layout);
            NativeCombatWorkerPlayerChecks.Run(host,layout,output);
            NativeCombatWorkerAssetChecks.GoreMetadata(host);
            NativeCombatWorkerBoundaryChecks.Effects(host);
            Console.WriteLine("PASS real owned helper / native NPC and player frozen 120-update scenarios / repeat / EOF / parent isolation and terrain/effect fences. Not all-type coverage or full client equivalence.");
        }
        private static void Stage(string source,string target,bool reuse)
        {
            if(!reuse){File.Copy(source,target);return;}
            using(var sha=SHA256.Create())using(var expected=File.OpenRead(source))using(var actual=File.OpenRead(target))
                Require(sha.ComputeHash(expected).SequenceEqual(sha.ComputeHash(actual)),"Reused fixture layout must exactly match the requested candidate: "+Path.GetFileName(target));
        }
        internal static Process Start(string layout)
        {
            // Framework Process creates a StreamWriter for redirected stdin
            // and may emit Console.InputEncoding's preamble before BaseStream
            // writes. This isolated test harness owns its console encoding;
            // the eventual live transport must use raw owned pipe handles.
            Console.InputEncoding=new UTF8Encoding(false);
            string host=Path.Combine(layout,"JueMingR.TerrariaHost.dll");string hash;
            using(var stream=File.OpenRead(host))using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
            var parent=Process.GetCurrentProcess();
            var start=new ProcessStartInfo(Path.Combine(layout,"JueMingR.PredictionWorker.exe"),"--pipes \""+Path.Combine(Program.Repository,"external/TerrariaRefs/Terraria.exe")+"\" "+hash+" "+parent.Id+" "+parent.StartTime.ToUniversalTime().Ticks)
            {UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=layout,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(string key in new[]{"APPDOMAIN_MANAGER_ASM","APPDOMAIN_MANAGER_TYPE","COMPLUS_Version","COMPLUS_ApplicationMigrationRuntimeActivationConfigPath","COR_ENABLE_PROFILING","COR_PROFILER","COR_PROFILER_PATH"})start.EnvironmentVariables.Remove(key);
            return Process.Start(start);
        }
        internal sealed class FrozenScene
        {internal byte[] Snapshot,Future;internal int[] Npcs,Projectiles;}
        internal static FrozenScene AcquireFrozen(Assembly host,Process child,int[] npcs,int[] projectiles,int selected,Rectangle? terrain=null,bool sparse=false,int horizon=120)
        {
            var capture=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true).GetMethod(sparse?"CaptureProduction":terrain.HasValue?"CaptureSceneRegion":"CaptureScene",Flags);
            var npcPages=new SortedSet<int>(npcs);var projectilePages=new SortedSet<int>(projectiles);
            var chunks=new SortedSet<int>();
            if(sparse)foreach(var center in npcs.Select(i=>Main.npc[i].Center).Concat(projectiles.Select(i=>Main.projectile[i].Center)).Concat(Main.player.Where(p=>p.active).Select(p=>p.Center)))
                for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++){int cx=(int)center.X/512+dx,cy=(int)center.Y/512+dy;if(cx>=0 && cy>=0 && cx*32<Main.maxTilesX && cy*32<Main.maxTilesY)chunks.Add(cx*128+cy);}
            // Fixture-only dependency discovery: the original scene has not
            // advanced at all, so each retry is a new complete capture of the
            // same frozen instant. A live producer must resample a new instant,
            // never splice later pages into an older request.
            for(int attempt=0;attempt<=Main.maxNPCs+Main.maxProjectiles+2;attempt++)
            {
                int[] n=npcPages.ToArray(),p=projectilePages.ToArray();
                object[] args=terrain.HasValue?new object[]{n,p,selected,1000L,horizon,1L,terrain.Value.Left,terrain.Value.Top,terrain.Value.Right-1,terrain.Value.Bottom-1,false}:new object[]{n,p,selected,1000L,horizon};
                if(sparse){object tiles=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeTerrainSnapshot",true).GetMethod("CaptureChunks",Flags).Invoke(null,new object[]{1L,chunks});args=new object[]{n,p,selected,1000L,horizon,tiles,null,false};}
                byte[] request=(byte[])capture.Invoke(null,args);
                byte[] response=Exchange(child,request);
                Require(request.SequenceEqual((byte[])capture.Invoke(null,args)),"child leaves every captured parent scene value unchanged");
                using(var reader=new BinaryReader(new MemoryStream(response,false)))
                {
                    int protocol=reader.ReadInt32();
                    if(protocol==ExpectedProtocol)
                    {Console.WriteLine("DEPENDENCIES retries="+attempt+" npc-pages="+n.Length+" projectile-pages="+p.Length);return new FrozenScene{Snapshot=request,Future=response,Npcs=n,Projectiles=p};}
                    Require(protocol==-ExpectedProtocol,"Compatible refusal protocol.");reader.ReadString();string reason=reader.ReadString();int tileX=reader.ReadInt32(),tileY=reader.ReadInt32();
                    int kind=reader.ReadInt32(),slot=reader.ReadInt32();reader.ReadInt32();
                    bool added=kind==1 && slot>=0 && slot<=Main.maxNPCs?npcPages.Add(slot):kind==2 && slot>=0 && slot<=Main.maxProjectiles && projectilePages.Add(slot);
                    if(!added && sparse && tileX>=0 && tileY>=0 && tileX<Main.maxTilesX && tileY<Main.maxTilesY)added=chunks.Add(tileX/32*128+tileY/32);
                    if(!added)throw new InvalidOperationException("Frozen native scene refused without a new dependency: "+reason);
                    Console.WriteLine("DEPENDENCY kind="+kind+" slot="+slot);
                }
            }
            throw new InvalidOperationException("Bounded native dependency discovery exhausted.");
        }
        internal static byte[] Exchange(Process process,byte[] payload)
        {
            var transfer=Task.Run(()=>
            {
                var writer=new BinaryWriter(process.StandardInput.BaseStream,Encoding.UTF8,true);writer.Write(payload.Length);writer.Write(payload);writer.Flush();
                var reader=new BinaryReader(process.StandardOutput.BaseStream,Encoding.UTF8,true);int size=reader.ReadInt32();Require(size>0 && size<4194304,"bounded result");
                byte[] bytes=reader.ReadBytes(size);Require(bytes.Length==size,"complete result");return bytes;
            });
            // This bound includes private-image/JIT startup, not a product
            // latency target. Observed cold starts exceeded 30s before any AI;
            // keep cold cost visible rather than mislabel that as an AI RED.
            try {if(!transfer.Wait(60000)){if(!process.HasExited)process.Kill();throw new TimeoutException("Owned fixture helper did not answer within the 60s fixture bound.");}return transfer.Result;}
            catch {if(!process.HasExited)process.Kill();process.WaitForExit(5000);throw;}
        }
        internal static void Exit(Process child,string context)
        {
            child.StandardInput.Close();
            if(child.WaitForExit(15000))return;
            // Process.Start retains the owned process handle. Never read a
            // still-open stderr Task.Result after an exit timeout.
            if(!child.HasExited)child.Kill();
            child.WaitForExit(5000);
            throw new TimeoutException(context);
        }
        private static void Initialize()
        {
            Main.dedServ=true;Main.netMode=0;Main.myPlayer=0;Main.gameMenu=Main.gamePaused=false;
            Main.ActiveWorldFileData=new Terraria.IO.WorldFileData();
            Main.rand=new UnifiedRandom(123);
            typeof(Main).GetMethod("Initialize_TileAndNPCData1",Flags).Invoke(null,null);
            typeof(Main).GetMethod("Initialize_TileAndNPCData2",Flags).Invoke(null,null);
            NativeCombatWorkerAssetChecks.Initialize();
        }
        internal static int[] Scene(bool linked)
        {
            Main.rand=new UnifiedRandom(123);Main.maxTilesX=Main.maxTilesY=120;Main.tile=new Tile[120,120];
            for(int x=0;x<120;x++)for(int y=0;y<120;y++)Main.tile[x,y]=new Tile();
            Main.tileSolid[1]=true;for(int x=0;x<120;x++)for(int y=65;y<120;y++){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}
            Main.leftWorld=Main.topWorld=0;Main.rightWorld=Main.bottomWorld=1920;Main.screenWidth=960;Main.screenHeight=640;
            Main.worldSurface=60;Main.rockLayer=80;Main.dayTime=false;Main.time=1800;Main.GameMode=0;Main.bloodMoon=Main.eclipse=false;Main.windSpeedCurrent=0;
            for(int i=0;i<Main.npc.Length;i++)Main.npc[i]=new NPC();
            for(int i=0;i<Main.player.Length;i++)Main.player[i]=new Player();
            for(int i=0;i<Main.projectile.Length;i++)Main.projectile[i]=new Projectile();
            for(int i=0;i<Main.dust.Length;i++)Main.dust[i]=new Dust();
            for(int i=0;i<Main.gore.Length;i++)Main.gore[i]=new Gore();
            for(int i=0;i<Main.item.Length;i++)Main.item[i]=new WorldItem();
            Main.player[0].active=true;Main.player[0].whoAmI=0;Main.player[0].position=new Vector2(800,65*16-Main.player[0].height);Main.player[0].statLife=Main.player[0].statLifeMax2=400;
            NPC.ClearFoundActiveNPCs();NPC.mechQueen=NPC.golemBoss=-1;
            typeof(Main).GetField("_gameUpdateCount",Flags).SetValue(null,1000U);
            if(!linked){SetNpc(0,48,480,480);Main.npc[0].velocity=new Vector2(2,-1);return new[]{0};}
            SetNpc(0,35,700,400);Main.npc[0].ai[0]=1;
            SetNpc(1,36,500,600);Main.npc[1].ai[0]=-1;Main.npc[1].ai[1]=0;
            SetNpc(2,36,900,600);Main.npc[2].ai[0]=1;Main.npc[2].ai[1]=0;
            return new[]{0,1,2};
        }
        private static void SetNpc(int slot,int type,float x,float y)
        {var n=Main.npc[slot];n.SetDefaults(type);n.whoAmI=slot;n.active=true;n.position=new Vector2(x,y);n.target=0;n.timeLeft=750;}
        internal static void Compare(byte[] frozen,int selected,string output,string name,int[] projectiles=null,bool nativeStreams=false,Action playerUpdate=null,Action projectileUpdate=null,Action<byte[],byte[]> dependencyComparison=null,bool expectPlayerMotion=true,Action<int,NPC> nativeStep=null,float motionTolerance=0)
        {
            using(var stream=new MemoryStream(frozen))using(var reader=new BinaryReader(stream))using(var log=new StreamWriter(Path.Combine(output,name+"-oracle.csv"),false,Encoding.UTF8))
            using(var nativeDependencies=new MemoryStream())using(var dependencyWriter=new BinaryWriter(nativeDependencies))
            {
                int protocol=reader.ReadInt32();if(protocol<0)throw new InvalidOperationException("Worker refused oracle: "+reader.ReadString()+": "+reader.ReadString());
                Require(protocol==ExpectedProtocol && reader.ReadInt64()==1000 && reader.ReadInt32()==selected && reader.ReadInt32()==121,"result identity and 120-step horizon");
                log.WriteLine("tick,predictedX,predictedY,nativeX,nativeY,error");
                float worst=0;double sum=0;
                NPC capturedTarget=Main.npc[selected];byte capturedGeneration=capturedTarget.generation,expectedEnd=0;
                int[] playerSlots=Enumerable.Range(0,Main.maxPlayers).Where(slot=>Main.player[slot].active).ToArray();var playerFuture=new float[121*playerSlots.Length*4];
                for(int i=0;i<=120;i++)
                {
                    if(i>0)
                    {
                        typeof(Main).GetField("_gameUpdateCount",Flags).SetValue(null,(uint)(1000+i));NPC.UpdateProtectedSpawnSlots();NPC.ClearFoundActiveNPCs();NPC.UpdateFoundActiveNPCs();
                        playerUpdate?.Invoke();
                        // Main.UpdateWorld_NPCs retires a stale gravity source
                        // after players have consumed this tick's old index.
                        if(NPC.brainOfGravity>=0 && NPC.brainOfGravity<Main.maxNPCs && (!Main.npc[NPC.brainOfGravity].active || Main.npc[NPC.brainOfGravity].type!=266))NPC.brainOfGravity=-1;
                        using(nativeStreams?Main.SwapRandom("UpdateNPCs"):null)
                            for(int slot=0;slot<Main.maxNPCs;slot++)if(Main.npc[slot].active)Main.npc[slot].UpdateNPC(slot);
                        using(nativeStreams?Main.SwapRandom("UpdateProjectiles"):null)
                        try
                        {if(projectileUpdate!=null)projectileUpdate();else for(int slot=0;slot<Main.maxProjectiles;slot++){Main.ProjectileUpdateLoopIndex=slot;if(Main.projectile[slot].active)Main.projectile[slot].Update(slot);}}
                        finally{Main.ProjectileUpdateLoopIndex=-1;}
                        Main.time+=Main.dayRate;
                        if(Terraria.GameContent.Events.DD2Event.Ongoing)using(nativeStreams?Main.SwapRandom("UpdateTime"):null)Terraria.GameContent.Events.DD2Event.UpdateTime();
                    }
                    Require(reader.ReadInt32()==i,"future point tick");int type=reader.ReadInt32();bool active=reader.ReadBoolean();
                    var point=new Vector2(reader.ReadSingle(),reader.ReadSingle());var velocity=new Vector2(reader.ReadSingle(),reader.ReadSingle());
                    int width=reader.ReadInt32(),height=reader.ReadInt32();float phase=reader.ReadSingle();int life=reader.ReadInt32();NPC actual=capturedTarget;
                    var frame=new Rectangle(reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32());double frameCounter=reader.ReadDouble();
                    int playerQuality=reader.ReadInt32();if(playerUpdate!=null && i>0)Require(((playerQuality&1)!=0)==expectPlayerMotion,"player motion quality matches the native fixture movement premise");
                    int netId=reader.ReadInt32();byte generation=reader.ReadByte(),ended=reader.ReadByte();
                    if(expectedEnd==0)
                        expectedEnd=!ReferenceEquals(Main.npc[selected],capturedTarget)?(byte)2:capturedTarget.generation!=capturedGeneration?(byte)3:!capturedTarget.active?(byte)1:(byte)0;
                    Require(netId==actual.netID && generation==capturedGeneration && ended==expectedEnd,name+" native birth/form/end mismatch tick="+i);
                    Require(frame==actual.frame && frameCounter==actual.frameCounter,name+" native frame mismatch tick="+i);
                    float error=Vector2.Distance(point,actual.position);sum+=error;worst=Math.Max(worst,error);
                    log.WriteLine(i+","+point.X+","+point.Y+","+actual.position.X+","+actual.position.Y+","+error);
                    Require(error<=motionTolerance && Vector2.Distance(velocity,actual.velocity)<=motionTolerance && type==actual.type && active==(expectedEnd==0) && phase==actual.ai[0] && life==actual.life && width==actual.width && height==actual.height,name+" frozen original mismatch tick="+i+" error="+error+" native-life="+actual.life+" predicted-life="+life+" velocity="+velocity.X.ToString("R")+"/"+velocity.Y.ToString("R")+" native="+actual.velocity.X.ToString("R")+"/"+actual.velocity.Y.ToString("R")+" type="+type+"/"+actual.type+" active="+active+"/"+actual.active+" phase="+phase+"/"+actual.ai[0]+" size="+width+"x"+height+"/"+actual.width+"x"+actual.height);
                    int at=i*playerSlots.Length*4;foreach(int slot in playerSlots){var p=Main.player[slot];playerFuture[at++]=p.position.X;playerFuture[at++]=p.position.Y;playerFuture[at++]=p.velocity.X;playerFuture[at++]=p.velocity.Y;}
                    NativeCombatWorkerBirthChecks.Record(dependencyWriter,i);
                    nativeStep?.Invoke(i,capturedTarget);
                }
                Require(reader.ReadInt32()==playerSlots.Length,"player dependency count");foreach(int slot in playerSlots)Require(reader.ReadInt32()==slot,"player dependency identity");
                // Original x86 Release methods and the explicit continuation
                // can round intermediate floats differently. Bound the complete
                // frozen dependency drift to 0.002px, far below a game pixel;
                // NPC state/branch comparisons above remain exact.
                float dependencyError=0;
                using(var dependencyLog=new StreamWriter(Path.Combine(output,name+"-players.csv")))
                for(int at=0;at<playerFuture.Length;at++){float value=reader.ReadSingle();float difference=Math.Abs(value-playerFuture[at]);dependencyError=Math.Max(dependencyError,difference);dependencyLog.WriteLine(at+","+value.ToString("R")+","+playerFuture[at].ToString("R"));Require(difference<=0.002f,name+" player dependency mismatch tick="+(at/(playerSlots.Length*4))+" component="+(at%4)+" predicted="+value.ToString("R")+" native="+playerFuture[at].ToString("R"));}
                dependencyWriter.Flush();NativeCombatWorkerBirthChecks.Compare(reader,nativeDependencies.ToArray(),output,name,dependencyComparison);
                double milliseconds=reader.ReadInt64()*1000.0/reader.ReadInt64();reader.ReadInt64();reader.ReadInt64();Require(stream.Position==stream.Length,"no extra result data");
                Console.WriteLine("ORACLE "+name+" ticks=120 mean="+(sum/121)+" max="+worst+" player-component-max="+dependencyError.ToString("R")+" child-snapshot-and-native-ms="+milliseconds.ToString("F3"));
            }
        }
        internal static string RandomStamp()
        {return string.Join(";",typeof(UnifiedRandom).GetFields(Flags).Where(f=>!f.IsStatic).Select(f=>{object v=f.GetValue(Main.rand);return v is int[]?string.Join(",",(int[])v):Convert.ToString(v);}));}
        private static void RandomScene(Assembly host,string layout,string output)
        {
            Scene(false);NPC.ClearAll();
            int selected=NPC.NewNPC(new Terraria.DataStructures.EntitySource_DebugCommand(),480,480,371);
            if(selected>=Main.maxNPCs || Main.npc[selected].target!=255)throw new InvalidOperationException("Invalid native bubble spawn.");
            var field=typeof(Main).GetField("_rngs",Flags);object old=field.GetValue(null);
            var streams=new System.Collections.Generic.Dictionary<string,UnifiedRandom>{{"UpdateNPCs",new UnifiedRandom(789)}};
            field.SetValue(null,streams);
            try
            {
                // Advance a real keyed stream before capture. Resetting helper
                // RNG to a convenient seed must fail on the very first future.
                using(Main.SwapRandom("UpdateNPCs"))for(int i=0;i<17;i++)Main.rand.Next();
                string before;using(Main.SwapRandom("UpdateNPCs"))before=RandomStamp();
                var capture=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true).GetMethod("Capture",Flags);
                byte[] snapshot=(byte[])capture.Invoke(null,new object[]{new[]{selected},selected,1000L,120}),future;
                using(Main.SwapRandom("UpdateNPCs"))Require(before==RandomStamp(),"capture does not consume keyed RNG");
                Require(streams.Count==1,"capture does not create missing live random streams");
                using(var child=Start(layout))
                {var errors=child.StandardError.ReadToEndAsync();future=Exchange(child,snapshot);Exit(child,"random helper exits");File.WriteAllText(Path.Combine(output,"random-worker.log"),errors.Result);}
                Compare(future,selected,output,"bubble-warm-keyed-rng",nativeStreams:true);
            }
            finally{field.SetValue(null,old);}
            Console.WriteLine("PASS original spawned bubble with warmed keyed RNG, frozen 120-step future and capture purity.");
        }
        private static void Catalogue(string layout,MethodInfo capture,string output)
        {
            string subset=Environment.GetEnvironmentVariable("JUEMINGR_NPC_TYPES");var selectedTypes=string.IsNullOrEmpty(subset)?null:subset.Split(',').Select(int.Parse).ToArray();
            using(var child=Start(layout))using(var csv=new StreamWriter(Path.Combine(output,"native-worker-default-catalogue.csv"),false,Encoding.UTF8))
            {
                var errors=child.StandardError.ReadToEndAsync();int complete=0,failed=0;
                csv.WriteLine("type,aiStyle,friendly,lifeMax,activeAtEnd,result");
                for(int type=1;type<Terraria.ID.NPCID.Count;type++)
                {
                    if(selectedTypes!=null && !selectedTypes.Contains(type))continue;
                    Scene(false);SetNpc(0,type,480,480);var npc=Main.npc[0];
                    if(npc.type!=type || npc.friendly || npc.lifeMax<=0 || npc.dontTakeDamage){csv.WriteLine(type+","+npc.aiStyle+","+npc.friendly+","+npc.lifeMax+",,not-default-target");continue;}
                    byte[] snapshot=(byte[])capture.Invoke(null,new object[]{new[]{0},0,1000L,120});
                    byte[] result;
                    try{result=Exchange(child,snapshot);}catch{Console.Error.WriteLine(errors.Result);throw;}
                    using(var data=new MemoryStream(result))using(var reader=new BinaryReader(data))
                    {
                        if(reader.ReadInt32()<0){failed++;csv.WriteLine(type+","+npc.aiStyle+","+npc.friendly+","+npc.lifeMax+",,\""+(reader.ReadString()+": "+reader.ReadString()).Replace("\"","\"\"")+"\"");}
                        else{complete++;data.Position=20+120*PointBytes+8;bool active=reader.ReadBoolean();csv.WriteLine(type+","+npc.aiStyle+","+npc.friendly+","+npc.lifeMax+","+active+",native-120");}
                    }
                    csv.Flush();if(type%50==0)Console.WriteLine("CATALOGUE at="+type+" complete="+complete+" failed="+failed+" (default states only)");
                }
                Exit(child,"catalogue helper EOF");Require(child.ExitCode==0,"catalogue helper success");Console.WriteLine(errors.Result.Trim());
                Console.WriteLine("CATALOGUE complete="+complete+" failed="+failed+". Default states are NOT legal-scene coverage or accuracy evidence.");
            }
        }
        private static void Timing(byte[] bytes,string name)
        {
            Require(BitConverter.ToInt32(bytes,0)==ExpectedProtocol,"Only a successful timeline has timing fields.");
            using(var stream=new MemoryStream(bytes))using(var reader=new BinaryReader(stream))
            {stream.Position=bytes.Length-32;long total=reader.ReadInt64(),frequency=reader.ReadInt64(),reset=reader.ReadInt64(),restore=reader.ReadInt64();Console.WriteLine("TIMING "+name+" reset-ms="+(reset*1000.0/frequency).ToString("F3")+" restore-ms="+(restore*1000.0/frequency).ToString("F3")+" native-ms="+((total-reset-restore)*1000.0/frequency).ToString("F3"));}
        }
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
