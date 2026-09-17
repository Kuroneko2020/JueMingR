using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.F5
{
    // The shell owns navigation/input scheduling; the browser owns its page.
    // Native catalog and world receivers remain outside shell composition.
    internal interface IBrowserPage
    {
        bool OwnsPointer { get; }
        bool OwnsTextToken { get; }
        bool ConsumeLeft { get; }
        bool ConsumeRight { get; }
        bool ConsumeWheel { get; }
        bool RequestFinish();
        void Suspend();
        void BeforeInput(bool active);
        bool Wheel(float x, float y, int delta);
        void Process(bool active, Vector2 pointer, bool blocked, bool geometryCurrent);
        void Prepare(bool active, Matrix transform);
        void Draw();
    }
}
