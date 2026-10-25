using System;
using System.Collections.Generic;
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
    internal static class NativeCombatFixedSkyChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static bool legacy;
        private static bool insideFixed,inverseObserved,finalReplayObserved;
        private static AttackContact inverseContact,finalReplayContact;
        private static Vector2 replayAim,replayOrigin,replayVelocity;
        private struct NativeBirth {internal int Type,Owner,Damage,MouseX,MouseY;internal Vector2 Center,Velocity;}
        private static bool ordinaryShoot;
        private static readonly List<NativeBirth> ordinaryBirths=new List<NativeBirth>();
        private static void Born(int __result)
        {
            if(!ordinaryShoot || __result<0 || __result>=Main.maxProjectiles)return;
            var q=Main.projectile[__result];ordinaryBirths.Add(new NativeBirth{Type=q.type,Owner=q.owner,Damage=q.damage,Center=q.Center,Velocity=q.velocity,MouseX=Main.mouseX,MouseY=Main.mouseY});
        }
        // This test-only exclusion runs the unchanged TASK_BASE sky search
        // through the same current host interfaces and actual native motor.
        private static bool Fixed(ref AttackContact __result)
        {insideFixed=!legacy;inverseObserved=finalReplayObserved=false;inverseContact=finalReplayContact=null;replayAim=replayOrigin=replayVelocity=Vector2.Zero;if(!legacy)return true;__result=null;return false;}
        // Typed values only: these observers never write native/ref arguments,
        // invoke a solve, or grant a candidate input.
        private static void Inverse(AttackContact __result){if(insideFixed){inverseObserved=true;inverseContact=__result;}}
        private static void ReplayStart(float aimX,float aimY,float x,float y,float vx,float vy)
        {if(insideFixed && inverseObserved){replayAim=new Vector2(aimX,aimY);replayOrigin=new Vector2(x,y);replayVelocity=new Vector2(vx,vy);}}
        private static void ReplayDone(AttackContact __result){if(insideFixed && inverseObserved){finalReplayObserved=true;finalReplayContact=__result;}}
        private static void FixedDone(){insideFixed=false;}
        private static void CandidateIdentity()
        {
            string candidate=Environment.GetEnvironmentVariable("JUEMINGR_COMBAT_GEOMETRY_CANDIDATE");if(string.IsNullOrEmpty(candidate))return;
            string directory=System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(candidate));
            Func<string,string> hash=path=>{using(var file=System.IO.File.OpenRead(path))using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","");};
            foreach(string name in new[]{"JueMingR.Platform","JueMingR.Features","JueMingR.Infrastructure","JueMingR.TerrariaHost"})
            {
                var loaded=AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a=>a.GetName().Name==name);string expected=System.IO.Path.Combine(directory,name+".dll");
                Require(loaded!=null && System.IO.File.Exists(expected) && hash(loaded.Location)==hash(expected),"explicit sky candidate binds every actual production component: "+name);
                Console.WriteLine("EXPLICIT-COMPONENT "+name+" path="+loaded.Location+" MVID="+loaded.ManifestModule.ModuleVersionId+" SHA256="+hash(loaded.Location));
            }
        }
        private static NpcTrajectory Path(NPC n)
        {
            var key=new NpcIdentity(1,n,n.whoAmI,n.generation,n.type,n.netID);
            var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true,Active=true,NoTileCollide=n.noTileCollide};
            var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(key,Main.GameUpdateCount,1,PredictionAssumption.None,PredictionStop.None,points,points.Length);
        }
        internal static void Run(object context)
        {RunCore(context,false);}
        internal static void RunReflection(object context)
        {RunCore(context,true);}
        private static void RunCore(object context,bool reflection)
        {
            var host=Get(context,"Combat").GetType().Assembly;CandidateIdentity();var model=host.GetType("JueMingR.TerrariaHost.Combat.HostSkyAttack",true);var audit=new Harmony("JueMingR.Tests.FixedSky");
            audit.Patch(model.GetMethod("FixedSky",Flags),prefix:new HarmonyMethod(typeof(NativeCombatFixedSkyChecks),nameof(Fixed)),postfix:new HarmonyMethod(typeof(NativeCombatFixedSkyChecks),nameof(FixedDone)));
            audit.Patch(typeof(AttackIntercept).GetMethod("Solve",Flags),postfix:new HarmonyMethod(typeof(NativeCombatFixedSkyChecks),nameof(Inverse)));
            audit.Patch(typeof(AttackIntercept).GetMethod("Replay",Flags),prefix:new HarmonyMethod(typeof(NativeCombatFixedSkyChecks),nameof(ReplayStart)),postfix:new HarmonyMethod(typeof(NativeCombatFixedSkyChecks),nameof(ReplayDone)));int cases=0;
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatFixedSkyChecks),nameof(Born)));
            string replayRotation=Environment.GetEnvironmentVariable("JMR_FIXED_SKY_REPLAY_ROTATION");float rotation=0;
            if(replayRotation!=null){Require(Environment.GetEnvironmentVariable("JMR_WEAPON_EFFECT_ORACLE_ONLY")=="fixed-sky" && float.TryParse(replayRotation,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out rotation),"fixed-sky input replay is explicit local feedback only");Console.WriteLine("LOCAL INPUT REPLAY rotation="+rotation+"; default Effects not evaluated");}
            try
            {
                foreach(bool mirror in new[]{false,true})foreach(int distance in new[]{160,320,500})foreach(int immunity in new[]{0,12})
                {
                    if(reflection && (mirror || distance!=160 || immunity!=0))continue;
                    var p=NativeCombatBallisticCandidateChecks.Scene(context,3065,50,mirror);foreach(var npc in Main.npc)npc.active=false;
                    if(replayRotation!=null)p.fullRotation=rotation;
                    var n=Main.npc[2];n.SetDefaults(3);n.active=true;n.whoAmI=2;n.life=n.lifeMax=10000;n.knockBackResist=0;n.defense=0;n.aiStyle=-1;n.noGravity=true;n.Center=p.Center+new Vector2(mirror?-distance:distance,0);n.immune[p.whoAmI]=immunity;
                    var path=Path(n);var ammo=host.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true).GetMethod("Capture",Flags).Invoke(null,new object[]{p,p.HeldItem});
                    var sample=Terraria.ID.ContentSamples.ProjectilesByType[(int)Get(ammo,"Projectile")];var origin=(Vector2)host.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels",true).GetMethod("LaunchOrigin",Flags,null,new[]{typeof(Player),typeof(Item)},null).Invoke(null,new object[]{p,p.HeldItem});
                    var contacts=new AttackContact[2];var native=new int[2];var elapsed=new double[2];
                    for(int version=0;version<2;version++)
                    {
                        Console.WriteLine("FIXED-SKY before mirror="+mirror+" distance="+distance+" immune="+immunity+" version="+version+" GUC="+Main.GameUpdateCount+" sample="+path.SampleTick+" player="+p.position+" direction="+p.direction+" origin="+origin+" receiver="+n.position+" life="+n.life+" liveImmune="+n.immune[p.whoAmI]+" activeShots="+Main.projectile.Count(q=>q.active));
                        Console.WriteLine("FIXED-SKY pose rotation="+p.fullRotation+" rotationOrigin="+p.fullRotationOrigin+" gfxOffY="+p.gfxOffY+" center="+p.Center+" mounted="+p.MountedCenter+" size="+p.width+"/"+p.height+" gravity="+p.gravDir+" screen="+Main.screenPosition+" receiverSize="+n.width+"/"+n.height+" sample503="+sample.type+"/"+sample.extraUpdates+"/"+sample.penetrate+"/"+sample.maxPenetrate+" localPolicy="+sample.usesLocalNPCImmunity+"/"+sample.localNPCHitCooldown+"/"+sample.localNPCImmunity[n.whoAmI]+" static="+sample.usesIDStaticNPCImmunity+" tile="+sample.tileCollide);
                        legacy=version==0;var terrain=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");
                        var root=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,sample,path,0,true,false},null);
                        var watch=System.Diagnostics.Stopwatch.StartNew();using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))contacts[version]=(AttackContact)model.GetMethod("Solve",Flags).Invoke(null,new object[]{p,p.HeldItem,ammo,path,origin,0,terrain,true,root,0});watch.Stop();elapsed[version]=watch.Elapsed.TotalMilliseconds;
                        var found=contacts[version];Console.WriteLine("FIXED-SKY solution version="+version+" present="+(found!=null)+(found==null?"":" aim="+new Vector2(found.AimX,found.AimY)+" tick="+found.Tick+" receive="+found.ReceiveTick+" sub="+found.Subupdate));
                        Console.WriteLine("FIXED-SKY inverse observed="+inverseObserved+" present="+(inverseContact!=null)+(inverseContact==null?"":" aim="+new Vector2(inverseContact.AimX,inverseContact.AimY)+" tick="+inverseContact.Tick+" sub="+inverseContact.Subupdate)+" finalReplay="+finalReplayObserved+" aim="+replayAim+" origin="+replayOrigin+" velocity="+replayVelocity+" tick="+(finalReplayContact?.Tick.ToString()??"null"));
                        if(contacts[version]==null)continue;
                        foreach(var shot in Main.projectile)shot.active=false;n.life=10000;n.immune[p.whoAmI]=immunity;
                        var launch=new object[]{p,p.HeldItem.type,origin,new Vector2(contacts[version].AimX,contacts[version].AimY),(float)Get(ammo,"Speed"),null,null};model.GetMethod("Launch",Flags).Invoke(null,launch);
                        var birth=(Vector2)launch[5];var speed=(Vector2)launch[6];Console.WriteLine("FIXED-SKY launch version="+version+" center="+birth+" velocity="+speed+" gate="+Math.Min(contacts[version].AimY,p.Center.Y-200));
                        for(int tx=(int)(birth.X-12)/16;tx<=(int)(birth.X+12)/16;tx++)for(int ty=(int)(birth.Y-12)/16;ty<=(int)(birth.Y+12)/16;ty++){var tile=Main.tile[tx,ty];Console.WriteLine("FIXED-SKY birthTile="+tx+"/"+ty+" active="+tile.active()+" type="+tile.type+" liquid="+tile.liquid+"/"+tile.liquidType());}
                        int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),(Vector2)launch[5],(Vector2)launch[6],503,50,0,p.whoAmI,0,Math.Min(contacts[version].AimY,p.Center.Y-200));
                        for(int outer=1;outer<=120 && Main.projectile[slot].active;outer++)
                        {NativeQuickItemChecks.BeginWorldStep();if(n.immune[p.whoAmI]>0)n.immune[p.whoAmI]--;Main.projectile[slot].Update(slot);if(n.life<10000){native[version]=outer;break;}}
                        var actual=Main.projectile[slot];Console.WriteLine("FIXED-SKY native version="+version+" first="+native[version]+" GUC="+Main.GameUpdateCount+" active="+actual.active+" center="+actual.Center+" velocity="+actual.velocity+" life="+n.life+" immune="+n.immune[p.whoAmI]+" local="+actual.localNPCImmunity[n.whoAmI]+" left="+actual.timeLeft+" penetrate="+actual.penetrate);
                        Require(native[version]>0 && native[version]==contacts[version].Tick,"fixed sky representative original Update first Damage agrees with exact receive clock version="+version);
                    }
                    Console.WriteLine("FIXED-SKY mirror="+mirror+" distance="+distance+" immune="+immunity+" old/new-native="+native[0]+"/"+native[1]+" elapsed-ms="+elapsed[0]+"/"+elapsed[1]);cases++;
                    Require(contacts[0]!=null && contacts[1]!=null && native[1]<=native[0],"fixed sky preserves every baseline useful source with no later first native receive");
                }
                // One separate ordinary opportunity reproduces the sealed
                // legal pose with the actual owner budget. The twelve paired
                // launches above remain mechanism evidence, not Shoot receipts.
                if(replayRotation!=null && rotation==.4f)Ordinary(context);
            }
            finally{legacy=insideFixed=ordinaryShoot=false;ordinaryBirths.Clear();inverseContact=finalReplayContact=null;audit.UnpatchAll(audit.Id);}
            Console.WriteLine("FIXED-SKY actual-native-pairs="+cases);
        }
        private static void Ordinary(object context)
        {
            var combat=Get(context,"Combat");var observation=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");
            try
            {
            var p=NativeCombatBallisticCandidateChecks.Scene(context,3065,50,true);p.fullRotation=.4f;foreach(var npc in Main.npc)npc.active=false;
            var n=Main.npc[2];n.SetDefaults(3);n.active=true;n.whoAmI=2;n.life=n.lifeMax=10000;n.knockBackResist=0;n.defense=0;n.aiStyle=-1;n.noGravity=true;n.Center=p.Center+new Vector2(-160,0);Array.Clear(n.immune,0,n.immune.Length);
            var raw=n.Center;NativeToolExecutionChecks.Sample(context,input,raw,true);Call(combat,"Sample");NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,false,false,25,false,true));
            var work=(CombatPredictionWork)Get(observation,"PredictionWork");var random=Main.rand;var fields=random.GetType().GetFields(Flags).Where(f=>!f.IsStatic).ToArray();var values=fields.Select(f=>f.GetValue(random) is Array a?(object)a.Clone():f.GetValue(random)).ToArray();
            double allowance=(long)Get(work,"allowance")*1000d/System.Diagnostics.Stopwatch.Frequency;
            Require(Math.Abs(allowance-6)<.001,"fixed sky feedback uses the original six millisecond owner");
            double before=work.UsedMilliseconds;bool exceededBefore=work.Exceeded;uint tick=Main.GameUpdateCount;int mouseX=Main.mouseX,mouseY=Main.mouseY;
            // Exactly one existing ordinary PrepareAction; no mechanism scope,
            // retry, sample refresh or enlarged allowance grants this input.
            NativeCombatAimChecks.PrepareBeforeNpc(observation,attack,n,0);
            var solution=NativeCombatAimChecks.ReadSolution(attack);var capability=NativeCombatAimChecks.QueryInput(attack,p);
            Console.WriteLine("FIXED-SKY ORDINARY GUC="+tick+" rotation="+p.fullRotation+" origin="+p.RotatedRelativePoint(p.MountedCenter)+" target="+n.position+" raw="+raw+" beforeMs="+before+" exceededBefore="+exceededBefore+" afterPrepareMs="+work.UsedMilliseconds+" exceeded="+work.Exceeded+" hasPoint="+solution.HasPoint+" receive="+solution.ReceiveTick+" phase="+solution.Phase+" kind="+capability.Kind+" allowed="+capability.Allowed+" point="+solution.Point+" attempts=1 allowanceMs="+allowance);
            Require(ReferenceEquals(random,Main.rand) && fields.Select((f,i)=>{var value=f.GetValue(random);return value is Array a && values[i] is Array b?a.Cast<object>().SequenceEqual(b.Cast<object>()):Equals(value,values[i]);}).All(v=>v),"ordinary fixed sky preparation preserves the original random stream without reseeding");
            Require(!work.Exceeded,"same legal rotated sky scene fits its original ordinary work allowance");
            solution=NativeCombatAimChecks.RequireSolution(attack,p,"same legal rotated sky scene retains actual current Enhanced within ordinary allowance");
            Require(!((bool)Get(attack,"hasConsumedInput")),"ordinary fixed sky input is not consumed before the native Shoot");ordinaryBirths.Clear();ordinaryShoot=true;
            try{typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});}finally{ordinaryShoot=false;}
            NativeCombatAimChecks.RequireConsumedInput(attack,p,solution,"ordinary fixed sky real Shoot consumes this current Enhanced source");
            Require(Main.GameUpdateCount==tick && Main.mouseX==mouseX && Main.mouseY==mouseY,"ordinary fixed sky real Shoot preserves clock and both physical cursor axes");
            Require(ordinaryBirths.Count==3 && ordinaryBirths.All(q=>q.Type==503 && q.Owner==p.whoAmI && q.Damage>0 && q.Velocity.Y>0 && q.Center.Y<=p.MountedCenter.Y-590),"ordinary fixed sky real Shoot retains all three original native sky births");
            foreach(var q in ordinaryBirths)Console.WriteLine("FIXED-SKY ORDINARY BIRTH type="+q.Type+" owner="+q.Owner+" center="+q.Center+" V="+q.Velocity+" damage="+q.Damage+" cursor="+q.MouseX+"/"+q.MouseY);
            Console.WriteLine("FIXED-SKY ORDINARY actualEnhanced=1 nativeBirths="+ordinaryBirths.Count+" afterShootMs="+work.UsedMilliseconds+" exceeded="+work.Exceeded+" nativeDamage=NOT_EVALUATED pairedLaunchesAreSeparate=True");
            }
            finally{ordinaryShoot=false;NativeCombatObservationChecks.Save(observation,new ObservationOptions());}
        }
    }
}
