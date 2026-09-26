using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Tools
{
    internal sealed class NetGeometry
    {
        private Player proxy;
        private readonly Rectangle[] hits=new Rectangle[128];
        private object asset;
        private Rectangle frame;
        private int count,type,direction,animation,width,height;
        private long tick=-1;
        private Vector2 position;
        private float gravity,scale,offset;
        private bool glove;
        internal static bool IsNet(Item item){return item!=null && item.stack>0 && (item.type==1991 || item.type==3183 || item.type==4821);}
        internal static int Frames(Player p,Item item)
        {return Math.Max(1,(int)(item.useAnimation*(item.melee && !ItemID.Sets.NoMeleeSpeedBonus[item.type]?p.meleeSpeed:1f)));}
        internal bool Hits(Player p,Item item,Rectangle target,long update)
        {
            int dir=p.direction;
            if(!Prepare(p,item,dir,update))return false;
            for(int i=0;i<count;i++)if(hits[i].Intersects(target))return true;return false;
        }
        private bool Prepare(Player p,Item item,int dir,long update)
        {
            if(!IsNet(item) || item.useStyle!=1)return false;
            object current=TextureAssets.Item[item.type];if(current==null)return false;
            if(type!=item.type || !ReferenceEquals(asset,current))
            {frame=Item.GetDrawHitbox(item.type,p);asset=current;type=item.type;tick=-1;}
            int frames=Frames(p,item);if(frames<2 || frames>hits.Length)return false;
            float mount=p.HeightOffsetHitboxCenter;
            if(tick==update && position==p.position && width==p.width && height==p.height && direction==dir && gravity==p.gravDir && scale==item.scale && glove==p.meleeScaleGlove && offset==mount && animation==frames)return true;
            if(proxy==null)proxy=new Player();
            proxy.position=p.position;proxy.width=p.width;proxy.height=p.height;proxy.direction=dir;proxy.gravDir=p.gravDir;proxy.meleeScaleGlove=p.meleeScaleGlove;
            proxy.itemAnimationMax=frames;count=0;
            // StartActualUse is followed by the animation decrement before
            // native catching: max and zero are never reachable catch frames.
            for(int phase=frames-1;phase>0;phase--)
            {
                proxy.itemAnimation=phase;proxy.ItemCheck_ApplyUseStyle(mount,item,frame);
                bool inactive;Rectangle hit;proxy.ItemCheck_GetMeleeHitbox(item,frame,out inactive,out hit);
                if(!inactive)hits[count++]=hit;
            }
            tick=update;position=p.position;width=p.width;height=p.height;direction=dir;gravity=p.gravDir;scale=item.scale;glove=p.meleeScaleGlove;offset=mount;animation=frames;return count>0;
        }
    }
}
