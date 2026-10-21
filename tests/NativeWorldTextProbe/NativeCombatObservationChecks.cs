using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.GameInput;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatObservationChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static OffWorkScope offWork;
        private static void CandidateBounds(){if(offWork!=null)offWork.candidates++;}
        private static void ProjectileCapture(){if(offWork!=null)offWork.projectiles++;}
        private sealed class OffWorkScope : IDisposable
        {
            private readonly Harmony audit=new Harmony("JueMingR.Tests.ObservationOffWork");
            internal int candidates,projectiles;
            private bool disposed;
            internal OffWorkScope(object selection,object geometry)
            {
                Require(offWork==null,"OFF collection observer has one scoped owner");
                // These leaves are reached immediately after the old Debug
                // increments. They count real work in optimized Release too;
                // a harmless Update entry is not itself a collected body.
                var bounds=selection.GetType().GetMethod("ReceiveBounds",Flags);
                var capture=geometry.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.ProjectileCollisionGeometry",true).GetMethod("TryCapture",Flags);
                Require(bounds!=null && bounds.GetParameters().Length==1 && capture!=null && capture.GetParameters().Length==4,"actual receiver/projectile collection observers exist");
                try
                {
                    audit.Patch(bounds,prefix:new HarmonyMethod(typeof(NativeCombatObservationChecks),nameof(CandidateBounds)));
                    audit.Patch(capture,prefix:new HarmonyMethod(typeof(NativeCombatObservationChecks),nameof(ProjectileCapture)));
                    offWork=this;
                }
                catch{audit.UnpatchAll(audit.Id);throw;}
            }
            internal void RequireUnchanged(int candidateCount,int projectileCount)
            {Require(!disposed && ReferenceEquals(offWork,this),"OFF work reads require the installed collection observer scope");Require(candidates==candidateCount && projectiles==projectileCount,"OFF real entry has no candidate/shape collection work");}
            public void Dispose(){if(disposed)return;disposed=true;offWork=null;audit.UnpatchAll(audit.Id);}
        }
        // Host lifecycles own 0 (path) and 1 (aim), including OFF releases.
        // Independent forecast fixtures use the two existing unowned slots.
        internal const int IndependentReader=2,StrictReader=3;
        internal static void Save(object host,ObservationOptions value)
        {var settings=(ObservationSettings)Get(host,"Settings");NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return settings.Loaded && !settings.Busy;});Require(settings.Set(value),"observation preference accepted; loaded="+settings.Loaded+" busy="+settings.Busy+" protected="+settings.Protected+" message="+settings.Message);NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !settings.Busy;});Require(settings.CompletionSucceeded,"observation preference actually committed: "+settings.Message);}
        internal static void Run(object context)
        {using(var futureSteps=NativeCombatUsageChecks.ObserveDrawSteps())RunCore(context);}
        private static void RunCore(object context)
        {
            var host=GetOptional(context,"CombatObservation");Require(host!=null,"full candidate has an independent observation owner");
            if(!(bool)Get(host,"PathAccess"))NativeHiddenEntryChecks.RevealForComposition(context);
            Require((bool)Get(Get(host,"Hooks"),"Ready"),"native collision observation ABI installed: "+GetOptional(Get(host,"Hooks"),"Error"));
            Save(host,new ObservationOptions());
            var selection=Get(host,"Selection");var geometry=Get(host,"Geometry");var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");
            using(var collection=new OffWorkScope(selection,geometry))
            {
                int before=collection.candidates,samples=collection.projectiles,steps=NativeCombatUsageChecks.ObservedNpcSteps;
                for(int i=0;i<600;i++)Call(host,"Update",(ulong)i);
                collection.RequireUnchanged(before,samples);
                Require(NativeCombatUsageChecks.ObservedNpcSteps==steps,"OFF real entry has no candidate/shape/prediction work");
            }
            foreach(var n in Main.npc)n.active=false;
            var player=Main.LocalPlayer;player.position=new Vector2(640,640);player.itemAnimation=player.itemTime=0;player.inventory[0].SetDefaults(ItemID.CopperPickaxe);
            var bubble=Main.npc[0];bubble.SetDefaults(371);bubble.whoAmI=0;bubble.active=true;bubble.position=new Vector2(700,650);bubble.target=0;bubble.ai[3]=1;
            Save(host,new ObservationOptions(path:true,radius:0));Fresh(context,host);
            Require((bool)Get(selection,"HasTarget") && ((NpcIdentity)Get(selection,"Target")).Type==371,"path alone selects 1 HP bubble while holding a tool with no attack; player range ignores mouse radius zero");
            var result=cache.Read(0);
            if(result==null || result.Count<=1)
            {
                // One post-failure snapshot only: inspect the actual producer
                // outcome, never prepare another future to satisfy the test.
                var source=Get(host,"Prediction");var work=Get(host,"PredictionWork");var raw=(NpcTrajectory)GetOptional(cache,"result");
                Console.WriteLine("OBSERVATION PATH FIRST FAILURE GUC="+Main.GameUpdateCount+" budgetTick="+Get(work,"tick")+" budgetSpent="+Get(work,"spent")+" budgetExceeded="+Get(work,"Exceeded")+" budgetMs="+Get(work,"UsedMilliseconds")+
                    " cacheRequired="+cache.Required+" minimum="+cache.MinimumRequired+" rawCount="+(raw==null?"none":raw.Count.ToString())+" rawStop="+(raw==null?"none":raw.Stop.ToString())+
                    " sourceFailure="+Get(Get(source,"rolling"),"FailureLayer")+" sourceOutcome="+Get(source,"outcomeStop")+" outcomeTick="+Get(source,"outcomeTick")+" future="+Get(source,"outcomeFuture")+" owns="+Get(source,"ownsState")+" observed="+Get(source,"observedCount")+" native="+(GetOptional(source,"Native")!=null)+
                    " pathFailed="+Get(host,"pathFailed")+" bubble="+bubble.type+"/style"+bubble.aiStyle+" active="+bubble.active+" life="+bubble.life+" ai="+string.Join(",",bubble.ai)+" player="+player.position+" size="+player.width+"x"+player.height+" dead="+player.dead+" gravity="+player.gravDir+" item="+player.HeldItem.type);
            }
            // This component verifies independent path ownership/recovery,
            // not an unconditional first-cold-GUC latency guarantee. Keep the
            // first miss above, and allow only actual new Host updates after
            // a measured budget expiry. No scope enlargement or same-GUC retry.
            int pathAttempts=1;bool coldBudgetMiss=(bool)Get(Get(host,"PredictionWork"),"Exceeded");
            int refresh=(int)host.GetType().GetField("DisplayRefreshUpdates",Flags|BindingFlags.Static).GetRawConstantValue();
            // A failed optional admission still owns its normal refresh clock.
            // Wait that actual cadence, rather than assuming the next GUC solves.
            while((result==null || result.Count<=1) && coldBudgetMiss && pathAttempts<=refresh*3)
            {Fresh(context,host);pathAttempts++;result=cache.Read(0);var currentSource=Get(host,"Prediction");if((long)Get(currentSource,"outcomeTick")== (long)Main.GameUpdateCount)Console.WriteLine("OBSERVATION admission GUC="+Main.GameUpdateCount+" budgetMs="+Get(Get(host,"PredictionWork"),"UsedMilliseconds")+" exceeded="+Get(Get(host,"PredictionWork"),"Exceeded")+" outcome="+Get(currentSource,"outcomeStop")+" future="+Get(currentSource,"outcomeFuture"));}
            Console.WriteLine("OBSERVATION cold path actualGucAttempts="+pathAttempts+" finalGuc="+Main.GameUpdateCount);
            if(result==null || result.Count<=1)
            {
                var source=Get(host,"Prediction");var states=(NpcMotionState[])Get(source,"states");var rolling=Get(source,"rolling");var f=Flags|BindingFlags.Static;
                var finalWork=Get(host,"PredictionWork");var finalRaw=(NpcTrajectory)GetOptional(cache,"result");
                Console.WriteLine("OBSERVATION FINAL GUC="+Main.GameUpdateCount+" optionalTick="+Get(host,"optionalTick")+" optional="+Get(host,"OptionalPreparation")+" budgetMs="+Get(finalWork,"UsedMilliseconds")+" exceeded="+Get(finalWork,"Exceeded")+" outcome="+Get(source,"outcomeStop")+" outcomeTick="+Get(source,"outcomeTick")+" future="+Get(source,"outcomeFuture")+" raw="+(finalRaw==null?"none":finalRaw.Count+"/"+finalRaw.Stop));
                var npcValid=(bool)rolling.GetType().GetMethod("Valid",f,null,new[]{typeof(NpcMotionState)},null).Invoke(null,new object[]{states[0]});
                var pm=(PredictionPlayerMotion)source.GetType().GetMethod("ReadPlayer",f).Invoke(null,new object[]{player});var playerValid=(bool)rolling.GetType().GetMethod("Valid",f,null,new[]{typeof(PredictionPlayerMotion)},null).Invoke(null,new object[]{pm});
                Console.WriteLine("OBSERVATION rejected npcValid="+npcValid+" playerValid="+playerValid+" life="+states[0].Life+" active="+states[0].Active+" receive="+states[0].CanReceive+" size="+states[0].Width+","+states[0].Height+" scale="+states[0].Scale+" water="+states[0].WaterSpeed+" playerInvalid="+pm.InvalidMechanism+" playerMax="+pm.MaxSpeed+"/"+pm.FastMaxSpeed+"/"+pm.MaxFall);
            }
            Require(result!=null && result.Count>1,"real path demand prepares a future without aim provider during three normal display recovery admissions");
            int existing=NativeCombatUsageChecks.ObservedNpcSteps;for(int i=0;i<2000;i++)Require(ReferenceEquals(cache.Read(0),result),"cached reads preserve result identity");Require(NativeCombatUsageChecks.ObservedNpcSteps==existing,"reading never advances NPC simulation");
            // This bubble is already in its short terminal phase. Preserving
            // demand cannot turn its real tail into sixty future updates.
            // Production owns slots 0 (path) and 1 (aim); independent readers
            // must survive both owners' natural OFF retirement.
            cache.Demand(IndependentReader,1,60);cache.Demand(StrictReader,60);
            Require(result.Stop==PredictionStop.Despawn && result.Count<=60 && ReferenceEquals(cache.Read(IndependentReader),result) && cache.Read(StrictReader)==null,"before OFF the short reader accepts the terminal tail and strict60 rejects it");
            Save(host,new ObservationOptions());
            Require(cache.Read(0)==null && ReferenceEquals(cache.Read(IndependentReader),result) && cache.Read(StrictReader)==null && cache.Required==60 && cache.MinimumRequired==1,"turning path OFF releases only its reader and preserves the same terminal result and both other demands");
            Fresh(context,host);
            // These two fixture-owned readers have no registered production
            // scheduler. Like any real independent owner they explicitly call
            // Source.Prepare; a cache demand retains ownership, not a hidden
            // obligation to run Path's optional producer while Path is OFF.
            Call(Get(host,"Prediction"),"Prepare",(NpcIdentity)Get(selection,"Target"),(long)Main.GameUpdateCount);
            var remaining=cache.Read(IndependentReader);
            Require((bool)Get(selection,"HasTarget") && remaining!=null && remaining.SampleTick==(long)Main.GameUpdateCount && remaining.Stop==PredictionStop.Despawn && remaining.Count<=60 && cache.Read(StrictReader)==null,"independent owner prepares a fresh truthful tail without a strict60 exemption");
            cache.Release(IndependentReader);Call(host,"Poll");Fresh(context,host);Call(Get(host,"Prediction"),"Prepare",(NpcIdentity)Get(selection,"Target"),(long)Main.GameUpdateCount);
            var strictTail=(NpcTrajectory)Get(cache,"result");
            Require((bool)Get(selection,"HasTarget") && cache.Required==60 && cache.MinimumRequired==60 && strictTail!=null && strictTail.SampleTick==(long)Main.GameUpdateCount && strictTail.Count<=60 && cache.Read(StrictReader)==null,"strict independent owner keeps selection alive and explicitly prepares while rejecting the short result");
            cache.Release(StrictReader);Call(host,"Poll");Require(cache.Required==0 && !(bool)Get(selection,"HasTarget") && GetOptional(cache,"result")==null,"last consumer retirement clears shared selection and result");Save(host,new ObservationOptions(path:true,radius:0));Fresh(context,host);
            var old=(NpcIdentity)Get(selection,"Target");var replacement=new NPC();replacement.SetDefaults(371);replacement.whoAmI=0;replacement.active=true;replacement.position=bubble.position;replacement.target=0;replacement.ai[3]=1;Main.npc[0]=bubble=replacement;Fresh(context,host);Require(!((NpcIdentity)Get(selection,"Target")).Equals(old),"slot object replacement retires old identity");
            bubble.dontTakeDamage=true;Fresh(context,host);Require(!(bool)Get(selection,"HasTarget") && cache.Read(0)==null,"invulnerability retires target without Draw or a boss lock");
            bubble.active=false;var dummy=Main.npc[1];dummy.SetDefaults(NPCID.TargetDummy);dummy.whoAmI=1;dummy.active=true;dummy.position=new Vector2(680,650);
            Fresh(context,host);Require(!(bool)Get(selection,"HasTarget"),"dummy excluded by preference");
            Save(host,new ObservationOptions(path:true,dummy:true));Fresh(context,host);Require(((NpcIdentity)Get(selection,"Target")).Type==NPCID.TargetDummy,"dummy opt-in handles immortal dummy separately");
            Save(host,new ObservationOptions(collision:true));Fresh(context,host);Require(cache.Read(0)==null && !(bool)Get(selection,"HasTarget"),"collision alone does not select or predict");
            foreach(var n in Main.npc)n.active=false;
            var near=Main.npc[2];near.SetDefaults(2);near.active=true;near.whoAmI=2;near.target=0;near.position=new Vector2(760,650);
            var far=Main.npc[3];far.SetDefaults(2);far.active=true;far.whoAmI=3;far.target=0;far.position=new Vector2(640,880);
            Main.tileSolid[TileID.Stone]=true;for(int y=36;y<49;y++){Main.tile[45,y].active(true);Main.tile[45,y].type=TileID.Stone;}
            Save(host,new ObservationOptions(path:true));Fresh(context,host);Require(((NpcIdentity)Get(selection,"Target")).Slot==2,"nearest ranks hitbox distance regardless of current occlusion");
            // The held pickaxe has no admitted attack model. Clear-line now
            // ranks actual attack feasibility, so this observation-only case
            // retains nearest. Real farther reachable attacks are covered by
            // NativeCombatSelectionPreviewChecks with their native weapons.
            Save(host,new ObservationOptions(path:true,clearLine:true));Fresh(context,host);Require(((NpcIdentity)Get(selection,"Target")).Slot==2,"without an eligible attack, clear-line does not invent a straight-ray veto");
            far.active=false;Fresh(context,host);Require(((NpcIdentity)Get(selection,"Target")).Slot==2,"occlusion never hard-removes the remaining candidate");
            for(int y=36;y<49;y++)Main.tile[45,y].active(false);
            Main.screenPosition=Vector2.Zero;Main.LocalPlayer.gravDir=1;PlayerInput.CacheOriginalScreenDimensions();
            var input=Get(context,"Input");Call(input,"BeginUpdate");PlayerInput.MouseInfo=new MouseState(765,655,0,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);Call(input,"AfterNativeMouse",new List<string>());Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");
            Save(host,new ObservationOptions(path:true,mouseCenter:true,radius:0));Call(host,"SampleMouse");var physical=(Vector2)Get(selection,"RealMouse");
            Main.mouseX=Main.mouseY=2;Fresh(context,host);Require((bool)Get(selection,"HasTarget") && ((NpcIdentity)Get(selection,"Target")).Slot==2,"zero-radius physical point can overlap a body and ignores later virtual mouse writes");
            Set(input,"mapped",false);Set(input,"PhysicalMapX",1);Call(host,"SampleMouse");Require((Vector2)Get(selection,"RealMouse")==physical,"invalid/focusless input retains last reliable intent");
            for(int i=0;i<300;i++){near.active=false;Fresh(context,host);}Require(cache.Read(0)==null,"enabled empty world never preserves a stale path");
            Save(host,new ObservationOptions(path:true,collision:true));
            foreach(int type in new[]{438,439,440})
            {
                near.SetDefaults(type);near.whoAmI=2;near.active=true;near.position=new Vector2(680,650);near.dontTakeDamage=false;near.immortal=false;near.friendly=false;near.damage=0;near.chaseable=false;Fresh(context,host);
                Require((bool)Get(selection,"HasTarget") && ((NpcIdentity)Get(selection,"Target")).Slot==2,"vulnerable ritual NPC remains receivable without contact damage or homing: "+type);
                near.dontTakeDamage=true;Fresh(context,host);Require(!(bool)Get(selection,"HasTarget"),"ritual invulnerability still excludes "+type);
            }
            near.SetDefaults(437);near.whoAmI=2;near.active=true;near.position=new Vector2(680,650);Fresh(context,host);Require(!(bool)Get(selection,"HasTarget"),"native cultist controller is not a damage receiver");
            near.SetDefaults(414);near.whoAmI=2;near.active=true;near.position=new Vector2(760,650);near.dontTakeDamage=false;near.immortal=false;
            Save(host,new ObservationOptions(path:true,collision:true,mouseCenter:true,radius:0));
            Call(input,"BeginUpdate");PlayerInput.MouseInfo=new MouseState(754,655,0,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);Call(input,"AfterNativeMouse",new List<string>());Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");Call(host,"SampleMouse");Fresh(context,host);
            Require((bool)Get(selection,"HasTarget") && ((NpcIdentity)Get(selection,"Target")).Slot==2,"mouse in native projectile tail extension selects the same observed receivable region");near.active=false;
            Save(host,new ObservationOptions());
            Console.WriteLine("PASS first batch: actual OFF=600, independent toggles, no-weapon path, bubble/dummy eligibility, identity retirement, 2000 cached reads.");
        }
        internal static void Release(object context)
        {
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())if(assembly.GetName().Name.StartsWith("JueMingR.",StringComparison.Ordinal))
            {var config=(System.Reflection.AssemblyConfigurationAttribute)Attribute.GetCustomAttribute(assembly,typeof(System.Reflection.AssemblyConfigurationAttribute));Require(config!=null && config.Configuration=="Release","Release probe must not mix Debug dependencies: "+assembly.Location);Console.WriteLine("RELEASE ASSEMBLY "+assembly.GetName().Name+" MVID="+assembly.ManifestModule.ModuleVersionId+" path="+assembly.Location);}
            var host=Get(context,"CombatObservation");var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");foreach(var n in Main.npc)n.active=false;
            Main.LocalPlayer.position=new Vector2(640,640);Main.LocalPlayer.itemAnimation=Main.LocalPlayer.itemTime=0;
            var npc=Main.npc[0];npc.SetDefaults(371);npc.whoAmI=0;npc.active=true;npc.position=new Vector2(700,650);npc.target=0;npc.ai[3]=1;
            Save(host,new ObservationOptions(path:true));Fresh(context,host);Require(cache.Read(0)!=null && cache.Read(0).Count>1,"Release path creates a real shared forecast");
            Save(host,new ObservationOptions(collision:true));Fresh(context,host);Require(cache.Read(0)==null && !(bool)Get(Get(host,"Selection"),"HasTarget"),"Release collision alone does not forecast");
            var p=Main.projectile[4];p.SetDefaults(632);p.active=true;p.whoAmI=4;p.owner=0;p.damage=10;p.friendly=true;p.velocity=Vector2.UnitX;p.localAI[1]=900;p.position=new Vector2(300,300);p.Damage();
            var geometry=Get(host,"Geometry");Require((int)Get(((Array)Get(geometry,"Attacks")).GetValue(4),"Count")==2,"Release natural Damage produces beam geometry");
            Save(host,new ObservationOptions(collision:true,path:true));Fresh(context,host);Require(cache.Read(0)!=null,"Release combined mode retains prediction");Call(host,"OnSessionEnded");Require(cache.Read(0)==null && cache.Required==0 && !(bool)Get(Get(host,"Selection"),"HasTarget") && ((Array)Get(geometry,"Attacks")).GetValue(4)==null,"Release session exit retires demands identities and geometry");
            Call(host,"OnSessionStarted");Save(host,new ObservationOptions());Fresh(context,host);Require(!(bool)Get(host,"Enabled"),"Release all OFF");Console.WriteLine("PASS Release first batch: actual shared forecast, natural beam, independent modes and complete session retirement.");
        }
        internal static void Fresh(object context,object host)
        {NativeQuickItemChecks.BeginWorldStep();Call(Get(context,"nativeNpcs"),"BeginTick");Call(host,"Update",(ulong)Main.GameUpdateCount);}
    }
}
