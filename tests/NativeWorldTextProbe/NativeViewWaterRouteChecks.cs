using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.Shaders;
using Terraria.Graphics;
using Terraria.Graphics.Effects;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeViewWaterRouteChecks
    {
        private static object water;
        private static WaterShaderData shader;
        private static int foreground,masks,steps,applies,inverts;
        private static void WaterDraw(bool __0,bool __runOriginal)
        {if(__0||!__runOriginal)return;if((bool)Get(water,"RenderingMask"))masks++;else foreground++;}
        private static void Step(WaterShaderData __instance){if(ReferenceEquals(__instance,shader))steps++;}
        private static void Apply(WaterShaderData __instance){if(ReferenceEquals(__instance,shader))applies++;}
        private static void Invert(Filter __instance){if(ReferenceEquals(__instance,Filters.Scene["testInvert"]))inverts++;}
        internal static void Run(Main game,object view,Action draw,Func<RenderTarget2D> final,string output)
        {
            var cache=Get(view,"Cache");water=Get(view,"Water");var filter=Filters.Scene["WaterDistortion"];shader=(WaterShaderData)filter.GetShader();int quality=Main.WaveQuality;
            var audit=new Harmony("JueMingR.Tests.View.WaterRoute");
            audit.Patch(AccessTools.Method(typeof(Main),"DrawWaters"),postfix:new HarmonyMethod(typeof(NativeViewWaterRouteChecks),nameof(WaterDraw)));
            audit.Patch(AccessTools.Method(typeof(WaterShaderData),"StepLiquids"),prefix:new HarmonyMethod(typeof(NativeViewWaterRouteChecks),nameof(Step)));
            audit.Patch(AccessTools.Method(typeof(WaterShaderData),"Apply"),postfix:new HarmonyMethod(typeof(NativeViewWaterRouteChecks),nameof(Apply)));
            audit.Patch(AccessTools.Method(typeof(Filter),"Apply"),postfix:new HarmonyMethod(typeof(NativeViewWaterRouteChecks),nameof(Invert)));
            try
            {
                Main.GameZoomTarget=.3f;Set(cache,"ForceDirect",false);Call(view,"SetUnlockEnabled",true);draw();draw();
                Filters.Scene.Activate("testInvert",Main.LocalPlayer.Center);Filters.Scene["testInvert"].Opacity=1;
                foreach(string phase in new[]{"zero-faded","one-active","zero-tail"})
                {
                    Main.WaveQuality=phase=="one-active"?1:0;
                    if(phase=="zero-faded"){Filters.Scene.Deactivate("WaterDistortion");filter.Opacity=0;}
                    else if(phase=="one-active"){Filters.Scene.Activate("WaterDistortion",Main.LocalPlayer.Center);filter.Opacity=1;}
                    else{Filters.Scene.Deactivate("WaterDistortion");filter.Opacity=.5f;}
                    // Only native Update chooses wave flags. A zero duration
                    // preserves the controlled, genuinely nonzero fade tail.
                    Filters.Scene.Update(new GameTime(TimeSpan.Zero,TimeSpan.Zero));
                    foreground=masks=steps=applies=inverts=0;draw();
                    Console.WriteLine("WATER ROUTE phase="+phase+" IsInUse="+filter.IsInUse()+" opacity="+filter.Opacity+" foreground-original="+foreground+" mask-original="+masks+" steps="+steps+" waterApply="+applies+" invertApply="+inverts+" route="+Get(Get(view,"Plan"),"Route"));
                    Require(inverts==1,"other actual active scene filter still captures when water quality is zero");
                    Require(steps==0,"native quality0/1 does not manufacture visual simulation steps");
                    if(phase=="zero-faded")Require(foreground==1&&masks==0&&applies==0,"fully faded quality0 omits only extra physical mask and restores one original foreground liquid Draw");
                    else Require(foreground==0&&masks==1&&applies==1,"quality1 and opacity tail retain actual physical water mask and unique foreground Draw");
                    using(var file=File.Create(Path.Combine(output,"water-route-"+phase+".png")))final().SaveAsPng(file,Main.screenWidth,Main.screenHeight);
                }
                Main.WaveQuality=3;Filters.Scene.Activate("WaterDistortion",Main.LocalPlayer.Center);filter.Opacity=1;
                for(int i=0;i<3;i++)shader.Update(new GameTime(TimeSpan.Zero,TimeSpan.FromSeconds(1.0/60)));
                int queued=(int)Get(shader,"_queuedSteps");steps=0;draw();
                Require(queued>=3&&steps==2&&(int)Get(shader,"_queuedSteps")==0,"one deferred native PreDraw consumes exactly min(queued,2), never repeated or doubled");
                steps=0;draw();Require(steps==0,"another Draw without native Update consumes no new queued steps");
                var first=Get(water,"first");var second=Get(water,"second");var live=(RenderTarget2D)Get(shader,"_distortionTarget");var offset=Get(shader,"_lastDistortionDrawOffset");
                var before=new Color[live.Width*live.Height];live.GetData(before);
                for(int i=0;i<20;i++){Set(cache,"ForceDirect",i%2==0);draw();}
                var after=new Color[before.Length];live.GetData(after);int changes=0;for(int i=0;i<before.Length;i++)if(before[i]!=after[i])changes++;
                Require(ReferenceEquals(Get(water,"first"),first)&&ReferenceEquals(Get(water,"second"),second)&&Get(shader,"_lastDistortionDrawOffset").Equals(offset)&&changes==0,"tile-cache/direct transitions retain native b=0 wave domain, both buffers, phase and frozen wave pixels");
                Console.WriteLine("PASS WATER ROUTE actual native quality0/1/tail DrawWaters/Apply, queued3->Step2 thenStep0,20 partial-cache/direct transitions unchanged wave phase/pixels; no seven-layer water route claim.");
            }
            finally{audit.UnpatchAll(audit.Id);Main.WaveQuality=quality;Filters.Scene.Deactivate("WaterDistortion");Filters.Scene.Deactivate("testInvert");Filters.Scene.Update(new GameTime(TimeSpan.Zero,TimeSpan.FromSeconds(2)));Set(cache,"ForceDirect",false);water=null;shader=null;}
        }
    }
}
