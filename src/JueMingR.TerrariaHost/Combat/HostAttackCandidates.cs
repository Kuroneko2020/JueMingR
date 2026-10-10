using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // Selection preference describes CURRENT straight-ray passage, not a
    // prediction of other NPCs or proof that a moving target can never be hit.
    // Unknown and blocked candidates remain selectable when no better one exists.
    internal sealed class HostAttackCandidates
    {
        private readonly PredictionTerrain terrain=new PredictionTerrain();
        private Vector2 origin;
        private bool straight;
        private int queries;
#if DEBUG
        internal int Queries {get{return queries;}}
        internal int Profiles {get;private set;}
#endif
        internal void Begin(Player player,bool enabled)
        {
            straight=false;queries=0;if(!enabled || player==null)return;
            var item=player.HeldItem;if(!HostAttackWindow.Ordinary(item))return;
            var ammo=AttackAmmoSnapshot.Capture(player,item);if(ammo==null)return;
#if DEBUG
            Profiles++;
#endif
            // Deliberately finite deterministic direct-shot members. Homing,
            // ricochet, secondary effects and scatter retain unknown passage.
            switch(ammo.Projectile)
            {case 14:case 242:case 981:case 158:case 159:case 160:case 161:break;default:return;}
            AttackMotion motion;Projectile sample;
            if(!HostAttackModels.TryRead(ammo,out motion) || motion.Confidence!=AttackConfidence.Conditional ||
                !ContentSamples.ProjectilesByType.TryGetValue(ammo.Projectile,out sample) || !sample.tileCollide)return;
            // A curved environmental path is outside this current-ray preference.
            // This leaves qualification unchanged; the actual solver owns wind/liquid.
            if(Main.windPhysics && Main.windSpeedCurrent*Main.windPhysicsStrength!=0)return;
            origin=player.RotatedRelativePoint(player.MountedCenter);terrain.Reset();straight=true;
        }
        internal int Priority(NPC npc)
        {
            if(!straight || queries>=512)return 0;
            var box=CombatSelection.ReceiveBounds(npc);
            float near=origin.X<box.Left?box.Left:origin.X>box.Right?box.Right:origin.X;
            if(Math.Abs(near-origin.X)<32)return 0;
            int direction=near>origin.X?1:-1,first=(int)Math.Floor(origin.X/16)+direction;
            int limit=(int)Math.Floor(near/16);int local=0;
            for(int x=first;direction>0?x<limit:x>limit;x+=direction)
            {
                float cut=x*16+8,min=float.MaxValue,max=float.MinValue;
                if((cut-origin.X)*direction<=0 || (near-cut)*direction<=0)continue;
                // All four corners bound the ray cone to the entire receiver.
                // One FULL solid column across that cone blocks every current
                // ray, unlike a centre LOS test that could discard legal edges.
                for(int corner=0;corner<4;corner++)
                {float endX=(corner&1)==0?box.Left:box.Right,endY=(corner&2)==0?box.Top:box.Bottom;
                    float y=origin.Y+(endY-origin.Y)*(cut-origin.X)/(endX-origin.X);min=Math.Min(min,y);max=Math.Max(max,y);}
                bool full=true;
                for(int y=(int)Math.Floor(min/16);y<=(int)Math.Floor(max/16);y++)
                {
                    if(++local>64 || queries>=512)return 0;
                    queries++;PredictionTile tile;PredictionStop stop;
                    if(!terrain.Tile(x,y,out tile,out stop))return 0;
                    if(!tile.Solid || tile.SolidTop || tile.Half || tile.Slope!=0 || tile.Liquid!=0){full=false;break;}
                }
                if(full)return -1;
            }
            return 0;
        }
    }
}
