using System;
using System.Reflection;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using HarmonyLib;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatTerrainChecks
    {
        private static int farCoinDust;
        private static void CoinDust(Vector2 __0,int __3){if(__0.X>65000 && __3==247)farCoinDust++;}
        internal static void Trace(Assembly host)
        {
            const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
            var trace=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeNpcMotionTrace",true);
            var patches=new Harmony("JueMingR.Tests.SelectedMotionTrace");trace.GetMethod("Install",flags).Invoke(null,new object[]{patches});
            try
            {
                NativeCombatWorkerChecks.Scene(false);var npc=Main.npc[0];npc.SetDefaults(241);npc.active=true;npc.whoAmI=0;npc.wet=true;
                Action begin=()=>{trace.GetMethod("Begin",flags).Invoke(null,new object[]{npc});trace.GetMethod("Enter",flags).Invoke(null,new object[]{npc});};
                Func<bool> complete=()=>{trace.GetMethod("Leave",flags).Invoke(null,null);return (bool)trace.GetMethod("Complete",flags).Invoke(null,null);};
                var move=typeof(NPC).GetMethod("Collision_MoveWhileWet",flags);
                foreach(float slowdown in new[]{.5f,.25f,.375f})
                {
                    npc.position=new Vector2(700,800);npc.velocity=new Vector2(2,2);begin();move.Invoke(npc,new object[]{npc.velocity,slowdown});
                    Require(!complete() && npc.position==new Vector2(700+2*slowdown,800+2*slowdown),"Actual original wet displacement is continuous at native slowdown="+slowdown);
                    npc.position=new Vector2(700,800);npc.velocity=new Vector2(2,2);begin();npc.position.X+=100;move.Invoke(npc,new object[]{npc.velocity,slowdown});
                    Require(complete(),"An AI-style direct relocation before wet movement remains discontinuous.");
                }
                npc.position=new Vector2(700,800);begin();npc.Teleport(new Vector2(700.0001f,800),-1);Require(complete(),"Explicit native Teleport remains discontinuous even below numeric tolerance and while wet.");
                npc.position=new Vector2(700,800);begin();move.Invoke(npc,new object[]{npc.velocity,.5f});trace.GetMethod("Leave",flags).Invoke(null,null);npc.position.X+=30;Require((bool)trace.GetMethod("Complete",flags).Invoke(null,null),"A later actor's relocation is not hidden by an earlier normal movement phase.");
                trace.GetMethod("Clear",flags).Invoke(null,null);Require(!(bool)trace.GetMethod("Complete",flags).Invoke(null,null),"Trace clear releases the selected instance and discontinuity across requests.");
                // A real UpdateNPC particle branch adds/subtracts netOffset.
                // Near the 65536 float exponent boundary it can round by one
                // coordinate ULP without performing any actual relocation.
                patches.Patch(typeof(Dust).GetMethod("NewDust",flags),prefix:new HarmonyMethod(typeof(NativeCombatTerrainChecks).GetMethod("CoinDust",flags)));
                int mode=Main.netMode,smoothing=Main.multiplayerNPCSmoothingRange,offsetDelay=NPC.offSetDelayTime;try
                {
                    Main.maxTilesX=8400;Main.rightWorld=8400*16;Main.tile=new Tile[8400,120];
                    for(int x=4060;x<4140;x++)for(int y=0;y<120;y++)Main.tile[x,y]=new Tile();
                    Main.player[0].position=new Vector2(65500,900);Main.netMode=1;Main.multiplayerNPCSmoothingRange=10;NPC.offSetDelayTime=0;farCoinDust=0;
                    for(int seed=0;seed<64 && farCoinDust==0;seed++)
                    {
                        npc=Main.npc[0]=new NPC();npc.SetDefaults(173);npc.active=true;npc.whoAmI=0;npc.position=new Vector2(65532.00390625f,800);npc.netOffset=new Vector2(10,0);npc.extraValue=1000000;npc.velocity=Vector2.Zero;
                        Main.rand=new Terraria.Utilities.UnifiedRandom(seed);begin();npc.UpdateNPC(0);bool broken=complete();
                        if(farCoinDust>0)Console.WriteLine("HIGH-COORD original coin branch seed="+seed+" oldX="+npc.oldPosition.X.ToString("R")+" x="+npc.position.X.ToString("R")+" offset="+npc.netOffset+" segment="+broken);
                        Require(!broken,"Original high-coordinate particle offset roundtrip is continuous; seed="+seed+" coin-dust="+farCoinDust);
                    }
                    Require(farCoinDust>0,"Original UpdateNPC extraValue particle branch must actually execute.");
                    npc.position=new Vector2(65532,800);begin();npc.Teleport(new Vector2(65532.00390625f,800),-1);Require(complete(),"Explicit high-coordinate one-ULP Teleport is still a real discontinuity.");
                    npc.position=new Vector2(65532,800);begin();npc.position.X+=.03125f;Require(complete(),"High-coordinate unexplained movement beyond one ULP remains discontinuous.");
                }
                finally{Main.netMode=mode;Main.multiplayerNPCSmoothingRange=smoothing;NPC.offSetDelayTime=offsetDelay;NativeCombatWorkerChecks.Scene(false);}
                Console.WriteLine("PASS original wet movement at three liquid rates / direct wet relocation / original Teleport / later relocation / clear.");
            }
            finally{trace.GetMethod("Clear",flags).Invoke(null,null);patches.UnpatchAll(patches.Id);}
        }
        internal static void Run(object context)
        {
            var source=Get(Get(context,"CombatObservation"),"Prediction");var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var read=source.GetType().GetMethod("Read",BindingFlags.Static|BindingFlags.NonPublic);
            var nativeMove=typeof(NPC).GetMethod("UpdateCollision",BindingFlags.Instance|BindingFlags.NonPublic);
            var nativeGravity=typeof(NPC).GetField("gravity",BindingFlags.Static|BindingFlags.NonPublic);var savedGravity=nativeGravity.GetValue(null);nativeGravity.SetValue(null,.3f);
            var saved=Main.tile[60,60];Main.tile[60,60]=new Tile();Main.tileSolid[TileID.Stone]=true;
            int cases=0,contacts=0;
            try
            {
                for(int variant=0;variant<9;variant++)
                {
                    byte slope=(byte)(variant<5?variant:variant==8?1:0);var tile=Main.tile[60,60];tile.active(true);tile.type=(ushort)(variant>=6?TileID.Platforms:TileID.Stone);tile.slope(slope);tile.halfBrick(variant==5);tile.frameX=0;tile.frameY=(short)(variant==7?18:0);
                    foreach(float vx in new[]{-4f,0f,4f})foreach(float vy in new[]{-4f,.3f,4f})
                    for(int x=938;x<=980;x+=7)for(int y=920;y<=982;y+=7)
                    {
                        var n=new NPC();n.SetDefaults(3);n.active=true;n.whoAmI=199;n.position=new Vector2(x+.25f,y+.75f);n.velocity=new Vector2(vx,vy);n.width=20;n.height=32;n.directionY=-1;
                        var model=(NpcMotionState)read.Invoke(null,new object[]{n,1L});model.Gravity=.3f;terrain.Reset();PredictionStop stop;
                        Require(terrain.Move(ref model,default(PredictionEnvironment),out stop),"terrain should evaluate actual slope contact, not stop for a nearby tile: slope="+slope+" stop="+stop);
                        nativeMove.Invoke(n,null);
                        float error=Vector2.Distance(n.position,new Vector2(model.X,model.Y));
                        Require(error<.015f && Math.Abs(n.velocity.X-model.Vx)<.015f && Math.Abs(n.velocity.Y-model.Vy)<.015f && n.collideX==model.CollideX && n.collideY==model.CollideY,
                            "native terrain variant="+variant+" slope="+slope+" start="+x+","+y+" v="+vx+","+vy+" native="+n.position+" / "+n.velocity+" model="+model.X+","+model.Y+" / "+model.Vx+","+model.Vy);
                        if(n.collideX || n.collideY || n.position!=new Vector2(x+.25f+vx,y+.75f+vy))contacts++;cases++;
                    }
                }
                Require(contacts>0,"slope corpus really makes contact");Console.WriteLine("TERRAIN native UpdateCollision slope cases="+cases+" contacts="+contacts);
            }
            finally{Main.tile[60,60]=saved;nativeGravity.SetValue(null,savedGravity);}
        }
    }
}
