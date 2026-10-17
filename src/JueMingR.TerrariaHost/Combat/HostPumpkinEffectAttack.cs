using System;
using System.Collections.Generic;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Testing;

namespace JueMingR.TerrariaHost.Combat
{
    // Horseman's arc owns finite AI055 pumpkins. The native apparent random
    // 100..300 distances cancel algebraically: only four viewport corners
    // around owner.position remain. Unborn corner choice stays representative.
    internal static class HostPumpkinEffectAttack
    {
        internal static bool Family(int type){return type==321;}
        internal sealed class Inputs
        {
            private sealed class Row {internal NPC Npc;internal NpcIdentity Identity;internal Vector2 Center;internal bool Immortal,Statue;}
            private readonly List<Row> rows=new List<Row>();private readonly long session;private readonly bool dummy;
            internal readonly Point View;
            internal Inputs(long session){this.session=session;View=Main.MaxWorldViewSize;dummy=DebugOptions.LetProjectilesAimAtTargetDummies;foreach(var n in Main.npc)if(n.active)rows.Add(new Row{Npc=n,Identity=CombatSelection.Identity(n,session),Center=n.Center,Immortal=n.immortal,Statue=n.SpawnedFromStatue});}
            internal bool Proc(int slot){foreach(var row in rows)if(row.Identity.Slot==slot)return (!row.Immortal || dummy && row.Identity.Type==488) && !row.Statue && !NPCID.Sets.CountsAsCritter[row.Identity.Type];return false;}
            internal Vector2 Center(int slot,NpcTrajectory path,int tick){if(slot==path.Identity.Slot){var b=path[tick].Bounds;return new Vector2(b.CenterX,b.CenterY);}foreach(var row in rows)if(row.Identity.Slot==slot)return row.Center;return Vector2.Zero;}
            // Presentation checks only already captured scalar members. Full
            // population qualification belongs to prepare/consume separately.
            internal bool MembersCurrent {get{if(Main.MaxWorldViewSize!=View || DebugOptions.LetProjectilesAimAtTargetDummies!=dummy)return false;foreach(var row in rows)if(!CombatSelection.Valid(row.Identity,session) || row.Npc.immortal!=row.Immortal || row.Npc.SpawnedFromStatue!=row.Statue)return false;return true;}}
            internal bool Current {get{if(!MembersCurrent)return false;int count=0;foreach(var n in Main.npc)if(n.active){if(count>=rows.Count || !ReferenceEquals(rows[count++].Npc,n))return false;}return count==rows.Count;}}
        }
        internal static Projectile Birth(Projectile parent,Player owner,Vector2 victim,int victimSlot,int variant,Inputs inputs)
        {
            var q=HostNoteAttack.Copy(ContentSamples.ProjectilesByType[321]);q.active=true;q.owner=parent.owner;q.Center=owner.position+new Vector2((variant%2==0?-1:1)*(inputs.View.X/2),(variant<2?-1:1)*(inputs.View.Y/2));var delta=victim-q.Center;float factor=8/delta.Length();q.velocity=new Vector2(delta.X*factor,delta.Y*factor);q.damage=parent.damage;q.knockBack=parent.knockBack;q.ai[0]=victimSlot;return q;
        }
        internal static bool Step(Projectile p,HostProjectileTargets targets,NpcTrajectory path,int tick,PredictionTerrain terrain)
        {
            if(HostRaySpellAttack.WorldRetired(p)){p.active=false;return true;}var wetVelocity=p.velocity;
            if(p.ai[0]<0 || p.ai[0]>=Main.maxNPCs){p.active=false;p.timeLeft=0;return true;}
            int target=-1;for(int i=0;i<targets.Count;i++)if(targets.Slot(i)==(int)p.ai[0] && !NPCID.Sets.CountsAsCritter[targets.Type(i)]){target=i;break;}
            if(target>=0){var b=targets.Bounds(target,path,tick);var delta=new Vector2(b.CenterX,b.CenterY)-p.Center;float factor=8/delta.Length();p.velocity.X=(p.velocity.X*14+delta.X*factor)/15;p.velocity.Y=(p.velocity.Y*14+delta.Y*factor)/15;}
            else
            {
                float nearest=1000;for(int i=0;i<targets.Count;i++){if(NPCID.Sets.CountsAsCritter[targets.Type(i)])continue;var b=targets.Bounds(i,path,tick);float distance=Math.Abs(p.Center.X-b.CenterX)+Math.Abs(p.Center.Y-b.CenterY);if(distance>=nearest)continue;bool clear;PredictionStop stop;if(!terrain.CanHit(new MotionRect(p.position.X,p.position.Y,p.width,p.height),b,out clear,out stop))return false;if(clear){nearest=distance;p.ai[0]=targets.Slot(i);}}
            }
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;if(p.owner<255 && !Main.player[p.owner].active){p.active=false;p.timeLeft=0;}p.position+=p.wet?wetVelocity:p.velocity;return true;
        }
    }
}
