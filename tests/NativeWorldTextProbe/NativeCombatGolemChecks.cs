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
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatGolemChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Action<NPC> before,after,death;
        private static readonly List<string> networkOutlets=new List<string>();
        private static bool IsolateStrike(int msgType,int remoteClient,int ignoreClient,Terraria.Localization.NetworkText text,int number,float number2,float number3,float number4,int number5,int number6,int number7)
        {
            if(msgType!=28 || number<0 || number>=Main.maxNPCs || !Family(Main.npc[number]))return true;
            // Retain the original damage/checkDead/HitEffect action. Only its
            // real Golem strike network outlet is isolated from the machine.
            networkOutlets.Add(string.Join("/",msgType,remoteClient,ignoreClient,text,number,number2,number3,number4,number5,number6,number7));return false;
        }
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static void Death(NPC __instance){death?.Invoke(__instance);}
        private static bool Family(NPC n){return n!=null && n.type>=245 && n.type<=249;}
        internal static void Boundaries()
        {
            var model=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcGolemMotion",true);var begin=model.GetMethod("BeginFrame",Flags);var kernel=model.GetMethod("Step",Flags);var length=model.GetMethod("XnaLength",Flags);
            for(int i=1;i<=8192;i++){float x=((i*7919)%100007-50003)/31f,y=((i*3571)%90007-45003)/29f;Require((float)length.Invoke(null,new object[]{x,y})==new Vector2(x,y).Length(),"Exact original fist-anchor Vector2.Length stores "+i);}
            var body=new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,245,245),Active=true,Style=45,Width=140,Height=140,Life=1000,LifeMax=1000,L0=1};
            var head=new NpcMotionState{Identity=new NpcIdentity(1,new object(),17,1,246,246),Active=true,Style=46,Width=70,Height=70,Life=25000,LifeMax=25000,Scale=1,GolemBalanceCaptured=true,GolemBalance=1,GolemRelationsCaptured=true,GolemRelationsComplete=true,GolemBossCaptured=true,GolemBossValid=true,GolemBossIdentity=body.Identity,GolemOrphanDamageCaptured=true,GolemOrphanDamage=9989};
            var group=new[]{body,head};var e=new PredictionEnvironment{PlayerX=1000,PlayerY=2400,GravityWorldSurface=140,Jungle=true};object[] args={head,e,null,1,PredictionStop.None,group,2,false};
            Require((bool)kernel.Invoke(null,args),"Full active owner identity supplies deterministic attached-head motion.");
            args[5]=new[]{head};args[6]=1;Require(!(bool)kernel.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.MissingDependency,"Missing observed full owner is not a known orphan.");
            body.Life=0;group[0]=body;begin.Invoke(null,new object[]{group,2});Require(group[1].GolemBossValid,"Original Main frame check uses active/type, independently of zero life.");
            body.Active=false;group[0]=body;args[0]=head;args[5]=group;args[6]=2;Require((bool)kernel.Invoke(null,args),"Current native slot still reads an inactive nonnegative global owner's last geometry.");
            begin.Invoke(null,new object[]{group,2});Require(!group[1].GolemBossValid,"The next native frame clears the inactive global body before NPC actions.");
            args[0]=group[1];Require((bool)kernel.Invoke(null,args) && ((NpcMotionState)args[0]).Life==25000-9989,"9999 orphan strike is ordinary damage, not an invented instant death.");
            var foreign=body;foreign.Active=true;foreign.MotionType=1;group[0]=foreign;group[1]=head;begin.Invoke(null,new object[]{group,2});Require(!group[1].GolemBossValid,"A foreign type at the raw global slot is cleared at the frame boundary.");
            body.Active=true;body.Life=1000;body.GolemRelationsCaptured=body.GolemRelationsComplete=body.GolemBossCaptured=body.GolemBalanceCaptured=true;body.GolemBalance=1;body.L0=0;group[0]=body;group[1]=head;args[0]=body;
            Require(!(bool)kernel.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.PhaseBoundary,"Actual first server body action needs three not-yet-observed identities.");
            Console.WriteLine("PASS Golem native math8192/fullidentity/raw-index/frame-clear/ordinary-orphan/birth boundaries");
        }
        internal static void Lifecycle(object context)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");long session=(long)Get(host,"Session");var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var model=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcGolemMotion",true);var kernel=model.GetMethod("Step",Flags);var begin=model.GetMethod("BeginFrame",Flags);var check=typeof(Main).GetMethod("CheckBossIndexes",Flags);int actions=0;
            var network=new Harmony("JueMingR.Probe.GolemOriginalStrikeOutlet");var send=typeof(NetMessage).GetMethod("SendData",Flags);var isolate=new HarmonyMethod(typeof(NativeCombatGolemChecks).GetMethod(nameof(IsolateStrike),Flags)){priority=Priority.First+100};networkOutlets.Clear();network.Patch(send,prefix:isolate);
            try
            {
            foreach(int mode in new[]{0,1,2})
            {
                Setup(context,0);var body=Birth(1600,16);body.UpdateNPC(body.whoAmI);var head=Part(246);Require(head!=null && Count(247)==1 && Count(248)==1,"The real first Golem AI allocates its complete three-part family.");
                bool activeZero=false;var hook=new Harmony("JueMingR.Probe.GolemActiveZero");var checkDead=typeof(NPC).GetMethod("checkDead",Flags);
                death=n=>{if(!ReferenceEquals(n,body) || !n.active || n.life>0)return;var group=Capture(read,tracking,terrain,session);int index=Index(group,head);Require(index>=0 && group[index].GolemBossValid,"Real lethal strike still exposes an active life-zero raw owner before checkDead.");activeZero=true;};
                hook.Patch(checkDead,prefix:new HarmonyMethod(typeof(NativeCombatGolemChecks).GetMethod(nameof(Death),Flags)));
                try{body.StrikeNPCNoInteraction(body.lifeMax+body.defense+100,0,0);Require(activeZero && !body.active,"Actual body checkDead retires the original owner.");}
                finally{death=null;hook.Unpatch(checkDead,HarmonyPatchType.Prefix,hook.Id);}
                Require(NPC.golemBoss==body.whoAmI,"Owner death alone does not clear the raw global index mid-frame.");
                var initial=Capture(read,tracking,terrain,session);int h=Index(initial,head);object[] current={initial[h],EnvironmentFor(head),terrain,1,PredictionStop.None,initial,initial.Length,false};
                Require((bool)kernel.Invoke(null,current),"Current-slot capture preserves dead-owner geometry until Main's frame check.");
                var next=(NpcMotionState[])initial.Clone();begin.Invoke(null,new object[]{next,next.Length});Require(!next[h].GolemBossValid,"Pure frame boundary mirrors real owner validity.");check.Invoke(null,null);Require(NPC.golemBoss<0,"Original Main.CheckBossIndexes clears the same observed dead owner.");
                Main.netMode=mode;
                try
                {
                    for(int action=0;action<8 && head.active;action++)
                    {
                        var group=Capture(read,tracking,terrain,session);h=Index(group,head);object[] args={group[h],EnvironmentFor(head),terrain,1,PredictionStop.None,group,group.Length,false};bool valid=(bool)kernel.Invoke(null,args);var predicted=(NpcMotionState)args[0];
                        // This is the lifecycle's one original action, never a
                        // second AI replay to construct its sampled inputs.
                        head.AI();actions++;Require(predicted.Life==head.life && predicted.Active==head.active,"Original orphan9999 damage and retirement mode="+mode+" action="+action+" predicted="+predicted.Life+" actual="+head.life);
                        Require(valid || (PredictionStop)args[4]==PredictionStop.Despawn,"Exact known orphan retirement boundary.");
                    }
                    Require(!head.active,"Repeated actual orphan strikes eventually retire the attached head.");
                    Require(mode==1?Count(249)==0:Count(249)>0,"Only native server/singleplayer head HitEffect creates the free head.");
                }
                finally{Main.netMode=0;}
            }
            Console.WriteLine("PASS Golem original active-zero/dead-global/next-frame/orphan client-server lifecycle actions="+actions);
            Require(networkOutlets.Count>0,"Real orphan strikes expose original damage network outlets.");Console.WriteLine("GOLEM original isolated strike outlets="+networkOutlets.Count+" parameters="+string.Join(";",networkOutlets));
            }
            finally{network.Unpatch(send,HarmonyPatchType.Prefix,network.Id);}
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");long session=(long)Get(host,"Session");var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var records=new List<string>{"scene,frame,type,phase,clock,target,global,selected,published,count,stop,matched,longMatched,error,cross,body,head,fist,freeHead,receiving"};var failures=new List<string>();
            var owner=new Harmony("JueMingR.Probe.GolemOriginalSlot");var update=typeof(NPC).GetMethod("UpdateNPC",Flags);owner.Patch(update,prefix:new HarmonyMethod(typeof(NativeCombatGolemChecks).GetMethod(nameof(Before),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatGolemChecks).GetMethod(nameof(After),Flags)));
            bool oldGood=Main.getGoodWorld;int totalMatches=0,totalBody=0,totalHead=0,totalFist=0,totalFree=0,totalReceiving=0;
            try
            {
                for(int scene=0;scene<12;scene++)
                {
                    try
                    {
                        Setup(context,scene);NPC body=Birth(1600,16),actor=body;NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                        var freezes=new List<NpcTrajectory>();var phases=new Dictionary<NpcTrajectory,float>();int matched=0,longMatches=0,cross=0,published=0,selected=0,bodyActions=0,headActions=0,fistActions=0,freeActions=0,receiving=0,births=0,retire=0;double maxError=0,tangent=0;
                        NPC pending=null;NpcMotionState predicted=default(NpcMotionState);PredictionStop actionStop=PredictionStop.None;bool valid=false;
                        before=n=>
                        {
                            pending=n;valid=false;if(!n.active || !Family(n))return;var group=Capture(read,tracking,terrain,session);int index=Index(group,n);Require(index>=0,"Current original Golem slot exists in the exact captured collection.");
                            predicted=group[index];terrain.Reset();valid=NpcMotion.Step(ref predicted,group,group.Length,EnvironmentFor(n),terrain,1,true,out actionStop);
                            if(!valid){if(actionStop==PredictionStop.PhaseBoundary && n.type==245 && n.localAI[0]==0)births++;else Require(actionStop==PredictionStop.Despawn || actionStop==PredictionStop.MissingDependency,"Explicit original Golem dependency boundary "+actionStop+" type="+n.type+" pos="+n.position+" target="+n.target);}
                        };
                        after=n=>
                        {
                            if(!ReferenceEquals(n,pending) || !Family(n))return;if(!valid){if(actionStop==PredictionStop.Despawn && !n.active)retire++;return;}
                            double error=Vector2.Distance(new Vector2(predicted.X,predicted.Y),n.position);
                            // ai2 head laser RNG has no own motor consumer;
                            // ai0/ai1 and the actual receiving flags do.
                            Require(error<=.08 && Math.Abs(predicted.Vx-n.velocity.X)<=.0001 && Math.Abs(predicted.Vy-n.velocity.Y)<=.0001 && predicted.A0==n.ai[0] && predicted.A1==n.ai[1] && predicted.Alpha==n.alpha && predicted.NoTileCollide==n.noTileCollide && predicted.CanReceive==(!n.dontTakeDamage && !n.immortal) && predicted.Life==n.life,"Original Golem slot scene="+scene+" type="+n.type+" slot="+n.whoAmI+" error="+error+" predictedV="+predicted.Vx+","+predicted.Vy+" actualV="+n.velocity+" phase="+string.Join("/",n.ai)+" predictedPhase="+predicted.A0+"/"+predicted.A1+" boss="+NPC.golemBoss+" p="+Main.LocalPlayer.position);
                            if(n.type==245)bodyActions++;else if(n.type==246)headActions++;else if(n.type==249){freeActions++;Require(n.dontTakeDamage,"Natural free head retains its native receiving exclusion while serving the body dependency.");}else fistActions++;
                        };
                        for(int frame=0;frame<1600 && body.active;frame++)
                        {
                            bool input=false;
                            if(frame==2)
                            {
                                Require(Count(246)==1 && Count(247)==1 && Count(248)==1,"Natural first body action is followed by real three-part closure capture.");
                                // The newborn body is genuinely invulnerable.
                                // Select its receiving head/fist until actual
                                // head death exposes the receiving body below.
                                actor=Part(246);if(scene==4 || scene==7)actor=Part(247);if(scene==5)actor=Part(248);
                            }
                            if(frame==300 && scene==6){HitTo(Part(246),.3f);input=true;}
                            if(frame==300 && scene==7){HitTo(Part(247),.2f);input=true;}
                            if(frame==400 && scene==8){var head=Part(246);Require(head!=null,"Actual attached head exists before receiving death.");head.StrikeNPCNoInteraction(head.lifeMax+head.defense+100,0,0);Require(!head.active && Count(249)>0,"Original lethal attached-head HitEffect creates the true nonreceiving free head.");input=true;}
                            if(frame==500 && scene==8){Require(!body.dontTakeDamage,"Head loss actually exposes the native body to receiving.");HitTo(body,.2f);input=true;}
                            if(frame==300 && scene==9){Part(247).StrikeNPCNoInteraction(1000000,0,0);Part(248).StrikeNPCNoInteraction(1000000,0,0);Part(246).StrikeNPCNoInteraction(1000000,0,0);Require(Count(247)==0 && Count(248)==0 && Count(246)==0 && Count(249)>0,"Real fist/head deaths change the receiving body's jump rate and preserve the free-head dependency.");input=true;}
                            if(frame==200 && scene==10){Birth(2000,24);input=true;}
                            if(frame==800 && scene==11){Main.LocalPlayer.KillMe(Terraria.DataStructures.PlayerDeathReason.ByCustomReason("isolated Golem retreat"),1000000,0);Require(Main.LocalPlayer.dead,"Actual player death changes the retreat input.");input=true;}
                            if(input){freezes.Clear();phases.Clear();}
                            if(actor==null || !actor.active)actor=body;NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();if(!body.active)break;
                            if(!body.dontTakeDamage)receiving++;
                            bool chosen=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,actor);if(chosen)selected++;
                            var path=cache.Read(0);if(path!=null && ReferenceEquals(path.Identity.Token,actor) && path.Identity.Type==actor.type && !freezes.Contains(path)){freezes.Add(path);phases[path]=actor.type==246?actor.ai[1]:actor.ai[0];published++;}
                            for(int i=freezes.Count-1;i>=0;i--)
                            {
                                var frozen=freezes[i];int offset=(int)(Main.GameUpdateCount-frozen.CaptureTick);if(offset>=frozen.Count || !ReferenceEquals(frozen.Identity.Token,actor) || frozen.Identity.Type!=actor.type){freezes.RemoveAt(i);phases.Remove(frozen);continue;}if(offset<1)continue;
                                var point=frozen[offset];double error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),actor.position);maxError=Math.Max(maxError,error);matched++;
                                if(offset>=30){longMatches++;tangent=Math.Max(tangent,Vector2.Distance(actor.position,new Vector2(frozen[0].Bounds.X+frozen[0].Vx*offset,frozen[0].Bounds.Y+frozen[0].Vy*offset)));}
                                // Attached-head phase0's periodic native
                                // firing clock wraps while its attachment
                                // remains continuous. Other selected actors
                                // cross actual jump/charge motor phases.
                                if(actor.type==246?actor.ai[1]<phases[frozen]:phases[frozen]!=actor.ai[0])cross++;
                                Require(error<=.08 && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal) && point.NoTileCollide==actor.noTileCollide,"Frozen Golem scene="+scene+" frame="+frame+" type="+actor.type+" capture="+frozen.CaptureTick+" offset="+offset+" error="+error+" predicted="+point.Bounds.X+","+point.Bounds.Y+" actual="+actor.position+" V="+actor.velocity+" phase="+string.Join("/",actor.ai)+" boss="+NPC.golemBoss+" stop="+frozen.Stop);
                            }
                            records.Add(string.Join(",",scene,frame,actor.type,actor.ai[0],actor.ai[1],actor.target,NPC.golemBoss,chosen,path!=null,path?.Count??0,path?.Stop.ToString()??"none",matched,longMatches,maxError.ToString("R",CultureInfo.InvariantCulture),cross,bodyActions,headActions,fistActions,freeActions,receiving));
                        }
                        before=after=null;
                        Require(bodyActions>=500 && headActions>=100 && fistActions>=100 && births>0,"Natural body/head/fists continue after observed true birth identities.");
                        Require(selected>0 && published>0 && longMatches>=30 && tangent>8,"Natural Golem publication has a real curved long future window.");
                        Require(cross>=30,"Selected original body/head/fist passes genuine motor phase transitions inside long forecasts.");
                        if(scene==8)Require(freeActions>=100 && receiving>=100,"Actual head death restores damageable body coverage with the full nonreceiving free-head motor dependency.");
                        if(scene==10)Require(Count(245)>=2 && Count(246)>=2,"Multiple naturally spawned bodies retain the complete global-slot and global-part closure.");
                        totalMatches+=matched;totalBody+=bodyActions;totalHead+=headActions;totalFist+=fistActions;totalFree+=freeActions;totalReceiving+=receiving;
                        Console.WriteLine("GOLEM scene="+scene+" selected="+selected+" published="+published+" matched="+matched+" long="+longMatches+" cross="+cross+" body="+bodyActions+" head="+headActions+" fists="+fistActions+" free="+freeActions+" receiving="+receiving+" births="+births+" retired="+retire+" error="+maxError);
                    }
                    catch(Exception failure) when(!(failure is OutOfMemoryException)){before=after=null;failures.Add(scene+": "+failure.Message);Console.WriteLine("GOLEM FAIL "+scene+": "+failure);}
                }
                Require(failures.Count==0,"Golem natural failures="+failures.Count+": "+string.Join(" | ",failures));
                Console.WriteLine("PASS Golem12 natural matched="+totalMatches+" body="+totalBody+" head="+totalHead+" fists="+totalFist+" free="+totalFree+" receiving="+totalReceiving);
            }
            finally{before=after=null;death=null;Main.getGoodWorld=oldGood;Main.netMode=0;owner.Unpatch(update,HarmonyPatchType.All,owner.Id);Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"golem-continuous.csv"),records);}
        }
        private static NpcMotionState[] Capture(MethodInfo read,MethodInfo tracking,IPredictionTerrain terrain,long session)
        {
            var rows=new List<NpcMotionState>();NpcIdentity global=default(NpcIdentity);bool valid=NPC.golemBoss>=0 && NPC.golemBoss<Main.maxNPCs && Main.npc[NPC.golemBoss]!=null;
            for(int i=0;i<Main.maxNPCs;i++)
            {
                var n=Main.npc[i];if(n==null || !(n.active && Family(n) || valid && i==NPC.golemBoss))continue;
                var s=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,s,terrain};tracking.Invoke(null,args);s=(NpcMotionState)args[1];if(valid && i==NPC.golemBoss)global=s.Identity;rows.Add(s);
            }
            for(int i=0;i<rows.Count;i++){var s=rows[i];s.GolemRelationsCaptured=s.GolemRelationsComplete=s.GolemBossCaptured=true;s.GolemBossValid=valid;s.GolemBossIdentity=global;rows[i]=s;}return rows.ToArray();
        }
        private static PredictionEnvironment EnvironmentFor(NPC n)
        {
            var p=Main.player[n.target>=0 && n.target<Main.player.Length?n.target:255];var rect=p.Hitbox;return new PredictionEnvironment{Players=new PredictionPlayers(new[]{new MotionRect(rect.X,rect.Y,rect.Width,rect.Height)},1,new[]{p.whoAmI}),PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,PlayerVelocityCaptured=true,PlayerTimelineActive=p.active && !p.dead && !p.ghost,PlayerDead=p.dead || !p.active,Temple=p.ZoneLihzhardTemple,Jungle=p.ZoneJungle,Expert=Main.expertMode,GoodWorld=Main.getGoodWorld,Day=Main.dayTime,Multiplayer=Main.netMode==1,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,GravityWorldSurface=Main.worldSurface,RockLayer=(float)Main.rockLayer};
        }
        private static int Index(NpcMotionState[] rows,NPC n){for(int i=0;i<rows.Length;i++)if(ReferenceEquals(rows[i].Identity.Token,n))return i;return -1;}
        private static int Count(int type){int count=0;for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type==type)count++;return count;}
        private static NPC Part(int type){for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type==type)return Main.npc[i];return null;}
        private static NPC Birth(int x,int slot){return Main.npc[NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),x,2100,245,Start:slot,Target:Main.myPlayer)];}
        private static void HitTo(NPC n,float remaining){Require(n!=null && n.active,"Original receiving part exists.");n.StrikeNPCNoInteraction(n.life-(int)(n.lifeMax*remaining)+n.defense+20,0,0);Require(n.active && n.life<n.lifeMax*remaining,"Genuine receiving strike crosses the requested native health threshold.");}
        private static void Setup(object context,int scene)
        {
            NativeCombatLiveContextChecks.FlightWorld();
            // The 240-high flight arena puts floor149 BELOW UnderworldLayer40.
            // Native SceneMetrics excludes Jungle in the Underworld regardless
            // of grass count. Extend the real tile grid, retaining the floor,
            // so these are actual underground Jungle scenes, not forced flags.
            var oldTiles=Main.tile;Main.maxTilesY=400;Main.bottomWorld=Main.maxTilesY*16;Main.tile=new Tile[Main.maxTilesX,Main.maxTilesY];
            for(int x=0;x<Main.maxTilesX;x++)for(int y=0;y<Main.maxTilesY;y++){if(y<oldTiles.GetLength(1))Main.tile[x,y]=oldTiles[x,y];else{var tile=new Tile();tile.active(true);tile.type=TileID.Stone;Main.tile[x,y]=tile;}}
            Main.Map=new Terraria.Map.WorldMap(Main.maxTilesX,Main.maxTilesY);NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.GameMode=scene==0?0:1;Main.dayTime=false;Main.time=1800;Main.getGoodWorld=scene==2;NPC.golemBoss=-1;
            if(Main.ItemDropSolver==null){Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);}for(int i=0;i<Main.item.Length;i++)Main.item[i].whoAmI=i;
            if(scene!=0)for(int x=0;x<Main.maxTilesX;x++)for(int y=150;y<170;y++)Main.tile[x,y].type=TileID.JungleGrass;
            if(scene==5)for(int y=120;y<150;y++){Main.tile[95,y].active(true);Main.tile[95,y].type=TileID.Stone;}
            for(int i=0;i<255;i++)Main.player[i].active=false;var p=Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};p.position=new Vector2(1100,2400-p.height);p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.immune=true;p.immuneTime=1000000;p.ResetFallDamage();for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
            Main.PlayerSceneMetrics.Reset();p.UpdateSceneMetrics();p.UpdateBiomes();Require(p.ZoneJungle==(scene!=0),"The original tile/biome scan establishes the real Golem Jungle condition.");
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions(path:false,mouseCenter:true,clearLine:false,radius:50));
        }
    }
}
