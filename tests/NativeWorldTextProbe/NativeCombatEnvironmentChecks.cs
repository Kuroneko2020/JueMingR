using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace NativeWorldTextProbe
{
    // Explicit investigation, not a coverage PASS: original actors advance
    // normally and all non-publication stages are retained for causal analysis.
    internal static class NativeCombatEnvironmentChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static void Run(object context,object native,NpcPredictionCache cache,Action step,string output)
        {
            var selection=Get(Get(context,"CombatObservation"),"Selection");
            string axis=Environment.GetEnvironmentVariable("JUEMINGR_NPC_ENV_AXIS")??"bunnies";
            if(axis=="query-boundary"){NativeCombatQueryBoundaryChecks.Run(context,output);return;}
            if(axis=="conditional-candidate"){NativeCombatConditionalChecks.Run(context,native,cache,step,output);return;}
            if(axis=="conditional-scope"){NativeCombatConditionalChecks.CheckGeometryScope(native);return;}
            if(axis=="route-feasibility"){Continuous(context,native,cache,step,output);return;}
            if(axis=="failure-recovery"){NativeCombatFailureRecoveryChecks.Run(context,native,cache,step,output);return;}
            if(axis=="terrain-relevance"){NativeCombatTerrainRelevanceChecks.Run(context,native,cache,step,output);return;}
            int[] types={42,176,110,175,153,51,56,674};
            string selectedTypes=Environment.GetEnvironmentVariable("JUEMINGR_NPC_ENV_TYPES");
            if(!string.IsNullOrEmpty(selectedTypes))types=selectedTypes.Split(',').Select(int.Parse).ToArray();
            var rows=new List<string>{"type,background,frame,selected,shown,alive,activeNpcs,activeProjectiles,x,y,vx,vy,ai0,ai1,ai2,ai3,reason,selectedSlot,selectedType,dependencyNpcs,dependencyProjectiles,extraChunks,timeLeft,mouseDistance,friendly,failed,workerState"};
            bool hard=Main.hardMode;
            try
            {
                Main.hardMode=true;
                Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();
                Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);
                foreach(int type in types)foreach(int background in (axis=="pet" || axis=="liquid" || axis=="mouse"?new[]{0,1}:new[]{0,8}))
                {
                    NativeCombatLiveContextChecks.FlightWorld();
                    foreach(var n in Main.npc)n.active=false;foreach(var p in Main.projectile)p.active=false;
                    var player=Main.LocalPlayer;
                    player.controlLeft=player.controlRight=player.controlUp=player.controlDown=player.controlJump=false;
                    player.position=new Vector2(axis=="mouse"?1400:1100,2400-player.height);player.velocity=Vector2.Zero;
                    player.dead=false;player.wet=player.honeyWet=player.lavaWet=player.shimmerWet=false;
                    player.fallStart=player.fallStart2=(int)(player.position.Y/16);player.statLife=player.statLifeMax=player.statLifeMax2=400;
                    player.immune=true;player.immuneTime=100000;for(int i=0;i<player.hurtCooldowns.Length;i++)player.hurtCooldowns[i]=100000;
                    typeof(Main).GetField("_rngs",Flags).SetValue(null,new Dictionary<string,UnifiedRandom>{{"UpdatePlayers",new UnifiedRandom(531)},{"UpdateNPCs",new UnifiedRandom(879)},{"UpdateProjectiles",new UnifiedRandom(171)}});
                    Array.Clear(player.buffType,0,player.buffType.Length);Array.Clear(player.buffTime,0,player.buffTime.Length);
                    if(axis=="pet" && background!=0)player.AddBuff(BuffID.PetBunny,18000);
                    if(axis=="bunnies" || axis=="hostiles" || axis=="bees")for(int i=0;i<background;i++)
                        NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),axis=="bunnies"?300+i*25:2700+i*30,axis=="bunnies"?2400:2200,axis=="bunnies"?NPCID.Bunny:axis=="bees"?210:i%2==0?42:176,Start:i);
                    if(axis=="liquid")
                    {
                        Main.Map=new Terraria.Map.WorldMap(Main.maxTilesX,Main.maxTilesY);
                        Main.liquid=new Liquid[Liquid.maxLiquid];Main.liquidBuffer=new LiquidBuffer[Liquid.maxLiquidBuffer];
                        Liquid.ReInit();LiquidBuffer.numLiquidBuffer=0;
                        for(int tx=129;tx<=136;tx++)for(int ty=100;ty<=115;ty++)
                        {var tile=Main.tile[tx,ty];bool edge=tx==129 || tx==136 || ty==115;tile.active(edge);tile.type=1;if(!edge && ty>=101)tile.liquid=255;}
                    }
                    int x=1500,y=type==42 || type==176 || type==51?2200:2400;
                    if(type==175 || type==56){Main.tile[93,149].active(true);Main.tile[93,149].type=60;}
                    int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),x,y,type,Start:16,ai0:type==175 || type==56?93:0,ai1:type==175 || type==56?149:0,Target:Main.myPlayer);
                    Vector2 initialMouse=Main.npc[slot].Center;
                    if(axis=="mouse")
                    {NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions(collision:true,path:true,clearLine:false,mouseCenter:true,dummy:true,radius:25));Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();}
                    var reasons=new Dictionary<string,int>();int shown=0,selected=0,first=-1,gaps=0,longest=0,blank=0,shots=0;bool previous=false;
                    long requests=(long)Get(native,"Requests"),rejected=(long)Get(native,"Rejected"),refused=(long)Get(native,"Refused");
                    for(int frame=0;frame<600;frame++)
                    {
                        if(axis=="mouse")SampleMouse(context,background==0?initialMouse:Main.npc[slot].Center);
                        if(axis=="liquid" && background!=0)
                        {
                            if(frame==120)
                            {
                                var terrain=Get(native,"terrain");var tile=Main.tile[132,104];byte before=tile.bTileHeader3;byte amount=tile.liquid;string otherFields=TileFieldsExceptHeader3(tile);
                                var key=(NpcIdentity)Get(selection,"Target");var isCurrent=terrain.GetType().GetMethod("IsCurrent",Flags);
                                bool was=(bool)isCurrent.Invoke(terrain,new object[]{key.Session});
                                WorldGen.TileFrame(132,104,false,false);
                                Console.WriteLine("LIQUID-FRAME before-current="+was+" after-current="+isCurrent.Invoke(terrain,new object[]{key.Session})+" header3="+before+"->"+tile.bTileHeader3+" liquid="+amount+"->"+tile.liquid+" active="+tile.active()+" other-eight-fields-unchanged="+(otherFields==TileFieldsExceptHeader3(tile))+" path-before="+(cache.Read(0)!=null));
                                Main.tile[132,115].active(false);for(int tx=130;tx<=135;tx++)for(int ty=101;ty<115;ty++)Liquid.AddWater(tx,ty);
                            }
                            if(frame>=120)Liquid.UpdateLiquid();
                        }
                        step();var n=Main.npc[slot];var path=cache.Read(0);
                        bool owns=(bool)Get(selection,"HasTarget") && ((NpcIdentity)Get(selection,"Target")).Slot==slot;
                        bool present=path!=null && path.Identity.Slot==slot;
                        if(owns)selected++;if(present){shown++;if(first<0)first=frame;blank=0;}else if(owns)longest=Math.Max(longest,++blank);else blank=0;
                        if(previous && !present && owns)gaps++;previous=present;
                        int projectileCount=Main.projectile.Count(p=>p.active);if(projectileCount>0)shots++;
                        string reason=(string)Get(native,"Reason")??"none";int count;reasons.TryGetValue(reason,out count);reasons[reason]=count+1;
                        var target=(NpcIdentity)Get(selection,"Target");
                        var mouse=(Vector2)Get(selection,"RealMouse");float dx=mouse.X-MathHelper.Clamp(mouse.X,n.Hitbox.Left,n.Hitbox.Right),dy=mouse.Y-MathHelper.Clamp(mouse.Y,n.Hitbox.Top,n.Hitbox.Bottom);
                        rows.Add(string.Join(",",type,background,frame,owns?1:0,present?1:0,n.active?1:0,Main.npc.Count(a=>a.active),projectileCount,n.position.X,n.position.Y,n.velocity.X,n.velocity.Y,n.ai[0],n.ai[1],n.ai[2],n.ai[3],"\""+reason.Replace("\"","\"\"")+"\"",target.Slot,target.Type,Get(Get(native,"npcs"),"Count"),Get(Get(native,"projectiles"),"Count"),Get(Get(native,"extraChunks"),"Count"),n.timeLeft,Math.Sqrt(dx*dx+dy*dy),n.friendly?1:0,(bool)Get(native,"Failed")?1:0,Get(Get(native,"Worker"),"State")));
                    }
                    Console.WriteLine("ENV type="+type+" axis="+axis+" background="+background+" slot="+slot+" selected="+selected+" shown="+shown+" first="+first+" longest="+longest+" gaps="+gaps+" projectile-updates="+shots+" requests="+((long)Get(native,"Requests")-requests)+" rejected="+((long)Get(native,"Rejected")-rejected)+" refused="+((long)Get(native,"Refused")-refused));
                    foreach(var pair in reasons.OrderByDescending(p=>p.Value).Take(12))Console.WriteLine("ENV-REASON frames="+pair.Value+" "+pair.Key);
                    File.WriteAllLines(Path.Combine(output,"environment-windows.csv"),rows);
                }
            }
            finally{Main.hardMode=hard;File.WriteAllLines(Path.Combine(output,"environment-windows.csv"),rows);}
            Console.WriteLine("INVESTIGATION COMPLETE: environment cases recorded; no product PASS inferred.");
        }
        private static object Get(object owner,string name)
        {var field=owner.GetType().GetField(name,Flags);return field!=null?field.GetValue(owner):owner.GetType().GetProperty(name,Flags).GetValue(owner);}
        private static string TileFieldsExceptHeader3(Tile tile)
        {return string.Join(",",tile.type,tile.wall,tile.liquid,tile.sTileHeader,tile.bTileHeader,tile.bTileHeader2,tile.frameX,tile.frameY);}
        private static void SampleMouse(object context,Vector2 point)
        {
            // Isolated input fixture only; never moves the operating-system cursor.
            Main.screenPosition=point-new Vector2(Main.screenWidth/2,Main.screenHeight/2);
            var input=Get(context,"Input");Call(input,"BeginUpdate");
            Terraria.GameInput.PlayerInput.MouseInfo=new Microsoft.Xna.Framework.Input.MouseState(Main.screenWidth/2,Main.screenHeight/2,0,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released);
            Call(input,"AfterNativeMouse",new List<string>());Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");Call(Get(context,"CombatObservation"),"SampleMouse");
        }
        private static void Call(object owner,string name,params object[] args){owner.GetType().GetMethod(name,Flags).Invoke(owner,args);}

        // Explicit, bounded investigation only. These hooks observe the original
        // Receive boundary; they never alter its arguments, result or acceptance.
        // Diagnostics add work, so measured costs are attributed to this fixture.
        private sealed class FutureSample
        {
            internal string Source,Outcome,Phase;
            internal NpcIdentity Identity;
            internal long Capture,Sample,Due;
            internal int Horizon,Selection,Environment,NearTerrain,Life,PlayerLife;
            internal Vector2 Predicted,Linear;
        }
        private static object routeNative;
        private static int routeFrame,routeSelection,routeEnvironment,routeNearTerrain;
        private static string routePhase;
        private static double prepareMs;
        private static readonly List<FutureSample> futures=new List<FutureSample>();
        private static readonly List<string> futureRows=new List<string>(),replyRows=new List<string>();
        private static readonly int[] horizons={15,30,60,90,120};
        private static readonly string[] costFields={"CaptureMs","EncodeMs","ExchangeMs","DecodeMs","AcceptMs","TotalMs","ResetMs","RestoreMs","AdvanceMs","Bytes","ReplyBytes"};
        private static string Csv(params object[] values)=>string.Join(",",values.Select(v=>"\""+Convert.ToString(v,CultureInfo.InvariantCulture).Replace("\"","\"\"")+"\""));
        private static double Milliseconds(long value)=>value*1000.0/Stopwatch.Frequency;
        private static void PrepareStarting(object __instance,ref long __state)
        {if(ReferenceEquals(__instance,routeNative))__state=Stopwatch.GetTimestamp();}
        private static void PrepareFinished(long __state)
        {if(__state!=0)prepareMs+=Milliseconds(Stopwatch.GetTimestamp()-__state);}
        private static void ReceiveStarting(object __instance,object response,ref object[] __state)
        {if(ReferenceEquals(__instance,routeNative))__state=new[]{Get(__instance,"pending"),Get(response,"Result")};}
        private static void ReceiveFinished(object __instance,long tick,object[] __state)
        {
            if(__state==null || __state[0]==null)return;
            object request=__state[0],result=__state[1];
            object measurement=((IEnumerable)Get(__instance,"Measurements")).Cast<object>().LastOrDefault();
            string outcome=measurement==null?"measurement unavailable":(string)Get(measurement,"Outcome");
            long capture=(long)Get(request,"Tick");var identity=(NpcIdentity)Get(request,"Identity");
            replyRows.Add(Csv(new object[]{routeFrame,routePhase,capture,tick,identity.Slot,identity.Generation,identity.Type,outcome,Get(result,"Error")}
                .Concat(costFields.Select(f=>measurement==null?null:Get(measurement,f))).ToArray()));
            var path=(NpcTrajectory)Get(result,"Trajectory");
            if(path!=null)SampleFuture("candidate",outcome,path,identity,capture,tick);
        }
        private static void SampleFuture(string source,string outcome,NpcTrajectory path,NpcIdentity identity,long capture,long sample)
        {
            NPC npc=Main.npc[identity.Slot];
            foreach(int horizon in horizons)
            {
                long index=sample-path.SampleTick+horizon;
                if(index<0 || index>=path.Count)
                {futureRows.Add(Csv(source,outcome,routePhase,capture,sample,identity.Slot,identity.Generation,identity.Type,horizon,sample+horizon,"insufficient remaining horizon",null,null,null,null,null,null,null));continue;}
                var bounds=path[(int)index].Bounds;
                futures.Add(new FutureSample{Source=source,Outcome=outcome,Phase=routePhase,Identity=identity,Capture=capture,Sample=sample,Due=sample+horizon,Horizon=horizon,
                    Selection=routeSelection,Environment=routeEnvironment,NearTerrain=routeNearTerrain,Life=npc.life,PlayerLife=Main.LocalPlayer.statLife,
                    Predicted=new Vector2(bounds.X,bounds.Y),Linear=npc.position+npc.velocity*horizon});
                if(futures.Count>1500)throw new InvalidOperationException("Bounded prospective samples exceeded.");
            }
        }
        private static void CompleteFutures(bool ending=false)
        {
            long tick=Main.GameUpdateCount;
            for(int i=futures.Count-1;i>=0;i--)
            {
                var sample=futures[i];if(!ending && sample.Due>tick)continue;
                NPC npc=Main.npc[sample.Identity.Slot];
                bool same=ReferenceEquals(npc,sample.Identity.Token) && npc.generation==sample.Identity.Generation && npc.type==sample.Identity.Type && npc.netID==sample.Identity.NetId && npc.active;
                var changes=new List<string>();
                if(ending && sample.Due>tick)changes.Add("run ended before horizon");
                if(!same)changes.Add("lifecycle/instance changed");
                if(sample.Selection!=routeSelection)changes.Add("selection changed");
                if(sample.Environment!=routeEnvironment)changes.Add("far environment changed");
                if(sample.NearTerrain!=routeNearTerrain)changes.Add("near terrain changed");
                if(npc.life!=sample.Life || Main.LocalPlayer.statLife!=sample.PlayerLife)changes.Add("hit/life changed");
                bool evaluable=same && sample.Due==tick;
                futureRows.Add(Csv(sample.Source,sample.Outcome,sample.Phase,sample.Capture,sample.Sample,sample.Identity.Slot,sample.Identity.Generation,sample.Identity.Type,sample.Horizon,sample.Due,
                    changes.Count==0?"unchanged":string.Join(";",changes),sample.Predicted.X,sample.Predicted.Y,evaluable?(object)npc.position.X:null,evaluable?(object)npc.position.Y:null,
                    evaluable?(object)Vector2.Distance(sample.Predicted,npc.position):null,evaluable?(object)Vector2.Distance(sample.Linear,npc.position):null,evaluable?1:0));
                futures.RemoveAt(i);
            }
        }
        internal static void Continuous(object context,object native,NpcPredictionCache cache,Action step,string output,bool conditional=false)
        {
            int frames=conditional?1440:2400;
            var host=Get(context,"CombatObservation");var selection=Get(host,"Selection");
            var worker=Get(native,"Worker");int child=(int)Get(worker,"ChildId");
            routeNative=native;routeSelection=routeEnvironment=routeNearTerrain=0;futures.Clear();futureRows.Clear();replyRows.Clear();
            NativeCombatConditionalChecks.RolePeak=NativeCombatConditionalChecks.HarpyConsumed=0;
            var rows=new List<string>{"frame,phase,tick,desiredSlot,selectedSlot,selectedType,selectedCorrect,shown,childId,workerState,failed,requests,rejected,refused,npcPages,projectilePages,extraChunks,terrainChunks,activeBackground,activeProjectiles,newProjectiles,projectileUpdateFrames,x,y,vx,vy,life,playerLife,prepareMs,stepWallMs,tilesCompared,tilesCaptured,reason,fullAiRoles,fullAiActive,remainingFuture"};
            var backgrounds=new List<int>();var seenProjectiles=new HashSet<string>();int projectileUpdateFrames=0,secondary=-1,desired=-1;
            bool oldHard=Main.hardMode;var patches=new Harmony("JueMingR.Tests.RouteFeasibility");
            try
            {
                // One world/actor/RNG initialization; phase changes below use
                // this same world, monotonically advancing tick and native owner.
                NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.hardMode=true;
                Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();
                Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);
                var player=Main.LocalPlayer;player.controlLeft=player.controlRight=player.controlUp=player.controlDown=player.controlJump=false;
                player.position=new Vector2(1100,2400-player.height);player.velocity=Vector2.Zero;player.dead=false;
                player.wet=player.honeyWet=player.lavaWet=player.shimmerWet=false;player.fallStart=player.fallStart2=player.position.ToPoint().Y/16;
                player.statLife=player.statLifeMax=player.statLifeMax2=400;player.immune=true;player.immuneTime=100000;
                for(int i=0;i<player.hurtCooldowns.Length;i++)player.hurtCooldowns[i]=100000;
                Array.Clear(player.buffType,0,player.buffType.Length);Array.Clear(player.buffTime,0,player.buffTime.Length);
                int npcSeed; if(!int.TryParse(Environment.GetEnvironmentVariable("JUEMINGR_NPC_CONDITIONAL_SEED"),out npcSeed))npcSeed=879;
                typeof(Main).GetField("_rngs",Flags).SetValue(null,new Dictionary<string,UnifiedRandom>{{"UpdatePlayers",new UnifiedRandom(531)},{"UpdateNPCs",new UnifiedRandom(npcSeed)},{"UpdateProjectiles",new UnifiedRandom(171)}});
                Main.Map=new Terraria.Map.WorldMap(Main.maxTilesX,Main.maxTilesY);Main.liquid=new Liquid[Liquid.maxLiquid];Main.liquidBuffer=new LiquidBuffer[Liquid.maxLiquidBuffer];Liquid.ReInit();LiquidBuffer.numLiquidBuffer=0;
                for(int tx=129;tx<=136;tx++)for(int ty=100;ty<=115;ty++){var tile=Main.tile[tx,ty];bool edge=tx==129 || tx==136 || ty==115;tile.active(edge);tile.type=1;if(!edge && ty>=101)tile.liquid=255;}
                int primary=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1500,2200,NPCID.Hornet,Start:16,Target:Main.myPlayer);
                NativeCombatObservationChecks.Save(host,new ObservationOptions(collision:true,path:true,clearLine:false,mouseCenter:true,dummy:true,radius:25));Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();
                var type=native.GetType();
                patches.Patch(type.GetMethod("Prepare",Flags),new HarmonyMethod(typeof(NativeCombatEnvironmentChecks).GetMethod(nameof(PrepareStarting),Flags)),new HarmonyMethod(typeof(NativeCombatEnvironmentChecks).GetMethod(nameof(PrepareFinished),Flags)));
                patches.Patch(type.GetMethod("Receive",Flags),new HarmonyMethod(typeof(NativeCombatEnvironmentChecks).GetMethod(nameof(ReceiveStarting),Flags)),new HarmonyMethod(typeof(NativeCombatEnvironmentChecks).GetMethod(nameof(ReceiveFinished),Flags)));
                for(routeFrame=0;routeFrame<frames;routeFrame++)
                {
                    if(conditional)
                    {
                        routePhase=routeFrame<120?"gate-simple":routeFrame<720?"gate-background":routeFrame<840?"gate-same-target-exit":"gate-harpy";
                        if(routeFrame==120){routeEnvironment++;for(int i=0;i<8;i++)backgrounds.Add(NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),300+i*25,2400,NPCID.Bunny,Start:i));}
                        if(routeFrame==720){routeEnvironment++;foreach(int slot in backgrounds)Main.npc[slot].active=false;}
                        if(routeFrame==840){Main.npc[primary].active=false;primary=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1500,2200,NPCID.Harpy,Start:16,Target:Main.myPlayer);}
                    }
                    else
                    {
                    routePhase=routeFrame<480?"simple-start":routeFrame<960?"background":routeFrame<1440?"remote-liquid":routeFrame<1800?"rapid-switch":"simple-return";
                    if(routeFrame==480){routeEnvironment++;for(int i=0;i<8;i++)backgrounds.Add(NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),2700+i*25,2400,NPCID.Bunny,Start:i));}
                    if(routeFrame==960){routeEnvironment++;WorldGen.TileFrame(132,104,false,false);Main.tile[132,115].active(false);for(int tx=130;tx<=135;tx++)for(int ty=101;ty<115;ty++)Liquid.AddWater(tx,ty);}
                    if(routeFrame==1440){routeNearTerrain++;Main.tile[93,149].active(true);Main.tile[93,149].type=60;secondary=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1500,2400,NPCID.AngryTrapper,Start:17,ai0:93,ai1:149,Target:Main.myPlayer);}
                    if(routeFrame==1800){routeEnvironment++;foreach(int slot in backgrounds)Main.npc[slot].active=false;Main.npc[secondary].active=false;foreach(var p in Main.projectile)p.active=false;for(int tx=130;tx<=135;tx++)for(int ty=101;ty<150;ty++)Main.tile[tx,ty].liquid=0;Main.tile[132,115].active(true);}
                    if(routeFrame>=960 && routeFrame<1800)Liquid.UpdateLiquid();
                    }
                    int next=!conditional && routeFrame>=1440 && routeFrame<1788 && (routeFrame-1440)/12%2!=0?secondary:primary;
                    if(next!=desired){desired=next;routeSelection++;}
                    SampleMouse(context,Main.npc[desired].Center);prepareMs=0;var clock=Stopwatch.StartNew();step();double elapsed=clock.Elapsed.TotalMilliseconds;
                    if(!ReferenceEquals(worker,Get(native,"Worker")) || (int)Get(worker,"ChildId")!=child)throw new InvalidOperationException("Continuous investigation must retain one exact prepared child.");
                    CompleteFutures();var path=cache.Read(0);var target=(NpcIdentity)Get(selection,"Target");bool selected=(bool)Get(selection,"HasTarget") && target.Slot==desired;
                    bool shown=path!=null && path.Identity.Slot==desired && ReferenceEquals(path.Identity.Token,Main.npc[desired]);
                    if(conditional && routePhase=="gate-background")NativeCombatConditionalChecks.RolePeak=Math.Max(NativeCombatConditionalChecks.RolePeak,NativeCombatConditionalChecks.Roles(native,false));
                    if(conditional && routePhase=="gate-harpy" && shown && path.Count==121)NativeCombatConditionalChecks.HarpyConsumed++;
                    if(path!=null && !shown)throw new InvalidOperationException("Published route belongs to a retired/other selected actor.");
                    if(shown && routeFrame%15==0)SampleFuture("published","accepted",path,path.Identity,path.CaptureTick,path.SampleTick);
                    int shots=0;foreach(var p in Main.projectile)if(p.active && seenProjectiles.Add(((uint)p.key)+":"+p.type))shots++;
                    int activeShots=Main.projectile.Count(p=>p.active);projectileUpdateFrames+=activeShots;
                    var terrain=Get(native,"terrain");int chunks=terrain==null?0:((Array)Get(terrain,"Chunks")).Length;var comparison=Get(native,"terrainComparison");var npc=Main.npc[desired];
                    rows.Add(Csv(routeFrame,routePhase,Main.GameUpdateCount,desired,target.Slot,target.Type,selected?1:0,shown?1:0,child,Get(worker,"State"),Get(native,"Failed"),Get(native,"Requests"),Get(native,"Rejected"),Get(native,"Refused"),
                        Get(Get(native,"npcs"),"Count"),Get(Get(native,"projectiles"),"Count"),Get(Get(native,"extraChunks"),"Count"),chunks,backgrounds.Count(s=>Main.npc[s].active),activeShots,shots,projectileUpdateFrames,npc.position.X,npc.position.Y,npc.velocity.X,npc.velocity.Y,npc.life,player.statLife,prepareMs,elapsed,Get(comparison,"TilesRead"),Get(comparison,"TilesCaptured"),Get(native,"Reason"),
                        NativeCombatConditionalChecks.Roles(native,false),NativeCombatConditionalChecks.Roles(native,true),shown?path.Count-1:0));
                    if(routeFrame%480==479)Console.WriteLine("ROUTE phase="+routePhase+" through="+routeFrame+" child="+child+" requests="+Get(native,"Requests")+" rejected="+Get(native,"Rejected")+" refused="+Get(native,"Refused")+" background="+backgrounds.Count(s=>Main.npc[s].active)+" distinct-shots="+seenProjectiles.Count);
                }
                CompleteFutures(true);
                Console.WriteLine("INVESTIGATION COMPLETE: continuous "+frames+" updates, unchanged Host/worker; received/published futures distinguished, no product PASS or FPS inferred.");
            }
            finally
            {
                patches.UnpatchAll(patches.Id);Main.hardMode=oldHard;routeNative=null;
                File.WriteAllLines(Path.Combine(output,"route-updates.csv"),rows);
                File.WriteAllLines(Path.Combine(output,"route-futures.csv"),new[]{"source,outcome,phase,captureTick,sampleTick,slot,generation,type,horizon,dueTick,condition,predictedX,predictedY,actualX,actualY,error,linearError,evaluable"}.Concat(futureRows));
                File.WriteAllLines(Path.Combine(output,"route-replies.csv"),new[]{"frame,phase,captureTick,arriveTick,slot,generation,type,outcome,error,"+string.Join(",",costFields)}.Concat(replyRows));
            }
        }
    }
}
