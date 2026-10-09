using System;
using System.Collections;
using System.Reflection;
using System.Text;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatPredictionChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var read=source.GetType().GetMethod("Read",Flags);long session=(long)Get(host,"Session");
            Main.dayTime=false;Main.worldSurface=20;Lighting.Mode=Terraria.Graphics.Light.LightMode.Color;
            for(int i=0;i<Main.maxPlayers;i++)if(Main.player[i]==null)Main.player[i]=new Player();
            foreach(var n in Main.npc)n.active=false;
            var player=Main.LocalPlayer;player.position=new Vector2(640,640);
            var env=new PredictionEnvironment{PlayerX=player.Center.X,PlayerY=player.Center.Y,PlayerWidth=player.width,PlayerHeight=player.height,WorldWidth=Main.maxTilesX,WorldSurface=(float)Main.worldSurface,Enraged=true,ClearLine=true};
            Func<NPC,NpcMotionState> capture=n=>(NpcMotionState)read.Invoke(null,new object[]{n,session});
            ReceiveShapes(capture,env,terrain);
            var cache=new NpcPredictionCache();cache.Demand(0,60);
            var npc=Main.npc[0];npc.SetDefaults(372);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,400);npc.ai[0]=0;npc.ai[1]=88;npc.ai[3]=-2;npc.direction=1;
            var states=new[]{capture(npc)};
            string initial=Stamp();cache.Prepare(states,1,0,100,env,terrain);Require(Stamp()==initial,"prediction preserves all native fields/arrays, tile values, RNG and Collision scratch");
            int computed=cache.Steps;var first=cache.Read(0);cache.Demand(1,30);cache.Prepare(states,1,0,100,env,terrain);
            Require(first!=null && cache.Steps==computed && ReferenceEquals(first,cache.Read(1)),"two equivalent consumers share one real result");
            cache.Demand(1,30,120);cache.Prepare(states,1,0,100,env,terrain);Require(cache.Builds==1 && cache.Steps<=120,"longer preferred demand extends existing tail");
            // The shark reaches the finite terrain boundary after 68 future
            // updates. Cancellation preserves this real tail, not 120 steps.
            var cancellationTail=cache.Read(1);cache.Demand(2,120);
            Require(cancellationTail!=null && cancellationTail.Count==69 && cancellationTail.Stop==PredictionStop.TerrainUnavailable && cache.Read(2)==null,"short reader receives the truthful terrain-boundary tail while strict120 rejects it");
            cache.Release(0);Require(cache.Read(0)==null && ReferenceEquals(cache.Read(1),cancellationTail) && cache.Read(2)==null,"consumer cancellation preserves the same other-reader result and strict minimum");
            cache.Release(1);Require(cache.Read(1)==null && cache.Read(2)==null && cache.Required==120 && ReferenceEquals(Get(cache,"result"),cancellationTail),"strict consumer remains registered without accepting the short result");
            cache.Release(2);Require(cache.Required==0 && GetOptional(cache,"result")==null,"last release retires shared result");
            foreach(int type in new[]{2,6,42,49,93,137})
            {
                npc.SetDefaults(type);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,400);npc.velocity=Vector2.Zero;npc.timeLeft=750;
                if(npc.aiStyle==14){npc.ai[1]=200;npc.ai[2]=0;}
                Compare(npc,capture,env,terrain,120,"flight type "+type,.12f);
            }
            foreach(bool damaged in new[]{false,true})
            {
                npc.SetDefaults(133);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,400);npc.velocity=new Vector2(-2,-1);npc.scale=1.2f;npc.timeLeft=750;if(damaged)npc.life=npc.lifeMax/2-1;
                Compare(npc,capture,env,terrain,120,"wandering eye damaged="+damaged,.12f);
            }
            Main.dayTime=true;Main.worldSurface=100;
            var dayEnvironment=env;dayEnvironment.Day=true;dayEnvironment.WorldSurface=100;
            npc.SetDefaults(2);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,400);npc.velocity=new Vector2(2,0);npc.timeLeft=750;
            Compare(npc,capture,dayEnvironment,terrain,120,"eye deterministic daylight retreat",.12f);
            Main.dayTime=false;Main.worldSurface=20;
            Main.tileSolid[TileID.Stone]=true;for(int x=1;x<Main.maxTilesX-1;x++){Main.tile[x,75].active(true);Main.tile[x,75].type=TileID.Stone;}
            foreach(int scenario in new[]{0,1,2,3})
            {
                npc.SetDefaults(61);npc.active=true;npc.whoAmI=0;npc.target=0;npc.timeLeft=750;npc.velocity=Vector2.Zero;
                npc.position=new Vector2(400,1200-npc.height);
                player.position=scenario==0?new Vector2(800,1200-player.height):new Vector2(480,1200-player.height);
                if(scenario==2){npc.life--;player.position=new Vector2(800,1200-player.height);}
                if(scenario==3){npc.ai[0]=1;npc.position=new Vector2(400,900);npc.velocity=new Vector2(-2,-5);}
                var vultureEnvironment=env;vultureEnvironment.PlayerX=player.Center.X;vultureEnvironment.PlayerY=player.Center.Y;
                Compare(npc,capture,vultureEnvironment,terrain,120,"vulture full future scenario "+scenario,.12f);
                Require(scenario==0?npc.ai[0]==0:npc.ai[0]==1,"vulture fixture actually remains perched or triggers flight");
            }
            player.position=new Vector2(640,640);
            foreach(int timer in new[]{-2,-1002,-2002})
            {
                npc.SetDefaults(1);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,1200-npc.height);npc.velocity=Vector2.Zero;npc.ai[0]=timer;npc.ai[1]=-1;npc.ai[2]=1;npc.direction=1;npc.timeLeft=750;
                Compare(npc,capture,env,terrain,120,"slime jump/landing "+timer,.12f);
            }
            NativeCombatGroundChecks.Run(npc,capture,env,terrain);
            for(int x=1;x<Main.maxTilesX-1;x++)Main.tile[x,75].active(false);
            for(int kind=0;kind<4;kind++)
            {
                for(int x=24;x<=30;x++)for(int y=24;y<=30;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(kind);}
                npc.SetDefaults(49);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,400);npc.velocity=new Vector2(2,2);npc.timeLeft=750;npc.lavaImmune=true;
                Compare(npc,capture,env,terrain,120,"bat liquid kind="+kind,.12f);
                if(kind==1)
                {
                    npc.SetDefaults(49);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,400);npc.velocity=new Vector2(2,2);npc.timeLeft=750;npc.lavaImmune=false;npc.life=npc.lifeMax=10000;
                    Compare(npc,capture,env,terrain,120,"bat nonimmune lava",.12f);
                }
            }
            for(int x=24;x<=30;x++)for(int y=24;y<=30;y++){Main.tile[x,y].liquid=0;Main.tile[x,y].liquidType(0);}
            player.position=new Vector2(640,400);var sightEnvironment=env;sightEnvironment.PlayerX=player.Center.X;sightEnvironment.PlayerY=player.Center.Y;
            for(int y=20;y<=27;y++){Main.tile[35,y].active(true);Main.tile[35,y].type=TileID.Stone;}
            npc.SetDefaults(49);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,400);npc.velocity=new Vector2(2,1);npc.ai[1]=200;npc.timeLeft=750;
            Compare(npc,capture,sightEnvironment,terrain,120,"bat LOS changes during full future",.12f);
            for(int y=20;y<=27;y++)Main.tile[35,y].active(false);player.position=new Vector2(640,640);
            npc.SetDefaults(2);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(500,400);npc.buffType[0]=BuffID.Confused;npc.buffTime[0]=30;npc.timeLeft=750;
            Compare(npc,capture,env,terrain,20,"confused eye",.12f);
            Array.Clear(npc.buffType,0,npc.buffType.Length);Array.Clear(npc.buffTime,0,npc.buffTime.Length);npc.confused=false;
            Linked(capture,env,terrain);
            npc.SetDefaults(2);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,400);npc.timeLeft=1;
            Compare(npc,capture,env,terrain,120,"near player renews timeLeft=1",.12f);
            NativeCombatHealthChecks.Run(npc,capture,env,terrain);
            // The oracle is actual fixed .8 UpdateNPC in this isolated process.
            // No production model, collision or movement method is patched out.
            npc.SetDefaults(372);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,400);npc.ai[1]=88;npc.ai[3]=-2;npc.direction=1;
            Compare(npc,capture,env,terrain,12,"shark preparation/dash",.02f);
            npc.SetDefaults(370);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,400);npc.ai[0]=1;npc.ai[2]=28;npc.localAI[0]=1;npc.velocity=new Vector2(16,0);npc.timeLeft=750;
            Compare(npc,capture,env,terrain,10,"Duke dash to hover",.05f);
            Rolling(npc,capture,env,terrain);
            var dukeTiles=Main.tile;int dukeHeight=Main.maxTilesY;
            try
            {
                // The phase-nine dash also needs known terrain beyond the
                // small caller world's bottom scan boundary. Preserve every
                // phase and its full oracle, with no shared Tile references.
                Require(Main.maxTilesX==120 && dukeHeight==120,"Duke phases start with their exact 120x120 caller fixture");
                Main.maxTilesY=160;Main.tile=new Tile[Main.maxTilesX,Main.maxTilesY];
                for(int x=0;x<Main.maxTilesX;x++)for(int y=0;y<Main.maxTilesY;y++)
                    Main.tile[x,y]=y<dukeHeight?(Tile)dukeTiles[x,y].Clone():new Tile();
                foreach(int phase in new[]{0,2,3,4,5,7,8,9,10,12})
                {
                    npc.SetDefaults(370);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,400);npc.velocity=new Vector2(3,-2);npc.localAI[0]=1;npc.ai[0]=phase;npc.ai[2]=phase==4 || phase==9?170:phase==7?110:phase==12?13:phase==3 || phase==8?80:0;npc.ai[3]=phase==0?10:phase==5?6:0;npc.timeLeft=750;npc.direction=1;
                    Compare(npc,capture,env,terrain,120,"Duke deterministic phase "+phase,.12f);
                }
            }
            finally{Main.tile=dukeTiles;Main.maxTilesY=dukeHeight;}
            npc.SetDefaults(371);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,400);npc.ai[0]=1;npc.ai[1]=4;npc.ai[3]=1;npc.timeLeft=750;
            Compare(npc,capture,env,terrain,2,"bubble explosion phase",1.0f);
            npc.SetDefaults(29);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,400);npc.ai[2]=50;npc.ai[3]=45;npc.timeLeft=750;
            var tele=capture(npc);var group=new[]{tele};PredictionStop reason;terrain.Reset();Require(NpcMotion.Step(ref tele,group,1,env,terrain,1,out reason) && tele.NewSegment,"known teleport emits a discontinuity");
            bool dedicated=Main.dedServ;Main.dedServ=true;try{npc.UpdateNPC(0);}finally{Main.dedServ=dedicated;}
            Require(Vector2.Distance(new Vector2(tele.X,tele.Y),npc.position)<.02,"known teleport position follows native update, including post-AI gravity: model="+tele.X+","+tele.Y+" native="+npc.position+" style="+npc.aiStyle+" ai="+string.Join(",",npc.ai));
            npc.SetDefaults(488);npc.active=true;npc.whoAmI=0;npc.position=new Vector2(400,400);npc.velocity=Vector2.Zero;npc.timeLeft=750;cache=new NpcPredictionCache();cache.Demand(0,120);states=new[]{capture(npc)};terrain.Reset();cache.Prepare(states,1,0,200,env,terrain);int built=cache.Builds,stepped=cache.Steps;
            for(int i=1;i<=120;i++)cache.Prepare(states,1,0,200+i,env,terrain);
            Require(cache.Builds==built && cache.Reuses==120 && cache.Steps==stepped,"unchanged real dummy observations republish sample identity without any repeated prediction steps");
            var snapshot=cache.Read(0);states[0].TimeLeft=1;cache.Prepare(states,1,0,320,env,terrain);Require(cache.Builds==built+1 && cache.Read(0).Stop==PredictionStop.None && snapshot.Count==121,"same-tick timer changes invalidate but inactivity-exempt dummy does not despawn; old buffer remains immutable");
            states[0].TimeLeft=750;string unchanged=Stamp();bool threw=false;try{cache.Prepare(states,1,0,321,env,new FailedTerrain());}catch(InvalidOperationException){threw=true;}Require(threw && cache.Read(0)==null && Stamp()==unchanged,"failed preparation retires output and never changes native state");
            Console.WriteLine("PASS first batch prediction: native UpdateNPC oracle, isolated state, known teleport discontinuity, shared/extended demand and cancellation.");
        }
        private sealed class FailedTerrain : IPredictionTerrain
        {public bool Unchanged {get{throw new InvalidOperationException("isolated terrain failure");}}public void Reset(){throw new InvalidOperationException("isolated terrain failure");}public bool Move(ref NpcMotionState n,PredictionEnvironment environment,out PredictionStop stop){stop=PredictionStop.None;throw new InvalidOperationException();}public bool Solid(MotionRect b,out bool value,out PredictionStop stop){value=false;stop=PredictionStop.None;throw new InvalidOperationException();}public bool CanHit(MotionRect a,MotionRect b,out bool value,out PredictionStop stop){value=false;stop=PredictionStop.None;throw new InvalidOperationException();}public bool Tile(int x,int y,out PredictionTile tile,out PredictionStop stop){tile=default(PredictionTile);stop=PredictionStop.None;throw new InvalidOperationException();}}
        private static void ReceiveShapes(Func<NPC,NpcMotionState> capture,PredictionEnvironment env,IPredictionTerrain terrain)
        {
            var npc=Main.npc[0];bool server=Main.dedServ;int delay=NPC.offSetDelayTime,smoothing=Main.multiplayerNPCSmoothingRange;
            try
            {
                NPC.offSetDelayTime=0;Main.multiplayerNPCSmoothingRange=200;Main.dedServ=true;
                npc.SetDefaults(2);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400.75f,400.25f);npc.netOffset=new Vector2(300,400);npc.timeLeft=750;
                var cache=new NpcPredictionCache();cache.Demand(0,120);terrain.Reset();cache.Prepare(new[]{capture(npc)},1,0,100,env,terrain);var future=cache.Read(0);Require(future.Count==121,"network offset forecast has full future");
                for(int tick=0;tick<=120;tick++)
                {
                    if(tick>0)npc.UpdateNPC(0);var expected=new MotionRect((int)(npc.position.X+npc.netOffset.X),(int)(npc.position.Y+npc.netOffset.Y),npc.width,npc.height);
                    Require(PredictionPlayers.Same(future[tick].ReceiveBounds,expected),"full frozen receive shape follows native smoothing tick="+tick);
                }
                var parent=Main.npc[5];parent.SetDefaults(413);parent.whoAmI=5;parent.active=true;parent.position=new Vector2(500,450);parent.ai[0]=6;
                var tail=Main.npc[6];tail.SetDefaults(414);tail.whoAmI=6;tail.active=true;tail.target=0;tail.ai[1]=5;tail.position=new Vector2(420.75f,400.25f);tail.netOffset=new Vector2(20,10);tail.timeLeft=750;
                tail.buffType[0]=24;tail.buffTime[0]=10;tail.buffType[1]=323;tail.buffTime[1]=10;
                for(int x=20;x<40;x++)for(int y=20;y<40;y++)Main.tile[x,y].liquid=255;
                var model=capture(tail);var group=new[]{capture(parent),model};var points=new NpcTrajectoryPoint[2];points[0]=new NpcTrajectoryPoint(0,model);PredictionStop stop;terrain.Reset();Require(NpcMotion.Step(ref model,group,2,env,terrain,1,out stop),"414 known parent predicts next attachment");points[1]=new NpcTrajectoryPoint(1,model);
                for(int tick=0;tick<=1;tick++)
                {
                    if(tick>0)tail.UpdateNPC(6);var body=new Rectangle((int)(tail.position.X+tail.netOffset.X),(int)(tail.position.Y+tail.netOffset.Y),tail.width,tail.height);var projectile=body;projectile.Inflate(8,8);var point=points[tick];
                    Require(point.HasProjectileExtension && point.ReceiveBounds.X==body.X && point.ReceiveBounds.Y==body.Y && point.ProjectileReceiveBounds.X==projectile.X && point.ProjectileReceiveBounds.Y==projectile.Y && point.ProjectileReceiveBounds.Width==projectile.Width && point.ProjectileReceiveBounds.Height==projectile.Height,"414 current/future public body and projectile-only margin agree with native attachment");
                    Require(PredictionPlayers.Same(point.AtOffset(0).ProjectileReceiveBounds,point.ProjectileReceiveBounds),"rolling rebase keeps specialized receive geometry");
                    if(tick==1)Require(model.Health.Fire==0 && model.Health.Fire3==9 && model.Health.Equals(capture(tail).Health),"linked water pass preserves native adjacent-fire deletion order: model="+model.Health.Fire+","+model.Health.Fire3+" native="+capture(tail).Health.Fire+","+capture(tail).Health.Fire3);
                }
                foreach(int type in new[]{13,14})
                {npc.SetDefaults(type);npc.active=true;npc.whoAmI=0;npc.target=0;npc.ai[0]=99;Main.npc[99].active=false;var missing=capture(npc);terrain.Reset();Require(!NpcMotion.Step(ref missing,new[]{missing},1,env,terrain,1,out stop) && stop==PredictionStop.MissingDependency,"EOW head/body cannot forecast through a missing native child: "+type);}
                parent.active=tail.active=false;Console.WriteLine("PASS public receive shapes: 120-tick frozen network smoothing, current/future 414 projectile-only margin, rebase and EOW missing child gates.");
            }
            finally{NPC.offSetDelayTime=delay;Main.multiplayerNPCSmoothingRange=smoothing;Main.dedServ=server;npc.netOffset=Vector2.Zero;for(int x=20;x<40;x++)for(int y=20;y<40;y++)Main.tile[x,y].liquid=0;}
        }
        private static void Rolling(NPC npc,Func<NPC,NpcMotionState> capture,PredictionEnvironment env,IPredictionTerrain terrain)
        {
            npc.SetDefaults(370);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,400);npc.ai[0]=1;npc.ai[2]=0;npc.localAI[0]=1;npc.velocity=new Vector2(16,0);npc.timeLeft=750;
            // Production samples completed updates: native CheckActive resets
            // the live timer then decrements it (750 -> 749) on the first step.
            bool prior=Main.dedServ;Main.dedServ=true;try{npc.UpdateNPC(0);}finally{Main.dedServ=prior;}
            var cache=new NpcPredictionCache();cache.Demand(0,12);var states=new[]{capture(npc)};float initialX=npc.position.X;terrain.Reset();cache.Prepare(states,1,0,500,env,terrain);var original=cache.Read(0);Main.dedServ=true;
            try{for(int i=1;i<=12;i++){npc.UpdateNPC(0);states[0]=capture(npc);cache.Prepare(states,1,0,500+i,env,terrain);Require(cache.Read(0).SampleTick==500+i,"rolling result carries current native sampling tick");}}
            finally{Main.dedServ=prior;}
            Require(cache.Builds==1 && cache.Rolls==12 && cache.Steps==36,"actual advancing native Duke must extend only its rolling tail: builds="+cache.Builds+" rolls="+cache.Rolls+" steps="+cache.Steps);
            Require(original.SampleTick==500 && original[0].Bounds.X==initialX,"rolling publication cannot overwrite a retained result");
            Console.WriteLine("WORKLOAD native moving Duke: 12 updates, builds="+cache.Builds+" rolls="+cache.Rolls+" steps="+cache.Steps+" (full rebuild would be 156).");
        }
        private static void Linked(Func<NPC,NpcMotionState> capture,PredictionEnvironment env,IPredictionTerrain terrain)
        {
            var head=Main.npc[5];var body=Main.npc[6];var tail=Main.npc[7];NPC[] native={head,body,tail};int[] types={7,8,9};
            bool prior=Main.dedServ,priorCorrupt=Main.LocalPlayer.ZoneCorrupt;
            var priorTiles=Main.tile;int priorHeight=Main.maxTilesY;
            int totalRollingSteps=0,totalFullSteps=0;
            try
            {
                // The 120-step oracle plus twelve rolling steps must stay above
                // the native bottom-forty-tile scan boundary. Each cell is an
                // independent copy: this temporary world cannot mutate the
                // caller's tile objects and is always restored on failure.
                Require(Main.maxTilesX==120 && priorHeight==120,"linked oracle starts with its exact 120x120 caller fixture");
                Main.maxTilesY=160;Main.tile=new Tile[Main.maxTilesX,Main.maxTilesY];
                for(int x=0;x<Main.maxTilesX;x++)for(int y=0;y<Main.maxTilesY;y++)
                    Main.tile[x,y]=y<priorHeight?(Tile)priorTiles[x,y].Clone():new Tile();
                Main.dedServ=true;Main.LocalPlayer.ZoneCorrupt=true;env.Corrupt=env.AnyLivingCorrupt=true;env.WorldHeight=Main.maxTilesY;env.RockLayer=(float)Main.rockLayer;
                foreach(int headType in new[]{7,10,13,39,134})foreach(bool earth in new[]{false,true})
                {
                    types=new[]{headType,headType+1,headType+2};
                    for(int x=15;x<85;x++)for(int y=15;y<85;y++){Main.tile[x,y].active(earth);Main.tile[x,y].type=TileID.Stone;}
                    for(int i=0;i<3;i++){var n=native[i];n.SetDefaults(types[i]);n.active=true;n.whoAmI=i+5;n.target=0;n.direction=n.directionY=-1;n.ai[0]=i==2?0:i+6;n.ai[1]=i==0?0:i+4;n.ai[3]=n.realLife=5;n.position=new Vector2(420-i*30,400+i*15);n.timeLeft=750;n.life=n.lifeMax=10000;if(headType!=134){n.width|=1;n.height|=1;}}
                    body.AddBuff(BuffID.Poisoned,2);body.lifeRegenCount=-119;
                    var group=new[]{capture(head),capture(body),capture(tail)};var future=new NpcMotionState[120,3];terrain.Reset();float maximum=0;
                    for(int tick=1;tick<=120;tick++)
                    {
                        for(int j=0;j<3;j++){var n=group[j];PredictionStop stop;Require(NpcMotion.Step(ref n,group,3,env,terrain,tick,out stop),"full worm forecast type="+headType+" earth="+earth+" slot="+j+" tick="+tick+" stop="+stop+" bounds="+n.X+","+n.Y+","+n.Width+","+n.Height+" world="+Main.maxTilesX+","+Main.maxTilesY);group[j]=n;}
                        for(int j=0;j<3;j++)future[tick-1,j]=group[j];
                    }
                    for(int tick=1;tick<=120;tick++)
                    {
                        for(int j=0;j<3;j++)native[j].UpdateNPC(j+5);
                        for(int j=0;j<3;j++)
                        {
                            var n=future[tick-1,j];var observed=capture(native[j]);float error=Vector2.Distance(new Vector2(n.X,n.Y),native[j].position);maximum=Math.Max(maximum,error);
                            Require(native[j].active && error<.12,"full worm type="+headType+" earth="+earth+" slot="+j+" tick="+tick+" error="+error+" model="+n.X+","+n.Y+" native="+native[j].position);
                            Require(n.Life==observed.Life && n.Health.Equals(observed.Health),"whole-tick worm shared health type="+headType+" slot="+j+" tick="+tick+" modelLife="+n.Life+" nativeLife="+observed.Life+" realLife="+n.Health.RealLife+"/"+observed.Health.RealLife);
                            Require(n.Target==observed.Target && n.Direction==observed.Direction && n.DirectionY==observed.DirectionY,"worm native target/facing type="+headType+" slot="+j+" tick="+tick+" dir="+n.Direction+","+n.DirectionY+"/"+observed.Direction+","+observed.DirectionY);
                        }
                    }
                    Console.WriteLine("ORACLE full worm chain type="+headType+" earth="+earth+" 120 ticks max="+maximum.ToString("F4"));
                    var chainCache=new NpcPredictionCache();chainCache.Demand(0,12);var observedGroup=new[]{capture(head),capture(body),capture(tail)};terrain.Reset();chainCache.Prepare(observedGroup,3,0,1000,env,terrain);
                    for(int tick=1;tick<=12;tick++)
                    {
                        var expected=(NpcMotionState[])Get(chainCache,"first");
                        for(int j=0;j<3;j++)native[j].UpdateNPC(j+5);
                        for(int j=0;j<3;j++)
                        {
                            observedGroup[j]=capture(native[j]);
                            var rounded=expected[j];var actual=observedGroup[j];
                            // Native and the independent x86 integrator can
                            // round a linked center one float unit differently.
                            // The production cache deliberately rebuilds on it;
                            // never relax its state equality to force reuse.
                            if(Math.Abs(rounded.X-actual.X)<.0002f)rounded.X=actual.X;if(Math.Abs(rounded.Y-actual.Y)<.0002f)rounded.Y=actual.Y;
                            if(Math.Abs(rounded.OldX-actual.OldX)<.0002f)rounded.OldX=actual.OldX;if(Math.Abs(rounded.OldY-actual.OldY)<.0002f)rounded.OldY=actual.OldY;
                            if(Math.Abs(rounded.Vx-actual.Vx)<.000002f)rounded.Vx=actual.Vx;if(Math.Abs(rounded.Vy-actual.Vy)<.000002f)rounded.Vy=actual.Vy;
                            if(!NpcPredictionCache.Same(rounded,actual))
                            {var why=new StringBuilder();foreach(var f in typeof(NpcMotionState).GetFields())if(!Equals(f.GetValue(expected[j]),f.GetValue(observedGroup[j])))why.Append(f.Name).Append('=').Append(f.GetValue(expected[j])).Append('/').Append(f.GetValue(observedGroup[j])).Append(';');Require(false,"worm rolling dependency type="+headType+" earth="+earth+" slot="+j+" tick="+tick+" "+why);}
                        }
                        chainCache.Prepare(observedGroup,3,0,1000+tick,env,terrain);
                    }
                    Require(chainCache.Rolls>0 && chainCache.Steps<468,"whole-chain rolling reuses matching native steps for type="+headType+" builds="+chainCache.Builds+" rolls="+chainCache.Rolls+" steps="+chainCache.Steps);totalRollingSteps+=chainCache.Steps;totalFullSteps+=468;
                    Console.WriteLine("WORKLOAD native worm type="+headType+" earth="+earth+" builds="+chainCache.Builds+" rolls="+chainCache.Rolls+" steps="+chainCache.Steps+" versus 468 repeated-full steps");
                }
                Require(totalRollingSteps<totalFullSteps/2,"mixed native chains save more than half repeated-full work: steps="+totalRollingSteps+" full="+totalFullSteps);
            }
            finally{Main.dedServ=prior;Main.LocalPlayer.ZoneCorrupt=priorCorrupt;foreach(var n in native)n.active=false;Main.tile=priorTiles;Main.maxTilesY=priorHeight;}
        }
        internal static void Compare(NPC npc,Func<NPC,NpcMotionState> capture,PredictionEnvironment env,IPredictionTerrain terrain,int ticks,string name,float maximum)
        {
            var model=capture(npc);var group=new[]{model};Vector2 start=npc.position,velocity=npc.velocity;double sum=0,baseline=0;float worst=0;int grounded=0,clearSteps=0,blockedSteps=0;bool dedicated=Main.dedServ;
            terrain.Reset();Main.dedServ=true;
            try
            {
                // Freeze the entire forecast before native time advances. Lazy
                // terrain reads must not see future native AI/world changes.
                var forecast=new NpcMotionState[ticks];
                for(int i=1;i<=ticks;i++)
                {PredictionStop stop;Require(NpcMotion.Step(ref model,group,1,env,terrain,i,out stop),name+" model unexpectedly ended at "+i+": "+stop);group[0]=model;forecast[i-1]=model;}
                for(int i=1;i<=ticks;i++)
                {
                    model=forecast[i-1];
                    if(name.StartsWith("bat LOS",StringComparison.Ordinal)){var player=Main.LocalPlayer;if(Collision.CanHit(npc.position,npc.width,npc.height,player.position,player.width,player.height))clearSteps++;else blockedSteps++;}
                    npc.UpdateNPC(npc.whoAmI);
                    if(npc.collideY && npc.velocity.Y==0)grounded++;
                    float error=Vector2.Distance(new Vector2(model.X,model.Y),npc.position);worst=Math.Max(worst,error);sum+=error;baseline+=Vector2.Distance(start+velocity*i,npc.position);
                    Require(error<=maximum,name+" t="+i+" error="+error+" native="+npc.position+" model="+model.X+","+model.Y);
                    Require(model.A0==npc.ai[0] && model.CanReceive==(!npc.dontTakeDamage && !npc.immortal),name+" phase/receive mismatch t="+i);
                    Require(model.Life==npc.life,name+" health mismatch t="+i+" predicted="+model.Life+" native="+npc.life);
                    Require(model.Health.Regen==npc.lifeRegen && model.Health.RegenCount==npc.lifeRegenCount,name+" DOT accumulation mismatch t="+i);
                    if(name.StartsWith("double fire",StringComparison.Ordinal))Require(model.Health.Equals(capture(npc).Health),name+" ordered buff state mismatch t="+i+" fire="+model.Health.Fire+"/"+capture(npc).Health.Fire+" fire3="+model.Health.Fire3+"/"+capture(npc).Health.Fire3);
                }
            }
            finally{Main.dedServ=dedicated;}
            if(name.StartsWith("slime",StringComparison.Ordinal))Require(grounded>0,"slime oracle must actually land on the native solid floor: position="+npc.position+" velocity="+npc.velocity+" noGravity="+npc.noGravity+" noTile="+npc.noTileCollide+" ai="+string.Join(",",npc.ai));
            if(name.StartsWith("bat LOS",StringComparison.Ordinal))Require(clearSteps>0 && blockedSteps>0,"future LOS fixture crosses both visibility states: clear="+clearSteps+" blocked="+blockedSteps);
            Console.WriteLine("ORACLE "+name+" ticks="+ticks+" mean="+(sum/ticks).ToString("F4")+" max="+worst.ToString("F4")+" inertialMean="+(baseline/ticks).ToString("F4"));
        }
        private static string Stamp()
        {
            var s=new StringBuilder();foreach(var n in Main.npc)Append(s,n);foreach(var p in Main.player)Append(s,p);foreach(var p in Main.projectile)Append(s,p);
            Append(s,Main.rand);for(int x=0;x<Main.maxTilesX;x++)for(int y=0;y<Main.maxTilesY;y++)Append(s,Main.tile[x,y]);
            foreach(var field in typeof(Collision).GetFields(Flags))if(field.IsStatic && (field.FieldType.IsPrimitive || field.FieldType.IsValueType))s.Append(field.Name).Append('=').Append(field.GetValue(null)).Append(';');
            return s.ToString();
        }
        private static void Append(StringBuilder s,object value)
        {
            if(value==null){s.Append("null;");return;}
            foreach(var field in value.GetType().GetFields(Flags))
            {
                if(field.IsStatic)continue;object data=field.GetValue(value);s.Append(field.Name).Append('=');
                var array=data as Array;if(array!=null){foreach(var item in array)s.Append(item).Append(',');}
                else if(data!=null && (data.GetType().IsValueType || data is string))s.Append(data);
                else s.Append(data==null?0:System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(data));s.Append(';');
            }
        }
    }
}
