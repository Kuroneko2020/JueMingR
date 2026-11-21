using System;
using Terraria;
using Terraria.GameInput;
using Microsoft.Xna.Framework.Input;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class CompanionWindow
    {
        private void StartCreation()
        {
            if (!host.CanCreate) return;
            host.CancelItemGesture(); CancelEdit();
            // Close only this surface; the independent native session retains
            // the manager's selection, position and both scroll offsets.
            Close();
            CompanionCreationSession.Open(host,input, ResumeAfterCreation);
        }
        private void ResumeAfterCreation(string createdName)
        {
            if (createdName != null) { selected=host.LastCreatedId;pendingCreatedId=selected;pendingCreatedName=createdName;pendingCreationToken=host.CreationSessionToken;page=0;bodyOffset=0; }
            ToggleCore(true);
            // Native SetState retires the entering click. Retain physical tails
            // until the existing shared trusted sample reports actual release.
            leftTail |= PlayerInput.MouseInfo.LeftButton==ButtonState.Pressed;
            rightTail |= PlayerInput.MouseInfo.RightButton==ButtonState.Pressed;
            input.ConsumeHotkeyActions();
        }
    }
}
