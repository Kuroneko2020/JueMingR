using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Events;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Locked AI83 original-action oracle. Every natural registry comes from the real
    // tablet's original first update, not an invented four-slot arrangement.
    internal static class NativeCombatCultistRitualChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Action<NPC> before,after;
        private static readonly List<string> network=new List<string>();
        private static Type Model{get{return typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcCultistRitualMotion",true);}}
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static bool IsolateNetwork(int msgType,int remoteClient,int ignoreClient,Terraria.Localization.NetworkText text,int number,float number2,float number3,float number4,int number5,int number6,int number7)
        {
            if((msgType!=23 && msgType!=28) || number<0 || number>=Main.maxNPCs || !Family(Main.npc[number]))return true;
            network.Add(string.Join("/",msgType,remoteClient,ignoreClient,text,number,number2,number3,number4,number5,number6,number7));return false;
        }
        internal static void Boundaries()
        {
            var owner=new NpcMotionState{Identity=new NpcIdentity(1,new object(),20,1,437,437),Style=83,Active=true,Life=400,Width=54,Height=54,X=1800,Y=2300,L3=1};
            var child=new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,438,438),Style=83,Active=true,Life=400,Width=22,Height=40,X=1700,Y=2360,Vx=2,A3=-21,Direction=-1,PositionOwnerCaptured=true,PositionOwnerValid=true,PositionOwner=owner.Identity};
            var step=Model.GetMethod("Step",Flags);object[] args={child,default(PredictionEnvironment),null,1,PredictionStop.None,new[]{child,owner},2,false};
            Require((bool)step.Invoke(null,args) && ((NpcMotionState)args[0]).Direction==1 && ((NpcMotionState)args[0]).Vx==0,"Tether reversal reads the actual raw owner's center and cancels horizontal velocity.");
            owner.Active=false;owner.Life=0;args[0]=child;args[5]=new[]{child,owner};
            Require((bool)step.Invoke(null,args),"AI83 raw owner geometry remains necessary even when inactive or life0.");
            args[0]=child;args[5]=new[]{child};args[6]=1;
            Require(!(bool)step.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.MissingDependency,"Missing full raw owner identity is not an invented inactive-owner retirement.");
            child.A3=0;args[0]=child;
            Require(!(bool)step.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.Despawn,"Native438 raw owner -1 retires in the actual action.");
            owner.Active=true;owner.Life=400;owner.A0=-1;owner.A3=299;args[0]=owner;args[5]=new[]{owner};args[6]=1;
            Require((bool)step.Invoke(null,args) && ((NpcMotionState)args[0]).A3==300,"Tablet action300 is still alive despite emitted projectile RNG.");
            owner.A3=300;args[0]=owner;
            Require(!(bool)step.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.Despawn,"Tablet retires only after actual clock increment beyond300.");
            Console.WriteLine("PASS Cultist ritual raw owner / missing identity / deterministic tablet retirement boundaries");
        }
        internal static void Lifecycle(object context)
        {
            var source=Get(Get(context,"CombatObservation"),"Prediction");var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var floor=Model.GetMethod("CheckFloor",Flags);
            // Query-equivalent terrain cases: active solidity and raw platform
            // type intentionally differ for inactive/actuated floor tiles.
            for(int variant=0;variant<12;variant++)
            {
                Setup(context);int cx=variant==10?4:variant==11?396:120,cy=145;
                foreach(int dx in new[]{-10,-6,6,10})
                {
                    int x=cx+dx;if(x<0 || x>=Main.maxTilesX)continue;
                    for(int y=140;y<157;y++){Main.tile[x,y].active(false);Main.tile[x,y].inActive(false);Main.tile[x,y].type=0;Main.tile[x,y].slope(0);Main.tile[x,y].halfBrick(false);}
                    var tile=Main.tile[x,150];tile.active(true);tile.type=1;
                    if(variant==1){tile.type=19;tile.frameY=18;}
                    if(variant==2){tile.type=19;tile.active(false);}
                    if(variant==3){tile.type=19;tile.inActive(true);}
                    if(variant==4)tile.inActive(true);
                    if(variant==5)tile.halfBrick(true);
                    if(variant==6)tile.slope(1);
                    if(variant==7 || variant==8 || variant==9)
                    {
                        for(int ax=x-1;ax<=x+1;ax++)for(int y=147;y<150;y++){Main.tile[ax,y].active(true);Main.tile[ax,y].type=1;}
                        if(variant==8)for(int y=147;y<150;y++)Main.tile[x,y].active(false);
                        if(variant==9){for(int y=147;y<150;y++)Main.tile[x,y].active(false);Main.tile[x+1,147].active(false);Main.tile[x+1,148].active(false);Main.tile[x-1,147].active(false);Main.tile[x-1,148].active(false);}
                    }
                }
                Point[] points;var center=new Vector2(cx*16+8,cy*16+8);bool native=CultistRitual.CheckFloor(center,out points);terrain.Reset();object[] args={center.X,center.Y,EnvironmentFor(Main.npc[0]),terrain,false,PredictionStop.None};
                Require((bool)floor.Invoke(null,args) && (bool)args[4]==native,"Actual CheckFloor variant="+variant+" native="+native+" stop="+args[5]);
            }
            var observer=new Harmony("JueMingR.Probe.CultistRitualLifecycle");var send=typeof(NetMessage).GetMethod("SendData",Flags);observer.Patch(send,prefix:new HarmonyMethod(typeof(NativeCombatCultistRitualChecks).GetMethod(nameof(IsolateNetwork),Flags)){priority=Priority.First+100});
            try
            {
                foreach(int type in new[]{438,379,380})foreach(int mode in new[]{0,1,2})foreach(int variant in new[]{0,1,2})
                {
                    Setup(context);int parentSlot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1800,2350,437,Start:20);var owner=Main.npc[parentSlot];owner.localAI[3]=1;
                    int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1700,2400,type,Start:16,Target:Main.myPlayer);var n=Main.npc[slot];n.ai[3]=-(parentSlot+1);n.velocity.X=1.25f;
                    // Raw owner counterexamples are explicitly separate from
                    // the natural ritual birth/receiving phase matrix below.
                    if(variant==0)owner.life=0;if(variant==1)owner.active=false;if(variant==2)owner.SetDefaults(1);
                    Main.netMode=mode;var group=Capture(context,n);var predicted=group[Index(group,n)];PredictionStop stop;terrain.Reset();bool valid=NpcMotion.Step(ref predicted,group,group.Length,EnvironmentFor(n),terrain,1,true,out stop);n.UpdateNPC(slot);
                    Require(valid && n.active && Vector2.Distance(new Vector2(predicted.X,predicted.Y),n.position)<=.08 && predicted.Vx==n.velocity.X && predicted.Vy==n.velocity.Y && predicted.A0==n.ai[0] && predicted.CanHarm==(n.damage>0 && !n.friendly),"Actual raw owner lifecycle type="+type+" mode="+mode+" variant="+variant+" stop="+stop);
                }
                Setup(context);
                int tabletSlot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1800,2350,437,Start:20);var tablet=Main.npc[tabletSlot];
                tablet.UpdateNPC(tabletSlot);
                Require(tablet.localAI[3]==1,"Original tablet action births its actual registry for the buff slot-order counterexample.");
                var outer=Main.npc[(int)tablet.ai[0]-1];
                Require(outer.active && outer.type==379 && outer.whoAmI<tabletSlot,"Actual registry outer slot executes before the later tablet slot.");
                outer.AddBuff(31,1);
                Require(outer.buffImmune[31] && outer.FindBuffIndex(31)<0,"Original379 immunity rejects Confused instead of inventing a susceptible cultist.");
                // Native release has no raw-slot type gate. A real Transform
                // supplies a susceptible foreign row without overriding its
                // immunity or setting the private solver's confused input.
                outer.Transform(3);Require(outer.type==3 && outer.active && !outer.buffImmune[31],"Actual foreign raw-slot replacement has its original Zombie buff profile.");
                outer.AddBuff(31,1);Main.npc[(int)tablet.localAI[1]-1].StrikeNPCNoInteraction(1,0,0);outer.UpdateNPC(outer.whoAmI);
                Require(outer.confused && outer.FindBuffIndex(31)<0 && tablet.localAI[0]==1,"Original lower-slot last buff action removes the timer but retains confused for the pending release.");
                var releasedGroup=Capture(context,tablet);var predictedTablet=releasedGroup[Index(releasedGroup,tablet)];var observedOuter=releasedGroup[Index(releasedGroup,outer)];
                Require(observedOuter.ConfusedTicks==0 && observedOuter.RitualConfused,"Current raw confused is distinct from the expired buff counter.");
                PredictionStop releaseStop;terrain.Reset();bool releaseValid=NpcMotion.Step(ref predictedTablet,releasedGroup,releasedGroup.Length,EnvironmentFor(tablet),terrain,1,true,out releaseStop);
                tablet.UpdateNPC(tabletSlot);var predictedOuter=releasedGroup[Index(releasedGroup,outer)];
                Require(releaseValid && outer.ai[3]==0 && predictedOuter.Direction==outer.direction && predictedOuter.DirectionY==outer.directionY && predictedOuter.Target==outer.target,"Actual last-buff lower-slot release matches native TargetClosest direction/target: "+releaseStop);
            }
            finally{Main.netMode=0;observer.Unpatch(send,HarmonyPatchType.Prefix,observer.Id);}
            Console.WriteLine("PASS Cultist ritual native CheckFloor12, raw-owner27 and last-confused-action slot-order counterexamples");
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            string selection=Environment.GetEnvironmentVariable("JUEMINGR_CULTIST_RITUAL_SCENE");int selectedScene=-1;
            if(!string.IsNullOrEmpty(selection) && (!int.TryParse(selection,out selectedScene) || selectedScene<0 || selectedScene>2))throw new ArgumentException("JUEMINGR_CULTIST_RITUAL_SCENE must be0,1,2 or unset.");
            Console.WriteLine("CULTIST_RITUAL actualLeaves="+(selectedScene<0?"0,1,2":selectedScene.ToString(CultureInfo.InvariantCulture)));
            var source=Get(Get(context,"CombatObservation"),"Prediction");var terrain=(IPredictionTerrain)Get(source,"Terrain");var update=typeof(NPC).GetMethod("UpdateNPC",Flags);var observer=new Harmony("JueMingR.Probe.CultistRitualOriginalSlots");
            observer.Patch(update,prefix:new HarmonyMethod(typeof(NativeCombatCultistRitualChecks).GetMethod(nameof(Before),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatCultistRitualChecks).GetMethod(nameof(After),Flags)));
            var send=typeof(NetMessage).GetMethod("SendData",Flags);network.Clear();
            // Receiving and death stay native. Only this family's actual
            // network outlet is intercepted, with all original parameters.
            observer.Patch(send,prefix:new HarmonyMethod(typeof(NativeCombatCultistRitualChecks).GetMethod(nameof(IsolateNetwork),Flags)){priority=Priority.First+100});
            var records=new List<string>{"scene,frame,actions,tablet,devotee,archer,matched,long,woke,fresh,bossBirth,tabletRetired,error"};
            try
            {
                for(int scene=0;scene<3;scene++)
                {
                    if(selectedScene>=0 && scene!=selectedScene)continue;
                    Setup(context);Require(CultistRitual.TrySpawning(120,150,true),"Actual CheckRitual and CheckFloor permit native tablet birth.");NPC tablet=null;for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type==437){tablet=Main.npc[i];break;}Require(tablet!=null && tablet.localAI[3]==0,"Real ritual tablet begins before its first original four-child birth.");
                    // One real fixture world action births the registry; no
                    // extra live AI or RNG is used to fabricate dependencies.
                    step();Require(tablet.localAI[3]==1,"Original tablet first action establishes the observed registry.");
                    var parts=new[]{Main.npc[(int)tablet.ai[0]-1],Main.npc[(int)tablet.localAI[1]-1],Main.npc[(int)tablet.localAI[2]-1],Main.npc[(int)tablet.ai[1]-1]};
                    Require(parts[0].type==379 && parts[1].type==438 && parts[2].type==438 && parts[3].type==379,"Actual native registry preserves379/438/438/379 roles.");var selected=parts[1];
                    NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                    int actions=0,tablets=0,devotees=0,archers=0,matched=0,longMatches=0,woke=0,fresh=0,bossBirth=0,tabletRetired=0;double max=0;bool hit=false,killed=false;long changedAt=-1;NPC pending=null;NpcMotionState predicted=default(NpcMotionState);bool valid=false;PredictionStop stop=PredictionStop.None;var frozen=new List<NpcTrajectory>();
                    before=n=>{pending=null;if(!Family(n) || !n.active)return;var group=Capture(context,n);predicted=group[Index(group,n)];terrain.Reset();valid=NpcMotion.Step(ref predicted,group,group.Length,EnvironmentFor(n),terrain,1,true,out stop);pending=n;Require(valid || stop==PredictionStop.Despawn,"Actual ritual current-action only deterministic retirement ends this observed complete group: "+stop);};
                    after=n=>{if(!ReferenceEquals(n,pending))return;if(!valid){Require(!n.active,"Predicted retirement must be an actual original retirement.");if(n.type==437)tabletRetired++;return;}double error=Vector2.Distance(new Vector2(predicted.X,predicted.Y),n.position);Require(error<=.08 && Math.Abs(predicted.Vx-n.velocity.X)<=.0001 && Math.Abs(predicted.Vy-n.velocity.Y)<=.0001 && predicted.A0==n.ai[0] && predicted.A3==n.ai[3] && predicted.CanHarm==(!n.friendly && n.damage>0),"Actual ritual actor type="+n.type+" error="+error+" V="+predicted.Vx.ToString("R")+","+predicted.Vy.ToString("R")+" native="+n.velocity);actions++;if(n.type==437)tablets++;else if(n.type==438)devotees++;else archers++;};
                    for(int frame=0;frame<1800;frame++)
                    {
                        if(scene>0 && frame==300)
                        {var old=cache.Read(0);selected.StrikeNPCNoInteraction(30,0,0);Require(selected.active && selected.justHit && tablet.localAI[0]==1,"Actual receiving writes the real tablet wake flag before its next slot action.");hit=true;changedAt=(long)Main.GameUpdateCount;frozen.Clear();Require(old==null || old.CaptureTick<=changedAt,"Receiving preserves the old capture as a conditional pre-event record.");}
                        if(scene==2 && frame==700)
                        {foreach(var part in parts)if(part.active)part.StrikeNPCNoInteraction(100000,0,0);Require(!parts[0].active && !parts[1].active && !parts[2].active && !parts[3].active,"All four actual receivers die through original checkDead/drop.");killed=true;frozen.Clear();changedAt=(long)Main.GameUpdateCount;}
                        NativeCombatModeledImpactChecks.SampleMouse(context,selected.Center);step();if(tablet.localAI[0]==2)woke++;if(tablet.ai[0]==-1 && Main.npc[(int)tablet.ai[2]].active && Main.npc[(int)tablet.ai[2]].type==439)bossBirth++;
                        var path=cache.Read(0);if(path!=null && ReferenceEquals(path.Identity.Token,selected) && selected.active)
                        {if(changedAt>=0){Require(path.CaptureTick>=changedAt,"Original receiving withdraws the old production publication before current selected-path consumption.");fresh++;changedAt=-1;}if(frame%5==0 && path.Count>1 && !frozen.Contains(path))frozen.Add(path);}
                        for(int i=frozen.Count-1;i>=0;i--){var pathAt=frozen[i];int offset=(int)(Main.GameUpdateCount-pathAt.CaptureTick);if(offset>=pathAt.Count){frozen.RemoveAt(i);continue;}if(offset<1)continue;double error=Vector2.Distance(new Vector2(pathAt[offset].Bounds.X,pathAt[offset].Bounds.Y),selected.position);max=Math.Max(max,error);Require(error<=.08,"Frozen actual ritual scene="+scene+" frame="+frame+" offset="+offset+" error="+error);matched++;if(offset>=30)longMatches++;}
                        records.Add(string.Join(",",scene,frame,actions,tablets,devotees,archers,matched,longMatches,woke,fresh,bossBirth,tabletRetired,max.ToString("R",CultureInfo.InvariantCulture)));
                    }
                    before=after=null;Require(tablets>300 && devotees>300 && archers>300 && matched>0 && longMatches>=30,"Original ritual registry has useful long motor prefixes for every actual role.");
                    if(scene>0)Require(hit && woke>0 && fresh>0,"Real receiving releases the native archers and restores the selected publication.");
                    if(scene==2)Require(killed && bossBirth>0 && tabletRetired==1 && !tablet.active,"Four real deaths create actual439 then deterministic tablet retirement, without treating external spawn RNG as its motor uncertainty.");
                    Console.WriteLine("CULTIST_RITUAL scene="+scene+" actions="+actions+" tablet="+tablets+" devotee="+devotees+" archer="+archers+" matched="+matched+" long="+longMatches+" woke="+woke+" fresh="+fresh+" bossBirth="+bossBirth+" retired="+tabletRetired+" max="+max);
                }
            }
            finally{before=after=null;observer.Unpatch(update,HarmonyPatchType.Prefix,observer.Id);observer.Unpatch(update,HarmonyPatchType.Postfix,observer.Id);observer.Unpatch(send,HarmonyPatchType.Prefix,observer.Id);Console.WriteLine("CULTIST_RITUAL original isolated network parameters="+string.Join(";",network));File.WriteAllLines(Path.Combine(output,"cultist-ritual-continuous.csv"),records);}
        }
        private static NpcMotionState[] Capture(object context,NPC selected)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");long session=(long)Get(host,"Session");var read=source.GetType().GetMethod("Read",Flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);var rows=new List<NpcMotionState>();var required=new HashSet<int>();
            for(int i=0;i<Main.maxNPCs;i++)if(Family(Main.npc[i]) && Main.npc[i].active)
            {required.Add(i);var n=Main.npc[i];if(n.type==438 || (n.type==379 || n.type==380) && n.ai[3]<0){int raw=(int)(0f-n.ai[3]-1f);if(raw>=0 && raw<Main.maxNPCs)required.Add(raw);}if(n.type==437 && n.localAI[3]!=0 && n.ai[0]!=-1)foreach(float encoded in new[]{n.ai[0],n.localAI[1],n.localAI[2],n.ai[1]}){int raw=(int)encoded-1;if(raw>=0 && raw<Main.maxNPCs)required.Add(raw);}}
            required.Add(selected.whoAmI);
            for(int i=0;i<Main.maxNPCs;i++)if(required.Contains(i))
            {
                var n=Main.npc[i];var state=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,state,terrain};tracking.Invoke(null,args);state=(NpcMotionState)args[1];state.CultistNetMode=Main.netMode;state.RitualRelationsCaptured=state.RitualRelationsComplete=true;state.RitualReleasedCanHarm=!n.friendly && n.defDamage>0;
                if(n.type==437 && n.localAI[3]!=0 && n.ai[0]!=-1){state.RitualPart0=Identity(session,(int)n.ai[0]-1);state.RitualPart1=Identity(session,(int)n.localAI[1]-1);state.RitualPart2=Identity(session,(int)n.localAI[2]-1);state.RitualPart3=Identity(session,(int)n.ai[1]-1);}
                if(n.type==438 || (n.type==379 || n.type==380) && n.ai[3]<0){int raw=(int)(0f-n.ai[3]-1f);state.PositionOwnerCaptured=true;state.PositionOwnerValid=raw>=0 && raw<Main.maxNPCs && Main.npc[raw]!=null;if(state.PositionOwnerValid)state.PositionOwner=Identity(session,raw);}
                rows.Add(state);
            }
            return rows.ToArray();
        }
        private static NpcIdentity Identity(long session,int slot)
        {
            // A geometry-only raw owner counterexample need not have birthed
            // a registry. Preserve invalid raw indices as absent identities,
            // matching Source.RitualIdentity rather than indexing slot-1.
            if(slot<0 || slot>=Main.maxNPCs || slot>=Main.npc.Length)return default(NpcIdentity);
            var n=Main.npc[slot];return n==null?default(NpcIdentity):new NpcIdentity(session,n,slot,n.generation,n.type,n.netID);
        }
        private static int Index(NpcMotionState[] group,NPC n){for(int i=0;i<group.Length;i++)if(ReferenceEquals(group[i].Identity.Token,n))return i;throw new InvalidOperationException("Missing actual ritual actor.");}
        private static bool Family(NPC n){return n!=null && (n.type==437 || n.type==438 || n.type==379 || n.type==380);}
        private static PredictionEnvironment EnvironmentFor(NPC n)
        {
            // This fixture has one actual active player. A ready tablet's
            // release consumes the outer rows' closest query, not target255.
            int target=n.type==437 && n.localAI[0]==1?Main.myPlayer:n.target;
            var p=Main.player[target>=0 && target<Main.player.Length?target:255];return new PredictionEnvironment{PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerVelocityCaptured=true,PlayerTimelineActive=p.active && !p.dead && !p.ghost,PlayerDead=p.dead || !p.active,Expert=Main.expertMode,GoodWorld=Main.getGoodWorld,Multiplayer=Main.netMode==1,Day=Main.dayTime,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,GravityWorldSurface=Main.worldSurface,RockLayer=(float)Main.rockLayer};
        }
        private static void Setup(object context)
        {
            NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.GameMode=0;Main.dayTime=false;Main.getGoodWorld=false;
            if(Main.ItemDropSolver==null){Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);}for(int i=0;i<Main.item.Length;i++)Main.item[i].whoAmI=i;
            for(int i=0;i<255;i++)Main.player[i].active=false;var p=Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};p.position=new Vector2(1750,2400-p.height);p.ResetFallDamage();p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.immune=true;p.immuneTime=1000000;for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions(path:false,mouseCenter:true,clearLine:false,radius:50));
        }
    }
}
