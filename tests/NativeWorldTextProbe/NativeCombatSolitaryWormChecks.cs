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
    // Pending atomic integration. Original374->375 and original402 births,
    // selection, receiving, clock570/600 and effects remain real consumers.
    internal static class NativeCombatSolitaryWormChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Action<NPC> before,after;
        private static readonly List<string> outlets=new List<string>();
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static Type Model{get{return typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcSegmentedWormMotion",true);}}
        private static bool IsolateNetwork(int msgType,int remoteClient,int ignoreClient,Terraria.Localization.NetworkText text,int number,float number2,float number3,float number4,int number5,int number6,int number7)
        {
            if((msgType!=23 && msgType!=28) || number<0 || number>=Main.maxNPCs)return true;
            int type=Main.npc[number].type;if(type!=25 && type!=374 && type!=375 && type!=402)return true;
            outlets.Add(string.Join("/",msgType,remoteClient,ignoreClient,text,number,number2,number3,number4,number5,number6,number7));return false;
        }
        private static Harmony Network()
        {var h=new Harmony("JueMingR.Probe.SolitaryWormNetwork");h.Patch(typeof(NetMessage).GetMethod("SendData",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSolitaryWormChecks).GetMethod(nameof(IsolateNetwork),Flags)){priority=Priority.First+100});return h;}
        internal static void Boundaries()
        {
            // These are actually different from a head whose missing A0 means
            // imminent descendants. Their ordinary zero links are not guards.
            foreach(int type in new[]{375,402})
            {
                var actor=new NPC();actor.SetDefaults(type);actor.active=true;
                var n=new NpcMotionState{Identity=new NpcIdentity(1,actor,16,1,type,type),Style=actor.aiStyle,Active=true,A0=actor.ai[0],A1=actor.ai[1],A2=actor.ai[2]};
                Require((bool)Model.GetMethod("Known",Flags).Invoke(null,new object[]{n}),"Actual solitary default enters the exact AI6 route type="+type);
                foreach(bool client in new[]{false,true})Require(!(bool)Model.GetMethod("BirthPending",Flags).Invoke(null,new object[]{n,new PredictionEnvironment{Multiplayer=client}}),"Original solitary defaults never fabricate chain birth type="+type+" client="+client);
            }
            Console.WriteLine("PASS SolitaryWorm actual defaults / no chain-birth guards");
        }
        internal static void Lifecycle(object context)
        {
            var network=Network();outlets.Clear();int actions=0,normalized=0,reset=0,marks=0;
            try
            {
                foreach(int type in new[]{375,402})
                {
                    Setup(context);var actor=Main.npc[NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1800,2200,type,Start:16)];
                    var terrain=Terrain(context);
                    for(int action=0;action<(type==402?605:45);action++)
                    {
                        var rows=Capture(context);var n=rows[Index(rows,actor)];float clock=actor.ai[2];terrain.Reset();
                        object[] args={n,EnvironmentFor(actor),terrain,0,PredictionStop.None,rows,rows.Length,actor.confused};
                        Require((bool)Model.GetMethod("Step",Flags).Invoke(null,args),"Original solitary AI has a determined current action type="+type+" action="+action+" stop="+args[4]);
                        actor.AI();Compare((NpcMotionState)args[0],actor,"AI action="+action);actions++;
                        if(type==402 && clock==569){normalized++;Require(HasMark(),"Original570 server consumer emits genuine540 without forecast RNG.");marks++;}
                        if(type==402 && clock==599){reset++;Require(actor.ai[2]==0,"Original600 server reset is retained.");}
                    }
                    int life=actor.life;actor.StrikeNPCNoInteraction(type==375?1:actor.defense+20,0,0);Require(actor.life>0 && actor.life<life,"Original positive receiving preserves this solitary owner.");
                    actor.StrikeNPCNoInteraction(actor.lifeMax+actor.defense+100,0,0);Require(!actor.active,"Original solitary death/loot is not skipped.");
                }
                Require(normalized==1 && reset==1 && marks==1,"Actual Stardust complete prefix crosses570 normalization/mark and600 reset.");
                Console.WriteLine("PASS SolitaryWorm lifecycle actions="+actions+" normalized="+normalized+" reset="+reset+" marks="+marks+" outlets="+outlets.Count);
            }
            finally{network.UnpatchAll(network.Id);}
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            int leaf=-1;string selector=System.Environment.GetEnvironmentVariable("JUEMINGR_SOLITARY_WORM_SCENE");
            if(!string.IsNullOrEmpty(selector) && (!int.TryParse(selector,out leaf) || leaf<0 || leaf>5))throw new ArgumentException("JUEMINGR_SOLITARY_WORM_SCENE must be0..5 or unset.");
            Console.WriteLine("SOLITARY actualLeaves="+(leaf<0?"0,1,2,3,4,5":leaf.ToString(CultureInfo.InvariantCulture)));
            var host=Get(context,"CombatObservation");var network=Network();var hooks=new Harmony("JueMingR.Probe.SolitaryWormSlots");
            hooks.Patch(typeof(NPC).GetMethod("UpdateNPC",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSolitaryWormChecks),nameof(Before)),postfix:new HarmonyMethod(typeof(NativeCombatSolitaryWormChecks),nameof(After)));
            var csv=new List<string>{"scene,frame,type,slot,clock,selected,actions,matched,long,marks,resets,hit,fresh,restored,error"};
            try
            {
                for(int scene=0;scene<2;scene++)
                {
                    if(leaf>=0 && leaf!=scene)continue;Setup(context);
                    int startType=scene==0?374:402;var actor=Main.npc[NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1800,scene==0?2400:2200,startType,Start:16)];
                    if(scene==0)
                    {
                        int elapsed=0;for(;elapsed<300 && actor.active && actor.type==374;elapsed++)step();
                        Require(actor.active && actor.type==375 && elapsed>=90,"Original nearby-player census and90 clock cause actual374 Transform375.");
                        Console.WriteLine("SOLITARY actualTransform elapsed="+elapsed+" slot="+actor.whoAmI+" target="+actor.target+" A="+string.Join("/",actor.ai));
                    }
                    var terrain=Terrain(context);var frozen=new List<NpcTrajectory>();NpcMotionState predicted=default(NpcMotionState);bool valid=false;PredictionStop stop=PredictionStop.None;
                    int actions=0,selected=0,matched=0,longMatches=0,marks=0,resets=0,fresh=0,restored=0;bool hit=false;long hitTick=-1;NpcTrajectory previous=null;double maximum=0;float beforeClock=0;
                    before=n=>{if(!ReferenceEquals(n,actor) || !n.active)return;var rows=Capture(context);predicted=rows[Index(rows,n)];beforeClock=n.ai[2];terrain.Reset();valid=NpcMotion.Step(ref predicted,rows,rows.Length,EnvironmentFor(n),terrain,1,true,out stop);Require(valid || stop==PredictionStop.Despawn,"Current solitary original action has no unknown motor type="+n.type+" stop="+stop);};
                    after=n=>{if(!ReferenceEquals(n,actor))return;if(!valid){Require(!n.active,"Current original retirement agrees with the predicted boundary.");return;}Compare(predicted,n,"full action="+actions);actions++;if(n.type==402 && beforeClock==569){Require(HasMark(),"Original selected402 emits real marks.");marks++;}if(n.type==402 && beforeClock==599)resets++;};
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                    for(int frame=0;frame<(scene==0?480:650) && actor.active;frame++)
                    {
                        if(frame==100){previous=cache.Read(0);int life=actor.life;actor.StrikeNPCNoInteraction(scene==0?1:actor.defense+20,0,0);Require(actor.active && actor.life>0 && actor.life<life,"Natural positive receiving is genuine, not a health rewrite.");frozen.Clear();hit=true;hitTick=(long)Main.GameUpdateCount;}
                        NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();if(!actor.active)break;
                        bool chosen=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,actor);if(chosen)selected++;
                        var path=cache.Read(0);if(hit && Main.GameUpdateCount>hitTick){Require(path==null || !ReferenceEquals(path,previous),"Actual solitary receiving withdraws the old production publication.");var prepared=GetOptional(Get(host,"World"),"preparedPath") as NpcTrajectory;Require(prepared==null || ReferenceEquals(prepared,path),"Prepared consumer cannot retain the old solitary input.");}
                        if(path!=null && ReferenceEquals(path.Identity.Token,actor) && !frozen.Contains(path)){frozen.Add(path);if(hit && path.CaptureTick>hitTick)fresh++;}
                        foreach(var old in frozen)
                        {
                            int age=(int)((long)Main.GameUpdateCount-old.CaptureTick);if(age<1 || age>=old.Count)continue;var point=old[age];double error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),actor.position);maximum=Math.Max(maximum,error);
                            Require(error<=.08 && Math.Abs(point.Vx-actor.velocity.X)<=.0001 && Math.Abs(point.Vy-actor.velocity.Y)<=.0001 && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal && !actor.friendly),"Frozen solitary scene="+scene+" frame="+frame+" age="+age+" clock="+actor.ai[2]+" error="+error+" predictedV="+point.Vx+","+point.Vy+" originalV="+actor.velocity+" stop="+old.Stop);
                            matched++;if(age>=30){longMatches++;if(hit && old.CaptureTick>hitTick)restored++;}
                        }
                        csv.Add(string.Join(",",scene,frame,actor.type,actor.whoAmI,actor.ai[2],chosen,actions,matched,longMatches,marks,resets,hit,fresh,restored,maximum.ToString("R",CultureInfo.InvariantCulture)));
                    }
                    Require(selected>30 && actions>30 && matched>=30 && longMatches>=30 && hit && fresh>0 && restored>=30,"Actual mouse radius/ranking selects the solitary receiver and preserves useful receiving recovery.");
                    if(scene==1)Require(marks>0 && resets>0,"Natural402 publication crosses actual570 mark/normalize and600 clock reset.");
                    Console.WriteLine("PASS Solitary natural scene="+scene+" type="+actor.type+" selected="+selected+" actions="+actions+" matched="+matched+" long="+longMatches+" marks="+marks+" resets="+resets+" fresh="+fresh+" restored="+restored+" max="+maximum.ToString("R",CultureInfo.InvariantCulture));before=after=null;
                }
                for(int scene=2;scene<=4;scene++)if(leaf<0 || leaf==scene)RelationScene(context,cache,step,scene,csv);
                if(leaf<0 || leaf==5)ClientForeignTail(context,cache,step,csv);
            }
            finally{before=after=null;Main.netMode=0;hooks.UnpatchAll(hooks.Id);network.UnpatchAll(network.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"solitary-worm-scenes.csv"),csv);}
        }
        private static void RelationScene(object context,NpcPredictionCache cache,Action step,int scene,List<string> csv)
        {
            // The client starts at a genuine default birth, without jumping its
            // clock. Positive A1 is an explicit legal non-default raw input,
            // not a claim that solitary births ordinarily create a parent.
            int variants=scene==2?1:2;
            for(int variant=0;variant<variants;variant++)
            {
                before=after=null;Setup(context);var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");NPC parent=null;
                int type=scene==2 || variant==1?402:375;
                int parentSlot=variant==0?16:8,headSlot=variant==0?8:16;
                if(scene!=2)
                {
                    parent=Main.npc[NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1800,2200,25,Start:parentSlot)];
                    // This actual original action supplies its TargetClosest
                    // initialization and velocity, before the relation sample.
                    step();Require(parent.active && parent.aiStyle==9 && parent.target!=255 && parent.velocity.Length()>0,"Actual non-AI6 parent has initialized moving geometry.");
                    if(scene==4){parent.StrikeNPCNoInteraction(parent.lifeMax+parent.defense+100,0,0);Require(!parent.active,"Inactive raw parent comes from actual Strike/death.");}
                }
                var actor=Main.npc[NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),scene==2?1800:(int)parent.Center.X+80,2200,type,Start:scene==2?16:headSlot)];
                if(parent!=null){actor.ai[1]=parent.whoAmI;Require(actor.ai[1]>0 && actor.ai[1]<Main.npc.Length,"Explicit raw A1 is a legal positive nonzero slot.");}
                if(scene==2)Main.netMode=1;
                var terrain=Terrain(context);var expected=new Dictionary<NPC,NpcMotionState>();var frozen=new List<NpcTrajectory>();var capturedClock=new Dictionary<NpcTrajectory,float>();
                var originalParent=parent==null?Vector2.Zero:parent.position;
                int actions=0,parentActions=0,selected=0,matched=0,longMatches=0,cross570=0,cross600=0,closures=0;double maximum=0;
                before=n=>
                {
                    if(!n.active || !ReferenceEquals(n,actor) && !ReferenceEquals(n,parent))return;
                    var rows=Capture(context);var prediction=rows[Index(rows,n)];terrain.Reset();PredictionStop stop;
                    Require(NpcMotion.Step(ref prediction,rows,rows.Length,EnvironmentFor(n),terrain,1,true,out stop),"Original relation current action scene="+scene+" variant="+variant+" type="+n.type+" slot="+n.whoAmI+" stop="+stop);
                    expected[n]=prediction;
                };
                after=n=>
                {
                    NpcMotionState prediction;if(!expected.TryGetValue(n,out prediction))return;Compare(prediction,n,"relation scene="+scene+" variant="+variant+" slot="+n.whoAmI);expected.Remove(n);
                    if(ReferenceEquals(n,actor))actions++;else parentActions++;
                };
                NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                for(int frame=0;frame<(scene==2?650:120);frame++)
                {
                    Require(actor.active,"Selected solitary remains live through this explicit relation window.");
                    if(scene==3)Require(parent.active,"Active foreign-parent leaf retains its actual motor denominator.");
                    NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();
                    bool chosen=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,actor);if(chosen)selected++;
                    var path=cache.Read(0);
                    if(path!=null && ReferenceEquals(path.Identity.Token,actor) && !frozen.Contains(path))
                    {
                        frozen.Add(path);capturedClock[path]=actor.ai[2]-((long)Main.GameUpdateCount-path.CaptureTick);
                        if(parent!=null)
                        {
                            var rows=(NpcMotionState[])Get(source,"states");var roles=(bool[])Get(source,"motionRoles");int count=(int)Get(source,"observedCount"),index=-1;
                            var parentIdentity=new NpcIdentity((long)Get(host,"Session"),parent,parent.whoAmI,parent.generation,parent.type,parent.netID);
                            for(int i=0;i<count;i++)if(rows[i].Identity.Equals(parentIdentity)){index=i;break;}
                            Require(index>=0 && rows[index].Active==parent.active,"Production closure retains exact raw foreign parent identity/activity.");
                            Require(roles[index]==parent.active,"Production active parent is MOTOR; genuinely inactive raw geometry is a value role.");closures++;
                        }
                    }
                    foreach(var old in frozen)
                    {
                        int age=(int)((long)Main.GameUpdateCount-old.CaptureTick);if(age<1 || age>=old.Count)continue;var point=old[age];double error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),actor.position);maximum=Math.Max(maximum,error);
                        Require(error<=.08 && Math.Abs(point.Vx-actor.velocity.X)<=.0001 && Math.Abs(point.Vy-actor.velocity.Y)<=.0001 && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal && !actor.friendly),"Frozen relation scene="+scene+" variant="+variant+" frame="+frame+" age="+age+" error="+error+" clock="+actor.ai[2]+" parent="+(parent==null?-1:parent.whoAmI)+" predictedV="+point.Vx+","+point.Vy+" originalV="+actor.velocity+" stop="+old.Stop);
                        matched++;if(age>=30){longMatches++;if(capturedClock[old]<570 && capturedClock[old]+age>=570)cross570++;if(capturedClock[old]<600 && capturedClock[old]+age>=600)cross600++;}
                    }
                    if(scene==2)Require(actor.ai[2]==frame+1 && !HasMark(),"Actual client clock progresses through570/600 with no server mark birth/reset.");
                    csv.Add(string.Join(",",scene,frame,actor.type,actor.whoAmI,actor.ai[2],chosen,actions,matched,longMatches,0,0,false,0,0,maximum.ToString("R",CultureInfo.InvariantCulture)));
                }
                Require(actions==(scene==2?650:120) && selected>30 && matched>=30 && longMatches>=30,"Actual relation selection/current actions retain useful production futures.");
                if(scene==2)Require(cross570>0 && cross600>0 && actor.ai[2]==650,"Actual client frozen long windows cross both570 and600 without server reset.");
                else if(scene==3)Require(parentActions==120 && Vector2.Distance(parent.position,originalParent)>30 && closures>0,"Foreign parent actually advances120 actions and production MOTOR closure follows it.");
                else Require(!parent.active && parentActions==0 && parent.position==originalParent && closures>0,"Inactive foreign parent has no actions; original solitary keeps reading its raw center without orphan death.");
                Console.WriteLine("PASS Solitary relation scene="+scene+" variant="+variant+" type="+type+" slot="+actor.whoAmI+" parent="+(parent==null?-1:parent.whoAmI)+" parentActions="+parentActions+" selected="+selected+" actions="+actions+" matched="+matched+" long="+longMatches+" closures="+closures+" cross570="+cross570+" cross600="+cross600+" max="+maximum.ToString("R",CultureInfo.InvariantCulture));
                before=after=null;Main.netMode=0;
            }
        }
        private static void ClientForeignTail(object context,NpcPredictionCache cache,Action step,List<string> csv)
        {
            // This is a genuine non-solitary chain birth, then an explicit
            // legal raw-parent boundary input. Client AI skips the server's
            // style guard and still consumes positive ai1's raw future center.
            // Reuse the certified full-slot birth and actual network outlets.
            before=after=null;Setup(context);var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var terrain=Terrain(context);
            var network=(Harmony)typeof(NativeCombatSegmentedWormChecks).GetMethod("Network",Flags).Invoke(null,null);
            try
            {
                var head=Main.npc[NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1800,2200,87,Start:8)];
                typeof(NativeCombatSegmentedWormChecks).GetMethod("SpawnChain",Flags).Invoke(null,new object[]{head});
                NPC actor=null;foreach(var n in Main.npc)if(n.active && n.type==92 && n.realLife==head.whoAmI){actor=n;break;}
                Require(actor!=null && actor.active && !actor.dontTakeDamage,"Actual original Wyvern chain supplies a damageable non-solitary92 tail.");
                var parent=Main.npc[NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1800,2200,25,Start:64)];
                NativeQuickItemChecks.BeginWorldStep();Main.EverLastingTicker++;NPC.UpdateProtectedSpawnSlots();parent.UpdateNPC(parent.whoAmI);
                Require(parent.active && parent.aiStyle==9 && parent.velocity.Length()>0 && parent.whoAmI>actor.whoAmI,"Original later-slot type25 initializes its real moving foreign motor.");
                actor.ai[1]=parent.whoAmI;Main.netMode=1;Vector2 originalParent=parent.position;
                var predictions=new Dictionary<NPC,NpcMotionState>();var frozen=new List<NpcTrajectory>();
                int actions=0,parentActions=0,selected=0,matched=0,longMatches=0,closures=0;double maximum=0;
                before=n=>
                {
                    if(!n.active || !ReferenceEquals(n,actor) && !ReferenceEquals(n,parent))return;
                    var rows=Capture(context);var value=rows[Index(rows,n)];terrain.Reset();PredictionStop stop;
                    Require(NpcMotion.Step(ref value,rows,rows.Length,EnvironmentFor(n),terrain,1,true,out stop),"Current client foreign-parent original action type="+n.type+" slot="+n.whoAmI+" stop="+stop);
                    predictions[n]=value;
                };
                after=n=>
                {
                    NpcMotionState value;if(!predictions.TryGetValue(n,out value))return;Compare(value,n,"client raw-parent scene5 slot="+n.whoAmI);predictions.Remove(n);
                    if(ReferenceEquals(n,actor))actions++;else parentActions++;
                };
                NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                for(int frame=0;frame<120;frame++)
                {
                    Require(actor.active && parent.active,"Client raw-parent leaf preserves both actual actors throughout its finite window.");
                    NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();
                    bool chosen=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,actor);if(chosen)selected++;
                    var path=cache.Read(0);
                    if(path!=null && ReferenceEquals(path.Identity.Token,actor) && !frozen.Contains(path))
                    {
                        var rows=(NpcMotionState[])Get(source,"states");var roles=(bool[])Get(source,"motionRoles");int count=(int)Get(source,"observedCount"),index=-1;
                        var identity=new NpcIdentity((long)Get(host,"Session"),parent,parent.whoAmI,parent.generation,parent.type,parent.netID);
                        for(int i=0;i<count;i++)if(rows[i].Identity.Equals(identity)){index=i;break;}
                        Require(index>=0 && rows[index].Active && roles[index],"Production client positive-ai1 active foreign center requires its full-identity MOTOR, never health-only frozen geometry.");
                        closures++;frozen.Add(path);
                    }
                    foreach(var old in frozen)
                    {
                        int age=(int)((long)Main.GameUpdateCount-old.CaptureTick);if(age<1 || age>=old.Count)continue;var point=old[age];double error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),actor.position);maximum=Math.Max(maximum,error);
                        Require(error<=.08 && Math.Abs(point.Vx-actor.velocity.X)<=.0001 && Math.Abs(point.Vy-actor.velocity.Y)<=.0001 && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal && !actor.friendly),"Frozen client non-solitary scene5 frame="+frame+" age="+age+" error="+error.ToString("R")+" parent="+parent.whoAmI+" parentPosition="+parent.position+" predicted="+point.Bounds.X+","+point.Bounds.Y+" actual="+actor.position+" stop="+old.Stop);
                        matched++;if(age>=30)longMatches++;
                    }
                    csv.Add(string.Join(",",5,frame,actor.type,actor.whoAmI,actor.ai[2],chosen,actions,matched,longMatches,0,0,false,0,0,maximum.ToString("R",CultureInfo.InvariantCulture)));
                }
                Require(actions==120 && parentActions==120 && selected>30 && closures>0 && longMatches>=30 && Vector2.Distance(parent.position,originalParent)>30,"Actual client moving foreign-parent consumer retains useful120-action production futures.");
                // The exact same observed foreign parent is a server orphan:
                // original active/style guard happens before geometric linking.
                // Keep this distinction instead of weakening the native gate.
                before=after=null;Main.netMode=0;var serverRows=Capture(context);var server=serverRows[Index(serverRows,actor)];terrain.Reset();PredictionStop serverStop;
                Require(!NpcMotion.Step(ref server,serverRows,serverRows.Length,EnvironmentFor(actor),terrain,1,true,out serverStop) && serverStop==PredictionStop.Despawn,"Server active wrong-style parent takes the actual orphan branch before Center consumption.");
                actor.UpdateNPC(actor.whoAmI);Require(!actor.active,"Original server UpdateNPC really retires the same wrong-style tail; AI/checkDead is not skipped.");
                Console.WriteLine("PASS client non-solitary scene5 type92 parent25 actions="+actions+" parentActions="+parentActions+" selected="+selected+" closures="+closures+" matched="+matched+" long="+longMatches+" serverRetired=1 max="+maximum.ToString("R",CultureInfo.InvariantCulture));
            }
            finally{before=after=null;Main.netMode=0;network.UnpatchAll(network.Id);}
        }
        private static void Setup(object context){typeof(NativeCombatSegmentedWormChecks).GetMethod("Setup",Flags).Invoke(null,new object[]{context});}
        private static IPredictionTerrain Terrain(object context){return (IPredictionTerrain)Get(Get(Get(context,"CombatObservation"),"Prediction"),"Terrain");}
        private static PredictionEnvironment EnvironmentFor(NPC n){return (PredictionEnvironment)typeof(NativeCombatSegmentedWormChecks).GetMethod("EnvironmentFor",Flags).Invoke(null,new object[]{n});}
        private static NpcMotionState[] Capture(object context)
        {
            var rows=new List<NpcMotionState>((NpcMotionState[])typeof(NativeCombatSegmentedWormChecks).GetMethod("Capture",Flags).Invoke(null,new object[]{context}));
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var read=source.GetType().GetMethod("Read",Flags);long session=(long)Get(host,"Session");
            // Current-action oracle observes raw inactive geometry, too. The
            // production role/completeness check above is independently read
            // from Source; these oracle facts cannot certify a frozen parent.
            for(int i=0;i<rows.Count;i++)if(rows[i].EffectiveType==375 || rows[i].EffectiveType==402)
            {
                var n=rows[i];n.WormRelationsCaptured=n.WormRelationsComplete=n.WormLinkFactsCaptured=true;
                if(n.A1>0 && n.A1<Main.npc.Length)
                {
                    var parent=(NpcMotionState)read.Invoke(null,new object[]{Main.npc[(int)n.A1],session});n.ParentIdentity=parent.Identity;
                    bool found=false;for(int j=0;j<rows.Count;j++)if(rows[j].Identity.Equals(parent.Identity)){found=true;break;}if(!found)rows.Add(parent);
                }
                rows[i]=n;
            }
            return rows.ToArray();
        }
        private static int Index(NpcMotionState[] rows,NPC n){for(int i=0;i<rows.Length;i++)if(ReferenceEquals(rows[i].Identity.Token,n))return i;return -1;}
        private static bool HasMark(){for(int i=0;i<Main.maxProjectiles;i++)if(Main.projectile[i].active && Main.projectile[i].type==540)return true;return false;}
        private static void Compare(NpcMotionState n,NPC actual,string label)
        {
            double error=Vector2.Distance(new Vector2(n.X,n.Y),actual.position);
            Require(error<=.08 && Math.Abs(n.Vx-actual.velocity.X)<=.0001 && Math.Abs(n.Vy-actual.velocity.Y)<=.0001 && n.A2==actual.ai[2] && n.CanReceive==(!actual.dontTakeDamage && !actual.immortal && !actual.friendly),"Original solitary "+label+" type="+actual.type+" error="+error+" predictedV="+n.Vx+","+n.Vy+" actualV="+actual.velocity+" clock="+n.A2+"/"+actual.ai[2]);
        }
    }
}
