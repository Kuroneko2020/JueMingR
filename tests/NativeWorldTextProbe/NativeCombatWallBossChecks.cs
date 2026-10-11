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
    internal static class NativeCombatWallBossChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Action<NPC> before,after,death;
        private static Action<Player> playerBefore,playerAfter;
        private static Action<Player> healthBefore,healthAfter;
        private static Action<bool> worldPrepare;
        private struct HurtSample { internal string Facts; }
        private static Action<Player,double,Terraria.DataStructures.PlayerDeathReason,int,int,HurtSample> receivedHit;
        private static readonly List<string> outlets=new List<string>();
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static void PlayerBefore(Player __instance){playerBefore?.Invoke(__instance);}
        private static void PlayerAfter(Player __instance){playerAfter?.Invoke(__instance);}
        private static void HealthBefore(Player __instance){healthBefore?.Invoke(__instance);}
        private static void HealthAfter(Player __instance){healthAfter?.Invoke(__instance);}
        private static void WorldBefore(){worldPrepare?.Invoke(false);}
        private static void WorldAfter(){worldPrepare?.Invoke(true);}
        private static void BeforeHurt(Player __instance,out HurtSample __state)
        {__state=new HurtSample{Facts=HealthFacts(__instance)};}
        private static void AfterHurt(Player __instance,double __result,Terraria.DataStructures.PlayerDeathReason damageSource,int Damage,int hitDirection,HurtSample __state)
        {if(__result>0)receivedHit?.Invoke(__instance,__result,damageSource,Damage,hitDirection,__state);}
        private static void Death(NPC __instance){death?.Invoke(__instance);}
        private static bool Family(NPC n){return n!=null && n.type>=113 && n.type<=115;}
        private static bool IsolateNetwork(int msgType,int remoteClient,int ignoreClient,Terraria.Localization.NetworkText text,int number,float number2,float number3,float number4,int number5,int number6,int number7)
        {
            if(msgType!=28 || number<0 || number>=Main.maxNPCs || !Family(Main.npc[number]))return true;
            // Preserve StrikeNPC/checkDead and every original argument. Only
            // the real network outlet is isolated from the user's machine.
            outlets.Add(string.Join("/",msgType,remoteClient,ignoreClient,text,number,number2,number3,number4,number5,number6,number7));return false;
        }
        private static Type Model{get{return typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcWallBossMotion",true);}}
        internal static void Boundaries()
        {
            Setup(null,0);var body=new NPC();body.SetDefaults(113);body.whoAmI=16;body.position=new Vector2(1600,4000);body.velocity=new Vector2(1.5f,0);body.active=true;body.target=255;Main.npc[16]=body;
            var players=new[]{Main.player[0],Main.player[1],Main.player[2]};var winner=Model.GetMethod("Query",Flags);var kernel=Model.GetMethod("Step",Flags);var begin=Model.GetMethod("BeginFrame",Flags);
            for(int mode=0;mode<8;mode++)
            {
                for(int i=0;i<3;i++){players[i].active=true;players[i].dead=players[i].ghost=false;players[i].gross=i!=2;players[i].aggro=0;players[i].position=new Vector2(1600+(i-1)*100,4000);players[i].npcTypeNoAggro[113]=false;players[i].tankPet=-1;}
                if(mode==1)players[0].aggro=-1000;if(mode==2)players[1].npcTypeNoAggro[113]=true;if(mode==3)players[0].dead=true;
                if(mode==4)players[0].ghost=true;if(mode==5)for(int i=0;i<3;i++)players[i].gross=false;
                body.target=mode==6?255:1;body.direction=mode==7?0:1;body.confused=mode==7;
                var state=Basic(body,113,27);state.TargetCaptured=state.HasPlayer=true;state.PlayerIndex=state.Target=body.target;state.PlayerNativeDead=body.target<255 && Main.player[body.target].dead;
                if(body.target<255){var p=Main.player[body.target];state.PlayerArea=new MotionRect((int)p.position.X,(int)p.position.Y,p.width,p.height);state.PlayerActive=p.active;}
                var e=EnvironmentFor(body);object[] args={state,e,new EmptyTerrain(),1,body.confused,PredictionStop.None,true};
                Require((bool)winner.Invoke(null,args),"Complete current native WOF query facts.");var predicted=(NpcMotionState)args[0];body.TargetClosest_WOF();
                Require(predicted.Target==body.target && predicted.Direction==body.direction && predicted.DirectionY==body.directionY,"Original WOF Manhattan/gross/dead/ghost/noaggro/tie/fallback query mode="+mode+" predicted="+predicted.Target+" actual="+body.target);
            }
            var owner=Basic(body,113,27);owner.Active=true;owner.Life=0;
            var child=Basic(new NPC{whoAmI=17,position=new Vector2(1500,4000)},114,28);child.Width=child.Height=100;child.WallBossValid=true;child.WallBossIdentity=owner.Identity;child.WallTop=3360;child.WallBottom=4400;
            var group=new[]{owner,child};begin.Invoke(null,new object[]{group,2});Require(group[1].WallBossValid,"Native frame cleanup accepts active113 with zero life.");
            owner.Active=false;group[0]=owner;object[] stepArgs={child,EnvironmentFor(body),new EmptyTerrain(),1,PredictionStop.None,group,2,false};
            Require((bool)kernel.Invoke(null,stepArgs),"A still-positive inactive raw global supplies THIS slot's eye geometry.");
            stepArgs[0]=child;stepArgs[5]=new[]{child};stepArgs[6]=1;Require(!(bool)kernel.Invoke(null,stepArgs) && (PredictionStop)stepArgs[4]==PredictionStop.MissingDependency,"Missing full raw global identity is not orphan retirement.");
            begin.Invoke(null,new object[]{group,2});Require(!group[1].WallBossValid,"The next native frame clears inactive raw owner.");stepArgs[0]=group[1];stepArgs[5]=group;stepArgs[6]=2;
            Require(!(bool)kernel.Invoke(null,stepArgs) && (PredictionStop)stepArgs[4]==PredictionStop.Despawn,"Known negative global deterministically retires its eye.");
            var hp=new PredictionPlayerMotion{WallHealthCaptured=true,WallHealthKnown=true,WallLocal=true,WallLife=400,WallLifeMax=400,WallTongued=true};var health=Model.GetMethod("AdvanceHealth",Flags);var expert=new PredictionEnvironment{Expert=true};
            for(int tick=1;tick<=120;tick++){object[] a={hp,expert,PredictionStop.None};Require((bool)health.Invoke(null,a),"Canonical existing ExpertTongue DOT remains determined before death.");hp=(PredictionPlayerMotion)a[0];Require(hp.WallLife==400-tick/6*5 && hp.WallRegenCount==-(tick%6)*100 && hp.WallRegenTime==1,"Original ExpertTongue -100/+600/5HP clocks tick="+tick);}
            hp.WallLife=5;hp.WallRegenCount=-500;object[] lethal={hp,expert,PredictionStop.None};Require(!(bool)health.Invoke(null,lethal) && (PredictionStop)lethal[2]==PredictionStop.Despawn,"Existing DOT kills before this same action's NPC target read.");
            hp.BordersGodMode=true;lethal[0]=hp;Require((bool)health.Invoke(null,lethal) && ((PredictionPlayerMotion)lethal[0]).WallLife==0,"Creative GodMode prevents KillMe, not native regen HP subtraction.");
            hp.WallHealthKnown=false;lethal[0]=hp;Require(!(bool)health.Invoke(null,lethal) && (PredictionStop)lethal[2]==PredictionStop.MissingDependency,"Uncaptured effective regen modifiers cannot pretend health is static.");
            var pull=Model.GetMethod("AdvancePlayer",Flags);var contact=new PredictionPlayerMotion{WallFactsCaptured=true,WallLocal=true,WallHorrifiedTicks=10,WallHealthCaptured=true,WallHealthKnown=true,WallLife=400,WallLifeMax=400,WallImmunityCaptured=true,WallImmune=true,WallImmuneTime=2,X=960,Y=4000,Width=20,Height=42};
            var raw=new PredictionWallSource(true,true,owner.Identity,new MotionRect(1000,4000,100,100),1,1.5f,0);object[] immunity={contact,new PredictionEnvironment{WorldWidth=400,WorldHeight=400},new EmptyTerrain(),raw,false,PredictionStop.None};
            Require((bool)pull.Invoke(null,immunity) && ((PredictionPlayerMotion)immunity[0]).WallImmuneTime==1,"Native immunity decrements once before known rejected WOF contact.");
            contact.WallImmuneTime=1;immunity[0]=contact;Require(!(bool)pull.Invoke(null,immunity) && (PredictionStop)immunity[5]==PredictionStop.PhaseBoundary,"Native immunity expiry exposes this action's actual contact Hurt consumer before movement.");
            Console.WriteLine("PASS Wall native targeting/rawglobal/frameclear/ExpertTongue health boundaries");
        }
        internal static void Lifecycle(object context)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");long session=(long)Get(host,"Session");var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");
            Binding(source);
            FallReceiving(source,terrain,context);
            TouchingTiles(context,terrain);
            TongueCenters(source,terrain,context);
            HungryGoal();
            Setup(context,0);var body=Birth(1600,16);body.UpdateNPC(body.whoAmI);var eye=Part(114,true);Require(eye!=null && Count(114)==2 && Count(115)==11,"Original first Wall action creates its complete eyes/Hungry family.");
            var net=new Harmony("JueMingR.Probe.WallOriginalNetwork");var observed=new Harmony("JueMingR.Probe.WallActiveZero");var send=typeof(NetMessage).GetMethod("SendData",Flags);var checkDead=typeof(NPC).GetMethod("checkDead",Flags);outlets.Clear();
            net.Patch(send,prefix:new HarmonyMethod(typeof(NativeCombatWallBossChecks).GetMethod(nameof(IsolateNetwork),Flags)){priority=Priority.First+100});bool activeZero=false;
            death=n=>{if(!ReferenceEquals(n,body) || !n.active || n.life>0)return;var rows=Capture(read,tracking,terrain,session,eye);Require(rows[Index(rows,eye)].WallBossValid,"Real lethal receiving exposes active-life0 raw owner before native checkDead.");activeZero=true;};
            observed.Patch(checkDead,prefix:new HarmonyMethod(typeof(NativeCombatWallBossChecks).GetMethod(nameof(Death),Flags)));
            try
            {
                body.StrikeNPCNoInteraction(body.lifeMax+body.defense+100,0,0);Require(activeZero && !body.active,"Actual lethal body receiving retires the owner.");Require(Main.wofNPCIndex==body.whoAmI,"Death does not prematurely clear native raw global.");
                var rows=Capture(read,tracking,terrain,session,eye);var predicted=rows[Index(rows,eye)];terrain.Reset();PredictionStop stop;bool valid=NpcMotion.Step(ref predicted,rows,rows.Length,EnvironmentFor(eye),terrain,1,true,out stop);eye.UpdateNPC(eye.whoAmI);
                Require(valid && Vector2.Distance(new Vector2(predicted.X,predicted.Y),eye.position)<=.08,"Current eye consumes the same inactive raw owner geometry before frame cleanup.");
                typeof(Main).GetMethod("CheckBossIndexes",Flags).Invoke(null,null);Require(Main.wofNPCIndex<0,"Original next-frame cleanup clears dead WOF index.");
                rows=Capture(read,tracking,terrain,session,eye);predicted=rows[Index(rows,eye)];valid=NpcMotion.Step(ref predicted,rows,rows.Length,EnvironmentFor(eye),terrain,1,true,out stop);eye.UpdateNPC(eye.whoAmI);
                Require(!valid && stop==PredictionStop.Despawn && !eye.active,"Actual orphan eye retires at the known negative global boundary.");
                Require(outlets.Count>0,"Original lethal strike network outlet remains observed.");Console.WriteLine("PASS Wall real birth/activezero/death/rawglobal/nextframe/orphan outlets="+outlets.Count+" parameters="+string.Join(";",outlets));
            }
            finally{death=null;observed.Unpatch(checkDead,HarmonyPatchType.Prefix,observed.Id);net.Unpatch(send,HarmonyPatchType.Prefix,net.Id);}
        }
        private static void TongueCenters(object source,IPredictionTerrain terrain,object context)
        {
            var readPlayer=source.GetType().GetMethod("ReadPlayer",Flags);
            // The original failing velocity is below the pull speed (snap).
            // Exercise the returned Entity.Center field at both sides of the
            // x4096 precision boundary, then consume the full native Update.
            float[] xs={4086.318603515625f,4086.318359375f,4085.999755859375f,4086f};
            foreach(float x in xs)
            {
                Setup(context,1);var p=Main.LocalPlayer;p.position=new Vector2(x,3899.25f);p.velocity=Vector2.Zero;p.ResetFallDamage();p.AddBuff(37,10);p.AddBuff(38,10);p.gross=p.tongued=true;
                var body=new NPC();body.SetDefaults(113);body.whoAmI=16;body.active=true;body.target=p.whoAmI;body.direction=1;body.position=new Vector2(3905.5f-body.width/2f,3914.863525390625f-body.height/2f);body.velocity=new Vector2(1.5f,0);Main.npc[16]=body;Main.wofNPCIndex=16;
                terrain.Reset();var predicted=(PredictionPlayerMotion)readPlayer.Invoke(null,new object[]{p});PredictionStop stop;
                Require(PlayerMotionContinuation.Advance(ref predicted,EnvironmentFor(body),terrain,out stop),"Known current native Tongue center input remains determined: "+stop);
                p.Update(p.whoAmI);
                Require(predicted.Vx==p.velocity.X && predicted.Vy==p.velocity.Y,"Original full player Tongue float center writeback x="+x.ToString("R",CultureInfo.InvariantCulture)+" predictedV="+predicted.Vx.ToString("R",CultureInfo.InvariantCulture)+","+predicted.Vy.ToString("R",CultureInfo.InvariantCulture)+" actualV="+p.velocity);
                Require(Vector2.Distance(new Vector2(predicted.X,predicted.Y),p.position)<=.08,"Original Tongue movement follows its exact current velocity.");
            }
            Console.WriteLine("PASS Wall original full player Tongue center writeback four current inputs");
        }
        private static void HungryGoal()
        {
            // R34 scene7/frame1467 records this same-action native input and
            // Vy=-7.338743 after AI. The direct native anchor+offset comparison
            // stays below position even though its float-rounded value is equal.
            float position=3810.225341796875f,anchor=3304f,offset=506.22528076171875f;
            Require((float)((double)anchor+offset)==position && (double)anchor+offset<position,"Captured Hungry goal lies below its rounded position boundary.");
            var hungry=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcHungryMotion",true);
            object[] args={-7.23874331f,position,anchor,offset,.1f};
            hungry.GetMethod("Axis",Flags).Invoke(null,args);
            Require((float)args[0]==-7.338743f,"Captured native Hungry velocity decrements strictly at the extended goal boundary.");
            Console.WriteLine("PASS Wall Hungry captured R34 strict scalar goal/velocity boundary");
        }
        private static void Binding(object source)
        {
            var bind=source.GetType().GetMethod("TryBindPlayerWall",Flags);var identity=new NpcIdentity(1,new object(),16,1,113,113);var token=new object();
            var p=new PredictionPlayerMotion{PlayerToken=token,PlayerIndex=0,WallFactsCaptured=true,WallHealthCaptured=true,WallHealthKnown=true,WallLife=400,WallLifeMax=400,Width=20,Height=42};
            var values=new PredictionWallSource[5];var points=new NpcTrajectoryPoint[5];for(int i=0;i<5;i++){values[i]=new PredictionWallSource(true,true,identity,new MotionRect(100+i,200+i,100,100),1,1.5f,0);points[i]=new NpcTrajectoryPoint(i,new NpcMotionState{Width=20,Height=20});}
            foreach(bool beforeNpc in new[]{false,true})foreach(int age in new[]{0,1})
            {
                long sample=100,current=sample+age+(beforeNpc?1:0);var payload=new PredictionWallSources(values,5,token,0,identity,sample);var path=new NpcTrajectory(identity,sample,1,PredictionAssumption.None,PredictionStop.None,points,5,PredictionStrategy.RollingConditional,wallSources:payload);
                object[] args={path,current,p,new PredictionEnvironment()};Require((bool)bind.Invoke(null,args),"Actual Wall payload phase/age binds original player token.");var motion=(PredictionPlayerMotion)args[2];var env=(PredictionEnvironment)args[3];
                Require(motion.WallAction==age+(beforeNpc?1:0) && env.WallSources[motion.WallAction].Area.X==100+age+(beforeNpc?1:0),"BeforeNPC/completed-world Wall ordinal uses actual GUC.");
                NpcTrajectory window;Require(path.TryWindow(sample+1,2,2,out window),"Wall immutable source and points share window offset.");args=new object[]{window,current+1,p,new PredictionEnvironment()};Require((bool)bind.Invoke(null,args) && ((PredictionEnvironment)args[3]).WallSources[((PredictionPlayerMotion)args[2]).WallAction].Area.X==101+age+(beforeNpc?1:0),"Sliced source offsets preserve capture metadata.");
                var clone=p;clone.PlayerToken=new object();args=new object[]{path,current,clone,new PredictionEnvironment()};Require(!(bool)bind.Invoke(null,args) && ((PredictionPlayerMotion)args[2]).WallSourceExpected,"Same numbered clone cannot borrow another player's body source.");
                PredictionStop stop;bool handled;Require(PlayerMotionContinuation.BeginWall(ref motion,env,new EmptyTerrain(),out handled,out stop) && !handled && PlayerMotionContinuation.BeginWall(ref motion,env,new EmptyTerrain(),out handled,out stop) && !handled,"Two true launch-delay prefixes consume one common immutable Wall sequence.");
                Require(motion.WallAction==age+(beforeNpc?1:0)+2 && ReferenceEquals(motion.PlayerToken,token),"Pre-roll motion keeps original token and ordinal; no ReadPlayer(clone) rebinding.");bool refused=false;try{path.Republish(sample+1,3);}catch(InvalidOperationException){refused=true;}Require(refused,"Conditional body payload cannot restamp another capture.");
            }
            Console.WriteLine("PASS Wall source phase/age/window/token/preroll binding matrix");
        }
        private static void FallReceiving(object source,IPredictionTerrain terrain,object context)
        {
            var readPlayer=source.GetType().GetMethod("ReadPlayer",Flags);var fall=Model.GetMethod("AdvanceFall",Flags);
            var original=typeof(Player).GetMethod("Hurt",Flags,null,new[]{typeof(Terraria.DataStructures.PlayerDeathReason),typeof(int),typeof(int),typeof(bool),typeof(bool),typeof(bool),typeof(int),typeof(bool)},null);
            var hook=new Harmony("JueMingR.Probe.WallFallReceiving");
            hook.Patch(original,prefix:new HarmonyMethod(typeof(NativeCombatWallBossChecks).GetMethod(nameof(BeforeHurt),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatWallBossChecks).GetMethod(nameof(AfterHurt),Flags)));
            int actualHits=0;
            try
            {
                for(int variant=0;variant<12;variant++)
                {
                    Setup(context,0);var p=Main.LocalPlayer;int row=(int)(p.position.Y/16f);
                    int distance=variant==0?25:variant==1?26:29;p.fallStart=row-distance;
                    if(variant==1)p.forcedGravity=2;
                    if(variant==2){p.gravDir=-1;p.fallStart=row+26;}
                    if(variant==3)p.extraFall=4;if(variant==4)p.noFallDmg=true;if(variant==5)p.hasWings=true;if(variant==6)p.creativeGodMode=true;
                    if(variant>=7)
                    {
                        int support=p.gravDir>0?(int)((p.position.Y+p.height+1)/16f):(int)((p.position.Y-1)/16f);
                        for(int x=(int)(p.position.X/16f);x<=(int)((p.position.X+p.width)/16f);x++)
                        {var tile=Main.tile[x,support];tile.active(true);tile.type=variant==11?TileID.Stone:variant==9 || variant==10?TileID.Platforms:TileID.Cloud;tile.inActive(variant==8);tile.frameY=(short)(variant==9?49*18:variant==10?48*18:0);}
                    }
                    terrain.Reset();var sampled=(PredictionPlayerMotion)readPlayer.Invoke(null,new object[]{p});
                    Require(sampled.WallFallCaptured && sampled.WallFallKnown,"Actual canonical native fall facts are captured without equipment or fake immunity.");
                    object[] args={sampled,EnvironmentFor(Main.npc[0]),terrain,PredictionStop.None};bool valid=(bool)fall.Invoke(null,args);
                    if(variant==1)
                    {
                        // The original fall prefix still sees old +1 gravity;
                        // the known forced clock flips it only LATER. Both pure
                        // callers must stop before spending that gravity action.
                        var env=EnvironmentFor(Main.npc[0]);env.GravitySourceCaptured=true;env.GravitySourceValid=false;
                        var ordinary=sampled;PredictionStop orderedStop;terrain.Reset();
                        Require(!PlayerMotionContinuation.Advance(ref ordinary,env,terrain,out orderedStop) && orderedStop==PredictionStop.PhaseBoundary && ordinary.ForcedGravityTicks==2 && ordinary.GravityDirection==1,"Ordinary continuation consumes old-gravity fall before the forced gravity clock.");
                        object[] rollingArgs={sampled,env,terrain,PredictionStop.None};terrain.Reset();
                        Require(!(bool)typeof(RollingNpcPrediction).GetMethod("AdvancePlayer",Flags).Invoke(new RollingNpcPrediction(),rollingArgs) && (PredictionStop)rollingArgs[3]==PredictionStop.PhaseBoundary && ((PredictionPlayerMotion)rollingArgs[0]).ForcedGravityTicks==2 && ((PredictionPlayerMotion)rollingArgs[0]).GravityDirection==1,"Rolling continuation consumes the same fall prefix once before gravity or the settled fast path.");
                    }
                    bool received=false;int requested=0;
                    receivedHit=(actor,result,reason,damage,direction,beforeHit)=>{if(!ReferenceEquals(actor,p))return;Require(reason.SourceOtherIndex==0,"Actual fall observer records native ByOther(0).");received=true;requested=damage;actualHits++;};
                    using(Main.SwapRandom("UpdatePlayers"))p.Update(p.whoAmI);
                    bool shouldHit=variant==1 || variant==2 || variant==10 || variant==11;
                    Require(received==shouldHit && valid==!received && (valid || (PredictionStop)args[3]==PredictionStop.PhaseBoundary),"Pure stops at the actual ordinary fall Hurt consumer variant="+variant+" received="+received+" valid="+valid+" stop="+args[3]+" requested="+requested);
                    if(valid)
                    {var next=(PredictionPlayerMotion)args[0];Require(next.WallFallStart==p.fallStart && !next.WallNoFallDamage && !next.WallHasWings && next.WallExtraFall==0,"Fall consumes completed flags before the canonical ResetEffects rebuild variant="+variant);}
                }
                Setup(context,0);var dependent=Main.LocalPlayer;dependent.fallStart=(int)(dependent.position.Y/16f)-29;
                var raw=(PredictionPlayerMotion)readPlayer.Invoke(null,new object[]{dependent});object[] missing={raw,EnvironmentFor(Main.npc[0]),new EmptyTerrain(true),PredictionStop.None};
                Require(!(bool)fall.Invoke(null,missing) && (PredictionStop)missing[3]==PredictionStop.TerrainUnavailable,"Unknown necessary foot cells cannot become safe air or invent a certified fall hit.");
                dependent.stoned=true;raw=(PredictionPlayerMotion)readPlayer.Invoke(null,new object[]{dependent});missing[0]=raw;missing[2]=new EmptyTerrain();
                Require(!raw.WallFallKnown && !(bool)fall.Invoke(null,missing) && (PredictionStop)missing[3]==PredictionStop.MissingDependency,"Real noncanonical stone fall needs its own receiving profile, not the ordinary threshold.");
                Console.WriteLine("PASS Wall original Player.Update fall receiving matrix cases=12 actualHits="+actualHits);
            }
            finally{receivedHit=null;hook.Unpatch(original,HarmonyPatchType.All,hook.Id);}
        }
        private static void TouchingTiles(object context,IPredictionTerrain terrain)
        {
            var query=terrain.GetType().GetMethod("PlayerTouchingTiles",Flags);int checks=0;
            foreach(int kind in new[]{0,1,2,3,4,5,6,7})foreach(float vy in new[]{-11f,-5f,0f,5f,11f})foreach(float grav in new[]{-1f,1f})foreach(float fraction in new[]{0f,.25f,15.75f})
            {
                Setup(context,0);for(int x=95;x<=106;x++)for(int y=238;y<=249;y++)Main.tile[x,y]=new Tile();
                for(int x=96;x<=105;x++)
                {
                    var tile=Main.tile[x,245];if(kind!=0){tile.active(true);tile.type=kind==4?TileID.Platforms:kind==5?TileID.PinkSlimeBlock:TileID.Stone;}
                    if(kind==2)tile.slope(1);if(kind==3)tile.halfBrick(true);if(kind==6)tile.inActive(true);if(kind==7){tile.active(false);tile.liquid=255;}
                }
                if(kind==1)for(int y=238;y<=248;y++){Main.tile[100,y].active(true);Main.tile[100,y].type=TileID.Stone;}
                var p=Main.LocalPlayer;p.position=new Vector2(99*16+fraction,245*16-p.height-4);p.velocity=new Vector2(3.125f,vy);p.gravDir=grav;
                p.UpdateTouchingTiles();bool nativeHead=grav>0?Collision.up:Collision.down;int bounce=int.MinValue;
                foreach(var point in p.TouchedTiles){var tile=Main.tile[point.X,point.Y];if(tile!=null && tile.active() && tile.nactive() && Main.tileBouncy[tile.type]){bounce=point.Y;break;}}
                terrain.Reset();var n=new NpcMotionState{X=p.position.X,Y=p.position.Y,Vx=p.velocity.X,Vy=p.velocity.Y,Width=p.width,Height=p.height};object[] args={n,grav,false,int.MinValue,PredictionStop.None};
                Require((bool)query.Invoke(terrain,args) && (bool)args[2]==nativeHead && (int)args[3]==bounce,"Native UpdateTouchingTiles scratch/firstbouncy kind="+kind+" vy="+vy+" grav="+grav+" fraction="+fraction+" original="+nativeHead+"/"+bounce+" predicted="+args[2]+"/"+args[3]+" stop="+args[4]);checks++;
            }
            Console.WriteLine("PASS Wall true UpdateTouchingTiles native queries="+checks);
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            string leafText=Environment.GetEnvironmentVariable("JUEMINGR_WALL_HEALTH_SCENE");int leaf=-1;
            if(!string.IsNullOrEmpty(leafText))Require(int.TryParse(leafText,NumberStyles.Integer,CultureInfo.InvariantCulture,out leaf) && leaf>=0 && leaf<13,"JUEMINGR_WALL_HEALTH_SCENE must name an existing natural scene0..12.");
            string leavesText=Environment.GetEnvironmentVariable("JUEMINGR_WALL_HEALTH_SCENES");var sceneIds=new List<int>();
            Require(string.IsNullOrEmpty(leafText) || string.IsNullOrEmpty(leavesText),"Wall single-scene and explicit group selectors cannot both be set.");
            if(!string.IsNullOrEmpty(leavesText))
            {
                foreach(string item in leavesText.Split(','))
                {
                    int selectedScene;
                    Require(int.TryParse(item,NumberStyles.Integer,CultureInfo.InvariantCulture,out selectedScene) && selectedScene>=0 && selectedScene<13 && !sceneIds.Contains(selectedScene),"JUEMINGR_WALL_HEALTH_SCENES must list unique existing scene0..12 identifiers.");
                    sceneIds.Add(selectedScene);
                }
            }
            else if(leaf>=0)sceneIds.Add(leaf);
            else for(int i=0;i<13;i++)sceneIds.Add(i);
            string actualLeaf=!string.IsNullOrEmpty(leavesText)?"scenes["+string.Join(",",sceneIds)+"]":leaf<0?"all13":"scene"+leaf;
            // This process-only selector narrows execution, never its original
            // inputs, duration, health oracle or per-scene coverage criteria.
            Console.WriteLine("WALL actualLeaf="+actualLeaf+" environment=JUEMINGR_WALL_HEALTH_SCENE="+(leafText??"<unset>")+" JUEMINGR_WALL_HEALTH_SCENES="+(leavesText??"<unset>"));
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");long session=(long)Get(host,"Session");var read=source.GetType().GetMethod("Read",Flags);var readPlayer=source.GetType().GetMethod("ReadPlayer",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var update=typeof(NPC).GetMethod("UpdateNPC",Flags);var playerUpdate=typeof(Player).GetMethod("Update",Flags);var healthUpdate=typeof(Player).GetMethod("UpdateLifeRegen",Flags);var healthStep=Model.GetMethod("AdvanceHealth",Flags);var send=typeof(NetMessage).GetMethod("SendData",Flags);var hooks=new Harmony("JueMingR.Probe.WallNaturalSlots");
            var hurt=typeof(Player).GetMethod("Hurt",Flags,null,new[]{typeof(Terraria.DataStructures.PlayerDeathReason),typeof(int),typeof(int),typeof(bool),typeof(bool),typeof(bool),typeof(int),typeof(bool)},null);
            var world=Get(host,"World");var prepareWorld=world.GetType().GetMethod("Prepare",Flags);
            hooks.Patch(prepareWorld,prefix:new HarmonyMethod(typeof(NativeCombatWallBossChecks).GetMethod(nameof(WorldBefore),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatWallBossChecks).GetMethod(nameof(WorldAfter),Flags)));
            hooks.Patch(update,prefix:new HarmonyMethod(typeof(NativeCombatWallBossChecks).GetMethod(nameof(Before),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatWallBossChecks).GetMethod(nameof(After),Flags)));
            hooks.Patch(playerUpdate,prefix:new HarmonyMethod(typeof(NativeCombatWallBossChecks).GetMethod(nameof(PlayerBefore),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatWallBossChecks).GetMethod(nameof(PlayerAfter),Flags)));
            hooks.Patch(healthUpdate,prefix:new HarmonyMethod(typeof(NativeCombatWallBossChecks).GetMethod(nameof(HealthBefore),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatWallBossChecks).GetMethod(nameof(HealthAfter),Flags)));
            hooks.Patch(hurt,prefix:new HarmonyMethod(typeof(NativeCombatWallBossChecks).GetMethod(nameof(BeforeHurt),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatWallBossChecks).GetMethod(nameof(AfterHurt),Flags)));
            hooks.Patch(send,prefix:new HarmonyMethod(typeof(NativeCombatWallBossChecks).GetMethod(nameof(IsolateNetwork),Flags)){priority=Priority.First+100});
            var records=new List<string>{"scene,frame,type,target,global,top,bottom,selected,published,count,stop,matched,long,cross,body,eyes,hungry,pc,tongue,gross,pending37,pending38,error,fallReceived,fallRestored,fallExpiredWindows,fallFreshPublished,fallRecoveryLong"};var failures=new List<string>();bool oldGood=Main.getGoodWorld;int totalMatched=0,totalBody=0,totalEyes=0,totalHungry=0,totalPc=0,totalTongue=0;
            try
            {
                foreach(int scene in sceneIds)
                {
                    try
                    {
                        Setup(context,scene);var body=Birth(scene==11?5600:1600,16);NPC actor=body;int frameNow=0,bodyActions=0,eyes=0,hungry=0,pcActions=0,tongue=0,gross=0,birth=0,retired=0,matched=0,longMatches=0,cross=0,published=0,selected=0,pause=0,receiving=0,snap=0,restored=0;double maxError=0;var targets=new HashSet<int>();
                        var frozen=new List<NpcTrajectory>();var phases=new Dictionary<NpcTrajectory,float>();var freezesHealth=new Dictionary<NpcTrajectory,int>();NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                        NPC pending=null;NpcMotionState predicted=default(NpcMotionState),initial=default(NpcMotionState),initialOwner=default(NpcMotionState);PredictionEnvironment initialEnvironment=default(PredictionEnvironment);string initialPlayer="not captured";bool valid=false;PredictionStop stop=PredictionStop.None;
                        before=n=>
                        {
                            pending=n;valid=false;if(!n.active || !Family(n))return;var rows=Capture(read,tracking,terrain,session,n);int index=Index(rows,n);Require(index>=0,"Current real Wall slot has captured complete raw-global roles.");predicted=initial=rows[index];initialOwner=default(NpcMotionState);for(int i=0;i<rows.Length;i++)if(rows[i].Identity.Equals(initial.WallBossIdentity)){initialOwner=rows[i];break;}initialEnvironment=EnvironmentFor(n);
                            var direct=Main.player[n.target>=0 && n.target<Main.player.Length?n.target:255];initialPlayer="slot="+direct.whoAmI+" position="+Precise(direct.position.X)+","+Precise(direct.position.Y)+" center="+Precise(direct.Center.X)+","+Precise(direct.Center.Y)+" V="+Precise(direct.velocity.X)+","+Precise(direct.velocity.Y)+" size="+direct.width+","+direct.height;
                            terrain.Reset();valid=NpcMotion.Step(ref predicted,rows,rows.Length,initialEnvironment,terrain,1,true,out stop);
                            if(!valid)Require(stop==PredictionStop.Despawn || stop==PredictionStop.MissingDependency,"Real Wall actor ends only at its exact dependencies/lifecycle: "+stop+" type="+n.type+" target="+n.target+" global="+Main.wofNPCIndex);
                        };
                        after=n=>
                        {
                            if(!ReferenceEquals(n,pending) || !Family(n))return;if(!valid){if(stop==PredictionStop.Despawn && !n.active)retired++;return;}
                            double error=Vector2.Distance(new Vector2(predicted.X,predicted.Y),n.position);
                            if(error>.08 || Math.Abs(predicted.Vx-n.velocity.X)>.0001 || Math.Abs(predicted.Vy-n.velocity.Y)>.0001)
                                Console.WriteLine("WALL FIRST-ACTION-INPUT scene="+scene+" frame="+frameNow+" actor="+ActorFacts(initial)+" owner="+ActorFacts(initialOwner)+" rawPlayer="+initialPlayer+" observedPlayerArea="+Precise(initial.PlayerArea.X)+","+Precise(initial.PlayerArea.Y)+","+Precise(initial.PlayerArea.Width)+","+Precise(initial.PlayerArea.Height)+" capturedCenter="+Precise(initial.PlayerCenterX)+","+Precise(initial.PlayerCenterY)+" closest="+initial.ClosestPlayerIndex+"/"+Precise(initial.ClosestPlayerCenterX)+","+Precise(initial.ClosestPlayerCenterY)+" envPC="+initialEnvironment.PlayerIndex+"/"+Precise(initialEnvironment.PlayerX)+","+Precise(initialEnvironment.PlayerY)+" size="+Precise(initialEnvironment.PlayerWidth)+","+Precise(initialEnvironment.PlayerHeight)+" expert="+initialEnvironment.Expert+" good="+initialEnvironment.GoodWorld+" global="+Main.wofNPCIndex+" band="+initial.WallTop+","+initial.WallBottom+" predicted="+ActorFacts(predicted)+" native="+Precise(n.position.X)+","+Precise(n.position.Y)+" V="+Precise(n.velocity.X)+","+Precise(n.velocity.Y));
                            Require(error<=.08 && Math.Abs(predicted.Vx-n.velocity.X)<=.0001 && Math.Abs(predicted.Vy-n.velocity.Y)<=.0001 && predicted.A1==n.ai[1] && predicted.A2==n.ai[2] && predicted.Direction==n.direction && predicted.SpriteDirection==n.spriteDirection && predicted.CanReceive==(!n.dontTakeDamage && !n.immortal),"Original Wall slot scene="+scene+" frame="+frameNow+" type="+n.type+" slot="+n.whoAmI+" error="+error+" predictedV="+predicted.Vx+","+predicted.Vy+" actualV="+n.velocity+" clocks="+predicted.A1+"/"+predicted.A2+" native="+string.Join("/",n.ai)+" target="+n.target+" global="+Main.wofNPCIndex+" band="+Main.wofDrawAreaTop+"/"+Main.wofDrawAreaBottom);
                            if(n.type==113){bodyActions++;Require(predicted.WallTop==Main.wofDrawAreaTop && predicted.WallBottom==Main.wofDrawAreaBottom,"Original body writes exact global scan/easing band.");}else if(n.type==114)eyes++;else{hungry++;if(n.ai[1]>0)pause++;}targets.Add(n.target);
                        };
                        PredictionPlayerMotion pc=default(PredictionPlayerMotion),pcInitial=default(PredictionPlayerMotion),healthInitial=default(PredictionPlayerMotion);bool pcValid=false;PredictionStop pcStop=PredictionStop.None;bool beforeTongue=false;int beforeBuff=0;string nativeHealthBefore="not observed",nativeHealthAfter="not observed";
                        bool enteredHealth=false;string playerPrefixFacts="not observed",actualHurt="none";
                        bool fallReceivedThisAction=false,waitingFallRestore=false;int fallReceived=0,fallRestored=0,fallExpiredWindows=0;
                        bool waitingFallPublication=false;long lastFallTick=-1;int fallFreshPublished=0,fallRecoveryLong=0;
                        var contactChanged=new HashSet<NpcTrajectory>();var contactReported=new HashSet<NpcTrajectory>();var contactInvalidFrom=new Dictionary<NpcTrajectory,long>();
                        bool contactThisAction=false,contactPublicationPending=false,waitingContactRestore=false,waitingContactPublication=false,playerCompleted=false;
                        int projectileReceived=0,projectilePlayerNegative=0;
                        int contactReceived=0,contactPlayerNegative=0,contactInvalidated=0,contactPublication=0,contactRestored=0,contactFreshPublished=0,contactRecoveryLong=0,contactFrozenNegative=0;
                        long lastContactTick=-1;double contactNegativeMax=0;NpcTrajectory publicationBeforeContact=null;
                        PredictionPlayerMotion healthCompleted=default(PredictionPlayerMotion);
                        worldPrepare=completed=>
                        {
                            if(!completed || !contactPublicationPending)return;
                            var current=cache.Read(0);var prepared=GetOptional(world,"preparedPath") as NpcTrajectory;
                            Require(current==null || current.CaptureTick==Main.GameUpdateCount && !ReferenceEquals(current,publicationBeforeContact),"Actual receiving retires the pre-hit production publication before completed World.Prepare.");
                            Require(prepared==null || ReferenceEquals(prepared,current),"Actual presentation consumes only the current post-hit Wall publication.");
                            if((scene==0 || scene==5 || scene==6) && current!=null)
                            {
                                var actual=Main.LocalPlayer;var input=(PredictionPlayerMotion)Get(Get(source,"rolling"),"previousPlayer");
                                Require(ReferenceEquals(input.PlayerToken,actual) && input.PlayerIndex==actual.whoAmI && input.X==actual.position.X && input.Y==actual.position.Y && input.Vx==actual.velocity.X && input.Vy==actual.velocity.Y && input.WallLife==actual.statLife && input.WallRegenCount==actual.lifeRegenCount && input.WallRegenTime==actual.lifeRegenTime && input.WallImmune==actual.immune && input.WallImmuneTime==actual.immuneTime,"Post-contact production input uses the real current pose, health and immunity instead of the preceding player lease.");
                            }
                            Console.WriteLine("WALL CONTACT-PUBLICATION scene="+scene+" frame="+frameNow+" tick="+Main.GameUpdateCount+" oldCapture="+(publicationBeforeContact?.CaptureTick??-1)+" currentCapture="+(current?.CaptureTick??-1)+" preparedCurrent="+ReferenceEquals(prepared,current));
                            contactPublication++;contactPublicationPending=false;
                        };
                        // Only observe the original receiving action. In particular,
                        // landing can clear immune before Hurt; do not suppress it
                        // or replay damage to force the health premise to survive.
                        receivedHit=(p,result,reason,damage,direction,sample)=>
                        {
                            if(!ReferenceEquals(p,Main.LocalPlayer))return;Entity cause;reason.TryGetCausingEntity(out cause);
                            actualHurt="reasonOther="+reason.SourceOtherIndex+" reasonProjectile="+reason.SourceProjectileType+" cause="+(cause==null?"none":cause.GetType().Name+":"+cause.whoAmI)+" requested="+damage+" result="+result+" direction="+direction+" beforeRegen="+!enteredHealth+" before="+sample.Facts+" after="+HealthFacts(p);
                            Console.WriteLine("WALL ORIGINAL-HURT scene="+scene+" frame="+frameNow+" playerPrefix="+playerPrefixFacts+" "+actualHurt+" caller="+System.Environment.StackTrace);
                            if(cause is NPC || cause is Projectile)
                            {
                                if(cause is NPC contact)
                                {
                                    Require(Family(contact),"New NPC receiving in this Wall-only scene must retain its actual Wall actor source, not hide an unrelated injury.");
                                    var actual=(NpcMotionState)read.Invoke(null,new object[]{contact,session});var identity=actual.Identity;
                                    Console.WriteLine("WALL CONTACT-IDENTITY scene="+scene+" frame="+frameNow+" slot="+identity.Slot+" type="+identity.Type+" netId="+identity.NetId+" generation="+identity.Generation+" session="+identity.Session+" active="+contact.active+" position="+contact.position+" V="+contact.velocity+" pcBefore="+sample.Facts+" pcAfter="+HealthFacts(p));
                                }
                                else
                                {
                                    var shot=(Projectile)cause;
                                    Require(shot.type==83 && shot.hostile && !shot.friendly && playerCompleted,"Actual Wall eye projectile83 receiving follows the completed player/NPC actions; retain unrelated injuries as failures.");
                                    Console.WriteLine("WALL SHOT-IDENTITY scene="+scene+" frame="+frameNow+" slot="+shot.whoAmI+" key="+shot.key+" type="+shot.type+" owner="+shot.owner+" active="+shot.active+" position="+shot.position+" V="+shot.velocity+" pcBefore="+sample.Facts+" pcAfter="+HealthFacts(p));
                                    projectileReceived++;
                                    if(pcValid)
                                    {
                                        double changedError=Vector2.Distance(new Vector2(pc.X,pc.Y),p.position);contactNegativeMax=Math.Max(contactNegativeMax,changedError);
                                        if(changedError>.08 || Math.Abs(pc.Vx-p.velocity.X)>.0001 || Math.Abs(pc.Vy-p.velocity.Y)>.0001){contactPlayerNegative++;projectilePlayerNegative++;}
                                        Console.WriteLine("WALL SHOT-PLAYER-NEGATIVE scene="+scene+" frame="+frameNow+" error="+changedError+" predictedV="+pc.Vx+","+pc.Vy+" actualV="+p.velocity);
                                    }
                                }
                                Require(enteredHealth,"Observed Wall receiving consumes the real post-regen player action.");
                                long invalidFrom=(long)Main.GameUpdateCount+(playerCompleted?1:0);
                                foreach(var old in frozen)
                                {
                                    Require((old.Assumptions&PredictionAssumption.NoNewHits)!=0,"Retained pre-contact paths explicitly carry the no-new-hits premise.");
                                    if(contactChanged.Add(old))contactInvalidated++;
                                    long prior;if(!contactInvalidFrom.TryGetValue(old,out prior) || invalidFrom<prior)contactInvalidFrom[old]=invalidFrom;
                                }
                                publicationBeforeContact=cache.Read(0);contactThisAction=!playerCompleted;contactPublicationPending=true;
                                waitingContactRestore=waitingContactPublication=!p.dead;lastContactTick=(long)Main.GameUpdateCount;contactReceived++;
                            }
                            if(reason.SourceOtherIndex==0)
                            {
                                Require(!enteredHealth && !pcValid && pcStop==PredictionStop.PhaseBoundary,"Actual ordinary fall receiving has already ended the no-new-hits continuation before regen.");
                                foreach(var old in frozen)
                                {if(contactChanged.Contains(old))continue;int offset=(int)(Main.GameUpdateCount-old.CaptureTick);Require(offset>=old.Count,"A retained no-new-hits frozen Wall window cannot include the actual fall-hit NPC action. capture="+old.CaptureTick+" offset="+offset+" count="+old.Count+" stop="+old.Stop);fallExpiredWindows++;}
                                // Receiving is real, not injected. Prior windows
                                // have ended; fresh post-Hurt facts own recovery.
                                frozen.Clear();phases.Clear();freezesHealth.Clear();contactChanged.Clear();contactReported.Clear();contactInvalidFrom.Clear();fallReceivedThisAction=true;waitingFallRestore=!p.dead;waitingFallPublication=!p.dead;lastFallTick=(long)Main.GameUpdateCount;fallReceived++;
                            }
                        };
                        // Observe the real regen consumer's inputs separately
                        // from Player.Update's prefix. Never replay native AI,
                        // damage or buffs to manufacture a matching state.
                        healthBefore=p=>
                        {
                            if(!ReferenceEquals(p,Main.LocalPlayer))return;
                            enteredHealth=true;
                            nativeHealthBefore=HealthFacts(p);
                            healthInitial=new PredictionPlayerMotion{WallHealthCaptured=true,WallHealthKnown=pcInitial.WallHealthKnown,WallLocal=p.whoAmI==Main.myPlayer,WallTongued=p.tongued,WallLife=p.statLife,WallLifeMax=p.statLifeMax2,WallRegenCount=p.lifeRegenCount,WallRegenTime=p.lifeRegenTime,Vx=p.velocity.X,Grappled=p.grappling[0]>=0,BordersGodMode=p.creativeGodMode};
                        };
                        healthAfter=p=>{if(ReferenceEquals(p,Main.LocalPlayer)){nativeHealthAfter=HealthFacts(p);healthCompleted=new PredictionPlayerMotion{WallLife=p.statLife,WallRegenCount=p.lifeRegenCount,WallRegenTime=p.lifeRegenTime};}};
                        playerBefore=p=>
                        {
                            if(ReferenceEquals(p,Main.LocalPlayer)){enteredHealth=playerCompleted=false;fallReceivedThisAction=contactThisAction=false;playerPrefixFacts=HealthFacts(p);actualHurt="none";}
                            pcValid=false;pcStop=PredictionStop.None;if(frameNow==0 || !ReferenceEquals(p,Main.LocalPlayer) || !p.active || p.dead)return;pc=pcInitial=(PredictionPlayerMotion)readPlayer.Invoke(null,new object[]{p});nativeHealthBefore=nativeHealthAfter="not observed";beforeTongue=p.tongued;beforeBuff=pc.WallTonguedTicks;terrain.Reset();pcValid=PlayerMotionContinuation.Advance(ref pc,EnvironmentFor(body),terrain,out pcStop);
                            if(!pcValid)Require(pcStop==PredictionStop.Despawn || pcStop==PredictionStop.MissingDependency || pcStop==PredictionStop.PhaseBoundary,"Exact actual Wall PC consumer boundary "+pcStop);
                        };
                        playerAfter=p=>
                        {
                            if(!ReferenceEquals(p,Main.LocalPlayer) || !p.active)return;playerCompleted=true;
                            if(contactThisAction)
                            {
                                // The genuine Hurt changes the conditional input,
                                // not the regen formula. Keep its actual mismatch
                                // as a negative case; never replay or suppress it.
                                if(pcValid)
                                {
                                    Require(enteredHealth && (!pc.WallHealthKnown || pc.WallLife==healthCompleted.WallLife && pc.WallRegenCount==healthCompleted.WallRegenCount && pc.WallRegenTime==healthCompleted.WallRegenTime),"The actual pre-contact regen consumer still agrees before the new receiving premise.");
                                    double changedError=Vector2.Distance(new Vector2(pc.X,pc.Y),p.position);contactNegativeMax=Math.Max(contactNegativeMax,changedError);
                                    if(changedError>.08 || Math.Abs(pc.Vx-p.velocity.X)>.0001 || Math.Abs(pc.Vy-p.velocity.Y)>.0001)contactPlayerNegative++;
                                    Console.WriteLine("WALL CONTACT-PLAYER-NEGATIVE scene="+scene+" frame="+frameNow+" error="+changedError+" predicted="+pc.X+","+pc.Y+" V="+pc.Vx+","+pc.Vy+" actual="+p.position+" V="+p.velocity+" "+actualHurt);
                                }
                                return;
                            }
                            if(!pcValid)
                            {
                                if(pcStop==PredictionStop.Despawn)Require(p.dead,"Pure existing-Tongue health/edge death agrees with real KillMe.");
                                if(fallReceivedThisAction && !p.dead)
                                {object[] receivingSlice={healthInitial,EnvironmentFor(body),PredictionStop.None};bool receivingValid=(bool)healthStep.Invoke(null,receivingSlice);var received=(PredictionPlayerMotion)receivingSlice[0];Require(enteredHealth && receivingValid && received.WallLife==p.statLife && received.WallRegenCount==p.lifeRegenCount && received.WallRegenTime==p.lifeRegenTime,"Actual post-fall regen consumer still matches its true new-health inputs without replaying Hurt.");}
                                return;
                            }
                            double error=Vector2.Distance(new Vector2(pc.X,pc.Y),p.position);
                            Require(error<=.08 && Math.Abs(pc.Vx-p.velocity.X)<=.0001 && Math.Abs(pc.Vy-p.velocity.Y)<=.0001 && pc.WallGross==p.gross && pc.WallTongued==p.tongued,"Original Wall player action scene="+scene+" frame="+frameNow+" error="+error+" predicted="+pc.X+","+pc.Y+" V="+pc.Vx+","+pc.Vy+" actual="+p.position+" V="+p.velocity+" gross/tongue="+p.gross+"/"+p.tongued+" buffs="+Buff(p,37)+"/"+Buff(p,38)+" health="+p.statLife+"/"+p.lifeRegenCount);
                            if(pc.WallHealthKnown && (pc.WallLife!=p.statLife || pc.WallRegenCount!=p.lifeRegenCount || pc.WallRegenTime!=p.lifeRegenTime))
                            {
                                object[] slice={healthInitial,EnvironmentFor(body),PredictionStop.None};bool sliceValid=(bool)healthStep.Invoke(null,slice);var hp=(PredictionPlayerMotion)slice[0];
                                Require(false,"Actual canonical Wall health continuation matches original UpdateLifeRegen. scene="+scene+" frame="+frameNow+" captured="+pcInitial.WallLife+"/"+pcInitial.WallLifeMax+"/"+pcInitial.WallRegenCount+"/"+pcInitial.WallRegenTime+" Vx="+pcInitial.Vx+" tongue="+pcInitial.WallTongued+" pending="+pcInitial.WallTonguedTicks+" predicted="+pc.WallLife+"/"+pc.WallRegenCount+"/"+pc.WallRegenTime+" actual="+HealthFacts(p)+" playerPrefix="+playerPrefixFacts+" actualHurt="+actualHurt+" nativeConsumerBefore="+nativeHealthBefore+" nativeConsumerAfter="+nativeHealthAfter+" pureConsumer="+sliceValid+"/"+slice[2]+"/"+hp.WallLife+"/"+hp.WallRegenCount+"/"+hp.WallRegenTime);
                            }
                            Require(pc.WallHorrifiedTicks==Buff(p,37) && pc.WallTonguedTicks==Buff(p,38),"Pending native AddBuff/delete slot timing is separate from completed current fields.");pcActions++;if(p.tongued)tongue++;if(p.gross)gross++;if(beforeBuff>0 && p.tongued && Buff(p,38)==0)snap++;if(beforeTongue && !p.tongued)restored++;
                            if(waitingFallRestore){Require(pc.WallHealthKnown,"Post-fall genuine sample restores the canonical health premise.");fallRestored++;waitingFallRestore=false;}
                            if(waitingContactRestore){Require(pc.WallHealthKnown,"Actual post-contact sample restores canonical Wall health and motion.");contactRestored++;waitingContactRestore=false;}
                        };
                        for(int frame=0;frame<1800 && body.active;frame++)
                        {
                            frameNow=frame;bool changed=false;
                            if(frame==2){Require(Count(114)==2 && Count(115)>=11,"Natural body allocation is followed by observed real part identities.");birth++;if(scene==3)actor=Part(114,true);if(scene==4)actor=Part(114,false);if(scene==5 || scene==6)actor=Part(115,true);}
                            if(scene==1 && frame%200==0 && frame>0 && frame<=1600){float[] fractions={.74f,.65f,.49f,.32f,.24f,.09f,.034f,.024f};HitTo(body,fractions[frame/200-1]);changed=true;}
                            if(scene==6 && frame==400){actor.StrikeNPCNoInteraction(10+actor.defense,0,0);Require(actor.justHit,"Real Hungry receiving sets native JustHit pause.");changed=true;}
                            if(scene==7 && frame==400){Birth(2300,24);changed=true;}
                            if((scene==8 || scene==9) && frame==350){Main.LocalPlayer.Teleport(new Vector2(body.position.X-180,scene==9?3100:body.Center.Y));Main.LocalPlayer.ResetFallDamage();if(scene==9)Main.LocalPlayer.statLife=10;changed=true;}
                            if(scene==10 && frame==500){Main.LocalPlayer.KillMe(Terraria.DataStructures.PlayerDeathReason.ByCustomReason("isolated Wall retreat"),1000000,0);Require(Main.LocalPlayer.dead,"Actual player death enters body retreat clock.");changed=true;}
                            if(scene==12 && frame==500){Main.player[1].Teleport(new Vector2(body.position.X+600,4400-Main.player[1].height));Main.player[1].ResetFallDamage();changed=true;}
                            if(changed){frozen.Clear();phases.Clear();freezesHealth.Clear();}
                            if(actor==null || !actor.active)actor=body;NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();if(!body.active)break;if(!body.dontTakeDamage)receiving++;
                            bool chosen=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,actor);if(chosen)selected++;var path=cache.Read(0);
                            if(path!=null && ReferenceEquals(path.Identity.Token,actor) && !frozen.Contains(path)){published++;if(frame%5==0 && path.Count>1){frozen.Add(path);phases[path]=actor.type==114?actor.localAI[2]:actor.ai[2];freezesHealth[path]=body.life;}}
                            if(waitingFallPublication && path!=null && ReferenceEquals(path.Identity.Token,actor) && path.CaptureTick>=lastFallTick && path.Count>1){fallFreshPublished++;waitingFallPublication=false;}
                            if(waitingContactPublication && path!=null && ReferenceEquals(path.Identity.Token,actor) && path.CaptureTick>=lastContactTick && path.Count>1){contactFreshPublished++;waitingContactPublication=false;}
                            for(int i=frozen.Count-1;i>=0;i--)
                            {
                                var old=frozen[i];int offset=(int)(Main.GameUpdateCount-old.CaptureTick);if(offset>=old.Count || !ReferenceEquals(old.Identity.Token,actor)){frozen.RemoveAt(i);phases.Remove(old);freezesHealth.Remove(old);contactChanged.Remove(old);contactReported.Remove(old);contactInvalidFrom.Remove(old);continue;}if(offset<1)continue;
                                var point=old[offset];double error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),actor.position);
                                // A projectile hits after this frame's NPC actions.
                                // Their completed prefix remains exact; only later
                                // points consume the changed player facts.
                                if(contactChanged.Contains(old) && (long)Main.GameUpdateCount>=contactInvalidFrom[old])
                                {
                                    if(error>.08 && contactReported.Add(old)){contactFrozenNegative++;Console.WriteLine("WALL CONTACT-FROZEN-NEGATIVE scene="+scene+" frame="+frame+" capture="+old.CaptureTick+" offset="+offset+" error="+error+" contactTick="+lastContactTick);}
                                    continue;
                                }
                                maxError=Math.Max(maxError,error);matched++;
                                Require(error<=.08 && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal),"Frozen Wall scene="+scene+" frame="+frame+" type="+actor.type+" offset="+offset+" error="+error+" predicted="+point.Bounds.X+","+point.Bounds.Y+" actual="+actor.position+" target="+actor.target+" global="+Main.wofNPCIndex+" stop="+old.Stop);
                                if(offset>=30){longMatches++;if(lastFallTick>=0 && old.CaptureTick>=lastFallTick)fallRecoveryLong++;if(lastContactTick>=0 && old.CaptureTick>=lastContactTick)contactRecoveryLong++;}if(phases[old]!=(actor.type==114?actor.localAI[2]:actor.ai[2]))cross++;
                            }
                            records.Add(string.Join(",",scene,frame,actor.type,actor.target,Main.wofNPCIndex,Main.wofDrawAreaTop,Main.wofDrawAreaBottom,chosen,path!=null,path?.Count??0,path?.Stop.ToString()??"none",matched,longMatches,cross,bodyActions,eyes,hungry,pcActions,tongue,gross,Buff(Main.LocalPlayer,37),Buff(Main.LocalPlayer,38),maxError.ToString("R",CultureInfo.InvariantCulture),fallReceived,fallRestored,fallExpiredWindows,fallFreshPublished,fallRecoveryLong));
                        }
                        before=after=null;playerBefore=playerAfter=healthBefore=healthAfter=null;receivedHit=null;worldPrepare=null;
                        Require(bodyActions>=100 && eyes>=100 && hungry>=100 && birth>0,"Natural complete Wall family retains observed body/eyes/Hungry actions.");
                        if(scene!=9 && scene!=10 && scene!=11 && scene!=12)Require(selected>0 && published>0 && longMatches>=30,"Known Wall windows remain published for genuine long futures scene="+scene);
                        if(scene==3 || scene==4 || scene==5 || scene==6)Require(cross>=30,"Real eye firing/Hungry radius clocks cross inside long windows.");if(scene==6)Require(pause>0,"Actual receiving Hungry pauses before restored pursuit.");
                        if(scene==8)Require(tongue>0 && snap>0 && restored>0,"Legal behind-body teleport drives native pending Tongue, snap-delete, then ordinary movement recovery.");
                        if(scene==9)Require(tongue>0 && Main.LocalPlayer.dead,"Real Expert existing-Tongue DOT/edge lifecycle is not a frozen health assumption.");if(scene==10)Require(!body.active && retired>0,"Original dead player retreats through localAI1 into real body retirement.");
                        if(scene==11)Require(Main.LocalPlayer.dead && retired>0,"Native right-edge Tongue KillMe feeds the real body retreat/retirement chain.");
                        if(scene==7)Require(Count(113)>=2,"Natural second body retains all global writers in the slot closure.");if(scene==12)Require(targets.Count>1,"Actual two-player gross eligibility changes numbered targets.");
                        if(scene==0)Require(fallReceived>0 && fallRestored>0 && fallFreshPublished>0 && fallRecoveryLong>=30,"Unmodified natural Tongue fall keeps its real Hurt boundary, fresh publication and long health/motion recovery.");
                        if(scene==0)Require(contactReceived>0 && contactPlayerNegative>0 && contactInvalidated>0 && contactPublication>0 && !contactPublicationPending && contactRestored>0 && contactFreshPublished>0 && contactRecoveryLong>=30,"Actual Wall NPC contact keeps its conditional negative, production withdrawal, genuine sample and long recovery.");
                        if(scene==5 || scene==6)Require(projectileReceived>0 && projectilePlayerNegative>0 && contactFrozenNegative>0 && contactInvalidated>0 && contactPublication>0 && !contactPublicationPending && contactRestored>0 && contactFreshPublished>0 && contactRecoveryLong>=30,"Actual original Wall eye projectile injury keeps the completed prefix, old frozen negative, production withdrawal and fresh long recovery.");
                        Console.WriteLine("WALL SHOT-RECOVERY scene="+scene+" received="+projectileReceived+" playerNegative="+projectilePlayerNegative);
                        Console.WriteLine("WALL CONTACT-RECOVERY scene="+scene+" received="+contactReceived+" playerNegative="+contactPlayerNegative+" negativeMax="+contactNegativeMax+" invalidated="+contactInvalidated+" publication="+contactPublication+" restored="+contactRestored+" freshPublished="+contactFreshPublished+" recoveryLong="+contactRecoveryLong+" frozenNegative="+contactFrozenNegative);
                        totalMatched+=matched;totalBody+=bodyActions;totalEyes+=eyes;totalHungry+=hungry;totalPc+=pcActions;totalTongue+=tongue;Console.WriteLine("WALL scene="+scene+" body="+bodyActions+" eyes="+eyes+" hungry="+hungry+" pc="+pcActions+" tongue="+tongue+" gross="+gross+" snap="+snap+" restored="+restored+" matched="+matched+" long="+longMatches+" cross="+cross+" selected="+selected+" published="+published+" retired="+retired+" fallReceived="+fallReceived+" fallRestored="+fallRestored+" fallExpiredWindows="+fallExpiredWindows+" fallFreshPublished="+fallFreshPublished+" fallRecoveryLong="+fallRecoveryLong+" error="+maxError);
                    }
                    catch(Exception failure) when(!(failure is OutOfMemoryException)){before=after=null;playerBefore=playerAfter=healthBefore=healthAfter=null;receivedHit=null;worldPrepare=null;failures.Add(scene+": "+failure.Message);Console.WriteLine("WALL FAIL scene="+scene+": "+failure);}
                }
                Require(failures.Count==0,"Wall original natural expectations remain required for actualLeaf="+actualLeaf+": "+string.Join(" | ",failures));Console.WriteLine("PASS Wall natural actualLeaf="+actualLeaf+" scenes="+sceneIds.Count+" matched="+totalMatched+" body="+totalBody+" eyes="+totalEyes+" hungry="+totalHungry+" player="+totalPc+" tongue="+totalTongue);
            }
            finally{before=after=death=null;playerBefore=playerAfter=healthBefore=healthAfter=null;receivedHit=null;worldPrepare=null;Main.getGoodWorld=oldGood;hooks.Unpatch(update,HarmonyPatchType.All,hooks.Id);hooks.Unpatch(playerUpdate,HarmonyPatchType.All,hooks.Id);hooks.Unpatch(healthUpdate,HarmonyPatchType.All,hooks.Id);hooks.Unpatch(hurt,HarmonyPatchType.All,hooks.Id);hooks.Unpatch(prepareWorld,HarmonyPatchType.All,hooks.Id);hooks.Unpatch(send,HarmonyPatchType.Prefix,hooks.Id);Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"wall-boss-continuous.csv"),records);}
        }
        private static string HealthFacts(Player p)
        {return p.statLife+"/"+p.statLifeMax2+"/"+p.lifeRegenCount+"/"+p.lifeRegenTime+" regen="+p.lifeRegen+" position="+p.position+" V="+p.velocity+" fall="+p.fallStart+","+p.fallStart2+" grav="+p.gravDir+" extraFall="+p.extraFall+" noFall="+p.noFallDmg+" wings="+p.hasWings+" immune="+p.immune+","+p.immuneTime+" tongue="+p.tongued+" pending="+Buff(p,38)+" grapple="+p.grappling[0]+" god="+p.creativeGodMode+" wellFed="+p.wellFed+" honey="+p.honey+" suffocating="+p.suffocating+" bleed="+p.bleed+" burning="+p.onFire+"/"+p.burned+" regenModifiers="+p.crimsonRegen+"/"+p.shinyStone+"/"+p.nebulaLevelLife+" scene="+Player.SceneMetrics.HasCampfire+"/"+Player.SceneMetrics.HasHeartLantern;}
        private static string Precise(float value){return value.ToString("R",CultureInfo.InvariantCulture);}
        private static string ActorFacts(NpcMotionState n)
        {return "slot="+n.Identity.Slot+" type="+n.EffectiveType+" position="+Precise(n.X)+","+Precise(n.Y)+" V="+Precise(n.Vx)+","+Precise(n.Vy)+" size="+n.Width+","+n.Height+" life="+n.Life+"/"+n.LifeMax+" ai="+Precise(n.A0)+","+Precise(n.A1)+","+Precise(n.A2)+","+Precise(n.A3)+" local="+Precise(n.L0)+","+Precise(n.L1)+","+Precise(n.L2)+","+Precise(n.L3)+" face="+n.Direction+","+n.DirectionY+","+n.SpriteDirection+" target="+n.Target+" player="+n.PlayerIndex+" justHit="+n.JustHit+" collide="+n.CollideX+","+n.CollideY+" parameter="+Precise(n.PositionParameter);}
        private static NpcMotionState Basic(NPC n,int type,int style)
        {var p=Main.player[n.target>=0 && n.target<255?n.target:0];return new NpcMotionState{Identity=new NpcIdentity(1,n,n.whoAmI,1,type,type),Style=style,Active=true,X=n.position.X,Y=n.position.Y,Vx=n.velocity.X,Vy=n.velocity.Y,Width=n.width,Height=n.height,Life=n.life,LifeMax=Math.Max(1,n.lifeMax),Target=n.target,Direction=n.direction,DirectionY=n.directionY,SpriteDirection=n.spriteDirection,Boss=n.boss,TargetCaptured=true,HasPlayer=true,PlayerIndex=p.whoAmI,PlayerActive=p.active,PlayerNativeDead=p.dead,PlayerGross=p.gross,PlayerArea=new MotionRect((int)p.position.X,(int)p.position.Y,p.width,p.height),PlayerCenterX=p.Center.X,PlayerCenterY=p.Center.Y,PlayerVelocityCaptured=true,WallRelationsCaptured=true,WallRelationsComplete=true,WallBossCaptured=true};}
        private static NpcMotionState[] Capture(MethodInfo read,MethodInfo tracking,IPredictionTerrain terrain,long session,NPC selected)
        {
            var rows=new List<NpcMotionState>();NpcIdentity global=default(NpcIdentity);bool valid=Main.wofNPCIndex>=0 && Main.wofNPCIndex<Main.maxNPCs && Main.npc[Main.wofNPCIndex]!=null;
            for(int i=0;i<Main.maxNPCs;i++)
            {var n=Main.npc[i];if(n==null || !(n.active && n.type==113 || ReferenceEquals(n,selected) || valid && i==Main.wofNPCIndex))continue;var s=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,s,terrain};tracking.Invoke(null,args);s=(NpcMotionState)args[1];if(valid && i==Main.wofNPCIndex)global=s.Identity;rows.Add(s);}
            for(int i=0;i<rows.Count;i++){var s=rows[i];s.WallRelationsCaptured=s.WallRelationsComplete=s.WallBossCaptured=true;s.WallBossValid=valid;s.WallBossIdentity=global;s.WallTop=Main.wofDrawAreaTop;s.WallBottom=Main.wofDrawAreaBottom;rows[i]=s;}return rows.ToArray();
        }
        private static PredictionEnvironment EnvironmentFor(NPC n)
        {
            var pc=Main.LocalPlayer;var target=Main.player[n.target>=0 && n.target<Main.player.Length?n.target:255];var peers=new List<PredictionWallPlayer>();var rects=new List<MotionRect>();var exact=new List<MotionRect>();var slots=new List<int>();var alive=new List<bool>();var dead=new List<bool>();
            for(int i=0;i<255;i++)
            {var p=Main.player[i];if(!p.active && i!=0)continue;var pet=p.tankPet>=0 && p.tankPet<Main.maxProjectiles?Main.projectile[p.tankPet]:null;peers.Add(new PredictionWallPlayer(i,p,new MotionRect(p.position.X,p.position.Y,p.width,p.height),p.velocity.X,p.velocity.Y,p.aggro,p.active,p.dead,p.ghost,p.gross,p.itemAnimation==0,p.npcTypeNoAggro[113],pet!=null,pet,pet==null?default(MotionRect):new MotionRect(pet.position.X,pet.position.Y,pet.width,pet.height)));if(!p.active)continue;var r=p.Hitbox;rects.Add(new MotionRect(r.X,r.Y,r.Width,r.Height));exact.Add(new MotionRect(p.position.X,p.position.Y,p.width,p.height));slots.Add(i);alive.Add(!p.dead && !p.ghost);dead.Add(p.dead);}
            bool captured=true,valid=Main.wofNPCIndex>=0 && Main.wofNPCIndex<Main.maxNPCs && Main.npc[Main.wofNPCIndex].active && Main.npc[Main.wofNPCIndex].type==113;var body=valid?Main.npc[Main.wofNPCIndex]:null;
            return new PredictionEnvironment{WallPlayers=new PredictionWallPlayers(peers.ToArray(),peers.Count),WallSource=new PredictionWallSource(captured,valid,body==null?default(NpcIdentity):new NpcIdentity(1,body,body.whoAmI,1,body.type,body.netID),body==null?default(MotionRect):new MotionRect(body.position.X,body.position.Y,body.width,body.height),body==null?0:body.direction,body==null?0:body.velocity.X,body==null?0:body.velocity.Y),WallViewWidth=Main.MaxWorldViewSize.X,WallTimelineCaptured=true,WallTimelineIndex=pc.whoAmI,WallTimelineArea=new MotionRect(pc.position.X,pc.position.Y,pc.width,pc.height),WallTimelineGross=pc.gross,Players=new PredictionPlayers(rects.ToArray(),rects.Count,slots.ToArray(),exact.ToArray(),alive.ToArray(),dead.ToArray()),PlayerIndex=target.whoAmI,PlayerX=target.Center.X,PlayerY=target.Center.Y,PlayerWidth=target.width,PlayerHeight=target.height,PlayerVx=target.velocity.X,PlayerVy=target.velocity.Y,PlayerVelocityCaptured=true,PlayerTimelineActive=target.active && !target.dead && !target.ghost,PlayerDead=target.dead || !target.active,PlayerGross=target.gross,Expert=Main.expertMode,GoodWorld=Main.getGoodWorld,Day=Main.dayTime,Multiplayer=Main.netMode==1,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,GravityWorldSurface=Main.worldSurface,RockLayer=(float)Main.rockLayer};
        }
        private static void Setup(object context,int scene)
        {
            NativeCombatLiveContextChecks.InitializeMount();NativeCombatLiveContextChecks.FlightWorld();Main.maxTilesY=400;Main.bottomWorld=6400;Main.tile=new Tile[400,400];Main.Map=new Terraria.Map.WorldMap(400,400);
            for(int x=0;x<400;x++)for(int y=0;y<400;y++){var tile=new Tile();if(y>=275 || y>=210 && y<=212){tile.active(true);tile.type=TileID.Stone;}Main.tile[x,y]=tile;}
            if(scene==4)for(int x=70;x<180;x++){Main.tile[x,210].slope((byte)(1+x%4));Main.tile[x,211].halfBrick(true);Main.tile[x,212].inActive(true);Main.tile[x,270].liquid=128;}
            NPC.ClearAll();Projectile.ClearAll();Main.wofNPCIndex=-1;Main.wofDrawAreaTop=Main.wofDrawAreaBottom=-1;Main.netMode=0;Main.GameMode=scene==0?0:1;Main.dayTime=false;Main.time=1800;Main.getGoodWorld=scene==2;
            if(Main.ItemDropSolver==null){Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);}for(int i=0;i<Main.item.Length;i++)Main.item[i].whoAmI=i;
            for(int i=0;i<255;i++)Main.player[i].active=false;PlayerAt(Main.myPlayer,scene==11?5800:2000);if(scene==12){PlayerAt(1,2800);Main.player[1].AddBuff(37,10);}
            Main.PlayerSceneMetrics.Reset();Main.LocalPlayer.UpdateSceneMetrics();Main.LocalPlayer.UpdateBiomes();if(context!=null)NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions(path:false,mouseCenter:true,clearLine:false,radius:50));
        }
        private static void PlayerAt(int index,float x)
        {var p=Main.player[index]=new Player{whoAmI=index,active=true,isControlledByFilm=true,releaseJump=true};p.position=new Vector2(x,4400-p.height);p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.immune=true;p.immuneTime=1000000;p.ResetFallDamage();for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;}
        private static NPC Birth(int x,int slot){return Main.npc[NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),x,4200,113,Start:slot,Target:255)];}
        private static int Buff(Player p,int type){int slot=p.FindBuffIndex(type);return slot<0?0:p.buffTime[slot];}
        private static int Count(int type){int count=0;for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type==type)count++;return count;}
        private static NPC Part(int type,bool upper){for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type==type && (type!=114 || (Main.npc[i].ai[0]>0)==upper))return Main.npc[i];return null;}
        private static int Index(NpcMotionState[] rows,NPC n){for(int i=0;i<rows.Length;i++)if(ReferenceEquals(rows[i].Identity.Token,n))return i;return -1;}
        private static void HitTo(NPC n,float fraction){Require(n!=null && n.active && !n.dontTakeDamage,"Real receiving part exists before hit.");n.StrikeNPCNoInteraction(n.life-(int)(n.lifeMax*fraction)+n.defense+20,0,0);Require(n.active && n.life<n.lifeMax*fraction,"Real strike crosses the desired health threshold.");}
        private sealed class EmptyTerrain:IPredictionTerrain
        {
            private readonly bool unknownCells;
            internal EmptyTerrain(bool unknownCells=false){this.unknownCells=unknownCells;}
            public bool CanHit(MotionRect source,MotionRect target,out bool result,out PredictionStop stop){result=true;stop=PredictionStop.None;return true;}
            public bool Solid(MotionRect area,out bool result,out PredictionStop stop){result=false;stop=PredictionStop.None;return true;}
            public bool Tile(int x,int y,out PredictionTile tile,out PredictionStop stop){tile=default(PredictionTile);stop=unknownCells?PredictionStop.TerrainUnavailable:PredictionStop.None;return !unknownCells;}
            public bool Move(ref NpcMotionState state,PredictionEnvironment e,out PredictionStop stop){stop=PredictionStop.None;return true;}
            public bool Unchanged{get{return true;}}public void Reset(){}
        }
    }
}
