using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Runtime.Serialization;
using JueMingR.TerrariaHost.WorldObjectText;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content.Readers;
using ReLogic.Graphics;
using ReLogic.Content;
using ReLogic.Content.Sources;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;

namespace NativeWorldTextProbe
{
    internal sealed class ProbeGraphics : IDisposable, IGraphicsDeviceService, IContentSource
    {
        private IntPtr window;
        private readonly SpriteBatch batch;
        private readonly XnbReader reader;
        private RenderTarget2D costCanvas;
        private readonly string contentDirectory;
        private IAssetRepository screenEffectsRepository,previousAssets;
        private Asset<Effect> previousScreenShader;
        private FilterManager previousFilters;
        private readonly Dictionary<int, Texture2D> tiles = new Dictionary<int, Texture2D>();
        public GraphicsDevice GraphicsDevice { get; private set; }
        internal DynamicSpriteFont Font { get; }
        internal void SetMouseFont(DynamicSpriteFont value) { Terraria.GameContent.FontAssets.MouseText = Loaded("probe-replaced-font", value); }
        internal void LoadDeathTexture()
        { using (var stream = File.OpenRead(Path.Combine(contentDirectory, "Images", "MapDeath.xnb"))) Terraria.GameContent.TextureAssets.MapDeath = Loaded("Images/MapDeath", reader.FromStream<Texture2D>(stream)); }
        internal void LoadMarkerTextures()
        { foreach (int id in new[] { 8, 48, 50, 224, 171, 393, 966, 29 }) using (var stream = File.OpenRead(Path.Combine(contentDirectory, "Images", "Item_" + id + ".xnb"))) Terraria.GameContent.TextureAssets.Item[id] = Loaded("Images/Item_" + id, reader.FromStream<Texture2D>(stream)); }
        internal void LoadItemTextures(IEnumerable<int> ids)
        { foreach (int id in ids) if (Terraria.GameContent.TextureAssets.Item[id] == null || !Terraria.GameContent.TextureAssets.Item[id].IsLoaded) using (var stream = File.OpenRead(Path.Combine(contentDirectory, "Images", "Item_" + id + ".xnb"))) Terraria.GameContent.TextureAssets.Item[id] = Loaded("Images/Item_" + id, reader.FromStream<Texture2D>(stream)); }
        internal void LoadBuffTexture(int id)
        {using(var stream=File.OpenRead(Path.Combine(contentDirectory,"Images","Buff_"+id+".xnb")))Terraria.GameContent.TextureAssets.Buff[id]=Loaded("Images/Buff_"+id,reader.FromStream<Texture2D>(stream));}
        internal void LoadTexture(string field,string relative,int index=-1)
        {
            using(var stream=File.OpenRead(Path.Combine(contentDirectory,relative+".xnb")))
            {
                var asset=Loaded(relative,reader.FromStream<Texture2D>(stream));var target=typeof(Terraria.GameContent.TextureAssets).GetField(field);
                if(index<0)target.SetValue(null,asset);else ((Array)target.GetValue(null)).SetValue(asset,index);
            }
        }
        private void EnsureNativeAssets()
        {
            if(screenEffectsRepository!=null)return;
            // Original asset services/XNB decoding read the supplied content
            // directory. XnaContentSource assumes a relative TitleContainer
            // URI; this isolated probe receives an absolute directory, so use
            // ReLogic's existing filesystem provider without game startup or
            // resource-pack/configuration enumeration.
            var services=new GameServiceContainer();services.AddService(typeof(IGraphicsDeviceService),this);
            Terraria.Initializers.AssetInitializer.CreateAssetServices(services);
            screenEffectsRepository=(IAssetRepository)services.GetService(typeof(IAssetRepository));
            screenEffectsRepository.SetSources(new IContentSource[]{new FileSystemContentSource(contentDirectory)});
            previousAssets=Terraria.Main.Assets;previousScreenShader=Terraria.Main.ScreenShaderRef;
            Terraria.Main.Assets=screenEffectsRepository;
        }
        internal void LoadCompanionAssets()
        {
            EnsureNativeAssets();
            // Normal Main.Initialize establishes the native light engine before
            // any Player.GetHairColor call. This isolated Draw skips that entry.
            Terraria.Lighting.Mode=Terraria.Graphics.Light.LightMode.Color;
            // HandleInput normally computes this pulse before HUD drawing.
            Terraria.Main.cursorScale=1f;
            Terraria.Main.PixelShaderRef=screenEffectsRepository.Request<Effect>("PixelShader",AssetRequestMode.ImmediateLoad);
            Terraria.Main.spriteBuffer=new Terraria.DataStructures.SpriteDrawBuffer(GraphicsDevice);
            Terraria.Initializers.PlayerDataInitializer.Load();
            var playerAssets=new HashSet<Asset<Texture2D>>();
            for(int variant=0;variant<Terraria.ID.PlayerVariantID.Count;variant++)
                for(int piece=0;piece<Terraria.ID.PlayerTextureID.Count;piece++)
                {var asset=Terraria.GameContent.TextureAssets.Players[variant,piece];if(asset!=null&&asset!=Asset<Texture2D>.Empty)playerAssets.Add(asset);}
            // PlayerDataInitializer requests AsyncLoad. Re-requesting an already
            // Loading asset with ImmediateLoad does not change its mode. Normal
            // Main.Draw pumps these original GPU transfers; the probe must do the
            // same before claiming a body texture was available to the renderer.
            var watch=System.Diagnostics.Stopwatch.StartNew();int pending;
            do
            {
                screenEffectsRepository.TransferCompletedAssets();pending=0;
                foreach(var asset in playerAssets)if(!asset.IsLoaded||asset.Value==null)pending++;
                if(pending==0)break;
                if(watch.Elapsed.TotalSeconds>10)throw new InvalidOperationException("native-player-assets-transfer-timeout pending="+pending);
                System.Threading.Thread.Sleep(2);
            }while(true);
            Console.WriteLine("BODY-ASSETS native-transferred="+playerAssets.Count+" pending="+pending);
            for(int i=0;i<Terraria.GameContent.TextureAssets.PlayerHair.Length;i++)
            {
                Terraria.GameContent.TextureAssets.PlayerHair[i]=screenEffectsRepository.Request<Texture2D>("Images/Player_Hair_"+(i+1),AssetRequestMode.ImmediateLoad);
                Terraria.GameContent.TextureAssets.PlayerHairAlt[i]=screenEffectsRepository.Request<Texture2D>("Images/Player_HairAlt_"+(i+1),AssetRequestMode.ImmediateLoad);
            }
            LoadItemTextures(new[]{0}); // native HeldItem layer loads even its empty item before returning
            LoadTexture("Heart","Images/Heart"); LoadTexture("Heart2","Images/Heart2"); LoadTexture("Mana","Images/Mana");
            LoadTexture("Extra","Images/Extra_54",54);
            LoadTexture("Pvp","Images/UI/PVP_1",1);LoadTexture("Pvp","Images/UI/PVP_2",2);
            foreach(string field in new[]{"ColorBar","ColorBlip","ColorSlider"})LoadTexture(field,"Images/"+field);
            LoadTexture("ColorHighlight","Images/UI/Slider_Highlight");
            using(var stream=File.OpenRead(Path.Combine(contentDirectory,"Fonts","Death_Text.xnb")))Terraria.GameContent.FontAssets.DeathText=Loaded("Fonts/Death_Text",reader.FromStream<DynamicSpriteFont>(stream));
            foreach(string name in new[]{"CategoryPanel","CategoryPanelHighlight","CategoryPanelBorder","SmallPanel","SmallPanelBorder","PanelGrayscale","Copy","Paste","Randomize","Voice","ColorHair","ColorEye","ColorEyeBack","ColorSkin","ColorShirt","ColorUndershirt","ColorPants","ColorShoes","ColorCharacter","HairStyle_Hair","HairStyle_Arrow","ClothStyleMale","ClothStyleFemale","CharInfo"})
                screenEffectsRepository.Request<Texture2D>("Images/UI/CharCreation/"+name,AssetRequestMode.ImmediateLoad);
            foreach(string name in new[]{"PanelBorder","PanelBackground","Scrollbar","ScrollbarInner","PlayerBackground","TexturePackButtons","TexturePackButtonsOutline","VK_Shift","VK_Backspace"})
                screenEffectsRepository.Request<Texture2D>("Images/UI/"+name,AssetRequestMode.ImmediateLoad);
            LoadItemTextures(new int[]{271,5660,91,239,237});
            foreach(int type in new int[]{271,5660,91,239,237})screenEffectsRepository.Request<Texture2D>("Images/Item_"+type,AssetRequestMode.ImmediateLoad);
            if(!Terraria.UI.Gamepad.UILinkPointNavigator.Points.ContainsKey(3000))Terraria.Initializers.UILinksInitializer.Load();

            // Native render paths use these tables even for an empty armor set.
            foreach(var pair in new[]{new[]{"ArmorHead","Armor_Head_"},new[]{"ArmorBody","Armor_Body_"},new[]{"ArmorArm","Armor_Arm_"},new[]{"ArmorLeg","Armor_Legs_"},new[]{"ArmorBodyComposite","Armor/Armor_"}})
            {
                var field=typeof(Terraria.GameContent.TextureAssets).GetField(pair[0]); var values=(Asset<Texture2D>[])field.GetValue(null);
                for(int i=0;i<values.Length;i++) if(File.Exists(Path.Combine(contentDirectory,"Images",pair[1]+i+".xnb"))) values[i]=screenEffectsRepository.Request<Texture2D>("Images/"+pair[1]+i,AssetRequestMode.DoNotLoad);
            }
            if(Terraria.Main.ResourceSetsManager==null) Terraria.Main.ResourceSetsManager=new Terraria.GameContent.UI.ResourceSets.PlayerResourceSetsManager();
            Terraria.Main.ResourceSetsManager.LoadContent(AssetRequestMode.ImmediateLoad);
        }
        internal void LoadCompanionBarAssets()
        {
            EnsureNativeAssets();
            foreach(string name in new[]{"HP_Fill","HP_Fill_Honey","MP_Fill","Panel_Left","HP_Panel_Middle","HP_Panel_Right","MP_Panel_Middle","MP_Panel_Right"})
                screenEffectsRepository.Request<Texture2D>("Images/UI/PlayerResourceSets/HorizontalBars/"+name,AssetRequestMode.ImmediateLoad);
        }
        internal void LoadCompanionFancyAssets()
        {
            EnsureNativeAssets();
            foreach(string name in new[]{"Heart_Left","Heart_Middle","Heart_Right","Heart_Right_Fancy","Heart_Single_Fancy","Heart_Fill","Heart_Fill_B","Star_A","Star_B","Star_C","Star_Single","Star_Fill"})
                screenEffectsRepository.Request<Texture2D>("Images/UI/PlayerResourceSets/FancyClassic/"+name,AssetRequestMode.ImmediateLoad);
        }
        internal void LoadMoonDeathAssets()
        {
            // AI77's death explosion is a real visual outlet. Keep its native
            // object creation and RNG; load only its original XNB dependency.
            EnsureNativeAssets();
            var explosion=screenEffectsRepository.Request<Texture2D>("Images/Misc/MoonExplosion/Explosion",AssetRequestMode.ImmediateLoad);
            if(!explosion.IsLoaded || explosion.Value==null)throw new InvalidOperationException("original-moon-explosion-content-not-loaded");
        }
        internal void LoadScreenEffects()
        {
            if(previousFilters!=null)return;
            EnsureNativeAssets();
            Terraria.Main.ScreenShaderRef=screenEffectsRepository.Request<Effect>("ScreenShader",AssetRequestMode.ImmediateLoad);
            var noise=screenEffectsRepository.Request<Texture2D>("Images/Misc/noise",AssetRequestMode.ImmediateLoad);
            if(!Terraria.Main.ScreenShaderRef.IsLoaded || !noise.IsLoaded)throw new InvalidOperationException("original-screen-effect-content-not-loaded");
            // These are the native initializer's three DD2 filter bindings.
            // Load real Effect/Filter objects; the unrelated ending-credit
            // sky requires the full NPC texture atlas and is not a dependency
            // of AI132 crystal loss or AI146 victory.
            previousFilters=Filters.Scene;Filters.Scene=new FilterManager();
            Filters.Scene["CrystalDestructionColor"]=new Filter(new ScreenShaderData("FilterCrystalDestructionColor").UseColor(1f,0f,.75f).UseIntensity(1f).UseOpacity(.8f),EffectPriority.VeryHigh);
            Filters.Scene["CrystalDestructionVortex"]=new Filter(new ScreenShaderData("FilterCrystalDestructionVortex").UseImage("Images/Misc/noise"),EffectPriority.VeryHigh);
            Filters.Scene["CrystalWin"]=new Filter(new ScreenShaderData("FilterCrystalWin"),EffectPriority.VeryHigh);
            Filters.Scene.Load();
        }
        internal ProbeGraphics(string content, bool largeCanvas = false)
        {
            contentDirectory = content;
            if (GraphicsAdapter.Adapters.Count == 0)
                throw new NotSupportedException("No XNA graphics adapters are available before device creation; resource drawing and pixels were not executed.");
            window = CreateWindowEx(0, "STATIC", "Native text probe", 0, 0, 0, 960, 640, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (window == IntPtr.Zero) throw new InvalidOperationException("hidden-test-window-unavailable");
            // A 2560-wide offscreen render target exceeds Reach's 2048 texture
            // limit. Only the explicit large-window visual scope needs HiDef.
            GraphicsDevice = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, largeCanvas ? GraphicsProfile.HiDef : GraphicsProfile.Reach, new PresentationParameters {
                DeviceWindowHandle = window, BackBufferWidth = 960, BackBufferHeight = 640, BackBufferFormat = SurfaceFormat.Color, DepthStencilFormat = DepthFormat.None, IsFullScreen = false });
            batch = new SpriteBatch(GraphicsDevice); Terraria.Main.spriteBatch = batch;
            var services = new GameServiceContainer(); services.AddService(typeof(IGraphicsDeviceService), this); reader = new XnbReader(services);
            using (var stream = File.OpenRead(Path.Combine(content, "Fonts", "Mouse_Text.xnb"))) Font = reader.FromStream<DynamicSpriteFont>(stream);
            Terraria.Main.instance = (Terraria.Main)FormatterServices.GetUninitializedObject(typeof(Terraria.Main));
            // Supply only the already-created device service. Calling the real
            // Main/Game constructor or game Initialize/Run is outside this probe.
            typeof(Game).GetField("graphicsDeviceService", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Terraria.Main.instance, this);
            GC.SuppressFinalize(Terraria.Main.instance);
            Terraria.Main.netMode = 0; Terraria.Main.dedServ = false;
            Terraria.GameContent.FontAssets.MouseText = Loaded("probe-native-font", Font);
            using (var stream = File.OpenRead(Path.Combine(content, "Fonts", "Item_Stack.xnb"))) Terraria.GameContent.FontAssets.ItemStack = Loaded("probe-native-stack", reader.FromStream<DynamicSpriteFont>(stream));
            using (var stream = File.OpenRead(Path.Combine(content, "Fonts", "Combat_Text.xnb"))) Terraria.GameContent.FontAssets.CombatText[0] = Loaded("probe-native-combat", reader.FromStream<DynamicSpriteFont>(stream));
            using (var stream = File.OpenRead(Path.Combine(content, "Fonts", "Combat_Crit.xnb"))) Terraria.GameContent.FontAssets.CombatText[1] = Loaded("probe-native-combat-crit", reader.FromStream<DynamicSpriteFont>(stream));
            using (var stream = File.OpenRead(Path.Combine(content, "Images", "Item_8.xnb"))) Terraria.GameContent.TextureAssets.Item[8] = Loaded("Images/Item_8", reader.FromStream<Texture2D>(stream));
            using (var stream = File.OpenRead(Path.Combine(content, "Images", "Inventory_Back.xnb"))) Terraria.GameContent.TextureAssets.InventoryBack = Loaded("Images/Inventory_Back", reader.FromStream<Texture2D>(stream));
            using (var stream = File.OpenRead(Path.Combine(content, "Images", "MagicPixel.xnb"))) Terraria.GameContent.TextureAssets.MagicPixel = Loaded("Images/MagicPixel", reader.FromStream<Texture2D>(stream));
        }
        internal void Preview(string output, params NativeWorldTextLayout[] layouts)
        {
            Directory.CreateDirectory(output);
            using (var canvas = new RenderTarget2D(GraphicsDevice, 960, 640))
            {
                GraphicsDevice.SetRenderTarget(canvas); GraphicsDevice.Clear(new Color(30, 43, 47));
                batch.Begin();
                for (int i = 0; i < layouts.Length; i++) { layouts[i].ApplyColor(0xE6C16A); layouts[i].Draw(batch, new Vector2(30 + i % 3 * 310, 50 + i / 3 * 300)); }
                batch.End(); GraphicsDevice.SetRenderTarget(null);
                using (var stream = new FileStream(Path.Combine(output, "native-text-layout.png"), FileMode.CreateNew)) canvas.SaveAsPng(stream, 960, 640);
            }
        }
        internal void DrawWorld(WorldObjectTextWorldLayer layer, string output)
        {
            using (var canvas = new RenderTarget2D(GraphicsDevice, 960, 640))
            {
                GraphicsDevice.SetRenderTarget(canvas); GraphicsDevice.Clear(new Color(30, 43, 47));
                batch.Begin(); layer.Draw(); batch.End(); GraphicsDevice.SetRenderTarget(null);
                using (var stream = new FileStream(output, FileMode.CreateNew)) canvas.SaveAsPng(stream, 960, 640);
            }
        }
        internal Color[] WorldPixels(WorldObjectTextWorldLayer layer)
        {
            using (var canvas = new RenderTarget2D(GraphicsDevice, 960, 640))
            {
                GraphicsDevice.SetRenderTarget(canvas); GraphicsDevice.Clear(Color.Transparent);
                batch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, Terraria.Main.GameViewMatrix.ZoomMatrix);
                layer.Draw(); batch.End(); GraphicsDevice.SetRenderTarget(null);
                var pixels = new Color[960 * 640]; canvas.GetData(pixels); return pixels;
            }
        }
        internal void DrawFrame(WorldObjectTextWorldLayer layer)
        {
            if (costCanvas == null) costCanvas = new RenderTarget2D(GraphicsDevice, 960, 640);
            GraphicsDevice.SetRenderTarget(costCanvas); GraphicsDevice.Clear(new Color(30, 43, 47));
            batch.Begin(); layer.Draw(); batch.End(); GraphicsDevice.SetRenderTarget(null);
        }
        internal void Render(Action draw,Matrix matrix)
        {
            if(costCanvas==null)costCanvas=new RenderTarget2D(GraphicsDevice,960,640);
            GraphicsDevice.SetRenderTarget(costCanvas);GraphicsDevice.Clear(Color.Transparent);
            batch.Begin(SpriteSortMode.Deferred,null,null,null,null,null,matrix);draw();batch.End();GraphicsDevice.SetRenderTarget(null);
        }
        internal void ReadRenderedPixels(Color[] destination)
        {
            // Reuse the Render-owned canvas and caller-owned complete buffer.
            // Continuous pixel checks must not create a 2.4 MB buffer and a
            // GPU target every update, perturbing the next CPU budget scope.
            if(costCanvas==null || destination==null || destination.Length!=costCanvas.Width*costCanvas.Height)throw new ArgumentException("rendered-pixel-buffer-size");
            costCanvas.GetData(destination);
        }
        internal void Image(string output, Action draw, Matrix matrix, int width = 960, int height = 640)
        {
            using (var canvas = new RenderTarget2D(GraphicsDevice, width, height))
            {
                GraphicsDevice.SetRenderTarget(canvas); GraphicsDevice.Clear(new Color(30, 43, 47));
                batch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, matrix); draw(); batch.End(); GraphicsDevice.SetRenderTarget(null);
                using (var stream = new FileStream(output, FileMode.CreateNew)) canvas.SaveAsPng(stream, width, height);
            }
        }
        internal Color[] Pixels(Action draw, Matrix matrix,int width=960,int height=640)
        {
            using (var canvas = new RenderTarget2D(GraphicsDevice, width, height))
            {
                GraphicsDevice.SetRenderTarget(canvas); GraphicsDevice.Clear(Color.Transparent);
                batch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, matrix); draw(); batch.End(); GraphicsDevice.SetRenderTarget(null);
                var pixels = new Color[width * height]; canvas.GetData(pixels); return pixels;
            }
        }
        internal void Scene(WorldObjectTextWorldLayer layer, string output)
        { Scene(() => layer.Draw(), output); }
        internal void Scene(Action overlay, string output)
        {
            Image(output, () =>
            {
                for (int y = 0; y < 50; y++) for (int x = 0; x < 75; x++)
                {
                    var tile = Terraria.Main.tile[x, y]; if (tile == null || !tile.active()) continue;
                    var texture = TileTexture(tile.type);
                    int frameX = tile.frameX, frameY = tile.frameY;
                    if (tile.type == 88) { frameY += 36 * (frameX / 1998); frameX %= 1998; }
                    int height = IsChest(tile.type) && tile.frameY == 18 ? 18 : 16;
                    var position = new Vector2(x * 16, y * 16) - Terraria.Main.screenPosition;
                    if (tile.type == 85) position.Y += 2;
                    if (Terraria.Main.LocalPlayer.gravDir == -1) position.Y = Terraria.Main.screenHeight - position.Y - height;
                    batch.Draw(texture, position, new Rectangle(frameX, frameY, 16, height), Color.White, 0, Vector2.Zero, 1,
                        Terraria.Main.LocalPlayer.gravDir == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None, 0);
                }
                overlay();
            }, Terraria.Main.GameViewMatrix.ZoomMatrix);
        }
        private Texture2D TileTexture(int type)
        {
            Texture2D texture;
            if (!tiles.TryGetValue(type, out texture))
            { using (var stream = File.OpenRead(Path.Combine(contentDirectory, "Images", "Tiles_" + type + ".xnb"))) texture = reader.FromStream<Texture2D>(stream); tiles.Add(type, texture); }
            return texture;
        }
        private static bool IsChest(int type) { return type == 21 || type == 441 || type == 467 || type == 468; }
        internal Rectangle ObjectArtBounds(int type, int style, int width, bool boardOnly = false)
        {
            // Test-only texture readback. Reproduce .8 closed source slices,
            // including 18-high chest bottom tiles and dresser style wrapping.
            var texture = TileTexture(type); var pixels = new Color[texture.Width * texture.Height]; texture.GetData(pixels);
            int left = width * 16, right = -1, top = 34, bottom = -1;
            for (int y = 0; y < (IsChest(type) ? 34 : 32); y++) for (int x = 0; x < width * 16; x++)
            {
                // Original XNB board regions, independently inspected against
                // chains/posts. Read actual alpha within them, retaining jagged
                // edges; a generic row-width threshold would erase fragments.
                if (boardOnly && (type == 55 || type == 425 || type == 573))
                {
                    if (style == 0 && (y >= 22 || type == 573 && y < 4)) continue;
                    if (style == 1 && y < (type == 573 ? 14 : 10)) continue;
                }
                int sourceX = (type == 88 ? style % 37 : style) * width * 18 + x / 16 * 18 + x % 16;
                int sourceY = y + (y >= 16 ? 2 : 0) + (type == 88 ? style / 37 * 36 : 0);
                if (pixels[sourceY * texture.Width + sourceX].A == 0) continue;
                left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y);
            }
            if (bottom < 0) throw new InvalidOperationException("Empty native art fixture");
            return new Rectangle(left, top + (type == 85 ? 2 : 0), right - left + 1, bottom - top + 1);
        }
        public void Dispose() { if(screenEffectsRepository!=null){if(previousFilters!=null)Filters.Scene=previousFilters;Terraria.Main.Assets=previousAssets;Terraria.Main.ScreenShaderRef=previousScreenShader;(screenEffectsRepository as IDisposable)?.Dispose();screenEffectsRepository=null;} foreach (var texture in tiles.Values) texture.Dispose(); costCanvas?.Dispose(); reader?.Dispose(); batch?.Dispose(); GraphicsDevice?.Dispose(); if (window != IntPtr.Zero) { DestroyWindow(window); window = IntPtr.Zero; } }
        private Asset<T> Loaded<T>(string name, T value) where T : class
        {
            var asset = (Asset<T>)Activator.CreateInstance(typeof(Asset<T>), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { name }, null);
            typeof(Asset<T>).GetMethod("SubmitLoadedContent", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(asset, new object[] { value, this });
            if (asset.State != AssetState.Loaded) throw new InvalidOperationException("native-probe-asset-not-loaded");
            return asset;
        }
        public IContentValidator ContentValidator { get; set; }
        public string FileWatcherPath { get { return null; } }
        public bool HasAsset(string name) { return false; }
        public List<string> GetAllAssetsStartingWith(string name) { return new List<string>(); }
        public string GetExtension(string name) { return null; }
        public Stream OpenStream(string name) { throw new NotSupportedException(); }
        public void RejectAsset(string name, IRejectionReason reason) { }
        public void ClearRejections() { }
        public bool TryGetRejections(List<string> reasons) { return false; }
        public void Refresh() { }
        public event EventHandler<EventArgs> DeviceCreated { add { } remove { } }
        public event EventHandler<EventArgs> DeviceDisposing { add { } remove { } }
        public event EventHandler<EventArgs> DeviceReset { add { } remove { } }
        public event EventHandler<EventArgs> DeviceResetting { add { } remove { } }
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(int extended, string className, string title, int style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr module, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr window);
    }
}
