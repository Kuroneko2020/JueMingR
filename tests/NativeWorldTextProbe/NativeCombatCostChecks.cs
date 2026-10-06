using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Finite, isolated Host measurements. Never invoked by product code. Old
    // and new candidates run in separate processes with their complete DLL set.
    internal static class NativeCombatCostChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        internal static void Run(object context,string output)
        {
            Directory.CreateDirectory(output);var host=Get(context,"CombatObservation");var layer=Get(host,"World");var geometry=Get(host,"Geometry");var cache=Get(Get(host,"Prediction"),"Cache");var settings=(ObservationSettings)Get(host,"Settings");
            for(int i=0;i<Main.maxPlayers;i++)if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};
            var update=(Action)Delegate.CreateDelegate(typeof(Action),context,context.GetType().GetMethod("UpdateRuntime",Flags));
            var prepareWorld=(Action)Delegate.CreateDelegate(typeof(Action),layer,layer.GetType().GetMethod("Prepare",Flags));
            Action prepare=()=>{prepareWorld();if((bool)Get(host,"Enabled") && (bool)Get(host,"CanDraw"))NativeCombatPresentationChecks.Project(layer);};
            var allocation=typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",BindingFlags.Public|BindingFlags.Static);var allocated=allocation==null?null:(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),allocation);
            Set(host,"LayerStatus",Enum.Parse(Get(host,"LayerStatus").GetType(),"Ready"));Main.screenWidth=960;Main.screenHeight=640;Main.screenPosition=Vector2.Zero;Main.GameViewMatrix.Zoom=Vector2.One;Main.hideUI=false;Main.mapFullscreen=false;Main.LocalPlayer.gravDir=1;Main.LocalPlayer.position=new Vector2(640,640);Main.LocalPlayer.itemAnimation=0;Main.dayTime=false;
            var rows=new List<string>{"scenario\tsamples\tdamage_p50_us\tdamage_p95_us\thost_prepare_p50_us\thost_prepare_p95_us\tdamage_bytes_per_sample\thost_prepare_bytes_per_sample\tGC_0_1_2\tdetail"};
            foreach(string scenario in new[]{"off","idle","stable","changing","dense","toggle","marker-only","dual","marker-idle","marker-dense"})
            {
                foreach(var n in Main.npc)n.active=false;foreach(var p in Main.projectile)p.active=false;Call(geometry,"Clear");Call(cache,"Clear");
                bool marker=scenario.StartsWith("marker-") || scenario=="dual",many=scenario=="dense" || scenario=="marker-dense",none=scenario=="idle" || scenario=="marker-idle";
                bool collision=!marker,pathEnabled=marker?scenario=="dual":!many;int shots=none?0:many?Main.maxProjectiles:1,npcs=none?0:many?Main.maxNPCs:1;
                for(int i=0;i<npcs;i++)
                {var n=Main.npc[i];n.SetDefaults(many?1:scenario=="changing"?370:488);n.active=true;n.whoAmI=i;n.target=0;n.damage=0;n.life=n.lifeMax=1000000;n.timeLeft=750;n.position=many?new Vector2(20+i%40*22,30+i/40*60):new Vector2(700,600);if(scenario=="changing"){n.ai[0]=1;n.localAI[0]=1;n.velocity=new Vector2(16,0);}}
                for(int i=0;i<shots;i++)
                {var p=Main.projectile[i];p.SetDefaults(1);p.active=true;p.whoAmI=i;p.owner=Main.myPlayer;p.damage=1;p.friendly=true;p.penetrate=p.maxPenetrate=-1;p.position=new Vector2(20+i%50*18,30+i/50*20);}
                SetOptions(scenario=="off"?new ObservationOptions():new ObservationOptions(collision:collision,path:pathEnabled,dummy:true,marker:marker));
                void Input(int index)
                {
                    // Advancing the fixture clock, native oracle and settings
                    // I/O are outside both measured windows. Product retirement
                    // still occurs inside the next actual UpdateRuntime/Poll.
                    NativeQuickItemChecks.BeginWorldStep();
                    if(scenario=="changing"){bool prior=Main.dedServ;Main.dedServ=true;try{Main.npc[0].UpdateNPC(0);}finally{Main.dedServ=prior;}}
                    if(scenario=="toggle")SetOptions(new ObservationOptions(collision:index%4==1 || index%4==3,path:index%4>=2,dummy:true));
                }
                void Damage(){for(int i=0;i<shots;i++)Main.projectile[i].Damage();}
                var damageTime=new double[120];var hostTime=new double[120];long damageBytes=0,hostBytes=0;
                for(int i=0;i<30;i++){Input(i);Damage();update();prepare();}
                string[] counterNames={"Builds","Steps","Reuses","Rolls"};var before=new int?[counterNames.Length];for(int i=0;i<before.Length;i++)before[i]=Counter(cache,counterNames[i]);int? captured=Counter(geometry,"ProjectileSamples");int[] gc={GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)};
                for(int i=0;i<120;i++)
                {
                    Input(i+30);long bytes=allocated==null?0:allocated(),start=Stopwatch.GetTimestamp();Damage();damageTime[i]=(Stopwatch.GetTimestamp()-start)*1000000.0/Stopwatch.Frequency;if(allocated!=null)damageBytes+=allocated()-bytes;
                    bytes=allocated==null?0:allocated();start=Stopwatch.GetTimestamp();update();prepare();hostTime[i]=(Stopwatch.GetTimestamp()-start)*1000000.0/Stopwatch.Frequency;if(allocated!=null)hostBytes+=allocated()-bytes;
                }
                int strokes=(int)Get(layer,"StrokeCount");bool limited=(bool)Get(layer,"limited");object path=Call(cache,"Read",0);
                if(scenario=="off" || none)Require(strokes==0 && path==null,"cost fixture off/empty output remains empty");
                if(scenario=="dense")Require(strokes==4800 && !limited && path==null,"cost comparison requires identical complete ordinary dense output, not lower work through omissions");
                if(marker)
                {
                    var prediction=Get(host,"Prediction");Require(GetOptional(prediction,"Native")==null,"Marker costs cannot start exact worker.");
                    Require(((NpcPredictionCache)cache).Required==(pathEnabled?120:0),"Actual marker-only/dual cache demand remains independent.");
                    Require((bool)Get(Get(layer,"Marker"),"Visible")==!none,"Actual current marker command survives the measured update/prepare.");
                    if(!pathEnabled)Require(strokes==0 && path==null,"Marker-only costs contain selection/commands without path work.");
                    else Require(path!=null && (int)Get(path,"Count")==121,"Dual cost includes the full unchanged selected path.");
                }
                string detail="nativeDamageInputs="+shots+";activeNPC="+npcs+";strokes="+strokes+";limited="+limited+";pathCount="+(path==null?"0":Get(path,"Count").ToString())+";pathStop="+(path==null?"NA":Get(path,"Stop").ToString());
                for(int i=0;i<before.Length;i++){var after=Counter(cache,counterNames[i]);detail+=";"+counterNames[i]+"="+(before[i].HasValue && after.HasValue?(after.Value-before[i].Value).ToString():"NA");}
                var sampled=Counter(geometry,"ProjectileSamples");detail+=";capture="+(captured.HasValue && sampled.HasValue?(sampled.Value-captured.Value).ToString():"NA");
                Array.Sort(damageTime);Array.Sort(hostTime);rows.Add(string.Join("\t",scenario,"120",F(damageTime[60]),F(damageTime[113]),F(hostTime[60]),F(hostTime[113]),allocated==null?"NA":F(damageBytes/120.0),allocated==null?"NA":F(hostBytes/120.0),(GC.CollectionCount(0)-gc[0])+"/"+(GC.CollectionCount(1)-gc[1])+"/"+(GC.CollectionCount(2)-gc[2]),detail));
            }
            foreach(var row in rows)Console.WriteLine(row);File.WriteAllLines(Path.Combine(output,"combat-costs.tsv"),rows);
            Console.WriteLine("COST IDENTITY "+host.GetType().Assembly.Location+" MVID="+host.GetType().Assembly.ManifestModule.ModuleVersionId+"; CPU natural Damage and actual Host update/prepare; no GPU/readback, gameplay FPS or long-run claim. Release-only counters are NA.");
            SetOptions(new ObservationOptions());update();prepare();
            void SetOptions(ObservationOptions value){NativeQuickItemChecks.Until(()=>{settings.Poll();return settings.Ready;});Require(settings.Set(value),"cost setting accepted");NativeQuickItemChecks.Until(()=>{settings.Poll();return !settings.Busy;});Require(settings.CompletionSucceeded,"cost setting committed");}
        }
        private static int? Counter(object owner,string name){var value=GetOptional(owner,name);return value is int?(int?)value:null;}
        private static string F(double value){return value.ToString("F3",CultureInfo.InvariantCulture);}
    }
}
