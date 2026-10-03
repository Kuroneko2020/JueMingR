using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using JueMingR.Features.Combat;
using Terraria;

namespace NativeWorldTextProbe
{
    // Legal births go directly into the real Session. No per-type warm-up,
    // restart or patched future: frozen catalogue evidence is a separate layer.
    internal static class NativeCombatLiveCoverageChecks
    {
        internal static void Run(object context,object native,NpcPredictionCache cache,Action step)
        {
            var player=Main.LocalPlayer;bool hard=Main.hardMode;int failed=0;
            try
            {
                Main.hardMode=true;
                foreach(int type in new[]{1,61,181,239,236,42,176,175})
                {
                    NativeCombatLiveContextChecks.FlightWorld();foreach(var n in Main.npc)n.active=false;foreach(var p in Main.projectile)p.active=false;
                    int owner=Main.myPlayer;try{Main.myPlayer=1;player.mount.Dismount(player);}finally{Main.myPlayer=owner;}
                    player.controlLeft=player.controlRight=player.controlUp=player.controlDown=player.controlJump=false;
                    player.position=new Vector2(1100,2400-player.height);player.velocity=Vector2.Zero;player.dead=false;player.wet=player.honeyWet=player.lavaWet=player.shimmerWet=false;player.breath=200;
                    player.fallStart=player.fallStart2=(int)(player.position.Y/16);player.statLife=player.statLifeMax=player.statLifeMax2=400;
                    for(int i=0;i<player.hurtCooldowns.Length;i++)player.hurtCooldowns[i]=100000;
                    if(type==239 || type==236)for(int x=40;x<100;x++)for(int y=125;y<150;y++)Main.tile[x,y].wall=1;
                    if(type==181){Main.tile[72,149].active(true);Main.tile[72,149].type=1;}
                    if(type==175){Main.tile[75,149].active(true);Main.tile[75,149].type=60;}
                    int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1160,type==42 || type==176?2200:2400,type,Start:1,ai0:type==175?75:0,ai1:type==175?149:0,Target:Main.myPlayer);
                    int shown=0,stableBlanks=0,gaps=0,first=-1,forms=0,lastType=type,sinceForm=0,shots=0;bool prior=false;var reasons=new Dictionary<string,int>();
                    for(int frame=0;frame<240;frame++)
                    {
                        step();Require(!player.dead,"Independent production fixture remains alive.");var n=Main.npc[slot];
                        if(n.type!=lastType){forms++;sinceForm=0;lastType=n.type;}else sinceForm++;
                        var path=cache.Read(0);bool present=path!=null;
                        if(present){shown++;if(first<0)first=frame;Require(path.Identity.Slot==slot && ReferenceEquals(path.Identity.Token,n) && path.Identity.Type==n.type && path.Identity.NetId==n.netID && path.SampleTick==Main.GameUpdateCount && path.Count==121,"Published path keeps current real identity and complete future.");}
                        else if(frame>=30 && sinceForm>=30)stableBlanks++;
                        if(prior && !present)gaps++;prior=present;
                        foreach(var p in Main.projectile)if(p.active && p.type==55){shots++;break;}
                        string reason=(string)Get(native,"Reason")??"none";int count;reasons.TryGetValue(reason,out count);reasons[reason]=count+1;
                    }
                    Console.WriteLine("LIVE-FAMILY type="+type+" first-update="+first+" shown="+shown+" stable-blanks="+stableBlanks+" gaps="+gaps+" forms="+forms+" final-type="+Main.npc[slot].type+" native-stinger-updates="+shots);
                    foreach(var pair in reasons)Console.WriteLine("FAMILY-REASON frames="+pair.Value+" "+pair.Key);
                    if(stableBlanks!=0 || first<0)failed++;
                    if(type==239 || type==236)Require(forms>0,"Spider scenario actually enters its native wall form.");
                }
            }
            finally{Main.hardMode=hard;}
            Require(failed==0,"Shared production acceptance failed ordinary mechanism windows="+failed);
        }
        private static object Get(object owner,string name)
        {const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;var field=owner.GetType().GetField(name,flags);return field!=null?field.GetValue(owner):owner.GetType().GetProperty(name,flags).GetValue(owner);}
        private static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    }
}
