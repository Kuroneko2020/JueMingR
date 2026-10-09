using System;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.ID;

namespace NativeWorldTextProbe
{
    // Actual event setup and death-driven wave progression. Source API calls
    // can accelerate births at an eligible gate; no wave, AI or corpse-list
    // values are fabricated to make a desired identity appear.
    internal static class NativeCombatDd2CoverageChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        internal static int Birth(int type,Assembly host,out string scene,bool beforeBetsy=false)
        {
            NativeCombatWorkerChecks.Scene(false);NPC.ClearAll();Projectile.ClearAll();DD2Event.ResetProgressEntirely();
            const int width=384,height=160,floor=65,center=192;
            Main.maxTilesX=width;Main.maxTilesY=height;Main.rightWorld=width*16;Main.bottomWorld=height*16;
            Main.tile=new Tile[width,height];for(int x=0;x<width;x++)for(int y=0;y<height;y++){var t=new Tile();Main.tile[x,y]=t;if(y>=floor){t.active(true);t.type=1;}}
            var p=Main.LocalPlayer;p.position=new Vector2(center*16,floor*16-p.height);p.immune=true;p.immuneTime=1000000;
            for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
            Main.CurrentFrameFlags.ActivePlayersCount=1;
            int difficulty=Tier(type);Main.hardMode=difficulty>1;NPC.downedMechBossAny=difficulty>1;NPC.downedGolemBoss=difficulty==3;
            if(!(bool)typeof(Terraria.ObjectData.TileObjectData).GetField("readOnlyData",Flags).GetValue(null))Terraria.ObjectData.TileObjectData.Initialize();
            // The original placement API establishes frames, origin and floor.
            WorldGen.PlaceObject(center,floor-1,TileID.ElderCrystalStand);
            Require(Main.tile[center,floor-1].type==TileID.ElderCrystalStand && !DD2Event.WouldFailSpawningHere(center,floor-1),"Legal DD2 stand and clear gate lanes.");
            DD2Event.SummonCrystal(center,floor-1,0);Require(DD2Event.Ongoing && DD2Event.OngoingDifficulty==difficulty,"Native event difficulty.");
            Advance();Require(Main.npc.Count(n=>n.active && n.type==549)==2,"Crystal AI creates both real gates.");
            scene="NativeDD2-T"+difficulty;
            if(type==548 || type==549)return Find(type);
            bool skeleton=type==566 || type==567;int mageType=difficulty==3?565:564;
            int heldMage=-1;
            for(int tick=0;tick<30000;tick++)
            {
                if(beforeBetsy && NPC.waveNumber==7 && DD2Event.TimeLeftBetweenWaves==2)return NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),center*16+100,800,48,Start:1);
                if(DD2Event.TimeLeftBetweenWaves>60)DD2Event.RequestToSkipWaitTime(center,floor-1);
                Advance();
                int selected=Array.FindIndex(Main.npc,n=>n.active && n.type==type);
                if(selected>=0 && NativeCombatLegalCoverageChecks.Receives(host,Main.npc[selected])){scene+="-wave"+NPC.waveNumber+(skeleton?"-original-mage-revival":"-gate-or-time-birth");return selected;}
                if(!DD2Event.Ongoing)throw new InvalidOperationException("DD2 ended before requested birth "+type);
                if(DD2Event.EnemySpawningIsOnHold)continue;
                if(skeleton && heldMage<0)heldMage=Array.FindIndex(Main.npc,n=>n.active && n.type==mageType);
                // SpawnMonsterFromGate is the same source-conditioned entry
                // called by gate AI. Its own wave/progress/random selection
                // decides the kind; passing a desired type is impossible.
                foreach(var gate in Main.npc.Where(n=>n.active && n.type==549).ToArray())DD2Event.SpawnMonsterFromGate(gate.Bottom,gate.Center.X<center*16);
                foreach(var n in Main.npc.Where(n=>n.active && n.type>=551 && n.type<=578).ToArray())
                {
                    if(n.type==type || n.whoAmI==heldMage)continue;
                    if(skeleton && n.type==mageType){heldMage=n.whoAmI;continue;}
                    if(NativeCombatLegalCoverageChecks.Receives(host,n))NativeCombatLegalCoverageChecks.Hit(host,n.whoAmI,1000000);
                }
            }
            throw new InvalidOperationException("Bounded original DD2 progression did not produce "+type+" wave="+NPC.waveNumber+" kills="+NPC.waveKills+" mage="+heldMage);
        }
        private static int Tier(int type)
        {
            if(new[]{553,556,559,562,568,570,572,574,576}.Contains(type))return 2;
            if(new[]{551,554,557,560,563,565,567,569,571,573,575,577,578}.Contains(type))return 3;
            return 1;
        }
        internal static void Boundary(Assembly host,Process child,string output)
        {
            string scene;int slot=Birth(551,host,out scene,beforeBetsy:true);Require(!Main.npc.Any(n=>n.active && n.type==551),"Boundary begins before actual Betsy birth.");
            typeof(Main).GetField("_gameUpdateCount",Flags).SetValue(null,1000U);
            Func<int,NativeCombatWorkerChecks.FrozenScene> capture=steps=>NativeCombatWorkerChecks.AcquireFrozen(host,child,Enumerable.Range(0,Main.maxNPCs).Where(i=>Main.npc[i].active).ToArray(),Enumerable.Range(0,Main.maxProjectiles).Where(i=>Main.projectile[i].active).ToArray(),slot,horizon:steps);
            var one=capture(1);Require(BitConverter.ToInt32(one.Future,16)==2,"One update before the boundary is supported.");
            bool refused=false;try{capture(2);}catch(InvalidOperationException e){refused=e.Message.Contains("Betsy");}Require(refused,"Crossing the unobserved time/weather RNG boundary refuses explicitly.");
            Require(DD2Event.TimeLeftBetweenWaves==2 && !Main.npc.Any(n=>n.active && n.type==551),"Private refusal cannot mutate the original event.");
            Advance();Advance();Require(Main.npc.Any(n=>n.active && n.type==551),"Actual original event creates Betsy after the boundary.");
            slot=Find(551);for(int i=0;i<500 && !NativeCombatLegalCoverageChecks.Receives(host,Main.npc[slot]);i++)Advance();
            typeof(Main).GetField("_gameUpdateCount",Flags).SetValue(null,1000U);var after=capture(120);
            NativeCombatWorkerChecks.Compare(after.Future,slot,output,"dd2-after-boundary",dependencyComparison:NativeCombatLegalCoverageChecks.CompareMotion,motionTolerance:.002f);
            Console.WriteLine("PASS actual DD2 last-wave wait2: one-step success / two-step explicit refusal / unchanged source / actual birth fresh120 recovery");
        }
        private static int Find(int type){return Array.FindIndex(Main.npc,n=>n.active && n.type==type);}
        private static void Advance()
        {
            NativeCombatLegalCoverageChecks.AdvanceScene(1);Main.time+=Main.dayRate;
            // This is an event-source fixture, not a claim that the complete
            // original Main.UpdateTime weather RNG phase has been replayed.
            DD2Event.UpdateTime();
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
