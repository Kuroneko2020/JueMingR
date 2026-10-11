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
    internal static class NativeCombatWhipTerrainChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,5480,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.itemAnimationMax=p.itemAnimation=30;Main.screenPosition=new Vector2(600,500);
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(850,646);n.velocity=n.netOffset=Vector2.Zero;n.life=n.lifeMax=10000;n.target=0;Array.Clear(n.immune,0,n.immune.Length);
            NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");
            typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});
            var primary=Main.projectile.Single(q=>q.active && q.type==1035);primary.ai[0]=19;primary.ai[2]=1;
            Require(p.HeldItem.shoot==1035 && ContentSamples.ProjectilesByType[1035].ownerHitCheck && primary.ownerHitCheck,"actual 5480/1035 sample and original primary own the native owner-hit-check gate");
            var assembly=attack.GetType().Assembly;var whip=assembly.GetType("JueMingR.TerrariaHost.Combat.HostWhipAttack",true);var terrainType=assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);var frameType=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPlayerFrame",true);var phaseType=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true);var clockType=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true);
            try
            {
                foreach(bool current in new[]{false,true})foreach(bool wall in new[]{false,true})
                {
                    bool future=!current;n.noTileCollide=current;
                    for(int y=20;y<=42;y++){Main.tile[48,y].active(wall);Main.tile[48,y].type=1;}
                    int x=Main.mouseX,yMouse=Main.mouseY;var velocity=primary.velocity;float age=primary.ai[0],role=primary.ai[2];int sources=Main.projectile.Count(q=>q.active);
                    foreach(string consumer in new[]{"idle-preview","primary-child","extra-use"})
                    {
                        string phase=consumer=="idle-preview"?"CompletedWorld":"BeforeNpc";
                        var timeline=FuturePath(n,(long)Get(host,"Session"),current,future,phase=="BeforeNpc");
                        var clock=Activator.CreateInstance(clockType,Flags,null,new object[]{timeline,Enum.Parse(phaseType,phase)},null);var terrain=Activator.CreateInstance(terrainType,true);
                        bool hit;int tick=-1;
                        if(consumer=="extra-use")
                        {var args=new object[]{p,timeline,clock,terrain,Vector2.Zero};hit=(bool)whip.GetMethod("TryExtraPoint",Flags).Invoke(null,args);}
                        else
                        {
                            var frame=frameType.GetMethod("Capture",Flags).Invoke(null,new object[]{p,timeline});
                            var args=new object[]{p,consumer=="idle-preview"?null:primary,timeline,clock,terrain,Vector2.Zero,frame};
                            var contact=(AttackContact)whip.GetMethod("ContactPrepared",Flags).Invoke(null,args);hit=contact!=null;if(hit)tick=contact.ReceiveTick;
                        }
                        Console.WriteLine("WHIP FUTURE consumer="+consumer+" current="+current+" future="+future+" wall="+wall+" contact="+hit+" receive="+tick);
                        Require(hit==(!wall || future),"real whip shape/owner consumer follows future noTileCollide, including opposite current state: "+consumer+" current="+current+" future="+future+" wall="+wall);
                        if(hit && tick>=0)Require(tick>0 && timeline[tick].NoTileCollide==future,"contact belongs to the explicit future flag rather than the capture point");
                    }
                    Require(Main.mouseX==x && Main.mouseY==yMouse && primary.velocity==velocity && primary.ai[0]==age && primary.ai[2]==role && Main.projectile.Count(q=>q.active)==sources,"whip future preview/shared input solvers do not consume physical cursor or mutate native primary/pool");
                }
                Console.WriteLine("PASS real 5480/1035 whip future owner gate: both flag directions, wall/open reach controls, idle/primary-child/extra shared consumers; explicit future-path oracle, not full native NPC phase coverage.");
            }
            finally{NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
        // An explicit immutable future input isolates the weapon consumer.
        // Native sample, whip rectangles and stone owner line are real; this
        // does not claim to validate every NPC motor that can change the flag.
        private static NpcTrajectory FuturePath(NPC n,long session,bool current,bool future,bool beforeNpc)
        {
            var identity=new NpcIdentity(session,n,n.whoAmI,1,n.type,n.netID);var points=new NpcTrajectoryPoint[121];
            for(int tick=0;tick<points.Length;tick++)points[tick]=new NpcTrajectoryPoint(tick,new NpcMotionState{Identity=identity,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,Active=true,CanReceive=true,NoTileCollide=tick==0?current:future});
            return new NpcTrajectory(identity,(long)Main.GameUpdateCount-(beforeNpc?1:0),1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length);
        }
    }
}
