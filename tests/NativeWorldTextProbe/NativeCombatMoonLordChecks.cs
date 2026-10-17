using System;
using System.Collections.Generic;
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
    // CPU controller coverage and real death graphics use separate entries
    // and denominators. Current-slot forks never replace default Source proof.
    internal static class NativeCombatMoonLordChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Action<NPC> before,after;
        private static int isolatedMoonMessages;
        private static bool HeadOnly()
        {
            string role=System.Environment.GetEnvironmentVariable("JMR_MOON_LORD_NATURAL_ROLE");
            Require(string.IsNullOrEmpty(role) || role=="all" || role=="head","JMR_MOON_LORD_NATURAL_ROLE must be all or head.");
            return role=="head";
        }
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static bool IsolateMoonProjectileMessage(int msgType,int number)
        {
            if(Main.netMode!=0 || msgType!=27 || number<0 || number>=Main.projectile.Length)return true;
            var q=Main.projectile[number];if(q==null || !q.active || q.type!=454)return true;
            int slot=(int)q.ai[1];if(slot<0 || slot>=Main.npc.Length || Main.npc[slot]==null || Main.npc[slot].type!=397)return true;
            isolatedMoonMessages++;return false;
        }
        private sealed class NetworkOutlet:IDisposable
        {
            private readonly Harmony patch=new Harmony("jmr.moon.original.projectile.outlet");
            internal NetworkOutlet(){var p=new HarmonyMethod(typeof(NativeCombatMoonLordChecks).GetMethod(nameof(IsolateMoonProjectileMessage),Flags));p.priority=Priority.First+100;patch.Patch(typeof(NetMessage).GetMethod("SendData",Flags),prefix:p);}
            public void Dispose(){patch.UnpatchAll(patch.Id);}
        }
        private static Type Model {get{return typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcMoonLordMotion",true);}}
        private static bool ModelStep(ref NpcMotionState n,NpcMotionState[] group,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop)
        {object[] args={n,e,terrain,elapsed,PredictionStop.None,group,group?.Length??0,false};bool ok=(bool)Model.GetMethod("Step",Flags).Invoke(null,args);n=(NpcMotionState)args[0];stop=(PredictionStop)args[4];return ok;}
        private static NpcMotionState State(int type,int slot)
        {return new NpcMotionState{Identity=new NpcIdentity(1,new object(),slot,1,type,type),Style=type==398?77:type==397?78:79,Active=true,Life=100,LifeMax=100,Width=type==396?38:46,Height=type==396?56:66,X=1800,Y=1800,L3=1,NoGravity=true,NoTileCollide=true,MoonRelationsCaptured=true,MoonRelationsComplete=true,MoonEffectsCaptured=true,MoonExternalEffectAt=int.MaxValue,MoonGlobalRetireAt=int.MaxValue,MoonHandApertureCaptured=type==397};}
        internal static void Boundaries()
        {
            if(HeadOnly()){Console.WriteLine("MoonLord head-only: ownership/scalar evidence reused from fixed R39b; boundary cases not rerun.");return;}
            PredictionStop stop;var owner=State(398,16);owner.A0=-2;var hand=State(397,20);hand.PositionOwnerCaptured=true;hand.PositionOwnerValid=false;
            Require(!ModelStep(ref hand,new[]{owner,hand},new PredictionEnvironment(),null,1,out stop) && stop==PredictionStop.Despawn,"Known invalid native hand owner retires, rather than unknown trend.");
            hand=State(397,20);hand.PositionOwnerCaptured=hand.PositionOwnerValid=true;hand.PositionOwner=owner.Identity;owner.Life=0;hand.A0=-2;Require(ModelStep(ref hand,new[]{owner,hand},new PredictionEnvironment(),null,1,out stop),"Active zero-life type398 remains a valid native owner.");
            hand.PositionOwner=new NpcIdentity(1,owner.Identity.Token,16,2,398,398);Require(!ModelStep(ref hand,new[]{owner,hand},new PredictionEnvironment(),null,1,out stop) && stop==PredictionStop.MissingDependency,"Reused owner generation cannot replace captured identity.");
            hand=State(397,20);hand.PositionOwnerCaptured=hand.PositionOwnerValid=true;hand.PositionOwner=owner.Identity;hand.A0=-2;hand.MoonHandApertureCaptured=false;Require(!ModelStep(ref hand,new[]{owner,hand},new PredictionEnvironment(),null,1,out stop) && stop==PredictionStop.MissingDependency,"Receiving aperture is an actual required scalar.");
            hand=State(397,20);hand.PositionOwnerCaptured=hand.PositionOwnerValid=true;hand.PositionOwner=owner.Identity;hand.Health.Immortal=true;Require(ModelStep(ref hand,new[]{owner,hand},new PredictionEnvironment(),null,1,out stop) && !hand.CanReceive && !hand.Health.DontTakeDamage,"Native open aperture does not remove an independently captured immortal receive gate.");
            var core=State(398,16);core.L3=0;Require(ModelStep(ref core,new[]{core},new PredictionEnvironment(),null,1,out stop) && core.A0==-1 && core.A1==1 && !core.CanReceive,"First native intro action keeps a known deterministic prefix.");
            core.A1=59;Require(!ModelStep(ref core,new[]{core},new PredictionEnvironment(),null,1,out stop) && stop==PredictionStop.MissingDependency,"Actual server child-birth identities must be observed before their same-action consumer.");
            core=State(398,16);core.A0=3;core.A1=59;hand=State(397,20);Require(!ModelStep(ref core,new[]{core,hand},new PredictionEnvironment(),null,1,out stop) && stop==PredictionStop.Despawn,"Observed withdrawal reaches true native retirement.");
            hand=State(397,20);hand.MoonExternalEffectAt=2;Require(!ModelStep(ref hand,new[]{owner,hand},new PredictionEnvironment(),null,2,out stop) && stop==PredictionStop.PhaseBoundary,"Known existing heal/explosive effect ends before its true consumer.");
            Console.WriteLine("PASS MoonLord ownership/intro/retire/effect boundaries; no claim of AI81/82 completion");
        }
        internal static void Lifecycle(object context)
        {
            if(HeadOnly()){Console.WriteLine("MoonLord head-only: 69 controller scalar + 4 actual heal actions reused from fixed R39b; lifecycle not rerun.");return;}
            using(var network=new NetworkOutlet())
            {
            var io=new Inputs(context);int cases=0;
            // Native scalar forks compare only path/receive/lifecycle facts.
            // Pupil-angle and dust state are not added to the model as a test aid.
            // Role-specific absolute clocks hit the same natural phase2
            // local-time consumers (30/210/282/287/292/300) on both hands.
            foreach(int side in new[]{0,1})foreach(int clock in side==0
                ?new[]{0,49,50,119,120,149,150,329,330,401,402,406,407,411,412,419,420,449,450,509,510,599}
                :new[]{0,69,70,119,120,209,210,269,270,299,300,479,480,551,552,556,557,561,562,569,570,599})
            {
                World();var core=Actor(16,398,new Vector2(1800.125f,1900.25f));core.localAI[3]=1;var n=Actor(20,397,new Vector2(side==0?1350.125f:2200.625f,1700.875f));n.ai[3]=16;n.ai[2]=side;n.ai[1]=clock;n.velocity=new Vector2(-2.375f,1.125f);n.frameCounter=clock%3==0?21:clock%3==1?19:0;Compare(io,n);cases++;
            }
            foreach(int clock in new[]{0,179,180,209,210,644,645,824,825,1004,1005,1007,1184,1185,1186,1199})
            {
                World();var core=Actor(16,398,new Vector2(4096.125f,1900.375f));core.localAI[3]=1;var n=Actor(20,396,new Vector2(4100,1500));n.ai[3]=16;n.ai[1]=clock;n.localAI[3]=clock%2==0?15:13;Compare(io,n);cases++;
            }
            foreach(float phase in new[]{-2,-1,2,3})
            {
                World();var n=Actor(16,398,new Vector2(1800,1900));n.localAI[3]=1;n.ai[0]=phase;n.ai[1]=phase==3?58:0;n.velocity=new Vector2(2.375f,-1.125f);Compare(io,n);cases++;
            }
            foreach(int variant in new[]{0,1,2,3,4})
            {
                World();var n=Actor(16,398,new Vector2(1800.125f,1900.375f));n.localAI[3]=1;n.ai[0]=variant==0?0:1;n.localAI[0]=20;n.localAI[1]=21;n.localAI[2]=22;
                foreach(int childSlot in new[]{20,21,22}){var child=Actor(childSlot,childSlot==22?396:397,new Vector2(1800,1700));child.ai[3]=16;child.ai[0]=variant==0?-2:0;}
                if(variant==1)Main.LocalPlayer.Center=n.Center+new Vector2(0,-130);
                if(variant==2)n.velocity=Main.LocalPlayer.Center-n.Center+new Vector2(0,130);
                if(variant==3)Main.LocalPlayer.Center=n.Center+new Vector2(2400,0);
                if(variant==4)Main.LocalPlayer.Center=n.Center+new Vector2(2400.125f,0);
                Compare(io,n);cases++;
            }
            SourceEffects(io);
            Console.WriteLine("PASS MoonLord CPU original scalar actions="+cases+" isolatedOriginalProjectileOutlets="+isolatedMoonMessages+"; final death texture consumer intentionally belongs to DeathGraphics");
            }
        }
        private static void Compare(Inputs io,NPC n)
        {
            io.Terrain.Reset();var group=io.CurrentGroup();var state=io.Capture(n);var sampler=state;
            if(n.type!=398){object[] sampleArgs={sampler};io.Source.GetType().GetMethod("MoonSampleAperture",Flags).Invoke(null,sampleArgs);sampler=(NpcMotionState)sampleArgs[0];}
            PredictionStop stop;bool ok=ModelStep(ref state,group,Environment(),io.Terrain,1,out stop);Require(ok || stop==PredictionStop.Despawn,"Original controller scalar must have complete current fixture input stop="+stop);n.AI();
            Require(state.Active==n.active && (!n.active || state.Vx==n.velocity.X && state.Vy==n.velocity.Y && state.X==n.position.X && state.Y==n.position.Y && state.CanReceive==(!n.dontTakeDamage && !n.immortal) && state.A0==n.ai[0] && state.A1==n.ai[1] && (n.type!=397 || state.MoonHandAperture==n.frameCounter) && (n.type!=396 || state.L3==n.localAI[3])),
                "Original MoonLord scalar type="+n.type+" phase="+string.Join("/",n.ai)+" predPose="+state.X.ToString("R")+","+state.Y.ToString("R")+" nativePose="+n.position+" predV="+state.Vx.ToString("R")+","+state.Vy.ToString("R")+" nativeV="+n.velocity+" receive="+state.CanReceive+"/"+!n.dontTakeDamage+" aperture="+state.MoonHandAperture+"/"+n.frameCounter);
            if(n.type!=398)Require(sampler.Health.DontTakeDamage==n.dontTakeDamage && (n.type==397?sampler.MoonHandAperture==n.frameCounter:sampler.L3==n.localAI[3]),"Source effect-clock projection preserves actual aperture consumer type="+n.type);
        }
        private static void SourceEffects(Inputs io)
        {
            int actions=0;
            foreach(int clotSlot in new[]{10,25})foreach(int mode in new[]{0,1})
            {
                World();Main.netMode=mode;var core=Actor(16,398,new Vector2(1800,1900));core.localAI[3]=1;core.life-=100;
                var left=Actor(20,397,new Vector2(1400,1800));left.ai[3]=16;left.ai[2]=0;var right=Actor(21,397,new Vector2(2200,1800));right.ai[3]=16;right.ai[2]=1;var head=Actor(22,396,new Vector2(1800,1500));head.ai[3]=16;
                var clot=Actor(clotSlot,401,new Vector2(1800,1500));clot.ai[0]=23;clot.ai[2]=89;
                int first=(int)io.Source.GetType().GetMethod("FirstMoonLeechConsumer",Flags).Invoke(null,new object[]{core,120});
                Require(first==(mode==1?int.MaxValue:clotSlot<core.whoAmI?1:2),"Actual clot heal deadline respects server/client and native slot order.");
                int prior=core.life;clot.AI();Require(!clot.active && core.life==(mode==1?prior:prior+100),"Actual AI82 heal consumer runs once without its earlier moving-anchor path being claimed.");actions++;
            }
            World();Console.WriteLine("PASS MoonLord existing-heal original actions="+actions+"; moving401/456 remains a separate unresolved mechanism");
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            bool headOnly=HeadOnly();int frameCount=headOnly?1260:3000;
            Console.WriteLine("MoonLord natural role="+(headOnly?"head":"all")+" frames="+frameCount+"; actual NewNPC intro included, selected head cycle="+(headOnly?1200:400));
            var io=new Inputs(context);World();Main.rand=new Terraria.Utilities.UnifiedRandom(770078);var p=Main.LocalPlayer;p.position=new Vector2(2400,2400-p.height);p.ResetFallDamage();p.isControlledByFilm=true;p.releaseJump=true;p.immune=true;p.immuneTime=1000000;p.statLife=p.statLifeMax=p.statLifeMax2=5000;for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),2200,1850,398,Start:16,Target:Main.myPlayer);var core=Main.npc[slot];var children=new List<NPC>();NPC selected=null;var frozen=new List<Frozen>();int matches=0,longMatches=0,actions=0,openClosed=0,hits=0,coreReceiving=0,restored=0,selectedActions=0;var phaseMatches=new int[4];var phaseLong=new int[4];var phases=new HashSet<string>();var rows=new List<string>{"frame,type,phase,receiving,count,stop,matches,long,pathType,pathSlot"};bool waiting=false,valid=false;long waitingTick=-1;int waitingCount=0;PredictionStop waitingStop=PredictionStop.None;var predicted=default(NpcMotionState);var oracle=new Harmony("JueMingR.Tests.MoonControllerOriginalSlot");
            var network=new NetworkOutlet();oracle.Patch(typeof(NPC).GetMethod("UpdateNPC",Flags),prefix:new HarmonyMethod(typeof(NativeCombatMoonLordChecks),nameof(Before)),postfix:new HarmonyMethod(typeof(NativeCombatMoonLordChecks),nameof(After)));
            NativeCombatObservationChecks.Save(io.Host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
            try
            {
                before=n=>{valid=false;if(!ReferenceEquals(n,selected))return;io.Terrain.Reset();predicted=io.Capture(n);PredictionStop stop;var e=Environment();e.PlayerTimelineActive=true;var group=io.CurrentGroup();valid=NpcMotion.Step(ref predicted,group,group.Length,e,io.Terrain,1,true,out stop);Require(valid || stop==PredictionStop.PhaseBoundary || stop==PredictionStop.MissingDependency || stop==PredictionStop.Despawn,"Explicit current Moon controller input boundary="+stop);};
                after=n=>{if(!ReferenceEquals(n,selected) || !valid)return;Require(predicted.Vx==n.velocity.X && predicted.Vy==n.velocity.Y && Vector2.Distance(n.position,new Vector2(predicted.X,predicted.Y))<=.002 && predicted.CanReceive==(!n.dontTakeDamage && !n.immortal),"Original current Moon controller action agrees in motion and receive aperture.");actions++;};
                for(int frame=0;frame<frameCount;frame++)
                {
                    if(children.Count==0)for(int i=0;i<Main.maxNPCs;i++){var n=Main.npc[i];if(n.active && (n.type==396 || n.type==397) && n.ai[3]==slot)children.Add(n);}
                    NPC target=headOnly?children.Find(n=>n.type==396):frame<1500?children.Find(n=>n.type==397 && n.ai[2]==0):frame<2000?children.Find(n=>n.type==397 && n.ai[2]==1):frame<2400?children.Find(n=>n.type==396):core;
                    if(!ReferenceEquals(target,selected)){frozen.Clear();selected=target;}
                    if(frame%240==0){p.controlRight=frame%480==0;p.controlLeft=!p.controlRight;frozen.Clear();}
                    // Actual body parts become -2 and spawn their actual eye.
                    // The selected core is never killed in the CPU denominator.
                    if(frame==2400){frozen.Clear();foreach(var n in children){int old=n.life;n.StrikeNPCNoInteraction(n.lifeMax*4,0,0);Require(n.active && n.ai[0]==-2 && n.life==n.lifeMax,"Real part checkDead enters nonreceiving death animation and real child birth.");hits++;}Require(hits==3,"Three real observed part deaths unlock the core.");}
                    NativeCombatModeledImpactChecks.SampleMouse(context,(selected??core).Center);step();Require(core.active && p.active && !p.dead,"CPU native controller prerequisites remain valid.");if(selected==null)continue;selectedActions++;phases.Add(selected.type+":"+selected.ai[0]);if(core.ai[0]==1 && !core.dontTakeDamage)coreReceiving++;
                    var path=cache.Read(0);if(path!=null && ReferenceEquals(path.Identity.Token,selected))
                    {
                        if(headOnly)
                        {
                            // A later unresolved consumer can still terminate
                            // a freshly recovered, independently valid prefix.
                            // Require an actual short dependency window first,
                            // followed by this same actor's NEW sampled window.
                            if(!waiting && path.Count<30 && (path.Stop==PredictionStop.PhaseBoundary || path.Stop==PredictionStop.MissingDependency))
                            {waiting=true;waitingTick=path.CaptureTick;waitingCount=path.Count;waitingStop=path.Stop;}
                            else if(waiting && path.CaptureTick>waitingTick && path.Count>=30)
                            {restored++;waiting=false;Console.WriteLine("MoonLord head prefix recovery oldTick="+waitingTick+" oldCount="+waitingCount+" oldStop="+waitingStop+" newTick="+path.CaptureTick+" newCount="+path.Count+" newStop="+path.Stop);}
                        }
                        else{if(path.Stop==PredictionStop.PhaseBoundary || path.Stop==PredictionStop.MissingDependency)waiting=true;else if(waiting && path.Count>=30){restored++;waiting=false;}}
                        if(frame%5==0 && path.Count>1)frozen.Add(new Frozen(path,selected));
                    }
                    for(int i=frozen.Count-1;i>=0;i--){var f=frozen[i];int offset=(int)((long)Main.GameUpdateCount-f.Path.CaptureTick);if(offset>=f.Path.Count){frozen.RemoveAt(i);continue;}if(offset<=0)continue;var point=f.Path[offset];double error=Vector2.Distance(f.Actor.position,new Vector2(point.Bounds.X,point.Bounds.Y));Require(error<=.08 && point.CanReceive==(!f.Actor.dontTakeDamage && !f.Actor.immortal) && (!headOnly || point.Phase==f.Actor.ai[0]),"Known conditional Moon path/receive first difference offset="+offset+" error="+error.ToString("R"));matches++;if(offset>=30)longMatches++;if(point.CanReceive!=f.Path[0].CanReceive)openClosed++;if(headOnly){int phase=(int)f.Actor.ai[0];if(phase>=0 && phase<4){phaseMatches[phase]++;if(offset>=30)phaseLong[phase]++;}}}
                    rows.Add(string.Join(",",frame,selected.type,selected.ai[0],!selected.dontTakeDamage,path?.Count??0,path?.Stop.ToString()??"none",matches,longMatches,path?.Identity.Type.ToString()??"",path?.Identity.Slot.ToString()??""));
                }
                Require(matches>=30 && longMatches>=30 && actions>=300 && openClosed>=30 && (headOnly || coreReceiving>=30),"Default selection actually consumes curved stages, receive opening/closing and the selected controller role.");
                for(int phase=0;phase<4;phase++)Require(phases.Contains((headOnly?"396:":"397:")+phase) && (!headOnly || phaseMatches[phase]>0),"Real selected attack-loop role has native and frozen phase coverage phase="+phase);
                if(headOnly){Require(selectedActions>=1200 && phaseLong[2]>=30,"Selected head completes its actual1200-cycle, including phase2 default Source long receive windows.");Console.WriteLine("MoonLord head-only selectedActions="+selectedActions+" phaseMatches="+string.Join("/",phaseMatches)+" phaseLong="+string.Join("/",phaseLong));}
                if(headOnly){Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"moon-controller.csv"),rows);Console.WriteLine("MoonLord head prefix recovery count="+restored+"; head-only CSV saved before final recovery assertion.");}
                // Closed head apertures can make default selection publish a
                // different part. This added phase leaf does not manufacture
                // a same-head dependency recovery from that unrelated window;
                // the default R39b leaf already exercised two real recoveries.
                if(headOnly && restored==0)Console.WriteLine("MoonLord head-only recovery denominator not triggered; reuse fixed R39b default two actual recoveries.");
                if(!headOnly){Require(restored>0,"A typed birth/effect consumer limit is followed by fresh observed-source recovery.");Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"moon-controller.csv"),rows);}Console.WriteLine("PASS MoonLord CPU controller matched="+matches+" long="+longMatches+" receiveCross="+openClosed+" currentActions="+actions+" actualPartHits="+hits+" coreReceiving="+coreReceiving+" restored="+restored+"; AI81/82 and final death graphics not certified");
            }
            finally{before=after=null;oracle.UnpatchAll(oracle.Id);network.Dispose();}
        }
        internal static void DeathGraphics(object context)
        {
            // Root schedules this only in an actual Content/GPU-capable leaf.
            // No skipped checkDead, ThrowPieces or simulated Loot is permitted.
            World();var io=new Inputs(context);var n=Actor(16,398,new Vector2(2000,1850));n.localAI[3]=1;n.ai[0]=2;n.ai[1]=599;var state=io.Capture(n);PredictionStop stop;Require(!ModelStep(ref state,io.CurrentGroup(),Environment(),io.Terrain,1,out stop) && stop==PredictionStop.Despawn,"Known final controller action retires.");n.AI();Require(!n.active && n.life==0,"Actual original death ran ThrowPieces and checkDead through real assets.");Console.WriteLine("PASS MoonLord real graphics final-death action; independent from CPU controller leaf");
        }
        private sealed class Frozen {internal readonly NpcTrajectory Path;internal readonly NPC Actor;internal Frozen(NpcTrajectory p,NPC actor){Path=p;Actor=actor;}}
        private sealed class Inputs
        {
            internal readonly object Host,Source;internal readonly IPredictionTerrain Terrain;private readonly long session;private readonly MethodInfo read,tracking,position;
            internal Inputs(object context){Host=Get(context,"CombatObservation");Source=Get(Host,"Prediction");Terrain=(IPredictionTerrain)Get(Source,"Terrain");session=(long)Get(Host,"Session");read=Source.GetType().GetMethod("Read",Flags);tracking=Source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);position=Source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcPositionObservation",true).GetMethod("Capture",Flags);}
            // Private original-slot oracle captures only the CURRENT facts.
            // It is not a substitute for the production Source closure above.
            internal NpcMotionState Capture(NPC n)
            {
                var s=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,s,Terrain};tracking.Invoke(null,args);s=(NpcMotionState)args[1];args=new object[]{n,s,session};position.Invoke(null,args);s=(NpcMotionState)args[1];s.MoonRelationsCaptured=s.MoonRelationsComplete=s.MoonEffectsCaptured=true;s.MoonExternalEffectAt=s.MoonGlobalRetireAt=int.MaxValue;
                s.MoonHandApertureCaptured=n.type==397;s.MoonHandAperture=n.frameCounter;
                if(n.type==398){s.MoonPart0=IdentityAt((int)n.localAI[0]);s.MoonPart1=IdentityAt((int)n.localAI[1]);s.MoonPart2=IdentityAt((int)n.localAI[2]);}
                else{s.PositionOwnerCaptured=true;int slot=(int)n.ai[3];var owner=slot>=0 && slot<Main.maxNPCs?Main.npc[slot]:null;s.PositionOwnerValid=owner!=null && owner.active && owner.type==398;if(owner!=null)s.PositionOwner=IdentityAt(slot);}
                // Current action containing an already-born external heal or
                // local attached explosive must be taken as a known effect
                // boundary. Full moving 401/456 belongs to a later typed model.
                for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type==401 && Main.npc[i].ai[2]>=89)s.MoonExternalEffectAt=1;
                for(int i=0;i<Main.maxProjectiles;i++){var q=Main.projectile[i];if(q.active && q.owner==Main.myPlayer && Terraria.ID.ProjectileID.Sets.IsAnNPCAttachedExplosive[q.type] && q.IsAttachedTo(n)){s.MoonExternalEffectAt=1;break;}}
                return s;
            }
            private NpcIdentity IdentityAt(int slot){if(slot<0 || slot>=Main.maxNPCs || Main.npc[slot]==null)return default(NpcIdentity);return ((NpcMotionState)read.Invoke(null,new object[]{Main.npc[slot],session})).Identity;}
            internal NpcMotionState[] CurrentGroup(){var list=new List<NpcMotionState>();for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && (Main.npc[i].type==398 || Main.npc[i].type==397 || Main.npc[i].type==396))list.Add(Capture(Main.npc[i]));return list.ToArray();}
        }
        private static NPC Actor(int slot,int type,Vector2 at){var n=Main.npc[slot]=new NPC{whoAmI=slot};n.SetDefaults(type);n.active=true;n.target=Main.myPlayer;n.position=at;return n;}
        private static void World(){NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.dayTime=false;Main.remixWorld=false;Main.Map=new Terraria.Map.WorldMap(Main.maxTilesX,Main.maxTilesY);Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true};Main.LocalPlayer.position=new Vector2(2400.125f,2400-Main.LocalPlayer.height);Main.LocalPlayer.ResetFallDamage();}
        private static PredictionEnvironment Environment(){var p=Main.LocalPlayer;return new PredictionEnvironment{Players=new PredictionPlayers(new[]{new MotionRect((int)p.position.X,(int)p.position.Y,p.width,p.height)},1,new[]{p.whoAmI},new[]{new MotionRect(p.position.X,p.position.Y,p.width,p.height)},new[]{p.active&&!p.dead&&!p.ghost},new[]{p.dead}),PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,PlayerVelocityCaptured=true,GravityWorldSurface=Main.worldSurface,WorldSurface=(float)Main.worldSurface,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,Day=Main.dayTime,Multiplayer=Main.netMode==1,Expert=Main.expertMode};}
    }
}
