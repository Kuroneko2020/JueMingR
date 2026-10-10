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
    internal static class NativeCombatBubbleChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Projectile watched;private static int watchedKey;private static NPC receiver;private static bool inKill;private static Vector2 release;private static int damages,children;
        // NewProjectile may reuse the same pooled object for a later bullet.
        // Reference equality alone must not overwrite the parent's Kill fact.
        private static bool IsParent(Projectile shot){return ReferenceEquals(watched,shot) && shot.type==444 && (int)shot.key==watchedKey;}
        private static Vector2[] expectedCenters,expectedVelocities;private static float[] expectedAngles;private static int nativeStep;private static bool inMovement;private static string movementBefore,firstDifference;
        private static readonly System.Collections.Generic.List<string> movementTrace=new System.Collections.Generic.List<string>();
        private static string Exact(Vector2 v){return v.X.ToString("R")+","+v.Y.ToString("R");}
        private static void MovementBefore(Projectile __instance){inMovement=IsParent(__instance);if(inMovement){movementTrace.Clear();movementBefore="pos="+Exact(__instance.position)+" velocity="+Exact(__instance.velocity)+" angle="+__instance.ai[0].ToString("R")+" size="+__instance.width+","+__instance.height+" wet="+__instance.wet+" left="+__instance.timeLeft;}}
        private static void TileTrace(Vector2 __0,Vector2 __1,int __2,int __3,Vector2 __result){if(inMovement)movementTrace.Add("tile "+Exact(__0)+" v="+Exact(__1)+" size="+__2+","+__3+" out="+Exact(__result));}
        private static void SlopeTrace(Vector2 __0,Vector2 __1,int __2,int __3,Vector4 __result){if(inMovement)movementTrace.Add("slope "+Exact(__0)+" v="+Exact(__1)+" size="+__2+","+__3+" out="+__result);}
        private static void MovementAfter(Projectile __instance)
        {
            if(!inMovement)return;inMovement=false;
            if(expectedCenters!=null && nativeStep<expectedCenters.Length && firstDifference==null && (Vector2.DistanceSquared(expectedCenters[nativeStep],__instance.Center)>=.001f || Vector2.DistanceSquared(expectedVelocities[nativeStep],__instance.velocity)>=.00001f || Math.Abs(expectedAngles[nativeStep]-__instance.ai[0])>=.00001f))
            {firstDifference="step="+nativeStep+" before "+movementBefore+" model="+Exact(expectedCenters[nativeStep])+"/"+Exact(expectedVelocities[nativeStep])+" angle="+expectedAngles[nativeStep].ToString("R")+" native="+Exact(__instance.Center)+"/"+Exact(__instance.velocity)+" angle="+__instance.ai[0].ToString("R")+" trace="+string.Join(" | ",movementTrace);Console.WriteLine("BUBBLE FIRST DIFFERENCE "+firstDifference);}
            nativeStep++;
        }
        private static void KillBefore(Projectile __instance){inKill=IsParent(__instance);if(inKill){Console.WriteLine("BUBBLE Kill entry active="+__instance.active+" type="+__instance.type+" left="+__instance.timeLeft+" center="+Exact(__instance.Center)+" movements="+nativeStep);release=__instance.Center;}}
        private static void KillAfter(){inKill=false;}
        private static Exception KillFinal(Exception __exception){inKill=false;return __exception;}
        private static void Birth(Projectile __0){if(inKill && __0.type==14){children++;Require(__0.velocity.X>0,"bounced parent natural Kill aims its sealed child at the shared receiver");}}
        private static void Damage(NPC __instance,int __result){if(ReferenceEquals(__instance,receiver) && __result>0)damages++;}
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var input=Get(context,"Input");var attack=Get(combat,"Attack");var terrain=Get(attack,"terrain");
            var advance=attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostBubbleAttack",true).GetMethod("Advance",Flags);bool oldWind=Main.windPhysics;Main.windPhysics=false;
            int substeps=0,reflections=0;
            var movementAudit=new Harmony("JueMingR.Tests.BubbleMovement");
            movementAudit.Patch(AccessTools.Method(typeof(Projectile),"HandleMovement"),prefix:new HarmonyMethod(typeof(NativeCombatBubbleChecks).GetMethod(nameof(MovementBefore),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatBubbleChecks).GetMethod(nameof(MovementAfter),Flags)));
            movementAudit.Patch(AccessTools.Method(typeof(Collision),"TileCollision"),postfix:new HarmonyMethod(typeof(NativeCombatBubbleChecks).GetMethod(nameof(TileTrace),Flags)));movementAudit.Patch(AccessTools.Method(typeof(Collision),"SlopeCollision"),postfix:new HarmonyMethod(typeof(NativeCombatBubbleChecks).GetMethod(nameof(SlopeTrace),Flags)));
            try
            {
                // Original projectile.Update is the oracle. These explicit
                // scalar states exercise movement, not a claimed natural birth.
                foreach(int shape in Enumerable.Range(0,8))foreach(int liquid in new[]{-1,0,2})
                {
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,2797,0,0);p.ResetEffects();
                    if(shape==1)for(int y=32;y<=44;y++)NativeToolsChecks.Tile(50,y,1);
                    if(shape>=2)for(int x=40;x<=65;x++){NativeToolsChecks.Tile(x,40,shape==2?19:1);if(shape==3)Main.tile[x,40].halfBrick(true);if(shape>=4)Main.tile[x,40].slope((byte)(shape-3));}
                    if(liquid>=0)for(int x=35;x<70;x++)for(int y=25;y<44;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(liquid);}
                    int slot=Projectile.NewProjectile(new EntitySource_DebugCommand(),new Vector2(786,610),new Vector2(6,4),444,0,0,0);
                    var q=Main.projectile[slot];q.ai[0]=new Vector2(6,4).ToRotation();q.localAI[0]=14;q.localAI[1]=8;
                    Require(q.ignoreWater && !q.correctSlopeCollision,"locked bubble uses ordinary 10px collision and ignores water displacement");
                    Vector2 center=q.Center,velocity=q.velocity;float angle=q.ai[0];Call(terrain,"Reset");
                    for(int frame=0;frame<29;frame++)
                    {
                        for(int k=0;k<=q.extraUpdates;k++)
                        {
                            float oldAngle=angle;object[] args={q,terrain,center,velocity,angle};Require((bool)advance.Invoke(null,args),"finite bubble movement has sampled geometry");center=(Vector2)args[2];velocity=(Vector2)args[3];angle=(float)args[4];if(Math.Abs(angle-oldAngle)>.1f)reflections++;substeps++;
                        }
                        q.Update(slot);
                        Require(q.active && Vector2.DistanceSquared(center,q.Center)<.001f && Vector2.DistanceSquared(velocity,q.velocity)<.00001f && Math.Abs(angle-q.ai[0])<.00001f,"bubble native wall/platform/half/slope/liquid action matches: shape="+shape+" liquid="+liquid+" frame="+frame+" model="+center+" native="+q.Center+" velocity="+velocity+"/"+q.velocity);
                    }
                }
                Require(reflections>0,"movement matrix actually crosses reflection boundaries");
                Console.WriteLine("PASS original bubble AI78 collision/angle movement substeps="+substeps+" reflections="+reflections);
                Natural(context,host,combat,input,attack,terrain,advance);
            }
            finally{Main.windPhysics=oldWind;NativeCombatObservationChecks.Save(host,new ObservationOptions());movementAudit.UnpatchAll(movementAudit.Id);}
        }
        private static void Natural(object context,object host,object combat,object input,object attack,object terrain,MethodInfo advance)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,2797,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.releaseUseItem=true;p.inventory[54].SetDefaults(3104);Main.screenPosition=new Vector2(600,400);Main.rand=new Terraria.Utilities.UnifiedRandom(7193);
            for(int y=35;y<=43;y++)NativeToolsChecks.Tile(40,y,1);
            receiver=NativeCombatControlPreviewChecks.Dummy(2,55,40);damages=children=0;release=new Vector2(float.NaN);
            var audit=new Harmony("JueMingR.Tests.BubbleBounce");
            // The CPU harness omits achievements, as in the existing natural
            // Player phase oracle; movement, use, projectile and damage stay native.
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks).GetMethod("SkipAchievement",Flags)));
            audit.Patch(AccessTools.Method(typeof(Projectile),"Kill"),prefix:new HarmonyMethod(typeof(NativeCombatBubbleChecks).GetMethod(nameof(KillBefore),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatBubbleChecks).GetMethod(nameof(KillAfter),Flags)),finalizer:new HarmonyMethod(typeof(NativeCombatBubbleChecks).GetMethod(nameof(KillFinal),Flags)));
            audit.Patch(typeof(Player).GetMethod("TryUpdateChannel",Flags),postfix:new HarmonyMethod(typeof(NativeCombatBubbleChecks).GetMethod(nameof(Birth),Flags)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatBubbleChecks).GetMethod(nameof(Damage),Flags)));
            try
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,false));
                for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,650),false,true);
                NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,650),true);
                watched=Main.projectile.First(q=>q.active && q.owner==0 && q.type==444);watchedKey=(int)watched.key;Require(watched.velocity.X<0,"real manual birth initially travels toward left wall");
                Vector2 center=watched.Center,velocity=watched.velocity;float angle=watched.ai[0];Call(terrain,"Reset");int predictedReflections=0;
                expectedCenters=new Vector2[watched.timeLeft];expectedVelocities=new Vector2[watched.timeLeft];expectedAngles=new float[watched.timeLeft];nativeStep=0;firstDifference=null;
                Console.WriteLine("BUBBLE original birth sample pos="+Exact(watched.position)+" velocity="+Exact(watched.velocity)+" angle="+angle.ToString("R")+" left="+watched.timeLeft+" extra="+watched.extraUpdates);
                for(int k=0;k<watched.timeLeft;k++){float prior=angle;object[] args={watched,terrain,center,velocity,angle};Require((bool)advance.Invoke(null,args),"natural bubble finite release origin");center=(Vector2)args[2];velocity=(Vector2)args[3];angle=(float)args[4];expectedCenters[k]=center;expectedVelocities[k]=velocity;expectedAngles[k]=angle;if(Math.Abs(angle-prior)>.1f)predictedReflections++;}
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,true));
                int visible=0;bool bounced=false;float priorVx=watched.velocity.X;
                for(int i=0;i<75;i++)
                {
                    string before="center="+Exact(watched.Center)+" velocity="+Exact(watched.velocity)+" angle="+watched.ai[0].ToString("R")+" left="+watched.timeLeft;
                    NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,650),false);
                    int end=(i+1)*(watched.extraUpdates+1)-1;
                    if(IsParent(watched) && watched.active && end<expectedCenters.Length && firstDifference==null && (Vector2.DistanceSquared(expectedCenters[end],watched.Center)>=.001f || Vector2.DistanceSquared(expectedVelocities[end],watched.velocity)>=.00001f || Math.Abs(expectedAngles[end]-watched.ai[0])>=.00001f))
                    {firstDifference="outer="+i+" before="+before+" expected="+Exact(expectedCenters[end])+"/"+Exact(expectedVelocities[end])+" angle="+expectedAngles[end].ToString("R")+" actual="+Exact(watched.Center)+"/"+Exact(watched.velocity)+" angle="+watched.ai[0].ToString("R")+" nativeMoves="+nativeStep;Console.WriteLine("BUBBLE OUTER FIRST DIFFERENCE "+firstDifference);}
                    if(IsParent(watched) && watched.active){if(priorVx<0 && watched.velocity.X>0)bounced=true;priorVx=watched.velocity.X;if(GetOptional(attack,"ExpectedPreview")!=null)visible++;}
                }
                Require(predictedReflections>0 && bounced,"real parent reaches and reflects off the wall before lifetime exit");
                Require(children==1 && Vector2.DistanceSquared(center,release)<.001f,"natural Kill child birth uses the predicted reflected origin: children="+children+" predicted="+center+" actual="+release+" parentActive="+watched.active+" left="+watched.timeLeft+" reflections="+predictedReflections+" visible="+visible+" damage="+damages);
                Require(visible>5 && damages>0,"released reflected source keeps a useful preview and native child damage");
                Console.WriteLine("PASS natural bubble wall bounce -> lifetime Kill -> sealed child -> actual Damage; visible="+visible+" damage="+damages+" origin="+release);
            }
            finally{watched=null;receiver=null;inKill=inMovement=false;expectedCenters=expectedVelocities=null;expectedAngles=null;audit.UnpatchAll(audit.Id);}
        }
    }
}
