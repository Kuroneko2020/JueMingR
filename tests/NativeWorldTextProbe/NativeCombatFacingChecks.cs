using System;
using System.Reflection;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatFacingChecks
    {
        internal static void Run(object context)
        {
            object combat=Get(context,"Combat"),facing=GetOptional(combat,"Facing"),input=Get(context,"Input");
            Require(facing!=null,"independent facing owner is absent");
            var settings=(CombatSettings)Get(combat,"Settings");
            NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return settings.Loaded;});
            NativeCombatCadenceChecks.Save(combat,new CombatOptions(32));
            try
            {
                var p=Main.LocalPlayer;p.inventory[0].SetDefaults(ItemID.CopperShortsword);p.selectedItemState.Select(0);p.selectedItemState.Update();p.itemAnimation=10;p.itemTime=5;p.position=new Vector2(640,600);
                NativeToolExecutionChecks.Sample(context,input,p.Center+new Vector2(200,0),true);Call(combat,"Sample");NativeQuickItemChecks.BeginWorldStep();
                foreach(var npc in Main.npc)npc.active=false;
                var target=Main.npc[0];target.SetDefaults(NPCID.BlueSlime);target.active=true;target.whoAmI=0;target.position=p.Center-new Vector2(100,10);
                p.direction=1;p.controlLeft=false;p.controlRight=false;Call(facing,"Apply",p);
                Require(p.direction==-1,"facing independently finds hostile NPC instead of rightward cursor");
                var property=facing.GetType().GetProperty("TargetProvider",BindingFlags.Instance|BindingFlags.NonPublic);
                var provided=Main.npc[1];provided.SetDefaults(NPCID.BlueSlime);provided.active=true;provided.whoAmI=1;provided.position=p.Center+new Vector2(150,0);
                object prepared=null;property.SetValue(facing,NativeCombatBoundaryChecks.Provider(property.PropertyType,r=>prepared));
                NativeQuickItemChecks.BeginWorldStep();Type stamp=facing.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.FacingTarget");
                prepared=Activator.CreateInstance(stamp,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{provided,(long)Get(Get(combat,"Runtime"),"Generation")},null);
                int searches=Counter(facing,"Searches");Call(facing,"Apply",p);
                Require(p.direction==1 && Counter(facing,"Searches")==searches,"fresh provided target supersedes fallback inside cooldown without another scan");
                prepared=null;NativeQuickItemChecks.BeginWorldStep();Call(facing,"Apply",p);Require(p.direction==-1,"provider revocation promptly resumes independent target");
                property.SetValue(facing,null);provided.active=false;
                p.controlRight=true;Call(facing,"Apply",p);Require(p.direction==1,"actual manual right overrides enemy without waiting for automatic cooldown");
                searches=Counter(facing,"Searches");for(int i=0;i<30;i++){NativeQuickItemChecks.BeginWorldStep();Call(facing,"Apply",p);}Require(Counter(facing,"Searches")==searches,"manual direction does not perform useless target search");
                p.controlRight=false;target.active=false;NativeQuickItemChecks.BeginWorldStep();Call(facing,"Apply",p);
                Require(p.direction==1,"removed target cannot hold stale left direction");
                target.SetDefaults(NPCID.GoblinTinkerer);target.active=true;target.position=p.Center-new Vector2(100,10);p.direction=-1;NativeQuickItemChecks.BeginWorldStep();Call(facing,"Reset");Call(facing,"Apply",p);
                Require(p.direction==1,"friendly tinkerer never enters ordinary target selection");
                int reads=Counter(facing,"CandidateReads");NativeToolExecutionChecks.Sample(context,input,p.Center+new Vector2(200,0),false);Call(combat,"Sample");p.itemAnimation=p.itemTime=0;
                for(int i=0;i<100;i++){NativeQuickItemChecks.BeginWorldStep();Call(facing,"Apply",p);}Require(Counter(facing,"CandidateReads")==reads,"enabled without use intent does not read NPC candidates");
                p.inventory[0].SetDefaults(ItemID.CopperPickaxe);NativeToolExecutionChecks.Sample(context,input,p.Center+new Vector2(200,0),true);Call(combat,"Sample");
                for(int i=0;i<30;i++){NativeQuickItemChecks.BeginWorldStep();Call(facing,"Apply",p);}Require(Counter(facing,"CandidateReads")==reads,"ineligible tools do not search even with held input");
                NativeCombatCadenceChecks.Save(combat,new CombatOptions());p.direction=-1;Call(facing,"Apply",p);Require(p.direction==-1,"off releases coverage without restoring an old direction");
                Console.WriteLine("PASS G11A independent facing, native manual controls, stale target and friendly exclusion.");
            }
            finally{NativeCombatCadenceChecks.Save(combat,new CombatOptions());}
        }
        private static int Counter(object value,string name){return (int)(GetOptional(value,name)??0);}
    }
}
