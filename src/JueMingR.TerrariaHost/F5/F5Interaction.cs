using System;
using JueMingR.Platform.Settings;

namespace JueMingR.TerrariaHost.F5
{
    internal struct F5Input
    {
        internal float Width, Height, Scale, X, Y;
        internal bool Active, Focused, F5, Left, Right;
        internal int Wheel;
        internal bool PageWheelHandled;
        internal bool ModalPointerOwner;
    }

    internal sealed class F5Interaction
    {
        private bool previousF5, previousLeft, leftTail, rightTail, active;
        private int capture; // 1: title, 2: scrollbar; never a second hover-ownership flag.
        private float grabX, grabY, windowWidth, windowHeight;
        private float dragWidth, dragHeight, dragScale;
        private int dragStartX, dragStartY;
        // These are a display projection and one pending UI command. Settings
        // owns the preference; viewport clamping must never become a command.
        private WindowPosition positionProjection, positionToSave;
        private F5Element armed;
        private int armedGeneration;
        internal readonly F5Layout Layout = new F5Layout();
        internal bool Visible { get; private set; }
        internal bool Ready { get; set; }
        internal Func<int, bool> BeforeLeave { get; set; }
        internal int Page { get; private set; } = 9;
        internal float X { get; private set; }
        internal float Y { get; private set; }
        internal float Scroll { get; private set; }
        internal bool ConsumeLeft { get; private set; }
        internal bool ConsumeRight { get; private set; }
        internal bool ConsumeWheel { get; private set; }
        internal F5Command Command { get; private set; }
        internal float PointerX { get; private set; }
        internal float PointerY { get; private set; }
        internal bool DraggingScroll { get { return capture == 2; } }
        internal bool OwnsPointer
        {
            get { return Ready && Visible && active &&
                (capture != 0 || new F5Rect(X, Y, windowWidth, windowHeight).Contains(PointerX, PointerY)); }
        }

