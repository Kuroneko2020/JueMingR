namespace JueMingR.Platform.Information
{
    // One completed game-update sample. Missing facts remain nullable; a
    // default number/false never silently supplies a missing observation.
    public struct InfectionObservation
    {
        public InformationAvailability Availability;
        public int? Hallow, Corruption, Crimson;
    }
    public struct LuckObservation
    {
        public InformationAvailability Availability;
        public float? Total, LadybugTime, Torch, Equipment, Coin;
        public int? Potion, Kite;
        public bool? Pearl, Lantern, Gnome, Stinky, Mirror;
    }
    public struct AnglerObservation
    {
        public InformationAvailability Availability;
        public int? ItemType, Completed;
        public bool? SubmittedToday;
        // Host resolves these only when the legal item/language changes.
        public string Name, Location;
    }
}
