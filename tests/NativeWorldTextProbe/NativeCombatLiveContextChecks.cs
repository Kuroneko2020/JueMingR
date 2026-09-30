using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;
using Terraria.ID;

namespace NativeWorldTextProbe
{
    // Focused reproduction of ordinary production acceptance under additional
    // live-world context. The callback keeps the existing original/Host order;
    // no prediction result or history state is patched by this fixture.
    internal static class NativeCombatLiveContextChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        internal static void Run(object context,NpcPredictionCache cache,Action step,string output)
        {
            string mode=Environment.GetEnvironmentVariable("JUEMINGR_NPC_LIVE_CONTEXT");
            var native=Get(Get(Get(context,"CombatObservation"),"Prediction"),"Native");
            Require(native.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionDiagnostic")==null,"Ordinary product excludes the retired one-shot diagnostic.");
            if(mode=="mounted"){Mounted(context,native,cache,step);return;}
            if(mode!="shared-rng")throw new InvalidOperationException("Unknown live-context scenario.");
            int failed=0;
            foreach(bool neighbor in new[]{false,true})
            {
                foreach(var npc in Main.npc)npc.active=false;
                foreach(var projectile in Main.projectile)projectile.active=false;
                var player=Main.LocalPlayer;player.controlRight=player.controlLeft=player.controlJump=false;
                player.position=new Vector2(640,70*16-player.height);player.velocity=Vector2.Zero;
                if(neighbor)
                {
                    int bunny=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),300,70*16,NPCID.Bunny);
                    Require(bunny==0 && Main.npc[bunny].friendly,"Unrelated native Bunny occupies the earlier slot.");
                }
                int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),680,70*16,NPCID.Zombie,Start:1);
                Require(slot>=1 && slot<Main.maxNPCs,"Selected native Zombie occupies a later slot, respecting original spawn protection.");
                long requests=(long)Get(native,"Requests"),rejected=(long)Get(native,"Rejected"),refused=(long)Get(native,"Refused");
                int shown=0,longest=0,blank=0;var reasons=new Dictionary<string,int>();
                for(int frame=0;frame<300;frame++)
                {
                    step();var path=cache.Read(0);
                    string reason=(string)Get(native,"Reason")??"none";
                    int count;reasons.TryGetValue(reason,out count);reasons[reason]=count+1;
                    if(path==null){longest=Math.Max(longest,++blank);continue;}
                    blank=0;shown++;
                    Require(path.Identity.Slot==slot && ReferenceEquals(path.Identity.Token,Main.npc[slot]) && path.Strategy==PredictionStrategy.NativeIsolated,"Current native target owns every displayed window.");
                    Require(path.SampleTick==Main.GameUpdateCount && path.Count==121 && Math.Abs(path[0].Bounds.X-Main.npc[slot].position.X)<.002f && Math.Abs(path[0].Bounds.Y-Main.npc[slot].position.Y)<.002f,"Current origin and complete future remain truthful.");
                }
                Console.WriteLine("LIVE-CONTEXT neighbor="+neighbor+" slot="+slot+" shown="+shown+" longest-blank="+longest+" requests="+((long)Get(native,"Requests")-requests)+" rejected="+((long)Get(native,"Rejected")-rejected)+" refused="+((long)Get(native,"Refused")-refused));
                foreach(var pair in reasons.OrderByDescending(p=>p.Value))Console.WriteLine("LIVE-REASON frames="+pair.Value+" "+pair.Key);
                if(shown<180 || longest>=60)failed++;
            }
            Require(failed==0,"Ordinary production must remain usable with an unrelated earlier NPC; failing scenes="+failed);
        }
        private static void Mounted(object context,object native,NpcPredictionCache cache,Action step)
        {
            bool dedicated=Main.dedServ;int network=Main.netMode;
            try{Main.dedServ=true;Main.netMode=2;Mount.Initialize();}finally{Main.dedServ=dedicated;Main.netMode=network;}
            foreach(var npc in Main.npc)npc.active=false;
            foreach(var projectile in Main.projectile)projectile.active=false;
            var player=Main.LocalPlayer;player.controlRight=player.controlLeft=player.controlJump=false;
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),680,70*16,NPCID.Zombie,Start:1);
            Require(slot>=1 && slot<Main.maxNPCs,"Native Zombie birth for mounted production.");
            foreach(int mount in new[]{0,MountID.WitchBroom,-1})
            {
            int local=Main.myPlayer;
            // Original mounting establishes its buff, dimensions and owned
            // MountData. Suppress only the local network outlet during this
            // isolated setup; every measured update is the ordinary player.
            try{Main.myPlayer=1;if(mount<0)player.mount.Dismount(player);else player.mount.SetMount(mount,player);}finally{Main.myPlayer=local;}
            player.position=new Vector2(640,70*16-player.height);player.velocity=Vector2.Zero;
            int shown=0,longest=0,blank=0;var reasons=new Dictionary<string,int>();
            for(int frame=0;frame<300;frame++)
            {
                step();Require(player.mount.Active==(mount>=0),"Original scene retains its mount state.");
                var path=cache.Read(0);string reason=(string)Get(native,"Reason")??"none";
                int count;reasons.TryGetValue(reason,out count);reasons[reason]=count+1;
                if(path==null){longest=Math.Max(longest,++blank);continue;}
                blank=0;shown++;
                Require(path.Identity.Slot==slot && ReferenceEquals(path.Identity.Token,Main.npc[slot]) && path.Strategy==PredictionStrategy.NativeIsolated,"Mounted player keeps the selected native target.");
                Require(path.SampleTick==Main.GameUpdateCount && path.Count==121 && Math.Abs(path[0].Bounds.X-Main.npc[slot].position.X)<.002f && Math.Abs(path[0].Bounds.Y-Main.npc[slot].position.Y)<.002f,"Mounted production has a truthful current origin and 120 future steps.");
                if(mount>=0)Require((path.Assumptions&PredictionAssumption.ApproximateMechanism)!=0,"Mounted conditional movement remains explicitly approximate.");
            }
            Console.WriteLine("MOUNTED type="+mount+" shown="+shown+" longest-blank="+longest);
            foreach(var pair in reasons.OrderByDescending(p=>p.Value))Console.WriteLine("MOUNTED-REASON frames="+pair.Value+" "+pair.Key);
            Require(shown>=180 && longest<60,"Mounted ordinary prediction must publish continuously after preparation.");
            }
        }
        private static object Get(object owner,string name)
        {var field=owner.GetType().GetField(name,Flags);return field!=null?field.GetValue(owner):owner.GetType().GetProperty(name,Flags).GetValue(owner);}
        private static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    }
}
