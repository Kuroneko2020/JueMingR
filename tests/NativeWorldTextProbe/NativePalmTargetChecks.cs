using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Drawing;
using Terraria.Localization;
using Terraria.Utilities;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativePalmTargetChecks
    {
        private const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Consumers(object context)
        {
            using(var f=new NativeActorGestureChecks.Fixture(context,1024))
            {
                Main.netMode=0;f.Reset();var trunk=Make(22,16);Main.tile[20,20]=trunk;
                Rectangle native=WorldGen.GetTileVisualHitbox(20,20).Value;Point center=native.Center;
                f.Neutral(center.X,center.Y);f.Proof();f.Sample(true,center.X,center.Y);f.Proof();f.Update();
                Console.WriteLine("PALM-TRUNK-ORACLE native="+native+" created="+NativeActorGestureChecks.Created+" text="+NativeActorGestureChecks.Text);
                Require(NativeActorGestureChecks.Created==1 && NativeActorGestureChecks.Text.Contains(Language.GetTextValue("MapObject.PalmTree")),"native shifted trunk hitbox selects the actual source in the neighbouring column");
                using(var render=new RenderHarness())
                {
                foreach(bool oasis in new[]{false,true}) foreach(int offset in new[]{-16,16}) foreach(int variant in new[]{0,1,2}) foreach(int wind in new[]{-1,1})
                {
                    f.Reset();Main.tile[20,20]=Main.tile[20,21]=new Tile();int sx=oasis?400:20;Main.screenPosition=new Vector2(sx*16-320,0);Main.tile[sx,20]=Make(88+22*variant,offset);Main.tile[sx,21]=Ground();
                    Main.windSpeedCurrent=.6f*wind;render.Render(new[]{new Point(sx,20)});
                    Require(render.Native.Count==1 && render.Flushed==1,"actual DrawTrees queues and reaches controlled final nature display sink");
                    NativeDraw draw=render.Native[0];Require(draw.Source.Width==(oasis?114:80) && draw.Source.Height==(oasis?98:80),"actual native biome selects coast/oasis texture geometry");
                    Require(draw.Rotation*wind>0,"actual native wind transforms include both rotation directions");
                    Vector2 point=draw.WorldPoint(new Vector2(8,draw.Source.Height/2))-Main.screenPosition;int x=(int)Math.Round(point.X),y=(int)Math.Round(point.Y);
                    f.Neutral(x,y);f.Proof();f.Sample(true,x,y);object frozen=Get(f.Targets,"frozen");
                    Require((int)Get(frozen,"ItemType")==0 && draw.OracleContains(new Vector2(x,y)+Main.screenPosition),"crown is a descriptive target inside actual native transformed quad");
                    Rectangle bounds=(Rectangle)Get(frozen,"WorldBounds");foreach(Vector2 corner in draw.Corners())Require(corner.X>=bounds.Left && corner.X<=bounds.Right && corner.Y>=bounds.Top && corner.Y<=bounds.Bottom,"frozen visible bounds enclose all four original rotated native vertices");
                    var savedRandom=Main.rand;Main.rand=new UnifiedRandom(937);var expectedRandom=new UnifiedRandom(937);
                    try{var resolve=Get(f.Targets,"palms").GetType().GetMethod("Resolve",F);for(int i=0;i<20;i++){object[] args={new Vector2(x,y)+Main.screenPosition,null};Require(resolve.Invoke(null,args).ToString()=="Found","bounded crown resolver repeatedly identifies the same actual native quad");}Require(Main.rand.Next()==expectedRandom.Next(),"crown hit observation consumes no RNG");}finally{Main.rand=savedRandom;}
                    f.Proof();f.Update();
                    Console.WriteLine("PALM-PRODUCER-ORACLE oasis="+oasis+" frame="+(88+22*variant)+","+offset+" wind="+wind+" source="+draw.Source+" rotation="+draw.Rotation+" position="+draw.Position+" origin="+draw.Origin+" public="+NativeActorGestureChecks.Created);
                    Require(NativeActorGestureChecks.Created==1 && NativeActorGestureChecks.Text.Contains(Language.GetTextValue("MapObject.PalmTree")),"actual producer quad -> request/proof -> final native chat selects the frozen crown source");
                }
                Boundaries(f,render);
                }
            }
            Console.WriteLine("PASS palm geometry L2: native VisualHitbox and actual DrawTrees/NextNatureRenderer queue parameters through PostDrawTiles normal return and controlled UI boundary to final chat. Resource texture and final GPU sink controlled; no rendered/game claim.");
        }
        private static void Boundaries(NativeActorGestureChecks.Fixture f,RenderHarness render)
        {
            short glow=Main.tileGlowMask[323];
            try
            {
            // Main.Initialize (.8:8618) sets ordinary glow entries to -1. A
            // CPU world has not executed that full initializer (zero default).
            Main.tileGlowMask[323]=-1;
            foreach(string change in new[]{"outside-quad","camera","foreground","overlap","dark","echo","missing","ui-skip","outer-error","viewport","source-change","unreceived"})
            {
                f.Reset();Main.netMode=0;Main.screenPosition=Vector2.Zero;
                for(int cx=16;cx<26;cx++)for(int cy=10;cy<24;cy++)Main.tile[cx,cy]=new Tile();
                Main.tile[20,20]=Make(88,16);Main.tile[20,21]=Ground();
                Main.sectionManager.SetAllSectionsLoaded();render.Dark=false;
                if(change=="overlap"){Main.tile[21,20]=Make(88,0);Main.tile[21,21]=Ground();}
                render.Render(change=="overlap"?new[]{new Point(20,20),new Point(21,20)}:new[]{new Point(20,20)});
                NativeDraw draw=render.Native[0];Vector2 point=draw.WorldPoint(new Vector2(change=="outside-quad"?draw.Source.Width+4:draw.Source.Width/2,draw.Source.Height/2));int x=(int)Math.Round(point.X),y=(int)Math.Round(point.Y);
                if(change=="overlap")Require(render.Native.Count==2 && render.Native.All(d=>d.OracleContains(new Vector2(x,y))),"two real native crown quads overlap this exact point");
                if(change=="outside-quad")Require(!draw.OracleContains(new Vector2(x,y)),"independent transformed quad outside point");
                f.Neutral(x,y);f.Proof();
                if(change=="foreground"){var block=new Tile{type=3,frameX=0,frameY=0};block.active(true);block.fullbrightBlock(true);Main.tile[x/16,y/16]=block;}
                if(change=="dark"){Main.tile[20,20].fullbrightBlock(false);render.Dark=true;Console.WriteLine("PALM-DARK-INPUT light="+Lighting.GetColor(20,20)+" fullbright="+Main.tile[20,20].fullbrightBlock()+" flame="+Main.tileFlame[323]+" perspective="+Main.SceneMetrics.PerspectivePlayer.biomeSight+","+Main.SceneMetrics.PerspectivePlayer.findTreasure+","+Main.SceneMetrics.PerspectivePlayer.dangerSense);}
                if(change=="echo"){Main.tile[20,20].invisibleBlock(true);}
                if(change=="missing")Call(Get(f.Targets,"palms"),"Invalidate");
                if(change=="ui-skip")f.Proof(); // A UI completion without the outer producer retires its prior table.
                if(change=="outer-error")
                {
                    render.ThrowAtFlush=true;try{render.Render(new[]{new Point(20,20)});}catch(InvalidOperationException){}finally{render.ThrowAtFlush=false;}
                }
                if(change=="viewport")Main.GameViewMatrix.Zoom=new Vector2(1.1f);
                if(change=="source-change")Main.tile[20,20].frameY=14;
                if(change=="unreceived"){Main.netMode=1;Main.sectionManager=new WorldSections(6,1);}
                f.Sample(true,x,y);object frozen=Get(f.Targets,"frozen");
                if(change=="camera"){Main.screenPosition=new Vector2(8,0);}
                f.Proof();f.Update();
                bool palm=change=="camera";bool other=change=="outside-quad"||change=="foreground";
                Console.WriteLine("PALM-BOUNDARY-DETAIL case="+change+" public="+NativeActorGestureChecks.Created+" text="+NativeActorGestureChecks.Text+" ignoreLight="+Terraria.ID.TileID.Sets.IgnoreDrawLightConditions[323]+" glow="+Main.tileGlowMask[323]+" sense="+Main.LocalPlayer.dangerSense+","+Main.LocalPlayer.findTreasure);
                Require(NativeActorGestureChecks.Created==(palm||other?1:0),"palm frozen receipt failure/foreground boundary "+change);
                if(palm)Require(NativeActorGestureChecks.Text.Contains(Language.GetTextValue("MapObject.PalmTree")),"camera movement retains actual source anchor");
                if(other)Require(!NativeActorGestureChecks.Text.Contains(Language.GetTextValue("MapObject.PalmTree")),"quad negative/real foreground never silently chooses palm");
                Console.WriteLine("PALM-BOUNDARY case="+change+" public="+NativeActorGestureChecks.Created+" notices="+NativeActorGestureChecks.Notices);
                Main.GameViewMatrix.Zoom=Vector2.One;
            }
            // Adjacent unknown sections alone do not turn a received, unrelated
            // air/wall/wire target into a supposed missing palm receipt.
            Main.netMode=1;Main.sectionManager=new WorldSections(6,1);Main.sectionManager.SetSectionLoaded(0,0);
            f.Reset();Main.screenPosition=new Vector2(2800,0);Main.tile[199,20]=new Tile();f.Neutral(392,328);f.Proof();f.Sample(true,392,328);f.Proof();f.Update();
            Require(NativeActorGestureChecks.Created==1,"received air next to an unreceived section remains normal air");
            Main.netMode=0;Main.screenPosition=Vector2.Zero;Main.sectionManager.SetAllSectionsLoaded();render.Dark=false;f.Reset();
            var sources=new Point[129];for(int i=0;i<sources.Length;i++){sources[i]=new Point(10+i,20);Main.tile[10+i,20]=Make(88,0);Main.tile[10+i,21]=Ground();}
            render.Render(sources);Require(render.Native.Count==129,"actual native producer supplies more than the bounded crown capacity");f.Neutral(328,288);f.Proof();
            Require(!(bool)Get(Get(f.Targets,"palms"),"available"),"overflow never publishes a partial crown table");
            f.Sample(true,328,288);f.Proof();f.Update();Require(NativeActorGestureChecks.Created==0,"overflow cannot expose a misleading partial crown target");
            Call(f.Announcements,"SetEnabled",false);int before=(int)Get(Get(f.Targets,"palms"),"buildingCount");render.Render(sources);f.Proof();
            Require(!(bool)Get(Get(f.Targets,"palms"),"recording") && !(bool)Get(Get(f.Targets,"palms"),"available") && before==(int)Get(Get(f.Targets,"palms"),"buildingCount"),"disabled target demand performs no crown collection or partial-table publication");
            Call(f.Announcements,"SetEnabled",true);Console.WriteLine("PASS palm bounded workload: actual129-entry source overflow rejects whole table; disabled demand retains no active/available receipt or new entries.");
            Console.WriteLine("PASS palm lifecycle/foreground negatives and adjacent unreceived-section air; texture quad boundaries are not alpha-pixel precision.");
            }
            finally {Main.tileGlowMask[323]=glow;}
        }
        private static Tile Make(int x,int y){var tile=new Tile{type=323,frameX=(short)x,frameY=(short)y};tile.active(true);tile.fullbrightBlock(true);return tile;}
        private static Tile Ground(){var tile=new Tile{type=53};tile.active(true);return tile;}
        private struct NativeDraw
        {
            internal Vector2 Position,Origin,Camera;internal Rectangle Source;internal float Rotation,Scale;
            internal Vector2 WorldPoint(Vector2 local){return Position+Camera+Vector2.Transform((local-Origin)*Scale,Matrix.CreateRotationZ(Rotation));}
            internal Vector2[] Corners(){return new[]{WorldPoint(Vector2.Zero),WorldPoint(new Vector2(Source.Width,0)),WorldPoint(new Vector2(Source.Width,Source.Height)),WorldPoint(new Vector2(0,Source.Height))};}
            internal bool OracleContains(Vector2 point)
            {
                var corners=Corners();
                float previous=0;for(int i=0;i<4;i++){Vector2 edge=corners[(i+1)%4]-corners[i],relative=point-corners[i];float cross=edge.X*relative.Y-edge.Y*relative.X;if(i>0 && cross*previous<0)return false;if(cross!=0)previous=cross;}return true;
            }
        }
        private sealed class RenderHarness:IDisposable
        {
            private static RenderHarness active;
            internal readonly List<NativeDraw> Native=new List<NativeDraw>();internal int Flushed;
            internal bool Dark,ThrowAtFlush;
            private readonly TileDrawing drawing=new TileDrawing(null);
            private readonly Harmony harmony=new Harmony("JueMingR.Tests.PalmRender."+Guid.NewGuid().ToString("N"));
            private readonly SpriteBatch oldBatch=Main.spriteBatch;
            private readonly bool oldSway=Main.SettingsEnabled_TilesSwayInWind;private readonly float oldWind=Main.windSpeedCurrent;private readonly double oldSurface=Main.worldSurface;
            private readonly Texture2D texture;
            internal RenderHarness()
            {
                active=this;texture=(Texture2D)FormatterServices.GetUninitializedObject(typeof(Texture2D));GC.SuppressFinalize(texture);typeof(Texture2D).GetField("_width",F).SetValue(texture,400);typeof(Texture2D).GetField("_height",F).SetValue(texture,1000);
                Main.spriteBatch=(SpriteBatch)FormatterServices.GetUninitializedObject(typeof(SpriteBatch));GC.SuppressFinalize(Main.spriteBatch);
                Main.SettingsEnabled_TilesSwayInWind=true;Main.windSpeedCurrent=.6f;Main.worldSurface=50;Set(drawing,"_treeWindCounter",.125d);
                Patch(typeof(TileDrawing).GetMethod("GetTreeTopTexture",F),nameof(Texture));
                Patch(typeof(SpriteBatchBeginner).GetMethod("Begin",new[]{typeof(SpriteBatch)}),nameof(NoDisplay));
                foreach(var method in typeof(SpriteBatch).GetMethods().Where(m=>m.Name=="Begin"))Patch(method,nameof(NoDisplay));
                Patch(typeof(SpriteBatch).GetMethod("End",Type.EmptyTypes),nameof(NoDisplay));
                Patch(typeof(NextNatureRenderer).GetMethod("DrawAfterAllObjects"),nameof(Flush));
                harmony.Patch(typeof(NextNatureRenderer).GetMethod("DrawNature"),postfix:new HarmonyMethod(typeof(RenderHarness).GetMethod(nameof(Queued),F)));
                // The enclosing bounded World owns a default white fallback.
                // This explicit scenario source must run first and own the
                // result; otherwise its false-prefix can skip our dark input.
                harmony.Patch(typeof(Lighting).GetMethod("GetColor",new[]{typeof(int),typeof(int)}),prefix:new HarmonyMethod(typeof(RenderHarness).GetMethod(nameof(Light),F)){priority=Priority.First});
            }
            private void Patch(MethodInfo method,string name){Require(method!=null,"exact native palm CPU endpoint exists "+name);harmony.Patch(method,prefix:new HarmonyMethod(typeof(RenderHarness).GetMethod(name,F)));}
            internal void Render(Point[] sources)
            {
                Native.Clear();Flushed=0;var counts=(int[])Get(drawing,"_specialsCount");Array.Clear(counts,0,counts.Length);counts[0]=sources.Length;
                var positions=(Point[][])Get(drawing,"_specialPositions");Array.Copy(sources,positions[0],sources.Length);
                drawing.PostDrawTiles(false);
            }
            private static bool Texture(ref Texture2D __result){__result=active.texture;return false;}
            private static bool NoDisplay(){return false;}
            private static bool Light(ref Color __result){__result=active.Dark?Color.Black:Color.White;return false;}
            private static void Queued(Vector2 position,Rectangle sourceRectangle,float rotation,Vector2 origin,float scale,bool __runOriginal)
            {if(__runOriginal)active.Native.Add(new NativeDraw{Position=position,Source=sourceRectangle,Rotation=rotation,Origin=origin,Scale=scale,Camera=Main.Camera.UnscaledPosition});}
            private static bool Flush(NextNatureRenderer __instance)
            {var entries=(IList)typeof(NextNatureRenderer).GetField("_entries",F).GetValue(__instance);active.Flushed+=entries.Count;entries.Clear();if(active.ThrowAtFlush)throw new InvalidOperationException("controlled-palm-final-display-failure");return false;}
            public void Dispose(){harmony.UnpatchAll(harmony.Id);Main.spriteBatch=oldBatch;Main.SettingsEnabled_TilesSwayInWind=oldSway;Main.windSpeedCurrent=oldWind;Main.worldSurface=oldSurface;active=null;}
        }
        internal static void Names(Assembly assembly)
        {
            string culture=Language.ActiveCulture.Name;int cases=0;
            try
            {
                foreach(string locale in new[]{"en-US","zh-Hans"})
                {
                    LanguageManager.Instance.SetLanguage(locale);Lang.InitializeLegacyLocalization();Terraria.Map.MapHelper.Initialize();
                    string expected=Language.GetTextValue("MapObject.PalmTree");
                    using(var world=new NativeNaturalNameChecks.World(assembly))
                    {
                        object value=world.At(323,22,2);
                        Console.WriteLine("PALM-NAME-ORACLE locale="+locale+" frame=22,2 expected="+expected+" actual="+NativeNaturalNameChecks.Text(value));
                        Require(NativeNaturalNameChecks.Text(value)==expected && (int)Get(value,"ItemType")==0,"native legal signed palm displacement names a palm, not unknown or a drop item");
                    }
                    MethodInfo resolve=assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.NativeNaturalNames",true).GetMethod("Resolve",F);
                    // GrowPalmTree writes X22 styles0..6 and signed even Y;
                    // CheckPalmTree adds cut styles7..10; conversion writes Y0.
                    for(int x=0;x<=220;x+=22)for(int y=-16;y<=16;y+=2)
                    {
                        var tile=new Tile{type=323,frameX=(short)x,frameY=(short)y};tile.active(true);
                        object name=resolve.Invoke(null,new object[]{tile});
                        Require((bool)Get(name,"Handled") && (int)Get(name,"ItemType")==0 && (string)GetOptional(name,"Description")==expected,"native palm root/trunk/crown/cut/conversion frame domain");cases++;
                    }
                    foreach(var frame in new[]{new[]{-22,0},new[]{242,0},new[]{1,0},new[]{0,-17},new[]{0,17},new[]{0,1},new[]{0,22}})
                    {
                        var tile=new Tile{type=323,frameX=(short)frame[0],frameY=(short)frame[1]};tile.active(true);
                        object name=resolve.Invoke(null,new object[]{tile});Require((bool)Get(name,"Handled") && (int)Get(name,"ItemType")==0 && GetOptional(name,"Description")==null,"invalid palm axes do not borrow the generic grid or a seed identity");
                    }
                    var random=Main.rand;Main.rand=new UnifiedRandom(806);var oracle=new UnifiedRandom(806);
                    try{for(int i=0;i<40;i++){var t=new Tile{type=323,frameX=22,frameY=-16};t.active(true);resolve.Invoke(null,new object[]{t});}Require(Main.rand.Next()==oracle.Next(),"palm naming consumes no shared RNG");}finally{Main.rand=random;}
                }
                Console.WriteLine("PASS palm names L1/L2: "+cases+" signed-axis native frame/localization cases, actual World legal positive frame, malformed negatives, no item ID or RNG. Tree rendering/geometry is a separate responsibility.");
            }
            finally{LanguageManager.Instance.SetLanguage(culture);Lang.InitializeLegacyLocalization();Terraria.Map.MapHelper.Initialize();}
        }
    }
}
