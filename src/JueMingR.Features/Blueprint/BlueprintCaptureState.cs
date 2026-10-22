namespace JueMingR.Features.Blueprint
{
    // The sole session editor state. UI presents it; it never owns another mode,
    // draft, or selection. Release observation is independent of edit admission.
    public sealed class BlueprintCaptureState
    {
        public BlueprintSelection Selection { get; } = new BlueprintSelection();
        public bool Active { get; private set; }
        public bool Dragging { get; private set; }
        public bool NeedsNeutral { get; private set; } = true;
        public BlueprintRectangle Preview { get; private set; }
        private int startX, startY, tails, sampleTails;
        private bool left, right, item, tile, previousLeft;
        public int BlockedActions { get { return Active ? 3 : tails | sampleTails; } }

        public void Observe(bool trusted, bool leftDown, bool rightDown, bool itemRequest, bool tileRequest)
        {
            if (!trusted) { Interrupt(); return; }
            left = leftDown; right = rightDown; item = itemRequest; tile = tileRequest;
            // Keep each real neutral sample blocked until the next mapping. A
            // lost-focus synthetic Released cannot retire either physical tail.
            sampleTails = tails;
            int held = (left || item ? 1 : 0) | (right || tile ? 2 : 0);
            tails &= held;
        }
        public void Enter()
        {
            Active = true; Selection.Clear(); Interrupt();
            tails |= (left || item ? 1 : 0) | (right || tile ? 2 : 0);
        }
        public void Exit()
        {
            tails |= (left || item ? 1 : 0) | (right || tile ? 2 : 0);
            Active = false; Selection.Clear(); Interrupt();
        }
        public void Interrupt()
        { Dragging = false; Preview = default(BlueprintRectangle); NeedsNeutral = true; previousLeft = true; }

        // Only a frozen sample certified by the matching native UI pass arrives
        // here. Unknown/UI ownership cancels; Draw itself never commits XOR.
        public void Pointer(bool trustedWorld, bool leftDown, bool rightDown, int x, int y, int width, int height)
        {
            if (!Active || !trustedWorld) { Interrupt(); return; }
            if (NeedsNeutral)
            {
                if (!leftDown && !rightDown) { NeedsNeutral = false; previousLeft = false; }
                return;
            }
            if (leftDown && !previousLeft) { Dragging = true; startX = x; startY = y; }
            if (Dragging)
            {
                Preview = BlueprintRectangle.FromEndpoints(startX, startY, x, y, width, height);
                if (!leftDown)
                {
                    Selection.ToggleRectangle(startX, startY, x, y, width, height);
                    Dragging = false; Preview = default(BlueprintRectangle);
                }
            }
            previousLeft = leftDown;
        }
    }
}
