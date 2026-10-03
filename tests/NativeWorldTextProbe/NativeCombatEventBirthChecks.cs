using System;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Events;
using Terraria.ID;

namespace NativeWorldTextProbe
{
    // Source-conditioned event births. These fixtures exercise native actor
    // transitions, not inventory use, complete fishing, or a saved user world.
    internal static class NativeCombatEventBirthChecks
    {
        internal static bool Handles(int type){return new[]{370,371,372,373,379,422,437,438,439,440,493,507,517,519,522,523,666}.Contains(type);}
        internal static int Birth(int type,Assembly host,out string scene,bool expert=false,bool good=false)
        {
            NativeCombatWorkerChecks.Scene(false);NPC.ClearAll();Projectile.ClearAll();DD2Event.ResetProgressEntirely();
            Main.pumpkinMoon=Main.snowMoon=false;Main.hardMode=true;Main.worldSurface=160;Main.rockLayer=250;
            Main.maxTilesX=1000;Main.maxTilesY=400;Main.rightWorld=16000;Main.bottomWorld=6400;Main.tile=new Tile[1000,400];
            for(int x=0;x<1000;x++)for(int y=0;y<400;y++){var t=new Tile();Main.tile[x,y]=t;if(y>=145){t.active(true);t.type=1;}}
            var p=Main.LocalPlayer;p.position=new Vector2(800,2320-p.height);p.immune=true;p.immuneTime=1000000;
            for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
            if(type>=370 && type<=373){Main.GameMode=expert?1:0;return Fishron(type,host,out scene);}
            if(new[]{422,493,507,517,519}.Contains(type))return Pillar(type,host,out scene);
            if(type==666)
            {
                Main.GameMode=good?0:1;Main.getGoodWorld=good;p.position=new Vector2(3000,2320-p.height);
                for(int x=160;x<220;x++)for(int y=145;y<175;y++)Main.tile[x,y].type=23;
                Main.PlayerSceneMetrics.Reset();p.UpdateSceneMetrics();p.UpdateBiomes();Require(p.ZoneCorrupt,"Original corrupted biome for expert EoW.");
                NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),2900,2290,13,Start:1);Step(3);
                if(good)NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),3050,2200,6,Start:1);
                // The expert EoW's actual attack gives ai1=1. Its head/body
                // remain on the approved trend route; the spit does not.
                int selected;
                if(good){Until(()=>Slots(666).Any(i=>Main.npc[i].ai[1]==0 && NativeCombatLegalCoverageChecks.Receives(host,Main.npc[i])),5000,"Actual good-world Eater spit");selected=Slots(666).First(i=>Main.npc[i].ai[1]==0 && NativeCombatLegalCoverageChecks.Receives(host,Main.npc[i]));}
                else selected=Wait(type,host,5000);
                Require(Main.npc[selected].ai[1]==(good?0:1),"Original vile spit variant parameter.");scene=good?"OriginalGoodWorldEaterAttack":"OriginalExpertEowAttack";return selected;
            }
            NPC.downedBoss3=NPC.downedGolemBoss=true;CultistRitual.delay=0;Main.GameMode=type==523?1:0;
            Require(CultistRitual.TrySpawning(500,145),"Unforced ritual checks actual floor and offscreen location.");
            p.position=new Vector2(8150,2320-p.height);Step(3);
            Require(Slots(379).Length==2 && Slots(438).Length==2,"Tablet AI creates four actual worshippers.");
            scene="OriginalCultistRitual";
            if(type==437 || type==379 || type==438)return Find(type);
            foreach(int slot in Slots(379).Concat(Slots(438)))NativeCombatLegalCoverageChecks.Hit(host,slot,1000000);
            int boss=Wait(439,host,1000);scene+="-worshippers-defeated";
            if(type==439)return boss;
            if(type==522 || type==523)
            {
                while(Main.npc[boss].life>Main.npc[boss].lifeMax/2)NativeCombatLegalCoverageChecks.Hit(host,boss,Main.npc[boss].lifeMax/10);
                scene+="-half-life";
            }
            return Wait(type,host,15000);
        }
        private static int Fishron(int type,Assembly host,out string scene)
        {
            var p=Main.LocalPlayer;p.position=new Vector2(3500,2320-p.height);
            for(int x=100;x<200;x++)for(int y=100;y<145;y++)Main.tile[x,y].liquid=255;
            int bobber=Projectile.NewProjectile(new EntitySource_DebugCommand(),new Vector2(2500,1900),Vector2.Zero,ProjectileID.BobberWooden,0,0,0);
            Require(Main.projectile[bobber].bobber,"Real native fishing bobber identity.");NPC.SpawnOnPlayer(0,370);
            int boss=Wait(370,host,500);Main.projectile[bobber].Kill();scene="OriginalFishronFishingSpawnEntry";
            if(type==370)return boss;
            if(type==373){while(Main.npc[boss].life>Main.npc[boss].lifeMax/2)NativeCombatLegalCoverageChecks.Hit(host,boss,Main.npc[boss].lifeMax/10);scene+="-half-life";}
            return Wait(type,host,15000);
        }
        internal static void VerifyVariants(Assembly host,Process child,string output)
        {
            string ignored;int slot;var names=(Environment.GetEnvironmentVariable("JUEMINGR_NPC_VARIANTS")??"redhat,goodspit,eye,twins,queen,empress,fishron,dd2boundary").Split(',');
            foreach(string name in names)
            {
                if(name=="dd2boundary"){NativeCombatDd2CoverageChecks.Boundary(host,child,output);continue;}
                if(name=="goodspit"){slot=Birth(666,host,out ignored,good:true);Freeze(host,child,output,name,slot);continue;}
                if(name=="redhat")
                {
                    NativeCombatLegalCoverageChecks.SpawnContext(35,host,out ignored);NPC.ClearAll();Projectile.ClearAll();Main.LocalPlayer.armor[3].SetDefaults(1307);Main.LocalPlayer.UpdateEquips(0);
                    Require(Main.LocalPlayer.killClothier,"Real equipped voodoo doll enables the source entry.");NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),780,1040,54,Start:1);NPC.RedHatSkeletron(0);Step(2);
                    Require(Slots(35).Any(i=>Main.npc[i].ai[3]==1) && Slots(36).All(i=>Main.npc[i].localAI[3]==1),"Red hat source creates marked head and hands.");
                    slot=Wait(33,host,6000);Require(Main.npc[slot].ai[3]==1 && Slots(32).Any(i=>Main.npc[i].localAI[3]==1),"Original red hat magician creates the marked WaterSphere.");Freeze(host,child,output,name,slot);continue;
                }
                if(name=="twins")
                {
                    NativeCombatLegalCoverageChecks.SpawnContext(125,host,out ignored);NPC.ClearAll();Projectile.ClearAll();NPC.SpawnOnPlayer(0,125);NPC.SpawnOnPlayer(0,126);Step(3);
                    foreach(int target in Slots(125).Concat(Slots(126)))Below(host,target,.39f);
                    slot=Find(125);Until(()=>Main.npc[slot].ai[0]==1 && Main.npc[slot].ai[1]>=80,2000,"Twins native first transformation");Freeze(host,child,output,name,slot);
                    Require(Slots(125).Concat(Slots(126)).All(i=>Main.npc[i].ai[0]==3),"Both real twins complete their transformation in the frozen window.");continue;
                }
                if(name=="fishron")
                {
                    slot=Birth(370,host,out ignored,expert:true);Below(host,slot,.49f);Until(()=>Main.npc[slot].ai[0]==0 && NativeCombatLegalCoverageChecks.Receives(host,Main.npc[slot]),2000,"Fishron half-life decision");Freeze(host,child,output,name+"-into-second",slot);
                    Until(()=>Main.npc[slot].ai[0]==5 && NativeCombatLegalCoverageChecks.Receives(host,Main.npc[slot]),2000,"Fishron second phase");Below(host,slot,.14f);Freeze(host,child,output,name+"-into-third",slot);
                    Until(()=>Main.npc[slot].ai[0]>=10 && NativeCombatLegalCoverageChecks.Receives(host,Main.npc[slot]),2000,"Fishron third phase");Freeze(host,child,output,name+"-third",slot);continue;
                }
                int type=name=="eye"?4:name=="queen"?657:name=="empress"?636:0;Require(type!=0,"Known bounded variant selection.");
                slot=NativeCombatLegalCoverageChecks.SpawnContext(type,host,out ignored);Below(host,slot,.49f);
                if(type==4)Until(()=>Main.npc[slot].ai[0]==1 && Main.npc[slot].ai[1]>=80,2000,"Eye native transformation");
                if(type==636)Until(()=>Main.npc[slot].ai[0]==10 && Main.npc[slot].ai[1]>=20 && NativeCombatLegalCoverageChecks.Receives(host,Main.npc[slot]),3000,"Empress pre-invulnerability transformation");
                Freeze(host,child,output,name,slot);
                Require(type==4?Main.npc[slot].ai[0]==3:type==657?Main.npc[slot].noGravity && Main.npc[slot].noTileCollide:Main.npc[slot].ai[3]==1,"Original damage crosses the actual native phase threshold.");
            }
        }
        private static void Below(Assembly host,int slot,float fraction)
        {while(Main.npc[slot].life>=Main.npc[slot].lifeMax*fraction)NativeCombatLegalCoverageChecks.Hit(host,slot,Math.Max(1,Main.npc[slot].lifeMax/20));}
        private static void Until(Func<bool> condition,int limit,string reason)
        {for(int i=0;i<limit && !condition();i++)Step(1);Require(condition(),reason);}
        private static void Freeze(Assembly host,Process child,string output,string name,int slot)
        {
            Require(NativeCombatLegalCoverageChecks.Receives(host,Main.npc[slot]),"Variant capture must be a live eligible target: "+name);
            typeof(Main).GetField("_gameUpdateCount",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,1000U);
            var frozen=NativeCombatWorkerChecks.AcquireFrozen(host,child,Enumerable.Range(0,Main.maxNPCs).Where(i=>Main.npc[i].active).ToArray(),Enumerable.Range(0,Main.maxProjectiles).Where(i=>Main.projectile[i].active).ToArray(),slot,sparse:true);
            File.WriteAllBytes(Path.Combine(output,"variant-"+name+"-snapshot.bin"),frozen.Snapshot);File.WriteAllBytes(Path.Combine(output,"variant-"+name+"-future.bin"),frozen.Future);
            NativeCombatWorkerChecks.Compare(frozen.Future,slot,output,"variant-"+name,dependencyComparison:NativeCombatLegalCoverageChecks.CompareMotion,motionTolerance:.002f);
            Console.WriteLine("VARIANT "+name+" selected="+Main.npc[slot].type+" full-future=120");
        }
        private static int Pillar(int type,Assembly host,out string scene)
        {
            WorldGen.TriggerLunarApocalypse();Step(2);int parentType=type==519?517:type,slot=Find(parentType);NPC tower=Main.npc[slot];
            Main.LocalPlayer.position=new Vector2(tower.Center.X+200,2320-Main.LocalPlayer.height);
            Require(!NativeCombatLegalCoverageChecks.Receives(host,tower),"Actual initial pillar shield excludes selection.");
            scene="OriginalLunarEvent-"+parentType;
            if(type==519)return Wait(519,host,1200);
            int defender=parentType==517?418:parentType==422?425:parentType==507?424:411;
            for(int i=0;i<NPC.ShieldStrengthTowerMax+1 && !NativeCombatLegalCoverageChecks.Receives(host,tower);i++)
            {
                int enemy=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),(int)tower.Center.X+30,(int)tower.Bottom.Y,defender,Start:1);
                NativeCombatLegalCoverageChecks.Hit(host,enemy,1000000);Step(2);
            }
            for(int i=0;i<600 && !NativeCombatLegalCoverageChecks.Receives(host,tower);i++)Step(1);
            Require(NativeCombatLegalCoverageChecks.Receives(host,tower),"Real deaths and original soul projectile arrivals break the shield.");
            scene+="-actual-souls-broke-shield";return slot;
        }
        private static int Wait(int type,Assembly host,int limit)
        {
            for(int i=0;i<limit;i++){int[] slots=Slots(type);foreach(int slot in slots)if(NativeCombatLegalCoverageChecks.Receives(host,Main.npc[slot]))return slot;Step(1);}
            throw new InvalidOperationException("Original event never produced eligible "+type+" within "+limit+" updates.");
        }
        private static int[] Slots(int type){return Enumerable.Range(0,Main.maxNPCs).Where(i=>Main.npc[i].active && Main.npc[i].type==type).ToArray();}
        private static int Find(int type){return Slots(type).First();}
        private static void Step(int ticks){NativeCombatLegalCoverageChecks.AdvanceScene(ticks);}
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
