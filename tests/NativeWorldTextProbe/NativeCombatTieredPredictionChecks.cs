using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using static NativeWorldTextProbe.NativeCombatAttackMechanismChecks;

namespace NativeWorldTextProbe
{
    // Finite checks for the shared cache and actual private prediction image.
    // No result, missing-page answer or observed history is substituted.
    internal static class NativeCombatTieredPredictionChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Private(Assembly host,object sandbox,string output)
        {
            var cache=new NpcPredictionCache();cache.Demand(0,30);cache.Demand(1,120);
            var identity=new NpcIdentity(1,new object(),0,0,2,2);
            var points=new NpcTrajectoryPoint[61];
            for(int i=0;i<points.Length;i++)points[i]=new NpcTrajectoryPoint(i,new NpcMotionState{Identity=identity,X=i,Width=20,Height=20,Active=true});
            var shortPath=new NpcTrajectory(identity,100,1,PredictionAssumption.None,PredictionStop.None,points,points.Length,PredictionStrategy.NativeIsolated);
            cache.Publish(shortPath);
            Require(cache.Read(0)==shortPath,"The short-demand consumer receives a real short path.");
            Require(cache.Read(1)==null,"A current+60 native result must never satisfy a strict current+120 consumer.");
            // Reflection lets this behavioral regression run against the sealed
            // preceding binary without a missing-new-method loader failure.
            var demand=typeof(NpcPredictionCache).GetMethod("Demand",new[]{typeof(int),typeof(int),typeof(int)});
            Require(demand!=null,"An explicit minimum/preferred demand is required.");
            demand.Invoke(cache,new object[]{0,30,120});
            Require(cache.Required==120 && (int)typeof(NpcPredictionCache).GetProperty("MinimumRequired").GetValue(cache)==30,"Each consumer retains its own minimum while the preferred horizon stays 120.");
            cache.Release(0);Require(cache.Read(1)==null && (int)typeof(NpcPredictionCache).GetProperty("MinimumRequired").GetValue(cache)==120,"Releasing the short consumer restores strict long demand.");
            cache.EndSession();Require(cache.Required==0 && cache.Read(1)==null,"Session end clears both demand bounds.");
            // A truthful terminal prefix remains visible to its short reader;
            // the stop reason never exempts a strict reader from its minimum.
            cache.Demand(0,120);cache.Demand(1,1,120);
            var terminal=new NpcTrajectory(identity,100,2,PredictionAssumption.None,PredictionStop.MissingDependency,points,20);
            cache.Publish(terminal);
            Require(cache.Read(0)==null && ReferenceEquals(cache.Read(1),terminal) && terminal.Count==20 && terminal.SampleTick==100 && terminal.CaptureTick==100 && terminal.Stop==PredictionStop.MissingDependency,"The real nineteen-future terminal prefix is preserved while strict120 rejects it without padding.");
            cache.Release(1);Require(cache.Read(0)==null && cache.Required==120 && cache.MinimumRequired==120,"Releasing the short reader leaves the strict minimum intact.");
            cache.EndSession();Require(cache.Required==0 && cache.Read(0)==null,"Session end retires the terminal result and final reader.");
            Console.WriteLine("PASS native per-consumer minimum coverage; strict long demand; truthful terminal prefix; release/end");
            if(Environment.GetEnvironmentVariable("JUEMINGR_TIERED_PRIVATE")=="cache")return;
            byte[] request=File.ReadAllBytes(Path.Combine(output,"fixed-request.bin"));
            Require(BitConverter.ToInt32(request,12)==180,"Fixed raw input begins with the original 180 actual steps.");
            Buffer.BlockCopy(BitConverter.GetBytes(60),0,request,12,4);File.WriteAllBytes(Path.Combine(output,"short-request.bin"),request);
            var type=sandbox.GetType();byte[] core=(byte[])type.GetMethod("Predict",Flags).Invoke(sandbox,new object[]{request,true});
            byte[] proof=(byte[])type.GetProperty("Alignment",Flags).GetValue(sandbox);
            File.WriteAllBytes(Path.Combine(output,"short-core.bin"),core);File.WriteAllBytes(Path.Combine(output,"short-alignment.bin"),proof??new byte[0]);
            Require(BitConverter.ToInt32(core,0)>0 && BitConverter.ToInt32(core,16)==61 && BitConverter.ToInt32(proof,0)==61,"The actual short request completes exactly 60 original updates plus sample zero.");
            var alignment=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true);
            byte[] expected=File.ReadAllBytes(Path.Combine(output,"expected-alignment.bin"));
            using(var a=new BinaryReader(new MemoryStream(proof)))using(var b=new BinaryReader(new MemoryStream(expected)))
            {
                a.ReadInt32();b.ReadInt32();
                for(int i=0;i<61;i++)
                {
                    var read=alignment.GetMethod("Read",Flags);object current=read.Invoke(null,new object[]{a}),prior=read.Invoke(null,new object[]{b});
                    string difference=(string)alignment.GetMethod("Difference",Flags).Invoke(null,new[]{prior,current});
                    Require(difference==null,"Every completed short frame must match the sealed original full-history prefix: "+i+" "+difference);
                }
            }
            Console.WriteLine("PASS actual short request: 61 complete proof frames match the sealed 180-step input prefix");
            var capture=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true).GetMethod("CaptureScene",Flags);
            var decode=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionResult",true).GetMethod("Read",Flags);
            foreach(bool late in new[]{true,false})
            {
                type.GetMethod("ClearWorld",Flags).Invoke(sandbox,null);NativeCombatWorkerChecks.Scene(false);Main.npc[0].ai[0]=late?-120:0;
                var actor=Main.npc[0];var id=new NpcIdentity(1,actor,0,actor.generation,actor.type,actor.netID);
                byte[] raw=(byte[])capture.Invoke(null,new object[]{new[]{0},new int[0],0,1000L,late?180:60});
                byte[] reply;
                try{reply=(byte[])type.GetMethod("Predict",Flags).Invoke(sandbox,new object[]{raw,true});}
                catch(TargetInvocationException error) when(!late && error.InnerException is InvalidDataException)
                {
                    // This direct sandbox API throws; only the worker transport
                    // wraps the native refusal in a negative protocol envelope.
                    var directory=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeEntityDirectory",true);
                    Require((int)directory.GetProperty("MissingKind",Flags).GetValue(null)==2 && (int)directory.GetProperty("MissingSlot",Flags).GetValue(null)==0 && type.GetProperty("Alignment",Flags).GetValue(sandbox)==null,"Short unknown birth retains the original refusal and supplies no partial proof.");
                    File.WriteAllBytes(Path.Combine(output,"short-refusal-request.bin"),raw);File.WriteAllText(Path.Combine(output,"short-refusal.txt"),error.InnerException.ToString());continue;
                }
                byte[] alignmentBytes=(byte[])type.GetProperty("Alignment",Flags).GetValue(sandbox);
                File.WriteAllBytes(Path.Combine(output,late?"late-prefix-core.bin":"short-refusal-core.bin"),reply);
                if(late)
                {
                    object result=decode.Invoke(null,new object[]{reply,alignmentBytes,id,1000L,1L,false});
                    Require(((Array)Get(result,"Frames")).Length==150 && ((NpcTrajectory)Get(result,"Trajectory")).Count==150 && (int)Get(result,"ContinuationKind")==2 && !((SortedSet<int>)Get(result,"Projectiles")).Contains(0),"Actual failure at update 150 retains only 0..149; no failed birth point, proof or dependency.");
                    File.WriteAllBytes(Path.Combine(output,"late-prefix-alignment.bin"),alignmentBytes);
                }
                else throw new InvalidOperationException("A birth failure within 60 updates must retain its native refusal.");
            }
            Console.WriteLine("PASS original late-prefix failed-step boundary; short unknown birth remains refusal");
        }
        private static readonly object gate=new object();
        private static readonly Dictionary<long,int> horizons=new Dictionary<long,int>();
        private static readonly List<string> encoded=new List<string>();
        private static void Encoded(byte[] __result)
        {
            long tick=BitConverter.ToInt64(__result,4);int horizon=BitConverter.ToInt32(__result,12);
            lock(gate){horizons[tick]=horizon;encoded.Add(Csv(tick,horizon,__result.Length));}
        }
        internal static void Live(object context,object native,NpcPredictionCache cache,Action step,string output)
        {
            var host=Get(context,"CombatObservation");var worker=Get(native,"Worker");
            Call(native,"ClearTarget");var wait=Stopwatch.StartNew();
            while(Get(native,"pending")!=null && wait.ElapsedMilliseconds<8000){Call(native,"DiscardRetiredResult");Thread.Sleep(2);}
            Require(Get(native,"pending")==null,"Boundary fixture starts after the prior retired mailbox drains.");
            NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();
            var player=Main.LocalPlayer;player.controlLeft=player.controlRight=player.controlJump=false;player.position=new Vector2(1100,2400-player.height);player.velocity=Vector2.Zero;player.fallStart=player.fallStart2=(int)(player.position.Y/16);
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1400,2140,2,Start:64,Target:Main.myPlayer);var target=Main.npc[slot];
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,clearLine:false,mouseCenter:true,dummy:true,radius:25));
            Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();
            var rows=new List<string>{"phase,tick,capture,count,pending,pendingHorizon,stableSince"};
            horizons.Clear();encoded.Clear();encoded.Add("capture,horizon,bytes");
            var hooks=new Harmony("JueMingR.Tests.TieredWindow");
            hooks.Patch(native.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeCapturedValues",true).GetMethod("Encode",Flags),postfix:new HarmonyMethod(typeof(NativeCombatTieredPredictionChecks),nameof(Encoded)));
            var checkedRequests=new HashSet<object>();int historyFrames=0;string phase="short-to-long";long first=-1,firstCapture=-1;
            using(var trace=new NativeCombatAttackTrace(native,output))
            try
            {
                cache.Demand(NativeCombatObservationChecks.IndependentReader,120);
                Func<NpcTrajectory> advance=()=>
                {
                    trace.Phase=phase;trace.Selected=slot;trace.Frame=rows.Count-1;
                    NativeCombatModeledImpactChecks.SampleMouse(context,target.Center);step();
                    Require(trace.Fault==null && !(bool)Get(native,"Failed") && ReferenceEquals(worker,Get(native,"Worker")),"Boundary checks keep one healthy real owner.");
                    var path=cache.Read(0);var request=Get(native,"acceptedRequest");var pending=Get(native,"pending");
                    if(path!=null)
                    {
                        Require(path.SampleTick==Main.GameUpdateCount && ReferenceEquals(path.Identity.Token,target) && path.Count>=31 && path.Count<=121,"Display uses this current identity and actual 30..120 future steps.");
                        Require((cache.Read(NativeCombatObservationChecks.IndependentReader)!=null)==(path.Count==121),"The real shared consumer never receives a short native window.");
                        if(checkedRequests.Add(request))
                        {
                            int actual;lock(gate)Require(horizons.TryGetValue(path.CaptureTick,out actual) && actual==(int)Get(request,"Horizon"),"Accepted horizon comes from actual encoded transport bytes.");
                            var frames=(Array)Get(Get(native,"accepted"),"Frames");var history=(IList)Get(request,"History");
                            var difference=native.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true).GetMethod("Difference",Flags);
                            Require(history.Count==path.SampleTick-path.CaptureTick+1,"Full intervening History has no skipped updates.");
                            for(int i=0;i<history.Count;i++){Require(difference.Invoke(null,new[]{frames.GetValue(i),history[i]})==null,"Every actual accepted history frame agrees.");historyFrames++;}
                        }
                    }
                    rows.Add(Csv(phase,Main.GameUpdateCount,path?.CaptureTick,path?.Count,Tick(pending),pending==null?null:Get(pending,"Horizon"),Get(native,"stableSince")));
                    return path;
                };
                NpcTrajectory shown=null;
                for(int i=0;i<240;i++)
                {
                    shown=advance();if(shown==null)continue;
                    if(first<0){first=Main.GameUpdateCount;firstCapture=shown.CaptureTick;Require(shown.Count<=61,"First display comes from an actually short request.");}
                    if(shown.Count==121)break;
                }
                Require(shown!=null && shown.Count==121 && Main.GameUpdateCount-first>=30 && shown.CaptureTick>firstCapture,"Stable short presentation hands over to a fresh, fully validated 120-step future.");
                Console.WriteLine("PASS actual 60-step recovery -> stable fresh 180-step handoff; strict second consumer; complete History="+historyFrames);
                for(int i=0;i<15 && Get(native,"pending")==null;i++)advance();
                object retired=Get(native,"pending");Require(retired!=null,"Hurt boundary includes a real in-flight request.");
                phase="hurt";player.immune=false;player.immuneTime=0;Array.Clear(player.hurtCooldowns,0,player.hurtCooldowns.Length);player.noKnockback=false;
                Require(player.Hurt(Terraria.DataStructures.PlayerDeathReason.ByCustomReason("tiered isolated boundary"),10,1,quiet:true,dodgeable:false)>0,"Original Hurt really applies.");
                Require(advance()==null && (bool)Get(retired,"Retired"),"Hurt immediately retires old publication and its actual pending reply.");
                for(int i=0;i<180 && (shown=advance())==null;i++){}
                Require(shown!=null && shown.Count<=61 && !ReferenceEquals(retired,Get(native,"acceptedRequest")),"After Hurt, only a new short capture can restore the line.");
                phase="identity-reuse";object old=Get(native,"pending");
                var replacement=new NPC();replacement.SetDefaults(2);replacement.whoAmI=slot;replacement.active=true;replacement.position=target.position;replacement.target=Main.myPlayer;Main.npc[slot]=replacement;target=replacement;
                Require(advance()==null && (old==null || (bool)Get(old,"Retired")),"Same-slot object replacement immediately retires the old identity and mailbox.");
                for(int i=0;i<180 && (shown=advance())==null;i++){}
                Require(shown!=null && shown.Count<=61 && ReferenceEquals(shown.Identity.Token,replacement),"Replacement gets its own real short capture, never the retired result.");
                Console.WriteLine("PASS actual Hurt and same-slot replacement revoke immediately; late replies retire; new short capture restores current identity");
            }
            finally{cache.Release(NativeCombatObservationChecks.IndependentReader);hooks.UnpatchAll(hooks.Id);File.WriteAllLines(Path.Combine(output,"tiered-boundaries.csv"),rows);lock(gate)File.WriteAllLines(Path.Combine(output,"tiered-encoded.csv"),encoded);}
        }
        private static void Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Flags).Invoke(value,args);
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
