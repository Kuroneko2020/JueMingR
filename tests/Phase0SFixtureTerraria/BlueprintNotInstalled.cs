using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.F5
{
    // This old source-linked receiver never installs Blueprint. Preserve only
    // the real renderer's static identifiers/classification; invoking blueprint
    // presentation is a fixture error, never evidence of a supported feature.
    internal sealed class BlueprintControls
    {
        internal const string SaveAction="blueprint.save",LibraryAction="blueprint.library",PlacedListAction="blueprint.placed-list";
        internal const string MoveAction="blueprint.move",RemoveAction="blueprint.remove",MirrorAction="blueprint.mirror";
        internal const string TrimAction="blueprint.trim",ConfirmTrimAction="blueprint.trim.confirm";
        internal const string StartHint="点击开始键执行选区，反选取消选区。",ExitHint="退出选区，并清空已有选区",SaveHint="点击保存键保存世界上已有的蓝图选区。";
        private static Exception Missing(){return new InvalidOperationException("Use the real native Blueprint fixture.");}
        internal BlueprintControls(){throw Missing();}
        internal bool Active {get{throw Missing();}}
        internal int OperationMode {get{throw Missing();}}
        internal long PreviewRevision {get{throw Missing();}}
        internal bool LibraryVisible {get{throw Missing();}}
        internal static bool Owns(F5Command command){return command>=F5Command.BlueprintCapture && command<=F5Command.BlueprintPlacedList;}
        internal static void AddRows(List<F5Element> elements,Func<string,float,F5Size> measure,ref float y,bool active,int operation=0){throw Missing();}
        internal bool Available(F5Command command){throw Missing();}
        internal Color? Selected(F5Command command){throw Missing();}
        internal string Hint(F5Command command){throw Missing();}
    }
    internal sealed class BlueprintReplacementPopup
    {
        private static Exception Missing(){return new InvalidOperationException("Use the real native Blueprint fixture.");}
        internal BlueprintReplacementPopup(){throw Missing();}
        internal bool Visible {get{throw Missing();}}
        internal bool CanHint {get{throw Missing();}}
        internal string Hint(float x,float y,out F5Rect rect){throw Missing();}
    }
}
