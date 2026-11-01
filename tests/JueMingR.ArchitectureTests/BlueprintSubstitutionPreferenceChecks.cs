using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JueMingR.Features.Blueprint;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class BlueprintSubstitutionPreferenceChecks
    {
        private const string Prefix = "{\"format\":\"JueMingR.Blueprint\",\"version\":1,\"gelToolbar\":true";
        internal static void Check(IList<string> failures)
        {
            Run(failures, "additive defaults and independent copies", Values);
            Run(failures, "strict replacement field classification", InvalidFields);
            Run(failures, "real preference owner and protected original bytes", Persistence);
        }

        private static void Values()
        {
            var codec = new BlueprintPreferenceCodec();
            var old = codec.Decode(Bytes(Prefix + "}"));
            Require(old.GelToolbar && !old.AutoBuild && !old.SameTypeReplacement && old.ReplacementCategories == 4095,
                "gel-only v1 uses off/all replacement defaults without changing its existing intention");
            old = codec.Decode(Bytes(Prefix + ",\"autoBuild\":true}"));
            Require(old.AutoBuild && !old.SameTypeReplacement && old.ReplacementCategories == 4095,
                "old auto-build v1 preserves automatic intent and independently defaults replacement");
            Require(codec.Decode(Bytes(Prefix + ",\"sameTypeReplacement\":true}")).ReplacementCategories == 4095 &&
                !codec.Decode(Bytes(Prefix + ",\"replacementCategories\":0}")).SameTypeReplacement,
                "each new optional field may be absent independently");
            Require(new BlueprintPreferences(false, false).Equals(new BlueprintPreferences(false, false, false, 4095)),
                "existing construction sites retain the explicit off/all implementation default");
            for (int i = 0; i < 12; i++)
            {
                int mask = 1 << i;
                var value = new BlueprintPreferences(true, true, true, mask);
                Require(codec.Decode(codec.Encode(value)).Equals(value), "each stable category bit round-trips independently");
                Require(value.WithGelToolbar(false).Equals(new BlueprintPreferences(false, true, true, mask)) &&
                    value.WithAutoBuild(false).Equals(new BlueprintPreferences(true, false, true, mask)) &&
                    value.WithSameTypeReplacement(false).Equals(new BlueprintPreferences(true, true, false, mask)) &&
                    value.WithReplacementCategories(4095 ^ mask).Equals(new BlueprintPreferences(true, true, true, 4095 ^ mask)),
                    "all four copy setters preserve the other intentions and category selection");
                Require(!value.Equals(value.WithReplacementCategories(0)) && !value.Equals(value.WithSameTypeReplacement(false)),
                    "preference equality includes the new fields so a real edit reaches the owner");
            }
            foreach (int mask in new[] { 0, 4095 })
                Require(codec.Decode(codec.Encode(new BlueprintPreferences(false, false, false, mask))).ReplacementCategories == mask,
                    "both inclusive mask boundaries are valid");
        }

        private static void InvalidFields()
        {
            var codec = new BlueprintPreferenceCodec();
            foreach (string suffix in new[] {
                ",\"sameTypeReplacement\":1}", ",\"sameTypeReplacement\":\"true\"}", ",\"sameTypeReplacement\":null}",
                ",\"sameTypeReplacement\":true,\"sameTypeReplacement\":false}",
                ",\"replacementCategories\":-1}", ",\"replacementCategories\":4096}", ",\"replacementCategories\":2147483648}",
                ",\"replacementCategories\":1.0}", ",\"replacementCategories\":1e0}", ",\"replacementCategories\":true}",
                ",\"replacementCategories\":\"4095\"}", ",\"replacementCategories\":null}", ",\"replacementCategories\":[]}",
                ",\"replacementCategories\":1,\"replacementCategories\":2}" })
                Expect(codec, Prefix + suffix, PreferenceStatus.Invalid);
            Expect(codec, Prefix + ",\"replacementCategories\":4095,\"future\":true}", PreferenceStatus.UnknownFields);
            Expect(codec, Prefix.Replace("\"version\":1", "\"version\":2") + ",\"sameTypeReplacement\":true}", PreferenceStatus.UnsupportedVersion);
            foreach (int mask in new[] { -1, 4096 })
            {
                bool rejected = false;
                try { new BlueprintPreferences(false, false, true, mask); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                Require(rejected, "an invalid mask cannot be constructed then silently truncated during encoding");
            }
        }

        private static void Persistence()
        {
            string root = Path.Combine(Path.GetTempPath(), "JueMingR-BlueprintSubstitution-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            // Each actual worker is joined before this isolated input directory
            // can be removed. No game-install or user configuration path is used.
            bool stopped = false;
            try
            {
                string path = Path.Combine(root, "blueprint.json");
                byte[] legacy = Bytes(Prefix + "}"); File.WriteAllBytes(path, legacy);
                var storage = new PreferenceChecks.CountingStorage(path);
                using (var document = new PreferenceDocument<BlueprintPreferences>(storage, new BlueprintPreferenceCodec(), new BlueprintPreferences(false, false), 0))
                {
                    PreferenceChecks.Loaded(document);
                    Require(storage.Writes == 0 && Convert.ToBase64String(File.ReadAllBytes(path)) == Convert.ToBase64String(legacy) && !File.Exists(path + ".bak"),
                        "reading optional defaults does not rewrite old v1 bytes or create a backup");
                    var initial = document.Snapshot;
                    for (int i = 0; i < 20; i++) Require(!document.Set(initial.Value), "same-value commands are inert");
                    Require(document.Snapshot.Revision == initial.Revision && storage.Writes == 0, "stable choices do not queue saves");
                    Require(document.Set(initial.Value.WithReplacementCategories(0x805)), "off-state category edit is accepted");
                    var changed = document.Snapshot.Value.WithSameTypeReplacement(true).WithAutoBuild(true).WithGelToolbar(false);
                    Require(document.Set(changed), "other intentions retain the category mask");
                    stopped = document.Stop(3000); Require(stopped, "real save worker finishes");
                    Require(new BlueprintPreferenceCodec().Decode(File.ReadAllBytes(path)).Equals(changed), "the last accepted four-field value reaches real storage");
                }
                foreach (string json in new[] {
                    "{broken", Prefix + ",\"replacementCategories\":4096}", Prefix + ",\"sameTypeReplacement\":true,\"sameTypeReplacement\":false}",
                    Prefix + ",\"sameTypeReplacement\":\"true\"}", Prefix + ",\"future\":true}", Prefix.Replace("\"version\":1", "\"version\":2") + "}" })
                {
                    File.WriteAllBytes(path, Bytes(json));
                    storage = new PreferenceChecks.CountingStorage(path);
                    using (var document = new PreferenceDocument<BlueprintPreferences>(storage, new BlueprintPreferenceCodec(), new BlueprintPreferences(false, false), 0))
                    {
                        stopped = false; PreferenceChecks.Loaded(document);
                        Require(document.Snapshot.IsProtected && document.Set(document.Snapshot.Value.WithSameTypeReplacement(true).WithReplacementCategories(3)),
                            "protected input permits this-session memory changes without unlocking the document");
                        Require(document.Snapshot.IsProtected && document.Snapshot.Value.SameTypeReplacement && document.Snapshot.Value.ReplacementCategories == 3,
                            "the single protected owner publishes the chosen memory value");
                        stopped = document.Stop(3000); Require(stopped, "protected worker finishes");
                        Require(storage.Writes == 0 && Encoding.UTF8.GetString(File.ReadAllBytes(path)) == json,
                            "invalid, unknown, duplicate, wrong-type and future original bytes remain intact");
                    }
                }
            }
            finally { if (stopped) Directory.Delete(root, true); }
        }

        private static byte[] Bytes(string value) { return new UTF8Encoding(false, true).GetBytes(value); }
        private static void Expect(BlueprintPreferenceCodec codec, string json, PreferenceStatus expected)
        {
            try { codec.Decode(Bytes(json)); }
            catch (PreferenceFormatException error) { Require(error.Status == expected, "invalid fields retain their exact protection class"); return; }
            throw new InvalidOperationException("invalid replacement document was accepted");
        }
        private static void Run(IList<string> failures, string name, Action check)
        { try { check(); } catch (Exception error) { failures.Add("Blueprint substitution preferences " + name + ": " + error.Message); } }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
