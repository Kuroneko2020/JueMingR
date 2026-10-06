using System;
using System.Reflection;
using System.Collections;
using System.Linq;
using JueMingR.Platform.Hotkeys;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatTargetMarkerChecks
    {
        internal static void Run(object context)
        {
            var marker=typeof(ObservationOptions).GetProperty("Marker");
            Require(marker!=null,"Target marker is an actual independent preference.");
            Require(!(bool)marker.GetValue(new ObservationOptions()),"Marker defaults OFF.");
            var host=Get(context,"CombatObservation");var selection=Get(host,"Selection");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");
            foreach(var n in Main.npc)n.active=false;
            Main.LocalPlayer.position=new Vector2(640,640);
            var target=Main.npc[2];target.SetDefaults(371);target.whoAmI=2;target.active=true;target.dontTakeDamage=false;target.immortal=false;target.friendly=false;target.position=new Vector2(720,650);
            var options=(ObservationOptions)Activator.CreateInstance(typeof(ObservationOptions),new object[]{false,false,false,false,false,25,true});
            NativeCombatObservationChecks.Save(host,options);NativeCombatObservationChecks.Fresh(context,host);
            Console.WriteLine("MARKER GATE enabled="+Get(host,"Enabled")+" marker="+Get(host,"Marker")+" session="+Get(host,"Session")+" active="+Main.LocalPlayer.active+" dead="+Main.LocalPlayer.dead+" targetLife="+target.life+" count="+Get(Get(context,"nativeNpcs"),"Count")+" selected="+Get(selection,"HasTarget"));
            Require((bool)Get(selection,"HasTarget") && ((NpcIdentity)Get(selection,"Target")).Slot==2,"Marker-only uses actual shared selection.");
            Require(cache.Required==0 && GetOptional(cache,"result")==null,"Marker-only has zero future demand or fabricated cache result.");
            Require(GetOptional(source,"Native")==null,"Marker-only does not start the exact comparison helper.");
            Call(host,"Poll");Require((bool)Get(selection,"HasTarget"),"Poll keeps marker-only target alive.");
            var shell=Get(context,"Shell");var state=Get(shell,"State");var renderer=Get(shell,"renderer");var layout=Get(state,"Layout");
            Call(renderer,"RefreshResources");Call(state,"Navigate",8);Call(renderer,"Prepare",state,960f,640f,1f);
            object Button()=>((IEnumerable)Get(layout,"Elements")).Cast<object>().Single(e=>Get(e,"Command").ToString()=="ObservationMarker");
            Require((string)Get(Button(),"Text")=="目标标记：开","Same-page initial marker text is committed ON.");
            var control=Get(renderer,"CombatObservationControls");Call(control,"Execute",Get(Button(),"Command"));
            var settings=(ObservationSettings)Get(host,"Settings");NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !settings.Busy;});
            Call(renderer,"Prepare",state,960f,640f,1f);Require((string)Get(Button(),"Text")=="目标标记：关","Actual same-page command refreshes OFF after commit.");
            var registry=(HotkeyRegistry)Get(Get(shell,"hotkeys"),"Registry");var bindings=(HotkeyBindings)Get(Get(shell,"hotkeys"),"Bindings");
            Require(bindings.Get("combat.target-marker")==null && registry.Find("combat.target-marker")==null,"Owner requires no marker public action or binding entrance.");
            Call(control,"Execute",Get(Button(),"Command"));
            NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !settings.Busy;});Call(renderer,"Prepare",state,960f,640f,1f);
            Require((string)Get(Button(),"Text")=="目标标记：开","Actual same-page command refreshes ON after commit.");
            Require(!((IEnumerable)Get(layout,"Elements")).Cast<object>().Any(e=>(string)GetOptional(e,"HotkeyTarget")=="combat.target-marker"),"F5 has no marker key picker.");
            var row=((IEnumerable)Get(layout,"Elements")).Cast<object>().Where(e=>new[]{"ObservationPolicy","ObservationCenter","ObservationDummy","ObservationMarker"}.Contains(Get(e,"Command").ToString())).ToArray();Require(row.Length==4 && row.All(e=>(float)Get(Get(e,"Rect"),"Y")== (float)Get(Get(Button(),"Rect"),"Y")),"Four settings share the same measured row.");
            RequireTitleRow(((IEnumerable)Get(layout,"Elements")).Cast<object>().ToArray(),row);
            foreach(object element in row)
            {var rect=Get(element,"Rect");Require((float)Get(rect,"X")>=0 && (float)Get(rect,"Right")<=522,"Measured marker controls stay inside their actual panel.");}
            var world=Get(host,"World");var display=Get(world,"Marker");Set(host,"LayerStatus",Enum.Parse(Get(host,"LayerStatus").GetType(),"Ready"));
            Main.screenWidth=960;Main.screenHeight=640;Main.screenPosition=new Vector2(300,300);Main.GameViewMatrix.Zoom=Vector2.One;
            target.position=new Vector2(720,450);target.netOffset=new Vector2(8,6);NativeCombatObservationChecks.Fresh(context,host);Call(world,"Prepare");NativeCombatPresentationChecks.Project(world);
            Require((bool)Get(display,"Visible"),"Marker prepares actual current selected region without a path.");var pieces=(Array)Get(display,"Pieces");Require(pieces.Length==6,"Native marker style emits exactly six pieces.");
            for(int i=0;i<6;i++)Require((int)Get(pieces.GetValue(i),"SourceY")==i%2*16,"Native atlas source rows alternate 0 and 16.");
            var positions=Enumerable.Range(0,6).Select(i=>(Vector2)Get(pieces.GetValue(i),"Position")).ToArray();
            Require(Math.Abs(positions.Average(p=>p.X)-(target.Center.X+8-300))<1 && Math.Abs(positions.Average(p=>p.Y)-(target.Center.Y+6-300))<1,"Native pieces surround current netOffset body after one coordinate transform.");
            var identity=(NpcIdentity)Get(selection,"Target");target.active=false;NativeCombatObservationChecks.Fresh(context,host);Call(world,"Prepare");NativeCombatPresentationChecks.Project(world);Require(!(bool)Get(display,"Visible"),"Death clears marker promptly without Draw.");
            target=new NPC();target.SetDefaults(371);target.active=true;target.whoAmI=2;target.position=new Vector2(720,450);Main.npc[2]=target;NativeCombatObservationChecks.Fresh(context,host);Call(world,"Prepare");NativeCombatPresentationChecks.Project(world);Require(!((NpcIdentity)Get(selection,"Target")).Equals(identity) && (bool)Get(display,"Visible"),"Replacement marker uses only the new shared identity.");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
            Require(!(bool)Get(selection,"HasTarget"),"Final consumer OFF retires selection.");
            NativeCombatMarkerDemandChecks.Run(context);
            Console.WriteLine("PASS TARGET-MARKER actual same-page command refresh, four same-row settings and no marker key, measured CPU layout, six native source-row pieces, current netOffset and replacement; marker-only zero prediction. No GPU Draw claim.");
        }
        internal static void Graphics(object context,ProbeGraphics graphics,string output)
        {
            graphics.LoadTexture("LockOnCursor","Images/UI/LockOn_Cursor");var atlas=Terraria.GameContent.TextureAssets.LockOnCursor;
            Terraria.Localization.LanguageManager.Instance.SetLanguage("zh-Hans");var shell=Get(context,"Shell");var ui=Get(shell,"State");var renderer=Get(shell,"renderer");var layout=Get(ui,"Layout");Call(renderer,"RefreshResources");Call(ui,"Navigate",8);
            foreach(var size in new[]{new[]{960,760,100},new[]{960,440,100},new[]{1440,900,150}})
            {
                Main.screenWidth=size[0];Main.screenHeight=size[1];Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();Main.UIScale=size[2]/100f;NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false);Call(ui,"RestoreVisible");NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false);
                Require(Math.Abs(Main.UIScale-size[2]/100f)<.001f && Math.Abs(Main.UIScaleMatrix.M11-size[2]/100f)<.001f && Math.Abs(((Matrix)Get(shell,"matrix")).M11-size[2]/100f)<.001f,"Requested actual UI scale and sampled Shell matrix match.");
                var elements=((IEnumerable)Get(layout,"Elements")).Cast<object>().ToArray();var button=elements.Single(e=>Get(e,"Command").ToString()=="ObservationMarker");
                var row=elements.Where(e=>new[]{"ObservationPolicy","ObservationCenter","ObservationDummy","ObservationMarker"}.Contains(Get(e,"Command").ToString())).ToArray();Require(row.Length==4 && row.All(e=>(float)Get(Get(e,"Rect"),"Y")== (float)Get(Get(button,"Rect"),"Y")),"Original font draws all four settings on the same row.");
                RequireTitleRow(elements,row);
                foreach(var e in row)Require((float)Get(Get(e,"Rect"),"Right")<=510,"Original Chinese font four controls fit panel at "+Main.UIScale);
                Call(ui,"ScrollTo",0f);Require((float)Get(ui,"Scroll")==0 && (bool)Get(ui,"Visible") && (bool)Get(ui,"Ready") && !(bool)Get(shell,"Failed"),"Real F5 draw gates ready with the full card top/title visible at scroll zero.");
                var pixels=graphics.Pixels(()=>Call(shell,"DrawLayer"),Main.UIScaleMatrix,size[0],size[1]);Require(!(bool)Get(shell,"Failed") && pixels.Count(p=>p.A>0)>1000 && pixels.Any(p=>p.R>180 && p.G>180 && p.B>180 && p.A>0),"Actual F5 panel and text draw visible pixels, not a saved empty canvas.");
                graphics.Image(System.IO.Path.Combine(output,"marker-f5-"+size[0]+"-"+size[1]+".png"),()=>Call(shell,"DrawLayer"),Main.UIScaleMatrix,size[0],size[1]);
            }
            if(Environment.GetEnvironmentVariable("JUEMINGR_MARKER_F5_ONLY")=="1"){Console.WriteLine("PASS TARGET-MARKER F5 title plus four controls same row, full top at scroll0, real Chinese font and actual150%. No world Draw rerun.");return;}
            Call(ui,"Close");Main.screenWidth=960;Main.screenHeight=640;Main.UIScale=1;Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();
            var host=Get(context,"CombatObservation");var world=Get(host,"World");var marker=Get(world,"Marker");
            foreach(var n in Main.npc)n.active=false;var target=Main.npc[2];target.SetDefaults(2);target.whoAmI=2;target.active=true;target.position=new Vector2(720,450);target.target=0;Main.LocalPlayer.position=new Vector2(640,500);Main.dayTime=false;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,marker:true));NativeCombatObservationChecks.Fresh(context,host);Call(world,"Prepare");NativeCombatPresentationChecks.Project(world);Require((bool)Get(marker,"Visible") && (int)Get(world,"StrokeCount")>5,"Marker and real path both prepared.");
            graphics.Image(System.IO.Path.Combine(output,"marker-path.png"),()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);
            // Prepare world content under a different camera. Only the real
            // subsequent Draw may consume the final matrix and event once.
            Main.screenPosition=new Vector2(200,200);Main.GameViewMatrix.Zoom=Vector2.One;Call(world,"Prepare");Main.screenPosition=new Vector2(470,330);Main.GameViewMatrix.Zoom=new Vector2(1.4f);var finalPixels=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);Require(finalPixels.Any(p=>p.A>0),"Final-camera real Draw contains path and marker pixels.");Require(finalPixels.Count(p=>p.A>0 && p.R>p.G+10 && p.G>p.B+20)>20,"The selected approximate path has independent gold pixels under the final matrix.");var route=Get(world,"preparedPath") as NpcTrajectory;Require(route!=null && route.Count>2,"Final-camera oracle uses a real retained route.");var routeCenter=new Vector2(route[2].Bounds.CenterX,route[2].Bounds.CenterY);var projected=Vector2.Transform(routeCenter-Main.screenPosition,Main.GameViewMatrix.ZoomMatrix);int nearby=0;for(int y=Math.Max(0,(int)projected.Y-40);y<Math.Min(640,(int)projected.Y+40);y++)for(int x=Math.Max(0,(int)projected.X-40);x<Math.Min(960,(int)projected.X+40);x++){var pixel=finalPixels[y*960+x];if(pixel.A>0 && pixel.R>pixel.G+10 && pixel.G>pixel.B+20)nearby++;}Require(nearby>5,"Actual path gold pixels meet a captured future body projected by the final camera.");var finalPieces=(Array)Get(marker,"Pieces");var mean=Vector2.Zero;foreach(var piece in finalPieces)mean+=(Vector2)Get(piece,"Position");mean/=6;Require(Vector2.Distance(mean,target.Center-Main.screenPosition)<2,"Real Draw projects captured marker with the final camera after Prepare.");
            NativeCombatObservationChecks.Save(host,new ObservationOptions(collision:true,path:true,marker:true));var geometry=Get(host,"Geometry");Call(geometry,"Clear");var pending=Call(geometry,"Event");Call(pending,"Rectangle",target.Hitbox,0,false);for(int i=0;i<6;i++){NativeQuickItemChecks.BeginWorldStep();Call(geometry,"PrepareEvents");}Call(world,"Prepare");graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);Require((bool)Get(pending,"Presented"),"Real XNA Draw consumes the six-update deferred event.");graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);Require((int)Get(world,"eventStart")== (int)Get(world,"eventEnd"),"Repeated real Draw does not replay the event.");Call(geometry,"Clear");Main.screenPosition=new Vector2(300,300);Main.GameViewMatrix.Zoom=Vector2.One;Call(world,"Prepare");
            Require(!atlas.Value.IsDisposed,"Drawing only borrows original atlas.");
            Terraria.GameContent.TextureAssets.LockOnCursor=null;
            var survived=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);
            Require((bool)Get(marker,"Failed") && survived.Take(960*450).Any(p=>p.A>0),"Controlled marker resource failure still executes remaining path pixels above the label on real XNA device.");
            Call(world,"Prepare");NativeCombatPresentationChecks.Project(world);Require((int)Get(world,"StrokeCount")>5 && !(bool)Get(marker,"Visible"),"Local marker failure is latched without retiring prediction or path commands.");
            Terraria.GameContent.TextureAssets.LockOnCursor=atlas;Call(host,"Set",5,true);Call(world,"Prepare");NativeCombatPresentationChecks.Project(world);Require((bool)Get(marker,"Visible"),"Explicit retry recovers marker from restored borrowed resource.");
            graphics.Image(System.IO.Path.Combine(output,"marker-recovered.png"),()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);
            using(var disposedBatch=new Microsoft.Xna.Framework.Graphics.SpriteBatch(graphics.GraphicsDevice))
            {disposedBatch.Dispose();bool shared=false;try{Call(marker,"Draw",disposedBatch);}catch(TargetInvocationException error){shared=error.InnerException is InvalidOperationException || error.InnerException is ObjectDisposedException;}Require(shared && !(bool)Get(marker,"Failed"),"Real disposed shared batch faults escape marker-local resource latch.");}
            foreach(int gravity in new[]{1,-1})foreach(float zoom in new[]{.8f,1.4f})
            {Main.LocalPlayer.gravDir=gravity;Main.GameViewMatrix.Zoom=new Vector2(zoom);Call(world,"Prepare");NativeCombatPresentationChecks.Project(world);Require((bool)Get(marker,"Visible"),"Marker survives supported gravity/zoom.");graphics.Image(System.IO.Path.Combine(output,"marker-transform-"+gravity+"-"+zoom.ToString(System.Globalization.CultureInfo.InvariantCulture)+".png"),()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);}
            Main.LocalPlayer.gravDir=1;Main.GameViewMatrix.Zoom=Vector2.One;NativeCombatMarkerCapacityChecks.Run(context,graphics,output);NativeGuidanceCameraChecks.Run(context,graphics,output);Main.mapFullscreen=true;Call(world,"Prepare");NativeCombatPresentationChecks.Project(world);Require(!(bool)Get(marker,"Visible"),"Fullscreen map retires marker commands.");Main.mapFullscreen=false;
            NativeCombatObservationChecks.Save(host,new ObservationOptions());Console.WriteLine("PASS TARGET-MARKER real original atlas/Draw, resource-failure remaining path pixels, bounded retry. This is isolated XNA, not gameplay FPS/owner acceptance.");
        }
        private static void RequireTitleRow(object[] elements,object[] row)
        {
            var title=elements.Single(e=>(string)GetOptional(e,"Text")=="辅助瞄准设置");var rect=Get(title,"Rect");var first=Get(row[0],"Rect");
            Require(Math.Abs((float)Get(rect,"Y")+(float)Get(rect,"Height")*.5f-(float)Get(first,"Y")-(float)Get(first,"Height")*.5f)<.01f && (float)Get(rect,"Right")+4<=(float)Get(first,"X"),"Visible original title shares the four-control row without overlapping buttons.");
            for(int i=0;i<row.Length;i++){var current=Get(row[i],"Rect");Require((float)Get(current,"Height")>=30 && (float)Get(current,"Right")<=510,"Measured controls retain readable, clickable bounds.");if(i>0)Require((float)Get(Get(row[i-1],"Rect"),"Right")+4<=(float)Get(current,"X"),"Measured row has nonoverlapping control gaps.");}
        }
    }
}
