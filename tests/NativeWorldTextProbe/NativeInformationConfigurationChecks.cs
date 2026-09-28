using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingR.Features.Information;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Information;
using JueMingR.Platform.Settings;
using JueMingR.Infrastructure.Storage;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Uses both real composition catalogs and the popup's captured delegates;
    // disk paths belong only to the probe's isolated temporary game directory.
    internal static class NativeInformationConfigurationChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        internal static string[] SeedOldBindings(Type worker, string root)
        {
            string oldRoot = Path.Combine(root, "previous-catalog"); Directory.CreateDirectory(oldRoot);
            object previous = Activator.CreateInstance(worker.GetNestedType("PostfixContext", Flags), Flags, null,
                new object[] { "world-object-text-" + new string('6', 40), Path.Combine(oldRoot, "evidence.txt"), oldRoot }, null);
            string[] ids;
            try
            {
                Call(previous, "InitializeRuntime", true);
                ids = ((HotkeyRegistry)Get(Get(Get(previous, "Shell"), "hotkeys"), "Registry")).Actions.Select(a => a.Id).ToArray();
                Require(ids.Length > 0, "previous full profile must supply a nonempty catalog");
            }
            finally { StopContext(previous); }
            string path = Path.Combine(root, "JueMingRData", "config", "hotkeys.json"); Directory.CreateDirectory(Path.GetDirectoryName(path));
            var entries = ids.Select((id, i) => new KeyValuePair<string, string>(id, ((char)('A' + i)).ToString())).ToList();
            entries.Add(new KeyValuePair<string, string>("future.unknown", "Z"));
            File.WriteAllBytes(path, HotkeyDocument.Encode(new HotkeyDocument(entries))); return ids;
        }
        internal static void Run(object context, object host, string root, string[] oldActions)
        {
            Migration(host,root);
            var shell = Get(context, "Shell"); var bindings = (HotkeyBindings)Get(Get(shell, "hotkeys"), "Bindings");
            Until(() => { bindings.Poll(); return bindings.Loaded; });
            var newIds = new[] { "information.infection.toggle", "information.luck.toggle", "information.angler.toggle", "information-window.adjust", "information.full-fish.toggle", "information.filtered-fish.toggle" };
            Require(oldActions.Select((id, i) => bindings.Get(id)?.Text == ((char)('A' + i)).ToString()).All(v => v) && newIds.All(id => bindings.Get(id) == null),
                "full new catalog preserves every actual old binding and leaves the six new actions unbound");
            long command; string reason; HotkeyChord chord; HotkeyChord.TryParse("F1", out chord, out reason);
            Require(bindings.TrySet(newIds[0], chord, null, out command, out reason), "new action binds through the existing reliable-save owner");
            Until(() => { bindings.Poll(); return !bindings.Busy; });
            var saved = HotkeyDocument.Decode(File.ReadAllBytes(Path.Combine(root, "JueMingRData", "config", "hotkeys.json"))).Entries;
            Require(saved.Count == oldActions.Length + 2 && saved.Any(e => e.Key == "future.unknown" && e.Value == "Z") &&
                oldActions.Select((id, i) => saved.Any(e => e.Key == id && e.Value == ((char)('A' + i)).ToString())).All(v => v), "new binding save preserves the full old catalog and future entry");

            var popup = Get(shell, "StylePopup"); var assembly = shell.GetType().Assembly;
            var rect = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.F5.F5Rect"), Flags, null, new object[] { 0f, 0f, 80f, 30f }, null);
            var click = popup.GetType().GetMethods(Flags).Single(m => m.Name == "Click" && m.GetParameters()[0].ParameterType == typeof(InformationKind));
            object prior = null;
            foreach (InformationKind kind in Enum.GetValues(typeof(InformationKind)))
            {
                InformationPreferences before = Value(host);
                click.Invoke(popup, new[] { (object)kind, rect, 9 });
                var target = Get(popup, "selection"); var editor = Get(popup, "Editor");
                int defaultSize=InformationPreferences.Default.Style(kind).Size;
                Require((InformationKind)Get(popup, "InformationTarget") == kind && (int)Get(popup, "NameSize") == defaultSize && (prior == null || !(bool)Call(target, "Same", prior)), "six actual popup targets open independently with their own default sizes");
                Call(editor, "BeginHex"); Call(editor, "Insert", "ABCDEF"); ((Func<int, bool>)Get(target, "StepSize"))(1);
                Require(Value(host).Style(kind).Rgb == 0xABCDEF && Value(host).Style(kind).Size == defaultSize+10 &&
                    Enum.GetValues(typeof(InformationKind)).Cast<InformationKind>().Where(k => k != kind).All(k => Value(host).Style(k).Equals(before.Style(k))), "captured popup color/size edits only its selected target");
                ((Action)Get(target, "Reset"))();
                Require(Value(host).Style(kind).Equals(InformationPreferences.Default.Style(kind)) && Value(host).EnabledMask == before.EnabledMask, "style reset restores exactly this item's defaults without changing switches");
                prior = target;
            }
            Call(popup, "Close");
            Until(() => ((PreferenceSnapshot<InformationPreferences>)Get(host, "Preferences")).Status == PreferenceStatus.Saved);
            string file = Path.Combine(root, "JueMingRData", "config", "features", "information-display.json");
            var expected = Value(host);
            // A real restart releases the first document's exclusive ownership
            // before a fresh owner reads it. Keep that owner in Host for cleanup.
            Require((bool)Call(Get(host, "preferences"), "Stop", 750), "first settings owner must release before restart");
            var reloaded = new PreferenceDocument<InformationPreferences>(new AtomicFileDocument(file, 65536, true), new InformationPreferenceCodec(), InformationPreferences.Default);
            Set(host, "preferences", reloaded);
            Until(() => reloaded.Snapshot.IsLoaded);
            Require(reloaded.Snapshot.Status == PreferenceStatus.Saved && reloaded.Snapshot.Value.Equals(expected), "real background writer and fresh reader retain 0.82 styles and the accepted switch");
            Console.WriteLine("PASS: six actual style popup identities, independent commands/reset/reload, previous full action catalog and unknown binding retention.");
        }
        private static InformationPreferences Value(object host) { return ((PreferenceSnapshot<InformationPreferences>)Get(host, "Preferences")).Value; }
        private static void Migration(object host,string root)
        {
            string path=Path.Combine(root,"schema1-retention.json");
            byte[] original=System.Text.Encoding.UTF8.GetBytes("{\"format\":\"JueMingR.InformationDisplay\",\"version\":1,\"enabled\":8,\"biome\":{\"rgb\":1,\"size\":92},\"infection\":{\"rgb\":2,\"size\":82},\"luck\":{\"rgb\":3,\"size\":72},\"angler\":{\"rgb\":4,\"size\":102}}");
            File.WriteAllBytes(path,original);
            var file=new AtomicFileDocument(path,65536,true,".schema1-original");
            var codec=(IPreferenceCodec<InformationPreferences>)Activator.CreateInstance(host.GetType().GetNestedType("RetainingCodec",Flags),Flags,null,new object[]{file},null);
            var document=new PreferenceDocument<InformationPreferences>(file,codec,InformationPreferences.Default);
            try
            {
                Until(()=>document.Snapshot.IsLoaded);
                Require(File.ReadAllBytes(path).SequenceEqual(original) && !File.Exists(path+".schema1-original"),"schema1 load is read-only and preserves original bytes");
                Require(document.Snapshot.Value.Enabled(InformationKind.Angler) && !document.Snapshot.Value.Enabled(InformationKind.FullFish),"actual retaining owner adds only disabled fishing defaults");
                Require(document.Set(document.Snapshot.Value.WithEnabled(InformationKind.FullFish,true)),"explicit migration save accepted");
                Until(()=>document.Snapshot.Status==PreferenceStatus.Saved);
                Require(File.ReadAllBytes(path+".schema1-original").SequenceEqual(original) && new InformationPreferenceCodec().Decode(File.ReadAllBytes(path)).Enabled(InformationKind.FullFish),"first explicit save retains exact schema1 source and commits new fish preference");
            }
            finally{Require(document.Stop(750),"isolated migration writer exits");}
        }
        private static void Until(Func<bool> predicate)
        { var deadline = DateTime.UtcNow.AddSeconds(5); while (!predicate()) { if (DateTime.UtcNow > deadline) throw new TimeoutException("information configuration readiness/save"); Thread.Sleep(5); } }
    }
}
