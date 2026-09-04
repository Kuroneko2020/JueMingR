namespace JueMingR.Platform.Biomes
{
    public interface IBiomeObservationSource
    {
        bool TryObserve(out BiomeObservation observation);
    }
}
