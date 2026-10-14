using System;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatDedupModelChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var aim=Get(combat,"Attack");var input=Get(context,"Input");var assembly=aim.GetType().Assembly;
            foreach(int item in new[]{95,1228,ContentSamples.ItemsByType.Values.First(i=>i.shoot==130).type,ContentSamples.ItemsByType.Values.First(i=>i.shoot==730).type,ContentSamples.ItemsByType.Values.First(i=>i.shoot==342).type,ItemID.BlandWhip,4956})
            foreach(bool moving in new[]{false,true})
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,item,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.Center=new Vector2(970,620);n.aiStyle=-1;n.noGravity=true;n.noTileCollide=true;n.life=n.lifeMax=100000;
                NativeToolExecutionChecks.Sample(context,input,new Vector2(600,500),true);Call(combat,"Sample");NativeQuickItemChecks.BeginWorldStep();
                var id=new NpcIdentity((long)Get(host,"Session"),n,2,0,n.type,n.netID);
                var state=new NpcMotionState{Identity=id,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,Active=true,CanReceive=true,NoTileCollide=true};
                foreach(int count in new[]{17,33,65,121})
                {
                    var points=new NpcTrajectoryPoint[count];for(int t=0;t<count;t++){var step=state;step.X+=moving?.3f*t:0;step.Y+=moving?.15f*t:0;points[t]=new NpcTrajectoryPoint(t,step);}
                    var timeline=new NpcTrajectory(id,(long)Main.GameUpdateCount-1,1,PredictionAssumption.HeldPlayerControls,PredictionStop.None,points,count);
                    var terrain=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain"),true);
                    var motionBefore=(PredictionPlayerMotion)assembly.GetType("JueMingR.TerrariaHost.Combat.NpcPredictionSource").GetMethod("ReadPlayer",Flags).Invoke(null,new object[]{p});
                    var ammo=assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot").GetMethod("Capture",Flags).Invoke(null,new object[]{p,p.HeldItem});
                    var origin=(Vector2)assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels").GetMethod("LaunchOrigin",Flags).Invoke(null,new object[]{p,p.HeldItem});
                    AttackContact result;using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))result=(AttackContact)aim.GetType().GetMethod("SolveOnTerrain",Flags).Invoke(null,new object[]{p,p.HeldItem,ammo,timeline,origin,0,true,terrain,0,null,null,null});
                    var motionAfter=(PredictionPlayerMotion)assembly.GetType("JueMingR.TerrariaHost.Combat.NpcPredictionSource").GetMethod("ReadPlayer",Flags).Invoke(null,new object[]{p});
                    Require(motionBefore.SameSample(motionAfter) && p.inventory[54].stack==999,"pure model preserves full real player premise and ammo");
                    Console.WriteLine("MODEL item="+item+" moving="+moving+" count="+count+" contact="+(result!=null)+" aim="+(result==null?"none":result.AimX+","+result.AimY)+" impact="+(result==null?"none":result.ImpactX+","+result.ImpactY)+" tick="+(result?.Tick??-1)+" receive="+(result?.ReceiveTick??-1)+" sub="+(result?.Subupdate??-1)+" confidence="+result?.Confidence);
                }
            }
        }
    }
}
