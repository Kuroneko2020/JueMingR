using System;
using System.Reflection;
using System.Linq;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Actual Player/ItemCheck, NPC and projectile updates keep both the idle
    // observation and first controller birth on their natural outer schedule.
    internal static class NativeCombatControlPreviewChecks
    {
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var input=Get(context,"Input");var attack=Get(combat,"Attack");
            var failures=new System.Collections.Generic.List<string>();
            foreach(int type in new[]{3475,2797,3540,3854,3930,113,218,495,3030,3278,3541})
            {
                try
                {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,type,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.releaseUseItem=true;p.statMana=p.statManaMax=p.statManaMax2=200;
                p.inventory[54].SetDefaults(p.HeldItem.useAmmo==40?1334:p.HeldItem.useAmmo==771?771:3104);p.inventory[54].stack=999;Main.screenPosition=new Vector2(600,400);
                var n=Dummy(2,type==3278?51:52,40);
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,false));
                for(int i=0;i<3;i++)
                {
                    NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),false,true);
                    Require(GetOptional(attack,"ExpectedPreview")!=null,"idle native controlled-family preview appears before any attack: "+type);
                    Require(!Main.projectile.Any(q=>q.active) && !p.controlUseItem && !p.channel,"controlled preview cannot create its own live source: "+type);
                }
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,true));
                NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),true);
                Require(Main.projectile.Any(q=>q.active && q.owner==0),"first actual click creates original source without needing an earlier hit: "+type);
                var registry=(System.Collections.IDictionary)Get(Get(attack,"Control"),"shots");Require(registry.Count>0,"first natural source is registered for later legal consumers: "+type);
                int visible=GetOptional(attack,"ExpectedPreview")!=null?1:0;
                for(int i=0;i<8;i++){NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),true);if(GetOptional(attack,"ExpectedPreview")!=null)visible++;}
                Require(visible>0,"current live controller stage retains useful preview through natural updates: "+type);
                Console.WriteLine("PASS natural controlled preview/first birth item="+type+" visible="+visible+"/9 sources="+registry.Count);
                }
                catch(Exception error){failures.Add(type+": "+error.Message);Console.WriteLine("FAIL natural controlled preview/first birth item="+type+": "+error);}
            }
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
            Require(failures.Count==0,"natural controller family failures: "+string.Join(" | ",failures));
            NativeCombatCelebrationChecks.Run(context);
        }
        internal static NPC Dummy(int slot,int x,int y)
        {
            // Native AI92 retires a dummy without its real placed tile. Keep
            // the full 2x3 object and vanilla spawn coordinates; do not disable
            // that AI to make an invalid target survive the outer update.
            for(int dx=0;dx<2;dx++)for(int dy=0;dy<3;dy++)
            {NativeToolsChecks.Tile(x+dx,y+dy,378);Main.tile[x+dx,y+dy].frameX=(short)(dx*18);Main.tile[x+dx,y+dy].frameY=(short)(dy*18);}
            // SetDefaults does not clear a previous occupant's velocity.
            // Use vanilla's fresh-instance slot boundary, as NewNPC does,
            // or an ostensibly placed dummy drifts on an old zombie vector.
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,new object[]{slot,(byte)0});
            var n=Main.npc[slot];n.SetDefaults(488);n.whoAmI=slot;n.active=true;n.position=new Vector2(x*16+16-n.width/2,y*16+48-n.height);n.ai[0]=x;n.ai[1]=y;n.target=0;Array.Clear(n.immune,0,n.immune.Length);return n;
        }
    }
}
