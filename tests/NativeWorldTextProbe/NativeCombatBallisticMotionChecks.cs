using System;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatBallisticMotionChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static NpcTrajectory Path(NpcIdentity identity,Vector2 center,int width=20,int height=24)
        {
            var state=new NpcMotionState{Identity=identity,X=center.X-width*.5f,Y=center.Y-height*.5f,Width=width,Height=height,Active=true,CanReceive=true};
            var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();
            return new NpcTrajectory(identity,(long)Main.GameUpdateCount-1,1,PredictionAssumption.None,PredictionStop.None,points,points.Length);
        }
        private static AttackMotion Motion(Assembly host,int projectile,float speed)
        {
            var args=new object[]{projectile,speed,0,null};
            Require((bool)host.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels",true).GetMethod("TryResolved",Flags).Invoke(null,args),"resolved native member has an explicit motor: "+projectile);
            return (AttackMotion)args[3];
        }
        private static int NativeFirst(Player player,int type,Vector2 origin,Vector2 velocity,NpcTrajectory path)
        {
            int slot=Projectile.NewProjectile(player.GetProjectileSource_Item(player.HeldItem),origin,velocity,type,0,0,player.whoAmI);var native=Main.projectile[slot];int first=0;
            for(int outer=1;outer<=120 && native.active;outer++)
            {
                NativeQuickItemChecks.BeginWorldStep();native.Update(slot);var b=path[outer].ProjectileReceiveBounds;
                if(native.Hitbox.Intersects(new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height))){first=outer;break;}
            }
            native.active=false;return first;
        }
        internal static void Run(object context)
        {
            var host=Get(context,"Combat").GetType().Assembly;int cases=0,contacts=0;
            // Different items share AI002's delayed gravity/horizontal drag;
            // snowball and grenade use distinct starts/damping and receive
            // stages. 1006 is the opposite-gravity/max-rise counterexample.
            foreach(int type in new[]{3,21,48,54,166,497,133,1006})foreach(float elevation in new[]{-.45f,.15f})
            {
                var player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),517,0,0);
                Main.windPhysics=false;foreach(var npc in Main.npc)npc.active=false;foreach(var shot in Main.projectile)shot.active=false;
                for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();
                Vector2 origin=new Vector2(700,350),velocity=new Vector2((float)Math.Cos(elevation),(float)Math.Sin(elevation))*10;
                var motion=Motion(host,type,10);
                Require(AttackMotionCandidates.Supports(motion),"all selected mechanism members use the shared separable response");
                int slot=Projectile.NewProjectile(player.GetProjectileSource_Item(player.HeldItem),origin,velocity,type,0,0,player.whoAmI);
                var original=Main.projectile[slot];
                for(int outer=1;outer<=35 && original.active;outer++){NativeQuickItemChecks.BeginWorldStep();original.Update(slot);}
                Require(original.active,"the actual native source survives to the witnessed target placement");
                var center=original.Center;original.active=false;
                var identity=new NpcIdentity(1,new object(),2,1,1,1);var path=Path(identity,center);
                var proposals=AttackMotionCandidates.Generate(path,0,origin.X,origin.Y,motion);
                Require(proposals.Length>0 && proposals.Length<=8,"finite inverse-region proposals retain a reachable native body: "+type);
                int oldFirst=0,newFirst=0;
                var old=AttackIntercept.Solve(origin.X,origin.Y,motion,path,0,0);
                if(old!=null)
                {
                    var unit=new Vector2(old.AimX-origin.X,old.AimY-origin.Y).SafeNormalize(Vector2.UnitX);
                    oldFirst=NativeFirst(player,type,origin,unit*10,path);
                }
                bool received=false;
                foreach(var input in proposals)
                {
                    // The production caller returns its FIRST feasible input.
                    // Compare that same selection order to the replaced direct
                    // proposal using the original motors, not a best-of oracle.
                    newFirst=NativeFirst(player,type,origin,new Vector2(input.X,input.Y)*10,path);
                    if(newFirst>0){received=true;break;}
                }
                Require(received,"shared proposal reaches the receiver under unmodified original projectile Update: "+type+"/"+elevation);
                Require(oldFirst==0 || newFirst<=oldFirst,"first feasible inverse proposal retains the earlier low-arc receive type="+type+" old="+oldFirst+" new="+newFirst);
                Console.WriteLine("BALLISTIC-MOTION-QUALITY type="+type+" elevation="+elevation+" oldNative="+oldFirst+" newNative="+newFirst);
                cases++;contacts++;
            }
            // Exact scalar endpoints across both cap switches. This is a
            // mathematical differential, separate from the original motors.
            var position=typeof(AttackMotionCandidates).GetMethod("PositionY",Flags);
            foreach(float gravity in new[]{.4f,-.4f})foreach(float launch in new[]{-12f,0f,12f})
            {
                var motion=new AttackMotion(14,gravity,20,3,4,4,360,maxFallSpeed:16,maxRiseSpeed:16);
                float x=0,y=0,vx=0,vy=launch;
                for(int sub=1;sub<=360;sub++)
                {
                    motion.Advance(ref x,ref y,ref vx,ref vy,sub);
                    double proposed=(double)position.Invoke(null,new object[]{motion,sub,(double)launch});
                    Require(Math.Abs(proposed-y)<.035,"discrete response retains pre-gravity/cap movement order at "+sub);
                }
            }
            Console.WriteLine("BALLISTIC-MOTION native-members=8 native-cases="+cases+" native-contacts="+contacts+" scalar-cap-actions=2160");
        }
    }
}
