using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCombatAimChecks
    {

        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        // Shared native setup and sealed fact readers keep their real consumers.
        // Weapon assertions remain in the ordinary Aim leaf and specialist leaves.
        internal static void Initialize()
        {
            typeof(Main).GetMethod("Initialize_TileAndNPCData1",Flags).Invoke(null,null);typeof(Main).GetMethod("Initialize_TileAndNPCData2",Flags).Invoke(null,null);
            Terraria.ObjectData.TileObjectData.Initialize();Lighting.Mode=Terraria.Graphics.Light.LightMode.Color;
            NativeCombatWorkerAssetChecks.Initialize();
            // Main's real entity initialization also fills its static gore
            // array. These components execute original Kill/explosion paths,
            // so supply normal original Gore instances, not a fake ABI type.
            for(int i=0;i<Main.gore.Length;i++)Main.gore[i]=new Gore();
            Terraria.GameContent.Creative.CreativePowerManager.Initialize();Terraria.DataStructures.ArmorSetBonuses.Initialize();Terraria.DataStructures.ArmorSetBonuses.BuildLookup();
            if(Main.instance==null){Main.instance=(Main)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Main));GC.SuppressFinalize(Main.instance);}
            PopupText.popupText=new PopupText[20];for(int i=0;i<20;i++)PopupText.popupText[i]=new PopupText();
            foreach(var item in Main.item)item.whoAmI=Array.IndexOf(Main.item,item);
            for(int i=1;i<Main.player.Length;i++)if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};
        }
        internal static void Prepare(object host,object attack,NPC body,float vx,bool projectilePhase=false)
        {PublishFixture(host,body,vx);Call(attack,projectilePhase?"PrepareProjectiles":"Prepare");}
        internal static void PrepareBeforeNpc(object host,object attack,NPC body,float vx)
        {
            // Direct ItemCheck_Shoot observes the pre-NPC epoch. Keep the
            // ordinary allowance while providing that phase's T-1 sample.
            PublishFixtureAt(host,body,vx,(long)Main.GameUpdateCount-1);
            Call(attack,"PrepareAction");
        }
        // Complete-model/native-outcome components have their own finite work
        // scope. They do not assert that this synthetic121 future can be built
        // during a cold real input allowance. Native Shoot still uses the normal
        // operation/permission outlet and retains its real owner protection.
        internal static void PrepareMechanismFixture(object host,object attack,NPC body,float vx,bool projectilePhase=false,string phaseName=null)
        {
            // A fixture inside the actual pre-NPC Shoot entry still observes
            // T-1 NPCs. Match that real phase instead of moving native clocks.
            PublishFixtureAt(host,body,vx,phaseName=="BeforeNpc"?(long)Main.GameUpdateCount-1:(long)Main.GameUpdateCount);
            var phase=attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true);
            using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))Call(attack,"PrepareCore",Enum.Parse(phase,phaseName??(projectilePhase?"Projectiles":"CompletedWorld")));
        }
        // Retain the unique three-argument reflection boundary used by the
        // existing navigation oracle; MethodInfo.Invoke does not fill C#
        // optional parameters. The explicit phase caller uses a named core.
        private static void PublishFixture(object host,NPC body,float vx)
        {PublishFixtureAt(host,body,vx,null);}
        private static void PublishFixtureAt(object host,NPC body,float vx,long? sampleTick)
        {
            var selection=Get(host,"Selection");Call(selection,"Update",((ObservationSettings)Get(host,"Settings")).Value,(long)Get(host,"Session"),true,null);
            var id=(NpcIdentity)Get(selection,"Target");Require(id.Slot==body.whoAmI,"single shared final target is selected");
            var points=new NpcTrajectoryPoint[121];for(int i=0;i<points.Length;i++)points[i]=new NpcTrajectoryPoint(i,new NpcMotionState{Identity=id,X=body.position.X+vx*i,Y=body.position.Y,Width=body.width,Height=body.height,Vx=vx,A0=body.ai[0],Wet=body.wet,NoTileCollide=body.noTileCollide,CanReceive=true,Active=true});
            // The synthetic component path reads no NPC terrain. Initialize its empty
            // terrain lease against real world dimensions before publication;
            // an uninitialized lease cannot authorize the display component.
            var prediction=Get(host,"Prediction");Call(Get(prediction,"Terrain"),"Reset");
            var cache=(NpcPredictionCache)Get(prediction,"Cache");cache.Demand(0,1,120);cache.Demand(1,1,120);cache.Publish(new NpcTrajectory(id,sampleTick??Main.GameUpdateCount,1,PredictionAssumption.None,PredictionStop.None,points,points.Length));
        }
        internal static void PrepareDisplay(object host,object attack)
        {
            // This synthetic trajectory helper belongs only to components.
            // Explicitly perform Host's distinct display capture/validation
            // and preview duties. Its pre-published input is not evidence of
            // natural first-click preparation or display scheduling.
            if(!(bool)Get(host,"Path"))return;
            host.GetType().GetMethod("CaptureDisplay",Flags,null,new[]{typeof(long)},null).Invoke(host,new object[]{(long)Main.GameUpdateCount});
            Call(host,"ValidateDisplay",(long)Main.GameUpdateCount);
            Call(attack,"PreparePreviewOnly");Call(attack,"ValidatePreviewPresentation");
        }
        internal static object PhaseClock(object attack,NpcTrajectory timeline,string phase)
        {var assembly=attack.GetType().Assembly;var stage=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true);var type=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true);return Activator.CreateInstance(type,Flags,null,new object[]{timeline,Enum.Parse(stage,phase)},null);}
        internal struct SolutionFacts
        {
            internal NpcTrajectory Timeline;internal Vector2 Point;internal int ReceiveTick,NativeTick,FirstTick,Delay;
            internal AttackContact Contact;internal bool HasPoint;internal string Reachability,Route,Phase;
        }
        internal struct InputFacts
        {
            internal bool Allowed;internal string Kind;internal Vector2 Point;internal NpcIdentity Target;
        }
        // Facts-only component adapters. Reading a private candidate/controller
        // is not an Aim permission query and never lends/records input. A caller
        // separately proves plan freshness and the real consumer's permission.
        internal static SolutionFacts ReadSolutionValue(object attack,object solution,string phase,int launchDelay=0)
        {
            var facts=new SolutionFacts{Route="representative",Phase=phase,Delay=launchDelay,ReceiveTick=-1,NativeTick=-1,FirstTick=-1,Reachability="Unknown"};
            if(solution==null)return facts;
            facts.Timeline=(NpcTrajectory)GetOptional(solution,"Timeline");facts.Point=(Vector2)Get(solution,"Point");facts.HasPoint=(bool)Get(solution,"HasPoint");
            facts.ReceiveTick=(int)Get(solution,"ReceiveTick");facts.Reachability=Get(solution,"Reachability").ToString();facts.Contact=(AttackContact)GetOptional(solution,"Contact");
            return FinishFacts(attack,facts);
        }
        internal static SolutionFacts ReadPlanSolution(object attack,object plan)
        {
            if(plan==null)return new SolutionFacts{Route="unprepared-plan",Phase="None",Reachability="Unknown",ReceiveTick=-1,NativeTick=-1,FirstTick=-1};
            string phase=Get(plan,"Phase").ToString();int delay=(int)Get(plan,"Delay");
            if((bool)Get(plan,"Balanced"))
            {
                // An observed held body supplies its own cooldown to the birth
                // adapter. PrepareBalancedPreview uses clock delay zero there;
                // an opening instead uses the plan's future-hand delay.
                var held=Main.LocalPlayer.HeldItem.shoot==615 || Main.LocalPlayer.HeldItem.shoot==630 || Main.LocalPlayer.HeldItem.shoot==705?ReadObservedHeldSource(attack):null;
                var value=ReadSolutionValue(attack,Get(plan,"Solution"),phase,held==null?delay:0);
                value.Delay=held==null?delay:Math.Max(0,(int)Math.Ceiling(((Projectile)Get(held,"Shot")).ai[1])-1);
                return value;
            }
            var contact=(AttackContact)GetOptional(plan,"Contact");
            var facts=new SolutionFacts{Route="dedicated-contact",Phase=phase,Delay=delay,ReceiveTick=-1,NativeTick=-1,FirstTick=-1,Reachability="Unknown",Contact=contact};
            if(contact!=null){facts.Delay=contact.Delay;facts.Timeline=contact.Timeline;facts.Point=new Vector2(contact.AimX,contact.AimY);facts.HasPoint=true;facts.ReceiveTick=contact.ReceiveTick;facts.Reachability="Reachable";}
            return FinishFacts(attack,facts);
        }
        internal static object ReadObservedHeldSource(object attack)
        {
            var control=Get(attack,"Control");
            foreach(System.Collections.DictionaryEntry pair in (System.Collections.IDictionary)Get(control,"shots"))
            {
                var entry=pair.Value;int kind=(int)Get(entry,"Type");if(kind!=615 && kind!=630 && kind!=705)continue;
                var observed=control.GetType().GetMethod("Observed",Flags,null,new[]{entry.GetType()},null);
                if((bool)observed.Invoke(control,new[]{entry}))return entry;
            }
            return null;
        }
        private static SolutionFacts FinishFacts(object attack,SolutionFacts facts)
        {
            if(facts.Timeline==null)return facts;
            var assembly=attack.GetType().Assembly;var phase=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true);var type=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true);
            var clock=Activator.CreateInstance(type,Flags,null,new object[]{facts.Timeline,Enum.Parse(phase,facts.Phase),facts.Delay},null);
            facts.FirstTick=(int)Get(clock,"FirstTick");facts.NativeTick=facts.Contact!=null?facts.Contact.Tick:facts.ReceiveTick<facts.FirstTick?-1:facts.ReceiveTick-facts.FirstTick+1;
            return facts;
        }
        // Read the actual post-7a8f18a ordinary solution. A representative point
        // is not a fabricated full Contact; dedicated solvers retain theirs.
        // These are finite component observations, never a second input owner.
        internal static SolutionFacts ReadSolution(object attack,bool next=false)
        {
            bool balanced=(bool)Get(attack,"balancedOrdinary");bool future=next || Main.GameUpdateCount>(uint)Get(attack,"prepared");
            string phase=Get(attack,"preparedOrdinaryPhase").ToString();
            var result=new SolutionFacts{Route=balanced?"representative":"dedicated-contact",Phase=phase,ReceiveTick=-1,NativeTick=-1,FirstTick=-1};
            if(balanced)
            {
                var value=Get(attack,future?"nextSolution":"currentSolution");
                result.Timeline=(NpcTrajectory)GetOptional(value,"Timeline");result.Point=(Vector2)Get(value,"Point");result.HasPoint=(bool)Get(value,"HasPoint");
                result.ReceiveTick=(int)Get(value,"ReceiveTick");result.Reachability=Get(value,"Reachability").ToString();result.Contact=(AttackContact)GetOptional(value,"Contact");
            }
            else
            {
                // Snapshot the prepared field before any validity/consumer query.
                // ExpectedImpact may ClearExecution when its live receiver gate
                // fails; an observation must not erase the cause being tested.
                result.Contact=(AttackContact)GetOptional(attack,future?"next":"current");
                if(result.Contact!=null){result.Timeline=result.Contact.Timeline;result.Point=new Vector2(result.Contact.AimX,result.Contact.AimY);result.HasPoint=true;result.ReceiveTick=result.Contact.ReceiveTick;result.Reachability="Reachable";}
            }
            if(result.Timeline!=null)
            {
                result.FirstTick=(int)Get(PhaseClock(attack,result.Timeline,phase),"FirstTick");
                result.NativeTick=result.Contact!=null?result.Contact.Tick:result.ReceiveTick<result.FirstTick?-1:result.ReceiveTick-result.FirstTick+1;
            }
            return result;
        }
        internal static InputFacts QueryInput(object attack,Player player,string phase="BeforeNpc")
        {
            var stage=attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true);
            var args=new object[]{player,player.HeldItem,Enum.Parse(stage,phase),null};
            bool allowed=(bool)attack.GetType().GetMethod("TryOrdinaryInput",Flags).Invoke(attack,args);
            return new InputFacts{Allowed=allowed,Kind=allowed?Get(args[3],"Kind").ToString():"None",Point=allowed?(Vector2)Get(args[3],"Point"):Vector2.Zero,Target=allowed?(NpcIdentity)Get(args[3],"Target"):default(NpcIdentity)};
        }
        internal static SolutionFacts RequireSolution(object attack,Player player,string reason)
        {
            var value=ReadSolution(attack);var input=QueryInput(attack,player);
            Require(value.HasPoint && value.Timeline!=null && value.Reachability=="Reachable" && value.ReceiveTick>=0 && value.ReceiveTick<value.Timeline.Count && value.NativeTick>0 &&
                !float.IsNaN(value.Point.X) && !float.IsNaN(value.Point.Y) && !float.IsInfinity(value.Point.X) && !float.IsInfinity(value.Point.Y) && input.Allowed && input.Kind=="Enhanced" && input.Target.Equals(value.Timeline.Identity) && Vector2.DistanceSquared(input.Point,value.Point)<.0001f,reason);
            if(value.Contact!=null)Require(ReferenceEquals(value.Contact.Timeline,value.Timeline) && value.Contact.ReceiveTick==value.ReceiveTick && Vector2.DistanceSquared(new Vector2(value.Contact.AimX,value.Contact.AimY),value.Point)<.0001f,"Optional dedicated Contact agrees with its actual solution clock/target/point");
            return value;
        }
        internal static void RequireConsumedInput(object attack,Player player,SolutionFacts solution,string reason,string phase="BeforeNpc")
        {
            // RecordInput is published only by the native cursor scope's Begin,
            // never by QueryInput. Call after the real hand Shoot, with the
            // solution sealed before Shoot; the hand has no source projectile.
            var consumed=Get(attack,"consumedInput");
            Require((bool)Get(attack,"hasConsumedInput") && (uint)Get(attack,"consumedInputStep")==Main.GameUpdateCount &&
                Get(consumed,"Kind").ToString()=="Enhanced" && Get(consumed,"Phase").ToString()==phase &&
                ReferenceEquals(Get(consumed,"Weapon"),player.HeldItem) && GetOptional(consumed,"Source")==null &&
                ((NpcIdentity)Get(consumed,"Target")).Equals(solution.Timeline.Identity) &&
                Vector2.DistanceSquared((Vector2)Get(consumed,"Point"),solution.Point)<.0001f,reason);
            Console.WriteLine("NATIVE HAND CONSUMED tick="+Main.GameUpdateCount+" phase="+phase+" target="+solution.Timeline.Identity.Slot+" kind="+Get(consumed,"Kind")+" point="+Get(consumed,"Point")+" source=hand route="+solution.Route+" receive="+solution.ReceiveTick+" first="+solution.FirstTick);
        }
        internal static void SolutionCheckpoint(object context,object attack,string stage)
        {
            var host=Get(context,"CombatObservation");var movement=Get(context,"Movement");var assembly=attack.GetType().Assembly;var player=Main.LocalPlayer;
            var motion=(PredictionPlayerMotion)Get(host,"Prediction").GetType().GetMethod("ReadPlayer",Flags).Invoke(null,new object[]{player});
            string hash;using(var sha=System.Security.Cryptography.SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(System.IO.File.ReadAllBytes(assembly.Location))).Replace("-","");
            Console.WriteLine("AMMO CURRENT CHECKPOINT stage="+stage+" hostMvid="+assembly.ManifestModule.ModuleVersionId+" sha256="+hash+" tick="+Main.GameUpdateCount+" prepared="+Get(attack,"prepared")+" phase="+Get(attack,"preparedOrdinaryPhase")+" balanced="+Get(attack,"balancedOrdinary")+" failed="+Get(attack,"Failed")+" permission="+Get(attack,"Permission")+" receiver="+Get(attack,"CurrentReceiver")+" movement="+Call(movement,"IsEnabled",0)+","+Call(movement,"IsEnabled",1)+","+Call(movement,"IsEnabled",2)+" assisted="+motion.AssistedJump+" intentVersion="+motion.AssistedJumpVersion);
            var nextMotion=(PredictionPlayerMotion)Get(attack,"nextBalancedMotion");var preparedMotion=(PredictionPlayerMotion)Get(attack,"preparedMotion");
            Console.WriteLine("AMMO ORIGIN stage="+stage+" origin="+Get(attack,"origin")+" nextOrigin="+Get(attack,"nextOrigin")+" currentPlayer="+player.position+" velocity="+player.velocity+" mountedCenter="+player.MountedCenter+" observed="+motion.X+","+motion.Y+"/"+motion.Vx+","+motion.Vy+" observedGravity="+motion.Gravity+"/"+motion.GravityDirection+" preparedMotion="+preparedMotion.X+","+preparedMotion.Y+"/"+preparedMotion.Vx+","+preparedMotion.Vy+" nextMotion="+nextMotion.X+","+nextMotion.Y+"/"+nextMotion.Vx+","+nextMotion.Vy+" nextComplex="+nextMotion.Complex+" nextWall="+nextMotion.WallGross+","+nextMotion.WallTongued+" nextMount="+nextMotion.Mounted+" originalFilm="+player.isControlledByFilm);
            foreach(string field in new[]{"currentSolution","nextSolution"})
            {
                var value=Get(attack,field);var timeline=(NpcTrajectory)GetOptional(value,"Timeline");
                Console.WriteLine("AMMO SOLUTION field="+field+" point="+Get(value,"Point")+" has="+Get(value,"HasPoint")+" reach="+Get(value,"Reachability")+" receive="+Get(value,"ReceiveTick")+" contact="+(GetOptional(value,"Contact")!=null)+" sample="+(timeline?.SampleTick??-1)+" count="+(timeline?.Count??0)+" target="+(timeline==null?"none":timeline.Identity.Slot+"/"+timeline.Identity.Generation+"/"+timeline.Identity.Type)+" rawCurrent="+(GetOptional(attack,"current")!=null)+" rawNext="+(GetOptional(attack,"next")!=null));
            }
            var input=QueryInput(attack,player);var consumed=Get(attack,"consumedInput");
            Console.WriteLine("AMMO INPUT query="+input.Allowed+" kind="+input.Kind+" point="+input.Point+" target="+input.Target.Slot+" consumed="+Get(attack,"hasConsumedInput")+" consumedTick="+Get(attack,"consumedInputStep")+" consumedKind="+Get(consumed,"Kind")+" consumedPoint="+Get(consumed,"Point")+" consumedSource="+(GetOptional(consumed,"Source")!=null));
        }
    }
}
