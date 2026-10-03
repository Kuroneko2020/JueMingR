using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace NativeWorldTextProbe
{
    // Native walking-to-wall-creeping transformation needs an asset which
    // was absent from the captured current type. No response is substituted.
    internal static class NativeCombatAssetRecoveryChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static readonly List<string> replies=new List<string>();
        private static int missing=-1;private static string error;
        private static void Receive(object __0)
        {
            var result=Get(__0,"Result");string failure=(string)Get(result,"Error");
            replies.Add(string.Join(",",Main.GameUpdateCount,Get(__0,"MissingAsset"),Get(result,"Kind"),Get(__0,"ReplyBytes"),"\""+(failure??"").Replace("\"","\"\"")+"\""));
            if(failure!=null && failure.Contains("Unobserved texture metadata: 240")){missing=(int)Get(__0,"MissingAsset");error=failure;}
        }
        internal static void Run(object context,object native,NpcPredictionCache cache,Action step,string output)
        {
            object worker=Get(native,"Worker");replies.Clear();replies.Add("tick,missingAsset,kind,replyBytes,error");missing=-1;error=null;
            var owner=new Harmony("JueMingR.probe.asset-recovery");
            owner.Patch(native.GetType().GetMethod("Receive",Flags),prefix:new HarmonyMethod(typeof(NativeCombatAssetRecoveryChecks).GetMethod("Receive",Flags)));
            try
            {
                NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();
                var player=Main.LocalPlayer;player.controlLeft=player.controlRight=player.controlUp=player.controlDown=player.controlJump=false;
                player.position=new Vector2(1100,2400-player.height);player.velocity=Vector2.Zero;player.immune=true;player.immuneTime=100000;
                player.dead=false;player.statLife=player.statLifeMax=player.statLifeMax2=400;player.fallStart=player.fallStart2=150;player.wet=player.honeyWet=player.lavaWet=player.shimmerWet=false;
                for(int x=60;x<130;x++)for(int y=130;y<150;y++)Main.tile[x,y].wall=1;
                var npc=Main.npc[16];npc.SetDefaults(239);npc.whoAmI=16;npc.active=true;npc.position=new Vector2(1500,2400-npc.height);npc.velocity=Vector2.Zero;npc.target=0;npc.timeLeft=750;npc.localAI[1]=120;
                long start=Main.GameUpdateCount;var watch=Stopwatch.StartNew();bool expanded=false;int shown=0;
                while(watch.Elapsed.TotalSeconds<12 && shown<60)
                {
                    step();expanded|=((System.Collections.Generic.SortedSet<int>)Get(native,"assets")).Contains(240);
                    if(error!=null)
                    {
                        Require(missing==240,"Real transformed NPC texture refusal carries bounded MissingAsset=240.");
                        Require(expanded,"Real Session receives the refusal and expands asset pages without an explicit Retry.");
                    }
                    var route=cache.Read(0);if(route!=null && route.Identity.Slot==16 && error!=null){Require(route.SampleTick==Main.GameUpdateCount && route.Count==121,"Asset recovery consumer reads current plus 120 future steps.");shown++;}
                }
                Require(error!=null && expanded && shown==60,"Native 239→240 missing texture expands a page and resumes actual Host current+120 windows.");
                Require(ReferenceEquals(worker,Get(native,"Worker")) && !(bool)Get(native,"Failed"),"Asset refusal preserves the same authenticated worker and Session.");
                Console.WriteLine("ASSET native239->240 missing="+missing+" expanded="+expanded+" shown="+shown+" updates="+(Main.GameUpdateCount-start)+" elapsed-ms="+watch.Elapsed.TotalMilliseconds+" same-worker=true");
            }
            finally{owner.UnpatchAll(owner.Id);File.WriteAllLines(Path.Combine(output,"asset-recovery.csv"),replies);}
        }
        private static object Get(object owner,string name){var field=owner.GetType().GetField(name,Flags);return field!=null?field.GetValue(owner):owner.GetType().GetProperty(name,Flags).GetValue(owner);}
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
