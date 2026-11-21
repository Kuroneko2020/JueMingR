using System;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        // AI003's common movement straddles the subtype action. Capture the
        // ENTRY velocity/hit before an Armed swing can damp X to zero; the
        // later support branch must not reinterpret that new zero as stuck.
        private static bool BeginFighterMotion(NPC npc)
        {
            bool stuck=npc.velocity.X==0f && !npc.justHit;
            bool backwards=npc.velocity.Y==0f &&
                (npc.velocity.X>0f && npc.direction<0 || npc.velocity.X<0f && npc.direction>0);
            if(npc.position.X==npc.oldPosition.X || npc.ai[3]>=60f || backwards)npc.ai[3]++;
            else if(Math.Abs(npc.velocity.X)>.9 && npc.ai[3]>0f)npc.ai[3]--;
            if(npc.ai[3]>600f || npc.justHit)npc.ai[3]=0f;
            var target=ObservedPhysicsTarget(npc);
            if(target!=null && target.Hitbox.Intersects(npc.Hitbox))npc.ai[3]=0f;
            return stuck;
        }

        private static void FinishFighterMotion(NPC npc,bool entryStuck)
        {
            // The shared AI003 support/forward branch follows the subtype's
            // velocity and attack-clock changes. Query only received tiles;
            // unlike vanilla's null fallback this copy never creates terrain.
            if(npc.velocity.Y!=0f || !ObservationBodyKnown(npc))return;
            int foot=(int)(npc.position.Y+npc.height+7f)/16;
            int head=(int)(npc.position.Y-9f)/16;
            int left=(int)(npc.position.X+8f)/16,right=(int)(npc.position.X+npc.width-8f)/16;
            bool supported=false;
            for(int x=left;x<=right;x++)
            {
                if(!ObservationTileKnown(x,head) || !ObservationTileKnown(x,foot))return;
                if(WorldGen.SolidTileNoPlatforms(x,head)){supported=false;break;}
                var tile=Main.tile[x,foot];if(tile.nactive() && Main.tileSolid[tile.type])supported=true;
            }
            if(!supported || !(npc.velocity.X<0f && npc.spriteDirection==-1 || npc.velocity.X>0f && npc.spriteDirection==1))return;
            int front=(int)((npc.position.X+npc.width/2+15*npc.direction)/16f);
            int y=(int)((npc.position.Y+npc.height-15f)/16f);
            for(int x=front-1;x<=front+1;x++)for(int row=y-3;row<=y+1;row++)if(!ObservationTileKnown(x,row))return;
            // Preserve native obstacle precedence; the unstuck hop is a final
            // fallback, not an unconditional replacement for a higher jump.
            if(npc.height>=32 && WorldGen.SolidTileNoPlatforms(front,y-2))
                npc.velocity.Y=WorldGen.SolidTileNoPlatforms(front,y-3)?-8f:-7f;
            else if(WorldGen.SolidTileNoPlatforms(front,y-1))npc.velocity.Y=-6f;
            else if(npc.position.Y+npc.height-y*16f>20f && !Main.tile[front,y].topSlope() && WorldGen.SolidTileNoPlatforms(front,y))npc.velocity.Y=-5f;
            else if(npc.directionY<0 && !WorldGen.SolidTileAllowBottomSlope(front,y+1) && !WorldGen.SolidTileAllowBottomSlope(front+npc.direction,y+1))
            {npc.velocity.Y=-8f;npc.velocity.X*=1.5f;}
            if(npc.velocity.Y==0f && entryStuck && npc.ai[3]==1f)npc.velocity.Y=-5f;
            // Gravity/collision and justHit retirement remain at the single
            // shared world-step tail, never inside either movement half.
        }
    }
}
