using System;
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
    internal static class NativeCombatFlyingKnifeChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static NPC target;private static int damage,offInputs;private static bool observeOff;
        private static void ReadOffInput(Projectile __instance)
        {
            if(!observeOff || __instance.type!=491 || __instance.owner!=0)return;
            Require(Main.MouseWorld==new Vector2(600,500),"Aim OFF original AI009 sees the physical cursor after the production AI prefix");
            if(__instance.ai[0]<=0 && Main.player[0].channel && Main.player[0].HeldItem.shoot==491)offInputs++;
        }
        private static bool trace;private static readonly System.Collections.Generic.List<string> collisionTrace=new System.Collections.Generic.List<string>();
        private static string Exact(Vector2 v){return v.X.ToString("R")+","+v.Y.ToString("R");}
        private static void TileTrace(Vector2 __0,Vector2 __1,int __2,int __3,Vector2 __result){if(trace)collisionTrace.Add("tile pos="+Exact(__0)+" v="+Exact(__1)+" size="+__2+","+__3+" out="+Exact(__result));}
        private static void SlopeTrace(Vector2 __0,Vector2 __1,int __2,int __3,Vector4 __result){if(trace)collisionTrace.Add("slope pos="+Exact(__0)+" v="+Exact(__1)+" size="+__2+","+__3+" out="+__result);}
        private static void Struck(NPC __instance,int __result){if(ReferenceEquals(target,__instance) && __result>0)damage++;}
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var input=Get(context,"Input");var attack=Get(combat,"Attack");
            var model=attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostFlyingKnifeAttack",true);var stateType=model.GetNestedType("State",Flags);var advance=model.GetMethod("Step",Flags);var terrain=Get(attack,"terrain");
            bool oldWind=Main.windPhysics;Main.windPhysics=false;int actions=0,catches=0,reflections=0;
            var traceAudit=new Harmony("JueMingR.Tests.KnifeCollisionTrace");traceAudit.Patch(AccessTools.Method(typeof(Collision),"TileCollision"),postfix:new HarmonyMethod(typeof(NativeCombatFlyingKnifeChecks).GetMethod(nameof(TileTrace),Flags)));traceAudit.Patch(AccessTools.Method(typeof(Collision),"SlopeCollision"),postfix:new HarmonyMethod(typeof(NativeCombatFlyingKnifeChecks).GetMethod(nameof(SlopeTrace),Flags)));
            try
            {
                // Explicit phase/collision states compare the original complete
                // projectile action. Natural source and release are separate below.
                foreach(int shape in Enumerable.Range(0,8))foreach(int liquid in new[]{-1,0,2})foreach(int mode in Enumerable.Range(0,7))
                {
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3030,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.channel=mode!=3 && mode!=4;p.controlUseItem=p.channel;Main.screenPosition=Vector2.Zero;
                    if(shape==1)for(int y=32;y<46;y++)NativeToolsChecks.Tile(51,y,1);
                    if(shape>=2)for(int x=40;x<70;x++){NativeToolsChecks.Tile(x,40,shape==2?19:1);if(shape==3)Main.tile[x,40].halfBrick(true);if(shape>=4)Main.tile[x,40].slope((byte)(shape-3));}
                    if(liquid>=0)for(int x=35;x<75;x++)for(int y=25;y<46;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(liquid);}
                    int slot=Projectile.NewProjectile(new EntitySource_DebugCommand(),mode==5?p.Center+new Vector2(2100,0):new Vector2(786,610),mode==6?Vector2.Zero:new Vector2(6,4),491,0,0,0);var shot=Main.projectile[slot];
                    shot.ai[0]=mode==2?-10:mode==4?1:0;shot.localAI[0]=mode==4?16:0;object state=Activator.CreateInstance(stateType,Flags,null,new object[]{shot},null);
                    Vector2 raw=mode==1 || mode==6?new Vector2(800,625):new Vector2(1100,650);Main.mouseX=(int)raw.X;Main.mouseY=(int)raw.Y;
                    Vector2 anchor;int width,height;shot.GetCollisionParams(out anchor,out width,out height);Require(width==10 && height==10,"flying knife native collision body is ten pixels");Call(terrain,"Reset");
                    for(int frame=0;frame<70 && shot.active;frame++)
                    {
                        string before="model="+Exact((Vector2)Get(state,"Center"))+"/"+Exact((Vector2)Get(state,"Velocity"))+" native="+Exact(shot.Center)+"/"+Exact(shot.velocity);
                        Vector2 aim=raw;p.LimitPointToPlayerReachableArea(ref aim);object[] args={shot,terrain,state,p.Center,aim,p.channel};Require((bool)advance.Invoke(null,args),"finite knife action has complete sampled geometry");state=args[2];float prior=shot.ai[0];collisionTrace.Clear();trace=true;try{shot.Update(slot);}finally{trace=false;}actions++;
                        if(shot.active && (Vector2.DistanceSquared((Vector2)Get(state,"Center"),shot.Center)>=.001f || Vector2.DistanceSquared((Vector2)Get(state,"Velocity"),shot.velocity)>=.00001f))Console.WriteLine("KNIFE first difference before="+before+" aim="+aim+" wet="+shot.wet+" slope="+shot.correctSlopeCollision+" stages="+string.Join(" | ",collisionTrace));
                        if(prior>-10 && shot.ai[0]==-10)reflections++;
                        Require((bool)Get(state,"Alive")==shot.active,"knife native lifetime/return catch agrees: mode="+mode+" shape="+shape+" liquid="+liquid+" frame="+frame);
                        if(!shot.active){catches++;break;}
                        Require(Vector2.DistanceSquared((Vector2)Get(state,"Center"),shot.Center)<.001f && Vector2.DistanceSquared((Vector2)Get(state,"Velocity"),shot.velocity)<.00001f && (float)Get(state,"Phase")==shot.ai[0] && (float)Get(state,"Age")==shot.localAI[0] && (int)Get(state,"Left")==shot.timeLeft && (bool)Get(state,"Tile")==shot.tileCollide && (bool)Get(state,"Wet")==shot.wet && (bool)Get(state,"Honey")==shot.honeyWet,"knife original full motion: mode="+mode+" shape="+shape+" liquid="+liquid+" frame="+frame+" model="+Get(state,"Center")+" native="+shot.Center+" velocity="+Get(state,"Velocity")+"/"+shot.velocity+" phase="+Get(state,"Phase")+"/"+shot.ai[0]);
                    }
                }
                Require(catches>0 && reflections>0,"knife matrix crosses return/catch and negative-clock transitions");Console.WriteLine("PASS flying knife full native actions="+actions+" retired="+catches+" negative-clock-transitions="+reflections);
                Natural(context,combat,host,input,attack);
            }
            finally{trace=false;traceAudit.UnpatchAll(traceAudit.Id);Main.windPhysics=oldWind;NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
        private static void Natural(object context,object combat,object host,object input,object attack)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3030,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.releaseUseItem=true;Main.screenPosition=new Vector2(600,400);target=NativeCombatControlPreviewChecks.Dummy(2,52,40);damage=0;
            var audit=new Harmony("JueMingR.Tests.FlyingKnife");foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks).GetMethod("SkipAchievement",Flags)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatFlyingKnifeChecks).GetMethod(nameof(Struck),Flags)));
            audit.Patch(AccessTools.Method(typeof(Projectile),"AI_009_MagicMissiles_Old"),prefix:new HarmonyMethod(typeof(NativeCombatFlyingKnifeChecks).GetMethod(nameof(ReadOffInput),Flags)));
            try
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,false));
                for(int i=0;i<3;i++){NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),false,true);Require(GetOptional(attack,"ExpectedPreview")!=null && !Main.projectile.Any(q=>q.active),"knife idle preview precedes native source and cannot create it");}
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,true));NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),true);var shot=Main.projectile.Single(q=>q.active && q.owner==0 && q.type==491);Require(shot.velocity.X>0,"first original knife source uses shared target despite left physical cursor");
                int visible=0;for(int i=0;i<35;i++){NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),true);if(GetOptional(attack,"ExpectedPreview")!=null)visible++;}
                Require(damage>0 && visible>3,"native continuing knife creates real dummy damage and lasting preview");
                NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),false);Require(!shot.active || shot.ai[0]>0,"physical release begins original autonomous return");
                int returning=0;for(;returning<120 && shot.active;returning++){NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),false);Require(GetOptional(attack,"ExpectedImpact")==null,"return preview cannot authorize a released knife cursor consumer");}
                Require(!shot.active,"original knife reaches owner and retires after release");
                NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),true);shot=Main.projectile.Single(q=>q.active && q.owner==0 && q.type==491);Require(shot.velocity.X>0,"real catch and fresh click obtain a new assisted source");
                // Replacing the inventory object cannot re-adopt this earlier
                // source, even though the vanilla held type remains the same.
                p.inventory[p.selectedItem]=new Item();p.inventory[p.selectedItem].SetDefaults(3030);NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),true);
                Require(GetOptional(attack,"ExpectedImpact")==null,"same-type new inventory instance retires old knife input receipt");
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,false));
                observeOff=true;offInputs=0;
                for(int i=0;i<5 && shot.active;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),true,true);
                Console.WriteLine("KNIFE OFF existing active="+shot.active+" type="+shot.type+" velocity="+shot.velocity+" phase="+shot.ai[0]+" channel="+p.channel+" nativeCursorReads="+offInputs);
                // Inventory replacement/release can let the old body return;
                // five later frames are not a new directed birth. Observe its
                // actual consumer and also require a fresh manual source.
                NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),false,true);
                for(int i=0;i<120 && Main.projectile.Any(q=>q.active && q.owner==0 && q.type==491);i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),false,true);
                Require(!Main.projectile.Any(q=>q.active && q.owner==0 && q.type==491),"OFF released knife naturally returns before the fresh manual click");
                NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),true,true);
                var manual=Main.projectile.Single(q=>q.active && q.owner==0 && q.type==491);
                Require(offInputs>0 && manual.velocity.X<0,"Aim OFF original cursor consumer and fresh source preserve manual left steering");
                Console.WriteLine("PASS natural knife idle/first click/continuous damage/release/catch/reuse/instance replacement/OFF; damage="+damage+" visible="+visible+" return-steps="+returning);
            }
            finally{target=null;observeOff=false;audit.UnpatchAll(audit.Id);}
        }
    }
}
