#if PHASE0T_MAPPING_TEST || PHASE0T_LIFECYCLE_TEST || PHASE0T_ALL_TESTS
using System;
using System.Collections.Generic;
using JueMingR.Features.Biomes;
using JueMingR.Platform.Biomes;
using JueMingR.Platform.Runtime;

namespace JueMingR.ArchitectureTests
{
    internal static class BiomeFeatureChecks
    {
#if PHASE0T_MAPPING_TEST || PHASE0T_ALL_TESTS
        internal static void CheckMapping(IList<string> failures)
        {
            AssertText(
                BiomeFlags.Desert |
                BiomeFlags.UndergroundDesert |
                BiomeFlags.Snow |
                BiomeFlags.Jungle |
                BiomeFlags.Dungeon |
                BiomeFlags.Beach |
                BiomeFlags.Corrupt |
                BiomeFlags.Crimson |
                BiomeFlags.Hallow |
                BiomeFlags.Holy |
                BiomeFlags.Glowshroom |
                BiomeFlags.Meteor |
                BiomeFlags.Granite |
                BiomeFlags.Marble |
                BiomeFlags.Hive |
                BiomeFlags.LihzhardTemple |
                BiomeFlags.Graveyard |
                BiomeFlags.RockLayer,
                "群系: 沙漠 / 地下沙漠 / 雪原 / 丛林 / 地牢 / 海洋 / 腐化 / 猩红 / 神圣 / 发光蘑菇 / 陨石 / 花岗岩 / 大理石 / 蜂巢 / 神庙 / 墓地 / 洞穴",
                "ordered special biomes, Chinese de-duplication, and cave height",
                failures);
            AssertText(BiomeFlags.Overworld, "群系: 森林", "plain overworld forest", failures);
            AssertText(
                BiomeFlags.Desert | BiomeFlags.Overworld,
                "群系: 沙漠",
                "special surface biome does not append forest",
                failures);
            AssertText(
                BiomeFlags.Sky | BiomeFlags.Underworld | BiomeFlags.RockLayer | BiomeFlags.DirtLayer,
                "群系: 天空",
                "mutually exclusive height priority",
                failures);
            AssertText(BiomeFlags.BelowSurface, "群系: 地下", "shopping below-surface fallback", failures);
            AssertText(BiomeFlags.None, "群系: N/A", "valid player with no recognized biome", failures);
        }

        private static void AssertText(
            BiomeFlags flags,
            string expected,
            string label,
            IList<string> failures)
        {
            var source = new FakeBiomeSource(new BiomeObservation(flags));
            var feature = new BiomeDisplayFeature(source, true);
            feature.OnSessionStarted();
            feature.Update(0);
            if (!feature.CurrentViewModel.Visible ||
                !String.Equals(feature.CurrentViewModel.Text, expected, StringComparison.Ordinal))
            {
                failures.Add("Biome mapping failed for " + label + ".");
            }
        }
#endif

#if PHASE0T_LIFECYCLE_TEST || PHASE0T_ALL_TESTS
        internal static void CheckLifecycle(IList<string> failures)
        {
            var session = new FakeSessionProbe();
            var source = new FakeBiomeSource(new BiomeObservation(BiomeFlags.Desert));
            var feature = new BiomeDisplayFeature(source, true);
            var runtime = new SingleFeatureRuntime(session, feature);

            runtime.Update(0);
            AssertLifecycle(source.ReadCount == 0 && !feature.CurrentViewModel.Visible,
                "inactive world must not observe or show", failures);

            session.Active = true;
            runtime.Update(1);
            AssertLifecycle(source.ReadCount == 1 && feature.CurrentViewModel.Visible &&
                feature.CurrentViewModel.Text == "群系: 沙漠",
                "session enter must observe immediately", failures);

            source.Next = new BiomeObservation(BiomeFlags.Snow);
            for (ulong tick = 2; tick < 31; tick++)
            {
                runtime.Update(tick);
            }
            AssertLifecycle(source.ReadCount == 1 && feature.CurrentViewModel.Text == "群系: 沙漠",
                "equivalent observation must not run within 30 ticks", failures);
            runtime.Update(31);
            AssertLifecycle(source.ReadCount == 2 && feature.CurrentViewModel.Text == "群系: 雪原",
                "30-tick boundary must publish changed observation", failures);

            BiomeDisplayViewModel unchanged = feature.CurrentViewModel;
            runtime.Update(61);
            AssertLifecycle(source.ReadCount == 3 && Object.ReferenceEquals(unchanged, feature.CurrentViewModel),
                "unchanged observation must reuse ViewModel", failures);

            session.Active = false;
            runtime.Update(62);
            AssertLifecycle(!feature.CurrentViewModel.Visible && feature.CurrentViewModel.Text.Length == 0,
                "session exit must clear the old world model", failures);
            session.Active = true;
            runtime.Update(63);
            AssertLifecycle(source.ReadCount == 4 && feature.CurrentViewModel.Visible,
                "session re-entry must observe immediately", failures);

            feature.SetEnabled(false);
            int readsBeforeDisabledTick = source.ReadCount;
            runtime.Update(64);
            AssertLifecycle(!feature.CurrentViewModel.Visible && source.ReadCount == readsBeforeDisabledTick,
                "disabled feature must not observe or show", failures);
            feature.SetEnabled(true);
            runtime.Update(65);
            AssertLifecycle(source.ReadCount == readsBeforeDisabledTick + 1 && feature.CurrentViewModel.Visible,
                "feature enable must observe immediately", failures);

            source.ThrowOnRead = true;
            runtime.Update(95);
            AssertLifecycle(!feature.Enabled && !feature.CurrentViewModel.Visible,
                "observation error must disable only the feature and clear the model", failures);
        }

        private static void AssertLifecycle(bool condition, string label, IList<string> failures)
        {
            if (!condition)
            {
                failures.Add("Biome lifecycle failed: " + label + ".");
            }
        }
#endif

        private sealed class FakeBiomeSource : IBiomeObservationSource
        {
            internal FakeBiomeSource(BiomeObservation next)
            {
                Next = next;
            }

            internal BiomeObservation Next { get; set; }

            internal int ReadCount { get; private set; }

            internal bool ThrowOnRead { get; set; }

            public bool TryObserve(out BiomeObservation observation)
            {
                ReadCount++;
                if (ThrowOnRead)
                {
                    throw new InvalidOperationException("controlled observation failure");
                }

                observation = Next;
                return true;
            }
        }

#if PHASE0T_LIFECYCLE_TEST || PHASE0T_ALL_TESTS
        private sealed class FakeSessionProbe : IGameSessionProbe
        {
            internal bool Active { get; set; }

            public bool IsSessionActive
            {
                get { return Active; }
            }
        }
#endif
    }
}
#endif
