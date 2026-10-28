using System;

namespace JueMingR.Features.Blueprint
{
    public enum BlueprintSpecialProducerKind { TileWand, Flexible }

    // Execution identity is deliberately separate from the nominal bill. A
    // non-consuming flexible bucket still has to exist, and two declarations
    // with the same bill need not select the same native variant.
    public sealed class BlueprintSpecialProducer
    {
        public BlueprintSpecialProducer(BlueprintSpecialProducerKind kind, int bucketItemType,
            bool consumesBucket, bool consumesHeld, int optionIndex = 0, int optionCount = 1,
            int randomModulus = 1, int randomResidue = 0, int nativeRandom = 0)
        {
            Kind = kind; BucketItemType = bucketItemType; ConsumesBucket = consumesBucket; ConsumesHeld = consumesHeld;
            OptionIndex = optionIndex; OptionCount = optionCount; RandomModulus = randomModulus;
            RandomResidue = randomResidue; NativeRandom = nativeRandom;
        }
        public BlueprintSpecialProducerKind Kind { get; }
        public int BucketItemType { get; }
        public bool ConsumesBucket { get; }
        public bool ConsumesHeld { get; }
        public int OptionIndex { get; }
        public int OptionCount { get; }
        public int RandomModulus { get; }
        public int RandomResidue { get; }
        public int NativeRandom { get; }
        public BlueprintSpecialProducer WithRandom(int modulus, int residue, int nativeRandom)
        { return new BlueprintSpecialProducer(Kind, BucketItemType, ConsumesBucket, ConsumesHeld, OptionIndex, OptionCount, modulus, residue, nativeRandom); }

        // One native cycle selects both the bucket option and TileObject's
        // random substyle. Solve their joint period without calling CanPlace
        // repeatedly or consuming/resetting a game RNG.
        public bool TryCycle(int current, out int cycle)
        {
            cycle = current;
            if (OptionCount <= 0 || OptionIndex < 0 || OptionIndex >= OptionCount || RandomModulus <= 0 ||
                RandomResidue < 0 || RandomResidue >= RandomModulus) return false;
            int gcd = Gcd(OptionCount, RandomModulus);
            if ((OptionIndex - RandomResidue) % gcd != 0) return false;
            long period = (long)OptionCount / gcd * RandomModulus;
            if (period > int.MaxValue) return false;
            long first = -1;
            for (int t = 0; t < RandomModulus / gcd; t++)
            {
                long candidate = OptionIndex + (long)t * OptionCount;
                if (candidate % RandomModulus == RandomResidue) { first = candidate; break; }
            }
            if (first < 0) return false;
            long q = ((long)current - first) / period;
            long best = 0, distance = long.MaxValue;
            for (long k = q - 1; k <= q + 1; k++)
            {
                long candidate = first + k * period;
                if (candidate < int.MinValue || candidate > int.MaxValue) continue;
                long delta = Math.Abs(candidate - current);
                if (delta < distance || delta == distance && candidate < best) { best = candidate; distance = delta; }
            }
            if (distance == long.MaxValue) return false;
            cycle = (int)best; return true;
        }
        private static int Gcd(int a, int b) { while (b != 0) { int next = a % b; a = b; b = next; } return a; }
    }

    // Only recipes whose real Player/WorldGen consumers are known belong here.
    // The base is placed at the final target cell, never as outside support.
    public sealed class BlueprintSubstrateRecipe
    {
        private BlueprintSubstrateRecipe(int target, int basis, int baseItem, int conversion)
        { TargetTile = target; BaseTile = basis; BaseItem = baseItem; ConversionItem = conversion; }
        public int TargetTile { get; }
        public int BaseTile { get; }
        public int BaseItem { get; }
        public int ConversionItem { get; }
        private static readonly BlueprintSubstrateRecipe[] recipes = {
            new BlueprintSubstrateRecipe(2, 0, 2, 62), new BlueprintSubstrateRecipe(23, 0, 2, 59),
            new BlueprintSubstrateRecipe(199, 0, 2, 2171), new BlueprintSubstrateRecipe(109, 0, 2, 369),
            new BlueprintSubstrateRecipe(60, 59, 176, 195), new BlueprintSubstrateRecipe(70, 59, 176, 194),
            new BlueprintSubstrateRecipe(661, 59, 176, 59), new BlueprintSubstrateRecipe(662, 59, 176, 2171),
            new BlueprintSubstrateRecipe(633, 57, 172, 5214)
        };
        public static bool TryGet(int targetTile, out BlueprintSubstrateRecipe recipe)
        {
            foreach (var value in recipes) if (value.TargetTile == targetTile) { recipe = value; return true; }
            recipe = null; return false;
        }
    }
}
