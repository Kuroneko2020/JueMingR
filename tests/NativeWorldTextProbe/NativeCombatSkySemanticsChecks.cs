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
    // Mechanism/permission checks are separate from the ordinary 6ms Source
    // scenes. This fixture supplies a complete future and a deliberate optional
    // proof interruption; it cannot certify Source scheduling or native damage.
    internal static class NativeCombatSkySemanticsChecks
    {
        const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        static int prepares,launches,replays,steps;
        static NpcTrajectory lastTimeline;
        static readonly HashSet<Vector2> unique=new HashSet<Vector2>();
        static void Prepared(NpcTrajectory __3){prepares++;lastTimeline=__3;}
        static void Launch(Vector2 __3){launches++;unique.Add(__3);}
        static void Advance(){steps++;}
        static void Interrupt(){replays++;throw new CombatPredictionBudgetExceededException();}
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");var assembly=attack.GetType().Assembly;
            var sky=assembly.GetType("JueMingR.TerrariaHost.Combat.HostSkyAttack",true);
            var audit=new Harmony("JueMingR.Tests.SkyOptionalProof20261025");
            audit.Patch(sky.GetMethod("LunarReplay",F),prefix:new HarmonyMethod(typeof(NativeCombatSkySemanticsChecks),nameof(Interrupt)));
            audit.Patch(sky.GetMethod("Prepare",F),prefix:new HarmonyMethod(typeof(NativeCombatSkySemanticsChecks),nameof(Prepared)));
            try
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3570,0,0);p.position=new Vector2(700,900);p.ResetEffects();p.releaseUseItem=true;
                foreach(var npc in Main.npc)npc.active=false;foreach(var q in Main.projectile)q.active=false;
                for(int x=5;x<115;x++)for(int y=5;y<78;y++)Main.tile[x,y].ClearEverything();
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.Center=new Vector2(1100,1000);n.width=n.height=34;n.noGravity=n.noTileCollide=true;n.aiStyle=-1;n.life=n.lifeMax=10000;Array.Clear(n.immune,0,n.immune.Length);
                Main.windPhysics=false;Main.screenPosition=new Vector2(600.25f,700.75f);
                NativeToolExecutionChecks.Sample(context,Get(context,"Input"),n.Center,true);Call(combat,"Sample");
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,50,true,true));
                NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,2,phaseName:"BeforeNpc");
                var solution=NativeCombatAimChecks.ReadSolution(attack);var input=NativeCombatAimChecks.QueryInput(attack,p);
                Require(solution.HasPoint && solution.Contact==null && solution.Reachability=="Unknown" && input.Allowed && input.Kind=="Enhanced","completed natural point survives the independently interrupted optional Contact, with honest Unknown");
                Require(Get(Get(attack,"currentSolution"),"PointSemantics").ToString()=="WorldPoint","the nullable point retains its spatial consumer contract");
                int before=prepares;p.direction=-p.direction;var timeline=NativeCombatAimChecks.ReadSolution(attack).Timeline;var scope=Call(attack,"BeginShot",p,p.HeldItem,true);
                try
                {
                    Require(scope!=null && prepares==before+1 && (int)Get(attack,"preparedDirection")==p.direction,"a null-Contact sky point refreshes once at the final native facing");
                    Require(ReferenceEquals(timeline,lastTimeline) && !(bool)Get(Get(attack,"currentSolution"),"HasPoint"),"final-facing refresh reuses the same published future, then real consumption retires its one-shot capability");
                    var consumed=Get(attack,"consumedInput");var point=(Vector2)Get(consumed,"Point");
                    Require(Get(consumed,"Kind").ToString()=="Enhanced" && Get(consumed,"PointSemantics").ToString()=="WorldPoint","refreshed optional-proof point reaches the real cursor scope and consumption receipt");
                    Require(Enumerable.Range(0,timeline.Count).Any(i=>{var b=timeline[i].ProjectileReceiveBounds;return Math.Abs(point.X-b.CenterX)<1.01f && Math.Abs(point.Y-b.CenterY)<1.01f;}),"final-facing refresh retains same-time natural world intent");
                }
                finally{if(scope!=null)Call(scope,"End");}
                NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,2,phaseName:"BeforeNpc");p.position.X+=16;
                Require(NativeCombatAimChecks.QueryInput(attack,p).Kind!="Enhanced","null Contact never waives the actual player/origin displacement dependency");p.position.X-=16;
                NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,2,phaseName:"BeforeNpc");
                NativeQuickItemChecks.BeginWorldStep();NativeQuickItemChecks.BeginWorldStep();
                Require(NativeCombatAimChecks.QueryInput(attack,p).Kind!="Enhanced","expired nullable points retain their original freshness limit");
                audit.Patch(sky.GetMethod("Launch",F),prefix:new HarmonyMethod(typeof(NativeCombatSkySemanticsChecks),nameof(Launch)));
                audit.Patch(typeof(AttackMotion).GetMethod("Advance",F),prefix:new HarmonyMethod(typeof(NativeCombatSkySemanticsChecks),nameof(Advance)));
                int referenceLaunch=-1,referenceUnique=-1,referenceSteps=-1;
                for(int repeat=0;repeat<3;repeat++)
                {
                    launches=replays=steps=0;unique.Clear();long allocated=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                    var process=System.Diagnostics.Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;var wall=System.Diagnostics.Stopwatch.StartNew();
                    NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,2,phaseName:"BeforeNpc");
                    var bounded=NativeCombatAimChecks.ReadSolution(attack);
                    Require(bounded.HasPoint && bounded.Contact==null && replays==1 && launches>0 && launches<=5*6*120+10,"finite natural proposal work reaches one optional proof; no per-time whole-lifetime replay");
                    if(repeat==0){referenceLaunch=launches;referenceUnique=unique.Count;referenceSteps=steps;}
                    else Require(launches==referenceLaunch && unique.Count==referenceUnique && steps==referenceSteps,"identical finite future repeats the same Launch/unique-cursor/substep work");
                    Console.WriteLine("SKY-COST component=published121/1000ms optional-proof=interrupted repeat="+repeat+" launch="+launches+" uniqueCursor="+unique.Count+" replay="+replays+" advance="+steps+" wallMs="+wall.Elapsed.TotalMilliseconds+" cpuMs="+(process.TotalProcessorTime-cpu).TotalMilliseconds+" allocated="+(AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-allocated));process.Dispose();
                }
                Console.WriteLine("SKY-SEMANTICS optional-proof=interrupted point=consumed reach=Unknown contact=null final-facing=refreshed displacement=retired freshness=retired prepares="+prepares);
            }
            finally{audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());}
            StarWrathOrigin(context,sky,assembly);
        }
        static void StarWrathOrigin(object context,Type sky,Assembly assembly)
        {
            NativeCombatLiveContextChecks.InitializeMount();
            foreach(bool mount in new[]{false,true})
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3065,0,0);p.position=new Vector2(700,900);p.ResetEffects();
                if(mount)
                {
                    int mode=Main.netMode,me=Main.myPlayer;bool dedicated=Main.dedServ;
                    try{Main.netMode=2;Main.myPlayer=1;Main.dedServ=true;p.mount.SetMount(MountID.Unicorn,p);}
                    finally{Main.netMode=mode;Main.myPlayer=me;Main.dedServ=dedicated;}
                }
                var originMethod=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels",true).GetMethod("LaunchOrigin",F,null,new[]{typeof(Player),typeof(Item)},null);
                var origin=(Vector2)originMethod.Invoke(null,new object[]{p,p.HeldItem});
                if(mount)Require(p.Center!=p.MountedCenter,"native mounted pose has a real Center/MountedCenter distinction");
                foreach(var delta in new[]{Vector2.Zero,new Vector2(13,-27)})
                {
                    var args=new object[]{p,3065,origin+delta,new Vector2(1100,1000),p.HeldItem.shootSpeed,null,null};sky.GetMethod("Launch",F).Invoke(null,args);var birth=(Vector2)args[5];
                    Require(birth.Y==p.Center.Y-600+delta.Y && birth.X==p.Center.X-200*p.direction+delta.X,"Star Wrath uses Center plus the supplied default-origin-relative future delta");
                    Console.WriteLine("SKY-3065 mount="+mount+" center="+p.Center+" mounted="+p.MountedCenter+" defaultOrigin="+origin+" futureDelta="+delta+" birth="+birth);
                }
                if(mount)p.mount.Dismount(p);
            }
        }
    }
}
