using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JueMingR.Features.Items;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class ItemAutomationSettingsChecks
    {
        internal static void Check(IList<string> failures)
        {
            try
            {
                ItemAutomationSettings defaults = ItemAutomationSettings.Default;
                string newDefaultJson = Encoding.UTF8.GetString(new ItemAutomationCodec(7000).Encode(defaults));
                Require(newDefaultJson.Contains("\"version\":2") && !newDefaultJson.Contains("Binding"),
                    "current item writes must use version 2 without retired bindings");
                Require(!defaults.StackEnabled && !defaults.SellEnabled && !defaults.DiscardEnabled, "destructive defaults must stay off");
                Require(defaults.SellTypes.SequenceEqual(new[] { 2337, 2338, 2339 }) && defaults.DiscardTypes.Count == 0, "only accepted default sale list");
                int[] draft = { 9, 9, 71, 72, 73, 74, 8 };
                var changed = defaults.WithTypes(ItemListKind.Sell, draft).WithEnabled(ItemActionKind.Sell, true);
                draft[0] = 2;
                Require(changed.SellTypes.SequenceEqual(new[] { 8, 9 }), "immutable deduplicated type lists exclude every coin");
                Require(defaults.SellTypes.Count == 3 && !defaults.SellEnabled, "draft commands must not mutate earlier revisions");
                changed = changed.WithTypes(ItemListKind.Discard, new[] { 8, 9 });
                Require(changed.DiscardTypes.SequenceEqual(new[] { 8, 9 }), "independent lists permit overlap");
                var codec = new ItemAutomationCodec(7000);
                ItemAutomationSettings recovered = codec.Decode(codec.Encode(changed));
                Require(recovered.Equals(changed) && recovered.GetHashCode() == changed.GetHashCode(), "worker roundtrip must retain value equality");
                int[] many = Enumerable.Range(100, 2000).ToArray();
                var large = defaults.WithTypes(ItemListKind.Sell, many);
                Require(codec.Decode(codec.Encode(large)).SellTypes.Count == 2000, "valid later list entries must not be truncated by simple preference quotas");
                string json = Encoding.UTF8.GetString(codec.Encode(changed));
                string legacy = json.Replace("\"version\":2", "\"version\":1,\"stackBinding\":112,\"sellBinding\":113,\"discardBinding\":114");
                Require(codec.Decode(Encoding.UTF8.GetBytes(legacy)).Equals(changed), "v1 nonzero keys retire at read boundary, switches and lists survive");
                Require(codec.Decode(Encoding.UTF8.GetBytes(legacy.Replace(":112", ":-99").Replace(":113", ":-99"))).Equals(changed), "retired integers have no key or conflict semantics");
                ExpectFormat(codec, legacy.Replace(":112", ":\"F1\""), PreferenceStatus.Invalid);
                ExpectFormat(codec, json.Replace("\"version\":2", "\"version\":2,\"stackBinding\":112"), PreferenceStatus.UnknownFields);
                ExpectFormat(codec, json.Replace("\"version\":2", "\"version\":2,\"version\":2"), PreferenceStatus.Invalid);
                ExpectFormat(codec, json.Replace("\"version\":2", "\"version\":99"), PreferenceStatus.UnsupportedVersion);
                ExpectFormat(codec, json.Replace("\"version\":2", "\"version\":2,\"future\":true"), PreferenceStatus.UnknownFields);
                ExpectFormat(codec, json.Replace("\"sellEnabled\":true", "\"sellEnabled\":\"true\""), PreferenceStatus.Invalid);
                ExpectFormat(codec, "{\"__type\":\"future\"," + json.Substring(1), PreferenceStatus.UnknownFields);
                ExpectFormat(codec, json.Replace("[8,9]", "[8,7000]"), PreferenceStatus.Invalid);
            }
            catch (Exception e) { failures.Add("item settings: " + e.Message); }
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void ExpectFormat(ItemAutomationCodec codec, string json, PreferenceStatus status)
        {
            try { codec.Decode(Encoding.UTF8.GetBytes(json)); }
            catch (PreferenceFormatException e) { Require(e.Status == status, "wrong protection reason"); return; }
            throw new InvalidOperationException("unsafe preference file accepted");
        }
    }
}
