using System;
using System.Collections;
using System.Linq;
using JueMingR.Features.Recovery;
using Microsoft.Xna.Framework;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeRecoveryUiChecks
    {
        internal static void Run(object context)
        {
            var shell=Get(context,"Shell");var state=Get(shell,"State");var renderer=Get(shell,"renderer");var ui=Get(shell,"RecoveryUi");var layout=Get(state,"Layout");
            Call(state,"Navigate",10);Call(state,"RestoreVisible");Call(renderer,"RefreshResources");
            Action prepare=()=>{Call(renderer,"Prepare",state,960f,760f,1f);Call(ui,"PrepareLayout",Matrix.Identity);};prepare();
            var rows=((IEnumerable)Get(ui,"logical")).Cast<object>().Where(p=>GetOptional(Get(p,"Element"),"Description")!=null).Select(p=>(string)Get(Get(p,"Element"),"Text")).ToArray();
            Require(string.Join("|",rows)=="自动回血|自动回蓝|自动护士|家具增益|自动增益","actual F5 recovery order");
            float height=(float)Get(layout,"ContentHeight");var settings=(RecoverySettings)Get(Get(context,"Recovery"),"Potions");
            Require(settings.Set(new RecoveryOptions(2)),"UI save starts");prepare();Require((float)Get(layout,"ContentHeight")==height,"pending settings no reserved row");
            NativeQuickItemChecks.Until(()=>{settings.Poll();return !settings.Busy;});prepare();Require((float)Get(layout,"ContentHeight")==height,"saved settings no flash height");
            var button=((IEnumerable)Get(ui,"visible")).Cast<object>().First(p=>(int)Get(p,"Command")==-2 && (int)Get(p,"Value")==0);
            Call(ui,"Execute",button);prepare();
            Require(((IEnumerable)Get(ui,"logical")).Cast<object>().Count(p=>(int)Get(p,"Type")>0)>10,"real full-definition medication icons reachable");
            Call(state,"ScrollTo",60f);prepare();float scroll=(float)Get(state,"Scroll");prepare();Require((float)Get(state,"Scroll")==scroll,"dynamic page preparation preserves scroll");
            Call(state,"Navigate",1);prepare();Require(((IEnumerable)Get(ui,"logical")).Cast<object>().Any(p=>(string)GetOptional(Get(p,"Element"),"Text")=="自动收税"),"tax remains on misc page");
            Call(state,"Close");Call(ui,"Suspend");NativeRecoveryChecks.Save(settings,new RecoveryOptions());
            Console.WriteLine("PASS G07 UI: real composition controls, full catalogue, stable off/pending/saved heights and scroll.");
        }
    }
}
