using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatLaserChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static NPC target;private static bool observe,assist;private static int births,hits;private static readonly List<Vector2> born=new List<Vector2>();
        // A pooled Projectile can be another shot by the end of the scenario.
        // The native source direction is a birth-time value, not a live lease.
        private static void Birth(Projectile __0){if(observe && __0.owner==0 && __0.type==440){births++;born.Add(__0.velocity);if(assist)Require(__0.velocity.X>0,"real laser firing window follows shared target despite left physical cursor");}}
        private static void Damage(NPC __instance,int __result){if(ReferenceEquals(target,__instance) && __result>0)hits++;}
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var input=Get(context,"Input");var attack=Get(combat,"Attack");
            var audit=new Harmony("JueMingR.Tests.LaserStages");
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks).GetMethod("SkipAchievement",Flags)));
            audit.Patch(typeof(Player).GetMethod("TryUpdateChannel",Flags),postfix:new HarmonyMethod(typeof(NativeCombatLaserChecks).GetMethod(nameof(Birth),Flags)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatLaserChecks).GetMethod(nameof(Damage),Flags)));
            bool oldWind=Main.windPhysics;Main.windPhysics=false;
            try{Pulse(context,host,combat,input,attack);Natural(context,host,combat,input,attack);}
            finally{observe=assist=false;target=null;born.Clear();Main.windPhysics=oldWind;NativeCombatObservationChecks.Save(host,new ObservationOptions());audit.UnpatchAll(audit.Id);}
        }
        private static void Pulse(object context,object host,object combat,object input,object attack)
        {
            var type=attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostPulseAttack",true);var stateType=type.GetNestedType("State",Flags);var advance=type.GetMethod("Step",Flags);var terrain=Get(attack,"terrain");int actions=0,fades=0,stops=0;
            foreach(int projectile in new[]{440,449,606})foreach(int shape in Enumerable.Range(0,8))foreach(int liquid in new[]{-1,0,2})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,2795,0,0);p.position=new Vector2(700,646);p.ResetEffects();
                if(shape==1)for(int y=30;y<46;y++)NativeToolsChecks.Tile(52,y,1);
                if(shape>=2)for(int x=40;x<70;x++){NativeToolsChecks.Tile(x,40,shape==2?19:1);if(shape==3)Main.tile[x,40].halfBrick(true);if(shape>=4)Main.tile[x,40].slope((byte)(shape-3));}
                if(liquid>=0)for(int x=35;x<75;x++)for(int y=25;y<46;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(liquid);}
                int slot=Projectile.NewProjectile(new EntitySource_DebugCommand(),new Vector2(786,610),new Vector2(30,13),projectile,0,0,0);var q=Main.projectile[slot];object state=Activator.CreateInstance(stateType,Flags,null,new object[]{q},null);Call(terrain,"Reset");
                for(int frame=0;frame<45 && q.active;frame++)
                {
                    for(int k=0;k<=q.extraUpdates && (bool)Get(state,"Alive");k++){object[] args={q,terrain,state};Require((bool)advance.Invoke(null,args),"pulse finite collision state");state=args[2];}
                    float oldPhase=q.ai[1];q.Update(slot);actions++;
                    if(oldPhase==0 && q.ai[1]==1)stops++;
                    Require((bool)Get(state,"Alive")==q.active,"pulse original tile-fade lifetime: type="+projectile+" shape="+shape+" liquid="+liquid+" frame="+frame);
                    if(!q.active){fades++;break;}
                    Require(Vector2.DistanceSquared((Vector2)Get(state,"Center"),q.Center)<.001f && Vector2.DistanceSquared((Vector2)Get(state,"Velocity"),q.velocity)<.00001f && (float)Get(state,"Phase")==q.ai[1] && (float)Get(state,"Length")==q.localAI[0] && (int)Get(state,"Left")==q.timeLeft,"pulse native segmented collision/fade: type="+projectile+" shape="+shape+" liquid="+liquid+" frame="+frame+" predicted="+Get(state,"Center")+" actual="+q.Center+" phase="+Get(state,"Phase")+"/"+q.ai[1]);
                }
            }
            Require(stops>0 && fades>0,"pulse oracle crosses real stationary fade and original Kill");Console.WriteLine("PASS pulse original motors: 72 cases actions="+actions+" tileStops="+stops+" faded="+fades+"; hostile449 is physics only");
        }
        private static void Natural(object context,object host,object combat,object input,object attack)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,2795,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.statMana=p.statManaMax=p.statManaMax2=200;p.releaseUseItem=true;Main.screenPosition=new Vector2(600,400);Main.rand=new Terraria.Utilities.UnifiedRandom(27423);
            target=NativeCombatControlPreviewChecks.Dummy(2,52,40);hits=births=0;born.Clear();observe=true;assist=false;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,false));
            for(int i=0;i<3;i++){NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,650),false,true);Require(GetOptional(attack,"ExpectedPreview")==null && !Main.projectile.Any(q=>q.active),"retired idle laser preview remains absent and cannot grant use");}
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,true));assist=true;
            NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,650),true);var parent=Main.projectile.Single(q=>q.active && q.type==439);Require(parent.velocity.X>0 && births==0,"first real controller points at target while native charge has not fired");
            int visible=0,nonWindows=0,observedLive=0;var consumes=attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostLaserAttack",true).GetMethod("Consumes",Flags);var registry=(System.Collections.IDictionary)Get(Get(attack,"Control"),"shots");
            for(int i=0;i<135;i++)
            {
                Vector2 previous=parent.velocity;bool fire=(bool)consumes.Invoke(null,new object[]{p,parent});
                NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,650),true);
                if(!fire && parent.active){Require(parent.velocity==previous,"non-window laser AI retains its sealed current direction");nonWindows++;}
                Require(GetOptional(attack,"ExpectedPreview")==null,"idle preview stays retired while the real laser source operates");
                if(GetOptional(attack,"ExpectedImpact")!=null)visible++;
                foreach(var child in Main.projectile.Where(q=>q.active && q.owner==0 && q.type==440)){Require(registry.Contains((int)child.key),"real derived pulse keeps its observed source while it is alive: frame="+i+" key="+child.key+" registry="+registry.Count);observedLive++;}
            }
            Require(parent.active && parent.ai[0]>=120 && births>=8 && hits>0 && visible>20 && nonWindows>20,"natural laser reaches all charge tiers, real child damage and sustained actual-source contacts: births="+births+" hits="+hits+" visible="+visible);
            Require(observedLive>0,"real derived pulses retain causal observations after their parent AI");
            int releaseBirths=births;NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,650),false);int released=0;
            for(;released<70 && parent.active;released++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,650),false);
            Require(!parent.active && births==releaseBirths && !p.channel,"physical release retires the parent at its original firing gate without a new child");
            assist=false;NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,false));int before=births;
            Console.WriteLine("LASER OFF entry player="+p.position+" active="+p.active+" dead="+p.dead+" mana="+p.statMana+" max="+p.statManaMax2+" item="+p.HeldItem.type+" animation="+p.itemAnimation+" time="+p.itemTime+" release="+p.releaseUseItem+" channel="+p.channel+" slow="+p.slowMagicUse);
            for(int i=0;i<35;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,650),true,true);
            // The parent dies with two animation ticks left. This non-auto-
            // reuse item consumes an early physical press without queuing a
            // new controller (Player.ItemCheck 43060/43156). Preserve that
            // native negative case, then test an actual released fresh use.
            Require(births==before && p.itemAnimation==0 && !p.channel,"early press during the original laser cooldown does not invent a new launch");
            NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,650),false,true);
            for(int i=0;i<35;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,650),true,true);
            Require(births>before && born.Skip(before).All(v=>v.X<0),"Aim OFF preserves real leftward laser source and child directions: previous="+before+" current="+births+" directions="+string.Join(";",born.Skip(before))+" player="+p.position+" dead="+p.dead+" mana="+p.statMana+" animation="+p.itemAnimation+" time="+p.itemTime+" release="+p.releaseUseItem+" channel="+p.channel+" slow="+p.slowMagicUse+" live="+string.Join(";",Main.projectile.Where(q=>q.active).Select(q=>q.type+"/"+q.ai[0]+"/"+q.ai[1]+"/"+q.velocity)));
            Console.WriteLine("PASS natural LaserMachinegun idle/charge tiers/causal children/continuous Damage/release/OFF: births="+births+" hits="+hits+" actualContacts="+visible+" nonWindows="+nonWindows+" release="+released);
        }
    }
}
