using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal static class HostGuidedAttack
    {
        internal static bool Handles(int type){return type==16 || type==34 || type==79;}
        internal static AttackContact Solve(Player player,Projectile shot,NpcTrajectory timeline,int age,bool beforeNpc,PredictionTerrain terrain)
        {
            int updates=shot.extraUpdates+1;
            int estimate=Math.Max(1,(int)(Vector2.Distance(shot.Center,new Vector2(timeline[Math.Min(age+1,timeline.Count-1)].Bounds.CenterX,timeline[Math.Min(age+1,timeline.Count-1)].Bounds.CenterY))/32/updates));
            // Four bounded representative future control points, each replayed
            // with the extracted CONTROLLED AI_009 motor. Released autoseeking
            // belongs to vanilla and is never predicted as continued control.
            foreach(int offset in new[]{0,1,3,8})
            {
                int candidate=Math.Min(timeline.Count-1,age+estimate+offset);var box=timeline[candidate].ProjectileReceiveBounds;
                Vector2 aim=new Vector2(box.CenterX,box.CenterY);player.LimitPointToPlayerReachableArea(ref aim);
                Vector2 center=shot.Center,velocity=shot.velocity;
                for(int k=1;k<=Math.Min(120*updates,(timeline.Count-age-1)*updates);k++)
                {
                    Vector2 old=center,delta=aim-center;
                    if(delta.Length()>=64)velocity=delta.SafeNormalize(Vector2.Zero)*Math.Min(32,delta.Length());
                    else velocity=velocity*.3f+delta*.3f;
                    center+=velocity;
                    if(!terrain.ProjectilePassage(old.X,old.Y,center.X,center.Y,shot.width,shot.height))break;
                    int tick=age+(k-1)/updates+(beforeNpc?1:0);
                    var contact=AttackIntercept.BodyContact(timeline,tick,(k-1)%updates,aim.X,aim.Y,center.X,center.Y,shot.width,shot.height,AttackConfidence.Conditional);
                    if(contact!=null)return contact;
                }
            }
            return null;
        }
    }
}
