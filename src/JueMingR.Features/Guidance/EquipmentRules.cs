using System;

namespace JueMingR.Features.Guidance
{
    public static class EquipmentRules
    {
        // Terraria 1.4.5.8 ItemID + embedded en-US/zh-Hans ItemName mapping.
        // 153 historical aliases collapse to 75 entries. Radio aliases 5113;
        // Mechanical Ruler is 2799, not Ruler 486. Music boxes use native family
        // identities (including 27 Otherworld boxes), never resource-pack names.
        private static readonly int[] fixedTypes = {
            4056,2215,407,1923,2214,2217,2216,3061,3624,4409,5126,5452,4341,5010,
            2373,2375,2374,3721,4881,5064,5139,5140,5141,5146,5145,5144,5142,5143,
            15,707,16,708,17,709,18,393,395,3120,3037,3096,3036,3102,3099,3119,3121,
            3118,3095,3084,3122,3123,2799,3619,267,1307,854,855,3033,3034,3035,3017,
            3068,3989,1303,576,5113,4822,2367,2368,2369,88,4008,3109,268,410,411,
            1963,1964,1965,2742,3044,3235,3236,3237,3370,3371,3796,3869,
            4237,4356,4357,4358,4421,4606,4979,4985,4990,4991,4992,5006,5044,5112,
            5362,5538,5539,5578,5579,5580,5581,5582,5637,5638,5639,6144,6145,6146
        };
        static EquipmentRules() { Array.Sort(fixedTypes); }
        public static bool IsNonCombat(int type)
        { return type >= 562 && type <= 574 || type >= 1596 && type <= 1610 || type >= 4077 && type <= 4082 || type >= 5014 && type <= 5040 || Array.BinarySearch(fixedTypes, type) >= 0; }
    }
}
