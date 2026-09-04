using System;
using System.Collections.Generic;
using JueMingR.Platform.Biomes;
using JueMingR.Platform.Runtime;

namespace JueMingR.Features.Biomes
{
    public sealed class BiomeDisplayFeature : IRuntimeFeature
    {
        private const ulong ObservationIntervalTicks = 30;

        private readonly IBiomeObservationSource observationSource;
        private bool enabled;
        private bool sessionActive;
        private bool observeImmediately;
        private bool hasObservationTick;
        private ulong lastObservationTick;
        private bool hasObservation;
        private BiomeObservation lastObservation;
        private BiomeDisplayViewModel currentViewModel = BiomeDisplayViewModel.Hidden;

        public BiomeDisplayFeature(IBiomeObservationSource observationSource, bool enabled)
        {
            this.observationSource = observationSource ??
                throw new ArgumentNullException(nameof(observationSource));
            this.enabled = enabled;
            observeImmediately = enabled;
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public BiomeDisplayViewModel CurrentViewModel
        {
            get { return currentViewModel; }
        }

        public void SetEnabled(bool value)
        {
            if (enabled == value)
            {
                return;
            }

            enabled = value;
            ClearObservationState();
            observeImmediately = value && sessionActive;
        }

        public void OnSessionStarted()
        {
            sessionActive = true;
            ClearObservationState();
            observeImmediately = enabled;
        }

        public void OnSessionEnded()
        {
            sessionActive = false;
            ClearObservationState();
            observeImmediately = false;
        }

        public void Update(ulong updateTick)
        {
            if (!enabled || !sessionActive)
            {
                return;
            }

            if (!observeImmediately &&
                hasObservationTick &&
                unchecked(updateTick - lastObservationTick) < ObservationIntervalTicks)
            {
                return;
            }

            observeImmediately = false;
            hasObservationTick = true;
            lastObservationTick = updateTick;

            BiomeObservation observation;
            if (!observationSource.TryObserve(out observation))
            {
                hasObservation = false;
                PublishHidden();
                return;
            }

            if (hasObservation && observation == lastObservation)
            {
                return;
            }

            hasObservation = true;
            lastObservation = observation;
            string text = BuildText(observation.Flags);
            if (currentViewModel.Visible &&
                String.Equals(currentViewModel.Text, text, StringComparison.Ordinal))
            {
                return;
            }

            currentViewModel = new BiomeDisplayViewModel(true, text);
        }

        public void FailClosed()
        {
            enabled = false;
            sessionActive = false;
            observeImmediately = false;
            ClearObservationState();
        }

        private static string BuildText(BiomeFlags flags)
        {
            var names = new List<string>(18);
            Add(names, flags, BiomeFlags.Desert, "沙漠");
            Add(names, flags, BiomeFlags.UndergroundDesert, "地下沙漠");
            Add(names, flags, BiomeFlags.Snow, "雪原");
            Add(names, flags, BiomeFlags.Jungle, "丛林");
            Add(names, flags, BiomeFlags.Dungeon, "地牢");
            Add(names, flags, BiomeFlags.Beach, "海洋");
            Add(names, flags, BiomeFlags.Corrupt, "腐化");
            Add(names, flags, BiomeFlags.Crimson, "猩红");
            Add(names, flags, BiomeFlags.Hallow, "神圣");
            Add(names, flags, BiomeFlags.Holy, "神圣");
            Add(names, flags, BiomeFlags.Glowshroom, "发光蘑菇");
            Add(names, flags, BiomeFlags.Meteor, "陨石");
            Add(names, flags, BiomeFlags.Granite, "花岗岩");
            Add(names, flags, BiomeFlags.Marble, "大理石");
            Add(names, flags, BiomeFlags.Hive, "蜂巢");
            Add(names, flags, BiomeFlags.LihzhardTemple, "神庙");
            Add(names, flags, BiomeFlags.Graveyard, "墓地");

            if (Has(flags, BiomeFlags.Sky))
            {
                AddUnique(names, "天空");
            }
            else if (Has(flags, BiomeFlags.Underworld))
            {
                AddUnique(names, "地狱");
            }
            else if (Has(flags, BiomeFlags.RockLayer))
            {
                AddUnique(names, "洞穴");
            }
            else if (Has(flags, BiomeFlags.DirtLayer) || Has(flags, BiomeFlags.BelowSurface))
            {
                AddUnique(names, "地下");
            }
            else if (Has(flags, BiomeFlags.Overworld) && names.Count == 0)
            {
                names.Add("森林");
            }

            return "群系: " + (names.Count == 0 ? "N/A" : String.Join(" / ", names.ToArray()));
        }

        private static bool Has(BiomeFlags flags, BiomeFlags value)
        {
            return (flags & value) != 0;
        }

        private static void Add(
            ICollection<string> names,
            BiomeFlags flags,
            BiomeFlags value,
            string displayName)
        {
            if (Has(flags, value))
            {
                AddUnique(names, displayName);
            }
        }

        private static void AddUnique(ICollection<string> names, string displayName)
        {
            foreach (string existing in names)
            {
                if (String.Equals(existing, displayName, StringComparison.Ordinal))
                {
                    return;
                }
            }

            names.Add(displayName);
        }

        private void ClearObservationState()
        {
            hasObservationTick = false;
            lastObservationTick = 0;
            hasObservation = false;
            lastObservation = default(BiomeObservation);
            PublishHidden();
        }

        private void PublishHidden()
        {
            currentViewModel = BiomeDisplayViewModel.Hidden;
        }
    }
}
