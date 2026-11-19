using Terraria;
using Terraria.GameContent.Achievements;

namespace JueMingR.TerrariaHost.Companions
{
    internal static partial class CompanionHooks
    {
        private static void InstallAchievements()
        {
            var helper = typeof(AchievementsHelper);
            Patch(helper, nameof(AchievementsHelper.NotifyItemPickup), nameof(PersonalAchievement), args: new[] { typeof(Player), typeof(Item) });
            Patch(helper, nameof(AchievementsHelper.NotifyItemPickup), nameof(PersonalAchievement), args: new[] { typeof(Player), typeof(Item), typeof(int) });
            Patch(helper, nameof(AchievementsHelper.NotifyTileDestroyed), nameof(PersonalAchievement));
            Patch(helper, nameof(AchievementsHelper.NotifyNPCKilledDirect), nameof(PersonalAchievement));
            Patch(helper, nameof(AchievementsHelper.HandleOnEquip), nameof(PersonalAchievement));
            Patch(helper, nameof(AchievementsHelper.HandleSpecialEvent), nameof(SpecialAchievement));
            Patch(helper, nameof(AchievementsHelper.HandleRunning), nameof(LocalAchievement));
            Patch(helper, nameof(AchievementsHelper.HandleMining), nameof(LocalAchievement));
            Patch(helper, nameof(AchievementsHelper.PlantedAcorn), nameof(LocalAchievement));
            Patch(helper, nameof(AchievementsHelper.NotifyItemCraft), nameof(LocalAchievement));
            Patch(helper, nameof(AchievementsHelper.HandleAnglerService), nameof(LocalAchievement));
            Patch(helper, nameof(AchievementsHelper.CheckResearchAchievement), nameof(LocalAchievement));
            Patch(helper, nameof(AchievementsHelper.NotifyProgressionEvent), nameof(ProgressionAchievement));
        }

        // The helpers normally trust Main.myPlayer or a listener does. An
        // AI lends that index to native actions, but owns no achievement data.
        private static bool PersonalAchievement(Player __0) { return host?.Owned(__0) == null; }
        private static bool LocalAchievement() { return current == null; }
        private static bool SpecialAchievement(Player __0, int __1)
        {
            // 27 reports the actual world's purification, independently of
            // which local body caused the final block to change.
            return __1 == 27 || PersonalAchievement(__0);
        }

        private static bool ProgressionAchievement(int __0)
        {
            if (current == null) return true;
            // Native also uses the progression outlet for these personal
            // milestones (attire, survival, music, equipment, actions and
            // research). Other progression events report shared world facts.
            switch (__0)
            {
                case 33: case 35: case 36: case 37: case 38: case 40: case 41: case 42: case 44: case 45:
                    return false;
                default:
                    return true;
            }
        }
    }
}
