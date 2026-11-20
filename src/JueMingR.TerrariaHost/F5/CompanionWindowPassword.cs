using System;
using JueMingR.Features.Text;
using JueMingR.TerrariaHost.Input;
using JueMingR.TerrariaHost.Notes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class CompanionWindow
    {
        private long passwordToken;
        private int passwordWaiting, passwordWaitingLabel;
        private bool openedForPassword;
        private string passwordError;
        internal bool PasswordPromptVisible => Visible && passwordToken != 0;

        private sealed class PasswordClipboard : INotesClipboard
        {
            private readonly NotesClipboard clipboard = new NotesClipboard(() => Main.instance.Window.Handle);
            public bool TryCopy(string text) => false;
            public bool TryPaste(out string text) => clipboard.TryPaste(out text);
        }

        private void SyncPasswordPrompt()
        {
            if (!host.PasswordPromptState(out long token, out int waiting))
            {
                if (PasswordPromptVisible) RetirePasswordPrompt();
                return;
            }
            passwordWaiting = waiting;
            if (passwordToken == token) return;
            if (!Visible)
            {
                ToggleCore(true);
                openedForPassword = true;
            }
            passwordToken = token;
            passwordError = null;
            ready = false;
            notice = null;
            CancelDrag();
            host.CancelItemGesture();
            host.EndItemRepeat();
            CancelSelectionModes();
            RetireConfigurationInput();
            Invalidate();
            // A challenge projects the new controls under an existing physical
            // press. That press cannot become an admission on this new dialog.
            previousLeft = PlayerInput.MouseInfo.LeftButton == ButtonState.Pressed;
            previousRight = PlayerInput.MouseInfo.RightButton == ButtonState.Pressed;
            editor = new TextEditBuffer("", true, 256, 1024, "密码过长。");
            textInput.PrepareEditor();
        }

        private string PasswordWaitingText() => host.PasswordPromptRejected() ? "密码有误，请重新输入。" :
            passwordWaiting == 1 ? "一名队友正在等待加入。" : passwordWaiting + " 名队友正在等待加入。";

        private void PreparePassword(Matrix transform, Vector2 physical)
        {
            Vector2 logical = physical / transform.M11;
            float width = Math.Min(384, logical.X - 24);
            float row = Math.Max(34, measure("输入服务器密码", .7f).Height + 14);
            float height = 4 * row + 32;
            if (width < 192 || logical.Y < height + 24) { ready = false; return; }
            var rect = new F5Rect((logical.X - width) / 2, (logical.Y - height) / 2, width, height);
            if (!ready || !panel.Equals(rect) || transform != matrix || renderer.Generation != skin)
            {
                Invalidate();
                controls.Clear();
                labels.Clear();
                panel = rect;
                matrix = transform;
                screen = physical;
                skin = renderer.Generation;
                float x = rect.X + 16, usable = width - 32, top = rect.Y + 10;
                Text("输入服务器密码", new F5Rect(x, top, usable, row), .85f);
                passwordWaitingLabel = labels.Count;
                Text(PasswordWaitingText(), new F5Rect(x, top + row, usable, row), .65f);
                field = new F5Rect(x, top + 2 * row, usable, row);
                float confirmWidth = ConfigurationWidth("确认"), cancelWidth = ConfigurationWidth("取消");
                Button("password-confirm:" + passwordToken, "确认", new F5Rect(rect.Right - 16 - confirmWidth, top + 3 * row + 8, confirmWidth, row),
                    () => RequestFinish());
                Button("password-cancel:" + passwordToken, "取消", new F5Rect(rect.Right - 22 - confirmWidth - cancelWidth, top + 3 * row + 8, cancelWidth, row),
                    CancelPasswordPrompt);
                ready = true;
                LayoutBuilds++;
            }
            UpdateLabel(passwordWaitingLabel, passwordError ?? textInput.Error ?? editor?.Error ?? PasswordWaitingText());
            PrepareEditPresentation();
        }

        private void ProcessPasswordInput(Vector2 point, bool left, bool right, int wheel)
        {
            OwnsPointer = Captured || Contains(point.X, point.Y);
            if (OwnsPointer)
            {
                if (left) leftTail = true;
                if (right) rightTail = true;
                ConsumeWheel = wheel != 0;
            }
            Control hit = ready ? controls.Find(control => control.Rect.Contains(point.X, point.Y)) : null;
            if (left && !previousLeft) { armedKey = hit?.Key; armedGeneration = generation; }
            if (!left && previousLeft && armedKey != null && armedGeneration == generation && hit?.Key == armedKey)
            {
                armedKey = null;
                hit.Execute();
            }
            ReleaseTails(left, right, true);
            previousLeft = left;
            previousRight = right;
        }

        private void DrawPassword()
        {
            renderer.Pass(matrix, panel, () =>
            {
                UiSurface.Panel(Main.spriteBatch, TextureAssets.MagicPixel.Value, panel, TextureAssets.InventoryBack.Value, new Color(165, 180, 205), roundedOuter: true);
                foreach (var label in labels) renderer.Text(label.Text, label.Rect, Color.White, label.TextScale);
                renderer.ItemButton(field, true, field.Contains(pointer.X, pointer.Y));
                foreach (var control in controls)
                    renderer.Button(control.Element, false, control.Enabled, false, control.Rect.Contains(pointer.X, pointer.Y), armedKey == control.Key);
                DrawEditor();
            });
        }

        private void RetirePasswordPrompt()
        {
            bool close = openedForPassword;
            ResetPasswordPresentation();
            CancelEdit();
            if (close) Close();
            else { ready = false; Invalidate(); }
        }
        private void RetireInvalidPasswordOwner()
        {
            // The native file identity can change before the runtime update.
            // Neither drawing nor sampling may retain its old password draft.
            if (PasswordPromptVisible && !host.ArchiveIdentityCurrent()) RetirePasswordPrompt();
        }

        private void ResetPasswordPresentation()
        { passwordToken = 0; passwordError = null; openedForPassword = false; }

        private void CancelPasswordPrompt()
        {
            host.CancelPassword(passwordToken);
            RetirePasswordPrompt();
        }

        void ITextEditSession.CancelEdit()
        {
            if (PasswordPromptVisible)
            {
                input.ConsumeCancellation((int)Keys.Escape);
                CancelPasswordPrompt();
            }
            else CancelEdit();
        }
    }
}
