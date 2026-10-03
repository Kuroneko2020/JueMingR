using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;

namespace NativeWorldTextProbe
{
    // One real native hit after a sealed request. Only mailbox consumption is
    // delayed; neither response bytes nor Session acceptance are substituted.
    internal static class NativeCombatImpactRetirementChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static object heldWorker;
        private static bool hold;
        private static bool Mailbox(object __instance){return !hold || !ReferenceEquals(__instance,heldWorker);}
        internal static void Run(object context,object native,NpcPredictionCache cache,Action step,string output)
        {
            var rows=new List<string>{"stage,tick,capture,shown,life,vx,vy,pendingRetired,rejected,reason"};
            object worker=Get(native,"Worker");heldWorker=worker;
            var hooks=new Harmony("JueMingR.probe.impact-retirement");
            hooks.Patch(worker.GetType().GetMethod("TryTakeResult",Flags),prefix:new HarmonyMethod(typeof(NativeCombatImpactRetirementChecks).GetMethod("Mailbox",Flags)));
            try
            {
                NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();
                NativeCombatWorkerImmunityChecks.CombatFont(output);
                for(int i=0;i<Main.combatText.Length;i++)Main.combatText[i]=new CombatText();
                var player=Main.LocalPlayer;player.controlLeft=player.controlRight=player.controlUp=player.controlDown=player.controlJump=false;
                player.position=new Vector2(1100,2400-player.height);player.velocity=Vector2.Zero;player.immune=true;player.immuneTime=100000;
                player.statLife=player.statLifeMax=player.statLifeMax2=400;player.wet=player.honeyWet=player.lavaWet=player.shimmerWet=false;player.fallStart=player.fallStart2=150;
                var target=Main.npc[16];target.SetDefaults(3);target.whoAmI=16;target.active=true;target.life=1000;target.lifeMax=1000;target.position=new Vector2(1500,2400-target.height);target.target=0;
                var background=Main.npc[0];background.SetDefaults(3);background.whoAmI=0;background.active=true;background.life=1000;background.lifeMax=1000;background.position=new Vector2(700,2400-background.height);background.target=0;
                var watch=Stopwatch.StartNew();
                // Seed just the already-captured-background precondition. This
                // is not projectile discovery: all captures remain real values.
                do
                {
                    step();((SortedSet<int>)Get(native,"npcs")).Add(0);
                }while(!OwnsBackground(native) && watch.Elapsed.TotalSeconds<12);
                Require(OwnsBackground(native),"Actual Session accepts a complete captured background page.");
                var request=Get(native,"acceptedRequest");var accepted=Get(native,"accepted");
                int[] slots=(int[])Get(request,"Npcs");var frame=((Array)Get(accepted,"Frames")).GetValue(0);
                Require(!((bool[])Get(frame,"NpcRequired"))[Array.IndexOf(slots,0)],"Real accepted background has no route use before the new impact.");
                long oldCapture=(long)Get(request,"Tick");hold=true;
                for(int i=0;i<12 && Get(native,"pending")==null;i++)step();
                var pending=Get(native,"pending");Require(pending!=null && Array.IndexOf((int[])Get(pending,"Npcs"),0)>=0,"A real sealed second request also owns that background.");
                long pendingCapture=(long)Get(pending,"Tick"),rejected=(long)Get(native,"Rejected");
                var route=cache.Read(0);Require(route!=null && route.CaptureTick==oldCapture,"Original default consumer has a current+120 route before impact.");
                int life=background.life;Vector2 velocity=background.velocity;
                int shot=Projectile.NewProjectile(new EntitySource_DebugCommand(),background.Center,new Vector2(12,0),1,20,3,0);
                var arrow=Main.projectile[shot];arrow.aiStyle=0;arrow.tileCollide=false;arrow.timeLeft=100;
                Require(Array.IndexOf((int[])Get(request,"Projectiles"),shot)<0 && Array.IndexOf((int[])Get(pending,"Projectiles"),shot)<0,"Existing sealed accepted and pending requests did not capture the newly observed arrow.");
                arrow.Damage();
                Require(background.life<life && background.velocity!=velocity && arrow.penetrate==0,"Original finite arrow really hits and knocks back captured background.");
                Console.WriteLine("IMPACT original tick="+Main.GameUpdateCount+" life="+life+"->"+background.life+" velocity="+velocity+"->"+background.velocity+" arrow="+shot+" absent-from-both=true old="+oldCapture+" pending="+pendingCapture);
                Record(rows,"native-hit",native,cache,background,pending);
                step();Record(rows,"next-prepare",native,cache,background,pending);
                bool oldRetired=!ReferenceEquals(request,Get(native,"acceptedRequest")) && (cache.Read(0)==null || cache.Read(0).CaptureTick!=oldCapture);
                bool pendingRetired=(bool)Get(pending,"Retired");
                Console.WriteLine("IMPACT next-prepare old-retired="+oldRetired+" pending-retired="+pendingRetired+" shown-capture="+(cache.Read(0)?.CaptureTick.ToString()??"none"));
                hold=false;for(int i=0;i<35 && ReferenceEquals(pending,Get(native,"pending"));i++){step();Record(rows,"late-reply",native,cache,background,pending);}
                bool lateRejected=(long)Get(native,"Rejected")>rejected && (cache.Read(0)==null || cache.Read(0).CaptureTick!=pendingCapture);
                int shown=0;watch.Restart();
                while(shown<30 && watch.Elapsed.TotalSeconds<8)
                {
                    step();Record(rows,"recovery",native,cache,background,pending);route=cache.Read(0);
                    if(route!=null && route.CaptureTick>pendingCapture){Require(route.Count==121 && route.SampleTick==Main.GameUpdateCount,"Recovery consumes actual current plus 120.");shown++;}
                }
                Console.WriteLine("IMPACT result old-retired="+oldRetired+" pending-retired="+pendingRetired+" late-rejected="+lateRejected+" recovery="+shown+" same-worker="+ReferenceEquals(worker,Get(native,"Worker")));
                Require(oldRetired && pendingRetired && lateRejected,"Real previously unobserved arrow impact retires accepted and in-flight background premises before publication.");
                Require(shown==30 && ReferenceEquals(worker,Get(native,"Worker")) && !(bool)Get(native,"Failed"),"Real Session resumes with the same worker after impact.");
            }
            finally{hold=false;heldWorker=null;hooks.UnpatchAll(hooks.Id);File.WriteAllLines(Path.Combine(output,"impact-retirement.csv"),rows);}
        }
        private static bool OwnsBackground(object native)
        {var request=Get(native,"acceptedRequest");return request!=null && Array.IndexOf((int[])Get(request,"Npcs"),0)>=0 && Array.IndexOf((int[])Get(request,"Npcs"),16)>=0;}
        private static void Record(List<string> rows,string stage,object native,NpcPredictionCache cache,NPC background,object pending)
        {rows.Add(string.Join(",",stage,Main.GameUpdateCount,cache.Read(0)?.CaptureTick.ToString()??"",cache.Read(0)!=null?1:0,background.life,background.velocity.X,background.velocity.Y,Get(pending,"Retired"),Get(native,"Rejected"),"\""+((string)Get(native,"Reason")??"").Replace("\"","\"\"")+"\""));}
        private static object Get(object owner,string name){var field=owner.GetType().GetField(name,Flags);return field!=null?field.GetValue(owner):owner.GetType().GetProperty(name,Flags).GetValue(owner);}
        internal static void Off(object context,object native,NpcPredictionCache cache,Action step,string output)
        {
            var host=Get(context,"CombatObservation");object worker=Get(native,"Worker");
            NativeCombatWorkerImmunityChecks.CombatFont(output);
            for(int i=0;i<Main.combatText.Length;i++)Main.combatText[i]=new CombatText();
            var npc=Main.npc[0];npc.life=npc.lifeMax=1000;
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
            long requests=(long)Get(native,"Requests");int life=npc.life;
            int damage=npc.StrikeNPC(20,3,1);
            Require(damage>0 && npc.life<life,"Function OFF check still performs a real native hit.");
            for(int i=0;i<6;i++)step();
            Require(cache.Required==0 && cache.Read(0)==null && (long)Get(native,"Requests")==requests && Get(native,"acceptedRequest")==null && Get(native,"pending")==null,"No-demand hit cannot retain a request, publish or start prediction.");
            // Exercise the existing real observer session-end boundary, not a
            // replacement hook or a simulated second game process.
            host.GetType().GetMethod("OnSessionEnded",Flags).Invoke(host,null);
            life=npc.life;damage=npc.StrikeNPC(20,3,-1);
            Require(damage>0 && npc.life<life && Get(native,"acceptedRequest")==null && Get(native,"pending")==null && cache.Required==0,"Session-end native hit has no old request to contaminate.");
            host.GetType().GetMethod("OnSessionStarted",Flags).Invoke(host,null);
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true));
            var timer=Stopwatch.StartNew();do{step();}while(cache.Read(0)==null && timer.Elapsed.TotalSeconds<8);
            Require(cache.Read(0)!=null && cache.Read(0).Count==121 && ReferenceEquals(worker,Get(native,"Worker")) && !(bool)Get(native,"Failed"),"New world-owner entry resumes current+120 after OFF/end hits without carrying old impact facts.");
            Console.WriteLine("IMPACT OFF/end actual-hits=2 requests-while-off=0 retained-requests=0 same-worker=true reentry-current+120=true");
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
