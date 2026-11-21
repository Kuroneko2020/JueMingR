using System;
using System.Collections;
using System.IO;
using System.Linq;
using JueMingR.Platform.Companions;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;
using static NativeWorldTextProbe.NativeCompanionConfigurationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionHotkeyFeedbackChecks
    {
        internal static void Run(object context, object window, object host, Guid first, Guid second,
            Action<string> click, Action release, ProbeGraphics graphics, string output)
        {
            var archive = Get(host, "profiles");
            var shell = Get(context, "Shell");
            var keys = Get(shell, "hotkeys");
            var bindings = (HotkeyBindings)Get(keys, "Bindings");
            var registry = (HotkeyRegistry)Get(keys, "Registry");
            var feedback = Get(keys, "Feedback");
            var display = Get(context, "ShortFeedback");
            var originalFirst = Profile(host, first);
            var originalSecond = Profile(host, second);
            string prefix = "companions.profile." + originalFirst.OwnerId.ToString("N") + ".";
            string firstKeys = prefix + first.ToString("N") + ".", secondKeys = prefix + second.ToString("N") + ".";
            string firstLife = firstKeys + "life", secondLife = secondKeys + "life";
            var originalCamera = Main.screenPosition;
            bool showItems = Main.showItemText;
            Action<Keys> press = key => NativeToolsUiChecks.UiFrame(context, Vector2.Zero, false, key);
            Action<Guid> saved = id => NativeQuickItemChecks.Until(() =>
            {
                release();
                return (bool)Call(archive, "IsSaved", id) && (int)Get(feedback, "PendingCount") == 0;
            });
            Action<string, Keys> bind = (id, key) =>
            {
                Require(HotkeyChord.TryCreate((int)key, HotkeyModifiers.None, out var chord, out _) &&
                    bindings.TrySet(id, chord, null, out _, out _), "register actual companion chord " + key);
                NativeQuickItemChecks.Until(() => { release(); return !bindings.Busy; });
            };
            Action<string, string> shown = (id, text) =>
            {
                var e = NativeShortFeedbackChecks.Entries(display).SingleOrDefault(entry => (string)Get(entry, "Id") == id);
                Require(e != null && (string)Get(e, "Text") == text && (bool)Get(e, "On") == text.EndsWith("已开启", StringComparison.Ordinal) &&
                    PopupText.popupText.Any(popup => popup.active && popup.name == text),
                    "confirmed companion key reaches the original native popup with its own identity and exact text: " + text);
            };
            try
            {
                Main.screenPosition = Main.LocalPlayer.Center - new Vector2(480, 330);
                Main.showItemText = true;
                Call(feedback, "Clear");
                click("config-Life/快速/"); saved(first);
                long revision = Profile(host, first).Revision;
                click("config-Life/快速/");
                Require(Profile(host, first).Revision == revision && (int)Get(display, "Count") == 0,
                    "ordinary and unchanged mode clicks are silent and the latter submits no new revision");
                bind(firstLife, Keys.F18);
                string lifeIcon = "binding-" + firstLife + "/";
                click(lifeIcon); click(lifeIcon);
                Require((bool)Get(Get(shell, "HotkeyPopup"), "Visible"), "the real binding popup owns keyboard input");
                press(Keys.F18); release();
                Require(Profile(host, first).Revision == revision && (int)Get(display, "Count") == 0 &&
                    (int)Get(feedback, "PendingCount") == 0, "a previously bound gameplay key cannot toggle through the open binding popup");
                press(Keys.Escape); release();
                (string suffix, string name, Keys key)[] rows =
                {
                    ("follow", "跟随主人", Keys.F20), ("attack", "允许攻击", Keys.F14),
                    ("mana", "主动回蓝", Keys.F21), ("buff", "使用增益物品", Keys.F22),
                    ("growth", "使用成长物品", Keys.F23), ("respawn", "自动复活", Keys.F24),
                    ("nurse", "护士治疗", Keys.F16), ("furniture", "家具增益", Keys.F17)
                };
                foreach (var row in rows)
                    if (bindings.Get(firstKeys + row.suffix) == null) bind(firstKeys + row.suffix, row.key);
                click("profile:" + second + "/");
                bind(secondLife, Keys.F19);
                Call(window, "Close"); release();
                press(Keys.F18); press(Keys.F18); saved(first);
                var health = Profile(host, first).Behavior;
                Require(!health.HealthRecoveryEnabled && health.HealthRecoveryMode == CompanionHealthRecoveryMode.Fast &&
                    Profile(host, first).Revision == revision + 1 && Profile(host, second).Behavior.HealthRecoveryEnabled,
                    "a held physical key while the panel is closed toggles only its target once and keeps its chosen mode");
                shown(firstLife, originalFirst.Name + " · 主动回血（快速） 已关闭");
                press(Keys.F19); saved(second);
                shown(secondLife, originalSecond.Name + " · 主动回血（智能） 已关闭");
                shown(firstLife, originalFirst.Name + " · 主动回血（快速） 已关闭");
                for (int frame = 0; frame < 12; frame++) { PopupText.UpdateItemText(); Call(display, "Refresh"); }
                Action draw = () => PopupText.DrawItemTextPopups(PopupText.TargetScale);
                var pixels = graphics.Pixels(draw, Main.GameViewMatrix.ZoomMatrix);
                Require(pixels.Any(pixel => pixel.A > 96), "real native short feedback draws nonempty glyphs");
                graphics.Image(Path.Combine(output, "companion-hotkey-feedback.png"), draw, Main.GameViewMatrix.ZoomMatrix);
                press(Keys.F18); saved(first);
                press(Keys.F19); saved(second);
                shown(firstLife, originalFirst.Name + " · 主动回血（快速） 已开启");
                shown(secondLife, originalSecond.Name + " · 主动回血（智能） 已开启");
                Require(Profile(host, first).Behavior.HealthRecoveryMode == CompanionHealthRecoveryMode.Fast &&
                    Profile(host, second).Behavior.HealthRecoveryMode == CompanionHealthRecoveryMode.Smart,
                    "the two owners restore their own actual recovery modes");
                foreach (var row in rows)
                {
                    string action = firstKeys + row.suffix;
                    press(row.key); saved(first);
                    shown(action, originalFirst.Name + " · " + row.name + (row.suffix == "furniture" ? " 已开启" : " 已关闭"));
                    press(row.key); saved(first);
                    shown(action, originalFirst.Name + " · " + row.name + (row.suffix == "furniture" ? " 已关闭" : " 已开启"));
                }
                CheckPersistence(host, archive, feedback, display, second, secondLife, press, release, saved, shown);
                var oldAction = registry.Find(secondLife);
                Rename(archive, Profile(host, second), "更名队友");
                Require(!oldAction.Invoke(HotkeyContext.SinglePlayer), "old action cannot label the renamed character before registry refresh");
                saved(second);
                press(Keys.F19); saved(second);
                shown(secondLife, "更名队友 · 主动回血（智能） 已关闭");
                var previousOwner = Main.ActivePlayerFileData;
                try
                {
                    Main.ActivePlayerFileData = new Terraria.IO.PlayerFileData(
                        Path.Combine(Terraria.Program.SavePath, "different-owner.plr"), false) { Player = Main.LocalPlayer };
                    Require(!registry.Find(secondLife).Invoke(HotkeyContext.SinglePlayer), "an old action refuses a different owner's actual character file");
                    Call(display, "Refresh");
                    Require((int)Get(display, "Count") == 0, "a new owner's display never retains the old character's result");
                }
                finally { Main.ActivePlayerFileData = previousOwner; }
                Rename(archive, Profile(host, second), originalSecond.Name);
                saved(second);
                Require(!NativeShortFeedbackChecks.Entries(display).Any(e => (string)Get(e, "Id") == secondLife),
                    "the former name's active result is retired after an actual archive revision and roster change");
                Call(shell, "OpenCompanions", Get(Get(Get(shell, "State"), "Layout"), "Viewport"));
                release(); click("profile:" + first + "/"); release();
                var icons = ((IEnumerable)Get(window, "controls")).Cast<object>().Where(c => GetOptional(c, "HotkeyId") != null).ToArray();
                Require(icons.Length == 9 && icons.All(icon => (string)Get(icon, "Hint") == "双击设置功能开关快捷键"),
                    "all nine concrete key controls carry the shared toggle explanation");
                NativeToolsUiChecks.UiFrame(context, NativeToolsUiChecks.Point(Get(icons[0], "Rect")), false);
                var hint = Get(window, "hint");
                Require((bool)Get(hint, "Visible") && (string)Get(hint, "text") == "双击设置功能开关快捷键",
                    "real pointer hover reaches the shared hint layout");
                Console.WriteLine("PASS nine companion switch keys, current names and exact archive receipts, independent Smart/Fast modes, real native text, silent F5 and unchanged input.");
            }
            finally
            {
                Call(feedback, "Clear");
                Main.screenPosition = originalCamera;
                Main.showItemText = showItems;
                Call(host, "SetBehavior", first, originalFirst.Behavior);
                Call(host, "SetBehavior", second, originalSecond.Behavior);
                release();
            }
        }

        private static void CheckPersistence(object host, object archive, object feedback, object display, Guid id,
            string action, Action<Keys> press, Action release, Action<Guid> saved, Action<string, string> shown)
        {
            Call(feedback, "Clear");
            var profile = Profile(host, id);
            var manager = Get(archive, "manager");
            var gate = Get(manager, "gate");
            var path = Path.Combine(Terraria.Program.SavePath, "composition", "JueMingRData", "companions", "characters",
                profile.OwnerId.ToString("N"), id.ToString("N") + ".bin");
            var before = File.ReadAllBytes(path);
            // Hold the actual archive file against its original replacement.
            // Pausing dequeue lets us inspect acceptance before any result.
            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                lock (gate)
                {
                    press(Keys.F19);
                    Require(!Profile(host, id).Behavior.HealthRecoveryEnabled && (int)Get(feedback, "PendingCount") == 1 &&
                        (int)Get(display, "Count") == 0, "accepted RAM change has no success text while its real writer is pending");
                }
                NativeQuickItemChecks.Until(() => { release(); return (bool)Call(archive, "SaveFailed", id) && (int)Get(feedback, "PendingCount") == 0; });
                Require((int)Get(display, "Count") == 0 && !String.IsNullOrEmpty((string)Call(archive, "SaveError", id)),
                    "an actual archive write failure retains its original error and publishes no switch success");
            }
            Require(before.SequenceEqual(File.ReadAllBytes(path)), "the original file survives the failed command");
            Require((bool)Call(host, "RetrySave", id), "existing archive retry accepted");
            saved(id);
            Require((int)Get(display, "Count") == 0 && !before.SequenceEqual(File.ReadAllBytes(path)),
                "later same-revision recovery persists the data without replaying the failed key");
            lock (gate)
            {
                press(Keys.F19);
                Require((int)Get(feedback, "PendingCount") == 1, "new owned key awaits its own write");
                var superseding = Profile(host, id).Behavior;
                superseding.HealthRecoveryEnabled = false;
                Require((bool)Call(host, "SetBehavior", id, superseding), "another actual same-profile revision supersedes that key");
                release();
            }
            saved(id);
            Require(!NativeShortFeedbackChecks.Entries(display).Any(e => (string)Get(e, "Id") == action),
                "a different later command's write cannot acknowledge the superseded key");
            Require((bool)Call(host, "Join", id), "join the second character for a native capture and closing-window rejection");
            var body = (Player)Call(host, "GetBody", id);
            var actor = Call(host, "Owned", body);
            var originalSlot = body.inventory[45];
            lock (gate)
            {
                press(Keys.F19);
                var independent = Profile(host, id).Behavior;
                independent.ManaRecoveryEnabled = !independent.ManaRecoveryEnabled;
                Require((bool)Call(host, "SetBehavior", id, independent), "a different setting shares this archive file");
                body.inventory[45] = new Item(Terraria.ID.ItemID.Wood) { stack = 3 };
                Call(host, "CaptureActorState", actor, null);
                release();
                Require((int)Get(feedback, "PendingCount") == 1 && (int)Get(display, "Count") == 0,
                    "unrelated same-character setting and native inventory capture keep the original switch pending");
            }
            saved(id);
            shown(action, profile.Name + " · 主动回血（智能） 已开启");
            body.inventory[45] = originalSlot;
            Call(host, "CaptureActorState", actor, null);
            saved(id);
            shown(action, profile.Name + " · 主动回血（智能） 已开启");
            Call(feedback, "Clear");
            lock (gate)
            {
                Require((bool)Call(host, "Recall", id), "the second actor's real recall owns its closing revision");
                long closing = Profile(host, id).Revision;
                press(Keys.F19); release();
                Require(Profile(host, id).Revision == closing && Profile(host, id).Behavior.HealthRecoveryEnabled &&
                    (int)Get(feedback, "PendingCount") == 0 && (int)Get(display, "Count") == 0,
                    "a real closing actor refuses gameplay without submitting or displaying a success");
            }
            saved(id);
        }

        private static void Rename(object archive, CompanionProfile profile, string name)
        {
            // Present a valid newly named roster entry to the registry. There
            // is no companion rename button; do not invent one for the check.
            var body = (Player)Call(archive, "LoadBody", profile);
            body.name = name;
            var bytes = (byte[])Call(archive, "Capture", body);
            var renamed = new CompanionProfile(profile.Id, profile.OwnerId, name, profile.Revision + 1, profile.DataEpoch,
                profile.Behavior, bytes, profile.PermanentlyDead);
            Call(Get(archive, "manager"), "Update", renamed, 0L);
        }
    }
}
