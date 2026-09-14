using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Guidance;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeGuidanceEquipmentChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        internal static void Run(Assembly assembly)
        {
            // Expectations name native constants established from the contract's
            // bilingual aliases, rather than reading the production numeric table.
            string names = "AncientChisel ExtendoGrip Toolbelt Toolbox BrickLayer PortableCementMixer PaintSprayer ArchitectGizmoPack ActuationAccessory SpectreGoggles HandOfCreation JimsDroneVisor PortableStool TreasureMagnet HighTestFishingLine TackleBox AnglerEarring AnglerTackleBag LavaFishingHook LavaproofTackleBag FishingBobber FishingBobberGlowingStar FishingBobberGlowingLava FishingBobberGlowingRainbow FishingBobberGlowingViolet FishingBobberGlowingArgon FishingBobberGlowingKrypton FishingBobberGlowingXenon CopperWatch TinWatch SilverWatch TungstenWatch GoldWatch PlatinumWatch DepthMeter Compass GPS FishermansGuide WeatherRadio Sextant FishFinder MetalDetector Stopwatch DPSMeter GoblinTech LifeformAnalyzer TallyCounter Radar REK PDA LaserRuler MechanicalLens GuideVoodooDoll ClothierVoodooDoll DiscountCard LuckyCoin GoldRing CoinRing GreedyRing FlowerBoots CordageGuide GolfBall JellyfishNecklace MusicBox DontStarveShaderItem FlameWakerBoots AnglerHat AnglerVest AnglerPants MiningHelmet UltrabrightHelmet NightVisionHelmet DivingHelmet MiningShirt MiningPants";
            var expected = new HashSet<int>();
            foreach (string name in names.Split(' ')) expected.Add(Convert.ToInt32(typeof(ItemID).GetField(name).GetRawConstantValue()));
            foreach (var field in typeof(ItemID).GetFields(BindingFlags.Public | BindingFlags.Static))
                if (field.IsLiteral && field.Name.StartsWith("MusicBox", StringComparison.Ordinal)) expected.Add(Convert.ToInt32(field.GetRawConstantValue()));
            Require(expected.Count == 176, "independent locked native constants must reproduce all 176 identities");
            for (int i = 0; i < ItemID.Count; i++) Require(EquipmentRules.IsNonCombat(i) == expected.Contains(i), "native full identity mapping differs at " + i);

            var original = Main.LocalPlayer; var world = Main.ActiveWorldFileData;
            var difficultyField = typeof(Main).GetField("_gameModeDifficultyOverride", Flags); object difficulty = difficultyField.GetValue(null);
            bool good = Main.getGoodWorld;
            object reader = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Guidance.EffectiveEquipmentReader", true), true);
            try
            {
                difficultyField.SetValue(null, null); Main.getGoodWorld = false; world.GameMode = 0;
                var p = new Player { active = true }; Main.player[Main.myPlayer] = p;
                p.inventory[0] = Accessory(ItemID.Toolbelt); p.armor[13] = Accessory(ItemID.TreasureMagnet);
                Read(reader, p); Require(!Has(reader, ItemID.Toolbelt) && !Has(reader, ItemID.TreasureMagnet), "inventory/vanity cannot become functional wear");
                p.armor[13].TurnToAir(); var shared = Accessory(ItemID.Toolbelt); shared.favorited = true; p.Loadouts[1].Armor[3] = shared;
                Read(reader, p); Require(Has(reader, ItemID.Toolbelt) && ReferenceEquals(p.GetEffectiveArmor(3), shared), "actual favorite cross-loadout share reaches production adapter");
                shared.favorited = false; Read(reader, p); Require(!Has(reader, ItemID.Toolbelt), "unfavorited other loadout is not worn");
                shared.favorited = true; p.armor[13] = Accessory(ItemID.Toolbelt); Read(reader, p); Require(!Has(reader, ItemID.Toolbelt), "native vanity conflict blocks sharing without counting vanity itself");
                p.armor[13].TurnToAir(); p.armor[3] = Accessory(ItemID.TreasureMagnet); Read(reader, p);
                Require(Has(reader, ItemID.TreasureMagnet) && !Has(reader, ItemID.Toolbelt), "current functional item takes precedence over shared favorite");
                p.Loadouts[1].Armor[3].TurnToAir(); p.armor[3].TurnToAir(); p.armor[8] = Accessory(ItemID.Toolbelt); p.armor[9] = Accessory(ItemID.TreasureMagnet); p.extraAccessory = true;
                Read(reader, p); Require(!Has(reader, ItemID.Toolbelt) && !Has(reader, ItemID.TreasureMagnet), "Classic extra slots excluded");
                world.GameMode = 1; Read(reader, p); Require(Has(reader, ItemID.Toolbelt) && !Has(reader, ItemID.TreasureMagnet), "Expert unlocked slot8 only");
                world.GameMode = 2; Read(reader, p); Require(Has(reader, ItemID.Toolbelt) && Has(reader, ItemID.TreasureMagnet), "Master slot9 functional");
                p.extraAccessory = false; Read(reader, p); Require(!Has(reader, ItemID.Toolbelt), "extra slot still requires unlock");
                world.GameMode = 0; p.armor[3] = Accessory(ItemID.Toolbelt); p.armor[3].expertOnly = true; Read(reader, p); Require(!Has(reader, ItemID.Toolbelt), "expertOnly gate is independent of GetEffectiveArmor");
                p.armor[3].expertOnly = false; p.armor[3].accessory = false; Read(reader, p); Require(!Has(reader, ItemID.Toolbelt), "invalid role cannot grant accessory benefit");
                p.armor[0] = new Item { type = ItemID.MiningHelmet, stack = 1, headSlot = 1, bodySlot = -1, legSlot = -1 };
                Read(reader, p); Require(Has(reader, ItemID.MiningHelmet), "functional head item included"); p.armor[0].headSlot = -1; Read(reader, p); Require(!Has(reader, ItemID.MiningHelmet), "invalid head role excluded");
                Require(p.CurrentLoadoutIndex == 0 && ReferenceEquals(p.armor[3], p.GetEffectiveArmor(3)), "observation does not switch or replace equipment");
            }
            finally { Main.player[Main.myPlayer] = original; world.GameMode = 0; Main.getGoodWorld = good; difficultyField.SetValue(null, difficulty); }
            Console.WriteLine("PASS: full 176-ID native mapping; actual unlocked/effective/favorite/shared-conflict/role/difficulty equipment adapter.");
        }
        internal static Item Accessory(int type) { return new Item { type = type, stack = 1, accessory = true, headSlot = -1, bodySlot = -1, legSlot = -1 }; }
        private static void Read(object reader, Player player) { Require((bool)Call(reader, "Read", player), "actual equipment input readable"); }
        private static bool Has(object reader, int type)
        { var types = (int[])Get(reader, "Types"); int count = (int)Get(reader, "Count"); for (int i = 0; i < count; i++) if (types[i] == type) return true; return false; }
    }
}
