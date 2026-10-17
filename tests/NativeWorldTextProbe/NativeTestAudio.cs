using System;
using System.Reflection;
using HarmonyLib;

namespace NativeWorldTextProbe
{
    // Test-process policy only. Never touch Windows sessions, the owner's
    // game/configuration, or production Host audio. Install before any native
    // menu/world entry and reapply after isolated settings can overwrite it.
    internal static class NativeTestAudio
    {
        private static FieldInfo music,sound,ambient;
        private static bool installed;
        internal static void Install(Assembly game)
        {
            if(installed)return;
            var main=game.GetType("Terraria.Main",true);
            const BindingFlags flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            // Main's static paths derive from Program.SavePath. Installing in
            // AssemblyResolve would run its cctor before the fixture owns that
            // path. The real scope must establish isolation first; never mint
            // a substitute path or read the owner's settings to mute a test.
            if(String.IsNullOrEmpty((string)game.GetType("Terraria.Program",true).GetField("SavePath",flags).GetValue(null)))
                throw new InvalidOperationException("Establish the isolated test SavePath before native audio initialization.");
            music=main.GetField("musicVolume",flags);sound=main.GetField("soundVolume",flags);ambient=main.GetField("ambientVolume",flags);
            if(music==null || sound==null || ambient==null)throw new MissingFieldException("Native test audio volume fields");
            installed=true;
            // An audible run is an explicit test instruction, never inherited
            // from an owner's settings. Default and all ordinary probes mute.
            if(Program.TestSoundRequested)
            {Console.WriteLine("TEST AUDIO explicitly enabled by test invocation");return;}
            Apply();
            var patch=new Harmony("JueMingR.Tests.DefaultSilentAudio");
            patch.Patch(main.GetMethod("LoadSettings",flags),postfix:new HarmonyMethod(typeof(NativeTestAudio),nameof(AfterSettings)));
            patch.Patch(main.GetMethod("UpdateAudio",flags),prefix:new HarmonyMethod(typeof(NativeTestAudio),nameof(Apply)));
            Report("startup-before-native-entry");
        }
        private static void Apply()
        {music.SetValue(null,0f);sound.SetValue(null,0f);ambient.SetValue(null,0f);}
        private static void AfterSettings(){Apply();Report("after-isolated-settings");}
        internal static void Report(string phase)
        {Console.WriteLine("TEST AUDIO phase="+phase+" music="+music.GetValue(null)+" sound="+sound.GetValue(null)+" ambient="+ambient.GetValue(null)+" scope=isolated-process-only");}
    }
}
