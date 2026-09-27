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
        private readonly Rectangle[] hits=new Rectangle[3];
        private readonly bool[] enabled=new bool[3];
        private readonly int[] firstPhase=new int[3],lastPhase=new int[3];
        private object asset;
        private Rectangle frame;
        private int type,direction,animation,width,height;
        private bool ready;
        internal int ShapeVersion {get;private set;}
        private Vector2 position;
        private float gravity,scale,offset;
#if DEBUG
        internal long ShapeBuilds {get;private set;}
        internal long ApplyUseStyleCalls {get;private set;}
        internal long GetMeleeHitboxCalls {get;private set;}
        internal long Intersections {get;private set;}
        internal long FrameReads {get;private set;}
#endif
        internal static bool IsNet(Item item){return item!=null && item.stack>0 && (item.type==1991 || item.type==3183 || item.type==4821);}
        internal static int Frames(Player p,Item item)
        {return Math.Max(1,(int)(item.useAnimation*(item.melee && !ItemID.Sets.NoMeleeSpeedBonus[item.type]?p.meleeSpeed:1f)));}
        internal bool Hits(Player p,Item item,Rectangle target,long update)
        {
            int dir=p.direction;
            if(!Prepare(p,item,dir,update))return false;
            for(int i=0;i<3;i++)if(enabled[i] && Intersects(i,target))return true;return false;
        }
        private bool Intersects(int phase,Rectangle target)
        {
#if DEBUG
            Intersections++;
#endif
            return hits[phase].Intersects(target);
        }
        private static int Phase(int remaining,int maximum){return remaining<maximum*0.333?2:remaining<maximum*0.666?1:0;}
        // Query the actual future consumer phases, not the already elapsed
        // envelope. The first catch runs before this update's NPC movement.
        internal bool Opportunity(Player p,Item item,NPC target,int remaining)
        {
            if(!Prepare(p,item,p.direction,0))return false;
            int first=remaining>0?remaining:animation-1;
            Vector2 relative=target.velocity-p.velocity;
            for(int index=0;index<3;index++)
            {
                if(!enabled[index] || lastPhase[index]>first)continue;
                int start=Math.Max(0,first-firstPhase[index]),end=first-lastPhase[index];
                var begin=target.Hitbox;begin.X=(int)(target.position.X+relative.X*start);begin.Y=(int)(target.position.Y+relative.Y*start);
                if(relative==Vector2.Zero){if(Intersects(index,begin))return true;continue;}
                var finish=target.Hitbox;finish.X=(int)(target.position.X+relative.X*end);finish.Y=(int)(target.position.Y+relative.Y*end);
                if(!Intersects(index,Rectangle.Union(begin,finish)))continue;
                for(int step=start;step<=end;step++)
                {
                    var future=target.Hitbox;future.X=(int)(target.position.X+relative.X*step);future.Y=(int)(target.position.Y+relative.Y*step);
                    if(Intersects(index,future))return true;
                }
            }
            return false;
        }
        internal void Clear(){ready=false;asset=null;proxy=null;}
        private bool Prepare(Player p,Item item,int dir,long update)
        {
            if(!IsNet(item) || item.useStyle!=1)return false;
            var resource=TextureAssets.Item[item.type];if(resource==null){ready=false;return false;}
            // A carried net need not have been drawn. Let vanilla request its
            // resource once needed; an unready resource is never a cached miss.
            if(!resource.IsLoaded && Main.instance!=null)Main.instance.LoadItem(item.type);
            if(!resource.IsLoaded || resource.Value==null){ready=false;return false;}
            object current=resource.Value;Rectangle actualFrame=frame;
            // Native nets have static frames. Animated resources still ask
            // vanilla for their current frame; a new texture value is a real
            // invalidation even when its Asset wrapper has not changed.
            if(!ready || type!=item.type || !ReferenceEquals(asset,current) || Main.itemAnimations[item.type]!=null)
            {
                actualFrame=Item.GetDrawHitbox(item.type,p);
#if DEBUG
                FrameReads++;
#endif
            }
            int frames=Frames(p,item);if(frames<2 || frames>128)return false;
            float mount=p.HeightOffsetHitboxCenter;
            float adjusted=p.GetAdjustedItemScale(item);
            if(ready && type==item.type && ReferenceEquals(asset,current) && frame==actualFrame && position==p.position && width==p.width && height==p.height && direction==dir && gravity==p.gravDir && scale==adjusted && offset==mount && animation==frames)return true;
            if(!ready || type!=item.type || !ReferenceEquals(asset,current) || frame!=actualFrame || width!=p.width || height!=p.height || direction!=dir || gravity!=p.gravDir || scale!=adjusted || offset!=mount || animation!=frames)ShapeVersion++;
            frame=actualFrame;asset=current;type=item.type;
            if(proxy==null)proxy=new Player();
            proxy.position=p.position;proxy.width=p.width;proxy.height=p.height;proxy.direction=dir;proxy.gravDir=p.gravDir;proxy.meleeScaleGlove=p.meleeScaleGlove;
            proxy.itemAnimationMax=frames;Array.Clear(enabled,0,enabled.Length);
#if DEBUG
            ShapeBuilds++;
#endif
            // StartActualUse is followed by the animation decrement before
            // native catching: max and zero are never reachable catch frames.
            for(int phase=frames-1;phase>0;phase--)
            {
                int index=Phase(phase,frames);lastPhase[index]=phase;if(enabled[index])continue;firstPhase[index]=phase;
                proxy.itemAnimation=phase;proxy.ItemCheck_ApplyUseStyle(mount,item,frame);
                bool inactive;Rectangle hit;proxy.ItemCheck_GetMeleeHitbox(item,frame,out inactive,out hit);
#if DEBUG
                ApplyUseStyleCalls++;GetMeleeHitboxCalls++;
#endif
                if(!inactive){hits[index]=hit;enabled[index]=true;}
            }
            ready=true;position=p.position;width=p.width;height=p.height;direction=dir;gravity=p.gravDir;scale=adjusted;offset=mount;animation=frames;return true;
        }
    }
}
