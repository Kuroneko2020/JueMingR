using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatBallisticSeedChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(object context)
        {
            var host=Get(context,"Combat").GetType().Assembly;
            var motor=host.GetType("JueMingR.TerrariaHost.Combat.HostSeedThrowAttack",true);
            var stateType=host.GetType("JueMingR.TerrariaHost.Combat.HostFiniteThrowAttack",true).GetNestedType("State",Flags);
            var terrainType=host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);
            int cases=0,actions=0;
            foreach(int type in new[]{42,65,68,354,1081})foreach(int counter in new[]{0,18,58})foreach(int liquid in new[]{-1,0,2})
            {
                var player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),266,0,0);player.channel=false;Main.windPhysics=false;
                foreach(var npc in Main.npc)npc.active=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();
                if(liquid>=0)for(int x=20;x<115;x++)for(int y=5;y<78;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(liquid);}
                int slot=Projectile.NewProjectile(player.GetProjectileSource_Item(player.HeldItem),new Vector2(700,350),new Vector2(7,-1),type,0,0,player.whoAmI);
                var original=Main.projectile[slot];original.ai[0]=type==1081?counter:1;original.ai[1]=type==1081?0:counter;
                var state=Activator.CreateInstance(stateType,Flags,null,new object[]{original,original.Center,original.velocity},null);
                stateType.GetField("Counter",Flags).SetValue(state,(float)counter);
                var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");
                for(int outer=1;outer<=50 && original.active;outer++)
                {
                    for(int sub=0;sub<=original.extraUpdates && (bool)Get(state,"Alive");sub++)
                    {
                        var args=new object[]{original,terrain,state};Require((bool)motor.GetMethod("Step",Flags).Invoke(null,args),"seed finite motor has acquired its concrete terrain");state=args[2];actions++;
                    }
                    NativeQuickItemChecks.BeginWorldStep();original.Update(slot);
                    Require(original.active==(bool)Get(state,"Alive") && (!original.active || Vector2.DistanceSquared(original.Center,(Vector2)Get(state,"Center"))<.002f && Vector2.DistanceSquared(original.velocity,(Vector2)Get(state,"Velocity"))<.0001f && original.timeLeft==(int)Get(state,"Left")),
                        "native AI010 released seed/mud position, velocity, first clock and wet movement agree: "+type+"/"+counter+"/"+liquid+"/"+outer+" native="+original.Center+"/"+original.velocity+" model="+Get(state,"Center")+"/"+Get(state,"Velocity"));
                }
                cases++;
            }
            Console.WriteLine("BALLISTIC-SEED native-cases="+cases+" native-actions="+actions+" members=42,65,68,354,1081");
        }
    }
}
