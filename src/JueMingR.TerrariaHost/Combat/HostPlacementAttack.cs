using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Locked .8 summon hand inputs: cursor placements and Clay Pot's distinct
    // directed birth. This is not a blanket rule for summon damage. The born
    // creature/turret owns later targeting; no spawn point is an AttackContact.
    internal static class HostPlacementAttack
    {
        internal static bool Handles(Item item)
        {
            if(item==null || item.IsAir || item.shoot<=0 || item.damage<=0)return false;
            switch(item.type)
            {
                case 1157:case 1309:case 1802:case 2364:case 2365:case 2535:case 2551:case 2584:
                case 2621:case 2749:case 3249:case 3474:case 3531:case 4269:case 4273:case 4281:
                case 4607:case 4758:case 5005:case 5069:case 5114:case 5456:case 5663:case 5664:
                case 6148:case 6149:case 6161:case 6164:
                case 1572:case 2366:case 3569:case 3571:case 5119:case 5463:
                case 3818:case 3819:case 3820:case 3824:case 3825:case 3826:
                case 3829:case 3830:case 3831:case 3832:case 3833:case 3834:return true;
                default:return false;
            }
        }
        internal static bool ReverseGravity(Item item)
        {return item.type==6161 || item.type==1572 || item.type==2366 || item.type==3569 || item.type==3571 || item.type==5119;}
        internal static bool DirectedBirth(Item item){return item.type==6161;}
        internal static bool ReadsCursor(Player player,Item item)
        {
            if(!Handles(item) || player.altFunctionUse==2)return false;
            if(item.type!=3531)return true;
            // An existing dragon extends its real linked chain. It is not a
            // fresh cursor placement; examine it once at actual use entry.
            for(int i=0;i<Main.maxProjectiles;i++)
            {var q=Main.projectile[i];if(q.active && q.owner==player.whoAmI && (q.type==625 || q.type==628))return false;}
            return true;
        }
        internal static Vector2 Input(NPC target)
        {return target.Center+target.netOffset;}
    }
}
