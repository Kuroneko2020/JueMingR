using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.Items
{
    // Both item browser and blueprint materials borrow only their visible game
    // textures. Draw never calls LoadItem; clearing this cache never disposes a
    // texture owned by Terraria or a resource pack.
    internal sealed class PreparedItemIcons
    {
        private readonly Dictionary<int, Texture2D> icons = new Dictionary<int, Texture2D>();
        private readonly HashSet<int> visible = new HashSet<int>();
        private readonly List<int> removed = new List<int>();
#if DEBUG
        internal long IconLoads, VisibleSetBuilds;
#endif
        internal void BeginVisibleSet() { visible.Clear(); }
        internal void AddVisible(int type) { if (type > 0) visible.Add(type); }
        internal void EndVisibleSet()
        {
            removed.Clear();
            foreach (int type in icons.Keys) if (!visible.Contains(type)) removed.Add(type);
            foreach (int type in removed) icons.Remove(type);
#if DEBUG
            VisibleSetBuilds++;
#endif
        }
        internal void Prepare()
        {
            foreach (int type in visible)
            {
                if (type >= TextureAssets.Item.Length) continue;
                Texture2D prior;
                if (!icons.TryGetValue(type, out prior) || prior == null || prior.IsDisposed || !ReferenceEquals(prior, TextureAssets.Item[type]?.Value))
                {
                    Main.instance.LoadItem(type); icons[type] = TextureAssets.Item[type]?.Value;
#if DEBUG
                    IconLoads++;
#endif
                }
            }
        }
        internal Texture2D Get(int type) { Texture2D value; return icons.TryGetValue(type, out value) ? value : null; }
        internal void Clear() { icons.Clear(); visible.Clear(); removed.Clear(); }
    }
}
