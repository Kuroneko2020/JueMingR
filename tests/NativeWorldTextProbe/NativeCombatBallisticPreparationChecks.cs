using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Same native fixture as the actual Shoot/Damage check. These assertions
    // isolate immutable facts, private receive history and complete checkpoints.
    internal static class NativeCombatBallisticPreparationChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly host;private static int captures,motors,scenes,checkpoints;
        private static void Captured(){captures++;}private static void Motor(){motors++;}private static void Scene(){scenes++;}
        private static void Checkpoint(){checkpoints++;}
        private static object Invoke(object value,string name,params object[] args){return value.GetType().GetMethod(name,Flags).Invoke(value,args);}
        private static object New(string name,params object[] args){var type=host.GetType("JueMingR.TerrariaHost.Combat."+name,true);return args.Length==0?Activator.CreateInstance(type,true):Activator.CreateInstance(type,Flags,null,args,null);}
        private static Projectile Shot(int type,int owner){var p=new Projectile();p.SetDefaults(type);p.owner=owner;p.active=true;p.damage=50;return p;}
        private static object Root(Player player,Projectile shot,NpcTrajectory path){return New("HostAttackObstacles",player,shot,path,0,true,false);}
        private static bool Allows(object receive,int outer=1){return (bool)Invoke(receive,"SelectedAllows",outer);}
        private static void Hit(object receive,NpcTrajectory path)
        {
            var b=path[1].ProjectileReceiveBounds;
            Invoke(receive,"PassCustomOuter",2,1,1,b.CenterX,b.CenterY,(int)b.Width,(int)b.Height,null,false,0,true,null,null);
        }
        private static AttackContact Solve(Player p,NpcTrajectory path,object terrain)
        {
            var ammo=NativeCombatBallisticCandidateChecks.Ammo(p);int type=(int)Get(ammo,"Projectile");var root=Root(p,Shot(type,p.whoAmI),path);
            return (AttackContact)host.GetType("JueMingR.TerrariaHost.Combat."+(type==711?"HostBetsyFlameAttack":"HostBottleAttack"),true).GetMethod("Solve",Flags).Invoke(null,new object[]{p,ammo,path,p.RotatedRelativePoint(p.MountedCenter),0,true,terrain,root,0});
        }
        private static object Terrain(){var value=New("PredictionTerrain");Invoke(value,"Reset");return value;}
        private static NpcTrajectory Window(NpcTrajectory path,int length){NpcTrajectory value;Require(path.TryWindow(path.SampleTick,length-1,path.Version+length,out value),"fixed immutable prefix window");return value;}
        private static void Same(AttackContact a,AttackContact b,string reason)
        {Require(a==null?b==null:b!=null && a.Tick==b.Tick && a.Subupdate==b.Subupdate && a.AimX==b.AimX && a.AimY==b.AimY && a.ImpactX==b.ImpactX && a.ImpactY==b.ImpactY && a.Confidence==b.Confidence,reason);}
        private static void ReceiveFacts(object context)
        {
            var p=NativeCombatBallisticCandidateChecks.Scene(context,3870,76);var path=NativeCombatBallisticCandidateChecks.Timeline(Main.npc[2]);
            var root=Root(p,Shot(711,p.whoAmI),path);var a=Invoke(root,"ForkCandidate");var pa=Shot(711,p.whoAmI);var ra=Invoke(a,"ForkForSpawn",pa,false);
            int first=captures;Hit(ra,path);Require(!Allows(ra),"the first flame retains its own local hit");
            var b=Invoke(root,"ForkCandidate");var rb=Invoke(b,"ForkForSpawn",Shot(711,p.whoAmI),false);
            Require(captures==first && Allows(rb),"branch B reuses captured policy while retaining clean local history after A");
            Require(ReferenceEquals(Get(ra,"policyLocal"),Get(rb,"policyLocal")) && !ReferenceEquals(Get(ra,"localNext"),Get(rb,"localNext")),"equal policy shares immutable initial facts while private hit deadlines remain independent");
            var clean=Invoke(Root(p,Shot(711,p.whoAmI),path),"ForkForSpawn",Shot(711,p.whoAmI),false);
            Require(Allows(rb)==Allows(clean),"B after A equals independently created B");
            var immune=Shot(711,p.whoAmI);immune.localNPCImmunity[2]=-1;int prior=captures;var blocked=Invoke(b,"ForkForSpawn",immune,false);
            Require(captures>prior && !Allows(blocked),"different observed local immunity is a distinct captured policy");
            var trap=Shot(711,p.whoAmI);trap.trap=true;prior=captures;Invoke(b,"ForkForSpawn",trap,false);Require(captures>prior,"trap qualification is in policy identity");
            root=Root(p,Shot(669,p.whoAmI),path);a=Invoke(root,"ForkCandidate");ra=Invoke(a,"ForkForSpawn",Shot(669,p.whoAmI),false);Hit(ra,path);
            var sibling=Invoke(a,"ForkForSpawn",Shot(669,p.whoAmI),false);b=Invoke(root,"ForkCandidate");rb=Invoke(b,"ForkForSpawn",Shot(669,p.whoAmI),false);
            Require(!Allows(sibling) && Allows(rb),"owner immunity is shared inside one family and isolated across directions");
            Console.WriteLine("BALLISTIC-PREP policy captures="+captures+" branch-independent=true local-and-trap-distinct=true");
        }
        private static void Response(NpcTrajectory path)
        {
            var response=new AttackBallisticCandidates.Response(2,true);var shortPath=Window(path,17);var medium=Window(path,33);
            AttackBallisticCandidates.GeneratePrepared(shortPath,0,660,667,14,2,true,40,10,response);
            var horizontal=(double[])Get(response,"Horizontal");double prefix=horizontal[32];
            AttackBallisticCandidates.GeneratePrepared(medium,0,660,667,14,2,true,40,10,response);
            Require((int)Get(response,"completed")==64 && horizontal[32]==prefix && ReferenceEquals(horizontal,Get(response,"Horizontal")),"response extension appends subupdates and preserves the immutable prefix");
            var angles=Activator.CreateInstance(typeof(AttackBallisticCandidates).GetNestedType("Angles",Flags),true);
            Invoke(angles,"Add",-Math.Cos(.0001),Math.Sin(.0001),1,0d);Invoke(angles,"Add",-Math.Cos(.0001),-Math.Sin(.0001),1,1d);
            Require(((Array)Invoke(angles,"Select")).Length==1,"angle seam checks wraparound neighbors");
            Invoke(angles,"Add",1d,0d,1,0d);Require(((Array)Invoke(angles,"Select")).Length==2,"distinct opposite arcs survive bounded selection");
            using(new CombatPredictionWork(.00001).Enter(Main.GameUpdateCount))
            {bool stopped=false;try{AttackBallisticCandidates.GeneratePrepared(path,0,660,667,14,2,true,40,10,new AttackBallisticCandidates.Response(2,true));}catch(CombatPredictionBudgetExceededException){stopped=true;}Require(stopped,"pure candidate loops cooperate with budget rather than claim a physical miss");}
        }
        private static void Prefix(object context,int item)
        {
            var p=NativeCombatBallisticCandidateChecks.Scene(context,item,111);var path=NativeCombatBallisticCandidateChecks.Timeline(Main.npc[2]);var terrain=Terrain();
            var scope=(IDisposable)New("HostAttackAim+SolvePreparation");int before=motors,sceneBefore=scenes;
            try
            {
                foreach(int length in new[]{17,33}){var prefix=Window(path,length);Invoke(scope,"Bind",prefix);Require(Solve(p,prefix,terrain)==null,"far prefix remains unresolved, not a fabricated contact");}
                int segmented=motors-before;Require(scenes-sceneBefore==1,"same-sample extension captures the live family once");
                int raw=motors;Solve(p,Window(path,17),Terrain());Solve(p,Window(path,33),Terrain());int restarted=motors-raw;
                // Above is still inside the same scope but a new terrain, so
                // its premise fails and it must start from clean birth state.
                Require(segmented<restarted,"complete direction checkpoints avoid repeated native-prefix motor work");
                Console.WriteLine("BALLISTIC-PREP item="+item+" segmented17+33-motors="+segmented+" restarted17+33-motors="+restarted+" sceneCaptures="+(scenes-sceneBefore));
            }
            finally{scope.Dispose();}
            int directBefore=motors,rootsBefore=checkpoints;var direct=Solve(p,path,Terrain());int directWork=motors-directBefore;
            Require(checkpoints==rootsBefore,"one-shot full-horizon solve creates no unused continuation history roots");
            terrain=Terrain();scope=(IDisposable)New("HostAttackAim+SolvePreparation");before=motors;AttackContact expanded=null;
            try{foreach(int length in new[]{17,33,65,121}){var prefix=Window(path,length);Invoke(scope,"Bind",prefix);expanded=Solve(p,prefix,terrain);if(expanded!=null)break;}}
            finally{scope.Dispose();}
            Same(direct,expanded,"segmented full-window far result equals a direct solve");
            Console.WriteLine("BALLISTIC-PREP full item="+item+" segmented-motors="+(motors-before)+" direct-motors="+directWork+" result="+direct?.Tick);
            if(item!=3870)return;
            p=NativeCombatBallisticCandidateChecks.Scene(context,item,80,tileY:25);path=NativeCombatBallisticCandidateChecks.Timeline(Main.npc[2]);
            direct=Solve(p,path,Terrain());Require(direct!=null && direct.Tick>32,"elevated target supplies a genuine late-receive expansion case");
            terrain=Terrain();scope=(IDisposable)New("HostAttackAim+SolvePreparation");expanded=null;
            try
            {
                foreach(int length in new[]{17,33,65,121}){var prefix=Window(path,length);Invoke(scope,"Bind",prefix);expanded=Solve(p,prefix,terrain);if(expanded!=null)break;}
                Same(direct,expanded,"late receive preserves direction, member/body witness and native receive tick under extension");
            }
            finally{scope.Dispose();}
            Console.WriteLine("BALLISTIC-PREP late direct="+direct.Tick+" expanded="+expanded.Tick+" aim="+expanded.AimX+","+expanded.AimY);
            // A real owner immunity write changes captured facts even if the
            // target's sampled geometry is byte-for-byte unchanged.
            p=NativeCombatBallisticCandidateChecks.Scene(context,item,111);path=NativeCombatBallisticCandidateChecks.Timeline(Main.npc[2]);terrain=Terrain();scope=(IDisposable)New("HostAttackAim+SolvePreparation");
            try
            {
                var prefix=Window(path,17);Invoke(scope,"Bind",prefix);Solve(p,prefix,terrain);int saved=scenes;
                Main.npc[2].immune[p.whoAmI]=5;prefix=Window(path,33);Invoke(scope,"Bind",prefix);Solve(p,prefix,terrain);
                Require(scenes==saved+1,"actual receive-history change retires old prepared scene/checkpoints");
                saved=scenes;p.direction=-p.direction;Solve(p,prefix,terrain);Require(scenes==saved+1,"actual owner facing change retires the previous preparation");
                saved=scenes;p.HeldItem.prefix++;Solve(p,prefix,terrain);Require(scenes==saved+1,"weapon premise change retires the previous preparation even when dry numbers happen to match");
                var key=new object();Invoke(scope,"Store",key,key);
                var samePoints=Enumerable.Range(0,prefix.Count).Select(i=>prefix[i]).ToArray();
                var otherProducer=new NpcTrajectory(prefix.Identity,prefix.SampleTick,prefix.Version+100,prefix.Assumptions,prefix.Stop,samePoints,prefix.Count,prefix.Strategy,prefix.RelationVersion+100,prefix.Quality);
                Require((bool)Invoke(scope,"Bind",otherProducer) && ReferenceEquals(Invoke(scope,"Read",key),key),"independent producer epoch numbers cannot reject an identical complete sample prefix");
                var identity=new NpcIdentity(path.Identity.Session,path.Identity.Token,path.Identity.Slot,path.Identity.Generation+1,path.Identity.Type,path.Identity.NetId);
                var replaced=new NpcTrajectory(identity,path.SampleTick,path.Version,path.Assumptions,path.Stop,Enumerable.Range(0,path.Count).Select(i=>path[i]).ToArray(),path.Count,path.Strategy);
                Require(!(bool)Invoke(scope,"Bind",replaced) && Invoke(scope,"Read",key)==null,"real target generation change discards local preparation facts");
            }
            finally{scope.Dispose();}
        }
        internal static void Run(object context)
        {
            host=Get(context,"Combat").GetType().Assembly;var audit=new Harmony("JueMingR.Tests.BallisticPreparation");captures=motors=scenes=checkpoints=0;
            audit.Patch(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackReceive",true).GetMethod("Capture",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBallisticPreparationChecks),nameof(Captured)));
            audit.Patch(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true).GetMethod("ForkCandidate",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBallisticPreparationChecks),nameof(Checkpoint)));
            foreach(string model in new[]{"HostBetsyFlameAttack","HostBottleAttack"}){var type=host.GetType("JueMingR.TerrariaHost.Combat."+model,true);audit.Patch(type.GetMethod("Motor",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBallisticPreparationChecks),nameof(Motor)));audit.Patch(type.GetMethod("Capture",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBallisticPreparationChecks),nameof(Scene)));}
            try
            {
                ReceiveFacts(context);Response(NativeCombatBallisticCandidateChecks.Timeline(Main.npc[2]));Prefix(context,3870);Prefix(context,3821);
                NativeCombatBallisticSceneChecks.Run(context);NativeCombatBallisticMotionChecks.Run(context);
                // M1's fixed candidate predates the AI010 family. The explicit
                // final leaf requests this new proof against its own fixed
                // candidate; do not accidentally test M2 against M1's DLLs.
                if(Environment.GetEnvironmentVariable("JMR_ALGORITHM_REWORK_SEEDS")=="1")NativeCombatBallisticSeedChecks.Run(context);
                if(Environment.GetEnvironmentVariable("JMR_ALGORITHM_REWORK_FREE_FLIGHT")=="1")NativeCombatBallisticFreeFlightChecks.Run(context);
            }
            finally{audit.UnpatchAll(audit.Id);}
        }
    }
}
