using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using HarmonyLib;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // R1 direct-owner competition. Native UI receipts/terminal display sinks
    // are stated separately from real owner/dispatch/ItemCheck consumers.
    internal static class NativeTargetOwnerChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private static bool Sink() { return false; }
        private static bool MinusOne(ref int __result) { __result = -1; return false; }
        private static bool False(ref bool __result) { __result = false; return false; }
        private static bool Hitbox(ref Rectangle __result) { __result = new Rectangle(0, 0, 24, 24); return false; }
        private static int sent;
        private static bool Chat() { sent++; return false; }
        private static ReLogic.Content.Asset<Texture2D> Texture(int width, int height)
        {
            var texture = (Texture2D)FormatterServices.GetUninitializedObject(typeof(Texture2D)); GC.SuppressFinalize(texture);
            typeof(Texture2D).GetField("_width", F).SetValue(texture, width); typeof(Texture2D).GetField("_height", F).SetValue(texture, height);
            var asset = (ReLogic.Content.Asset<Texture2D>)Activator.CreateInstance(typeof(ReLogic.Content.Asset<Texture2D>), F, null, new object[] { "owner-cpu" }, null);
            asset.GetType().GetMethod("SubmitLoadedContent", F).Invoke(asset, new object[] { texture, new ReLogic.Content.Sources.FileSystemContentSource(Terraria.Program.SavePath) }); return asset;
        }
        internal static void Buttons(object context, Func<int> outlets)
        {
            using (var run = new Replay(context, outlets))
            {
                var isolation = new Harmony("JueMingR.Tests.NativeInventoryOwner");
                var native = typeof(Main).GetMethod("DrawInventory", F);
                var oldBatch = Main.spriteBatch; var back = TextureAssets.InventoryBack; var on = TextureAssets.InventoryTickOn; var off = TextureAssets.InventoryTickOff;
                var cartOn = TextureAssets.Extra[255]; var cartOff = TextureAssets.Extra[256];
                var misc = (Item[])Main.LocalPlayer.miscEquips.Clone(); var armor = (Item[])Main.LocalPlayer.armor.Clone();
                var hideMisc = Main.LocalPlayer.hideMisc; var hideArmor = (bool[])Main.LocalPlayer.hideVisibleAccessory.Clone();
                bool unlocked = Main.LocalPlayer.unlockedSuperCart, super = Main.LocalPlayer.enabledSuperCart; int equip = Main.EquipPage;
                try
                {
                    TextureAssets.InventoryBack = Texture(52, 52); TextureAssets.InventoryTickOn = Texture(14, 14); TextureAssets.InventoryTickOff = Texture(14, 14);
                    TextureAssets.Extra[255] = Texture(14, 14); TextureAssets.Extra[256] = Texture(14, 14);
                    Main.spriteBatch = (SpriteBatch)FormatterServices.GetUninitializedObject(typeof(SpriteBatch)); GC.SuppressFinalize(Main.spriteBatch);
                    foreach (var method in typeof(SpriteBatch).GetMethods().Where(m => m.Name == "Draw")) Patch(isolation, method, nameof(Sink));
                    foreach (var method in typeof(Main).GetMethods(F).Where(m => m.Name == "DrawLoadoutButtons" || m.Name == "DrawDefenseCounter")) Patch(isolation, method, nameof(Sink));
                    Patch(isolation, typeof(Main).GetMethod("DrawPageIcons", F), nameof(MinusOne));
                    Patch(isolation, typeof(Main).GetProperty("HasSpaceForControllerAchievementAdvisor", F).GetGetMethod(true), nameof(False));
                    Patch(isolation, typeof(Terraria.UI.ItemSlot).GetMethod("Draw", F, null, new[] { typeof(SpriteBatch), typeof(Item[]), typeof(int), typeof(int), typeof(Vector2), typeof(Color) }, null), nameof(Sink));
                    isolation.Patch(native, transpiler: new HarmonyMethod(typeof(NativeTargetOwnerChecks).GetMethod(nameof(InventoryScope), F)));
                    object receiver = FormatterServices.GetUninitializedObject(typeof(Main)); GC.SuppressFinalize(receiver);
                    Main.playerInventory = true; Main.mouseItem = new Item(); Main.LocalPlayer.unlockedSuperCart = true;
                    foreach (int index in new[] { 0, 1, 2 }) { Main.LocalPlayer.miscEquips[index] = new Item(); Main.LocalPlayer.miscEquips[index].SetDefaults(9); }
                    Main.LocalPlayer.armor[3] = new Item(); Main.LocalPlayer.armor[3].SetDefaults(9);
                    Action draw = () =>
                    {
                        Main.inventoryScale = .85f; Call(run.Targets, "BeginPass"); native.Invoke(receiver, null);
                        Call(run.Targets, "BeforeWorldHover"); Call(run.Targets, "AfterResources", true); Call(run.Targets, "EndPass", true);
                    };
                    foreach (string family in new[] { "pet", "light", "supercart", "accessory", "pet-paused" })
                    foreach (bool query in new[] { false, true })
                    {
                        Main.EquipPage = family == "accessory" ? 0 : 2; Main.gamePaused = family == "pet-paused";
                        int index = family == "light" ? 1 : family == "supercart" ? 2 : family == "accessory" ? 3 : 0;
                        run.Bind(query); run.Point = new Vector2(10, 600); run.Neutral(); draw();
                        var snapshot = Get(run.Targets, "snapshot"); var entries = ((Array)Get(snapshot, "committed")).Cast<object>().Take((int)Get(snapshot, "committedCount")).Where(e => (bool)Get(e, "NativeControl")).ToArray();
                        Require(entries.Length == (family == "accessory" ? 5 : 3), "actual scoped DrawInventory reaches every expected native button producer: " + family);
                        var rect = (Vector4)Get(entries[family == "accessory" ? 0 : index], "Rect");
                        run.Point = new Vector2(rect.X + 2, rect.Y + 2); run.Neutral(); draw();
                        bool before = Flag(family, index); int count = outlets(); run.Frame(true, run.ChordKeys(query));
                        Require((bool)Call(run.Targets, "NativeControlOwnsPointer") && (int)Get(run.Input, "ReadOnlyMouseMask") == 0 && Main.mouseLeft,
                            "fresh native button ownership preserves real click instead of target claim: " + family);
                        run.NoUse(family); draw(); run.Update();
                        Require(Flag(family, index) != before && outlets() == count && !(bool)Get(run.Targets, "Busy"), "native button command wins without query/announcement: " + family + "/" + query);
                        run.Neutral(); draw();
                        // Same displayed slot, outside the tiny overlay: prove
                        // the fix retains legitimate query/announcement ability.
                        run.Point = new Vector2(rect.X - 25, rect.Y + 12); run.Neutral(); draw(); count = outlets(); Call(Get(Get(run.Browser, "Announcements"), "cooldown"), "Clear");
                        run.Frame(true, run.ChordKeys(query)); run.NoUse(family + " adjacent"); draw(); run.Update();
                        Require(!(bool)Get(run.Targets, "Busy") && (query ? (int)Get(Get(Get(run.Browser, "Knowledge"), "Workspace"), "Selected") == 9 : outlets() == count + 1), "adjacent native item slot remains usable: " + family + "/" + query);
                        run.Neutral(); Call(run.Shell, "CloseAndSubmitPosition");
                        Require((int)Get(run.Input, "ReadOnlyMouseMask") == 0, "native button/adjacent gesture release restores target tail");
                    }
                    Main.gamePaused = false; Main.EquipPage = 2; run.Point = new Vector2(10, 600); run.Neutral(); draw();
                    var controls = ((Array)Get(Get(run.Targets, "snapshot"), "committed")).Cast<object>().Take((int)Get(Get(run.Targets, "snapshot"), "committedCount")).Where(e => (bool)Get(e, "NativeControl")).ToArray();
                    Vector4 firstRect = (Vector4)Get(controls[0], "Rect"); run.Point = new Vector2(firstRect.X + 2, firstRect.Y + 2); run.Neutral();
                    Require(ReferenceEquals(GetOptional(run.Shell, "nativeControlPlayer"), Main.LocalPlayer) && Main.LocalPlayer.mouseInterface, "committed real button creates one captured-player native UI lease");
                    Require(!(bool)Get(run.Shell, "CanAutomaticMouseInterface"), "native button lease cannot be relaxed as Shell's own automatic hover permission");
                    Call(run.Shell, "EndPointerLayer"); Require(GetOptional(run.Shell, "nativeControlPlayer") == null && !Main.LocalPlayer.mouseInterface, "native control lease retires at pointer-finally even without another native draw");
                    Main.LocalPlayer.mouseInterface = true; Call(run.Shell, "ProcessInput"); Call(run.Shell, "EndPointerLayer"); Require(Main.LocalPlayer.mouseInterface, "restoring native lease preserves a pre-existing foreign mouseInterface owner"); Main.LocalPlayer.mouseInterface = false;
                    run.Frame(false); Set(run.Input, "foregroundWindow", (Func<IntPtr>)(() => IntPtr.Zero)); run.Frame(false);
                    Require(GetOptional(run.Shell, "nativeControlPlayer") == null && !Main.LocalPlayer.mouseInterface, "unfocused sample retires native control lease without pretending a physical release");
                    Set(run.Input, "foregroundWindow", (Func<IntPtr>)(() => new IntPtr(1))); run.Neutral();
                    // Start the real page picker while the old native-button
                    // press is held; its actual neutral occurs over that owner.
                    run.Frame(true); var page = Get(run.Browser, "page");
                    var part = Activator.CreateInstance(page.GetType().Assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.BrowserPart"), true); Set(part, "Command", 7); Call(page, "Execute", part, false);
                    Require((bool)Get(run.Targets, "Picking") && !(bool)Get(run.Targets, "released"), "real page entry waits for release of the held native-button press");
                    run.Frame(false); Require((bool)Get(run.Targets, "released"), "trusted physical neutral over native button releases picker independently of pointer owner");
                    run.Point = new Vector2(firstRect.X - 25, firstRect.Y + 12); run.Frame(true); run.NoUse("page picker after native-button release"); draw(); run.Update();
                    Require(!(bool)Get(run.Targets, "Busy") && (int)Get(Get(Get(run.Browser, "Knowledge"), "Workspace"), "Selected") == 9, "first adjacent slot press after actual button neutral completes real page query");
                    Console.WriteLine("CHECK R1 picker release: held native button -> real page pick -> physical neutral on button -> adjacent fresh click -> query item9; native use=0");
                    run.Neutral(); Call(run.Shell, "CloseAndSubmitPosition"); run.Point = new Vector2(firstRect.X + 2, firstRect.Y + 2); run.Neutral();
                    var position = GetOptional(run.State, "positionProjection");
                    Call(run.State, "RestorePosition", new JueMingR.Platform.Settings.WindowPosition(350, 50)); Call(run.State, "RestoreVisible"); run.Neutral();
                    Require((bool)Get(run.Shell, "TargetPointerOwned") && !(bool)Call(run.Targets, "NativeControlOwnsPointer") && GetOptional(run.Shell, "nativeControlPlayer") == null && (bool)Get(run.Shell, "CanAutomaticMouseInterface"), "higher fresh F5 window over a native button retains its own hover lease instead of inheriting underlying native ownership");
                    Call(run.Shell, "CloseAndSubmitPosition"); Set(run.State, "positionProjection", position); run.Neutral();
                    var world = Main.ActiveWorldFileData; Main.ActiveWorldFileData = null; run.Frame(false);
                    Require(!(bool)Call(run.Targets, "NativeControlOwnsPointer") && GetOptional(run.Shell, "nativeControlPlayer") == null, "world identity replacement cannot inherit native button receipt/lease"); Main.ActiveWorldFileData = world; run.Point = new Vector2(10, 600); run.Neutral();
                    run.Save("item-browser.query", null); run.Save("announcement.send", null); Require(!(bool)Call(run.Targets, "NativeControlOwnsPointer"), "no enabled binding/picking/pending demand bypasses native-control layout reads"); run.Bind(false);
                    run.OrdinaryUse();
                    Console.WriteLine("PASS R1 native buttons L2: actual scoped DrawInventory pet/light/supercart/accessory business and all three production drawing callsites; query+announcement negatives, adjacent slots positive, real ItemCheck zero, trusted release and ordinary ItemCheck positive. Prologue/other inventory sections and terminal slot/sprite cosmetics are isolated; full Draw/GPU NOT RUN.");
                }
                finally
                {
                    Main.gamePaused = false; isolation.UnpatchAll(isolation.Id); Main.spriteBatch = oldBatch; TextureAssets.InventoryBack = back; TextureAssets.InventoryTickOn = on; TextureAssets.InventoryTickOff = off;
                    TextureAssets.Extra[255] = cartOn; TextureAssets.Extra[256] = cartOff; Array.Copy(misc, Main.LocalPlayer.miscEquips, misc.Length); Array.Copy(armor, Main.LocalPlayer.armor, armor.Length);
                    Main.LocalPlayer.hideMisc = hideMisc; Array.Copy(hideArmor, Main.LocalPlayer.hideVisibleAccessory, hideArmor.Length); Main.LocalPlayer.unlockedSuperCart = unlocked; Main.LocalPlayer.enabledSuperCart = super; Main.EquipPage = equip;
                }
            }
        }
        private static bool Flag(string family, int index) { return family == "accessory" ? Main.LocalPlayer.hideVisibleAccessory[index] : family == "supercart" ? Main.LocalPlayer.enabledSuperCart : Main.LocalPlayer.hideMisc[index]; }
        internal static void SharedOwners(object context)
        {
            var back = TextureAssets.InventoryBack; var pixel = TextureAssets.MagicPixel;
            TextureAssets.InventoryBack = Texture(52, 52); TextureAssets.MagicPixel = Texture(1, 1);
            var chat = new Harmony("JueMingR.Tests.OwnerChat"); sent = 0;
            Patch(chat, typeof(Terraria.Chat.ChatHelper).GetMethod("SendChatMessageFromClient", F), nameof(Chat));
            Patch(chat, typeof(Terraria.Chat.ChatCommandProcessor).GetMethod("ProcessIncomingMessage", F), nameof(Chat));
            try
            {
                using (var run = new Replay(context, () => sent))
                {
                    var renderer = Get(run.Shell, "renderer"); var layout = Get(run.State, "Layout");
                    Action prepare = () => { Call(renderer, "RefreshResources"); Call(renderer, "Prepare", run.State, 960f, 760f, 1f); };
                    Call(run.State, "Navigate", 9); Call(run.State, "RestoreVisible"); run.Neutral(); prepare();
                    object nav = Call(layout, "Navigation", 1); run.Point = LocalPoint(run.State, nav); run.Neutral();
                    run.Frame(true, run.ChordKeys()); run.NoUse("F5 actual navigation button"); run.Frame(false, run.ChordKeys());
                    Require((int)Get(run.State, "Page") == 1 && sent == 0 && !(bool)Get(run.Targets, "Busy"), "announcement chord preserves real F5 button navigation and emits no declaration");
                    run.Neutral(); prepare();
                    var viewport = Get(layout, "Viewport");
                    run.Point = new Vector2((float)Get(run.State, "X") + (float)Get(viewport, "Right") - 2, (float)Get(run.State, "Y") + (float)Get(viewport, "Bottom") - 2);
                    run.Neutral(); run.Frame(true, run.ChordKeys()); run.NoUse("F5 body whitespace"); run.Frame(false, run.ChordKeys());
                    Require((int)Get(run.State, "Page") == 1 && sent == 0 && !(bool)Get(run.Targets, "Busy"), "F5 body owns announcement primary without command or world fallback");
                    run.Neutral(); run.Point = LocalPoint(run.State, Get(layout, "Title")); run.Frame(true, run.ChordKeys());
                    Require((int)Get(run.State, "capture") == 1, "actual title starts capture with announcement chord");
                    run.Point += new Vector2(20, 15); run.Frame(true, run.ChordKeys()); run.NoUse("F5 title captured tail");
                    run.Point = new Vector2(950, 750); run.Frame(true, run.ChordKeys()); run.NoUse("F5 title held outside"); run.Frame(false, run.ChordKeys());
                    Require((int)Get(run.State, "capture") == 0 && sent == 0 && !(bool)Get(run.Targets, "Busy"), "title capture owns complete tail outside its former window"); run.Neutral();

                    // Actual popup entry and category command, no fake modal flag.
                    Call(run.State, "Navigate", 1); Call(run.State, "RestoreVisible"); prepare(); Call(run.Shell, "PrepareMisc", true, Matrix.Identity, new Vector2(960, 760), true);
                    var misc = Get(run.Shell, "MiscUi");
                    var config = NativeToolsUiChecks.Controls(misc).Single(c => Get(c, "Command").ToString() == "Tools" && (int)Get(c, "Argument") == 3);
                    run.Point = NativeToolsUiChecks.Point(Get(config, "Rect")); run.Neutral(); run.Frame(true, run.ChordKeys()); run.NoUse("F5 capture configuration button"); run.Frame(false, run.ChordKeys());
                    var popup = Get(run.Shell, "CaptureUi"); Require((bool)Get(popup, "Visible"), "real configuration button opens actual capture popup under announcement chord");
                    Call(popup, "Prepare", Matrix.Identity, new Vector2(960, 760), true);
                    var setting = ((JueMingR.Features.Tools.ToolSettings[])Get(Get(context, "Tools"), "Settings"))[0]; int categories = setting.Value.Categories; long command = setting.AcceptedCommandId;
                    run.Point = NativeToolsUiChecks.Point(((Array)Get(popup, "cells")).GetValue(0)); run.Neutral(); run.Frame(true, run.ChordKeys()); run.NoUse("actual capture popup category"); run.Frame(false, run.ChordKeys());
                    NativeQuickItemChecks.Until(() => { Call(Get(context, "Tools"), "Poll"); return !setting.Busy; });
                    Console.WriteLine("R1 popup command before=" + command + " after=" + setting.AcceptedCommandId + " categories=" + categories + "->" + setting.Value.Categories + " sent=" + sent + " targetBusy=" + Get(run.Targets, "Busy") + " visible=" + Get(popup, "Visible") + " armed=" + Get(popup, "armed") + " point=" + run.Point);
                    Require(setting.AcceptedCommandId == command + 1 && setting.Value.Categories == (categories ^ 1) && sent == 0 && !(bool)Get(run.Targets, "Busy"), "actual popup commits exactly its category command, never declaration or native use");
                    Call(popup, "Close"); run.Neutral(); Require(setting.Set(setting.Value.WithCategories(categories)), "restore isolated popup preference"); NativeQuickItemChecks.Until(() => { Call(Get(context, "Tools"), "Poll"); return !setting.Busy; });
                    Console.WriteLine("CHECK R1 F5 announcement chord: navigation/body/title outside capture/real Capture popup command; declarations=0 actual ItemCheck use=0");

                    // A waiting declaration cannot lend its frozen proof to a
                    // later F5 navigation command or replace/renew its request.
                    Call(run.State, "Navigate", 9); Call(run.State, "RestoreVisible"); run.Point = new Vector2(30, 328); run.Neutral(); prepare(); run.Pass(); run.Frame(true, run.ChordKeys());
                    object first = Get(run.Targets, "frozen"); long expires = (long)Get(run.Targets, "expires"); Require(first != null && (bool)Get(run.Targets, "Busy"), "owner test starts real pending declaration");
                    run.Frame(false, run.ChordKeys()); run.Point = LocalPoint(run.State, Call(layout, "Navigation", 1)); run.Frame(true, run.ChordKeys()); run.NoUse("pending F5 owner competition");
                    Require(ReferenceEquals(first, Get(run.Targets, "frozen")) && expires == (long)Get(run.Targets, "expires"), "F5 owner repeat cannot replace the first target/deadline"); run.Frame(false, run.ChordKeys()); run.Pass(); run.Update();
                    Require(sent == 0 && !(bool)Get(run.Targets, "Busy") && (int)Get(run.State, "Page") == 1, "changed F5 context retires old target proof while preserving navigation command"); run.Neutral(); Call(run.Shell, "CloseAndSubmitPosition");
                    ColdOwner(run); MapOwner(run); BlueprintOwner(run);
                    run.Point = new Vector2(10, 600); run.Neutral(); run.Frame(false, Keys.W); run.Frame(false, Keys.W);
                    Require(PlayerInput.Triggers.Current.Up, "cached native W resumes after owner release");
                    run.Frame(false, Keys.W, Keys.F5); Require((bool)Get(run.State, "Visible"), "fresh F5 restores after all owner/chord tails"); run.Frame(false, Keys.W, Keys.F5); Require((bool)Get(run.State, "Visible"), "held F5 cannot replay"); run.Neutral(); Call(run.Shell, "CloseAndSubmitPosition");
                    run.Point = new Vector2(30, 328); run.Neutral(); run.Pass(); Call(Get(Get(run.Browser, "Announcements"), "cooldown"), "Clear"); run.Frame(true, run.ChordKeys()); run.NoUse("restored ordinary declaration"); run.Pass(); run.Update(); Require(sent == 1, "after all owners release real announcement reaches intercepted chat exactly once"); run.Neutral(); run.OrdinaryUse();
                    Console.WriteLine("PASS R1 competing owners L2: announcement chord with real F5 button/body/title capture+outside tail/actual Capture popup; actual FootprintMapLayer and BlueprintToolbar owner/command chains, pending repeat and proof isolation; real ItemCheck zero and ordinary click/W/F5 recovery. Map Layout/Completed and toolbar font/skin/layout/clip/presented receipts are explicit controlled preconditions, not Draw/GPU producer evidence.");
                }
            }
            finally { chat.UnpatchAll(chat.Id); TextureAssets.InventoryBack = back; TextureAssets.MagicPixel = pixel; }
        }
        private static void ColdOwner(Replay run)
        {
            var catalog = Get(Get(run.Browser, "Knowledge"), "Native"); var popup = Get(run.Shell, "CaptureUi"); var tile = Main.tile[1, 20]; var drop = Main.item[0];
            try
            {
                Call(run.State, "Navigate", 1); Call(run.State, "RestoreVisible"); run.Point = new Vector2(30, 328); run.Neutral();
                Main.item[0] = null; var chest = new Tile { type = 21, frameX = 36 }; chest.active(true); chest.fullbrightBlock(true); Main.tile[1, 20] = chest; Call(catalog, "Reset");
                run.Pass(); run.Frame(true, run.ChordKeys()); run.Pass(); run.Update(); Require((bool)Get(run.Targets, "completed") && (bool)Get(run.Targets, "preparingPlacement"), "higher-owner representative has already accepted world proof and entered actual cold preparation");
                long reads = (long)Get(catalog, "PlacementReads");
                object anchor = Activator.CreateInstance(run.Shell.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Rect"), F, null, new object[] { 0f, 300f, 1f, 1f }, null);
                Call(popup, "Open", anchor); Call(popup, "Prepare", Matrix.Identity, new Vector2(960, 760), true);
                Require((bool)Call(popup, "Contains", run.Point.X, run.Point.Y), "real popup geometry covers the exact already-proven world point");
                run.Frame(false, run.ChordKeys()); run.Update(); Require(!(bool)Get(run.Targets, "Busy") && sent == 0 && (long)Get(catalog, "PlacementReads") == reads, "higher actual owner retires completed cold proof before another prepare budget or output");
                Console.WriteLine("CHECK R1 cold higher owner: completed=true/preparing=true -> actual Capture popup overlaps same point -> cancelled; extra placement reads=0 declarations=0");
            }
            finally { Call(popup, "Close"); Call(run.Targets, "Cancel", (object)null); Main.tile[1, 20] = tile; Main.item[0] = drop; run.Neutral(); Call(run.Shell, "CloseAndSubmitPosition"); }
        }
        private static Vector2 LocalPoint(object state, object rect)
        { return new Vector2((float)Get(state, "X") + (float)Get(rect, "X") + 5, (float)Get(state, "Y") + (float)Get(rect, "Y") + 5); }
        private static void MapOwner(Replay run)
        {
            var host = Get(run.Context, "Footprints"); var layer = Get(host, "Layer"); bool display = (bool)Get(host, "Display");
            try
            {
                NativeQuickItemChecks.Until(() => { Call(run.Context, "UpdateRuntime"); return (bool)Get(host, "ControlsEnabled"); }); Call(host, "SetDisplay", true); Main.mapFullscreen = true;
                object frame = Activator.CreateInstance(host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Map.MapView"), F, null, new object[] { Vector2.Zero, Vector2.Zero, 1f, 1f, 255 }, null);
                Call(layer, "Layout", frame, FontAssets.MouseText.Value); Set(layer, "drewBar", true); Call(layer, "Completed", frame);
                var rect = Get(layer, "speed"); run.Point = NativeToolsUiChecks.Point(rect); run.Neutral(); var playback = Get(layer, "Playback"); int speed = (int)Get(playback, "Speed");
                run.Frame(true, run.ChordKeys()); run.NoUse("actual map timeline owner"); Require((bool)Get(run.Input, "MapPointerOwned") && (int)Get(run.Input, "ReadOnlyMouseMask") == 0, "actual registered map owner wins before read-only claim");
                run.Frame(false, run.ChordKeys()); Require((int)Get(playback, "Speed") != speed && sent == 0 && !(bool)Get(run.Targets, "Busy"), "map speed command receives chord primary without declaration"); run.Neutral();
                Main.mapFullscreen = false; run.Point = new Vector2(30, 328); run.Neutral(); run.Pass(); run.Frame(true, run.ChordKeys()); object first = Get(run.Targets, "frozen"); long expires = (long)Get(run.Targets, "expires"); run.Frame(false, run.ChordKeys());
                Main.mapFullscreen = true; Call(layer, "Completed", frame); run.Point = NativeToolsUiChecks.Point(rect); run.Frame(true, run.ChordKeys()); run.NoUse("map owner versus pending repeat");
                Require(ReferenceEquals(first, Get(run.Targets, "frozen")) && expires == (long)Get(run.Targets, "expires") && (int)Get(run.Input, "ReadOnlyMouseMask") == 0, "real map claim does not borrow/replace pending target receipt"); run.Frame(false, run.ChordKeys()); run.Pass(); run.Update(); Require(sent == 0 && !(bool)Get(run.Targets, "Busy"), "map ownership cannot certify the prior world request"); run.Neutral();
            }
            finally { Main.mapFullscreen = false; Call(host, "SetDisplay", display); Call(layer, "Invalidate"); run.Neutral(); }
        }
        private static void BlueprintOwner(Replay run)
        {
            var host = Get(run.Context, "Blueprint"); var toolbar = Get(host, "Toolbar"); var state = Get(host, "State"); var layout = Get(toolbar, "Layout"); var p = Main.LocalPlayer;
            bool enabled = (bool)Get(host, "ToolbarEnabled"); Item item = p.inventory[4]; var back = TextureAssets.InventoryBack; var pixel = TextureAssets.MagicPixel;
            try
            {
                NativeQuickItemChecks.Until(() => { Call(run.Context, "UpdateRuntime"); return (bool)Get(host, "ControlsEnabled"); }); Call(host, "SetToolbar", true);
                p.inventory[4] = new Item(); p.inventory[4].SetDefaults(Terraria.ID.ItemID.Gel); p.inventory[4].stack = 3; p.selectedItemState.Select(4); p.selectedItemState.Update();
                TextureAssets.InventoryBack = Texture(52, 52); TextureAssets.MagicPixel = Texture(1, 1);
                Set(toolbar, "font", FontAssets.MouseText.Value); Set(toolbar, "skin", TextureAssets.InventoryBack.Value); Set(toolbar, "pixel", TextureAssets.MagicPixel.Value); Set(toolbar, "drawnMatrix", Matrix.Identity);
                Call(layout, "SetPreviewRevision", Get(host, "TrimPreviewRevision")); Call(layout, "SetPlacedListVisible", false);
                var measure = Get(toolbar, "measure"); Call(layout, "EnsureProjection", 960f, 760f, 1f, FontAssets.MouseText.Value, measure, false, 0);
                object clip = Activator.CreateInstance(layout.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Rect"), F, null, new object[] { 0f, 0f, 960f, 760f }, null); Set(toolbar, "visibleClip", clip); Set(toolbar, "presented", true);
                var panel = Get(layout, "Panel"); object button = ((Array)Get(layout, "Buttons")).GetValue(0); run.Point = NativeToolsUiChecks.Point(Get(button, "Rect")) + new Vector2((float)Get(panel, "X"), (float)Get(panel, "Y")); run.Neutral();
                Require((bool)Call(toolbar, "HitCurrent"), "actual toolbar HitCurrent accepts explicit controlled receipt");
                run.Frame(true, run.ChordKeys()); p.selectedItemState.Select(3); p.selectedItemState.Update(); run.NoUse("actual Blueprint toolbar owner"); p.selectedItemState.Select(4); p.selectedItemState.Update();
                Require((bool)Get(run.Input, "MapPointerOwned") && (int)Get(run.Input, "ReadOnlyMouseMask") == 0, "actual composite map/toolbar early claim protects toolbar primary"); run.Frame(false, run.ChordKeys());
                Require((bool)Get(state, "Active") && sent == 0 && !(bool)Get(run.Targets, "Busy"), "actual toolbar capture command wins announcement chord without target or use");
                Call(host, "Toggle"); run.Neutral(); Call(toolbar, "Cancel");
                p.selectedItemState.Select(3); p.selectedItemState.Update(); run.Point = new Vector2(30, 328); run.Neutral(); run.Pass(); run.Frame(true, run.ChordKeys()); object first = Get(run.Targets, "frozen"); long expires = (long)Get(run.Targets, "expires");
                run.Frame(false, run.ChordKeys()); run.Frame(true, run.ChordKeys()); run.NoUse("pending repeat with retired toolbar proof");
                Require(ReferenceEquals(first, Get(run.Targets, "frozen")) && expires == (long)Get(run.Targets, "expires") && !(bool)Call(toolbar, "HitCurrent"), "retired toolbar receipt cannot certify/retarget pending repeat");
                Call(host, "OpenMaterials"); run.Frame(false, run.ChordKeys()); run.Pass(); run.Update(); Require(sent == 0 && !(bool)Get(run.Targets, "Busy"), "actual blueprint modal invalidates independent pending target proof"); Call(Get(run.Shell, "BlueprintMaterialsUi"), "Close"); run.Neutral();
            }
            finally
            { if ((bool)Get(state, "Active")) Call(host, "Toggle"); Call(toolbar, "Cancel"); Call(host, "SetToolbar", enabled); p.inventory[4] = item; p.selectedItemState.Select(3); p.selectedItemState.Update(); TextureAssets.InventoryBack = back; TextureAssets.MagicPixel = pixel; run.Neutral(); }
        }
        private static void Patch(Harmony harmony, MethodInfo method, string name) { if (method == null) throw new MissingMethodException(name); harmony.Patch(method, prefix: new HarmonyMethod(typeof(NativeTargetOwnerChecks).GetMethod(name, F))); }
        // Preserve the actual vanilla instruction families and production
        // transpiler wrappers. Only skip unrelated inventory prologue/tail;
        // no button geometry, conditions or commands are reimplemented here.
        private static IEnumerable<CodeInstruction> InventoryScope(IEnumerable<CodeInstruction> source, ILGenerator generator)
        {
            var code = source.ToList(); int call = code.FindIndex(c => c.operand is MethodInfo m && m.Name == "GetAmountOfExtraAccessorySlotsToShow");
            int start = call; while (start >= call - 8 && code[start].opcode != OpCodes.Ldc_I4_8) start--;
            int houses = code.FindIndex(c => c.operand is MethodInfo m && m.Name == "DrawNPCHousesInUI");
            int branch = houses + 1; while (branch < houses + 4 && code[branch].opcode != OpCodes.Br && code[branch].opcode != OpCodes.Br_S) branch++;
            if (call < 0 || start < call - 8 || houses < 0 || branch == houses + 4) throw new InvalidOperationException("locked inventory family scope changed");
            Label exit = (Label)code[branch].operand; int end = code.FindIndex(c => c.labels.Contains(exit));
            if (end <= houses) throw new InvalidOperationException("locked inventory family end changed");
            code[end].opcode = OpCodes.Ret; code[end].operand = null;
            Label enter = generator.DefineLabel(); code[start].labels.Add(enter); code.Insert(0, new CodeInstruction(OpCodes.Br, enter));
            Console.WriteLine("R1 native inventory scope actualIL start=" + start + " end=" + end + " retained-button-calls=3; unrelated prologue/tail skipped"); return code;
        }
        private sealed class Replay : IDisposable
        {
            internal readonly object Input, Shell, Targets, Browser, State, Context;
            internal Vector2 Point = new Vector2(30, 328);
            private readonly HotkeyBindings bindings;
            private readonly HotkeyChord oldSend, oldQuery;
            private readonly Func<KeyboardState> source;
            private readonly Harmony cosmetic = new Harmony("JueMingR.Tests.OwnerItemCheck");
            private readonly Item useItem;
            private readonly int selected, life, max, delay;
            private readonly int[] buffs, times;
            private readonly Dust[] dust;
            private readonly WorldItem[] drops;
            private readonly bool inventory, enabled;
            internal Replay(object context, Func<int> count)
            {
                Context = context; Input = Get(context, "Input"); Shell = Get(context, "Shell"); State = Get(Shell, "State"); Browser = Get(context, "Browser"); Targets = Get(Browser, "Targets");
                bindings = (HotkeyBindings)Get(Get(Shell, "hotkeys"), "Bindings"); oldSend = bindings.Get("announcement.send"); oldQuery = bindings.Get("item-browser.query"); source = (Func<KeyboardState>)Get(Input, "ReadOnlyKeyboardSource");
                var p = Main.LocalPlayer; useItem = p.inventory[3]; selected = p.selectedItem; life = p.statLife; max = p.statLifeMax2; delay = p.potionDelay; buffs = (int[])p.buffType.Clone(); times = (int[])p.buffTime.Clone(); inventory = Main.playerInventory; enabled = (bool)Get(Get(Browser, "Announcements"), "Enabled");
                dust = Main.dust; Main.dust = new Dust[dust.Length]; for (int i = 0; i < Main.dust.Length; i++) Main.dust[i] = new Dust();
                drops = Main.item; Main.item = new WorldItem[drops.Length]; var observed = new Item(); observed.SetDefaults(9); observed.stack = 23; Main.item[0] = new WorldItem(observed) { position = new Vector2(20, 320), width = 16, height = 16 };
                Patch(cosmetic, typeof(Item).GetMethod("GetDrawHitbox", F, null, new[] { typeof(int), typeof(Player) }, null), nameof(Hitbox));
                p.inventory[3] = new Item(); p.inventory[3].SetDefaults(28); p.inventory[3].stack = 3; p.selectedItemState.Select(3); p.selectedItemState.Update(); p.statLife = 50; p.statLifeMax2 = 100; p.potionDelay = p.itemAnimation = p.itemTime = 0;
                Main.playerInventory = false; Main.gameMenu = Main.hideUI = Main.mapFullscreen = Main.blockInput = false;
                Main.screenWidth = 960; Main.screenHeight = 760; Main.screenPosition = Vector2.Zero; Main.LocalPlayer.gravDir = 1;
                typeof(Main).GetField("_uiScaleMatrix", F).SetValue(null, Matrix.Identity); PlayerInput.CacheOriginalScreenDimensions(); PlayerInput.RawMouseScale = Vector2.One;
                Set(Input, "gameWindow", (Func<IntPtr>)(() => new IntPtr(1))); Set(Input, "foregroundWindow", (Func<IntPtr>)(() => new IntPtr(1))); FocusHelper.IsSelectedApplication = true;
                Set(Shell, "LayersReady", true); Set(State, "Ready", true); Call(Shell, "CloseAndSubmitPosition"); Call(Get(Browser, "Announcements"), "SetEnabled", true);
                Bind(false); Neutral(); NativeQuickItemChecks.NativeFrame(p);
            }
            internal Keys[] ChordKeys(bool query = false) { return query ? new[] { Keys.LeftControl } : new[] { Keys.LeftShift, Keys.LeftAlt }; }
            internal void Save(string id, HotkeyChord chord)
            { long command; string reason; Require(bindings.TrySet(id, chord, null, out command, out reason), "owner binding transaction: " + reason); NativeQuickItemChecks.Until(() => { bindings.Poll(); return !bindings.Busy; }); }
            internal void Bind(bool query)
            {
                Save("announcement.send", null); Save("item-browser.query", null); HotkeyChord chord; string reason;
                Require(HotkeyChord.TryParse(query ? "LeftControl+Mouse1" : "LeftShift+LeftAlt+Mouse1", out chord, out reason), "owner chord parses"); Save(query ? "item-browser.query" : "announcement.send", chord);
            }
            internal void Frame(bool down, params Keys[] keys)
            {
                var early = new KeyboardState(keys); Set(Input, "ReadOnlyKeyboardSource", (Func<KeyboardState>)(() => early)); Call(Input, "BeginUpdate");
                PlayerInput.MouseInfo = new MouseState((int)Point.X, (int)Point.Y, 0, down ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                PlayerInput.MouseX = Main.mouseX = (int)Point.X; PlayerInput.MouseY = Main.mouseY = (int)Point.Y; Main.LocalPlayer.mouseInterface = Main.mouseText = false;
                var tokens = new List<string>(); if (down) tokens.Add("Mouse1"); Call(Input, "AfterNativeMouse", tokens);
                PlayerInput.Triggers.Reset(); PlayerInput.Triggers.Current.MouseLeft = tokens.Contains("Mouse1"); PlayerInput.Triggers.Current.Up = Main.keyState.IsKeyDown(Keys.W); PlayerInput.Triggers.Update(); Main.mouseLeft = PlayerInput.Triggers.Current.MouseLeft;
                Call(Input, "AfterMapping"); PlayerInput.CacheMousePositionForZoom(); PlayerInput.SetZoom_MouseInWorld(); Main.keyState = early; Call(Input, "AfterKeyboardRefresh");
                Call(Targets, "ProcessInput"); Call(Get(Get(Context, "Footprints"), "Layer"), "ProcessInput"); Call(Shell, "ProcessInput"); Call(Get(Get(Context, "MapFeatures"), "Layer"), "ProcessInput");
                var blueprint = GetOptional(Context, "Blueprint"); if (blueprint != null) Call(blueprint, "ProcessInput"); Require(!(bool)Get(Shell, "Failed"), "owner sample keeps actual Shell healthy");
            }
            internal void Neutral() { Frame(false); Frame(false); Main.mouseLeftRelease = true; }
            internal void Update() { Call(Targets, "Update", (long)Get(Targets, "tick") + 1); }
            internal void Pass() { Call(Targets, "BeginPass"); Main.LocalPlayer.mouseInterface = Main.mouseText = false; Call(Targets, "BeforeWorldHover"); Call(Targets, "AfterResources", true); Call(Targets, "EndPass", true); }
            internal void NoUse(string label)
            { NativeQuickItemChecks.NativeFrame(Main.LocalPlayer); Require(Main.LocalPlayer.inventory[3].stack == 3 && Main.LocalPlayer.statLife == 50 && Main.LocalPlayer.itemAnimation == 0, label + " has zero actual ItemCheck use/consumption"); }
            internal void OrdinaryUse()
            {
                Call(Targets, "Cancel", (object)null); Call(Shell, "CloseAndSubmitPosition"); Main.playerInventory = false; Point = new Vector2(10, 600); Neutral(); NativeQuickItemChecks.NativeFrame(Main.LocalPlayer);
                Frame(true); NativeQuickItemChecks.NativeFrame(Main.LocalPlayer); Require(Main.LocalPlayer.inventory[3].stack == 2 && Main.LocalPlayer.statLife > 50, "ordinary click after owner physical release reaches actual native ItemCheck and consumes one potion"); Neutral();
            }
            public void Dispose()
            {
                Call(Targets, "Cancel", (object)null); Call(Shell, "CloseAndSubmitPosition"); Call(Shell, "EndPointerLayer"); Save("announcement.send", null); Save("item-browser.query", null); Save("announcement.send", oldSend); Save("item-browser.query", oldQuery); Set(Input, "ReadOnlyKeyboardSource", source);
                var p = Main.LocalPlayer; p.inventory[3] = useItem; p.selectedItemState.Select(selected); p.selectedItemState.Update(); p.statLife = life; p.statLifeMax2 = max; p.potionDelay = delay; p.itemAnimation = p.itemTime = 0; Array.Copy(buffs, p.buffType, buffs.Length); Array.Copy(times, p.buffTime, times.Length); Main.playerInventory = inventory; Main.dust = dust; Main.item = drops; Call(Get(Browser, "Announcements"), "SetEnabled", enabled); cosmetic.UnpatchAll(cosmetic.Id);
            }
        }
    }
}
