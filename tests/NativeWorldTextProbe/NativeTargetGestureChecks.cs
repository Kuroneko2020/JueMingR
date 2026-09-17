using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeTargetGestureChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static void Run(Assembly assembly, object catalog)
        {
            Main.gameMenu = Main.mapFullscreen = Main.hideUI = false; Main.netMode = 0;
            Main.maxTilesX = Main.maxTilesY = 64; Main.tile = new Tile[64, 64]; Main.tile[1, 1] = new Tile();
            Terraria.ObjectData.TileObjectData.Initialize();
            // Main.Initialize_TileAndNPCData2 sets this exact native fixture flag.
            Main.tileFrameImportant[21] = true;
            // The headless probe skips Main.Initialize: its zero-filled arrays
            // must not masquerade as an enabled glow mask for ordinary tiles.
            foreach (int tileType in new[] { 0, 1, 21, 144 }) { Main.tileGlowMask[tileType] = -1; Main.tileFlame[tileType] = false; }
            Main.LocalPlayer.position = new Vector2(800, 800); Main.LocalPlayer.gravDir = 1; Main.LocalPlayer.chest = -1;
            Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(null); Main.GameViewMatrix.SetViewportOverride(new Viewport(0, 0, 960, 640));
            Main.screenPosition = Vector2.Zero; Main.screenWidth = 960; Main.screenHeight = 640;
            typeof(Main).GetField("_uiScaleMatrix", Flags).SetValue(null, Matrix.Identity);
            typeof(PlayerInput).GetField("_originalScreenWidth", Flags).SetValue(null, 960); typeof(PlayerInput).GetField("_originalScreenHeight", Flags).SetValue(null, 640);
            typeof(PlayerInput).GetField("RawMouseScale", Flags).SetValue(null, Vector2.One);
            PlayerInput.Triggers.Initialize(); Main.keyState = new KeyboardState(); Main.mouseX = Main.mouseY = 20;
            PlayerInput.MouseInfo = new MouseState(20, 20, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            var inputType = assembly.GetType("JueMingR.TerrariaHost.Input.HostInputState");
            object input = Activator.CreateInstance(inputType, Flags, null, new object[] { (Func<IntPtr>)(() => new IntPtr(1)), (Func<IntPtr>)(() => new IntPtr(1)) }, null);
            FocusHelper.IsSelectedApplication = true; Call(input, "BeginUpdate"); Call(input, "AfterMapping"); Call(input, "AfterKeyboardRefresh");
            var type = assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.ReadOnlyTargetGesture");
            object coldCatalog = Activator.CreateInstance(catalog.GetType(), true);
            object gesture = Activator.CreateInstance(type, Flags, null, new[] { input, coldCatalog }, null);
            try
            {
                var observer = assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.NativeTargetObservation").GetMethod("World", Flags);
                Main.LocalPlayer.position = new Vector2(16, 16); Main.LocalPlayer.name = "ghost sentinel"; Main.LocalPlayer.ghost = true;
                Main.npc[0] = new NPC { active = true, hide = true, life = 10, lifeMax = 20, position = new Vector2(16, 16), width = 32, height = 32 };
                object hidden = observer.Invoke(null, new object[] { new Vector2(20, 20), catalog, true });
                Require(!((System.Collections.Generic.List<string>)Get(hidden, "Entries")).Exists(s => s.Contains("生命")), "ghost players and hidden NPCs cannot become public actor targets");
                Main.LocalPlayer.ghost = false; Main.LocalPlayer.position = new Vector2(800, 800); Main.npc[0].active = false;
                NativeAnnouncementTargetChecks.Run(assembly, catalog);
                var wallTile = new Tile { type = 1, wall = 4 }; wallTile.active(true); wallTile.invisibleBlock(true); wallTile.fullbrightWall(true);
                Main.tile[1, 1] = wallTile;
                object wallTarget = observer.Invoke(null, new object[] { new Vector2(20, 20), catalog, false });
                Require((int)Get(wallTarget, "ItemType") == (int)Call(catalog, "WallItem", (ushort)4) && (int)Get(wallTarget, "ItemType") > 0,
                    "visible fullbright wall remains identifiable behind an echo-painted foreground");
                wallTile.invisibleWall(true);
                Require((int)Get(observer.Invoke(null, new object[] { new Vector2(20, 20), catalog, false }), "ItemType") == 0,
                    "hidden wall and foreground are not revealed by the wall light override");
                var alwaysDrawn = new Tile { type = 144 }; alwaysDrawn.active(true); Main.tile[1, 1] = alwaysDrawn;
                Require(Terraria.ID.TileID.Sets.IgnoreDrawLightConditions[144] && (int)Get(observer.Invoke(null, new object[] { new Vector2(20, 20), catalog, false }), "ItemType") > 0,
                    "native IgnoreDrawLightConditions tile remains identifiable without a lighting sample");
                Main.tile[1, 1] = new Tile();
                Require((bool)Get(gesture, "Ready"), "fixed native read-only gesture hooks install");
                Set(gesture, "CanPick", (Func<bool>)(() => true)); Set(gesture, "CanAnnounce", (Func<bool>)(() => true));
                object result = null; int delivered = 0, cancelled = 0;
                Set(gesture, "Picked", Adapt(type.GetProperty("Picked", Flags).PropertyType, value => { delivered++; result = value; }));
                Set(gesture, "PickCancelled", (Action)(() => cancelled++));
                Action<bool> uiPass = blocked => { Call(gesture, "BeginPass"); Main.LocalPlayer.mouseInterface = blocked; Main.mouseText = blocked; Call(gesture, "BeforeWorldHover"); Call(gesture, "AfterResources", true); Main.mouseText = true; Call(gesture, "EndPass", true); };
                Call(gesture, "Request", 1); uiPass(false); Call(gesture, "Update", 1L);
                Require(delivered == 1 && result != null, "world tooltip flag after UI boundary is not UI遮挡");
                Call(gesture, "Request", 1); uiPass(true); Call(gesture, "Update", 2L);
                Require(delivered == 1 && cancelled == 1, "fresh UI boundary vetoes world target");
                Call(gesture, "Request", 1); Call(gesture, "BeginPass"); Call(gesture, "Hover", new Item[] { new Item() }, 0, 0);
                Main.LocalPlayer.mouseInterface = true; Main.mouseText = false; Call(gesture, "BeforeWorldHover"); Call(gesture, "AfterResources", true); Call(gesture, "EndPass", true); Call(gesture, "Update", 3L);
                Require(delivered == 2 && (bool)Get(result, "UiSlot") && (int)Get(result, "ItemType") == 0, "fresh empty slot wins over frozen world target");
                foreach (int context in new[] { 22, 41, 42 })
                {
                    var sample = new Item(); sample.SetDefaults(9); sample.stack = 123;
                    Call(gesture, "Request", 1); Call(gesture, "BeginPass"); Call(gesture, "Hover", new[] { sample }, context, 0);
                    Call(gesture, "BeforeWorldHover"); Call(gesture, "AfterResources", true); Call(gesture, "EndPass", true); Call(gesture, "Update", 3L);
                    Require((int)Get(result, "ItemType") == 0, "craft sample context never becomes possessed stack " + context);
                }
                int previous = delivered; Call(gesture, "Request", 1); Call(gesture, "BeginPass"); Call(gesture, "EndPass", true); Call(gesture, "Update", 9L);
                Require(delivered == previous && !(bool)Get(gesture, "Busy"), "UI pass missing concrete hover boundary times out without fallback");
                Call(gesture, "BeginPick"); Main.tile = new Tile[64, 64]; Call(gesture, "Update", 10L);
                Require(!(bool)Get(gesture, "Picking"), "waiting-for-click session replacement cancels old selection");
                Call(gesture, "Request", 1); uiPass(false); Set(gesture, "CanPick", (Func<bool>)(() => false)); Call(gesture, "Update", 11L);
                Require(delivered == previous, "modal takeover before delivery vetoes frozen target"); Set(gesture, "CanPick", (Func<bool>)(() => true));
                var goldChest = new Tile { type = 21, frameX = 36, frameY = 0 }; goldChest.active(true); goldChest.fullbrightBlock(true); Main.tile[1, 1] = goldChest;
                Call(gesture, "Request", 1); uiPass(false); Call(gesture, "Update", 12L);
                Require((bool)Get(gesture, "Busy") && (long)Get(coldCatalog, "PlacementReads") == 64, "cold gesture does not synchronously scan every item");
                Set(gesture, "CanPick", (Func<bool>)(() => false)); Call(gesture, "Update", 13L);
                long cancelledReads = (long)Get(coldCatalog, "PlacementReads");
                for (int i = 14; i < 24; i++) Call(gesture, "Update", (long)i);
                Require(!(bool)Get(gesture, "Busy") && (long)Get(coldCatalog, "PlacementReads") == cancelledReads, "cancelled cold gesture stops placement reads");
                Set(gesture, "CanPick", (Func<bool>)(() => true));
                Call(gesture, "Request", 1); uiPass(false);
                int beforeCold = delivered;
                for (int update = 24; update < 180 && (bool)Get(gesture, "Busy"); update++)
                { long reads = (long)Get(coldCatalog, "PlacementReads"); Call(gesture, "Update", (long)update); Require((long)Get(coldCatalog, "PlacementReads") - reads <= 64, "cold placement capture is bounded to 64 metadata entries per update"); }
                Require(delivered == beforeCold + 1 && (int)Get(result, "ItemType") == Terraria.ID.ItemID.GoldChest,
                    "first world query resolves the actual gold chest style without opening the browser; delivered=" + (delivered - beforeCold) + "; type=" + Get(result, "ItemType") + "; reads=" + Get(coldCatalog, "PlacementReads") + "; mouse=" + Main.MouseWorld);
                Require(GetOptional(coldCatalog, "Catalog") == null && (long)Get(coldCatalog, "ItemReads") == 0 && (long)Get(coldCatalog, "RecipeReads") == 0,
                    "cold world query prepares only placement metadata, never the full catalog or recipe families");
                long completedReads = (long)Get(coldCatalog, "PlacementReads"); for (int i = 200; i < 320; i++) Call(gesture, "Update", (long)i);
                Require((long)Get(coldCatalog, "PlacementReads") == completedReads, "completed cold gesture has zero continuing placement work");
                Call(coldCatalog, "Reset"); var coldWall = new Tile { wall = 4 }; coldWall.fullbrightWall(true); Main.tile[1, 1] = coldWall;
                int announced = 0; object wallAnnouncement = null;
                Set(gesture, "Announced", Adapt(type.GetProperty("Announced", Flags).PropertyType, value => { announced++; wallAnnouncement = value; }));
                Call(gesture, "Request", 2); uiPass(false); long coldTick = (long)Get(gesture, "tick");
                for (int i = 0; i < 180 && (bool)Get(gesture, "Busy"); i++) Call(gesture, "Update", ++coldTick);
                Require(announced == 1 && (int)Get(wallAnnouncement, "ItemType") == (int)Call(catalog, "WallItem", (ushort)4), "cold announcement resolves a visible wall without a directory warm-up");
                Call(coldCatalog, "Reset"); Main.tile[1, 1] = goldChest;
                Call(gesture, "Request", 1); uiPass(false); Call(gesture, "Update", ++coldTick); goldChest.frameX = 72;
                int beforeChanged = delivered;
                for (int i = 0; i < 180 && (bool)Get(gesture, "Busy"); i++) Call(gesture, "Update", ++coldTick);
                Require(delivered == beforeChanged, "a changed furniture style cancels the frozen cold query instead of substituting another item");
                var displayOrigin = new Terraria.DataStructures.Point16(1, 1);
                var display = new Terraria.GameContent.Tile_Entities.TEItemFrame { Position = displayOrigin, item = new Item() };
                display.item.SetDefaults(4);
                Terraria.DataStructures.TileEntity priorEntity; bool hadEntity = Terraria.DataStructures.TileEntity.ByPosition.TryGetValue(displayOrigin, out priorEntity);
                bool frameFlag = Main.tileFrameImportant[395]; Main.tileFrameImportant[395] = true;
                try
                {
                    Terraria.DataStructures.TileEntity.ByPosition[displayOrigin] = display;
                    for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++)
                    { var tile = new Tile { type = 395, frameX = (short)(18 * x), frameY = (short)(18 * y) }; tile.active(true); tile.fullbrightBlock(true); Main.tile[1 + x, 1 + y] = tile; }
                    foreach (bool changed in new[] { true, false })
                    {
                        Call(coldCatalog, "Reset"); int before = announced; long reads = (long)Get(coldCatalog, "PlacementReads");
                        Call(gesture, "Request", 2); uiPass(false); Call(gesture, "Update", ++coldTick);
                        Require((bool)Get(gesture, "Busy") && (long)Get(coldCatalog, "PlacementReads") - reads == 64, "carrier cold observation waits for bounded placement metadata");
                        if (changed) display.item.SetDefaults(8);
                        for (int i = 0; i < 180 && (bool)Get(gesture, "Busy"); i++) Call(gesture, "Update", ++coldTick);
                        Require(!(bool)Get(gesture, "Busy") && announced == before + (changed ? 0 : 1), "real cold gesture cancels changed contents and delivers unchanged contents");
                        if (!changed) NativeAnnouncementIconChecks.AssertMessage(JueMingR.Features.Announcements.SafeChatText.Build((List<string>)Get(wallAnnouncement, "Entries")), "这里有 放在" + Lang.GetItemNameValue(3270) + " 的1 个 " + Lang.GetItemNameValue(8) + " ", 3270, 8);
                        long stopped = (long)Get(coldCatalog, "PlacementReads"); Call(gesture, "Update", ++coldTick);
                        Require((long)Get(coldCatalog, "PlacementReads") == stopped, "finished carrier gesture stops metadata reads");
                    }
                    var jar = new Terraria.GameContent.Tile_Entities.TEDeadCellsDisplayJar { Position = displayOrigin, item = new Item() }; jar.item.SetDefaults(4);
                    Terraria.DataStructures.TileEntity.ByPosition[displayOrigin] = jar;
                    for (int y = 0; y < 2; y++) { var tile = new Tile { type = 698, frameY = (short)(18 * y) }; tile.active(true); tile.fullbrightBlock(true); Main.tile[1, 1 + y] = tile; }
                    Main.mouseY = 36; PlayerInput.MouseInfo = new MouseState(20, 36, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                    Call(coldCatalog, "Reset"); int beforeCoating = announced;
                    Call(gesture, "Request", 2); uiPass(false); Call(gesture, "Update", ++coldTick);
                    Require((bool)Get(gesture, "Busy"), "jar lower-cell cold gesture waits for body identity");
                    Main.tile[1, 1].invisibleBlock(true);
                    for (int i = 0; i < 180 && (bool)Get(gesture, "Busy"); i++) Call(gesture, "Update", ++coldTick);
                    Require(announced == beforeCoating && !(bool)Get(gesture, "Busy"), "coating the real jar draw anchor cancels a pending lower-cell gesture");
                    Main.mouseY = 20; PlayerInput.MouseInfo = new MouseState(20, 20, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                }
                finally { Main.tileFrameImportant[395] = frameFlag; if (hadEntity) Terraria.DataStructures.TileEntity.ByPosition[displayOrigin] = priorEntity; else Terraria.DataStructures.TileEntity.ByPosition.Remove(displayOrigin); }
                // Actual original slot methods are intercepted before transfer.
                var held = new Item(); held.SetDefaults(9); held.stack = 17; var inventory = new[] { held }; Main.mouseItem = new Item();
                Set(input, "ReadOnlyMouseMask", 3); Main.mouseLeft = Main.mouseRight = Main.mouseLeftRelease = Main.mouseRightRelease = true;
                Terraria.UI.ItemSlot.LeftClick(inventory, 0, 0); Terraria.UI.ItemSlot.RightClick(inventory, 0, 0);
                Require(ReferenceEquals(inventory[0], held) && held.stack == 17 && Main.mouseItem.IsAir, "actual ItemSlot click outlets leave all items untouched");
                Set(input, "ReadOnlyMouseMask", 0);
                for (int mouse = 0; mouse < 5; mouse++)
                {
                    int bit = 1 << mouse; Set(input, "ClaimsReadOnlyGesture", (Func<int, int, int>)((down, press) => press));
                    PlayerInput.MouseInfo = Mouse(mouse); var tokens = new List<string> { "Mouse" + (mouse + 1), "K" };
                    Call(input, "AfterNativeMouse", tokens);
                    Require(tokens.Count == 1 && tokens[0] == "K" && ((int)Get(input, "ReadOnlyMouseMask") & bit) != 0, "five-button mapping consumes only owned mouse token");
                    PlayerInput.MouseInfo = new MouseState(); Call(input, "AfterNativeMouse", tokens);
                    Require(((int)Get(input, "ReadOnlyMouseMask") & bit) != 0, "mouse release remains owned");
                    Call(input, "AfterNativeMouse", tokens); Require((int)Get(input, "ReadOnlyMouseMask") == 0, "release tail retires exactly once");
                }
                Console.WriteLine("PASS: fixed hook installation, fresh UI/empty-slot boundary, timeout/session/modal cancellation, actual ItemSlot safety and all five mapping tails.");
            }
            finally { ((IDisposable)gesture).Dispose(); ((IDisposable)coldCatalog).Dispose(); Main.mouseLeft = Main.mouseRight = false; }
        }
        private static Delegate Adapt(Type type, Action<object> callback)
        { var value = Expression.Parameter(type.GetGenericArguments()[0]); return Expression.Lambda(type, Expression.Invoke(Expression.Constant(callback), Expression.Convert(value, typeof(object))), value).Compile(); }
        private static MouseState Mouse(int button)
        { return new MouseState(20, 20, 0, button == 0 ? ButtonState.Pressed : ButtonState.Released, button == 2 ? ButtonState.Pressed : ButtonState.Released, button == 1 ? ButtonState.Pressed : ButtonState.Released, button == 3 ? ButtonState.Pressed : ButtonState.Released, button == 4 ? ButtonState.Pressed : ButtonState.Released); }
    }
}
