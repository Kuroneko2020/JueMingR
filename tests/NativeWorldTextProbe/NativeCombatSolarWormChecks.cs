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
    // Locked AI6 Solar412/413/414, real full-slot birth,
    // selectable tail/shared life, ground policy and moving same-head census.
    // Production closure is inspected separately from the current-slot oracle.
    internal static class NativeCombatSolarWormChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Action<NPC> before,after;
        private static Action<Player,double> received;
        private static readonly List<string> outlets=new List<string>();
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static void AfterHurt(Player __instance,double __result){if(__result>0)received?.Invoke(__instance,__result);}
        private static Type Model{get{return typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcSegmentedWormMotion",true);}}
        private static bool Family(int t){return t>=412 && t<=414;}
        private static bool IsolateNetwork(int msgType,int remoteClient,int ignoreClient,Terraria.Localization.NetworkText text,int number,float number2,float number3,float number4,int number5,int number6,int number7)
        {
            if((msgType!=23 && msgType!=28) || number<0 || number>=Main.maxNPCs || !Family(Main.npc[number].type))return true;
            outlets.Add(string.Join("/",msgType,remoteClient,ignoreClient,text,number,number2,number3,number4,number5,number6,number7));return false;
        }
        private static Harmony Network()
        {var h=new Harmony("JueMingR.Probe.SolarWormNetwork");h.Patch(typeof(NetMessage).GetMethod("SendData",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSolarWormChecks).GetMethod(nameof(IsolateNetwork),Flags)){priority=Priority.First+100});return h;}

        internal static void Boundaries()
        {
            foreach(int type in new[]{412,413,414})
            {
                var native=new NPC();native.SetDefaults(type);native.active=true;
                var n=new NpcMotionState{Identity=new NpcIdentity(1,native,16,1,type,type),Style=native.aiStyle,Active=true};
                Require((bool)Model.GetMethod("Known",Flags).Invoke(null,new object[]{n}),"Actual Solar member enters the bounded family type="+type);
                Require(native.dontTakeDamage==(type!=414),"Actual protected head/body and damageable tail are distinct.");
                if(type==412)
                {
                    Require((bool)Model.GetMethod("BirthPending",Flags).Invoke(null,new object[]{n,new PredictionEnvironment()}),"Server Solar birth requires genuine new full identities.");
                    Require(!(bool)Model.GetMethod("BirthPending",Flags).Invoke(null,new object[]{n,new PredictionEnvironment{Multiplayer=true}}),"Client Solar does not duplicate authoritative birth.");
                }
                if(type==414)Require(native.takenDamageMultiplier==10f,"Native tail receiving multiplies actual damage before shared life.");
            }
            Console.WriteLine("PASS SolarWorm actual protection / tail receiving / authoritative birth boundaries");
        }

        internal static void Lifecycle(object context)
        {
            var network=Network();outlets.Clear();int current=0;
            try
            {
                Setup(context);var head=Birth(1800,2200,16);Spawn(head);var tail=Tail(head);Require(tail!=null,"Actual full birth supplies the selectable tail.");
                var terrain=Terrain(context);var selection=Get(Get(context,"CombatObservation"),"Selection");var accepts=selection.GetType().GetMethod("Receives",Flags);
                Require(!(bool)accepts.Invoke(null,new object[]{head,false}) && !(bool)accepts.Invoke(null,new object[]{Main.npc[(int)head.ai[0]],false}) && (bool)accepts.Invoke(null,new object[]{tail,false}),"Production selection rejects protected head/body and accepts the real tail.");
                // These are legal native velocity boundary inputs. They enter
                // both branches of the clamp before original AI; no solver-only
                // value is edited, and the ordinary natural leaves use birth V.
                foreach(var velocity in new[]{new Vector2(.2f,-.1f),new Vector2(24.125f,-7.375f)})
                {
                    head.velocity=velocity;
                    for(int action=0;action<35;action++)
                    {
                        var rows=Capture(context);var n=rows[Index(rows,head)];terrain.Reset();object[] args={n,EnvironmentFor(head),terrain,0,PredictionStop.None,rows,rows.Length,head.confused};
                        Require((bool)Model.GetMethod("Step",Flags).Invoke(null,args),"Current original Solar clamp/steering has a determined action stop="+args[4]);
                        head.AI();Compare((NpcMotionState)args[0],head,"AI clamp action="+action);current++;
                    }
                }
                // Native crowd stops at Main.maxNPCs. Raw dummy200 can still
                // be a legal ai1 geometry value, but is never a crowd actor.
                // Configure the actual raw slot for one original AI action;
                // do not call its AI or pretend it was a normal NewNPC birth.
                Require(Main.maxNPCs==NpcPredictionCache.Capacity,"The locked native crowd range matches the existing prediction capacity.");
                var savedDummy=Main.npc[Main.maxNPCs];
                try
                {
                    var dummy=Main.npc[Main.maxNPCs]=new NPC();dummy.SetDefaults(412);dummy.whoAmI=Main.maxNPCs;dummy.active=true;
                    dummy.position=head.Center+new Vector2(24,12)-new Vector2(dummy.width*.5f,dummy.height*.5f);
                    var rows=Capture(context);Require(Index(rows,dummy)>=0,"Current value oracle includes the actual raw dummy rather than hiding it from the model.");
                    var n=rows[Index(rows,head)];terrain.Reset();object[] args={n,EnvironmentFor(head),terrain,0,PredictionStop.None,rows,rows.Length,head.confused};
                    Require((bool)Model.GetMethod("Step",Flags).Invoke(null,args),"Actual Solar head excludes raw dummy200 from its same-type census.");
                    head.AI();Compare((NpcMotionState)args[0],head,"native dummy200 crowd exclusion");current++;
                }
                finally{Main.npc[Main.maxNPCs]=savedDummy;}
                int life=head.life;int result=tail.StrikeNPCNoInteraction(5,0,0);
                Require(result>0 && head.active && head.life>0 && head.life==life-result && tail.life==head.life,"Original positive tail Strike consumes multiplied damage through actual realLife owner.");
                tail.StrikeNPCNoInteraction(head.lifeMax+100,0,0);Require(!head.active,"Actual tail Strike kills the shared owner; checkDead/loot/network are not skipped.");
                for(int frame=0;frame<3 && Count()>0;frame++)NativeSlots();
                Require(Count()==0,"Actual parent death retires the complete native Solar chain.");
                Console.WriteLine("PASS SolarWorm lifecycle parts=31 currentAI="+current+" positiveDamage="+result+" nativeOutlets="+outlets.Count);
            }
            finally{network.UnpatchAll(network.Id);}
        }

        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            int leaf=-1;string selector=System.Environment.GetEnvironmentVariable("JUEMINGR_SOLAR_WORM_SCENE");
            if(!string.IsNullOrEmpty(selector) && (!int.TryParse(selector,out leaf) || leaf<0 || leaf>2))throw new ArgumentException("JUEMINGR_SOLAR_WORM_SCENE must be0..2 or unset.");
            Console.WriteLine("SOLAR-WORM actualLeaves="+(leaf<0?"0,1,2":leaf.ToString(CultureInfo.InvariantCulture)));
            var host=Get(context,"CombatObservation");var terrain=Terrain(context);var network=Network();var hooks=new Harmony("JueMingR.Probe.SolarWormSlots");
            hooks.Patch(typeof(NPC).GetMethod("UpdateNPC",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSolarWormChecks),nameof(Before)),postfix:new HarmonyMethod(typeof(NativeCombatSolarWormChecks),nameof(After)));
            var hurt=typeof(Player).GetMethod("Hurt",Flags,null,new[]{typeof(Terraria.DataStructures.PlayerDeathReason),typeof(int),typeof(int),typeof(bool),typeof(bool),typeof(bool),typeof(int),typeof(bool)},null);
            hooks.Patch(hurt,postfix:new HarmonyMethod(typeof(NativeCombatSolarWormChecks),nameof(AfterHurt)));
            var csv=new List<string>{"scene,frame,parts,actions,headActions,selected,matched,long,ground,air,crowd,fresh,restored,withdrawn,receiving,max"};
            try
            {
                for(int scene=0;scene<=2;scene++)
                {
                    if(leaf>=0 && leaf!=scene)continue;Setup(context);var head=Birth(1800,2200,8);Spawn(head);NPC other=null;
                    if(scene==1){other=Birth(1920,2225,48);Spawn(other);Require(Count()==62,"Two actual native births retain both31-part chains.");}
                    var tail=Tail(head);Require(tail!=null && tail.active,"Selected part is a genuine original414 tail.");
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                    var frozen=new List<NpcTrajectory>();NpcTrajectory previous=null;bool changed=false,pending=false;long changedTick=-1;int latestRecovery=0;
                    int actions=0,headActions=0,selected=0,matched=0,longMatches=0,ground=0,air=0,crowd=0,fresh=0,restored=0,withdrawn=0,receiving=0,closures=0;double maximum=0;
                    NpcMotionState predicted=default(NpcMotionState);bool valid=false;PredictionStop stop=PredictionStop.None;NPC observed=null;
                    Action invalidate=()=>{previous=cache.Read(0);withdrawn+=frozen.Count;frozen.Clear();changed=true;pending=true;latestRecovery=0;changedTick=(long)Main.GameUpdateCount;};
                    received=(p,amount)=>{if(!ReferenceEquals(p,Main.LocalPlayer))return;receiving++;invalidate();Console.WriteLine("SOLAR actual Player.Hurt tick="+Main.GameUpdateCount+" amount="+amount+" fall="+p.fallStart+","+p.fallStart2);};
                    before=n=>
                    {
                        observed=null;if(!n.active || !Family(n.type))return;observed=n;var rows=Capture(context);predicted=rows[Index(rows,n)];terrain.Reset();
                        if(n.type==412){if(GroundRow(Main.player[n.target])>0)ground++;else air++;if(other!=null && NativeCrowd(n)>0)crowd++;}
                        valid=NpcMotion.Step(ref predicted,rows,rows.Length,EnvironmentFor(n),terrain,1,true,out stop);
                        Require(valid || stop==PredictionStop.Despawn,"Current real Solar slot has a determined full action type="+n.type+" slot="+n.whoAmI+" A="+string.Join("/",n.ai)+" stop="+stop);
                    };
                    after=n=>
                    {
                        if(!ReferenceEquals(observed,n))return;if(!valid){Require(!n.active,"Current full Solar retirement matches the native action.");return;}
                        Compare(predicted,n,"full slot="+n.whoAmI);actions++;if(n.type==412)headActions++;
                    };
                    for(int frame=0;frame<560 && head.active && tail.active;frame++)
                    {
                        if(frame>=480 && !pending && latestRecovery>0)break;
                        if(frame==100){int life=head.life;int damage=tail.StrikeNPCNoInteraction(5,0,0);Require(damage>0 && head.life==life-damage && head.active,"Real positive414 receiving changes shared head life.");receiving++;invalidate();}
                        if(scene==0 && frame==200)
                        {
                            // A real relocation changes the player premise; no
                            // freeze across this input change is called exact.
                            Main.LocalPlayer.Teleport(new Vector2(Main.LocalPlayer.position.X,1900-Main.LocalPlayer.height));invalidate();
                        }
                        NativeCombatModeledImpactChecks.SampleMouse(context,tail.Center);step();if(!tail.active || !head.active)break;
                        bool chosen=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,tail);if(chosen)selected++;
                        var path=cache.Read(0);
                        if(changed && Main.GameUpdateCount>changedTick)
                        {
                            Require(path==null || !ReferenceEquals(path,previous),"Production withdraws the old Solar receiver/player premise.");
                            var prepared=GetOptional(Get(host,"World"),"preparedPath") as NpcTrajectory;Require(prepared==null || ReferenceEquals(prepared,path),"Prepared consumer cannot retain the old Solar input.");
                        }
                        if(path!=null && ReferenceEquals(path.Identity.Token,tail) && !frozen.Contains(path))
                        {
                            AssertClosure(context,head,other);closures++;frozen.Add(path);
                            if(changed && path.CaptureTick>changedTick){fresh++;pending=false;}
                        }
                        foreach(var old in frozen)
                        {
                            int age=(int)((long)Main.GameUpdateCount-old.CaptureTick);if(age<1 || age>=old.Count)continue;var point=old[age];
                            double error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),tail.position);maximum=Math.Max(maximum,error);
                            Require(error<=.08 && Math.Abs(point.Vx-tail.velocity.X)<=.0001 && Math.Abs(point.Vy-tail.velocity.Y)<=.0001 && point.CanReceive==(!tail.dontTakeDamage && !tail.immortal && !tail.friendly),"Frozen Solar scene="+scene+" frame="+frame+" age="+age+" error="+error.ToString("R")+" predicted="+point.Bounds.X+","+point.Bounds.Y+" V="+point.Vx+","+point.Vy+" actual="+tail.position+" V="+tail.velocity+" stop="+old.Stop);
                            matched++;if(age>=30){longMatches++;if(changed && old.CaptureTick>changedTick){restored++;latestRecovery++;}}
                        }
                        csv.Add(string.Join(",",scene,frame,Count(),actions,headActions,selected,matched,longMatches,ground,air,crowd,fresh,restored,withdrawn,receiving,maximum.ToString("R",CultureInfo.InvariantCulture)));
                    }
                    Require(selected>30 && actions>=480*31 && headActions>=480 && longMatches>=30 && changed && withdrawn>0 && fresh>0 && restored>=30 && !pending && latestRecovery>0 && closures>0,"Actual414 selection/complete chain/current actions and bounded latest-input recovery have useful futures.");
                    if(scene==0)Require(ground>0 && air>0,"Actual legal player relocation consumes both Solar ground policies.");
                    if(scene==1)Require(other.active && crowd>30 && headActions>=960,"Both active head motors and native slot-order crowd consumption are exercised.");
                    if(scene==2)
                    {
                        tail.StrikeNPCNoInteraction(head.lifeMax+100,0,0);Require(!head.active,"Real selected414 death reaches its shared owner.");invalidate();
                        for(int retire=0;retire<4 && tail.active;retire++){NativeCombatModeledImpactChecks.SampleMouse(context,tail.Center);step();}
                        Require(!tail.active && cache.Read(0)==null,"The real dead selected chain withdraws its production publication.");
                        Require(GetOptional(Get(host,"World"),"preparedPath")==null,"Prepared consumer drops the dead selected identity.");
                    }
                    Console.WriteLine("PASS Solar natural scene="+scene+" actions="+actions+" heads="+headActions+" selected="+selected+" matched="+matched+" long="+longMatches+" ground="+ground+" air="+air+" crowd="+crowd+" closures="+closures+" fresh="+fresh+" restored="+restored+" withdrawn="+withdrawn+" receiving="+receiving+" max="+maximum.ToString("R",CultureInfo.InvariantCulture));
                    before=after=null;received=null;
                }
            }
            finally{before=after=null;received=null;hooks.UnpatchAll(hooks.Id);network.UnpatchAll(network.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"solar-worm-scenes.csv"),csv);}
        }

        private static void AssertClosure(object context,NPC head,NPC other)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var rows=(NpcMotionState[])Get(source,"states");var roles=(bool[])Get(source,"motionRoles");int count=(int)Get(source,"observedCount");long session=(long)Get(host,"Session");
            int expected=0,found=0;
            foreach(var actor in Main.npc)
            {
                if(!actor.active || !Family(actor.type) || actor.realLife!=head.whoAmI && (other==null || actor.realLife!=other.whoAmI))continue;expected++;
                var identity=new NpcIdentity(session,actor,actor.whoAmI,actor.generation,actor.type,actor.netID);bool present=false;
                for(int i=0;i<count;i++)if(rows[i].Identity.Equals(identity)){Require(roles[i] && rows[i].WormRelationsCaptured && rows[i].WormRelationsComplete,"Production includes complete actual Solar MOTOR role type="+actor.type+" slot="+actor.whoAmI);present=true;found++;break;}
                Require(present,"Production cannot freeze or omit a necessary Solar chain/crowd actor slot="+actor.whoAmI);
            }
            Require(expected==(other==null?31:62) && found==expected,"Full native Solar identities survive production closure.");
        }
        private static NpcMotionState[] Capture(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");long session=(long)Get(host,"Session");var read=source.GetType().GetMethod("Read",Flags);
            var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);var rows=new List<NpcMotionState>();
            foreach(var actor in Main.npc)
            {
                if(actor==null || !actor.active && !Family(actor.type))continue;
                var n=(NpcMotionState)read.Invoke(null,new object[]{actor,session});object[] args={actor,n,Terrain(context)};tracking.Invoke(null,args);n=(NpcMotionState)args[1];
                if(Family(actor.type))
                {
                    n.WormRelationsCaptured=n.WormRelationsComplete=n.WormLinkFactsCaptured=true;
                    int parent=(int)actor.ai[1],child=(int)actor.ai[0];
                    if(parent>=0 && parent<Main.npc.Length)n.ParentIdentity=((NpcMotionState)read.Invoke(null,new object[]{Main.npc[parent],session})).Identity;
                    if(child>=0 && child<Main.npc.Length)n.ChildIdentity=((NpcMotionState)read.Invoke(null,new object[]{Main.npc[child],session})).Identity;
                }
                rows.Add(n);
            }
            return rows.ToArray();
        }
        private static void Compare(NpcMotionState n,NPC actual,string label)
        {
            double error=Vector2.Distance(new Vector2(n.X,n.Y),actual.position);
            Require(error<=.08 && Math.Abs(n.Vx-actual.velocity.X)<=.0001 && Math.Abs(n.Vy-actual.velocity.Y)<=.0001 && n.Health.RealLife==actual.realLife && n.Health.DontTakeDamage==actual.dontTakeDamage && n.CanReceive==(!actual.dontTakeDamage && !actual.immortal && !actual.friendly),"Original Solar "+label+" type="+actual.type+" error="+error.ToString("R")+" predictedV="+n.Vx.ToString("R")+","+n.Vy.ToString("R")+" actualV="+actual.velocity+" owner="+n.Health.RealLife+"/"+actual.realLife);
        }
        private static int GroundRow(Player p)
        {int px=(int)(p.Center.X/16f),py=(int)(p.Center.Y/16f);for(int x=px-2;x<=px+2;x++){for(int y=py;y<=py+15;y++)if(WorldGen.SolidTile2(x,y))return y;}return -1;}
        private static int NativeCrowd(NPC n)
        {int radius=GroundRow(Main.player[n.target])>0?400:60,count=0;foreach(var other in Main.npc)if(other.active && other.type==412 && other!=n && Vector2.Distance(other.Center,n.Center)<radius)count++;return count;}
        private static NPC Birth(int x,int y,int start)
        {int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),x,y,412,Start:start);Require(slot>=0 && slot<Main.maxNPCs && Main.npc[slot].active,"Original Solar NewNPC obtains a legal head slot.");return Main.npc[slot];}
        private static void Spawn(NPC head)
        {for(int frame=0;frame<2;frame++)NativeSlots();int pieces=0;foreach(var n in Main.npc)if(n.active && Family(n.type) && n.realLife==head.whoAmI){Require(Finite(n.position.X) && Finite(n.position.Y) && Finite(n.velocity.X) && Finite(n.velocity.Y),"Original full-slot Solar birth yields finite geometry.");pieces++;}Require(pieces==31,"Actual fixed Solar birth is one412,29body413 and one414, not a guessed chain count.");}
        private static void NativeSlots()
        {NativeQuickItemChecks.BeginWorldStep();Main.EverLastingTicker++;NPC.UpdateProtectedSpawnSlots();for(int slot=0;slot<Main.maxNPCs;slot++)if(Main.npc[slot].active && Family(Main.npc[slot].type))Main.npc[slot].UpdateNPC(slot);}
        private static bool Finite(float value){return !float.IsNaN(value) && !float.IsInfinity(value);}
        private static NPC Tail(NPC head){foreach(var n in Main.npc)if(n.active && n.type==414 && n.realLife==head.whoAmI)return n;return null;}
        private static int Count(){int count=0;foreach(var n in Main.npc)if(n.active && Family(n.type))count++;return count;}
        private static int Index(NpcMotionState[] rows,NPC n){for(int i=0;i<rows.Length;i++)if(ReferenceEquals(rows[i].Identity.Token,n))return i;return -1;}
        private static void Setup(object context)
        {
            typeof(NativeCombatSegmentedWormChecks).GetMethod("Setup",Flags).Invoke(null,new object[]{context});
            // This arena starts outside the Lunar event. These are the actual
            // neutral-world counter defaults, not a skipped death/effect call.
            NPC.ShieldStrengthTowerSolar=0;NPC.TowerActiveSolar=false;
        }
        private static IPredictionTerrain Terrain(object context){return (IPredictionTerrain)Get(Get(Get(context,"CombatObservation"),"Prediction"),"Terrain");}
        private static PredictionEnvironment EnvironmentFor(NPC n){return (PredictionEnvironment)typeof(NativeCombatSegmentedWormChecks).GetMethod("EnvironmentFor",Flags).Invoke(null,new object[]{n});}
    }
}
