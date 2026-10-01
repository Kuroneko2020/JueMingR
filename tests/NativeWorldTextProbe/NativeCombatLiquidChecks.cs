using System;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;

namespace NativeWorldTextProbe
{
    // One frozen instant, then all 120 original NPC updates. The same owned
    // transport produces the real NewSegment proof, not stdout-only futures.
    internal static class NativeCombatLiquidChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(Assembly host,string output,Func<byte[],byte[]> exchange,Func<byte[]> alignment)
        {
            var proofType=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true);
            foreach(string scene in new[]{"241-bottom","242-wall","241-slope","242-surface","3-enter","3-exit"})
            {
                NativeCombatWorkerChecks.Scene(false);NPC.ClearAll();Projectile.ClearAll();
                // Stay inside the actual 640px border and on dry ground,
                // matching the existing independent-NPC oracle contract.
                // An earlier X=1500 was clamped to1260 inside the pool only in
                // the worker, incorrectly changing the fish pursuit premise.
                Main.LocalPlayer.position.X=1200;
                for(int i=0;i<Main.LocalPlayer.hurtCooldowns.Length;i++)Main.LocalPlayer.hurtCooldowns[i]=100000;
                for(int x=20;x<70;x++)for(int y=40;y<65;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(0);}
                Require(Main.LocalPlayer.position.X>=640 && Main.LocalPlayer.position.X+Main.LocalPlayer.width<=Main.rightWorld-640 && !Collision.WetCollision(Main.LocalPlayer.position,Main.LocalPlayer.width,Main.LocalPlayer.height),"Independent oracle player stays inside the native border on dry ground.");
                int type=int.Parse(scene.Substring(0,scene.IndexOf('-')));
                if(scene.EndsWith("wall",StringComparison.Ordinal))for(int y=40;y<65;y++){Main.tile[49,y].active(true);Main.tile[49,y].type=1;}
                if(scene.EndsWith("slope",StringComparison.Ordinal))for(int x=40;x<60;x++){Main.tile[x,64].active(true);Main.tile[x,64].type=1;Main.tile[x,64].slope(1);}
                int yStart=scene.Contains("surface") || scene.Contains("exit")?665:scene.Contains("enter")?590:1020;
                int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),750,yStart,type,Start:1);var npc=Main.npc[slot];
                npc.velocity=scene.Contains("surface") || scene.Contains("exit")?new Vector2(0,-6):scene.Contains("wall")?new Vector2(4,0):new Vector2(1,3);
                // Initial observed occupancy is real geometry, not a future
                // per-frame patch; the original update owns all transitions.
                npc.wet=Collision.WetCollision(npc.position,npc.width,npc.height);npc.honeyWet=Collision.honey;npc.shimmerWet=Collision.shimmer;
                var frozen=NativeCombatWorkerChecks.AcquireFrozen(host,exchange,new[]{slot},new int[0],slot);
                byte[] proof=alignment();Require(proof!=null,"Production transport supplies movement phase proof.");
                using(var stream=new MemoryStream(proof))using(var reader=new BinaryReader(stream))
                {
                    Require(reader.ReadInt32()==121,"Complete liquid proof horizon.");
                    for(int i=0;i<=120;i++){object frame=proofType.GetMethod("Read",Flags).Invoke(null,new object[]{reader});Require((long)frame.GetType().GetField("Tick",Flags).GetValue(frame)==1000+i && !(bool)frame.GetType().GetField("NewSegment",Flags).GetValue(frame),"Normal original liquid/collision motion must stay continuous: "+scene+" step="+i);}
                    reader.ReadInt64();host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeTerrainUsage",true).GetMethod("Read",Flags).Invoke(null,new object[]{reader});Require(stream.Position==stream.Length,"Complete liquid proof consumption.");
                }
                int wet=0,dry=0,xContact=0,yContact=0,transitions=0,slope=0;bool prior=npc.wet;
                NativeCombatWorkerChecks.Compare(frozen.Future,slot,output,"liquid-"+scene,dependencyComparison:NativeCombatLegalCoverageChecks.CompareMotion,motionTolerance:.002f,nativeStep:(tick,n)=>
                {
                    if(n.wet)wet++;else dry++;if(n.wet!=prior)transitions++;prior=n.wet;if(n.collideX)xContact++;if(n.collideY)yContact++;
                    if(n.Bottom.Y>=64*16 && n.Bottom.Y<=65*16+1 && n.Center.X>=40*16 && n.Center.X<60*16)slope++;
                });
                Console.WriteLine("LIQUID-ORACLE "+scene+" wet="+wet+" dry="+dry+" transitions="+transitions+" collideX="+xContact+" collideY="+yContact+" slope-region="+slope);
                Require(wet>0,"Liquid scene really enters water.");
                if(scene.Contains("bottom"))Require(yContact>0,"Bottom case really collides.");
                if(scene.Contains("wall"))Require(xContact>0,"Wall case really collides.");
                if(scene.Contains("slope"))Require(slope>0,"Slope case really reaches the sloped surface.");
                if(scene.Contains("surface") || scene.Contains("enter") || scene.Contains("exit"))Require(transitions>0 && dry>0,"Surface case really changes wet state.");
            }
        }
        private static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    }
}
