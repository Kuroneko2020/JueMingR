using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatPhantasmEchoChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static Assembly assembly;private static Type model;private static Projectile parent,receiving;private static int selectedSlot=2,first,step;private static readonly List<Projectile> children=new List<Projectile>();
        private static bool traceBoundaries,boundaryCapture,observerFault,flightStrikeViolation;private static Projectile observed;private static Player boundaryOwner;private static int boundaryMode,aiCall;private static long boundarySample,firstGuc;private static int boundaryAge,boundaryFirst;private static readonly List<BoundaryFact> boundaryFacts=new List<BoundaryFact>();
        // These records own value copies only. Native callbacks neither solve,
        // print nor assert: a swallowed callback exception cannot manufacture a
        // harmless flight. The explicit switch changes observation only; both
        // original boundary cases and their success predicates always run.
        private sealed class ShotFact
        {
            internal int Slot,Key,Type,Style,Owner,Width,Height,Left,Extra,Damage,Alpha,Penetrate,MaxPenetrate,Immunity,LocalCooldown,StaticCooldown;internal bool Active,Tile,Local,Static;internal Vector2 Position,Velocity;internal float Scale;internal float[] Ai,LocalAi;internal int[] LocalImmune;internal uint StaticVictim,StaticSelected;
            internal static ShotFact Read(Projectile p)
            {
                if(p==null)return null;
                return new ShotFact{Slot=p.whoAmI,Key=(int)p.key,Type=p.type,Style=p.aiStyle,Owner=p.owner,Width=p.width,Height=p.height,Left=p.timeLeft,Extra=p.extraUpdates,Damage=p.damage,Alpha=p.alpha,Penetrate=p.penetrate,MaxPenetrate=p.maxPenetrate,Immunity=p.immunityIdentity,LocalCooldown=p.localNPCHitCooldown,StaticCooldown=p.idStaticNPCHitCooldown,Active=p.active,Tile=p.tileCollide,Local=p.usesLocalNPCImmunity,Static=p.usesIDStaticNPCImmunity,Position=p.position,Velocity=p.velocity,Scale=p.scale,Ai=(float[])p.ai.Clone(),LocalAi=(float[])p.localAI.Clone(),LocalImmune=(int[])p.localNPCImmunity.Clone(),StaticVictim=Projectile.perIDStaticNPCImmunity[p.immunityIdentity,1],StaticSelected=Projectile.perIDStaticNPCImmunity[p.immunityIdentity,2]};
            }
            public override string ToString()
            {return "slot="+Slot+" key="+Key+" type="+Type+" style="+Style+" owner="+Owner+" active="+Active+" p="+Position+" v="+Velocity+" size="+Width+"x"+Height+" scale="+Scale.ToString("R")+" left="+Left+" extra="+Extra+" damage="+Damage+" alpha="+Alpha+" pen="+Penetrate+"/"+MaxPenetrate+" tile="+Tile+" ai="+string.Join("/",Ai)+" local="+string.Join("/",LocalAi)+" policy="+Immunity+"/"+Local+"/"+Static+"/"+LocalCooldown+"/"+StaticCooldown+" localImmune="+Nonzero(LocalImmune)+" static1/2="+StaticVictim+"/"+StaticSelected;}
        }
        private sealed class NpcFact
        {
            internal int Slot,Generation,Type,NetId,Life,Max,Regen,RegenCount,RealLife,Style,Action,Direction,Target,Width,Height;internal bool Active,NoGravity,NoTile,Invulnerable,Immortal,Friendly,JustHit;internal Vector2 Position,Velocity,NetOffset;internal float[] Ai,LocalAi;internal int[] BuffType,BuffTime,Immune;
            internal static NpcFact Read(NPC n)
            {return new NpcFact{Slot=n.whoAmI,Generation=n.generation,Type=n.type,NetId=n.netID,Life=n.life,Max=n.lifeMax,Regen=n.lifeRegen,RegenCount=n.lifeRegenCount,RealLife=n.realLife,Style=n.aiStyle,Action=n.aiAction,Direction=n.direction,Target=n.target,Width=n.width,Height=n.height,Active=n.active,NoGravity=n.noGravity,NoTile=n.noTileCollide,Invulnerable=n.dontTakeDamage,Immortal=n.immortal,Friendly=n.friendly,JustHit=n.justHit,Position=n.position,Velocity=n.velocity,NetOffset=n.netOffset,Ai=(float[])n.ai.Clone(),LocalAi=(float[])n.localAI.Clone(),BuffType=(int[])n.buffType.Clone(),BuffTime=(int[])n.buffTime.Clone(),Immune=(int[])n.immune.Clone()};}
            public override string ToString()
            {return "id="+Slot+"/"+Generation+"/"+Type+"/"+NetId+" active="+Active+" p="+Position+" v="+Velocity+" size="+Width+"x"+Height+" net="+NetOffset+" life="+Life+"/"+Max+" regen="+Regen+"/"+RegenCount+" realLife="+RealLife+" ai="+Style+"/"+Action+"/"+string.Join("/",Ai)+" local="+string.Join("/",LocalAi)+" flags="+NoGravity+"/"+NoTile+"/"+Invulnerable+"/"+Immortal+"/"+Friendly+"/"+JustHit+" dir="+Direction+" target="+Target+" buffs="+Nonzero(BuffType)+":"+Nonzero(BuffTime)+" immune="+Nonzero(Immune);}
        }
        private sealed class BoundaryFact
        {
            internal string Event;internal long Guc,Sample;internal int Step,AiCall,Loop,Slot,Request,Result,LifeBefore;internal ShotFact Shot,Source;internal NpcFact Victim,Selected;internal Vector2 OwnerPosition,OwnerVelocity,RotationOrigin;internal float Rotation,Gravity;internal int Direction,Phantasm,Mana,First;internal long FirstGuc;
        }
        private static string Nonzero(int[] values)
        {return values.Length+"["+string.Join(",",values.Select((v,i)=>new{v,i}).Where(x=>x.v!=0).Select(x=>x.i+"="+x.v))+"]";}
        private static void Observe(string name,Projectile source=null,int slot=-1,int request=0,int result=0,int lifeBefore=0)
        {
            if(!boundaryCapture)return;
            try{boundaryFacts.Add(new BoundaryFact{Event=name,Guc=Main.GameUpdateCount,Sample=boundarySample,Step=step,AiCall=aiCall,Loop=Main.ProjectileUpdateLoopIndex,Slot=slot,Request=request,Result=result,LifeBefore=lifeBefore,Shot=ShotFact.Read(observed),Source=ShotFact.Read(source),Victim=NpcFact.Read(Main.npc[1]),Selected=NpcFact.Read(Main.npc[2]),OwnerPosition=boundaryOwner.position,OwnerVelocity=boundaryOwner.velocity,RotationOrigin=boundaryOwner.fullRotationOrigin,Rotation=boundaryOwner.fullRotation,Gravity=boundaryOwner.gravDir,Direction=boundaryOwner.direction,Phantasm=boundaryOwner.phantasmTime,Mana=boundaryOwner.statMana,First=first,FirstGuc=firstGuc});}catch{observerFault=true;}
        }
        private static void AiBefore(Projectile __instance){if(boundaryCapture && ReferenceEquals(__instance,observed)){aiCall++;Observe("AI.before",__instance);}}
        private static void AiAfter(Projectile __instance){if(boundaryCapture && ReferenceEquals(__instance,observed))Observe("AI.after",__instance);}
        private static void KillBefore(Projectile __instance){if(boundaryCapture && ReferenceEquals(__instance,observed))Observe("Kill.before",__instance);}
        private static void KillAfter(Projectile __instance){if(boundaryCapture && ReferenceEquals(__instance,observed))Observe("Kill.after",__instance);}
        private static void StrikeBefore(NPC __instance,int __0,out int __state)
        {__state=__instance.life;if(__instance.whoAmI==1 || __instance.whoAmI==2)Observe("Strike.before",receiving,__instance.whoAmI,__0,lifeBefore:__state);}
        private static void BeginBoundary(int mode,Player p,Projectile q)
        {boundaryMode=mode;boundaryOwner=p;observed=q;aiCall=0;boundarySample=-1;boundaryAge=boundaryFirst=-1;observerFault=false;boundaryFacts.Clear();boundaryCapture=traceBoundaries;Observe("initial");}
        private static void EndBoundary()
        {
            boundaryCapture=false;
            foreach(var f in boundaryFacts)Console.WriteLine("PHANTASM BOUNDARY mode="+boundaryMode+" event="+f.Event+" guc="+f.Guc+" sample="+f.Sample+" age/first="+boundaryAge+"/"+boundaryFirst+" step="+f.Step+" aiCall="+f.AiCall+" loop="+f.Loop+" strike="+f.Slot+"/"+f.Request+"/"+f.Result+" lifeBefore="+f.LifeBefore+" first="+f.First+" firstGUC="+f.FirstGuc+" owner="+f.OwnerPosition+"/"+f.OwnerVelocity+" rotation="+f.Rotation.ToString("R")+" origin="+f.RotationOrigin+" grav/dir="+f.Gravity+"/"+f.Direction+" phantom/mana="+f.Phantasm+"/"+f.Mana+" shot={"+f.Shot+"} source={"+f.Source+"} victim={"+f.Victim+"} selected={"+f.Selected+"}");
            if(traceBoundaries)Console.WriteLine("PHANTASM BOUNDARY end mode="+boundaryMode+" first="+first+" firstGUC="+firstGuc+" selectedLife="+Main.npc[2].life+" fault="+observerFault+" forbiddenFlightStrike="+flightStrikeViolation);
            observed=null;boundaryOwner=null;boundaryFacts.Clear();Require(!observerFault && !flightStrikeViolation,"native631 boundary observation is complete and every actual631 Strike is post-Kill");
        }
        private static object Static(Type type,string name,params object[] args){return type.GetMethod(name,Flags).Invoke(null,args);}
        private static void Birth(IEntitySource __0,int __result){if((__0 as EntitySource_Parent)?.Entity==parent && __result>=0 && __result<1000 && Main.projectile[__result].type==631)children.Add(Main.projectile[__result]);}
        private static void Damage(Projectile __instance,out Projectile __state)
        {__state=receiving;receiving=__instance;Observe("Damage.before",__instance);}
        private static Exception DamageDone(Projectile __instance,Projectile __state,Exception __exception)
        {Observe("Damage.after",__instance);receiving=__state;if(boundaryCapture && __exception!=null)observerFault=true;return __exception;}
        private static void Strike(NPC __instance,int __result,int __state)
        {
            if(__result>0 && __instance.whoAmI==selectedSlot && receiving?.type==631){if(receiving.localAI[1]!=1)flightStrikeViolation=true;if(first==0){first=step;firstGuc=Main.GameUpdateCount;}}
            if(__instance.whoAmI==1 || __instance.whoAmI==2)Observe("Strike.after",receiving,__instance.whoAmI,result:__result,lifeBefore:__state);
        }
        private static Player Reset(object context)
        {
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3854,0,0);p.position=new Vector2(850,646);p.velocity=Vector2.Zero;p.ResetEffects();p.phantasmTime=20;Main.windPhysics=false;
            foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;for(int x=35;x<115;x++)for(int y=10;y<65;y++)Main.tile[x,y].ClearEverything();children.Clear();first=step=0;firstGuc=-1;observerFault=false;flightStrikeViolation=false;selectedSlot=2;return p;
        }
        private static NPC Target(int slot,Vector2 center)
        {
            var n=Main.npc[slot];n.SetDefaults(3);
            // Vanilla SetDefaults retains the reused slot's buff arrays. These
            // dedicated static receivers start healthy: an earlier weapon's
            // DOT must not masquerade as this631's Damage or a flight hit.
            // This is setup only; native execution may add its real buffs.
            Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);
            Require(n.buffType.All(v=>v==0) && n.buffTime.All(v=>v==0) && n.lifeRegen==0 && n.lifeRegenCount==0,"dedicated Phantasm receiver starts without inherited buffs or accumulated DOT");
            n.whoAmI=slot;n.active=true;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;n.realLife=-1;n.friendly=n.immortal=n.dontTakeDamage=false;Array.Clear(n.immune,0,n.immune.Length);return n;
        }
        private static void RequireClock(AttackContact contact)
        {Require(firstGuc>=0 && firstGuc-contact.Timeline.SampleTick==contact.Tick && firstGuc-contact.Timeline.SampleTick==contact.ReceiveTick,"actual631 Strike GUC agrees with its dedicated BeforeNpc timeline");Console.WriteLine("PHANTASM absolute sample="+contact.Timeline.SampleTick+" strike="+firstGuc+" receive="+contact.ReceiveTick);}
        private static NpcTrajectory Timeline(NPC n)
        {
            var identity=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=identity,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(identity,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static AttackContact Known(Player p,Projectile[] sources,NPC n)
        {
            var path=Timeline(n);var phase=Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc");var clock=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new[]{(object)path,phase},null);if(boundaryCapture){boundarySample=path.SampleTick;boundaryAge=(int)Get(clock,"Age");boundaryFirst=(int)Get(clock,"FirstTick");}var terrain=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");var root=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,sources[0],path,0,true,false},null);return (AttackContact)Static(model,"KnownFamily",p,sources,path,clock,terrain,root);
        }
        private static void Advance(int limit)
        {
            // The BeforeNpc root owns this GUC's first NPC/projectile pass.
            // Later worlds alone advance GUC; the window still counts passes.
            for(step=1;step<=limit && first==0;step++)
            {
                if(step>1)NativeQuickItemChecks.BeginWorldStep();Observe("world.beforeNpc");
                foreach(var n in Main.npc)if(n.active){Observe("NPC.before",slot:n.whoAmI);n.UpdateNPC(n.whoAmI);Observe("NPC.after",slot:n.whoAmI);}
                for(int slot=0;slot<1000;slot++)if(Main.projectile[slot].active)
                {
                    Main.ProjectileUpdateLoopIndex=slot;
                    try{Observe("Update.before",Main.projectile[slot],slot);Main.projectile[slot].Update(slot);Observe("Update.after",Main.projectile[slot],slot);}finally{Main.ProjectileUpdateLoopIndex=-1;}
                    Require(!flightStrikeViolation,"original631 can Strike only in its post-Kill AI burst (checked after native Update)");
                }
                Observe("world.afterProjectiles");Require(!observerFault,"631 boundary callbacks copied a complete native observation");
            }
        }
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostPhantasmEchoAttack",true);var audit=new Harmony("JueMingR.Tests.PhantasmEcho");bool wind=Main.windPhysics;
            traceBoundaries=Environment.GetEnvironmentVariable("JMR_PHANTASM_BOUNDARY_TRACE")=="1";
            try
            {
                // Cover partial installation as well as native/assertion exits.
                foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
                var birth=typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float));audit.Patch(birth,postfix:new HarmonyMethod(typeof(NativeCombatPhantasmEchoChecks),nameof(Birth)));
                audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPhantasmEchoChecks),nameof(Damage)),finalizer:new HarmonyMethod(typeof(NativeCombatPhantasmEchoChecks),nameof(DamageDone)));audit.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),prefix:new HarmonyMethod(typeof(NativeCombatPhantasmEchoChecks),nameof(StrikeBefore)),postfix:new HarmonyMethod(typeof(NativeCombatPhantasmEchoChecks),nameof(Strike)));
                if(traceBoundaries)
                {
                    audit.Patch(typeof(Projectile).GetMethod("AI",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPhantasmEchoChecks),nameof(AiBefore)),postfix:new HarmonyMethod(typeof(NativeCombatPhantasmEchoChecks),nameof(AiAfter)));
                    audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPhantasmEchoChecks),nameof(KillBefore)),postfix:new HarmonyMethod(typeof(NativeCombatPhantasmEchoChecks),nameof(KillAfter)));
                }
                Births(context);Receive(context);Boundaries(context);Console.WriteLine("PASS PHANTASM ECHO real arrow OnHit3x631, sealed victim/waits, no flight Damage and actual terminal side receive.");
            }
            finally
            {
                boundaryCapture=traceBoundaries=false;observerFault=flightStrikeViolation=false;observed=receiving=parent=null;boundaryOwner=null;boundaryFacts.Clear();children.Clear();Main.windPhysics=wind;Main.ProjectileUpdateLoopIndex=-1;audit.UnpatchAll(audit.Id);
            }
        }
        private static void Births(object context)
        {
            foreach(int type in new[]{1,91,117,639})
            {
                var p=Reset(context);var n=Target(2,new Vector2(1100,667));parent=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),n.Center,Vector2.UnitX*8,type,60,0,p.whoAmI)];parent.Damage();
                Require(children.Count==3 && children.Select(q=>q.ai[1]).SequenceEqual(new[]{0f,15f,30f}),"every qualifying actual arrow OnHit produces exactly3 native delayed echoes");
                foreach(var child in children)Require(child.ai[0]==n.whoAmI && child.damage==18 && p.Hitbox.Contains(child.Center.ToPoint()) && Math.Abs(child.velocity.Length()-6)<.001f,"native echo inherits real parent cause,30%pre-final-OnHit damage, legal owner-body birth and locked victim");
                Require(n.life<10000,"echo birth follows a real primary Strike, never a hypothetical first shot");
            }
            Console.WriteLine("PASS PHANTASM native4 arrow OnHit families, original3 source children /0,15,30 delays");
        }
        private static void Receive(object context)
        {
            var p=Reset(context);var n=Target(2,new Vector2(1100,667));parent=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),n.Center,Vector2.UnitX*8,1,60,0,p.whoAmI)];parent.Damage();parent.Kill();p.phantasmTime=0;Array.Clear(n.immune,0,n.immune.Length);var source=children.ToArray();var expected=Known(p,source,n);Require(expected!=null,"actual already-born631 retains its sealed later receive after owner phantom timer expires");Advance(90);RequireClock(expected);Require(first==expected.Tick,"original AI122 wait/movement/pre-move burst yields identical first echo Strike");Console.WriteLine("PHANTASM actual631 predicted="+expected.Tick+" nativeFirst="+first);
        }
        private static void Boundaries(object context)
        {
            int failures=0;
            var p=Reset(context);p.phantasmTime=0;var victim=Target(1,new Vector2(1300,667));var selected=Target(2,new Vector2(1050,667));var q=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(900,667),Vector2.UnitX*6,631,30,0,p.whoAmI,1,15)];BeginBoundary(0,p,q);
            try
            {
                var expected=Known(p,new[]{q},selected);Observe("Known.after");Require(expected==null,"passing through a different receiver does not grant631 in-flight damage or retarget");Advance(90);Observe("final");
                if(!(first==0 && selected.life==10000))failures++;
            }
            finally{EndBoundary();}
            p=Reset(context);p.phantasmTime=0;victim=Target(1,new Vector2(1100,667));victim.dontTakeDamage=true;selected=Target(2,victim.Center);q=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),victim.Center,Vector2.Zero,631,30,0,p.whoAmI,1,30)];BeginBoundary(1,p,q);
            try
            {
                var expected=Known(p,new[]{q},selected);Observe("Known.after");Require(expected!=null && expected.Tick==1,"active invulnerable locked target still triggers native terminal burst before its wait, allowing an adjacent real receiver");Advance(1);RequireClock(expected);Observe("final");
                if(!(first==1 && selected.life<10000 && victim.life==10000))failures++;
            }
            finally{EndBoundary();}
            // Both original cases are observed, but either original predicate
            // still makes the complete boundary scope RED.
            Require(failures==0,"actual631 passes selected body without Strike before its locked endpoint; original active-only lock and post-Kill Damage preserve the real side-receive counterexample (failures="+failures+")");
            Console.WriteLine("PASS PHANTASM no flight damage / active invulnerable lock / terminal adjacent receive");
        }
    }
}
