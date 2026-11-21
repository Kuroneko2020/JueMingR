using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Enums;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private bool PrepareRemainingNpcState(NPC npc,NpcState state,bool decoded)
        {
            // Most families prepare their received phase's physical inputs.
            // StatueMimic and Snail also need the ordinary client's local
            // movement/shape action, once at the real NPC world step. Neither
            // path runs a second world update or publishes server results.
            // A successful call must either produce definite physical fields
            // or preserve qualified history from this same observation owner.
            bool known=true;
            switch(npc.aiStyle)
            {
                case 19:
                    known=PrepareAntlionSupport(npc,decoded);
                    break;
                case 87:
                    float mimic=npc.ai[0];
                    if(mimic==4.1f)
                    {
                        var target=ObservedPhysicsTarget(npc);
                        // ai2 is the already accepted obstruction decision.
                        // Do not repeat the CanHit decision which can change it.
                        if(target!=null)npc.noGravity=npc.noTileCollide=npc.ai[2]==0f && npc.velocity.Y>0f && npc.Bottom.Y<target.position.Y;
                        else if(npc.ai[2]!=0f || npc.velocity.Y<=0f)npc.noGravity=npc.noTileCollide=false;
                        else known=false;
                    }
                    else{npc.noGravity=mimic==4f || mimic==5f;npc.noTileCollide=npc.noGravity || mimic==7f;}
                    break;
                case 88:
                    npc.noGravity=true;
                    float moth=npc.ai[0];
                    npc.noTileCollide=moth==-1f || moth==1f || moth==3f || moth==3.1f || moth==3.2f || moth==4.1f || moth==4.2f;
                    break;
                case 90:
                    npc.noGravity=true;
                    float spawn=npc.ai[0];
                    npc.noTileCollide=!Main.eclipse || spawn==-1f || spawn==1f || spawn==2f || spawn==2.1f;
                    break;
                case 107:
                    known=PrepareWalkerPhysics(npc,state,decoded);
                    break;
                case 126:
                    if(!decoded)known=AdvanceStatuePhysics(npc);
                    break;
            }

            if(npc.type==378 && npc.ai[1]==5f)
            {
                // AI_041 publishes ai2=10 before expanding. Its following
                // action decrements that timer to9 and expands about CENTER.
                // An already expanded23 carries the new top-left; shift only
                // the first actual local expansion of that entry publication.
                bool entry=npc.ai[2]==10f;
                if(!entry || state.Advances>0)
                {
                    if(!decoded && entry && state.Advances==1)
                        npc.position+=new Vector2(npc.width/2-80,npc.height/2-80);
                    npc.width=npc.height=160;
                    // Each actual explosion action zeros travel BEFORE its
                    // gravity/collision tail. A decoded23 already carries
                    // that tail's velocity, which must remain the sample.
                    if(!decoded)npc.velocity=Vector2.Zero;
                }
            }
            else if(npc.type==359)
            {
                // GetData23 retains the receiver's current local shape. Only
                // its next AI_067 action applies ai3. The caller must preserve
                // this live shape instead of resetting defaults every step.
                if(!decoded && npc.ai[3]!=0f)
                {
                    npc.scale=npc.ai[3];int size=(int)(12f*npc.scale);
                    if(size!=npc.width)
                    {npc.position.X=npc.position.X+(float)(npc.width/2)-(float)size-2f;npc.width=size;}
                    if(size!=npc.height)
                    {npc.position.Y=npc.position.Y+(float)npc.height-(float)size;npc.height=size;}
                }
            }
            else if(npc.type==371)
            {
                npc.scale=npc.ai[3];
                if(npc.ai[0]==1f)npc.width=npc.height=100;
            }
            else if(npc.type==50 && state.LifeMax>0 && npc.life>0)
            {
                float clock=npc.ai[0]+state.Advances;
                float form=1f;
                if(npc.ai[1]==5f)form=.5f+.5f*MathHelper.Clamp((60f-clock)/60f,0f,1f);
                else if(npc.ai[1]==6f)form=.5f+.5f*MathHelper.Clamp(clock/30f,0f,1f);
                float life=(float)npc.life/state.LifeMax;
                float world=1f;if(Main.getGoodWorld){float multiplier=2f;multiplier-=1f-life;world*=multiplier;}
                npc.scale=(life*.5f+.75f)*form;npc.scale*=world;
                npc.width=(int)(98f*npc.scale);npc.height=(int)(92f*npc.scale);
                // The caller owns the nonzero native SyncAnchor correction.
            }
            else if(npc.type==657)
            {
                float scale=QueenObservationScale(npc,state.Advances);
                if(!decoded)
                {
                    float previous=QueenObservationScale(npc,state.Advances-1);
                    // QueenSlime uses top-left on23 and bottom-center during
                    // local rescaling. Keep those two coordinate contracts
                    // separate, including native integer half-width rounding.
                    npc.position+=new Vector2((int)(114f*previous)/2-(int)(114f*scale)/2,(int)(100f*previous)-(int)(100f*scale));
                }
                npc.scale=scale;npc.width=(int)(114f*scale);npc.height=(int)(100f*scale);
            }
            return known;
        }

        private static float QueenObservationScale(NPC npc,int advances)
        {
            float clock=npc.ai[1]+advances;
            if(npc.ai[0]==1f)return .5f+.5f*MathHelper.Clamp(clock/30f,0f,1f);
            if(npc.ai[0]==2f)return .5f+.5f*MathHelper.Clamp((60f-clock)/60f,0f,1f);
            return 1f;
        }

        private static Player ObservedPhysicsTarget(NPC npc)
        {
            if(npc.target<0 || npc.target>=Main.maxPlayers)return null;
            // AI_126 also uses its synchronized dead target while departing.
            var player=Main.player[npc.target];return player!=null && player.active?player:null;
        }

        private static bool ObservationTileKnown(int x,int y)
        {
            return x>=0 && y>=0 && x<Main.maxTilesX && y<Main.maxTilesY && Main.tile[x,y]!=null &&
                (Main.netMode!=1 || Main.sectionManager!=null && Main.sectionManager.TileLoaded(x,y));
        }

        private static bool ObservationBodyKnown(NPC npc)
        {
            // Native Solid/WetCollision inspect this body-local apron. Check
            // its entire footprint before querying; null/unreceived tiles are
            // unknown, never evidence of air or a reason to create a Tile.
            int left=(int)(npc.position.X/16f)-1,right=(int)((npc.position.X+npc.width)/16f)+1;
            int top=(int)(npc.position.Y/16f)-1,bottom=(int)((npc.position.Y+npc.height)/16f)+1;
            for(int x=left;x<=right;x++)for(int y=top;y<=bottom;y++)if(!ObservationTileKnown(x,y))return false;
            return true;
        }

        private static bool PrepareAntlionSupport(NPC npc,bool decoded)
        {
            // The supported source sets Vy=-.2 and then takes the no-tile
            // position+=velocity tail. A23 at Bottom959.8 can therefore have
            // read support at960. Decode qualifies that preceding foot point;
            // the next actual action reads its current foot, not that old one.
            Vector2 foot=npc.position;
            if(decoded && npc.velocity.Y==-.2f)
            {
                // x86 can retain rawY-Vy below936 in an extended register even
                // though native's stored float coordinate was936. Preserve
                // that single-precision value boundary before the tile cast,
                // and accept the inverse only if the same stored-float tail
                // reproduces the received coordinate EXACTLY. No epsilon.
                var prior=StoreNpcPhysicsPoint(npc.position.X-npc.velocity.X,npc.position.Y-npc.velocity.Y);
                if(StoreNpcPhysicsPoint(prior.X+npc.velocity.X,prior.Y+npc.velocity.Y)!=npc.position)return false;
                foot=prior;
            }
            int left=(int)foot.X/16,middle=(int)(foot.X+npc.width/2)/16,right=(int)(foot.X+npc.width)/16;
            int y=(int)(foot.Y+npc.height)/16;
            if(!ObservationTileKnown(left,y) || !ObservationTileKnown(middle,y) || !ObservationTileKnown(right,y))return false;
            var a=Main.tile[left,y];var b=Main.tile[middle,y];var c=Main.tile[right,y];
            npc.noGravity=npc.noTileCollide=a.nactive() && Main.tileSolid[a.type] || b.nactive() && Main.tileSolid[b.type] || c.nactive() && Main.tileSolid[c.type];
            return true;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static Vector2 StoreNpcPhysicsPoint(float x,float y){return new Vector2(x,y);}

        private static bool AdvanceStatuePhysics(NPC npc)
        {
            // AI_126 has no client-side spawn/loot/random transition. Reuse its
            // native target primitive and reproduce only the local movement
            // and clocks, omitting sounds and the server-only sleeping search.
            // The caller has already established THIS action's native gravity;
            // its common gravity/collision tail still runs exactly once later.
            npc.oldTarget=npc.target;npc.oldDirection=npc.direction;npc.oldDirectionY=npc.directionY;
            if(npc.noTileCollide)npc.wet=false;
            if(npc.ai[0]==0f)
            {
                npc.timeLeft=60;npc.TargetClosest();npc.direction=npc.oldDirection;return true;
            }
            // Native uses the numbered target even if it has just died or is
            // the sentinel body; active-only filtering changes this action
            // before TargetClosest is actually called at the jump boundary.
            var target=npc.target>=0 && npc.target<Main.player.Length?Main.player[npc.target]:null;
            if(target==null)return false;
            bool overlap=npc.Right.X>=target.Left.X && npc.Left.X<=target.Right.X;
            bool aligned=overlap || target.dead && (npc.ai[3]==0f || npc.ai[3]>200f);
            bool below=npc.velocity.Y>0f && npc.Top.Y>target.Bottom.Y;
            bool needsSolid=below || npc.velocity.Y!=0f && !(aligned && npc.Bottom.Y<target.Top.Y) &&
                (npc.velocity.Y>0f || target.dead && aligned && npc.Bottom.Y<target.Bottom.Y);
            bool solid=false;
            // These are the only terrain reads in the local movement action.
            // Do not advance half an action before discovering missing tiles.
            if(needsSolid)
            {
                if(!ObservationBodyKnown(npc))return false;
                solid=Collision.SolidCollision(npc.position,npc.width,npc.height);
            }
            if(npc.localAI[2]==0f)npc.localAI[2]=1f;
            bool jump=below && solid;
            if(!below && npc.velocity.Y==0f)
            {
                npc.velocity.X=0f;npc.ai[2]-=1f;
                if(npc.ai[2]<=0f){npc.ai[2]=20f;jump=true;}
            }
            if(overlap && npc.Bottom.Y<target.Top.Y)
            {
                npc.velocity.X*=.75f;if(npc.velocity.Y<0f)npc.velocity.Y*=.75f;
            }
            if(target.dead)
            {
                if(npc.ai[3]==0f)npc.ai[3]=300f;
                if(npc.ai[3]>200f)npc.ai[3]-=1f;
            }
            else if(npc.ai[3]>0f)npc.ai[3]-=1f;
            if(jump)
            {
                npc.noTileCollide=true;npc.TargetClosest();target=Main.player[npc.target];
                float height=(npc.Bottom.Y-target.Bottom.Y)/40f;
                height=target.dead?0f:Utils.Clamp(height,0f,10f);
                npc.velocity.Y=-9.01f-height;
                float distance=Math.Abs(npc.Center.X-target.Center.X)/50f;if(distance>12f)distance=12f;
                npc.velocity.X=(4f+distance)*(float)npc.direction;
                // A local client can jump again without receiving a new23.
                // netUpdate is a server publication request, not an output
                // which this observation table sends into the real world.
                return true;
            }
            if(npc.velocity.Y==0f)return true;
            if(target.dead && aligned && npc.Bottom.Y<target.Bottom.Y || aligned && npc.Bottom.Y<target.Top.Y)npc.velocity.Y=16f;
            else
            {
                if(npc.velocity.Y>0f)npc.velocity.Y+=(float)gravityField.GetValue(null);
                if(!target.dead && (npc.direction>0 && npc.Center.X>target.Center.X || npc.direction<0 && npc.Center.X<target.Center.X))
                    npc.velocity=StoreNpcPhysicsPoint(npc.velocity.X*.96f,npc.velocity.Y*.96f);
            }
            if(npc.velocity.Y<0f || aligned && npc.Bottom.Y<target.Top.Y)npc.noTileCollide=true;
            else if(!solid)npc.noTileCollide=false;
            // Embedded falling retains THIS client's field, including defaults
            // false. It does not require a hidden server flag or an attack ban.
            return true;
        }

        private static bool PrepareWalkerPhysics(NPC npc,NpcState state,bool decoded)
        {
            bool known=true;
            if(!NPCID.Sets.FighterUsesDD2PortalAppearEffect[npc.type])npc.noGravity=npc.noTileCollide=false;
            else if(npc.ai[0]>0f)npc.noGravity=npc.noTileCollide=false;
            else
            {
                var target=npc.GetTargetData();
                // Native may choose ANOTHER target when its crystal becomes
                // protected. This preparation cannot make that decision.
                bool protectedCrystal=target.Type==NPCTargetType.NPC && Main.npc[npc.TranslatedTargetIndex].type==548 && Main.npc[npc.TranslatedTargetIndex].dontTakeDamageFromHostiles;
                if(!target.Invalid && !protectedCrystal)
                {
                    Vector2 delta=target.Center-npc.Center;
                    bool faces=npc.direction==Math.Sign(delta.X);
                    bool usesHistory=delta.LengthSquared()>50f*50f && delta.Y<0f;
                    bool direction=faces || npc.noGravity && usesHistory;
                    if(!faces && !usesHistory || !direction && state.PhysicsKnown)npc.noGravity=npc.noTileCollide=false;
                    else if(ObservationSightKnown(npc.Center,target.Center))
                    {
                        // This query produces CURRENT physical inputs only;
                        // never change target/AI/timers/velocity from its result.
                        bool obstructed=!Collision.CanHit(npc.Center,0,0,target.Center,0,0);
                        if(!obstructed)npc.noGravity=npc.noTileCollide=false;
                        else if(!faces && usesHistory && !state.PhysicsKnown)known=false;
                        else if(npc.velocity.Y==0f || Math.Abs(delta.Y)>800f)npc.noGravity=npc.noTileCollide=true;
                        // Otherwise native intentionally retains the flags
                        // from this very object's preceding local action.
                        else known=state.PhysicsKnown;
                    }
                    else known=false;
                }
                else known=false;
            }
            // The synchronized escape action has already moved the source.
            // Restore only its current flags, never repeat the teleport or
            // advance ai3. Later local actions must not replay that pulse.
            if(decoded && npc.type!=620 && npc.ai[0]<=0f && npc.ai[3]==30f)npc.noGravity=npc.noTileCollide=true;
            if(npc.type==620)
            {
                if(!ObservationBodyKnown(npc))known=false;
                else
                {
                    bool honey=Collision.honey,shimmer=Collision.shimmer;
                    try{npc.wet=Collision.WetCollision(npc.position,npc.width,npc.height);npc.noGravity=npc.wet;}
                    finally{Collision.honey=honey;Collision.shimmer=shimmer;}
                }
            }
            return known;
        }

        private static bool ObservationSightKnown(Vector2 from,Vector2 to)
        {
            // Match CanHit(Point)'s tile walk ONLY to qualify its reads, not
            // to implement a second ray test. Each iteration decreases the
            // endpoint Manhattan distance; work is linear in this one ray,
            // within the shared per-NPC preparation, never once per actor.
            int x=(int)from.X/16,y=(int)from.Y/16,tx=(int)to.X/16,ty=(int)to.Y/16;
            if(x<1 || y<1 || tx<1 || ty<1 || x>=Main.maxTilesX-1 || tx>=Main.maxTilesX-1 || y>=Main.maxTilesY-40 || ty>=Main.maxTilesY-40)return false;
            while(x!=tx || y!=ty)
            {
                if(Math.Abs(x-tx)>Math.Abs(y-ty))
                {
                    x+=x<tx?1:-1;
                    if(!ObservationTileKnown(x,y-1) || !ObservationTileKnown(x,y+1))return false;
                }
                else
                {
                    y+=y<ty?1:-1;
                    if(!ObservationTileKnown(x-1,y) || !ObservationTileKnown(x+1,y))return false;
                }
                if(!ObservationTileKnown(x,y))return false;
            }
            return true;
        }
    }
}
