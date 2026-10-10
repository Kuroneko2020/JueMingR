using System;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatCadenceAimChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static object watchCombat;
        private static readonly Dictionary<string,int> points=new Dictionary<string,int>();
        private static readonly List<Tuple<int,int,Vector2,float>> born=new List<Tuple<int,int,Vector2,float>>();
        private static void Born(Player __instance,Projectile __0){if(__instance.whoAmI==0)born.Add(Tuple.Create(__0.type,(int)__0.key,__0.velocity,__0.ai[0]));}
        private static void Point(object __0,bool __result)
        {
            string stage=Get(__0,"Stage").ToString();int count;points.TryGetValue(stage,out count);if(__result)points[stage]=count+1;
            if(count==0)Console.WriteLine("CADENCE request stage="+stage+" allowed="+Call(Get(watchCombat,"Use"),"MatchesAimRequest",__0)+" result="+__result+" operation="+Get(__0,"Operation"));
        }
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var input=Get(context,"Input");
            watchCombat=combat;var audit=new Harmony("JueMingR.Tests.CadenceAim");audit.Patch(Get(combat,"Aim").GetType().GetMethod("TryPoint",Flags),postfix:new HarmonyMethod(typeof(NativeCombatCadenceAimChecks).GetMethod("Point",Flags)));
            audit.Patch(typeof(Player).GetMethod("TryUpdateChannel",Flags),postfix:new HarmonyMethod(typeof(NativeCombatCadenceAimChecks).GetMethod("Born",Flags)));
            try
            {
            NativeCombatCadenceChecks.Save(combat,new CombatOptions());NativeCombatObservationChecks.Save(host,new ObservationOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,5462,0,0);p.position=new Vector2(700,646);p.inventory[1].SetDefaults(6153);p.releaseUseItem=true;p.statMana=p.statManaMax=p.statManaMax2=1000;Main.screenPosition=new Vector2(600,400);
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(1040,646);n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=10000;Array.Clear(n.immune,0,n.immune.Length);
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatCadenceChecks.Save(combat,new CombatOptions(4));
            NativeCombatCadenceChecks.Step(context,false,false,0,point:new Vector2(650,550));
            NativeCombatCadenceChecks.Step(context,false,true,0,point:new Vector2(650,550));
            Projectile flint=null;foreach(var shot in Main.projectile)if(shot.active && shot.type==1040){flint=shot;break;}
            Console.WriteLine("CADENCE Flint actual charge source="+(flint==null?"none":((int)flint.key).ToString())+" channel="+p.channel+" direction="+(flint==null?0:flint.direction)+" selected="+Get(Get(host,"Selection"),"Target"));
            Require(flint!=null && flint.ai[0]==0 && flint.direction==1,"real G11A Flint charging reads prepared shared target to the right despite physical cursor to the left");
            Require(Main.mouseX==NativeCombatCadenceChecks.ManualMouseX && Main.mouseY==NativeCombatCadenceChecks.ManualMouseY,"nested G11A shared charge returns physical cursor");
            bool glacierReleased=false;Vector2 glacierHeld=Vector2.Zero;int glacierKey=0;
            for(int i=0;i<170 && !glacierReleased;i++)
            {
                foreach(var shot in Main.projectile)if(shot.active && shot.type==1115 && shot.ai[1]==0){glacierHeld=shot.velocity;glacierKey=(int)shot.key;}
                int prior=Count("GlacierCharge");NativeCombatCadenceChecks.Step(context,false,true,0,point:new Vector2(650,550));Cursor();
                foreach(var shot in Main.projectile)if(shot.active && shot.type==1115 && shot.ai[1]==1 && (int)shot.key==glacierKey)
                {
                    glacierReleased=true;Console.WriteLine("CADENCE Glacier native release key="+glacierKey+" charge="+shot.ai[0]+" held="+glacierHeld+" release="+shot.velocity+" newPoints="+(Count("GlacierCharge")-prior));
                    Require(glacierHeld.X>0 && shot.velocity.X>0 && Count("GlacierCharge")==prior,"Glacier uses charge-window direction and never borrows a new release cursor");
                }
            }
            Require(glacierReleased && born.Exists(b=>b.Item1==1043 && b.Item3.X>0) && Count("FlintCharge")>=2 && Count("GlacierCharge")>=2,"actual quick-switch pair charges/release uses both shared stages; Flint releases retained right direction");
            Console.WriteLine("PASS G11A charge stages: Flint right-bearing, retained release wave; Glacier real every-third-AI direction and retained full-charge release; nested cursor restored");
            OtherCadences(context,combat,host,input);
            NativeCombatCadenceChecks.Save(combat,new CombatOptions());NativeCombatObservationChecks.Save(host,new ObservationOptions());
            }
            finally{audit.UnpatchAll(audit.Id);watchCombat=null;NativeCombatCadenceChecks.Save(combat,new CombatOptions());NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
        private static int Count(string stage){int value;return points.TryGetValue(stage,out value)?value:0;}
        private static void Cursor(){Require(Main.mouseX==NativeCombatCadenceChecks.ManualMouseX && Main.mouseY==NativeCombatCadenceChecks.ManualMouseY,"G11A/attack nested scopes restore physical cursor after actual native AI");}
        private static void OtherCadences(object context,object combat,object host,object input)
        {
            foreach(int mode in new[]{0,1,2,3,4})
            {
                NativeCombatCadenceChecks.Save(combat,new CombatOptions());NativeCombatObservationChecks.Save(host,new ObservationOptions());points.Clear();born.Clear();
                int item=mode==0?95:mode==1?162:mode==2?198:mode==3?2269:3262;
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,item,0,0);p.position=new Vector2(700,646);p.releaseUseItem=true;foreach(var armor in p.armor)armor.TurnToAir();if(mode==4)p.armor[3].SetDefaults(Terraria.ID.ItemID.MagicString);
                if(mode==2)p.inventory[1].SetDefaults(671);if(mode==0 || mode==3){p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;}
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(mode==4?790:850,646);n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=100000;Array.Clear(n.immune,0,n.immune.Length);
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatCadenceChecks.Save(combat,new CombatOptions(1<<mode));
                NativeCombatCadenceChecks.Step(context,false,false,0,point:new Vector2(650,550));
                for(int i=0;i<110;i++){NativeCombatCadenceChecks.Step(context,mode==0 || mode==3 || mode==4,mode==1 || mode==2,0,point:new Vector2(650,550));Cursor();}
                int primaryCount=born.FindAll(b=>b.Item1==p.inventory[0].shoot).Count;bool assisted=born.Exists(b=>b.Item1==p.inventory[0].shoot && b.Item3.X>0);
                Console.WriteLine("CADENCE mode="+mode+" item="+item+" sources="+primaryCount+" assisted="+assisted+" itemRelease="+Count("ItemRelease")+" flailRelease="+Count("FlailRelease")+" life="+n.life);
                Require(primaryCount>=2 && assisted,"real repeated cadence preserves naturally born source attacks and shared direction, mode="+mode);
                if(mode==1)Require(Count("FlailRelease")>0,"G11A real release AI borrows registered same-source flail point");
                if(mode==2)Require(Count("ItemRelease")>0,"G11A actual item release borrows current shared prepared point");
                if(mode==3)Require(p.inventory[54].stack<999 && p.revolverCritChanceBonus>0,"native revolver resource use/release bonus remain active with Aim");
                if(mode==4)Require(born.Exists(b=>b.Item4==-2),"native magic-string release creates detached role instead of Aim extending channel");
                for(int i=0;i<3;i++)NativeCombatCadenceChecks.Step(context,false,false,0,point:new Vector2(650,550));Require(!(bool)Get(Get(combat,"Use"),"Active"),"physical release ends cadence source without cancelling native surviving attacks");
            }
        }
    }
}
