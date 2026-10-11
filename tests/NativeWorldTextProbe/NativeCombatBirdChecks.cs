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
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Locked Terraria1.4.5.8 AI024 oracles. Explicit one-action inputs are not
    // claims that a natural spawn chose those phases; Continuous is separate.
    internal static class NativeCombatBirdChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static readonly int[] Members={74,297,298,442,611,671,672,673,674,675,689};
        private static readonly List<string> network=new List<string>();
        private static Action<NPC> before,after;
        private static Player secondaryPlayer;
        private static int secondaryActions;
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static void BeforePlayer(Player __instance)
        {
            if(secondaryPlayer!=null && ReferenceEquals(__instance,Main.LocalPlayer) && secondaryPlayer.whoAmI<__instance.whoAmI)AdvanceSecondary();
        }
        private static void AfterPlayer(Player __instance)
        {
            if(secondaryPlayer!=null && ReferenceEquals(__instance,Main.LocalPlayer) && secondaryPlayer.whoAmI>__instance.whoAmI)AdvanceSecondary();
        }
        private static void AdvanceSecondary()
        {
            // RollingStep updates only its original one-player fixture. This
            // leaf adds a real numbered player, so complete the native player
            // slot loop in that same UpdatePlayers RNG scope before NPCs.
            // Recursion cannot repeat it: only the local player's hook enters.
            secondaryPlayer.Update(secondaryPlayer.whoAmI);secondaryActions++;
        }
        private static bool IsMember(int type){foreach(int member in Members)if(member==type)return true;return false;}
        private static bool Network(int msgType,int remoteClient,int ignoreClient,Terraria.Localization.NetworkText text,int number,float number2,float number3,float number4,int number5,int number6,int number7)
        {
            if(msgType!=23 && msgType!=54 || number<0 || number>=Main.maxNPCs)return true;
            var n=Main.npc[number];if(n==null || !IsMember(n.type) && n.type!=317)return true;
            network.Add(string.Join("/",msgType,remoteClient,ignoreClient,text,number,number2,number3,number4,number5,number6,number7));return false;
        }
        internal static void Boundaries()
        {
            var model=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcBirdMotion",true);
            var known=model.GetMethod("Known",Flags);var nearest=model.GetMethod("Nearest",Flags);
            foreach(int member in Members)Require((bool)known.Invoke(null,new object[]{new NpcMotionState{Identity=new NpcIdentity(1,new object(),0,1,member,member),Style=24}}),"Exact AI024 default member "+member);
            var n=new NpcMotionState{X=100,Y=100,Width=15,Height=21};
            var e=new PredictionEnvironment{PlayerIndex=0,PlayerTimelineActive=true,PlayerX=120,PlayerY=120,Players=new PredictionPlayers(new[]{new MotionRect(110,110,20,20),new MotionRect(105,105,20,20)},2,new[]{0,1},new[]{new MotionRect(110,110,20,20),new MotionRect(105,105,20,20)},new[]{true,false},new[]{false,false})};
            object[] args={n,e,0,0,default(MotionRect),PredictionStop.None};
            Require((bool)nearest.Invoke(null,args) && (int)args[3]==1,"FindClosest includes nondead ghost; SearchAlive's nonghost gate is not this search.");
            args[2]=1;Require(!(bool)nearest.Invoke(null,args) && (PredictionStop)args[5]==PredictionStop.MissingDependency,"Unknown other-player future geometry ends before the nearest-transform consumer.");
        }
        internal static void Lifecycle(object context)
        {
            var observer=new Harmony("JueMingR.Probe.BirdLifecycle");var send=typeof(NetMessage).GetMethod("SendData",Flags);
            observer.Patch(send,prefix:new HarmonyMethod(typeof(NativeCombatBirdChecks).GetMethod(nameof(Network),Flags)){priority=Priority.First+100});
            int checks=0;
            try
            {
                foreach(int member in Members)foreach(int phase in new[]{0,1,2})
                {
                    Setup(context);var n=Birth(member,16,new Vector2(1650.125f,2200.375f));
                    // Real allocated entity, deliberately sampled phase-input
                    // equivalent. The only executed AI is its original action.
                    n.ai[0]=phase;n.localAI[0]=1;n.velocity=new Vector2(phase==0?0:1.25f,phase==2?0:-.75f);
                    FullAction(context,n);checks++;
                }
                foreach(float vy in new[]{BitDecrement(.3f),.3f,BitIncrement(.3f),-.01f})
                {
                    Setup(context);var n=Birth(611,16,new Vector2(1650.125f,2200.375f));n.localAI[0]=1;n.velocity.Y=vy;FullAction(context,n);checks++;
                }
                foreach(float distance in new[]{79.5f,80f,80.5f})
                {
                    Setup(context);Main.dayTime=false;var p=Main.player[Main.myPlayer];var n=Birth(689,16,new Vector2(1650,2200));n.Center=p.Center+new Vector2(distance,0);Main.netMode=2;
                    FullAction(context,n);Require((distance<80)==(n.type==317),"Actual strict80 transform distance="+distance);
                    if(n.type==317){Require(n.aiStyle==2 && n.ai[0]==1 && n.localAI[0]==1 && !n.noGravity,"Transform317 retains the remainder of THISAI024 action after SetDefaults.");FullAction(context,n);}
                    checks++;
                }
                // One original pure nearest query per actual player collection,
                // including native all-dead/ghost/inactive fallback semantics.
                foreach(int kind in new[]{0,1,2,3})
                {
                    Setup(context);var n=Birth(689,16,new Vector2(1650.125f,2200.375f));var p=Main.player[Main.myPlayer];
                    p.dead=kind==1;p.ghost=kind==2;p.active=kind!=3;
                    var rows=Capture(context,n);var value=rows[Index(rows,n)];var e=EnvironmentFor(n);var nearest=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcBirdMotion",true).GetMethod("Nearest",Flags);
                    object[] args={value,e,0,0,default(MotionRect),PredictionStop.None};
                    Require((bool)nearest.Invoke(null,args) && (int)args[3]==Player.FindClosest(n.position,n.width,n.height),"Original FindClosest actual collection kind="+kind);checks++;
                }
                foreach(bool peerFirst in new[]{false,true})
                {
                    Setup(context);Main.dayTime=false;var town=Birth(17,peerFirst?30:0,new Vector2(1600,2390));var peer=Birth(611,peerFirst?0:30,new Vector2(1640,2200));var n=Birth(611,50,new Vector2(1600,2200));
                    n.ai[0]=1;n.localAI[0]=1;n.velocity.Y=-.75f;peer.ai[0]=0;peer.localAI[0]=1;
                    for(int x=90;x<=115;x++){Main.tile[x,142].active(true);Main.tile[x,142].type=1;}
                    Require(town.townNPC && !Collision.CanHit(n,town),"Original17 actual town row below the owl is genuinely obscured.");
                    var initialPosition=n.position;peer.active=false;FullAction(context,n);Require(n.ai[0]==2,"Actual blocked town without a same-type peer chooses original landing phase.");
                    peer.active=true;n.position=initialPosition;n.ai[0]=1;n.velocity.Y=-.75f;FullAction(context,n);
                    Require(n.ai[0]==1,"Native same-type peer terminates scan and preserves flight in both actual slot orders.");checks+=2;
                }
                Setup(context);var wet=Birth(671,16,new Vector2(1650.125f,2200.375f));wet.ai[0]=1;wet.ai[1]=7;wet.velocity.Y=1.25f;
                // Current wet is sampled native state; full original action
                // also recomputes future liquid from the actual fixed tiles.
                for(int x=102;x<=105;x++)for(int y=137;y<=140;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(0);}wet.wet=true;FullAction(context,wet);Require(wet.ai[1]==0,"Actual macaw wet tail clearsAI1 and applies original steering.");checks++;
                SourceClosure(context);
                Console.WriteLine("PASS Bird original full-action/scalar checks="+checks+" realNetwork="+string.Join(";",network));
            }
            finally{Main.netMode=0;observer.Unpatch(send,HarmonyPatchType.Prefix,observer.Id);}
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            string selection=Environment.GetEnvironmentVariable("JUEMINGR_BIRD_SCENE");int leaf=-1;
            if(!string.IsNullOrEmpty(selection) && (!int.TryParse(selection,out leaf) || leaf<0 || leaf>6))throw new ArgumentException("JUEMINGR_BIRD_SCENE must be0..6 or unset.");
            Console.WriteLine("BIRD actualLeaves="+(leaf<0?"0,1,2,3,4,5,6":leaf.ToString(CultureInfo.InvariantCulture)));
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var observer=new Harmony("JueMingR.Probe.BirdNatural");var update=typeof(NPC).GetMethod("UpdateNPC",Flags);var send=typeof(NetMessage).GetMethod("SendData",Flags);
            var playerUpdate=typeof(Player).GetMethod("Update",Flags,null,new[]{typeof(int)},null);
            observer.Patch(update,prefix:new HarmonyMethod(typeof(NativeCombatBirdChecks).GetMethod(nameof(Before),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatBirdChecks).GetMethod(nameof(After),Flags)));
            observer.Patch(playerUpdate,prefix:new HarmonyMethod(typeof(NativeCombatBirdChecks).GetMethod(nameof(BeforePlayer),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatBirdChecks).GetMethod(nameof(AfterPlayer),Flags)));
            observer.Patch(send,prefix:new HarmonyMethod(typeof(NativeCombatBirdChecks).GetMethod(nameof(Network),Flags)){priority=Priority.First+100});
            var records=new List<string>{"scene,frame,type,actions,matched,long,transform,fresh,maxError"};
            try
            {
                for(int scene=0;scene<7;scene++)
                {
                    if(leaf>=0 && scene!=leaf)continue;secondaryPlayer=null;secondaryActions=0;Setup(context);
                    Main.dayTime=scene<2 || scene==5;Main.cloudAlpha=scene==5?.7f:0;
                    int member=scene==0?74:scene==1?671:scene==2 || scene==5?611:689;
                    var n=Birth(member,16,new Vector2(1650.125f,2200.375f));
                    if(scene==3 || scene==6)n.Center=Main.player[Main.myPlayer].Center+new Vector2(60,0);
                    if(scene==6)
                    {
                        int slot=Main.myPlayer==0?1:0;
                        var competitor=Main.player[slot]=new Player{whoAmI=slot,active=true,isControlledByFilm=true,releaseJump=true};
                        competitor.position=Main.player[Main.myPlayer].position+new Vector2(300,0);competitor.ResetFallDamage();
                        competitor.statLife=competitor.statLifeMax=competitor.statLifeMax2=5000;competitor.immune=true;competitor.immuneTime=1000000;
                        for(int i=0;i<competitor.hurtCooldowns.Length;i++)competitor.hurtCooldowns[i]=1000000;
                        // Real equipped armor supplies750 aggro every original
                        // Player.UpdateEquips; no prefix overwrites aggro or
                        // pins the other player's future geometry.
                        competitor.armor[0].SetDefaults(1316);competitor.armor[1].SetDefaults(1317);competitor.armor[2].SetDefaults(1318);
                        secondaryPlayer=competitor;
                    }
                    if(scene==4)Main.netMode=1;
                    if(scene==2)
                    {
                        var town=Birth(17,0,new Vector2(1650,2390));Require(town.townNPC,"Natural original town row has actual town flag.");
                        for(int x=90;x<=115;x++){Main.tile[x,142].active(true);Main.tile[x,142].type=1;}
                    }
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                    var frozen=new List<NpcTrajectory>();int actions=0,matched=0,longMatches=0,transforms=0,fresh=0,searchBoundaries=0,competition=0;double max=0;
                    NpcMotionState expected=default(NpcMotionState);bool valid=false;PredictionStop stop=PredictionStop.None;int beforeType=member;
                    before=actor=>
                    {
                        if(!ReferenceEquals(actor,n) || !n.active)return;
                        var rows=Capture(context,n);expected=rows[Index(rows,n)];beforeType=n.type;terrain.Reset();
                        var environment=EnvironmentFor(n);
                        if(scene==6 && beforeType==689)
                        {
                            Require(secondaryActions==1 && secondaryPlayer.aggro==750,"The actual second-player UpdateEquips consumes all three real armor pieces before the first NPC action.");
                            Require(Player.FindClosest(n.position,n.width,n.height)==Main.myPlayer && expected.HasClosestPlayer && expected.ClosestPlayerIndex!=Main.myPlayer,"Actual nearest search and armor-aggro TargetClosest choose different players before689 transforms. nearest="+Player.FindClosest(n.position,n.width,n.height)+" closest="+expected.ClosestPlayerIndex+" secondaryAggro="+secondaryPlayer.aggro);competition++;
                            var future=expected;PredictionStop boundary;
                            Require(!NpcMotion.Step(ref future,rows,rows.Length,environment,terrain,1,true,out boundary) && boundary==PredictionStop.MissingDependency,"Unknown second-player future ends before the independent689 nearest consumer.");searchBoundaries++;
                        }
                        // elapsed0 here proves the genuinely observed current
                        // action only. The separate elapsed1 negative control
                        // above retains the unmodeled future dependency.
                        valid=NpcMotion.Step(ref expected,rows,rows.Length,environment,terrain,scene==6 && beforeType==689?0:1,true,out stop);
                        Require(valid || stop==PredictionStop.Despawn,"Natural known Bird action type="+n.type+" stop="+stop);
                    };
                    after=actor=>
                    {
                        if(!ReferenceEquals(actor,n))return;
                        if(!valid){Require(!n.active,"Original Bird actual retirement agrees with exact pure stop.");return;}
                        Require(PoseMatches(expected,n) && expected.A0==n.ai[0] && expected.Width==n.width && expected.Height==n.height && expected.NoGravity==n.noGravity,"Original Bird current full action type="+beforeType+" actualType="+n.type+" expected="+expected.X+","+expected.Y+" actual="+n.position);actions++;
                        if(beforeType==689 && n.type==317){transforms++;if(scene==6)Require(n.target!=Main.myPlayer,"Actual Transform317 executes its ordinary aggro query instead of adopting FindClosest's winner.");}
                    };
                    for(int frame=0;frame<600;frame++)
                    {
                        NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();
                        var path=cache.Read(0);
                        if(path!=null && ReferenceEquals(path.Identity.Token,n) && n.active)
                        {if(transforms>0){Require(path.Identity.Type==317,"Real Transform retires the old689 full identity before a restored317 publication.");fresh++;}if(frame%5==0 && path.Count>1 && !frozen.Contains(path))frozen.Add(path);}
                        for(int i=frozen.Count-1;i>=0;i--)
                        {
                            var old=frozen[i];int offset=(int)(Main.GameUpdateCount-old.CaptureTick);
                            if(offset>=old.Count || !n.active || old.Identity.Type!=n.type){frozen.RemoveAt(i);continue;}if(offset<1)continue;
                            var point=old[offset];double error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),n.position);max=Math.Max(max,error);
                            Require(error<=.08,"Frozen original Bird scene="+scene+" frame="+frame+" offset="+offset+" error="+error);matched++;if(offset>=30)longMatches++;
                        }
                        records.Add(string.Join(",",scene,frame,n.type,actions,matched,longMatches,transforms,fresh,max.ToString("R",CultureInfo.InvariantCulture)));
                    }
                    before=after=null;Require(actions>30 && matched>0 && longMatches>=30,"Natural Bird has actual useful action prefix and long frozen window.");
                    if(scene==3)Require(transforms==1 && fresh>0,"Actual689 natural transform has fresh317 publication and useful long continuation.");
                    if(scene==4)Require(n.type==689 && transforms==0,"Native client night does not transform689.");
                    if(scene==6)Require(secondaryActions==600 && competition==1 && searchBoundaries==1 && transforms==1 && fresh>0,"Real two-player competition preserves the unknown-future boundary and then restores a native317 publication with useful long continuation.");
                    Console.WriteLine("BIRD scene="+scene+" actions="+actions+" matched="+matched+" long="+longMatches+" transforms="+transforms+" fresh="+fresh+" competition="+competition+" searchBoundaries="+searchBoundaries+" secondaryActions="+secondaryActions+" max="+max);
                }
            }
            finally{Main.netMode=0;before=after=null;secondaryPlayer=null;secondaryActions=0;observer.Unpatch(update,HarmonyPatchType.All,observer.Id);observer.Unpatch(playerUpdate,HarmonyPatchType.All,observer.Id);observer.Unpatch(send,HarmonyPatchType.Prefix,observer.Id);File.WriteAllLines(Path.Combine(output,"bird-continuous.csv"),records);}
        }
        private static void FullAction(object context,NPC n)
        {
            var source=Get(Get(context,"CombatObservation"),"Prediction");var terrain=(IPredictionTerrain)Get(source,"Terrain");var rows=Capture(context,n);var expected=rows[Index(rows,n)];terrain.Reset();PredictionStop stop;
            bool valid=NpcMotion.Step(ref expected,rows,rows.Length,EnvironmentFor(n),terrain,1,true,out stop);n.UpdateNPC(n.whoAmI);
            Require(valid && PoseMatches(expected,n) && expected.A0==n.ai[0] && expected.L0==n.localAI[0] && expected.Life==n.life && expected.LifeMax==n.lifeMax && expected.Width==n.width && expected.Height==n.height && expected.NoGravity==n.noGravity,"Actual Bird full action type="+n.type+" stop="+stop+" AI0="+expected.A0+"/"+n.ai[0]+" V="+expected.Vx+","+expected.Vy+"/"+n.velocity);
        }
        private static NpcMotionState[] Capture(object context,NPC selected)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var terrain=(IPredictionTerrain)Get(source,"Terrain");long session=(long)Get(host,"Session");
            var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);var rows=new List<NpcMotionState>();
            for(int slot=0;slot<Main.maxNPCs;slot++)
            {
                var n=Main.npc[slot];if(n==null || !n.active)continue;var value=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,value,terrain};tracking.Invoke(null,args);value=(NpcMotionState)args[1];
                // This pure current-action oracle reads the real prefix's
                // observed town geometry. It does not certify its future AI;
                // the separate production Source closure checks that boundary.
                if(IsMember(n.type) || n.townNPC){value.BirdRelationsCaptured=true;value.BirdRelationsComplete=true;}rows.Add(value);
            }
            return rows.ToArray();
        }
        private static void SourceClosure(object context)
        {
            Setup(context);Main.dayTime=false;var selected=Birth(611,16,new Vector2(1650,2200));var peer=Birth(611,20,new Vector2(1700,2200));var town=Birth(17,0,new Vector2(1650,2390));var unrelated=Birth(689,30,new Vector2(3000,2200));
            // Observed flying peer cannot override the necessary unknown Town consumer.
            peer.ai[0]=1;peer.localAI[0]=1;
            var source=Get(Get(context,"CombatObservation"),"Prediction");var type=source.GetType();var owner=Activator.CreateInstance(type,Flags,null,new object[]{null,false},CultureInfo.InvariantCulture);
            try
            {
                var cache=(NpcPredictionCache)Get(owner,"Cache");cache.Demand(0,1,32);long session=(long)Get(Get(context,"CombatObservation"),"Session");
                var state=(NpcMotionState)type.GetMethod("Read",Flags).Invoke(null,new object[]{selected,session});type.GetMethod("Prepare",Flags).Invoke(owner,new object[]{state.Identity,(long)Main.GameUpdateCount});
                int count=(int)Get(owner,"observedCount");var rows=(NpcMotionState[])Get(owner,"states");bool own=false,hasPeer=false,hasTown=false,hasOther=false;
                for(int i=0;i<count;i++){own|=ReferenceEquals(rows[i].Identity.Token,selected);hasPeer|=ReferenceEquals(rows[i].Identity.Token,peer);hasTown|=ReferenceEquals(rows[i].Identity.Token,town);hasOther|=ReferenceEquals(rows[i].Identity.Token,unrelated);}
                Require(own && hasPeer && hasTown && !hasOther,"Production Source captures actual same-type/town night-census identities, excluding an unrelated different bird type.");
                var ownRow=rows[Index(rows,selected)];Require(ownRow.BirdRelationsCaptured && ownRow.BirdRelationsComplete,"Actual Source exposes complete night owl relation facts.");
                var townRow=rows[Index(rows,town)];var roles=(bool[])Get(owner,"motionRoles");
                Require(townRow.BirdRelationsCaptured && !townRow.BirdRelationsComplete && !roles[Index(rows,town)],"Unknown original17 is an explicitly unavailable future geometry role, not an invented frozen town motor.");
                var model=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcBirdMotion",true).GetMethod("Step",Flags);
                var env=EnvironmentFor(selected);object[] initialization={ownRow,env,(IPredictionTerrain)Get(owner,"Terrain"),1,PredictionStop.None,rows,count,false};
                Require((bool)model.Invoke(null,initialization),"Unknown town does not reject the original owl initialization before any census geometry consumer.");
                var initialized=(NpcMotionState)initialization[0];initialized.A0=1;initialized.L0=1;
                object[] census={initialized,env,(IPredictionTerrain)Get(owner,"Terrain"),2,PredictionStop.None,rows,count,false};
                Require(!(bool)model.Invoke(null,census) && (PredictionStop)census[4]==PredictionStop.MissingDependency,"The first actual town geometry consumer stops honestly before reading an unavailable future Center.");
                Main.dayTime=true;type.GetMethod("Prepare",Flags).Invoke(owner,new object[]{state.Identity,(long)Main.GameUpdateCount});count=(int)Get(owner,"observedCount");
                Require(count==1,"Original day owl has no global town/peer scan consumer and no unrelated night collection.");
            }
            finally{type.GetMethod("Clear",Flags).Invoke(owner,null);}
        }
        private static PredictionEnvironment EnvironmentFor(NPC n)
        {
            var areas=new List<MotionRect>();var precise=new List<MotionRect>();var slots=new List<int>();var alive=new List<bool>();var dead=new List<bool>();
            for(int slot=0;slot<Main.maxPlayers;slot++)
            {var p=Main.player[slot];if(p==null || !p.active)continue;areas.Add(new MotionRect((int)p.position.X,(int)p.position.Y,p.width,p.height));precise.Add(new MotionRect(p.position.X,p.position.Y,p.width,p.height));slots.Add(slot);alive.Add(!p.dead && !p.ghost);dead.Add(p.dead);}
            int winner=n.type==689 && !Main.dayTime && Main.netMode!=1?Player.FindClosest(n.position,n.width,n.height):n.target>=0 && n.target<255?n.target:Main.myPlayer;
            var pc=Main.player[winner];var zero=Main.player[0];
            return new PredictionEnvironment{Players=new PredictionPlayers(areas.ToArray(),areas.Count,slots.ToArray(),precise.ToArray(),alive.ToArray(),dead.ToArray()),BirdWeatherCaptured=true,BirdCloudAlpha=Main.cloudAlpha,BirdInactiveZeroCaptured=areas.Count==0 && zero!=null && !zero.active,BirdInactiveZero=zero==null?default(MotionRect):new MotionRect(zero.position.X,zero.position.Y,zero.width,zero.height),PlayerIndex=winner,PlayerX=pc.Center.X,PlayerY=pc.Center.Y,PlayerWidth=pc.width,PlayerHeight=pc.height,PlayerVx=pc.velocity.X,PlayerVy=pc.velocity.Y,PlayerVelocityCaptured=true,PlayerTimelineActive=pc.active && !pc.dead && !pc.ghost,PlayerDead=!pc.active || pc.dead,WindTarget=Main.windSpeedTarget,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,GravityWorldSurface=Main.worldSurface,RockLayer=(float)Main.rockLayer,Day=Main.dayTime,GoodWorld=Main.getGoodWorld,Multiplayer=Main.netMode==1};
        }
        private static NPC Birth(int type,int start,Vector2 position)
        {
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),(int)position.X,(int)position.Y,type,Start:start,Target:Main.myPlayer);
            Require(slot>=0 && slot<Main.maxNPCs,"Actual Bird/town NewNPC birth allocates a legal native identity.");var n=Main.npc[slot];n.position=position;return n;
        }
        private static void Setup(object context)
        {
            // Original CPU profile construction guards texture requests with
            // dedServ. Restore it before any real NewNPC/AI so native town
            // variation/name and the bird's client/server behavior still run.
            bool dedicated=Main.dedServ;
            try{Main.dedServ=true;System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Terraria.GameContent.TownNPCProfiles).TypeHandle);}
            finally{Main.dedServ=dedicated;}
            NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();for(int i=0;i<2;i++){NativeQuickItemChecks.BeginWorldStep();Main.EverLastingTicker++;NPC.UpdateProtectedSpawnSlots();}
            Main.netMode=0;Main.GameMode=0;Main.dayTime=true;Main.cloudAlpha=0;Main.getGoodWorld=false;
            for(int i=0;i<255;i++)Main.player[i].active=false;
            var p=Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};p.position=new Vector2(1750,2400-p.height);p.ResetFallDamage();p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.immune=true;p.immuneTime=1000000;for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions(path:false,mouseCenter:true,clearLine:false,radius:50));
        }
        private static float BitIncrement(float value){return BitConverter.ToSingle(BitConverter.GetBytes(BitConverter.ToInt32(BitConverter.GetBytes(value),0)+1),0);}
        private static float BitDecrement(float value){return BitConverter.ToSingle(BitConverter.GetBytes(BitConverter.ToInt32(BitConverter.GetBytes(value),0)-1),0);}
        private static int Index(NpcMotionState[] group,NPC n){for(int i=0;i<group.Length;i++)if(ReferenceEquals(group[i].Identity.Token,n))return i;throw new InvalidOperationException("Missing actual Bird fixture full identity.");}
        private static bool PoseMatches(NpcMotionState expected,NPC n){return Vector2.Distance(new Vector2(expected.X,expected.Y),n.position)<=.08 && Math.Abs(expected.Vx-n.velocity.X)<=.0001 && Math.Abs(expected.Vy-n.velocity.Y)<=.0001;}
    }
}
