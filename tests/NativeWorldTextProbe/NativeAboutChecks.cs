using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using JueMingR.Features.Onboarding;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;
namespace NativeWorldTextProbe
{
    internal static class NativeAboutChecks
    {
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        internal static void Run(object context)
        {
            foreach (string name in new[] { "CoinDeposit", "QuickItems", "KeepFavorited", "Browser", "Footprints", "MapFeatures", "DeathRecords", "Information", "Guidance", "WorldObjects", "WorldTargets", "Labels", "onboarding" }) Require(GetOptional(context, name) != null, "Full About profile retains " + name);
            object shell = Get(context, "Shell"), state = Get(shell, "State"), layout = Get(state, "Layout"), page = Get(layout, "About"), renderer = Get(shell, "renderer");
            var assembly = shell.GetType().Assembly;
            string version = (string)Get(page, "Version");
            Require(version.Contains(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion) && version.Contains("1.4.5.8"), "Displayed identity comes from loaded Host and actual .8 EXE");
            var names = new[] { "wechat.png", "alipay.jpg" };
            var hashes = new[] { "8AFAD8D5C17B1A23DA579155D772A4BABE4AAEB900CC5CCEAB6CB0712E049835", "6C5F1165FE46901BE2A3C5FFF273116374FA995960ABF79A1A9CA4305B9C7BE3" };
            for (int i = 0; i < 2; i++) using (var stream = assembly.GetManifestResourceStream("JueMingR.About." + names[i])) using (var hash = SHA256.Create())
                Require(stream != null && BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "") == hashes[i], "Built Host embeds exact authorized bytes " + names[i]);
            int copies = 0; string copied = null;
            Call(page, "Attach", (Func<string>)(() => version), (Func<string, bool>)(text => { copies++; copied = text; return true; }));
            Call(state, "Navigate", 5); Call(state, "RestoreVisible"); Set(state, "Ready", true); Call(renderer, "RefreshResources");
            Call(renderer, "Prepare", state, (float)PlayerInput.OriginalScreenSize.X, (float)PlayerInput.OriginalScreenSize.Y, Main.UIScaleMatrix.M11);
            var button = ((IEnumerable)Get(layout, "Elements")).Cast<object>().First(e => Get(e, "Command").ToString() == "AboutCopyGroup");
            Click(context, button);
            Require(copies == 1 && copied == "915753352", "Actual Shell.ProcessInput dispatch reaches the injected clipboard outlet exactly once");
            var identity = assembly.GetType("JueMingR.TerrariaHost.Onboarding.CharacterIdentity");
            var previousWorld = Main.ActiveWorldFileData; var previousFile = Main.ActivePlayerFileData; int previousMode = Main.netMode; int previousConnection = Netplay.Connection.State;
            try
            {
                Main.ActivePlayerFileData = new Terraria.IO.PlayerFileData(Path.Combine(Terraria.Program.SavePath, "onboarding-fixture.plr"), false) { Player = Main.LocalPlayer };
                Main.ActiveWorldFileData = null; Main.netMode = 1; Netplay.Connection.State = 10;
                var args = new object[] { false };
                string path = (string)identity.GetMethod("Path", Flags).Invoke(null, args);
                Require(path == Main.ActivePlayerFileData.Path, "Ordinary client character admission works without world identity or world history");
                string key = (string)identity.GetMethod("Key", Flags).Invoke(null, new object[] { path, false });
                Require(OnboardingMarkerCodec.ValidKey(key), "No name or slot becomes the persistent key");
                Main.ServerSideCharacter = true;
                Require(identity.GetMethod("Path", Flags).Invoke(null, args) == null, "SSC never writes a shared anonymous marker");
                Main.ServerSideCharacter = false;
                object owner = Get(context, "onboarding"); Call(owner, "OnSessionStarted"); Call(owner, "Update", 0UL);
                var marker = (OnboardingState)Get(owner, "State"); NativeQuickItemChecks.Until(() => { marker.Poll(); return marker.Ready; });
                long before = (long)Get(owner, "admission");
                var oldPlayer = Main.LocalPlayer;
                try
                {
                    Main.player[Main.myPlayer] = new Player { active = true };
                    Call(owner, "Update", 1UL); marker.Presented(before, 100);
                    Require(!marker.Shown && (long)Get(owner, "admission") > before, "Role object replacement rejects prior Draw receipt");
                }
                finally { Main.player[Main.myPlayer] = oldPlayer; Call(owner, "OnSessionEnded"); }
            }
            finally { Main.ActiveWorldFileData = previousWorld; Main.ActivePlayerFileData = previousFile; Main.netMode = previousMode; Netplay.Connection.State = previousConnection; }
            Console.WriteLine("PASS: complete About native composition, built asset bytes, loaded identity, real Shell clipboard dispatch, ordinary-client and SSC identity, stale-role receipt.");
        }
        private static void Click(object context, object element)
        {
            object shell = Get(context, "Shell"), state = Get(shell, "State"), input = Get(context, "Input"), layout = Get(state, "Layout");
            object rect = Get(element, "Rect"), view = Get(layout, "Viewport");
            Call(state, "ScrollTo", Math.Max(0, (float)Get(rect, "Bottom") - (float)Get(view, "Height") + 8));
            int x = (int)((float)Get(state, "X") + (float)Get(view, "X") + (float)Get(rect, "X") + 5);
            int y = (int)((float)Get(state, "Y") + (float)Get(view, "Y") + (float)Get(rect, "Y") - (float)Get(state, "Scroll") + 5);
            foreach (var down in new[] { false, true, true, false })
            {
                NativeQuickItemChecks.Sample(input, new Keys[0]);
                PlayerInput.MouseInfo = new MouseState(x, y, 0, down ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                Call(shell, "ProcessInput");
            }
        }
    }
}
