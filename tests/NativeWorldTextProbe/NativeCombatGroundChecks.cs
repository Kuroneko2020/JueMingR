using System;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatGroundChecks
    {
        internal static void Run(NPC npc,Func<NPC,NpcMotionState> capture,PredictionEnvironment env,IPredictionTerrain terrain)
        {
            var player=Main.LocalPlayer;var old=player.position;
            player.position=new Vector2(680,1200-player.height);env.PlayerX=player.Center.X;env.PlayerY=player.Center.Y;
            try
            {
                foreach(int type in new[]{3,21,27,109,120,166})
                {
                    Set(npc,type,1);NativeCombatPredictionChecks.Compare(npc,capture,env,terrain,120,"fighter flat type="+type,.12f);
                }
                foreach(float scale in new[]{.8f,1.2f})
                {Set(npc,3,scale);npc.velocity.X=3;NativeCombatPredictionChecks.Compare(npc,capture,env,terrain,120,"fighter speed scale="+scale,.12f);}
                for(int variant=0;variant<4;variant++)
                {
                    var tile=Main.tile[32,74];tile.active(true);tile.type=variant==3?TileID.Platforms:TileID.Stone;tile.halfBrick(variant==1);tile.slope((byte)(variant==2?1:0));tile.frameX=tile.frameY=0;
                    Set(npc,3,1);NativeCombatPredictionChecks.Compare(npc,capture,env,terrain,120,"fighter step/half/slope/platform "+variant,.12f);tile.active(false);tile.halfBrick(false);tile.slope(0);
                }
                for(int y=72;y<75;y++){Main.tile[32,y].active(true);Main.tile[32,y].type=TileID.Stone;}
                Set(npc,3,1);NativeCombatPredictionChecks.Compare(npc,capture,env,terrain,120,"fighter three-tile obstacle",.12f);
            }
            finally{for(int y=72;y<75;y++)Main.tile[32,y].active(false);player.position=old;}
        }
        private static void Set(NPC n,int type,float scale)
        {n.SetDefaults(type);n.active=true;n.whoAmI=0;n.target=0;n.position=new Vector2(400,1200-n.height);n.oldPosition=n.position;n.velocity=Vector2.Zero;n.scale=scale;n.direction=n.spriteDirection=1;n.directionY=-1;n.timeLeft=750;}
    }
}
