using System;
using System.Collections;
using System.Linq;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // The owner supplied this order independently of module registration order.
    internal static class NativePageCompositionChecks
    {
        internal static void Run(object context)
        {
            var shell=Get(context,"Shell");var state=Get(shell,"State");var renderer=Get(shell,"renderer");var page=Get(shell,"items");
            NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return (bool)Call(Get(context,"Tools"),"Controls",0) && (bool)Call(Get(context,"Processing"),"Controls",2);});
            Call(state,"Navigate",0);Call(state,"RestoreVisible");Call(renderer,"RefreshResources");
            Call(renderer,"Prepare",state,1200f,1200f,1f);Call(page,"PrepareLayout",Microsoft.Xna.Framework.Matrix.Identity,new Microsoft.Xna.Framework.Vector2(1200,1200));ItemsOrder(page);
            Call(state,"Navigate",1);Call(renderer,"Prepare",state,1200f,1200f,1f);
            Action prepare=()=>Call(shell,"PrepareMisc",true,Microsoft.Xna.Framework.Matrix.Identity,new Microsoft.Xna.Framework.Vector2(1200,1200),false);
            prepare();MiscOrder(shell);prepare();
            var misc=Get(shell,"MiscUi");int builds=(int)Get(misc,"LayoutBuildCount");
            for(int i=0;i<30;i++)prepare();Require(builds==(int)Get(misc,"LayoutBuildCount"),"stable complete Misc composition does not rebuild migrated blocks");
            Input(context, prepare);
            Call(shell,"CloseAndSubmitPosition");Console.WriteLine("PASS complete Items/Misc composition order, unchanged identities, final footer and stable layout work.");
        }
        private static void Input(object context, Action prepare)
        {
            var shell=Get(context,"Shell");var misc=Get(shell,"MiscUi");var host=Get(context,"Tools");
            var setting=((JueMingR.Features.Tools.ToolSettings[])Get(host,"Settings"))[0];
            var controls=NativeToolsUiChecks.Controls(misc);
            Require(controls.Where(c=>Get(c,"Command").ToString()=="Hotkey").Select(c=>(string)Get(Get(c,"Element"),"HotkeyTarget"))
                .OrderBy(x=>x).SequenceEqual(new[]{"tools.capture","tools.herbs","items.coin-deposit.toggle"}.OrderBy(x=>x)),"migrated bindings retain exact original identities");
            var on=controls.Single(c=>Get(c,"Command").ToString()=="Tools" && (int)Get(c,"Argument")==1);
            var rect=Get(on,"Rect");var point=new Microsoft.Xna.Framework.Vector2((float)Get(rect,"X")+2,(float)Get(rect,"Y")+2);
            Action<bool,bool,bool> input=(left,current,focused)=>{NativeToolsUiChecks.Mouse(point,left);Call(misc,"ProcessInput",true,new Microsoft.Xna.Framework.Input.KeyboardState(),point,current,focused,false);};
            long command=setting.AcceptedCommandId;
            input(false,true,true);input(true,true,true);input(false,false,true);
            Require(setting.AcceptedCommandId==command,"stale page geometry cannot execute an armed command");
            prepare();input(false,true,true);input(true,true,true);input(false,true,false);
            Require(setting.AcceptedCommandId==command,"focus loss cancels the page command");
            prepare();input(false,true,true);input(true,true,true);input(false,true,true);
            NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !setting.Busy;});
            Require(setting.AcceptedCommandId==command+1 && setting.Value.Mode==1,"fresh actual page click dispatches exactly one capture command");
            prepare();var config=NativeToolsUiChecks.Controls(misc).Single(c=>Get(c,"Command").ToString()=="Tools" && (int)Get(c,"Argument")==3);
            rect=Get(config,"Rect");point=new Microsoft.Xna.Framework.Vector2((float)Get(rect,"X")+2,(float)Get(rect,"Y")+2);
            input(false,true,true);input(true,true,true);input(false,true,true);
            Require((bool)Get(Get(shell,"CaptureUi"),"Visible"),"actual page configuration dispatch reaches its owning popup");
            Call(Get(shell,"CaptureUi"),"Close");
        }
        internal static void MiscOrder(object shell)
        {
            var labels=((IEnumerable)Get(Get(shell,"MiscUi"),"elements")).Cast<object>()
                .Concat(((IEnumerable)Get(Get(shell,"MiningUi"),"elements")).Cast<object>())
                .Concat(((IEnumerable)Get(Get(shell,"RecoveryUi"),"visible")).Cast<object>().Select(p=>Get(p,"Element")))
                .Concat(((IEnumerable)Get(Get(shell,"ReforgeUi"),"Parts")).Cast<object>().Select(p=>Get(p,"Element")))
                .Where(e=>GetOptional(e,"Description")!=null).OrderBy(e=>(float)Get(Get(e,"Rect"),"Y"))
                .Select(e=>(string)Get(e,"Text")).ToArray();
            Require(labels.SequenceEqual(new[]{"自动重铸","自动挖矿","自动捕捉","自动收获","自动存钱","自动收税"}),
                "actual Misc feature order: "+string.Join(" > ",labels));
            var state=Get(shell,"State");var layout=Get(state,"Layout");
            var merchant=((IEnumerable)Get(layout,"Elements")).Cast<object>().Single(e=>(string)GetOptional(e,"Text")=="游商测试");
            Require((float)Get(Get(merchant,"Rect"),"Y")>=(float)Get(Get(shell,"RecoveryUi"),"ContentBottom"),"merchant follows the complete tax block");
        }
        internal static void ItemsOrder(object page)
        {
            var labels=((IEnumerable)Get(page,"elements")).Cast<object>()
                .Concat(((IEnumerable)Get(Get(page,"QuickPanel"),"visible")).Cast<object>().Select(p=>Get(p,"Element")))
                .Where(e=>GetOptional(e,"Description")!=null)
                .OrderBy(e=>(float)Get(Get(e,"Rect"),"Y"))
                .Select(e=>(string)Get(e,"Text")).ToArray();
            Require(labels.SequenceEqual(new[]{"快捷物品","自动堆叠","自动出售","自动丢弃","持续开袋","自动提炼","保持收藏"}),
                "actual Items feature order and no migrated rows: "+string.Join(" > ",labels));
        }
    }
}
