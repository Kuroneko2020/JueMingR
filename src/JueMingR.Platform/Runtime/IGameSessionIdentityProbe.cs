namespace JueMingR.Platform.Runtime
{
    // Identity is an opaque Host token, stable only for one live player/world/
    // connection. The Runtime, not the probe or a Feature, issues generations.
    public interface IGameSessionIdentityProbe : IGameSessionProbe
    {
        object SessionIdentity { get; }
    }
}
