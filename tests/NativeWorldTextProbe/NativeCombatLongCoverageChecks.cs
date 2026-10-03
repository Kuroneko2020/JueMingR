using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria.GameInput;
using JueMingR.Features.Combat;

namespace NativeWorldTextProbe
{
    // Legal native births establish linkage and generation. The production
    // gatherer and the independent native future are separate assertions.
    internal static class NativeCombatLongCoverageChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static void Run(Assembly host,string layout,string output)
        {
            Lighting.Mode=Terraria.Graphics.Light.LightMode.Color;
            NativeCombatSegmentedPredictionChecks.Run(host,output);
            // Exact whole-chain replay is a research oracle, no longer the
            // default product contract for the explicitly approved families.
            if(Environment.GetEnvironmentVariable("JUEMINGR_NPC_EXACT_CHAIN_REFERENCE")!="1")return;
            bool gatherOnly=Environment.GetEnvironmentVariable("JUEMINGR_NPC_COVERAGE_GATHER_ONLY")=="1";
            foreach(bool good in new[]{false,true})foreach(int role in new[]{0,1,2})
            {
                int[] chain=Destroyer(good);int selected=chain[role==0?0:role==1?chain.Length/2:chain.Length-1];
                int[] pages=Gather(host,selected);
                Require(pages.SequenceEqual(chain),"Production gather retains the complete natural Destroyer chain for head/body/tail: "+pages.Length+"/"+chain.Length);
                Console.WriteLine("GATHER destroyer good="+good+" selected-type="+Main.npc[selected].type+" pages="+pages.Length);
            }
            if(gatherOnly)return;
            using(var child=NativeCombatWorkerChecks.Start(layout))
            {
                var errors=child.StandardError.ReadToEndAsync();
                try
                {
                    foreach(bool good in new[]{false,true})foreach(int role in new[]{0,1,2})
                    {
                        int[] chain=Destroyer(good);int selected=chain[role==0?0:role==1?chain.Length/2:chain.Length-1];
                        int[] pages=Gather(host,selected);
                        var frozen=NativeCombatWorkerChecks.AcquireFrozen(host,child,pages,new int[0],selected);
                        NativeCombatWorkerChecks.Compare(frozen.Future,selected,output,"destroyer-"+(good?"good":"normal")+"-"+Main.npc[selected].type);
                    }
                }
                finally
                {
                    NativeCombatWorkerChecks.Exit(child,"legal long-chain helper exits");
                    File.WriteAllText(Path.Combine(output,"long-coverage-worker.log"),errors.Result);
                    Main.getGoodWorld=false;
                }
            }
        }
        internal static int[] Destroyer(bool good)
        {
            NativeCombatWorkerChecks.Scene(false);NPC.ClearAll();Projectile.ClearAll();
            return SpawnDestroyer(good);
        }
        private static int[] SpawnDestroyer(bool good)
        {
            Main.getGoodWorld=good;Main.dayTime=false;NPC.mechQueen=NPC.brainOfGravity=-1;
            int head=NPC.NewNPC(new EntitySource_DebugCommand(),800,800,134,Start:1,Target:0);
            Require(head==1,"Original worm allocation starts at legal slot one.");
            NPC.UpdateProtectedSpawnSlots();NPC.ClearFoundActiveNPCs();NPC.UpdateFoundActiveNPCs();
            // Higher slots born during head.UpdateNPC also update this phase.
            for(int slot=0;slot<Main.maxNPCs;slot++)if(Main.npc[slot].active)Main.npc[slot].UpdateNPC(slot);
            var chain=new List<int>();int at=head;
            while(true)
            {
                Require(at>0 && at<Main.maxNPCs && !chain.Contains(at),"Finite original linked chain.");
                NPC n=Main.npc[at];Require(n.active && n.realLife==head && (n.type==134 || n.type==135 || n.type==136),"Natural linked identity.");
                chain.Add(at);if(n.type==136)break;at=(int)n.ai[0];
            }
            Require(chain.Count==(good?102:82) && Main.npc.Count(n=>n.active)==chain.Count,"Original normal/good-world chain length.");
            return chain.ToArray();
        }
        internal static void Production(object context,NpcPredictionCache cache,Action<Action> step,Action firstDraw=null)
        {
            object host=Get(context,"CombatObservation"),input=Get(context,"Input");
            bool good=Environment.GetEnvironmentVariable("JUEMINGR_NPC_LARGE_GOOD")=="1";
            NPC.ClearAll();Projectile.ClearAll();int[] chain=SpawnDestroyer(good);string role=Environment.GetEnvironmentVariable("JUEMINGR_NPC_LARGE_ROLE")??"head";int selected=chain[role=="tail"?chain.Length-1:role=="body"?chain.Length/2:0];
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,radius:0));
            object native=Get(Get(host,"Prediction"),"Native"),worker=Get(native,"Worker");var process=(Process)Get(worker,"child");process.Refresh();double cpu=process.TotalProcessorTime.TotalMilliseconds;long memory=process.PrivateMemorySize64,requests=(long)Get(native,"Requests"),observed=(long)Get(native,"Observed");
            bool rapid=Environment.GetEnvironmentVariable("JUEMINGR_NPC_LARGE_RAPID")=="1";
            PlayerInput.CacheOriginalScreenDimensions();var watch=Stopwatch.StartNew();int frames=0,shown=0,wrong=0,longestBlank=0,blank=0,minBreaks=120,maxBreaks=0,otherSelection=0;double first=-1;
            while(watch.Elapsed.TotalSeconds<10)
            {
                if(rapid)selected=chain[(frames/30)%3==0?0:(frames/30)%3==1?chain.Length/2:chain.Length-1];
                step(()=>
                {
                    Vector2 center=SelectPoint(selected);Main.screenPosition=center-new Vector2(Main.screenWidth/2,Main.screenHeight/2);
                    Call(input,"BeginUpdate");PlayerInput.MouseInfo=new MouseState(Main.screenWidth/2,Main.screenHeight/2,0,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);
                    Call(input,"AfterNativeMouse",new List<string>());Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");Call(host,"SampleMouse");
                });
                frames++;var path=cache.Read(0);
                if(NativeCombatHistoryChecks.Completed)return;
                if(frames==180 && Environment.GetEnvironmentVariable("JUEMINGR_NPC_GUARD_PROFILE")=="1")
                {
                    // Diagnostic capture only. This run's wall costs are not
                    // a production performance sample: retain one real scene
                    // for counted private replay without altering its state.
                    var capture=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true).GetMethod("CaptureScene",Flags);
                    var shots=Enumerable.Range(0,Main.maxProjectiles).Where(i=>Main.projectile[i].active).ToArray();
                    string destination=Environment.GetEnvironmentVariable("JUEMINGR_NPC_GUARD_PROFILE_OUTPUT");
                    File.WriteAllBytes(destination,(byte[])capture.Invoke(null,new object[]{Gather(host.GetType().Assembly,selected),shots,selected,Main.GameUpdateCount,180}));
                    Console.WriteLine("DIAGNOSTIC captured actual large-chain scene after 180 updates; this run is not a cost benchmark");return;
                }
                if(frames==1 || frames==180)
                {
                    var target=Main.npc[selected];var selection=Get(host,"Selection");var owner=Get(Get(host,"Prediction"),"Native");
                    Console.WriteLine("LARGE state intended="+selected+" actual="+((JueMingR.Platform.Combat.NpcIdentity)Get(selection,"Target")).Slot+" active="+target.active+" life="+target.life+" friendly="+target.friendly+" immortal="+target.immortal+" invulnerable="+target.dontTakeDamage+" position="+target.position+" player-dead="+Main.LocalPlayer.dead+" has-target="+Get(selection,"HasTarget")+" mouse="+Get(selection,"RealMouse")+" native-reason="+Get(owner,"Reason"));
                }
                var actualSelection=Get(host,"Selection");bool hasTarget=(bool)Get(actualSelection,"HasTarget");var actualIdentity=(JueMingR.Platform.Combat.NpcIdentity)Get(actualSelection,"Target");
                if(path!=null && (!hasTarget || !path.Identity.Equals(actualIdentity))){wrong++;continue;}
                if(hasTarget && actualIdentity.Slot!=selected)otherSelection++;
                if(path==null){blank++;longestBlank=Math.Max(longestBlank,blank);continue;}
                Require(path.Count==121 && path.SampleTick==Main.GameUpdateCount && ReferenceEquals(path.Identity.Token,Main.npc[actualIdentity.Slot]) && path.Strategy==JueMingR.Platform.Combat.PredictionStrategy.SegmentedTrend,"Large production result retains the actual current selection plus 120 future; overlapping boxes preserve native selection ties.");shown++;blank=0;if(first<0){first=watch.Elapsed.TotalMilliseconds;firstDraw?.Invoke();}
                int breaks=0;for(int i=1;i<path.Count;i++)if(path[i].NewSegment)breaks++;minBreaks=Math.Min(minBreaks,breaks);maxBreaks=Math.Max(maxBreaks,breaks);
            }
            Console.WriteLine("LARGE production good="+good+" role="+role+" frames="+frames+" shown="+shown+" other-selected="+otherSelection+" wrong-selected="+wrong+" longest-blank="+longestBlank+" first-ms="+first.ToString("F3")+" path-breaks="+minBreaks+".."+maxBreaks);
            process.Refresh();Console.WriteLine("LARGE complete entry rapid="+rapid+" native-requests="+((long)Get(native,"Requests")-requests)+" native-observations="+((long)Get(native,"Observed")-observed)+" worker-cpu-ms="+(process.TotalProcessorTime.TotalMilliseconds-cpu).ToString("F3")+" private-before="+memory+" private-after="+process.PrivateMemorySize64);
            Require((long)Get(native,"Requests")==requests && (long)Get(native,"Observed")==observed,"Production segmented path bypasses capture and full-state history; no shadow requests.");
            Require(shown>0 && wrong==0,"Natural large chain must deliver a current complete production line; continuity is reported separately.");
        }
        internal static Vector2 SelectPoint(int slot)
        {
            // Native selection measures distance to receive rectangles, not
            // centers. Aim through real mouse input at a nonoverlapped part of
            // the selected segment when available; never force target state.
            var target=Main.npc[slot];var box=target.Hitbox;
            foreach(float x in new[]{.5f,.05f,.95f})foreach(float y in new[]{.5f,.05f,.95f})
            {
                var point=new Vector2(box.Left+box.Width*x,box.Top+box.Height*y);bool overlap=false;
                for(int i=0;i<Main.maxNPCs;i++)if(i!=slot){var n=Main.npc[i];if(n.active && n.life>0 && !n.friendly && !n.immortal && !n.dontTakeDamage && n.Hitbox.Contains((int)point.X,(int)point.Y)){overlap=true;break;}}
                if(!overlap)return point;
            }
            return target.Center;
        }
        private static object Get(object owner,string name)
        {var type=owner.GetType();return type.GetField(name,Flags)?.GetValue(owner)??type.GetProperty(name,Flags).GetValue(owner);}
        private static object Call(object owner,string name,params object[] args){return owner.GetType().GetMethod(name,Flags).Invoke(owner,args);}
        private static int[] Gather(Assembly host,int selected)
        {
            var type=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionSession",true);
            object session=Activator.CreateInstance(type,Flags,null,new object[]{null,new JueMingR.Features.Combat.NpcPredictionCache()},null);
            var pages=(SortedSet<int>)type.GetField("npcs",Flags).GetValue(session);pages.Add(selected);
            type.GetMethod("GatherDependencies",Flags).Invoke(session,null);return pages.ToArray();
        }
        private static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    }
}
