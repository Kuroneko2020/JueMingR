using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeViewCacheCostChecks
    {
        internal static void Run(object view,Action draw,string output)
        {
            var cache=Get(view,"Cache");var origin=Main.LocalPlayer.position;
            try
            {
                foreach(float target in new[]{1f,2f,.5f,.3f})
                foreach(string motion in target>=1?new[]{"still"}:new[]{"still","moving","adjusting"})
                foreach(bool direct in new[]{true,false,true,false})
                {
                    Main.LocalPlayer.position=origin;Main.GameZoomTarget=target;Set(cache,"ForceDirect",direct);Call(view,"SetUnlockEnabled",target>=1?!direct:true);
                    for(int i=0;i<8;i++)draw();
                    var values=new List<double>();var routes=new Dictionary<string,int>();var rows=new List<string>{"draw_wall_ms,target,render_zoom,route,visible_width,visible_height"};
                    int generation=(int)Get(cache,"Generation"),gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2);
                    long allocations=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize,managed=GC.GetTotalMemory(false),memory=Process.GetCurrentProcess().PrivateMemorySize64;
                    var timer=new Stopwatch();
                    for(int i=0;i<30;i++)
                    {
                        if(motion=="moving")Main.LocalPlayer.position+=new Vector2(1,1);
                        if(motion=="adjusting")Main.GameZoomTarget=target+i*.02f;
                        timer.Restart();draw();timer.Stop();values.Add(timer.Elapsed.TotalMilliseconds);
                        string route=Get(Get(view,"Plan"),"Route").ToString();if(!routes.ContainsKey(route))routes[route]=0;routes[route]++;
                        rows.Add(timer.Elapsed.TotalMilliseconds.ToString("R",CultureInfo.InvariantCulture)+","+Main.GameZoomTarget.ToString("R",CultureInfo.InvariantCulture)+","+Main.GameViewMatrix.RenderZoom.X.ToString("R",CultureInfo.InvariantCulture)+","+route+","+Main.Camera.ScaledSize.X+","+Main.Camera.ScaledSize.Y);
                    }
                    string name="cost-"+target.ToString(CultureInfo.InvariantCulture)+"-"+motion+"-"+(direct?"direct":"auto");int pass=Directory.GetFiles(output,name+"-*.csv").Length+1;
                    File.WriteAllLines(Path.Combine(output,name+"-"+pass+".csv"),rows);
                    var sorted=values.OrderBy(v=>v).ToArray();
                    Console.WriteLine("CACHE COST target="+target+" permission="+Get(view,"UnlockEnabled")+" motion="+motion+" override="+(direct?"direct":"auto")+" pass="+pass+" n=30 mean="+values.Average()+" median="+sorted[15]+" p95="+sorted[28]+" max="+sorted[29]+" allocated="+(AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-allocations)+" managed-delta="+(GC.GetTotalMemory(false)-managed)+" private-delta="+(Process.GetCurrentProcess().PrivateMemorySize64-memory)+" private-total="+Process.GetCurrentProcess().PrivateMemorySize64+" GC="+(GC.CollectionCount(0)-gc0)+"/"+(GC.CollectionCount(1)-gc1)+"/"+(GC.CollectionCount(2)-gc2)+" generations="+generation+"->"+Get(cache,"Generation")+" theoretical-old+new-peak="+Get(cache,"PeakWorldBytes")+" routes="+String.Join(";",routes.Select(v=>v.Key+":"+v.Value)));
                    if(target<1&&!direct&&motion=="still")Require(routes.ContainsKey("TileCache"),"stable automatic route actually consumes three cached native layers");
                    if(!direct&&motion=="moving")Require(routes.ContainsKey("Direct"),"fractional camera motion uses correct direct fallback rather than refilling every output phase");
                }
                var update=(Action<ulong>)Delegate.CreateDelegate(typeof(Action<ulong>),view,view.GetType().GetMethod("Update"));
                foreach(float target in new[]{1f,.3f,10f})
                foreach(bool permission in new[]{false,true})
                {
                    Main.GameZoomTarget=target;Call(view,"SetUnlockEnabled",permission);Set(cache,"ForceDirect",false);for(int j=0;j<8;j++)draw();
                    for(ulong i=0;i<1000;i++)update(i);
                    var timer=new Stopwatch();long before=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;timer.Start();
                    for(ulong i=0;i<100000;i++)update(i);timer.Stop();
                    long allocated=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-before;
                    Console.WriteLine("CACHE OWNER UPDATE target="+target+" actual="+Main.GameViewMatrix.RenderZoom.X+" permission="+permission+" calls=100000 elapsed-ms="+timer.Elapsed.TotalMilliseconds+" allocated="+allocated+" full-native-Update=False");
                    Require(allocated==0,"stable HostView.Update callback does not allocate per update");
                }
            }
            finally{Main.LocalPlayer.position=origin;Main.GameZoomTarget=.3f;Set(cache,"ForceDirect",false);draw();draw();}
            Console.WriteLine("PASS CACHE COST normal1/2 OFF/ON and interleaved same-zoom direct/auto x2 static/moving plus monotonic .02 target traces; all raw Draw-wall samples retained, no GPU-time/FPS claim.");
        }
    }
}
