using System;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Compare the same native family solver with/without its semantic scene
    // reduction. The old route exists only in this isolated Harmony oracle.
    internal static class NativeCombatBallisticSceneChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly host;
        private static bool fullScene;
        private static int motors;
        private static bool AllMembers(Projectile[] __0,ref Projectile[] __result)
        {if(!fullScene)return true;__result=__0;return false;}
        private static void Motor(){motors++;}
        private static object New(string name,params object[] args)
        {var type=host.GetType("JueMingR.TerrariaHost.Combat."+name,true);return args.Length==0?Activator.CreateInstance(type,true):Activator.CreateInstance(type,Flags,null,args,null);}
        private static Projectile Ambient(int slot,Player player)
        {
            var shot=Main.projectile[slot];shot.SetDefaults(711);shot.active=true;shot.whoAmI=slot;shot.owner=player.whoAmI;
            shot.Center=new Vector2(320+slot*5,400);shot.velocity=new Vector2(2,-2);shot.damage=40;
            return shot;
        }
        private static object Root(Player player,NpcTrajectory path)
        {
            var shot=new Projectile();shot.SetDefaults(711);shot.owner=player.whoAmI;shot.damage=50;
            return New("HostAttackObstacles",player,shot,path,0,true,false);
        }
        private static Projectile[] Relevant(Player player,NpcTrajectory path,Projectile[] scene,Projectile[] sources=null,int delay=0)
        {
            return (Projectile[])host.GetType("JueMingR.TerrariaHost.Combat.HostBetsyFlameAttack",true).GetMethod("RelevantScene",Flags)
                .Invoke(null,new object[]{scene,sources,Root(player,path),delay});
        }
        private static AttackContact Solve(Player player,NpcTrajectory path)
        {
            var terrain=New("PredictionTerrain");terrain.GetType().GetMethod("Reset",Flags).Invoke(terrain,null);
            return (AttackContact)host.GetType("JueMingR.TerrariaHost.Combat.HostBetsyFlameAttack",true).GetMethod("Solve",Flags).Invoke(null,
                new object[]{player,NativeCombatBallisticCandidateChecks.Ammo(player),path,player.RotatedRelativePoint(player.MountedCenter),0,true,terrain,Root(player,path),0});
        }
        internal static void Run(object context)
        {
            host=Get(context,"Combat").GetType().Assembly;
            var type=host.GetType("JueMingR.TerrariaHost.Combat.HostBetsyFlameAttack",true);
            var audit=new Harmony("JueMingR.Tests.BallisticSemanticScene");
            audit.Patch(type.GetMethod("RelevantScene",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBallisticSceneChecks),nameof(AllMembers)));
            audit.Patch(type.GetMethod("Motor",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBallisticSceneChecks),nameof(Motor)));
            try
            {
                var player=NativeCombatBallisticCandidateChecks.Scene(context,3870,80,tileY:25);
                var path=NativeCombatBallisticCandidateChecks.Timeline(Main.npc[2]);
                var scene=new Projectile[8];for(int i=0;i<scene.Length;i++)scene[i]=Ambient(20+i,player);
                // Different local histories cannot be shared, but they cannot
                // write another projectile's local history either.
                scene[0].localNPCImmunity[2]=-1;
                Require(Relevant(player,path,scene).Length==0,"already-clear owner immunity removes independent ambient local-only members");
                fullScene=true;motors=0;var baseline=Solve(player,path);int oldMotors=motors;
                fullScene=false;motors=0;var candidate=Solve(player,path);int newMotors=motors;
                Require(baseline!=null && candidate!=null && baseline.Tick==candidate.Tick && baseline.Subupdate==candidate.Subupdate && baseline.AimX==candidate.AimX && baseline.AimY==candidate.AimY && baseline.ImpactX==candidate.ImpactX && baseline.ImpactY==candidate.ImpactY,
                    "semantic reduction preserves the full ordered native-family result");
                Require(newMotors*2<oldMotors,"dense unrelated family no longer multiplies exact motor work by each input direction");
                Console.WriteLine("BALLISTIC-SCENE ambient="+scene.Length+" fullMotors="+oldMotors+" relevantMotors="+newMotors+" receive="+candidate.Tick);
                var sources=new[]{scene[1],scene[3]};
                Require(ReferenceEquals(Relevant(player,path,scene,sources),sources),"known-family keeps every actual contributing source and its private local state");
                Require(ReferenceEquals(Relevant(player,path,scene,delay:1),scene),"delayed births retain distant retirement/occupied-slot effects");
                scene[0].usesIDStaticNPCImmunity=true;
                Require(ReferenceEquals(Relevant(player,path,scene),scene),"a shared static-immunity writer cannot be pruned as independent local history");
                scene[0].usesIDStaticNPCImmunity=false;scene[0].usesLocalNPCImmunity=false;
                Require(ReferenceEquals(Relevant(player,path,scene),scene),"an owner-immunity writer preserves actual array order and penetration effects");
                scene[0].usesLocalNPCImmunity=true;Main.npc[2].immune[player.whoAmI]=8;
                Require(ReferenceEquals(Relevant(player,path,scene),scene),"even a distant ambient shot is retained when clearing selected owner immunity can matter");
                Main.npc[2].immune[player.whoAmI]=0;
                var other=Main.npc[3];other.SetDefaults(3);other.whoAmI=3;other.active=true;other.position=new Vector2(800,630);other.velocity=Vector2.Zero;other.immune[player.whoAmI]=8;
                Require(ReferenceEquals(Relevant(player,path,scene),scene),"an earlier other receiver's owner gate can change source penetration before selected contact");
                other.active=false;
            }
            finally{fullScene=false;audit.UnpatchAll(audit.Id);}
        }
    }
}
