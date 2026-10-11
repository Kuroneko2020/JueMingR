using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // Explicit autonomous hand shots share a finite movement/receive replay.
    // Their motors, native tile responses and damage gates stay separate;
    // AI style alone never registers a new member. Liquid entry/exit belongs
    // to each replayed position, including sticky liquid flags and shimmer.
    internal static class HostFiniteShotAttack
    {
        internal static bool Handles(int type){return type==45 || type==263 || type==274 || type==118 || type==119 || type==359 || type==114 || type==116 || type==173 || type==24 || type==261 || type==183 || type==507;}
        internal struct State
        {
            internal Vector2 Position,Velocity;internal float Counter,StoredSpeed,Bounces;internal int Left;
            internal bool Alive,Wet,Honey,Lava,Shimmer,Killed;
            internal State(Projectile sample,Vector2 center,Vector2 velocity)
            {Position=center-new Vector2(sample.width,sample.height)*.5f;Velocity=velocity;Counter=sample.ai[0];StoredSpeed=sample.localAI[0];Bounces=sample.ai[2];Left=sample.timeLeft;Alive=true;Wet=sample.wet;Honey=sample.honeyWet;Lava=sample.lavaWet;Shimmer=sample.shimmerWet;Killed=false;}
        }
        // Damage occurs between movement and Finish. A wall Kill returns from
        // original Update before both Damage and timeLeft--, while an expiry
        // Kill happens after the final eligible Damage position.
        internal static bool Step(Projectile sample,PredictionTerrain terrain,ref State state)
        {
            if(!state.Alive || state.Left<=0){state.Alive=false;return true;}
            // These non-AI003 roles retire at the native Update boundary,
            // before AI and without Kill/timeLeft mutation. A still-in-world
            // body can use the clipped collision scan right up to that edge.
            if(state.Position.X<=Main.leftWorld || state.Position.X+sample.width>=Main.rightWorld || state.Position.Y<=Main.topWorld || state.Position.Y+sample.height>=Main.bottomWorld)
            {state.Alive=false;return true;}
            Vector2 wetVelocity=state.Velocity;
            if(sample.type==263 || sample.type==274)state.Velocity*=sample.type==263?.95f:.96f;
            else if(sample.type==45){state.Counter++;if(state.Counter>=30){if(state.Counter<100)state.Velocity*=1.06f;else state.Counter=200;}}
            else if(sample.type==114)
            {
                state.Counter++;if(state.StoredSpeed==0)state.StoredSpeed=state.Velocity.Length();
                if(state.Counter>=20){state.Velocity*=.98f;state.StoredSpeed*=.98f;if(state.StoredSpeed<1){state.Alive=false;state.Left=0;}}
            }
            else if(sample.type==24 || sample.type==261 || sample.type==183)
            {
                int start=sample.type==261?15:5;state.Counter++;
                if(state.Counter>start)
                {
                    if(state.Velocity.Y==0 && state.Velocity.X!=0)
                    {
                        state.Velocity.X*=.97f;
                        if(sample.type==261){if(Math.Abs(state.Velocity.X)<=.01f){state.Alive=false;state.Left=0;}}
                        else if((double)state.Velocity.X>-.01 && (double)state.Velocity.X<.01)state.Velocity.X=0;
                    }
                    state.Counter=start;state.Velocity.Y+=.2f;
                }
            }
            if(sample.type==507){state.Counter++;if(state.Counter>60){state.Velocity.X*=.98f;state.Velocity.Y+=.3f;}}
            if((sample.type==114 || sample.type==116 || sample.type==173 || sample.type==24 || sample.type==261 || sample.type==183 || sample.type==507) && state.Velocity.Y>16)state.Velocity.Y=16;
            Vector2 half=new Vector2(sample.width,sample.height)*.5f,center=state.Position+half;
            float wind;if(!HostProjectileEnvironment.Impulse(sample,terrain,center.X,center.Y,state.Velocity.X,out wind))return false;state.Velocity.X+=wind;
            if(!sample.ignoreWater)
            {
                bool wet,lava;byte kind;
                if(!terrain.ProjectileWet(center.X,center.Y,sample.width,sample.height,out wet,out kind,true) || !terrain.ProjectileLava(center.X,center.Y,sample.width,sample.height,out lava,true))return false;
                if(lava)state.Lava=true;if(wet && kind==2)state.Honey=true;if(wet && kind==3)state.Shimmer=true;state.Wet=wet;
                if(!wet)state.Honey=state.Lava=state.Shimmer=false;
            }
            if(state.Shimmer)
            {
                if(sample.aiStyle==14){if(state.Left>3000)state.Left-=60;state.Velocity.Y-=.5f;}
                else if(sample.aiStyle==28 || sample.type==507)
                {
                    bool full;if(!ShimmerColumn(terrain,center.X,state.Position.Y,out full))return false;
                    if(full){state.Alive=false;state.Left=0;}
                    else if(state.Velocity.Y>0){state.Velocity.Y*=-1;if(sample.type==507){if(state.Left>600)state.Left=600;state.Left-=60;}state.Shimmer=state.Wet=false;}
                }
            }
            Vector2 prior=state.Velocity;
            bool hit=false;
            if(sample.tileCollide && !terrain.ProjectileTileMotion(sample,ref state.Position,ref state.Velocity,state.Wet,state.Honey,out wetVelocity,out hit,fallThrough:sample.type!=24,shimmer:state.Shimmer,nativeClip:true))return false;
            if(hit)
            {
                // Shadowbeam settles against the wall while its independent
                // speed clock fades; BeamSword reflects only its first three
                // tile contacts. The boulder's explosion is native dust/gore,
                // not an extra damage body or a new projectile.
                if(sample.type==114){state.Position+=state.Velocity*.99f;state.Velocity=prior.SafeNormalize(Vector2.Zero)*.0001f;}
                else if(sample.type==116 && ++state.Bounces<4){if(state.Velocity.X!=prior.X)state.Velocity.X=-prior.X;if(state.Velocity.Y!=prior.Y)state.Velocity.Y=-prior.Y;}
                else if(sample.type==24 || sample.type==261 || sample.type==183){if(state.Velocity.X!=prior.X)state.Velocity.X=prior.X*-.5f;if(state.Velocity.Y!=prior.Y && prior.Y>1)state.Velocity.Y=prior.Y*-.5f;}
                else if(sample.type==507){state.Position+=state.Velocity;state.Alive=false;state.Left=0;}
                else{state.Alive=false;state.Left=0;return true;}
            }
            // With tileCollide=false native retains the PRE-AI wetVelocity,
            // including full speed; applying generic water damping is wrong.
            state.Position+=state.Wet?wetVelocity:state.Velocity;return true;
        }
        private static bool ShimmerColumn(PredictionTerrain terrain,float centerX,float top,out bool full)
        {
            full=false;int x=(int)(centerX/16),y=(int)(top/16);if(x<0 || y<0 || x>=Main.maxTilesX || y>=Main.maxTilesY)return true;
            PredictionTile tile,above;PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;
            if(tile.Liquid==255 && tile.ShimmerLiquid && y>0){if(!terrain.Tile(x,y-1,out above,out stop))return false;full=above.Liquid>0 && above.ShimmerLiquid;}return true;
        }
        internal static bool Damaging(Projectile sample,State state){return sample.type!=261 || state.Velocity.Length()>=1.5f;}
        internal static void Finish(ref State state)
        {if(state.Alive && --state.Left<=0){state.Left=0;state.Alive=false;state.Killed=true;}}
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            var sample=ContentSamples.ProjectilesByType[ammo.Projectile];
            var receive=HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,beforeNpc,!beforeNpc);
            int limit=Math.Min(120,timeline.Count-age-1);if(limit<=0)return null;
            AttackMotion motion;var direct=HostAttackModels.TryRead(ammo,out motion)?AttackIntercept.Solve(origin.X,origin.Y,motion,timeline,age,0):null;
            bool beeBomb=sample.type==183,angular=sample.type==116 || sample.type==24 || sample.type==261 || beeBomb;
            var births=beeBomb?obstacles.CaptureBirthSlots():null;
            Projectile parent=null;HostProjectileTargets beeTargets=null;
            if(beeBomb)
            {parent=new Projectile();parent.SetDefaults(183);parent.active=true;parent.owner=player.whoAmI;parent.damage=ammo.Damage;beeTargets=HostBeeAttack.CaptureTargets(parent,obstacles);}
            var directions=new Vector2[angular?42:10];int count=0;
            for(int seed=-1;seed<(angular?41:9);seed++)
            {
                if(seed<0 && direct==null)continue;
                var body=timeline[Math.Min(timeline.Count-1,age+1+Math.Max(0,seed)*8)].ProjectileReceiveBounds;
                Vector2 direction=seed>=9?((seed-9)*MathHelper.TwoPi/32).ToRotationVector2():(seed<0?new Vector2(direct.AimX,direct.AimY)-origin:new Vector2(body.CenterX,body.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction);
                bool duplicate=false;for(int i=0;i<count;i++)if(directions[i]==direction){duplicate=true;break;}if(duplicate)continue;directions[count++]=direction;
                var aim=origin+direction*10000;var state=new State(sample,origin,direction*ammo.Speed);obstacles.BeginCandidate();int action=0;
                HostProjectileBirthSlots slots=null;
                if(beeBomb)
                {
                    slots=births.Copy();int slot;if(!slots.TryAllocate(out slot))continue;
                    parent.whoAmI=slot;
                }
                for(int outer=1;outer<=limit && state.Alive;outer++)for(int sub=0;sub<=sample.extraUpdates && state.Alive;sub++)
                {
                    var previousCenter=state.Position+new Vector2(sample.width,sample.height)*.5f;
                    if(sub==0 && !obstacles.BeforeNpc(age+outer,delay+outer,previousCenter.X,previousCenter.Y)){state.Alive=false;break;}
                    if(!Step(sample,terrain,ref state) || !state.Alive){state.Alive=false;break;}
                    int tick=age+outer;Vector2 center=state.Position+new Vector2(sample.width,sample.height)*.5f;
                    action++;
                    if(Damaging(sample,state) && !obstacles.PassOuter(action,tick,delay+outer,center.X,center.Y))
                    {
                        state.Alive=false;
                        // A qualified final receive really calls Kill. An
                        // unknown reflector/dependency is not a bee birth.
                        if(beeBomb && obstacles.Remaining==0)
                        {var bees=Bees(parent,state,slots,timeline,tick,delay+outer,terrain,obstacles,aim,beeTargets);if(bees!=null)return bees;}
                        break;
                    }
                    if(Damaging(sample,state) && receive.Allows(delay+outer))
                    {var contact=AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,center.X,center.Y,sample.width,sample.height,AttackConfidence.Conditional);if(contact!=null)return contact;}
                    Finish(ref state);
                    if(beeBomb && state.Killed)
                    {var bees=Bees(parent,state,slots,timeline,tick,delay+outer,terrain,obstacles,aim,beeTargets);if(bees!=null)return bees;}
                }
            }
            return null;
        }
        private static AttackContact Bees(Projectile parent,State state,HostProjectileBirthSlots slots,NpcTrajectory timeline,int tick,int outer,PredictionTerrain terrain,HostAttackObstacles obstacles,Vector2 aim,HostProjectileTargets targets)
        {
            // Native Kill uses the parent's top-left as NewProjectile's
            // center. Its slot remains occupied until all bees are born.
            var contact=HostBeeAttack.BirthRepresentative(parent,state.Position,state.Velocity,false,slots,timeline,tick,outer,terrain,obstacles,targets);
            return AttackIntercept.WithAimInput(contact,aim.X,aim.Y);
        }
    }
}
