using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatHarpoonChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static Projectile watched;private static NPC receiver;private static int hits;private static long firstHit;
        private static readonly List<Rectangle> nativeBodies=new List<Rectangle>();
        private static void Body(Projectile __instance,Rectangle __result){if(ReferenceEquals(watched,__instance))nativeBodies.Add(__result);}
        private static void Strike(NPC __instance,int __result){if(ReferenceEquals(receiver,__instance) && __result>0){hits++;if(firstHit<0)firstHit=Main.GameUpdateCount;}}
        internal static void Run(object context)
        {
            var audit=new Harmony("JueMingR.Tests.HarpoonStages");
            audit.Patch(typeof(Projectile).GetMethod("Damage_GetHitbox",Flags),postfix:new HarmonyMethod(typeof(NativeCombatHarpoonChecks).GetMethod(nameof(Body),Flags)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatHarpoonChecks).GetMethod(nameof(Strike),Flags)));
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks).GetMethod("SkipAchievement",Flags)));
            bool wind=Main.windPhysics,remix=Main.remixWorld;
            try
            {
                Main.windPhysics=false;var failures=new List<string>();
                foreach(var part in new Action[]{()=>MotorWorld(context),()=>Natural(context),()=>AlreadyFlying(context)})try{part();}catch(Exception error){Console.WriteLine("HARPOON GROUP FAIL "+error);failures.Add(error.Message);}
                Require(failures.Count==0,"harpoon independent groups: "+string.Join(" | ",failures));
            }
            finally{watched=null;receiver=null;nativeBodies.Clear();Main.windPhysics=wind;Main.remixWorld=remix;NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());audit.UnpatchAll(audit.Id);}
        }
        private static void MotorWorld(object context)
        {
            // A held harpoon travels 1000px before returning; its late gravity
            // arc extends below the small 120-tile tool fixture's known scan
            // area. Give that actual range room without weakening terrain
            // validity or altering the native motor/catch rules.
            var tiles=Main.tile;int height=Main.maxTilesY;var world=Main.ActiveWorldFileData;float bottom=Main.bottomWorld;
            try
            {
                // Resizing the fixture is a different world. Reusing its old
                // UUID would reopen a valid footprint archive with conflicting
                // dimensions during the ordinary shared-context updates.
                Main.ActiveWorldFileData=new Terraria.IO.WorldFileData{UniqueId=Guid.NewGuid()};
                Main.maxTilesY=220;Main.bottomWorld=Main.maxTilesY*16;Main.tile=new Tile[Main.maxTilesX,Main.maxTilesY];
                for(int x=0;x<Main.maxTilesX;x++)for(int y=0;y<Main.maxTilesY;y++)Main.tile[x,y]=new Tile();
                Motor(context);
            }
            finally{Main.tile=tiles;Main.maxTilesY=height;Main.bottomWorld=bottom;Main.ActiveWorldFileData=world;}
        }
        private static void Motor(object context)
        {
            var combat=Get(context,"Combat");var input=Get(context,"Input");var attack=Get(combat,"Attack");var terrain=Get(attack,"terrain");
            var model=attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostHarpoonAttack",true);var advance=model.GetMethod("Advance",Flags);var body=model.GetMethod("DamageBody",Flags);var clone=model.GetMethod("Copy",Flags);
            int actions=0,returns=0,catches=0,wide=0;var failures=new List<string>();
            foreach(int type in new[]{23,262,271,273,481})foreach(int scene in Enumerable.Range(0,12))
            {
                try
                {
                    NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,160,0,0);p.position=new Vector2(700,630);p.ResetEffects();p.controlUseItem=true;
                    for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();
                    if(scene==1 || scene==2 || scene==5)for(int y=10;y<80;y++)NativeToolsChecks.Tile(62,y,1);
                    if(scene==2 || scene==3 || scene==4)for(int x=35;x<110;x++)for(int y=25;y<80;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==4?2:0);}
                    if(scene==6 || scene==7){NativeToolsChecks.Tile(62,40,1);Main.tile[62,40].slope((byte)(scene==6?1:4));}
                    Main.remixWorld=scene==8;
                    var origin=scene==9 || scene==10?new Vector2(scene==9?900:800,650):p.Center;
                    int slot=Projectile.NewProjectile(new EntitySource_DebugCommand(),origin,new Vector2(14,scene==5 || scene==6 || scene==7?3:-.4f),type,1,0,0);var q=Main.projectile[slot];
                    if(scene==9 || scene==10)q.ai[0]=1;
                    watched=q;var shadow=(Projectile)clone.Invoke(null,new object[]{q});Call(terrain,"Reset");
                    for(int frame=0;frame<180 && q.active;frame++)
                    {
                        if(scene==11 && frame==15)p.controlUseItem=false;
                        var expected=new List<Rectangle>();bool alive=true;int updates=shadow.extraUpdates+1;
                        for(int sub=0;sub<updates && alive;sub++)
                        {
                            object[] args={shadow,p,terrain,false};Require((bool)advance.Invoke(null,args),"AI13 terrain finite: type="+type+" scene="+scene+" frame="+frame+" center="+shadow.Center+" velocity="+shadow.velocity+" phase="+shadow.ai[0]+","+shadow.ai[1]);alive=(bool)args[3];if(!alive)break;
                            var rectangle=(Rectangle)body.Invoke(null,new object[]{shadow});expected.Add(rectangle);if(rectangle.Width==200)wide++;shadow.timeLeft--;
                        }
                        float prior=q.ai[0];nativeBodies.Clear();q.Update(slot);actions++;
                        if(prior==0 && q.ai[0]==1)returns++;
                        string detail=" type="+type+" scene="+scene+" frame="+frame+" actual="+q.Center+"/"+q.velocity+" phase="+q.ai[0]+","+q.ai[1]+" local="+q.localAI[0]+" model="+shadow.Center+"/"+shadow.velocity+" phase="+shadow.ai[0]+","+shadow.ai[1]+" local="+shadow.localAI[0];
                        Require(alive==q.active,"AI13 original release/range/catch lifetime"+detail);
                        if(!alive){catches++;break;}
                        Require(Vector2.DistanceSquared(q.Center,shadow.Center)<.001f && Vector2.DistanceSquared(q.velocity,shadow.velocity)<.00001f && q.ai[0]==shadow.ai[0] && q.ai[1]==shadow.ai[1] && q.localAI[0]==shadow.localAI[0] && q.extraUpdates==shadow.extraUpdates && q.tileCollide==shadow.tileCollide && q.timeLeft==shadow.timeLeft,"AI13 original complete action"+detail);
                        Require(nativeBodies.Count==expected.Count && nativeBodies.SequenceEqual(expected),"AI13 actual per-action receive bodies"+detail+" counts="+nativeBodies.Count+"/"+expected.Count);
                    }
                }
                catch(Exception error){failures.Add(type+"/"+scene+": "+error.Message);Console.WriteLine("HARPOON MOTOR FAIL "+type+"/"+scene+": "+error);}
            }
            Main.remixWorld=false;watched=null;
            Require(failures.Count==0,"AI13 native motors failures="+failures.Count+": "+string.Join(" | ",failures));
            Require(returns>=40 && catches>=50 && wide>0,"native range/release/terrain returns, catches and KO hitbox exercised");
            Console.WriteLine("PASS AI13 native motors 60 cases actions="+actions+" returns="+returns+" catches="+catches+" wide="+wide);
        }
        private static void AlreadyFlying(object context)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");var input=Get(context,"Input");int cases=0,contacts=0,blocked=0,relaunches=0;
            foreach(var row in new[]{new[]{160,23},new[]{1297,262},new[]{1314,271},new[]{1325,273},new[]{3012,481}})
            foreach(bool wrongDirection in new[]{false,true})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,row[0],0,0);p.position=new Vector2(700,646);p.ResetFallDamage();p.ResetEffects();p.releaseUseItem=true;Main.screenPosition=new Vector2(500,400);
                receiver=NativeCombatControlPreviewChecks.Dummy(2,wrongDirection?33:52,40);hits=0;firstHit=-1;
                // This is a real hand birth with observation entirely off.
                // Enabling the path later cannot invent a causal input receipt.
                NativeCombatSelectionPreviewChecks.Step(context,wrongDirection?p.Center+new Vector2(300,0):receiver.Center,true);
                var born=Main.projectile.Single(q=>q.active && q.owner==p.whoAmI && q.type==row[1]);int key=(int)born.key;
                var receipts=(System.Collections.IDictionary)Get(Get(attack,"Control"),"shots");
                Require(!receipts.Contains(key),"original off-state hand birth has no observed source receipt: "+row[1]);
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,false));
                NativeCombatSelectionPreviewChecks.Step(context,receiver.Center,true,true);
                Require(GetOptional(attack,"ExpectedPreview")==null,"withdrawn idle preview cannot be revived by an observed harpoon");
                Require(!receipts.Contains(key),"old harpoon observation must not fabricate this member's input-source receipt: "+row[1]);
                bool opening=wrongDirection && row[1]==481;
                var observed=Observe(attack,p,receiver);var contact=observed==null?null:Get(observed,"Contact") as AttackContact;
                if(wrongDirection && !opening)
                {
                    Require(contact==null,"a wrong-way live harpoon owns native dummy timers; no fictitious observed receive: "+row[1]);blocked++;cases++;continue;
                }
                long predicted;
                if(opening)
                {
                    Require(contact==null,"the wrong-way old Golem fist is not the new hand's useful contact");
                    // Golem permits another real launch while its old fist is
                    // alive. That is a pressed hand, never an observation-only
                    // input authority or an idle-preview replacement.
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,true));
                    NativeToolExecutionChecks.Sample(context,input,receiver.Center,true);Call(combat,"Sample");
                    NativeCombatAimChecks.PrepareMechanismFixture(host,attack,receiver,0);
                    contact=(AttackContact)GetOptional(attack,"ExpectedImpact");
                    predicted=-1;
                }
                else
                {
                    Require(contact!=null && (bool)Get(observed,"Current") && (bool)Get(observed,"DependenciesCurrent"),"enabling path observes the sealed natural flight with its full lease: "+row[1]);
                    predicted=contact.Timeline.SampleTick+contact.Tick;
                }
                bool second=false,enhanced=false;
                for(int frame=0;frame<90 && hits==0;frame++)
                {
                    if(opening && !second)
                    {
                        NativeToolExecutionChecks.Sample(context,input,receiver.Center,true);Call(combat,"Sample");
                        NativeCombatAimChecks.PrepareMechanismFixture(host,attack,receiver,0);contact=(AttackContact)GetOptional(attack,"ExpectedImpact");
                        if(contact!=null)predicted=contact.Timeline.SampleTick+contact.Tick;
                    }
                    NativeCombatSelectionPreviewChecks.Step(context,receiver.Center,true,true);
                    bool newBorn=Main.projectile.Any(q=>q.active && q.owner==p.whoAmI && q.type==row[1] && (int)q.key!=key);
                    if(newBorn && !second)enhanced=(bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced";
                    second|=newBorn;
                }
                Require(hits>0 && firstHit==predicted,"observed flight / legal Golem relaunch agrees with actual Damage: "+row[1]+" opening="+opening+" predicted="+predicted+" native="+firstHit);
                if(opening){Require(second && enhanced,"GolemFist actually consumes Enhanced input to launch another original fist while the older one flies");relaunches++;}
                contacts++;cases++;
            }
            Require(cases==10 && contacts==6 && blocked==4 && relaunches==1,"existing-flight matrix retains all five hand roles and the distinct Golem relaunch");
            Console.WriteLine("PASS harpoon enabled after natural birth cases="+cases+" useful="+contacts+" blocked opening="+blocked+" original relaunch="+relaunches);
        }
        private static object Observe(object attack,Player player,NPC target)
        {
            // This immutable component path is sampled at the completed native
            // world boundary. The actual public Flight reader owns actor,
            // terrain and receive leases; it never creates a hand receipt.
            var identity=new NpcIdentity(1,target,target.whoAmI,target.generation,target.type,target.netID);
            var state=new NpcMotionState{Identity=identity,X=target.position.X,Y=target.position.Y,Width=target.width,Height=target.height,Active=true,CanReceive=!target.dontTakeDamage,NoTileCollide=target.noTileCollide};
            var points=Enumerable.Range(0,121).Select(i=>new NpcTrajectoryPoint(i,state)).ToArray();var path=new NpcTrajectory(identity,Main.GameUpdateCount,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length);
            var phase=Enum.Parse(attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");var args=new object[]{player,path,phase,false};
            object result;using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))result=Call(attack,"ReadFlight",args);
            Require((bool)args[3],"public Flight recognizes the already-born harpoon");return result;
        }
        private static void Natural(object context)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");var input=Get(context,"Input");var failures=new List<string>();int completed=0;
            foreach(var row in new[]{new[]{160,23},new[]{1297,262},new[]{1314,271},new[]{1325,273},new[]{3012,481}})
            {
                try
                {
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,row[0],0,0);p.position=new Vector2(700,646);p.fallStart=p.fallStart2=(int)(p.position.Y/16);p.ResetEffects();p.releaseUseItem=true;Main.screenPosition=new Vector2(600,400);Main.rand=new Terraria.Utilities.UnifiedRandom(90261);
                    receiver=NativeCombatControlPreviewChecks.Dummy(2,52,40);hits=0;firstHit=-1;
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,false));
                    for(int i=0;i<3;i++){NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,650),false,true);Require(GetOptional(attack,"ExpectedPreview")==null && !Main.projectile.Any(q=>q.active),"withdrawn idle harpoon preview performs no attack type="+row[1]);}
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,true));
                    NativeToolExecutionChecks.Sample(context,input,new Vector2(600,650),true);Call(combat,"Sample");
                    NativeCombatAimChecks.PrepareMechanismFixture(host,attack,receiver,0);var contact=(AttackContact)GetOptional(attack,"ExpectedImpact");
                    Require(contact!=null,"real pressed harpoon hand has a prepared receive type="+row[1]);
                    long start=Main.GameUpdateCount;bool enhanced=false;var keys=new HashSet<int>();
                    for(int frame=0;frame<100 && hits==0;frame++)
                    {
                        NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,650),true);
                        foreach(var q in Main.projectile.Where(q=>q.active && q.type==row[1] && q.owner==0))keys.Add((int)q.key);
                        if(keys.Count>0 && (bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced")enhanced=true;
                    }
                    Require(keys.Count>0 && hits>0 && enhanced,"harpoon real source birth consumes Enhanced and selected dummy receives type="+row[1]);
                    Require(firstHit-start==contact.Tick,"harpoon consumed component preparation equals actual first Damage type="+row[1]+" plan="+contact.Tick+" hit="+(firstHit-start));
                    NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,650),false);
                    int returning=0;for(int frame=0;frame<100 && Main.projectile.Any(q=>q.active && keys.Contains((int)q.key));frame++){NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,650),false);returning++;}
                    Require(!Main.projectile.Any(q=>q.active && keys.Contains((int)q.key)),"real release eventually catches original harpoon/fist");
                    Console.WriteLine("HARPOON natural item="+row[0]+" type="+row[1]+" preparedTick="+contact.Tick+" firstDamage="+(firstHit-start)+" enhanced="+enhanced+" return="+returning);completed++;
                }
                catch(Exception error){failures.Add(row[0]+": "+error.Message);Console.WriteLine("HARPOON NATURAL FAIL "+row[0]+": "+error);}
            }
            Require(failures.Count==0,"harpoon natural failures="+failures.Count+": "+string.Join(" | ",failures));Console.WriteLine("PASS harpoon/fist natural first-use/release members="+completed);
        }
    }
}
