using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Utilities;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatFlyingTailChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static void Aim(object context,object host,NPC n)
        {typeof(NativeCombatPositionRelationChecks).GetMethod("Aim",Flags).Invoke(null,new object[]{context,host,n});}
        private static void Native(NPC n)
        {typeof(NativeCombatFighterControlChecks).GetMethod("Native",Flags).Invoke(null,new object[]{n});}
        internal static void Pet(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;
            for(int branch=0;branch<3;branch++)
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2]=new NPC();n.SetDefaults(210);n.whoAmI=2;n.position=new Vector2(650,790);n.velocity=new Vector2(1,0);n.target=0;n.direction=1;n.ai[1]=120;
                p.Center=new Vector2(n.Center.X+200,960-p.height*.5f);p.tankPet=5;p.npcTypeNoAggro[210]=branch==2;var pet=Main.projectile[5];pet.SetDefaults(625);pet.whoAmI=5;pet.owner=0;pet.active=true;pet.Center=n.Center-new Vector2(80,0);pet.velocity=Vector2.Zero;
                for(int y=40;y<=57;y++){Main.tile[39,y].active(branch==1);Main.tile[39,y].type=1;}
                var search=NPCUtils.SearchForTarget(n,NPCUtils.TargetSearchFlag.All,null,NPCUtils.SearchFilters.NonBeeNPCs);
                Require(search.FoundTank && (search.NearestTankType==NPCUtils.TargetType.TankPet)==(branch==0),"Real guardian query positive/LOS/qualification branch="+branch);
                Aim(context,host,n);cache.Demand(0,1,120);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Native(n);
                Console.WriteLine("BEE PET branch="+branch+" kind="+search.NearestTankType+" count="+path?.Count+" stop="+Get(source,"outcomeStop")+" native="+n.velocity);
                Require(path!=null && path.Count>1 && Math.Abs(path[1].Vx-n.velocity.X)<.01f && Math.Abs(path[1].Vy-n.velocity.Y)<.01f,"Actual Source guardian finite winner uses valid LOS cells and owner motion");
            }
            p.tankPet=-1;p.npcTypeNoAggro[210]=false;Main.projectile[5].active=false;for(int y=40;y<=57;y++)Main.tile[39,y].active(false);
            Console.WriteLine("PASS bee guardian winner/blocked LOS/no-aggro negative Source first actions=3");
        }
        internal static void Tail(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;
            foreach(int branch in new[]{0,1,2,3,4})
            {
                foreach(var npc in Main.npc)npc.active=false;Main.dayTime=branch>=2;Main.remixWorld=branch==3;Main.netMode=branch==4?0:1;NPC.mechQueen=-1;
                var n=Main.npc[2]=new NPC();n.SetDefaults(branch==4?619:139);n.whoAmI=2;n.position=new Vector2(650,960-n.height);n.velocity=new Vector2(1,2);n.oldVelocity=n.velocity;n.target=0;n.direction=1;n.ai[3]=branch==0?1:0;n.ai[2]=-1;n.localAI[0]=branch==4?119:0;n.alpha=0;n.dontTakeDamage=n.immortal=n.friendly=false;
                p.position=new Vector2(850,960-p.height);p.velocity=Vector2.Zero;Aim(context,host,n);cache.Demand(0,1,120);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Native(n);
                Console.WriteLine("FLY TAIL branch="+branch+" type="+n.type+" count="+path?.Count+" stop="+Get(source,"outcomeStop")+" native="+n.velocity+" phase="+n.ai[3]+" local="+n.localAI[0]);
                Require(path!=null && path.Count>1 && Math.Abs(path[1].Vx-n.velocity.X)<.01f && Math.Abs(path[1].Vy-n.velocity.Y)<.01f,"AI5 real detach/free/IsItDay-remix/recoil native first motion branch="+branch);
            }
            Main.dayTime=Main.remixWorld=false;Main.netMode=1;Console.WriteLine("PASS AI5 finite detach/free/world day/recoil Source first actions=5");
        }
    }
}
