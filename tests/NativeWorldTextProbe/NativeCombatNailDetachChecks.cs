using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatNailDetachChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static bool recording,recorded;private static int nailSlot,seed;private static Vector2 predicted;private static UnifiedRandom saved;
        private static void ModelStep(Projectile __0){if(recording && !recorded && __0.whoAmI==nailSlot){recorded=true;predicted=__0.Center;}}
        private static void NativeAi(Projectile __instance){if(__instance.whoAmI==nailSlot && __instance.type==514 && __instance.ai[0]<0){saved=Main.rand;Main.rand=new UnifiedRandom(seed);}}
        private static void AfterAi(){if(saved!=null){Main.rand=saved;saved=null;}}
        private static bool NoSend(){return false;}
        private static NPC Target(int slot,Vector2 center)
        {var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.width=n.height=40;n.Center=center;n.life=n.lifeMax=100000;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;return n;}
        // Serialize original packet23 fields. GetData performs the actual
        // position write followed by inactive transition before preparation.
        private static void DeadPacket(NPC n,Vector2 center)
        {
            var buffer=new MessageBuffer{whoAmI=256};var position=center-n.Size*.5f+NPCID.Sets.SyncAnchor[n.type]*n.Size;
            using(var bytes=new MemoryStream())using(var writer=new BinaryWriter(bytes))
            {
                writer.Write((byte)23);writer.Write((byte)n.whoAmI);writer.Write((byte)n.generation);writer.Write(position.X);writer.Write(position.Y);writer.Write(0f);writer.Write(0f);writer.Write((ushort)65535);
                writer.Write((byte)0);writer.Write((byte)0);writer.Write((short)n.netID);writer.Write((byte)1);writer.Write((sbyte)0);writer.Flush();
                var data=bytes.ToArray();Array.Copy(data,buffer.readBuffer,data.Length);int message;buffer.GetData(0,data.Length,out message);Require(message==23 && !n.active && n.life==0 && n.Center==center,"original packet23 moved and invalidated exactly the attached slot");
            }
        }
        internal static void Run(object context)
        {
            NativeCombatAimChecks.Initialize();
            var host=Get(context,"Combat").GetType().Assembly;var model=host.GetType("JueMingR.TerrariaHost.Combat.HostNailAttack",true);var patch=new Harmony("JueMingR.Tests.ReviewedNailDetach");
            patch.Patch(model.GetMethod("Step",Flags),postfix:new HarmonyMethod(typeof(NativeCombatNailDetachChecks),nameof(ModelStep)));
            patch.Patch(typeof(Projectile).GetMethod("AI",Flags),prefix:new HarmonyMethod(typeof(NativeCombatNailDetachChecks),nameof(NativeAi)),postfix:new HarmonyMethod(typeof(NativeCombatNailDetachChecks),nameof(AfterAi)));
            // The original client AI may request a sync after detaching. Its
            // transport is outside this in-process packet/physics oracle.
            patch.Patch(typeof(NetMessage).GetMethod("SendData",Flags),prefix:new HarmonyMethod(typeof(NativeCombatNailDetachChecks),nameof(NoSend)));
            int mode=Main.netMode;var sections=Main.sectionManager;var random=Main.rand;
            try
            {
                var p=NativeCombatBallisticCandidateChecks.Scene(context,3107,50);foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;
                for(int x=5;x<115;x++)for(int y=5;y<78;y++)Main.tile[x,y].ClearEverything();Main.windPhysics=false;
                var attached=Target(1,new Vector2(1010,500));var selected=Target(2,new Vector2(1600,500));
                nailSlot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(1000,500),new Vector2(10,0),514,100,0,p.whoAmI);var actual=Main.projectile[nailSlot];actual.ai[0]=-2;actual.ai[1]=10;actual.ignoreWater=true;
                Main.netMode=1;Main.sectionManager=new WorldSections(1,1);Main.sectionManager.SetAllSectionsLoaded();DeadPacket(attached,new Vector2(1200,500));
                var id=new NpcIdentity(1,selected,2,selected.generation,selected.type,selected.netID);var state=new NpcMotionState{Identity=id,X=selected.position.X,Y=selected.position.Y,Width=selected.width,Height=selected.height,Active=true,CanReceive=true,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(i=>new NpcTrajectoryPoint(i,state)).ToArray();var path=new NpcTrajectory(id,Main.GameUpdateCount,1,PredictionAssumption.None,PredictionStop.None,points,points.Length);
                var terrain=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");var root=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,actual,path,0,false,true},null);var clock=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld")},null);
                recording=true;recorded=false;using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))model.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{p,new[]{actual},path,clock,terrain,root});recording=false;
                Require(recorded,"real KnownFamily actually advances the captured old attached source");
                bool initiallyCurrent=(bool)Get(root,"Unchanged");attached.position.X+=16;bool changedCurrent=(bool)Get(root,"Unchanged");attached.position.X-=16;
                // A positive X and exactly zero Y is a legal native six-speed
                // representative. Seed only this isolated original AI call;
                // later native effects and the caller's RNG are restored.
                for(seed=0;seed<100000;seed++){var sample=new UnifiedRandom(seed);if(sample.Next(-100,101)>0 && sample.Next(-100,101)==0)break;}Require(seed<100000,"legal horizontal native detach representative exists");
                NativeQuickItemChecks.BeginWorldStep();actual.Update(nailSlot);
                Require(actual.ai[0]==0 && actual.ai[1]==20 && actual.velocity==Vector2.UnitX*6,"original AI093 takes the legal invalid-target detach branch");
                Require(Vector2.DistanceSquared(predicted,actual.Center)<.0001f,"B10-F03: KnownFamily must use packet23's current slot geometry before invalid-target detach native="+actual.Center+" predicted="+predicted);
                Require(initiallyCurrent && !changedCurrent,"unchanged inactive attachment geometry is current, and a later inactive-slot mutation revokes the prepared family");
                Console.WriteLine("PASS packet23 -> capture -> original Nail AI: deterministic detach center and inactive-slot lease");
            }
            finally{recording=false;AfterAi();Main.rand=random;Main.netMode=mode;Main.sectionManager=sections;patch.UnpatchAll(patch.Id);}
        }
    }
}
