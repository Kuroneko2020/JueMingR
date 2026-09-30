using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Utilities;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatProductionPredictionChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static readonly List<double> warm=new List<double>();
        private static readonly List<double> cadence=new List<double>();
        private static readonly List<double> active=new List<double>(),displayAge=new List<double>();
        private static readonly List<string> updates=new List<string>();
        private static long priorStep; private static string phase;
        private static int continuousSeconds;
        private static readonly List<string> measurementRows=new List<string>();
        private static Microsoft.Win32.SafeHandles.SafeWaitHandle cadenceTimer;
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern Microsoft.Win32.SafeHandles.SafeWaitHandle CreateWaitableTimerEx(IntPtr attributes,string name,uint flags,uint access);
        [DllImport("kernel32.dll",SetLastError=true)]private static extern bool SetWaitableTimer(Microsoft.Win32.SafeHandles.SafeWaitHandle timer,ref long due,int period,IntPtr callback,IntPtr argument,bool resume);
        [DllImport("kernel32.dll")]private static extern uint WaitForSingleObject(Microsoft.Win32.SafeHandles.SafeWaitHandle timer,uint milliseconds);
        [DllImport("winmm.dll")]private static extern uint timeBeginPeriod(uint period);
        [DllImport("winmm.dll")]private static extern uint timeEndPeriod(uint period);
        internal static void Run(object context,string output,string content)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");
            var native=GetOptional(source,"Native");Require(native!=null,"Authenticated production composition selects native owner");
            Codec(host.GetType().Assembly);warm.Clear();cadence.Clear();priorStep=0;
            active.Clear();displayAge.Clear();updates.Clear();
            measurementRows.Clear();int.TryParse(Environment.GetEnvironmentVariable("JUEMINGR_NPC_CONTINUOUS_SECONDS"),out continuousSeconds);if(content=="--cpu" && continuousSeconds==0)continuousSeconds=1;if(continuousSeconds<0 || continuousSeconds>60)throw new InvalidOperationException("Bounded measurement window required.");
            bool timer=timeBeginPeriod(1)==0;
            cadenceTimer=CreateWaitableTimerEx(IntPtr.Zero,null,2,0x1F0003);if(cadenceTimer.IsInvalid)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            var cache=(NpcPredictionCache)Get(source,"Cache");object worker=null;
            var sink=new Harmony("JueMingR.Tests.NativeProductionOutlets");
            var samples=new List<double>();var prepares=new List<double>();var draws=new List<double>();
            try
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());
                for(int i=0;i<60;i++){Call(context,"UpdateRuntime");Call(context,"UpdateShell");}
                Require(GetOptional(native,"Worker")==null && (long)Get(native,"Requests")==0,"OFF entry never starts or samples helper");
                using(var graphics=continuousSeconds>0?null:new ProbeGraphics(content))
                {
                    Initialize();
                    NativeCombatHistoryChecks.Start(sink,host.GetType().Assembly,output);
                    foreach(string name in new[]{"HandleSpecialEvent","HandleRunning"})Patch(sink,typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),nameof(Skip));
                    foreach(var method in new[]{typeof(WorldGen).GetMethod("saveToonWhilePlaying",Flags),typeof(Player).GetMethod("SavePlayer",Flags),typeof(NetMessage).GetMethod("SendData",Flags)})Patch(sink,method,nameof(Refuse));
                    Set(host,"LayerStatus",Enum.Parse(host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Rendering.WorldLayerStatus"),"Ready"));
                    if(continuousSeconds>0){Scene(2);Window(context,cache,samples,prepares,"baseline-off",continuousSeconds,false);Main.npc[0].active=false;}
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true));
                    phase="first-eye";Scene(2);long first=Stopwatch.GetTimestamp();int valid=0,moving=0,frames=0,pid=0,playerMoving=0;bool restartedMovement=false;
                    var waiting=Stopwatch.StartNew();
                    while(waiting.Elapsed.TotalSeconds<70 && valid<90)
                    {
                        Step(context,samples,prepares);frames++;worker=GetOptional(native,"Worker");
                        if(worker!=null && (double)Get(worker,"ReadyMilliseconds")>0 && !restartedMovement){restartedMovement=true;Main.LocalPlayer.position=new Vector2(640,70*16-Main.LocalPlayer.height);Main.LocalPlayer.velocity=Vector2.Zero;continue;}
                        if(worker!=null && (int)Get(worker,"ChildId")!=0){int actual=(int)Get(worker,"ChildId");if(pid==0)pid=actual;Require(pid==actual,"steady scene keeps one resident process");}
                        var path=cache.Read(0);
                        if(path!=null){Check(path,0);valid++;if(Main.LocalPlayer.velocity.X>0)playerMoving++;if(path[120].Bounds.X!=path[0].Bounds.X || path[120].Bounds.Y!=path[0].Bounds.Y)moving++;if(valid==1){Console.WriteLine("FIRST path wall-ms="+Ms(Stopwatch.GetTimestamp()-first).ToString("F3")+" age-ticks="+(Main.GameUpdateCount-path.CaptureTick));if(graphics!=null)Image(graphics,output,"production-eye",host,draws);phase="steady-"+Main.npc[path.Identity.Slot].type;Main.LocalPlayer.position=new Vector2(640,70*16-Main.LocalPlayer.height);Main.LocalPlayer.velocity=Vector2.Zero;}}
                        if(frames%120==0)Console.WriteLine("LIVE frames="+frames+" valid="+valid+" worker="+(worker==null?"none":Get(worker,"State").ToString())+" reason="+GetOptional(native,"Reason"));
                        if(worker!=null && (int)Get(worker,"State")==4)throw new InvalidOperationException("Production helper fault: "+GetOptional(worker,"Failure"));
                    }
                    Require(valid>=90 && moving>0 && playerMoving>5,"continuous original NPC and player movement publishes full future windows; moving-player="+playerMoving+" reason="+GetOptional(native,"Reason"));
                    cache.Demand(1,120);NativeCombatObservationChecks.Save(host,new ObservationOptions());
                    Require(cache.Read(1)!=null,"Path OFF cannot detach a real native timeline still requested by another consumer.");
                    for(int i=0;i<6;i++)Step(context,samples,prepares);
                    Require(cache.Read(1)!=null && (bool)Get(Get(host,"Selection"),"HasTarget"),"Remaining native consumer continues through actual Host updates.");
                    cache.Release(1);Call(host,"Poll");Require(!(bool)Get(Get(host,"Selection"),"HasTarget"),"Last native consumer retires selection after path was already OFF.");
                    long retiredRequests=(long)Get(native,"Requests");for(int i=0;i<6;i++)Step(context,samples,prepares);
                    Require((long)Get(native,"Requests")==retiredRequests && cache.Required==0,"No native requests follow final consumer retirement.");
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true));WaitPath(context,cache,samples,prepares,0,"shared-consumer-return");
                    if(Environment.GetEnvironmentVariable("JUEMINGR_NPC_LIVE_CONTEXT")!=null)
                    {
                        phase="live-context";
                        NativeCombatLiveContextChecks.Run(context,cache,()=>Step(context,samples,prepares,false),output);return;
                    }
                    if(Environment.GetEnvironmentVariable("JUEMINGR_NPC_MEASURE_OFF")=="1")
                    {
                        Require(((ICollection)Get(native,"Measurements")).Count==0 && (double)Get(native,"ObserveMilliseconds")==0 && (double)Get(worker,"ExchangeMilliseconds")==0,"Default product path must not collect detailed costs.");
                        var result=Get(native,"accepted");Require((double)Get(result,"TotalMs")==0 && (double)Get(result,"AdvanceMs")==0,"Worker step/request sampling is disabled as well.");
                        Console.WriteLine("PASS diagnostics OFF: real native windows, no measurement records or worker step timings");return;
                    }
                    if(Environment.GetEnvironmentVariable("JUEMINGR_NPC_PRODUCTION_BUFFS")=="1"){BuffProduction(context,cache,samples,prepares);return;}
                    if(Environment.GetEnvironmentVariable("JUEMINGR_NPC_PRODUCTION_WIND")=="1"){WindProduction(context,cache,samples,prepares);return;}
                    if(Environment.GetEnvironmentVariable("JUEMINGR_NPC_PRODUCTION_NUMERIC")=="1"){NumericProduction(context,cache,samples,prepares);return;}
                    bool swiftOnly=Environment.GetEnvironmentVariable("JUEMINGR_NPC_PRODUCTION_SWIFT")=="1",gearOnly=Environment.GetEnvironmentVariable("JUEMINGR_NPC_PRODUCTION_GEAR")=="1",largeOnly=Environment.GetEnvironmentVariable("JUEMINGR_NPC_PRODUCTION_LARGE")=="1";
                    if(swiftOnly || !gearOnly && !largeOnly)
                    {
                        foreach(int accessory in new[]{0,54,2423})
                        {
                            var player=Main.LocalPlayer;player.armor[3]=new Item();if(accessory!=0)player.armor[3].SetDefaults(accessory);
                            player.AddBuff(3,120);phase="swift-expiring-"+accessory;WaitPath(context,cache,samples,prepares,0,phase);Window(context,cache,samples,prepares,phase,3,true);
                            Require(!player.buffType.Contains(3),"Production window must cross the actual native buff expiry.");
                        }
                        Console.WriteLine("PASS production timed Swiftness through expiry with bare/Hermes/Frog movement");if(swiftOnly)return;
                    }
                    if(gearOnly || !largeOnly)
                    {
                        foreach(string gear in new[]{"boots","shared-boots","frog"})
                        {
                            var player=Main.LocalPlayer;player.armor[3]=new Item();player.Loadouts[0].Armor[3]=new Item();
                            Item equipped=gear=="shared-boots"?player.Loadouts[0].Armor[3]:player.armor[3];equipped.SetDefaults(gear=="frog"?2423:54);equipped.favorited=true;
                            Main.LocalPlayer.position=new Vector2(680,70*16-player.height);Main.LocalPlayer.velocity=Vector2.Zero;
                            phase="gear-"+gear;WaitPath(context,cache,samples,prepares,0,phase);Window(context,cache,samples,prepares,phase,3,true);
                            Require(gear=="frog"?player.autoJump && player.jumpSpeedBoost>0:player.accRunSpeed==6,"Original effective accessory must actually apply.");
                        }
                        Console.WriteLine("PASS production direct and shared effective equipment premises");if(gearOnly)return;
                        Main.LocalPlayer.armor[3]=new Item();Main.LocalPlayer.Loadouts[0].Armor[3]=new Item();
                        Step(context,samples,prepares);
                    }
                    if(largeOnly)
                    {
                        int start=samples.Count,intervalStart=cadence.Count;
                        phase="large-chain";NativeCombatLongCoverageChecks.Production(context,cache,intent=>
                        {
                            Main.LocalPlayer.controlRight=Main.GameUpdateCount%120<60;Main.LocalPlayer.controlLeft=!Main.LocalPlayer.controlRight;
                            Step(context,samples,prepares,false,intent);
                        },()=>{if(graphics!=null)Image(graphics,output,"production-segmented",host,draws);});
                        Print("Host-large-production",samples.Skip(start).ToList());Print("large-world-step-wall-interval",cadence.Skip(intervalStart).ToList());
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true));ClearActors();Scene(2);phase="back-to-native";WaitPath(context,cache,samples,prepares,0,phase);
                        Require(ReferenceEquals(worker,Get(native,"Worker")) && cache.Read(0).Strategy==PredictionStrategy.NativeIsolated,"Native / segmented / native reuses its prepared child and cannot publish a retired approximate identity.");
                        Console.WriteLine("PASS production native -> segmented -> native, late result retirement, same prepared worker");return;
                    }
                    long beforeRequests=(long)Get(native,"Requests");var before=cache.Read(0);Call(context,"UpdateRuntime");Call(context,"UpdateShell");Require((long)Get(native,"Requests")==beforeRequests && ReferenceEquals(before,cache.Read(0)),"duplicate callback cannot resample or retimestamp");
                    long privateBefore=Private(pid);Console.WriteLine("MEMORY initial-steady-private="+privateBefore);
                    if(continuousSeconds>0)Window(context,cache,samples,prepares,"continuous-eye",continuousSeconds,true);
                    for(int i=0;i<20 && GetOptional(native,"pending")==null;i++)Step(context,samples,prepares);
                    Require(GetOptional(native,"pending")!=null,"target switches with a request in flight");
                    phase="first-harpy";Main.npc[0].active=false;Scene(48,1);long switched=Stopwatch.GetTimestamp();long switchTick=Main.GameUpdateCount;int switchedValid=0;
                    waiting.Restart();
                    while(waiting.Elapsed.TotalSeconds<20 && switchedValid<120)
                    {
                        Step(context,samples,prepares);var path=cache.Read(0);
                        if(path!=null){Check(path,1);switchedValid++;if(switchedValid==1){Console.WriteLine("SWITCH wall-ms="+Ms(Stopwatch.GetTimestamp()-switched).ToString("F3")+" ticks="+(Main.GameUpdateCount-switchTick)+" age="+(Main.GameUpdateCount-path.CaptureTick));if(graphics!=null)Image(graphics,output,"production-harpy",host,draws);phase="steady-"+Main.npc[path.Identity.Slot].type;Main.LocalPlayer.position=new Vector2(640,70*16-Main.LocalPlayer.height);Main.LocalPlayer.velocity=Vector2.Zero;}}
                        Require((int)Get(worker,"ChildId")==pid,"target switch reuses resident worker");
                    }
                    Require(switchedValid>=120,"formerly short Harpy reaches sustained 120-step display: "+GetOptional(native,"Reason"));
                    if(continuousSeconds>0)Window(context,cache,samples,prepares,"continuous-harpy",continuousSeconds,true);
                    phase="harpy-draw";for(int i=0;graphics!=null && i<3;i++){Step(context,samples,prepares);long start=Stopwatch.GetTimestamp();graphics.Pixels(()=>Call(Get(host,"World"),"Draw"),Main.GameViewMatrix.ZoomMatrix);draws.Add(Ms(Stopwatch.GetTimestamp()-start));}
                    Console.WriteLine("MEMORY final-steady-private="+Private(pid)+" delta="+(Private(pid)-privateBefore));
                    Require((int)Get(Get(host,"World"),"StrokeCount")>0,"actual world layer has rendered path strokes");
                    string shown=(string)Get(Get(host,"World"),"pathText");Require(shown.Contains("延续当前输入") && shown.Contains("随机代表路线"),"rendered premise matches continuation");
                    if(continuousSeconds>0)
                    {
                        phase="first-zombie";Main.npc[1].active=false;Scene(3,2);
                        WaitPath(context,cache,samples,prepares,2,"unseen-zombie");Window(context,cache,samples,prepares,"continuous-zombie",continuousSeconds,true);
                        Main.LocalPlayer.controlJump=true;Main.LocalPlayer.releaseJump=true;
                        Window(context,cache,samples,prepares,"jump-held",5,true);Main.LocalPlayer.controlJump=false;
                        Main.LocalPlayer.inventory[0].SetDefaults(Terraria.ID.ItemID.WoodenSword);Main.LocalPlayer.selectedItemState.Select(0);Main.LocalPlayer.selectedItemState.Update();Main.LocalPlayer.releaseUseItem=true;Main.LocalPlayer.controlUseItem=true;
                        Window(context,cache,samples,prepares,"attack-held",5,true);Main.LocalPlayer.controlUseItem=false;
                        Changes(context,cache,samples,prepares,pid);
                        MenuRoundTrips(context,cache,samples,prepares,worker);
                    }
                    phase="input-invalidation";Main.LocalPlayer.controlRight=false;Main.LocalPlayer.controlLeft=true;Step(context,samples,prepares,false);Require(cache.Read(0)==null,"changed player premise removes old path immediately");
                    for(int i=0;i<80 && GetOptional(native,"pending")==null;i++)Step(context,samples,prepares);
                    Require(GetOptional(native,"pending")!=null,"OFF exercises an in-flight request");
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());Call(context,"UpdateRuntime");Call(context,"UpdateShell");Require(cache.Read(0)==null && (int)Get(Get(host,"World"),"StrokeCount")==0,"OFF clears result and presentation");
                    Require(Alive(pid) && ReferenceEquals(worker,GetOptional(native,"Worker")),"Short OFF retires world work while retaining its prepared child");
                    phase="reopen-eye";foreach(var n in Main.npc)n.active=false;foreach(var p in Main.projectile)p.active=false;Scene(2);Main.LocalPlayer.controlLeft=false;
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true));waiting.Restart();bool recovered=false;
                    while(waiting.Elapsed.TotalSeconds<65 && !recovered){Step(context,samples,prepares);worker=GetOptional(native,"Worker");if(cache.Read(0)!=null){Check(cache.Read(0),0);recovered=true;}}
                    Require(recovered && (int)Get(worker,"ChildId")==pid,"ON after in-flight OFF captures fresh world values in the same prepared worker");
                    Console.WriteLine("REOPEN same-child first-path-ms="+waiting.Elapsed.TotalMilliseconds.ToString("F3"));
                    if(continuousSeconds>0){worker=FaultRecovery(context,cache,samples,prepares,worker,output);pid=(int)Get(worker,"ChildId");}
                    if(continuousSeconds>0){NumericProduction(context,cache,samples,prepares);BuffProduction(context,cache,samples,prepares);WindProduction(context,cache,samples,prepares);}
                    Call(host,"OnSessionEnded");Require(cache.Read(0)==null && Alive(pid),"Session exit clears result while retaining prepared execution");
                    long closing=Stopwatch.GetTimestamp();Call(host,"Exit",null,EventArgs.Empty);WaitClosed(worker);Require(!Alive(pid),"Host exit reaps its exact owned helper");Console.WriteLine("CLOSE host-ms="+Ms(Stopwatch.GetTimestamp()-closing).ToString("F3"));
                }
                Print("Host-UpdateRuntime",samples);Print("Host-UpdateRuntime-after-Ready",warm);Print("existing-UpdateShell-prepare",prepares);Print("Draw-plus-GPU-readback-not-AI",draws);Print("actual-world-step-wall-interval",cadence);
                Print("Host-active-sample-or-observe-or-accept",active);Print("displayed-result-wall-age",displayAge);
                Console.WriteLine("PASS production entry: OFF, async native movement, capture age, 120 remaining steps, one process, switch, formerly short Harpy, World.Prepare, input invalidation, cleanup; pixel-draw="+(continuousSeconds==0?"verified":"not executed in CPU window"));
            }
            catch
            {
                if(Environment.GetEnvironmentVariable("JUEMINGR_NPC_DIAGNOSE_DIFFERENCE")=="1")
                {
                    int selected=((NpcIdentity)Get(Get(host,"Selection"),"Target")).Slot;
                    var capture=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true).GetMethod("CaptureScene",Flags);
                    int[] ns=Enumerable.Range(0,Main.maxNPCs).Where(i=>Main.npc[i].active).ToArray(),ps=Enumerable.Range(0,Main.maxProjectiles).Where(i=>Main.projectile[i].active).ToArray();
                    File.WriteAllBytes(Path.Combine(output,"difference-one-step.bin"),(byte[])capture.Invoke(null,new object[]{ns,ps,selected,Main.GameUpdateCount,1}));
                    Step(context,samples,prepares,false);
                    File.WriteAllLines(Path.Combine(output,"difference-native.txt"),NativeCombatPrivateImageChecks.Dump(Main.npc[selected],Main.LocalPlayer));
                }
                throw;
            }
            finally
            {
                Call(host,"Exit",null,EventArgs.Empty);if(worker==null)worker=GetOptional(native,"Worker");if(worker!=null)WaitClosed(worker);
                try{Dump(native,worker,output);sink.UnpatchAll(sink.Id);}finally{cadenceTimer?.Dispose();cadenceTimer=null;if(timer)timeEndPeriod(1);}
            }
        }
        private static void WindProduction(object context,NpcPredictionCache cache,List<double> samples,List<double> prepares)
        {
            var native=Get(Get(Get(context,"CombatObservation"),"Prediction"),"Native");
            try
            {
                foreach(float target in new[]{.7f,-.7f})
                {
                    ClearActors();Scene(2);Main.windSpeedCurrent=Main.windSpeedTarget=Main.maxRaining=0;Main.windSpeedTarget=target;Main.maxRaining=.6f;phase="wind-drift-"+target;int shown=0,longest=0,blank=0;
                    for(int i=0;i<180;i++){Step(context,samples,prepares);var path=cache.Read(0);if(path==null){longest=Math.Max(longest,++blank);continue;}blank=0;Check(path,0);shown++;}
                    Console.WriteLine("WIND original convergence target="+target+" current="+Main.windSpeedCurrent+" shown="+shown+" longest-blank="+longest+" reason="+GetOptional(native,"Reason"));
                    Require(shown>=120 && longest<60 && Math.Sign(Main.windSpeedCurrent)==Math.Sign(target),"Native weather convergence must not starve ordinary production windows.");
                }
            }
            finally{Main.windSpeedCurrent=Main.windSpeedTarget=Main.maxRaining=0;}
        }
        private static void BuffProduction(object context,NpcPredictionCache cache,List<double> samples,List<double> prepares)
        {
            var native=Get(Get(Get(context,"CombatObservation"),"Prediction"),"Native");var p=Main.LocalPlayer;
            foreach(int buff in new[]{2,5,1,12,26,206,207,3,71})
            {
                ClearActors();Scene(2);Array.Clear(p.buffType,0,p.buffType.Length);Array.Clear(p.buffTime,0,p.buffTime.Length);
                p.armor[3]=new Item();if(buff==3)p.armor[3].SetDefaults(Terraria.ID.ItemID.ObsidianSkull);
                p.AddBuff(buff,1200);phase="buff-clock-"+buff;int shown=0,longest=0,blank=0;var captures=new HashSet<long>();
                for(int i=0;i<180;i++)
                {
                    Step(context,samples,prepares);var path=cache.Read(0);
                    if(path==null){longest=Math.Max(longest,++blank);continue;}blank=0;Check(path,0);shown++;captures.Add(path.CaptureTick);
                }
                Console.WriteLine("BUFF actual type="+buff+" shown="+shown+" captures="+captures.Count+" longest-blank="+longest+" remaining="+p.buffTime[Array.IndexOf(p.buffType,buff)]+" reason="+GetOptional(native,"Reason"));
                Require(shown>=120 && captures.Count>=3 && longest<60,"Ordinary timed buff must not starve production history acceptance: "+buff);
            }
            p.armor[3]=new Item();Array.Clear(p.buffType,0,p.buffType.Length);Array.Clear(p.buffTime,0,p.buffTime.Length);
        }
        private static void NumericProduction(object context,NpcPredictionCache cache,List<double> samples,List<double> prepares)
        {
            var host=Get(context,"CombatObservation");var native=Get(Get(host,"Prediction"),"Native");var input=Get(context,"Input");int numeric=0;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,radius:0));Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();
            foreach(int type in new[]{662,248})
            {
                ClearActors();NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),650,850,type==248?245:type,Start:1);
                Step(context,samples,prepares,false);int slot=Array.FindIndex(Main.npc,n=>n.active && n.type==type);Require(slot>=0,"Actual original birth produces numeric regression actor "+type);
                phase="numeric-native-"+type;int shown=0,desired=0,blank=0,longest=0,different=0;var captures=new HashSet<long>();
                for(int i=0;i<300;i++)
                {
                    Step(context,samples,prepares,false,()=>
                    {
                        Vector2 point=NativeCombatLongCoverageChecks.SelectPoint(slot);Main.screenPosition=point-new Vector2(Main.screenWidth/2,Main.screenHeight/2);
                        Call(input,"BeginUpdate");Terraria.GameInput.PlayerInput.MouseInfo=new Microsoft.Xna.Framework.Input.MouseState(Main.screenWidth/2,Main.screenHeight/2,0,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released);
                        Call(input,"AfterNativeMouse",new List<string>());Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");Call(host,"SampleMouse");
                    });
                    var path=cache.Read(0);var selected=(NpcIdentity)Get(Get(host,"Selection"),"Target");if(selected.Slot==slot)desired++;
                    if(path==null){blank++;longest=Math.Max(longest,blank);continue;}blank=0;Check(path,selected.Slot);Require(path.Strategy==PredictionStrategy.NativeIsolated,"Ordinary actors retain native backend.");
                    if(path.Identity.Slot!=slot)continue;shown++;captures.Add(path.CaptureTick);
                    var accepted=GetOptional(native,"accepted");if(accepted==null)continue;object frame=((Array)Get(accepted,"Frames")).GetValue((int)(Main.GameUpdateCount-path.CaptureTick));
                    int index=Array.IndexOf((int[])Get(frame,"Npcs"),slot);var v=((Vector2[])Get(frame,"NpcVelocity"))[index];var old=((Vector2[])Get(frame,"NpcOldVelocity"))[index];
                    if(v!=Main.npc[slot].velocity || old!=Main.npc[slot].oldVelocity)different++;
                }
                numeric+=different;Console.WriteLine("NUMERIC actual type="+type+" desired="+desired+" shown="+shown+" captures="+captures.Count+" longest-blank="+longest+" accepted-nonidentical-velocity="+different+" reason="+GetOptional(native,"Reason"));
                Require(shown>=180 && captures.Count>=3 && longest<60,"Actual numeric actor must sustain fresh production windows.");
            }
            Require(numeric>0,"At least one real private/original numeric tail difference passes the production history/reuse gate.");
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true));ClearActors();Scene(2);
        }
        private static void WaitPath(object context,NpcPredictionCache cache,List<double> samples,List<double> prepares,int slot,string label,bool right=true)
        {
            var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<5000){Step(context,samples,prepares,right);var path=cache.Read(0);if(path!=null){Check(path,slot);Console.WriteLine("RESPONSE "+label+" ms="+watch.Elapsed.TotalMilliseconds.ToString("F3"));return;}}
            throw new InvalidOperationException("No complete production path for "+label+": "+GetOptional(Get(Get(Get(context,"CombatObservation"),"Prediction"),"Native"),"Reason"));
        }
        private static void Image(ProbeGraphics graphics,string output,string name,object host,List<double> draws)
        {
            var timer=Stopwatch.StartNew();graphics.Image(Path.Combine(output,name+".png"),()=>Call(Get(host,"World"),"Draw"),Main.GameViewMatrix.ZoomMatrix);
            draws.Add(timer.Elapsed.TotalMilliseconds);Console.WriteLine("READBACK "+name+" draw-GPU-readback-save-ms="+timer.Elapsed.TotalMilliseconds.ToString("F3"));
        }
        private static void Window(object context,NpcPredictionCache cache,List<double> samples,List<double> prepares,string label,int seconds,bool expected)
        {
            phase=label;int start=samples.Count,cadenceStart=cadence.Count,shown=0,frames=0,attacking=0,airborne=0,blank=0,longestBlank=0,g0=GC.CollectionCount(0),g1=GC.CollectionCount(1),g2=GC.CollectionCount(2);
            float minX=Main.LocalPlayer.position.X,maxX=minX,minY=Main.LocalPlayer.position.Y;bool right=true;
            var native=Get(Get(Get(context,"CombatObservation"),"Prediction"),"Native");var worker=GetOptional(native,"Worker");
            int pid=worker==null?0:(int)Get(worker,"ChildId");long before=pid==0?0:Private(pid);double cpu=Cpu(pid);
            var watch=Stopwatch.StartNew();
            while(watch.Elapsed.TotalSeconds<seconds)
            {
                // Keep real movement inside the known floor instead of
                // eventually measuring held-right while stationary at a wall.
                if(Main.LocalPlayer.position.X>=740)right=false;else if(Main.LocalPlayer.position.X<=660)right=true;
                Main.LocalPlayer.controlRight=right;Main.LocalPlayer.controlLeft=!right;
                Step(context,samples,prepares,false);frames++;var path=cache.Read(0);
                minX=Math.Min(minX,Main.LocalPlayer.position.X);maxX=Math.Max(maxX,Main.LocalPlayer.position.X);minY=Math.Min(minY,Main.LocalPlayer.position.Y);if(Main.LocalPlayer.velocity.Y<-.1f)airborne++;
                if(Main.LocalPlayer.itemAnimation>0)attacking++;
                if(path!=null){Check(path,path.Identity.Slot);shown++;blank=0;}else{blank++;longestBlank=Math.Max(longestBlank,blank);}
                if(!expected)Require(path==null,"OFF baseline does not create a result.");
                // Drain all measurements, including failures, before the
                // product's bounded development queue can overwrite history.
                foreach(var m in (IEnumerable)Get(native,"Measurements"))measurementRows.Add(MeasurementRow(m));
                Call(Get(native,"Measurements"),"Clear");
            }
            Console.WriteLine("WINDOW "+label+" frames="+frames+" shown="+shown+" longest-blank-updates="+longestBlank+" x-span="+(maxX-minX).ToString("F3")+" airborne-updates="+airborne+" wall-ms="+watch.Elapsed.TotalMilliseconds.ToString("F3")+" gc="+(GC.CollectionCount(0)-g0)+"/"+(GC.CollectionCount(1)-g1)+"/"+(GC.CollectionCount(2)-g2)+" worker-cpu-ms="+(Cpu(pid)-cpu).ToString("F3")+" private-start="+before+" private-end="+(pid==0?0:Private(pid)));
            Print("window-"+label,samples.Skip(start).ToList());Print("world-interval-"+label,cadence.Skip(cadenceStart).ToList());if(expected)Require(shown>0 && longestBlank<60,"Expected window must deliver without a one-second starvation: "+label);
            if(expected)Require(maxX-minX>40,"Movement window must observe actual displacement: "+label);
            if(label=="jump-held")Require(airborne>0 && minY<70*16-Main.LocalPlayer.height-8,"Jump perturbation must actually leave the floor.");
            if(label=="attack-held")Require(attacking>0,"Attack perturbation must execute an actual native weapon animation.");
        }
        private static void ClearActors()
        {foreach(var n in Main.npc)n.active=false;foreach(var p in Main.projectile)p.active=false;Main.LocalPlayer.position=new Vector2(640,70*16-Main.LocalPlayer.height);Main.LocalPlayer.velocity=Vector2.Zero;Main.LocalPlayer.controlLeft=false;}
        private static void Changes(object context,NpcPredictionCache cache,List<double> samples,List<double> prepares,int pid)
        {
            var native=Get(Get(Get(context,"CombatObservation"),"Prediction"),"Native");
            ClearActors();phase="world-left-border";Scene(3,2);Main.LocalPlayer.controlRight=false;Main.LocalPlayer.controlLeft=true;
            WaitPath(context,cache,samples,prepares,2,"world-left-border",false);
            Require(Main.LocalPlayer.position.X==Main.leftWorld+640 && Main.LocalPlayer.velocity.X==0,"The edge regression must exercise the original stationary border, not avoid it.");
            // Reuse the existing legal head/hand relationships, preserving the
            // current world, player object and monotonically increasing tick.
            ClearActors();phase="unseen-linked-skeletron";Scene(35);Main.npc[0].ai[0]=1;
            Scene(36,1);Main.npc[1].position=new Vector2(500,600);Main.npc[1].ai[0]=-1;Main.npc[1].ai[1]=0;
            Scene(36,2);Main.npc[2].position=new Vector2(900,600);Main.npc[2].ai[0]=1;Main.npc[2].ai[1]=0;
            WaitPath(context,cache,samples,prepares,0,"unseen-linked-skeletron");
            ClearActors();phase="unseen-perched-vulture";Scene(61,3);var vulture=Main.npc[3];vulture.position=new Vector2(650,70*16-vulture.height);Main.LocalPlayer.position.X=1000;
            WaitPath(context,cache,samples,prepares,3,"unseen-perched-vulture");Require(vulture.ai[0]==0,"Native vulture must actually remain perched before the phase stimulus.");
            phase="vulture-native-takeoff";Main.LocalPlayer.position.X=730;Step(context,samples,prepares);Require(vulture.ai[0]==1 && cache.Read(0)==null,"Native proximity causes takeoff and retires the old premise.");WaitPath(context,cache,samples,prepares,3,"vulture-native-takeoff");
            ClearActors();phase="terrain-before";Scene(3,2);Main.npc[2].position.Y=70*16-Main.npc[2].height;WaitPath(context,cache,samples,prepares,2,"terrain-before");
            phase="terrain-and-water";
            for(int x=39;x<=46;x++)for(int y=64;y<70;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(0);}
            for(int y=64;y<70;y++){Main.tile[47,y].active(true);Main.tile[47,y].type=1;}
            Step(context,samples,prepares);Require(cache.Read(0)==null,"Changed observed terrain retires the old complete path immediately.");
            Require(Main.npc[2].wet,"Water mutation must enter native wet movement.");WaitPath(context,cache,samples,prepares,2,"terrain-and-water");
            phase="terrain-removed";for(int x=39;x<=46;x++)for(int y=64;y<70;y++)Main.tile[x,y].liquid=0;for(int y=64;y<70;y++)Main.tile[47,y].active(false);
            Step(context,samples,prepares);Require(cache.Read(0)==null,"Removing the observed obstacle/water also retires the old result.");WaitPath(context,cache,samples,prepares,2,"terrain-removed");
            for(int pass=0;pass<2;pass++)foreach(int type in new[]{2,48,3}){ClearActors();phase="repeat-switch-"+type;Scene(type);WaitPath(context,cache,samples,prepares,0,phase);}
            Require((int)Get(Get(native,"Worker"),"ChildId")==pid,"First mechanisms, phase/terrain changes and repeated targets retain the prepared child.");
            Console.WriteLine("PASS production first linked mechanism / native takeoff / terrain and water invalidation / repeated seen targets");
        }
        private static void MenuRoundTrips(object context,NpcPredictionCache cache,List<double> samples,List<double> prepares,object worker)
        {
            var host=Get(context,"CombatObservation");var native=Get(Get(host,"Prediction"),"Native");
            foreach(bool another in new[]{false,true})
            {
                var before=cache.Read(0);Require(before!=null,"World round trip starts with a displayed result.");
                Main.gameMenu=true;Call(context,"UpdateRuntime");Call(context,"UpdateShell");Require(cache.Read(0)==null,"Menu immediately retires the prior world's displayed result.");
                long requests=(long)Get(native,"Requests");var ready=Stopwatch.StartNew();
                while((int)Get(worker,"State")!=1 && ready.ElapsedMilliseconds<6000){Call(context,"UpdateRuntime");Thread.Sleep(10);}
                Require((int)Get(worker,"State")==1 && (long)Get(native,"Requests")==requests,"Menu waits for world-clear ACK without sampling.");
                if(another)Main.ActiveWorldFileData=new Terraria.IO.WorldFileData{UniqueId=Guid.NewGuid()};
                Main.gameMenu=false;ClearActors();Scene(2);phase=another?"other-world-first-line":"same-world-first-line";
                WaitPath(context,cache,samples,prepares,0,phase);
                Require(ReferenceEquals(worker,Get(native,"Worker")) && cache.Read(0).Identity.Session!=before.Identity.Session,"Fresh world identity uses the same cleared and prepared process.");
            }
            Console.WriteLine("PASS displayed result / menu clear ACK / ready entry / same and other world fresh first line");
        }
        private static object FaultRecovery(object context,NpcPredictionCache cache,List<double> samples,List<double> prepares,object worker,string output)
        {
            var host=Get(context,"CombatObservation");var native=Get(Get(host,"Prediction"),"Native");int oldPid=(int)Get(worker,"ChildId");
            phase="owned-child-failure";
            // Use the exact child handle retained by this test's transport;
            // never enumerate or terminate processes by name or stale PID.
            ((Process)Get(worker,"child")).Kill();var wait=Stopwatch.StartNew();
            while(!(bool)Get(native,"Failed") && wait.ElapsedMilliseconds<6000)Step(context,samples,prepares);
            Require((bool)Get(native,"Failed") && cache.Read(0)==null,"An actual child exit fails closed at the production Host.");
            for(int i=0;i<20;i++)Step(context,samples,prepares);
            Require(ReferenceEquals(worker,Get(native,"Worker")),"Failure does not create a restart storm.");
            WaitClosed(worker);File.WriteAllText(Path.Combine(output,"production-first-worker.log"),(string)GetOptional(worker,"Diagnostics")??"");ClearActors();Scene(2);phase="explicit-retry";Call(host,"Set",1,true);wait.Restart();
            while(wait.ElapsedMilliseconds<15000){Step(context,samples,prepares);if(cache.Read(0)!=null){Check(cache.Read(0),0);var fresh=Get(native,"Worker");Require(!ReferenceEquals(fresh,worker) && (int)Get(fresh,"ChildId")!=oldPid,"Explicit retry owns a fresh child.");Console.WriteLine("RESPONSE explicit-retry ms="+wait.Elapsed.TotalMilliseconds.ToString("F3"));return fresh;}}
            throw new InvalidOperationException("Explicit production retry did not recover: "+GetOptional(native,"Reason"));
        }
        private static double Cpu(int pid){if(pid==0)return 0;using(var p=Process.GetProcessById(pid))return p.TotalProcessorTime.TotalMilliseconds;}
        private static void Initialize()
        {
            Terraria.ObjectData.TileObjectData.Initialize();Terraria.GameContent.Creative.CreativePowerManager.Initialize();Terraria.DataStructures.ArmorSetBonuses.Initialize();Terraria.DataStructures.ArmorSetBonuses.BuildLookup();
            typeof(Main).GetMethod("Initialize_TileAndNPCData1",Flags).Invoke(null,null);typeof(Main).GetMethod("Initialize_TileAndNPCData2",Flags).Invoke(null,null);
            NativeCombatWorkerAssetChecks.Initialize();Lighting.Mode=Terraria.Graphics.Light.LightMode.Color;
            for(int i=1;i<Main.player.Length;i++)Main.player[i]=new Player{whoAmI=i};for(int i=0;i<Main.gore.Length;i++)Main.gore[i]=new Gore();for(int i=0;i<Main.item.Length;i++)Main.item[i].whoAmI=i;
            PopupText.popupText=new PopupText[20];for(int i=0;i<20;i++)PopupText.popupText[i]=new PopupText();
            Main.ActiveWorldFileData=new Terraria.IO.WorldFileData();Main.dayTime=false;Main.time=1800;Main.dayRate=1;Main.worldSurface=60;Main.rockLayer=90;Main.leftWorld=Main.topWorld=0;Main.rightWorld=Main.bottomWorld=1920;Main.screenPosition=new Vector2(300,480);Main.GameMode=0;Main.bloodMoon=Main.eclipse=false;Main.windSpeedCurrent=Main.windSpeedTarget=Main.maxRaining=0;
            Main.tileSolid[1]=true;for(int x=0;x<120;x++)for(int y=70;y<120;y++){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}for(int x=50;x<53;x++)for(int y=55;y<70;y++){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}
            typeof(Main).GetField("_rngs",Flags).SetValue(null,new Dictionary<string,UnifiedRandom>{{"UpdatePlayers",new UnifiedRandom(531)},{"UpdateNPCs",new UnifiedRandom(879)},{"UpdateProjectiles",new UnifiedRandom(171)}});
            var player=Main.LocalPlayer;player.position=new Vector2(640,70*16-player.height);player.fallStart=player.fallStart2=67;player.statLife=player.statLifeMax=player.statLifeMax2=400;player.immune=true;player.immuneTime=100000;player.isControlledByFilm=true;player.releaseJump=true;
        }
        private static void Scene(int type,int slot=0)
        {var n=Main.npc[slot];n.SetDefaults(type);n.whoAmI=slot;n.active=true;n.target=0;n.position=new Vector2(650,850);n.timeLeft=750;NPC.ClearFoundActiveNPCs();NPC.mechQueen=NPC.golemBoss=-1;}
        private static void Step(object context,List<double> samples,List<double> prepares,bool right=true,Action sampleIntent=null)
        {
            long now=Stopwatch.GetTimestamp();double interval=priorStep==0?0:Ms(now-priorStep);if(priorStep!=0)cadence.Add(interval);priorStep=now;
            int gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2);
            var pace=Stopwatch.StartNew();NativeQuickItemChecks.BeginWorldStep();NPC.UpdateProtectedSpawnSlots();NPC.ClearFoundActiveNPCs();NPC.UpdateFoundActiveNPCs();
            // Exercise the locked original deterministic branch, excluding
            // first-iteration lightning and authority-only weather RNG.
            NativeCombatWorkerChecks.AdvanceWeather();
            if(right){Main.LocalPlayer.controlRight=true;Main.LocalPlayer.controlLeft=false;}
            using(Main.SwapRandom("UpdatePlayers"))Main.LocalPlayer.Update(0);
            if(NPC.brainOfGravity>=0 && NPC.brainOfGravity<Main.maxNPCs && (!Main.npc[NPC.brainOfGravity].active || Main.npc[NPC.brainOfGravity].type!=266))NPC.brainOfGravity=-1;
            using(Main.SwapRandom("UpdateNPCs"))for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active)Main.npc[i].UpdateNPC(i);
            using(Main.SwapRandom("UpdateProjectiles"))try{for(int i=0;i<Main.maxProjectiles;i++){Main.ProjectileUpdateLoopIndex=i;if(Main.projectile[i].active)Main.projectile[i].Update(i);}}finally{Main.ProjectileUpdateLoopIndex=-1;}
            Main.time+=Main.dayRate;
            sampleIntent?.Invoke();
            double originalMs=pace.Elapsed.TotalMilliseconds;
            var native=Get(Get(Get(context,"CombatObservation"),"Prediction"),"Native");
            long requests=(long)Get(native,"Requests"),observed=(long)Get(native,"Observed");int measured=((ICollection)Get(native,"Measurements")).Count;
            long start=Stopwatch.GetTimestamp();Call(context,"UpdateRuntime");samples.Add(Ms(Stopwatch.GetTimestamp()-start));start=Stopwatch.GetTimestamp();Call(context,"UpdateShell");prepares.Add(Ms(Stopwatch.GetTimestamp()-start));
            var worker=GetOptional(native,"Worker");bool ready=worker!=null && (double)Get(worker,"ReadyMilliseconds")>0;
            if(ready)warm.Add(samples.Last());
            var cache=(NpcPredictionCache)Get(Get(Get(context,"CombatObservation"),"Prediction"),"Cache");var shown=cache.Read(0);var accepted=GetOptional(native,"acceptedRequest");
            bool didWork=shown!=null && shown.Strategy==PredictionStrategy.SegmentedTrend || requests!=(long)Get(native,"Requests") || observed!=(long)Get(native,"Observed") && (GetOptional(native,"pending")!=null || GetOptional(native,"accepted")!=null) || measured!=((ICollection)Get(native,"Measurements")).Count;
            if(didWork)active.Add(samples.Last());
            double age=shown!=null && accepted!=null && (long)Get(accepted,"Wall")>0?Ms(Stopwatch.GetTimestamp()-(long)Get(accepted,"Wall")):-1;
            if(age>=0)displayAge.Add(age);
            string row=string.Join(",",Main.GameUpdateCount,Ms(Stopwatch.GetTimestamp()).ToString("R",System.Globalization.CultureInfo.InvariantCulture),phase,ready?1:0,didWork?1:0,requests!=(long)Get(native,"Requests")?1:0,measured!=((ICollection)Get(native,"Measurements")).Count?1:0,samples.Last().ToString("R",System.Globalization.CultureInfo.InvariantCulture),shown==null?-1:shown.CaptureTick,age.ToString("R",System.Globalization.CultureInfo.InvariantCulture),(long)Get(native,"Requests"),GC.CollectionCount(0)-gc0,GC.CollectionCount(1)-gc1,GC.CollectionCount(2)-gc2,originalMs.ToString("R",System.Globalization.CultureInfo.InvariantCulture),prepares.Last().ToString("R",System.Globalization.CultureInfo.InvariantCulture),interval.ToString("R",System.Globalization.CultureInfo.InvariantCulture),shown==null?"none":shown.Strategy.ToString());
            // Occluded Windows probes can lose timeBeginPeriod resolution.
            // A high-resolution waitable timer keeps the fixture near 60 Hz
            // without spinning; actual intervals are still recorded, not assumed.
            double work=pace.Elapsed.TotalMilliseconds;long waitStart=Stopwatch.GetTimestamp();double remaining=1000.0/60-work;
            if(remaining>0){long due=-(long)(remaining*10000);if(!SetWaitableTimer(cadenceTimer,ref due,0,IntPtr.Zero,IntPtr.Zero,false) || WaitForSingleObject(cadenceTimer,1000)!=0)throw new InvalidOperationException("Fixture cadence timer failed.");}
            updates.Add(row+","+work.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+Ms(Stopwatch.GetTimestamp()-waitStart).ToString("R",System.Globalization.CultureInfo.InvariantCulture));
        }
        private static void Check(NpcTrajectory path,int slot)
        {Require(path.Identity.Slot==slot && ReferenceEquals(path.Identity.Token,Main.npc[slot]),"never display another target");Require(path.SampleTick==Main.GameUpdateCount && path.CaptureTick<=path.SampleTick && path.Count==121,"current window retains capture age and 120 future steps");Require(Math.Abs(path[0].Bounds.X-Main.npc[slot].position.X)<.002f && Math.Abs(path[0].Bounds.Y-Main.npc[slot].position.Y)<.002f,"visible origin is current observed position");}
        private static void Dump(object native,object worker,string output)
        {
            File.WriteAllLines(Path.Combine(output,"production-updates.csv"),new[]{"tick,wallMs,phase,ready,active,sampled,received,hostMs,displayCaptureTick,displayAgeMs,requests,gc0,gc1,gc2,originalMs,shellMs,intervalMs,strategy,workMs,waitMs"}.Concat(updates));
            using(var csv=new StreamWriter(Path.Combine(output,"production-costs.csv"))){csv.WriteLine(string.Join(",",MeasurementFields));foreach(string row in measurementRows)csv.WriteLine(row);foreach(var m in (IEnumerable)Get(native,"Measurements"))csv.WriteLine(MeasurementRow(m));}
            Console.WriteLine("SESSION requests="+Get(native,"Requests")+" published-frames="+Get(native,"Published")+" rejected="+Get(native,"Rejected")+" refused="+Get(native,"Refused")+" observe-total-ms="+Get(native,"ObserveMilliseconds")+" observe-max-ms="+Get(native,"ObserveMaximum"));
            if(worker!=null){Console.WriteLine("READY ms="+Get(worker,"ReadyMilliseconds"));File.WriteAllText(Path.Combine(output,"production-worker.log"),(string)GetOptional(worker,"Diagnostics")??"");}
        }
        private static readonly string[] MeasurementFields={"CaptureTick","ArriveTick","Age","WallAgeMs","CaptureMs","EncodeMs","ExchangeMs","DecodeMs","AcceptMs","TotalMs","ResetMs","RestoreMs","AdvanceMs","Bytes","ReplyBytes","Outcome"};
        private static string MeasurementRow(object value){return string.Join(",",MeasurementFields.Select(f=>"\""+Convert.ToString(Get(value,f),System.Globalization.CultureInfo.InvariantCulture).Replace("\"","\"\"")+"\""));}
        private static void Print(string name,List<double> values){if(values.Count==0)return;values.Sort();Console.WriteLine("COST "+name+" n="+values.Count+" mean-ms="+values.Average().ToString("F3")+" p95-ms="+values[(int)((values.Count-1)*.95)].ToString("F3")+" p99-ms="+values[(int)((values.Count-1)*.99)].ToString("F3")+" max-ms="+values.Last().ToString("F3"));}
        private static void WaitClosed(object worker){var timer=Stopwatch.StartNew();while(!(bool)Get(worker,"Closed") && timer.ElapsedMilliseconds<5000)Thread.Sleep(10);Require((bool)Get(worker,"Closed"),"bounded owned-process cleanup");}
        private static bool Alive(int pid){try{using(var p=Process.GetProcessById(pid))return !p.HasExited;}catch(ArgumentException){return false;}}
        private static long Private(int pid){using(var p=Process.GetProcessById(pid))return p.PrivateMemorySize64;}
        private static double Ms(long ticks){return ticks*1000.0/Stopwatch.Frequency;}
        private static void Patch(Harmony h,MethodInfo method,string name){h.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatProductionPredictionChecks).GetMethod(name,Flags)));}
        private static bool Skip(){return false;}
        private static bool Refuse(MethodBase __originalMethod){if(__originalMethod.DeclaringType==typeof(NetMessage) && Main.netMode==0)return true;throw new InvalidOperationException("Isolated production scene attempted "+__originalMethod.Name);}
        internal static void Codec(Assembly host)
        {
            var read=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionResult").GetMethod("Read",Flags);var identity=new NpcIdentity(1,new object(),0,0,2,2);
            using(var bytes=new MemoryStream())using(var w=new BinaryWriter(bytes))
            {w.Write(-NativeCombatWorkerChecks.ExpectedProtocol);w.Write("InvalidDataException");w.Write("missing page");w.Write(-1);w.Write(-1);w.Write(2);w.Write(7);w.Write(0);w.Flush();var parsed=read.Invoke(null,new object[]{bytes.ToArray(),null,identity,0L,1L,false});Require((int)Get(parsed,"Kind")==2 && (int)Get(parsed,"Slot")==7,"refusal field ID is an Int32, preserving missing-page discovery");}
            byte[] core,alignment;const int count=181;float key=BitConverter.ToSingle(BitConverter.GetBytes(0x7F800100U),0);
            using(var bytes=new MemoryStream())using(var w=new BinaryWriter(bytes))
            {
                w.Write(NativeCombatWorkerChecks.ExpectedProtocol);w.Write(0L);w.Write(0);w.Write(count);
                for(int i=0;i<count;i++){w.Write(i);w.Write(2);w.Write(true);for(int j=0;j<4;j++)w.Write(0f);w.Write(32);w.Write(32);w.Write(key);w.Write(100);for(int j=0;j<4;j++)w.Write(0);w.Write(0d);w.Write(0);w.Write(2);w.Write((byte)0);w.Write((byte)0);}
                w.Write(0);w.Write(count*(12+49+44));
                for(int i=0;i<count;i++){w.Write(i);w.Write(1);w.Write(0);w.Write((byte)0);w.Write(2);w.Write(2);for(int j=0;j<4;j++)w.Write(0f);w.Write(100);for(int j=0;j<4;j++)w.Write(key);w.Write(1);w.Write(0);w.Write(1U);w.Write(1);for(int j=0;j<4;j++)w.Write(0f);w.Write(100);for(int j=0;j<3;j++)w.Write(key);}
                w.Write(0L);w.Write(1000L);w.Write(0L);w.Write(0L);w.Flush();core=bytes.ToArray();
            }
            Func<int,byte[]> proofBytes=changed=>{using(var bytes=new MemoryStream())using(var w=new BinaryWriter(bytes)){w.Write(count);for(int i=0;i<count;i++){bool hasState=i<=60;if(i==changed)hasState=!hasState;w.Write((long)i);w.Write(hasState);if(hasState){w.Write(0UL);w.Write(1);w.Write(0);w.Write(0UL);w.Write(0);for(int j=0;j<4;j++)w.Write(0f);w.Write(0);}w.Write(0f);w.Write(0f);w.Write(true);w.Write(true);w.Write(false);}w.Write(0L);w.Flush();return bytes.ToArray();}};
            alignment=proofBytes(-1);
            foreach(bool network in new[]{false,true})
            {
                var parsed=read.Invoke(null,new object[]{core,alignment,identity,0L,1L,network});var path=(NpcTrajectory)Get(parsed,"Trajectory");
                Require(((path.Assumptions&PredictionAssumption.NetworkObservation)!=0)==network,"Native timeline preserves the request's single-player/client observation condition.");
            }
            foreach(int edge in new[]{60,61})
            {bool rejected=false;try{read.Invoke(null,new object[]{core,proofBytes(edge),identity,0L,1L,false});}catch(TargetInvocationException e){rejected=e.InnerException is InvalidDataException && e.InnerException.Message=="Alignment proof extent.";}Require(rejected,"The 180-step packet rejects either side of the state-proof boundary being reversed.");}
            foreach(int offset in new[]{12,25})
            {
                var bad=(byte[])alignment.Clone();bad[offset]=offset==12?(byte)0:(byte)1;bool rejectedProof=false;
                try{read.Invoke(null,new object[]{core,bad,identity,0L,1L,false});}catch(TargetInvocationException error){rejectedProof=error.InnerException is InvalidDataException && error.InnerException.Message==(offset==12?"Alignment proof extent.":"Alignment target absent.");}
                Require(rejectedProof,"Missing history or target cannot be replaced by presentation metadata.");
            }
            Array.Copy(BitConverter.GetBytes(key),0,core,29,4);bool refused=false;
            try{read.Invoke(null,new object[]{core,alignment,identity,0L,1L,false});}catch(TargetInvocationException e){refused=e.InnerException is InvalidDataException;}
            Require(refused,"raw AI key NaN remains legal while nonfinite physical position is rejected");Console.WriteLine("PASS production codec refusal-page / raw identity bits / invalid geometry");
        }
    }
}
