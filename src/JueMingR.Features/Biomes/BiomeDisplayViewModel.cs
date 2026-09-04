namespace JueMingR.Features.Biomes
{
    public sealed class BiomeDisplayViewModel
    {
        private static readonly BiomeDisplayViewModel HiddenInstance =
            new BiomeDisplayViewModel(false, string.Empty);

        public BiomeDisplayViewModel(bool visible, string text)
        {
            Visible = visible;
            Text = text ?? string.Empty;
        }

        public bool Visible { get; }

        public string Text { get; }

        internal static BiomeDisplayViewModel Hidden
        {
            get { return HiddenInstance; }
        }
    }
}
