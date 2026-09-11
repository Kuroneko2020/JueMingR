using System;
using System.Collections.Generic;
using System.Text;
using JueMingR.Features.EntityLabels;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class EntityLabelSettingsChecks
    {
        internal static void Check(IList<string> failures)
        {
            try
            {
                var defaults = EntityLabelSettings.Default;
                Require(!defaults.AnyEnabled, "first use must not enable any label");
                var typed = defaults.WithNpcMode(NpcLabelMode.Type).Toggle(EntityLabelKind.Npc);
                Require(typed.NpcMode == NpcLabelMode.Off && typed.LastNpcMode == NpcLabelMode.Type, "off must remember explicit type mode");
                Require(typed.Toggle(EntityLabelKind.Npc).NpcMode == NpcLabelMode.Type, "toggle must restore type rather than force name");
                Require(defaults.Toggle(EntityLabelKind.Npc).NpcMode == NpcLabelMode.Name, "first toggle has meaningful name mode");
                var changed = typed.WithStyle(EntityLabelKind.Enemy, new EntityLabelStyle(0x123456, 180));
                Require(changed.Style(EntityLabelKind.Enemy).HealthSize == 167, "large name must not accidentally make health 1.80");
                Require(defaults.Style(EntityLabelKind.Enemy).HealthSize == 57, "default health spacing uses 0.57");
                Require(new EntityLabelStyle(0, 50).HealthSize == 50, "health lower bound");
                var size = defaults.Style(EntityLabelKind.Critter);
                for (int i = 0; i < 30; i++) size = size.StepSize(1);
                Require(size.NameSize == 180, "size clamps upper bound without drift");
                for (int i = 0; i < 30; i++) size = size.StepSize(-1);
                Require(size.NameSize == 50, "size clamps lower bound without drift");
                Require(changed.Style(EntityLabelKind.Critter).Rgb == 0x5DADEC && changed.Style(EntityLabelKind.Npc).Rgb == 0x90EE90,
                    "enemy editing must not alter other styles");
                Require(changed.ResetStyle(EntityLabelKind.Enemy).Equals(typed), "reset only style, preserving mode memory");
                var codec = new EntityLabelCodec();
                Require(codec.Decode(codec.Encode(changed)).Equals(changed), "mode memory and style survive process restart");
                string json = Encoding.UTF8.GetString(codec.Encode(changed));
                Expect(codec, json.Replace("\"version\":1", "\"version\":9"), PreferenceStatus.UnsupportedVersion);
                Expect(codec, json.Replace("\"version\":1", "\"version\":1,\"future\":true"), PreferenceStatus.UnknownFields);
                Expect(codec, json.Replace("\"version\":1", "\"version\":1,\"version\":1"), PreferenceStatus.Invalid);
                Expect(codec, json.Replace("\"lastNpcMode\":2", "\"lastNpcMode\":0"), PreferenceStatus.Invalid);
                Expect(codec, json.Replace("\"nameSize\":180", "\"nameSize\":181"), PreferenceStatus.Invalid);
                Expect(codec, json.Replace("\"rgb\":1193046", "\"rgb\":16777216"), PreferenceStatus.Invalid);
                Expect(codec, json.Replace("\"nameSize\":180", "\"nameSize\":180,\"alpha\":1"), PreferenceStatus.UnknownFields);
            }
            catch (Exception e) { failures.Add("entity label settings: " + e.Message); }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Expect(EntityLabelCodec codec, string json, PreferenceStatus wanted)
        {
            try { codec.Decode(Encoding.UTF8.GetBytes(json)); }
            catch (PreferenceFormatException e) { Require(e.Status == wanted, "wrong protected-file reason: " + e.Status); return; }
            throw new InvalidOperationException("unsafe label settings accepted");
        }
    }
}
