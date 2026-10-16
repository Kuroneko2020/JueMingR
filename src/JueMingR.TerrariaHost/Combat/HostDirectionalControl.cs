using System;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // These native consumers read a direction/point after Shoot. They share
    // Source registration and cursor lifetime with the other controllers,
    // while retaining their own actual read phases and autonomous exits.
    internal static class HostDirectionalControl
    {
        internal static bool Handles(int type){return type==595 || type==735 || type==697 || type==707 || type==1117;}
        internal static Vector2 Cursor(Player player,Vector2 point)
        {var screen=point-Main.screenPosition;if(player.gravDir==-1)screen.Y=Main.screenHeight-screen.Y;return new Vector2((int)screen.X+Main.screenPosition.X,player.gravDir==-1?Main.screenPosition.Y+Main.screenHeight-(int)screen.Y:Main.screenPosition.Y+(int)screen.Y);}
        internal static bool Weapon(Player player,Item item){return item!=null && Handles(item.shoot) && !(item.type==3858 && player.altFunctionUse==2);}
        internal static bool Pending(Player player,Projectile shot)
        {
            if(player==null || player.dead || player.noItems || player.CCed)return false;
            if(shot.type==595 || shot.type==735)return player.channel;
            if(!player.controlUseItem)return false;
            if(shot.type==1117)return shot.ai[0]>-2 && shot.ai[0]<0;
            if(shot.type==707)return shot.ai[0]<25 && shot.ai[0]+1>=25;
            return shot.type==697 && shot.ai[0]<25 && shot.ai[0]+(1+shot.ai[0]/50*.5f)>=25;
        }
        internal static bool Consumes(Player player,Projectile shot)
        {
            if(!Pending(player,shot))return false;
            if(shot.type!=1117)return true;
            // AI203 acquires a nearby native target BEFORE its cursor branch.
            // Once that happens the bolt is autonomous. Do not record a point
            // as consumed or lend another cursor to that locked damage phase.
            for(int i=0;i<Main.maxNPCs;i++)
            {
                var npc=Main.npc[i];
                if(npc.active && !npc.dontTakeDamage && Vector2.Distance(shot.Center,npc.Hitbox.ClosestPointInRect(shot.Center))<=12 && npc.CanBeChasedBy(shot))return false;
            }
            return true;
        }
        internal static bool OpeningPoint(NpcTrajectory timeline,HostAttackClock clock,out Vector2 point)
        {
            point=Vector2.Zero;int tick=clock.FirstTick;
            if(tick<0 || tick>=timeline.Count)return false;
            var bounds=timeline[tick].ProjectileReceiveBounds;point=new Vector2(bounds.CenterX,bounds.CenterY);return true;
        }
        internal static bool TryPoint(Player player,Projectile shot,NpcTrajectory timeline,HostAttackClock clock,out Vector2 point)
        {
            point=Vector2.Zero;if(!Consumes(player,shot))return false;
            int first=clock.FirstTick;if(first<0 || first>=timeline.Count)return false;
            if(shot.type!=1117)return OpeningPoint(timeline,clock,out point);
            // The unlocked bolt moves at most 16 pixels per native update.
            // A bounded fixed-point lead supplies a direction only, not a
            // Contact: native acquisition, solid-cell clipping and damage
            // remain vanilla. Never fill missing future with a copied tail.
            int tick=first;
            for(int i=0;i<3;i++)
            {
                var bounds=timeline[tick].ProjectileReceiveBounds;point=new Vector2(bounds.CenterX,bounds.CenterY);
                int next=first+Math.Max(0,(int)Math.Ceiling(Vector2.Distance(shot.Center,point)/16)-1);
                if(next>=timeline.Count)return false;
                if(next==tick)return true;tick=next;
            }
            var target=timeline[tick].ProjectileReceiveBounds;point=new Vector2(target.CenterX,target.CenterY);return true;
        }
    }
}
