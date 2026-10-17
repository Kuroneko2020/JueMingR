using System;
using System.Linq;
using System.Reflection;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatBallisticTyphoonChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(object context)
        {
            var host=Get(context,"Combat").GetType().Assembly;int cases=0,actions=0;
            foreach(int immune in new[]{0,20,-1})foreach(bool blocked in new[]{false,true})foreach(float angle in new[]{-.4f,.7f})
            {
                var player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),2622,0,0);Main.windPhysics=false;
                foreach(var npc in Main.npc)npc.active=false;var target=Main.npc[2];target.SetDefaults(1);target.whoAmI=2;target.active=true;target.position=new Vector2(850,350);target.immune[player.whoAmI]=immune;
                if(blocked)for(int y=5;y<80;y++)NativeToolsChecks.Tile(48,y,1);
                int slot=Projectile.NewProjectile(player.GetProjectileSource_Item(player.HeldItem),new Vector2(700,350),angle.ToRotationVector2()*6,409,0,0,player.whoAmI);var native=Main.projectile[slot];
                var shadow=(Projectile)host.GetType("JueMingR.TerrariaHost.Combat.HostBetsyFlameAttack",true).GetMethod("Copy",Flags).Invoke(null,new object[]{native});
                var identity=new NpcIdentity(1,target,2,target.generation,target.type,target.netID);var state=new NpcMotionState{Identity=identity,X=target.position.X,Y=target.position.Y,Width=target.width,Height=target.height,CanReceive=true,Active=true};
                var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();var path=new NpcTrajectory(identity,Main.GameUpdateCount,1,PredictionAssumption.None,PredictionStop.None,points,points.Length);
                var root=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{player,shadow,path,0,true,false},null);
                var targets=Call(root,"CaptureOwnerChaseTargets",shadow);var terrain=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");
                for(int action=1;action<=180;action++)
                {
                    int outer=(action-1)/3+1;if((action-1)%3==0 && target.immune[player.whoAmI]>0)target.immune[player.whoAmI]--;
                    Require((bool)host.GetType("JueMingR.TerrariaHost.Combat.HostTyphoonAttack",true).GetMethod("Ai",Flags).Invoke(null,new object[]{shadow,terrain,targets,path,outer,outer,root}),"AI071 captured target/terrain");
                    typeof(Projectile).GetMethod("AI",Flags).Invoke(native,null);
                    Require(Vector2.DistanceSquared(native.velocity,shadow.velocity)<.000001f && native.ai[0]==shadow.ai[0] && native.localAI[0]==shadow.localAI[0],"AI071 native acquisition/immune/angle/cooldown action="+action+" immune="+immune+" blocked="+blocked+" native="+native.velocity+"/"+native.ai[0]+" model="+shadow.velocity+"/"+shadow.ai[0]);
                    native.position+=native.velocity;shadow.position+=shadow.velocity;actions++;
                }
                cases++;
            }
            Console.WriteLine("BALLISTIC-TYPHOON nativeCases="+cases+" aiActions="+actions+" ownerTimers=0,20,-1 LOS=clear,blocked");
        }
    }
}
