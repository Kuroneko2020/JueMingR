using Terraria;
using Terraria.ID;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.Combat
{
    internal enum ReleaseMechanism { None, ItemRelease, Flint, Glacier }
    internal static class WeaponCatalog
    {
        // Fixed .8 mechanisms, verified against ItemCheck and the corresponding
        // projectile AI. This is not a reflection scan or a name-based catalog.
        internal static ReleaseMechanism Release(Item item)
        {
            if(item==null || item.type<=0 || item.stack<=0)return ReleaseMechanism.None;
            switch(item.type)
            {
                case 198:case 199:case 200:case 201:case 202:case 203:case 4258:case 5535:case 5670:
                case 3764:case 3765:case 3766:case 3767:case 3768:case 3769:case 4259:case 5536:case 5671:
                case 671:case 3772:case 3352:return ReleaseMechanism.ItemRelease;
                case 5462:return ReleaseMechanism.Flint;
                case 6153:return ReleaseMechanism.Glacier;
                default:return ReleaseMechanism.None;
            }
        }
        internal static bool Switchable(Item item){return Release(item)!=ReleaseMechanism.None;}
        internal static bool AutoClick(Item item)
        {
            return item!=null && item.type>0 && item.stack>0 && item.useStyle>0 && item.useAnimation>0 && item.useTime>0 &&
                !item.channel && !ItemID.Sets.ShootsOnUseRelease[item.type] && item.fishingPole<=0 && item.pick<=0 && item.axe<=0 && item.hammer<=0 && item.type!=2269;
        }
        internal static bool Yoyo(Item item)
        {
            if(item==null || item.type<=0 || item.stack<=0)return false;
            int id=item.type,shot=item.shoot;
            return id==3262 || id>=3278 && id<=3292 || id>=3315 && id<=3317 || id==3389 || id==5294 ||
                shot==534 || shot>=541 && shot<=555 || shot>=562 && shot<=564 || shot==603 || shot==999;
        }
        internal static bool Flail(Item item)
        {
            if(item==null || item.type<=0 || item.stack<=0 || item.damage<=0 && item.type!=905 || item.createTile>=0 || item.createWall>=0 ||
                item.sentry || item.summon && item.buffType>0 || item.pick>0 || item.axe>0 || item.hammer>0 || item.fishingPole>0 || item.ammo>0 ||
                !item.channel || item.shoot<=0 || item.useAmmo!=0 || Yoyo(item))return false;
            Projectile sample;
            return ContentSamples.ProjectilesByType.TryGetValue(item.shoot,out sample) && sample.aiStyle==15;
        }
        internal static bool RightAvailable(Player player,Item item)
        {
            return !Main.SmartInteractShowingGenuine && Main.SmartInteractNPC==-1 && Main.SmartInteractProj==-1 &&
                !player.tileInteractionHappened && player.altFunctionUse==0 && !ItemID.Sets.HasRightFire[item.type] && !ItemID.Sets.ItemsThatAllowRepeatedRightClick[item.type];
        }
        internal static bool Ready(Player player)
        {return player.itemAnimation<=0 && player.itemTime<=0 && player.reuseDelay<=0 && !player.delayUseItem;}
    }
}
