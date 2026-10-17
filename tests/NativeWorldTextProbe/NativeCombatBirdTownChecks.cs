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
    // A necessary input slice, not another replay of the R55/R71 bird set.
    // Unknown Town geometry stays unavailable even in the positive case.
    internal static class NativeCombatBirdTownChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static readonly HashSet<NPC> outlets=new HashSet<NPC>();
        private static Action<NPC> before,after;
        private static NPC countedTown;
        private static int townQueries;
        private static bool candidateSampling,candidateInputCaptured;
        private static PredictionEnvironment candidateEnvironment;
        private static PredictionPlayerMotion candidatePlayer;
        private static void RollingInput(PredictionEnvironment environment,PredictionPlayerMotion player)
        {if(candidateSampling){candidateEnvironment=environment;candidatePlayer=player;candidateInputCaptured=true;}}
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static void Hit(Entity __1){if(ReferenceEquals(__1,countedTown))townQueries++;}
        private static bool Network(int msgType,int number)
        {
            if((msgType!=23 && msgType!=54) || number<0 || number>=Main.npc.Length)return true;
            return !outlets.Contains(Main.npc[number]);
        }
        private static object Existing(string name,params object[] args){return typeof(NativeCombatBirdChecks).GetMethod(name,Flags).Invoke(null,args);}
        private static void Setup(object context)
        {Existing("Setup",context);Main.dayTime=false;Main.cloudAlpha=0;outlets.Clear();}
        private static NPC Birth(int type,int slot,Vector2 position)
        {var n=(NPC)Existing("Birth",type,slot,position);outlets.Add(n);return n;}
        private static int Index(NpcMotionState[] rows,NPC n)
        {for(int i=0;i<rows.Length;i++)if(ReferenceEquals(rows[i].Identity.Token,n))return i;throw new InvalidOperationException("Missing original bird-town identity");}
        private static PredictionEnvironment Env(NPC n){return (PredictionEnvironment)Existing("EnvironmentFor",n);}
        private static NpcMotionState[] Current(object context,NPC n,NPC town)
        {
            var rows=(NpcMotionState[])Existing("Capture",context,n);
            int at=Index(rows,town);rows[at].BirdRelationsCaptured=true;rows[at].BirdRelationsComplete=false;
            return rows;
        }
        private static bool Matches(NpcMotionState n,NPC actual)
        {return Vector2.Distance(new Vector2(n.X,n.Y),actual.position)<=.08 && Math.Abs(n.Vx-actual.velocity.X)<=.0001 && Math.Abs(n.Vy-actual.velocity.Y)<=.0001 && n.A0==actual.ai[0] && n.NoGravity==actual.noGravity;}
        private static object Owner(object context)
        {var type=Get(Get(context,"CombatObservation"),"Prediction").GetType();return Activator.CreateInstance(type,Flags,null,new object[]{null,false},CultureInfo.InvariantCulture);}
        private static NpcIdentity Identity(object context,NPC n)
        {
            var type=Get(Get(context,"CombatObservation"),"Prediction").GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true);
            return (NpcIdentity)type.GetMethod("Identity",Flags).Invoke(null,new object[]{n,(long)Get(Get(context,"CombatObservation"),"Session")});
        }
        private static NpcTrajectory Candidate(object context,object owner,NPC selected)
        {
            candidateInputCaptured=false;candidateSampling=true;
            try{return (NpcTrajectory)owner.GetType().GetMethod("TryCandidate",Flags).Invoke(owner,new object[]{Identity(context,selected),(long)Main.GameUpdateCount,false,120});}
            finally{candidateSampling=false;}
        }
        private static void RequireUnavailable(object owner,NPC town)
        {
            var source=Get(owner,"candidateSource");int count=(int)Get(source,"observedCount");var all=(NpcMotionState[])Get(source,"states");var roles=(bool[])Get(source,"motionRoles");
            int at=-1;for(int i=0;i<count;i++)if(ReferenceEquals(all[i].Identity.Token,town))at=i;
            Require(at>=0 && all[at].Town && all[at].BirdRelationsCaptured && !all[at].BirdRelationsComplete && !roles[at],"production town stays unavailable health role; positive path does not certify town motor");
        }
        private static string Facts(object value,params string[] names)
        {
            if(value==null)return "null";var result=new List<string>();
            foreach(string name in names)
            {
                var field=value.GetType().GetField(name,Flags);var property=value.GetType().GetProperty(name,Flags);
                object fact=field!=null?field.GetValue(value):property!=null?property.GetValue(value,null):null;
                string text=fact is float?((float)fact).ToString("R",CultureInfo.InvariantCulture):fact is double?((double)fact).ToString("R",CultureInfo.InvariantCulture):Convert.ToString(fact,CultureInfo.InvariantCulture);
                result.Add(name+"="+(text??"null"));
            }
            return string.Join(" ",result);
        }
        private static void PrefixFailure(object owner,NPC selected,NPC peer,int frame,NpcTrajectory path)
        {
            var source=Get(owner,"candidateSource");var cache=Get(source,"Cache");var raw=(NpcTrajectory)GetOptional(cache,"result");var rolling=Get(source,"rolling");
            Console.WriteLine("BIRD TOWN prefix diagnostic frame="+frame+" tick="+Main.GameUpdateCount+" acceptedCount="+(path?.Count??0)+" acceptedStop="+path?.Stop+" rawCount="+(raw?.Count??0)+" rawStop="+raw?.Stop+" failureLayer="+Get(rolling,"FailureLayer")+" source "+Facts(source,"outcomeTick","outcomeStop","outcomeLayer","outcomeFuture","observedCount","targetPlayer"));
            int count=(int)Get(source,"observedCount");var initial=(NpcMotionState[])Get(source,"states");var roles=(bool[])Get(source,"motionRoles");var work=(NpcMotionState[])Get(rolling,"work");
            for(int i=0;i<count;i++)
            {
                Console.WriteLine("BIRD TOWN sampled slot="+initial[i].Identity.Slot+" type="+initial[i].EffectiveType+" motor="+roles[i]+" "+Facts(initial[i],"Active","CanReceive","Friendly","Life","LifeMax","X","Y","Vx","Vy","Width","Height","A0","A1","L0","L1","L2","L3","NoGravity","NoTileCollide","Target","PlayerIndex","HasPlayer","BirdRelationsCaptured","BirdRelationsComplete","Town"));
                Console.WriteLine("BIRD TOWN stopped slot="+work[i].Identity.Slot+" "+Facts(work[i],"Active","X","Y","Vx","Vy","A0","A1","L0","NoGravity","NoTileCollide","Target","PlayerIndex"));
            }
            Console.WriteLine("BIRD TOWN candidateInputCaptured="+candidateInputCaptured+" candidateEnvironment "+Facts(candidateInputCaptured?(object)candidateEnvironment:null,"PlayerIndex","PlayerX","PlayerY","PlayerVx","PlayerVy","PlayerWidth","PlayerHeight","PlayerTimelineActive","Day","CloudAlpha","WorldWidth","WorldHeight","Multiplayer"));
            Console.WriteLine("BIRD TOWN candidatePlayer "+Facts(candidateInputCaptured?(object)candidatePlayer:null,"PlayerIndex","X","Y","Vx","Vy","Width","Height","Complex","Mechanism","Grounded","Gravity","GravityDirection","ForcedGravityTicks","GravitySourceExpected","WallSourceExpected"));
            var p=Main.player[Main.myPlayer];Console.WriteLine("BIRD TOWN nativePlayer "+Facts(p,"whoAmI","active","dead","ghost","position","velocity","width","height","isControlledByFilm","gravity","gravDir","jump","fallStart","controlLeft","controlRight")+" selectedNative="+Facts(selected,"active","friendly","dontTakeDamage","immortal","life","catchableNPCTempImmunityCounter","releaseOwner")+" selectedSlot="+selected.whoAmI+" selectedPosition="+selected.position+" selectedA0="+selected.ai[0]+" selectedL0="+selected.localAI[0]+" peerSlot="+peer.whoAmI+" peerPosition="+peer.position+" peerA0="+peer.ai[0]+" peerL0="+peer.localAI[0]);
        }
        internal static void Run(object context,Action step,string output)
        {
            var audit=new Harmony("JueMingR.Probe.BirdTownCensus");var playerArray=Main.player;var players=(Player[])playerArray.Clone();var active=new bool[players.Length];for(int i=0;i<players.Length;i++)active[i]=players[i]!=null && players[i].active;
            bool day=Main.dayTime,good=Main.getGoodWorld;float cloud=Main.cloudAlpha;int mode=Main.netMode,game=Main.GameMode;
            var outlet=new HarmonyMethod(typeof(NativeCombatBirdTownChecks),nameof(Network));outlet.priority=Priority.First+100;
            audit.Patch(typeof(NetMessage).GetMethod("SendData",Flags),prefix:outlet);
            audit.Patch(typeof(NPC).GetMethod("UpdateNPC",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBirdTownChecks),nameof(Before)),postfix:new HarmonyMethod(typeof(NativeCombatBirdTownChecks),nameof(After)));
            audit.Patch(typeof(RollingNpcPrediction).GetMethod("Prepare",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBirdTownChecks),nameof(RollingInput)));
            audit.Patch(typeof(Collision).GetMethod("CanHit",Flags,null,new[]{typeof(Entity),typeof(Entity)},null),prefix:new HarmonyMethod(typeof(NativeCombatBirdTownChecks),nameof(Hit)));
            try{Scalars(context);Existing("SourceClosure",context);Natural(context,step,output);}
            finally
            {
                before=after=null;countedTown=null;candidateSampling=candidateInputCaptured=false;audit.UnpatchAll(audit.Id);outlets.Clear();Main.netMode=mode;Main.GameMode=game;Main.dayTime=day;Main.cloudAlpha=cloud;Main.getGoodWorld=good;
                Main.player=playerArray;Array.Copy(players,playerArray,players.Length);for(int i=0;i<players.Length;i++)if(players[i]!=null)players[i].active=active[i];
                NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());
            }
        }
        private static void Scalars(object context)
        {
            // 0 Town before proof peer; 1 proof peer before Town; 2 strict320;
            // 3 observed peer flight; 4 no peer; 5 unrelated far Town.
            // 689 separately protects the same census after its nearest query.
            int actions=0;
            foreach(int variant in new[]{0,1,2,3,4,5,6,7})
            {
                Setup(context);int member=variant>=6?689:611;bool positive=variant==0 || variant==1 || variant==6;
                var town=Birth(17,variant==1?30:0,new Vector2(1600,2390));var n=Birth(member,50,new Vector2(1600,2200));
                NPC peer=null;if(variant!=4)peer=Birth(member,variant==1?0:30,new Vector2(1640,2200));
                Require(peer==null || (variant==1?peer.whoAmI<town.whoAmI:town.whoAmI<peer.whoAmI),"actual native allocated slots preserve the tested scan order");
                n.ai[0]=1;n.localAI[0]=1;n.velocity.Y=-.75f;
                if(peer!=null){peer.localAI[0]=1;peer.ai[0]=variant==3 || variant==5 || variant==7?1:0;if(variant==2)peer.Center=new Vector2(n.Center.X+320,peer.Center.Y);}
                if(variant==5)town.position.X=n.position.X+500;
                for(int x=90;x<=115;x++)NativeToolsChecks.Tile(x,142,1);
                Require(town.townNPC && (variant==5 || !Collision.CanHit(n,town)),"real Town17 opaque line determines landing whenever that geometry is actually necessary");
                var source=Get(Get(context,"CombatObservation"),"Prediction");var terrain=(IPredictionTerrain)Get(source,"Terrain");terrain.GetType().GetMethod("Reset",Flags).Invoke(terrain,null);
                var rows=Current(context,n,town);var value=rows[Index(rows,n)];PredictionStop stop;
                bool valid=NpcMotion.Step(ref value,rows,rows.Length,Env(n),terrain,1,true,out stop);
                Require(valid==positive && (valid || stop==PredictionStop.MissingDependency),"only a proved peer removes overwritten Town geometry; strict320/state1/no-peer remain Missing variant="+variant);
                // Exactly one actual native action. No temporary original AI
                // obtains the future Town geometry for the model.
                countedTown=town;townQueries=0;n.UpdateNPC(n.whoAmI);countedTown=null;
                Require(townQueries==(variant==1 || variant==5?0:1),"original CanHit is actually consumed before later proof peer, and skipped after earlier break/far Town: "+variant);
                Require(n.ai[0]==(positive || variant==5?1:2),"real slot scan final phase agrees with overwrite or necessary blocked Town: "+variant);
                if(valid)Require(Matches(value,n),"proved scan preserves the original full current motor: "+variant);
                Console.WriteLine("BIRD TOWN current variant="+variant+" member="+member+" valid="+valid+" stop="+stop+" originalPhase="+n.ai[0]+" peerSlot="+peer?.whoAmI+" townSlot="+town.whoAmI+" originalTownQueries="+townQueries);actions++;
            }
            Console.WriteLine("PASS BIRD TOWN actual current slices="+actions+" noFrozenTown=True");
        }
        private static void Natural(object context,Action step,string output)
        {
            Setup(context);var n=Birth(611,16,new Vector2(1650,2200));var town=Birth(17,0,new Vector2(1650,2390));var peer=Birth(611,30,new Vector2(1790,2400-34));
            // These actual catchable births first execute their native90-clock
            // friendly protection. No selected forecast is owed in that phase.
            // The single observed scene input is applied only AFTER the native
            // release action; the following64 actions are all original updates.
            for(int x=90;x<=115;x++)NativeToolsChecks.Tile(x,142,1);
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions(path:false,mouseCenter:true,clearLine:false,radius:50));
            var owner=Owner(context);var terrain=(IPredictionTerrain)Get(owner,"Terrain");var frozen=new List<NpcTrajectory>();var records=new List<string>{"frame,x,y,phase,peerPhase,townX,townY,count,stop,matches,long,rawCount,rawStop,failureLayer"};int actions=0,matched=0,longMatches=0,townMoves=0;Vector2 oldTown=town.position;NpcMotionState expected=default(NpcMotionState);
            try
            {
                int warmup=0,protectedActions=0;
                Require(((int)Get(n,"catchableNPCTempImmunityCounter"))==90 && n.friendly && ((int)Get(peer,"catchableNPCTempImmunityCounter"))==90 && peer.friendly,"actual SetDefaults catchable birth has native90-clock protection");
                for(int action=1;action<=91;action++)
                {
                    step();warmup++;
                    Require(n.active && peer.active,"actual protection warmup preserves both native bird identities");
                    int counter=Math.Max(90-action,0);
                    Require(((int)Get(n,"catchableNPCTempImmunityCounter"))==counter && ((int)Get(peer,"catchableNPCTempImmunityCounter"))==counter,"original UpdateNPC consumes the catchable clock once per world action");
                    if(action<=90)
                    {
                        Require(n.friendly && peer.friendly,"the counter-zero90th action still executes the native protected branch");
                        Require(Candidate(context,owner,n)==null,"strict Source does not publish a selected future for the actually friendly newborn");protectedActions++;
                    }
                    else Require(!n.friendly && !peer.friendly,"the actual next native action restores damage qualification, without setting friendly/counter");
                    Console.WriteLine("BIRD TOWN qualification action="+action+" counter="+((int)Get(n,"catchableNPCTempImmunityCounter"))+" friendly="+n.friendly+" peerCounter="+((int)Get(peer,"catchableNPCTempImmunityCounter"))+" peerFriendly="+peer.friendly);
                }
                Require(warmup==91 && protectedActions==90,"qualification actions and predicted natural actions have separate actual denominators");
                var qualified=(NpcMotionState)owner.GetType().GetMethod("Read",Flags).Invoke(null,new object[]{n,Identity(context,n).Session});
                Require(qualified.CanReceive,"actual production receiving qualification is true after native immunity release");
                // Exactly one explicit observed flying/settled input, matching
                // the original planned scene. No state is held during its64
                // subsequent world actions, and unknown Town stays unknown.
                n.position=new Vector2(1650,2200);n.velocity=Vector2.Zero;n.ai[0]=1;n.localAI[0]=1;
                town.position=new Vector2(1650,2390);
                peer.position=new Vector2(1790,2400-peer.height);peer.velocity=Vector2.Zero;peer.ai[0]=0;peer.localAI[0]=1;
                before=actor=>
                {
                    if(!ReferenceEquals(actor,n))return;terrain.GetType().GetMethod("Reset",Flags).Invoke(terrain,null);
                    var rows=Current(context,n,town);expected=rows[Index(rows,n)];PredictionStop stop;
                    Require(NpcMotion.Step(ref expected,rows,rows.Length,Env(n),terrain,1,true,out stop),"actual-prefix group, with unavailable Town, proves this original bird action: "+stop);
                };
                after=actor=>{if(ReferenceEquals(actor,n)){Require(Matches(expected,n),"actual bird motor remains exact while real Town performs its own AI");actions++;}};
                for(int frame=0;frame<64;frame++)
                {
                    step();if(town.position!=oldTown){townMoves++;oldTown=town.position;}
                    var path=Candidate(context,owner,n);RequireUnavailable(owner,town);
                    var sampled=Get(owner,"candidateSource");var raw=(NpcTrajectory)GetOptional(Get(sampled,"Cache"),"result");var layer=Get(Get(sampled,"rolling"),"FailureLayer");
                    // Persist the actual rejected prefix BEFORE its unchanged
                    // useful-window assertion; a failed frame is evidence too.
                    records.Add(string.Join(",",frame,n.position.X.ToString("R"),n.position.Y.ToString("R"),n.ai[0],peer.ai[0],town.position.X.ToString("R"),town.position.Y.ToString("R"),path?.Count??0,path?.Stop.ToString()??"null",matched,longMatches,raw?.Count??0,raw?.Stop.ToString()??"null",layer));
                    if(path==null || path.Count<=30)PrefixFailure(owner,n,peer,frame,path);
                    Require(path!=null && path.Count>30,"actual Source publishes a useful known prefix without freezing Town geometry");if(frame%8==0)frozen.Add(path);
                    for(int i=frozen.Count-1;i>=0;i--)
                    {
                        var prior=frozen[i];int offset=(int)(Main.GameUpdateCount-prior.CaptureTick);if(offset>=prior.Count){frozen.RemoveAt(i);continue;}if(offset<1)continue;
                        Require(Vector2.Distance(new Vector2(prior[offset].Bounds.X,prior[offset].Bounds.Y),n.position)<=.08,"frozen proved-peer bird path matches the original scene frame="+frame+" offset="+offset);matched++;if(offset>=30)longMatches++;
                    }
                    records[records.Count-1]=string.Join(",",frame,n.position.X.ToString("R"),n.position.Y.ToString("R"),n.ai[0],peer.ai[0],town.position.X.ToString("R"),town.position.Y.ToString("R"),path.Count,path.Stop,matched,longMatches,raw?.Count??0,raw?.Stop.ToString()??"null",layer);
                }
                before=after=null;Require(actions==64 && matched>0 && longMatches>0,"finite natural prefix has genuine current and long frozen evidence");
                Console.WriteLine("PASS BIRD TOWN qualificationActions="+warmup+" protectedSourceNull="+protectedActions+" naturalActions="+actions+" matched="+matched+" long="+longMatches+" townMovedActions="+townMoves+" townMotorStillUnknown=True");
            }
            finally
            {before=after=null;File.WriteAllLines(Path.Combine(output,"bird-town-census.csv"),records);owner.GetType().GetMethod("Clear",Flags).Invoke(owner,null);}
        }
    }
}
