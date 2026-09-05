using System;

namespace JueMingR.TerrariaHost.F5
{
    internal struct F5Input
    {
        internal float Width, Height, Scale, X, Y;
        internal bool Active, Focused, F5, Left, Right;
        internal int Wheel;
    }

    internal sealed class F5Interaction
    {
        private bool previousF5, previousLeft, leftTail, rightTail, active, positioned;
        private int capture; // 1: title, 2: scrollbar; never a second hover-ownership flag.
        private float grabX, grabY, windowWidth, windowHeight;
        private F5Element armed;
        private int armedGeneration;
        internal readonly F5Layout Layout = new F5Layout();
        internal bool Visible { get; private set; }
        internal bool Ready { get; set; }
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
            ConsumeLeft = ConsumeRight = ConsumeWheel = false;
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
            F5Size size = F5Layout.WindowSize(input.Width, input.Height, input.Scale);
            windowWidth = size.Width; windowHeight = size.Height;
            if (!positioned)
            {
                X = (input.Width / input.Scale - size.Width) / 2;
                Y = (input.Height / input.Scale - size.Height) / 2;
                positioned = true;
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
                    Close();
                }
                else Visible = true;
            }
            previousF5 = input.F5;
            bool layoutReady = Layout.Matches(input.Width, input.Height, input.Scale, Page);
            if (Visible && capture == 1 && input.Left)
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
                if (layoutReady)
                {
                    float localX = input.X - X, localY = input.Y - Y;
                    if (pressed)
                    {
                        armed = null;
                        if (Layout.Title.Contains(localX, localY))
                        { capture = 1; grabX = localX; grabY = localY; }
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
                                { Page = i; Scroll = 0; layoutReady = false; break; }
                            if (layoutReady) { armed = HitButton(localX, localY); armedGeneration = Layout.Generation; }
                        }
                    }
                    if (capture == 2 && input.Left)
                    {
                        F5Rect thumb = Layout.ScrollThumb(Scroll);
                        float travel = Layout.ScrollTrack.Height - thumb.Height;
                        Scroll = travel <= 0 ? 0 : Clamp((localY - Layout.ScrollTrack.Y - grabY) / travel, 0, 1) * Layout.MaxScroll;
                    }
                    if (input.Wheel != 0 && capture == 0 && layoutReady)
                    { Scroll = Clamp(Scroll - input.Wheel / 120f * 40, 0, Layout.MaxScroll); armed = null; }
                    if (released && capture == 0 && layoutReady && armed != null &&
                        armedGeneration == Layout.Generation && ReferenceEquals(armed, HitButton(localX, localY)))
                        Command = armed.Command;
                }
            }
            ConsumeLeft = leftTail; ConsumeRight = rightTail;
            if (!input.Left) { leftTail = false; capture = 0; armed = null; }
            if (!input.Right) rightTail = false;
            previousLeft = input.Left;
        }

        internal void ClampScroll() { Scroll = Clamp(Scroll, 0, Layout.MaxScroll); }

        internal F5Element HitButton(float localX, float localY)
        {
            if (!Layout.Viewport.Contains(localX, localY)) return null;
            float x = localX - Layout.Viewport.X, y = localY - Layout.Viewport.Y + Scroll;
            for (int i = 0; i < Layout.Elements.Count; i++)
            {
                F5Element element = Layout.Elements[i];
                if (element.Kind == F5ElementKind.Button && element.Rect.Contains(x, y)) return element;
            }
            return null;
        }

        internal void Close() { Visible = false; capture = 0; armed = null; Command = F5Command.None; }
        private static float Clamp(float value, float minimum, float maximum)
        { return Math.Max(minimum, Math.Min(maximum, value)); }
    }
}
