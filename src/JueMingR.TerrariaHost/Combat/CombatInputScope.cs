using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // A scope restores only still-owned temporary inputs. Native release and
    // channel state are results, and are deliberately never snapshotted here.
    internal sealed class CombatInputScope
    {
        private readonly Player player;
        private readonly bool oldUse,oldTile,oldMouse;
        private readonly bool use,tile,mouse,hasMouse;
        private bool ended,filledRelease,aim;
        private int oldX,oldY,ownX,ownY;
        internal CombatInputScope(Player player,bool press,bool right,bool mouseScope)
        {
            this.player=player;oldUse=player.controlUseItem;oldTile=player.controlUseTile;oldMouse=Main.mouseLeft;
            use=press;tile=right?false:oldTile;mouse=press;hasMouse=mouseScope;
        }
        internal void Apply()
        {player.controlUseItem=use;player.controlUseTile=tile;if(hasMouse)Main.mouseLeft=mouse;}
        internal void FillRelease(){if(!player.releaseUseItem){filledRelease=true;player.releaseUseItem=true;}}
        internal void ReleaseConsumed(){filledRelease=false;}
        internal void BorrowAim(Microsoft.Xna.Framework.Vector2 world)
        {
            var screen=Main.ReverseGravitySupport(world-Main.screenPosition);
            if(screen.X<int.MinValue || screen.X>int.MaxValue || screen.Y<int.MinValue || screen.Y>int.MaxValue)return;
            oldX=Main.mouseX;oldY=Main.mouseY;ownX=(int)screen.X;ownY=(int)screen.Y;aim=true;
            Main.mouseX=ownX;Main.mouseY=ownY;
        }
        internal void End(bool completed=false)
        {
            if(ended)return;ended=true;
            // Only our own ready-boundary repair is reversible. A completed
            // call or an actual native start has consumed it; native release
            // and channel results must otherwise remain untouched.
            if(!completed && filledRelease && player.releaseUseItem)player.releaseUseItem=false;
            if(aim){if(Main.mouseX==ownX)Main.mouseX=oldX;if(Main.mouseY==ownY)Main.mouseY=oldY;}
            if(player.controlUseItem==use)player.controlUseItem=oldUse;
            if(player.controlUseTile==tile)player.controlUseTile=oldTile;
            if(hasMouse && Main.mouseLeft==mouse)Main.mouseLeft=oldMouse;
        }
    }
}
