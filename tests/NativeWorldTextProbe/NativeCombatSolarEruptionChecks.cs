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
    internal static class NativeCombatSolarEruptionChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static Projectile parent,receiving;private static int step,first,kind;private static long firstGuc;private static readonly List<Projectile> children=new List<Projectile>();
        private static object Static(Type t,string name,params object[] args){return t.GetMethod(name,Flags).Invoke(null,args);}
        private static void Birth(IEntitySource __0,int __result){if((__0 as EntitySource_Parent)?.Entity==parent && __result>=0 && __result<1000 && Main.projectile[__result].type==612)children.Add(Main.projectile[__result]);}
        private static void Damage(Projectile __instance){receiving=__instance;}private static void DamageDone(){receiving=null;}
        private static void Strike(NPC __instance,int __result){if(__result>0 && __instance.whoAmI==2 && receiving!=null && (receiving.type==611 || receiving.type==612) && first==0){first=step;firstGuc=Main.GameUpdateCount;kind=receiving.type;}}
        private static void RequireClock(AttackContact contact)
        {Require(firstGuc>=0 && firstGuc-contact.Timeline.SampleTick==contact.Tick && firstGuc-contact.Timeline.SampleTick==contact.ReceiveTick,"Solar actual Strike GUC agrees with the dedicated timeline receive index");Console.WriteLine("SOLAR absolute sample="+contact.Timeline.SampleTick+" strike="+firstGuc+" receive="+contact.ReceiveTick);}
        private static Player Reset(object context)
        {NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3473,0,0);p.position=new Vector2(850,646);p.velocity=Vector2.Zero;p.ResetEffects();p.itemAnimationMax=p.itemAnimation=p.HeldItem.useAnimation;Main.windPhysics=false;foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;for(int x=35;x<115;x++)for(int y=10;y<65;y++)Main.tile[x,y].ClearEverything();for(int x=35;x<115;x++)NativeToolsChecks.Tile(x,43,1);children.Clear();first=step=kind=0;firstGuc=-1;return p;}
        private static NPC Target(int slot,Vector2 center)
        {var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;n.realLife=-1;n.friendly=n.immortal=n.dontTakeDamage=false;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static NpcTrajectory Timeline(NPC n)
        {var identity=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=identity,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(identity,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);}
        private static object Terrain(){var t=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static AttackContact Known(Player p,Projectile[] sources,NPC n,out object root)
        {var path=Timeline(n);var phase=Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc");var clock=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new[]{(object)path,phase},null);root=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,sources[0],path,0,true,false},null);return (AttackContact)Static(model,"KnownFamily",p,sources,path,clock,Terrain(),root);}
        private static Projectile Shoot(Player p)
        {Main.mouseX=1300-(int)Main.screenPosition.X;Main.mouseY=667-(int)Main.screenPosition.Y;typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});return Main.projectile.Single(q=>q.active && q.type==611);}
        private static void Advance(int maximum,bool beforeNpc=true)
        {
            // A BeforeNpc root owns the current GUC's NPC/projectile pass.
            // The real CompletedWorld hand consumer instead starts next world.
            for(step=1;step<=maximum && first==0;step++){if(step>1 || !beforeNpc)NativeQuickItemChecks.BeginWorldStep();foreach(var n in Main.npc)if(n.active)n.UpdateNPC(n.whoAmI);for(int slot=0;slot<1000;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;try{Main.projectile[slot].Update(slot);}finally{Main.ProjectileUpdateLoopIndex=-1;}}}
        }
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostSolarEruptionAttack",true);var audit=new Harmony("JueMingR.Tests.SolarEruption");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatSolarEruptionChecks),nameof(Birth)));audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSolarEruptionChecks),nameof(Damage)),postfix:new HarmonyMethod(typeof(NativeCombatSolarEruptionChecks),nameof(DamageDone)));audit.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatSolarEruptionChecks),nameof(Strike)));
            try{Motors(context);Births(context);Receive(context);Consumer(context);RegisteredPreview(context);Console.WriteLine("PASS SOLAR ERUPTION actual611 hand/OnHit612, exact finite motors and owner4/local6 receive.");}finally{Main.windPhysics=wind;Main.ProjectileUpdateLoopIndex=-1;audit.UnpatchAll(audit.Id);}
        }
        private static void Motors(object context)
        {
            foreach(float arc in new[]{-.3f,0f,.3f})
            {
                var p=Reset(context);parent=Shoot(p);parent.ai[1]=arc;var q=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostNebulaBlazeAttack",true),"Copy",parent);var body=(Player)typeof(object).GetMethod("MemberwiseClone",Flags).Invoke(p,null);
                for(int action=0;action<29;action++){Static(model,"Parent",q,body);parent.Update(parent.whoAmI);Require(Vector2.Distance(parent.position,q.position)<.002f && Vector2.Distance(parent.velocity,q.velocity)<.002f && parent.ai[0]==q.ai[0] && parent.localAI[0]==q.localAI[0],"native611 original AI075+movement matches its sealed random arc and arm pose each action");}
            }
            foreach(float scale in new[]{.85f,1.425f,1.99f})
            {var p=Reset(context);var original=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(1100,350),Vector2.Zero,612,50,10,p.whoAmI,0,scale)];var q=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostNebulaBlazeAttack",true),"Copy",original);for(int action=0;action<3*Main.projFrames[612];action++){Static(model,"Child",q);original.Update(original.whoAmI);Require(original.active==q.active && original.width==q.width && original.height==q.height && original.position==q.position && original.ai[0]==q.ai[0] && original.ai[1]==q.ai[1],"native612 first-AI52scale resize, repeated Damage and frame retirement preserve full random range");if(!q.active)break;q.timeLeft--;}}
            Console.WriteLine("PASS SOLAR original3 parent arcs x29 +3 complete612 clocks/scales");
        }
        private static void Births(object context)
        {
            var p=Reset(context);var n=Target(2,new Vector2(1000,667));parent=Shoot(p);parent.ai[1]=0;for(int action=0;action<10 && children.Count==0;action++)parent.Update(parent.whoAmI);Require(children.Count>0 && n.life<10000,"a genuine611 original hand first receives before its own EntitySource child birth");var child=children[0];Require(child.damage==parent.damage && child.knockBack==10 && child.ai[1]>=.85f && child.ai[1]<2 && child.velocity==Vector2.Zero && child.Center==n.Center && n.immune[p.whoAmI]==4 && parent.localNPCImmunity[2]==6,"native OnHit612 exact source damage/10KB/random scale/body and simultaneous owner4/local6");int count=children.Count;Array.Clear(n.immune,0,n.immune.Length);Array.Clear(parent.localNPCImmunity,0,parent.localNPCImmunity.Length);parent.Damage();Require(children.Count==count && parent.localAI[1]==4,"native localAI1 cooldown forbids another child despite explicit receive eligibility");
        }
        private static void Receive(object context)
        {
            var p=Reset(context);var n=Target(2,new Vector2(1000,667));parent=Shoot(p);object root;var expected=Known(p,new[]{parent},n,out root);Require(expected!=null,"actual born611 retains its complete native body receive");Advance(30);RequireClock(expected);Require(first==expected.Tick && kind==611,"actual original611 first Strike agrees with full family replay");Console.WriteLine("SOLAR original parent predicted="+expected.Tick+" native="+first);
            p=Reset(context);n=Target(2,new Vector2(1000,667));parent=Shoot(p);for(int slot=0;slot<1000;slot++)if(!Main.projectile[slot].active){var filler=Main.projectile[slot];filler.SetDefaults(1);filler.whoAmI=slot;filler.owner=p.whoAmI;filler.active=true;filler.damage=0;filler.position=new Vector2(600,350);filler.velocity=Vector2.Zero;filler.timeLeft=3600;}Require(Known(p,new[]{parent},n,out root)!=null,"a full pool cannot erase611 primary Strike merely because its later612 cannot be allocated");
            p=Reset(context);var victim=Target(1,new Vector2(1100,350));n=Target(2,new Vector2(1100,380));parent=Shoot(p);parent.Center=victim.Center;parent.velocity=Vector2.UnitX;parent.Damage();Require(children.Count>0 && n.life==10000,"real611 hit on a different receiver creates its own burst without a primary selected hit");var child=children[0];parent.active=false;Array.Clear(victim.immune,0,victim.immune.Length);expected=Known(p,new[]{child},n,out root);Require(expected!=null,"actual sealed612 born on another receiver can reach the adjacent selected body");Advance(30);RequireClock(expected);Require(first==expected.Tick && kind==612,"original611 causal child produces the selected later Strike with exact immunity order");Console.WriteLine("SOLAR original child predicted="+expected.Tick+" native="+first);
            p=Reset(context);n=Target(2,new Vector2(1000,667));parent=Shoot(p);Known(p,new[]{parent},n,out root);p.velocity=Vector2.UnitX;Require(!(bool)Get(root,"Unchanged") && !(bool)Get(root,"BirthInputsCurrent"),"capturedSolar owner pose really reaches the shared complete and lightweight root leases");
        }
        private static void Consumer(object context)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");
            for(int mode=0;mode<3;mode++)
            {
                var p=Reset(context);p.position=new Vector2(650,646);p.itemAnimation=p.itemTime=0;p.releaseUseItem=true;Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);var n=Target(2,p.Center+new Vector2(150,0));n.width=100;n.height=80;n.Center=p.Center+new Vector2(150,0);var raw=n.Center-new Vector2(450,0);
                Projectile ambient=null;if(mode==2)ambient=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(1100,350),Vector2.Zero,612,0,0,p.whoAmI,0,1.4f)];
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,true));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),raw,true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,0);var prepared=(AttackContact)GetOptional(attack,"ExpectedImpact");var captured=GetOptional(attack,"obstacles");Require(prepared!=null && captured!=null,"real HostAttackAim611 preparation publishes a complete family root, not only a direct-helper contact");
                if(mode==1)p.velocity=Vector2.UnitX;if(mode==2)ambient.ai[0]++;if(mode!=0)Require(!(bool)Get(captured,"Unchanged"),"same-GUC owner/family edit revokes the actual prepared611 root before native consumption");
                int mouse=Main.mouseX;typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});Require(Main.mouseX==mouse && Main.projectile.Any(q=>q.active && q.type==611),"real611 native hand and physical cursor restoration survive guarded preparation");
                if(mode==0){Require((bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced","actual native611 Shoot consumes its real prepared enhanced input");Advance(30,false);Require(first>0,"actual enhanced native611 hand reaches a real Strike");Console.WriteLine("SOLAR native consumer Enhanced first="+first+" strikeGUC="+firstGuc+" (1000ms mechanism component allowance)");}
            }
            Console.WriteLine("PASS SOLAR real preparation/normal Shoot Enhanced+Strike and same-GUC owner/family root guards");
        }
        private static void RegisteredPreview(object context)
        {
            var p=Reset(context);var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),new Vector2(1300,667),true);Call(combat,"Sample");parent=Shoot(p);var control=Get(attack,"Control");var registered=(System.Collections.IDictionary)Get(control,"shots");Require(registered.Contains((int)parent.key),"genuine native611 birth is registered by its source even when Aim is off");
            var copy=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostNebulaBlazeAttack",true),"Copy",parent);var body=(Player)typeof(object).GetMethod("MemberwiseClone",Flags).Invoke(p,null);Static(model,"Parent",copy,body);var unit=copy.velocity.SafeNormalize(Vector2.UnitX);var victim=Target(1,copy.Center+unit*28);var n=Target(2,victim.Center+new Vector2(-unit.Y,unit.X)*24);n.width=n.height=8;n.Center=victim.Center+new Vector2(-unit.Y,unit.X)*24;
            var path=Timeline(n);var phase=Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc");AttackContact plan;using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))plan=(AttackContact)Call(control,"Preview",path,phase,0,false);Require(plan!=null && plan.Confidence==AttackConfidence.Representative,"registered611 preview retains a useful future612 born on another receiver, outside611 shaft");var root=GetOptional(control,"previewObstacles");var dependencies=Call(control,"CapturePreviewDependencies",plan);Require(root!=null && dependencies!=null && (bool)Call(dependencies,"Current",p),"registered Solar preview retains its actual complete family/terrain dependency owner");p.velocity=Vector2.UnitX;Require(!(bool)Call(dependencies,"Current",p),"same-GUC registered611 owner pose change revokes captured preview");p.velocity=Vector2.Zero;
            parent.ai[0]++;Require(!(bool)Call(dependencies,"Current",p),"same-GUC registered Solar family action edit revokes captured preview");parent.ai[0]--;Advance(12);RequireClock(plan);Require(children.Count>0 && first==plan.Tick && kind==612,"actual registered611 hits another receiver, then its own612 delivers predicted selected Strike");Console.WriteLine("PASS SOLAR registered611 preview causal612 predicted="+plan.Tick+" native="+first+" AimOFF, no cursor capability");
        }
    }
}
