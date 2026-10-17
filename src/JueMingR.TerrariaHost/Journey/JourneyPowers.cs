using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Terraria;
using Terraria.GameContent.Creative;
using Terraria.IO;

namespace JueMingR.TerrariaHost.Journey
{
    // Immutable persistent substreams are captured by the native serializers
    // before activation. A background save never swaps live power singletons.
    internal sealed class JourneyPowers
    {
        internal readonly Player Player;
        internal readonly PlayerFileData PlayerFile;
        internal readonly WorldFileData WorldFile;
        internal readonly byte[] WorldBytes;
        internal readonly byte[] PlayerBytes;
        private readonly bool godmode;
        private readonly bool range;
        private readonly bool creativeGodmode;
        private readonly Slider[] sliders;
        private static readonly Action UpdateDifficulty = (Action)Delegate.CreateDelegate(typeof(Action),
            AccessTools.Method(typeof(Main), "UpdateCreativeGameModeOverride"));

        internal JourneyPowers(Player player, PlayerFileData playerFile, WorldFileData worldFile)
        {
            Player = player; PlayerFile = playerFile; WorldFile = worldFile;
            WorldBytes = Capture(w => CreativePowerManager.Instance.SaveToWorld(w));
            PlayerBytes = Capture(w => CreativePowerManager.Instance.SaveToPlayer(player, w));
            godmode = Power<CreativePowers.GodmodePower>().IsEnabledForPlayer(player.whoAmI);
            range = Power<CreativePowers.FarPlacementRangePower>().IsEnabledForPlayer(player.whoAmI);
            creativeGodmode = player.creativeGodMode;
            sliders = new[] { new Slider(Power<CreativePowers.ModifyTimeRate>()), new Slider(Power<CreativePowers.DifficultySliderPower>()),
                new Slider(Power<CreativePowers.ModifyWindDirectionAndStrength>()), new Slider(Power<CreativePowers.ModifyRainPower>()),
                new Slider(Power<CreativePowers.SpawnRateSliderPerPlayerPower>(), player.whoAmI) };
        }

        internal void InitializeNeutral()
        {
            if (Main.GameMode != 3)
            {
                // Native non-Journey saves retain a default range=true which
                // was previously gated off. Admitting the gate must not grant
                // range until the player explicitly chooses it.
                Power<CreativePowers.FarPlacementRangePower>().SetEnabledState(Player.whoAmI, false);
                float value = Main.GameMode == 2 ? 1f : Main.GameMode == 1 ? 0.665f : 0.33f;
                SetShared(Power<CreativePowers.DifficultySliderPower>(), value);
            }
            UpdateDifficulty();
        }

        internal void Restore()
        {
            using (var stream = new MemoryStream(WorldBytes, false))
            using (var reader = new BinaryReader(stream)) CreativePowerManager.Instance.LoadFromWorld(reader, 326);
            Power<CreativePowers.GodmodePower>().SetEnabledState(Player.whoAmI, godmode);
            Power<CreativePowers.FarPlacementRangePower>().SetEnabledState(Player.whoAmI, range);
            foreach (Slider slider in sliders) slider.Restore();
            Player.creativeGodMode = creativeGodmode;
            // Wind/rain slider caches are restored without calling their apply
            // methods: actual weather already caused by the player is retained.
            UpdateDifficulty();
        }

        internal static T Power<T>() where T : class, ICreativePower
        { return CreativePowerManager.Instance.GetPower<T>() ?? throw new InvalidOperationException("Missing native Journey power: " + typeof(T).Name); }
        private static byte[] Capture(Action<BinaryWriter> write)
        { using (var stream = new MemoryStream()) { using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) write(writer); return stream.ToArray(); } }
        private static void SetShared(CreativePowers.ASharedSliderPower power, float value)
        {
            Slider.SharedCache.SetValue(power, value); Slider.SharedTarget.SetValue(power, value); Slider.SharedPending.SetValue(power, false);
            AccessTools.Method(power.GetType(), "UpdateInfoFromSliderValueCache").Invoke(power, null);
        }

        private sealed class Slider
        {
            internal static readonly FieldInfo SharedCache = AccessTools.Field(typeof(CreativePowers.ASharedSliderPower), "_sliderCurrentValueCache");
            internal static readonly FieldInfo SharedTarget = AccessTools.Field(typeof(CreativePowers.ASharedSliderPower), "_currentTargetValue");
            internal static readonly FieldInfo SharedPending = AccessTools.Field(typeof(CreativePowers.ASharedSliderPower), "_needsToCommitChange");
            private static readonly FieldInfo PlayerCache = AccessTools.Field(typeof(CreativePowers.APerPlayerSliderPower), "_sliderCurrentValueCache");
            private static readonly FieldInfo PlayerTarget = AccessTools.Field(typeof(CreativePowers.APerPlayerSliderPower), "_currentTargetValue");
            private static readonly FieldInfo PlayerPending = AccessTools.Field(typeof(CreativePowers.APerPlayerSliderPower), "_needsToCommitChange");
            private static readonly FieldInfo PlayerValues = AccessTools.Field(typeof(CreativePowers.APerPlayerSliderPower), "_cachePerPlayer");
            private readonly object power;
            private readonly FieldInfo cache;
            private readonly FieldInfo target;
            private readonly FieldInfo pending;
            private readonly float value;
            private readonly float targetValue;
            private readonly int playerIndex;
            private readonly float playerValue;
            internal Slider(object power, int playerIndex = -1)
            {
                this.power = power; this.playerIndex = playerIndex;
                cache = playerIndex < 0 ? SharedCache : PlayerCache;
                target = playerIndex < 0 ? SharedTarget : PlayerTarget;
                pending = playerIndex < 0 ? SharedPending : PlayerPending;
                value = (float)cache.GetValue(power); targetValue = (float)target.GetValue(power);
                if (playerIndex >= 0) playerValue = ((float[])PlayerValues.GetValue(power))[playerIndex];
            }
            internal void Restore()
            {
                cache.SetValue(power, value); target.SetValue(power, targetValue); pending.SetValue(power, false);
                if (playerIndex >= 0) ((float[])PlayerValues.GetValue(power))[playerIndex] = playerValue;
            }
        }
    }
}
