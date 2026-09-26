using System;
using System.Collections;
using System.Linq;
using JueMingR.Features.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeToolsUiChecks
    {
        internal static void Run(object context,ProbeGraphics graphics)
        {
            object host=Get(context,"Tools"),shell=Get(context,"Shell"),state=Get(shell,"State"),items=Get(shell,"items"),popup=Get(shell,"CaptureUi"),renderer=Get(shell,"renderer");
            var settings=((ToolSettings[])Get(host,"Settings"))[0];Prepare(context,0,960,760,1);
            var panel=Get(items,"ToolsPanel");var rows=(IEnumerable[])Get(panel,"rows");var first=rows[0].Cast<object>().First();
            Call(state,"ScrollTo",Get(Get(first,"Rect"),"Y"));Prepare(context,0,960,760,1);
            var config=Controls(items).First(c=>Get(c,"Command").ToString()=="Tools" && (int)Get(c,"Argument")==3);var point=Point(Get(config,"Rect"));
            foreach(bool left in new[]{false,true,false}){Mouse(point,left);Call(items,"ProcessInput",true,new KeyboardState(),point,true,true,false);}
            Require((bool)Get(popup,"Visible"),"actual Items configuration button opens capture window");Call(popup,"Prepare",Matrix.Identity,new Vector2(960,760),true);
            var cells=(Array)Get(popup,"cells");int saved=settings.Value.Categories;
            for(int i=0;i<8;i++)
            {
                point=Point(cells.GetValue(i));long before=settings.AcceptedCommandId;
                foreach(bool left in new[]{false,true,false}){Mouse(point,left);Call(popup,"Process",true,new KeyboardState(),point,Matrix.Identity,new Vector2(960,760),true);}
                NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !settings.Busy;});
                Require(settings.AcceptedCommandId==before+1 && settings.Value.Categories==(saved^((1<<(i+1))-1)),"each real category button commits exactly once: "+i);
                Call(popup,"Prepare",Matrix.Identity,new Vector2(960,760),true);
            }
            int layouts=(int)Get(popup,"LayoutBuilds");for(int i=0;i<240;i++)Call(popup,"Prepare",Matrix.Identity,new Vector2(960,760),true);
            Require((int)Get(popup,"LayoutBuilds")==layouts,"stable popup does no repeated text/layout construction");
            point=Point(cells.GetValue(0));Mouse(point,false);Call(popup,"Process",true,new KeyboardState(),point,Matrix.Identity,new Vector2(960,760),true);
            Set(state,"PointerX",point.X);Set(state,"PointerY",point.Y);Set(state,"PointerBlocked",true);
            object[] hintArgs={state,items,false,false,null,null};string hint=(string)renderer.GetType().GetMethod("ResolveHint",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(renderer,hintArgs);
            Require(hint!=null && hint.Contains("鱼饵"),"popup hint remains reachable while the underlying page pointer is blocked");
            long command=settings.AcceptedCommandId;Mouse(point,true);Call(popup,"Process",true,new KeyboardState(),point,Matrix.Identity,new Vector2(960,760),true);
            graphics.SetMouseFont(graphics.Font); // New asset wrapper only must not fake a different font identity.
            Call(popup,"Close");Mouse(point,false);Call(popup,"Process",true,new KeyboardState(),point,Matrix.Identity,new Vector2(960,760),true);
            Require(command==settings.AcceptedCommandId && (bool)Get(popup,"ConsumeLeft"),"close cancels pending category and consumes the release tail");
            Require(settings.Set(settings.Value.WithCategories(saved)),"restore isolated category fixture");NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !settings.Busy;});
            Prepare(context,1,960,760,1);var mining=Get(shell,"MiningUi");var all=Controls(mining);
            Require(all.Count(c=>Get(c,"Command").ToString()=="Hotkey")==2,"separate mining mode and select action binding controls");
            Require(all.Where(c=>Get(c,"Command").ToString()=="Hotkey").Select(c=>(string)Get(Get(c,"Element"),"HotkeyTarget")).OrderBy(x=>x).SequenceEqual(new[]{"tools.mining","tools.mining.select"}),"both keys route to exact shared registry IDs");
            var on=all.First(c=>Get(c,"Command").ToString()=="Tools" && (int)Get(c,"Argument")==21);point=Point(Get(on,"Rect"));var miningSetting=((ToolSettings[])Get(host,"Settings"))[2];command=miningSetting.AcceptedCommandId;
            Mouse(point,false);Call(mining,"ProcessInput",true,point,true,true,false);Mouse(point,true);Call(mining,"ProcessInput",true,point,true,true,false);Mouse(point,false);Call(mining,"ProcessInput",true,point,false,true,false);
            Require(miningSetting.AcceptedCommandId==command,"changed native viewport cancels armed mining action before layout catches up");
            Call(shell,"CloseAndSubmitPosition");Require(!(bool)Get(popup,"Visible") && !(bool)Get(mining,"ready"),"explicit shell exit releases both G09 views");
            Console.WriteLine("PASS G09 UI: real configuration/category persistence, modal hints, close tails, distinct shared mining bindings and stale-geometry rejection.");
        }
        internal static object[] Controls(object ui){return ((IEnumerable)Get(ui,"controls")).Cast<object>().ToArray();}
        internal static Vector2 Point(object rect){return new Vector2((float)Get(rect,"X")+5,(float)Get(rect,"Y")+5);}
        internal static void Mouse(Vector2 point,bool left){PlayerInput.MouseInfo=new MouseState((int)point.X,(int)point.Y,0,left?ButtonState.Pressed:ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);}
        internal static void Prepare(object context,int page,int width,int height,float scale)
        {
            Main.screenWidth=width;Main.screenHeight=height;PlayerInput.CacheOriginalScreenDimensions();Main.UIScale=scale;
            object shell=Get(context,"Shell"),state=Get(shell,"State"),renderer=Get(shell,"renderer");var matrix=Matrix.CreateScale(scale);
            if((int)Get(state,"Page")!=page)Call(state,"Navigate",page);Call(state,"RestoreVisible");Call(renderer,"RefreshResources");Call(renderer,"Prepare",state,(float)width,(float)height,scale);
            if(page==0)Call(Get(shell,"items"),"Prepare",true,matrix,new Vector2(width,height));
            else
            {
                var recovery=Get(shell,"RecoveryUi");var reforge=Get(shell,"ReforgeUi");var mining=Get(shell,"MiningUi");
                Call(recovery,"Prepare",true,matrix,new Vector2(width,height));float bottom=(float)Get(recovery,"ContentBottom");Call(reforge,"Prepare",true,matrix,bottom);Call(mining,"Prepare",true,matrix,Get(reforge,"Height"));
                Call(Get(state,"Layout"),"SetRecoveryContentHeight",Get(mining,"Height"));Call(state,"ClampScroll");Call(recovery,"PrepareLayout",matrix);Call(reforge,"PrepareLayout",matrix,bottom);Call(mining,"PrepareLayout",matrix,Get(reforge,"Height"));
            }
        }
    }
}
