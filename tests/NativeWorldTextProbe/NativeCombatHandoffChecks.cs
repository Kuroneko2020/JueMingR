using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Features.QuickItems;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatHandoffChecks
    {
        private static readonly List<int> uses=new List<int>();
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var tools=Get(context,"Tools");var processing=Get(context,"Processing");var input=Get(context,"Input");
            NativeQuickItemChecks.Until(()=>{Call(processing,"Poll");return (bool)Call(processing,"Controls",1);});
            var audit=new Harmony("JueMingR.Tests.CombatHandoff");audit.Patch(AccessTools.Method(typeof(Player),"ItemCheck_StartActualUse"),postfix:new HarmonyMethod(typeof(NativeCombatHandoffChecks),nameof(Started)));
            try
            {
                foreach(int empty in new[]{0,3})foreach(int weapon in new[]{ItemID.CopperShortsword,98,198})
                {
                    NativeCombatCadenceChecks.Save(combat,new CombatOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,weapon,0,0);
                    if(weapon==198)p.inventory[1].SetDefaults(671);
                    p.inventory[54].SetDefaults(ItemID.MusketBall);p.inventory[54].stack=999;
                    p.inventory[12].SetDefaults(ItemID.StaffofRegrowth);p.inventory[10].SetDefaults(ItemID.SiltBlock);p.inventory[10].stack=20;
                    NativeToolsChecks.Tile(38,42,1);Require(WorldGen.PlaceTile(38,41,78,mute:true,forced:true,plr:0),"combat mixed native pot");NativeToolsChecks.Tile(38,40,84);
                    NativeExtractionChecks.Machine(43,40,219);uses.Clear();NativeToolsChecks.SetMode(tools,1,1);
                    Call(processing,"Set",1,true);NativeQuickItemChecks.Until(()=>{Call(processing,"Poll");return (bool)Call(processing,"Value",1);});
                    NativeCombatCadenceChecks.Save(combat,new CombatOptions(weapon==198?4:1));
                    for(int step=0;step<700;step++)NativeCombatCadenceChecks.Step(context,weapon!=198,weapon==198,empty);
                    Require(uses.Count(x=>x==weapon)>=3,"combat continues while lending real uses: weapon="+weapon+" empty="+empty+" uses="+string.Join(",",uses));
                    Require(uses.Contains(ItemID.StaffofRegrowth) && Main.tile[38,40].active() && Main.tile[38,40].type==82,"held combat yields a complete native harvest: weapon="+weapon+" empty="+empty+" uses="+string.Join(",",uses));
                    Require(p.inventory[10].IsAir || p.inventory[10].stack<20,"held combat yields actual extraction without starving it: weapon="+weapon+" empty="+empty+" selected="+p.selectedItem+" handoff="+Get(Get(combat,"Handoff"),"Waiting"));
                    NativeToolsChecks.SetMode(tools,1,0);Call(processing,"Set",1,false);NativeQuickItemChecks.Until(()=>{Call(processing,"Poll");return (bool)Call(processing,"Controls",1);});
                    for(int i=0;i<90;i++)NativeCombatCadenceChecks.Step(context,false,false,0);
                }
                Quick(context,combat,tools,input,false,false);
                Quick(context,combat,tools,input,true,false);
                Quick(context,combat,tools,input,true,true);
                Console.WriteLine("PASS G11A held left/right combat, native autoReuse weapon, actual harvest/extraction fairness and single quick-item pulse with 0/3 unsampled outer updates.");
            }
            finally
            {
                NativeToolsChecks.SetMode(tools,1,0);if((bool)Call(processing,"Controls",1))Call(processing,"Set",1,false);
                NativeCombatCadenceChecks.Save(combat,new CombatOptions());foreach(var m in audit.GetPatchedMethods().ToArray())audit.Unpatch(m,HarmonyPatchType.All,audit.Id);
            }
        }
        private static void Quick(object context,object combat,object tools,object input,bool right,bool interact)
        {
            NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            int weapon=right?198:ItemID.CopperShortsword;
            var p=NativeToolExecutionChecks.Reset(context,tools,input,weapon,0,0);p.inventory[1].SetDefaults(671);p.inventory[17].SetDefaults(98);p.inventory[54].SetDefaults(ItemID.MusketBall);p.inventory[54].stack=100;
            NativeCombatCadenceChecks.Step(context,false,false,0);if(interact)NativeCombatInteractionChecks.Chest();
            var quick=Get(context,"QuickItems");var settings=(QuickItemSettings)Get(quick,"Settings");
            var entry=new QuickItemEntry("11001100110011001100110011001100",98,QuickItemMode.Use,false,true);string reason;
            Require(settings.TryChange(new QuickItemDocument(false,true,new[]{entry}),entry.Id,out reason),"combat quick entry "+reason);NativeQuickItemChecks.Until(()=>{Call(quick,"Poll");return !settings.Busy;});
            var bindings=(HotkeyBindings)Get(Get(Get(context,"Shell"),"hotkeys"),"Bindings");NativeQuickGestureChecks.Bind(bindings,entry.ActionId,"J");
            NativeCombatCadenceChecks.Save(combat,new CombatOptions(right?4:1));uses.Clear();bool pressed=false;
            for(int i=0;i<240;i++)
            {
                bool press=!pressed && (bool)Get(Get(combat,"Use"),"CanYield");if(press)pressed=true;
                if(press && interact)p.releaseUseTile=true;
                NativeCombatCadenceChecks.Step(context,!right,right,1,press?new[]{Keys.J}:null,press && interact?(Microsoft.Xna.Framework.Vector2?)new Microsoft.Xna.Framework.Vector2(696,664):null);
            }
            Require(pressed && uses.Count(t=>t==98)==(interact?0:1) && (interact?p.chest==0 && p.inventory[54].stack==100:uses.Count(t=>t==weapon)>3) && !(bool)Get(Get(quick,"Use"),"Active"),
                "fresh quick request honors native interaction/one pulse and original return: right="+right+" interaction="+interact+" uses="+string.Join(",",uses));
            NativeCombatCadenceChecks.Step(context,false,false,0);NativeCombatCadenceChecks.Save(combat,new CombatOptions());p.chest=-1;Main.playerInventory=false;Main.chest[0]=null;
        }
        private static void Started(Player __instance,Item __0){if(__instance.whoAmI==Main.myPlayer)uses.Add(__0.type);}
    }
}
