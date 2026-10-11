using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Separate existing AI82/85 consumers from the certified 77/78/79 loop.
    // Native current-slot forks and default Source natural paths have their
    // own denominators; no extra native AI is invoked during the natural run.
    internal static class NativeCombatMoonLeechChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Type Model {get{return typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcMoonLeechMotion",true);}}
        private static Action<NPC> before,after;
        private static Action<Projectile> projectileBefore,projectileAfter;
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static void ProjectileBefore(Projectile __instance){projectileBefore?.Invoke(__instance);}
        private static void ProjectileAfter(Projectile __instance){projectileAfter?.Invoke(__instance);}
        private static bool Network(int msgType,int number){return msgType!=27 && msgType!=29 && !(msgType==113 && number==1);}
        private sealed class Outlets:IDisposable
        {
            private readonly Harmony patch=new Harmony("JueMingR.Probe.MoonLeechOutlets");
            internal Outlets(){var method=new HarmonyMethod(typeof(NativeCombatMoonLeechChecks).GetMethod(nameof(Network),Flags));method.priority=Priority.First+100;patch.Patch(typeof(NetMessage).GetMethod("SendData",Flags),prefix:method);}
            public void Dispose(){patch.UnpatchAll(patch.Id);}
        }
        private static bool Step(ref NpcMotionState n,NpcMotionState[] group,PredictionEnvironment e,IPredictionTerrain terrain,out PredictionStop stop)
        {object[] args={n,e,terrain,1,PredictionStop.None,group,group.Length,false};bool ok=(bool)Model.GetMethod("Step",Flags).Invoke(null,args);n=(NpcMotionState)args[0];stop=(PredictionStop)args[4];return ok;}
        private static object End(Projectile q,NpcMotionState[] group,PredictionEnvironment e,PredictionPlayerMotion player,IPredictionTerrain terrain,out PredictionStop stop)
        {
            e.MoonLeeches=Snapshot(q,group);object work=Model.GetMethod("CreateLeeches",Flags).Invoke(null,new object[]{e});
            object[] args={work,group,group.Length,e,player,true,false,1,terrain,PredictionStop.None};
            Require((bool)Model.GetMethod("EndFrame",Flags).Invoke(null,args),"Complete original456 input proceeds stop="+args[9]);stop=(PredictionStop)args[9];return ((Array)work).GetValue(0);
        }
        internal static void Boundaries(object context)
        {
            Setup();var io=new Inputs(context);var head=Actor(20,396,new Vector2(1900,1500));head.ai[3]=16;
            var clot=Actor(25,401,new Vector2(1900,2100));clot.ai[0]=head.whoAmI+1;
            var state=io.Capture(clot);var group=io.Group(clot);PredictionStop stop;
            state.MoonRelationsComplete=false;Require(!Step(ref state,group,Environment(),io.Terrain,out stop) && stop==PredictionStop.MissingDependency,"Unknown relation is not a fabricated anchor.");
            state=io.Capture(clot);state.MoonLeechAnchorValid=false;var pose=state.Bounds;Require(Step(ref state,group,Environment(),io.Terrain,out stop) && state.X==pose.X && state.Y==pose.Y && state.Vx==0 && state.Vy==0,"Observed missing packed key preserves native zero-V pose.");
            state=io.Capture(clot);state.PositionOwnerValid=false;Require(!Step(ref state,group,Environment(),io.Terrain,out stop) && stop==PredictionStop.Despawn,"Observed active/type-invalid encoded head retires.");
            // Packed generation bits can legally look like a NaN float. Their
            // identity is the integer bit pattern, never float equality/cast.
            var key=(ProjectileKey)unchecked((int)0x7fc000ff);clot.ai[1]=key;state=io.Capture(clot);
            Require(state.MoonLeechKey==(int)key && float.IsNaN(state.A1),"Source preserves packed key bit identity.");
            state=io.Capture(clot);state.MoonLeechAnchorCaptured=false;Require(!Step(ref state,group,Environment(),io.Terrain,out stop) && stop==PredictionStop.MissingDependency,"Uncaptured anchor is distinct from an observed absent key.");
            Console.WriteLine("PASS MoonLeech typed key / head / missing-anchor boundaries5");
        }
        internal static void Lifecycle(object context)
        {
            var io=new Inputs(context);int clotActions=0,projectileActions=0;
            using(var outlets=new Outlets())
            {
                foreach(int clock in new[]{0,1,44,88,89})foreach(int mode in new[]{0,1})
                {
                    Setup();Main.netMode=mode;var core=Actor(16,398,new Vector2(1800.125f,1900.375f));core.localAI[3]=1;
                    var left=Actor(18,397,new Vector2(1500,1700));left.ai[3]=16;left.ai[2]=0;
                    var right=Actor(19,397,new Vector2(2200,1700));right.ai[3]=16;right.ai[2]=1;
                    var head=Actor(20,396,new Vector2(1800.875f,1500.25f));head.ai[3]=16;
                    var q=ProjectileBirth(head,head.Center+new Vector2(129.625f,215.875f));q.velocity=new Vector2(3.125f,-1.25f);
                    var clot=Actor(25,401,new Vector2(2000.625f,1700.875f));clot.ai[0]=head.whoAmI+1;clot.ai[1]=q.key;clot.ai[2]=clock;
                    head.life-=120;core.life-=900;left.life-=70;right.life-=100;
                    var group=io.Group(clot);var state=io.Capture(clot);PredictionStop stop;bool ok=Step(ref state,group,Environment(),io.Terrain,out stop);clot.AI();
                    Require(ok || stop==PredictionStop.Despawn,"Original clot scalar has complete group input.");
                    Require(state.Active==clot.active && state.A2==clot.ai[2] && (!clot.active || state.X==clot.position.X && state.Y==clot.position.Y && state.Vx==clot.velocity.X && state.Vy==clot.velocity.Y),"Original AI82 pose/retire clock="+clock+" mode="+mode);
                    foreach(var row in group)if(!ReferenceEquals(row.Identity.Token,clot))Require(row.Life==((NPC)row.Identity.Token).life,"Four deficits snapshot before exact1000 heal ordering.");clotActions++;
                }
                // Raw core may be inactive/foreign or even alias the head.
                // Native still reads that slot and snapshots all deficits first.
                foreach(int alias in new[]{0,1})
                {
                    Setup();var raw=Actor(16,alias==0?1:396,new Vector2(1800,1700));raw.active=alias!=0;var head=alias==0?Actor(20,396,new Vector2(1800,1500)):raw;head.ai[3]=raw.whoAmI;
                    var left=Actor(18,397,new Vector2(1600,1700));left.ai[3]=raw.whoAmI;left.ai[2]=0;var right=Actor(19,397,new Vector2(2000,1700));right.ai[3]=raw.whoAmI;right.ai[2]=1;
                    head.life-=100;raw.life-=50;left.life-=200;right.life-=800;
                    var clot=Actor(25,401,new Vector2(1900,2100));clot.ai[0]=head.whoAmI+1;clot.ai[2]=89;var group=io.Group(clot);var state=io.Capture(clot);PredictionStop stop;Require(!Step(ref state,group,Environment(),io.Terrain,out stop) && stop==PredictionStop.Despawn,"Known raw heal completes.");clot.AI();
                    foreach(var row in group)if(!ReferenceEquals(row.Identity.Token,clot))Require(row.Life==((NPC)row.Identity.Token).life,"Raw/inactive/alias core keeps original preheal deficits.");clotActions++;
                }
                for(int variant=0;variant<14;variant++)
                {
                    Setup();var head=Actor(20,396,new Vector2(1900.625f,1500.875f));head.ai[3]=16;
                    var q=ProjectileBirth(head,new Vector2(2000.125f,2100.375f));q.velocity=new Vector2(3.125f,-1.25f);
                    var clot=Actor(25,401,new Vector2(1900,2100));clot.ai[0]=head.whoAmI+1;clot.ai[1]=q.key;
                    if(variant>=1 && variant<=4)for(int x=124;x<=126;x++)for(int y=130;y<=132;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(variant-1);}
                    if(variant==5){q.ai[0]*=-1;q.position=head.Center+new Vector2(0,216)-q.Size/2;}
                    if(variant==6)q.localAI[0]=329;
                    if(variant==7){Main.LocalPlayer.active=false;}
                    if(variant==8)q.position.X=Main.leftWorld;
                    if(variant==9)q.timeLeft=1;
                    if(variant==10)q.penetrate=0;
                    if(variant==11){q.extraUpdates=2;q.wet=true;}
                    if(variant==12){q.ai[0]*=-1;q.position=new Vector2(3000.875f,1700.25f);}
                    if(variant==13){Main.netMode=1;q.localAI[0]=329;}
                    var group=io.Group(clot);var e=Environment();e.PlayerTimelineActive=true;var p=io.Player();PredictionStop stop;io.Terrain.Reset();object work=End(q,group,e,p,io.Terrain,out stop);q.Update(q.whoAmI);
                    Require((bool)Get(work,"Active")==q.active,"Original456 full Update retirement variant="+variant);
                    if(q.active)Require((float)Get(work,"X")==q.position.X && (float)Get(work,"Y")==q.position.Y && (float)Get(work,"Vx")==q.velocity.X && (float)Get(work,"Vy")==q.velocity.Y && (float)Get(work,"A0")==q.ai[0] && (float)Get(work,"L0")==q.localAI[0] && (float)Get(work,"L1")==q.localAI[1] && (int)Get(work,"TimeLeft")==q.timeLeft && (bool)Get(work,"Wet")==q.wet,"Original456 full Update exact motor/wet/subupdate clock variant="+variant+" pred="+Get(work,"X")+","+Get(work,"Y")+" native="+q.position);
                    projectileActions++;
                }
            }
            Setup();Console.WriteLine("PASS MoonLeech original AI82 actions="+clotActions+" full456Update actions="+projectileActions+"; no repeated controller69/3000 matrix");
        }
        private sealed class Frozen {internal NpcTrajectory Path;internal NPC Actor;internal Frozen(NpcTrajectory path,NPC actor){Path=path;Actor=actor;}}
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            Setup();var io=new Inputs(context);var p=Main.LocalPlayer;p.position=new Vector2(2400,2400-p.height);p.ResetFallDamage();
            int coreSlot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),2200,1850,398,Start:16,Target:Main.myPlayer);var core=Main.npc[coreSlot];
            NativeCombatObservationChecks.Save(io.Host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
            int clotActions=0,projectileActions=0,births=0,heals=0,selected=0,published=0,matches=0,longMatches=0,headMatches=0;double errorMax=0;var born=new HashSet<NpcIdentity>();var freezes=new List<Frozen>();
            var rows=new List<string>{"frame,clots,births,heals,selected,published,matched,long,headMatched,projectileActions,error"};
            NpcMotionState expected=default(NpcMotionState);NpcMotionState[] currentGroup=null;NPC pending=null;PredictionStop actionStop=PredictionStop.None;bool valid=false;object projectileWork=null;Projectile pendingProjectile=null;
            var observer=new Harmony("JueMingR.Probe.MoonLeechOriginalActions");using(var outlets=new Outlets())try
            {
                observer.Patch(typeof(NPC).GetMethod("UpdateNPC",Flags),prefix:new HarmonyMethod(typeof(NativeCombatMoonLeechChecks),nameof(Before)),postfix:new HarmonyMethod(typeof(NativeCombatMoonLeechChecks),nameof(After)));
                observer.Patch(typeof(Projectile).GetMethod("Update",Flags),prefix:new HarmonyMethod(typeof(NativeCombatMoonLeechChecks),nameof(ProjectileBefore)),postfix:new HarmonyMethod(typeof(NativeCombatMoonLeechChecks),nameof(ProjectileAfter)));
                before=n=>{pending=null;if(!n.active || n.type!=401)return;pending=n;io.Terrain.Reset();currentGroup=io.Group(n);expected=io.Capture(n);var e=Environment();e.PlayerTimelineActive=true;valid=NpcMotion.Step(ref expected,currentGroup,currentGroup.Length,e,io.Terrain,1,true,out actionStop);Require(valid || actionStop==PredictionStop.Despawn,"Actual already-born401 current group has complete native input: "+actionStop);};
                after=n=>{if(!ReferenceEquals(n,pending))return;Require(expected.Active==n.active && (!n.active || expected.X==n.position.X && expected.Y==n.position.Y && expected.Vx==n.velocity.X && expected.Vy==n.velocity.Y),"Natural401 current fullStep pose/retire agrees.");if(!n.active && n.ai[2]>=90){foreach(var row in currentGroup)if(!ReferenceEquals(row.Identity.Token,n))Require(row.Life==((NPC)row.Identity.Token).life,"Natural401 original heal affects exact slot life before later NPC actions.");heals++;}clotActions++;};
                projectileBefore=q=>{pendingProjectile=null;if(!q.active || q.type!=456)return;NPC clot=null;foreach(var n in Main.npc)if(n.active && n.type==401 && (int)(ProjectileKey)n.ai[1]==(int)q.key){clot=n;break;}if(clot==null)return;io.Terrain.Reset();var group=io.Group(clot);var e=Environment();e.PlayerTimelineActive=true;PredictionStop stop;projectileWork=End(q,group,e,io.Player(),io.Terrain,out stop);pendingProjectile=q;};
                projectileAfter=q=>{if(!ReferenceEquals(q,pendingProjectile))return;Require((bool)Get(projectileWork,"Active")==q.active && (!q.active || (float)Get(projectileWork,"X")==q.position.X && (float)Get(projectileWork,"Y")==q.position.Y && (float)Get(projectileWork,"Vx")==q.velocity.X && (float)Get(projectileWork,"Vy")==q.velocity.Y && (int)Get(projectileWork,"TimeLeft")==q.timeLeft),"Natural456 full Update uses this frame's real PC and completed NPC group.");projectileActions++;};
                for(int frame=0;frame<1020;frame++)
                {
                    NPC head=null,clot=null;foreach(var n in Main.npc){if(n.active && n.type==396 && n.ai[3]==coreSlot)head=n;if(n.active && n.type==401){if(born.Add(io.Capture(n).Identity))births++;if(clot==null || n.whoAmI<clot.whoAmI)clot=n;}}
                    if(frame%120==0){p.controlRight=frame%240==0;p.controlLeft=!p.controlRight;freezes.Clear();}
                    var actor=clot??head??core;NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();Require(core.active && p.active && !p.dead,"Natural Moon leech prerequisites stay alive without skipped native actors.");
                    var path=cache.Read(0);if(path!=null && ReferenceEquals(path.Identity.Token,actor)){if(actor.type==401)selected++;published++;if(frame%7==0 && path.Count>1)freezes.Add(new Frozen(path,actor));}
                    for(int i=freezes.Count-1;i>=0;i--){var f=freezes[i];int offset=(int)(Main.GameUpdateCount-f.Path.CaptureTick);if(offset>=f.Path.Count){freezes.RemoveAt(i);continue;}if(offset<1)continue;var point=f.Path[offset];double error=Vector2.Distance(f.Actor.position,new Vector2(point.Bounds.X,point.Bounds.Y));errorMax=Math.Max(errorMax,error);Require(error<=.08 && point.CanReceive==(!f.Actor.dontTakeDamage && !f.Actor.immortal),"Natural existing401/head frozen Source path error="+error.ToString("R")+" type="+f.Actor.type+" frame="+frame+" offset="+offset);matches++;if(offset>=30)longMatches++;if(f.Actor.type==396)headMatches++;}
                    rows.Add(string.Join(",",frame,CountClots(),births,heals,selected,published,matches,longMatches,headMatches,projectileActions,errorMax.ToString("R")));
                }
                Require(births>=3 && heals>=3 && clotActions>=90 && projectileActions>=30 && selected>0 && longMatches>=30 && headMatches>0,"Real head NewNPC birth causes456 approach, buff145,401 movement/heal, default selected clot and receiving-head long paths.");
                Console.WriteLine("PASS MoonLeech one actual head cycle births="+births+" heals="+heals+" clotActions="+clotActions+" projectileActions="+projectileActions+" selected="+selected+" matched="+matches+" long="+longMatches+" headMatched="+headMatches+" maxError="+errorMax);
            }
            finally{before=after=null;projectileBefore=projectileAfter=null;observer.UnpatchAll(observer.Id);Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"moon-leech-continuous.csv"),rows);}
        }
        private static int CountClots(){int n=0;foreach(var row in Main.npc)if(row.active && row.type==401)n++;return n;}
        private static NPC Actor(int slot,int type,Vector2 position){var n=Main.npc[slot]=new NPC{whoAmI=slot};n.SetDefaults(type);n.active=true;n.target=Main.myPlayer;n.position=position;return n;}
        private static Projectile ProjectileBirth(NPC head,Vector2 center){int slot=Projectile.NewProjectile(head.GetSpawnSource_ForProjectile(),center,Vector2.Zero,456,0,0,Main.myPlayer,head.whoAmI+1,Main.myPlayer);return Main.projectile[slot];}
        private static void Setup(){NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.dayTime=false;Main.getGoodWorld=false;Main.remixWorld=false;Main.Map=new Terraria.Map.WorldMap(Main.maxTilesX,Main.maxTilesY);for(int i=0;i<255;i++)Main.player[i].active=false;var p=Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};p.position=new Vector2(2400.125f,2400-p.height);p.ResetFallDamage();p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.immune=true;p.immuneTime=1000000;for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;}
        private static PredictionEnvironment Environment(){var p=Main.LocalPlayer;return new PredictionEnvironment{PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,GravityWorldSurface=Main.worldSurface,Multiplayer=Main.netMode==1,Expert=Main.expertMode};}
        private static PredictionMoonLeeches Snapshot(Projectile q,NpcMotionState[] group)
        {int slot=(int)Math.Abs(q.ai[0])-1;NpcIdentity id=default(NpcIdentity);foreach(var row in group)if(row.Identity.Slot==slot){id=row.Identity;break;}var p=Main.player[(int)q.ai[1]];var f=new PredictionMoonLeech(q,q.whoAmI,(int)q.key,q.owner,q.timeLeft,q.extraUpdates,q.penetrate,true,id,true,p,p.whoAmI,new MotionRect(p.position.X,p.position.Y,p.width,p.height),p.active,p.dead,p.creativeGodMode,q.owner==255 || Main.player[q.owner].active,new MotionRect(q.position.X,q.position.Y,q.width,q.height),q.velocity.X,q.velocity.Y,q.ai[0],q.localAI[0],q.localAI[1],true,q.ignoreWater,q.wet);return new PredictionMoonLeeches(new[]{f},1,Main.leftWorld,Main.rightWorld,Main.topWorld,Main.bottomWorld);}
        private sealed class Inputs
        {
            internal readonly object Host,Source;internal readonly IPredictionTerrain Terrain;private readonly long session;private readonly MethodInfo read,tracking,player;
            internal Inputs(object context){Host=Get(context,"CombatObservation");Source=Get(Host,"Prediction");Terrain=(IPredictionTerrain)Get(Source,"Terrain");session=(long)Get(Host,"Session");read=Source.GetType().GetMethod("Read",Flags);tracking=Source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);player=Source.GetType().GetMethod("ReadPlayer",Flags);}
            internal NpcMotionState Capture(NPC n){var s=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,s,Terrain};tracking.Invoke(null,args);s=(NpcMotionState)args[1];s.MoonRelationsCaptured=s.MoonRelationsComplete=s.MoonEffectsCaptured=true;s.MoonExternalEffectAt=s.MoonGlobalRetireAt=int.MaxValue;return s;}
            internal PredictionPlayerMotion Player(){return (PredictionPlayerMotion)player.Invoke(null,new object[]{Main.LocalPlayer});}
            internal NpcMotionState[] Group(NPC clot){var values=new List<NpcMotionState>();int raw=-1,head=(int)Math.Abs(clot.ai[0])-1;if(head>=0 && head<Main.maxNPCs)raw=(int)Main.npc[head].ai[3];for(int i=0;i<Main.maxNPCs;i++){var n=Main.npc[i];if(n!=null && (n.active && (n.type==398 || n.type==397 || n.type==396 || n.type==401) || i==raw))values.Add(Capture(n));}return values.ToArray();}
        }
    }
}