        internal void Update(F5Input input)
        {
            Command = F5Command.None;
            // If validation throws, the shell must still consume buttons owned
            // by an earlier sample while it closes the failed local UI.
            ConsumeLeft = leftTail; ConsumeRight = rightTail; ConsumeWheel = false;
            PointerX = input.X; PointerY = input.Y;
            active = input.Active && input.Focused;
            if (!active || !Ready)
            {
                Close();
                // A focus-loss release is synthesized by Terraria. Only a focused
                // physical release can retire a press that started in this window.
                ConsumeLeft = leftTail; ConsumeRight = rightTail;
                if (input.Focused) { if (!input.Left) leftTail = false; if (!input.Right) rightTail = false; }
                previousLeft = input.Focused ? input.Left : true;
                previousF5 = input.Focused ? input.F5 : true;
                return;
            }
            if (!Finite(input.Width) || !Finite(input.Height) || !Finite(input.X) || !Finite(input.Y))
                throw new InvalidOperationException("F5 input coordinates are unavailable.");
            F5Size size = F5Layout.WindowSize(input.Width, input.Height, input.Scale);
            windowWidth = size.Width; windowHeight = size.Height;
            if (capture == 1 && (dragWidth != input.Width || dragHeight != input.Height || dragScale != input.Scale))
            {
                // The grab offset belongs to the old coordinate domain. Finish
                // from its last valid sample before using the new viewport.
                FinishTitleDrag();
                capture = 0;
            }
            if (capture != 1)
            {
                X = positionProjection == null ? (input.Width / input.Scale - size.Width) / 2 : positionProjection.X;
                Y = positionProjection == null ? (input.Height / input.Scale - size.Height) / 2 : positionProjection.Y;
            }
            bool pressed = input.Left && !previousLeft;
            bool released = !input.Left && previousLeft;
            if (input.F5 && !previousF5)
            {
                if (Visible)
                {
                    if (OwnsPointer)
                    {
                        if (input.Left) leftTail = true;
                        if (input.Right) rightTail = true;
                        ConsumeWheel = true;
                    }
                    if (BeforeLeave == null || BeforeLeave(-1)) Close();
                }
                else Visible = true;
            }
            previousF5 = input.F5;
            bool layoutReady = Layout.Matches(input.Width, input.Height, input.Scale, Page);
            if (Visible && capture == 1)
            { X = input.X - grabX; Y = input.Y - grabY; }
            // Terraria's panel helper accepts integer logical origins. Quantize
            // here so the outer painted rectangle and pointer gate agree.
            X = (float)Math.Floor(Clamp(X, 12, input.Width / input.Scale - size.Width - 12));
            Y = (float)Math.Floor(Clamp(Y, 12, input.Height / input.Scale - size.Height - 12));
            if (OwnsPointer)
            {
                if (input.Left) leftTail = true;
                if (input.Right) rightTail = true;
                ConsumeWheel = true;
                if (layoutReady && !input.ModalPointerOwner)
                {
                    float localX = input.X - X, localY = input.Y - Y;
                    if (pressed)
                    {
                        armed = null;
                        if (Layout.Title.Contains(localX, localY))
                        {
                            capture = 1; grabX = localX; grabY = localY;
                            dragStartX = (int)X; dragStartY = (int)Y;
                            dragWidth = input.Width; dragHeight = input.Height; dragScale = input.Scale;
                        }
                        else if (Layout.ScrollTrack.Contains(localX, localY) && Layout.MaxScroll > 0)
                        {
                            capture = 2;
                            F5Rect thumb = Layout.ScrollThumb(Scroll);
                            grabY = thumb.Contains(localX, localY) ? localY - thumb.Y : thumb.Height / 2;
                        }
                        else
                        {
                            for (int i = 0; i < F5Layout.Pages.Length; i++)
                                if (Layout.Navigation(i).Contains(localX, localY))
                                {
                                    if (i != Page && (BeforeLeave == null || BeforeLeave(i))) Navigate(i);
                                    layoutReady = false; break;
                                }
                            if (layoutReady) { armed = HitButton(localX, localY); armedGeneration = Layout.Generation; }
                        }
                    }
                    if (capture == 2 && input.Left)
                    {
                        F5Rect thumb = Layout.ScrollThumb(Scroll);
                        float travel = Layout.ScrollTrack.Height - thumb.Height;
                        Scroll = travel <= 0 ? 0 : Clamp((localY - Layout.ScrollTrack.Y - grabY) / travel, 0, 1) * Layout.MaxScroll;
                    }
                    if (input.Wheel != 0 && !input.PageWheelHandled && capture == 0 && layoutReady)
                    { Scroll = Clamp(Scroll - input.Wheel / 120f * 40, 0, Layout.MaxScroll); armed = null; }
                    // A click must release on the same element in the same layout generation.
                    if (released && capture == 0 && layoutReady && armed != null &&
                        armedGeneration == Layout.Generation && ReferenceEquals(armed, HitButton(localX, localY)))
                        Command = armed.Command;
                }
            }
            // Consume the release sample before retiring its tail, including after window closure.
            ConsumeLeft = leftTail; ConsumeRight = rightTail;
            if (!input.Left) { FinishTitleDrag(); leftTail = false; capture = 0; armed = null; }
            if (!input.Right) rightTail = false;
            previousLeft = input.Left;
        }

        internal void ClampScroll() { Scroll = Clamp(Scroll, 0, Layout.MaxScroll); }
        internal void ScrollTo(float value) { Scroll = Clamp(value, 0, Layout.MaxScroll); }
        internal void Navigate(int page) { if (page >= 0 && page < F5Layout.Pages.Length) { Page = page; Scroll = 0; } }

        internal F5Element HitButton(float localX, float localY)
        {
            // Clip in window coordinates first, then invert the renderer's page scroll offset.
            if (!Layout.Viewport.Contains(localX, localY)) return null;
            float x = localX - Layout.Viewport.X, y = localY - Layout.Viewport.Y + Scroll;
            for (int i = 0; i < Layout.Elements.Count; i++)
            {
                F5Element element = Layout.Elements[i];
                if (element.Kind == F5ElementKind.Button && element.Rect.Contains(x, y)) return element;
            }
            return null;
        }

        internal void RestorePosition(WindowPosition position) { positionProjection = position; }
        internal WindowPosition TakePositionToSave()
        { WindowPosition result = positionToSave; positionToSave = null; return result; }

        private void FinishTitleDrag()
        {
            if (capture != 1 || ((int)X == dragStartX && (int)Y == dragStartY)) return;
            positionToSave = new WindowPosition((int)X, (int)Y);
            positionProjection = positionToSave;
        }

        internal void Close()
        {
            // Close/focus loss/session exit may carry synthesized input. Submit
            // the last accepted position before cancelling, never that sample.
            FinishTitleDrag();
            Visible = false; capture = 0; armed = null; Command = F5Command.None;
        }
        private static float Clamp(float value, float minimum, float maximum)
        { return Math.Max(minimum, Math.Min(maximum, value)); }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
