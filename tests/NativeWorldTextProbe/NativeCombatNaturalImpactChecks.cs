using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Diagnostics;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Microsoft.Xna.Framework.Input;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // A finite, isolated original-world scene, not a stationary NPC substitute.
    // The expected denominator is declared before Contact is read: a clear,
    // supplied gun against the same living ordinary slime inside 25 tiles.
    // A separate unassisted, explicitly aimed original run must produce native
    // births and damage. Missing candidate contacts never remove a frame.
    internal static class NativeCombatNaturalImpactChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static Player player;
        private static NPC receiver;
        private static Vector2 physical;
        private static bool candidate,cpuMode;
        private static bool moveLeft,moveRight;
        private static int births,assistedBirths,hits;
        private static readonly List<string> birthRows=new List<string>();
        private static void MoveBefore(Player __instance)
        {
            if(!ReferenceEquals(__instance,player))return;
            // The existing Step samples Mouse1 before Player.Update. Supply
            // movement at that same native boundary, then let the original
            // ResetControls/CopyInto branch consume all three controls.
            PlayerInput.Triggers.Current.Left=moveLeft;PlayerInput.Triggers.Current.Right=moveRight;
        }
        private static void Born(Player __instance,Projectile __0)
        {
            if(!ReferenceEquals(__instance,player) || __0.owner!=player.whoAmI)return;
            births++;
            var cursor=new Vector2(Main.mouseX,Main.mouseY)+Main.screenPosition;
            bool changed=Vector2.DistanceSquared(cursor,physical)>4;
            if(changed)assistedBirths++;
            if(!cpuMode)birthRows.Add("tick="+Main.GameUpdateCount+" projectile="+__0.type+" key="+__0.key+" origin="+__0.Center+" velocity="+__0.velocity+" consumedCursor="+cursor+" physical="+physical+" selectedReceiver="+receiver.whoAmI+"/"+receiver.generation+" center="+receiver.Center);
            Require(candidate?changed:!changed,"natural slime native birth consumes the selected assisted/explicit original cursor");
        }
        private static void Hit(NPC __instance,int __result)
        {if(ReferenceEquals(__instance,receiver) && __result>0)hits++;}
        private static bool Red(Color p){return p.A>100 && p.R>190 && p.G<110 && p.B<110;}
        private static List<Vector2> RedPixels(Color[] pixels)
        {
            var result=new List<Vector2>(100);
            for(int i=0;i<pixels.Length;i++)if(Red(pixels[i]))result.Add(new Vector2(i%960,i/960));
            return result;
        }
        internal static void Run(object context,ProbeGraphics graphics,string output)
        {RunCore(context,graphics,output,false,false);}
        internal static void RunReference(object context,ProbeGraphics graphics,string output)
        {RunCore(context,graphics,output,true,false);}
        internal static void RunCandidate(object context,ProbeGraphics graphics,string output)
        {RunCore(context,graphics,output,false,true);}
        // Assets are initialized by the same original graphics fixture, but
        // this mode never renders, reads pixels or asserts the Draw denominator.
        internal static void RunCpu(object context,ProbeGraphics graphics,string output)
        {RunCore(context,graphics,output,false,false,true);}
        // Attribution keeps the original map, physical input and native phases.
        // Modes are test-only; no persisted owner option is changed.
        internal static void RunCpuModes(object context,ProbeGraphics graphics,string output)
        {RunCore(context,graphics,output,false,false,true,true);}
        internal static void RunCpuModesFinal(object context,ProbeGraphics graphics,string output)
        {RunCore(context,graphics,output,false,false,true,true,false,true);}
        internal static void RunDrawCpu(object context,ProbeGraphics graphics,string output)
        {RunCore(context,graphics,output,false,false,true,true,false,true,true);}
        internal static void RunCpuNativeDamage(object context,ProbeGraphics graphics,string output)
        {RunCore(context,graphics,output,true,false,true,false,true);}
        private sealed class CpuBindings
        {
            internal readonly Action BeginInput,AfterMapping,AfterKeys,ProcessInput,CombatSample,SampleMouse,UpdateRuntime,UpdateShell;
            internal readonly Action<List<string>> AfterMouse;
            internal readonly Action<uint> SetWorldTick;
            internal readonly Func<bool> Exceeded;
            internal readonly Func<double> BudgetMilliseconds;
            internal readonly Func<ulong> BudgetTick;
            internal readonly Func<AttackContact> Preview;
            internal readonly NpcPredictionCache Cache;
            internal readonly List<string> Keys=new List<string>(1);
            private static Action Bind(object owner,string name)
            {return (Action)Delegate.CreateDelegate(typeof(Action),owner,owner.GetType().GetMethod(name,Flags));}
            internal CpuBindings(object context,object combat,object host,object input,object attack)
            {
                BeginInput=Bind(input,"BeginUpdate");AfterMapping=Bind(input,"AfterMapping");AfterKeys=Bind(input,"AfterKeyboardRefresh");
                AfterMouse=(Action<List<string>>)Delegate.CreateDelegate(typeof(Action<List<string>>),input,input.GetType().GetMethod("AfterNativeMouse",Flags));
                ProcessInput=Bind(Get(context,"Shell"),"ProcessInput");CombatSample=Bind(combat,"Sample");SampleMouse=Bind(host,"SampleMouse");UpdateRuntime=Bind(context,"UpdateRuntime");UpdateShell=Bind(context,"UpdateShell");
                var work=Get(host,"PredictionWork");Exceeded=(Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>),work,work.GetType().GetProperty("Exceeded",Flags).GetGetMethod(true));BudgetMilliseconds=(Func<double>)Delegate.CreateDelegate(typeof(Func<double>),work,work.GetType().GetProperty("UsedMilliseconds",Flags).GetGetMethod(true));
                var workTick=work.GetType().GetField("tick",Flags);Require(workTick!=null,"CPU budget diagnosis has the actual owner's key");BudgetTick=()=>(ulong)workTick.GetValue(work);
                var planField=attack.GetType().GetField("preview",Flags);var contactField=planField.FieldType.GetField("Contact",Flags);
                // Read the already prepared raw fact outside timing. Calling
                // its validating public getter again could scan more inputs.
                Preview=()=>{var plan=planField.GetValue(attack);return (AttackContact)contactField.GetValue(plan);};Cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");
                // The original probe increments this exact field once at the
                // world's entry. Bind its setter once; no extra clock/reset or
                // reflection argument arrays enter the measured interval.
                var clock=typeof(Main).GetField("_gameUpdateCount",Flags);Require(clock!=null && clock.FieldType==typeof(uint),"CPU sequence has the original native world counter");
                var setter=new DynamicMethod("NaturalCpuWorldTick",typeof(void),new[]{typeof(uint)},typeof(NativeCombatNaturalImpactChecks),true);var il=setter.GetILGenerator();il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Stsfld,clock);il.Emit(OpCodes.Ret);SetWorldTick=(Action<uint>)setter.CreateDelegate(typeof(Action<uint>));
            }
        }
        private struct CpuRecord
        {
            internal int Case,Frame,Phase,Gc0,Gc1,Gc2,PathCount,Births,Assisted,Hits,Life;
            internal uint Tick;internal ulong BudgetTick;internal long Elapsed,DrawElapsed,RenderElapsed;
            internal bool Moving;
            internal bool BudgetCurrent,Exceeded,Preview,Exit;
            internal bool Aim,Path;
            internal long DisplayCapture,DisplaySample;
            internal int DisplayCount;
            internal double Budget;
            internal float PlayerX,PlayerY,PlayerVx,PlayerVy,NpcX,NpcY,NpcVx,NpcVy;
        }
        private static void CpuStep(CpuBindings bound,Vector2 mouse,bool held)
        {
            // Same real sequence as SelectionPreview.Step, without its test
            // player/RNG clones. Transport the same physical input on both
            // OFF/ON sides, then let vanilla Player.Update consume controls.
            bound.BeginInput();Vector2 pixel=Vector2.Transform(mouse-Main.screenPosition,Main.GameViewMatrix.ZoomMatrix);
            PlayerInput.MouseInfo=new MouseState((int)pixel.X,(int)pixel.Y,0,held?ButtonState.Pressed:ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);
            bound.Keys.Clear();if(held)bound.Keys.Add("Mouse1");PlayerInput.Triggers.Reset();bound.AfterMouse(bound.Keys);
            foreach(string token in bound.Keys)PlayerInput.CurrentProfile.InputModes[InputMode.Keyboard].Processkey(PlayerInput.Triggers.Current,token,InputMode.Keyboard);
            PlayerInput.Triggers.Update();Main.mouseLeft=PlayerInput.Triggers.Current.MouseLeft;Main.mouseRight=false;PlayerInput.MouseX=(int)pixel.X;PlayerInput.MouseY=(int)pixel.Y;PlayerInput.UpdateMainMouse();PlayerInput.CacheZoomableValues();Main.oldKeyState=Main.keyState;Main.keyState=new KeyboardState();bound.AfterMapping();bound.AfterKeys();bound.ProcessInput();PlayerInput.SetZoom_World();
            bound.CombatSample();bound.SampleMouse();bound.SetWorldTick(unchecked(Main.GameUpdateCount+1));player.Update(0);
            foreach(var n in Main.npc)if(n.active)n.UpdateNPC(n.whoAmI);
            for(int slot=0;slot<Main.maxProjectiles;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;Main.projectile[slot].Update(slot);}Main.ProjectileUpdateLoopIndex=-1;
            bound.UpdateRuntime();bound.UpdateShell();
        }
        private static bool CpuExited(object combat)
        {
            if(player.itemAnimation!=0 || player.itemTime!=0 || player.channel || player.controlUseItem || (bool)Get(Get(combat,"Use"),"Active"))return false;
            foreach(var projectile in Main.projectile)if(projectile.active && projectile.owner==player.whoAmI)return false;return true;
        }
        private static void WriteCpu(string output,CpuRecord[] records,int count,long allocationStart,long allocationEnd,long[] caseAllocations,bool longWindow,bool drawTiming)
        {
            var rows=new List<string>(count+1){"case,moving,enabled,phase,frame,tick,elapsedMs,gc0,gc1,gc2,budgetCurrent,budgetTick,budgetExceeded,budgetMs,pathCount,rawPreviewContact,nativeBirths,assistedBirths,nativeHits,npcLife,playerX,playerY,playerVx,playerVy,npcX,npcY,npcVx,npcVy,nativeExitComplete,aim,path,displayCaptureTick,displayAge,displayCount,displaySampleTick,windowOffset,drawCallbackMs,renderScopeMs,pairedUpdateRenderMs"};
            string[] phases={"idle","attack","switch","restore","release"};
            for(int i=0;i<count;i++){var r=records[i];rows.Add(string.Join(",",r.Case,r.Moving,r.Aim||r.Path,phases[r.Phase],r.Frame,r.Tick,r.Elapsed*1000.0/Stopwatch.Frequency,r.Gc0,r.Gc1,r.Gc2,r.BudgetCurrent,r.BudgetTick,r.BudgetCurrent?r.Exceeded.ToString():"unavailable",r.BudgetCurrent?r.Budget.ToString():"unavailable",r.PathCount,r.Preview,r.Births,r.Assisted,r.Hits,r.Life,r.PlayerX,r.PlayerY,r.PlayerVx,r.PlayerVy,r.NpcX,r.NpcY,r.NpcVx,r.NpcVy,r.Exit,r.Aim,r.Path,r.DisplayCapture,r.Preview?r.Tick-r.DisplayCapture:-1,r.DisplayCount,r.DisplaySample,r.Preview?r.DisplaySample-r.DisplayCapture:-1,drawTiming?(r.DrawElapsed*1000.0/Stopwatch.Frequency).ToString():"unmeasured",drawTiming?(r.RenderElapsed*1000.0/Stopwatch.Frequency).ToString():"unmeasured",drawTiming?((r.Elapsed+r.RenderElapsed)*1000.0/Stopwatch.Frequency).ToString():"unmeasured"));}
            File.WriteAllLines(Path.Combine(output,"natural-cpu-frames.csv"),rows);
            string order=caseAllocations.Length==3?"Order: moving OFF/Path/Both, actual Draw CPU.":caseAllocations.Length==2?"Order: standing then moving original native damage, Aim/Path OFF.":caseAllocations.Length==8?"Order: standing OFF/Aim/Path/Both then moving OFF/Aim/Path/Both.":"Order: standing OFF, standing ON, moving OFF, moving ON.";
            string cursor=caseAllocations.Length==2?" Physical input follows the current original receiver centre, preserving a matched positive native first-hit cold control.":" Same physical world point930,1199 every frame.";
            var summary=new List<string>{drawTiming?"CPU update + actual World.Draw: callback and render scope timed separately; no readback/PNG.":"CPU only: NO Draw/GetData/PNG; pixel denominator294 unchanged but unmeasured here. Complete CpuStep timed; no player/RNG audit cloning, per-birth formatting or per-call probes. Adapter must not subscribe FirstChance or timed-call patches.",order+cursor+" Left use180..269/274..299; left/right40-frame rhythm; same original map/seed/AI/knockback/gravity/life/immunity. Cases share a process and JIT history; cold OFF/ON spikes are not independent paired cold observations.","Weather is fixed dayTime=false/raining=false/windPhysics=false; no native UpdateWeather/Main.DoUpdate loop. The separate real weather paired checks own that boundary.","Existing Reset's80 empty outer callbacks precede each fresh scene; no extra actual-player/NPC/forecast warmup. Initial measured frames are kept. Native damage changes OFF/ON futures; it is not restored.","GC/budget/path/preview readings occur outside Stopwatch interval; all frame numbers and complete phases preserved."};
            summary.Add("AppDomain MonitoringTotalAllocatedMemorySize start="+allocationStart+" end="+allocationEnd+" delta="+(allocationEnd-allocationStart)+" bytes; starts after records/bindings allocation and stops before output. Includes fixture setup, window-external numeric observations and background work, not pure product allocation. Budget key not equal GUC is unavailable and excluded from exceed counts.");
            if(caseAllocations.Length==8)summary.Add("Mode comparison order: standing OFF/Aim/Path/Both then moving OFF/Aim/Path/Both; Aim stays ON even before attack, Path stays as declared. Phase windows diagnose demand; the final schedule is identified separately and does not prove FPS.");
            if(longWindow)summary.Add("Final continuous window replaces the short schedule: idle0..419, attack420..839, switch840..843, restore844..869, release870..1289; stable idle/attack/release each exclude their first60 and retain360. All cold/setup frames remain in raw output; cases still share a process/JIT history. No FPS claim.");
            if(drawTiming)summary.Add("DRAW CPU mode: moving OFF/Path/Both, one actual World.Draw per native update. drawCallbackMs times only the bound product World.Draw inside the existing SpriteBatch; renderScopeMs additionally includes ProbeGraphics target/clear/batch submission. No GetData/PNG/readback, terrain/Npc Step counters or per-call attribution hooks. pairedUpdateRenderMs sums the TWO measured subintervals of EACH SAME frame; percentiles are computed from those paired sums, never by adding separate percentiles. This finite renderer fixture is not whole vanilla Main.Draw or FPS. GC spans both measured subintervals in this mode.");
            for(int c=0;c<caseAllocations.Length;c++)
            {
                var selected=records.Take(count).Where(r=>r.Case==c).ToArray();if(selected.Length==0)continue;
                foreach(string phase in new[]{"idle","cold0..9","attack","switch","restore","release","idle10..179 supplemental"})
                {
                    var phaseRows=selected.Where(r=>phase=="cold0..9"?r.Frame<10:phase=="idle10..179 supplemental"?r.Frame>=10 && r.Frame<180:phases[r.Phase]==phase).ToArray();if(phaseRows.Length==0)continue;var sorted=phaseRows.Select(r=>r.Elapsed*1000.0/Stopwatch.Frequency).OrderBy(v=>v).ToArray();summary.Add("case="+c+" phase="+phase+" n="+sorted.Length+" meanMs="+sorted.Average()+" p95Ms="+sorted[Math.Min(sorted.Length-1,(int)Math.Ceiling(sorted.Length*.95)-1)]+" p99Ms="+sorted[Math.Min(sorted.Length-1,(int)Math.Ceiling(sorted.Length*.99)-1)]+" maxMs="+sorted.Last()+" budgetExceeded="+phaseRows.Count(r=>r.BudgetCurrent && r.Exceeded)+" budgetUnavailable="+phaseRows.Count(r=>!r.BudgetCurrent)+" gc="+phaseRows.Sum(r=>r.Gc0)+"/"+phaseRows.Sum(r=>r.Gc1)+"/"+phaseRows.Sum(r=>r.Gc2));
                }
                var last=selected.Last();summary.Add("case="+c+" allocatedBytesIncludingFixtureAndObservation="+caseAllocations[c]+" nativeBirths="+last.Births+" assisted="+last.Assisted+" nativeHits="+last.Hits+" npcLife="+last.Life+" releaseFrames="+selected.Count(r=>r.Phase==4)+" exitComplete="+last.Exit+" (no forced Kill/timer reset)");
                if(longWindow)
                foreach(int stablePhase in new[]{0,1,4})
                {
                    int first=stablePhase==0?60:stablePhase==1?480:930;
                    var stable=selected.Where(r=>r.Phase==stablePhase && r.Frame>=first).ToArray();
                    var sorted=stable.Select(r=>r.Elapsed*1000.0/Stopwatch.Frequency).OrderBy(v=>v).ToArray();
                    if(sorted.Length>0)summary.Add("case="+c+" stablePhase="+phases[stablePhase]+" n="+sorted.Length+" meanMs="+sorted.Average()+" p95Ms="+sorted[(int)Math.Ceiling(sorted.Length*.95)-1]+" p99Ms="+sorted[(int)Math.Ceiling(sorted.Length*.99)-1]+" maxMs="+sorted.Last()+" budgetExceeded="+stable.Count(r=>r.BudgetCurrent && r.Exceeded)+" gc="+stable.Sum(r=>r.Gc0)+"/"+stable.Sum(r=>r.Gc1)+"/"+stable.Sum(r=>r.Gc2));
                    if(drawTiming)foreach(string part in new[]{"World.Draw","RenderScope","PairedUpdateRender"})
                    {var values=stable.Select(r=>(part=="World.Draw"?r.DrawElapsed:part=="RenderScope"?r.RenderElapsed:r.Elapsed+r.RenderElapsed)*1000.0/Stopwatch.Frequency).OrderBy(v=>v).ToArray();if(values.Length>0)summary.Add("case="+c+" stablePhase="+phases[stablePhase]+" part="+part+" n="+values.Length+" meanMs="+values.Average()+" p95Ms="+values[(int)Math.Ceiling(values.Length*.95)-1]+" p99Ms="+values[(int)Math.Ceiling(values.Length*.99)-1]+" maxMs="+values.Last());}
                }
            }
            File.WriteAllLines(Path.Combine(output,"natural-cpu-summary.txt"),summary);
        }
        private static void RunCore(object context,ProbeGraphics graphics,string output,bool referenceOnly,bool candidateOnly,bool timing=false,bool compareModes=false,bool nativeDamage=false,bool longWindow=false,bool drawTiming=false)
        {
            Require(graphics!=null,"natural slime red marker requires actual XNA pixels");
            Directory.CreateDirectory(output);
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var input=Get(context,"Input");var world=Get(host,"World");var attack=Get(combat,"Attack");
            var audit=new Harmony("JueMingR.Tests.NaturalSlimeImpact");
            bool originalFilm=Main.LocalPlayer.isControlledByFilm;
            var rows=new List<string>{"run,phase,frame,tick,expected,contact,visible,redPixels,pathCount,npcX,npcY,npcVx,npcVy,playerX,playerY,playerVx,playerVy,receiveTick,stop,selected,budgetExceeded,budgetMs,previewPhase,displayCaptureTick,displayAge,displayCount,rawPreviewContact,contactSource,displaySampleTick,windowOffset"};
            var failures=new List<string>();
            var invalidPresentation=new List<string>();
            var inputRows=new List<string>();birthRows.Clear();
            var pixelBuffer=timing?null:new Color[960*640];
            int caseCount=drawTiming?3:nativeDamage?2:compareModes?8:4;
            int idleEnd=longWindow?420:180,attackEnd=longWindow?840:270,switchEnd=longWindow?844:274,restoreEnd=longWindow?870:300,totalEnd=longWindow?1290:480;
            var cpuRecords=timing?new CpuRecord[caseCount*totalEnd]:null;int cpuCount=0,cpuCase=0;
            var cpuAllocations=timing?new long[caseCount]:null;long allocationStart=0,allocationEnd=0;
            Exception runFailure=null;
            var cleanupFailures=new List<Exception>();
            try
            {
                // Even a partial hook installation or binding failure owns
                // the same cleanup as a completed observation window.
                foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
                audit.Patch(typeof(Player).GetMethod("TryUpdateChannel",Flags),postfix:new HarmonyMethod(typeof(NativeCombatNaturalImpactChecks).GetMethod(nameof(Born),Flags)));
                audit.Patch(AccessTools.Method(typeof(Player),"Update",new[]{typeof(int)}),prefix:new HarmonyMethod(typeof(NativeCombatNaturalImpactChecks).GetMethod(nameof(MoveBefore),Flags)));
                audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatNaturalImpactChecks).GetMethod(nameof(Hit),Flags)));
                cpuMode=timing;
                var bound=timing?new CpuBindings(context,combat,host,input,attack):null;
                long timedDrawTicks=0;Func<bool> productDraw=drawTiming?(Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>),world,world.GetType().GetMethod("Draw",Flags)):null;
                Action timedDraw=drawTiming?(Action)(()=>{long start=Stopwatch.GetTimestamp();productDraw();timedDrawTicks=Stopwatch.GetTimestamp()-start;}):null;
                if(timing){AppDomain.MonitoringIsEnabled=true;allocationStart=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;}
                Set(host,"LayerStatus",Enum.Parse(Get(host,"LayerStatus").GetType(),"Ready"));Main.screenWidth=960;Main.screenHeight=640;Main.UIScale=1;
                foreach(bool moving in drawTiming?new[]{true}:new[]{false,true})
                {
                    // Both runs receive the same initial original state,
                    // weapon, map and seed, with their own actual identities.
                    // Damage legitimately changes the
                    // attack window's future; no AI/receive state is restored
                    // during a run to make the candidate agree with reference.
                    foreach(int configuration in drawTiming?new[]{0,2,3}:compareModes?new[]{0,1,2,3}:referenceOnly?new[]{0}:candidateOnly?new[]{3}:new[]{0,3})
                    {
                        bool assist=configuration!=0;
                        bool modeAim=compareModes && (configuration&1)!=0;
                        bool modePath=compareModes?(configuration&2)!=0:assist;
                        long caseAllocationStart=timing?AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize:0;
                        candidate=compareModes?modeAim:assist;moveLeft=moveRight=false;NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                        player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,98,0,0);
                        NativeCombatLiveContextChecks.FlightWorld();
                        // A different world size is an actual isolated world
                        // replacement. Replace the WorldFileData token as well
                        // as its persistent ID; ItemSessionProbe compares the
                        // object, and footprints bind dimensions at session start.
                        Main.ActiveWorldFileData=new Terraria.IO.WorldFileData(Path.Combine(Terraria.Program.SavePath,"natural-room.wld"),false){UniqueId=Guid.NewGuid()};
                        // Ordinary solid sidewalls bound the observation
                        // space. The first open-world reference naturally
                        // jumped beyond range (frame 260, native births 11,
                        // hits 2). This map keeps real AI/knockback/collision
                        // and free jump/landing, rather than freezing motion
                        // or filtering out candidate Contact failures.
                        // Reset's tiny-world floor 43 lies in vanilla's space
                        // gravity clamp (.075 instead of .3). Moving the map
                        // to tile 80 makes UpdateNPC_UpdateGravity naturally
                        // select ordinary surface gravity; no NPC field or
                        // physics/AI method is substituted.
                        for(int x=5;x<115;x++){Main.tile[x,43].ClearEverything();NativeToolsChecks.Tile(x,80,1);}
                        for(int y=55;y<80;y++){NativeToolsChecks.Tile(41,y,1);NativeToolsChecks.Tile(63,y,1);}
                        Console.WriteLine("NATURAL-MAP original Stone sidewalls x=41,63 y=55..79; Stone floor y=80/worldSurface=80, ordinary original height-derived gravity; same OFF/ON map; slime life=100000, player immunity for finite observation only");
                        player.position=new Vector2(700,1280-player.height);player.velocity=Vector2.Zero;player.isControlledByFilm=false;player.releaseUseItem=true;
                        player.statLife=player.statLifeMax=player.statLifeMax2=500;player.immune=true;player.immuneTime=100000;player.ResetFallDamage();
                        player.controlLeft=player.controlRight=player.controlUp=player.controlDown=player.controlJump=false;
                        for(int i=0;i<player.armor.Length;i++)player.armor[i].TurnToAir();Array.Clear(player.buffType,0,player.buffType.Length);Array.Clear(player.buffTime,0,player.buffTime.Length);
                        player.inventory[54].SetDefaults(97);player.inventory[54].stack=999;
                        Main.screenPosition=new Vector2(480,1000);Main.GameViewMatrix.Zoom=Vector2.One;Main.rand=new Terraria.Utilities.UnifiedRandom(7193);Main.dayTime=false;Main.windPhysics=false;Main.worldSurface=80;
                        int target=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),930,1280,1,Start:2,Target:player.whoAmI);
                        Require(target>=0 && target<Main.maxNPCs,"original natural-spawn entry supplies the ordinary blue slime");receiver=Main.npc[target];
                        // Increased life only extends observation. Native AI,
                        // gravity, dimensions, knockback and damage gates stay
                        // intact. Long player immunity prevents scene death.
                        receiver.life=receiver.lifeMax=100000;
                        Require(receiver.aiStyle==1 && !receiver.noGravity,"blue slime retains its locked-original natural AI and gravity");
                        Call(context,"UpdateRuntime");
                        var footprints=Get(context,"Footprints");
                        var identity=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Map.LocalWorldIdentity",true).GetMethod("Observe",Flags).Invoke(null,null);
                        Require(identity!=null && (long)Get(footprints,"Session")>=0 && (int)Get(footprints,"width")==400 && (int)Get(footprints,"height")==240 && Equals(Get(footprints,"pair"),identity),"the real runtime observes the new isolated world and its matching footprint pair/dimensions");
                        Console.WriteLine("NATURAL-WORLD id="+Main.ActiveWorldFileData.UniqueId+" footprintPair="+identity+" footprintDimensions="+Get(footprints,"width")+"x"+Get(footprints,"height"));
                        // The idle candidate has Path ON and Aim OFF. A red
                        // preview is independent of permission to borrow input.
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(false,modePath,false,false,false,25,assist,modeAim));
                        Require(((ObservationSettings)Get(host,"Settings")).Value.Path==modePath && ((ObservationSettings)Get(host,"Settings")).Value.Aim==modeAim,"completed saved settings match the declared idle runtime demand");
                        if(compareModes)Console.WriteLine("NATURAL-MODE case="+cpuCase+" moving="+moving+" aim="+modeAim+" path="+modePath);
                        births=assistedBirths=hits=0;int airborne=0,landings=0,normalGravitySteps=0,drawn=0,expected=0,missing=0,blank=0,longestBlank=0,gapRuns=0;float minPlayer=player.position.X,maxPlayer=minPlayer,priorVy=0;bool wasAirborne=false;
                        var phaseExpected=new int[3];var phaseDrawn=new int[3];
                        for(int frame=0;frame<(timing?totalEnd:300);frame++)
                        {
                            string phase=frame>=restoreEnd?"release":frame<idleEnd?"idle":frame<attackEnd?"attack":frame<switchEnd?"switch":"restore";
                            if(frame==idleEnd && assist && !compareModes)NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,true,true));
                            if(frame==attackEnd)player.inventory[0].SetDefaults(3509);
                            if(frame==switchEnd)player.inventory[0].SetDefaults(98);
                            moveRight=moving && frame%80<40;moveLeft=moving && !moveRight;
                            physical=timing?(nativeDamage?receiver.Center:new Vector2(930,1199)):assist?player.Center+new Vector2(receiver.Center.X>=player.Center.X?-220:220,-60):receiver.Center;
                            long elapsed=0,renderElapsed=0;int gc0=0,gc1=0,gc2=0;
                            if(timing)
                            {
                                gc0=GC.CollectionCount(0);gc1=GC.CollectionCount(1);gc2=GC.CollectionCount(2);
                                long stamp=Stopwatch.GetTimestamp();CpuStep(bound,physical,phase=="attack" || phase=="restore");elapsed=Stopwatch.GetTimestamp()-stamp;
                                if(drawTiming){long drawStart=Stopwatch.GetTimestamp();graphics.Render(timedDraw,Main.GameViewMatrix.ZoomMatrix);renderElapsed=Stopwatch.GetTimestamp()-drawStart;}
                                gc0=GC.CollectionCount(0)-gc0;gc1=GC.CollectionCount(1)-gc1;gc2=GC.CollectionCount(2)-gc2;
                            }
                            else NativeCombatSelectionPreviewChecks.Step(context,physical,phase=="attack" || phase=="restore",phase=="idle" || phase=="switch");
                            if(frame==12)
                            {
                                float gravity=(float)typeof(NPC).GetField("gravity",Flags).GetValue(null);
                                Require(Math.Abs(gravity-.3f)<.001f && Math.Abs(player.Bottom.Y-1280)<1 && Math.Abs(receiver.Bottom.Y-1280)<1,"original smoke: ordinary NPC gravity and player/NPC collision retain the real floor before the full window");
                                if(!timing)Console.WriteLine("NATURAL-SMOKE moving="+moving+" assist="+assist+" gravity="+gravity+" playerBottom="+player.Bottom.Y+" npcBottom="+receiver.Bottom.Y+" world="+Main.maxTilesX+"x"+Main.maxTilesY);
                            }
                            minPlayer=Math.Min(minPlayer,player.position.X);maxPlayer=Math.Max(maxPlayer,player.position.X);
                            bool flying=receiver.velocity.Y!=0;if(flying)airborne++;if(wasAirborne && !flying)landings++;
                            if(frame<idleEnd && flying && wasAirborne && Math.Abs(receiver.velocity.Y-priorVy-.3f)<.001f)normalGravitySteps++;
                            wasAirborne=flying;priorVy=receiver.velocity.Y;
                            Require(receiver.active && receiver.type==1 && receiver.aiStyle==1 && receiver.life>0 && !receiver.dontTakeDamage && !receiver.immortal && !receiver.friendly,"declared natural receiver remains legal throughout the fixed window");
                            if(timing)
                            {
                                Require(Vector2.Distance(player.Center,receiver.Center)<380,"CPU original close-range room remains intact on both OFF/ON sides");
                                bool exited=frame>=restoreEnd && CpuExited(combat);
                                ulong budgetTick=bound.BudgetTick();bool budgetCurrent=budgetTick==Main.GameUpdateCount;
                                var settings=((ObservationSettings)Get(host,"Settings")).Value;
                                var display=bound.Preview();
                                cpuRecords[cpuCount++]=new CpuRecord{Case=cpuCase,Moving=moving,Aim=settings.Aim,Path=settings.Path,DisplayCapture=display?.Timeline.CaptureTick??-1,DisplaySample=display?.Timeline.SampleTick??-1,DisplayCount=display?.Timeline.Count??0,Frame=frame,Phase=frame<idleEnd?0:frame<attackEnd?1:frame<switchEnd?2:frame<restoreEnd?3:4,Tick=Main.GameUpdateCount,Elapsed=elapsed,DrawElapsed=timedDrawTicks,RenderElapsed=renderElapsed,Gc0=gc0,Gc1=gc1,Gc2=gc2,BudgetTick=budgetTick,BudgetCurrent=budgetCurrent,Exceeded=budgetCurrent && bound.Exceeded(),Budget=budgetCurrent?bound.BudgetMilliseconds():double.NaN,PathCount=bound.Cache.Read(0)?.Count??0,Preview=display!=null,Births=births,Assisted=assistedBirths,Hits=hits,Life=receiver.life,PlayerX=player.position.X,PlayerY=player.position.Y,PlayerVx=player.velocity.X,PlayerVy=player.velocity.Y,NpcX=receiver.position.X,NpcY=receiver.position.Y,NpcVx=receiver.velocity.X,NpcVy=receiver.velocity.Y,Exit=exited};
                                // Keep at least60 real release steps. If a live
                                // source/hand remains, allow up to180 and report
                                // unfinished exit instead of killing/resetting it.
                                if(!longWindow && frame>=359 && exited)break;
                                continue;
                            }
                            string state=" phase="+phase+" frame="+frame+" npc="+receiver.position+" velocity="+receiver.velocity+" player="+player.position+" playerVelocity="+player.velocity+" births="+births+" hits="+hits;
                            if(!assist)rows.Add(string.Join(",","original-"+(moving?"moving":"standing"),phase,frame,Main.GameUpdateCount,false,false,false,0,0,receiver.position.X,receiver.position.Y,receiver.velocity.X,receiver.velocity.Y,player.position.X,player.position.Y,player.velocity.X,player.velocity.Y,-1,"reference",false,"unavailable","unavailable","reference"));
                            Require(Vector2.Distance(player.Center,receiver.Center)<380,"declared open close-range window remains inside its independent 25-tile range"+state);
                            if(!assist)continue;
                            // The only excluded acquisition is the first two
                            // declared startup updates. Neither path nor
                            // Contact controls this expected-show denominator.
                            bool should=frame>=2 && phase!="switch";
                            var rawPreview=(AttackContact)GetOptional(attack,"ExpectedPreview");var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");
                            graphics.Render(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);graphics.ReadRenderedPixels(pixelBuffer);var pixels=pixelBuffer;
                            var red=RedPixels(pixels);
                            var marker=Get(world,"Impact");var contact=(AttackContact)GetOptional(marker,"contact");bool visible=(bool)Get(marker,"Visible");
                            string contactSource=contact==null?"absent":(bool)Get(marker,"execution")?"execution":"preview";
                            var work=GetOptional(host,"PredictionWork");var plan=GetOptional(attack,"preview");
                            bool selected=(bool)Get(Get(host,"Selection"),"HasTarget");
                            string budget=work==null?"unavailable":Convert.ToDouble(Get(work,"UsedMilliseconds")).ToString("R",System.Globalization.CultureInfo.InvariantCulture);
                            if(frame>=10 && frame<=20 && Environment.GetEnvironmentVariable("JMR_NATURAL_LEASE_DIAG")=="1")
                            {
                                var shown=GetOptional(host,"DisplayPath") as NpcTrajectory;var planned=GetOptional(plan,"Timeline") as NpcTrajectory;
                                inputRows.Add("LEASE "+(moving?"moving":"standing")+" frame="+frame+" tick="+Main.GameUpdateCount+" optional="+GetOptional(host,"OptionalPreparation")+" usedMs="+budget+" display="+(shown!=null)+" displaySample="+(shown?.SampleTick??-1)+" displayCount="+(shown?.Count??0)+" rawPlanSample="+(planned?.SampleTick??-1)+" rawPlanCount="+(planned?.Count??0)+" sameCalculation="+(shown!=null && planned!=null && ReferenceEquals(shown.CalculationToken,planned.CalculationToken))+" previewStep="+GetOptional(attack,"previewStep")+" rawPlanContact="+(GetOptional(plan,"Contact")!=null)+" qualifiedPreview="+(rawPreview!=null)+" actualContact="+(contact!=null)+" source="+contactSource);
                            }
                            rows.Add(string.Join(",",moving?"moving":"standing",phase,frame,Main.GameUpdateCount,should,contact!=null,visible,red.Count,cache.Read(0)?.Count??0,receiver.position.X,receiver.position.Y,receiver.velocity.X,receiver.velocity.Y,player.position.X,player.position.Y,player.velocity.X,player.velocity.Y,contact?.ReceiveTick??-1,cache.Read(0)?.Stop.ToString()??"empty",selected,work==null?"unavailable":Get(work,"Exceeded").ToString(),budget,plan==null?"empty":GetOptional(plan,"Phase")?.ToString()??"empty",contact?.Timeline.CaptureTick??-1,contact==null?-1:Main.GameUpdateCount-contact.Timeline.CaptureTick,contact?.Timeline.Count??0,rawPreview!=null,contactSource,contact?.Timeline.SampleTick??-1,contact==null?-1:contact.Timeline.SampleTick-contact.Timeline.CaptureTick));
                            if(frame>=178 && frame<=182 || should && contact==null && missing==0)
                            {
                                var consumed=GetOptional(attack,"consumedInput");var targetIdentity=(NpcIdentity)Get(Get(host,"Selection"),"Target");
                                inputRows.Add((moving?"moving":"standing")+"/"+phase+"/"+frame+" tick="+Main.GameUpdateCount+" target="+targetIdentity.Session+"/"+targetIdentity.Slot+"/"+targetIdentity.Generation+"/"+targetIdentity.Type+"/"+targetIdentity.NetId+" sameReceiver="+ReferenceEquals(targetIdentity.Token,receiver)+" current="+(GetOptional(attack,"current")!=null)+" next="+(GetOptional(attack,"next")!=null)+" rawPreviewContact="+(GetOptional(plan,"Contact")!=null)+" flight="+(GetOptional(plan,"Flight")!=null)+" controlled="+GetOptional(plan,"Controlled")+" previewCompatible="+Call(attack,"PreviewInputCompatible")+" hasConsumed="+GetOptional(attack,"hasConsumedInput")+" consumedKind="+GetOptional(consumed,"Kind")+" consumedPoint="+GetOptional(consumed,"Point")+" consumedStep="+GetOptional(attack,"consumedInputStep")+" actualTick="+Main.GameUpdateCount+" physical="+physical);
                            }
                            if(should)
                            {
                                expected++;
                                int displayPhase=phase=="idle"?0:phase=="attack"?1:2;phaseExpected[displayPhase]++;
                                bool valid=contact!=null && visible && red.Count>=16 && red.Count<=100;
                                if(valid)
                                {
                                    // Display can hold an older conditional prefix; its own
                                    // immutable timeline owns receive geometry. It must still
                                    // belong to the current real receiver, never the cache's
                                    // newer attack publication or an unrelated stale target.
                                    valid=ReferenceEquals(contact.Timeline.Identity.Token,receiver) && contact.ReceiveTick>=0 && contact.ReceiveTick<contact.Timeline.Count;
                                    var b=contact.Timeline[contact.ReceiveTick].Bounds;var center=Vector2.Transform(new Vector2(b.CenterX,b.CenterY)-Main.screenPosition,Main.GameViewMatrix.ZoomMatrix);
                                    valid&=red.All(v=>Math.Abs(v.X-center.X)<=6 && Math.Abs(v.Y-center.Y)<=6);
                                }
                                // Keep every declared opportunity in the denominator.
                                // Occasional absence is allowed by the owner; incorrect
                                // displayed identity/geometry remains a strict failure.
                                if(valid){drawn++;phaseDrawn[displayPhase]++;blank=0;}
                                else
                                {
                                    if(blank==0)gapRuns++;
                                    missing++;longestBlank=Math.Max(longestBlank,++blank);
                                    string failure=(moving?"moving":"standing")+"/"+phase+"/"+frame+" tick="+Main.GameUpdateCount+" selected="+selected+" path="+(cache.Read(0)?.Count??0)+" contact="+(contact!=null)+" visible="+visible+" red="+red.Count+" budgetExceeded="+(work==null?"unavailable":Get(work,"Exceeded").ToString())+" usedMs="+budget;
                                    failures.Add(failure);
                                    if(visible || red.Count!=0)invalidPresentation.Add(failure);
                                    if(missing==1){Console.WriteLine("NATURAL-FIRST-FAIL "+failure);graphics.Image(Path.Combine(output,(moving?"moving":"standing")+"-first-failure.png"),()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);}
                                }
                            }
                            else
                            {
                                blank=0;
                                if(phase=="switch" && (contact!=null || visible || red.Count!=0))invalidPresentation.Add((moving?"moving":"standing")+"/switch/"+frame+" old precise marker survived weapon switch");
                            }
                            if(frame==90 || frame==210 || frame==299)graphics.Image(Path.Combine(output,(moving?"moving":"standing")+"-"+phase+".png"),()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);
                        }
                        if(!timing)Console.WriteLine("NATURAL-IMPACT moving="+moving+" assist="+assist+" expected="+expected+" visible="+drawn+" missing="+missing+" longestBlank="+longestBlank+" gapRuns="+gapRuns+" idle="+phaseDrawn[0]+"/"+phaseExpected[0]+" attack="+phaseDrawn[1]+"/"+phaseExpected[1]+" restore="+phaseDrawn[2]+"/"+phaseExpected[2]+" airborne="+airborne+" landings="+landings+" normalGravitySteps="+normalGravitySteps+" playerDelta="+(maxPlayer-minPlayer)+" births="+births+" assistedBirths="+assistedBirths+" nativeHits="+hits);
                        if(!timing && assist)Require(phaseDrawn[0]>0 && phaseDrawn[1]>0 && phaseDrawn[2]>0,"Natural idle/attack/restore each make real presentation progress; coverage, gap duration and flicker still require the task's availability assessment.");
                        Require(airborne>10 && landings>0 && normalGravitySteps>10,"natural slime actually jumps and lands with ordinary original gravity during the fixed observation window");
                        Require(!moving || maxPlayer-minPlayer>30,"moving scene contains real continuous original player movement");
                        bool expectedAssistance=compareModes?modeAim:assist;
                        Require(births>=2 && (timing && !nativeDamage && !expectedAssistance || hits>0) && (expectedAssistance?assistedBirths==births:assistedBirths==0),timing?"CPU paired input produces native births; enabled aim or explicit native damage reference receives damage, OFF retains its raw aim":"explicit original and assisted gun scenes both produce real sources and positive native damage");
                        if(timing){cpuAllocations[cpuCase]=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-caseAllocationStart;cpuCase++;}
                    }
                }
                if(timing)return;
                if(referenceOnly){Console.WriteLine("PASS independent original standing/moving blue-slime gun reference, fixed near-range windows, natural jump/landing and native birth/damage");return;}
                // An independently unreachable short blade has no credible
                // damage shape at this distance even if selection is legal.
                player.inventory[0].SetDefaults(3507);moveLeft=moveRight=false;player.controlLeft=player.controlRight=false;
                receiver.position=new Vector2(player.position.X+350,1280-receiver.height);
                for(int frame=0;frame<12;frame++)
                {
                    physical=player.Center-new Vector2(220,60);NativeCombatSelectionPreviewChecks.Step(context,physical,false,true);
                    Require(Vector2.Distance(player.Center,receiver.Center)>220,"short-blade negative window remains independently beyond its native reach");
                    Require((bool)Get(Get(host,"Selection"),"HasTarget"),"unreachable target remains selected");
                    graphics.Render(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);graphics.ReadRenderedPixels(pixelBuffer);var pixels=pixelBuffer;
                    Require(GetOptional(attack,"ExpectedPreview")==null && !pixels.Any(Red),"unreachable natural receiver has no invented contact or red pixels");
                }
                Require(invalidPresentation.Count==0,"natural slime complete declared windows contain "+invalidPresentation.Count+" incorrectly displayed frames; first="+(invalidPresentation.Count==0?"none":invalidPresentation[0]));
                Console.WriteLine("PASS natural blue slime display identity/geometry, native gun damage and unreachable blade negative; absence metrics retain the full denominator and require task availability assessment, not a294/294 gate. Missing observations="+failures.Count);
            }
            catch(Exception failure){runFailure=failure;throw;}
            finally
            {
                // Evidence I/O and each restoration are independent: one
                // failure must not retain hooks, references or film ownership.
                // Preserve all original exceptions rather than masking the run.
                try{if(timing){allocationEnd=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;WriteCpu(output,cpuRecords,cpuCount,allocationStart,allocationEnd,cpuAllocations,longWindow,drawTiming);}else{File.WriteAllLines(Path.Combine(output,"natural-slime-frames.csv"),rows);File.WriteAllLines(Path.Combine(output,"natural-input-facts.txt"),inputRows);File.WriteAllLines(Path.Combine(output,"natural-births.txt"),birthRows);}}
                catch(Exception failure){cleanupFailures.Add(failure);}
                try{audit.UnpatchAll(audit.Id);}catch(Exception failure){cleanupFailures.Add(failure);}
                try{Main.LocalPlayer.isControlledByFilm=originalFilm;}
                catch(Exception failure){cleanupFailures.Add(failure);}
                finally{cpuMode=false;player=null;receiver=null;}
                try{NativeCombatObservationChecks.Save(host,new ObservationOptions());}catch(Exception failure){cleanupFailures.Add(failure);}
                try{NativeCombatCadenceChecks.Save(combat,new CombatOptions());}catch(Exception failure){cleanupFailures.Add(failure);}
                if(cleanupFailures.Count>0)
                {
                    if(runFailure!=null)cleanupFailures.Insert(0,runFailure);
                    throw new AggregateException("Natural observation cleanup failed; original run and cleanup errors are retained.",cleanupFailures);
                }
            }
        }
    }
}
