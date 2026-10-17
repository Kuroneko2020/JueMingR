using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatFixedSkyChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static bool legacy;
        // This test-only exclusion runs the unchanged TASK_BASE sky search
        // through the same current host interfaces and actual native motor.
        private static bool Fixed(ref AttackContact __result){if(!legacy)return true;__result=null;return false;}
        private static NpcTrajectory Path(NPC n)
        {
            var key=new NpcIdentity(1,n,n.whoAmI,n.generation,n.type,n.netID);
            var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true,Active=true,NoTileCollide=n.noTileCollide};
            var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(key,Main.GameUpdateCount,1,PredictionAssumption.None,PredictionStop.None,points,points.Length);
        }
        internal static void Run(object context)
        {
            var host=Get(context,"Combat").GetType().Assembly;var model=host.GetType("JueMingR.TerrariaHost.Combat.HostSkyAttack",true);var audit=new Harmony("JueMingR.Tests.FixedSky");
            audit.Patch(model.GetMethod("FixedSky",Flags),prefix:new HarmonyMethod(typeof(NativeCombatFixedSkyChecks),nameof(Fixed)));int cases=0;
            try
            {
                foreach(bool mirror in new[]{false,true})foreach(int distance in new[]{160,320,500})foreach(int immunity in new[]{0,12})
                {
                    var p=NativeCombatBallisticCandidateChecks.Scene(context,3065,50,mirror);foreach(var npc in Main.npc)npc.active=false;
                    var n=Main.npc[2];n.SetDefaults(3);n.active=true;n.whoAmI=2;n.life=n.lifeMax=10000;n.knockBackResist=0;n.defense=0;n.aiStyle=-1;n.noGravity=true;n.Center=p.Center+new Vector2(mirror?-distance:distance,0);n.immune[p.whoAmI]=immunity;
                    var path=Path(n);var ammo=host.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true).GetMethod("Capture",Flags).Invoke(null,new object[]{p,p.HeldItem});
                    var sample=Terraria.ID.ContentSamples.ProjectilesByType[(int)Get(ammo,"Projectile")];var origin=(Vector2)host.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels",true).GetMethod("LaunchOrigin",Flags).Invoke(null,new object[]{p,p.HeldItem});
                    var contacts=new AttackContact[2];var native=new int[2];var elapsed=new double[2];
                    for(int version=0;version<2;version++)
                    {
                        legacy=version==0;var terrain=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");
                        var root=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,sample,path,0,true,false},null);
                        var watch=System.Diagnostics.Stopwatch.StartNew();using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))contacts[version]=(AttackContact)model.GetMethod("Solve",Flags).Invoke(null,new object[]{p,p.HeldItem,ammo,path,origin,0,terrain,true,root,0});watch.Stop();elapsed[version]=watch.Elapsed.TotalMilliseconds;
                        if(contacts[version]==null)continue;
                        foreach(var shot in Main.projectile)shot.active=false;n.life=10000;n.immune[p.whoAmI]=immunity;
                        var launch=new object[]{p,p.HeldItem.type,origin,new Vector2(contacts[version].AimX,contacts[version].AimY),(float)Get(ammo,"Speed"),null,null};model.GetMethod("Launch",Flags).Invoke(null,launch);
                        int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),(Vector2)launch[5],(Vector2)launch[6],503,50,0,p.whoAmI,0,Math.Min(contacts[version].AimY,p.Center.Y-200));
                        for(int outer=1;outer<=120 && Main.projectile[slot].active;outer++)
                        {NativeQuickItemChecks.BeginWorldStep();if(n.immune[p.whoAmI]>0)n.immune[p.whoAmI]--;Main.projectile[slot].Update(slot);if(n.life<10000){native[version]=outer;break;}}
                        Require(native[version]>0 && native[version]==contacts[version].Tick,"fixed sky representative original Update first Damage agrees with exact receive clock version="+version);
                    }
                    Require(contacts[0]!=null && contacts[1]!=null && native[1]<=native[0],"fixed sky preserves every baseline useful source with no later first native receive");
                    Console.WriteLine("FIXED-SKY mirror="+mirror+" distance="+distance+" immune="+immunity+" old/new-native="+native[0]+"/"+native[1]+" elapsed-ms="+elapsed[0]+"/"+elapsed[1]);cases++;
                }
            }
            finally{legacy=false;audit.UnpatchAll(audit.Id);}
            Console.WriteLine("FIXED-SKY actual-native-pairs="+cases);
        }
    }
}
