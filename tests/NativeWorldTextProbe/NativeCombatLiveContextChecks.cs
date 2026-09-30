using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;
using Terraria.ID;

namespace NativeWorldTextProbe
{
    // Focused reproduction of ordinary production acceptance under additional
    // live-world context. The callback keeps the existing original/Host order;
    // no prediction result or history state is patched by this fixture.
    internal static class NativeCombatLiveContextChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        internal static void Run(object context,NpcPredictionCache cache,Action step,string output,ProbeGraphics graphics=null)
        {
            string mode=Environment.GetEnvironmentVariable("JUEMINGR_NPC_LIVE_CONTEXT");
            var native=Get(Get(Get(context,"CombatObservation"),"Prediction"),"Native");
            Require(native.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionDiagnostic")==null,"Ordinary product excludes the retired one-shot diagnostic.");
            if(mode=="mounted"){Mounted(context,native,cache,step);return;}
            if(mode=="post-delivery"){PostDelivery(context,native,cache,step);return;}
            if(mode=="liquid"){Liquid(context,native,cache,step);return;}
            if(mode=="name-draw"){NameDraw(context,native,cache,step,graphics,output);return;}
            if(mode=="relocations"){Relocations(context,native,cache,step);return;}
            if(mode=="special"){Special(context,native,cache,step);return;}
            if(mode=="families"){NativeCombatLiveCoverageChecks.Run(context,native,cache,step);return;}
            if(mode=="environment"){NativeCombatEnvironmentChecks.Run(context,native,cache,step,output);return;}
            if(mode=="post-delivery-suite"){Relocations(context,native,cache,step);Liquid(context,native,cache,step);Special(context,native,cache,step);PostDelivery(context,native,cache,step);NativeCombatLiveCoverageChecks.Run(context,native,cache,step);return;}
            if(mode!="shared-rng")throw new InvalidOperationException("Unknown live-context scenario.");
            int failed=0;
            foreach(bool neighbor in new[]{false,true})
            {
                foreach(var npc in Main.npc)npc.active=false;
                foreach(var projectile in Main.projectile)projectile.active=false;
                var player=Main.LocalPlayer;player.controlRight=player.controlLeft=player.controlJump=false;
                player.position=new Vector2(640,70*16-player.height);player.velocity=Vector2.Zero;
                if(neighbor)
                {
                    int bunny=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),300,70*16,NPCID.Bunny);
                    Require(bunny==0 && Main.npc[bunny].friendly,"Unrelated native Bunny occupies the earlier slot.");
                }
                int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),680,70*16,NPCID.Zombie,Start:1);
                Require(slot>=1 && slot<Main.maxNPCs,"Selected native Zombie occupies a later slot, respecting original spawn protection.");
                long requests=(long)Get(native,"Requests"),rejected=(long)Get(native,"Rejected"),refused=(long)Get(native,"Refused");
                int shown=0,longest=0,blank=0;var reasons=new Dictionary<string,int>();
                for(int frame=0;frame<300;frame++)
                {
                    step();var path=cache.Read(0);
                    string reason=(string)Get(native,"Reason")??"none";
                    int count;reasons.TryGetValue(reason,out count);reasons[reason]=count+1;
                    if(path==null){longest=Math.Max(longest,++blank);continue;}
                    blank=0;shown++;
                    Require(path.Identity.Slot==slot && ReferenceEquals(path.Identity.Token,Main.npc[slot]) && path.Strategy==PredictionStrategy.NativeIsolated,"Current native target owns every displayed window.");
                    Require(path.SampleTick==Main.GameUpdateCount && path.Count==121 && Math.Abs(path[0].Bounds.X-Main.npc[slot].position.X)<.002f && Math.Abs(path[0].Bounds.Y-Main.npc[slot].position.Y)<.002f,"Current origin and complete future remain truthful.");
                }
                Console.WriteLine("LIVE-CONTEXT neighbor="+neighbor+" slot="+slot+" shown="+shown+" longest-blank="+longest+" requests="+((long)Get(native,"Requests")-requests)+" rejected="+((long)Get(native,"Rejected")-rejected)+" refused="+((long)Get(native,"Refused")-refused));
                foreach(var pair in reasons.OrderByDescending(p=>p.Value))Console.WriteLine("LIVE-REASON frames="+pair.Value+" "+pair.Key);
                if(shown<180 || longest>=60)failed++;
            }
            Require(failed==0,"Ordinary production must remain usable with an unrelated earlier NPC; failing scenes="+failed);
        }
        private static void Mounted(object context,object native,NpcPredictionCache cache,Action step)
        {
            InitializeMount();
            foreach(var npc in Main.npc)npc.active=false;
            foreach(var projectile in Main.projectile)projectile.active=false;
            var player=Main.LocalPlayer;player.controlRight=player.controlLeft=player.controlJump=false;
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),680,70*16,NPCID.Zombie,Start:1);
            Require(slot>=1 && slot<Main.maxNPCs,"Native Zombie birth for mounted production.");
            foreach(int mount in new[]{0,MountID.WitchBroom,-1})
            {
            int local=Main.myPlayer;
            // Original mounting establishes its buff, dimensions and owned
            // MountData. Suppress only the local network outlet during this
            // isolated setup; every measured update is the ordinary player.
            try{Main.myPlayer=1;if(mount<0)player.mount.Dismount(player);else player.mount.SetMount(mount,player);}finally{Main.myPlayer=local;}
            player.position=new Vector2(640,70*16-player.height);player.velocity=Vector2.Zero;
            int shown=0,longest=0,blank=0;var reasons=new Dictionary<string,int>();
            for(int frame=0;frame<300;frame++)
            {
                step();Require(player.mount.Active==(mount>=0),"Original scene retains its mount state.");
                var path=cache.Read(0);string reason=(string)Get(native,"Reason")??"none";
                int count;reasons.TryGetValue(reason,out count);reasons[reason]=count+1;
                if(path==null){longest=Math.Max(longest,++blank);continue;}
                blank=0;shown++;
                Require(path.Identity.Slot==slot && ReferenceEquals(path.Identity.Token,Main.npc[slot]) && path.Strategy==PredictionStrategy.NativeIsolated,"Mounted player keeps the selected native target.");
                Require(path.SampleTick==Main.GameUpdateCount && path.Count==121 && Math.Abs(path[0].Bounds.X-Main.npc[slot].position.X)<.002f && Math.Abs(path[0].Bounds.Y-Main.npc[slot].position.Y)<.002f,"Mounted production has a truthful current origin and 120 future steps.");
                if(mount>=0)Require((path.Assumptions&PredictionAssumption.ApproximateMechanism)!=0,"Mounted conditional movement remains explicitly approximate.");
            }
            Console.WriteLine("MOUNTED type="+mount+" shown="+shown+" longest-blank="+longest);
            foreach(var pair in reasons.OrderByDescending(p=>p.Value))Console.WriteLine("MOUNTED-REASON frames="+pair.Value+" "+pair.Key);
            Require(shown>=180 && longest<60,"Mounted ordinary prediction must publish continuously after preparation.");
            }
        }
        private static void Relocations(object context,object native,NpcPredictionCache cache,Action step)
        {
            var player=Main.LocalPlayer;var worker=Get(native,"Worker");
            player.controlLeft=player.controlRight=false;
            Action ready=()=>{for(int i=0;i<90 && cache.Read(0)==null;i++)step();Require(cache.Read(0)!=null,"Relocation regression begins with an actual accepted production result.");};
            ready();
            foreach(int style in new[]{-1,2,3})
            {
                player.Teleport(player.position+new Vector2(8,0),style);
                Require((int)Get(native,"observedRelocation")==1,"Original Player.Teleport records relocation even when teleporting is false.");
                step();Require(cache.Read(0)==null && (int)Get(native,"observedRelocation")==0,"Owner consumes relocation before displaying any old result.");ready();
                Require(ReferenceEquals(worker,Get(native,"Worker")),"Relocation preserves the prepared worker.");
            }
            int oldMode=Main.netMode;var remote=Main.player[1];Main.player[1]=new Player{whoAmI=1,active=true,position=new Vector2(900,850)};
            try
            {
                Main.netMode=1;var buffer=new MessageBuffer{whoAmI=256};
                foreach(bool large in new[]{false,true})
                {
                    var p=Main.player[1];Vector2 next=p.position+new Vector2(large?Main.multiplayerNPCSmoothingRange+16:1,0);
                    using(var stream=new System.IO.MemoryStream())using(var writer=new System.IO.BinaryWriter(stream))
                    {writer.Write((byte)13);writer.Write((byte)1);writer.Write((byte)0);writer.Write((byte)16);writer.Write((byte)0);writer.Write((byte)0);writer.Write((byte)0);writer.Write(next.X);writer.Write(next.Y);writer.Flush();byte[] bytes=stream.ToArray();Array.Copy(bytes,buffer.readBuffer,bytes.Length);int message;buffer.GetData(0,bytes.Length,out message);Require(message==13 && p.position==next,"Real original player control packet is applied.");}
                    Require(((int)Get(native,"observedRelocation")!=0)==large,"Only the original unsmoothed correction boundary records relocation.");
                    if(large)Require(p.netOffset==Vector2.Zero,"Original large correction clears netOffset; post-update polling alone would miss it.");
                }
            }
            finally{Main.netMode=oldMode;Main.player[1]=remote;}
            step();Require(cache.Read(0)==null,"A network relocation cannot retain a pre-correction result.");ready();
            player.immune=false;player.immuneTime=0;for(int i=0;i<player.hurtCooldowns.Length;i++)player.hurtCooldowns[i]=0;
            player.statLife=player.statLifeMax=player.statLifeMax2=400;player.noKnockback=false;
            double hurt=player.Hurt(Terraria.DataStructures.PlayerDeathReason.ByCustomReason("isolated knockback oracle"),10,1,quiet:true,dodgeable:false);
            Require(hurt>0 && player.velocity==new Vector2(4.5f,-3.5f),"Original Hurt must really apply the observed external impulse.");
            Require((int)Get(native,"observedRelocation")!=0,"An actual hit retires conditional futures even when their inputs/mechanism are unchanged.");
            step();Require(cache.Read(0)==null,"Old no-new-hits continuation cannot survive an actual hit.");ready();
            player.immune=true;player.immuneTime=10000;hurt=player.Hurt(Terraria.DataStructures.PlayerDeathReason.ByCustomReason("isolated immune oracle"),10,1,quiet:true,dodgeable:false);
            Require(hurt==0 && (int)Get(native,"observedRelocation")==0,"Rejected damage and ordinary immunity do not repeatedly retire a path.");
            Console.WriteLine("PASS actual Player.Teleport styles, original packet13 small/large corrections, Hurt knockback / immune-zero counterexample, immediate owner retirement and resident-worker recovery; isolated client dispatch, not multiplayer acceptance.");
        }
        private static void Special(object context,object native,NpcPredictionCache cache,Action step)
        {
            InitializeMount();
            FlightWorld();foreach(var n in Main.npc)n.active=false;foreach(var p in Main.projectile)p.active=false;
            var player=Main.LocalPlayer;
            player.controlLeft=player.controlRight=player.controlUp=player.controlDown=player.controlJump=false;player.position=new Vector2(1000,1500);player.velocity=Vector2.Zero;
            player.fallStart=player.fallStart2=(int)(player.position.Y/16);player.dead=false;player.breath=200;player.statLife=player.statLifeMax=player.statLifeMax2=400;
            player.wet=Collision.WetCollision(player.position,player.width,player.height);player.honeyWet=Collision.honey;player.shimmerWet=Collision.shimmer;player.lavaWet=false;
            int local=Main.myPlayer;try{Main.myPlayer=1;player.mount.SetMount(5,player);}finally{Main.myPlayer=local;}
            for(int i=0;i<player.hurtCooldowns.Length;i++)player.hurtCooldowns[i]=100000;
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1200,1500,NPCID.Crimera,Start:1);
            int changes=0,shown=0,stableBlanks=0,gaps=0;bool priorShown=false;float priorSpeed=player.maxRunSpeed;var reasons=new Dictionary<string,int>();
            for(int frame=0;frame<480;frame++)
            {
                step();Require(!player.dead && player.mount.Active && player.mount.Type==5,"Original bee flight remains an active real fixture.");
                if(player.maxRunSpeed!=priorSpeed)changes++;priorSpeed=player.maxRunSpeed;
                bool present=cache.Read(0)!=null;if(present){shown++;Require(cache.Read(0).Identity.Slot==slot,"Conditional movement retains the real selected target.");}else if(frame>=30)stableBlanks++;
                if(priorShown && !present)gaps++;priorShown=present;string reason=(string)Get(native,"Reason")??"none";int count;reasons.TryGetValue(reason,out count);reasons[reason]=count+1;
            }
            Console.WriteLine("SPECIAL bee updates=480 real-speed-changes="+changes+" shown="+shown+" stable-blanks="+stableBlanks+" gaps="+gaps+" position="+player.position);
            foreach(var pair in reasons.OrderByDescending(p=>p.Value))Console.WriteLine("SPECIAL-REASON frames="+pair.Value+" "+pair.Key);
            Require(changes>20,"Original fatigue really changes movement outputs under held controls.");
            Require(stableBlanks==0,"A conditional motion output must not cause permanent self-rejection or periodic flicker.");
        }
        private static void NameDraw(object context,object native,NpcPredictionCache cache,Action step,ProbeGraphics graphics,string output)
        {
            Require(graphics!=null,"Name fade regression requires actual original texture rendering.");
            graphics.LoadTexture("Npc","Images/NPC_173",173);
            InitializeMount();
            var engine=typeof(Lighting).GetField("NewEngine",Flags|BindingFlags.Static).GetValue(null);var engineType=engine.GetType();
            var map=(Terraria.Graphics.Light.LightMap)engineType.GetField("_activeLightMap",Flags).GetValue(engine);var area=engineType.GetField("_activeProcessedArea",Flags);
            map.SetSize(Main.maxTilesX,Main.maxTilesY);area.SetValue(engine,new Rectangle(0,0,Main.maxTilesX,Main.maxTilesY));Lighting.GlobalBrightness=1;
            var host=Get(context,"CombatObservation");var world=Get(host,"World");var player=Main.LocalPlayer;int failed=0;
            foreach(int netId in new[]{173,-22,-23})foreach(int ratio in new[]{1,3,-3})foreach(bool mounted in new[]{false,true})
            {
                foreach(var n in Main.npc)n.active=false;foreach(var p in Main.projectile)p.active=false;
                player.controlLeft=player.controlRight=player.controlUp=player.controlDown=player.controlJump=false;
                int local=Main.myPlayer;try{Main.myPlayer=1;if(mounted)player.mount.SetMount(MountID.WitchBroom,player);else player.mount.Dismount(player);}finally{Main.myPlayer=local;}
                player.position=new Vector2(680,mounted?820:1120-player.height);player.velocity=Vector2.Zero;player.fallStart=player.fallStart2=(int)(player.position.Y/16);
                for(int i=0;i<player.hurtCooldowns.Length;i++)player.hurtCooldowns[i]=100000;
                // Keep the earlier critter behind this movement window. It is
                // a damageable candidate; putting it ahead caused a genuine
                // nearest-target change, not stale Crimera publication.
                if(mounted)NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),300,1120,NPCID.Bunny);
                int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),760,mounted?850:1060,netId,Start:1);var npc=Main.npc[slot];
                Require(npc.type==173 && npc.netID==netId,"Original spawn establishes the true Crimera variant.");
                int shown=0,blank=0,selected=0,fadeChanges=0,drawCount=0,transitionBlanks=0;bool publishedInInput=false;
                var firstInInput=new[]{-1,-1,-1};var blankFrames=new List<int>();
                float minX=player.position.X,maxX=minX;var reasons=new Dictionary<string,int>();
                for(int frame=0;frame<180;frame++)
                {
                    if(frame==0 || mounted && frame%60==0)publishedInInput=false;
                    if(frame%20==0)for(int x=0;x<Main.maxTilesX;x++)for(int y=0;y<Main.maxTilesY;y++)map[x,y]=frame/20%2==0?Vector3.One:Vector3.Zero;
                    player.controlRight=mounted && frame%120<60;player.controlLeft=mounted && !player.controlRight;
                    step();minX=Math.Min(minX,player.position.X);maxX=Math.Max(maxX,player.position.X);if((bool)Get(Get(host,"Selection"),"HasTarget"))selected++;
                    var path=cache.Read(0);string reason=(string)Get(native,"Reason")??"none";int count;reasons.TryGetValue(reason,out count);reasons[reason]=count+1;
                    // A real left/right edge retires the old held-input
                    // future. Count its complete recovery separately; after
                    // the first publication in that input window, ANY blank
                    // is a continuity failure (no percentage threshold).
                    if(path==null){blankFrames.Add(frame);if(publishedInInput)blank++;else transitionBlanks++;}
                    else
                    {
                        int inputWindow=mounted?frame/60:0;
                        if(!publishedInInput)firstInInput[inputWindow]=mounted?frame%60:frame;
                        publishedInInput=true;
                        shown++;Require(path.Identity.Slot==slot && ReferenceEquals(path.Identity.Token,npc) && path.Identity.NetId==netId && path.SampleTick==Main.GameUpdateCount,"Actual selected variant owns the current displayed window: netId="+netId+" mounted="+mounted+" frame="+frame+" expected-slot="+slot+" actual-slot="+path.Identity.Slot+" actual-netId="+path.Identity.NetId+" same-instance="+ReferenceEquals(path.Identity.Token,npc)+" sample="+path.SampleTick+" current="+Main.GameUpdateCount);
                        Require((int)Get(world,"StrokeCount")>0 && Get(world,"pathText")!=null,"Cache reaches prepared line geometry and current path feedback.");
                    }
                    int draws=ratio<0?(frame%3==0?1:0):ratio;
                    for(int draw=0;draw<draws;draw++)
                    {
                        float prior=npc.nameOver;ulong tick=Main.GameUpdateCount;
                        graphics.Render(()=>{Main.instance.DrawNPCDirect(Main.spriteBatch,npc,false,Main.screenPosition);world.GetType().GetMethod("Draw",Flags).Invoke(world,null);},Main.GameViewMatrix.ZoomMatrix);
                        Require(Main.GameUpdateCount==tick,"Original Draw never advances simulation time.");drawCount++;if(npc.nameOver!=prior)fadeChanges++;
                    }
                    if(frame==70)graphics.Image(System.IO.Path.Combine(output,"crimera-"+netId+"-ratio-"+ratio+"-mounted-"+mounted+".png"),()=>{Main.instance.DrawNPCDirect(Main.spriteBatch,npc,false,Main.screenPosition);world.GetType().GetMethod("Draw",Flags).Invoke(world,null);},Main.GameViewMatrix.ZoomMatrix);
                }
                Console.WriteLine("NAME-DRAW netId="+netId+" mounted="+mounted+" updates=180 draws="+drawCount+" ratio="+ratio+" fades="+fadeChanges+" selected="+selected+" shown="+shown+" stable-blanks="+blank+" transition-blanks="+transitionBlanks+" input-first="+string.Join("/",firstInInput)+" blank-frames="+string.Join("/",blankFrames)+" x-span="+(maxX-minX));
                foreach(var pair in reasons.OrderByDescending(p=>p.Value))Console.WriteLine("NAME-REASON frames="+pair.Value+" "+pair.Key);
                Require(fadeChanges>20,"The actual original bright/dark name fade branch must execute repeatedly.");
                if(mounted)Require(maxX-minX>20 && player.position.Y<1000,"Combined Draw scenario really moves an airborne broom with another active actor.");
                Require(firstInInput[0]>=0 && (!mounted || firstInInput[1]>=0 && firstInInput[2]>=0),"Every actual held-input window must publish; report complete first-publication latency for each edge.");
                if(blank!=0 || selected!=180)failed++;
            }
            Require(failed==0,"Actual Draw/Update ratio must not periodically retire otherwise valid Crimera paths. Failing windows="+failed);
        }
        private static void Liquid(object context,object native,NpcPredictionCache cache,Action step)
        {
            var player=Main.LocalPlayer;player.controlLeft=player.controlRight=player.controlUp=player.controlDown=player.controlJump=false;
            foreach(int type in new[]{NPCID.BloodFeeder,NPCID.BloodJelly,NPCID.Zombie})
            {
                foreach(var n in Main.npc)n.active=false;foreach(var p in Main.projectile)p.active=false;
                for(int x=20;x<100;x++)for(int y=40;y<70;y++){Main.tile[x,y].active(false);Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(0);}
                player.position=new Vector2(660,1120-player.height);player.velocity=Vector2.Zero;
                player.dead=false;player.breath=200;player.statLife=player.statLifeMax=player.statLifeMax2=400;
                for(int i=0;i<player.hurtCooldowns.Length;i++)player.hurtCooldowns[i]=100000;
                int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),760,1000,type,Start:1);
                int shown=0,segments=0,wet=0,stableBlanks=0;var reasons=new Dictionary<string,int>();
                for(int frame=0;frame<180;frame++)
                {
                    step();if(Main.npc[slot].wet)wet++;var path=cache.Read(0);string reason=(string)Get(native,"Reason")??"none";int count;reasons.TryGetValue(reason,out count);reasons[reason]=count+1;
                    if(path==null){if(frame>=60)stableBlanks++;continue;}shown++;
                    Require(path.Identity.Slot==slot && ReferenceEquals(path.Identity.Token,Main.npc[slot]),"Liquid result owns the current real target.");
                    for(int i=1;i<path.Count;i++)if(path[i].NewSegment)segments++;
                }
                Console.WriteLine("LIQUID type="+type+" netId="+Main.npc[slot].netID+" wet="+wet+" shown="+shown+" stable-blanks="+stableBlanks+" segments="+segments);
                foreach(var pair in reasons.OrderByDescending(p=>p.Value))Console.WriteLine("LIQUID-REASON frames="+pair.Value+" "+pair.Key);
                Require(wet>120 && shown>0,"Original aquatic scene is wet and actually publishes a trajectory.");
                Require(segments==0,"Ordinary wet motion must not be emitted as repeated teleport segments.");
                Require(stableBlanks==0,"Stable aquatic target must continuously retain its current result.");
            }
        }
        private static void PostDelivery(object context,object native,NpcPredictionCache cache,Action step)
        {
            InitializeMount();
            var player=Main.LocalPlayer;int failures=0;
            // Give held flight real travel room: the earlier 120-tile fixture
            // reached its border before the braking phase began.
            FlightWorld();
            foreach(int type in new[]{NPCID.Crimera,-22,-23,NPCID.Zombie})
            foreach(bool mounted in new[]{false,true})
            {
                foreach(var n in Main.npc)n.active=false;
                foreach(var p in Main.projectile)p.active=false;
                player.controlLeft=player.controlRight=player.controlUp=player.controlDown=player.controlJump=false;
                int local=Main.myPlayer;
                try{Main.myPlayer=1;if(mounted)player.mount.SetMount(MountID.WitchBroom,player);else player.mount.Dismount(player);}finally{Main.myPlayer=local;}
                player.position=new Vector2(1000,mounted?1800:150*16-player.height);player.velocity=Vector2.Zero;
                player.fallStart=player.fallStart2=(int)(player.position.Y/16);player.dead=false;player.statLife=player.statLifeMax=player.statLifeMax2=400;
                for(int i=0;i<player.hurtCooldowns.Length;i++)player.hurtCooldowns[i]=100000;
                int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1240,150*16,type,Start:1);
                Require(slot>=1 && slot<Main.maxNPCs,"Original legal birth for production regression.");
                foreach(string motion in mounted?new[]{"hover","right","brake","up","down","diagonal","reverse","hover-again","unmount","remount"}:new[]{"stationary","walking"})
                {
                    if(motion=="unmount" || motion=="remount")
                    {int owner=Main.myPlayer;try{Main.myPlayer=1;if(motion=="unmount")player.mount.Dismount(player);else player.mount.SetMount(MountID.WitchBroom,player);}finally{Main.myPlayer=owner;}}
                    player.controlRight=motion=="right" || motion=="diagonal" || motion=="walking";
                    player.controlLeft=motion=="reverse";
                    player.controlUp=motion=="up" || motion=="diagonal";
                    player.controlDown=motion=="down";
                    int shown=0,blank=0,longest=0,gaps=0,stableBlanks=0,selected=0;bool wasShown=false;
                    var reasons=new Dictionary<string,int>();Vector2 begin=player.position;float minY=player.position.Y,maxXSpeed=0,maxYSpeed=0,startSpeed=player.velocity.X;
                    // Input stays held through real original updates. Only the
                    // beginning of each independent scene sets position; never
                    // pin velocity or pull the player back during a window.
                    for(int frame=0;frame<120;frame++)
                    {
                        step();Require(!player.dead,"Movement fixture must remain alive through its real original updates.");minY=Math.Min(minY,player.position.Y);maxXSpeed=Math.Max(maxXSpeed,Math.Abs(player.velocity.X));maxYSpeed=Math.Max(maxYSpeed,Math.Abs(player.velocity.Y));
                        if((bool)Get(Get(Get(context,"CombatObservation"),"Selection"),"HasTarget"))selected++;
                        var path=cache.Read(0);string reason=(string)Get(native,"Reason")??"none";
                        int count;reasons.TryGetValue(reason,out count);reasons[reason]=count+1;
                        if(path==null){longest=Math.Max(longest,++blank);if(frame>=30)stableBlanks++;if(wasShown)gaps++;wasShown=false;continue;}
                        blank=0;shown++;wasShown=true;
                        Require(path.Identity.Slot==slot && ReferenceEquals(path.Identity.Token,Main.npc[slot]),"Published result belongs to the actual selected target.");
                        Require(path.SampleTick==Main.GameUpdateCount && path.Count==121 && path.CaptureTick<=path.SampleTick,"Production preserves source age and 120 future updates.");
                    }
                    Console.WriteLine("POST-DELIVERY type="+type+" mounted="+mounted+" motion="+motion+" selected="+selected+" shown="+shown+" stable-blanks="+stableBlanks+" gaps="+gaps+" longest="+longest+" from="+begin+" to="+player.position+" minY="+minY+" maxX="+maxXSpeed+" maxY="+maxYSpeed+" velocity="+player.velocity);
                    foreach(var pair in reasons.OrderByDescending(p=>p.Value))Console.WriteLine("POST-REASON frames="+pair.Value+" "+pair.Key);
                    if(mounted && motion=="hover")Require(player.position.Y<2000 && maxYSpeed>0,"Broom really remains airborne in its tiny-velocity hover branch.");
                    if(mounted && motion=="right")Require(maxXSpeed>4,"Original held input really accelerates the airborne broom.");
                    if(mounted && motion=="brake")Require(startSpeed>4 && player.velocity.X==0 && player.position.X>begin.X,"Release really brakes an airborne moving broom, before any world border.");
                    if(mounted && motion=="up")Require(player.position.Y<begin.Y-100,"Held up produces real ascent.");
                    if(mounted && motion=="down")Require(player.position.Y>begin.Y+100,"Held down produces real descent.");
                    // A stable same-target input must not periodically revoke
                    // the whole result after its bounded initial transition.
                    if(selected!=120 || stableBlanks!=0)failures++;
                }
            }
            Require(failures==0,"Post-delivery production continuity failed windows="+failures);
        }
        private static bool mountsReady;
        private static void InitializeMount()
        {if(mountsReady)return;bool dedicated=Main.dedServ;int network=Main.netMode;try{Main.dedServ=true;Main.netMode=2;Mount.Initialize();mountsReady=true;}finally{Main.dedServ=dedicated;Main.netMode=network;}}
        internal static void FlightWorld()
        {
            Main.maxTilesX=400;Main.maxTilesY=240;Main.rightWorld=6400;Main.bottomWorld=3840;Main.worldSurface=140;Main.rockLayer=180;Main.tile=new Tile[400,240];
            for(int x=0;x<400;x++)for(int y=0;y<240;y++){var tile=new Tile();if(y>=150){tile.active(true);tile.type=1;}Main.tile[x,y]=tile;}
        }
        private static object Get(object owner,string name)
        {var field=owner.GetType().GetField(name,Flags);return field!=null?field.GetValue(owner):owner.GetType().GetProperty(name,Flags).GetValue(owner);}
        private static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    }
}
