using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeAimLightSessionChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static readonly List<string> receipts=new List<string>();
        private static object owner;
        internal static void Semantics(Assembly host,string output)
        {
            // The same initialized fixture precedes all timed windows. Do not
            // compare late live snapshots whose async scheduling can differ.
            NPC.ClearAll();Projectile.ClearAll();SetTarget(110);
            var capture=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire").GetMethod("CaptureScene",Flags);
            var args=new object[]{new[]{0},new int[0],0,700L,180};
            var first=(byte[])capture.Invoke(null,args);var trace=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.AimLightTrace");var field=trace?.GetField("log",Flags);object observer=field?.GetValue(null);byte[] second;
            // Same process/state with the actual observer detached then restored.
            // No Session or request learning is involved in this wire-only gate.
            try{field?.SetValue(null,null);second=(byte[])capture.Invoke(null,args);}finally{field?.SetValue(null,observer);}
            Require(System.Linq.Enumerable.SequenceEqual(first,second),"Observation must not change identical captured request bytes.");
            File.WriteAllBytes(Path.Combine(output,"semantic-input.bin"),first);
            var values=(Array)host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeWorldSnapshot").GetField("Values",Flags).GetValue(null);
            var schema=values.GetValue(0);var fields=(FieldInfo[])schema.GetType().GetField("fields",Flags).GetValue(schema);var read=schema.GetType().GetMethod("ReadScalar",Flags);
            using(var stream=new MemoryStream(first))using(var reader=new BinaryReader(stream))
            {
                stream.Position=67;reader.ReadString();var map=new List<string>();
                foreach(var nativeField in fields){long offset=stream.Position;read.Invoke(null,new object[]{reader,nativeField.FieldType});map.Add(offset+","+(stream.Position-offset)+","+nativeField.Name);}
                File.WriteAllLines(Path.Combine(output,"semantic-world-fields.csv"),map);
            }
        }
        private static void Before(object __instance,out object __state){__state=ReferenceEquals(__instance,owner)?GetOptional(owner,"pending"):null;}
        private static void After(object __instance,object response,long tick,object __state)
        {
            if(__state==null)return;
            var result=Get(response,"Result");
            bool accepted=ReferenceEquals(GetOptional(owner,"acceptedRequest"),__state);
            receipts.Add(string.Join(",",Get(__state,"Tick"),tick,accepted,Get(__state,"Retired"),Get(result,"Kind"),Get(result,"Slot"),(GetOptional(result,"Error")??"none").ToString().Replace(',',';').Replace("\r","\\r").Replace("\n","\\n"),((System.Collections.ICollection)Get(__state,"History")).Count));
        }
        internal static void Run(object context,NpcPredictionCache cache,Action step,string output)
        {
            var host=Get(context,"CombatObservation");owner=Get(Get(host,"Prediction"),"Native");receipts.Clear();receipts.Add("capture,receive,accepted,retired,kind,slot,error,history");
            var patches=new Harmony("JueMingR.Tests.AimLightPair");
            patches.Patch(owner.GetType().GetMethod("Receive",Flags),new HarmonyMethod(typeof(NativeAimLightSessionChecks).GetMethod(nameof(Before),Flags)),new HarmonyMethod(typeof(NativeAimLightSessionChecks).GetMethod(nameof(After),Flags)));
            var rows=new List<string>{"frame,tick,type,ready,selected,shown,capture,requests,rejected,refused,stepMs"};
            try
            {
                NPC.ClearAll();Projectile.ClearAll();Main.hardMode=true;SetTarget(110);
                var clock=Stopwatch.StartNew();int shown=0,blank=0,longest=0,notReady=0,firstReady=-1;
                for(int frame=0;frame<1680;frame++)
                {
                    if(frame==1200)SetTarget(204);
                    if(frame==1440)NativeCombatObservationChecks.Save(host,new ObservationOptions(collision:true));
                    if(frame==1500)NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,radius:25));
                    NativeCombatModeledImpactChecks.SampleMouse(context,Main.npc[0].Center);
                    var wall=Stopwatch.StartNew();step();wall.Stop();
                    var worker=GetOptional(owner,"Worker");bool ready=worker!=null && (double)Get(worker,"ReadyMilliseconds")>0;
                    if(!ready)notReady++;else if(firstReady<0)firstReady=frame;
                    var path=cache.Read(0);if(path==null){blank++;longest=Math.Max(longest,blank);}else{shown++;blank=0;Require(path.SampleTick==Main.GameUpdateCount && path.Count==121,"Published path preserves actual current+120.");}
                    rows.Add(string.Join(",",frame,Main.GameUpdateCount,Main.npc[0].type,ready?1:0,(bool)Get(Get(host,"Selection"),"HasTarget")?1:0,path==null?0:1,path?.CaptureTick??-1,Get(owner,"Requests"),Get(owner,"Rejected"),Get(owner,"Refused"),wall.Elapsed.TotalMilliseconds.ToString("R",CultureInfo.InvariantCulture)));
                }
                Require(firstReady>=0 && receipts.Count>1 && shown>0,"Bounded natural Session includes real readiness, reply consumption and publication.");
                Require(!(bool)Get(owner,"Failed"),"Observation cannot turn this real Session into Native.Failed.");
                var pending=GetOptional(owner,"pending");
                File.WriteAllText(Path.Combine(output,"light-pair-summary.txt"),"frames=1680;shown="+shown+";longestBlank="+longest+";notReady="+notReady+";firstReadyFrame="+firstReady+";requests="+Get(owner,"Requests")+";receive="+(receipts.Count-1)+";carryIn=0;carryOut="+(pending==null?-1L:(long)Get(pending,"Tick"))+";wallMs="+clock.Elapsed.TotalMilliseconds.ToString("R",CultureInfo.InvariantCulture));
                Console.WriteLine(File.ReadAllText(Path.Combine(output,"light-pair-summary.txt")));
            }
            finally
            {
                File.WriteAllLines(Path.Combine(output,"light-pair-frames.csv"),rows);File.WriteAllLines(Path.Combine(output,"light-pair-receipts.csv"),receipts);
                patches.UnpatchAll(patches.Id);owner=null;
            }
        }
        private static void SetTarget(int type)
        {var n=Main.npc[0];n.SetDefaults(type);n.whoAmI=0;n.active=true;n.target=0;n.position=new Vector2(700,70*16-n.height);n.velocity=Vector2.Zero;n.timeLeft=10000;}
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
