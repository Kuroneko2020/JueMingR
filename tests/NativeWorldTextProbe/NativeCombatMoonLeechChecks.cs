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
        private static Action<NPC> before,after; private static Type moonSource;
        private static Action<Projectile> projectileBefore,projectileAfter;
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static void ProjectileBefore(Projectile __instance){projectileBefore?.Invoke(__instance);}
        private static void ProjectileAfter(Projectile __instance){projectileAfter?.Invoke(__instance);}
        private static readonly List<string> clearExports=new List<string>();
        private static bool Network(int msgType,int remoteClient,int ignoreClient,Terraria.Localization.NetworkText text,int number,float number2,float number3,float number4,int number5,int number6,int number7)
        {
            if(System.Environment.GetEnvironmentVariable("JUEMINGR_MOON_LEECH_CLEAR_ONLY")=="1" && msgType==23 && number>=0 && number<Main.maxNPCs && Main.npc[number].type>=396 && Main.npc[number].type<=401)
            {clearExports.Add(string.Join("/",msgType,remoteClient,ignoreClient,text,number,number2,number3,number4,number5,number6,number7));return false;}
            return msgType!=27 && msgType!=29 && !(msgType==113 && number==1);
        }
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
            if(System.Environment.GetEnvironmentVariable("JUEMINGR_MOON_LEECH_CLEAR_ONLY")=="1"){ClearLifecycle(context);return;}
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
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output,ProbeGraphics graphics=null)
        {
            if(System.Environment.GetEnvironmentVariable("JUEMINGR_MOON_LEECH_CLEAR_ONLY")=="1")
            {
                Require(graphics!=null,"Actual AI77 death explosions require original texture assets and a real graphics device.");
                graphics.LoadMoonDeathAssets();
                ClearContinuous(context,cache,step,output);return;
            }
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
        private static int ClearDeadline(Inputs io,out int writer)
        {
            object[] args={120,-1};int result=(int)io.Source.GetType().GetMethod("FirstMoonAnchorClear",Flags).Invoke(null,args);writer=(int)args[1];return result;
        }
        private static int Index(NpcMotionState[] group,NPC actor)
        {for(int i=0;i<group.Length;i++)if(ReferenceEquals(group[i].Identity.Token,actor))return i;return -1;}
        private static void ClearLifecycle(object context)
        {
            clearExports.Clear();
            int cases=0,clears=0,negative=0;
            var coreModel=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcMoonLordMotion",true);
            using(var outlets=new Outlets())foreach(int phase in new[]{2,3})foreach(int coreSlot in new[]{8,30})foreach(int variant in new[]{0,1,2,3})
            {
                Setup();var io=new Inputs(context);float mark=phase==2?60:40;
                var core=Actor(coreSlot,398,new Vector2(1800,1900));core.localAI[3]=1;core.ai[0]=phase;
                core.ai[1]=variant==0?mark-1:variant==1?mark:variant==2?mark+.01f:mark-.5f;
                var head=Actor(20,396,new Vector2(1800,1500));head.ai[3]=coreSlot;
                var clot=Actor(25,401,new Vector2(2000.625f,2100.875f));clot.ai[0]=head.whoAmI+1;clot.ai[2]=10;
                var q=ProjectileBirth(head,new Vector2(2250.125f,2150.875f));clot.ai[1]=q.key;
                int writer,deadline=ClearDeadline(io,out writer);
                Require(deadline==(variant==0?1:int.MaxValue) && writer==(variant==0?coreSlot:-1),"Actual exact clear clock, not >= or truncated fractional clock phase="+phase+" variant="+variant);
                var group=io.Group(clot);var e=Environment();e.PlayerTimelineActive=true;e.MoonLeeches=Snapshot(q,group);
                object work=Model.GetMethod("CreateLeeches",Flags).Invoke(null,new object[]{e});
                Action coreAction=()=>
                {
                    int index=Index(group,core);object[] args={group[index],e,io.Terrain,1,PredictionStop.None,group,group.Length,false};
                    Require((bool)coreModel.GetMethod("Step",Flags).Invoke(null,args),"Core exact current NPC write is known.");group[index]=(NpcMotionState)args[0];core.AI();
                    Require(group[index].A1==core.ai[1] && group[index].Vx==core.velocity.X && group[index].Vy==core.velocity.Y,"Original core clock/motor agrees at global clear.");
                };
                if(coreSlot<clot.whoAmI)coreAction();
                int clotIndex=Index(group,clot);var value=group[clotIndex];PredictionStop stop;
                Require(Step(ref value,group,e,io.Terrain,out stop),"Known clear never truncates live401 before its original heal.");group[clotIndex]=value;clot.AI();
                Require(value.Active==clot.active && value.X==clot.position.X && value.Y==clot.position.Y && value.Vx==0 && value.Vy==0 && value.A2==clot.ai[2],"Original401 same-frame packed-key consumer respects writer slot.");
                if(coreSlot>clot.whoAmI)coreAction();
                object[] end={work,group,group.Length,e,io.Player(),true,false,1,io.Terrain,PredictionStop.None};
                Require((bool)Model.GetMethod("EndFrame",Flags).Invoke(null,end),"Known NPC clear synchronizes private worker.");if(q.active)q.Update(q.whoAmI);
                Require((bool)Get(((Array)work).GetValue(0),"Active")==q.active && q.active==(variant!=0),"Original456 survives only when exact clear did not execute.");
                if(variant==0)
                {
                    var fresh=io.Capture(clot);var position=clot.position;Require(!fresh.MoonLeechAnchorValid,"Actual postclear source observes absent packed key.");
                    Require(Step(ref fresh,io.Group(clot),Environment(),io.Terrain,out stop),"Fresh absent-key sample preserves original static motion.");clot.AI();
                    Require(fresh.X==clot.position.X && fresh.Y==clot.position.Y && clot.position==position && fresh.Vx==0 && fresh.Vy==0 && fresh.A2==clot.ai[2],"New real sample resumes zero-V/static while its heal clock continues.");clears++;
                }
                else negative++;
                cases++;
            }
            Setup();Console.WriteLine("PASS MoonLeech clear exact-clock/native slot cases="+cases+" clear="+clears+" skipped="+negative);
        }
        private static void ClearContinuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            clearExports.Clear();
            var records=new List<string>{"phase,writer,frame,clotActions,selected,matched,staticRestored,cleared,expired,error"};
            using(var outlets=new Outlets())try
            {
                foreach(int phase in new[]{2,3})foreach(int writerSlot in new[]{8,30})
                {
                    Setup();var io=new Inputs(context);
                    var core=Actor(16,398,new Vector2(1900,2000));core.localAI[3]=1;core.ai[0]=1;
                    var left=Actor(18,397,new Vector2(1500,1800));left.ai[3]=16;left.ai[2]=0;
                    var right=Actor(19,397,new Vector2(2300,1800));right.ai[3]=16;right.ai[2]=1;
                    var head=Actor(20,396,new Vector2(1900,1600));head.ai[3]=16;
                    var writer=Actor(writerSlot,398,new Vector2(1100,1700));writer.localAI[3]=1;writer.ai[0]=phase;writer.ai[1]=(phase==2?60:40)-4;
                    var q=ProjectileBirth(head,new Vector2(2350,2300));
                    var clot=Actor(25,401,new Vector2(2000,2100));clot.ai[0]=head.whoAmI+1;clot.ai[1]=q.key;
                    NativeCombatObservationChecks.Save(io.Host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                    var paths=new List<NpcTrajectory>();NpcTrajectory beforeClear=null;long clearTick=-1;int selected=0,matched=0,restored=0,expired=0,actions=0;double maximum=0;
                    for(int frame=0;frame<95 && clot.active;frame++)
                    {
                        NativeCombatModeledImpactChecks.SampleMouse(context,clot.Center);step();actions++;
                        if(!q.active && clearTick<0){clearTick=(long)Main.GameUpdateCount;Require(frame==3,"Actual known foreign core reaches exact global clear on fourth frame.");}
                        var path=cache.Read(0);bool own=path!=null && ReferenceEquals(path.Identity.Token,clot);
                        if(own)
                        {
                            selected++;if(beforeClear==null && clearTick<0)
                            {
                                beforeClear=path;var sourceRows=(NpcMotionState[])Get(io.Source,"states");var roles=(bool[])Get(io.Source,"motionRoles");int count=(int)Get(io.Source,"observedCount");
                                for(int i=0;i<count;i++)if(ReferenceEquals(sourceRows[i].Identity.Token,writer))Require(!roles[i],"Foreign finite clear writer does not acquire full unrelated AI.");
                                Require(path.Count>4,"Known clear keeps useful401 lifecycle beyond its real consumer.");
                            }
                            if(!paths.Contains(path))paths.Add(path);
                        }
                        if(clearTick>=0 && Main.GameUpdateCount>clearTick)
                        {
                            Require(!ReferenceEquals(path,beforeClear),"Real anchor retirement replaces the old production input publication.");
                            var prepared=GetOptional(Get(io.Host,"World"),"preparedPath") as NpcTrajectory;Require(prepared==null || !ReferenceEquals(prepared,beforeClear),"Prepared consumer cannot retain preclear input.");expired++;
                        }
                        foreach(var old in paths)
                        {
                            int age=(int)((long)Main.GameUpdateCount-old.CaptureTick);if(age<1 || age>=old.Count || !clot.active)continue;
                            var point=old[age];double error=Vector2.Distance(clot.position,new Vector2(point.Bounds.X,point.Bounds.Y));maximum=Math.Max(maximum,error);
                            Require(error<=.08 && point.Vx==clot.velocity.X && point.Vy==clot.velocity.Y && point.CanReceive==(!clot.dontTakeDamage && !clot.immortal),"Known global clear frozen401 phase="+phase+" writer="+writerSlot+" frame="+frame+" age="+age+" error="+error+" stop="+old.Stop);
                            matched++;if(clearTick>=0 && old.CaptureTick>clearTick && age>=8 && !io.Capture(clot).MoonLeechAnchorValid){Require(point.Vx==0 && point.Vy==0,"Postclear restoration is genuinely static.");restored++;}
                        }
                        records.Add(string.Join(",",phase,writerSlot,frame,actions,selected,matched,restored,clearTick>=0,expired,maximum.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
                    }
                    Require(clearTick>=0 && selected>8 && beforeClear!=null && expired>0 && matched>8 && restored>=8,"Actual finite foreign clear has matched prefix, publication retirement and fresh static restoration.");
                    if(phase==2)Require(!clot.active && clot.ai[2]>=90,"Known clear retains the full90-action original healing/retirement consumer.");
                    else Require(!clot.active && clot.ai[2]<90,"Actual foreign withdrawal subsequently retires the live head; this is distinct from clearing456.");
                    Console.WriteLine("PASS MoonLeech finiteclear phase="+phase+" writer="+writerSlot+" actions="+actions+" selected="+selected+" matched="+matched+" staticRestored="+restored+" expired="+expired+" max="+maximum);
                }
                Require(clearExports.Count>0,"Actual global withdrawal really emits its isolated23 exports with original parameters.");
                Console.WriteLine("MoonLeech finiteclear originalNetwork23="+clearExports.Count);
            }
            finally{NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"moon-leech-clear.csv"),records);File.WriteAllLines(Path.Combine(output,"moon-leech-clear-network.csv"),clearExports);}
        }
        private static int CountClots(){int n=0;foreach(var row in Main.npc)if(row.active && row.type==401)n++;return n;}
        private static NPC Actor(int slot,int type,Vector2 position){var n=Main.npc[slot]=new NPC{whoAmI=slot};n.SetDefaults(type);n.active=true;n.target=Main.myPlayer;n.position=position;return n;}
        private static Projectile ProjectileBirth(NPC head,Vector2 center){int slot=Projectile.NewProjectile(head.GetSpawnSource_ForProjectile(),center,Vector2.Zero,456,0,0,Main.myPlayer,head.whoAmI+1,Main.myPlayer);return Main.projectile[slot];}
        private static void Setup(){NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.dayTime=false;Main.getGoodWorld=false;Main.remixWorld=false;Main.Map=new Terraria.Map.WorldMap(Main.maxTilesX,Main.maxTilesY);for(int i=0;i<255;i++)Main.player[i].active=false;var p=Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};p.position=new Vector2(2400.125f,2400-p.height);p.ResetFallDamage();p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.immune=true;p.immuneTime=1000000;for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;}
        private static PredictionEnvironment Environment(){var p=Main.LocalPlayer;return new PredictionEnvironment{PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,GravityWorldSurface=Main.worldSurface,Multiplayer=Main.netMode==1,Expert=Main.expertMode};}
        private static PredictionMoonLeeches Snapshot(Projectile q,NpcMotionState[] group)
        {int slot=(int)Math.Abs(q.ai[0])-1;NpcIdentity id=default(NpcIdentity);foreach(var row in group)if(row.Identity.Slot==slot){id=row.Identity;break;}var p=Main.player[(int)q.ai[1]];object[] clearArgs={120,-1};int clearAt=(int)moonSource.GetMethod("FirstMoonAnchorClear",Flags).Invoke(null,clearArgs),clearWriter=(int)clearArgs[1];var f=new PredictionMoonLeech(q,q.whoAmI,(int)q.key,q.owner,q.timeLeft,q.extraUpdates,q.penetrate,true,id,true,p,p.whoAmI,new MotionRect(p.position.X,p.position.Y,p.width,p.height),p.active,p.dead,p.creativeGodMode,q.owner==255 || Main.player[q.owner].active,new MotionRect(q.position.X,q.position.Y,q.width,q.height),q.velocity.X,q.velocity.Y,q.ai[0],q.localAI[0],q.localAI[1],true,q.ignoreWater,q.wet,clearAt,clearWriter);return new PredictionMoonLeeches(new[]{f},1,Main.leftWorld,Main.rightWorld,Main.topWorld,Main.bottomWorld);}
        private sealed class Inputs
        {
            internal readonly object Host,Source;internal readonly IPredictionTerrain Terrain;private readonly long session;private readonly MethodInfo read,tracking,player;
            internal Inputs(object context){Host=Get(context,"CombatObservation");Source=Get(Host,"Prediction");moonSource=Source.GetType();Terrain=(IPredictionTerrain)Get(Source,"Terrain");session=(long)Get(Host,"Session");read=Source.GetType().GetMethod("Read",Flags);tracking=Source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);player=Source.GetType().GetMethod("ReadPlayer",Flags);}
            internal NpcMotionState Capture(NPC n){var s=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,s,Terrain};tracking.Invoke(null,args);s=(NpcMotionState)args[1];s.MoonRelationsCaptured=s.MoonRelationsComplete=s.MoonEffectsCaptured=true;s.MoonExternalEffectAt=s.MoonGlobalRetireAt=int.MaxValue;return s;}
            internal PredictionPlayerMotion Player(){return (PredictionPlayerMotion)player.Invoke(null,new object[]{Main.LocalPlayer});}
            internal NpcMotionState[] Group(NPC clot){var values=new List<NpcMotionState>();int raw=-1,head=(int)Math.Abs(clot.ai[0])-1;if(head>=0 && head<Main.maxNPCs)raw=(int)Main.npc[head].ai[3];for(int i=0;i<Main.maxNPCs;i++){var n=Main.npc[i];if(n!=null && (n.active && (n.type==398 || n.type==397 || n.type==396 || n.type==401) || i==raw))values.Add(Capture(n));}return values.ToArray();}
        }
    }
}
