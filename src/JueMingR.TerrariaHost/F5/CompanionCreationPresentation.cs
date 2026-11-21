using System;
using Terraria;
using Terraria.GameInput;
using Microsoft.Xna.Framework.Input;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class CompanionWindow
    {
        internal F5Shell CreationShell { get; set; }

        private void StartCreation()
        {
            if (!host.CanCreate || CreationShell?.OwnsCompanionCreation != true || Main.inFancyUI) return;
            host.CancelItemGesture(); CancelEdit();
            // Close only this surface; the independent native session retains
            // the manager's selection, position and both scroll offsets.
            Close();
            CompanionCreationSession.Open(CreationShell,host,input,ResumeAfterCreation);
        }
        private void ResumeAfterCreation(string createdName,bool failed)
        {
            if (CreationShell?.OwnsCompanionCreation != true) return;
            if (createdName != null) { selected=host.LastCreatedId;pendingCreatedId=selected;pendingCreatedName=createdName;pendingCreationToken=host.CreationSessionToken;page=0;bodyOffset=0; }
            ToggleCore(true);
            if (failed) ShowNotice(createdName == null ? "创建界面出现问题，已返回队友管理。" : "队友创建已受理，创建界面已关闭。");
            // Native SetState retires the entering click. Retain physical tails
            // until the existing shared trusted sample reports actual release.
            leftTail |= PlayerInput.MouseInfo.LeftButton==ButtonState.Pressed;
            rightTail |= PlayerInput.MouseInfo.RightButton==ButtonState.Pressed;
            input.ConsumeHotkeyActions();
        }
    }
}
