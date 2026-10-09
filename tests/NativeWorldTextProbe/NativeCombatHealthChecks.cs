using System;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using JueMingR.Features.Combat;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatHealthChecks
    {
        internal static void Run(NPC npc,Func<NPC,NpcMotionState> capture,PredictionEnvironment env,IPredictionTerrain terrain)
        {
            foreach(int buff in new[]{20,24,39,70,44,323,324,153,31})
            foreach(int duration in new[]{1,10,120})
            {
                npc.SetDefaults(2);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,400);npc.timeLeft=750;npc.life=npc.lifeMax=1000;
                Array.Clear(npc.buffType,0,npc.buffType.Length);Array.Clear(npc.buffTime,0,npc.buffTime.Length);
                npc.buffType[0]=buff;npc.buffTime[0]=duration;
                NativeCombatPredictionChecks.Compare(npc,capture,env,terrain,120,"health buff="+buff+" duration="+duration,.12f);
            }
            Array.Clear(npc.buffType,0,npc.buffType.Length);Array.Clear(npc.buffTime,0,npc.buffTime.Length);npc.confused=false;
            foreach(bool reverse in new[]{false,true})foreach(bool wetBuff in new[]{false,true})foreach(int gap in new[]{0,1})foreach(int duration in new[]{1,10})
            {
                npc.SetDefaults(2);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,400);npc.timeLeft=750;npc.life=npc.lifeMax=1000;
                Array.Clear(npc.buffType,0,npc.buffType.Length);Array.Clear(npc.buffTime,0,npc.buffTime.Length);
                npc.buffType[0]=reverse?323:24;npc.buffTime[0]=duration;npc.buffType[1+gap]=reverse?24:323;npc.buffTime[1+gap]=10;
                if(gap==1){npc.buffType[1]=20;npc.buffTime[1]=2;}
                if(wetBuff){npc.buffType[3]=103;npc.buffTime[3]=3;}
                else for(int x=20;x<40;x++)for(int y=20;y<40;y++)Main.tile[x,y].liquid=255;
                try{NativeCombatPredictionChecks.Compare(npc,capture,env,terrain,120,"double fire reverse="+reverse+" wetBuff="+wetBuff+" gap="+gap+" duration="+duration,.12f);}
                finally{for(int x=20;x<40;x++)for(int y=20;y<40;y++)Main.tile[x,y].liquid=0;}
            }
            bool dedicated=Main.dedServ;int netMode=Main.netMode;
            // Headless content samples do not run Main's debuff registration. These exact IDs
            // are registered by 1.4.5.8 Main.Initialize; restore the shared fixture after testing
            // unmodified NPC.AddBuff's full-slot rejection, eviction and duration extension.
            int[] saturationBuffs={20,21,22,23,25,28,30,31,32,33,34,35,36,37,38,39,44,46,47,67};
            bool[] previousDebuffs=(bool[])Main.debuff.Clone();
            try
            {
                foreach(int id in saturationBuffs)Main.debuff[id]=true;Main.debuff[24]=Main.debuff[323]=true;Main.debuff[1]=false;
                foreach(int scenario in new[]{0,1,2})
                {
                    npc.SetDefaults(2);npc.buffImmune[24]=false;
                    for(int slot=0;slot<saturationBuffs.Length;slot++){npc.buffType[slot]=saturationBuffs[slot];npc.buffTime[slot]=100;}
                    if(scenario==1)npc.buffType[0]=1;if(scenario==2)npc.buffType[0]=24;
                    var health=capture(npc).Health;JueMingR.Features.Combat.NpcHealth.ApplyFire(ref health,420);npc.AddBuff(24,420);
                    var actual=capture(npc).Health;Require(health.Fire==actual.Fire && health.Buffs.Equals(actual.Buffs),"pure lava AddBuff matches native full-slot reject/evict/extend scenario="+scenario);
                }
                Console.WriteLine("ORACLE full 20 buff slots: native AddBuff rejects all-debuff, evicts non-debuff and extends existing fire.");
            }
            finally{Array.Copy(previousDebuffs,Main.debuff,previousDebuffs.Length);}
            Array.Clear(npc.buffType,0,npc.buffType.Length);Array.Clear(npc.buffTime,0,npc.buffTime.Length);
            try
            {
                Main.dedServ=true;
                for(int i=0;i<Main.gore.Length;i++)if(Main.gore[i]==null)Main.gore[i]=new Gore();
                if(Main.ItemDropSolver==null){var drops=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();drops.Populate();Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(drops);}
                foreach(bool client in new[]{false,true})
                {
                    Main.netMode=client?1:0;env.Multiplayer=client;npc.SetDefaults(2);npc.active=true;npc.whoAmI=0;npc.target=0;npc.position=new Vector2(400,400);npc.life=1;npc.lifeRegenCount=-119;npc.buffType[0]=20;npc.buffTime[0]=1;
                    var model=capture(npc);var group=new[]{model};terrain.Reset();PredictionStop stop;bool future=NpcMotion.Step(ref model,group,1,env,terrain,1,out stop);
                    npc.UpdateNPC(0);Require(client?future && model.Life==1 && npc.active && npc.life==1:!future && stop==PredictionStop.Despawn && !npc.active,"natural lethal DOT distinguishes local death from client waiting for authority");
                }
                Console.WriteLine("ORACLE lethal DOT: local actual death and multiplayer local life=1 wait.");
            }
            finally{Main.dedServ=dedicated;Main.netMode=netMode;npc.active=false;}
        }
    }
}
