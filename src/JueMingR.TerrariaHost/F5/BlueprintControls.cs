using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed class BlueprintControls
    {
        internal const string CaptureAction = "blueprint.capture", SaveAction = "blueprint.save", ToolbarAction = "blueprint.toolbar", LibraryAction = "blueprint.library";
        internal const string MoveAction = "blueprint.move", RemoveAction = "blueprint.remove";
        internal const string MirrorAction = "blueprint.mirror", TrimAction = "blueprint.trim", ConfirmTrimAction = "blueprint.trim.confirm";
        internal const string AutoBuildAction = "blueprint.autobuild", MaterialsAction = "blueprint.materials";
        internal const string StartHint = "点击开始键执行选区，反选取消选区。";
        internal const string ExitHint = "退出选区，并清空已有选区";
        internal const string SaveHint = "点击保存键保存世界上已有的蓝图选区。";
        internal const string MoveHint = "先选择需要移动的世界蓝图，再确认新位置。";
        internal const string RemoveHint = "选择并移除一份世界蓝图，保留蓝图库中的模板。";
        internal const string MirrorHint = "选择一份未建造的世界蓝图，在原框内水平镜像。";
        internal const string TrimHint = "选择一份世界蓝图，框选要删除的部分，再确定修剪。";
        internal const string ConfirmTrimHint = "永久删除红色预览中的图纸内容，保留真实建筑。";
        internal const string AutoBuildHint = "使用随身材料，在原版可放置的距离内自动建造已固定的蓝图。";
        internal const string MaterialsHint = "核对已放置蓝图还需的材料、随身持有量和缺少数量。";
        private readonly Blueprint.HostBlueprint owner;
        internal BlueprintControls(Blueprint.HostBlueprint owner) { this.owner = owner; }
        internal bool Active { get { return owner.State.Active; } }
        internal int OperationMode { get { return owner.OwnMirrorCancellation ? 4 : owner.OwnTrimCancellation ? owner.CanInvokeConfirmTrim ? 6 : 5 : owner.OwnMoveCancellation ? 1 : owner.OwnRemoveCancellation ? 2 : owner.OwnPlaceCancellation ? 3 : 0; } }
        internal long PreviewRevision { get { return owner.TrimPreviewRevision; } }
        internal bool LibraryVisible { get { return owner.Shell?.BlueprintUi?.Visible == true; } }
        internal static void AddRows(List<F5Element> elements, Func<string, float, F5Size> measure, ref float y, bool active, int operation = 0)
        {
            var rows = new F5RowLayout(elements, measure);
            rows.Row(ref y, 0, 522, active ? "退出采集" : "采集蓝图", new[] { active ? "退出" : "开始", "键" },
                text => text == "键" ? F5Command.None : F5Command.BlueprintCapture,
                new F5RowDescription(CaptureAction, active ? ExitHint : StartHint));
            Key(elements, CaptureAction);
            rows.Row(ref y, 0, 522, "保存采集", new[] { "保存", "键" }, text => text == "键" ? F5Command.None : F5Command.BlueprintSave,
                new F5RowDescription(SaveAction, SaveHint));
            Key(elements, SaveAction);
            rows.Row(ref y, 0, 522, "蓝图库", new[] { "打开", "键" }, text => text == "键" ? F5Command.None : F5Command.BlueprintLibrary);
            Key(elements, LibraryAction);
            rows.Row(ref y, 0, 522, operation == 1 ? "取消移动" : "移动蓝图", new[] { operation == 1 ? "取消" : "移动", "键" }, text => text == "键" ? F5Command.None : F5Command.BlueprintMove,
                new F5RowDescription(MoveAction, MoveHint));
            Key(elements, MoveAction);
            rows.Row(ref y, 0, 522, operation == 2 ? "取消移除" : "移除蓝图", new[] { operation == 2 ? "取消" : "移除", "键" }, text => text == "键" ? F5Command.None : F5Command.BlueprintRemove,
                new F5RowDescription(RemoveAction, RemoveHint));
            Key(elements, RemoveAction);
            rows.Row(ref y, 0, 522, operation == 4 ? "取消镜像" : "水平镜像", new[] { operation == 4 ? "取消" : "镜像", "键" }, text => text == "键" ? F5Command.None : F5Command.BlueprintMirror,
                new F5RowDescription(MirrorAction, MirrorHint));
            Key(elements, MirrorAction);
            bool trimming = operation == 5 || operation == 6;
            rows.Row(ref y, 0, 522, trimming ? "取消修剪" : "局部修剪", new[] { trimming ? "取消" : "修剪", "键" }, text => text == "键" ? F5Command.None : F5Command.BlueprintTrim,
                new F5RowDescription(TrimAction, TrimHint));
            Key(elements, TrimAction);
            if (trimming)
            {
                rows.Row(ref y, 0, 522, "确定修剪", new[] { "确定", "键" }, text => text == "键" ? F5Command.None : F5Command.BlueprintConfirmTrim,
                    new F5RowDescription(ConfirmTrimAction, ConfirmTrimHint));
                Key(elements, ConfirmTrimAction);
            }
            rows.Row(ref y, 0, 522, "手持凝胶显示快捷栏", new[] { "开启", "关闭", "键" },
                text => text == "键" ? F5Command.None : text == "开启" ? F5Command.BlueprintToolbarOn : F5Command.BlueprintToolbarOff);
            Key(elements, ToolbarAction);
            rows.Row(ref y, 0, 522, "自动建造", new[] { "开启", "关闭", "键" },
                text => text == "键" ? F5Command.None : text == "开启" ? F5Command.BlueprintAutoBuildOn : F5Command.BlueprintAutoBuildOff,
                new F5RowDescription(AutoBuildAction, AutoBuildHint));
            Key(elements, AutoBuildAction);
            rows.Row(ref y, 0, 522, "施工材料", new[] { "打开", "键" }, text => text == "键" ? F5Command.None : F5Command.BlueprintMaterials,
                new F5RowDescription(MaterialsAction, MaterialsHint));
            Key(elements, MaterialsAction);
        }
        private static void Key(List<F5Element> elements, string id)
        {
            var key = elements[elements.Count - 1];
            elements[elements.Count - 1] = new F5Element(key.Kind, key.Rect, key.Text, key.TextSize, key.TextScale, F5Command.None, id);
        }
        internal static bool Owns(F5Command command) { return command >= F5Command.BlueprintCapture && command <= F5Command.BlueprintMaterials; }
        // UI display is phase independent; Host action entries own admission.
        internal bool Available(F5Command command) { return owner.ControlsEnabled && (command != F5Command.BlueprintSave || owner.CanSave) &&
            (command != F5Command.BlueprintLibrary || owner.CanOpenLibrary) &&
            (command != F5Command.BlueprintMove || owner.CanInvokeMove) && (command != F5Command.BlueprintRemove || owner.CanInvokeRemove) &&
            (command != F5Command.BlueprintMirror || owner.CanInvokeMirror) && (command != F5Command.BlueprintTrim || owner.CanInvokeTrim) &&
            (command != F5Command.BlueprintConfirmTrim || owner.CanInvokeConfirmTrim) &&
            (command != F5Command.BlueprintMaterials || owner.CanOpenMaterials) &&
            (command != F5Command.BlueprintCapture || owner.State.Active || !owner.ProjectionEditing); }
        internal string Hint(F5Command command)
        {
            if (command == F5Command.BlueprintSave) return owner.CanSave ? null : owner.SaveDisabledReason;
            if (command == F5Command.BlueprintLibrary) return owner.ManualLibraryDisabledReason;
            if (command == F5Command.BlueprintMaterials) return owner.MaterialsDisabledReason;
            if (command == F5Command.BlueprintMove) return owner.CanInvokeMove ? owner.OwnMoveCancellation ? "取消移动，保留原蓝图" : "点击需要移动的蓝图" : owner.MoveDisabledReason;
            if (command == F5Command.BlueprintRemove) return owner.CanInvokeRemove ? owner.OwnRemoveCancellation ? "退出移除选择" : "点击需要移除的蓝图" : owner.RemoveDisabledReason;
            if (command == F5Command.BlueprintMirror) return owner.CanInvokeMirror ? owner.OwnMirrorCancellation ? "取消镜像，保留原蓝图" : "点击需要镜像的蓝图" : owner.MirrorDisabledReason;
            if (command == F5Command.BlueprintTrim) return owner.CanInvokeTrim ? owner.OwnTrimCancellation ? "取消修剪，保留图纸内容" : "点击需要修剪的蓝图" : owner.TrimDisabledReason;
            if (command == F5Command.BlueprintConfirmTrim) return owner.CanInvokeConfirmTrim ? null : owner.ConfirmTrimDisabledReason;
            if (command == F5Command.BlueprintCapture && owner.ProjectionEditing) return "请先完成或取消当前摆放";
            return null;
        }
        internal Color? Selected(F5Command command)
        { return command == F5Command.BlueprintToolbarOn && owner.ToolbarEnabled || command == F5Command.BlueprintAutoBuildOn && owner.AutoBuildEnabled ? (Color?)Color.LightGreen :
                command == F5Command.BlueprintToolbarOff && !owner.ToolbarEnabled || command == F5Command.BlueprintAutoBuildOff && !owner.AutoBuildEnabled ? (Color?)Color.IndianRed : null; }
        internal void Execute(F5Command command)
        {
            if (!Owns(command) || !Available(command)) return;
            if (command == F5Command.BlueprintCapture) owner.Toggle();
            else if (command == F5Command.BlueprintSave) owner.Save();
            else if (command == F5Command.BlueprintLibrary) owner.OpenLibrary();
            else if (command == F5Command.BlueprintMove) owner.MoveProjection();
            else if (command == F5Command.BlueprintRemove) owner.RemoveProjection();
            else if (command == F5Command.BlueprintMirror) owner.MirrorProjection();
            else if (command == F5Command.BlueprintTrim) owner.TrimProjection();
            else if (command == F5Command.BlueprintConfirmTrim) owner.ConfirmTrimProjection();
            else if (command == F5Command.BlueprintAutoBuildOn || command == F5Command.BlueprintAutoBuildOff) owner.SetAutoBuild(command == F5Command.BlueprintAutoBuildOn);
            else if (command == F5Command.BlueprintMaterials) owner.OpenMaterials();
            else if (command == F5Command.BlueprintToolbarOn || command == F5Command.BlueprintToolbarOff) owner.SetToolbar(command == F5Command.BlueprintToolbarOn);
        }
    }
}
